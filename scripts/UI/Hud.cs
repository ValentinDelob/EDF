using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// HUD en mission (cf. docs/ui-knowledge-base.md §4) : pas de cadres, texte avec ombre portée,
/// informations dans les coins, éléments contextuels qui n'apparaissent que si utiles.
/// </summary>
public partial class Hud : CanvasLayer
{
    public World World;
    private Control _root;
    private Label _objective, _remaining, _radio, _toast, _bossName, _hpText, _className, _abilityText, _reviveText, _downed, _resourceLabel;
    private readonly Label[] _crateLabels = new Label[4];
    private ProgressBar _hp, _resource, _boss, _revive;
    private Control _bossBox, _radioBox, _reviveBox;
    private VBoxContainer _squad;
    private Label _wName, _wAmmo, _wMag, _wSub, _wOther;
    private ColorRect _wRarity;
    private ProgressBar _wReload;
    private Crosshair _crosshair;
    private Radar _radar;
    private ColorRect _vignette;
    private float _radioTime, _toastTime;
    private VBoxContainer _pickupFeed;
    private DamageArc _damageArc;
    private const int M = 40;

    public override void _Ready()
    {
        Layer = 5;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiKit.Theme };
        UiKit.FillRect(_root);
        AddChild(_root);

        _vignette = new ColorRect { Color = new Color(0.6f, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore, Material = VignetteMaterial() };
        UiKit.FillRect(_vignette);
        _root.AddChild(_vignette);
        // Dégradés discrets depuis les bords (lisibilité sans cadres).
        _root.AddChild(EdgeShade(true));
        _root.AddChild(EdgeShade(false));

        _crosshair = new Crosshair { World = World };
        _crosshair.SetAnchorsPreset(Control.LayoutPreset.Center);
        _root.AddChild(_crosshair);
        _damageArc = new DamageArc();
        _damageArc.SetAnchorsPreset(Control.LayoutPreset.Center);
        _root.AddChild(_damageArc);

        BuildTopLeft();
        BuildTopCenter();
        BuildTopRight();
        BuildBottomLeft();
        BuildBottomRight();
        BuildCenterMessages();
    }

    private static TextureRect EdgeShade(bool top)
    {
        var tex = new GradientTexture2D
        {
            Gradient = new Gradient { Colors = new[] { new Color(0, 0, 0, 0.38f), new Color(0, 0, 0, 0) } },
            FillFrom = new Vector2(0, top ? 0 : 1), FillTo = new Vector2(0, top ? 1 : 0), Width = 4, Height = 128,
        };
        var r = new TextureRect
        {
            Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore, AnchorRight = 1,
        };
        if (top) { r.AnchorBottom = 0; r.OffsetBottom = 180; }
        else { r.AnchorTop = 1; r.AnchorBottom = 1; r.OffsetTop = -220; }
        return r;
    }

