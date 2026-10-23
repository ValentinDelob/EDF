using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Décor 3D vivant derrière les menus : ville au crépuscule, fourmis en approche.
/// Mode « titre » : caméra cinématique ; mode « escouade » : les soldats du salon face caméra.
/// </summary>
public partial class MenuBackground : Node3D
{
    public enum Mode { Title, Squad }

    private Camera3D _cam;
    private float _t;
    private Mode _mode = Mode.Title;
    private readonly List<BugModel> _bugs = new();
    private readonly List<(Node3D root, SoldierModel model, Label3D name, Label3D sub)> _slots = new();
    private readonly List<SoldierModel> _titleSquad = new();
    private Transform3D _camTarget;

    public override void _Ready()
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.14f, 0.18f, 0.32f),
            SkyHorizonColor = new Color(0.92f, 0.52f, 0.3f),
            GroundHorizonColor = new Color(0.45f, 0.32f, 0.26f),
            GroundBottomColor = new Color(0.1f, 0.09f, 0.09f),
            SunAngleMax = 30,
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Agx,
            TonemapExposure = 1.05f,
            GlowEnabled = true,
            GlowIntensity = 0.7f,
            FogEnabled = true,
            FogDensity = 0.006f,
            FogLightColor = new Color(0.75f, 0.5f, 0.38f),
            FogAerialPerspective = 0.7f,
            SsaoEnabled = true,
            VolumetricFogEnabled = Settings.I.Quality >= 2,
            VolumetricFogDensity = 0.008f,
            VolumetricFogAlbedo = new Color(0.8f, 0.55f, 0.4f),
        };
        AddChild(new WorldEnvironment { Environment = env });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-14, 68, 0),
            LightEnergy = 1.3f,
            LightColor = new Color(1f, 0.63f, 0.4f),
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 150f,
        });
        // Lumière d'appoint pour les soldats du salon (éclairage « studio »).
        AddChild(new OmniLight3D { Position = new Vector3(4, 4, 10), LightEnergy = 1.4f, OmniRange = 16, LightColor = new Color(0.85f, 0.9f, 1f) });
        CityBuilder.Build(this, TimeOfDay.Dusk);

        // Escouade du mode titre, de dos, face aux fourmis.
        var classes = new[] { SoldierClass.Rifleman, SoldierClass.Heavy, SoldierClass.Airborne, SoldierClass.Support };
        for (var i = 0; i < classes.Length; i++)
        {
            var s = SoldierModel.Create(classes[i]);
            AddChild(s);
            s.Position = new Vector3(-3f + i * 2.1f, 0, 4f + (i % 2) * 1.2f);
            s.RotationDegrees = new Vector3(0, 180 + (i - 1.5f) * 8f, 0);
            _titleSquad.Add(s);
        }
        // Emplacements du salon (cachés en mode titre).
        for (var i = 0; i < 4; i++)
        {
            var root = new Node3D { Position = SlotPosition(i, false), Visible = false };
            AddChild(root);
            var name = new Label3D { FontSize = 64, OutlineSize = 12, PixelSize = 0.0042f, Position = new Vector3(0, -0.2f, 0.6f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Font = UiKit.HeadFont };
            var sub = new Label3D { FontSize = 40, OutlineSize = 10, PixelSize = 0.0042f, Position = new Vector3(0, -0.42f, 0.6f), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = UiKit.Muted, Font = UiKit.BodyFont };
            root.AddChild(name);
            root.AddChild(sub);
            _slots.Add((root, null, name, sub));
        }

        var rng = new RandomNumberGenerator { Seed = 5 };
        for (var i = 0; i < 9; i++)
        {
            var b = BugModel.Create(i == 4 ? EnemyType.RedAnt : EnemyType.BlackAnt);
            AddChild(b);
            b.Position = new Vector3(rng.RandfRange(-6, 6), 0, -30 - i * 7 + rng.RandfRange(-2, 2));
            _bugs.Add(b);
        }
        var w = BugModel.Create(EnemyType.Wasp);
        AddChild(w);
        w.Position = new Vector3(10, 16, -40);
        _bugs.Add(w);

        _cam = new Camera3D { Current = true, Fov = 55, Far = 3000 };
        AddChild(_cam);
        SetMode(Mode.Title);
        _cam.GlobalTransform = TitleTransform();
    }

    private static Vector3 SlotPosition(int i, bool solo) =>
        solo ? new Vector3(6.2f, 0, 3.6f) : new Vector3(4.0f + i * 2.2f, 0, 3.4f + (i % 2 == 0 ? 0 : -0.5f));

    public void SetMode(Mode mode)
    {
        _mode = mode;
        foreach (var s in _titleSquad) s.Visible = mode == Mode.Title;
        foreach (var slot in _slots) slot.root.Visible = mode == Mode.Squad;
    }

    /// <summary>Affiche les joueurs du salon (1 à 4) face caméra ; les places libres montrent « + ».</summary>
    public void SetSquad(IReadOnlyList<PlayerInfo> players, bool solo)
    {
        for (var i = 0; i < _slots.Count; i++)
        {
            var (root, model, name, sub) = _slots[i];
            var p = i < players.Count ? players[i] : null;
            if (model != null && (p == null || model.Class != p.Class))
            {
                model.QueueFree();
                model = null;
            }
            if (p != null && model == null)
            {
                model = SoldierModel.Create(p.Class);
                root.AddChild(model);
                model.RotationDegrees = new Vector3(0, 8 - i * 6, 0);
            }
            _slots[i] = (root, model, name, sub);
            root.Position = SlotPosition(i, solo);
            root.Visible = _mode == Mode.Squad && (p != null || !solo);
            if (p != null)
            {
                name.Text = p.Name;
                name.Modulate = UiKit.Text;
                sub.Text = Classes.Get(p.Class).Name + (solo || p.Id == 1 ? "" : p.Ready ? "  ·  PRÊT" : "");
                sub.Modulate = !solo && p.Ready ? UiKit.Good : UiKit.Muted;
                name.Position = new Vector3(0, -0.2f, 0.6f);
            }
            else
            {
                name.Text = "+";
                name.Modulate = UiKit.Dim;
                name.Position = new Vector3(0, 1.1f, 0.6f);
                sub.Text = "Place libre";
                sub.Modulate = UiKit.Dim;
            }
        }
    }

    private Transform3D TitleTransform()
    {
        var a = Mathf.Sin(_t * 0.05f) * 0.35f;
        var pos = new Vector3(Mathf.Sin(a) * 16f + 3f, 3.2f + Mathf.Sin(_t * 0.13f) * 0.3f, 14f + Mathf.Cos(a) * 4f);
        return Transform3D.Identity.Translated(pos).LookingAt(new Vector3(-2f, 2.6f, -14f), Vector3.Up);
    }

    private Transform3D SquadTransform()
    {
        var pos = new Vector3(3.0f + Mathf.Sin(_t * 0.2f) * 0.15f, 1.6f, 11f);
        return Transform3D.Identity.Translated(pos).LookingAt(new Vector3(4.4f, 1.1f, 3.4f), Vector3.Up);
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _t += dt;
        _camTarget = _mode == Mode.Title ? TitleTransform() : SquadTransform();
        _cam.GlobalTransform = _cam.GlobalTransform.InterpolateWith(_camTarget, Mathf.Clamp(dt * 3f, 0, 1));
        foreach (var b in _bugs)
        {
            if (b.Type == EnemyType.Wasp)
            {
                b.Position = new Vector3(10 + Mathf.Sin(_t * 0.4f) * 14, 16 + Mathf.Sin(_t) * 1.5f, -40 + Mathf.Cos(_t * 0.4f) * 10);
                b.Rotation = new Vector3(0, _t * 0.4f + Mathf.Pi / 2, 0);
                b.Animate(dt, 8, 0);
                continue;
            }
            var p = b.Position;
            p.Z += dt * 2.2f;
            if (p.Z > -12) p.Z = -78;
            b.Position = p;
            b.Animate(dt, 2.2f, Mathf.Max(0, Mathf.Sin(_t * 2 + p.X)) * 0.5f);
        }
        foreach (var s in _titleSquad) if (s.Visible) s.Animate(dt, 0, -0.05f, false, false, false, true, 0);
        foreach (var slot in _slots) slot.model?.Animate(dt, 0, -0.15f, false, false, false, false, 0);
    }
}
