using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Machines extraterrestres (voir docs/ARCHITECTURE.md, « Machines ») : Marcheur, Tripode et vaisseau de largage.
/// Elles gardent leurs distances et tirent (IA hôte) ; effets d'attaque rejoués chez chaque pair par <see cref="MachineFx"/>
/// (RPC EnemyAttack existante, genres 40 à 79) ; formes de collision recalées sur le modèle animé ; points faibles
/// (genoux ×2, noyau ×3, soute ouverte ×3) ; destruction en plusieurs explosions.
/// </summary>
public partial class Enemy
{
    public bool IsMachine => Def.Type is EnemyType.Walker or EnemyType.Tripod or EnemyType.DropShip;
    private bool IsShip => Def.Type == EnemyType.DropShip;

    /// <summary>Origine de l'explosion en cours de traitement par <see cref="World.HostExplosion"/> (soute du vaisseau).</summary>
    public static Vector3? BlastOrigin;

    /// <summary>Formes recalées chaque image sur le modèle animé (jambes, genoux, noyau) et leur transformation dans le modèle.</summary>
    private readonly List<(CollisionShape3D Shape, System.Func<Transform3D> Xf)> _mSync = new();
    private CollisionShape3D[] _mShapes;

    // ---------------- Création ----------------

    /// <summary>Formes de collision : 0 = coque, 1 (et 2 pour le Marcheur) = point faible, puis membres.</summary>
    private void CreateMachineShapes()
    {
        var shapes = new List<CollisionShape3D>();
        CollisionShape3D Add(Shape3D s, Vector3 p, System.Func<Transform3D> sync = null)
        {
            var c = new CollisionShape3D { Shape = s, Position = p };
            AddChild(c);
            shapes.Add(c);
            if (sync != null) _mSync.Add((c, sync));
            return c;
        }
        switch (Def.Type)
        {
            case EnemyType.Walker:
                Add(new BoxShape3D { Size = new Vector3(6.4f, 4.6f, 5.6f) }, new Vector3(0, 10.4f, 0.1f), () => Model.WalkerTorso);
                for (var i = 0; i < 2; i++)
                {
                    var leg = i;
                    Add(new SphereShape3D { Radius = BugModel.WalkerKneeRadius }, Vector3.Zero, () => new Transform3D(Basis.Identity, Model.WalkerKnee(leg)));
                }
                for (var i = 0; i < 2; i++)
                {
                    var leg = i;
                    Add(new CapsuleShape3D { Radius = 0.8f, Height = 4.6f }, Vector3.Zero, () => Model.WalkerThigh(leg));
                    Add(new CapsuleShape3D { Radius = 0.65f, Height = 4.8f }, Vector3.Zero, () => Model.WalkerShin(leg));
                }
                Add(new BoxShape3D { Size = new Vector3(4.4f, 1.8f, 2.8f) }, new Vector3(0, 7.4f, 0));
                State = EnemyState.Moving;
                Model.Stomp += OnMachineStomp;
                break;
            case EnemyType.Tripod:
                Add(new CylinderShape3D { Radius = 6f, Height = 6f }, new Vector3(0, BugModel.TBodyY, 0), () => new Transform3D(Basis.Identity, Model.TripodHull));
                Add(new SphereShape3D { Radius = BugModel.TCoreRadius + 0.15f }, Vector3.Zero, () => new Transform3D(Basis.Identity, Model.TripodCore));
                for (var i = 0; i < 3; i++)
                {
                    var leg = i;
                    Add(new CapsuleShape3D { Radius = 1.0f, Height = 12.5f }, Vector3.Zero, () => Model.TripodFemur(leg));
                    Add(new CapsuleShape3D { Radius = 0.75f, Height = 18.5f }, Vector3.Zero, () => Model.TripodTibia(leg));
                }
                State = EnemyState.Moving;
                Model.Stomp += OnMachineStomp;
                break;
            case EnemyType.DropShip:
                Add(new BoxShape3D { Size = new Vector3(19f, 5.4f, 42f) }, new Vector3(0, 2.0f, -0.8f));
                Add(new BoxShape3D { Size = BugModel.ShipBayHalf * 2f }, BugModel.ShipBayPos);
                Add(new BoxShape3D { Size = new Vector3(5.9f, 3.4f, 26f) }, new Vector3(6.4f, -1.95f, -0.8f));
                Add(new BoxShape3D { Size = new Vector3(5.9f, 3.4f, 26f) }, new Vector3(-6.4f, -1.95f, -0.8f));
                Add(new BoxShape3D { Size = new Vector3(4.8f, 4.2f, 25f) }, new Vector3(11.6f, 0.2f, -0.8f));
                Add(new BoxShape3D { Size = new Vector3(4.8f, 4.2f, 25f) }, new Vector3(-11.6f, 0.2f, -0.8f));
                State = EnemyState.Moving;
                break;
        }
        _mShapes = shapes.ToArray();
    }

    /// <summary>Le vaisseau arrive haut dans le ciel ; son grondement de moteurs le suit.</summary>
    private void MachineReady()
    {
        if (!IsShip) return;
        _sEngine = new AudioStreamPlayer3D { Bus = "Sfx", MaxDistance = 320f, UnitSize = 40f, VolumeDb = -4f, Position = Vector3.Down * 2f };
        AddChild(_sEngine);
    }

    private void SyncMachineShapes()
    {
        if (_mSync.Count == 0 || !IsInsideTree()) return;
        foreach (var (shape, xf) in _mSync) shape.GlobalTransform = xf();
    }

    /// <summary>Centre de la machine : torse, coque ou fuselage (suit l'animation).</summary>
    private Vector3 MachineCenter => Def.Type switch
    {
        EnemyType.Walker when IsInsideTree() => Model.WalkerTorso.Origin,
        EnemyType.Tripod when IsInsideTree() => Model.TripodHull,
        EnemyType.Walker => GlobalPosition + Vector3.Up * BugModel.WalkerCenterY,
        EnemyType.Tripod => GlobalPosition + Vector3.Up * BugModel.TBodyY,
        _ => GlobalPosition,
    };

