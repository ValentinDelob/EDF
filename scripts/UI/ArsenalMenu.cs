using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Arsenal (maquette E-Arsenal, ADN EDF) : tableau du haut avec une colonne par type d'arme de la classe
/// (colonnes Lv et ☆), panneau du bas (description et une ligne par caractéristique « (☆n) Nom : valeur (+bonus) »),
/// soldat et carte d'équipement à droite, aides de touches en bas.
/// Clavier / manette : flèches pour parcourir le tableau, Entrée pour équiper dans l'emplacement visé,
/// Q / E (LB / RB) pour changer d'emplacement, Tab (Select) pour changer de classe, Échap pour revenir.
/// Une arme déjà équipée est assombrie, étiquetée « ÉQUIPÉE n » et ne peut pas être équipée une seconde fois.
/// </summary>
public partial class ArsenalMenu : Control
{
    public Action OnBack;

    // Géométrie de la maquette (unités 1280 × 720).
    private const float TableX = 138, TableY = 110, TableW = 772, ColGap = 3;
    private const float HeadH = 30, RowH = 25, RowGap = 2, RowsTop = TableY + HeadH + RowGap, RowsBottom = 296;
    private const float LvW = 26, StarW = 22;

    private sealed class Column
    {
        public string Category;
        public float X, W;
        public Control Clip;
        public readonly List<Row> Rows = new();
        public float Scroll;
    }

    private sealed class Row
    {
        public WeaponInstance W;
        public Column Col;
        public int Index;
        public Control Node;
        public Control NameClip;
        public Control NameInk; // enfant du cadre de découpe : seul le dessin des enfants est rogné
        public bool Overflow;
        /// <summary>Arme nouvelle à l'ouverture de l'écran (l'étiquette reste jusqu'à la prochaine reconstruction).</summary>
        public bool ShowNew;
    }

    private SoldierClass _cls;
    private int _slot;
    private WeaponInstance _selected;
    private ArsenalEBackground _bg;
    private Control _stage;
    private readonly List<Column> _cols = new();
    private readonly List<(Rect2 rect, Action action)> _hints = new();
    private float _k = 1;
    private SoldierPortrait _portrait; // même rendu 3D du personnage que le menu principal et la sélection de mission
    private Vector2 _o;
    private double _t;

    public override void _Ready()
    {
        Theme = UiKit.Theme;
        UiKit.FillRect(this);
        MouseFilter = MouseFilterEnum.Stop;
        _cls = Profile.I.Data.SelectedClass;
        _bg = new ArsenalEBackground();
        AddChild(_bg);
        _portrait = new SoldierPortrait();
        AddChild(_portrait);
        _stage = new Control { MouseFilter = MouseFilterEnum.Stop };
        UiKit.FillRect(_stage);
        _stage.Draw += DrawStage;
        _stage.GuiInput += StageInput;
        AddChild(_stage);
        Resized += Layout;
        App.I?.Background?.SetMode(MenuBackground.Mode.Title);
        Rebuild();
        UiKit.FadeIn(this);
    }

    // ───────────────────────── Construction ─────────────────────────

