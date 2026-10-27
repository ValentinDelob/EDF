using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Godot;

namespace Rempart;

/// <summary>
/// Audit des armes (plan de test, docs/PLAN_TEST_ARMES.md) : --weapon-audit[=filtre] avec --class=N. Pour chaque arme, au
/// maximum d'étoiles, contre une fourmi immobile très résistante placée à une distance adaptée : tir (rafale de mesure), puis
/// rechargement complet ; journal « [audit] » : valeurs attendues d'EDF 5 (BEGIN), tirs (SHOT), dégâts reçus par la cible à
/// chaque image (HIT), rechargement (RELOAD), fin (END). tools/weapon_audit.py en tire le rapport PASS / ÉCHEC par mesure.
/// </summary>
public static partial class AutoTest
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string F(float v) => v.ToString("0.###", Inv);

    /// <summary>Distance de mesure selon la nature de l'arme (assez près pour toucher, assez loin pour mesurer la vitesse).</summary>
    private static float AuditDistance(Edf5Spec s, WeaponInstance w) => s.Kind switch
    {
        Edf5Kind.Flame => s.Family is "flamecannon" ? 18f : Mathf.Min(10f, w.Range * 0.5f),
        Edf5Kind.Strike => s.Family is "blade" or "forceax" or "roller" ? 12f : 6f,
        Edf5Kind.Pierce => s.Family == "spinedriver" ? 30f : 7f,
        Edf5Kind.Arc => Mathf.Min(10f, w.Range * 0.5f),
        Edf5Kind.HandGrenade => 16f,
        // Pétards : gerbe à courte portée.
        Edf5Kind.Firecracker => 8f,
        Edf5Kind.Bomb => 9f,
        Edf5Kind.Impulse => 14f,
        Edf5Kind.Mortar => s.Family == "crusher" ? 90f : 60f,
        Edf5Kind.Missile => 45f,
        Edf5Kind.Homing => 35f,
        Edf5Kind.Gunship or Edf5Kind.Satellite => 55f,
        Edf5Kind.AirMissile => 80f,
        Edf5Kind.Artillery or Edf5Kind.Bomber => 22f,
        Edf5Kind.Limpet => 20f,
        Edf5Kind.Sentry or Edf5Kind.Robot => 18f,
        Edf5Kind.Orb => 22f,
        _ when s.Pellets > 1 => Mathf.Clamp(w.Range * 0.25f, 5f, 12f),
        _ => Mathf.Clamp(w.Range * 0.15f, 8f, 60f),
    };

    private static void WeaponAudit(App app, Player p, string filter)
    {
        var source = p.Def.Id switch
        {
            SoldierClass.Airborne => Edf5Weapons.WingDiver, SoldierClass.Heavy => Edf5Weapons.Fencer,
            SoldierClass.Support => Edf5Weapons.AirRaider, _ => Edf5Weapons.Ranger,
        };
        var specs = source.Where(s => s.Kind is not (Edf5Kind.Core or Edf5Kind.Gear)
                                      && (filter is "" or "all" || s.Family == filter || s.Id == filter || s.Category == filter
                                          || s.Kind.ToString() == filter || filter.Contains(',') && filter.Split(',').Contains(s.Id)))
            .ToList();
        if (p.Def.Id == SoldierClass.Airborne) { p.Core = Grant("vzplasmacore"); p.Energy = p.MaxEnergy; }
        GD.Print($"[audit] START {specs.Count} classe={p.Def.Id}");
        Player.BotStill = true;
        Ally.HoldFire = true;
        Enemy.NoWeakPoints = true;
        var runner = new AuditRunner { App = app, P = p, Specs = specs, Home = p.GlobalPosition };
        app.AddChild(runner);
    }

    /// <summary>Pilote de l'audit, image par image (physique) : une arme après l'autre.</summary>
    private partial class AuditRunner : Node
    {
        public App App;
        public Player P;
        public List<Edf5Spec> Specs;
        /// <summary>Position de départ du soldat, reprise à chaque arme (les charges et ruées du Fencer le déplacent).</summary>
        public Vector3 Home;
        private int _i = -1;
        private Edf5Spec _s;
        private WeaponState _ws;
        private Enemy _target;
        private float _t, _lastHp, _phaseT;
        private int _lastAmmo, _shots, _lastFired, _moves, _retries;
        private bool _dealt;
        private bool _reloading;
        private string _phase = "";
        private float _fireTime;

        public override void _Ready() => Next();

        private void Log(string line) => GD.Print($"[audit] {line}");

        private void Next()
        {
            _i++;
            Player.BotForceFire = false;
            var w = App.CurrentWorld;
            if (_i >= Specs.Count || w == null || !IsInstanceValid(P))
            {
                Log($"FIN {_i}");
                Player.BotLookAt = null;
                // Fin du plan : le jeu se ferme (le journal est complet).
                var tree = GetTree();
                tree.CreateTimer(0.5f).Timeout += () => tree.Quit();
                QueueFree();
                return;
            }
            _s = Specs[_i];
            P.GlobalPosition = Home;
            P.Velocity = Vector3.Zero;
            var inst = Grant(_s.Id);
            var d = AuditDistance(_s, inst);
            // Ligne de tir dégagée (aucun décor entre le soldat et la cible, ni juste derrière) : on tourne autour du soldat.
            var fwd = P.AimForward with { Y = 0 };
            fwd = fwd.LengthSquared() > 0.01f ? fwd.Normalized() : Vector3.Forward;
            var at = P.GlobalPosition + fwd * d;
            var space = P.GetWorld3D().DirectSpaceState;
            for (var k = 0; k < 24; k++)
            {
                var dir = fwd.Rotated(Vector3.Up, Mathf.DegToRad(15f * k));
                var cand = P.GlobalPosition + dir * d;
                cand.Y = w.GroundHeight(cand);
                var eye = P.GlobalPosition + Vector3.Up * 1.8f;
                // Mêmes couches que la visée (décor, véhicules).
                var q = PhysicsRayQueryParameters3D.Create(eye, cand + Vector3.Up * 2.2f + dir * 6f, 1u | (1u << 2));
                q.Exclude = new Godot.Collections.Array<Rid> { P.GetRid() };
                var hit = space.IntersectRay(q);
                if (hit.Count == 0 || hit["collider"].AsGodotObject() is Enemy) { at = cand; fwd = dir; break; }
            }
            at.Y = w.GroundHeight(at);
            if (_target == null || !IsInstanceValid(_target) || _target.State == EnemyState.Dead || _target.Health < 1e6f)
            {
                _target = w.HostSpawnEnemy(EnemyType.BlackAnt, at);
                _target.MaxHealth = _target.Health = 5e6f;
            }
            else _target.GlobalPosition = at;
            // Les autres ennemis : loin derrière le soldat, hors de l'écran (les lance-missiles ne répartissent pas leurs
            // verrouillages sur eux).
            var side = fwd.Cross(Vector3.Up);
            var idx = 0;
            foreach (var e in w.EnemiesById.Values)
                if (e != _target && (e.GlobalPosition - P.GlobalPosition).Dot(fwd) > -200f)
                    e.GlobalPosition = P.GlobalPosition - fwd * 260f + side * (idx++ % 20 - 10) * 6f;
            // Wing Diver : cœur plasma assez grand pour la charge de l'arme (Raijin Alpha : Big Bang Core).
            if (P.Def.Id == SoldierClass.Airborne)
            {
                var core = inst.EnergyCost > 1500f ? "bigbangcore" : "vzplasmacore";
                if (P.Core?.TemplateId != core) P.Core = Grant(core);
                P.Energy = P.MaxEnergy;
            }
            _ws = P.Arm(inst);
            P.Weapons[0] = _ws;
            P.Current = 0;
            Player.BotLookAt = _target.Center;
            // Valeurs attendues (tables d'EDF 5, maximum d'étoiles).
            // Même règle que le jeu (Edf5Fire) : atténuation forte des fusils à pompe et des canons à mitraille.
            var shotgun = _s.Category == "Fusil à pompe" || _s.Family is "dexter" or "cannonshot";
            Log($"BEGIN id={_s.Id} kind={_s.Kind} family={_s.Family} supply={_s.Supply} d={F(d)} dmg={F(inst.Damage)} pellets={_s.Pellets} " +
                $"far={F(_s.FarDamage)} fexp={(shotgun ? 2.5f : 1f).ToString(Inv)} rof={F(_s.Rof.Present ? inst.FireRate : 0)} burst={_s.Burst} " +
                $"mag={inst.Magazine} reload={F(_s.Reload.Present ? inst.Reload : 0)} pt={(_s.PointReload ? 1 : 0)} speed={F(_s.Speed.Present ? inst.ShotSpeed : 0)} " +
                $"range={F(_s.Range.Present ? inst.Range : 0)} blast={F(inst.BlastRadius)} lock={F(inst.LockTime)} spin={F(inst.SpinUp)} " +
                $"shots={_s.Shots} dmgmul={F(P.DamageMul)} noreload={(_ws.NoReload ? 1 : 0)} guide={(_s.GuideOnly ? 1 : 0)} fuse={F(_s.Fuse)}");
            _t = 0;
            _phaseT = 0;
            _shots = 0;
            _lastAmmo = _ws.Ammo;
            _lastFired = Player.ShotsFired;
            _lastHp = _target.Health;
            _reloading = false;
            _phase = "aim";
            _moves = 0;
            _retries = 0;
            _dealt = false;
            // Temps de tir : un chargeur ou 6 s au plus (frappes, charges et verrouillages un peu plus).
            _fireTime = _s.Kind switch
            {
                Edf5Kind.Missile or Edf5Kind.Homing => 6f, Edf5Kind.Strike => 7f,
                _ when _s.Supply is Edf5Supply.Charge or Edf5Supply.ChargeBurst => 6f,
                _ => Mathf.Min(6f, _ws.W.Magazine / Mathf.Max(_ws.W.FireRate, 0.2f) + 0.5f),
            };
        }

        public override void _PhysicsProcess(double delta)
        {
            if (_s == null || !IsInstanceValid(P) || _target == null || !IsInstanceValid(_target)) return;
            var dt = (float)delta;
            _t += dt;
            _phaseT += dt;
            // Dégâts reçus par la cible à cette image.
            var hp = _target.Health;
            if (hp < _lastHp - 0.001f) { Log($"HIT t={F(_t)} dmg={F(_lastHp - hp)}"); _dealt = true; }
            _lastHp = hp;
            if (_target.Health < 1e6f) { _target.Health = 5e6f; _lastHp = _target.Health; }
            // Coups partis : munitions consommées, ou coups comptés par le soldat (armes à charge : la munition est remise puis tirée).
            var fired = Mathf.Max(_lastAmmo - _ws.Ammo, Player.ShotsFired - _lastFired);
            _lastFired = Player.ShotsFired;
            if (fired > 0)
            {
                for (var k = 0; k < fired; k++) Log($"SHOT t={F(_t)} phase={_phase}");
                // Distance réelle du premier coup : bouche du canon → surface visée (mesure de la vitesse des projectiles).
                if (_shots == 0)
                {
                    var ap = P.AimPoint(400f, out var col);
                    Log($"AIM dsurf={F(P.Model.Muzzle.GlobalPosition.DistanceTo(ap))} sur={(col as Node)?.Name ?? "-"} cam={F(P.Camera.GlobalPosition.DistanceTo(ap))}");
                }
                _shots += fired;
            }
            _lastAmmo = _ws.Ammo;
            if (_ws.Reloading != _reloading)
            {
                _reloading = _ws.Reloading;
                Log($"RELOAD t={F(_t)} {(_reloading ? "start" : "end")} ammo={_ws.Ammo}");
            }
            Player.BotLookAt = _target.Center;
            if (Screenshot.CmdArg("audit-trace") != null && Mathf.PosMod(_t, 0.25f) < dt)
            {
                var ap = P.AimPoint(400f, out var col);
                Log($"TRACE t={F(_t)} ph={_phase} p={P.GlobalPosition} tg={_target.Center} aim={ap} sur={(col as Node)?.Name ?? "-"} ammo={_ws.Ammo} hp={_target.Health} st={_target.State} fly={P.Flying} en={P.Energy}");
            }
            switch (_phase)
            {
                case "aim":
                    // Laisse au soldat le temps de se tourner vers la cible.
                    if (_phaseT <= 0.8f) break;
                    // Décor dans la ligne de visée (gravats laissés par une arme précédente…) : la cible tourne autour du soldat.
                    P.AimPoint(400f, out var seen);
                    if (seen is not Enemy && _moves < 8)
                    {
                        _moves++;
                        // Autre direction, un peu plus près (relief, décor).
                        var off = (_target.GlobalPosition - P.GlobalPosition) * 0.88f;
                        off.Y = 0;
                        var np = P.GlobalPosition + off.Rotated(Vector3.Up, Mathf.DegToRad(40f * _moves));
                        np.Y = App.CurrentWorld.GroundHeight(np);
                        _target.GlobalPosition = np;
                        _phaseT = 0;
                        break;
                    }
                    _phase = "fire";
                    _phaseT = 0;
                    Player.BotForceFire = true;
                    break;
                case "fire":
                    if (_phaseT < _fireTime && !(_ws.Ammo <= 0 && _shots > 0 && !_ws.Reloading && _ws.NoReload)) break;
                    // Arme à charge : la charge en cours est abandonnée (relâcher tirerait un coup à charge partielle).
                    if (_s.Supply == Edf5Supply.Charge) _ws.Charge = 0;
                    Player.BotForceFire = false;
                    _phase = "reload";
                    _phaseT = 0;
                    // Mesure du rechargement : chargeur vidé de force, puis rechargement complet attendu.
                    if (_s.Reload.Present && !_s.PointReload && !_ws.NoReload && _s.Kind is not (Edf5Kind.Strike or Edf5Kind.Shield)
                        && _s.Supply is Edf5Supply.Ammo or Edf5Supply.Reload)
                    {
                        if (!_ws.Reloading) { _ws.Ammo = 0; _lastAmmo = 0; _ws.StartReload(); }
                    }
                    else _phase = "settle";
                    break;
                case "reload":
                    if (!_ws.Reloading && _phaseT > 0.2f || _phaseT > _ws.W.Reload * 1.6f + 3f) { _phase = "settle"; _phaseT = 0; }
                    break;
                case "settle":
                    // Projectiles et frappes encore en vol.
                    var wait = _s.Kind switch
                    {
                        Edf5Kind.Bomber => 14f,
                        Edf5Kind.Artillery or Edf5Kind.AirMissile or Edf5Kind.Satellite => 9f,
                        Edf5Kind.Gunship or Edf5Kind.Sentry or Edf5Kind.Robot or Edf5Kind.Missile or Edf5Kind.Mortar or Edf5Kind.Orb => 5f,
                        Edf5Kind.Napalm => 4f,
                        _ => 1.5f,
                    };
                    // Grenades à retardement : la mèche brûle encore.
                    if (_s.Fuse > 0) wait = Mathf.Max(wait, _s.Fuse + 1f);
                    if (_s.Kind is Edf5Kind.Bomb or Edf5Kind.Limpet && _phaseT < dt * 1.5f) P.Edf5DetonateForTest();
                    if (_phaseT < wait) break;
                    // Frappes aériennes : coups dispersés dans la zone d'attaque (« Attack Range ») ; si aucun n'a atteint la
                    // cible, la frappe est redemandée (deux fois au plus), comme le referait un joueur.
                    if (!_dealt && _retries < 2 && _s.Kind is Edf5Kind.Artillery or Edf5Kind.Bomber or Edf5Kind.Gunship or Edf5Kind.AirMissile or Edf5Kind.Satellite)
                    {
                        _retries++;
                        _ws.Ammo = _ws.W.Magazine;
                        _ws.ReloadLeft = 0;
                        _lastAmmo = _ws.Ammo;
                        Log($"RETRY t={F(_t)}");
                        _phase = "fire";
                        _phaseT = 0;
                        Player.BotForceFire = true;
                        break;
                    }
                    Log($"END id={_s.Id} shots={_shots} t={F(_t)}");
                    Next();
                    break;
            }
        }
    }
}
