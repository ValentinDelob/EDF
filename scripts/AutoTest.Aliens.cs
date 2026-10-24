using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Tests et vitrine des aliens (voir docs/ARCHITECTURE.md, « Tests automatisés »).</summary>
public static partial class AutoTest
{
    /// <summary>
    /// --aliens=AlienTrooper,AlienGuard,AlienFlyer,Portal : les aliens apparaissent devant le soldat (bot immobile, sans tirer)
    /// et combattent normalement ; journal [alien] toutes les 2 s (état de l'IA, posture, PV) en plus des événements
    /// (abri trouvé, charge, passe, passage du portail).
    /// --showcase=aliens : fantassin, garde, volant et portail figés en rang, dimensions et triangles dans le journal.
    /// Options : --aliens-at=X,Z,Cap (soldat déplacé), --aliens-dist=D, --aliens-cam=D,H,Azimut[,deg/s] (caméra libre autour
    /// du premier), --aliens-pose=aim|crouch|walk|run (vitrine), --aliens-shots=T1,T2 (captures), --aliens-weak=T (rafale
    /// sur les points faibles), --aliens-kill=T (mort de tous : test des chutes et explosions), --aliens-fire (le bot tire),
    /// --freeze, --god.
    /// </summary>
    /// <summary>
    /// --aliens-net (avec --autohost / --autojoin, ex. --mission=18) : chaque pair journalise toutes les 3 s les aliens qu'il
    /// voit (état, posture reçue dans l'instantané, position, portails) — contrôle de la synchronisation vue du client.
    /// </summary>
    private static void AlienNetLog(App app)
    {
        if (Screenshot.CmdArg("aliens-net") == null) return;
        var tree = app.GetTree();
        void Log()
        {
            if (app.CurrentWorld is { } w)
            {
                var list = w.EnemiesById.Values.Where(e => e.IsAlienKind && e.State != EnemyState.Dead).ToList();
                var role = Net.I.IsHost ? "hôte" : "client";
                GD.Print($"[alien-net] {role} t={w.Elapsed:0} : {list.Count} aliens/portails, " + string.Join(" ; ", list.Take(8).Select(e =>
                    $"{e.Def.Type} n°{e.Id} {e.State}{(Net.I.IsHost ? " " + e.AlienStateName : "")} acc {e.Model.AlienCrouch:0} vis {e.Model.AlienAiming:0} pos {e.GlobalPosition.Snapped(Vector3.One)}")));
            }
            tree.CreateTimer(3f).Timeout += Log;
        }
        tree.CreateTimer(3f).Timeout += Log;
    }

