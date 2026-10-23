using Godot;

namespace Rempart;

/// <summary>Décor 3D vivant derrière les menus : la ville au crépuscule, des fourmis et une escouade.</summary>
public partial class MenuBackground : Node3D
{
    private Camera3D _cam;
    private float _t;
    private readonly System.Collections.Generic.List<BugModel> _bugs = new();
    private readonly System.Collections.Generic.List<SoldierModel> _soldiers = new();

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
        CityBuilder.Build(this, TimeOfDay.Dusk);

        // Escouade au premier plan, sur le carrefour central.
        var classes = new[] { SoldierClass.Rifleman, SoldierClass.Heavy, SoldierClass.Airborne, SoldierClass.Support };
        for (var i = 0; i < classes.Length; i++)
        {
            var s = SoldierModel.Create(classes[i]);
            AddChild(s);
            s.Position = new Vector3(-3f + i * 2.1f, 0, 4f + (i % 2) * 1.2f);
            s.RotationDegrees = new Vector3(0, 180 + (i - 1.5f) * 8f, 0);
            _soldiers.Add(s);
        }
        // Fourmis en approche dans la rue.
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
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _t += dt;
        var a = Mathf.Sin(_t * 0.05f) * 0.35f;
        _cam.Position = new Vector3(Mathf.Sin(a) * 16f + 3f, 3.2f + Mathf.Sin(_t * 0.13f) * 0.3f, 14f + Mathf.Cos(a) * 4f);
        _cam.LookAt(new Vector3(-2f, 2.6f, -14f), Vector3.Up);
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
        foreach (var s in _soldiers) s.Animate(dt, 0, -0.05f, false, false, false, true, 0);
    }
}
