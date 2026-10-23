using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Zone portuaire envahie : conteneurs calcinés et renversés, cuve en feu, nappe d'hydrocarbures,
/// remorqueur coulé contre le quai, épaves, position militaire abandonnée, toiles entre les piles
/// de conteneurs et sous les grues, œufs près des nids. Générateur à graine fixe, hachages de position.
/// </summary>
public static partial class PortBuilder
{
    private static void DressPort(Node3D root, StaticBody3D statics, CityBuilder.Result res, bool night)
    {
        var r = new RuinKit(root, statics, 0x9047, night);
        var rng = r.Rng;
        r.Block(new Rect2(-30, -30, 60, 60));
        var rects = new List<Rect2>();
        foreach (var b in res.Buildings)
        {
            var rect = new Rect2(b.Position.X - b.Size.X / 2, b.Position.Z - b.Size.Z / 2, b.Size.X, b.Size.Z);
            rects.Add(rect);
            r.Block(rect.Grow(0.6f));
        }
        // Cuves, grues, mâts.
        for (var i = 0; i < 4; i++) r.Block(new Vector3(150 + (i % 2) * 34, 0, 95 - (i / 2) * 34), 14.5f, 14.5f);
        for (var i = -1; i <= 1; i++) r.Block(new Vector3(i * 120, 0, CityBuilder.SeaStart - 18), 11f, 9f);
        for (var x = -180f; x <= 180f; x += 60f)
        for (var z = -120f; z <= 100f; z += 110f)
            r.Block(new Vector3(x, 0, z + 18), 1f, 1f);
        foreach (var n in res.NestSpots) r.Block(n, 9f, 9f);

        // Cuve en feu (grosse colonne noire) et cuve voisine noircie.
        var burning = new Vector3(150, 0, 95);
        r.Fire(burning + Vector3.Up * 18.6f, 3.2f);
        r.Root.AddChild(RuinFire.SmokeColumn(burning + Vector3.Up * 20f, 4f, true));
        r.SootCylinder(burning, 13f, 16f, 1);
        r.SootCylinder(new Vector3(184, 0, 95), 13f, 16f, 2);
        r.Decal(Mats.Decal.Oil, new Vector3(150, 0, 75), 16f, 0.4f, 0.03f, 1.6f);
        r.Decal(Mats.Decal.Scorch, new Vector3(166, 0, 80), 20f, 1.2f, 0.025f);

        // Hangars et piles de conteneurs : suie, conteneurs incendiés.
        foreach (var b in res.Buildings)
        {
            var h = RuinKit.Hash(b.Id, 31, 5);
            if (b.Size.X > 20f)
            {
                if (h < 0.75f) RuinKit.DamageFacades(b, h < 0.3f, b.Id);
                if (h < 0.4f) RubbleBy(r, b, rects);
            }
            else if (h < 0.35f) RuinKit.SootWalls(b, b.Size, b.Id, b.Size.Y);
        }

        // Conteneurs calcinés couchés dans les allées et sur les places libres.
        var placed = 0;
        for (var tries = 0; tries < 400 && placed < 16; tries++)
        {
            var p = new Vector3(rng.RandfRange(-195, 195), 0, rng.RandfRange(-110, 120));
            var yaw = rng.Randf() < 0.6f ? rng.RandfRange(-0.25f, 0.25f) : Mathf.Pi / 2 + rng.RandfRange(-0.3f, 0.3f);
            if (!r.Free(p, 7f)) continue;
            r.Take(p, 6.6f);
            var v = rng.RandiRange(0, 1);
            var onSide = rng.Randf() < 0.3f;
            var basis = new Basis(Vector3.Up, yaw);
            var origin = p;
            if (onSide)
            {
                basis *= new Basis(Vector3.Back, Mathf.Pi / 2);
                origin += new Basis(Vector3.Up, yaw) * new Vector3(1.3f, 1.3f, 0);
            }
            var xf = new Transform3D(basis, origin);
            r.Inst(Props.ContainerWreck(v), xf, true, 360f);
            r.Box(new Transform3D(basis, xf * new Vector3(0, 1.3f, 0)), new Vector3(2.6f, 2.6f, 12.2f));
            r.Decal(Mats.Decal.Scorch, p, 15f, yaw, 0.025f, 0.55f);
            if (placed == 2) r.Fire(p + Vector3.Up * 1.2f, 1.6f);
            else if (placed % 4 == 1) r.Smolder(p + Vector3.Up * 2.6f, 1.4f);
            placed++;
        }

        // Véhicules du port abandonnés (épaves, camion militaire).
        for (var tries = 0; tries < 300 && placed < 34; tries++)
        {
            var p = new Vector3(rng.RandfRange(-200, 200), 0, rng.RandfRange(-140, 130));
            if (!r.Free(p, 4f)) continue;
            var yaw = rng.RandfRange(0, Mathf.Tau);
            var roll = rng.Randf();
            var type = roll < 0.25f ? Props.VehicleType.Truck : roll < 0.45f ? Props.VehicleType.Van : Props.VehicleType.Car;
            var v = rng.RandiRange(0, 3);
            var size = Props.WreckSize(type, v);
            var mesh = type switch { Props.VehicleType.Van => Props.WreckVan(v % 2), Props.VehicleType.Truck => Props.WreckTruck(), _ => Props.WreckCar(v) };
            r.Inst(mesh, RuinKit.At(p, yaw), true, 320f);
            r.BoxOnGround(p, yaw, size);
            r.Take(p, size.Z / 2);
            r.Decal(Mats.Decal.Scorch, p, size.Z * 1.4f, yaw, 0.025f, 0.75f);
            placed++;
        }

        // Position militaire abandonnée à l'entrée du parc à conteneurs.
        MilitaryPost(r, new Vector3(-55, 0, 32));

        // Remorqueur coulé contre le quai, gîté ; nappes d'hydrocarbures sur l'eau.
        var boatP = new Vector3(48, -2.2f, CityBuilder.SeaStart + 19);
        var boatB = new Basis(Vector3.Up, 1.32f) * new Basis(Vector3.Back, 0.32f) * new Basis(Vector3.Right, -0.08f);
        r.Inst(Props.BoatWreck(), new Transform3D(boatB, boatP), true, 600f);
        r.Smolder(boatP + Vector3.Up * 8f, 2.4f);
        var water = r.Kit(RuinKit.Layer.Decal, new Vector3(0, 0, CityBuilder.SeaStart + 20));
        var slick = Mats.OilSlick();
        foreach (var (x, z, s, yaw) in new[] { (48f, 182f, 46f, 0.3f), (-30f, 175f, 30f, 1.2f), (130f, 190f, 38f, 2.1f), (-140f, 200f, 52f, 0.7f) })
        {
            var c = new Vector3(x, -2.43f, z);
            var b = new Basis(Vector3.Up, yaw);
            var hx = b * new Vector3(s * 0.7f, 0, 0);
            var hz = b * new Vector3(0, 0, s * 0.5f);
            water.Quad(slick, c - hx + hz, c + hx + hz, c + hx - hz, c - hx - hz, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0));
        }

