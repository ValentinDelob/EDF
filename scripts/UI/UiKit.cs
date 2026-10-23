using System;
using Godot;

namespace Rempart;

/// <summary>
/// Jetons de design et fabriques de contrôles (cf. docs/ui-knowledge-base.md) :
/// aplats translucides sans bordure, un seul accent orange, focus = barre d'accent à gauche.
/// </summary>
public static class UiKit
{
    // Palette
    public static readonly Color Bg = new("0e1114");
    public static readonly Color Panel = new(0.082f, 0.102f, 0.122f, 0.72f);   // #151A1F à 72 %
    public static readonly Color PanelSolid = new("151a1f");
    public static readonly Color Line = new("2a323a");
    public static readonly Color Text = new("f2f4f5");
    public static readonly Color Muted = new("9aa4ad");
    public static readonly Color Dim = new("5c6670");
    public static readonly Color Accent = new("ff8a1f");
    public static readonly Color AccentPress = new("e06f00");
    public static readonly Color OnAccent = new("0e1114");
    public static readonly Color Info = new("3fb6e8");
    public static readonly Color Danger = new("e8413c");
    public static readonly Color Good = new("5bd16e");
    public static readonly Color Gold = new("ffc233");
    public static readonly Color Surface = Panel;

    // Marges d'écran (1600×900)
    public const int ScreenX = 80;
    public const int ScreenTop = 48;
    public const int ScreenBottom = 40;

    private static Theme _theme;
    private static Font _body, _bodyMedium, _head, _headSemi;

    private static Font LoadFont(string path, Font fallback)
    {
        var f = ResourceLoader.Exists(path) ? ResourceLoader.Load<FontFile>(path) : null;
        return f ?? fallback;
    }

    public static Font BodyFont => _body ??= LoadFont("res://assets/fonts/Inter.ttf", new SystemFont { FontNames = new[] { "Segoe UI", "Arial" } });

    public static Font BodyMedium => _bodyMedium ??= new FontVariation
    {
        BaseFont = BodyFont,
        VariationOpentype = new Godot.Collections.Dictionary { { TextServerManager.GetPrimaryInterface().NameToTag("wght"), 520 } },
    };

    public static Font HeadFont => _head ??= LoadFont("res://assets/fonts/BarlowCondensed-Bold.ttf", new SystemFont { FontNames = new[] { "Bahnschrift", "Arial" }, FontWeight = 700 });
    public static Font HeadSemi => _headSemi ??= LoadFont("res://assets/fonts/BarlowCondensed-SemiBold.ttf", HeadFont);

