using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Monde de mission. Tous les pairs construisent la même carte ; l'hôte simule les ennemis,
/// les caisses, la destruction et le déroulé de mission, puis diffuse les résultats.
/// </summary>
public partial class World : Node3D
{
    public static World Current { get; private set; }

    public MissionDef Mission;
    public Difficulty Difficulty;
    public int Seed;
    public RandomNumberGenerator Rng = new();
    public Node3D FxRoot;
    public Hud Hud;
    public Player LocalPlayer;
    public readonly Dictionary<long, RemotePlayer> Remotes = new();
    public readonly Dictionary<int, Enemy> EnemiesById = new();
    public readonly Dictionary<int, Crate> CratesById = new();
    public readonly List<Building> Buildings = new();
    public FlowField Flow = new();
    public CityBuilder.Result City;
    /// <summary>Mobilier et véhicules destructibles de la carte (identifiants dans l'ordre de génération).</summary>
    public Destructibles Decor;
    public bool Paused;
    public bool Ended;
    public float Elapsed;
    public float DamageMul => Difficulties.EnemyDamage[(int)Difficulty];
    public float HealthMul => Difficulties.EnemyHealth[(int)Difficulty] * (1f + 0.35f * (Net.I.Players.Count - 1));

    // Butin d'équipe ramassé pendant la mission.
    public readonly int[] CratesCollected = new int[Crates.Count];
    /// <summary>Éliminations par joueur (0 = alliés PNJ et décor). Tenu par l'hôte, diffusé à tous.</summary>
    public readonly Dictionary<long, int> KillsBy = new();
    private float _scoreTimer;
    public int TeamKills;
    public string ObjectiveText = "";
    public int EnemiesRemaining;
    public float BossHealth = -1;
    public string BossName = "";

    private MissionDirector _director;
    private WeatherFx _weather;
    public AllyManager Allies;
    /// <summary>Objectif de mission (structure, convoi, minuteurs de survie et d'extraction), sur chaque pair.</summary>
    public MissionObjective Objective;
    /// <summary>Véhicules pilotables par identifiant (char 1, hélicoptère 2 : identiques chez tous les pairs).</summary>
    public readonly Dictionary<int, Vehicle> Vehicles = new();
    private int _nextEnemyId = 1;
    private int _nextCrateId = 1;
    private float _snapshotTimer;
    private float _flowTimer;
    private float _objectiveTimer;
    private readonly Dictionary<long, bool> _downed = new();
    private PauseMenu _pause;
    private WorldEnvironment _env;
    public Godot.Environment Env => _env?.Environment;
    public DirectionalLight3D Sun => _sun;
    private DirectionalLight3D _sun;

    public event Action<bool> Finished;

    public static World Create(int mission, Difficulty d, int seed)
    {
        var w = new World { Mission = Missions.All[mission], Difficulty = d, Seed = seed, Name = "World" };
        w.Rng.Seed = (ulong)seed;
        return w;
    }

    public override void _EnterTree() => Current = this;

    public override void _ExitTree()
    {
        if (Current == this) Current = null;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public override void _Ready()
    {
        FxRoot = new Node3D { Name = "Fx" };
        AddChild(FxRoot);
        ExplosionFx.For(this); // émetteurs et textures prêts avant le premier tir (pas d'à-coup à la première explosion)
        BuildEnvironment();
        var buildClock = System.Diagnostics.Stopwatch.StartNew();
        Decor = Destructibles.Begin(this);
        City = Mission.Map switch { MapId.Port => PortBuilder.Build(this, Mission.Time), MapId.Suburb => SuburbBuilder.Build(this, Mission.Time), MapId.Business => BusinessBuilder.Build(this, Mission.Time), _ => CityBuilder.Build(this, Mission.Time) };
        Decor.EndBuild();
        if (Screenshot.CmdArg("fps") != null) GD.Print($"[carte] {Mission.Map} construite en {buildClock.ElapsedMilliseconds} ms");
        Buildings.AddRange(City.Buildings);
        foreach (var b in Buildings) Flow.SetBlockedRect(b.GlobalPosition, b.Size, true);
        Flow.BlockOutside(CityBuilder.Half, CityBuilder.SeaStart);

        // Joueurs : ordre stable par identifiant.
        var ids = Net.I.Players.Keys.OrderBy(i => i).ToList();
        if (ids.Count == 0) ids.Add(Net.I.MyId);
        for (var i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            var spawn = City.PlayerSpawns[i % City.PlayerSpawns.Count];
            if (id == Net.I.MyId)
            {
                var cls = Profile.I.Data.SelectedClass;
                var armor = Profile.I.Armor(cls);
                var cap = Difficulties.OnlineArmorCap[(int)Difficulty];
                var capped = !Net.I.IsSolo && cap > 0 && armor > cap;
                if (capped) armor = cap;
                LocalPlayer = Player.Create(this, cls, Profile.I.Equipped(cls), armor);
                if (capped) CallDeferred(MethodName.CapNotice, cap);
                AddChild(LocalPlayer);
                LocalPlayer.GlobalPosition = spawn;
                LocalPlayer.SetYaw(0);
                PrewarmFade(LocalPlayer.Camera);
                BugModel.PrewarmMachines(LocalPlayer.Camera, Mission);
            }
            else if (Net.I.Players.TryGetValue(id, out var info))
            {
                var r = RemotePlayer.Create(info, spawn);
                AddChild(r);
                Remotes[id] = r;
            }
        }

        if (Mission.Weather != Weather.Clear) _weather = WeatherFx.Create(this, Mission.Weather);

        Hud = new Hud { World = this };
        AddChild(Hud);
        _pause = new PauseMenu { World = this };
        AddChild(_pause);
        Input.MouseMode = Input.MouseModeEnum.Captured;

        Allies = new AllyManager(this);
        // Char de combat et hélicoptère d'attaque garés près du point de départ (mêmes identifiants chez tous les pairs).
        AddVehicle(Tank.Create(this, 1, new Vector3(14, 0.3f, 22), Mathf.Pi));
        AddVehicle(Helicopter.Create(this, 2, new Vector3(-16, 0.3f, 24), Mathf.Pi));
        Objective = MissionObjective.Create(this);
        if (Net.I.IsHost)
        {
            _director = new MissionDirector(this);
            // Escouades alliées : une près du point de départ, une en avant.
            Allies.HostSpawnSquad(new Vector3(0, 0, 18), Mission.Index == 1 ? 4 : 3);
            if (Mission.Index != 1) Allies.HostSpawnSquad(new Vector3(-25, 0, -25), 3);
        }
        Hud.Briefing(Mission);
        Settings.I.Changed += ApplyGraphics;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete && Settings.I != null) Settings.I.Changed -= ApplyGraphics;
    }

