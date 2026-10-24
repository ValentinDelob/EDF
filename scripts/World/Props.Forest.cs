using Godot;

namespace Rempart;

/// <summary>
/// Accessoires de la vallée forestière : blocs rocheux moussus, arbres abattus (tronc, galette de racines), souches,
/// piles de grumes, rémanents, fougères, épicéas, pylône électrique, remorque grumière. Convention : posé sur y = 0,
/// avant vers +Z ; maillages en cache, posés par MultiMesh (<see cref="PropScatter"/>).
/// </summary>
public static partial class Props
{
    private static readonly Vector3[] Ico2 = BuildIco(2);

    private static readonly Color Bark = new(0.27f, 0.24f, 0.21f);
    private static readonly Color BarkDark = new(0.17f, 0.15f, 0.13f);
    private static readonly Color Sapwood = new(0.62f, 0.52f, 0.38f);
    private static readonly Color MossCol = new(0.2f, 0.25f, 0.12f);

    /// <summary>Couleur rendue opaque (Color × réel multiplie aussi l'alpha : les cartes en découpe disparaîtraient).</summary>
    private static Color Opaque(Color c) => new(c.R, c.G, c.B, 1f);

    /// <summary>
    /// Bloc de roche (environ 2 m d'envergure à l'échelle 1) : icosaèdre subdivisé, bosselé par un bruit de direction
    /// (sommets partagés identiques : normales lisses), base aplatie et enterrée. Variantes 0 à 5 (5 : dalle allongée).
    /// </summary>
    public static ArrayMesh Boulder(int v) => Cached($"forestboulder{v}", k =>
    {
        var mat = Mats.ForestRock();
        var r = new RandomNumberGenerator { Seed = (ulong)(v * 811 + 3) };
        var p1 = r.RandfRange(0, 10);
        var p2 = r.RandfRange(0, 10);
        var stretch = v == 5 ? new Vector3(1.8f, 0.55f, 1.1f) : new Vector3(r.RandfRange(0.9f, 1.25f), r.RandfRange(0.6f, 0.85f), r.RandfRange(0.85f, 1.15f));
        Vector3 D(Vector3 d)
        {
            var bump = 1f + 0.16f * Mathf.Sin(d.X * 3.1f + p1) * Mathf.Cos(d.Z * 2.7f + p2) + 0.09f * Mathf.Sin(d.Y * 5.3f + d.X * 4f + p2)
                       + 0.05f * Mathf.Sin(d.Z * 9f + d.Y * 7f + p1);
            // Facettes : légère quantification du rayon (arêtes de cassure).
            bump = Mathf.Lerp(bump, Mathf.Snapped(bump, 0.12f), 0.45f);
            var p = new Vector3(d.X * stretch.X, d.Y * stretch.Y, d.Z * stretch.Z) * bump;
            if (p.Y < -0.25f) p.Y = -0.25f + (p.Y + 0.25f) * 0.3f; // base aplatie
            return p + Vector3.Up * 0.22f;
        }
        for (var i = 0; i < Ico2.Length; i += 3)
        {
            var a = D(Ico2[i]);
            var b = D(Ico2[i + 1]);
            var c = D(Ico2[i + 2]);
            var fn = (b - a).Cross(c - a).Normalized();
            if (fn.Dot(a + b + c) < 0) fn = -fn;
            Color Cc(Vector3 d) => new Color(0.82f, 0.82f, 0.8f) * (0.85f + 0.2f * Mathf.Sin(d.X * 5f + d.Z * 3f + p1));
            k.TriSmooth(mat, a, b, c, (Ico2[i] * 0.6f + fn).Normalized(), (Ico2[i + 1] * 0.6f + fn).Normalized(), (Ico2[i + 2] * 0.6f + fn).Normalized(),
                Cc(Ico2[i]), Cc(Ico2[i + 1]), Cc(Ico2[i + 2]));
        }
    });

