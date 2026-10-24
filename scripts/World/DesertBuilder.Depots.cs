using Godot;

namespace Rempart;

/// <summary>
/// Dépôts de la base : parc à carburant dans sa cuvette de merlons (deux cuves de stockage, deux réservoirs
/// horizontaux, tuyauteries, citernes routières) et soutes à munitions enterrées en igloo (citernes explosives : une
/// soute percée brûle puis saute, avec un souffle énorme). Voir <c>Destructibles.Tanks.cs</c>.
/// </summary>
public static partial class DesertBuilder
{
    /// <summary>Cuvette du dépôt de carburant : x0, z0, largeur, profondeur (pied extérieur des merlons).</summary>
    private static readonly Rect2 FuelBund = new(152, 52, 50, 78);
    private static readonly float[] BunkerXs = { -186f, -156f, -126f };
    private const float BunkerZ = 108f;

    private static readonly Color TankSandPaint = new(0.52f, 0.49f, 0.42f);

    private static void BuildDepots(Ctx c)
    {
        // Cuves de stockage.
        foreach (var (x, z, v) in new[] { (176f, 72f, 0), (176f, 108f, 2) })
        {
            var (rad, h) = Props.StorageTankDims[v];
            var p = new Vector3(x, 0, z);
            var spec = new Destructibles.TankSpec
            {
                Wreck = Props.StorageTankWreck(v), Blast = 20f, Damage = 520f, Core = h * 0.55f, FireScale = 3.4f, FireY = 1.2f, Paint = TankSandPaint,
            };
            Destructibles.AddTank(c.Root, c.Body, Props.StorageTank(v), new Transform3D(new Basis(Vector3.Up, x * 0.07f + v), p), 650f + v * 50f, spec);
            FlowBlock(c, p, new Vector3(rad * 2 + 1f, h, rad * 2 + 1f));
            Take(c, p, rad + 1.6f, rad + 1.6f);
        }
        // Réservoirs horizontaux (GPL / kérosène), serrés contre le merlon est.
        foreach (var z in new[] { 72f, 92f })
        {
            var p = new Vector3(194f, 0, z);
            var spec = new Destructibles.TankSpec { Wreck = Props.BulletTankWreck(), Blast = 15f, Damage = 360f, Core = Props.BulletAxisY, FireScale = 1.8f, FireY = 1.6f, Paint = TankSandPaint };
            Destructibles.AddTank(c.Root, c.Body, Props.BulletTank(z > 80f ? 1 : 0), new Transform3D(Basis.Identity, p), 320f, spec);
            FlowBlock(c, p, new Vector3(4f, 5f, Props.BulletLen));
            Take(c, p, 2.5f, Props.BulletLen / 2 + 0.5f);
        }
        // Merlons de terre (praticables), deux passages : rocade (ouest) et nord.
        var k = c.Solid.At(new Vector3(FuelBund.GetCenter().X, 0, FuelBund.GetCenter().Y));
        var x0 = FuelBund.Position.X;
        var z0 = FuelBund.Position.Y;
        var x1 = FuelBund.End.X;
        var z1 = FuelBund.End.Y;
        Berm(k, new Vector3(x0, 0, z0), new Vector3(x0, 0, 84f));
        Berm(k, new Vector3(x0, 0, 93f), new Vector3(x0, 0, z1));
        Berm(k, new Vector3(x0, 0, z1), new Vector3(x1, 0, z1));
        Berm(k, new Vector3(x1, 0, z1), new Vector3(x1, 0, z0));
        Berm(k, new Vector3(x1, 0, z0), new Vector3(178f, 0, z0));
        Berm(k, new Vector3(169f, 0, z0), new Vector3(x0, 0, z0));
        // Tuyauteries basses entre les cuves et le poste de chargement (relief non solide, supports solides).
        var m = Mats.DetailMetal();
        k.Col = new Color(0.32f, 0.3f, 0.26f);
        foreach (var (a, b) in new[] { (new Vector3(160, 0.9f, 80), new Vector3(160, 0.9f, 104)), (new Vector3(160, 0.9f, 92), new Vector3(192, 0.9f, 92)) })
        {
            k.Solid = false;
            k.Tube(m, a, b, 0.22f, 8);
            k.Tube(m, a + new Vector3(0.6f, 0, 0.6f), b + new Vector3(0.6f, 0, 0.6f), 0.15f, 8);
            k.Solid = true;
            var len = (b - a).Length();
            for (var t = 0f; t <= len; t += 4f)
                k.Box(Mats.Detail(), a + (b - a) / len * t + new Vector3(0.3f, -0.45f, 0.3f), new Vector3(1.6f, 0.9f, 0.4f));
        }
        // Citernes routières : une calcinée qui brûle encore, une intacte (destructible).
        var r = c.Ruins;
        var wp = new Vector3(162f, 0, 56f + 3f);
        r.Inst(Props.WreckTruck(), RuinKit.At(wp, 1.4f), true, 360f);
        r.BoxOnGround(wp, 1.4f, Props.WreckSize(Props.VehicleType.Truck, 0));
        r.Fire(wp + new Vector3(0, 1f, 0), 1.6f);
        Decal(c, Mats.Decal.Oil, wp + new Vector3(3, 0, 4), 12f, 0.6f, 0.03f, 1.5f);
        Decal(c, Mats.Decal.Scorch, wp, 14f, 0.2f, 0.028f);
        r.Take(wp, 6f);
        Vehicle(c, Props.MilTruck(), new Vector3(142f, 0, 106f), 0.05f, Props.VehicleType.Truck);
        // Fûts de carburant.
        for (var i = 0; i < 18; i++)
        {
            var p = new Vector3(158f + RuinKit.Hash(i, 1, 95) * 8f, 0, 112f + RuinKit.Hash(i, 2, 95) * 12f);
            if (!r.TryTake(p, 0.45f)) continue;
            if (RuinKit.Hash(i, 3, 95) < 0.25f) r.Inst(Props.Drum(0), RuinKit.At(p + Vector3.Up * 0.29f, RuinKit.Hash(i, 4, 95) * 6f, 0, Mathf.Pi / 2), true, 140f);
            else c.Props.Add(Props.Drum(0), p, 0, range: 140f, shadows: true);
        }
        Take(c, FuelBund, 0f);

        // Soutes à munitions en igloo, portes vers la rocade (nord).
        foreach (var x in BunkerXs)
        {
            var p = new Vector3(x, 0, BunkerZ);
            var spec = new Destructibles.TankSpec
            {
                Wreck = Props.AmmoBunker(true), Blast = 30f, Damage = 760f, Core = 3.5f, FireScale = 3.2f, FireY = 1.5f, Paint = new Color(0.5f, 0.48f, 0.43f),
            };
            Destructibles.AddTank(c.Root, c.Body, Props.AmmoBunker(false), new Transform3D(new Basis(Vector3.Up, Mathf.Pi), p), 950f, spec);
            var center = p + new Vector3(0, 0, -(Props.BunkerFront + Props.BunkerBack) / 2);
            FlowBlock(c, center, new Vector3(Props.BunkerHalfW * 2, Props.BunkerCrest, Props.BunkerFront - Props.BunkerBack));
            Take(c, center, Props.BunkerHalfW + 0.5f, (Props.BunkerFront - Props.BunkerBack) / 2 + 1f);
            // Caisses de munitions devant les portes.
            c.Props.Add(Props.AmmoCrates(2), p + new Vector3(4.5f, 0, -Props.BunkerFront - 3.5f), 0.3f, range: 160f, shadows: true);
            c.Props.Add(Props.AmmoCrates(0), p + new Vector3(-5f, 0, -Props.BunkerFront - 4.5f), 1.2f, range: 160f, shadows: true);
        }
        // Murs en T entre les soutes (traverses pare-éclats).
        foreach (var x in new[] { -171f, -141f })
            for (var z = BunkerZ - 9f; z <= BunkerZ + 1f; z += 1.55f)
                c.Props.Add(Props.TWall(1), new Vector3(x, 0, z), Mathf.Pi / 2, range: 230f, shadows: true);
    }

