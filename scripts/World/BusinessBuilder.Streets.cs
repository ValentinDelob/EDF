using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Rues du quartier d'affaires : marquages (2 × 2 voies, passages piétons, lignes d'arrêt), plaques et avaloirs,
/// mobilier destructible (candélabres, feux éteints, abribus, potelets, corbeilles, bornes, arbres morts en
/// grille), véhicules garés le long des trottoirs.
/// </summary>
public static partial class BusinessBuilder
{
    /// <summary>Rue : axe, sens (nord-sud si vertical), nœuds (début, carrefours, fin) et nature de chaque nœud.</summary>
    private readonly record struct Street(bool Vertical, float Axis, float[] Nodes, bool[] Crossing);

    private static IEnumerable<Street> Streets()
    {
        // Rues nord-sud : de la limite nord au mur antibruit, carrefours avec toutes les rues est-ouest et la traverse.
        foreach (var x in Xs)
            yield return new Street(true, x, new[] { -262f, -190f, -130f, -70f, 0f, 60f, 128f, 150f }, new[] { false, true, true, true, true, true, true, false });
        // Avenue centrale : au nord du parvis et au sud (jusqu'au viaduc).
        yield return new Street(true, 0f, new[] { -262f, -190f, -130f, -70f }, new[] { false, true, true, true });
        yield return new Street(true, 0f, new[] { 60f, 128f, 150f }, new[] { true, true, false });
        // Rues est-ouest (toutes croisent l'avenue centrale).
        foreach (var z in Zs)
            yield return new Street(false, z, new[] { -262f, -190f, -130f, -70f, 0f, 70f, 130f, 190f, 262f }, new[] { false, true, true, true, true, true, true, true, false });
        // Traverse z = 0 de part et d'autre du parvis.
        yield return new Street(false, 0f, new[] { -262f, -190f, -130f, -70f }, new[] { false, true, true, true });
        yield return new Street(false, 0f, new[] { 70f, 130f, 190f, 262f }, new[] { true, true, true, false });
    }

    // ───────────────────────── Marquages ─────────────────────────

    private static void BuildRoadMarkings(Ctx c)
    {
        var deco = new RandomNumberGenerator { Seed = 6161 };
        foreach (var st in Streets())
            for (var i = 0; i + 1 < st.Nodes.Length; i++)
            {
                var s0 = st.Nodes[i] + (st.Crossing[i] ? RoadHalf : 0f);
                var s1 = st.Nodes[i + 1] - (st.Crossing[i + 1] ? RoadHalf : 0f);
                if (s1 - s0 < 2f) continue;
                RoadSegment(c, st.Vertical, st.Axis, s0, s1, st.Crossing[i], st.Crossing[i + 1], deco);
            }
    }