    public static StyleBoxFlat Box(Color bg, Color border = default, int borderW = 0, int radius = 0, int padH = 16, int padV = 10)
    {
        var s = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            ContentMarginLeft = padH, ContentMarginRight = padH, ContentMarginTop = padV, ContentMarginBottom = padV,
        };
        s.SetCornerRadiusAll(radius);
        s.SetBorderWidthAll(borderW);
        return s;
    }

    /// <summary>Fond + barre verticale d'accent à gauche (état focus / sélection).</summary>
    public static StyleBoxFlat Bar(Color bg, Color bar, int barW = 4, int padH = 16, int padV = 10)
    {
        var s = Box(bg, bar, 0, 0, padH, padV);
        s.BorderWidthLeft = barW;
        return s;
    }

    public static StyleBoxFlat Underline(Color bg, Color line, int w = 3, int padH = 0, int padV = 8)
    {
        var s = Box(bg, line, 0, 0, padH, padV);
        s.BorderWidthBottom = w;
        return s;
    }

    private static readonly StyleBoxEmpty Empty = new();

    public static Theme Theme
    {
        get
        {
            if (_theme != null) return _theme;
            var t = new Theme { DefaultFont = BodyFont, DefaultFontSize = 17 };
            var hover = new Color(1, 1, 1, 0.06f);

            // Bouton secondaire : aplat translucide, focus = barre d'accent
            t.SetStylebox("normal", "Button", Box(Panel, default, 0, 0, 20, 10));
            t.SetStylebox("hover", "Button", Bar(new Color(0.12f, 0.145f, 0.17f, 0.85f), Accent, 4, 20, 10));
            t.SetStylebox("pressed", "Button", Bar(new Color(0.14f, 0.165f, 0.19f, 0.9f), AccentPress, 4, 20, 10));
            t.SetStylebox("focus", "Button", Bar(new Color(0, 0, 0, 0), Accent, 4, 20, 10));
            t.SetStylebox("disabled", "Button", Box(new Color(Panel, 0.4f), default, 0, 0, 20, 10));
            t.SetColor("font_color", "Button", Text);
            t.SetColor("font_hover_color", "Button", Text);
            t.SetColor("font_pressed_color", "Button", Text);
            t.SetColor("font_focus_color", "Button", Text);
            t.SetColor("font_hover_pressed_color", "Button", Text);
            t.SetColor("font_disabled_color", "Button", Dim);
            t.SetFont("font", "Button", BodyMedium);

            // Action principale (bas-droite)
            t.SetTypeVariation("Primary", "Button");
            t.SetStylebox("normal", "Primary", Box(Accent, default, 0, 0, 44, 12));
            t.SetStylebox("hover", "Primary", Box(Accent.Lightened(0.12f), default, 0, 0, 44, 12));
            t.SetStylebox("pressed", "Primary", Box(AccentPress, default, 0, 0, 44, 12));
            t.SetStylebox("focus", "Primary", Box(new Color(0, 0, 0, 0), Text, 2, 0, 44, 12));
            // Désactivé : aplat neutre et texte atténué (l'accent reste réservé à ce qui est actionnable).
            t.SetStylebox("disabled", "Primary", Box(Panel, default, 0, 0, 44, 12));
            foreach (var k in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
                t.SetColor(k, "Primary", OnAccent);
            t.SetColor("font_disabled_color", "Primary", Muted);
            t.SetFont("font", "Primary", HeadFont);
            t.SetFontSize("font_size", "Primary", 26);

            // Entrée de menu texte (menu principal, pause)
            t.SetTypeVariation("MenuItem", "Button");
            t.SetStylebox("normal", "MenuItem", Box(new Color(0, 0, 0, 0), default, 0, 0, 20, 4));
            t.SetStylebox("hover", "MenuItem", Bar(new Color(0, 0, 0, 0), Accent, 4, 16, 4));
            t.SetStylebox("focus", "MenuItem", Bar(new Color(0, 0, 0, 0), Accent, 4, 16, 4));
            t.SetStylebox("pressed", "MenuItem", Bar(new Color(0, 0, 0, 0), AccentPress, 4, 16, 4));
            t.SetColor("font_color", "MenuItem", Muted);
            t.SetColor("font_hover_color", "MenuItem", Text);
            t.SetColor("font_focus_color", "MenuItem", Text);
            t.SetColor("font_pressed_color", "MenuItem", Text);
            t.SetColor("font_hover_pressed_color", "MenuItem", Text);
            t.SetFont("font", "MenuItem", HeadSemi);
            t.SetFontSize("font_size", "MenuItem", 36);

            // Onglet (souligné accent quand actif)
            t.SetTypeVariation("Tab", "Button");
            t.SetStylebox("normal", "Tab", Underline(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), 3, 2, 6));
            t.SetStylebox("hover", "Tab", Underline(new Color(0, 0, 0, 0), Line, 3, 2, 6));
            t.SetStylebox("pressed", "Tab", Underline(new Color(0, 0, 0, 0), Accent, 3, 2, 6));
            t.SetStylebox("focus", "Tab", Underline(new Color(0, 0, 0, 0), Muted, 3, 2, 6));
            t.SetColor("font_color", "Tab", Muted);
            t.SetColor("font_hover_color", "Tab", Text);
            t.SetColor("font_pressed_color", "Tab", Text);
            t.SetColor("font_hover_pressed_color", "Tab", Text);
            t.SetColor("font_focus_color", "Tab", Text);
            t.SetFont("font", "Tab", HeadSemi);
            t.SetFontSize("font_size", "Tab", 21);

            // Ligne de liste sélectionnable (missions, armes, parties LAN)
            t.SetTypeVariation("Row", "Button");
            t.SetStylebox("normal", "Row", Box(new Color(0, 0, 0, 0), default, 0, 0, 22, 12));
            t.SetStylebox("hover", "Row", Box(hover, default, 0, 0, 22, 12));
            t.SetStylebox("pressed", "Row", Bar(Panel, Accent, 4, 18, 12));
            t.SetStylebox("hover_pressed", "Row", Bar(new Color(0.12f, 0.145f, 0.17f, 0.85f), Accent, 4, 18, 12));
            t.SetStylebox("focus", "Row", Bar(new Color(0, 0, 0, 0), Muted, 2, 20, 12));
            t.SetStylebox("disabled", "Row", Box(new Color(0, 0, 0, 0), default, 0, 0, 22, 12));
            // Tuile (classes, emplacements) : comme une ligne, avec un léger aplat pour se lire comme un bouton.
            t.SetTypeVariation("Card", "Row");
            t.SetStylebox("normal", "Card", Box(new Color(1, 1, 1, 0.04f), default, 0, 0, 22, 12));
            t.SetStylebox("hover", "Card", Box(new Color(1, 1, 1, 0.08f), default, 0, 0, 22, 12));
            t.SetStylebox("disabled", "Card", Box(new Color(1, 1, 1, 0.02f), default, 0, 0, 22, 12));

            // Panneaux
            t.SetStylebox("panel", "PanelContainer", Box(Panel, default, 0, 0, 22, 18));
            t.SetTypeVariation("Glass", "PanelContainer");
            t.SetStylebox("panel", "Glass", Box(new Color(0.055f, 0.067f, 0.078f, 0.55f), default, 0, 0, 16, 12));
            t.SetTypeVariation("Clear", "PanelContainer");
            t.SetStylebox("panel", "Clear", Empty);

            // Libellés
            t.SetColor("font_color", "Label", Text);
            t.SetColor("font_shadow_color", "Label", new Color(0, 0, 0, 0));

            // Champs
            t.SetStylebox("normal", "LineEdit", Underline(Panel, Line, 2, 14, 10));
            t.SetStylebox("focus", "LineEdit", Underline(Panel, Accent, 2, 14, 10));
            t.SetColor("caret_color", "LineEdit", Accent);
            t.SetColor("font_color", "LineEdit", Text);
            t.SetColor("selection_color", "LineEdit", new Color(Accent, 0.35f));

            // Curseurs
            t.SetStylebox("slider", "HSlider", Box(Line, default, 0, 0, 0, 2));
            t.SetStylebox("grabber_area", "HSlider", Box(Accent, default, 0, 0, 0, 2));
            t.SetStylebox("grabber_area_highlight", "HSlider", Box(Accent.Lightened(0.15f), default, 0, 0, 0, 2));
            t.SetIcon("grabber", "HSlider", Dot(16, Text));
            t.SetIcon("grabber_highlight", "HSlider", Dot(18, Accent));
            t.SetConstant("center_grabber", "HSlider", 1);

            // Barres de progression
            t.SetStylebox("background", "ProgressBar", Box(new Color(0, 0, 0, 0.4f), default, 0, 0, 0, 0));
            t.SetStylebox("fill", "ProgressBar", Box(Text, default, 0, 0, 0, 0));

            // Défilement
            t.SetStylebox("scroll", "VScrollBar", Box(new Color(1, 1, 1, 0.02f), default, 0, 0, 2, 2));
            t.SetStylebox("grabber", "VScrollBar", Box(new Color(1, 1, 1, 0.16f), default, 0, 0, 2, 2));
            t.SetStylebox("grabber_highlight", "VScrollBar", Box(new Color(1, 1, 1, 0.3f), default, 0, 0, 2, 2));
            t.SetStylebox("grabber_pressed", "VScrollBar", Box(Accent, default, 0, 0, 2, 2));

            // Cases à cocher / bascules
            t.SetColor("font_color", "CheckButton", Text);

            // Listes déroulantes
            t.SetStylebox("normal", "OptionButton", Box(Panel, default, 0, 0, 16, 10));
            t.SetStylebox("hover", "OptionButton", Bar(new Color(0.12f, 0.145f, 0.17f, 0.85f), Accent, 4, 16, 10));
            t.SetStylebox("focus", "OptionButton", Bar(new Color(0, 0, 0, 0), Accent, 4, 16, 10));
            t.SetStylebox("pressed", "OptionButton", Bar(Panel, Accent, 4, 16, 10));
            t.SetStylebox("panel", "PopupMenu", Box(PanelSolid, default, 0, 0, 6, 6));
            t.SetStylebox("hover", "PopupMenu", Bar(new Color(1, 1, 1, 0.06f), Accent, 3, 8, 6));
            t.SetColor("font_hover_color", "PopupMenu", Text);

            // Info-bulles
            t.SetStylebox("panel", "TooltipPanel", Box(PanelSolid, default, 0, 0, 12, 8));
            _theme = t;
            return t;
        }
    }

    private static ImageTexture Dot(int size, Color c)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var r = size / 2f;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var d = new Vector2(x + 0.5f - r, y + 0.5f - r).Length();
            img.SetPixel(x, y, new Color(c, Mathf.Clamp(r - d, 0, 1)));
        }
        return ImageTexture.CreateFromImage(img);
    }

    // ------------------------------------------------------------ Fabriques

    public static Label Label(string text, int size = 17, Color? color = null, bool head = false)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        if (color.HasValue) l.AddThemeColorOverride("font_color", color.Value);
        if (head) l.AddThemeFontOverride("font", HeadFont);
        return l;
    }

    /// <summary>Texte de HUD : blanc avec ombre portée pour rester lisible sur une nuée ou une explosion.</summary>
    public static Label HudLabel(string text, int size, Color? color = null, bool head = false)
    {
        var l = Label(text, size, color, head);
        l.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.75f));
        l.AddThemeConstantOverride("shadow_offset_x", 1);
        l.AddThemeConstantOverride("shadow_offset_y", 1);
        l.AddThemeConstantOverride("shadow_outline_size", 3);
        return l;
    }

    public static Label Heading(string text, int size = 48) => Label(text.ToUpperInvariant(), size, Text, true);

    public static Label Eyebrow(string text, Color? color = null)
    {
        var l = Label(text.ToUpperInvariant(), 13, color ?? Muted);
        l.AddThemeFontOverride("font", BodyMedium);
        return l;
    }

    public static Button Button(string text, Action onClick, string variation = null, int minHeight = 48)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, minHeight), FocusMode = Control.FocusModeEnum.All };
        if (variation != null) b.ThemeTypeVariation = variation;
        if (variation == "Primary") b.Text = text.ToUpperInvariant();
        if (variation == "Tab") b.Text = text.ToUpperInvariant();
        if (variation == "MenuItem") { b.Text = text.ToUpperInvariant(); b.Alignment = HorizontalAlignment.Left; }
        b.Pressed += () =>
        {
            Sfx.Ui("ui_click", -6f);
            onClick?.Invoke();
        };
        b.MouseEntered += () =>
        {
            if (b.Disabled) return;
            Sfx.Ui("ui_hover", -12f, 1f, 60);
            // Liste de menu : le survol déplace le focus, pour qu'une seule entrée soit en surbrillance.
            if (variation == "MenuItem" && !b.HasFocus()) FocusSilently(b);
        };
        b.FocusEntered += () => { if (!_silentFocus) Sfx.Ui("ui_hover", -12f, 1f, 60); };
        return b;
    }

    private static bool _silentFocus;

    /// <summary>Donne le focus sans jouer le son de survol (ouverture d'écran, reconstruction de liste).</summary>
    public static void FocusSilently(Control c)
    {
        if (c == null || !GodotObject.IsInstanceValid(c) || !c.IsInsideTree() || !c.IsVisibleInTree()) return;
        if (c is BaseButton { Disabled: true }) return;
        _silentFocus = true;
        c.GrabFocus();
        _silentFocus = false;
    }

    /// <summary>Focus différé (après la mise en page) et silencieux.</summary>
    public static void FocusLater(Control c)
    {
        if (c == null) return;
        Callable.From(() => FocusSilently(c)).CallDeferred();
    }

    /// <summary>Premier descendant focalisable (ordre de l'arbre), ou null.</summary>
    public static Control FirstFocusable(Node root)
    {
        foreach (var n in root.GetChildren())
        {
            if (n is Control { Visible: false }) continue;
            if (n is Control c && c.FocusMode == Control.FocusModeEnum.All && !(c is BaseButton { Disabled: true }) && !c.IsQueuedForDeletion())
                return c;
            if (n is Node child && !child.IsQueuedForDeletion() && FirstFocusable(child) is { } f) return f;
        }
        return null;
    }

    /// <summary>Chaîne verticale de focus qui boucle (haut du premier → dernier, bas du dernier → premier).</summary>
    public static void ChainVertical(params Control[] items)
    {
        var list = Array.FindAll(items, c => c != null);
        for (var i = 0; i < list.Length; i++)
        {
            var prev = list[(i + list.Length - 1) % list.Length];
            var next = list[(i + 1) % list.Length];
            list[i].FocusNeighborTop = list[i].GetPathTo(prev);
            list[i].FocusNeighborBottom = list[i].GetPathTo(next);
        }
    }

    /// <summary>Apparition en fondu (≤ 200 ms) d'un écran ou d'un panneau.</summary>
    public static void FadeIn(CanvasItem c, float seconds = 0.15f)
    {
        c.Modulate = new Color(1, 1, 1, 0);
        var tw = c.CreateTween();
        tw.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(c, "modulate:a", 1f, seconds);
    }

    /// <summary>Vrai si le nœud a des enfants non marqués pour suppression (les QueueFree restent jusqu'à la fin de l'image).</summary>
    public static bool HasLiveChildren(Node n)
    {
        foreach (var c in n.GetChildren())
            if (!c.IsQueuedForDeletion()) return true;
        return false;
    }

    /// <summary>Espace horizontal fixe (Gap fixe les deux dimensions).</summary>
    public static Control HGap(int px) => new() { CustomMinimumSize = new Vector2(px, 0), MouseFilter = Control.MouseFilterEnum.Ignore };

    public static VBoxContainer VBox(int sep = 12) { var v = new VBoxContainer(); v.AddThemeConstantOverride("separation", sep); return v; }
    public static HBoxContainer HBox(int sep = 12) { var h = new HBoxContainer(); h.AddThemeConstantOverride("separation", sep); return h; }

    public static MarginContainer Margin(int l, int t, int r, int b)
    {
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", l);
        m.AddThemeConstantOverride("margin_top", t);
        m.AddThemeConstantOverride("margin_right", r);
        m.AddThemeConstantOverride("margin_bottom", b);
        return m;
    }

    public static Control Spacer(bool expandV = true, bool expandH = false) => new()
    {
        SizeFlagsVertical = expandV ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
        SizeFlagsHorizontal = expandH ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    public static Control Gap(int px) => new() { CustomMinimumSize = new Vector2(px, px), MouseFilter = Control.MouseFilterEnum.Ignore };

    public static ColorRect Rule(Color? c = null) => new() { Color = c ?? Line, CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };

    public static PanelContainer Panel_(string variation = null)
    {
        var p = new PanelContainer();
        if (variation != null) p.ThemeTypeVariation = variation;
        return p;
    }

    public static string Stars(int n) => new string('★', Mathf.Clamp(n, 0, 5)) + new string('☆', 5 - Mathf.Clamp(n, 0, 5));

    public static string Time(float seconds) => $"{(int)seconds / 60:00}:{(int)seconds % 60:00}";

    /// <summary>Voile plein écran (assombrit la scène 3D sans la masquer).</summary>
    public static ColorRect Scrim(float alpha = 0.55f) => new()
    {
        Color = new Color(Bg, alpha),
        AnchorRight = 1, AnchorBottom = 1,
        MouseFilter = Control.MouseFilterEnum.Stop,
    };

    /// <summary>Dégradé horizontal sombre depuis le bord gauche (lisibilité des menus sur le décor).</summary>
    public static TextureRect SideShade(float width, float strength = 0.92f, bool fromRight = false)
    {
        var tex = new GradientTexture2D
        {
            Gradient = new Gradient { Colors = new[] { new Color(Bg, strength), new Color(Bg, strength * 0.6f), new Color(Bg, 0f) }, Offsets = new[] { 0f, 0.55f, 1f } },
            FillFrom = fromRight ? new Vector2(1, 0) : new Vector2(0, 0),
            FillTo = fromRight ? new Vector2(0, 0) : new Vector2(1, 0),
            Width = 256, Height = 4,
        };
        var r = new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorBottom = 1,
        };
        if (fromRight) { r.AnchorLeft = 1; r.AnchorRight = 1; r.OffsetLeft = -width; }
        else r.OffsetRight = width;
        return r;
    }

    public static void FillRect(Control c)
    {
        c.AnchorLeft = 0; c.AnchorTop = 0; c.AnchorRight = 1; c.AnchorBottom = 1;
        c.OffsetLeft = c.OffsetTop = c.OffsetRight = c.OffsetBottom = 0;
    }

    /// <summary>Indication de touche (« Échap  Retour ») pour la barre d'aide.</summary>
    public static Control KeyHint(string key, string action, Action onClick = null)
    {
        var b = new Button { Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        b.AddThemeStyleboxOverride("normal", Empty);
        b.AddThemeStyleboxOverride("hover", Empty);
        b.AddThemeStyleboxOverride("pressed", Empty);
        var h = HBox(8);
        h.MouseFilter = Control.MouseFilterEnum.Ignore;
        var k = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        k.AddThemeStyleboxOverride("panel", Box(new Color(1, 1, 1, 0.1f), default, 0, 2, 6, 1));
        var kl = Label(key, 13, Text);
        kl.AddThemeFontOverride("font", BodyMedium);
        k.AddChild(kl);
        h.AddChild(k);
        var a = Label(action, 14, Muted);
        a.MouseFilter = Control.MouseFilterEnum.Ignore;
        h.AddChild(a);
        b.AddChild(h);
        b.CustomMinimumSize = new Vector2(0, 28);
        h.Resized += () => b.CustomMinimumSize = new Vector2(h.Size.X, 28);
        if (onClick != null) b.Pressed += onClick;
        return b;
    }

    public static string KeyName(string action) => Settings.Describe(Settings.I.GetBinding(action));

    /// <summary>Indication de touche pour le HUD (touche encadrée + libellé ombré).</summary>
    public static Control HudKey(string key, string action)
    {
        var h = HBox(8);
        h.MouseFilter = Control.MouseFilterEnum.Ignore;
        var k = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        k.AddThemeStyleboxOverride("panel", Box(new Color(Bg, 0.6f), default, 0, 2, 6, 1));
        var kl = Label(key, 13, Text);
        kl.AddThemeFontOverride("font", BodyMedium);
        k.AddChild(kl);
        h.AddChild(k);
        var a = HudLabel(action, 15, new Color(Text, 0.9f));
        a.VerticalAlignment = VerticalAlignment.Center;
        h.AddChild(a);
        return h;
    }
}

