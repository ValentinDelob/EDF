using System;
using Godot;

namespace Rempart;

/// <summary>Bilan de mission : statistiques puis révélation des armes une par une.</summary>
public partial class ResultsScreen : CanvasLayer
{
    public MissionReport Report;
    public Action OnContinue;
    private GridContainer _grid;
    private int _revealed;
    private float _timer = 1.2f;
    private Button _revealAll, _continue;
    private Label _waiting;

    public override void _Ready()
    {
        Layer = 30;
        var root = new Control { Theme = UiKit.Theme };
        UiKit.FillRect(root);
        AddChild(root);
        root.AddChild(UiKit.Scrim(0.88f));
        var m = UiKit.Margin(96, 72, 96, 56);
        UiKit.FillRect(m);
        root.AddChild(m);
        var v = UiKit.VBox(28);
        m.AddChild(v);

        var head = UiKit.HBox(48);
        var title = UiKit.VBox(4);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.AddChild(UiKit.Eyebrow($"{Report.MissionName} · {Difficulties.Names[(int)Report.Difficulty]}"));
        title.AddChild(UiKit.Heading(Report.Success ? "Mission accomplie" : "Mission échouée", 60));
        ((Label)title.GetChild(1)).AddThemeColorOverride("font_color", Report.Success ? UiKit.Good : UiKit.Danger);
        head.AddChild(title);
        head.AddChild(Stat("Durée", UiKit.Time(Report.Elapsed), UiKit.Text));
        head.AddChild(Stat("Éliminations", Report.Kills.ToString(), UiKit.Text));
        head.AddChild(Stat("Armure", $"+{Report.ArmorGained:0} PV", UiKit.Good));
        head.AddChild(Stat("Crédits", $"+{Report.CreditsGained}", new Color("ffcc26")));
        v.AddChild(head);
        if (!Report.Success)
            v.AddChild(UiKit.Label("Échec : les armes ramassées sont conservées, l'armure est réduite de moitié et les crédits sont perdus.", 16, UiKit.Muted));
        if (Report.AutoCollected > 0)
            v.AddChild(UiKit.Label($"{Report.AutoCollected} caisses restantes collectées automatiquement.", 16, UiKit.Muted));

        v.AddChild(UiKit.Eyebrow($"Armes récupérées · {Report.Weapons.Count}"));
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 14);
        _grid.AddThemeConstantOverride("v_separation", 14);
        scroll.AddChild(_grid);
        v.AddChild(scroll);
        if (Report.Weapons.Count == 0) _grid.AddChild(UiKit.Label("Aucune caisse d'arme ramassée.", 16, UiKit.Muted));

        var foot = UiKit.HBox(12);
        _waiting = UiKit.Label("", 16, UiKit.Muted);
        _waiting.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        foot.AddChild(_waiting);
        _revealAll = UiKit.Button("Tout révéler", RevealAll, null, 56);
        _revealAll.CustomMinimumSize = new Vector2(200, 56);
        foot.AddChild(_revealAll);
        _continue = UiKit.Button(Net.I.IsHost ? "Retour au salon" : "Retour au salon (hôte)", () => OnContinue?.Invoke(), "Primary", 56);
        _continue.CustomMinimumSize = new Vector2(280, 56);
        foot.AddChild(_continue);
        v.AddChild(foot);
        if (!Net.I.IsHost)
        {
            _continue.Disabled = true;
            _waiting.Text = "En attente de l'hôte pour revenir au salon…";
        }
        _revealAll.Visible = Report.Weapons.Count > 0;
    }

    private static Control Stat(string label, string value, Color c)
    {
        var v = UiKit.VBox(0);
        v.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        var l = UiKit.Label(label, 14, UiKit.Muted);
        l.HorizontalAlignment = HorizontalAlignment.Right;
        var val = UiKit.Label(value, 32, c, true);
        val.HorizontalAlignment = HorizontalAlignment.Right;
        v.AddChild(l);
        v.AddChild(val);
        return v;
    }

    public override void _Process(double delta)
    {
        if (_revealed >= Report.Weapons.Count) { _revealAll.Visible = false; return; }
        _timer -= (float)delta;
        if (_timer > 0) return;
        _timer = 0.55f;
        RevealNext();
    }

    private void RevealAll()
    {
        while (_revealed < Report.Weapons.Count) RevealNext(false);
    }

    private void RevealNext(bool sound = true)
    {
        var (w, upgraded, stat) = Report.Weapons[_revealed++];
        var color = Rarities.Colors[(int)w.Rarity];
        var card = new PanelContainer();
        var style = UiKit.Box(UiKit.Surface, UiKit.Line, 1, 6, 18, 14);
        style.BorderWidthTop = 3;
        style.BorderColor = color;
        card.AddThemeStyleboxOverride("panel", style);
        card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        card.CustomMinimumSize = new Vector2(320, 0);
        var v = UiKit.VBox(6);
        card.AddChild(v);
        v.AddChild(UiKit.Label(Rarities.Names[(int)w.Rarity].ToUpperInvariant(), 13, color, true));
        v.AddChild(UiKit.Label($"{w.T.Name}  Nv.{w.Level}", 19));
        string sub;
        if (!upgraded) sub = $"{w.T.Category} · Nouvelle arme";
        else if (stat >= 0) sub = $"Doublon · {WeaponInstance.StatNames[stat]} +1 ★";
        else sub = "Doublon déjà au maximum · converti en crédits";
        v.AddChild(UiKit.Label(sub, 14, UiKit.Muted));
        v.AddChild(UiKit.Label($"DPS {w.Dps:0}  ·  {UiKit.Stars(Mathf.RoundToInt(w.TotalStars / 5f))}", 14, new Color(UiKit.Text, 0.8f)));
        _grid.AddChild(card);
        card.Modulate = new Color(1, 1, 1, 0);
        card.CreateTween().TweenProperty(card, "modulate:a", 1f, 0.25f);
        if (sound) Sfx.Ui(w.Rarity >= Rarity.Elite ? "pickup_weapon" : "pickup", w.Rarity >= Rarity.Prototype ? 2f : -4f, 0.8f + 0.1f * (int)w.Rarity);
    }
}
