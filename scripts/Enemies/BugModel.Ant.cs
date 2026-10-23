using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>Fourmis géantes : noire, rouge, acide, charognard et reine.</summary>
public partial class BugModel
{
    private static Action<InsectMesh, float> AntFemur(Color col, float th) => (m, lf) =>
    {
        var up = Vector3.Up;
        m.Use(Chitin());
        // Trochanter puis fémur renflé à sa base, genou légèrement bombé.
        m.Segment(Vector3.Zero, new Vector3(lf * 0.17f, -0.01f, 0), 0.08f * th, 0.07f * th, 6, col, up, mid: 0);
        m.Segment(new Vector3(lf * 0.12f, 0, 0), new Vector3(lf, 0, 0), 0.08f * th, 0.06f * th, 6, col, up, knob: 0.3f, mid: 2,
            shape: t => 1f + 0.3f * Mathf.Sin(Mathf.Pi * Mathf.Min(1, t * 1.4f)));
        var dark = col.Darkened(0.3f);
        for (var i = 0; i < 3; i++)
        {
            var x = lf * (0.45f + 0.15f * i);
            m.Cone(new Vector3(x, -0.06f * th, 0), new Vector3(x + 0.1f, -0.14f * th, 0), 0.014f, 3, dark);
        }
    };

    private static Action<InsectMesh, float, float> AntTibia(Color col, float th, float ls) => (m, lt, tau) =>
    {
        var up = Vector3.Up;
        var dark = col.Darkened(0.3f);
        m.Use(Chitin());
        m.Segment(Vector3.Zero, new Vector3(lt, 0, 0), 0.062f * th, 0.05f * th, 6, col, up, knob: 0.15f, mid: 2,
            shape: t => 1f - 0.12f * Mathf.Sin(Mathf.Pi * t));
        // Deux rangées de soies raides sous le tibia.
        for (var i = 0; i < 4; i++)
        {
            var x = lt * (0.25f + 0.18f * i);
            foreach (var z in new[] { -1f, 1f })
                m.Cone(new Vector3(x, -0.04f * th, 0.025f * z * th), new Vector3(x + 0.13f, -0.1f * th, 0.06f * z * th), 0.011f, 3, dark);
        }
        var d = new Vector3(Mathf.Cos(tau), Mathf.Sin(tau), 0);
        m.Cone(new Vector3(lt * 0.97f, -0.03f, 0), new Vector3(lt, -0.04f, 0) + d * 0.16f, 0.02f, 3, dark);
        // Tarse : cinq articles en chapelet, puis deux griffes.
        var lens = new[] { 0.34f, 0.17f, 0.15f, 0.13f, 0.21f };
        var pts = new List<Vector3>();
        var rad = new List<float>();
        var p = new Vector3(lt - 0.02f, 0, 0);
        pts.Add(p); rad.Add(0);
        for (var k = 0; k < 5; k++)
        {
            var l = lens[k] * ls;
            var r = 0.045f * th * (1f - 0.1f * k);
            pts.Add(p + d * l * 0.25f); rad.Add(r);
            pts.Add(p + d * l * 0.85f); rad.Add(r * 0.85f);
            pts.Add(p + d * l); rad.Add(r * 0.6f);
            p += d * l;
        }
        pts.Add(p + d * 0.02f); rad.Add(0);
        m.Tube(pts, rad, 4, col.Lightened(0.05f), up);
        foreach (var z in new[] { -1f, 1f })
            m.Hook(new[] { p, p + d * 0.07f + new Vector3(0, -0.02f, 0.03f * z), p + d * 0.1f + new Vector3(0, -0.09f, 0.05f * z) }, 0.018f, 3, dark, up);
    };

