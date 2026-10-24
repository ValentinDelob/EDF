using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Zone industrielle envahie : cuve éventrée qui brûle encore (colonne de fumée noire visible de partout), nappes
/// d'hydrocarbures et coulures toxiques, épaves de camions et citerne routière calcinée, véhicules du personnel,
/// fûts et conteneurs-citernes renversés, poste militaire à l'entrée du site, clôtures grillagées percées, toiles
/// entre les ouvrages, nids et œufs dans les cours, débris partout. Graines fixes et hachages de position.
/// </summary>
public static partial class IndustrialBuilder
{
    // ───────────────────────── Clôtures ─────────────────────────

    /// <summary>
    /// Ligne de clôture grillagée (panneaux destructibles de 3 m) : portails et brèches (hachage), panneaux
    /// affaissés, rien sur une emprise réservée.
    /// </summary>
    private static void FenceLine(Ctx c, Vector3 a, Vector3 b, int salt)
    {
        var dir = (b - a).Normalized();
        var len = (b - a).Length();
        var yaw = Mathf.Atan2(-dir.Z, dir.X);
        var n = Mathf.FloorToInt(len / 3f);
        for (var i = 0; i < n; i++)
        {
            var p = a + dir * (i * 3f + 1.5f);
            if (IsTaken(c, p, 1.2f)) continue;
            if (RoadCross(p, 1.5f)) continue;
            // Portails tous les ~45 m (deux panneaux), brèches au hasard.
            if (i % 15 is 7 or 8) continue;
            var h = RuinKit.Hash(i, salt, 71);
            if (h < 0.14f) continue;
            var v = h < 0.26f ? 1 : 0;
            c.Props.Add(Props.ChainFence(v), p, yaw + (v == 1 ? (RuinKit.Hash(i, salt, 72) - 0.5f) * 0.2f : 0f), range: 200f);
        }
    }

    /// <summary>Point sur une chaussée transversale (pas de clôture en travers d'une route).</summary>
    private static bool RoadCross(Vector3 p, float m)
    {
        var rh = RoadHalf + m;
        if (Mathf.Abs(p.X) < rh || Mathf.Abs(p.X + 118f) < rh && p.Z < 70f || Mathf.Abs(p.X - 118f) < rh && p.Z < -93f) return true;
        if (Mathf.Abs(p.Z + 100f) < rh || Mathf.Abs(p.Z - 62f) < rh) return true;
        if (Mathf.Abs(p.X - 150f) < 5f + m && p.Z > 55f) return true;
        return false;
    }

    private static void BuildFences(Ctx c)
    {
        const float o = RoadHalf + 1.6f;
        // Raffinerie (site classé) : tout le périmètre côté routes.
        FenceLine(c, new Vector3(o, 0, -228), new Vector3(o, 0, -100 - o), 1);
        FenceLine(c, new Vector3(o, 0, -100 - o), new Vector3(118 - o, 0, -100 - o), 2);
        FenceLine(c, new Vector3(118 - o, 0, -228), new Vector3(118 - o, 0, -100 - o), 3);
        FenceLine(c, new Vector3(118 + o, 0, -228), new Vector3(118 + o, 0, -100 - o), 4);
        FenceLine(c, new Vector3(118 + o, 0, -100 - o), new Vector3(228, 0, -100 - o), 5);
        // Ateliers (sud), gare (nord), parc à conteneurs (ouest), station d'épuration (nord).
        FenceLine(c, new Vector3(-228, 0, -100 - o), new Vector3(-o, 0, -100 - o), 6);
        FenceLine(c, new Vector3(o, 0, -100 + o), new Vector3(228, 0, -100 + o), 7);
        FenceLine(c, new Vector3(-118 + o, 0, -100 + o), new Vector3(-118 + o, 0, 62 - o), 8);
        FenceLine(c, new Vector3(-228, 0, 62 + o), new Vector3(-o, 0, 62 + o), 9);
        FenceLine(c, new Vector3(o, 0, 62 - o), new Vector3(228, 0, 62 - o), 10);
        // Bords du canal.
        FenceLine(c, new Vector3(-228, 0, CanalZ0 - 2.6f), new Vector3(228, 0, CanalZ0 - 2.6f), 11);
        FenceLine(c, new Vector3(-228, 0, CanalZ1 + 2.6f), new Vector3(228, 0, CanalZ1 + 2.6f), 12);
    }

