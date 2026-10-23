using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Palette et polices de la direction E (ADN EDF), autonomes pour l'arsenal en attendant le kit d'interface commun.
/// Toutes les cotes des écrans E sont exprimées dans l'espace de la maquette (1280 × 720).
/// </summary>
public static class EStyle
{
    public const float DesignW = 1280f, DesignH = 720f;

    public static readonly Color Text = new("DBF6E6");
    public static readonly Color White = new("FFFFFF");
    public static readonly Color Bright = new("9CFFB0");
    public static readonly Color Accent = new("46E86B");
    public static readonly Color Frame = new("6DC692");
    public static readonly Color FrameInner = new("3A7854");
    public static readonly Color Gold = new("F2D27A");
    public static readonly Color Cyan = new("3FD8F0");
    public static readonly Color Bonus = new("6BEA7A");
    public static readonly Color Outline = new("06140C");
    public static readonly Color DimText = new("6F8C79");
    public static readonly Color Orange = new("F08A1E");
    public static readonly Color Ink = new("03140A");

    private static Font _mono, _cond, _condBold;

    private static Font Load(string path, Font fallback)
    {
        var f = ResourceLoader.Exists(path) ? ResourceLoader.Load<FontFile>(path) : null;
        return f ?? fallback;
    }

    /// <summary>« Share Tech Mono » : chiffres, statistiques.</summary>
    public static Font Mono => _mono ??= Load("res://assets/fonts/ShareTechMono-Regular.ttf", new SystemFont { FontNames = new[] { "Consolas", "Courier New" } });
    /// <summary>« Saira Condensed » demi-gras : textes, lignes de tableau.</summary>
    public static Font Cond => _cond ??= Load("res://assets/fonts/SairaCondensed-SemiBold.ttf", new SystemFont { FontNames = new[] { "Bahnschrift", "Arial Narrow", "Arial" }, FontWeight = 600 });
    /// <summary>« Saira Condensed » gras : titres.</summary>
    public static Font CondBold => _condBold ??= Load("res://assets/fonts/SairaCondensed-Bold.ttf", Cond);
}

