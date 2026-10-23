using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Écran titre : logo + entrées texte à gauche sur la ville cinématique, profil discret en bas à droite.</summary>
public partial class MainMenu : Control
{
    private Label _notice;

    public override void _Ready()
    {
        Theme = UiKit.Theme;
        UiKit.FillRect(this);
        AddChild(UiKit.SideShade(900, 0.94f));

        var col = UiKit.VBox(0);
        col.Position = new Vector2(UiKit.ScreenX, 0);
        col.AnchorTop = 0.5f;
        col.AnchorBottom = 0.5f;
        col.GrowVertical = GrowDirection.Both;
        AddChild(col);

        col.AddChild(UiKit.Eyebrow("Force de défense terrestre", UiKit.Accent));
        var title = UiKit.Heading("Rempart", 112);
        title.AddThemeConstantOverride("line_spacing", -24);
        col.AddChild(title);
        col.AddChild(UiKit.Gap(48));

        var menu = UiKit.VBox(2);
        menu.CustomMinimumSize = new Vector2(440, 0);
        menu.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        var solo = UiKit.Button("Jouer en solo", () => App.I.StartSolo(), "MenuItem", 54);
        menu.AddChild(solo);
        menu.AddChild(UiKit.Button("Héberger (LAN)", () => App.I.Host(), "MenuItem", 54));
        menu.AddChild(UiKit.Button("Rejoindre (LAN)", () => App.I.ShowJoin(), "MenuItem", 54));
        menu.AddChild(UiKit.Button("Arsenal", () => App.I.ShowArsenal(() => App.I.ShowMain()), "MenuItem", 54));
        menu.AddChild(UiKit.Button("Options", () => App.I.ShowOptions(), "MenuItem", 54));
        menu.AddChild(UiKit.Button("Guide", () => App.I.ShowGuide(), "MenuItem", 54));
        var quit = UiKit.Button("Quitter", () => App.I.Quit(), null, 40);
        quit.ThemeTypeVariation = "MenuItem";
        quit.AddThemeFontOverride("font", UiKit.BodyFont);
        quit.AddThemeFontSizeOverride("font_size", 16);
        quit.Alignment = HorizontalAlignment.Left;
        menu.AddChild(UiKit.Gap(8));
        menu.AddChild(quit);
        col.AddChild(menu);
        col.AddChild(UiKit.Gap(24));
        _notice = UiKit.Label("", 15, UiKit.Danger);
        col.AddChild(_notice);
        solo.CallDeferred(Control.MethodName.GrabFocus);

        var hints = UiKit.HBox(28);
        hints.AddChild(UiKit.KeyHint("Entrée", "Valider"));
        hints.AddChild(UiKit.KeyHint("F12", "Capture d'écran"));
        AddChild(hints);
        hints.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
        hints.Position = new Vector2(UiKit.ScreenX, hints.Position.Y - UiKit.ScreenBottom);

        // Profil (bas-droite, discret)
        var p = Profile.I;
        var cls = p.Data.SelectedClass;
        var card = UiKit.VBox(2);
        var name = UiKit.Label(Settings.I.PlayerName, 20, UiKit.Text, true);
        card.AddChild(name);
        card.AddChild(UiKit.Label($"{Classes.Get(cls).Name} niv. {p.Level(cls)} · {p.Armor(cls):0} PV · {p.Data.Credits} crédits", 14, UiKit.Muted));
        var cleared = Missions.All.Count(mm => Enumerable.Range(0, 5).Any(d => p.IsCleared(mm.Index, (Difficulty)d)));
        card.AddChild(UiKit.Label($"{cleared}/{Missions.All.Length} missions · {p.Data.Arsenal.Count} armes · v{ProjectSettings.GetSetting("application/config/version")}", 13, UiKit.Dim));
        AddChild(card);
        card.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        card.GrowHorizontal = GrowDirection.Begin;
        card.GrowVertical = GrowDirection.Begin;
        card.Position += new Vector2(-UiKit.ScreenX, -UiKit.ScreenBottom);
        foreach (var l in card.GetChildren().OfType<Label>()) l.HorizontalAlignment = HorizontalAlignment.Right;
    }

    public void Notice(string text) => _notice.Text = text ?? "";
}
