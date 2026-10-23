using System;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Bilan : titre centré, révélation des armes une par une au centre, totaux en bas à gauche.</summary>
public partial class ResultsScreen : CanvasLayer
{
    public MissionReport Report;
    public Action OnContinue;
    private VBoxContainer _card;
    private HBoxContainer _strip;
    private int _revealed;
    private int _viewing = -1;
    private float _timer = 1.6f;
    private Button _revealAll, _continue;

    public override void _Ready()
    {
        Layer = 30;
        var root = new Control { Theme = UiKit.Theme };
        UiKit.FillRect(root);
        AddChild(root);
        root.AddChild(UiKit.Scrim(0.9f));

        var top = UiKit.VBox(4);
        top.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        top.Position = new Vector2(top.Position.X, 64);
        top.GrowHorizontal = Control.GrowDirection.Both;
        var meta = UiKit.Eyebrow($"{Report.MissionName} · {Difficulties.Names[(int)Report.Difficulty]} · {UiKit.Time(Report.Elapsed)}");
        meta.HorizontalAlignment = HorizontalAlignment.Center;
        top.AddChild(meta);
        var title = UiKit.Heading(Report.Success ? "Mission accomplie" : "Mission échouée", 72);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeColorOverride("font_color", Report.Success ? UiKit.Text : UiKit.Danger);
        top.AddChild(title);
        if (!Report.Success)
        {
            var l = UiKit.Label("Les armes ramassées sont conservées. L'armure est réduite de moitié, les crédits sont perdus.", 15, UiKit.Muted);
            l.HorizontalAlignment = HorizontalAlignment.Center;
            top.AddChild(l);
        }
        root.AddChild(top);

        _card = UiKit.VBox(8);
        _card.CustomMinimumSize = new Vector2(620, 0);
        root.AddChild(_card);
        _card.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        _card.GrowHorizontal = Control.GrowDirection.Both;
        _card.GrowVertical = Control.GrowDirection.Both;
        _card.Position += new Vector2(0, 10);

        // Escouade (multijoueur) : éliminations par soldat, en haut à gauche.
        if (Report.Squad.Count > 1)
        {
            var sq = UiKit.VBox(6);
            sq.Position = new Vector2(UiKit.ScreenX, 220);
            sq.AddChild(UiKit.Eyebrow("Escouade"));
            foreach (var row in Report.Squad)
            {
                var h = UiKit.HBox(12);
                var n = UiKit.Label(row.name, 16, row.me ? UiKit.Accent : UiKit.Text);
                n.CustomMinimumSize = new Vector2(170, 0);
                h.AddChild(n);
                var k = UiKit.Label($"{row.kills}", 16, UiKit.Text, true);
                k.CustomMinimumSize = new Vector2(40, 0);
                h.AddChild(k);
                h.AddChild(UiKit.Label(row.cls, 13, UiKit.Muted));
                sq.AddChild(h);
            }
            root.AddChild(sq);
        }

        _strip = UiKit.HBox(8);
        root.AddChild(_strip);
        _strip.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        _strip.GrowHorizontal = Control.GrowDirection.Both;
        _strip.Position += new Vector2(0, -140);
        for (var i = 0; i < Report.Weapons.Count; i++)
        {
            var idx = i;
            var b = new Button { CustomMinimumSize = new Vector2(34, 8), FocusMode = Control.FocusModeEnum.None, TooltipText = "" };
            var sb = UiKit.Box(UiKit.Line, default, 0, 0, 0, 0);
            b.AddThemeStyleboxOverride("normal", sb);
            b.AddThemeStyleboxOverride("hover", sb);
            b.AddThemeStyleboxOverride("pressed", sb);
            b.Pressed += () => { if (idx < _revealed) Show(idx); };
            _strip.AddChild(b);
        }

        // Pied : totaux à gauche, actions à droite
        var foot = UiKit.HBox(40);
        root.AddChild(foot);
        foot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        foot.Position = new Vector2(UiKit.ScreenX, foot.Position.Y - UiKit.ScreenBottom - 40);
        foot.AddChild(Total($"{Report.Kills}", "éliminations", UiKit.Text));
        foot.AddChild(Total($"+{Report.ArmorGained:0}", "PV d'armure", UiKit.Info));
        foot.AddChild(Total($"+{Report.CreditsGained}", "crédits", UiKit.Gold));
        foot.AddChild(Total($"{Report.Weapons.Count}", "armes", UiKit.Text));
        var cls = Profile.I.Data.SelectedClass;
        var lvl = UiKit.VBox(4);
        lvl.AddChild(UiKit.Label(Report.LevelAfter > Report.LevelBefore ? $"Niveau {Report.LevelAfter} !" : $"Niveau {Report.LevelAfter}", 30, Report.LevelAfter > Report.LevelBefore ? UiKit.Accent : UiKit.Text, true));
        var xpBar = new ProgressBar { ShowPercentage = false, MaxValue = 1, Value = Profile.I.LevelProgress(cls), CustomMinimumSize = new Vector2(180, 4) };
        xpBar.AddThemeStyleboxOverride("fill", UiKit.Box(UiKit.Accent, default, 0, 0, 0, 0));
        lvl.AddChild(xpBar);
        lvl.AddChild(UiKit.Label($"+{Report.XpGained} XP · {Classes.Get(cls).Name}", 13, UiKit.Muted));
        foot.AddChild(lvl);
        if (Report.AutoCollected > 0) foot.AddChild(Total($"{Report.AutoCollected}", "caisses auto-collectées", UiKit.Muted));

        var actions = UiKit.HBox(12);
        root.AddChild(actions);
        actions.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        actions.GrowHorizontal = Control.GrowDirection.Begin;
        actions.GrowVertical = Control.GrowDirection.Begin;
        actions.Position += new Vector2(-UiKit.ScreenX, -UiKit.ScreenBottom);
        _revealAll = UiKit.Button("Tout révéler", RevealAll, null, 60);
        _revealAll.CustomMinimumSize = new Vector2(180, 60);
        actions.AddChild(_revealAll);
        _continue = UiKit.Button(Net.I.IsHost ? "Continuer" : "En attente de l'hôte", () => OnContinue?.Invoke(), "Primary", 60);
        _continue.Disabled = !Net.I.IsHost;
        actions.AddChild(_continue);
        _revealAll.Visible = Report.Weapons.Count > 0;
        if (Report.Weapons.Count == 0)
        {
            var none = UiKit.Label("Aucune caisse d'arme récupérée.", 17, UiKit.Muted);
            none.HorizontalAlignment = HorizontalAlignment.Center;
            _card.AddChild(none);
        }
        _continue.CallDeferred(Control.MethodName.GrabFocus);
    }

