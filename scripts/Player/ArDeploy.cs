using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Objets posés par l'Air Raider : tourelles ZE (tir automatique), robots (bombes roulantes, coléoptères sauteurs, patrouilleurs,
/// Speed Star), postes et zones de soutien (soin, puissance, protection), abris de murs d'énergie, ballons leurres, balises de
/// guidage. Chaque pair crée les mêmes objets à partir du tir rejoué ; seul le propriétaire inflige des dégâts, chaque pair
/// applique les zones de soutien à son propre soldat, l'hôte gère les leurres (cibles des ennemis) et l'usure des abris.
/// </summary>
public static class ArDeploy
{
    public static bool Shot(World world, Edf5Spec s, WeaponInstance w, WeaponTemplate t, Vector3 from, Vector3 dir, int seed,
        RandomNumberGenerator rng, bool local, Player shooter, float dmg, float spread, float range, float speed, float blast)
    {
        var flat = new Vector3(dir.X, 0, dir.Z);
        flat = flat.LengthSquared() > 0.001f ? flat.Normalized() : Vector3.Forward;
        // Pose devant le soldat (au sol), sauf abris (plus loin) et cage (au point visé).
        Vector3 Ground(float dist)
        {
            var p = from + flat * dist;
            p.Y = world.GroundHeight(p);
            return p;
        }
        switch (s.Kind)
        {
            case Edf5Kind.Sentry:
                world.FxRoot.AddChild(Sentry.Create(world, s, Ground(2.5f), flat, dmg, w?.Range ?? t.Range, local));
                return true;
            case Edf5Kind.Robot:
                world.FxRoot.AddChild(Robot.Create(world, s, Ground(1.8f), flat, dmg, blast, w?.LockRange ?? s.LockRange.Max, local, seed));
                return true;
            case Edf5Kind.Decoy:
                world.FxRoot.AddChild(Decoy.Create(world, Ground(8f), w?.Durability ?? s.Durability.Max, s.Duration > 0 ? s.Duration : 90f, seed));
                return true;
            case Edf5Kind.Bunker:
            {
                var at = s.Family switch { "shelter" => Ground(0f), "prison" => from, "emwall" => Ground(9f), _ => Ground(7f) };
                if (s.Family == "prison") at.Y = world.GroundHeight(at);
                Bunker.Spawn(world, s, at, flat, w?.Durability ?? s.Durability.Max);
                return true;
            }
            case Edf5Kind.Post when s.Family is "lifevendor" or "powerpost" or "guardpost":
                world.FxRoot.AddChild(SupportZone.Create(world, s, Ground(2f), dmg, blast, s.Duration, true));
                return true;
            case Edf5Kind.Post:
            {
                // Pistolets de soutien : charge qui crée une petite zone d'effet à l'impact.
                var p = Projectile.Spawn(world, ProjectileKind.Energy, from, dir * Mathf.Clamp(speed > 0 ? speed : 200f, 80f, 220f), false, 0, 0);
                p.Life = 3f;
                p.Gravity = 4f;
                p.CustomImpact = (at, n) => world.FxRoot.AddChild(SupportZone.Create(world, s, at, dmg, Mathf.Max(blast, 3f), s.Duration, false));
                return true;
            }
            case Edf5Kind.Guide when s.Family == "beacongun":
            {
                // Balise : dard collant que les lance-missiles à kit de guidage visent.
                var p = Projectile.Spawn(world, ProjectileKind.Grenade, from, dir * 160f, false, 0, 0);
                p.Gravity = 2f;
                p.Sticky = true;
                p.Life = 90f;
                p.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f }, MaterialOverride = Mats.Glow(new Color(1f, 0.3f, 0.1f), 5f) });
                p.CustomImpact = (at, n) => { };
                world.AddGuideTarget(p);
                return true;
            }
        }
        return false;
    }
}

/// <summary>Modèles des objets de l'Air Raider (Blender, assets/models/airraid), matières du jeu ; repli procédural.</summary>
public static class AirRaidModels
{
    private static readonly Dictionary<string, PackedScene> Scenes = new();

