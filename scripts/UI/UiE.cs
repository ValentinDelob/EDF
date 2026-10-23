using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Kit d'interface « direction E » (ADN EDF, cf. docs/maquettes/DECISIONS.md) : palette verte, polices
/// Share Tech Mono / Saira Condensed, contours sombres, bandeaux biseautés, panneaux bordés de vert.
/// Les écrans sont conçus sur une scène de 1280×720 (coordonnées des maquettes) mise à l'échelle de la fenêtre.
/// </summary>
public static class UiE
{
    // ------------------------------------------------------------ Palette
    public static readonly Color BgHi = new("158050");
    public static readonly Color BgMid = new("0c4a2c");
    public static readonly Color BgLo = new("052012");
    public static readonly Color Frame = new("46e86b");      // cadres
    public static readonly Color Edge = new("2ab84f");       // liserés
    public static readonly Color Bright = new("8cff9e");     // filets clairs
    public static readonly Color Mint = new("9cffb0");       // libellés verts
    public static readonly Color Lime = new("7cff8e");       // numéros
    public static readonly Color Text = Colors.White;
    public static readonly Color TextSoft = new("dbf6e6");
    public static readonly Color TextDim = new("d8f5dd");
    public static readonly Color TextMuted = new("b8d8be");
    public static readonly Color Ink = new("031208");        // contours, texte sur vert vif
    public static readonly Color PanelBg = new(6 / 255f, 40 / 255f, 18 / 255f, 0.88f);
    public static readonly Color CardBg = new(6 / 255f, 40 / 255f, 18 / 255f, 0.84f);
    public static readonly Color BandDark = new(4 / 255f, 24 / 255f, 12 / 255f, 0.82f);
    public static readonly Color Danger = new("ff8c7a");
    public static readonly Color DangerSoft = new("ffb09e");
    public static readonly Color Gold = new("ffe08a");
    public static readonly Color Cyan = new("3fe0c8");

    /// <summary>Taille de conception des maquettes.</summary>
    public static readonly Vector2 Design = new(1280, 720);

    // ------------------------------------------------------------ Polices
    private static Font _mono, _cond, _condSemi, _condXb;

    private static Font Load(string path, Font fallback)
    {
        var f = ResourceLoader.Exists(path) ? ResourceLoader.Load<FontFile>(path) : null;
        return f ?? fallback;
    }

    /// <summary>Share Tech Mono : chiffres, briefing, statistiques.</summary>
    public static Font Mono => _mono ??= Load("res://assets/fonts/ShareTechMono-Regular.ttf", UiKit.BodyFont);
    /// <summary>Saira Condensed Bold : titres, boutons.</summary>
    public static Font Cond => _cond ??= Load("res://assets/fonts/SairaCondensed-Bold.ttf", UiKit.HeadFont);
    public static Font CondSemi => _condSemi ??= Load("res://assets/fonts/SairaCondensed-SemiBold.ttf", Cond);
    public static Font CondXBold => _condXb ??= Load("res://assets/fonts/SairaCondensed-ExtraBold.ttf", Cond);

    /// <summary>Police avec espacement des lettres (letter-spacing CSS).</summary>
    public static Font Spaced(Font f, int spacing) => new FontVariation { BaseFont = f, SpacingGlyph = spacing };

    /// <summary>Réduire les animations (option du jeu).</summary>
    public static bool Calm => Settings.I != null && Settings.I.ReduceMotion;