    private void Rebuild()
    {
        foreach (var c in _cols) c.Clip.QueueFree();
        _cols.Clear();
        var slots = Classes.Get(_cls).WeaponSlots;
        if (_slot >= slots) _slot = 0;
        var cats = Weapons.CategoriesFor(_cls);
        var colW = (TableW - ColGap * (cats.Count - 1)) / Math.Max(1, cats.Count);
        var owned = Profile.I.Data.Arsenal.Where(w => w.T.Class == _cls).ToList();
        for (var i = 0; i < cats.Count; i++)
        {
            var col = new Column { Category = cats[i], X = TableX + i * (colW + ColGap), W = colW };
            col.Clip = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
            var cc = col;
            col.Clip.GuiInput += e => ColumnInput(cc, e);
            AddChild(col.Clip);
            var list = owned.Where(w => w.T.Category == col.Category)
                .OrderByDescending(w => w.Level).ThenByDescending(w => w.TotalStars).ToList();
            for (var j = 0; j < list.Count; j++)
            {
                var row = new Row { W = list[j], Col = col, Index = j, ShowNew = list[j].IsNew };
                row.Node = new Control { FocusMode = FocusModeEnum.All, MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
                row.NameClip = new Control { ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
                var r = row;
                row.Node.Draw += () => DrawRow(r);
                row.NameInk = new Control { MouseFilter = MouseFilterEnum.Ignore };
                row.NameClip.AddChild(row.NameInk);
                row.NameInk.Draw += () => DrawRowName(r);
                row.Node.FocusEntered += () => OnRowFocused(r);
                row.Node.FocusExited += () => r.Node.QueueRedraw();
                row.Node.GuiInput += e => RowInput(r, e);
                row.Node.AddChild(row.NameClip);
                col.Clip.AddChild(row.Node);
                col.Rows.Add(row);
            }
            _cols.Add(col);
        }
        Layout();
        LinkFocus();
        if (_selected != null && _selected.T.Class != _cls) _selected = null;
        var eq = Profile.I.Equipped(_cls);
        var target = FindRow(_selected) ?? FindRow(eq.FirstOrDefault()) ?? _cols.SelectMany(c => c.Rows).FirstOrDefault();
        _selected = target?.W;
        if (target != null) Callable.From(() => UiKit.FocusSilently(target.Node)).CallDeferred();
        _stage.QueueRedraw();
    }

    private Row FindRow(WeaponInstance w) => w == null ? null : _cols.SelectMany(c => c.Rows).FirstOrDefault(r => r.W == w);

    /// <summary>Voisins de focus explicites : haut / bas dans la colonne, gauche / droite vers la colonne non vide voisine.</summary>
    private void LinkFocus()
    {
        var filled = _cols.Where(c => c.Rows.Count > 0).ToList();
        for (var ci = 0; ci < filled.Count; ci++)
        {
            var col = filled[ci];
            for (var i = 0; i < col.Rows.Count; i++)
            {
                var n = col.Rows[i].Node;
                n.FocusNeighborTop = n.GetPathTo(col.Rows[Math.Max(0, i - 1)].Node);
                n.FocusNeighborBottom = n.GetPathTo(col.Rows[Math.Min(col.Rows.Count - 1, i + 1)].Node);
                var left = filled[(ci - 1 + filled.Count) % filled.Count];
                var right = filled[(ci + 1) % filled.Count];
                n.FocusNeighborLeft = n.GetPathTo(left.Rows[Math.Min(i, left.Rows.Count - 1)].Node);
                n.FocusNeighborRight = n.GetPathTo(right.Rows[Math.Min(i, right.Rows.Count - 1)].Node);
                n.FocusNext = n.FocusNeighborRight;
                n.FocusPrevious = n.FocusNeighborLeft;
            }
        }
    }

    /// <summary>Échelle et origine : la maquette 1280 × 720 est centrée et agrandie pour tenir dans l'écran.</summary>
    private void Layout()
    {
        _k = Mathf.Min(Size.X / EStyle.DesignW, Size.Y / EStyle.DesignH);
        if (_k <= 0) _k = 1;
        _o = ((Size - new Vector2(EStyle.DesignW, EStyle.DesignH) * _k) / 2f).Floor();
        _portrait.Position = _o + new Vector2(944, 96) * _k;
        _portrait.Size = new Vector2(300, 540) * _k;
        _portrait.SetClass(_cls);
        foreach (var col in _cols)
        {
            col.Clip.Position = _o + new Vector2(col.X, RowsTop) * _k;
            col.Clip.Size = new Vector2(col.W, RowsBottom - RowsTop) * _k;
            foreach (var r in col.Rows)
            {
                r.Node.Position = new Vector2(0, r.Index * (RowH + RowGap) - col.Scroll) * _k;
                r.Node.Size = new Vector2(col.W, RowH) * _k;
                var nameW = NameWidth(r);
                r.NameClip.Position = Vector2.Zero;
                r.NameClip.Size = new Vector2(nameW, RowH) * _k;
                r.NameInk.Size = r.NameClip.Size;
                r.Overflow = new Pen(this, _k, Vector2.Zero).Measure(EStyle.Cond, r.W.T.Name, 14) > nameW - 10;
                r.Node.QueueRedraw();
                r.NameInk.QueueRedraw();
            }
        }
        _stage.QueueRedraw();
    }

    private int EquippedSlot(WeaponInstance w) => Profile.I.Equipped(_cls).IndexOf(w);

    /// <summary>Place réservée à droite du nom : étiquette « ÉQUIPÉE n » ou « NEW » (fixes, hors du bandeau défilant).</summary>
    private float TagWidth(Row r) => EquippedSlot(r.W) >= 0 ? 52 : r.ShowNew ? 26 : 0;

    private float NameWidth(Row r) => r.Col.W - LvW - StarW - TagWidth(r);

    // ───────────────────────── Interaction ─────────────────────────

    private void OnRowFocused(Row r)
    {
        var changed = _selected != r.W;
        _selected = r.W;
        EnsureVisible(r);
        if (changed) Sfx.Ui("ui_hover", -12f, 1f, 60);
        if (r.W.IsNew)
        {
            // L'étiquette NEW disparaît à la prochaine reconstruction (la ligne reste lisible pendant la consultation).
            r.W.IsNew = false;
            Profile.I.Save();
        }
        foreach (var c in _cols) foreach (var x in c.Rows) x.Node.QueueRedraw();
        _stage.QueueRedraw();
    }

    private void EnsureVisible(Row r)
    {
        var col = r.Col;
        var view = RowsBottom - RowsTop;
        var top = r.Index * (RowH + RowGap);
        var old = col.Scroll;
        if (top < col.Scroll) col.Scroll = top;
        else if (top + RowH > col.Scroll + view) col.Scroll = top + RowH - view;
        if (!Mathf.IsEqualApprox(old, col.Scroll)) Layout();
    }

    private void ColumnInput(Column col, InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true } mb) return;
        var dir = mb.ButtonIndex == MouseButton.WheelDown ? 1 : mb.ButtonIndex == MouseButton.WheelUp ? -1 : 0;
        if (dir == 0) return;
        var view = RowsBottom - RowsTop;
        var max = Mathf.Max(0, col.Rows.Count * (RowH + RowGap) - RowGap - view);
        col.Scroll = Mathf.Clamp(col.Scroll + dir * (RowH + RowGap), 0, max);
        Layout();
        col.Clip.AcceptEvent();
    }