    public static Node3D Load(string name, Color paint, Color accent)
    {
        if (!Scenes.TryGetValue(name, out var scene))
        {
            var path = $"res://assets/models/airraid/{name}.glb";
            scene = ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;
            Scenes[name] = scene;
        }
        if (scene == null) return null;
        var model = scene.Instantiate<Node3D>();
        SoldierModel.ApplyMats(model, paint, accent, null, Mats.GunMask($"res://assets/models/airraid/{name}.glb"));
        return model;
    }

    /// <summary>Appareil d'une frappe (canonnière, bombardier) ; avion simple si le modèle manque.</summary>
    public static Node3D Craft(string family)
    {
        var name = family switch { "gunship" => "gunship", "kamuy" => "kamuy", "vesta" or "phobos" => family, _ => "km6" };
        var model = Load(name, new Color(0.34f, 0.37f, 0.36f), new Color(0.12f, 0.13f, 0.14f));
        var n = new Node3D();
        // Nez du modèle vers +Z ; l'appareil est orienté par LookAt (−Z vers la route) : demi-tour du modèle.
        if (model != null)
        {
            model.RotationDegrees = new Vector3(0, 180, 0);
            n.AddChild(model);
            return n;
        }
        var mat = Mats.Metal(new Color(0.3f, 0.32f, 0.33f), 0.5f);
        n.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3f, 2.5f, 22f), Material = mat } });
        n.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(30f, 0.5f, 5f), Material = mat } });
        return n;
    }
}

/// <summary>Tourelle ZE : tire seule sur l'ennemi le plus proche à portée, jusqu'à épuisement de ses munitions.</summary>
public partial class Sentry : Node3D
{
    private World _world;
    private Edf5Spec _s;
    private float _dmg, _range, _cd, _retarget, _life = 1f;
    private int _ammo;
    private bool _local;
    private Node3D _head, _muzzle;
    private Enemy _target;

