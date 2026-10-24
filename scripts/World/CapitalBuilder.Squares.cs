using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Grands espaces de la capitale : arc de triomphe sur son rond-point (arcades, frise, attique écorné, quadrige de
/// bronze renversé), grande place pavée (obélisque légèrement penché, deux fontaines à vasques dont une brisée,
/// candélabres, balustrade côté fleuve, kiosques), jardin aux arbres morts (allées de gravier, quinconces, grand
/// bassin croupi, statues, grilles, batterie d'artillerie abandonnée).
/// </summary>
public static partial class CapitalBuilder
{
    // ───────────────────────── Arc de triomphe ─────────────────────────

    private static void BuildArch(Ctx c)
    {
        var stone = Mats.Ashlar();
        var col = StoneCol * 0.9f;
        // Îlot pavé du rond-point (solide, 0,15 m).
        {
            var k = c.Solid.At(ArchCenter);
            k.Col = Colors.White;
            k.Cylinder(Mats.Cobbles(), ArchCenter, ArchIsland, ArchIsland, Walk, 48);
            k.Col = Colors.White;
            k.Cylinder(Mats.Curb(), ArchCenter, ArchIsland + 0.3f, ArchIsland + 0.3f, Walk + 0.005f, 48, false);
            c.Raised.Add(new Rect2(ArchCenter.X - 21f, ArchCenter.Z - 21f, 42f, 42f));
        }
        const float hx = 22.5f, hz = 11f, mainHalf = 7.5f, mainSpring = 21.5f, sideHalf = 4.25f, sideSpring = 14.25f, corn = 38f, top = 50f;
        var origin = ArchCenter + Vector3.Up * Walk;
        var k2 = new MeshKit { Record = true };
        foreach (var sx in new[] { -1f, 1f })
        {
            var xa = Mathf.Min(sx * mainHalf, sx * hx);
            var xb = Mathf.Max(sx * mainHalf, sx * hx);
            k2.Col = col;
            Shell(k2, stone, new Vector3(xa, 0, -hz), new Vector3(xb, corn, -sideHalf), false);
            Shell(k2, stone, new Vector3(xa, 0, sideHalf), new Vector3(xb, corn, hz), false);
            Shell(k2, stone, new Vector3(xa, sideHalf * 2f + sideSpring - sideHalf, -sideHalf), new Vector3(xb, corn, sideHalf), false);
            ArchFill(k2, stone, new Vector3(sx * (mainHalf + hx) / 2, sideSpring, 0), Vector3.Back, Vector3.Right, sideHalf, sideHalf, sideHalf, hx - mainHalf, 12, col);
            // Groupes sculptés devant les piles (socle, figure), panneaux en relief au-dessus.
            foreach (var sz in new[] { -1f, 1f })
            {
                var face = new Vector3(sx * 15f, 0, sz * (hz + 0.01f));
                var n = new Vector3(0, 0, sz);
                k2.Col = col * 1.05f;
                k2.Box(stone, face + n * 1.6f + Vector3.Up * 2.2f, new Vector3(7f, 4.4f, 3.2f));
                Statue(k2, stone, face + n * 1.6f + Vector3.Up * 4.4f, 9f, col * 1.05f, sz > 0 ? 0f : Mathf.Pi, sx > 0);
                Statue(k2, stone, face + n * 1.6f + Vector3.Right * 1.8f + Vector3.Up * 4.4f, 6.5f, col, sz > 0 ? 0.4f : Mathf.Pi - 0.4f);
                k2.Box(stone, face + n * 0.25f + Vector3.Up * 24f, new Vector3(11f, 6.5f, 0.5f));
                k2.Box(stone, face + n * 0.12f + Vector3.Up * 31.5f, new Vector3(14.4f, 0.6f, 0.24f));
            }
        }
        // Au-dessus de l'arche principale : massif, arche, frise, corniche, attique (angle nord-est écorné).
        k2.Col = col;
        Shell(k2, stone, new Vector3(-mainHalf, mainSpring + mainHalf, -hz), new Vector3(mainHalf, corn, hz), false);
        ArchFill(k2, stone, new Vector3(0, mainSpring, 0), Vector3.Right, Vector3.Back, mainHalf, mainHalf, mainHalf, hz * 2f, 16, col);
        k2.Col = col * 1.05f;
        k2.BoxMinMax(stone, new Vector3(-hx, 32.4f, -hz - 0.3f), new Vector3(hx, 35f, hz + 0.3f));
        k2.BoxMinMax(stone, new Vector3(-hx - 1f, corn, -hz - 1f), new Vector3(hx + 1f, corn + 1.5f, hz + 1f));
        k2.Col = col;
        Shell(k2, stone, new Vector3(-hx, corn + 1.5f, -hz), new Vector3(8f, top - 1f, hz));
        Shell(k2, stone, new Vector3(8f, corn + 1.5f, -hz), new Vector3(hx, top - 4.5f, 2f));
        Shell(k2, stone, new Vector3(8f, corn + 1.5f, 2f), new Vector3(16f, top - 1f, hz));
        k2.Col = col * 1.05f;
        k2.BoxMinMax(stone, new Vector3(-hx - 0.5f, top - 1f, -hz - 0.5f), new Vector3(8f, top, hz + 0.5f));
        // Écus de l'attique.
        for (var x = -hx + 2.5f; x < 7f; x += 3f)
            foreach (var sz in new[] { -1f, 1f })
                k2.Box(stone, new Vector3(x, corn + 5f, sz * (hz + 0.2f)), new Vector3(1.6f, 2f, 0.4f));
        // Quadrige de bronze renversé sur le bord de l'attique.
        var bronze = Mats.Bronze();
        var qp = new Vector3(-6f, top, 0);
        var qr = new Basis(Vector3.Back, 0.22f) * new Basis(Vector3.Up, 0.3f);
        k2.Col = Colors.White;
        k2.Box(bronze, qp + qr * new Vector3(0, 1.2f, 0), new Vector3(4f, 2.4f, 3f), qr);
        for (var i = 0; i < 4; i++)
        {
            var hp = qp + qr * new Vector3(-3f + i * 2f, 2.6f, 3.2f);
            k2.Box(bronze, hp, new Vector3(0.9f, 1.5f, 3.4f), qr);
            k2.Tube(bronze, hp + qr * new Vector3(0, 0.5f, 1.6f), hp + qr * new Vector3(0, 2.2f, 2.6f), 0.4f, 6);
            foreach (var (lx, lz) in new[] { (-0.35f, 1.2f), (0.35f, 1.2f), (-0.35f, -1.2f), (0.35f, -1.2f) })
                k2.Tube(bronze, hp + qr * new Vector3(lx, -0.5f, lz), hp + qr * new Vector3(lx, -2.6f, lz + (lz > 0 ? 0.6f : 0f)), 0.15f, 5);
        }
        Statue(k2, bronze, qp + qr * new Vector3(0, 2.4f, -0.6f), 4.2f, Colors.White, 0.3f, true);
        var b = AddBuilding(c, origin, new Vector3(hx * 2f, corn, hz * 2f), k2.Commit(), Mats.Ashlar(), 8f, null, open: true);
        _ = b;
        foreach (var sx in new[] { -1f, 1f })
        foreach (var sz in new[] { -1f, 1f })
            FlowBlock(c, origin + new Vector3(sx * 15f, 0, sz * 7.6f), new Vector3(15f, corn, 6.8f));
        // Gravats de l'angle écorné.
        var rk = c.Ruins;
        var rp = origin + new Vector3(20f, 0, -16f);
        rk.Convex(Props.RubblePile(rk.Kit(RuinKit.Layer.Solid, rp), rp, 0.6f, new Vector3(9f, 2.6f, 6f), 2101, StoneCol * 0.7f, false));
        c.HotSpots.Add((origin, 40f, 0.35f));
    }

    // ───────────────────────── Grande place ─────────────────────────

    private static void BuildPlaza(Ctx c)
    {
        Obelisk(c, new Vector3(0, Walk, -46f));
        Fountain(c, new Vector3(-50f, Walk, -4f), true);
        Fountain(c, new Vector3(50f, Walk, -4f), false);
        // Candélabres à trois lanternes en couronne sur le pourtour de la place.
        for (var x = -70f; x <= 70f; x += 20f)
            foreach (var z in new[] { -55f, 56f })
                PlazaLamp(c, new Vector3(x, Walk, z));
        for (var z = -35f; z <= 40f; z += 25f)
            foreach (var x in new[] { -71f, 71f })
                PlazaLamp(c, new Vector3(x, Walk, z));
        foreach (var (x, z) in new[] { (-7f, -52f), (7f, -52f), (-7f, -40f), (7f, -40f) })
            PlazaLamp(c, new Vector3(x, Walk, z));
        // Balustrade côté fleuve (ouverte dans l'axe du grand pont).
        var k = c.Solid.At(new Vector3(0, 0, 60f));
        foreach (var (x0, x1) in new[] { (-74f, -24f), (24f, 74f) })
            Balustrade(k, Mats.Ashlar(), new Vector3(x0, Walk, 60.6f), new Vector3(x1, Walk, 60.6f), 1.05f, StoneCol * 0.85f);
        c.KeepOut.Add(new Rect2(-75f, 60f, 150f, 1.2f));
        // Kiosques et colonnes d'affichage aux angles.
        foreach (var (x, z, yaw, kiosk) in new[] { (-66f, -50f, 0.4f, true), (66f, -50f, -0.4f, false), (-66f, 50f, 2.7f, false), (66f, 50f, -2.7f, true) })
        {
            var p = new Vector3(x, Walk, z);
            c.Props.Add(kiosk ? Props.Kiosk() : Props.MorrisColumn(), p, yaw, range: 240, shadows: true);
            c.Ruins.Take(p, 2f);
        }
    }

    /// <summary>Candélabre de la place (trois lanternes), éclairé la nuit une fois sur trois.</summary>
    private static void PlazaLamp(Ctx c, Vector3 p)
    {
        if (SpawnArea.Grow(2f).HasPoint(new Vector2(p.X, p.Z))) return;
        var lit = c.Night && RuinKit.Hash(p.X, p.Z, 501) < 0.35f && c.LitLamps < 24;
        var id = c.Props.Add(Props.CapitalLamp(lit, true), p, RuinKit.Hash(p.X, p.Z, 502) * Mathf.Tau, range: 300, shadows: true);
        if (lit)
        {
            c.LitLamps++;
            var light = new OmniLight3D { Position = p + Vector3.Up * 5f, LightColor = new Color(1f, 0.78f, 0.5f), LightEnergy = 2.5f, OmniRange = 16f, ShadowEnabled = false };
            c.Root.AddChild(light);
            Destructibles.AddExtra(id, light);
        }
        var cs = new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.28f, Height = 5.4f }, Position = p + Vector3.Up * 2.7f };
        if (id >= 0) Destructibles.AddShape(id, cs);
        else c.Body.AddChild(cs);
        c.Ruins.Take(p, 0.8f);
    }

    /// <summary>Obélisque de granit sur son socle à degrés, pyramidion doré ; légèrement penché par une explosion.</summary>
    private static void Obelisk(Ctx c, Vector3 p)
    {
        var k = c.Solid.At(p);
        var stone = Mats.Ashlar();
        var granite = new Color(0.78f, 0.62f, 0.58f);
        k.Col = StoneCol * 0.8f;
        k.Box(stone, p + Vector3.Up * 0.4f, new Vector3(9f, 0.8f, 9f));
        k.Box(stone, p + Vector3.Up * 1.2f, new Vector3(7.4f, 0.8f, 7.4f));
        k.Col = StoneCol * 0.9f;
        k.Box(stone, p + Vector3.Up * 4.1f, new Vector3(4.6f, 5f, 4.6f));
        k.Box(stone, p + Vector3.Up * 6.8f, new Vector3(5.2f, 0.5f, 5.2f));
        k.Col = Colors.White;
        foreach (var (n, along) in new[] { (Vector3.Back, Vector3.Right), (Vector3.Forward, Vector3.Left), (Vector3.Right, Vector3.Forward), (Vector3.Left, Vector3.Back) })
            k.Box(Mats.Gilt(), p + n * 2.32f + Vector3.Up * 4.2f, (n.Abs() * 0.06f) + along.Abs() * 2.4f + Vector3.Up * 2.8f);
        var tilt = new Basis(Vector3.Forward, 0.035f) * new Basis(Vector3.Right, 0.02f);
        var b0 = p + Vector3.Up * 7.05f;
        k.Col = granite;
        k.Xf = new Transform3D(tilt, b0);
        k.Frustum(stone, Vector3.Zero, new Vector2(2.5f, 2.5f), new Vector2(1.6f, 1.6f), 22.5f);
        k.Col = Colors.White;
        k.Frustum(Mats.Gilt(), Vector3.Up * 22.5f, new Vector2(1.6f, 1.6f), new Vector2(0.02f, 0.02f), 2.2f);
        // Lignes d'inscriptions (relief non solide).
        k.Solid = false;
        k.Col = granite * 0.6f;
        foreach (var (n, along) in new[] { (Vector3.Back, Vector3.Right), (Vector3.Forward, Vector3.Right), (Vector3.Right, Vector3.Back), (Vector3.Left, Vector3.Back) })
            for (var y = 2f; y < 20f; y += 1.4f)
            {
                var w = Mathf.Lerp(2.5f, 1.6f, y / 22.5f) / 2f;
                k.Box(stone, n * (w + 0.01f) + Vector3.Up * y, n.Abs() * 0.04f + along.Abs() * 0.5f + Vector3.Up * 0.9f);
            }
        k.Solid = true;
        k.Xf = Transform3D.Identity;
        c.Ruins.Block(p, 5f, 5f);
        c.KeepOut.Add(new Rect2(p.X - 4.5f, p.Z - 4.5f, 9f, 9f));
        FlowBlock(c, p, new Vector3(9f, 30f, 9f));
        c.Ruins.Decal(Mats.Decal.Scorch, p + new Vector3(5f, 0, 3f), 12f, 0.7f, Walk + 0.025f);
    }

    /// <summary>Fontaine monumentale : bassin circulaire à margelle, eau croupie, vasques superposées, divinités de bronze.</summary>
    private static void Fountain(Ctx c, Vector3 p, bool broken)
    {
        var k = c.Solid.At(p);
        var stone = Mats.Ashlar();
        var col = StoneCol * 0.85f;
        const float r = 10f;
        Lathe(k, stone, p, new[] { new Vector2(r, 0), new Vector2(r, 0.75f), new Vector2(r + 0.25f, 0.8f), new Vector2(r + 0.25f, 0.95f), new Vector2(r - 0.6f, 0.95f), new Vector2(r - 0.6f, 0.2f), new Vector2(0, 0.2f) }, 40, col);
        k.Solid = false;
        k.Col = new Color(0.6f, 0, 0);
        for (var i = 0; i < 40; i++)
        {
            var a0 = i * Mathf.Tau / 40;
            var a1 = (i + 1) * Mathf.Tau / 40;
            k.Tri(Mats.CanalWater(c.Time), p + Vector3.Up * 0.55f, p + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * (r - 0.6f) + Vector3.Up * 0.55f,
                p + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * (r - 0.6f) + Vector3.Up * 0.55f, p);
        }
        k.Solid = true;
        // Fût central et vasques (la seconde gît dans le bassin si la fontaine est brisée).
        Lathe(k, stone, p, new[] { new Vector2(1.6f, 0.2f), new Vector2(1.4f, 1.2f), new Vector2(0.9f, 1.6f), new Vector2(0.8f, 3.4f) }, 16, col);
        Lathe(k, stone, p + Vector3.Up * 3.4f, new[] { new Vector2(0.8f, 0), new Vector2(3.8f, 0.6f), new Vector2(4.1f, 1.0f), new Vector2(3.6f, 1.05f), new Vector2(0.6f, 0.7f), new Vector2(0, 0.7f) }, 24, col);
        var bronze = Mats.Bronze();
        if (!broken)
        {
            Lathe(k, stone, p + Vector3.Up * 4.4f, new[] { new Vector2(0.55f, 0), new Vector2(0.45f, 2.4f) }, 12, col);
            Lathe(k, stone, p + Vector3.Up * 6.8f, new[] { new Vector2(0.4f, 0), new Vector2(1.8f, 0.4f), new Vector2(1.9f, 0.6f), new Vector2(0, 0.5f) }, 16, col);
            k.Col = Colors.White;
            Statue(k, bronze, p + Vector3.Up * 7.3f, 2.2f, Colors.White, 0.4f);
        }
        else
        {
            // Vasque supérieure tombée de travers dans le bassin.
            k.Xf = new Transform3D(new Basis(Vector3.Right, 0.5f) * new Basis(Vector3.Forward, 0.2f), p + new Vector3(4.6f, 0.4f, 3.4f));
            Lathe(k, stone, Vector3.Zero, new[] { new Vector2(0.4f, 0), new Vector2(1.8f, 0.4f), new Vector2(1.9f, 0.6f), new Vector2(0, 0.5f) }, 16, col);
            k.Xf = Transform3D.Identity;
            c.Ruins.Decal(Mats.Decal.Crater, p + new Vector3(-6f, 0, -12f), 9f, 1.1f, Walk + 0.03f);
        }
        k.Col = Colors.White;
        for (var i = 0; i < 4; i++)
        {
            var a = i * Mathf.Tau / 4 + 0.785f;
            var sp = p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3.2f;
            k.Col = col;
            k.Box(stone, sp + Vector3.Up * 0.8f, new Vector3(1.4f, 1.2f, 1.4f));
            k.Col = Colors.White;
            Statue(k, bronze, sp + Vector3.Up * 1.4f, 2.6f, Colors.White, -a + Mathf.Pi / 2);
        }
        c.Ruins.Block(p, r + 1f, r + 1f);
        c.KeepOut.Add(new Rect2(p.X - r, p.Z - r, r * 2f, r * 2f));
        FlowBlock(c, p, new Vector3(r * 2f, 1f, r * 2f), 0f);
    }

    // ───────────────────────── Jardin ─────────────────────────

    private const float ParkX0 = -230f, ParkX1 = -88f, ParkZ0 = -60f, ParkZ1 = 62f;

    private static void BuildPark(Ctx c)
    {
        var gravel = Mats.Gravel();
        var g = c.Ground;
        // Allées de gravier : grande allée est-ouest, deux allées nord-sud, pourtour du bassin.
        void Alley(float x0, float z0, float x1, float z1)
        {
            for (var x = x0; x < x1 - 0.01f; x += 32f)
            {
                var xe = Mathf.Min(x1, x + 32f);
                var k = g.At(new Vector3((x + xe) / 2, 0, (z0 + z1) / 2));
                k.Col = new Color(0.78f, 0.74f, 0.66f);
                k.OrientedQuad(gravel, new Vector3(x, Walk + 0.012f, z0), new Vector3(xe, Walk + 0.012f, z0), new Vector3(xe, Walk + 0.012f, z1), new Vector3(x, Walk + 0.012f, z1), Vector3.Up);
            }
        }
        Alley(ParkX0 - 30f, -5f, ParkX1, 5f);
        foreach (var ax in new[] { -195f, -125f })
            for (var z = ParkZ0; z < ParkZ1; z += 30f)
                Alley(ax - 4f, z, ax + 4f, Mathf.Min(ParkZ1, z + 30f));
        Alley(ParkX0 - 30f, ParkZ0 + 2f, ParkX1, ParkZ0 + 8f);
        Alley(ParkX0 - 30f, ParkZ1 - 8f, ParkX1, ParkZ1 - 2f);
        // Grand bassin octogonal croupi au croisement central.
        Basin(c, new Vector3(-160f, Walk, 0f));
        // Alignements d'arbres morts le long des allées, quinconces dans les carrés.
        var deco = new RandomNumberGenerator { Seed = 5151 };
        void Tree(Vector3 p)
        {
            if (!c.Ruins.Free(p, 1.5f) || !Clear(c, p, 1f)) return;
            c.Ruins.Take(p, 1.2f);
            var v = deco.RandiRange(0, 3);
            var fate = RuinKit.Hash(p.X, p.Z, 511);
            if (fate < 0.12f)
            {
                // Arbre abattu par le souffle, couché dans l'herbe.
                var fall = deco.RandfRange(0, Mathf.Tau);
                var basis = new Basis(Vector3.Up, fall) * new Basis(Vector3.Right, Mathf.Pi / 2 - 0.08f);
                c.Ruins.Inst(Props.DeadTree(v), new Transform3D(basis, p + Vector3.Up * 0.3f), true, 300f);
                c.Ruins.Box(new Transform3D(new Basis(Vector3.Up, fall), p + new Basis(Vector3.Up, fall) * new Vector3(0, 0.35f, 2.6f)), new Vector3(0.6f, 0.6f, 5.2f));
                return;
            }
            c.Props.Add(Props.DeadTree(fate < 0.4f ? 2 + (v % 2) : v), p, deco.RandfRange(0, Mathf.Tau), null, 340, true, deco.RandfRange(0.85f, 1.15f));
        }
        for (var x = ParkX1 - 6f; x > ParkX0 - 10f; x -= 8f)
            foreach (var z in new[] { -8.5f, 8.5f, ParkZ0 + 11f, ParkZ1 - 11f })
                Tree(new Vector3(x + deco.RandfRange(-0.4f, 0.4f), Walk, z));
        foreach (var ax in new[] { -195f, -125f })
            for (var z = ParkZ0 + 14f; z < ParkZ1 - 12f; z += 8f)
                foreach (var s in new[] { -7f, 7f })
                    if (Mathf.Abs(z) > 12f) Tree(new Vector3(ax + s, Walk, z));
        foreach (var (cx, cz) in new[] { (-160f, -32f), (-160f, 34f), (-108f, -32f), (-108f, 34f) })
            for (var i = -2; i <= 2; i++)
            for (var j = -1; j <= 1; j++)
                Tree(new Vector3(cx + i * 7f + deco.RandfRange(-0.6f, 0.6f), Walk, cz + j * 7f + deco.RandfRange(-0.6f, 0.6f)));
        // Statues sur piédestal aux croisements d'allées.
        foreach (var (x, z) in new[] { (-125f, -15f), (-125f, 15f), (-195f, -15f), (-195f, 15f), (-92f, -12f), (-92f, 12f) })
            ParkStatue(c, new Vector3(x, Walk, z));
        // Grille de fonte sur muret le long de la rue (x = -88) et des rues nord et sud, ouverte aux allées.
        ParkFence(c, new Vector3(ParkX1, Walk, ParkZ0), new Vector3(ParkX1, Walk, -6f));
        ParkFence(c, new Vector3(ParkX1, Walk, 6f), new Vector3(ParkX1, Walk, ParkZ1));
        ParkFence(c, new Vector3(ParkX1, Walk, ParkZ0), new Vector3(ParkX0 - 30f, Walk, ParkZ0));
        ParkFence(c, new Vector3(ParkX1, Walk, ParkZ1), new Vector3(ParkX0 - 30f, Walk, ParkZ1));
        Battery(c, new Vector3(-118f, Walk, 34f));
    }

    private static void Basin(Ctx c, Vector3 p)
    {
        var k = c.Solid.At(p);
        var stone = Mats.Ashlar();
        const float r = 14f;
        Lathe(k, stone, p, new[] { new Vector2(r, 0), new Vector2(r, 0.5f), new Vector2(r - 0.7f, 0.5f), new Vector2(r - 0.7f, -0.4f), new Vector2(0, -0.4f) }, 8, StoneCol * 0.8f, Mathf.Pi / 8, Mathf.Tau + Mathf.Pi / 8);
        k.Solid = false;
        k.Col = new Color(0.3f, 0, 0);
        for (var i = 0; i < 8; i++)
        {
            var a0 = Mathf.Pi / 8 + i * Mathf.Tau / 8;
            var a1 = a0 + Mathf.Tau / 8;
            k.Tri(Mats.Sludge(), p + Vector3.Up * 0.05f, p + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * (r - 0.7f) + Vector3.Up * 0.05f, p + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * (r - 0.7f) + Vector3.Up * 0.05f, p + Vector3.Down);
        }
        k.Solid = true;
        c.Ruins.Block(p, r + 1f, r + 1f);
        c.KeepOut.Add(new Rect2(p.X - r, p.Z - r, r * 2f, r * 2f));
    }

    private static void ParkStatue(Ctx c, Vector3 p)
    {
        var k = c.Solid.At(p);
        k.Col = StoneCol * 0.85f;
        k.Box(Mats.Ashlar(), p + Vector3.Up * 1.1f, new Vector3(1.8f, 2.2f, 1.8f));
        k.Box(Mats.Ashlar(), p + Vector3.Up * 2.3f, new Vector3(2.1f, 0.25f, 2.1f));
        k.Col = Colors.White;
        if (RuinKit.Hash(p.X, p.Z, 521) < 0.4f) return; // statue arrachée de son socle
        Statue(k, Mats.Bronze(), p + Vector3.Up * 2.42f, 2.8f, Colors.White, RuinKit.Hash(p.X, p.Z, 522) * Mathf.Tau);
        c.Ruins.Block(p, 1.5f, 1.5f);
        c.KeepOut.Add(new Rect2(p.X - 1.2f, p.Z - 1.2f, 2.4f, 2.4f));
    }

    /// <summary>Grille de fonte à pointes sur un muret de pierre (solide), avec quelques brèches.</summary>
    private static void ParkFence(Ctx c, Vector3 a, Vector3 b)
    {
        var len = a.DistanceTo(b);
        var dir = (b - a) / len;
        var n = Mathf.CeilToInt(len / 6f);
        for (var i = 0; i < n; i++)
        {
            var p0 = a + dir * (len * i / n);
            var p1 = a + dir * (len * (i + 1) / n);
            if (RuinKit.Hash(p0.X, p0.Z, 531) < 0.15f) continue; // brèche
            var k = c.Solid.At((p0 + p1) / 2);
            var rot = new Basis(Vector3.Up, Mathf.Atan2(dir.X, dir.Z));
            k.Col = StoneCol * 0.8f;
            k.Box(Mats.Ashlar(), (p0 + p1) / 2 + Vector3.Up * 0.3f, new Vector3(0.5f, 0.6f, p0.DistanceTo(p1)), rot);
            k.Solid = false;
            k.Quad(Mats.IronRail(), p0 + Vector3.Up * 2.2f, p1 + Vector3.Up * 2.2f, p1 + Vector3.Up * 0.6f, p0 + Vector3.Up * 0.6f,
                new Vector2(0, 0), new Vector2(p0.DistanceTo(p1) * 0.7f, 0), new Vector2(p0.DistanceTo(p1) * 0.7f, 1), new Vector2(0, 1));
            k.Solid = true;
            k.Col = new Color(0.1f, 0.1f, 0.1f);
            k.Box(Mats.DetailMetal(), (p0 + p1) / 2 + Vector3.Up * 1.4f, new Vector3(0.08f, 1.6f, p0.DistanceTo(p1)), rot);
        }
    }

    /// <summary>Batterie d'artillerie abandonnée dans le jardin : épaulement de sacs de sable, obusier, caisses, filet.</summary>
    private static void Battery(Ctx c, Vector3 p)
    {
        var rk = c.Ruins;
        var k = rk.Kit(RuinKit.Layer.Solid, p);
        for (var i = 0; i < 6; i++)
        {
            var a0 = i * Mathf.Tau / 7f + 0.4f;
            var a1 = (i + 1) * Mathf.Tau / 7f + 0.4f;
            var (bc, bs, byaw) = Props.SandbagWall(k, p + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 5.5f, p + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 5.5f, 5, i * 7 + 3);
            rk.Box(new Transform3D(new Basis(Vector3.Up, byaw), bc), bs);
        }
        // Obusier : affût, bouclier, tube pointé vers le nord-est, roues.
        var metal = Mats.Worn(new Color(0.27f, 0.29f, 0.22f), 0.7f, 0.3f);
        var rot = new Basis(Vector3.Up, 2.4f);
        k.Col = Colors.White;
        k.Box(metal, p + rot * new Vector3(0, 0.9f, 0), new Vector3(1.6f, 0.6f, 2.2f), rot);
        k.Box(metal, p + rot * new Vector3(0, 1.5f, 0.9f), new Vector3(2.4f, 1.4f, 0.1f), rot);
        k.Tube(metal, p + rot * new Vector3(0, 1.4f, 0.4f), p + rot * new Vector3(0, 3.4f, 5.6f), 0.16f, 8, true);
        foreach (var sx in new[] { -1f, 1f })
        {
            k.Cylinder(metal, p + rot * new Vector3(sx * 1.1f, 0.6f, 0), 0.6f, 0.6f, 0.3f, 10, true, rot * new Basis(Vector3.Back, Mathf.Pi / 2));
            k.Tube(metal, p + rot * new Vector3(sx * 0.4f, 0.5f, -0.6f), p + rot * new Vector3(sx * 1.6f, 0.1f, -4f), 0.12f, 5);
        }
        rk.Box(new Transform3D(rot, p + rot * new Vector3(0, 1.1f, 0.6f)), new Vector3(2.4f, 2.2f, 3f));
        for (var i = 0; i < 3; i++)
        {
            var cp = p + new Vector3(-2.5f + i * 0.8f, 0, -3f);
            rk.Inst(Props.AmmoCrates(i % 2), RuinKit.At(cp, i * 0.4f), true, 200f);
        }
        rk.Decal(Mats.Decal.Scorch, p + new Vector3(3f, 0, 4f), 9f, 0.3f, Walk + 0.03f);
        rk.Take(p, 6.5f);
        c.KeepOut.Add(new Rect2(p.X - 6f, p.Z - 6f, 12f, 12f));
    }
}
