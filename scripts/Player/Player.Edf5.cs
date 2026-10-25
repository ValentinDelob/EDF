using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Tir des armes d'EDF 5 (Fusilier) : cadence, rafales, verrouillage des missiles, bombes C déclenchées à la demande,
/// lance-flammes, Reverser (soin des alliés). Les coups partent par <see cref="Edf5Fire.Shoot"/> ; les autres joueurs
/// les rejouent à l'identique à partir de la graine envoyée (<see cref="Net.SendEdf5Shot"/>).
/// </summary>
public partial class Player
{
    private int _burstLeft;
    private float _burstT;
    private readonly List<Projectile> _bombs = new();
    /// <summary>Ennemis verrouillés par le lance-missiles tenu (dans l'ordre du verrouillage).</summary>
    public readonly List<Enemy> Locks = new();
    private float _lockT, _streamT;
    /// <summary>Cible en cours de verrouillage (réticule du HUD) et progression 0..1.</summary>
    public Enemy LockPending { get; private set; }
    public float LockProgress { get; private set; }
    private int _salvoLeft, _salvoIdx;
    private float _salvoT;

    private bool IsEdf5(WeaponState ws) => ws?.W.T.Spec != null;

    /// <summary>Armes à charge : bouton tenu à l'image précédente (le tir part au relâchement).</summary>
    private bool _chargeHeld;
    /// <summary>Rafale d'une arme à charge en cours de décharge (Phalanx, Lightning Bow, Pulse, Closed Laser, MONSTER…).</summary>
    private bool _discharging;
    private float _shotPower = 1f;

    /// <summary>
    /// Arme à charge (Wing Diver) : bouton tenu = charge payée en énergie (0 → 1 en « Charge Time », plus vite avec un cœur
    /// de charge) ; relâché = tir dont la puissance suit la charge ; les lances multiples tirent une rafale.
    /// </summary>
    private void HandleCharge(WeaponState ws, float dt, bool held)
    {
        var s = ws.W.T.Spec;
        if (held && ws.Charge < 1f)
        {
            var step = Mathf.Min(1f - ws.Charge, dt * ws.ChargeSpeed / Mathf.Max(ws.W.Reload, 0.01f));
            if (DrawEnergy(ws.W.EnergyCost * step)) ws.Charge += step;
        }
        var release = _chargeHeld && !held;
        _chargeHeld = held;
        if (!release || ws.Charge < 0.05f) return;
        var power = ws.Charge;
        ws.Charge = 0f;
        _lastFireTime = Time.GetTicksMsec() / 1000f;
        if (ws.W.Magazine > 1)
        {
            // Double / Triple / Twin Lance, Plasma Burst Cannon : rafale d'autant de coups que la charge le permet.
            _burstLeft = Mathf.Max(1, Mathf.RoundToInt(ws.W.Magazine * power));
            _burstT = 0;
            _shotPower = 1f;
            ws.Ammo = _burstLeft;
            return;
        }
        ws.Ammo = 1;
        _shotPower = power;
        ShootOnce(ws);
        _shotPower = 1f;
    }

    /// <summary>
    /// Arme à rafale chargée : bouton tenu = le chargeur se remplit (énergie par coup) ; relâché = il se vide en rafale à la
    /// cadence de l'arme, jusqu'au dernier coup.
    /// </summary>
    private void HandleChargeBurst(WeaponState ws, float dt, bool held)
    {
        if (_discharging)
        {
            if (ws.Ammo <= 0) { _discharging = false; return; }
            if (ws.Cooldown > 0) return;
            // Cadence supérieure à l'image (60 coups/s) : plusieurs coups dans la même image.
            var n = Mathf.Clamp(Mathf.RoundToInt(dt * ws.W.FireRate), 1, 4);
            for (var i = 0; i < n && ws.Ammo > 0; i++) ShootOnce(ws);
            ws.Cooldown = n / Mathf.Max(ws.W.FireRate, 0.05f);
            _lastFireTime = Time.GetTicksMsec() / 1000f;
            return;
        }
        if (held && ws.Ammo < ws.W.Magazine)
        {
            var rate = ws.W.Magazine * ws.ChargeSpeed / Mathf.Max(ws.W.Reload, 0.01f);
            ws.Charge += rate * dt;
            var whole = Mathf.Min((int)ws.Charge, ws.W.Magazine - ws.Ammo);
            if (whole > 0)
            {
                if (DrawEnergy(ws.W.EnergyCost * whole / Mathf.Max(ws.W.Magazine, 1))) ws.Ammo += whole;
                ws.Charge -= whole;
            }
        }
        var release = _chargeHeld && !held;
        _chargeHeld = held;
        if (release && ws.Ammo > 0) { _discharging = true; ws.Charge = 0; }
    }

