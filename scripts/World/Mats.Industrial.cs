using Godot;

namespace Rempart;

/// <summary>
/// Matières de la zone industrielle : verrières d'usine (petits carreaux sur châssis d'acier, vitres brisées,
/// condamnées ou passées au blanc), brique d'usine, grillage, caillebotis, boues de décantation.
/// Textures générées en C# (en parallèle), mises en cache comme les autres matériaux.
/// </summary>
public static partial class Mats
{
    /// <summary>
    /// Verrière d'atelier : châssis d'acier à petits carreaux (6 × 6 par travée de 3 m), carreaux sales, brisés (vide
    /// sombre), passés au blanc de chaux ou condamnés par une tôle ; coulures de rouille sous les traverses.
    /// UV : 1 U = 1 V = une travée de 3 m. Teinte par sommet.
    /// </summary>
    public static ShaderMaterial FactoryGlazing()
    {
        if (TryCache<ShaderMaterial>("factoryglazing", out var cached)) return cached;
        const int s = 256;
        const int panes = 6;
        const int cell = s / panes;
        var alb = new Px(s, s);
        var rough = new Px(s, s, 1);
        var bump = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var cx = x / cell;
                var cy = y / cell;
                var lx = x % cell;
                var ly = y % cell;
                var frame = lx < 3 || ly < 3 || x < 6 || x >= s - 6;
                var grain = ValueNoise(x, y, 4, s / 4, 811) * 0.5f + Hash(x, y, 812) * 0.5f;
                var dirt = ValueNoise(x, y, 32, s / 32, 813);
                Color col;
                float r, h;
                if (frame)
                {
                    // Châssis d'acier peint, écaillé et rouillé.
                    var rust = ValueNoise(x, y, 8, s / 8, 814) > 0.62f;
                    col = rust ? new Color(0.22f, 0.13f, 0.08f) : new Color(0.17f, 0.18f, 0.17f);
                    col *= 0.85f + grain * 0.3f;
                    r = rust ? 0.9f : 0.55f;
                    h = 0.9f;
                }
                else
                {
                    var roll = Hash(cx, cy, 815);
                    var u = lx / (float)cell;
                    var v = ly / (float)cell;
                    if (roll < 0.26f)
                    {
                        // Carreau brisé : vide sombre, éclats restés dans les angles.
                        var shard = (u + v < 0.35f + Hash(cx, cy, 816) * 0.3f) || (u > 0.8f && v < 0.2f + Hash(cx, cy, 817) * 0.4f);
                        col = shard ? new Color(0.3f, 0.32f, 0.31f) : new Color(0.025f, 0.024f, 0.022f) * (0.8f + grain * 0.4f);
                        r = shard ? 0.2f : 1f;
                        h = shard ? 0.6f : 0.2f;
                    }
                    else if (roll < 0.36f)
                    {
                        // Condamné : tôle rouillée.
                        col = new Color(0.2f, 0.14f, 0.1f) * (0.75f + grain * 0.5f);
                        r = 0.9f;
                        h = 0.75f;
                    }
                    else if (roll < 0.5f)
                    {
                        // Passé au blanc de chaux (atelier peint), écaillé.
                        var chip = ValueNoise(x, y, 6, s / 6, 818) > 0.66f;
                        col = chip ? new Color(0.16f, 0.17f, 0.17f) : new Color(0.5f, 0.5f, 0.47f) * (0.8f + grain * 0.25f);
                        r = 0.85f;
                        h = 0.5f;
                    }
                    else
                    {
                        // Verre armé encrassé : reflets ternes, suie en bas du carreau.
                        var g = 0.17f + 0.08f * Hash(cx, cy, 819) + v * 0.06f;
                        col = new Color(g, g * 1.04f, g * 1.06f) * (0.85f + grain * 0.25f);
                        col = col.Lerp(new Color(0.05f, 0.045f, 0.04f), Mathf.Clamp(dirt - 0.4f, 0, 1));
                        r = 0.25f;
                        h = 0.5f;
                    }
                }
                // Coulures de rouille sous les traverses.
                var drip = VNoise(x, y, 3, 48, s, 820);
                if (!frame && ly < cell * 0.6f && drip > 0.72f) col = col.Lerp(new Color(0.2f, 0.11f, 0.06f), (drip - 0.72f) * 2.5f);
                alb.Set(x, y, col);
                rough.Set(x, y, r);
                bump.Set(x, y, new Color(h, h, h));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 3f), ImageTexture.CreateFromImage(rough.ToImage()), 0, Vector3.One)
            .With("roughness", 1f).With("metallic", 0.2f).With("normal_depth", 0.6f).With("vertex_color", true).With("streaks", 1.2f).With("desat", 0.2f);
        Cache["factoryglazing"] = mat;
        return mat;
    }

    /// <summary>Brique d'usine (cheminées, vieux ateliers) : rangs de 6,5 cm, briques sombres et noircies, joints ; triplanaire monde, teinte par sommet.</summary>
    public static ShaderMaterial FactoryBrick()
    {
        if (TryCache<ShaderMaterial>("factorybrick", out var cached)) return cached;
        const int s = 256; // 2 m
        const int rowH = 8;
        const int brickW = 28;
        var alb = new Px(s, s);
        var bump = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var row = y / rowH;
                var off = row % 2 == 0 ? 0 : brickW / 2;
                var bx = ((x + off) % s) / brickW;
                var lx = (x + off) % brickW;
                var ly = y % rowH;
                var joint = ly < 1 || lx < 2;
                var tone = Hash(bx, row, 821);
                var grain = Hash(x, y, 822) * 0.12f + ValueNoise(x, y, 4, s / 4, 823) * 0.1f;
                var c = joint
                    ? new Color(0.46f, 0.45f, 0.42f)
                    : new Color(0.52f, 0.32f, 0.24f).Lerp(new Color(0.34f, 0.26f, 0.22f), tone * 0.8f) * (0.85f + grain);
                // Briques brûlées plus sombres, efflorescences claires.
                if (!joint && Hash(bx, row, 824) < 0.08f) c *= 0.55f;
                var eff = ValueNoise(x, y, 32, s / 32, 825);
                if (eff > 0.7f) c = c.Lerp(new Color(0.5f, 0.48f, 0.44f), (eff - 0.7f) * 1.2f);
                alb.Set(x, y, c);
                var h = joint ? 0.25f : 0.8f + grain;
                bump.Set(x, y, new Color(h, h, h));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 4f), null, 2, new Vector3(0.5f, 0.5f, 0.5f))
            .With("roughness", 0.9f).With("vertex_color", true).With("desat", 0.35f).With("streaks", 1.3f).With("grime", 1.2f);
        Cache["factorybrick"] = mat;
        return mat;
    }

    /// <summary>
    /// Grillage à mailles losangées galvanisé terni : ajouré (transparence alpha, se fond en voile gris au loin),
    /// visible des deux côtés. UV : 1 U = 1 m.
    /// </summary>
    public static StandardMaterial3D ChainLink()
    {
        if (TryCache<StandardMaterial3D>("chainlink", out var cached)) return cached;
        const int s = 64; // 0,5 m
        const int pitch = 8;
        var px = new Px(s, s);
        for (var y = 0; y < s; y++)
        for (var x = 0; x < s; x++)
        {
            // Deux familles de fils en diagonale.
            var a = Mathf.Abs(((x + y) % pitch) - pitch / 2f);
            var b = Mathf.Abs(((x - y + s) % pitch) - pitch / 2f);
            var w = Mathf.Max(Mathf.Clamp(1.6f - (pitch / 2f - a), 0, 1), Mathf.Clamp(1.6f - (pitch / 2f - b), 0, 1));
            var rust = ValueNoise(x, y, 8, s / 8, 831) > 0.6f;
            var c = rust ? new Color(0.33f, 0.24f, 0.18f) : new Color(0.46f, 0.47f, 0.46f);
            c.A = w;
            px.Set(x, y, c);
        }
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(px.ToImage()),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.6f,
            Metallic = 0.5f,
            Uv1Scale = new Vector3(2f, 2f, 1f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            VertexColorUseAsAlbedo = true,
        };
        Cache["chainlink"] = mat;
        return mat;
    }

    /// <summary>Caillebotis d'acier (passerelles, plateformes) : barres porteuses et entretoises, ajouré (découpe alpha). UV : 1 U = 1 m.</summary>
    public static StandardMaterial3D Grating()
    {
        if (TryCache<StandardMaterial3D>("grating", out var cached)) return cached;
        const int s = 64; // 1 m : barres tous les 3 cm environ
        var px = new Px(s, s);
        for (var y = 0; y < s; y++)
        for (var x = 0; x < s; x++)
        {
            var bar = x % 2 == 0 || y % 8 == 0;
            var rust = ValueNoise(x, y, 8, s / 8, 841);
            var c = new Color(0.2f, 0.2f, 0.19f).Lerp(new Color(0.26f, 0.15f, 0.09f), Mathf.Clamp((rust - 0.45f) * 2f, 0, 1));
            c.A = bar ? 1f : 0f;
            px.Set(x, y, c);
        }
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(px.ToImage()),
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = 0.4f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.7f,
            Metallic = 0.6f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        Cache["grating"] = mat;
        return mat;
    }

    /// <summary>Boues de décantation : surface huileuse brun-vert presque noire, irisations ternes, croûtes sèches sur les bords (triplanaire monde).</summary>
    public static StandardMaterial3D Sludge()
    {
        if (TryCache<StandardMaterial3D>("sludge", out var cached)) return cached;
        const int s = 256;
        var px = new Px(s, s);
        var rough = new Px(s, s, 1);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var n = Fbm(x, y, 64, s / 64, 851);
                var crust = Smooth(0.62f, 0.7f, n);
                var sheen = ValueNoise(x, y, 16, s / 16, 852);
                var wet = new Color(0.045f, 0.05f, 0.035f).Lerp(new Color(0.09f, 0.075f, 0.05f), sheen);
                var dry = new Color(0.2f, 0.18f, 0.14f) * (0.8f + Hash(x, y, 853) * 0.3f);
                var c = wet.Lerp(dry, crust);
                px.Set(x, y, c);
                rough.Set(x, y, Mathf.Lerp(0.38f, 0.95f, crust));
            }
        });
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(px.ToImage()),
            RoughnessTexture = ImageTexture.CreateFromImage(rough.ToImage()),
            Roughness = 1f,
            Metallic = 0f,
            MetallicSpecular = 0.35f,
            NormalEnabled = true,
            NormalTexture = Noise(854, 0.04f, true, 256, 2f),
            NormalScale = 0.4f,
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = Vector3.One / 12f,
        };
        Cache["sludge"] = mat;
        return mat;
    }

    /// <summary>Eau stagnante du canal : opaque, brune et huileuse, reflets lents (pas de transparence : on ne voit pas le fond).</summary>
    public static ShaderMaterial CanalWater(TimeOfDay time)
    {
        var key = $"canal{time}";
        if (TryCache<ShaderMaterial>(key, out var m)) return m;
        var shader = new Shader
        {
            Code = @"
shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_opaque;
uniform vec3 base_col : source_color = vec3(0.035, 0.04, 0.03);
uniform sampler2D n1 : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D n2 : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D slick : source_color, filter_linear_mipmap, repeat_enable;
varying vec3 wpos;
void vertex() {
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}
void fragment() {
    vec2 uv = wpos.xz * 0.07;
    vec3 a = texture(n1, uv + vec2(TIME * 0.012, 0.003 * TIME)).rgb;
    vec3 b = texture(n2, uv * 2.3 + vec2(TIME * 0.02, -0.006 * TIME)).rgb;
    NORMAL_MAP = normalize(mix(a, b, 0.5));
    NORMAL_MAP_DEPTH = 0.5;
    // Nappes d'hydrocarbures : irisations très ternes, écume grise le long des berges (couleur de sommet).
    float s = texture(slick, wpos.xz * 0.013 + vec2(TIME * 0.002, 0.0)).r;
    vec3 iris = 0.5 + 0.5 * cos(6.2831 * (s * 2.0 + vec3(0.0, 0.33, 0.67)));
    vec3 col = mix(base_col, base_col + iris * 0.025, smoothstep(0.55, 0.75, s));
    col = mix(col, vec3(0.16, 0.15, 0.13), COLOR.r);
    ALBEDO = col;
    ROUGHNESS = mix(0.05, 0.8, COLOR.r);
    METALLIC = 0.0;
    SPECULAR = 0.55;
}"
        };
        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("n1", Noise(861, 0.03f, true, 512, 4f));
        mat.SetShaderParameter("n2", Noise(862, 0.05f, true, 512, 2f));
        mat.SetShaderParameter("slick", Noise(863, 0.02f, false, 256));
        if (time == TimeOfDay.Night) mat.SetShaderParameter("base_col", new Color(0.012f, 0.014f, 0.012f));
        Cache[key] = mat;
        return mat;
    }
}
