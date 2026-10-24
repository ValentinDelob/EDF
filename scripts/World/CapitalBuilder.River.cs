using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Fleuve encaissé : murs de quai en pierre de taille, berges basses pavées (rampes pour y descendre), eau sombre et
/// huileuse, péniche coulée ; trois ponts de pierre à trois arches (le grand pont à pylônes dorés et candélabres, un
/// pont éventré dont l'arche centrale s'est effondrée) ; viaduc du métro aérien sur le terre-plein du boulevard
/// (arcades de pierre, voies, station couverte, escalier d'accès, travée écroulée en rampe, rame déraillée).
/// Collision exacte (secteurs solides). Le fleuve bloque le champ de flux, sauf aux ponts intacts.
/// </summary>
public static partial class CapitalBuilder
{
    // ───────────────────────── Fleuve ─────────────────────────

    private static void BuildRiver(Ctx c)
    {
        var stone = Mats.Ashlar();
        // Berges basses (pavés) et lit (vase), solides.
        CityBuilder.AddSlab(c.Root, c.Body, new Vector3(0, BergeY - 0.5f, (QuayN + BergeN) / 2), new Vector3(524, 1f, BergeN - QuayN), Mats.Cobbles());
        CityBuilder.AddSlab(c.Root, c.Body, new Vector3(0, BergeY - 0.5f, (BergeS + QuayS) / 2), new Vector3(524, 1f, QuayS - BergeS), Mats.Cobbles());
        CityBuilder.AddSlab(c.Root, c.Body, new Vector3(0, BedY - 0.5f, (BergeN + BergeS) / 2), new Vector3(524, 1f, BergeS - BergeN), Mats.Concrete(new Color(0.16f, 0.15f, 0.13f), 1601));
        // Eau (visuelle), écume le long des murs des berges.
        var water = Mats.CanalWater(c.Time);
        for (var x = -262f; x < 262f; x += 48f)
        {
            var k = c.Ground.At(new Vector3(x + 24f, 0, BergeN));
            var x1 = x + 48f;
            var foam = new Color(0.8f, 0, 0);
            var clear = new Color(0f, 0, 0);
            void Strip(float z0, float z1, Color c0, Color c1) =>
                k.QuadC(water, new Vector3(x, WaterY, z0), new Vector3(x1, WaterY, z0), new Vector3(x1, WaterY, z1), new Vector3(x, WaterY, z1),
                    Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, c0, c0, c1, c1, Vector3.Up);
            Strip(BergeN, BergeN + 1.5f, foam, clear);
            Strip(BergeN + 1.5f, BergeS - 1.5f, clear, clear);
            Strip(BergeS - 1.5f, BergeS, clear, foam);
        }
        // Murs de quai (du quai à la berge) et murs des berges (de la berge au lit), margelles.
        for (var x = -262f; x < 262f; x += 48f)
        {
            var x1 = x + 48f;
            var k = c.Solid.At(new Vector3(x + 24f, 0, BergeN));
            k.Col = StoneCol * 0.82f;
            k.OrientedQuad(stone, new Vector3(x, BergeY, QuayN), new Vector3(x1, BergeY, QuayN), new Vector3(x1, 0, QuayN), new Vector3(x, 0, QuayN), Vector3.Back);
            k.OrientedQuad(stone, new Vector3(x, BergeY, QuayS), new Vector3(x1, BergeY, QuayS), new Vector3(x1, 0, QuayS), new Vector3(x, 0, QuayS), Vector3.Forward);
            k.Col = StoneCol * 0.7f;
            k.OrientedQuad(stone, new Vector3(x, BedY, BergeN), new Vector3(x1, BedY, BergeN), new Vector3(x1, BergeY, BergeN), new Vector3(x, BergeY, BergeN), Vector3.Back);
            k.OrientedQuad(stone, new Vector3(x, BedY, BergeS), new Vector3(x1, BedY, BergeS), new Vector3(x1, BergeY, BergeS), new Vector3(x, BergeY, BergeS), Vector3.Forward);
            // Margelles des berges (on les enjambe), laisse de crue sombre.
            k.Col = StoneCol * 0.85f;
            k.BoxMinMax(stone, new Vector3(x, BergeY, BergeN - 0.6f), new Vector3(x1, BergeY + 0.18f, BergeN));
            k.BoxMinMax(stone, new Vector3(x, BergeY, BergeS), new Vector3(x1, BergeY + 0.18f, BergeS + 0.6f));
            k.Solid = false;
            k.Col = new Color(0.2f, 0.19f, 0.16f);
            k.OrientedQuad(stone, new Vector3(x, WaterY, BergeN + 0.02f), new Vector3(x1, WaterY, BergeN + 0.02f), new Vector3(x1, WaterY + 0.6f, BergeN + 0.02f), new Vector3(x, WaterY + 0.6f, BergeN + 0.02f), Vector3.Back);
            k.OrientedQuad(stone, new Vector3(x, WaterY, BergeS - 0.02f), new Vector3(x1, WaterY, BergeS - 0.02f), new Vector3(x1, WaterY + 0.6f, BergeS - 0.02f), new Vector3(x, WaterY + 0.6f, BergeS - 0.02f), Vector3.Forward);
            k.Solid = true;
            // Anneaux d'amarrage dans les murs de quai (visuels).
            k.Solid = false;
            k.Col = new Color(0.12f, 0.12f, 0.12f);
            for (var xa = x + 6f; xa < x1; xa += 16f)
                Torus(k, Mats.DetailMetal(), new Vector3(xa, BergeY + 1.6f, QuayN + 0.12f), Vector3.Back, 0.28f, 0.05f, 10);
            k.Solid = true;
        }
        // Rampes de descente sur les berges.
        foreach (var (x, north) in Ramps) Ramp(c, x, north);
        // Parapets de pierre le long des quais (ouverts aux rampes et aux ponts).
        Parapet(c, QuayN - 0.6f, QuayN, true);
        Parapet(c, QuayS, QuayS + 0.6f, false);
        // Le fleuve bloque le champ de flux, sauf aux ponts intacts.
        var x0 = -262f;
        var crossings = new List<(float x, float half)>();
        foreach (var (bx, half, broken) in Bridges) if (!broken) crossings.Add((bx, half));
        crossings.Sort((a, b) => a.x.CompareTo(b.x));
        foreach (var (bx, half) in crossings)
        {
            FlowBlock(c, new Vector3((x0 + bx - half) / 2, 0, (QuayN + QuayS) / 2), new Vector3(bx - half - x0, 0f, QuayS - QuayN - 1f), 0.1f);
            x0 = bx + half;
        }
        FlowBlock(c, new Vector3((x0 + 262f) / 2, 0, (QuayN + QuayS) / 2), new Vector3(262f - x0, 0f, QuayS - QuayN - 1f), 0.1f);
        Take(c, new Rect2(-262, QuayN - 0.6f, 524, QuayS - QuayN + 1.2f), 0f);
        SunkenBarge(c, new Vector3(64f, 0, 106f), 0.08f);
    }

