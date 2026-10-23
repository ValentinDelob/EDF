using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>Options : onglets, colonne unique de réglages, panneau de description à droite.</summary>
public partial class OptionsMenu : MenuScreen
{
    public Action OnClose;
    private VBoxContainer _col;
    private Label _descTitle, _descText;
    private string _waitingAction;
    private readonly Dictionary<string, Button> _bindButtons = new();
    private Control _resetHint;

    public override void _Ready()
    {
        BuildFrame("Options", 0.96f);
        var row = UiKit.HBox(64);
        UiKit.FillRect(row);
        row.MouseFilter = MouseFilterEnum.Ignore;
        Body.AddChild(row);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(880, 0) };
        _col = UiKit.VBox(2);
        _col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _col.CustomMinimumSize = new Vector2(860, 0);
        scroll.AddChild(_col);
        row.AddChild(scroll);
        var desc = UiKit.VBox(12);
        desc.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _descTitle = UiKit.Label("", 28, UiKit.Text, true);
        _descText = UiKit.Label("", 16, new Color(UiKit.Text, 0.78f));
        _descText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.AddChild(_descTitle);
        desc.AddChild(_descText);
        row.AddChild(desc);

        AddTab("Contrôles", BuildControls);
        AddTab("Vidéo", BuildVideo);
        AddTab("Audio", BuildAudio);
        AddTab("Jeu", BuildGame);
        FooterLeft.AddChild(UiKit.KeyHint("Échap", "Retour", Close));
        FooterLeft.AddChild(UiKit.KeyHint("Q / E", "Onglets"));
        _resetHint = UiKit.KeyHint("R", "Rétablir les touches", () => { Settings.I.ResetBindings(); RefreshBindings(); });
        FooterLeft.AddChild(_resetHint);
        SelectTab(0);
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
        if (_waitingAction == null) return;
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

    public override void _UnhandledInput(InputEvent e)
    {
        if (_waitingAction != null) return;
        if (e.IsActionPressed("pause") || e.IsActionPressed("ui_cancel")) { Close(); GetViewport().SetInputAsHandled(); return; }
        if (HandleTabKeys(e)) { GetViewport().SetInputAsHandled(); return; }
        if (e is InputEventKey { Pressed: true, PhysicalKeycode: Key.R } && CurrentTab() == 0 && GetViewport().GuiGetFocusOwner() is not LineEdit)
        {
            Settings.I.ResetBindings();
            RefreshBindings();
            GetViewport().SetInputAsHandled();
        }
    }

    private void EndWait()
    {
        _waitingAction = null;
        RefreshBindings();
    }

    private void Clear(string title, string text)
    {
        foreach (var c in _col.GetChildren()) c.QueueFree();
        _bindButtons.Clear();
        _descTitle.Text = title.ToUpperInvariant();
        _descText.Text = text;
        if (_resetHint != null) _resetHint.Visible = CurrentTab() == 0;
    }

    private void Section(string title)
    {
        if (_col.GetChildCount() > 0) _col.AddChild(UiKit.Gap(20));
        _col.AddChild(UiKit.Eyebrow(title));
        _col.AddChild(UiKit.Gap(4));
    }

