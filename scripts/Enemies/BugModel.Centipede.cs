using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Mille-pattes géant : tête à forcipules, chaîne de segments qui suivent le trajet de la tête
/// (trace des positions passées, calculée localement chez chaque pair à partir de la position affichée),
/// une paire de pattes par segment animée en vague métachronale.
/// </summary>
public partial class BugModel
{
    public const int CentiSegments = 12;
    private const float CentiSpacing = 0.72f;   // écart entre deux segments (unités du modèle)
    private const float CentiPivot = 0.55f;     // hauteur du centre des segments au repos
    private const float CentiTrailStep = 0.2f;  // pas d'échantillonnage de la trace (m)

    private static readonly Color CentiBody = new(0.52f, 0.18f, 0.05f);
    private static readonly Color CentiHead = new(0.3f, 0.08f, 0.035f);
    private static readonly Color CentiBand = new(0.08f, 0.035f, 0.025f);
    private static readonly Color CentiLeg = new(0.82f, 0.56f, 0.14f);
    private static readonly Color CentiBelly = new(0.6f, 0.4f, 0.16f);

    private class CentiSeg
    {
        public Node3D Node, Legs;
        public float Scale, Dist;
    }

    private readonly List<CentiSeg> _segs = new();
    /// <summary>Positions passées de la tête (monde), la plus récente en premier.</summary>
    private readonly List<Vector3> _trail = new();
    private Vector3 _trailBack = Vector3.Forward;

    public int CentipedeSegmentCount => _segs.Count;

    /// <summary>Centre du segment i dans le repère du modèle.</summary>
    public Vector3 CentipedeSegmentLocal(int i) => _segs[i].Node.Position;

    public float CentipedeSegmentScale(int i) => _segs[i].Scale;

    private void BuildCentipede()
    {
        Scale = Vector3.One * 1.25f;
        _pivotY = CentiPivot;

        // Segments : le premier (cou) un peu plus étroit, les cinq derniers s'effilent vers la queue.
        float dist = 0.5f, prevSc = 0f;
        for (var k = 0; k < CentiSegments; k++)
        {
            var sc = k == 0 ? 0.93f : Mathf.Lerp(1f, 0.68f, Mathf.Clamp((k - 6f) / 5f, 0f, 1f));
            if (k > 0) dist += CentiSpacing * (sc + prevSc) / 2f;
            prevSc = sc;
            var tail = k == CentiSegments - 1;
            var node = new Node3D { Position = new Vector3(0, CentiPivot * sc, -dist), Scale = Vector3.One * sc };
            AddChild(node);
            Part(node, tail ? "tail" : "seg", m => BuildCentiSegment(m, tail));
            Node3D legs = null;
            if (!tail)
            {
                // Les deux pattes d'un segment forment une seule pièce : le lacet les fait alterner.
                legs = new Node3D();
                node.AddChild(legs);
                Part(legs, "legs", BuildCentiLegs, false, 90f);
            }
            _segs.Add(new CentiSeg { Node = node, Legs = legs, Scale = sc, Dist = dist });
        }

        // Tête : bouclier céphalique aplati, ocelles, pièces buccales ; forcipules et antennes articulées.
        _head = new Node3D { Position = new Vector3(0, CentiPivot, 0) };
        AddChild(_head);
        Part(_head, "head", BuildCentiHead);
        _mandL = new Node3D { Position = new Vector3(0.2f, -0.13f, 0.36f) };
        _mandR = new Node3D { Position = new Vector3(-0.2f, -0.13f, 0.36f) };
        _head.AddChild(_mandL);
        _head.AddChild(_mandR);
        foreach (var (mand, s) in new[] { (_mandL, 1f), (_mandR, -1f) })
            Part(mand, s > 0 ? "forcR" : "forcL", m => { m.Mirror = s < 0; BuildForcipule(m); }, true, 70f);
        foreach (var s in new[] { 1f, -1f })
        {
            var a = new Node3D { Position = new Vector3(0.13f * s, 0.06f, 0.6f) };
            _head.AddChild(a);
            Part(a, s > 0 ? "antR" : "antL", m => { m.Mirror = s < 0; BuildCentiAntenna(m); }, false, 70f);
            _antennae.Add(a);
        }
    }

