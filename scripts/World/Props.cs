using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Mobilier urbain et véhicules : maillages fusionnés (un ArrayMesh par modèle, mis en cache).
/// Convention : l'avant du modèle regarde +Z, posé sur y = 0.
/// </summary>
public static class Props
{
    private static readonly Dictionary<string, ArrayMesh> Cache = new();

    private static ArrayMesh Cached(string key, System.Action<MeshKit> build)
    {
        if (Cache.TryGetValue(key, out var m)) return m;
        var kit = new MeshKit();
        build(kit);
        m = kit.Commit();
        Cache[key] = m;
        return m;
    }

    private static readonly Color PoleGray = new(0.2f, 0.21f, 0.22f);
    private static readonly Color Wood = new(0.45f, 0.32f, 0.2f);

    /// <summary>Banc public : lattes de bois sur piètement en fonte.</summary>
    public static ArrayMesh Bench() => Cached("bench", k =>
    {
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        k.Col = Wood;
        for (var i = 0; i < 3; i++) k.Box(d, new Vector3(0, 0.45f, 0.14f - i * 0.15f), new Vector3(1.9f, 0.05f, 0.12f));
        var back = new Basis(Vector3.Right, Mathf.DegToRad(-12));
        for (var i = 0; i < 2; i++) k.Box(d, new Vector3(0, 0.66f + i * 0.17f, -0.25f - i * 0.035f), new Vector3(1.9f, 0.12f, 0.04f), back);
        k.Col = PoleGray;
        foreach (var x in new[] { -0.8f, 0.8f })
        {
            k.Box(m, new Vector3(x, 0.22f, 0.12f), new Vector3(0.06f, 0.44f, 0.06f));
            k.Box(m, new Vector3(x, 0.45f, 0f), new Vector3(0.06f, 0.05f, 0.5f));
            k.Box(m, new Vector3(x, 0.45f, -0.22f), new Vector3(0.06f, 0.9f, 0.06f));
        }
    });

    /// <summary>Corbeille de rue sur poteau.</summary>
    public static ArrayMesh Bin() => Cached("bin", k =>
    {
        var m = Mats.DetailMetal();
        k.Col = new Color(0.12f, 0.24f, 0.18f);
        k.Cylinder(m, new Vector3(0, 0.25f, 0), 0.24f, 0.26f, 0.65f, 10);
        k.Col = new Color(0.08f, 0.08f, 0.08f);
        k.Cylinder(m, new Vector3(0, 0.9f, 0), 0.27f, 0.22f, 0.06f, 10);
        k.Col = PoleGray;
        k.Cylinder(m, new Vector3(0, 0, -0.3f), 0.04f, 0.04f, 1.0f, 6);
    });

    /// <summary>Potelet anti-stationnement avec bande réfléchissante.</summary>
    public static ArrayMesh Bollard() => Cached("bollard", k =>
    {
        var m = Mats.DetailMetal();
        k.Col = new Color(0.14f, 0.14f, 0.15f);
        k.Cylinder(m, Vector3.Zero, 0.075f, 0.07f, 0.8f, 8);
        k.Col = new Color(0.85f, 0.85f, 0.82f);
        k.Cylinder(Mats.Detail(), new Vector3(0, 0.66f, 0), 0.078f, 0.078f, 0.06f, 8, false);
        k.Col = new Color(0.14f, 0.14f, 0.15f);
        k.Cylinder(m, new Vector3(0, 0.8f, 0), 0.085f, 0.03f, 0.08f, 8);
    });

    /// <summary>Borne d'incendie.</summary>
    public static ArrayMesh Hydrant() => Cached("hydrant", k =>
    {
        var m = Mats.DetailMetal();
        k.Col = new Color(0.62f, 0.07f, 0.05f);
        k.Cylinder(m, Vector3.Zero, 0.13f, 0.12f, 0.7f, 10);
        k.Cylinder(m, new Vector3(0, 0.7f, 0), 0.14f, 0.05f, 0.12f, 10);
        k.Tube(m, new Vector3(-0.2f, 0.45f, 0), new Vector3(0.2f, 0.45f, 0), 0.05f, 6, true);
        k.Col = new Color(0.8f, 0.7f, 0.1f);
        k.Cylinder(m, new Vector3(0, 0.6f, 0), 0.135f, 0.135f, 0.05f, 10, false);
    });

