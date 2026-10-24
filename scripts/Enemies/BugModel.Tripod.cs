using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Tripode : coque blindée en carapace (≈ 11 m) perchée à ~16 m sur trois longues pattes dont les genoux remontent
/// au-dessus de la coque (≈ 21 m de haut). Émetteur du rayon de destruction à l'avant (lacet et tangage), noyau
/// incandescent sous le ventre dans une cage de côtes (point faible), deux tentacules mécaniques pendants.
/// Démarche à trois temps (une patte à la fois, deux toujours au sol) par cinématique inverse.
/// </summary>
public partial class BugModel
{
    public const float TBodyY = 16.5f;
    private const float TLf = 12f, TLt = 18.5f, TFootR = 13f, TAnkle = 1.3f, TStride = 6f, TStance = 0.72f, THipR = 3.8f, THipY = -1.2f;
    /// <summary>Noyau (point faible) dans le repère de la coque.</summary>
    public static readonly Vector3 TCorePos = new(0, -3.05f, -0.2f);
    public const float TCoreRadius = 1.75f;
    /// <summary>Directions des pattes (avant gauche, avant droite, arrière), angle depuis l'avant.</summary>
    private static readonly float[] TLegAngles = { -Mathf.Pi / 3f, Mathf.Pi / 3f, Mathf.Pi };
    private static readonly Vector3 TEmitterPos = new(0, -0.7f, 4.7f);

    private Node3D _tEmitter, _tBeam, _tImpact;
    private MeshInstance3D _tCore, _tLens, _tEyes;
    private OmniLight3D _tImpactLight;
    private readonly List<Node3D> _tFeet = new();
    private readonly List<Node3D[]> _tTendrils = new();
    private float _tGait, _tAmp, _tCharge = -1f, _tFire;
    private Vector3 _tBeamPoint;
    private readonly float[] _tPrevU = new float[3];

    public Vector3 TripodCore => _tCore.GlobalPosition;
    public Vector3 TripodEmitter => _tEmitter.GlobalTransform * new Vector3(0, 0, 2.85f);
    public Vector3 TripodHull => _bodyPivot.GlobalPosition;
    /// <summary>Milieu du fémur et du tibia de la patte i (formes de collision).</summary>
    public Transform3D TripodFemur(int i) => _legs[i].Femur.GlobalTransform * new Transform3D(new Basis(Vector3.Back, Mathf.Pi / 2), new Vector3(TLf * 0.5f, 0, 0));
    public Transform3D TripodTibia(int i) => _legs[i].Knee.GlobalTransform * new Transform3D(new Basis(Vector3.Back, Mathf.Pi / 2), new Vector3(TLt * 0.5f, 0, 0));

    /// <summary>Charge du rayon (0 → 1 en 1,8 s : lentille et noyau s'embrasent).</summary>
    public void TripodCharge() => _tCharge = 0f;

    /// <summary>Rayon allumé (point d'impact monde) ou éteint (null).</summary>
    public void TripodBeam(Vector3? point)
    {
        if (point is { } p)
        {
            _tBeamPoint = p;
            _tFire = 1f;
            _tCharge = -1f;
        }
        else _tFire = 0f;
    }

    // ---------------- Construction ----------------

    private void BuildTripod()
    {
        _pivotY = TBodyY;
        _bodyPivot = new Node3D { Position = new Vector3(0, TBodyY, 0) };
        AddChild(_bodyPivot);

        Part(_bodyPivot, "hull", BuildTripodHull);

        // Noyau : sphère incandescente qui pulse (s'emballe pendant la charge).
        _tCore = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = TCoreRadius * 0.9f, Height = TCoreRadius * 1.8f, RadialSegments = 20, Rings = 10 },
            MaterialOverride = Mats.Glow(new Color(1f, 0.5f, 0.2f), 3.5f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = TCorePos,
        };
        _bodyPivot.AddChild(_tCore);
        _bodyPivot.AddChild(new OmniLight3D { Position = TCorePos + Vector3.Down * 1.6f, LightColor = new Color(1f, 0.4f, 0.12f), LightEnergy = 2.2f, OmniRange = 16f });

