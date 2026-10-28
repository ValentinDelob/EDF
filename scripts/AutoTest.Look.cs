using Godot;

namespace Rempart;

/// <summary>
/// --fx-test : flamme de bouche et effets de tir filmés de côté.
/// --crate-test (avec --shots=dossier) : les trois caisses tombent devant le soldat (chemin hôte complet : apparition, chute,
/// rebonds, pose) ; caméra rapprochée qui tourne autour, captures « crate_NN », puis vue de la caméra du jeu « crate_game ».
/// </summary>
public static partial class AutoTest
{
    private static void LookTests(App app)
    {
        FxTest(app);
        SplatTest(app);
        if (Screenshot.CmdArg("crate-test") == null) return;
        Enemy.Frozen = true;
        Player.BotStill = true;
        Player.BotHoldFire = true;
        WhenPlayerReady(app, 1.5f, p =>
        {
            var w = app.CurrentWorld;
            var fwd = -p.Camera.GlobalBasis.Z with { Y = 0 };
            fwd = fwd.Normalized();
            var side = fwd.Cross(Vector3.Up).Normalized();
            var at = p.GlobalPosition + fwd * 7f;
            w.HostSpawnCrate(CrateType.Weapon, at - side * 2.2f);
            w.HostSpawnCrate(CrateType.Armor, at);
            w.HostSpawnCrate(CrateType.Health, at + side * 2.2f);
            GD.Print($"[caisses] {w.CratesById.Count} caisses lâchées à {at}");
            var cam = new Camera3D { Fov = 45, Current = true };
            w.AddChild(cam);
            var orbit = new OrbitCam { Cam = cam, Center = at + Vector3.Up * 0.4f, Fwd = -fwd, Side = side };
            w.AddChild(orbit);
            cam.MakeCurrent();
            for (var i = 0; i < 10; i++)
            {
                var k = i;
                At(app, 0.15f + i * (i < 4 ? 0.12f : 0.5f), () => Shot(app, $"crate_{k:00}"));
            }
            At(app, 4.5f, () =>
            {
                orbit.QueueFree();
                cam.QueueFree();
                p.Camera.MakeCurrent();
            });
            At(app, 4.8f, () => Shot(app, "crate_game"));
            At(app, 5.2f, () =>
            {
                foreach (var c in w.CratesById.Values)
                    GD.Print($"[caisses] {c.Type} posée à {c.GlobalPosition.Y - World.GroundAt(c.GlobalPosition.X, c.GlobalPosition.Z):0.00} m du sol");
                // Ramassage : le soldat avance jusqu'aux caisses (aimant, effet de ramassage).
                p.GlobalPosition = at - fwd * 2.5f;
            });
            At(app, 5.45f, () => Shot(app, "crate_pick_0"));
            At(app, 5.6f, () => Shot(app, "crate_pick_1"));
            At(app, 6.5f, () => GD.Print($"[caisses] restantes : {w.CratesById.Count}, ramassées : {string.Join(", ", w.CratesCollected)}"));
        });
    }

