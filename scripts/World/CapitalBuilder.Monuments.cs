using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Monuments de la capitale (bâtiments destructibles, PV multipliés) : parlement à portique et dôme sur la rive sud,
/// deux ministères à colonnade (celui de l'est à demi effondré), cathédrale gothique à deux tours (comble de la nef
/// incendié, flèche réduite à un moignon calciné), opéra (arcades, loggia à colonnes jumelées, dôme vert-de-gris, cage
/// de scène à fronton et statue dorée). Murs portés par le volume plein (non solides), tout ce qui dépasse est exact.
/// </summary>
public static partial class CapitalBuilder
{
    private static readonly Color StoneCol = new(0.95f, 0.93f, 0.9f);
    private static readonly Color DarkGlass = new(0.07f, 0.07f, 0.075f);

    private static void BuildMonuments(Ctx c)
    {
        Parliament(c);
        Ministry(c, -141f, false);
        Ministry(c, 141f, true);
        _pointy = 1.8f; // arcs brisés gothiques
        Cathedral(c);
        _pointy = 1f;
        Opera(c);
    }

    /// <summary>Mur plat de pierre (non solide) de A à B entre deux hauteurs, avec une rangée de baies optionnelle.</summary>
    private static void StoneWall(MeshKit k, Vector3 a, Vector3 b, Vector3 outward, float y0, float y1, Color col)
    {
        var solid = k.Solid;
        k.Solid = false;
        k.Col = col;
        k.OrientedQuad(Mats.Ashlar(), a + Vector3.Up * y0, b + Vector3.Up * y0, b + Vector3.Up * y1, a + Vector3.Up * y1, outward);
        k.Solid = solid;
    }

    /// <summary>Rangée de baies régulièrement espacées le long de [a, b] (centres à <paramref name="pitch"/> m).</summary>
    private static void BayRow(MeshKit k, Vector3 a, Vector3 b, Vector3 outward, float y, float w, float h, float pitch, bool arched, Color col, float margin = 2f)
    {
        var len = a.DistanceTo(b);
        var along = (b - a) / len;
        var n = Mathf.Max(1, (int)((len - margin * 2) / pitch));
        var used = n * pitch;
        var start = (len - used) / 2 + pitch / 2;
        for (var i = 0; i < n; i++)
        {
            var p = a + along * (start + i * pitch) + Vector3.Up * y + outward * 0.01f;
            WindowBay(k, Mats.Ashlar(), Mats.Detail(), p, along, outward, w, h, col, DarkGlass, arched);
        }
    }

    // ───────────────────────── Parlement ─────────────────────────