    private static Control Total(string value, string label, Color c)
    {
        var v = UiKit.VBox(0);
        v.AddChild(UiKit.Label(value, 30, c, true));
        v.AddChild(UiKit.Label(label, 13, UiKit.Muted));
        return v;
    }

    public override void _Process(double delta)
    {
        if (_revealed >= Report.Weapons.Count) { _revealAll.Visible = false; return; }
        _timer -= (float)delta;
        if (_timer > 0) return;
        _timer = 1.1f;
        RevealNext(true);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_accept") && _revealed < Report.Weapons.Count) { RevealAll(); GetViewport().SetInputAsHandled(); }
    }

    private void RevealAll()
    {
        while (_revealed < Report.Weapons.Count) RevealNext(false);
        // Montre la plus rare.
        Show(Report.Weapons.Count - 1);
    }

    private void RevealNext(bool sound)
    {
        var i = _revealed++;
        var w = Report.Weapons[i].weapon;
        var b = (Button)_strip.GetChild(i);
        var sb = UiKit.Box(Rarities.Colors[(int)w.Rarity], default, 0, 0, 0, 0);
        b.AddThemeStyleboxOverride("normal", sb);
        b.AddThemeStyleboxOverride("hover", sb);
        b.AddThemeStyleboxOverride("pressed", sb);
        b.TooltipText = $"{w.T.Name} Nv.{w.Level}";
        if (sound) Show(i);
        if (sound) Sfx.Ui(w.Rarity >= Rarity.Elite ? "pickup_weapon" : "pickup", w.Rarity >= Rarity.Prototype ? 2f : -4f, 0.8f + 0.1f * (int)w.Rarity);
    }

