using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Forteresse marcheuse extraterrestre : coque blindée d'environ 35 m perchée sur quatre longues pattes articulées,
/// noyau lumineux à l'avant (point faible), quatre tourelles laser, deux lance-missiles latéraux et canon principal ventral.
/// Démarche quadrupède lente (un pied après l'autre) ; chaque appui déclenche l'événement <see cref="Stomp"/>.
/// </summary>
public partial class BugModel
{
    // ---------------- Gabarit (repère du modèle, avant = +Z) ----------------

    /// <summary>Hauteur du centre de la coque.</summary>
    public const float FortHullY = 26f;
    /// <summary>Noyau (point faible), en saillie à l'avant de la coque.</summary>
    public static readonly Vector3 FortCorePos = new(0, 25f, 12.2f);
    public const float FortCoreRadius = 3f;

    private const float FortReach = 15f, FortLf = 18f, FortLt = 25f, FortFoot = 3f, FortSwing = 12f, FortFwd = 35f;
    /// <summary>Avance du corps par pas (composante avant de l'arc décrit par un pied).</summary>
    private static readonly float FortStride = 2f * FortReach * Mathf.Sin(Mathf.DegToRad(FortSwing)) * Mathf.Cos(Mathf.DegToRad(FortFwd));

    // Pattes : avant droite, avant gauche, arrière droite, arrière gauche.
    private static readonly Vector3[] FortHips = { new(8.6f, 24f, 8f), new(-8.6f, 24f, 8f), new(8.6f, 24f, -8f), new(-8.6f, 24f, -8f) };
    // Ordre de marche quadrupède : arrière gauche, avant gauche, arrière droite, avant droite.
    private static readonly float[] FortPhases = { 0.75f, 0.25f, 0.5f, 0f };
    private static readonly Vector3[] FortTurretPos = { new(4.2f, 31.4f, 5.5f), new(-4.2f, 31.4f, 5.5f), new(5f, 31.1f, -5.5f), new(-5f, 31.1f, -5.5f) };
    private static readonly Vector3[] FortPods = { new(10.6f, 29.6f, -4f), new(-10.6f, 29.6f, -4f) };
    private static readonly Vector3 FortMuzzle = new(0, 20.8f, 17.8f);

    private readonly List<Node3D> _fortTurrets = new();
    private readonly List<Node3D> _fortGuns = new();
    private MeshInstance3D _fortCore, _fortChargeBall;
    private OmniLight3D _fortCoreLight;
    private float _fortCharge = -1f;
    private float _fortFlashT;
    private Vector3 _fortAim;
    private bool _fortHasAim;
    private float _fortGait;
    private readonly float[] _fortPrevU = new float[4];

    /// <summary>Un pied touche le sol (position au sol, repère monde).</summary>
    public event Action<Vector3> Stomp;

    /// <summary>Position de repos d'un pied (repère du modèle), pour les formes de collision.</summary>
    public static Vector3 FortressFootRest(int i)
    {
        var h = FortHips[i];
        var side = Mathf.Sign(h.X);
        var f = Mathf.DegToRad(FortFwd) * Mathf.Sign(h.Z);
        return new Vector3(h.X + Mathf.Cos(f) * FortReach * side, 0, h.Z + Mathf.Sin(f) * FortReach);
    }

    public Vector3 FortressTurretMuzzle(int i) => _fortGuns[i % _fortGuns.Count].GlobalTransform * new Vector3(0, 0, 4.7f);
    public Vector3 FortressPod(int i) => _bodyPivot.GlobalTransform * FortPods[i % FortPods.Length];
    public Vector3 FortressMuzzle => _fortChargeBall.GlobalPosition;
    public int FortressTurretCount => _fortGuns.Count;
    /// <summary>Centre de la coque (suit l'effondrement à la mort).</summary>
    public Vector3 FortressHullCenter => _bodyPivot.GlobalTransform * new Vector3(0, FortHullY, 0);

    /// <summary>Oriente les tourelles vers un point (repère monde).</summary>
    public void FortressAim(Vector3 world)
    {
        _fortAim = world;
        _fortHasAim = true;
    }