    /// <summary>Rampes des berges : x de départ (haut), quai nord ou sud ; elles descendent vers +x.</summary>
    private static readonly (float x, bool north)[] Ramps = { (-70f, true), (96f, true), (40f, false), (-112f, false) };
    private const float RampLen = 24f, RampW = 3.6f;

    /// <summary>Rampe pavée le long du mur de quai, du quai (y = 0) à la berge, avec son muret.</summary>
    private static void Ramp(Ctx c, float x, bool north)
    {
        var k = c.Solid.At(new Vector3(x + RampLen / 2, 0, north ? QuayN : QuayS));
        var cob = Mats.Cobbles();
        var stone = Mats.Ashlar();
        var zw = north ? QuayN : QuayS;
        var zi = north ? QuayN + RampW : QuayS - RampW;
        var hiW = new Vector3(x, 0f, zw);
        var hiI = new Vector3(x, 0f, zi);
        var loW = new Vector3(x + RampLen, BergeY, zw);
        var loI = new Vector3(x + RampLen, BergeY, zi);
        k.Col = Colors.White;
        k.OrientedQuad(cob, hiW, hiI, loI, loW, Vector3.Up);
        k.Col = StoneCol * 0.8f;
        k.Tri(stone, hiI, loI, new Vector3(x, BergeY, zi), new Vector3(x, BergeY, zw));
        k.OrientedQuad(stone, hiW, hiI, new Vector3(x, BergeY, zi), new Vector3(x, BergeY, zw), Vector3.Left);
        // Muret du côté fleuve (0,9 m), sauf en bas de la rampe.
        var n = north ? Vector3.Back : Vector3.Forward;
        k.Col = StoneCol * 0.88f;
        for (var i = 0; i < 6; i++)
        {
            var t0 = i / 6f;
            var t1 = (i + 1) / 6f;
            if (t1 > 0.95f) break;
            var a = hiI.Lerp(loI, t0);
            var b = hiI.Lerp(loI, t1);
            var o = n * 0.45f;
            k.OrientedQuad(stone, a, b, b + Vector3.Up * 0.9f, a + Vector3.Up * 0.9f, n);
            k.OrientedQuad(stone, a + o, b + o, b + o + Vector3.Up * 0.9f, a + o + Vector3.Up * 0.9f, -n);
            k.OrientedQuad(stone, a + Vector3.Up * 0.9f, b + Vector3.Up * 0.9f, b + o + Vector3.Up * 0.9f, a + o + Vector3.Up * 0.9f, Vector3.Up);
        }
        c.Ruins.Block(new Rect2(x - 3f, north ? QuayN - 6f : QuayS, RampLen + 6f, 6f));
    }

