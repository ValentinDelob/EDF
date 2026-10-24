using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Bilan (maquette E-Bilan) : VICTOIRE / ÉCHEC dans le bandeau, statistiques en plaques, butin (armes avec Lv et ☆,
/// sans rareté ; une arme déjà possédée et améliorée liste ses améliorations en dessous), armure gagnée, carte de classe,
/// boutons Rejouer / Continuer.
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

        // Liste défilante : chaque arme, puis sous une arme possédée améliorée une ligne par amélioration.
        var height = 0f;
        foreach (var lr in r.Weapons)
            height += 37 + (lr.IsNew ? 0 : (lr.After.Level > lr.Before.Level ? 1 : 0) + lr.ImprovedStats.Count()) * SubLineHeight;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, Mathf.Clamp(height, 37, 380)), MouseFilter = Control.MouseFilterEnum.Pass };
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
        foreach (var lr in r.Weapons)
        {
            var w = lr.After;
            var owned = !lr.IsNew && !lr.Improved; // déjà possédée, rien d'amélioré : ligne assombrie
            var group = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            group.AddThemeConstantOverride("separation", 0);
            var subs = SubLines(lr);
            var row = LootRow(w.T.Name, $"{w.Level}", $"{Mathf.RoundToInt(w.TotalStars / 5f)}", 18,
                owned ? UiE.TextMuted : UiE.Text, subs.Count == 0, lr.IsNew ? "NEW" : owned ? "déjà possédée" : null);
            if (owned) row.Modulate = new Color(1, 1, 1, 0.55f);
            group.AddChild(row);
            if (subs.Count > 0)
            {
                foreach (var s in subs) group.AddChild(s);
                group.AddChild(UiKit.Gap(4));
                group.AddChild(new ColorRect { Color = new Color("1e6a34"), CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore });
            }
            group.Modulate = new Color(1, 1, 1, 0);
            list.AddChild(group);
            _lootRows.Add(group);
        }

        // Armure gagnée (ligne simple), note d'échec.
        var info = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        info.AddThemeConstantOverride("separation", 6);
        var pad = UiKit.Margin(12, 14, 12, 0);
        pad.MouseFilter = Control.MouseFilterEnum.Ignore;
        pad.AddChild(info);
        v.AddChild(pad);
        var cls = Profile.I.Data.SelectedClass;
        var after = Profile.I.Armor(cls);
        var before = after - r.ArmorGained;
        if (r.ArmorGained > 0)
            info.AddChild(Pair("Armure", $"{before:0} → {after:0}", $" (+{r.ArmorGained:0})"));
        if (!r.Success)
        {
            var n = UiE.Label("Échec : armes conservées, armure réduite de moitié.", 15, UiE.DangerSoft);
            n.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            n.CustomMinimumSize = new Vector2(410, 0);
            info.AddChild(n);
        }
    }

    private const int SubLineHeight = 24;

    /// <summary>
    /// Lignes d'amélioration d'une arme déjà possédée, en retrait sous l'arme : « Niveau 12 → 18 » puis
    /// « Dégâts ☆2 → ☆3 (+10) » par caractéristique améliorée (bonus des étoiles après amélioration).
    /// </summary>
    private static List<Control> SubLines(LootResult lr)
    {
        var lines = new List<Control>();
        if (lr.IsNew) return lines;
        if (lr.After.Level > lr.Before.Level)
            lines.Add(SubLine("Niveau", $"{lr.Before.Level} → {lr.After.Level}", null));
        foreach (var s in lr.ImprovedStats)
            lines.Add(SubLine(WeaponInstance.StatNames[(int)s], $"☆{lr.Before.StarsOf(s)} → ☆{lr.After.StarsOf(s)}", $"({lr.After.Describe(s).bonus})"));
        return lines;
    }

    private static Control SubLine(string name, string change, string bonus)
    {
        var h = new HBoxContainer { CustomMinimumSize = new Vector2(0, SubLineHeight), MouseFilter = Control.MouseFilterEnum.Ignore };
        h.AddThemeConstantOverride("separation", 8);
        h.AddChild(UiKit.HGap(30));
        var mark = UiE.Label("▲", 11, UiE.Bright);
        mark.VerticalAlignment = VerticalAlignment.Center;
        h.AddChild(mark);
        var n = UiE.Label(name, 16, UiE.Mint);
        n.CustomMinimumSize = new Vector2(118, 0);
        n.VerticalAlignment = VerticalAlignment.Center;
        h.AddChild(n);
        var c = UiE.Label(change, 16, UiE.Text);
        c.VerticalAlignment = VerticalAlignment.Center;
        h.AddChild(c);
        if (bonus != null)
        {
            var b = UiE.Label(bonus, 16, UiE.Bright);
            b.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(b);
        }
        return h;
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
            var t = UiE.Label(tag, tag == "NEW" ? 10 : 13, tag == "NEW" ? UiE.Gold : UiE.TextMuted);
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
            new(new[] { new ECard.Cell("Classe", 84), new ECard.Cell(Classes.Get(cls).Name, 0, 19) }, 30, true),
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
        var w = Report.Weapons[_shown].After;
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
