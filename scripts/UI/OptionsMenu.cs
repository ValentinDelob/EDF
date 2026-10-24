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
    private Control _returnFocus;
    private const int ControlWidth = 360;

    public override void _Ready()
    {
        // Focus d'origine (menu principal, pause) rendu à la fermeture.
        _returnFocus = GetViewport().GuiGetFocusOwner();
        BuildFrame("Options", 0.96f);
        Body.Size = new Vector2(1160, 516);
        var row = UiKit.HBox(32);
        UiKit.FillRect(row);
        row.MouseFilter = MouseFilterEnum.Ignore;
        Body.AddChild(row);
        // Colonne de réglages (2/3) + description (1/3) : s'adapte de 1280 à 1920 px de large.
        var scroll = Scroll(720);
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsStretchRatio = 2f;
        _col = UiKit.VBox(2);
        _col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var colPad = UiKit.Margin(0, 0, 16, 0);
        colPad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        colPad.AddChild(_col);
        scroll.AddChild(colPad);
        row.AddChild(scroll);
        var desc = UiKit.VBox(12);
        desc.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        desc.CustomMinimumSize = new Vector2(280, 0);
        _descTitle = UiE.Title("", 28);
        _descText = UiE.Label("", 17, UiE.TextDim);
        _descText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.AddChild(_descTitle);
        desc.AddChild(_descText);
        var descPanel = UiE.Panel(3, 18, 14);
        descPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        descPanel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        descPanel.CustomMinimumSize = new Vector2(320, 220);
        descPanel.AddChild(desc);
        row.AddChild(descPanel);

        AddTab("Contrôles", BuildControls);
        AddTab("Vidéo", BuildVideo);
        AddTab("Audio", BuildAudio);
        AddTab("Jeu", BuildGame);
        FooterLeft.AddChild(UiE.KeyHint("Q/E:Onglets"));
        _resetHint = UiE.KeyHint("R:Rétablir", () => { Settings.I.ResetBindings(); RefreshBindings(); });
        FooterLeft.AddChild(_resetHint);
        FooterLeft.AddChild(UiE.KeyHint("Échap:Retour", Close));
        SelectTab(0);
    }

    private void Close()
    {
        Settings.I.Save();
        OnClose?.Invoke();
        // Si l'appelant n'a pas repris le focus, on le rend à l'élément qui l'avait avant l'ouverture.
        var f = GetViewport().GuiGetFocusOwner();
        if ((f == null || IsAncestorOf(f)) && _returnFocus != null && IsInstanceValid(_returnFocus) && !IsAncestorOf(_returnFocus))
            UiKit.FocusLater(_returnFocus);
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
        if (UiKit.HasLiveChildren(_col)) _col.AddChild(UiKit.Gap(20));
        _col.AddChild(UiE.Title(title.ToUpperInvariant(), 22, UiE.Mint, UiE.CondSemi));
        _col.AddChild(UiKit.Gap(4));
    }

    private void Row(string label, Control control, string description, int height = 52)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, height), MouseFilter = MouseFilterEnum.Pass };
        var normal = UiKit.Box(new Color(3 / 255f, 22 / 255f, 10 / 255f, 0.55f), default, 0, 0, 22, 6);
        // Même marge intérieure qu'au repos : la barre d'accent ne décale pas le contenu.
        var hot = UiKit.Bar(new Color(14 / 255f, 80 / 255f, 34 / 255f, 0.85f), UiE.Mint, 4, 22, 6);
        panel.AddThemeStyleboxOverride("panel", normal);
        var h = UiKit.HBox(24);
        h.MouseFilter = MouseFilterEnum.Pass;
        var l = UiE.Label(label, 18);
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        l.VerticalAlignment = VerticalAlignment.Center;
        h.AddChild(l);
        // Colonne de contrôles de largeur fixe : tous les réglages commencent et finissent au même x.
        control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        control.CustomMinimumSize = new Vector2(ControlWidth, control.CustomMinimumSize.Y);
        h.AddChild(control);
        panel.AddChild(h);
        void On()
        {
            panel.AddThemeStyleboxOverride("panel", hot);
            _descTitle.Text = label.ToUpperInvariant();
            _descText.Text = description ?? "";
        }
        void Off()
        {
            if (!IsInstanceValid(panel) || !panel.IsInsideTree()) return;
            var f = GetViewport()?.GuiGetFocusOwner();
            if (f != null && panel.IsAncestorOf(f)) return;
            if (panel.GetGlobalRect().HasPoint(panel.GetGlobalMousePosition())) return;
            panel.AddThemeStyleboxOverride("panel", normal);
        }
        panel.MouseEntered += On;
        panel.MouseExited += Off;
        // Surbrillance au focus clavier / manette, quel que soit le contrôle focalisable de la ligne.
        foreach (var f in Focusables(control))
        {
            f.FocusEntered += On;
            f.FocusExited += () => Callable.From(Off).CallDeferred();
        }
        _col.AddChild(panel);
    }

    private static IEnumerable<Control> Focusables(Control root)
    {
        if (root.FocusMode != FocusModeEnum.None) yield return root;
        foreach (var n in root.GetChildren())
            if (n is Control c)
                foreach (var f in Focusables(c)) yield return f;
    }

    private static Control Slider(float min, float max, float step, float value, Action<float> onChange, Func<float, string> fmt)
    {
        var h = UiKit.HBox(16);
        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(200, 24), FocusMode = FocusModeEnum.All, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var l = UiE.Label(fmt(value), 18, UiE.Text);
        l.CustomMinimumSize = new Vector2(64, 0);
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

    /// <summary>Sélecteur « ‹ valeur › » en un seul contrôle (← / → au clavier et à la manette).</summary>
    private static Control Selector(string[] values, int index, Action<int> onChange)
    {
        return new Stepper(values, index, onChange, 40);
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
                b.AddThemeStyleboxOverride("normal", UiKit.Box(UiE.Frame, UiE.Mint, 1, 0, 20, 8));
                b.AddThemeColorOverride("font_color", UiE.Ink);
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
        Clear("Jeu", "Identité du joueur, interface en mission et gestion du profil.");
        Section("Interface");
        Row("Échelle du HUD", Slider(0.8f, 1.2f, 0.05f, Settings.I.HudScale, f => Settings.I.HudScale = f, f => $"{f * 100:0} %"),
            "Taille des éléments affichés en mission (vie, armes, objectif, radar).");
        Row("Chiffres de dégâts", Selector(OnOff, Settings.I.DamageNumbers ? 1 : 0, i => Settings.I.DamageNumbers = i == 1),
            "Affiche les dégâts infligés au-dessus des ennemis touchés (en jaune sur un point faible).");
        Row("Réduire les animations", Selector(OnOff, Settings.I.ReduceMotion ? 1 : 0, i => Settings.I.ReduceMotion = i == 1),
            "Fige le fond animé des menus (grille, radar, circuits, particules) et les reflets des boutons.");
        Section("Accessibilité");
        Row("Caisses pour daltoniens", Selector(OnOff, Settings.I.ColorblindCrates ? 1 : 0, i => Settings.I.ColorblindCrates = i == 1),
            "Arme orange, armure bleu ciel, soin magenta (au lieu de rouge, vert et rose). Chaque type garde aussi sa forme.");
        Row("Secousses de caméra", Slider(0f, 1f, 0.05f, Settings.I.ShakeScale, f => Settings.I.ShakeScale = f, f => $"{f * 100:0} %"),
            "Intensité des tremblements de la caméra (explosions, coups reçus, saisie).");
        Row("Atténuer les flashs", Selector(OnOff, Settings.I.ReduceFlashes ? 1 : 0, i => Settings.I.ReduceFlashes = i == 1),
            "Réduit les éclairs lumineux des explosions et des tirs.");
        Section("Joueur");
        var name = new LineEdit { Text = Settings.I.PlayerName, MaxLength = 20, CustomMinimumSize = new Vector2(320, 42) };
        name.TextChanged += t => Settings.I.PlayerName = string.IsNullOrWhiteSpace(t) ? "Soldat" : t.Trim();
        Row("Nom du soldat", name, "Affiché aux autres joueurs dans le salon et au-dessus de votre soldat en mission.");
        Section("Profil");
        var reset = UiKit.Button("Réinitialiser", null, null, 40);
        var confirm = false;
        reset.Pressed += () =>
        {
            if (!confirm) { confirm = true; reset.Text = "Confirmer ?"; reset.AddThemeColorOverride("font_color", UiE.Danger); return; }
            Profile.I.ResetProfile();
            reset.Text = "Effacé";
            reset.Disabled = true;
        };
        Row("Réinitialiser la progression", reset, "Efface l'armure gagnée, les armes et les missions terminées. Cliquez deux fois pour confirmer. Action irréversible.");
    }
}
