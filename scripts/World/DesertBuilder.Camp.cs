using Godot;

namespace Rempart;

/// <summary>
/// Cantonnement de la base : casernements préfabriqués et réfectoire (ouest), parc automobile (auvent ouvert à toit
/// praticable, atelier, véhicules garés destructibles), radôme sur socle (escalier jusqu'à la terrasse), pylône de
/// transmissions et son abri, hélisurfaces, postes de combat en sacs de sable autour de la place d'armes.
/// </summary>
public static partial class DesertBuilder
{
    private static readonly Vector3 MotorShed = new(88, 0, 24), MotorShedSize = new(50, 7, 14);
    private static readonly Vector3 Radar = new(34, 0, 106), RadarBase = new(18, 9, 18);
    private static readonly Vector3 Mast = new(92, 0, 112);
    /// <summary>Hélisurfaces : deux accueillent un hélicoptère pilotable, la troisième une épave.</summary>
    private static readonly Vector3[] Helipads = { new(-78, 0, 62), new(-50, 0, 112), new(-84, 0, 118) };
    public const float RadarDomeR = 7.6f;

    private static void BuildCamp(Ctx c)
    {
        // Casernements : deux colonnes de trois, puis le réfectoire.
        var n = 0;
        foreach (var z in new[] { -6f, 18f, 42f })
            foreach (var x in new[] { -178f, -132f })
            {
                var size = new Vector3(36, 4.2f, 10);
                AddBuilding(c, new Vector3(x, 0, z), size, BarracksMesh(size, n++), Mats.Corrugated(new Color(0.5f, 0.46f, 0.38f), 1801, 2.4f));
            }
        AddBuilding(c, new Vector3(-155, 0, 70), new Vector3(30, 5.2f, 13), BarracksMesh(new Vector3(30, 5.2f, 13), 9), Mats.Corrugated(new Color(0.47f, 0.45f, 0.4f), 1802, 2.4f));
        // Murs en T le long des casernements (protection contre les éclats).
        for (var i = 0; i < 3; i++)
        {
            var z = -6f + i * 24f + 7.2f;
            for (var x = -194f; x <= -116f; x += 1.55f)
            {
                if (x is > -159f and < -151f) continue; // passage entre les colonnes
                if (RuinKit.Hash(x, z, 91) < 0.1f) continue;
                c.Props.Add(Props.TWall((int)(RuinKit.Hash(x, z, 92) * 2)), new Vector3(x, 0, z), 0, range: 230f, shadows: true);
            }
        }

        // Parc automobile : auvent ouvert (toit praticable par l'escalier ouest), atelier fermé.
        AddOpen(c, MotorShed, MotorShedSize, MotorShedMesh(MotorShedSize), Mats.Corrugated(new Color(0.45f, 0.43f, 0.38f), 1803, 2.4f), 1.2f);
        FlowBlock(c, MotorShed + new Vector3(0, 0, MotorShedSize.Z / 2), new Vector3(MotorShedSize.X, MotorShedSize.Y, 1f), 0.6f);
        AddBuilding(c, new Vector3(128, 0, 26), new Vector3(18, 8, 16), WorkshopMesh(new Vector3(18, 8, 16)), Mats.Corrugated(new Color(0.44f, 0.42f, 0.37f), 1804, 2.4f));
        // Véhicules garés sous l'auvent et devant l'atelier (destructibles : ils brûlent puis explosent).
        for (var i = 0; i < 7; i++)
        {
            var x = MotorShed.X - 21f + i * 7f;
            var h = RuinKit.Hash(i, 3, 93);
            if (h < 0.15f) continue;
            if (h < 0.55f) Vehicle(c, Props.MilJeep(i % 2), new Vector3(x, 0, MotorShed.Z + 1.5f), Mathf.Pi + (h - 0.35f) * 0.1f, Props.VehicleType.Car);
            else Vehicle(c, Props.MilTruck(), new Vector3(x, 0, MotorShed.Z + 0.5f), Mathf.Pi + (h - 0.75f) * 0.08f, Props.VehicleType.Truck);
        }
        Vehicle(c, Props.MilTruck(), new Vector3(122, 0, 44), 0.5f, Props.VehicleType.Truck);
        Vehicle(c, Props.MilJeep(1), new Vector3(134, 0, 46), -0.3f, Props.VehicleType.Car);
        Vehicle(c, Props.MilJeep(0), new Vector3(60, 0, 60), 2.2f, Props.VehicleType.Car);
        // Pièces, fûts et caisses dans le parc.
        for (var i = 0; i < 16; i++)
        {
            var p = new Vector3(66 + RuinKit.Hash(i, 1, 94) * 70f, 0, 36 + RuinKit.Hash(i, 2, 94) * 6f);
            if (!c.Ruins.TryTake(p, 0.8f)) continue;
            if (i % 3 == 0) c.Props.Add(Props.AmmoCrates(i % 3), p, RuinKit.Hash(i, 3, 94) * Mathf.Tau, range: 150f, shadows: true);
            else c.Props.Add(Props.Drum(0), p, 0, range: 140f, shadows: true);
        }

        // Radôme : socle plein + dôme (forme exacte, escaladable), escalier jusqu'à la terrasse.
        AddBuilding(c, Radar, RadarBase, RadarMesh(RadarBase), Mats.Concrete(new Color(0.55f, 0.53f, 0.48f), 64), 1.2f);
        // Pylône de transmissions (ouvrage fixe) et son abri technique.
        {
            var k = c.Solid.At(Mast);
            k.Xf = new Transform3D(Basis.Identity, Mast);
            LatticeMast(k, 46f);
            k.Xf = Transform3D.Identity;
            FlowBlock(c, Mast, new Vector3(7f, 46f, 7f));
            Take(c, Mast, 5f, 5f);
        }
        AddBuilding(c, Mast + new Vector3(12, 0, -6), new Vector3(9, 3.4f, 5), ShelterBoxMesh(new Vector3(9, 3.4f, 5)), Mats.Concrete(new Color(0.5f, 0.48f, 0.44f), 65));

        // Hélisurfaces.
        foreach (var p in Helipads) Helipad(c, p);
        {
            var k = c.Solid.At(Helipads[2]);
            k.Xf = new Transform3D(new Basis(Vector3.Up, 2.3f), Helipads[2] + new Vector3(2, 0, -1));
            Props.HeliWreck(k);
            k.Xf = Transform3D.Identity;
            c.Ruins.Fire(Helipads[2] + new Vector3(2, 1.2f, -1), 1.4f);
            Decal(c, Mats.Decal.Scorch, Helipads[2], 12f, 0.7f, 0.03f);
            Take(c, Helipads[2], 9f, 9f, 0f);
        }

        // Postes de combat en sacs de sable autour de la place d'armes (hors de l'aire de départ).
        foreach (var (x, z, yaw) in new[] { (-46f, -8f, 0.3f), (46f, 6f, -0.2f), (-44f, 52f, 2.5f), (40f, 54f, 3.6f), (-20f, -48f, 0f), (24f, -46f, 0.1f) })
            SandbagPit(c, new Vector3(x, 0, z), yaw);
        // Murs en T autour du poste de commandement (tarmac), avec passages.
        for (var i = 0; i < 72; i++)
        {
            var a = i * Mathf.Tau / 72f;
            if (i % 18 < 3) continue; // quatre passages
            var p = new Vector3(0, 0, -62) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 17f;
            c.Props.Add(Props.TWall(i % 2), p, -a - Mathf.Pi / 2, range: 230f, shadows: true);
        }
    }

