using Godot;

namespace Rempart;

/// <summary>
/// Rempart de la base : lignes de gabions (modules destructibles de 3,3 m) percées de brèches là où la colonie est
/// passée (sable répandu, module éventré), coupures de la piste fermées de hérissons et de séparateurs, miradors
/// (douze, escalier tourné vers l'intérieur), poste de contrôle de la porte sud (guérite, barrières levantes, chicane
/// de murs en T et de séparateurs, barbelés), véhicules du poste.
/// </summary>
public static partial class DesertBuilder
{
    private static void BuildPerimeter(Ctx c)
    {
        // Gabions : nord, sud (porte), ouest et est (coupés par la piste).
        HescoLine(c, new Vector3(WallW, 0, WallN), new Vector3(WallE, 0, WallN), 1);
        HescoLine(c, new Vector3(WallW, 0, WallS), new Vector3(-GateHalf, 0, WallS), 2);
        HescoLine(c, new Vector3(GateHalf, 0, WallS), new Vector3(WallE, 0, WallS), 3);
        HescoLine(c, new Vector3(WallW, 0, WallN), new Vector3(WallW, 0, RunwayZ0 - 4f), 4);
        HescoLine(c, new Vector3(WallW, 0, RunwayZ1 + 4f), new Vector3(WallW, 0, WallS), 5);
        HescoLine(c, new Vector3(WallE, 0, WallN), new Vector3(WallE, 0, RunwayZ0 - 4f), 6);
        HescoLine(c, new Vector3(WallE, 0, RunwayZ1 + 4f), new Vector3(WallE, 0, WallS), 7);
        // Coupures de la piste : hérissons et séparateurs en travers (les engins passent entre).
        var r = c.Ruins;
        foreach (var x in new[] { WallW, WallE })
            for (var i = 0; i < 6; i++)
            {
                var z = RunwayZ0 + 3f + i * 6.8f;
                var p = new Vector3(x + (i % 2 == 0 ? 2.5f : -2.5f), 0, z);
                if (i % 3 == 1)
                {
                    c.Props.Add(Props.JerseyBarrier(), p, 0.2f * (i - 2), range: 260f, shadows: true);
                    continue;
                }
                r.Inst(Props.Hedgehog(), RuinKit.At(p, i * 0.9f), true, 260f);
                r.Take(p, 1.2f);
            }

        // Miradors (escalier vers l'intérieur de la base).
        foreach (var (x, z) in new[]
        {
            (-196f, -196f), (196f, -196f), (-196f, 127f), (196f, 127f), (-197f, -128f), (197f, -128f),
            (-197f, -60f), (197f, -60f), (197f, 28f), (0f, -197f), (-24f, 127f), (24f, 127f),
        })
        {
            var p = new Vector3(x, 0, z);
            var inward = Mathf.Abs(x) < 30f ? new Vector3(0, 0, -Mathf.Sign(z)) : Mathf.Abs(z) > 190f ? new Vector3(-Mathf.Sign(x), 0, -Mathf.Sign(z)).Normalized() : new Vector3(-Mathf.Sign(x), 0, 0);
            if (x == 0f) inward = Vector3.Back;
            if (Mathf.Abs(z - 127f) < 1f && Mathf.Abs(x) > 100f) inward = new Vector3(-Mathf.Sign(x), 0, -1).Normalized();
            var yaw = Mathf.Atan2(inward.X, inward.Z);
            c.Props.Add(Props.Watchtower(), p, yaw, range: 420f, shadows: true);
            Take(c, p, 3f, 3f, 0f);
            r.Take(p + inward * 6f, 2.5f);
            // Pieds bloqués dans le champ de flux : aucun insecte ne sort de terre dessous (il l'abattrait aussitôt).
            FlowBlock(c, p, new Vector3(4.6f, Props.TowerDeck + 2.7f, 4.6f), 0f);
        }

        Gate(c);
    }

