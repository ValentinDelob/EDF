using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;

namespace Rempart;

/// <summary>
/// Pause (maquette E-Pause) : la scène de jeu désaturée et assombrie en fond, plaques Reprendre / Options /
/// Abandonner à gauche, panneau mission (objectif, phase, difficulté, temps, éliminations, ennemis restants)
/// et panneau escouade. En multijoueur, le jeu continue : les panneaux se mettent à jour.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    public World World;
    private Control _root;
    private EPlate _resume, _abandon;
    private Label _abandonHint;
    private bool _confirmAbandon;
    private Label _missionHead, _objective, _phase, _diff, _time, _kills, _left;
    private HBoxContainer _phaseBoxes;
    private Control _phaseRow;
    private SquadPanel _squad;
    private ShaderMaterial _bgMat;
    private float _refresh;

    private const string BgShader = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float cell = 28.0;
uniform float line_w = 1.0;
void fragment() {
    vec3 c = texture(screen_tex, SCREEN_UV).rgb;
    float l = dot(c, vec3(0.299, 0.587, 0.114));
    c = mix(vec3(l), c, 0.6) * 0.55;
    float x = UV.x;
    float a = x < 0.45 ? mix(0.82, 0.55, x / 0.45) : mix(0.55, 0.7, (x - 0.45) / 0.55);
    c = mix(c, vec3(4.0, 30.0, 14.0) / 255.0, a);
    vec2 g = mod(FRAGCOORD.xy, cell);
    if (g.x < line_w || g.y < line_w) c = mix(c, vec3(110.0, 255.0, 160.0) / 255.0, 0.07);
    COLOR = vec4(c, 1.0);
}";

    public override void _Ready()
    {
        Layer = 20;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
        _root = new Control { Theme = UiE.Theme, MouseFilter = Control.MouseFilterEnum.Stop };
        UiKit.FillRect(_root);
        AddChild(_root);
        _bgMat = new ShaderMaterial { Shader = new Shader { Code = BgShader } };
        var bg = new ColorRect { Material = _bgMat, MouseFilter = Control.MouseFilterEnum.Ignore };
        UiKit.FillRect(bg);
        _root.AddChild(bg);

        var stage = EFrame.Build(_root, "Pause", out _, backdrop: false, accent: false, dark: 0.86f);

        // Plaques.
        var col = new VBoxContainer { Position = new Vector2(110, 140), Size = new Vector2(360, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 10);
        stage.AddChild(col);
        _resume = new EPlate("Reprendre", () => World.TogglePause(), 320) { Icon = UiE.Icon.Play };
        var options = new EPlate("Options", OpenOptions, 320) { Icon = UiE.Icon.Gear };
        _abandon = new EPlate(AbandonText, Abandon, 320) { Icon = UiE.Icon.Exit, Danger = true };
        _abandon.FocusExited += ResetAbandon;
        _abandon.MouseExited += ResetAbandon;
        col.AddChild(_resume);
        col.AddChild(options);
        col.AddChild(_abandon);
        _abandonHint = UiE.Label("", 16, UiE.Danger);
        _abandonHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _abandonHint.CustomMinimumSize = new Vector2(330, 0);
        col.AddChild(_abandonHint);
        UiKit.ChainVertical(_resume, options, _abandon);

        // Panneau mission.
        var mp = new Panel { Position = new Vector2(500, 120), Size = new Vector2(380, 236), MouseFilter = Control.MouseFilterEnum.Ignore };
        mp.AddThemeStyleboxOverride("panel", UiE.PanelBox(3, 0, 0));
        stage.AddChild(mp);
        _missionHead = UiE.Label("", 16, UiE.Mint);
        _missionHead.Position = new Vector2(14, 6);
        _missionHead.Size = new Vector2(352, 22);
        mp.AddChild(_missionHead);
        mp.AddChild(new ColorRect { Color = UiE.Edge, Position = new Vector2(3, 33), Size = new Vector2(374, 2), MouseFilter = Control.MouseFilterEnum.Ignore });
        var rows = new VBoxContainer { Position = new Vector2(14, 44), Size = new Vector2(352, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 4);
        mp.AddChild(rows);
        var obj = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        obj.AddThemeConstantOverride("separation", 0);
        obj.AddChild(UiE.Label("Objectif : ", 18, UiE.Mint));
        _objective = UiE.Label("", 16);
        _objective.ClipText = true;
        _objective.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        obj.AddChild(_objective);
        rows.AddChild(obj);
        var ph = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        ph.AddThemeConstantOverride("separation", 8);
        ph.AddChild(UiE.Label("Phase", 18, UiE.Mint));
        _phase = UiE.Label("", 18);
        ph.AddChild(_phase);
        _phaseBoxes = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        _phaseBoxes.AddThemeConstantOverride("separation", 8);
        ph.AddChild(_phaseBoxes);
        _phaseRow = ph;
        rows.AddChild(ph);
        _diff = Line(rows, "Difficulté");
        _time = Line(rows, "Temps");
        _kills = Line(rows, "Éliminations");
        _left = Line(rows, "Ennemis restants");

        // Panneau escouade.
        _squad = new SquadPanel { Position = new Vector2(910, 120) };
        stage.AddChild(_squad);

        var hints = EFrame.Hints(stage);
        hints.Position = new Vector2(680, 650);
        hints.AddChild(UiE.KeyHint("Entrée:OK"));
        hints.AddChild(UiE.KeyHint("Échap:Reprendre", () => World.TogglePause()));
        VisibilityChanged += OnShown;
    }

    private static Label Line(VBoxContainer rows, string name)
    {
        var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var n = UiE.Label(name, 18, UiE.Mint);
        n.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        h.AddChild(n);
        var v = UiE.Label("", 18);
        h.AddChild(v);
        rows.AddChild(h);
        return v;
    }

    private string AbandonText => Net.I.IsHost ? "Abandonner la mission" : "Quitter la partie";

    private void ResetAbandon()
    {
        if (!_confirmAbandon) return;
        _confirmAbandon = false;
        _abandon.Title = AbandonText;
        _abandon.QueueRedraw();
        _abandonHint.Text = "";
    }

    private void OnShown()
    {
        // Le HUD est masqué derrière la pause (sinon il transparaît sous le menu).
        if (World.Hud != null) World.Hud.Visible = !Visible && !World.Ended;
        if (!Visible) return;
        ResetAbandon();
        var s = EStage.ScaleFor(_root.GetViewportRect().Size);
        _bgMat.SetShaderParameter("cell", 28f * s);
        _bgMat.SetShaderParameter("line_w", Mathf.Max(1f, s));
        UiKit.FadeIn(_root, 0.12f);
        UiKit.FocusLater(_resume);
        UpdatePanels();
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _refresh -= (float)delta;
        if (_refresh > 0) return;
        _refresh = 0.5f;
        UpdatePanels();
    }

    private static readonly Regex PhaseRx = new(@"\s*·\s*phase\s+(\d+)\s*/\s*(\d+)", RegexOptions.IgnoreCase);

    private void UpdatePanels()
    {
        _missionHead.Text = MissionProgress.Label(World.Mission);
        var obj = World.ObjectiveText ?? "";
        var m = PhaseRx.Match(obj);
        _objective.Text = m.Success ? obj.Remove(m.Index, m.Length) : obj;
        _phaseRow.Visible = m.Success;
        if (m.Success)
        {
            var cur = int.Parse(m.Groups[1].Value);
            var tot = int.Parse(m.Groups[2].Value);
            _phase.Text = $"{cur}/{tot}";
            foreach (var c in _phaseBoxes.GetChildren()) c.QueueFree();
            for (var i = 0; i < tot; i++)
            {
                var filled = i < cur;
                var box = new Panel { CustomMinimumSize = new Vector2(30, 10), MouseFilter = Control.MouseFilterEnum.Ignore };
                box.AddThemeStyleboxOverride("panel", UiKit.Box(filled ? UiE.Frame : new Color(0, 0, 0, 0), UiE.Frame, filled ? 0 : 2, 0, 0, 0));
                _phaseBoxes.AddChild(box);
            }
        }
        _diff.Text = Difficulties.Names[(int)World.Difficulty];
        _time.Text = UiKit.Time(World.Elapsed);
        _kills.Text = $"{World.TeamKills}";
        _left.Text = $"{World.EnemiesRemaining}";

        var list = new List<SquadPanel.Member>();
        var me = World.LocalPlayer;
        if (me != null) list.Add(new(Settings.I.PlayerName, me.Def.Name, me.Health, me.MaxHealth, me.Downed, true));
        foreach (var r in World.Remotes.Values)
            list.Add(new(r.PlayerName, Classes.Get(r.Class).Name, r.Health, Mathf.Max(r.MaxHealth, 1), r.Downed, false));
        _squad.Set(list);
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
            _abandon.Title = "Confirmer ?";
            _abandon.QueueRedraw();
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

/// <summary>Panneau escouade de la pause (cadre HUD EDF à onglet, une jauge graduée par soldat).</summary>
public partial class SquadPanel : Control
{
    public record Member(string Name, string Class, float Hp, float Max, bool Downed, bool Me);
    private List<Member> _m = new();

    public SquadPanel() { MouseFilter = MouseFilterEnum.Ignore; Size = new Vector2(320, 236); }

    public void Set(List<Member> m)
    {
        _m = m;
        Size = new Vector2(320, Mathf.Max(1, m.Count) * 68 + 32);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var w = Size.X; var h = Size.Y;
        var frame = new[]
        {
            new Vector2(1, 14), new Vector2(9, 6), new Vector2(110, 6), new Vector2(118, 14), new Vector2(311, 14), new Vector2(319, 22),
            new Vector2(319, h - 10), new Vector2(311, h - 1), new Vector2(9, h - 1), new Vector2(1, h - 9),
        };
        DrawColoredPolygon(frame, new Color(6 / 255f, 30 / 255f, 14 / 255f, 0.8f));
        var loop = new Vector2[frame.Length + 1];
        frame.CopyTo(loop, 0); loop[^1] = frame[0];
        DrawPolyline(loop, UiE.Frame, 1.8f, true);
        var cs = UiE.CondSemi; var mono = UiE.Mono;
        void Text(Font f, string s, float x, float y, int size, Color c, bool right = false)
        {
            if (right) x -= f.GetStringSize(s, HorizontalAlignment.Left, -1, size).X;
            DrawStringOutline(f, new Vector2(x, y), s, HorizontalAlignment.Left, -1, size, 3, UiE.Ink);
            DrawString(f, new Vector2(x, y), s, HorizontalAlignment.Left, -1, size, c);
        }
        Text(cs, "Escouade", 14, 4, 15, UiE.Mint);
        for (var i = 0; i < _m.Count; i++)
        {
            var m = _m[i];
            var y = 30 + i * 68;
            Text(cs, $"{m.Name} · {m.Class}", 14, y + 24, 19, UiE.Text);
            var val = m.Downed ? "À TERRE" : m.Me ? $"{m.Hp:N0} / {m.Max:N0}" : $"{Mathf.RoundToInt(100 * m.Hp / m.Max)} %";
            Text(m.Downed ? UiE.Cond : mono, val, w - 14, y + 24, 16, m.Downed ? UiE.Danger : UiE.Text, true);
            var bar = new Rect2(14, y + 32, 292, 12);
            DrawRect(bar, new Color(0, 0, 0, 0.6f));
            if (!m.Downed)
            {
                DrawRect(new Rect2(15, y + 33, 290 * Mathf.Clamp(m.Hp / m.Max, 0, 1), 10), new Color("33c653"));
                for (var x = 14f; x <= 306f; x += 29.2f) DrawLine(new Vector2(x + 0.5f, y + 33), new Vector2(x + 0.5f, y + 43), new Color(2 / 255f, 16 / 255f, 8 / 255f, 0.6f), 1f);
            }
            DrawRect(bar, m.Downed ? new Color("e8432e") : new Color("2c9c48"), false, 1f);
        }
    }
}
