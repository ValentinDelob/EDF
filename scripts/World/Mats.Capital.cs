using Godot;

namespace Rempart;

/// <summary>
/// Matières de la capitale en ruines : façade d'immeuble haussmannien (pierre de taille, portes-fenêtres, balconnets),
/// rez-de-chaussée à bossages et devantures, zinc des combles, pierre de taille des monuments, des quais et des ponts,
/// pavés de granit, cuivre oxydé des dômes, dorures ternies, ferronnerie ajourée des balcons et des parapets.
/// Textures générées en C# (en parallèle), shader « Weathered » partagé ; teintes ternes (l'étalonnage gris de
/// l'invasion en retire encore une part de la saturation).
/// </summary>
public static partial class Mats
{
    /// <summary>Largeur d'une travée de façade haussmannienne (m) et hauteur d'un étage courant (m).</summary>
    public const float BayW = 3f, StoryH = 3.3f;

    /// <summary>Hauteur du rez-de-chaussée (boutiques à bossages, entresol compris).</summary>
    public const float ShopStoryH = 4.4f;

    /// <summary>Pierre calcaire (teinte de base des façades et des monuments).</summary>
    private static readonly Color Limestone = new(0.62f, 0.58f, 0.5f);

    /// <summary>
    /// Étages d'un immeuble haussmannien : 4 travées × 4 étages (1 U = une travée de 3 m, 1 V = un étage de 3,3 m, V compté
    /// depuis le haut de l'étage). Pierre de taille en assises de 42 cm à joints décalés, bandeau d'étage, portes-fenêtres
    /// à chambranle mouluré et clé de linteau, croisées sombres à imposte, balconnets de fonte ; persiennes closes, carreaux
    /// soufflés (trou noir), fenêtres incendiées (panache de suie au-dessus), coulures sous les appuis, impacts d'éclats.
    /// </summary>
    public static ShaderMaterial HaussmannFacade(bool night)
    {
        var key = $"haussfacade{night}";
        if (TryCache<ShaderMaterial>(key, out var cached)) return cached;
        const int perU = 128, perF = 140, bays = 4, floors = 4;
        const int w = perU * bays, h = perF * floors;
        var alb = new Px(w, h);
        var rough = new Px(w, h, 1);
        var bump = new Px(w, h);
        var emit = night ? new Px(w, h, 3) : null;
        var soot = new Color(0.03f, 0.028f, 0.026f);
        var dirt = new Color(0.2f, 0.185f, 0.16f);
        const float winHalf = 0.22f, winTop = 0.16f, winBot = 0.94f;
        bool Burnt(int bx, int f) => Hash(((bx % bays) + bays) % bays, ((f % floors) + floors) % floors, 1501) < 0.13f;
        System.Threading.Tasks.Parallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var fl = y / perF;
                var ly = (y % perF) / (float)perF;     // 0 = haut de l'étage
                var bx = x / perU;
                var lx = (x % perU) / (float)perU;
                var grime = ValueNoise(x, y, 32, w / 32, 1502) * 0.6f + ValueNoise(x, y, 8, w / 8, 1503) * 0.4f;
                var grain = ValueNoise(x, y, 2, w / 2, 1504) * 0.5f + Hash(x, y, 1505) * 0.5f;
                var wh = Hash(bx, fl, 1506);
                var wh2 = Hash(bx, fl, 1507);
                var burnt = Burnt(bx, fl);
                var broken = !burnt && Hash(bx, fl, 1508) < 0.22f;
                var shut = !burnt && !broken && wh < 0.3f;
                var boarded = !burnt && !broken && !shut && wh2 < 0.08f;
                var sn = VNoise(x, y, 3, 18, w, 1509);
                var sootK = 0f;
                if (burnt && ly < winTop) sootK = Soot(winTop - ly, lx, winHalf * 2f, sn);
                if (Burnt(bx, fl + 1)) sootK = Mathf.Max(sootK, Soot(1f - ly + winTop, lx, winHalf * 2f, sn));
                var dx = Mathf.Abs(lx - 0.5f);
                var inWin = dx < winHalf && ly > winTop && ly < winBot;
                var inFrame = !inWin && dx < winHalf + 0.055f && ly > winTop - 0.05f && ly < winBot;
                Color c;
                float r, hh;
                var e = new Color(0, 0, 0);
                if (inWin)
                {
                    var wy = (ly - winTop) / (winBot - winTop);
                    var mull = Mathf.Abs(lx - 0.5f) < 0.012f || Mathf.Abs(wy - 0.26f) < 0.014f || dx > winHalf - 0.012f;
                    var refl = Mathf.Lerp(0.2f, 0.07f, wy) * (0.7f + 0.6f * ValueNoise(x, y, 48, w / 48, 1510));
                    var glass = new Color(0.05f, 0.052f, 0.055f) + new Color(refl * 0.8f, refl * 0.84f, refl * 0.88f);
                    r = 0.1f + grime * 0.1f;
                    hh = 0.18f;
                    if (burnt) { c = new Color(0.016f, 0.014f, 0.013f) * (0.8f + grain * 0.4f); r = 0.95f; hh = 0.05f; }
                    else if (broken)
                    {
                        var shard = ValueNoise(x, y, 5, w / 5, 1511) > 0.64f && (wy < 0.3f || dx > winHalf * 0.6f);
                        c = shard ? glass : new Color(0.022f, 0.021f, 0.02f);
                        if (mull && !shard) c = new Color(0.16f, 0.15f, 0.14f);
                        r = shard ? 0.1f : 0.95f;
                        hh = shard ? 0.2f : 0.08f;
                    }
                    else if (shut)
                    {
                        // Persiennes de fer repliées closes : lames horizontales grises, rouille.
                        var slat = (y % 6) < 2 ? 0.7f : 1f;
                        var rust = ValueNoise(x, y, 6, w / 6, 1512) > 0.7f;
                        c = (rust ? new Color(0.24f, 0.15f, 0.1f) : new Color(0.33f, 0.34f, 0.33f)) * slat * (0.85f + grain * 0.25f);
                        r = 0.7f;
                        hh = 0.55f;
                    }
                    else if (boarded)
                    {
                        var plank = (x / 9 + y / 70) % 2 == 0 ? 1f : 0.85f;
                        c = new Color(0.3f, 0.24f, 0.17f) * plank * (0.8f + grain * 0.3f);
                        r = 0.9f;
                        hh = 0.6f;
                    }
                    else c = mull ? new Color(0.36f, 0.35f, 0.32f) : glass;
                    // Balconnet de fonte sur le bas de la porte-fenêtre : barreaux, lisses, volutes.
                    if (wy > 0.78f && !burnt)
                    {
                        var bar = (x % 7) < 2;
                        var rail = Mathf.Abs(wy - 0.8f) < 0.012f || Mathf.Abs(wy - 0.985f) < 0.01f;
                        var cx = (x % 14) - 7f;
                        var cy = (wy - 0.89f) * perF * 0.8f;
                        var ring = Mathf.Abs(Mathf.Sqrt(cx * cx + cy * cy) - 4.5f) < 1f;
                        if (bar || rail || ring) { c = new Color(0.05f, 0.05f, 0.05f); hh = 0.95f; r = 0.6f; }
                    }
                    c = c.Lerp(soot, sootK * 0.5f);
                    if (night && !burnt && !broken && !shut && !boarded && wh2 > 0.94f)
                        e = new Color(1f, 0.7f, 0.4f) * (0.35f + 0.3f * (1 - wy));
                }
                else
                {
                    // Pierre de taille : assises de 42 cm, joints verticaux décalés d'une assise à l'autre.
                    var yy = (fl * perF + (y % perF)) / (float)perF * 3.3f;   // mètres depuis le haut de la tuile
                    var course = Mathf.FloorToInt(yy / 0.42f);
                    var cy = Mathf.PosMod(yy, 0.42f);
                    var xm = x / (float)perU * 3f + (course % 2) * 0.55f + Hash(course, 3, 1513) * 0.3f;
                    var block = Mathf.FloorToInt(xm / 1.1f);
                    var cxm = Mathf.PosMod(xm, 1.1f);
                    var joint = cy < 0.018f || cxm < 0.016f;
                    var tone = 0.92f + Hash(block, course, 1514) * 0.13f;
                    c = Limestone * tone * (0.92f + grain * 0.14f);
                    hh = joint ? 0.72f : 0.86f + grain * 0.06f;
                    r = 0.88f;
                    if (joint) c = c.Darkened(0.2f);
                    // Bandeau d'étage mouluré (haut de l'étage) et appui filant sous les fenêtres.
                    if (ly < 0.05f) { c = Limestone * 1.06f * (0.95f + grain * 0.1f); hh = ly < 0.015f ? 0.7f : 1f; }
                    if (inFrame)
                    {
                        // Chambranle mouluré : deux filets, clé saillante au milieu du linteau.
                        var edge = Mathf.Min(winHalf + 0.055f - dx, ly - (winTop - 0.05f));
                        c = Limestone * 1.04f * (0.94f + grain * 0.1f);
                        hh = edge < 0.012f ? 0.75f : 0.97f;
                        if (ly < winTop && dx < 0.045f) { c = Limestone * 1.1f; hh = 1f; }
                    }
                    // Coulures sous les appuis et les bandeaux.
                    var down = ly > winBot ? ly - winBot : ly + 1f - winBot;
                    var within = dx < winHalf + 0.06f ? 1f : 0.3f;
                    var sc = VNoise(x, y, 2, 44, w, 1515) * 0.65f + VNoise(x, y, 5, 120, w, 1516) * 0.35f;
                    var streak = Mathf.Clamp((sc - 0.4f) * 2.4f, 0, 1) * Mathf.Exp(-down * 2.4f) * within;
                    streak = Mathf.Max(streak, Mathf.Clamp((sc - 0.55f) * 2f, 0, 1) * Mathf.Exp(-ly * 5f) * 0.6f);
                    c = c.Lerp(dirt, streak * 0.7f);
                    // Impacts d'éclats groupés.
                    if (ValueNoise(x, y, 64, w / 64, 1517) > 0.6f)
                    {
                        int icx = x / 12, icy = y / 12;
                        if (Hash(icx, icy, 1518) < 0.45f)
                        {
                            var ox = icx * 12 + 3 + Hash(icx, icy, 1519) * 6;
                            var oy = icy * 12 + 3 + Hash(icx, icy, 1520) * 6;
                            var rad = 1.2f + Hash(icx, icy, 1521) * 2.4f;
                            var d = Mathf.Sqrt((x - ox) * (x - ox) + (y - oy) * (y - oy));
                            if (d < rad * 0.55f) { c = new Color(0.08f, 0.07f, 0.065f); hh = 0.4f; r = 1f; }
                            else if (d < rad * 1.6f) { c = c.Lightened(0.15f); hh = 0.78f; }
                        }
                    }
                    var gg = (grime - 0.5f) * 0.12f;
                    c = new Color(c.R + gg, c.G + gg, c.B + gg * 0.9f);
                    c = c.Lerp(soot, sootK);
                }
                alb.Set(x, y, c);
                rough.Set(x, y, r);
                bump.Set(x, y, new Color(hh, hh, hh));
                emit?.Set(x, y, e);
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 4f), ImageTexture.CreateFromImage(rough.ToImage()), 0, new Vector3(1f / bays, 1f / floors, 1f))
            .With("roughness", 1f).With("normal_depth", 0.8f).With("vertex_color", true).With("streaks", 1.1f).With("foot", 0.6f).With("desat", 0.25f);
        if (emit != null)
        {
            mat.SetShaderParameter("emission_tex", ImageTexture.CreateFromImage(emit.ToImage()));
            mat.SetShaderParameter("emission_energy", 2.2f);
        }
        Cache[key] = mat;
        return mat;
    }

    /// <summary>
    /// Rez-de-chaussée haussmannien : 4 travées (1 U = 3 m) sur la hauteur du rez-de-chaussée (1 V = 4,4 m). Bossages
    /// continus (refends horizontaux profonds), devantures : rideaux de fer baissés et rouillés, vitrines soufflées sur une
    /// boutique noire, contreplaqué, boutique incendiée ; bandeau d'enseigne terne, porte cochère de bois vert sombre.
    /// </summary>
    public static ShaderMaterial HaussmannShops(bool night)
    {
        var key = $"haussshops{night}";
        if (TryCache<ShaderMaterial>(key, out var cached)) return cached;
        const int perU = 128, bays = 4, hgt = 188;
        const int w = perU * bays;
        var alb = new Px(w, hgt);
        var rough = new Px(w, hgt, 1);
        var bump = new Px(w, hgt);
        Color[] signs = { new(0.12f, 0.17f, 0.14f), new(0.3f, 0.1f, 0.08f), new(0.08f, 0.08f, 0.09f), new(0.14f, 0.15f, 0.22f), new(0.36f, 0.3f, 0.18f) };
        System.Threading.Tasks.Parallel.For(0, hgt, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var bx = x / perU;
                var lx = (x % perU) / (float)perU;
                var ym = y / (float)hgt * ShopStoryH;   // mètres depuis le haut
                var up = ShopStoryH - ym;               // mètres depuis le sol
                var grain = ValueNoise(x, y, 2, w / 2, 1531) * 0.5f + Hash(x, y, 1532) * 0.5f;
                var grime = ValueNoise(x, y, 24, w / 24, 1533);
                var kind = Hash(bx, 0, 1534);           // 0 rideau, 1 vitrine soufflée, 2 contreplaqué, 3 incendiée, 4 porte cochère
                var door = Hash(bx, 1, 1535) < 0.18f;
                var dx = Mathf.Abs(lx - 0.5f);
                var half = door ? 0.3f : 0.4f;
                var top = door ? 3.6f : 3.15f;
                var arch = door && up > top - 0.6f && dx < half ? (up - (top - 0.6f)) / 0.6f > Mathf.Sqrt(Mathf.Max(0, 1 - dx * dx / (half * half))) : false;
                var inOpen = dx < half && up < top && up > 0.05f && !arch;
                Color c;
                float r, hh;
                if (inOpen)
                {
                    r = 0.8f;
                    hh = 0.3f;
                    if (door)
                    {
                        // Porte cochère : deux vantaux à panneaux, vert sombre écaillé.
                        var panel = Mathf.Abs(lx - 0.5f) < 0.01f || (int)(up / 0.9f) != (int)((up + 0.04f) / 0.9f) || Mathf.Abs(dx - half * 0.5f) < 0.012f;
                        c = new Color(0.09f, 0.13f, 0.1f) * (panel ? 0.6f : 0.9f + grain * 0.3f);
                        if (ValueNoise(x, y, 4, w / 4, 1536) > 0.72f) c = new Color(0.25f, 0.2f, 0.15f);
                        hh = panel ? 0.35f : 0.5f;
                    }
                    else if (kind < 0.42f)
                    {
                        // Rideau de fer : lames ondulées, rouille, bas cabossé.
                        var lame = (y % 5) < 2 ? 0.78f : 1f;
                        var rust = ValueNoise(x, y, 8, w / 8, 1537) * 0.7f + grime * 0.3f;
                        c = new Color(0.35f, 0.36f, 0.36f).Lerp(new Color(0.26f, 0.15f, 0.09f), Mathf.Clamp((rust - 0.45f) * 2f, 0, 1)) * lame * (0.85f + grain * 0.2f);
                        if (up < 0.4f + ValueNoise(x, y, 6, w / 6, 1538) * 0.4f && Hash(bx, 2, 1539) < 0.5f) c = new Color(0.03f, 0.03f, 0.03f);
                        r = 0.6f;
                        hh = (y % 5) < 2 ? 0.4f : 0.6f;
                    }
                    else if (kind < 0.68f)
                    {
                        // Vitrine soufflée : boutique noire, éclats aux montants, comptoir devinable.
                        var shard = ValueNoise(x, y, 5, w / 5, 1540) > 0.66f && (dx > half * 0.75f || up > top - 0.5f);
                        c = shard ? new Color(0.28f, 0.3f, 0.3f) : new Color(0.025f, 0.024f, 0.022f) * (0.8f + grain * 0.4f);
                        if (!shard && up < 1.0f && up > 0.9f) c = new Color(0.08f, 0.07f, 0.06f);
                        r = shard ? 0.15f : 0.95f;
                    }
                    else if (kind < 0.85f)
                    {
                        var plank = (x / 15) % 2 == 0 ? 1f : 0.82f;
                        c = new Color(0.36f, 0.29f, 0.2f) * plank * (0.8f + grain * 0.3f);
                        if (ValueNoise(x, y, 10, w / 10, 1541) > 0.7f) c = c.Darkened(0.4f);
                        hh = 0.55f;
                    }
                    else
                    {
                        c = new Color(0.02f, 0.018f, 0.016f) * (0.8f + grain * 0.5f);
                        r = 1f;
                    }
                }
                else
                {
                    // Bossages : assises de 45 cm à refends profonds, sans joints verticaux.
                    var cy = Mathf.PosMod(up, 0.45f);
                    var groove = cy < 0.06f;
                    c = Limestone * (0.86f + Hash((int)(up / 0.45f), bx, 1542) * 0.1f) * (0.92f + grain * 0.14f);
                    hh = groove ? 0.35f : 0.9f;
                    r = 0.9f;
                    if (groove) c = c.Darkened(0.35f);
                    // Bandeau d'enseigne au-dessus de la devanture.
                    if (!door && dx < half + 0.02f && up > top && up < top + 0.42f)
                    {
                        c = signs[(int)(Hash(bx, 3, 1543) * signs.Length) % signs.Length] * (0.75f + grain * 0.3f + grime * 0.2f);
                        if (up > top + 0.1f && up < top + 0.3f && (x % 9) < 5 && Mathf.Abs(lx - 0.5f) < 0.28f && Hash(x / 9, bx, 1544) < 0.7f)
                            c = new Color(0.55f, 0.52f, 0.44f) * (0.7f + grain * 0.3f); // lettres délavées
                        hh = 0.95f;
                        r = 0.6f;
                    }
                    // Socle encrassé, éclaboussures.
                    if (up < 0.6f) c = c.Lerp(new Color(0.16f, 0.15f, 0.13f), (0.6f - up) / 0.6f * 0.6f);
                    // Suie au-dessus des boutiques incendiées.
                    if (!door && kind >= 0.85f && up > top && dx < half + 0.3f)
                        c = c.Lerp(new Color(0.03f, 0.028f, 0.026f), Mathf.Clamp(1.2f - (up - top) * 0.9f - dx * 1.5f, 0, 0.95f));
                }
                alb.Set(x, y, c);
                rough.Set(x, y, r);
                bump.Set(x, y, new Color(hh, hh, hh));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 4f), ImageTexture.CreateFromImage(rough.ToImage()), 0, new Vector3(1f / bays, 1f, 1f))
            .With("roughness", 1f).With("normal_depth", 0.9f).With("vertex_color", true).With("streaks", 0.9f).With("foot", 1.2f).With("desat", 0.25f);
        Cache[key] = mat;
        return mat;
    }

    /// <summary>
    /// Zinc des combles (1 U = 1 V = 2 m) : feuilles à tasseaux tous les 50 cm, oxydation blanchâtre en coulures, plaques
    /// rapiécées plus sombres, suie. U le long de l'égout, V vers le faîtage.
    /// </summary>
    public static ShaderMaterial Zinc()
    {
        if (TryCache<ShaderMaterial>("zinc", out var cached)) return cached;
        const int s = 256;
        var alb = new Px(s, s);
        var bump = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var seam = (x % 64) < 4;
                var cross = (y % 128) < 2;
                var sheet = Hash(x / 64, y / 128, 1551);
                var ox = VNoise(x, y, 3, 40, s, 1552);
                var c = new Color(0.36f, 0.385f, 0.4f) * (0.88f + sheet * 0.16f);
                c = c.Lerp(new Color(0.55f, 0.56f, 0.55f), Mathf.Clamp((ox - 0.55f) * 2.2f, 0, 0.6f));
                if (sheet < 0.1f) c = c.Darkened(0.35f);
                if (seam) c = c.Lightened(0.1f);
                if (cross) c = c.Darkened(0.25f);
                var hh = seam ? 1f : cross ? 0.45f : 0.6f + Hash(x, y, 1553) * 0.04f;
                alb.Set(x, y, c);
                bump.Set(x, y, new Color(hh, hh, hh));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 5f), null, 0, new Vector3(0.5f, 0.5f, 1f))
            .With("roughness", 0.55f).With("metallic", 0.45f).With("normal_depth", 0.8f).With("vertex_color", true).With("streaks", 1.3f).With("rust", 0.08f).With("desat", 0.2f).With("foot", 0f);
        Cache["zinc"] = mat;
        return mat;
    }

    /// <summary>
    /// Pierre de taille des monuments, quais et ponts (triplanaire monde, tuile de 4 m, teintée par sommet) : assises de
    /// 50 cm, blocs de 1,2 m à joints décalés, épaufrures, croûte noire sous les saillies.
    /// </summary>
    public static ShaderMaterial Ashlar()
    {
        if (TryCache<ShaderMaterial>("ashlar", out var cached)) return cached;
        const int s = 512;
        var alb = new Px(s, s);
        var bump = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var ym = y / 128f;
                var course = Mathf.FloorToInt(ym / 0.5f);
                var cy = Mathf.PosMod(ym, 0.5f);
                var xm = x / 128f + (course % 2) * 0.6f;
                var block = Mathf.FloorToInt(xm / 1.2f);
                var cx = Mathf.PosMod(xm, 1.2f);
                var joint = cy < 0.02f || cx < 0.02f;
                var tone = 0.9f + Hash(((block % 4) + 4) % 4, course % 8, 1561) * 0.16f;
                var grain = ValueNoise(x, y, 3, s / 3, 1562) * 0.5f + Hash(x, y, 1563) * 0.5f;
                var c = Limestone * tone * (0.9f + grain * 0.16f);
                var chip = ValueNoise(x, y, 8, s / 8, 1564);
                var hh = joint ? 0.68f : 0.88f + grain * 0.06f;
                if (!joint && chip > 0.74f && (cx < 0.12f || cy < 0.1f)) { c = c.Lightened(0.08f); hh -= 0.2f; }
                if (joint) c = c.Darkened(0.25f);
                alb.Set(x, y, c);
                bump.Set(x, y, new Color(hh, hh, hh));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 4.5f), null, 2, Vector3.One / 4f)
            .With("roughness", 0.92f).With("normal_depth", 0.9f).With("vertex_color", true).With("streaks", 1.2f).With("foot", 0.8f).With("desat", 0.25f);
        Cache["ashlar"] = mat;
        return mat;
    }

    /// <summary>Pavés de granit (triplanaire monde, tuile de 2 m) : rangs de pavés de 12 × 20 cm, joints sablés, pavés manquants.</summary>
    public static ShaderMaterial Cobbles()
    {
        if (TryCache<ShaderMaterial>("cobbles", out var cached)) return cached;
        const int s = 512;
        var alb = new Px(s, s);
        var bump = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                // 256 px/m : rangs de 12 cm (31 px), pavés de 20 cm (51 px) décalés d'un rang à l'autre.
                var row = y / 31;
                var ry = y % 31;
                var xo = x + (row % 2) * 25 + (int)(Hash(row, 1, 1571) * 12);
                var col = (xo / 51) % 10;
                var rx = xo % 51;
                var edge = Mathf.Min(Mathf.Min(ry, 30 - ry), Mathf.Min(rx, 50 - rx));
                var joint = edge < 2;
                // Contraste retenu : vus de loin, des pavés trop francs font un damier de briques.
                var tone = 0.86f + Hash(col, row % 16, 1572) * 0.16f;
                var grain = Hash(x, y, 1573) * 0.08f + ValueNoise(x, y, 6, s / 6, 1575) * 0.08f;
                var wear = ValueNoise(x, y, 128, s / 128, 1576) * 0.12f;
                var c = new Color(0.4f, 0.39f, 0.38f) * (tone + grain - wear);
                var missing = Hash(col, row % 16, 1574) < 0.012f;
                var hh = joint ? 0.35f : Mathf.Clamp(0.55f + edge * 0.04f, 0, 0.85f) + grain * 0.3f;
                if (!joint && edge < 5) c *= 0.9f + edge * 0.02f;
                if (joint) c *= 0.7f;
                if (missing) { c = new Color(0.2f, 0.18f, 0.16f); hh = 0.15f; }
                alb.Set(x, y, c);
                bump.Set(x, y, new Color(hh, hh, hh));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 4f), null, 2, Vector3.One / 2f)
            .With("roughness", 0.9f).With("normal_depth", 0.65f).With("vertex_color", true).With("puddles", 0.55f).With("desat", 0.3f).With("macro_var", 1.4f);
        Cache["cobbles"] = mat;
        return mat;
    }

    /// <summary>Cuivre oxydé des dômes et des toits monumentaux (triplanaire monde) : vert-de-gris pâle, coulures noires, côtes.</summary>
    public static ShaderMaterial Patina()
    {
        if (TryCache<ShaderMaterial>("patina", out var cached)) return cached;
        const int s = 256;
        var alb = new Px(s, s);
        var bump = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var n = ValueNoise(x, y, 32, s / 32, 1581) * 0.6f + ValueNoise(x, y, 8, s / 8, 1582) * 0.4f;
                var streak = VNoise(x, y, 3, 48, s, 1583);
                var seam = (x % 32) < 2;
                var c = new Color(0.3f, 0.42f, 0.37f).Lerp(new Color(0.42f, 0.5f, 0.44f), n);
                c = c.Lerp(new Color(0.12f, 0.13f, 0.11f), Mathf.Clamp((streak - 0.55f) * 2.5f, 0, 0.75f));
                if (seam) c = c.Darkened(0.3f);
                var hh = seam ? 1f : 0.6f + n * 0.1f;
                alb.Set(x, y, c);
                bump.Set(x, y, new Color(hh, hh, hh));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 4f), null, 2, Vector3.One / 3f)
            .With("roughness", 0.7f).With("metallic", 0.25f).With("normal_depth", 0.6f).With("vertex_color", true).With("streaks", 0.8f).With("desat", 0.15f).With("foot", 0f);
        Cache["patina"] = mat;
        return mat;
    }

    /// <summary>Dorure ternie (statues, lanternons, grilles) : or sombre métallique, encrassé par la fumée.</summary>
    public static StandardMaterial3D Gilt()
    {
        if (TryCache<StandardMaterial3D>("gilt", out var cached)) return cached;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.42f, 0.2f),
            AlbedoTexture = ColorNoise(1591, 0.08f, new Color(0.55f, 0.5f, 0.45f), new Color(1f, 1f, 1f), 256),
            Metallic = 0.85f,
            Roughness = 0.42f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.5f, 0.5f, 0.5f),
        };
        Cache["gilt"] = mat;
        return mat;
    }

    /// <summary>Bronze noirci des statues et des lampadaires monumentaux (triplanaire).</summary>
    public static StandardMaterial3D Bronze()
    {
        if (TryCache<StandardMaterial3D>("bronze", out var cached)) return cached;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.16f, 0.17f, 0.15f),
            AlbedoTexture = ColorNoise(1592, 0.1f, new Color(0.6f, 0.75f, 0.68f), new Color(1f, 1f, 1f), 256),
            Metallic = 0.6f,
            Roughness = 0.55f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.6f, 0.6f, 0.6f),
        };
        Cache["bronze"] = mat;
        return mat;
    }

    /// <summary>
    /// Ferronnerie ajourée (balcons filants, parapets des ponts, grilles du jardin) : lisses haute et basse, barreaux,
    /// volutes ; découpe alpha, visible des deux côtés. UV : 1 U = 1 V = 1 m (V = 0 en haut).
    /// </summary>
    public static StandardMaterial3D IronRail()
    {
        if (TryCache<StandardMaterial3D>("ironrail", out var cached)) return cached;
        const int s = 128;
        var px = new Px(s, s);
        for (var y = 0; y < s; y++)
        for (var x = 0; x < s; x++)
        {
            var rail = y < 6 || (y > 112 && y < 118) || y > 124;
            var bar = (x % 16) < 3;
            var cx = (x % 32) - 16f;
            var cy = y - 60f;
            var d = Mathf.Sqrt(cx * cx * 1.2f + cy * cy * 0.35f);
            var loop = Mathf.Abs(d - 13f) < 1.6f;
            var dia = Mathf.Abs(Mathf.Abs(cx) - Mathf.Abs(cy) * 0.28f) < 1.4f && y > 20 && y < 100;
            var on = rail || bar || loop || dia;
            var rust = ValueNoise(x, y, 8, s / 8, 1593) > 0.68f;
            var c = rust ? new Color(0.17f, 0.1f, 0.07f) : new Color(0.06f, 0.065f, 0.06f);
            c.A = on ? 1f : 0f;
            px.Set(x, y, c);
        }
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(px.ToImage()),
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = 0.5f,
            AlphaAntialiasingMode = BaseMaterial3D.AlphaAntiAliasing.AlphaToCoverage,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.6f,
            Metallic = 0.4f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        Cache["ironrail"] = mat;
        return mat;
    }

    /// <summary>Ardoise sombre (toits des monuments, flèches) : réutilise les tuiles teintées par sommet en gris bleuté.</summary>
    public static ShaderMaterial Slate() => RoofTiles();
}
