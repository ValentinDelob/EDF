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

    public void StartReload()
    {
        if (Reloading) return;
        if (W.T.Kind == WeaponKind.Beam) { if (BeamCharge >= 0.999f) return; }
        else if (Ammo >= W.Magazine) return;
        ReloadLeft = W.Reload;
    }

    public void Tick(float dt)
    {
        Cooldown = Mathf.Max(0, Cooldown - dt);
        if (ReloadLeft > 0)
        {
            var step = dt * ChargeSpeed;
            // Recharge payée en énergie, au fil de la recharge ; en recharge d'urgence, elle attend.
            if (Drain != null && !Drain(W.EnergyCost * step / Mathf.Max(W.Reload, 0.01f))) return;
            ReloadLeft -= step;
            if (ReloadLeft <= 0)
            {
                ReloadLeft = 0;
                Ammo = W.Magazine;
                BeamCharge = 1f;
            }
        }
    }
}
