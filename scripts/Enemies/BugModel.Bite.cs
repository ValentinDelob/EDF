using Godot;

namespace Rempart;

/// <summary>
/// Morsure des fourmis (noire, rouge, pillarde) : animation en quatre temps, déclenchée par le front montant de l'attaque
/// (hôte et clients) et indépendante de la décroissance de <c>attack</c> :
/// armé (le corps recule et se cabre, tête relevée, mandibules grandes ouvertes, pattes avant levées),
/// frappe (bond en avant, tête plongeante, mandibules qui claquent à <see cref="BiteImpact"/>),
/// arrachage (la tête secoue la proie de droite à gauche), retour au repos.
/// </summary>
public partial class BugModel
{
    /// <summary>Durée de la morsure (s) et instant où les mandibules se referment (dégâts et son, voir Enemy).</summary>
    public const float BiteDur = 0.85f, BiteImpact = 0.32f;

    private float _biteT = -1f, _lastAttack;

    private bool Bites => Type is EnemyType.BlackAnt or EnemyType.RedAnt or EnemyType.Scavenger;
    public bool HasBite => Bites;

    // Clés : instant, recul/bond (z), hauteur, tangage du corps (rad, + nez vers le bas), tangage de la tête (°),
    // ouverture des mandibules (°), levée des pattes avant (0..1), gastre (°, + relevé).
    private static readonly float[,] BiteKeys =
    {
        { 0f,    0f,    0f,    0f,     0f,   8f, 0f,   0f },
        { 0.24f, -0.42f, 0.32f, -0.32f, -30f, 65f, 1f,  18f },
        { 0.32f, 1.05f, -0.12f, 0.24f,  30f, -2f, 0.2f, -8f },
        { 0.44f, 0.92f, -0.06f, 0.18f,  22f,  1f, 0f,  -4f },
        { 0.62f, 0.8f,  0f,    0.14f,  16f,  3f, 0f,   0f },
        { BiteDur, 0f,  0f,    0f,     0f,   8f, 0f,   0f },
    };

    /// <summary>Fait avancer la morsure ; vrai pendant qu'elle se joue.</summary>
    private bool BiteTick(float dt, float attack)
    {
        if (!Bites) return false;
        if (attack > _lastAttack + 0.3f) _biteT = 0f;
        _lastAttack = attack;
        if (_biteT < 0f) return false;
        var before = _biteT;
        _biteT += dt;
        // Claquement des mandibules qui se referment (tous les pairs).
        if (before < BiteImpact && _biteT >= BiteImpact) Sfx.At("crunch", GlobalPosition, -3f, (float)GD.RandRange(0.9, 1.1), 0f);
        if (_biteT >= BiteDur) { _biteT = -1f; return false; }
        return true;
    }

    /// <summary>Pose interpolée (lissée entre les clés) pour l'instant <see cref="_biteT"/>.</summary>
    private float BiteKey(int col)
    {
        var t = _biteT;
        for (var i = 1; i < BiteKeys.GetLength(0); i++)
        {
            var t1 = BiteKeys[i, 0];
            if (t > t1) continue;
            var t0 = BiteKeys[i - 1, 0];
            var u = Mathf.Clamp((t - t0) / (t1 - t0), 0f, 1f);
            // Frappe : départ sec (accélération), les autres segments en douceur.
            u = i == 2 ? u * u * (2f - u) : u * u * (3f - 2f * u);
            return Mathf.Lerp(BiteKeys[i - 1, col], BiteKeys[i, col], u);
        }
        return BiteKeys[BiteKeys.GetLength(0) - 1, col];
    }

    /// <summary>Secousse de la tête pendant l'arrachage (°), qui s'éteint au retour.</summary>
    private float BiteShake()
    {
        if (_biteT < BiteImpact) return 0f;
        var k = _biteT - BiteImpact;
        var env = Mathf.Clamp(k / 0.05f, 0f, 1f) * Mathf.Clamp(1f - (k - 0.18f) / 0.2f, 0f, 1f);
        return Mathf.Sin(k * Mathf.Tau * 7.5f) * 13f * env;
    }

    private void BiteBody(float bob, float amp)
    {
        var shake = BiteShake();
        _bodyPivot.Position = new Vector3(0, _pivotY + bob + BiteKey(2), BiteKey(1));
        _bodyPivot.Rotation = new Vector3(BiteKey(3), Mathf.DegToRad(shake * 0.25f), Mathf.DegToRad(shake * 0.15f) + Mathf.Sin(_gait) * 0.03f * amp);
    }

    /// <summary>Pattes avant pendant la morsure : levées et tendues vers la proie à l'armé, reposées à la frappe.</summary>
    private bool BiteLeg(Leg l, float fa, float lift)
    {
        if (l.Fwd < 20f) return false;
        var raise = BiteKey(6);
        PoseLeg(l, Mathf.Lerp(fa, 0.8f, raise), Mathf.Max(lift, raise * 1.4f));
        return true;
    }

    private void BiteHead()
    {
        if (_head != null) _head.RotationDegrees = new Vector3(BiteKey(4), BiteShake(), BiteShake() * 0.3f);
        for (var i = 0; i < _antennae.Count; i++)
        {
            var s = i == 0 ? 1f : -1f;
            // Antennes plaquées vers l'arrière à l'armé, pointées vers la proie à la frappe.
            var back = BiteKey(6);
            _antennae[i].Rotation = new Vector3(-0.35f * back + 0.25f * Mathf.Clamp(BiteKey(1), 0f, 1f), 0.25f * back * s, Mathf.Sin(_t * 1.7f + i * 2.3f) * 0.08f);
        }
        if (_mandL != null)
        {
            // Mâchonnement pendant l'arrachage.
            var open = BiteKey(5) + (_biteT > BiteImpact + 0.05f ? Mathf.Abs(Mathf.Sin(_biteT * 30f)) * 5f : 0f);
            _mandL.RotationDegrees = new Vector3(0, open, 0);
            _mandR.RotationDegrees = new Vector3(0, -open, 0);
        }
        if (_gaster != null) _gaster.RotationDegrees = new Vector3(BiteKey(7), BiteShake() * -0.3f, 0);
    }
}