    /// <summary>Journal des machines (tests : --fps ou --machines).</summary>
    private static readonly bool MachineLog = Screenshot.CmdArg("fps") != null || Screenshot.CmdArg("machines") != null;

    private void Log(string msg)
    {
        if (MachineLog) GD.Print($"[machine] {Def.Type} n°{Id} t={_world.Elapsed:0.0} : {msg}");
    }

    /// <summary>Tourne la machine vers un point (tests, vitrine).</summary>
    public void FaceTowards(Vector3 p)
    {
        var d = p - GlobalPosition;
        _yaw = Mathf.Atan2(d.X, d.Z);
        Rotation = new Vector3(0, _yaw, 0);
    }

    /// <summary>Zone visée par un tir lourd en préparation (canon du Marcheur, balayage du Tripode), pour l'esquive du bot.</summary>
    private Vector3? MachineDangerZone =>
        _wChargeT[0] >= 0 ? _wAim[0] : _wChargeT[1] >= 0 ? _wAim[1] : _bT >= 0 ? _bStart.Lerp(_bEnd, Mathf.Clamp((_bT - BeamCharge) / BeamFire, 0f, 1f)) : null;

    // ---------------- Points faibles ----------------

    /// <summary>Forme de collision touchée = point faible ? (tête des insectes, genoux du Marcheur, noyau du Tripode, soute ouverte).</summary>
    public bool IsWeakShape(int shape) => Def.Type switch
    {
        EnemyType.Walker => shape is 1 or 2,
        EnemyType.Tripod => shape == 1,
        EnemyType.DropShip => shape == 1 && HatchOpen,
        // Titan : la tête, et la chair à vif des plaques brisées (formes 16 à 23).
        EnemyType.Titan => shape == 1 || shape >= 16 && shape < 16 + BugModel.TitanPlateCount && TitanPlateBroken(shape - 16),
        EnemyType.Portal => false,
        _ => shape == 1,
    };

    /// <summary>Le tir ne peut rien (vaisseau hors de sa soute ouverte) : étincelles, aucun chiffre de dégâts.</summary>
    public bool Invulnerable(bool weak) => IsShip && !(weak && HatchOpen);

    /// <summary>
    /// Dégâts réellement subis (hôte). Le tireur a déjà appliqué ×1,5 sur un point faible : genoux du Marcheur ×2,
    /// noyau du Tripode ×3, soute ouverte du vaisseau ×3 ; le vaisseau ignore le reste, sauf une explosion dans la soute ouverte.
    /// </summary>
    public float HostAdjustDamage(float dmg, bool weak)
    {
        switch (Def.Type)
        {
            case EnemyType.Walker: return weak ? dmg * 2f / 1.5f : dmg;
            case EnemyType.Tripod: return weak ? dmg * 3f / 1.5f : dmg;
            case EnemyType.DropShip:
                if (BlastOrigin is { } b) return HatchOpen && b.DistanceTo(Model.ShipBay) < 11f ? dmg * 1.5f : 0f;
                return weak && HatchOpen ? dmg * 3f / 1.5f : 0f;
            default: return dmg;
        }
    }

    // ---------------- IA (hôte) ----------------

    private float _mAtkCd = 3f, _mStrafeT, _mStrafe = 1f, _mFace;
    private readonly List<(float At, Vector3 Pos)> _mBlasts = new();
    private float _mClock;

    private void HostTickMachine(float dt)
    {
        _mClock += dt;
        _attackAnim = Mathf.Max(0, _attackAnim - dt * 1.5f);
        // Obus en vol : explosion à l'arrivée.
        for (var i = _mBlasts.Count - 1; i >= 0; i--)
        {
            if (_mBlasts[i].At > _mClock) continue;
            _world.HostEnemyBlast(_mBlasts[i].Pos, 6.5f, Def.Damage * _world.DamageMul * DamageScale);
            _mBlasts.RemoveAt(i);
        }
        if (IsShip) { TickShip(dt); AnimateModel(dt, _speedNow, _attackAnim); return; }
        if (HostStuck(dt)) { _world.HostRespawnStuck(this); return; }
        if (!_world.NearestPlayer(GlobalPosition, out var peer, out var target))
        {
            _speedNow = Mathf.MoveToward(_speedNow, 0, dt * 2f);
            TickBeam(dt, true);
            AnimateModel(dt, _speedNow, _attackAnim);
            return;
        }
        Model.MachineAim(target + Vector3.Up);
        if (Def.Type == EnemyType.Walker) TickWalker(dt, target);
        else TickTripod(dt, target);
        AnimateModel(dt, _speedNow, _attackAnim);
        State = _attackAnim > 0.05f || _bT >= 0 || _wChargeT[0] >= 0 || _wChargeT[1] >= 0 ? EnemyState.Attacking : EnemyState.Moving;
    }

