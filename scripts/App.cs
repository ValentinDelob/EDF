using System;
using System.Globalization;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Racine de l'application : navigation entre les écrans et lancement des missions.</summary>
public partial class App : Node
{
    public static App I { get; private set; }
    private CanvasLayer _ui;
    private Control _screen;
    private MenuBackground _bg;
    private World _world;
    private ResultsScreen _results;
    private string _pendingNotice;

    public override void _EnterTree() => I = this;

    public override void _Ready()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        _ui = new CanvasLayer { Layer = 10 };
        AddChild(_ui);
        Net.I.MissionStarting += StartMission;
        Net.I.ReturnToLobby += BackToLobby;
        Net.I.SessionEnded += reason =>
        {
            CleanupWorld();
            _pendingNotice = reason;
            ShowMain();
        };
        Net.I.LobbyChanged += () =>
        {
            if (_screen is JoinMenu && Net.I.Players.ContainsKey(Net.I.MyId)) ShowLobby();
            else if (_screen is LobbyMenu l) l.Refresh();
        };
        ShowMain();
        AutoTest.Run(this);
    }

    private void EnsureBackground()
    {
        if (_bg != null) return;
        _bg = new MenuBackground();
        AddChild(_bg);
        MoveChild(_bg, 0);
    }

    private void SetScreen(Control c)
    {
        // Fondu de sortie court de l'écran précédent (le nouvel écran a son propre fondu d'entrée).
        if (_screen != null && IsInstanceValid(_screen))
        {
            var old = _screen;
            // Plus d'entrées pour l'ancien écran, mais il doit continuer à traiter son fondu.
            old.MouseFilter = Control.MouseFilterEnum.Ignore;
            old.SetProcessInput(false);
            old.SetProcessUnhandledInput(false);
            foreach (var n in old.FindChildren("*", "Control", true, false))
                if (n is Control ctl) { ctl.MouseFilter = Control.MouseFilterEnum.Ignore; ctl.FocusMode = Control.FocusModeEnum.None; }
            // Tween porté par App : celui de l'écran désactivé ne serait jamais traité (l'écran resterait affiché).
            var tw = old.CreateTween();
            tw.TweenProperty(old, "modulate:a", 0f, 0.12f);
            tw.TweenCallback(Callable.From(old.QueueFree));
        }
        _screen = c;
        _ui.AddChild(c);
    }

    public void ShowMain()
    {
        EnsureBackground();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _bg.SetMode(MenuBackground.Mode.Title);
        var m = new MainMenu();
        SetScreen(m);
        if (_pendingNotice != null) { m.Notice(_pendingNotice); _pendingNotice = null; }
    }

    public void ShowJoin()
    {
        EnsureBackground();
        SetScreen(new JoinMenu());
    }

    public void ShowLobby()
    {
        EnsureBackground();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        SetScreen(new LobbyMenu());
    }

    public void ShowArsenal(Action back)
    {
        EnsureBackground();
        SetScreen(new ArsenalMenu { OnBack = back });
    }

    public void ShowGuide()
    {
        EnsureBackground();
        SetScreen(new GuideMenu());
    }

    public void ShowOptions()
    {
        var o = new OptionsMenu { OnClose = () => { } };
        _ui.AddChild(o);
    }

    public void StartSolo()
    {
        Net.I.StartSolo();
        ShowLobby();
    }

    public void Host()
    {
        var err = Net.I.Host();
        if (err != Error.Ok)
        {
            _pendingNotice = $"Impossible d'héberger (port {Net.GamePort} occupé ?) : {err}";
            ShowMain();
            return;
        }
        ShowLobby();
    }

    private static readonly string[] Tips =
    {
        "Les caisses rouges contiennent des armes : plus la difficulté est haute, plus elles sont puissantes.",
        "Les charognards avalent les caisses au sol. Tuez-les pour tout récupérer, avec un bonus.",
        "Visez la tête des insectes : les points faibles infligent 50 % de dégâts en plus.",
        "Les nids ne craignent vraiment que les explosifs : roquettes, grenades et frappes d'artillerie.",
        "Le char de combat attend près du point de départ : approchez-vous et appuyez sur la touche d'interaction.",
        "Le Commando aéroporté partage la même énergie pour voler et recharger : ne tombez pas à court en plein ciel.",
        "L'Opérateur d'appui remplit sa jauge en infligeant des dégâts ; pleine, elle déclenche un barrage d'artillerie.",
        "Un équipier à terre se relève en maintenant la touche d'interaction près de lui.",
    };

    private Control BuildLoading(MissionDef m, Difficulty d)
    {
        var root = new Control { Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Stop };
        UiKit.FillRect(root);
        root.AddChild(new ColorRect { Color = UiKit.Bg, AnchorRight = 1, AnchorBottom = 1 });
        var v = UiKit.VBox(8);
        v.Position = new Vector2(UiKit.ScreenX, 0);
        v.AnchorTop = 0.5f;
        v.AnchorBottom = 0.5f;
        v.GrowVertical = Control.GrowDirection.Both;
        v.AddChild(UiKit.Eyebrow($"Mission {m.Index + 1:00} · {Difficulties.Names[(int)d]}", UiKit.Accent));
        v.AddChild(UiKit.Heading(m.Name, 72));
        var brief = UiKit.Label(m.Briefing, 18, new Color(UiKit.Text, 0.85f));
        brief.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        brief.CustomMinimumSize = new Vector2(760, 0);
        v.AddChild(brief);
        root.AddChild(v);
        var foot = UiKit.VBox(6);
        foot.AddChild(UiKit.Label("DÉPLOIEMENT…", 16, UiKit.Muted, true));
        var tip = UiKit.Label("Conseil : " + Tips[(int)(GD.Randi() % Tips.Length)], 15, UiKit.Muted);
        tip.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        tip.CustomMinimumSize = new Vector2(900, 0);
        foot.AddChild(tip);
        root.AddChild(foot);
        foot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        foot.Position = new Vector2(UiKit.ScreenX, foot.Position.Y - UiKit.ScreenBottom - 30);
        return root;
    }

    private async void StartMission(int mission, Difficulty d, int seed)
    {
        CleanupWorld();
        _screen?.QueueFree();
        _screen = null;
        // Écran de chargement affiché pendant la génération de la ville (deux images pour qu'il soit dessiné).
        var loading = BuildLoading(Missions.All[mission], d);
        _ui.AddChild(loading);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _bg?.QueueFree();
        _bg = null;
        _world = World.Create(mission, d, seed);
        AddChild(_world);
        var fade = loading.CreateTween();
        fade.TweenInterval(0.3f);
        fade.TweenProperty(loading, "modulate:a", 0f, 0.5f);
        fade.TweenCallback(Callable.From(loading.QueueFree));
        _world.Finished += success =>
        {
            _results = new ResultsScreen
            {
                Report = _world.Report,
                OnContinue = () =>
                {
                    if (Net.I.IsHost) Net.I.HostReturnToLobby();
                },
            };
            AddChild(_results);
        };
    }

    private void BackToLobby()
    {
        CleanupWorld();
        if (Net.I.InSession) ShowLobby();
        else ShowMain();
    }

    private void CleanupWorld()
    {
        GetTree().Paused = false;
        _results?.QueueFree();
        _results = null;
        if (_world != null)
        {
            _world.QueueFree();
            _world = null;
        }
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void Quit() => GetTree().Quit();

    public World CurrentWorld => _world;
    public MenuBackground Background => _bg;
    public Control CurrentScreen => _screen;
}