    /// <summary>Merlon de terre entre deux points (2,2 m, talus à ~35°), sable teinté, collision exacte.</summary>
    private static void Berm(MeshKit k, Vector3 a, Vector3 b)
    {
        var sand = Mats.DesertSand();
        var dir = (b - a).Normalized();
        var side = new Vector3(-dir.Z, 0, dir.X);
        const float h = 2.2f, top = 0.8f, foot = 3.5f;
        // Extrémités arrondies par un talus qui prolonge la crête.
        var a0 = a - dir * foot;
        var b0 = b + dir * foot;
        Vector3 P(Vector3 o, float s, float y) => o + side * s + Vector3.Up * y;
        var col = new Color(0.92f, 0.9f, 0.86f);
        k.Col = col;
        foreach (var sg in new[] { -1f, 1f })
            k.OrientedQuad(sand, P(a, sg * foot, 0), P(b, sg * foot, 0), P(b, sg * top, h), P(a, sg * top, h), side * sg + Vector3.Up * 0.6f);
        k.OrientedQuad(sand, P(a, -top, h), P(b, -top, h), P(b, top, h), P(a, top, h), Vector3.Up);
        foreach (var (o, end, d) in new[] { (a, a0, -dir), (b, b0, dir) })
        {
            k.OrientedQuad(sand, P(o, -top, h), P(o, top, h), end + side * top * 0.5f, end - side * top * 0.5f, d + Vector3.Up * 0.6f);
            k.Tri(sand, P(o, top, h), P(o, foot, 0), end + side * top * 0.5f, o);
            k.Tri(sand, P(o, -top, h), P(o, -foot, 0), end - side * top * 0.5f, o);
        }
    }
}