    public static Sentry Create(World world, Edf5Spec s, Vector3 at, Vector3 dir, float dmg, float range, bool local)
    {
        var t = new Sentry { _world = world, _s = s, _dmg = dmg, _local = local, _ammo = Math.Max(1, s.Loaded) };
        t._range = s.Search.Present ? s.Search.Max : Mathf.Max(range, 100f);
        t.Position = at;
        var model = AirRaidModels.Load(s.Family, new Color(0.55f, 0.5f, 0.36f), new Color(0.12f, 0.12f, 0.12f));
        var basePart = model?.FindChild("Base", true, false) as Node3D;
        t._head = model?.FindChild("Head", true, false) as Node3D;
        if (model != null) t.AddChild(model);
        if (t._head == null)
        {
            var metal = Mats.Metal(new Color(0.45f, 0.42f, 0.32f), 0.5f);
            t.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.45f, Height = 0.8f, Material = metal }, Position = Vector3.Up * 0.4f });
            t._head = new Node3D { Position = Vector3.Up * 0.95f };
            t.AddChild(t._head);
            t._head.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.35f, 0.7f), Material = metal } });
        }
        t._muzzle = t._head.FindChild("Muzzle", true, false) as Node3D ?? new Node3D { Position = new Vector3(0, 0.05f, 0.8f) };
        if (t._muzzle.GetParent() == null) t._head.AddChild(t._muzzle);
        t.Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Z), 0);
        _ = basePart;
        return t;
    }

    /// <summary>Cadence selon la tourelle : ZE-GUN ≈ 8 coups/s, mitrailleuses lourdes (FZ) 30/s, tireur d'élite 1,5/s, lanceurs 2/s.</summary>
    private float Rate => _s.Family switch
    {
        "zesniper" => 1.5f, "zelauncher" => _s.Loaded >= 100 ? 6f : 2f, "zeblaster" => 20f,
        _ => Mathf.Clamp(_s.Loaded / 12f, 6f, 30f),
    };

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        if (_ammo <= 0)
        {
            _life -= dt;
            if (_life <= 0) { Effects.Smoke(_world, GlobalPosition + Vector3.Up, 1.2f, 1.5f); QueueFree(); }
            return;
        }
        _retarget -= dt;
        if (_retarget <= 0 || _target == null || !IsInstanceValid(_target) || _target.State == EnemyState.Dead)
        {
            _retarget = 0.35f;
            _target = _world.NearestEnemy(GlobalPosition, _range);
        }
        if (_target == null) return;
        var to = _target.Center - _head.GlobalPosition;
        var yaw = Mathf.Atan2(to.X, to.Z);
        _head.GlobalRotation = new Vector3(0, Mathf.LerpAngle(_head.GlobalRotation.Y, yaw, Mathf.Clamp(dt * 10f, 0, 1)), 0);
        _cd -= dt;
        if (_cd > 0) return;
        var n = Mathf.Clamp(Mathf.RoundToInt(dt * Rate), 1, 3);
        _cd = n / Rate;
        for (var i = 0; i < n && _ammo > 0; i++, _ammo--) FireOnce();
    }

    private void FireOnce()
    {
        var from = _muzzle.GlobalPosition;
        var aim = _target.Center + new Vector3((float)GD.RandRange(-0.6, 0.6), (float)GD.RandRange(-0.5, 0.5), (float)GD.RandRange(-0.6, 0.6));
        var d = (aim - from).Normalized();
        switch (_s.Family)
        {
            case "zelauncher":
            {
                var p = Projectile.Spawn(_world, ProjectileKind.Missile, from, (d + Vector3.Up * 0.4f).Normalized() * 70f, _local, _dmg, _s.Blast.Present ? _s.Blast.Max : 4f);
                p.Life = 10f;
                p.SetHoming(_target, 4f);
                Sfx.At("e5_zelauncherfire", from, -4f, 1f, 80);
                break;
            }
            default:
            {
                var sniper = _s.Family == "zesniper";
                var blaster = _s.Family == "zeblaster";
                var sp = sniper ? 1500f : blaster ? 2000f : 700f;
                Bullets.For(_world).Add(new Bullets.Shot
                {
                    Pos = from, Vel = d * sp, Life = _range / sp, MaxLife = _range / sp, Damage = _dmg, Far = 1f, FalloffExp = 1f, Local = _local,
                    Pierce = blaster, Color = blaster ? new Color(0.5f, 0.85f, 1f) : new Color(1f, 0.75f, 0.35f),
                    Length = sniper ? 14f : blaster ? 10f : 3f, Width = sniper ? 0.08f : 0.05f,
                });
                Sfx.At(sniper ? "e5_zesniperfire" : blaster ? "e5_zeblasterfire" : "e5_zegunfire", from, -6f, (float)GD.RandRange(0.95, 1.05), sniper ? 0 : 55);
                break;
            }
        }
        Effects.MuzzleFlash(_world.FxRoot, from, new Color(1f, 0.7f, 0.3f), 0.35f);
    }
}

/// <summary>
/// Robots de l'Air Raider : bombe roulante vers l'ennemi le plus proche devant elle (portée de verrouillage), coléoptère qui
/// bondit sur sa cible, patrouilleur qui avance en mitraillant, Speed Star qui court en attirant les ennemis.
/// </summary>
public partial class Robot : Node3D
{
    private World _world;
    private Edf5Spec _s;
    private float _dmg, _blast, _range, _life, _cd, _hopT, _travel;
    private bool _local, _done;
    private Vector3 _dir, _vel;
    private Enemy _target;
    private Decoy _lure;

