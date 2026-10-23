using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>Matériaux PBR générés procéduralement (textures de bruit), mis en cache.</summary>
public static class Mats
{
    private static readonly Dictionary<string, Material> Cache = new();

    private static NoiseTexture2D Noise(int seed, float freq, bool normal, int size = 512, float bump = 4f, FastNoiseLite.NoiseTypeEnum type = FastNoiseLite.NoiseTypeEnum.SimplexSmooth, int octaves = 5)
    {
        var n = new FastNoiseLite
        {
            Seed = seed,
            Frequency = freq,
            NoiseType = type,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = octaves,
        };
        return new NoiseTexture2D
        {
            Width = size,
            Height = size,
            Seamless = true,
            Noise = n,
            AsNormalMap = normal,
            BumpStrength = bump,
            GenerateMipmaps = true,
        };
    }

    private static GradientTexture1D Ramp(Color a, Color b)
    {
        var g = new Gradient();
        g.SetColor(0, a);
        g.SetColor(1, b);
        return new GradientTexture1D { Gradient = g };
    }

    private static NoiseTexture2D ColorNoise(int seed, float freq, Color a, Color b, int size = 512)
    {
        var t = Noise(seed, freq, false, size);
        t.ColorRamp = new Gradient { Colors = new[] { a, b }, Offsets = new[] { 0.2f, 0.8f } };
        return t;
    }

    public static StandardMaterial3D Concrete(Color tint, int seed = 1)
    {
        var key = $"concrete{tint}{seed}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(seed, 0.02f, tint.Darkened(0.12f), tint.Lightened(0.06f)),
            NormalEnabled = true,
            NormalTexture = Noise(seed + 7, 0.05f, true, 512, 3f),
            NormalScale = 0.6f,
            Roughness = 0.92f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.25f, 0.25f, 0.25f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        Cache[key] = mat;
        return mat;
    }

