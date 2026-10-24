using Godot;

namespace Rempart;

/// <summary>Pluie autour de la caméra et, par orage, éclairs avec tonnerre.</summary>
public partial class WeatherFx : Node3D
{
    private World _world;
    private Weather _weather;
    private GpuParticles3D _rain;
    private DirectionalLight3D _flash;
    private float _nextLightning = 6f;
    private float _flashT;

    public static WeatherFx Create(World w, Weather weather)
    {
        var fx = new WeatherFx { _world = w, _weather = weather, Name = "Weather" };
        w.AddChild(fx);
        return fx;
    }

    public override void _Ready()
    {
        if (_weather == Weather.Sandstorm) { BuildSandstorm(); return; } // WeatherFx.Sand.cs
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(45, 1, 45),
            Direction = new Vector3(0.15f, -1, 0.05f),
            Spread = 2,
            InitialVelocityMin = 38,
            InitialVelocityMax = 46,
            Gravity = Vector3.Zero,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.75f, 0.8f, 0.88f, _weather == Weather.Storm ? 0.32f : 0.25f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY,
        };
        _rain = new GpuParticles3D
        {
            Amount = _weather == Weather.Storm ? 9000 : 6000,
            Lifetime = 1.4f,
            ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.025f, 1.1f), Material = mat },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-60, -80, -60), new Vector3(120, 120, 120)),
            Preprocess = 1.5f,
        };
        AddChild(_rain);
        if (_weather == Weather.Storm)
        {
            _flash = new DirectionalLight3D { LightEnergy = 0, LightColor = new Color(0.8f, 0.85f, 1f), RotationDegrees = new Vector3(-60, 30, 0), ShadowEnabled = false };
            AddChild(_flash);
        }
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        var cam = GetViewport().GetCamera3D();
        if (_rain == null) { ProcessSandstorm(dt, cam); return; }
        if (cam != null) _rain.GlobalPosition = cam.GlobalPosition + Vector3.Up * 30f;
        if (_flash == null) return;
        _nextLightning -= dt;
        if (_nextLightning <= 0)
        {
            _nextLightning = (float)GD.RandRange(7, 16);
            _flashT = 0.35f;
            var delay = _world.GetTree().CreateTimer(GD.RandRange(0.4, 1.6), false);
            delay.Timeout += () => Sfx.Ui("collapse", -6f, 0.55f);
        }
        if (_flashT > 0)
        {
            _flashT -= dt;
            // Double scintillement typique d'un éclair.
            _flash.LightEnergy = _flashT > 0.25f || (_flashT < 0.15f && _flashT > 0.05f) ? 3.5f : 0.3f;
            if (_flashT <= 0) _flash.LightEnergy = 0;
        }
    }
}
