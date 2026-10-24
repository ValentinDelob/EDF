using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Rues de la capitale et leur habillage de guerre : marquages, candélabres de fonte, arbres morts d'alignement, bancs,
/// colonnes d'affichage, voitures abandonnées ; barricades aux têtes de ponts (bus couchés, voitures, sacs de sable,
/// hérissons, barbelés), convois militaires calcinés sur l'avenue et le quai, cratères, gravats au pied des façades,
/// incendies d'étage, marques au sol, débris épars, panaches de fumée sur toute la ville.
/// </summary>
public static partial class CapitalBuilder
{
    private static readonly Color[] CarColors =
    {
        new(0.5f, 0.5f, 0.51f), new(0.08f, 0.08f, 0.085f), new(0.33f, 0.13f, 0.11f), new(0.15f, 0.18f, 0.24f),
        new(0.6f, 0.6f, 0.58f), new(0.22f, 0.23f, 0.24f), new(0.3f, 0.28f, 0.2f),
    };

    private static void BuildStreets(Ctx c)
    {
        var deco = new RandomNumberGenerator { Seed = 8181 };
        var vrng = new RandomNumberGenerator { Seed = 8282 };
        // Trottoirs des îlots : candélabres, arbres morts, bancs et colonnes, voitures le long des bordures.
        foreach (var r in Blocks)
        {
            var h = r.Size / 2;
            var ctr = new Vector3(r.GetCenter().X, Walk, r.GetCenter().Y);
            var sides = new (Vector3 o, Vector3 along, Vector3 n, float len)[]
            {
                (ctr + new Vector3(-h.X, 0, -h.Y), Vector3.Right, Vector3.Forward, r.Size.X),
                (ctr + new Vector3(-h.X, 0, h.Y), Vector3.Right, Vector3.Back, r.Size.X),
                (ctr + new Vector3(-h.X, 0, -h.Y), Vector3.Back, Vector3.Left, r.Size.Y),
                (ctr + new Vector3(h.X, 0, -h.Y), Vector3.Back, Vector3.Right, r.Size.Y),
            };
            foreach (var (o, along, n, len) in sides)
            {
                var yawRoad = Mathf.Atan2(n.X, n.Z);
                var nl = Mathf.Max(1, Mathf.RoundToInt(len / 24f));
                for (var i = 0; i < nl; i++)
                {
                    var p = o + along * ((i + 0.5f) * len / nl) - n * 0.7f;
                    if (Clear(c, p, 0.4f)) StreetLamp(c, p, yawRoad);
                }
                for (var a = 9f; a < len - 9f; a += 12f)
                {
                    var p = o + along * a - n * 1.7f;
                    if (!Clear(c, p, 1f) || deco.Randf() < 0.45f) continue;
                    if (!c.Ruins.TryTake(p, 1.4f)) continue;
                    c.Props.Add(Props.DeadTree(deco.RandiRange(0, 3)), p, deco.RandfRange(0, Mathf.Tau), null, 320, true, deco.RandfRange(0.75f, 0.95f));
                }
                if (deco.Randf() < 0.3f)
                {
                    var p = o + along * deco.RandfRange(8f, len - 8f) - n * 1.6f;
                    if (Clear(c, p, 1.2f) && c.Ruins.TryTake(p, 1.4f))
                        c.Props.Add(deco.Randf() < 0.5f ? Props.MorrisColumn() : Props.Kiosk(), p, yawRoad + Mathf.Pi, range: 220, shadows: true);
                }
                var cars = vrng.RandiRange(0, 2);
                for (var i = 0; i < cars; i++)
                {
                    var a = vrng.RandfRange(9f, len - 9f);
                    var roll = vrng.Randf();
                    var type = roll < 0.1f ? Props.VehicleType.Truck : roll < 0.3f ? Props.VehicleType.Van : Props.VehicleType.Car;
                    var vlen = vrng.RandfRange(4.0f, 4.8f);
                    var col = CarColors[vrng.RandiRange(0, CarColors.Length - 1)];
                    var size = Props.VehicleSize(type, vlen);
                    var p = o + along * a + n * (0.35f + size.X / 2);
                    p.Y = 0;
                    if (GroundY(c, p.X, p.Z) > 0.05f) continue;
                    var yaw = Mathf.Atan2(along.X, along.Z) + (vrng.Randf() < 0.5f ? 0 : Mathf.Pi) + vrng.RandfRange(-0.06f, 0.06f);
                    ParkVehicle(c, p, yaw, type, col, vlen, vrng);
                }
            }
        }
        // Quais : candélabres sur le trottoir côté fleuve, arbres morts côté ville.
        for (var x = -224f; x < 230f; x += 22f)
        {
            if (NearBridge(x, 3f)) continue;
            StreetLamp(c, new Vector3(x, Walk, 81.2f), 0f);
            StreetLamp(c, new Vector3(x + 11f, Walk, 130.8f), Mathf.Pi);
            var tp = new Vector3(x + 6f, Walk, 64f);
            if (c.Ruins.TryTake(tp, 1.4f)) c.Props.Add(Props.DeadTree((int)(RuinKit.Hash(x, 2, 541) * 4)), tp, x, null, 320, true, 0.9f);
        }
        // Avenue et boulevard : arbres d'alignement le long des chaussées.
        for (var z = -180f; z < -64f; z += 10f)
            foreach (var x in new[] { -18f, 18f })
            {
                var p = new Vector3(x, GroundY(c, x, z), z);
                if (p.Y < 0.1f || !Clear(c, p, 1f) || !c.Ruins.TryTake(p, 1.4f)) continue;
                c.Props.Add(Props.DeadTree((int)(RuinKit.Hash(x, z, 542) * 4)), p, z, null, 320, true, 0.9f);
            }
        for (var x = -224f; x < 230f; x += 16f)
        {
            if (Mathf.Abs(x) < 20f) continue;
            StreetLamp(c, new Vector3(x + 8f, Walk, MedianZ0 + 0.6f), Mathf.Pi);
        }
        BuildRoadMarkings(c);
    }