    /// <summary>Véhicule garé destructible (nœud visuel : l'explosion le remplace par son épave).</summary>
    private static void Vehicle(Ctx c, ArrayMesh mesh, Vector3 p, float yaw, Props.VehicleType type)
    {
        if (!c.Ruins.TryTake(p, type == Props.VehicleType.Truck ? 4f : 2.8f)) return;
        var node = new MeshInstance3D { Mesh = mesh, Position = p, Rotation = new Vector3(0, yaw, 0), VisibilityRangeEnd = 300f };
        c.Root.AddChild(node);
        CityBuilder.AddVehicleProp(c.Body, node, mesh, type, type == Props.VehicleType.Truck ? 7.8f : 4.9f, p, yaw, new Color(0.44f, 0.39f, 0.3f));
    }

    /// <summary>Poste de combat : sacs de sable en fer à cheval (ouvert vers l'arrière), caisses de munitions.</summary>
    private static void SandbagPit(Ctx c, Vector3 p, float yaw)
    {
        var r = c.Ruins;
        var k = r.Kit(RuinKit.Layer.Solid, p);
        var b = new Basis(Vector3.Up, yaw);
        Vector3 P(float x, float z) => p + b * new Vector3(x, 0, z);
        foreach (var (a, e) in new[] { (P(-3f, 1.5f), P(-1f, -2.6f)), (P(-1f, -2.6f), P(2.2f, -2.6f)), (P(2.2f, -2.6f), P(3.5f, 1.2f)) })
        {
            var (bc, bs, byaw) = Props.SandbagWall(k, a, e, 6, (int)(p.X * 3 + p.Z));
            r.Box(new Transform3D(new Basis(Vector3.Up, byaw), bc), bs);
        }
        c.Props.Add(Props.AmmoCrates(1), P(0.6f, 0.4f), yaw + 0.3f, range: 150f, shadows: true);
        r.Take(p, 4.2f);
    }

