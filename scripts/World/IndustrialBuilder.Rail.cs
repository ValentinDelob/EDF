using Godot;

namespace Rempart;

/// <summary>
/// Gare de triage : six voies sur ballast (est-ouest, prolongées hors carte), heurtoirs, rame de wagons-citernes
/// (explosifs, réaction en chaîne d'un wagon à l'autre), wagons couverts et trémies (poussés par les explosions),
/// wagon déraillé, portique à conteneurs sur rails avec tour d'escalier jusqu'à la poutre (poste de tir).
/// </summary>
public static partial class IndustrialBuilder
{
    /// <summary>Axes des voies (z).</summary>
    private static readonly float[] Tracks = { -78f, -70f, -62f, -12f, -4f, 24f };
    private const float TrackX0 = 42f;

    private static void BuildRailYard(Ctx c)
    {
        foreach (var z in Tracks) Track(c, z, z == 24f ? 96f : TrackX0, 262f);

        // Rame de wagons-citernes sur la voie -62 (au pied de la raffinerie).
        for (var i = 0; i < 6; i++)
        {
            var p = new Vector3(60f + i * 16.6f, RailTop, -62f);
            var spec = new Destructibles.TankSpec { Wreck = Props.TankWagonWreck(), Blast = 13f, Damage = 300f, Core = 2.7f, FireScale = 1.8f, FireY = 2.9f, Paint = i % 3 == 0 ? new Color(0.14f, 0.14f, 0.14f) : new Color(0.55f, 0.55f, 0.52f) };
            Destructibles.AddTank(c.Root, c.Body, Props.TankWagon(i % 3), new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2), p), 350f, spec);
            c.Tanks.Add(p);
        }
        // Wagons couverts et trémies (accessoires poussés par les explosions), avec des trous dans les rames.
        Rake(c, -78f, 112f, 8, true);
        Rake(c, -70f, 70f, 9, false);
        Rake(c, -12f, 96f, 4, false);
        Rake(c, -4f, 178f, 3, false);
        Rake(c, 24f, 112f, 5, false);

        // Wagon couvert déraillé, couché en travers des voies du milieu.
        var dp = new Vector3(220f, 0f, -8f);
        var db = new Basis(Vector3.Up, Mathf.Pi / 2 + 0.35f) * new Basis(Vector3.Back, 1.15f);
        c.Ruins.Inst(Props.BoxWagon(0), new Transform3D(db, dp + new Basis(Vector3.Up, Mathf.Pi / 2 + 0.35f) * new Vector3(1.6f, 0.4f, 0)), true, 400f);
        c.Ruins.Box(new Transform3D(db, dp + new Basis(Vector3.Up, Mathf.Pi / 2 + 0.35f) * new Vector3(1.6f, 0.4f, 0) + db * new Vector3(0, 2.6f, 0)), new Vector3(2.9f, 3.0f, 14.6f));
        c.Ruins.Decal(Mats.Decal.Scorch, dp, 16f, 0.35f, 0.03f, 1.6f);
        Take(c, dp, 8f, 5f);

        GantryCrane(c, new Vector3(128f, 0, -22f));
    }

    /// <summary>Voie sur ballast : remblai trapézoïdal solide, traverses et rails (relief non solide).</summary>
    private static void Track(Ctx c, float z, float x0, float x1)
    {
        var metal = Mats.DetailMetal();
        var concrete = Mats.Detail();
        var ballast = Mats.Gravel();
        for (var x = x0; x < x1; x += 24f)
        {
            var xe = Mathf.Min(x1, x + 24f);
            var k = c.Solid.At(new Vector3((x + xe) / 2, 0, z));
            k.Col = new Color(0.3f, 0.28f, 0.26f);
            const float top = 0.18f;
            k.QuadC(ballast, new Vector3(x, 0, z + 2.3f), new Vector3(xe, 0, z + 2.3f), new Vector3(xe, top, z + 1.6f), new Vector3(x, top, z + 1.6f),
                Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, k.Col, k.Col, k.Col, k.Col, new Vector3(0, 1, 1));
            k.QuadC(ballast, new Vector3(x, 0, z - 2.3f), new Vector3(xe, 0, z - 2.3f), new Vector3(xe, top, z - 1.6f), new Vector3(x, top, z - 1.6f),
                Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, k.Col, k.Col, k.Col, k.Col, new Vector3(0, 1, -1));
            k.Col = new Color(0.24f, 0.225f, 0.21f);
            k.OrientedQuad(ballast, new Vector3(x, top, z - 1.6f), new Vector3(xe, top, z - 1.6f), new Vector3(xe, top, z + 1.6f), new Vector3(x, top, z + 1.6f), Vector3.Up);
            // Traverses (béton, quelques-unes en bois noirci) et rails.
            k.Solid = false;
            for (var s = x + 0.3f; s < xe; s += 0.8f)
            {
                var wood = RuinKit.Hash(s, z, 41) < 0.25f;
                k.Col = wood ? new Color(0.12f, 0.1f, 0.085f) : new Color(0.26f, 0.255f, 0.24f);
                k.Box(concrete, new Vector3(s, top + 0.06f, z), new Vector3(0.26f, 0.12f, 2.5f));
            }
            k.Col = new Color(0.2f, 0.17f, 0.15f);
            foreach (var g in new[] { -0.72f, 0.72f })
            {
                k.Box(metal, new Vector3((x + xe) / 2, top + 0.15f, z + g), new Vector3(xe - x, 0.08f, 0.14f));
                k.Col = new Color(0.4f, 0.4f, 0.4f);
                k.Box(metal, new Vector3((x + xe) / 2, RailTop - 0.015f, z + g), new Vector3(xe - x, 0.03f, 0.07f));
                k.Col = new Color(0.2f, 0.17f, 0.15f);
            }
            k.Solid = true;
        }
        // Heurtoir en bout de voie (côté ouest).
        var hk = c.Solid.At(new Vector3(x0, 0, z));
        hk.Col = new Color(0.3f, 0.12f, 0.08f);
        hk.Box(metal, new Vector3(x0 - 0.6f, 0.9f, z), new Vector3(0.8f, 1.2f, 2.6f));
        hk.Col = new Color(0.2f, 0.19f, 0.18f);
        hk.Box(concrete, new Vector3(x0 - 1.4f, 0.6f, z), new Vector3(1.2f, 1.2f, 3f));
        Take(c, new Rect2(x0 - 2f, z - 2.4f, x1 - x0 + 2f, 4.8f), 0f);
    }

    /// <summary>Rame de wagons couverts (ou de trémies) sur une voie, avec des trous (hachage) et des variantes.</summary>
    private static void Rake(Ctx c, float z, float x0, int n, bool hoppers)
    {
        for (var i = 0; i < n; i++)
        {
            if (RuinKit.Hash(i, z, 43) < 0.18f) continue;
            var p = new Vector3(x0 + i * 16.6f, RailTop, z);
            if (p.X > 236f) break;
            var mesh = hoppers ? Props.HopperWagon() : Props.BoxWagon((int)(RuinKit.Hash(i, z, 44) * 3));
            c.Props.Add(mesh, p, Mathf.Pi / 2 + (RuinKit.Hash(i, z, 45) - 0.5f) * 0.01f, range: 420f, shadows: true);
        }
    }

    /// <summary>
    /// Portique à conteneurs sur rails : deux palées en A, double poutre caisson à 17 m, chariot et cabine, tour
    /// d'escalier contre la palée nord jusqu'à la passerelle de la poutre. Collision exacte.
    /// </summary>
    private static void GantryCrane(Ctx c, Vector3 p)
    {
        var k = c.Solid.At(p);
        var paint = Mats.Worn(new Color(0.5f, 0.38f, 0.14f), 0.6f, 0.3f);
        var metal = Mats.DetailMetal();
        const float h = 17f, zN = -26f, zS = 26f, hw = 5f;
        k.Col = Colors.White;
        foreach (var z in new[] { zN, zS })
        {
            // Palée : deux jambes inclinées, sommier sur bogies, entretoise.
            foreach (var sx in new[] { -1f, 1f })
            {
                k.Tube(paint, p + new Vector3(sx * (hw + 1.5f), 1.2f, z), p + new Vector3(sx * hw, h - 0.8f, z), 0.55f, 8, true);
                k.Col = new Color(0.12f, 0.11f, 0.1f);
                k.Box(metal, p + new Vector3(sx * (hw + 1.5f), 0.7f, z), new Vector3(3.2f, 1f, 1.6f));
                k.Col = Colors.White;
            }
            k.Box(paint, p + new Vector3(0, 1.2f, z), new Vector3(hw * 2 + 3f, 0.8f, 1.2f));
            k.Box(paint, p + new Vector3(0, h * 0.55f, z), new Vector3(hw * 2 + 1f, 0.5f, 0.5f));
            // Rails du portique au sol (relief).
            k.Solid = false;
            k.Col = new Color(0.3f, 0.3f, 0.3f);
            foreach (var sx in new[] { -1f, 1f })
                k.Box(metal, p + new Vector3(sx * (hw + 1.5f), 0.04f, 0), new Vector3(0.12f, 0.08f, zS - zN + 30f));
            k.Solid = true;
            k.Col = Colors.White;
        }
        // Poutres caissons (praticables) et passerelle en caillebotis entre elles.
        foreach (var sx in new[] { -1f, 1f })
            k.Box(paint, p + new Vector3(sx * (hw - 0.6f), h + 0.6f, 0), new Vector3(1.2f, 1.8f, zS - zN + 14f));
        k.Col = Colors.White;
        var gz0 = p.Z + zN - 7f;
        var gz1 = p.Z + zS + 7f;
        var gy = h + 1.5f; // au ras du dessus des poutres : passerelle continue d'une poutre à l'autre
        k.Quad(Mats.Grating(), new Vector3(p.X - hw + 1.2f, gy, gz0), new Vector3(p.X + hw - 1.2f, gy, gz0), new Vector3(p.X + hw - 1.2f, gy, gz1), new Vector3(p.X - hw + 1.2f, gy, gz1),
            new Vector2(0, 0), new Vector2(hw * 2, 0), new Vector2(hw * 2, gz1 - gz0), new Vector2(0, gz1 - gz0));
        k.Quad(Mats.Grating(), new Vector3(p.X - hw + 1.2f, gy, gz1), new Vector3(p.X + hw - 1.2f, gy, gz1), new Vector3(p.X + hw - 1.2f, gy, gz0), new Vector3(p.X - hw + 1.2f, gy, gz0),
            new Vector2(0, gz1 - gz0), new Vector2(hw * 2, gz1 - gz0), new Vector2(hw * 2, 0), new Vector2(0, 0));
        // Chariot, cabine (vitres noires), câbles et palonnier.
        var cz = p.Z + 22f; // chariot garé au bout sud : la passerelle reste libre depuis la tour d'escalier
        k.Col = new Color(0.45f, 0.44f, 0.42f);
        k.Box(metal, new Vector3(p.X, gy + 0.8f, cz), new Vector3(hw * 2 + 0.6f, 1.6f, 5f));
        k.Col = new Color(0.4f, 0.4f, 0.38f);
        k.Box(metal, new Vector3(p.X + hw - 1f, h - 1.6f, cz + 1f), new Vector3(2.2f, 2.4f, 2.4f));
        k.Solid = false;
        k.Col = new Color(0.03f, 0.03f, 0.03f);
        k.Box(metal, new Vector3(p.X + hw - 1f, h - 1.3f, cz + 2.21f), new Vector3(2f, 1.2f, 0.02f));
        k.Col = new Color(0.1f, 0.1f, 0.1f);
        foreach (var (dx, dz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
            k.Tube(metal, new Vector3(p.X + dx * 1.2f, h + 1.4f, cz + dz * 1.2f), new Vector3(p.X + dx * 1.2f, 6.6f, cz + dz * 1.2f), 0.03f, 3);
        k.Solid = true;
        k.Col = new Color(0.45f, 0.36f, 0.12f);
        k.Box(metal, new Vector3(p.X, 6.3f, cz), new Vector3(3f, 0.5f, 12.4f));
        // Garde-corps de la passerelle.
        RailLine(k, new Vector3(p.X - hw + 1.25f, gy, gz0), new Vector3(p.X - hw + 1.25f, gy, gz1));
        RailLine(k, new Vector3(p.X + hw - 1.25f, gy, gz0 + 4f), new Vector3(p.X + hw - 1.25f, gy, gz1));
        // Tour d'escalier contre la palée nord (côté est), arrivée sur la passerelle.
        StairTower(k, new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2), new Vector3(p.X + hw + 4.6f, 0, p.Z + zN - 4.5f)), gy, 3.2f);
        Take(c, new Rect2(p.X + hw + 2.6f, p.Z + zN - 6f, 6f, 3f), 0f);
        foreach (var z in new[] { zN, zS })
            foreach (var sx in new[] { -1f, 1f })
                Take(c, p + new Vector3(sx * (hw + 1.5f), 0, z), 1.8f, 1f, 0f);
    }
}