    // ------------------------------------------------------------ Libellés
    /// <summary>Libellé avec contour sombre (text-shadow 1 px des maquettes).</summary>
    public static Label Label(string text, int size, Color? color = null, Font font = null, int outline = 3)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontOverride("font", font ?? Mono);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color ?? Text);
        if (outline > 0)
        {
            l.AddThemeConstantOverride("outline_size", outline);
            l.AddThemeColorOverride("font_outline_color", Ink);
        }
        return l;
    }

    public static Label Mono_(string text, int size, Color? color = null) => Label(text, size, color, Mono);
    public static Label Title(string text, int size, Color? color = null, Font font = null) => Label(text, size, color, font ?? Cond, 4);

    /// <summary>Place un contrôle à une position absolue de la maquette.</summary>
    public static T At<T>(this T c, float x, float y, float w = -1, float h = -1) where T : Control
    {
        c.Position = new Vector2(x, y);
        if (w >= 0 || h >= 0) c.Size = new Vector2(w >= 0 ? w : c.Size.X, h >= 0 ? h : c.Size.Y);
        if (w >= 0) c.CustomMinimumSize = new Vector2(w, c.CustomMinimumSize.Y);
        return c;
    }

    // ------------------------------------------------------------ Panneaux
    /// <summary>Panneau d'information : bordure verte 3 px, fond vert sombre, halo.</summary>
    public static StyleBoxFlat PanelBox(int border = 3, int padH = 18, int padV = 14, float alpha = 0.88f, int glow = 10)
    {
        var s = new StyleBoxFlat
        {
            BgColor = new Color(PanelBg, alpha),
            BorderColor = Frame,
            ShadowColor = new Color(Frame, 0.3f),
            ShadowSize = glow,
            ContentMarginLeft = padH, ContentMarginRight = padH, ContentMarginTop = padV, ContentMarginBottom = padV,
        };
        s.SetBorderWidthAll(border);
        return s;
    }

    public static PanelContainer Panel(int border = 3, int padH = 18, int padV = 14, float alpha = 0.88f)
    {
        var p = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", PanelBox(border, padH, padV, alpha));
        return p;
    }

    // ------------------------------------------------------------ Thème
    private static Theme _theme;

    /// <summary>Thème vert des écrans E (contrôles standard : champs, curseurs, sélecteurs, défilement).</summary>
    public static Theme Theme
    {
        get
        {
            if (_theme != null) return _theme;
            var t = (Theme)UiKit.Theme.Duplicate(true);
            t.DefaultFont = Mono;
            t.DefaultFontSize = 17;
            var glass = new Color(3 / 255f, 22 / 255f, 10 / 255f, 0.85f);
            var glassHot = new Color(14 / 255f, 80 / 255f, 34 / 255f, 0.92f);
            StyleBoxFlat Plate(Color bg, Color border, int bw, int padH = 16, int padV = 8)
            {
                var s = UiKit.Box(bg, border, bw, 0, padH, padV);
                return s;
            }
            // Bouton générique (touches à réassigner, réinitialiser…)
            t.SetStylebox("normal", "Button", Plate(glass, new Color(Lime, 0.32f), 1));
            t.SetStylebox("hover", "Button", Plate(glassHot, new Color(Mint, 0.7f), 1));
            t.SetStylebox("pressed", "Button", Plate(Frame, Mint, 1));
            t.SetStylebox("focus", "Button", Plate(new Color(0, 0, 0, 0), Mint, 2));
            t.SetStylebox("disabled", "Button", Plate(new Color(glass, 0.4f), new Color(Lime, 0.15f), 1));
            foreach (var k in new[] { "font_color", "font_hover_color", "font_focus_color", "font_hover_pressed_color" })
                t.SetColor(k, "Button", Text);
            t.SetColor("font_pressed_color", "Button", Ink);
            t.SetColor("font_disabled_color", "Button", TextMuted);
            t.SetColor("font_outline_color", "Button", Ink);
            t.SetConstant("outline_size", "Button", 3);
            t.SetFont("font", "Button", Mono);
            t.SetFontSize("font_size", "Button", 17);

            t.SetColor("font_color", "Label", Text);
            t.SetColor("font_outline_color", "Label", Ink);
            t.SetConstant("outline_size", "Label", 3);
            t.SetFont("font", "Label", Mono);

            // Champs de saisie
            t.SetStylebox("normal", "LineEdit", Plate(glass, Edge, 1, 12, 8));
            t.SetStylebox("focus", "LineEdit", Plate(new Color(0, 0, 0, 0), Mint, 2, 12, 8));
            t.SetColor("caret_color", "LineEdit", Mint);
            t.SetColor("font_color", "LineEdit", Text);
            t.SetColor("font_placeholder_color", "LineEdit", new Color(TextMuted, 0.6f));
            t.SetColor("selection_color", "LineEdit", new Color(Frame, 0.35f));
            t.SetFont("font", "LineEdit", Mono);
            t.SetFontSize("font_size", "LineEdit", 18);

            // Curseurs
            t.SetStylebox("slider", "HSlider", UiKit.Box(new Color(0, 0, 0, 0.55f), Edge, 1, 0, 0, 3));
            t.SetStylebox("grabber_area", "HSlider", UiKit.Box(new Color("33c653"), default, 0, 0, 0, 3));
            t.SetStylebox("grabber_area_highlight", "HSlider", UiKit.Box(Frame, default, 0, 0, 0, 3));
            t.SetIcon("grabber", "HSlider", Diamond(18, Mint));
            t.SetIcon("grabber_highlight", "HSlider", Diamond(20, Color.FromHtml("e8ffee")));

            // Défilement
            t.SetStylebox("scroll", "VScrollBar", UiKit.Box(new Color(0, 0, 0, 0.3f), default, 0, 0, 2, 2));
            t.SetStylebox("grabber", "VScrollBar", UiKit.Box(new Color(Edge, 0.6f), default, 0, 0, 2, 2));
            t.SetStylebox("grabber_highlight", "VScrollBar", UiKit.Box(Frame, default, 0, 0, 2, 2));
            t.SetStylebox("grabber_pressed", "VScrollBar", UiKit.Box(Mint, default, 0, 0, 2, 2));

            // Barres de progression
            t.SetStylebox("background", "ProgressBar", UiKit.Box(new Color(0, 0, 0, 0.5f), Edge, 1, 0, 0, 0));
            t.SetStylebox("fill", "ProgressBar", UiKit.Box(new Color("33c653"), default, 0, 0, 0, 0));

            // Lignes de liste (parties LAN, armes) : verre sombre, sélection = liseré vert vif à gauche.
            t.SetStylebox("normal", "Row", UiKit.Box(new Color(3 / 255f, 22 / 255f, 10 / 255f, 0.6f), default, 0, 0, 22, 12));
            t.SetStylebox("hover", "Row", UiKit.Bar(glassHot, new Color(Mint, 0.7f), 4, 18, 12));
            t.SetStylebox("pressed", "Row", UiKit.Bar(new Color(Frame, 0.28f), Frame, 4, 18, 12));
            t.SetStylebox("hover_pressed", "Row", UiKit.Bar(new Color(Frame, 0.36f), Mint, 4, 18, 12));
            t.SetStylebox("focus", "Row", UiKit.Bar(new Color(0, 0, 0, 0), Mint, 4, 18, 12));
            t.SetStylebox("disabled", "Row", UiKit.Box(new Color(3 / 255f, 22 / 255f, 10 / 255f, 0.35f), default, 0, 0, 22, 12));
            t.SetStylebox("normal", "Card", UiKit.Box(new Color(3 / 255f, 22 / 255f, 10 / 255f, 0.75f), new Color(Lime, 0.32f), 1, 0, 22, 12));
            t.SetStylebox("hover", "Card", UiKit.Box(glassHot, new Color(Mint, 0.7f), 1, 0, 22, 12));
            // Action principale (bouton texte) : plaque vert vif.
            t.SetStylebox("normal", "Primary", UiKit.Box(Frame, Mint, 1, 0, 36, 10));
            t.SetStylebox("hover", "Primary", UiKit.Box(new Color("5af07c"), new Color("b4ffc4"), 1, 0, 36, 10));
            t.SetStylebox("pressed", "Primary", UiKit.Box(Edge, Mint, 1, 0, 36, 10));
            t.SetStylebox("focus", "Primary", UiKit.Box(new Color(0, 0, 0, 0), Colors.White, 2, 0, 36, 10));
            t.SetStylebox("disabled", "Primary", UiKit.Box(glass, new Color(Lime, 0.3f), 1, 0, 36, 10));
            foreach (var k in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" })
                t.SetColor(k, "Primary", Ink);
            t.SetColor("font_disabled_color", "Primary", TextMuted);
            t.SetConstant("outline_size", "Primary", 0);
            t.SetFont("font", "Primary", Cond);
            t.SetFontSize("font_size", "Primary", 24);

            // Onglets (dans le bandeau de titre) : capitales Saira, actif = souligné vert vif.
            t.SetStylebox("normal", "Tab", UiKit.Underline(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), 3, 4, 4));
            t.SetStylebox("hover", "Tab", UiKit.Underline(new Color(0, 0, 0, 0), new Color(Edge, 0.7f), 3, 4, 4));
            t.SetStylebox("pressed", "Tab", UiKit.Underline(new Color(0, 0, 0, 0), Frame, 3, 4, 4));
            t.SetStylebox("hover_pressed", "Tab", UiKit.Underline(new Color(0, 0, 0, 0), Mint, 3, 4, 4));
            t.SetStylebox("focus", "Tab", new StyleBoxEmpty());
            t.SetColor("font_color", "Tab", new Color(TextSoft, 0.7f));
            t.SetColor("font_hover_color", "Tab", Text);
            t.SetColor("font_pressed_color", "Tab", Text);
            t.SetColor("font_hover_pressed_color", "Tab", Text);
            t.SetColor("font_focus_color", "Tab", Text);
            t.SetFont("font", "Tab", CondSemi);
            t.SetFontSize("font_size", "Tab", 22);

            t.SetStylebox("panel", "TooltipPanel", PanelBox(2, 10, 6, 0.95f, 0));
            t.SetColor("font_color", "TooltipLabel", Text);
            _theme = t;
            return t;
        }
    }

    private static ImageTexture Diamond(int size, Color c)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var r = size / 2f;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var d = Mathf.Abs(x + 0.5f - r) + Mathf.Abs(y + 0.5f - r);
            img.SetPixel(x, y, new Color(c, Mathf.Clamp(r - d, 0, 1)));
        }
        return ImageTexture.CreateFromImage(img);
    }

    // ------------------------------------------------------------ Indications de touches
    /// <summary>Indication « Entrée:OK » des bandes basses (cliquable si une action est fournie).</summary>
    public static Button KeyHint(string text, Action onClick = null, int size = 19)
    {
        var b = new Button { Text = text, Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        var empty = new StyleBoxEmpty { ContentMarginLeft = 4, ContentMarginRight = 4 };
        foreach (var k in new[] { "normal", "hover", "pressed", "focus", "disabled", "hover_pressed" }) b.AddThemeStyleboxOverride(k, empty);
        b.AddThemeFontOverride("font", Mono);
        b.AddThemeFontSizeOverride("font_size", size);
        b.AddThemeColorOverride("font_color", Text);
        b.AddThemeColorOverride("font_hover_color", Mint);
        b.AddThemeColorOverride("font_pressed_color", Mint);
        b.AddThemeColorOverride("font_hover_pressed_color", Mint);
        b.AddThemeColorOverride("font_focus_color", Text);
        b.AddThemeColorOverride("font_outline_color", Ink);
        b.AddThemeConstantOverride("outline_size", 3);
        if (onClick != null) b.Pressed += onClick;
        else b.MouseDefaultCursorShape = Control.CursorShape.Arrow;
        return b;
    }

    /// <summary>Nom court d'une touche réassignable (pour les aides).</summary>
    public static string Key(string action) => Settings.Describe(Settings.I.GetBinding(action));

    // ------------------------------------------------------------ Dessin
    /// <summary>Polygone convexe à coins coupés (haut-droite et bas-gauche) des plaques.</summary>
    public static Vector2[] CutCorners(Rect2 r, float c) => new[]
    {
        r.Position, new Vector2(r.End.X - c, r.Position.Y), new Vector2(r.End.X, r.Position.Y + c),
        r.End, new Vector2(r.Position.X + c, r.End.Y), new Vector2(r.Position.X, r.End.Y - c),
    };

    /// <summary>Dégradé horizontal à plusieurs arrêts dans un polygone convexe (découpé en bandes verticales).</summary>
    public static void HGradient(CanvasItem ci, Vector2[] poly, float x0, float x1, IList<(float at, Color c)> stops)
    {
        var minX = float.MaxValue; var maxX = float.MinValue;
        foreach (var p in poly) { minX = Mathf.Min(minX, p.X); maxX = Mathf.Max(maxX, p.X); }
        x0 = Mathf.Max(x0, minX); x1 = Mathf.Min(x1, maxX);
        if (x1 <= x0) return;
        var breaks = new List<float> { x0, x1 };
        foreach (var p in poly) if (p.X > x0 && p.X < x1) breaks.Add(p.X);
        foreach (var s in stops) if (s.at > x0 && s.at < x1) breaks.Add(s.at);
        breaks.Sort();
        Color ColAt(float x)
        {
            if (x <= stops[0].at) return stops[0].c;
            for (var i = 1; i < stops.Count; i++)
                if (x <= stops[i].at) return stops[i - 1].c.Lerp(stops[i].c, (x - stops[i - 1].at) / Mathf.Max(0.001f, stops[i].at - stops[i - 1].at));
            return stops[^1].c;
        }
        for (var i = 0; i < breaks.Count - 1; i++)
        {
            var a = breaks[i]; var b = breaks[i + 1];
            if (b - a < 0.01f) continue;
            var (ta, ba) = Span(poly, a + 0.001f);
            var (tb, bb) = Span(poly, b - 0.001f);
            ci.DrawPolygon(new[] { new Vector2(a, ta), new Vector2(b, tb), new Vector2(b, bb), new Vector2(a, ba) },
                new[] { ColAt(a), ColAt(b), ColAt(b), ColAt(a) });
        }
    }

    /// <summary>Haut et bas d'un polygone convexe à l'abscisse x.</summary>
    private static (float top, float bottom) Span(Vector2[] poly, float x)
    {
        var top = float.MaxValue; var bot = float.MinValue;
        for (var i = 0; i < poly.Length; i++)
        {
            var p = poly[i]; var q = poly[(i + 1) % poly.Length];
            if ((x < Mathf.Min(p.X, q.X)) || (x > Mathf.Max(p.X, q.X))) continue;
            float y;
            if (Mathf.Abs(q.X - p.X) < 0.0001f) { top = Mathf.Min(top, Mathf.Min(p.Y, q.Y)); bot = Mathf.Max(bot, Mathf.Max(p.Y, q.Y)); continue; }
            y = p.Y + (q.Y - p.Y) * (x - p.X) / (q.X - p.X);
            top = Mathf.Min(top, y); bot = Mathf.Max(bot, y);
        }
        return (top, bot);
    }

    /// <summary>Dégradé vertical simple (rectangle).</summary>
    public static void VGradient(CanvasItem ci, Rect2 r, Color top, Color bottom) =>
        ci.DrawPolygon(new[] { r.Position, new Vector2(r.End.X, r.Position.Y), r.End, new Vector2(r.Position.X, r.End.Y) },
            new[] { top, top, bottom, bottom });

    public static void Poly(CanvasItem ci, Color c, params float[] xy)
    {
        var pts = new Vector2[xy.Length / 2];
        for (var i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
        ci.DrawColoredPolygon(pts, c);
    }

    /// <summary>Icônes des plaques (dessinées dans un carré de 16 centré en c).</summary>
    public enum Icon { None, Diamond, Play, Gear, Exit, Replay, Check }

    public static void DrawIcon(CanvasItem ci, Icon icon, Vector2 c, Color col)
    {
        switch (icon)
        {
            case Icon.Diamond:
                ci.DrawPolyline(new[] { c + new Vector2(0, -6), c + new Vector2(6, 0), c + new Vector2(0, 6), c + new Vector2(-6, 0), c + new Vector2(0, -6) }, col, 2f, true);
                break;
            case Icon.Play:
                ci.DrawColoredPolygon(new[] { c + new Vector2(-4, -6), c + new Vector2(4, 0), c + new Vector2(-4, 6) }, col);
                break;
            case Icon.Gear:
                ci.DrawArc(c, 3, 0, Mathf.Tau, 16, col, 2f, true);
                for (var i = 0; i < 8; i++)
                {
                    var d = Vector2.Right.Rotated(i * Mathf.Pi / 4);
                    ci.DrawLine(c + d * 5f, c + d * 7f, col, 1.6f, true);
                }
                break;
            case Icon.Exit:
                ci.DrawPolyline(new[] { c + new Vector2(-2, -5), c + new Vector2(-5, -5), c + new Vector2(-5, 5), c + new Vector2(-2, 5) }, col, 2f, true);
                ci.DrawPolyline(new[] { c + new Vector2(1, -3), c + new Vector2(4, 0), c + new Vector2(1, 3) }, col, 2f, true);
                ci.DrawLine(c + new Vector2(4, 0), c + new Vector2(-2, 0), col, 2f, true);
                break;
            case Icon.Replay:
                ci.DrawArc(c, 5, -Mathf.Pi / 2, Mathf.Pi * 1.5f - 0.01f, 20, col, 2f, true);
                ci.DrawPolyline(new[] { c + new Vector2(0, -7), c + new Vector2(3, -5), c + new Vector2(0, -3) }, col, 2f, true);
                break;
            case Icon.Check:
                ci.DrawPolyline(new[] { c + new Vector2(-6, 0), c + new Vector2(-2, 4), c + new Vector2(6, -5) }, col, 3f, true);
                break;
        }
    }
}

/// <summary>
/// Écran de conception 1280×720 mis à l'échelle de la fenêtre (centré) : les contrôles enfants sont placés aux
/// coordonnées des maquettes. Le fond (<see cref="MenuBackdrop"/>) et les bandeaux couvrent toute la fenêtre.
/// </summary>
public partial class EStage : Control
{
    public EStage()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Size = UiE.Design;
    }

    public override void _Ready()
    {
        Fit();
        GetViewport().SizeChanged += Fit;
    }

    public override void _ExitTree()
    {
        if (GetViewport() != null) GetViewport().SizeChanged -= Fit;
    }

    public static float ScaleFor(Vector2 vp) => Mathf.Min(vp.X / UiE.Design.X, vp.Y / UiE.Design.Y);

    private void Fit()
    {
        var vp = GetViewportRect().Size;
        var s = ScaleFor(vp);
        Scale = new Vector2(s, s);
        Size = UiE.Design;
        Position = ((vp - UiE.Design * s) / 2f).Floor();
    }

    /// <summary>Marge de conception disponible à gauche / à droite de la scène (fenêtre plus large que 16:9).</summary>
    public float SideRoom => Mathf.Max(0, (GetViewportRect().Size.X / Scale.X - UiE.Design.X) / 2f);
}

