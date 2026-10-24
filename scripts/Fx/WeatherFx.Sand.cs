using Godot;

namespace Rempart;

/// <summary>
/// Tempête de sable (<see cref="Weather.Sandstorm"/>, base militaire du désert) : filets de sable chassés à l'horizontale
/// autour de la caméra, nappes de poussière qui roulent au ras du sol, rafales qui épaississent le brouillard par
/// moments. L'étalonnage (brouillard brun, soleil voilé) vient de <see cref="DesertBuilder.ApplyGrade"/>.
/// </summary>
public partial class WeatherFx
{
    /// <summary>Direction du vent de sable (plan horizontal, légèrement plongeante).</summary>
    public static readonly Vector3 SandWind = new Vector3(1f, -0.04f, 0.32f).Normalized();

    private GpuParticles3D _grains;
    private DustDrift _drift;
    private float _baseFog = -1f, _gustT;

    private void BuildSandstorm()
    {
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(42, 7, 42),
            Direction = SandWind,
            Spread = 6,
            InitialVelocityMin = 24,
            InitialVelocityMax = 34,
            Gravity = new Vector3(0, -1.5f, 0),
            ScaleMin = 0.5f,
            ScaleMax = 1.4f,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.5f, 0.43f, 0.33f, 0.26f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false,
        };
        // Filets fins et allongés à l'écran : le vent est presque horizontal.
        _grains = new GpuParticles3D
        {
            Name = "SandGrains",
            Amount = 5000,
            Lifetime = 1.5f,
            ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.45f, 0.012f), Material = mat },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-70, -30, -70), new Vector3(140, 60, 140)),
            Preprocess = 1.5f,
        };
        AddChild(_grains);
        _drift = DustDrift.Create(1f);
        AddChild(_drift);
    }

    private void ProcessSandstorm(float dt, Camera3D cam)
    {
        if (cam != null) _grains.GlobalPosition = cam.GlobalPosition - SandWind * 20f + Vector3.Up * 2f;
        // Rafales : le brouillard s'épaissit et se dissipe lentement (deux périodes non multiples).
        var env = _world?.Env;
        if (env == null) return;
        if (_baseFog < 0f) _baseFog = env.FogDensity;
        _gustT += dt;
        var gust = 0.5f + 0.32f * Mathf.Sin(_gustT * 0.23f) + 0.18f * Mathf.Sin(_gustT * 0.61f + 1.3f);
        env.FogDensity = _baseFog * (0.75f + 0.55f * gust);
    }
}

/// <summary>
/// Nappes de poussière qui dérivent au ras du sol autour de la caméra (grandes taches douces, sans ombre ni
/// collision) : voile de chaleur et de sable des cartes désertiques, renforcé par la tempête.
/// </summary>
public partial class DustDrift : Node3D
{
    private GpuParticles3D _sheets;
    private float _strength;

    public static DustDrift Create(float strength) => new() { Name = "DustDrift", _strength = strength };

    public override void _Ready()
    {
        var tex = new GradientTexture2D
        {
            Width = 64,
            Height = 64,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f),
            FillTo = new Vector2(1f, 0.5f),
            Gradient = new Gradient { Colors = new[] { new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.45f), new Color(1, 1, 1, 0) }, Offsets = new[] { 0f, 0.45f, 1f } },
        };
        var storm = _strength >= 0.99f;
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(storm ? 60 : 80, 2.5f, storm ? 60 : 80),
            Direction = WeatherFx.SandWind,
            Spread = 10,
            InitialVelocityMin = storm ? 9 : 1.5f,
            InitialVelocityMax = storm ? 15 : 3.5f,
            Gravity = Vector3.Zero,
            ScaleMin = 0.7f,
            ScaleMax = 1.6f,
            AngleMin = -180,
            AngleMax = 180,
            Color = new Color(1, 1, 1, 1),
            AlphaCurve = new CurveTexture { Curve = Fade() },
        };
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = tex,
            AlbedoColor = storm ? new Color(0.56f, 0.48f, 0.37f, 0.3f) : new Color(0.62f, 0.56f, 0.47f, 0.09f),
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            ProximityFadeEnabled = true,
            ProximityFadeDistance = 3f,
            DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha,
            DistanceFadeMinDistance = 1.5f,
            DistanceFadeMaxDistance = 9f,
        };
        _sheets = new GpuParticles3D
        {
            Name = "DustSheets",
            // Peu de nappes, de taille moyenne : une nappe collée à la caméra couvre tout l'écran (coût de remplissage).
            Amount = storm ? 44 : 16,
            Lifetime = storm ? 6f : 14f,
            ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = storm ? new Vector2(12f, 6f) : new Vector2(14f, 4f), Material = mat },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-110, -20, -110), new Vector3(220, 50, 220)),
            Preprocess = storm ? 6f : 14f,
            Position = Vector3.Up * 2.5f,
        };
        AddChild(_sheets);
    }

    /// <summary>Apparition et disparition progressives de chaque nappe.</summary>
    private static Curve Fade()
    {
        var c = new Curve();
        c.AddPoint(new Vector2(0, 0));
        c.AddPoint(new Vector2(0.25f, 1));
        c.AddPoint(new Vector2(0.7f, 1));
        c.AddPoint(new Vector2(1, 0));
        return c;
    }

    public override void _Process(double delta)
    {
        // L'émetteur suit la caméra au niveau du sol (les nappes restent posées, la caméra peut monter).
        if (GetViewport().GetCamera3D() is { } cam) GlobalPosition = new Vector3(cam.GlobalPosition.X, 0, cam.GlobalPosition.Z) - WeatherFx.SandWind * 25f;
    }
}
