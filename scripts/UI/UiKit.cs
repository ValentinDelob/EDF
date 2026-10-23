using System;
using Godot;

namespace Rempart;

/// <summary>Jetons de design et fabriques de contrôles : toute l'interface passe par ici.</summary>
public static class UiKit
{
    // Couleurs
    public static readonly Color Bg = new("0b0e11");
    public static readonly Color Surface = new(0.075f, 0.09f, 0.11f, 0.92f);
    public static readonly Color SurfaceHi = new(0.11f, 0.13f, 0.16f, 0.95f);
    public static readonly Color Line = new("2b333b");
    public static readonly Color Text = new("e8eaed");
    public static readonly Color Muted = new("9aa3ad");
    public static readonly Color Accent = new("f0a43a");
    public static readonly Color AccentText = new("1a1206");
    public static readonly Color Good = new("7fd49b");
    public static readonly Color Danger = new("e0533f");
    public static readonly Color Info = new("6fa8ff");

    private static Theme _theme;
    private static SystemFont _body, _head;

    public static SystemFont BodyFont => _body ??= new SystemFont
    {
        FontNames = new[] { "Segoe UI", "Bahnschrift", "Arial" },
        FontWeight = 400,
        Antialiasing = TextServer.FontAntialiasing.Lcd,
        Hinting = TextServer.Hinting.Light,
    };

    public static SystemFont HeadFont => _head ??= new SystemFont
    {
        FontNames = new[] { "Bahnschrift", "Segoe UI", "Arial" },
        FontWeight = 600,
        FontStretch = 87,
        Antialiasing = TextServer.FontAntialiasing.Gray,
    };