    public static Robot Create(World world, Edf5Spec s, Vector3 at, Vector3 dir, float dmg, float blast, float range, bool local, int seed)
    {
        var r = new Robot { _world = world, _s = s, _dmg = dmg, _blast = Mathf.Max(blast, 3f), _range = range > 0 ? range : 150f, _local = local, _dir = dir };
        r._life = s.Family switch { "patroller" => 30f, "speedstar" => 20f, _ => 16f };
        r.Position = at + Vector3.Up * 0.3f;
        var model = AirRaidModels.Load(s.Family, s.Family == "speedstar" ? new Color(0.9f, 0.75f, 0.15f) : new Color(0.55f, 0.5f, 0.36f), new Color(0.12f, 0.12f, 0.12f));
        if (model != null) r.AddChild(model);
        else r.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.45f, Height = 0.7f }, MaterialOverride = Mats.Metal(new Color(0.5f, 0.45f, 0.3f), 0.5f) });
        r.AddChild(new OmniLight3D { LightColor = new Color(1f, 0.3f, 0.15f), LightEnergy = 0.8f, OmniRange = 3f, Position = Vector3.Up * 0.8f });
        if (s.Family == "speedstar") r._lure = Decoy.Attach(world, r, 60f);
        return r;
    }

    public override void _Process(double delta)
    {
        if (_done) return;
        var dt = (float)delta;
        _life -= dt;
        if (_life <= 0) { Boom(); return; }
        if (_target == null || !IsInstanceValid(_target) || _target.State == EnemyState.Dead) _target = Pick();
        switch (_s.Family)
        {
            case "beetle": Hop(dt); break;
            case "patroller": Patrol(dt); break;
            case "speedstar": Run(dt); break;
            default: Roll(dt); break;
        }
    }

    /// <summary>Ennemi le plus proche dans le cône avant (même choix chez tous les pairs).</summary>
    private Enemy Pick()
    {
        Enemy best = null;
        var bd = _range;
        foreach (var e in _world.EnemiesById.Values)
        {
            if (e.State == EnemyState.Dead) continue;
            var to = e.GlobalPosition - GlobalPosition;
            to.Y = 0;
            var d = to.Length();
            if (d > bd || d > 2f && _dir.AngleTo(to / d) > Mathf.DegToRad(_s.Family == "beetle" ? 90f : 50f)) continue;
            bd = d;
            best = e;
        }
        return best;
    }

    private void Face(Vector3 d)
    {
        if (d.LengthSquared() > 0.001f) Rotation = new Vector3(0, Mathf.Atan2(d.X, d.Z), 0);
    }

    private void Roll(float dt)
    {
        var goal = _target != null ? (_target.GlobalPosition - GlobalPosition) with { Y = 0 } : _dir;
        if (goal.LengthSquared() > 0.01f) _dir = _dir.Slerp(goal.Normalized(), Mathf.Clamp(dt * 3f, 0, 1)).Normalized();
        var p = GlobalPosition + _dir * 13f * dt;
        p.Y = _world.GroundHeight(p) + 0.3f;
        GlobalPosition = p;
        Face(_dir);
        if (_target != null && _target.BodyDistance(GlobalPosition) < _target.Def.Size * 1.6f + 1.5f) Boom();
    }

    private void Hop(float dt)
    {
        _hopT -= dt;
        if (_hopT <= 0 && GlobalPosition.Y <= _world.GroundHeight(GlobalPosition) + 0.4f)
        {
            // Bond de 6 à 12 m vers la cible.
            var to = _target != null ? (_target.GlobalPosition - GlobalPosition) with { Y = 0 } : _dir * 10f;
            var dist = Mathf.Clamp(to.Length(), 4f, 12f);
            var d = to.LengthSquared() > 0.01f ? to.Normalized() : _dir;
            _dir = d;
            _vel = d * dist / 0.8f + Vector3.Up * 9.8f;
            _hopT = 0.9f;
            Sfx.At("dash", GlobalPosition, -10f, 1.6f, 60);
        }
        _vel += Vector3.Down * 24.5f * dt;
        var p = GlobalPosition + _vel * dt;
        var g = _world.GroundHeight(p) + 0.3f;
        if (p.Y < g) { p.Y = g; _vel = Vector3.Zero; }
        GlobalPosition = p;
        Face(_dir);
        if (_target != null && _target.BodyDistance(GlobalPosition) < _target.Def.Size * 1.6f + 1.5f) Boom();
    }

    private void Patrol(float dt)
    {
        if (_travel < _s.Range.Max)
        {
            var p = GlobalPosition + _dir * 5f * dt;
            p.Y = _world.GroundHeight(p) + 0.3f;
            GlobalPosition = p;
            _travel += 5f * dt;
        }
        Face(_dir);
        _cd -= dt;
        var search = _s.Search.Present ? _s.Search.Max : 40f;
        if (_cd > 0 || _target == null || _target.BodyDistance(GlobalPosition) > search) return;
        // Rafale de vingt balles (« 20.4 × 20 »).
        _cd = 1.4f;
        var from = GlobalPosition + Vector3.Up * 0.9f;
        for (var i = 0; i < Math.Max(1, _s.Pellets); i++)
        {
            var aim = _target.Center + new Vector3((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(-0.6, 0.6), (float)GD.RandRange(-1.0, 1.0));
            var d = (aim - from).Normalized();
            Bullets.For(_world).Add(new Bullets.Shot
            {
                Pos = from + d * (i * 0.6f), Vel = d * 600f, Life = search / 600f + 0.1f, MaxLife = search / 600f + 0.1f, Damage = _dmg, Far = 1f,
                FalloffExp = 1f, Local = _local, Color = new Color(1f, 0.75f, 0.35f), Length = 3f, Width = 0.05f,
            });
        }
        Sfx.At("e5_zegunfire", from, -4f, 1.15f, 60);
    }

    private void Run(float dt)
    {
        // Speed Star : course rapide en zigzag ; les ennemis proches la prennent pour cible.
        var zig = Mathf.Sin(_life * 2.2f) * 0.8f;
        var d = _dir.Rotated(Vector3.Up, zig).Normalized();
        var p = GlobalPosition + d * 16f * dt;
        p.Y = _world.GroundHeight(p) + 0.3f;
        GlobalPosition = p;
        Face(d);
    }

    private void Boom()
    {
        if (_done) return;
        _done = true;
        if (_s.Family == "speedstar" || _s.Family == "patroller")
        {
            Effects.Smoke(_world, GlobalPosition + Vector3.Up * 0.5f, 1f, 1.2f);
            QueueFree();
            return;
        }
        if (_local) AirRaid.Explode(_world, GlobalPosition + Vector3.Up * 0.5f, _blast, _dmg);
        QueueFree();
    }
}

