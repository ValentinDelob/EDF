using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Zone de feu des napalms d'EDF 5 : nappe au sol (Volatile / Volcanic Napalm) ou colonne verticale (Flame Geyser).
/// Brûle pendant <c>burn</c> secondes ; un ennemi qui y reste tout ce temps reçoit les « dégâts totaux » de l'arme.
/// Seul le tireur (local) inflige les dégâts ; ailleurs, l'effet est visuel.
/// </summary>
public partial class FireField : Node3D
{
    private World _world;
    private float _radius, _burn, _left, _total, _tick;
    private bool _local, _column;
    private GpuParticles3D _fx;
    private OmniLight3D _light;

    public static void Spawn(World w, Vector3 at, Vector3 normal, float radius, float burn, float total, bool local, bool column)
    {
        var f = new FireField { _world = w, _radius = radius, _burn = burn, _left = burn, _total = total, _local = local, _column = column };
        w.FxRoot.AddChild(f);
        f.GlobalPosition = at + normal * 0.1f;
        f.Build();
        Sfx.At("explosion_small", at, -6f, 0.7f);
    }

    private void Build()
    {
        var g = new Gradient();
        g.SetColor(0, new Color(1f, 0.85f, 0.4f, 0.95f));
        g.AddPoint(0.35f, new Color(1f, 0.45f, 0.1f, 0.8f));
        g.SetColor(g.GetPointCount() - 1, new Color(0.2f, 0.15f, 0.12f, 0f));
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = _column ? ParticleProcessMaterial.EmissionShapeEnum.Sphere : ParticleProcessMaterial.EmissionShapeEnum.Ring,
            EmissionSphereRadius = _radius * 0.5f,
            EmissionRingAxis = Vector3.Up, EmissionRingRadius = _radius * 0.55f, EmissionRingInnerRadius = 0f, EmissionRingHeight = 0.2f,
            Direction = Vector3.Up, Spread = _column ? 6f : 25f,
            InitialVelocityMin = _column ? 22f : 1.5f, InitialVelocityMax = _column ? 34f : 4f,
            Gravity = new Vector3(0, _column ? -2f : 1.5f, 0),
            ScaleMin = 1.2f, ScaleMax = 2.4f,
            ColorRamp = new GradientTexture1D { Gradient = g },
        };
        var tex = new GradientTexture2D
        {
            Gradient = new Gradient { Colors = new[] { Colors.White, new Color(1, 1, 1, 0) } },
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 32, Height = 32,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = tex, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, VertexColorUseAsAlbedo = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        _fx = new GpuParticles3D
        {
            Amount = _column ? 220 : Mathf.Clamp((int)(_radius * 26f), 60, 600), Lifetime = _column ? 1.6f : 1.1f, ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = new Vector2(1.4f, 1.4f), Material = mat }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-40, -5, -40), new Vector3(80, 80, 80)),
        };
        AddChild(_fx);
        _light = new OmniLight3D { LightColor = new Color(1f, 0.55f, 0.2f), LightEnergy = 3f, OmniRange = Mathf.Max(_radius * 2.5f, 10f), Position = Vector3.Up * 1.5f };
        AddChild(_light);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;
        _left -= dt;
        if (_left <= 0)
        {
            _fx.Emitting = false;
            if (_left < -1.8f) QueueFree();
            return;
        }
        _light.LightEnergy = 2.5f + Mathf.Sin(_left * 23f) * 0.6f;
        if (!_local) return;
        _tick += dt;
        if (_tick < 0.25f) return;
        var share = _total * _tick / _burn;
        _tick = 0;
        var c = GlobalPosition;
        foreach (var e in _world.EnemiesById.Values.ToList())
        {
            if (e.State == EnemyState.Dead) continue;
            var p = e.Center;
            var dh = new Vector2(p.X - c.X, p.Z - c.Z).Length() - e.Def.Size * 1.2f;
            var inside = _column ? dh < _radius && p.Y > c.Y - 2f && p.Y < c.Y + 45f : dh < _radius && Mathf.Abs(p.Y - c.Y) < 4f + e.Def.Size;
            if (!inside) continue;
            Net.I.SendDamageEnemy(e.Id, share, p, false);
        }
    }
}