    private void BuildAnt(Color color, float size, float legScale, bool acid = false, bool queen = false)
    {
        Scale = Vector3.One * size;
        _pivotY = 1.05f;
        var ch = Chitin();
        var bodyC = color;
        var legC = color.Lerp(new Color(0.2f, 0.13f, 0.08f), 0.2f);
        var gasterC = Type == EnemyType.RedAnt ? new Color(0.17f, 0.05f, 0.035f) : color.Darkened(0.08f);
        var membrane = acid ? new Color(0.7f, 0.95f, 0.2f) : queen ? new Color(0.72f, 0.6f, 0.45f) : color.Lightened(0.18f);
        var twoNodes = Type is EnemyType.RedAnt or EnemyType.Queen or EnemyType.AcidAnt;
        var spines = Type is EnemyType.RedAnt or EnemyType.AcidAnt or EnemyType.Queen;
        var th = Type == EnemyType.Scavenger ? 0.85f : 1f;
        var zc = new[] { 0.5f, 0.1f, -0.22f };
        var dzc = new[] { 0.14f, 0f, -0.06f };

        _bodyPivot = new Node3D { Position = new Vector3(0, _pivotY, 0) };
        AddChild(_bodyPivot);

        // Mésosome (pronotum bombé, sillon métanotal, propodéum), pétiole(s) et hanches : une seule pièce.
        Part(_bodyPivot, "body", m =>
        {
            m.Use(ch);
            BodyLoft(m, -0.62f, 0.8f,
                new[] { 0f, 0.2f, 0.26f, 0.21f, 0.27f, 0.34f, 0.36f, 0.28f, 0.1f },
                new[] { 0.1f, 0.22f, 0.27f, 0.17f, 0.28f, 0.36f, 0.38f, 0.3f, 0.12f },
                new[] { 0.1f, -0.1f, -0.19f, -0.21f, -0.23f, -0.25f, -0.22f, -0.12f, 0.02f },
                Steps(18), 14, bodyC);
            if (twoNodes)
                BodyLoft(m, -0.5f, -1.04f,
                    new[] { 0f, 0.09f, 0.15f, 0.15f, 0.09f, 0.17f, 0.18f, 0.1f, 0f },
                    new[] { 0f, 0.02f, 0.24f, 0.26f, 0.05f, 0.18f, 0.2f, 0.05f, 0f },
                    new[] { 0f, -0.12f, -0.08f, -0.08f, -0.1f, -0.14f, -0.14f, -0.06f, 0f },
                    Steps(14), 10, bodyC.Darkened(0.1f));
            else
                BodyLoft(m, -0.5f, -1.04f,
                    new[] { 0f, 0.1f, 0.1f, 0.15f, 0.16f, 0.1f, 0.08f, 0f },
                    new[] { 0f, 0.02f, 0.05f, 0.26f, 0.3f, 0.1f, 0.05f, 0f },
                    new[] { 0f, -0.12f, -0.12f, -0.1f, -0.08f, -0.08f, -0.06f, 0f },
                    Steps(12), 10, bodyC.Darkened(0.1f));
            if (spines)
                foreach (var s in new[] { 1f, -1f })
                    m.Cone(new Vector3(0.1f * s, 0.2f, -0.42f), new Vector3(0.17f * s, 0.42f, -0.64f), 0.045f, 4, bodyC, true);
            for (var i = 0; i < 3; i++)
                foreach (var s in new[] { 1f, -1f })
                    m.Segment(new Vector3(0.12f * s, -0.12f, zc[i]), new Vector3(0.3f * s, -0.36f, zc[i] + dzc[i]), 0.12f, 0.09f, 6, bodyC, Vector3.Up, mid: 1);
            if (queen)
            {
                // Moignons d'ailes déchirés après le vol nuptial.
                foreach (var s in new[] { 1f, -1f })
                {
                    m.Mirror = s < 0;
                    m.Use(WingMat());
                    var o = new[]
                    {
                        new Vector3(0.12f, 0.33f, 0.2f), new Vector3(0.45f, 0.42f, 0f), new Vector3(0.85f, 0.46f, -0.45f), new Vector3(0.95f, 0.45f, -0.62f),
                        new Vector3(0.8f, 0.44f, -0.6f), new Vector3(0.78f, 0.43f, -0.75f), new Vector3(0.55f, 0.4f, -0.55f), new Vector3(0.12f, 0.32f, -0.05f),
                    };
                    var cols = new Color[o.Length];
                    for (var k = 0; k < o.Length; k++) cols[k] = new Color(0.55f, 0.45f, 0.35f, 0.5f);
                    m.Flat(o, Vector3.Up, cols, new Color(0.5f, 0.4f, 0.3f, 0.6f));
                    m.Use(ch);
                    var vein = bodyC.Darkened(0.3f);
                    m.Tube(new[] { new Vector3(0.12f, 0.34f, 0.18f), new Vector3(0.5f, 0.43f, -0.05f), new Vector3(0.92f, 0.46f, -0.58f) }, new[] { 0.03f, 0.02f, 0.01f }, 3, vein, Vector3.Up);
                    m.Tube(new[] { new Vector3(0.14f, 0.33f, 0.05f), new Vector3(0.5f, 0.41f, -0.3f), new Vector3(0.75f, 0.43f, -0.7f) }, new[] { 0.025f, 0.018f, 0.008f }, 3, vein, Vector3.Up);
                    m.Mirror = false;
                }
            }
        });

        // Gastre : gros segments annelés (tergites qui se chevauchent), pointe recourbée vers le bas.
        _gaster = new Node3D { Position = new Vector3(0, 0.05f, -1.0f) };
        _bodyPivot.AddChild(_gaster);
        var gl = queen ? 2.7f : 1.95f;
        var gw = queen ? 1.25f : 1f;
        Part(_gaster, "gaster", m => BuildGaster(m, gl, gw, gasterC, membrane, acid, queen));

        // Tête : capsule aplatie, yeux composés, carènes, clypéus ; mandibules et antennes coudées.
        _head = new Node3D { Position = new Vector3(0, 0.15f, 0.92f), Scale = Vector3.One * 1.1f };
        _bodyPivot.AddChild(_head);
        Part(_head, "head", m =>
        {
            m.Use(ch);
            BodyLoft(m, -0.02f, 0.92f,
                new[] { 0f, 0.36f, 0.47f, 0.5f, 0.47f, 0.4f, 0.3f, 0.18f, 0f },
                new[] { 0f, 0.25f, 0.34f, 0.35f, 0.32f, 0.25f, 0.15f, 0.06f, -0.02f },
                new[] { 0f, -0.18f, -0.26f, -0.28f, -0.27f, -0.24f, -0.2f, -0.14f, -0.06f },
                Steps(14), 14, bodyC, 2.6f);
            foreach (var s in new[] { 1f, -1f })
                m.Ellipsoid(new Vector3(0.17f * s, 0.2f, 0.7f), new Vector3(0.09f, 0.06f, 0.1f), 4, 6, bodyC);
            m.Ellipsoid(new Vector3(0, -0.06f, 0.84f), new Vector3(0.22f, 0.09f, 0.1f), 5, 8, bodyC.Darkened(0.15f));
            m.Use(Eye());
            var eyeC = new Color(0.03f, 0.03f, 0.035f);
            foreach (var s in new[] { 1f, -1f })
                m.Ellipsoid(new Vector3(0.43f * s, 0.08f, 0.4f), new Vector3(0.1f, 0.15f, 0.19f), 6, 8, eyeC);
            if (queen)
                for (var k = 0; k < 3; k++)
                    m.Ellipsoid(new Vector3((k - 1) * 0.08f, 0.33f, 0.45f + (k == 1 ? 0.07f : 0)), Vector3.One * 0.035f, 3, 5, eyeC);
        });
        _mandL = new Node3D { Position = new Vector3(0.24f, -0.1f, 0.8f) };
        _mandR = new Node3D { Position = new Vector3(-0.24f, -0.1f, 0.8f) };
        _head.AddChild(_mandL);
        _head.AddChild(_mandR);
        foreach (var (mand, s) in new[] { (_mandL, 1f), (_mandR, -1f) })
            Part(mand, s > 0 ? "mandR" : "mandL", m => { m.Mirror = s < 0; BuildMandible(m, bodyC.Darkened(0.25f)); }, true, 70f);
        foreach (var s in new[] { 1f, -1f })
        {
            var a = new Node3D { Position = new Vector3(0.17f * s, 0.22f, 0.72f) };
            _head.AddChild(a);
            Part(a, s > 0 ? "antR" : "antL", m => { m.Mirror = s < 0; BuildAntenna(m, bodyC, legC.Lightened(0.06f)); }, false, 70f);
            _antennae.Add(a);
        }

        // Pattes : avant tournées vers l'avant, arrière vers l'arrière ; démarche en trépied.
        var fwd = new[] { 40f, -2f, -40f };
        var lfs = new[] { 0.95f, 1.0f, 1.18f };
        var lts = new[] { 0.95f, 1.05f, 1.25f };
        var lss = new[] { 0.62f, 0.68f, 0.82f };
        var reach = new[] { 1.15f, 1.25f, 1.45f };
        for (var i = 0; i < 3; i++)
            foreach (var side in new[] { 1f, -1f })
            {
                var ls = lss[i] * legScale;
                var leg = AddLeg($"leg{i}", new Vector3(0.3f * side, -0.36f, zc[i] + dzc[i]), side, fwd[i], lfs[i] * legScale, lts[i] * legScale, ls,
                    reach[i] * legScale, 22f, AntFemur(legC, th), AntTibia(legC, th, ls), 18f, 0.35f * legScale);
                leg.Phase = (i % 2 == 0 ? 0 : Mathf.Pi) + (side > 0 ? 0 : Mathf.Pi);
            }
    }

