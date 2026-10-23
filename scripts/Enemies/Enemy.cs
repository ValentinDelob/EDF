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
        e.MaxHealth = e.Health = def.Health * hpMul;
        e.Model = BugModel.Create(type);
        e.AddChild(e.Model);
        var s = def.Size;
        if (type == EnemyType.Nest)
        {
            e.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 6f, Height = 4.5f }, Position = new Vector3(0, 2.2f, 0) });
            e.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 2.2f }, Position = new Vector3(0, 4.3f, 0) });
        }
        else if (type == EnemyType.Wasp)
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

    public Vector3 Center => GlobalPosition + Vector3.Up * (Def.Flying ? 0 : 1.1f * Def.Size);

    // ---------------- Hôte ----------------

    /// <summary>Fige l'IA (mode vitrine pour contrôler les modèles).</summary>
    public static bool Frozen;

    public void HostTick(float dt)
    {
        if (State == EnemyState.Dead) return;
        if (Frozen && State != EnemyState.Emerging) { Model.Animate(dt, 2f, Mathf.Max(0, Mathf.Sin(Time.GetTicksMsec() / 600f))); return; }
        if (State == EnemyState.Emerging)
        {
            _emergeT += dt;
            var k = Mathf.Clamp(_emergeT / 1.2f, 0, 1);
            if (!Def.Flying) Model.Position = new Vector3(0, Mathf.Lerp(-2.5f * Def.Size, 0, k), 0);
            if (k >= 1) State = EnemyState.Moving;
            Model.Animate(dt, 3f, 0);
            return;
        }
        if (Def.Static) { Model.Animate(dt, 0, 0); return; }

        _attackCd -= dt;
        _specialCd -= dt;
        _attackAnim = Mathf.Max(0, _attackAnim - dt * 3f);
        if (!_world.NearestPlayer(GlobalPosition, out var peer, out var target))
        {
            _speedNow = Mathf.Lerp(_speedNow, 0, dt * 4);
            Model.Animate(dt, _speedNow, _attackAnim);
            return;
        }

        if (Def.Flying) TickFlying(dt, peer, target);
        else TickGround(dt, peer, target);
        Model.Animate(dt, _speedNow, _attackAnim);
    }

    private void TickGround(float dt, long peer, Vector3 target)
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

        if (attacking && _attackCd <= 0)
        {
            _attackCd = Def.AttackCooldown * (float)GD.RandRange(0.85, 1.15);
            _attackAnim = 1f;
            if (Def.Ranged && (!Def.Boss || dist > 10f))
            {
                var from = Center + Basis.Z * 1.5f * Def.Size;
                _world.HostEnemyProjectile(this, 0, from, target + Vector3.Up, Def.Boss ? 3 : 1);
                Net.I.BroadcastEnemyAttack(Id, 1, target);
            }
            else
            {
                Net.I.DamagePlayer(peer, Def.Damage * _world.DamageMul, pos);
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

    private void TickFlying(float dt, long peer, Vector3 target)
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
            _world.HostEnemyProjectile(this, 1, pos + Vector3.Down * 0.8f, target + Vector3.Up * 1f, 1);
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
        Model.Animate(dt, _speedNow, _attackAnim);
    }

    public void PlayAttack() => _attackAnim = 1f;

    // ---------------- Mort ----------------

    public void Die()
    {
        if (State == EnemyState.Dead) return;
        State = EnemyState.Dead;
        CollisionLayer = 0;
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
        Model.Flash();
    }
}
