using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Carte inclinée (skewY −2°) des maquettes E : tableau à bordure verte (profil, classe, escouade).
/// Peut être focalisable : ← / → appellent <see cref="OnStep"/>, Entrée / clic sur une ligne appelle <see cref="OnRow"/>.
/// </summary>
public partial class ECard : Control
{
    public record Cell(string Text, float Width = 0, int Size = 17, Color? Color = null);
    public record Row(Cell[] Cells, float Height = 24, bool Separator = false, int Pad = 8);

    public readonly List<Row> Rows = new();
    /// <summary>Colonne gauche séparée par un liseré vertical (largeur de la première colonne, 0 = aucun).</summary>
    public bool ColumnRule = true;
    public float Skew = -2f;
    public Action<int> OnStep;
    public Action<int> OnRow;
    /// <summary>Ligne qui porte les chevrons ‹ › quand la carte a le focus.</summary>
    public int StepRow = -1;
    private bool _hover;

    public ECard()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
    }

    public void Interactive()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += () => { _hover = true; QueueRedraw(); };
        MouseExited += () => { _hover = false; QueueRedraw(); };
        FocusEntered += () => { if (!UiKit.SilentFocus) Sfx.Ui("ui_hover", -12f, 1f, 60); QueueRedraw(); };
        FocusExited += QueueRedraw;
    }

    public void SetRows(IEnumerable<Row> rows)
    {
        Rows.Clear();
        Rows.AddRange(rows);
        var h = 2f;
        foreach (var r in Rows) h += r.Height;
        Size = new Vector2(Size.X, h + 2);
        CustomMinimumSize = new Vector2(CustomMinimumSize.X, h + 2);
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (FocusMode == FocusModeEnum.None) return;
        if (e.IsActionPressed("ui_left", true) && OnStep != null) { OnStep(-1); Sfx.Ui("ui_click", -8f, 1.1f); AcceptEvent(); return; }
        if (e.IsActionPressed("ui_right", true) && OnStep != null) { OnStep(1); Sfx.Ui("ui_click", -8f, 1.1f); AcceptEvent(); return; }
        if (e.IsActionPressed("ui_accept")) { OnRow?.Invoke(StepRow >= 0 ? StepRow : 0); Sfx.Ui("ui_click", -6f); AcceptEvent(); return; }
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
        {
            UiKit.FocusSilently(this);
            var y = 1f; var row = Rows.Count - 1;
            for (var i = 0; i < Rows.Count; i++) { if (mb.Position.Y < y + Rows[i].Height) { row = i; break; } y += Rows[i].Height; }
            if (row == StepRow && OnStep != null) OnStep(mb.Position.X < Size.X * 0.45f ? -1 : 1);
            else OnRow?.Invoke(row);
            Sfx.Ui("ui_click", -6f);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var w = Size.X; var h = Size.Y;
        var k = Mathf.Tan(Mathf.DegToRad(Skew));
        // skewY autour du centre (transform-origin CSS par défaut).
        DrawSetTransformMatrix(new Transform2D(new Vector2(1, k), new Vector2(0, 1), new Vector2(0, -k * w / 2f)));
        var focus = HasFocus();
        DrawRect(new Rect2(0, 0, w, h), UiE.CardBg);
        var y = 1f;
        var font = UiE.Mono;
        var first = 0f;
        for (var ri = 0; ri < Rows.Count; ri++)
        {
            var r = Rows[ri];
            var x = 1f;
            var fixedW = 0f; var flex = 0;
            foreach (var c in r.Cells) { if (c.Width > 0) fixedW += c.Width; else flex++; }
            var flexW = flex > 0 ? (w - 2 - fixedW) / flex : 0;
            for (var ci = 0; ci < r.Cells.Length; ci++)
            {
                var c = r.Cells[ci];
                var cw = c.Width > 0 ? c.Width : flexW;
                if (ci == 0) first = cw;
                if (!string.IsNullOrEmpty(c.Text))
                {
                    var right = c.Text.StartsWith("»");
                    var txt = right ? c.Text[1..] : c.Text;
                    var ty = y + (r.Height + font.GetAscent(c.Size) - font.GetDescent(c.Size)) / 2f;
                    var tx = x + r.Pad;
                    var avail = cw - r.Pad * 2;
                    if (right) tx = x + cw - r.Pad - Mathf.Min(avail, font.GetStringSize(txt, HorizontalAlignment.Left, -1, c.Size).X);
                    DrawStringOutline(font, new Vector2(tx, ty), txt, HorizontalAlignment.Left, avail, c.Size, 3, UiE.Ink);
                    DrawString(font, new Vector2(tx, ty), txt, HorizontalAlignment.Left, avail, c.Size, c.Color ?? UiE.Text);
                }
                x += cw;
            }
            if (focus && ri == StepRow && OnStep != null)
            {
                var cy = y + r.Height / 2f;
                DrawString(UiE.Cond, new Vector2(w - 22, cy + 8), "›", HorizontalAlignment.Left, -1, 26, UiE.Mint);
                DrawString(UiE.Cond, new Vector2(first + 4 - 10 + 2, cy + 8), "‹", HorizontalAlignment.Left, -1, 26, UiE.Mint);
            }
            y += r.Height;
            if (r.Separator) DrawLine(new Vector2(0, y), new Vector2(w, y), UiE.Edge, 1f);
        }
        if (ColumnRule && first > 0)
        {
            var y0 = 1f;
            foreach (var r in Rows) { if (r.Cells.Length > 1) DrawLine(new Vector2(first + 1, y0), new Vector2(first + 1, y0 + r.Height), UiE.Edge, 1f); y0 += r.Height; }
        }
        var bc = focus ? UiE.Mint : _hover ? UiE.Bright : UiE.Frame;
        DrawRect(new Rect2(1, 1, w - 2, h - 2), bc, false, focus ? 3f : 2f);
        if (focus) DrawRect(new Rect2(-3, -3, w + 6, h + 6), new Color(UiE.Frame, 0.35f), false, 2f);
        DrawSetTransform(Vector2.Zero);
    }
}