    // ───────────────────────── Habillage ─────────────────────────

    private static void DressIndustrial(Ctx c)
    {
        var r = c.Ruins;
        var rng = r.Rng;
        foreach (var n in NestSpots) r.Block(n, 10f, 10f);

        // Cuve éventrée qui brûle depuis des jours : épave fixe, incendie géant, colonne de fumée noire.
        BurningTank(c, new Vector3(205, 0, -148));
        // Citerne routière calcinée et flaque en feu sur la route de la raffinerie.
        var tp = new Vector3(84, 0, -100);
        r.Inst(Props.WreckTruck(), RuinKit.At(tp, 1.75f), true, 360f);
        r.BoxOnGround(tp, 1.75f, Props.WreckSize(Props.VehicleType.Truck, 0));
        var trailer = tp + new Basis(Vector3.Up, 1.75f) * new Vector3(0, 0, -9.5f);
        r.Put(RuinKit.Layer.Solid, new Transform3D(new Basis(Vector3.Up, 1.75f) * new Basis(Vector3.Back, 0.06f), trailer), k => TankerTrailer(k));
        r.Box(new Transform3D(new Basis(Vector3.Up, 1.75f), trailer + Vector3.Up * 2.1f), new Vector3(2.6f, 2.6f, 10.5f));
        r.Fire(trailer + new Vector3(0, 0.3f, 3f), 2f);
        r.Decal(Mats.Decal.Oil, trailer + new Vector3(4, 0, 2), 14f, 0.4f, 0.03f, 1.5f);
        r.Decal(Mats.Decal.Scorch, trailer, 18f, 1.2f, 0.026f);
        r.Take(tp, 6f);
        r.Take(trailer, 6f);

        // Feux de flaques et fumées autour des citernes, nappes d'hydrocarbures.
        for (var i = 0; i < c.Tanks.Count; i++)
        {
            var t = c.Tanks[i];
            var a = RuinKit.Hash(i, 3, 73) * Mathf.Tau;
            var p = t + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (RuinKit.Hash(i, 4, 73) * 4f + 10f);
            if (RuinKit.Hash(i, 7, 73) < 0.45f) r.Decal(Mats.Decal.Oil, p with { Y = 0 }, 4f + RuinKit.Hash(i, 5, 73) * 5f, a, 0.03f + i * 0.0002f, 1.3f);
            if (RuinKit.Hash(i, 6, 73) < 0.18f && r.Free(p with { Y = 0 }, 2f)) r.Fire(p with { Y = 0.2f }, 1.1f, false);
        }

        // Cheminée qui fume encore (chaufferie) et panaches lointains.
        r.Smolder(new Vector3(-196, 59f, -121), 3.2f);

        // Poste militaire à l'entrée du site (au nord de l'aire de départ, route dégagée).
        Checkpoint(c, new Vector3(0, 0, -44));
        // Véhicules du personnel et camions sur les parkings et les quais.
        ParkingLot(c, new Vector3(-30, 0, 74), 0);
        ParkingLot(c, new Vector3(40, 0, 74), 1);
        foreach (var (x, z, yaw) in new[] { (-138f, -36f, 0f), (-150f, -28f, 0.1f), (-196f, -32f, Mathf.Pi), (90f, 44f, Mathf.Pi / 2), (190f, 108f, 0f), (-206f, 34f, Mathf.Pi) })
            Vehicle(c, new Vector3(x, 0, z), yaw, Props.VehicleType.Truck, new Color(0.5f, 0.5f, 0.48f), 7.4f);

        // Épaves dans les routes (de travers, calcinées).
        foreach (var (a, b) in new[] { (new Vector3(0, 0, -228), new Vector3(0, 0, -60)), (new Vector3(-228, 0, -100), new Vector3(228, 0, -100)), (new Vector3(-228, 0, 62), new Vector3(228, 0, 62)), (new Vector3(-118, 0, -228), new Vector3(-118, 0, 56)) })
        {
            var dir = (b - a).Normalized();
            var len = (b - a).Length();
            for (var s = 6f; s < len; s += 11f)
            {
                if (rng.Randf() > 0.13f) continue;
                var p = a + dir * s + new Vector3(-dir.Z, 0, dir.X) * rng.RandfRange(-4.5f, 4.5f);
                if (IsTaken(c, p, 3f) || !r.Free(p, 3.2f)) continue;
                var yaw = Mathf.Atan2(dir.X, dir.Z) + rng.RandfRange(-0.8f, 0.8f);
                var roll = rng.Randf();
                var type = roll < 0.2f ? Props.VehicleType.Van : roll < 0.38f ? Props.VehicleType.Truck : Props.VehicleType.Car;
                var v = rng.RandiRange(0, 3);
                var mesh = type switch { Props.VehicleType.Van => Props.WreckVan(v % 2), Props.VehicleType.Truck => Props.WreckTruck(), _ => Props.WreckCar(v) };
                r.Inst(mesh, RuinKit.At(p, yaw), true, 320f);
                r.BoxOnGround(p, yaw, Props.WreckSize(type, v));
                r.Take(p, 3.2f);
                r.Decal(Mats.Decal.Scorch, p, 7f, yaw, 0.026f, 0.8f);
                if (rng.Randf() < 0.15f) r.Smolder(p + Vector3.Up * 1.2f, 0.9f);
            }
        }

        // Fûts (certains renversés) et conteneurs-citernes près des ateliers et des quais.
        foreach (var (cx, cz, n) in new[] { (-140f, -48f, 14), (-140f, 24f, 10), (-120f, -130f, 12), (-45f, -138f, 8), (200f, 80f, 10), (104f, 60f, 6), (-36f, 96f, 8), (138f, -60f, 6) })
            for (var i = 0; i < n; i++)
            {
                var p = new Vector3(cx + rng.RandfRange(-7f, 7f), 0, cz + rng.RandfRange(-5f, 5f));
                if (IsTaken(c, p, 0.8f) || !r.Free(p, 0.7f) || RoadCross(p, 0f)) continue;
                var roll = rng.Randf();
                var yaw = rng.RandfRange(0, Mathf.Tau);
                if (roll < 0.22f)
                {
                    c.Props.Add(Props.Ibc(), p, yaw, range: 160f, shadows: true);
                    r.Take(p, 0.9f);
                    continue;
                }
                if (roll < 0.4f)
                {
                    // Fût couché (décor, rien de solide : il roulerait).
                    r.Inst(Props.Drum(0), RuinKit.At(p + Vector3.Up * 0.29f, yaw, 0, Mathf.Pi / 2), true, 140f);
                    r.Decal(Mats.Decal.Oil, p, 2.4f, yaw, 0.03f + i * 0.0003f);
                    continue;
                }
                c.Props.Add(Props.Drum(0), p, yaw, range: 140f, shadows: true);
                r.Take(p, 0.35f);
            }

        // Coulures toxiques autour des lagunes, hémolymphe et acide dans les cours.
        for (var i = 0; i < 14; i++)
        {
            var p = new Vector3(rng.RandfRange(-215f, -30f), 0, rng.RandfRange(72f, 126f));
            r.Decal(i % 2 == 0 ? Mats.Decal.Acid : Mats.Decal.Oil, p, rng.RandfRange(3f, 6f), rng.RandfRange(0, Mathf.Tau), 0.028f + i * 0.0002f, rng.RandfRange(0.8f, 1.6f));
        }
        // Marques au sol discrètes (les taches noires en grand nombre font « pois » de loin) : surtout fissures et cendres.
        for (var i = 0; i < 130; i++)
        {
            var p = new Vector3(rng.RandfRange(-218f, 218f), 0, rng.RandfRange(-218f, CanalZ0 - 3f));
            if (IsTaken(c, p, 1f) && rng.Randf() < 0.7f) continue;
            var roll = rng.Randf();
            var kind = roll < 0.42f ? Mats.Decal.Cracks : roll < 0.52f ? Mats.Decal.Oil : roll < 0.62f ? Mats.Decal.Ichor : roll < 0.74f ? Mats.Decal.Scorch : roll < 0.92f ? Mats.Decal.Ash : Mats.Decal.Crater;
            var size = kind is Mats.Decal.Scorch or Mats.Decal.Crater or Mats.Decal.Ash ? rng.RandfRange(5f, 10f) : kind == Mats.Decal.Cracks ? rng.RandfRange(4f, 9f) : rng.RandfRange(2f, 4.5f);
            r.Decal(kind, p, size, rng.RandfRange(0, Mathf.Tau), 0.02f + i * 0.0001f, rng.RandfRange(0.7f, 1.4f));
        }

        // Toiles géantes entre ouvrages voisins (silos et cheminées, palées du portique, sphères).
        Props.Web(r.Kit(RuinKit.Layer.Organic, new Vector3(-40, 0, -126)), new[]
        {
            new Vector3(-37.5f, 34f, -126f), new Vector3(-26.5f, 30f, -126f), new Vector3(-26f, 3f, -124.5f), new Vector3(-38f, 6f, -124.5f),
        }, 81);
        Props.Web(r.Kit(RuinKit.Layer.Organic, new Vector3(128, 0, -48)), new[]
        {
            new Vector3(122f, 14f, -48f), new Vector3(134f, 12f, -48f), new Vector3(133f, 2f, -46.5f), new Vector3(123f, 4f, -46.5f),
        }, 82);
        Props.Web(r.Kit(RuinKit.Layer.Organic, new Vector3(165, 0, -124)), new[]
        {
            new Vector3(158.5f, 11f, -122f), new Vector3(171.5f, 12f, -122f), new Vector3(170f, 1f, -119f), new Vector3(160f, 1.5f, -119f),
        }, 83);

        // Nids : œufs, résine et hémolymphe autour des emplacements.
        foreach (var n in NestSpots) NestGrowth(c, n);

        // Débris épars (sans collision ni ombres), plus denses au pied des bâtiments.
        Litter(c);
        r.DistantPlumes(new[]
        {
            new Vector3(-420, 30, -360), new Vector3(60, 40, -520), new Vector3(380, 30, -330),
            new Vector3(520, 25, 60), new Vector3(-500, 25, 40), new Vector3(-140, 10, 480), new Vector3(260, 10, 420),
        });
    }