    /// <summary>
    /// Arbre abattu couché selon +X (longueur 9 à 15 m) : fût écorcé par endroits, mousse sur le dessus, chicots de
    /// branches ; variantes paires : galette de racines arrachée avec sa terre ; impaires : cassure en esquilles.
    /// </summary>
    public static ArrayMesh FallenLog(int v) => Cached($"forestlog{v}", k =>
    {
        var bark = Mats.Detail();
        var r = new RandomNumberGenerator { Seed = (ulong)(v * 613 + 9) };
        var len = r.RandfRange(9f, 15f);
        var rad = r.RandfRange(0.32f, 0.5f);
        var a = new Vector3(-len * 0.35f, rad * 0.8f, 0);
        var b = new Vector3(len * 0.65f, rad * 0.62f, 0);
        k.Col = Bark;
        Taper(k, bark, a, b, rad, rad * 0.55f, 10);
        // Mousse : bande bombée sur le dessus du fût.
        k.Col = MossCol;
        k.Solid = false;
        Taper(k, bark, a + new Vector3(1.2f, rad * 0.55f, 0), b + new Vector3(-2.5f, rad * 0.35f, 0), rad * 0.62f, rad * 0.35f, 7, 0.45f);
        k.Solid = true;
        // Chicots de branches.
        k.Col = BarkDark;
        for (var i = 0; i < 7; i++)
        {
            var t = r.RandfRange(0.25f, 0.95f);
            var p = a.Lerp(b, t);
            var ang = r.RandfRange(0.4f, 2.7f) * (r.Randf() < 0.5f ? 1 : -1);
            var dir = new Vector3(r.RandfRange(0.2f, 0.6f), Mathf.Sin(ang), Mathf.Cos(ang)).Normalized();
            k.Tube(bark, p, p + dir * r.RandfRange(0.6f, 2.2f), r.RandfRange(0.05f, 0.11f), 5);
        }
        if (v % 2 == 0)
        {
            // Galette de racines dressée au pied, terre et cailloux pris dedans.
            var c0 = a + new Vector3(-0.3f, 0.5f, 0);
            k.Col = new Color(0.2f, 0.16f, 0.12f);
            var disc = new Basis(Vector3.Forward, Mathf.Pi / 2f);
            k.Cylinder(bark, c0 + new Vector3(-0.25f, 0, 0), rad * 3.4f, rad * 2.6f, 0.5f, 11, true, disc);
            k.Col = BarkDark;
            for (var i = 0; i < 9; i++)
            {
                var ang = i * Mathf.Tau / 9f + r.RandfRange(-0.2f, 0.2f);
                var d = new Vector3(-0.3f, Mathf.Sin(ang), Mathf.Cos(ang));
                k.Tube(bark, c0 + d * rad * 0.8f, c0 + d * rad * r.RandfRange(3f, 4.2f) + new Vector3(r.RandfRange(-0.6f, 0.2f), 0, 0), r.RandfRange(0.05f, 0.1f), 5);
            }
        }
        else
        {
            // Cassure : esquilles claires dressées.
            k.Col = Sapwood;
            for (var i = 0; i < 6; i++)
            {
                var ang = i * Mathf.Tau / 6f;
                var o = a + new Vector3(0, Mathf.Sin(ang), Mathf.Cos(ang)) * rad * 0.6f;
                k.Tube(bark, o, o + new Vector3(-r.RandfRange(0.3f, 0.9f), r.RandfRange(-0.1f, 0.2f), r.RandfRange(-0.1f, 0.1f)), 0.07f, 4, true);
            }
        }
    });