    private static void BuildGaster(InsectMesh m, float len, float w, Color col, Color membrane, bool acid, bool queen)
    {
        var edges = new[] { 0.26f, 0.46f, 0.64f, 0.79f, 0.9f };
        var gap = queen ? 0.13f : 0.09f;
        float Dip(float t)
        {
            var d = 0f;
            foreach (var e in edges)
                if (t >= e && t < e + gap) d = Mathf.Max(d, 1f - (t - e) / gap);
            return d;
        }
        var width = new[] { 0f, 0.26f, 0.54f, 0.66f, 0.69f, 0.67f, 0.62f, 0.54f, 0.42f, 0.26f, 0f };
        var top = new[] { 0f, 0.24f, 0.52f, 0.66f, 0.72f, 0.7f, 0.64f, 0.54f, 0.38f, 0.18f, -0.1f };
        var bot = new[] { 0f, -0.16f, -0.3f, -0.38f, -0.41f, -0.41f, -0.39f, -0.35f, -0.3f, -0.24f, -0.16f };
        var extra = new List<float>();
        foreach (var e in edges) { extra.Add(e - 0.01f); extra.Add(e + 0.004f); }
        var ts = Steps(18, extra.ToArray());
        var h = queen ? 1.2f : 1f;
        top = Scaled(top, h);
        bot = Scaled(bot, h);
        m.Use(Chitin());
        BodyLoft(m, 0.08f, -len, width, top, bot, ts, 14, col, 2.15f,
            t => (1f - 0.06f * Dip(t)) * w,
            (t, th, p, c) =>
            {
                var d = Dip(t);
                var k = queen ? Mathf.Pow(d, 1.5f) * 0.8f : d * d * d;
                // Rebord postérieur de chaque tergite un peu plus clair (reflet).
                var rim = 0f;
                foreach (var e in edges) if (t < e && t > e - 0.03f) rim = 0.12f;
                return c.Lightened(rim).Lerp(membrane, k * 0.85f);
            });
        // Soies autour de l'acidopore.
        for (var i = 0; i < 8; i++)
        {
            var a = Mathf.Tau * i / 8;
            var b = new Vector3(Mathf.Cos(a) * 0.12f * w, -0.13f * h + Mathf.Sin(a) * 0.1f, -len * 0.93f);
            m.Cone(b, b + new Vector3(Mathf.Cos(a) * 0.08f, Mathf.Sin(a) * 0.06f, -0.12f), 0.012f, 3, col);
        }
        if (acid)
        {
            // Glande à acide luminescente et membranes intersegmentaires qui brillent entre les plaques.
            m.Use(Mats.Glow(new Color(0.75f, 0.95f, 0.15f), 1.2f));
            m.Ellipsoid(new Vector3(0, -0.3f, -len * 0.72f), new Vector3(0.28f, 0.17f, 0.36f), 6, 10, Colors.White);
            foreach (var e in edges)
            {
                var rings = new List<InsectMesh.Ring>();
                foreach (var t in new[] { e + 0.001f, e + 0.018f })
                {
                    var tp = InsectMesh.Spline(top, t);
                    var bt = InsectMesh.Spline(bot, t);
                    rings.Add(new InsectMesh.Ring(new Vector3(0, (tp + bt) / 2, Mathf.Lerp(0.08f, -len, t)), Vector3.Right, Vector3.Up,
                        InsectMesh.Spline(width, t) * w * 0.975f, (tp - bt) / 2 * w * 0.975f, Colors.White));
                }
                m.Loft(rings, 14, 2.15f);
            }
        }
    }