/// <summary>
/// Sélecteur « ‹ valeur › » en un seul contrôle focalisable : ← / → (ou croix directionnelle) changent la valeur,
/// Entrée passe à la suivante, clic sur un chevron = précédente / suivante.
/// </summary>
public partial class Stepper : Button
{
    private readonly string[] _values;
    private readonly Action<int> _onChange;
    private readonly Label _prev, _next;
    private bool _wrap = true;
    public bool Wrap { get => _wrap; set { _wrap = value; if (_values != null) Refresh(); } }
    public int Index { get; private set; }

    public Stepper() { }

    public Stepper(string[] values, int index, Action<int> onChange, int height = 44)
    {
        _values = values;
        _onChange = onChange;
        Index = Mathf.Clamp(index, 0, values.Length - 1);
        CustomMinimumSize = new Vector2(0, height);
        FocusMode = FocusModeEnum.All;
        ClipText = true;
        _prev = Chevron("‹", HorizontalAlignment.Left);
        _next = Chevron("›", HorizontalAlignment.Right);
        Refresh();
        MouseEntered += () => { if (!Disabled) Sfx.Ui("ui_hover", -12f, 1f, 60); };
        Pressed += () => Step(+1, true);
    }

    private Label Chevron(string t, HorizontalAlignment a)
    {
        var l = UiKit.Label(t, 22, UiKit.Muted);
        l.MouseFilter = MouseFilterEnum.Ignore;
        UiKit.FillRect(l);
        l.OffsetLeft = 16; l.OffsetRight = -16;
        l.HorizontalAlignment = a;
        l.VerticalAlignment = VerticalAlignment.Center;
        AddChild(l);
        return l;
    }