        // Toiles géantes : entre les jambes des grues et entre piles de conteneurs voisines.
        for (var i = -1; i <= 1; i += 2)
        {
            var c = new Vector3(i * 120, 0, CityBuilder.SeaStart - 18);
            Props.Web(r.Kit(RuinKit.Layer.Organic, c), new[]
            {
                c + new Vector3(-9, 26, -7), c + new Vector3(9, 30, -7), c + new Vector3(9, 4, 7), c + new Vector3(-9, 9, 7),
            }, 70 + i);
        }
        var webs = 0;
        foreach (var a in res.Buildings)
        {
            if (webs >= 5 || a.Size.Y < 7f || a.Size.X > 20f || RuinKit.Hash(a.Id, 41, 5) > 0.25f) continue;
            foreach (var b in res.Buildings)
            {
                if (b == a || b.Size.Y < 5f || b.Size.X > 20f) continue;
                var d = b.Position - a.Position;
                if (Mathf.Abs(d.Z) > 1f || d.X < 25f || d.X > 40f) continue;
                var ax = a.Position.X + 6.1f;
                var bx = b.Position.X - 6.1f;
                var z = a.Position.Z;
                Props.Web(r.Kit(RuinKit.Layer.Organic, a.Position), new[]
                {
                    new Vector3(ax, a.Size.Y, z - 1.3f), new Vector3(ax, 0.3f, z + 1.3f), new Vector3(bx, 0.5f, z + 1.3f), new Vector3(bx, b.Size.Y, z - 1.3f),
                }, a.Id);
                webs++;
                break;
            }
        }

