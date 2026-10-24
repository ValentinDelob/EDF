using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Au-delà des murs de la capitale (sans collision ni ombres, secteurs de 400 m) : îlots de toits de zinc à perte de
/// vue (certains éventrés et noircis), le fleuve qui file entre ses quais vers l'est et l'ouest avec d'autres ponts,
/// une colline couronnée d'une basilique blanche au nord, un dôme sur la rive sud, une tour de treillis brisée au
/// sud-est. Outil « --capprobe » : parcours des accès en hauteur avec la vraie physique.
/// </summary>
public static partial class CapitalBuilder
{
    private sealed class FarChunks
    {
        private const float Size = 400f;
        private readonly Dictionary<(int, int), MeshKit> _kits = new();

        public MeshKit At(Vector3 p)
        {
            var key = (Mathf.FloorToInt(p.X / Size), Mathf.FloorToInt(p.Z / Size));
            if (!_kits.TryGetValue(key, out var k)) _kits[key] = k = new MeshKit();
            return k;
        }

        public void Flush(Node3D root)
        {
            foreach (var (key, k) in _kits)
                if (!k.Empty)
                    root.AddChild(new MeshInstance3D { Name = $"CapFar_{key.Item1}_{key.Item2}", Mesh = k.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
    }

    private const float FarEdge = 1500f;

    private static void BuildSkyline(Ctx c)
    {
        var far = new FarChunks();
        var ground = Mats.Concrete(new Color(0.22f, 0.21f, 0.2f), 1611);
        // Sol lointain (sous les dalles de l'aire de jeu) de part et d'autre du fleuve.
        foreach (var (z0, z1) in new[] { (-FarEdge, QuayN), (QuayS, FarEdge) })
            for (var x = -FarEdge; x < FarEdge; x += 300f)
            for (var z = z0; z < z1 - 0.01f; z += 300f)
            {
                var ze = Mathf.Min(z1, z + 300f);
                var k = far.At(new Vector3(x + 150f, 0, (z + ze) / 2));
                k.Col = Colors.White;
                k.OrientedQuad(ground, new Vector3(x, -0.04f, z), new Vector3(x + 300f, -0.04f, z), new Vector3(x + 300f, -0.04f, ze), new Vector3(x, -0.04f, ze), Vector3.Up);
            }
        // Fleuve lointain : eau, berges, murs de quai, ponts.
        var water = Mats.CanalWater(c.Time);
        var stone = Mats.Ashlar();
        foreach (var (xa, xb) in new[] { (-FarEdge, -262f), (262f, FarEdge) })
            for (var x = xa; x < xb - 0.01f; x += 150f)
            {
                var xe = Mathf.Min(xb, x + 150f);
                var k = far.At(new Vector3((x + xe) / 2, 0, 106f));
                k.Col = Colors.Black;
                k.OrientedQuad(water, new Vector3(x, WaterY, BergeN), new Vector3(xe, WaterY, BergeN), new Vector3(xe, WaterY, BergeS), new Vector3(x, WaterY, BergeS), Vector3.Up);
                k.Col = StoneCol * 0.7f;
                k.OrientedQuad(Mats.Cobbles(), new Vector3(x, BergeY, QuayN), new Vector3(xe, BergeY, QuayN), new Vector3(xe, BergeY, BergeN), new Vector3(x, BergeY, BergeN), Vector3.Up);
                k.OrientedQuad(Mats.Cobbles(), new Vector3(x, BergeY, BergeS), new Vector3(xe, BergeY, BergeS), new Vector3(xe, BergeY, QuayS), new Vector3(x, BergeY, QuayS), Vector3.Up);
                k.Col = StoneCol * 0.8f;
                k.OrientedQuad(stone, new Vector3(x, BergeY, QuayN), new Vector3(xe, BergeY, QuayN), new Vector3(xe, 1f, QuayN), new Vector3(x, 1f, QuayN), Vector3.Back);
                k.OrientedQuad(stone, new Vector3(x, BergeY, QuayS), new Vector3(xe, BergeY, QuayS), new Vector3(xe, 1f, QuayS), new Vector3(x, 1f, QuayS), Vector3.Forward);
                k.OrientedQuad(stone, new Vector3(x, WaterY - 1f, BergeN), new Vector3(xe, WaterY - 1f, BergeN), new Vector3(xe, BergeY, BergeN), new Vector3(x, BergeY, BergeN), Vector3.Back);
                k.OrientedQuad(stone, new Vector3(x, WaterY - 1f, BergeS), new Vector3(xe, WaterY - 1f, BergeS), new Vector3(xe, BergeY, BergeS), new Vector3(x, BergeY, BergeS), Vector3.Forward);
            }
        foreach (var bx in new[] { -420f, -700f, 380f, 640f, 980f })
        {
            var k = far.At(new Vector3(bx, 0, 106f));
            var half = bx == -700f ? 6f : 12f;
            foreach (var (zc, ah, sy) in new[] { (95.5f, 9.5f, BergeY), (106f, 4.5f, WaterY + 0.6f), (116.5f, 9.5f, BergeY) })
                ArchFill(k, stone, new Vector3(bx, sy, zc), Vector3.Back, Vector3.Right, ah, -0.9f - sy, -sy, half * 2f, 8, StoneCol * 0.8f);
            k.BoxMinMax(stone, new Vector3(bx - half, WaterY - 1f, 100.5f), new Vector3(bx + half, 0f, 101.5f));
            k.BoxMinMax(stone, new Vector3(bx - half, WaterY - 1f, 110.5f), new Vector3(bx + half, 0f, 111.5f));
            k.BoxMinMax(stone, new Vector3(bx - half, -0.9f, QuayN), new Vector3(bx + half, 1.1f, QuayS));
        }
        FarBlocks(c, far);
        Basilica(far, new Vector3(-180f, 0, -860f));
        FarDome(far, new Vector3(-460f, 0, 560f));
        LatticeTower(far, new Vector3(560f, 0, 470f));
        far.Flush(c.Root);
    }

    /// <summary>Îlots lointains : volumes à façade haussmannienne, comble de zinc, cheminées ; quelques carcasses noircies.</summary>
    private static void FarBlocks(Ctx c, FarChunks far)
    {
        var facade = Mats.HaussmannFacade(c.Night);
        var zinc = Mats.Zinc();
        var burnt = Mats.Charred();
        const float pitch = 74f, street = 16f;
        for (var gx = -14; gx <= 14; gx++)
        for (var gz = -14; gz <= 14; gz++)
        {
            var cx = gx * pitch + 37f;
            var cz = gz * pitch + 37f;
            var w = pitch - street;
            // Hors de l'aire de jeu et de son premier rang, hors du fleuve, de la colline et de la tour.
            if (Mathf.Abs(cx) < 262f + w / 2 && cz > -262f - w / 2 && cz < 262f + w / 2) continue;
            if (cz + w / 2 > QuayN - 8f && cz - w / 2 < QuayS + 8f) continue;
            if (new Vector2(cx + 180f, cz + 860f).Length() < 300f) continue;
            if (new Vector2(cx - 560f, cz - 470f).Length() < 90f || new Vector2(cx + 460f, cz - 560f).Length() < 80f) continue;
            if (new Vector2(cx, cz).Length() > 1350f) continue;
            var h0 = RuinKit.Hash(cx, cz, 601);
            var floors = 5 + (int)(h0 * 2.5f);
            var hgt = Mats.ShopStoryH + Mats.StoryH * floors;
            var ruined = RuinKit.Hash(cx, cz, 602) < 0.14f;
            if (ruined) hgt *= 0.45f + RuinKit.Hash(cx, cz, 603) * 0.3f;
            var k = far.At(new Vector3(cx, 0, cz));
            var tint = StoneTint(new Vector3(cx, 0, cz)) * (ruined ? 0.45f : 0.92f);
            var hw = w / 2;
            var corners = new[] { new Vector3(cx - hw, 0, cz - hw), new Vector3(cx + hw, 0, cz - hw), new Vector3(cx + hw, 0, cz + hw), new Vector3(cx - hw, 0, cz + hw) };
            for (var i = 0; i < 4; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % 4];
                var n = ((a + b) / 2 - new Vector3(cx, 0, cz)).Normalized();
                var bays = Mathf.Round(w / Mats.BayW);
                var v1 = hgt / Mats.StoryH;
                k.QuadC(facade, a, b, b + Vector3.Up * hgt, a + Vector3.Up * hgt, new Vector2(0, v1), new Vector2(bays, v1), new Vector2(bays, 0), new Vector2(0, 0), tint, tint, tint, tint, n);
            }
            if (ruined)
            {
                k.Col = new Color(0.4f, 0.38f, 0.36f);
                k.OrientedQuad(burnt, corners[0] + Vector3.Up * hgt, corners[1] + Vector3.Up * hgt, corners[2] + Vector3.Up * hgt, corners[3] + Vector3.Up * hgt, Vector3.Up);
                continue;
            }
            k.Col = RuinKit.Hash(cx, cz, 604) < 0.3f ? new Color(0.36f, 0.38f, 0.42f) : Colors.White;
            k.Frustum(zinc, new Vector3(cx, hgt, cz), new Vector2(w + 0.6f, w + 0.6f), new Vector2(w - 9f, w - 9f), 4.4f);
            // Cour intérieure : creux sombre au centre du comble.
            k.Col = new Color(0.15f, 0.15f, 0.15f);
            k.OrientedQuad(zinc, new Vector3(cx - hw + 9f, hgt + 4.45f, cz - hw + 9f), new Vector3(cx + hw - 9f, hgt + 4.45f, cz - hw + 9f), new Vector3(cx + hw - 9f, hgt + 4.45f, cz + hw - 9f), new Vector3(cx - hw + 9f, hgt + 4.45f, cz + hw - 9f), Vector3.Up);
            k.Col = tint * 0.8f;
            for (var q = 0; q < 4; q++)
            {
                var px = cx + (RuinKit.Hash(cx, q, 605) - 0.5f) * (w - 10f);
                var pz = cz + (RuinKit.Hash(cz, q, 606) - 0.5f) * (w - 10f);
                k.Box(Mats.Ashlar(), new Vector3(px, hgt + 4.4f, pz), new Vector3(0.9f, 3f, 2.4f));
            }
        }
    }

    /// <summary>Colline et basilique blanche (trois coupoles, grand dôme, campanile).</summary>
    private static void Basilica(FarChunks far, Vector3 p)
    {
        var k = far.At(p);
        var hill = new List<Vector2>();
        for (var i = 0; i <= 10; i++)
        {
            var t = i / 10f;
            hill.Add(new Vector2(280f * (1 - t), 72f * Mathf.Sin(t * Mathf.Pi / 2)));
        }
        Lathe(k, Mats.GroundTint(), p + Vector3.Down * 0.1f, hill, 40, new Color(0.45f, 0.42f, 0.36f));
        var top = p + Vector3.Up * 72f;
        var white = StoneCol * 1.1f;
        var stone = Mats.Ashlar();
        k.Col = white;
        k.Box(stone, top + new Vector3(0, 12f, 8f), new Vector3(46f, 24f, 60f));
        Lathe(k, stone, top + new Vector3(0, 24f, 8f), new[] { new Vector2(13f, 0), new Vector2(13f, 14f), new Vector2(12f, 14f) }, 24, white);
        var dome = new List<Vector2>();
        for (var i = 0; i <= 8; i++)
        {
            var t = i / 8f * Mathf.Pi / 2;
            dome.Add(new Vector2(12f * Mathf.Cos(t), 14f + 20f * Mathf.Sin(t)));
        }
        Lathe(k, stone, top + new Vector3(0, 24f, 8f), dome, 24, white);
        Lathe(k, stone, top + new Vector3(0, 58f, 8f), new[] { new Vector2(2.5f, 0), new Vector2(2.5f, 5f), new Vector2(0, 9f) }, 12, white);
        foreach (var (dx, dz) in new[] { (-16f, -14f), (16f, -14f), (-16f, 30f), (16f, 30f) })
        {
            Lathe(k, stone, top + new Vector3(dx, 24f, dz), new[] { new Vector2(5f, 0), new Vector2(5f, 4f), new Vector2(3.5f, 10f), new Vector2(0, 13f) }, 16, white);
        }
        k.Box(stone, top + new Vector3(0, 30f, 46f), new Vector3(9f, 60f, 9f));
        Lathe(k, stone, top + new Vector3(0, 60f, 46f), new[] { new Vector2(5f, 0), new Vector2(4f, 6f), new Vector2(0, 12f) }, 12, white);
    }

    /// <summary>Dôme lointain sur la rive sud (tambour à colonnade, calotte de pierre, lanternon).</summary>
    private static void FarDome(FarChunks far, Vector3 p)
    {
        var k = far.At(p);
        var stone = Mats.Ashlar();
        var col = StoneCol * 0.9f;
        k.Col = col;
        k.Box(stone, p + Vector3.Up * 14f, new Vector3(80f, 28f, 80f));
        Lathe(k, stone, p + Vector3.Up * 28f, new[] { new Vector2(22f, 0), new Vector2(22f, 22f), new Vector2(20f, 23f) }, 32, col);
        var dome = new List<Vector2>();
        for (var i = 0; i <= 8; i++)
        {
            var t = i / 8f * Mathf.Pi / 2;
            dome.Add(new Vector2(20f * Mathf.Cos(t), 23f + 24f * Mathf.Sin(t)));
        }
        Lathe(k, Mats.Slate(), p + Vector3.Up * 28f, dome, 32, new Color(0.4f, 0.41f, 0.44f));
        Lathe(k, stone, p + Vector3.Up * 75f, new[] { new Vector2(4f, 0), new Vector2(4f, 8f), new Vector2(0, 14f) }, 12, col);
    }

    /// <summary>Tour de treillis de 300 m, brisée vers 190 m : la pointe pend sur le côté (piliers cintrés, croisillons, plateformes).</summary>
    private static void LatticeTower(FarChunks far, Vector3 p)
    {
        var k = far.At(p);
        var iron = Mats.Worn(new Color(0.24f, 0.2f, 0.17f), 0.75f, 0.5f);
        k.Col = Colors.White;
        float Half(float y) => y < 57f ? Mathf.Lerp(62f, 34f, y / 57f) : y < 115f ? Mathf.Lerp(34f, 16f, (y - 57f) / 58f) : Mathf.Lerp(16f, 4f, (y - 115f) / 75f);
        var levels = new List<float>();
        for (var y = 0f; y <= 190f; y += 9.5f) levels.Add(y);
        foreach (var (sx, sz) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            for (var i = 0; i + 1 < levels.Count; i++)
            {
                var y0 = levels[i];
                var y1 = levels[i + 1];
                var a = p + new Vector3(sx * Half(y0), y0, sz * Half(y0));
                var b = p + new Vector3(sx * Half(y1), y1, sz * Half(y1));
                var r = Mathf.Lerp(2.2f, 0.6f, y0 / 190f);
                k.Tube(iron, a, b, r, 5);
                // Croisillons vers les piliers voisins.
                var nb = p + new Vector3(-sz * Half(y1), y1, sx * Half(y1));
                var na = p + new Vector3(-sz * Half(y0), y0, sx * Half(y0));
                k.Tube(iron, a, nb, r * 0.35f, 4);
                k.Tube(iron, b, na, r * 0.35f, 4);
            }
        foreach (var y in new[] { 57f, 115f })
        {
            var h = Half(y) + 3f;
            k.Box(iron, p + Vector3.Up * y, new Vector3(h * 2f, 3f, h * 2f));
        }
        // Grandes arches entre les piliers au pied.
        foreach (var (n, along) in new[] { (Vector3.Back, Vector3.Right), (Vector3.Forward, Vector3.Right), (Vector3.Right, Vector3.Back), (Vector3.Left, Vector3.Back) })
        {
            var prev = p + n * 48f - along * 46f;
            for (var i = 1; i <= 8; i++)
            {
                var t = i / 8f;
                var q = p + n * 48f + along * Mathf.Lerp(-46f, 46f, t) + Vector3.Up * (Mathf.Sin(t * Mathf.Pi) * 30f + 8f);
                k.Tube(iron, prev, q, 1.1f, 5);
                prev = q;
            }
        }
        // Pointe brisée qui pend.
        var brk = p + Vector3.Up * 190f;
        var rot = new Basis(Vector3.Forward, 2.2f) * new Basis(Vector3.Up, 0.6f);
        for (var i = 0; i < 4; i++)
        {
            var a = brk + rot * new Vector3((i % 2 == 0 ? -1 : 1) * 3.5f, 0, (i < 2 ? -1 : 1) * 3.5f);
            var b = brk + rot * new Vector3((i % 2 == 0 ? -1 : 1) * 1f, 70f, (i < 2 ? -1 : 1) * 1f);
            k.Tube(iron, a, b, 0.6f, 4);
        }
    }

    // ───────────────────────── Sondes (outil de développement) ─────────────────────────

    /// <summary>
    /// « --capprobe » : des capsules de la taille d'un soldat suivent avec la vraie physique l'escalier de la station du
    /// métro aérien, une rampe des berges (descente), la travée écroulée du viaduc (montée), l'emmarchement du parlement
    /// et le grand pont ; journal « [probe] … ok » (sinon BLOQUÉ et la position).
    /// </summary>
    private static void AttachProbes(Ctx c)
    {
        if (Screenshot.CmdArg("capprobe") == null) return;
        var sx0 = StationX1 + 32f;
        var zs = BlvdZ0 + 3.5f;
        var top = new Vector3(CollapseX0 + 1.2f, ViaductTop, ViaductZ);
        var walkers = new List<BizWalker>
        {
            new() { Label = "escalier de la station (quai)", GoalY = ViaductTop + 0.25f, Path = new List<Vector3> { new(sx0 + 3f, 0, zs), new(sx0 + 1f, 0, zs), new(StationX1 - 0.5f, 0, zs), new(StationX1 - 1f, 0, MedianZ0 - 2f) } },
            new() { Label = "rampe de la berge nord", GoalY = BergeY, Path = new List<Vector3> { new(Ramps[0].x - 3f, 0, QuayN - 2f), new(Ramps[0].x - 0.5f, 0, QuayN - 1f), new(Ramps[0].x + 1f, 0, QuayN + 1.8f), new(Ramps[0].x + RampLen + 2f, 0, QuayN + 1.8f), new(Ramps[0].x + RampLen + 4f, 0, QuayN + 5f) } },
            new() { Label = "travée écroulée du viaduc", GoalY = ViaductTop - 0.4f, Path = new List<Vector3> { new(CollapseX1 + 12f, 0, ViaductZ + 1.2f), new(CollapseX1 + 5f, 0, ViaductZ + 1.2f), new(top.X + 1.5f, 0, ViaductZ + 0.6f), new(top.X - 3f, 0, ViaductZ - 1.8f) } },
            new() { Label = "emmarchement du parlement", GoalY = 3.2f + Walk, Path = new List<Vector3> { new(8f, 0, 130f), new(8f, 0, 134f), new(8f, 0, 146f) } },
            new() { Label = "grand pont (rive sud)", GoalY = Walk, Path = new List<Vector3> { new(-6f, 0, 64f), new(-6f, 0, 90f), new(-6f, 0, 128f), new(-6f, 0, 133f) } },
        };
        foreach (var w in walkers)
        {
            w.GlobalPositionOnReady = w.Path[0] + Vector3.Up * 1.2f;
            c.Root.AddChild(w);
        }
    }
}
