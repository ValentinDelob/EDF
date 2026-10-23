using System;
using Godot;

namespace Rempart;

/// <summary>
/// Plaque commune de la direction E (menus, missions, difficulté, pause, bilan) : verre vert sombre à coins coupés
/// (14 px en haut à droite et en bas à gauche), bloc icône / numéro de 50 px, titre en capitales Saira 22 px.
/// Survol : s'éclaircit et glisse de 6 px. Sélection (focus clavier / manette, ou <see cref="Lit"/>) : vert vif,
/// texte foncé, décalage de 16 px, halo, reflet qui la parcourt et flèche animée à gauche. Variante <see cref="Danger"/>.
/// </summary>
public partial class EPlate : Button
{
    public string Title = "";
    public string Number;            // remplace l'icône (« 01 », « 3 »)
    public UiE.Icon Icon = UiE.Icon.Diamond;
    public string Value;              // valeur à droite (statistiques du bilan)
    public bool Check;                // coche « mission terminée »
    public bool Danger;
    public bool Static;               // plaque d'affichage (non interactive)
    public bool Dim;                  // légèrement atténuée (Quitter)
    private bool _lit;
    /// <summary>Allumée en permanence (élément choisi même sans le focus).</summary>
    public bool Lit { get => _lit; set { _lit = value; QueueRedraw(); } }

    private float _slide;
    private bool _hover;
    private const float Cut = 14f;
    private const float Block = 50f;

    public EPlate() { }

    public EPlate(string title, Action onClick, float width = 310, float height = 50)
    {
        Title = title;
        CustomMinimumSize = new Vector2(width, height);
        Size = CustomMinimumSize;
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin; // largeur fixe dans les colonnes
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        var empty = new StyleBoxEmpty();
        foreach (var k in new[] { "normal", "hover", "pressed", "focus", "disabled", "hover_pressed" }) AddThemeStyleboxOverride(k, empty);
        Pressed += () =>
        {
            Sfx.Ui("ui_click", -6f);
            onClick?.Invoke();
        };
        MouseEntered += () =>
        {
            _hover = true;
            if (!Disabled && !Static) Sfx.Ui("ui_hover", -12f, 1f, 60);
        };
        MouseExited += () => _hover = false;
        FocusEntered += () => { if (!UiKit.SilentFocus) Sfx.Ui("ui_hover", -12f, 1f, 60); };
    }

    /// <summary>Plaque d'affichage (statistiques) : ni focus ni clic.</summary>
    public static EPlate Display(string title, string value, bool lit, float width = 330)
    {
        var p = new EPlate(title, null, width) { Value = value, Static = true, FocusMode = FocusModeEnum.None, MouseFilter = MouseFilterEnum.Ignore };
        p.Lit = lit;
        p.MouseDefaultCursorShape = CursorShape.Arrow;
        return p;
    }


    public bool On => _lit || (HasFocus() && !Static);