    private static float[] Scaled(float[] k, float s)
    {
        var r = new float[k.Length];
        for (var i = 0; i < k.Length; i++) r[i] = k[i] * s;
        return r;
    }

    private static void BuildMandible(InsectMesh m, Color col)
    {
        m.Use(Chitin());
        // Mandibule en lame courbe, inclinée vers le bas.
        m.Xf = new Transform3D(new Basis(Vector3.Right, 0.4f), Vector3.Zero);
        var pts = new[] { Vector3.Zero, new Vector3(0.07f, -0.01f, 0.24f), new Vector3(0.04f, -0.03f, 0.48f), new Vector3(-0.09f, -0.04f, 0.66f), new Vector3(-0.27f, -0.05f, 0.74f) };
        m.Tube(pts, new[] { 0.12f, 0.11f, 0.09f, 0.06f, 0f }, 6, col, Vector3.Up, 0.55f);
        // Dents sur le bord interne.
        var dark = col.Darkened(0.4f);
        for (var k = 1; k < 4; k++)
            foreach (var f in new[] { 0.3f, 0.8f })
            {
                var b = pts[k].Lerp(pts[k + 1], f);
                var tg = (pts[k + 1] - pts[k]).Normalized();
                var inward = tg.Cross(Vector3.Up).Normalized();
                m.Cone(b, b + inward * 0.13f + tg * 0.04f, 0.035f, 3, dark);
            }
    }

