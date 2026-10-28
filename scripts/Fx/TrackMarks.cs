using Godot;

namespace Rempart;

/// <summary>
/// Traces de chenilles au sol (un nœud par mission, partagé par tous les chars) : empreintes de patins posées tous les
/// 0,8 m sous chaque chenille, plaquées sur le sol sondé, qui s'effacent en 40 s. Un seul MultiMesh (anneau de 720
/// empreintes, la plus ancienne est réutilisée), matériau transparent sans écriture de profondeur ; aucun nœud par empreinte.
/// </summary>
public partial class TrackMarks : MultiMeshInstance3D
{
    private const int Capacity = 720;
    private const float Life = 40f;
    private int _next;
    private float _now;
    private ShaderMaterial _mat;

    private const string Code = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, specular_disabled;
uniform float now = 0.0;
uniform float life = 40.0;
varying float born;
void vertex() {
    born = INSTANCE_CUSTOM.r;
}
void fragment() {
    float age = clamp((now - born) / life, 0.0, 1.0);
    // Patins : barres en travers de la chenille, bords irréguliers, plus marqués au centre de la bande.
    float bar = smoothstep(0.32, 0.42, abs(fract(UV.y * 5.0) - 0.5));
    float edge = smoothstep(0.0, 0.12, UV.x) * smoothstep(1.0, 0.88, UV.x);
    float a = (0.28 + 0.32 * bar) * edge * (1.0 - age) * (1.0 - age);
    ALBEDO = vec3(0.07, 0.065, 0.055);
    ROUGHNESS = 0.95;
    ALPHA = a * COLOR.a;
}";

    public static TrackMarks For(World world)
    {
        if (world.GetNodeOrNull<TrackMarks>("TrackMarks") is { } t) return t;
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            UseColors = true,
            Mesh = new PlaneMesh { Size = new Vector2(1, 1) },
            InstanceCount = Capacity,
            VisibleInstanceCount = 0,
        };
        t = new TrackMarks { Name = "TrackMarks", Multimesh = mm, CastShadow = ShadowCastingSetting.Off };
        t._mat = new ShaderMaterial { Shader = new Shader { Code = Code } };
        t._mat.SetShaderParameter("life", Life);
        t.MaterialOverride = t._mat;
        // Grande boîte englobante : les empreintes couvrent toute la carte.
        t.CustomAabb = new Aabb(new Vector3(-600, -50, -600), new Vector3(1200, 200, 1200));
        world.AddChild(t);
        return t;
    }

    /// <summary>Empreinte centrée en <paramref name="at"/> (sur le sol), alignée sur <paramref name="forward"/>, de normale <paramref name="up"/>.</summary>
    public void Stamp(Vector3 at, Vector3 forward, Vector3 up, float width, float length)
    {
        var mm = Multimesh;
        var z = (forward - up * forward.Dot(up)).Normalized();
        var x = up.Cross(z).Normalized();
        mm.SetInstanceTransform(_next, new Transform3D(new Basis(x * width, up, z * length), at + up * 0.025f));
        mm.SetInstanceCustomData(_next, new Color(_now, 0, 0, 0));
        mm.SetInstanceColor(_next, Colors.White);
        _next = (_next + 1) % Capacity;
        if (mm.VisibleInstanceCount < Capacity) mm.VisibleInstanceCount = Mathf.Max(mm.VisibleInstanceCount, _next == 0 ? Capacity : _next);
    }

    public override void _Process(double delta)
    {
        _now += (float)delta;
        _mat.SetShaderParameter("now", _now);
    }
}
