using Godot;

namespace Rempart;

/// <summary>
/// Direction artistique « invasion » : ciel couvert de fumée, lumière voilée, couleurs éteintes,
/// étalonnage froid dans les ombres et jaunâtre dans les hautes lumières, vignettage et grain.
/// Partagé par les missions et le décor des menus.
/// </summary>
public static class Grade
{
    private static NoiseTexture2D _clouds;
    private static GradientTexture1D _lut;

    /// <summary>Couverture nuageuse sale (texture de bruit réutilisée par tous les ciels).</summary>
    public static NoiseTexture2D Clouds => _clouds ??= new NoiseTexture2D
    {
        Width = 1024,
        Height = 512,
        Seamless = true,
        Noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = 0.006f,
            FractalOctaves = 5,
            FractalGain = 0.55f,
            Seed = 7,
        },
    };

    /// <summary>Étalonnage par luminance : ombres bleu-vert éteint, tons moyens gris-brun, hautes lumières ivoire sale.</summary>
    public static GradientTexture1D Lut => _lut ??= new GradientTexture1D
    {
        Width = 256,
        Gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.18f, 0.5f, 0.82f, 1f },
            Colors = new[]
            {
                new Color(0.02f, 0.026f, 0.03f),
                new Color(0.17f, 0.19f, 0.2f),
                new Color(0.5f, 0.49f, 0.45f),
                new Color(0.84f, 0.8f, 0.7f),
                new Color(1f, 0.96f, 0.88f),
            },
        },
    };

    /// <summary>Ciel, soleil et brouillard d'une ville envahie, selon l'heure et la météo.</summary>
    public static void Apply(Godot.Environment env, ProceduralSkyMaterial sky, DirectionalLight3D sun, TimeOfDay time, Weather weather, float fog)
    {
        sky.SkyCover = Clouds;
        sky.SunAngleMax = 12;
        sky.SunCurve = 0.04f;
        // Lumière ambiante en couleur fixe (mêlée au ciel) : sous un ciel de fumée, les ombres ne doivent pas tomber au noir.
        env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        env.AmbientLightSkyContribution = 0.35f;
        env.FogEnabled = true;
        env.FogAerialPerspective = 0.75f;
        env.FogSkyAffect = 0.45f;
        env.AdjustmentEnabled = true;
        env.AdjustmentContrast = 1.12f;
        env.AdjustmentSaturation = 0.68f;
        env.AdjustmentColorCorrection = Lut;
        env.GlowEnabled = true;
        env.GlowIntensity = 0.55f;
        env.GlowBloom = 0.04f;
        env.GlowHdrThreshold = 1.0f;
        switch (time)
        {
            case TimeOfDay.Day:
                // Jour voilé par la fumée des incendies : soleil pâle, horizon jaunâtre.
                sky.SkyTopColor = new Color(0.3f, 0.32f, 0.33f);
                sky.SkyHorizonColor = new Color(0.56f, 0.52f, 0.44f);
                sky.SkyCoverModulate = new Color(0.36f, 0.35f, 0.33f, 0.85f);
                sky.GroundHorizonColor = new Color(0.4f, 0.38f, 0.34f);
                sky.GroundBottomColor = new Color(0.12f, 0.12f, 0.11f);
                sun.RotationDegrees = new Vector3(-38, -35, 0);
                sun.LightEnergy = 1.35f;
                sun.LightColor = new Color(1f, 0.87f, 0.68f);
                env.AmbientLightColor = new Color(0.42f, 0.42f, 0.4f);
                env.AmbientLightEnergy = 0.75f;
                env.FogLightColor = new Color(0.47f, 0.46f, 0.44f);
                env.FogDensity = fog * 1.9f + 0.002f;
                env.TonemapExposure = 1.05f;
                break;
            case TimeOfDay.Dusk:
                // Crépuscule rouge sang sous un plafond de fumée.
                sky.SkyTopColor = new Color(0.1f, 0.09f, 0.1f);
                sky.SkyHorizonColor = new Color(0.62f, 0.25f, 0.12f);
                sky.SkyCoverModulate = new Color(0.16f, 0.1f, 0.09f, 0.9f);
                sky.GroundHorizonColor = new Color(0.3f, 0.16f, 0.1f);
                sky.GroundBottomColor = new Color(0.06f, 0.05f, 0.05f);
                sun.RotationDegrees = new Vector3(-16, 70, 0);
                sun.LightEnergy = 1.4f;
                sun.LightColor = new Color(1f, 0.56f, 0.34f);
                env.AmbientLightColor = new Color(0.4f, 0.32f, 0.28f);
                env.AmbientLightEnergy = 0.9f;
                env.FogLightColor = new Color(0.5f, 0.27f, 0.17f);
                env.FogDensity = fog * 1.2f + 0.002f;
                env.TonemapExposure = 1.2f;
                break;
            default:
                // Nuit sans lune : la ville ne brille plus que de ses incendies (lueur orange à l'horizon).
                sky.SkyTopColor = new Color(0.03f, 0.035f, 0.045f);
                sky.SkyHorizonColor = new Color(0.24f, 0.12f, 0.06f);
                sky.SkyCoverModulate = new Color(0.06f, 0.04f, 0.035f, 0.9f);
                sky.GroundHorizonColor = new Color(0.08f, 0.05f, 0.04f);
                sky.GroundBottomColor = new Color(0.01f, 0.01f, 0.01f);
                sun.RotationDegrees = new Vector3(-40, 150, 0);
                sun.LightEnergy = 0.4f;
                sun.LightColor = new Color(0.55f, 0.62f, 0.85f);
                // Ambiance nocturne lisible : lumière d'appoint froide, indépendante du ciel presque noir.
                env.AmbientLightColor = new Color(0.22f, 0.24f, 0.3f);
                env.AmbientLightEnergy = 1.2f;
                env.FogLightColor = new Color(0.12f, 0.1f, 0.1f);
                env.FogDensity = fog * 0.7f + 0.002f;
                env.TonemapExposure = 1.6f;
                break;
        }
        if (weather != Weather.Clear)
        {
            // Pluie ou orage : plafond bas et lourd, presque plus de soleil direct.
            sun.LightEnergy *= 0.55f;
            env.AmbientLightEnergy *= 1.35f;
            env.FogDensity *= 1.25f;
            sky.SkyTopColor = sky.SkyTopColor.Lerp(new Color(0.16f, 0.17f, 0.18f), 0.6f);
            sky.SkyHorizonColor = sky.SkyHorizonColor.Lerp(new Color(0.3f, 0.3f, 0.3f), 0.6f);
            sky.SkyCoverModulate = sky.SkyCoverModulate.Darkened(0.35f);
        }
    }

    private const string PostShader = @"
shader_type canvas_item;
uniform float vignette = 0.55;
uniform float grain = 0.045;
void fragment() {
    vec2 uv = SCREEN_UV - 0.5;
    uv.x *= SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
    float v = smoothstep(0.35, 0.95, length(uv)) * vignette;
    float n = fract(sin(dot(FRAGCOORD.xy + fract(TIME * 7.13) * 91.7, vec2(12.9898, 78.233))) * 43758.5453) - 0.5;
    // Grain doux (clair ou sombre) sous un vignettage noir, composés en une seule couche.
    float ga = abs(n) * grain * 2.0 * (1.0 - v);
    float a = v + ga;
    COLOR = vec4(vec3(step(0.0, n)) * ga / max(a, 1e-4), a);
}";

    /// <summary>Calque de vignettage et de grain posé sur la 3D, sous le HUD.</summary>
    public static CanvasLayer Overlay()
    {
        var layer = new CanvasLayer { Layer = -1, Name = "Grade" };
        var rect = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = new Shader { Code = PostShader } } };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(rect);
        return layer;
    }
}