    /// <summary>Cuve de stockage déjà détruite (décor fixe) : épave déchirée, incendie géant, fumée noire, suie au sol.</summary>
    private static void BurningTank(Ctx c, Vector3 p)
    {
        var mesh = Props.StorageTankWreck(1);
        c.Root.AddChild(new MeshInstance3D { Mesh = mesh, Position = p, Rotation = new Vector3(0, 0.7f, 0) });
        if (MeshKit.ShapeOf(mesh) is { } s) c.Body.AddChild(new CollisionShape3D { Shape = s, Position = p, Rotation = new Vector3(0, 0.7f, 0) });
        var (rad, h) = Props.StorageTankDims[1];
        c.Ruins.Fire(p + Vector3.Up * 1.4f, 4.2f);
        c.Ruins.Fire(p + new Vector3(3f, 2f, -2f), 3f, false);
        c.Root.AddChild(RuinFire.SmokeColumn(p + Vector3.Up * (h * 0.6f), 5f, true));
        c.Ruins.Decal(Mats.Decal.Scorch, p, rad * 3.4f, 0.3f, 0.025f);
        c.Ruins.Decal(Mats.Decal.Oil, p + new Vector3(rad + 3f, 0, 4f), 16f, 1.1f, 0.03f, 1.5f);
        FlowBlock(c, p, new Vector3(rad * 2 + 1f, h * 0.5f, rad * 2 + 1f));
        Take(c, p, rad + 1.6f, rad + 1.6f);
    }