    public override void _Process(double delta)
    {
        var target = On ? 16f : (_hover && !Disabled && !Static) ? 6f : 0f;
        if (UiE.Calm) _slide = target;
        else _slide = Mathf.MoveToward(_slide, target, (float)delta * 110f);
        // Redessin continu seulement pour les animations (reflet, flèche, glissement).
        if (On || Mathf.Abs(_slide - target) > 0.01f) QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what is (int)NotificationFocusEnter or (int)NotificationFocusExit or (int)NotificationMouseEnter or (int)NotificationMouseExit or (int)NotificationResized)
            QueueRedraw();
    }

    public override void _Draw()
    {
        var w = Size.X; var h = Size.Y;
        var on = On;
        var hot = _hover && !Disabled && !Static;
        var t = Time.GetTicksMsec() / 1000.0;

        // Flèche animée (hors du décalage, dans l'espace libéré à gauche).
        if (on && !Static)
        {
            var ph = UiE.Calm ? 0f : (float)(t % 1.0);
            var k = 0.5f - 0.5f * Mathf.Cos(ph * Mathf.Tau);
            var py = (h - 18) / 2f;
            DrawColoredPolygon(new[] { new Vector2(-4 + 4 * k, py), new Vector2(8 + 4 * k, py + 9), new Vector2(-4 + 4 * k, py + 18) }, new Color(UiE.Mint, 1f - 0.4f * k));
        }

        DrawSetTransform(new Vector2(_slide, 0));
        var shape = UiE.CutCorners(new Rect2(0, 0, w, h), Cut);
        var alpha = Disabled ? 0.45f : Dim && !on ? 0.75f : 1f;
        Color A(Color c) => new(c, c.A * alpha);

        // Halo de la plaque sélectionnée.
        if (on)
        {
            for (var i = 1; i <= 4; i++)
            {
                var g = UiE.CutCorners(new Rect2(-i * 2, -i * 2, w + i * 4, h + i * 4), Cut + i);
                var loop = new Vector2[g.Length + 1];
                g.CopyTo(loop, 0); loop[^1] = g[0];
                DrawPolyline(loop, new Color(0.27f, 0.91f, 0.42f, 0.16f / i), 3f, true);
            }
        }

        // Fond (dégradé horizontal).
        (float, Color)[] stops;
        if (on)
            stops = hot
                ? new[] { (0f, new Color("b4ffc4")), (w * 0.55f, new Color("5af07c")), (w, new Color("33c653")) }
                : new[] { (0f, new Color("9cffb0")), (w * 0.55f, new Color("46e86b")), (w, new Color("2ab84f")) };
        else if (hot && Danger)
            stops = new[] { (0f, new Color(60 / 255f, 12 / 255f, 8 / 255f, 0.95f)), (w, new Color(90 / 255f, 20 / 255f, 14 / 255f, 0.7f)) };
        else if (hot)
            stops = new[] { (0f, new Color(10 / 255f, 60 / 255f, 26 / 255f, 0.95f)), (w, new Color(14 / 255f, 80 / 255f, 34 / 255f, 0.7f)) };
        else
            stops = new[] { (0f, new Color(3 / 255f, 22 / 255f, 10 / 255f, 0.9f)), (w, new Color(6 / 255f, 40 / 255f, 18 / 255f, 0.66f)) };
        for (var i = 0; i < stops.Length; i++) stops[i].Item2 = A(stops[i].Item2);
        UiE.HGradient(this, shape, 0, w, stops);

        // Bloc icône / numéro.
        var block = new[] { new Vector2(0, 0), new Vector2(Block, 0), new Vector2(Block, h), new Vector2(Cut, h), new Vector2(0, h - Cut) };
        var blockCol = on ? UiE.Ink : Danger ? new Color(232 / 255f, 67 / 255f, 46 / 255f, 0.18f) : new Color(70 / 255f, 232 / 255f, 107 / 255f, 0.13f);
        DrawColoredPolygon(block, A(blockCol));
        if (!on) DrawLine(new Vector2(Block - 0.5f, 0), new Vector2(Block - 0.5f, h), A(Danger ? new Color(1, 140 / 255f, 122 / 255f, 0.4f) : new Color(124 / 255f, 1, 142 / 255f, 0.3f)), 1f);
        var center = new Vector2(Block / 2f, h / 2f);
        if (Number != null)
        {
            var f = UiE.Mono;
            var sz = f.GetStringSize(Number, HorizontalAlignment.Left, -1, 20);
            DrawString(f, new Vector2(center.X - sz.X / 2f, center.Y + f.GetAscent(20) / 2f - 3), Number, HorizontalAlignment.Left, -1, 20, A(on ? UiE.Mint : UiE.Lime));
        }
        else UiE.DrawIcon(this, Icon, center, A(Danger ? UiE.Danger : UiE.Mint));

        // Contour intérieur 1 px (hors sélection).
        if (!on)
        {
            var loop = new Vector2[shape.Length + 1];
            var inset = UiE.CutCorners(new Rect2(0.5f, 0.5f, w - 1, h - 1), Cut);
            inset.CopyTo(loop, 0); loop[^1] = inset[0];
            var bc = Danger && hot ? new Color(1, 140 / 255f, 122 / 255f, 0.7f) : hot ? new Color(156 / 255f, 1, 176 / 255f, 0.7f) : new Color(124 / 255f, 1, 142 / 255f, 0.32f);
            DrawPolyline(loop, A(bc), 1f);
        }

        // Titre (capitales, Saira 22 gras).
        var right = w - 14f;
        if (Check) right -= 26f;
        string val = Value;
        if (val != null)
        {
            var vf = UiE.Mono;
            var vs = vf.GetStringSize(val, HorizontalAlignment.Left, -1, 20);
            var vx = right - vs.X;
            var vy = h / 2f + vf.GetAscent(20) / 2f - 3;
            if (!on) DrawStringOutline(vf, new Vector2(vx, vy), val, HorizontalAlignment.Left, -1, 20, 3, A(UiE.Ink));
            DrawString(vf, new Vector2(vx, vy), val, HorizontalAlignment.Left, -1, 20, A(on ? UiE.Ink : UiE.Text));
            right = vx - 10f;
        }
        var title = Title.ToUpperInvariant();
        var font = UiE.Cond;
        var size = 22;
        var avail = right - (Block + 12f);
        var ts = font.GetStringSize(title, HorizontalAlignment.Left, -1, size);
        while (ts.X > avail && size > 14) { size--; ts = font.GetStringSize(title, HorizontalAlignment.Left, -1, size); }
        var ty = h / 2f + (font.GetAscent(size) - font.GetDescent(size)) / 2f;
        var tc = on ? UiE.Ink : Danger ? new Color("ffb4a8") : new Color("e8ffee");
        if (!on) DrawStringOutline(font, new Vector2(Block + 12f, ty), title, HorizontalAlignment.Left, avail, size, 3, A(new Color(UiE.Ink, 0.8f)));
        DrawString(font, new Vector2(Block + 12f, ty), title, HorizontalAlignment.Left, avail, size, A(on ? tc : Colors.White));

        if (Check) UiE.DrawIcon(this, UiE.Icon.Check, new Vector2(w - 14f - 9f, h / 2f), A(on ? UiE.Ink : UiE.Lime));

        // Reflet lumineux qui parcourt la plaque sélectionnée (3 s : traversée sur 55 %, puis pause).
        if (on && !UiE.Calm)
        {
            var p = (float)(t % 3.0) / 3f;
            var u = Mathf.Min(1f, p / 0.55f);
            u = u * u * (3 - 2 * u);
            var gx = Mathf.Lerp(-90f, w * 1.1f, u);
            if (p < 0.55f)
                UiE.HGradient(this, shape, gx, gx + 70, new[] { (gx, new Color(1, 1, 1, 0)), (gx + 35, new Color(1, 1, 1, 0.5f)), (gx + 70, new Color(1, 1, 1, 0)) });
        }
        DrawSetTransform(Vector2.Zero);
    }
}