    /// <summary>
    /// --fx-test (avec --shots=dossier) : le soldat tire en continu vers un point dégagé ; caméra de côté (AnimCam) puis de
    /// trois quarts avant ; rafales de captures « fx_side_NN » et « fx_front_NN » (flamme de bouche, traçantes, douilles).
    /// </summary>
    private static void FxTest(App app)
    {
        if (Screenshot.CmdArg("fx-test") == null) return;
        Enemy.Frozen = true;
        Player.BotStill = true;
        WhenPlayerReady(app, 1.5f, p =>
        {
            var fwd = (-p.Camera.GlobalBasis.Z with { Y = 0 }).Normalized();
            // Cible : le premier mur devant le soldat (à moins de 60 m), sinon un point à 60 m.
            var eye = p.GlobalPosition + Vector3.Up * 1.4f;
            var ray = p.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(eye, eye + fwd * 60f, 1));
            Player.BotLookAt = ray.Count > 0 ? (Vector3)ray["position"] : eye + fwd * 60f;
            Player.BotForceFire = true;
            var cam = new Camera3D { Fov = 40, Current = true };
            app.CurrentWorld.AddChild(cam);
            var side = fwd.Cross(Vector3.Up).Normalized();
            var follow = new AnimCam { Cam = cam, Target = p, Side = side };
            app.CurrentWorld.AddChild(follow);
            cam.MakeCurrent();
            for (var i = 0; i < 8; i++)
            {
                var k = i;
                At(app, 1.0f + i * 0.033f, () => Shot(app, $"fx_side_{k:00}"));
            }
            At(app, 2.0f, () => follow.Side = (side * 0.6f + fwd * 0.8f).Normalized());
            for (var i = 0; i < 8; i++)
            {
                var k = i;
                At(app, 3.0f + i * 0.033f, () => Shot(app, $"fx_front_{k:00}"));
            }
            At(app, 4.0f, () => { Player.BotForceFire = false; follow.QueueFree(); cam.QueueFree(); p.Camera.MakeCurrent(); Player.BotAiming = true; });
            // Impacts laissés sur la cible, vus par la caméra du jeu en visée.
            At(app, 4.8f, () => { Shot(app, "fx_holes"); Player.BotAiming = false; GD.Print("[fx] fin"); });
        });
    }

    /// <summary>
    /// --splat-test (avec --shots=dossier) : trois insectes (fourmi noire, acide, rouge) sortent à 14 m devant le soldat, figés,
    /// puis sont abattus par l'hôte : captures « splat_NN » des cadavres, puis des flaques d'hémolymphe vues de plus haut.
    /// </summary>
    private static void SplatTest(App app)
    {
        if (Screenshot.CmdArg("splat-test") == null) return;
        Enemy.Frozen = true;
        Player.BotStill = true;
        Player.BotHoldFire = true;
        WhenPlayerReady(app, 1.5f, p =>
        {
            var w = app.CurrentWorld;
            var fwd = (-p.Camera.GlobalBasis.Z with { Y = 0 }).Normalized();
            var side = fwd.Cross(Vector3.Up).Normalized();
            var ants = new System.Collections.Generic.List<Enemy>();
            foreach (var (t, k) in new[] { (EnemyType.BlackAnt, -1f), (EnemyType.AcidAnt, 0f), (EnemyType.RedAnt, 1f) })
                ants.Add(w.HostSpawnEnemy(t, p.GlobalPosition + fwd * 16f + side * k * 10f));
            At(app, 4f, () =>
            {
                foreach (var e in ants)
                    if (GodotObject.IsInstanceValid(e)) w.HostDamageEnemy(e.Id, 1e6f, e.Center, Net.I.MyId, false);
            });
            At(app, 5f, () => GD.Print($"[splat] marques : {ImpactMarks.For(w).Multimesh.VisibleInstanceCount}"));
            At(app, 6f, () => Shot(app, "splat_00"));
            At(app, 7f, () => { Player.BotLookAt = p.GlobalPosition + fwd * 9f; });
            At(app, 9f, () => Shot(app, "splat_01"));
            At(app, 9.5f, () => GD.Print("[splat] fin"));
        });
    }

    /// <summary>Caméra qui tourne lentement autour d'un point, à hauteur d'homme.</summary>
    private partial class OrbitCam : Node
    {
        public Camera3D Cam;
        public Vector3 Center, Fwd, Side;
        private float _t;

        public override void _Process(double delta)
        {
            if (!IsInstanceValid(Cam)) return;
            _t += (float)delta;
            var a = -0.5f + _t * 0.25f;
            var dir = (Fwd * Mathf.Cos(a) + Side * Mathf.Sin(a)).Normalized();
            Cam.GlobalPosition = Center + dir * 5.2f + Vector3.Up * 1.6f;
            Cam.LookAt(Center, Vector3.Up);
        }
    }
}
