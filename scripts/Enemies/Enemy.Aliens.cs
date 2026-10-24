using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Aliens (spécification §10.1, voir docs/ARCHITECTURE.md, « Aliens ») : fantassin qui se met à couvert et tire en
/// rafales, garde au bouclier frontal qui charge, volant à réacteurs qui harcèle d'en haut, et portail qui fait passer
/// des groupes d'aliens et de machines jusqu'à sa destruction. IA sur l'hôte ; effets rejoués chez chaque pair par
/// <see cref="AlienFx"/> (RPC EnemyAttack existante, genres 80 à 89) ; posture (accroupi, visée, charge, bouclier)
/// transmise dans les 4 bits hauts de l'octet d'état de l'instantané ; formes de collision recalées sur l'armature animée.
/// </summary>
public partial class Enemy
{
    public bool IsAlien => Def.Type is EnemyType.AlienTrooper or EnemyType.AlienGuard or EnemyType.AlienFlyer;
    public bool IsPortal => Def.Type == EnemyType.Portal;
    /// <summary>Alien humanoïde ou portail (chemins propres dans Enemy.cs et World.cs).</summary>
    public bool IsAlienKind => IsAlien || IsPortal;
    private bool IsGuardAlien => Def.Type == EnemyType.AlienGuard;
    private bool IsFlyerAlien => Def.Type == EnemyType.AlienFlyer;

    /// <summary>Sang des aliens : ichor violet sombre.</summary>
    private static readonly Color AlienBlood = new(0.22f, 0.1f, 0.3f);

    /// <summary>Vitesse de marche sur place d'un alien figé (vitrine, --aliens avec --freeze).</summary>
    public float FrozenSpeed;

    private readonly List<(CollisionShape3D Shape, Node3D Node, Transform3D Off)> _aSync = new();
    private AlienShield _aShieldBody;
    private bool _aShieldOn = true;
    private float _aGroundY, _aGroundT = -1f;
    private AudioStreamPlayer3D _aHum;

    /// <summary>Journal des aliens (tests : --aliens ou --fps).</summary>
    private static readonly bool AlienLog = Screenshot.CmdArg("aliens") != null || Screenshot.CmdArg("fps") != null;

    private void ALog(string msg)
    {
        if (AlienLog) GD.Print($"[alien] {Def.Type} n°{Id} t={_world.Elapsed:0.0} : {msg}");
    }

    // ---------------- Création ----------------

