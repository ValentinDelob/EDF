using System.Linq;
using Godot;

namespace Rempart;

public enum EnemyState : byte { Emerging = 0, Moving = 1, Attacking = 2, Dead = 3 }

/// <summary>
/// Ennemi. Sur l'hôte, l'IA le pilote ; sur les clients, il interpole les instantanés reçus.
/// Formes de collision : 0 = corps, 1 = tête (point faible).
/// </summary>
public partial class Enemy : CharacterBody3D
{
    public int Id;
    public EnemyDef Def;
    public float Health;
    public float MaxHealth;
    public EnemyState State = EnemyState.Emerging;
    public BugModel Model;
    public int EatenCrates;
    /// <summary>Variante élite : dorée, PV ×3, dégâts ×1,5, butin garanti.</summary>
    public bool Elite;
    public float DamageScale = 1f;
    public bool IsBoss => Def.Boss;
    public bool IsStatic => Def.Static;

    private World _world;
    private float _emergeT;
    private float _attackCd;
    private float _attackAnim;
    private float _thinkT;
    private Vector3 _moveDir;
    private Vector3 _velocity;
    private float _speedNow;
    private float _yaw;
    private float _altitude;
    private float _orbit;
    private int _targetCrate = -1;
    private float _leapT = -1f;
    private Vector3 _leapFrom, _leapTo;
    private float _specialCd;

    // Interpolation côté client
    private Vector3 _netPos;
    private float _netYaw;
    private Vector3 _prevPos;

