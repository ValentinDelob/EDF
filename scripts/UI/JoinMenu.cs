using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Rejoindre une partie LAN : liste des parties détectées + connexion directe par IP.</summary>
public partial class JoinMenu : MenuScreen
{
    private VBoxContainer _list;
    private LineEdit _ip, _name;
    private Label _status;
    private Button _join;
    private float _refresh, _pulse;
    private string _selectedIp;
    private string _shown;
    private bool _connecting;

    public override void _Ready()
    {
        BuildFrame("Rejoindre (LAN)");
        Net.I.StartListening();
        // Panneau d'information vert (maquettes E) : parties détectées puis connexion directe.
        var panel = UiE.Panel(3, 22, 18);
        panel.Position = new Vector2(50, 4);
        panel.MouseFilter = MouseFilterEnum.Pass;
        Body.AddChild(panel);
        var col = UiKit.VBox(4);
        col.CustomMinimumSize = new Vector2(820, 0);
        panel.AddChild(col);

        var lh = UiKit.HBox();
        var e = UiE.Label("Parties sur le réseau local", 18, UiE.Mint);
        e.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        lh.AddChild(e);
        _status = UiE.Label("● Recherche…", 16, UiE.Bright);
        lh.AddChild(_status);
        col.AddChild(lh);
        col.AddChild(UiKit.Gap(4));
        _list = UiKit.VBox(2);
        col.AddChild(_list);

        col.AddChild(UiKit.Gap(32));
        col.AddChild(UiE.Label("Connexion directe", 18, UiE.Mint));
        col.AddChild(UiKit.Gap(4));
        var row = UiKit.HBox(16);
        var ipBox = UiKit.VBox(6);
        ipBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ipBox.AddChild(UiE.Label("Adresse IP de l'hôte", 16, UiE.TextDim));
        _ip = new LineEdit { Text = Settings.I.LastIp, CustomMinimumSize = new Vector2(0, 48), PlaceholderText = "192.168.1.20" };
        _ip.TextSubmitted += _ => Connect(_ip.Text);
        _ip.TextChanged += _ => { _selectedIp = null; _shown = null; Rebuild(); };
        ipBox.AddChild(_ip);
        row.AddChild(ipBox);
        var nameBox = UiKit.VBox(6);
        nameBox.CustomMinimumSize = new Vector2(280, 0);
        nameBox.AddChild(UiE.Label("Nom du soldat", 16, UiE.TextDim));
        _name = new LineEdit { Text = Settings.I.PlayerName, MaxLength = 20, CustomMinimumSize = new Vector2(0, 48) };
        _name.TextSubmitted += _ => Connect(_selectedIp ?? _ip.Text);
        nameBox.AddChild(_name);
        row.AddChild(nameBox);
        col.AddChild(row);
        var help = UiE.Label($"Ports UDP {Net.GamePort} (jeu) et {Net.DiscoveryPort} (découverte). À la première connexion, autorisez Rempart dans le pare-feu Windows.", 15, UiE.TextMuted);
        help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(UiKit.Gap(8));
        col.AddChild(help);

        FooterLeft.AddChild(UiE.KeyHint("Entrée:Rejoindre"));
        FooterLeft.AddChild(UiE.KeyHint("Échap:Retour", Back));
        _join = new EPlate("Rejoindre", () => Connect(_selectedIp ?? _ip.Text), 260) { Icon = UiE.Icon.Play };
        FooterRight.AddChild(_join);
        Rebuild();
        UiKit.FocusLater(_join);
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
        if (ip.Length == 0)
        {
            _status.Text = "Saisissez l'adresse IP de l'hôte ou choisissez une partie.";
            _status.AddThemeColorOverride("font_color", UiE.Danger);
            UiKit.FocusSilently(_ip);
            return;
        }
        Settings.I.PlayerName = string.IsNullOrWhiteSpace(_name.Text) ? "Soldat" : _name.Text.Trim();
        Settings.I.LastIp = ip;
        Settings.I.Save();
        var err = Net.I.Join(ip);
        _connecting = err == Error.Ok;
        _status.Text = err == Error.Ok ? $"Connexion à {ip}…" : $"Erreur : {err}";
        _status.AddThemeColorOverride("font_color", err == Error.Ok ? UiE.Mint : UiE.Danger);
    }

    public override void _ExitTree() => Net.I.StopListening();

    public override void _Process(double delta)
    {
        // Pastille de recherche qui pulse doucement (indique que la découverte tourne).
        _pulse += (float)delta;
        if (!_connecting && _status.Text.StartsWith("●"))
            _status.Modulate = new Color(1, 1, 1, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_pulse * 2.2f)));
        else _status.Modulate = Colors.White;

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
        var sig = string.Join("|", games.Select(g => $"{g.Ip}{g.Players}{g.Mission}")) + "#" + _selectedIp;
        if (sig == _shown) return; // pas de reconstruction inutile (évite le scintillement)
        _shown = sig;
        if (!_connecting)
        {
            _status.Text = games.Count == 0 ? "● Recherche…" : $"● {games.Count} partie{(games.Count > 1 ? "s" : "")}";
            _status.AddThemeColorOverride("font_color", UiE.Bright);
        }
        var focused = FocusedKey();
        foreach (var c in _list.GetChildren()) c.QueueFree();
        if (games.Count == 0)
        {
            // État vide : explication sur une ligne de la même hauteur qu'une partie (pas de saut de mise en page).
            var empty = UiE.Label("Aucune partie détectée pour l'instant. L'hôte doit choisir « Héberger (LAN) » sur le même réseau ; sinon, saisissez son adresse IP ci-dessous.", 16, UiE.TextDim);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            empty.CustomMinimumSize = new Vector2(0, 56);
            empty.VerticalAlignment = VerticalAlignment.Center;
            _list.AddChild(empty);
            return;
        }
        foreach (var g in games)
        {
            var ip = g.Ip;
            var b = UiKit.Button("", () => { _selectedIp = ip; _ip.Text = ip; _shown = null; Rebuild(); }, "Row", 56);
            b.ToggleMode = true;
            b.SetPressedNoSignal(_selectedIp == ip);
            SetFocusKey(b, "g" + ip);
            b.GuiInput += ev => { if (ev is InputEventMouseButton { DoubleClick: true }) Connect(ip); };
            var h = UiKit.HBox(24);
            h.MouseFilter = MouseFilterEnum.Ignore;
            UiKit.FillRect(h);
            h.OffsetLeft = 22; h.OffsetRight = -22;
            var n = UiE.Label(g.Name, 19);
            n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            n.VerticalAlignment = VerticalAlignment.Center;
            n.ClipText = true;
            h.AddChild(n);
            foreach (var (t, w) in new[] { (g.Mission, 200), ($"{g.Players}/{Net.MaxPlayers}", 56), (g.Ip, 136) })
            {
                var l = UiE.Label(t, 16, UiE.TextDim);
                l.VerticalAlignment = VerticalAlignment.Center;
                l.CustomMinimumSize = new Vector2(w, 0);
                l.ClipText = true;
                h.AddChild(l);
            }
            b.AddChild(h);
            _list.AddChild(b);
        }
        if (focused != null) RestoreFocus(focused, _join);
    }
}
