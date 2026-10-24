using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>Guêpe et araignée géantes.</summary>
public partial class BugModel
{
    // ---------------- Guêpe ----------------

    private static readonly Color WaspBlack = new(0.06f, 0.05f, 0.04f);
    private static readonly Color WaspYellow = new(0.86f, 0.64f, 0.08f);

    private void BuildWasp()
    {
        _flying = true;
        Scale = Vector3.One * 0.9f;
        var ch = Chitin();
        _bodyPivot = new Bone();
        AddChild(_bodyPivot);

        // Mésosome noir marqué de jaune, pétiole très fin (taille de guêpe).
        Part(_bodyPivot, "body", m =>
        {
            m.Use(ch);
            BodyLoft(m, -0.5f, 0.55f,
                new[] { 0f, 0.3f, 0.4f, 0.44f, 0.44f, 0.4f, 0.32f, 0.18f, 0f },
                new[] { 0f, 0.26f, 0.38f, 0.44f, 0.45f, 0.42f, 0.34f, 0.2f, 0f },
                new[] { 0f, -0.22f, -0.34f, -0.4f, -0.4f, -0.36f, -0.28f, -0.14f, 0f },
                Steps(14), 14, WaspBlack, 2.1f, null, (t, th, p, c) =>
                {
                    if (t > 0.2f && t < 0.3f && p.Y > 0.18f) return WaspYellow; // scutellum
                    if (t > 0.78f && p.Y > 0.05f) return WaspYellow;             // bord du pronotum
                    if (Mathf.Abs(p.X) > 0.3f && t > 0.45f && t < 0.6f && p.Y > -0.12f && p.Y < 0.15f) return WaspYellow;
                    return c;
                });
            m.Segment(new Vector3(0, -0.05f, -0.4f), new Vector3(0, -0.1f, -0.84f), 0.11f, 0.1f, 8, WaspBlack, Vector3.Up, mid: 3,
                shape: t => 1f - 0.5f * Mathf.Sin(Mathf.Pi * t));
            m.Segment(new Vector3(0, 0.02f, 0.45f), new Vector3(0, 0.05f, 0.62f), 0.14f, 0.12f, 8, WaspBlack, Vector3.Up, mid: 0);
        });

        // Pattes pendantes (statiques), fémurs noirs, tibias et tarses jaunes.
        Part(_bodyPivot, "legs", m =>
        {
            m.Use(ch);
            var zc = new[] { 0.3f, 0.05f, -0.2f };
            for (var i = 0; i < 3; i++)
                foreach (var s in new[] { 1f, -1f })
                {
                    var b = new Vector3(0.15f * s, -0.32f, zc[i]);
                    var f = b + new Vector3(0.35f * s, -0.3f, 0.18f - 0.18f * i);
                    var t = f + new Vector3(0.06f * s, -0.55f - 0.08f * i, -0.22f - 0.08f * i);
                    m.Segment(b, f, 0.07f, 0.055f, 5, WaspBlack, Vector3.Up, 0.2f, 1);
                    m.Segment(f, t, 0.05f, 0.042f, 5, WaspYellow.Darkened(0.1f), Vector3.Up, 0.2f, 1);
                    var d = new Vector3(0, -0.85f, -0.5f).Normalized();
                    var pts = new List<Vector3> { t - d * 0.04f, t };
                    var rad = new List<float> { 0f, 0.036f };
                    var p = t;
                    for (var k = 0; k < 4; k++)
                    {
                        pts.Add(p + d * 0.05f); rad.Add(0.032f);
                        pts.Add(p + d * 0.1f); rad.Add(0.024f);
                        p += d * 0.11f;
                    }
                    pts.Add(p); rad.Add(0f);
                    m.Tube(pts, rad, 4, WaspYellow, Vector3.Forward);
                    m.Cone(f + new Vector3(0, -0.04f, 0), f + new Vector3(0.05f * s, -0.16f, -0.05f), 0.015f, 3, WaspBlack);
                }
        }, false, 80f);

        // Gastre rayé (tergites noirs bordés de jaune, encoche dorsale) et dard.
        _gaster = new Bone { Position = new Vector3(0, -0.1f, -0.8f), RotationDegrees = new Vector3(-20, 0, 0) };
        _bodyPivot.AddChild(_gaster);
        Part(_gaster, "gaster", m =>
        {
            m.Use(ch);
            var edges = new[] { 0.18f, 0.36f, 0.52f, 0.66f, 0.79f, 0.89f, 1.01f };
            float Dip(float t)
            {
                var d = 0f;
                foreach (var e in edges)
                    if (t >= e && t < e + 0.07f) d = Mathf.Max(d, 1f - (t - e) / 0.07f);
                return d;
            }
            var extra = new List<float>();
            foreach (var e in edges) if (e < 1f) { extra.Add(e - 0.008f); extra.Add(e + 0.004f); }
            BodyLoft(m, 0.06f, -1.75f,
                new[] { 0f, 0.26f, 0.46f, 0.54f, 0.55f, 0.52f, 0.45f, 0.34f, 0.2f, 0f },
                new[] { 0f, 0.2f, 0.4f, 0.5f, 0.52f, 0.48f, 0.38f, 0.24f, 0.08f, -0.04f },
                new[] { 0f, -0.2f, -0.36f, -0.44f, -0.46f, -0.42f, -0.34f, -0.22f, -0.1f, -0.04f },
                Steps(20, extra.ToArray()), 14, WaspBlack, 2.1f,
                t => 1f - 0.05f * Dip(t),
                (t, th, p, c) =>
                {
                    var k = 0;
                    while (k < edges.Length - 1 && t >= edges[k]) k++;
                    var e0 = k == 0 ? 0f : edges[k - 1];
                    var s = (t - e0) / (edges[k] - e0);
                    if (k == 0) return s > 0.75f ? WaspYellow : c;
                    if (s < 0.45f) return c;
                    if (s < 0.68f && p.Y > 0.1f && Mathf.Abs(p.X) < 0.1f) return c; // encoche dorsale
                    return WaspYellow;
                });
            m.Hook(new[] { new Vector3(0, -0.05f, -1.68f), new Vector3(0, -0.09f, -1.9f), new Vector3(0, -0.16f, -2.08f) }, 0.05f, 4, new Color(0.12f, 0.07f, 0.03f), Vector3.Up);
        });

        // Tête : face jaune, gros yeux réniformes, ocelles, mandibules, antennes coudées.
        _head = new Bone { Position = new Vector3(0, 0.05f, 0.62f) };
        _bodyPivot.AddChild(_head);
        Part(_head, "head", m =>
        {
            m.Use(ch);
            BodyLoft(m, -0.12f, 0.32f,
                new[] { 0f, 0.34f, 0.42f, 0.42f, 0.36f, 0.22f, 0f },
                new[] { 0f, 0.28f, 0.36f, 0.36f, 0.3f, 0.16f, -0.02f },
                new[] { 0f, -0.3f, -0.38f, -0.4f, -0.38f, -0.3f, -0.16f },
                Steps(10), 14, WaspBlack, 2.2f, null,
                (t, th, p, c) => p.Z > 0.08f && p.Y < 0.2f ? WaspYellow : c);
            foreach (var s in new[] { 1f, -1f })
            {
                m.Hook(new[] { new Vector3(0.12f * s, -0.32f, 0.22f), new Vector3(0.08f * s, -0.44f, 0.28f), new Vector3(-0.02f * s, -0.5f, 0.26f) }, 0.06f, 4, new Color(0.55f, 0.35f, 0.06f), Vector3.Forward);
                var sc = new Vector3(0.1f * s, 0.12f, 0.28f);
                var el = new Vector3(0.18f * s, 0.42f, 0.5f);
                m.Segment(sc, el, 0.04f, 0.045f, 5, WaspYellow.Darkened(0.2f), Vector3.Up, mid: 0);
                var pts = new List<Vector3> { el };
                var rad = new List<float> { 0f };
                var p = el;
                for (var k = 0; k < 10; k++)
                {
                    var d = new Vector3(0.35f * s, 0.25f - 0.12f * k, 1f).Normalized();
                    pts.Add(p + d * 0.05f); rad.Add(0.034f);
                    p += d * 0.09f;
                }
                pts.Add(p); rad.Add(0f);
                m.Tube(pts, rad, 4, WaspBlack, Vector3.Up);
            }
            m.Use(Eye());
            foreach (var s in new[] { 1f, -1f })
                m.Ellipsoid(new Vector3(0.32f * s, 0.06f, 0.1f), new Vector3(0.13f, 0.3f, 0.19f), 8, 10, new Color(0.2f, 0.08f, 0.02f), new Basis(Vector3.Forward, 0.25f * s));
            for (var k = 0; k < 3; k++)
                m.Ellipsoid(new Vector3((k - 1) * 0.07f, 0.34f, 0.02f + (k == 1 ? 0.06f : 0)), Vector3.One * 0.03f, 3, 5, new Color(0.1f, 0.05f, 0.02f));
        });

        // Deux paires d'ailes membraneuses nervurées.
        foreach (var s in new[] { 1f, -1f })
            for (var k = 0; k < 2; k++)
            {
                var hinge = new Bone { Position = new Vector3(0.3f * s, 0.38f - k * 0.04f, 0.2f - k * 0.3f) };
                _bodyPivot.AddChild(hinge);
                var sc = k == 0 ? 1f : 0.66f;
                Part(hinge, $"wing{k}{(s > 0 ? "R" : "L")}", m => { m.Mirror = s < 0; BuildWaspWing(m, sc, k == 1); }, false, 90f);
                _wings.Add(hinge);
            }
    }