    /// <summary>Semi-remorque citerne calcinée (pied à l'origine, longueur selon Z).</summary>
    private static void TankerTrailer(MeshKit k)
    {
        var ch = Mats.Charred();
        k.Col = new Color(0.14f, 0.13f, 0.12f);
        k.Box(ch, new Vector3(0, 1f, 0), new Vector3(1.2f, 0.3f, 11f));
        k.Col = new Color(0.32f, 0.22f, 0.17f);
        k.Cylinder(ch, new Vector3(0, 2.3f, -5f), 1.25f, 1.25f, 10f, 18, true, new Basis(Vector3.Right, Mathf.Pi / 2));
        k.Col = new Color(0.06f, 0.06f, 0.06f);
        foreach (var z in new[] { 2.8f, 4.2f })
            foreach (var x in new[] { -1f, 1f })
                k.Cylinder(ch, new Vector3(x - (x > 0 ? 0 : 0.35f), 0.5f, z), 0.5f, 0.5f, 0.35f, 10, true, new Basis(Vector3.Back, Mathf.Pi / 2));
    }

    /// <summary>Poste militaire : murs de gabions et sacs de sable en chicane, laissant la route ouverte.</summary>
    private static void Checkpoint(Ctx c, Vector3 p)
    {
        var r = c.Ruins;
        foreach (var sx in new[] { -1f, 1f })
        {
            for (var i = 0; i < 5; i++)
            {
                var q = p + new Vector3(sx * (9f + i * 1.12f), 0, 0);
                r.Inst(Props.Hesco(), RuinKit.At(q, 0), true, 260f);
                r.BoxOnGround(q, 0, new Vector3(1.1f, 1.3f, 1.1f));
            }
            var k = r.Kit(RuinKit.Layer.Solid, p);
            var a = p + new Vector3(sx * 6f, 0, -6f);
            var b = p + new Vector3(sx * 12f, 0, -6f);
            var (bc, bs, byaw) = Props.SandbagWall(k, a, b, 5, (int)(sx * 7));
            r.Box(new Transform3D(new Basis(Vector3.Up, byaw), bc), bs);
            r.Inst(Props.Barrier(sx > 0 ? 0 : 1), RuinKit.At(p + new Vector3(sx * 3.5f, 0, -12f), Mathf.Pi / 2 + sx * 0.2f), true, 240f);
            r.BoxOnGround(p + new Vector3(sx * 3.5f, 0, -12f), Mathf.Pi / 2 + sx * 0.2f, Props.BarrierSize);
        }
        Props.RazorWire(r.Kit(RuinKit.Layer.Solid, p), p + new Vector3(-22, 0, -3), p + new Vector3(-15, 0, -3));
        Props.RazorWire(r.Kit(RuinKit.Layer.Solid, p), p + new Vector3(15, 0, -3), p + new Vector3(22, 0, -3));
        var tp = p + new Vector3(18, 0, 8);
        r.Inst(Props.ArmyTruck(), RuinKit.At(tp, 0.3f), true, 320f);
        r.Box(new Transform3D(new Basis(Vector3.Up, 0.3f), tp + Vector3.Up * (Props.ArmyTruckSize.Y / 2)), Props.ArmyTruckSize);
        r.Take(p, 8f);
        r.Take(tp, 4f);
        Take(c, new Rect2(p.X - 16, p.Z - 8, 32, 10), 0f);
    }