    private static void BuildCentiSegment(InsectMesh m, bool tail)
    {
        m.Use(Chitin());
        // Tergite large et plat (superellipse carrée) : l'avant étroit glisse sous l'arrière du segment précédent.
        var width = tail
            ? new[] { 0f, 0.4f, 0.46f, 0.48f, 0.47f, 0.43f, 0.35f, 0.22f, 0f }
            : new[] { 0f, 0.42f, 0.48f, 0.53f, 0.56f, 0.59f, 0.6f, 0.6f, 0f };
        var top = tail
            ? new[] { 0.03f, 0.1f, 0.13f, 0.15f, 0.15f, 0.14f, 0.12f, 0.08f, 0.02f }
            : new[] { 0.03f, 0.1f, 0.13f, 0.15f, 0.17f, 0.19f, 0.2f, 0.2f, 0.12f };
        var bot = tail
            ? new[] { -0.03f, -0.09f, -0.11f, -0.12f, -0.12f, -0.11f, -0.1f, -0.07f, -0.03f }
            : new[] { -0.03f, -0.09f, -0.11f, -0.12f, -0.13f, -0.14f, -0.14f, -0.13f, -0.06f };
        var ts = Steps(16, 0.11f, 0.125f, 0.615f, 0.625f, 0.675f, 0.685f, 0.915f, 0.925f, 0.97f, 0.985f);
        BodyLoft(m, 0.6f, -0.46f, width, top, bot, ts, 18, CentiBody, 4f, null, (t, th, p, c) =>
        {
            var up = Mathf.Sin(th);
            if (up < -0.4f) return CentiBelly.Darkened(t > 0.68f ? 0.3f : 0.05f); // sternite
            var col = c;
            if (t < 0.12f) col = c.Darkened(0.4f);                           // membrane cachée sous le segment précédent
            else if (t > 0.92f) col = CentiBand.Lightened(0.12f);            // rebord qui accroche la lumière
            else if (t > 0.68f) col = CentiBand;                             // bande sombre postérieure
            else if (t > 0.62f) col = c.Lerp(CentiBand, 0.5f);
            if (up > 0.5f && Mathf.Abs(Mathf.Abs(p.X) - 0.17f) < 0.03f) col = col.Darkened(0.3f); // sutures paramédianes
            if (Mathf.Abs(up) < 0.4f) col = col.Lerp(CentiBelly, 0.3f);      // flancs
            return col;
        });
        // Membrane intersegmentaire : relie au segment précédent même quand le corps se courbe.
        m.Ellipsoid(new Vector3(0, 0.01f, 0.55f), new Vector3(0.4f, 0.1f, 0.34f), 5, 12, CentiBand.Lerp(CentiBelly, 0.25f));
        // Pleures renflées où s'attachent les hanches.
        foreach (var s in new[] { 1f, -1f })
            m.Ellipsoid(new Vector3(0.42f * s, -0.07f, -0.02f), new Vector3(0.13f, 0.1f, 0.26f), 4, 7, CentiBelly.Darkened(0.15f));
        if (!tail) return;

        // Dernier segment : pattes terminales allongées vers l'arrière, épineuses, qui ne marchent pas.
        var legC = CentiLeg.Darkened(0.1f);
        foreach (var s in new[] { 1f, -1f })
        {
            m.Mirror = s < 0;
            var pts = new[]
            {
                new Vector3(0.28f, -0.06f, -0.22f), new Vector3(0.6f, -0.08f, -0.58f), new Vector3(0.78f, -0.12f, -1.12f),
                new Vector3(0.88f, -0.2f, -1.58f), new Vector3(0.93f, -0.3f, -1.95f),
            };
            var rad = new[] { 0.11f, 0.09f, 0.07f, 0.05f, 0.035f };
            for (var i = 0; i < pts.Length - 1; i++)
                m.Segment(pts[i], pts[i + 1], rad[i], rad[i + 1], 6, legC, Vector3.Up, 0.25f, 1, null,
                    t => t > 0.85f ? legC.Darkened(0.35f) : legC);
            for (var i = 0; i < 6; i++)
            {
                var b = pts[1].Lerp(pts[2], i / 6f);
                m.Cone(b + new Vector3(-0.05f, 0, 0), b + new Vector3(-0.13f, -0.04f, -0.08f), 0.02f, 3, CentiBand);
            }
            var e = pts[^1];
            m.Hook(new[] { e, e + new Vector3(0.01f, -0.04f, -0.08f), e + new Vector3(0f, -0.12f, -0.12f) }, 0.03f, 3, CentiBand, Vector3.Up);
        }
        m.Mirror = false;
    }

