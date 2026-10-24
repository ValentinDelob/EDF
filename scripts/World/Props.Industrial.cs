using Godot;

namespace Rempart;

/// <summary>
/// Modèles de la zone industrielle (repère local : pied au centre, y vers le haut) : cuves de stockage à toit fixe et
/// escalier hélicoïdal, sphères de gaz sur pieds, réservoirs horizontaux sur berceaux, cuves de procédé, wagons
/// (citerne, couvert, trémie), panneaux de grillage. Chaque cuve a son épave (tôle déchirée et calcinée) : elle
/// remplace le modèle intact quand la cuve explose (voir <c>Destructibles.Tanks.cs</c>).
/// Triangles solides enregistrés (collision exacte) ; marches, rambardes et petits détails restent non solides
/// (une plaque inclinée sous le nez des marches sert de rampe, comme les escaliers de secours).
/// </summary>
public static partial class Props
{
    // Peintures industrielles ternes (multipliées par le shader d'usure : rouille, coulures, crasse).
    private static readonly Color TankWhite = new(0.6f, 0.6f, 0.57f);
    private static readonly Color TankGrey = new(0.42f, 0.43f, 0.42f);
    private static readonly Color TankGreen = new(0.33f, 0.37f, 0.31f);
    private static readonly Color TankSand = new(0.52f, 0.49f, 0.42f);
    private static readonly Color Steel = new(0.2f, 0.205f, 0.2f);
    private static readonly Color SafetyYellow = new(0.46f, 0.39f, 0.16f);
    private static readonly Color RustBrown = new(0.27f, 0.16f, 0.11f);

    /// <summary>Dimensions des cuves de stockage (rayon, hauteur de robe) par variante.</summary>
    public static readonly (float r, float h)[] StorageTankDims = { (11f, 13f), (9f, 15.5f), (12.5f, 10.5f) };

    public const float SphereR = 7.5f, SphereY = 10.6f;
    public const float BulletR = 1.7f, BulletLen = 15f, BulletAxisY = 3.0f;
    public const float VesselR = 2.4f, VesselH = 9f;
    public const float WagonLen = 16.2f, WagonW = 2.9f;

    private static ShaderMaterial Paint(Color c) => Mats.Worn(c, 0.62f, 0.25f);

    /// <summary>Maillage mis en cache et rattaché à une famille destructible (sans passer par les préfixes de clés).</summary>
    private static ArrayMesh CachedKind(string key, PropKind kind, System.Action<MeshKit> build)
    {
        var m = Cached(key, build);
        Kinds[m] = kind;
        return m;
    }

    /// <summary>Quad visible des deux côtés (tôles déchirées, parois vues de l'intérieur).</summary>
    private static void Quad2(MeshKit k, Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        k.Quad(m, a, b, c, d);
        k.Quad(m, d, c, b, a);
    }

