using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Arsenal : onglets de classe, liste d'armes à gauche, fiche et comparaison à droite.
/// Parcourir la liste au clavier / à la manette met la fiche à jour ; l'emplacement et l'atelier
/// (amélioration, recyclage) sont dans la fiche ; une seule action principale « Équiper » en bas à droite.
/// </summary>
public partial class ArsenalMenu : MenuScreen
{
    public Action OnBack;
    private SoldierClass _cls;
    private VBoxContainer _list, _detail;
    private WeaponInstance _selected;
    private int _target = -1;
    private Button _equip;
    private readonly Dictionary<WeaponInstance, (Button row, Label name, Label isNew)> _rows = new();

    public override void _Ready()
    {
        BuildFrame("Arsenal", 0.9f);
        _cls = Profile.I.Data.SelectedClass;
        foreach (var cd in Classes.All)
        {
            var cls = cd.Id;
            var b = AddTab(cd.Name, () => { _cls = cls; _selected = null; _target = -1; Rebuild(); });
            b.SetPressedNoSignal(cls == _cls);
        }
        UpdateCredits();
        HeaderRight.AddThemeColorOverride("font_color", UiKit.Gold);
        HeaderRight.AddThemeFontOverride("font", UiKit.BodyMedium);
        HeaderRight.AddThemeFontSizeOverride("font_size", 16);

        var row = UiKit.HBox(48);
        UiKit.FillRect(row);
        row.MouseFilter = MouseFilterEnum.Ignore;
        Body.AddChild(row);
        var scroll = Scroll(440);
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsStretchRatio = 0.8f;
        _list = UiKit.VBox(2);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);
        row.AddChild(scroll);
        // Fiche défilante aussi : à 1280×720 l'emplacement et l'atelier passent sous le pli.
        var detailScroll = Scroll(616);
        detailScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _detail = UiKit.VBox(8);
        _detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var pad = UiKit.Margin(0, 0, 16, 0);
        pad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pad.AddChild(_detail);
        detailScroll.AddChild(pad);
        row.AddChild(detailScroll);

