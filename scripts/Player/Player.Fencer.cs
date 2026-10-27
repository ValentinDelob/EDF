using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Fencer d'EDF 5 (Grenadier blindé) : frappes chargées, coups de lance enchaînés, boucliers (garde passive dans le jeu actif,
/// garde renvoyant les projectiles quand le bouclier est en main), mise en rotation des canons automatiques, poussées et sauts
/// propulsés consécutifs, et effets des deux équipements de soutien.
/// </summary>
public partial class Player
{
    /// <summary>Équipements de soutien portés (Fencer).</summary>
    public readonly List<WeaponInstance> Gear = new();

    /// <summary>Effets cumulés des équipements de soutien (valeurs neutres sans équipement).</summary>
    public struct FencerGear
    {
        public int Dashes, Boosts;
        public float DashInterval, BoostGain, ShieldUse, ShieldDurability, ShieldAngle, ShieldCut, KnockbackCut, RecoilCut, WeightSpeed, WalkSpeed;

        public static FencerGear Neutral => new()
        {
            Dashes = 1, Boosts = 1, DashInterval = 1f, BoostGain = 1f, ShieldUse = 1f, ShieldDurability = 1f, WeightSpeed = 1f, WalkSpeed = 1f,
        };

        /// <summary>
        /// Deux équipements du même type : seul le plus fort (le plus haut niveau) compte, comme dans EDF 5 ; types différents :
        /// effets cumulés (le recul réduit par un stabilisateur et par l'exosquelette de bombardement : le meilleur).
        /// </summary>
        public static FencerGear From(IEnumerable<Edf5Spec> gear)
        {
            var r = Neutral;
            foreach (var g in gear.GroupBy(x => x.Category).Select(x => x.OrderByDescending(e => e.Level).First()))
            {
                switch (g.Category)
                {
                    case "Propulseurs":
                        if (g.Dashes > 0) r.Dashes = g.Dashes;
                        if (g.Boosts > 0) r.Boosts = g.Boosts;
                        r.DashInterval = g.DashInterval;
                        r.BoostGain = g.BoostGain;
                        break;
                    case "Renforts de bouclier":
                        r.ShieldUse = g.ShieldUse;
                        r.ShieldDurability = g.ShieldDurability;
                        r.ShieldAngle = g.ShieldAngle;
                        r.ShieldCut = g.ShieldCut;
                        r.KnockbackCut = g.KnockbackCut;
                        break;
                    case "Stabilisateurs":
                        r.RecoilCut = Mathf.Max(r.RecoilCut, g.RecoilCut);
                        break;
                    case "Exosquelettes":
                        r.WeightSpeed = g.WeightSpeed;
                        r.WalkSpeed = g.WalkSpeed;
                        r.RecoilCut = Mathf.Max(r.RecoilCut, g.RecoilCut);
                        break;
                }
            }
            return r;
        }
    }

    public FencerGear FGear = FencerGear.Neutral;

    // Poussées (sprint / esquive) et sauts propulsés (saut en l'air) : réserves rechargées après un intervalle / à l'atterrissage.
    private int _dashLeft = 1, _boostLeft = 1;
    private float _dashRefill, _heavyDashSpeed = 19f;
    /// <summary>Poussées disponibles (HUD) : 0..1.</summary>
    public float DashReady => _dashLeft / (float)Mathf.Max(FGear.Dashes, 1);

    // Frappe : parade pendant l'attaque ; charge en cours (rouleau) ; mise en rotation ; enchaînement des lances ; garde.
    private float _guardT, _guardCut;
    private float _lungeLeft, _lungeDmg;
    private Vector3 _lungeDir;
    private readonly HashSet<Enemy> _lungeHit = new();
    private float _spin;
    private float _lastThrust = -10f;
    private int _thrustIdx;
    /// <summary>Garde du bouclier tenu en main (bouton de tir tenu) : renvoi des projectiles, état transmis aux autres pairs.</summary>
    public bool Reflecting { get; private set; }

    /// <summary>Mise en rotation 0..1 du canon automatique tenu (HUD).</summary>
    public float SpinProgress => Weapon?.W.T.Spec?.SpinUp.Present == true ? Mathf.Clamp(_spin, 0, 1) : 1f;

