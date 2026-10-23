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
    private ColorRect _flash;
    private Control _navHints;
    private Tween _cardTween, _flashTween;
    private int _flashed = -1;

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

        // Flash plein écran (couleur selon les étoiles) à chaque révélation (derrière la fiche).
        _flash = new ColorRect { Color = new Color(1, 1, 1, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        UiKit.FillRect(_flash);
        root.AddChild(_flash);
        root.MoveChild(_flash, 1);

        // Bande de progression : un segment par arme, celui affiché est plus haut.
        _strip = UiKit.HBox(8);
        _strip.CustomMinimumSize = new Vector2(0, 12);
        root.AddChild(_strip);
        _strip.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom, Control.LayoutPresetMode.KeepSize);
        _strip.GrowHorizontal = Control.GrowDirection.Both;
        _strip.GrowVertical = Control.GrowDirection.Begin;
        _strip.Position += new Vector2(0, -168);
        for (var i = 0; i < Report.Weapons.Count; i++)
        {
            var idx = i;
            var b = new Button { CustomMinimumSize = new Vector2(34, 6), FocusMode = Control.FocusModeEnum.None, TooltipText = "", SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
            var sb = UiKit.Box(UiKit.Line, default, 0, 0, 0, 0);
            b.AddThemeStyleboxOverride("normal", sb);
            b.AddThemeStyleboxOverride("hover", sb);
            b.AddThemeStyleboxOverride("pressed", sb);
            b.Pressed += () => { if (idx < _revealed) Show(idx); };
            _strip.AddChild(b);
        }
        if (Report.Weapons.Count > 1)
        {
            var nav = UiKit.HBox(24);
            nav.AddChild(UiKit.KeyHint("← / →", "Parcourir"));
            nav.AddChild(UiKit.KeyHint("Entrée", "Tout révéler", RevealAll));
            _navHints = nav;
            root.AddChild(nav);
            nav.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom, Control.LayoutPresetMode.KeepSize);
            nav.GrowHorizontal = Control.GrowDirection.Both;
            nav.GrowVertical = Control.GrowDirection.Begin;
            nav.Position += new Vector2(0, -124);
        }

        // Pied : totaux à gauche (alignés sur le bas de l'action principale), actions à droite
        var foot = UiKit.HBox(40);
        root.AddChild(foot);
        foot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft, Control.LayoutPresetMode.KeepSize);
        foot.GrowVertical = Control.GrowDirection.Begin;
        foot.Position += new Vector2(UiKit.ScreenX, -UiKit.ScreenBottom);
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
        // Entrée révèle d'abord tout le butin, puis valide « Continuer » (jamais l'inverse).
        UiKit.FocusLater(Report.Weapons.Count > 0 ? _revealAll : _continue);
        UiKit.FadeIn(root, 0.2f);
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
        if (_revealed >= Report.Weapons.Count)
        {
            if (_revealAll.Visible)
            {
                var hadFocus = _revealAll.HasFocus();
                _revealAll.Visible = false;
                if (_navHints?.GetChild(1) is Control h) h.Visible = false;
                if (hadFocus || _continue.GetViewport()?.GuiGetFocusOwner() == null) UiKit.FocusSilently(_continue);
            }
            return;
        }
        _timer -= (float)delta;
        if (_timer > 0) return;
        _timer = 1.1f;
        RevealNext(true);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_accept") && _revealed < Report.Weapons.Count) { RevealAll(); GetViewport().SetInputAsHandled(); }
    }

    public override void _Input(InputEvent e)
    {
        // ← / → : parcourir les armes déjà révélées (avant la navigation de focus : Entrée suffit pour les boutons).
        var d = e.IsActionPressed("ui_left") ? -1 : e.IsActionPressed("ui_right") ? 1 : 0;
        if (d != 0 && _revealed > 0)
        {
            var i = Mathf.Clamp((_viewing < 0 ? 0 : _viewing) + d, 0, _revealed - 1);
            if (i != _viewing) { Show(i); Sfx.Ui("ui_hover", -10f); }
            GetViewport().SetInputAsHandled();
        }
    }

    private void RevealAll()
    {
        while (_revealed < Report.Weapons.Count) RevealNext(false);
        // Montre la plus étoilée (tri du bilan).
        Show(Report.Weapons.Count - 1);
    }

    private void RevealNext(bool sound)
    {
        var i = _revealed++;
        var w = Report.Weapons[i].weapon;
        var b = (Button)_strip.GetChild(i);
        var sb = UiKit.Box(Tier(w), default, 0, 0, 0, 0);
        b.AddThemeStyleboxOverride("normal", sb);
        b.AddThemeStyleboxOverride("hover", sb);
        b.AddThemeStyleboxOverride("pressed", sb);
        b.TooltipText = $"{w.T.Name} Nv.{w.Level}";
        if (sound) Show(i);
        if (sound) Sfx.Ui(StarFrac(w) >= 0.5f ? "pickup_weapon" : "pickup", StarFrac(w) >= 0.7f ? 2f : -4f, 0.8f + 0.4f * StarFrac(w));
    }

    /// <summary>Part des étoiles obtenues (0..1) : remplace l'ancienne rareté pour l'éclat de la révélation.</summary>
    private static float StarFrac(WeaponInstance w) => w.MaxTotalStars > 0 ? w.TotalStars / (float)w.MaxTotalStars : 0f;

    private static Color Tier(WeaponInstance w) => UiKit.Muted.Lerp(UiKit.Gold, StarFrac(w));

    private void Show(int i)
    {
        _viewing = i;
        foreach (var c in _card.GetChildren()) c.QueueFree();
        var (w, duplicate) = Report.Weapons[i];
        var rc = Tier(w);
        var e = UiKit.Eyebrow(duplicate ? $"{w.T.Category} · Doublon déjà possédé · converti en crédits" : $"{w.T.Category} · Nouvelle arme", rc);
        e.HorizontalAlignment = HorizontalAlignment.Center;
        _card.AddChild(e);
        var n = UiKit.Heading($"{w.T.Name}  Lv{w.Level}", 46);
        n.HorizontalAlignment = HorizontalAlignment.Center;
        _card.AddChild(n);
        var cat = UiKit.Label($"☆ {w.TotalStars} / {w.MaxTotalStars}", 16, UiKit.Muted);
        cat.HorizontalAlignment = HorizontalAlignment.Center;
        _card.AddChild(cat);
        var bar = new ColorRect { Color = rc, CustomMinimumSize = new Vector2(520, 3), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        _card.AddChild(UiKit.Gap(10));
        _card.AddChild(bar);
        _card.AddChild(UiKit.Gap(10));

        // Une ligne par caractéristique : (☆n) Nom : valeur (+bonus des étoiles) — jamais de comparaison.
        foreach (var s in System.Enum.GetValues<WeaponStat>())
        {
            if (!w.HasStat(s)) continue;
            var (value, bonus) = w.Describe(s);
            var h = UiKit.HBox(16);
            h.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            h.CustomMinimumSize = new Vector2(520, 28);
            h.AddChild(UiKit.Label($"(☆{w.StarsOf(s)})", 16, UiKit.Gold));
            var l = UiKit.Label(WeaponInstance.StatNames[(int)s], 16, UiKit.Muted);
            l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            h.AddChild(l);
            var val = UiKit.Label(value, 17, UiKit.Text);
            val.AddThemeFontOverride("font", UiKit.BodyMedium);
            h.AddChild(val);
            var d = UiKit.Label(w.StarsOf(s) > 0 ? $"({bonus})" : "", 14, UiKit.Good);
            d.CustomMinimumSize = new Vector2(90, 0);
            d.HorizontalAlignment = HorizontalAlignment.Right;
            h.AddChild(d);
            _card.AddChild(h);
        }
        // Segment affiché : plus haut et plein ; les autres révélés restent à la couleur de leurs étoiles.
        for (var k = 0; k < _strip.GetChildCount(); k++)
        {
            var pip = (Control)_strip.GetChild(k);
            pip.CustomMinimumSize = new Vector2(k == i ? 48 : 34, k == i ? 10 : 6);
            pip.Modulate = new Color(1, 1, 1, k == i || k >= _revealed ? 1f : 0.55f);
        }

        // Apparition : fondu + légère mise à l'échelle (≤ 250 ms), flash de rareté pour une arme fraîchement révélée.
        _cardTween?.Kill();
        _card.Modulate = new Color(1, 1, 1, 0);
        _card.PivotOffset = new Vector2(_card.Size.X / 2f, 120);
        _card.Scale = Vector2.One * 0.94f;
        _cardTween = _card.CreateTween().SetParallel();
        _cardTween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        _cardTween.TweenProperty(_card, "scale", Vector2.One, 0.22f);
        _cardTween.TweenProperty(_card, "modulate:a", 1f, 0.16f).SetTrans(Tween.TransitionType.Cubic);
        if (i == _revealed - 1 && i != _flashed)
        {
            _flashed = i;
            var strength = 0.10f + 0.16f * StarFrac(w);
            _flash.Color = new Color(rc, strength);
            _flashTween?.Kill();
            _flashTween = _flash.CreateTween();
            _flashTween.TweenProperty(_flash, "color:a", 0f, 0.25f).SetEase(Tween.EaseType.Out);
        }
    }
}
