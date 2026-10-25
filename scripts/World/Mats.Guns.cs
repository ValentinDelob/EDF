using Godot;

namespace Rempart;

/// <summary>
/// Matières des armes tenues (modèles Blender des armes d'EDF 5) : grain fin à l'échelle d'une arme (projection triplanaire
/// serrée), acier phosphaté aux reflets usés, polymère grenu, peinture mate, verres traités. Les noms correspondent aux
/// matériaux des .glb (tools/blender/weapons/kit.py).
/// </summary>
public static partial class Mats
{
    /// <summary>Acier phosphaté : gris anthracite, rugosité marbrée (usure des arêtes et des zones de prise).</summary>
    public static StandardMaterial3D GunSteel()
    {
        if (TryCache<StandardMaterial3D>("gunsteel", out var c)) return c;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(301, 0.9f, new Color(0.055f, 0.056f, 0.06f), new Color(0.11f, 0.11f, 0.115f), 256),
            Metallic = 0.75f,
            RoughnessTexture = Noise(302, 1.6f, false, 256),
            Roughness = 0.62f,
            NormalEnabled = true,
            NormalTexture = Noise(303, 2.4f, true, 256, 1.2f),
            NormalScale = 0.25f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(14f, 14f, 14f),
        };
        Cache["gunsteel"] = mat;
        return mat;
    }

    /// <summary>Polymère noir grenu (poignées, garde-mains, crosses) : mat, texture granuleuse fine.</summary>
    public static StandardMaterial3D GunPolymer(Color c)
    {
        var key = $"gunpoly{c}";
        if (TryCache<StandardMaterial3D>(key, out var m)) return m;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(311, 1.2f, c.Darkened(0.12f), c.Lightened(0.06f), 256),
            Roughness = 0.82f,
            NormalEnabled = true,
            NormalTexture = Noise(312, 6f, true, 256, 2.5f, FastNoiseLite.NoiseTypeEnum.Cellular, 2),
            NormalScale = 0.45f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(18f, 18f, 18f),
        };
        Cache[key] = mat;
        return mat;
    }

    /// <summary>Peinture de l'arme (couleur de la famille) : mate, légèrement usée et salie.</summary>
    public static StandardMaterial3D GunPaint(Color c)
    {
        var key = $"gunpaint{c}";
        if (TryCache<StandardMaterial3D>(key, out var m)) return m;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(321, 0.7f, c.Darkened(0.18f), c.Lightened(0.08f), 256),
            Roughness = 0.6f,
            Metallic = 0.12f,
            RoughnessTexture = Noise(322, 1.4f, false, 256),
            NormalEnabled = true,
            NormalTexture = Noise(323, 1.8f, true, 256, 1f),
            NormalScale = 0.2f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(10f, 10f, 10f),
        };
        Cache[key] = mat;
        return mat;
    }

    /// <summary>Verre traité des optiques : sombre, très réfléchissant, reflet bleuté.</summary>
    public static StandardMaterial3D GunLens()
    {
        if (TryCache<StandardMaterial3D>("gunlens", out var c)) return c;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.05f, 0.12f, 0.16f),
            Metallic = 0.6f,
            Roughness = 0.04f,
            ClearcoatEnabled = true,
            Clearcoat = 1f,
            RimEnabled = true,
            Rim = 0.5f,
            RimTint = 0.8f,
        };
        Cache["gunlens"] = mat;
        return mat;
    }
}
