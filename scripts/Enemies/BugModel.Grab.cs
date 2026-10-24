using Godot;

namespace Rempart;

/// <summary>
/// Saisie de la fourmi rouge : la tête se relève avec la victime coincée entre les mandibules, secoue violemment
/// de gauche à droite et croque à intervalles réguliers (mandibules qui claquent, hochement vers le bas).
/// Le temps écoulé depuis la saisie (<see cref="GrabT"/>) suffit à rejouer exactement la même animation chez chaque pair.
/// </summary>
public partial class BugModel
{
    /// <summary>Point de prise entre les mandibules (fourmis), dans le repère de la tête.</summary>
    public Node3D GrabSocket { get; private set; }
    /// <summary>Centre de la capsule de la tête (fourmis) : la forme « point faible » le suit quand la tête se relève.</summary>
    public Node3D HeadCenter { get; private set; }

    /// <summary>Temps depuis la saisie (s), négatif hors saisie.</summary>
    public float GrabT = -1f;

    /// <summary>Durée du soulèvement de la victime jusqu'aux mandibules.</summary>
    public const float GrabLift = 0.45f;
    /// <summary>Intervalle entre deux coups de mandibules (un tic de dégâts chacun).</summary>
    public const float BitePeriod = 0.55f;
    /// <summary>Instant du claquement dans un cycle de morsure (fraction de <see cref="BitePeriod"/>).</summary>
    public const float BiteSnap = 0.1f;

    /// <summary>0 → 1 pendant le soulèvement.</summary>
    private float GrabBlend => Mathf.SmoothStep(0f, 1f, Mathf.Clamp(GrabT / GrabLift, 0f, 1f));

    /// <summary>Position dans le cycle de morsure (0..1, négatif pendant le soulèvement).</summary>
    private float BiteCycle => GrabT < GrabLift ? -1f : Mathf.PosMod((GrabT - GrabLift) / BitePeriod, 1f);

    /// <summary>Hochement de la morsure : monte d'un coup jusqu'au claquement puis retombe.</summary>
    private float BiteNod
    {
        get
        {
            var f = BiteCycle;
            if (f < 0) return 0f;
            return f < BiteSnap ? f / BiteSnap : Mathf.Exp(-(f - BiteSnap) * 9f);
        }
    }

    /// <summary>Secousse latérale (radians) : violente, par à-coups irréguliers.</summary>
    private float GrabShake => Mathf.DegToRad(26f) * GrabBlend
        * Mathf.Sin(GrabT * Mathf.Tau * 2.4f) * (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(GrabT * 1.3f + 0.4f)));

    /// <summary>Corps : l'avant se cabre (victime soulevée), léger lacet qui accompagne la secousse.</summary>
    private void GrabBody()
    {
        var g = GrabBlend;
        var nod = BiteNod;
        _bodyPivot.Position += new Vector3(0, 0.3f * g - 0.05f * nod * g, 0.1f * g);
        _bodyPivot.Rotation += new Vector3(-0.14f * g + 0.05f * nod * g, GrabShake * 0.3f, GrabShake * 0.12f);
    }

    /// <summary>Tête relevée qui secoue la victime, mandibules serrées qui claquent à chaque morsure, antennes agitées.</summary>
    private void GrabHead()
    {
        var g = GrabBlend;
        var nod = BiteNod;
        var shake = GrabShake;
        if (_head != null)
            _head.Rotation = new Vector3(Mathf.DegToRad(-16f * g + 12f * nod * g), shake, shake * 0.35f + Mathf.Sin(_t * 11f) * 0.03f * g);
        if (_mandL != null)
        {
            // Soulèvement : mâchoires grandes ouvertes qui se referment sur la victime ; ensuite, cycle desserrage → claquement.
            float open;
            var f = BiteCycle;
            if (f < 0) open = Mathf.Lerp(34f, 4f, g);
            else if (f < BiteSnap) open = Mathf.Lerp(24f, 1f, f / BiteSnap);
            else if (f < 0.55f) open = Mathf.Lerp(1f, 6f, (f - BiteSnap) / (0.55f - BiteSnap));
            else open = Mathf.Lerp(6f, 24f, Mathf.SmoothStep(0f, 1f, (f - 0.55f) / 0.45f));
            open += Mathf.Sin(_t * 31f) * 1.2f; // tremblement de l'effort
            _mandL.RotationDegrees = new Vector3(0, open, 0);
            _mandR.RotationDegrees = new Vector3(0, -open, 0);
        }
        for (var i = 0; i < _antennae.Count; i++)
        {
            var s = i == 0 ? 1f : -1f;
            _antennae[i].Rotation = new Vector3(0.3f * g + Mathf.Sin(_t * 9f + i * 1.7f) * 0.25f, Mathf.Sin(_t * 7f + i) * 0.3f * s, Mathf.Sin(_t * 8f + i * 2.3f) * 0.15f);
        }
    }
}
