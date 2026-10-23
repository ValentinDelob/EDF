using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>Drone d'attaque extraterrestre et nid de fourmilière.</summary>
public partial class BugModel
{
    // ---------------- Drone ----------------

    /// <summary>Drone d'attaque extraterrestre : soucoupe blindée à panneaux, quatre rotors carénés, œil laser.</summary>
    private void BuildDrone()
    {
        _flying = true;
        _drone = true;
        var hullC = new Color(0.42f, 0.44f, 0.47f);
        var darkC = new Color(0.1f, 0.1f, 0.12f);
        var groove = new Color(0.03f, 0.03f, 0.035f);
        _bodyPivot = new Node3D();
        AddChild(_bodyPivot);
        _head = new Node3D();
        _bodyPivot.AddChild(_head);
        var arms = new Vector3[4];
        for (var i = 0; i < 4; i++)
        {
            var a = i * Mathf.Pi / 2 + Mathf.Pi / 4;
            arms[i] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
        }
        Part(_bodyPivot, "hull", m =>
        {
            m.Use(Metal());
            // Profil de révolution (hauteur, rayon, teinte) : rainures sombres = joints de panneaux.
            var prof = new (float Y, float R, Color C)[]
            {
                (-0.45f, 0f, darkC), (-0.44f, 0.3f, darkC), (-0.4f, 0.55f, darkC), (-0.37f, 0.6f, groove), (-0.34f, 0.66f, hullC),
                (-0.22f, 1.0f, hullC), (-0.13f, 1.2f, hullC), (-0.115f, 1.18f, groove), (-0.1f, 1.24f, hullC), (-0.04f, 1.36f, hullC),
                (0f, 1.36f, darkC), (0.04f, 1.3f, hullC), (0.1f, 1.16f, hullC), (0.112f, 1.12f, groove), (0.125f, 1.1f, hullC),
                (0.2f, 0.82f, hullC), (0.215f, 0.76f, groove), (0.23f, 0.72f, darkC), (0.24f, 0f, darkC),
            };
            var rings = new List<InsectMesh.Ring>();
            foreach (var (y, r, c) in prof) rings.Add(new InsectMesh.Ring(new Vector3(0, y, 0), Vector3.Right, Vector3.Back, r, r, c));
            m.Loft(rings, 32);
            // Nervures radiales sur le dessus, aérations dessous.
            for (var k = 0; k < 8; k++)
            {
                var a = k * Mathf.Tau / 8 + Mathf.Tau / 16;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                m.Tube(new[] { d * 0.78f + new Vector3(0, 0.222f, 0), d * 1.13f + new Vector3(0, 0.122f, 0) }, new[] { 0.035f, 0.035f }, 4, darkC, Vector3.Up, 1f, null, 4f);
                m.Box(new Transform3D(new Basis(Vector3.Up, -a), d * 0.85f + new Vector3(0, -0.29f, 0)), new Vector3(0.12f, 0.03f, 0.05f), groove);
            }
            // Bras et carénages de rotors.
            foreach (var d in arms)
            {
                var c = d * 1.6f + new Vector3(0, 0.1f, 0);
                m.Tube(new[] { d * 0.9f + new Vector3(0, -0.05f, 0), d * 1.5f + new Vector3(0, 0.08f, 0) }, new[] { 0.14f, 0.1f }, 6, darkC, Vector3.Up, 0.55f, null, 4f);
                m.Torus(c, Basis.Identity, 0.76f, 0.08f, 24, 6, hullC);
                m.Ellipsoid(c, new Vector3(0.15f, 0.13f, 0.15f), 4, 8, darkC);
                m.Box(new Transform3D(new Basis(Vector3.Up, -Mathf.Atan2(d.Z, d.X)), c + d * 0.4f), new Vector3(0.38f, 0.02f, 0.03f), darkC);
            }
            // Canon ventral et logement de l'œil.
            m.Segment(new Vector3(0, -0.42f, 0.2f), new Vector3(0, -0.5f, 1.05f), 0.1f, 0.07f, 8, darkC, Vector3.Up, 0.25f, 2);
            m.Torus(new Vector3(0, -0.1f, 1.16f), new Basis(Vector3.Right, Mathf.Pi / 2), 0.28f, 0.05f, 16, 5, darkC);
            // Dôme sombre et vitreux.
            m.Use(Eye());
            m.Ellipsoid(new Vector3(0, 0.2f, 0), new Vector3(0.7f, 0.7f, 0.32f), 6, 20, new Color(0.06f, 0.03f, 0.09f), new Basis(Vector3.Right, Mathf.Pi / 2));
            m.Use(Mats.Glow(new Color(1f, 0.12f, 0.08f), 4f));
            m.Ellipsoid(new Vector3(0, -0.1f, 1.18f), new Vector3(0.26f, 0.14f, 0.1f), 5, 10, Colors.White);
            m.Ellipsoid(new Vector3(0, -0.5f, 1.06f), new Vector3(0.05f, 0.05f, 0.03f), 3, 6, Colors.White);
            m.Use(Mats.Glow(new Color(1f, 0.4f, 0.12f), 2.2f));
            m.Torus(new Vector3(0, -0.02f, 0), Basis.Identity, 1.375f, 0.022f, 48, 4, Colors.White);
            m.Ellipsoid(new Vector3(0, -0.44f, 0), new Vector3(0.28f, 0.28f, 0.03f), 3, 12, Colors.White, new Basis(Vector3.Right, Mathf.Pi / 2));
            foreach (var d in arms) m.Ellipsoid(d * 2.38f + new Vector3(0, 0.1f, 0), Vector3.One * 0.06f, 3, 6, Colors.White);
        });
        foreach (var d in arms)
        {
            var rotor = new Node3D { Position = d * 1.6f + new Vector3(0, 0.12f, 0) };
            _bodyPivot.AddChild(rotor);
            Part(rotor, "rotor", m =>
            {
                m.Use(Metal());
                for (var k = 0; k < 3; k++)
                {
                    var b = new Basis(Vector3.Up, k * Mathf.Tau / 3) * new Basis(Vector3.Right, 0.25f);
                    m.Box(new Transform3D(b, b * new Vector3(0.36f, 0, 0)), new Vector3(0.32f, 0.012f, 0.06f), darkC);
                }
                m.Use(WingMat());
                var o = new Vector3[20];
                var cols = new Color[20];
                for (var k = 0; k < 20; k++)
                {
                    var a = k * Mathf.Tau / 20;
                    o[k] = new Vector3(Mathf.Cos(a) * 0.7f, 0, Mathf.Sin(a) * 0.7f);
                    cols[k] = new Color(0.7f, 0.75f, 0.8f, 0.12f);
                }
                m.Flat(o, Vector3.Up, cols, new Color(0.7f, 0.75f, 0.8f, 0.2f));
            }, false, 90f);
            _wings.Add(rotor);
        }
        AddChild(new OmniLight3D { Position = new Vector3(0, -0.3f, 1.3f), LightColor = new Color(1f, 0.15f, 0.1f), LightEnergy = 1f, OmniRange = 6f });
    }