/// <summary>Bandeau de titre biseauté (haut de l'écran) : aplat sombre sous le titre, deux bandes vertes.</summary>
public partial class TitleBand : Control
{
    public float Dark = 0.82f;
    public bool Accent = true;

    public TitleBand() { MouseFilter = MouseFilterEnum.Ignore; Size = new Vector2(1280, 100); }

    public override void _Draw()
    {
        const float far = 4000; // déborde de la scène sur les écrans plus larges que 16:9
        UiE.Poly(this, new Color(0.0157f, 0.094f, 0.047f, Dark), -far, 0, 470, 0, 420, 76, -far, 76);
        UiE.Poly(this, UiE.Frame, 470, 0, far, 0, far, 10, 476, 10, 426, 82, -far, 82, -far, 76, 420, 76);
        UiE.Poly(this, UiE.Edge, 484, 16, far, 16, far, 22, 488, 22, 440, 92, -far, 92, -far, 88, 434, 88);
        if (Accent) UiE.Poly(this, UiE.Bright, -far, 96, 400, 96, 404, 100, -far, 100);
    }
}

/// <summary>Bande basse biseautée (aide clavier à droite, dans l'aplat sombre).</summary>
public partial class BottomBand : Control
{
    public float Dark = 0.82f;

    public BottomBand() { MouseFilter = MouseFilterEnum.Ignore; Size = new Vector2(1280, 80); }