    /// <summary>Hélisurface : dalle circulaire, cercle et « H » peints, balises au pourtour.</summary>
    private static void Helipad(Ctx c, Vector3 p)
    {
        const float r = 11f;
        const int seg = 24;
        var k = c.Ground.At(p);
        var concrete = Mats.DesertApron();
        k.Col = Colors.White;
        for (var i = 0; i < seg; i++)
        {
            var a0 = i * Mathf.Tau / seg;
            var a1 = (i + 1) * Mathf.Tau / seg;
            k.Tri(concrete, p + Vector3.Up * 0.016f, p + new Vector3(Mathf.Cos(a0) * r, 0.016f, Mathf.Sin(a0) * r), p + new Vector3(Mathf.Cos(a1) * r, 0.016f, Mathf.Sin(a1) * r), p + Vector3.Down);
        }
        var paint = Mats.AirfieldPaint();
        k.Col = PaintWhite;
        for (var i = 0; i < 40; i++)
        {
            var a0 = i * Mathf.Tau / 40f;
            var a1 = (i + 0.7f) * Mathf.Tau / 40f;
            Vector3 Q(float a, float rr) => p + new Vector3(Mathf.Cos(a) * rr, 0.021f, Mathf.Sin(a) * rr);
            k.OrientedQuad(paint, Q(a0, r - 1.6f), Q(a1, r - 1.6f), Q(a1, r - 0.9f), Q(a0, r - 0.9f), Vector3.Up);
        }
        void Bar(float x0, float z0, float x1, float z1) =>
            k.OrientedQuad(paint, p + new Vector3(x0, 0.021f, z0), p + new Vector3(x1, 0.021f, z0), p + new Vector3(x1, 0.021f, z1), p + new Vector3(x0, 0.021f, z1), Vector3.Up);
        Bar(-3f, -4f, -1.9f, 4f);
        Bar(1.9f, -4f, 3f, 4f);
        Bar(-1.9f, -0.55f, 1.9f, 0.55f);
        for (var i = 0; i < 8; i++)
        {
            var a = i * Mathf.Tau / 8f;
            c.Props.Add(Props.RunwayLight(), p + new Vector3(Mathf.Cos(a) * (r + 0.6f), 0, Mathf.Sin(a) * (r + 0.6f)), 0, range: 120f);
        }
        c.Ruins.Take(p, r);
    }

    // ───────────────────────── Maillages ─────────────────────────