/// <summary>
/// Pilotage automatique pour les tests et les captures : arguments après « -- » :
/// --autostart=0 --class=0 --difficulty=1 --shots=chemin --shot-times=4,10 --quit-after=20 --menu-shots.
/// </summary>
public static class AutoTest
{
    /// <summary>Attend que la mission soit chargée (durée variable, partie LAN), puis demande la place du véhicule.</summary>
    private static void BoardWhenReady(App app, int vehicleId, Vector3 near)
    {
        app.GetTree().CreateTimer(1f).Timeout += () =>
        {
            var w = app.CurrentWorld;
            if (w?.LocalPlayer == null || !GodotObject.IsInstanceValid(w.LocalPlayer)) { BoardWhenReady(app, vehicleId, near); return; }
            app.GetTree().CreateTimer(1.5f).Timeout += () =>
            {
                if (w.LocalPlayer == null || !GodotObject.IsInstanceValid(w.LocalPlayer)) return;
                w.LocalPlayer.GlobalPosition = near;
                Net.I.RequestVehicleSeat(vehicleId, true);
            };
        };
    }

    private static void ArsenalShots(App app)
    {
        var rng = new RandomNumberGenerator { Seed = 42 };
        var pity = 0;
        foreach (var cd in Classes.All)
            for (var i = 0; i < 12; i++)
            {
                var w = Loot.RollWeapon(cd.Id, (Difficulty)(i % 5), i / 11f, rng, ref pity);
                Profile.I.Grant(w);
            }
        // Migration d'armes d'un profil version 1 (rareté + 5 étoiles 1..5), lue comme depuis le fichier JSON.
        foreach (var json in new[]
                 {
                     """{"Uid":"legacy0001","TemplateId":"fa30","Level":33,"Rarity":4,"Stars":[5,3,1,2,4],"IsNew":false}""",
                     """{"Uid":"legacy0002","TemplateId":"art1","Level":20,"Rarity":2,"Stars":[4,2,5,3,1],"IsNew":false}""",
                 })
        {
            var old = System.Text.Json.JsonSerializer.Deserialize<WeaponInstance>(json);
            old.MigrateLegacy();
            GD.Print($"Migration {old.TemplateId} Nv.{old.Level} : étoiles [{string.Join(", ", old.Stars)}] → {System.Text.Json.JsonSerializer.Serialize(old)}");
            Profile.I.Data.Arsenal.Add(old);
        }
        var tree = app.GetTree();
        void Key(Key k)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
        }
        void At(float s, Action a) => tree.CreateTimer(s, true, false, true).Timeout += a;
        At(3f, () => app.ShowArsenal(app.ShowMain));
        At(5.5f, () => Screenshot.Take(app.GetViewport(), "arsenal_open"));
        At(6f, () => { Key(Godot.Key.Right); Key(Godot.Key.Down); });
        At(7f, () => Screenshot.Take(app.GetViewport(), "arsenal_nav"));
        At(7.5f, () => { Key(Godot.Key.E); Key(Godot.Key.Enter); });
        At(8.5f, () => Screenshot.Take(app.GetViewport(), "arsenal_equip"));
        At(9f, () => Key(Godot.Key.Tab));
        At(10.5f, () => Screenshot.Take(app.GetViewport(), "arsenal_class2"));
        At(11f, () => Key(Godot.Key.Tab));
        At(11.5f, () => Key(Godot.Key.Right));
        At(12.5f, () => Screenshot.Take(app.GetViewport(), "arsenal_class3"));
        At(13f, () => Key(Godot.Key.Tab));
        At(14.5f, () => Screenshot.Take(app.GetViewport(), "arsenal_class4"));
    }

    public static void Run(App app)
    {
        var dir = Screenshot.CmdArg("shots");
        var quitAfter = Screenshot.CmdArg("quit-after");
        if (quitAfter != null && float.TryParse(quitAfter, NumberStyles.Float, CultureInfo.InvariantCulture, out var q))
            app.GetTree().CreateTimer(q, true, false, true).Timeout += app.Quit;

        // --menu-shots=arsenal : profil de test garni d'armes de tous types, puis captures de l'arsenal
        // (navigation clavier simulée, changement d'emplacement et de classe).
        if (Screenshot.CmdArg("menu-shots") == "arsenal")
        {
            ArsenalShots(app);
            return;
        }
        if (Screenshot.CmdArg("menu-shots") != null)
        {
            var tree = app.GetTree();
            void Shot(float at, Action before, string name)
            {
                tree.CreateTimer(at, true, false, true).Timeout += () =>
                {
                    before?.Invoke();
                    tree.CreateTimer(1.2f, true, false, true).Timeout += () => Screenshot.Take(app.GetViewport(), name);
                };
            }
            Shot(3f, null, "menu_main");
            Shot(6f, app.StartSolo, "menu_lobby");
            Shot(9f, () => app.ShowOptions(), "menu_options");
            Shot(12f, () => app.ShowArsenal(app.ShowMain), "menu_arsenal");
            Shot(15f, app.ShowJoin, "menu_join");
            Shot(18f, app.ShowGuide, "menu_guide");
        }

        // --end-after=N : termine la mission (succès) après N s avec du butin fictif, pour tester le bilan.
        if (Screenshot.CmdArg("end-after") is { } ea && float.TryParse(ea, NumberStyles.Float, CultureInfo.InvariantCulture, out var endAfter))
            app.GetTree().CreateTimer(endAfter).Timeout += () =>
            {
                var w = app.CurrentWorld;
                if (w == null) return;
                w.CratesCollected[(int)CrateType.Weapon] += 6;
                w.CratesCollected[(int)CrateType.Armor] += 14;
                w.CratesCollected[(int)CrateType.Credits] += 3;
                w.EndMission(true);
            };
        // --tank / --heli : le joueur monte dans le char ou l'hélicoptère peu après le chargement de la mission
        // (le bot fait voler l'hélicoptère).
        if (Screenshot.CmdArg("tank") != null) BoardWhenReady(app, 1, new Vector3(14, 1, 17));
        if (Screenshot.CmdArg("heli") != null) BoardWhenReady(app, 2, new Vector3(-16, 1, 19));
        // --board=N : affiche le tableau des scores (Tab) à partir de N secondes.
        if (Screenshot.CmdArg("board") is { } bd && float.TryParse(bd, NumberStyles.Float, CultureInfo.InvariantCulture, out var boardAt))
            app.GetTree().CreateTimer(boardAt).Timeout += () => Input.ActionPress("scoreboard");
        // Test LAN : --autohost=2 (attend 2 joueurs puis lance) / --autojoin=127.0.0.1
        Player.Bot = Screenshot.CmdArg("bot") != null;
        Player.God = Screenshot.CmdArg("god") != null;
        Player.BotHoldFire = Screenshot.CmdArg("holdfire") != null;
        RoofTest(app);
        if (Screenshot.CmdArg("name") is { } nm) Settings.I.PlayerName = nm;
        if (Screenshot.CmdArg("class") is { } cc && int.TryParse(cc, out var ccls)) Profile.I.Data.SelectedClass = (SoldierClass)ccls;
        var mission0 = int.TryParse(Screenshot.CmdArg("mission") ?? "0", out var mm) ? mm : 0;
        if (Screenshot.CmdArg("autohost") is { } ah && int.TryParse(ah, out var expected))
        {
            app.GetTree().CreateTimer(0.5f).Timeout += app.Host;
            var started = false;
            Net.I.LobbyChanged += () =>
            {
                if (started || Net.I.Players.Count < expected) return;
                started = true;
                app.GetTree().CreateTimer(2f).Timeout += () =>
                {
                    Net.I.SetLobbyMission(mission0, Difficulty.Normal);
                    Net.I.HostStartMission();
                };
            };
            ScheduleShots(app, dir, "host");
            return;
        }
        if (Screenshot.CmdArg("autojoin") is { } ip)
        {
            app.GetTree().CreateTimer(2f).Timeout += () => { app.ShowJoin(); Net.I.Join(ip); };
            ScheduleShots(app, dir, "client");
            return;
        }

        var auto = Screenshot.CmdArg("autostart");
        if (auto == null) return;
        Hud.ForceHidden = Screenshot.CmdArg("nohud") != null; // --nohud : captures sans interface (fonds de maquettes)
        if (Screenshot.CmdArg("class") is { } c && int.TryParse(c, out var cls)) Profile.I.Data.SelectedClass = (SoldierClass)cls;
        var diff = Screenshot.CmdArg("difficulty") is { } ds && int.TryParse(ds, out var di) ? (Difficulty)di : Difficulty.Normal;
        int.TryParse(auto, out var mission);
        Player.Bot = Screenshot.CmdArg("bot") != null;
        app.GetTree().CreateTimer(1.0f).Timeout += () =>
        {
            app.StartSolo();
            Net.I.SetLobbyMission(mission, diff);
            Net.I.HostStartMission();
        };
        // --stress=N : fait apparaître N fourmis autour du joueur (test de performance), --novsync.
        if (Screenshot.CmdArg("novsync") != null) DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        if (Screenshot.CmdArg("stress") is { } st && int.TryParse(st, out var stressN))
            app.GetTree().CreateTimer(4f).Timeout += () =>
            {
                var w = app.CurrentWorld;
                if (w == null) return;
                for (var i = 0; i < stressN; i++)
                {
                    var a = i * 2.399f;
                    var r = 40f + (i % 30) * 3f;
                    w.HostSpawnEnemy(i % 7 == 0 ? EnemyType.RedAnt : EnemyType.BlackAnt, new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r - 40));
                }
            };
        // --showcase : un spécimen de chaque ennemi, figé, aligné devant le joueur.
        if (Screenshot.CmdArg("showcase") != null)
            app.GetTree().CreateTimer(3f).Timeout += () =>
            {
                var w = app.CurrentWorld;
                if (w == null) return;
                Enemy.Frozen = true;
                var big = Screenshot.CmdArg("showcase") == "big";
                var types = big ? new[] { EnemyType.Nest, EnemyType.Queen } : new[] { EnemyType.BlackAnt, EnemyType.Spider, EnemyType.AcidAnt, EnemyType.Drone, EnemyType.Wasp };
                for (var i = 0; i < types.Length; i++)
                {
                    var p = big ? new Vector3(-16 + i * 30, 0, -34) : new Vector3(-14 + i * 7, types[i] is EnemyType.Wasp or EnemyType.Drone ? 3.5f : 0, -16);
                    var e = w.HostSpawnEnemy(types[i], p);
                    e.GlobalPosition = p;
                    e.Rotation = new Vector3(0, 0.6f, 0);
                }
                if (!big)
                {
                    // Mille-pattes au premier plan, de trois quarts, le corps en S pour montrer la trace.
                    var p = new Vector3(6, 0, -2);
                    var c = w.HostSpawnEnemy(EnemyType.Centipede, p);
                    c.GlobalPosition = p;
                    c.Rotation = new Vector3(0, 1.0f, 0);
                    c.Model.SeedCentipedeTrail(0.45f);
                }
            };
        ScheduleShots(app, dir, $"mission{mission}");
    }

    /// <summary>
    /// --roof[=N] : le soldat (bot immobile) est posé sur le toit de l'immeuble le plus proche du départ et N insectes (30)
    /// apparaissent autour : ils doivent escalader les façades pour l'atteindre. --holdfire : le bot vise sans tirer.
    /// Fonctionne aussi avec --autohost (le soldat de l'hôte monte sur le toit, test de l'escalade vue du client).
    /// </summary>
    private static void RoofTest(App app)
    {
        if (Screenshot.CmdArg("roof") is not { } arg) return;
        var n = int.TryParse(arg, out var v) ? v : 30;
        Player.BotStill = true;
        void Try()
        {
            app.GetTree().CreateTimer(1f).Timeout += () =>
            {
                var w = app.CurrentWorld;
                if (w?.LocalPlayer == null || !GodotObject.IsInstanceValid(w.LocalPlayer) || w.Elapsed < 2.5f) { Try(); return; }
                // Immeuble le plus proche du départ dont le toit est plat à l'endroit où l'on pose le soldat.
                Building best = null;
                var spot = Vector3.Zero;
                var bd = float.MaxValue;
                var space = w.GetWorld3D().DirectSpaceState;
                foreach (var b in w.Buildings)
                {
                    if (b.Destroyed || b.Size.Y < 6f || b.Size.Y > 26f || b.Size.X < 9f || b.Size.Z < 9f) continue;
                    var d = new Vector2(b.GlobalPosition.X, b.GlobalPosition.Z).Length();
                    if (d >= bd) continue;
                    var p = b.GlobalPosition + new Vector3(b.Size.X * 0.22f, 0, b.Size.Z * 0.22f);
                    var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(p + Vector3.Up * (b.Size.Y + 30f), p, 1));
                    if (hit.Count == 0 || ((Vector3)hit["normal"]).Y < 0.95f || ((Vector3)hit["position"]).Y < b.GlobalPosition.Y + b.Size.Y - 0.5f) continue;
                    bd = d;
                    best = b;
                    spot = (Vector3)hit["position"];
                }
                if (best == null) return;
                var c = best.GlobalPosition;
                w.LocalPlayer.GlobalPosition = spot + Vector3.Up * 1.5f;
                w.LocalPlayer.Velocity = Vector3.Zero;
                GD.Print($"[roof] soldat sur l'immeuble {best.Id} ({best.Size.X:0}×{best.Size.Z:0} m, {best.Size.Y:0} m de haut) en {c.X:0},{c.Z:0}");
                if (!Net.I.IsHost) return;
                for (var i = 0; i < n; i++)
                {
                    var a = i * Mathf.Tau / n;
                    var r = Mathf.Max(best.Size.X, best.Size.Z) / 2 + 12f + (i % 3) * 6f;
                    var type = i % 9 == 4 ? EnemyType.Spider : i % 11 == 7 ? EnemyType.Centipede : i % 5 == 2 ? EnemyType.RedAnt : EnemyType.BlackAnt;
                    w.HostSpawnEnemy(type, c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
                }
            };
        }
        Try();
    }

    private static void ScheduleShots(App app, string dir, string prefix)
    {
        if (dir == null) return;
        var times = (Screenshot.CmdArg("shot-times") ?? "6,12").Split(',')
            .Select(s => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 5f);
        foreach (var t in times)
            app.GetTree().CreateTimer(t, true, false, true).Timeout += () => Screenshot.Take(app.GetViewport(), prefix);
    }
}