    /// <summary>Tube conique horizontal ou quelconque (fût, mousse aplatie selon <paramref name="flatY"/>).</summary>
    private static void Taper(MeshKit k, Material m, Vector3 from, Vector3 to, float r0, float r1, int seg, float flatY = 1f)
    {
        var dir = to - from;
        var len = dir.Length();
        var y = dir / len;
        var x = Mathf.Abs(y.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up.Cross(y).Normalized();
        var z = x.Cross(y).Normalized();
        var basis = new Basis(x, y, z).Scaled(new Vector3(1f, 1f, 1f));
        if (flatY < 1f)
        {
            // Aplatissement vertical : on écrase la composante verticale du repère latéral.
            basis = new Basis(x, y, z * flatY);
        }
        k.Cylinder(m, from, r0, r1, len, seg, true, basis);
    }

    /// <summary>Souche coupée à la tronçonneuse (aubier clair, contreforts des racines). Variantes 0 à 2.</summary>
    public static ArrayMesh Stump(int v) => Cached($"foreststump{v}", k =>
    {
        var bark = Mats.Detail();
        var r = new RandomNumberGenerator { Seed = (ulong)(v * 97 + 4) };
        var rad = r.RandfRange(0.28f, 0.42f);
        var h = r.RandfRange(0.35f, 0.7f);
        k.Col = Bark;
        k.Cylinder(bark, new Vector3(0, -0.2f, 0), rad * 1.15f, rad, h + 0.2f, 9, false);
        k.Col = Sapwood * (v == 2 ? 0.6f : 1f);
        k.Cylinder(bark, new Vector3(0, h - 0.02f, 0), rad * 0.97f, rad * 0.95f, 0.03f, 9, true, new Basis(Vector3.Right, r.RandfRange(-0.12f, 0.12f)));
        k.Col = BarkDark;
        for (var i = 0; i < 4; i++)
        {
            var ang = i * Mathf.Tau / 4f + r.RandfRange(-0.4f, 0.4f);
            var d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
            k.Tube(bark, d * rad * 0.7f + Vector3.Up * 0.15f, d * rad * 2.4f + Vector3.Down * 0.15f, 0.1f, 5);
        }
    });

    /// <summary>Pile de grumes ébranchées (≈ 7 m), bouts sciés clairs, trois lits. Variantes 0 à 2.</summary>
    public static ArrayMesh LogPile(int v) => Cached($"forestlogpile{v}", k =>
    {
        var bark = Mats.Detail();
        var r = new RandomNumberGenerator { Seed = (ulong)(v * 211 + 7) };
        var len = r.RandfRange(6f, 8f);
        var rows = new[] { 5, 4, 3 };
        const float rad = 0.3f;
        for (var row = 0; row < rows.Length; row++)
        for (var i = 0; i < rows[row]; i++)
        {
            var z = (i - (rows[row] - 1) / 2f) * rad * 2.05f;
            var y = rad + row * rad * 1.75f;
            var off = r.RandfRange(-0.4f, 0.4f);
            var rr = rad * r.RandfRange(0.85f, 1.1f);
            var from = new Vector3(-len / 2f + off, y, z);
            var to = new Vector3(len / 2f + off, y, z);
            k.Col = Bark * r.RandfRange(0.8f, 1.15f);
            k.Tube(bark, from, to, rr, 8, false);
            k.Col = Sapwood * r.RandfRange(0.75f, 1.05f);
            k.Tube(bark, from - new Vector3(0.01f, 0, 0), from + new Vector3(0.02f, 0, 0), rr * 0.98f, 8, true);
            k.Tube(bark, to - new Vector3(0.02f, 0, 0), to + new Vector3(0.01f, 0, 0), rr * 0.98f, 8, true);
        }
        // Chandelles qui calent la pile.
        k.Col = BarkDark;
        foreach (var x in new[] { -len * 0.3f, len * 0.3f })
        foreach (var sz in new[] { -1f, 1f })
            k.Tube(bark, new Vector3(x, 0, sz * 1.75f), new Vector3(x, 1.6f, sz * 1.55f), 0.07f, 5);
    });

    /// <summary>Tas de rémanents (branches et cimes laissées par la coupe) : traversable, sans ombre.</summary>
    public static ArrayMesh SlashPile(int v) => Cached($"forestslash{v}", k =>
    {
        var bark = Mats.Detail();
        var r = new RandomNumberGenerator { Seed = (ulong)(v * 331 + 2) };
        k.Solid = false;
        for (var i = 0; i < 16; i++)
        {
            var a = new Vector3(r.RandfRange(-2.2f, 2.2f), r.RandfRange(0f, 0.5f), r.RandfRange(-1.4f, 1.4f));
            var b = a + new Vector3(r.RandfRange(-2.5f, 2.5f), r.RandfRange(0.1f, 0.9f), r.RandfRange(-1.5f, 1.5f));
            k.Col = (i % 3 == 0 ? BarkDark : Bark) * r.RandfRange(0.8f, 1.2f);
            k.Tube(bark, a, b, r.RandfRange(0.03f, 0.07f), 4);
        }
        // Rameaux d'aiguilles roussies.
        for (var i = 0; i < 6; i++)
        {
            var c = new Vector3(r.RandfRange(-1.6f, 1.6f), r.RandfRange(0.3f, 0.8f), r.RandfRange(-1f, 1f));
            CanopyLow(k, Ico0, c, new Vector3(r.RandfRange(0.6f, 1.1f), r.RandfRange(0.25f, 0.45f), r.RandfRange(0.5f, 0.9f)), i * 1.7f,
                new Color(0.2f, 0.16f, 0.1f), Mats.Twigs());
        }
    });

    /// <summary>Fougère aigle (≈ 1 m) : frondes arquées découpées, vert éteint ou roussi (variantes impaires). Traversable.</summary>
    public static ArrayMesh Fern(int v) => Cached($"forestfern{v}", k =>
    {
        var leaf = Mats.Foliage();
        var r = new RandomNumberGenerator { Seed = (ulong)(v * 53 + 1) };
        var baseCol = v % 2 == 0 ? new Color(0.13f, 0.17f, 0.08f) : new Color(0.26f, 0.19f, 0.1f);
        var fronds = 6 + v % 2;
        for (var f = 0; f < fronds; f++)
        {
            var ang = f * Mathf.Tau / fronds + r.RandfRange(-0.25f, 0.25f);
            var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
            var side = new Vector3(-dir.Z, 0, dir.X);
            var len = r.RandfRange(0.8f, 1.25f);
            var lift = r.RandfRange(0.55f, 0.9f);
            const int seg = 4;
            var prev = Vector3.Zero;
            var prevW = 0.04f;
            for (var s = 1; s <= seg; s++)
            {
                var t = s / (float)seg;
                var p = dir * len * t + Vector3.Up * (lift * Mathf.Sin(t * 2.2f) * (1f - t * 0.35f));
                var wdt = Mathf.Sin(t * Mathf.Pi) * len * 0.22f + 0.02f;
                var col = baseCol * (0.75f + 0.4f * t) * r.RandfRange(0.9f, 1.1f);
                // Folioles en dents de scie : bord extérieur avancé, bord intérieur en retrait.
                var tooth = side * wdt;
                k.Tri(leaf, prev, p, prev + side * prevW + dir * 0.06f, Vector3.Down, col * 0.8f, col, col);
                k.Tri(leaf, prev, p, prev - side * prevW + dir * 0.06f, Vector3.Down, col * 0.8f, col, col);
                k.Tri(leaf, p, prev + side * prevW + dir * 0.06f, p + tooth + Vector3.Down * 0.05f, Vector3.Down, col, col, col * 1.1f);
                k.Tri(leaf, p, prev - side * prevW + dir * 0.06f, p - tooth + Vector3.Down * 0.05f, Vector3.Down, col, col, col * 1.1f);
                prev = p;
                prevW = wdt;
            }
        }
    });

    /// <summary>
    /// Épicéa (≈ 19 m à l'échelle 1) : fût fin, cône sombre intérieur qui ferme la silhouette, puis verticilles de branches
    /// retombantes en cartes d'aiguilles ajourées (découpe), plus courtes vers la cime. Variantes 0 à 3 (3 : sec, roussi).
    /// Clé « conifer… » : famille arbre (destructible).
    /// </summary>
    public static ArrayMesh Spruce(int v) => Cached($"coniferspruce{v}", k =>
    {
        var r = new RandomNumberGenerator { Seed = (ulong)(v * 1301 + 17) };
        var bark = Mats.Detail();
        var needles = Mats.Needles();
        var core = Mats.Foliage();
        const float h = 19f;
        var dead = v == 3;
        k.Col = BarkDark;
        k.Cylinder(bark, Vector3.Zero, 0.36f, 0.05f, h * 0.97f, 7, false);
        var col = dead ? new Color(0.19f, 0.15f, 0.11f) : new Color(0.07f, 0.095f, 0.075f) * r.RandfRange(0.9f, 1.1f);
        var baseY = h * r.RandfRange(0.16f, 0.24f);
        var maxR = r.RandfRange(3f, 3.6f);
        // Cône intérieur (opaque) : densité de la silhouette sans multiplier les cartes.
        k.Col = Opaque(col * 0.55f);
        k.Cylinder(core, new Vector3(0, baseY + 1.6f, 0), maxR * 0.3f, 0.05f, h - baseY - 1.4f, 7, false);
        var whorls = 13;
        k.Solid = false; // cartes d'aiguilles : jamais solides
        for (var i = 0; i < whorls; i++)
        {
            var t = i / (float)(whorls - 1);
            var y = Mathf.Lerp(baseY, h - 0.6f, t);
            var len = maxR * Mathf.Pow(1f - t, 0.85f) + 0.35f;
            var n = t > 0.75f ? 4 : 6;
            for (var b = 0; b < n; b++)
            {
                var ang = b * Mathf.Tau / n + i * 0.9f + r.RandfRange(-0.25f, 0.25f);
                var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                var side = new Vector3(-dir.Z, 0, dir.X);
                var droop = len * r.RandfRange(0.28f, 0.45f);
                var root = dir * 0.15f + Vector3.Up * y;
                var mid = root + dir * len * 0.55f + Vector3.Up * (len * 0.05f);
                var tip = root + dir * len + Vector3.Down * droop;
                var w1 = len * 0.42f;
                var shade = 0.7f + 0.45f * t;
                var cIn = Opaque(col * (shade * 0.6f));
                var cOut = Opaque((col * shade).Lightened(dead ? 0.05f : 0.12f));
                var cTip = Opaque(cOut.Lightened(0.06f));
                // Carte en deux segments (UV : u de la base à la pointe), légèrement en gouttière (bords abaissés).
                var m0 = mid + side * w1 + Vector3.Down * 0.3f;
                var m1 = mid - side * w1 + Vector3.Down * 0.3f;
                var r0 = root + side * w1 * 0.6f;
                var r1 = root - side * w1 * 0.6f;
                var t0 = tip + side * w1 * 0.55f + Vector3.Down * 0.2f;
                var t1 = tip - side * w1 * 0.55f + Vector3.Down * 0.2f;
                k.QuadC(needles, r1, r0, m0, m1, new Vector2(0, 1), new Vector2(0, 0), new Vector2(0.55f, 0), new Vector2(0.55f, 1), cIn, cIn, cOut, cOut, Vector3.Up);
                k.QuadC(needles, m1, m0, t0, t1, new Vector2(0.55f, 1), new Vector2(0.55f, 0), new Vector2(1, 0), new Vector2(1, 1), cOut, cOut, cTip, cTip, Vector3.Up);
            }
        }
        k.Solid = true;
        // Flèche.
        k.Col = Opaque(col * 0.8f);
        k.Cylinder(core, new Vector3(0, h - 1.4f, 0), 0.35f, 0.02f, 1.8f, 5, false);
    });

    /// <summary>
    /// Feuillu de forêt (hêtre, chêne : ≈ 16 m à l'échelle 1) : fût élancé, charpentières qui montent en éventail,
    /// rameaux, et bouquets de feuillage en cartes croisées ajourées (découpe) au bout des branches, plus quelques
    /// cartes intérieures sombres ; variantes 3 et 4 : arbre mort (ramilles seules). Clé « broadleaf… » : famille arbre.
    /// </summary>
    public static ArrayMesh Beech(int v) => Cached($"broadleafbeech{v}", k =>
    {
        var r = new RandomNumberGenerator { Seed = (ulong)(v * 2113 + 29) };
        var bark = Mats.Detail();
        var dead = v >= 3;
        var leaves = Mats.LeafCluster(dead);
        const float h = 16f;
        var trunkTop = h * r.RandfRange(0.42f, 0.55f);
        var barkCol = v % 2 == 0 ? new Color(0.3f, 0.29f, 0.27f) : new Color(0.23f, 0.21f, 0.19f);
        k.Col = barkCol;
        var lean = new Vector3(r.RandfRange(-0.5f, 0.5f), 0, r.RandfRange(-0.5f, 0.5f));
        var top = Vector3.Up * trunkTop + lean;
        // Fût conique avec empattement (contreforts fondus dans le cône de base).
        k.Cylinder(bark, Vector3.Down * 0.3f, 0.5f, 0.36f, 1.5f, 9, false);
        k.Cylinder(bark, Vector3.Up * 1.1f, 0.36f, 0.22f, (top - Vector3.Up * 1.1f).Length() + 0.2f, 9, false, new Basis(new Quaternion(Vector3.Up, (top - Vector3.Up * 1.1f).Normalized())));
        var leafCol = dead ? new Color(0.2f, 0.18f, 0.15f) : new Color(0.18f, 0.21f, 0.1f) * r.RandfRange(0.85f, 1.1f);
        var limbs = 6 + v % 2;
        for (var i = 0; i < limbs; i++)
        {
            var ang = i * Mathf.Tau / limbs + r.RandfRange(-0.3f, 0.3f);
            var outDir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
            // Charpentières étagées : les basses s'écartent davantage (couronne ronde, pas en parasol).
            var low = i % 3 == 0;
            var start = top * (low ? r.RandfRange(0.62f, 0.75f) : r.RandfRange(0.82f, 1f));
            var len = low ? r.RandfRange(4.5f, 6f) : r.RandfRange(4f, 6.5f);
            var end = start + (outDir * (low ? r.RandfRange(0.75f, 0.95f) : r.RandfRange(0.4f, 0.65f)) + Vector3.Up * (low ? r.RandfRange(0.35f, 0.6f) : r.RandfRange(0.75f, 1f))).Normalized() * len;
            k.Col = barkCol;
            k.Tube(bark, start, end, low ? 0.13f : 0.16f, 6);
            // Rameaux et bouquets répartis le long de la branche (dessous et côtés compris).
            for (var b = 0; b < 4; b++)
            {
                var t = 0.35f + b * 0.2f + r.RandfRange(-0.05f, 0.05f);
                var bp = start.Lerp(end, t);
                var bd = (outDir * r.RandfRange(0.5f, 1f) + Vector3.Up * r.RandfRange(-0.25f, 0.5f) + new Vector3(r.RandfRange(-0.6f, 0.6f), 0, r.RandfRange(-0.6f, 0.6f))).Normalized();
                var be = bp + bd * r.RandfRange(1.2f, 2.4f);
                k.Col = barkCol * 1.05f;
                k.Tube(bark, bp, be, 0.06f, 4);
                Cluster(k, leaves, be + Vector3.Up * 0.2f, r.RandfRange(1.3f, 2f) * (dead ? 0.8f : 1f), r, leafCol);
            }
            Cluster(k, leaves, end + Vector3.Up * 0.3f, r.RandfRange(1.6f, 2.3f) * (dead ? 0.8f : 1f), r, leafCol);
        }
        if (!dead)
        {
            // Cœur de la couronne : quelques cartes sombres (l'arbre n'est pas transparent au contre-jour).
            for (var i = 0; i < 5; i++)
                Cluster(k, leaves, top + new Vector3(r.RandfRange(-2.5f, 2.5f), r.RandfRange(1.5f, 5.5f), r.RandfRange(-2.5f, 2.5f)), 2.3f, r, leafCol * 0.6f);
        }
    });

    /// <summary>Buisson de sous-bois (noisetier, ronces : ≈ 1,5 m) : tiges et bouquets de feuilles en cartes. Traversable.</summary>
    public static ArrayMesh Bush(int v) => Cached($"forestbush{v}", k =>
    {
        var r = new RandomNumberGenerator { Seed = (ulong)(v * 389 + 13) };
        var bark = Mats.Detail();
        var leaves = Mats.LeafCluster(v == 2);
        var col = v == 2 ? new Color(0.24f, 0.18f, 0.1f) : new Color(0.13f, 0.16f, 0.08f) * r.RandfRange(0.9f, 1.15f);
        k.Solid = false;
        for (var i = 0; i < 6; i++)
        {
            var ang = i * Mathf.Tau / 6f + r.RandfRange(-0.3f, 0.3f);
            var tip = new Vector3(Mathf.Cos(ang) * r.RandfRange(0.4f, 0.9f), r.RandfRange(0.9f, 1.6f), Mathf.Sin(ang) * r.RandfRange(0.4f, 0.9f));
            k.Col = BarkDark;
            k.Tube(bark, Vector3.Zero, tip, 0.03f, 3);
            Cluster(k, leaves, tip * r.RandfRange(0.7f, 0.95f), r.RandfRange(0.55f, 0.85f), r, col);
        }
        Cluster(k, leaves, new Vector3(0, 0.6f, 0), 0.8f, r, col * 0.7f);
    });

    /// <summary>Bouquet de feuillage : trois cartes croisées (deux verticales, une inclinée) en découpe, teinte dégradée.</summary>
    private static void Cluster(MeshKit k, Material m, Vector3 c, float s, RandomNumberGenerator r, Color col)
    {
        var yaw = r.RandfRange(0, Mathf.Pi);
        var lit = Opaque(col.Lightened(0.12f));
        var dark = Opaque(col * 0.7f);
        var keep = k.Solid;
        k.Solid = false; // feuillage : jamais solide (formes de collision des arbres destructibles)
        for (var q = 0; q < 3; q++)
        {
            var a = yaw + q * Mathf.Tau / 3f;
            var tilt = q == 0 ? 0.3f : 0.95f; // une carte presque à plat, deux inclinées : ni rideau, ni galette
            var u = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * s;
            var w = (new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * Mathf.Cos(tilt) + Vector3.Up * Mathf.Sin(tilt)) * s * 0.8f;
            k.QuadC(m, c - u - w, c + u - w, c + u + w, c - u + w, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0),
                dark, dark, lit, lit, u.Cross(w));
        }
        k.Solid = keep;
    }

