using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Vaisseau-mère (voir docs/ARCHITECTURE.md, « Vaisseau-mère ») : IA hôte, effets rejoués chez chaque pair, dégâts
/// localisés (canon ouvert, tourelles), lancement de vagues, séquence de mort et écrasement au loin.
///
/// <list type="bullet">
/// <item>Arrivée : il descend des nuages (430 → 165 m) en 24 s, invulnérable, puis sonne la charge (corne).</item>
/// <item>Canon principal : la couronne s'ouvre, le cœur descend et s'emballe (lueur, trait de visée, disque au sol qui
/// grandit) ; puis une colonne d'énergie dévaste la zone (soldats, alliés, immeubles, mobilier) en suivant lentement le
/// soldat visé ; la couronne reste ouverte quelques secondes puis se referme. Seul le canon ouvert est vulnérable :
/// cœur ×4 (visé), ×3 (explosion), trappes ouvertes ×1,5 ; ailleurs, étincelles sans dégât.</item>
/// <item>Tourelles : dix (six au début, quatre de plus en deuxième phase), rafales de traits d'énergie ; chacune a ses
/// PV, et sa destruction arrache 2 % des PV du vaisseau (et lâche une caisse).</item>
/// <item>Soutes : drones, volants et vaisseaux de largage lancés par vagues (plafond de 16 en vol).</item>
/// <item>Phases à 66 % et 33 % : tourelles déployées, charge plus courte, tirs plus longs, double tir en fin de combat,
/// lancements plus fréquents.</item>
/// </list>
/// Réseau : aucune RPC nouvelle, genres 100 à 162 d'<c>EnemyAttack</c> (100/101 couronne, 102 charge, 103/104 début et
/// fin du tir, 105 point d'impact, 110+i tir de tourelle, 130+i tourelle détruite, 150+b / 154+b soute ouverte / fermée,
/// 158 lancement, 160 phase, 161 tourelles déployées, 162 arrivée).
/// </summary>
public partial class Enemy
{
    public bool IsMothership => Def.Type == EnemyType.Mothership;

    private CollisionShape3D _msHullShape, _msCoreShape, _msIrisShape;
    private readonly CollisionShape3D[] _msTurretShapes = new CollisionShape3D[BugModel.MsTurretCount];
    private readonly float[] _msTurretHp = new float[BugModel.MsTurretCount];

    // État partagé (tous les pairs, mis à jour par les genres d'attaque).
    private int _msPhase;
    private bool _msOpen;
    private float _msChargeT = -1f, _msChargeDur = 7f, _msFireT = -1f, _msFireDur = 4f;
    private Vector3 _msTarget, _msTargetNet;
    private MeshInstance3D _msMarker;
    private StandardMaterial3D _msMarkerMat;
    private AudioStreamPlayer3D _msHum, _msVoice, _msBeamSnd;
    private float _msFxT, _msBoomT, _msShakeT;

    // IA (hôte).
    private bool _msArrived;
    private float _msArriveT, _msCycleCd = 9f, _msLaunchCd = 22f, _msCloseT = -1f, _msWanderT, _msDmgT, _msBuildT, _msNetT, _msBurstT;
    private readonly float[] _msTurretCd = new float[BugModel.MsTurretCount];
    private readonly int[] _msBurst = new int[BugModel.MsTurretCount];
    private Vector3 _msGoal, _msVel;
    private bool _msSecond, _msTurretHint;
    private int _msLaunchN;
    private readonly List<(float At, EnemyType Type, int Bay)> _msPending = new();
    private readonly List<int> _msLaunched = new();

    /// <summary>Journal du vaisseau-mère (tests : --fps, --mothership-log, --showcase=mothership).</summary>
    private static readonly bool MsLogOn = Screenshot.CmdArg("fps") != null || Screenshot.CmdArg("mothership-log") != null || Screenshot.CmdArg("showcase") == "mothership";

    private void MsLog(string msg)
    {
        if (MsLogOn) GD.Print($"[mothership] n°{Id} t={_world.Elapsed:0.0} PV {Health:0}/{MaxHealth:0} phase {_msPhase} : {msg}");
    }

    public int MothershipPhase => _msPhase;
    public bool MothershipOpen => _msOpen;
    public bool MothershipArrived => _msArrived;
    public float MothershipTurretHealth(int i) => _msTurretHp[i];

    /// <summary>Point visé par le canon (charge ou tir), pour le radar et l'esquive du bot ; null sinon.</summary>
    public Vector3? MothershipDangerZone => _msChargeT >= 0f || _msFireT >= 0f ? _msTarget : null;

    // ---------------- Création ----------------

    /// <summary>Formes : 0 = coque (convexe), 1 = cœur (point faible, canon ouvert), 2 = trappes ouvertes, puis une par tourelle.</summary>
    private void CreateMothershipShapes(float hpMul)
    {
        var pts = new List<Vector3>();
        for (var z = -212f; z <= 212f; z += 26.5f)
        {
            var zc = Mathf.Clamp(z, -210f, 210f);
            var w = BugModel.MsHalfWidth(zc);
            pts.Add(new Vector3(0, BugModel.MsUnder(zc, 0f), z));
            foreach (var s in new[] { -1f, 1f })
            {
                pts.Add(new Vector3(s * w, 1f, z));
                pts.Add(new Vector3(s * w * 0.7f, 16f, z));
                pts.Add(new Vector3(s * w * 0.7f, BugModel.MsUnder(zc, w * 0.7f), z));
            }
        }
        _msHullShape = new CollisionShape3D { Shape = new ConvexPolygonShape3D { Points = pts.ToArray() } };
        AddChild(_msHullShape);
        _msCoreShape = new CollisionShape3D { Shape = new SphereShape3D { Radius = BugModel.MsCoreR }, Position = BugModel.MsIris };
        AddChild(_msCoreShape);
        _msIrisShape = new CollisionShape3D { Shape = new CylinderShape3D { Radius = BugModel.MsIrisR + 4f, Height = 8f }, Position = BugModel.MsIris + Vector3.Down * 4f, Disabled = true };
        AddChild(_msIrisShape);
        for (var i = 0; i < BugModel.MsTurretCount; i++)
        {
            _msTurretShapes[i] = new CollisionShape3D { Shape = new SphereShape3D { Radius = 5.5f }, Position = BugModel.MsTurrets[i], Disabled = i >= BugModel.MsTurretsInitial };
            AddChild(_msTurretShapes[i]);
            _msTurretHp[i] = 9000f * hpMul;
            _msTurretCd[i] = 3f + i * 0.7f;
        }
        _mShapes = new[] { _msCoreShape }.Concat(_msTurretShapes).ToArray();
        State = EnemyState.Moving;
    }

