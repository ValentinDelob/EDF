using Godot;

namespace Rempart;

/// <summary>
/// Accessoires de la banlieue boisée : arbres de forêt (avec version lointaine allégée), arbustes,
/// poteaux électriques, mobilier de jardin, rochers et roseaux. Maillages en cache, posés par MultiMesh.
/// </summary>
public static partial class Props
{
    /// <summary>Icosaèdre subdivisé une fois (80 faces) : houppiers légers pour la forêt.</summary>
    private static readonly Vector3[] Ico1 = BuildIco(1);

    /// <summary>Icosaèdre brut (20 faces) : versions lointaines.</summary>
    private static readonly Vector3[] Ico0 = BuildIco(0);

    /// <summary>Hauteur de référence des arbres de forêt (échelle 1).</summary>
    public const float ConiferHeight = 17f;
    public const float BroadleafHeight = 14f;

    /// <summary>Points d'accroche des câbles sur un poteau électrique (repère local, traverse selon X).</summary>
    public static readonly Vector3[] PoleWires = { new(-0.95f, 9.12f, 0), new(0, 9.12f, 0), new(0.95f, 9.12f, 0) };

    /// <summary>Houppier bosselé sur un icosaèdre donné, rayons distincts par axe.</summary>
    private static void CanopyLow(MeshKit k, Vector3[] ico, Vector3 c, Vector3 r, float seed, Color leaf, Material m = null)
    {
        m ??= Mats.Foliage();
        Vector3 D(Vector3 d)
        {
            var bump = 1f + 0.22f * Mathf.Sin(d.X * 4.1f + seed) * Mathf.Cos(d.Z * 3.7f + seed * 1.3f) + 0.12f * Mathf.Sin(d.Y * 6f + seed * 0.7f);
            return c + new Vector3(d.X * r.X, d.Y * r.Y, d.Z * r.Z) * bump;
        }
        Color C(Vector3 d)
        {
            var k2 = 0.62f + 0.45f * (d.Y * 0.5f + 0.5f) + 0.1f * Mathf.Sin(d.X * 9f + d.Z * 7f + seed);
            return new Color(leaf.R * k2, leaf.G * k2, leaf.B * k2);
        }
        for (var i = 0; i < ico.Length; i += 3)
        {
            Vector3 a = D(ico[i]), b = D(ico[i + 1]), e = D(ico[i + 2]);
            var fn = (b - a).Cross(e - a).Normalized();
            if (fn.Dot((a + b + e) / 3 - c) < 0) fn = -fn;
            k.TriSmooth(m, a, b, e, (ico[i] * 3f + fn).Normalized(), (ico[i + 1] * 3f + fn).Normalized(), (ico[i + 2] * 3f + fn).Normalized(),
                C(ico[i]), C(ico[i + 1]), C(ico[i + 2]));
        }
    }

    // Résineux sombres et ternes ; la variante 3 est un résineux mort (aiguilles rousses grisées).
    private static readonly Color[] NeedleColors =
    {
        new(0.075f, 0.1f, 0.08f), new(0.085f, 0.11f, 0.085f), new(0.07f, 0.09f, 0.078f), new(0.15f, 0.13f, 0.1f),
    };

    private static readonly Color[] LeafColors =
    {
        new(0.12f, 0.145f, 0.085f), new(0.15f, 0.155f, 0.09f), new(0.11f, 0.13f, 0.08f), new(0.17f, 0.155f, 0.1f), new(0.13f, 0.14f, 0.09f),
    };

    /// <summary>Feuillus morts : variantes impaires de la forêt et des jardins.</summary>
    private static bool DeadVariant(int v) => v % 2 == 1;

