using Godot;

namespace Rempart;

/// <summary>
/// Marques au sol et sur le décor (un nœud par mission) : impacts de balles (petit cratère sombre cerclé d'éclats clairs, 30 s)
/// et flaques d'hémolymphe laissées par les insectes abattus (tache luisante à lobes et gouttelettes, 45 s). Un seul MultiMesh
/// (anneau de 600 marques, la plus ancienne est réutilisée), aucun nœud par marque ; les marques d'un immeuble qui s'effondre
/// disparaissent avec lui (<see cref="ClearInside"/>).
/// </summary>
public partial class ImpactMarks : MultiMeshInstance3D
{
    private const int Capacity = 600;
    private int _next, _count;
    private float _now;
    private ShaderMaterial _mat;
    private readonly Vector3[] _pos = new Vector3[Capacity];

    private const string Code = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back;
uniform float now = 0.0;
varying float born;
varying float seed;
varying float kind;
void vertex() {
    born = INSTANCE_CUSTOM.r;
    seed = INSTANCE_CUSTOM.g;
    kind = INSTANCE_CUSTOM.b;
}
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float a = atan(p.y, p.x);
    // Durée : 30 s pour un impact, 45 s pour une flaque.
    float age = clamp((now - born) / (kind < 0.5 ? 30.0 : 45.0), 0.0, 1.0);
    float fade = 1.0 - age * age;
    if (kind < 0.5) {
        // Impact : bord irrégulier (éclats), rayon modulé par l'angle et la graine.
        float r = length(p) * (1.0 + 0.22 * sin(a * 5.0 + seed * 6.28) + 0.12 * sin(a * 11.0 + seed * 17.0));
        float hole = smoothstep(0.3, 0.18, r);
        float rim = smoothstep(0.75, 0.35, r) * (1.0 - hole);
        ALBEDO = mix(vec3(0.55, 0.53, 0.5), vec3(0.03, 0.028, 0.025), hole);
        ROUGHNESS = 0.95;
        ALPHA = (hole * 0.95 + rim * 0.35) * fade;
    } else {
        // Flaque : grande tache à lobes, gouttelettes autour, bord plus sombre ; luisante, elle sèche et s'efface.
        float r = length(p) * (1.0 + 0.28 * sin(a * 3.0 + seed * 6.28) + 0.16 * sin(a * 7.0 + seed * 13.0) + 0.08 * sin(a * 17.0 + seed * 3.0));
        float body = smoothstep(0.62, 0.52, r);
        vec2 q = p * 4.0 + seed * 9.0;
        vec2 cell = floor(q);
        vec2 f = fract(q) - 0.5;
        float h = fract(sin(dot(cell, vec2(12.9898, 78.233)) + seed * 7.0) * 43758.5453);
        float drop = step(0.72, h) * smoothstep(0.22, 0.12, length(f)) * smoothstep(1.0, 0.55, length(p));
        float cov = max(body, drop);
        float edge = smoothstep(0.35, 0.58, r) * body;
        // Teinte (kind) : 1 hémolymphe ambrée, 2 acide vert-jaune, 3 terre du nid.
        vec3 tint = kind < 1.5 ? vec3(0.42, 0.3, 0.06) : kind < 2.5 ? vec3(0.36, 0.5, 0.05) : vec3(0.3, 0.24, 0.17);
        ALBEDO = tint * mix(0.45, 0.22, edge);
        ROUGHNESS = mix(0.35, 0.8, age);
        SPECULAR = 0.3;
        ALPHA = cov * 0.9 * fade;
    }
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
        m.MaterialOverride = m._mat;
        m.CustomAabb = new Aabb(new Vector3(-600, -50, -600), new Vector3(1200, 400, 1200));
        world.AddChild(m);
        return m;
    }

    /// <summary>Impact en <paramref name="at"/> sur une surface de normale <paramref name="normal"/>, de diamètre <paramref name="size"/> m.</summary>
    public void Add(Vector3 at, Vector3 normal, float size) => Put(at, normal, size, 0);

    /// <summary>
    /// Flaque sur le sol en <paramref name="at"/>, de diamètre <paramref name="size"/> m ; <paramref name="tint"/> : 1 hémolymphe
    /// ambrée, 2 acide vert-jaune, 3 terre du nid.
    /// </summary>
    public void Splat(Vector3 at, float size, int tint) => Put(at, Vector3.Up, size, Mathf.Clamp(tint, 1, 3));

    private void Put(Vector3 at, Vector3 normal, float size, int kind)
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
        mm.SetInstanceTransform(_next, new Transform3D(new Basis(bx, by, n), at + n * (kind == 0 ? 0.015f : 0.1f)));
        mm.SetInstanceCustomData(_next, new Color(_now, GD.Randf(), kind, 0));
        _next = (_next + 1) % Capacity;
        _count = Mathf.Min(_count + 1, Capacity);
        mm.VisibleInstanceCount = _count;
    }

    /// <summary>Retire les marques posées sur un volume (immeuble qui s'effondre) : boîte <paramref name="size"/> (base au sol)
    /// dans le repère <paramref name="xf"/>, à 0,5 m près.</summary>
    public void ClearInside(Transform3D xf, Vector3 size)
    {
        var mm = Multimesh;
        var inv = xf.AffineInverse();
        for (var i = 0; i < _count; i++)
        {
            var l = inv * _pos[i];
            if (Mathf.Abs(l.X) <= size.X / 2 + 0.5f && Mathf.Abs(l.Z) <= size.Z / 2 + 0.5f && l.Y >= -0.5f && l.Y <= size.Y + 0.5f)
                mm.SetInstanceTransform(i, new Transform3D(new Basis(Vector3.Zero, Vector3.Zero, Vector3.Zero), _pos[i]));
        }
    }

    public override void _Process(double delta)
    {
        _now += (float)delta;
        _mat.SetShaderParameter("now", _now);
    }
}
