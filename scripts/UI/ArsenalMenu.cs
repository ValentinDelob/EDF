using System;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Arsenal : armes possédées par classe, détail des statistiques, affectation aux emplacements.</summary>
public partial class ArsenalMenu : Control
{
    public Action OnBack;
    private SoldierClass _cls;
    private HBoxContainer _tabs;
    private VBoxContainer _list, _detail;
    private WeaponInstance _selected;

    public override void _Ready()
    {
        Theme = UiKit.Theme;
        UiKit.FillRect(this);
        AddChild(UiKit.Scrim(0.8f));
        _cls = Profile.I.Data.SelectedClass;
        var m = UiKit.Margin(72, 56, 72, 48);
        UiKit.FillRect(m);
        AddChild(m);
        var root = UiKit.VBox(24);
        m.AddChild(root);
        var head = UiKit.HBox(24);
        head.AddChild(UiKit.Button("‹ Retour", () => OnBack?.Invoke(), null, 44));
        head.AddChild(UiKit.Heading("Arsenal", 40));
        head.AddChild(UiKit.Spacer(false, true));
        var credits = UiKit.Label($"{Profile.I.Data.Credits} crédits", 18, new Color("ffcc26"), true);
        credits.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(credits);
        root.AddChild(head);
        _tabs = UiKit.HBox(4);
        root.AddChild(_tabs);

        var cols = UiKit.HBox(40);
        cols.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddChild(cols);
        var scroll = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SizeFlagsStretchRatio = 1.2f;
        _list = UiKit.VBox(6);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);
        cols.AddChild(scroll);
        _detail = UiKit.VBox(12);
        _detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        cols.AddChild(_detail);
        Rebuild();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel")) { OnBack?.Invoke(); GetViewport().SetInputAsHandled(); }
    }

    private void Rebuild()
    {
        foreach (var c in _tabs.GetChildren()) c.QueueFree();
        foreach (var cd in Classes.All)
        {
            var cls = cd.Id;
            var b = UiKit.Button(cd.Name, () => { _cls = cls; _selected = null; Rebuild(); }, "Chip", 40);
            b.ToggleMode = true;
            b.SetPressedNoSignal(cls == _cls);
            _tabs.AddChild(b);
        }
        foreach (var c in _list.GetChildren()) c.QueueFree();
        var equipped = Profile.I.Equipped(_cls);
        var weapons = Profile.I.Data.Arsenal.Where(w => w.T.Class == _cls)
            .OrderByDescending(w => w.Rarity).ThenByDescending(w => w.Level).ThenBy(w => w.T.Name).ToList();
        _selected ??= equipped.FirstOrDefault() ?? weapons.FirstOrDefault();
        foreach (var w in weapons)
        {
            var wi = w;
            var b = UiKit.Button("", () => { _selected = wi; Rebuild(); }, "Card", 64);
            b.ToggleMode = true;
            b.SetPressedNoSignal(w == _selected);
            var h = UiKit.HBox(14);
            h.MouseFilter = MouseFilterEnum.Ignore;
            UiKit.FillRect(h);
            h.OffsetLeft = 16; h.OffsetRight = -16;
            var bar = new ColorRect { Color = Rarities.Colors[(int)w.Rarity], CustomMinimumSize = new Vector2(3, 36), SizeFlagsVertical = SizeFlags.ShrinkCenter };
            h.AddChild(bar);
            var info = UiKit.VBox(0);
            info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            info.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            info.AddChild(UiKit.Label($"{w.T.Name}  Nv.{w.Level}" + (w.IsNew ? "   NOUVEAU" : ""), 17));
            info.AddChild(UiKit.Label($"{w.T.Category} · {Rarities.Names[(int)w.Rarity]}", 13, UiKit.Muted));
            h.AddChild(info);
            var slot = equipped.IndexOf(w);
            var dps = UiKit.Label(slot >= 0 ? $"Emplacement {slot + 1}" : $"DPS {w.Dps:0}", 14, slot >= 0 ? UiKit.Accent : UiKit.Muted, slot >= 0);
            dps.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            h.AddChild(dps);
            b.AddChild(h);
            _list.AddChild(b);
        }
        BuildDetail(equipped);
    }

    private void BuildDetail(System.Collections.Generic.List<WeaponInstance> equipped)
    {
        foreach (var c in _detail.GetChildren()) c.QueueFree();
        var w = _selected;
        if (w == null) return;
        if (w.IsNew) { w.IsNew = false; Profile.I.Save(); }
        _detail.AddChild(UiKit.Eyebrow($"{w.T.Category} · {Rarities.Names[(int)w.Rarity]}", Rarities.Colors[(int)w.Rarity]));
        _detail.AddChild(UiKit.Heading($"{w.T.Name}", 36));
        _detail.AddChild(UiKit.Label($"Niveau {w.Level} · DPS estimé {w.Dps:0}", 16, UiKit.Muted));
        _detail.AddChild(UiKit.Rule());
        var current = equipped.FirstOrDefault(e => e.T.Kind == w.T.Kind && e != w) ?? equipped.FirstOrDefault(e => e != w);
        void Stat(string name, float value, float compare, string fmt, bool lowerBetter = false, int stars = -1)
        {
            var h = UiKit.HBox(16);
            h.CustomMinimumSize = new Vector2(0, 34);
            var l = UiKit.Label(name, 16, UiKit.Muted);
            l.CustomMinimumSize = new Vector2(150, 0);
            h.AddChild(l);
            var v = UiKit.Label(string.Format(fmt, value), 18, UiKit.Text, true);
            v.CustomMinimumSize = new Vector2(110, 0);
            h.AddChild(v);
            if (current != null && Mathf.Abs(value - compare) > 0.001f)
            {
                var better = lowerBetter ? value < compare : value > compare;
                h.AddChild(UiKit.Label((value > compare ? "▲ " : "▼ ") + string.Format(fmt, Mathf.Abs(value - compare)), 14, better ? UiKit.Good : UiKit.Danger));
            }
            h.AddChild(UiKit.Spacer(false, true));
            if (stars >= 0) h.AddChild(UiKit.Label(UiKit.Stars(stars), 16, UiKit.Accent));
            _detail.AddChild(h);
        }
        Stat("Dégâts", w.Damage * w.T.Pellets, current != null ? current.Damage * current.T.Pellets : 0, "{0:0}", stars: w.Stars[0]);
        if (w.T.Kind is WeaponKind.Hitscan or WeaponKind.Projectile)
        {
            Stat("Cadence", w.FireRate, current?.FireRate ?? 0, "{0:0.0}/s", stars: w.Stars[1]);
            Stat("Chargeur", w.Magazine, current?.Magazine ?? 0, "{0:0}", stars: w.Stars[2]);
        }
        Stat("Rechargement", w.Reload, current?.Reload ?? 0, "{0:0.0} s", true, w.Stars[3]);
        if (w.T.Kind == WeaponKind.Hitscan) Stat("Dispersion", w.Spread, current?.Spread ?? 0, "{0:0.0}°", true, w.Stars[4]);
        if (w.T.BlastRadius > 0) Stat("Rayon", w.BlastRadius, current?.BlastRadius ?? 0, "{0:0.0} m");
        Stat("Portée", w.Range, current?.Range ?? 0, "{0:0} m");
        if (current != null) _detail.AddChild(UiKit.Label($"Comparé à : {current.T.Name} Nv.{current.Level}", 13, UiKit.Muted));
        _detail.AddChild(UiKit.Spacer());
        var slots = UiKit.HBox(10);
        for (var i = 0; i < Classes.Get(_cls).WeaponSlots; i++)
        {
            var slot = i;
            var isHere = i < equipped.Count && equipped[i] == w;
            var b = UiKit.Button(isHere ? $"Équipée · {i + 1}" : $"Équiper en {i + 1}", () =>
            {
                Profile.I.Equip(_cls, slot, w);
                Rebuild();
            }, isHere ? null : "Primary", 56);
            b.Disabled = isHere;
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            slots.AddChild(b);
        }
        _detail.AddChild(slots);
    }
}
