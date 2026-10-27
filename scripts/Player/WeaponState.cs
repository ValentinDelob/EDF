using Godot;

namespace Rempart;

/// <summary>État d'une arme équipée pendant la mission (munitions, rechargement, cadence).</summary>
public class WeaponState
{
    public WeaponInstance W;
    public int Ammo;
    public float ReloadLeft;
    public float Cooldown;
    public bool Reloading => ReloadLeft > 0;
    /// <summary>Pour les rayons : réserve 0..1.</summary>
    public float BeamCharge = 1f;
    /// <summary>
    /// Wing Diver d'EDF 5 : prélèvement d'énergie pendant la recharge (faux en recharge d'urgence : la recharge attend) et
    /// vitesse de charge du cœur plasma. Charge : progression 0..1 d'une arme à charge.
    /// </summary>
    public System.Func<float, bool> Drain;
    public float ChargeSpeed = 1f;
    public float Charge;

    public WeaponState(WeaponInstance w)
    {
        W = w;
        Ammo = w.Magazine;
    }

    public float ReloadProgress => Reloading ? 1f - ReloadLeft / Mathf.Max(W.Reload, 0.01f) : 1f;

    /// <summary>Arme sans rechargement (Disruptor du Fencer : un seul chargeur par mission, comme dans EDF 5).</summary>
    /// Air Raider : postes de soin et armes sans temps de rechargement : nombre limité par mission.
    public bool NoReload => W.T.Spec is { Reload.Present: false } e
                            && (e is { Kind: Edf5Kind.Bullet, Category: "Canons automatiques" } || e.Class == SoldierClass.Support);

    /// <summary>Air Raider : rechargement compté en points (gagnés en infligeant des dégâts), pas en secondes.</summary>
    public bool PointReload => W.T.Spec?.PointReload == true;

    /// <summary>Points gagnés (dégâts infligés) : avance le rechargement des frappes à points.</summary>
    public void EarnPoints(float pts)
    {
        if (PointReload && ReloadLeft > 0) ReloadLeft = Mathf.Max(0.0001f, ReloadLeft - pts);
    }

    public void StartReload()
    {
        if (Reloading || NoReload) return;
        // Bouclier du Fencer : rechargement de la durabilité (bouclier inutilisable pendant ce temps).
        if (W.T.Spec?.Kind == Edf5Kind.Shield)
        {
            if (Charge >= 0.999f) return;
            ReloadLeft = W.Reload;
            return;
        }
        if (W.T.Kind == WeaponKind.Beam) { if (BeamCharge >= 0.999f) return; }
        else if (Ammo >= W.Magazine) return;
        ReloadLeft = W.Reload;
    }

    /// <summary>
    /// Coup parti : prochain coup dans 1 / cadence. Le reliquat de l'image (temps de recharge dépassé) est reporté : la cadence
    /// d'EDF 5 reste exacte quelle que soit la fréquence des images (une arme à 12 coups/s en tire bien 12).
    /// <paramref name="sustained"/> : tir automatique tenu, tout le retard est rattrapé (plusieurs coups dans la même image si la
    /// cadence dépasse celle des images) ; sinon le report est borné à 0,05 s (pas de rafale gratuite après une pause).
    /// </summary>
    public void NextShot(bool sustained = false) => Cooldown = Mathf.Max(Cooldown, sustained ? -MaxLag : -0.05f) + 1f / Mathf.Max(W.FireRate, 0.05f);

    /// <summary>Retard rattrapable au plus (s) : une image lente (10 images/s) ne fait pas perdre de coups.</summary>
    public const float MaxLag = 0.25f;

    public void Tick(float dt)
    {
        // Négatif : retard accumulé sur le prochain coup (reporté par NextShot), borné à MaxLag.
        Cooldown = Mathf.Max(-MaxLag, Cooldown - dt);
        if (ReloadLeft > 0)
        {
            // Frappes à points : un léger écoulement (3 pt/s) s'ajoute aux points gagnés en combat.
            var step = PointReload ? dt * 3f : dt * ChargeSpeed;
            // Recharge payée en énergie, au fil de la recharge ; en recharge d'urgence, elle attend.
            if (Drain != null && !Drain(W.EnergyCost * step / Mathf.Max(W.Reload, 0.01f))) return;
            ReloadLeft -= step;
            if (ReloadLeft <= 0)
            {
                ReloadLeft = 0;
                Ammo = W.Magazine;
                BeamCharge = 1f;
                if (W.T.Spec?.Kind == Edf5Kind.Shield) Charge = 1f;
            }
        }
    }
}