    private void MothershipReady()
    {
        _msHum = new AudioStreamPlayer3D { Bus = "Sfx", MaxDistance = 2200f, UnitSize = 260f, VolumeDb = 2f, AttenuationFilterCutoffHz = 20000f };
        _msVoice = new AudioStreamPlayer3D { Bus = "Sfx", MaxDistance = 2500f, UnitSize = 320f, VolumeDb = 6f, AttenuationFilterCutoffHz = 20000f };
        _msBeamSnd = new AudioStreamPlayer3D { Bus = "Sfx", MaxDistance = 900f, UnitSize = 90f, VolumeDb = 4f };
        AddChild(_msHum);
        AddChild(_msVoice);
        AddChild(_msBeamSnd);
        // Disque de visée au sol (additif, dégradé radial) : grandit pendant la charge.
        var g = new Gradient();
        g.SetColor(0, new Color(1f, 0.25f, 0.5f, 0.9f));
        g.SetColor(1, new Color(0.6f, 0.1f, 0.4f, 0f));
        g.AddPoint(0.82f, new Color(1f, 0.2f, 0.45f, 0.25f));
        g.AddPoint(0.93f, new Color(1f, 0.35f, 0.55f, 0.85f));
        _msMarkerMat = new StandardMaterial3D
        {
            AlbedoTexture = new GradientTexture2D { Gradient = g, Width = 128, Height = 128, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f) },
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = false,
        };
        _msMarker = new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(2f, 2f) }, MaterialOverride = _msMarkerMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, TopLevel = true, Visible = false };
        AddChild(_msMarker);
    }

    /// <summary>Formes recalées sur le modèle animé : cœur (descend à l'ouverture), trappes, tourelles (sortie, destruction).</summary>
    private void SyncMothershipShapes()
    {
        if (!IsInsideTree() || _msCoreShape == null) return;
        _msCoreShape.GlobalPosition = Model.MsCorePos;
        _msIrisShape.Disabled = Model.MsIrisOpen < 0.6f || State == EnemyState.Dead;
        for (var i = 0; i < BugModel.MsTurretCount; i++)
        {
            _msTurretShapes[i].GlobalPosition = Model.MsTurretPos(i) + Model.GlobalBasis.Y * -2f;
            _msTurretShapes[i].Disabled = !Model.MsTurretDeployed(i);
        }
    }

    // ---------------- Dégâts localisés ----------------

    /// <summary>
    /// Dégâts réellement subis (hôte) : tourelle touchée → elle encaisse (détruite : 2 % des PV du vaisseau) ; canon
    /// ouvert : cœur ×4 visé (le tireur a compté ×1,5) ou ×3 (explosion, rayon), trappes ×1,5 ; ailleurs rien.
    /// </summary>
    public float MothershipAdjustDamage(float dmg, bool weak, Vector3 at)
    {
        if (!_msArrived) return 0f;
        var blast = BlastOrigin;
        var p = blast ?? at;
        for (var i = 0; i < BugModel.MsTurretCount; i++)
        {
            if (!Model.MsTurretDeployed(i)) continue;
            if (p.DistanceTo(_msTurretShapes[i].GlobalPosition) > (blast != null ? 12f : 7.5f)) continue;
            _msTurretHp[i] -= dmg * (blast != null ? 1.4f : 1f);
            return _msTurretHp[i] <= 0f ? HostKillTurret(i) : 0f;
        }
        if (!_msOpen) return 0f;
        var dc = p.DistanceTo(Model.MsCorePos);
        if (dc < BugModel.MsCoreR + (blast != null ? 10f : 2.5f)) return blast == null && weak ? dmg * 4f / 1.5f : dmg * 3f;
        return dc < BugModel.MsIrisR + 12f ? dmg * 1.5f : 0f;
    }

    /// <summary>Multiplicateur affiché chez le tireur (0 : étincelles sans chiffre), d'après l'état connu localement.</summary>
    public float MothershipDisplayScale(Vector3 at, bool weak)
    {
        if (!_msArrived && Net.I.IsHost) return 0f;
        for (var i = 0; i < BugModel.MsTurretCount; i++)
            if (Model.MsTurretDeployed(i) && at.DistanceTo(_msTurretShapes[i].GlobalPosition) < 7.5f) return 1f;
        if (!_msOpen) return 0f;
        var dc = at.DistanceTo(Model.MsCorePos);
        if (dc < BugModel.MsCoreR + 2.5f) return weak ? 4f / 1.5f : 3f;
        return dc < BugModel.MsIrisR + 12f ? 1.5f : 0f;
    }

    /// <summary>Impact visible : étincelles sur la coque, éclairs violets sur le cœur ouvert.</summary>
    public void MothershipHitFx(Vector3 at)
    {
        var fx = ExplosionFx.For(_world);
        var core = _msOpen && at.DistanceTo(Model.MsCorePos) < BugModel.MsCoreR + 3f;
        var n = (at - (core ? Model.MsCorePos : Center)).Normalized();
        for (var i = 0; i < (core ? 5 : 3); i++)
            fx.Spark(at, (n + new Vector3((float)GD.RandRange(-0.8, 0.8), (float)GD.RandRange(-0.8, 0.8), (float)GD.RandRange(-0.8, 0.8))).Normalized() * (float)GD.RandRange(5, 12),
                core ? 0.25f : 0.14f, (float)GD.RandRange(0.2, 0.5), core ? new Color(0.8f, 0.55f, 1f) : new Color(1f, 0.85f, 0.55f));
    }

    /// <summary>Tourelle détruite (hôte) : diffusée, caisse lâchée, 2 % des PV du vaisseau arrachés.</summary>
    private float HostKillTurret(int i)
    {
        var p = Model.MsTurretPos(i);
        MsSend(130 + i, p);
        var ground = new Vector3(Mathf.Clamp(p.X, -200f, 200f), 0.3f, Mathf.Clamp(p.Z, -210f, 150f));
        _world.HostSpawnCrate(Crates.Roll(_world.Rng), ground);
        var alive = Enumerable.Range(0, BugModel.MsTurretCount).Count(t => !Model.MsTurretDead(t));
        MsLog($"tourelle {i} détruite ({alive} restantes)");
        if (!_msTurretHint)
        {
            _msTurretHint = true;
            Net.I.BroadcastMessage("Tourelle détruite : la coque se fissure ! Continuez, et visez le canon quand il s'ouvre.", 1);
        }
        return MaxHealth * 0.02f;
    }

    // ---------------- IA (hôte) ----------------

    private void MsSend(int kind, Vector3 target)
    {
        MothershipFx(kind, target);
        Net.I.BroadcastEnemyAttack(Id, kind, target);
    }

    private float ChargeDur => _msSecond ? 2.6f : _msPhase switch { 0 => 7.5f, 1 => 6f, _ => 4.8f };
    private float FireDur => _msPhase switch { 0 => 4f, 1 => 4.6f, _ => 5.2f };

    private void HostTickMothership(float dt)
    {
        _attackAnim = Mathf.Max(0, _attackAnim - dt * 1.5f);
        var hasTarget = _world.NearestPlayer(GlobalPosition with { Y = 0 }, out _, out var target);
        var pos = GlobalPosition;
        if (!_msArrived)
        {
            // Descente des nuages, lente et lourde ; la corne retentit à l'arrivée.
            _msArriveT += dt;
            var k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_msArriveT / 24f, 0f, 1f));
            pos.Y = Mathf.Lerp(BugModel.MsArrivalAlt, BugModel.MsAlt, k);
            pos.X = Mathf.MoveToward(pos.X, Mathf.Clamp(pos.X, -60f, 60f), dt * 3f);
            GlobalPosition = pos;
            _msShakeT -= dt;
            if (_msShakeT <= 0f) { _msShakeT = 0.6f; _world.ShakeCamera(pos with { Y = 0 }, 0.18f, 600f); }
            if (_msArriveT >= 24f || Frozen)
            {
                _msArrived = true;
                if (Frozen) GlobalPosition = pos with { Y = BugModel.MsAlt };
                MsSend(162, GlobalPosition);
                MsLog("en position au-dessus de la ville");
            }
            if (hasTarget) Model.MsAim = target + Vector3.Up;
            AnimateModel(dt, 0f, 0f);
            return;
        }
        if (Frozen)
        {
            // Vitrine : immobile, mais les actions forcées se déroulent.
            TickMothershipFx(dt, true);
            HostTickMsCycle(dt, hasTarget, target, true);
            if (hasTarget) Model.MsAim = target + Vector3.Up;
            AnimateModel(dt, 0f, 0f);
            return;
        }
        // Phases : 66 % et 33 % des PV.
        var frac = Health / MaxHealth;
        var want = frac <= 0.33f ? 2 : frac <= 0.66f ? 1 : 0;
        if (want > _msPhase)
        {
            MsSend(160, new Vector3(want, 0, 0));
            if (want == 1) MsSend(161, GlobalPosition);
            Net.I.BroadcastMessage(want == 1 ? "Le vaisseau-mère déploie ses tourelles de réserve et accélère sa charge !" : "Le vaisseau-mère est en perdition : il tire sans relâche. Achevez-le !", 1);
            MsLog($"phase {want}");
        }
        var rate = _msPhase switch { 0 => 1f, 1 => 1.25f, _ => 1.6f };

        // Déplacement : dérive majestueuse au-dessus du centre, vers le gros des soldats, sans quitter la ville.
        _msWanderT -= dt;
        if (_msWanderT <= 0f || _msGoal == Vector3.Zero)
        {
            _msWanderT = (float)GD.RandRange(24, 34);
            var c = Vector3.Zero;
            var n = 0;
            foreach (var p in _world.AlivePlayerPositions()) { c += p; n++; }
            c = n > 0 ? c / n : new Vector3(0, 0, -40f);
            _msGoal = new Vector3(Mathf.Clamp(c.X + (float)GD.RandRange(-50, 50), -80f, 80f), BugModel.MsAlt, Mathf.Clamp(c.Z - 20f + (float)GD.RandRange(-40, 40), -130f, 40f));
        }
        var flat = new Vector3(_msGoal.X - pos.X, 0, _msGoal.Z - pos.Z);
        var dist = flat.Length();
        var wantV = dist > 2f ? flat / dist * Mathf.Min(Def.Speed * (1f + 0.2f * _msPhase), dist * 0.06f) : Vector3.Zero;
        if (_msFireT >= 0f) wantV *= 0.25f;
        _msVel = _msVel.MoveToward(wantV, dt * 0.4f);
        pos += _msVel * dt;
        pos.Y = Mathf.MoveToward(pos.Y, BugModel.MsAlt, dt * 4f);
        GlobalPosition = pos;
        _speedNow = _msVel.Length();
        // Lacet très lent : la proue se tourne vers les soldats.
        if (hasTarget)
        {
            var to = new Vector3(target.X - pos.X, 0, target.Z - pos.Z);
            if (to.LengthSquared() > 400f)
            {
                var dy = Mathf.Wrap(Mathf.Atan2(to.X, to.Z) - _yaw, -Mathf.Pi, Mathf.Pi);
                _yaw += Mathf.Clamp(dy, -1f, 1f) * 0.025f * dt;
                Rotation = new Vector3(0, _yaw, 0);
            }
        }
        TickMothershipFx(dt, true);
        HostTickMsCycle(dt * rate, hasTarget, target, false);
        HostTickMsTurrets(dt * rate, hasTarget, target);
        HostTickMsLaunch(dt);
        if (hasTarget) Model.MsAim = target + Vector3.Up;
        AnimateModel(dt, _speedNow, _attackAnim);
        State = _msChargeT >= 0f || _msFireT >= 0f ? EnemyState.Attacking : EnemyState.Moving;
    }

    /// <summary>Cycle du canon (hôte) : refroidissement, ouverture et charge, tir qui suit la cible, aération, fermeture.</summary>
    private void HostTickMsCycle(float dt, bool hasTarget, Vector3 target, bool forcedOnly)
    {
        if (_msFireT >= 0f)
        {
            // Le point d'impact suit lentement le soldat visé.
            if (hasTarget)
            {
                var track = _msPhase switch { 0 => 2.6f, 1 => 3.6f, _ => 4.6f };
                var flatT = new Vector3(target.X, 0, target.Z);
                var cur = new Vector3(_msTarget.X, 0, _msTarget.Z);
                var nxt = cur.MoveToward(flatT, track * dt);
                _msTarget = GroundAt(nxt);
            }
            _msNetT -= dt;
            if (_msNetT <= 0f)
            {
                _msNetT = 0.2f;
                MsSend(105, _msTarget);
            }
            if (_msFireT >= _msFireDur)
            {
                MsSend(104, _msTarget);
                if (_msPhase >= 2 && !_msSecond && hasTarget)
                {
                    _msSecond = true;
                    StartMsCharge(target);
                }
                else
                {
                    _msSecond = false;
                    _msCloseT = 3.5f;
                }
            }
            return;
        }
        if (_msChargeT >= 0f)
        {
            if (_msChargeT >= _msChargeDur) MsSend(103, _msTarget);
            return;
        }
        if (_msCloseT >= 0f)
        {
            _msCloseT -= dt;
            if (_msCloseT < 0f)
            {
                MsSend(101, Model.MsCorePos);
                _msCycleCd = _msPhase switch { 0 => 16f, 1 => 12f, _ => 8f };
            }
            return;
        }
        if (forcedOnly) return;
        _msCycleCd -= dt;
        if (_msCycleCd <= 0f && hasTarget) StartMsCharge(target);
    }

    /// <summary>Sol sous un point (rayon vers le bas, gravats et toits compris), à défaut y = 0.</summary>
    private Vector3 GroundAt(Vector3 p)
    {
        var q = PhysicsRayQueryParameters3D.Create(new Vector3(p.X, 120f, p.Z), new Vector3(p.X, -20f, p.Z), 1u);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        return hit.Count > 0 ? (Vector3)hit["position"] : new Vector3(p.X, 0f, p.Z);
    }

    private void StartMsCharge(Vector3 target)
    {
        // Point visé : le soldat, ramené à portée de la colonne (≤ 230 m à l'horizontale du cœur).
        var core = Model.MsCorePos;
        var flat = new Vector3(target.X - core.X, 0, target.Z - core.Z);
        if (flat.Length() > 230f) flat = flat.Normalized() * 230f;
        var t = GroundAt(new Vector3(core.X + flat.X, 0, core.Z + flat.Z));
        if (!_msOpen) MsSend(100, core);
        MsSend(102, t);
        MsLog($"charge du canon vers {t.Snapped(Vector3.One)} ({ChargeDur:0.0} s)");
    }

    /// <summary>Tourelles (hôte) : rafales de trois traits d'énergie lents vers le soldat le plus proche à portée.</summary>
    private void HostTickMsTurrets(float dt, bool hasTarget, Vector3 target)
    {
        if (!hasTarget) return;
        for (var i = 0; i < BugModel.MsTurretCount; i++)
        {
            if (!Model.MsTurretDeployed(i)) continue;
            var muzzle = Model.MsTurretMuzzle(i);
            if (_msBurst[i] > 0)
            {
                _msTurretCd[i] -= dt;
                if (_msTurretCd[i] > 0f) continue;
                _msBurst[i]--;
                _msTurretCd[i] = _msBurst[i] > 0 ? 0.22f : (float)GD.RandRange(3.6, 5.4);
                if (!_world.NearestPlayer(muzzle, out _, out var aimAt) || aimAt.DistanceTo(muzzle) > 300f) continue;
                var aim = aimAt + Vector3.Up + new Vector3((float)GD.RandRange(-2.5, 2.5), (float)GD.RandRange(-0.5, 1.5), (float)GD.RandRange(-2.5, 2.5));
                var dir = (aim - muzzle).Normalized();
                var p = Projectile.Spawn(_world, ProjectileKind.HeavyBolt, muzzle, dir * 110f, true, Def.Damage * 0.7f * _world.DamageMul * DamageScale, 2.6f);
                p.SourceEnemy = this;
                Sfx.At("laser", muzzle, 6f, 0.45f, 40);
                Net.I.BroadcastEnemyAttack(Id, 110 + i, aim);
                continue;
            }
            _msTurretCd[i] -= dt;
            if (_msTurretCd[i] <= 0f && target.DistanceTo(muzzle) < 300f)
            {
                _msBurst[i] = 3;
                _msTurretCd[i] = 0f;
            }
        }
    }

    /// <summary>Lancements par les soutes (hôte) : groupe selon la phase, plafond de 16 unités lancées encore en vie.</summary>
    private void HostTickMsLaunch(float dt)
    {
        for (var i = _msPending.Count - 1; i >= 0; i--)
        {
            if (_msPending[i].At > _world.Elapsed) continue;
            var (_, type, bay) = _msPending[i];
            _msPending.RemoveAt(i);
            var bp = Model.MsBayPos(bay) + new Vector3((float)GD.RandRange(-4, 4), 0, (float)GD.RandRange(-6, 6));
            var e = _world.HostSpawnEnemy(type, bp, airdrop: type != EnemyType.DropShip);
            if (type == EnemyType.DropShip) e.GlobalPosition = bp + Vector3.Down * 10f;
            _msLaunched.Add(e.Id);
            MsSend(158, bp + Vector3.Down * 30f);
        }
        _msLaunchCd -= dt;
        if (_msLaunchCd > 0f) return;
        _msLaunchCd = _msPhase switch { 0 => 38f, 1 => 30f, _ => 24f };
        _msLaunched.RemoveAll(id => !_world.EnemiesById.TryGetValue(id, out var e) || e.State == EnemyState.Dead);
        if (_msLaunched.Count >= 16) return;
        var active = _world.EnemiesById.Values.Count(x => x.State != EnemyState.Dead && !x.IsStatic);
        if (active >= 120) return;
        var b = _msLaunchN++ % BugModel.MsBays.Length;
        MsSend(150 + b, Model.MsBayPos(b));
        var t0 = _world.Elapsed + 1.8f;
        var group = _msPhase switch
        {
            0 => _msLaunchN % 2 == 1 ? new[] { EnemyType.Drone, EnemyType.Drone, EnemyType.Drone, EnemyType.Drone } : new[] { EnemyType.AlienFlyer, EnemyType.AlienFlyer, EnemyType.AlienFlyer },
            1 => new[] { EnemyType.DropShip, EnemyType.Drone, EnemyType.Drone, EnemyType.Drone },
            _ => new[] { EnemyType.DropShip, EnemyType.AlienFlyer, EnemyType.AlienFlyer, EnemyType.Drone, EnemyType.Drone },
        };
        for (var i = 0; i < group.Length; i++) _msPending.Add((t0 + i * 0.7f, group[i], b));
        MsLog($"lancement soute {b} : {string.Join(", ", group)}");
        var close = _world.GetTree().CreateTimer(t0 - _world.Elapsed + group.Length * 0.7f + 2f, false);
        close.Timeout += () => { if (IsInstanceValid(this) && State != EnemyState.Dead) MsSend(154 + b, Model.MsBayPos(b)); };
    }

    /// <summary>Tests (hôte) : open, close, charge, launch, turret (prochaine tourelle détruite), phase1, phase2, kill.</summary>
    public void MothershipForce(string act)
    {
        var hasTarget = _world.NearestPlayer(GlobalPosition with { Y = 0 }, out _, out var target);
        if (!hasTarget) target = GlobalPosition with { Y = 0 } + Basis.Z * 60f;
        _msArrived = true;
        switch (act)
        {
            case "open": if (!_msOpen) MsSend(100, Model.MsCorePos); break;
            case "close": if (_msOpen) MsSend(101, Model.MsCorePos); break;
            case "charge": if (_msChargeT < 0f && _msFireT < 0f) StartMsCharge(target); break;
            case "launch": _msLaunchCd = 0f; HostTickMsLaunch(0f); break;
            case "turret":
                for (var i = 0; i < BugModel.MsTurretCount; i++)
                    if (Model.MsTurretDeployed(i)) { _msTurretHp[i] = 0f; Health -= HostKillTurret(i); break; }
                break;
            case "phase1": Health = Mathf.Min(Health, MaxHealth * 0.65f); MsSend(160, new Vector3(1, 0, 0)); MsSend(161, GlobalPosition); break;
            case "phase2": Health = Mathf.Min(Health, MaxHealth * 0.32f); MsSend(160, new Vector3(2, 0, 0)); MsSend(161, GlobalPosition); break;
            case "kill": _world.HostDamageEnemy(Id, Health + 1f, Model.MsCorePos, Net.I.MyId, true); if (State != EnemyState.Dead) { _msOpen = true; _world.HostDamageEnemy(Id, Health * 2f + 1f, Model.MsCorePos, Net.I.MyId, true); } break;
        }
    }

    // ---------------- Déroulés (tous les pairs) ----------------

    private void ClientTickMothership(float dt)
    {
        if (_world.NearestPlayer(GlobalPosition with { Y = 0 }, out _, out var t)) Model.MsAim = t + Vector3.Up;
        TickMothershipFx(dt, false);
    }

    /// <summary>Charge (lueur, étincelles qui convergent, disque au sol), tir (colonne, impact qui laboure le sol) ; l'hôte inflige les dégâts.</summary>
    private void TickMothershipFx(float dt, bool host)
    {
        var fx = ExplosionFx.For(_world);
        if (!host) _msTarget = _msTarget.Lerp(_msTargetNet, Mathf.Clamp(dt * 6f, 0f, 1f));
        if (_msChargeT >= 0f)
        {
            _msChargeT += dt;
            var k = Mathf.Clamp(_msChargeT / _msChargeDur, 0f, 1f);
            Model.MsCharge = k;
            Model.MsSight = k;
            Model.MsImpact = _msTarget;
            _attackAnim = Mathf.Max(_attackAnim, 0.5f);
            // Étincelles qui convergent vers le cœur, de plus en plus serrées.
            var core = Model.MsCorePos;
            _msFxT -= dt;
            if (_msFxT <= 0f && fx.CameraDistance(core) < 900f)
            {
                _msFxT = Mathf.Lerp(0.09f, 0.025f, k);
                var d = new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(-0.6, 0.6), (float)GD.RandRange(-1, 1)).Normalized();
                var from = core + d * (BugModel.MsCoreR + 18f);
                fx.Spark(from, (core - from) * 1.8f, 0.6f + k * 0.6f, 0.55f, new Color(0.75f, 0.5f, 1f));
            }
            // Disque au sol : grandit et pulse ; poussière aspirée à la fin.
            _msMarker.Visible = true;
            _msMarker.GlobalPosition = _msTarget + Vector3.Up * 0.35f;
            var pulse = 0.85f + 0.15f * Mathf.Sin(_msChargeT * (4f + k * 16f));
            _msMarker.Scale = Vector3.One * (MsBeamRadius * 2.1f * (0.35f + 0.65f * k) * pulse);
            _msMarkerMat.AlbedoColor = new Color(1f, 1f, 1f, 0.35f + 0.65f * k);
            if (k > 0.75f && GD.Randf() < dt * 20f && fx.CameraDistance(_msTarget) < 400f)
            {
                var a = (float)GD.RandRange(0, Mathf.Tau);
                var p = _msTarget + new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)) * (float)GD.RandRange(6, 20);
                fx.DustPuff(p, (_msTarget - p) * 0.6f + Vector3.Up * 3f, 3f, 1.4f, new Color(0.4f, 0.37f, 0.33f));
            }
            if (host && _msChargeT >= _msChargeDur) { /* le tir est lancé par HostTickMsCycle */ }
        }
        else if (_msFireT < 0f) _msMarker.Visible = false;
        if (_msFireT >= 0f)
        {
            _msFireT += dt;
            Model.MsFiring = _msFireT < _msFireDur;
            Model.MsImpact = _msTarget;
            Model.MsCharge = 1f;
            _attackAnim = 1f;
            _msMarker.Visible = true;
            _msMarker.GlobalPosition = _msTarget + Vector3.Up * 0.35f;
            _msMarker.Scale = Vector3.One * MsBeamRadius * 2.3f * (1f + 0.06f * Mathf.Sin(_msFireT * 30f));
            if (_msBeamSnd != null) _msBeamSnd.GlobalPosition = _msTarget + Vector3.Up * 20f;
            // Le sol se soulève et brûle sous la colonne.
            var near = fx.CameraDistance(_msTarget) < 450f;
            _msFxT -= dt;
            if (_msFxT <= 0f && near)
            {
                _msFxT = 0.07f;
                var a = (float)GD.RandRange(0, Mathf.Tau);
                var r = (float)GD.RandRange(2, MsBeamRadius);
                var p = _msTarget + new Vector3(Mathf.Cos(a) * r, 0.5f, Mathf.Sin(a) * r);
                fx.FirePuff(p, Vector3.Up * (float)GD.RandRange(8, 16) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 4f, (float)GD.RandRange(3, 6), 1.2f, new Color(0.9f, 0.6f, 1f));
                fx.Chunk(p, new Vector3(Mathf.Cos(a) * 8f, (float)GD.RandRange(8, 16), Mathf.Sin(a) * 8f), (float)GD.RandRange(0.3, 0.8), 3f, new Color(0.25f, 0.23f, 0.21f));
                fx.SmokePuff(p + Vector3.Up * 4f, Vector3.Up * 6f + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 5f, 7f, 3.5f, new Color(0.12f, 0.11f, 0.12f));
            }
            _msBoomT -= dt;
            if (_msBoomT <= 0f)
            {
                _msBoomT = 0.38f;
                var a = (float)GD.RandRange(0, Mathf.Tau);
                Effects.Explosion(_world, _msTarget + new Vector3(Mathf.Cos(a), 0.6f, Mathf.Sin(a)) * (float)GD.RandRange(2, MsBeamRadius + 4f), (float)GD.RandRange(6, 11), ExplosionFx.Plasma);
                _world.ShakeCamera(_msTarget, 0.7f, 160f);
            }
            if (host) HostMsBeamDamage(dt);
        }
    }

    /// <summary>Rayon de la colonne du canon (dégâts pleins dedans, partiels jusqu'à deux fois plus loin).</summary>
    private const float MsBeamRadius = 10f;

    /// <summary>Dégâts de la colonne (hôte) : soldats et alliés (plein dans la colonne, partiel autour), immeubles, mobilier.</summary>
    private void HostMsBeamDamage(float dt)
    {
        _msDmgT -= dt;
        if (_msDmgT > 0f) return;
        _msDmgT = 0.1f;
        var t = _msTarget;
        var dmg = Def.Damage * _world.DamageMul * DamageScale;
        foreach (var (peer, ppos) in TargetsNear(t, MsBeamRadius * 2.2f))
        {
            var d = new Vector2(ppos.X - t.X, ppos.Z - t.Z).Length();
            if (ppos.Y - t.Y > 30f) continue;
            Net.I.DamagePlayer(peer, d < MsBeamRadius ? dmg : dmg * 0.25f, t);
        }
        if (_world.Allies != null)
            foreach (var a in _world.Allies.ById.Values.ToList())
                if (!a.Dead && new Vector2(a.GlobalPosition.X - t.X, a.GlobalPosition.Z - t.Z).Length() < MsBeamRadius * 1.5f) _world.Allies.HostDamage(a, dmg * 1.5f);
        _msBuildT -= 0.1f;
        if (_msBuildT > 0f) return;
        _msBuildT = 0.5f;
        foreach (var b in _world.Buildings)
        {
            if (b.Destroyed) continue;
            var local = t - b.GlobalPosition;
            var dx = local.X - Mathf.Clamp(local.X, -b.Size.X / 2, b.Size.X / 2);
            var dz = local.Z - Mathf.Clamp(local.Z, -b.Size.Z / 2, b.Size.Z / 2);
            if (dx * dx + dz * dz <= (MsBeamRadius + 6f) * (MsBeamRadius + 6f)) _world.HostBuildingDamage(b.Id, 1600f, t + Vector3.Up * 4f);
        }
        _world.Decor?.HostBlast(t, MsBeamRadius + 6f, 320f, 0);
        // Ennemis au sol pris dans la colonne : le vaisseau ne fait pas de quartier.
        foreach (var e in _world.EnemiesById.Values.ToList())
            if (e != this && e.State != EnemyState.Dead && !e.IsBoss && !e.Def.Flying && new Vector2(e.GlobalPosition.X - t.X, e.GlobalPosition.Z - t.Z).Length() < MsBeamRadius)
                _world.HostDamageEnemy(e.Id, 60f, e.Center, 0, false);
    }

    /// <summary>Effets du vaisseau-mère (tous les pairs), genres 100 à 162 (voir l'en-tête du fichier).</summary>
    public void MothershipFx(int kind, Vector3 target)
    {
        if (State == EnemyState.Dead) return;
        if (!Net.I.IsHost && MsLogOn) MsLog($"effet {kind} reçu de l'hôte");
        switch (kind)
        {
            case 100:
                _msOpen = true;
                Model.MsSetIris(true);
                PlayMs("ms_open", 6f);
                break;
            case 101:
                _msOpen = false;
                Model.MsSetIris(false);
                PlayMs("ms_close", 4f);
                Model.MsCharge = 0f;
                break;
            case 102:
                _msTarget = _msTargetNet = target;
                _msChargeT = 0f;
                _msChargeDur = ChargeDur;
                _msFireT = -1f;
                Effects.Ring(_world, target, MsBeamRadius, new Color(1f, 0.2f, 0.45f), _msChargeDur + 0.6f);
                Effects.Ring(_world, target, MsBeamRadius * 2.2f, new Color(1f, 0.45f, 0.2f), _msChargeDur + 0.6f);
                PlayMs("ms_charge", 8f, _msChargeDur > 3f ? 7.5f / _msChargeDur : 1.6f);
                break;
            case 103:
                _msChargeT = -1f;
                _msFireT = 0f;
                _msFireDur = FireDur;
                _msTarget = _msTargetNet = target;
                Model.MsSight = 0f;
                Model.MsFiring = true;
                Effects.Explosion(_world, target + Vector3.Up, 16f, ExplosionFx.Plasma);
                Sfx.At("ms_fire", target + Vector3.Up * 30f, 14f, 1f, 0);
                PlayMs("ms_fire", 10f);
                if (Sfx.Loop("ms_beam") is { } loop && _msBeamSnd != null)
                {
                    _msBeamSnd.Stream = loop;
                    _msBeamSnd.GlobalPosition = target + Vector3.Up * 20f;
                    _msBeamSnd.Play();
                }
                _world.ShakeCamera(target, 1.2f, 260f);
                ExplosionFx.For(_world).Flash(target + Vector3.Up * 8f, new Color(0.85f, 0.6f, 1f), 30f, 120f, 0.6f);
                break;
            case 104:
                _msFireT = -1f;
                Model.MsFiring = false;
                Model.MsCharge = 0.15f;
                _msMarker.Visible = false;
                if (_msBeamSnd != null && _msBeamSnd.Playing) _msBeamSnd.Stop();
                Effects.Smoke(_world, target + Vector3.Up * 2f, 9f, 6f);
                break;
            case 105:
                _msTargetNet = target;
                break;
            case >= 110 and < 130:
            {
                if (Net.I.IsHost) break;
                var i = kind - 110;
                var m = Model.MsTurretMuzzle(i);
                Projectile.Spawn(_world, ProjectileKind.HeavyBolt, m, (target - m).Normalized() * 110f, false, 0, 0);
                Sfx.At("laser", m, 6f, 0.45f, 40);
                break;
            }
            case >= 130 and < 150:
            {
                var i = kind - 130;
                Model.MsKillTurret(i);
                Effects.Explosion(_world, target, 14f, 0);
                Effects.Explosion(_world, target + Vector3.Down * 4f, 9f, ExplosionFx.Plasma);
                var fx = ExplosionFx.For(_world);
                for (var q = 0; q < 12; q++)
                    fx.Chunk(target, new Vector3((float)GD.RandRange(-8, 8), (float)GD.RandRange(-14, -2), (float)GD.RandRange(-8, 8)), (float)GD.RandRange(0.5, 1.4), 7f, new Color(0.18f, 0.18f, 0.19f));
                Sfx.At("explosion", target, 14f, 0.6f, 0);
                _world.ShakeCamera(target, 0.6f, 300f);
                break;
            }
            case >= 150 and < 154:
                Model.MsSetBay(kind - 150, true);
                Sfx.At("ms_launch", target, 12f, 1f, 0);
                break;
            case >= 154 and < 158:
                Model.MsSetBay(kind - 154, false);
                break;
            case 158:
            {
                // Lancement : traînée violette de la soute vers la ville.
                var from = target + Vector3.Up * 30f;
                Effects.Tracer(_world.FxRoot, from, target, new Color(0.75f, 0.45f, 1f), 1.2f, 0.6f);
                ExplosionFx.For(_world).Spark(from, Vector3.Down * 20f, 1.2f, 0.8f, new Color(0.8f, 0.6f, 1f));
                break;
            }
            case 160:
                _msPhase = Mathf.Clamp((int)target.X, 0, 2);
                PlayMs("ms_horn", 8f, _msPhase == 2 ? 0.9f : 1f);
                _world.ShakeCamera(GlobalPosition with { Y = 0 }, 0.6f, 900f);
                break;
            case 161:
                for (var i = BugModel.MsTurretsInitial; i < BugModel.MsTurretCount; i++) Model.MsDeployTurret(i, true);
                break;
            case 162:
                _msArrived = true;
                PlayMs("ms_horn", 8f);
                _world.ShakeCamera(GlobalPosition with { Y = 0 }, 0.8f, 1000f);
                break;
        }
    }

    private void PlayMs(string name, float db, float pitch = 1f)
    {
        if (_msVoice == null || Sfx.Stream(name) is not { } s) return;
        if (name is "ms_open" or "ms_close" or "ms_charge" or "ms_fire")
        {
            // Bruits du canon : depuis le cœur ; la corne et les ordres depuis le centre de la coque.
            _msVoice.GlobalPosition = Model.MsCorePos;
        }
        else _msVoice.Position = Vector3.Zero;
        _msVoice.Stream = s;
        _msVoice.VolumeDb = db;
        _msVoice.PitchScale = pitch;
        _msVoice.Play();
    }

    /// <summary>Bourdonnement continu de la coque (boucle), lancé dès que la synthèse est prête.</summary>
    private void MothershipAudio()
    {
        if (_msHum == null || _msHum.Playing || State == EnemyState.Dead) return;
        if (Sfx.Loop("ms_hum") is not { } loop) return;
        _msHum.Stream = loop;
        _msHum.Play();
    }

    // ---------------- Mort ----------------

    /// <summary>
    /// Mort (tous les pairs) : le cœur implose, des explosions courent sur toute la coque, les feux s'éteignent ; le
    /// vaisseau s'incline et glisse en perdant de l'altitude, traînant feu et fumée, et s'écrase au loin hors de la ville
    /// (boule de feu, onde de poussière, secousse jusqu'à 2 km), où son épave brûle jusqu'à la fin de la mission.
    /// Le butin tombe sous l'endroit où le cœur a cédé.
    /// </summary>
    private void MothershipDie()
    {
        MsLog("abattu");
        _msChargeT = _msFireT = -1f;
        if (_msBeamSnd != null && _msBeamSnd.Playing) _msBeamSnd.Stop();
        if (_msHum != null) _msHum.Stop();
        if (_msMarker != null) _msMarker.Visible = false;
        Model.MothershipDeath();
        var core = Model.MsCorePos;
        LandingPoint = new Vector3(Mathf.Clamp(core.X, -190f, 190f), 0.3f, Mathf.Clamp(core.Z, -200f, 140f));
        Effects.Explosion(_world, core, 26f, ExplosionFx.Plasma);
        ExplosionFx.For(_world).Flash(core, new Color(0.85f, 0.6f, 1f), 40f, 400f, 1.2f);
        _world.ShakeCamera(core with { Y = 0 }, 1.2f, 1500f);
        Sfx.At("ms_death", core, 16f, 1f, 0);
        PlayMs("ms_death", 12f, 0.8f);
        var fall = new MothershipFall { Name = "MothershipFall" };
        fall.Setup(_world, this);
        _world.FxRoot.AddChild(fall);
    }

    /// <summary>Caméra : jamais translucide (la coque est loin au-dessus).</summary>
    private void MothershipCameraFade() { }
}