    /// <summary>Feu tricolore sur poteau ; <paramref name="green"/> : feu vert allumé, sinon rouge.</summary>
    public static ArrayMesh TrafficLight(bool green) => Cached($"tl{green}", k =>
    {
        var m = Mats.DetailMetal();
        var d = Mats.Detail();
        var lamps = Mats.Lamps();
        k.Col = PoleGray;
        k.Cylinder(m, Vector3.Zero, 0.08f, 0.065f, 3.6f, 8);
        k.Col = new Color(0.06f, 0.06f, 0.06f);
        k.Box(m, new Vector3(0, 3.0f, 0.12f), new Vector3(0.34f, 1.0f, 0.26f));
        k.Box(d, new Vector3(0, 3.0f, -0.02f), new Vector3(0.52f, 1.18f, 0.03f)); // plaque de contraste
        k.Col = new Color(0.9f, 0.9f, 0.88f);
        k.Box(d, new Vector3(0, 3.0f, -0.04f), new Vector3(0.56f, 1.22f, 0.01f)); // liseré blanc
        var colors = new[] { new Color(1f, 0.12f, 0.08f), new Color(1f, 0.62f, 0.05f), new Color(0.15f, 1f, 0.45f) };
        for (var i = 0; i < 3; i++)
        {
            var y = 3.3f - i * 0.3f;
            var lit = green ? i == 2 : i == 0;
            k.Col = lit ? colors[i] : colors[i] * 0.16f;
            k.Cylinder(lit ? lamps : d, new Vector3(0, y, 0.25f), 0.1f, 0.1f, 0.02f, 10, true, new Basis(Vector3.Right, Mathf.Pi / 2));
            k.Col = new Color(0.05f, 0.05f, 0.05f);
            k.Box(m, new Vector3(0, y + 0.11f, 0.33f), new Vector3(0.26f, 0.02f, 0.16f)); // visière
        }
        // Boîtier piéton et bouton d'appel.
        k.Col = new Color(0.06f, 0.06f, 0.06f);
        k.Box(m, new Vector3(0.0f, 2.25f, 0.1f), new Vector3(0.26f, 0.42f, 0.16f));
        k.Col = green ? new Color(0.9f, 0.15f, 0.1f) : new Color(0.2f, 0.9f, 0.4f);
        k.Box(lamps, new Vector3(0.0f, 2.25f, 0.185f), new Vector3(0.14f, 0.16f, 0.01f));
        k.Col = new Color(0.85f, 0.75f, 0.1f);
        k.Box(m, new Vector3(0, 1.15f, 0.1f), new Vector3(0.12f, 0.2f, 0.1f));
    });

    /// <summary>Lampadaire à crosse (lanterne vers +Z). La nuit, la lanterne est lumineuse.</summary>
    public static ArrayMesh StreetLamp(bool night) => Cached($"lamp{night}", k =>
    {
        var m = Mats.DetailMetal();
        k.Col = new Color(0.24f, 0.26f, 0.27f);
        k.Cylinder(m, Vector3.Zero, 0.16f, 0.16f, 0.6f, 8);
        k.Cylinder(m, new Vector3(0, 0.6f, 0), 0.12f, 0.07f, 6.6f, 8);
        k.Tube(m, new Vector3(0, 6.9f, 0), new Vector3(0, 7.15f, 0.6f), 0.05f, 6);
        k.Tube(m, new Vector3(0, 7.15f, 0.6f), new Vector3(0, 7.2f, 1.3f), 0.05f, 6);
        k.Box(m, new Vector3(0, 7.18f, 1.45f), new Vector3(0.36f, 0.14f, 0.75f));
        k.Col = night ? new Color(1f, 0.85f, 0.62f) : new Color(0.75f, 0.76f, 0.74f);
        k.Box(night ? Mats.Lamps() : Mats.Detail(), new Vector3(0, 7.1f, 1.45f), new Vector3(0.28f, 0.03f, 0.6f));
    });