    /// <summary>
    /// Formes de collision. Humanoïdes : 0 = torse, 1 = point faible (tête ; cellule dorsale du garde), puis bassin,
    /// cuisses, tibias (et tête du garde) ; le bouclier du garde est un corps à part (<see cref="AlienShield"/>).
    /// Portail : 0 = anneau et membrane, 1 = socle, 2-3 = piliers.
    /// </summary>
    private void CreateAlienShapes()
    {
        State = EnemyState.Moving;
        var shapes = new List<CollisionShape3D>();
        void Add(Shape3D s, Node3D node, Transform3D off)
        {
            var c = new CollisionShape3D { Shape = s, Transform = off };
            AddChild(c);
            shapes.Add(c);
            if (node != null) _aSync.Add((c, node, off));
        }
        if (IsPortal)
        {
            Add(new CylinderShape3D { Radius = BugModel.PortalRingR + 1.7f, Height = 2.3f }, null,
                new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2), new Vector3(0, BugModel.PortalRingY, 0)));
            Add(new BoxShape3D { Size = new Vector3(15f, 1.6f, 13f) }, null, new Transform3D(Basis.Identity, new Vector3(0, 0.6f, 0)));
            for (var s = -1; s <= 1; s += 2)
                Add(new BoxShape3D { Size = new Vector3(2f, 6f, 2.6f) }, null, new Transform3D(Basis.Identity, new Vector3(s * 7f, 4f, 0)));
            _mShapes = shapes.ToArray();
            return;
        }
        var d = Model.AlienDimensions;
        var L = d.L;
        var W = d.W;
        Transform3D T(float x, float y, float z) => new(Basis.Identity, new Vector3(x, y, z));
        Add(new CapsuleShape3D { Radius = 0.6f * L * W, Height = 1.55f * L }, Model.AlienChestNode, T(0, 0.45f * L, -0.02f * L));
        if (IsGuardAlien)
        {
            Add(new BoxShape3D { Size = new Vector3(0.6f, 0.75f, 0.42f) * L }, Model.AlienCoreNode, T(0, 0, -0.04f * L));
            Add(new SphereShape3D { Radius = 0.32f * L }, Model.AlienHeadNode, T(0, 0.34f * L, 0.03f * L));
        }
        else Add(new SphereShape3D { Radius = 0.3f * L }, Model.AlienHeadNode, T(0, 0.36f * L, 0.04f * L));
        Add(new CapsuleShape3D { Radius = 0.42f * L * W, Height = 1.1f * L }, Model.AlienHipsNode, T(0, 0.1f * L, 0));
        for (var i = 0; i < 2; i++)
        {
            Add(new CapsuleShape3D { Radius = 0.25f * L * W, Height = d.Thigh + 0.25f * L }, Model.AlienThighNode(i), T(0, -d.Thigh * 0.5f, 0.03f * L));
            Add(new CapsuleShape3D { Radius = 0.19f * L * W, Height = d.Shin + 0.15f * L }, Model.AlienShinNode(i), T(0, -d.Shin * 0.5f, 0.04f * L));
        }
        _mShapes = shapes.ToArray();
        if (IsGuardAlien)
        {
            _aShieldBody = new AlienShield { Bearer = this, CollisionLayer = 1 << 2, CollisionMask = 0, Name = "Shield" };
            _aShieldBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.85f, 3.15f, 0.45f) * L }, Position = new Vector3(0, 0, -0.08f * L) });
            AddChild(_aShieldBody);
        }
        SyncAlienShapes();
    }

    /// <summary>Apparition : les aliens se matérialisent (éclair violet, étincelles, son de téléportation) ; le portail bourdonne.</summary>
    private void AlienReady()
    {
        if (IsPortal)
        {
            // Le portail fait face au centre de la carte (même cap chez tous les pairs, déduit de la position).
            var p = GlobalPosition;
            if (new Vector2(p.X, p.Z).LengthSquared() > 1f) _yaw = Mathf.Atan2(-p.X, -p.Z);
            Rotation = new Vector3(0, _yaw, 0);
            _netRot = Quaternion;
            _netPos = GlobalPosition;
            _aHum = new AudioStreamPlayer3D { Bus = "Sfx", MaxDistance = 200f, UnitSize = 28f, VolumeDb = -2f, Position = Vector3.Up * BugModel.PortalRingY };
            AddChild(_aHum);
            return;
        }
        if (_falling || Frozen) return;
        var c = GlobalPosition + Vector3.Up * 2.5f;
        var fx = ExplosionFx.For(_world);
        fx.Flash(c, BugModel.AlienEnergy, 6f, 18f, 0.45f);
        for (var i = 0; i < 14; i++)
            fx.Spark(c + new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(-2, 2), (float)GD.RandRange(-1, 1)),
                new Vector3((float)GD.RandRange(-3, 3), (float)GD.RandRange(2, 9), (float)GD.RandRange(-3, 3)), 0.12f, (float)GD.RandRange(0.3, 0.7), BugModel.AlienEnergy);
        Sfx.At("alien_warp", GlobalPosition, 2f, (float)GD.RandRange(0.9, 1.1), 150);
        // Matérialisation : le corps se déploie verticalement en 0,4 s.
        var s = Model.Scale;
        Model.Scale = new Vector3(s.X * 0.6f, s.Y * 0.03f, s.Z * 0.6f);
        Model.CreateTween().TweenProperty(Model, "scale", s, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    private void SyncAlienShapes()
    {
        var mx = Model.Transform;
        foreach (var (shape, node, off) in _aSync) shape.Transform = Model.AlienNodeXf(node, mx) * off;
        if (_aShieldBody != null && Model.AlienShieldNode != null)
        {
            _aShieldBody.Transform = Model.AlienNodeXf(Model.AlienShieldNode, mx);
            var on = State != EnemyState.Dead && !Model.AlienShieldDown;
            if (on != _aShieldOn) _aShieldBody.CollisionLayer = on ? 1u << 2 : 0u;
            _aShieldOn = on;
        }
    }

    /// <summary>Centre du corps : haut du torse (suit l'accroupissement) ; centre de l'anneau du portail.</summary>
    private Vector3 AlienCenter
    {
        get
        {
            if (IsPortal) return GlobalPosition + Vector3.Up * BugModel.PortalRingY;
            if (!IsInsideTree()) return GlobalPosition + Vector3.Up * Model.AlienChestHeight;
            return ToGlobal(Model.AlienNodeXf(Model.AlienChestNode) * new Vector3(0, 0.4f * Model.AlienDimensions.L, 0));
        }
    }

    // ---------------- Points faibles et dégâts (hôte) ----------------

    /// <summary>
    /// Dégâts réellement subis (hôte ; le tireur a déjà compté ×1,5 sur la forme 1) : tête du fantassin et du volant ×2,
    /// cellule dorsale du garde ×3 ; explosion devant un garde au bouclier levé ×0,4 ; portail : balles ×0,5, explosions ×1,5.
    /// </summary>
    public float AlienAdjustDamage(float dmg, bool weak)
    {
        switch (Def.Type)
        {
            case EnemyType.Portal:
                return BlastOrigin != null ? dmg * 1.5f : dmg * 0.5f;
            case EnemyType.AlienGuard:
                if (BlastOrigin is { } b && !Model.AlienShieldDown)
                {
                    var to = (b - GlobalPosition) with { Y = 0 };
                    if (to.LengthSquared() > 0.01f && to.Normalized().Dot(GlobalBasis.Z) > 0.4f) return dmg * 0.4f;
                }
                return weak ? dmg * 3f / 1.5f : dmg;
            default:
                return weak ? dmg * 2f / 1.5f : dmg;
        }
    }

    /// <summary>Impact sur un alien (tous les pairs) : giclée d'ichor violet ; étincelles sur la cellule du garde ; sursaut.</summary>
    public void AlienHitFx(Vector3 at, bool weak)
    {
        if (IsPortal) { HitSparks(at); return; }
        Effects.Goo(_world, at, AlienBlood, 0.55f, 5);
        if (weak && IsGuardAlien)
        {
            var fx = ExplosionFx.For(_world);
            for (var i = 0; i < 4; i++)
                fx.Spark(at, new Vector3((float)GD.RandRange(-4, 4), (float)GD.RandRange(0, 6), (float)GD.RandRange(-4, 4)), 0.1f, 0.3f, BugModel.AlienEnergy);
        }
    }

    // ---------------- IA (hôte) ----------------

    private enum AState : byte { Advance, ToCover, Cover, Peek, Fire, Open, Approach, Charge, Recover }

    private AState _aState;
    private float _aStateT, _aSearchCd, _aHold = 2f, _aBashCd, _aCrushT;
    private Vector3 _aCover, _aPeek;
    private int _aPeeks;
    // Rafale en cours : tirs restants, intervalle, cible.
    private int _aBurstLeft, _aBurstKind;
    private float _aBurstT, _aBurstGap;
    private long _aBurstPeer;
    private Ally _aBurstAlly;
    private Guard _aBurstGuard;
    private Vector3 _aBurstAim;
    // Volant : passe de mitraillage.
    private bool _fRun;
    private float _fRunT, _fNextRun = 8f, _fGroundTop, _fGroundT, _fClimbT, _fAvoidT, _fRunShotT;
    private Vector3 _fRunEnd;
    private float _pSpawnT = 4f;
    private int _pGroups;
    private readonly List<Enemy> _pChildren = new();

    /// <summary>État de l'IA (journal des tests).</summary>
    public string AlienStateName => IsPortal ? "portail" : IsFlyerAlien ? (_fRun ? "passe" : "orbite") : _aState.ToString();

    private void SetA(AState s)
    {
        if (_aState == s) return;
        _aState = s;
        _aStateT = 0f;
    }

    private void HostTickAlien(float dt)
    {
        if (IsPortal)
        {
            if (!Frozen) HostTickPortal(dt);
            AnimateModel(dt, 0, 0);
            return;
        }
        if (Frozen)
        {
            if (_world.NearestPlayer(GlobalPosition, out _, out var ft)) Model.AlienAim(ft + Vector3.Up * 1.4f);
            _speedNow = FrozenSpeed;
            Model.AlienSpeedOverride = FrozenSpeed > 0f ? FrozenSpeed : -1f;
            AnimateModel(dt, FrozenSpeed, 0);
            return;
        }
        _attackCd -= dt;
        _specialCd -= dt;
        _aBashCd -= dt;
        _attackAnim = Mathf.Max(0, _attackAnim - dt * 3f);
        TickBurst(dt);
        if (_falling)
        {
            AlienFall(dt);
            AnimateModel(dt, 0, 0);
            return;
        }
        if (HostStuck(dt)) { _world.HostRespawnStuck(this); return; }
        if (!_world.NearestPlayer(GlobalPosition, out var peer, out var target))
        {
            Model.AlienAiming = 0f;
            Model.AlienCharging = false;
            if (IsFlyerAlien) TickFlyerAlien(dt, GlobalPosition, 0, null);
            else AlienStep(Vector3.Zero, 0, Vector3.Zero, 2f, dt);
            AnimateModel(dt, _speedNow, _attackAnim);
            return;
        }
        // Mêmes règles de cible que les insectes : allié nettement plus proche, ou élément de l'objectif.
        Ally ally = null;
        if (!IgnoreAllies)
        {
            var a = _world.Allies?.Nearest(GlobalPosition, 45f);
            if (a != null && a.GlobalPosition.DistanceTo(GlobalPosition) < target.DistanceTo(GlobalPosition) * 0.85f)
            {
                ally = a;
                target = a.GlobalPosition;
            }
        }
        _guard = null;
        if (_world.Objective != null && _world.Objective.HostPickTarget(this, target, out var guardPoint, out var guard))
        {
            _guard = guard;
            ally = null;
            target = guardPoint;
        }
        Model.AlienAim(target + Vector3.Up * 1.3f);
        switch (Def.Type)
        {
            case EnemyType.AlienTrooper: TickTrooper(dt, peer, target, ally); break;
            case EnemyType.AlienGuard: TickGuardAlien(dt, peer, target, ally); break;
            default: TickFlyerAlien(dt, target, peer, ally); break;
        }
        AnimateModel(dt, _speedNow, _attackAnim);
        State = _attackAnim > 0.05f || _aBurstLeft > 0 || _aState == AState.Charge ? EnemyState.Attacking : EnemyState.Moving;
    }

    /// <summary>
    /// Déplacement au sol : direction voulue + séparation, vitesse, immeubles infranchissables (glisse le long), hauteur du
    /// sol suivie par un rayon tous les 0,25 s (marches, ponts, rampes), corps tourné vers <paramref name="face"/> à vitesse bornée.
    /// </summary>
    private void AlienStep(Vector3 want, float speed, Vector3 face, float turnRate, float dt)
    {
        var pos = GlobalPosition;
        var sep = _world.Separation(this, 2.2f * Def.Size);
        sep.Y = 0;
        var move = want * (speed > 0 ? 1f : 0f) + sep * (speed > 0 ? 1.4f : 0.6f);
        if (move.LengthSquared() > 1) move = move.Normalized();
        _moveDir = _moveDir.Lerp(move, Mathf.Clamp(dt * 5f, 0, 1));
        var v = _moveDir * Mathf.Max(speed, 1.5f);
        var next = pos + v * dt;
        if (_world.Flow.IsBlocked(next) && !_world.Flow.IsBlocked(pos))
        {
            // Glisse le long de la façade.
            var nx = pos + new Vector3(v.X, 0, 0) * dt;
            var nz = pos + new Vector3(0, 0, v.Z) * dt;
            next = !_world.Flow.IsBlocked(nx) ? nx : !_world.Flow.IsBlocked(nz) ? nz : pos;
        }
        _aGroundT -= dt;
        if (_aGroundT <= 0)
        {
            _aGroundT = 0.25f;
            _aGroundY = Ray(next + Vector3.Up * 2.2f, next + Vector3.Down * 6f, out var gp, out var gn) && gn.Y > 0.5f ? gp.Y : _world.GroundHeight(next);
        }
        next.Y = Mathf.MoveToward(pos.Y, _aGroundY, dt * 7f);
        GlobalPosition = next;
        _speedNow = new Vector2(next.X - pos.X, next.Z - pos.Z).Length() / Mathf.Max(dt, 1e-4f);
        if (face.LengthSquared() > 0.01f)
        {
            var ty = Mathf.Atan2(face.X, face.Z);
            _yaw += Mathf.Clamp(Mathf.Wrap(ty - _yaw, -Mathf.Pi, Mathf.Pi), -turnRate * dt, turnRate * dt);
            Rotation = new Vector3(0, _yaw, 0);
        }
        _fwd = new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw));
    }

    /// <summary>Direction du champ de flux (contourne les immeubles), ou la direction directe à défaut.</summary>
    private Vector3 FlowOr(Vector3 pos, Vector3 fallback)
    {
        var d = _world.Flow.Direction(pos);
        return d == Vector3.Zero ? fallback : d;
    }

    /// <summary>Largage par un vaisseau : chute jusqu'au sol, réception lourde.</summary>
    private void AlienFall(float dt)
    {
        _fallV += 22f * dt;
        var pos = GlobalPosition;
        var next = pos + Vector3.Down * _fallV * dt;
        if (Ray(pos + Vector3.Up, next + Vector3.Down * 0.2f, out var hit, out _) || next.Y <= 0f)
        {
            next.Y = Mathf.Max(hit.Y, 0f);
            _falling = false;
            _fallV = 0;
            _aGroundY = next.Y;
            AlienFx(86, next);
            Net.I.BroadcastEnemyAttack(Id, 86, next);
        }
        GlobalPosition = next;
        _speedNow = 0;
    }

    // ---- Fantassin : abri, créneau, rafales ----

    private void TickTrooper(float dt, long peer, Vector3 target, Ally ally)
    {
        var pos = GlobalPosition;
        var to = (target - pos) with { Y = 0 };
        var dist = to.Length();
        var dirT = dist > 0.01f ? to / dist : _fwd;
        _aStateT += dt;
        _aSearchCd -= dt;
        var crouch = 0f;
        var aiming = 0f;
        switch (_aState)
        {
            case AState.ToCover:
            {
                var tc = (_aCover - pos) with { Y = 0 };
                var dc = tc.Length();
                if (dc < 1.1f || _aStateT > 9f)
                {
                    SetA(dc < 3f ? AState.Cover : AState.Advance);
                    break;
                }
                // Court s'il est loin de l'abri ; face au chemin, ou à la cible sur les derniers mètres (pas de côté).
                var dir = tc / dc;
                AlienStep(dir, Def.Speed * (dc > 10f ? 1.5f : 1f), dc < 6f ? dirT : dir, 3.5f, dt);
                aiming = dc < 6f ? 0.6f : 0f;
                break;
            }
            case AState.Cover:
                crouch = 1f;
                AlienStep(Vector3.Zero, 0, dirT, 2.5f, dt);
                if (dist < 14f) { SetA(AState.Open); break; }
                if (_aStateT > _aHold)
                {
                    if (!CoverStillValid(target)) { SetA(AState.Advance); _aSearchCd = 0f; break; }
                    SetA(AState.Peek);
                }
                break;
            case AState.Peek:
            {
                aiming = 1f;
                var tp = (_aPeek - pos) with { Y = 0 };
                var dp = tp.Length();
                if (dp > 0.8f && _aStateT < 3f) AlienStep(tp / dp, Def.Speed, dirT, 3f, dt);
                else
                {
                    AlienStep(Vector3.Zero, 0, dirT, 3f, dt);
                    if (_aStateT > 0.5f && _aBurstLeft <= 0)
                    {
                        StartBurst(peer, target, ally, 80, 4, 0.19f);
                        SetA(AState.Fire);
                    }
                }
                break;
            }
            case AState.Fire:
                aiming = 1f;
                AlienStep(Vector3.Zero, 0, dirT, 3f, dt);
                if (_aBurstLeft <= 0 && _aStateT > 0.4f)
                {
                    _aPeeks++;
                    _aHold = (float)GD.RandRange(1.3, 3.0);
                    if (_aPeeks >= 3 && GD.Randf() < 0.55f) { _aPeeks = 0; SetA(AState.Advance); _aSearchCd = 0f; }
                    else SetA(AState.ToCover);
                }
                break;
            case AState.Open:
                // À découvert (aucun abri, ou cible trop proche) : debout, vise et tire ; avance si la cible s'éloigne.
                aiming = 1f;
                if (dist > 48f) AlienStep(FlowOr(pos, dirT), Def.Speed * 0.7f, dirT, 3f, dt);
                else if (dist < 10f) AlienStep(-dirT, Def.Speed * 0.6f, dirT, 3f, dt);
                else AlienStep(Vector3.Zero, 0, dirT, 3f, dt);
                if (_attackCd <= 0 && dist < Def.AttackRange && _aBurstLeft <= 0)
                {
                    _attackCd = Def.AttackCooldown * (float)GD.RandRange(0.8, 1.2);
                    StartBurst(peer, target, ally, 80, 4, 0.19f);
                }
                if (_aSearchCd <= 0 && _aBurstLeft <= 0)
                {
                    _aSearchCd = 2.5f;
                    if (dist > 16f && FindCover(target, out _aCover, out _aPeek)) SetA(AState.ToCover);
                    else if (dist > 75f) SetA(AState.Advance);
                }
                break;
            default: // Advance
            {
                if (_aSearchCd <= 0 && dist < 95f)
                {
                    _aSearchCd = (float)GD.RandRange(1.4, 2.2);
                    if (FindCover(target, out _aCover, out _aPeek))
                    {
                        ALog($"abri trouvé à {_aCover.Snapped(Vector3.One)} (créneau {_aPeek.Snapped(Vector3.One)}), cible à {dist:0} m");
                        SetA(AState.ToCover);
                        break;
                    }
                    if (dist < 62f) { SetA(AState.Open); break; }
                }
                var dir = dist > 60f ? FlowOr(pos, dirT) : dirT;
                AlienStep(dir, Def.Speed, dir, 3.5f, dt);
                // Tir en marchant quand la cible est à portée.
                if (dist < Def.AttackRange && _attackCd <= 0 && _aBurstLeft <= 0)
                {
                    _attackCd = Def.AttackCooldown * (float)GD.RandRange(1.1, 1.5);
                    StartBurst(peer, target, ally, 80, 3, 0.22f);
                }
                aiming = _aBurstLeft > 0 ? 1f : 0f;
                break;
            }
        }
        Model.AlienCrouch = crouch;
        Model.AlienAiming = _aBurstLeft > 0 ? 1f : aiming;
    }

    /// <summary>
    /// Cherche un abri (hôte) : points tirés autour de l'alien, entre 16 et 70 m de la cible ; un obstacle du décor doit
    /// couper la ligne entre la cible et le torse accroupi, tout près de l'alien (immeuble, mur, conteneur, camion) ; et un
    /// créneau doit exister : debout sur place, ou en se décalant de 3,5 à 5,5 m sur le côté, la ligne de tir est dégagée.
    /// </summary>
    private bool FindCover(Vector3 target, out Vector3 cover, out Vector3 peek)
    {
        cover = peek = Vector3.Zero;
        var L = Model.AlienDimensions.L;
        var pos = GlobalPosition;
        var eye = target + Vector3.Up * 1.6f;
        var flatT = target with { Y = 0 };
        var toT = flatT - pos with { Y = 0 };
        var dist = toT.Length();
        var dirT = dist > 0.01f ? toT / dist : _fwd;
        var best = float.MaxValue;
        var rng = _world.Rng;
        for (var k = 0; k < 14; k++)
        {
            var a = rng.Randf() * Mathf.Tau;
            var r = rng.RandfRange(3f, 30f);
            var c = pos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r + dirT * Mathf.Min(10f, dist * 0.15f);
            var dT = new Vector2(c.X - flatT.X, c.Z - flatT.Z).Length();
            if (dT < 16f || dT > 70f || _world.Flow.IsBlocked(c)) continue;
            c.Y = Ray(c + Vector3.Up * 3f, c + Vector3.Down * 6f, out var gp, out var gn) && gn.Y > 0.5f ? gp.Y : _world.GroundHeight(c);
            var chest = c + Vector3.Up * 2.5f * L;
            if (!Ray(eye, chest, out var hp, out _) || hp.DistanceTo(chest) > 8f) continue;
            var side = ((flatT - c) with { Y = 0 }).Normalized().Cross(Vector3.Up);
            Vector3? pk = null;
            if (!Ray(c + Vector3.Up * 4.1f * L, eye, out _, out _)) pk = c;
            else
                foreach (var off in new[] { 3.5f, -3.5f, 5.5f, -5.5f })
                {
                    var q = c + side * off;
                    if (_world.Flow.IsBlocked(q)) continue;
                    if (!Ray(q + Vector3.Up * 3.8f * L, eye, out _, out _)) { pk = q; break; }
                }
            if (pk == null) continue;
            var score = c.DistanceTo(pos) + Mathf.Abs(dT - 38f) * 0.6f + rng.Randf() * 3f;
            if (score >= best) continue;
            best = score;
            cover = c;
            peek = pk.Value;
        }
        return best < float.MaxValue;
    }

    /// <summary>L'abri cache-t-il encore le torse accroupi (la cible a pu le contourner) ?</summary>
    private bool CoverStillValid(Vector3 target)
    {
        var chest = GlobalPosition + Vector3.Up * 2.5f * Model.AlienDimensions.L;
        return Ray(target + Vector3.Up * 1.6f, chest, out var hp, out _) && hp.DistanceTo(chest) < 7f;
    }

    // ---- Rafales (fantassin, volant, blaster du garde) ----

    private void StartBurst(long peer, Vector3 target, Ally ally, int kind, int shots, float gap)
    {
        _aBurstPeer = peer;
        _aBurstAlly = ally;
        _aBurstGuard = _guard;
        _aBurstAim = target + Vector3.Up * 1.1f;
        _aBurstKind = kind;
        _aBurstLeft = shots;
        _aBurstGap = gap;
        _aBurstT = kind == 83 ? 0.35f : 0.25f; // le temps d'épauler
    }

    private void TickBurst(float dt)
    {
        if (_aBurstLeft <= 0) return;
        _aBurstT -= dt;
        if (_aBurstT > 0) return;
        _aBurstT = _aBurstGap;
        _aBurstLeft--;
        // La cible bouge pendant la rafale : on suit le soldat.
        if (_aBurstAlly != null && IsInstanceValid(_aBurstAlly) && !_aBurstAlly.Dead) _aBurstAim = _aBurstAlly.GlobalPosition + Vector3.Up * 1.1f;
        else if (_aBurstGuard == null && _world.PlayerPosition(_aBurstPeer, out var pp)) _aBurstAim = pp + Vector3.Up * 1.1f;
        FireAlienBolt(_aBurstAim, _aBurstKind, _aBurstAlly, _aBurstGuard);
    }

    /// <summary>Un trait d'énergie (hôte) : projectile autoritaire vers un soldat, ou dégâts directs sur un allié ou l'objectif.</summary>
    private void FireAlienBolt(Vector3 aim, int kind, Ally ally, Guard guard)
    {
        if (State == EnemyState.Dead) return;
        var from = Model.AlienMuzzle;
        var dist = from.DistanceTo(aim);
        var spread = dist * (kind == 84 ? 0.03f : kind == 83 ? 0.018f : 0.024f);
        var rng = _world.Rng;
        var a = aim + new Vector3(rng.RandfRange(-spread, spread), rng.RandfRange(-spread, spread) * 0.6f, rng.RandfRange(-spread, spread));
        var dmg = Def.Damage * (kind == 83 ? 0.5f : 1f) * _world.DamageMul * DamageScale;
        var dir = (a - from).Normalized();
        if (guard != null || ally != null)
        {
            if (guard != null) _world.Objective.HostDamage(guard, dmg * 0.6f);
            else _world.Allies.HostDamage(ally, dmg * 0.6f);
            Projectile.Spawn(_world, kind == 83 ? ProjectileKind.HeavyBolt : ProjectileKind.Bolt, from, dir * BoltSpeed(kind), false, 0, 0);
        }
        else
        {
            var p = Projectile.Spawn(_world, kind == 83 ? ProjectileKind.HeavyBolt : ProjectileKind.Bolt, from, dir * BoltSpeed(kind), true, dmg, kind == 83 ? 2.4f : 0.9f);
            p.SourceEnemy = this;
        }
        AlienFx(kind, a);
        Net.I.BroadcastEnemyAttack(Id, kind, a);
    }

    private static float BoltSpeed(int kind) => kind == 84 ? 115f : kind == 83 ? 62f : 85f;

    // ---- Garde : approche bouclier levé, charge, coup de bouclier ----

    private void TickGuardAlien(float dt, long peer, Vector3 target, Ally ally)
    {
        var pos = GlobalPosition;
        var to = (target - pos) with { Y = 0 };
        var dist = to.Length();
        var dirT = dist > 0.01f ? to / dist : _fwd;
        _aStateT += dt;
        Model.AlienCharging = false;
        Model.AlienShieldDown = false;
        var aiming = 0.4f;
        switch (_aState)
        {
            case AState.Charge:
            {
                if (_aStateT < 0.55f)
                {
                    // Élan : rugissement, bouclier en avant.
                    AlienStep(Vector3.Zero, 0, dirT, 2.5f, dt);
                    Model.AlienCharging = true;
                    break;
                }
                Model.AlienCharging = true;
                AlienStep(_fwd.Lerp(dirT, 0.5f).Normalized(), 11.5f, dirT, 1.0f, dt);
                _aCrushT -= dt;
                if (_aCrushT <= 0)
                {
                    _aCrushT = 0.1f;
                    _world.Decor?.HostCrushArea(GlobalPosition + _fwd * 1.8f, 1.6f, 1.6f, 3);
                }
                if (dist < Def.AttackRange + 0.8f)
                {
                    GuardBash(peer, target, ally);
                    SetA(AState.Recover);
                }
                else if (_aStateT > 3.4f || (_aStateT > 1.2f && _speedNow < 2f)) SetA(AState.Recover);
                break;
            }
            case AState.Recover:
                // Bouclier abaissé, souffle : le garde est vulnérable de face.
                Model.AlienShieldDown = true;
                aiming = 0f;
                AlienStep(Vector3.Zero, 0, dirT, 0.8f, dt);
                if (_aStateT > 1.9f) SetA(AState.Approach);
                break;
            default:
            {
                if (_aState != AState.Approach) SetA(AState.Approach);
                var dir = dist > 40f ? FlowOr(pos, dirT) : dirT;
                if (dist > Def.AttackRange * 0.75f) AlienStep(dir, Def.Speed, dist > 40f ? dir : dirT, 1.5f, dt);
                else AlienStep(Vector3.Zero, 0, dirT, 1.5f, dt);
                if (_attackCd <= 0 && dist > 10f && dist < 48f && _aBurstLeft <= 0)
                {
                    _attackCd = (float)GD.RandRange(3.0, 4.2);
                    StartBurst(peer, target, ally, 83, 1, 0.1f);
                }
                if (_specialCd <= 0 && dist > 9f && dist < 30f && _aBurstLeft <= 0)
                {
                    _specialCd = (float)GD.RandRange(7, 10);
                    SetA(AState.Charge);
                    ALog($"charge sur une cible à {dist:0} m");
                    AlienFx(81, target);
                    Net.I.BroadcastEnemyAttack(Id, 81, target);
                }
                else if (dist < Def.AttackRange && _aBashCd <= 0)
                {
                    GuardBash(peer, target, ally);
                    _aBashCd = Def.AttackCooldown;
                    // Après chaque coup, le bouclier retombe un instant : la fenêtre pour frapper de face.
                    SetA(AState.Recover);
                }
                if (_aBurstLeft > 0) aiming = 1f;
                break;
            }
        }
        Model.AlienAiming = aiming;
        Model.AlienCrouch = 0f;
    }

    /// <summary>Coup de bouclier sur la cible (soldat, allié ou objectif).</summary>
    private void GuardBash(long peer, Vector3 target, Ally ally)
    {
        var dmg = Def.Damage * _world.DamageMul * DamageScale;
        if (_guard != null) _world.Objective.HostDamage(_guard, dmg);
        else if (ally != null) _world.Allies.HostDamage(ally, dmg);
        else Net.I.DamagePlayer(peer, dmg, GlobalPosition);
        _aBashCd = Def.AttackCooldown;
        _attackAnim = 1f;
        var at = GlobalPosition + _fwd * 2.5f + Vector3.Up * 1.5f;
        ALog("coup de bouclier");
        AlienFx(82, at);
        Net.I.BroadcastEnemyAttack(Id, 82, at);
    }

    // ---- Volant : harcèlement en hauteur, passes de mitraillage ----

    private void TickFlyerAlien(float dt, Vector3 target, long peer, Ally ally)
    {
        var pos = GlobalPosition;
        _fNextRun -= dt;
        _fClimbT -= dt;
        // Toits sous le volant et devant lui (il passe au-dessus des immeubles).
        _fGroundT -= dt;
        if (_fGroundT <= 0)
        {
            _fGroundT = 0.3f;
            var ahead = pos + _velocity * 0.8f;
            var top = 0f;
            foreach (var p in new[] { pos, ahead })
                if (Ray(new Vector3(p.X, Mathf.Max(p.Y, 0f) + 60f, p.Z), new Vector3(p.X, -2f, p.Z), out var hp, out _)) top = Mathf.Max(top, hp.Y);
            _fGroundTop = top;
        }
        var flat = (target - pos) with { Y = 0 };
        var dist = flat.Length();
        var dirT = dist > 0.01f ? flat / dist : _fwd;
        Vector3 goal;
        float speed;
        if (_fRun)
        {
            _fRunT += dt;
            goal = _fRunEnd;
            speed = 18f;
            var left = (_fRunEnd - pos) with { Y = 0 };
            // Mitraille en rapprochement.
            _fRunShotT -= dt;
            if (_fRunShotT <= 0 && dist < 55f && _velocity.Dot(target - pos) > 0)
            {
                _fRunShotT = 0.22f;
                FireAlienBolt(target + Vector3.Up * 1.1f, 84, ally, _guard);
            }
            if (left.Length() < 6f || _fRunT > 7f) { _fRun = false; _fNextRun = (float)GD.RandRange(8, 13); }
        }
        else
        {
            _orbit += dt * 0.32f;
            goal = target + new Vector3(Mathf.Cos(_orbit), 0, Mathf.Sin(_orbit)) * 32f;
            speed = Def.Speed;
            if (_attackCd <= 0 && dist < Def.AttackRange && _aBurstLeft <= 0 && peer != 0)
            {
                _attackCd = Def.AttackCooldown * (float)GD.RandRange(0.8, 1.3);
                StartBurst(peer, target, ally, 84, 3, 0.13f);
            }
            if (_fNextRun <= 0 && dist < 90f && peer != 0)
            {
                // Passe : à travers la cible, plus bas, jusqu'à 40 m au-delà.
                _fRun = true;
                _fRunT = 0;
                var through = dist > 1f ? dirT : _fwd;
                _fRunEnd = target + through * 40f;
                ALog("passe de mitraillage");
            }
        }
        var alt = _fRun ? 11f : 14f + _altitude * 0.5f;
        var wantY = Mathf.Max(_fGroundTop + 7f, target.Y + alt);
        if (_fClimbT > 0) wantY = Mathf.Max(wantY, pos.Y + 8f);
        var toGoal = (goal - pos) with { Y = 0 };
        var desired = toGoal.LengthSquared() > 1f ? toGoal.Normalized() * speed : Vector3.Zero;
        desired.Y = Mathf.Clamp((wantY - pos.Y) * 1.2f, -8f, 10f);
        // Évitement : immeuble devant, à hauteur de vol → monte et s'écarte.
        _fAvoidT -= dt;
        if (_fAvoidT <= 0)
        {
            _fAvoidT = 0.15f;
            var hv = _velocity with { Y = 0 };
            if (hv.LengthSquared() > 1f && Ray(pos, pos + hv.Normalized() * 14f, out _, out var n))
            {
                _fClimbT = 1.2f;
                _velocity += new Vector3(n.X, 0, n.Z) * 6f;
            }
        }
        var sep = _world.Separation(this, 4f);
        desired += sep * 6f;
        _velocity = _velocity.MoveToward(desired, dt * 16f);
        var next = pos + _velocity * dt;
        next.Y = Mathf.Max(next.Y, _fGroundTop + 3.5f);
        GlobalPosition = next;
        _speedNow = _velocity.Length();
        var face = _fRun ? _velocity with { Y = 0 } : dirT;
        if (face.LengthSquared() > 0.01f)
        {
            var ty = Mathf.Atan2(face.X, face.Z);
            _yaw = Mathf.LerpAngle(_yaw, ty, Mathf.Clamp(dt * 3f, 0, 1));
            Rotation = new Vector3(0, _yaw, 0);
        }
        _fwd = new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw));
        Model.AlienAiming = 1f;
        Model.AlienCrouch = 0f;
    }

    // ---- Portail : groupes d'aliens et de machines ----

    private const float PortalInterval = 13f;
    /// <summary>Escouade en vie d’un portail au-delà de laquelle il attend (le flot reste soutenable).</summary>
    private const int PortalMaxAlive = 6;

    private void HostTickPortal(float dt)
    {
        _pSpawnT -= dt;
        if (_pSpawnT > 0) return;
        _pSpawnT = PortalInterval / MissionDirector.ScaleForPlayers(Net.I.Players.Count);
        if (!_world.NearestPlayer(GlobalPosition, out _, out var t) || t.DistanceTo(GlobalPosition) > 230f) return;
        var all = _world.EnemiesById.Values.Where(e => e.State != EnemyState.Dead && !e.IsStatic).ToList();
        if (all.Count >= 90) return;
        // Le portail attend que son escouade précédente soit réduite (×1,4 à 2 selon l'escouade de joueurs).
        _pChildren.RemoveAll(e => !IsInstanceValid(e) || e.State == EnemyState.Dead);
        if (_pChildren.Count >= Mathf.CeilToInt(PortalMaxAlive * MissionDirector.ScaleForPlayers(Net.I.Players.Count))) return;
        var rng = _world.Rng;
        var r = rng.Randf();
        _pGroups++;
        var group = new List<EnemyType>();
        if (r < 0.5f)
        {
            group.Add(EnemyType.AlienTrooper);
            group.Add(EnemyType.AlienTrooper);
            if (rng.Randf() < 0.4f) group.Add(EnemyType.AlienTrooper);
        }
        else if (r < 0.7f) { group.Add(EnemyType.AlienGuard); group.Add(EnemyType.AlienTrooper); }
        else if (r < 0.85f) { group.Add(EnemyType.AlienFlyer); group.Add(EnemyType.AlienFlyer); }
        else if (r < 0.95f || _pGroups < 4 || all.Any(e => e.Def.Type == EnemyType.Walker)) { group.Add(EnemyType.Drone); group.Add(EnemyType.Drone); group.Add(EnemyType.Drone); }
        else group.Add(EnemyType.Walker);
        var side = rng.Randf() < 0.5f ? 1f : -1f;
        foreach (var type in group)
        {
            var p = GlobalPosition + GlobalBasis.Z * side * rng.RandfRange(4f, 8f) + GlobalBasis.X * rng.RandfRange(-4f, 4f);
            if (type == EnemyType.Walker) p = GlobalPosition + GlobalBasis.Z * side * 16f;
            p.Y = 0;
            _pChildren.Add(_world.HostSpawnEnemy(type, p));
        }
        ALog($"passage : {string.Join(", ", group)}");
        AlienFx(85, GlobalPosition);
        Net.I.BroadcastEnemyAttack(Id, 85, GlobalPosition);
    }

    // ---------------- Clients ----------------

    /// <summary>Octet d'état de l'instantané : 4 bits hauts = posture (accroupi, visée, charge, bouclier abaissé).</summary>
    private byte AlienStateByte()
    {
        var f = 0;
        if (Model.AlienCrouch > 0.5f) f |= 1;
        if (Model.AlienAiming > 0.5f) f |= 2;
        if (Model.AlienCharging) f |= 4;
        if (Model.AlienShieldDown) f |= 8;
        return (byte)((byte)State | (f << 4));
    }

    private void ApplyAlienFlags(int f)
    {
        Model.AlienCrouch = (f & 1) != 0 ? 1f : 0f;
        Model.AlienAiming = (f & 2) != 0 ? 1f : 0f;
        Model.AlienCharging = (f & 4) != 0;
        Model.AlienShieldDown = (f & 8) != 0;
    }

    private void ClientTickAlien(float dt)
    {
        if (IsPortal) return;
        if (_world.NearestPlayer(GlobalPosition, out _, out var t)) Model.AlienAim(t + Vector3.Up * 1.3f);
    }

    // ---------------- Effets (tous les pairs) ----------------

    /// <summary>
    /// Effets des aliens : 80 trait du fantassin, 83 blaster du garde, 84 laser du volant (point visé), 81 charge du garde,
    /// 82 coup de bouclier (point d'impact), 85 passage d'un groupe par le portail, 86 réception d'un alien largué.
    /// </summary>
    public void AlienFx(int kind, Vector3 target)
    {
        if (State == EnemyState.Dead) return;
        switch (kind)
        {
            case 80 or 83 or 84:
            {
                var m = Model.AlienMuzzle;
                Model.AlienFire();
                _attackAnim = 1f;
                if (!Net.I.IsHost)
                    Projectile.Spawn(_world, kind == 83 ? ProjectileKind.HeavyBolt : ProjectileKind.Bolt, m, (target - m).Normalized() * BoltSpeed(kind), false, 0, 0);
                // Éclair de bouche : lumière de la réserve partagée et gerbe d'étincelles (aucun nœud créé par tir).
                var fx = ExplosionFx.For(_world);
                fx.Flash(m, BugModel.AlienEnergy, kind == 83 ? 4f : 2.5f, kind == 83 ? 10f : 7f, 0.07f);
                var dir = (target - m).Normalized();
                for (var i = 0; i < (kind == 83 ? 6 : 3); i++)
                    fx.Spark(m, dir * 8f + new Vector3((float)GD.RandRange(-3, 3), (float)GD.RandRange(-3, 3), (float)GD.RandRange(-3, 3)), 0.12f, 0.12f, BugModel.AlienEnergy);
                Sfx.At(kind == 83 ? "alien_heavy" : kind == 84 ? "alien_laser" : "alien_rifle", m, kind == 83 ? 5f : 1f, (float)GD.RandRange(0.92, 1.08), 30);
                break;
            }
            case 81:
                Model.AlienCharging = true;
                Sfx.At("alien_roar", Center, 8f, (float)GD.RandRange(0.9, 1.05), 0);
                PuffFx(GlobalPosition + _fwd * 1.5f, 2.2f, 1.6f, true);
                break;
            case 82:
                Model.AlienBash();
                Sfx.At("alien_bash", target, 7f, (float)GD.RandRange(0.9, 1.1), 0);
                _world.ShakeCamera(target, 0.45f, 22f);
                PuffFx(target with { Y = GlobalPosition.Y + 0.3f }, 2.5f, 1.4f, true);
                break;
            case 85:
            {
                Model.PortalSurge();
                Sfx.At("portal_surge", Center, 10f, (float)GD.RandRange(0.9, 1.05), 0);
                var fx = ExplosionFx.For(_world);
                fx.Flash(Center, BugModel.AlienEnergy, 9f, 45f, 0.8f);
                for (var i = 0; i < 24; i++)
                {
                    var a = (float)GD.RandRange(0, Mathf.Tau);
                    var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    fx.Spark(Center + GlobalBasis * (d * BugModel.PortalRingR * 0.9f), GlobalBasis * (-d * 4f + new Vector3(0, 0, (float)GD.RandRange(-8, 8))), 0.18f, 0.9f, BugModel.AlienEnergy);
                }
                break;
            }
            case 86:
                PuffFx(target + Vector3.Up * 0.3f, 3f, 1.8f, true);
                Sfx.At("mech_step", target, 2f, 1.3f, 60);
                _world.ShakeCamera(target, 0.2f, 25f);
                break;
        }
    }

    // ---------------- Mort ----------------

    private void AlienDie()
    {
        _aBurstLeft = 0;
        if (_aShieldBody != null) _aShieldBody.CollisionLayer = 0;
        _aHum?.Stop();
        if (IsPortal) { PortalDie(); return; }
        var c = Center;
        ALog("abattu");
        Effects.Goo(_world, c, AlienBlood, 1.8f, 24);
        Sfx.At("alien_death", c, 5f, (float)GD.RandRange(0.85, 1.1), 40);
        var backward = GD.Randf() < 0.6f;
        Model.AlienDeath(backward);
        LandingPoint = GroundBelow(GlobalPosition);
        var tw = CreateTween();
        if (IsFlyerAlien && GlobalPosition.Y > LandingPoint.Y + 1f)
        {
            // Réacteurs coupés : chute en vrille, explosion du paquetage au sol.
            var fall = Mathf.Clamp(Mathf.Sqrt(2f * (GlobalPosition.Y - LandingPoint.Y) / 22f), 0.4f, 2.2f);
            var par = CreateTween().SetParallel();
            par.TweenProperty(this, "global_position", LandingPoint, fall).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
            par.TweenProperty(this, "rotation:y", _yaw + (float)GD.RandRange(2.5, 5.0), fall);
            ExplosionFx.For(_world).SmokePuff(c, Vector3.Up, 2f, 2.5f, new Color(0.2f, 0.18f, 0.2f));
            tw.TweenInterval(fall);
            tw.TweenCallback(Callable.From(() =>
            {
                Effects.Explosion(_world, GlobalPosition + Vector3.Up * 0.8f, 3.5f, 0);
                Effects.Smoke(_world, GlobalPosition + Vector3.Up, 2.5f, 5f);
            }));
        }
        else if (IsGuardAlien)
        {
            // La cellule dorsale se rompt.
            tw.TweenInterval(0.25f);
            tw.TweenCallback(Callable.From(() => Effects.Explosion(_world, ToGlobal(Model.AlienNodeXf(Model.AlienCoreNode).Origin), 3.2f, ExplosionFx.Plasma)));
        }
        tw.TweenInterval(7f);
        tw.TweenProperty(Model, "position:y", -2.5f, 2f).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(QueueFree));
    }

    /// <summary>Destruction du portail : la membrane implose, chapelet d'explosions sur l'anneau, déflagration, enfouissement.</summary>
    private void PortalDie()
    {
        ALog("portail détruit");
        LandingPoint = GlobalPosition;
        Model.PortalDeath();
        Sfx.At("portal_collapse", Center, 12f, 1f, 0);
        var fx = ExplosionFx.For(_world);
        fx.Flash(Center, BugModel.AlienEnergy, 14f, 60f, 1.2f);
        var rng = new RandomNumberGenerator();
        var tw = CreateTween();
        tw.TweenInterval(1f);
        tw.TweenCallback(Callable.From(() =>
        {
            Effects.Explosion(_world, Center, 12f, ExplosionFx.Plasma);
            _world.ShakeCamera(Center, 0.9f, 120f);
        }));
        for (var i = 0; i < 6; i++)
        {
            tw.TweenInterval(0.35f);
            tw.TweenCallback(Callable.From(() =>
            {
                var a = rng.RandfRange(0, Mathf.Tau);
                var p = Center + GlobalBasis * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * (BugModel.PortalRingR + 0.8f);
                Effects.Explosion(_world, p, rng.RandfRange(4f, 7f), 0);
            }));
        }
        tw.TweenCallback(Callable.From(() =>
        {
            Effects.Dust(_world, GlobalPosition, new Vector3(30f, 8f, 30f));
            Effects.Smoke(_world, Center, 8f, 9f);
        }));
        tw.TweenInterval(9f);
        tw.TweenProperty(Model, "position:y", -19f, 7f).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(QueueFree));
    }

    // ---------------- Caméra ----------------

    /// <summary>Alien entre la caméra et le soldat (ou caméra dans son corps) : translucide.</summary>
    private void AlienCameraFade(Vector3 cam, Vector3 focus)
    {
        if (IsPortal) return;
        var fade = false;
        if (State != EnemyState.Dead)
        {
            var d = Model.AlienDimensions;
            var r = 0.75f * d.L * d.W;
            var b = GlobalPosition;
            var h = Model.AlienChestHeight + 0.6f * d.L;
            for (var k = 0; k < 4 && !fade; k++)
                fade = SegmentDistance(b + Vector3.Up * (0.6f + (h - 0.6f) * k / 3f), cam, focus) < r;
        }
        if (fade == _camFaded) return;
        _camFaded = fade;
        Model.SetFade(fade ? 0.7f : 0f);
    }

    /// <summary>Portail : le bourdonnement démarre quand la synthèse est prête.</summary>
    private void PortalAudio()
    {
        if (_aHum == null || _aHum.Playing || State == EnemyState.Dead) return;
        if (Sfx.Loop("portal_hum") is not { } loop) return;
        _aHum.Stream = loop;
        _aHum.Play((float)GD.RandRange(0, 1.5));
    }
}

