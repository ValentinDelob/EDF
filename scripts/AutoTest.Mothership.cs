using System;
using System.Globalization;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Tests et vitrine du vaisseau-mère (voir docs/ARCHITECTURE.md, « Tests automatisés »).</summary>
public static partial class AutoTest
{
    /// <summary>
    /// --showcase=mothership : vaisseau-mère figé en position au-dessus de la ville (IA figée, actions forcées possibles),
    /// dimensions et triangles dans le journal. Sans vitrine, le vaisseau de la mission (22) sert aux options :
    /// --mothership-act=open@6,charge@8,launch@12,turret@15,phase1@20,phase2@30,close@40,kill@50 : actions forcées (hôte) ;
    /// --mothership-cam=D,H,A[,deg/s] : caméra libre à D m du vaisseau (à l'horizontale), H m du sol, azimut A (0 = devant),
    /// qui vise le cœur du canon (--mothership-look=ship : le centre de la coque ; =ground : le point visé par le canon) ;
    /// --mothership-shots=T1,T2 : captures aux instants de mission ; --mothership-hp=F : PV × F ; --mothership-log : journal
    /// toutes les 2 s ; --mothership-test=T : vérification des dégâts localisés (coque fermée, cœur, explosion, tourelle),
    /// journal « [mothership] dégâts ok ».
    /// </summary>
    private static void MothershipTest(App app, string dir)
    {
        var showcase = Screenshot.CmdArg("showcase") == "mothership";
        var acts = Screenshot.CmdArg("mothership-act");
        var camArg = Screenshot.CmdArg("mothership-cam");
        var shots = Screenshot.CmdArg("mothership-shots");
        var hp = Screenshot.CmdArg("mothership-hp");
        var log = Screenshot.CmdArg("mothership-log") != null;
        var test = Screenshot.CmdArg("mothership-test");
        if (!showcase && acts == null && camArg == null && shots == null && hp == null && !log && test == null) return;
        var tree = app.GetTree();
        Enemy Ship() => app.CurrentWorld?.EnemiesById.Values.FirstOrDefault(e => e.IsMothership && e.State != EnemyState.Dead);
        Enemy last = null;
        Enemy ShipOrLast() => Ship() ?? last;

        if (showcase)
        {
            Player.BotStill = true;
            Player.BotHoldFire = true;
            void Spawn()
            {
                var w = app.CurrentWorld;
                // Après 3 s : le vaisseau de la mission 22 (apparu à 2 s) sert de vitrine, sinon on en crée un.
                if (w?.LocalPlayer == null || !GodotObject.IsInstanceValid(w.LocalPlayer) || w.Elapsed < 3f) { tree.CreateTimer(0.5f).Timeout += Spawn; return; }
                Enemy.Frozen = true;
                if (Ship() == null) w.HostSpawnEnemy(EnemyType.Mothership, new Vector3(0, 0, -40f));
                tree.CreateTimer(2f).Timeout += () =>
                {
                    if (Ship() is not { } s) return;
                    s.MaxHealth = s.Health = 1e8f;
                    var b = s.Model.Bounds();
                    GD.Print($"[taille] Mothership : longueur {b.Size.Z:0.0} m, envergure {b.Size.X:0.0} m, épaisseur {b.Size.Y:0.0} m, altitude {s.GlobalPosition.Y:0} m, {s.Model.TriangleCount()} triangles");
                };
            }
            Spawn();
        }

        void Poll()
        {
            if (Ship() is { } s) last = s;
            if (hp != null && Ship() is { } sh && float.TryParse(hp, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && sh.Health > sh.MaxHealth * f + 1f && !_msHpDone)
            {
                _msHpDone = true;
                sh.Health = sh.MaxHealth * f;
                GD.Print($"[mothership] PV ramenés à {sh.Health:0}/{sh.MaxHealth:0}");
            }
            tree.CreateTimer(0.25f).Timeout += Poll;
        }
        Poll();

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
                if (w != null && next < list.Count && w.Elapsed >= list[next].At && Ship() is { } s)
                {
                    GD.Print($"[mothership] action forcée : {list[next].Act} (t={w.Elapsed:0.0})");
                    s.MothershipForce(list[next].Act);
                    next++;
                }
                if (next < list.Count) tree.CreateTimer(0f).Timeout += Acts;
            }
            Acts();
        }

        if (camArg != null)
        {
            var v = FloatList(camArg);
            var look = Screenshot.CmdArg("mothership-look") ?? "core";
            Camera3D cam = null;
            var t0 = 0f;
            void Orbit()
            {
                var w = app.CurrentWorld;
                if (w != null && ShipOrLast() is { } s && GodotObject.IsInstanceValid(s))
                {
                    if (cam == null || !GodotObject.IsInstanceValid(cam))
                    {
                        cam = new Camera3D { Fov = 62f, Far = 3000f };
                        w.AddChild(cam);
                        cam.MakeCurrent();
                        t0 = w.Elapsed;
                    }
                    var d = v.Length > 0 ? v[0] : 220f;
                    var h = v.Length > 1 ? v[1] : 2f;
                    var az = Mathf.DegToRad((v.Length > 2 ? v[2] : 30f) + (v.Length > 3 ? v[3] : 0f) * (w.Elapsed - t0));
                    var c = s.GlobalPosition;
                    var fwd = new Vector3(s.GlobalBasis.Z.X, 0, s.GlobalBasis.Z.Z).Normalized();
                    var off = new Basis(Vector3.Up, az) * fwd * d;
                    cam.GlobalPosition = new Vector3(c.X + off.X, h, c.Z + off.Z);
                    var target = look switch
                    {
                        "ship" => c,
                        "ground" => s.MothershipDangerZone ?? s.Model.MsCorePos with { Y = 0 },
                        _ => s.State == EnemyState.Dead ? c : s.Model.MsCorePos,
                    };
                    if (cam.GlobalPosition.DistanceTo(target) > 1f) cam.LookAt(target, Vector3.Up);
                }
                tree.CreateTimer(0f).Timeout += Orbit;
            }
            Orbit();
        }