        FooterLeft.AddChild(UiKit.KeyHint("Échap", "Retour", () => OnBack?.Invoke()));
        FooterLeft.AddChild(UiKit.KeyHint("Q / E", "Classe"));
        _equip = UiKit.Button("Équiper", Equip, "Primary", 60);
        _equip.CustomMinimumSize = new Vector2(260, 60);
        FooterRight.AddChild(_equip);
        App.I.Background?.SetMode(MenuBackground.Mode.Title);
        Rebuild();
        FocusContent();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { OnBack?.Invoke(); GetViewport().SetInputAsHandled(); return; }
        if (HandleTabKeys(e)) GetViewport().SetInputAsHandled();
    }

    protected override void FocusContent()
    {
        Callable.From(() =>
        {
            if (_selected != null && _rows.TryGetValue(_selected, out var r)) UiKit.FocusSilently(r.row);
            else UiKit.FocusSilently(_equip);
        }).CallDeferred();
    }

    private void UpdateCredits() => HeaderRight.Text = $"{Profile.I.Data.Credits} crédits";

    private void Rebuild()
    {
        foreach (var c in _list.GetChildren()) c.QueueFree();
        _rows.Clear();
        var equipped = Profile.I.Equipped(_cls);
        var weapons = Profile.I.Data.Arsenal.Where(w => w.T.Class == _cls)
            .OrderBy(w => w.T.Category).ThenByDescending(w => w.Rarity).ThenByDescending(w => w.Level).ToList();
        if (_selected != null && !weapons.Contains(_selected)) _selected = null;
        _selected ??= equipped.FirstOrDefault() ?? weapons.FirstOrDefault();
        string lastCat = null;
        foreach (var w in weapons)
        {
            if (w.T.Category != lastCat)
            {
                if (lastCat != null) _list.AddChild(UiKit.Gap(12));
                _list.AddChild(UiKit.Eyebrow(w.T.Category));
                _list.AddChild(UiKit.Gap(2));
                lastCat = w.T.Category;
            }
            var wi = w;
            var b = UiKit.Button("", () => Select(wi), "Row", 52);
            b.ToggleMode = true;
            b.SetPressedNoSignal(w == _selected);
            // Parcourir la liste met la fiche à jour (comme dans EDF).
            b.FocusEntered += () => Select(wi);
            var h = UiKit.HBox(14);
            h.MouseFilter = MouseFilterEnum.Ignore;
            UiKit.FillRect(h);
            h.OffsetLeft = 18; h.OffsetRight = -18;
            h.AddChild(new ColorRect { Color = Rarities.Colors[(int)w.Rarity], CustomMinimumSize = new Vector2(3, 28), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
            var n = UiKit.Label($"{w.T.Name}", 17, w == _selected ? UiKit.Text : new Color(UiKit.Text, 0.8f));
            n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            n.VerticalAlignment = VerticalAlignment.Center;
            n.ClipText = true;
            h.AddChild(n);
            var nw = UiKit.Label("NOUVEAU", 12, UiKit.Accent);
            nw.AddThemeFontOverride("font", UiKit.BodyMedium);
            nw.VerticalAlignment = VerticalAlignment.Center;
            nw.Visible = w.IsNew;
            h.AddChild(nw);
            var slot = equipped.IndexOf(w);
            if (slot >= 0)
            {
                var s = UiKit.Label($"ARME {slot + 1}", 12, UiKit.Muted);
                s.AddThemeFontOverride("font", UiKit.BodyMedium);
                s.VerticalAlignment = VerticalAlignment.Center;
                h.AddChild(s);
            }
            var lvl = UiKit.Label($"Nv.{w.Level}", 15, UiKit.Muted);
            lvl.VerticalAlignment = VerticalAlignment.Center;
            lvl.CustomMinimumSize = new Vector2(52, 0);
            lvl.HorizontalAlignment = HorizontalAlignment.Right;
            h.AddChild(lvl);
            b.AddChild(h);
            _list.AddChild(b);
            _rows[w] = (b, n, nw);
        }
        if (weapons.Count == 0)
        {
            var empty = UiKit.Label("Aucune arme pour cette classe. Les caisses rouges en mission en contiennent.", 15, UiKit.Muted);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _list.AddChild(empty);
        }
        BuildDetail();
    }

    private void Select(WeaponInstance w)
    {
        if (w == _selected) return;
        _selected = w;
        _target = -1;
        foreach (var (wi, r) in _rows)
        {
            r.row.SetPressedNoSignal(wi == w);
            r.name.AddThemeColorOverride("font_color", wi == w ? UiKit.Text : new Color(UiKit.Text, 0.8f));
        }
        BuildDetail();
    }

    /// <summary>Emplacement visé par défaut : celui qui contient déjà l'arme, sinon une arme de même catégorie, sinon le premier.</summary>
    private int DefaultTarget(List<WeaponInstance> equipped, WeaponInstance w)
    {
        var here = equipped.IndexOf(w);
        if (here >= 0) return here;
        var same = equipped.FindIndex(e => e.T.Category == w.T.Category);
        return same >= 0 ? same : 0;
    }

    private void Equip()
    {
        if (_selected == null || _target < 0) return;
        Profile.I.Equip(_cls, _target, _selected);
        Sfx.Ui("pickup", -6f, 1.2f);
        Rebuild();
        FocusContent();
    }

    private void BuildDetail()
    {
        var focused = FocusedKey();
        foreach (var c in _detail.GetChildren()) c.QueueFree();
        var equipped = Profile.I.Equipped(_cls);
        var w = _selected;
        _equip.Visible = w != null;
        if (w == null) return;
        if (w.IsNew)
        {
            w.IsNew = false;
            Profile.I.Save();
            if (_rows.TryGetValue(w, out var r)) r.isNew.Visible = false;
        }
        var slots = Classes.Get(_cls).WeaponSlots;
        if (_target < 0 || _target >= slots) _target = DefaultTarget(equipped, w);

        var rc = Rarities.Colors[(int)w.Rarity];
        // En-tête : identité de l'arme à gauche, chiffre héros (DPS) à droite.
        var top = UiKit.HBox(24);
        var id = UiKit.VBox(4);
        id.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        id.AddChild(UiKit.Eyebrow($"{Rarities.Names[(int)w.Rarity]} · {w.T.Category}", rc));
        var title = UiKit.Heading(w.T.Name, 48);
        title.ClipText = true;
        id.AddChild(title);
        var sub = UiKit.HBox(24);
        sub.AddChild(UiKit.Label($"Niveau {w.Level}", 17, UiKit.Muted));
        sub.AddChild(UiKit.Label(UiKit.Stars(Mathf.RoundToInt(w.TotalStars / 5f)), 17, rc));
        id.AddChild(sub);
        top.AddChild(id);
        var dps = UiKit.VBox(0);
        dps.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        var dv = UiKit.Label($"{w.Dps:0}", 72, UiKit.Text, true);
        dv.HorizontalAlignment = HorizontalAlignment.Right;
        dv.AddThemeConstantOverride("line_spacing", -12);
        dps.AddChild(dv);
        var dl = UiKit.Eyebrow("Dégâts par seconde");
        dl.HorizontalAlignment = HorizontalAlignment.Right;
        dps.AddChild(dl);
        top.AddChild(dps);
        _detail.AddChild(top);
        _detail.AddChild(UiKit.Gap(8));

        var current = _target < equipped.Count && equipped[_target] != w ? equipped[_target] : null;
        var stats = UiKit.VBox(0);
        void Stat(string name, float value, float? compare, string fmt, float max, bool lowerBetter = false, int stars = -1)
        {
            var h = UiKit.HBox(16);
            h.CustomMinimumSize = new Vector2(0, 32);
            var l = UiKit.Label(name, 16, UiKit.Muted);
            l.CustomMinimumSize = new Vector2(128, 0);
            l.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(l);
            var frac = Mathf.Clamp(lowerBetter ? 1f - value / max : value / max, 0.03f, 1f);
            var bar = new ProgressBar { ShowPercentage = false, MaxValue = 1, Value = frac, CustomMinimumSize = new Vector2(120, 4), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            bar.AddThemeStyleboxOverride("background", UiKit.Box(UiKit.Line, default, 0, 0, 0, 0));
            h.AddChild(bar);
            var v = UiKit.Label(string.Format(fmt, value), 17, UiKit.Text);
            v.AddThemeFontOverride("font", UiKit.BodyMedium);
            v.CustomMinimumSize = new Vector2(88, 0);
            v.HorizontalAlignment = HorizontalAlignment.Right;
            v.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(v);
            var d = UiKit.Label("", 14, UiKit.Muted);
            d.CustomMinimumSize = new Vector2(84, 0);
            d.VerticalAlignment = VerticalAlignment.Center;
            if (compare.HasValue && string.Format(fmt, Mathf.Abs(value - compare.Value)) != string.Format(fmt, 0f))
            {
                var better = lowerBetter ? value < compare.Value : value > compare.Value;
                d.Text = (value > compare.Value ? "▲ " : "▼ ") + string.Format(fmt, Mathf.Abs(value - compare.Value));
                d.AddThemeColorOverride("font_color", better ? UiKit.Good : UiKit.Danger);
            }
            h.AddChild(d);
            var st = UiKit.Label(stars >= 0 ? UiKit.Stars(stars) : "", 14, rc);
            st.CustomMinimumSize = new Vector2(76, 0);
            st.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(st);
            stats.AddChild(h);
        }
        Stat("Dégâts", w.Damage * w.T.Pellets, current != null ? current.Damage * current.T.Pellets : null, "{0:0}", 1200, stars: w.Stars[0]);
        if (w.T.Kind is WeaponKind.Hitscan or WeaponKind.Projectile)
        {
            Stat("Cadence", w.FireRate, current?.FireRate, "{0:0.0}/s", 20, stars: w.Stars[1]);
            Stat("Chargeur", w.Magazine, current?.Magazine, "{0:0}", 250, stars: w.Stars[2]);
        }
        Stat("Rechargement", w.Reload, current?.Reload, "{0:0.0} s", 30, true, w.Stars[3]);
        if (w.T.Kind == WeaponKind.Hitscan) Stat("Dispersion", w.Spread, current?.Spread, "{0:0.0}°", 10, true, w.Stars[4]);
        if (w.T.BlastRadius > 0) Stat("Rayon", w.BlastRadius, current?.BlastRadius, "{0:0.0} m", 16);
        Stat("Portée", w.Range, current?.Range, "{0:0} m", 1000);
        _detail.AddChild(stats);
        _detail.AddChild(UiKit.Label(current != null ? $"Comparaison avec {current.T.Name} Nv.{current.Level} (arme {_target + 1})" : " ", 14, UiKit.Dim));

        // Emplacement visé par « Équiper » : une tuile par arme de la classe.
        _detail.AddChild(UiKit.Gap(8));
        _detail.AddChild(UiKit.Eyebrow("Emplacement"));
        var slotRow = UiKit.HBox(4);
        for (var i = 0; i < slots; i++)
        {
            var si = i;
            var occupant = i < equipped.Count ? equipped[i] : null;
            var b = UiKit.Button("", () => { _target = si; BuildDetail(); }, "Card", 52);
            b.ToggleMode = true;
            b.SetPressedNoSignal(i == _target);
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            b.CustomMinimumSize = new Vector2(120, 52);
            SetFocusKey(b, $"s{i}");
            var v = UiKit.VBox(0);
            v.MouseFilter = MouseFilterEnum.Ignore;
            UiKit.FillRect(v);
            v.OffsetLeft = 18; v.OffsetRight = -12; v.OffsetTop = 6;
            var head = UiKit.Label($"ARME {i + 1}" + (occupant == w ? "  ✓" : ""), 12, i == _target ? UiKit.Text : UiKit.Muted);
            head.AddThemeFontOverride("font", UiKit.BodyMedium);
            v.AddChild(head);
            var on = UiKit.Label(occupant?.T.Name ?? "Vide", 15, occupant == w ? UiKit.Good : new Color(UiKit.Text, 0.85f));
            on.ClipText = true;
            v.AddChild(on);
            b.AddChild(v);
            slotRow.AddChild(b);
        }
        _detail.AddChild(slotRow);

        // Atelier : actions secondaires de la fiche (amélioration, recyclage).
        var shop = UiKit.HBox(8);
        var weakest = Enumerable.Range(0, 5).OrderBy(i => w.Stars[i]).First();
        if (w.Stars[weakest] < 5)
        {
            var cost = 300 + w.Level * 20 + w.TotalStars * 40;
            var can = Profile.I.Data.Credits >= cost;
            var up = UiKit.Button($"Améliorer {WeaponInstance.StatNames[weakest].ToLowerInvariant()} · {cost} cr.", () =>
            {
                if (Profile.I.Data.Credits < cost) return;
                Profile.I.Data.Credits -= cost;
                w.Stars[weakest]++;
                Profile.I.Save();
                Sfx.Ui("pickup_weapon", -2f);
                UpdateCredits();
                BuildDetail();
            }, null, 44);
            up.Disabled = !can;
            up.TooltipText = can ? "Atelier : ajoute une étoile à la statistique la plus faible." : "Crédits insuffisants.";
            SetFocusKey(up, "up");
            shop.AddChild(up);
        }
        var starter = Weapons.StarterLoadout[_cls].Contains(w.TemplateId) && Profile.I.Data.Arsenal.Count(x => x.TemplateId == w.TemplateId) <= 1;
        if (!equipped.Contains(w) && !starter)
        {
            var value = 50 + w.Level * 10 + (int)w.Rarity * 150 + w.TotalStars * 15;
            var confirm = false;
            Button rec = null;
            rec = UiKit.Button($"Recycler · +{value} cr.", () =>
            {
                if (!confirm)
                {
                    confirm = true;
                    rec.Text = "Confirmer le recyclage ?";
                    rec.AddThemeColorOverride("font_color", UiKit.Danger);
                    rec.AddThemeColorOverride("font_focus_color", UiKit.Danger);
                    rec.AddThemeColorOverride("font_hover_color", UiKit.Danger);
                    return;
                }
                Profile.I.Data.Arsenal.Remove(w);
                Profile.I.Data.Credits += value;
                Profile.I.Save();
                Sfx.Ui("pickup", -2f, 0.7f);
                UpdateCredits();
                _selected = null;
                Rebuild();
                FocusContent();
            }, null, 44);
            rec.TooltipText = "Démonte l'arme contre des crédits. Appuyez deux fois pour confirmer.";
            SetFocusKey(rec, "rec");
            shop.AddChild(rec);
        }
        if (shop.GetChildCount() > 0)
        {
            _detail.AddChild(UiKit.Gap(8));
            _detail.AddChild(UiKit.Eyebrow("Atelier"));
            _detail.AddChild(shop);
        }

        // Action principale
        var isHere = _target < equipped.Count && equipped[_target] == w;
        _equip.Text = isHere ? $"ÉQUIPÉE · ARME {_target + 1}" : $"ÉQUIPER · ARME {_target + 1}";
        _equip.Disabled = isHere;
        if (focused != null && !focused.StartsWith("w")) RestoreFocus(focused, _rows.TryGetValue(w, out var rr) ? rr.row : null);
    }
}
