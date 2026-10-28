using Godot;

namespace Rempart;

/// <summary>
/// Direction artistique inspirée de Monster Attack (EDF, PS2) au rendu actuel : par beau temps, grand ciel bleu à cumulus,
/// plein soleil franc et ombres bleutées, couleurs vives, brume claire de lointain ; crépuscule orangé, nuit bleue lisible.
/// Sous la pluie et l'orage : plafond gris bas et couleurs éteintes. Vignettage léger et grain fin.
/// Partagé par les missions et le décor des menus.
/// </summary>
public static class Grade
{
    private static GradientTexture1D _lut, _clearLut;
    private static Shader _skyShader;

    /// <summary>Courbes par beau temps : noirs à peine bleutés, tons moyens neutres, hautes lumières légèrement chaudes.</summary>
    public static GradientTexture1D ClearLut => _clearLut ??= new GradientTexture1D
    {
        Width = 256,
        Gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.2f, 0.5f, 0.8f, 1f },
            Colors = new[]
            {
                new Color(0.0f, 0.006f, 0.02f),
                new Color(0.17f, 0.185f, 0.215f),
                new Color(0.5f, 0.5f, 0.49f),
                new Color(0.83f, 0.82f, 0.78f),
                new Color(1f, 0.99f, 0.95f),
            },
        },
    };

    /// <summary>Courbes par temps couvert : ombres gris-bleu, tons moyens gris neutre, hautes lumières blanc cassé.</summary>
    public static GradientTexture1D Lut => _lut ??= new GradientTexture1D
    {
        Width = 256,
        Gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.18f, 0.5f, 0.82f, 1f },
            Colors = new[]
            {
                new Color(0.02f, 0.025f, 0.03f),
                new Color(0.16f, 0.18f, 0.19f),
                new Color(0.48f, 0.49f, 0.48f),
                new Color(0.8f, 0.8f, 0.77f),
                new Color(0.97f, 0.96f, 0.93f),
            },
        },
    };

    // Ciel couvert procédural : deux couches de nuages (bruit fractal projeté sur un plafond),
    // éclairées côté soleil, assombries en dessous, fondues dans la brume à l'horizon.
    private const string SkyCode = @"
shader_type sky;
uniform vec3 zenith : source_color = vec3(0.3);
uniform vec3 horizon : source_color = vec3(0.5);
uniform vec3 ground : source_color = vec3(0.1);
uniform vec3 cloud_lit : source_color = vec3(0.6);
uniform vec3 cloud_dark : source_color = vec3(0.2);
uniform float coverage = 0.6;
uniform float wind = 0.004;

// Hachage sur cellules repliées (mod 289) : reste précis même loin de l'origine (sinon facettes près de l'horizon).
float hash(vec2 p) { p = mod(p, 289.0); p = fract(p * vec2(0.1031, 0.1030)); p += dot(p, p.yx + 33.33); return fract((p.x + p.y) * p.x); }
float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
float fbm(vec2 p, int oct) {
    float v = 0.0, a = 0.5;
    mat2 r = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
    for (int i = 0; i < oct; i++) { v += a * noise(p); p = r * p * 2.03 + 17.1; a *= 0.5; }
    return v;
}
float density(vec2 p, int oct) {
    float n = fbm(p, oct);
    return smoothstep(1.0 - coverage - 0.15, 1.0 - coverage + 0.35, n);
}

