using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Armes d'EDF 5 (Fusilier) :
/// --weapons=id1,id2 : équipe ces armes (profil de test, toutes caractéristiques au maximum d'étoiles) ;
/// --edf5-fire[=famille|catégorie|id|all] : stand de tir. Les ennemis sont figés ; une fourmi noire très résistante sert de
/// cible ; le soldat reçoit chaque arme tour à tour, tire 2 s, puis le journal « [edf5] » donne les coups tirés et les
/// dégâts reçus par la cible (« ERREUR » si une arme offensive n'a rien infligé). Fin : « [edf5] fin : n armes, k erreurs ».
/// </summary>
public static partial class AutoTest
{
    public static void Edf5Tests(App app)
    {
        if (Screenshot.CmdArg("weapons") is { } ids)
        {
            var list = ids.Split(',').Select(s => s.Trim()).Where(Weapons.Exists).ToList();
            var cls = Weapons.Get(list.FirstOrDefault()).Class;
            Profile.I.Data.SelectedClass = cls;
            for (var i = 0; i < list.Count; i++) Profile.I.Equip(cls, i, Grant(list[i]));
        }
        // --weapon-cam=id1,id2… : gros plan de profil sur le soldat, qui change d'arme toutes les 2,5 s (captures).
        if (Screenshot.CmdArg("weapon-cam") is { } cam)
        {
            // Classe de la première arme de la liste (Fusilier par défaut).
            var first = cam.Split(',').FirstOrDefault(Weapons.Exists);
            Profile.I.Data.SelectedClass = first != null ? Weapons.Get(first).Class : SoldierClass.Rifleman;
            Enemy.Frozen = true;
            Player.BotStill = true;
            WhenPlayerReady(app, 2f, p => WeaponCam(app, p, cam));
        }
        if (Screenshot.CmdArg("edf5-fire") is { } filter)
        {
            if (Screenshot.CmdArg("class") == null) Profile.I.Data.SelectedClass = SoldierClass.Rifleman;
            Player.BotAiming = Screenshot.CmdArg("aim") != null;
            Enemy.Frozen = true;
            WhenPlayerReady(app, 2f, p => FireRange(app, p, filter));
        }
    }

    /// <summary>Exemplaire de l'arsenal de test, toutes caractéristiques au maximum.</summary>
    private static WeaponInstance Grant(string id)
    {
        var w = new WeaponInstance { TemplateId = id, Level = Weapons.Get(id).Spec?.Level ?? 0 };
        foreach (var st in System.Enum.GetValues<WeaponStat>()) w.Stars[(int)st] = w.MaxStarsOf(st);
        return Profile.I.Grant(w).Weapon;
    }

    private static void WeaponCam(App app, Player p, string ids)
    {
        var list = ids is "" or "all"
            ? Edf5Weapons.Ranger.GroupBy(s => s.Family).Select(g => g.First().Id).ToList()
            : ids.Split(',').Where(Weapons.Exists).ToList();
        var w = app.CurrentWorld;
        var fwd = p.AimForward with { Y = 0 };
        fwd = fwd.LengthSquared() > 0.01f ? fwd.Normalized() : Vector3.Forward;
        var right = fwd.Cross(Vector3.Up).Normalized();
        Player.BotLookAt = p.GlobalPosition + fwd * 30f + Vector3.Up * 1.4f;
        var cam = new Camera3D { Fov = 34, Current = true };
        w.AddChild(cam);
        var focus = p.GlobalPosition + Vector3.Up * 1.35f + fwd * 0.25f;
        cam.GlobalPosition = focus + right * 2.4f + fwd * 0.6f + Vector3.Up * 0.15f;
        cam.LookAt(focus, Vector3.Up);
        cam.MakeCurrent();
        var i = 0;
        void Next()
        {
            if (i >= list.Count || !GodotObject.IsInstanceValid(p)) return;
            var id = list[i++];
            p.Weapons[0] = p.Arm(Grant(id));
            p.Current = 0;
            p.Aiming = true;
            GD.Print($"[arme] {id} « {Weapons.Get(id).Name} »");
            app.GetTree().CreateTimer(1.6f).Timeout += () => Shot(app, "arme_" + id);
            app.GetTree().CreateTimer(2.5f).Timeout += Next;
        }
        Next();
    }

