using System;
using Godot;

namespace Rempart;

/// <summary>
/// Animation du titan reptilien : armature tenue côté C# (43 os), pieds posés au sol en repère monde (pas déclenchés
/// quand le pied s'écarte de sa place, un seul pied en l'air à la fois, cible du pas anticipée et recalée sur le sol),
/// jambes par cinématique inverse à deux os (genou vers l'avant), bassin qui se balance vers le pied d'appui et
/// s'affaisse à chaque pose, queue en chaîne de ressorts amortis (inertie aux virages, retard qui se propage, coup de
/// fouet du balayage), cou et tête qui suivent la cible, mâchoire, rugissement, titubation, chute à la mort.
/// </summary>
public partial class BugModel
{
    private TitanShared _tSh;
    private MeshInstance3D _tSkinMi;
    private OmniLight3D _tMouthLight;
    private readonly Transform3D[] _tX = new Transform3D[TBCount];

    // Pieds (repère monde) et pas en cours.
    private readonly Vector3[] _tFootW = new Vector3[2];
    private readonly float[] _tFootYaw = new float[2];
    private int _tStepLeg = -1, _tLastLeg = 1;
    private float _tStepK, _tStepDur = 1.1f, _tStepYaw0, _tStepYaw1;
    private Vector3 _tStepFrom, _tStepTo;
    private bool _tInit;
    private Vector3 _tLastRoot, _tVel;
    private float _tLastYaw, _tYawRate, _tLand, _tLandV, _tSway;

    // Queue, cou, tête.
    private readonly float[] _tTailA = new float[TTailN], _tTailV = new float[TTailN], _tTailP = new float[TTailN], _tTailPV = new float[TTailN];
    private float _tLookYaw, _tLookPitch, _tJawS, _tRearS, _tStagS, _tChargeS, _tChargeSent = -1f;
    private Vector3 _tLook;
    private bool _tHasLook;

    // Plaques.
    private readonly bool[] _tPlateGone = new bool[TPlates];
    private readonly float[] _tWoundT = new float[TPlates];

    // Mort.
    private float _tDeathT = -1f, _tDeathSide = 1f;
    private readonly Vector3[] _tDeathFoot = new Vector3[2];

    /// <summary>Consignes de l'IA (tous les pairs) : ouverture de la mâchoire, cabrage (rugissement), titubation, lueur de la gorge (0..1).</summary>
    public float TitanJaw, TitanRear, TitanStagger, TitanCharge;
    /// <summary>Balayage de la queue : angle visé (radians, signe = côté), réparti sur la chaîne.</summary>
    public float TitanTailSwing;
    /// <summary>Souffle en cours (lueur de la gueule, mâchoire tenue ouverte).</summary>
    public bool TitanFiring;

    public bool IsTitan => Type == EnemyType.Titan;

    /// <summary>Nombre de plaques d'armure.</summary>
    public const int TitanPlateCount = TPlates;

    // ---------------- Construction ----------------