    /// <summary>
    /// Marquages d'un tronçon (repère local : a le long de la rue, t en travers, ±8 m) : passages piétons, lignes
    /// d'arrêt, double ligne axiale, séparateurs de voies, lignes de rive ; plaque d'égout et avaloirs.
    /// </summary>
    private static void RoadSegment(Ctx c, bool vertical, float axis, float s0, float s1, bool cross0, bool cross1, RandomNumberGenerator deco)
    {
        var mid = vertical ? new Vector3(axis, 0, (s0 + s1) / 2) : new Vector3((s0 + s1) / 2, 0, axis);
        var kit = c.Ground.At(mid);
        var m = Mats.RoadPaint();
        kit.Col = Colors.White;
        void P(float a0, float a1, float t0, float t1)
        {
            if (a1 <= a0) return;
            float x0, z0, x1, z1;
            if (vertical) { x0 = axis + t0; x1 = axis + t1; z0 = a0; z1 = a1; }
            else { x0 = a0; x1 = a1; z0 = axis + t0; z1 = axis + t1; }
            kit.Quad(m, new Vector3(x0, PaintY, z1), new Vector3(x1, PaintY, z1), new Vector3(x1, PaintY, z0), new Vector3(x0, PaintY, z0));
        }
        Vector3 W(float a, float t, float y) => vertical ? new Vector3(axis + t, y, a) : new Vector3(a, y, axis + t);
        void Zebra(float a0, float a1)
        {
            for (var t = -7.15f; t < 7.3f; t += 1.1f) P(a0, a1, t - 0.27f, t + 0.27f);
        }
        // Circulation à droite : voie entrante côté s0 à t > 0 (nord-sud) ou t < 0 (est-ouest).
        var in0 = vertical ? 1f : -1f;
        if (cross0)
        {
            Zebra(s0 + 0.6f, s0 + 3.6f);
            if (in0 > 0) P(s0 + 4.2f, s0 + 4.6f, 0.2f, 7.4f); else P(s0 + 4.2f, s0 + 4.6f, -7.4f, -0.2f);
        }
        if (cross1)
        {
            Zebra(s1 - 3.6f, s1 - 0.6f);
            if (in0 > 0) P(s1 - 4.6f, s1 - 4.2f, -7.4f, -0.2f); else P(s1 - 4.6f, s1 - 4.2f, 0.2f, 7.4f);
        }
        var a0L = s0 + (cross0 ? 4.8f : 0.5f);
        var a1L = s1 - (cross1 ? 4.8f : 0.5f);
        // Double ligne axiale continue, séparateurs de voies discontinus, lignes de rive.
        P(a0L, a1L, -0.28f, -0.16f);
        P(a0L, a1L, 0.16f, 0.28f);
        foreach (var t in new[] { -3.7f, 3.7f })
            for (var a = a0L + 2f; a + 3f < a1L - 2f; a += 9f) P(a, a + 3f, t - 0.07f, t + 0.07f);
        P(a0L, a1L, -7.35f, -7.23f);
        P(a0L, a1L, 7.23f, 7.35f);

        // Plaque d'égout dans une voie, avaloirs au fil d'eau près des carrefours.
        var dark = Mats.Metal(new Color(0.11f, 0.11f, 0.11f), 0.7f);
        if (deco.Randf() < 0.75f && a1L - a0L > 14f)
        {
            var a = deco.RandfRange(a0L + 6, a1L - 6);
            var t = deco.Randf() < 0.5f ? -1.8f : 1.8f;
            kit.Cylinder(dark, W(a, t, -0.01f), 0.34f, 0.34f, 0.025f, 14);
        }
        foreach (var (a, t) in new[] { (s0 + 6f, 7.55f), (s1 - 6f, -7.55f) })
        {
            if (!(a > s0 && a < s1)) continue;
            var size = vertical ? new Vector3(0.38f, 0.03f, 0.85f) : new Vector3(0.85f, 0.03f, 0.38f);
            kit.Box(dark, W(a, t, 0.0f), size, null, false);
        }
    }

    // ───────────────────────── Mobilier ─────────────────────────

    /// <summary>Vrai si le point est libre de toute emprise d'immeuble (ou d'escalier de secours).</summary>
    private static bool Clear(Ctx c, Vector3 p, float r = 0.6f)
    {
        foreach (var k in c.KeepOut)
            if (p.X > k.Position.X - r && p.X < k.End.X + r && p.Z > k.Position.Y - r && p.Z < k.End.Y + r) return false;
        return true;
    }

