using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Bilan (maquette E-Bilan) : VICTOIRE / ÉCHEC dans le bandeau, statistiques en plaques, butin (armes avec Lv et ☆,
/// sans rareté), armure gagnée, crédits, XP de classe, carte de classe, boutons Rejouer / Continuer.
/// </summary>
public partial class ResultsScreen : CanvasLayer
{
    public MissionReport Report;
    public Action OnContinue;
    public Action OnReplay;
    private EPlate _continue, _replay;
    private readonly List<Control> _lootRows = new();
    private int _shown;
    private float _timer = 0.6f;

    public override void _Ready()
    {
        Layer = 30;
        var root = new Control { Theme = UiE.Theme, MouseFilter = Control.MouseFilterEnum.Stop };
        UiKit.FillRect(root);
        AddChild(root);
        root.AddChild(new MenuBackdrop { Animated = false });
        var stage = EFrame.Build(root, "Bilan de mission", out _, backdrop: false, accent: false);
        var r = Report;

        // Verdict + rappel de la mission.
        var italic = new FontVariation { BaseFont = UiE.CondXBold, SpacingGlyph = 4, VariationTransform = new Transform2D(new Vector2(1, 0), new Vector2(0.2f, 1), Vector2.Zero) };
        var verdict = UiE.Label(r.Success ? "VICTOIRE !" : "ÉCHEC", 46, r.Success ? UiE.Bright : UiE.Danger, italic, 4);
        verdict.AddThemeColorOverride("font_shadow_color", new Color(r.Success ? UiE.Frame : new Color("e8432e"), 0.45f));
        verdict.AddThemeConstantOverride("shadow_outline_size", 9);
        verdict.AddThemeConstantOverride("shadow_offset_x", 0);
        verdict.AddThemeConstantOverride("shadow_offset_y", 0);
        verdict.Position = new Vector2(500, 18);
        stage.AddChild(verdict);
        var meta = UiE.Label($"{Net.I.LobbyMission + 1:00}. {r.MissionName} · {Difficulties.Names[(int)r.Difficulty]} · {UiKit.Time(r.Elapsed)}", 20);
        meta.Position = new Vector2(780, 38);
        stage.AddChild(meta);

        stage.AddChild(new SoldierPortrait().At(936, 70, 330, 594));

        // Statistiques en plaques.
        var col = new VBoxContainer { Position = new Vector2(100, 112), Size = new Vector2(340, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 8);
        stage.AddChild(col);
        col.AddChild(EPlate.Display("Éliminations", $"{r.Kills}", true));
        if (r.Squad.Count > 1)
            foreach (var s in r.Squad) col.AddChild(EPlate.Display(s.name, $"{s.kills}", false));
        var crates = 0;
        foreach (var c in r.Crates) crates += c;
        col.AddChild(EPlate.Display("Caisses", $"{crates}", false));
        if (r.AutoCollected > 0) col.AddChild(EPlate.Display("Auto-collectées", $"{r.AutoCollected}", false));
        col.AddChild(EPlate.Display("Temps", UiKit.Time(r.Elapsed), false));

        BuildLoot(stage);
        BuildClassCard(stage);

        // Boutons (au-dessus de la bande basse) et aides.
        var host = Net.I.IsHost;
        _replay = new EPlate("Rejouer", () => OnReplay?.Invoke(), 168) { Icon = UiE.Icon.Replay, Disabled = !host };
        _replay.Position = new Vector2(290, 610);
        stage.AddChild(_replay);
        _continue = new EPlate(host ? "Continuer" : "Attente de l'hôte", () => OnContinue?.Invoke(), 236) { Icon = UiE.Icon.Play, Disabled = !host };
        _continue.Position = new Vector2(468, 610);
        stage.AddChild(_continue);
        _replay.FocusNeighborRight = _replay.GetPathTo(_continue);
        _continue.FocusNeighborLeft = _continue.GetPathTo(_replay);
        var hints = EFrame.Hints(stage);
        hints.AddChild(UiE.KeyHint("Entrée:Continuer", host ? () => OnContinue?.Invoke() : null));
        hints.AddChild(UiE.KeyHint("R:Rejouer", host ? () => OnReplay?.Invoke() : null));
        UiKit.FocusLater(host ? _continue : null);
        UiKit.FadeIn(root, 0.2f);
    }

    private void BuildLoot(EStage stage)
    {
        var r = Report;
        var box = new Panel { Position = new Vector2(468, 112), Size = new Vector2(440, 488), MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeStyleboxOverride("panel", UiE.PanelBox(3, 0, 0, 0.9f));
        stage.AddChild(box);
        var v = new VBoxContainer { Position = new Vector2(3, 3), Size = new Vector2(434, 482), MouseFilter = Control.MouseFilterEnum.Ignore };
        v.AddThemeConstantOverride("separation", 0);
        box.AddChild(v);
        v.AddChild(LootRow($"Butin : {r.Weapons.Count} arme{(r.Weapons.Count > 1 ? "s" : "")}", "Lv", "☆", 15, UiE.Mint, false, null));
        v.AddChild(new ColorRect { Color = UiE.Edge, CustomMinimumSize = new Vector2(0, 2), MouseFilter = Control.MouseFilterEnum.Ignore });

        // Liste défilante (au-delà de 6 armes).
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, Mathf.Min(6, Mathf.Max(1, r.Weapons.Count)) * 37), MouseFilter = Control.MouseFilterEnum.Pass };
        v.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        list.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(list);
        if (r.Weapons.Count == 0)
        {
            var none = UiE.Label("Aucune caisse d'arme récupérée.", 17, UiE.TextMuted);
            none.CustomMinimumSize = new Vector2(0, 36);
            none.VerticalAlignment = VerticalAlignment.Center;
            var m = UiKit.Margin(12, 0, 12, 0);
            m.AddChild(none);
            list.AddChild(m);
        }
        foreach (var (w, duplicate) in r.Weapons)
        {
            var tag = duplicate ? "DOUBLON" : "NEW"; // doublon strict : converti en crédits
            var row = LootRow(w.T.Name, $"{w.Level}", $"{Mathf.RoundToInt(w.TotalStars / 5f)}", 18, UiE.Text, true, tag);
            row.Modulate = new Color(1, 1, 1, 0);
            list.AddChild(row);
            _lootRows.Add(row);
        }

        // Armure, crédits, expérience.
        var info = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        info.AddThemeConstantOverride("separation", 6);
        var pad = UiKit.Margin(12, 14, 12, 0);
        pad.MouseFilter = Control.MouseFilterEnum.Ignore;
        pad.AddChild(info);
        v.AddChild(pad);
        var cls = Profile.I.Data.SelectedClass;
        var after = Profile.I.Armor(cls);
        var before = after - r.ArmorGained;
        info.AddChild(Pair("Armure", $"{before:0} → {after:0}", r.ArmorGained >= 0 ? $" (+{r.ArmorGained:0})" : $" ({r.ArmorGained:0})"));
        info.AddChild(new GaugeBar { Base = after > 0 ? Mathf.Clamp(Mathf.Min(before, after) / Mathf.Max(before, after), 0, 1) : 0, Gain = after > before ? 1f : 0f, Ticks = 22, CustomMinimumSize = new Vector2(0, 14) });
        info.AddChild(UiKit.Gap(0));
        info.AddChild(Pair("Crédits", r.CreditsGained >= 0 ? $"+{r.CreditsGained}" : $"{r.CreditsGained}", null));
        info.AddChild(UiKit.Gap(0));
        info.AddChild(Pair("XP classe", $"+{r.XpGained}", null));
        var lv = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        lv.AddThemeConstantOverride("separation", 8);
        lv.AddChild(UiE.Label($"Niv.{r.LevelAfter}", 15));
        var xp = new GaugeBar { Base = 0, Gain = Profile.I.LevelProgress(cls), Gradient = true, CustomMinimumSize = new Vector2(0, 14), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        lv.AddChild(xp);
        lv.AddChild(UiE.Label($"Niv.{r.LevelAfter + 1}", 15, UiE.Bright));
        info.AddChild(lv);
        var note = r.LevelAfter > r.LevelBefore
            ? $"Niveau supérieur atteint : {Classes.Get(cls).Name} niv. {r.LevelAfter}."
            : !r.Success ? "Échec : armes conservées, armure réduite de moitié, crédits perdus." : "";
        if (note != "")
        {
            var n = UiE.Label(note, 15, r.Success ? UiE.TextDim : UiE.DangerSoft);
            n.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            n.CustomMinimumSize = new Vector2(410, 0);
            info.AddChild(n);
        }
    }

    private static Control LootRow(string name, string lv, string stars, int size, Color color, bool line, string tag)
    {
        var wrap = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        wrap.AddThemeConstantOverride("separation", 0);
        var h = new HBoxContainer { CustomMinimumSize = new Vector2(0, line ? 36 : 30), MouseFilter = Control.MouseFilterEnum.Ignore };
        h.AddThemeConstantOverride("separation", 0);
        h.AddChild(UiKit.HGap(12));
        var n = UiE.Label(name, size, color);
        n.VerticalAlignment = VerticalAlignment.Center;
        n.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var nb = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        nb.AddThemeConstantOverride("separation", 6);
        n.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        nb.AddChild(n);
        if (tag != null)
        {
            var t = UiE.Label(tag, tag == "NEW" ? 10 : 12, UiE.Gold);
            t.VerticalAlignment = VerticalAlignment.Center;
            nb.AddChild(t);
        }
        h.AddChild(nb);
        foreach (var (s, wdt) in new[] { (lv, 40), (stars, 34) })
        {
            var l = UiE.Label(s, size == 18 ? 18 : 15, color);
            l.CustomMinimumSize = new Vector2(wdt, 0);
            l.HorizontalAlignment = HorizontalAlignment.Center;
            l.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(l);
        }
        h.AddChild(UiKit.HGap(12));
        wrap.AddChild(h);
        if (line) wrap.AddChild(new ColorRect { Color = new Color("1e6a34"), CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore });
        return wrap;
    }

    private static Control Pair(string name, string value, string extra)
    {
        var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        h.AddThemeConstantOverride("separation", 0);
        var n = UiE.Label(name, 18, UiE.Mint);
        n.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        h.AddChild(n);
        h.AddChild(UiE.Label(value, 18));
        if (extra != null) h.AddChild(UiE.Label(extra, 18, UiE.Bright));
        return h;
    }

    private void BuildClassCard(EStage stage)
    {
        var p = Profile.I;
        var cls = p.Data.SelectedClass;
        var r = Report;
        var card = new ECard { Position = new Vector2(912, 420), Size = new Vector2(326, 0) };
        var rows = new List<ECard.Row>
        {
            new(new[] { new ECard.Cell("Classe", 84), new ECard.Cell(Classes.Get(cls).Name, 0, 19), new ECard.Cell($"Niv.{r.LevelAfter}", 70, 17, r.LevelAfter > r.LevelBefore ? UiE.Bright : null) }, 30, true),
            new(new[] { new ECard.Cell("Armure:", 84), new ECard.Cell($"{p.Armor(cls):0}", 0, 19), new ECard.Cell(r.ArmorGained > 0 ? $"+{r.ArmorGained:0}" : "", 70, 17, UiE.Bright) }, 26),
        };
        var eq = p.Equipped(cls);
        for (var i = 0; i < eq.Count; i++)
            rows.Add(new(new[] { new ECard.Cell($"Arme {i + 1}", 84), new ECard.Cell(eq[i].T.Name), new ECard.Cell($"Lv{eq[i].Level}", 70) }, i == eq.Count - 1 ? 28 : 24));
        card.SetRows(rows);
        stage.AddChild(card);
    }

    public override void _Process(double delta)
    {
        // Les armes du butin apparaissent une à une.
        if (_shown >= _lootRows.Count) return;
        _timer -= (float)delta;
        if (_timer > 0) return;
        _timer = 0.35f;
        var row = _lootRows[_shown];
        var w = Report.Weapons[_shown].weapon;
        _shown++;
        var tw = row.CreateTween();
        tw.TweenProperty(row, "modulate:a", 1f, 0.2f);
        Sfx.Ui(_shown == _lootRows.Count ? "pickup_weapon" : "pickup", -6f, 0.9f + 0.002f * w.Level);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Net.I.IsHost) return;
        if (e is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.R } || e is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Y })
        {
            GetViewport().SetInputAsHandled();
            OnReplay?.Invoke();
            return;
        }
        if (e.IsActionPressed("ui_accept") && GetViewport().GuiGetFocusOwner() == null)
        {
            GetViewport().SetInputAsHandled();
            OnContinue?.Invoke();
        }
    }
}