    /// <summary>Abribus : structure métallique, parois vitrées, banc, panneau publicitaire (lumineux la nuit).</summary>
    public static ArrayMesh BusStop(bool night) => Cached($"busstop{night}", k =>
    {
        var m = Mats.DetailMetal();
        var d = Mats.Detail();
        var g = Mats.Glass();
        k.Col = new Color(0.3f, 0.32f, 0.34f);
        foreach (var x in new[] { -2.05f, 2.05f })
        foreach (var z in new[] { -0.7f, 0.55f })
            k.Box(m, new Vector3(x, 1.25f, z), new Vector3(0.08f, 2.5f, 0.08f));
        k.Box(m, new Vector3(0, 2.55f, -0.05f), new Vector3(4.4f, 0.12f, 1.75f));
        k.Col = new Color(0.25f, 0.27f, 0.28f);
        k.Box(m, new Vector3(0, 2.45f, -0.7f), new Vector3(4.1f, 0.08f, 0.06f));
        k.Box(g, new Vector3(0, 1.3f, -0.72f), new Vector3(4.0f, 2.1f, 0.03f));
        k.Box(g, new Vector3(-2.05f, 1.3f, -0.08f), new Vector3(0.03f, 2.1f, 1.2f));
        // Panneau publicitaire.
        k.Col = new Color(0.2f, 0.21f, 0.22f);
        k.Box(m, new Vector3(2.05f, 1.35f, -0.08f), new Vector3(0.16f, 2.0f, 1.3f));
        k.Col = night ? new Color(0.92f, 0.95f, 1f) : new Color(0.22f, 0.45f, 0.62f);
        k.Box(night ? Mats.Lamps() : d, new Vector3(2.05f, 1.45f, -0.08f), new Vector3(0.18f, 1.6f, 1.05f));
        k.Col = new Color(0.95f, 0.8f, 0.2f);
        if (!night) k.Box(d, new Vector3(2.05f, 1.0f, -0.08f), new Vector3(0.185f, 0.35f, 0.9f));
        // Banc intérieur.
        k.Col = new Color(0.55f, 0.56f, 0.57f);
        k.Box(m, new Vector3(-0.3f, 0.48f, -0.5f), new Vector3(2.4f, 0.05f, 0.35f));
        k.Box(m, new Vector3(-1.4f, 0.24f, -0.55f), new Vector3(0.05f, 0.48f, 0.25f));
        k.Box(m, new Vector3(0.8f, 0.24f, -0.55f), new Vector3(0.05f, 0.48f, 0.25f));
        // Poteau d'arrêt avec plaque.
        k.Col = PoleGray;
        k.Cylinder(m, new Vector3(2.8f, 0, 0.5f), 0.05f, 0.05f, 3f, 6);
        k.Col = new Color(0.1f, 0.3f, 0.6f);
        k.Box(d, new Vector3(2.8f, 2.7f, 0.5f), new Vector3(0.05f, 0.5f, 0.5f));
        k.Col = new Color(0.95f, 0.95f, 0.95f);
        k.Box(d, new Vector3(2.8f, 2.7f, 0.5f), new Vector3(0.06f, 0.18f, 0.4f));
    });

    /// <summary>Séparateur en béton (type GBA).</summary>
    public static ArrayMesh JerseyBarrier() => Cached("jersey", k =>
    {
        var d = Mats.Detail();
        k.Col = new Color(0.72f, 0.71f, 0.68f);
        k.Frustum(d, Vector3.Zero, new Vector2(0.6f, 2f), new Vector2(0.36f, 2f), 0.3f);
        k.Frustum(d, new Vector3(0, 0.3f, 0), new Vector2(0.36f, 2f), new Vector2(0.2f, 2f), 0.5f);
    });

    /// <summary>Barrière de chantier rouge et blanche.</summary>
    public static ArrayMesh Barricade() => Cached("barricade", k =>
    {
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        k.Col = new Color(0.3f, 0.3f, 0.3f);
        foreach (var x in new[] { -0.9f, 0.9f })
        {
            k.Box(m, new Vector3(x, 0.5f, 0), new Vector3(0.05f, 1f, 0.05f));
            k.Box(m, new Vector3(x, 0.03f, 0), new Vector3(0.08f, 0.06f, 0.6f));
        }
        for (var i = 0; i < 6; i++)
        {
            k.Col = i % 2 == 0 ? new Color(0.75f, 0.06f, 0.05f) : new Color(0.95f, 0.95f, 0.93f);
            k.Box(d, new Vector3(-0.95f + 0.3166f * (i + 0.5f), 0.85f, 0.03f), new Vector3(0.3166f, 0.22f, 0.03f));
        }
    });

    /// <summary>Cône de signalisation.</summary>
    public static ArrayMesh Cone() => Cached("cone", k =>
    {
        var d = Mats.Detail();
        k.Col = new Color(0.1f, 0.1f, 0.1f);
        k.Box(d, new Vector3(0, 0.015f, 0), new Vector3(0.4f, 0.03f, 0.4f));
        k.Col = new Color(0.95f, 0.35f, 0.05f);
        k.Cylinder(d, new Vector3(0, 0.03f, 0), 0.16f, 0.03f, 0.7f, 10);
        k.Col = new Color(0.95f, 0.95f, 0.95f);
        k.Cylinder(d, new Vector3(0, 0.38f, 0), 0.095f, 0.075f, 0.12f, 10, false);
    });

