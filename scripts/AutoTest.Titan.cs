using System;
using System.Globalization;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Tests du titan reptilien (voir docs/ARCHITECTURE.md, « Tests automatisés »).</summary>
public static partial class AutoTest
{
    /// <summary>
    /// Avec un titan en jeu (mission 17, --machines=Titan ou --showcase=titan) :
    /// --titan-act=breath@8,tail@14,roar@20,plate@24 : actions forcées aux instants de mission (hôte) ;
    /// --titan-plates=T : à partir de T s, tirs sur chaque plaque jusqu'à la briser, puis sur la plaie : vérifie que la
    /// plaque encaisse (titan à 25 %), qu'elle cède, puis que la chair à vif prend ×3 ; journal « [titan] plaques ok » ;
    /// --titan-hp=F : PV de départ × F (test des phases, 0,45 → phase 1 dès l'arrivée) ;
    /// --titan-log : journal [titan] toutes les 2 s (position, état, plaques, souffle, foyers).
    /// </summary>
    private static void TitanTest(App app)
    {
        var acts = Screenshot.CmdArg("titan-act");
        var plates = Screenshot.CmdArg("titan-plates");
        var hp = Screenshot.CmdArg("titan-hp");
        var log = Screenshot.CmdArg("titan-log") != null;
        var camArg = Screenshot.CmdArg("titan-cam");
        if (acts == null && plates == null && hp == null && !log && camArg == null) return;
        var tree = app.GetTree();
        Enemy Titan() => app.CurrentWorld?.EnemiesById.Values.FirstOrDefault(e => e.IsTitan && e.State != EnemyState.Dead);
        var hpDone = false;
        void Poll()
        {
            var w = app.CurrentWorld;
            if (w != null && Titan() is { } t && !hpDone && hp != null && float.TryParse(hp, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                hpDone = true;
                t.Health = t.MaxHealth * f;
                GD.Print($"[titan] PV ramenés à {t.Health:0}/{t.MaxHealth:0}");
            }
            tree.CreateTimer(0.25f).Timeout += Poll;
        }
        Poll();

        // Actions forcées sur l'horloge de mission.
        if (acts != null)
        {
            var list = acts.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Split('@'))
                .Where(p => p.Length == 2 && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                .Select(p => (Act: p[0].Trim(), At: float.Parse(p[1], CultureInfo.InvariantCulture)))
                .OrderBy(p => p.At).ToList();
            var next = 0;
            void Acts()
            {
                var w = app.CurrentWorld;
                if (w != null && next < list.Count && w.Elapsed >= list[next].At && Titan() is { } t)
                {
                    GD.Print($"[titan] action forcée : {list[next].Act} (t={w.Elapsed:0.0})");
                    t.TitanForce(list[next].Act);
                    next++;
                }
                if (next < list.Count) tree.CreateTimer(0f).Timeout += Acts;
            }
            Acts();
        }

        // --titan-cam=D,H,A[,deg/s] : caméra libre à D m du titan de la mission (devant = azimut 0), H m au-dessus du sol,
        // qui le suit dans ses déplacements et vise son torse (captures du titan dans la ville).
        if (camArg != null)
        {
            var v = FloatList(camArg);
            Camera3D cam = null;
            var t0 = 0f;
            void Orbit()
            {
                var w = app.CurrentWorld;
                if (w != null && Titan() is { } t)
                {
                    if (cam == null || !GodotObject.IsInstanceValid(cam))
                    {
                        cam = new Camera3D { Fov = 60f, Far = 1200f };
                        w.AddChild(cam);
                        cam.MakeCurrent();
                        t0 = w.Elapsed;
                    }
                    var d = v.Length > 0 ? v[0] : 120f;
                    var h = v.Length > 1 ? v[1] : 12f;
                    var az = Mathf.DegToRad((v.Length > 2 ? v[2] : 30f) + (v.Length > 3 ? v[3] : 0f) * (w.Elapsed - t0));
                    var c = t.Center;
                    var off = t.GlobalBasis * new Vector3(Mathf.Sin(az), 0, Mathf.Cos(az)) * d;
                    var p = new Vector3(c.X + off.X, h, c.Z + off.Z);
                    cam.GlobalPosition = cam.GlobalPosition.DistanceTo(p) > 30f ? p : cam.GlobalPosition.Lerp(p, 0.05f);
                    cam.LookAt(c + Vector3.Up * 4f, Vector3.Up);
                }
                tree.CreateTimer(0f).Timeout += Orbit;
            }
            Orbit();
        }

        // Journal périodique.
        if (log)
        {
            void Log()
            {
                if (Titan() is { } t)
                {
                    var broken = Enumerable.Range(0, BugModel.TitanPlateCount).Count(t.TitanPlateBroken);
                    GD.Print($"[titan] {t.State} pos {t.GlobalPosition.Snapped(Vector3.One)} PV {t.Health:0}/{t.MaxHealth:0} phase {t.TitanPhase} plaques {broken}/{BugModel.TitanPlateCount} souffle {t.TitanBreathTime:0.0} pieds {t.Model.TitanFoot(0).Snapped(Vector3.One)} {t.Model.TitanFoot(1).Snapped(Vector3.One)}");
                }
                tree.CreateTimer(2f).Timeout += Log;
            }
            Log();
        }

        // Plaques : tirs simulés sur chaque zone (chemin hôte complet : World.HostDamageEnemy, point d'impact).
        if (plates != null && float.TryParse(plates, NumberStyles.Float, CultureInfo.InvariantCulture, out var plateAt))
        {
            var plate = 0;
            var phase = 0; // 0 : plaque intacte, 1 : plaie
            var errors = 0;
            var shots = 0;
            void Shoot()
            {
                var w = app.CurrentWorld;
                var t = Titan();
                if (w == null || t == null) { tree.CreateTimer(0.5f, false).Timeout += Shoot; return; }
                if (plate >= BugModel.TitanPlateCount)
                {
                    GD.Print(errors == 0 ? "[titan] plaques ok" : $"[titan] plaques : {errors} ERREUR(S)");
                    return;
                }
                var at = t.Model.TitanPlatePoint(plate);
                var before = t.Health;
                var plateBefore = t.TitanPlateHealth(plate);
                if (phase == 0)
                {
                    w.HostDamageEnemy(t.Id, 300f, at, Net.I.MyId, false);
                    var took = before - t.Health;
                    if (shots++ % 6 == 0) GD.Print($"[titan] plaque {plate} : titan -{took:0} (attendu 75), plaque {plateBefore:0} → {t.TitanPlateHealth(plate):0}");
                    if (Mathf.Abs(took - 75f) > 1f) { errors++; GD.Print($"[titan] ERREUR plaque {plate} intacte : {took:0} au lieu de 75"); }
                    if (t.TitanPlateBroken(plate)) { phase = 1; GD.Print($"[titan] plaque {plate} brisée"); }
                }
                else
                {
                    // Plaie : tir visé (×1,5 compté par le tireur) → ×2 côté hôte ; explosion → ×3.
                    w.HostDamageEnemy(t.Id, 150f, at, Net.I.MyId, true);
                    var took = before - t.Health;
                    GD.Print($"[titan] plaie {plate} : titan -{took:0} (attendu 300)");
                    if (Mathf.Abs(took - 300f) > 1f) { errors++; GD.Print($"[titan] ERREUR plaie {plate} : {took:0} au lieu de 300"); }
                    plate++;
                    phase = 0;
                }
                tree.CreateTimer(0.1f, false).Timeout += Shoot;
            }
            tree.CreateTimer(plateAt, false).Timeout += Shoot;
        }
    }
}