/// <summary>
/// Zone de soutien (poste ou charge de pistolet) : soin, puissance (+ % de dégâts) ou protection (− % de dégâts reçus) pour le
/// soldat local qui s'y trouve. Chaque pair applique la zone à son propre soldat.
/// </summary>
public partial class SupportZone : Node3D
{
    private World _world;
    public string Family;
    public float Power, Radius, Life;
    private float _tick;

    public static readonly List<SupportZone> Active = new();

    public static SupportZone Create(World world, Edf5Spec s, Vector3 at, float power, float radius, float life, bool post)
    {
        var z = new SupportZone { _world = world, Family = s.Family, Power = power, Radius = Mathf.Max(radius, 2f), Life = life > 0 ? life : 30f, Position = at };
        var color = s.Family switch
        {
            "lifevendor" or "lifespout" => new Color(0.3f, 1f, 0.5f), "powerpost" or "powerassist" => new Color(1f, 0.45f, 0.15f),
            _ => new Color(0.35f, 0.6f, 1f),
        };
        z.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = z.Radius, BottomRadius = z.Radius, Height = 0.06f, RadialSegments = 40 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(color, 0.16f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            },
            Position = Vector3.Up * 0.2f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        if (post)
        {
            var model = AirRaidModels.Load(s.Family, new Color(0.85f, 0.86f, 0.84f), color * 0.6f);
            if (model != null) z.AddChild(model);
            else z.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.25f, Height = 1.2f, Material = Mats.Painted(new Color(0.9f, 0.9f, 0.88f), 0.4f) }, Position = Vector3.Up * 0.6f });
            z.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.12f) }, MaterialOverride = Mats.Glow(color, 4f), Position = Vector3.Up * 1.45f });
        }
        z.AddChild(new OmniLight3D { LightColor = color, LightEnergy = 1f, OmniRange = Mathf.Min(z.Radius, 20f), Position = Vector3.Up * 1.5f });
        Active.Add(z);
        return z;
    }

    public override void _ExitTree() => Active.Remove(this);

    public bool Contains(Vector3 p) => new Vector2(p.X - GlobalPosition.X, p.Z - GlobalPosition.Z).Length() <= Radius && Mathf.Abs(p.Y - GlobalPosition.Y) < Radius + 4f;

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        Life -= dt;
        if (Life <= 0) { QueueFree(); return; }
        var p = _world.LocalPlayer;
        // Soin : « PV par image » d'EDF 5, à 60 images par seconde.
        if (Family is "lifevendor" or "lifespout" && p != null && !p.Downed && Contains(p.GlobalPosition)) p.Heal(Power * 60f * dt);
        _tick -= dt;
        if (_tick > 0) return;
        _tick = 1.2f;
        Effects.Ring(_world, GlobalPosition, Mathf.Min(Radius * 0.6f, 12f), Family is "lifevendor" or "lifespout" ? new Color(0.3f, 1f, 0.5f)
            : Family is "powerpost" or "powerassist" ? new Color(1f, 0.45f, 0.15f) : new Color(0.35f, 0.6f, 1f), 0.6f);
    }

    /// <summary>Bonus de dégâts du soldat (zones de puissance qui le contiennent : la plus forte).</summary>
    public static float PowerAt(Vector3 p)
    {
        var best = 0f;
        foreach (var z in Active)
            if (z.Family is "powerpost" or "powerassist" && z.Contains(p)) best = Mathf.Max(best, z.Power / 100f);
        return best;
    }

    /// <summary>Réduction des dégâts reçus (zones de protection qui contiennent le soldat : la plus forte).</summary>
    public static float GuardAt(Vector3 p)
    {
        var best = 0f;
        foreach (var z in Active)
            if (z.Family is "guardpost" or "guardassist" && z.Contains(p)) best = Mathf.Max(best, z.Power / 100f);
        return Mathf.Clamp(best, 0f, 0.95f);
    }
}

