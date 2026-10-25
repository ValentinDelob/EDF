using Godot;

namespace Rempart;

/// <summary>
/// --tank-drive (avec --tank) : conduite scriptée du char par les vraies touches : avant, virage, avant, marche arrière,
/// rotation sur place. Journal « [char] » toutes les 0,5 s : position, hauteur, vitesse, inclinaison de la caisse ;
/// « [char] bloqué » si le char commandé n'avance plus (1 s sous 0,3 m), « [char] ok » à la fin.
/// --tank-drive=cam : caméra de contrôle qui suit le char de trois quarts arrière (fumée d'échappement, caisse suspendue).
/// </summary>
public static partial class AutoTest
{
    public static void TankDriveTest(App app)
    {
        if (Screenshot.CmdArg("tank-drive") == null) return;
        WhenPlayerReady(app, 1f, p => WaitBoarded(app, p));
    }

    private static void WaitBoarded(App app, Player p)
    {
        if (!GodotObject.IsInstanceValid(p)) return;
        if (p.Vehicle is not Tank t) { app.GetTree().CreateTimer(0.5f).Timeout += () => WaitBoarded(app, p); return; }
        // (durée s, avant, côté) : avant, virage à droite en avançant, avant, marche arrière, rotation sur place.
        var steps = new (float dur, float fwd, float side)[] { (9f, 1, 0), (4f, 1, 1), (7f, 1, 0), (3f, -1, 0), (3f, 0, -1), (5f, 1, 0) };
        var i = 0;
        var left = steps[0].dur;
        var last = t.GlobalPosition;
        var lastT = 0f;
        var stuckLogged = false;
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        var clock = 0f;
        void Apply((float dur, float fwd, float side) s)
        {
            foreach (var a in new[] { "move_forward", "move_back", "move_left", "move_right" }) Input.ActionRelease(a);
            if (s.fwd > 0) Input.ActionPress("move_forward");
            if (s.fwd < 0) Input.ActionPress("move_back");
            if (s.side > 0) Input.ActionPress("move_right");
            if (s.side < 0) Input.ActionPress("move_left");
        }
        Apply(steps[0]);
        if (Screenshot.CmdArg("tank-drive") == "cam")
        {
            var cam = new Camera3D { Fov = 60, Current = true };
            app.CurrentWorld.AddChild(cam);
            cam.MakeCurrent();
            var follow = new Timer { WaitTime = 0.016f, Autostart = true };
            app.AddChild(follow);
            follow.Timeout += () =>
            {
                if (!GodotObject.IsInstanceValid(t) || !GodotObject.IsInstanceValid(cam)) return;
                var b = new Basis(Vector3.Up, t.Rotation.Y);
                var target = t.GlobalPosition + Vector3.Up * 1.4f;
                var want = target + b * new Vector3(7.5f, 2.2f, -7.5f);
                cam.GlobalPosition = cam.GlobalPosition.DistanceTo(want) > 20f ? want : cam.GlobalPosition.Lerp(want, 0.15f);
                cam.LookAt(target, Vector3.Up);
            };
        }
        GD.Print($"[char] départ {t.GlobalPosition}");
        var timer = new Timer { WaitTime = 0.5f, Autostart = true };
        app.AddChild(timer);
        timer.Timeout += () =>
        {
            if (!GodotObject.IsInstanceValid(t)) { timer.QueueFree(); return; }
            clock += 0.5f;
            left -= 0.5f;
            var pos = t.GlobalPosition;
            minY = Mathf.Min(minY, pos.Y);
            maxY = Mathf.Max(maxY, pos.Y);
            var moved = new Vector2(pos.X - last.X, pos.Z - last.Z).Length();
            if (clock - lastT >= 1f)
            {
                var s = steps[i];
                if (s.fwd != 0 && moved < 0.3f && !stuckLogged) { GD.Print($"[char] bloqué à {pos} (étape {i}) : {t.BlockDebug} / {t.StepDebug}"); stuckLogged = true; }
                last = pos;
                lastT = clock;
            }
            GD.Print($"[char] t={clock:0.0} étape {i} pos=({pos.X:0.0}, {pos.Y:0.00}, {pos.Z:0.0}) vitesse={t.Speed:0.0} caisse={t.SprungDebug}");
            if (left > 0) return;
            i++;
            if (i >= steps.Length)
            {
                foreach (var a in new[] { "move_forward", "move_back", "move_left", "move_right" }) Input.ActionRelease(a);
                GD.Print($"[char] ok : hauteur {minY:0.00} à {maxY:0.00} m, {(stuckLogged ? "un blocage" : "aucun blocage")}");
                timer.QueueFree();
                return;
            }
            left = steps[i].dur;
            stuckLogged = false;
            Apply(steps[i]);
        };
    }
}
