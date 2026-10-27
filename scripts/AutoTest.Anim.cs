using Godot;

namespace Rempart;

/// <summary>
/// --anim-test (avec --class=N, --shots=dossier) : banc des animations du soldat. Caméra de côté qui le suit ; séquence
/// pilotée (attente, course, sprint, pas chassés et recul en visée, saut, roulade), rafales de captures « anim_&lt;phase&gt;_NN ».
/// </summary>
public static partial class AutoTest
{
    private static void AnimTest(App app)
    {
        if (Screenshot.CmdArg("anim-test") == null) return;
        Enemy.Frozen = true;
        Player.BotStill = true;
        Player.BotHoldFire = true;
        WhenPlayerReady(app, 1.5f, p =>
        {
            // Direction la plus dégagée (rayons à hauteur de poitrine) : la séquence parcourt une quarantaine de mètres.
            var space = p.GetWorld3D().DirectSpaceState;
            var from = p.GlobalPosition + Vector3.Up * 1.2f;
            var fwd = Vector3.Forward;
            var best = -1f;
            for (var i = 0; i < 24; i++)
            {
                var d = new Basis(Vector3.Up, i * Mathf.Tau / 24f) * Vector3.Forward;
                var q = PhysicsRayQueryParameters3D.Create(from, from + d * 80f);
                q.Exclude = new Godot.Collections.Array<Rid> { p.GetRid() };
                var hit = space.IntersectRay(q);
                var free = hit.Count == 0 ? 80f : from.DistanceTo((Vector3)hit["position"]);
                if (free > best) { best = free; fwd = d; }
            }
            GD.Print($"[anim] direction dégagée sur {best:0} m");
            Player.BotLookAt = p.GlobalPosition + fwd * 2000f + Vector3.Up * 1.4f;
            var side = Screenshot.CmdArg("anim-view") == "front" ? -fwd : fwd.Cross(Vector3.Up).Normalized();
            var cam = new Camera3D { Fov = 40, Current = true };
            app.CurrentWorld.AddChild(cam);
            var follow = new AnimCam { Cam = cam, Target = p, Side = side };
            app.CurrentWorld.AddChild(follow);
            cam.MakeCurrent();

            void Hold(string a, bool on) { if (on) Input.ActionPress(a); else Input.ActionRelease(a); }
            void Tap(float t, string a) { At(app, t, () => Hold(a, true)); At(app, t + 0.1f, () => Hold(a, false)); }
            void Burst(float t, string phase, int n, float step)
            {
                for (var i = 0; i < n; i++)
                {
                    var k = i;
                    At(app, t + i * step, () => Shot(app, $"anim_{phase}_{k:00}"));
                }
            }
            void Log(float t, string msg) => At(app, t, () => GD.Print($"[anim] {msg}"));

            Log(0f, "attente");
            Burst(0.6f, "a_attente", 2, 0.5f);
            Log(1.5f, "course");
            At(app, 1.5f, () => Hold("move_forward", true));
            Burst(3.0f, "b_course", 8, 0.1f);
            Log(4.0f, "sprint");
            At(app, 4.0f, () => Hold("sprint", true));
            Burst(5.2f, "c_sprint", 8, 0.08f);
            Log(6.5f, "visee, pas chasses");
            At(app, 6.5f, () => { Hold("sprint", false); Hold("move_forward", false); Player.BotAiming = true; Hold("move_left", true); });
            Burst(8.0f, "d_chasse", 8, 0.1f);
            Log(9.5f, "visee, recul");
            At(app, 9.5f, () => { Hold("move_left", false); Hold("move_back", true); });
            Burst(11.0f, "e_recul", 8, 0.1f);
            Log(12.5f, "saut");
            At(app, 12.5f, () => { Hold("move_back", false); Player.BotAiming = false; Hold("move_forward", true); });
            Tap(13.5f, "jump");
            Burst(13.55f, "f_saut", 12, 0.08f);
            Log(15.5f, "roulade");
            Tap(15.5f, "dodge");
            Burst(15.52f, "g_roulade", 9, 0.05f);
            At(app, 17.0f, () => { Hold("move_forward", false); GD.Print("[anim] fin"); });
            Burst(17.6f, "h_arret", 2, 0.4f);
        });
    }

    /// <summary>Caméra de côté qui suit le soldat à hauteur de bassin.</summary>
    private partial class AnimCam : Node
    {
        public Camera3D Cam;
        public Player Target;
        public Vector3 Side;
        private float? _y;

        public override void _Process(double delta)
        {
            if (!IsInstanceValid(Target) || !IsInstanceValid(Cam)) return;
            // Hauteur de la caméra fixée au départ : les sauts se voient.
            _y ??= Target.GlobalPosition.Y;
            _y = Mathf.Lerp(_y.Value, Target.GlobalPosition.Y, (float)delta * 0.6f);
            var focus = Target.GlobalPosition with { Y = _y.Value } + Vector3.Up * 0.95f;
            Cam.GlobalPosition = focus + Side * 3.6f + Vector3.Up * 0.25f;
            Cam.LookAt(focus, Vector3.Up);
        }
    }
}