    private void Refresh()
    {
        Text = _values[Index];
        var first = Index == 0 && !_wrap;
        var last = Index == _values.Length - 1 && !_wrap;
        _prev.Modulate = new Color(1, 1, 1, first || Disabled ? 0.25f : 1f);
        _next.Modulate = new Color(1, 1, 1, last || Disabled ? 0.25f : 1f);
    }

    public void SetIndex(int i, bool notify = false)
    {
        Index = Mathf.Clamp(i, 0, _values.Length - 1);
        Refresh();
        if (notify) _onChange?.Invoke(Index);
    }

    private void Step(int d, bool wrap)
    {
        if (Disabled) return;
        var n = Index + d;
        if (wrap && Wrap) n = (n + _values.Length) % _values.Length;
        n = Mathf.Clamp(n, 0, _values.Length - 1);
        if (n == Index) return;
        Index = n;
        Refresh();
        Sfx.Ui("ui_click", -8f, 1.1f);
        _onChange?.Invoke(Index);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (Disabled) return;
        if (e.IsActionPressed("ui_left", true)) { Step(-1, false); AcceptEvent(); return; }
        if (e.IsActionPressed("ui_right", true)) { Step(+1, false); AcceptEvent(); return; }
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
        {
            UiKit.FocusSilently(this);
            var x = mb.Position.X / Mathf.Max(1, Size.X);
            if (x < 0.3f) Step(-1, false);
            else if (x > 0.7f) Step(+1, false);
            else Step(+1, true);
            AcceptEvent();
        }
    }
}