/// <summary>
/// Chute du vaisseau-mère abattu (tous les pairs, même trajectoire : point d'écrasement déduit de la position et du cap à
/// la mort) : chapelet d'explosions sur la coque, inclinaison et glissade, traînée de feu et de fumée, impact au loin.
/// </summary>
public partial class MothershipFall : Node
{
    private World _world;
    private Enemy _ship;
    private Vector3 _start, _crash;
    private Basis _basis0;
    private float _t, _boomT, _smokeT;
    private bool _crashed;
    private readonly RandomNumberGenerator _rng = new() { Seed = 4404 };

    public const float FallTime = 26f;

    public void Setup(World w, Enemy ship)
    {
        _world = w;
        _ship = ship;
        _start = ship.GlobalPosition;
        _basis0 = ship.GlobalBasis;
        // S'écarte du centre de la ville (vers le nord ou le sud selon sa position) et va s'écraser à ~750 m.
        var away = new Vector3(_start.X, 0, _start.Z + 40f);
        if (away.LengthSquared() < 400f) away = new Vector3(ship.GlobalBasis.Z.X, 0, ship.GlobalBasis.Z.Z);
        away = away.Normalized();
        _crash = new Vector3(_start.X, -14f, _start.Z) + away * 760f;
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_ship)) { QueueFree(); return; }
        var dt = (float)delta;
        _t += dt;
        var fx = ExplosionFx.For(_world);
        if (!_crashed)
        {
            // Trajectoire : glisse en accélérant, perd de l'altitude de plus en plus vite, s'incline et pique du nez.
            var k = Mathf.Clamp(_t / FallTime, 0f, 1f);
            var h = k * k;
            var p = _start.Lerp(_crash, h);
            p.Y = Mathf.Lerp(_start.Y, _crash.Y, k * k * k * 0.6f + h * 0.4f);
            var dir = (_crash - _start) with { Y = 0 };
            var side = dir.Cross(Vector3.Up).Normalized();
            var tilt = new Basis(side, -0.16f * Mathf.SmoothStep(0, 1, k * 1.4f)) * new Basis(dir.Normalized(), 0.22f * Mathf.SmoothStep(0, 1, k * 1.2f));
            _ship.GlobalTransform = new Transform3D(tilt * _basis0, p);
            // Explosions en cascade sur la coque (plus serrées au début), fumée et feu traînés.
            _boomT -= dt;
            if (_boomT <= 0f)
            {
                _boomT = Mathf.Lerp(0.18f, 0.6f, k);
                var local = new Vector3(_rng.RandfRange(-130f, 130f), _rng.RandfRange(-12f, 14f), _rng.RandfRange(-190f, 190f));
                local.X = Mathf.Clamp(local.X, -BugModel.MsHalfWidth(local.Z) * 0.9f, BugModel.MsHalfWidth(local.Z) * 0.9f);
                var bp = _ship.GlobalTransform * local;
                Effects.Explosion(_world, bp, _rng.RandfRange(12f, 26f), _rng.Randf() < 0.3f ? ExplosionFx.Plasma : 0);
                if (_rng.Randf() < 0.35f) Sfx.At("explosion", bp, 12f, _rng.RandfRange(0.5f, 0.75f), 0);
            }
            _smokeT -= dt;
            if (_smokeT <= 0f)
            {
                _smokeT = 0.12f;
                for (var i = 0; i < 3; i++)
                {
                    var local = new Vector3(_rng.RandfRange(-90f, 90f), 10f, _rng.RandfRange(-150f, 150f));
                    var sp = _ship.GlobalTransform * local;
                    fx.SmokePuff(sp, Vector3.Up * 6f, _rng.RandfRange(22f, 36f), 9f, new Color(0.09f, 0.085f, 0.09f));
                    if (i == 0) fx.FirePuff(sp, Vector3.Up * 3f, _rng.RandfRange(12f, 20f), 1.6f, new Color(1f, 0.55f, 0.25f));
                }
            }
            if (_t >= FallTime) Crash();
            return;
        }
        // Épave au sol : fumée et feux qui ne s'éteignent pas.
        _smokeT -= dt;
        if (_smokeT <= 0f)
        {
            _smokeT = 0.35f;
            var local = new Vector3(_rng.RandfRange(-110f, 110f), 12f, _rng.RandfRange(-170f, 170f));
            var sp = _ship.GlobalTransform * local;
            fx.SmokePuff(sp, Vector3.Up * 9f, _rng.RandfRange(30f, 46f), 14f, new Color(0.08f, 0.075f, 0.08f));
            fx.FirePuff(sp, Vector3.Up * 4f, _rng.RandfRange(10f, 18f), 2f, new Color(1f, 0.5f, 0.2f));
        }
    }

    private void Crash()
    {
        _crashed = true;
        var c = _crash with { Y = 2f };
        for (var i = 0; i < 6; i++)
        {
            var o = new Vector3(_rng.RandfRange(-120f, 120f), _rng.RandfRange(0f, 20f), _rng.RandfRange(-160f, 160f));
            Effects.Explosion(_world, c + _ship.GlobalBasis * o, _rng.RandfRange(40f, 70f), i % 2 == 0 ? 0 : ExplosionFx.Plasma);
        }
        Effects.Dust(_world, c, new Vector3(520f, 60f, 520f));
        ExplosionFx.For(_world).Flash(c + Vector3.Up * 40f, new Color(1f, 0.7f, 0.4f), 60f, 1400f, 2.5f);
        _world.ShakeCamera(c, 1.8f, 2600f);
        Sfx.At("ms_crash", c, 20f, 1f, 0);
        foreach (var o in new[] { new Vector3(-60, 0, -80), new Vector3(40, 0, 30), new Vector3(10, 0, 120) })
            _world.FxRoot.AddChild(RuinFire.Plume(c + _ship.GlobalBasis * o, _world.Mission.Time == TimeOfDay.Night));
    }
}
