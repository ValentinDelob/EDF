using System;
using System.Globalization;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Tests de la destruction du décor (voir docs/ARCHITECTURE.md, « Tests automatisés »).</summary>
public static partial class AutoTest
{
    /// <summary>
    /// --wreck=N : N accessoires détruits par seconde autour du joueur (hôte) à partir de 5 s, journal [wreck] toutes les 2 s ;
    /// --wreck-at=T1,T2 [--wreck-kind=pole,car,...] : un accessoire de la famille donnée, devant la caméra, détruit à chaque
    /// instant, avec captures à +0,1 / 0,5 / 1,2 / 2,5 / 5 s ;
    /// --collapse-at=T : l'immeuble devant la caméra passe par ses trois paliers de dégâts (T, T+2, T+4) puis s'effondre
    /// (T+7), captures toutes les 0,5 s jusqu'à T+18.
    /// </summary>
    private static void WreckTest(App app, string dir)
    {
        var tree = app.GetTree();
        if (Screenshot.CmdArg("wreck") is { } ws && float.TryParse(ws, NumberStyles.Float, CultureInfo.InvariantCulture, out var perSec) && perSec >= 0)
        {
            var rng = new RandomNumberGenerator { Seed = 11 };
            var count = 0;
            void Tick()
            {
                if (Screenshot.CmdArg("novsync") != null && (Engine.MaxFps != 0 || DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled))
                {
                    DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
                    Engine.MaxFps = 0;
                }
                var w = app.CurrentWorld;
                if (perSec > 0 && w?.Decor != null && w.LocalPlayer != null && GodotObject.IsInstanceValid(w.LocalPlayer))
                {
                    var p = w.LocalPlayer.GlobalPosition;
                    var ids = w.Decor.Near(p, 70f).Where(i => w.Decor.Get(i) is { State: < 3 } e && e.Kind != PropKind.Container
                        && e.Xf.Origin.DistanceTo(p) is > 8f and < 70f).ToList();
                    if (ids.Count > 0)
                    {
                        var e = w.Decor.Get(ids[rng.RandiRange(0, ids.Count - 1)]);
                        w.Decor.HostDamage(e.Id, 1e6f, PropHit.Blast, e.Xf.Origin + new Vector3(rng.RandfRange(-3, 3), 0.5f, rng.RandfRange(-3, 3)), 0);
                        count++;
                    }
                }
                tree.CreateTimer(perSec > 0 ? 1f / perSec : 1f, false).Timeout += Tick;
            }
            tree.CreateTimer(5f, false).Timeout += Tick;
            ulong startFrame = 0, startMs = 0;
            var worst = 0.0;
            void Report()
            {
                var now = Time.GetTicksMsec();
                if (startMs == 0 && app.CurrentWorld != null && app.CurrentWorld.Elapsed > 6f) { startMs = now; startFrame = Engine.GetProcessFrames(); worst = 0; }
                var avg = startMs > 0 && now > startMs ? (Engine.GetProcessFrames() - startFrame) * 1000.0 / (now - startMs) : 0;
                GD.Print($"[wreck] {count} détruits, {Engine.GetFramesPerSecond()} i/s (moyenne {avg:0}, pire image {worst * 1000:0} ms), débris {DebrisPool.ActiveCount}/{DebrisPool.Max}, {Effects.Stats}");
                tree.CreateTimer(2f, true, false, true).Timeout += Report;
            }
            void Worst()
            {
                if (startMs > 0) worst = Mathf.Max(worst, app.GetProcessDeltaTime());
                tree.CreateTimer(0, true, false, true).Timeout += Worst;
            }
            tree.CreateTimer(6f, true, false, true).Timeout += Report;
            tree.CreateTimer(6f, true, false, true).Timeout += Worst;
        }

        // --crush-test[=Type] : un gros ennemi (reine par défaut) apparaît à 35 m du soldat et marche sur le mobilier (journal [decor] avec --fps).
        if (Screenshot.CmdArg("crush-test") is { } ct)
            tree.CreateTimer(9f, false).Timeout += () =>
            {
                var w = app.CurrentWorld;
                if (w?.LocalPlayer == null || !Net.I.IsHost) return;
                var type = Enum.TryParse<EnemyType>(ct, true, out var et) ? et : EnemyType.Queen;
                var p = w.LocalPlayer.GlobalPosition + new Vector3(35f, 0, 0);
                var e = w.HostSpawnEnemy(type, p with { Y = 0 });
                GD.Print($"[crush] {type} (taille {e.Def.Size}) en {p.Snapped(Vector3.One)}");
            };

        if (Screenshot.CmdArg("wreck-at") is { } wa)
        {
            Player.BotStill = true;
            Player.BotHoldFire = true;
            Enemy.Frozen = Screenshot.CmdArg("freeze") != null;
            var times = FloatList(wa);
            var kinds = (Screenshot.CmdArg("wreck-kind") ?? "pole").Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < times.Length; i++)
            {
                var at = times[i];
                var kindName = kinds[Math.Min(i, kinds.Length - 1)];
                PropEntry best = null;
                var fwd = Vector3.Forward;
                // 1,5 s avant : choisit l'accessoire le plus proche du soldat et le cadre (soldat posé à côté, caméra tournée vers lui).
                tree.CreateTimer(Mathf.Max(at - 1.5f, 0.1f), true, false, true).Timeout += () =>
                {
                    var w = app.CurrentWorld;
                    var pl = w?.LocalPlayer;
                    if (w?.Decor == null || pl == null) return;
                    var kind = Enum.TryParse<PropKind>(kindName, true, out var k) ? k : PropKind.Pole;
                    var bestD = float.MaxValue;
                    best = null;
                    foreach (var id in w.Decor.Near(pl.GlobalPosition, 120f))
                    {
                        var e = w.Decor.Get(id);
                        if (e.Kind != kind || e.State >= 3 || e.Flattened) continue;
                        var d = e.Xf.Origin.DistanceTo(pl.GlobalPosition);
                        if (d < bestD) { bestD = d; best = e; }
                    }
                    if (best == null) { GD.Print($"[wreck] aucun {kind} à moins de 120 m"); return; }
                    var dist = kind is PropKind.Pole or PropKind.Tree ? 13f : kind is PropKind.Small ? 6f : 9f;
                    // Point de vue dégagé : premier des 12 azimuts dont la ligne de visée n'est coupée par rien d'autre.
                    var space = w.GetWorld3D().DirectSpaceState;
                    var target = best.Center + Vector3.Up * 0.3f;
                    var off = Vector3.Back;
                    for (var j = 0; j < 12; j++)
                    {
                        var a = RuinKit.Hash(best.Id, 1, 3) * Mathf.Tau + j * Mathf.Tau / 12f;
                        var o = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                        var eye = best.Xf.Origin + o * (dist + 3f) + Vector3.Up * 2.2f;
                        var q = PhysicsRayQueryParameters3D.Create(eye, target, 1u);
                        q.Exclude = new Godot.Collections.Array<Rid> { pl.GetRid() };
                        var hit = space.IntersectRay(q);
                        var ok = hit.Count == 0 || w.Decor.FromHit(hit["collider"].AsGodotObject(), hit["shape"].AsInt32()) == best.Id;
                        if (j == 0 || ok) off = o;
                        if (ok) break;
                    }
                    pl.GlobalPosition = best.Xf.Origin + off * dist + Vector3.Up * 0.5f;
                    fwd = -off;
                    pl.SetYaw(Mathf.Atan2(off.X, off.Z) + 0.32f);
                    pl.SetPitch(kind is PropKind.Pole or PropKind.Tree ? 0.05f : -0.18f);
                    GD.Print($"[wreck] {kind} n°{best.Id} en {best.Xf.Origin.Snapped(Vector3.One * 0.1f)}");
                };
                tree.CreateTimer(at, true, false, true).Timeout += () =>
                {
                    var w = app.CurrentWorld;
                    if (w?.Decor == null || best == null) return;
                    // --wreck-by=shot : rafale de balles réelles (rayons) depuis la caméra, chemin complet tir → hôte → événement.
                    if (Screenshot.CmdArg("wreck-by") == "shot" && w.GetViewport().GetCamera3D() is { } cam)
                    {
                        var target = best;
                        var shots = 0;
                        void Shot()
                        {
                            if (target.State >= 3 || shots++ > 60) { GD.Print($"[wreck] tir : {shots} balles, état {target.State}"); return; }
                            var eye = cam.GlobalPosition;
                            var q = PhysicsRayQueryParameters3D.Create(eye, target.Center + Vector3.Up * 0.2f, 1u);
                            q.Exclude = new Godot.Collections.Array<Rid> { w.LocalPlayer.GetRid() };
                            var hit = w.GetWorld3D().DirectSpaceState.IntersectRay(q);
                            if (!w.Decor.LocalHit(hit, 25f, eye) && shots == 1) GD.Print("[wreck] tir : le rayon ne touche pas l'accessoire");
                            tree.CreateTimer(0.08f, false).Timeout += Shot;
                        }
                        Shot();
                        return;
                    }
                    // Source décalée sur le côté : la chute part en travers du champ.
                    var side = fwd.Cross(Vector3.Up);
                    var from = best.Xf.Origin + side * 2f - fwd * 0.3f;
                    if (best.Kind == PropKind.Wreck) w.Decor.HostCrush(best.Id, 6, from, 0);
                    else if (best.Kind == PropKind.Container) w.Decor.HostDamage(best.Id, 900f, PropHit.Blast, best.Xf.Origin + side * 4f, 0);
                    else w.Decor.HostDamage(best.Id, 1e6f, PropHit.Blast, from, 0);
                };
                if (dir == null) continue;
                foreach (var dt in new[] { 0.1f, 0.5f, 1.2f, 2.5f, 5f })
                    Capture(app, dir, $"wreck_{kindName}_t{at:0}_{dt.ToString("0.0", CultureInfo.InvariantCulture)}", at + dt);
            }
        }