    private static ShaderMaterial VignetteMaterial()
    {
        var sh = new Shader
        {
            Code = @"shader_type canvas_item;
uniform float strength = 0.0;
void fragment() {
    vec2 uv = UV - 0.5;
    float d = length(uv * vec2(1.6, 1.0));
    float v = smoothstep(0.35, 0.85, d);
    COLOR = vec4(0.55, 0.02, 0.0, v * strength);
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

    private void BuildTopLeft()
    {
        var v = UiKit.VBox(4);
        v.MouseFilter = Control.MouseFilterEnum.Ignore;
        v.AddChild(UiKit.HudLabel("OBJECTIF", 14, UiKit.Accent, true));
        _objective = UiKit.HudLabel("", 18);
        _objective.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _objective.CustomMinimumSize = new Vector2(380, 0);
        v.AddChild(_objective);
        _remaining = UiKit.HudLabel("", 14, new Color(UiKit.Text, 0.8f));
        v.AddChild(_remaining);
        var crates = UiKit.HBox(16);
        for (var i = 0; i < 4; i++)
        {
            var h = UiKit.HBox(6);
            h.AddChild(new ColorRect { Color = Crates.Colors[i], CustomMinimumSize = new Vector2(8, 8), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            _crateLabels[i] = UiKit.HudLabel("0", 14);
            h.AddChild(_crateLabels[i]);
            crates.AddChild(h);
        }
        v.AddChild(UiKit.Gap(2));
        v.AddChild(crates);
        Anchor(v, Control.LayoutPreset.TopLeft, new Vector2(M, M));
    }

    private void BuildTopCenter()
    {
        _bossBox = UiKit.VBox(6);
        _bossBox.CustomMinimumSize = new Vector2(800, 0);
        _bossName = UiKit.HudLabel("", 18, UiKit.Text, true);
        _bossName.HorizontalAlignment = HorizontalAlignment.Center;
        _bossBox.AddChild(_bossName);
        _boss = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(800, 8), MaxValue = 1, Step = 0.001 };
        _boss.AddThemeStyleboxOverride("fill", UiKit.Box(UiKit.Danger, default, 0, 0, 0, 0));
        _bossBox.AddChild(_boss);
        _bossBox.Visible = false;
        Anchor(_bossBox, Control.LayoutPreset.CenterTop, new Vector2(0, 32));
    }

    private void BuildTopRight()
    {
        _radar = new Radar { World = World, CustomMinimumSize = new Vector2(170, 170) };
        Anchor(_radar, Control.LayoutPreset.TopRight, new Vector2(-M, M));
        _pickupFeed = UiKit.VBox(4);
        _pickupFeed.Alignment = BoxContainer.AlignmentMode.End;
        Anchor(_pickupFeed, Control.LayoutPreset.TopRight, new Vector2(-M, M + 200));
        _pickupFeed.GrowHorizontal = Control.GrowDirection.Begin;
    }

    private void BuildBottomLeft()
    {
        var v = UiKit.VBox(8);
        v.CustomMinimumSize = new Vector2(330, 0);
        _squad = UiKit.VBox(8);
        v.AddChild(_squad);
        var top = UiKit.HBox();
        _className = UiKit.HudLabel(World.LocalPlayer?.Def.Name.ToUpperInvariant() ?? "", 15, UiKit.Text, true);
        _className.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _className.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        top.AddChild(_className);
        _hpText = UiKit.HudLabel("", 36, UiKit.Text, true);
        top.AddChild(_hpText);
        v.AddChild(top);
        _hp = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8), MaxValue = 1, Step = 0.001 };
        v.AddChild(_hp);
        var res = UiKit.HBox(10);
        _resourceLabel = UiKit.HudLabel("", 13, new Color(UiKit.Text, 0.75f));
        _resourceLabel.CustomMinimumSize = new Vector2(84, 0);
        res.AddChild(_resourceLabel);
        _resource = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 4), MaxValue = 1, Step = 0.001, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        res.AddChild(_resource);
        v.AddChild(res);
        _abilityText = UiKit.HudLabel("", 14, UiKit.Accent);
        v.AddChild(_abilityText);
        Anchor(v, Control.LayoutPreset.BottomLeft, new Vector2(M, -M));
        v.GrowVertical = Control.GrowDirection.Begin;
    }

    private void BuildBottomRight()
    {
        var v = UiKit.VBox(2);
        v.CustomMinimumSize = new Vector2(360, 0);
        _wOther = UiKit.HudLabel("", 14, new Color(UiKit.Text, 0.7f));
        _wOther.HorizontalAlignment = HorizontalAlignment.Right;
        v.AddChild(_wOther);
        var row = UiKit.HBox(10);
        row.Alignment = BoxContainer.AlignmentMode.End;
        var names = UiKit.VBox(0);
        names.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        _wName = UiKit.HudLabel("", 17);
        _wName.HorizontalAlignment = HorizontalAlignment.Right;
        _wSub = UiKit.HudLabel("", 13);
        _wSub.HorizontalAlignment = HorizontalAlignment.Right;
        names.AddChild(_wName);
        names.AddChild(_wSub);
        row.AddChild(names);
        _wAmmo = UiKit.HudLabel("", 54, UiKit.Text, true);
        row.AddChild(_wAmmo);
        _wMag = UiKit.HudLabel("", 20, new Color(UiKit.Text, 0.7f), true);
        _wMag.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        _wMag.CustomMinimumSize = new Vector2(0, 0);
        row.AddChild(_wMag);
        v.AddChild(row);
        _wRarity = new ColorRect { CustomMinimumSize = new Vector2(180, 3), SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
        v.AddChild(_wRarity);
        _wReload = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(180, 3), MaxValue = 1, Step = 0.001, SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd, Visible = false };
        _wReload.AddThemeStyleboxOverride("fill", UiKit.Box(UiKit.Accent, default, 0, 0, 0, 0));
        v.AddChild(_wReload);
        Anchor(v, Control.LayoutPreset.BottomRight, new Vector2(-M, -M));
        v.GrowVertical = Control.GrowDirection.Begin;
        v.GrowHorizontal = Control.GrowDirection.Begin;
    }

    private void BuildCenterMessages()
    {
        _radioBox = new PanelContainer();
        _radioBox.AddThemeStyleboxOverride("panel", UiKit.Bar(new Color(UiKit.Bg, 0.6f), UiKit.Accent, 3, 16, 8));
        _radio = UiKit.HudLabel("", 17);
        _radioBox.AddChild(_radio);
        _radioBox.Visible = false;
        Anchor(_radioBox, Control.LayoutPreset.CenterTop, new Vector2(0, 104));
        _radioBox.GrowHorizontal = Control.GrowDirection.Both;

        _toast = UiKit.HudLabel("", 17);
        _toast.HorizontalAlignment = HorizontalAlignment.Center;
        Anchor(_toast, Control.LayoutPreset.CenterBottom, new Vector2(0, -250));
        _toast.GrowHorizontal = Control.GrowDirection.Both;

        _reviveBox = UiKit.VBox(6);
        _reviveText = UiKit.HudLabel("", 16);
        _reviveText.HorizontalAlignment = HorizontalAlignment.Center;
        _reviveBox.AddChild(_reviveText);
        _revive = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(240, 4), MaxValue = 1, Step = 0.01 };
        _revive.AddThemeStyleboxOverride("fill", UiKit.Box(UiKit.Accent, default, 0, 0, 0, 0));
        _reviveBox.AddChild(_revive);
        _reviveBox.Visible = false;
        Anchor(_reviveBox, Control.LayoutPreset.Center, new Vector2(0, 90));
        _reviveBox.GrowHorizontal = Control.GrowDirection.Both;

        _downed = UiKit.HudLabel("", 28, UiKit.Danger, true);
        _downed.HorizontalAlignment = HorizontalAlignment.Center;
        Anchor(_downed, Control.LayoutPreset.Center, new Vector2(0, -130));
        _downed.GrowHorizontal = Control.GrowDirection.Both;
    }