    public static StandardMaterial3D Asphalt()
    {
        if (Cache.TryGetValue("asphalt", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(31, 0.08f, new Color(0.10f, 0.10f, 0.11f), new Color(0.2f, 0.2f, 0.21f)),
            NormalEnabled = true,
            NormalTexture = Noise(32, 0.25f, true, 512, 6f, FastNoiseLite.NoiseTypeEnum.Cellular, 3),
            NormalScale = 0.5f,
            Roughness = 0.85f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.12f, 0.12f, 0.12f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        Cache["asphalt"] = mat;
        return mat;
    }

    public static StandardMaterial3D Sidewalk()
    {
        if (Cache.TryGetValue("sidewalk", out var m)) return (StandardMaterial3D)m;
        var mat = Concrete(new Color(0.55f, 0.54f, 0.52f), 41);
        mat = (StandardMaterial3D)mat.Duplicate();
        mat.Uv1Scale = new Vector3(0.5f, 0.5f, 0.5f);
        Cache["sidewalk"] = mat;
        return mat;
    }

    public static StandardMaterial3D Sand()
    {
        if (Cache.TryGetValue("sand", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(51, 0.03f, new Color(0.72f, 0.64f, 0.48f), new Color(0.86f, 0.79f, 0.62f)),
            NormalEnabled = true,
            NormalTexture = Noise(52, 0.12f, true, 512, 5f),
            Roughness = 0.95f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.15f, 0.15f, 0.15f),
        };
        Cache["sand"] = mat;
        return mat;
    }

    public static StandardMaterial3D Grass()
    {
        if (Cache.TryGetValue("grass", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(61, 0.05f, new Color(0.16f, 0.24f, 0.09f), new Color(0.3f, 0.38f, 0.15f)),
            NormalEnabled = true,
            NormalTexture = Noise(62, 0.3f, true, 512, 4f),
            Roughness = 0.95f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.2f, 0.2f, 0.2f),
        };
        Cache["grass"] = mat;
        return mat;
    }

    public static StandardMaterial3D Dirt()
    {
        if (Cache.TryGetValue("dirt", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(71, 0.04f, new Color(0.22f, 0.17f, 0.12f), new Color(0.38f, 0.3f, 0.22f)),
            NormalEnabled = true,
            NormalTexture = Noise(72, 0.08f, true, 512, 8f),
            Roughness = 1f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.2f, 0.2f, 0.2f),
        };
        Cache["dirt"] = mat;
        return mat;
    }

    /// <summary>Façade d'immeuble : texture de fenêtres générée, UV en mètres (1 unité = 1 étage).</summary>
    public static StandardMaterial3D Facade(int variant, bool night)
    {
        var key = $"facade{variant}{night}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var rng = new RandomNumberGenerator { Seed = (ulong)(variant * 977 + 13) };
        var wall = variant switch
        {
            0 => new Color(0.62f, 0.6f, 0.56f),
            1 => new Color(0.55f, 0.45f, 0.38f),
            2 => new Color(0.42f, 0.44f, 0.47f),
            3 => new Color(0.7f, 0.66f, 0.58f),
            4 => new Color(0.36f, 0.3f, 0.27f),
            _ => new Color(0.5f, 0.52f, 0.5f),
        };
        const int size = 256;
        var img = Image.CreateEmpty(size, size, true, Image.Format.Rgba8);
        var emit = Image.CreateEmpty(size, size, true, Image.Format.Rgba8);
        var rough = Image.CreateEmpty(size, size, true, Image.Format.Rgba8);
        var noise = new FastNoiseLite { Seed = variant, Frequency = 0.05f };
        var cols = variant % 2 == 0 ? 2 : 3;
        var winW = variant == 2 ? 0.8f : 0.55f;
        var lit = new bool[cols];
        for (var i = 0; i < cols; i++) lit[i] = rng.Randf() < 0.35f;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var fx = x / (float)size * cols;
            var cell = (int)fx;
            var lx = fx - cell;
            var ly = y / (float)size;
            var n = noise.GetNoise2D(x, y) * 0.04f;
            var isWin = Mathf.Abs(lx - 0.5f) < winW * 0.5f && ly > 0.28f && ly < 0.82f;
            var isFrame = !isWin && Mathf.Abs(lx - 0.5f) < winW * 0.5f + 0.03f && ly > 0.25f && ly < 0.85f;
            var isLedge = ly > 0.94f;
            Color c;
            float r;
            var e = new Color(0, 0, 0);
            if (isWin)
            {
                var g = 0.12f + 0.12f * ly;
                c = new Color(g * 0.8f, g * 0.95f, g * 1.15f);
                r = 0.08f;
                if (night && lit[cell]) e = new Color(1f, 0.78f, 0.45f) * (0.6f + 0.4f * ly);
            }
            else if (isFrame)
            {
                c = new Color(0.2f, 0.2f, 0.21f);
                r = 0.4f;
            }
            else
            {
                c = (isLedge ? wall.Darkened(0.2f) : wall) + new Color(n, n, n);
                r = 0.9f;
            }
            img.SetPixel(x, y, c);
            emit.SetPixel(x, y, e);
            rough.SetPixel(x, y, new Color(r, r, r));
        }
        img.GenerateMipmaps();
        emit.GenerateMipmaps();
        rough.GenerateMipmaps();
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(img),
            RoughnessTexture = ImageTexture.CreateFromImage(rough),
            Roughness = 1f,
            Metallic = 0.1f,
            MetallicSpecular = 0.6f,
            NormalEnabled = true,
            NormalTexture = Noise(variant + 90, 0.08f, true, 256, 2f),
            NormalScale = 0.3f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        if (night)
        {
            mat.EmissionEnabled = true;
            mat.EmissionTexture = ImageTexture.CreateFromImage(emit);
            mat.EmissionEnergyMultiplier = 1.4f;
        }
        Cache[key] = mat;
        return mat;
    }

    public static StandardMaterial3D Roof()
    {
        if (Cache.TryGetValue("roof", out var m)) return (StandardMaterial3D)m;
        var mat = Concrete(new Color(0.32f, 0.31f, 0.3f), 81);
        Cache["roof"] = mat;
        return mat;
    }

    public static StandardMaterial3D Painted(Color c, float rough = 0.5f, float metal = 0f)
    {
        var key = $"paint{c}{rough}{metal}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D { AlbedoColor = c, Roughness = rough, Metallic = metal };
        Cache[key] = mat;
        return mat;
    }

    public static StandardMaterial3D Glow(Color c, float energy = 3f)
    {
        var key = $"glow{c}{energy}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = c,
            EmissionEnabled = true,
            Emission = c,
            EmissionEnergyMultiplier = energy,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        Cache[key] = mat;
        return mat;
    }

    /// <summary>Chitine d'insecte : brillante, légèrement irisée, avec bosses.</summary>
    public static StandardMaterial3D Chitin(Color c)
    {
        var key = $"chitin{c}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(101, 0.06f, c.Darkened(0.35f), c.Lightened(0.08f), 256),
            NormalEnabled = true,
            NormalTexture = Noise(102, 0.12f, true, 256, 6f),
            NormalScale = 0.8f,
            Roughness = 0.28f,
            Metallic = 0.15f,
            ClearcoatEnabled = true,
            Clearcoat = 0.6f,
            ClearcoatRoughness = 0.2f,
            RimEnabled = true,
            Rim = 0.25f,
            RimTint = 0.6f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.6f, 0.6f, 0.6f),
        };
        Cache[key] = mat;
        return mat;
    }

    public static StandardMaterial3D Fabric(Color c)
    {
        var key = $"fabric{c}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(111, 0.2f, c.Darkened(0.15f), c.Lightened(0.05f), 128),
            NormalEnabled = true,
            NormalTexture = Noise(112, 0.6f, true, 128, 2f),
            Roughness = 0.88f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(2f, 2f, 2f),
        };
        Cache[key] = mat;
        return mat;
    }

    public static StandardMaterial3D Metal(Color c, float rough = 0.35f)
    {
        var key = $"metal{c}{rough}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = c,
            Metallic = 0.85f,
            Roughness = rough,
            NormalEnabled = true,
            NormalTexture = Noise(121, 0.4f, true, 128, 1.5f),
            NormalScale = 0.3f,
            Uv1Triplanar = true,
        };
        Cache[key] = mat;
        return mat;
    }

    public static ShaderMaterial Water(TimeOfDay time)
    {
        var key = $"water{time}";
        if (Cache.TryGetValue(key, out var m)) return (ShaderMaterial)m;
        var shader = new Shader
        {
            Code = @"
shader_type spatial;
render_mode cull_disabled, depth_draw_always;
uniform vec3 deep : source_color = vec3(0.02, 0.09, 0.13);
uniform vec3 shallow : source_color = vec3(0.07, 0.25, 0.28);
uniform sampler2D n1 : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D n2 : hint_normal, filter_linear_mipmap, repeat_enable;
varying vec3 wpos;
void vertex() {
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    float h = sin(wpos.x * 0.05 + TIME * 0.8) * 0.35 + sin(wpos.z * 0.07 + TIME * 1.1) * 0.25;
    VERTEX.y += h;
}
void fragment() {
    vec2 uv = wpos.xz * 0.02;
    vec3 a = texture(n1, uv + vec2(TIME * 0.01, TIME * 0.006)).rgb;
    vec3 b = texture(n2, uv * 1.7 - vec2(TIME * 0.008, TIME * 0.012)).rgb;
    NORMAL_MAP = normalize(mix(a, b, 0.5));
    NORMAL_MAP_DEPTH = 0.6;
    float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
    ALBEDO = mix(deep, shallow, 0.35 + 0.3 * fres);
    ROUGHNESS = 0.06;
    METALLIC = 0.0;
    SPECULAR = 0.7;
}"
        };
        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("n1", Noise(131, 0.02f, true, 512, 6f));
        mat.SetShaderParameter("n2", Noise(132, 0.03f, true, 512, 4f));
        if (time == TimeOfDay.Night)
        {
            mat.SetShaderParameter("deep", new Color(0.005f, 0.02f, 0.035f));
            mat.SetShaderParameter("shallow", new Color(0.02f, 0.06f, 0.08f));
        }
        Cache[key] = mat;
        return mat;
    }

    public static void ClearNightVariants()
    {
        // Les façades nuit/jour sont mises en cache séparément : rien à faire pour l'instant.
    }
}
