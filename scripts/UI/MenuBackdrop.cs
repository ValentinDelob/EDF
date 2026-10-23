using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Fond animé commun des menus (menu principal, sélection de mission, arsenal…), d'après les classes bg-* des
/// maquettes E : dégradé radial vert, grille qui défile en diagonale, anneaux radar (sens opposés) avec balayage,
/// impulsions le long des circuits, nœuds qui pulsent, bande de balayage, particules qui montent, voyant qui clignote.
/// Tout est dessiné en 2D (quelques centaines de segments par image) ; animations figées si « réduire les animations ».
/// </summary>
public partial class MenuBackdrop : Control
{
    /// <summary>Faux : fond fixe (bilan), sans balayage ni particules.</summary>
    public bool Animated = true;
    private static GradientTexture2D _radial;
    private float _t;

    private static readonly Vector2 Radar = new(880, 360);

    // Circuits (sous-chemins des tracés SVG « M … H … L … »).
    private static readonly Vector2[][] Circuits =
    {
        P(0, 200, 120, 200, 150, 230, 420, 230),
        P(0, 470, 90, 470, 120, 440, 400, 440),
        P(440, 620, 620, 620, 650, 650, 900, 650),
        P(900, 90, 1020, 90, 1050, 60, 1280, 60),
        P(1100, 640, 1280, 640),
        P(20, 560, 110, 560, 130, 580),
    };
    private static readonly int[] Flow = { 0, 2, 3 };      // tirets 18/12, 2,4 s, vers l'avant
    private static readonly int[] FlowS = { 1, 4, 5 };     // tirets 6/14, 1,6 s, en sens inverse
    private static readonly Vector2[] Stubs = { new(0, 360), new(60, 360), new(0, 380), new(100, 380), new(0, 400), new(70, 400), new(1180, 300), new(1280, 300), new(1200, 320), new(1280, 320) };
    private static readonly (Vector2 p, float r, float delay)[] Nodes = { (new(420, 230), 5, 0f), (new(900, 650), 5, 0.7f), (new(1050, 60), 5, 1.4f), (new(400, 440), 4, 1.9f) };
    private static readonly (float x, float y, float delay)[] Motes =
    {
        (160, 640, 0f), (420, 700, 2.1f), (690, 660, 4.3f), (930, 690, 1.2f), (1120, 650, 5.6f), (300, 560, 6.8f), (820, 580, 3.4f), (1210, 520, 7.7f),
    };

    private static Vector2[] P(params float[] xy)
    {
        var r = new Vector2[xy.Length / 2];
        for (var i = 0; i < r.Length; i++) r[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
        return r;
    }

    public MenuBackdrop()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        UiKit.FillRect(this);
    }

    public override void _Process(double delta)
    {
        if (!Animated || UiE.Calm) return;
        _t += (float)delta;
        QueueRedraw();
    }