    public override void _Draw()
    {
        const float far = 4000;
        UiE.Poly(this, UiE.Bright, 700, 4, far, 4, far, 8, 704, 8);
        UiE.Poly(this, new Color(0.0157f, 0.094f, 0.047f, Dark), 680, 12, far, 12, far, 60, 650, 60);
        UiE.Poly(this, UiE.Frame, -far, 66, far, 66, far, 72, -far, 72);
        UiE.Poly(this, UiE.Edge, -far, 74, far, 74, far, 80, -far, 80);
        // Sous la bande : prolonge la dernière couleur jusqu'au bas de la fenêtre (écrans plus hauts que 16:9).
        UiE.Poly(this, UiE.Edge, -far, 80, far, 80, far, 400, -far, 400);
    }
}

/// <summary>Flèche animée (pointeur de la plaque sélectionnée).</summary>
public partial class PlatePointer : Control
{
    public PlatePointer() { MouseFilter = MouseFilterEnum.Ignore; Size = new Vector2(12, 18); }
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw()
    {
        var t = UiE.Calm ? 0f : (float)(Time.GetTicksMsec() % 1000) / 1000f;
        var k = 0.5f - 0.5f * Mathf.Cos(t * Mathf.Tau); // 0 → 1 → 0 (ease-in-out)
        var dx = 4f * k;
        var a = 1f - 0.4f * k;
        DrawColoredPolygon(new[] { new Vector2(dx, 0), new Vector2(12 + dx, 9), new Vector2(dx, 18) }, new Color(UiE.Mint, a));
    }
}