    private static void BuildCentiLegs(InsectMesh m)
    {
        m.Use(Chitin());
        var dark = CentiLeg.Darkened(0.45f);
        foreach (var s in new[] { 1f, -1f })
        {
            m.Mirror = s < 0;
            // Hanche, préfémur et fémur vers le haut, tibia et tarse vers le sol, griffe : pied à 3 cm du sol.
            var pts = new[]
            {
                new Vector3(0.4f, -0.08f, 0.02f), new Vector3(0.62f, -0.1f, 0f), new Vector3(0.93f, 0.06f, -0.08f),
                new Vector3(1.18f, -0.22f, -0.16f), new Vector3(1.3f, -0.46f, -0.22f),
            };
            var rad = new[] { 0.095f, 0.078f, 0.062f, 0.05f, 0.032f };
            for (var i = 0; i < pts.Length - 1; i++)
            {
                var c = i == 0 ? CentiLeg.Darkened(0.2f) : CentiLeg;
                m.Segment(pts[i], pts[i + 1], rad[i], rad[i + 1], 5, c, Vector3.Up, i == 0 ? 0f : 0.3f, 1, null,
                    t => t > 0.86f ? c.Lerp(dark, 0.6f) : c);
            }
            m.Cone(new Vector3(0.82f, 0.04f, -0.06f), new Vector3(0.86f, 0.16f, -0.16f), 0.018f, 3, dark);
            var f = pts[^1];
            m.Hook(new[] { f, f + new Vector3(0.02f, -0.04f, -0.02f), f + new Vector3(0.03f, -0.07f, -0.07f) }, 0.025f, 3, CentiBand, Vector3.Up);
        }
        m.Mirror = false;
    }

    private static void BuildCentiHead(InsectMesh m)
    {
        m.Use(Chitin());
        BodyLoft(m, -0.28f, 0.66f,
            new[] { 0f, 0.4f, 0.47f, 0.5f, 0.49f, 0.45f, 0.37f, 0.22f, 0f },
            new[] { 0.02f, 0.13f, 0.17f, 0.19f, 0.18f, 0.16f, 0.12f, 0.06f, 0f },
            new[] { -0.02f, -0.11f, -0.14f, -0.15f, -0.15f, -0.13f, -0.1f, -0.06f, -0.02f },
            Steps(14), 16, CentiHead, 2.8f, null, (t, th, p, c) =>
            {
                var up = Mathf.Sin(th);
                if (up < -0.4f) return CentiBelly.Darkened(0.25f);
                if (up > 0.5f && Mathf.Abs(p.X) < 0.025f) return c.Darkened(0.45f); // sillon médian
                return t > 0.8f ? c.Darkened(0.3f) : c;
            });
        // Coxosternite des forcipules sous la tête, pièces buccales.
        m.Ellipsoid(new Vector3(0, -0.13f, 0.2f), new Vector3(0.4f, 0.09f, 0.3f), 5, 10, CentiBelly.Darkened(0.2f));
        foreach (var s in new[] { 1f, -1f })
        {
            m.Ellipsoid(new Vector3(0.09f * s, -0.12f, 0.6f), new Vector3(0.08f, 0.05f, 0.1f), 4, 6, CentiLeg.Darkened(0.35f));
            m.Cone(new Vector3(0.1f * s, -0.13f, 0.66f), new Vector3(0.06f * s, -0.2f, 0.78f), 0.03f, 3, CentiBand);
        }
        // Ocelles : quatre yeux simples groupés de chaque côté.
        m.Use(Eye());
        var eyeC = new Color(0.02f, 0.02f, 0.025f);
        foreach (var s in new[] { 1f, -1f })
            for (var i = 0; i < 4; i++)
            {
                var o = new Vector3((0.3f + (i % 2) * 0.05f) * s, 0.09f + (i / 2) * 0.03f, 0.4f + (i % 2) * 0.07f - (i / 2) * 0.05f);
                m.Ellipsoid(o, Vector3.One * 0.045f, 4, 6, eyeC);
            }
    }

