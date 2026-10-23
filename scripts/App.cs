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

    private void StartMission(int mission, Difficulty d, int seed)
    {
        CleanupWorld();
        _screen?.QueueFree();
        _screen = null;
        _bg?.QueueFree();
        _bg = null;
        _world = World.Create(mission, d, seed);
        AddChild(_world);
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
        if (dir == null) return;
        var times = (Screenshot.CmdArg("shot-times") ?? "6,12").Split(',')
            .Select(s => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 5f);
        foreach (var t in times)
            app.GetTree().CreateTimer(t, true, false, true).Timeout += () => Screenshot.Take(app.GetViewport(), $"mission{mission}");
    }
}