    /// <summary>Résineux (épicéa, sapin) : étages de jupes coniques dentelées autour d'un tronc fin. Variantes 0 à 3.</summary>
    public static ArrayMesh Conifer(int v) => Cached($"conifer{v}", k =>
    {
        var r = new RandomNumberGenerator { Seed = (ulong)(v * 977 + 5) };
        var bark = Mats.Detail();
        var leaf = Mats.Foliage();
        const float h = ConiferHeight;
        k.Col = new Color(0.27f, 0.2f, 0.15f);
        k.Cylinder(bark, Vector3.Zero, 0.32f, 0.06f, h * 0.96f, 7, false);
        var tiers = 7 + v % 2;
        var baseY = h * r.RandfRange(0.12f, 0.22f);
        var maxR = r.RandfRange(2.7f, 3.4f);
        var col = NeedleColors[v % NeedleColors.Length];
        for (var i = 0; i < tiers; i++)
        {
            var t0 = i / (float)tiers;
            var y0 = Mathf.Lerp(baseY, h * 0.86f, t0);
            var top = Mathf.Min(y0 + (h - baseY) / tiers * 2.1f, h + 0.2f);
            var rad = maxR * Mathf.Pow(1 - t0, 0.9f) + 0.35f;
            const int seg = 10;
            var apex = new Vector3(r.RandfRange(-0.1f, 0.1f), top, r.RandfRange(-0.1f, 0.1f));
            var ring = new Vector3[seg];
            for (var j = 0; j < seg; j++)
            {
                var a = j * Mathf.Tau / seg + r.RandfRange(-0.12f, 0.12f) + i * 0.4f;
                var rr = rad * (j % 2 == 0 ? 1f : 0.7f) * r.RandfRange(0.88f, 1.1f);
                var droop = j % 2 == 0 ? rad * 0.32f : rad * 0.1f;
                ring[j] = new Vector3(Mathf.Cos(a) * rr, y0 - droop, Mathf.Sin(a) * rr);
            }
            var shade = 0.75f + 0.3f * t0;
            var tip = col.Lightened(0.15f) * shade;
            var inner = col.Darkened(0.45f);
            for (var j = 0; j < seg; j++)
            {
                var p0 = ring[j];
                var p1 = ring[(j + 1) % seg];
                k.Tri(leaf, p0, p1, apex, new Vector3(0, y0 - 0.5f, 0), tip * (j % 2 == 0 ? 1f : 0.85f), tip * ((j + 1) % 2 == 0 ? 1f : 0.85f), inner);
            }
        }
    });

    /// <summary>Résineux lointain : un seul cône (6 faces).</summary>
    public static ArrayMesh ConiferFar(int v) => Cached($"coniferfar{v}", k =>
    {
        var col = NeedleColors[v % NeedleColors.Length];
        k.Col = col * 0.95f;
        k.Cylinder(Mats.Foliage(), new Vector3(0, ConiferHeight * 0.14f, 0), 3.0f, 0.05f, ConiferHeight * 0.88f, 6, false);
    });

    /// <summary>Feuillu de forêt (chêne, hêtre) : fût élancé, branches maîtresses, couronne de quatre masses. Variantes 0 à 4.</summary>
    public static ArrayMesh Broadleaf(int v, bool fine = false) => Cached($"broadleaf{v}{fine}", k =>
    {
        var r = new RandomNumberGenerator { Seed = (ulong)(v * 1543 + 11) };
        var bark = Mats.Detail();
        const float h = BroadleafHeight;
        var barkCol = v % 2 == 0 ? new Color(0.24f, 0.225f, 0.2f) : new Color(0.2f, 0.185f, 0.17f);
        k.Col = barkCol;
        k.Cylinder(bark, Vector3.Zero, 0.42f, 0.24f, h * 0.62f, 8, false);
        var leaf = LeafColors[v % LeafColors.Length];
        var dead = DeadVariant(v);
        var n = 4;
        var tips = new System.Collections.Generic.List<Vector3>();
        for (var i = 0; i < n; i++)
        {
            var a = i * Mathf.Tau / n + r.RandfRange(-0.4f, 0.4f);
            var d = r.RandfRange(1.4f, 2.4f);
            var c = new Vector3(Mathf.Cos(a) * d, h * r.RandfRange(0.66f, 0.8f), Mathf.Sin(a) * d);
            k.Col = barkCol;
            // Charpentières ramifiées (plus fines et plus nombreuses sur un arbre mort ou un arbre de place).
            Branch(k, bark, r, new Vector3(0, h * 0.5f, 0), (c - new Vector3(0, h * 0.45f, 0)).Normalized(), h * 0.38f, 0.17f, dead || fine ? 2 : 1, tips);
        }
        if (dead) TwigClumps(k, r, tips, 1.6f, v * 2.1f);
        else LeafClumps(k, r, tips, Ico1, fine ? 1.7f : 2.3f, v * 3.1f, leaf, 0.8f);
    });