/// <summary>Leurre (ballon, Speed Star) : les ennemis proches le prennent pour cible ; durabilité gérée par l'hôte.</summary>
public partial class Decoy : Node3D
{
    private World _world;
    public int Id;
    public float Health, Life, Lure = 90f;
    private Node3D _follow;
    private static int _next = 1;

    public static readonly Dictionary<int, Decoy> All = new();

    public static Decoy Create(World world, Vector3 at, float durability, float life, int seed)
    {
        var d = new Decoy { _world = world, Health = durability, Life = life, Position = at, Id = -(1000 + (_next++ % 100000)) };
        var model = AirRaidModels.Load("decoy", new Color(0.2f, 0.65f, 0.3f), new Color(0.1f, 0.1f, 0.1f));
        if (model != null) d.AddChild(model);
        else
        {
            d.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 1.4f, Height = 2.8f }, MaterialOverride = Mats.Painted(new Color(0.2f, 0.65f, 0.3f), 0.5f), Position = Vector3.Up * 6f });
            d.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.02f, Height = 5f }, MaterialOverride = Mats.Metal(new Color(0.2f, 0.2f, 0.2f)), Position = Vector3.Up * 2.5f });
        }
        All[d.Id] = d;
        return d;
    }

    /// <summary>Leurre porté par un robot (Speed Star) : suit son porteur.</summary>
    public static Decoy Attach(World world, Node3D carrier, float lure)
    {
        var d = new Decoy { _world = world, Health = 1e6f, Life = 60f, Lure = lure, Id = -(1000 + (_next++ % 100000)), _follow = carrier };
        carrier.AddChild(d);
        All[d.Id] = d;
        return d;
    }

    /// <summary>Point visé par les ennemis.</summary>
    public Vector3 Point => _follow != null ? GlobalPosition : GlobalPosition + Vector3.Up * 1f;

    public override void _ExitTree() => All.Remove(Id);

    public override void _Process(double delta)
    {
        Life -= (float)delta;
        if (_follow == null && GetChildCount() > 0 && GetChild(0) is Node3D b)
            b.Position = new Vector3(Mathf.Sin(Life * 0.9f) * 0.15f, Mathf.Sin(Life * 1.3f) * 0.1f, 0);
        if (Life <= 0 || Health <= 0)
        {
            if (Health <= 0) Effects.Explosion(_world, GlobalPosition + Vector3.Up * 6f, 2f, ExplosionFx.Explosive);
            QueueFree();
        }
    }

    /// <summary>Hôte : coup reçu d'un ennemi.</summary>
    public void Hit(float dmg) => Health -= dmg;
}

