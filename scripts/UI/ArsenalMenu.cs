using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Arsenal : onglets de classe, liste d'armes à gauche, fiche et comparaison à droite, équiper en bas à droite.</summary>
public partial class ArsenalMenu : MenuScreen
{
    public Action OnBack;
    private SoldierClass _cls;
    private VBoxContainer _list, _detail;
    private WeaponInstance _selected;

    public override void _Ready()
    {
        BuildFrame("Arsenal", 0.9f);
        _cls = Profile.I.Data.SelectedClass;
        foreach (var cd in Classes.All)
        {
            var cls = cd.Id;
            var b = AddTab(cd.Name, () => { _cls = cls; _selected = null; Rebuild(); });
            b.SetPressedNoSignal(cls == _cls);
        }
        HeaderRight.Text = $"{Profile.I.Data.Credits} crédits";
        HeaderRight.AddThemeColorOverride("font_color", UiKit.Gold);

        var row = UiKit.HBox(64);
        UiKit.FillRect(row);
        row.MouseFilter = MouseFilterEnum.Ignore;
        Body.AddChild(row);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(540, 0) };
        _list = UiKit.VBox(2);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);
        row.AddChild(scroll);
        _detail = UiKit.VBox(10);
        _detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(_detail);
        FooterLeft.AddChild(UiKit.KeyHint("Échap", "Retour", () => OnBack?.Invoke()));
        FooterLeft.AddChild(UiKit.KeyHint("Q / E", "Classe"));
        App.I.Background?.SetMode(MenuBackground.Mode.Title);
        Rebuild();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { OnBack?.Invoke(); GetViewport().SetInputAsHandled(); return; }
        if (HandleTabKeys(e)) GetViewport().SetInputAsHandled();
    }

    private void Rebuild()
    {
        foreach (var c in _list.GetChildren()) c.QueueFree();
        var equipped = Profile.I.Equipped(_cls);
        var weapons = Profile.I.Data.Arsenal.Where(w => w.T.Class == _cls)
            .OrderBy(w => w.T.Category).ThenByDescending(w => w.Rarity).ThenByDescending(w => w.Level).ToList();
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
            var b = UiKit.Button("", () => { _selected = wi; Rebuild(); }, "Row", 54);
            b.ToggleMode = true;
            b.SetPressedNoSignal(w == _selected);
            var h = UiKit.HBox(14);
            h.MouseFilter = MouseFilterEnum.Ignore;
            UiKit.FillRect(h);
            h.OffsetLeft = 18; h.OffsetRight = -18;
            h.AddChild(new ColorRect { Color = Rarities.Colors[(int)w.Rarity], CustomMinimumSize = new Vector2(3, 30), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
            var n = UiKit.Label($"{w.T.Name}", 17, w == _selected ? UiKit.Text : new Color(UiKit.Text, 0.85f));
            n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            n.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(n);
            if (w.IsNew)
            {
                var nw = UiKit.Label("NOUVEAU", 12, UiKit.Accent);
                nw.VerticalAlignment = VerticalAlignment.Center;
                h.AddChild(nw);
            }
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
        }
        BuildDetail(equipped);
    }

    private void BuildDetail(List<WeaponInstance> equipped)
    {
        foreach (var c in _detail.GetChildren()) c.QueueFree();
        foreach (var c in FooterRight.GetChildren()) c.QueueFree();
        var w = _selected;
        if (w == null) return;
        if (w.IsNew) { w.IsNew = false; Profile.I.Save(); }
        var rc = Rarities.Colors[(int)w.Rarity];
        _detail.AddChild(UiKit.Eyebrow($"{Rarities.Names[(int)w.Rarity]} · {w.T.Category}", rc));
        _detail.AddChild(UiKit.Heading(w.T.Name, 52));
        var sub = UiKit.HBox(24);
        sub.AddChild(UiKit.Label($"Niveau {w.Level}", 17, UiKit.Muted));
        sub.AddChild(UiKit.Label(UiKit.Stars(Mathf.RoundToInt(w.TotalStars / 5f)), 17, rc));
        _detail.AddChild(sub);
        _detail.AddChild(UiKit.Gap(16));
        var dps = UiKit.HBox(12);
        var big = UiKit.Label($"{w.Dps:0}", 72, UiKit.Text, true);
        dps.AddChild(big);
        var dl = UiKit.Label("DPS", 18, UiKit.Muted, true);
        dl.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        dps.AddChild(dl);
        _detail.AddChild(dps);
        _detail.AddChild(UiKit.Gap(8));

        var current = equipped.FirstOrDefault(e => e != w && e.T.Category == w.T.Category);
        void Stat(string name, float value, float? compare, string fmt, float max, bool lowerBetter = false, int stars = -1)
        {
            var h = UiKit.HBox(16);
            h.CustomMinimumSize = new Vector2(0, 30);
            var l = UiKit.Label(name, 16, UiKit.Muted);
            l.CustomMinimumSize = new Vector2(140, 0);
            l.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(l);
            var barBg = new ColorRect { Color = UiKit.Line, CustomMinimumSize = new Vector2(260, 4), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            var frac = Mathf.Clamp(lowerBetter ? 1f - value / max : value / max, 0.03f, 1f);
            barBg.AddChild(new ColorRect { Color = UiKit.Text, Size = new Vector2(260 * frac, 4) });
            h.AddChild(barBg);
            var v = UiKit.Label(string.Format(fmt, value), 17, UiKit.Text);
            v.AddThemeFontOverride("font", UiKit.BodyMedium);
            v.CustomMinimumSize = new Vector2(96, 0);
            v.HorizontalAlignment = HorizontalAlignment.Right;
            h.AddChild(v);
            if (compare.HasValue && string.Format(fmt, Mathf.Abs(value - compare.Value)) != string.Format(fmt, 0f))
            {
                var better = lowerBetter ? value < compare.Value : value > compare.Value;
                var d = UiKit.Label((value > compare.Value ? "▲ " : "▼ ") + string.Format(fmt, Mathf.Abs(value - compare.Value)), 14, better ? UiKit.Good : UiKit.Danger);
                d.CustomMinimumSize = new Vector2(90, 0);
                h.AddChild(d);
            }
            else h.AddChild(UiKit.Gap(90));
            if (stars >= 0) h.AddChild(UiKit.Label(UiKit.Stars(stars), 14, rc));
            _detail.AddChild(h);
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
        if (current != null)
        {
            _detail.AddChild(UiKit.Gap(4));
            _detail.AddChild(UiKit.Label($"Comparaison avec {current.T.Name} Nv.{current.Level}", 14, UiKit.Dim));
        }

        // Action principale : équiper dans un emplacement
        for (var i = 0; i < Classes.Get(_cls).WeaponSlots; i++)
        {
            var slot = i;
            var isHere = i < equipped.Count && equipped[i] == w;
            var b = UiKit.Button(isHere ? $"Arme {i + 1} ✓" : $"Équiper arme {i + 1}", () =>
            {
                Profile.I.Equip(_cls, slot, w);
                Rebuild();
            }, isHere ? null : "Primary", 60);
            b.Disabled = isHere;
            if (isHere) b.CustomMinimumSize = new Vector2(150, 60);
            FooterRight.AddChild(b);
        }
    }
}