    private static void BuildWaspWing(InsectMesh m, float sc, bool hind)
    {
        m.Xf = new Transform3D(Basis.FromScale(Vector3.One * sc), Vector3.Zero);
        var o = new[]
        {
            new Vector3(0, 0, 0.05f), new Vector3(0.35f, 0.02f, 0.15f), new Vector3(1.0f, 0.03f, 0.12f), new Vector3(1.6f, 0.03f, -0.05f),
            new Vector3(1.95f, 0.02f, -0.25f), new Vector3(1.8f, 0f, -0.42f), new Vector3(1.2f, -0.01f, -0.45f), new Vector3(0.6f, 0f, -0.32f), new Vector3(0.15f, 0f, -0.12f),
        };
        var cols = new Color[o.Length];
        for (var i = 0; i < o.Length; i++)
            cols[i] = i <= 3 ? new Color(0.42f, 0.3f, 0.12f, 0.62f) : new Color(0.6f, 0.52f, 0.38f, 0.4f);
        m.Use(WingMat());
        m.Flat(o, Vector3.Up, cols, new Color(0.55f, 0.45f, 0.28f, 0.45f));
        m.Use(Chitin());
        var vein = new Color(0.14f, 0.09f, 0.04f);
        void V(params Vector3[] p)
        {
            var r = new float[p.Length];
            for (var i = 0; i < p.Length; i++) r[i] = 0.018f * (1f - 0.4f * i / Mathf.Max(1, p.Length - 1));
            m.Tube(p, r, 3, vein, Vector3.Up);
        }
        V(new Vector3(0, 0.01f, 0.05f), new Vector3(1.0f, 0.035f, 0.12f), new Vector3(1.6f, 0.035f, -0.05f));
        V(new Vector3(0.05f, 0.01f, -0.02f), new Vector3(0.9f, 0.03f, -0.05f), new Vector3(1.5f, 0.03f, -0.2f));
        V(new Vector3(0.1f, 0.01f, -0.08f), new Vector3(0.8f, 0.02f, -0.22f), new Vector3(1.3f, 0.02f, -0.36f));
        if (!hind)
        {
            V(new Vector3(0.15f, 0.01f, -0.12f), new Vector3(0.6f, 0.01f, -0.3f));
            V(new Vector3(0.9f, 0.03f, -0.05f), new Vector3(0.8f, 0.02f, -0.22f));
            V(new Vector3(1.5f, 0.03f, -0.2f), new Vector3(1.3f, 0.02f, -0.36f));
            m.Ellipsoid(new Vector3(1.05f, 0.035f, 0.1f), new Vector3(0.05f, 0.015f, 0.12f), 3, 5, vein, new Basis(Vector3.Up, 1.3f));
        }
    }

