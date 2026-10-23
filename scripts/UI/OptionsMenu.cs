using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>Options : contrôles (remappage), souris, graphismes, audio, joueur.</summary>
public partial class OptionsMenu : Control
{
    public Action OnClose;
    private Control _content;
    private readonly List<Button> _tabs = new();
    private string _waitingAction;
    private Button _waitingButton;
    private readonly Dictionary<string, Button> _bindButtons = new();

    public override void _Ready()
    {
        Theme = UiKit.Theme;
        UiKit.FillRect(this);
        AddChild(UiKit.Scrim(0.82f));
        var margin = UiKit.Margin(96, 64, 96, 56);
        UiKit.FillRect(margin);
        AddChild(margin);
        var root = UiKit.VBox(28);
        margin.AddChild(root);

        var header = UiKit.HBox(32);
        header.AddChild(UiKit.Heading("Options", 44));
        var tabs = UiKit.HBox(4);
        tabs.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        foreach (var (name, build) in new (string, Action)[] { ("Contrôles", BuildControls), ("Graphismes", BuildVideo), ("Audio", BuildAudio), ("Jeu", BuildGame) })
        {
            var b = UiKit.Button(name, null, "Chip", 44);
            b.ToggleMode = true;
            var builder = build;
            b.Pressed += () => Select(b, builder);
            tabs.AddChild(b);
            _tabs.Add(b);
        }
        header.AddChild(tabs);
        header.AddChild(UiKit.Spacer(false, true));
        header.AddChild(UiKit.Button("Retour", Close, null, 44));
        root.AddChild(header);

        _content = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(_content);
        Select(_tabs[0], BuildControls);
    }

    private void Close()
    {
        Settings.I.Save();
        OnClose?.Invoke();
        QueueFree();
    }