    private static void Parliament(Ctx c)
    {
        var k = new MeshKit { Record = true };
        var stone = Mats.Ashlar();
        var col = StoneCol;
        const float x0 = -60f, x1 = 60f, z0 = 157f, z1 = 187f, h = 21.5f;
        var origin = new Vector3(0, Walk, (z0 + z1) / 2);
        // Repère local : centre du corps de bâtiment au niveau du parvis.
        Vector3 L(float x, float y, float z) => new Vector3(x, y, z) - origin with { Y = 0 };
        // Murs et baies (façade nord sur le fleuve, côtés).
        var faces = new[]
        {
            (L(x0, 0, z0), L(x1, 0, z0), Vector3.Forward),
            (L(x1, 0, z0), L(x1, 0, z1), Vector3.Right),
            (L(x0, 0, z1), L(x0, 0, z0), Vector3.Left),
            (L(x1, 0, z1), L(x0, 0, z1), Vector3.Back),
        };
        foreach (var (a, b, n) in faces)
        {
            StoneWall(k, a, b, n, 0, h, col * 0.92f);
            BayRow(k, a, b, n, 1.6f, 2.4f, 5.4f, 5.2f, true, col);
            BayRow(k, a, b, n, 10.6f, 2.2f, 4.2f, 5.2f, false, col);
            BayRow(k, a, b, n, 16.6f, 1.8f, 2.2f, 5.2f, false, col);
            var mid = (a + b) / 2;
            var len = a.DistanceTo(b);
            var rot = new Basis(Vector3.Up, Mathf.Atan2((b - a).X, (b - a).Z));
            k.Col = col;
            k.Box(stone, mid + Vector3.Up * 8.9f + n * 0.2f, new Vector3(0.5f, 0.6f, len + 0.4f), rot, false);
            k.Box(stone, mid + Vector3.Up * (h - 0.3f) + n * 0.3f, new Vector3(0.9f, 0.6f, len + 0.8f), rot);
            k.Box(stone, mid + Vector3.Up * 0.5f + n * 0.25f, new Vector3(0.5f, 1f, len + 0.5f), rot, false);
            Balustrade(k, stone, a + Vector3.Up * h + n * 0.2f, b + Vector3.Up * h + n * 0.2f, 1.2f, col);
        }
        // Pavillons d'angle en avant-corps.
        foreach (var sx in new[] { -1f, 1f })
        {
            k.Col = col * 0.97f;
            k.Box(stone, L(sx * 55f, 12f, z0 - 0.8f), new Vector3(10f, 24f, 1.6f));
            Pediment(k, stone, L(sx * 55f, 24f, z0 - 1.6f), Vector3.Right, Vector3.Forward, 5.6f, 2.6f, 1.6f, col);
        }
        // Portique : emmarchement, stylobate, douze colonnes, entablement, fronton, statues aux acrotères.
        const float pz0 = 143f, pz1 = 157f, sty = 3.2f;
        for (var i = 0; i < 8; i++)
        {
            var y = (i + 1) * 0.4f;
            k.Col = col * 0.9f;
            // Marches visuelles ; la collision est le plan incliné qui suit leurs angles rentrants (pente 27°).
            k.Solid = false;
            k.BoxMinMax(stone, L(-26f, 0, pz0 - (8 - i) * 0.8f), L(26f, y, pz0));
            k.Solid = true;
        }
        // Le plan descend sous le parvis (le parvis est à Walk sous le pied des marches) : pas de lèvre au départ.
        k.OrientedQuad(stone, L(-26f, -Walk, pz0 - 6.4f - 2f * Walk), L(26f, -Walk, pz0 - 6.4f - 2f * Walk), L(26f, sty, pz0), L(-26f, sty, pz0), new Vector3(0, 2f, -1f));
        foreach (var sx in new[] { -25.95f, 25.95f })
            k.Tri(stone, L(sx, -Walk, pz0 - 6.4f - 2f * Walk), L(sx, sty, pz0), L(sx, -Walk, pz0), L(0, sty * 0.3f, pz0 - 2f));
        k.Col = col * 0.92f;
        k.BoxMinMax(stone, L(-24.5f, 0, pz0), L(24.5f, sty, pz1));
        for (var i = 0; i < 12; i++)
        {
            var x = -22f + i * 4f;
            Column(k, stone, L(x, sty, pz0 + 1.6f), 0.9f, 15f, col);
            if (i % 3 == 1) Column(k, stone, L(x + 2f, sty, pz0 + 6.5f), 0.85f, 15f, col * 0.95f);
        }
        k.Col = col;
        k.BoxMinMax(stone, L(-24.5f, sty + 15f, pz0 + 0.2f), L(24.5f, sty + 17.4f, pz1));
        k.BoxMinMax(stone, L(-25f, sty + 17.4f, pz0 - 0.2f), L(25f, sty + 18f, pz1));
        Pediment(k, stone, L(0, sty + 18f, pz0 - 0.1f), Vector3.Right, Vector3.Forward, 24.5f, 7.2f, pz1 - pz0, col);
        // Fond du portique sombre (portes), plafond à caissons simplifié.
        k.Col = DarkGlass;
        for (var i = -2; i <= 2; i++)
            k.OrientedQuad(Mats.Detail(), L(i * 8f - 1.8f, sty, z0 - 0.05f), L(i * 8f + 1.8f, sty, z0 - 0.05f), L(i * 8f + 1.8f, sty + 8f, z0 - 0.05f), L(i * 8f - 1.8f, sty + 8f, z0 - 0.05f), Vector3.Forward);
        var bronze = Mats.Bronze();
        Statue(k, bronze, L(0, sty + 25.2f, pz0 + 0.4f), 4.2f, Colors.White, 0f, true);
        Statue(k, bronze, L(-23.5f, sty + 18f, pz0 + 0.6f), 3.4f, Colors.White, 0.3f);
        Statue(k, bronze, L(23.5f, sty + 18f, pz0 + 0.6f), 3.4f, Colors.White, -0.3f);

        // Dôme : socle, colonnade circulaire, tambour, attique, calotte de cuivre à côtes dorées, lanternon.
        var dc = L(0, h, (z0 + z1) / 2);
        k.Col = col;
        Lathe(k, stone, dc, new[] { new Vector2(14.5f, 0), new Vector2(14.5f, 4.2f), new Vector2(13.8f, 4.5f), new Vector2(0, 4.5f) }, 32, col * 0.95f);
        Lathe(k, stone, dc + Vector3.Up * 4.5f, new[] { new Vector2(11.4f, 0), new Vector2(11.4f, 10.2f) }, 32, col * 0.85f);
        for (var i = 0; i < 20; i++)
        {
            var a = i * Mathf.Tau / 20f;
            Column(k, stone, dc + new Vector3(Mathf.Cos(a) * 13.2f, 4.5f, Mathf.Sin(a) * 13.2f), 0.55f, 9.6f, col);
            if (i % 2 == 0)
                WindowBay(k, stone, Mats.Detail(), dc + new Vector3(Mathf.Cos(a + 0.157f) * 11.42f, 6.5f, Mathf.Sin(a + 0.157f) * 11.42f),
                    new Vector3(-Mathf.Sin(a + 0.157f), 0, Mathf.Cos(a + 0.157f)), new Vector3(Mathf.Cos(a + 0.157f), 0, Mathf.Sin(a + 0.157f)), 1.6f, 5f, col, DarkGlass, true, 0.3f);
        }
        Lathe(k, stone, dc + Vector3.Up * 14.1f, new[] { new Vector2(0, 0), new Vector2(14.2f, 0), new Vector2(14.2f, 1.4f), new Vector2(12.6f, 1.6f), new Vector2(12.4f, 4f), new Vector2(12.9f, 4.2f), new Vector2(12.9f, 4.6f) }, 32, col);
        var dome = new List<Vector2>();
        for (var i = 0; i <= 10; i++)
        {
            var t = i / 10f * Mathf.Pi / 2;
            dome.Add(new Vector2(12.2f * Mathf.Cos(t), 18.7f + 13.5f * Mathf.Sin(t)));
        }
        Lathe(k, Mats.Patina(), dc, dome, 32, Colors.White);
        var gilt = Mats.Gilt();
        for (var r = 0; r < 16; r++)
        {
            var a = r * Mathf.Tau / 16f;
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var prev = dc + d * 12.3f + Vector3.Up * 18.7f;
            for (var i = 1; i <= 8; i++)
            {
                var t = i / 9f * Mathf.Pi / 2;
                var p = dc + d * (12.35f * Mathf.Cos(t)) + Vector3.Up * (18.7f + 13.6f * Mathf.Sin(t));
                k.Col = Colors.White;
                k.Tube(gilt, prev, p, 0.18f, 4);
                prev = p;
            }
        }
        // Lanternon : colonnettes, petite coupole, globe et flèche dorés.
        var lc = dc + Vector3.Up * 31.6f;
        Lathe(k, stone, lc, new[] { new Vector2(3.4f, 0), new Vector2(3.4f, 0.6f), new Vector2(2.6f, 0.6f), new Vector2(2.6f, 5.2f), new Vector2(3.1f, 5.4f), new Vector2(3.1f, 5.9f), new Vector2(0, 6f) }, 16, col);
        for (var i = 0; i < 8; i++)
        {
            var a = i * Mathf.Tau / 8f;
            k.Col = col;
            k.Cylinder(stone, lc + new Vector3(Mathf.Cos(a) * 3f, 0.6f, Mathf.Sin(a) * 3f), 0.22f, 0.2f, 4.8f, 6);
        }
        Lathe(k, Mats.Patina(), lc + Vector3.Up * 5.9f, new[] { new Vector2(2.8f, 0), new Vector2(2.2f, 1.6f), new Vector2(1.1f, 2.6f), new Vector2(0, 2.9f) }, 16, Colors.White);
        k.Col = Colors.White;
        Lathe(k, gilt, lc + Vector3.Up * 8.7f, new[] { new Vector2(0.2f, 0), new Vector2(0.75f, 0.6f), new Vector2(0.75f, 1.0f), new Vector2(0.2f, 1.6f), new Vector2(0.12f, 3.6f), new Vector2(0, 3.8f) }, 10, Colors.White);
        // Toit-terrasse en zinc (léger pan) derrière la balustrade.
        k.Col = Colors.White;
        k.OrientedQuad(Mats.Zinc(), L(x0 + 0.5f, h + 0.05f, z0 + 0.5f), L(x1 - 0.5f, h + 0.05f, z0 + 0.5f), L(x1 - 0.5f, h + 0.05f, z1 - 0.5f), L(x0 + 0.5f, h + 0.05f, z1 - 0.5f), Vector3.Up);

        var size = new Vector3(x1 - x0, h, z1 - z0);
        AddBuilding(c, origin, size, k.Commit(), Mats.Ashlar(), 7f);
        Take(c, new Rect2(-27f, 136f, 54f, 21f), 0.5f);
        FlowBlock(c, new Vector3(0, 0, 150f), new Vector3(50f, sty, 14f));
    }