    public static StyleBoxFlat Box(Color bg, Color border = default, int borderW = 0, int radius = 4, int padH = 16, int padV = 10)
    {
        var s = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = padH, ContentMarginRight = padH, ContentMarginTop = padV, ContentMarginBottom = padV,
            AntiAliasing = true,
        };
        s.SetBorderWidthAll(borderW);
        return s;
    }

    public static Theme Theme
    {
        get
        {
            if (_theme != null) return _theme;
            var t = new Theme { DefaultFont = BodyFont, DefaultFontSize = 18 };
            // Boutons
            t.SetStylebox("normal", "Button", Box(new Color(1, 1, 1, 0.04f), Line, 1));
            t.SetStylebox("hover", "Button", Box(new Color(1, 1, 1, 0.09f), Muted, 1));
            t.SetStylebox("pressed", "Button", Box(new Color(1, 1, 1, 0.14f), Accent, 1));
            t.SetStylebox("focus", "Button", Box(new Color(0, 0, 0, 0), Accent, 2));
            t.SetStylebox("disabled", "Button", Box(new Color(1, 1, 1, 0.02f), new Color(Line, 0.5f), 1));
            t.SetColor("font_color", "Button", Text);
            t.SetColor("font_hover_color", "Button", Colors.White);
            t.SetColor("font_pressed_color", "Button", Accent);
            t.SetColor("font_focus_color", "Button", Colors.White);
            t.SetColor("font_disabled_color", "Button", new Color(Muted, 0.5f));
            // Bouton principal
            t.SetTypeVariation("Primary", "Button");
            t.SetStylebox("normal", "Primary", Box(Accent, Accent, 0));
            t.SetStylebox("hover", "Primary", Box(Accent.Lightened(0.15f), Accent, 0));
            t.SetStylebox("pressed", "Primary", Box(Accent.Darkened(0.1f), Accent, 0));
            t.SetStylebox("focus", "Primary", Box(new Color(0, 0, 0, 0), Colors.White, 2));
            t.SetStylebox("disabled", "Primary", Box(new Color(Accent, 0.3f), Accent, 0));
            t.SetColor("font_color", "Primary", AccentText);
            t.SetColor("font_hover_color", "Primary", AccentText);
            t.SetColor("font_pressed_color", "Primary", AccentText);
            t.SetColor("font_focus_color", "Primary", AccentText);
            t.SetFont("font", "Primary", HeadFont);
            // Bouton de menu (texte seul, soulignement accent au survol)
            t.SetTypeVariation("MenuItem", "Button");
            var mi = Box(new Color(0, 0, 0, 0), default, 0, 2, 4, 6);
            t.SetStylebox("normal", "MenuItem", mi);
            var mih = Box(new Color(0, 0, 0, 0), Accent, 0, 0, 4, 6);
            mih.BorderWidthLeft = 3;
            mih.ContentMarginLeft = 14;
            t.SetStylebox("hover", "MenuItem", mih);
            t.SetStylebox("focus", "MenuItem", mih);
            t.SetStylebox("pressed", "MenuItem", mih);
            t.SetColor("font_color", "MenuItem", new Color(Text, 0.82f));
            t.SetColor("font_hover_color", "MenuItem", Colors.White);
            t.SetColor("font_focus_color", "MenuItem", Colors.White);
            t.SetColor("font_pressed_color", "MenuItem", Accent);
            t.SetFont("font", "MenuItem", HeadFont);
            t.SetFontSize("font_size", "MenuItem", 30);
            // Onglets / boutons bascule
            t.SetTypeVariation("Chip", "Button");
            t.SetStylebox("normal", "Chip", Box(new Color(0, 0, 0, 0), default, 0, 3, 14, 8));
            t.SetStylebox("hover", "Chip", Box(new Color(1, 1, 1, 0.06f), default, 0, 3, 14, 8));
            t.SetStylebox("pressed", "Chip", Box(new Color(Accent, 0.16f), Accent, 0, 3, 14, 8));
            t.SetStylebox("focus", "Chip", Box(new Color(0, 0, 0, 0), Accent, 1, 3, 14, 8));
            t.SetColor("font_pressed_color", "Chip", Accent);
            t.SetColor("font_color", "Chip", Muted);
            t.SetColor("font_hover_color", "Chip", Text);
            t.SetFont("font", "Chip", HeadFont);
            t.SetFontSize("font_size", "Chip", 19);
            // Cartes sélectionnables
            t.SetTypeVariation("Card", "Button");
            t.SetStylebox("normal", "Card", Box(Surface, Line, 1, 6, 18, 14));
            t.SetStylebox("hover", "Card", Box(SurfaceHi, Muted, 1, 6, 18, 14));
            t.SetStylebox("pressed", "Card", Box(SurfaceHi, Accent, 2, 6, 18, 14));
            t.SetStylebox("focus", "Card", Box(new Color(0, 0, 0, 0), Accent, 1, 6, 18, 14));
            // Panneaux
            t.SetStylebox("panel", "PanelContainer", Box(Surface, Line, 1, 6, 24, 20));
            t.SetTypeVariation("Glass", "PanelContainer");
            t.SetStylebox("panel", "Glass", Box(new Color(0.03f, 0.04f, 0.05f, 0.6f), default, 0, 6, 18, 14));
            // Libellés
            t.SetColor("font_color", "Label", Text);
            t.SetTypeVariation("Muted", "Label");
            t.SetColor("font_color", "Muted", Muted);
            t.SetFontSize("font_size", "Muted", 15);
            t.SetTypeVariation("Heading", "Label");
            t.SetFont("font", "Heading", HeadFont);
            t.SetFontSize("font_size", "Heading", 34);
            t.SetTypeVariation("Eyebrow", "Label");
            t.SetFont("font", "Eyebrow", HeadFont);
            t.SetFontSize("font_size", "Eyebrow", 15);
            t.SetColor("font_color", "Eyebrow", Muted);
            // Champs
            t.SetStylebox("normal", "LineEdit", Box(new Color(0, 0, 0, 0.35f), Line, 1, 4, 14, 10));
            t.SetStylebox("focus", "LineEdit", Box(new Color(0, 0, 0, 0.35f), Accent, 1, 4, 14, 10));
            t.SetColor("caret_color", "LineEdit", Accent);
            // Curseurs
            t.SetStylebox("slider", "HSlider", Box(new Color(1, 1, 1, 0.12f), default, 0, 2, 0, 3));
            t.SetStylebox("grabber_area", "HSlider", Box(Accent, default, 0, 2, 0, 3));
            t.SetStylebox("grabber_area_highlight", "HSlider", Box(Accent.Lightened(0.2f), default, 0, 2, 0, 3));
            t.SetIcon("grabber", "HSlider", Dot(18, Colors.White));
            t.SetIcon("grabber_highlight", "HSlider", Dot(20, Accent));
            // Barres de progression
            t.SetStylebox("background", "ProgressBar", Box(new Color(1, 1, 1, 0.1f), default, 0, 2, 0, 0));
            t.SetStylebox("fill", "ProgressBar", Box(Good, default, 0, 2, 0, 0));
            // Défilement
            t.SetStylebox("scroll", "VScrollBar", Box(new Color(1, 1, 1, 0.03f), default, 0, 3, 3, 3));
            t.SetStylebox("grabber", "VScrollBar", Box(new Color(1, 1, 1, 0.18f), default, 0, 3, 3, 3));
            t.SetStylebox("grabber_highlight", "VScrollBar", Box(new Color(1, 1, 1, 0.3f), default, 0, 3, 3, 3));
            // Info-bulles
            t.SetStylebox("panel", "TooltipPanel", Box(new Color(0.05f, 0.06f, 0.07f, 0.97f), Line, 1, 4, 12, 8));
            // Listes déroulantes
            t.SetStylebox("normal", "OptionButton", Box(new Color(1, 1, 1, 0.04f), Line, 1));
            t.SetStylebox("hover", "OptionButton", Box(new Color(1, 1, 1, 0.08f), Muted, 1));
            t.SetStylebox("panel", "PopupMenu", Box(new Color(0.06f, 0.07f, 0.09f, 0.98f), Line, 1, 4, 6, 6));
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

    public static Label Label(string text, int size = 18, Color? color = null, bool head = false)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        if (color.HasValue) l.AddThemeColorOverride("font_color", color.Value);
        if (head) l.AddThemeFontOverride("font", HeadFont);
        return l;
    }

    public static Label Heading(string text, int size = 34) => Label(text, size, Text, true);

    public static Label Eyebrow(string text, Color? color = null)
    {
        var l = Label(text.ToUpperInvariant(), 14, color ?? Muted, true);
        l.AddThemeConstantOverride("outline_size", 0);
        return l;
    }

    public static Button Button(string text, Action onClick, string variation = null, int minHeight = 48)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, minHeight), FocusMode = Control.FocusModeEnum.All };
        if (variation != null) b.ThemeTypeVariation = variation;
        if (variation == "Primary") b.AddThemeFontSizeOverride("font_size", 22);
        b.Pressed += () =>
        {
            Sfx.Ui("ui_click", -6f);
            onClick?.Invoke();
        };
        b.MouseEntered += () => Sfx.Ui("ui_hover", -10f, 1f, 40);
        return b;
    }

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
    };

    public static ColorRect Rule(Color? c = null) => new() { Color = c ?? Line, CustomMinimumSize = new Vector2(0, 1) };

    public static PanelContainer Panel(string variation = null)
    {
        var p = new PanelContainer();
        if (variation != null) p.ThemeTypeVariation = variation;
        return p;
    }

    public static string Stars(int n) => new string('★', Mathf.Clamp(n, 0, 5)) + new string('☆', 5 - Mathf.Clamp(n, 0, 5));

    public static string Time(float seconds) => $"{(int)seconds / 60:00}:{(int)seconds % 60:00}";

    /// <summary>Fond plein écran semi-opaque pour les écrans par-dessus la scène 3D.</summary>
    public static ColorRect Scrim(float alpha = 0.55f) => new()
    {
        Color = new Color(0.02f, 0.025f, 0.03f, alpha),
        AnchorRight = 1, AnchorBottom = 1,
        MouseFilter = Control.MouseFilterEnum.Stop,
    };

    public static void FillRect(Control c)
    {
        c.AnchorLeft = 0; c.AnchorTop = 0; c.AnchorRight = 1; c.AnchorBottom = 1;
        c.OffsetLeft = c.OffsetTop = c.OffsetRight = c.OffsetBottom = 0;
    }
}