    private static void BuildForcipule(InsectMesh m)
    {
        m.Use(Chitin());
        // Crochet à venin courbé vers l'intérieur : base rouille, pointe noire, dent interne.
        var pts = new[] { Vector3.Zero, new Vector3(0.1f, -0.02f, 0.18f), new Vector3(0.1f, -0.04f, 0.4f), new Vector3(0.0f, -0.05f, 0.56f), new Vector3(-0.16f, -0.06f, 0.64f) };
        var cols = new[] { CentiBody, CentiBody, CentiBody.Lerp(CentiBand, 0.4f), CentiBand, CentiBand };
        m.Tube(pts, new[] { 0.11f, 0.1f, 0.075f, 0.045f, 0f }, 6, CentiBody, Vector3.Up, 0.7f, cols);
        m.Cone(new Vector3(0.04f, -0.02f, 0.2f), new Vector3(-0.08f, -0.03f, 0.27f), 0.04f, 3, CentiBand);
    }

    private static void BuildCentiAntenna(InsectMesh m)
    {
        m.Use(Chitin());
        // Longue antenne en chapelet d'articles, vers l'avant puis vers l'extérieur.
        var pts = new List<Vector3> { Vector3.Zero };
        var rad = new List<float> { 0f };
        var cols = new List<Color> { CentiHead };
        var p = Vector3.Zero;
        for (var k = 0; k < 16; k++)
        {
            var d = new Vector3(0.18f + 0.05f * k, 0.22f - 0.035f * k, 1f).Normalized();
            var l = 0.1f;
            var r = Mathf.Lerp(0.045f, 0.014f, k / 15f);
            var c = CentiHead.Lerp(CentiLeg, Mathf.Min(1f, k / 10f));
            pts.Add(p + d * l * 0.1f); rad.Add(r); cols.Add(c);
            pts.Add(p + d * l * 0.6f); rad.Add(r * 1.15f); cols.Add(c);
            pts.Add(p + d * l); rad.Add(r * 0.8f); cols.Add(c);
            p += d * l;
        }
        pts.Add(p + Vector3.Back * 0.02f); rad.Add(0); cols.Add(CentiLeg);
        m.Tube(pts, rad, 4, CentiHead, Vector3.Up, 1f, cols);
    }

    // ---------------- Animation ----------------

    /// <summary>Repose la trace en ligne (ou en S pour la vitrine) derrière la tête.</summary>
    public void SeedCentipedeTrail(float bend)
    {
        var host = GetParentOrNull<Node3D>() ?? this;
        SeedTrail(host.GlobalPosition, Forward(host), bend);
    }

    private static Vector3 Forward(Node3D host)
    {
        var f = host.GlobalBasis.Z;
        f.Y = 0;
        return f.LengthSquared() > 1e-4f ? f.Normalized() : Vector3.Back;
    }

    private float TrailLength => (_segs[^1].Dist + 1.5f) * Mathf.Max(Scale.X, 0.01f);

    private void SeedTrail(Vector3 head, Vector3 fwd, float bend)
    {
        _trail.Clear();
        var p = head;
        var dir = -fwd;
        var n = Mathf.CeilToInt(TrailLength / CentiTrailStep) + 2;
        for (var i = 0; i < n; i++)
        {
            p += dir * CentiTrailStep;
            _trail.Add(p);
            dir = dir.Rotated(Vector3.Up, bend * Mathf.Sin(i * 0.12f) * CentiTrailStep);
        }
        _trailBack = dir;
    }

    /// <summary>Point de la trace à une distance donnée derrière la tête (prolongée en ligne droite au-delà).</summary>
    private Vector3 TrailPoint(Vector3 head, float dist)
    {
        var prev = head;
        var acc = 0f;
        var dir = _trailBack;
        foreach (var p in _trail)
        {
            var len = prev.DistanceTo(p);
            if (len > 1e-4f)
            {
                dir = (p - prev) / len;
                if (acc + len >= dist) return prev + dir * (dist - acc);
                acc += len;
            }
            prev = p;
        }
        return prev + dir * (dist - acc);
    }