    /// <summary>Début de la charge du canon principal (2 s : le noyau et la bouche du canon s'embrasent).</summary>
    public void FortressCharge() => _fortCharge = 0f;

    /// <summary>Tir du canon principal : éclair puis retour au repos.</summary>
    public void FortressFire()
    {
        _fortCharge = -1f;
        _fortFlashT = 1f;
    }

    // ---------------- Construction ----------------

    private static StandardMaterial3D _fortArmor;

    private static StandardMaterial3D FortCyan() => Mats.Glow(new Color(0.35f, 0.9f, 1f), 3f);

    /// <summary>Anneaux supplémentaires du loft de coque : un joint de panneau tous les huitièmes.</summary>
    private static readonly float[] Grooves =
    {
        0.119f, 0.125f, 0.131f, 0.244f, 0.25f, 0.256f, 0.369f, 0.375f, 0.381f, 0.494f, 0.506f, 0.619f, 0.625f, 0.631f, 0.744f, 0.75f, 0.756f, 0.869f, 0.875f, 0.881f,
    };

    /// <summary>Blindage satiné teinté par les couleurs de sommets (moins réfléchissant que le métal des drones).</summary>
    private static StandardMaterial3D FortArmor()
    {
        if (_fortArmor != null) return _fortArmor;
        _fortArmor = (StandardMaterial3D)Mats.Metal(new Color(1f, 1f, 1f), 0.5f).Duplicate();
        _fortArmor.VertexColorUseAsAlbedo = true;
        _fortArmor.VertexColorIsSrgb = true;
        _fortArmor.Metallic = 0.4f;
        _fortArmor.NormalScale = 0.5f;
        _fortArmor.RimEnabled = true;
        _fortArmor.Rim = 0.2f;
        return _fortArmor;
    }

