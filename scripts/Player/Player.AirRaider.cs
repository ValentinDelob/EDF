using Godot;

namespace Rempart;

/// <summary>
/// Air Raider d'EDF 5 (Opérateur d'appui) : désignation au laser (canonnière, missiles, satellites), kit de guidage laser pour les
/// lance-missiles alliés, refus des frappes aériennes sous terre. Le reste du tir suit le chemin commun (munitions, cadence,
/// rechargement en secondes ou en points).
/// </summary>
public partial class Player
{
    private float _guideSendT;
    /// <summary>Laser de désignation affiché (canonnière, missiles, satellites, kit de guidage) : bouche → point visé.</summary>
    public bool LaserOn { get; private set; }
    public Vector3 LaserPoint { get; private set; }

    /// <summary>Armes de l'Air Raider qui ne suivent pas le tir commun ; vrai si l'image est traitée ici.</summary>
    private bool HandleAirRaider(WeaponState ws, Edf5Spec s, float dt, bool held, bool pressed)
    {
        LaserOn = false;
        var laser = s.Kind is Edf5Kind.Gunship or Edf5Kind.AirMissile or Edf5Kind.Satellite || s.Family == "guidekit";
        if (laser && (held || Aiming))
        {
            LaserPoint = AimPoint(Mathf.Max(ws.W.Range, 50f), out _);
            LaserOn = true;
            // Le point laser du tireur : missiles et rayon du satellite le suivent.
            if (held) AirRaid.DotFor(this).GlobalPosition = LaserPoint;
            Effects.Beam(World, MuzzlePos, LaserPoint, new Color(1f, 0.15f, 0.08f), 0.025f);
        }
        if (s.Family == "guidekit")
        {
            // Kit de guidage : point laser partagé (les lance-missiles à kit de guidage alliés s'y verrouillent).
            if (!held) return true;
            World.UpdateGuideLaser(Net.I.MyId, LaserPoint);
            _guideSendT -= dt;
            if (_guideSendT <= 0)
            {
                _guideSendT = 0.15f;
                Net.I.SendEdf5Shot(s.Id, LaserPoint, Vector3.Forward, 0);
            }
            _lastFireTime = Time.GetTicksMsec() / 1000f;
            return true;
        }
        if (AirRaid.NeedsSky(s) && !World.AirSupport)
        {
            // Sous terre : aucune frappe aérienne ne passe.
            if (pressed)
            {
                Sfx.Ui("empty");
                World.Hud?.Toast("Pas d'appui aérien ici.");
            }
            return true;
        }
        return false;
    }
}