    /// <summary>
    /// Pylône de ligne électrique (treillis, 26 m) : quatre membrures convergentes, croisillons, deux consoles et
    /// isolateurs. Solide (les grimpeurs l'escaladent).
    /// </summary>
    public static ArrayMesh Pylon() => Cached("forestpylon", k =>
    {
        var steel = Mats.DetailMetal();
        k.Col = new Color(0.36f, 0.37f, 0.36f);
        const float h = 26f;
        Vector3 Leg(int i, float y)
        {
            var w = Mathf.Lerp(3.2f, 0.7f, Mathf.Pow(y / h, 0.7f));
            return new Vector3((i is 0 or 3 ? -1 : 1) * w, y, (i < 2 ? -1 : 1) * w);
        }
        for (var i = 0; i < 4; i++) k.Tube(steel, Leg(i, 0), Leg(i, h), 0.09f, 4, true);
        for (var y = 0f; y < h - 2f; y += 3.2f)
        {
            var y1 = y + 3.2f;
            for (var i = 0; i < 4; i++)
            {
                var j = (i + 1) % 4;
                k.Tube(steel, Leg(i, y), Leg(j, y1), 0.04f, 3);
                k.Tube(steel, Leg(j, y), Leg(i, y1), 0.04f, 3);
                k.Tube(steel, Leg(i, y1), Leg(j, y1), 0.045f, 3);
            }
        }
        foreach (var (y, w) in new[] { (18.5f, 6.5f), (23f, 4.8f) })
        {
            k.Tube(steel, new Vector3(-w, y, 0), new Vector3(w, y, 0), 0.12f, 4, true);
            k.Tube(steel, new Vector3(-w, y, 0), new Vector3(-0.9f, y + 1.6f, 0), 0.05f, 3);
            k.Tube(steel, new Vector3(w, y, 0), new Vector3(0.9f, y + 1.6f, 0), 0.05f, 3);
            k.Col = new Color(0.32f, 0.3f, 0.28f);
            foreach (var sx in new[] { -1f, 1f })
                k.Cylinder(Mats.Detail(), new Vector3(sx * (w - 0.3f), y - 1.6f, 0), 0.13f, 0.13f, 1.6f, 6);
            k.Col = new Color(0.36f, 0.37f, 0.36f);
        }
    });