    private void BuildFortress()
    {
        _pivotY = 0f;
        _bodyPivot = new Bone();
        AddChild(_bodyPivot);
        var armor = new Color(0.52f, 0.53f, 0.56f);
        var dark = new Color(0.25f, 0.26f, 0.28f);
        var groove = new Color(0.06f, 0.06f, 0.065f);
        var rust = new Color(0.42f, 0.13f, 0.08f);
        var red = Mats.Glow(new Color(1f, 0.1f, 0.06f), 5f);

        Part(_bodyPivot, "hull", m =>
        {
            m.Use(FortArmor());
            // Coque : loft à section presque carrée, joints de panneaux sombres et bande rouille sur les flancs.
            BodyLoft(m, -13f, 13.2f,
                new[] { 3f, 8f, 9.4f, 9.8f, 9.7f, 8.8f, 6.6f, 3.6f },
                new[] { 26.5f, 29.6f, 31f, 31.5f, 31.4f, 30.6f, 29.2f, 27.2f },
                new[] { 24.5f, 22f, 21f, 20.6f, 20.7f, 21.4f, 22.6f, 24.2f },
                Steps(18, Grooves), 28, armor, 3.4f, null, (t, th, p, c) =>
                {
                    if (Mathf.Abs(t - Mathf.Round(t * 8f) / 8f) < 0.002f && t > 0.01f && t < 0.99f) return groove;
                    if (Mathf.Abs(p.X) > 6.5f && Mathf.Abs(p.Y - 28.2f) < 0.45f) return rust;
                    return p.Y < 23.4f ? dark : c;
                });
            // Arête dorsale et plaques de blindage.
            for (var k = -3; k <= 3; k++)
                m.Box(new Transform3D(new Basis(Vector3.Right, 0.08f * k), new Vector3(0, 31.75f - Mathf.Abs(k) * 0.12f, k * 3.1f)), new Vector3(2.3f, 0.4f, 1.25f), dark);
            for (var s = -1; s <= 1; s += 2)
            {
                for (var k = -1; k <= 1; k++)
                    m.Box(new Transform3D(new Basis(Vector3.Forward, 0.22f * s), new Vector3(9.5f * s, 25.2f, k * 6.2f)), new Vector3(0.45f, 2.4f, 2.8f), dark);
                // Lance-missiles latéraux.
                var pod = FortPods[s > 0 ? 0 : 1];
                m.Box(new Transform3D(Basis.Identity, pod + new Vector3(0, -1.7f, 0)), new Vector3(1.5f, 1.8f, 3.2f), dark);
                m.Box(new Transform3D(Basis.Identity, pod + new Vector3(-0.9f * s, -2.4f, 0)), new Vector3(0.6f, 0.9f, 2.4f), armor);
                for (var i = 0; i < 2; i++)
                for (var j = 0; j < 3; j++)
                    m.Ellipsoid(pod + new Vector3((i - 0.5f) * 1.3f, 0.12f, (j - 1) * 1.9f), new Vector3(0.5f, 0.5f, 0.08f), 2, 8, groove, new Basis(Vector3.Right, Mathf.Pi / 2));
            }
            // Logements des hanches.
            foreach (var h in FortHips)
            {
                m.Ellipsoid(h, new Vector3(2.9f, 2.9f, 2.9f), 6, 12, dark);
                m.Torus(h + new Vector3(0, 1.2f, 0), Basis.Identity, 2.9f, 0.35f, 16, 5, armor);
            }
            // Machinerie ventrale et canon principal.
            m.Ellipsoid(new Vector3(0, 20.6f, -1.5f), new Vector3(6f, 1.8f, 8.5f), 6, 16, dark);
            m.Ellipsoid(new Vector3(0, 21.4f, 3.5f), new Vector3(3.2f, 2.3f, 4.5f), 6, 14, armor);
            m.Segment(new Vector3(0, 21.4f, 4f), FortMuzzle - new Vector3(0, 0, 0.6f), 1.8f, 1.3f, 12, dark, Vector3.Up, 0f, 4);
            for (var k = 0; k < 3; k++)
                m.Torus(new Vector3(0, 21.2f - k * 0.13f, 9f + k * 2.6f), new Basis(Vector3.Right, Mathf.Pi / 2), 1.75f - k * 0.1f, 0.32f, 16, 5, armor);
            m.Torus(FortMuzzle, new Basis(Vector3.Right, Mathf.Pi / 2), 1.45f, 0.42f, 18, 6, armor);
            // Logement du noyau, visière et mâchoires de blindage.
            m.Torus(FortCorePos + new Vector3(0, 0, -0.6f), new Basis(Vector3.Right, Mathf.Pi / 2), 3.3f, 0.75f, 24, 6, dark);
            m.Box(new Transform3D(new Basis(Vector3.Right, -0.38f), new Vector3(0, 28.9f, 10.6f)), new Vector3(4.6f, 0.55f, 1.9f), dark);
            for (var s = -1; s <= 1; s += 2)
                m.Box(new Transform3D(new Basis(Vector3.Up, 0.5f * s), new Vector3(3.9f * s, 24.3f, 11f)), new Vector3(0.6f, 2.6f, 1.6f), dark);
            // Feux de capteurs rouges.
            m.Use(red);
            for (var s = -1; s <= 1; s += 2)
            {
                for (var k = 0; k < 3; k++)
                    m.Ellipsoid(new Vector3(s * (1.6f + k * 1.3f), 28.15f - k * 0.2f, 11.35f - k * 0.55f), new Vector3(0.36f, 0.24f, 0.2f), 3, 8, Colors.White);
                for (var k = -1; k <= 1; k++)
                    m.Ellipsoid(new Vector3(s * 9.75f, 28.9f, k * 6f), new Vector3(0.12f, 0.3f, 0.3f), 3, 6, Colors.White);
                m.Ellipsoid(new Vector3(s * 3.5f, 28.5f, -12.2f), new Vector3(0.3f, 0.3f, 0.12f), 3, 6, Colors.White);
            }
            foreach (var pod in FortPods)
                for (var j = 0; j < 3; j++)
                    m.Ellipsoid(pod + new Vector3(0, -0.6f, (j - 1) * 1.9f), new Vector3(0.1f, 0.1f, 0.1f) * 1.6f, 2, 6, Colors.White);
            // Fentes d'énergie cyan sur les plaques latérales et le ventre.
            m.Use(FortCyan());
            for (var s = -1; s <= 1; s += 2)
            for (var k = -1; k <= 1; k++)
                m.Box(new Transform3D(new Basis(Vector3.Forward, 0.22f * s), new Vector3(9.98f * s, 25.4f, k * 6.2f)), new Vector3(0.05f, 0.14f, 2.2f), Colors.White);
            for (var k = 0; k < 4; k++)
            {
                var z = -7f + k * 2.6f;
                var u = (z + 1.5f) / 8.5f;
                m.Box(new Transform3D(Basis.Identity, new Vector3(0, 20.6f - 1.8f * Mathf.Sqrt(1f - u * u) - 0.02f, z)), new Vector3(1.5f, 0.06f, 0.18f), Colors.White);
            }
        });

        // Noyau : sphère lumineuse pulsante (grossit pendant la charge du canon).
        var coreColor = new Color(0.35f, 0.9f, 1f);
        _fortCore = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = FortCoreRadius * 0.92f, Height = FortCoreRadius * 1.84f, RadialSegments = 20, Rings = 10 },
            MaterialOverride = Mats.Glow(coreColor, 4.5f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = FortCorePos,
        };
        _bodyPivot.AddChild(_fortCore);
        _fortCoreLight = new OmniLight3D { Position = FortCorePos + new Vector3(0, 0, 3f), LightColor = coreColor, LightEnergy = 2f, OmniRange = 26f };
        _bodyPivot.AddChild(_fortCoreLight);
        _fortChargeBall = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 14, Rings = 7 },
            MaterialOverride = Mats.Glow(new Color(0.6f, 0.95f, 1f), 8f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = FortMuzzle + new Vector3(0, 0, 0.4f),
            Visible = false,
        };
        _bodyPivot.AddChild(_fortChargeBall);
        _bodyPivot.AddChild(new OmniLight3D { Position = new Vector3(0, 28.5f, 13f), LightColor = new Color(1f, 0.12f, 0.08f), LightEnergy = 1.5f, OmniRange = 16f });

        // Tourelles laser : socle orientable en lacet, double canon en tangage.
        for (var i = 0; i < FortTurretPos.Length; i++)
        {
            var yaw = new Node3D { Position = FortTurretPos[i] };
            _bodyPivot.AddChild(yaw);
            Part(yaw, "turret", m =>
            {
                m.Use(FortArmor());
                m.Tube(new[] { new Vector3(0, -0.6f, 0), new Vector3(0, 0.6f, 0) }, new[] { 1.9f, 1.7f }, 14, dark, Vector3.Forward);
                m.Ellipsoid(new Vector3(0, 0.8f, 0.2f), new Vector3(1.6f, 1.05f, 1.9f), 6, 14, armor);
                m.Box(new Transform3D(Basis.Identity, new Vector3(0, 1.1f, -1.3f)), new Vector3(0.9f, 0.5f, 0.6f), dark);
                m.Use(red);
                m.Ellipsoid(new Vector3(0, 1.25f, 1.75f), new Vector3(0.45f, 0.2f, 0.2f), 3, 8, Colors.White);
            });
            var gun = new Node3D { Position = new Vector3(0, 1.1f, 0.9f) };
            yaw.AddChild(gun);
            Part(gun, "gun", m =>
            {
                m.Use(FortArmor());
                m.Box(new Transform3D(Basis.Identity, new Vector3(0, 0, 0.4f)), new Vector3(1.05f, 0.45f, 0.7f), dark);
                for (var s = -1; s <= 1; s += 2)
                {
                    m.Segment(new Vector3(0.5f * s, 0, 0.6f), new Vector3(0.5f * s, 0, 4.5f), 0.3f, 0.22f, 8, dark, Vector3.Up, 0.6f, 2);
                    m.Torus(new Vector3(0.5f * s, 0, 2.2f), new Basis(Vector3.Right, Mathf.Pi / 2), 0.34f, 0.08f, 10, 4, armor);
                }
                m.Use(red);
                for (var s = -1; s <= 1; s += 2)
                    m.Ellipsoid(new Vector3(0.5f * s, 0, 4.55f), new Vector3(0.14f, 0.14f, 0.06f), 2, 6, Colors.White);
            });
            _fortTurrets.Add(yaw);
            _fortGuns.Add(gun);
        }

        for (var i = 0; i < FortHips.Length; i++) AddFortressLeg(i, armor, dark, groove, rust);
        for (var i = 0; i < 4; i++) _fortPrevU[i] = _legs[i].Phase;
    }

    /// <summary>Patte : fémur relevé vers le genou haut, tibia presque vertical, pied lourd à griffes.</summary>
    private void AddFortressLeg(int i, Color armor, Color dark, Color groove, Color rust)
    {
        var hipPos = FortHips[i];
        var side = Mathf.Sign(hipPos.X);
        var fwd = FortFwd * Mathf.Sign(hipPos.Z);
        const float gamma = Mathf.Pi / 2;
        var drop = FortFoot;
        Ik(FortLf, FortLt, FortReach, -(_pivotY + hipPos.Y) + drop, out var e0, out var k0);
        var tau = -gamma - (e0 + k0);
        var sideKey = side > 0 ? "R" : "L";
        var hip = new Bone { Position = hipPos, Rotation = new Vector3(0, -Mathf.DegToRad(fwd) * side, 0) };
        _bodyPivot.AddChild(hip);
        var fem = new Bone();
        hip.AddChild(fem);
        Part(fem, "femur" + sideKey, m =>
        {
            m.Mirror = side < 0;
            m.Use(FortArmor());
            m.Segment(Vector3.Zero, new Vector3(FortLf, 0, 0), 1.9f, 1.5f, 10, armor, Vector3.Up, 0.25f, 4, null, t => t > 0.42f && t < 0.46f ? groove : armor);
            m.Box(new Transform3D(Basis.Identity, new Vector3(FortLf * 0.5f, 1.55f, 0)), new Vector3(FortLf * 0.32f, 0.35f, 1.3f), dark);
            // Vérin hydraulique sous le fémur.
            m.Segment(new Vector3(2.2f, -1.7f, 0), new Vector3(FortLf * 0.55f, -1.5f, 0), 0.6f, 0.6f, 6, dark, Vector3.Up, 0f, 1);
            m.Segment(new Vector3(FortLf * 0.5f, -1.5f, 0), new Vector3(FortLf * 0.85f, -1.1f, 0), 0.32f, 0.32f, 6, armor, Vector3.Up, 0f, 1);
            m.Ellipsoid(new Vector3(FortLf, 0, 0), new Vector3(2.3f, 2.3f, 2.3f), 5, 10, dark);
            m.Cone(new Vector3(FortLf + 0.2f, 1.8f, 0), new Vector3(FortLf + 1.4f, 4.6f, 0), 0.7f, 6, dark, true);
            m.Use(Mats.Glow(new Color(1f, 0.1f, 0.06f), 5f));
            m.Ellipsoid(new Vector3(FortLf, 0, 2.2f), new Vector3(0.35f, 0.35f, 0.15f), 2, 6, Colors.White);
            m.Use(FortCyan());
            m.Torus(new Vector3(3.2f, 0, 0), new Basis(Vector3.Back, Mathf.Pi / 2), 1.9f, 0.14f, 16, 4, Colors.White);
            m.Box(new Transform3D(Basis.Identity, new Vector3(FortLf * 0.5f, 1.92f, 0)), new Vector3(FortLf * 0.26f, 0.05f, 0.16f), Colors.White);
        });
        var knee = new Bone { Position = new Vector3(FortLf * side, 0, 0) };
        fem.AddChild(knee);
        Part(knee, "tibia" + sideKey, m =>
        {
            m.Mirror = side < 0;
            m.Use(FortArmor());
            m.Segment(Vector3.Zero, new Vector3(FortLt, 0, 0), 1.7f, 1.15f, 10, armor, Vector3.Up, 0.35f, 5, null,
                t => Mathf.Abs(t - 0.3f) < 0.025f || Mathf.Abs(t - 0.62f) < 0.02f ? groove : t > 0.66f && t < 0.72f ? rust : armor);
            // Plaque de tibia.
            m.Box(new Transform3D(Basis.Identity, new Vector3(FortLt * 0.38f, 1.5f, 0)), new Vector3(FortLt * 0.3f, 0.35f, 1.35f), dark);
            // Manchons de blindage annelés.
            for (var k = 0; k < 3; k++)
                m.Torus(new Vector3(FortLt * (0.76f + k * 0.06f), 0, 0), new Basis(Vector3.Back, Mathf.Pi / 2), 1.36f - k * 0.04f, 0.22f, 16, 5, dark);
            // Cheville et pied.
            var dir = new Vector3(Mathf.Cos(tau), Mathf.Sin(tau), 0);
            var ankle = new Vector3(FortLt, 0, 0);
            var pc = ankle + dir * (FortFoot - 0.55f);
            m.Ellipsoid(ankle, new Vector3(1.5f, 1.5f, 1.5f), 5, 10, dark);
            m.Segment(ankle, pc, 1.2f, 1.5f, 10, dark, Vector3.Forward, 0f, 1);
            m.Tube(new[] { pc - dir * 0.9f, pc - dir * 0.8f, pc + dir * 0.1f, pc + dir * 0.5f, pc + dir * 0.55f },
                new[] { 0f, 2.1f, 3.1f, 3.1f, 0f }, 14, dark, Vector3.Forward, 1f, null, 2.6f);
            var p1 = new Vector3(-dir.Y, dir.X, 0);
            var p2 = Vector3.Back;
            foreach (var d in new[] { p1, -p1, p2, -p2 })
                m.Segment(pc + d * 2.2f, pc + d * 4.1f + dir * 0.4f, 0.75f, 0.35f, 6, armor, dir, 0f, 1);
        });
        var leg = new Leg
        {
            Hip = hip, Femur = fem, Knee = knee, Side = side, Fwd = fwd, Swing = FortSwing, Lf = FortLf, Lt = FortLt,
            Reach = FortReach, HipY = hipPos.Y, Drop = drop, Lift = 2.6f, Phase = FortPhases[i],
        };
        _legs.Add(leg);
        PoseLeg(leg, 0f, 0f);
    }

    // ---------------- Animation ----------------

    /// <summary>Démarche quadrupède (vitesse en m/s), tourelles, pulsation du noyau et charge du canon.</summary>
    public void AnimateFortress(float dt, float speed, float attack)
    {
        _t += dt;
        if (_fortCharge >= 0) _fortCharge = Mathf.Min(1f, _fortCharge + dt / 2f);
        _fortFlashT = Mathf.Max(0f, _fortFlashT - dt * 2.5f);
        var glow = Mathf.Max(_fortCharge, 0f);
        var pulse = 1f + Mathf.Sin(_t * (3f + glow * 16f)) * (0.04f + 0.08f * glow);
        if (!_dead)
        {
            _fortCore.Scale = Vector3.One * (1f + glow * 0.22f + _fortFlashT * 0.3f) * pulse;
            _fortCoreLight.LightEnergy = 2f + glow * 10f + _fortFlashT * 14f;
        }
        _fortChargeBall.Visible = glow > 0f || _fortFlashT > 0f;
        _fortChargeBall.Scale = Vector3.One * Mathf.Max(0.05f, glow * 2.8f + _fortFlashT * 4f) * pulse;
        if (_dead) return;

        // Tourelles : suivent la cible, ou balayent lentement.
        for (var i = 0; i < _fortTurrets.Count; i++)
        {
            var tur = _fortTurrets[i];
            float yaw, pitch;
            if (_fortHasAim)
            {
                var rel = _bodyPivot.ToLocal(_fortAim) - tur.Position;
                yaw = Mathf.Atan2(rel.X, rel.Z);
                pitch = Mathf.Clamp(Mathf.Atan2(-(rel.Y - 1.1f), new Vector2(rel.X, rel.Z).Length()), -0.4f, 1.2f);
            }
            else
            {
                yaw = Mathf.Sin(_t * 0.3f + i * 1.7f) * 0.9f + (i >= 2 ? Mathf.Pi * Mathf.Sign(FortTurretPos[i].X) * 0.5f : 0f);
                pitch = 0.1f;
            }
            tur.Rotation = new Vector3(0, Mathf.LerpAngle(tur.Rotation.Y, yaw, Mathf.Clamp(dt * 2.5f, 0, 1)), 0);
            var gun = _fortGuns[i];
            gun.Rotation = new Vector3(Mathf.Lerp(gun.Rotation.X, pitch, Mathf.Clamp(dt * 2.5f, 0, 1)), 0, 0);
        }

        // Marche : appui 75 % du cycle, transfert 25 % ; la cadence suit la vitesse pour que les pieds ne glissent pas.
        var amp = Mathf.Clamp(speed / 2.5f, 0f, 1f);
        _fortGait += dt * speed * 0.75f / FortStride;
        var g = _fortGait;
        _bodyPivot.Position = new Vector3(0, -0.55f * amp * (1f - Mathf.Abs(Mathf.Sin(g * Mathf.Tau * 2f))), 0);
        _bodyPivot.Rotation = new Vector3(Mathf.Sin(g * Mathf.Tau * 2f) * 0.008f * amp + attack * 0.02f, 0, Mathf.Sin(g * Mathf.Tau) * 0.014f * amp);
        for (var i = 0; i < _legs.Count; i++)
        {
            var l = _legs[i];
            var u = Mathf.PosMod(g + l.Phase, 1f);
            float fa, lift = 0f;
            if (u < 0.75f) fa = 1f - 2f * u / 0.75f;
            else
            {
                var w = (u - 0.75f) / 0.25f;
                fa = -1f + 2f * w * w * (3f - 2f * w);
                lift = Mathf.Sin(w * Mathf.Pi);
            }
            PoseLeg(l, fa * amp, lift * amp);
            // Le pied se repose : impact au sol.
            if (amp > 0.3f && _fortPrevU[i] > 0.6f && u < 0.3f && Stomp != null)
            {
                var foot = l.Knee.GlobalTransform * new Vector3(l.Lt * l.Side, 0, 0);
                Stomp(new Vector3(foot.X, 0, foot.Z));
            }
            _fortPrevU[i] = u;
        }
    }

    /// <summary>Effondrement : les pattes cèdent, la coque s'écrase en basculant, le noyau s'éteint.</summary>
    public void FortressDeath()
    {
        _dead = true;
        _fortCharge = -1f;
        var tw = CreateTween().SetParallel();
        tw.TweenProperty(_bodyPivot, "position", new Vector3(0, -17.2f, 1.5f), 2.6f).SetDelay(1.4f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        tw.TweenProperty(_bodyPivot, "rotation", new Vector3(0.12f, 0, -0.09f), 2.6f).SetDelay(1.4f).SetEase(Tween.EaseType.In);
        foreach (var l in _legs)
        {
            tw.TweenProperty(l.Femur, "rotation", new Vector3(0, 0, 0.35f * l.Side), 2.4f).SetDelay(1.4f);
            tw.TweenProperty(l.Knee, "rotation", new Vector3(0, 0, -1.4f * l.Side), 2.4f).SetDelay(1.4f);
        }
        tw.TweenProperty(_fortCore, "scale", Vector3.One * 0.75f, 4f);
        tw.TweenProperty(_fortCoreLight, "light_energy", 0f, 4f);
        tw.Chain().TweenCallback(Callable.From(() => _fortCore.MaterialOverride = Mats.Painted(new Color(0.08f, 0.12f, 0.14f), 0.3f, 0.4f)));
    }
}
