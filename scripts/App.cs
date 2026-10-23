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
        _screen?.QueueFree();
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
    public static void Run(App app)
    {
        var dir = Screenshot.CmdArg("shots");
        var quitAfter = Screenshot.CmdArg("quit-after");
        if (quitAfter != null && float.TryParse(quitAfter, NumberStyles.Float, CultureInfo.InvariantCulture, out var q))
            app.GetTree().CreateTimer(q, true, false, true).Timeout += app.Quit;

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
        // --tank : le joueur monte dans le char au bout de 4 s.
        if (Screenshot.CmdArg("tank") != null)
            app.GetTree().CreateTimer(5f).Timeout += () =>
            {
                var w = app.CurrentWorld;
                if (w?.LocalPlayer == null) return;
                w.LocalPlayer.GlobalPosition = new Vector3(14, 1, 17);
                Net.I.RequestTankSeat(1, true);
            };
        // --board=N : affiche le tableau des scores (Tab) à partir de N secondes.
        if (Screenshot.CmdArg("board") is { } bd && float.TryParse(bd, NumberStyles.Float, CultureInfo.InvariantCulture, out var boardAt))
            app.GetTree().CreateTimer(boardAt).Timeout += () => Input.ActionPress("scoreboard");
        // Test LAN : --autohost=2 (attend 2 joueurs puis lance) / --autojoin=127.0.0.1
        Player.Bot = Screenshot.CmdArg("bot") != null;
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
            };
        ScheduleShots(app, dir, $"mission{mission}");
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
