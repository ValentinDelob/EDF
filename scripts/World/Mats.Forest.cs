using Godot;

namespace Rempart;

/// <summary>
/// Matières de la vallée forestière : sol de forêt composé dans le shader (litière d'aiguilles et de feuilles mortes,
/// mousse, herbe sèche, gravier des pistes, boue, roche selon la pente, canopée lointaine), roche moussue des blocs et
/// des falaises, eau de rivière qui coule (courant, rapides écumeux, déversoir du barrage). Textures générées en C#
/// (en parallèle) ; teintes ternes et humides, l'étalonnage gris de l'invasion en retire encore.
/// </summary>
public static partial class Mats
{
    // ───────────────────────── Textures ─────────────────────────

    /// <summary>Litière de sous-bois (4 m) : aiguilles brunes en traits, feuilles mortes, brindilles, terre sombre.</summary>
    private static (ImageTexture alb, Px bump) ForestLitterTex()
    {
        const int s = 512;
        var alb = new Px(s, s);
        var bump = new Px(s, s);
        var soil = new Color(0.16f, 0.13f, 0.1f);
        var needle = new Color(0.36f, 0.27f, 0.18f);
        var leaf = new Color(0.33f, 0.24f, 0.15f);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var patch = ValueNoise(x, y, 64, s / 64, 2101) * 0.6f + ValueNoise(x, y, 16, s / 16, 2102) * 0.4f;
                var col = soil.Lerp(needle, Mathf.SmoothStep(0.3f, 0.75f, patch) * 0.8f);
                var h = 0.4f + patch * 0.2f;
                // Aiguilles : traits fins dans trois directions (bruit anisotrope, périodique sur la tuile).
                for (var k = 0; k < 3; k++)
                {

                    var u = k == 0 ? x : k == 1 ? y : x + y;
                    var v = k == 0 ? y : k == 1 ? x : x - y + s;
                    var n = VNoise(u, v, 2, 16, s, 2103 + k);
                    if (n > 0.8f)
                    {
                        var t = Mathf.SmoothStep(0.8f, 0.9f, n);
                        col = col.Lerp(needle * (0.75f + Hash(x / 3, y / 3, 2106 + k) * 0.6f), t * 0.85f);
                        h += 0.25f * t;
                    }
                }
                // Feuilles mortes : taches arrondies plus claires, bord sombre.
                var lf = ValueNoise(x, y, 8, s / 8, 2107);
                if (lf > 0.74f)
                {
                    var t = Mathf.SmoothStep(0.74f, 0.8f, lf);
                    col = col.Lerp(leaf * (0.8f + Hash(x / 8, y / 8, 2108) * 0.5f), t);
                    h += 0.15f * t;
                }
                col *= 0.85f + Hash(x, y, 2109) * 0.2f;
                alb.Set(x, y, col);
                bump.Set(x, y, new Color(h, h, h));
            }
        });
        return (ImageTexture.CreateFromImage(alb.ToImage()), bump);
    }

    /// <summary>Mousse (3 m) : coussinets bombés vert-de-gris, creux sombres.</summary>
    private static (ImageTexture alb, Px bump) ForestMossTex()
    {
        const int s = 256;
        var alb = new Px(s, s);
        var bump = new Px(s, s);
        var dark = new Color(0.1f, 0.13f, 0.07f);
        var lit = new Color(0.27f, 0.31f, 0.15f);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var c = ValueNoise(x, y, 8, s / 8, 2201) * 0.55f + ValueNoise(x, y, 4, s / 4, 2202) * 0.25f + ValueNoise(x, y, 32, s / 32, 2203) * 0.2f;
                var col = dark.Lerp(lit, Mathf.SmoothStep(0.3f, 0.75f, c));
                col *= 0.85f + Hash(x, y, 2204) * 0.25f;
                alb.Set(x, y, col);
                bump.Set(x, y, new Color(c, c, c));
            }
        });
        return (ImageTexture.CreateFromImage(alb.ToImage()), bump);
    }

    /// <summary>Roche (granite gneissique, 8 m) : grain, veines sombres, lichens clairs, fissures.</summary>
    private static (ImageTexture alb, Px bump) ForestRockTex()
    {
        const int s = 512;
        var alb = new Px(s, s);
        var bump = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var big = ValueNoise(x, y, 128, s / 128, 2301) * 0.5f + ValueNoise(x, y, 32, s / 32, 2302) * 0.3f + ValueNoise(x, y, 8, s / 8, 2303) * 0.2f;
                var grain = Hash(x, y, 2304);
                var tone = 0.2f + big * 0.13f + grain * 0.05f;
                var col = new Color(tone, tone * 0.99f, tone * 0.96f);
                var h = big * 0.7f + grain * 0.1f;
                // Veines obliques (foliation) et fissures.
                var vein = VNoise(x + y, y - x + s, 4, 64, s, 2305);
                if (Mathf.Abs(vein - 0.5f) < 0.04f) { col *= 0.72f; h -= 0.1f; }
                var crack = ValueNoise(x, y, 32, s / 32, 2306);
                if (Mathf.Abs(crack - 0.5f) < 0.012f) { col *= 0.4f; h -= 0.35f; }
                // Lichens : taches claires et vert-de-gris.
                var li = ValueNoise(x, y, 4, s / 4, 2307) * 0.6f + ValueNoise(x, y, 16, s / 16, 2308) * 0.4f;
                if (li > 0.7f) col = col.Lerp(new Color(0.4f, 0.42f, 0.35f), Mathf.SmoothStep(0.7f, 0.76f, li) * 0.5f);
                alb.Set(x, y, col);
                bump.Set(x, y, new Color(h, h, h));
            }
        });
        return (ImageTexture.CreateFromImage(alb.ToImage()), bump);
    }

    /// <summary>Gravier de piste (3 m) : cailloux gris-beige tassés, ornières de boue entre eux.</summary>
    private static (ImageTexture alb, Px bump) ForestGravelTex()
    {
        const int s = 256;
        var alb = new Px(s, s);
        var bump = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var peb = ValueNoise(x, y, 4, s / 4, 2401);
                var peb2 = ValueNoise(x + 37, y + 11, 8, s / 8, 2402);
                var p = Mathf.Max(peb, peb2 * 0.95f);
                var stone = new Color(0.36f, 0.34f, 0.31f) * (0.82f + Hash(x / 4, y / 4, 2403) * 0.3f);
                var soil = new Color(0.25f, 0.22f, 0.18f);
                var t = Mathf.SmoothStep(0.55f, 0.68f, p);
                var col = soil.Lerp(stone, t) * (0.9f + Hash(x, y, 2404) * 0.15f);
                alb.Set(x, y, col);
                var h = t * 0.6f + p * 0.3f;
                bump.Set(x, y, new Color(h, h, h));
            }
        });
        return (ImageTexture.CreateFromImage(alb.ToImage()), bump);
    }

    /// <summary>Bruit de macro-variation (taches, canopée lointaine, flaques), périodique.</summary>
    private static ImageTexture ForestMacroTex()
    {
        const int s = 256;
        var px = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var a = ValueNoise(x, y, 32, s / 32, 2501) * 0.6f + ValueNoise(x, y, 8, s / 8, 2502) * 0.4f;
                var b = ValueNoise(x, y, 16, s / 16, 2503) * 0.5f + ValueNoise(x, y, 4, s / 4, 2504) * 0.5f;
                var c = ValueNoise(x, y, 2, s / 2, 2505);
                px.Set(x, y, new Color(a, b, c, 1f));
            }
        });
        return ImageTexture.CreateFromImage(px.ToImage());
    }

    private static ImageTexture _forestMacro;
    private static ImageTexture ForestMacro => _forestMacro ??= ForestMacroTex();

    // ───────────────────────── Sol ─────────────────────────

    private const string ForestGroundCode = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D litter_alb : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D moss_alb : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D grass_alb : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rock_alb : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D gravel_alb : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D soft_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rock_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D gravel_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D macro : filter_linear_mipmap, repeat_enable;