        // Émetteur du rayon : capot orientable, lentille incandescente.
        _tEmitter = new Node3D { Position = TEmitterPos };
        _bodyPivot.AddChild(_tEmitter);
        Part(_tEmitter, "emitter", m =>
        {
            m.Use(MachArmor());
            Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 0.15f, 0.9f)), new Vector3(1.35f, 1.2f, 1.45f), MGun, 0.18f, 6f, 0.85f, 0.8f);
            for (var s = -1; s <= 1; s += 2)
                PlateAt(m, new Vector3(1.35f * s, 0.1f, 1.3f), new Vector3(0.14f, 0.9f, 1.25f), MSteel, 0.1f * s);
            PlateAt(m, new Vector3(0, 1.38f, 1.2f), new Vector3(1.1f, 0.12f, 1.2f), MSteel, 0, 0.08f);
            m.Use(MachMech());
            Cyl(m, new Vector3(0, 0, 1.6f), new Vector3(0, 0, 2.75f), 0.95f, 18, MDark);
            for (var k = 0; k < 3; k++) Cyl(m, new Vector3(0, 0, 1.75f + k * 0.32f), new Vector3(0, 0, 1.87f + k * 0.32f), 1.08f, 18, MSteel);
            m.Torus(new Vector3(0, 0, 2.78f), new Basis(Vector3.Right, Mathf.Pi / 2), 0.8f, 0.14f, 20, 5, MSteel);
            for (var k = 0; k < 4; k++)
            {
                var a = k * Mathf.Tau / 4 + Mathf.Pi / 4;
                m.Box(new Transform3D(new Basis(Vector3.Back, a), new Vector3(Mathf.Cos(a) * 1.05f, Mathf.Sin(a) * 1.05f, 2.3f)), new Vector3(0.32f, 0.06f, 0.45f), MDark);
            }
        });
        _tLens = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.62f, Height = 0.7f, RadialSegments = 16, Rings = 6 },
            MaterialOverride = Mats.Glow(new Color(1f, 0.45f, 0.15f), 3f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 0, 2.72f),
        };
        _tEmitter.AddChild(_tLens);
        _tEyes = Part(_bodyPivot, "eyes", m =>
        {
            m.Use(MachRed());
            for (var s = -1; s <= 1; s += 2)
            for (var k = 0; k < 3; k++)
                m.Ellipsoid(new Vector3(s * (1.15f + k * 0.55f), 0.95f - k * 0.18f, 4.62f - k * 0.42f), new Vector3(0.14f, 0.1f, 0.08f), 3, 8, Colors.White);
        }, false);

        // Pattes.
        for (var i = 0; i < 3; i++) AddTripodLeg(i);

        // Tentacules mécaniques : chaînes de segments qui se balancent.
        for (var s = -1; s <= 1; s += 2)
        {
            var chain = new Node3D[5];
            Node3D parent = _bodyPivot;
            var pos = new Vector3(1.9f * s, -2.0f, 2.6f);
            for (var k = 0; k < chain.Length; k++)
            {
                var n = new Node3D { Position = pos };
                parent.AddChild(n);
                chain[k] = n;
                var len = 1.7f - k * 0.18f;
                var r = 0.32f - k * 0.04f;
                Part(n, $"tendril{k}", m =>
                {
                    m.Use(MachMech());
                    Cyl(m, Vector3.Zero, new Vector3(0, -len, 0), r, 8, MDark, -1f, r * 0.85f);
                    Cyl(m, new Vector3(0, -len * 0.15f, 0), new Vector3(0, -len * 0.35f, 0), r * 1.25f, 8, MSteel);
                    if (k == 4)
                    {
                        for (var c = 0; c < 3; c++)
                        {
                            var a = c * Mathf.Tau / 3;
                            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                            m.Cone(new Vector3(0, -len, 0) + d * 0.1f, new Vector3(0, -len - 0.7f, 0) + d * 0.35f, 0.1f, 5, MDark, true);
                        }
                    }
                }, false, 120f);
                parent = n;
                pos = new Vector3(0, -len, 0);
            }
            _tTendrils.Add(chain);
        }

        // Rayon (repère monde) : cœur blanc, gaine orange, lueur à l'impact.
        _tBeam = new Node3D { TopLevel = true, Visible = false };
        AddChild(_tBeam);
        _tBeam.AddChild(BeamMesh(0.26f, new Color(1f, 0.97f, 0.88f), 8f, false));
        _tBeam.AddChild(BeamMesh(0.7f, new Color(1f, 0.55f, 0.18f, 0.6f), 3f, true));
        _tBeam.AddChild(BeamMesh(1.7f, new Color(1f, 0.28f, 0.05f, 0.3f), 1.2f, true));
        _tImpact = new Node3D { TopLevel = true, Visible = false };
        AddChild(_tImpact);
        _tImpact.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 2.2f, Height = 4.4f, RadialSegments = 14, Rings = 7 },
            MaterialOverride = Mats.Glow(new Color(1f, 0.55f, 0.2f), 5f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        _tImpactLight = new OmniLight3D { LightColor = new Color(1f, 0.45f, 0.15f), LightEnergy = 6f, OmniRange = 22f, Position = Vector3.Up * 2f };
        _tImpact.AddChild(_tImpactLight);
        for (var i = 0; i < 3; i++) _tPrevU[i] = Mathf.PosMod(_legs[i].Phase, 1f);
        AnimateTripod(0f, 0f, 0f);
    }

    /// <summary>Coque : carapace bombée à joints, jupe de blindage, cage du noyau, logements de hanches, arrière moteur.</summary>
    private static void BuildTripodHull(InsectMesh m)
    {
        m.Use(MachArmor());
        var grooves = new[] { 0.195f, 0.2f, 0.205f, 0.395f, 0.4f, 0.405f, 0.595f, 0.6f, 0.605f, 0.795f, 0.8f, 0.805f };
        BodyLoft(m, -5.6f, 5.3f,
            new[] { 2.5f, 4.9f, 5.7f, 6.0f, 5.85f, 5.0f, 3.0f },
            new[] { 1.0f, 2.45f, 3.2f, 3.4f, 3.15f, 2.3f, 0.8f },
            new[] { -0.7f, -2.0f, -2.5f, -2.6f, -2.45f, -1.9f, -0.8f },
            Steps(16, grooves), 28, MGun, 2.5f, null, (t, th, p, c) =>
            {
                foreach (var g in new[] { 0.2f, 0.4f, 0.6f, 0.8f })
                    if (Mathf.Abs(t - g) < 0.006f) return MGroove;
                if (p.Y < -0.9f) return MSteel;
                if (Mathf.Abs(p.Y - 0.15f) < 0.12f) return MGroove;
                if (p.Y > 1.6f && Mathf.Abs(p.X) < 1.6f) return MOchre;
                return c;
            });
        // Plaques dorsales en écailles et crête de capteurs.
        for (var k = -2; k <= 2; k++)
        {
            PlateAt(m, new Vector3(0, 3.38f - Mathf.Abs(k) * 0.12f, k * 1.7f), new Vector3(1.3f, 0.16f, 0.8f), MSteel, 0, 0.08f * k);
            for (var s = -1; s <= 1; s += 2)
                PlateAt(m, new Vector3(3.3f * s, 2.65f - Mathf.Abs(k) * 0.18f, k * 1.75f), new Vector3(1.15f, 0.14f, 0.78f), MGun, 0, 0.06f * k, 0.42f * s);
        }
        // Jupe de blindage à l'équateur.
        for (var k = 0; k < 14; k++)
        {
            var a = k * Mathf.Tau / 14 + 0.1f;
            var d = new Vector3(Mathf.Sin(a) * 5.9f, 0, Mathf.Cos(a) * 5.45f);
            PlateAt(m, d + new Vector3(0, -0.3f, 0), new Vector3(1.05f, 0.75f, 0.14f), MSteel, a, 0, 0, 0.08f);
        }
        // Visière avant au-dessus de l'émetteur.
        PlateAt(m, new Vector3(0, 0.75f, 5.0f), new Vector3(2.4f, 0.18f, 0.9f), MGun, 0, 0.45f, 0, 0.1f, 0.75f);
        // Arrière : bloc moteur, radiateurs, échappements.
        Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 0.6f, -5.4f)), new Vector3(2.3f, 1.25f, 0.8f), MSteel, 0.15f);
        for (var k = 0; k < 9; k++) m.Box(new Transform3D(Basis.Identity, new Vector3(-1.8f + k * 0.45f, 0.6f, -6.22f)), new Vector3(0.06f, 1.0f, 0.12f), MDark);
        m.Use(MachMech());
        for (var s = -1; s <= 1; s += 2)
        {
            Cyl(m, new Vector3(1.4f * s, 1.5f, -5.0f), new Vector3(1.55f * s, 3.3f, -5.8f), 0.38f, 10, MSteel);
            Cyl(m, new Vector3(1.55f * s, 3.2f, -5.76f), new Vector3(1.58f * s, 3.55f, -5.9f), 0.44f, 10, MDark);
        }
        // Mât et antennes.
        Cyl(m, new Vector3(0.8f, 2.6f, -2.5f), new Vector3(0.95f, 5.2f, -3.0f), 0.06f, 6, MDark);
        Cyl(m, new Vector3(-0.6f, 2.7f, -1.2f), new Vector3(-0.7f, 3.9f, -1.4f), 0.05f, 6, MDark);
        // Logements des hanches.
        foreach (var a in TLegAngles)
        {
            var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            var h = d * THipR + new Vector3(0, THipY, 0);
            m.Ellipsoid(h, Vector3.One * 1.9f, 6, 14, MDark);
            m.Torus(h + Vector3.Up * 0.9f, Basis.Identity, 1.9f, 0.28f, 18, 5, MSteel);
            Cyl(m, h + Vector3.Up * 0.2f, h + Vector3.Up * 1.6f, 1.55f, 16, MSteel);
        }
        // Cage du noyau : côtes courbes du ventre à un anneau sous le noyau.
        var ring = TCorePos + Vector3.Down * 1.75f;
        for (var k = 0; k < 6; k++)
        {
            var a = k * Mathf.Tau / 6 + Mathf.Pi / 6;
            var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            var top = d * 2.6f + new Vector3(0, -1.9f, -0.2f);
            var mid = d * 2.25f + TCorePos + Vector3.Down * 0.4f;
            var bot = ring + d * 0.95f;
            m.Tube(new[] { top, mid, bot }, new[] { 0.32f, 0.28f, 0.22f }, 6, MSteel, d, 0.55f, null, 3f);
        }
        m.Torus(ring, Basis.Identity, 0.95f, 0.22f, 18, 6, MDark);
        m.Ellipsoid(ring + Vector3.Down * 0.3f, new Vector3(0.6f, 0.4f, 0.6f), 4, 10, MDark);
        m.Torus(TCorePos + Vector3.Up * 1.15f, Basis.Identity, 1.55f, 0.35f, 22, 6, MDark);
        // Feux rouges de position sous la coque.
        m.Use(MachRed());
        for (var k = 0; k < 3; k++)
        {
            var a = TLegAngles[k] + Mathf.Pi / 3;
            m.Ellipsoid(new Vector3(Mathf.Sin(a) * 4.2f, -1.6f, Mathf.Cos(a) * 4.4f), Vector3.One * 0.12f, 2, 6, Colors.White);
        }
        m.Ellipsoid(new Vector3(0.95f, 5.24f, -3.01f), Vector3.One * 0.09f, 2, 6, Colors.White);
        // Fentes de chauffe orange autour du noyau.
        m.Use(MachHot());
        m.Torus(TCorePos + Vector3.Up * 1.15f, Basis.Identity, 1.55f, 0.08f, 22, 4, Colors.White);
    }

    /// <summary>Patte : hanche (lacet) → fémur qui monte vers le genou haut → long tibia → pied tenu à plat.</summary>
    private void AddTripodLeg(int i)
    {
        var a = TLegAngles[i];
        var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
        var hip = new Node3D { Position = d * THipR + new Vector3(0, THipY, 0) };
        _bodyPivot.AddChild(hip);
        var fem = new Node3D();
        hip.AddChild(fem);
        var alongX = new Basis(Vector3.Forward, Vector3.Up, Vector3.Right); // Z local le long de +X : plaques le long de l'os
        Part(fem, "femur", m =>
        {
            m.Use(MachArmor());
            Plate(m, new Transform3D(alongX, new Vector3(TLf * 0.5f, 0.1f, 0)), new Vector3(0.75f, 0.85f, TLf * 0.44f), MGun, 0.14f, 6f, 0.7f, 0.75f);
            Plate(m, new Transform3D(alongX, new Vector3(TLf * 0.46f, 0.92f, 0)), new Vector3(0.5f, 0.14f, TLf * 0.32f), MSteel, 0.08f, 9f, 0.75f);
            for (var s = -1; s <= 1; s += 2) Groove(m, new Vector3(1.5f, 0.1f, 0.77f * s), new Vector3(TLf - 1.5f, 0.1f, 0.56f * s), Vector3.Back * s);
            m.Use(MachMech());
            Cyl(m, new Vector3(0, 0, -1.1f), new Vector3(0, 0, 1.1f), 1.2f, 16, MDark);
            for (var s = -1; s <= 1; s += 2)
                Piston(m, new Vector3(1.4f, -0.95f, 0.45f * s), new Vector3(TLf - 1.6f, -0.75f, 0.35f * s), 0.24f, MSteel);
            Cable(m, new Vector3(0.8f, 0.6f, -0.85f), new Vector3(TLf - 1.2f, 0.4f, -0.7f), 0.1f, 0.5f, MDark);
            // Genou : rotule massive à couronne.
            Cyl(m, new Vector3(TLf, 0, -0.95f), new Vector3(TLf, 0, 0.95f), 1.05f, 16, MDark);
            Cyl(m, new Vector3(TLf, 0, 0.75f), new Vector3(TLf, 0, 1.05f), 1.2f, 16, MSteel);
            Cyl(m, new Vector3(TLf, 0, -1.05f), new Vector3(TLf, 0, -0.75f), 1.2f, 16, MSteel);
            m.Use(MachRed());
            m.Ellipsoid(new Vector3(TLf, 0, 1.08f), new Vector3(0.2f, 0.2f, 0.05f), 2, 6, Colors.White);
        }, true, 260f);
        var knee = new Node3D { Position = new Vector3(TLf, 0, 0) };
        fem.AddChild(knee);
        Part(knee, "tibia", m =>
        {
            m.Use(MachArmor());
            // Tibia : manchon blindé haut, jambe intermédiaire, tige basse plus fine.
            Plate(m, new Transform3D(alongX, new Vector3(TLt * 0.24f, 0, 0)), new Vector3(0.82f, 0.78f, TLt * 0.2f), MGun, 0.14f, 6f, 0.75f, 0.75f);
            Plate(m, new Transform3D(alongX, new Vector3(TLt * 0.22f, 0.8f, 0)), new Vector3(0.48f, 0.14f, TLt * 0.16f), MSteel, 0.06f);
            m.Use(MachMech());
            Cyl(m, new Vector3(TLt * 0.38f, 0, 0), new Vector3(TLt * 0.97f, 0, 0), 0.5f, 12, MSteel, -1f, 0.36f);
            for (var k = 0; k < 3; k++)
                Cyl(m, new Vector3(TLt * (0.42f + k * 0.035f), 0, 0), new Vector3(TLt * (0.43f + k * 0.035f), 0, 0), 0.66f, 12, MDark);
            Cyl(m, new Vector3(TLt * 0.62f, 0, 0), new Vector3(TLt * 0.66f, 0, 0), 0.6f, 12, MDark);
            Piston(m, new Vector3(1.2f, 0.75f, 0), new Vector3(TLt * 0.6f, 0.45f, 0), 0.2f, MSteel);
            Cable(m, new Vector3(1.0f, -0.7f, 0.3f), new Vector3(TLt * 0.7f, -0.35f, 0.2f), 0.08f, 0.7f, MDark);
            m.Ellipsoid(new Vector3(TLt, 0, 0), Vector3.One * 0.62f, 4, 10, MDark);
        }, true, 260f);
        var foot = new Node3D { Position = new Vector3(TLt, 0, 0) };
        knee.AddChild(foot);
        Part(foot, "tfoot", m =>
        {
            m.Use(MachArmor());
            // Pied : sabot conique et trois griffes d'ancrage.
            Cyl(m, new Vector3(0, -0.2f, 0), new Vector3(0, -TAnkle + 0.35f, 0), 0.55f, 12, MSteel, -1f, 1.05f);
            Plate(m, new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2), new Vector3(0, -TAnkle + 0.22f, 0)), new Vector3(1.35f, 1.35f, 0.22f), MGun, 0.1f, 4f);
            m.Use(MachMech());
            for (var c = 0; c < 3; c++)
            {
                var ca = c * Mathf.Tau / 3;
                var cd = new Vector3(Mathf.Cos(ca), 0, Mathf.Sin(ca));
                m.Tube(new[] { cd * 0.9f + Vector3.Down * (TAnkle - 0.4f), cd * 1.9f + Vector3.Down * (TAnkle - 0.25f), cd * 2.4f + Vector3.Down * TAnkle },
                    new[] { 0.32f, 0.22f, 0.04f }, 6, MDark, Vector3.Up, 0.7f);
            }
        }, true, 260f);
        var leg = new Leg { Hip = hip, Femur = fem, Knee = knee, Side = 1f, Lf = TLf, Lt = TLt, Reach = TFootR, Phase = i / 3f };
        _legs.Add(leg);
        _tFeet.Add(foot);
    }

    // ---------------- Animation ----------------

    /// <summary>Démarche à trois temps, visée de l'émetteur, charge et tir du rayon, tentacules.</summary>
    public void AnimateTripod(float dt, float speed, float attack)
    {
        _t += dt;
        if (_tCharge >= 0) _tCharge = Mathf.Min(1f, _tCharge + dt / 1.8f);
        var glow = Mathf.Max(_tCharge, 0f) + _tFire;
        var pulse = 1f + Mathf.Sin(_t * (2.5f + glow * 14f)) * (0.05f + 0.06f * glow);
        if (!_dead)
        {
            _tCore.Scale = Vector3.One * (1f + glow * 0.18f) * pulse;
            _tLens.Scale = Vector3.One * (1f + glow * 0.6f) * (1f + Mathf.Sin(_t * 35f) * 0.08f * glow);
        }
        if (_dead) { _tBeam.Visible = false; _tImpact.Visible = false; return; }

        // Émetteur : vise le point du rayon, sinon la cible.
        Vector3? aim = _tFire > 0 ? _tBeamPoint : _mHasAim ? _mAim : null;
        if (aim is { } w)
        {
            var rel = _bodyPivot.GlobalTransform.AffineInverse() * w - TEmitterPos;
            var yaw = Mathf.Clamp(Mathf.Atan2(rel.X, rel.Z), -0.9f, 0.9f);
            var pitch = Mathf.Clamp(Mathf.Atan2(-rel.Y, new Vector2(rel.X, rel.Z).Length()), -0.2f, 1.25f);
            var k = Mathf.Clamp(dt * (_tFire > 0 ? 12f : 2f), 0, 1);
            _tEmitter.Rotation = new Vector3(Mathf.Lerp(_tEmitter.Rotation.X, pitch, k), Mathf.LerpAngle(_tEmitter.Rotation.Y, yaw, k), 0);
        }

        // Marche : appui 72 % du cycle, une patte après l'autre.
        _tAmp = Mathf.MoveToward(_tAmp, Mathf.Clamp(Mathf.Abs(speed) / 3.6f, 0f, 1f), dt * 1.2f);
        var amp = _tAmp;
        _tGait += dt * TStance * 3.6f / TStride * Mathf.Min(1f, amp * 5f) * (speed < -0.05f ? -1f : 1f);
        var g = _tGait;
        var bob = -0.35f * amp * (0.5f + 0.5f * Mathf.Cos(g * Mathf.Tau * 3f));
        _bodyPivot.Position = new Vector3(0, TBodyY + bob + Mathf.Sin(_t * 0.8f) * 0.12f, 0);
        _bodyPivot.Rotation = new Vector3(0.03f * amp + attack * 0.04f + Mathf.Sin(_t * 0.6f) * 0.01f, 0, Mathf.Sin(g * Mathf.Tau) * 0.025f * amp);
        var body = _bodyPivot.Transform;
        var inv = body.AffineInverse();
        for (var i = 0; i < _legs.Count; i++)
        {
            var l = _legs[i];
            var u = Mathf.PosMod(g + l.Phase, 1f);
            var (fa, lift) = GaitCurve(u, TStance);
            var a = TLegAngles[i];
            var foot = new Vector3(Mathf.Sin(a) * TFootR, TAnkle + lift * 2.6f * Mathf.Min(1f, amp * 3f), Mathf.Cos(a) * TFootR + fa * TStride * 0.5f * amp);
            var local = inv * foot - l.Hip.Position; // repère de la coque, depuis la hanche
            var horiz = new Vector2(local.X, local.Z);
            l.Hip.Rotation = new Vector3(0, Mathf.Atan2(-local.Z, local.X), 0);
            Ik(TLf, TLt, horiz.Length(), local.Y, out var e, out var k);
            l.Femur.Rotation = new Vector3(0, 0, e);
            l.Knee.Rotation = new Vector3(0, 0, k);
            _tFeet[i].Rotation = new Vector3(0, 0, -(e + k));
            if (amp > 0.25f && FootDown(_tPrevU[i], u, TStance)) Stomp?.Invoke(body * (l.Hip.Position + l.Hip.Basis * (l.Femur.Basis * (new Vector3(TLf, 0, 0) + l.Knee.Basis * new Vector3(TLt, 0, 0)))) with { Y = 0f });
            _tPrevU[i] = u;
        }

        // Tentacules : balancement lent, raidis pendant le tir.
        for (var s = 0; s < _tTendrils.Count; s++)
        {
            var chain = _tTendrils[s];
            for (var k = 0; k < chain.Length; k++)
            {
                var ph = _t * 0.9f + k * 0.7f + s * 2.1f;
                var amt = (0.1f + k * 0.04f) * (1f - glow * 0.6f);
                chain[k].Rotation = new Vector3(Mathf.Sin(ph) * amt + (k == 0 ? 0.25f + amp * 0.2f : 0.05f), 0, Mathf.Cos(ph * 0.8f) * amt * 0.7f);
            }
        }

        // Rayon.
        _tBeam.Visible = _tFire > 0f;
        _tImpact.Visible = _tFire > 0f;
        if (_tFire > 0f)
        {
            var from = TripodEmitter;
            var to = _tBeamPoint;
            var len = from.DistanceTo(to);
            if (len > 0.5f)
            {
                var flick = 0.85f + 0.15f * Mathf.Sin(_t * 47f) + 0.08f * Mathf.Sin(_t * 113f);
                var dir = (to - from) / len;
                _tBeam.GlobalTransform = new Transform3D(Basis.LookingAt(dir, Mathf.Abs(dir.Y) > 0.98f ? Vector3.Right : Vector3.Up) * Basis.FromScale(new Vector3(flick, flick, len)), from);
            }
            _tImpact.GlobalPosition = to;
            _tImpact.Scale = Vector3.One * (0.8f + 0.25f * Mathf.Sin(_t * 31f));
            _tImpactLight.LightEnergy = 5f + 2f * Mathf.Sin(_t * 23f);
        }
    }

    /// <summary>Mort : le noyau s'éteint, les pattes cèdent, la coque s'abat en basculant.</summary>
    public void TripodDeath()
    {
        _dead = true;
        _tFire = 0f;
        _tCharge = -1f;
        _tBeam.Visible = false;
        _tImpact.Visible = false;
        var tw = CreateTween().SetParallel();
        const float d = 1.3f, len = 2.4f;
        tw.TweenProperty(_bodyPivot, "position", new Vector3(1.5f, 2.6f, -1.0f), len).SetDelay(d).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        tw.TweenProperty(_bodyPivot, "rotation", new Vector3(-0.32f, 0.2f, 0.28f), len).SetDelay(d).SetEase(Tween.EaseType.In);
        for (var i = 0; i < _legs.Count; i++)
        {
            var l = _legs[i];
            tw.TweenProperty(l.Femur, "rotation", new Vector3(0, 0, 0.75f - i * 0.2f), len).SetDelay(d);
            tw.TweenProperty(l.Knee, "rotation", new Vector3(0, 0, -2.4f + i * 0.3f), len).SetDelay(d);
        }
        foreach (var chain in _tTendrils)
            foreach (var n in chain) tw.TweenProperty(n, "rotation", new Vector3(0.6f, 0, 0.3f), len).SetDelay(d);
        tw.TweenProperty(_tCore, "scale", Vector3.One * 0.7f, d + len);
        tw.Chain().TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(_tCore)) _tCore.MaterialOverride = Mats.Painted(new Color(0.1f, 0.06f, 0.04f), 0.3f, 0.5f);
            if (IsInstanceValid(_tLens)) _tLens.MaterialOverride = Mats.Painted(new Color(0.08f, 0.05f, 0.04f), 0.2f, 0.5f);
            if (IsInstanceValid(_tEyes)) _tEyes.MaterialOverride = Mats.Painted(new Color(0.08f, 0.02f, 0.02f), 0.3f, 0.4f);
            foreach (var light in _bodyPivot.FindChildren("*", "OmniLight3D", false, false)) ((OmniLight3D)light).LightEnergy = 0f;
        }));
    }
}
