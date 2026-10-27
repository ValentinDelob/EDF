using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Surface des armes (modèles Blender) : la texture de détail cuite de chaque modèle (&lt;nom&gt;_mask.jpg : R occlusion, G arêtes,
/// B salissure, tools/blender/weapons/kit.py) pilote l'usure — peinture écaillée sur les arêtes laissant voir l'acier, arêtes
/// d'acier polies et brillantes, polymère grenu, creux sales et sombres, marquages usés — plus un grain procédural fin en
/// coordonnées d'objet (aucune texture répétée visible).
/// </summary>
public static partial class Mats
{
    public enum GunKind { Metal = 0, Polymer = 1, Paint = 2, Alloy = 3, Rubber = 4, Marking = 5 }

    private static Shader _gunShader;
    private static readonly Dictionary<string, ShaderMaterial> GunSurfaces = new();

    private const string GunShaderCode = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec3 base_color : source_color = vec3(0.1);
uniform vec3 under_color : source_color = vec3(0.12, 0.12, 0.13);
uniform float rough_base = 0.6;
uniform float metal_base = 0.0;
uniform int kind = 0;
uniform sampler2D mask : hint_default_white, filter_linear_mipmap, repeat_disable;
uniform float has_mask = 0.0;
uniform float wear = 1.0;

varying vec3 opos;
varying vec3 onrm;

void vertex() {
    opos = VERTEX;
    onrm = NORMAL;
}

float hash(vec3 p) {
    p = fract(p * 0.3183099 + vec3(0.1, 0.2, 0.3));
    p *= 17.0;
    return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float vnoise(vec3 x) {
    vec3 i = floor(x);
    vec3 f = fract(x);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix(hash(i), hash(i + vec3(1, 0, 0)), f.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
               mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}

float fbm(vec3 p) {
    float v = 0.0;
    float a = 0.5;
    for (int k = 0; k < 4; k++) { v += a * vnoise(p); p *= 2.03; a *= 0.5; }
    return v;
}

void fragment() {
    vec3 m = has_mask > 0.5 ? texture(mask, UV).rgb : vec3(1.0, 0.0, 0.0);
    float ao = mix(0.32, 1.0, m.r);
    float edge = smoothstep(0.35, 0.95, m.g) * wear;
    float grime = m.b;
    float big = fbm(opos * 22.0);
    float fine = fbm(opos * 160.0);
    vec3 col = base_color;
    float rough = rough_base;
    float met = metal_base;
    if (kind == 2) {
        // Peinture : légère variation de teinte, écaillage sur les arêtes (acier dessous), éraflures.
        col *= 0.9 + 0.2 * big;
        float chip = smoothstep(0.55, 0.62, edge * 0.85 + big * 0.45 + fine * 0.15);
        col = mix(col, under_color * (0.8 + 0.6 * fine), chip);
        rough = mix(rough + (fine - 0.5) * 0.12, 0.4, chip);
        met = mix(met, 0.85, chip);
    } else if (kind == 0 || kind == 3) {
        // Acier / alliage : grain, arêtes polies et plus claires.
        col *= 0.82 + 0.36 * fine;
        col = mix(col, col * 2.4 + vec3(0.07), edge * 0.85);
        rough = clamp(mix(rough + (big - 0.5) * 0.25, rough * 0.45, edge), 0.08, 1.0);
    } else if (kind == 1 || kind == 4) {
        // Polymère / caoutchouc : grain fin, arêtes blanchies par l'usure.
        col *= 0.88 + 0.24 * fine;
        col = mix(col, col * 1.8 + vec3(0.035), edge * 0.45);
        rough = clamp(rough + (fine - 0.5) * 0.18, 0.3, 1.0);
    } else if (kind == 5) {
        // Marquage au pochoir : usé par endroits (on voit le support à travers).
        float worn = smoothstep(0.45, 0.7, big * 0.7 + fine * 0.5 + edge * 0.5);
        col = mix(col, col * 0.35, worn);
        rough = 0.7;
    }
    // Salissure dans les creux.
    col = mix(col, col * 0.5 + vec3(0.03, 0.026, 0.02), clamp(grime * 1.4, 0.0, 0.8));
    rough = mix(rough, 0.95, clamp(grime * 1.2, 0.0, 0.7));
    ALBEDO = col * ao;
    ROUGHNESS = rough;
    METALLIC = met;
    AO = ao;
    AO_LIGHT_AFFECT = 0.4;
}
";

    /// <summary>Matière d'arme d'un type (acier, polymère, peinture…) et d'une couleur, pilotée par la texture de détail du modèle.</summary>
    public static ShaderMaterial GunSurface(GunKind kind, Color color, Texture2D mask)
    {
        var key = $"{kind}{color}{mask?.ResourcePath}";
        if (GunSurfaces.TryGetValue(key, out var m)) return m;
        _gunShader ??= new Shader { Code = GunShaderCode };
        m = new ShaderMaterial { Shader = _gunShader };
        var (rough, metal) = kind switch
        {
            GunKind.Metal => (0.5f, 0.85f),
            GunKind.Alloy => (0.35f, 0.9f),
            GunKind.Paint => (0.62f, 0.1f),
            GunKind.Rubber => (0.92f, 0f),
            GunKind.Marking => (0.7f, 0f),
            _ => (0.78f, 0f),
        };
        m.SetShaderParameter("base_color", new Vector3(color.R, color.G, color.B));
        m.SetShaderParameter("under_color", new Vector3(0.13f, 0.13f, 0.14f));
        m.SetShaderParameter("rough_base", rough);
        m.SetShaderParameter("metal_base", metal);
        m.SetShaderParameter("kind", (int)kind);
        m.SetShaderParameter("has_mask", mask != null ? 1f : 0f);
        if (mask != null) m.SetShaderParameter("mask", mask);
        GunSurfaces[key] = m;
        return m;
    }

    /// <summary>Texture de détail cuite d'un modèle (« …/nom.glb » → « …/nom_mask.jpg »), ou null.</summary>
    public static Texture2D GunMask(string glbPath)
    {
        var path = glbPath[..^4] + "_mask.jpg";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }
}