    /// <summary>Sphère (ou calotte) à normales lissées, latitudes de <paramref name="lat0"/> à <paramref name="lat1"/> (radians, -π/2 au pôle sud).</summary>
    private static void SpherePart(MeshKit k, Material m, Vector3 c, Vector3 radii, int segU, int segV, float lat0 = -Mathf.Pi / 2, float lat1 = Mathf.Pi / 2, Basis? rot = null)
    {
        var b = rot ?? Basis.Identity;
        Vector3 Pt(float lon, float lat) => new(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
        for (var j = 0; j < segV; j++)
        {
            var la0 = Mathf.Lerp(lat0, lat1, j / (float)segV);
            var la1 = Mathf.Lerp(lat0, lat1, (j + 1) / (float)segV);
            for (var i = 0; i < segU; i++)
            {
                var lo0 = i * Mathf.Tau / segU;
                var lo1 = (i + 1) * Mathf.Tau / segU;
                var n00 = Pt(lo0, la0); var n10 = Pt(lo1, la0); var n11 = Pt(lo1, la1); var n01 = Pt(lo0, la1);
                Vector3 P(Vector3 n) => c + b * (n * radii);
                Vector3 N(Vector3 n) => (b * new Vector3(n.X / radii.X, n.Y / radii.Y, n.Z / radii.Z)).Normalized();
                var col = k.Col;
                if (Mathf.Cos(la0) > 0.001f) k.TriSmooth(m, P(n00), P(n10), P(n11), N(n00), N(n10), N(n11), col, col, col);
                if (Mathf.Cos(la1) > 0.001f) k.TriSmooth(m, P(n00), P(n11), P(n01), N(n00), N(n11), N(n01), col, col, col);
            }
        }
    }

    /// <summary>Repère d'une marche ou d'un caillebotis tangent à un cercle (X radial, Z tangent).</summary>
    private static Basis Radial(float a) => new(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), Vector3.Up, new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)));

    private static Vector3 Polar(float r, float a, float y) => new(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);

    /// <summary>
    /// Escalier hélicoïdal le long d'une robe de cuve : plaque inclinée solide (rampe) entre les limons, marches et
    /// rambarde extérieure non solides, palier en haut. Renvoie l'angle d'arrivée.
    /// </summary>
    private static float SpiralStair(MeshKit k, float r, float h, float a0, float fraction = 1f)
    {
        var metal = Mats.DetailMetal();
        var r0 = r + 0.06f;
        var r1 = r + 1.05f;
        var rm = (r0 + r1) / 2;
        var span = h / Mathf.Tan(Mathf.DegToRad(34f)) / rm;
        var top = h * fraction;
        var n = Mathf.CeilToInt(span * fraction / 0.07f);
        float Y(float a) => (a - a0) / span * h;
        // Rampe (dessous des marches) : solide, visible des deux côtés.
        k.Col = Steel;
        for (var i = 0; i < n; i++)
        {
            var aa = a0 + span * fraction * i / n;
            var ab = a0 + span * fraction * (i + 1) / n;
            Quad2(k, metal, Polar(r0, aa, Y(aa)), Polar(r1, aa, Y(aa)), Polar(r1, ab, Y(ab)), Polar(r0, ab, Y(ab)));
            // Limon extérieur (tôle de rive).
            k.Solid = false;
            Quad2(k, metal, Polar(r1, aa, Y(aa) - 0.25f), Polar(r1, ab, Y(ab) - 0.25f), Polar(r1, ab, Y(ab) + 0.08f), Polar(r1, aa, Y(aa) + 0.08f));
            k.Solid = true;
        }
        // Palier d'arrivée à plat, qui recouvre le bord du toit (aucune marche à franchir pour passer sur le toit).
        if (fraction >= 1f)
        {
            var ae = a0 + span;
            for (var i = 0; i < 4; i++)
            {
                var aa = ae + 0.05f * i;
                var ab = aa + 0.05f;
                Quad2(k, metal, Polar(r - 0.9f, aa, h), Polar(r1, aa, h), Polar(r1, ab, h), Polar(r - 0.9f, ab, h));
            }
        }
        // Marches (non solides) : arête juste au-dessus de la rampe.
        k.Solid = false;
        var steps = Mathf.CeilToInt(top / 0.21f);
        for (var s = 0; s < steps; s++)
        {
            var a = a0 + span * fraction * (s + 0.5f) / steps;
            k.Col = Steel * 1.15f;
            k.Box(metal, Polar(rm, a, Y(a) + 0.05f), new Vector3(r1 - r0, 0.04f, 0.27f), Radial(a));
        }
        // Rambarde extérieure (poteaux, lisse, sous-lisse) en jaune sécurité délavé.
        k.Col = SafetyYellow;
        var posts = Mathf.CeilToInt(span * fraction * r1 / 1.6f);
        Vector3 prevTop = default, prevMid = default;
        for (var p = 0; p <= posts; p++)
        {
            var a = a0 + span * fraction * p / posts;
            var b = Polar(r1 - 0.04f, a, Y(a));
            var t = b + Vector3.Up * 1.05f;
            k.Tube(metal, b, t, 0.025f, 4);
            if (p > 0)
            {
                k.Tube(metal, prevTop, t, 0.025f, 4);
                k.Tube(metal, prevMid, b + Vector3.Up * 0.55f, 0.02f, 4);
            }
            prevTop = t;
            prevMid = b + Vector3.Up * 0.55f;
        }
        k.Solid = true;
        return a0 + span * fraction;
    }

    /// <summary>Garde-corps circulaire (poteaux, lisse) non solide.</summary>
    private static void RailRing(MeshKit k, float r, float y, int posts, float from = 0f, float to = Mathf.Tau)
    {
        var metal = Mats.DetailMetal();
        var keep = k.Solid;
        k.Solid = false;
        k.Col = SafetyYellow * 0.9f;
        Vector3 prev = default;
        for (var i = 0; i <= posts; i++)
        {
            var a = Mathf.Lerp(from, to, i / (float)posts);
            var b = Polar(r, a, y);
            var t = b + Vector3.Up * 1.05f;
            k.Tube(metal, b, t, 0.025f, 4);
            if (i > 0) k.Tube(metal, prev, t, 0.025f, 4);
            prev = t;
        }
        k.Solid = keep;
    }

    // ───────────────────────── Cuves de stockage ─────────────────────────

    /// <summary>
    /// Cuve de stockage d'hydrocarbures à toit fixe : robe en viroles soudées, raidisseur de vent, toit conique
    /// à garde-corps, escalier hélicoïdal jusqu'au toit, chambres à mousse, tuyauteries et vannes au pied.
    /// </summary>
    public static ArrayMesh StorageTank(int v) => Cached($"indtank{v}", k =>
    {
        var (r, h) = StorageTankDims[v];
        var paint = Paint(v == 1 ? TankGreen : v == 2 ? TankSand : TankWhite);
        var metal = Mats.DetailMetal();
        var concrete = Mats.Detail();
        // Socle en béton.
        k.Col = new Color(0.42f, 0.41f, 0.39f);
        k.Cylinder(concrete, Vector3.Zero, r + 0.7f, r + 0.6f, 0.35f, 40);
        // Robe.
        k.Col = Colors.White;
        k.Cylinder(paint, Vector3.Up * 0.35f, r, r, h - 0.35f, 40, false);
        // Joints des viroles et raidisseur de vent (non solides : relief).
        k.Solid = false;
        k.Col = Steel * 1.3f;
        for (var y = 2.4f; y < h - 0.5f; y += 2.4f) k.Cylinder(metal, Vector3.Up * y, r + 0.03f, r + 0.03f, 0.05f, 40, false);
        k.Cylinder(metal, Vector3.Up * (h - 1.1f), r + 0.45f, r + 0.45f, 0.18f, 40, true);
        k.Solid = true;
        // Toit conique (solide, praticable).
        k.Col = Colors.White * 0.92f;
        k.Cylinder(paint, Vector3.Up * h, r + 0.12f, 0.9f, r * 0.13f, 40, true);
        // Trappe, évents, chambres à mousse (non solides).
        k.Solid = false;
        k.Col = Steel;
        k.Box(metal, Polar(r * 0.55f, 0.6f, h + r * 0.06f), new Vector3(0.9f, 0.3f, 0.9f));
        for (var i = 0; i < 3; i++)
        {
            var a = 0.4f + i * Mathf.Tau / 3;
            k.Cylinder(metal, Polar(r * 0.3f, a, h + r * 0.1f), 0.18f, 0.18f, 0.9f, 8);
            k.Col = new Color(0.4f, 0.12f, 0.08f);
            k.Box(metal, Polar(r + 0.35f, a + 0.9f, h - 0.6f), new Vector3(0.5f, 0.7f, 0.5f), Radial(a + 0.9f));
            k.Tube(metal, Polar(r + 0.35f, a + 0.9f, h - 0.95f), Polar(r + 0.35f, a + 0.9f, 0.3f), 0.07f, 6);
            k.Col = Steel;
        }
        RailRing(k, r - 0.15f, h + 0.02f, 56);
        // Tuyauteries au pied : piquages, vannes, coudes vers le sol.
        k.Col = new Color(0.26f, 0.25f, 0.23f);
        foreach (var a in new[] { 3.6f, 4.1f })
        {
            var p0 = Polar(r, a, 0.9f);
            var p1 = Polar(r + 2.4f, a, 0.9f);
            k.Tube(metal, p0, p1, 0.3f, 10, true);
            k.Box(metal, Polar(r + 1.2f, a, 0.9f), new Vector3(0.5f, 0.9f, 0.9f), Radial(a));
            k.Cylinder(metal, Polar(r + 1.2f, a, 1.35f), 0.05f, 0.05f, 0.6f, 6);
            k.Cylinder(metal, Polar(r + 1.2f, a, 1.95f), 0.32f, 0.32f, 0.05f, 12);
            k.Tube(metal, p1, p1 - Vector3.Up * 1.4f, 0.3f, 10, true);
        }
        k.Solid = true;
        SpiralStair(k, r, h, 1.2f);
    });

    /// <summary>
    /// Épave de cuve de stockage : robe déchirée en dents de scie, tôles retroussées vers l'extérieur, toit effondré
    /// dans la cuvette, résidus calcinés au fond, bas de l'escalier encore debout. Parois visibles des deux côtés.
    /// </summary>
    public static ArrayMesh StorageTankWreck(int v) => Cached($"indtankw{v}", k =>
    {
        var (r, h) = StorageTankDims[v];
        var ch = Mats.Charred();
        var concrete = Mats.Detail();
        k.Col = new Color(0.25f, 0.24f, 0.23f);
        k.Cylinder(concrete, Vector3.Zero, r + 0.7f, r + 0.6f, 0.35f, 40);
        const int seg = 40;
        var tops = new float[seg + 1];
        for (var i = 0; i <= seg; i++)
        {
            var ii = i % seg;
            tops[i] = h * Mathf.Clamp(0.22f + 0.5f * H(ii / 3, v, 61) + 0.15f * H(ii, v, 62), 0.18f, 0.85f);
        }
        for (var i = 0; i < seg; i++)
        {
            var a0 = i * Mathf.Tau / seg;
            var a1 = (i + 1) * Mathf.Tau / seg;
            var t0 = tops[i];
            var t1 = tops[i + 1];
            var peel = H(i / 2, v, 63) < 0.35f;
            k.Col = H(i, v, 64) < 0.4f ? BurnSoot : H(i, v, 65) < 0.7f ? BurnRust : BurnBase;
            var hb0 = peel ? t0 * 0.55f : t0;
            var hb1 = peel ? t1 * 0.55f : t1;
            Quad2(k, ch, Polar(r, a0, 0.35f), Polar(r, a1, 0.35f), Polar(r, a1, hb1), Polar(r, a0, hb0));
            if (peel)
            {
                // Tôle retroussée : la partie haute s'ouvre vers l'extérieur en se tordant.
                var out0 = (t0 - hb0) * (0.5f + H(i, v, 66) * 0.7f);
                var out1 = (t1 - hb1) * (0.5f + H(i + 1, v, 66) * 0.7f);
                Quad2(k, ch, Polar(r, a0, hb0), Polar(r, a1, hb1), Polar(r + out1, a1, hb1 + (t1 - hb1) * 0.6f), Polar(r + out0, a0, hb0 + (t0 - hb0) * 0.6f));
            }
        }
        // Résidus calcinés au fond (praticables), toit effondré en plaques inclinées.
        k.Col = BurnSoot * 0.7f;
        k.Cylinder(ch, Vector3.Up * 0.35f, r - 0.05f, r - 0.05f, 0.5f, 40, true);
        for (var i = 0; i < 4; i++)
        {
            var a = H(i, v, 67) * Mathf.Tau;
            var tilt = 0.25f + H(i, v, 68) * 0.35f;
            k.Col = i % 2 == 0 ? BurnRust : BurnSoot;
            k.Box(ch, Polar(r * 0.45f, a, 1.2f + r * 0.18f * Mathf.Sin(tilt)), new Vector3(r * 0.9f, 0.08f, r * 0.6f), Radial(a) * new Basis(Vector3.Back, tilt));
        }
        // Bas de l'escalier, calciné.
        k.Col = BurnSoot;
        var keep = k.Col;
        SpiralStairCharred(k, r, h, 1.2f, 0.3f);
        k.Col = keep;
    });

    /// <summary>Bas d'escalier hélicoïdal calciné (rampe seule).</summary>
    private static void SpiralStairCharred(MeshKit k, float r, float h, float a0, float fraction)
    {
        var ch = Mats.Charred();
        var r0 = r + 0.06f;
        var r1 = r + 1.05f;
        var rm = (r0 + r1) / 2;
        var span = h / Mathf.Tan(Mathf.DegToRad(34f)) / rm;
        var n = Mathf.CeilToInt(span * fraction / 0.08f);
        float Y(float a) => (a - a0) / span * h;
        for (var i = 0; i < n; i++)
        {
            var aa = a0 + span * fraction * i / n;
            var ab = a0 + span * fraction * (i + 1) / n;
            Quad2(k, ch, Polar(r0, aa, Y(aa)), Polar(r1, aa, Y(aa)), Polar(r1, ab, Y(ab)), Polar(r0, ab, Y(ab)));
        }
    }

    // ───────────────────────── Sphère de gaz ─────────────────────────

    /// <summary>
    /// Sphère de stockage de gaz liquéfié (Horton) : dix poteaux à l'équateur avec croix de contreventement,
    /// escalier droit jusqu'à une coursive équatoriale, échelle à crinoline jusqu'à la plateforme sommitale.
    /// </summary>
    public static ArrayMesh GasSphere() => Cached("indsphere", k =>
    {
        const float r = SphereR, cy = SphereY;
        var paint = Paint(TankWhite);
        var metal = Mats.DetailMetal();
        var concrete = Mats.Detail();
        k.Col = Colors.White;
        SpherePart(k, paint, new Vector3(0, cy, 0), Vector3.One * r, 36, 18);
        // Ceinture équatoriale (relief).
        k.Solid = false;
        k.Col = Steel * 1.3f;
        k.Cylinder(metal, Vector3.Up * (cy - 0.15f), r + 0.04f, r + 0.04f, 0.3f, 36, false);
        k.Solid = true;
        // Poteaux et socles ; croix de contreventement (non solides).
        const int legs = 10;
        for (var i = 0; i < legs; i++)
        {
            var a = i * Mathf.Tau / legs;
            k.Col = new Color(0.45f, 0.45f, 0.43f);
            k.Cylinder(paint, Polar(r * 0.98f, a, 0), 0.36f, 0.36f, cy, 10);
            k.Col = new Color(0.4f, 0.39f, 0.37f);
            k.Box(concrete, Polar(r * 0.98f, a, 0.25f), new Vector3(1.4f, 0.5f, 1.4f), Radial(a));
            var b = (i + 1) * Mathf.Tau / legs;
            k.Solid = false;
            k.Col = Steel;
            k.Tube(metal, Polar(r * 0.98f, a, 0.8f), Polar(r * 0.98f, b, cy - 3.2f), 0.06f, 5);
            k.Tube(metal, Polar(r * 0.98f, b, 0.8f), Polar(r * 0.98f, a, cy - 3.2f), 0.06f, 5);
            k.Solid = true;
        }
        // Escalier droit (rampe solide sous les marches) jusqu'à la coursive.
        var run = cy / Mathf.Tan(Mathf.DegToRad(38f));
        const float sx = r + 1.9f;
        var z0 = -run / 2 - 0.6f;
        var z1 = run / 2 - 0.6f;
        k.Col = Steel;
        Quad2(k, metal, new Vector3(sx - 0.55f, 0, z0), new Vector3(sx + 0.55f, 0, z0), new Vector3(sx + 0.55f, cy, z1), new Vector3(sx - 0.55f, cy, z1));
        k.Solid = false;
        var steps = Mathf.CeilToInt(cy / 0.21f);
        for (var s = 0; s < steps; s++)
        {
            var u = (s + 0.5f) / steps;
            k.Box(metal, new Vector3(sx, u * cy + 0.05f, Mathf.Lerp(z0, z1, u)), new Vector3(1.1f, 0.04f, 0.28f));
        }
        k.Col = SafetyYellow;
        foreach (var side in new[] { -0.58f, 0.58f })
        {
            k.Tube(metal, new Vector3(sx + side, 1.05f, z0), new Vector3(sx + side, cy + 1.05f, z1), 0.025f, 4);
            for (var p = 0; p <= 6; p++)
            {
                var u = p / 6f;
                var b = new Vector3(sx + side, u * cy, Mathf.Lerp(z0, z1, u));
                k.Tube(metal, b, b + Vector3.Up * 1.05f, 0.025f, 4);
            }
        }
        k.Solid = true;
        // Palier en haut de l'escalier, relié à la coursive équatoriale (arc côté +X).
        k.Col = Steel;
        k.Box(metal, new Vector3(sx, cy - 0.05f, z1 + 0.7f), new Vector3(1.3f, 0.1f, 1.4f));
        var w0 = r + 0.08f;
        var w1 = r + 1.3f;
        for (var i = 0; i < 12; i++)
        {
            var a = -0.7f + 1.4f * i / 12f;
            var b = -0.7f + 1.4f * (i + 1) / 12f;
            Quad2(k, metal, Polar(w0, a, cy - 0.02f), Polar(w1, a, cy - 0.02f), Polar(w1, b, cy - 0.02f), Polar(w0, b, cy - 0.02f));
        }
        RailRing(k, w1 - 0.04f, cy, 12, -0.7f, 0.7f);
        // Échelle à crinoline vers le sommet (non solide), plateforme sommitale (solide).
        k.Solid = false;
        k.Col = SafetyYellow * 0.8f;
        for (var i = 0; i < 10; i++)
        {
            var la0 = i / 10f * Mathf.Pi / 2;
            var la1 = (i + 1) / 10f * Mathf.Pi / 2;
            var p0 = new Vector3(Mathf.Cos(la0) * (r + 0.5f), cy + Mathf.Sin(la0) * (r + 0.5f), 0.25f);
            var p1 = new Vector3(Mathf.Cos(la1) * (r + 0.5f), cy + Mathf.Sin(la1) * (r + 0.5f), 0.25f);
            k.Tube(metal, p0, p1, 0.03f, 4);
            k.Tube(metal, p0 with { Z = -0.25f }, p1 with { Z = -0.25f }, 0.03f, 4);
        }
        k.Solid = true;
        k.Col = Steel;
        k.Cylinder(metal, Vector3.Up * (cy + r - 0.15f), 1.6f, 1.6f, 0.2f, 16);
        RailRing(k, 1.55f, cy + r + 0.05f, 14);
        // Soupapes au sommet.
        k.Solid = false;
        k.Col = new Color(0.38f, 0.12f, 0.08f);
        k.Cylinder(metal, new Vector3(0.6f, cy + r + 0.05f, 0.3f), 0.15f, 0.12f, 0.9f, 8);
        k.Cylinder(metal, new Vector3(-0.5f, cy + r + 0.05f, -0.4f), 0.15f, 0.12f, 0.9f, 8);
        k.Solid = true;
    });

    /// <summary>Épave de sphère : calotte inférieure déchirée restée sur ses poteaux (dont deux pliés), coursive arrachée.</summary>
    public static ArrayMesh GasSphereWreck() => Cached("indspherew", k =>
    {
        const float r = SphereR, cy = SphereY;
        var ch = Mats.Charred();
        var concrete = Mats.Detail();
        // Calotte inférieure aux bords déchiquetés : bandes de latitude, chaque fuseau s'arrête à une hauteur variable.
        const int segU = 30;
        const int segV = 10;
        for (var i = 0; i < segU; i++)
        {
            var lo0 = i * Mathf.Tau / segU;
            var lo1 = (i + 1) * Mathf.Tau / segU;
            var cut = Mathf.Lerp(-0.55f, 0.15f, H(i / 2, 3, 71) * 0.7f + H(i, 3, 72) * 0.3f);
            for (var j = 0; j < segV; j++)
            {
                var la0 = Mathf.Lerp(-Mathf.Pi / 2, cut, j / (float)segV);
                var la1 = Mathf.Lerp(-Mathf.Pi / 2, cut, (j + 1) / (float)segV);
                Vector3 P(float lo, float la) => new Vector3(Mathf.Cos(la) * Mathf.Cos(lo), Mathf.Sin(la), Mathf.Cos(la) * Mathf.Sin(lo)) * r + Vector3.Up * cy;
                k.Col = H(i, j, 73) < 0.4f ? BurnSoot : H(i, j, 74) < 0.7f ? BurnRust : BurnBase;
                Quad2(k, ch, P(lo0, la0), P(lo1, la0), P(lo1, la1), P(lo0, la1));
            }
        }
        // Poteaux : deux pliés vers l'extérieur.
        for (var i = 0; i < 10; i++)
        {
            var a = i * Mathf.Tau / 10;
            k.Col = new Color(0.3f, 0.29f, 0.28f);
            k.Box(concrete, Polar(r * 0.98f, a, 0.25f), new Vector3(1.4f, 0.5f, 1.4f), Radial(a));
            k.Col = i % 3 == 0 ? BurnRust : BurnSoot;
            if (i == 2 || i == 7)
            {
                var knee = Polar(r * 0.98f, a, cy * 0.45f);
                k.Tube(ch, Polar(r * 0.98f, a, 0), knee, 0.36f, 8, true);
                k.Tube(ch, knee, Polar(r * 0.98f + 3.5f, a + 0.15f, cy * 0.3f), 0.36f, 8, true);
            }
            else k.Cylinder(ch, Polar(r * 0.98f, a, 0), 0.36f, 0.36f, cy - 0.6f, 8);
        }
        // Tôles projetées au pied.
        for (var i = 0; i < 5; i++)
        {
            var a = H(i, 5, 75) * Mathf.Tau;
            k.Col = BurnRust;
            k.Box(ch, Polar(r + 2f + H(i, 5, 76) * 4f, a, 0.6f), new Vector3(3.2f, 0.06f, 2.4f), Radial(a) * new Basis(Vector3.Back, 0.3f + H(i, 5, 77) * 0.5f));
        }
    });

    // ───────────────────────── Réservoir horizontal ─────────────────────────

    /// <summary>Réservoir de GPL horizontal (« cigare ») sur deux berceaux en béton : fonds bombés, soupapes, passerelle et échelle.</summary>
    public static ArrayMesh BulletTank(int v) => Cached($"indbullet{v}", k =>
    {
        const float r = BulletR, len = BulletLen, cy = BulletAxisY;
        var paint = Paint(v == 0 ? TankWhite : TankGrey);
        var metal = Mats.DetailMetal();
        var concrete = Mats.Detail();
        var along = new Basis(Vector3.Right, Mathf.Pi / 2); // axe du cylindre selon Z
        var body = len - 2 * r * 0.55f;
        k.Col = Colors.White;
        k.Cylinder(paint, new Vector3(0, cy, -body / 2), r, r, body, 28, false, along);
        SpherePart(k, paint, new Vector3(0, cy, body / 2), new Vector3(r, r * 0.55f, r), 28, 7, 0, Mathf.Pi / 2, along);
        SpherePart(k, paint, new Vector3(0, cy, -body / 2), new Vector3(r, r * 0.55f, r), 28, 7, -Mathf.Pi / 2, 0, along);
        // Berceaux.
        k.Col = new Color(0.44f, 0.43f, 0.41f);
        foreach (var z in new[] { -body * 0.3f, body * 0.3f })
        {
            k.Box(concrete, new Vector3(0, (cy - r * 0.5f) / 2, z), new Vector3(r * 1.7f, cy - r * 0.5f, 1f));
            k.Col = Steel;
            k.Box(metal, new Vector3(0, cy - r * 0.55f, z), new Vector3(r * 1.9f, 0.3f, 0.5f));
            k.Col = new Color(0.44f, 0.43f, 0.41f);
        }
        // Passerelle sur le dessus, soupapes, échelle (non solides sauf la passerelle).
        k.Col = Steel;
        k.Box(metal, new Vector3(0, cy + r + 0.08f, 0), new Vector3(0.9f, 0.06f, 3.2f));
        k.Solid = false;
        k.Col = new Color(0.4f, 0.12f, 0.08f);
        foreach (var z in new[] { -0.8f, 0.8f }) k.Cylinder(metal, new Vector3(0.6f, cy + r, z), 0.12f, 0.1f, 0.8f, 8);
        k.Col = SafetyYellow;
        foreach (var z in new[] { 1.4f, 1.8f }) k.Tube(metal, new Vector3(r + 0.6f, 0, z), new Vector3(r * 0.4f, cy + r + 0.1f, z), 0.025f, 4);
        // Tuyauterie de soutirage.
        k.Col = new Color(0.26f, 0.25f, 0.23f);
        k.Tube(metal, new Vector3(0, cy - r, -body * 0.1f), new Vector3(0, 0.5f, -body * 0.1f), 0.12f, 8);
        k.Tube(metal, new Vector3(0, 0.5f, -body * 0.1f), new Vector3(r + 2.5f, 0.5f, -body * 0.1f), 0.12f, 8);
        k.Solid = true;
    });

    /// <summary>Épave de réservoir horizontal : berceaux, moitié basse de la virole ouverte en longueur, un fond arraché.</summary>
    public static ArrayMesh BulletTankWreck() => Cached("indbulletw", k =>
    {
        const float r = BulletR, len = BulletLen, cy = BulletAxisY;
        var ch = Mats.Charred();
        var concrete = Mats.Detail();
        var body = len - 2 * r * 0.55f;
        k.Col = new Color(0.27f, 0.26f, 0.25f);
        foreach (var z in new[] { -body * 0.3f, body * 0.3f })
            k.Box(concrete, new Vector3(0, (cy - r * 0.5f) / 2, z), new Vector3(r * 1.7f, cy - r * 0.5f, 1f));
        // Gouttière déchirée (fuseaux bas, bords irréguliers), affaissée.
        const int seg = 16;
        for (var i = 0; i < seg; i++)
        {
            var z0 = -body / 2 + body * i / seg;
            var z1 = -body / 2 + body * (i + 1) / seg;
            var cut = 0.3f + H(i, 1, 81) * 1.1f;
            for (var j = 0; j < 6; j++)
            {
                var a0 = -Mathf.Pi / 2 - cut + 2 * cut * j / 6f;
                var a1 = -Mathf.Pi / 2 - cut + 2 * cut * (j + 1) / 6f;
                Vector3 P(float a, float z) => new(Mathf.Cos(a) * r, cy - 0.5f + Mathf.Sin(a) * r, z);
                k.Col = H(i, j, 82) < 0.5f ? BurnSoot : BurnRust;
                Quad2(k, ch, P(a0, z0), P(a1, z0), P(a1, z1), P(a0, z1));
            }
        }
        // Fond arraché projeté au sol.
        k.Col = BurnRust;
        SpherePart(k, ch, new Vector3(r + 3.5f, 0.6f, body / 2 + 2f), new Vector3(r, r * 0.55f, r), 14, 4, 0, Mathf.Pi / 2, new Basis(Vector3.Right, 2.4f));
    });

    // ───────────────────────── Cuve de procédé ─────────────────────────

    /// <summary>Cuve de procédé verticale sur jupe : fond bombé, échelle à crinoline, plateforme et soupape au sommet.</summary>
    public static ArrayMesh ProcessVessel(int v) => Cached($"indvessel{v}", k =>
    {
        const float r = VesselR, h = VesselH;
        var paint = Paint(v == 0 ? TankGrey : TankWhite);
        var metal = Mats.DetailMetal();
        k.Col = Steel * 1.2f;
        k.Cylinder(metal, Vector3.Zero, r * 0.85f, r * 0.85f, 1.2f, 20); // jupe
        k.Col = Colors.White;
        k.Cylinder(paint, Vector3.Up * 1.2f, r, r, h - 1.2f, 24, false);
        SpherePart(k, paint, Vector3.Up * h, new Vector3(r, r * 0.45f, r), 24, 6, 0, Mathf.Pi / 2);
        k.Solid = false;
        k.Col = Steel * 1.3f;
        for (var y = 3f; y < h; y += 2.5f) k.Cylinder(metal, Vector3.Up * y, r + 0.03f, r + 0.03f, 0.06f, 24, false);
        // Échelle à crinoline côté +X.
        k.Col = SafetyYellow * 0.8f;
        foreach (var z in new[] { -0.22f, 0.22f }) k.Tube(metal, new Vector3(r + 0.25f, 0.2f, z), new Vector3(r + 0.25f, h + 0.8f, z), 0.025f, 4);
        for (var y = 2.4f; y < h + 0.8f; y += 0.9f)
            for (var i = 0; i < 6; i++)
            {
                var a0 = -1.2f + 2.4f * i / 6f;
                var a1 = -1.2f + 2.4f * (i + 1) / 6f;
                k.Tube(metal, new Vector3(r + 0.6f + Mathf.Cos(a0) * 0.4f, y, Mathf.Sin(a0) * 0.4f), new Vector3(r + 0.6f + Mathf.Cos(a1) * 0.4f, y, Mathf.Sin(a1) * 0.4f), 0.015f, 3);
            }
        k.Col = new Color(0.4f, 0.12f, 0.08f);
        k.Cylinder(metal, new Vector3(0.4f, h + r * 0.4f, 0), 0.12f, 0.1f, 0.9f, 8);
        k.Col = new Color(0.26f, 0.25f, 0.23f);
        k.Tube(metal, new Vector3(-r, 2f, 0), new Vector3(-r - 1.8f, 2f, 0), 0.15f, 8, true);
        k.Tube(metal, new Vector3(-r - 1.8f, 2f, 0), new Vector3(-r - 1.8f, 0f, 0), 0.15f, 8);
        k.Solid = true;
        k.Col = Steel;
        k.Box(metal, new Vector3(0, h + r * 0.42f, 0), new Vector3(1.6f, 0.08f, 1.6f));
    });

    /// <summary>Épave de cuve de procédé : jupe et bas de virole déchirés, calotte arrachée au pied.</summary>
    public static ArrayMesh ProcessVesselWreck() => Cached("indvesselw", k =>
    {
        const float r = VesselR, h = VesselH;
        var ch = Mats.Charred();
        k.Col = BurnSoot;
        k.Cylinder(ch, Vector3.Zero, r * 0.85f, r * 0.85f, 1.2f, 20);
        const int seg = 20;
        for (var i = 0; i < seg; i++)
        {
            var a0 = i * Mathf.Tau / seg;
            var a1 = (i + 1) * Mathf.Tau / seg;
            var t0 = h * (0.25f + 0.45f * H(i, 2, 91));
            var t1 = h * (0.25f + 0.45f * H((i + 1) % seg, 2, 91));
            k.Col = H(i, 2, 92) < 0.5f ? BurnSoot : BurnRust;
            Quad2(k, ch, Polar(r, a0, 1.2f), Polar(r, a1, 1.2f), Polar(r, a1, t1), Polar(r, a0, t0));
        }
        k.Col = BurnSoot * 0.7f;
        k.Cylinder(ch, Vector3.Up * 1.2f, r - 0.05f, r - 0.05f, 0.4f, 20);
        k.Col = BurnRust;
        SpherePart(k, ch, new Vector3(r + 2.5f, 0.7f, 1.5f), new Vector3(r, r * 0.45f, r), 12, 3, 0, Mathf.Pi / 2, new Basis(Vector3.Back, 2.6f));
    });

    // ───────────────────────── Wagons ─────────────────────────

    /// <summary>Châssis de wagon (longerons, traverses, tampons, deux bogies à deux essieux) sur des rails à y = 0.</summary>
    private static void WagonChassis(MeshKit k, Color frame)
    {
        var metal = Mats.DetailMetal();
        const float l = WagonLen;
        k.Col = frame;
        k.Box(metal, new Vector3(0, 1.12f, 0), new Vector3(2.5f, 0.32f, l - 1.2f));
        k.Solid = false;
        foreach (var z in new[] { -l / 2 + 0.4f, l / 2 - 0.4f })
        {
            k.Box(metal, new Vector3(0, 1.05f, z), new Vector3(2.7f, 0.4f, 0.3f));
            foreach (var x in new[] { -0.85f, 0.85f }) k.Cylinder(metal, new Vector3(x, 1.0f, z), 0.18f, 0.18f, 0.5f, 8, true, new Basis(Vector3.Right, z > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2));
        }
        k.Col = new Color(0.1f, 0.095f, 0.09f);
        foreach (var bz in new[] { -l / 2 + 3.2f, l / 2 - 3.2f })
        {
            k.Box(metal, new Vector3(0, 0.75f, bz), new Vector3(2.2f, 0.35f, 2.6f));
            foreach (var az in new[] { -0.9f, 0.9f })
            foreach (var x in new[] { -0.75f, 0.75f })
                k.Cylinder(metal, new Vector3(x - Mathf.Sign(x) * 0.07f, 0.45f, bz + az), 0.45f, 0.45f, 0.14f, 12, true, new Basis(Vector3.Back, Mathf.Pi / 2));
        }
        k.Solid = true;
    }

    /// <summary>Wagon-citerne à hydrocarbures : virole sur châssis, dôme, passerelle latérale, échelles.</summary>
    public static ArrayMesh TankWagon(int v) => Cached($"indtankwagon{v}", k =>
    {
        var paint = Paint(v == 0 ? new Color(0.14f, 0.14f, 0.14f) : v == 1 ? TankWhite : TankGrey);
        var metal = Mats.DetailMetal();
        const float r = 1.42f, cy = 2.75f;
        var along = new Basis(Vector3.Right, Mathf.Pi / 2);
        var body = WagonLen - 3.4f;
        WagonChassis(k, Steel);
        k.Col = Colors.White;
        k.Cylinder(paint, new Vector3(0, cy, -body / 2), r, r, body, 24, false, along);
        SpherePart(k, paint, new Vector3(0, cy, body / 2), new Vector3(r, r * 0.5f, r), 24, 5, 0, Mathf.Pi / 2, along);
        SpherePart(k, paint, new Vector3(0, cy, -body / 2), new Vector3(r, r * 0.5f, r), 24, 5, -Mathf.Pi / 2, 0, along);
        k.Col = Steel;
        k.Cylinder(metal, new Vector3(0, cy + r - 0.1f, 0), 0.5f, 0.5f, 0.45f, 12); // dôme
        k.Solid = false;
        // Ceintures de fixation, passerelle et échelles.
        foreach (var z in new[] { -body * 0.35f, body * 0.35f }) k.Cylinder(metal, new Vector3(0, cy, z), r + 0.04f, r + 0.04f, 0.12f, 24, false, along);
        k.Col = SafetyYellow * 0.7f;
        foreach (var x in new[] { -1.35f, 1.35f })
        {
            k.Box(metal, new Vector3(x, 1.45f, 0), new Vector3(0.3f, 0.04f, 2f));
            k.Tube(metal, new Vector3(x, 0.6f, 0.9f), new Vector3(x * 0.6f, cy + r, 0.9f), 0.025f, 4);
            k.Tube(metal, new Vector3(x, 0.6f, -0.9f), new Vector3(x * 0.6f, cy + r, -0.9f), 0.025f, 4);
        }
        // Panneau orange de danger (code ONU), terni.
        k.Col = new Color(0.55f, 0.28f, 0.08f);
        foreach (var x in new[] { -r - 0.02f, r + 0.02f }) k.Box(metal, new Vector3(x, cy - 0.3f, body * 0.2f), new Vector3(0.02f, 0.45f, 0.6f));
        k.Solid = true;
    });

    /// <summary>Épave de wagon-citerne : châssis noirci, virole fendue sur le dessus, ouverte en corolle, dôme arraché.</summary>
    public static ArrayMesh TankWagonWreck() => Cached("indtankwagonw", k =>
    {
        var ch = Mats.Charred();
        const float r = 1.42f, cy = 2.75f;
        var body = WagonLen - 3.4f;
        WagonChassis(k, BurnSoot);
        // Virole fendue sur le dessus : le bas reste rond, les lèvres de la déchirure se retroussent vers l'extérieur
        // (plus largement au milieu de la citerne) ; fonds bombés restés en place, noircis.
        const int seg = 14;
        var opens = new float[seg + 1];
        for (var i = 0; i <= seg; i++) opens[i] = 0.25f + (0.55f + H(i, 4, 85) * 0.5f) * Mathf.Sin(Mathf.Pi * i / seg);
        // Anneau jj (0 : bas de la virole ; 6 : bord de la fente ; 7 à 9 : lèvre retroussée vers l'extérieur).
        Vector3 Q(int jj, float open, float z, float side)
        {
            if (jj <= 6)
            {
                var t = Mathf.Lerp(Mathf.Pi, open, jj / 6f);
                return new Vector3(side * Mathf.Sin(t) * r, cy + Mathf.Cos(t) * r, z);
            }
            var d = (jj - 6) * 0.42f;
            return new Vector3(side * (Mathf.Sin(open) * r + d * 0.85f), cy + Mathf.Cos(open) * r + d * 0.3f - d * d * 0.35f, z);
        }
        for (var i = 0; i < seg; i++)
        {
            var z0 = -body / 2 + body * i / seg;
            var z1 = -body / 2 + body * (i + 1) / seg;
            foreach (var side in new[] { -1f, 1f })
                for (var j = 0; j < 9; j++)
                {
                    k.Col = H(i, j, 86) < 0.45f ? BurnSoot : j > 5 ? BurnRust * 0.8f : BurnBase;
                    Quad2(k, ch, Q(j, opens[i], z0, side), Q(j + 1, opens[i], z0, side), Q(j + 1, opens[i + 1], z1, side), Q(j, opens[i + 1], z1, side));
                }
        }
        var along = new Basis(Vector3.Right, Mathf.Pi / 2);
        k.Col = BurnSoot;
        SpherePart(k, ch, new Vector3(0, cy, body / 2), new Vector3(r, r * 0.5f, r), 14, 4, 0, Mathf.Pi / 2, along);
        SpherePart(k, ch, new Vector3(0, cy, -body / 2), new Vector3(r, r * 0.5f, r), 14, 4, -Mathf.Pi / 2, 0, along);
    });

    /// <summary>Wagon couvert (portes coulissantes, ranchers, toit cintré) ; variante 2 : porte ouverte.</summary>
    public static ArrayMesh BoxWagon(int v) => CachedKind($"indboxwagon{v}", PropKind.Container, k =>
    {
        var metal = Mats.DetailMetal();
        var paint = v switch { 0 => RustBrown, 1 => new Color(0.3f, 0.31f, 0.3f), _ => new Color(0.22f, 0.26f, 0.29f) };
        WagonChassis(k, Steel);
        const float l = WagonLen - 1.6f, w = WagonW, y0 = 1.28f, hgt = 2.9f;
        k.Col = paint;
        k.Box(metal, new Vector3(0, y0 + hgt / 2, 0), new Vector3(w, hgt, l));
        // Toit cintré.
        k.Col = paint * 0.85f;
        for (var i = 0; i < 6; i++)
        {
            var a0 = Mathf.Pi * i / 6f;
            var a1 = Mathf.Pi * (i + 1) / 6f;
            Vector3 P(float a, float z) => new(Mathf.Cos(a) * w / 2, y0 + hgt + Mathf.Sin(a) * 0.35f, z);
            k.Quad(metal, P(a1, -l / 2), P(a0, -l / 2), P(a0, l / 2), P(a1, l / 2));
        }
        k.Quad(metal, new Vector3(-w / 2, y0 + hgt, l / 2), new Vector3(w / 2, y0 + hgt, l / 2), new Vector3(0, y0 + hgt + 0.35f, l / 2), new Vector3(0, y0 + hgt + 0.35f, l / 2));
        // Ranchers, portes, marquages (relief non solide).
        k.Solid = false;
        k.Col = paint * 0.7f;
        foreach (var x in new[] { -w / 2 - 0.03f, w / 2 + 0.03f })
        {
            for (var z = -l / 2 + 0.6f; z < l / 2; z += 1.25f) k.Box(metal, new Vector3(x, y0 + hgt / 2, z), new Vector3(0.06f, hgt - 0.1f, 0.1f));
            var open = v == 2 && x > 0;
            k.Col = paint * 0.9f;
            k.Box(metal, new Vector3(x + Mathf.Sign(x) * 0.05f, y0 + hgt / 2 - 0.05f, open ? 2.1f : 0), new Vector3(0.05f, hgt - 0.3f, 2.4f));
            k.Col = new Color(0.62f, 0.6f, 0.55f) * 0.6f;
            k.Box(metal, new Vector3(x + Mathf.Sign(x) * 0.02f, y0 + hgt - 0.5f, -l / 2 + 2.2f), new Vector3(0.02f, 0.4f, 2.2f));
            k.Col = paint * 0.7f;
        }
        k.Solid = true;
        if (v == 2)
        {
            // Porte ouverte : intérieur sombre.
            k.Col = new Color(0.04f, 0.04f, 0.04f);
            k.Box(metal, new Vector3(w / 2 + 0.005f, y0 + hgt / 2 - 0.1f, 0), new Vector3(0.01f, hgt - 0.4f, 2.3f));
        }
    });

    /// <summary>Wagon-trémie à minerai : caisse ouverte à pans inclinés, chargement de charbon tassé.</summary>
    public static ArrayMesh HopperWagon() => CachedKind("indhopper", PropKind.Container, k =>
    {
        var metal = Mats.DetailMetal();
        WagonChassis(k, Steel);
        const float l = WagonLen - 1.8f, w = WagonW, y0 = 1.28f, hgt = 2.6f;
        k.Col = new Color(0.24f, 0.22f, 0.2f);
        // Parois (solides) et trémies inclinées sous la caisse.
        k.Box(metal, new Vector3(-w / 2 + 0.05f, y0 + hgt / 2, 0), new Vector3(0.1f, hgt, l));
        k.Box(metal, new Vector3(w / 2 - 0.05f, y0 + hgt / 2, 0), new Vector3(0.1f, hgt, l));
        k.Box(metal, new Vector3(0, y0 + hgt / 2, -l / 2 + 0.05f), new Vector3(w, hgt, 0.1f));
        k.Box(metal, new Vector3(0, y0 + hgt / 2, l / 2 - 0.05f), new Vector3(w, hgt, 0.1f));
        foreach (var z in new[] { -l / 4, l / 4 }) k.Frustum(metal, new Vector3(0, y0 - 0.6f, z), new Vector2(1.2f, 1.6f), new Vector2(w, l / 2 - 0.2f), 0.6f);
        // Chargement (bombé).
        k.Col = new Color(0.07f, 0.068f, 0.065f);
        k.Frustum(Mats.Gravel(), new Vector3(0, y0 + hgt - 0.3f, 0), new Vector2(w - 0.2f, l - 0.2f), new Vector2(w * 0.5f, l * 0.75f), 0.6f);
        k.Solid = false;
        k.Col = new Color(0.17f, 0.16f, 0.15f);
        foreach (var x in new[] { -w / 2 - 0.04f, w / 2 + 0.04f })
            for (var z = -l / 2 + 0.7f; z < l / 2; z += 1.4f) k.Box(metal, new Vector3(x, y0 + hgt / 2, z), new Vector3(0.08f, hgt, 0.12f));
        k.Solid = true;
    });

    // ───────────────────────── Grillage ─────────────────────────

    /// <summary>
    /// Panneau de clôture grillagée de 3 m (le long de X) : poteaux, lisse haute, grillage, potences et trois rangs de
    /// barbelés. Variante 1 : grillage affaissé et déchiré. Famille « petit mobilier » : il cède sous les balles, les
    /// explosions et les gros insectes.
    /// </summary>
    public static ArrayMesh ChainFence(int v) => CachedKind($"indfence{v}", PropKind.Small, k =>
    {
        var metal = Mats.DetailMetal();
        var mesh = Mats.ChainLink();
        const float len = 3f, h = 2.2f;
        k.Col = new Color(0.36f, 0.37f, 0.36f);
        // Grillage (solide : on ne le traverse pas).
        if (v == 0)
            k.Quad(mesh, new Vector3(-len / 2, 0.05f, 0), new Vector3(len / 2, 0.05f, 0), new Vector3(len / 2, h, 0), new Vector3(-len / 2, h, 0),
                new Vector2(0, h), new Vector2(len, h), new Vector2(len, 0), new Vector2(0, 0));
        else
        {
            // Affaissé : le bord haut ploie vers l'avant au milieu, une déchirure en bas.
            var mid = new Vector3(0, h * 0.7f, 0.5f);
            k.Quad(mesh, new Vector3(-len / 2, 0.05f, 0), new Vector3(0, 0.8f, 0.2f), mid, new Vector3(-len / 2, h, 0),
                new Vector2(0, h), new Vector2(len / 2, h - 0.8f), new Vector2(len / 2, h - mid.Y), new Vector2(0, 0));
            k.Quad(mesh, new Vector3(0, 0.8f, 0.2f), new Vector3(len / 2, 0.05f, 0), new Vector3(len / 2, h, 0), mid,
                new Vector2(len / 2, h - 0.8f), new Vector2(len, h), new Vector2(len, 0), new Vector2(len / 2, h - mid.Y));
        }
        k.Col = new Color(0.3f, 0.31f, 0.3f);
        foreach (var x in new[] { -len / 2, len / 2 })
        {
            k.Cylinder(metal, new Vector3(x, 0, 0), 0.04f, 0.04f, h + 0.05f, 6);
            k.Solid = false;
            k.Tube(metal, new Vector3(x, h, 0), new Vector3(x, h + 0.42f, -0.3f), 0.025f, 4);
            k.Solid = true;
        }
        k.Solid = false;
        if (v == 0) k.Tube(metal, new Vector3(-len / 2, h - 0.03f, 0), new Vector3(len / 2, h - 0.03f, 0), 0.025f, 4);
        k.Col = new Color(0.25f, 0.2f, 0.17f);
        for (var i = 0; i < 3; i++)
        {
            var y = h + 0.14f * (i + 1);
            var z = -0.1f * (i + 1);
            k.Tube(metal, new Vector3(-len / 2, y, z), new Vector3(len / 2, y - (v == 1 && i == 2 ? 0.6f : 0f), z), 0.008f, 3);
        }
        k.Solid = true;
    });

    /// <summary>Conteneur-citerne de 1 m³ (GRV) : bidon blanchâtre encrassé dans sa cage, sur palette.</summary>
    public static ArrayMesh Ibc() => CachedKind("indibc", PropKind.Small, k =>
    {
        var metal = Mats.DetailMetal();
        var d = Mats.Detail();
        k.Col = new Color(0.38f, 0.3f, 0.2f);
        k.Box(d, new Vector3(0, 0.07f, 0), new Vector3(1.2f, 0.14f, 1f));
        k.Col = new Color(0.62f, 0.6f, 0.53f);
        k.Box(d, new Vector3(0, 0.62f, 0), new Vector3(1.1f, 0.95f, 0.92f));
        k.Solid = false;
        k.Col = new Color(0.3f, 0.3f, 0.29f);
        foreach (var y in new[] { 0.35f, 0.7f, 1.1f })
        {
            k.Box(metal, new Vector3(0, y, 0.48f), new Vector3(1.16f, 0.03f, 0.03f));
            k.Box(metal, new Vector3(0, y, -0.48f), new Vector3(1.16f, 0.03f, 0.03f));
            k.Box(metal, new Vector3(0.57f, y, 0), new Vector3(0.03f, 0.03f, 0.98f));
            k.Box(metal, new Vector3(-0.57f, y, 0), new Vector3(0.03f, 0.03f, 0.98f));
        }
        k.Solid = true;
    });

    // ───────────────────────── Encombrement des cours ─────────────────────────

    /// <summary>
    /// Mât d'éclairage de 22 m (fût tronconique galvanisé, couronne de quatre projecteurs, échelle) ; famille
    /// « poteau » : il bascule d'un bloc quand il cède. Variante 1 : projecteurs allumés (nuit, secours).
    /// </summary>
    public static ArrayMesh FloodMast(int v) => CachedKind($"indmast{v}", PropKind.Pole, k =>
    {
        var metal = Mats.DetailMetal();
        const float h = 22f;
        k.Col = new Color(0.4f, 0.41f, 0.4f);
        k.Cylinder(metal, Vector3.Zero, 0.32f, 0.14f, h, 10);
        k.Col = new Color(0.3f, 0.3f, 0.29f);
        k.Box(Mats.Detail(), new Vector3(0, 0.2f, 0), new Vector3(1f, 0.4f, 1f));
        k.Solid = false;
        k.Box(metal, new Vector3(0, h, 0), new Vector3(2.6f, 0.12f, 0.12f));
        k.Box(metal, new Vector3(0, h, 0), new Vector3(0.12f, 0.12f, 2.6f));
        for (var i = 0; i < 4; i++)
        {
            var a = i * Mathf.Pi / 2 + Mathf.Pi / 4;
            var p = new Vector3(Mathf.Cos(a) * 0.95f, h + 0.25f, Mathf.Sin(a) * 0.95f);
            k.Col = new Color(0.2f, 0.2f, 0.19f);
            k.Box(metal, p, new Vector3(0.6f, 0.5f, 0.25f), new Basis(Vector3.Up, -a + Mathf.Pi / 2) * new Basis(Vector3.Right, 0.5f));
            k.Col = v == 1 ? new Color(1f, 0.95f, 0.85f) : new Color(0.12f, 0.12f, 0.12f);
            k.Box(v == 1 ? Mats.Lamps() : metal, p + new Vector3(Mathf.Cos(a) * 0.14f, -0.1f, Mathf.Sin(a) * 0.14f), new Vector3(0.5f, 0.4f, 0.04f), new Basis(Vector3.Up, -a + Mathf.Pi / 2) * new Basis(Vector3.Right, 0.5f));
        }
        k.Col = new Color(0.3f, 0.3f, 0.29f);
        foreach (var z in new[] { -0.2f, 0.2f }) k.Tube(metal, new Vector3(0.38f, 2.5f, z), new Vector3(0.2f, h - 0.3f, z), 0.02f, 3);
        k.Solid = true;
    });

    /// <summary>Pile de palettes (bois grisé), parfois chargée de sacs ou d'une caisse bâchée.</summary>
    public static ArrayMesh PalletStack(int v) => CachedKind($"indpallets{v}", PropKind.Small, k =>
    {
        var d = Mats.Detail();
        var wood = new Color(0.4f, 0.34f, 0.26f);
        var n = 3 + v * 2;
        for (var i = 0; i < n; i++)
        {
            var y = i * 0.15f;
            k.Col = wood * (0.8f + 0.2f * H(i, v, 91));
            k.Box(d, new Vector3(0.03f * (i % 2), y + 0.075f, 0), new Vector3(1.2f, 0.15f, 1f), new Basis(Vector3.Up, (H(i, v, 92) - 0.5f) * 0.12f));
        }
        if (v == 1)
        {
            k.Col = new Color(0.5f, 0.48f, 0.43f);
            k.Box(d, new Vector3(0, n * 0.15f + 0.45f, 0), new Vector3(1.1f, 0.9f, 0.95f));
        }
    });

    /// <summary>Benne à gravats (caisson trapézoïdal d'acier, chargé de ferraille et de gravats).</summary>
    public static ArrayMesh Skip() => CachedKind("indskip", PropKind.Furniture, k =>
    {
        var metal = Mats.DetailMetal();
        k.Col = new Color(0.36f, 0.28f, 0.12f);
        // Parois (épaisseur 6 cm) et fond.
        k.Frustum(metal, new Vector3(0, 0, 0), new Vector2(3.2f, 1.6f), new Vector2(4.2f, 1.8f), 0.1f);
        foreach (var sz in new[] { -1f, 1f })
            k.Box(metal, new Vector3(0, 0.75f, sz * 0.85f), new Vector3(3.9f, 1.5f, 0.06f), new Basis(Vector3.Right, sz * 0.1f));
        foreach (var sx in new[] { -1f, 1f })
            k.Box(metal, new Vector3(sx * 1.85f, 0.75f, 0), new Vector3(0.06f, 1.5f, 1.8f), new Basis(Vector3.Back, -sx * 0.32f));
        // Contenu (gravats et ferraille).
        k.Col = new Color(0.3f, 0.29f, 0.27f);
        k.Frustum(Mats.Detail(), new Vector3(0, 0.1f, 0), new Vector2(3.4f, 1.6f), new Vector2(2.2f, 1f), 1.2f);
        k.Solid = false;
        k.Col = new Color(0.22f, 0.13f, 0.08f);
        k.Tube(metal, new Vector3(-1.2f, 1.1f, 0.3f), new Vector3(1.6f, 1.9f, -0.2f), 0.05f, 4);
        k.Tube(metal, new Vector3(-0.4f, 1.2f, -0.5f), new Vector3(0.9f, 1.5f, 0.7f), 0.08f, 4);
        k.Solid = true;
    });

    /// <summary>Semi-remorque fourgon (13,6 m) sur béquilles, sans tracteur : bâche ou caisse rigide sale.</summary>
    public static ArrayMesh BoxTrailer(int v) => CachedKind($"indtrailer{v}", PropKind.Container, k =>
    {
        var metal = Mats.DetailMetal();
        var body = v == 0 ? new Color(0.55f, 0.55f, 0.52f) : new Color(0.24f, 0.28f, 0.3f);
        const float l = 13.6f, w = 2.5f;
        k.Col = Steel;
        k.Box(metal, new Vector3(0, 1.1f, 0), new Vector3(1.2f, 0.3f, l - 0.4f));
        k.Col = body;
        k.Box(v == 0 ? Mats.Detail() : metal, new Vector3(0, 2.6f, 0), new Vector3(w, 2.7f, l));
        k.Col = body * 0.8f;
        k.Box(metal, new Vector3(0, 1.25f, 0), new Vector3(w, 0.1f, l));
        k.Solid = false;
        k.Col = new Color(0.06f, 0.06f, 0.06f);
        foreach (var z in new[] { -l / 2 + 1.2f, -l / 2 + 2.5f, -l / 2 + 3.8f })
            foreach (var x in new[] { -1f, 1f })
                k.Cylinder(metal, new Vector3(x - (x > 0 ? 0 : 0.32f), 0.5f, z), 0.5f, 0.5f, 0.32f, 12, true, new Basis(Vector3.Back, Mathf.Pi / 2));
        k.Col = Steel;
        foreach (var x in new[] { -0.8f, 0.8f }) k.Box(metal, new Vector3(x, 0.5f, l / 2 - 3.2f), new Vector3(0.12f, 1f, 0.12f));
        k.Col = body * 0.6f;
        for (var z = -l / 2 + 0.6f; z < l / 2; z += 1.2f)
            foreach (var x in new[] { -w / 2 - 0.02f, w / 2 + 0.02f })
                k.Box(metal, new Vector3(x, 2.6f, z), new Vector3(0.03f, 2.6f, 0.08f));
        k.Solid = true;
    });

    /// <summary>Touret de câble en bois (couché ou debout).</summary>
    public static ArrayMesh CableReel() => CachedKind("indreel", PropKind.Small, k =>
    {
        var d = Mats.Detail();
        var rot = new Basis(Vector3.Back, Mathf.Pi / 2);
        k.Col = new Color(0.36f, 0.3f, 0.22f);
        foreach (var x in new[] { -0.55f, 0.45f }) k.Cylinder(d, new Vector3(x, 0.85f, 0), 0.85f, 0.85f, 0.1f, 14, true, rot);
        k.Col = new Color(0.08f, 0.08f, 0.08f);
        k.Cylinder(d, new Vector3(-0.45f, 0.85f, 0), 0.55f, 0.55f, 0.9f, 12, true, rot);
    });

    /// <summary>Transformateur de poste électrique : cuve, radiateurs à ailettes, traversées isolantes.</summary>
    public static ArrayMesh Transformer() => CachedKind("indtransfo", PropKind.Barrier, k =>
    {
        var metal = Mats.DetailMetal();
        k.Col = new Color(0.32f, 0.34f, 0.31f);
        k.Box(metal, new Vector3(0, 1.4f, 0), new Vector3(3f, 2.4f, 2f));
        k.Box(Mats.Detail(), new Vector3(0, 0.1f, 0), new Vector3(3.6f, 0.2f, 2.6f));
        k.Solid = false;
        k.Col = new Color(0.28f, 0.3f, 0.27f);
        for (var i = 0; i < 9; i++)
            foreach (var sz in new[] { -1f, 1f })
                k.Box(metal, new Vector3(-1.3f + i * 0.32f, 1.4f, sz * 1.25f), new Vector3(0.05f, 1.9f, 0.5f));
        k.Col = new Color(0.45f, 0.3f, 0.2f);
        for (var i = 0; i < 3; i++)
            k.Cylinder(metal, new Vector3(-0.9f + i * 0.9f, 2.6f, 0), 0.12f, 0.08f, 1.1f, 8);
        k.Solid = true;
    });
}
