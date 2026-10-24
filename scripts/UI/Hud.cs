using Godot;

namespace Rempart;

/// <summary>
/// HUD en mission, direction « E » (ADN EDF) : cf. docs/maquettes/DECISIONS.md (section HUD) et
/// docs/maquettes/project/E-HUD.dc.html. Seuls restent : armure (haut gauche), radar (haut droite),
/// pile d'armes (bas droite), vie des alliés au-dessus de leur tête, réticule discret et chiffres de dégâts.
/// Les blocs (scripts/UI/HudE*.cs) dessinent eux-mêmes leurs formes (_Draw) en coordonnées de maquette 1280×720.
/// Les anciens panneaux (objectif, escouade, butin, radio, boss, capacité, aide des commandes) sont supprimés :
/// les points d'entrée (Briefing, Radio, Toast, PickupPopup) restent appelables mais n'affichent rien ;
/// l'objectif est consultable dans le menu pause.
/// </summary>
public partial class Hud : CanvasLayer
{
    /// <summary>Test « --nohud » : HUD masqué (fonds de maquettes).</summary>
    public static bool ForceHidden;

    public World World;
    private Control _root;
    private ColorRect _vignette;
    private DamageArc _damageArc;
    private HudArmor _armor;
    private HudRadar _radar;
    private HudWeapons _weapons;
    private HudWorldMarkers _markers;
    private HudReticle _reticle;
    private Vector2 _layoutSize;
    private float _layoutScale;
    private const int M = 40;

    public override void _Ready()
    {
        Layer = 5;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiKit.Theme };
        UiKit.FillRect(_root);
        AddChild(_root);

