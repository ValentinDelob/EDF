using Godot;

namespace Rempart;

/// <summary>
/// Locomotion du soldat : vitesse mesurée dans le repère du modèle, cycle de pas calé sur la vitesse (marche, trot, course,
/// sprint ; pas lourds du Fencer), pieds posés au sol par cinématique inverse des jambes (avant, arrière, de côté : le bassin
/// se tourne vers la direction de marche, le buste reste face à la visée), roulade d'esquive, ruée, et mouvement secondaire de
/// l'arme dans les mains (rebond à chaque appui, roulis alterné, inertie aux accélérations et aux virages).
/// </summary>
public partial class SoldierModel
{
    /// <summary>Vitesse du soldat (monde), fournie par le propriétaire ; sinon mesurée d'une image à l'autre.</summary>
    public Vector3? Motion;
    /// <summary>Roulade d'esquive : progression 0..1 (négative hors roulade) et direction (monde).</summary>
    public float RollT = -1f;
    public Vector3 RollDir;
    /// <summary>Ruée (Fencer) ou poussée (Wing Diver) en cours.</summary>
    public bool Dash;

    private const float ThighLen = 0.44f, ShinLen = 0.40f, AnkleY = 0.08f;

    private Vector3 _prevPos, _mv, _acc;
    private bool _hasPrev;
    private float _prevYaw, _yawRate;
    private float _phase, _gaitBlend, _legYaw, _rollBlend, _dashBlend, _fallSpeed, _rollSpin;
    private bool _stepL, _stepR;
    // Mouvement secondaire de l'arme (ressort amorti) : décalage, vitesse, angles, vitesses angulaires.
    private Vector3 _gOff, _gVel, _gAng, _gAngVel;

    /// <summary>Vitesse lissée dans le repère du modèle (+Z devant, +X à gauche), accélération et vitesse de rotation.</summary>
    private Vector3 MotionLocal(float dt, float speed)
    {
        if (dt <= 0f) return _mv;
        var inTree = IsInsideTree();
        var gp = inTree ? GlobalPosition : Position;
        var world = Motion ?? (_hasPrev && dt < 0.5f ? (gp - _prevPos) / dt : Vector3.Zero);
        _prevPos = gp;
        _hasPrev = true;
        var basis = (inTree ? GlobalBasis : Basis).Orthonormalized();
        var local = basis.Inverse() * world;
        if (local.LengthSquared() > 50f * 50f) local = Vector3.Zero; // téléportation
        // Modèle immobile mais vitesse annoncée (menus, vitrines) : course sur place, vers l'avant.
        if (new Vector2(local.X, local.Z).Length() < 0.3f && speed > 0.5f) local = new Vector3(0, local.Y, speed);
        var prev = _mv;
        _mv = dt >= 0.5f ? local : _mv.Lerp(local, Mathf.Min(1f, dt * (Motion != null ? 18f : 9f)));
        if (dt < 0.5f) _acc = _acc.Lerp((_mv - prev) / dt, Mathf.Min(1f, dt * 8f));
        var yaw = inTree ? GlobalRotation.Y : Rotation.Y;
        if (dt < 0.5f) _yawRate = Mathf.Lerp(_yawRate, Mathf.AngleDifference(_prevYaw, yaw) / dt, Mathf.Min(1f, dt * 10f));
        _prevYaw = yaw;
        return _mv;
    }

    /// <summary>Réglages du pas selon la vitesse : cadence (cycles/s), part d'appui, levée du pied, amplitude du rebond.</summary>
    private (float cadence, float stance, float lift, float bob, float runW) GaitParams(float spd)
    {
        if (Class == SoldierClass.Heavy)
        {
            // Exosquelette : pas lourds et appuyés, genoux fléchis, rebond marqué à chaque appui.
            var w = Mathf.Clamp((spd - 4f) / 3f, 0f, 1f);
            return (0.72f + 0.1f * spd, Mathf.Lerp(0.62f, 0.5f, w), 0.13f + 0.05f * w, 0.035f, 0.2f * w);
        }
        var runW = Mathf.Clamp((spd - 2.4f) / 2.6f, 0f, 1f);
        var light = Class == SoldierClass.Airborne ? 0.85f : 1f;
        return (Mathf.Clamp(0.85f + 0.12f * spd, 0.85f, 2.0f), Mathf.Lerp(0.6f, 0.34f, runW), Mathf.Lerp(0.07f, 0.26f, runW) * light,
            Mathf.Lerp(0.018f, -0.026f, runW) * light, runW);
    }