    public static Enemy Create(World world, int id, EnemyType type, Vector3 pos, float yaw, float hpMul)
    {
        var def = Enemies.Get(type);
        var e = new Enemy
        {
            Id = id,
            Def = def,
            Name = $"Enemy{id}",
            CollisionLayer = 1 << 2,
            CollisionMask = 0,
            _world = world,
        };
        // Un multiplicateur négatif transmis par le réseau signale une variante élite.
        e.Elite = hpMul < 0;
        hpMul = Mathf.Abs(hpMul);
        e.MaxHealth = e.Health = def.Health * hpMul * (e.Elite ? 3f : 1f);
        e.DamageScale = e.Elite ? 1.5f : 1f;
        e.Model = BugModel.Create(type);
        e.AddChild(e.Model);
        if (e.Elite)
        {
            var gold = Mats.Chitin(new Color(0.78f, 0.58f, 0.14f));
            foreach (var n in e.Model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (mi.Mesh is SphereMesh && mi.Scale.X > 0.5f) mi.MaterialOverride = gold;
            }
            e.Model.Scale *= 1.12f;
        }
        var s = def.Size;
        if (type == EnemyType.Nest)
        {
            e.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 6f, Height = 4.5f }, Position = new Vector3(0, 2.2f, 0) });
            e.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 2.2f }, Position = new Vector3(0, 4.3f, 0) });
        }
        else if (type == EnemyType.Fortress)
        {
            // 0 = coque, 1 = noyau (point faible), puis une colonne par patte (position de repos).
            e.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(18.5f, 10f, 24f) }, Position = new Vector3(0, BugModel.FortHullY, -0.5f) });
            e.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = BugModel.FortCoreRadius }, Position = BugModel.FortCorePos });
            for (var i = 0; i < 4; i++)
            {
                var f = BugModel.FortressFootRest(i);
                e.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3.4f, 24f, 3.4f) }, Position = new Vector3(f.X, 12f, f.Z) });
            }
            // Arrive déjà debout (pas de sortie de terre) ; chaque pas secoue le sol.
            e.State = EnemyState.Moving;
            e.Model.Stomp += e.OnFortressStomp;
        }
        else if (type == EnemyType.Centipede)
        {
            // 0 = cou, 1 = tête (point faible), puis une sphère par segment, recalée chaque image sur le corps.
            e.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.1f, 0.8f, 0.7f) * s }, Position = new Vector3(0, 0.6f, -0.15f) * s });
            e.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.5f * s }, Position = new Vector3(0, 0.65f, 0.4f) * s });
            e._segShapes = new CollisionShape3D[e.Model.CentipedeSegmentCount];
            for (var i = 0; i < e._segShapes.Length; i++)
            {
                e._segShapes[i] = new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.55f * s * e.Model.CentipedeSegmentScale(i) } };
                e.AddChild(e._segShapes[i]);
            }
            e.SyncSegmentShapes();
        }
        else if (type is EnemyType.Wasp or EnemyType.Drone)
        {
            e.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.6f, 1.4f, 3.4f) * s }, Position = new Vector3(0, 0, -0.5f) * s });
            e.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.55f * s }, Position = new Vector3(0, 0, 0.75f) * s });
        }
        else
        {
            e.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.8f, 1.5f, 4.0f) * s }, Position = new Vector3(0, 1.1f, -0.7f) * s });
            e.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.6f * s }, Position = new Vector3(0, 1.25f, 1.35f) * s });
        }
        e.Position = pos;
        e._yaw = yaw;
        e.Rotation = new Vector3(0, yaw, 0);
        e._netPos = e._prevPos = pos;
        e._netYaw = yaw;
        e._altitude = (float)GD.RandRange(12, 22);
        e._orbit = (float)GD.RandRange(0, Mathf.Tau);
        e._attackCd = (float)GD.RandRange(0.5, 1.5) * def.AttackCooldown;
        e._specialCd = 6f;
        if (def.Static) e.State = EnemyState.Moving;
        return e;
    }

    public override void _Ready()
    {
        if (State == EnemyState.Emerging)
        {
            Model.Position = new Vector3(0, -2.5f * Def.Size, 0);
            if (!Def.Flying) Effects.Emerge(_world, GlobalPosition, 2f * Def.Size);
            else Model.Position = Vector3.Zero;
        }
    }

    public Vector3 Center => IsFortress ? GlobalPosition + Vector3.Up * BugModel.FortHullY
        : GlobalPosition + Vector3.Up * (Def.Flying ? 0 : (_segShapes != null ? 0.65f : 1.1f) * Def.Size);

    private bool IsFortress => Def.Type == EnemyType.Fortress;
    /// <summary>Zone visée par le canon principal pendant sa charge (hôte uniquement), sinon null.</summary>
    public Vector3? CannonZone => IsFortress && _fChargeT >= 0 ? _fCannonTarget : null;

    /// <summary>Formes de collision des segments du mille-pattes (le corps suit la trace de la tête).</summary>
    private CollisionShape3D[] _segShapes;

    private void AnimateModel(float dt, float speed, float attack)
    {
        if (IsFortress) Model.AnimateFortress(dt, speed, attack);
        else Model.Animate(dt, speed, attack);
        if (_segShapes != null) SyncSegmentShapes();
    }

    /// <summary>Recale les formes de collision sur les segments affichés (hôte et clients : les tirs touchent tout le corps).</summary>
    private void SyncSegmentShapes()
    {
        var xf = Model.Transform;
        for (var i = 0; i < _segShapes.Length; i++) _segShapes[i].Position = xf * Model.CentipedeSegmentLocal(i);
    }

    /// <summary>Distance d'un point au corps : le centre, ou le segment le plus proche pour le mille-pattes.</summary>
    public float BodyDistance(Vector3 p)
    {
        var d = Center.DistanceTo(p);
        if (_segShapes != null)
            foreach (var sh in _segShapes) d = Mathf.Min(d, sh.GlobalPosition.DistanceTo(p));
        return d;
    }

    // ---------------- Hôte ----------------

    /// <summary>Fige l'IA (mode vitrine pour contrôler les modèles).</summary>
    public static bool Frozen;

    public void HostTick(float dt)
    {
        if (State == EnemyState.Dead) return;
        if (Frozen && State != EnemyState.Emerging) { AnimateModel(dt, 2f, Mathf.Max(0, Mathf.Sin(Time.GetTicksMsec() / 600f))); return; }
        if (State == EnemyState.Emerging)
        {
            _emergeT += dt;
            var k = Mathf.Clamp(_emergeT / 1.2f, 0, 1);
            if (!Def.Flying) Model.Position = new Vector3(0, Mathf.Lerp(-2.5f * Def.Size, 0, k), 0);
            if (k >= 1) State = EnemyState.Moving;
            AnimateModel(dt, 3f, 0);
            return;
        }
        if (Def.Static) { AnimateModel(dt, 0, 0); return; }

        _attackCd -= dt;
        _specialCd -= dt;
        _attackAnim = Mathf.Max(0, _attackAnim - dt * 3f);
        if (!_world.NearestPlayer(GlobalPosition, out var peer, out var target))
        {
            _speedNow = Mathf.Lerp(_speedNow, 0, dt * 4);
            AnimateModel(dt, _speedNow, _attackAnim);
            return;
        }
        if (IsFortress)
        {
            TickFortress(dt, peer, target);
            AnimateModel(dt, _speedNow, _attackAnim);
            return;
        }

        // Les soldats alliés plus proches que le joueur deviennent la cible.
        Ally ally = null;
        if (!Def.Boss)
        {
            var a = _world.Allies?.Nearest(GlobalPosition, 45f);
            if (a != null && a.GlobalPosition.DistanceTo(GlobalPosition) < target.DistanceTo(GlobalPosition) * 0.85f)
            {
                ally = a;
                target = a.GlobalPosition;
            }
        }
        if (Def.Flying) TickFlying(dt, peer, target, ally);
        else TickGround(dt, peer, target, ally);
        AnimateModel(dt, _speedNow, _attackAnim);
    }

    private void TickGround(float dt, long peer, Vector3 target, Ally ally)
    {
        var pos = GlobalPosition;
        var goal = target;

        // Charognard : les caisses d'abord.
        if (Def.Type == EnemyType.Scavenger)
        {
            _thinkT -= dt;
            if (_thinkT <= 0)
            {
                _thinkT = 0.5f;
                _targetCrate = _world.NearestCrate(pos, 90f);
            }
            if (_targetCrate >= 0 && _world.CratePosition(_targetCrate, out var cp))
            {
                goal = cp;
                if (pos.DistanceTo(cp) < 3.5f * Def.Size)
                {
                    _world.HostEatCrate(_targetCrate, this);
                    _targetCrate = -1;
                }
            }
        }

        var toGoal = goal - pos;
        toGoal.Y = 0;
        var dist = toGoal.Length();

        // Araignée : bonds paraboliques vers la cible.
        if (Def.Type == EnemyType.Spider)
        {
            if (_leapT >= 0)
            {
                _leapT += dt / 0.9f;
                var k = Mathf.Min(_leapT, 1f);
                var p = _leapFrom.Lerp(_leapTo, k);
                p.Y = Mathf.Sin(k * Mathf.Pi) * 9f;
                GlobalPosition = p;
                _speedNow = 0;
                if (_leapT >= 1f) { _leapT = -1f; GlobalPosition = new Vector3(_leapTo.X, 0, _leapTo.Z); Sfx.At("hit", _leapTo, -2f, 0.6f); }
                return;
            }
            if (_specialCd <= 0 && dist > 8f && dist < 60f)
            {
                _specialCd = (float)GD.RandRange(4, 7);
                _leapFrom = pos;
                var land = pos + toGoal.Normalized() * Mathf.Min(dist - 5f, 28f);
                if (!_world.Flow.IsBlocked(land)) { _leapTo = land; _leapT = 0f; Sfx.At("screech", pos, -2f, 1.4f); return; }
            }
        }
        var reach = Def.AttackRange * (Def.Ranged ? 0.85f : 1f) + (Def.Boss ? 2f : 0);
        var desired = Vector3.Zero;
        var attacking = goal == target && dist <= reach;

        if (!attacking)
        {
            if (dist < 14f || Def.Boss) desired = toGoal / Mathf.Max(dist, 0.01f);
            else
            {
                desired = _world.Flow.Direction(pos);
                if (desired == Vector3.Zero) desired = toGoal / Mathf.Max(dist, 0.01f);
            }
        }
        // Séparation entre ennemis.
        var sep = _world.Separation(this, 3.2f * Def.Size);
        var move = desired + sep * 2.2f;
        if (move.LengthSquared() > 1) move = move.Normalized();
        _moveDir = _moveDir.Lerp(move, Mathf.Clamp(dt * 6f, 0, 1));
        var speed = Def.Speed * (attacking ? 0.15f : 1f);
        var step = _moveDir * speed * dt;
        var next = pos + step;
        if (!Def.Boss && _world.Flow.IsBlocked(next) && !_world.Flow.IsBlocked(pos)) next = pos; // ne traverse pas les immeubles
        next.Y = 0;
        _speedNow = (next - pos).Length() / Mathf.Max(dt, 0.0001f);
        GlobalPosition = next;

        // Orientation
        var face = attacking || dist < 20 ? toGoal : _moveDir;
        if (face.LengthSquared() > 0.01f)
        {
            var targetYaw = Mathf.Atan2(face.X, face.Z);
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, Mathf.Clamp(dt * (Def.Boss ? 2f : 7f), 0, 1));
            Rotation = new Vector3(0, _yaw, 0);
        }

        // Mille-pattes : courte giclée d'acide sur une cible à mi-distance, entre deux morsures.
        if (Def.Type == EnemyType.Centipede && ally == null && _specialCd <= 0 && dist > reach + 3f && dist < 24f)
        {
            _specialCd = (float)GD.RandRange(4.5, 7.5);
            _attackAnim = 1f;
            _world.HostEnemyProjectile(this, 0, Center + Basis.Z * 1.4f * Def.Size, target + Vector3.Up, 1);
            Net.I.BroadcastEnemyAttack(Id, 1, target);
        }
        if (attacking && _attackCd <= 0)
        {
            _attackCd = Def.AttackCooldown * (float)GD.RandRange(0.85, 1.15);
            _attackAnim = 1f;
            if (ally != null)
            {
                _world.Allies.HostDamage(ally, Def.Damage * _world.DamageMul * DamageScale * (Def.Ranged ? 0.6f : 1f));
                Net.I.BroadcastEnemyAttack(Id, Def.Ranged ? 1 : 0, target);
                if (Def.Ranged) Projectile.Spawn(_world, ProjectileKind.Acid, Center + Basis.Z * 1.5f * Def.Size, World.BallisticVelocity(Center, target + Vector3.Up, 30f, 9f), false, 0, 0);
            }
            else if (Def.Ranged && (!Def.Boss || dist > 10f))
            {
                var from = Center + Basis.Z * 1.5f * Def.Size;
                _world.HostEnemyProjectile(this, Def.Type == EnemyType.Spider ? 2 : 0, from, target + Vector3.Up, Def.Boss ? 3 : 1);
                Net.I.BroadcastEnemyAttack(Id, 1, target);
            }
            else
            {
                Net.I.DamagePlayer(peer, Def.Damage * _world.DamageMul * DamageScale, pos);
                Net.I.BroadcastEnemyAttack(Id, 0, target);
                Sfx.At("hit", pos, 0f, 0.7f);
            }
        }
        // La reine crache de l'acide en éventail de temps en temps.
        if (Def.Boss && _specialCd <= 0)
        {
            _specialCd = (float)GD.RandRange(5, 8);
            for (var i = -3; i <= 3; i++)
            {
                var side = Basis.X * i * 5f;
                _world.HostEnemyProjectile(this, 0, Center + Basis.Z * 4f + Vector3.Up * 2f, target + side, 1);
            }
            Net.I.BroadcastEnemyAttack(Id, 2, target);
            Sfx.At("screech", pos, 8f, 0.6f);
        }
        State = attacking ? EnemyState.Attacking : EnemyState.Moving;
    }

    private void TickFlying(float dt, long peer, Vector3 target, Ally ally)
    {
        var pos = GlobalPosition;
        _orbit += dt * 0.5f;
        var ring = new Vector3(Mathf.Cos(_orbit), 0, Mathf.Sin(_orbit)) * 20f;
        var goal = target + ring + Vector3.Up * _altitude;
        var to = goal - pos;
        var sep = _world.Separation(this, 3f);
        var desired = (to.LengthSquared() > 1 ? to.Normalized() : Vector3.Zero) + sep;
        _velocity = _velocity.Lerp(desired * Def.Speed, Mathf.Clamp(dt * 2f, 0, 1));
        var next = pos + _velocity * dt;
        next.Y = Mathf.Max(next.Y, 4f);
        GlobalPosition = next;
        _speedNow = _velocity.Length();
        var look = target - next;
        var targetYaw = Mathf.Atan2(look.X, look.Z);
        _yaw = Mathf.LerpAngle(_yaw, targetYaw, Mathf.Clamp(dt * 4f, 0, 1));
        Rotation = new Vector3(0, _yaw, 0);
        var dist = pos.DistanceTo(target);
        if (dist < Def.AttackRange + 15 && _attackCd <= 0)
        {
            _attackCd = Def.AttackCooldown * (float)GD.RandRange(0.8, 1.3);
            _attackAnim = 1;
            if (ally != null)
            {
                _world.Allies.HostDamage(ally, Def.Damage * _world.DamageMul * DamageScale);
                Projectile.Spawn(_world, ProjectileKind.Stinger, pos + Vector3.Down * 0.8f, (target + Vector3.Up - pos).Normalized() * 60f, false, 0, 0);
            }
            else _world.HostEnemyProjectile(this, Def.Type == EnemyType.Drone ? 3 : 1, pos + Vector3.Down * 0.8f, target + Vector3.Up * 1f, 1);
            Net.I.BroadcastEnemyAttack(Id, 3, target);
        }
        State = EnemyState.Moving;
    }

    public void PackSnapshot(System.IO.BinaryWriter w)
    {
        var p = GlobalPosition;
        w.Write(Id);
        w.Write(p.X); w.Write(p.Y); w.Write(p.Z);
        w.Write((short)(Mathf.Wrap(_yaw, -Mathf.Pi, Mathf.Pi) * 10000f));
        w.Write((byte)State);
        w.Write((byte)Mathf.Clamp(Health / MaxHealth * 255f, 0, 255));
        w.Write((byte)Mathf.Clamp(_attackAnim * 255f, 0, 255));
        w.Write((byte)Mathf.Clamp(_speedNow * 10f, 0, 255));
    }

    // ---------------- Client ----------------

    public void ApplySnapshot(Vector3 pos, float yaw, EnemyState state, float hpFrac, float attack, float speed)
    {
        _prevPos = GlobalPosition;
        _netPos = pos;
        _netYaw = yaw;
        if (State != EnemyState.Dead) State = state;
        Health = hpFrac * MaxHealth;
        _attackAnim = Mathf.Max(_attackAnim, attack);
        _speedNow = speed;
    }

    public void ClientTick(float dt)
    {
        if (State == EnemyState.Dead) return;
        if (State == EnemyState.Emerging && !Def.Flying)
        {
            _emergeT += dt;
            Model.Position = new Vector3(0, Mathf.Lerp(-2.5f * Def.Size, 0, Mathf.Clamp(_emergeT / 1.2f, 0, 1)), 0);
        }
        else if (Model.Position.Y < 0) Model.Position = Vector3.Zero;
        var p = GlobalPosition;
        if (p.DistanceSquaredTo(_netPos) > 400) GlobalPosition = _netPos;
        else GlobalPosition = p.Lerp(_netPos, Mathf.Clamp(dt * 12f, 0, 1));
        _yaw = Mathf.LerpAngle(_yaw, _netYaw, Mathf.Clamp(dt * 10f, 0, 1));
        Rotation = new Vector3(0, _yaw, 0);
        _attackAnim = Mathf.Max(0, _attackAnim - dt * 3f);
        AnimateModel(dt, _speedNow, _attackAnim);
    }

    public void PlayAttack() => _attackAnim = 1f;

    // ---------------- Mort ----------------

    public void Die()
    {
        if (State == EnemyState.Dead) return;
        State = EnemyState.Dead;
        CollisionLayer = 0;
        if (IsFortress) { FortressDie(); return; }
        var pos = Center;
        var color = Def.Type switch
        {
            EnemyType.AcidAnt => new Color(0.7f, 0.85f, 0.15f),
            EnemyType.Nest => new Color(0.35f, 0.28f, 0.2f),
            _ => new Color(0.55f, 0.5f, 0.15f),
        };
        Effects.Goo(_world, pos, color, Def.Size, Def.Boss ? 60 : 14);
        if (Def.Type == EnemyType.Nest || Def.Boss) Effects.Explosion(_world, pos, Def.Boss ? 14 : 9, 0);
        Sfx.At("death_bug", pos, Def.Boss ? 10 : 0, Def.Boss ? 0.5f : (float)GD.RandRange(0.8, 1.2), 30);
        if (Def.Type != EnemyType.Nest) Model.DeathPose();
        var tw = CreateTween();
        if (Def.Flying)
        {
            tw.TweenProperty(this, "global_position:y", 0.3f, Mathf.Max(0.3f, GlobalPosition.Y / 25f)).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        }
        tw.TweenInterval(Def.Boss ? 10f : 3.5f);
        tw.TweenProperty(Model, "position:y", -3f * Def.Size, 1.5f);
        tw.TweenCallback(Callable.From(QueueFree));
    }

    public void FlashHit()
    {
        _attackAnim = Mathf.Max(_attackAnim, 0.25f);
        if (!IsFortress) Model.Flash(); // cible touchée en continu : pas de flash
    }

    // ---------------- Forteresse marcheuse ----------------

    private float _fLaserCd = 5f, _fMissileCd = 11f, _fCannonCd = 17f, _fDroneCd, _fCrushT;
    private int _fVolley, _fMissiles;
    private float _fVolleyT, _fMissileT, _fChargeT = -1f;
    private Vector3 _fCannonTarget;
    private bool _fEnraged;

    /// <summary>
    /// IA de la forteresse (hôte) : marche lente vers le joueur le plus proche en écrasant les immeubles,
    /// salves laser des tourelles, barrages de missiles, canon principal chargé 2 s. Sous 50 % : drones et cadence accrue.
    /// </summary>
    private void TickFortress(float dt, long peer, Vector3 target)
    {
        var pos = GlobalPosition;
        var to = target - pos;
        to.Y = 0;
        var dist = to.Length();
        if (!_fEnraged && Health < MaxHealth * 0.5f)
        {
            _fEnraged = true;
            _fDroneCd = 1.5f;
            Net.I.BroadcastMessage("La forteresse est endommagée : elle libère ses drones et redouble de violence !", 1);
        }
        var rate = _fEnraged ? 1.6f : 1f;

        // Lourde rotation sur place, puis avance tant que la cible n'est pas sous elle.
        if (dist > 0.5f)
        {
            _yaw = Mathf.LerpAngle(_yaw, Mathf.Atan2(to.X, to.Z), Mathf.Clamp(dt * 0.3f, 0, 1));
            Rotation = new Vector3(0, _yaw, 0);
        }
        var fwd = new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw));
        var align = dist > 0.5f ? fwd.Dot(to / dist) : 0f;
        var want = dist > 14f ? Def.Speed * Mathf.Clamp((align + 0.2f) / 1.2f, 0.3f, 1f) : 0f;
        _speedNow = Mathf.MoveToward(_speedNow, want, dt * 1.2f);
        GlobalPosition = pos + fwd * _speedNow * dt;

        _fCrushT -= dt;
        if (_fCrushT <= 0)
        {
            _fCrushT = 0.25f;
            CrushBuildings(GlobalPosition, 13f);
        }

        Model.FortressAim(target + Vector3.Up);
        _fLaserCd -= dt * rate;
        _fMissileCd -= dt * rate;
        _fCannonCd -= dt * rate;
        _attackAnim = Mathf.Max(_attackAnim, _fChargeT >= 0 ? _fChargeT * 0.5f : 0f);

        // Salve laser : chaque tourelle tire deux fois, en alternance.
        var turrets = Model.FortressTurretCount;
        if (_fVolley > 0)
        {
            _fVolleyT -= dt;
            if (_fVolleyT <= 0)
            {
                _fVolleyT = 0.14f;
                _fVolley--;
                var i = _fVolley % turrets;
                var aim = target + Vector3.Up + new Vector3((float)GD.RandRange(-3, 3), (float)GD.RandRange(-0.5, 1.5), (float)GD.RandRange(-3, 3));
                _world.HostEnemyProjectile(this, 3, Model.FortressTurretMuzzle(i), aim, 1);
                Net.I.BroadcastEnemyAttack(Id, 20 + i, aim);
            }
        }
        else if (_fLaserCd <= 0 && dist < Def.AttackRange)
        {
            _fLaserCd = Def.AttackCooldown * (float)GD.RandRange(0.8, 1.2);
            _fVolley = turrets * 2;
        }

        // Barrage de missiles en cloche autour de la cible.
        if (_fMissiles > 0)
        {
            _fMissileT -= dt;
            if (_fMissileT <= 0)
            {
                _fMissileT = 0.18f;
                _fMissiles--;
                var a = (float)GD.RandRange(0, Mathf.Tau);
                var r = Mathf.Sqrt((float)GD.Randf()) * 12f;
                var aim = new Vector3(target.X + Mathf.Cos(a) * r, 0, target.Z + Mathf.Sin(a) * r);
                FireBomb(_fMissiles % 2, aim, true);
                Net.I.BroadcastEnemyAttack(Id, 30 + _fMissiles % 2, aim);
            }
        }
        else if (_fMissileCd <= 0 && dist < Def.AttackRange)
        {
            _fMissileCd = (float)GD.RandRange(12, 15);
            _fMissiles = _fEnraged ? 12 : 8;
        }

        // Canon principal : 2 s de charge visible (zone marquée au sol), puis tir dévastateur.
        if (_fChargeT >= 0)
        {
            _fChargeT += dt;
            if (_fChargeT >= 2f)
            {
                _fChargeT = -1f;
                FortressFx(11, _fCannonTarget);
                Net.I.BroadcastEnemyAttack(Id, 11, _fCannonTarget);
                _world.HostEnemyBlast(_fCannonTarget, 10f, Def.Damage * 20f * _world.DamageMul * DamageScale);
            }
        }
        else if (_fCannonCd <= 0 && dist < Def.AttackRange)
        {
            _fCannonCd = (float)GD.RandRange(14, 18);
            _fChargeT = 0f;
            _fCannonTarget = new Vector3(target.X, 0.3f, target.Z);
            FortressFx(10, _fCannonTarget);
            Net.I.BroadcastEnemyAttack(Id, 10, _fCannonTarget);
        }

        // Seconde phase : lâcher de drones depuis la coque.
        if (_fEnraged)
        {
            _fDroneCd -= dt;
            if (_fDroneCd <= 0)
            {
                _fDroneCd = 20f;
                var n = _world.EnemiesById.Values.Count(e => e.Def.Type == EnemyType.Drone && e.State != EnemyState.Dead);
                for (var i = 0; i < 4 && n < 16; i++, n++)
                {
                    var a = i * Mathf.Tau / 4 + _yaw;
                    _world.HostSpawnEnemy(EnemyType.Drone, GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 12f);
                }
                FortressFx(12, target);
                Net.I.BroadcastEnemyAttack(Id, 12, target);
            }
        }
        State = _fVolley > 0 || _fMissiles > 0 || _fChargeT >= 0 ? EnemyState.Attacking : EnemyState.Moving;
    }

    /// <summary>Écrase les immeubles dont l'emprise touche le cercle donné (hôte).</summary>
    private void CrushBuildings(Vector3 c, float radius)
    {
        foreach (var b in _world.Buildings)
        {
            if (b.Destroyed) continue;
            var local = c - b.GlobalPosition;
            var dx = local.X - Mathf.Clamp(local.X, -b.Size.X / 2, b.Size.X / 2);
            var dz = local.Z - Mathf.Clamp(local.Z, -b.Size.Z / 2, b.Size.Z / 2);
            if (dx * dx + dz * dz <= radius * radius) _world.HostBuildingDamage(b.Id, b.MaxHealth + b.Health + 1e5f);
        }
    }

    /// <summary>Missile en cloche tiré d'un lance-missiles latéral (autoritaire sur l'hôte, visuel ailleurs).</summary>
    private void FireBomb(int pod, Vector3 aim, bool host)
    {
        var from = Model.FortressPod(pod) + Vector3.Up * 0.6f;
        var flat = new Vector3(aim.X - from.X, 0, aim.Z - from.Z);
        var t = Mathf.Clamp(flat.Length() / 22f, 2.4f, 5f);
        var v = flat / t + Vector3.Up * ((aim.Y - from.Y + 0.5f * Projectile.BombGravity * t * t) / t);
        var p = Projectile.Spawn(_world, ProjectileKind.Bomb, from, v, host, host ? Def.Damage * 2f * _world.DamageMul * DamageScale : 0, 5f);
        if (host) p.SourceEnemy = this;
        Sfx.At("launcher", from, 4f, 0.55f);
    }

    /// <summary>Effets des attaques de la forteresse, joués chez tous les pairs (10 = charge, 11 = tir, 12 = drones, 2x = laser, 3x = missile).</summary>
    public void FortressFx(int kind, Vector3 target)
    {
        if (!IsFortress || State == EnemyState.Dead) return;
        switch (kind)
        {
            case 10:
                Model.FortressCharge();
                Effects.Ring(_world, target, 10f, new Color(1f, 0.15f, 0.1f), 2f);
                Effects.Ring(_world, target, 4f, new Color(1f, 0.15f, 0.1f), 2f);
                Sfx.At("wave", Model.FortressMuzzle, 12f, 0.45f, 0);
                break;
            case 11:
                Model.FortressFire();
                var from = Model.FortressMuzzle;
                Effects.Tracer(_world.FxRoot, from, target, new Color(0.6f, 0.95f, 1f), 1.3f, 0.3f);
                Effects.Tracer(_world.FxRoot, from, target, new Color(1f, 1f, 1f), 0.45f, 0.35f);
                Sfx.At("plasma", from, 12f, 0.35f, 0);
                _world.ShakeCamera(target, 1f, 60f);
                break;
            case 12:
                Sfx.At("buzz", GlobalPosition + Vector3.Up * 30f, 10f, 0.5f);
                break;
            case >= 20 and < 30:
                var m = Model.FortressTurretMuzzle(kind - 20);
                Model.FortressAim(target);
                Projectile.Spawn(_world, ProjectileKind.Laser, m, (target - m).Normalized() * 90f, false, 0, 0);
                Sfx.At("laser", m, -2f, 0.7f);
                break;
            case >= 30 and < 40:
                Model.FortressAim(target);
                FireBomb(kind - 30, target, false);
                break;
        }
    }

    /// <summary>Un pied se pose : poussière, secousse ; sur l'hôte, écrasement et dégâts de piétinement.</summary>
    private void OnFortressStomp(Vector3 p)
    {
        if (State == EnemyState.Dead) return;
        Effects.Dust(_world, p, new Vector3(9f, 3f, 9f));
        _world.ShakeCamera(p, 0.55f, 70f);
        Sfx.At("hammer", p, 10f, 0.3f, 80);
        if (!Net.I.IsHost) return;
        CrushBuildings(p, 5f);
        if (_world.NearestPlayer(p, out var peer, out var at) && at.DistanceTo(p) < 7f)
            Net.I.DamagePlayer(peer, Def.Damage * 6f * _world.DamageMul * DamageScale, p);
    }

    /// <summary>Mort de la forteresse : chapelet d'explosions sur la coque, effondrement, déflagration finale, enfouissement.</summary>
    private void FortressDie()
    {
        Model.FortressDeath();
        Sfx.At("collapse", Center, 12f, 0.6f, 0);
        var tw = CreateTween();
        var rng = new RandomNumberGenerator();
        for (var i = 0; i < 10; i++)
        {
            tw.TweenCallback(Callable.From(() =>
            {
                var o = new Vector3(rng.RandfRange(-9, 9), rng.RandfRange(-4, 5), rng.RandfRange(-11, 11));
                Effects.Explosion(_world, Model.FortressHullCenter + Basis * o, rng.RandfRange(6f, 11f), 0);
            }));
            tw.TweenInterval(0.38f);
        }
        tw.TweenCallback(Callable.From(() =>
        {
            var c = Model.FortressHullCenter;
            Effects.Explosion(_world, c, 22f, 0);
            Effects.Explosion(_world, c + Vector3.Up * 5f, 15f, 2);
            Effects.Dust(_world, new Vector3(c.X, 0, c.Z), new Vector3(46f, 14f, 46f));
            _world.ShakeCamera(c, 1.2f, 160f);
        }));
        tw.TweenInterval(12f);
        tw.TweenProperty(Model, "position:y", -32f, 6f).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(QueueFree));
    }
}
