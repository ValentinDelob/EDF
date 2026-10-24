using System;
using Godot;

namespace Rempart;

/// <summary>
/// Marcheur : robot bipède d'environ 14 m. Jambes digitigrades (genou en avant, tibia qui repart vers l'arrière, tarse
/// vertical et pied à trois orteils), bassin à vérins, fuselage voûté au nez plongeant (fente de capteurs rouges, tourelle
/// de menton), nacelles d'épaules lance-missiles, deux bras-canons lourds suspendus dessous. Points faibles : les
/// articulations des genoux (actionneurs exposés, lueur de chauffe). Démarche bipède par cinématique inverse (pieds posés
/// sans glisser), secousse à chaque pas (<see cref="Stomp"/>).
/// </summary>
public partial class BugModel
{
    // ---------------- Gabarit (repère du modèle, avant = +Z, mètres) ----------------

    private const float WHipY = 7.4f, WThigh = 4.4f, WShin = 4.6f, WAnkle = 1.35f, WHipX = 2.45f, WStride = 3.8f, WStance = 0.62f;
    /// <summary>Cheville au repos par rapport à la hanche (un peu en arrière).</summary>
    private const float WAnkleZ = -0.35f;
    /// <summary>Taille (pivot du torse) dans le repère du bassin.</summary>
    private static readonly Vector3 WWaist = new(0, 0.55f, 0);
    /// <summary>Pivots des bras-canons (sous les nacelles d'épaules), repère du torse.</summary>
    private static readonly Vector3[] WShoulders = { new(3.3f, 2.35f, 0.5f), new(-3.3f, 2.35f, 0.5f) };
    private static readonly Vector3 WBarrelPos = new(0, -0.75f, 0);
    private const float WMuzzleZ = 7.0f;
    /// <summary>Hauteur du centre du torse (centre de l'ennemi, cible des alliés).</summary>
    public const float WalkerCenterY = 10.4f;
    public const float WalkerKneeRadius = 1.0f;

    private Node3D _wWaist, _wHead;
    private readonly Node3D[] _wHip = new Node3D[2], _wKnee = new Node3D[2], _wAnkleN = new Node3D[2];
    private readonly Node3D[] _wArm = new Node3D[2], _wBarrel = new Node3D[2];
    private readonly MeshInstance3D[] _wChargeBall = new MeshInstance3D[2];
    private readonly float[] _wCharge = { -1f, -1f };
    private readonly float[] _wRecoil = new float[2];
    private readonly float[] _wPrevU = new float[2];
    private Node3D _wSight;
    private Vector3 _wSightTarget;
    private int _wSightArm = -1;
    private float _wGait, _wYaw, _wAmp;
    private Vector3 _mAim;
    private bool _mHasAim;
    private MeshInstance3D _wEyes;

    /// <summary>Oriente torse, capteurs et canons (Marcheur), émetteur (Tripode) ou tourelles (vaisseau) vers un point monde.</summary>
    public void MachineAim(Vector3 world)
    {
        _mAim = world;
        _mHasAim = true;
    }

    public Vector3 WalkerMuzzle(int arm) => _wBarrel[arm & 1].GlobalTransform * new Vector3(0, 0, WMuzzleZ);
    public Vector3 WalkerKnee(int leg) => _wKnee[leg & 1].GlobalPosition;
    /// <summary>Milieu de la cuisse et du tibia (formes de collision des jambes).</summary>
    public Transform3D WalkerThigh(int leg) => _wHip[leg & 1].GlobalTransform * new Transform3D(Basis.Identity, new Vector3(0, -WThigh * 0.5f, 0.1f));
    public Transform3D WalkerShin(int leg) => _wKnee[leg & 1].GlobalTransform * new Transform3D(Basis.Identity, new Vector3(0, -WShin * 0.5f, 0));
    public Transform3D WalkerTorso => _wWaist.GlobalTransform * new Transform3D(Basis.Identity, new Vector3(0, 2.4f, 0.6f));
    public Vector3 WalkerFoot(int leg) => _wAnkleN[leg & 1].GlobalTransform * new Vector3(0, -WAnkle, 0.4f);

    /// <summary>Début de la charge d'un canon (cible verrouillée : trait de visée rouge jusqu'au tir).</summary>
    public void WalkerCharge(int arm, Vector3 target)
    {
        _wCharge[arm & 1] = 0f;
        _wSightArm = arm & 1;
        _wSightTarget = target;
    }

    /// <summary>Tir d'un canon : recul du tube, fin de la visée.</summary>
    public void WalkerFire(int arm)
    {
        _wCharge[arm & 1] = -1f;
        _wRecoil[arm & 1] = 1f;
        if (_wSightArm == (arm & 1)) _wSightArm = -1;
    }