    private void HandleFireEdf5(WeaponState ws, float dt, bool held, bool pressed)
    {
        var s = ws.W.T.Spec;
        UpdateLocks(ws, dt);
        // Bot : relâche la détente d'une arme à charge quand la charge est pleine.
        if (Bot && held && (s.Supply == Edf5Supply.Charge && ws.Charge >= 1f || s.Supply == Edf5Supply.ChargeBurst && ws.Ammo >= ws.W.Magazine)) held = false;
        if (s.Supply == Edf5Supply.Charge && _burstLeft <= 0) { HandleCharge(ws, dt, held); return; }
        if (s.Supply == Edf5Supply.ChargeBurst) { HandleChargeBurst(ws, dt, held); return; }
        if (ws.Reloading) { _burstLeft = 0; _salvoLeft = 0; return; }

        // Rafale en cours (Minion Buster Burst, Volcano…) : coups espacés, sans nouvelle pression.
        if (_burstLeft > 0)
        {
            _burstT -= dt;
            if (_burstT <= 0 && ws.Ammo > 0)
            {
                _burstT = s.Kind is Edf5Kind.Rocket ? 0.12f : s.Kind is Edf5Kind.PlasmaShot ? 0.15f : 0.06f;
                _burstLeft--;
                ShootOnce(ws);
                if (ws.Ammo <= 0) { _burstLeft = 0; if (s.Supply != Edf5Supply.Charge) Reload(); }
            }
            return;
        }
        // Salve de missiles : un missile par cible verrouillée.
        if (_salvoLeft > 0)
        {
            _salvoT -= dt;
            if (_salvoT <= 0 && ws.Ammo > 0)
            {
                _salvoT = 1f / Mathf.Max(ws.W.FireRate, 1f);
                _salvoLeft--;
                ShootOnce(ws, Locks.Count > 0 ? Locks[_salvoIdx++ % Locks.Count] : null);
                if (_salvoLeft == 0 || ws.Ammo <= 0) { _salvoLeft = 0; Locks.Clear(); if (ws.Ammo <= 0) Reload(); }
            }
            return;
        }

        switch (s.Kind)
        {
            case Edf5Kind.Flame:
            case Edf5Kind.Reverser when !s.Blast.Present:
                if (held && ws.Ammo > 0) Stream(ws, dt);
                else if (ws.Ammo <= 0 && held) Reload();
                return;
            case Edf5Kind.Bomb:
                if (!pressed || ws.Cooldown > 0) return;
                if (ws.Ammo <= 0) { DetonateBombs(); Reload(); return; }
                break;
        }

        if (ws.Cooldown > 0) return;
        if (!(ws.W.T.Automatic ? held : pressed)) return;
        if (ws.Ammo <= 0)
        {
            if (pressed) Sfx.Ui("empty");
            Reload();
            return;
        }
        ws.Cooldown = 1f / Mathf.Max(ws.W.FireRate, 0.05f);
        _lastFireTime = Time.GetTicksMsec() / 1000f;
        if (s.Kind == Edf5Kind.Missile)
        {
            // Une pression : tous les missiles du chargeur partent, un par cible verrouillée (en boucle s'il y a moins de cibles).
            _salvoLeft = Mathf.Min(ws.Ammo, Mathf.Max(1, s.Family == "fork" ? 1 : Mathf.Max(Locks.Count, 1)));
            _salvoT = 0;
            _salvoIdx = 0;
            return;
        }
        if (s.Burst > 1)
        {
            _burstLeft = s.Burst;
            _burstT = 0;
            return;
        }
        ShootOnce(ws);
        if (ws.Ammo <= 0 && s.Kind != Edf5Kind.Bomb) Reload();
    }

