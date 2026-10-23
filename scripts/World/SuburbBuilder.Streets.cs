using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>Voirie de la banlieue : chaussées, trottoirs, marquages, panneaux, lampadaires et lignes électriques.</summary>
public static partial class SuburbBuilder
{
    private static float RoadY(Road r) => 0.014f + (r.Index % 8) * 0.0015f;

    /// <summary>Vrai si le point est sur l'un des ponts (zone de la rivière traversée par une route).</summary>
    private static bool OnBridge(Vector3 p) => Mathf.Abs(p.Z - RiverZ(p.X)) < RiverHalf + RiverBank + 4.5f;

    /// <summary>Quad horizontal orienté vers le haut quel que soit l'ordre des coins.</summary>
    private static void QuadUp(MeshKit k, Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        if ((b - a).Cross(c - a).Y >= 0) k.Quad(m, a, b, c, d);
        else k.Quad(m, a, d, c, b);
    }

    /// <summary>Quad vertical orienté vers <paramref name="n"/>.</summary>
    private static void QuadFacing(MeshKit k, Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
    {
        if ((b - a).Cross(c - a).Dot(n) >= 0) k.Quad(m, a, b, c, d);
        else k.Quad(m, a, d, c, b);
    }

    /// <summary>Bande peinte le long d'une rue entre les abscisses s0 et s1, de t0 à t1 du côté gauche (repère de la rue).</summary>
    private static void Strip(Ctx c, Road r, float s0, float s1, float t0, float t1, float y)
    {
        if (s1 <= s0) return;
        var paint = Mats.RoadPaint();
        var n = Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / 2f));
        for (var i = 0; i < n; i++)
        {
            var sa = Mathf.Lerp(s0, s1, i / (float)n);
            var sb = Mathf.Lerp(s0, s1, (i + 1) / (float)n);
            var (pa, ta) = r.At(sa);
            var (pb, tb) = r.At(sb);
            var la = Vector3.Up.Cross(ta);
            var lb = Vector3.Up.Cross(tb);
            var k = c.Ground.At(pa);
            k.Col = Colors.White;
            QuadUp(k, paint, pa + la * t0 + Vector3.Up * y, pb + lb * t0 + Vector3.Up * y, pb + lb * t1 + Vector3.Up * y, pa + la * t1 + Vector3.Up * y);
        }
    }

    /// <summary>Points de raccordement : (point, rue qui arrive, rue rejointe, arrive par son début ?).</summary>
    private static IEnumerable<(Vector3 p, Road from, Road to, bool atStart)> Junctions(Ctx c)
    {
        foreach (var r in c.Roads)
        {
            if (r.StartJoin != null) yield return (r.P[0], r, r.StartJoin, true);
            if (r.EndJoin != null) yield return (r.P[^1], r, r.EndJoin, false);
        }
    }

    private static void BuildStreets(Ctx c)
    {
        var asphalt = Mats.Asphalt();
        var walk = Mats.Sidewalk();
        var curb = Mats.Curb();
        var gravel = Mats.Gravel();
        var junctions = new List<(Vector3 p, Road from, Road to, bool atStart)>(Junctions(c));

        foreach (var r in c.Roads)
        {
            var y = RoadY(r);
            for (var i = 0; i + 1 < r.P.Count; i++)
            {
                var a = r.P[i];
                var b = r.P[i + 1];
                var la = r.Left(i);
                var lb = r.Left(i + 1);
                var k = c.Ground.At(a);
                // Chaussée (ou chemin de gravier), accotements en terre pour les routes sans trottoir.
                k.Col = r.Gravel ? new Color(0.66f, 0.6f, 0.5f) : Colors.White;
                QuadUp(k, r.Gravel ? gravel : asphalt, a + la * r.Hw + Vector3.Up * y, b + lb * r.Hw + Vector3.Up * y, b - lb * r.Hw + Vector3.Up * y, a - la * r.Hw + Vector3.Up * y);
                if (!r.Walks && !r.Gravel)
                {
                    k.Col = new Color(0.5f, 0.45f, 0.36f);
                    foreach (var sg in new[] { -1f, 1f })
                        QuadUp(k, gravel, a + la * sg * r.Hw + Vector3.Up * (y - 0.004f), b + lb * sg * r.Hw + Vector3.Up * (y - 0.004f),
                            b + lb * sg * (r.Hw + 0.7f) + Vector3.Up * 0.008f, a + la * sg * (r.Hw + 0.7f) + Vector3.Up * 0.008f);
                }
                if (!r.Walks) continue;

                // Trottoirs : interrompus dans les carrefours (chaussée d'une autre rue) et sur les ponts.
                foreach (var sg in new[] { -1f, 1f })
                {
                    var mid = (a + b) / 2 + (la + lb) / 2 * sg * (r.Hw + 1f);
                    var other = c.Mask.AsphaltAt(mid.X, mid.Z);
                    if (other != 0 && other != r.Index + 1) continue;
                    if (OnBridge(mid)) continue;
                    if (r.Bulb > 0 && mid.DistanceTo(r.P[^1]) < r.Bulb + 0.5f) continue;
                    var near = false;
                    foreach (var j in junctions)
                        if (j.to == r && j.p.DistanceTo(mid) < j.from.Hw + 0.6f) near = true;
                    if (near) continue;
                    const float top = 0.13f;
                    var i0 = a + la * sg * r.Hw;
                    var i1 = b + lb * sg * r.Hw;
                    var c0 = a + la * sg * (r.Hw + 0.2f);
                    var c1 = b + lb * sg * (r.Hw + 0.2f);
                    var o0 = a + la * sg * (r.Hw + 2f);
                    var o1 = b + lb * sg * (r.Hw + 2f);
                    k.Col = Colors.White;
                    QuadUp(k, curb, i0 + Vector3.Up * top, i1 + Vector3.Up * top, c1 + Vector3.Up * top, c0 + Vector3.Up * top);
                    QuadFacing(k, curb, i0, i1, i1 + Vector3.Up * top, i0 + Vector3.Up * top, -la * sg);
                    QuadUp(k, walk, c0 + Vector3.Up * top, c1 + Vector3.Up * top, o1 + Vector3.Up * top, o0 + Vector3.Up * top);
                    QuadFacing(k, curb, o0, o1, o1 + Vector3.Up * top, o0 + Vector3.Up * top, la * sg);
                }
            }
        }

        // Raquettes de retournement en bout d'impasse : disque d'enrobé, trottoir circulaire, îlot planté.
        foreach (var r in c.Roads)
        {
            if (r.Bulb <= 0) continue;
            var e = r.P[^1];
            var y = RoadY(r);
            var k = c.Ground.At(e);
            const int seg = 28;
            for (var i = 0; i < seg; i++)
            {
                var a0 = i * Mathf.Tau / seg;
                var a1 = (i + 1) * Mathf.Tau / seg;
                var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                k.Col = Colors.White;
                k.Tri(asphalt, e + Vector3.Up * y, e + d0 * r.Bulb + Vector3.Up * y, e + d1 * r.Bulb + Vector3.Up * y, e + Vector3.Down);
                var midDir = (d0 + d1).Normalized();
                if (DistSeg(e + midDir * (r.Bulb + 1f), r.P[^1], r.P[Mathf.Max(0, r.P.Count - 6)]) < r.Hw + 0.2f) continue; // entrée de la raquette
                const float top = 0.13f;
                QuadUp(k, curb, e + d0 * r.Bulb + Vector3.Up * top, e + d1 * r.Bulb + Vector3.Up * top, e + d1 * (r.Bulb + 0.2f) + Vector3.Up * top, e + d0 * (r.Bulb + 0.2f) + Vector3.Up * top);
                QuadFacing(k, curb, e + d0 * r.Bulb, e + d1 * r.Bulb, e + d1 * r.Bulb + Vector3.Up * top, e + d0 * r.Bulb + Vector3.Up * top, -midDir);
                QuadUp(k, walk, e + d0 * (r.Bulb + 0.2f) + Vector3.Up * top, e + d1 * (r.Bulb + 0.2f) + Vector3.Up * top, e + d1 * (r.Bulb + 2f) + Vector3.Up * top, e + d0 * (r.Bulb + 2f) + Vector3.Up * top);
            }
            // Îlot central engazonné avec un arbre.
            k.Col = new Color(0.4f, 0.52f, 0.26f);
            k.Cylinder(Mats.GroundTint(), e, 2.6f, 2.6f, 0.15f, 18);
            k.Col = Colors.White;
            k.Cylinder(curb, e, 2.75f, 2.75f, 0.14f, 18, false);
            StreetTree(c, e + Vector3.Up * 0.15f, (r.Index + 1) % 5, 0.6f, r.Index * 1.3f, false);
        }

        // Marquages de la grande rue : axe discontinu, lignes de rive, passages piétons près de la place.
        var g = _grande;
        var gy = RoadY(g) + 0.004f;
        bool NearJunction(float s, float pad)
        {
            var (p, _) = g.At(s);
            foreach (var j in junctions)
                if (j.to == g && j.p.DistanceTo(p) < j.from.Edge + pad) return true;
            return OnBridge(p);
        }
        for (var s = 2f; s + 3f < g.Length; s += 10f)
            if (!NearJunction(s, 4f) && !NearJunction(s + 3f, 4f)) Strip(c, g, s, s + 3f, -0.07f, 0.07f, gy);
        for (var s = 0f; s + 2f < g.Length; s += 2f)
        {
            if (NearJunction(s + 1f, 1.5f)) continue;
            Strip(c, g, s, s + 2f, g.Hw - 0.45f, g.Hw - 0.32f, gy);
            Strip(c, g, s, s + 2f, -g.Hw + 0.32f, -g.Hw + 0.45f, gy);
        }
        foreach (var zx in new[] { -18f, 18f })
        {
            var s0 = ArcAtX(g, zx);
            for (var t = -g.Hw + 0.5f; t < g.Hw - 0.3f; t += 1.1f) Strip(c, g, s0 - 1.5f, s0 + 1.5f, t, t + 0.55f, gy);
        }

        // Carrefours : passage piéton, ligne d'arrêt (Stop) ou de cédez-le-passage sur la voie qui arrive.
        foreach (var (p, from, to, atStart) in junctions)
        {
            var y = RoadY(from) + 0.004f;
            var d0 = to.Edge + 0.8f;
            var sStop = atStart ? d0 + (from.Walks ? 3.6f : 0f) : from.Length - d0 - (from.Walks ? 3.6f : 0f);
            if (from.Walks && !from.Gravel)
            {
                var z0 = atStart ? d0 : from.Length - d0 - 3f;
                for (var t = -from.Hw + 0.5f; t < from.Hw - 0.3f; t += 1.1f) Strip(c, from, z0, z0 + 3f, t, t + 0.55f, y);
            }
            if (from.Gravel) continue;
            // Voie entrante : à gauche du sens de parcours au départ, à droite à l'arrivée (circulation à droite).
            var t0 = atStart ? 0.1f : -from.Hw + 0.2f;
            var t1 = atStart ? from.Hw - 0.2f : -0.1f;
            if (from.Stop) Strip(c, from, sStop - 0.25f, sStop + 0.25f, t0, t1, y);
            else
                for (var t = t0; t < t1 - 0.3f; t += 1.0f) Strip(c, from, sStop - 0.15f, sStop + 0.15f, t, t + 0.5f, y);
            if (from.Stop)
            {
                var (sp, st) = from.At(sStop);
                var lft = Vector3.Up.Cross(st);
                var side = atStart ? 1f : -1f;
                var pos = sp + lft * side * (from.Hw + (from.Walks ? 1.4f : 0.9f)) + Vector3.Up * (from.Walks ? 0.13f : 0f);
                var face = atStart ? st : -st;
                c.Props.Add(Props.StopSign(), pos, Mathf.Atan2(face.X, face.Z), range: 160);
            }
        }

        BuildLamps(c, junctions);
        BuildPowerLines(c);
        // Abribus sur la grande rue, côté bourg.
        var (bp, bt) = g.At(ArcAtX(g, -48f));
        var bl = Vector3.Up.Cross(bt);
        c.Props.Add(Props.BusStop(c.Night), bp - bl * (g.Hw + 1.3f) + Vector3.Up * 0.13f, Mathf.Atan2(bl.X, bl.Z), range: 220, shadows: true);
    }

    /// <summary>Abscisse curviligne du point de la rue (est-ouest) le plus proche de x.</summary>
    private static float ArcAtX(Road r, float x)
    {
        for (var i = 1; i < r.P.Count; i++)
            if (r.P[i].X >= x) return r.S[i - 1] + (x - r.P[i - 1].X) / Mathf.Max(r.P[i].X - r.P[i - 1].X, 0.001f) * (r.S[i] - r.S[i - 1]);
        return r.Length;
    }

    /// <summary>Lampadaires : en quinconce sur la grande rue, d'un seul côté sur les rues à trottoirs.</summary>
    private static void BuildLamps(Ctx c, List<(Vector3 p, Road from, Road to, bool atStart)> junctions)
    {
        foreach (var r in c.Roads)
        {
            if (!r.Walks) continue;
            var step = r.Main ? 26f : 36f;
            var idx = 0;
            for (var s = 10f; s < r.Length - 6f; s += step, idx++)
            {
                var (p, t) = r.At(s);
                if (Mathf.Abs(p.X) > Half + 30 || p.Z > SouthEdge + 10) continue;
                var side = r.Main && idx % 2 == 1 ? -1f : 1f;
                var lft = Vector3.Up.Cross(t);
                var pos = p + lft * side * (r.Hw + 1.6f);
                if (OnBridge(pos)) continue;
                var other = c.Mask.AsphaltAt(pos.X, pos.Z);
                if (other != 0 && other != r.Index + 1) continue;
                var bad = false;
                foreach (var j in junctions)
                    if (j.p.DistanceTo(pos) < j.from.Edge + j.to.Edge + 1f) bad = true;
                if (bad) continue;
                var face = -lft * side;
                var yaw = Mathf.Atan2(face.X, face.Z);
                pos.Y = 0.13f;
                c.Props.Add(Props.StreetLamp(c.Night), pos, yaw, range: 240, shadows: true);
                c.Body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.15f, Height = 7f }, Position = pos + new Vector3(0, 3.5f, 0) });
                if (c.Night)
                {
                    var head = pos + new Basis(Vector3.Up, yaw) * new Vector3(0, 6.8f, 1.45f);
                    c.Root.AddChild(new OmniLight3D { Position = head, LightColor = new Color(1f, 0.78f, 0.5f), LightEnergy = 3f, OmniRange = 20f, OmniAttenuation = 1.2f, ShadowEnabled = false });
                }
            }
        }
    }

    /// <summary>Lignes électriques aériennes sur poteaux bois (câbles en chaînette) le long des rues résidentielles.</summary>
    private static void BuildPowerLines(Ctx c)
    {
        var wireMat = Mats.DetailMetal();
        foreach (var r in new[] { _r5, _r6e, _r6w, _r7, _r8, _r3, _r13, _r4 })
        {
            var side = r == _r3 || r == _r4 ? 1f : -1f;   // côté forêt pour les chemins de lisière
            var prev = new List<Vector3>();
            var n = 0;
            for (var s = 6f; s < r.Length - 4f; s += 34f)
            {
                var (p, t) = r.At(s);
                if (Mathf.Abs(p.X) > Half + 40) { prev.Clear(); continue; }
                var lft = Vector3.Up.Cross(t);
                var pos = p + lft * side * (r.Edge + 0.9f);
                var code = c.Mask.Get(pos.X, pos.Z);
                if (code == BuiltC || code == RiverC || c.Mask.AsphaltAt(pos.X, pos.Z) != 0 || OnBridge(pos)) { prev.Clear(); continue; }
                var yaw = Mathf.Atan2(-lft.Z, lft.X);
                c.Props.Add(Props.PowerPole(n % 5 == 2), pos, yaw, range: 260, shadows: true);
                c.Body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.17f, Height = 9.4f }, Position = pos + Vector3.Up * 4.7f });
                n++;
                var basis = new Basis(Vector3.Up, yaw);
                var pts = new List<Vector3>();
                foreach (var w in Props.PoleWires) pts.Add(pos + basis * w);
                if (prev.Count == pts.Count)
                {
                    var k = c.Ground.At(pos);
                    k.Col = new Color(0.12f, 0.12f, 0.12f);
                    for (var w = 0; w < pts.Count; w++)
                    {
                        // Chaînette approchée : quatre tronçons, flèche de 0,7 m.
                        var a = prev[w];
                        var b = pts[w];
                        var last = a;
                        for (var q = 1; q <= 4; q++)
                        {
                            var u = q / 4f;
                            var cur = a.Lerp(b, u) + Vector3.Down * (0.7f * 4f * u * (1 - u));
                            k.Tube(wireMat, last, cur, 0.014f, 3);
                            last = cur;
                        }
                    }
                }
                prev = pts;
            }
        }
    }
}