    // ---------------- Construction ----------------

    private void BuildWalker()
    {
        _pivotY = WHipY;
        _bodyPivot = new Node3D { Position = new Vector3(0, WHipY, 0) };
        AddChild(_bodyPivot);

        // Bassin : caisson central, tambours de hanche, jupes de blindage, couronne de la taille.
        Part(_bodyPivot, "pelvis", m =>
        {
            m.Use(MachArmor());
            Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 0.05f, -0.1f)), new Vector3(1.45f, 0.82f, 1.5f), MSteel, 0.22f, 7f);
            PlateAt(m, new Vector3(0, -0.2f, 1.35f), new Vector3(1.2f, 0.7f, 0.22f), MGun, 0, -0.4f, 0, 0.1f, 0.85f);
            PlateAt(m, new Vector3(0, -0.15f, -1.6f), new Vector3(1.25f, 0.72f, 0.22f), MGun, 0, 0.35f, 0, 0.1f, 0.85f);
            for (var s = -1; s <= 1; s += 2)
                PlateAt(m, new Vector3(1.55f * s, -0.35f, -0.1f), new Vector3(0.14f, 0.6f, 1.25f), MGun, 0, 0, 0.15f * s);
            m.Use(MachMech());
            Cyl(m, new Vector3(0, 0.65f, -0.1f), new Vector3(0, 1.0f, -0.1f), 1.45f, 20, MDark);
            for (var s = -1; s <= 1; s += 2)
            {
                Cyl(m, new Vector3(1.2f * s, 0, 0), new Vector3((WHipX - 0.75f) * s, 0, 0), 0.92f, 18, MDark);
                Cyl(m, new Vector3((WHipX - 0.85f) * s, 0, 0), new Vector3((WHipX - 0.62f) * s, 0, 0), 1.05f, 18, MSteel);
                for (var k = 0; k < 8; k++)
                {
                    var a = k * Mathf.Tau / 8;
                    m.Box(new Transform3D(Basis.Identity, new Vector3((WHipX - 0.6f) * s, Mathf.Sin(a) * 0.85f, Mathf.Cos(a) * 0.85f)), new Vector3(0.04f, 0.08f, 0.08f), MBare);
                }
                // Vérins de stabilisation bassin → torse.
                Piston(m, new Vector3(0.85f * s, 0.15f, -1.1f), new Vector3(1.05f * s, 1.35f, -0.85f), 0.16f, MSteel);
            }
        });

        _wWaist = new Node3D { Position = WWaist };
        _bodyPivot.AddChild(_wWaist);
        Part(_wWaist, "torso", BuildWalkerTorso);
        _wEyes = Part(_wWaist, "eyes", m =>
        {
            // Fente de capteurs le long du nez : trois yeux par côté, plus un œil central.
            m.Use(MachRed());
            for (var s = -1; s <= 1; s += 2)
            for (var k = 0; k < 3; k++)
                m.Ellipsoid(new Vector3(s * (0.55f + k * 0.42f), 2.12f - k * 0.05f, 4.18f - k * 0.5f), new Vector3(0.09f, 0.07f, 0.08f), 3, 8, Colors.White,
                    new Basis(Vector3.Up, 0.35f * s));
            m.Ellipsoid(new Vector3(0, 2.0f, 4.45f), new Vector3(0.12f, 0.08f, 0.05f), 3, 8, Colors.White);
        }, false);

        // Tourelle de menton : suit la cible.
        _wHead = new Node3D { Position = new Vector3(0, 0.95f, 3.55f) };
        _wWaist.AddChild(_wHead);
        Part(_wHead, "head", m =>
        {
            m.Use(MachMech());
            m.Ellipsoid(Vector3.Zero, new Vector3(0.62f, 0.5f, 0.62f), 5, 12, MSteel);
            for (var s = -1; s <= 1; s += 2)
            {
                Cyl(m, new Vector3(0.2f * s, -0.08f, 0.3f), new Vector3(0.2f * s, -0.08f, 1.65f), 0.08f, 8, MDark);
                Cyl(m, new Vector3(0.2f * s, -0.08f, 0.35f), new Vector3(0.2f * s, -0.08f, 0.8f), 0.13f, 8, MDark);
            }
            m.Use(MachRed());
            m.Ellipsoid(new Vector3(0, 0.18f, 0.55f), new Vector3(0.1f, 0.06f, 0.04f), 2, 8, Colors.White);
        });

        // Bras-canons.
        for (var i = 0; i < 2; i++)
        {
            var side = i == 0 ? 1f : -1f;
            var arm = new Node3D { Position = WShoulders[i] };
            _wWaist.AddChild(arm);
            _wArm[i] = arm;
            var sideKey = side > 0 ? "R" : "L";
            Part(arm, "arm" + sideKey, m =>
            {
                m.Mirror = side < 0;
                m.Use(MachArmor());
                // Carter de culasse sous la nacelle, nez en biseau.
                Plate(m, new Transform3D(Basis.Identity, new Vector3(0, -0.72f, 0.3f)), new Vector3(0.66f, 0.72f, 1.95f), MGun, 0.15f, 7f, 0.82f, 0.75f);
                PlateAt(m, new Vector3(0.66f, -0.68f, 0.2f), new Vector3(0.12f, 0.62f, 1.6f), MSteel, 0, 0, 0.06f);
                PlateAt(m, new Vector3(0, -0.02f, 0.1f), new Vector3(0.55f, 0.12f, 1.35f), MSteel);
                Groove(m, new Vector3(0.79f, -0.35f, -1.3f), new Vector3(0.79f, -0.35f, 1.6f), Vector3.Right);
                Groove(m, new Vector3(0.79f, -1.05f, -1.3f), new Vector3(0.79f, -1.05f, 1.6f), Vector3.Right);
                Vents(m, new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2), new Vector3(0.8f, -0.7f, -0.75f)), new Vector2(0.95f, 0.55f), 5, MSteel);
                // Tambour de munitions extérieur et bande d'alimentation.
                m.Use(MachMech());
                Cyl(m, new Vector3(-0.1f, -0.75f, -2.25f), new Vector3(-0.1f, -0.75f, -1.55f), 0.62f, 16, MSteel);
                Cyl(m, new Vector3(-0.1f, -0.75f, -2.3f), new Vector3(-0.1f, -0.75f, -2.18f), 0.66f, 16, MDark);
                // Rotule d'épaule.
                Cyl(m, new Vector3(-0.55f, 0, 0), new Vector3(0.55f, 0, 0), 0.5f, 14, MDark);
            });
            var barrel = new Node3D { Position = WBarrelPos };
            arm.AddChild(barrel);
            _wBarrel[i] = barrel;
            Part(barrel, "barrel", m =>
            {
                m.Use(MachArmor());
                // Manchon de refroidissement rectangulaire, ajouré.
                Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 0.02f, 3.15f)), new Vector3(0.44f, 0.44f, 1.05f), MSteel, 0.08f, 5f);
                m.Use(MachMech());
                for (var s = -1; s <= 1; s += 2)
                for (var k = 0; k < 4; k++)
                    m.Box(new Transform3D(Basis.Identity, new Vector3(0.43f * s, 0.02f, 2.45f + k * 0.45f)), new Vector3(0.03f, 0.2f, 0.12f), MDark);
                // Tube, frein de bouche à évents.
                Cyl(m, new Vector3(0, 0, 4.1f), new Vector3(0, 0, 6.6f), 0.3f, 14, MDark, -1f, 0.27f);
                Cyl(m, new Vector3(0, 0, 4.15f), new Vector3(0, 0, 4.4f), 0.38f, 14, MSteel);
                Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 0, 6.75f)), new Vector3(0.45f, 0.36f, 0.32f), MSteel, 0.07f, 6f);
                for (var s = -1; s <= 1; s += 2)
                for (var k = 0; k < 2; k++)
                    m.Box(new Transform3D(Basis.Identity, new Vector3(0.45f * s, 0, 6.6f + k * 0.3f)), new Vector3(0.02f, 0.22f, 0.07f), MDark);
                m.Ellipsoid(new Vector3(0, 0, WMuzzleZ + 0.07f), new Vector3(0.22f, 0.22f, 0.02f), 2, 12, MGroove);
                Cyl(m, new Vector3(0, -0.5f, 2.2f), new Vector3(0, -0.5f, 4.6f), 0.11f, 8, MDark);
            });
            var ball = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 12, Rings = 6 },
                MaterialOverride = Mats.Glow(new Color(1f, 0.35f, 0.12f), 6f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0, 0, WMuzzleZ + 0.15f),
                Visible = false,
            };
            barrel.AddChild(ball);
            _wChargeBall[i] = ball;
        }

        // Jambes.
        for (var i = 0; i < 2; i++) AddWalkerLeg(i);

        // Trait de visée du canon en charge (repère monde).
        _wSight = new Node3D { TopLevel = true, Visible = false };
        AddChild(_wSight);
        _wSight.AddChild(BeamMesh(0.06f, new Color(1f, 0.1f, 0.05f, 0.6f), 4f, true));
        AnimateWalker(0f, 0f, 0f);
    }

    /// <summary>
    /// Torse : fuselage voûté au nez plongeant (fente de capteurs), glacis et joues de blindage, nacelles d'épaules
    /// lance-missiles reliées par des jougs, dos (réacteur, radiateurs, échappements, mât).
    /// </summary>
    private static void BuildWalkerTorso(InsectMesh m)
    {
        m.Use(MachArmor());
        var grooves = new[] { 0.295f, 0.3f, 0.305f, 0.545f, 0.55f, 0.555f };
        BodyLoft(m, -2.5f, 4.7f,
            new[] { 1.5f, 2.3f, 2.65f, 2.65f, 2.45f, 2.0f, 1.4f, 0.8f },
            new[] { 3.1f, 4.0f, 4.35f, 4.35f, 4.05f, 3.5f, 2.85f, 2.25f },
            new[] { 1.3f, 0.65f, 0.4f, 0.4f, 0.5f, 0.85f, 1.15f, 1.5f },
            Steps(16, grooves), 28, MGun, 3.8f, null, (t, th, p, c) =>
            {
                if (Mathf.Abs(t - 0.3f) < 0.004f || Mathf.Abs(t - 0.55f) < 0.004f) return MGroove;
                // Fente de capteurs sombre sur le nez.
                var mid = Mathf.Lerp(2.4f, 1.85f, Mathf.Clamp((t - 0.62f) / 0.38f, 0, 1));
                if (t > 0.64f && Mathf.Abs(p.Y - mid) < 0.16f && Mathf.Abs(p.X) > 0.25f) return MDark;
                if (p.Y < 1.0f) return MSteel;
                if (t > 0.18f && t < 0.5f && p.Y > 3.2f && Mathf.Abs(p.X) < 1.4f) return MOchre;
                return c;
            });
        // Glacis du nez, visière, joues.
        PlateAt(m, new Vector3(0, 3.2f, 3.0f), new Vector3(1.75f, 0.16f, 1.05f), MGun, 0, 0.42f, 0, 0.1f, 0.7f);
        PlateAt(m, new Vector3(0, 2.62f, 3.95f), new Vector3(1.05f, 0.12f, 0.55f), MSteel, 0, 0.62f, 0, 0.06f, 0.75f);
        // Lèvre de blindage au-dessus de la fente des capteurs et proue sous le nez : casse la rondeur vue de face.
        PlateAt(m, new Vector3(0, 2.55f, 3.35f), new Vector3(2.15f, 0.14f, 0.45f), MGun, 0, 0.08f, 0, 0.08f, 0.6f);
        PlateAt(m, new Vector3(0, 1.45f, 3.85f), new Vector3(1.0f, 0.4f, 0.8f), MSteel, 0, -0.25f, 0, 0.1f, 0.55f);
        for (var s = -1; s <= 1; s += 2)
            PlateAt(m, new Vector3(2.25f * s, 2.6f, 1.6f), new Vector3(0.14f, 1.25f, 1.1f), MGun, -0.12f * s, 0, 0.1f * s, 0.08f, 0.85f);
        for (var s = -1; s <= 1; s += 2)
        {
            PlateAt(m, new Vector3(1.75f * s, 1.55f, 2.55f), new Vector3(0.16f, 0.72f, 1.25f), MSteel, -0.32f * s, 0.12f, 0, 0.08f, 0.8f);
            // Jougs d'épaule et nacelle lance-missiles.
            m.Use(MachMech());
            Cyl(m, new Vector3(2.2f * s, 3.05f, 0.4f), new Vector3(2.75f * s, 3.05f, 0.4f), 0.75f, 16, MDark);
            m.Use(MachArmor());
            var pod = new Vector3(3.35f * s, 3.55f, 0.45f);
            Plate(m, new Transform3D(Basis.Identity, pod), new Vector3(0.95f, 0.88f, 1.75f), MGun, 0.18f, 7f, 0.82f, 0.7f);
            PlateAt(m, pod + new Vector3(0.98f * s, -0.05f, 0), new Vector3(0.12f, 0.72f, 1.5f), MSteel, 0, 0, 0.08f * s);
            PlateAt(m, pod + new Vector3(0, 0.95f, -0.35f), new Vector3(0.82f, 0.1f, 1.15f), MSteel);
            Groove(m, pod + new Vector3(0.98f * s, 0.55f, -1.4f), pod + new Vector3(0.98f * s, 0.55f, 1.4f), Vector3.Right * s);
            // Tubes lance-missiles (2 × 3) sur la face avant de la nacelle.
            m.Use(MachMech());
            for (var r = 0; r < 2; r++)
            for (var c = 0; c < 3; c++)
                m.Ellipsoid(pod + new Vector3((c - 1) * 0.5f, 0.3f - r * 0.5f, 1.53f), new Vector3(0.18f, 0.18f, 0.04f), 2, 10, MGroove);
            m.Use(MachArmor());
            // Bavette de blindage sous la nacelle (protège la rotule du bras).
            PlateAt(m, pod + new Vector3(0, -0.98f, 1.3f), new Vector3(0.8f, 0.1f, 0.5f), MSteel, 0, -0.35f);
        }
        // Arête dorsale en écailles.
        for (var k = -2; k <= 1; k++)
            PlateAt(m, new Vector3(0, 4.58f - Mathf.Abs(k + 0.5f) * 0.05f, k * 0.9f + 0.4f), new Vector3(0.55f, 0.12f, 0.4f), MSteel, 0, 0.05f * k);
        // Dos : réacteur, radiateurs, échappements.
        Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 3.0f, -2.45f)), new Vector3(1.45f, 1.15f, 0.6f), MSteel, 0.14f);
        for (var k = 0; k < 7; k++)
            m.Box(new Transform3D(Basis.Identity, new Vector3(-1.08f + k * 0.36f, 3.0f, -3.08f)), new Vector3(0.04f, 0.85f, 0.09f), MDark);
        m.Use(MachMech());
        for (var s = -1; s <= 1; s += 2)
        {
            Cyl(m, new Vector3(0.9f * s, 3.9f, -2.3f), new Vector3(1.05f * s, 5.15f, -2.85f), 0.28f, 10, MSteel);
            Cyl(m, new Vector3(1.05f * s, 5.05f, -2.82f), new Vector3(1.08f * s, 5.4f, -2.95f), 0.34f, 10, MDark);
            Cable(m, new Vector3(1.7f * s, 4.0f, -1.6f), new Vector3(2.5f * s, 3.2f, -0.9f), 0.09f, 0.3f, MDark);
            Cable(m, new Vector3(1.0f * s, 0.7f, -1.4f), new Vector3(1.9f * s, 1.5f, 0.6f), 0.08f, 0.3f, MDark);
        }
        // Mât d'antenne et capteur de toit.
        Cyl(m, new Vector3(1.2f, 4.3f, -1.0f), new Vector3(1.32f, 6.4f, -1.35f), 0.05f, 6, MDark);
        Cyl(m, new Vector3(-0.9f, 4.4f, -0.6f), new Vector3(-0.9f, 4.85f, -0.6f), 0.24f, 10, MDark);
        m.Ellipsoid(new Vector3(-0.9f, 4.95f, -0.6f), new Vector3(0.38f, 0.14f, 0.38f), 3, 12, MSteel);
        // Ventre : logement de la tourelle de menton.
        Plate(m, new Transform3D(Basis.Identity, new Vector3(0, 1.0f, 3.1f)), new Vector3(0.75f, 0.45f, 0.6f), MDark, 0.08f);
        m.Use(MachRed());
        m.Ellipsoid(new Vector3(1.32f, 6.42f, -1.36f), Vector3.One * 0.07f, 2, 6, Colors.White);
    }

    /// <summary>Jambe digitigrade : hanche (tangage) → cuisse → genou (point faible) → tibia → cheville tenue à plat → tarse et pied.</summary>
    private void AddWalkerLeg(int i)
    {
        var side = i == 0 ? 1f : -1f;
        var sideKey = side > 0 ? "R" : "L";
        var down = new Basis(Vector3.Right, Vector3.Back, Vector3.Down); // Z local le long de -Y : plaques le long de l'os
        var hip = new Node3D { Position = new Vector3(WHipX * side, 0, 0) };
        _bodyPivot.AddChild(hip);
        _wHip[i] = hip;
        Part(hip, "thigh" + sideKey, m =>
        {
            m.Mirror = side < 0;
            m.Use(MachArmor());
            // Caisson de cuisse massif, plaque extérieure et plaque avant.
            Plate(m, new Transform3D(down, new Vector3(0, -WThigh * 0.46f, 0.12f)), new Vector3(0.6f, 0.82f, WThigh * 0.47f), MSteel, 0.14f, 6f, 0.72f, 0.7f);
            Plate(m, new Transform3D(down, new Vector3(0.62f, -WThigh * 0.42f, 0.2f)), new Vector3(0.12f, 0.78f, WThigh * 0.4f), MGun, 0.08f, 8f, 0.8f, 0.75f);
            Plate(m, new Transform3D(down, new Vector3(0, -WThigh * 0.4f, 0.9f)), new Vector3(0.5f, 0.12f, WThigh * 0.38f), MGun, 0.08f, 8f, 0.7f);
            Groove(m, new Vector3(0.75f, -0.7f, 0.65f), new Vector3(0.72f, -WThigh + 1.0f, 0.5f), Vector3.Right);
            Groove(m, new Vector3(0.75f, -0.7f, -0.25f), new Vector3(0.72f, -WThigh + 1.0f, -0.2f), Vector3.Right);
            m.Use(MachMech());
            Cyl(m, new Vector3(-0.72f, 0, 0), new Vector3(0.72f, 0, 0), 0.95f, 18, MDark);
            // Vérins de cuisse (arrière) et faisceau de câbles.
            Piston(m, new Vector3(0.25f, -0.35f, -0.95f), new Vector3(0.25f, -WThigh + 0.6f, -0.65f), 0.22f, MSteel);
            Piston(m, new Vector3(-0.25f, -0.35f, -0.95f), new Vector3(-0.25f, -WThigh + 0.6f, -0.65f), 0.22f, MSteel);
            Cable(m, new Vector3(-0.55f, -0.4f, 0.75f), new Vector3(-0.5f, -WThigh + 0.8f, 0.7f), 0.07f, 0.2f, MDark);
        });
        var knee = new Node3D { Position = new Vector3(0, -WThigh, 0) };
        hip.AddChild(knee);
        _wKnee[i] = knee;
        Part(knee, "shin" + sideKey, m =>
        {
            m.Mirror = side < 0;
            m.Use(MachArmor());
            // Genouillère bombée sur l'avant de l'articulation.
            m.Ellipsoid(new Vector3(0, -0.25f, 0.42f), new Vector3(0.66f, 0.8f, 0.42f), 5, 12, MGun);
            // Tibia : poutre blindée, plaque de protection sur la face avant (vers le bas et l'avant).
            Plate(m, new Transform3D(down, new Vector3(0, -WShin * 0.52f, 0)), new Vector3(0.46f, 0.55f, WShin * 0.42f), MSteel, 0.1f, 7f, 0.75f, 0.75f);
            Plate(m, new Transform3D(down, new Vector3(0, -WShin * 0.47f, 0.52f)), new Vector3(0.5f, 0.12f, WShin * 0.36f), MGun, 0.08f, 8f, 0.7f);
            Groove(m, new Vector3(0, -1.3f, 0.66f), new Vector3(0, -WShin + 1.1f, 0.58f), Vector3.Back);
            m.Use(MachMech());
            // Genou : actionneur en retrait entre les flasques, couronne de boulons.
            Cyl(m, new Vector3(-0.62f, 0, 0), new Vector3(0.62f, 0, 0), 0.62f, 18, MDark);
            Cyl(m, new Vector3(0.62f, 0, 0), new Vector3(0.72f, 0, 0), 0.5f, 16, MBare);
            for (var k = 0; k < 8; k++)
            {
                var a = k * Mathf.Tau / 8;
                m.Box(new Transform3D(Basis.Identity, new Vector3(0.73f, Mathf.Sin(a) * 0.38f, Mathf.Cos(a) * 0.38f)), new Vector3(0.03f, 0.06f, 0.06f), MDark);
            }
            Piston(m, new Vector3(0, -0.5f, -0.65f), new Vector3(0, -WShin + 0.55f, -0.45f), 0.2f, MSteel);
            Cyl(m, new Vector3(-0.5f, -WShin, 0), new Vector3(0.5f, -WShin, 0), 0.48f, 14, MDark);
            // Chauffe des actionneurs : le point faible luit dans la fente du genou.
            m.Use(MachHot());
            m.Torus(new Vector3(-0.42f, 0, 0), new Basis(Vector3.Back, Mathf.Pi / 2), 0.6f, 0.07f, 20, 4, Colors.White);
            m.Torus(new Vector3(0.42f, 0, 0), new Basis(Vector3.Back, Mathf.Pi / 2), 0.6f, 0.07f, 20, 4, Colors.White);
        });
        var ankle = new Node3D { Position = new Vector3(0, -WShin, 0) };
        knee.AddChild(ankle);
        _wAnkleN[i] = ankle;
        Part(ankle, "foot" + sideKey, m =>
        {
            m.Mirror = side < 0;
            m.Use(MachArmor());
            // Tarse vertical (amortisseur) puis pied à trois orteils et ergot.
            Plate(m, new Transform3D(down, new Vector3(0, -WAnkle * 0.42f, -0.05f)), new Vector3(0.38f, 0.42f, WAnkle * 0.42f), MSteel, 0.08f);
            Plate(m, new Transform3D(Basis.Identity, new Vector3(0, -WAnkle + 0.34f, 0.35f)), new Vector3(0.8f, 0.34f, 0.9f), MGun, 0.14f, 7f, 0.8f, 0.7f);
            foreach (var (yaw, len) in new[] { (0f, 2.3f), (0.45f, 1.95f), (-0.45f, 1.95f) })
            {
                var b = new Basis(Vector3.Up, yaw);
                var c = new Vector3(0, -WAnkle + 0.24f, 0.55f) + b * new Vector3(0, 0, len * 0.55f);
                Plate(m, new Transform3D(b * new Basis(Vector3.Right, 0.1f), c), new Vector3(0.3f, 0.24f, len * 0.5f), MSteel, 0.08f, 7f, 0.6f, 0.55f);
                m.Use(MachMech());
                m.Cone(c + b * new Vector3(0, -0.08f, len * 0.45f), c + b * new Vector3(0, -0.22f, len * 0.7f), 0.18f, 6, MDark, true);
                m.Use(MachArmor());
            }
            Plate(m, new Transform3D(new Basis(Vector3.Right, -0.35f), new Vector3(0, -WAnkle + 0.35f, -0.95f)), new Vector3(0.25f, 0.2f, 0.6f), MSteel, 0.06f, 8f, 0.6f);
            m.Use(MachMech());
            m.Ellipsoid(Vector3.Zero, Vector3.One * 0.55f, 4, 10, MDark);
            Piston(m, new Vector3(0, -0.1f, 0.45f), new Vector3(0, -WAnkle + 0.55f, 0.75f), 0.13f, MSteel);
        });
        _wPrevU[i] = i * 0.5f;
    }

    // ---------------- Animation ----------------

    /// <summary>Démarche bipède, visée du torse et des canons, charge, recul.</summary>
    public void AnimateWalker(float dt, float speed, float attack)
    {
        _t += dt;
        for (var i = 0; i < 2; i++)
        {
            if (_wCharge[i] >= 0) _wCharge[i] = Mathf.Min(1f, _wCharge[i] + dt / 1.3f);
            _wRecoil[i] = Mathf.Max(0f, _wRecoil[i] - dt * 2.2f);
            var g = Mathf.Max(_wCharge[i], 0f);
            var ball = _wChargeBall[i];
            ball.Visible = !_dead && (g > 0f || _wRecoil[i] > 0.75f);
            ball.Scale = Vector3.One * Mathf.Max(0.05f, g * (0.9f + 0.15f * Mathf.Sin(_t * 30f)) + Mathf.Max(0, _wRecoil[i] - 0.75f) * 6f);
        }
        if (_dead) { _wSight.Visible = false; return; }

        // Visée : rotation du torse (±70°), tangage des canons, la tourelle de menton suit.
        float yawT = 0, pitchT = 0;
        if (_mHasAim)
        {
            var local = _bodyPivot.GlobalTransform.AffineInverse() * _mAim;
            yawT = Mathf.Clamp(Mathf.Atan2(local.X, local.Z), -1.2f, 1.2f);
        }
        _wYaw = Mathf.LerpAngle(_wYaw, yawT, Mathf.Clamp(dt * 1.6f, 0, 1));
        _wWaist.Rotation = new Vector3(0, _wYaw, 0);
        for (var i = 0; i < 2; i++)
        {
            if (_mHasAim)
            {
                var sh = _wArm[i].GlobalPosition;
                var to = _mAim - sh;
                pitchT = Mathf.Clamp(Mathf.Atan2(to.Y + 0.8f, new Vector2(to.X, to.Z).Length()), -0.55f, 0.45f);
            }
            var arm = _wArm[i];
            arm.Rotation = new Vector3(Mathf.Lerp(arm.Rotation.X, -pitchT - _wRecoil[i] * 0.12f, Mathf.Clamp(dt * 2.5f, 0, 1)), 0, 0);
            _wBarrel[i].Position = WBarrelPos + new Vector3(0, 0, -1.1f * _wRecoil[i] * _wRecoil[i]);
        }
        if (_mHasAim)
        {
            var hl = _wWaist.GlobalTransform.AffineInverse() * _mAim - _wHead.Position;
            _wHead.Rotation = new Vector3(Mathf.Clamp(-Mathf.Atan2(hl.Y, new Vector2(hl.X, hl.Z).Length()), -0.35f, 0.6f), Mathf.Clamp(Mathf.Atan2(hl.X, hl.Z), -0.7f, 0.7f), 0);
        }

        // Marche : appui 62 % du cycle, transfert 38 % ; la cadence suit la vitesse (pieds sans glissement à pleine allure).
        // Vitesse signée : négative = marche arrière (le cycle se déroule à l'envers).
        _wAmp = Mathf.MoveToward(_wAmp, Mathf.Clamp(Mathf.Abs(speed) / 4.2f, 0f, 1f), dt * 1.5f);
        var amp = _wAmp;
        _wGait += dt * WStance * 4.2f / WStride * Mathf.Min(1f, amp * 5f) * (speed < -0.05f ? -1f : 1f);
        var g2 = _wGait;
        var sway = Mathf.Sin(g2 * Mathf.Tau) * 0.32f * amp;
        var bob = -0.32f * amp * (0.5f + 0.5f * Mathf.Cos(g2 * Mathf.Tau * 2f));
        var breathe = Mathf.Sin(_t * 1.1f) * 0.04f;
        _bodyPivot.Position = new Vector3(sway, WHipY + bob + breathe, 0);
        _bodyPivot.Rotation = new Vector3(0.05f * amp + attack * 0.03f, Mathf.Sin(g2 * Mathf.Tau) * 0.05f * amp, -sway * 0.05f);
        var bodyInv = _bodyPivot.Transform;
        for (var i = 0; i < 2; i++)
        {
            var u = Mathf.PosMod(g2 + i * 0.5f, 1f);
            var (fa, lift) = GaitCurve(u, WStance);
            // Pied au sol, dans le repère du modèle, puis ramené dans celui du bassin.
            var foot = new Vector3(WHipX * (i == 0 ? 1 : -1) * 0.98f, WAnkle + lift * 1.4f * Mathf.Min(1f, amp * 3f), WAnkleZ + fa * WStride * 0.5f * amp);
            var hipModel = bodyInv * _wHip[i].Position;
            var local = bodyInv.Basis.Inverse() * (foot - hipModel);
            PlanarIk(WThigh, WShin, local.Y, local.Z, true, out var th, out var sh);
            _wHip[i].Rotation = new Vector3(-th, 0, 0);
            _wKnee[i].Rotation = new Vector3(-(sh - th), 0, 0);
            // Cheville tenue à plat (pointe un peu basse quand le pied se lève), compense le tangage du bassin.
            _wAnkleN[i].Rotation = new Vector3(sh - _bodyPivot.Rotation.X + lift * 0.3f * amp, 0, 0);
            if (amp > 0.25f && FootDown(_wPrevU[i], u, WStance)) Stomp?.Invoke(WalkerFoot(i) with { Y = 0f });
            _wPrevU[i] = u;
        }

        // Trait de visée pendant la charge.
        var aimArm = _wSightArm;
        if (aimArm >= 0 && _wCharge[aimArm] >= 0f)
        {
            var from = WalkerMuzzle(aimArm);
            var len = from.DistanceTo(_wSightTarget);
            _wSight.Visible = len > 1f;
            if (len > 1f)
            {
                var flick = 0.7f + 0.3f * Mathf.Sin(_t * 40f);
                _wSight.GlobalTransform = new Transform3D(Basis.LookingAt(_wSightTarget - from, Mathf.Abs((_wSightTarget - from).Normalized().Y) > 0.98f ? Vector3.Right : Vector3.Up)
                     * Basis.FromScale(new Vector3(flick, flick, len)), from);
            }
        }
        else _wSight.Visible = false;
    }

    /// <summary>Mort : les genoux cèdent, le bassin s'effondre, le torse bascule en avant, les capteurs s'éteignent.</summary>
    public void WalkerDeath()
    {
        _dead = true;
        _wSight.Visible = false;
        var tw = CreateTween().SetParallel();
        const float d = 0.8f, len = 2.2f;
        tw.TweenProperty(_bodyPivot, "position", new Vector3(0.6f, 2.4f, 1.2f), len).SetDelay(d).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        tw.TweenProperty(_bodyPivot, "rotation", new Vector3(0.42f, 0.15f, -0.22f), len).SetDelay(d).SetEase(Tween.EaseType.In);
        tw.TweenProperty(_wWaist, "rotation", new Vector3(0.35f, _wYaw * 0.5f, 0.1f), len * 0.8f).SetDelay(d + 0.3f);
        for (var i = 0; i < 2; i++)
        {
            tw.TweenProperty(_wHip[i], "rotation", new Vector3(-1.65f + i * 0.25f, 0.1f * (i == 0 ? 1 : -1), 0), len).SetDelay(d);
            tw.TweenProperty(_wKnee[i], "rotation", new Vector3(2.3f - i * 0.2f, 0, 0), len).SetDelay(d);
            tw.TweenProperty(_wArm[i], "rotation", new Vector3(0.6f, 0, 0.2f * (i == 0 ? -1 : 1)), len).SetDelay(d + 0.2f);
        }
        tw.Chain().TweenCallback(Callable.From(() => { if (IsInstanceValid(_wEyes)) _wEyes.MaterialOverride = Mats.Painted(new Color(0.08f, 0.02f, 0.02f), 0.3f, 0.4f); }));
    }
}