    // ---------------- Nid ----------------

    /// <summary>Fourmilière géante : butte irrégulière, cratère luminescent, galeries, gravats urbains et œufs.</summary>
    private void BuildNest()
    {
        var noise = new FastNoiseLite { Seed = 5, Frequency = 0.16f, FractalOctaves = 3 };
        float BaseR(float a) => 9.5f + noise.GetNoise2D(Mathf.Cos(a) * 7f, Mathf.Sin(a) * 7f) * 2.2f;
        float H(float r, float a)
        {
            var rb = BaseR(a);
            var s = Mathf.Clamp((r - 2.25f) / (rb - 2.25f), 0f, 1.1f);
            var h = 4.55f * Mathf.Pow(Mathf.Max(0, 1f - s), 1.5f) - (s > 1f ? (s - 1f) * 4f : 0f);
            return h + noise.GetNoise2D(Mathf.Cos(a) * r * 3f, Mathf.Sin(a) * r * 3f) * 0.55f * (1f - 0.4f * s);
        }
        var dirtC = Colors.White;
        Part(this, "mound", m =>
        {
            const int M = 44;
            // Butte : de la lèvre intérieure du cratère jusqu'au pied enterré.
            m.Use(Mats.Dirt());
            var rs = new List<float> { 1.95f, 2.25f };
            for (var i = 1; i <= 14; i++) rs.Add(2.25f + Mathf.Pow(i / 14f, 1.25f) * 8.2f);
            var pos = new Vector3[rs.Count, M];
            var col = new Color[rs.Count, M];
            var cen = new Vector3[rs.Count];
            for (var i = 0; i < rs.Count; i++)
            {
                cen[i] = new Vector3(0, -3f, 0);
                for (var j = 0; j < M; j++)
                {
                    var a = j * Mathf.Tau / M;
                    var r = i < 2 ? rs[i] : 2.25f + (rs[i] - 2.25f) / 8.2f * (BaseR(a) + 0.8f - 2.25f);
                    var h = i == 0 ? 4.25f : H(r, a);
                    pos[i, j] = new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r);
                    col[i, j] = dirtC;
                }
            }
            m.Grid(pos, col, cen);
            // Gorge sombre du cratère.
            m.Use(Mats.Painted(new Color(0.03f, 0.025f, 0.02f), 1f));
            var throat = new (float R, float Y)[] { (1.95f, 4.25f), (1.7f, 3.8f), (1.35f, 3.1f), (1.0f, 2.2f), (0.6f, 1.4f), (0f, 1.1f) };
            var tp = new Vector3[throat.Length, 20];
            var tc = new Color[throat.Length, 20];
            var tcen = new Vector3[throat.Length];
            for (var i = 0; i < throat.Length; i++)
            {
                tcen[i] = new Vector3(0, throat[i].Y, 0);
                for (var j = 0; j < 20; j++)
                {
                    var a = j * Mathf.Tau / 20;
                    var rr = i == 0 ? throat[i].R : throat[i].R * (1f + noise.GetNoise2D(j * 3f, i * 5f) * 0.25f);
                    tp[i, j] = new Vector3(Mathf.Cos(a) * rr, throat[i].Y, Mathf.Sin(a) * rr);
                    tc[i, j] = Colors.White;
                }
            }
            m.Grid(tp, tc, tcen, true);

            // Galeries latérales : bourrelet de terre et trou noir.
            var rng = new RandomNumberGenerator { Seed = 99 };
            var holes = new List<(Vector3 P, Vector3 N, float A)>();
            for (var k = 0; k < 5; k++)
            {
                var a = k * Mathf.Tau / 5 + 0.4f;
                var r = 4.6f + (k % 2) * 1.6f;
                var p = new Vector3(Mathf.Cos(a) * r, H(r, a), Mathf.Sin(a) * r);
                var slope = (H(r + 0.3f, a) - H(r - 0.3f, a)) / 0.6f;
                var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var n = (radial * -slope + Vector3.Up).Normalized();
                holes.Add((p, n, a));
            }
            m.Use(Mats.Dirt());
            // Mottes de terre rejetées par les ouvrières.
            for (var k = 0; k < 14; k++)
            {
                var a = rng.RandfRange(0, Mathf.Tau);
                var r = rng.RandfRange(3f, 9.5f);
                var s = rng.RandfRange(0.35f, 0.8f);
                m.Ellipsoid(new Vector3(Mathf.Cos(a) * r, H(r, a) - s * 0.25f, Mathf.Sin(a) * r), new Vector3(s * 1.3f, s, s * 0.6f), 4, 7, dirtC,
                    new Basis(Vector3.Up, rng.RandfRange(0, 3)) * new Basis(Vector3.Right, Mathf.Pi / 2));
            }
            foreach (var (p, n, a) in holes)
            {
                var x = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                var b = new Basis(x, n, x.Cross(n));
                var seed = a;
                m.Torus(p + n * 0.05f, b, 0.95f, 0.3f, 14, 6, dirtC, t => 1f + 0.15f * Mathf.Sin(t * 3f + seed));
            }
            m.Use(Mats.Painted(new Color(0.03f, 0.025f, 0.02f), 1f));
            foreach (var (p, n, a) in holes)
            {
                var x = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                m.Ellipsoid(p - n * 0.12f, new Vector3(0.95f, 0.8f, 0.3f), 4, 10, Colors.White, new Basis(x, n.Cross(x).Normalized(), n));
            }

            // Gravats urbains arrachés : béton, enrobé, fers à béton.
            m.Use(Mats.Concrete(new Color(0.4f, 0.38f, 0.35f), 3));
            var rebar = new List<(Vector3, Vector3)>();
            for (var k = 0; k < 16; k++)
            {
                var a = rng.RandfRange(0, Mathf.Tau);
                var r = rng.RandfRange(3f, 11f);
                var half = new Vector3(rng.RandfRange(0.3f, 0.9f), rng.RandfRange(0.12f, 0.35f), rng.RandfRange(0.3f, 0.8f));
                var p = new Vector3(Mathf.Cos(a) * r, Mathf.Max(H(r, a), 0f) - 0.05f, Mathf.Sin(a) * r);
                var b = new Basis(new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-1, 1), rng.RandfRange(-1, 1)).Normalized(), rng.RandfRange(0.2f, 0.8f)) * new Basis(Vector3.Up, rng.RandfRange(0, 3));
                m.Box(new Transform3D(b, p), half, Colors.White);
                if (k % 3 == 0) rebar.Add((p + b * new Vector3(half.X * 0.6f, half.Y, 0), b.Y));
            }
            m.Use(Mats.Asphalt());
            for (var k = 0; k < 5; k++)
            {
                var a = rng.RandfRange(0, Mathf.Tau);
                var r = rng.RandfRange(8f, 11.5f);
                var p = new Vector3(Mathf.Cos(a) * r, 0.15f, Mathf.Sin(a) * r);
                var b = new Basis(new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a)), rng.RandfRange(0.25f, 0.6f)) * new Basis(Vector3.Up, -a);
                m.Box(new Transform3D(b, p), new Vector3(rng.RandfRange(0.8f, 1.6f), 0.12f, rng.RandfRange(0.7f, 1.3f)), Colors.White);
            }
            m.Use(Metal());
            foreach (var (p, up) in rebar)
            {
                var side = up.Cross(Vector3.Right).Normalized();
                m.Tube(new[] { p, p + up * 0.7f, p + up * 1.1f + side * 0.4f }, new[] { 0.035f, 0.035f, 0.03f }, 4, new Color(0.35f, 0.18f, 0.1f), side);
            }

            // Œufs pâles luisants et anneau de lueur au fond du cratère.
            m.Use(Mats.Glow(new Color(0.78f, 0.85f, 0.6f), 0.7f));
            foreach (var (p, n, a) in holes)
                for (var e = 0; e < 3; e++)
                {
                    var o = new Vector3(rng.RandfRange(-0.4f, 0.4f), 0, rng.RandfRange(-0.4f, 0.4f));
                    m.Ellipsoid(p + n * 0.15f + o, new Vector3(0.22f, 0.22f, 0.36f), 4, 8, Colors.White, new Basis(Vector3.Up, rng.RandfRange(0, 3)));
                }
            m.Use(Mats.Glow(new Color(0.65f, 0.85f, 0.2f), 1.5f));
            m.Torus(new Vector3(0, 3.9f, 0), Basis.Identity, 1.78f, 0.07f, 36, 4, Colors.White);
            m.Ellipsoid(new Vector3(0, 1.3f, 0), new Vector3(0.9f, 0.9f, 0.12f), 3, 12, Colors.White, new Basis(Vector3.Right, Mathf.Pi / 2));
        });
        AddChild(new OmniLight3D { Position = new Vector3(0, 5.5f, 0), LightColor = new Color(0.65f, 0.9f, 0.3f), LightEnergy = 1.5f, OmniRange = 14 });
    }
}
