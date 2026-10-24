using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Zone industrielle au-delà des murs invisibles (décor sans collision ni ombres, maillages fusionnés par secteurs de
/// 400 m) : autres usines, parcs de cuves, tours de refroidissement, cheminées (certaines fument), gazomètre à
/// charpente, ligne à haute tension, portiques du port au sud. Sol de béton puis friches jusqu'aux collines.
/// </summary>
public static partial class IndustrialBuilder
{
    private static void BuildSkyline(Ctx c)
    {
        var rng = new RandomNumberGenerator { Seed = 5151 };
        var apron = Mats.Apron();
        foreach (var (x0, z0, x1, z1) in new[] { (-1800f, -1800f, 1800f, -262f), (-1800f, 262f, 1800f, 1800f), (-1800f, -262f, -262f, 262f), (262f, -262f, 1800f, 262f) })
            CityBuilder.AddSlab(c.Root, c.Body, new Vector3((x0 + x1) / 2, -0.5f, (z0 + z1) / 2), new Vector3(x1 - x0, 1f, z1 - z0), apron, collide: false);

        var sectors = new Dictionary<(int, int), MeshKit>();
        MeshKit At(Vector3 p)
        {
            var key = (Mathf.FloorToInt(p.X / 400f), Mathf.FloorToInt(p.Z / 400f));
            if (!sectors.TryGetValue(key, out var k)) sectors[key] = k = new MeshKit();
            return k;
        }
        var brick = Mats.FactoryBrick();
        var glazing = Mats.FactoryGlazing();
        var detail = Mats.Detail();
        var metal = Mats.DetailMetal();
        var tankPaint = Mats.Worn(new Color(0.5f, 0.5f, 0.48f), 0.65f, 0.25f);
        var clads = new[]
        {
            Mats.Corrugated(new Color(0.4f, 0.41f, 0.4f), 97, 2.4f), Mats.Corrugated(new Color(0.36f, 0.3f, 0.26f), 98, 2.4f),
            Mats.Corrugated(new Color(0.32f, 0.35f, 0.37f), 99, 2.4f),
        };

        // Halles et entrepôts lointains, sur une trame lâche.
        for (var ix = -14; ix <= 14; ix++)
        for (var iz = -14; iz <= 14; iz++)
        {
            var cx = ix * 70f + rng.RandfRange(-12, 12);
            var cz = iz * 70f + rng.RandfRange(-12, 12);
            if (Mathf.Abs(cx) < 290f && cz > -290f && cz < 250f) continue;
            if (new Vector2(cx, cz).Length() > 1150f || rng.Randf() < 0.35f) continue;
            if (cz > 200f && cz < 330f) continue; // le canal file vers l'horizon
            var w = rng.RandfRange(24f, 60f);
            var d = rng.RandfRange(18f, 44f);
            var h = rng.RandfRange(8f, 22f);
            var p = new Vector3(cx, 0, cz);
            var k = At(p);
            var hx = w / 2;
            var hz = d / 2;
            var useBrick = rng.Randf() < 0.4f;
            var wall = useBrick ? brick : (Material)clads[rng.RandiRange(0, 2)];
            foreach (var (start, along, n, len) in Faces(hx, hz))
            {
                WallBand(k, wall, p + start, p + start + along * len, 0, h, n, 3f, 3f, Colors.White * 0.9f);
                if (useBrick && h > 10f) WallBand(k, glazing, p + start + along * 2f + n * 0.05f, p + start + along * (len - 2f) + n * 0.05f, 3f, h - 2f, n, 3f, 3f, Colors.White);
            }
            k.Col = new Color(0.22f, 0.22f, 0.21f);
            if (rng.Randf() < 0.5f)
            {
                // Sheds (silhouette en dents de scie).
                for (var z = -hz; z < hz - 6f; z += 7f)
                    k.Quad(detail, p + new Vector3(-hx, h, z + 7f), p + new Vector3(hx, h, z + 7f), p + new Vector3(hx, h + 3f, z), p + new Vector3(-hx, h + 3f, z));
            }
            k.Quad(detail, p + new Vector3(-hx, h, hz), p + new Vector3(hx, h, hz), p + new Vector3(hx, h, -hz), p + new Vector3(-hx, h, -hz));
            // Cheminées sur certaines usines (quelques-unes fument).
            if (rng.Randf() < 0.14f)
            {
                var ch = rng.RandfRange(45f, 95f);
                var cp = p + new Vector3(hx + 4f, 0, 0);
                k.Col = rng.Randf() < 0.5f ? new Color(0.42f, 0.3f, 0.25f) : new Color(0.5f, 0.49f, 0.46f);
                k.Cylinder(detail, cp, ch * 0.05f, ch * 0.032f, ch, 10, false);
                if (rng.Randf() < 0.35f) c.Ruins.Smolder(cp + Vector3.Up * (ch + 2f), 3f);
            }
        }

        // Parc de cuves voisin (est) et tours de refroidissement (nord-ouest).
        for (var i = 0; i < 14; i++)
        {
            var p = new Vector3(330f + (i % 4) * 34f + rng.RandfRange(-4, 4), 0, -320f + (i / 4) * 40f);
            var r = rng.RandfRange(9f, 15f);
            var h = rng.RandfRange(10f, 16f);
            var k = At(p);
            k.Col = Colors.White;
            k.Cylinder(tankPaint, p, r, r, h, 24, false);
            k.Cylinder(tankPaint, p + Vector3.Up * h, r, 1f, r * 0.12f, 24, true);
            if (i == 5) c.Ruins.Smolder(p + Vector3.Up * (h + 2f), 4f);
        }
        foreach (var (x, z, s) in new[] { (-640f, -520f, 1f), (-520f, -640f, 0.9f), (-760f, -380f, 1.1f) })
            CoolingTower(At(new Vector3(x, 0, z)), new Vector3(x, 0, z), s, detail);

        // Gazomètre à charpente (cuve télescopique affaissée dans son guidage).
        GasHolder(At(new Vector3(-420, 0, 420)), new Vector3(-420, 0, 420), metal, tankPaint);

        // Ligne à haute tension traversant le paysage au nord.
        var prev = Vector3.Zero;
        for (var i = 0; i < 9; i++)
        {
            var p = new Vector3(-900f + i * 230f, 0, -420f - 60f * Mathf.Sin(i * 0.7f));
            Pylon(At(p), p, metal, out var arm);
            if (i > 0)
            {
                var k = At((p + prev) / 2);
                k.Col = new Color(0.1f, 0.1f, 0.1f);
                foreach (var dx in new[] { -7f, 0f, 7f })
                {
                    var a = prev + arm + new Vector3(dx, 0, 0);
                    var b = p + arm + new Vector3(dx, 0, 0);
                    // Chaînette (trois tronçons).
                    var m1 = a.Lerp(b, 0.33f) - Vector3.Up * 6f;
                    var m2 = a.Lerp(b, 0.66f) - Vector3.Up * 6f;
                    k.Tube(metal, a, m1, 0.06f, 3);
                    k.Tube(metal, m1, m2, 0.06f, 3);
                    k.Tube(metal, m2, b, 0.06f, 3);
                }
            }
            prev = p;
        }

        // Portiques du port, loin au sud (silhouettes).
        for (var i = 0; i < 5; i++)
        {
            var p = new Vector3(-300f + i * 150f, 0, 640f);
            var k = At(p);
            k.Col = new Color(0.38f, 0.3f, 0.16f);
            foreach (var dx in new[] { -10f, 10f })
                foreach (var dz in new[] { -8f, 8f })
                    k.Box(metal, p + new Vector3(dx, 22f, dz), new Vector3(1.6f, 44f, 1.6f));
            k.Box(metal, p + new Vector3(0, 46f, -10f), new Vector3(3f, 4f, 90f));
        }

        foreach (var (key, k) in sectors)
            c.Root.AddChild(new MeshInstance3D { Name = $"IndSkyline_{key.Item1}_{key.Item2}", Mesh = k.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Tour de refroidissement (hyperboloïde de béton sur pieds en V).</summary>
    private static void CoolingTower(MeshKit k, Vector3 p, float s, Material m)
    {
        const int seg = 36;
        var h = 120f * s;
        float R(float y) => (32f + 24f * Mathf.Pow((y / h - 0.72f) / 0.72f, 2f)) * s;
        const int rings = 10;
        for (var j = 0; j < rings; j++)
        {
            var y0 = 8f * s + (h - 8f * s) * j / rings;
            var y1 = 8f * s + (h - 8f * s) * (j + 1) / rings;
            for (var i = 0; i < seg; i++)
            {
                var a0 = i * Mathf.Tau / seg;
                var a1 = (i + 1) * Mathf.Tau / seg;
                Vector3 P(float a, float y) => p + new Vector3(Mathf.Cos(a) * R(y), y, Mathf.Sin(a) * R(y));
                k.Col = new Color(0.52f, 0.51f, 0.48f) * (0.85f + 0.15f * j / rings);
                var radial = new Vector3(Mathf.Cos((a0 + a1) / 2), 0, Mathf.Sin((a0 + a1) / 2));
                k.OrientedQuad(m, P(a0, y0), P(a1, y0), P(a1, y1), P(a0, y1), radial);
            }
        }
        k.Col = new Color(0.3f, 0.3f, 0.29f);
        for (var i = 0; i < 24; i++)
        {
            var a = i * Mathf.Tau / 24;
            var r0 = R(8f * s);
            k.Tube(m, p + new Vector3(Mathf.Cos(a) * r0, 8f * s, Mathf.Sin(a) * r0), p + new Vector3(Mathf.Cos(a + 0.12f) * r0 * 1.04f, 0, Mathf.Sin(a + 0.12f) * r0 * 1.04f), 0.8f * s, 4);
        }
    }

    /// <summary>Gazomètre : anneaux et poteaux de guidage, cuve basse affaissée.</summary>
    private static void GasHolder(MeshKit k, Vector3 p, Material metal, Material paint)
    {
        const float r = 34f, h = 52f;
        k.Col = Colors.White * 0.85f;
        k.Cylinder(paint, p, r - 1f, r - 1f, 14f, 32, true);
        k.Col = new Color(0.2f, 0.16f, 0.14f);
        for (var i = 0; i < 16; i++)
        {
            var a = i * Mathf.Tau / 16;
            k.Box(metal, p + new Vector3(Mathf.Cos(a) * r, h / 2, Mathf.Sin(a) * r), new Vector3(1.2f, h, 1.2f), new Basis(Vector3.Up, -a));
        }
        for (var y = 12f; y <= h; y += 13f) k.Cylinder(metal, p + Vector3.Up * y, r + 0.5f, r + 0.5f, 0.8f, 32, false);
    }

    /// <summary>Pylône à haute tension (treillis évoqué par quatre membrures, consoles). <paramref name="arm"/> : hauteur des conducteurs.</summary>
    private static void Pylon(MeshKit k, Vector3 p, Material metal, out Vector3 arm)
    {
        const float h = 52f;
        k.Col = new Color(0.32f, 0.33f, 0.33f);
        foreach (var (dx, dz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
            k.Tube(metal, p + new Vector3(dx * 5f, 0, dz * 5f), p + new Vector3(dx * 1f, h, dz * 1f), 0.35f, 4);
        for (var y = 8f; y < h; y += 8f)
        {
            var w = Mathf.Lerp(5f, 1f, y / h);
            k.Tube(metal, p + new Vector3(-w, y, -w), p + new Vector3(w, y + 8f, w), 0.15f, 3);
            k.Tube(metal, p + new Vector3(w, y, -w), p + new Vector3(-w, y + 8f, w), 0.15f, 3);
        }
        k.Box(metal, p + new Vector3(0, h - 8f, 0), new Vector3(16f, 0.8f, 0.8f));
        k.Box(metal, p + new Vector3(0, h - 16f, 0), new Vector3(12f, 0.8f, 0.8f));
        arm = new Vector3(0, h - 9f, 0);
    }
}