    public override void _Input(InputEvent e)
    {
        // Capture prioritaire (avant l'interface) pendant l'attente d'une nouvelle touche.
        if (_waitingAction != null)
        {
            if (e is InputEventKey k && k.Pressed && !k.Echo)
            {
                if (k.PhysicalKeycode != Key.Escape) Settings.I.SetBinding(_waitingAction, k);
                EndWait();
                GetViewport().SetInputAsHandled();
            }
            else if (e is InputEventMouseButton mb && mb.Pressed)
            {
                Settings.I.SetBinding(_waitingAction, mb);
                EndWait();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (_waitingAction != null) return;
        if (e.IsActionPressed("pause") || e.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void EndWait()
    {
        _waitingAction = null;
        _waitingButton = null;
        RefreshBindings();
    }

    private void Select(Button tab, Action build)
    {
        foreach (var t in _tabs) t.SetPressedNoSignal(t == tab);
        foreach (var c in _content.GetChildren()) c.QueueFree();
        _bindButtons.Clear();
        build();
    }

    private ScrollContainer Scroll(Control inner)
    {
        var s = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        UiKit.FillRect(s);
        s.AddChild(inner);
        inner.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _content.AddChild(s);
        return s;
    }

    private static Control Row(string label, Control control, string hint = null)
    {
        var h = UiKit.HBox(24);
        h.CustomMinimumSize = new Vector2(0, 52);
        var v = UiKit.VBox(2);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        v.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        v.AddChild(UiKit.Label(label, 18));
        if (hint != null) v.AddChild(UiKit.Label(hint, 14, UiKit.Muted));
        h.AddChild(v);
        control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        h.AddChild(control);
        return h;
    }

    private static Control Slider(float min, float max, float step, float value, Action<float> onChange, Func<float, string> fmt)
    {
        var h = UiKit.HBox(16);
        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(320, 24) };
        var l = UiKit.Label(fmt(value), 18, UiKit.Accent, true);
        l.CustomMinimumSize = new Vector2(70, 0);
        l.HorizontalAlignment = HorizontalAlignment.Right;
        s.ValueChanged += v =>
        {
            l.Text = fmt((float)v);
            onChange((float)v);
        };
        h.AddChild(s);
        h.AddChild(l);
        return h;
    }

    private static CheckButton Toggle(bool value, Action<bool> onChange)
    {
        var c = new CheckButton { ButtonPressed = value };
        c.Toggled += v => onChange(v);
        return c;
    }

    private static Control Section(string title)
    {
        var v = UiKit.VBox(8);
        v.AddChild(UiKit.Eyebrow(title, UiKit.Accent));
        v.AddChild(UiKit.Rule());
        return v;
    }

    private void BuildControls()
    {
        var cols = UiKit.HBox(64);
        var left = UiKit.VBox(4);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        left.CustomMinimumSize = new Vector2(520, 0);
        left.AddChild(Section("Souris"));
        left.AddChild(Row("Sensibilité", Slider(0.1f, 4f, 0.05f, Settings.I.MouseSensitivity, v => Settings.I.MouseSensitivity = v, v => v.ToString("0.00"))));
        left.AddChild(Row("Sensibilité en visée", Slider(0.1f, 2f, 0.05f, Settings.I.AimSensitivity, v => Settings.I.AimSensitivity = v, v => v.ToString("0.00")), "Multiplicateur appliqué en visant"));
        left.AddChild(Row("Inverser l'axe vertical", Toggle(Settings.I.InvertY, v => Settings.I.InvertY = v)));
        cols.AddChild(left);

        var right = UiKit.VBox(2);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var head = UiKit.HBox();
        var sec = Section("Touches");
        sec.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(sec);
        right.AddChild(head);
        foreach (var a in Settings.Actions)
        {
            var b = UiKit.Button("", null, null, 40);
            b.CustomMinimumSize = new Vector2(170, 40);
            var id = a.Id;
            b.Pressed += () =>
            {
                _waitingAction = id;
                _waitingButton = b;
                b.Text = "Appuyez sur une touche…";
            };
            _bindButtons[a.Id] = b;
            var row = Row(a.Label, b);
            row.CustomMinimumSize = new Vector2(0, 44);
            right.AddChild(row);
        }
        right.AddChild(UiKit.Spacer(false));
        var reset = UiKit.Button("Rétablir les touches par défaut", () => { Settings.I.ResetBindings(); RefreshBindings(); }, null, 40);
        reset.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        right.AddChild(reset);
        right.AddChild(UiKit.Label("Cliquez une touche puis appuyez sur la nouvelle. Échap annule.", 14, UiKit.Muted));
        cols.AddChild(right);
        Scroll(cols);
        RefreshBindings();
    }

    private void RefreshBindings()
    {
        foreach (var (id, b) in _bindButtons)
            if (IsInstanceValid(b)) b.Text = Settings.Describe(Settings.I.GetBinding(id));
    }

    private void BuildVideo()
    {
        var v = UiKit.VBox(4);
        v.CustomMinimumSize = new Vector2(760, 0);
        v.AddChild(Section("Affichage"));
        var q = new OptionButton();
        foreach (var n in new[] { "Bas", "Moyen", "Haut", "Ultra" }) q.AddItem(n);
        q.Selected = Settings.I.Quality;
        q.CustomMinimumSize = new Vector2(220, 44);
        q.ItemSelected += i => { Settings.I.Quality = (int)i; Settings.I.ApplyVideo(); };
        v.AddChild(Row("Qualité graphique", q, "Haut active l'éclairage global (SDFGI) et le brouillard volumétrique"));
        v.AddChild(Row("Plein écran", Toggle(Settings.I.Fullscreen, b => { Settings.I.Fullscreen = b; Settings.I.ApplyVideo(); })));
        v.AddChild(Row("Synchronisation verticale", Toggle(Settings.I.VSync, b => { Settings.I.VSync = b; Settings.I.ApplyVideo(); })));
        v.AddChild(Row("Champ de vision", Slider(60, 100, 1, Settings.I.Fov, f => { Settings.I.Fov = f; Settings.I.NotifyChanged(); }, f => $"{f:0}°")));
        v.AddChild(Row("Échelle de rendu", Slider(0.5f, 1f, 0.05f, Settings.I.RenderScale, f => { Settings.I.RenderScale = f; Settings.I.ApplyVideo(); }, f => $"{f * 100:0} %"), "En dessous de 100 %, mise à l'échelle FSR 2"));
        var fps = new OptionButton();
        int[] caps = { 0, 60, 120, 144, 240 };
        foreach (var c in caps) fps.AddItem(c == 0 ? "Illimité" : c.ToString());
        fps.Selected = Mathf.Max(0, Array.IndexOf(caps, Settings.I.MaxFps));
        fps.CustomMinimumSize = new Vector2(220, 44);
        fps.ItemSelected += i => { Settings.I.MaxFps = caps[i]; Settings.I.ApplyVideo(); };
        v.AddChild(Row("Limite d'images par seconde", fps));
        Scroll(v);
    }

    private void BuildAudio()
    {
        var v = UiKit.VBox(4);
        v.CustomMinimumSize = new Vector2(760, 0);
        v.AddChild(Section("Volumes"));
        v.AddChild(Row("Général", Slider(0, 1, 0.01f, Settings.I.MasterVolume, f => { Settings.I.MasterVolume = f; Settings.I.ApplyAudio(); }, f => $"{f * 100:0}")));
        v.AddChild(Row("Effets", Slider(0, 1, 0.01f, Settings.I.SfxVolume, f => { Settings.I.SfxVolume = f; Settings.I.ApplyAudio(); }, f => $"{f * 100:0}")));
        v.AddChild(Row("Musique", Slider(0, 1, 0.01f, Settings.I.MusicVolume, f => { Settings.I.MusicVolume = f; Settings.I.ApplyAudio(); }, f => $"{f * 100:0}")));
        Scroll(v);
    }

    private void BuildGame()
    {
        var v = UiKit.VBox(4);
        v.CustomMinimumSize = new Vector2(760, 0);
        v.AddChild(Section("Joueur"));
        var name = new LineEdit { Text = Settings.I.PlayerName, MaxLength = 20, CustomMinimumSize = new Vector2(320, 44) };
        name.TextChanged += t => Settings.I.PlayerName = string.IsNullOrWhiteSpace(t) ? "Soldat" : t.Trim();
        v.AddChild(Row("Nom du soldat", name, "Affiché aux autres joueurs en LAN"));
        v.AddChild(UiKit.Spacer(false));
        v.AddChild(Section("Profil"));
        var reset = UiKit.Button("Réinitialiser la progression", null, null, 44);
        var confirm = false;
        reset.Pressed += () =>
        {
            if (!confirm) { confirm = true; reset.Text = "Confirmer : tout effacer ?"; return; }
            Profile.I.ResetProfile();
            reset.Text = "Progression réinitialisée";
            reset.Disabled = true;
        };
        v.AddChild(Row("Armure, armes et crédits", reset, "Action irréversible"));
        Scroll(v);
    }
}
