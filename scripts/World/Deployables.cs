using Godot;

namespace Rempart;

/// <summary>Tourelle automatique : visible par tous, ne tire réellement que chez son propriétaire.</summary>
public partial class Turret : Node3D
{
    public bool Owned;
    public float Damage;
    public float Range;
    public float Life;
    private World _world;
    private Node3D _head;
    private Node3D _muzzle;
    private float _cd;
    private Enemy _target;
    private float _retarget;

    public static Turret Create(World world, Vector3 pos, float yaw, bool owned, float damage, float range, float life)
    {
        var t = new Turret { _world = world, Owned = owned, Damage = damage, Range = range, Life = life, Position = pos };
        var metal = Mats.Metal(new Color(0.32f, 0.34f, 0.3f), 0.5f);
        foreach (var a in new[] { 0f, 2.1f, 4.2f })
            t.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = 1.3f, Material = metal },
                Position = new Vector3(Mathf.Cos(a) * 0.35f, 0.5f, Mathf.Sin(a) * 0.35f),
                RotationDegrees = new Vector3(Mathf.Sin(a) * 25, 0, -Mathf.Cos(a) * 25),
            });
        t._head = new Node3D { Position = new Vector3(0, 1.15f, 0), Rotation = new Vector3(0, yaw + Mathf.Pi, 0) };
        t.AddChild(t._head);
        t._head.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.4f, 0.7f), Material = metal } });
        t._head.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.05f, Height = 0.8f, Material = Mats.Metal(new Color(0.1f, 0.1f, 0.1f)) }, Position = new Vector3(0, 0.05f, 0.7f), RotationDegrees = new Vector3(90, 0, 0) });
        t._head.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.06f, 0.06f), Material = Mats.Glow(new Color(0.3f, 1f, 0.4f), 3f) }, Position = new Vector3(0, 0.22f, -0.2f) });
        t._muzzle = new Node3D { Position = new Vector3(0, 0.05f, 1.1f) };
        t._head.AddChild(t._muzzle);
        return t;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        Life -= dt;
        if (Life <= 0)
        {
            Effects.Smoke(_world, GlobalPosition + Vector3.Up, 1.5f, 2f);
            QueueFree();
            return;
        }
        _retarget -= dt;
        if (_retarget <= 0 || _target == null || !IsInstanceValid(_target) || _target.State == EnemyState.Dead)
        {
            _retarget = 0.4f;
            _target = _world.NearestEnemy(GlobalPosition, Range);
        }
        if (_target == null) return;
        var to = _target.Center - _head.GlobalPosition;
        var yaw = Mathf.Atan2(to.X, to.Z);
        _head.GlobalRotation = new Vector3(0, Mathf.LerpAngle(_head.GlobalRotation.Y, yaw, Mathf.Clamp(dt * 8f, 0, 1)), 0);
        _cd -= dt;
        if (_cd > 0) return;
        _cd = 1f / 9f;
        var from = _muzzle.GlobalPosition;
        var end = _target.Center + new Vector3((float)GD.RandRange(-0.5, 0.5), (float)GD.RandRange(-0.5, 0.5), (float)GD.RandRange(-0.5, 0.5));
        Effects.Tracer(_world.FxRoot, from, end, new Color(1f, 0.75f, 0.35f), 0.04f);
        Sfx.At("smg", from, -8f, 1.1f, 60);
        if (Owned)
        {
            Net.I.SendDamageEnemy(_target.Id, Damage, end, false);
            _world.LocalHitFx(_target, end);
        }
    }
}

/// <summary>Champ de soin : chaque pair soigne son propre soldat s'il est dedans.</summary>
public partial class HealField : Node3D
{
    public float Power;
    public float Radius;
    public float Life;
    private World _world;
    private float _tick;

    public static HealField Create(World world, Vector3 pos, float power, float radius, float life)
    {
        var h = new HealField { _world = world, Power = power, Radius = radius, Life = life, Position = pos };
        h.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.08f, RadialSegments = 32, Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.3f, 1f, 0.5f, 0.18f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            } },
            Position = new Vector3(0, 0.25f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        h.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.25f, Height = 0.8f, Material = Mats.Painted(new Color(0.9f, 0.9f, 0.88f), 0.4f) }, Position = new Vector3(0, 0.4f, 0) });
        h.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.08f, 0.08f), Material = Mats.Glow(new Color(0.3f, 1f, 0.5f), 3f) }, Position = new Vector3(0, 0.85f, 0) });
        h.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.3f, 0.08f), Material = Mats.Glow(new Color(0.3f, 1f, 0.5f), 3f) }, Position = new Vector3(0, 0.85f, 0) });
        h.AddChild(new OmniLight3D { LightColor = new Color(0.3f, 1f, 0.5f), LightEnergy = 1.2f, OmniRange = radius, Position = new Vector3(0, 1.5f, 0) });
        return h;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        Life -= dt;
        if (Life <= 0) { QueueFree(); return; }
        _tick -= dt;
        if (_tick > 0) return;
        _tick = 0.5f;
        var p = _world.LocalPlayer;
        if (p != null && !p.Downed && p.GlobalPosition.DistanceTo(GlobalPosition) <= Radius)
        {
            p.Heal(p.MaxHealth * Power / 100f * 0.5f);
        }
        Effects.Ring(_world, GlobalPosition, Radius * 0.6f, new Color(0.3f, 1f, 0.5f), 0.5f);
    }
}
