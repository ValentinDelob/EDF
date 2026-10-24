using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Tests et vitrine des machines (voir docs/ARCHITECTURE.md, « Tests automatisés »).</summary>
public static partial class AutoTest
{
    /// <summary>
    /// --machines=Walker,Tripod,DropShip : les machines apparaissent devant le soldat (bot immobile, sans tirer), face à lui,
    /// et s'animent normalement (attaques comprises) ; journal [machine] toutes les 2 s (état, PV, soute).
    /// --showcase=machines : les trois machines figées (vitrine), soute du vaisseau ouverte, dimensions dans le journal.
    /// Options : --machines-dist=D (distance du premier rang), --machines-kill=T (destruction de toutes à T s, test des
    /// explosions de mort), --machines-weak=T (rafale sur les points faibles à partir de T s), --machines-fire (le bot tire),
    /// --freeze (IA figée), --god.
    /// </summary>
    private static void MachineTest(App app, string dir)
    {
        // --hitch : journal des images de plus de 50 ms (à-coups : compilation de shaders, pics de charge), avec l'heure de mission.
        if (Screenshot.CmdArg("hitch") != null)
        {
            var t0 = Time.GetTicksUsec();
            void Watch()
            {
                var now = Time.GetTicksUsec();
                var ms = (now - t0) / 1000.0;
                t0 = now;
                if (ms > 50 && app.CurrentWorld is { } hw && hw.Elapsed > 0.5f) GD.Print($"[à-coup] {ms:0} ms à t={hw.Elapsed:0.00}");
                app.GetTree().CreateTimer(0f).Timeout += Watch;
            }
            app.GetTree().CreateTimer(0f).Timeout += Watch;
        }
        var sc = Screenshot.CmdArg("showcase");
        var showcase = sc is "machines" or "titan";
        var arg = Screenshot.CmdArg("machines") ?? (sc == "titan" ? "Titan" : showcase ? "Walker,Tripod,DropShip" : null);
        if (arg == null) return;
        var tree = app.GetTree();
        var types = arg.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Enum.TryParse<EnemyType>(s.Trim(), true, out var t) ? t : EnemyType.Walker).ToList();
        var dist0 = float.TryParse(Screenshot.CmdArg("machines-dist") ?? "", NumberStyles.Float, CultureInfo.InvariantCulture, out var dd) ? dd : 0f;
        var spawned = new List<Enemy>();
        Player.BotStill = true;
        Player.BotHoldFire = Screenshot.CmdArg("machines-fire") == null;
        void Go()
        {
            var w = app.CurrentWorld;
            if (w?.LocalPlayer == null || !GodotObject.IsInstanceValid(w.LocalPlayer) || w.Elapsed < 1f) { tree.CreateTimer(0.5f).Timeout += Go; return; }
            Enemy.Frozen = showcase || Screenshot.CmdArg("freeze") != null;
            foreach (var act in new[] { "move_left", "move_right", "sprint" }) Input.ActionRelease(act);
            foreach (var n in w.FindChildren("*", "Node3D", true, false))
                if (n is Ally a) a.Visible = false;
            var pl = w.LocalPlayer;
            // --machines-at=X,Z,Cap : soldat déplacé sur un terrain dégagé (plage de la ville : 0,128,90), regard selon le cap (degrés).
            if (Screenshot.CmdArg("machines-at") is { } at)
            {
                var v = FloatList(at);
                pl.GlobalPosition = new Vector3(v.Length > 0 ? v[0] : 0f, 1f, v.Length > 1 ? v[1] : 0f);
                if (v.Length > 2) pl.SetYaw(Mathf.DegToRad(v[2]));
            }
            var pp = pl.GlobalPosition with { Y = 0 };
            var f = pl.AimForward with { Y = 0 };
            if (Screenshot.CmdArg("machines-at") is { } at2 && FloatList(at2) is { Length: > 2 } av)
                f = new Vector3(-Mathf.Sin(Mathf.DegToRad(av[2])), 0, -Mathf.Cos(Mathf.DegToRad(av[2])));
            f = f.LengthSquared() > 0.01f ? f.Normalized() : Vector3.Forward;
            var right = f.Cross(Vector3.Up);
            for (var i = 0; i < types.Count; i++)
            {
                var t = types[i];
                var dist = (dist0 > 0 ? dist0 : t switch { EnemyType.Walker => 34f, EnemyType.Tripod => 58f, EnemyType.Titan => 100f, _ => 70f }) + (types.Count > 3 ? i * 6f : 0f);
                var lat = types.Count == 1 ? 0f : t switch { EnemyType.Walker => -16f, EnemyType.Tripod => 22f, _ => -6f } + (i >= 3 ? (i - 2) * 14f : 0f);
                var p = pp + f * dist + right * lat;
                var e = w.HostSpawnEnemy(t, p);
                if (t == EnemyType.DropShip) p.Y = 38f;
                e.GlobalPosition = p;
                e.FaceTowards(pp);
                if (showcase || Screenshot.CmdArg("god") != null) e.MaxHealth = e.Health = 1e7f;
                if (showcase && t == EnemyType.DropShip) e.MachineFx(60, p);
                spawned.Add(e);
                GD.Print($"[machine] {t} n°{e.Id} à {pp.DistanceTo(p with { Y = 0 }):0} m");
            }
            tree.CreateTimer(3f).Timeout += () =>
            {
                foreach (var e in spawned.Where(GodotObject.IsInstanceValid))
                {
                    var b = e.Model.Bounds();
                    GD.Print($"[taille] {e.Def.Type} : longueur {b.Size.Z:0.0} m, envergure {b.Size.X:0.0} m, hauteur {b.End.Y:0.0} m, {e.Model.TriangleCount()} triangles");
                }
            };
            // --machines-cam=D,H,A[,T] : caméra libre à D m de la première machine, H m de haut, azimut A degrés (0 = de face),
            // qui tourne de T degrés par seconde ; vise le centre de la machine (captures de contrôle des modèles).
            if (Screenshot.CmdArg("machines-cam") is { } mc && spawned.Count > 0)
            {
                var v = FloatList(mc);
                var target = spawned[0];
                var cam = new Camera3D { Fov = 55f, Far = 900f };
                w.AddChild(cam);
                cam.MakeCurrent();
                var t0 = Time.GetTicksMsec();
                void Orbit()
                {
                    if (!GodotObject.IsInstanceValid(cam) || !GodotObject.IsInstanceValid(target)) return;
                    var d = v.Length > 0 ? v[0] : 30f;
                    var h = v.Length > 1 ? v[1] : 6f;
                    var az = Mathf.DegToRad((v.Length > 2 ? v[2] : 25f) + (v.Length > 3 ? v[3] : 0f) * (Time.GetTicksMsec() - t0) / 1000f);
                    var c = target.Center;
                    var basis = target.GlobalBasis;
                    var off = basis * new Vector3(Mathf.Sin(az), 0, Mathf.Cos(az)) * d;
                    cam.GlobalPosition = new Vector3(c.X + off.X, h, c.Z + off.Z);
                    cam.LookAt(c + (target.Def.Type == EnemyType.DropShip ? Vector3.Zero : Vector3.Down * 2f), Vector3.Up);
                    tree.CreateTimer(0f).Timeout += Orbit;
                }
                Orbit();
            }
            // --machines-shots=T1,T2 : captures aux instants de mission donnés (même horloge que le journal [machine] t=...).
            if (Screenshot.CmdArg("machines-shots") is { } ms && dir != null)
            {
                // Sondage à chaque image sur l'horloge de mission (les minuteries suivent un autre temps lors des à-coups).
                var pending = new Queue<float>(FloatList(ms).OrderBy(x => x));
                void Poll()
                {
                    if (!GodotObject.IsInstanceValid(w) || pending.Count == 0) return;
                    if (w.Elapsed >= pending.Peek())
                    {
                        var t = pending.Dequeue();
                        GD.Print($"[machine] capture t={t:0.0} (mission {w.Elapsed:0.00}) : " + string.Join(", ", spawned.Where(GodotObject.IsInstanceValid).Select(e => $"{e.Def.Type} rayon {e.BeamTime:0.00}")));
                        Screenshot.Take(app.GetViewport(), $"machines_t{t:00.0}");
                    }
                    tree.CreateTimer(0f).Timeout += Poll;
                }
                Poll();
            }
            void Log()
            {
                foreach (var e in spawned)
                    GD.Print(GodotObject.IsInstanceValid(e)
                        ? $"[machine] {e.Def.Type} n°{e.Id} {e.State} PV {e.Health:0}/{e.MaxHealth:0} pos {e.GlobalPosition.Snapped(Vector3.One)}{(e.Def.Type == EnemyType.DropShip ? $" soute {(e.HatchOpen ? "ouverte" : "fermée")}" : "")}"
                        : "[machine] détruite");
                tree.CreateTimer(2f).Timeout += Log;
            }
            Log();
        }
        tree.CreateTimer(1f).Timeout += Go;