        if (Screenshot.CmdArg("collapse-at") is { } ca && float.TryParse(ca, NumberStyles.Float, CultureInfo.InvariantCulture, out var t0))
        {
            Player.BotStill = true;
            Player.BotHoldFire = true;
            Enemy.Frozen = Screenshot.CmdArg("freeze") != null;
            Building target = null;
            Vector3 hit = default;
            void Stage(float frac)
            {
                var w = app.CurrentWorld;
                var cam = w?.GetViewport().GetCamera3D();
                if (cam == null) return;
                if (target == null)
                {
                    // Immeuble à 35-90 m dont le milieu de la façade est directement visible depuis la caméra.
                    var space = w.GetWorld3D().DirectSpaceState;
                    var eye = cam.GlobalPosition;
                    foreach (var b in w.Buildings.Where(b => !b.Destroyed && b.Size.Y > 10f).OrderBy(b => Mathf.Abs(b.GlobalPosition.DistanceTo(eye) - 42f)))
                    {
                        var d = b.GlobalPosition.DistanceTo(eye);
                        if (d < 30f || d > 90f) continue;
                        var face = eye - b.GlobalPosition;
                        var p = b.GlobalPosition + new Vector3(Mathf.Clamp(face.X, -b.Size.X / 2, b.Size.X / 2), b.Size.Y * 0.45f, Mathf.Clamp(face.Z, -b.Size.Z / 2, b.Size.Z / 2));
                        var q = PhysicsRayQueryParameters3D.Create(eye, p + (eye - p).Normalized() * 0.3f, 1u);
                        if (w.LocalPlayer != null) q.Exclude = new Godot.Collections.Array<Rid> { w.LocalPlayer.GetRid() };
                        if (space.IntersectRay(q).Count > 0) continue;
                        target = b;
                        hit = p;
                        break;
                    }
                    if (target == null) { GD.Print("[collapse] aucun immeuble visible"); return; }
                    if (w.LocalPlayer is { } pl)
                    {
                        var to = target.GlobalPosition + Vector3.Up * target.Size.Y * 0.4f - eye;
                        pl.SetYaw(Mathf.Atan2(-to.X, -to.Z) - 0.1f);
                        pl.SetPitch(Mathf.Atan2(to.Y, new Vector2(to.X, to.Z).Length()));
                    }
                    GD.Print($"[collapse] immeuble n°{target.Id} {target.Size.Snapped(Vector3.One * 0.1f)} à {target.GlobalPosition.DistanceTo(eye):0} m");
                }
                var dmg = target.Health - target.MaxHealth * frac;
                if (dmg > 0) w.HostBuildingDamage(target.Id, dmg + (frac <= 0 ? 1f : 0f), hit + Vector3.Up * (frac < 0.3f ? 4f : 0f));
            }
            tree.CreateTimer(Mathf.Max(t0 - 2f, 0.1f), true, false, true).Timeout += () => Stage(1f);
            tree.CreateTimer(t0, true, false, true).Timeout += () => Stage(0.65f);
            tree.CreateTimer(t0 + 2f, true, false, true).Timeout += () => Stage(0.35f);
            tree.CreateTimer(t0 + 4f, true, false, true).Timeout += () => Stage(0.1f);
            tree.CreateTimer(t0 + 7f, true, false, true).Timeout += () => Stage(0f);
            if (dir != null)
                for (var s = 0.5f; s <= 30f; s += s < 18f ? 0.5f : 2f)
                    Capture(app, dir, $"collapse_t{(s).ToString("00.0", CultureInfo.InvariantCulture)}", t0 + s);
        }
    }

    private static void Capture(App app, string dir, string name, float at)
    {
        app.GetTree().CreateTimer(at, true, false, true).Timeout += () =>
        {
            var img = app.GetViewport().GetTexture().GetImage();
            var path = $"{dir}/{name}_f{Engine.GetFramesDrawn()}.png";
            System.Threading.Tasks.Task.Run(() => img.SavePng(path));
        };
    }
}