void sky() {
    vec3 d = EYEDIR;
    float up = max(d.y, 0.0);
    vec3 col = mix(horizon, zenith, pow(up, 0.6));
    if (d.y < 0.0) col = mix(horizon, ground, clamp(-d.y * 4.0, 0.0, 1.0));
    vec3 sd = normalize(LIGHT0_DIRECTION);
    float sun = pow(max(dot(d, sd), 0.0), 600.0) * LIGHT0_ENERGY;
    float glow = pow(max(dot(d, sd), 0.0), 8.0) * 0.25 * LIGHT0_ENERGY;
    if (d.y > 0.0) {
        int oct = AT_CUBEMAP_PASS ? 3 : 6;
        // Projection sur un plafond nuageux : les nuages s'aplatissent vers l'horizon.
        vec2 uv = d.xz / (d.y + 0.12) * 1.6 + vec2(TIME * wind, TIME * wind * 0.6);
        float c1 = density(uv, oct);
        float c2 = density(uv * 0.45 + 31.0 - vec2(TIME * wind * 0.5, 0.0), oct);
        float c = clamp(max(c1, c2 * 0.9), 0.0, 1.0);
        // Éclairage : on échantillonne vers le soleil ; plus il y a de nuage devant, plus le dessous est sombre.
        vec2 toward = normalize(sd.xz + 1e-4) * 0.12;
        float shade = density(uv + toward, AT_CUBEMAP_PASS ? 2 : 4);
        vec3 cc = mix(cloud_lit, cloud_dark, clamp(shade * 0.85 + c * 0.25, 0.0, 1.0));
        cc += LIGHT0_COLOR * glow * (1.0 - shade) * 0.6;
        float fade = smoothstep(0.0, 0.18, d.y);
        col = mix(col + LIGHT0_COLOR * glow, cc, c * fade);
        sun *= 1.0 - c;
    }
    COLOR = col + LIGHT0_COLOR * sun * 0.6;
}";

    private static ShaderMaterial NewSky() => new() { Shader = _skyShader ??= new Shader { Code = SkyCode } };

    /// <summary>Ciel, soleil et brouillard d'une mission, selon l'heure et la météo.</summary>
    public static void Apply(Godot.Environment env, DirectionalLight3D sun, TimeOfDay time, Weather weather, float fog)
    {
        var sky = NewSky();
        env.BackgroundMode = Godot.Environment.BGMode.Sky;
        env.Sky = new Sky { SkyMaterial = sky, ProcessMode = Sky.ProcessModeEnum.Incremental, RadianceSize = Sky.RadianceSizeEnum.Size128 };
        env.ReflectedLightSource = Godot.Environment.ReflectionSource.Sky;
        // Lumière ambiante en couleur fixe (mêlée au ciel) : ombres bleutées par beau temps, jamais noires sous un ciel couvert.
        env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
        env.AmbientLightSkyContribution = 0.35f;
        env.FogEnabled = true;
        env.FogAerialPerspective = 0.7f;
        env.FogSkyAffect = 0.25f;
        env.TonemapMode = Godot.Environment.ToneMapper.Aces;
        env.TonemapWhite = 6f;
        env.AdjustmentEnabled = true;
        env.AdjustmentContrast = 1.05f;
        env.AdjustmentSaturation = 1.12f;
        env.AdjustmentColorCorrection = ClearLut;
        env.GlowEnabled = true;
        env.GlowIntensity = 0.55f;
        env.GlowBloom = 0.04f;
        env.GlowHdrThreshold = 1.0f;
        void Clouds(Color zenith, Color horizon, Color ground, Color lit, Color dark, float coverage)
        {
            sky.SetShaderParameter("zenith", zenith);
            sky.SetShaderParameter("horizon", horizon);
            sky.SetShaderParameter("ground", ground);
            sky.SetShaderParameter("cloud_lit", lit);
            sky.SetShaderParameter("cloud_dark", dark);
            sky.SetShaderParameter("coverage", coverage);
        }
        var stormy = weather != Weather.Clear;
        switch (time)
        {
            case TimeOfDay.Day when !stormy:
                // Grand beau temps de Monster Attack : ciel bleu profond, cumulus blancs épars, soleil haut et franc,
                // ombres bleutées, brume claire qui bleuit le lointain.
                Clouds(new Color(0.22f, 0.45f, 0.84f), new Color(0.7f, 0.8f, 0.9f), new Color(0.34f, 0.35f, 0.36f),
                    new Color(1f, 1f, 0.98f), new Color(0.58f, 0.63f, 0.72f), 0.34f);
                sun.RotationDegrees = new Vector3(-50, -35, 0);
                sun.LightEnergy = 2.3f;
                sun.LightColor = new Color(1f, 0.95f, 0.87f);
                env.AmbientLightColor = new Color(0.6f, 0.66f, 0.76f);
                env.AmbientLightEnergy = 0.6f;
                env.FogLightColor = new Color(0.72f, 0.8f, 0.9f);
                env.FogDensity = fog * 0.9f + 0.001f;
                env.TonemapExposure = 1.0f;
                break;
            case TimeOfDay.Day:
                // Jour de pluie : plafond gris bas, le soleil perce à peine.
                Clouds(new Color(0.34f, 0.36f, 0.38f), new Color(0.55f, 0.56f, 0.56f), new Color(0.2f, 0.2f, 0.2f),
                    new Color(0.66f, 0.66f, 0.65f), new Color(0.24f, 0.25f, 0.27f), 0.85f);
                sun.RotationDegrees = new Vector3(-38, -35, 0);
                sun.LightEnergy = 1.7f;
                sun.LightColor = new Color(0.96f, 0.93f, 0.86f);
                env.AmbientLightColor = new Color(0.46f, 0.48f, 0.5f);
                env.AmbientLightEnergy = 0.8f;
                env.FogLightColor = new Color(0.5f, 0.51f, 0.52f);
                env.FogDensity = fog * 1.6f + 0.002f;
                env.TonemapExposure = 1.1f;
                break;
            case TimeOfDay.Dusk when !stormy:
                // Coucher de soleil : bande orangée à l'horizon, zénith bleu-violet, nuages rosés par en dessous.
                Clouds(new Color(0.16f, 0.22f, 0.44f), new Color(0.98f, 0.6f, 0.34f), new Color(0.1f, 0.08f, 0.08f),
                    new Color(1f, 0.72f, 0.5f), new Color(0.3f, 0.25f, 0.36f), 0.42f);
                sun.RotationDegrees = new Vector3(-15, 70, 0);
                sun.LightEnergy = 2.0f;
                sun.LightColor = new Color(1f, 0.8f, 0.6f);
                env.AmbientLightColor = new Color(0.46f, 0.5f, 0.64f);
                env.AmbientLightEnergy = 0.7f;
                env.FogLightColor = new Color(0.66f, 0.56f, 0.54f);
                env.FogDensity = fog * 0.7f + 0.001f;
                env.TonemapExposure = 1.05f;
                break;
            case TimeOfDay.Dusk:
                // Crépuscule d'orage : une bande rouille sous des nuages gris-violet.
                Clouds(new Color(0.12f, 0.12f, 0.14f), new Color(0.5f, 0.3f, 0.2f), new Color(0.08f, 0.07f, 0.07f),
                    new Color(0.52f, 0.36f, 0.28f), new Color(0.12f, 0.11f, 0.12f), 0.85f);
                sun.RotationDegrees = new Vector3(-18, 70, 0);
                sun.LightEnergy = 2.4f;
                sun.LightColor = new Color(1f, 0.74f, 0.56f);
                env.AmbientLightColor = new Color(0.46f, 0.45f, 0.5f);
                env.AmbientLightEnergy = 1.0f;
                env.FogLightColor = new Color(0.46f, 0.41f, 0.4f);
                env.FogDensity = fog * 1.0f + 0.002f;
                env.TonemapExposure = 1.3f;
                break;
            default:
                // Nuit claire : ciel bleu nuit, nuages argentés par la lune, éclairage lunaire froid et lisible.
                Clouds(new Color(0.02f, 0.035f, 0.08f), new Color(0.08f, 0.1f, 0.17f), new Color(0.01f, 0.012f, 0.015f),
                    new Color(0.2f, 0.22f, 0.3f), new Color(0.03f, 0.035f, 0.05f), stormy ? 0.85f : 0.42f);
                sun.RotationDegrees = new Vector3(-42, 150, 0);
                sun.LightEnergy = 0.7f;
                sun.LightColor = new Color(0.62f, 0.72f, 1f);
                env.AmbientLightColor = new Color(0.22f, 0.27f, 0.4f);
                env.AmbientLightEnergy = 1.1f;
                env.FogLightColor = new Color(0.08f, 0.1f, 0.15f);
                env.FogDensity = fog * 0.7f + 0.002f;
                env.TonemapExposure = 1.45f;
                break;
        }
        if (stormy)
        {
            // Pluie ou orage : plafond bas et lourd, presque plus de soleil direct, couleurs éteintes.
            sun.LightEnergy *= 0.55f;
            env.AmbientLightEnergy *= 1.35f;
            env.FogDensity *= 1.25f;
            env.AdjustmentSaturation = 0.78f;
            env.AdjustmentContrast = 1.1f;
            env.AdjustmentColorCorrection = Lut;
            sky.SetShaderParameter("wind", 0.012f);
        }
    }

    private const string PostShader = @"
shader_type canvas_item;
uniform float vignette = 0.3;
uniform float grain = 0.02;
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
