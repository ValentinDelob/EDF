using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Pause : liste à gauche, mission et escouade à droite. En multijoueur, le jeu continue.</summary>
public partial class PauseMenu : CanvasLayer
{
    public World World;
    private Control _root;
    private Button _resume;
    private VBoxContainer _info;
    private Button _abandon;
    private Label _abandonHint;
    private bool _confirmAbandon;

    public override void _Ready()
    {
        Layer = 20;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
        _root = new Control { Theme = UiKit.Theme };
        UiKit.FillRect(_root);
        AddChild(_root);
        _root.AddChild(UiKit.Scrim(0.6f));
        _root.AddChild(UiKit.SideShade(760, 0.85f));

        var left = UiKit.VBox(2);
        left.Position = new Vector2(UiKit.ScreenX, 0);
        left.AnchorTop = 0.5f;
        left.AnchorBottom = 0.5f;
        left.GrowVertical = Control.GrowDirection.Both;
        _root.AddChild(left);
        left.AddChild(UiKit.Eyebrow(Net.I.IsSolo ? "Pause" : "Le jeu continue pour l'escouade", UiKit.Accent));
        left.AddChild(UiKit.Heading(World.Mission.Name, 56));
        left.AddChild(UiKit.Gap(32));
        _resume = UiKit.Button("Reprendre", () => World.TogglePause(), "MenuItem", 54);
        left.AddChild(_resume);
        var options = UiKit.Button("Options", OpenOptions, "MenuItem", 54);
        left.AddChild(options);
        _abandon = UiKit.Button(AbandonText, Abandon, "MenuItem", 54);
        _abandon.FocusExited += ResetAbandon;
        _abandon.MouseExited += ResetAbandon;
        left.AddChild(_abandon);
        _abandonHint = UiKit.Label("", 14, UiKit.Danger);
        _abandonHint.CustomMinimumSize = new Vector2(0, 20);
        left.AddChild(UiKit.Gap(4));
        left.AddChild(_abandonHint);
        UiKit.ChainVertical(_resume, options, _abandon);

        _info = UiKit.VBox(6);
        _info.CustomMinimumSize = new Vector2(420, 0);
        _root.AddChild(_info);
        _info.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterRight);
        _info.GrowHorizontal = Control.GrowDirection.Begin;
        _info.GrowVertical = Control.GrowDirection.Both;
        _info.Position += new Vector2(-UiKit.ScreenX, 0);

        var hint = UiKit.KeyHint("Échap", "Reprendre", () => World.TogglePause());
        _root.AddChild(hint);
        hint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft, Control.LayoutPresetMode.KeepSize);
        hint.GrowVertical = Control.GrowDirection.Begin;
        hint.Position += new Vector2(UiKit.ScreenX, -UiKit.ScreenBottom);
        VisibilityChanged += OnShown;
    }

    private string AbandonText => Net.I.IsHost ? "Abandonner la mission" : "Quitter la partie";

    private void ResetAbandon()
    {
        if (!_confirmAbandon) return;
        _confirmAbandon = false;
        _abandon.Text = AbandonText.ToUpperInvariant();
        _abandon.RemoveThemeColorOverride("font_focus_color");
        _abandon.RemoveThemeColorOverride("font_hover_color");
        _abandonHint.Text = "";
    }

    private void OnShown()
    {
        // Le HUD est masqué derrière la pause (sinon le briefing et la vie transparaissent sous le menu).
        if (World.Hud != null) World.Hud.Visible = !Visible && !World.Ended;
        if (!Visible) return;
        ResetAbandon();
        UiKit.FadeIn(_root, 0.12f);
        UiKit.FocusLater(_resume);
        foreach (var c in _info.GetChildren()) c.QueueFree();
        _info.AddChild(UiKit.Eyebrow("Mission"));
        _info.AddChild(UiKit.Label(World.ObjectiveText, 18));
        _info.AddChild(UiKit.Label($"{Difficulties.Names[(int)World.Difficulty]} · {UiKit.Time(World.Elapsed)} · {World.TeamKills} éliminations", 15, UiKit.Muted));
        _info.AddChild(UiKit.Gap(20));
        _info.AddChild(UiKit.Eyebrow("Escouade"));
        var me = World.LocalPlayer;
        if (me != null) _info.AddChild(SquadRow(Settings.I.PlayerName, me.Def.Name, me.Health / me.MaxHealth, me.Downed));
        foreach (var r in World.Remotes.Values)
            _info.AddChild(SquadRow(r.PlayerName, Classes.Get(r.Class).Name, r.Health / Mathf.Max(r.MaxHealth, 1), r.Downed));
        _info.AddChild(UiKit.Gap(20));
        _info.AddChild(UiKit.Eyebrow("Butin de l'équipe"));
        _info.AddChild(UiKit.Label(string.Join("   ", Enumerable.Range(0, 4).Select(i => $"{Crates.Names[i]} {World.CratesCollected[i]}")), 15, UiKit.Muted));
    }

    private static Control SquadRow(string name, string cls, float hp, bool downed)
    {
        var v = UiKit.VBox(4);
        var h = UiKit.HBox();
        var n = UiKit.Label(name, 17);
        n.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        h.AddChild(n);
        h.AddChild(UiKit.Label(downed ? "À TERRE" : cls, 14, downed ? UiKit.Danger : UiKit.Muted));
        v.AddChild(h);
        var bar = new ProgressBar { ShowPercentage = false, MaxValue = 1, Value = hp, CustomMinimumSize = new Vector2(0, 4) };
        v.AddChild(bar);
        return v;
    }

    private void OpenOptions()
    {
        var o = new OptionsMenu { OnClose = () => UiKit.FocusLater(_resume) };
        _root.AddChild(o);
    }

    private void Abandon()
    {
        // Confirmation en deux temps (pas de fenêtre modale empilée).
        if (!_confirmAbandon)
        {
            _confirmAbandon = true;
            _abandon.Text = "CONFIRMER ?";
            _abandon.AddThemeColorOverride("font_focus_color", UiKit.Danger);
            _abandon.AddThemeColorOverride("font_hover_color", UiKit.Danger);
            _abandonHint.Text = Net.I.IsHost ? "La mission sera comptée comme un échec." : "Vous quitterez la partie en cours.";
            return;
        }
        _confirmAbandon = false;
        if (Net.I.IsHost) World.EndMission(false);
        else
        {
            Net.I.SendLeftMission();
            Net.I.Leave();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!e.IsActionPressed("pause")) return;
        if (!Visible)
        {
            if (!World.Ended) { World.TogglePause(); GetViewport().SetInputAsHandled(); }
            return;
        }
        foreach (var c in _root.GetChildren())
            if (c is OptionsMenu) return;
        World.TogglePause();
        GetViewport().SetInputAsHandled();
    }
}