    /// <summary>Véhicule garé destructible (voiture, fourgon, camion), comme en ville.</summary>
    private static void Vehicle(Ctx c, Vector3 p, float yaw, Props.VehicleType type, Color paint, float len)
    {
        if (!c.Ruins.TryTake(p, type == Props.VehicleType.Truck ? 4f : 2.6f)) return;
        if (Props.Burnt(p)) paint = Mats.BurntPaint;
        var mesh = Props.Vehicle(type, paint, len);
        var node = new MeshInstance3D { Mesh = mesh, Position = p, Rotation = new Vector3(0, yaw, 0), VisibilityRangeEnd = 260f };
        c.Root.AddChild(node);
        CityBuilder.AddVehicleProp(c.Body, node, mesh, type, len, p, yaw, paint);
    }

    private static readonly Color[] CarPaints =
    {
        new(0.5f, 0.5f, 0.51f), new(0.08f, 0.08f, 0.085f), new(0.33f, 0.13f, 0.11f), new(0.15f, 0.18f, 0.24f),
        new(0.6f, 0.6f, 0.58f), new(0.22f, 0.23f, 0.24f), new(0.3f, 0.28f, 0.2f),
    };

    /// <summary>Parking du personnel : places marquées, voitures abandonnées en rangs (certaines manquent ou ont brûlé).</summary>
    private static void ParkingLot(Ctx c, Vector3 p, int seed)
    {
        var g = c.Ground.At(p);
        g.Col = Colors.White;
        g.OrientedQuad(Mats.Asphalt(), p + new Vector3(-24, 0.013f, -8), p + new Vector3(24, 0.013f, -8), p + new Vector3(24, 0.013f, 14), p + new Vector3(-24, 0.013f, 14), Vector3.Up);
        g.Col = new Color(0.5f, 0.49f, 0.45f);
        foreach (var row in new[] { -4f, 10f })
            for (var x = -22.5f; x <= 22.5f; x += 2.6f)
                g.OrientedQuad(Mats.RoadPaint(), p + new Vector3(x - 0.05f, 0.017f, row - 2.5f), p + new Vector3(x + 0.05f, 0.017f, row - 2.5f), p + new Vector3(x + 0.05f, 0.017f, row + 2.5f), p + new Vector3(x - 0.05f, 0.017f, row + 2.5f), Vector3.Up);
        var i = 0;
        foreach (var row in new[] { -4f, 10f })
            for (var x = -21.2f; x <= 21.2f; x += 2.6f, i++)
            {
                var h = RuinKit.Hash(i, seed, 75);
                if (h < 0.35f) continue;
                var q = p + new Vector3(x, 0, row);
                var yaw = (row < 0 ? 0f : Mathf.Pi) + (RuinKit.Hash(i, seed, 76) - 0.5f) * 0.12f;
                if (h < 0.45f)
                {
                    var v = (int)(RuinKit.Hash(i, seed, 77) * 4);
                    c.Ruins.Inst(Props.WreckCar(v), RuinKit.At(q, yaw), true, 320f);
                    c.Ruins.BoxOnGround(q, yaw, Props.WreckSize(Props.VehicleType.Car, v));
                    c.Ruins.Take(q, 2.4f);
                    continue;
                }
                var type = h > 0.92f ? Props.VehicleType.Van : Props.VehicleType.Car;
                Vehicle(c, q, yaw, type, CarPaints[(int)(RuinKit.Hash(i, seed, 78) * CarPaints.Length)], 4.2f + RuinKit.Hash(i, seed, 79) * 0.5f);
            }
        Take(c, new Rect2(p.X - 24, p.Z - 8, 48, 22), 0f);
    }