        // Résine et œufs près des nids, flaques d'hémolymphe.
        foreach (var n in res.NestSpots)
        {
            var seed = (int)(Mathf.Abs(n.X) * 3 + Mathf.Abs(n.Z));
            for (var i = 0; i < 4; i++)
            {
                var a = i * Mathf.Tau / 4 + RuinKit.Hash(seed, i, 2);
                var p = n + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (10f + i);
                Props.EggCluster(r.Kit(RuinKit.Layer.Organic, p), p, 0.9f + RuinKit.Hash(seed, i, 3) * 0.6f, seed + i * 7);
                r.Decal(Mats.Decal.Acid, p, 5f, a, 0.028f);
            }
            for (var i = 0; i < 3; i++)
            {
                var a = RuinKit.Hash(seed, i, 7) * Mathf.Tau;
                var p = n + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (16f + i * 3);
                Props.ResinBlob(r.Kit(RuinKit.Layer.Organic, p), p + Vector3.Up * 0.2f, new Vector3(2.4f, 1.1f, 1.8f), seed + 40 + i);
            }
        }

        // Marques au sol : fissures du béton, huile, hémolymphe, cendres.
        for (var i = 0; i < 160; i++)
        {
            var p = new Vector3(rng.RandfRange(-215, 215), 0, rng.RandfRange(-210, 150));
            var roll = rng.Randf();
            var kind = roll < 0.3f ? Mats.Decal.Cracks : roll < 0.5f ? Mats.Decal.Oil : roll < 0.68f ? Mats.Decal.Ichor : roll < 0.8f ? Mats.Decal.Scorch : roll < 0.9f ? Mats.Decal.Ash : Mats.Decal.Crater;
            var size = kind is Mats.Decal.Scorch or Mats.Decal.Crater or Mats.Decal.Ash ? rng.RandfRange(5f, 10f) : rng.RandfRange(2.5f, 8f);
            r.Decal(kind, p, size, rng.RandfRange(0, Mathf.Tau), 0.02f + i * 0.0001f, rng.RandfRange(0.7f, 1.4f));
        }

        // Débris épars.
        var idx = 0;
        for (var z = -215f; z < CityBuilder.SeaStart - 2; z += 2.6f)
        for (var x = -215f; x < 215f; x += 2.6f, idx++)
        {
            var jx = x + (RuinKit.Hash(idx, 1, 6) - 0.5f) * 2.6f;
            var jz = z + (RuinKit.Hash(idx, 2, 6) - 0.5f) * 2.6f;
            var inside = false;
            foreach (var b in rects)
                if (jx > b.Position.X - 0.2f && jx < b.End.X + 0.2f && jz > b.Position.Y - 0.2f && jz < b.End.Y + 0.2f) { inside = true; break; }
            if (inside || RuinKit.Hash(idx, 3, 6) > (Mathf.Abs(jx) < 20 && Mathf.Abs(jz) < 20 ? 0.02f : 0.06f)) continue;
            var kind = (int)(RuinKit.Hash(idx, 4, 6) * Props.DebrisKinds);
            r.Inst(Props.Debris(kind), RuinKit.At(new Vector3(jx, 0, jz), RuinKit.Hash(idx, 6, 6) * Mathf.Tau, 0, 0, 0.8f + RuinKit.Hash(idx, 7, 6) * 0.6f), false, 95f);
        }