    private void Row(string label, Control control, string description, int height = 52)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, height), MouseFilter = MouseFilterEnum.Pass };
        var normal = UiKit.Box(new Color(0, 0, 0, 0), default, 0, 0, 22, 6);
        var hot = UiKit.Bar(UiKit.Panel, UiKit.Accent, 4, 18, 6);
        panel.AddThemeStyleboxOverride("panel", normal);
        var h = UiKit.HBox(24);
        h.MouseFilter = MouseFilterEnum.Pass;
        var l = UiKit.Label(label, 17);
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        l.VerticalAlignment = VerticalAlignment.Center;
        h.AddChild(l);
        control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        h.AddChild(control);
        panel.AddChild(h);
        void On()
        {
            panel.AddThemeStyleboxOverride("panel", hot);
            _descTitle.Text = label.ToUpperInvariant();
            _descText.Text = description ?? "";
        }
        void Off() => panel.AddThemeStyleboxOverride("panel", normal);
        panel.MouseEntered += On;
        panel.MouseExited += () => { if (!panel.GetGlobalRect().HasPoint(panel.GetGlobalMousePosition())) Off(); };
        control.FocusEntered += On;
        control.FocusExited += Off;
        _col.AddChild(panel);
    }

    private static Control Slider(float min, float max, float step, float value, Action<float> onChange, Func<float, string> fmt)
    {
        var h = UiKit.HBox(16);
        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(280, 24), FocusMode = FocusModeEnum.All };
        var l = UiKit.Label(fmt(value), 17, UiKit.Text);
        l.AddThemeFontOverride("font", UiKit.BodyMedium);
        l.CustomMinimumSize = new Vector2(64, 0);
        l.HorizontalAlignment = HorizontalAlignment.Right;
        s.ValueChanged += v =>
        {
            l.Text = fmt((float)v);
            onChange((float)v);
        };
        h.AddChild(s);
        h.AddChild(l);
        h.FocusEntered += () => s.GrabFocus();
        return h;
    }

    /// <summary>Sélecteur « ‹ valeur › » compact (pattern manette).</summary>
    private static Control Selector(string[] values, int index, Action<int> onChange)
    {
        var h = UiKit.HBox(0);
        var label = UiKit.Label(values[index], 17, UiKit.Text);
        label.AddThemeFontOverride("font", UiKit.BodyMedium);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.CustomMinimumSize = new Vector2(200, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        var i = index;
        Button prev = null, next = null;
        void Set(int ni)
        {
            i = Mathf.Clamp(ni, 0, values.Length - 1);
            label.Text = values[i];
            onChange(i);
        }
        prev = UiKit.Button("‹", () => Set(i - 1), null, 40);
        next = UiKit.Button("›", () => Set(i + 1), null, 40);
        prev.CustomMinimumSize = next.CustomMinimumSize = new Vector2(44, 40);
        h.AddChild(prev);
        h.AddChild(label);
        h.AddChild(next);
        return h;
    }

    private static readonly string[] OnOff = { "Non", "Oui" };

    private void BuildControls()
    {
        Clear("Contrôles", "Survolez un réglage pour afficher son explication. Les changements s'appliquent immédiatement.");
        Section("Souris");
        Row("Sensibilité", Slider(0.1f, 10f, 0.05f, Settings.I.MouseSensitivity, v => Settings.I.MouseSensitivity = v, v => v.ToString("0.00")),
            "Vitesse de rotation de la caméra à la souris. 1,00 correspond à la vitesse par défaut.");
        Row("Sensibilité en visée", Slider(0.1f, 2f, 0.05f, Settings.I.AimSensitivity, v => Settings.I.AimSensitivity = v, v => v.ToString("0.00")),
            "Multiplicateur appliqué quand vous visez (clic droit), pour des tirs plus précis à longue distance.");
        Row("Inverser l'axe vertical", Selector(OnOff, Settings.I.InvertY ? 1 : 0, i => Settings.I.InvertY = i == 1),
            "Pousser la souris vers l'avant fait regarder vers le bas.");
        Section("Touches");
        foreach (var a in Settings.Actions)
        {
            var b = UiKit.Button("", null, null, 38);
            b.CustomMinimumSize = new Vector2(180, 38);
            var id = a.Id;
            b.Pressed += () =>
            {
                _waitingAction = id;
                b.Text = "Appuyez…";
                b.AddThemeStyleboxOverride("normal", UiKit.Box(UiKit.Accent, default, 0, 0, 20, 8));
                b.AddThemeColorOverride("font_color", UiKit.OnAccent);
            };
            _bindButtons[a.Id] = b;
            Row(a.Label, b, "Cliquez puis appuyez sur la nouvelle touche ou le bouton de souris. Échap annule. Une touche déjà utilisée est retirée de l'autre action.", 46);
        }
        RefreshBindings();
    }

    private void RefreshBindings()
    {
        foreach (var (id, b) in _bindButtons)
        {
            if (!IsInstanceValid(b)) continue;
            b.Text = Settings.Describe(Settings.I.GetBinding(id));
            b.RemoveThemeStyleboxOverride("normal");
            b.RemoveThemeColorOverride("font_color");
        }
    }

    private void BuildVideo()
    {
        Clear("Vidéo", "Réglages graphiques. « Haut » active l'éclairage global (SDFGI) et le brouillard volumétrique.");
        Section("Qualité");
        Row("Préréglage", Selector(new[] { "Bas", "Moyen", "Haut", "Ultra" }, Settings.I.Quality, i => { Settings.I.Quality = i; Settings.I.ApplyVideo(); }),
            "Bas : ombres courtes, sans occlusion ambiante. Moyen : SSAO. Haut : éclairage global et brouillard volumétrique. Ultra : réflexions et ombres longues.");
        Row("Échelle de rendu", Slider(0.5f, 1f, 0.05f, Settings.I.RenderScale, f => { Settings.I.RenderScale = f; Settings.I.ApplyVideo(); }, f => $"{f * 100:0} %"),
            "En dessous de 100 %, l'image est calculée en plus petit puis agrandie avec FSR 2 : gain de performances.");
        Row("Champ de vision", Slider(60, 100, 1, Settings.I.Fov, f => { Settings.I.Fov = f; Settings.I.NotifyChanged(); }, f => $"{f:0}°"),
            "Angle de vue horizontal de la caméra.");
        Section("Affichage");
        Row("Plein écran", Selector(OnOff, Settings.I.Fullscreen ? 1 : 0, i => { Settings.I.Fullscreen = i == 1; Settings.I.ApplyVideo(); }), "Bascule entre fenêtre et plein écran.");
        Row("Synchronisation verticale", Selector(OnOff, Settings.I.VSync ? 1 : 0, i => { Settings.I.VSync = i == 1; Settings.I.ApplyVideo(); }), "Évite les déchirures d'image au prix d'un peu de latence.");
        int[] caps = { 0, 60, 120, 144, 240 };
        Row("Limite d'images par seconde", Selector(new[] { "Illimitée", "60", "120", "144", "240" }, Mathf.Max(0, Array.IndexOf(caps, Settings.I.MaxFps)), i => { Settings.I.MaxFps = caps[i]; Settings.I.ApplyVideo(); }),
            "Plafonne la fréquence d'images pour réduire la chaleur et la consommation.");
    }

    private void BuildAudio()
    {
        Clear("Audio", "Volumes des différentes sources sonores.");
        Section("Volumes");
        Row("Général", Slider(0, 1, 0.01f, Settings.I.MasterVolume, f => { Settings.I.MasterVolume = f; Settings.I.ApplyAudio(); }, f => $"{f * 100:0}"), "Volume global du jeu.");
        Row("Effets", Slider(0, 1, 0.01f, Settings.I.SfxVolume, f => { Settings.I.SfxVolume = f; Settings.I.ApplyAudio(); }, f => $"{f * 100:0}"), "Tirs, explosions, créatures, interface.");
        Row("Musique", Slider(0, 1, 0.01f, Settings.I.MusicVolume, f => { Settings.I.MusicVolume = f; Settings.I.ApplyAudio(); }, f => $"{f * 100:0}"), "Ambiance musicale.");
    }

    private void BuildGame()
    {
        Clear("Jeu", "Identité du joueur et gestion du profil.");
        Section("Joueur");
        var name = new LineEdit { Text = Settings.I.PlayerName, MaxLength = 20, CustomMinimumSize = new Vector2(320, 42) };
        name.TextChanged += t => Settings.I.PlayerName = string.IsNullOrWhiteSpace(t) ? "Soldat" : t.Trim();
        Row("Nom du soldat", name, "Affiché aux autres joueurs dans le salon et au-dessus de votre soldat en mission.");
        Section("Profil");
        var reset = UiKit.Button("Réinitialiser", null, null, 40);
        var confirm = false;
        reset.Pressed += () =>
        {
            if (!confirm) { confirm = true; reset.Text = "Confirmer ?"; reset.AddThemeColorOverride("font_color", UiKit.Danger); return; }
            Profile.I.ResetProfile();
            reset.Text = "Effacé";
            reset.Disabled = true;
        };
        Row("Réinitialiser la progression", reset, "Efface l'armure gagnée, les armes et les crédits. Cliquez deux fois pour confirmer. Action irréversible.");
    }
}