    /// <summary>Feuillu lointain : tronc et masse unique à 20 faces (ramilles grises ajourées pour un arbre mort).</summary>
    public static ArrayMesh BroadleafFar(int v) => Cached($"broadleaffar{v}", k =>
    {
        k.Col = new Color(0.22f, 0.2f, 0.18f);
        k.Cylinder(Mats.Detail(), Vector3.Zero, 0.4f, 0.25f, BroadleafHeight * 0.6f, 5, false);
        if (DeadVariant(v))
            CanopyLow(k, Ico0, new Vector3(0, BroadleafHeight * 0.75f, 0), new Vector3(4.2f, 3.6f, 4.2f), v * 2.3f, TwigCol, Mats.Twigs());
        else
            CanopyLow(k, Ico0, new Vector3(0, BroadleafHeight * 0.78f, 0), new Vector3(4.6f, 3.6f, 4.6f), v * 2.3f, LeafColors[v % LeafColors.Length]);
    });

    /// <summary>Arbre de jardin (fruitier, érable) : petit tronc et couronne basse. Variantes 0 à 4.</summary>
    public static ArrayMesh GardenTree(int v) => Cached($"gardentree{v}", k =>
    {
        var r = new RandomNumberGenerator { Seed = (ulong)(v * 331 + 7) };
        var bark = Mats.Detail();
        var h = 4.2f + v * 0.5f;
        k.Col = BarkGray;
        k.Cylinder(bark, Vector3.Zero, 0.2f, 0.12f, h * 0.7f, 7, false);
        var leaf = v == 4 ? new Color(0.24f, 0.15f, 0.1f) : LeafColors[(v + 2) % LeafColors.Length].Lightened(0.04f);
        var dead = DeadVariant(v);
        var tips = new System.Collections.Generic.List<Vector3>();
        for (var i = 0; i < 3; i++)
        {
            var a = i * Mathf.Tau / 3 + r.RandfRange(-0.4f, 0.4f);
            var c = new Vector3(Mathf.Cos(a) * 0.8f, h * r.RandfRange(0.78f, 0.95f), Mathf.Sin(a) * 0.8f);
            k.Col = BarkGray;
            Branch(k, bark, r, new Vector3(0, h * 0.45f, 0), (c - new Vector3(0, h * 0.4f, 0)).Normalized(), h * 0.5f, 0.08f, dead ? 2 : 1, tips);
        }
        if (dead) TwigClumps(k, r, tips, 0.9f, v * 2.1f);
        else LeafClumps(k, r, tips, Ico1, 1.15f, v * 2.7f, leaf, 0.85f);
    });

    /// <summary>Arbuste ou massif (forme écrasée). Variantes 0 à 3.</summary>
    public static ArrayMesh Shrub(int v) => Cached($"shrub{v}", k =>
    {
        var leaf = v switch
        {
            1 => new Color(0.11f, 0.14f, 0.09f),
            2 => new Color(0.18f, 0.18f, 0.11f),
            3 => new Color(0.22f, 0.17f, 0.13f),   // ancien massif, desséché
            _ => new Color(0.13f, 0.16f, 0.09f),
        };
        // Trois touffes qui se chevauchent : silhouette irrégulière.
        CanopyLow(k, Ico1, new Vector3(0, 0.6f, 0), new Vector3(0.85f, 0.75f, 0.8f), v * 5.3f, leaf.Lightened(0.05f), Mats.Leaves());
        CanopyLow(k, Ico1, new Vector3(0.55f, 0.45f, 0.2f), new Vector3(0.6f, 0.5f, 0.6f), v * 5.3f + 1f, leaf, Mats.Leaves());
        CanopyLow(k, Ico1, new Vector3(-0.45f, 0.42f, -0.3f), new Vector3(0.6f, 0.48f, 0.55f), v * 5.3f + 2f, leaf.Darkened(0.05f), Mats.Leaves());
    });