    // ───────────────────────── Ministères ─────────────────────────

    /// <summary>Ministère à colonnade colossale face au fleuve (x centre ; <paramref name="ruined"/> : moitié est effondrée).</summary>
    private static void Ministry(Ctx c, float cx, bool ruined)
    {
        const float z0 = 153f, z1 = 180f, h = 17.5f, len = 118f;
        var x0 = cx - len / 2;
        var x1 = cx + len / 2;
        var cut = ruined ? cx + 6f : x1;   // l'effondrement emporte l'aile est
        var stone = Mats.Ashlar();
        var col = StoneCol * 0.96f;
        var k = new MeshKit { Record = true };
        var origin = new Vector3((x0 + cut) / 2, Walk, (z0 + z1) / 2);
        Vector3 L(float x, float y, float z) => new Vector3(x, y, z) - origin with { Y = 0 };
        var faces = new[]
        {
            (L(x0, 0, z0), L(cut, 0, z0), Vector3.Forward),
            (L(cut, 0, z0), L(cut, 0, z1), Vector3.Right),
            (L(x0, 0, z1), L(x0, 0, z0), Vector3.Left),
            (L(cut, 0, z1), L(x0, 0, z1), Vector3.Back),
        };
        foreach (var (a, b, n) in faces)
        {
            StoneWall(k, a, b, n, 0, h, col * 0.9f);
            BayRow(k, a, b, n, 1.4f, 2.2f, 4.2f, 5f, true, col);
            BayRow(k, a, b, n, 7.6f, 2f, 3.6f, 5f, false, col);
            BayRow(k, a, b, n, 12.6f, 1.8f, 3f, 5f, false, col);
            var mid = (a + b) / 2;
            var l = a.DistanceTo(b);
            var rot = new Basis(Vector3.Up, Mathf.Atan2((b - a).X, (b - a).Z));
            k.Col = col;
            k.Box(stone, mid + Vector3.Up * (h - 0.3f) + n * 0.3f, new Vector3(0.9f, 0.6f, l + 0.8f), rot);
            Balustrade(k, stone, a + Vector3.Up * h + n * 0.2f, b + Vector3.Up * h + n * 0.2f, 1.1f, col);
        }
        // Colonnade colossale sur stylobate devant la façade nord.
        k.Col = col * 0.9f;
        k.BoxMinMax(stone, L(x0, 0, z0 - 6f), L(cut, 1.2f, z0));
        for (var x = x0 + 3f; x < cut - 1f; x += 5f)
            Column(k, stone, L(x, 1.2f, z0 - 4.2f), 0.75f, 14f, col);
        k.Col = col;
        k.BoxMinMax(stone, L(x0, 15.2f, z0 - 5.8f), L(cut, 17.5f, z0));
        // Comble de zinc en croupe.
        k.Col = Colors.White;
        k.Frustum(Mats.Zinc(), L((x0 + cut) / 2, h + 0.1f, (z0 + z1) / 2), new Vector2(cut - x0 - 3f, z1 - z0 - 3f), new Vector2(cut - x0 - 14f, 4f), 6.5f);
        AddBuilding(c, origin, new Vector3(cut - x0, h, z1 - z0), k.Commit(), Mats.Ashlar(), 4f);
        Take(c, new Rect2(x0, z0 - 7f, cut - x0, 7f), 0.3f);
        FlowBlock(c, new Vector3((x0 + cut) / 2, 0, z0 - 3f), new Vector3(cut - x0, 1.2f, 6f));
        if (!ruined) return;

        // Aile effondrée : carcasse éventrée, colonnes brisées, gravats sur le quai.
        var rk = c.Ruins;
        var wc = new Vector3((cut + x1) / 2 + 2f, Walk, (z0 + z1) / 2);
        var boxes = new List<(Transform3D, Vector3)>();
        Props.RuinedShell(rk.Kit(RuinKit.Layer.Solid, wc), wc, new Vector2(x1 - cut - 6f, z1 - z0), h, 7717, new Color(0.62f, 0.58f, 0.5f), boxes);
        foreach (var (xf, s) in boxes) rk.Box(xf, s);
        FlowBlock(c, wc, new Vector3(x1 - cut - 6f, h, z1 - z0));
        var kk = rk.Kit(RuinKit.Layer.Solid, wc);
        for (var x = cut + 3f; x < x1; x += 5f)
        {
            var hh = RuinKit.Hash(x, 3, 451);
            var p = new Vector3(x, Walk, z0 - 4.2f);
            if (hh < 0.3f) continue;
            var ch = hh < 0.6f ? 2f + hh * 6f : 14f;
            kk.Col = StoneCol * 0.85f;
            kk.Cylinder(stone, p, 0.75f, 0.72f, ch, 12);
            rk.Box(new Transform3D(Basis.Identity, p + Vector3.Up * ch / 2), new Vector3(1.4f, ch, 1.4f));
            if (hh < 0.6f)
            {
                // Tambour tombé au pied de la colonne.
                var fall = new Basis(Vector3.Up, hh * 9f) * new Basis(Vector3.Right, Mathf.Pi / 2);
                var q = p + new Basis(Vector3.Up, hh * 9f) * new Vector3(0, 0.75f, 2.4f);
                kk.Cylinder(stone, q - fall.Y * 1.6f, 0.74f, 0.74f, 3.2f, 12, true, fall);
            }
        }
        foreach (var (dx, dz, s) in new[] { (0f, -9f, 1f), (14f, -11f, 0.8f), (-12f, -8f, 0.7f) })
        {
            var p = new Vector3((cut + x1) / 2 + dx, Walk, z0 + dz);
            rk.Convex(Props.RubblePile(rk.Kit(RuinKit.Layer.Solid, p), p, dx, new Vector3(16f * s, 4f * s, 9f * s), (int)(dx * 7 + 3), new Color(0.62f, 0.58f, 0.5f), false));
        }
        rk.Fire(wc + new Vector3(-6f, 2f, 0), 2.2f, rk.Lights < 5);
        rk.Smolder(wc + new Vector3(8f, 6f, 4f), 2.6f);
        c.HotSpots.Add((wc with { Z = z0 - 6f }, 30f, 0.55f));
    }

