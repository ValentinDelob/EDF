using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Débris physiques recyclés (morceaux de mobilier, panneaux de carrosserie, blocs de façade) : au plus
/// <see cref="Max"/> corps rigides simultanés, créés une fois par mission. Un morceau inactif est retiré de la
/// simulation (mode désactivé) ; quand tout est pris, le plus ancien est recyclé. Couleur par instance
/// (un seul matériau), boîte unitaire mise à l'échelle. Effet local à chaque pair (aucun réseau).
/// </summary>
public partial class DebrisPool : Node3D
{
    public const int Max = 120;

    private sealed class Piece
    {
        public RigidBody3D B;
        public MeshInstance3D M;
        public BoxShape3D S;
        public Vector3 Size;
        public float Age, Life;
        public bool Active;
        public ulong Serial;
    }

    private static DebrisPool _current;
    private static ShaderMaterial _mat;
    private static BoxMesh _box;

    /// <summary>Maillage des morceaux (matériau compris), pour le préchauffage des shaders.</summary>
    public static Mesh PieceMesh => _box;
    private readonly List<Piece> _pieces = new();
    private ulong _serial;
    private World _world;

    public static DebrisPool For(World world)
    {
        if (_current != null && IsInstanceValid(_current) && _current._world == world) return _current;
        _current = new DebrisPool { _world = world, Name = "DebrisPool" };
        world.FxRoot.AddChild(_current);
        return _current;
    }

    /// <summary>Morceaux actifs (journal des tests).</summary>
    public static int ActiveCount
    {
        get
        {
            if (_current == null || !IsInstanceValid(_current)) return 0;
            var n = 0;
            foreach (var p in _current._pieces) if (p.Active) n++;
            return n;
        }
    }

    private const string Code = @"
shader_type spatial;
render_mode cull_back;
instance uniform vec4 tint : source_color = vec4(0.5, 0.5, 0.5, 1.0);
varying vec3 lp;
void vertex() { lp = VERTEX; }
float h(vec3 p) { return fract(sin(dot(floor(p), vec3(12.9898, 78.233, 37.719))) * 43758.5453); }
void fragment() {
    // Grain irrégulier (cassure) : variation de valeur par petites facettes.
    float n = h(lp * 9.0) * 0.6 + h(lp * 23.0) * 0.4;
    ALBEDO = tint.rgb * (0.78 + 0.3 * n);
    ROUGHNESS = mix(0.95, 0.45, tint.a < 0.5 ? 1.0 : 0.0);
    METALLIC = tint.a < 0.5 ? 0.55 : 0.0;
}";

    public override void _Ready()
    {
        _mat ??= new ShaderMaterial { Shader = new Shader { Code = Code } };
        _box ??= new BoxMesh { Size = Vector3.One, Material = _mat };
        for (var i = 0; i < Max; i++)
        {
            var shape = new BoxShape3D { Size = Vector3.One * 0.3f };
            var b = new RigidBody3D
            {
                CollisionLayer = 1 << 4,
                CollisionMask = 1 | (1 << 4),
                ContinuousCd = false,
                CanSleep = true,
                ProcessMode = ProcessModeEnum.Disabled,
                Visible = false,
                PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.9f, Bounce = 0.15f },
                AngularDamp = 0.6f,
                LinearDamp = 0.05f,
            };
            var m = new MeshInstance3D { Mesh = _box, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            b.AddChild(m);
            b.AddChild(new CollisionShape3D { Shape = shape });
            AddChild(b);
            b.Position = new Vector3(0, -400, 0);
            _pieces.Add(new Piece { B = b, M = m, S = shape });
        }
    }

    /// <summary>
    /// Lance un morceau (boîte <paramref name="size"/>) depuis <paramref name="xf"/>. Couleur : alpha &lt; 0,5 = métal.
    /// </summary>
    public void Spawn(Transform3D xf, Vector3 size, Vector3 vel, Vector3 spin, Color color, float life = 8f)
    {
        Piece pick = null;
        foreach (var p in _pieces)
        {
            if (!p.Active) { pick = p; break; }
            if (pick == null || p.Serial < pick.Serial) pick = p;
        }
        if (pick == null) return;
        size = size.Clamp(Vector3.One * 0.04f, Vector3.One * 4f);
        pick.Active = true;
        pick.Serial = ++_serial;
        pick.Age = 0;
        pick.Life = life;
        pick.Size = size;
        pick.S.Size = size;
        pick.M.Scale = size;
        pick.M.SetInstanceShaderParameter("tint", color);
        pick.M.CastShadow = size.X * size.Y * size.Z > 0.15f ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off;
        pick.B.Mass = Mathf.Clamp(size.X * size.Y * size.Z * 600f, 2f, 3000f);
        pick.B.ProcessMode = ProcessModeEnum.Inherit;
        pick.B.Visible = true;
        pick.B.Sleeping = false;
        pick.B.GlobalTransform = new Transform3D(xf.Basis.Orthonormalized(), xf.Origin);
        pick.B.LinearVelocity = vel;
        pick.B.AngularVelocity = spin;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        foreach (var p in _pieces)
        {
            if (!p.Active) continue;
            p.Age += dt;
            // Fin de vie : le morceau rétrécit (poussière) puis retourne à la réserve.
            var left = p.Life - p.Age;
            if (left < 1f) p.M.Scale = p.Size * Mathf.Max(left, 0.01f);
            if (left > 0 && p.B.GlobalPosition.Y > -30f) continue;
            p.Active = false;
            p.B.ProcessMode = ProcessModeEnum.Disabled;
            p.B.Visible = false;
            p.B.Position = new Vector3(0, -400, 0);
        }
    }
}