    // ---------------- Araignée ----------------

    private static readonly Color SpiderBody = new(0.22f, 0.15f, 0.1f);
    private static readonly Color SpiderMark = new(0.72f, 0.5f, 0.24f);

    private static Action<InsectMesh, float> SpiderFemur(Color col) => (m, lf) =>
    {
        m.Use(Chitin());
        m.Segment(Vector3.Zero, new Vector3(lf * 0.14f, 0, 0), 0.1f, 0.09f, 6, col, Vector3.Up, mid: 0);
        m.Segment(new Vector3(lf * 0.1f, 0, 0), new Vector3(lf, 0, 0), 0.1f, 0.08f, 6, col, Vector3.Up, 0.35f, 3,
            t => 1f + 0.15f * Mathf.Sin(Mathf.Pi * t), t => t > 0.86f ? col.Lerp(SpiderMark, 0.45f) : col);
        SpiderHairs(m, 0.15f * lf, lf * 0.95f, 0.085f, 12, col.Darkened(0.2f), 11);
    };

    private static Action<InsectMesh, float, float> SpiderTibia(Color col, float ls) => (m, lt, tau) =>
    {
        m.Use(Chitin());
        // Patelle + tibia, puis métatarse et tarse fins, griffes.
        m.Segment(Vector3.Zero, new Vector3(lt, 0, 0), 0.085f, 0.06f, 6, col, Vector3.Up, 0.25f, 3, null,
            t => t < 0.12f || t > 0.9f ? col.Lerp(SpiderMark, 0.45f) : col);
        SpiderHairs(m, 0.1f * lt, lt * 0.95f, 0.07f, 14, col.Darkened(0.2f), 23);
        var d = new Vector3(Mathf.Cos(tau), Mathf.Sin(tau), 0);
        var a = new Vector3(lt - 0.03f, 0, 0);
        var mid = a + d * ls * 0.62f;
        m.Segment(a, mid, 0.055f, 0.04f, 5, col, Vector3.Up, 0.3f, 1, null, t => t > 0.85f ? col.Lerp(SpiderMark, 0.4f) : col);
        m.Segment(mid, a + d * ls, 0.04f, 0.026f, 5, col, Vector3.Up, 0f, 0);
        var e = a + d * ls;
        m.Hook(new[] { e, e + d * 0.05f + new Vector3(0, -0.05f, 0), e + d * 0.06f + new Vector3(0, -0.11f, 0) }, 0.02f, 3, col.Darkened(0.5f), Vector3.Forward);
    };