    /// <summary>Position de la cheville d'une jambe (repère du modèle) à la phase p du cycle : appui (pied fixe au sol) puis envol.</summary>
    private static Vector3 FootPath(float p, float stance, float reach, float lift, float runW, Vector3 dir, Vector3 side)
    {
        float u, h;
        if (p < stance)
        {
            u = Mathf.Lerp(reach, -reach, p / stance);
            h = 0f;
        }
        else
        {
            var q = (p - stance) / (1f - stance);
            u = Mathf.Lerp(-reach, reach, Mathf.SmoothStep(0f, 1f, q));
            // Course : talon ramené haut derrière (pic de levée plus tôt), puis jambe lancée devant.
            h = lift * Mathf.Sin(Mathf.Pi * Mathf.Pow(q, Mathf.Lerp(1f, 0.7f, runW)));
        }
        return side + dir * u + new Vector3(0, AnkleY + h, 0);
    }

    /// <summary>
    /// IK de la jambe (hanche-genou-cheville) vers une cheville cible du repère du modèle : flexion de la hanche (X), écart (Z),
    /// flexion du genou. Cible hors d'atteinte : jambe tendue vers elle.
    /// </summary>
    private void LegIK(Node3D leg, Vector3 target, out float th, out float kn, out float ab)
    {
        var hipX = _hips.Transform;
        var joint = hipX * leg.Position;
        var d = hipX.Basis.Orthonormalized().Inverse() * (target - joint);
        var len = Mathf.Clamp(d.Length(), 0.25f, ThighLen + ShinLen - 0.001f);
        d = d.LengthSquared() > 1e-8f ? d.Normalized() * len : Vector3.Down * len;
        var cosInt = Mathf.Clamp((ThighLen * ThighLen + ShinLen * ShinLen - len * len) / (2f * ThighLen * ShinLen), -1f, 1f);
        kn = Mathf.Pi - Mathf.Acos(cosInt);
        var ay = -(ThighLen + ShinLen * Mathf.Cos(kn));
        var az = -ShinLen * Mathf.Sin(kn);
        ab = Mathf.Asin(Mathf.Clamp(d.X / -ay, -0.8f, 0.8f));
        th = Mathf.Wrap(Mathf.Atan2(d.Z, d.Y) - Mathf.Atan2(az, ay * Mathf.Cos(ab)), -Mathf.Pi, Mathf.Pi);
    }

    /// <summary>Mouvement secondaire de l'arme : ressort amorti, rebond aux appuis, inertie aux accélérations et aux virages.</summary>
    private void GunSpring(float dt, float intensity, bool stepL, bool stepR, float aimB)
    {
        if (dt <= 0f || dt >= 0.5f) return;
        var k = intensity * (1f - 0.7f * aimB);
        if (stepL || stepR)
        {
            _gVel.Y -= 0.42f * k;
            _gAngVel.X += 1.1f * k;                 // la bouche plonge à l'appui
            _gAngVel.Z += (stepL ? 1.4f : -1.4f) * k; // roulis alterné
        }
        // Inertie : l'arme traîne derrière le corps qui accélère, et en lacet quand le soldat pivote.
        var push = new Vector3(-_acc.X, 0, -_acc.Z) * 0.0035f;
        if (push.Length() > 0.3f) push = push.Normalized() * 0.3f;
        var twist = Mathf.Clamp(-_yawRate * 1.5f, -7f, 7f) * (1f - 0.6f * aimB);
        const float K = 170f, C = 17f;
        _gVel += (-K * _gOff - C * _gVel + push * 60f) * dt;
        _gOff += _gVel * dt;
        _gAngVel += (-K * _gAng - C * _gAngVel + new Vector3(0, twist, 0)) * dt;
        _gAng += _gAngVel * dt;
        _gOff = _gOff.Clamp(new Vector3(-0.06f, -0.06f, -0.06f), new Vector3(0.06f, 0.06f, 0.06f));
        _gAng = _gAng.Clamp(new Vector3(-0.2f, -0.25f, -0.2f), new Vector3(0.2f, 0.25f, 0.2f));
    }
}