    /// <summary>Un coup : munition, départ local, réplique réseau, son, flamme de bouche, recul.</summary>
    private void ShootOnce(WeaponState ws, Enemy target = null)
    {
        var s = ws.W.T.Spec;
        ws.Ammo--;
        var range = ws.W.Range;
        var aim = AimPoint(Mathf.Max(range, 60f), out _);
        var muzzle = MuzzlePos;
        var dir = (aim - muzzle).Normalized();
        if (s.Kind is Edf5Kind.HandGrenade or Edf5Kind.Bomb or Edf5Kind.Orb) { muzzle = GlobalPosition + Vector3.Up * 1.6f; dir = (-Camera.GlobalBasis.Z).Normalized(); }
        var seed = (int)World.Rng.Randi();
        // Blasters : la puissance baisse quand le chargeur se vide (Mag Blaster, Reflectron : min + (max − min) × (reste)²).
        var power = _shotPower;
        if (s.Family is "magblaster" or "reflectron")
        {
            var frac = Mathf.Clamp((ws.Ammo + 1f) / Mathf.Max(ws.W.Magazine, 1), 0f, 1f);
            power *= 0.2f + 0.8f * frac * frac;
        }
        // Armes guidées de la Wing Diver : les projectiles se répartissent entre les cibles verrouillées.
        if (s.Kind == Edf5Kind.Homing) Edf5Fire.HomingTargets = Locks.Where(e => IsInstanceValid(e)).Cast<Node3D>().ToList();
        Edf5Fire.Shoot(World, s, ws.W, muzzle, dir, seed, true, this, () => AimPoint(800f, out _), target, power);
        Edf5Fire.HomingTargets = null;
        Net.I.SendEdf5Shot(s.Id, muzzle, dir, seed);
        Edf5Sounds.Fire(s, muzzle, true);
        if (s.Kind is Edf5Kind.Bullet or Edf5Kind.BoundGun or Edf5Kind.Rocket or Edf5Kind.GuidedRocket or Edf5Kind.GrenadeLauncher)
            Effects.MuzzleFlash(World.FxRoot, muzzle, new Color(1f, 0.7f, 0.3f), s.Pellets > 1 || s.Kind != Edf5Kind.Bullet ? 0.75f : 0.45f);
        var kick = s.Category switch
        {
            "Fusil de précision" => 1.4f, "Fusil à pompe" => 1.1f, "Lance-roquettes" => 1.2f, "Lance-missiles" => 0.6f,
            "Grenades" => 0.7f, _ => s.Kind == Edf5Kind.Bullet ? 0.22f : 0.4f,
        };
        _recoil = Mathf.Min(1.5f, _recoil + kick);
        _pitch += Mathf.DegToRad(kick * 1.1f) * (AnchorTime > 0 ? 0.2f : 1f);
    }

    /// <summary>Armes à jet continu (60 coups/s) : lance-flammes et Reverser.</summary>
    private void Stream(WeaponState ws, float dt)
    {
        var s = ws.W.T.Spec;
        _beamActive = true;
        _lastFireTime = Time.GetTicksMsec() / 1000f;
        var use = Mathf.Max(1, Mathf.RoundToInt(dt * 60f));
        ws.Ammo = Mathf.Max(0, ws.Ammo - use);
        var muzzle = MuzzlePos;
        var dir = (AimPoint(60f, out _) - muzzle).Normalized();
        if (s.Kind == Edf5Kind.Flame) FlameJet.For(World).Emit(s, ws.W.Damage * DamageMul, muzzle, dir, dt, true);
        else HealBeam(ws, muzzle, dir, dt);
        _streamT -= dt;
        if (_streamT <= 0)
        {
            _streamT = 0.1f;
            Net.I.SendEdf5Shot(s.Id, muzzle, dir, 0);
            Edf5Sounds.Fire(s, muzzle, true);
        }
        if (ws.Ammo <= 0) Reload();
    }