    private void RowInput(Row r, InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
        {
            // Premier clic : sélection ; clic sur la ligne déjà sélectionnée (ou double-clic) : équiper.
            if (r.Node.HasFocus() || mb.DoubleClick) Equip();
            else r.Node.GrabFocus();
            r.Node.AcceptEvent();
        }
        else if (e is InputEventMouseButton { Pressed: true } wheel && wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            ColumnInput(r.Col, e);
        else if (e.IsActionPressed("ui_accept"))
        {
            Equip();
            r.Node.AcceptEvent();
        }
    }

    public override void _Input(InputEvent e)
    {
        // Tab / Select : classe suivante (avant la navigation de focus de Godot, qui utilise aussi Tab).
        var dir = 0;
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Tab } k) dir = k.ShiftPressed ? -1 : 1;
        else if (e is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Back }) dir = 1;
        if (dir == 0) return;
        ChangeClass(dir);
        GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { Back(); GetViewport().SetInputAsHandled(); return; }
        var dir = 0;
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.PhysicalKeycode == Key.Q) dir = -1;
            else if (k.PhysicalKeycode == Key.E) dir = 1;
        }
        else if (e is InputEventJoypadButton { Pressed: true } j)
        {
            if (j.ButtonIndex == JoyButton.LeftShoulder) dir = -1;
            else if (j.ButtonIndex == JoyButton.RightShoulder) dir = 1;
        }
        if (dir == 0) return;
        ChangeSlot(dir);
        GetViewport().SetInputAsHandled();
    }

    private void StageInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) return;
        var pos = (mb.Position - _o) / _k;
        foreach (var (rect, action) in _hints)
            if (rect.HasPoint(pos)) { action(); _stage.AcceptEvent(); return; }
    }

    private void Back()
    {
        Sfx.Ui("ui_click", -8f, 0.9f);
        OnBack?.Invoke();
    }

    private void ChangeSlot(int dir)
    {
        var slots = Classes.Get(_cls).WeaponSlots;
        _slot = (_slot + dir + slots) % slots;
        Sfx.Ui("ui_click", -8f, 1.1f);
        _stage.QueueRedraw();
    }

    private void ChangeClass(int dir)
    {
        var n = Classes.All.Length;
        _cls = (SoldierClass)(((int)_cls + dir + n) % n);
        _portrait.SetClass(_cls);
        _selected = null;
        _slot = 0;
        Sfx.Ui("ui_click", -8f, 1f);
        Rebuild();
    }

    private void Equip()
    {
        var w = _selected;
        if (w == null) return;
        if (EquippedSlot(w) >= 0)
        {
            // Déjà équipée (dans cet emplacement ou l'autre) : non équipable une seconde fois.
            Sfx.Ui("empty", -6f);
            return;
        }
        Profile.I.Equip(_cls, _slot, w);
        Sfx.Ui("pickup", -6f, 1.2f);
        Layout();
        foreach (var c in _cols) foreach (var x in c.Rows) { x.Node.QueueRedraw(); x.NameInk.QueueRedraw(); }
    }

    public override void _Process(double delta)
    {
        _t += delta;
        // Défilement en boucle des noms trop longs.
        foreach (var c in _cols)
            foreach (var r in c.Rows)
                if (r.Overflow) r.NameInk.QueueRedraw();
    }

    // ───────────────────────── Dessin ─────────────────────────

    private static string ClassTitle(SoldierClass c) => c switch
    {
        SoldierClass.Rifleman => "Arsenal du fusilier",
        SoldierClass.Airborne => "Arsenal du commando",
        SoldierClass.Support => "Arsenal de l'opérateur",
        SoldierClass.Heavy => "Arsenal du grenadier",
        _ => "Arsenal",
    };

    private void DrawStage()
    {
        var p = new Pen(_stage, _k, _o);
        _hints.Clear();

        // Bandeau de titre.
        p.HexBand(260, 34, 760, 44, 20, (0f, new Color("B9EECF")), (0.48f, new Color("7ED5A2")), (0.52f, new Color("5CB681")), (1f, new Color("85D8A8")));
        p.HexBand(272, 40, 736, 32, 14, (0f, new Color("255137")), (1f, new Color("163021")));
        // Titre seul, centré dans le bandeau (« Choix des armes » retiré à la demande).
        var title = ClassTitle(_cls);
        var titleW = p.Measure(EStyle.CondBold, title, 24);
        p.Text(EStyle.CondBold, 640 - titleW / 2, 40, 32, title, 24, EStyle.White, outline: true);

        // Cadre du tableau.
        p.Poly(Pen.Chamfer(126, 94, 796, 206, 12), new Color(13 / 255f, 31 / 255f, 21 / 255f, 0.72f));
        p.Outline(Pen.Chamfer(126, 94, 796, 206, 12), EStyle.Frame, 2.4f);
        p.Outline(Pen.Chamfer(132, 100, 784, 194, 12), EStyle.FrameInner, 1f);

        // En-têtes de colonnes (type d'arme, Lv, ☆).
        var selCol = FindRow(_selected)?.Col;
        foreach (var col in _cols)
        {
            var active = col == selCol;
            p.VGrad(col.X, TableY, col.W, HeadH, new Color("2A563C"), new Color("173222"));
            p.Rect(col.X, TableY + HeadH - 2, col.W, 2, active ? EStyle.Frame : EStyle.FrameInner);
            var nameW = col.W - LvW - StarW - 8;
            var title2 = Weapons.CategoryTitle(col.Category);
            var fs = 15f;
            while (fs > 11 && p.Measure(EStyle.CondBold, title2, fs) > nameW) fs -= 0.5f;
            p.Text(EStyle.CondBold, col.X + 8, TableY, HeadH - 2, title2, fs, active ? EStyle.White : EStyle.Text);
            var lx = col.X + col.W - LvW - StarW;
            p.Rect(lx, TableY + 4, 1, HeadH - 10, EStyle.FrameInner);
            p.Rect(lx + LvW, TableY + 4, 1, HeadH - 10, EStyle.FrameInner);
            p.Text(EStyle.Cond, lx, TableY, HeadH - 2, "Lv", 14, EStyle.Text, HorizontalAlignment.Center, LvW);
            p.Text(EStyle.Cond, lx + LvW, TableY, HeadH - 2, "☆", 14, EStyle.Text, HorizontalAlignment.Center, StarW);
            if (col.Rows.Count == 0)
                p.Text(EStyle.Cond, col.X + 8, RowsTop, RowH, "Aucune arme", 13, EStyle.DimText);
            // Indicateur de défilement quand la colonne contient plus d'armes que la hauteur visible.
            var view = RowsBottom - RowsTop;
            var total = col.Rows.Count * (RowH + RowGap) - RowGap;
            if (total > view + 1)
            {
                var frac = view / total;
                var y = RowsTop + col.Scroll / total * view;
                p.Rect(col.X + col.W + 0.5f, y, 2, view * frac, new Color(EStyle.Bright, 0.7f));
            }
        }

        // Flèche orange entre les deux cadres.
        p.Poly(new[] { new Vector2(540, 310), new Vector2(572, 310), new Vector2(556, 324) }, EStyle.Orange);

        DrawDetail(p);
        DrawLoadout(p);
        DrawHints(p);
    }

    private void DrawDetail(Pen p)
    {
        // Cadre inférieur scindé.
        p.Poly(Pen.Chamfer(162, 332, 756, 310, 12), new Color(7 / 255f, 13 / 255f, 9 / 255f, 0.86f));
        p.Outline(Pen.Chamfer(162, 332, 756, 310, 12), EStyle.Frame, 2.4f);
        p.Outline(Pen.Chamfer(168, 338, 744, 298, 12), EStyle.FrameInner, 1f);
        p.Rect(178, 344, 6, 190, new Color("13F772"));
        p.Rect(178, 534, 6, 96, new Color("4E9BD6"));
        p.Rect(547, 344, 2, 286, EStyle.FrameInner);
        p.Rect(190, 344, 350, 30, new Color("20442F"));
        p.Rect(556, 344, 350, 30, new Color("20442F"));

        var w = _selected;
        if (w == null)
        {
            p.Text(EStyle.Cond, 204, 344, 30, "Aucune arme pour cette classe", 19, EStyle.Text);
            p.Text(EStyle.Cond, 204, 384, 23, "Les caisses rouges trouvées en mission contiennent des armes.", 16, EStyle.Text);
            return;
        }
        var t = w.T;
        p.Text(EStyle.Cond, 204, 344, 30, t.Name, 19, EStyle.White);
        p.Text(EStyle.Cond, 556, 344, 30, $"Niveau d'arme : {w.Level}", 18, EStyle.White, HorizontalAlignment.Center, 350);

        // Description (à gauche).
        var starter = Weapons.StarterLoadout.TryGetValue(t.Class, out var ids) && ids.Contains(t.Id) && w.Level == 0;
        var paragraphs = new[]
        {
            $"{t.Category}. {Weapons.Usage(t)}",
            "Chaque étoile d'une caractéristique lui ajoute un bonus, indiqué entre parenthèses.",
            (starter ? "Équipement de départ." : "Récompense de caisse d'arme.") + $" Niveau {w.Level}.",
        };
        var y = 384f;
        foreach (var para in paragraphs)
        {
            foreach (var line in p.Wrap(EStyle.Cond, para, 16, 336))
            {
                if (y > 606) break;
                p.Text(EStyle.Cond, 204, y, 23, line, 16, EStyle.Text);
                y += 23;
            }
            y += 12;
        }

        // Caractéristiques (à droite) : (☆n) Nom : valeur (+bonus).
        y = 382f;
        foreach (var s in Enum.GetValues<WeaponStat>())
        {
            if (!w.HasStat(s)) continue;
            var (value, bonus) = w.Describe(s);
            var n = w.StarsOf(s);
            p.Text(EStyle.Mono, 556, y, 22, $"(☆{n})", 16, EStyle.Gold);
            p.Text(EStyle.Mono, 612, y, 22, $"{WeaponInstance.StatNames[(int)s]} :", 16, EStyle.White);
            var vw = p.Text(EStyle.Mono, 746, y, 22, value, 16, EStyle.Cyan);
            if (n > 0) p.Text(EStyle.Mono, 746 + vw + 8, y, 22, $"({bonus})", 16, EStyle.Bonus);
            y += 26;
        }
        if (t.BlastRadius > 0)
        {
            p.Text(EStyle.Mono, 612, y, 22, "Rayon :", 16, EStyle.White);
            p.Text(EStyle.Mono, 746, y, 22, $"{w.BlastRadius:0.0} m".Replace('.', ','), 16, EStyle.Cyan);
            y += 26;
        }
        p.Text(EStyle.Mono, 612, y, 22, "Type :", 16, EStyle.White);
        var fs = 16f;
        while (fs > 11 && p.Measure(EStyle.Mono, t.Category, fs) > 900 - 746) fs -= 0.5f;
        p.Text(EStyle.Mono, 746, y, 22, t.Category, fs, EStyle.Cyan);
    }

    private void DrawLoadout(Pen p)
    {
        var def = Classes.Get(_cls);
        var eq = Profile.I.Equipped(_cls);
        const float x = 940, w = 290;
        var y = 410f;
        p.HGrad(x, y, w, 30, new Color(97 / 255f, 163 / 255f, 125 / 255f, 0.85f), new Color(74 / 255f, 120 / 255f, 93 / 255f, 0.55f));
        p.Text(EStyle.Cond, x + 12, y, 30, "Classe", 17, EStyle.White, outline: true);
        p.Text(EStyle.Cond, x + 102, y, 30, def.Name, 17, EStyle.White, outline: true);
        y += 33;
        var panel = new Color(24 / 255f, 44 / 255f, 32 / 255f, 0.85f);
        p.Rect(x, y, w, 28, panel);
        p.Text(EStyle.Cond, x + 12, y, 28, "Armure", 16, EStyle.Text);
        p.Text(EStyle.Mono, x + 102, y, 28, Profile.I.Armor(_cls).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' '), 17, EStyle.White);
        y += 31;

        // Emplacements : celui visé par « Équiper » (Q / E) est mis en évidence.
        var slots = def.WeaponSlots;
        var blockH = 12 + 24 * slots;
        p.Rect(x, y, w, blockH, panel);
        for (var i = 0; i < slots; i++)
        {
            var ry = y + 6 + 24 * i;
            var target = i == _slot;
            if (target)
            {
                p.HGrad(x + 4, ry, w - 8, 24, new Color("16CC62", 0.55f), new Color("09893E", 0.25f));
                p.Rect(x + 4, ry, 3, 24, EStyle.Bright);
            }
            var occupant = i < eq.Count ? eq[i] : null;
            p.Text(EStyle.Mono, x + 12, ry, 24, $"{i + 1}", 13, target ? EStyle.Bright : EStyle.DimText);
            var name = occupant?.T.Name ?? "Vide";
            p.Text(EStyle.Cond, x + 28, ry, 24, name, 16, occupant != null ? EStyle.White : EStyle.DimText);
            if (occupant != null) p.Text(EStyle.Mono, x + w - 12 - 60, ry, 24, $"Lv{occupant.Level}", 15, EStyle.White, HorizontalAlignment.Right, 60);
        }
        y += blockH + 6;
        p.Rect(x, y, w, 34, panel);
        p.Text(EStyle.Cond, x + 12, y + 2, 30, def.AbilityName, 16, EStyle.White);
        p.Text(EStyle.Mono, x + w - 12 - 60, y + 2, 30, "Équip.", 15, new Color("B8E9CC"), HorizontalAlignment.Right, 60);
    }

    private void DrawHints(Pen p)
    {
        p.HexBand(230, 652, 820, 40, 20, (0f, new Color("85D8A8")), (0.5f, new Color("479969")), (1f, new Color("34754F")));
        p.HexBand(242, 656, 796, 32, 14, (0f, new Color("1F3F2C")), (1f, new Color("102519")));

        var canEquip = _selected != null && EquippedSlot(_selected) < 0;
        var items = new List<(string key, string label, Action act, bool enabled)>
        {
            ("Entrée", "Équiper", Equip, canEquip),
            ("Échap", "Retour", Back, true),
        };
        if (Classes.Get(_cls).WeaponSlots > 1) items.Add(("Q / E", $"Emplacement {_slot + 1}", () => ChangeSlot(1), true));
        items.Add(("Tab", "Classe", () => ChangeClass(1), true));

        const float gap = 22, keyPad = 5, keyGap = 7;
        var widths = items.Select(it => p.Measure(EStyle.Cond, it.key, 12) + keyPad * 2 + keyGap + p.Measure(EStyle.Cond, it.label, 16)).ToList();
        var total = widths.Sum() + gap * (items.Count - 1);
        var cx = 242 + (796 - total) / 2f;
        for (var i = 0; i < items.Count; i++)
        {
            var (key, label, act, enabled) = items[i];
            var a = enabled ? 1f : 0.4f;
            var kw = p.Measure(EStyle.Cond, key, 12) + keyPad * 2;
            p.Inset(cx, 663, kw, 18, new Color(EStyle.Text, a), 1);
            p.Text(EStyle.Cond, cx, 663, 18, key, 12, new Color(EStyle.Text, a), HorizontalAlignment.Center, kw);
            p.Text(EStyle.Cond, cx + kw + keyGap, 650, 44, label, 16, new Color(EStyle.Text, a));
            _hints.Add((new Rect2(cx - 4, 652, widths[i] + 8, 40), act));
            cx += widths[i] + gap;
        }
    }

    private void DrawRow(Row r)
    {
        var p = new Pen(r.Node, _k, Vector2.Zero);
        var w = r.Col.W;
        var focused = r.Node.HasFocus() || (_selected == r.W && GetViewport()?.GuiGetFocusOwner() == null);
        var slot = EquippedSlot(r.W);
        var equipped = slot >= 0;
        if (equipped)
        {
            p.Rect(0, 0, w, RowH, new Color(2 / 255f, 10 / 255f, 5 / 255f, 0.88f));
            p.Inset(0, 0, w, RowH, focused ? EStyle.Bright : new Color("1E3A2A"), focused ? 2 : 1);
        }
        else if (focused)
        {
            p.VGrad(0, 0, w, RowH, new Color("16CC62"), new Color("09893E"));
            p.Inset(0, 0, w, RowH, EStyle.Bright, 1);
        }
        else p.Rect(0, 0, w, RowH, new Color(32 / 255f, 68 / 255f, 47 / 255f, 0.75f));

        var txt = equipped ? (focused ? new Color("A9C9B3") : EStyle.DimText) : EStyle.White;
        var lx = w - LvW - StarW;
        p.Text(EStyle.Mono, lx, 0, RowH, $"{r.W.Level}", 14, txt, HorizontalAlignment.Center, LvW);
        p.Text(EStyle.Mono, lx + LvW, 0, RowH, $"{r.W.TotalStars}", 14, txt, HorizontalAlignment.Center, StarW);
        if (equipped)
        {
            var tag = $"ÉQUIPÉE {slot + 1}";
            var tw = p.Measure(EStyle.CondBold, tag, 9) + 8;
            var tx = lx - 4 - tw;
            p.Rect(tx, (RowH - 13) / 2f, tw, 13, EStyle.Bright);
            p.Text(EStyle.CondBold, tx, (RowH - 13) / 2f, 13, tag, 9, EStyle.Ink, HorizontalAlignment.Center, tw);
        }
        else if (r.ShowNew)
            p.Text(EStyle.CondBold, lx - TagWidth(r), 0, RowH, "NEW", 9, EStyle.Gold, HorizontalAlignment.Center, TagWidth(r) - 4);
    }

    private void DrawRowName(Row r)
    {
        var p = new Pen(r.NameInk, _k, Vector2.Zero);
        var focused = r.Node.HasFocus();
        var equipped = EquippedSlot(r.W) >= 0;
        var color = equipped ? (focused ? new Color("A9C9B3") : EStyle.DimText) : EStyle.White;
        var name = r.W.T.Name;
        if (!r.Overflow)
        {
            p.Text(EStyle.Cond, 8, 0, RowH, name, 14, color);
            return;
        }
        // Bandeau en boucle (maquette : pause 18 % puis défilement d'une longueur sur 7 s).
        var span = p.Measure(EStyle.Cond, name, 14) + 24;
        var ph = (float)(_t % 7.0 / 7.0);
        var off = ph < 0.18f ? 0f : (ph - 0.18f) / 0.82f * span;
        for (var k = 0; k < 2; k++) p.Text(EStyle.Cond, 8 - off + k * span, 0, RowH, name, 14, color);
    }
}
