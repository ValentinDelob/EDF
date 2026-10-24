using Godot;

namespace Rempart;

/// <summary>
/// Matières de la base militaire du désert : sable ridé par le vent (sol, dunes), grès en strates (affleurements,
/// mesas), toile de camouflage ajourée. Textures générées en C# (en parallèle), shader « Weathered » partagé.
/// Teintes volontairement ternes : l'étalonnage gris de l'invasion en retire encore la moitié de la saturation.
/// </summary>
public static partial class Mats
{
    /// <summary>
    /// Sable du désert (8 m, triplanaire monde, teinté par sommet) : rides de vent ondulées, sable plus clair sur les
    /// crêtes, graviers et cailloux sombres, croûte craquelée par plaques, traces de chenilles effacées.
    /// </summary>
    public static ShaderMaterial DesertSand()
    {
        if (TryCache<ShaderMaterial>("desertsand", out var cached)) return cached;
        const int s = 512;
        var alb = new Px(s, s);
        var rough = new Px(s, s, 1);
        var bump = new Px(s, s);
        var sand = new Color(0.55f, 0.48f, 0.38f);
        var pale = new Color(0.63f, 0.57f, 0.47f);
        var dark = new Color(0.36f, 0.31f, 0.25f);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                // Rides : ondes obliques (~22 cm) déformées par un bruit lent, plus marquées par endroits.
                var warp = ValueNoise(x, y, 64, s / 64, 1301) * 26f + ValueNoise(x, y, 16, s / 16, 1302) * 5f;
                var phase = (x * 0.8f + y * 0.6f + warp) / 14f * Mathf.Tau;
                var ripple = 0.5f + 0.5f * Mathf.Sin(phase);
                ripple = Mathf.Pow(ripple, 1.6f);
                var amp = Mathf.SmoothStep(0.25f, 0.75f, ValueNoise(x, y, 128, s / 128, 1303));
                var grain = Hash(x, y, 1304);
                var patch = ValueNoise(x, y, 64, s / 64, 1305) * 0.6f + ValueNoise(x, y, 16, s / 16, 1306) * 0.4f;
                var col = sand.Lerp(pale, Mathf.SmoothStep(0.45f, 0.8f, patch) * 0.7f + ripple * amp * 0.25f);
                col *= 0.9f + grain * 0.14f;
                var hgt = 0.5f + ripple * amp * 0.35f + grain * 0.05f;
                var r = 0.97f;
                // Croûte craquelée (sol tassé) : plaques plus sombres et polygonales.
                var crust = ValueNoise(x, y, 32, s / 32, 1307);
                if (crust > 0.72f)
                {
                    var k = Mathf.SmoothStep(0.72f, 0.78f, crust);
                    var cell = ValueNoise(x, y, 6, s / 6, 1308);
                    var crack = Mathf.Abs(cell - 0.5f) < 0.025f;
                    col = col.Lerp(dark * 1.25f, k * 0.35f);
                    if (crack) { col = col.Lerp(dark, k * 0.45f); hgt -= 0.12f * k; }
                    hgt = Mathf.Lerp(hgt, 0.45f, k * 0.6f);
                }
                // Graviers et cailloux épars.
                // Graviers épars : petites taches arrondies (bruit lissé), pas de pixels carrés.
                var peb = ValueNoise(x, y, 4, s / 4, 1309) * 0.75f + ValueNoise(x, y, 2, s / 2, 1311) * 0.25f;
                if (peb > 0.8f)
                {
                    var t = ValueNoise(x, y, 8, s / 8, 1310);
                    var kp = Mathf.SmoothStep(0.8f, 0.86f, peb);
                    col = col.Lerp(dark.Lerp(new Color(0.5f, 0.45f, 0.38f), t) * (0.85f + grain * 0.25f), kp * 0.8f);
                    hgt += 0.22f * kp;
                    r = Mathf.Lerp(r, 0.8f, kp);
                }
                alb.Set(x, y, col);
                rough.Set(x, y, r);
                bump.Set(x, y, new Color(hgt, hgt, hgt));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 3.5f), ImageTexture.CreateFromImage(rough.ToImage()), 2, Vector3.One / 8f)
            .With("roughness", 1f).With("normal_depth", 0.85f).With("vertex_color", true).With("grime", 0.45f).With("streaks", 0f)
            .With("foot", 0f).With("puddles", 0f).With("desat", 0.1f).With("macro_var", 1.3f).With("specular", 0.3f);
        Cache["desertsand"] = mat;
        return mat;
    }

    /// <summary>
    /// Grès du désert (triplanaire monde, 6 m, teinté par sommet) : strates horizontales de dureté inégale, lits plus
    /// sombres, éclats et fissures verticales, vernis du désert sombre en coulures.
    /// </summary>
    public static ShaderMaterial DesertRock()
    {
        if (TryCache<ShaderMaterial>("desertrock", out var cached)) return cached;
        const int s = 512;
        var alb = new Px(s, s);
        var bump = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                // Strates : la hauteur de chaque lit varie lentement le long du banc.
                var wob = ValueNoise(x, y, 64, s / 64, 1401) * 30f;
                var yy = y + wob;
                var bed = Mathf.FloorToInt(yy / 22f);
                var ly = Mathf.PosMod(yy, 22f) / 22f;
                var tone = 0.75f + Hash(bed, 3, 1402) * 0.35f;
                var hard = Hash(bed, 4, 1403);
                var grain = Hash(x, y, 1404) * 0.12f + ValueNoise(x, y, 4, s / 4, 1405) * 0.15f;
                var col = new Color(0.5f, 0.41f, 0.33f) * (tone + grain - 0.1f);
                var hgt = 0.55f + grain + (hard > 0.5f ? 0.15f : -0.1f) * Mathf.SmoothStep(0f, 0.2f, ly) * Mathf.SmoothStep(1f, 0.8f, ly);
                if (ly < 0.08f) { col *= 0.62f; hgt -= 0.25f; }
                // Fissures verticales.
                var crack = VNoise(x, y, 4, 64, s, 1406);
                if (Mathf.Abs(crack - 0.5f) < 0.018f) { col *= 0.45f; hgt -= 0.3f; }
                // Vernis du désert (patine sombre en coulures).
                var varnish = VNoise(x, y, 8, 128, s, 1407) * 0.7f + ValueNoise(x, y, 32, s / 32, 1408) * 0.3f;
                if (varnish > 0.62f) col = col.Lerp(new Color(0.18f, 0.14f, 0.12f), (varnish - 0.62f) * 1.8f);
                alb.Set(x, y, col);
                bump.Set(x, y, new Color(hgt, hgt, hgt));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 5f), null, 2, Vector3.One / 6f)
            .With("roughness", 0.95f).With("normal_depth", 1f).With("vertex_color", true).With("grime", 0.6f).With("streaks", 0.5f)
            .With("foot", 0.3f).With("desat", 0.15f);
        Cache["desertrock"] = mat;
        return mat;
    }

    /// <summary>Dalles de béton du tarmac et de la piste au désert : celles du port, sans flaques, éclaircies et poudrées.</summary>
    public static ShaderMaterial DesertApron()
    {
        if (TryCache<ShaderMaterial>("desertapron", out var cached)) return cached;
        var mat = ((ShaderMaterial)Apron().Duplicate())
            .With("puddles", 0f).With("albedo", new Color(1.1f, 1.06f, 0.98f)).With("grime", 0.55f).With("macro_var", 0.7f).With("uv_scale", Vector3.One / 12f);
        Cache["desertapron"] = mat;
        return mat;
    }

    /// <summary>Enrobé des routes du désert : sans flaques, grisé par le soleil et le sable.</summary>
    public static ShaderMaterial DesertAsphalt()
    {
        if (TryCache<ShaderMaterial>("desertasphalt", out var cached)) return cached;
        var mat = ((ShaderMaterial)Asphalt().Duplicate())
            .With("puddles", 0f).With("albedo", new Color(1.25f, 1.2f, 1.1f)).With("grime", 0.6f);
        Cache["desertasphalt"] = mat;
        return mat;
    }

    /// <summary>
    /// Peinture des pistes et des hélisurfaces : texture de peinture routière usée (effacée par plaques) mais nettement
    /// plus claire, teintée par sommet (blanc, jaune de guidage).
    /// </summary>
    public static StandardMaterial3D AirfieldPaint()
    {
        if (TryCache<StandardMaterial3D>("airfieldpaint", out var cached)) return cached;
        var mat = (StandardMaterial3D)RoadPaint().Duplicate();
        mat.VertexColorUseAsAlbedo = true;
        mat.AlbedoColor = new Color(1.9f, 1.88f, 1.8f);
        Cache["airfieldpaint"] = mat;
        return mat;
    }

    /// <summary>Filet de camouflage désert : mailles et lambeaux de toile ajourés (ciseau alpha), vus des deux côtés.</summary>
    public static StandardMaterial3D CamoNet()
    {
        if (TryCache<StandardMaterial3D>("camonet", out var cached)) return cached;
        const int s = 256; // 2 m
        var px = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var mesh = x % 12 < 1 || y % 12 < 1;
                var tatter = ValueNoise(x, y, 8, s / 8, 1501) * 0.7f + ValueNoise(x, y, 2, s / 2, 1502) * 0.3f;
                var garnish = tatter > 0.5f;
                var tone = ValueNoise(x, y, 32, s / 32, 1503);
                var c = tone > 0.55f ? new Color(0.5f, 0.44f, 0.33f) : tone > 0.4f ? new Color(0.38f, 0.33f, 0.25f) : new Color(0.27f, 0.25f, 0.2f);
                c *= 0.85f + Hash(x, y, 1504) * 0.2f;
                px.Set(x, y, garnish ? c : mesh ? new Color(0.2f, 0.19f, 0.16f) : new Color(0, 0, 0, 0));
            }
        });
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(px.ToImage()),
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = 0.5f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 1f,
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = new Vector3(0.5f, 0.5f, 0.5f),
        };
        Cache["camonet"] = mat;
        return mat;
    }
}