uniform vec3 grass_tint : source_color = vec3(0.62, 0.6, 0.42);
uniform vec3 canopy_col : source_color = vec3(0.045, 0.06, 0.045);
uniform float wet = 0.0;

varying vec3 wpos;
varying vec3 wnrm;

void vertex() {
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

vec3 tri(sampler2D t, vec3 p, vec3 w, float s) {
    return texture(t, p.zy * s).rgb * w.x + texture(t, p.xz * s).rgb * w.y + texture(t, p.xy * s).rgb * w.z;
}

void fragment() {
    vec3 n = normalize(wnrm);
    float slope = 1.0 - n.y;
    vec3 m1 = texture(macro, wpos.xz * 0.0045).rgb;
    vec3 m2 = texture(macro, wpos.xz * 0.023 + vec2(0.37, 0.71)).rgb;
    vec2 uv = wpos.xz * 0.25;
    // Couches : litière, mousse (G), herbe sèche (R), piste (A), roche selon la pente ; B = ombre de canopée / canopée lointaine.
    vec3 col = texture(litter_alb, uv).rgb;
    vec3 nm = texture(soft_nrm, uv).rgb;
    float mossw = clamp(COLOR.g * 1.25 * smoothstep(0.15, 0.85, m2.r * 0.6 + m1.g * 0.4), 0.0, 1.0);
    col = mix(col, texture(moss_alb, uv * 1.3).rgb, mossw);
    float grassw = clamp(COLOR.r * smoothstep(0.1, 0.8, m2.g * 0.7 + m1.r * 0.3) * 1.15, 0.0, 1.0);
    col = mix(col, texture(grass_alb, wpos.xz * 0.19).rgb * grass_tint, grassw);
    float path = COLOR.a;
    vec3 grav = texture(gravel_alb, wpos.xz * 0.33).rgb;
    float mud = smoothstep(0.45, 0.7, m2.b * 0.5 + m1.b * 0.5) * 0.65;
    grav = mix(grav, grav * vec3(0.45, 0.4, 0.35), mud);
    col = mix(col, grav, path);
    nm = mix(nm, texture(gravel_nrm, wpos.xz * 0.33).rgb, path);
    // Roche sur les fortes pentes (triplanaire), bord irrégulier.
    vec3 bw = pow(abs(n), vec3(4.0));
    bw /= dot(bw, vec3(1.0));
    float rockw = smoothstep(0.3, 0.46, slope + (m2.r - 0.5) * 0.16);
    vec3 rk = tri(rock_alb, wpos, bw, 0.11);
    rk = mix(rk, rk * vec3(0.55, 0.62, 0.42), smoothstep(0.55, 0.8, m2.g) * 0.6 * n.y); // mousse sur les replats
    col = mix(col, rk, rockw);
    nm = mix(nm, texture(rock_nrm, (abs(n.x) > abs(n.z) ? wpos.zy : wpos.xy) * 0.11).rgb, rockw);
    // Variation à grande échelle, ombre du couvert, canopée lointaine (aplat bosselé sombre).
    col *= mix(0.88, 1.06, m1.r);
    float shade = COLOR.b;
    col *= mix(1.0, 0.55, clamp(shade * 1.6, 0.0, 1.0));
    float far = smoothstep(0.62, 0.9, shade);
    vec3 crowns = canopy_col * (0.55 + 0.9 * texture(macro, wpos.xz * 0.11).b) * (0.7 + 0.5 * m1.g);
    col = mix(col, crowns, far);
    // Humidité (pluie) : sol plus sombre et brillant, surtout les pistes et les creux.
    float wk = wet * (0.55 + 0.45 * path + 0.12 * smoothstep(0.5, 0.9, m2.b)) * (1.0 - rockw * 0.4) * (1.0 - far);
    col *= mix(1.0, 0.68, clamp(wk, 0.0, 1.0));
    ALBEDO = col;
    ROUGHNESS = mix(0.96, 0.3, clamp(wk * (0.4 + 0.6 * path), 0.0, 1.0));
    SPECULAR = 0.35;
    NORMAL_MAP = nm;
    NORMAL_MAP_DEPTH = mix(1.0, 0.25, far);
}";

    /// <summary>
    /// Sol de la vallée (maillage de terrain) : couleur de sommet R = herbe sèche, G = mousse, B = ombre du couvert
    /// (au-delà de 0,65 : canopée lointaine vue d'en haut), A = piste ou aire gravillonnée ; roche selon la pente.
    /// </summary>
    public static ShaderMaterial ForestGround(bool wet)
    {
        var key = wet ? "forestground_wet" : "forestground";
        if (TryCache<ShaderMaterial>(key, out var cached)) return cached;
        var (lit, litB) = ForestLitterTex();
        var (moss, mossB) = ForestMossTex();
        var (rock, rockB) = ForestRockTex();
        var (grav, gravB) = ForestGravelTex();
        var (grass, _, _) = DeadGrass(true);
        _ = mossB;
        var mat = new ShaderMaterial { Shader = new Shader { Code = ForestGroundCode } };
        mat.SetShaderParameter("litter_alb", lit);
        mat.SetShaderParameter("moss_alb", moss);
        mat.SetShaderParameter("grass_alb", grass);
        mat.SetShaderParameter("rock_alb", rock);
        mat.SetShaderParameter("gravel_alb", grav);
        mat.SetShaderParameter("soft_nrm", BumpToNormal(litB, 4f));
        mat.SetShaderParameter("rock_nrm", BumpToNormal(rockB, 5f));
        mat.SetShaderParameter("gravel_nrm", BumpToNormal(gravB, 6f));
        mat.SetShaderParameter("macro", ForestMacro);
        mat.SetShaderParameter("wet", wet ? 1f : 0.25f);
        Cache[key] = mat;
        return mat;
    }

    // ───────────────────────── Roche ─────────────────────────

    private const string ForestRockCode = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;
uniform sampler2D rock_alb : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rock_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D moss_alb : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D macro : filter_linear_mipmap, repeat_enable;
uniform float moss_amount = 1.0;
uniform float wet = 0.0;
varying vec3 wpos;
varying vec3 wnrm;
void vertex() {
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    wnrm = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}
void fragment() {
    vec3 n = normalize(wnrm);
    vec3 bw = pow(abs(n), vec3(4.0));
    bw /= dot(bw, vec3(1.0));
    float s = 0.14;
    vec3 col = texture(rock_alb, wpos.zy * s).rgb * bw.x + texture(rock_alb, wpos.xz * s).rgb * bw.y + texture(rock_alb, wpos.xy * s).rgb * bw.z;
    vec3 nm = texture(rock_nrm, (abs(n.x) > abs(n.z) ? wpos.zy : wpos.xy) * s).rgb;
    vec3 m = texture(macro, wpos.xz * 0.05 + wpos.y * 0.013).rgb;
    col *= COLOR.rgb * mix(0.8, 1.15, m.r);
    // Mousse sur les faces tournées vers le ciel, coulures sombres sous les surplombs.
    float mw = smoothstep(0.35, 0.8, n.y + (m.g - 0.5) * 0.6) * moss_amount;
    col = mix(col, texture(moss_alb, wpos.xz * 0.4).rgb, mw * 0.85);
    col *= mix(1.0, 0.65, smoothstep(0.0, -0.6, n.y));
    col *= mix(1.0, 0.72, smoothstep(0.55, 0.75, texture(macro, vec2(wpos.x + wpos.z, wpos.y * 3.0) * 0.02).g) * (1.0 - n.y));
    float wk = wet * (1.0 - mw * 0.5);
    col *= mix(1.0, 0.65, wk);
    ALBEDO = col;
    ROUGHNESS = mix(0.92, 0.38, wk);
    SPECULAR = 0.4;
    NORMAL_MAP = nm;
    NORMAL_MAP_DEPTH = 1.1;
}";

    /// <summary>Roche moussue (blocs, falaises, affleurements) : triplanaire monde, mousse au-dessus, teinte par sommet.</summary>
    public static ShaderMaterial ForestRock(bool wet = false)
    {
        var key = wet ? "forestrock_wet" : "forestrock";
        if (TryCache<ShaderMaterial>(key, out var cached)) return cached;
        var g = ForestGround(wet);
        var mat = new ShaderMaterial { Shader = new Shader { Code = ForestRockCode } };
        mat.SetShaderParameter("rock_alb", g.GetShaderParameter("rock_alb"));
        mat.SetShaderParameter("rock_nrm", g.GetShaderParameter("rock_nrm"));
        mat.SetShaderParameter("moss_alb", g.GetShaderParameter("moss_alb"));
        mat.SetShaderParameter("macro", ForestMacro);
        mat.SetShaderParameter("wet", wet ? 0.9f : 0.2f);
        Cache[key] = mat;
        return mat;
    }

    // ───────────────────────── Feuillage en cartes ─────────────────────────

    /// <summary>
    /// Rameau d'épicéa en carte (UV : u = de la base à la pointe, v = en travers) : tige centrale, rameaux secondaires
    /// obliques vers la pointe couverts d'aiguilles fines, silhouette en fuseau. Découpe (alpha scissor), deux faces.
    /// </summary>
    public static StandardMaterial3D Needles()
    {
        if (TryCache<StandardMaterial3D>("needles", out var c)) return c;
        const int w = 256, h = 128;
        var px = new Px(w, h);
        System.Threading.Tasks.Parallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var u = x / (float)w;
                var v = (y - h / 2f) / (h / 2f); // -1..1
                // Fuseau : étroit à la base et à la pointe.
                var env = Mathf.Sin(Mathf.Clamp(u * 1.08f, 0f, 1f) * Mathf.Pi) * 0.85f + 0.12f;
                var a = 0f;
                var shade = 0.6f;
                if (Mathf.Abs(v) < 0.035f) { a = 1f; shade = 0.35f; } // tige
                // Rameaux secondaires : obliques vers la pointe, de part et d'autre.
                const float spacing = 0.07f;
                for (var k = -1; k <= 1; k++)
                {
                    var u0 = (Mathf.Floor(u / spacing) + k) * spacing + (Hash((int)(u / spacing) + k, v > 0 ? 1 : 0, 2701) - 0.5f) * 0.02f;
                    var along = (u - u0) * 3.2f + Mathf.Abs(v);       // abscisse le long du rameau
                    var off = Mathf.Abs(v) - (u - u0) * 2.4f;          // écart au rameau
                    if (along < 0f || along > env * 1.15f) continue;
                    var d = Mathf.Abs(off);
                    // Aiguilles : hachures fines autour du rameau, plus courtes vers son bout.
                    var needle = 0.5f + 0.5f * Mathf.Sin(along * 95f + Hash(x / 2, y, 2702) * 2f);
                    var reach = 0.13f * (1f - along / (env * 1.2f)) + 0.03f;
                    if (d < 0.02f) { a = 1f; shade = Mathf.Min(shade, 0.45f); }
                    else if (d < reach && needle > 0.35f) { a = 1f; shade = Mathf.Max(shade, 0.55f + 0.45f * (d / reach)); }
                }
                if (Mathf.Abs(v) > env) a = 0f;
                var col = new Color(shade, shade, shade * 0.95f, a);
                px.Set(x, y, col);
            }
        });
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoTexture = ImageTexture.CreateFromImage(px.ToImage()),
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = 0.5f,
            AlphaAntialiasingMode = BaseMaterial3D.AlphaAntiAliasing.Off,
            Roughness = 0.88f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BacklightEnabled = true,
            Backlight = new Color(0.05f, 0.06f, 0.04f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        Cache["needles"] = mat;
        return mat;
    }

    /// <summary>
    /// Bouquet de feuilles en carte (UV 0..1) : feuilles ovales nervurées qui se chevauchent, plus denses au centre,
    /// quelques trous ; pour les feuillus de la vallée (variante <paramref name="dry"/> : feuilles sèches clairsemées).
    /// </summary>
    public static StandardMaterial3D LeafCluster(bool dry)
    {
        var key = dry ? "leafcluster_dry" : "leafcluster";
        if (TryCache<StandardMaterial3D>(key, out var c)) return c;
        const int s = 256;
        var a = new float[s * s];
        var shade = new float[s * s];
        var rng = new RandomNumberGenerator { Seed = dry ? 2801UL : 2802UL };
        var count = dry ? 70 : 190;
        for (var i = 0; i < count; i++)
        {
            // Position : densité décroissante vers le bord du bouquet.
            var r = Mathf.Sqrt(rng.Randf()) * 0.46f;
            var ang = rng.Randf() * Mathf.Tau;
            var cx = 0.5f + Mathf.Cos(ang) * r;
            var cy = 0.5f + Mathf.Sin(ang) * r * 0.85f;
            var dir = rng.Randf() * Mathf.Tau;
            var len = rng.RandfRange(0.07f, 0.11f);
            var wid = len * rng.RandfRange(0.42f, 0.55f);
            var tone = rng.RandfRange(0.55f, 1f);
            var ca = Mathf.Cos(dir);
            var sa = Mathf.Sin(dir);
            var x0 = Mathf.Max(0, (int)((cx - len) * s));
            var x1 = Mathf.Min(s - 1, (int)((cx + len) * s));
            var y0 = Mathf.Max(0, (int)((cy - len) * s));
            var y1 = Mathf.Min(s - 1, (int)((cy + len) * s));
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var dx = x / (float)s - cx;
                var dy = y / (float)s - cy;
                var lu = dx * ca + dy * sa;
                var lv = -dx * sa + dy * ca;
                var t = lu / len; // -1..1 le long de la feuille
                if (t < -1f || t > 1f) continue;
                var half = wid * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)) * (t > 0 ? 1f - 0.35f * t : 1f);
                if (Mathf.Abs(lv) > half) continue;
                var k = y * s + x;
                a[k] = 1f;
                // Nervure centrale plus sombre, bords légèrement plus clairs.
                var vein = Mathf.Abs(lv) < 0.004f ? 0.75f : 1f;
                shade[k] = tone * vein * (0.85f + 0.15f * Mathf.Abs(lv) / Mathf.Max(half, 1e-4f));
            }
        }
        var px = new Px(s, s);
        for (var y = 0; y < s; y++)
        for (var x = 0; x < s; x++)
        {
            var k = y * s + x;
            var v = shade[k];
            px.Set(x, y, new Color(v, v, v * 0.92f, a[k]));
        }
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoTexture = ImageTexture.CreateFromImage(px.ToImage()),
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = 0.5f,
            AlphaAntialiasingMode = BaseMaterial3D.AlphaAntiAliasing.Off,
            Roughness = 0.85f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BacklightEnabled = true,
            Backlight = new Color(0.07f, 0.08f, 0.04f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        Cache[key] = mat;
        return mat;
    }

    // ───────────────────────── Eau ─────────────────────────

    private const string ForestRiverCode = @"
shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_opaque, diffuse_burley, specular_schlick_ggx;
uniform vec3 deep : source_color = vec3(0.03, 0.05, 0.045);
uniform vec3 shallow : source_color = vec3(0.12, 0.14, 0.11);
uniform vec3 foam_col : source_color = vec3(0.62, 0.64, 0.62);
uniform sampler2D n1 : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D n2 : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D macro : filter_linear_mipmap, repeat_enable;
// UV.x = abscisse le long du courant (m), UV.y = travers (m) ; COLOR.r = écume, COLOR.g = vitesse, COLOR.a = opacité.
void fragment() {
    float sp = mix(0.25, 3.2, COLOR.g);
    vec2 f = vec2(UV.x - TIME * sp, UV.y);
    vec3 a = texture(n1, f * vec2(0.05, 0.09)).rgb;
    vec3 b = texture(n2, (f + vec2(-TIME * sp * 0.4, 0.0)) * vec2(0.11, 0.17)).rgb;
    NORMAL_MAP = normalize(mix(a, b, 0.5));
    NORMAL_MAP_DEPTH = mix(0.8, 2.0, COLOR.g);
    float fo = texture(macro, f * vec2(0.09, 0.22)).b * 0.6 + texture(macro, f * vec2(0.23, 0.5) + 0.3).g * 0.4;
    float foam = smoothstep(1.02 - COLOR.r, 1.12 - COLOR.r * 0.7, fo + COLOR.r * 0.35) * clamp(COLOR.r * 1.6, 0.0, 1.0);
    float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
    ALBEDO = mix(mix(shallow, deep, COLOR.a), foam_col, foam);
    ALPHA = clamp(max(COLOR.a * (0.84 + 0.15 * fres), foam * 0.95), 0.0, 1.0);
    ROUGHNESS = mix(0.04, 0.75, foam);
    SPECULAR = mix(0.6, 0.2, foam);
}";

    /// <summary>
    /// Eau courante de la vallée : courant le long des UV (UV.x = abscisse, m), écume et vitesse par sommet (rapides,
    /// déversoir), opacité par sommet (berges). Nuit : eau presque noire.
    /// </summary>
    public static ShaderMaterial ForestRiver(TimeOfDay time)
    {
        var key = $"forestriver{time}";
        if (TryCache<ShaderMaterial>(key, out var cached)) return cached;
        var mat = new ShaderMaterial { Shader = new Shader { Code = ForestRiverCode } };
        mat.SetShaderParameter("n1", Noise(2601, 0.03f, true, 512, 5f));
        mat.SetShaderParameter("n2", Noise(2602, 0.06f, true, 512, 3f));
        mat.SetShaderParameter("macro", ForestMacro);
        if (time == TimeOfDay.Night)
        {
            mat.SetShaderParameter("deep", new Color(0.008f, 0.012f, 0.014f));
            mat.SetShaderParameter("shallow", new Color(0.025f, 0.03f, 0.03f));
            mat.SetShaderParameter("foam_col", new Color(0.2f, 0.22f, 0.24f));
        }
        Cache[key] = mat;
        return mat;
    }
}