    // ───────────────────────── Cathédrale ─────────────────────────

    private static void Cathedral(Ctx c)
    {
        const float cz = -146f;
        var stone = Mats.Ashlar();
        var col = StoneCol * 0.88f;
        var dark = Mats.Detail();
        var stained = Mats.StainedGlass(c.Night);

        // Façade à deux tours (bâtiment A) : face +x sur le parvis.
        {
            const float fx0 = -86f, fx1 = -70f, fz0 = cz - 21f, fz1 = cz + 21f, hc = 40f, ht = 64f;
            var origin = new Vector3((fx0 + fx1) / 2, Walk, cz);
            var k = new MeshKit { Record = true };
            Vector3 L(float x, float y, float z) => new Vector3(x, y, z) - origin with { Y = 0 };
            // Volume central et tours (murs non solides, volume plein = boîte jusqu'à 40 m).
            StoneWall(k, L(fx1, 0, fz0), L(fx1, 0, fz1), Vector3.Right, 0, hc, col);
            StoneWall(k, L(fx0, 0, fz1), L(fx0, 0, fz0), Vector3.Left, 0, hc, col * 0.9f);
            StoneWall(k, L(fx0, 0, fz0), L(fx1, 0, fz0), Vector3.Forward, 0, hc, col * 0.92f);
            StoneWall(k, L(fx1, 0, fz1), L(fx0, 0, fz1), Vector3.Back, 0, hc, col * 0.92f);
            // Tours au-dessus de 40 m (solides) : faces percées de deux hautes lancettes.
            foreach (var tz in new[] { fz0 + 7f, fz1 - 7f })
            {
                var tc = L((fx0 + fx1) / 2, hc, tz);
                k.Col = col;
                Shell(k, stone, tc + new Vector3(-(fx1 - fx0) / 2, 0, -7f), tc + new Vector3((fx1 - fx0) / 2, ht - hc, 7f));
                foreach (var (n, along, w) in new[] { (Vector3.Right, Vector3.Back, fx1 - fx0), (Vector3.Left, Vector3.Forward, fx1 - fx0), (Vector3.Forward, Vector3.Right, 14f), (Vector3.Back, Vector3.Left, 14f) })
                {
                    var face = tc + n * ((n.X != 0 ? (fx1 - fx0) : 14f) / 2);
                    foreach (var s in new[] { -1f, 1f })
                        WindowBay(k, stone, dark, face + along * (s * 3f) + Vector3.Up * 3f, along, n, 2.2f, 16f, col, DarkGlass, true, 0.6f);
                }
                Balustrade(k, stone, tc + new Vector3(-(fx1 - fx0) / 2, ht - hc, -7f), tc + new Vector3((fx1 - fx0) / 2, ht - hc, -7f), 1.2f, col);
                Balustrade(k, stone, tc + new Vector3(-(fx1 - fx0) / 2, ht - hc, 7f), tc + new Vector3((fx1 - fx0) / 2, ht - hc, 7f), 1.2f, col);
                Balustrade(k, stone, tc + new Vector3((fx1 - fx0) / 2, ht - hc, -7f), tc + new Vector3((fx1 - fx0) / 2, ht - hc, 7f), 1.2f, col);
                Balustrade(k, stone, tc + new Vector3(-(fx1 - fx0) / 2, ht - hc, -7f), tc + new Vector3(-(fx1 - fx0) / 2, ht - hc, 7f), 1.2f, col);
                // Contreforts d'angle de la tour sur la façade.
                foreach (var s in new[] { -6.2f, 6.2f })
                {
                    k.Col = col * 0.95f;
                    k.BoxMinMax(stone, L(fx1, 0, tz + s - 0.9f), L(fx1 + 1.8f, 30f, tz + s + 0.9f));
                    k.BoxMinMax(stone, L(fx1, 30f, tz + s - 0.7f), L(fx1 + 1.1f, 44f, tz + s + 0.7f));
                }
            }
            // Portails à voussures (creusés), galerie des rois, rose, pignon central.
            var fc = L(fx1 + 0.01f, 0, cz);
            WindowBay(k, stone, dark, fc, Vector3.Back, Vector3.Right, 8f, 13f, col, new Color(0.05f, 0.04f, 0.035f), true, 2.4f);
            WindowBay(k, stone, dark, fc + Vector3.Back * 0.6f + Vector3.Right * 0.01f, Vector3.Back, Vector3.Right, 9.6f, 14f, col, new Color(0.05f, 0.04f, 0.035f), true, 1.2f);
            foreach (var s in new[] { -14f, 14f })
                WindowBay(k, stone, dark, fc + Vector3.Back * s, Vector3.Back, Vector3.Right, 6f, 11f, col, new Color(0.05f, 0.04f, 0.035f), true, 2f);
            for (var i = 0; i < 24; i++)
            {
                var z = cz - 19f + i * 38f / 23f;
                WindowBay(k, stone, dark, L(fx1 + 0.01f, 17.5f, z), Vector3.Back, Vector3.Right, 1.1f, 3.2f, col, new Color(0.18f, 0.17f, 0.15f), true, 0.5f);
            }
            k.Col = col;
            k.BoxMinMax(stone, L(fx1, 21.2f, cz - 21f), L(fx1 + 0.7f, 22f, cz + 21f));
            // Rose : disque de vitrail sombre, remplage rayonnant.
            var rc = L(fx1 + 0.05f, 28.5f, cz);
            k.Col = Colors.White;
            const int rs = 24;
            for (var i = 0; i < rs; i++)
            {
                var a0 = i * Mathf.Tau / rs;
                var a1 = (i + 1) * Mathf.Tau / rs;
                k.Tri(stained, rc, rc + new Vector3(0, Mathf.Sin(a0), Mathf.Cos(a0)) * 5f, rc + new Vector3(0, Mathf.Sin(a1), Mathf.Cos(a1)) * 5f, rc + Vector3.Left);
                k.Col = col;
                k.Tube(stone, rc + Vector3.Right * 0.15f, rc + Vector3.Right * 0.15f + new Vector3(0, Mathf.Sin(a0), Mathf.Cos(a0)) * 5f, 0.12f, 4);
                k.Col = Colors.White;
            }
            k.Col = col;
            Torus(k, stone, rc + Vector3.Right * 0.2f, Vector3.Right, 5.2f, 0.35f, 32);
            Torus(k, stone, rc + Vector3.Right * 0.2f, Vector3.Right, 1.6f, 0.25f, 16);
            Balustrade(k, stone, L(fx1 + 0.3f, hc, cz - 7f), L(fx1 + 0.3f, hc, cz + 7f), 1.4f, col);
            AddBuilding(c, origin, new Vector3(fx1 - fx0, hc, fz1 - fz0), k.Commit(), Mats.Ashlar(), 6f);
        }

        // Nef, bas-côtés, transept et abside (bâtiment B) : comble incendié à la croisée, flèche effondrée.
        {
            const float nx0 = -150f, nx1 = -86f, nh = 30f, nw = 11f, aw = 19f, ah = 14f;
            var origin = new Vector3((nx0 + nx1) / 2, Walk, cz);
            var k = new MeshKit { Record = true };
            Vector3 L(float x, float y, float z) => new Vector3(x, y, z) - origin with { Y = 0 };
            // Nef : murs hauts (non solides : volume plein convexe de la nef), fenêtres hautes en lancette.
            foreach (var (zz, n) in new[] { (cz - nw, Vector3.Forward), (cz + nw, Vector3.Back) })
            {
                var a = L(nx0, 0, zz);
                var b = L(nx1, 0, zz);
                StoneWall(k, a, b, n, ah, nh, col * 0.9f);
                for (var x = nx0 + 4f; x < nx1 - 2f; x += 6.4f)
                {
                    if (x > -132f && x < -116f) continue; // croisée du transept
                    WindowBay(k, stone, stained, L(x, 18.6f, zz) + n * 0.01f, Vector3.Right, n, 2.6f, 9f, col, Colors.White, true, 0.5f);
                }
                k.Col = col;
                k.Box(stone, (a + b) / 2 + Vector3.Up * (nh - 0.2f) + n * 0.3f, new Vector3(0.8f, 0.5f, nx1 - nx0), new Basis(Vector3.Up, Mathf.Pi / 2));
            }
            // Bas-côtés : murs, verrières basses, toits en appentis, contreforts et arcs-boutants.
            foreach (var s in new[] { -1f, 1f })
            {
                var zo = cz + s * aw;
                var n = s > 0 ? Vector3.Back : Vector3.Forward;
                k.Col = col * 0.92f;
                Shell(k, stone, L(nx0, 0, Mathf.Min(zo, zo - s * 0.8f)), L(nx1, ah, Mathf.Max(zo, zo - s * 0.8f)));
                for (var x = nx0 + 4f; x < nx1 - 2f; x += 6.4f)
                {
                    if (x > -132f && x < -116f) continue;
                    WindowBay(k, stone, stained, L(x, 3.5f, zo) + n * 0.01f, Vector3.Right, n, 2.4f, 7.5f, col, Colors.White, true, 0.4f);
                }
                // Appentis d'ardoise du mur des bas-côtés au pied des fenêtres hautes.
                k.Col = new Color(0.32f, 0.33f, 0.36f);
                k.OrientedQuad(Mats.Slate(), L(nx0, ah, zo), L(nx1, ah, zo), L(nx1, 18f, cz + s * nw), L(nx0, 18f, cz + s * nw), Vector3.Up + n);
                for (var x = nx0 + 0.8f; x < nx1; x += 6.4f)
                {
                    if (x > -132f && x < -116f) continue;
                    // Culée (contrefort), pinacle et arc-boutant jusqu'au haut du mur de la nef.
                    k.Col = col * 0.95f;
                    k.BoxMinMax(stone, L(x - 0.9f, 0, zo + s * 0.1f - (s < 0 ? 2.4f : 0)), L(x + 0.9f, 22f, zo + s * 0.1f + (s > 0 ? 2.4f : 0)));
                    var pin = L(x, 22f, zo + s * 1.3f);
                    k.Cylinder(stone, pin, 0.85f, 0.05f, 4.5f, 4);
                    var foot = L(x, 20f, zo + s * 0.4f);
                    var head = L(x, 27.5f, cz + s * (nw + 0.4f));
                    var mid = (foot + head) / 2 + Vector3.Up * 1.6f;
                    k.Tube(stone, foot, mid, 0.55f, 5);
                    k.Tube(stone, mid, head, 0.5f, 5);
                }
            }
            // Transept : bras nord et sud avec pignons et roses.
            foreach (var s in new[] { -1f, 1f })
            {
                var te = cz + s * 25f;
                k.Col = col * 0.92f;
                Shell(k, stone, L(-131f, 0, Mathf.Min(te, cz + s * nw)), L(-117f, nh, Mathf.Max(te, cz + s * nw)), false);
                var face = L(-124f, 0, te + s * 0.01f);
                var n = s > 0 ? Vector3.Back : Vector3.Forward;
                WindowBay(k, stone, dark, face, Vector3.Right * s, n, 5f, 9f, col, new Color(0.05f, 0.04f, 0.035f), true, 1.4f);
                var rc = face + Vector3.Up * 20f + n * 0.05f;
                k.Col = Colors.White;
                for (var i = 0; i < 16; i++)
                {
                    var a0 = i * Mathf.Tau / 16;
                    var a1 = (i + 1) * Mathf.Tau / 16;
                    k.Tri(stained, rc, rc + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * 4.5f, rc + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * 4.5f, rc - n);
                }
                // Pignon d'ardoise.
                k.Col = new Color(0.3f, 0.31f, 0.34f);
                var g0 = L(-131f, nh, te); var g1 = L(-117f, nh, te); var ga = L(-124f, nh + 13f, te);
                k.Tri(stone, g0, g1, ga, ga - n);
                k.OrientedQuad(Mats.Slate(), g0, ga, ga - n * (Mathf.Abs(te - cz) - nw + 0.5f), g0 - n * (Mathf.Abs(te - cz) - nw + 0.5f), Vector3.Up + Vector3.Left);
                k.OrientedQuad(Mats.Slate(), g1, ga, ga - n * (Mathf.Abs(te - cz) - nw + 0.5f), g1 - n * (Mathf.Abs(te - cz) - nw + 0.5f), Vector3.Up + Vector3.Right);
            }
            // Comble de la nef (ardoise et plomb) ; incendié de la croisée jusqu'à la quatrième travée.
            var ridge = nh + 14f;
            var charred = Mats.Charred();
            var burnt0 = -136f;
            var burnt1 = -104f;
            foreach (var s in new[] { -1f, 1f })
            {
                var eave = cz + s * (nw + 0.5f);
                k.Col = new Color(0.3f, 0.31f, 0.34f);
                k.OrientedQuad(Mats.Slate(), L(nx0, nh, eave), L(burnt0, nh, eave), L(burnt0, ridge, cz), L(nx0, ridge, cz), Vector3.Up + Vector3.Back * s);
                k.OrientedQuad(Mats.Slate(), L(burnt1, nh, eave), L(nx1, nh, eave), L(nx1, ridge, cz), L(burnt1, ridge, cz), Vector3.Up + Vector3.Back * s);
                // Partie brûlée : chevrons noircis, dont beaucoup brisés.
                k.Col = new Color(0.14f, 0.12f, 0.1f);
                for (var x = burnt0 + 0.6f; x < burnt1; x += 1.1f)
                {
                    var hgt = RuinKit.Hash(x, s, 461);
                    var p0 = L(x, nh, eave);
                    var p1 = L(x, ridge, cz);
                    k.Tube(charred, p0, hgt < 0.45f ? p0.Lerp(p1, 0.25f + hgt) : p1, 0.16f, 4);
                }
            }
            // Voûte noircie visible par le comble ouvert, faîtage partiel.
            k.Col = new Color(0.22f, 0.21f, 0.2f);
            k.OrientedQuad(charred, L(burnt0, nh + 0.3f, cz - nw), L(burnt1, nh + 0.3f, cz - nw), L(burnt1, nh + 0.3f, cz + nw), L(burnt0, nh + 0.3f, cz + nw), Vector3.Up);
            k.Tube(charred, L(burnt0, ridge, cz), L(burnt0 + 9f, ridge - 0.6f, cz), 0.2f, 4);
            // Moignon de la flèche à la croisée : charpente calcinée, penchée.
            var sp = L(-124f, ridge, cz);
            for (var i = 0; i < 6; i++)
            {
                var a = i * Mathf.Tau / 6;
                var tip = sp + new Vector3(Mathf.Cos(a) * 0.6f + 1.4f, 6f + RuinKit.Hash(i, 2, 462) * 4f, Mathf.Sin(a) * 0.6f);
                k.Tube(charred, sp + new Vector3(Mathf.Cos(a) * 2.2f, -1f, Mathf.Sin(a) * 2.2f), tip, 0.22f, 4);
            }
            // Abside : demi-cylindre et toit conique.
            var ac = L(nx0, 0, cz);
            k.Col = col * 0.9f;
            Lathe(k, stone, ac, new[] { new Vector2(nw, 0), new Vector2(nw, nh) }, 12, col * 0.9f, Mathf.Pi / 2, Mathf.Pi * 1.5f);
            Lathe(k, Mats.Slate(), ac + Vector3.Up * nh, new[] { new Vector2(nw + 0.5f, 0), new Vector2(0, 14f) }, 12, new Color(0.3f, 0.31f, 0.34f), Mathf.Pi / 2, Mathf.Pi * 1.5f);
            for (var i = 0; i < 5; i++)
            {
                var a = Mathf.Pi / 2 + (i + 0.5f) * Mathf.Pi / 5;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                WindowBay(k, stone, stained, ac + d * (nw + 0.01f) + Vector3.Up * 18f, new Vector3(-d.Z, 0, d.X), d, 2.4f, 9f, col, Colors.White, true, 0.4f);
                k.Col = col * 0.95f;
                k.Box(stone, ac + d * (nw + 1.2f) + Vector3.Up * 11f, new Vector3(1.6f, 22f, 2.4f), new Basis(Vector3.Up, Mathf.Atan2(d.X, d.Z)));
            }
            var hull = new List<Vector3>();
            foreach (var x in new[] { nx0 - 0.1f, nx1 })
            foreach (var z in new[] { cz - nw, cz + nw })
            {
                hull.Add(L(x, 0, z));
                hull.Add(L(x, nh, z));
            }
            AddBuilding(c, origin, new Vector3(nx1 - nx0, nh, aw * 2f), k.Commit(), Mats.Ashlar(), 6f, hull.ToArray());
            FlowBlock(c, origin with { Y = 0 }, new Vector3(nx1 - nx0 + 12f, ah, aw * 2f + 6f));
        }
    }