/// <summary>Mur d'énergie d'un abri : arrête les ennemis et leurs tirs ; l'hôte le détruit quand sa part de durabilité est épuisée.</summary>
public partial class BunkerWall : StaticBody3D
{
    public Bunker Owner;

    public void Hit(float dmg) => Owner?.Hit(dmg);
}

/// <summary>Abri de l'Air Raider : ensemble de murs d'énergie (cube, dôme couvert, cage, mur), durabilité commune.</summary>
public partial class Bunker : Node3D
{
    private World _world;
    public float Health;
    private float _life = 240f;
    private StandardMaterial3D _mat;
    private static readonly List<Bunker> Live = new();

    public static void Spawn(World world, Edf5Spec s, Vector3 at, Vector3 dir, float durability)
    {
        var b = new Bunker { _world = world, Health = durability, Position = at, Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Z), 0) };
        b._mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.45f, 0.75f, 1f, 0.32f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            EmissionEnabled = true, Emission = new Color(0.3f, 0.6f, 1f), EmissionEnergyMultiplier = 0.6f,
        };
        void Wall(Vector3 c, Vector3 size, float yaw = 0f)
        {
            var w = new BunkerWall { Owner = b, CollisionLayer = 1, CollisionMask = 0, Position = c, Rotation = new Vector3(0, yaw, 0) };
            w.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
            w.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = b._mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            // Arêtes lumineuses.
            w.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(size.X, 0.12f, size.Z + 0.02f) }, MaterialOverride = Mats.Glow(new Color(0.5f, 0.85f, 1f), 3f), Position = new Vector3(0, size.Y / 2f, 0) });
            b.AddChild(w);
        }
        void Ring(float radius, int n, float h, float thick, float gapIndex = -1)
        {
            for (var i = 0; i < n; i++)
            {
                if (i == (int)gapIndex) continue;
                var a = (i + 0.5f) * Mathf.Tau / n;
                var c = new Vector3(Mathf.Sin(a) * radius, h / 2f, Mathf.Cos(a) * radius);
                var w = 2f * radius * Mathf.Tan(Mathf.Pi / n) + 0.1f;
                Wall(c, new Vector3(w, h, thick), a);
            }
        }
        switch (s.Family)
        {
            case "aabunker": Ring(4.5f, 8, 4f, 0.3f, 4); Wall(new Vector3(0, 4.2f, 0), new Vector3(9.5f, 0.3f, 9.5f)); break;
            case "heavybunker": Ring(7f, 10, 6f, 0.6f, 5); Wall(new Vector3(0, 6.2f, 0), new Vector3(14.5f, 0.4f, 14.5f)); break;
            case "shelter": Ring(2.6f, 8, 3.2f, 0.25f); Wall(new Vector3(0, 3.3f, 0), new Vector3(5.6f, 0.25f, 5.6f)); break;
            case "prison": Ring(8f, 12, 8f, 0.4f); break;
            case "emwall": Wall(new Vector3(0, 4f, 0), new Vector3(s.Name.StartsWith("Border") ? 60f : 30f, 8f, 0.6f)); break;
            default: Ring(4.5f, 8, 3.6f, 0.3f, 4); break;
        }
        world.FxRoot.AddChild(b);
        Sfx.At("e5_bunker", at, 0f, 1f);
        Live.Add(b);
        // Au plus douze abris : le plus ancien s'efface.
        while (Live.Count > 12) { var old = Live[0]; Live.RemoveAt(0); if (IsInstanceValid(old)) old.QueueFree(); }
    }

    public override void _ExitTree() => Live.Remove(this);

    public void Hit(float dmg)
    {
        Health -= dmg;
        if (Health > 0) return;
        Effects.Sparks(_world, GlobalPosition + Vector3.Up * 2f, Vector3.Up);
        QueueFree();
    }

    public override void _Process(double delta)
    {
        _life -= (float)delta;
        _mat.AlbedoColor = new Color(0.45f, 0.75f, 1f, 0.26f + 0.06f * Mathf.Sin(_life * 3f));
        if (_life <= 0) QueueFree();
    }
}