        r.DistantPlumes(new[]
        {
            new Vector3(-330, 30, -260), new Vector3(40, 35, -430), new Vector3(300, 30, -300),
            new Vector3(430, 25, 20), new Vector3(-420, 25, -40), new Vector3(-120, 5, 460),
        });
        r.Flush();
        CityCam.Attach(root);
    }

    /// <summary>Tas de gravats et tôles contre un hangar.</summary>
    private static void RubbleBy(RuinKit r, Building b, List<Rect2> rects)
    {
        var side = RuinKit.Hash(b.Id, 32, 5) < 0.5f ? 1f : -1f;
        var c = new Vector3(b.Position.X + (RuinKit.Hash(b.Id, 33, 5) - 0.5f) * b.Size.X * 0.6f, 0, b.Position.Z + side * (b.Size.Z / 2 + 1.6f));
        var pts = Props.RubblePile(r.Kit(RuinKit.Layer.Solid, c), c, 0, new Vector3(7f, 1.8f, 4.5f), b.Id * 13, new Color(0.45f, 0.45f, 0.46f), false);
        r.Convex(pts);
        r.Take(c, 3.5f);
        r.Decal(Mats.Decal.Dust, c, 10f, 0.4f, 0.02f);
        if (RuinKit.Hash(b.Id, 34, 5) < 0.4f) r.Fire(c + Vector3.Up * 1.2f, 1.3f);
    }

    /// <summary>Position militaire : gabions, sacs de sable, camion bâché, séparateurs, brasero.</summary>
    private static void MilitaryPost(RuinKit r, Vector3 c)
    {
        var k = r.Kit(RuinKit.Layer.Solid, c);
        foreach (var (a, b) in new[] { (c + new Vector3(-4, 0, -3), c + new Vector3(4, 0, -3)), (c + new Vector3(4, 0, -3), c + new Vector3(4, 0, 2)) })
        {
            var (bc, bs, byaw) = Props.SandbagWall(k, a, b, 6, (int)(a.X + a.Z * 3));
            r.Box(new Transform3D(new Basis(Vector3.Up, byaw), bc), bs);
        }
        for (var i = 0; i < 6; i++)
        {
            var p = c + new Vector3(-10f + i * 1.12f, 0, -6f);
            r.Inst(Props.Hesco(), RuinKit.At(p, 0), true, 260f);
            r.BoxOnGround(p, 0, new Vector3(1.1f, 1.3f, 1.1f));
        }
        var tp = c + new Vector3(10, 0, 4);
        r.Inst(Props.ArmyTruck(), RuinKit.At(tp, 0.4f), true, 320f);
        r.Box(new Transform3D(new Basis(Vector3.Up, 0.4f), tp + Vector3.Up * (Props.ArmyTruckSize.Y / 2)), Props.ArmyTruckSize);
        for (var i = 0; i < 3; i++)
        {
            var p = c + new Vector3(-2f + i * 3.1f, 0, 7f);
            r.Inst(Props.Barrier(i % 2), RuinKit.At(p, Mathf.Pi / 2 + i * 0.1f), true, 240f);
            r.BoxOnGround(p, Mathf.Pi / 2 + i * 0.1f, Props.BarrierSize);
        }
        Props.RazorWire(k, c + new Vector3(-12, 0, 10), c + new Vector3(0, 0, 11));
        var bp = c + new Vector3(1, 0, 0);
        r.Inst(Props.Drum(1), RuinKit.At(bp, 0), true, 160f);
        r.BoxOnGround(bp, 0, new Vector3(0.6f, 0.88f, 0.6f));
        r.Fire(bp + Vector3.Up * 0.85f, 0.45f);
        r.Take(c, 12f);
    }
}