/// <summary>
/// Bouclier frontal du garde alien : corps statique sur la couche des ennemis, recalé sur le bouclier animé. Les tirs
/// s'y arrêtent (ce n'est pas un <see cref="Enemy"/> : aucun dégât) avec des étincelles et un tintement.
/// </summary>
public partial class AlienShield : StaticBody3D
{
    public Enemy Bearer;

    /// <summary>Balle locale arrêtée par le bouclier.</summary>
    public void LocalHit(Vector3 at)
    {
        if (Bearer == null || !IsInstanceValid(Bearer) || Bearer.State == EnemyState.Dead) return;
        var w = World.Current;
        if (w == null) return;
        var fx = ExplosionFx.For(w);
        var n = GlobalBasis.Z;
        for (var i = 0; i < 4; i++)
            fx.Spark(at, (n + new Vector3((float)GD.RandRange(-0.8, 0.8), (float)GD.RandRange(-0.4, 0.9), (float)GD.RandRange(-0.8, 0.8))).Normalized() * (float)GD.RandRange(4, 10),
                0.1f, (float)GD.RandRange(0.15, 0.35), i % 2 == 0 ? BugModel.AlienEnergy : new Color(1f, 0.85f, 0.6f));
        Sfx.At("shield_hit", at, -2f, (float)GD.RandRange(0.9, 1.15), 60);
    }
}