    /// <summary>Œufs, résine et hémolymphe autour d'un emplacement de nid.</summary>
    private static void NestGrowth(Ctx c, Vector3 nest)
    {
        var r = c.Ruins;
        var seed = (int)(Mathf.Abs(nest.X) * 3 + Mathf.Abs(nest.Z));
        for (var i = 0; i < 4; i++)
        {
            var a = i * Mathf.Tau / 4 + RuinKit.Hash(seed, i, 2);
            var p = nest + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (10f + i);
            if (IsTaken(c, p, 1.5f)) continue;
            Props.EggCluster(r.Kit(RuinKit.Layer.Organic, p), p, 0.9f + RuinKit.Hash(seed, i, 3) * 0.6f, seed + i * 7);
            r.Decal(Mats.Decal.Acid, p, 5f, a, 0.028f);
        }
        for (var i = 0; i < 6; i++)
        {
            var a = RuinKit.Hash(seed, i, 4) * Mathf.Tau;
            var d = 4f + RuinKit.Hash(seed, i, 5) * 14f;
            var p = nest + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
            r.Decal(Mats.Decal.Ichor, p, 2.5f + RuinKit.Hash(seed, i, 6) * 3f, a, 0.026f + i * 0.0003f);
        }
    }

    /// <summary>Débris épars (MultiMesh, sans collision ni ombres).</summary>
    private static void Litter(Ctx c)
    {
        var r = c.Ruins;
        const float step = 2.8f;
        var idx = 0;
        for (var z = -Half + 2; z < CanalZ0 - 2; z += step)
        for (var x = -Half + 2; x < Half - 2; x += step, idx++)
        {
            var jx = x + (RuinKit.Hash(idx, 1, 6) - 0.5f) * step;
            var jz = z + (RuinKit.Hash(idx, 2, 6) - 0.5f) * step;
            var p = new Vector3(jx, 0, jz);
            var spawn = SpawnArea.HasPoint(new Vector2(jx, jz));
            if (RuinKit.Hash(idx, 3, 6) > (spawn ? 0.015f : 0.045f) || IsTaken(c, p, 0.3f) && !spawn) continue;
            var kind = (int)(RuinKit.Hash(idx, 4, 6) * Props.DebrisKinds);
            r.Inst(Props.Debris(kind), RuinKit.At(p, RuinKit.Hash(idx, 6, 6) * Mathf.Tau, 0, 0, 0.8f + RuinKit.Hash(idx, 7, 6) * 0.6f), false, 95f);
        }
    }
}