    /// <summary>Bouclier du jeu actif (dans la main libre), ou null.</summary>
    public WeaponState ActiveShield
    {
        get
        {
            if (Def.Id != SoldierClass.Heavy) return null;
            var start = HasSets ? ActiveSet * Classes.SetSize : 0;
            var end = HasSets ? Mathf.Min(start + Classes.SetSize, Weapons.Count) : Weapons.Count;
            for (var i = start; i < end; i++)
                if (Weapons[i].W.T.Spec?.Kind == Edf5Kind.Shield) return Weapons[i];
            return null;
        }
    }

    /// <summary>Vitesse de marche du Fencer : exosquelette, bouclier lourd porté ou en garde (l'exosquelette réduit l'effet du poids).</summary>
    private float FencerSpeedMul()
    {
        var mul = FGear.WalkSpeed;
        var sh = ActiveShield?.W.T.Spec;
        if (sh != null)
        {
            var carry = Reflecting ? Mathf.Min(sh.Stance.Speed, sh.Carry.Speed) : sh.Carry.Speed;
            mul *= 1f - (1f - carry) * FGear.WeightSpeed;
        }
        return mul;
    }

    /// <summary>Équipement porté au départ (appelé par <see cref="Create"/>).</summary>
    private void SetupFencer()
    {
        FGear = FencerGear.From(Gear.Select(g => g.T.Spec));
        _dashLeft = FGear.Dashes;
        _boostLeft = FGear.Boosts;
    }

    /// <summary>Déplacements propulsés du Fencer : poussées consécutives au sol, sauts propulsés consécutifs en l'air.</summary>
    private void HeavyMove(ref Vector3 v, Vector3 wish, Basis camBasis, bool onFloor, bool jumpPressed, bool dashPressed, float dt)
    {
        if (_dashRefill > 0)
        {
            _dashRefill -= dt;
            if (_dashRefill <= 0) _dashLeft = FGear.Dashes;
        }
        if (onFloor && v.Y <= 0.1f) _boostLeft = FGear.Boosts;
        if (dashPressed && _dashTime <= 0 && _dashLeft > 0 && _lungeLeft <= 0)
        {
            _dashLeft--;
            // Intervalle avant de retrouver les poussées (réduit par les Dash Cell) ; relancé à chaque poussée.
            _dashRefill = 1.1f * FGear.DashInterval;
            _dashTime = 0.28f;
            _heavyDashSpeed = 19f;
            _dashDir = wish.LengthSquared() > 0.01f ? wish.Normalized() : -camBasis.Z;
            Sfx.At("dash", GlobalPosition, 0f, 0.8f);
        }
        if (jumpPressed && (onFloor || _boostLeft > 0))
        {
            if (onFloor) v.Y = 11f;
            else
            {
                // Saut propulsé : relance verticale et poussée horizontale (Add Booster : plus loin).
                _boostLeft--;
                v.Y = Mathf.Max(v.Y, 10.5f);
                _dashTime = 0.32f;
                _heavyDashSpeed = 13f * FGear.BoostGain;
                _dashDir = wish.LengthSquared() > 0.01f ? wish.Normalized() : -camBasis.Z with { Y = 0 };
            }
            Sfx.At("dash", GlobalPosition, -2f, 0.7f);
        }
        // Charge des rouleaux et du Force Ax : course rapide qui frappe chaque ennemi une fois.
        if (_lungeLeft > 0)
        {
            const float lungeSpeed = 42f;
            _dashTime = Mathf.Max(_dashTime, dt * 1.5f);
            _dashDir = _lungeDir;
            _heavyDashSpeed = lungeSpeed;
            _lungeLeft -= lungeSpeed * dt;
            Dictionary<Enemy, (float, Vector3, bool)> per = null;
            var c = GlobalPosition + Vector3.Up * 1.1f;
            foreach (var e in World.EnemiesById.Values)
            {
                if (e.State == EnemyState.Dead || _lungeHit.Contains(e) || e.BodyDistance(c) > 2.6f + e.Def.Size * 1.1f) continue;
                _lungeHit.Add(e);
                per ??= new();
                per[e] = (_lungeDmg, e.Center, false);
            }
            if (per != null) { FencerFire.Apply(World, per); AddShake(0.25f); }
            if (_lungeLeft <= 0) _dashTime = Mathf.Min(_dashTime, 0.05f);
        }
        // Jauge du HUD (« Propulsion ») : part des poussées utilisées.
        Heat = 100f * (1f - _dashLeft / (float)Mathf.Max(FGear.Dashes, 1));
        Overheated = false;
    }

