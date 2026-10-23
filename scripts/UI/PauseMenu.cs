using Godot;

namespace Rempart;

/// <summary>Menu pause en mission (le jeu continue en multijoueur).</summary>
public partial class PauseMenu : CanvasLayer
{
    public World World;
    private Control _root;

    public override void _Ready()
    {
        Layer = 20;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
        _root = new Control { Theme = UiKit.Theme };
        UiKit.FillRect(_root);
        AddChild(_root);
        _root.AddChild(UiKit.Scrim(0.6f));
        var m = UiKit.Margin(96, 0, 0, 0);
        UiKit.FillRect(m);
        _root.AddChild(m);
        var v = UiKit.VBox(6);
        v.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        m.AddChild(v);
        v.AddChild(UiKit.Eyebrow(Net.I.IsSolo ? "Jeu en pause" : "Mission en cours — le jeu continue", UiKit.Accent));
        v.AddChild(UiKit.Heading(World.Mission.Name, 52));
        v.AddChild(UiKit.Label($"{Difficulties.Names[(int)World.Difficulty]} · {Classes.Get(Profile.I.Data.SelectedClass).Name}", 17, UiKit.Muted));
        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });
        v.AddChild(UiKit.Button("Reprendre", () => World.TogglePause(), "MenuItem", 52));
        v.AddChild(UiKit.Button("Options", OpenOptions, "MenuItem", 52));
        v.AddChild(UiKit.Button(Net.I.IsHost ? "Abandonner la mission" : "Quitter la partie", Abandon, "MenuItem", 52));
    }

    private void OpenOptions()
    {
        var o = new OptionsMenu { OnClose = () => { } };
        _root.AddChild(o);
    }

    private void Abandon()
    {
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
        if (true)
        {
            // L'écran d'options se ferme lui-même ; sinon on reprend.
            foreach (var c in _root.GetChildren())
                if (c is OptionsMenu) return;
            World.TogglePause();
            GetViewport().SetInputAsHandled();
        }
    }
}