    // ------------------------------------------------------------------ Environnement

    private void BuildEnvironment()
    {
        var env = new Godot.Environment
        {
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Agx,
            TonemapExposure = 1.0f,
            GlowEnabled = true,
            GlowIntensity = 0.6f,
            GlowBloom = 0.05f,
            GlowHdrThreshold = 1.1f,
            FogEnabled = true,
            FogDensity = Mission.Fog,
            FogAerialPerspective = 0.6f,
            FogSkyAffect = 0.3f,
            AdjustmentEnabled = true,
            AdjustmentContrast = 1.08f,
            AdjustmentSaturation = 0.92f,
        };
        _sun = new DirectionalLight3D
        {
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 220f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            ShadowBlur = 1.2f,
        };
        Grade.Apply(env, _sun, Mission.Time, Mission.Weather, Mission.Fog);
        _env = new WorldEnvironment { Environment = env };
        AddChild(_env);
        AddChild(_sun);
        AddChild(Grade.Overlay());
        ApplyGraphics();
    }

    public void ApplyGraphics()
    {
        if (_env == null) return;
        var q = Settings.I.Quality;
        var env = _env.Environment;
        env.SsaoEnabled = q >= 1;
        env.SsaoRadius = 1.5f;
        env.SsaoIntensity = 1.8f;
        env.SsilEnabled = q >= 3;
        env.SsrEnabled = q >= 3;
        env.SdfgiEnabled = q >= 2;
        env.SdfgiUseOcclusion = true;
        env.SdfgiCascades = q >= 3 ? 6 : 4;
        env.VolumetricFogEnabled = q >= 2;
        env.VolumetricFogDensity = Mission.Time switch { TimeOfDay.Night => 0.012f, TimeOfDay.Dusk => 0.008f, _ => 0.005f };
        env.VolumetricFogAlbedo = env.FogLightColor;
        _sun.DirectionalShadowMaxDistance = q switch { 0 => 80f, 1 => 140f, 2 => 220f, _ => 320f };
        _sun.ShadowEnabled = true;
        RenderingServer.DirectionalSoftShadowFilterSetQuality(q >= 2 ? RenderingServer.ShadowQuality.SoftHigh : RenderingServer.ShadowQuality.SoftLow);
        if (LocalPlayer?.Camera != null) LocalPlayer.Camera.Fov = Settings.I.Fov;
    }

    /// <summary>
    /// Compile d'avance (pendant l'écran de déploiement) les variantes translucides des insectes (Enemy.CameraFade) :
    /// sans cela, le premier insecte traversé par la caméra provoque une saccade. Modèles minuscules devant la caméra, retirés ensuite.
    /// </summary>
    private void PrewarmFade(Camera3D cam)
    {
        var root = new Node3D { Name = "FadePrewarm", Position = new Vector3(0, 0, -4f), Scale = Vector3.One * 0.02f };
        cam.AddChild(root);
        foreach (var t in new[] { EnemyType.BlackAnt, EnemyType.Wasp, EnemyType.Drone })
        {
            var m = BugModel.Create(t);
            root.AddChild(m);
            m.SetFade(0.7f);
        }
        GetTree().CreateTimer(1.5f).Timeout += () => { if (IsInstanceValid(root)) root.QueueFree(); };
    }

    // ------------------------------------------------------------------ Boucle

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (!Ended) Elapsed += dt;
        if (Input.IsActionJustPressed("debug_screenshot")) Screenshot.Take(GetViewport(), "capture");

        if (Net.I.IsHost) { HostProcess(dt); Allies.HostTick(dt); }
        else
        {
            foreach (var e in EnemiesById.Values) e.ClientTick(dt);
            Allies.ClientTick(dt);
        }
        UpdateGrabs(); // victimes des fourmis rouges : dans les mandibules animées de cette image
        // Insectes géants : celui dans lequel entre la caméra devient translucide (le soldat et la visée restent lisibles).
        if (GetViewport().GetCamera3D() is { } cam)
        {
            var cp = cam.GlobalPosition;
            // Point suivi : le soldat (ou son véhicule), sinon un point devant la caméra.
            var focus = LocalPlayer != null && IsInstanceValid(LocalPlayer) ? LocalPlayer.GlobalPosition + Vector3.Up * 1.3f : cp - cam.GlobalBasis.Z * 3f;
            foreach (var e in EnemiesById.Values) e.CameraFade(cp, focus);
        }

