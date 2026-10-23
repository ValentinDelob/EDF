using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Menu principal : liste verticale sobre sur le décor 3D, résumé du profil en bas à droite.</summary>
public partial class MainMenu : Control
{
    private Label _notice;

    public override void _Ready()
    {
        Theme = UiKit.Theme;
        UiKit.FillRect(this);
        // Dégradé à gauche pour la lisibilité du menu sur le décor.
        var shade = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient { Colors = new[] { new Color(0.02f, 0.025f, 0.03f, 0.92f), new Color(0.02f, 0.025f, 0.03f, 0f) }, Offsets = new[] { 0.25f, 1f } },
                FillFrom = new Vector2(0, 0), FillTo = new Vector2(1, 0), Width = 256, Height = 4,
            },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            AnchorBottom = 1,
            OffsetRight = 900,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(shade);

        var m = UiKit.Margin(96, 88, 96, 64);
        UiKit.FillRect(m);
        AddChild(m);
        var col = UiKit.VBox(0);
        m.AddChild(col);

        col.AddChild(UiKit.Eyebrow("Force de défense terrestre", UiKit.Accent));
        var title = UiKit.Heading("REMPART", 104);
        title.AddThemeConstantOverride("line_spacing", -20);
        col.AddChild(title);
        col.AddChild(UiKit.Label($"Preuve de concept · v{ProjectSettings.GetSetting("application/config/version")}", 15, UiKit.Muted));
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 64) });

        var menu = UiKit.VBox(2);
        menu.CustomMinimumSize = new Vector2(420, 0);
        menu.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        var solo = UiKit.Button("Campagne solo", () => App.I.StartSolo(), "MenuItem", 56);
        menu.AddChild(solo);
        menu.AddChild(UiKit.Button("Héberger en LAN", () => App.I.Host(), "MenuItem", 56));
        menu.AddChild(UiKit.Button("Rejoindre en LAN", () => App.I.ShowJoin(), "MenuItem", 56));
        menu.AddChild(UiKit.Button("Arsenal", () => App.I.ShowArsenal(() => App.I.ShowMain()), "MenuItem", 56));
        menu.AddChild(UiKit.Button("Options", () => App.I.ShowOptions(), "MenuItem", 56));
        menu.AddChild(UiKit.Button("Quitter", () => App.I.Quit(), "MenuItem", 56));
        foreach (var b in menu.GetChildren().OfType<Button>()) b.Alignment = HorizontalAlignment.Left;
        col.AddChild(menu);
        col.AddChild(UiKit.Spacer());
        _notice = UiKit.Label("", 16, UiKit.Danger);
        col.AddChild(_notice);
        col.AddChild(UiKit.Label("Entrée : valider  ·  Échap : retour  ·  F12 : capture d'écran", 14, UiKit.Muted));
        solo.CallDeferred(Control.MethodName.GrabFocus);

        // Résumé du profil (bas droite)
        var p = Profile.I;
        var cls = p.Data.SelectedClass;
        var card = UiKit.VBox(4);
        card.AddChild(UiKit.Eyebrow("Profil"));
        card.AddChild(UiKit.Label(Settings.I.PlayerName, 26, UiKit.Text, true));
        card.AddChild(UiKit.Label($"{Classes.Get(cls).Name} · {p.Armor(cls):0} PV", 16, new Color(UiKit.Text, 0.85f)));
        var cleared = Missions.All.Count(mm => Enumerable.Range(0, 5).Any(d => p.IsCleared(mm.Index, (Difficulty)d)));
        card.AddChild(UiKit.Label($"{p.Data.Arsenal.Count} armes · {p.Data.Credits} crédits · {cleared}/{Missions.All.Length} missions", 15, UiKit.Muted));
        AddChild(card);
        card.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
        card.GrowHorizontal = GrowDirection.Begin;
        card.GrowVertical = GrowDirection.Begin;
        card.Position += new Vector2(-96, -64);
        foreach (var l in card.GetChildren().OfType<Label>()) l.HorizontalAlignment = HorizontalAlignment.Right;
    }

    public void Notice(string text) => _notice.Text = text ?? "";
}