/// <summary>
/// Soldat 3D (modèle de la classe choisie) rendu dans une vue à fond transparent, à droite des menus :
/// remplace la silhouette SVG des maquettes.
/// </summary>
public partial class SoldierPortrait : TextureRect
{
    private SubViewport _vp;
    private SoldierModel _model;
    private SoldierClass _cls = (SoldierClass)(-1);
    private Node3D _pivot;
    public float Yaw = 14f;

    public SoldierPortrait()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ExpandMode = ExpandModeEnum.IgnoreSize;
        StretchMode = StretchModeEnum.Scale;
    }

    public override void _Ready()
    {
        _vp = new SubViewport
        {
            TransparentBg = true,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = new Vector2I(340, 612),
        };
        AddChild(_vp);
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.75f, 0.9f, 0.8f),
            AmbientLightEnergy = 0.9f,
            TonemapMode = Godot.Environment.ToneMapper.Agx,
        };
        _vp.AddChild(new WorldEnvironment { Environment = env });
        var key = new DirectionalLight3D { LightEnergy = 1.6f, RotationDegrees = new Vector3(-28, -32, 0) };
        _vp.AddChild(key);
        var rim = new DirectionalLight3D { LightEnergy = 1.1f, LightColor = new Color(0.55f, 1f, 0.7f), RotationDegrees = new Vector3(-10, 150, 0) };
        _vp.AddChild(rim);
        _pivot = new Node3D();
        _vp.AddChild(_pivot);
        var cam = new Camera3D { Fov = 30, Current = true };
        _vp.AddChild(cam);
        cam.Position = new Vector3(0, 1.0f, 4.3f);
        cam.LookAt(new Vector3(0, 0.98f, 0), Vector3.Up);
        Texture = _vp.GetTexture();
        Resized += FitViewport;
        GetViewport().SizeChanged += FitViewport;
        FitViewport();
        SetClass(Profile.I.Data.SelectedClass);
    }

    public override void _ExitTree()
    {
        if (GetViewport() != null) GetViewport().SizeChanged -= FitViewport;
    }

    /// <summary>Résolution de rendu = taille affichée réelle (net quelle que soit l'échelle de la scène).</summary>
    private void FitViewport()
    {
        if (_vp == null) return;
        var s = GetGlobalTransformWithCanvas().Scale;
        var px = (Size * s).Round();
        _vp.Size = new Vector2I(Mathf.Clamp((int)px.X, 16, 2048), Mathf.Clamp((int)px.Y, 16, 2048));
    }

    public void SetClass(SoldierClass cls)
    {
        if (cls == _cls) return;
        _cls = cls;
        _model?.QueueFree();
        _model = SoldierModel.Create(cls);
        _pivot.AddChild(_model);
        _model.RotationDegrees = new Vector3(0, Yaw, 0);
        // Arme tenue : la première arme équipée de la classe (pas le cœur plasma).
        if (Profile.I?.Equipped(cls).FirstOrDefault(w => w.T.Spec?.Kind is not (Edf5Kind.Core or Edf5Kind.Gear or Edf5Kind.Shield)) is { } first) _model.SetWeapon(first.T);
        // Fencer : bouclier du premier jeu au bras gauche.
        if (cls == SoldierClass.Heavy && Profile.I?.Equipped(cls).Take(Classes.SetSize).FirstOrDefault(w => w.T.Spec?.Kind == Edf5Kind.Shield) is { } sh)
            _model.SetShield(sh.T.Spec);
    }

    /// <summary>Arme montrée dans les mains du soldat (arme survolée dans l'arsenal).</summary>
    public void SetWeapon(WeaponTemplate t)
    {
        if (t?.Spec?.Kind != Edf5Kind.Core) _model?.SetWeapon(t);
    }

    public override void _Process(double delta)
    {
        _model?.Animate((float)delta, 0, 0f, false, false, false, false, 0);
    }
}
