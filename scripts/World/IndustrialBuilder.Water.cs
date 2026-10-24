using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Sud de la zone : canal à quais de béton (eau brune et huileuse, cales de mise à l'eau pour en ressortir, pont
/// routier et pont de service), station d'épuration (deux décanteurs circulaires à pont racleur, deux lagunes de
/// boues, station de pompage) et quai de vrac (tas de charbon, convoyeur incliné praticable jusqu'à une tour de
/// transfert et une galerie au-dessus du quai, grue à flèche, cuves).
/// </summary>
public static partial class IndustrialBuilder
{
    /// <summary>Ponts : centre x et demi-largeur (multiples de la maille du champ de flux).</summary>
    private static readonly (float x, float half)[] Bridges = { (0f, 8f), (150f, 6f) };

    private static void BuildCanal(Ctx c)
    {
        var concrete = Mats.Concrete(new Color(0.4f, 0.39f, 0.37f), 61);
        // Fond (solide), eau (visuelle).
        CityBuilder.AddSlab(c.Root, c.Body, new Vector3(0, CanalBed - 0.5f, (CanalZ0 + CanalZ1) / 2), new Vector3(524, 1f, CanalZ1 - CanalZ0), Mats.Concrete(new Color(0.2f, 0.19f, 0.17f), 62));
        var water = Mats.CanalWater(c.Time);
        for (var x = -262f; x < 262f; x += 48f)
        {
            var k = c.Ground.At(new Vector3(x + 24f, 0, CanalZ0));
            var foam = new Color(0.8f, 0, 0);
            var clear = new Color(0f, 0, 0);
            var x1 = x + 48f;
            // Bandes d'écume le long des quais (couleur de sommet R).
            void Strip(float z0, float z1, Color c0, Color c1) =>
                k.QuadC(water, new Vector3(x, WaterY, z0), new Vector3(x1, WaterY, z0), new Vector3(x1, WaterY, z1), new Vector3(x, WaterY, z1),
                    Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, c0, c0, c1, c1, Vector3.Up);
            Strip(CanalZ0, CanalZ0 + 1.5f, foam, clear);
            Strip(CanalZ0 + 1.5f, CanalZ1 - 1.5f, clear, clear);
            Strip(CanalZ1 - 1.5f, CanalZ1, clear, foam);
        }
        // Murs de quai (solides) et margelles basses (12 cm : on les enjambe).
        for (var x = -262f; x < 262f; x += 48f)
        {
            var x1 = x + 48f;
            var k = c.Solid.At(new Vector3(x + 24f, 0, CanalZ0));
            k.Col = Colors.White;
            k.OrientedQuad(concrete, new Vector3(x, CanalBed, CanalZ0), new Vector3(x1, CanalBed, CanalZ0), new Vector3(x1, 0, CanalZ0), new Vector3(x, 0, CanalZ0), Vector3.Back);
            k.OrientedQuad(concrete, new Vector3(x, CanalBed, CanalZ1), new Vector3(x1, CanalBed, CanalZ1), new Vector3(x1, 0, CanalZ1), new Vector3(x, 0, CanalZ1), Vector3.Forward);
            k.Col = new Color(0.55f, 0.54f, 0.5f);
            k.BoxMinMax(concrete, new Vector3(x, 0, CanalZ0 - 0.5f), new Vector3(x1, 0.12f, CanalZ0));
            k.BoxMinMax(concrete, new Vector3(x, 0, CanalZ1), new Vector3(x1, 0.12f, CanalZ1 + 0.5f));
            // Coulures et laisses de crue sur les murs (relief non solide) : bande sombre au-dessus de l'eau.
            k.Solid = false;
            k.Col = new Color(0.25f, 0.24f, 0.2f);
            k.OrientedQuad(concrete, new Vector3(x, WaterY, CanalZ0 + 0.02f), new Vector3(x1, WaterY, CanalZ0 + 0.02f), new Vector3(x1, WaterY + 0.7f, CanalZ0 + 0.02f), new Vector3(x, WaterY + 0.7f, CanalZ0 + 0.02f), Vector3.Back);
            k.OrientedQuad(concrete, new Vector3(x, WaterY, CanalZ1 - 0.02f), new Vector3(x1, WaterY, CanalZ1 - 0.02f), new Vector3(x1, WaterY + 0.7f, CanalZ1 - 0.02f), new Vector3(x, WaterY + 0.7f, CanalZ1 - 0.02f), Vector3.Forward);
            k.Solid = true;
        }
        // Cales de mise à l'eau (rampes le long des quais) pour ressortir du canal.
        foreach (var (x, north) in new[] { (-160f, true), (70f, true), (-70f, false), (196f, false) }) Slipway(c, x, north);
        foreach (var (bx, half) in Bridges) Bridge(c, bx, half);
        // Le canal bloque le champ de flux sauf aux ponts : les insectes passent par les ponts (ou nagent en grimpant).
        var x0 = -262f;
        foreach (var (bx, half) in Bridges)
        {
            FlowBlock(c, new Vector3((x0 + bx - half) / 2, 0, (CanalZ0 + CanalZ1) / 2), new Vector3(bx - half - x0, 0f, CanalZ1 - CanalZ0 - 1f), 0.1f);
            x0 = bx + half;
        }
        FlowBlock(c, new Vector3((x0 + 262f) / 2, 0, (CanalZ0 + CanalZ1) / 2), new Vector3(262f - x0, 0f, CanalZ1 - CanalZ0 - 1f), 0.1f);
        Take(c, new Rect2(-262, CanalZ0 - 1f, 524, CanalZ1 - CanalZ0 + 2f), 0f);
    }

    /// <summary>Cale : rampe de béton (3,5 m de large) qui descend le long du mur de quai jusqu'au fond du canal.</summary>
    private static void Slipway(Ctx c, float x, bool north)
    {
        var k = c.Solid.At(new Vector3(x, 0, north ? CanalZ0 : CanalZ1));
        var concrete = Mats.Concrete(new Color(0.4f, 0.39f, 0.37f), 61);
        var zw = north ? CanalZ0 : CanalZ1;
        var zi = north ? CanalZ0 + 3.5f : CanalZ1 - 3.5f;
        const float len = 9f;
        k.Col = new Color(0.5f, 0.49f, 0.46f);
        // Dessus incliné (haut côté x, bas côté x + len), face latérale vers le canal, bout bas.
        var hiW = new Vector3(x, 0.13f, zw);
        var hiI = new Vector3(x, 0.13f, zi);
        var loW = new Vector3(x + len, CanalBed, zw);
        var loI = new Vector3(x + len, CanalBed, zi);
        k.OrientedQuad(concrete, hiW, hiI, loI, loW, Vector3.Up);
        k.Tri(concrete, hiI, loI, new Vector3(x, CanalBed, zi), new Vector3(x, CanalBed, zw));
        k.OrientedQuad(concrete, hiW, hiI, new Vector3(x, CanalBed, zi), new Vector3(x, CanalBed, zw), Vector3.Left);
        // Palier en haut de la cale, au ras de la margelle : on remonte sur le quai sans marche.
        k.BoxMinMax(concrete, new Vector3(x - 1.8f, CanalBed, north ? CanalZ0 - 0.6f : zi), new Vector3(x, 0.13f, north ? zi : CanalZ1 + 0.6f));
        // Bollards et échelle de quai (visuels).
        k.Solid = false;
        k.Col = new Color(0.15f, 0.14f, 0.13f);
        k.Cylinder(Mats.DetailMetal(), new Vector3(x - 2f, 0.12f, north ? CanalZ0 - 0.6f : CanalZ1 + 0.6f), 0.18f, 0.22f, 0.5f, 8);
        k.Solid = true;
        c.Ruins.Decal(Mats.Decal.Oil, new Vector3(x - 3f, 0, north ? CanalZ0 - 3f : CanalZ1 + 3f), 4f, x, 0.03f);
        // Débouché de la cale dégagé (ni clôture ni mobilier).
        Take(c, new Rect2(x - 5f, north ? CanalZ0 - 6f : CanalZ1, 16f, 6f), 0f);
    }

    /// <summary>Pont : tablier de béton au niveau des quais, parapets, corniches tachées, poutres sous le tablier.</summary>
    private static void Bridge(Ctx c, float x, float half)
    {
        var k = c.Solid.At(new Vector3(x, 0, (CanalZ0 + CanalZ1) / 2));
        var concrete = Mats.Concrete(new Color(0.46f, 0.45f, 0.42f), 63);
        var z0 = CanalZ0 - 1f;
        var z1 = CanalZ1 + 1f;
        k.Col = Colors.White;
        k.BoxMinMax(concrete, new Vector3(x - half, -0.9f, z0), new Vector3(x + half, 0.02f, z1));
        // Parapets (1,1 m) : murets pleins.
        k.Col = new Color(0.52f, 0.51f, 0.48f);
        foreach (var sx in new[] { -1f, 1f })
            k.BoxMinMax(concrete, new Vector3(x + sx * half - (sx > 0 ? 0.45f : 0f), 0, z0), new Vector3(x + sx * half + (sx < 0 ? 0.45f : 0f), 1.1f, z1));
        // Poutres sous le tablier et culées.
        k.Col = new Color(0.32f, 0.31f, 0.29f);
        for (var bx = x - half + 1.5f; bx < x + half - 1f; bx += 3f)
            k.BoxMinMax(concrete, new Vector3(bx - 0.4f, -1.7f, z0), new Vector3(bx + 0.4f, -0.9f, z1));
        if (half > 6f)
        {
            // Pont routier : enrobé et bandes de rive sur le tablier.
            var g = c.Ground.At(new Vector3(x, 0, (CanalZ0 + CanalZ1) / 2));
            g.Col = Colors.White;
            g.OrientedQuad(Mats.Asphalt(), new Vector3(x - half + 0.5f, 0.035f, z0), new Vector3(x + half - 0.5f, 0.035f, z0), new Vector3(x + half - 0.5f, 0.035f, z1), new Vector3(x - half + 0.5f, 0.035f, z1), Vector3.Up);
        }
        Take(c, new Rect2(x - half - 1f, z0 - 2f, half * 2 + 2f, z1 - z0 + 4f), 0f);
    }

    // ───────────────────────── Station d'épuration ─────────────────────────

    private static void BuildPonds(Ctx c)
    {
        Clarifier(c, new Vector3(-196, 0, 98), 13.5f, 0);
        Clarifier(c, new Vector3(-160, 0, 98), 13.5f, 1);
        Lagoon(c, new Rect2(-132, 76, 40, 44));
        Lagoon(c, new Rect2(-84, 78, 28, 26));
    }

    /// <summary>Décanteur circulaire : voile de béton d'un mètre, boues, pilier central, pont racleur (passerelle praticable).</summary>
    private static void Clarifier(Ctx c, Vector3 p, float r, int v)
    {
        var k = c.Solid.At(p);
        var concrete = Mats.Concrete(new Color(0.46f, 0.45f, 0.42f), 64);
        var metal = Mats.DetailMetal();
        const int seg = 40;
        const float wall = 1.05f, t = 0.35f;
        k.Col = Colors.White;
        for (var i = 0; i < seg; i++)
        {
            var a0 = i * Mathf.Tau / seg;
            var a1 = (i + 1) * Mathf.Tau / seg;
            Vector3 P(float rr, float a, float y) => p + new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
            var radial = new Vector3(Mathf.Cos((a0 + a1) / 2), 0, Mathf.Sin((a0 + a1) / 2));
            k.OrientedQuad(concrete, P(r, a1, 0), P(r, a0, 0), P(r, a0, wall), P(r, a1, wall), radial);
            k.OrientedQuad(concrete, P(r - t, a0, 0.3f), P(r - t, a1, 0.3f), P(r - t, a1, wall), P(r - t, a0, wall), -radial);
            k.OrientedQuad(concrete, P(r - t, a0, wall), P(r - t, a1, wall), P(r, a1, wall), P(r, a0, wall), Vector3.Up);
            // Fond de boues (solide à 0,3 m), surface visuelle juste au-dessus.
            k.Tri(Mats.Sludge(), p + Vector3.Up * 0.3f, P(r - t, a0, 0.3f), P(r - t, a1, 0.3f), p + Vector3.Down);
            k.Solid = false;
            k.Tri(Mats.Sludge(), p + Vector3.Up * 0.48f, P(r - t, a0, 0.48f), P(r - t, a1, 0.48f), p + Vector3.Down);
            k.Solid = true;
        }
        // Pilier central et pont racleur (poutre treillis, caillebotis), arrêté en biais.
        k.Col = new Color(0.4f, 0.39f, 0.37f);
        k.Cylinder(concrete, p, 1.4f, 1.4f, 2.4f, 16);
        var a = 0.6f + v * 2.1f;
        var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
        var side = new Vector3(-dir.Z, 0, dir.X);
        var y = 2.4f;
        k.Col = new Color(0.3f, 0.28f, 0.22f);
        var e0 = p + Vector3.Up * y;
        var e1 = p + dir * (r + 0.4f) + Vector3.Up * y;
        k.Quad(Mats.Grating(), e0 - side * 0.7f, e1 - side * 0.7f, e1 + side * 0.7f, e0 + side * 0.7f, new Vector2(0, 0), new Vector2(r, 0), new Vector2(r, 1.4f), new Vector2(0, 1.4f));
        k.Quad(Mats.Grating(), e0 + side * 0.7f, e1 + side * 0.7f, e1 - side * 0.7f, e0 - side * 0.7f, new Vector2(0, 1.4f), new Vector2(r, 1.4f), new Vector2(r, 0), new Vector2(0, 0));
        foreach (var sg in new[] { -1f, 1f })
            k.Tube(metal, e0 + side * 0.75f * sg - Vector3.Up * 0.3f, e1 + side * 0.75f * sg - Vector3.Up * 0.3f, 0.12f, 4);
        // Chariot d'entraînement sur le voile, racleur immergé (non solide), garde-corps.
        k.Box(metal, p + dir * (r - 0.2f) + Vector3.Up * (wall + 0.7f), new Vector3(1.6f, 1.4f, 1.6f));
        k.Solid = false;
        k.Tube(metal, p + dir * 2f + Vector3.Up * y, p + dir * 2f + Vector3.Up * 0.3f, 0.06f, 4);
        k.Tube(metal, p + dir * (r - 2f) + Vector3.Up * y, p + dir * (r - 2f) + Vector3.Up * 0.3f, 0.06f, 4);
        k.Solid = true;
        RailLine(k, e0 + side * 0.68f, e1 + side * 0.68f);
        RailLine(k, e0 - side * 0.68f, e1 - side * 0.68f);
        // Goulotte de sortie et conduite vers la station de pompage.
        k.Col = new Color(0.36f, 0.35f, 0.33f);
        k.Box(concrete, p + new Vector3(0, 0.5f, r + 1.2f), new Vector3(1.6f, 1f, 2.4f));
        FlowBlock(c, p, new Vector3(r * 2, wall, r * 2), 0f);
        Take(c, p, r + 1f, r + 2.6f);
    }

    /// <summary>Lagune de boues : murets de béton d'un mètre, boues croûtées (fond solide à 0,3 m), escalier d'accès.</summary>
    private static void Lagoon(Ctx c, Rect2 r)
    {
        var center = new Vector3(r.GetCenter().X, 0, r.GetCenter().Y);
        var k = c.Solid.At(center);
        var concrete = Mats.Concrete(new Color(0.44f, 0.43f, 0.4f), 65);
        const float wall = 1f, t = 0.4f;
        k.Col = Colors.White;
        k.Xf = new Transform3D(Basis.Identity, center);
        k.Ring(concrete, r.Size.X / 2, r.Size.Y / 2, 0, wall, t, 0f);
        k.Xf = Transform3D.Identity;
        var x0 = r.Position.X + t;
        var x1 = r.End.X - t;
        var z0 = r.Position.Y + t;
        var z1 = r.End.Y - t;
        k.Col = Colors.White;
        k.Quad(Mats.Sludge(), new Vector3(x0, 0.3f, z1), new Vector3(x1, 0.3f, z1), new Vector3(x1, 0.3f, z0), new Vector3(x0, 0.3f, z0));
        k.Solid = false;
        k.Quad(Mats.Sludge(), new Vector3(x0, 0.5f, z1), new Vector3(x1, 0.5f, z1), new Vector3(x1, 0.5f, z0), new Vector3(x0, 0.5f, z0));
        k.Solid = true;
        // Marches d'accès (blocs) contre le muret nord, de chaque côté.
        k.Col = new Color(0.4f, 0.39f, 0.37f);
        var sx = r.GetCenter().X;
        k.Box(concrete, new Vector3(sx, 0.25f, r.Position.Y - 0.8f), new Vector3(2.4f, 0.5f, 1.6f));
        k.Box(concrete, new Vector3(sx, 0.55f, z0 + 0.8f), new Vector3(2.4f, 0.5f, 1.6f));
        // Carcasses et fûts dans la boue (épaves posées par l'habillage autour).
        FlowBlock(c, center, new Vector3(r.Size.X, wall, r.Size.Y), 0f);
        Take(c, r, 1f);
    }

    // ───────────────────────── Quai de vrac ─────────────────────────

    private static void BuildWharf(Ctx c)
    {
        // Tas de charbon et de minerai (solides, praticables).
        CoalPile(c, new Vector3(52, 0, 98), new Vector3(24, 7f, 18), 1);
        CoalPile(c, new Vector3(88, 0, 101), new Vector3(20, 8.5f, 20), 2);
        // Convoyeur incliné jusqu'à la tour de transfert, puis galerie jusqu'au-dessus du quai.
        Conveyor(c);
        JibCrane(c, new Vector3(182, 0, 120));
        // Cuves de produits (explosives) près du quai.
        foreach (var (x, z) in new[] { (126f, 96f), (126f, 106f) })
        {
            var p = new Vector3(x, 0, z);
            var spec = new Destructibles.TankSpec { Wreck = Props.ProcessVesselWreck(), Blast = 12f, Damage = 260f, Core = 5f, FireScale = 1.6f, FireY = 1.7f, Paint = new Color(0.42f, 0.43f, 0.42f) };
            Destructibles.AddTank(c.Root, c.Body, Props.ProcessVessel(1), new Transform3D(new Basis(Vector3.Up, z * 0.2f), p), 300f, spec);
            FlowBlock(c, p, new Vector3(5f, Props.VesselH, 5f));
            Take(c, p, 3.4f, 3.4f);
            c.Tanks.Add(p);
        }
    }

    /// <summary>Tas de vrac : butte convexe (plusieurs couronnes irrégulières), surface de charbon tassé.</summary>
    private static void CoalPile(Ctx c, Vector3 p, Vector3 s, int seed)
    {
        var k = c.Ruins.Kit(RuinKit.Layer.Solid, p);
        var m = Mats.Gravel();
        const int seg = 26;
        // Talus d'éboulement (35°) puis crête aplatie et bosselée par les déversements successifs.
        var rings = new[] { (0f, 1f), (0.18f, 0.9f), (0.42f, 0.74f), (0.68f, 0.52f), (0.88f, 0.3f), (1f, 0.08f) };
        var pts = new List<Vector3>();
        Vector3 At(int i, int j)
        {
            var a = i * Mathf.Tau / seg;
            var (hy, rr) = rings[j];
            var lobe = 1f + 0.16f * Mathf.Sin(a * 3f + seed) + 0.08f * Mathf.Sin(a * 7f + seed * 2f);
            var jit = (0.86f + 0.28f * RuinKit.Hash(i, j * 7 + seed, 51)) * lobe;
            var hj = j == 0 ? 0f : (RuinKit.Hash(i, j * 5 + seed, 52) - 0.5f) * 0.12f * s.Y;
            return p + new Vector3(Mathf.Cos(a) * s.X / 2 * rr * jit, s.Y * hy * (0.85f + 0.15f * lobe) + hj, Mathf.Sin(a) * s.Z / 2 * rr * jit);
        }
        for (var j = 0; j + 1 < rings.Length; j++)
            for (var i = 0; i < seg; i++)
            {
                var col0 = new Color(0.08f, 0.078f, 0.075f) * (1f + j * 0.15f);
                k.Col = col0;
                var a0 = At(i, j); var a1 = At((i + 1) % seg, j); var b1 = At((i + 1) % seg, j + 1); var b0 = At(i, j + 1);
                k.TriSmooth(m, a0, a1, b1, PileNormal(a0 - p, s), PileNormal(a1 - p, s), PileNormal(b1 - p, s), col0, col0, col0);
                k.TriSmooth(m, a0, b1, b0, PileNormal(a0 - p, s), PileNormal(b1 - p, s), PileNormal(b0 - p, s), col0, col0, col0);
                pts.Add(a0);
            }
        pts.Add(p + Vector3.Up * s.Y);
        c.Ruins.Convex(pts.ToArray());
        FlowBlock(c, p, new Vector3(s.X * 0.7f, s.Y, s.Z * 0.7f), 0f);
        Take(c, p, s.X / 2 + 1f, s.Z / 2 + 1f);
    }

    /// <summary>Normale lissée d'un tas (demi-ellipsoïde de demi-axes s.X/2, s.Y, s.Z/2) au point relatif <paramref name="d"/>.</summary>
    private static Vector3 PileNormal(Vector3 d, Vector3 s) =>
        new Vector3(d.X / (s.X * s.X * 0.25f), d.Y / (s.Y * s.Y), d.Z / (s.Z * s.Z * 0.25f)).Normalized();

    /// <summary>
    /// Convoyeur à bande : treillis incliné (18°) avec passerelle de service praticable, tour de transfert en charpente,
    /// galerie horizontale à 13,5 m jusqu'à une trémie au-dessus du quai. Collision exacte.
    /// </summary>
    private static void Conveyor(Ctx c)
    {
        var metal = Mats.DetailMetal();
        var grating = Mats.Grating();
        var a = new Vector3(68, 0.02f, 80);
        var b = new Vector3(110.4f, 13.55f, 80); // arrive sur le plancher de la tour (qui commence à x = 110,2)
        var tower = new Vector3(112, 0, 80);
        var end = new Vector3(112, 13.5f, 122);
        void Span(Vector3 p0, Vector3 p1, float hw, bool belt)
        {
            var dir = (p1 - p0).Normalized();
            var flat = new Vector3(dir.X, 0, dir.Z).Normalized();
            var side = new Vector3(-flat.Z, 0, flat.X);
            var len = (p1 - p0).Length();
            var k = c.Solid.At((p0 + p1) / 2);
            // Passerelle (caillebotis solide), deux côtés de treillis (membrures solides, diagonales en relief).
            k.Col = Colors.White;
            k.Quad(grating, p0 - side * hw, p1 - side * hw, p1 - side * 0.1f, p0 - side * 0.1f, Vector2.Zero, new Vector2(len, 0), new Vector2(len, hw), new Vector2(0, hw));
            k.Quad(grating, p0 - side * 0.1f, p1 - side * 0.1f, p1 - side * hw, p0 - side * hw, Vector2.Zero, new Vector2(len, 0), new Vector2(len, hw), new Vector2(0, hw));
            k.Col = new Color(0.32f, 0.26f, 0.14f);
            foreach (var sg in new[] { -1f, 1f })
            {
                k.Tube(metal, p0 + side * hw * sg, p1 + side * hw * sg, 0.1f, 4);
                k.Tube(metal, p0 + side * hw * sg + Vector3.Up * 2.3f, p1 + side * hw * sg + Vector3.Up * 2.3f, 0.08f, 4);
                k.Solid = false;
                var n = Mathf.CeilToInt(len / 2.5f);
                for (var i = 0; i < n; i++)
                {
                    var q0 = p0.Lerp(p1, i / (float)n) + side * hw * sg;
                    var q1 = p0.Lerp(p1, (i + 1) / (float)n) + side * hw * sg;
                    k.Tube(metal, q0, q1 + Vector3.Up * 2.3f, 0.04f, 3);
                    k.Tube(metal, q0, q0 + Vector3.Up * 2.3f, 0.04f, 3);
                }
                k.Solid = true;
            }
            if (belt)
            {
                // Bande (non solide) avec un reste de charbon, rouleaux.
                k.Solid = false;
                k.Col = new Color(0.05f, 0.05f, 0.05f);
                k.Quad(metal, p0 + side * 0.1f + Vector3.Up * 0.5f, p1 + side * 0.1f + Vector3.Up * 0.5f, p1 + side * (hw - 0.15f) + Vector3.Up * 0.5f, p0 + side * (hw - 0.15f) + Vector3.Up * 0.5f);
                k.Solid = true;
            }
            // Toiture de tôle (abri de bande) partielle, arrachée par endroits.
            k.Col = new Color(0.4f, 0.4f, 0.38f);
            var segs = Mathf.CeilToInt(len / 4f);
            for (var i = 0; i < segs; i++)
            {
                if (RuinKit.Hash(i, p0.X, 53) < 0.35f) continue;
                var q0 = p0.Lerp(p1, i / (float)segs) + Vector3.Up * 2.35f;
                var q1 = p0.Lerp(p1, (i + 1) / (float)segs) + Vector3.Up * 2.35f;
                k.OrientedQuad(metal, q0 - side * hw, q1 - side * hw, q1 + side * hw, q0 + side * hw, Vector3.Up);
                k.OrientedQuad(metal, q0 - side * hw, q1 - side * hw, q1 + side * hw, q0 + side * hw, Vector3.Down);
            }
            // Palées tous les 10 m.
            for (var d = 8f; d < len - 2f; d += 10f)
            {
                var q = p0 + dir * d;
                if (q.Y < 2f) continue;
                k.Col = new Color(0.28f, 0.24f, 0.16f);
                foreach (var sg in new[] { -1f, 1f })
                    k.Box(metal, new Vector3(q.X, q.Y / 2, q.Z) + side * hw * sg, new Vector3(0.3f, q.Y, 0.3f));
                k.Box(metal, q - Vector3.Up * 0.2f, side * hw * 2.2f + flat * 0.3f + Vector3.Up * 0.3f);
                Take(c, q, 1.8f, 1.8f, 0f);
            }
        }
        Span(a, b, 1.3f, true);
        Span(new Vector3(tower.X, 13.5f, tower.Z + 3f), end, 1.3f, true);
        // Tour de transfert : charpente, planchers, bardage partiel en haut.
        var tk = c.Solid.At(tower);
        tk.Col = new Color(0.28f, 0.24f, 0.16f);
        foreach (var (dx, dz) in new[] { (-3f, -3f), (3f, -3f), (-3f, 3f), (3f, 3f) })
            tk.Box(metal, tower + new Vector3(dx, 9f, dz), new Vector3(0.4f, 18f, 0.4f));
        tk.Col = new Color(0.22f, 0.22f, 0.21f);
        tk.BoxMinMax(metal, tower + new Vector3(-1.8f, 13.3f, -3.2f), tower + new Vector3(3.2f, 13.5f, 3.2f));
        tk.BoxMinMax(metal, tower + new Vector3(-3.2f, 17.8f, -3.2f), tower + new Vector3(3.2f, 18f, 3.2f));
        var clad = Mats.Corrugated(new Color(0.42f, 0.4f, 0.36f), 96, 2.4f);
        tk.Col = Colors.White;
        foreach (var (start, along, n, len) in Faces(3.2f, 3.2f))
            if (n.X < 0.5f) WallBand(tk, clad, tower + start, tower + start + along * len, 15f, 17.8f, n, 3f, 3f, Colors.White);
        RailLine(tk, tower + new Vector3(3.1f, 13.5f, -3.1f), tower + new Vector3(3.1f, 13.5f, 3.1f));
        Take(c, tower, 3.6f, 3.6f, 0f);
        // Trémie de chargement au bout de la galerie, au-dessus du quai.
        var ek = c.Solid.At(end);
        ek.Col = new Color(0.25f, 0.24f, 0.22f);
        ek.Frustum(metal, end - Vector3.Up * 3.5f, new Vector2(1f, 1f), new Vector2(3f, 3f), 3.4f);
        ek.Box(metal, end + new Vector3(0, -6f, 0), new Vector3(0.6f, 5f, 0.6f));
        ek.BoxMinMax(metal, end + new Vector3(-1.6f, -0.2f, -1f), end + new Vector3(1.6f, 0f, 2.5f));
        RailLine(ek, end + new Vector3(-1.5f, 0, 2.4f), end + new Vector3(1.5f, 0, 2.4f));
        foreach (var dz in new[] { -2f, 1.5f })
        {
            ek.Box(metal, end + new Vector3(-1.4f, -6.75f, dz), new Vector3(0.35f, 13.5f, 0.35f));
            ek.Box(metal, end + new Vector3(1.4f, -6.75f, dz), new Vector3(0.35f, 13.5f, 0.35f));
            Take(c, end + new Vector3(0, 0, dz), 1.8f, 0.6f, 0f);
        }
    }

    /// <summary>Grue de quai à flèche sur portique : quatre pieds, plateforme tournante, cabine, flèche treillis au-dessus du canal.</summary>
    private static void JibCrane(Ctx c, Vector3 p)
    {
        var k = c.Solid.At(p);
        var paint = Mats.Worn(new Color(0.42f, 0.33f, 0.14f), 0.6f, 0.3f);
        var metal = Mats.DetailMetal();
        k.Col = Colors.White;
        foreach (var (dx, dz) in new[] { (-3.5f, -3.5f), (3.5f, -3.5f), (-3.5f, 3.5f), (3.5f, 3.5f) })
            k.Tube(paint, p + new Vector3(dx * 1.2f, 0, dz * 1.2f), p + new Vector3(dx * 0.7f, 9f, dz * 0.7f), 0.45f, 8, true);
        k.Box(paint, p + new Vector3(0, 9.4f, 0), new Vector3(6.6f, 0.8f, 6.6f));
        k.Col = new Color(0.36f, 0.35f, 0.33f);
        k.Cylinder(metal, p + Vector3.Up * 9.8f, 2.6f, 2.6f, 0.8f, 16);
        // Caisson machinerie, cabine, chevalet.
        k.Col = Colors.White;
        k.Box(paint, p + new Vector3(0, 12.2f, -2f), new Vector3(4.4f, 4f, 6f));
        k.Box(paint, p + new Vector3(1.8f, 12f, 2.4f), new Vector3(2f, 2.6f, 2.4f));
        k.Solid = false;
        k.Col = new Color(0.03f, 0.03f, 0.03f);
        k.Box(metal, p + new Vector3(1.8f, 12.4f, 3.61f), new Vector3(1.8f, 1.2f, 0.02f));
        k.Col = Colors.White;
        // Flèche en treillis (deux membrures solides, treillis en relief) tendue vers le canal (+Z).
        var foot = p + new Vector3(0, 11f, 3f);
        var tip = p + new Vector3(0, 30f, 30f);
        k.Solid = true;
        foreach (var sx in new[] { -0.9f, 0.9f })
        {
            k.Tube(paint, foot + Vector3.Right * sx, tip + Vector3.Right * sx * 0.3f, 0.22f, 6);
            k.Tube(paint, foot + Vector3.Right * sx + Vector3.Up * 1.4f, tip + Vector3.Right * sx * 0.3f + Vector3.Up * 0.3f, 0.16f, 6);
        }
        k.Solid = false;
        for (var i = 0; i < 14; i++)
        {
            var u0 = i / 14f;
            var u1 = (i + 1) / 14f;
            foreach (var sx in new[] { -0.9f, 0.9f })
                k.Tube(paint, foot.Lerp(tip, u0) + Vector3.Right * sx * Mathf.Lerp(1f, 0.3f, u0), foot.Lerp(tip, u1) + Vector3.Right * sx * Mathf.Lerp(1f, 0.3f, u1) + Vector3.Up * Mathf.Lerp(1.4f, 0.3f, u1), 0.06f, 4);
        }
        // Câble et crochet pendant au-dessus de l'eau, haubans.
        k.Col = new Color(0.1f, 0.1f, 0.1f);
        k.Tube(metal, tip, tip - Vector3.Up * 22f, 0.03f, 3);
        k.Box(metal, tip - Vector3.Up * 22.4f, new Vector3(0.8f, 0.8f, 0.5f));
        k.Tube(metal, p + new Vector3(0, 16f, -4f), tip + Vector3.Up * 0.3f, 0.04f, 3);
        k.Solid = true;
        k.Col = new Color(0.4f, 0.39f, 0.37f);
        k.Box(Mats.Detail(), p + new Vector3(0, 14.5f, -4.6f), new Vector3(3.6f, 1.6f, 1.4f)); // contrepoids
        Take(c, p, 5f, 5f, 0f);
        FlowBlock(c, p, new Vector3(8, 1f, 8), 0f);
    }
}