    /// <summary>Charge d'un rouleau ou du Force Ax : course de <paramref name="distance"/> m en frappant ce qu'elle traverse.</summary>
    public void FencerLunge(Vector3 dir, float distance, float damage)
    {
        _lungeDir = dir;
        _lungeLeft = distance;
        _lungeDmg = damage;
        _lungeHit.Clear();
        _guardT = Mathf.Max(_guardT, distance / 42f + 0.2f);
    }

    /// <summary>Tir des armes propres au Fencer ; faux si l'arme suit le tir commun (canon tourné, missiles…).</summary>
    private bool HandleFencer(WeaponState ws, Edf5Spec s, float dt, bool held, bool pressed)
    {
        _guardT = Mathf.Max(0, _guardT - dt);
        Reflecting = false;
        switch (s.Kind)
        {
            case Edf5Kind.Strike: HandleStrike(ws, s, dt, held); return true;
            case Edf5Kind.Pierce: HandlePierce(ws, s, held, pressed); return true;
            case Edf5Kind.Shield:
                Reflecting = held && ws.Charge > 0 && !ws.Reloading;
                if (Reflecting) _lastFireTime = Time.GetTicksMsec() / 1000f;
                return true;
        }
        if (!s.SpinUp.Present) return false;
        // Canons automatiques : le canon tourne tant que le bouton est tenu, le tir ne part qu'à pleine vitesse.
        if (held && !ws.Reloading) _spin = Mathf.Min(1f, _spin + dt / Mathf.Max(ws.W.SpinUp, 0.05f));
        else _spin = Mathf.Max(0f, _spin - dt / Mathf.Max(ws.W.SpinUp, 0.05f) * 0.8f);
        return _spin < 1f;
    }

    /// <summary>Frappe : bouton tenu = charge (niveaux d'EDF 5), relâché = frappe du niveau atteint ; parade pendant la frappe.</summary>
    private void HandleStrike(WeaponState ws, Edf5Spec s, float dt, bool held)
    {
        if (ws.Reloading || ws.Cooldown > 0) { if (!held) ws.Charge = 0; _chargeHeld = held && !ws.Reloading; return; }
        var top = s.Strikes[^1].Charge;
        if (held) ws.Charge = Mathf.Min(top + 0.5f, ws.Charge + dt * ws.W.Swing);
        var release = _chargeHeld && !held;
        // Bot : relâche au niveau maximal.
        // (Pas la dispersion arrière du Quake Haken : le bot frappe devant lui.)
        var botTop = s.Strikes.Length > 1 && s.Strikes[^1].Attack.StartsWith("Back") ? s.Strikes[^2].Charge : top;
        if (Bot && held && ws.Charge >= botTop) release = true;
        _chargeHeld = held && !release;
        if (!release) return;
        if (ws.Ammo <= 0) { ws.Charge = 0; Sfx.Ui("empty"); Reload(); return; }
        var level = 0;
        for (var i = 0; i < s.Strikes.Length; i++)
            if (ws.Charge >= s.Strikes[i].Charge) level = i;
        ws.Charge = 0;
        ws.Cooldown = 0.45f;
        _guardT = 0.55f;
        _guardCut = s.Guard;
        FireFencer(ws, level);
        if (ws.Ammo <= 0) Reload();
    }

    /// <summary>Lances : une pression = attaque 1, une seconde pression rapide = attaque 2 ; marteaux-piqueurs et Flashing Spear : bouton tenu.</summary>
    private void HandlePierce(WeaponState ws, Edf5Spec s, bool held, bool pressed)
    {
        if (ws.Reloading || ws.Cooldown > 0) return;
        var combo = s.Strikes is { Length: > 1 };
        if (!(ws.W.T.Automatic ? held : pressed)) return;
        if (ws.Ammo <= 0) { if (pressed) Sfx.Ui("empty"); Reload(); return; }
        var now = Time.GetTicksMsec() / 1000f;
        var idx = 0;
        if (combo)
        {
            idx = _thrustIdx == 0 && now - _lastThrust < 1.1f ? 1 : 0;
            _thrustIdx = idx;
            ws.Cooldown = (idx == 0 ? 0.6f : 0.8f) / Mathf.Max(ws.W.Swing, 0.3f);
        }
        else ws.NextShot();
        _lastThrust = now;
        FireFencer(ws, idx);
        if (ws.Ammo <= 0) Reload();
    }