    private static bool NearBridge(float x, float margin)
    {
        foreach (var (bx, half, _) in Bridges)
            if (Mathf.Abs(x - bx) < half + margin) return true;
        return false;
    }

    /// <summary>Candélabre de rue (une lanterne), parfois abattu ; éclairé la nuit au hasard (réseau coupé).</summary>
    private static void StreetLamp(Ctx c, Vector3 p, float yaw)
    {
        var r = c.Ruins;
        var fate = RuinKit.Hash(p.X, p.Z, 551);
        if (fate < 0.16f && r.Free(p, 0.5f))
        {
            var fall = yaw + (RuinKit.Hash(p.X, p.Z, 552) - 0.5f) * 1.4f;
            var basis = new Basis(Vector3.Up, fall) * new Basis(Vector3.Right, Mathf.Pi / 2 - 0.04f) * new Basis(Vector3.Up, Mathf.Pi / 2);
            r.Inst(Props.CapitalLamp(false, false), new Transform3D(basis, p + Vector3.Up * 0.3f), true, 260f);
            var dir = new Basis(Vector3.Up, fall) * Vector3.Back;
            r.Box(new Transform3D(new Basis(Vector3.Up, fall), p + dir * 2.8f + Vector3.Up * 0.3f), new Vector3(0.5f, 0.5f, 5.6f));
            return;
        }
        if (!r.TryTake(p, 0.6f)) return;
        var lit = c.Night && fate > 0.7f && c.LitLamps < 24;
        var id = c.Props.Add(Props.CapitalLamp(lit, false), p, yaw, range: 260, shadows: true);
        if (lit)
        {
            c.LitLamps++;
            var head = p + new Basis(Vector3.Up, yaw) * new Vector3(0, 5.2f, 0.62f);
            var light = new OmniLight3D { Position = head, LightColor = new Color(1f, 0.78f, 0.5f), LightEnergy = 2.6f, OmniRange = 18f, ShadowEnabled = false };
            c.Root.AddChild(light);
            Destructibles.AddExtra(id, light);
        }
        var cs = new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.2f, Height = 5.4f }, Position = p + Vector3.Up * 2.7f };
        if (id >= 0) Destructibles.AddShape(id, cs);
        else c.Body.AddChild(cs);
    }

    /// <summary>Véhicule abandonné le long du trottoir : épave calcinée une fois sur deux, sinon intact (destructible).</summary>
    private static void ParkVehicle(Ctx c, Vector3 p, float yaw, Props.VehicleType type, Color paint, float len, RandomNumberGenerator vrng)
    {
        var size = Props.VehicleSize(type, len);
        if (!c.Ruins.Free(p, size.Z * 0.5f)) return;
        paint = paint.Lerp(new Color(0.33f, 0.32f, 0.3f), 0.55f).Darkened(0.12f);
        var fate = RuinKit.Hash(p.X, p.Z, 561);
        if (fate < 0.5f)
        {
            PlaceWreck(c, p, yaw, type, (int)(RuinKit.Hash(p.X, p.Z, 562) * 4), 0, fate < 0.04f ? 1 : 0);
            return;
        }
        c.Ruins.Take(p, size.Z * 0.5f);
        if (Props.Burnt(p)) paint = Mats.BurntPaint;
        var mesh = Props.Vehicle(type, paint, len);
        var node = new MeshInstance3D { Mesh = mesh, Position = p, Rotation = new Vector3(0, yaw, 0), VisibilityRangeEnd = 260f };
        c.Root.AddChild(node);
        CityBuilder.AddVehicleProp(c.Body, node, mesh, type, len, p, yaw, paint);
    }

    /// <summary>Épave : <paramref name="pose"/> 0 sur roues, 1 sur le toit, 2 sur le flanc ; <paramref name="burn"/> 0, 1 fumante, 2 en flammes.</summary>
    private static void PlaceWreck(Ctx c, Vector3 p, float yaw, Props.VehicleType type, int v, int pose, int burn)
    {
        var r = c.Ruins;
        var size = Props.WreckSize(type, v);
        var mesh = type switch { Props.VehicleType.Van => Props.WreckVan(v % 2), Props.VehicleType.Truck => Props.WreckTruck(), _ => Props.WreckCar(v) };
        if (type != Props.VehicleType.Car) pose = 0;
        var b = new Basis(Vector3.Up, yaw);
        var origin = p;
        if (pose == 1) { b *= new Basis(Vector3.Back, Mathf.Pi); origin += Vector3.Up * (size.Y + 0.02f); }
        else if (pose == 2) { b *= new Basis(Vector3.Back, Mathf.Pi / 2); origin += new Basis(Vector3.Up, yaw) * new Vector3(size.Y * 0.5f, size.X * 0.5f, 0); }
        var xf = new Transform3D(b, origin);
        r.Inst(mesh, xf, true, 320f);
        r.Box(new Transform3D(b, xf * new Vector3(0, size.Y / 2, 0)), size);
        r.Take(p, size.Z * 0.5f);
        r.Decal(Mats.Decal.Scorch, p, size.Z * 1.5f, yaw, p.Y + 0.025f, 0.75f);
        if (burn == 2) r.Fire(p + new Basis(Vector3.Up, yaw) * new Vector3(0, size.Y * 0.45f, size.Z * 0.25f), type == Props.VehicleType.Car ? 1f : 1.4f);
        else if (burn == 1) r.Smolder(p + Vector3.Up * size.Y * 0.6f, 0.9f);
    }

    // ───────────────────────── Marquages ─────────────────────────

    private static void BuildRoadMarkings(Ctx c)
    {
        var m = Mats.RoadPaint();
        void P(float x0, float z0, float x1, float z1)
        {
            var k = c.Ground.At(new Vector3((x0 + x1) / 2, 0, (z0 + z1) / 2));
            k.Col = Colors.White;
            k.Quad(m, new Vector3(x0, PaintY, z1), new Vector3(x1, PaintY, z1), new Vector3(x1, PaintY, z0), new Vector3(x0, PaintY, z0));
        }
        // Avenue : ligne axiale discontinue, passages piétons devant la place et le rond-point.
        for (var z = -156f; z < -66f; z += 9f) P(-0.15f, z, 0.15f, z + 4f);
        for (var x = -14f; x < 14f; x += 1.1f) { P(x, -68f, x + 0.55f, -64f); P(x, -164f, x + 0.55f, -160f); }
        // Boulevard : lignes de rive des deux chaussées.
        P(-262f, BlvdZ0 + 0.6f, 262f, BlvdZ0 + 0.75f);
        P(-262f, BlvdZ1 - 0.75f, 262f, BlvdZ1 - 0.6f);
        for (var x = -258f; x < 258f; x += 9f) { P(x, (BlvdZ0 + MedianZ0) / 2 - 0.08f, x + 4f, (BlvdZ0 + MedianZ0) / 2 + 0.08f); P(x, (BlvdZ1 + MedianZ1) / 2 - 0.08f, x + 4f, (BlvdZ1 + MedianZ1) / 2 + 0.08f); }
        // Quais : ligne axiale.
        for (var x = -258f; x < 258f; x += 9f) { P(x, 72.9f, x + 4f, 73.1f); P(x, 137.9f, x + 4f, 138.1f); }
    }

    // ───────────────────────── Habillage de guerre ─────────────────────────

    private static void DressCapital(Ctx c)
    {
        var r = c.Ruins;
        var rng = r.Rng;
        foreach (var n in NestSpots) r.Block(n, 9f, 9f);

        Barricade(c, new Vector3(0, 0, 70f), false);
        Barricade(c, new Vector3(0, 0, 131f), true); // devant l'emmarchement du parlement, pas dessus
        Convoy(c);
        QuayConvoy(c);
        FloodLights(c);

        // Immeubles : incendies d'étage, panaches noirs, gravats contre les façades.
        var fires = 0;
        foreach (var b in c.Res.Buildings)
        {
            var hb = RuinKit.Hash(b.Id, 3, 7);
            if (hb < 0.16f && fires < 18)
            {
                fires++;
                var side = RuinKit.Hash(b.Id, 4, 7) < 0.5f ? 1f : -1f;
                var fy = Mats.ShopStoryH + Mathf.Floor(RuinKit.Hash(b.Id, 5, 7) * 4f) * Mats.StoryH + 1.2f;
                var fp = b.Position + new Vector3((RuinKit.Hash(b.Id, 6, 7) - 0.5f) * b.Size.X * 0.5f, Mathf.Min(fy, b.Size.Y - 2f), side * (b.Size.Z / 2 + 0.5f));
                r.Fire(fp, 1.3f, r.Lights < 6);
                if (hb < 0.08f) r.Smolder(b.Position + Vector3.Up * (b.Size.Y + 4f), 2.4f);
            }
            if (RuinKit.Hash(b.Id, 8, 7) < 0.28f) RubbleAt(c, b);
        }

        // Épaves dans les rues (de travers, parfois retournées).
        for (var i = 0; i < 70; i++)
        {
            var p = RandomStreetPoint(c, rng);
            if (p.Y > 0.05f) continue;
            TryWreck(c, p, rng.RandfRange(0, Mathf.Tau), 3.4f);
        }
        // Cratères d'obus sur la chaussée et la place.
        var craters = 0;
        for (var tries = 0; tries < 120 && craters < 22; tries++)
        {
            var p = RandomStreetPoint(c, rng);
            if (!r.Free(p, 4.5f)) continue;
            r.Take(p, 3.5f);
            Crater(c, p, rng.RandfRange(5f, 10f));
            craters++;
        }
        // Marques au sol : fissures, suie, cendres, hémolymphe, acide, huile, poussière.
        for (var i = 0; i < 260; i++)
        {
            var p = RandomStreetPoint(c, rng);
            if (p.X < ParkX1 && p.Z > ParkZ0 && p.Z < ParkZ1) continue; // herbe du jardin : pas de marques (taches claires ou noires)
            var roll = rng.Randf();
            // Peu de suie et de cendres : sur la pierre claire et les pavés, elles font des taches noires vues de loin.
            var kind = roll < 0.42f ? Mats.Decal.Cracks : roll < 0.56f ? Mats.Decal.Ichor : roll < 0.64f ? Mats.Decal.Acid : roll < 0.74f ? Mats.Decal.Oil
                : roll < 0.8f ? Mats.Decal.Ash : roll < 0.85f ? Mats.Decal.Scorch : Mats.Decal.Dust;
            var size = kind switch { Mats.Decal.Cracks => rng.RandfRange(4f, 9f), Mats.Decal.Scorch or Mats.Decal.Ash => rng.RandfRange(3f, 6f), _ => rng.RandfRange(1.8f, 4.5f) };
            r.Decal(kind, p, size, rng.RandfRange(0, Mathf.Tau), p.Y + 0.02f + i * 0.0002f, rng.RandfRange(0.7f, 1.3f));
        }
        // Traces d'exode : valises, matelas, canapés.
        for (var i = 0; i < 50; i++)
        {
            var p = RandomStreetPoint(c, rng);
            if (p.Y < 0.1f || !r.Free(p, 1.2f) || !Clear(c, p, 0.8f)) continue;
            var roll = rng.Randf();
            var yaw = rng.RandfRange(0, Mathf.Tau);
            if (roll < 0.55f) r.Inst(Props.Suitcase(), RuinKit.At(p, yaw), false, 110f);
            else if (roll < 0.8f) r.Inst(Props.Mattress(), RuinKit.At(p, yaw), false, 140f);
            else
            {
                r.Inst(Props.Sofa(), RuinKit.At(p, yaw), true, 160f);
                r.BoxOnGround(p, yaw, new Vector3(1.9f, 0.85f, 0.85f));
                r.Take(p, 1.2f);
            }
        }
        // Nids (missions de portails/nids) : flaques d'hémolymphe autour.
        foreach (var n in NestSpots)
        {
            r.Decal(Mats.Decal.Crater, n, 16f, n.X, n.Y + 0.03f);
            r.Decal(Mats.Decal.Scorch, n, 26f, n.Z, n.Y + 0.022f);
        }
        Litter(c);
        // Panaches lointains : toute la ville brûle.
        r.DistantPlumes(new[]
        {
            new Vector3(-460, 30, -420), new Vector3(-120, 35, -560), new Vector3(280, 30, -470), new Vector3(560, 25, -120),
            new Vector3(-580, 25, 80), new Vector3(240, 10, 520), new Vector3(-320, 10, 460), new Vector3(620, 20, 260),
            new Vector3(40, 40, -720), new Vector3(-700, 20, -260),
        });
    }

    /// <summary>Point au hasard sur une chaussée, un trottoir ou la place (hors aire de départ et fleuve).</summary>
    private static Vector3 RandomStreetPoint(Ctx c, RandomNumberGenerator rng)
    {
        for (var i = 0; i < 12; i++)
        {
            var p = OnGround(c, rng.RandfRange(-Half + 4, Half - 4), rng.RandfRange(-Half + 4, South - 6));
            if (SpawnArea.HasPoint(new Vector2(p.X, p.Z))) continue;
            if (p.Y < -0.5f) continue;
            if (!Clear(c, p, 0.5f)) continue;
            if (p.X < ParkX1 && p.X > ParkX0 - 40f && p.Z > ParkZ0 && p.Z < ParkZ1 && rng.Randf() < 0.7f) continue;
            return p;
        }
        return OnGround(c, 0, -100);
    }

    private static bool TryWreck(Ctx c, Vector3 p, float yaw, float radius, int burn = -1)
    {
        var r = c.Ruins;
        if (!r.Free(p, radius)) return false;
        var roll = r.Rng.Randf();
        var type = roll < 0.12f ? Props.VehicleType.Van : roll < 0.18f ? Props.VehicleType.Truck : Props.VehicleType.Car;
        var pose = r.Rng.Randf() switch { < 0.12f => 1, < 0.2f => 2, _ => 0 };
        var b = burn >= 0 ? burn : r.Rng.Randf() < 0.05f ? 1 : 0;
        PlaceWreck(c, p, yaw, type, r.Rng.RandiRange(0, 3), pose, b);
        return true;
    }

    private static void Crater(Ctx c, Vector3 p, float size)
    {
        var r = c.Ruins;
        var yaw = r.Rng.RandfRange(0, Mathf.Tau);
        r.Decal(Mats.Decal.Crater, p, size, yaw, p.Y + 0.03f);
        r.Decal(Mats.Decal.Scorch, p, size * 1.3f, yaw + 1.3f, p.Y + 0.022f);
        var k = r.Kit(RuinKit.Layer.Solid, p);
        var d = Mats.Detail();
        var n = 12 + (int)(size * 1.5f);
        for (var i = 0; i < n; i++)
        {
            var a = i * Mathf.Tau / n + r.Rng.RandfRange(-0.2f, 0.2f);
            var rad = size * r.Rng.RandfRange(0.22f, 0.32f);
            var q = p + new Vector3(Mathf.Cos(a) * rad, 0, Mathf.Sin(a) * rad);
            var s = r.Rng.RandfRange(0.3f, 0.75f);
            k.Col = new Color(0.12f, 0.12f, 0.12f).Lerp(new Color(0.34f, 0.31f, 0.27f), r.Rng.Randf());
            k.Box(d, q + Vector3.Up * 0.08f, new Vector3(s, 0.14f, s * 0.7f), new Basis(Vector3.Up, -a) * new Basis(Vector3.Back, r.Rng.RandfRange(0.25f, 0.7f)));
        }
        c.HotSpots.Add((p, size * 1.2f, 0.5f));
    }

    /// <summary>Tas de gravats contre une façade d'immeuble (pierre de taille éclatée), avec poussière.</summary>
    private static void RubbleAt(Ctx c, Building b)
    {
        var r = c.Ruins;
        var side = (int)(RuinKit.Hash(b.Id, 5, 9) * 4);
        var s = b.Size;
        var (n, along, len) = side switch
        {
            0 => (Vector3.Back, Vector3.Right, s.X),
            1 => (Vector3.Forward, Vector3.Right, s.X),
            2 => (Vector3.Right, Vector3.Back, s.Z),
            _ => (Vector3.Left, Vector3.Back, s.Z),
        };
        var half = side < 2 ? s.Z / 2 : s.X / 2;
        var w = Mathf.Min(len * 0.8f, 4f + RuinKit.Hash(b.Id, 6, 9) * 4f);
        var depth = 2.6f + RuinKit.Hash(b.Id, 7, 9) * 1.6f;
        var hgt = 1f + RuinKit.Hash(b.Id, 8, 9) * 1.2f;
        var off = (RuinKit.Hash(b.Id, 9, 9) - 0.5f) * Mathf.Max(0, len - w) * 0.8f;
        var ctr = new Vector3(b.Position.X, 0, b.Position.Z) + n * (half + depth * 0.32f) + along * off;
        ctr = OnGround(c, ctr.X, ctr.Z);
        if (ctr.Y < -0.5f || !Clear(c, ctr, -0.2f) || SpawnArea.Grow(4f).HasPoint(new Vector2(ctr.X, ctr.Z))) return;
        var yaw = Mathf.Atan2(n.X, n.Z);
        var tint = new Color(0.6f, 0.56f, 0.48f);
        var pts = Props.RubblePile(r.Kit(RuinKit.Layer.Solid, ctr), ctr, yaw, new Vector3(w, hgt, depth), b.Id * 7 + 1, tint, false);
        r.Convex(pts);
        r.Take(ctr, Mathf.Max(w, depth) * 0.5f);
        r.Decal(Mats.Decal.Dust, ctr, Mathf.Max(w, depth) * 1.7f, yaw, ctr.Y + 0.02f);
        c.HotSpots.Add((ctr, Mathf.Max(w, depth) * 1.1f, 0.45f));
    }

    /// <summary>
    /// Barricade de tête de pont en travers du quai : bus couché, voitures, nids de sacs de sable, hérissons, barbelés,
    /// passage en chicane au milieu ; <paramref name="south"/> : face au nord (rive sud).
    /// </summary>
    private static void Barricade(Ctx c, Vector3 ctr, bool south)
    {
        var r = c.Ruins;
        var f = south ? -1f : 1f;
        // Bus en travers à l'ouest, voitures empilées à l'est, chicane au centre.
        var bp = ctr + new Vector3(-11f, 0, 0);
        var bb = new Basis(Vector3.Up, Mathf.Pi / 2 + 0.1f) * new Basis(Vector3.Back, Mathf.Pi / 2 - 0.05f);
        var bo = bp + Vector3.Up * 1.3f;
        r.Inst(Props.WreckBus(), new Transform3D(bb, bo), true, 360f);
        r.Box(new Transform3D(bb, bo + bb * new Vector3(0, 1.4f, -0.3f)), new Vector3(2.5f, 3.1f, 11.2f));
        r.Take(bp, 6.5f);
        PlaceWreck(c, ctr + new Vector3(9f, 0, 0.4f), Mathf.Pi / 2 - 0.2f, Props.VehicleType.Car, 1, 2, 0);
        PlaceWreck(c, ctr + new Vector3(13.6f, 0, -0.6f), Mathf.Pi / 2 + 0.3f, Props.VehicleType.Car, 3, 0, 1);
        PlaceWreck(c, ctr + new Vector3(18f, 0, 0.2f), Mathf.Pi / 2, Props.VehicleType.Van, 0, 0, 0);
        foreach (var (x, dz, yaw) in new[] { (-3.5f, -1.4f, 0.1f), (3.5f, 1.4f, -0.1f) })
        {
            var p = ctr + new Vector3(x, 0, dz * f);
            r.Inst(Props.Barrier(0), RuinKit.At(p, Mathf.Pi / 2 + yaw), true, 240f);
            r.BoxOnGround(p, Mathf.Pi / 2 + yaw, Props.BarrierSize);
            r.Take(p, 1.5f);
        }
        // Nids de sacs de sable côté défenseurs, hérissons et barbelés côté assaillants.
        foreach (var sx in new[] { -1f, 1f })
        {
            var cc = ctr + new Vector3(sx * 6.5f, 0, -3.5f * f);
            var k = r.Kit(RuinKit.Layer.Solid, cc);
            foreach (var (p0, p1) in new[] { (cc + new Vector3(-1.8f, 0, 1.6f * f), cc + new Vector3(1.8f, 0, 1.6f * f)), (cc + new Vector3(sx * 1.8f, 0, 1.6f * f), cc + new Vector3(sx * 1.8f, 0, -1.6f * f)) })
            {
                var (bc, bs, byaw) = Props.SandbagWall(k, p0, p1, 6, (int)(p0.X * 3 + p0.Z));
                r.Box(new Transform3D(new Basis(Vector3.Up, byaw), bc), bs);
            }
            r.Take(cc, 2.6f);
        }
        var kit = r.Kit(RuinKit.Layer.Solid, ctr);
        Props.RazorWire(kit, ctr + new Vector3(-8f, 0, 5f * f), ctr + new Vector3(-1.5f, 0, 5.4f * f));
        Props.RazorWire(kit, ctr + new Vector3(1.5f, 0, 5.2f * f), ctr + new Vector3(8f, 0, 4.8f * f));
        for (var i = 0; i < 4; i++)
        {
            var p = ctr + new Vector3(-9f + i * 6f, 0, 8.5f * f);
            r.Inst(Props.Hedgehog(), RuinKit.At(p, i * 0.7f), true, 220f);
            r.BoxOnGround(p, i * 0.7f, new Vector3(1.3f, 1.2f, 1.3f));
            r.Take(p, 1f);
        }
        r.Decal(Mats.Decal.Scorch, ctr + new Vector3(2f, 0, 4f * f), 10f, 0.8f, 0.026f);
        r.Decal(Mats.Decal.Ichor, ctr + new Vector3(-4f, 0, 9f * f), 6f, 2.1f, 0.03f);
        r.Fire(ctr + new Vector3(14f, 1f, 0), 1.2f, r.Lights < 6);
        c.HotSpots.Add((ctr, 20f, 0.35f));
    }

    /// <summary>Convoi militaire anéanti sur l'avenue : chars, blindés et camions calcinés en file, certains encore en feu.</summary>
    private static void Convoy(Ctx c)
    {
        var r = c.Ruins;
        var list = new (float x, float z, float yaw, int kind)[]
        {
            (-5f, -84f, 0.08f, 0), (5.5f, -96f, -0.3f, 2), (-4f, -127f, 0.2f, 1), (6f, -139f, -0.05f, 3),
            (-6f, -151f, 0.5f, 2), (4f, -166f, -0.15f, 0), (-2f, -178f, 1.2f, 3),
        };
        foreach (var (x, z, yaw, kind) in list)
        {
            var p = new Vector3(x, 0, z);
            if (!r.Free(p, 3f)) continue;
            Mesh mesh = kind switch { 0 => Props.TankWreck(0), 1 => Props.TankWreck(1), 2 => Props.ApcWreck(), _ => Props.ArmyTruck() };
            var size = kind switch { 0 or 1 => new Vector3(3.6f, 2.6f, 7.8f), 2 => new Vector3(3f, 2.4f, 7f), _ => Props.ArmyTruckSize };
            r.Inst(mesh, RuinKit.At(p, yaw), true, 380f);
            r.BoxOnGround(p, yaw, size);
            r.Take(p, size.Z * 0.55f);
            r.Decal(Mats.Decal.Scorch, p, size.Z * 1.8f, yaw, 0.026f, 0.8f);
            var hf = RuinKit.Hash(x, z, 571);
            if (hf < 0.35f) r.Fire(p + Vector3.Up * 1.6f, 1.3f, r.Lights < 6);
            else if (hf < 0.75f) r.Smolder(p + Vector3.Up * 2.4f, 1.2f);
        }
        c.HotSpots.Add((new Vector3(0, 0, -130), 60f, 0.25f));
    }

    /// <summary>Colonne de camions et de blindés prise sous le feu sur le quai nord, à l'est du grand pont.</summary>
    private static void QuayConvoy(Ctx c)
    {
        var r = c.Ruins;
        for (var i = 0; i < 6; i++)
        {
            var p = new Vector3(40f + i * 13f, 0, 72f + (i % 2 == 0 ? -1.6f : 1.8f));
            if (!r.Free(p, 3f)) continue;
            var yaw = Mathf.Pi / 2 + (RuinKit.Hash(i, 5, 581) - 0.5f) * 0.7f;
            var truck = i % 3 != 1;
            var mesh = truck ? Props.ArmyTruck() : Props.ApcWreck();
            var size = truck ? Props.ArmyTruckSize : new Vector3(3f, 2.4f, 7f);
            r.Inst(mesh, RuinKit.At(p, yaw), true, 380f);
            r.BoxOnGround(p, yaw, size);
            r.Take(p, size.Z * 0.55f);
            r.Decal(Mats.Decal.Scorch, p, size.Z * 1.7f, yaw, 0.026f, 0.8f);
            if (i == 2) r.Fire(p + Vector3.Up * 1.8f, 1.4f, r.Lights < 6);
            else if (i % 2 == 0) r.Smolder(p + Vector3.Up * 2.6f, 1.1f);
        }
        c.HotSpots.Add((new Vector3(72f, 0, 72f), 46f, 0.25f));
    }

    /// <summary>Mâts d'éclairage militaires sur remorque près des barricades (allumés la nuit).</summary>
    private static void FloodLights(Ctx c)
    {
        foreach (var (p, target) in new[] { (new Vector3(-24f, Walk, 56f), new Vector3(0, 0, 90f)), (new Vector3(26f, Walk, 56f), new Vector3(10, 0, 90f)), (new Vector3(-30f, Walk, -58f), new Vector3(0, 0, -100f)) })
        {
            var r = c.Ruins;
            if (!r.TryTake(p, 2f)) continue;
            var dir = new Vector3(target.X - p.X, 0, target.Z - p.Z).Normalized();
            var yaw = Mathf.Atan2(-dir.X, -dir.Z);
            var b = new Basis(Vector3.Up, yaw);
            var k = r.Kit(RuinKit.Layer.Solid, p);
            var d = Mats.Detail();
            var metal = Mats.DetailMetal();
            k.Col = new Color(0.27f, 0.29f, 0.22f);
            k.Box(d, p + Vector3.Up * 0.75f, new Vector3(1.5f, 0.9f, 2.6f), b);
            k.Col = new Color(0.22f, 0.23f, 0.22f);
            k.Tube(metal, p + b * new Vector3(0, 1.2f, 0.9f), p + b * new Vector3(0, 7.2f, 0.9f), 0.09f, 6);
            var head = p + b * new Vector3(0, 7.4f, 0.9f);
            k.Box(metal, head, new Vector3(1.9f, 0.18f, 0.2f), b);
            k.Col = c.Night ? new Color(1f, 0.97f, 0.9f) : new Color(0.2f, 0.21f, 0.22f);
            foreach (var sx in new[] { -0.7f, -0.23f, 0.23f, 0.7f })
                k.Box(c.Night ? Mats.Lamps() : metal, head + b * new Vector3(sx, 0.3f, -0.08f), new Vector3(0.38f, 0.38f, 0.16f), b * new Basis(Vector3.Right, 0.5f));
            r.BoxOnGround(p, yaw, new Vector3(1.5f, 1.2f, 2.6f));
            if (c.Night)
                c.Root.AddChild(new SpotLight3D
                {
                    Position = head + Vector3.Up * 0.2f,
                    Basis = b * new Basis(Vector3.Right, -0.45f),
                    LightColor = new Color(0.86f, 0.9f, 1f), LightEnergy = 7f, SpotRange = 70f, SpotAngle = 36f, SpotAttenuation = 0.8f, ShadowEnabled = false,
                });
        }
    }

    /// <summary>Débris épars (MultiMesh, sans collision ni ombres) : denses près des gravats, cratères et barricades.</summary>
    private static void Litter(Ctx c)
    {
        var r = c.Ruins;
        const float step = 2.8f;
        var idx = 0;
        for (var z = -Half + 2; z < South - 4; z += step)
        for (var x = -Half + 2; x < Half - 2; x += step, idx++)
        {
            var jx = x + (RuinKit.Hash(idx, 1, 591) - 0.5f) * step;
            var jz = z + (RuinKit.Hash(idx, 2, 591) - 0.5f) * step;
            if (!Clear(c, new Vector3(jx, 0, jz), -0.2f)) continue;
            var dens = 0.05f;
            foreach (var (hp, hr, hd) in c.HotSpots)
            {
                var dd = new Vector2(jx - hp.X, jz - hp.Z).Length();
                if (dd < hr) dens = Mathf.Max(dens, hd * (1 - dd / hr) + 0.05f);
            }
            if (SpawnArea.HasPoint(new Vector2(jx, jz))) dens *= 0.3f;
            if (RuinKit.Hash(idx, 3, 591) > dens) continue;
            var p = OnGround(c, jx, jz);
            if (p.Y < -0.5f) continue;
            var kind = (int)(RuinKit.Hash(idx, 4, 591) * Props.DebrisKinds);
            var xf = RuinKit.At(p, RuinKit.Hash(idx, 6, 591) * Mathf.Tau, 0, 0, 0.75f + RuinKit.Hash(idx, 7, 591) * 0.6f);
            r.Inst(Props.Debris(kind), xf, false, kind == 5 || kind == 9 ? 60f : 95f);
        }
    }
}