/// <summary>
/// Crayon : dessine dans l'espace de la maquette avec un facteur d'échelle K et une origine O (en pixels).
/// Les polices sont rendues à leur taille finale (K × taille) pour rester nettes.
/// </summary>
public readonly struct Pen
{
    public readonly CanvasItem Ci;
    public readonly float K;
    public readonly Vector2 O;

    public Pen(CanvasItem ci, float k, Vector2 o) { Ci = ci; K = k; O = o; }

    public Vector2 P(float x, float y) => O + new Vector2(x, y) * K;
    public Vector2 P(Vector2 v) => O + v * K;
    public Rect2 R(float x, float y, float w, float h) => new(P(x, y), new Vector2(w, h) * K);
    public int Fs(float size) => Math.Max(1, Mathf.RoundToInt(size * K));

    public void Rect(float x, float y, float w, float h, Color c) => Ci.DrawRect(R(x, y, w, h), c);

    /// <summary>Contour intérieur (équivalent de box-shadow: inset 0 0 0 w).</summary>
    public void Inset(float x, float y, float w, float h, Color c, float width = 1f)
    {
        var t = width;
        Rect(x, y, w, t, c);
        Rect(x, y + h - t, w, t, c);
        Rect(x, y + t, t, h - 2 * t, c);
        Rect(x + w - t, y + t, t, h - 2 * t, c);
    }

    public void VGrad(float x, float y, float w, float h, Color top, Color bottom) =>
        Ci.DrawPolygon(new[] { P(x, y), P(x + w, y), P(x + w, y + h), P(x, y + h) }, new[] { top, top, bottom, bottom });

    public void HGrad(float x, float y, float w, float h, Color left, Color right) =>
        Ci.DrawPolygon(new[] { P(x, y), P(x + w, y), P(x + w, y + h), P(x, y + h) }, new[] { left, right, right, left });

    public void Line(float x0, float y0, float x1, float y1, Color c, float w = 1f) => Ci.DrawLine(P(x0, y0), P(x1, y1), c, w * K, true);

    public Vector2[] Map(IReadOnlyList<Vector2> pts)
    {
        var r = new Vector2[pts.Count];
        for (var i = 0; i < r.Length; i++) r[i] = P(pts[i]);
        return r;
    }

    public void Poly(IReadOnlyList<Vector2> pts, Color c) => Ci.DrawColoredPolygon(Map(pts), c);

    public void Outline(IReadOnlyList<Vector2> pts, Color c, float w)
    {
        var m = Map(pts);
        var closed = new Vector2[m.Length + 1];
        Array.Copy(m, closed, m.Length);
        closed[^1] = m[0];
        Ci.DrawPolyline(closed, c, w * K, true);
    }

    /// <summary>Octogone à coins coupés (cadres de la maquette).</summary>
    public static Vector2[] Chamfer(float x, float y, float w, float h, float c) => new[]
    {
        new Vector2(x + c, y), new Vector2(x + w - c, y), new Vector2(x + w, y + c), new Vector2(x + w, y + h - c),
        new Vector2(x + w - c, y + h), new Vector2(x + c, y + h), new Vector2(x, y + h - c), new Vector2(x, y + c),
    };

    /// <summary>
    /// Bandeau hexagonal pointu à gauche et à droite (clip-path de la maquette) rempli d'un dégradé vertical.
    /// </summary>
    public void HexBand(float x, float y, float w, float h, float cut, params (float at, Color c)[] stops)
    {
        var ys = new List<float> { 0f, 0.5f, 1f };
        foreach (var s in stops) if (!ys.Contains(s.at)) ys.Add(s.at);
        ys.Sort();
        Color At(float t)
        {
            if (t <= stops[0].at) return stops[0].c;
            for (var i = 1; i < stops.Length; i++)
                if (t <= stops[i].at)
                    return stops[i - 1].c.Lerp(stops[i].c, (t - stops[i - 1].at) / Mathf.Max(0.0001f, stops[i].at - stops[i - 1].at));
            return stops[^1].c;
        }
        float Inset(float t) => cut * Mathf.Abs(2f * t - 1f);
        for (var i = 0; i < ys.Count - 1; i++)
        {
            float t0 = ys[i], t1 = ys[i + 1];
            if (t1 - t0 < 0.0001f) continue;
            var y0 = y + h * t0;
            var y1 = y + h * t1;
            var pts = new[] { P(x + Inset(t0), y0), P(x + w - Inset(t0), y0), P(x + w - Inset(t1), y1), P(x + Inset(t1), y1) };
            Ci.DrawPolygon(pts, new[] { At(t0), At(t0), At(t1), At(t1) });
        }
    }

    public float Measure(Font f, string s, float size) => f.GetStringSize(s, HorizontalAlignment.Left, -1, Fs(size)).X / K;

    /// <summary>
    /// Texte placé comme en CSS : boîte de ligne de hauteur lineH à partir de yTop, texte centré verticalement.
    /// Renvoie la largeur dessinée (unités de maquette).
    /// </summary>
    public float Text(Font f, float x, float yTop, float lineH, string s, float size, Color c,
        HorizontalAlignment align = HorizontalAlignment.Left, float width = -1f, bool outline = false)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        var fs = Fs(size);
        var asc = f.GetAscent(fs);
        var desc = f.GetDescent(fs);
        var baseY = O.Y + yTop * K + (lineH * K - (asc + desc)) / 2f + asc;
        var pos = new Vector2(O.X + x * K, Mathf.Round(baseY));
        var wpx = width < 0 ? -1f : width * K;
        if (outline) Ci.DrawStringOutline(f, pos, s, align, wpx, fs, Math.Max(2, Mathf.RoundToInt(3 * K)), EStyle.Outline);
        Ci.DrawString(f, pos, s, align, wpx, fs, c);
        return f.GetStringSize(s, HorizontalAlignment.Left, -1, fs).X / K;
    }

    /// <summary>Découpe un paragraphe en lignes tenant dans la largeur donnée.</summary>
    public List<string> Wrap(Font f, string text, float size, float width)
    {
        var lines = new List<string>();
        var cur = "";
        foreach (var word in text.Split(' '))
        {
            var test = cur.Length == 0 ? word : cur + " " + word;
            if (cur.Length > 0 && Measure(f, test, size) > width) { lines.Add(cur); cur = word; }
            else cur = test;
        }
        if (cur.Length > 0) lines.Add(cur);
        return lines;
    }
}

/// <summary>
/// Fond animé des menus de la direction E (version autonome de l'arsenal) : dégradé radial vert, grille qui défile
/// en diagonale, anneaux radar en rotation et balayage, circuits parcourus d'impulsions, nœuds qui pulsent,
/// bande de balayage, particules qui montent, voyant clignotant. Mis à l'échelle pour couvrir l'écran.
/// </summary>
public partial class ArsenalEBackground : Control
{
    /// <summary>Coupe les animations (option « réduire les animations » à brancher sur le kit commun).</summary>
    public static bool ReduceMotion;

    private double _t;
    private GradientTexture2D _radial, _vignette;