    /// <summary>
    /// Casernement préfabriqué : plancher surélevé sur plots, bardage peint, petites fenêtres (certaines condamnées
    /// au contreplaqué), climatiseurs en façade, portes et marches aux pignons, toit à deux pans en débord.
    /// </summary>
    private static ArrayMesh BarracksMesh(Vector3 s, int seed)
    {
        var k = new MeshKit { Record = true };
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var clad = Mats.Corrugated(new Color(0.5f, 0.46f, 0.38f) * (0.92f + seed % 3 * 0.05f), 1801 + seed % 2, 2.4f);
        var roof = Mats.Corrugated(new Color(0.4f, 0.39f, 0.36f), 1805, 2.4f);
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        var glass = Mats.Painted(new Color(0.05f, 0.06f, 0.06f), 0.1f, 0.3f);
        // Plots et soubassement.
        k.Col = new Color(0.42f, 0.4f, 0.36f);
        for (var x = -hx + 1f; x <= hx - 1f; x += 4f)
            foreach (var z in new[] { -hz + 0.6f, hz - 0.6f })
                k.Box(d, new Vector3(x, 0.25f, z), new Vector3(0.5f, 0.5f, 0.5f));
        foreach (var (start, along, n, len) in new[]
        {
            (new Vector3(-hx, 0, hz), Vector3.Right, Vector3.Back, s.X), (new Vector3(hx, 0, -hz), Vector3.Left, Vector3.Forward, s.X),
            (new Vector3(hx, 0, hz), Vector3.Forward, Vector3.Right, s.Z), (new Vector3(-hx, 0, -hz), Vector3.Back, Vector3.Left, s.Z),
        })
        {
            Clad(k, clad, start, start + along * len, 0.5f, s.Y, 0.5f, s.Y, n, Colors.White);
            var gable = Mathf.Abs(n.X) > 0.5f;
            k.Solid = false;
            if (gable)
            {
                // Porte et marches.
                var p = start + along * (len / 2) + n * 0.05f;
                k.Col = new Color(0.24f, 0.23f, 0.2f);
                k.OrientedQuad(m, p - along * 0.55f + Vector3.Up * 0.5f, p + along * 0.55f + Vector3.Up * 0.5f, p + along * 0.55f + Vector3.Up * 2.6f, p - along * 0.55f + Vector3.Up * 2.6f, n);
                k.Solid = true;
                k.Col = new Color(0.4f, 0.38f, 0.34f);
                k.Box(d, p + n * 0.7f + Vector3.Up * 0.2f, new Vector3(Mathf.Abs(n.X) * 1.4f + Mathf.Abs(n.Z) * 1.8f, 0.4f, Mathf.Abs(n.Z) * 1.4f + Mathf.Abs(n.X) * 1.8f));
                k.Solid = false;
            }
            else
            {
                for (var x = 2.5f; x < len - 2f; x += 4.5f)
                {
                    var h = RuinKit.Hash(seed * 13 + (int)x, (int)(n.Z * 3), 15);
                    var p0 = start + along * x + n * 0.05f;
                    var p1 = start + along * (x + 1.4f) + n * 0.05f;
                    k.Col = h < 0.3f ? new Color(0.36f, 0.3f, 0.22f) : h < 0.45f ? new Color(0.02f, 0.02f, 0.02f) : Colors.White;
                    k.OrientedQuad(h < 0.3f ? d : h < 0.45f ? d : glass, p0 with { Y = 1.7f }, p1 with { Y = 1.7f }, p1 with { Y = 2.8f }, p0 with { Y = 2.8f }, n);
                    if (h > 0.7f)
                    {
                        // Climatiseur sous la fenêtre (solide).
                        k.Solid = true;
                        k.Col = new Color(0.52f, 0.5f, 0.45f);
                        k.Box(m, start + along * (x + 0.7f) + n * 0.4f + Vector3.Up * 1.1f, new Vector3(Mathf.Abs(along.X) * 0.9f + Mathf.Abs(n.X) * 0.7f, 0.6f, Mathf.Abs(along.Z) * 0.9f + Mathf.Abs(n.Z) * 0.7f));
                        k.Solid = false;
                    }
                }
            }
            k.Solid = true;
        }
        // Toit à deux pans, faîtage selon X, débords de 0,5 m.
        var ridge = s.Y + 1.1f;
        k.Col = Colors.White;
        foreach (var sz in new[] { -1f, 1f })
        {
            var n = new Vector3(0, hz + 0.5f, sz * 1.1f).Normalized();
            k.QuadC(roof, new Vector3(-hx - 0.4f, ridge, 0), new Vector3(hx + 0.4f, ridge, 0), new Vector3(hx + 0.4f, s.Y - 0.05f, sz * (hz + 0.5f)), new Vector3(-hx - 0.4f, s.Y - 0.05f, sz * (hz + 0.5f)),
                Vector2.Zero, new Vector2(s.X / 3f, 0), new Vector2(s.X / 3f, hz / 3f), new Vector2(0, hz / 3f), Colors.White, Colors.White, Colors.White, Colors.White, n);
            k.QuadC(roof, new Vector3(-hx - 0.4f, ridge - 0.06f, 0), new Vector3(hx + 0.4f, ridge - 0.06f, 0), new Vector3(hx + 0.4f, s.Y - 0.11f, sz * (hz + 0.5f)), new Vector3(-hx - 0.4f, s.Y - 0.11f, sz * (hz + 0.5f)),
                Vector2.Zero, new Vector2(s.X / 3f, 0), new Vector2(s.X / 3f, hz / 3f), new Vector2(0, hz / 3f), new Color(0.4f, 0.4f, 0.4f), new Color(0.4f, 0.4f, 0.4f), new Color(0.4f, 0.4f, 0.4f), new Color(0.4f, 0.4f, 0.4f), -n);
        }
        k.Col = new Color(0.88f, 0.86f, 0.82f);
        foreach (var sx in new[] { -1f, 1f })
            k.Tri(clad, new Vector3(sx * hx, s.Y, -hz), new Vector3(sx * hx, s.Y, hz), new Vector3(sx * hx, ridge, 0), new Vector3(0, s.Y, 0));
        // Sacs de sable sur une partie du toit (contre le vent), antenne.
        k.Col = new Color(0.4f, 0.37f, 0.3f);
        for (var i = 0; i < 4; i++)
            k.Box(d, new Vector3(-hx + 3f + i * (s.X - 6f) / 3f, ridge - 0.45f, (i % 2 - 0.5f) * 2.4f), new Vector3(0.7f, 0.3f, 0.45f), Rot(0, -0.24f * (i % 2 * 2 - 1)));
        return k.Commit();
    }