    /// <summary>Mât de parasol de plage (la toile est un modèle séparé, teinté par instance).</summary>
    public static ArrayMesh UmbrellaPole() => Cached("upole", k =>
    {
        k.Col = new Color(0.85f, 0.85f, 0.82f);
        k.Cylinder(Mats.DetailMetal(), new Vector3(0, -0.3f, 0), 0.025f, 0.025f, 2.6f, 6);
    });

    /// <summary>Toile de parasol (blanche, multipliée par la couleur d'instance).</summary>
    public static ArrayMesh UmbrellaTop() => Cached("utop", k =>
    {
        var f = Mats.Instanced();
        k.Col = Colors.White;
        k.Cylinder(f, new Vector3(0, 1.95f, 0), 1.25f, 0.05f, 0.4f, 12, false);
        k.Cylinder(f, new Vector3(0, 1.94f, 0), 0.05f, 1.24f, 0.0f, 12, false); // dessous
        k.Cylinder(f, new Vector3(0, 1.85f, 0), 1.25f, 1.25f, 0.1f, 12, false); // lambrequin
    });

    /// <summary>Serviette de plage (teintée par instance).</summary>
    public static ArrayMesh Towel() => Cached("towel", k =>
    {
        k.Col = Colors.White;
        k.Box(Mats.Instanced(), new Vector3(0, 0.01f, 0), new Vector3(0.85f, 0.02f, 1.8f), null, false);
    });

    /// <summary>Transat en plastique blanc.</summary>
    public static ArrayMesh Lounger() => Cached("lounger", k =>
    {
        var d = Mats.Detail();
        k.Col = new Color(0.92f, 0.92f, 0.9f);
        k.Box(d, new Vector3(0, 0.3f, -0.15f), new Vector3(0.65f, 0.06f, 1.3f));
        k.Box(d, new Vector3(0, 0.55f, 0.75f), new Vector3(0.65f, 0.06f, 0.7f), new Basis(Vector3.Right, Mathf.DegToRad(-40)));
        foreach (var x in new[] { -0.28f, 0.28f })
        foreach (var z in new[] { -0.7f, 0.5f })
            k.Box(d, new Vector3(x, 0.15f, z), new Vector3(0.05f, 0.3f, 0.05f));
    });

    // ───────────────────────── Végétation ─────────────────────────

    private static readonly Vector3[] Ico = BuildIco();

