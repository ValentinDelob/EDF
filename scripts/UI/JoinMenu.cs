using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Rejoindre une partie LAN : liste des parties détectées + connexion directe par IP.</summary>
public partial class JoinMenu : MenuScreen
{
    private VBoxContainer _list;
    private LineEdit _ip, _name;
    private Label _status;
    private float _refresh;
    private string _selectedIp;
    private string _shown = "";

    public override void _Ready()
    {
        BuildFrame("Rejoindre", 0.35f, 1100);
        Net.I.StartListening();
        var col = UiKit.VBox(4);
        col.CustomMinimumSize = new Vector2(820, 0);
        Body.AddChild(col);

        var lh = UiKit.HBox();
        var e = UiKit.Eyebrow("Parties sur le réseau local");
        e.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        lh.AddChild(e);
        _status = UiKit.Label("● Recherche…", 14, UiKit.Good);
        lh.AddChild(_status);
        col.AddChild(lh);
        col.AddChild(UiKit.Gap(4));
        _list = UiKit.VBox(2);
        col.AddChild(_list);

        col.AddChild(UiKit.Gap(36));
        col.AddChild(UiKit.Eyebrow("Connexion directe"));
        col.AddChild(UiKit.Gap(6));
        var row = UiKit.HBox(16);
        var ipBox = UiKit.VBox(6);
        ipBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ipBox.AddChild(UiKit.Label("Adresse IP de l'hôte", 14, UiKit.Muted));
        _ip = new LineEdit { Text = Settings.I.LastIp, CustomMinimumSize = new Vector2(0, 48), PlaceholderText = "192.168.1.20" };
        _ip.TextSubmitted += _ => Connect(_ip.Text);
        _ip.TextChanged += _ => _selectedIp = null;
        ipBox.AddChild(_ip);
        row.AddChild(ipBox);
        var nameBox = UiKit.VBox(6);
        nameBox.CustomMinimumSize = new Vector2(280, 0);
        nameBox.AddChild(UiKit.Label("Nom du soldat", 14, UiKit.Muted));
        _name = new LineEdit { Text = Settings.I.PlayerName, MaxLength = 20, CustomMinimumSize = new Vector2(0, 48) };
        nameBox.AddChild(_name);
        row.AddChild(nameBox);
        col.AddChild(row);
        var help = UiKit.Label($"Ports UDP {Net.GamePort} (jeu) et {Net.DiscoveryPort} (découverte). À la première connexion, autorisez Rempart dans le pare-feu Windows.", 14, UiKit.Dim);
        help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(UiKit.Gap(8));
        col.AddChild(help);

        FooterLeft.AddChild(UiKit.KeyHint("Échap", "Retour", Back));
        FooterRight.AddChild(UiKit.Button("Rejoindre", () => Connect(_selectedIp ?? _ip.Text), "Primary", 60));
        App.I.Background?.SetMode(MenuBackground.Mode.Title);
        Rebuild();
    }

    private void Back()
    {
        Net.I.StopListening();
        Net.I.Leave();
        App.I.ShowMain();
    }

    private void Connect(string ip)
    {
        ip = (ip ?? "").Trim();
        if (ip.Length == 0) return;
        Settings.I.PlayerName = string.IsNullOrWhiteSpace(_name.Text) ? "Soldat" : _name.Text.Trim();
        Settings.I.LastIp = ip;
        Settings.I.Save();
        var err = Net.I.Join(ip);
        _status.Text = err == Error.Ok ? $"Connexion à {ip}…" : $"Erreur : {err}";
        _status.AddThemeColorOverride("font_color", err == Error.Ok ? UiKit.Accent : UiKit.Danger);
    }

    public override void _ExitTree() => Net.I.StopListening();

    public override void _Process(double delta)
    {
        _refresh -= (float)delta;
        if (_refresh > 0) return;
        _refresh = 1f;
        Rebuild();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { Back(); GetViewport().SetInputAsHandled(); }
    }

    private void Rebuild()
    {
        var games = Net.I.FoundGames.Values.OrderBy(g => g.Name).ToList();
        var sig = string.Join("|", games.Select(g => $"{g.Ip}{g.Players}{g.Mission}")) + _selectedIp;
        if (sig == _shown) return; // pas de reconstruction inutile (évite le scintillement)
        _shown = sig;
        foreach (var c in _list.GetChildren()) c.QueueFree();
        if (games.Count == 0)
        {
            _list.AddChild(UiKit.Label("Aucune partie détectée. Vérifiez que l'hôte a choisi « Héberger (LAN) » et qu'il est sur le même réseau.", 15, UiKit.Muted));
            return;
        }
        foreach (var g in games)
        {
            var ip = g.Ip;
            var b = UiKit.Button("", () => { _selectedIp = ip; _ip.Text = ip; _shown = ""; Rebuild(); }, "Row", 56);
            b.ToggleMode = true;
            b.SetPressedNoSignal(_selectedIp == ip);
            b.GuiInput += ev => { if (ev is InputEventMouseButton { DoubleClick: true }) Connect(ip); };
            var h = UiKit.HBox(24);
            h.MouseFilter = MouseFilterEnum.Ignore;
            UiKit.FillRect(h);
            h.OffsetLeft = 22; h.OffsetRight = -22;
            var n = UiKit.Label(g.Name, 18);
            n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            n.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(n);
            foreach (var (t, w) in new[] { (g.Mission, 220), ($"{g.Players}/{Net.MaxPlayers}", 60), (g.Ip, 140) })
            {
                var l = UiKit.Label(t, 15, UiKit.Muted);
                l.VerticalAlignment = VerticalAlignment.Center;
                l.CustomMinimumSize = new Vector2(w, 0);
                h.AddChild(l);
            }
            b.AddChild(h);
            _list.AddChild(b);
        }
    }
}
