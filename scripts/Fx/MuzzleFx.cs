using System;
using Godot;

namespace Rempart;

/// <summary>
/// Flamme de bouche et traçantes (EDF) : étoile de feu face à la caméra (cœur blanc, cinq branches, roulis tiré au hasard) et, si la
/// direction du tir est connue, deux pétales de flamme croisés le long du canon. Matériau additif partagé (paramètres par
/// instance : teinte, roulis, ouverture), textures générées une fois ; lumière brève et courte (aucune ombre colorée au sol).
/// </summary>
public static class MuzzleFx
{
    private static Shader _shader;
    private static ShaderMaterial _star, _petal;
    private static QuadMesh _quad, _petalQuad;

    private const string Code = @"
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D tex : source_color, filter_linear_mipmap, repeat_disable;
uniform bool billboard = false;
instance uniform vec3 tint = vec3(1.0, 0.7, 0.3);
instance uniform float roll = 0.0;
instance uniform float fade = 1.0;
instance uniform float gain = 1.0;
void vertex() {
    if (billboard) {
        float c = cos(roll), s = sin(roll);
        VERTEX.xy = mat2(vec2(c, s), vec2(-s, c)) * VERTEX.xy;
        float k = length(MODEL_MATRIX[0].xyz);
        MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * k, INV_VIEW_MATRIX[1] * k, INV_VIEW_MATRIX[2] * k, MODEL_MATRIX[3]);
    }
}
void fragment() {
    vec4 t = texture(tex, UV);
    // Cœur presque blanc, bords à la teinte du tir ; au-delà de 1 : la lueur (bloom) prend le relais.
    vec3 col = mix(tint, vec3(1.0, 0.97, 0.9), t.r * t.r) * (1.5 + 3.0 * t.r);
    ALBEDO = col * t.a * fade * gain;
}";

    private static ShaderMaterial Material(bool billboard, ImageTexture tex)
    {
        _shader ??= new Shader { Code = Code };
        var m = new ShaderMaterial { Shader = _shader, RenderPriority = 10 };
        m.SetShaderParameter("billboard", billboard);
        m.SetShaderParameter("tex", tex);
        return m;
    }

    /// <summary>Étoile : R = chaleur (cœur), A = couverture ; cinq branches irrégulières autour d'un disque.</summary>
    private static ImageTexture StarTexture()
    {
        const int n = 128;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        var rng = new Random(7);
        var len = new float[5];
        for (var i = 0; i < 5; i++) len[i] = 0.65f + (float)rng.NextDouble() * 0.35f;
        for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var px = (x + 0.5f) / n * 2f - 1f;
                var py = (y + 0.5f) / n * 2f - 1f;
                var r = Mathf.Sqrt(px * px + py * py);
                var a = Mathf.Atan2(py, px);
                var core = Mathf.Exp(-r * r * 22f);
                var halo = Mathf.Exp(-r * r * 5f) * 0.45f;
                var spikes = 0f;
                for (var i = 0; i < 5; i++)
                {
                    var d = Mathf.Abs(Mathf.AngleDifference(a, i * Mathf.Tau / 5f + 0.3f));
                    var w = 0.05f + 0.22f * (1f - r / len[i]);
                    if (r < len[i] && d < w) spikes = Mathf.Max(spikes, (1f - d / w) * (1f - r / len[i]));
                }
                var cov = Mathf.Clamp(core + halo + spikes * 0.9f, 0f, 1f);
                var heat = Mathf.Clamp(core * 1.2f + spikes * 0.35f, 0f, 1f);
                img.SetPixel(x, y, new Color(heat, heat, heat, cov));
            }
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Pétale de flamme vu de côté (U le long du canon, de la bouche vers l'avant) : lobes qui s'effilent.</summary>
    private static ImageTexture PetalTexture()
    {
        const int w = 128, h = 64;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var u = (x + 0.5f) / w;
                var v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                // Silhouette : renflement près de la bouche, deux lobes, pointe effilée.
                var width = 0.85f * Mathf.Pow(1f - u, 0.7f) * (0.75f + 0.25f * Mathf.Cos(u * 13f)) * Mathf.Clamp(u * 9f, 0.25f, 1f);
                var cov = Mathf.Clamp((width - v) / Mathf.Max(width * 0.6f, 0.02f), 0f, 1f) * Mathf.Pow(1f - u, 0.8f);
                var heat = Mathf.Clamp((1f - u * 1.6f) * (1f - v / Mathf.Max(width, 0.05f)), 0f, 1f);
                img.SetPixel(x, y, new Color(heat, heat, heat, cov));
            }
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Traînée de traçante : gaussienne en travers, tête brillante (U = 1), queue qui s'efface (U = 0).</summary>
    private static ImageTexture StreakTexture()
    {
        const int w = 64, h = 16;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var u = (x + 0.5f) / w;
                var v = (y + 0.5f) / h * 2f - 1f;
                var across = Mathf.Exp(-v * v * 9f);
                var along = Mathf.Pow(u, 1.6f) * Mathf.Clamp((1f - u) * 14f, 0f, 1f);
                var heat = Mathf.Clamp(Mathf.Exp(-v * v * 40f) * Mathf.Pow(u, 3f), 0f, 1f);
                img.SetPixel(x, y, new Color(heat, heat, heat, across * along));
            }
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    private static ShaderMaterial _streak;
    private static ArrayMesh _bulletMesh;

    /// <summary>
    /// Maillage d'une balle visible (MultiMesh des balles d'EDF 5, couleur par instance) : deux plans croisés le long de −Z
    /// (queue en Z = +0,5, tête en Z = −0,5), traînée douce à tête brillante ; remplace le pavé plein.
    /// </summary>
    public static ArrayMesh BulletMesh()
    {
        if (_bulletMesh != null) return _bulletMesh;
        var tex = BulletTexture();
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = tex,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
        };
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        void Quad(Vector3 a)
        {
            // a : demi-largeur du plan (axe X ou Y) ; la largeur visible vaut environ la moitié (dégradé en travers).
            Vector3[] p = { a + Vector3.Back * 0.5f, -a + Vector3.Back * 0.5f, -a + Vector3.Forward * 0.5f, a + Vector3.Forward * 0.5f };
            Vector2[] uv = { new(0, 0), new(0, 1), new(1, 1), new(1, 0) };
            foreach (var i in new[] { 0, 1, 2, 0, 2, 3 })
            {
                st.SetUV(uv[i]);
                st.AddVertex(p[i]);
            }
        }
        Quad(Vector3.Right);
        Quad(Vector3.Up);
        st.SetMaterial(mat);
        _bulletMesh = st.Commit();
        return _bulletMesh;
    }

    /// <summary>Traînée de balle : blanc au cœur (U = 1 tête), couverture gaussienne en travers et qui s'efface vers la queue.</summary>
    private static ImageTexture BulletTexture()
    {
        const int w = 64, h = 16;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var u = (x + 0.5f) / w;
                var v = (y + 0.5f) / h * 2f - 1f;
                var a = Mathf.Exp(-v * v * 7f) * Mathf.Pow(u, 1.3f) * Mathf.Clamp((1f - u) * 12f, 0f, 1f);
                var core = Mathf.Exp(-v * v * 30f) * u;
                var c = 0.75f + 0.25f * core;
                img.SetPixel(x, y, new Color(c, c, c, Mathf.Clamp(a * 1.4f, 0f, 1f)));
            }
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    private static void Init()
    {
        if (_star != null) return;
        _star = Material(true, StarTexture());
        _petal = Material(false, PetalTexture());
        _streak = Material(false, StreakTexture());
        _quad = new QuadMesh { Size = new Vector2(1, 1) };
        // Pétale : de la bouche (X = 0, U = 0) vers l'avant (X = 1), centré sur l'axe du canon.
        _petalQuad = new QuadMesh { Size = new Vector2(1, 1), CenterOffset = new Vector3(0.5f, 0, 0) };
    }

    /// <summary>
    /// Traçante : courte traînée lumineuse (deux plans croisés, additifs) qui file de <paramref name=from/> à <paramref name=to/>
    /// à ~650 m/s, comme les balles visibles d'EDF ; <paramref name=width/> = épaisseur.
    /// </summary>
    public static void Tracer(Node3D parent, Vector3 from, Vector3 to, Color color, float width)
    {
        Init();
        var dist = from.DistanceTo(to);
        if (dist < 0.5f) return;
        var d = (to - from) / dist;
        var len = Mathf.Min(dist * 0.6f, 4f + width * 30f);
        var up = Mathf.Abs(d.Y) > 0.95f ? Vector3.Right : Vector3.Up;
        var side = d.Cross(up).Normalized();
        var upv = side.Cross(d).Normalized();
        var root = new Node3D();
        parent.AddChild(root);
        var tint = new Vector3(color.R, color.G, color.B);
        for (var i = 0; i < 2; i++)
        {
            var y = i == 0 ? upv : side;
            var q = new MeshInstance3D { Mesh = _petalQuad, MaterialOverride = _streak, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            root.AddChild(q);
            // Quad de X = 0 (queue) à X = 1 (tête) : la tête est au bout, côté cible.
            q.Basis = new Basis(d * len, y * width * 3f, d.Cross(y));
            q.Position = -d * len;
            q.SetInstanceShaderParameter("tint", tint);
            // Traînée : lueur bien plus faible qu'une flamme de bouche (sinon une planche blanche de près).
            q.SetInstanceShaderParameter("gain", 0.3f);
        }
        var time = Mathf.Max(0.03f, dist / 650f);
        root.GlobalPosition = from + d * Mathf.Min(len, dist);
        var tw = root.CreateTween();
        tw.TweenProperty(root, "global_position", to, time);
        tw.TweenCallback(Callable.From(root.QueueFree));
    }

    /// <summary>
    /// Flamme de bouche en <paramref name="pos"/> (taille <paramref name="size"/> en mètres) ; <paramref name="dir"/> = sens du tir
    /// (pétales le long du canon), sinon étoile seule.
    /// </summary>
    public static void Spawn(Node3D parent, Vector3 pos, Color color, float size, Vector3? dir)
    {
        Init();
        var root = new Node3D();
        parent.AddChild(root);
        root.GlobalPosition = pos;
        var tint = new Vector3(color.R, color.G, color.B);
        var star = new MeshInstance3D { Mesh = _quad, MaterialOverride = _star, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(star);
        star.Scale = Vector3.One * size * (1.3f + GD.Randf() * 0.5f);
        star.SetInstanceShaderParameter("tint", tint);
        star.SetInstanceShaderParameter("roll", GD.Randf() * Mathf.Tau);
        if (dir is { } d && d.LengthSquared() > 1e-4f)
        {
            d = d.Normalized();
            var up = Mathf.Abs(d.Y) > 0.95f ? Vector3.Right : Vector3.Up;
            var side = d.Cross(up).Normalized();
            var upv = side.Cross(d).Normalized();
            var len = size * (2.2f + GD.Randf() * 1.2f);
            var spin = GD.Randf() * 0.6f;
            for (var i = 0; i < 2; i++)
            {
                var petal = new MeshInstance3D { Mesh = _petalQuad, MaterialOverride = _petal, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                root.AddChild(petal);
                // Deux pétales croisés contenant l'axe du tir (X du quad le long du canon), roulis aléatoire autour de l'axe.
                var a = spin + i * Mathf.Pi / 2;
                var y = upv * Mathf.Cos(a) + side * Mathf.Sin(a);
                petal.GlobalBasis = new Basis(d * len, y * size * 1.1f, d.Cross(y));
                petal.SetInstanceShaderParameter("tint", tint);
            }
        }
        var energy = Settings.I.ReduceFlashes ? 0.5f : 1.3f;
        var light = new OmniLight3D
        {
            LightColor = color,
            LightEnergy = energy,
            OmniRange = 3.5f + size * 2f,
            LightSpecular = 0.3f,
        };
        root.AddChild(light);
        var tw = root.CreateTween();
        tw.TweenMethod(Callable.From<float>(f =>
        {
            foreach (var c in root.GetChildren())
                if (c is MeshInstance3D mi) mi.SetInstanceShaderParameter("fade", f);
            light.LightEnergy = energy * f;
        }), 1f, 0f, 0.06f);
        tw.TweenCallback(Callable.From(root.QueueFree));
    }
}