    private void BuildTitan()
    {
        _tSh = TitanBuild();
        _tSkinMi = new MeshInstance3D
        {
            Name = "TitanSkin",
            Mesh = _tSh.Mesh,
            Skeleton = new NodePath(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        AddChild(_tSkinMi);
        _skeleton = RenderingServer.SkeletonCreate();
        RenderingServer.SkeletonAllocateData(_skeleton, TBCount);
        _skinMeshes = new[] { _tSkinMi };
        _tMouthLight = new OmniLight3D { LightColor = new Color(1f, 0.45f, 0.12f), LightEnergy = 0f, OmniRange = 26f, Visible = false, ShadowEnabled = false };
        AddChild(_tMouthLight);
        for (var i = 0; i < TBCount; i++) _tX[i] = new Transform3D(Basis.Identity, _tSh.Pivot[i]);
        TitanSync();
    }

    /// <summary>Nombre de triangles du maillage partagé (diagnostic).</summary>
    public static int TitanTriangles => TitanBuild().Triangles;

    // ---------------- Accès (tous les pairs) ----------------

    /// <summary>Oriente le cou et la tête vers un point monde.</summary>
    public void TitanAim(Vector3 world)
    {
        _tLook = world;
        _tHasLook = true;
    }

    private Transform3D TitanBoneModel(int b) => _tX[b];

    /// <summary>Point de pose de liaison porté par un os, en repère monde.</summary>
    public Vector3 TitanPoint(int bone, Vector3 bind) => GlobalTransform * (_tX[bone] * (bind - _tSh.Pivot[bone]));

    /// <summary>Repère monde d'un os (base orthonormée).</summary>
    public Basis TitanBasis(int bone) => (GlobalBasis * _tX[bone].Basis).Orthonormalized();

    /// <summary>Sortie du souffle (entre les mâchoires), repère monde.</summary>
    public Vector3 TitanMouth => TitanPoint(TBHead, TMouth);

    /// <summary>Direction du museau (repère monde).</summary>
    public Vector3 TitanSnoutDir => (TitanBasis(TBHead) * new Vector3(0, -0.08f, 1f)).Normalized();

    /// <summary>Centre du torse (suit l'animation).</summary>
    public Vector3 TitanChest => TitanPoint(TBBelly, (TJ[10] + TJ[11]) * 0.5f);

    /// <summary>Centre du crâne.</summary>
    public Vector3 TitanSkull => TitanPoint(TBHead, TSkull);

    /// <summary>Point de la queue : 0 = base … 8 = pointe.</summary>
    public Vector3 TitanTail(int k) => k >= TTailN ? TitanPoint(TBTail + TTailN - 1, TJ[0]) : TitanPoint(TBTail + k, TJ[8 - k]);

    /// <summary>Centre d'une plaque (ou de la plaie dessous), repère monde.</summary>
    public Vector3 TitanPlatePoint(int i) => TitanPoint(_tSh.PlateParent[i], _tSh.PlateCenter[i]);

    public float TitanPlateRadius(int i) => _tSh.PlateRadius[i];

    public bool TitanPlateGone(int i) => _tPlateGone[i];

    /// <summary>Pied posé (ou en l'air) en repère monde.</summary>
    public Vector3 TitanFoot(int i) => _tStepLeg == i ? StepPos() : _tFootW[i];

    /// <summary>
    /// Formes de collision, dans le repère de leur os : (os, centre en pose de liaison, axe des capsules ou zéro, rayon,
    /// demi-longueur). Ordre = indices des formes : 0 torse, 1 tête (point faible), 2 bassin, 3 cou, 4-7 queue,
    /// 8-9 cuisses, 10-11 tibias, 12-13 pieds, 14-15 bras, 16-23 plaques.
    /// </summary>
    public static (int Bone, Vector3 Center, Vector3 Axis, float Radius, float Half)[] TitanShapes()
    {
        TitanCurve();
        var l = new System.Collections.Generic.List<(int, Vector3, Vector3, float, float)>
        {
            (TBBelly, (TJ[10] + TJ[11]) * 0.5f, (TJ[11] - TJ[10]).Normalized(), 9.2f * TS, 5f * TS),
            (TBHead, TSkull, Vector3.Back, 4.6f * TS, 3.4f * TS),
            (TBPelvis, TJ[9] + new Vector3(0, -0.5f, -1f) * TS, (TJ[9] - TJ[8]).Normalized(), 8.4f * TS, 3f * TS),
            (TBNeck1, (TJ[12] + TJ[14]) * 0.5f, (TJ[14] - TJ[12]).Normalized(), 6.0f * TS, 3f * TS),
            (TBTail, (TJ[8] + TJ[6]) * 0.5f, (TJ[8] - TJ[6]).Normalized(), 5.8f * TS, 4f * TS),
            (TBTail + 2, (TJ[6] + TJ[4]) * 0.5f, (TJ[6] - TJ[4]).Normalized(), 4.2f * TS, 4.5f * TS),
            (TBTail + 4, (TJ[4] + TJ[2]) * 0.5f, (TJ[4] - TJ[2]).Normalized(), 2.8f * TS, 5f * TS),
            (TBTail + 6, (TJ[2] + TJ[0]) * 0.5f, (TJ[2] - TJ[0]).Normalized(), 1.5f * TS, 5f * TS),
        };
        for (var i = 0; i < 2; i++) l.Add((TThigh(i), (TiHip(i) + TiKnee(i)) * 0.5f, (TiKnee(i) - TiHip(i)).Normalized(), 5.4f * TS, 3.2f * TS));
        for (var i = 0; i < 2; i++) l.Add((TShin(i), (TiKnee(i) + TiAnkle(i)) * 0.5f, (TiAnkle(i) - TiKnee(i)).Normalized(), 3.0f * TS, 3.2f * TS));
        for (var i = 0; i < 2; i++) l.Add((TFoot(i), TiBall(i) + new Vector3(0, 0.3f, 1.2f) * TS, Vector3.Zero, 3.2f * TS, 0f));
        for (var i = 0; i < 2; i++) l.Add((TUpper(i), (TiShoulder(i) + TiWrist(i)) * 0.5f, (TiWrist(i) - TiShoulder(i)).Normalized(), 2.2f * TS, 3.5f * TS));
        var sh = TitanBuild();
        for (var i = 0; i < TPlates; i++) l.Add((sh.PlateParent[i], sh.PlateCenter[i], Vector3.Zero, sh.PlateRadius[i], 0f));
        return l.ToArray();
    }

    /// <summary>Repère monde d'une forme de collision (capsule : axe Y le long de <paramref name="axis"/>).</summary>
    public Transform3D TitanShapeXf(int bone, Vector3 center, Vector3 axis)
    {
        var origin = TitanPoint(bone, center);
        if (axis == Vector3.Zero) return new Transform3D(Basis.Identity, origin);
        var y = (TitanBasis(bone) * axis).Normalized();
        var x = y.Cross(Mathf.Abs(y.Y) > 0.9f ? Vector3.Forward : Vector3.Up).Normalized();
        var z = x.Cross(y).Normalized();
        return new Transform3D(new Basis(x, y, z), origin);
    }

    /// <summary>Plaque brisée (tous les pairs) : elle disparaît, la plaie s'ouvre ; renvoie le maillage et le repère monde du débris.</summary>
    public (ArrayMesh Mesh, Transform3D Xf) TitanBreakPlate(int i)
    {
        _tPlateGone[i] = true;
        _tWoundT[i] = 0f;
        // Le maillage du débris est centré sur le point de la peau sous la plaque (voir TitanBuild).
        var parent = _tSh.PlateParent[i];
        return (_tSh.Debris[i], new Transform3D(TitanBasis(parent), TitanPoint(parent, _tSh.PlateSkin[i])));
    }

    // ---------------- Animation ----------------

    /// <summary>Anime le titan (vitesse en m/s, attaque 0..1) et envoie les os au squelette de rendu.</summary>
    public void AnimateTitan(float dt, float speed, float attack)
    {
        if (_tSh == null || dt <= 0f) return;
        _t += dt;
        var root = GetParent<Node3D>().GlobalTransform;
        var rp = root.Origin;
        var yaw = Mathf.Atan2(root.Basis.Z.X, root.Basis.Z.Z);
        if (!_tInit)
        {
            _tInit = true;
            _tLastRoot = rp;
            _tLastYaw = yaw;
            for (var i = 0; i < 2; i++)
            {
                _tFootW[i] = root * TiSole(i);
                _tFootW[i].Y = 0f;
                _tFootYaw[i] = yaw;
            }
        }
        var vel = (rp - _tLastRoot) / dt;
        vel.Y = 0;
        if (vel.LengthSquared() > 400f) vel = Vector3.Zero; // téléportation (tests)
        _tVel = _tVel.Lerp(vel, Mathf.Clamp(dt * 3f, 0, 1));
        var yr = Mathf.Wrap(yaw - _tLastYaw, -Mathf.Pi, Mathf.Pi) / dt;
        _tYawRate = Mathf.Lerp(_tYawRate, Mathf.Clamp(yr, -1f, 1f), Mathf.Clamp(dt * 3f, 0, 1));
        _tLastRoot = rp;
        _tLastYaw = yaw;

        // Consignes lissées (gros animal : rien n'est instantané).
        _tJawS = Mathf.Lerp(_tJawS, Mathf.Max(TitanJaw, TitanFiring ? 0.55f : 0f), Mathf.Clamp(dt * 5f, 0, 1));
        _tRearS = Mathf.Lerp(_tRearS, TitanRear, Mathf.Clamp(dt * 2.2f, 0, 1));
        _tStagS = Mathf.Lerp(_tStagS, TitanStagger, Mathf.Clamp(dt * (TitanStagger > _tStagS ? 9f : 2.5f), 0, 1));
        _tChargeS = Mathf.Lerp(_tChargeS, TitanFiring ? 1f : TitanCharge, Mathf.Clamp(dt * 4f, 0, 1));

        if (_tDeathT >= 0f) _tDeathT += dt;
        else TitanGait(dt, root, yaw);
        TitanPose(dt, root, yaw, attack);
        TitanSync();
    }

    private Vector3 StepPos()
    {
        var k = Mathf.Clamp(_tStepK, 0f, 1f);
        var e = k * k * (3f - 2f * k);
        var p = _tStepFrom.Lerp(_tStepTo, e);
        var len = _tStepFrom.DistanceTo(_tStepTo);
        return p + Vector3.Up * (Mathf.Sin(k * Mathf.Pi) * (2.2f * TS + len * 0.18f));
    }

    /// <summary>Pas : un pied à la fois, déclenché quand il s'écarte trop de sa place (marche, virage), cible anticipée.</summary>
    private void TitanGait(float dt, Transform3D root, float yaw)
    {
        var spd = _tVel.Length();
        var fwd = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        var lead = Mathf.Clamp(_tVel.Dot(fwd) * 0.55f, -2.5f, 4.5f);
        Span<Vector3> home = stackalloc Vector3[2];
        for (var i = 0; i < 2; i++)
        {
            home[i] = root * (TiSole(i) + new Vector3(0, 0, lead));
            home[i].Y = 0f;
        }
        if (_tStepLeg >= 0)
        {
            _tStepK += dt / _tStepDur;
            if (_tStepK >= 1f)
            {
                var leg = _tStepLeg;
                _tFootW[leg] = _tStepTo;
                _tFootYaw[leg] = _tStepYaw1;
                _tStepLeg = -1;
                _tLastLeg = leg;
                _tLandV -= 2.6f;
                Stomp?.Invoke(_tStepTo);
            }
            return;
        }
        var moving = spd > 0.25f || Mathf.Abs(_tYawRate) > 0.04f;
        var thr = moving ? 1.3f * TS : 3.2f * TS;
        var pick = -1;
        var worst = 0f;
        for (var i = 0; i < 2; i++)
        {
            var d = new Vector2(_tFootW[i].X - home[i].X, _tFootW[i].Z - home[i].Z).Length() + Mathf.Abs(Mathf.Wrap(_tFootYaw[i] - yaw, -Mathf.Pi, Mathf.Pi)) * 5f;
            // Alternance : le pied qui vient de se poser attend que l'autre ait bougé (sauf gros écart).
            if (i == _tLastLeg && moving) d *= 0.55f;
            if (d > thr && d > worst) { worst = d; pick = i; }
        }
        if (pick < 0) return;
        _tStepLeg = pick;
        _tStepK = 0f;
        _tStepDur = Mathf.Clamp(1.3f - spd * 0.075f, 0.72f, 1.3f);
        _tStepFrom = _tFootW[pick];
        var to = home[pick] + _tVel * (_tStepDur * 0.5f);
        to.Y = GroundAt(to);
        _tStepTo = to;
        _tStepYaw0 = _tFootYaw[pick];
        _tStepYaw1 = yaw;
    }

    /// <summary>Hauteur du sol sous un pied (gravats compris, jamais un toit d'immeuble encore debout).</summary>
    private float GroundAt(Vector3 p)
    {
        if (!IsInsideTree()) return 0f;
        var q = PhysicsRayQueryParameters3D.Create(new Vector3(p.X, 12f, p.Z), new Vector3(p.X, -2f, p.Z), 1u);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        if (hit.Count == 0 || hit["collider"].AsGodotObject() is Building) return 0f;
        return Mathf.Clamp(((Vector3)hit["position"]).Y, -4f, 7f);
    }

    /// <summary>Basis alignant la direction de liaison <paramref name="vb"/> sur <paramref name="vp"/> (torsion réglée par les références).</summary>
    private static Basis TAlign(Vector3 vb, Vector3 rb, Vector3 vp, Vector3 rp)
    {
        static Basis F(Vector3 d, Vector3 r)
        {
            var z = d.Normalized();
            var x = (r - z * r.Dot(z));
            if (x.LengthSquared() < 1e-6f) x = Vector3.Up.Cross(z);
            x = x.Normalized();
            return new Basis(x, z.Cross(x), z);
        }
        return F(vp, rp) * F(vb, rb).Transposed();
    }

    /// <summary>Cinématique inverse à deux os : genou du côté du pôle ; renvoie genou et cheville atteinte.</summary>
    private static (Vector3 Knee, Vector3 Ankle) TwoBone(Vector3 a, Vector3 c, float l1, float l2, Vector3 pole)
    {
        var d = c - a;
        var len = d.Length();
        var dir = len > 1e-4f ? d / len : Vector3.Down;
        len = Mathf.Clamp(len, Mathf.Abs(l1 - l2) + 0.05f, l1 + l2 - 0.05f);
        var x = (l1 * l1 + len * len - l2 * l2) / (2f * l1 * len);
        var along = l1 * x;
        var h = Mathf.Sqrt(Mathf.Max(l1 * l1 - along * along, 0f));
        var pv = pole - dir * pole.Dot(dir);
        pv = pv.LengthSquared() > 1e-6f ? pv.Normalized() : Vector3.Back;
        return (a + dir * along + pv * h, a + dir * len);
    }

    /// <summary>Pose de toute l'armature (repère du modèle).</summary>
    private void TitanPose(float dt, Transform3D root, float yaw, float attack)
    {
        var piv = _tSh.Pivot;
        var dead = _tDeathT >= 0f;
        // --- Bassin : balancement vers le pied d'appui, montée pendant le pas, affaissement à la pose. ---
        _tLandV += (-_tLand * 38f - _tLandV * 8f) * dt;
        _tLand += _tLandV * dt;
        var inv = GlobalTransform.AffineInverse();
        var k = _tStepLeg >= 0 ? Mathf.Clamp(_tStepK, 0, 1) : 0f;
        var swayT = 0f;
        if (_tStepLeg >= 0) swayT = (inv * _tFootW[1 - _tStepLeg]).X * 0.2f * Mathf.Sin(k * Mathf.Pi);
        _tSway = Mathf.Lerp(_tSway, swayT, Mathf.Clamp(dt * 3f, 0, 1));
        var spd = _tVel.Length();
        var fh = 0f;
        if (!dead) fh = 0.5f * ((inv * TitanFoot(0)).Y + (inv * TitanFoot(1)).Y) * 0.6f;
        var bob = (_tStepLeg >= 0 ? Mathf.Sin(k * Mathf.Pi) * 0.45f * TS : 0f) + _tLand * TS + fh + Mathf.Sin(_t * 0.9f) * 0.12f;
        var twist = _tStepLeg >= 0 ? (_tStepLeg == 0 ? 1f : -1f) * 0.045f * Mathf.Sin(k * Mathf.Pi) : 0f;
        var pitch = 0.025f * spd - _tRearS * 0.14f + _tStagS * 0.08f + attack * 0.02f;
        var roll = -_tSway * 0.012f + _tStagS * 0.06f * Mathf.Sin(_t * 7f);
        var rp = Basis.FromEuler(new Vector3(pitch, twist, roll));
        _tX[TBPelvis] = new Transform3D(rp, piv[TBPelvis] + new Vector3(_tSway, bob, 0));

        // --- Regard : lacet et tangage voulus, répartis sur le cou et la tête. ---
        var lookYaw = 0f;
        var lookPitch = 0.05f;
        if (_tHasLook && !dead)
        {
            var headBase = _tX[TBPelvis] * (piv[TBHead] - piv[TBPelvis]);
            var d = inv * _tLook - headBase;
            lookYaw = Mathf.Clamp(Mathf.Atan2(d.X, d.Z), -1.05f, 1.05f);
            lookPitch = Mathf.Clamp(Mathf.Atan2(-d.Y, new Vector2(d.X, d.Z).Length()) - 0.05f, -0.5f, 0.75f);
        }
        _tLookYaw = Mathf.LerpAngle(_tLookYaw, lookYaw, Mathf.Clamp(dt * (TitanFiring ? 2.2f : 1.2f), 0, 1));
        _tLookPitch = Mathf.Lerp(_tLookPitch, lookPitch, Mathf.Clamp(dt * (TitanFiring ? 2.2f : 1.2f), 0, 1));
        var rear = _tRearS;
        var stag = _tStagS;
        var breathe = Mathf.Sin(_t * 0.9f) * 0.012f;
        var stagYaw = stag * 0.18f * Mathf.Sin(_t * 5.3f);
        var deathK = dead ? Mathf.Clamp((_tDeathT - 1.6f) / 2.4f, 0f, 1f) : 0f;
        var deathRoar = dead ? Mathf.Clamp(_tDeathT / 0.6f, 0f, 1f) * (1f - deathK) : 0f;
        rear = Mathf.Max(rear, deathRoar);
        Transform3D Chain(int parent, int bone, Vector3 euler) => _tX[parent] * new Transform3D(Basis.FromEuler(euler), piv[bone] - piv[parent]);
        _tX[TBBelly] = Chain(TBPelvis, TBBelly, new Vector3(breathe - rear * 0.06f, _tLookYaw * 0.08f, 0));
        _tX[TBChest] = Chain(TBBelly, TBChest, new Vector3(-breathe - rear * 0.1f + stag * 0.1f, _tLookYaw * 0.12f + stagYaw, stag * 0.05f));
        _tX[TBNeck1] = Chain(TBChest, TBNeck1, new Vector3(_tLookPitch * 0.22f - rear * 0.32f + stag * 0.18f + deathK * 0.4f, _tLookYaw * 0.25f, 0));
        _tX[TBNeck2] = Chain(TBNeck1, TBNeck2, new Vector3(_tLookPitch * 0.3f - rear * 0.25f + deathK * 0.3f, _tLookYaw * 0.25f - stagYaw * 0.5f, 0));
        _tX[TBHead] = Chain(TBNeck2, TBHead, new Vector3(_tLookPitch * 0.48f - rear * 0.38f + stag * 0.2f, _tLookYaw * 0.3f, stag * 0.12f * Mathf.Sin(_t * 4f)));
        var jaw = Mathf.Max(_tJawS, rear * 0.85f) + (dead && deathK >= 1f ? 0.25f : 0f);
        _tX[TBJaw] = Chain(TBHead, TBJaw, new Vector3(jaw * 0.62f + Mathf.Sin(_t * 1.3f) * 0.01f, 0, 0));

        // --- Queue : ressorts amortis (inertie aux virages, coup de fouet du balayage), propagation vers la pointe. ---
        float[] swingW = { 0.26f, 0.21f, 0.16f, 0.12f, 0.09f, 0.07f, 0.05f, 0.04f };
        var prevA = 0f;
        for (var t = 0; t < TTailN; t++)
        {
            var idle = Mathf.Sin(_t * 0.75f - t * 0.6f) * 0.045f * (0.5f + t * 0.12f);
            var inertia = -_tYawRate * 0.42f * (0.25f + t * 0.1f);
            var target = dead ? 0.05f * Mathf.Sin(t) : idle + inertia + TitanTailSwing * swingW[t] + prevA * 0.22f - _tSway * 0.01f;
            var kk = 16f - t * 1.2f;
            _tTailV[t] += ((target - _tTailA[t]) * kk - _tTailV[t] * (3.2f + t * 0.1f)) * dt;
            _tTailA[t] += _tTailV[t] * dt;
            var pT = dead ? 0.05f + deathK * 0.06f : -0.025f + Mathf.Sin(_t * 0.6f - t * 0.5f) * 0.012f - _tLandV * 0.004f * t + stag * 0.02f;
            _tTailPV[t] += ((pT - _tTailP[t]) * 10f - _tTailPV[t] * 3.5f) * dt;
            _tTailP[t] += _tTailPV[t] * dt;
            prevA = _tTailA[t];
            var parent = t == 0 ? TBPelvis : TBTail + t - 1;
            _tX[TBTail + t] = Chain(parent, TBTail + t, new Vector3(_tTailP[t], _tTailA[t], 0));
        }

        // --- Jambes : cinématique inverse vers les pieds posés (repère monde), pieds à plat ou basculés pendant le pas. ---
        for (var i = 0; i < 2; i++)
        {
            var hip = _tX[TBPelvis] * (piv[TThigh(i)] - piv[TBPelvis]);
            Vector3 footL;
            float fyaw, fpitch = 0f;
            if (dead)
            {
                footL = _tDeathFoot[i];
                fyaw = 0f;
                fpitch = deathK * 0.6f;
            }
            else
            {
                footL = inv * TitanFoot(i);
                var fy = _tStepLeg == i ? Mathf.LerpAngle(_tStepYaw0, _tStepYaw1, Mathf.SmoothStep(0, 1, _tStepK)) : _tFootYaw[i];
                fyaw = Mathf.Wrap(fy - yaw, -Mathf.Pi, Mathf.Pi);
                if (_tStepLeg == i) fpitch = 0.4f * Mathf.Sin(Mathf.Clamp(_tStepK, 0, 1) * Mathf.Pi) - 0.1f * Mathf.Sin(Mathf.Clamp(_tStepK, 0, 1) * Mathf.Pi * 2f);
            }
            var fb = new Basis(Vector3.Up, fyaw) * new Basis(Vector3.Right, fpitch);
            var ankleT = footL + fb * (TiAnkle(i) - TiSole(i));
            var l1 = TiHip(i).DistanceTo(TiKnee(i));
            var l2 = TiKnee(i).DistanceTo(TiAnkle(i));
            var pole = rp * new Vector3(0.18f * TSide(i) * (1f + deathK * 2f), -0.05f, 1f);
            var (knee, ankle) = TwoBone(hip, ankleT, l1, l2, pole);
            var right = rp * Vector3.Right;
            _tX[TThigh(i)] = new Transform3D(TAlign(TiKnee(i) - TiHip(i), Vector3.Right, knee - hip, right), hip);
            _tX[TShin(i)] = new Transform3D(TAlign(TiAnkle(i) - TiKnee(i), Vector3.Right, ankle - knee, right), knee);
            _tX[TFoot(i)] = new Transform3D(fb, ankle);
        }

        // --- Bras : ballant, balancement opposé aux pas, écartés et levés quand il rugit. ---
        for (var i = 0; i < 2; i++)
        {
            var sd = TSide(i);
            var walk = _tStepLeg >= 0 ? (_tStepLeg == i ? -1f : 1f) * 0.08f * Mathf.Sin(k * Mathf.Pi) : 0f;
            var idle = Mathf.Sin(_t * 0.8f + i * 1.7f) * 0.04f;
            var up = new Vector3(-0.1f + walk + idle - rear * 0.75f + stag * 0.25f + deathK * 0.5f, 0.1f * sd * rear, (-0.05f - rear * 0.45f - stag * 0.15f) * sd);
            _tX[TUpper(i)] = Chain(TBChest, TUpper(i), up);
            _tX[TFore(i)] = Chain(TUpper(i), TFore(i), new Vector3(-0.15f - rear * 0.5f + idle * 0.5f, 0, 0));
            var curl = 0.25f + 0.2f * Mathf.Sin(_t * 1.1f + i) + rear * 0.5f;
            _tX[THand(i)] = Chain(TFore(i), THand(i), new Vector3(curl, 0, 0.1f * sd));
        }

        // --- Lumière de la gueule (charge, souffle). ---
        if (_tChargeS > 0.02f && !dead)
        {
            _tMouthLight.Visible = true;
            _tMouthLight.Position = _tX[TBHead] * (TMouth - piv[TBHead] + new Vector3(0, 0, -2.5f));
            _tMouthLight.LightEnergy = _tChargeS * (TitanFiring ? 7f + 2f * Mathf.Sin(_t * 31f) : 3.5f + Mathf.Sin(_t * 17f));
        }
        else _tMouthLight.Visible = false;
        var glow = dead ? 0f : _tChargeS;
        if (Mathf.Abs(glow - _tChargeSent) > 0.01f)
        {
            _tChargeSent = glow;
            _tSkinMi.SetInstanceShaderParameter("charge", glow);
        }
        for (var i = 0; i < TPlates; i++)
            if (_tPlateGone[i]) _tWoundT[i] += dt;
    }

    /// <summary>Envoie les matrices des os (pose · T(-pivot)) au squelette de rendu ; plaques brisées et plaies cachées ramenées à un point.</summary>
    private void TitanSync()
    {
        if (!_skeleton.IsValid) return;
        var piv = _tSh.Pivot;
        for (var b = 0; b < TBCount; b++)
        {
            Transform3D s;
            if (b >= TBPlate)
            {
                var i = b >= TBWound ? b - TBWound : b - TBPlate;
                var parent = _tSh.PlateParent[i];
                s = Skin(parent, piv);
                var c = _tSh.PlateCenter[i];
                var wound = b >= TBWound;
                if (wound == _tPlateGone[i])
                {
                    // Plaie ouverte : elle gonfle en s'ouvrant puis bat.
                    if (wound)
                    {
                        var g = Mathf.Clamp(_tWoundT[i] / 0.35f, 0f, 1f);
                        var sc = g * (1f + 0.03f * Mathf.Sin(_t * 3.4f + i));
                        s *= new Transform3D(Basis.Identity.Scaled(Vector3.One * Mathf.Max(sc, 0.001f)), c - c * Mathf.Max(sc, 0.001f));
                    }
                }
                else s *= new Transform3D(Basis.Identity.Scaled(Vector3.One * 0.001f), c - c * 0.001f);
            }
            else s = Skin(b, piv);
            RenderingServer.SkeletonBoneSetTransform(_skeleton, b, s);
        }
    }

    private Transform3D Skin(int b, Vector3[] piv) => new(_tX[b].Basis, _tX[b].Origin - _tX[b].Basis * piv[b]);

    /// <summary>
    /// Mort : rugissement d'agonie (1,6 s), puis le corps bascule sur le côté <paramref name="side"/> (le modèle pivote
    /// autour de ses pieds), jambes figées dans leur dernière pose, queue et cou inertes.
    /// </summary>
    public void TitanDeath(float side)
    {
        if (_tDeathT >= 0f) return;
        _tDeathT = 0f;
        _tDeathSide = side;
        var inv = GlobalTransform.AffineInverse();
        for (var i = 0; i < 2; i++) _tDeathFoot[i] = inv * TitanFoot(i);
        TitanFiring = false;
        TitanCharge = 0f;
        // L'ennemi mort n'anime plus son modèle : un petit nœud prend le relais (chute, cou et queue inertes).
        AddChild(new TitanDeathDriver { Model = this });
        var tw = CreateTween();
        tw.TweenInterval(1.6f);
        tw.TweenProperty(this, "rotation", new Vector3(0.12f, 0.1f * side, -1.22f * side), 2.4f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        tw.TweenProperty(this, "rotation", new Vector3(0.1f, 0.1f * side, -1.15f * side), 0.5f).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Temps écoulé depuis la mort (-1 : vivant).</summary>
    public float TitanDeathTime => _tDeathT;

    /// <summary>Durée entre le début de la mort et l'impact du corps au sol.</summary>
    public const float TitanFallTime = 4.0f;

    /// <summary>
    /// Préchauffage pendant l'écran de déploiement si la mission compte un titan : maillage et textures construits,
    /// un exemplaire minuscule devant la caméra, gorge embrasée et plaies visibles (shaders compilés).
    /// </summary>
    public static void PrewarmTitan(Camera3D cam, MissionDef mission)
    {
        var has = false;
        foreach (var p in mission.Phases)
        foreach (var w in p.Waves)
        foreach (var g in w.Groups)
            if (g.type == EnemyType.Titan) has = true;
        if (!has || cam == null) return;
        var root = new Node3D { Name = "TitanPrewarm", Position = new Vector3(-0.3f, 0, -4f), Scale = Vector3.One * 0.004f };
        cam.AddChild(root);
        var m = Create(EnemyType.Titan);
        root.AddChild(m);
        m.TitanCharge = 1f;
        m._tChargeS = 1f;
        for (var i = 0; i < TPlates; i++) { m._tPlateGone[i] = i % 2 == 0; m._tWoundT[i] = 1f; }
        m.TitanSync();
        m._tMouthLight.Visible = true;
        m._tSkinMi.SetInstanceShaderParameter("charge", 1f);
        // Débris de plaque.
        var deb = new MeshInstance3D { Mesh = m._tSh.Debris[0], Position = new Vector3(20f, 30f, 0) };
        root.AddChild(deb);
        TitanFire.Prewarm(root);
        cam.GetTree().CreateTimer(1.5f).Timeout += () => { if (IsInstanceValid(root)) root.QueueFree(); };
    }
}

/// <summary>Anime le titan mort chaque image pendant sa chute, puis s'arrête (le corps reste dans sa dernière pose).</summary>
public partial class TitanDeathDriver : Node
{
    public BugModel Model;

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(Model)) return;
        Model.AnimateTitan((float)delta, 0f, 0f);
        if (Model.TitanDeathTime > BugModel.TitanFallTime + 3f) SetProcess(false);
    }
}