    /// <summary>
    /// Ligne de gabions entre deux points (modules alignés), brèches par groupes (hachage) : modules manquants, sable
    /// répandu, module éventré de part et d'autre. Rien sur une emprise réservée (aire de départ, nids).
    /// </summary>
    private static void HescoLine(Ctx c, Vector3 a, Vector3 b, int salt)
    {
        var dir = (b - a).Normalized();
        var len = (b - a).Length();
        var yaw = Mathf.Atan2(-dir.Z, dir.X);
        var n = Mathf.FloorToInt(len / Props.HescoLen);
        var r = c.Ruins;
        for (var i = 0; i < n; i++)
        {
            var p = a + dir * (i * Props.HescoLen + Props.HescoLen / 2);
            // Brèche : un groupe de 8 modules sur 7 environ en perd deux ou trois au milieu.
            var group = i / 8;
            var inGroup = i % 8;
            var breach = RuinKit.Hash(group, salt, 101) < 0.16f;
            if (breach && inGroup is 3 or 4 or 5)
            {
                if (inGroup == 4)
                {
                    Decal(c, Mats.Decal.Dust, p, 9f, yaw + 0.3f, 0.03f + i * 0.0001f, 1.6f);
                    Decal(c, Mats.Decal.Dust, p + dir * 1.5f, 7f, yaw, 0.028f + i * 0.0001f);
                    var k = c.Solid.At(p);
                    k.Col = new Color(0.86f, 0.84f, 0.8f);
                    // Sable répandu (bourrelets bas et praticables).
                    k.Frustum(Mats.DesertSand(), p + new Vector3(0, 0, 0), new Vector2(4.5f, 4.5f), new Vector2(1.5f, 1.2f), 0.5f);
                }
                continue;
            }
            var torn = breach && inGroup is 2 or 6 || RuinKit.Hash(i, salt, 102) < 0.06f;
            c.Props.Add(Props.HescoWall(torn ? 1 : 0), p, yaw, range: 360f, shadows: true);
        }
    }

    /// <summary>Poste de contrôle de la porte sud (route du désert) : guérite, barrières, chicane, barbelés, véhicules.</summary>
    private static void Gate(Ctx c)
    {
        var r = c.Ruins;
        c.Props.Add(Props.GuardBooth(), new Vector3(14.5f, 0, 128f), Mathf.Pi / 2, range: 300f, shadows: true);
        r.Take(new Vector3(14.5f, 0, 128f), 3f);
        c.Props.Add(Props.BoomGate(), new Vector3(-8.2f, 0, 132.5f), 0, range: 220f, shadows: true);
        c.Props.Add(Props.BoomGate(), new Vector3(8.2f, 0, 130f), Mathf.Pi, range: 220f, shadows: true);
        // Chicane hors du rempart : murs en T décalés, séparateurs en bord de route.
        for (var i = 0; i < 5; i++)
        {
            c.Props.Add(Props.TWall(i % 2), new Vector3(-8.5f + i * 1.55f, 0, 144f), 0, range: 260f, shadows: true);
            c.Props.Add(Props.TWall((i + 1) % 2), new Vector3(8.5f - i * 1.55f, 0, 151f), 0, range: 260f, shadows: true);
        }
        foreach (var sx in new[] { -1f, 1f })
            for (var i = 0; i < 4; i++)
                c.Props.Add(Props.JerseyBarrier(), new Vector3(sx * 8f, 0, 140f + i * 4.2f), 0, range: 220f, shadows: true);
        // Barbelés le long du rempart sud (extérieur), interrompus à la porte.
        foreach (var (x0, x1) in new[] { (-120f, -14f), (14f, 110f) })
            for (var x = x0; x < x1; x += 12f)
                if (RuinKit.Hash(x, 139f, 103) > 0.25f) Props.RazorWire(r.Kit(RuinKit.Layer.Solid, new Vector3(x, 0, 139.5f)), new Vector3(x, 0, 139.5f), new Vector3(Mathf.Min(x + 11f, x1), 0, 139.5f));
        // Véhicules du poste : léger calciné en travers, camion abandonné sur le bas-côté.
        r.Inst(Props.MilJeepWreck(), RuinKit.At(new Vector3(-3.5f, 0, 148f), 0.9f), true, 320f);
        r.BoxOnGround(new Vector3(-3.5f, 0, 148f), 0.9f, Props.MilJeepSize);
        r.Fire(new Vector3(-3.5f, 0.9f, 148f), 1.1f);
        Decal(c, Mats.Decal.Scorch, new Vector3(-3.5f, 0, 148f), 9f, 0.3f, 0.03f);
        r.Take(new Vector3(-3.5f, 0, 148f), 3f);
        Vehicle(c, Props.MilTruck(), new Vector3(17f, 0, 150f), -0.25f, Props.VehicleType.Truck);
    }
}
