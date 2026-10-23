using Godot;

namespace Rempart;

/// <summary>Espaces publics de la banlieue : place de l'église, cimetière, parc, stade, parkings, vergers.</summary>
public static partial class SuburbBuilder
{
    /// <summary>Arbre d'alignement (feuillu de forêt réduit) avec grille d'arbre et tronc solide.</summary>
    private static void StreetTree(Ctx c, Vector3 p, int v, float scale, float yaw, bool grate)
    {
        var dead = DeadTreeAt(p);
        c.Props.Add(dead ?? Props.Broadleaf(v, true), p, yaw, null, 320, true, dead != null ? scale * 0.85f : scale);
        c.Body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.4f * scale, Height = 6f }, Position = p + Vector3.Up * 3f });
        if (!grate) return;
        var k = c.Ground.At(p);
        k.Col = new Color(0.12f, 0.12f, 0.12f);
        k.Box(Mats.DetailMetal(), p + Vector3.Up * 0.045f, new Vector3(1.6f, 0.02f, 1.6f), null, false);
    }

    private static void BuildSquare(Ctx c)
    {
        var r = new RandomNumberGenerator { Seed = 31 };
        var k = c.Ground.At(Vector3.Zero);
        k.Col = Colors.White;
        // Dallage de la place (sous le trottoir de la grande rue) et parvis de l'église.
        FlatRect(k, Mats.Sidewalk(), new Vector3(-25, 0, -24), new Vector3(25, 0, 31), 0.03f);
        FlatRect(k, Mats.Sidewalk(), new Vector3(-8, 0, -34), new Vector3(8, 0, -24), 0.03f);
        // Bande pavée plus sombre au milieu (axe de l'église).
        k.Col = new Color(0.5f, 0.48f, 0.45f);
        FlatRect(k, Mats.Gravel(), new Vector3(-2.2f, 0, -24), new Vector3(2.2f, 0, 2f), 0.034f);

        // Platanes en alignement sur les côtés est et ouest, bancs entre eux, lampadaires.
        var i = 0;
        for (var z = -16f; z <= 24f; z += 8f, i++)
        {
            foreach (var sx in new[] { -1f, 1f })
            {
                StreetTree(c, new Vector3(sx * 22.5f, 0, z), (i + (sx > 0 ? 2 : 0)) % 5, 0.72f, r.RandfRange(0, Mathf.Tau), true);
                if (z < 24f) c.Props.Add(Props.Bench(), new Vector3(sx * 20.6f, 0.03f, z + 4f), sx > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2, range: 140);
            }
        }
        foreach (var (x, z) in new[] { (-24f, -23f), (24f, -23f) })
        {
            c.Props.Add(Props.StreetLamp(c.Night), new Vector3(x, 0.03f, z), Mathf.Atan2(-x, -z), range: 240, shadows: true);
            c.Body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.15f, Height = 7f }, Position = new Vector3(x, 3.5f, z) });
        }
        c.Props.Add(Props.Bin(), new Vector3(-20.4f, 0.03f, -2f), Mathf.Pi / 2, range: 120);
        c.Props.Add(Props.Bin(), new Vector3(20.4f, 0.03f, 10f), -Mathf.Pi / 2, range: 120);

        // Monument aux morts : socle à degrés, obélisque, chaînes basses.
        var s = c.Solid.At(new Vector3(18, 0, -18));
        var mp = new Vector3(18f, 0.03f, -18f);
        s.Col = new Color(0.7f, 0.68f, 0.63f);
        s.Box(Mats.Masonry(), mp + Vector3.Up * 0.15f, new Vector3(3.2f, 0.3f, 3.2f));
        s.Box(Mats.Masonry(), mp + Vector3.Up * 0.45f, new Vector3(2.4f, 0.3f, 2.4f));
        s.Box(Mats.Masonry(), mp + Vector3.Up * 1.2f, new Vector3(1.4f, 1.2f, 1.4f));
        s.Col = new Color(0.78f, 0.76f, 0.7f);
        s.Frustum(Mats.Masonry(), mp + Vector3.Up * 1.8f, new Vector2(1.0f, 1.0f), new Vector2(0.45f, 0.45f), 4.2f);
        s.Frustum(Mats.Masonry(), mp + Vector3.Up * 6.0f, new Vector2(0.45f, 0.45f), new Vector2(0.02f, 0.02f), 0.5f);
        s.Col = new Color(0.65f, 0.55f, 0.3f);
        s.Box(Mats.DetailMetal(), mp + new Vector3(0, 1.3f, 0.71f), new Vector3(0.8f, 0.5f, 0.03f));
        s.Col = new Color(0.12f, 0.12f, 0.12f);
        foreach (var sx in new[] { -1f, 1f })
        foreach (var sz in new[] { -1f, 1f })
            s.Cylinder(Mats.DetailMetal(), mp + new Vector3(sx * 2.2f, 0, sz * 2.2f), 0.06f, 0.06f, 0.7f, 6);
        // Collision exacte : secteur solide (socle à degrés, obélisque, potelets).
    }

    private static void BuildCemetery(Ctx c)
    {
        var r = new RandomNumberGenerator { Seed = 41 };
        const float x0 = 10f, x1 = 26f, z0 = -79f, z1 = -31f;
        var s = c.Solid.At(new Vector3(18, 0, -55));
        var stone = Mats.Masonry();
        s.Col = new Color(0.74f, 0.7f, 0.62f);
        // Mur d'enceinte avec portail côté église.
        void Wall(Vector3 a, Vector3 b)
        {
            var len = a.DistanceTo(b);
            var alongX = Mathf.Abs(b.X - a.X) > 0.1f;
            var size = alongX ? new Vector3(len, 1.8f, 0.45f) : new Vector3(0.45f, 1.8f, len);
            s.Box(stone, (a + b) / 2 + Vector3.Up * 0.9f, size);
            s.Box(stone, (a + b) / 2 + Vector3.Up * 1.85f, size + new Vector3(alongX ? 0 : 0.15f, -1.7f, alongX ? 0.15f : 0));
        }
        Wall(new Vector3(x0, 0, z0), new Vector3(x1, 0, z0));
        Wall(new Vector3(x0, 0, z1), new Vector3(x1, 0, z1));
        Wall(new Vector3(x1, 0, z0), new Vector3(x1, 0, z1));
        Wall(new Vector3(x0, 0, z0), new Vector3(x0, 0, -57f));
        Wall(new Vector3(x0, 0, -53f), new Vector3(x0, 0, z1));
        s.Col = new Color(0.12f, 0.12f, 0.13f);
        foreach (var gz in new[] { -56.6f, -53.4f }) s.Box(stone, new Vector3(x0, 1.2f, gz), new Vector3(0.6f, 2.4f, 0.6f));
        var k = c.Ground.At(new Vector3(18, 0, -55));
        k.Col = new Color(0.66f, 0.62f, 0.54f);
        FlatRect(k, Mats.Gravel(), new Vector3(x0, 0, -56.2f), new Vector3(x1 - 0.4f, 0, -53.8f), 0.02f);
        FlatRect(k, Mats.Gravel(), new Vector3(17.2f, 0, z0 + 0.4f), new Vector3(18.8f, 0, z1 - 0.4f), 0.021f);
        // Tombes en rangées : dalle, stèle, parfois une croix.
        for (var z = z0 + 2.2f; z < z1 - 1.5f; z += 2.6f)
        {
            if (Mathf.Abs(z + 55f) < 2.2f) continue;
            foreach (var side in new[] { -1f, 1f })
            for (var j = 0; j < 3; j++)
            {
                if (r.Randf() < 0.12f) continue;
                var gx = 18f + side * (1.8f + j * 2.1f);
                if (gx < x0 + 0.9f || gx > x1 - 0.9f) continue;
                var g = new Vector3(gx, 0, z);
                var tone = r.RandfRange(0.35f, 0.75f);
                s.Col = new Color(tone, tone * 0.98f, tone * 0.95f);
                s.Box(Mats.Detail(), g + Vector3.Up * 0.15f, new Vector3(0.95f, 0.3f, 1.9f));
                var hh = r.RandfRange(0.6f, 1.0f);
                var face = side > 0 ? -1f : 1f;
                s.Box(Mats.Detail(), g + new Vector3(-face * 0.42f, 0.3f + hh / 2, 0), new Vector3(0.14f, hh, 0.85f));
                if (r.Randf() < 0.35f)
                {
                    s.Box(Mats.Detail(), g + new Vector3(-face * 0.42f, 0.3f + hh + 0.3f, 0), new Vector3(0.08f, 0.6f, 0.08f));
                    s.Box(Mats.Detail(), g + new Vector3(-face * 0.42f, 0.3f + hh + 0.4f, 0), new Vector3(0.08f, 0.08f, 0.4f));
                }
                if (r.Randf() < 0.3f) c.Props.Add(Props.Shrub(3), g + Vector3.Up * 0.3f, r.RandfRange(0, Mathf.Tau), null, 100, false, 0.3f);
            }
        }
        // Ifs et cyprès aux angles.
        foreach (var p in new[] { new Vector3(x0 + 2, 0, z0 + 2), new Vector3(x1 - 2, 0, z0 + 2), new Vector3(x1 - 2, 0, z1 - 2), new Vector3(x0 + 2, 0, z1 - 2) })
            c.Props.Add(Props.Conifer(r.RandiRange(0, 3)), p, r.RandfRange(0, Mathf.Tau), null, 320, true, 0.42f);
        // Jardin du presbytère, à l'ouest de l'église.
        for (var i = 0; i < 4; i++)
            GardenTree(c, r, new Vector3(r.RandfRange(-27f, -12f), 0, r.RandfRange(-78f, -36f)));
    }

    private static void BuildPark(Ctx c)
    {
        var r = new RandomNumberGenerator { Seed = 51 };
        var k = c.Ground.At(new Vector3(0, 0, -118));
        k.Col = new Color(0.68f, 0.63f, 0.52f);
        // Allées stabilisées en croix.
        FlatRect(k, Mats.Gravel(), new Vector3(-30, 0, -119.5f), new Vector3(26, 0, -116.5f), 0.02f);
        FlatRect(k, Mats.Gravel(), new Vector3(-3.5f, 0, -140), new Vector3(-0.5f, 0, -95), 0.021f);
        // Aire de jeux et bancs.
        c.Props.Add(Props.SwingSet(), new Vector3(16f, 0, -106f), 0, range: 160, shadows: true);
        c.Props.Add(Props.SwingSet(), new Vector3(16f, 0, -101f), 0, range: 160, shadows: true);
        foreach (var x in new[] { -22f, -12f, 8f, 18f })
        {
            c.Props.Add(Props.Bench(), new Vector3(x, 0.02f, -121.2f), 0, range: 140);
            c.Props.Add(Props.Bench(), new Vector3(x, 0.02f, -114.8f), Mathf.Pi, range: 140);
        }
        // Bosquets sur les bords (le centre reste dégagé pour la zone d'apparition nord).
        for (var i = 0; i < 14; i++)
        {
            var edge = r.RandiRange(0, 2);
            var p = edge switch
            {
                0 => new Vector3(r.RandfRange(-29, -18), 0, r.RandfRange(-139, -97)),
                1 => new Vector3(r.RandfRange(14, 25), 0, r.RandfRange(-139, -110)),
                _ => new Vector3(r.RandfRange(-28, 24), 0, r.RandfRange(-99, -96)),
            };
            StreetTree(c, p, r.RandiRange(0, 4), r.RandfRange(0.6f, 0.9f), r.RandfRange(0, Mathf.Tau), false);
        }
        for (var i = 0; i < 10; i++)
            c.Props.Add(Props.Shrub(r.RandiRange(0, 2)), new Vector3(r.RandfRange(-29, 25), 0, r.RandfRange(-139, -96)), r.RandfRange(0, Mathf.Tau), null, 140, false, r.RandfRange(0.8f, 1.5f));

        // Square de quartier à l'est (zone d'apparition est).
        c.Props.Add(Props.SwingSet(), new Vector3(136f, 0, -33f), Mathf.Pi / 2, range: 160, shadows: true);
        c.Props.Add(Props.Bench(), new Vector3(121f, 0.02f, -25f), Mathf.Pi / 2, range: 140);
        c.Props.Add(Props.Bench(), new Vector3(121f, 0.02f, -19f), Mathf.Pi / 2, range: 140);
        StreetTree(c, new Vector3(122f, 0, -34f), 1, 0.8f, 0.4f, false);
        StreetTree(c, new Vector3(137f, 0, -16f), 3, 0.75f, 1.4f, false);
    }

    private static void BuildPitch(Ctx c)
    {
        var k = c.Ground.At(new Vector3(-120, 0, 131));
        const float cx = -120f, cz = 131f, hl = 32f, hw = 20f;
        // Gazon tondu en bandes alternées.
        for (var i = 0; i < 16; i++)
        {
            var x0 = cx - hl + i * (hl * 2 / 16);
            k.Col = i % 2 == 0 ? new Color(0.36f, 0.4f, 0.26f) : new Color(0.31f, 0.35f, 0.23f);
            FlatRect(k, Mats.GroundTint(), new Vector3(x0, 0, cz - hw - 2), new Vector3(x0 + hl * 2 / 16, 0, cz + hw + 2), 0.012f);
        }
        var paint = Mats.RoadPaint();
        k.Col = Colors.White;
        void Line(float xa, float za, float xb, float zb) => FlatRect(k, paint, new Vector3(xa, 0, za), new Vector3(xb, 0, zb), 0.016f);
        Line(cx - hl, cz - hw, cx + hl, cz - hw + 0.12f);
        Line(cx - hl, cz + hw - 0.12f, cx + hl, cz + hw);
        Line(cx - hl, cz - hw, cx - hl + 0.12f, cz + hw);
        Line(cx + hl - 0.12f, cz - hw, cx + hl, cz + hw);
        Line(cx - 0.06f, cz - hw, cx + 0.06f, cz + hw);
        foreach (var sg in new[] { -1f, 1f })
        {
            var gx = cx + sg * hl;
            Line(Mathf.Min(gx, gx - sg * 13f), cz - 12f, Mathf.Max(gx, gx - sg * 13f), cz - 11.88f);
            Line(Mathf.Min(gx, gx - sg * 13f), cz + 11.88f, Mathf.Max(gx, gx - sg * 13f), cz + 12f);
            Line(gx - sg * 13f - 0.06f, cz - 12f, gx - sg * 13f + 0.06f, cz + 12f);
            c.Props.Add(Props.FootballGoal(), new Vector3(gx, 0, cz), sg > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2, range: 220, shadows: true);
        }
        // Rond central (segments).
        for (var i = 0; i < 28; i++)
        {
            var a0 = i * Mathf.Tau / 28;
            var a1 = (i + 1) * Mathf.Tau / 28;
            var p0 = new Vector3(cx + Mathf.Cos(a0) * 7f, 0.016f, cz + Mathf.Sin(a0) * 7f);
            var p1 = new Vector3(cx + Mathf.Cos(a1) * 7f, 0.016f, cz + Mathf.Sin(a1) * 7f);
            var q0 = new Vector3(cx + Mathf.Cos(a0) * 7.12f, 0.016f, cz + Mathf.Sin(a0) * 7.12f);
            var q1 = new Vector3(cx + Mathf.Cos(a1) * 7.12f, 0.016f, cz + Mathf.Sin(a1) * 7.12f);
            QuadUp(k, paint, p0, p1, q1, q0);
        }
        // Main courante autour du terrain (sans collision : on l'enjambe).
        var m = Mats.DetailMetal();
        k.Col = new Color(0.8f, 0.8f, 0.78f);
        var corners = new[] { new Vector3(cx - hl - 4, 0, cz - hw - 4), new Vector3(cx + hl + 4, 0, cz - hw - 4), new Vector3(cx + hl + 4, 0, cz + hw + 4), new Vector3(cx - hl - 4, 0, cz + hw + 4) };
        for (var i = 0; i < 4; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % 4];
            k.Tube(m, a + Vector3.Up * 1.0f, b + Vector3.Up * 1.0f, 0.04f, 5);
            var len = a.DistanceTo(b);
            for (var t = 0f; t <= len; t += 3f) k.Cylinder(m, a.Lerp(b, t / len), 0.035f, 0.035f, 1.0f, 5, false);
        }

        // Vergers et balles de foin dans les prés du sud.
        var r = new RandomNumberGenerator { Seed = 61 };
        for (var x = -84f; x < -26f; x += 9f)
        for (var z = 112f; z < 154f; z += 9f)
        {
            if (Mathf.Abs(x + 55) < 7 && Mathf.Abs(z - 128) < 7) continue; // centre de la zone sud dégagé
            if (Mathf.Abs(x + 60) < 6f) continue;                            // route du pont
            if (r.Randf() < 0.2f) continue;
            GardenTree(c, r, new Vector3(x + r.RandfRange(-1, 1), 0, z + r.RandfRange(-1, 1)));
        }
        var s = c.Solid.At(new Vector3(40, 0, 132));
        for (var i = 0; i < 7; i++)
        {
            var p = new Vector3(r.RandfRange(18, 62), 0, r.RandfRange(112, 152));
            if (p.DistanceTo(new Vector3(40, 0, 132)) < 8) continue;
            var yaw = r.RandfRange(0, Mathf.Tau);
            s.Col = new Color(0.72f, 0.62f, 0.36f);
            s.Cylinder(Mats.Fabric(new Color(0.75f, 0.66f, 0.42f)), p + new Basis(Vector3.Up, yaw) * new Vector3(-0.6f, 0.75f, 0), 0.75f, 0.75f, 1.2f, 14, true, new Basis(Vector3.Up, yaw) * new Basis(Vector3.Back, -Mathf.Pi / 2));
        }
    }

    private static void BuildParking(Ctx c)
    {
        var r = new RandomNumberGenerator { Seed = 71 };
        var k = c.Ground.At(new Vector3(120, 0, 62));
        k.Col = Colors.White;
        // Parking de la supérette.
        FlatRect(k, Mats.Asphalt(), new Vector3(105.5f, 0, 44.5f), new Vector3(135f, 0, 80f), 0.02f);
        FlatRect(k, Mats.Asphalt(), new Vector3(135f, 0, 74f), new Vector3(171f, 0, 80f), 0.02f);
        k.Col = new Color(0.78f, 0.76f, 0.72f);
        FlatRect(k, Mats.Sidewalk(), new Vector3(133f, 0, 52f), new Vector3(135.5f, 0, 76f), 0.06f);
        var paint = Mats.RoadPaint();
        k.Col = Colors.White;
        foreach (var (z0, z1, yaw) in new[] { (47f, 52f, Mathf.Pi), (60f, 65f, 0f), (65f, 70f, Mathf.Pi), (75f, 80f, 0f) })
        {
            for (var x = 108f; x <= 131f; x += 2.5f) FlatRect(k, paint, new Vector3(x - 0.06f, 0, z0), new Vector3(x + 0.06f, 0, z1), 0.025f);
            for (var x = 109.25f; x < 131f; x += 2.5f)
            {
                if (r.Randf() > 0.62f) continue;
                AddCar(c, new Vector3(x, 0.02f, (z0 + z1) / 2), yaw + r.RandfRange(-0.05f, 0.05f), r.Randf() < 0.12f ? Props.VehicleType.Van : Props.VehicleType.Car, Pick(CarColors, r), r.RandfRange(4f, 4.6f), r);
            }
        }
        // Abri à chariots et lampadaires du parking.
        var s = c.Solid.At(new Vector3(128, 0, 56));
        s.Col = new Color(0.3f, 0.32f, 0.34f);
        s.Box(Mats.DetailMetal(), new Vector3(128f, 2.3f, 56f), new Vector3(6f, 0.1f, 2.6f));
        foreach (var x in new[] { 125.2f, 130.8f }) s.Box(Mats.DetailMetal(), new Vector3(x, 1.15f, 56f), new Vector3(0.1f, 2.3f, 0.1f));
        s.Col = new Color(0.6f, 0.62f, 0.64f);
        for (var i = 0; i < 6; i++) s.Box(Mats.DetailMetal(), new Vector3(125.8f + i * 0.85f, 0.6f, 56f), new Vector3(0.6f, 0.9f, 1.4f));
        foreach (var p in new[] { new Vector3(112f, 0, 56f), new Vector3(124f, 0, 72.5f) })
        {
            c.Props.Add(Props.StreetLamp(c.Night), p, 0, range: 240, shadows: true);
            c.Body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.15f, Height = 7f }, Position = p + Vector3.Up * 3.5f });
        }

        // Parvis et stationnement du gymnase.
        k = c.Ground.At(new Vector3(-110, 0, 45));
        k.Col = Colors.White;
        FlatRect(k, Mats.Sidewalk(), new Vector3(-118f, 0, 39f), new Vector3(-102f, 0, 50f), 0.04f);
        FlatRect(k, Mats.Asphalt(), new Vector3(-134f, 0, 39f), new Vector3(-118f, 0, 50f), 0.02f);
        FlatRect(k, Mats.Asphalt(), new Vector3(-102f, 0, 39f), new Vector3(-86f, 0, 50f), 0.02f);
        foreach (var (xa, xb) in new[] { (-133f, -119f), (-101f, -87f) })
            for (var x = xa; x <= xb; x += 2.5f)
            {
                FlatRect(k, paint, new Vector3(x - 0.06f, 0, 44.5f), new Vector3(x + 0.06f, 0, 49.5f), 0.025f);
                if (x + 1.25f < xb && r.Randf() < 0.5f)
                    AddCar(c, new Vector3(x + 1.25f, 0.02f, 47f), Mathf.Pi + r.RandfRange(-0.05f, 0.05f), Props.VehicleType.Car, Pick(CarColors, r), r.RandfRange(4f, 4.6f), r);
            }
        for (var x = -117f; x <= -103f; x += 1.6f) c.Props.Add(Props.Bollard(), new Vector3(x, 0.04f, 39.6f), 0, range: 110);
    }
}