    private static Basis Rot(float yaw, float pitch = 0, float roll = 0) =>
        new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, pitch) * new Basis(Vector3.Back, roll);

    /// <summary>
    /// Auvent du parc automobile : poteaux tous les 6,25 m, toit monopente en tôle sur poutres (praticable), mur du fond
    /// en bardage, murets de séparation, escalier à l'ouest jusqu'au toit.
    /// </summary>
    private static ArrayMesh MotorShedMesh(Vector3 s)
    {
        var k = new MeshKit { Record = true };
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var clad = Mats.Corrugated(new Color(0.45f, 0.43f, 0.38f), 1803, 2.4f);
        var roof = Mats.Corrugated(new Color(0.4f, 0.39f, 0.36f), 1806, 2.4f);
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        float RoofY(float z) => Mathf.Lerp(s.Y, s.Y - 0.9f, (z + hz) / s.Z);
        k.Col = new Color(0.27f, 0.26f, 0.24f);
        for (var x = -hx; x <= hx + 0.01f; x += s.X / 8f)
            foreach (var z in new[] { -hz + 0.3f, hz - 0.3f })
                k.Box(m, new Vector3(x, RoofY(z) / 2, z), new Vector3(0.35f, RoofY(z), 0.35f));
        for (var x = -hx; x <= hx + 0.01f; x += s.X / 8f)
        {
            k.Solid = false;
            k.Tube(m, new Vector3(x, RoofY(-hz) - 0.3f, -hz), new Vector3(x, RoofY(hz) - 0.3f, hz), 0.15f, 4);
            k.Solid = true;
        }
        k.Col = Colors.White;
        k.QuadC(roof, new Vector3(-hx - 0.6f, RoofY(-hz - 0.8f), -hz - 0.8f), new Vector3(hx + 0.6f, RoofY(-hz - 0.8f), -hz - 0.8f), new Vector3(hx + 0.6f, RoofY(hz + 0.4f), hz + 0.4f), new Vector3(-hx - 0.6f, RoofY(hz + 0.4f), hz + 0.4f),
            Vector2.Zero, new Vector2(s.X / 3f, 0), new Vector2(s.X / 3f, s.Z / 3f), new Vector2(0, s.Z / 3f), Colors.White, Colors.White, Colors.White, Colors.White, Vector3.Up);
        var dark = new Color(0.35f, 0.35f, 0.35f);
        k.QuadC(roof, new Vector3(-hx - 0.6f, RoofY(-hz - 0.8f) - 0.07f, -hz - 0.8f), new Vector3(hx + 0.6f, RoofY(-hz - 0.8f) - 0.07f, -hz - 0.8f), new Vector3(hx + 0.6f, RoofY(hz + 0.4f) - 0.07f, hz + 0.4f), new Vector3(-hx - 0.6f, RoofY(hz + 0.4f) - 0.07f, hz + 0.4f),
            Vector2.Zero, new Vector2(s.X / 3f, 0), new Vector2(s.X / 3f, s.Z / 3f), new Vector2(0, s.Z / 3f), dark, dark, dark, dark, Vector3.Down);
        Clad2(k, clad, new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), 0, RoofY(hz), 0, RoofY(hz), Vector3.Back, Colors.White);
        // Murets de séparation entre les travées.
        k.Col = new Color(0.47f, 0.45f, 0.41f);
        for (var i = 1; i < 8; i += 2)
            k.Box(d, new Vector3(-hx + i * s.X / 8f, 0.6f, hz - 3.5f), new Vector3(0.3f, 1.2f, 6.4f));
        // Escalier ouest, sortie vers l'est sur le toit (plateforme à hauteur de la rive).
        StairTower(k, new Transform3D(new Basis(Vector3.Up, -Mathf.Pi / 2), new Vector3(-hx - 3.9f, 0, 0)), RoofY(0) + 0.05f, 1.6f);
        return k.Commit();
    }

    /// <summary>Atelier fermé : bardage, deux portes sectionnelles, bande d'imposte, toit plat à acrotère.</summary>
    private static ArrayMesh WorkshopMesh(Vector3 s)
    {
        var k = new MeshKit { Record = true };
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var clad = Mats.Corrugated(new Color(0.44f, 0.42f, 0.37f), 1804, 2.4f);
        var shutter = Mats.Corrugated(new Color(0.32f, 0.33f, 0.31f), 1807, 4f);
        var d = Mats.Detail();
        foreach (var (start, along, n, len) in new[]
        {
            (new Vector3(-hx, 0, hz), Vector3.Right, Vector3.Back, s.X), (new Vector3(hx, 0, -hz), Vector3.Left, Vector3.Forward, s.X),
            (new Vector3(hx, 0, hz), Vector3.Forward, Vector3.Right, s.Z), (new Vector3(-hx, 0, -hz), Vector3.Back, Vector3.Left, s.Z),
        })
        {
            Clad(k, clad, start, start + along * len, 0, s.Y, 0, s.Y, n, Colors.White);
            if (n == Vector3.Back)
                foreach (var x in new[] { 3f, 10f })
                    Clad(k, shutter, start + along * x + n * 0.05f, start + along * (x + 5f) + n * 0.05f, 0.05f, 5f, 0.05f, 5f, n, new Color(0.8f, 0.78f, 0.74f));
        }
        k.Col = new Color(0.4f, 0.39f, 0.36f);
        k.Ring(d, hx, hz, s.Y, s.Y + 0.6f, 0.25f, 0.05f);
        k.Quad(d, new Vector3(-hx, s.Y, hz), new Vector3(hx, s.Y, hz), new Vector3(hx, s.Y, -hz), new Vector3(-hx, s.Y, -hz));
        k.Col = new Color(0.3f, 0.3f, 0.28f);
        k.Box(Mats.DetailMetal(), new Vector3(3, s.Y + 0.8f, -2), new Vector3(2.4f, 1.6f, 2.4f));
        return k.Commit();
    }

    /// <summary>Abri technique en béton (transmissions) : porte d'acier, climatiseur, chemin de câbles.</summary>
    private static ArrayMesh ShelterBoxMesh(Vector3 s)
    {
        var k = new MeshKit { Record = true };
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        k.Col = new Color(0.55f, 0.53f, 0.48f);
        k.Box(d, new Vector3(0, s.Y / 2, 0), s);
        k.Col = new Color(0.25f, 0.26f, 0.24f);
        k.Box(m, new Vector3(-2f, 1.1f, s.Z / 2 + 0.03f), new Vector3(1f, 2.2f, 0.06f));
        k.Col = new Color(0.5f, 0.48f, 0.43f);
        k.Box(m, new Vector3(2f, 1.2f, s.Z / 2 + 0.4f), new Vector3(1.2f, 0.8f, 0.7f));
        k.Col = new Color(0.2f, 0.2f, 0.19f);
        k.Box(m, new Vector3(-s.X / 2 - 0.2f, s.Y - 0.4f, 0), new Vector3(0.3f, 0.15f, s.Z));
        return k.Commit();
    }

    /// <summary>
    /// Radôme : socle carré à terrasse (garde-corps, escalier extérieur), dôme à panneaux sur anneau de béton, éventré
    /// sur un côté (panneaux arrachés, coque intérieure sombre, antenne visible).
    /// </summary>
    private static ArrayMesh RadarMesh(Vector3 s)
    {
        var k = new MeshKit { Record = true };
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        var shell = Mats.Worn(new Color(0.64f, 0.62f, 0.57f), 0.7f, 0f);
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var wall = new Color(0.56f, 0.53f, 0.47f);
        foreach (var (a, b, n) in new[]
        {
            (new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), Vector3.Back), (new Vector3(hx, 0, -hz), new Vector3(-hx, 0, -hz), Vector3.Forward),
            (new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz), Vector3.Right), (new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), Vector3.Left),
        })
        {
            k.Col = wall;
            k.OrientedQuad(d, a, b, b with { Y = s.Y }, a with { Y = s.Y }, n);
            k.Solid = false;
            k.Col = new Color(0.04f, 0.04f, 0.04f);
            var mid = (a + b) / 2 + n * 0.04f;
            var along = (b - a).Normalized();
            k.OrientedQuad(d, mid - along * 2f + Vector3.Up * 5.5f, mid + along * 2f + Vector3.Up * 5.5f, mid + along * 2f + Vector3.Up * 6.5f, mid - along * 2f + Vector3.Up * 6.5f, n);
            k.Solid = true;
        }
        k.Col = new Color(0.33f, 0.32f, 0.3f);
        k.Quad(d, new Vector3(-hx, s.Y, hz), new Vector3(hx, s.Y, hz), new Vector3(hx, s.Y, -hz), new Vector3(-hx, s.Y, -hz));
        // Acrotère, ouvert à l'arrivée de l'escalier (côté ouest).
        k.Col = wall * 0.9f;
        const float gz0 = -1.8f, gz1 = 1.8f;
        var sz0 = -hz + 4.5f + gz0;
        var sz1 = -hz + 4.5f + gz1;
        k.BoxMinMax(d, new Vector3(-hx, s.Y, hz - 0.25f), new Vector3(hx, s.Y + 1f, hz));
        k.BoxMinMax(d, new Vector3(-hx, s.Y, -hz), new Vector3(hx, s.Y + 1f, -hz + 0.25f));
        k.BoxMinMax(d, new Vector3(hx - 0.25f, s.Y, -hz), new Vector3(hx, s.Y + 1f, hz));
        k.BoxMinMax(d, new Vector3(-hx, s.Y, -hz), new Vector3(-hx + 0.25f, s.Y + 1f, sz0));
        k.BoxMinMax(d, new Vector3(-hx, s.Y, sz1), new Vector3(-hx + 0.25f, s.Y + 1f, hz));
        // Anneau de béton sous le dôme.
        k.Col = wall * 0.85f;
        k.Cylinder(d, new Vector3(0, s.Y, 0), RadarDomeR + 0.3f, RadarDomeR + 0.3f, 1.2f, 28);
        // Dôme : 3/4 de sphère posée sur l'anneau, panneaux arrachés sur le flanc sud-est.
        var cy = s.Y + 1.2f + RadarDomeR * 0.55f;
        const int segU = 28, segV = 12;
        var lat0 = Mathf.Asin(-0.55f);
        Vector3 Pt(float lon, float lat) => new(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
        for (var j = 0; j < segV; j++)
        {
            var la0 = Mathf.Lerp(lat0, Mathf.Pi / 2, j / (float)segV);
            var la1 = Mathf.Lerp(lat0, Mathf.Pi / 2, (j + 1) / (float)segV);
            for (var i = 0; i < segU; i++)
            {
                var lo0 = i * Mathf.Tau / segU;
                var lo1 = (i + 1) * Mathf.Tau / segU;
                var hole = i >= 2 && i <= 5 && j >= 2 && j <= 5 && !(i == 5 && j == 5) || RuinKit.Hash(i, j, 17) < 0.03f;
                var n00 = Pt(lo0, la0); var n10 = Pt(lo1, la0); var n11 = Pt(lo1, la1); var n01 = Pt(lo0, la1);
                Vector3 P(Vector3 n, float r) => new Vector3(0, cy, 0) + n * r;
                if (!hole)
                {
                    var col = new Color(0.95f, 0.95f, 0.93f) * (0.9f + RuinKit.Hash(i, j, 18) * 0.12f);
                    if (Mathf.Cos(la0) > 0.001f) k.TriSmooth(shell, P(n00, RadarDomeR), P(n10, RadarDomeR), P(n11, RadarDomeR), n00, n10, n11, col, col, col);
                    if (Mathf.Cos(la1) > 0.001f) k.TriSmooth(shell, P(n00, RadarDomeR), P(n11, RadarDomeR), P(n01, RadarDomeR), n00, n11, n01, col, col, col);
                }
                // Coque intérieure sombre (vue par la brèche).
                var dark = new Color(0.12f, 0.12f, 0.11f);
                k.Solid = false;
                if (Mathf.Cos(la0) > 0.001f) k.TriSmooth(d, P(n00, RadarDomeR - 0.15f), P(n10, RadarDomeR - 0.15f), P(n11, RadarDomeR - 0.15f), -n00, -n10, -n11, dark, dark, dark);
                if (Mathf.Cos(la1) > 0.001f) k.TriSmooth(d, P(n00, RadarDomeR - 0.15f), P(n11, RadarDomeR - 0.15f), P(n01, RadarDomeR - 0.15f), -n00, -n11, -n01, dark, dark, dark);
                k.Solid = true;
            }
        }
        // Antenne radar à l'intérieur (visible par la brèche).
        k.Solid = false;
        k.Col = new Color(0.3f, 0.3f, 0.28f);
        k.Box(m, new Vector3(0, cy - 1f, 0), new Vector3(0.8f, 3f, 0.8f));
        k.Box(m, new Vector3(1.2f, cy + 1.2f, 1.5f), new Vector3(9f, 4f, 0.25f), new Basis(Vector3.Up, -0.8f) * new Basis(Vector3.Right, -0.3f));
        k.Solid = true;
        // Escalier extérieur côté ouest jusqu'à la terrasse.
        StairTower(k, new Transform3D(new Basis(Vector3.Up, -Mathf.Pi / 2), new Vector3(-hx - 3.5f, 0, -hz + 4.5f)), s.Y, 1.5f);
        return k.Commit();
    }

    /// <summary>Pylône treillis à trois membrures (fût effilé), paliers, paraboles et antennes. Pied au repère.</summary>
    private static void LatticeMast(MeshKit k, float h)
    {
        var m = Mats.DetailMetal();
        var paint = new Color(0.5f, 0.48f, 0.44f);
        const int legs = 3;
        float R(float y) => Mathf.Lerp(3.2f, 0.9f, y / h);
        Vector3 Leg(int i, float y)
        {
            var a = i * Mathf.Tau / legs + 0.3f;
            return new Vector3(Mathf.Cos(a) * R(y), y, Mathf.Sin(a) * R(y));
        }
        k.Col = paint;
        for (var i = 0; i < legs; i++)
        {
            k.Tube(m, Leg(i, 0), Leg(i, h), 0.14f, 6);
            k.Box(Mats.Detail(), Leg(i, 0) + Vector3.Up * 0.3f, new Vector3(1.2f, 0.6f, 1.2f));
        }
        k.Solid = false;
        const int panels = 14;
        for (var p = 0; p < panels; p++)
        {
            var y0 = p * h / panels;
            var y1 = (p + 1) * h / panels;
            for (var i = 0; i < legs; i++)
            {
                var j = (i + 1) % legs;
                k.Tube(m, Leg(i, y0), Leg(j, y1), 0.04f, 3);
                k.Tube(m, Leg(j, y0), Leg(i, y1), 0.04f, 3);
                k.Tube(m, Leg(i, y1), Leg(j, y1), 0.04f, 3);
            }
        }
        k.Solid = true;
        // Paliers (solides) et paraboles.
        foreach (var y in new[] { h * 0.45f, h * 0.85f })
        {
            k.Col = new Color(0.3f, 0.3f, 0.28f);
            k.Cylinder(m, new Vector3(0, y, 0), R(y) + 0.6f, R(y) + 0.6f, 0.12f, 12);
        }
        k.Solid = false;
        k.Col = new Color(0.62f, 0.6f, 0.56f);
        foreach (var (y, a) in new[] { (h * 0.6f, 0.4f), (h * 0.72f, 2.6f), (h * 0.9f, 4.4f) })
        {
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var cpos = new Vector3(0, y, 0) + dir * (R(y) + 0.6f);
            k.Cylinder(m, cpos, 1.2f, 0.25f, 0.6f, 14, true, new Basis(dir.Cross(Vector3.Up).Normalized(), Mathf.Pi / 2));
        }
        k.Col = new Color(0.15f, 0.15f, 0.15f);
        k.Tube(m, new Vector3(0, h, 0), new Vector3(0, h + 6f, 0), 0.08f, 4);
        k.Solid = true;
    }
}