    /// <summary>Points d'accroche des câbles sur un pylône (repère local).</summary>
    public static readonly Vector3[] PylonWires = { new(-6.2f, 16.9f, 0), new(6.2f, 16.9f, 0), new(-4.5f, 21.4f, 0), new(4.5f, 21.4f, 0) };

    /// <summary>Remorque grumière chargée (≈ 9 m) : châssis, ranchers, grumes. Solide, indestructible.</summary>
    public static ArrayMesh LogTrailer() => Cached("forestlogtrailer", k =>
    {
        var metal = Mats.DetailMetal();
        var bark = Mats.Detail();
        k.Col = new Color(0.16f, 0.17f, 0.16f);
        k.Box(metal, new Vector3(0, 1.05f, 0), new Vector3(1.1f, 0.3f, 9f));
        foreach (var z in new[] { -3.2f, -2f, 2.4f, 3.4f })
        foreach (var sx in new[] { -1f, 1f })
        {
            k.Col = new Color(0.06f, 0.06f, 0.06f);
            k.Cylinder(metal, new Vector3(sx * 0.95f, 0.5f, z), 0.5f, 0.5f, 0.42f, 10, true, new Basis(Vector3.Forward, Mathf.Pi / 2f));
        }
        k.Col = new Color(0.28f, 0.1f, 0.06f);
        foreach (var z in new[] { -3.6f, -1.2f, 1.2f, 3.6f })
        foreach (var sx in new[] { -1f, 1f })
            k.Box(metal, new Vector3(sx * 1.2f, 2.3f, z), new Vector3(0.14f, 2.3f, 0.14f));
        var r = new RandomNumberGenerator { Seed = 515 };
        for (var row = 0; row < 3; row++)
        for (var i = 0; i < 4 - row; i++)
        {
            var x = (i - (3 - row) / 2f) * 0.6f;
            var y = 1.5f + row * 0.52f;
            var from = new Vector3(x, y, -4.6f + r.RandfRange(-0.3f, 0.3f));
            var to = new Vector3(x, y, 4.8f + r.RandfRange(-0.3f, 0.3f));
            k.Col = Bark * r.RandfRange(0.85f, 1.15f);
            k.Tube(bark, from, to, 0.29f, 8, false);
            k.Col = Sapwood;
            k.Tube(bark, from, from + Vector3.Back * 0.02f, 0.285f, 8, true);
            k.Tube(bark, to - Vector3.Back * 0.02f, to, 0.285f, 8, true);
        }
    });
}