        if (shots != null && dir != null)
        {
            var times = FloatList(shots).OrderBy(x => x).ToList();
            var idx = 0;
            void Shots()
            {
                var w = app.CurrentWorld;
                if (w != null && idx < times.Count && w.Elapsed >= times[idx])
                {
                    Screenshot.Take(app.GetViewport(), $"mothership_t{times[idx]:0}");
                    idx++;
                }
                if (idx < times.Count) tree.CreateTimer(0f).Timeout += Shots;
            }
            Shots();
        }

        if (log)
        {
            void Log()
            {
                if (Ship() is { } s)
                {
                    var turrets = Enumerable.Range(0, BugModel.MsTurretCount).Count(i => s.Model.MsTurretDeployed(i));
                    GD.Print($"[mothership] {s.State} pos {s.GlobalPosition.Snapped(Vector3.One)} PV {s.Health:0}/{s.MaxHealth:0} phase {s.MothershipPhase} canon {(s.MothershipOpen ? "ouvert" : "fermé")} visée {(s.MothershipDangerZone?.Snapped(Vector3.One).ToString() ?? "-")} tourelles {turrets} ennemis {app.CurrentWorld.EnemiesById.Count}");
                }
                tree.CreateTimer(2f).Timeout += Log;
            }
            Log();
        }

        if (test != null && float.TryParse(test, NumberStyles.Float, CultureInfo.InvariantCulture, out var testAt))
        {
            var step = 0;
            var errors = 0;
            float before = 0;
            void Check(string what, float expected)
            {
                var s = Ship();
                var took = before - s.Health;
                var ok = Mathf.Abs(took - expected) <= Mathf.Max(1f, expected * 0.01f);
                if (!ok) errors++;
                GD.Print($"[mothership] {what} : -{took:0} (attendu {expected:0}) {(ok ? "ok" : "ERREUR")}");
            }
            void Run()
            {
                var w = app.CurrentWorld;
                var s = Ship();
                if (w == null || s == null) { tree.CreateTimer(0.5f, false).Timeout += Run; return; }
                switch (step)
                {
                    case 0:
                        s.MothershipForce("close");
                        before = s.Health;
                        w.HostDamageEnemy(s.Id, 300f, s.GlobalPosition, Net.I.MyId, false);
                        Check("coque, canon fermé", 0f);
                        before = s.Health;
                        w.HostDamageEnemy(s.Id, 300f, s.Model.MsCorePos, Net.I.MyId, true);
                        Check("cœur, canon fermé", 0f);
                        s.MothershipForce("open");
                        break;
                    case 1:
                        before = s.Health;
                        w.HostDamageEnemy(s.Id, 150f, s.Model.MsCorePos, Net.I.MyId, true);
                        Check("cœur visé, canon ouvert (×4)", 400f);
                        before = s.Health;
                        Enemy.BlastOrigin = s.Model.MsCorePos + Vector3.Down * 6f;
                        w.HostDamageEnemy(s.Id, 100f, s.Center, Net.I.MyId, false);
                        Enemy.BlastOrigin = null;
                        Check("explosion sous le cœur (×3)", 300f);
                        break;
                    case 2:
                        // Tourelle : elle encaisse jusqu'à sa destruction, puis 2 % des PV du vaisseau.
                        var ti = Enumerable.Range(0, BugModel.MsTurretCount).First(i => s.Model.MsTurretDeployed(i));
                        var at = s.Model.MsTurretPos(ti) + s.GlobalBasis.Y * -2f;
                        before = s.Health;
                        w.HostDamageEnemy(s.Id, 500f, at, Net.I.MyId, false);
                        Check($"tourelle {ti} intacte", 0f);
                        before = s.Health;
                        w.HostDamageEnemy(s.Id, s.MothershipTurretHealth(ti) + 1f, at, Net.I.MyId, false);
                        Check($"tourelle {ti} détruite (2 %)", s.MaxHealth * 0.02f);
                        break;
                    default:
                        GD.Print(errors == 0 ? "[mothership] dégâts ok" : $"[mothership] dégâts : {errors} ERREUR(S)");
                        return;
                }
                step++;
                tree.CreateTimer(step == 1 ? 3f : 0.5f, false).Timeout += Run;
            }
            tree.CreateTimer(testAt, false).Timeout += Run;
        }
    }

    private static bool _msHpDone;
}