    private void Show(int i)
    {
        _viewing = i;
        foreach (var c in _card.GetChildren()) c.QueueFree();
        var (w, upgraded, stat) = Report.Weapons[i];
        var rc = Rarities.Colors[(int)w.Rarity];
        string tag;
        if (!upgraded) tag = "Nouvelle arme";
        else if (stat >= 0) tag = $"Doublon · {WeaponInstance.StatNames[stat]} +1 ★";
        else tag = "Doublon au maximum · converti en crédits";
        var e = UiKit.Eyebrow($"{Rarities.Names[(int)w.Rarity]} · {tag}", rc);
        e.HorizontalAlignment = HorizontalAlignment.Center;
        _card.AddChild(e);
        var n = UiKit.Heading($"{w.T.Name}  Nv.{w.Level}", 46);
        n.HorizontalAlignment = HorizontalAlignment.Center;
        _card.AddChild(n);
        var cat = UiKit.Label($"{w.T.Category} · {UiKit.Stars(Mathf.RoundToInt(w.TotalStars / 5f))}", 16, UiKit.Muted);
        cat.HorizontalAlignment = HorizontalAlignment.Center;
        _card.AddChild(cat);
        var bar = new ColorRect { Color = rc, CustomMinimumSize = new Vector2(520, 3), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        _card.AddChild(UiKit.Gap(10));
        _card.AddChild(bar);
        _card.AddChild(UiKit.Gap(10));

        var cls = w.T.Class;
        var eq = Profile.I.Equipped(cls).FirstOrDefault(x => x != w && x.T.Category == w.T.Category);
        void Line(string name, float v, float? c, string fmt, bool lowerBetter = false)
        {
            var h = UiKit.HBox(16);
            h.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            h.CustomMinimumSize = new Vector2(520, 28);
            var l = UiKit.Label(name, 16, UiKit.Muted);
            l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            h.AddChild(l);
            var val = UiKit.Label(string.Format(fmt, v), 17, UiKit.Text);
            val.AddThemeFontOverride("font", UiKit.BodyMedium);
            h.AddChild(val);
            var d = UiKit.Label("", 14, UiKit.Muted);
            d.CustomMinimumSize = new Vector2(90, 0);
            d.HorizontalAlignment = HorizontalAlignment.Right;
            if (c.HasValue && string.Format(fmt, Mathf.Abs(v - c.Value)) != string.Format(fmt, 0f))
            {
                var better = lowerBetter ? v < c.Value : v > c.Value;
                d.Text = (v > c.Value ? "▲ " : "▼ ") + string.Format(fmt, Mathf.Abs(v - c.Value));
                d.AddThemeColorOverride("font_color", better ? UiKit.Good : UiKit.Danger);
            }
            h.AddChild(d);
            _card.AddChild(h);
        }
        Line("DPS", w.Dps, eq?.Dps, "{0:0}");
        Line("Dégâts", w.Damage * w.T.Pellets, eq != null ? eq.Damage * eq.T.Pellets : null, "{0:0}");
        Line("Rechargement", w.Reload, eq?.Reload, "{0:0.0} s", true);
        Line("Portée", w.Range, eq?.Range, "{0:0} m");
        if (eq != null)
        {
            var cmp = UiKit.Label($"comparé à {eq.T.Name} Nv.{eq.Level} (équipée)", 13, UiKit.Dim);
            cmp.HorizontalAlignment = HorizontalAlignment.Center;
            _card.AddChild(cmp);
        }
        _card.Modulate = new Color(1, 1, 1, 0);
        var tw = _card.CreateTween();
        tw.TweenProperty(_card, "modulate:a", 1f, 0.18f);
    }
}
