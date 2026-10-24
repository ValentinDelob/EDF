using Godot;

namespace Rempart;

/// <summary>
/// Matières du réseau souterrain (<see cref="UndergroundBuilder"/>) : voussoirs de béton des tunnels, carrelage
/// biseauté des stations, roche humide, paroi de ruche, sol mouillé, brique du collecteur. Toutes triplanaires en
/// repère monde (la coque du réseau n'a pas d'UV) et teintées par sommet (occlusion, encrassement près du sol).
/// </summary>
public static partial class Mats
{
    /// <summary>
    /// Revêtement d'un tunnel en voussoirs de béton préfabriqués (6 m) : anneaux de 1,5 m, joints entre voussoirs,
    /// alvéoles des boulons, efflorescences calcaires blanches, traces d'infiltration rouille, béton taché.
    /// </summary>
    public static ShaderMaterial TunnelLining()
    {
        if (TryCache<ShaderMaterial>("tunnellining", out var cached)) return cached;
        const int s = 512; // 6 m : 85 px/m
        var alb = new Px(s, s);
        var rough = new Px(s, s, 1);
        var bump = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                // Anneaux de 1,5 m (lignes verticales x constant), voussoirs de 3 m (lignes horizontales décalées par anneau).
                var ring = x / 128;
                var lx = x % 128;
                var segOff = ring % 2 == 0 ? 0 : 128;
                var ly = (y + segOff) % 256;
                var joint = lx < 3 || ly < 3;
                var grain = Hash(x, y, 1701) * 0.08f + ValueNoise(x, y, 4, s / 4, 1702) * 0.12f + ValueNoise(x, y, 32, s / 32, 1703) * 0.14f;
                var tone = 0.42f + (Hash(ring, (y + segOff) / 256, 1704) - 0.5f) * 0.07f + grain - 0.17f;
                var col = new Color(tone, tone * 0.98f, tone * 0.93f);
                var hgt = 0.7f + grain * 0.5f;
                var r = 0.88f;
                // Alvéoles des boulons de part et d'autre des joints.
                if ((Mathf.Abs(lx - 10) < 4 || Mathf.Abs(lx - 118) < 4) && (Mathf.Abs(ly - 64) < 5 || Mathf.Abs(ly - 192) < 5)) { col *= 0.45f; hgt = 0.35f; }
                if (joint) { col = new Color(0.1f, 0.1f, 0.095f); hgt = 0.25f; r = 0.7f; }
                // Infiltrations : traînées d'efflorescence blanche depuis les joints horizontaux, et de rouille.
                var drip = VNoise(x, y, 4, 128, s, 1705);
                var below = ly / 256f;
                if (drip > 0.62f) col = col.Lerp(new Color(0.62f, 0.62f, 0.58f), Mathf.Clamp((drip - 0.62f) * 3f, 0, 1) * Mathf.Exp(-below * 2.5f) * 0.8f);
                var rust = VNoise(x, y, 8, 64, s, 1706);
                if (rust > 0.72f) col = col.Lerp(new Color(0.22f, 0.12f, 0.06f), Mathf.Clamp((rust - 0.72f) * 3f, 0, 1) * 0.7f);
                // Taches d'humidité larges.
                var damp = ValueNoise(x, y, 64, s / 64, 1707);
                if (damp > 0.6f) { col *= 1f - (damp - 0.6f) * 0.9f; r = Mathf.Lerp(r, 0.45f, (damp - 0.6f) * 2f); }
                alb.Set(x, y, col);
                rough.Set(x, y, r);
                bump.Set(x, y, new Color(hgt, hgt, hgt));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 5f), ImageTexture.CreateFromImage(rough.ToImage()), 2, Vector3.One / 6f)
            .With("roughness", 1f).With("normal_depth", 1f).With("vertex_color", true).With("streaks", 1.4f).With("grime", 1.1f)
            .With("foot", 1.2f).With("desat", 0.2f);
        Cache["tunnellining"] = mat;
        return mat;
    }

    /// <summary>
    /// Carrelage biseauté de station (2 m) : carreaux blancs de 15 × 7,5 cm posés à joints décalés, ternis et jaunis,
    /// carreaux manquants (mortier gris), fissures, coulures noires.
    /// </summary>
    public static ShaderMaterial StationTiles()
    {
        if (TryCache<ShaderMaterial>("stationtiles", out var cached)) return cached;
        const int s = 512; // 2 m : 256 px/m
        const int tw = 38, th = 19;
        var alb = new Px(s, s);
        var rough = new Px(s, s, 1);
        var bump = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var row = y / th;
                var off = row % 2 == 0 ? 0 : tw / 2;
                var col_ = (x + off) / tw;
                var lx = (x + off) % tw;
                var ly = y % th;
                var tile = (col_ % 14 + 14) % 14;
                var grain = Hash(x, y, 1711) * 0.05f + ValueNoise(x, y, 8, s / 8, 1712) * 0.06f;
                var tone = 0.78f + Hash(tile, row, 1713) * 0.12f + grain;
                // Biseau : bord plus sombre, centre bombé.
                var ex = Mathf.Min(lx, tw - 1 - lx) / 4f;
                var ey = Mathf.Min(ly, th - 1 - ly) / 4f;
                var bev = Mathf.Clamp(Mathf.Min(ex, ey), 0, 1);
                var joint = lx < 2 || ly < 2;
                var c = new Color(tone, tone * 0.97f, tone * 0.88f) * (0.82f + 0.18f * bev);
                var hgt = 0.55f + 0.4f * bev;
                var r = 0.22f;
                if (joint) { c = new Color(0.36f, 0.35f, 0.32f); hgt = 0.2f; r = 0.9f; }
                // Carreaux manquants : mortier gris strié.
                if (!joint && Hash(tile, row, 1714) < 0.012f) { c = new Color(0.38f, 0.37f, 0.34f) * (0.85f + grain * 2f); hgt = 0.1f; r = 0.95f; }
                // Fissures.
                var cr = VNoise(x, y, 64, 16, s, 1715);
                if (Mathf.Abs(cr - 0.5f) < 0.003f) { c *= 0.6f; hgt -= 0.15f; }
                // Jaunissement et crasse.
                var dirt = ValueNoise(x, y, 64, s / 64, 1716) * 0.6f + ValueNoise(x, y, 16, s / 16, 1717) * 0.4f;
                c = c.Lerp(new Color(0.42f, 0.36f, 0.25f), Mathf.Clamp((dirt - 0.35f) * 0.9f, 0, 0.55f));
                alb.Set(x, y, c);
                rough.Set(x, y, r + dirt * 0.35f);
                bump.Set(x, y, new Color(hgt, hgt, hgt));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 3f), ImageTexture.CreateFromImage(rough.ToImage()), 2, Vector3.One / 2f)
            .With("roughness", 1f).With("normal_depth", 0.8f).With("vertex_color", true).With("streaks", 1.6f).With("grime", 1.3f)
            .With("foot", 1.3f).With("desat", 0.25f).With("specular", 0.6f);
        Cache["stationtiles"] = mat;
        return mat;
    }

    /// <summary>Roche humide (8 m) : bancs fracturés gris-brun sombres, veines, suintements luisants.</summary>
    public static ShaderMaterial CaveRock()
    {
        if (TryCache<ShaderMaterial>("caverock", out var cached)) return cached;
        const int s = 512;
        var alb = new Px(s, s);
        var rough = new Px(s, s, 1);
        var bump = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var n1 = ValueNoise(x, y, 64, s / 64, 1721);
                var n2 = ValueNoise(x, y, 16, s / 16, 1722);
                var n3 = ValueNoise(x, y, 4, s / 4, 1723);
                var wob = ValueNoise(x, y, 128, s / 128, 1724) * 40f;
                var bed = Mathf.PosMod(y + wob, 48f) / 48f;
                var tone = 0.24f + n1 * 0.12f + n2 * 0.08f + n3 * 0.05f;
                var col = new Color(tone, tone * 0.95f, tone * 0.88f);
                var hgt = 0.4f + n2 * 0.35f + n3 * 0.2f + (bed < 0.06f ? -0.3f : 0f);
                if (bed < 0.06f) col *= 0.55f;
                // Fractures.
                var fr = VNoise(x, y, 8, 48, s, 1725);
                if (Mathf.Abs(fr - 0.5f) < 0.02f) { col *= 0.4f; hgt -= 0.35f; }
                // Veines de calcite.
                var vein = VNoise(y, x, 6, 96, s, 1726);
                if (Mathf.Abs(vein - 0.5f) < 0.008f) col = col.Lerp(new Color(0.55f, 0.53f, 0.48f), 0.7f);
                var wet = ValueNoise(x, y, 32, s / 32, 1727);
                alb.Set(x, y, col);
                rough.Set(x, y, wet > 0.55f ? 0.35f : 0.85f);
                bump.Set(x, y, new Color(hgt, hgt, hgt));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 7f), ImageTexture.CreateFromImage(rough.ToImage()), 2, Vector3.One / 8f)
            .With("roughness", 1f).With("normal_depth", 1.3f).With("vertex_color", true).With("streaks", 0.8f).With("grime", 0.7f)
            .With("foot", 0.6f).With("desat", 0.15f).With("macro_var", 1.4f);
        Cache["caverock"] = mat;
        return mat;
    }

    /// <summary>
    /// Paroi de ruche : sécrétion durcie brun-olive en cellules irrégulières (alvéoles creusées), nervures, vernis
    /// humide ; veines faiblement luminescentes (vert maladif). Triplanaire monde, teinte par sommet.
    /// </summary>
    public static ShaderMaterial HiveWall()
    {
        if (TryCache<ShaderMaterial>("hivewall", out var cached)) return cached;
        const int s = 512; // 6 m
        var alb = new Px(s, s);
        var bump = new Px(s, s);
        var rough = new Px(s, s);
        var emis = new Px(s, s);
        // Cellules de Voronoï périodiques (alvéoles).
        const int cells = 8;
        var cs = s / (float)cells;
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var gx = Mathf.FloorToInt(x / cs);
                var gy = Mathf.FloorToInt(y / cs);
                float d1 = 1e9f, d2 = 1e9f;
                var id = 0;
                for (var j = -1; j <= 1; j++)
                for (var i = -1; i <= 1; i++)
                {
                    var cx = gx + i;
                    var cy = gy + j;
                    var wx = ((cx % cells) + cells) % cells;
                    var wy = ((cy % cells) + cells) % cells;
                    var px = (cx + 0.15f + Hash(wx, wy, 1731) * 0.7f) * cs;
                    var py = (cy + 0.15f + Hash(wx, wy, 1732) * 0.7f) * cs;
                    var dd = (px - x) * (px - x) + (py - y) * (py - y);
                    if (dd < d1) { d2 = d1; d1 = dd; id = wx * 31 + wy; }
                    else if (dd < d2) d2 = dd;
                }
                var edge = (Mathf.Sqrt(d2) - Mathf.Sqrt(d1)) / cs; // 0 sur la cloison
                var depth = Mathf.Clamp(Mathf.Sqrt(d1) / (cs * 0.55f), 0, 1); // 0 au fond de l'alvéole
                var n = ValueNoise(x, y, 8, s / 8, 1733) * 0.5f + ValueNoise(x, y, 32, s / 32, 1734) * 0.5f;
                var wall = edge < 0.09f;
                var tone = Hash(id, 7, 1735);
                var c = wall
                    ? new Color(0.36f, 0.3f, 0.19f).Lerp(new Color(0.48f, 0.4f, 0.25f), n)
                    : new Color(0.1f, 0.08f, 0.05f).Lerp(new Color(0.3f, 0.23f, 0.13f), depth * (0.6f + tone * 0.4f));
                var h = wall ? 0.85f + n * 0.15f - edge : 0.15f + depth * 0.5f;
                // Nervures (filaments) qui courent sur la paroi.
                var vein = VNoise(x, y, 4, 64, s, 1736);
                var v = Mathf.Clamp(1f - Mathf.Abs(vein - 0.5f) * 40f, 0, 1);
                c = c.Lerp(new Color(0.42f, 0.38f, 0.22f), v * 0.6f);
                h += v * 0.2f;
                alb.Set(x, y, c);
                bump.Set(x, y, new Color(h, h, h));
                rough.Set(x, y, new Color(wall ? 0.35f : 0.12f, 0, 0));
                // Lueur : fond de certaines alvéoles et filaments.
                var glow = v * 0.7f + (!wall && tone > 0.9f ? (1f - depth) * 0.35f : 0f);
                emis.Set(x, y, new Color(0.35f, 0.75f, 0.32f) * Mathf.Clamp(glow, 0, 1));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 8f), ImageTexture.CreateFromImage(rough.ToImage()), 2, Vector3.One / 6f)
            .With("roughness", 1f).With("normal_depth", 1.4f).With("vertex_color", true).With("specular", 0.7f).With("grime", 0.4f)
            .With("streaks", 0.3f).With("foot", 0.2f).With("macro_var", 0.6f)
            .With("emission_tex", ImageTexture.CreateFromImage(emis.ToImage())).With("emission_energy", 0.7f).With("emission_patch", 0.045f);
        Cache["hivewall"] = mat;
        return mat;
    }

    /// <summary>Sol de béton mouillé (dalles de 4 m, joints, taches d'huile), flaques luisantes en coordonnées monde.</summary>
    public static ShaderMaterial WetFloor()
    {
        if (TryCache<ShaderMaterial>("wetfloor", out var cached)) return cached;
        var mat = (ShaderMaterial)Apron().Duplicate();
        mat.With("puddles", 1.25f).With("grime", 1.3f).With("albedo", new Color(0.72f, 0.72f, 0.7f)).With("macro_var", 1.3f);
        Cache["wetfloor"] = mat;
        return mat;
    }

    /// <summary>Sol de grotte : terre et roche broyée, boue luisante dans les creux.</summary>
    public static ShaderMaterial CaveFloor()
    {
        if (TryCache<ShaderMaterial>("cavefloor", out var cached)) return cached;
        var mat = Weathered(ColorNoise(1741, 0.04f, new Color(0.14f, 0.12f, 0.1f), new Color(0.3f, 0.27f, 0.22f)), Noise(1742, 0.08f, true, 512, 6f), null, 2, Vector3.One / 5f)
            .With("roughness", 0.9f).With("normal_depth", 1.1f).With("vertex_color", true).With("puddles", 0.35f).With("grime", 1.2f).With("desat", 0.2f);
        Cache["cavefloor"] = mat;
        return mat;
    }

    /// <summary>Ballast de voie : pierres concassées sombres, huileuses.</summary>
    public static ShaderMaterial Ballast()
    {
        if (TryCache<ShaderMaterial>("ballast", out var cached)) return cached;
        var mat = (ShaderMaterial)Gravel().Duplicate();
        mat.With("albedo", new Color(0.42f, 0.4f, 0.38f)).With("uv_scale", Vector3.One * 0.6f).With("puddles", 0.3f);
        Cache["ballast"] = mat;
        return mat;
    }
}