    private static void FireRange(App app, Player p, string filter)
    {
        var source = p.Def.Id switch
        {
            SoldierClass.Airborne => Edf5Weapons.WingDiver, SoldierClass.Heavy => Edf5Weapons.Fencer,
            SoldierClass.Support => Edf5Weapons.AirRaider, _ => Edf5Weapons.Ranger,
        };
        // Équipements et boucliers : pas de tir (les boucliers ont leur propre essai, --shield-test).
        var specs = source.Where(s => s.Kind is not (Edf5Kind.Core or Edf5Kind.Gear or Edf5Kind.Shield) && (filter is "" or "all" || s.Family == filter || s.Id == filter || filter.Contains(',') && filter.Split(',').Contains(s.Id) || s.Category == filter
                                                  || s.Kind.ToString() == filter)).ToList();
        // Wing Diver : cœur le plus puissant (les armes de haut niveau coûtent plus que l'énergie d'un cœur de départ).
        if (p.Def.Id == SoldierClass.Airborne) { p.Core = Grant("vzplasmacore"); p.Energy = p.MaxEnergy; }
        GD.Print($"[edf5] stand de tir : {specs.Count} armes");
        Player.BotStill = true;
        Player.BotForceFire = true;
        var errors = 0;
        var i = 0;
        Enemy target = null;
        void Next()
        {
            var w = app.CurrentWorld;
            if (i >= specs.Count || w == null || !GodotObject.IsInstanceValid(p))
            {
                GD.Print($"[edf5] fin : {i} armes, {errors} erreurs");
                Player.BotForceFire = false;
                Player.BotLookAt = null;
                return;
            }
            var s = specs[i++];
            // Distance de la cible selon la portée de l'arme.
            var dist = s.Kind switch
            {
                Edf5Kind.Flame => s.Family == "flamecannon" ? 22f : 9f,
                Edf5Kind.Firecracker or Edf5Kind.HandGrenade => 16f,
                Edf5Kind.Bomb => 9f,
                Edf5Kind.Impulse => 14f,
                Edf5Kind.Arc => s.Family == "phalanx" ? 20f : 12f,
                Edf5Kind.Lance or Edf5Kind.Spark or Edf5Kind.Beam => 26f,
                Edf5Kind.Orb => 22f,
                Edf5Kind.Strike => s.Family is "blade" or "forceax" or "roller" ? 12f : 6f,
                Edf5Kind.Gunship or Edf5Kind.Satellite => 55f,
                Edf5Kind.AirMissile => 80f,
                Edf5Kind.Artillery or Edf5Kind.Bomber => 22f,
                Edf5Kind.Limpet => 20f,
                Edf5Kind.Sentry or Edf5Kind.Robot => 18f,
                Edf5Kind.Pierce => s.Family == "spinedriver" ? 30f : 7f,
                Edf5Kind.Mortar => s.Family == "crusher" ? 90f : 60f,
                _ when s.Family is "dexter" or "cannonshot" => 18f,
                _ when s.Family == "flamerevolver" => 12f,
                _ when s.Category == "Fusil à pompe" => 18f,
                _ => 34f,
            };
            var fwd = p.AimForward with { Y = 0 };
            fwd = fwd.LengthSquared() > 0.01f ? fwd.Normalized() : Vector3.Forward;
            if (target == null || !GodotObject.IsInstanceValid(target) || target.State == EnemyState.Dead || target.Health < 1e6f)
            {
                target = w.HostSpawnEnemy(EnemyType.BlackAnt, p.GlobalPosition + fwd * dist);
                target.MaxHealth = target.Health = 2e6f;
            }
            else target.GlobalPosition = (p.GlobalPosition + fwd * dist) with { Y = w.GroundHeight(p.GlobalPosition + fwd * dist) };
            // Éloigne les autres insectes (figés) de la ligne de tir.
            foreach (var e in w.EnemiesById.Values)
                if (e != target && e.GlobalPosition.DistanceTo(p.GlobalPosition) < 60f) e.GlobalPosition += (e.GlobalPosition - p.GlobalPosition).Normalized() * 120f;
            var ws = p.Arm(Grant(s.Id));
            p.Weapons[0] = ws;
            p.Current = 0;
            Player.BotLookAt = target.Center;
            var before = target.Health;
            var ammo0 = ws.Ammo;
            var shots = 0;
            var last = ws.Ammo;
            var watch = new Timer { WaitTime = 0.05f, Autostart = true };
            app.AddChild(watch);
            watch.Timeout += () => { if (ws.Ammo < last) shots += last - ws.Ammo; last = ws.Ammo; };
            app.GetTree().CreateTimer(2f).Timeout += () =>
            {
                Player.BotForceFire = false;
                // Bombes C : rechargement = déclenchement.
                if (s.Kind is Edf5Kind.Bomb or Edf5Kind.Limpet) p.Edf5DetonateForTest();
                // Frappes aériennes : délai d'arrivée et durée de la frappe.
                var wait = s.Kind switch
                {
                    Edf5Kind.Napalm => 3f, Edf5Kind.Artillery => 9f, Edf5Kind.Bomber => 8f, Edf5Kind.AirMissile => 9f, Edf5Kind.Satellite => 9f,
                    Edf5Kind.Gunship => 5f, Edf5Kind.Sentry or Edf5Kind.Robot => 5f, _ => 1.6f,
                };
                app.GetTree().CreateTimer(wait).Timeout += () =>
                {
                    watch.QueueFree();
                    var dmg = GodotObject.IsInstanceValid(target) ? before - target.Health : 0;
                    var heals = s.Kind is Edf5Kind.Reverser or Edf5Kind.ReverseShooter or Edf5Kind.Guide or Edf5Kind.Post or Edf5Kind.Bunker
                        or Edf5Kind.Decoy || s.Family == "speedstar";
                    var err = !heals && dmg <= 0;
                    if (err) errors++;
                    GD.Print($"[edf5] {s.Id} « {s.Name} » ({s.Kind}, Lv {s.Level}) : {shots} coups / {ammo0}, dégâts {dmg:0}{(err ? " ERREUR" : "")}");
                    Player.BotForceFire = true;
                    Next();
                };
            };
        }
        Next();
    }
}