    public void Briefing(MissionDef m)
    {
        // Colonne de briefing à gauche (zone libre entre l'objectif et la vie) : titre, texte, puis rappel des commandes.
        var col = UiKit.VBox(24);
        col.MouseFilter = Control.MouseFilterEnum.Ignore;
        var v = UiKit.VBox(6);
        v.AddChild(UiKit.HudLabel($"MISSION {m.Index + 1:00} · {Difficulties.Names[(int)World.Difficulty].ToUpperInvariant()}", 15, UiKit.Accent, true));
        v.AddChild(UiKit.HudLabel(m.Name.ToUpperInvariant(), 64, UiKit.Text, true));
        var b = UiKit.HudLabel(m.Briefing, 17, new Color(UiKit.Text, 0.9f));
        b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        b.CustomMinimumSize = new Vector2(560, 0);
        v.AddChild(b);
        col.AddChild(v);

        // Rappel des commandes pendant les premières secondes (touches encadrées, comme dans les menus).
        // Il était en bas au centre et chevauchait la vie et l'arme en 1280×720.
        var keys = new HFlowContainer { CustomMinimumSize = new Vector2(560, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        keys.AddThemeConstantOverride("h_separation", 20);
        keys.AddThemeConstantOverride("v_separation", 8);
        string K(string a) => UiKit.KeyName(a);
        var items = new List<(string key, string text)> { ($"{K("move_forward")}{K("move_left")}{K("move_back")}{K("move_right")}", "Se déplacer") };
        items.AddRange(World.LocalPlayer?.Def.Id switch
        {
            SoldierClass.Airborne => new[] { (K("jump"), "Voler"), (K("sprint"), "Propulsion") },
            SoldierClass.Heavy => new[] { (K("sprint"), "Poussée"), (K("jump"), "Saut assisté") },
            _ => new[] { (K("sprint"), "Courir"), (K("dodge"), "Roulade") },
        });
        items.AddRange(new[] { (K("reload"), "Recharger"), ("1 2 3", "Armes"), (K("ability"), "Capacité"), (K("grenade"), "Grenade"), (K("interact"), "Relever") });
        foreach (var (key, text) in items) keys.AddChild(UiKit.HudKey(key, text));
        col.AddChild(keys);
        // Ancré sous le bloc objectif (et non centré) : ne le chevauche pas en 1280×720, reste au-dessus de la vie.
        Anchor(col, Control.LayoutPreset.TopLeft, new Vector2(UiKit.ScreenX, 168));

        var kt = keys.CreateTween();
        keys.Modulate = new Color(1, 1, 1, 0);
        kt.TweenInterval(0.6f);
        kt.TweenProperty(keys, "modulate:a", 1f, 0.4f);
        kt.TweenInterval(11f);
        kt.TweenProperty(keys, "modulate:a", 0f, 1.0f);
        kt.TweenCallback(Callable.From(col.QueueFree));

        var tw = v.CreateTween();
        v.Modulate = new Color(1, 1, 1, 0);
        tw.TweenProperty(v, "modulate:a", 1f, 0.5f);
        tw.TweenInterval(6f);
        tw.TweenProperty(v, "modulate:a", 0f, 1.0f);
    }

    public void Radio(string text, int kind)
    {
        _radio.Text = text;
        _radioBox.Visible = true;
        _radioTime = 6f;
    }

    public void Toast(string text)
    {
        _toast.Text = text;
        _toastTime = 4f;
    }

    public void PickupPopup(CrateType type)
    {
        var h = UiKit.HBox(8);
        h.Alignment = BoxContainer.AlignmentMode.End;
        var text = type switch
        {
            CrateType.Weapon => "Caisse d'arme",
            CrateType.Armor => $"Armure +{Difficulties.ArmorPerCrate[(int)World.Difficulty]}",
            CrateType.Health => "Soin",
            _ => "Crédits",
        };
        h.AddChild(UiKit.HudLabel(text, 15));
        h.AddChild(new ColorRect { Color = Crates.Colors[(int)type], CustomMinimumSize = new Vector2(8, 8), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
        _pickupFeed.AddChild(h);
        while (_pickupFeed.GetChildCount() > 5) _pickupFeed.GetChild(0).Free();
        h.Modulate = new Color(1, 1, 1, 0);
        var tw = h.CreateTween();
        tw.TweenProperty(h, "modulate:a", 1f, 0.12f);
        tw.TweenInterval(3f);
        tw.TweenProperty(h, "modulate:a", 0f, 0.6f);
        tw.TweenCallback(Callable.From(h.QueueFree));
    }

    public void DamageFrom(Vector3 from)
    {
        var p = World.LocalPlayer;
        if (p == null) return;
        var to = from - p.GlobalPosition;
        var fwd = p.AimForward;
        var a = Mathf.Atan2(to.X, to.Z) - Mathf.Atan2(fwd.X, fwd.Z);
        _damageArc.Show(-a);
    }

    private float _squadRefresh;
    private PanelContainer _board;
    private VBoxContainer _boardRows;
    private Label _fps;
    private float _fpsLog;

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var p = World.LocalPlayer;
        Visible = !World.Ended && !World.Paused;
        // Échelle du HUD : on agrandit la racine et on réduit sa taille logique d'autant.
        var s = Mathf.Clamp(Settings.I.HudScale, 0.8f, 1.2f);
        if (!Mathf.IsEqualApprox(_root.Scale.X, s) || _root.Size != GetViewport().GetVisibleRect().Size / s)
        {
            _root.AnchorRight = 0;
            _root.AnchorBottom = 0;
            _root.Scale = Vector2.One * s;
            _root.Size = GetViewport().GetVisibleRect().Size / s;
        }
        if (p == null) return;

        _objective.Text = World.ObjectiveText;
        _remaining.Text = World.EnemiesRemaining > 0 ? $"{World.EnemiesRemaining} ennemis · {UiKit.Time(World.Elapsed)}" : UiKit.Time(World.Elapsed);
        for (var i = 0; i < 4; i++) _crateLabels[i].Text = World.CratesCollected[i].ToString();

        _bossBox.Visible = World.BossHealth >= 0;
        if (_bossBox.Visible)
        {
            _bossName.Text = World.BossName.ToUpperInvariant();
            _boss.Value = Mathf.Lerp((float)_boss.Value, World.BossHealth, dt * 6f);
        }

        _hpText.Text = $"{Mathf.CeilToInt(p.Health)}";
        _hp.Value = p.Health / p.MaxHealth;
        var low = p.Health / p.MaxHealth < 0.25f;
        _hp.AddThemeStyleboxOverride("fill", UiKit.Box(low ? UiKit.Danger : UiKit.Text, default, 0, 0, 0, 0));
        _hpText.AddThemeColorOverride("font_color", low ? UiKit.Danger : UiKit.Text);

        Color resColor;
        switch (p.Def.Id)
        {
            case SoldierClass.Airborne:
                _resourceLabel.Text = p.Overheated ? "SURCHAUFFE" : "ÉNERGIE";
                _resource.Value = p.Energy / 100f;
                resColor = p.Overheated ? UiKit.Danger : UiKit.Info;
                break;
            case SoldierClass.Heavy:
                _resourceLabel.Text = p.Overheated ? "SURCHAUFFE" : "CHALEUR";
                _resource.Value = p.Heat / 100f;
                resColor = p.Overheated || p.Heat > 75 ? UiKit.Danger : UiKit.Accent;
                break;
            case SoldierClass.Support:
                _resourceLabel.Text = "APPUI";
                _resource.Value = Mathf.Clamp(p.CallCredits / p.CallCost, 0, 1);
                resColor = UiKit.Accent;
                break;
            default:
                _resourceLabel.Text = $"GRENADE [{UiKit.KeyName("grenade")}]";
                _resource.Value = 1f - p.GrenadeCooldown / 9f;
                resColor = UiKit.Info;
                break;
        }
        _resource.AddThemeStyleboxOverride("fill", UiKit.Box(resColor, default, 0, 0, 0, 0));
        var ready = p.AbilityReady >= 0.999f;
        var abilityName = p.Def.Id == SoldierClass.Support ? "Barrage d'artillerie" : p.Def.AbilityName;
        _abilityText.Text = ready ? $"[{UiKit.KeyName("ability")}]  {abilityName}" : $"{abilityName}  {Mathf.RoundToInt(p.AbilityReady * 100)} %";
        _abilityText.AddThemeColorOverride("font_color", ready ? UiKit.Accent : new Color(UiKit.Text, 0.55f));
        if (p.RallyTime > 0) _abilityText.Text += $"   ·   +30 % dégâts {Mathf.CeilToInt(p.RallyTime)} s";
        if (p.AnchorTime > 0) _abilityText.Text += $"   ·   Ancrage {Mathf.CeilToInt(p.AnchorTime)} s";

        // Arme active (ou armement du véhicule)
        var ws = p.Weapon;
        if (p.Vehicle is { } veh)
        {
            _wName.Text = veh.DisplayName;
            _wSub.Text = veh.HudSub;
            _wSub.AddThemeColorOverride("font_color", veh.Health < veh.MaxHealth * 0.3f ? UiKit.Danger : UiKit.Info);
            _wAmmo.Text = veh.HudAmmo;
            _wMag.Text = veh.HudMag;
            _wReload.Visible = veh.HudReady < 1;
            _wReload.Value = veh.HudReady;
            _wRarity.Visible = false;
            _wOther.Text = veh.HudOther;
            _wAmmo.AddThemeColorOverride("font_color", UiKit.Text);
        }
        else if (ws != null)
        {
            _wName.Text = ws.W.T.Name;
            _wSub.Text = $"{Rarities.Names[(int)ws.W.Rarity]} · Nv.{ws.W.Level}";
            _wSub.AddThemeColorOverride("font_color", Rarities.Colors[(int)ws.W.Rarity]);
            _wRarity.Color = Rarities.Colors[(int)ws.W.Rarity];
            if (ws.W.T.Kind == WeaponKind.Beam) { _wAmmo.Text = $"{Mathf.RoundToInt(ws.BeamCharge * 100)}"; _wMag.Text = "%"; }
            else if (ws.W.Magazine <= 1) { _wAmmo.Text = ws.Reloading ? "—" : "1"; _wMag.Text = ws.Reloading ? "RECHARGE" : "PRÊT"; }
            else { _wAmmo.Text = $"{ws.Ammo}"; _wMag.Text = $"/ {ws.W.Magazine}"; }
            _wAmmo.AddThemeColorOverride("font_color", ws.Ammo == 0 && ws.W.T.Kind != WeaponKind.Beam ? UiKit.Danger : UiKit.Text);
            _wReload.Visible = ws.Reloading;
            _wReload.Value = ws.ReloadProgress;
            _wRarity.Visible = !ws.Reloading;
            var others = p.Weapons.Select((w, i) => (w, i)).Where(t => t.i != p.Current)
                .Select(t => $"{t.i + 1}  {t.w.W.T.Name}" + (t.w.Reloading ? " (recharge)" : ""));
            _wOther.Text = string.Join("     ", others);
        }

        // Escouade (multijoueur)
        _squadRefresh -= dt;
        if (_squadRefresh <= 0)
        {
            _squadRefresh = 0.25f;
            foreach (var c in _squad.GetChildren()) c.QueueFree();
            foreach (var r in World.Remotes.Values)
            {
                var row = UiKit.VBox(2);
                var h = UiKit.HBox(8);
                var n = UiKit.HudLabel(r.PlayerName, 14, r.Downed ? UiKit.Danger : UiKit.Text);
                n.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                h.AddChild(n);
                h.AddChild(UiKit.HudLabel(r.Downed ? "À TERRE" : Classes.Get(r.Class).Name, 13, new Color(UiKit.Text, 0.7f)));
                row.AddChild(h);
                var bar = new ProgressBar { ShowPercentage = false, MaxValue = 1, Value = r.Health / Mathf.Max(1, r.MaxHealth), CustomMinimumSize = new Vector2(0, 3) };
                bar.AddThemeStyleboxOverride("fill", UiKit.Box(r.Downed ? UiKit.Danger : UiKit.Info, default, 0, 0, 0, 0));
                row.AddChild(bar);
                _squad.AddChild(row);
            }
            if (World.Remotes.Count > 0) _squad.AddChild(UiKit.Gap(6));
        }

        _radioTime -= dt;
        _radioBox.Visible = _radioTime > 0;
        _toastTime -= dt;
        _toast.Modulate = new Color(1, 1, 1, Mathf.Clamp(_toastTime, 0, 1));

        var mat = (ShaderMaterial)_vignette.Material;
        var lowHp = p.Downed ? 0.9f : Mathf.Clamp(1f - p.Health / p.MaxHealth - 0.6f, 0, 0.4f) * 1.6f;
        mat.SetShaderParameter("strength", Mathf.Max(p.DamageFlash, lowHp));

        _reviveBox.Visible = p.ReviveTarget != 0 && !p.Downed;
        _revive.Value = p.ReviveProgress;
        _reviveText.Text = $"[{UiKit.KeyName("interact")}]  Relever {(World.Remotes.TryGetValue(p.ReviveTarget, out var rp) ? rp.PlayerName : "")}";
        _downed.Text = p.Downed
            ? (Net.I.IsSolo && !p.SecondChanceUsed ? "À TERRE — UN INFIRMIER ARRIVE" : "À TERRE — ATTENDEZ UN ÉQUIPIER")
            : "";
        _crosshair.Visible = !p.Downed;

        // Tableau des scores (maintenir Tab).
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
        if (_board != null)
        {
            _board.Visible = showBoard;
            if (showBoard)
            {
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
                _boardRows.AddChild(UiKit.Label($"Butin : {World.CratesCollected[0]} armes · {World.CratesCollected[1]} armures · {World.CratesCollected[3]} crédits   ·   {UiKit.Time(World.Elapsed)}", 14, UiKit.Muted));
            }
        }

        // Compteur d'images (F3) et journal pour les tests (--fps).
        if (Input.IsKeyPressed(Key.F3) && _fps == null)
        {
            _fps = UiKit.HudLabel("", 13, UiKit.Muted);
            Anchor(_fps, Control.LayoutPreset.TopRight, new Vector2(-M, 8));
            _fps.GrowHorizontal = Control.GrowDirection.Begin;
        }
        if (_fps != null) _fps.Text = $"{Engine.GetFramesPerSecond()} i/s · {World.EnemiesById.Count} ennemis";
        _fpsLog -= dt;
        if (_fpsLog <= 0 && Screenshot.CmdArg("fps") != null)
        {
            _fpsLog = 3f;
            GD.Print($"FPS {Engine.GetFramesPerSecond()} ennemis {World.EnemiesById.Count} t={World.Elapsed:0} {Enemy.ClimbStats(World.EnemiesById.Values)}");
        }
    }
}

/// <summary>Réticule dynamique (écartement selon la précision, marqueur de touche, rechargement).</summary>
public partial class Crosshair : Control
{
    public World World;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var p = World.LocalPlayer;
        if (p == null || p.Weapon == null) return;
        var ws = p.Weapon;
        var spread = Mathf.Clamp(ws.W.Spread * (p.Aiming ? 0.55f : 1f) * 6f, 4f, 60f);
        var c = new Color(1, 1, 1, 0.9f);
        var sh = new Color(0, 0, 0, 0.5f);
        void L(Vector2 a, Vector2 b)
        {
            DrawLine(a + Vector2.One, b + Vector2.One, sh, 2f, true);
            DrawLine(a, b, c, 2f, true);
        }
        var kind = ws.W.T.Kind;
        if (kind is WeaponKind.Airstrike or WeaponKind.Turret or WeaponKind.HealField)
        {
            DrawArc(Vector2.Zero, 10, 0, Mathf.Tau, 24, c, 2f, true);
        }
        else if (ws.W.T.Pellets > 1)
        {
            DrawArc(Vector2.Zero, spread + 6, 0, Mathf.Tau, 40, new Color(c, 0.6f), 1.5f, true);
            DrawCircle(Vector2.Zero, 2f, c);
        }
        else
        {
            L(new Vector2(0, -spread - 10), new Vector2(0, -spread));
            L(new Vector2(0, spread), new Vector2(0, spread + 10));
            L(new Vector2(-spread - 10, 0), new Vector2(-spread, 0));
            L(new Vector2(spread, 0), new Vector2(spread + 10, 0));
            DrawCircle(Vector2.Zero, 1.5f, c);
        }
        if (p.HitMarker > 0)
        {
            var hc = new Color(1f, 0.85f, 0.4f, p.HitMarker);
            var s = 8f + (1 - p.HitMarker) * 4f;
            DrawLine(new Vector2(-s, -s), new Vector2(-s / 2, -s / 2), hc, 2.5f, true);
            DrawLine(new Vector2(s, -s), new Vector2(s / 2, -s / 2), hc, 2.5f, true);
            DrawLine(new Vector2(-s, s), new Vector2(-s / 2, s / 2), hc, 2.5f, true);
            DrawLine(new Vector2(s, s), new Vector2(s / 2, s / 2), hc, 2.5f, true);
        }
        if (ws.Reloading)
            DrawArc(Vector2.Zero, 26, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * ws.ReloadProgress, 40, UiKit.Accent, 3f, true);
    }
}

/// <summary>Indicateur directionnel des dégâts reçus.</summary>
public partial class DamageArc : Control
{
    private float _angle, _t;

    public void Show(float angle)
    {
        _angle = angle;
        _t = 1f;
    }

    public override void _Process(double delta)
    {
        _t = Mathf.Max(0, _t - (float)delta * 1.2f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_t <= 0) return;
        var a = _angle - Mathf.Pi / 2;
        DrawArc(Vector2.Zero, 140, a - 0.35f, a + 0.35f, 24, new Color(1f, 0.2f, 0.1f, _t * 0.85f), 8f, true);
    }
}

/// <summary>Radar circulaire orienté selon la caméra.</summary>
public partial class Radar : Control
{
    public World World;
    private const float Range = 120f;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var p = World.LocalPlayer;
        if (p == null) return;
        var size = Size;
        var c = size / 2;
        var r = Mathf.Min(size.X, size.Y) / 2;
        DrawCircle(c, r, new Color(0, 0, 0, 0.35f));
        // Cône de vision discret (haut = devant soi).
        var cone = new List<Vector2> { c };
        for (var k = 0; k <= 12; k++)
        {
            var a = -Mathf.Pi / 2 - 0.5f + k / 12f;
            cone.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r - 2));
        }
        DrawColoredPolygon(cone.ToArray(), new Color(1, 1, 1, 0.05f));
        DrawArc(c, r, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.18f), 1.5f, true);
        DrawArc(c, r * 0.5f, 0, Mathf.Tau, 48, new Color(1, 1, 1, 0.07f), 1f, true);
        var fwd = p.AimForward;
        var yaw = Mathf.Atan2(fwd.X, fwd.Z);
        Vector2 Map(Vector3 w)
        {
            var d = w - p.GlobalPosition;
            var v = new Vector2(d.X, d.Z).Rotated(yaw);
            v = new Vector2(-v.X, -v.Y);
            var len = v.Length();
            if (len > Range) v = v / len * Range;
            return c + v / Range * (r - 6);
        }
        foreach (var crate in World.CratesById.Values)
        {
            var q = Map(crate.GlobalPosition);
            DrawRect(new Rect2(q - Vector2.One * 2.5f, Vector2.One * 5), Crates.Colors[(int)crate.Type]);
        }
        foreach (var e in World.EnemiesById.Values)
        {
            if (e.State == EnemyState.Dead) continue;
            var q = Map(e.GlobalPosition);
            var big = e.IsBoss || e.IsStatic;
            DrawCircle(q, big ? 6 : 2.6f, e.Def.Flying ? new Color(1f, 0.6f, 0.3f) : UiKit.Danger);
        }
        if (World.Allies != null)
            foreach (var a in World.Allies.ById.Values)
                if (!a.Dead) DrawCircle(Map(a.GlobalPosition), 2.5f, new Color(UiKit.Info, 0.7f));
        foreach (var rp in World.Remotes.Values)
            DrawCircle(Map(rp.GlobalPosition), 4, rp.Downed ? UiKit.Accent : UiKit.Info);
        foreach (var ping in World.Pings)
        {
            if (ping.until < World.Elapsed) continue;
            var q = Map(ping.pos);
            var pc = ping.kind == 1 ? UiKit.Danger : ping.kind == 2 ? UiKit.Gold : UiKit.Accent;
            DrawColoredPolygon(new[] { q + new Vector2(0, -6), q + new Vector2(6, 0), q + new Vector2(0, 6), q + new Vector2(-6, 0) }, pc);
        }
        // Joueur : triangle pointant vers le haut
        DrawColoredPolygon(new[] { c + new Vector2(0, -7), c + new Vector2(5, 5), c + new Vector2(-5, 5) }, Colors.White);

        // Repère du nord (-Z) sur le bord : le radar tourne avec la caméra.
        var north = Map(p.GlobalPosition + new Vector3(0, 0, -Range * 4f)) - c;
        if (north.LengthSquared() > 0.01f)
        {
            var np = c + north.Normalized() * (r - 1);
            DrawCircle(np, 9f, new Color(UiKit.Bg, 0.85f));
            DrawArc(np, 9f, 0, Mathf.Tau, 24, new Color(1, 1, 1, 0.35f), 1f, true);
            var font = UiKit.HeadFont;
            const int fs = 13;
            var ts = font.GetStringSize("N", HorizontalAlignment.Left, -1, fs);
            DrawString(font, np + new Vector2(-ts.X / 2f, ts.Y / 2f - 3f), "N", HorizontalAlignment.Left, -1, fs, UiKit.Accent);
        }
    }
}
