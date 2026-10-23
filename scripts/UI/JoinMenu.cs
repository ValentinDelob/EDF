using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Rejoindre une partie LAN : parties détectées automatiquement ou adresse IP.</summary>
public partial class JoinMenu : Control
{
    private VBoxContainer _list;
    private LineEdit _ip, _name;
    private Label _status;
    private float _refresh;

    public override void _Ready()
    {
        Theme = UiKit.Theme;
        UiKit.FillRect(this);
        AddChild(UiKit.Scrim(0.7f));
        Net.I.StartListening();
        var m = UiKit.Margin(96, 72, 96, 64);
        UiKit.FillRect(m);
        AddChild(m);
        var root = UiKit.VBox(32);
        m.AddChild(root);
        var head = UiKit.HBox(24);
        head.AddChild(UiKit.Button("‹ Retour", Back, null, 44));
        head.AddChild(UiKit.Heading("Rejoindre en LAN", 44));
        root.AddChild(head);

        var cols = UiKit.HBox(48);
        cols.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddChild(cols);

        var left = UiKit.VBox(10);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        left.SizeFlagsStretchRatio = 1.6f;
        var lh = UiKit.HBox();
        var e = UiKit.Eyebrow("Parties sur le réseau local");
        e.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        lh.AddChild(e);
        _status = UiKit.Label("Recherche…", 14, UiKit.Good);
        lh.AddChild(_status);
        left.AddChild(lh);
        left.AddChild(UiKit.Rule());
        _list = UiKit.VBox(8);
        left.AddChild(_list);
        cols.AddChild(left);

        var right = UiKit.VBox(14);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        right.AddChild(UiKit.Eyebrow("Connexion directe"));
        right.AddChild(UiKit.Rule());
        right.AddChild(UiKit.Label("Nom du soldat", 15, UiKit.Muted));
        _name = new LineEdit { Text = Settings.I.PlayerName, MaxLength = 20, CustomMinimumSize = new Vector2(0, 48) };
        right.AddChild(_name);
        right.AddChild(UiKit.Label("Adresse IP de l'hôte", 15, UiKit.Muted));
        _ip = new LineEdit { Text = Settings.I.LastIp, CustomMinimumSize = new Vector2(0, 48) };
        _ip.TextSubmitted += _ => Connect(_ip.Text);
        right.AddChild(_ip);
        right.AddChild(UiKit.Label($"Ports UDP {Net.GamePort} (jeu) et {Net.DiscoveryPort} (découverte). Autorisez Rempart dans le pare-feu Windows.", 14, UiKit.Muted));
        ((Label)right.GetChild(right.GetChildCount() - 1)).AutowrapMode = TextServer.AutowrapMode.WordSmart;
        right.AddChild(UiKit.Button("Rejoindre", () => Connect(_ip.Text), "Primary", 56));
        cols.AddChild(right);
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
        ip = ip.Trim();
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
        foreach (var c in _list.GetChildren()) c.QueueFree();
        var games = Net.I.FoundGames.Values.OrderBy(g => g.Name).ToList();
        if (games.Count == 0)
        {
            _list.AddChild(UiKit.Label("Aucune partie détectée pour l'instant. Vérifiez que l'hôte a choisi « Héberger en LAN ».", 16, UiKit.Muted));
            return;
        }
        foreach (var g in games)
        {
            var b = UiKit.Button("", () => Connect(g.Ip), "Card", 68);
            var h = UiKit.HBox(24);
            h.MouseFilter = MouseFilterEnum.Ignore;
            UiKit.FillRect(h);
            h.OffsetLeft = 20;
            h.OffsetRight = -20;
            var n = UiKit.Label(g.Name, 19, UiKit.Text, true);
            n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            n.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(n);
            foreach (var t in new[] { g.Mission, $"{g.Players}/{Net.MaxPlayers}", g.Ip })
            {
                var l = UiKit.Label(t, 16, UiKit.Muted);
                l.VerticalAlignment = VerticalAlignment.Center;
                l.CustomMinimumSize = new Vector2(120, 0);
                h.AddChild(l);
            }
            b.AddChild(h);
            _list.AddChild(b);
        }
    }
}