    private static readonly Vector2[][] Circuits =
    {
        new Vector2[] { new(0, 200), new(120, 200), new(150, 230), new(420, 230) },
        new Vector2[] { new(0, 470), new(90, 470), new(120, 440), new(400, 440) },
        new Vector2[] { new(440, 620), new(620, 620), new(650, 650), new(900, 650) },
        new Vector2[] { new(900, 90), new(1020, 90), new(1050, 60), new(1280, 60) },
        new Vector2[] { new(1100, 640), new(1280, 640) },
        new Vector2[] { new(20, 560), new(110, 560), new(130, 580) },
    };

    private static readonly (float x, float y, float r, float delay)[] Pulses =
        { (420, 230, 5, 0f), (900, 650, 5, 0.7f), (1050, 60, 5, 1.4f), (400, 440, 4, 1.9f) };

    private static readonly (float x, float y, float delay)[] Motes =
        { (160, 640, 0f), (420, 700, 2.1f), (690, 660, 4.3f), (930, 690, 1.2f), (1120, 650, 5.6f), (300, 560, 6.8f), (820, 580, 3.4f), (1210, 520, 7.7f) };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        UiKit.FillRect(this);
        _radial = Radial(new[] { 0f, 0.55f, 1f }, new[] { new Color("158050"), new Color("0C4A2C"), new Color("052012") });
        _vignette = Radial(new[] { 0f, 0.4f, 1f }, new[] { new Color(0.016f, 0.078f, 0.04f, 0f), new Color(0.016f, 0.078f, 0.04f, 0f), new Color(0.008f, 0.047f, 0.024f, 0.5f) });
    }

    private static GradientTexture2D Radial(float[] offsets, Color[] colors) => new()
    {
        Gradient = new Gradient { Offsets = offsets, Colors = colors },
        Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f),
        FillTo = new Vector2(1f, 0.5f),
        Width = 256,
        Height = 256,
    };

    public override void _Process(double delta)
    {
        if (!ReduceMotion) _t += delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Échelle « couvrir » : la maquette remplit tout l'écran quel que soit son format.
        var k = Mathf.Max(Size.X / EStyle.DesignW, Size.Y / EStyle.DesignH);
        var o = (Size - new Vector2(EStyle.DesignW, EStyle.DesignH) * k) / 2f;
        var p = new Pen(this, k, o);
        var t = (float)_t;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("052012"));
        DrawTextureRect(_radial, p.R(760 - 900, 380 - 640, 1800, 1280), false);

        // Grille qui défile en diagonale (28 px en 6 s).
        var off = t / 6f % 1f * 28f;
        var gc = new Color(110 / 255f, 1f, 160 / 255f, 0.10f);
        for (var x = -56f + off; x < EStyle.DesignW + 56; x += 28) p.Rect(x, -56, 1f / k, EStyle.DesignH + 112, gc);
        for (var y = -56f + off; y < EStyle.DesignH + 56; y += 28) p.Rect(-56, y, EStyle.DesignW + 112, 1f / k, gc);

        // Balayage radar conique (7 s par tour).
        var center = new Vector2(880, 360);
        var sweepRot = t / 7f * Mathf.Tau;
        const int seg = 24;
        var teal = new Color("3FE0C8");
        for (var i = 0; i < seg; i++)
        {
            var a0 = sweepRot + Mathf.DegToRad(300f + 60f * i / seg);
            var a1 = sweepRot + Mathf.DegToRad(300f + 60f * (i + 1) / seg);
            var c0 = new Color(teal, 0.22f * i / seg);
            var c1 = new Color(teal, 0.22f * (i + 1) / seg);
            var v0 = center + new Vector2(Mathf.Sin(a0), -Mathf.Cos(a0)) * 300;
            var v1 = center + new Vector2(Mathf.Sin(a1), -Mathf.Cos(a1)) * 300;
            DrawPolygon(new[] { p.P(center), p.P(v0), p.P(v1) }, new[] { c0.Lerp(c1, 0.5f), c0, c1 });
        }

        // Anneaux.
        DrawArc(p.P(center), 300 * k, 0, Mathf.Tau, 128, new Color(teal, 0.35f), 2 * k, true);
        DashedCircle(p, center, 240, 14, new[] { 120f, 40f, 60f, 30f }, t / 60f * Mathf.Tau, new Color(teal, 0.2f));
        DashedCircle(p, center, 196, 3, new[] { 4f, 10f }, -t / 38f * Mathf.Tau, new Color(teal, 0.4f));
        DrawArc(p.P(center), 150 * k, 0, Mathf.Tau, 96, new Color(teal, 0.3f), 1.5f * k, true);

        // Circuits : tracé fixe puis impulsions qui les parcourent.
        var lineC = new Color("6CFF9A");
        var flowC = new Color("9CFFB0");
        foreach (var c in Circuits) DrawPolyline(p.Map(c), new Color(lineC, 0.22f), 1.6f * k, true);
        for (var i = 0; i < Circuits.Length; i++)
        {
            var slow = i is 0 or 2 or 3;
            if (slow) DashPath(p, Circuits[i], 18, 12, t / 2.4f * 60f, new Color(flowC, 0.55f), 2);
            else DashPath(p, Circuits[i], 6, 14, -t / 1.6f * 60f, new Color(flowC, 0.5f), 2);
        }
        foreach (var (x0, y0, x1) in new[] { (0f, 360f, 60f), (0f, 380f, 100f), (0f, 400f, 70f), (1180f, 300f, 1280f), (1200f, 320f, 1280f) })
            p.Line(x0, y0, x1, y0, new Color(lineC, 0.25f), 5);

        // Nœuds qui pulsent (2,2 s).
        foreach (var (x, y, r, delay) in Pulses)
        {
            var ph = ReduceMotion ? 0.5f : ((t - delay) / 2.2f % 1f + 1f) % 1f;
            var s = 0.5f - 0.5f * Mathf.Cos(ph * Mathf.Tau);
            DrawCircle(p.P(x, y), r * k * Mathf.Lerp(0.7f, 1.25f, s), new Color(flowC, Mathf.Lerp(0.25f, 1f, s)), true, -1, true);
        }
        // Voyant qui clignote.
        var blink = ReduceMotion || t % 1.2f < 0.6f ? 1f : 0.2f;
        p.Rect(1236, 296, 10, 10, new Color(EStyle.Accent, blink));

        // Bande de balayage horizontale (5,5 s).
        if (!ReduceMotion)
        {
            var sy = -120f + t / 5.5f % 1f * 860f;
            var c0 = new Color(110 / 255f, 1f, 160 / 255f, 0f);
            var c1 = new Color(110 / 255f, 1f, 160 / 255f, 0.10f);
            var c2 = new Color(170 / 255f, 1f, 200 / 255f, 0.28f);
            p.VGrad(-200, sy, EStyle.DesignW + 400, 84, c0, c1);
            p.VGrad(-200, sy + 84, EStyle.DesignW + 400, 36, c1, c2);
        }

        // Particules qui montent (9 s).
        if (!ReduceMotion)
            foreach (var (x, y, delay) in Motes)
            {
                var ph = ((t - delay) / 9f % 1f + 1f) % 1f;
                if (t < delay) continue;
                var a = ph < 0.15f ? ph / 0.15f * 0.9f : ph < 0.85f ? Mathf.Lerp(0.9f, 0.6f, (ph - 0.15f) / 0.7f) : Mathf.Lerp(0.6f, 0f, (ph - 0.85f) / 0.15f);
                var py = y - 320f * ph;
                p.Rect(x - 2, py - 2, 7, 7, new Color("6CFF9A", a * 0.25f));
                p.Rect(x, py, 3, 3, new Color(flowC, a));
            }

        // Vignette.
        DrawTextureRect(_vignette, p.R(560 - 760, 360 - 480, 1520, 960), false);
    }

    private void DashedCircle(Pen p, Vector2 c, float r, float width, float[] pattern, float rot, Color col)
    {
        var circ = Mathf.Tau * r;
        var s = 0f;
        var i = 0;
        while (s < circ)
        {
            var len = pattern[i % pattern.Length];
            if (i % 2 == 0)
            {
                var a0 = rot + s / r;
                var a1 = rot + Mathf.Min(s + len, circ) / r;
                DrawArc(p.P(c), r * p.K, a0, a1, Math.Max(2, (int)(len / 6)), col, width * p.K, true);
            }
            s += len;
            i++;
        }
    }

    /// <summary>Pointillés le long d'une polyligne, décalés de « offset » (animation stroke-dashoffset).</summary>
    private void DashPath(Pen p, Vector2[] pts, float dash, float gap, float offset, Color col, float width)
    {
        var period = dash + gap;
        var start = -(((offset % period) + period) % period);
        var acc = 0f;
        for (var i = 0; i < pts.Length - 1; i++)
        {
            var a = pts[i];
            var b = pts[i + 1];
            var len = a.DistanceTo(b);
            var dir = (b - a) / len;
            // Premier tiret qui touche ce segment.
            var d = start + Mathf.Floor((acc - start) / period) * period;
            for (; d < acc + len; d += period)
            {
                var s0 = Mathf.Max(d, acc) - acc;
                var s1 = Mathf.Min(d + dash, acc + len) - acc;
                if (s1 > s0) DrawLine(p.P(a + dir * s0), p.P(a + dir * s1), col, width * p.K, true);
            }
            acc += len;
        }
    }
}