    private static void BuildAntenna(InsectMesh m, Color col, Color tip)
    {
        m.Use(Chitin());
        // Scape (long article basal) vers le haut et l'arrière, puis coude et funicule en chapelet terminé en massue.
        var elbow = new Vector3(0.42f, 0.56f, -0.14f);
        m.Segment(Vector3.Zero, elbow, 0.04f, 0.055f, 5, col, Vector3.Back, mid: 1);
        var d0 = new Vector3(0.4f, 0.15f, 1f).Normalized();
        var pts = new List<Vector3> { elbow };
        var rad = new List<float> { 0 };
        var cols = new List<Color> { col };
        var p = elbow;
        for (var k = 0; k < 9; k++)
        {
            var d = (d0 + new Vector3(0, -0.09f * k, 0)).Normalized();
            var club = k >= 6 ? 1.45f : 1f;
            var l = 0.11f * (k >= 6 ? 1.1f : 1f);
            var c = col.Lerp(tip, k / 8f);
            pts.Add(p + d * l * 0.1f); rad.Add(0.026f * club); cols.Add(c);
            pts.Add(p + d * l * 0.55f); rad.Add(0.036f * club); cols.Add(c);
            pts.Add(p + d * l); rad.Add(0.028f * club); cols.Add(c);
            p += d * l;
        }
        pts.Add(p + d0 * 0.02f); rad.Add(0); cols.Add(tip);
        m.Tube(pts, rad, 4, col, Vector3.Up, 1f, cols);
    }
}