    // ───────────────────────── Opéra ─────────────────────────

    private static void Opera(Ctx c)
    {
        const float x0 = 90f, x1 = 168f, z0 = -168f, z1 = -124f, h = 24f;
        var cz = (z0 + z1) / 2;
        var origin = new Vector3((x0 + x1) / 2, Walk, cz);
        var stone = Mats.Ashlar();
        var gilt = Mats.Gilt();
        var col = StoneCol * 0.92f;
        var k = new MeshKit { Record = true };
        Vector3 L(float x, float y, float z) => new Vector3(x, y, z) - origin with { Y = 0 };
        StoneWall(k, L(x0, 0, z1), L(x0, 0, z0), Vector3.Left, 0, h, col);
        StoneWall(k, L(x0, 0, z0), L(x1, 0, z0), Vector3.Forward, 0, h, col * 0.9f);
        StoneWall(k, L(x1, 0, z1), L(x0, 0, z1), Vector3.Back, 0, h, col * 0.9f);
        StoneWall(k, L(x1, 0, z0), L(x1, 0, z1), Vector3.Right, 0, h, col * 0.85f);
        foreach (var (a, b, n) in new[] { (L(x0, 0, z0), L(x1, 0, z0), Vector3.Forward), (L(x1, 0, z1), L(x0, 0, z1), Vector3.Back) })
        {
            BayRow(k, a, b, n, 1.5f, 3f, 6f, 7f, true, col);
            BayRow(k, a, b, n, 11f, 2.4f, 6.5f, 7f, true, col);
        }
        // Façade : sept arcades, loggia à colonnes jumelées, œils-de-bœuf, attique, groupes dorés.
        var front = L(x0 - 0.01f, 0, cz);
        for (var i = -3; i <= 3; i++)
            WindowBay(k, stone, Mats.Detail(), front + Vector3.Back * (i * 6f), Vector3.Back, Vector3.Left, 4.2f, 7.4f, col, DarkGlass, true, 1.6f);
        k.Col = col;
        k.BoxMinMax(stone, L(x0 - 2.4f, 8.2f, z0 + 1f), L(x0, 9.2f, z1 - 1f));
        for (var i = -3; i <= 3; i++)
        {
            var zc = cz + i * 6f;
            WindowBay(k, stone, Mats.Detail(), L(x0 - 0.01f, 10.4f, zc), Vector3.Back, Vector3.Left, 3f, 7f, col, DarkGlass, true, 0.8f);
            if (i < 3)
            {
                foreach (var dz in new[] { 2.4f, 3.6f })
                    Column(k, stone, L(x0 - 1.6f, 9.2f, zc + dz), 0.42f, 9.2f, col * 1.04f);
                var ob = L(x0 - 0.05f, 19.2f, zc + 3f);
                k.Col = DarkGlass;
                for (var q = 0; q < 10; q++)
                {
                    var a0 = q * Mathf.Tau / 10;
                    var a1 = (q + 1) * Mathf.Tau / 10;
                    k.Tri(Mats.Detail(), ob, ob + new Vector3(0, Mathf.Sin(a0), Mathf.Cos(a0)) * 0.9f, ob + new Vector3(0, Mathf.Sin(a1), Mathf.Cos(a1)) * 0.9f, ob + Vector3.Right);
                }
                k.Col = Colors.White;
                Torus(k, gilt, ob + Vector3.Left * 0.05f, Vector3.Right, 1.0f, 0.12f, 12);
            }
        }
        k.Col = col;
        k.BoxMinMax(stone, L(x0 - 2.6f, 18.4f, z0 + 0.5f), L(x0, 20.6f, z1 - 0.5f));
        k.BoxMinMax(stone, L(x0 - 1f, 20.6f, z0 + 2f), L(x0, h + 2.5f, z1 - 2f));
        foreach (var s in new[] { -1f, 1f })
        {
            var gp = L(x0 - 1.2f, h + 2.5f, cz + s * 19f);
            k.Col = Colors.White;
            Statue(k, gilt, gp, 4.6f, Colors.White, Mathf.Pi / 2 * -1, true);
            Statue(k, gilt, gp + Vector3.Back * 1.6f * s, 3.4f, Colors.White, -Mathf.Pi / 2 + 0.4f * s);
        }
        // Dôme de la salle : coupole basse vert-de-gris, côtes et couronne dorées.
        var dc = L(x0 + 30f, h, cz);
        k.Col = col;
        Lathe(k, stone, dc, new[] { new Vector2(18.5f, 0), new Vector2(18.5f, 2.2f), new Vector2(17.6f, 2.4f) }, 32, col);
        var dome = new List<Vector2>();
        for (var i = 0; i <= 8; i++)
        {
            var t = i / 8f * Mathf.Pi / 2;
            dome.Add(new Vector2(17.4f * Mathf.Cos(t), 2.4f + 8.5f * Mathf.Sin(t)));
        }
        Lathe(k, Mats.Patina(), dc, dome, 32, Colors.White);
        for (var r = 0; r < 12; r++)
        {
            var a = r * Mathf.Tau / 12f;
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var prev = dc + d * 17.5f + Vector3.Up * 2.4f;
            for (var i = 1; i <= 6; i++)
            {
                var t = i / 7f * Mathf.Pi / 2;
                var p = dc + d * (17.55f * Mathf.Cos(t)) + Vector3.Up * (2.4f + 8.6f * Mathf.Sin(t));
                k.Col = Colors.White;
                k.Tube(gilt, prev, p, 0.2f, 4);
                prev = p;
            }
        }
        k.Col = Colors.White;
        Lathe(k, gilt, dc + Vector3.Up * 10.7f, new[] { new Vector2(2.2f, 0), new Vector2(2.6f, 0.8f), new Vector2(1.2f, 1.6f), new Vector2(0.4f, 2.8f), new Vector2(0, 3.2f) }, 12, Colors.White);
        // Cage de scène : haut volume à fronton côté salle, statue dorée au sommet.
        const float sx0 = 136f, sx1 = 166f;
        k.Col = col * 0.88f;
        k.BoxMinMax(stone, L(sx0, h, z0 + 1f), L(sx1, 42f, z1 - 1f));
        Pediment(k, Mats.Patina(), L(sx0 - 0.3f, 42f, cz), Vector3.Back, Vector3.Left, (z1 - z0) / 2 - 0.5f, 9f, sx1 - sx0 + 0.3f, Colors.White);
        k.Col = Colors.White;
        Statue(k, gilt, L(sx0 + 0.6f, 51f, cz), 6.5f, Colors.White, -Mathf.Pi / 2, true);
        foreach (var s in new[] { -1f, 1f })
        {
            k.Col = Colors.White;
            Statue(k, gilt, L(sx0 + 0.6f, 42.4f, cz + s * 20.5f), 3.2f, Colors.White, -Mathf.Pi / 2);
        }
        // Toit-terrasse de cuivre.
        k.Col = Colors.White;
        k.OrientedQuad(Mats.Patina(), L(x0 + 0.5f, h + 0.05f, z0 + 0.5f), L(x1 - 0.5f, h + 0.05f, z0 + 0.5f), L(x1 - 0.5f, h + 0.05f, z1 - 0.5f), L(x0 + 0.5f, h + 0.05f, z1 - 0.5f), Vector3.Up);
        Balustrade(k, stone, L(x0 + 0.4f, h, z0 + 0.4f), L(x1 - 0.4f, h, z0 + 0.4f), 1.2f, col);
        Balustrade(k, stone, L(x0 + 0.4f, h, z1 - 0.4f), L(x1 - 0.4f, h, z1 - 0.4f), 1.2f, col);
        // Emmarchement devant les arcades.
        for (var i = 0; i < 5; i++)
        {
            k.Col = col * 0.9f;
            k.BoxMinMax(stone, L(x0 - 4f + i * 0.8f, 0, z0 + 2f), L(x0, (i + 1) * 0.3f, z1 - 2f));
        }
        AddBuilding(c, origin, new Vector3(x1 - x0, h, z1 - z0), k.Commit(), Mats.Ashlar(), 6f);
    }
}