    /// <summary>Rocher (berges) : icosaèdre déformé, gris.</summary>
    public static ArrayMesh Rock(int v) => Cached($"rock{v}", k =>
    {
        var d = Mats.Detail();
        var seed = v * 3.7f;
        for (var i = 0; i < Ico1.Length; i += 3)
        {
            Vector3 P(Vector3 q) => new Vector3(q.X * 1.1f, q.Y * 0.55f, q.Z * 0.9f) * (1f + 0.18f * Mathf.Sin(q.X * 5f + seed) * Mathf.Cos(q.Z * 4f + seed)) + Vector3.Up * 0.2f;
            k.Col = new Color(0.5f, 0.49f, 0.46f) * (0.85f + 0.15f * Mathf.Sin(seed + i));
            k.Tri(d, P(Ico1[i]), P(Ico1[i + 1]), P(Ico1[i + 2]), Vector3.Up * 0.2f);
        }
    });

    /// <summary>Touffe de roseaux : lames fines double face.</summary>
    public static ArrayMesh Reeds() => Cached("reeds", k =>
    {
        var leaf = Mats.Foliage();
        var r = new RandomNumberGenerator { Seed = 77 };
        for (var i = 0; i < 16; i++)
        {
            var a = r.RandfRange(0, Mathf.Tau);
            var b = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r.RandfRange(0, 0.6f);
            var hgt = r.RandfRange(1.0f, 1.9f);
            var lean = new Vector3(r.RandfRange(-0.3f, 0.3f), 0, r.RandfRange(-0.3f, 0.3f));
            var side = new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a)) * 0.05f;
            var top = b + lean + Vector3.Up * hgt;
            k.Tri(leaf, b - side, b + side, top, b + Vector3.Forward, new Color(0.3f, 0.34f, 0.16f), new Color(0.3f, 0.34f, 0.16f), new Color(0.6f, 0.58f, 0.32f));
        }
    });

    /// <summary>Poteau électrique en bois : traverse, isolateurs ; <paramref name="transformer"/> ajoute un transformateur.</summary>
    public static ArrayMesh PowerPole(bool transformer) => Cached($"pole{transformer}", k =>
    {
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        k.Col = new Color(0.36f, 0.31f, 0.26f);
        k.Cylinder(d, Vector3.Zero, 0.17f, 0.12f, 9.4f, 8);
        k.Col = new Color(0.3f, 0.26f, 0.22f);
        k.Box(d, new Vector3(0, 8.9f, 0), new Vector3(2.3f, 0.12f, 0.12f));
        k.Tube(d, new Vector3(-0.7f, 8.9f, 0), new Vector3(0, 8.3f, 0), 0.04f, 4);
        k.Tube(d, new Vector3(0.7f, 8.9f, 0), new Vector3(0, 8.3f, 0), 0.04f, 4);
        foreach (var w in PoleWires)
        {
            k.Col = new Color(0.82f, 0.82f, 0.8f);
            k.Cylinder(d, new Vector3(w.X, 8.96f, 0), 0.06f, 0.05f, 0.18f, 6);
        }
        if (!transformer) return;
        k.Col = new Color(0.55f, 0.58f, 0.56f);
        k.Cylinder(m, new Vector3(0, 6.6f, 0.38f), 0.32f, 0.32f, 1.1f, 10);
        k.Cylinder(m, new Vector3(0, 7.7f, 0.38f), 0.34f, 0.2f, 0.12f, 10);
        k.Col = new Color(0.25f, 0.25f, 0.26f);
        k.Box(m, new Vector3(0, 7.1f, 0.12f), new Vector3(0.12f, 0.6f, 0.2f));
    });

    /// <summary>Boîte aux lettres sur poteau.</summary>
    public static ArrayMesh Mailbox() => Cached("mailbox", k =>
    {
        var d = Mats.Detail();
        k.Col = new Color(0.2f, 0.2f, 0.21f);
        k.Box(Mats.DetailMetal(), new Vector3(0, 0.55f, 0), new Vector3(0.06f, 1.1f, 0.06f));
        k.Col = new Color(0.22f, 0.3f, 0.24f);
        k.Box(Mats.DetailMetal(), new Vector3(0, 1.2f, 0), new Vector3(0.38f, 0.36f, 0.3f));
        k.Col = new Color(0.85f, 0.85f, 0.82f);
        k.Box(d, new Vector3(0, 1.3f, 0.152f), new Vector3(0.24f, 0.03f, 0.005f));
    });

    /// <summary>Bac roulant à ordures (0 : ordures ménagères, 1 : tri sélectif à couvercle jaune).</summary>
    public static ArrayMesh WheelieBin(int v) => Cached($"wbin{v}", k =>
    {
        var d = Mats.Detail();
        k.Col = new Color(0.16f, 0.2f, 0.18f);
        k.Box(d, new Vector3(0, 0.5f, 0), new Vector3(0.58f, 0.95f, 0.7f));
        k.Col = v == 1 ? new Color(0.85f, 0.7f, 0.1f) : new Color(0.12f, 0.14f, 0.13f);
        k.Box(d, new Vector3(0, 1.0f, 0.02f), new Vector3(0.62f, 0.06f, 0.76f));
        k.Col = new Color(0.05f, 0.05f, 0.05f);
        k.Cylinder(d, new Vector3(-0.3f, 0.1f, -0.3f), 0.1f, 0.1f, 0.05f, 8, true, new Basis(Vector3.Back, Mathf.Pi / 2));
        k.Cylinder(d, new Vector3(0.35f, 0.1f, -0.3f), 0.1f, 0.1f, 0.05f, 8, true, new Basis(Vector3.Back, Mathf.Pi / 2));
    });

    /// <summary>Trampoline de jardin.</summary>
    public static ArrayMesh Trampoline() => Cached("trampoline", k =>
    {
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        k.Col = new Color(0.5f, 0.52f, 0.54f);
        for (var i = 0; i < 6; i++)
        {
            var a = i * Mathf.Tau / 6;
            k.Tube(m, new Vector3(Mathf.Cos(a) * 1.7f, 0, Mathf.Sin(a) * 1.7f), new Vector3(Mathf.Cos(a) * 1.75f, 0.8f, Mathf.Sin(a) * 1.75f), 0.03f, 4);
        }
        k.Col = new Color(0.1f, 0.35f, 0.65f);
        k.Cylinder(d, new Vector3(0, 0.78f, 0), 1.85f, 1.85f, 0.1f, 18);
        k.Col = new Color(0.05f, 0.05f, 0.06f);
        k.Cylinder(d, new Vector3(0, 0.79f, 0), 1.5f, 1.5f, 0.1f, 18);
    });

    /// <summary>Portique de balançoires.</summary>
    public static ArrayMesh SwingSet() => Cached("swing", k =>
    {
        var m = Mats.DetailMetal();
        var d = Mats.Detail();
        k.Col = new Color(0.75f, 0.2f, 0.12f);
        foreach (var x in new[] { -1.3f, 1.3f })
        {
            k.Tube(m, new Vector3(x, 0, -0.9f), new Vector3(x, 2.2f, 0), 0.05f, 6);
            k.Tube(m, new Vector3(x, 0, 0.9f), new Vector3(x, 2.2f, 0), 0.05f, 6);
        }
        k.Tube(m, new Vector3(-1.4f, 2.2f, 0), new Vector3(1.4f, 2.2f, 0), 0.06f, 6, true);
        foreach (var x in new[] { -0.6f, 0.6f })
        {
            k.Col = new Color(0.3f, 0.3f, 0.3f);
            k.Tube(m, new Vector3(x - 0.2f, 2.2f, 0), new Vector3(x - 0.2f, 0.5f, 0), 0.012f, 3);
            k.Tube(m, new Vector3(x + 0.2f, 2.2f, 0), new Vector3(x + 0.2f, 0.5f, 0), 0.012f, 3);
            k.Col = new Color(0.1f, 0.1f, 0.1f);
            k.Box(d, new Vector3(x, 0.48f, 0), new Vector3(0.48f, 0.04f, 0.18f));
        }
    });

    /// <summary>But de football (7,32 m × 2,44 m), ouverture vers +Z.</summary>
    public static ArrayMesh FootballGoal() => Cached("goal", k =>
    {
        var m = Mats.DetailMetal();
        k.Col = new Color(0.92f, 0.92f, 0.9f);
        k.Tube(m, new Vector3(-3.66f, 0, 0), new Vector3(-3.66f, 2.44f, 0), 0.06f, 8, true);
        k.Tube(m, new Vector3(3.66f, 0, 0), new Vector3(3.66f, 2.44f, 0), 0.06f, 8, true);
        k.Tube(m, new Vector3(-3.72f, 2.44f, 0), new Vector3(3.72f, 2.44f, 0), 0.06f, 8, true);
        k.Col = new Color(0.6f, 0.6f, 0.6f);
        foreach (var x in new[] { -3.66f, 3.66f })
        {
            k.Tube(m, new Vector3(x, 2.44f, 0), new Vector3(x, 0, -1.8f), 0.03f, 5);
            k.Tube(m, new Vector3(x, 0, -1.8f), new Vector3(x, 0, 0), 0.03f, 5);
        }
        k.Tube(m, new Vector3(-3.66f, 0, -1.8f), new Vector3(3.66f, 0, -1.8f), 0.03f, 5);
    });

    /// <summary>Abri de jardin en bois à toit à deux pans (2,4 × 2 m).</summary>
    public static ArrayMesh GardenShed() => Cached("shed", k =>
    {
        var d = Mats.Detail();
        k.Col = new Color(0.42f, 0.32f, 0.22f);
        k.Box(d, new Vector3(0, 1.0f, 0), new Vector3(2.4f, 2.0f, 2.0f));
        k.Col = new Color(0.34f, 0.25f, 0.17f);
        for (var y = 0.25f; y < 2f; y += 0.25f) k.Box(d, new Vector3(0, y, 1.005f), new Vector3(2.4f, 0.03f, 0.02f));
        k.Col = new Color(0.3f, 0.22f, 0.15f);
        k.Box(d, new Vector3(0.4f, 0.9f, 1.02f), new Vector3(0.8f, 1.75f, 0.03f));
        k.Col = new Color(0.16f, 0.2f, 0.16f);
        var ang = Mathf.Atan2(0.6f, 1.2f);
        foreach (var sg in new[] { -1f, 1f })
            k.Box(d, new Vector3(0, 2.3f, sg * 0.6f), new Vector3(2.7f, 0.05f, 1.48f), new Basis(Vector3.Right, sg * ang));
        k.Col = new Color(0.42f, 0.32f, 0.22f);
        k.Tri(d, new Vector3(-1.2f, 2f, -1f), new Vector3(-1.2f, 2f, 1f), new Vector3(-1.2f, 2.6f, 0), Vector3.Up * 2f);
        k.Tri(d, new Vector3(1.2f, 2f, -1f), new Vector3(1.2f, 2f, 1f), new Vector3(1.2f, 2.6f, 0), Vector3.Up * 2f);
    });

    /// <summary>Panneau « Stop » (octogone rouge à liseré blanc), face vers +Z.</summary>
    public static ArrayMesh StopSign() => Cached("stop", k =>
    {
        var d = Mats.Detail();
        var rot = new Basis(Vector3.Right, Mathf.Pi / 2) * new Basis(Vector3.Up, Mathf.Pi / 8);
        k.Col = new Color(0.55f, 0.56f, 0.57f);
        k.Cylinder(Mats.DetailMetal(), Vector3.Zero, 0.035f, 0.035f, 2.3f, 6);
        k.Col = new Color(0.92f, 0.92f, 0.9f);
        k.Cylinder(d, new Vector3(0, 2.15f, 0.04f), 0.4f, 0.4f, 0.02f, 8, true, rot);
        k.Col = new Color(0.7f, 0.06f, 0.05f);
        k.Cylinder(d, new Vector3(0, 2.15f, 0.06f), 0.36f, 0.36f, 0.01f, 8, true, rot);
    });
}