/// <summary>
/// Gabarit commun des écrans de menu : titre + onglets en haut à gauche, contenu, barre de pied
/// avec aides de touches à gauche et action principale à droite.
/// </summary>
public partial class MenuScreen : Control
{
    protected HBoxContainer Header;
    protected HBoxContainer Tabs;
    protected Control Body;
    protected HBoxContainer FooterLeft;
    protected HBoxContainer FooterRight;
    protected Label TitleLabel;
    protected Label HeaderRight;

    protected void BuildFrame(string title, float scrim = 0f, float shade = 0f)
    {
        Theme = UiKit.Theme;
        UiKit.FillRect(this);
        if (scrim > 0) AddChild(UiKit.Scrim(scrim));
        if (shade > 0) AddChild(UiKit.SideShade(shade));
        var m = UiKit.Margin(UiKit.ScreenX, UiKit.ScreenTop, UiKit.ScreenX, UiKit.ScreenBottom);
        UiKit.FillRect(m);
        m.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(m);
        var v = UiKit.VBox(28);
        v.MouseFilter = MouseFilterEnum.Ignore;
        m.AddChild(v);

        Header = UiKit.HBox(40);
        TitleLabel = UiKit.Heading(title, 48);
        Header.AddChild(TitleLabel);
        Tabs = UiKit.HBox(24);
        Tabs.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        Header.AddChild(Tabs);
        Header.AddChild(UiKit.Spacer(false, true));
        HeaderRight = UiKit.Label("", 14, UiKit.Muted);
        HeaderRight.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        Header.AddChild(HeaderRight);
        v.AddChild(Header);

        Body = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        v.AddChild(Body);

        var foot = UiKit.HBox(24);
        foot.CustomMinimumSize = new Vector2(0, 60);
        FooterLeft = UiKit.HBox(28);
        FooterLeft.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        foot.AddChild(FooterLeft);
        foot.AddChild(UiKit.Spacer(false, true));
        FooterRight = UiKit.HBox(12);
        foot.AddChild(FooterRight);
        v.AddChild(foot);
        UiKit.FadeIn(this);
    }