    private static void AlienTest(App app, string dir)
    {
        AlienNetLog(app);
        var showcase = Screenshot.CmdArg("showcase") == "aliens";
        var arg = Screenshot.CmdArg("aliens") ?? (showcase ? "AlienTrooper,AlienGuard,AlienFlyer,Portal" : null);
        if (arg == null) return;
        var tree = app.GetTree();
        // « Type*N » : N exemplaires (test de charge, ex. --aliens=AlienTrooper*30,AlienFlyer*10 --perf).
        var types = arg.Split(',', StringSplitOptions.RemoveEmptyEntries).SelectMany(s =>
        {
            var parts = s.Split('*');
            var t = Enum.TryParse<EnemyType>(parts[0].Trim(), true, out var tt) ? tt : EnemyType.AlienTrooper;
            var n = parts.Length > 1 && int.TryParse(parts[1], out var nn) ? nn : 1;
            return Enumerable.Repeat(t, n);
        }).ToList();
        var dist0 = float.TryParse(Screenshot.CmdArg("aliens-dist") ?? "", NumberStyles.Float, CultureInfo.InvariantCulture, out var dd) ? dd : 0f;
        var pose = Screenshot.CmdArg("aliens-pose") ?? "aim";
        var spawned = new List<Enemy>();
        Player.BotStill = true;
        Player.BotHoldFire = Screenshot.CmdArg("aliens-fire") == null;
        void Go()
        {
            var w = app.CurrentWorld;
            if (w?.LocalPlayer == null || !GodotObject.IsInstanceValid(w.LocalPlayer) || w.Elapsed < 1f) { tree.CreateTimer(0.5f).Timeout += Go; return; }
            Enemy.Frozen = showcase || Screenshot.CmdArg("freeze") != null;
            foreach (var act in new[] { "move_left", "move_right", "sprint" }) Input.ActionRelease(act);
            foreach (var n in w.FindChildren("*", "Node3D", true, false))
                if (n is Ally a) a.Visible = false;
            var pl = w.LocalPlayer;
            if (Screenshot.CmdArg("aliens-at") is { } at)
            {
                var v = FloatList(at);
                pl.GlobalPosition = new Vector3(v.Length > 0 ? v[0] : 0f, 1f, v.Length > 1 ? v[1] : 0f);
                if (v.Length > 2) pl.SetYaw(Mathf.DegToRad(v[2]));
            }
            var pp = pl.GlobalPosition with { Y = 0 };
            var f = pl.AimForward with { Y = 0 };
            if (Screenshot.CmdArg("aliens-at") is { } at2 && FloatList(at2) is { Length: > 2 } av)
                f = new Vector3(-Mathf.Sin(Mathf.DegToRad(av[2])), 0, -Mathf.Cos(Mathf.DegToRad(av[2])));
            f = f.LengthSquared() > 0.01f ? f.Normalized() : Vector3.Forward;
            var right = f.Cross(Vector3.Up);
            var humans = types.Count(t => t != EnemyType.Portal);
            var k = 0;
            for (var i = 0; i < types.Count; i++)
            {
                var t = types[i];
                Vector3 p;
                if (t == EnemyType.Portal) p = pp + f * (dist0 > 0 ? dist0 + 22f : 42f);
                else
                {
                    var dist = dist0 > 0 ? dist0 : showcase ? 14f : 30f;
                    var lat = humans > 8 ? (k % 8 - 3.5f) * 9f : (k - (humans - 1) * 0.5f) * (showcase ? 7f : 9f);
                    if (humans > 8) dist += (k / 8) * 10f;
                    p = pp + f * dist + right * lat;
                    k++;
                }
                var e = w.HostSpawnEnemy(t, p);
                if (t == EnemyType.AlienFlyer && showcase) p.Y = 5f;
                else if (t == EnemyType.AlienFlyer) p.Y = 22f;
                e.GlobalPosition = p;
                e.FaceTowards(pp);
                if (showcase || Screenshot.CmdArg("god") != null) e.MaxHealth = e.Health = 1e7f;
                if (showcase && e.IsAlien)
                {
                    var ps = pose.Split(','); var pz = ps[(k - 1 + ps.Length) % ps.Length];
                    e.Model.AlienAiming = pz is "aim" or "run" ? 1f : 0f;
                    e.Model.AlienCrouch = pz == "crouch" ? 1f : 0f;
                    e.FrozenSpeed = pz == "walk" ? 3.5f : pz == "run" ? 9f : 0f;
                }
                spawned.Add(e);
                GD.Print($"[alien] {t} n°{e.Id} à {pp.DistanceTo(p with { Y = 0 }):0} m");
            }
            tree.CreateTimer(3f).Timeout += () =>
            {
                foreach (var e in spawned.Where(GodotObject.IsInstanceValid))
                {
                    var b = e.Model.Bounds();
                    GD.Print($"[taille] {e.Def.Type} : longueur {b.Size.Z:0.0} m, envergure {b.Size.X:0.0} m, hauteur {b.End.Y - b.Position.Y:0.0} m, {e.Model.TriangleCount()} triangles");
                    if (e.IsAlien)
                        GD.Print($"[pose] {e.Def.Type} bassin {e.Model.AlienNodeXf(e.Model.AlienHipsNode).Origin.Snapped(Vector3.One * 0.01f)} genou {e.Model.AlienNodeXf(e.Model.AlienShinNode(0)).Origin.Snapped(Vector3.One * 0.01f)} "
                            + $"torse {e.Model.AlienNodeXf(e.Model.AlienChestNode).Origin.Snapped(Vector3.One * 0.01f)} pied {e.Model.AlienNodeXf(e.Model.AlienFootNode(0)).Origin.Snapped(Vector3.One * 0.01f)} tête {e.Model.AlienNodeXf(e.Model.AlienHeadNode).Origin.Snapped(Vector3.One * 0.01f)} bouche {e.ToLocal(e.Model.AlienMuzzle).Snapped(Vector3.One * 0.01f)}");
                }
            };
            if (Screenshot.CmdArg("aliens-cam") is { } mc && spawned.Count > 0)
            {
                var v = FloatList(mc);
                var target = spawned[0];
                var cam = new Camera3D { Fov = 50f, Far = 900f };
                w.AddChild(cam);
                cam.MakeCurrent();
                var t0 = Time.GetTicksMsec();
                void Orbit()
                {
                    if (!GodotObject.IsInstanceValid(cam) || !GodotObject.IsInstanceValid(target)) return;
                    var d = v.Length > 0 ? v[0] : 12f;
                    var h = v.Length > 1 ? v[1] : 3f;
                    var az = Mathf.DegToRad((v.Length > 2 ? v[2] : 25f) + (v.Length > 3 ? v[3] : 0f) * (Time.GetTicksMsec() - t0) / 1000f);
                    var c = target.IsPortal ? target.GlobalPosition + Vector3.Up * 8f : target.GlobalPosition + Vector3.Up * 2.6f;
                    var off = target.GlobalBasis * new Vector3(Mathf.Sin(az), 0, Mathf.Cos(az)) * d;
                    cam.GlobalPosition = new Vector3(c.X + off.X, target.GlobalPosition.Y + h, c.Z + off.Z);
                    cam.LookAt(c, Vector3.Up);
                    if (!cam.Current) cam.MakeCurrent();
                    tree.CreateTimer(0f).Timeout += Orbit;
                }
                Orbit();
            }
            if (Screenshot.CmdArg("aliens-shots") is { } ms && dir != null)
            {
                var pending = new Queue<float>(FloatList(ms).OrderBy(x => x));
                void Poll()
                {
                    if (!GodotObject.IsInstanceValid(w) || pending.Count == 0) return;
                    if (w.Elapsed >= pending.Peek())
                    {
                        var t = pending.Dequeue();
                        Screenshot.Take(app.GetViewport(), $"aliens_{pose}_t{t:00.0}");
                    }
                    tree.CreateTimer(0f).Timeout += Poll;
                }
                Poll();
            }
            // --aliens-only : les vagues de la mission (insectes) sont retirées au fur et à mesure (scène lisible, journal propre).
            if (Screenshot.CmdArg("aliens-only") != null)
            {
                void Purge()
                {
                    if (!GodotObject.IsInstanceValid(w)) return;
                    foreach (var x in w.EnemiesById.Values.Where(x => !spawned.Contains(x) && !x.IsAlienKind && !x.IsMachine && x.State != EnemyState.Dead).ToList())
                        w.HostRetreatEnemy(x);
                    tree.CreateTimer(0.5f).Timeout += Purge;
                }
                Purge();
            }
            void Log()
            {
                foreach (var e in spawned)
                    GD.Print(GodotObject.IsInstanceValid(e) && e.State != EnemyState.Dead
                        ? $"[alien] {e.Def.Type} n°{e.Id} {e.State} IA {e.AlienStateName} accroupi {e.Model.AlienCrouch:0} visée {e.Model.AlienAiming:0} PV {e.Health:0}/{e.MaxHealth:0} pos {e.GlobalPosition.Snapped(Vector3.One)}"
                        : "[alien] abattu");
                var others = w.EnemiesById.Values.Count(x => x.State != EnemyState.Dead && !spawned.Contains(x));
                if (others > 0) GD.Print($"[alien] autres ennemis actifs : {others} ({string.Join(", ", w.EnemiesById.Values.Where(x => !spawned.Contains(x)).GroupBy(x => x.Def.Type).Select(g => $"{g.Key} ×{g.Count()}"))})");
                tree.CreateTimer(2f).Timeout += Log;
            }
            Log();
        }
        tree.CreateTimer(1f).Timeout += Go;

        if (float.TryParse(Screenshot.CmdArg("aliens-weak") ?? "", NumberStyles.Float, CultureInfo.InvariantCulture, out var weakAt))
        {
            var shots = 0;
            void Burst()
            {
                var w = app.CurrentWorld;
                if (w == null) return;
                foreach (var e in spawned.Where(e => GodotObject.IsInstanceValid(e) && e.State != EnemyState.Dead).ToList())
                {
                    var before = e.Health;
                    w.HostDamageEnemy(e.Id, 30f, e.Center, Net.I.MyId, true);
                    if (shots % 10 == 0) GD.Print($"[alien] {e.Def.Type} point faible : {before - e.Health:0} dégâts (30 envoyés)");
                }
                if (shots++ < 400) tree.CreateTimer(0.1f, false).Timeout += Burst;
            }
            tree.CreateTimer(weakAt, false).Timeout += Burst;
        }
        if (float.TryParse(Screenshot.CmdArg("aliens-kill") ?? "", NumberStyles.Float, CultureInfo.InvariantCulture, out var killAt))
            tree.CreateTimer(killAt, false).Timeout += () =>
            {
                var w = app.CurrentWorld;
                if (w == null) return;
                Enemy.Frozen = false;
                foreach (var e in spawned.Where(e => GodotObject.IsInstanceValid(e) && e.State != EnemyState.Dead))
                    w.HostDamageEnemy(e.Id, e.Health * 10f + 1e6f, e.Center, Net.I.MyId, true);
                GD.Print("[alien] mort demandée");
            };
    }
}