    /// <summary>Reverser : soigne le soldat allié le plus proche du réticule (30 m), comme dans EDF 5.</summary>
    private void HealBeam(WeaponState ws, Vector3 muzzle, Vector3 dir, float dt)
    {
        const float reach = 30f;
        Node3D best = null;
        var bestAng = 0.35f;
        foreach (var r in World.Remotes.Values)
        {
            var to = r.GlobalPosition + Vector3.Up * 1.1f - muzzle;
            if (to.Length() > reach) continue;
            var a = dir.AngleTo(to);
            if (a < bestAng) { bestAng = a; best = r; }
        }
        var end = best != null ? best.GlobalPosition + Vector3.Up * 1.1f : muzzle + dir * 12f;
        Effects.Beam(World, muzzle, end, new Color(0.35f, 1f, 0.55f), 0.05f);
        if (best is RemotePlayer rp) Net.I.SendHealPlayer(rp.PeerId, ws.W.Damage * 60f * dt);
    }

    /// <summary>Lance-missiles tenu : verrouille une cible toutes les « Lock-On Time » secondes dans un cône de 20° autour du réticule.</summary>
    private void UpdateLocks(WeaponState ws, float dt)
    {
        var s = ws.W.T.Spec;
        if (s.Kind is not (Edf5Kind.Missile or Edf5Kind.Homing) || Downed)
        {
            if (Locks.Count > 0) Locks.Clear();
            LockPending = null;
            return;
        }
        var range = ws.W.LockRange;
        var fwd = -Camera.GlobalBasis.Z;
        var eye = Camera.GlobalPosition;
        Locks.RemoveAll(e => !IsInstanceValid(e) || e.State == EnemyState.Dead || e.Center.DistanceTo(eye) > range * 1.1f
                             || fwd.AngleTo(e.Center - eye) > Mathf.DegToRad(40f));
        if (ws.Reloading || _salvoLeft > 0 || Locks.Count >= ws.W.LockTargets) { LockPending = null; LockProgress = 0; return; }
        var cand = World.EnemiesById.Values
            .Where(e => e.State != EnemyState.Dead && !Locks.Contains(e) && e.Center.DistanceTo(eye) < range && fwd.AngleTo(e.Center - eye) < Mathf.DegToRad(20f))
            .OrderBy(e => fwd.AngleTo(e.Center - eye)).FirstOrDefault();
        if (cand != LockPending) { LockPending = cand; _lockT = 0; }
        if (cand == null) { LockProgress = 0; return; }
        _lockT += dt;
        LockProgress = Mathf.Clamp(_lockT / Mathf.Max(ws.W.LockTime, 0.02f), 0, 1);
        if (_lockT < ws.W.LockTime) return;
        Locks.Add(cand);
        _lockT = 0;
        LockPending = null;
        Sfx.Ui("hitmarker", -10f, 1.6f, 30);
    }

    /// <summary>État d'une arme tenue : énergie (recharge payée par le cœur plasma) et vitesse de charge du cœur.</summary>
    public WeaponState Arm(WeaponInstance w)
    {
        var ws = new WeaponState(w);
        if (w.T.Spec?.Energy.Present == true) ws.Drain = DrawEnergy;
        if (Core != null) ws.ChargeSpeed = 1f + Core.T.Spec.WeaponCharge;
        if (w.T.Spec?.Supply == Edf5Supply.Charge) ws.Ammo = 1;
        return ws;
    }

    /// <summary>Changement d'arme : la charge en cours est perdue (comme dans EDF 5).</summary>
    private void DropCharge()
    {
        _discharging = false;
        _chargeHeld = false;
        _burstLeft = 0;
        foreach (var w in Weapons) w.Charge = 0f;
    }

    /// <summary>Test (--edf5-fire) : déclenche les bombes C posées.</summary>
    public void Edf5DetonateForTest() => DetonateBombs();

    /// <summary>Bombe C posée (déclenchée plus tard par <see cref="DetonateBombs"/>).</summary>
    public void Edf5PlacedBomb(Projectile p) => _bombs.Add(p);

    /// <summary>Déclenche toutes les bombes C posées (chargeur vide puis tir, ou rechargement).</summary>
    private void DetonateBombs()
    {
        var n = 0;
        foreach (var b in _bombs)
            if (IsInstanceValid(b)) { b.Life = 0.02f + 0.06f * n++; }
        _bombs.Clear();
        if (n > 0) Sfx.Ui("ui_click", -2f, 0.6f);
    }
}