    /// <summary>
    /// Déplacement à distance (Marcheur, Tripode) : se rapproche par le champ de flux au-delà de <paramref name="far"/>,
    /// recule face à la cible en deçà de <paramref name="near"/>, sinon avance en zigzag ; s'arrête pendant les tirs lourds.
    /// </summary>
    private void MachineMove(float dt, Vector3 target, float near, float far, float turnRate, float speedMul)
    {
        var pos = GlobalPosition;
        var to = target - pos;
        to.Y = 0;
        var dist = to.Length();
        var dirT = dist > 0.01f ? to / dist : Vector3.Forward;
        _mStrafeT -= dt;
        if (_mStrafeT <= 0)
        {
            _mStrafeT = (float)GD.RandRange(7, 13);
            _mStrafe = GD.Randf() < 0.5f ? -1f : 1f;
        }
        Vector3 desired;
        var backing = false;
        if (dist > far)
        {
            desired = dist < 70f ? dirT : _world.Flow.Direction(pos);
            if (desired == Vector3.Zero) desired = dirT;
        }
        else if (dist < near) { desired = -dirT; backing = true; }
        else desired = (dirT.Cross(Vector3.Up) * _mStrafe * 0.85f + dirT * 0.35f).Normalized();
        var sep = MachineSeparation(2.6f * Def.Size);
        sep.Y = 0;
        var move = desired + sep * 1.5f;
        if (move.LengthSquared() > 1) move = move.Normalized();
        var hold = speedMul <= 0f;
        if (hold) move = Vector3.Zero;
        _moveDir = _moveDir.Lerp(move, Mathf.Clamp(dt * 1.5f, 0, 1));

        // Orientation : vers la marche, ou vers la cible en reculant et pendant les tirs.
        var face = hold || backing || _moveDir.LengthSquared() < 0.04f ? dirT : _moveDir.Normalized();
        var targetYaw = Mathf.Atan2(face.X, face.Z);
        var dy = Mathf.Wrap(targetYaw - _yaw, -Mathf.Pi, Mathf.Pi);
        _yaw += Mathf.Clamp(dy, -turnRate * dt, turnRate * dt);
        Rotation = new Vector3(0, _yaw, 0);
        var fwd = new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw));
        // Avance selon le regard (les pieds ne glissent pas de côté) ; à reculons si la cible est trop près.
        var along = _moveDir.Dot(fwd);
        var want = Def.Speed * along * speedMul * (Mathf.Abs(dy) > 0.9f ? 0.3f : 1f);
        _speedNow = Mathf.MoveToward(_speedNow, want, dt * 2.5f);
        var next = pos + fwd * _speedNow * dt;
        next.Y = 0;
        if (_world.Flow.IsBlocked(next) && !_world.Flow.IsBlocked(pos)) { next = pos; _speedNow = 0; _mStrafe = -_mStrafe; }
        GlobalPosition = next;
    }

    /// <summary>Écartement entre machines seulement (les fourmis passent entre leurs jambes, elles ne les dévient pas).</summary>
    private Vector3 MachineSeparation(float radius)
    {
        var push = Vector3.Zero;
        var p = GlobalPosition;
        foreach (var o in _world.EnemiesById.Values)
        {
            if (o == this || !o.IsMachine || o.State == EnemyState.Dead || o.Def.Flying != Def.Flying) continue;
            var d = p - o.GlobalPosition;
            d.Y = 0;
            var r = radius + o.Def.Size * 2.6f;
            var len = d.Length();
            if (len < r && len > 0.001f) push += d / len * (1f - len / r);
        }
        return push;
    }

    /// <summary>Grondement continu des moteurs du vaisseau (boucle synthétisée, démarrée dès qu'elle est prête).</summary>
    private void MachineAudio()
    {
        if (_sEngine == null || _sEngine.Playing || State == EnemyState.Dead) return;
        if (Sfx.Loop("ship_engine") is not { } loop) return;
        _sEngine.Stream = loop;
        _sEngine.Play((float)GD.RandRange(0, 3));
    }

    // ---- Marcheur : canons lourds (charge 1,3 s visible, obus explosif) ----

    private readonly float[] _wChargeT = { -1f, -1f };
    private readonly Vector3[] _wAim = new Vector3[2];
    private float _wSecond = -1f;

    private void TickWalker(float dt, Vector3 target)
    {
        var dist = new Vector2(target.X - GlobalPosition.X, target.Z - GlobalPosition.Z).Length();
        var charging = _wChargeT[0] >= 0 || _wChargeT[1] >= 0;
        MachineMove(dt, target, 38f, 78f, 0.9f, charging ? 0.35f : 1f);

        _mAtkCd -= dt;
        if (_mAtkCd <= 0 && dist < Def.AttackRange && !charging)
        {
            _mAtkCd = Def.AttackCooldown * (float)GD.RandRange(0.85, 1.2);
            StartWalkerCharge(0, target);
            _wSecond = 0.75f;
        }
        if (_wSecond >= 0)
        {
            _wSecond -= dt;
            if (_wSecond < 0) StartWalkerCharge(1, target);
        }
        for (var arm = 0; arm < 2; arm++)
        {
            if (_wChargeT[arm] < 0) continue;
            _wChargeT[arm] += dt;
            if (_wChargeT[arm] < 1.3f) continue;
            _wChargeT[arm] = -1f;
            // Trajectoire tendue : le premier obstacle (immeuble) arrête l'obus.
            var from = Model.WalkerMuzzle(arm);
            var aim = _wAim[arm];
            var q = PhysicsRayQueryParameters3D.Create(from, aim + (aim - from).Normalized() * 2f, 1u);
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
            var impact = hit.Count > 0 ? (Vector3)hit["position"] : aim;
            MachineFx(42 + arm, impact);
            Log($"tir du canon {arm} → {impact.Snapped(Vector3.One)} ({(hit.Count > 0 ? "obstacle" : "cible")})");
            Net.I.BroadcastEnemyAttack(Id, 42 + arm, impact);
            _mBlasts.Add((_mClock + from.DistanceTo(impact) / ShellSpeed, impact));
        }
    }

    private const float ShellSpeed = 170f;

    private void StartWalkerCharge(int arm, Vector3 target)
    {
        // Légère dispersion : rester immobile dans la zone marquée est mortel, en sortir suffit.
        var aim = target + new Vector3((float)GD.RandRange(-1.5, 1.5), 0.3f, (float)GD.RandRange(-1.5, 1.5));
        _wAim[arm] = aim;
        _wChargeT[arm] = 0f;
        _attackAnim = 1f;
        MachineFx(40 + arm, aim);
        Net.I.BroadcastEnemyAttack(Id, 40 + arm, aim);
    }

    // ---- Tripode : rayon de destruction balayé (charge 1,8 s, tir 2,6 s) ----

    private const float BeamCharge = 1.8f, BeamFire = 2.6f;
    private float _bT = -1f, _bTick, _bBlast, _bDmgT;
    /// <summary>Temps écoulé depuis le début de la charge du rayon (-1 : aucun), pour les tests.</summary>
    public float BeamTime => _bT;
    private Vector3 _bStart, _bEnd;
    private bool _bFiring;

    private void TickTripod(float dt, Vector3 target)
    {
        var dist = new Vector2(target.X - GlobalPosition.X, target.Z - GlobalPosition.Z).Length();
        MachineMove(dt, target, 42f, 88f, 0.45f, _bT >= 0 ? 0f : 1f);
        _mAtkCd -= dt;
        if (_mAtkCd <= 0 && _bT < 0 && dist < Def.AttackRange)
        {
            _mAtkCd = Def.AttackCooldown * (float)GD.RandRange(0.85, 1.25);
            // Balayage en travers de la cible (diagonale), du côté du Tripode vers l'arrière de la cible.
            var dir = new Vector3(target.X - GlobalPosition.X, 0, target.Z - GlobalPosition.Z).Normalized();
            var side = dir.Cross(Vector3.Up) * (GD.Randf() < 0.5f ? -1f : 1f);
            var g = new Vector3(target.X, Mathf.Max(target.Y, 0f), target.Z);
            var start = g + side * 17f - dir * 9f;
            var end = g - side * 17f + dir * 9f;
            MachineFx(50, start);
            MachineFx(51, end);
            Log($"balayage {start.Snapped(Vector3.One)} → {end.Snapped(Vector3.One)}");
            Net.I.BroadcastEnemyAttack(Id, 50, start);
            Net.I.BroadcastEnemyAttack(Id, 51, end);
        }
        TickBeam(dt, true);
    }

    /// <summary>Déroulé du rayon (tous les pairs) ; l'hôte applique les dégâts toutes les 0,1 s.</summary>
    private void TickBeam(float dt, bool host)
    {
        if (_bT < 0) return;
        _bT += dt;
        _attackAnim = Mathf.Max(_attackAnim, 0.6f);
        if (_bT < BeamCharge)
        {
            Model.MachineAim(_bStart);
            return;
        }
        if (_bT >= BeamCharge + BeamFire || State == EnemyState.Dead)
        {
            _bT = -1f;
            _bFiring = false;
            Model.TripodBeam(null);
            return;
        }
        var k = (_bT - BeamCharge) / BeamFire;
        k = k * k * (3f - 2f * k);
        var p = _bStart.Lerp(_bEnd, k);
        var from = Model.TripodEmitter;
        var dir = (p - from).Normalized();
        var q = PhysicsRayQueryParameters3D.Create(from, p + dir * 25f, 1u);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        var impact = hit.Count > 0 ? (Vector3)hit["position"] : p;
        Model.TripodBeam(impact);
        if (!_bFiring)
        {
            _bFiring = true;
            Log($"rayon allumé, impact {impact.Snapped(Vector3.One)}");
            Sfx.At("beam_fire", from, 10f, 1f, 0);
            _world.ShakeCamera(impact, 0.5f, 60f);
        }
        _bTick -= dt;
        if (_bTick <= 0)
        {
            _bTick = 0.07f;
            BeamImpactFx(impact, hit.Count > 0 ? (Vector3)hit["normal"] : Vector3.Up);
        }
        if (!host) return;
        // Dégâts toutes les 0,1 s : le long du faisceau et autour de l'impact ; explosion toutes les 0,35 s.
        _bDmgT -= dt;
        if (_bDmgT > 0) return;
        _bDmgT = 0.1f;
        _bBlast -= 0.1f;
        var tick = _bBlast <= 0;
        var dmg = Def.Damage * _world.DamageMul * DamageScale;
        foreach (var (peerId, ppos) in TargetsNear(from, 250f))
        {
            var c = ppos + Vector3.Up;
            var onBeam = Geometry3D.GetClosestPointToSegment(c, from, impact).DistanceTo(c) < 1.8f;
            if (onBeam || c.DistanceTo(impact) < 4f) Net.I.DamagePlayer(peerId, dmg, impact);
        }
        if (hit.Count > 0 && hit["collider"].AsGodotObject() is Building b && !b.Destroyed) _world.HostBuildingDamage(b.Id, 140f, impact);
        if (tick)
        {
            _bBlast = 0.35f;
            _world.HostEnemyBlast(impact, 4.5f, dmg * 3f);
        }
    }

    /// <summary>Joueurs vivants (pair, position) à moins de <paramref name="r"/> m d'un point.</summary>
    private IEnumerable<(long, Vector3)> TargetsNear(Vector3 p, float r)
    {
        if (_world.LocalPlayer is { Downed: false } lp && lp.GlobalPosition.DistanceTo(p) < r) yield return (Net.I.MyId, lp.GlobalPosition);
        foreach (var (id, rp) in _world.Remotes)
            if (!rp.Downed && rp.GlobalPosition.DistanceTo(p) < r) yield return (id, rp.GlobalPosition);
    }

    // ---- Vaisseau de largage ----

    private enum ShipPhase { Transit, Descend, Open, Drop, Close, Climb, Leave }

    private ShipPhase _sPhase = ShipPhase.Transit;
    private float _sPhaseT, _sDropT, _sGunCd = 4f, _sCruise = 62f, _sHover = 32f;
    private Vector3 _sGoal, _sVel;
    private int _sCycle, _sDropped, _sGun;
    private bool _sHasGoal;
    private AudioStreamPlayer3D _sEngine;
    /// <summary>Soute ouverte (état logique, connu de chaque pair par les genres 60/61).</summary>
    public bool HatchOpen { get; private set; }

    private const int ShipCycles = 4, ShipCargo = 6;

    private void TickShip(float dt)
    {
        _sPhaseT += dt;
        var hasTarget = _world.NearestPlayer(GlobalPosition, out _, out var target);
        if (hasTarget) Model.MachineAim(target + Vector3.Up);
        if (!_sHasGoal) PickShipGoal(hasTarget ? target : GlobalPosition);
        var pos = GlobalPosition;
        var flat = new Vector3(_sGoal.X - pos.X, 0, _sGoal.Z - pos.Z);
        var fd = flat.Length();
        float wantY;
        var wantV = Vector3.Zero;
        switch (_sPhase)
        {
            case ShipPhase.Transit:
                wantY = _sCruise;
                wantV = fd > 1f ? flat / fd * Mathf.Min(Def.Speed, fd * 0.35f) : Vector3.Zero;
                if (fd < 5f && Mathf.Abs(pos.Y - _sCruise) < 8f) SetShipPhase(ShipPhase.Descend);
                break;
            case ShipPhase.Descend:
                wantY = _sHover;
                wantV = flat * 0.3f;
                if (Mathf.Abs(pos.Y - _sHover) < 1.5f) { SetShipPhase(ShipPhase.Open); ShipHatchFx(true); }
                break;
            case ShipPhase.Open:
                wantY = _sHover;
                if (_sPhaseT > 2.4f) { SetShipPhase(ShipPhase.Drop); _sDropped = 0; _sDropT = 0.3f; }
                break;
            case ShipPhase.Drop:
                wantY = _sHover;
                _sDropT -= dt;
                if (_sDropT <= 0 && _sDropped < ShipCargo)
                {
                    _sDropT = 1.15f;
                    _sDropped++;
                    DropCargo();
                }
                if (_sDropped >= ShipCargo && _sPhaseT > ShipCargo * 1.15f + 3f) { SetShipPhase(ShipPhase.Close); ShipHatchFx(false); }
                break;
            case ShipPhase.Close:
                wantY = _sHover;
                if (_sPhaseT > 2.6f)
                {
                    _sCycle++;
                    SetShipPhase(_sCycle >= ShipCycles ? ShipPhase.Leave : ShipPhase.Climb);
                    if (_sPhase == ShipPhase.Leave) PickLeaveGoal();
                }
                break;
            case ShipPhase.Climb:
                wantY = _sCruise;
                if (Mathf.Abs(pos.Y - _sCruise) < 6f || _sPhaseT > 8f) { _sHasGoal = false; SetShipPhase(ShipPhase.Transit); }
                break;
            default: // Leave
                wantY = 95f;
                wantV = fd > 1f ? flat / fd * Def.Speed * 1.3f : Vector3.Zero;
                if (_sPhaseT > 30f || Mathf.Abs(pos.X) > 400f || Mathf.Abs(pos.Z) > 400f)
                {
                    // Parti : retiré sans élimination ni butin.
                    Log("quitte la zone");
                    _world.EnemiesById.Remove(Id);
                    Net.I.BroadcastEnemyDespawn(Id);
                    State = EnemyState.Dead;
                    QueueFree();
                    return;
                }
                break;
        }
        // Pilotage : vitesse horizontale amortie, altitude par ressort.
        var sep = MachineSeparation(24f);
        sep.Y = 0;
        wantV += sep * 6f;
        _sVel = _sVel.MoveToward(wantV, dt * 3.5f);
        var vy = Mathf.Clamp((wantY - pos.Y) * 0.6f, -7f, 6f);
        var next = pos + (_sVel + Vector3.Up * vy) * dt;
        GlobalPosition = next;
        _speedNow = _sVel.Length();
        // Cap : dans le sens de la marche en transit, vers la cible en vol stationnaire ; inclinaison selon l'accélération.
        var face = _sVel.LengthSquared() > 9f ? _sVel : hasTarget ? new Vector3(target.X - pos.X, 0, target.Z - pos.Z) : _sVel;
        if (face.LengthSquared() > 0.01f)
        {
            var ty = Mathf.Atan2(face.X, face.Z);
            _yaw += Mathf.Clamp(Mathf.Wrap(ty - _yaw, -Mathf.Pi, Mathf.Pi), -0.35f * dt, 0.35f * dt);
        }
        var fwd = new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw));
        var right = new Vector3(fwd.Z, 0, -fwd.X);
        var acc = (wantV - _sVel).LimitLength(10f);
        var pitch = Mathf.Clamp(_sVel.Dot(fwd) * 0.012f + acc.Dot(fwd) * 0.02f, -0.15f, 0.15f);
        var roll = Mathf.Clamp(-_sVel.Dot(right) * 0.015f, -0.18f, 0.18f);
        _mFace = Mathf.Lerp(_mFace, roll, Mathf.Clamp(dt * 1.5f, 0, 1));
        Basis = new Basis(Vector3.Up, _yaw) * new Basis(Vector3.Right, pitch) * new Basis(Vector3.Back, _mFace);
        Model.ShipThrust(_sPhase == ShipPhase.Descend ? 0.7f : 1f + Mathf.Clamp(vy, 0, 6) * 0.08f + _speedNow * 0.02f);

        // Tourelles laser en vol stationnaire.
        _sGunCd -= dt;
        if (hasTarget && _sPhase is ShipPhase.Open or ShipPhase.Drop or ShipPhase.Descend && _sGunCd <= 0 && pos.DistanceTo(target) < Def.AttackRange)
        {
            _sGunCd = Def.AttackCooldown * (float)GD.RandRange(0.8, 1.3);
            _sGun = (_sGun + 1) % 2;
            var muzzle = Model.ShipTurretMuzzle(_sGun);
            var aim = target + Vector3.Up + new Vector3((float)GD.RandRange(-1.5, 1.5), 0, (float)GD.RandRange(-1.5, 1.5));
            _world.HostEnemyProjectile(this, 3, muzzle, aim, 1);
            Net.I.BroadcastEnemyAttack(Id, 70 + _sGun, aim);
            _attackAnim = 1f;
        }
        State = _sPhase is ShipPhase.Drop or ShipPhase.Open ? EnemyState.Attacking : EnemyState.Moving;
    }

    private void SetShipPhase(ShipPhase p)
    {
        _sPhase = p;
        _sPhaseT = 0f;
    }

    private void ShipHatchFx(bool open)
    {
        Log(open ? $"ouvre la soute à {GlobalPosition.Snapped(Vector3.One)}" : "ferme la soute");
        ShipHatchFxSend(open);
    }

    private void ShipHatchFxSend(bool open)
    {
        MachineFx(open ? 60 : 61, GlobalPosition);
        Net.I.BroadcastEnemyAttack(Id, open ? 60 : 61, GlobalPosition);
    }

    /// <summary>Point de largage : à 50-70 m du joueur le plus proche, dans la carte ; vol stationnaire au-dessus des toits.</summary>
    private void PickShipGoal(Vector3 near)
    {
        var a = (float)GD.RandRange(0, Mathf.Tau);
        var r = (float)GD.RandRange(50, 70);
        var g = near + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
        g.X = Mathf.Clamp(g.X, -165f, 165f);
        g.Z = Mathf.Clamp(g.Z, -165f, 135f);
        g.Y = 0;
        var top = 0f;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(g + Vector3.Up * 160f, g + Vector3.Down * 5f, 1u));
        if (hit.Count > 0) top = ((Vector3)hit["position"]).Y;
        _sHover = Mathf.Max(30f, top + 20f);
        _sCruise = Mathf.Max(60f, _sHover + 18f);
        _sGoal = g;
        _sHasGoal = true;
    }

    private void PickLeaveGoal()
    {
        var p = GlobalPosition;
        var d = new Vector3(p.X, 0, p.Z);
        if (d.LengthSquared() < 1f) d = Vector3.Forward;
        _sGoal = d.Normalized() * 600f;
        _sHasGoal = true;
    }

    /// <summary>Cargaison : fourmis (ou drones si la mission n'a pas d'insectes au sol) lâchées par la soute.</summary>
    private void DropCargo()
    {
        var active = _world.EnemiesById.Values.Count(e => e.State != EnemyState.Dead && !e.IsStatic);
        if (active >= 125) return;
        var type = CargoType();
        if (type == EnemyType.BlackAnt && GD.Randf() < 0.3f) type = EnemyType.RedAnt;
        else if (type == EnemyType.AlienTrooper && GD.Randf() < 0.2f) type = EnemyType.AlienGuard;
        var p = Model.ShipBay + Basis * new Vector3((float)GD.RandRange(-1.5, 1.5), -1.5f, (float)GD.RandRange(-6, 6));
        var cargo = _world.HostSpawnEnemy(type, p, airdrop: true);
        Log($"largage {type} n°{cargo.Id} depuis {p.Snapped(Vector3.One)}");
        MachineFx(62, p);
        Net.I.BroadcastEnemyAttack(Id, 62, p);
    }

    private EnemyType? _cargo;

    private EnemyType CargoType()
    {
        if (_cargo is { } c) return c;
        var types = _world.Mission.Phases.SelectMany(ph => ph.Waves).SelectMany(w => w.Groups).Select(g => g.type).ToList();
        _cargo = types.Any(t => t is EnemyType.BlackAnt or EnemyType.RedAnt or EnemyType.AcidAnt) ? EnemyType.BlackAnt
            // Mission d'aliens : le vaisseau dépose des fantassins (et parfois un garde).
            : types.Contains(EnemyType.AlienTrooper) ? EnemyType.AlienTrooper
            : !types.Contains(EnemyType.Drone) ? EnemyType.BlackAnt : EnemyType.Drone;
        return _cargo.Value;
    }

    // ---------------- Clients ----------------

    /// <summary>Client : la visée suit le joueur le plus proche ; le rayon du Tripode se déroule localement.</summary>
    private void ClientTickMachine(float dt)
    {
        if (_world.NearestPlayer(GlobalPosition, out _, out var t)) Model.MachineAim(t + Vector3.Up);
        TickBeam(dt, false);
    }

    // ---------------- Effets (tous les pairs) ----------------

    /// <summary>
    /// Effets des attaques des machines : 40/41 charge d'un canon du Marcheur (cible), 42/43 tir (point d'impact),
    /// 50/51 début et fin du balayage du Tripode, 60/61 ouverture et fermeture de la soute, 62 largage, 70/71 tourelles.
    /// </summary>
    public void MachineFx(int kind, Vector3 target)
    {
        if (State == EnemyState.Dead) return;
        switch (kind)
        {
            case 40 or 41:
            {
                var arm = kind - 40;
                Model.WalkerCharge(arm, target);
                Effects.Ring(_world, target, 6.5f, new Color(1f, 0.15f, 0.08f), 1.4f);
                Effects.Ring(_world, target, 2.5f, new Color(1f, 0.15f, 0.08f), 1.4f);
                Sfx.At("cannon_charge", Model.WalkerMuzzle(arm), 6f, 1f, 0);
                break;
            }
            case 42 or 43:
            {
                var arm = kind - 42;
                Model.WalkerFire(arm);
                var from = Model.WalkerMuzzle(arm);
                Effects.MuzzleFlash(_world.FxRoot, from, new Color(1f, 0.65f, 0.3f), 3.2f);
                PuffFx(from, 2.5f, 1.6f, false);
                Sfx.At("mech_cannon", from, 10f, (float)GD.RandRange(0.92, 1.05), 0);
                Shell(from, target);
                _world.ShakeCamera(from, 0.35f, 45f);
                break;
            }
            case 50:
                _bStart = target;
                break;
            case 51:
                _bEnd = target;
                _bT = 0f;
                _bTick = 0f;
                _bBlast = 0f;
                _bFiring = false;
                Model.TripodCharge();
                // Trajectoire du balayage marquée au sol.
                for (var i = 0; i <= 4; i++)
                    Effects.Ring(_world, _bStart.Lerp(_bEnd, i / 4f), i is 0 or 4 ? 5f : 2.6f, new Color(1f, 0.3f, 0.08f), BeamCharge + 0.6f);
                Sfx.At("beam_charge", Model.TripodEmitter, 10f, 1f, 0);
                break;
            case 60 or 61:
                HatchOpen = kind == 60;
                Model.ShipSetHatch(HatchOpen);
                Sfx.At("hatch", Model.ShipBay, 10f, HatchOpen ? 1f : 0.9f, 0);
                if (HatchOpen && Net.I.IsHost && _hatchTold != _world.GetInstanceId())
                {
                    _hatchTold = _world.GetInstanceId();
                    Net.I.BroadcastMessage("Un vaisseau ouvre sa soute : visez l'intérieur !", 1);
                }
                break;
            case 62:
                PuffFx(target + Vector3.Up, 2.5f, 1.4f, false);
                Sfx.At("hit", target, 2f, 0.5f, 60);
                break;
            case 70 or 71:
                if (Net.I.IsHost) break; // l'hôte a déjà tiré le projectile autoritaire
                var m = Model.ShipTurretMuzzle(kind - 70);
                Projectile.Spawn(_world, ProjectileKind.Laser, m, (target - m).Normalized() * 90f, false, 0, 0);
                Sfx.At("laser", m, -2f, 0.6f);
                break;
        }
    }

    /// <summary>Mission (monde) où le conseil « visez la soute » a déjà été donné.</summary>
    private static ulong _hatchTold;

    /// <summary>Impact du rayon : étincelles, flammèches et fumée par les émetteurs partagés d'ExplosionFx (aucun nœud créé).</summary>
    private void BeamImpactFx(Vector3 p, Vector3 n)
    {
        var fx = ExplosionFx.For(_world);
        for (var i = 0; i < 5; i++)
        {
            var v = (n + new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(0, 1.2), (float)GD.RandRange(-1, 1))).Normalized() * (float)GD.RandRange(6, 16);
            fx.Spark(p, v, 0.18f, (float)GD.RandRange(0.3, 0.7), new Color(1f, 0.7f, 0.3f));
        }
        fx.FirePuff(p + n * 0.5f, n * 2f, 1.6f, 0.5f, new Color(1f, 0.55f, 0.25f));
        if (GD.Randf() < 0.4f) fx.SmokePuff(p + n, Vector3.Up * 2.5f, 3.5f, 3f, new Color(0.18f, 0.17f, 0.16f));
    }

    /// <summary>Impact de balle sur le blindage : quelques étincelles (émetteurs partagés, feu nourri sans nœud par tir).</summary>
    public void HitSparks(Vector3 at)
    {
        var fx = ExplosionFx.For(_world);
        var n = (at - Center).Normalized();
        for (var i = 0; i < 3; i++)
            fx.Spark(at, (n + new Vector3((float)GD.RandRange(-0.8, 0.8), (float)GD.RandRange(-0.3, 1), (float)GD.RandRange(-0.8, 0.8))).Normalized() * (float)GD.RandRange(4, 10),
                0.1f, (float)GD.RandRange(0.15, 0.35), new Color(1f, 0.8f, 0.45f));
    }

    /// <summary>Bouffée de fumée (bouche du canon, largage) ou de poussière (pas lourd), sans nœud.</summary>
    private void PuffFx(Vector3 p, float size, float life, bool dust)
    {
        var fx = ExplosionFx.For(_world);
        for (var i = 0; i < 4; i++)
        {
            var d = new Vector3((float)GD.RandRange(-1, 1), 0, (float)GD.RandRange(-1, 1));
            if (dust) fx.DustPuff(p + d * size * 0.4f, d * size * 0.8f + Vector3.Up * 0.6f, size, life, new Color(0.52f, 0.49f, 0.44f));
            else fx.SmokePuff(p + d * 0.5f, d + Vector3.Up * 1.5f, size, life, new Color(0.3f, 0.29f, 0.28f));
        }
    }

    /// <summary>Obus du Marcheur (visuel) : trait incandescent du canon jusqu'à l'impact.</summary>
    private void Shell(Vector3 from, Vector3 to)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.28f, Height = 0.56f, RadialSegments = 8, Rings = 4 },
            MaterialOverride = Mats.Glow(new Color(1f, 0.6f, 0.25f), 8f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Scale = new Vector3(1f, 1f, 6f),
        };
        _world.FxRoot.AddChild(mi);
        mi.GlobalPosition = from;
        if ((to - from).LengthSquared() > 1f) mi.LookAt(to, Mathf.Abs((to - from).Normalized().Y) > 0.98f ? Vector3.Right : Vector3.Up);
        var t = Mathf.Max(from.DistanceTo(to) / ShellSpeed, 0.05f);
        var tw = mi.CreateTween();
        tw.TweenProperty(mi, "global_position", to, t);
        tw.TweenCallback(Callable.From(mi.QueueFree));
        Effects.Tracer(_world.FxRoot, from, from.Lerp(to, 0.35f), new Color(1f, 0.55f, 0.25f), 0.18f, 0.12f);
    }

    /// <summary>Un pied se pose : poussière, secousse, grondement ; sur l'hôte, mobilier écrasé et soldat piétiné.</summary>
    private void OnMachineStomp(Vector3 p)
    {
        if (State == EnemyState.Dead) return;
        var tri = Def.Type == EnemyType.Tripod;
        PuffFx(p, tri ? 5f : 3.5f, 2.4f, true);
        _world.ShakeCamera(p, tri ? 0.4f : 0.3f, tri ? 60f : 45f);
        Sfx.At("mech_step", p, tri ? 8f : 5f, tri ? 0.8f : (float)GD.RandRange(0.95, 1.08), 60);
        if (!Net.I.IsHost) return;
        _world.Decor?.HostCrushArea(p, 2.6f, 2.6f, 4);
        foreach (var (peer, at) in TargetsNear(p, 3.2f))
            Net.I.DamagePlayer(peer, Def.Damage * (tri ? 6f : 0.5f) * _world.DamageMul * DamageScale, p);
    }

    // ---------------- Mort ----------------

    /// <summary>Destruction : chapelet d'explosions, effondrement (ou chute du vaisseau), déflagration finale, enfouissement.</summary>
    private void MachineDie()
    {
        Log("détruit");
        MachineDieFx();
    }

    private void MachineDieFx()
    {
        _bT = -1f;
        Model.MachineDeath();
        _sEngine?.Stop();
        HatchOpen = false;
        var rng = new RandomNumberGenerator();
        var tw = CreateTween();
        switch (Def.Type)
        {
            case EnemyType.Walker:
            {
                LandingPoint = new Vector3(GlobalPosition.X, 0, GlobalPosition.Z);
                Sfx.At("collapse", Center, 8f, 0.8f, 0);
                for (var i = 0; i < 5; i++)
                {
                    tw.TweenCallback(Callable.From(() => Effects.Explosion(_world, Model.WalkerTorso * new Vector3(rng.RandfRange(-2.5f, 2.5f), rng.RandfRange(-1.5f, 2f), rng.RandfRange(-2.2f, 2.2f)), rng.RandfRange(3.5f, 6f), 0)));
                    tw.TweenInterval(0.45f);
                }
                tw.TweenCallback(Callable.From(() =>
                {
                    var c = Model.WalkerTorso.Origin;
                    Effects.Explosion(_world, c, 11f, 0);
                    Effects.Dust(_world, new Vector3(c.X, 0, c.Z), new Vector3(18f, 6f, 18f));
                    _world.ShakeCamera(c, 0.8f, 90f);
                }));
                tw.TweenInterval(10f);
                tw.TweenProperty(Model, "position:y", -9f, 4f).SetEase(Tween.EaseType.In);
                break;
            }
            case EnemyType.Tripod:
            {
                LandingPoint = new Vector3(GlobalPosition.X, 0, GlobalPosition.Z);
                Effects.Explosion(_world, Model.TripodCore, 7f, 2);
                Sfx.At("collapse", Center, 10f, 0.7f, 0);
                for (var i = 0; i < 6; i++)
                {
                    tw.TweenCallback(Callable.From(() => Effects.Explosion(_world, Model.TripodHull + Basis * new Vector3(rng.RandfRange(-4.5f, 4.5f), rng.RandfRange(-2f, 2.5f), rng.RandfRange(-4.5f, 4.5f)), rng.RandfRange(4f, 7f), 0)));
                    tw.TweenInterval(0.6f);
                }
                tw.TweenCallback(Callable.From(() =>
                {
                    var c = Model.TripodHull;
                    Effects.Explosion(_world, c, 16f, 0);
                    Effects.Explosion(_world, c + Vector3.Up * 3f, 9f, 2);
                    Effects.Dust(_world, new Vector3(c.X, 0, c.Z), new Vector3(32f, 9f, 32f));
                    _world.ShakeCamera(c, 1f, 130f);
                }));
                tw.TweenInterval(11f);
                tw.TweenProperty(Model, "position:y", -14f, 5f).SetEase(Tween.EaseType.In);
                break;
            }
            default:
            {
                // Le vaisseau pique en flammes et s'écrase devant lui.
                var start = GlobalPosition;
                var ahead = start + new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw)) * Mathf.Clamp(start.Y * 0.5f, 10f, 35f);
                var land = GroundBelow(ahead with { Y = start.Y });
                LandingPoint = land;
                var fall = Mathf.Clamp(Mathf.Sqrt(2f * Mathf.Max(start.Y - land.Y, 1f) / 9f), 1.8f, 4.5f);
                Sfx.At("collapse", start, 12f, 0.75f, 0);
                var par = CreateTween().SetParallel();
                par.TweenProperty(this, "global_position", land + Vector3.Up * 2.5f, fall).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
                par.TweenProperty(this, "rotation", new Vector3(0.32f, _yaw + rng.RandfRange(-0.4f, 0.4f), rng.RandfRange(-0.45f, 0.45f)), fall).SetEase(Tween.EaseType.In);
                var n = Mathf.CeilToInt(fall / 0.35f);
                for (var i = 0; i < n; i++)
                {
                    tw.TweenCallback(Callable.From(() => Effects.Explosion(_world, GlobalTransform * new Vector3(rng.RandfRange(-8f, 8f), rng.RandfRange(-1f, 3f), rng.RandfRange(-18f, 18f)), rng.RandfRange(4f, 7.5f), 0)));
                    tw.TweenInterval(fall / n);
                }
                tw.TweenCallback(Callable.From(() =>
                {
                    var c = GlobalPosition;
                    Effects.Explosion(_world, c, 20f, 0);
                    Effects.Explosion(_world, c + Basis.Z * 14f, 12f, 0);
                    Effects.Explosion(_world, c - Basis.Z * 14f, 12f, 0);
                    Effects.Dust(_world, new Vector3(c.X, land.Y, c.Z), new Vector3(50f, 12f, 50f));
                    _world.ShakeCamera(c, 1.2f, 170f);
                    if (Net.I.IsHost) _world.HostEnemyBlast(c, 16f, Def.Damage * 12f * _world.DamageMul);
                }));
                tw.TweenInterval(2f);
                tw.TweenCallback(Callable.From(() => Effects.Smoke(_world, GlobalPosition + Vector3.Up * 3f, 9f, 8f)));
                tw.TweenInterval(12f);
                tw.TweenProperty(Model, "position:y", -12f, 5f).SetEase(Tween.EaseType.In);
                break;
            }
        }
        tw.TweenCallback(Callable.From(QueueFree));
    }

    // ---------------- Caméra ----------------

    /// <summary>Caméra dans le volume de la machine : le modèle devient translucide.</summary>
    private void MachineCameraFade(Vector3 cam)
    {
        var a = GlobalTransform.AffineInverse() * cam;
        var fade = State != EnemyState.Dead && Def.Type switch
        {
            EnemyType.Walker => Mathf.Abs(a.X) < 4.6f && a.Y > 6.5f && a.Y < 14f && Mathf.Abs(a.Z) < 4f,
            EnemyType.Tripod => Mathf.Abs(a.X) < 6f && Mathf.Abs(a.Y - BugModel.TBodyY) < 3.6f && Mathf.Abs(a.Z) < 6.5f,
            _ => Mathf.Abs(a.X) < 10f && a.Y > -4f && a.Y < 5f && Mathf.Abs(a.Z) < 22f,
        };
        if (fade == _camFaded) return;
        _camFaded = fade;
        Model.SetFade(fade ? 0.7f : 0f);
    }
}