        _vignette = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore, Material = VignetteMaterial() };
        UiKit.FillRect(_vignette);
        _root.AddChild(_vignette);

        _markers = new HudWorldMarkers { World = World };
        _root.AddChild(_markers);
        _reticle = new HudReticle { World = World };
        _reticle.SetAnchorsPreset(Control.LayoutPreset.Center);
        _root.AddChild(_reticle);
        _damageArc = new DamageArc();
        _damageArc.SetAnchorsPreset(Control.LayoutPreset.Center);
        _root.AddChild(_damageArc);

        _armor = new HudArmor { World = World };
        _radar = new HudRadar { World = World };
        _weapons = new HudWeapons { World = World };
        _root.AddChild(_armor);
        _root.AddChild(_radar);
        _root.AddChild(_weapons);
    }

    /// <summary>Assombrissement radial des bords (maquette) + voile rouge des dégâts / armure basse.</summary>
    private static ShaderMaterial VignetteMaterial()
    {
        var sh = new Shader
        {
            Code = @"shader_type canvas_item;
uniform float strength = 0.0;
void fragment() {
    vec2 uv = UV - 0.5;
    float d = length(uv * vec2(1.6, 1.0));
    float dark = smoothstep(0.55, 0.95, d) * 0.3;
    float red = smoothstep(0.35, 0.85, d) * strength;
    vec3 col = mix(vec3(0.0), vec3(0.55, 0.02, 0.0), red / max(red + dark, 0.0001));
    COLOR = vec4(col, clamp(red + dark, 0.0, 1.0));
}"
        };
        return new ShaderMaterial { Shader = sh };
    }

    private T Anchor<T>(T c, Control.LayoutPreset preset, Vector2 offset) where T : Control
    {
        _root.AddChild(c);
        c.SetAnchorsAndOffsetsPreset(preset, Control.LayoutPresetMode.KeepSize);
        c.Position += offset;
        return c;
    }

    /// <summary>Briefing de début de mission : plus affiché en jeu (le menu pause montre l'objectif).</summary>
    public void Briefing(MissionDef m) { }

    /// <summary>Messages radio : supprimés du HUD (maquette E).</summary>
    public void Radio(string text, int kind) { }

    /// <summary>Notifications : supprimées du HUD (maquette E).</summary>
    public void Toast(string text) { }

    /// <summary>Notification de butin : supprimée du HUD (maquette E).</summary>
    public void PickupPopup(CrateType type) { }

    public void DamageFrom(Vector3 from)
    {
        var p = World.LocalPlayer;
        if (p == null) return;
        var to = from - p.GlobalPosition;
        var fwd = p.AimForward;
        var a = Mathf.Atan2(to.X, to.Z) - Mathf.Atan2(fwd.X, fwd.Z);
        _damageArc.Show(-a);
    }

    private PanelContainer _board;
    private VBoxContainer _boardRows;
    private Label _fps;
    private float _fpsLog;

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var p = World.LocalPlayer;
        Visible = !ForceHidden && !World.Ended && !World.Paused;

        // Mise à l'échelle : maquette 1280×720 → fenêtre, puis option « Échelle du HUD ».
        var screen = GetViewport().GetVisibleRect().Size;
        var s = HudE.ScaleFor(screen);
        if (screen != _layoutSize || !Mathf.IsEqualApprox(s, _layoutScale))
        {
            _layoutSize = screen;
            _layoutScale = s;
            _armor.Layout(s, screen);
            _radar.Layout(s, screen);
            _weapons.Layout(s, screen);
            _markers.S = _reticle.S = s;
            _damageArc.Scale = new Vector2(s, s);
        }
        if (p == null) return;

        var mat = (ShaderMaterial)_vignette.Material;
        var lowHp = p.Downed ? 0.9f : Mathf.Clamp(1f - p.Health / p.MaxHealth - 0.6f, 0, 0.4f) * 1.6f;
        // Saisi par une fourmi : voile rouge qui pulse (en plus du flash de chaque morsure).
        var held = p.GrabbedBy != null && !p.Downed ? 0.24f + 0.1f * Mathf.Sin(Time.GetTicksMsec() * 0.007f) : 0f;
        mat.SetShaderParameter("strength", Mathf.Max(Mathf.Max(p.DamageFlash, lowHp), held));

        UpdateScoreboard();

        // Compteur d'images (F3) et journal pour les tests (--fps).
        if (Input.IsKeyPressed(Key.F3) && _fps == null)
        {
            _fps = UiKit.HudLabel("", 13, UiKit.Muted);
            Anchor(_fps, Control.LayoutPreset.CenterTop, new Vector2(0, 8));
            _fps.GrowHorizontal = Control.GrowDirection.Both;
        }
        if (_fps != null) _fps.Text = $"{Engine.GetFramesPerSecond()} i/s · {World.EnemiesById.Count} ennemis";
        _fpsLog -= dt;
        if (_fpsLog <= 0 && Screenshot.CmdArg("fps") != null)
        {
            _fpsLog = 3f;
            GD.Print($"FPS {Engine.GetFramesPerSecond()} ennemis {World.EnemiesById.Count} t={World.Elapsed:0} {Enemy.ClimbStats(World.EnemiesById.Values)}");
        }
    }

    /// <summary>Tableau des scores (maintenir Tab) : affiché seulement à la demande.</summary>
    private void UpdateScoreboard()
    {
        var showBoard = Input.IsActionPressed("scoreboard") && !World.Paused;
        if (showBoard && _board == null)
        {
            _board = new PanelContainer();
            _board.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(UiKit.Bg, 0.85f), default, 0, 0, 28, 22));
            var v = UiKit.VBox(10);
            _board.AddChild(v);
            v.AddChild(UiKit.Label(World.Mission.Name.ToUpperInvariant(), 28, UiKit.Text, true));
            _boardRows = UiKit.VBox(6);
            _boardRows.CustomMinimumSize = new Vector2(560, 0);
            v.AddChild(_boardRows);
            Anchor(_board, Control.LayoutPreset.Center, Vector2.Zero);
            _board.GrowHorizontal = Control.GrowDirection.Both;
            _board.GrowVertical = Control.GrowDirection.Both;
        }
        if (_board == null) return;
        _board.Visible = showBoard;
        if (!showBoard) return;
        foreach (var c in _boardRows.GetChildren()) c.QueueFree();
        var head = UiKit.HBox(16);
        foreach (var (t, w) in new[] { ("SOLDAT", 220), ("CLASSE", 200), ("ÉLIM.", 80) }) { var l = UiKit.Eyebrow(t); l.CustomMinimumSize = new Vector2(w, 0); head.AddChild(l); }
        _boardRows.AddChild(head);
        foreach (var row in World.ScoreRows())
        {
            var h = UiKit.HBox(16);
            var n = UiKit.Label(row.name + (row.downed ? "  (à terre)" : ""), 17, row.me ? UiKit.Accent : row.downed ? UiKit.Danger : UiKit.Text);
            n.CustomMinimumSize = new Vector2(220, 0);
            var c = UiKit.Label(row.cls, 15, UiKit.Muted);
            c.CustomMinimumSize = new Vector2(200, 0);
            var k = UiKit.Label(row.kills.ToString(), 17, UiKit.Text, true);
            h.AddChild(n);
            h.AddChild(c);
            h.AddChild(k);
            _boardRows.AddChild(h);
        }
        _boardRows.AddChild(UiKit.Rule());
        _boardRows.AddChild(UiKit.Label($"Butin : {World.CratesCollected[0]} armes · {World.CratesCollected[1]} armures   ·   {UiKit.Time(World.Elapsed)}", 14, UiKit.Muted));
    }
}

/// <summary>Indicateur directionnel des dégâts reçus.</summary>
public partial class DamageArc : Control
{
    private float _angle, _t;

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public void Show(float angle)
    {
        _angle = angle;
        _t = 1f;
    }

    public override void _Process(double delta)
    {
        if (_t <= 0) return;
        _t = Mathf.Max(0, _t - (float)delta * 1.2f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_t <= 0) return;
        var a = _angle - Mathf.Pi / 2;
        DrawArc(Vector2.Zero, 140, a - 0.35f, a + 0.35f, 24, new Color(1f, 0.2f, 0.1f, _t * 0.85f), 6f, true);
    }
}
