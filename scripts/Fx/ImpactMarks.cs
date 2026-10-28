using Godot;

namespace Rempart;

/// <summary>
/// Impacts de balles sur le décor (un nœud par mission) : petit cratère sombre cerclé d'éclats clairs, plaqué sur la surface
/// touchée (murs, sol, mobilier), qui s'efface en 30 s. Un seul MultiMesh (anneau de 600 impacts, le plus ancien est
/// réutilisé), aucun nœud par impact ; les impacts d'un immeuble qui s'effondre disparaissent avec lui (<see cref="ClearInside"/>).
/// </summary>
public partial class ImpactMarks : MultiMeshInstance3D
{
    private const int Capacity = 600;
    private const float Life = 30f;
    private int _next, _count;
    private float _now;
    private ShaderMaterial _mat;
    private readonly Vector3[] _pos = new Vector3[Capacity];

    private const string Code = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back;
uniform float now = 0.0;
uniform float life = 30.0;
varying float born;
varying float seed;
void vertex() {
    born = INSTANCE_CUSTOM.r;
    seed = INSTANCE_CUSTOM.g;
}
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float a = atan(p.y, p.x);
    // Bord irrégulier (éclats) : rayon modulé par l'angle et la graine de l'impact.
    float r = length(p) * (1.0 + 0.22 * sin(a * 5.0 + seed * 6.28) + 0.12 * sin(a * 11.0 + seed * 17.0));
    float hole = smoothstep(0.3, 0.18, r);
    float rim = smoothstep(0.75, 0.35, r) * (1.0 - hole);
    float age = clamp((now - born) / life, 0.0, 1.0);
    float fade = 1.0 - age * age;
    ALBEDO = mix(vec3(0.55, 0.53, 0.5), vec3(0.03, 0.028, 0.025), hole);
    ROUGHNESS = 0.95;
    ALPHA = (hole * 0.95 + rim * 0.35) * fade;
}";

    public static ImpactMarks For(World world)
    {
        if (world.GetNodeOrNull<ImpactMarks>("ImpactMarks") is { } m) return m;
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new QuadMesh { Size = new Vector2(1, 1) },
            InstanceCount = Capacity,
            VisibleInstanceCount = 0,
        };
        m = new ImpactMarks { Name = "ImpactMarks", Multimesh = mm, CastShadow = ShadowCastingSetting.Off };
        m._mat = new ShaderMaterial { Shader = new Shader { Code = Code } };
        m._mat.SetShaderParameter("life", Life);
        m.MaterialOverride = m._mat;
        m.CustomAabb = new Aabb(new Vector3(-600, -50, -600), new Vector3(1200, 400, 1200));
        world.AddChild(m);
        return m;
    }

    /// <summary>Impact en <paramref name="at"/> sur une surface de normale <paramref name="normal"/>, de diamètre <paramref name="size"/> m.</summary>
    public void Add(Vector3 at, Vector3 normal, float size)
    {
        if (normal.LengthSquared() < 0.5f) return;
        var n = normal.Normalized();
        var up = Mathf.Abs(n.Y) > 0.95f ? Vector3.Forward : Vector3.Up;
        var x = up.Cross(n).Normalized();
        var y = n.Cross(x);
        // Rotation aléatoire autour de la normale, quad plaqué 1,5 cm devant la surface.
        var a = GD.Randf() * Mathf.Tau;
        var bx = (x * Mathf.Cos(a) + y * Mathf.Sin(a)) * size;
        var by = (y * Mathf.Cos(a) - x * Mathf.Sin(a)) * size;
        var mm = Multimesh;
        _pos[_next] = at;
        mm.SetInstanceTransform(_next, new Transform3D(new Basis(bx, by, n), at + n * 0.015f));
        mm.SetInstanceCustomData(_next, new Color(_now, GD.Randf(), 0, 0));
        _next = (_next + 1) % Capacity;
        _count = Mathf.Min(_count + 1, Capacity);
        mm.VisibleInstanceCount = _count;
    }

    /// <summary>Retire les impacts contenus dans une boîte (immeuble qui s'effondre).</summary>
    public void ClearInside(Aabb box)
    {
        var mm = Multimesh;
        var grown = box.Grow(0.5f);
        for (var i = 0; i < _count; i++)
            if (grown.HasPoint(_pos[i])) mm.SetInstanceTransform(i, new Transform3D(new Basis(Vector3.Zero, Vector3.Zero, Vector3.Zero), _pos[i]));
    }

    public override void _Process(double delta)
    {
        _now += (float)delta;
        _mat.SetShaderParameter("now", _now);
    }
}