    private void AnimateCentipede(float dt, float speed, float attack)
    {
        var host = GetParentOrNull<Node3D>() ?? this;
        var s = Mathf.Max(Scale.X, 0.01f);
        var head = host.GlobalPosition;

        // Trace : un point tous les 20 cm ; un saut (téléportation, recalage réseau) la remet à plat.
        if (_trail.Count == 0 || head.DistanceSquaredTo(_trail[0]) > 64f) SeedTrail(head, Forward(host), 0f);
        else if (head.DistanceTo(_trail[0]) >= CentiTrailStep)
        {
            _trail.Insert(0, head);
            var need = TrailLength;
            var acc = 0f;
            for (var i = 1; i < _trail.Count; i++)
            {
                acc += _trail[i - 1].DistanceTo(_trail[i]);
                if (acc <= need) continue;
                if (i + 1 < _trail.Count)
                {
                    _trailBack = (_trail[i] - _trail[i - 1]).Normalized();
                    _trail.RemoveRange(i + 1, _trail.Count - i - 1);
                }
                break;
            }
        }

        var amp = Mathf.Clamp(speed / 1.5f, 0f, 1f);
        _gait += dt * speed * 3f / s;
        var inv = host.GlobalTransform.AffineInverse();
        var h = 0.35f * s;
        for (var k = 0; k < _segs.Count; k++)
        {
            var seg = _segs[k];
            var d = seg.Dist * s;
            var p = TrailPoint(head, d);
            var dir = TrailPoint(head, Mathf.Max(d - h, 0f)) - TrailPoint(head, d + h);
            var lp = inv * p;
            var ld = inv.Basis * dir;
            // La trace suit les surfaces (sol, mur, toit) : chaque segment prend la pente de la trace dans le repère de la tête.
            var horiz = new Vector2(ld.X, ld.Z).Length();
            var yaw = horiz > 0.3f * ld.Length() ? Mathf.Atan2(ld.X, ld.Z) : 0f;
            var pitch = ld.LengthSquared() > 1e-8f ? -Mathf.Atan2(ld.Y, yaw == 0f ? Mathf.Abs(ld.Z) * Mathf.Sign(ld.Z + 1e-6f) : horiz) : 0f;
            // Vague de pas qui remonte vers la queue, légère ondulation latérale, l'avant se cabre pour mordre.
            var ph = _gait - k * 0.85f;
            var rear = k < 3 ? attack * 0.3f * (1f - k / 3f) : 0f;
            var lat = Mathf.Sin(_gait * 0.22f - k * 0.7f) * 0.05f * amp;
            var rot = new Vector3(-rear * 0.5f + pitch, yaw, Mathf.Sin(ph) * 0.03f * amp);
            var sb = Basis.FromEuler(rot);
            seg.Node.Position = lp / s + sb.Y * (CentiPivot * seg.Scale + Mathf.Sin(ph * 2f) * 0.012f * amp + rear) + sb.X * lat;
            seg.Node.Rotation = rot;
            if (seg.Legs != null) seg.Legs.Rotation = new Vector3(0, Mathf.Sin(ph) * 0.3f * amp, -Mathf.Cos(ph) * 0.08f * amp);
        }

        _head.Position = new Vector3(0, CentiPivot + attack * 0.4f + Mathf.Sin(_t * 2.3f) * 0.015f, attack * 0.15f);
        _head.Rotation = new Vector3(-attack * 0.3f + Mathf.Sin(_t * 2f) * 0.03f, Mathf.Sin(_t * 1.3f) * 0.08f, 0);
        var open = 8f + Mathf.Sin(_t * 3f) * 4f + attack * 45f * Mathf.Abs(Mathf.Sin(_t * 9f));
        _mandL.RotationDegrees = new Vector3(0, open, 0);
        _mandR.RotationDegrees = new Vector3(0, -open, 0);
        for (var i = 0; i < _antennae.Count; i++)
        {
            var sd = i == 0 ? 1f : -1f;
            _antennae[i].Rotation = new Vector3(Mathf.Sin(_t * 2.6f + i * 1.7f) * 0.15f - attack * 0.3f, Mathf.Sin(_t * 1.8f + i) * 0.2f * sd, 0);
        }
    }

    /// <summary>Mort : segments retournés en alternance, ventre et pattes en l'air.</summary>
    private void CentipedeDeathPose()
    {
        for (var k = 0; k < _segs.Count; k++)
        {
            var seg = _segs[k];
            var roll = Mathf.Pi * (0.82f + 0.12f * Mathf.Sin(k * 1.3f));
            seg.Node.Rotation = new Vector3(0, seg.Node.Rotation.Y, roll);
            seg.Node.Position = new Vector3(seg.Node.Position.X, 0.24f * seg.Scale, seg.Node.Position.Z);
            if (seg.Legs != null) seg.Legs.Rotation = new Vector3(0, 0.25f * Mathf.Sin(k * 2.1f), 0);
        }
        _head.Position = new Vector3(0, 0.25f, 0);
        _head.Rotation = new Vector3(0, _head.Rotation.Y, Mathf.Pi * 0.85f);
        _mandL.RotationDegrees = new Vector3(0, 35, 0);
        _mandR.RotationDegrees = new Vector3(0, -35, 0);
    }
}