    private static void BuildStreetFurniture(Ctx c)
    {
        var deco = new RandomNumberGenerator { Seed = 4343 };
        var vrng = new RandomNumberGenerator { Seed = 7337 };
        foreach (var r in c.Blocks)
        {
            var h = r.Size / 2;
            var ctr = new Vector3(r.GetCenter().X, Walk, r.GetCenter().Y);
            // Côtés : (point de départ, direction le long, normale vers la rue, longueur).
            var sides = new (Vector3 o, Vector3 along, Vector3 n, float len)[]
            {
                (ctr + new Vector3(-h.X, 0, -h.Y), Vector3.Right, Vector3.Forward, r.Size.X),
                (ctr + new Vector3(-h.X, 0, h.Y), Vector3.Right, Vector3.Back, r.Size.X),
                (ctr + new Vector3(-h.X, 0, -h.Y), Vector3.Back, Vector3.Left, r.Size.Y),
                (ctr + new Vector3(h.X, 0, -h.Y), Vector3.Back, Vector3.Right, r.Size.Y),
            };
            foreach (var (o, along, n, len) in sides)
            {
                // Côté hors aire de jeu (au-delà des murs) : seulement les candélabres.
                var yawRoad = Mathf.Atan2(n.X, n.Z);
                // Candélabres tous les ~26 m, crosse vers la chaussée.
                var nl = Mathf.Max(1, Mathf.RoundToInt(len / 26f));
                for (var i = 0; i < nl; i++)
                {
                    var a = (i + 0.5f) * len / nl;
                    var p = o + along * a + n * -0.7f;
                    if (!Clear(c, p)) continue;
                    Lamp(c, p, yawRoad);
                }
                // Arbres morts en grille d'arbre, corbeilles, bornes d'incendie.
                for (var a = 7f; a < len - 7f; a += 13f)
                {
                    var p = o + along * (a + 6.5f) + n * -1.6f;
                    if (!Clear(c, p, 1.2f) || deco.Randf() < 0.35f) continue;
                    StreetTree(c, p, deco);
                }
                var nb = deco.RandiRange(0, 2);
                for (var i = 0; i < nb; i++)
                {
                    var p = o + along * deco.RandfRange(6f, len - 6f) + n * -0.5f;
                    if (!Clear(c, p)) continue;
                    c.Props.Add(i == 1 ? Props.Hydrant() : Props.Bin(), p, yawRoad + Mathf.Pi, range: 120);
                }
                // Véhicules abandonnés le long du trottoir.
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
                    var yaw = Mathf.Atan2(along.X, along.Z) + (vrng.Randf() < 0.5f ? 0 : Mathf.Pi) + vrng.RandfRange(-0.06f, 0.06f);
                    ParkVehicle(c, p, yaw, type, col, vlen, vrng);
                }
            }
            // Potelets aux angles.
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                if (deco.Randf() < 0.4f) continue;
                for (var i = 0; i < 3; i++)
                {
                    var a = 1.0f + i * 0.85f;
                    var p1 = ctr + new Vector3(sx * (h.X - 0.35f), 0, sz * (h.Y - a));
                    var p2 = ctr + new Vector3(sx * (h.X - a), 0, sz * (h.Y - 0.35f));
                    if (Clear(c, p1)) c.Props.Add(Props.Bollard(), p1, 0, range: 110);
                    if (Clear(c, p2)) c.Props.Add(Props.Bollard(), p2, 0, range: 110);
                }
            }
        }

        // Feux tricolores éteints aux carrefours (sur les trottoirs seulement).
        foreach (var st in Streets())
        {
            if (!st.Vertical) continue;
            for (var i = 0; i < st.Nodes.Length; i++)
            {
                if (!st.Crossing[i]) continue;
                var x = st.Axis;
                var z = st.Nodes[i];
                foreach (var (dx, dz, yaw) in new[] { (-8.7f, -12f, Mathf.Pi), (8.7f, 12f, 0f), (12f, -8.7f, Mathf.Pi / 2), (-12f, 8.7f, -Mathf.Pi / 2) })
                {
                    var p = OnGround(c, x + dx, z + dz);
                    if (p.Y < 0.1f || !Clear(c, p)) continue;
                    TrafficLight(c, p, yaw);
                }
            }
        }

        // Abribus le long de l'avenue centrale et de la rue du viaduc.
        foreach (var (x, z, yaw) in new[] { (-9.6f, -112f, Mathf.Pi / 2), (9.6f, -168f, -Mathf.Pi / 2), (-40f, 69.4f, Mathf.Pi), (100f, 119.6f, 0f) })
        {
            var p = new Vector3(x, Walk, z);
            if (Clear(c, p, 1.5f)) c.Props.Add(Props.BusStopRuined(), p, yaw, range: 220, shadows: true);
        }
    }

    private static readonly Color[] CarColors =
    {
        new(0.5f, 0.5f, 0.51f), new(0.08f, 0.08f, 0.085f), new(0.33f, 0.13f, 0.11f), new(0.15f, 0.18f, 0.24f),
        new(0.6f, 0.6f, 0.58f), new(0.22f, 0.23f, 0.24f), new(0.3f, 0.28f, 0.2f),
    };

    /// <summary>Candélabre destructible : abattu une fois sur cinq, éteint le plus souvent (réseau coupé).</summary>
    private static void Lamp(Ctx c, Vector3 p, float yaw, bool forceLit = false)
    {
        var r = c.Ruins;
        var fate = RuinKit.Hash(p.X, p.Z, 181);
        if (!forceLit && fate < 0.18f && r.Free(p, 0.5f))
        {
            // Couché en travers du trottoir et de la chaussée.
            var fall = yaw + (RuinKit.Hash(p.X, p.Z, 182) - 0.5f) * 1.4f;
            var basis = new Basis(Vector3.Up, fall) * new Basis(Vector3.Right, Mathf.Pi / 2 - 0.04f) * new Basis(Vector3.Up, Mathf.Pi / 2);
            r.Inst(Props.StreetLamp(false), new Transform3D(basis, p + Vector3.Up * 0.17f), true, 260f);
            var dir = new Basis(Vector3.Up, fall) * Vector3.Back;
            r.Box(new Transform3D(new Basis(Vector3.Up, fall), p + dir * 3.6f + Vector3.Up * 0.18f), new Vector3(0.32f, 0.32f, 7.2f));
            return;
        }
        var lit = c.Night && (forceLit || fate > 0.55f) && c.LitLamps < 24;
        var id = c.Props.Add(Props.StreetLamp(lit), p, yaw, range: 260, shadows: true);
        if (lit)
        {
            c.LitLamps++;
            var head = p + new Basis(Vector3.Up, yaw) * new Vector3(0, 6.8f, 1.45f);
            var light = new OmniLight3D { Position = head, LightColor = new Color(1f, 0.78f, 0.5f), LightEnergy = 3f, OmniRange = 20f, OmniAttenuation = 1.2f, ShadowEnabled = false };
            c.Root.AddChild(light);
            Destructibles.AddExtra(id, light);
        }
        var cs = new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.15f, Height = 7f }, Position = p + new Vector3(0, 3.5f, 0) };
        if (id >= 0) Destructibles.AddShape(id, cs);
        else c.Body.AddChild(cs);
    }

    /// <summary>Feu tricolore éteint, parfois penché ou abattu.</summary>
    private static void TrafficLight(Ctx c, Vector3 p, float yaw)
    {
        var fate = RuinKit.Hash(p.X, p.Z, 183);
        var r = c.Ruins;
        if (fate < 0.15f)
        {
            var fall = yaw + (fate - 0.075f) * 12f;
            var basis = new Basis(Vector3.Up, fall) * new Basis(Vector3.Right, Mathf.Pi / 2 - 0.05f);
            r.Inst(Props.TrafficLightDead(), new Transform3D(basis, p + Vector3.Up * 0.1f), true, 200f);
            r.Box(new Transform3D(new Basis(Vector3.Up, fall), p + new Basis(Vector3.Up, fall) * new Vector3(0, 0.25f, 1.8f)), new Vector3(0.6f, 0.5f, 3.6f));
            return;
        }
        if (fate < 0.28f)
        {
            r.Inst(Props.TrafficLightDead(), new Transform3D(new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, 0.25f + fate), p), true, 200f);
            return;
        }
        c.Props.Add(Props.TrafficLightDead(), p, yaw, range: 200);
    }

    /// <summary>Arbre d'alignement mort (destructible) dans une grille de fonte.</summary>
    private static void StreetTree(Ctx c, Vector3 p, RandomNumberGenerator deco)
    {
        var v = deco.RandiRange(0, 3);
        c.Props.Add(Props.DeadTree(v), p, deco.RandfRange(0, Mathf.Tau), null, 320, true, deco.RandfRange(0.7f, 0.9f));
        var k = c.Ground.At(p);
        k.Col = new Color(0.12f, 0.12f, 0.12f);
        k.Box(Mats.DetailMetal(), p + Vector3.Up * 0.012f, new Vector3(1.6f, 0.02f, 1.6f), null, false);
    }

    /// <summary>Véhicule abandonné le long du trottoir : épave calcinée une fois sur deux, sinon intact (destructible).</summary>
    private static void ParkVehicle(Ctx c, Vector3 p, float yaw, Props.VehicleType type, Color paint, float len, RandomNumberGenerator vrng)
    {
        var size = Props.VehicleSize(type, len);
        if (!c.Ruins.Free(p, size.Z * 0.5f)) return;
        paint = paint.Lerp(new Color(0.33f, 0.32f, 0.3f), 0.55f).Darkened(0.12f);
        var fate = RuinKit.Hash(p.X, p.Z, 177);
        if (fate < 0.5f)
        {
            PlaceWreck(c, p, yaw, type, (int)(RuinKit.Hash(p.X, p.Z, 178) * 4), 0, fate < 0.03f ? 1 : 0);
            return;
        }
        c.Ruins.Take(p, size.Z * 0.5f);
        if (type == Props.VehicleType.Van && vrng.Randf() < 0.6f) paint = new Color(0.5f, 0.5f, 0.48f);
        if (Props.Burnt(p)) paint = Mats.BurntPaint;
        var mesh = Props.Vehicle(type, paint, len);
        var node = new MeshInstance3D { Mesh = mesh, Position = p, Rotation = new Vector3(0, yaw, 0), VisibilityRangeEnd = 260f };
        c.Root.AddChild(node);
        CityBuilder.AddVehicleProp(c.Body, node, mesh, type, len, p, yaw, paint);
    }
}