    /// <summary>Parapet de pierre (1 m) le long d'un quai, interrompu aux ponts et en haut des rampes.</summary>
    private static void Parapet(Ctx c, float z0, float z1, bool north)
    {
        var stone = Mats.Ashlar();
        var gaps = new List<(float a, float b)>();
        foreach (var (bx, half, _) in Bridges) gaps.Add((bx - half - 0.6f, bx + half + 0.6f));
        foreach (var (x, n) in Ramps) if (n == north) gaps.Add((x - 1.2f, x + 2.4f));
        gaps.Sort((a, b) => a.a.CompareTo(b.a));
        var x0 = -262f;
        void Run(float a, float b)
        {
            for (var s = a; s < b - 0.1f; s += 24f)
            {
                var e = Mathf.Min(b, s + 24f);
                var k = c.Solid.At(new Vector3((s + e) / 2, 0, z0));
                k.Col = StoneCol * 0.86f;
                k.BoxMinMax(stone, new Vector3(s, Walk, z0), new Vector3(e, 1.05f, z1));
                k.Col = StoneCol * 0.95f;
                k.BoxMinMax(stone, new Vector3(s, 1.05f, z0 - 0.08f), new Vector3(e, 1.2f, z1 + 0.08f));
            }
        }
        foreach (var (a, b) in gaps)
        {
            if (a > x0) Run(x0, a);
            x0 = Mathf.Max(x0, b);
        }
        Run(x0, 262f);
    }