        if (LocalPlayer != null && !LocalPlayer.Downed) LocalPickupCheck();
        FlushDamageNumbers(dt);
    }

    private void HostProcess(float dt)
    {
        foreach (var e in EnemiesById.Values.ToList()) e.HostTick(dt);
        _flowTimer -= dt;
        if (_flowTimer <= 0)
        {
            _flowTimer = 0.5f;
            // Les insectes convergent vers les soldats et vers l'objectif (structure défendue, camions du convoi).
            Flow.Compute(Objective != null ? AlivePlayerPositions().Concat(Objective.FlowTargets()) : AlivePlayerPositions());
            RebuildSpatialHash();
        }
        else RebuildSpatialHash();
        _director?.Tick(dt);
        Decor?.HostTick(dt);

        _snapshotTimer -= dt;
        if (_snapshotTimer <= 0 && !Net.I.IsSolo)
        {
            _snapshotTimer = 1f / 15f;
            SendSnapshots();
        }
        _objectiveTimer -= dt;
        if (_objectiveTimer <= 0)
        {
            _objectiveTimer = 0.25f;
            Net.I.BroadcastObjective(ObjectiveText, EnemiesRemaining, BossHealth, BossName);
        }
        // Objectif rempli : l'escouade à terre pendant l'extraction ne fait plus échouer la mission.
        if (!Ended && AllPlayersDown() && Elapsed > 3f && !(Objective?.Completed ?? false)) EndMission(false);
        _scoreTimer -= dt;
        if (_scoreTimer <= 0)
        {
            _scoreTimer = 2f;
            Net.I.BroadcastScores(ScoresString());
        }
    }

    private void SendSnapshots()
    {
        // Par paquets de 70 ennemis (18 octets chacun) pour rester sous la taille d'un datagramme.
        var list = EnemiesById.Values.Where(e => e.State != EnemyState.Dead && !e.IsStatic).ToList();
        const int perPacket = 1260 / Enemy.SnapshotBytes;
        for (var i = 0; i < list.Count; i += perPacket)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            foreach (var e in list.Skip(i).Take(perPacket)) e.PackSnapshot(w);
            w.Flush();
            Net.I.BroadcastEnemySnapshot(ms.ToArray());
        }
    }

    private void CapNotice(float cap) => Hud?.Toast($"Armure plafonnée à {cap:0} PV pour cette difficulté en multijoueur.");

    public void TogglePause()
    {
        Paused = !Paused;
        _pause.Visible = Paused;
        Input.MouseMode = Paused ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        if (Net.I.IsSolo) GetTree().Paused = Paused;
    }

    // ------------------------------------------------------------------ Joueurs

    public IEnumerable<Vector3> AlivePlayerPositions()
    {
        if (LocalPlayer != null && !LocalPlayer.Downed) yield return LocalPlayer.GlobalPosition;
        foreach (var r in Remotes.Values)
            if (!r.Downed) yield return r.GlobalPosition;
    }

    public bool NearestPlayer(Vector3 pos, out long peer, out Vector3 at)
    {
        peer = 0;
        at = Vector3.Zero;
        var best = float.MaxValue;
        if (LocalPlayer != null && !LocalPlayer.Downed)
        {
            best = pos.DistanceSquaredTo(LocalPlayer.GlobalPosition);
            peer = Net.I.MyId;
            at = LocalPlayer.GlobalPosition;
        }
        foreach (var (id, r) in Remotes)
        {
            if (r.Downed) continue;
            var d = pos.DistanceSquaredTo(r.GlobalPosition);
            if (d < best) { best = d; peer = id; at = r.GlobalPosition; }
        }
        return peer != 0;
    }

    // ------------------------------------------------------------------ Véhicules

    private void AddVehicle(Vehicle v)
    {
        AddChild(v);
        Vehicles[v.Id] = v;
    }

    /// <summary>Véhicule libre le plus proche à portée d'interaction.</summary>
    public Vehicle VehicleNear(Vector3 pos)
    {
        Vehicle best = null;
        var bestD = float.MaxValue;
        foreach (var v in Vehicles.Values)
        {
            if (v.Wrecked || v.Driver != 0 || v.Delivering) continue;
            var d = v.GlobalPosition.DistanceTo(pos);
            if (d <= v.InteractRadius && d < bestD) { best = v; bestD = d; }
        }
        return best;
    }

    /// <summary>Véhicule conduit par ce joueur (ou null).</summary>
    public Vehicle VehicleOf(long peer)
    {
        foreach (var v in Vehicles.Values)
            if (v.Driver == peer && !v.Wrecked) return v;
        return null;
    }

    /// <summary>Rayon d'impact supplémentaire d'un joueur à bord d'un véhicule (plus gros qu'un soldat).</summary>
    private float VehicleHitBonus(long peer) => VehicleOf(peer)?.HitRadius ?? 0f;

    public void OnVehicleState(int id, Vector3 pos, Vector3 rot, float a, float b, float c, float hp, float speed, int ammo)
    {
        if (Vehicles.TryGetValue(id, out var v) && !v.IsLocalDriver) v.ApplyState(pos, rot, a, b, c, hp, speed, ammo);
    }

    public void OnVehicleDriver(int id, long driver)
    {
        if (!Vehicles.TryGetValue(id, out var v)) return;
        var previous = v.Driver;
        v.Driver = driver;
        if (driver == Net.I.MyId && LocalPlayer != null) LocalPlayer.EnterVehicle(v);
        else if (previous == Net.I.MyId && LocalPlayer != null) LocalPlayer.ExitVehicle();
        Sfx.At("dash", v.GlobalPosition, -4f, 0.5f);
    }

    public void OnVehicleWrecked(int id)
    {
        if (!Vehicles.TryGetValue(id, out var v)) return;
        var driver = v.Driver;
        v.Wreck();
        if (driver == Net.I.MyId && LocalPlayer != null)
        {
            LocalPlayer.ExitVehicle();
            Hud?.Toast(v.WreckedHint);
        }
    }

    /// <summary>Le char écrase les insectes au sol devant lui (dégâts envoyés par le conducteur).</summary>
    public void TankCrush(Tank t, Vector3 at, float radius, float dmg)
    {
        foreach (var e in EnemiesById.Values)
        {
            if (e.State == EnemyState.Dead || e.Def.Flying || e.IsStatic || e.IsBoss) continue;
            if (e.GlobalPosition.DistanceTo(at) > radius + e.Def.Size
                && (e.Def.Type != EnemyType.Centipede || e.BodyDistance(at) > radius + e.Def.Size)) continue;
            Net.I.SendDamageEnemy(e.Id, dmg, e.Center, false);
        }
    }

    public bool PlayerPosition(long peer, out Vector3 pos)
    {
        pos = Vector3.Zero;
        if (peer == Net.I.MyId && LocalPlayer != null && !LocalPlayer.Downed) { pos = LocalPlayer.GlobalPosition; return true; }
        if (Remotes.TryGetValue(peer, out var r) && !r.Downed) { pos = r.GlobalPosition; return true; }
        return false;
    }

    private bool AllPlayersDown()
    {
        if (LocalPlayer != null && !LocalPlayer.Downed) return false;
        return Remotes.Values.All(r => r.Downed);
    }

    public long DownedTeammateNear(Vector3 pos, float radius)
    {
        foreach (var (id, r) in Remotes)
            if (r.Downed && r.GlobalPosition.DistanceTo(pos) <= radius) return id;
        return 0;
    }

    public void OnRemotePlayerState(long id, Vector3 pos, float yaw, float pitch, int flags, float hp, int weapon, Vector3 vel)
    {
        if (Remotes.TryGetValue(id, out var r)) r.ApplyState(pos, yaw, pitch, flags, hp, vel);
    }

    public void OnPlayerDowned(long id, bool downed)
    {
        _downed[id] = downed;
        if (id != Net.I.MyId && Remotes.TryGetValue(id, out var r))
        {
            r.Downed = downed;
            if (downed) Hud?.Toast($"{r.PlayerName} est à terre ! Approchez et maintenez E.");
        }
    }

    public void OnRevive(long target, long by)
    {
        if (target == Net.I.MyId && LocalPlayer != null && LocalPlayer.Downed)
        {
            LocalPlayer.Revived();
            Hud?.Toast("Vous avez été relevé !");
            Sfx.Ui("heal");
        }
    }

    /// <summary>Marqueurs actifs : position, type (0 lieu, 1 ennemi, 2 caisse), fin de vie.</summary>
    public readonly List<(Vector3 pos, int kind, float until, long from)> Pings = new();

    public void OnPing(long from, Vector3 pos, int kind)
    {
        Pings.RemoveAll(p => p.from == from);
        Pings.Add((pos, kind, Elapsed + 6f, from));
        var color = kind == 1 ? UiKit.Danger : kind == 2 ? UiKit.Gold : UiKit.Accent;
        var who = from == Net.I.MyId ? Settings.I.PlayerName : Remotes.TryGetValue(from, out var r) ? r.PlayerName : "";
        var marker = new Node3D();
        FxRoot.AddChild(marker);
        marker.GlobalPosition = pos;
        marker.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 30f, Material = Mats.Glow(color, 2f) },
            Position = new Vector3(0, 15f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Transparency = 0.4f,
        });
        var label = new Label3D
        {
            Text = (kind == 1 ? "◆ ENNEMI" : kind == 2 ? "◆ BUTIN" : "◆ ICI") + $"\n{who}",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, FixedSize = true, PixelSize = 0.0012f,
            FontSize = 40, OutlineSize = 10, Modulate = color, Font = UiKit.HeadFont, Position = new Vector3(0, 3f, 0),
        };
        marker.AddChild(label);
        var tw = marker.CreateTween();
        tw.TweenInterval(5.4f);
        tw.TweenProperty(label, "modulate:a", 0f, 0.6f);
        tw.TweenCallback(Callable.From(marker.QueueFree));
        Sfx.Ui("ui_click", -2f, 0.7f);
    }

    public void OnRally(long from, Vector3 pos)
    {
        Effects.Ring(this, pos, 30f, new Color(1f, 0.65f, 0.2f), 1f);
        Sfx.At("radio", pos, 4f);
        if (LocalPlayer != null && LocalPlayer.GlobalPosition.DistanceTo(pos) <= 30f)
        {
            LocalPlayer.ApplyRally();
            Hud?.Toast("Fusée de ralliement : +30 % de dégâts pendant 15 s !");
        }
    }

    public void OnLocalPlayerDamaged(float dmg, Vector3 from) => LocalPlayer?.TakeDamage(dmg, from);

    public void OnPeerLeft(long id)
    {
        Net.I.ReleaseVehicleSeats(id);
        if (Remotes.Remove(id, out var r))
        {
            Hud?.Toast($"{r.PlayerName} a quitté la mission.");
            r.QueueFree();
        }
    }

    public void OnShotFx(long from, int kind, Vector3 a, Vector3 b)
    {
        if (!Remotes.TryGetValue(from, out var r)) return;
        switch (kind)
        {
            case 0:
                Effects.Tracer(FxRoot, a, b, new Color(1f, 0.75f, 0.35f));
                Effects.MuzzleFlash(FxRoot, a, new Color(1f, 0.7f, 0.3f), 0.4f);
                Sfx.At("rifle_far", a, -2f);
                break;
            case 1:
                Effects.Tracer(FxRoot, a, b, new Color(0.5f, 0.75f, 1f));
                Sfx.At("zap", a, -2f);
                break;
            case 2:
                Effects.Beam(this, a, b, new Color(0.45f, 0.8f, 1f), 0.08f);
                Sfx.At("laser", a, -4f);
                break;
            case 7:
                Sfx.At("hammer", a, 0f);
                break;
            case 8:
                // Arme de bord d'un véhicule (minigun, mitrailleuse coaxiale) : le son en boucle est porté par le véhicule.
                Effects.Tracer(FxRoot, a, b, new Color(1f, 0.75f, 0.35f), 0.05f);
                Effects.MuzzleFlash(FxRoot, a, new Color(1f, 0.7f, 0.3f), 0.35f);
                if (VehicleOf(from) is { } fv) fv.OnRemoteFire();
                else Sfx.At("minigun", a, -2f);
                break;
            default:
                if (kind >= 10)
                {
                    var pk = (ProjectileKind)(kind - 10);
                    var shooter = VehicleOf(from);
                    var pr = Projectile.Spawn(this, pk, a, b - a, false, 0, 0);
                    // Roquettes et obus d'un véhicule : ne jamais exploser sur le véhicule tireur.
                    if (shooter != null) pr.IgnoreBody(shooter);
                    var snd = pk == ProjectileKind.Plasma ? "plasma" : shooter is Helicopter ? "heli_rocket" : "rocket";
                    Sfx.At(snd, a, 0f);
                }
                break;
        }
    }

    // ------------------------------------------------------------------ Ennemis (hôte)

    private readonly Dictionary<long, List<Enemy>> _hash = new();
    private const float HashCell = 10f; // ≥ rayon de séparation des grandes fourmis (≈ 3,2 × taille + voisin)

    private static long HashKey(Vector3 p) => ((long)Mathf.FloorToInt(p.X / HashCell) << 32) ^ (uint)Mathf.FloorToInt(p.Z / HashCell);

    private void RebuildSpatialHash()
    {
        foreach (var l in _hash.Values) l.Clear();
        foreach (var e in EnemiesById.Values)
        {
            if (e.State == EnemyState.Dead) continue;
            var k = HashKey(e.GlobalPosition);
            if (!_hash.TryGetValue(k, out var list)) _hash[k] = list = new List<Enemy>();
            list.Add(e);
        }
    }

    private readonly Dictionary<int, (float dmg, Vector3 at, bool weak)> _pendingDamage = new();
    private float _damageFlush;

    private void FlushDamageNumbers(float dt)
    {
        _damageFlush -= dt;
        if (_damageFlush > 0 || _pendingDamage.Count == 0) return;
        _damageFlush = 0.15f;
        foreach (var (_, v) in _pendingDamage) Effects.DamageNumber(this, v.at, v.dmg, v.weak);
        _pendingDamage.Clear();
    }

    public Vector3 Separation(Enemy self, float radius)
    {
        var p = self.GlobalPosition;
        var push = Vector3.Zero;
        var cx = Mathf.FloorToInt(p.X / HashCell);
        var cz = Mathf.FloorToInt(p.Z / HashCell);
        for (var dz = -1; dz <= 1; dz++)
        for (var dx = -1; dx <= 1; dx++)
        {
            var k = ((long)(cx + dx) << 32) ^ (uint)(cz + dz);
            if (!_hash.TryGetValue(k, out var list)) continue;
            foreach (var o in list)
            {
                if (o == self) continue;
                var d = p - o.GlobalPosition;
                // Au sol, écartement horizontal ; les grimpeurs (murs, toits) s'écartent en 3D.
                if (!self.Def.Flying && !self.Climbs) d.Y = 0;
                var r = radius + o.Def.Size * 1.3f;
                var len = d.Length();
                if (len < r && len > 0.001f) push += d / len * (1f - len / r);
            }
        }
        return push;
    }

    public Enemy HostSpawnEnemy(EnemyType type, Vector3 pos, bool airdrop = false)
    {
        var id = _nextEnemyId++;
        var yaw = Rng.RandfRange(-Mathf.Pi, Mathf.Pi);
        if (Enemies.Get(type).Flying && !airdrop) pos.Y = Rng.RandfRange(25, 40);
        // Largage par un vaisseau : signalé aux pairs par un cap décalé de +10 rad (voir Enemy.Create).
        if (airdrop) yaw += 10f;
        var hpMul = HealthMul;
        var def = Enemies.Get(type);
        // Les grimpeurs apparaissent sur la surface la plus haute (jamais dans un immeuble ou une voiture : sur le toit).
        if (!def.Flying && !def.Static && !def.Boss)
        {
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(new Vector3(pos.X, 200f, pos.Z), new Vector3(pos.X, -5f, pos.Z), 1));
            if (hit.Count > 0) pos.Y = Mathf.Max(pos.Y, ((Vector3)hit["position"]).Y);
        }
        if (!def.Static && !def.Boss && !Enemy.Frozen && Rng.Randf() < 0.06f) hpMul = -hpMul; // élite
        var e = Enemy.Create(this, id, type, pos, yaw, hpMul);
        AddChild(e);
        EnemiesById[id] = e;
        Net.I.BroadcastEnemySpawn(id, (int)type, pos, yaw, hpMul);
        return e;
    }

    public void OnEnemySpawn(int id, EnemyType type, Vector3 pos, float yaw, float hpMul)
    {
        if (EnemiesById.ContainsKey(id)) return;
        var e = Enemy.Create(this, id, type, pos, yaw, hpMul);
        AddChild(e);
        EnemiesById[id] = e;
        if (type == EnemyType.Queen) Sfx.At("screech", pos, 10f, 0.5f);
        else if (type == EnemyType.Fortress) Sfx.At("wave", pos, 12f, 0.35f);
    }

    public void OnEnemySnapshot(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var r = new BinaryReader(ms);
        while (ms.Position < ms.Length)
        {
            // Voir Enemy.PackSnapshot (18 octets par ennemi).
            var id = r.ReadInt32();
            var pos = Enemy.UnpackPos(r);
            var up = Enemy.UnpackDir(r.ReadUInt16());
            var fwd = Enemy.UnpackDir(r.ReadUInt16());
            var state = (EnemyState)r.ReadByte();
            var hp = r.ReadByte() / 255f;
            var atk = r.ReadByte() / 255f;
            var spd = r.ReadByte() / 10f;
            if (EnemiesById.TryGetValue(id, out var e)) e.ApplySnapshot(pos, up, fwd, state, hp, atk, spd);
        }
    }

    public void HostDamageEnemy(int id, float dmg, Vector3 at, long by, bool weak)
    {
        if (!EnemiesById.TryGetValue(id, out var e) || e.State == EnemyState.Dead) return;
        // Les nids ne craignent vraiment que les explosifs.
        if (e.Def.Type == EnemyType.Nest) dmg *= 0.35f;
        // Noyau de la forteresse : ×2,5 au total (le tireur a déjà appliqué ×1,5).
        if (weak && e.Def.Type == EnemyType.Fortress) dmg *= 2.5f / 1.5f;
        // Machines : points faibles (genoux, noyau, soute ouverte) ; le vaisseau ignore le reste.
        if (e.IsMachine && (dmg = e.HostAdjustDamage(dmg, weak)) <= 0f) return;
        e.Health -= dmg;
        if (by != Net.I.MyId) Net.I.BroadcastEnemyHit(id, at);
        if (e.Health <= 0)
        {
            KillsBy.TryGetValue(by, out var k);
            KillsBy[by] = k + 1;
            KillEnemy(e);
        }
    }

    public void HostExplosion(Vector3 pos, float radius, float dmg, int kind, long by)
    {
        Net.I.BroadcastExplosionFx(pos, radius, kind);
        Enemy.BlastOrigin = pos;
        foreach (var e in EnemiesById.Values.ToList())
        {
            if (e.State == EnemyState.Dead) continue;
            var d = e.BodyDistance(pos) - e.Def.Size * 1.2f;
            if (e.Def.Type == EnemyType.Nest) d = e.GlobalPosition.DistanceTo(pos) - 6f * e.Def.Size;
            if (d > radius) continue;
            var f = Mathf.Clamp(1f - Mathf.Max(d, 0) / radius * 0.6f, 0.4f, 1f);
            var amount = dmg * f;
            if (e.Def.Type == EnemyType.Nest) amount /= 0.35f; // annule la réduction de HostDamageEnemy
            HostDamageEnemy(e.Id, amount, e.Center, by, false);
        }
        Enemy.BlastOrigin = null;
        if (kind != 3 || radius > 3)
            foreach (var b in Buildings)
            {
                if (b.Destroyed) continue;
                var local = pos - b.GlobalPosition;
                var closest = new Vector3(Mathf.Clamp(local.X, -b.Size.X / 2, b.Size.X / 2), Mathf.Clamp(local.Y, 0, b.Size.Y), Mathf.Clamp(local.Z, -b.Size.Z / 2, b.Size.Z / 2));
                if (closest.DistanceTo(local) <= radius) HostBuildingDamage(b.Id, dmg * (kind == 3 ? 0.6f : 1f), pos);
            }
        if (kind != 3 || radius > 3) Decor?.HostBlast(pos, radius, dmg * (kind == 3 ? 0.6f : 1f), by);
    }

    public void OnExplosionFx(Vector3 pos, float radius, int kind)
    {
        if (kind == 3)
        {
            Effects.Sparks(this, pos, Vector3.Up);
            Effects.Smoke(this, pos - Vector3.Up, 2f, 1.2f);
            ShakeCamera(pos, 0.3f, 20f);
            return;
        }
        Effects.Explosion(this, pos, radius, kind);
    }

    public void OnEnemyHitFx(int id, Vector3 at)
    {
        if (EnemiesById.TryGetValue(id, out var e)) LocalHitFx(e, at, false);
    }

    public void LocalHitFx(Enemy e, Vector3 at, bool sound = true, float damage = 0, bool weak = false)
    {
        if (e.Invulnerable(weak)) damage = 0; // blindage du vaisseau : ni chiffre ni dégât
        if (e.IsMachine) e.HitSparks(at);
        else if (e.Def.Type == EnemyType.Fortress) Effects.Sparks(this, at, Vector3.Up);
        else Effects.Goo(this, at, e.Def.Type == EnemyType.AcidAnt ? new Color(0.7f, 0.9f, 0.15f) : new Color(0.55f, 0.48f, 0.12f), 0.5f, 4);
        e.FlashHit();
        if (damage > 0 && Settings.I.DamageNumbers)
        {
            // Regroupement par ennemi (feu continu : un chiffre toutes les 0,15 s au lieu d'une pile).
            _pendingDamage.TryGetValue(e.Id, out var acc);
            _pendingDamage[e.Id] = (acc.dmg + damage, at, acc.weak || weak);
        }
        if (sound) Sfx.Ui("hitmarker", -8f, 1f, 40);
    }

    private void KillEnemy(Enemy e)
    {
        var pos = e.GlobalPosition;
        e.Die();
        EnemiesById.Remove(e.Id);
        TeamKills++;
        CountKill(e);
        Net.I.BroadcastEnemyDeath(e.Id, pos);
        _director?.OnEnemyKilled(e);

        // Butin : là où le corps tombe (au pied du mur, ou sur le toit).
        var ground = e.LandingPoint;
        var drops = 0;
        if (e.Def.Boss) drops = 10;
        else if (e.Def.Type == EnemyType.Nest) drops = 4;
        else if (e.Elite) drops = 2;
        else if (Rng.Randf() < e.Def.DropChance) drops = 1;
        drops += e.EatenCrates + (e.EatenCrates > 0 ? 1 : 0);
        for (var i = 0; i < drops; i++)
        {
            var type = (e.Def.Boss && i < 5) || (e.Elite && i == 0) ? CrateType.Weapon : Crates.Roll(Rng);
            HostSpawnCrate(type, ground + new Vector3(Rng.RandfRange(-2, 2) * (e.Def.Size), 0, Rng.RandfRange(-2, 2) * e.Def.Size));
        }
    }

    /// <summary>
    /// Hôte : un ennemi coincé (sous la carte, hors limites ou immobile trop longtemps) est retiré sans butin
    /// ni élimination, puis remplacé par un ennemi du même type à la zone d'apparition la plus proche.
    /// </summary>
    public void HostRespawnStuck(Enemy e)
    {
        if (e.State == EnemyState.Dead || !EnemiesById.ContainsKey(e.Id)) return;
        var from = new Vector3(e.GlobalPosition.X, 0, e.GlobalPosition.Z);
        var type = e.Def.Type;
        EnemiesById.Remove(e.Id);
        Net.I.BroadcastEnemyDespawn(e.Id);
        e.QueueFree();
        var zone = City.Zones.Count > 0 ? City.Zones.Values.OrderBy(z => z.DistanceSquaredTo(from)).First() : Vector3.Zero;
        HostSpawnEnemy(type, zone + new Vector3(Rng.RandfRange(-6, 6), 0, Rng.RandfRange(-6, 6)));
        if (Screenshot.CmdArg("fps") != null) GD.Print($"[ennemi] {type} bloqué en {from} : réapparition en {zone}");
    }

    /// <summary>Hôte : un insecte se replie (objectif rempli) : retiré sans butin ni élimination.</summary>
    public void HostRetreatEnemy(Enemy e)
    {
        if (!EnemiesById.Remove(e.Id)) return;
        Net.I.BroadcastEnemyDespawn(e.Id);
        e.QueueFree();
    }

    public void OnEnemyDespawn(int id)
    {
        if (!EnemiesById.Remove(id, out var e)) return;
        e.QueueFree();
    }

    public void OnEnemyDeath(int id, Vector3 pos)
    {
        if (!EnemiesById.TryGetValue(id, out var e)) return;
        e.GlobalPosition = pos;
        e.Die();
        EnemiesById.Remove(id);
        TeamKills++;
        CountKill(e);
    }

    public void OnEnemyAttack(int id, int kind, Vector3 target)
    {
        if (!EnemiesById.TryGetValue(id, out var e)) return;
        if (kind >= 10) { e.FortressFx(kind, target); return; }
        e.PlayAttack();
        switch (kind)
        {
            case 0: Sfx.At("hit", e.GlobalPosition, 0f, 0.7f); break;
            case 1:
                Projectile.Spawn(this, e.IsBoss ? ProjectileKind.AcidBig : e.Def.Type == EnemyType.Spider ? ProjectileKind.Web : ProjectileKind.Acid, e.Center + e.Basis.Z * 1.5f * e.Def.Size,
                    BallisticVelocity(e.Center, target + Vector3.Up, 30f, 9f), false, 0, 0);
                Sfx.At("spit", e.GlobalPosition, 0f);
                break;
            case 2: Sfx.At("screech", e.GlobalPosition, 8f, 0.6f); break;
            case 3:
                var drone = e.Def.Type == EnemyType.Drone;
                Projectile.Spawn(this, drone ? ProjectileKind.Laser : ProjectileKind.Stinger, e.GlobalPosition + Vector3.Down * (0.8f * e.Def.Size), (target + Vector3.Up - e.GlobalPosition).Normalized() * (drone ? 90f : 60f), false, 0, 0);
                Sfx.At("buzz", e.GlobalPosition, 0f);
                break;
        }
    }

    public static Vector3 BallisticVelocity(Vector3 from, Vector3 to, float speed, float gravity)
    {
        var d = to - from;
        var flat = new Vector3(d.X, 0, d.Z);
        var dist = flat.Length();
        var t = Mathf.Max(dist / speed, 0.3f);
        var vy = (d.Y + 0.5f * gravity * t * t) / t;
        return flat / t + Vector3.Up * vy;
    }

    public void HostEnemyProjectile(Enemy e, int kind, Vector3 from, Vector3 target, int size)
    {
        Projectile p;
        if (kind == 2)
        {
            p = Projectile.Spawn(this, ProjectileKind.Web, from, BallisticVelocity(from, target, 32f, 9f), true, e.Def.Damage * DamageMul * e.DamageScale, 2.5f);
            Sfx.At("spit", from, 0f, 1.3f);
        }
        else if (kind == 3)
        {
            p = Projectile.Spawn(this, ProjectileKind.Laser, from, (target - from).Normalized() * 90f, true, e.Def.Damage * DamageMul * e.DamageScale, 1.2f);
            Sfx.At("laser", from, -2f, 0.7f);
        }
        else if (kind == 0)
        {
            var pk = size >= 3 ? ProjectileKind.AcidBig : ProjectileKind.Acid;
            p = Projectile.Spawn(this, pk, from, BallisticVelocity(from, target, size >= 3 ? 26f : 30f, 9f), true, e.Def.Damage * DamageMul * e.DamageScale, size >= 3 ? 5f : 2.5f);
            Sfx.At("spit", from, 0f);
        }
        else
        {
            p = Projectile.Spawn(this, ProjectileKind.Stinger, from, (target - from).Normalized() * 60f, true, e.Def.Damage * DamageMul * e.DamageScale, 1.5f);
            Sfx.At("buzz", from, 0f);
        }
        p.SourceEnemy = e;
    }

    public bool HostProjectileHitsPlayer(Projectile p, Vector3 a, Vector3 b)
    {
        foreach (var (peer, pos) in PlayerTargets())
        {
            var c = pos + Vector3.Up * 1f;
            var closest = Geometry3D.GetClosestPointToSegment(c, a, b);
            if (closest.DistanceTo(c) < 0.9f + p.Radius * 0.2f + VehicleHitBonus(peer)) return true;
        }
        return false;
    }

    public void HostEnemyProjectileImpact(Projectile p, Vector3 pos)
    {
        foreach (var (peer, ppos) in PlayerTargets())
        {
            var d = (ppos + Vector3.Up).DistanceTo(pos);
            if (d <= p.Radius + 0.6f + VehicleHitBonus(peer)) Net.I.DamagePlayer(peer, p.Damage, pos);
        }
    }

    /// <summary>Explosion ennemie (canon de la forteresse) : effet chez tous, dégâts aux joueurs et aux immeubles proches.</summary>
    public void HostEnemyBlast(Vector3 pos, float radius, float dmg)
    {
        Net.I.BroadcastExplosionFx(pos, radius, 0);
        foreach (var (peer, ppos) in PlayerTargets())
        {
            var d = (ppos + Vector3.Up).DistanceTo(pos);
            d = Mathf.Max(0, d - VehicleHitBonus(peer));
            if (d <= radius) Net.I.DamagePlayer(peer, dmg * Mathf.Clamp(1.2f - d / radius, 0.4f, 1f), pos);
        }
        foreach (var b in Buildings)
        {
            if (b.Destroyed) continue;
            var local = pos - b.GlobalPosition;
            var closest = new Vector3(Mathf.Clamp(local.X, -b.Size.X / 2, b.Size.X / 2), Mathf.Clamp(local.Y, 0, b.Size.Y), Mathf.Clamp(local.Z, -b.Size.Z / 2, b.Size.Z / 2));
            if (closest.DistanceTo(local) <= radius) HostBuildingDamage(b.Id, dmg * 4f, pos);
        }
        Decor?.HostBlast(pos, radius, dmg * 4f, 0);
    }

    private IEnumerable<(long, Vector3)> PlayerTargets()
    {
        if (LocalPlayer != null && !LocalPlayer.Downed) yield return (Net.I.MyId, LocalPlayer.GlobalPosition);
        foreach (var (id, r) in Remotes)
            if (!r.Downed) yield return (id, r.GlobalPosition);
    }

    public Enemy NearestEnemy(Vector3 pos, float range)
    {
        Enemy best = null;
        var bd = range * range;
        foreach (var e in EnemiesById.Values)
        {
            if (e.State == EnemyState.Dead || e.State == EnemyState.Emerging) continue;
            var d = e.GlobalPosition.DistanceSquaredTo(pos);
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }

    public Enemy EnemyNearRay(Vector3 origin, Vector3 dir, float maxAngle, float range)
    {
        Enemy best = null;
        var bestDot = Mathf.Cos(maxAngle);
        foreach (var e in EnemiesById.Values)
        {
            if (e.State == EnemyState.Dead) continue;
            var to = e.Center - origin;
            var len = to.Length();
            if (len > range) continue;
            var dot = to.Dot(dir) / Mathf.Max(len, 0.01f);
            if (dot > bestDot) { bestDot = dot; best = e; }
        }
        return best;
    }

    // ------------------------------------------------------------------ Caisses

    public void HostSpawnCrate(CrateType type, Vector3 pos)
    {
        // Plafond : on retire la plus ancienne caisse de soin.
        if (CratesById.Count >= 400)
        {
            var old = CratesById.Values.FirstOrDefault(c => c.Type == CrateType.Health) ?? CratesById.Values.First();
            RemoveCrate(old.Id);
            Net.I.BroadcastCrateGone(old.Id);
        }
        var id = _nextCrateId++;
        OnCrateSpawn(id, type, pos);
        Net.I.BroadcastCrateSpawn(id, (int)type, pos);
    }

    public void OnCrateSpawn(int id, CrateType type, Vector3 pos)
    {
        if (CratesById.ContainsKey(id)) return;
        var c = Crate.Create(id, type, pos);
        AddChild(c);
        CratesById[id] = c;
    }

    private float _pickupRequestT;

    private void LocalPickupCheck()
    {
        _pickupRequestT -= (float)GetProcessDeltaTime();
        var p = LocalPlayer.GlobalPosition;
        var radius = 2.4f;
        var dt = (float)GetProcessDeltaTime();
        foreach (var c in CratesById.Values)
        {
            if (c.Taken) continue;
            // Aimantation douce des caisses proches.
            if (c.GlobalPosition.DistanceTo(p) < 7f && !(c.Type == CrateType.Health && LocalPlayer.Health >= LocalPlayer.MaxHealth))
                c.Attract(p, dt);
            var d = c.GlobalPosition - p;
            d.Y *= 0.5f;
            if (d.Length() > radius) continue;
            // Soin inutile si la vie est pleine.
            if (c.Type == CrateType.Health && LocalPlayer.Health >= LocalPlayer.MaxHealth) continue;
            c.Taken = true;
            Net.I.RequestPickup(c.Id);
            break;
        }
    }

    public void HostPickup(int id, long by)
    {
        if (!CratesById.TryGetValue(id, out var c)) return;
        Net.I.BroadcastCratePicked(id, by, (int)c.Type, c.GlobalPosition);
    }

    public void OnCratePicked(int id, long by, CrateType type, Vector3 pos)
    {
        RemoveCrate(id);
        CratesCollected[(int)type]++;
        if (by == Net.I.MyId)
        {
            Sfx.Ui(type == CrateType.Weapon ? "pickup_weapon" : "pickup", -2f);
            Hud?.PickupPopup(type);
        }
        if (type == CrateType.Health && LocalPlayer != null && LocalPlayer.GlobalPosition.DistanceTo(pos) < 8f)
            LocalPlayer.Heal(LocalPlayer.MaxHealth * 0.2f);
    }

    public void OnCrateGone(int id) => RemoveCrate(id);

    private void RemoveCrate(int id)
    {
        if (CratesById.Remove(id, out var c)) c.QueueFree();
    }

    public int NearestCrate(Vector3 pos, float range)
    {
        var best = -1;
        var bd = range * range;
        foreach (var c in CratesById.Values)
        {
            var d = c.GlobalPosition.DistanceSquaredTo(pos);
            if (d < bd) { bd = d; best = c.Id; }
        }
        return best;
    }

    public bool CratePosition(int id, out Vector3 pos)
    {
        pos = Vector3.Zero;
        if (!CratesById.TryGetValue(id, out var c)) return false;
        pos = c.GlobalPosition;
        return true;
    }

    public void HostEatCrate(int id, Enemy e)
    {
        if (!CratesById.ContainsKey(id)) return;
        e.EatenCrates++;
        RemoveCrate(id);
        Net.I.BroadcastCrateGone(id);
        Sfx.At("death_bug", e.GlobalPosition, -4f, 1.6f);
    }

    // ------------------------------------------------------------------ Décor

    /// <summary>Dégâts à un immeuble (hôte) : paliers d'endommagement (voir <see cref="Building.StageFor"/>), puis effondrement.</summary>
    public void HostBuildingDamage(int id, float dmg, Vector3 at = default)
    {
        if (id < 0 || id >= Buildings.Count) return;
        var b = Buildings[id];
        if (b.Destroyed) return;
        if (b.Standing) { var panel = b.HostStandingDamage(dmg); if (panel > 0) Net.I.BroadcastBuildingStage(id, panel, at); return; } // tour ≥ 20 étages : façade seulement
        b.Health -= dmg;
        if (b.Health <= 0) { Net.I.BroadcastBuildingDestroyed(id); return; }
        var stage = b.StageFor(b.Health / b.MaxHealth);
        if (stage <= b.HostStage) return;
        b.HostStage = stage;
        Net.I.BroadcastBuildingStage(id, stage, at);
    }

    public void OnBuildingStage(int id, int stage, Vector3 at)
    {
        if (id >= 0 && id < Buildings.Count) Buildings[id].ShowStage(this, stage, at);
    }

    public void OnBuildingDestroyed(int id)
    {
        if (id < 0 || id >= Buildings.Count) return;
        var b = Buildings[id];
        if (b.Destroyed) return;
        b.Collapse(this);
        Flow.SetBlockedRect(b.GlobalPosition, b.Size, false);
        // Ce qui se trouve au pied de l'immeuble est écrasé par l'effondrement.
        if (Net.I.IsHost) Decor?.HostCrushArea(b.GlobalPosition, b.Size.X / 2 + 2.5f, b.Size.Z / 2 + 2.5f, 4);
    }

    public void ShakeCamera(Vector3 pos, float strength, float radius)
    {
        if (LocalPlayer == null) return;
        var d = LocalPlayer.GlobalPosition.DistanceTo(pos);
        if (d > radius) return;
        LocalPlayer.AddShake(strength * (1f - d / radius));
    }

    // ------------------------------------------------------------------ Soutien

    public void OnAirstrike(long from, Vector3 target, Vector3 dir, float dmg, int count, float radius)
    {
        // Marqueur fumigène rouge sur la cible.
        var marker = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.1f, BottomRadius = 0.1f, Height = 0.5f, Material = Mats.Glow(new Color(1f, 0.2f, 0.1f), 4f) },
        };
        FxRoot.AddChild(marker);
        marker.GlobalPosition = target + Vector3.Up * 0.25f;
        Effects.Ring(this, target, radius, new Color(1f, 0.3f, 0.15f), 3f);
        Sfx.At("whistle", target + Vector3.Up * 30f, 6f);
        var line = count > 6;
        for (var i = 0; i < count; i++)
        {
            var idx = i;
            var t = GetTree().CreateTimer(3f + i * 0.22f, false);
            t.Timeout += () =>
            {
                if (!IsInstanceValid(this)) return;
                Vector3 p;
                if (line) p = target + dir * ((idx - count / 2f) * radius * 0.7f);
                else p = target + new Vector3(Rng.RandfRange(-radius, radius), 0, Rng.RandfRange(-radius, radius)) * 0.8f;
                p.Y = 0.5f;
                if (Net.I.IsHost) HostExplosion(p, radius, dmg, 0, from);
            };
        }
        var cleanup = GetTree().CreateTimer(4f + count * 0.22f, false);
        cleanup.Timeout += () => { if (IsInstanceValid(marker)) marker.QueueFree(); };
    }

    public void OnDeploy(long from, int kind, Vector3 pos, float yaw, float power, float radius, float duration)
    {
        var owned = from == Net.I.MyId;
        if (kind == 0) FxRoot.AddChild(Turret.Create(this, pos, yaw, owned, power, radius, duration));
        else FxRoot.AddChild(HealField.Create(this, pos, power, radius, duration));
        Sfx.At(kind == 0 ? "reload" : "heal", pos, 0f);
    }

    // ------------------------------------------------------------------ Mission

    public void OnMissionMessage(string text, int kind)
    {
        Hud?.Radio(text, kind);
        Sfx.Ui(kind == 1 ? "wave" : "radio", kind == 1 ? -2f : -4f);
    }

    public void OnObjective(string text, int remaining, float bossHp, string bossName)
    {
        ObjectiveText = text;
        EnemiesRemaining = remaining;
        BossHealth = bossHp;
        BossName = bossName;
    }

    private string ScoresString()
    {
        var parts = new List<string>();
        foreach (var (id, k) in KillsBy) parts.Add($"{id}:{k}");
        return string.Join(";", parts);
    }

    public void OnScores(string data)
    {
        if (Net.I.IsHost) return;
        KillsBy.Clear();
        foreach (var part in data.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split(':');
            if (kv.Length == 2 && long.TryParse(kv[0], out var id) && int.TryParse(kv[1], out var k)) KillsBy[id] = k;
        }
    }

    /// <summary>Lignes du tableau des scores : nom, classe, éliminations, état.</summary>
    public List<(string name, string cls, int kills, bool downed, bool me)> ScoreRows()
    {
        var rows = new List<(string, string, int, bool, bool)>();
        foreach (var p in Net.I.Players.Values.OrderBy(p => p.Id))
        {
            KillsBy.TryGetValue(p.Id, out var k);
            var downed = p.Id == Net.I.MyId ? LocalPlayer?.Downed ?? false : Remotes.TryGetValue(p.Id, out var r) && r.Downed;
            rows.Add((p.Name, Classes.Get(p.Class).Name, k, downed, p.Id == Net.I.MyId));
        }
        KillsBy.TryGetValue(0, out var npc);
        if (Allies != null && Allies.ById.Count > 0) rows.Add(("Escouades alliées", $"{Allies.Alive} en vie", npc, false, false));
        return rows;
    }

    public void EndMission(bool success)
    {
        if (Ended) return;
        Net.I.BroadcastScores(ScoresString());
        Net.I.BroadcastMissionEnd(success, Elapsed);
    }

    public MissionReport Report;

    public void OnMissionEnd(bool success, float elapsed)
    {
        if (Ended) return;
        Ended = true;
        Elapsed = elapsed;
        GD.Print($"[mission] {(success ? "victoire" : "échec")} en {elapsed:0} s, {TeamKills} éliminations, difficulté {(int)Difficulty}");
        if (Paused) TogglePause();
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Sfx.Ui(success ? "victory" : "defeat", 0f);
        Report = MissionReport.Build(this, success);
        Finished?.Invoke(success);
    }
}