/// <summary>Jauge du bilan : part acquise (vert), gain (vert clair), graduations.</summary>
public partial class GaugeBar : Control
{
    public float Base, Gain;
    public int Ticks;
    public bool Gradient;

    public GaugeBar() { MouseFilter = MouseFilterEnum.Ignore; }

    public override void _Draw()
    {
        var w = Size.X; var h = Size.Y;
        DrawRect(new Rect2(0, 0, w, h), new Color(0, 0, 0, 0.5f));
        var iw = w - 2;
        var b = iw * Mathf.Clamp(Base, 0, 1);
        if (Gradient)
        {
            var gw = iw * Mathf.Clamp(Gain, 0, 1);
            if (gw > 0) UiE.HGradient(this, new[] { new Vector2(1, 1), new Vector2(1 + gw, 1), new Vector2(1 + gw, h - 1), new Vector2(1, h - 1) }, 1, 1 + gw, new[] { (1f, new Color("33c653")), (1 + gw, new Color("a6ffb4")) });
        }
        else
        {
            if (b > 0) DrawRect(new Rect2(1, 1, b, h - 2), new Color("33c653"));
            if (Gain > 0 && b < iw) DrawRect(new Rect2(1 + b, 1, iw - b, h - 2), new Color("a6ffb4"));
        }
        if (Ticks > 0)
            for (var x = 0f; x < w; x += Ticks) DrawRect(new Rect2(x, 0, 1, h), new Color(2 / 255f, 16 / 255f, 8 / 255f, 0.55f));
        DrawRect(new Rect2(0, 0, w, h), UiE.Edge, false, 1f);
    }
}