        // Rafale sur les points faibles (chemin hôte complet), puis destruction.
        if (float.TryParse(Screenshot.CmdArg("machines-weak") ?? "", NumberStyles.Float, CultureInfo.InvariantCulture, out var weakAt))
        {
            var shots = 0;
            void Burst()
            {
                var w = app.CurrentWorld;
                if (w == null) return;
                foreach (var e in spawned.Where(e => GodotObject.IsInstanceValid(e) && e.State != EnemyState.Dead).ToList())
                {
                    var before = e.Health;
                    w.HostDamageEnemy(e.Id, 60f, e.Center, Net.I.MyId, true);
                    if (shots % 10 == 0) GD.Print($"[machine] {e.Def.Type} point faible : {before - e.Health:0} dégâts (60 envoyés)");
                }
                if (shots++ < 400) tree.CreateTimer(0.1f, false).Timeout += Burst;
            }
            tree.CreateTimer(weakAt, false).Timeout += Burst;
        }
        if (float.TryParse(Screenshot.CmdArg("machines-kill") ?? "", NumberStyles.Float, CultureInfo.InvariantCulture, out var killAt))
            tree.CreateTimer(killAt, false).Timeout += () =>
            {
                var w = app.CurrentWorld;
                if (w == null) return;
                foreach (var e in spawned.Where(e => GodotObject.IsInstanceValid(e) && e.State != EnemyState.Dead))
                {
                    if (e.Def.Type == EnemyType.DropShip) e.MachineFx(60, e.GlobalPosition);
                    w.HostDamageEnemy(e.Id, e.Health * 10f + 1e6f, e.Center, Net.I.MyId, true);
                }
                GD.Print("[machine] destruction demandée");
            };
    }
}