    private static Texture2D RadialTex => _radial ??= new GradientTexture2D
    {
        Gradient = new Gradient { Colors = new[] { UiE.BgHi, UiE.BgMid, UiE.BgLo }, Offsets = new[] { 0f, 0.55f, 1f } },
        Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f),
        FillTo = new Vector2(1f, 0.5f),
        Width = 256, Height = 256,
    };

    public override void _Draw()
    {
        var vp = Size;
        var s = EStage.ScaleFor(vp);
        var off = (vp - UiE.Design * s) / 2f;
        var t = _t;
        // Repère « maquette » : 1280×720 centré, mis à l'échelle ; la zone visible déborde à gauche / à droite.
        var left = -off.X / s; var top = -off.Y / s;
        var right = left + vp.X / s; var bottom = top + vp.Y / s;
        DrawSetTransform(off, 0, new Vector2(s, s));

        // Dégradé radial (ellipse 900×640 centrée en 760,380), au-delà : couleur extérieure.
        DrawRect(new Rect2(left, top, right - left, bottom - top), UiE.BgLo);
        DrawTextureRect(RadialTex, new Rect2(760 - 900, 380 - 640, 1800, 1280), false);

        // Grille 28 px (défile de 28 px en diagonale en 6 s).
        var g = Animated ? (t / 6f % 1f) * 28f : 0f;
        var gc = new Color(110 / 255f, 1, 160 / 255f, 0.10f);
        var lines = new List<Vector2>();
        for (var x = Mathf.Floor(left / 28f) * 28f + g; x < right; x += 28f) { lines.Add(new Vector2(x, top)); lines.Add(new Vector2(x, bottom)); }
        for (var y = Mathf.Floor(top / 28f) * 28f + g; y < bottom; y += 28f) { lines.Add(new Vector2(left, y)); lines.Add(new Vector2(right, y)); }
        DrawMultiline(lines.ToArray(), gc, 1f);

        // Balayage radar (dégradé conique, tour en 7 s).
        if (Animated)
        {
            var a0 = (t / 7f % 1f) * Mathf.Tau - Mathf.Pi / 2f; // conic « from 0deg » = haut
            const int n = 20;
            for (var i = 0; i < n; i++)
            {
                var u0 = (float)i / n; var u1 = (float)(i + 1) / n;
                var ang0 = a0 - Mathf.Pi / 3f + u0 * Mathf.Pi / 3f;
                var ang1 = a0 - Mathf.Pi / 3f + u1 * Mathf.Pi / 3f;
                var c0 = new Color(UiE.Cyan, 0.22f * u0); var c1 = new Color(UiE.Cyan, 0.22f * u1);
                DrawPolygon(new[] { Radar, Radar + Vector2.Right.Rotated(ang0) * 300, Radar + Vector2.Right.Rotated(ang1) * 300 },
                    new[] { new Color(UiE.Cyan, 0.11f * (u0 + u1)), c0, c1 });
            }
        }

        // Anneaux radar.
        DrawArc(Radar, 300, 0, Mathf.Tau, 128, new Color(UiE.Cyan, 0.35f), 2f, true);
        DashedCircle(240, 14f, new[] { 120f, 40f, 60f, 30f }, Animated ? t / 60f * Mathf.Tau : 0f, new Color(UiE.Cyan, 0.2f));
        DashedCircle(196, 3f, new[] { 4f, 10f }, Animated ? -t / 38f * Mathf.Tau : 0f, new Color(UiE.Cyan, 0.4f));
        DrawArc(Radar, 150, 0, Mathf.Tau, 96, new Color(UiE.Cyan, 0.3f), 1.5f, true);

        // Circuits : tracé fixe + impulsions en tirets qui circulent.
        var baseC = new Color(108 / 255f, 1, 154 / 255f, 0.22f);
        foreach (var c in Circuits) DrawPolyline(c, baseC, 1.6f, true);
        foreach (var i in Flow) Dashes(Circuits[i], 18, 12, Animated ? -(t / 2.4f % 1f) * 60f : 0f, new Color(UiE.Mint, 0.55f), 2f);
        foreach (var i in FlowS) Dashes(Circuits[i], 6, 14, Animated ? (t / 1.6f % 1f) * 60f : 0f, new Color(UiE.Mint, 0.5f), 2f);
        DrawMultiline(Stubs, new Color(108 / 255f, 1, 154 / 255f, 0.25f), 5f);

        if (!Animated) { DrawSetTransform(Vector2.Zero); return; }

        // Nœuds qui pulsent (2,2 s).
        foreach (var (p, r, d) in Nodes)
        {
            var k = 0.5f - 0.5f * Mathf.Cos(((t - d) / 2.2f % 1f + 1f) % 1f * Mathf.Tau);
            DrawCircle(p, r * Mathf.Lerp(0.7f, 1.25f, k), new Color(UiE.Mint, Mathf.Lerp(0.25f, 1f, k)), true, -1, true);
        }
        // Voyant qui clignote.
        DrawRect(new Rect2(1236, 296, 10, 10), new Color(UiE.Frame, t % 1.2f < 0.6f ? 1f : 0.2f));

        // Bande de balayage horizontale (de -120 à 740 en 5,5 s).
        var sy = -120f + (t / 5.5f % 1f) * 860f;
        var c0b = new Color(110 / 255f, 1, 160 / 255f, 0f);
        var c1b = new Color(110 / 255f, 1, 160 / 255f, 0.10f);
        var c2b = new Color(170 / 255f, 1, 200 / 255f, 0.28f);
        UiE.VGradient(this, new Rect2(left, sy, right - left, 84), c0b, c1b);
        UiE.VGradient(this, new Rect2(left, sy + 84, right - left, 36), c1b, c2b);

        // Particules qui montent (9 s).
        foreach (var (x, y, d) in Motes)
        {
            var u = ((t - d) / 9f % 1f + 1f) % 1f;
            if (t < d) continue;
            var a = u < 0.15f ? u / 0.15f * 0.9f : u < 0.85f ? Mathf.Lerp(0.9f, 0.6f, (u - 0.15f) / 0.7f) : Mathf.Lerp(0.6f, 0f, (u - 0.85f) / 0.15f);
            var p = new Vector2(x, y - 320f * u);
            DrawCircle(p + new Vector2(1.5f, 1.5f), 5f, new Color(108 / 255f, 1, 154 / 255f, 0.18f * a));
            DrawRect(new Rect2(p, new Vector2(3, 3)), new Color(UiE.Mint, a));
        }
        DrawSetTransform(Vector2.Zero);
    }

    /// <summary>Cercle en tirets (stroke-dasharray SVG : départ à 3 h, sens horaire), tourné de rot.</summary>
    private void DashedCircle(float r, float width, float[] pattern, float rot, Color c)
    {
        var circ = Mathf.Tau * r;
        var pos = 0f; var k = 0;
        var segs = new List<Vector2>();
        while (pos < circ)
        {
            var len = pattern[k % pattern.Length];
            if (k % 2 == 0)
            {
                var end = Mathf.Min(circ, pos + len);
                var a0 = rot + pos / r; var a1 = rot + end / r;
                if (len > 8f) DrawArc(Radar, r, a0, a1, Mathf.Max(3, (int)(len / 6f)), c, width, true);
                else { segs.Add(Radar + Vector2.Right.Rotated(a0) * r); segs.Add(Radar + Vector2.Right.Rotated(a1) * r); }
            }
            pos += len; k++;
        }
        if (segs.Count > 0) DrawMultiline(segs.ToArray(), c, width, true);
    }

    /// <summary>Tirets le long d'une polyligne (stroke-dasharray / dashoffset SVG).</summary>
    private void Dashes(Vector2[] path, float dash, float gap, float offset, Color c, float width)
    {
        var period = dash + gap;
        var start = -(((offset % period) + period) % period);
        var segs = new List<Vector2>();
        var acc = 0f;
        for (var i = 0; i < path.Length - 1; i++)
        {
            var a = path[i]; var b = path[i + 1];
            var len = a.DistanceTo(b);
            var dir = (b - a) / len;
            // Tirets qui recouvrent [acc, acc + len].
            var k0 = Mathf.Floor((acc - start) / period);
            for (var d0 = start + k0 * period; d0 < acc + len; d0 += period)
            {
                var s0 = Mathf.Max(d0, acc); var s1 = Mathf.Min(d0 + dash, acc + len);
                if (s1 <= s0) continue;
                segs.Add(a + dir * (s0 - acc));
                segs.Add(a + dir * (s1 - acc));
            }
            acc += len;
        }
        if (segs.Count > 0) DrawMultiline(segs.ToArray(), c, width, true);
    }
}