    /// <summary>Un coup de mêlée : munition, départ local, réplique réseau (niveau dans la graine), son, recul.</summary>
    private void FireFencer(WeaponState ws, int level)
    {
        var s = ws.W.T.Spec;
        ws.Ammo--;
        ShotsFired++;
        _lastFireTime = Time.GetTicksMsec() / 1000f;
        var from = GlobalPosition + Vector3.Up * 1.3f;
        var reach = s.Kind == Edf5Kind.Pierce && s.Family != "spinedriver" ? FencerFire.PierceReach(ws.W.Range) : Mathf.Max(ws.W.Range, 30f);
        var dir = (AimPoint(Mathf.Max(reach, 30f), out _) - from).Normalized();
        if (s.Kind == Edf5Kind.Pierce) from = MuzzlePos;
        var seed = FencerFire.WithLevel((int)World.Rng.Randi(), level);
        Edf5Fire.Shoot(World, s, ws.W, from, dir, seed, true, this);
        Net.I.SendEdf5Shot(s.Id, from, dir, seed);
        Edf5Sounds.Fire(s, from, true);
        var kick = s.Kind == Edf5Kind.Strike ? 0.5f + 0.35f * level : 0.35f;
        _recoil = Mathf.Min(1.5f, _recoil + kick);
        AddShake(s.Kind == Edf5Kind.Strike ? 0.08f + 0.06f * level : 0.04f);
    }

    /// <summary>
    /// Garde du Fencer (appelée par <see cref="TakeDamage"/>) : parade d'une frappe en cours, puis bouclier du jeu actif si
    /// le coup vient de face (angle de garde, élargi en garde tenue) : part parée retirée de la durabilité, le reste passe.
    /// </summary>
    private float FencerGuard(float dmg, Vector3 from)
    {
        if (Def.Id != SoldierClass.Heavy) return dmg;
        if (_guardT > 0) dmg *= 1f - _guardCut;
        var sh = ActiveShield;
        if (sh == null || sh.Charge <= 0 || sh.Reloading) return dmg;
        var spec = sh.W.T.Spec;
        var to = from - GlobalPosition;
        to.Y = 0;
        var fwd = -new Basis(Vector3.Up, _yaw).Z;
        var half = Mathf.DegToRad((spec.GuardAngle + FGear.ShieldAngle) * (Reflecting ? 1.25f : 1f) * 0.5f);
        if (to.LengthSquared() > 0.04f && fwd.AngleTo(to.Normalized()) > half) return dmg;
        var blocked = dmg * spec.Guard;
        var leak = (dmg - blocked) * (1f - FGear.ShieldCut);
        sh.Charge -= blocked * FGear.ShieldUse / Mathf.Max(sh.W.Durability * FGear.ShieldDurability, 1f);
        var at = GlobalPosition + Vector3.Up * 1.2f + (to.LengthSquared() > 0.04f ? to.Normalized() : fwd) * 0.9f;
        Effects.Sparks(World, at, (to.LengthSquared() > 0.04f ? to.Normalized() : fwd));
        Sfx.At("e5_shieldblock", at, -4f, (float)GD.RandRange(0.9, 1.05));
        // Recul à la parade (réduit par les renforts de bouclier).
        if (to.LengthSquared() > 0.04f)
            Velocity -= to.Normalized() * Mathf.Min(6f, 1f + blocked / MaxHealth * 30f) * (1f - FGear.KnockbackCut);
        if (sh.Charge <= 0)
        {
            sh.Charge = 0;
            sh.ReloadLeft = sh.W.Reload;
            Sfx.Ui("empty", -2f, 0.5f);
            World.Hud?.Toast($"{spec.Name} : bouclier brisé");
        }
        return leak;
    }

    /// <summary>Renvoi d'un projectile ennemi par la garde du bouclier en main : coût en durabilité (part de la durabilité maximale).</summary>
    public void OnShieldReflect()
    {
        var sh = Weapon;
        if (sh?.W.T.Spec is not { Kind: Edf5Kind.Shield } spec) return;
        sh.Charge = Mathf.Max(0, sh.Charge - spec.ReflectCost * FGear.ShieldUse / FGear.ShieldDurability);
        if (sh.Charge <= 0) { sh.ReloadLeft = sh.W.Reload; Sfx.Ui("empty", -2f, 0.5f); }
    }

    /// <summary>Rayon de renvoi du bouclier tenu en garde (0 sans garde).</summary>
    public float ReflectRadius => Reflecting && Weapon?.W.T.Spec is { Kind: Edf5Kind.Shield } sp ? sp.ReflectRadius : 0f;

    /// <summary>Direction de la garde (avant de la caméra, à plat).</summary>
    public Vector3 GuardForward => -new Basis(Vector3.Up, _yaw).Z;
}