    /// <summary>Icosaèdre subdivisé une fois (80 faces), sommets sur la sphère unité, par triangles.</summary>
    private static Vector3[] BuildIco()
    {
        var t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] v =
        {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
            new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
        };
        int[] f =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        var res = new List<Vector3>();
        for (var i = 0; i < f.Length; i += 3)
        {
            var a = v[f[i]].Normalized(); var b = v[f[i + 1]].Normalized(); var c = v[f[i + 2]].Normalized();
            var ab = (a + b).Normalized(); var bc = (b + c).Normalized(); var ca = (c + a).Normalized();
            res.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
        }
        return res.ToArray();
    }

    /// <summary>Houppier irrégulier : sphère bosselée (déformation continue, donc sans trous), teinte plus claire au sommet.</summary>
    public static void Canopy(MeshKit k, Vector3 c, float r, float seed, Color leaf)
    {
        var m = Mats.Foliage();
        Vector3 D(Vector3 d)
        {
            var bump = 1f + 0.22f * Mathf.Sin(d.X * 4.1f + seed) * Mathf.Cos(d.Z * 3.7f + seed * 1.3f) + 0.12f * Mathf.Sin(d.Y * 6f + seed * 0.7f);
            return c + new Vector3(d.X, d.Y * 0.78f, d.Z) * (r * bump);
        }
        Color C(Vector3 d)
        {
            var k2 = 0.7f + 0.35f * (d.Y * 0.5f + 0.5f) + 0.08f * Mathf.Sin(d.X * 9f + d.Z * 7f + seed);
            return new Color(leaf.R * k2, leaf.G * k2, leaf.B * k2);
        }
        // Normales à mi-chemin entre la sphère et la facette : volume doux sans aspect « low-poly ».
        for (var i = 0; i < Ico.Length; i += 3)
        {
            Vector3 a = D(Ico[i]), b = D(Ico[i + 1]), e = D(Ico[i + 2]);
            var fn = (b - a).Cross(e - a).Normalized();
            if (fn.Dot((a + b + e) / 3 - c) < 0) fn = -fn;
            k.TriSmooth(m, a, b, e, (Ico[i] * 2f + fn).Normalized(), (Ico[i + 1] * 2f + fn).Normalized(), (Ico[i + 2] * 2f + fn).Normalized(),
                C(Ico[i]), C(Ico[i + 1]), C(Ico[i + 2]));
        }
    }

    /// <summary>Palmier : stipe légèrement courbé à anneaux, couronne de palmes arquées dentelées.</summary>
    public static ArrayMesh Palm(float hgt, float seed)
    {
        var k = new MeshKit();
        var bark = Mats.Detail();
        var leaf = Mats.Foliage();
        const int segs = 7;
        var lean = new Vector3(Mathf.Sin(seed) * 0.6f, 0, Mathf.Cos(seed) * 0.6f);
        Vector3 Axis(float t) => new Vector3(0, t * hgt, 0) + lean * (t * t);
        for (var i = 0; i < segs; i++)
        {
            var t0 = i / (float)segs;
            var t1 = (i + 1) / (float)segs;
            var r0 = Mathf.Lerp(0.3f, 0.17f, t0);
            var r1 = Mathf.Lerp(0.3f, 0.17f, t1);
            k.Col = new Color(0.46f, 0.39f, 0.3f) * (0.9f + 0.1f * (i % 2));
            k.Tube(bark, Axis(t0), Axis(t1), (r0 + r1) / 2, 8, false);
            k.Col = new Color(0.36f, 0.3f, 0.23f);
            k.Tube(bark, Axis(t1) - Vector3.Up * 0.08f, Axis(t1) + Vector3.Up * 0.04f, r1 + 0.03f, 8, false);
        }
        var top = Axis(1f);
        k.Col = new Color(0.3f, 0.33f, 0.17f);
        Canopy(k, top + Vector3.Up * 0.15f, 0.42f, seed, new Color(0.3f, 0.33f, 0.17f));

        var fronds = 11;
        for (var f = 0; f < fronds; f++)
        {
            var h1 = Mathf.Sin(seed * 3.1f + f * 2.3f) * 0.5f + 0.5f;
            var dead = f >= fronds - 2;
            var yaw = f * Mathf.Tau / fronds + h1 * 0.4f;
            var dir = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw));
            var side = Vector3.Up.Cross(dir).Normalized();
            var len = dead ? 2.4f : 3.6f + h1 * 1.2f;
            var lift = dead ? -1.2f : 0.45f + h1 * 0.35f;
            var droop = dead ? 0.6f : 0.95f + h1 * 0.4f;
            var baseCol = dead ? new Color(0.45f, 0.36f, 0.2f) : new Color(0.2f, 0.36f, 0.12f);
            var tipCol = dead ? new Color(0.4f, 0.32f, 0.2f) : new Color(0.36f, 0.46f, 0.16f);
            const int n = 12;
            Vector3 Pt(float t) => top + dir * (len * t) + Vector3.Up * (len * (lift * t - droop * t * t));
            for (var s = 0; s < n; s++)
            {
                var ta = s / (float)n;
                var tb = (s + 1) / (float)n;
                // Largeur dentelée : alterne folioles larges et étroites.
                float W(float t, int idx) => 0.62f * Mathf.Sin(Mathf.Pi * Mathf.Pow(t, 0.6f)) * (idx % 2 == 0 ? 1f : 0.55f);
                var pa = Pt(ta); var pb = Pt(tb);
                var wa = W(ta, s); var wb = W(tb, s + 1);
                var ca = baseCol.Lerp(tipCol, ta); var cb = baseCol.Lerp(tipCol, tb);
                var inside = pa - Vector3.Up * 2f; // normales vers le haut
                foreach (var sg in new[] { -1f, 1f })
                {
                    var ea = pa + side * (sg * wa) - Vector3.Up * (wa * 0.35f);
                    var eb = pb + side * (sg * wb) - Vector3.Up * (wb * 0.35f);
                    k.Tri(leaf, pa, pb, eb, inside, ca, cb, cb.Darkened(0.1f));
                    k.Tri(leaf, pa, eb, ea, inside, ca, cb.Darkened(0.1f), ca.Darkened(0.1f));
                }
            }
        }
        return k.Commit();
    }

    /// <summary>Arbre feuillu : tronc conique, branches maîtresses et houppiers bosselés.</summary>
    public static ArrayMesh Tree(float hgt, (float r, Vector3 off)[] blobs, float seed)
    {
        var k = new MeshKit();
        var bark = Mats.Detail();
        k.Col = new Color(0.3f, 0.24f, 0.18f);
        k.Cylinder(bark, Vector3.Zero, 0.32f, 0.16f, hgt * 0.85f, 8, false);
        var leaf = new Color(0.17f, 0.3f, 0.11f);
        foreach (var (r, off) in blobs)
        {
            var c = new Vector3(off.X, hgt + off.Y, off.Z);
            k.Col = new Color(0.3f, 0.24f, 0.18f);
            k.Tube(bark, new Vector3(0, hgt * 0.55f, 0), c * 0.85f + Vector3.Up * (hgt * 0.1f), 0.1f, 5);
            Canopy(k, c, r * 0.95f, seed + off.X * 3f, leaf);
        }
        return k.Commit();
    }

    // ───────────────────────── Véhicules ─────────────────────────

    public enum VehicleType { Car, Van, Truck }

    /// <summary>Longueur et gabarit (largeur, hauteur) de collision d'un véhicule.</summary>
    public static Vector3 VehicleSize(VehicleType t, float carLen) => t switch
    {
        VehicleType.Van => new Vector3(2.0f, 2.2f, 5.2f),
        VehicleType.Truck => new Vector3(2.5f, 3.7f, 7.4f),
        _ => new Vector3(1.85f, 1.5f, carLen),
    };

    private static void Wheel(MeshKit k, Vector3 c, float r, float w, bool left)
    {
        var tire = new Color(0.04f, 0.04f, 0.04f);
        var rot = new Basis(Vector3.Back, -Mathf.Pi / 2); // axe du cylindre selon X
        k.Col = tire;
        k.Cylinder(Mats.Detail(), c - Vector3.Right * (w / 2), r, r, w, 12, true, rot);
        k.Col = new Color(0.55f, 0.56f, 0.58f);
        var face = left ? -w / 2 - 0.006f : w / 2 - 0.004f;
        k.Cylinder(Mats.DetailMetal(), c + Vector3.Right * face, r * 0.6f, r * 0.6f, 0.01f, 10, true, rot);
    }

    /// <summary>Véhicule garé (voiture, fourgon ou camion porteur), maillage mis en cache par type/couleur/longueur.</summary>
    public static ArrayMesh Vehicle(VehicleType type, Color paint, float len)
    {
        var lenKey = Mathf.Snapped(len, 0.2f);
        return Cached($"veh{type}{paint}{lenKey}", k =>
        {
            var p = Mats.CarPaint(paint);
            var glass = Mats.Painted(new Color(0.04f, 0.05f, 0.06f), 0.05f, 0.3f);
            var d = Mats.Detail();
            var lamps = Mats.Lamps();
            var trimC = new Color(0.08f, 0.08f, 0.09f);
            switch (type)
            {
                case VehicleType.Car:
                {
                    var l = lenKey;
                    var wz = l * 0.33f;
                    foreach (var z in new[] { wz, -wz })
                    {
                        Wheel(k, new Vector3(-0.8f, 0.32f, z), 0.32f, 0.22f, true);
                        Wheel(k, new Vector3(0.8f, 0.32f, z), 0.32f, 0.22f, false);
                    }
                    // Caisse basse, capot plongeant, habitacle vitré, pavillon.
                    k.Frustum(p, new Vector3(0, 0.3f, 0), new Vector2(1.82f, l), new Vector2(1.8f, l - 0.1f), 0.5f);
                    k.Frustum(p, new Vector3(0, 0.8f, l * 0.32f), new Vector2(1.78f, l * 0.36f - 0.1f), new Vector2(1.72f, l * 0.3f), 0.1f, new Vector2(0, -0.02f));
                    k.Frustum(p, new Vector3(0, 0.8f, -l * 0.39f), new Vector2(1.78f, l * 0.2f), new Vector2(1.7f, l * 0.18f), 0.14f);
                    k.Frustum(glass, new Vector3(0, 0.8f, -l * 0.06f), new Vector2(1.72f, l * 0.52f), new Vector2(1.42f, l * 0.3f), 0.55f, new Vector2(0, -l * 0.05f));
                    k.Frustum(p, new Vector3(0, 1.35f, -l * 0.11f), new Vector2(1.43f, l * 0.31f), new Vector2(1.38f, l * 0.27f), 0.05f);
                    k.Col = trimC;
                    k.Box(d, new Vector3(0, 0.42f, l / 2 + 0.02f), new Vector3(1.84f, 0.22f, 0.12f));
                    k.Box(d, new Vector3(0, 0.42f, -l / 2 - 0.02f), new Vector3(1.84f, 0.22f, 0.12f));
                    k.Box(d, new Vector3(0, 0.36f, 0), new Vector3(1.86f, 0.1f, l * 0.5f));
                    // Rétroviseurs.
                    k.Box(p, new Vector3(-0.98f, 0.98f, l * 0.16f), new Vector3(0.18f, 0.12f, 0.1f));
                    k.Box(p, new Vector3(0.98f, 0.98f, l * 0.16f), new Vector3(0.18f, 0.12f, 0.1f));
                    // Plaques et feux.
                    k.Col = new Color(0.92f, 0.92f, 0.9f);
                    k.Box(d, new Vector3(0, 0.45f, l / 2 + 0.085f), new Vector3(0.52f, 0.11f, 0.01f));
                    k.Box(d, new Vector3(0, 0.62f, -l / 2 - 0.01f), new Vector3(0.52f, 0.11f, 0.01f));
                    k.Col = new Color(0.95f, 0.95f, 0.88f);
                    k.Box(lamps, new Vector3(-0.62f, 0.68f, l / 2 + 0.005f), new Vector3(0.38f, 0.1f, 0.02f));
                    k.Box(lamps, new Vector3(0.62f, 0.68f, l / 2 + 0.005f), new Vector3(0.38f, 0.1f, 0.02f));
                    k.Col = new Color(0.55f, 0.03f, 0.03f);
                    k.Box(lamps, new Vector3(-0.66f, 0.7f, -l / 2 - 0.005f), new Vector3(0.34f, 0.12f, 0.02f));
                    k.Box(lamps, new Vector3(0.66f, 0.7f, -l / 2 - 0.005f), new Vector3(0.34f, 0.12f, 0.02f));
                    break;
                }
                case VehicleType.Van:
                {
                    const float l = 5.2f;
                    foreach (var z in new[] { 1.65f, -1.65f })
                    {
                        Wheel(k, new Vector3(-0.85f, 0.36f, z), 0.36f, 0.24f, true);
                        Wheel(k, new Vector3(0.85f, 0.36f, z), 0.36f, 0.24f, false);
                    }
                    k.Box(p, new Vector3(0, 1.25f, -0.45f), new Vector3(2.0f, 1.8f, l - 0.9f));
                    k.Frustum(p, new Vector3(0, 0.35f, l / 2 - 0.45f), new Vector2(1.96f, 0.9f), new Vector2(1.94f, 0.8f), 0.75f, new Vector2(0, -0.04f));
                    k.Frustum(glass, new Vector3(0, 1.1f, l / 2 - 0.5f), new Vector2(1.9f, 0.8f), new Vector2(1.9f, 0.04f), 0.95f, new Vector2(0, -0.36f));
                    k.Box(glass, new Vector3(0, 1.62f, l / 2 - 1.45f), new Vector3(2.02f, 0.62f, 0.95f));
                    k.Col = trimC;
                    k.Box(d, new Vector3(0, 0.45f, l / 2 + 0.03f), new Vector3(2.0f, 0.25f, 0.1f));
                    k.Box(d, new Vector3(0, 0.45f, -l / 2 + 0.0f), new Vector3(2.0f, 0.25f, 0.12f));
                    k.Box(d, new Vector3(0, 0.38f, -0.2f), new Vector3(2.02f, 0.14f, l - 1.6f));
                    k.Col = new Color(0.95f, 0.95f, 0.88f);
                    k.Box(lamps, new Vector3(-0.7f, 0.85f, l / 2 + 0.005f), new Vector3(0.32f, 0.14f, 0.02f));
                    k.Box(lamps, new Vector3(0.7f, 0.85f, l / 2 + 0.005f), new Vector3(0.32f, 0.14f, 0.02f));
                    k.Col = new Color(0.55f, 0.03f, 0.03f);
                    k.Box(lamps, new Vector3(-0.88f, 0.9f, -l / 2 + 0.06f), new Vector3(0.14f, 0.4f, 0.02f));
                    k.Box(lamps, new Vector3(0.88f, 0.9f, -l / 2 + 0.06f), new Vector3(0.14f, 0.4f, 0.02f));
                    break;
                }
                default:
                {
                    // Camion porteur : cabine avancée + caisse de livraison.
                    const float l = 7.4f;
                    foreach (var z in new[] { 2.5f, -1.6f, -2.6f })
                    {
                        Wheel(k, new Vector3(-1.0f, 0.48f, z), 0.48f, 0.3f, true);
                        Wheel(k, new Vector3(1.0f, 0.48f, z), 0.48f, 0.3f, false);
                    }
                    k.Col = trimC;
                    k.Box(d, new Vector3(0, 0.75f, -0.3f), new Vector3(1.9f, 0.35f, l - 1.0f));
                    k.Box(p, new Vector3(0, 1.85f, l / 2 - 1.05f), new Vector3(2.3f, 2.3f, 2.0f));
                    k.Box(glass, new Vector3(0, 2.35f, l / 2 - 0.04f), new Vector3(2.1f, 0.9f, 0.06f));
                    k.Box(glass, new Vector3(0, 2.35f, l / 2 - 0.9f), new Vector3(2.32f, 0.8f, 0.9f));
                    k.Col = new Color(0.93f, 0.93f, 0.91f);
                    k.Box(Mats.DetailMetal(), new Vector3(0, 2.3f, -1.15f), new Vector3(2.45f, 2.7f, 5.1f));
                    k.Col = paint;
                    k.Box(d, new Vector3(0, 1.4f, -1.15f), new Vector3(2.47f, 0.3f, 5.0f));
                    k.Col = trimC;
                    k.Box(d, new Vector3(0, 0.55f, l / 2 + 0.0f), new Vector3(2.3f, 0.3f, 0.12f));
                    k.Col = new Color(0.95f, 0.95f, 0.88f);
                    k.Box(lamps, new Vector3(-0.85f, 0.95f, l / 2 + 0.005f), new Vector3(0.3f, 0.16f, 0.02f));
                    k.Box(lamps, new Vector3(0.85f, 0.95f, l / 2 + 0.005f), new Vector3(0.3f, 0.16f, 0.02f));
                    k.Col = new Color(0.55f, 0.03f, 0.03f);
                    k.Box(lamps, new Vector3(-1.0f, 0.75f, -l / 2 + 0.25f), new Vector3(0.3f, 0.14f, 0.02f));
                    k.Box(lamps, new Vector3(1.0f, 0.75f, -l / 2 + 0.25f), new Vector3(0.3f, 0.14f, 0.02f));
                    break;
                }
            }
        });
    }
}