/// <summary>Assemblage commun d'un écran E : fond animé, scène mise à l'échelle, bandeau de titre, bande basse.</summary>
public static class EFrame
{
    public static EStage Build(Control root, string title, out Label titleLabel, bool backdrop = true, bool accent = true, float dark = 0.82f)
    {
        if (backdrop) root.AddChild(new MenuBackdrop());
        var stage = new EStage();
        root.AddChild(stage);
        stage.AddChild(new TitleBand { Accent = accent, Dark = dark });
        titleLabel = TitleText(title);
        stage.AddChild(titleLabel);
        stage.AddChild(new BottomBand { Position = new Vector2(0, 640), Dark = dark });
        return stage;
    }

    /// <summary>Titre du bandeau : Saira 50, blanc, ombre portée 3 px + contour sombre.</summary>
    public static Label TitleText(string title)
    {
        var l = UiE.Label(title, 50, UiE.Text, UiE.Cond, 4);
        l.AddThemeColorOverride("font_shadow_color", UiE.Ink);
        l.AddThemeConstantOverride("shadow_offset_x", 3);
        l.AddThemeConstantOverride("shadow_offset_y", 3);
        l.AddThemeConstantOverride("shadow_outline_size", 4);
        l.Position = new Vector2(60, 6);
        l.Size = new Vector2(420, 64);
        l.VerticalAlignment = VerticalAlignment.Center;
        return l;
    }

    /// <summary>Rangée d'aides clavier alignée à droite dans la bande basse.</summary>
    public static HBoxContainer Hints(Control stage)
    {
        var h = UiKit.HBox(12);
        h.Alignment = BoxContainer.AlignmentMode.End;
        h.Position = new Vector2(680, 650);
        h.Size = new Vector2(570, 44);
        h.MouseFilter = Control.MouseFilterEnum.Ignore;
        stage.AddChild(h);
        return h;
    }
}