    /// <summary>Soies raides réparties autour d'un article de patte (le long de +X).</summary>
    private static void SpiderHairs(InsectMesh m, float x0, float x1, float r, int n, Color col, int seed)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)seed };
        for (var i = 0; i < n; i++)
        {
            var x = Mathf.Lerp(x0, x1, (i + rng.Randf()) / n);
            var a = rng.RandfRange(-2.6f, 2.6f) + Mathf.Pi / 2; // surtout dessus et côtés
            var dir = new Vector3(0, Mathf.Sin(a), Mathf.Cos(a));
            var b = new Vector3(x, 0, 0) + dir * r * 0.8f;
            m.Cone(b, b + dir * rng.RandfRange(0.1f, 0.18f) + new Vector3(0.1f, 0, 0), 0.012f, 3, col);
        }
    }

    private void BuildSpider()
    {
        Scale = Vector3.One * 1.05f;
        _pivotY = 1.15f;
        var ch = Chitin();
        _bodyPivot = new Bone { Position = new Vector3(0, _pivotY, 0) };
        AddChild(_bodyPivot);
        var zc = new[] { 0.45f, 0.2f, -0.05f, -0.28f };

        // Céphalothorax bombé avec bande médiane claire et stries rayonnantes, pédicelle, hanches.
        Part(_bodyPivot, "body", m =>
        {
            m.Use(ch);
            BodyLoft(m, -0.45f, 0.95f,
                new[] { 0f, 0.42f, 0.58f, 0.62f, 0.6f, 0.52f, 0.4f, 0.26f, 0f },
                new[] { 0f, 0.18f, 0.28f, 0.32f, 0.33f, 0.34f, 0.32f, 0.24f, 0.06f },
                new[] { 0f, -0.14f, -0.2f, -0.22f, -0.22f, -0.2f, -0.17f, -0.12f, -0.04f },
                Steps(14), 16, SpiderBody, 2.2f, null, (t, th, p, c) =>
                {
                    if (p.Y > 0.12f && Mathf.Abs(p.X) < 0.1f) return SpiderMark;
                    var ang = Mathf.Atan2(p.X, p.Z - 0.15f);
                    return p.Y > 0.05f && Mathf.Abs(Mathf.Sin(ang * 4f)) < 0.25f ? c.Darkened(0.4f) : c;
                });
            m.Segment(new Vector3(0, 0.02f, -0.35f), new Vector3(0, 0.15f, -0.62f), 0.12f, 0.12f, 8, SpiderBody, Vector3.Up, mid: 1);
            for (var i = 0; i < 4; i++)
                foreach (var s in new[] { 1f, -1f })
                    m.Segment(new Vector3(0.2f * s, -0.08f, zc[i] * 0.9f), new Vector3(0.5f * s, -0.08f, zc[i]), 0.13f, 0.11f, 6, SpiderBody, Vector3.Up, mid: 0);
        });

        // Abdomen velu avec motif en chevrons (folium), filières.
        _gaster = new Bone { Position = new Vector3(0, 0.25f, -0.4f) };
        _bodyPivot.AddChild(_gaster);
        Part(_gaster, "abdomen", m =>
        {
            m.Use(ch);
            var width = new[] { 0f, 0.55f, 0.85f, 0.95f, 0.93f, 0.82f, 0.58f, 0.26f, 0f };
            var top = new[] { 0f, 0.42f, 0.78f, 0.95f, 0.98f, 0.88f, 0.62f, 0.3f, -0.05f };
            var bot = new[] { 0f, -0.3f, -0.5f, -0.58f, -0.58f, -0.52f, -0.38f, -0.18f, -0.1f };
            BodyLoft(m, -0.05f, -2.15f, width, top, bot, Steps(26), 18, SpiderBody, 2f, null, (t, th, p, c) =>
            {
                var w = InsectMesh.Spline(width, t);
                var x = Mathf.Abs(p.X) / Mathf.Max(w, 0.01f);
                var up = Mathf.Sin(th);
                if (up < 0.2f) return c.Darkened(0.3f);
                if (t < 0.45f && x < 0.12f) return c.Darkened(0.5f); // marque cardiaque
                var v = t * 5f + x * 1.3f;
                return x < 0.6f && t > 0.3f && v - Mathf.Floor(v) < 0.38f ? SpiderMark : c;
            });
            var rng = new RandomNumberGenerator { Seed = 7 };
            for (var i = 0; i < 70; i++)
            {
                var t = rng.RandfRange(0.08f, 0.92f);
                var th = rng.RandfRange(-0.4f, Mathf.Pi + 0.4f);
                var w = InsectMesh.Spline(width, t) * 0.97f;
                var tp = InsectMesh.Spline(top, t);
                var bt = InsectMesh.Spline(bot, t);
                var n = new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0);
                var b = new Vector3(n.X * w, (tp + bt) / 2 + n.Y * (tp - bt) / 2 * 0.97f, Mathf.Lerp(-0.05f, -2.15f, t));
                m.Cone(b, b + n * rng.RandfRange(0.1f, 0.2f) + new Vector3(0, 0, -0.08f), 0.014f, 3, SpiderMark.Darkened(0.3f));
            }
            foreach (var s in new[] { -1f, 0f, 1f })
                m.Cone(new Vector3(0.06f * s, -0.12f, -2.05f), new Vector3(0.09f * s, -0.18f, -2.25f), 0.05f, 4, SpiderBody.Darkened(0.3f), true);
        });

        // Tête : groupe de huit yeux luisants, chélicères à crochets, pédipalpes.
        _head = new Bone { Position = new Vector3(0, 0.12f, 0.78f) };
        _bodyPivot.AddChild(_head);
        Part(_head, "head", m =>
        {
            m.Use(ch);
            foreach (var s in new[] { 1f, -1f })
            {
                m.Segment(new Vector3(0.2f * s, -0.1f, 0.0f), new Vector3(0.38f * s, -0.15f, 0.35f), 0.07f, 0.06f, 5, SpiderBody, Vector3.Up, 0.2f, 0);
                m.Segment(new Vector3(0.38f * s, -0.15f, 0.35f), new Vector3(0.42f * s, -0.6f, 0.55f), 0.06f, 0.06f, 5, SpiderBody, Vector3.Up, 0.2f, 0);
                m.Ellipsoid(new Vector3(0.42f * s, -0.62f, 0.56f), new Vector3(0.08f, 0.1f, 0.08f), 4, 6, SpiderBody.Darkened(0.3f));
            }
            m.Use(Mats.Glow(new Color(0.9f, 0.15f, 0.08f), 1.5f));
            foreach (var s in new[] { 1f, -1f })
            {
                m.Ellipsoid(new Vector3(0.08f * s, 0.08f, 0.16f), Vector3.One * 0.075f, 5, 8, Colors.White);
                m.Ellipsoid(new Vector3(0.2f * s, 0.06f, 0.11f), Vector3.One * 0.05f, 4, 6, Colors.White);
                m.Ellipsoid(new Vector3(0.1f * s, 0.18f, 0.06f), Vector3.One * 0.05f, 4, 6, Colors.White);
                m.Ellipsoid(new Vector3(0.24f * s, 0.15f, -0.02f), Vector3.One * 0.045f, 4, 6, Colors.White);
            }
        });
        _mandL = new Bone { Position = new Vector3(0.12f, -0.08f, 0.12f) };
        _mandR = new Bone { Position = new Vector3(-0.12f, -0.08f, 0.12f) };
        _head.AddChild(_mandL);
        _head.AddChild(_mandR);
        foreach (var (mand, s) in new[] { (_mandL, 1f), (_mandR, -1f) })
            Part(mand, s > 0 ? "cheR" : "cheL", m =>
            {
                m.Mirror = s < 0;
                m.Use(Chitin());
                m.Segment(Vector3.Zero, new Vector3(0.02f, -0.42f, 0.12f), 0.13f, 0.1f, 7, SpiderBody, Vector3.Forward, 0.1f, 1);
                m.Hook(new[] { new Vector3(0.02f, -0.4f, 0.12f), new Vector3(-0.04f, -0.52f, 0.1f), new Vector3(-0.14f, -0.56f, 0.02f) }, 0.05f, 4, new Color(0.03f, 0.02f, 0.02f), Vector3.Forward);
            }, true, 70f);

        // Huit pattes : genoux bien au-dessus du corps.
        var fwd = new[] { 62f, 24f, -18f, -55f };
        var lfs = new[] { 1.55f, 1.4f, 1.35f, 1.6f };
        var lts = new[] { 1.75f, 1.55f, 1.5f, 1.8f };
        var reach = new[] { 2.05f, 1.9f, 1.85f, 2.1f };
        for (var i = 0; i < 4; i++)
            foreach (var side in new[] { 1f, -1f })
            {
                var leg = AddLeg($"leg{i}", new Vector3(0.5f * side, -0.08f, zc[i]), side, fwd[i], lfs[i], lts[i], 0.75f, reach[i], 55f,
                    SpiderFemur(SpiderBody), SpiderTibia(SpiderBody, 0.75f), 14f, 0.45f);
                leg.Phase = (i % 2 == 0 ? 0 : Mathf.Pi) + (side > 0 ? 0 : Mathf.Pi);
            }
    }
}
