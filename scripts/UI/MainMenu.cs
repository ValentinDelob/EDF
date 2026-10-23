using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Écran titre : logo + entrées texte à gauche sur la ville cinématique, profil discret en bas à droite.</summary>
public partial class MainMenu : Control
{
    private Label _notice;
    private Button _quit;

    public override void _Ready()
    {
        Theme = UiKit.Theme;
        UiKit.FillRect(this);
        AddChild(UiKit.SideShade(900, 0.94f));
        // Fenêtre basse (720 px) : entrées plus serrées pour garder de l'air au-dessus de la barre d'aide.
        var compact = GetViewport().GetVisibleRect().Size.Y < 800;
        var h = compact ? 48 : 54;

        var col = UiKit.VBox(0);
        col.Position = new Vector2(UiKit.ScreenX, -16);
        col.AnchorTop = 0.5f;
        col.AnchorBottom = 0.5f;
        col.GrowVertical = GrowDirection.Both;
        AddChild(col);

        col.AddChild(UiKit.Eyebrow("Force de défense terrestre", UiKit.Accent));
        var title = UiKit.Heading("Rempart", compact ? 96 : 112);
        title.AddThemeConstantOverride("line_spacing", -24);
        col.AddChild(title);
        col.AddChild(UiKit.Gap(compact ? 24 : 48));

        var menu = UiKit.VBox(2);
        menu.CustomMinimumSize = new Vector2(440, 0);
        menu.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        var solo = UiKit.Button("Jouer en solo", () => App.I.StartSolo(), "MenuItem", h);
        menu.AddChild(solo);
        menu.AddChild(UiKit.Button("Héberger (LAN)", () => App.I.Host(), "MenuItem", h));
        menu.AddChild(UiKit.Button("Rejoindre (LAN)", () => App.I.ShowJoin(), "MenuItem", h));
        menu.AddChild(UiKit.Button("Arsenal", () => App.I.ShowArsenal(() => App.I.ShowMain()), "MenuItem", h));
        menu.AddChild(UiKit.Button("Options", () => App.I.ShowOptions(), "MenuItem", h));
        menu.AddChild(UiKit.Button("Guide", () => App.I.ShowGuide(), "MenuItem", h));
        _quit = UiKit.Button("Quitter", () => App.I.Quit(), "MenuItem", 40);
        _quit.Text = "Quitter";
        _quit.AddThemeFontOverride("font", UiKit.BodyMedium);
        _quit.AddThemeFontSizeOverride("font_size", 17);
        menu.AddChild(UiKit.Gap(8));
        menu.AddChild(_quit);
        col.AddChild(menu);
        col.AddChild(UiKit.Gap(24));
        _notice = UiKit.Label("", 15, UiKit.Danger);
        _notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _notice.CustomMinimumSize = new Vector2(440, 0);
        col.AddChild(_notice);
        // Navigation clavier / manette : la liste boucle de « Quitter » vers « Jouer en solo ».
        UiKit.ChainVertical(menu.GetChildren().OfType<Button>().ToArray());
        UiKit.FocusLater(solo);

        var hints = UiKit.HBox(28);
        hints.AddChild(UiKit.KeyHint("Entrée", "Valider"));
        hints.AddChild(UiKit.KeyHint("Échap", "Quitter", () => UiKit.FocusSilently(_quit)));
        hints.AddChild(UiKit.KeyHint("F12", "Capture d'écran"));
        AddChild(hints);
        hints.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft, LayoutPresetMode.KeepSize);
        hints.GrowVertical = GrowDirection.Begin;
        hints.Position += new Vector2(UiKit.ScreenX, -UiKit.ScreenBottom);

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
        UiKit.FadeIn(this);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // Échap : place le focus sur « Quitter » (une seconde validation quitte), sans fermer brutalement.
        if (!e.IsActionPressed("ui_cancel")) return;
        UiKit.FocusSilently(_quit);
        GetViewport().SetInputAsHandled();
    }

    public void Notice(string text) => _notice.Text = text ?? "";
}
