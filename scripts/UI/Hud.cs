using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Interface en mission : vie, ressource de classe, armes, objectif, radar, boss, messages.</summary>
public partial class Hud : CanvasLayer
{
    public World World;
    private Control _root;
    private Label _objective, _remaining, _radio, _toast, _bossName, _hpText, _className, _abilityText, _reviveText, _downed;
    private Label[] _crateLabels = new Label[4];
    private ProgressBar _hp, _resource, _boss, _revive;
    private Label _resourceLabel;
    private Control _bossBox, _radioBox, _reviveBox;
    private VBoxContainer _weaponList;
    private readonly List<(PanelContainer panel, Label name, Label ammo, Label sub, ProgressBar reload)> _weaponRows = new();
    private Crosshair _crosshair;
    private Radar _radar;
    private ColorRect _vignette;
    private float _radioTime, _toastTime;
    private VBoxContainer _pickupFeed;
    private Control _briefing;
    private float _damageDirTime;
    private float _damageDirAngle;
    private DamageArc _damageArc;

    public override void _Ready()
    {
        Layer = 5;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiKit.Theme };
        UiKit.FillRect(_root);
        AddChild(_root);

        _vignette = new ColorRect { Color = new Color(0.6f, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore, Material = VignetteMaterial() };
        UiKit.FillRect(_vignette);
        _root.AddChild(_vignette);

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

    private Control Anchor(Control c, Control.LayoutPreset preset, Vector2 offset)
    {
        _root.AddChild(c);
        c.SetAnchorsAndOffsetsPreset(preset, Control.LayoutPresetMode.KeepSize);
        c.Position += offset;
        return c;
    }

    private void BuildTopLeft()
    {
        var panel = UiKit.Panel("Glass");
        panel.CustomMinimumSize = new Vector2(340, 0);
        var v = UiKit.VBox(6);
        panel.AddChild(v);
        v.AddChild(UiKit.Eyebrow("Objectif", UiKit.Accent));
        _objective = UiKit.Label("", 17);
        _objective.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _objective.CustomMinimumSize = new Vector2(320, 0);
        v.AddChild(_objective);
        _remaining = UiKit.Label("", 14, UiKit.Muted);
        v.AddChild(_remaining);
        var crates = UiKit.HBox(14);
        for (var i = 0; i < 4; i++)
        {
            var h = UiKit.HBox(6);
            h.AddChild(new ColorRect { Color = Crates.Colors[i], CustomMinimumSize = new Vector2(10, 10), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            _crateLabels[i] = UiKit.Label("0", 14, new Color(UiKit.Text, 0.9f));
            h.AddChild(_crateLabels[i]);
            h.TooltipText = Crates.Names[i];
            crates.AddChild(h);
        }
        v.AddChild(crates);
        Anchor(panel, Control.LayoutPreset.TopLeft, new Vector2(28, 24));
    }

    private void BuildTopCenter()
    {
        _bossBox = UiKit.VBox(6);
        _bossBox.CustomMinimumSize = new Vector2(620, 0);
        _bossName = UiKit.Label("", 18, UiKit.Text, true);
        _bossName.HorizontalAlignment = HorizontalAlignment.Center;
        _bossName.AddThemeConstantOverride("outline_size", 6);
        _bossName.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
        _bossBox.AddChild(_bossName);
        _boss = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(620, 10), MaxValue = 1, Step = 0.001 };
        _boss.AddThemeStyleboxOverride("fill", UiKit.Box(UiKit.Danger, default, 0, 2, 0, 0));
        _bossBox.AddChild(_boss);
        _bossBox.Visible = false;
        Anchor(_bossBox, Control.LayoutPreset.CenterTop, new Vector2(0, 26));
    }

    private void BuildTopRight()
    {
        _radar = new Radar { World = World, CustomMinimumSize = new Vector2(200, 200) };
        Anchor(_radar, Control.LayoutPreset.TopRight, new Vector2(-28, 24));
    }

    private void BuildBottomLeft()
    {
        var panel = UiKit.Panel("Glass");
        panel.CustomMinimumSize = new Vector2(400, 0);
        var v = UiKit.VBox(8);
        panel.AddChild(v);
        var top = UiKit.HBox();
        _className = UiKit.Label(World.LocalPlayer?.Def.Name.ToUpperInvariant() ?? "", 16, UiKit.Text, true);
        _className.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(_className);
        _hpText = UiKit.Label("", 28, UiKit.Text, true);
        top.AddChild(_hpText);
        v.AddChild(top);
        _hp = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 12), MaxValue = 1, Step = 0.001 };
        v.AddChild(_hp);
        var res = UiKit.HBox(10);
        _resourceLabel = UiKit.Label("", 13, UiKit.Muted);
        _resourceLabel.CustomMinimumSize = new Vector2(92, 0);
        res.AddChild(_resourceLabel);
        _resource = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 6), MaxValue = 1, Step = 0.001, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        _resource.AddThemeStyleboxOverride("fill", UiKit.Box(UiKit.Info, default, 0, 2, 0, 0));
        res.AddChild(_resource);
        v.AddChild(res);
        _abilityText = UiKit.Label("", 14, UiKit.Muted);
        v.AddChild(_abilityText);
        Anchor(panel, Control.LayoutPreset.BottomLeft, new Vector2(28, -24));
    }

    private void BuildBottomRight()
    {
        _weaponList = UiKit.VBox(6);
        _weaponList.CustomMinimumSize = new Vector2(360, 0);
        var p = World.LocalPlayer;
        if (p != null)
            for (var i = 0; i < p.Weapons.Count; i++)
            {
                var panel = UiKit.Panel("Glass");
                var h = UiKit.HBox(12);
                panel.AddChild(h);
                var left = UiKit.VBox(0);
                left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                var name = UiKit.Label("", 16);
                var sub = UiKit.Label("", 12, UiKit.Muted);
                left.AddChild(name);
                left.AddChild(sub);
                var reload = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 3), MaxValue = 1, Step = 0.001, Visible = false };
                reload.AddThemeStyleboxOverride("fill", UiKit.Box(UiKit.Accent, default, 0, 1, 0, 0));
                left.AddChild(reload);
                h.AddChild(left);
                var ammo = UiKit.Label("", 30, UiKit.Text, true);
                ammo.VerticalAlignment = VerticalAlignment.Center;
                h.AddChild(ammo);
                _weaponList.AddChild(panel);
                _weaponRows.Add((panel, name, ammo, sub, reload));
            }
        Anchor(_weaponList, Control.LayoutPreset.BottomRight, new Vector2(-28, -24));
        _weaponList.GrowVertical = Control.GrowDirection.Begin;
        _weaponList.GrowHorizontal = Control.GrowDirection.Begin;
    }

    private void BuildCenterMessages()
    {
        _radioBox = UiKit.Panel("Glass");
        var rh = UiKit.HBox(12);
        _radioBox.AddChild(rh);
        rh.AddChild(new ColorRect { Color = UiKit.Accent, CustomMinimumSize = new Vector2(3, 0) });
        _radio = UiKit.Label("", 19);
        rh.AddChild(_radio);
        _radioBox.Visible = false;
        Anchor(_radioBox, Control.LayoutPreset.CenterTop, new Vector2(0, 92));
        _radioBox.GrowHorizontal = Control.GrowDirection.Both;

        _toast = UiKit.Label("", 18, UiKit.Text);
        _toast.AddThemeConstantOverride("outline_size", 6);
        _toast.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        _toast.HorizontalAlignment = HorizontalAlignment.Center;
        Anchor(_toast, Control.LayoutPreset.CenterBottom, new Vector2(0, -190));
        _toast.GrowHorizontal = Control.GrowDirection.Both;

        _pickupFeed = UiKit.VBox(4);
        Anchor(_pickupFeed, Control.LayoutPreset.CenterRight, new Vector2(-40, 60));
        _pickupFeed.GrowHorizontal = Control.GrowDirection.Begin;

        _reviveBox = UiKit.VBox(6);
        _reviveText = UiKit.Label("Maintenez E pour relever", 16, UiKit.Text);
        _reviveText.HorizontalAlignment = HorizontalAlignment.Center;
        _reviveBox.AddChild(_reviveText);
        _revive = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(260, 6), MaxValue = 1, Step = 0.01 };
        _reviveBox.AddChild(_revive);
        _reviveBox.Visible = false;
        Anchor(_reviveBox, Control.LayoutPreset.Center, new Vector2(0, 90));

        _downed = UiKit.Label("", 26, UiKit.Danger, true);
        _downed.HorizontalAlignment = HorizontalAlignment.Center;
        _downed.AddThemeConstantOverride("outline_size", 8);
        _downed.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        Anchor(_downed, Control.LayoutPreset.Center, new Vector2(0, -120));
        _downed.GrowHorizontal = Control.GrowDirection.Both;
    }

    public void Briefing(MissionDef m)
    {
        var panel = UiKit.Panel("Glass");
        var v = UiKit.VBox(8);
        panel.AddChild(v);
        v.AddChild(UiKit.Eyebrow($"Mission {m.Index + 1} · {Difficulties.Names[(int)World.Difficulty]}", UiKit.Accent));
        v.AddChild(UiKit.Heading(m.Name, 40));
        var b = UiKit.Label(m.Briefing, 17, new Color(UiKit.Text, 0.85f));
        b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        b.CustomMinimumSize = new Vector2(560, 0);
        v.AddChild(b);
        _briefing = panel;
        Anchor(panel, Control.LayoutPreset.CenterLeft, new Vector2(60, -120));
        var tw = panel.CreateTween();
        panel.Modulate = new Color(1, 1, 1, 0);
        tw.TweenProperty(panel, "modulate:a", 1f, 0.6f);
        tw.TweenInterval(6f);
        tw.TweenProperty(panel, "modulate:a", 0f, 1.2f);
        tw.TweenCallback(Callable.From(panel.QueueFree));
    }

    public void Radio(string text, int kind)
    {
        _radio.Text = "QG : " + text;
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
        h.AddChild(new ColorRect { Color = Crates.Colors[(int)type], CustomMinimumSize = new Vector2(10, 10), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
        var text = type switch
        {
            CrateType.Weapon => "Caisse d'arme",
            CrateType.Armor => $"Armure +{Difficulties.ArmorPerCrate[(int)World.Difficulty]}",
            CrateType.Health => "Soin",
            _ => "Crédits",
        };
        var l = UiKit.Label(text, 16);
        l.AddThemeConstantOverride("outline_size", 5);
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
        h.AddChild(l);
        _pickupFeed.AddChild(h);
        while (_pickupFeed.GetChildCount() > 6) _pickupFeed.GetChild(0).Free();
        var tw = h.CreateTween();
        tw.TweenInterval(2.5f);
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

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var p = World.LocalPlayer;
        Visible = !World.Ended;
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
        var low = p.Health / p.MaxHealth < 0.3f;
        _hp.AddThemeStyleboxOverride("fill", UiKit.Box(low ? UiKit.Danger : UiKit.Good, default, 0, 2, 0, 0));

        switch (p.Def.Id)
        {
            case SoldierClass.Airborne:
                _resourceLabel.Text = p.Overheated ? "SURCHAUFFE" : "ÉNERGIE";
                _resource.Value = p.Energy / 100f;
                _resource.AddThemeStyleboxOverride("fill", UiKit.Box(p.Overheated ? UiKit.Danger : UiKit.Info, default, 0, 2, 0, 0));
                break;
            case SoldierClass.Heavy:
                _resourceLabel.Text = p.Overheated ? "SURCHAUFFE" : "CHALEUR";
                _resource.Value = p.Heat / 100f;
                _resource.AddThemeStyleboxOverride("fill", UiKit.Box(p.Overheated || p.Heat > 75 ? UiKit.Danger : UiKit.Accent, default, 0, 2, 0, 0));
                break;
            case SoldierClass.Support:
                _resourceLabel.Text = "APPUI";
                _resource.Value = Mathf.Clamp(p.CallCredits / p.CallCost, 0, 1);
                _resource.AddThemeStyleboxOverride("fill", UiKit.Box(UiKit.Accent, default, 0, 2, 0, 0));
                break;
            default:
                _resourceLabel.Text = "GRENADE";
                _resource.Value = 1f - p.GrenadeCooldown / 9f;
                break;
        }
        var ready = p.AbilityReady >= 0.999f;
        var key = Settings.Describe(Settings.I.GetBinding("ability"));
        var abilityName = p.Def.Id == SoldierClass.Support ? "Barrage d'artillerie" : p.Def.AbilityName;
        _abilityText.Text = ready ? $"{abilityName} · prête [{key}]" : $"{abilityName} · {Mathf.RoundToInt(p.AbilityReady * 100)} %";
        _abilityText.AddThemeColorOverride("font_color", ready ? UiKit.Accent : UiKit.Muted);
        if (p.RallyTime > 0) _abilityText.Text += $"   ·   Ralliement {Mathf.CeilToInt(p.RallyTime)} s";
        if (p.AnchorTime > 0) _abilityText.Text += $"   ·   Ancrage {Mathf.CeilToInt(p.AnchorTime)} s";

        for (var i = 0; i < _weaponRows.Count && i < p.Weapons.Count; i++)
        {
            var ws = p.Weapons[i];
            var row = _weaponRows[i];
            var active = i == p.Current;
            row.name.Text = $"{i + 1}  {ws.W.T.Name}";
            row.sub.Text = $"{Rarities.Names[(int)ws.W.Rarity]} · Nv.{ws.W.Level} · {ws.W.T.Category}";
            row.sub.AddThemeColorOverride("font_color", Rarities.Colors[(int)ws.W.Rarity]);
            if (ws.W.T.Kind == WeaponKind.Beam) row.ammo.Text = $"{Mathf.RoundToInt(ws.BeamCharge * 100)}%";
            else if (ws.W.Magazine <= 1) row.ammo.Text = ws.Reloading ? "…" : "PRÊT";
            else row.ammo.Text = $"{ws.Ammo}";
            row.ammo.AddThemeFontSizeOverride("font_size", active ? 30 : 20);
            row.ammo.AddThemeColorOverride("font_color", ws.Ammo == 0 && ws.W.T.Kind != WeaponKind.Beam ? UiKit.Danger : UiKit.Text);
            row.reload.Visible = ws.Reloading;
            row.reload.Value = ws.ReloadProgress;
            row.panel.Modulate = new Color(1, 1, 1, active ? 1f : 0.55f);
            row.panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.03f, 0.04f, 0.05f, active ? 0.72f : 0.45f), active ? UiKit.Accent : default, active ? 1 : 0, 6, 16, 10));
        }

        _radioTime -= dt;
        _radioBox.Visible = _radioTime > 0;
        _toastTime -= dt;
        _toast.Modulate = new Color(1, 1, 1, Mathf.Clamp(_toastTime, 0, 1));

        var mat = (ShaderMaterial)_vignette.Material;
        var lowHp = p.Downed ? 0.9f : Mathf.Clamp(1f - p.Health / p.MaxHealth - 0.55f, 0, 0.45f) * 1.5f;
        mat.SetShaderParameter("strength", Mathf.Max(p.DamageFlash, lowHp));

        _reviveBox.Visible = p.ReviveTarget != 0 && !p.Downed;
        _revive.Value = p.ReviveProgress;
        _reviveText.Text = $"Maintenez {Settings.Describe(Settings.I.GetBinding("interact"))} pour relever {(World.Remotes.TryGetValue(p.ReviveTarget, out var rp) ? rp.PlayerName : "")}";
        _downed.Text = p.Downed
            ? (Net.I.IsSolo && !p.SecondChanceUsed ? "À TERRE — un infirmier arrive…" : "À TERRE — attendez qu'un équipier vous relève")
            : "";
        _crosshair.Visible = !p.Downed;
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
        DrawCircle(c, r, new Color(0.03f, 0.04f, 0.05f, 0.6f));
        DrawArc(c, r, 0, Mathf.Tau, 64, new Color(1, 1, 1, 0.15f), 1.5f, true);
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
        foreach (var rp in World.Remotes.Values)
            DrawCircle(Map(rp.GlobalPosition), 4, rp.Downed ? UiKit.Accent : UiKit.Good);
        // Joueur : triangle pointant vers le haut
        DrawColoredPolygon(new[] { c + new Vector2(0, -7), c + new Vector2(5, 5), c + new Vector2(-5, 5) }, Colors.White);
        DrawString(UiKit.HeadFont, c + new Vector2(-5, -r + 16), "N".Length > 0 ? "" : "", HorizontalAlignment.Left, -1, 12, UiKit.Muted);
    }
}