/// <summary>
/// Regroupe les instances d'accessoires identiques en MultiMesh, par cellule de 100 m,
/// avec une distance de visibilité (pas d'ombres portées pour les petits objets).
/// </summary>
public class PropScatter
{
    private const float Cell = 100f;

    private sealed class Batch
    {
        public Mesh Mesh;
        public bool Colored;
        public bool Shadows;
        public float Range;
        public Vector3 Origin;
        public readonly List<Transform3D> Xf = new();
        public readonly List<Color> Colors = new();
    }

    private readonly Dictionary<(Mesh, int, int), Batch> _batches = new();

    public void Add(Mesh mesh, Vector3 pos, float yaw, Color? color = null, float range = 160f, bool shadows = false, float scale = 1f)
    {
        var cx = Mathf.FloorToInt(pos.X / Cell);
        var cz = Mathf.FloorToInt(pos.Z / Cell);
        var key = (mesh, cx, cz);
        if (!_batches.TryGetValue(key, out var b))
        {
            b = new Batch
            {
                Mesh = mesh, Colored = color.HasValue, Shadows = shadows, Range = range,
                Origin = new Vector3((cx + 0.5f) * Cell, 0, (cz + 0.5f) * Cell),
            };
            _batches[key] = b;
        }
        b.Xf.Add(new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * scale), pos - b.Origin));
        b.Colors.Add(color ?? Colors.White);
    }

    public void Flush(Node3D root)
    {
        foreach (var b in _batches.Values)
        {
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = b.Colored,
                Mesh = b.Mesh,
            };
            mm.InstanceCount = b.Xf.Count;
            for (var i = 0; i < b.Xf.Count; i++)
            {
                mm.SetInstanceTransform(i, b.Xf[i]);
                if (b.Colored) mm.SetInstanceColor(i, b.Colors[i]);
            }
            root.AddChild(new MultiMeshInstance3D
            {
                Multimesh = mm,
                Position = b.Origin,
                CastShadow = b.Shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                // La cellule fait 100 m : la marge couvre sa demi-diagonale.
                VisibilityRangeEnd = b.Range + Cell * 0.71f,
            });
        }
        _batches.Clear();
    }
}