    /// <summary>Colonne défilante verticale qui suit le focus clavier / manette.</summary>
    public static ScrollContainer Scroll(float minWidth = 0)
    {
        return new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
            CustomMinimumSize = new Vector2(minWidth, 0),
            MouseFilter = MouseFilterEnum.Pass,
        };
    }

    /// <summary>Ajoute un onglet ; renvoie le bouton. Les onglets se changent à la souris ou avec Q / E (LB / RB) :
    /// ils ne prennent pas le focus, la croix directionnelle reste dans le contenu.</summary>
    protected Button AddTab(string name, Action onSelect)
    {
        var b = UiKit.Button(name, null, "Tab", 40);
        b.FocusMode = FocusModeEnum.None;
        b.ToggleMode = true;
        b.Pressed += () =>
        {
            foreach (var c in Tabs.GetChildren())
                if (c is Button o) o.SetPressedNoSignal(o == b);
            onSelect();
            FocusContent();
        };
        Tabs.AddChild(b);
        return b;
    }

    protected void SelectTab(int i)
    {
        if (i < 0 || i >= Tabs.GetChildCount()) return;
        ((Button)Tabs.GetChild(i)).EmitSignal(BaseButton.SignalName.Pressed);
    }

    protected int CurrentTab()
    {
        for (var i = 0; i < Tabs.GetChildCount(); i++)
            if (((Button)Tabs.GetChild(i)).ButtonPressed) return i;
        return 0;
    }

    /// <summary>Onglets précédent / suivant avec Q / E (ou LB / RB).</summary>
    protected bool HandleTabKeys(InputEvent e)
    {
        if (Tabs.GetChildCount() < 2) return false;
        var dir = 0;
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (GetViewport().GuiGetFocusOwner() is LineEdit) return false;
            if (k.PhysicalKeycode == Key.Q) dir = -1;
            else if (k.PhysicalKeycode == Key.E) dir = 1;
        }
        else if (e is InputEventJoypadButton { Pressed: true } j)
        {
            if (j.ButtonIndex == JoyButton.LeftShoulder) dir = -1;
            else if (j.ButtonIndex == JoyButton.RightShoulder) dir = 1;
        }
        if (dir == 0) return false;
        var n = Tabs.GetChildCount();
        SelectTab((CurrentTab() + n + dir) % n);
        return true;
    }

    /// <summary>Après un changement d'onglet au clavier : focus sur le premier élément actionnable du contenu.</summary>
    protected virtual void FocusContent()
    {
        Callable.From(() => UiKit.FocusSilently(UiKit.FirstFocusable(Body))).CallDeferred();
    }

    /// <summary>Clé de focus : permet de retrouver l'élément équivalent après une reconstruction de liste.</summary>
    protected static void SetFocusKey(Control c, string key) => c.SetMeta("focus_key", key);

    protected string FocusedKey()
    {
        var f = GetViewport()?.GuiGetFocusOwner();
        return f != null && f.HasMeta("focus_key") ? f.GetMeta("focus_key").AsString() : null;
    }

    /// <summary>Redonne le focus (différé) à l'élément portant la clé, sinon au repli.</summary>
    protected void RestoreFocus(string key, Control fallback = null)
    {
        Callable.From(() =>
        {
            var c = key != null ? FindKey(Body, key) : null;
            UiKit.FocusSilently(c ?? fallback);
        }).CallDeferred();
    }

    private static Control FindKey(Node root, string key)
    {
        foreach (var n in root.GetChildren())
        {
            if (n.IsQueuedForDeletion()) continue;
            if (n is Control c && c.HasMeta("focus_key") && c.GetMeta("focus_key").AsString() == key) return c;
            if (FindKey(n, key) is { } f) return f;
        }
        return null;
    }

    protected void ClearBody()
    {
        foreach (var c in Body.GetChildren()) c.QueueFree();
    }
}