    /// <summary>Péniche coulée par l'avant, cale éventrée, timonerie hors de l'eau (décor solide).</summary>
    private static void SunkenBarge(Ctx c, Vector3 p, float yaw)
    {
        var k = c.Solid.At(p);
        var hull = Mats.Worn(new Color(0.16f, 0.17f, 0.17f), 0.6f, 0.4f);
        var rot = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Back, 0.06f) * new Basis(Vector3.Right, 0.035f);
        var o = new Vector3(p.X, WaterY - 1.6f, p.Z);
        Vector3 P(float x, float y, float z) => o + rot * new Vector3(x, y, z);
        const float hl = 19f, hw = 2.9f, hh = 2.6f;
        k.Col = Colors.White;
        // Flancs et pont (la poupe émerge, la proue plonge).
        foreach (var s in new[] { -1f, 1f })
            k.OrientedQuad(hull, P(-hl, 0, s * hw), P(hl, 0, s * hw), P(hl, hh, s * hw), P(-hl, hh, s * hw), rot * new Vector3(0, 0, s));
        k.OrientedQuad(hull, P(hl, 0, -hw), P(hl, 0, hw), P(hl, hh, hw), P(hl, hh, -hw), rot * Vector3.Right);
        k.OrientedQuad(hull, P(-hl, 0, -hw), P(-hl, 0, hw), P(-hl - 1.6f, hh, hw * 0.6f), P(-hl - 1.6f, hh, -hw * 0.6f), rot * Vector3.Left);
        k.Col = new Color(0.32f, 0.26f, 0.2f);
        // Plats-bords et cale ouverte (fond sombre).
        foreach (var s in new[] { -1f, 1f })
            k.OrientedQuad(Mats.Planks(), P(-hl, hh, s * hw), P(hl, hh, s * hw), P(hl, hh, s * (hw - 0.6f)), P(-hl, hh, s * (hw - 0.6f)), rot * Vector3.Up);
        k.Col = new Color(0.08f, 0.075f, 0.07f);
        k.OrientedQuad(Mats.Charred(), P(-hl + 1, hh - 1.4f, -hw + 0.6f), P(hl - 6, hh - 1.4f, -hw + 0.6f), P(hl - 6, hh - 1.4f, hw - 0.6f), P(-hl + 1, hh - 1.4f, hw - 0.6f), rot * Vector3.Up);
        // Timonerie à la poupe.
        k.Col = new Color(0.28f, 0.29f, 0.28f);
        k.Box(hull, P(hl - 3.2f, hh + 1.3f, 0), new Vector3(4f, 2.6f, 3.4f), rot);
        k.Col = new Color(0.05f, 0.05f, 0.05f);
        k.Box(Mats.Detail(), P(hl - 5.21f, hh + 1.7f, 0), new Vector3(0.02f, 0.8f, 2.6f), rot);
        c.Ruins.Smolder(P(hl - 3f, hh + 3f, 0), 1.2f);
    }

    // ───────────────────────── Ponts ─────────────────────────

    private static void BuildBridges(Ctx c)
    {
        foreach (var (x, half, broken) in Bridges) Bridge(c, x, half, broken);
    }

    /// <summary>
    /// Pont de pierre à trois arches surbaissées (piles à avant-becs dans le fleuve, arches de rive au-dessus des
    /// berges), tablier au niveau des quais, trottoirs, garde-corps de fonte entre des dés de pierre, corniche ;
    /// le grand pont (x = 0) a des candélabres et quatre pylônes à statues dorées ; <paramref name="broken"/> : arche
    /// centrale effondrée (tablier béant, dalles pendantes, gravats dans l'eau).
    /// </summary>
    private static void Bridge(Ctx c, float x, float half, bool broken)
    {
        var k = c.Solid.At(new Vector3(x, 0, (QuayN + QuayS) / 2));
        var stone = Mats.Ashlar();
        var col = StoneCol * 0.84f;
        var springEnd = BergeY;
        var springMid = WaterY + 0.6f;
        // Arches : (z centre, demi-ouverture, naissance).
        var arches = new[] { ((QuayN + 98.5f) / 2, (98.5f - QuayN) / 2, springEnd), (106f, 4.5f, springMid), ((113.5f + QuayS) / 2, (QuayS - 113.5f) / 2, springEnd) };
        var crown = -0.9f;
        for (var i = 0; i < arches.Length; i++)
        {
            if (broken && i == 1) continue;
            var (zc, ah, sy) = arches[i];
            ArchFill(k, stone, new Vector3(x, sy, zc), Vector3.Back, Vector3.Right, ah, crown - sy, -sy, half * 2f, 12, col);
            // Clé d'arc et bandeau de l'intrados sur les deux têtes.
            foreach (var s in new[] { -1f, 1f })
            {
                k.Col = col * 1.08f;
                k.Box(stone, new Vector3(x + s * (half + 0.12f), crown + 0.1f, zc), new Vector3(0.3f, 1.4f, 1.1f));
            }
        }
        // Piles (du lit jusqu'au tablier) avec avant-becs pointus des deux côtés.
        foreach (var (pz0, pz1) in new[] { (98.5f, 101.5f), (110.5f, 113.5f) })
        {
            k.Col = col * 0.92f;
            k.BoxMinMax(stone, new Vector3(x - half, BedY, pz0), new Vector3(x + half, springMid, pz1));
            k.BoxMinMax(stone, new Vector3(x - half, springMid, pz0), new Vector3(x + half, broken ? springMid + 1.5f : 0f, pz1));
            foreach (var s in new[] { -1f, 1f })
            {
                var e = x + s * half;
                var tip = new Vector3(e + s * 2.6f, 0, (pz0 + pz1) / 2);
                var top = -2.2f;
                k.OrientedQuad(stone, new Vector3(e, BedY, pz0), tip with { Y = BedY }, tip with { Y = top }, new Vector3(e, top, pz0), Vector3.Forward + Vector3.Right * s);
                k.OrientedQuad(stone, new Vector3(e, BedY, pz1), tip with { Y = BedY }, tip with { Y = top }, new Vector3(e, top, pz1), Vector3.Back + Vector3.Right * s);
                k.Tri(stone, new Vector3(e, top, pz0), new Vector3(e, top, pz1), tip with { Y = top + 1.6f }, tip with { Y = BedY });
                k.Tri(stone, new Vector3(e, top, pz0), tip with { Y = top }, tip with { Y = top + 1.6f }, new Vector3(e, BedY, pz1));
                k.Tri(stone, new Vector3(e, top, pz1), tip with { Y = top }, tip with { Y = top + 1.6f }, new Vector3(e, BedY, pz0));
            }
        }
        // Tablier : chaussée, trottoirs, corniche, garde-corps.
        var spans = broken ? new[] { (QuayN, 99.2f), (112.8f, QuayS) } : new[] { (QuayN, QuayS) };
        var iron = Mats.IronRail();
        var g = c.Ground.At(new Vector3(x, 0, (QuayN + QuayS) / 2));
        foreach (var (z0, z1) in spans)
        {
            k.Col = col;
            k.BoxMinMax(stone, new Vector3(x - half, -0.9f, z0), new Vector3(x + half, 0f, z1), false);
            g.Col = Colors.White;
            g.OrientedQuad(Mats.Asphalt(), new Vector3(x - half + 2.6f, 0.012f, z0), new Vector3(x + half - 2.6f, 0.012f, z0), new Vector3(x + half - 2.6f, 0.012f, z1), new Vector3(x - half + 2.6f, 0.012f, z1), Vector3.Up);
            foreach (var s in new[] { -1f, 1f })
            {
                var e = x + s * half;
                k.Col = Colors.White;
                k.BoxMinMax(Mats.Sidewalk(), new Vector3(Mathf.Min(e, e - s * 2.6f), 0f, z0), new Vector3(Mathf.Max(e, e - s * 2.6f), Walk, z1));
                k.Col = col * 1.05f;
                k.BoxMinMax(stone, new Vector3(Mathf.Min(e, e + s * 0.45f), -0.75f, z0), new Vector3(Mathf.Max(e, e + s * 0.45f), -0.15f, z1));
                // Dés de pierre tous les 5 m, fonte entre eux.
                for (var z = z0; z < z1 - 0.1f; z += 5f)
                {
                    var ze = Mathf.Min(z1, z + 5f);
                    k.Col = col;
                    k.BoxMinMax(stone, new Vector3(e - s * 0.55f - 0.3f, Walk, z), new Vector3(e - s * 0.55f + 0.3f, 1.25f, z + 0.6f));
                    k.Solid = false;
                    var px = e - s * 0.55f;
                    k.Quad(iron, new Vector3(px, 1.15f, z + 0.6f), new Vector3(px, 1.15f, ze), new Vector3(px, Walk, ze), new Vector3(px, Walk, z + 0.6f),
                        new Vector2(0, 0), new Vector2(ze - z - 0.6f, 0), new Vector2(ze - z - 0.6f, 1), new Vector2(0, 1));
                    k.Solid = true;
                }
                // Main courante continue (solide : on ne tombe pas du pont).
                k.Col = new Color(0.1f, 0.1f, 0.1f);
                k.BoxMinMax(Mats.DetailMetal(), new Vector3(e - s * 0.55f - 0.05f, 1.12f, z0), new Vector3(e - s * 0.55f + 0.05f, 1.2f, z1));
            }
        }
        if (broken)
        {
            // Arche centrale soufflée : dalles du tablier qui pendent vers l'eau, gravats dans le lit.
            var rk = c.Ruins;
            foreach (var (edge, dir) in new[] { (99.2f, 1f), (112.8f, -1f) })
            {
                var hinge = new Vector3(x, -0.4f, edge);
                var tilt = new Basis(Vector3.Right, dir * 0.55f);
                for (var i = 0; i < 3; i++)
                {
                    var w = half * 2f / 3f;
                    var cx = x - half + w * (i + 0.5f);
                    var b2 = new Basis(Vector3.Right, dir * (0.45f + RuinKit.Hash(cx, edge, 471) * 0.5f)) * new Basis(Vector3.Back, (RuinKit.Hash(cx, edge, 472) - 0.5f) * 0.3f);
                    var p = new Vector3(cx, -0.45f, edge) + b2 * new Vector3(0, 0, dir * 2.4f);
                    k.Col = col * 0.9f;
                    k.Box(stone, p, new Vector3(w * 0.95f, 0.9f, 4.8f), b2);
                }
                _ = hinge;
                _ = tilt;
            }
            for (var i = 0; i < 4; i++)
            {
                var p = new Vector3(x + (RuinKit.Hash(i, x, 473) - 0.5f) * half * 2f, BedY + 0.4f, 102f + i * 2.6f);
                rk.Convex(Props.RubblePile(rk.Kit(RuinKit.Layer.Solid, p), p, i, new Vector3(6f, 3.4f + i * 0.4f, 4f), 1700 + i, StoneCol * 0.6f, false));
            }
            rk.Smolder(new Vector3(x, 1f, 98f), 1.4f);
        }
        if (Mathf.Abs(x) < 1f)
        {
            // Grand pont : candélabres à trois lanternes sur les dés, quatre pylônes à statue dorée aux têtes du pont.
            foreach (var s in new[] { -1f, 1f })
            for (var z = QuayN + 6f; z < QuayS - 2f; z += 10f)
                BridgeLamp(c, new Vector3(x + s * (half - 0.55f), Walk, z + 0.3f));
            foreach (var s in new[] { -1f, 1f })
            foreach (var pz in new[] { QuayN - 4.4f, QuayS + 3.4f })
                Pylon(c, new Vector3(x + s * (half + 2.4f), Walk, pz));
        }
        c.Raised.Add(new Rect2(x - half, QuayN, 2.6f, QuayS - QuayN));
        c.Raised.Add(new Rect2(x + half - 2.6f, QuayN, 2.6f, QuayS - QuayN));
    }

    /// <summary>Pylône de tête de pont : socle, fût à refends, chapiteau, cheval ailé doré (stylisé) au sommet.</summary>
    private static void Pylon(Ctx c, Vector3 p)
    {
        var k = c.Solid.At(p);
        var stone = Mats.Ashlar();
        k.Col = StoneCol * 0.86f;
        k.Box(stone, p + Vector3.Up * 2f, new Vector3(4f, 4f, 4f));
        k.Box(stone, p + Vector3.Up * 10f, new Vector3(2.8f, 12f, 2.8f));
        for (var y = 5f; y < 16f; y += 1.2f) k.Box(stone, p + Vector3.Up * y, new Vector3(2.95f, 0.12f, 2.95f));
        k.Box(stone, p + Vector3.Up * 16.6f, new Vector3(3.6f, 1.2f, 3.6f));
        k.Col = Colors.White;
        Statue(k, Mats.Gilt(), p + Vector3.Up * 17.2f, 4.6f, Colors.White, p.Z > 100 ? Mathf.Pi : 0f, true);
        c.Ruins.Block(p, 2.4f, 2.4f);
        FlowBlock(c, p, new Vector3(4f, 17f, 4f));
    }

    /// <summary>Candélabre du grand pont (destructible, famille poteau) : fût de fonte, trois lanternes.</summary>
    private static void BridgeLamp(Ctx c, Vector3 p)
    {
        var lit = c.Night && RuinKit.Hash(p.X, p.Z, 481) < 0.3f && c.LitLamps < 24;
        var id = c.Props.Add(Props.CapitalLamp(lit, true), p, 0f, range: 300, shadows: true);
        if (lit)
        {
            c.LitLamps++;
            var light = new OmniLight3D { Position = p + Vector3.Up * 5.2f, LightColor = new Color(1f, 0.78f, 0.5f), LightEnergy = 2.5f, OmniRange = 16f, ShadowEnabled = false };
            c.Root.AddChild(light);
            Destructibles.AddExtra(id, light);
        }
        var cs = new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.25f, Height = 5.5f }, Position = p + Vector3.Up * 2.75f };
        if (id >= 0) Destructibles.AddShape(id, cs);
        else c.Body.AddChild(cs);
    }

    // ───────────────────────── Viaduc du métro ─────────────────────────

    private const float PierPitch = 24f, StationX0 = 24f, StationX1 = 66f, CollapseX0 = 118f, CollapseX1 = 142f;

    private static void BuildViaduct(Ctx c)
    {
        var stone = Mats.Ashlar();
        var col = StoneCol * 0.78f;
        var deckB = ViaductTop - 1.2f;
        var spring = 3.4f;
        var dz0 = MedianZ0 + 0.4f;
        var dz1 = MedianZ1 - 0.4f;
        var w = dz1 - dz0;
        for (var px = -264f; px < 264f; px += PierPitch)
        {
            var k = c.Solid.At(new Vector3(px, 0, ViaductZ));
            var nx = px + PierPitch;
            var fallen = px >= CollapseX0 - 0.1f && px < CollapseX1 - 0.1f;
            // Pile (2,4 m) et imposte.
            k.Col = col;
            k.BoxMinMax(stone, new Vector3(px - 1.2f, Walk, dz0 - 0.2f), new Vector3(px + 1.2f, fallen ? deckB : deckB, dz1 + 0.2f));
            k.Box(stone, new Vector3(px, spring - 0.2f, ViaductZ), new Vector3(3f, 0.4f, w + 0.6f));
            FlowBlock(c, new Vector3(px, 0, ViaductZ), new Vector3(2.4f, deckB, w));
            if (fallen) continue;
            // Arche entre deux piles, tympans jusqu'au tablier.
            var half = PierPitch / 2 - 1.2f;
            ArchFill(k, stone, new Vector3(px + PierPitch / 2, spring, ViaductZ), Vector3.Right, Vector3.Back, half, deckB - spring - 0.6f, deckB - spring, w, 12, col);
            // Tablier : dalle, corniche, garde-corps de fonte, voies.
            k.Col = col * 1.05f;
            k.BoxMinMax(stone, new Vector3(px, deckB, dz0 - 0.3f), new Vector3(nx, ViaductTop, dz1 + 0.3f));
            k.Col = col * 1.12f;
            k.BoxMinMax(stone, new Vector3(px, ViaductTop - 0.5f, dz0 - 0.55f), new Vector3(nx, ViaductTop - 0.1f, dz0 - 0.3f));
            k.BoxMinMax(stone, new Vector3(px, ViaductTop - 0.5f, dz1 + 0.3f), new Vector3(nx, ViaductTop - 0.1f, dz1 + 0.55f));
            var station = px + PierPitch > StationX0 && px < StationX1;
            if (!station)
                foreach (var z in new[] { dz0 - 0.2f, dz1 + 0.2f })
                {
                    k.Solid = false;
                    k.Quad(Mats.IronRail(), new Vector3(px, ViaductTop + 1.1f, z), new Vector3(nx, ViaductTop + 1.1f, z), new Vector3(nx, ViaductTop, z), new Vector3(px, ViaductTop, z),
                        new Vector2(0, 0), new Vector2(PierPitch, 0), new Vector2(PierPitch, 1), new Vector2(0, 1));
                    k.Solid = true;
                    k.Col = new Color(0.1f, 0.1f, 0.1f);
                    k.BoxMinMax(Mats.DetailMetal(), new Vector3(px, ViaductTop + 1.05f, z - 0.05f), new Vector3(nx, ViaductTop + 1.15f, z + 0.05f));
                }
            Tracks(c, px, nx);
        }
        // Travée écroulée : tablier couché en rampe jusqu'à la chaussée, rame déraillée qui pend.
        {
            var k = c.Solid.At(new Vector3((CollapseX0 + CollapseX1) / 2, 0, ViaductZ));
            var top = new Vector3(CollapseX0 + 1.2f, ViaductTop - 0.6f, ViaductZ);
            var low = new Vector3(CollapseX1 + 3f, Walk + 0.6f, ViaductZ + 1.2f);
            var dir = (low - top).Normalized();
            var side = Vector3.Up.Cross(dir).Normalized();
            var nrm = dir.Cross(side).Normalized();
            if (nrm.Y < 0) nrm = -nrm;
            k.Col = col;
            var hw = w / 2 + 0.3f;
            k.OrientedQuad(stone, top - side * hw, low - side * hw, low + side * hw, top + side * hw, nrm);
            k.OrientedQuad(stone, top - side * hw - nrm * 1.2f, low - side * hw - nrm * 1.2f, low + side * hw - nrm * 1.2f, top + side * hw - nrm * 1.2f, -nrm);
            foreach (var s in new[] { -1f, 1f })
                k.OrientedQuad(stone, top + side * hw * s, low + side * hw * s, low + side * hw * s - nrm * 1.2f, top + side * hw * s - nrm * 1.2f, side * s);
            k.Col = new Color(0.25f, 0.18f, 0.12f);
            foreach (var s in new[] { -1.6f, 1.6f })
            {
                k.Solid = false;
                k.Tube(Mats.DetailMetal(), top + side * (s - 0.7f) + nrm * 0.1f, low + side * (s - 0.7f) + nrm * 0.1f, 0.06f, 4);
                k.Tube(Mats.DetailMetal(), top + side * (s + 0.7f) + nrm * 0.1f, low + side * (s + 0.7f) + nrm * 0.1f, 0.06f, 4);
                k.Solid = true;
            }
            var rk = c.Ruins;
            foreach (var p in new[] { new Vector3(CollapseX1 + 6f, Walk, ViaductZ - 3f), new Vector3(CollapseX0 + 8f, Walk, ViaductZ + 4f) })
                rk.Convex(Props.RubblePile(rk.Kit(RuinKit.Layer.Solid, p), p, p.X, new Vector3(9f, 2.6f, 6f), (int)p.X, StoneCol * 0.65f, false));
            // Voiture de métro pendue au bord de la travée (penchée de 38°), une autre déraillée sur le tablier.
            MetroCar(c, new Vector3(CollapseX0 - 4.5f, ViaductTop + 0.2f, ViaductZ + 0.4f) + new Vector3(Mathf.Cos(0.66f) * 7.5f, -Mathf.Sin(0.66f) * 7.5f, 0), new Basis(Vector3.Back, -0.66f) * new Basis(Vector3.Right, 0.08f));
            MetroCar(c, new Vector3(CollapseX0 - 22f, ViaductTop + 0.25f, ViaductZ - 0.9f), new Basis(Vector3.Up, 0.12f) * new Basis(Vector3.Right, -0.1f));
            rk.Fire(new Vector3(CollapseX0 + 6f, Walk + 1f, ViaductZ), 1.5f, rk.Lights < 6);
            c.HotSpots.Add((new Vector3(CollapseX1, 0, ViaductZ), 26f, 0.5f));
        }
        Station(c);
    }

    /// <summary>Deux voies sur ballast (rails et traverses) sur une travée du viaduc.</summary>
    private static void Tracks(Ctx c, float x0, float x1)
    {
        var k = c.Ground.At(new Vector3((x0 + x1) / 2, ViaductTop, ViaductZ));
        foreach (var tz in new[] { ViaductZ - 1.8f, ViaductZ + 1.8f })
        {
            k.Col = new Color(0.25f, 0.24f, 0.22f);
            k.BoxMinMax(Mats.Gravel(), new Vector3(x0, ViaductTop, tz - 1.4f), new Vector3(x1, ViaductTop + 0.12f, tz + 1.4f), false);
            k.Col = new Color(0.2f, 0.14f, 0.1f);
            for (var x = x0 + 0.3f; x < x1; x += 0.75f)
                k.Box(Mats.Planks(), new Vector3(x, ViaductTop + 0.16f, tz), new Vector3(0.25f, 0.1f, 2.4f), null, false);
            k.Col = new Color(0.3f, 0.26f, 0.22f);
            foreach (var r in new[] { -0.72f, 0.72f })
                k.Box(Mats.DetailMetal(), new Vector3((x0 + x1) / 2, ViaductTop + 0.27f, tz + r), new Vector3(x1 - x0, 0.14f, 0.08f), null, false);
        }
    }

    /// <summary>
    /// Station aérienne : quais de part et d'autre des voies, marquise vitrée (verrière brisée) sur colonnettes de fonte,
    /// escalier droit depuis la chaussée nord jusqu'au quai, palier.
    /// </summary>
    private static void Station(Ctx c)
    {
        var k = c.Solid.At(new Vector3((StationX0 + StationX1) / 2, ViaductTop, ViaductZ));
        var stone = Mats.Ashlar();
        var metal = Mats.DetailMetal();
        var zq0 = MedianZ0 - 4f;
        var zq1 = MedianZ1 + 4f;
        // Quais en encorbellement (dalles sur consoles).
        foreach (var (a, b) in new[] { (zq0, MedianZ0 + 0.1f), (MedianZ1 - 0.1f, zq1) })
        {
            k.Col = StoneCol * 0.8f;
            k.BoxMinMax(stone, new Vector3(StationX0, ViaductTop - 0.6f, a), new Vector3(StationX1, ViaductTop + 0.25f, b));
            for (var x = StationX0 + 2f; x < StationX1; x += 4f)
            {
                k.Col = new Color(0.12f, 0.13f, 0.12f);
                var outer = a < ViaductZ ? a + 0.3f : b - 0.3f;
                k.Tube(metal, new Vector3(x, ViaductTop - 0.6f, outer), new Vector3(x, ViaductTop - 3.2f, a < ViaductZ ? MedianZ0 : MedianZ1), 0.14f, 5);
            }
        }
        // Colonnettes et marquise (verrière en dents de scie, vitres manquantes).
        var glass = Mats.FactoryGlazing();
        for (var x = StationX0 + 3f; x < StationX1; x += 6f)
            foreach (var z in new[] { zq0 + 1f, zq1 - 1f })
            {
                k.Col = new Color(0.1f, 0.12f, 0.11f);
                k.Cylinder(metal, new Vector3(x, ViaductTop + 0.25f, z), 0.14f, 0.12f, 4.6f, 6);
            }
        k.Col = new Color(0.12f, 0.14f, 0.13f);
        foreach (var z in new[] { zq0 + 1f, zq1 - 1f })
            k.BoxMinMax(metal, new Vector3(StationX0 + 2f, ViaductTop + 4.8f, z - 0.15f), new Vector3(StationX1 - 2f, ViaductTop + 5.1f, z + 0.15f));
        for (var x = StationX0 + 2f; x < StationX1 - 2f; x += 3f)
        {
            if (RuinKit.Hash(x, 1, 491) < 0.3f) continue;
            k.Col = Colors.White;
            k.Solid = false;
            k.Quad(glass, new Vector3(x, ViaductTop + 5.1f, zq0 + 1f), new Vector3(x + 3f, ViaductTop + 5.1f, zq0 + 1f), new Vector3(x + 3f, ViaductTop + 6.6f, ViaductZ), new Vector3(x, ViaductTop + 6.6f, ViaductZ),
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0));
            k.Quad(glass, new Vector3(x, ViaductTop + 6.6f, ViaductZ), new Vector3(x + 3f, ViaductTop + 6.6f, ViaductZ), new Vector3(x + 3f, ViaductTop + 5.1f, zq1 - 1f), new Vector3(x, ViaductTop + 5.1f, zq1 - 1f),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            k.Solid = true;
        }
        // Escalier droit depuis la chaussée nord (marches de 0,3 m), palier, raccord au quai.
        const float sx0 = StationX1 + 32f, sx1 = StationX1 + 4f;
        var zs0 = BlvdZ0 + 2f;
        var zs1 = zs0 + 3f;
        var steps = Mathf.CeilToInt(ViaductTop / 0.3f);
        var run = (sx0 - sx1) / steps;
        for (var i = 0; i < steps; i++)
        {
            var y = (i + 1) * ViaductTop / steps;
            var x = sx0 - i * run;
            k.Col = StoneCol * 0.82f;
            k.BoxMinMax(stone, new Vector3(x - run, y - 0.3f, zs0), new Vector3(x, y, zs1));
            if (i % 4 == 0)
            {
                k.Col = new Color(0.1f, 0.1f, 0.1f);
                k.Box(metal, new Vector3(x - run / 2, y / 2, zs0 + 0.2f), new Vector3(0.2f, y, 0.2f));
                k.Box(metal, new Vector3(x - run / 2, y / 2, zs1 - 0.2f), new Vector3(0.2f, y, 0.2f));
            }
        }
        k.Col = StoneCol * 0.82f;
        k.BoxMinMax(stone, new Vector3(StationX1 - 2f, ViaductTop - 0.3f, zs0), new Vector3(sx1 + 0.01f, ViaductTop, zq0 + 0.1f));
        foreach (var z in new[] { zs0, zs1 })
        {
            k.Solid = false;
            k.Quad(Mats.IronRail(), new Vector3(sx0, 1.1f, z), new Vector3(sx1, ViaductTop + 1.1f, z), new Vector3(sx1, ViaductTop, z), new Vector3(sx0, 0, z),
                new Vector2(0, 0), new Vector2(sx0 - sx1, 0), new Vector2(sx0 - sx1, 1), new Vector2(0, 1));
            k.Solid = true;
        }
        c.Ruins.Block(new Rect2(sx1 - 4f, zs0 - 0.5f, sx0 - sx1 + 6f, 4f));
        FlowBlock(c, new Vector3((sx0 + sx1) / 2, 0, (zs0 + zs1) / 2), new Vector3(sx0 - sx1, 4f, 3f));
    }

    /// <summary>Voiture de métro (vert sombre, baies, portes), éventuellement déraillée et penchée (décor solide).</summary>
    private static void MetroCar(Ctx c, Vector3 center, Basis rot)
    {
        var k = c.Solid.At(center);
        var body = Mats.CarPaint(new Color(0.16f, 0.24f, 0.2f));
        const float hl = 7.5f, hw = 1.25f, hh = 1.6f;
        Vector3 P(float x, float y, float z) => center + rot * new Vector3(x, y, z);
        k.Col = Colors.White;
        k.Box(body, center + rot * new Vector3(0, 0.1f, 0), new Vector3(hl * 2, hh * 2 - 0.2f, hw * 2), rot);
        k.Col = new Color(0.04f, 0.04f, 0.045f);
        k.Solid = false;
        foreach (var s in new[] { -1f, 1f })
            for (var x = -hl + 1f; x < hl - 1f; x += 2.2f)
                k.OrientedQuad(Mats.Detail(), P(x, 0.2f, s * (hw + 0.01f)), P(x + 1.5f, 0.2f, s * (hw + 0.01f)), P(x + 1.5f, 1.2f, s * (hw + 0.01f)), P(x, 1.2f, s * (hw + 0.01f)), rot * new Vector3(0, 0, s));
        k.Solid = true;
        k.Col = new Color(0.1f, 0.1f, 0.1f);
        foreach (var bx in new[] { -hl + 2.5f, hl - 2.5f })
            k.Box(Mats.DetailMetal(), P(bx, -hh, 0), new Vector3(3f, 0.8f, 2f), rot);
        k.Col = new Color(0.3f, 0.32f, 0.3f);
        k.Box(Mats.DetailMetal(), P(0, hh, 0), new Vector3(hl * 2 - 0.4f, 0.3f, hw * 2 - 0.4f), rot);
    }
}
