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

    private void HandleFireEdf5(WeaponState ws, float dt, bool held, bool pressed)
    {
        var s = ws.W.T.Spec;
        UpdateLocks(ws, dt);
        if (ws.Reloading) { _burstLeft = 0; _salvoLeft = 0; return; }

        // Rafale en cours (Minion Buster Burst, Volcano…) : coups espacés, sans nouvelle pression.
        if (_burstLeft > 0)
        {
            _burstT -= dt;
            if (_burstT <= 0 && ws.Ammo > 0)
            {
                _burstT = s.Kind is Edf5Kind.Rocket ? 0.12f : 0.06f;
                _burstLeft--;
                ShootOnce(ws);
                if (ws.Ammo <= 0) { _burstLeft = 0; Reload(); }
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
        if (s.Kind is Edf5Kind.HandGrenade or Edf5Kind.Bomb) { muzzle = GlobalPosition + Vector3.Up * 1.6f; dir = (-Camera.GlobalBasis.Z).Normalized(); }
        var seed = (int)World.Rng.Randi();
        Edf5Fire.Shoot(World, s, ws.W, muzzle, dir, seed, true, this, () => AimPoint(800f, out _), target);
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
        if (s.Kind != Edf5Kind.Missile || Downed)
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
