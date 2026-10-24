using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Explosions semi-réalistes : éclair, boule de feu bouillonnante qui se change en fumée noire, panache qui dérive,
/// onde de choc (anneau de poussière et distorsion de l'air), terre projetée, braises et éclats, trace de brûlure.
///
/// Tout passe par quelques émetteurs de particules permanents (un appel de rendu par famille, quel que soit le nombre
/// d'explosions) alimentés à la main par <see cref="GpuParticles3D.EmitParticle"/> : aucun nœud n'est créé par
/// explosion. Lumières, distorsions et traces au sol viennent de petites réserves recyclées. Le niveau de détail
/// dépend de la distance à la caméra et de la cadence des explosions (salves de roquettes).
/// Effets purement visuels : appelés sur chaque pair, aucune incidence réseau.
/// </summary>
public partial class ExplosionFx : Node3D
{
    /// <summary>Variante d'explosion (le « kind » réseau) : 0 explosif, 2 plasma, 4 acide.</summary>
    public const int Explosive = 0, Plasma = 2, Acid = 4;

    // Familles de particules « nuage » (champ CUSTOM.w = durée de vie + 64 × type, lu par les shaders).
    private const int TFire = 0, TSmoke = 1, TDust = 2, TDirt = 3, TEnergy = 4, TGas = 5, TSpark = 6, TChunk = 7, TFlash = 8;

    private const uint EmitAll = (uint)(GpuParticles3D.EmitFlags.Position | GpuParticles3D.EmitFlags.RotationScale |
        GpuParticles3D.EmitFlags.Velocity | GpuParticles3D.EmitFlags.Color | GpuParticles3D.EmitFlags.Custom);

    private const int MaxLights = 6, MaxHaze = 4, MaxDecals = 28;
    private const float DecalLife = 22f;

    private static ExplosionFx _current;
    private static readonly string ProfileSkip = Screenshot.CmdArg("boom-skip");
    private static Texture2D _noise, _scorch;
    private static Shader _processShader, _billowShader, _sparkShader, _flashShader, _hazeShader;
    private static GradientTexture1D _fireRamp;

    private World _world;
    private GpuParticles3D _billows, _sparks, _chunks, _flashes;
    private ShaderMaterial _billowMat;
    private float _sceneT;
    private readonly RandomNumberGenerator _rng = new();

    private sealed class FxLight { public OmniLight3D L; public float Age = 99f, Peak, Life; }
    private sealed class FxHaze { public MeshInstance3D M; public float Age = 99f, Life; }
    private sealed class FxDecal { public Decal D; public float Age = 999f; public float Alpha; }
    private readonly List<FxLight> _lights = new();
    private readonly List<FxHaze> _haze = new();
    private readonly List<FxDecal> _decals = new();
    private int _nextDecal;

    /// <summary>Charge récente (explosions par seconde, lissée) : au-delà de quelques-unes, chaque explosion s'allège.</summary>
    private float _load, _busy = 1f;
    private int _total;

    public static string Stats => _current != null && IsInstanceValid(_current)
        ? $"charge {_current._load:0.0}/s, total {_current._total}, lumières {_current.ActiveLights()}/{MaxLights}, traces {_current.ActiveDecals()}/{MaxDecals}"
        : "inactif";

    public static ExplosionFx For(World world)
    {
        if (_current != null && IsInstanceValid(_current) && _current._world == world) return _current;
        _current = new ExplosionFx { _world = world, Name = "ExplosionFx" };
        world.FxRoot.AddChild(_current);
        return _current;
    }

    // ------------------------------------------------------------------ Ressources partagées (créées une fois)

    /// <summary>Bruit fractal répétable sur deux canaux (R : volutes larges, G : détail plus fin et « gonflé »).</summary>
    private static Texture2D Noise
    {
        get
        {
            if (_noise != null) return _noise;
            const int n = 256;
            var a = new FastNoiseLite { Seed = 11, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.012f, FractalType = FastNoiseLite.FractalTypeEnum.Fbm, FractalOctaves = 5 };
            var b = new FastNoiseLite { Seed = 23, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.028f, FractalType = FastNoiseLite.FractalTypeEnum.Ridged, FractalOctaves = 4 };
            var ia = a.GetSeamlessImage(n, n, false, false, 0.1f, true);
            var ib = b.GetSeamlessImage(n, n, false, false, 0.1f, true);
            ia.Convert(Image.Format.L8);
            ib.Convert(Image.Format.L8);
            var da = ia.GetData();
            var db = ib.GetData();
            var rgba = new byte[n * n * 4];
            // B, A : pente de R (différences centrées, bouclées) pour éclairer le relief sans lecture supplémentaire.
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var i = y * n + x;
                var dx = da[y * n + (x + 1) % n] - da[y * n + (x + n - 1) % n];
                var dy = da[(y + 1) % n * n + x] - da[(y + n - 1) % n * n + x];
                rgba[i * 4] = da[i];
                rgba[i * 4 + 1] = db[i];
                rgba[i * 4 + 2] = (byte)Mathf.Clamp(128 + dx * 4, 0, 255);
                rgba[i * 4 + 3] = (byte)Mathf.Clamp(128 + dy * 4, 0, 255);
            }
            var img = Image.CreateFromData(n, n, false, Image.Format.Rgba8, rgba);
            img.GenerateMipmaps();
            return _noise = ImageTexture.CreateFromImage(img);
        }
    }

    /// <summary>Trace de brûlure : cœur de suie, bord déchiqueté, éclaboussures radiales.</summary>
    private static Texture2D Scorch
    {
        get
        {
            if (_scorch != null) return _scorch;
            const int n = 256;
            var noise = new FastNoiseLite { Seed = 5, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.035f, FractalOctaves = 4 };
            var rays = new FastNoiseLite { Seed = 9, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 1f, FractalOctaves = 2 };
            var data = new byte[n * n * 4];
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var dx = (x + 0.5f) / n * 2f - 1f;
                var dy = (y + 0.5f) / n * 2f - 1f;
                var r = Mathf.Sqrt(dx * dx + dy * dy);
                var ang = Mathf.Atan2(dy, dx);
                var edge = noise.GetNoise2D(x, y) * 0.22f;
                var core = 1f - Mathf.SmoothStep(0.25f, 0.62f, r + edge);
                // Rayons de projection : fines traînées au-delà du cratère.
                var ray = Mathf.Clamp(rays.GetNoise2D(Mathf.Cos(ang) * 6f + 10f, Mathf.Sin(ang) * 6f + 10f) * 2.2f - 0.25f, 0f, 1f);
                var streak = ray * (1f - Mathf.SmoothStep(0.45f, 0.98f, r + edge * 0.5f)) * Mathf.SmoothStep(0.2f, 0.45f, r);
                var speck = Mathf.Clamp(noise.GetNoise2D(x * 3.1f + 400, y * 3.1f) * 3f - 1.2f, 0f, 1f) * (1f - Mathf.SmoothStep(0.5f, 0.95f, r));
                var a = Mathf.Clamp(core * 0.95f + streak * 0.7f + speck * 0.5f, 0f, 1f);
                // Suie noire au centre, brun-gris (terre retournée) vers le bord.
                var t = Mathf.Clamp(r * 1.4f, 0f, 1f);
                var i = (y * n + x) * 4;
                data[i] = (byte)(Mathf.Lerp(10f, 46f, t));
                data[i + 1] = (byte)(Mathf.Lerp(9f, 40f, t));
                data[i + 2] = (byte)(Mathf.Lerp(8f, 33f, t));
                data[i + 3] = (byte)(a * 255f);
            }
            var img = Image.CreateFromData(n, n, false, Image.Format.Rgba8, data);
            img.GenerateMipmaps();
            return _scorch = ImageTexture.CreateFromImage(img);
        }
    }

    /// <summary>Rampe de température (corps noir) : rouge sombre → orange → jaune → blanc.</summary>
    private static GradientTexture1D FireRamp => _fireRamp ??= new GradientTexture1D
    {
        Width = 128,
        Gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.18f, 0.42f, 0.68f, 0.86f, 1f },
            Colors = new[]
            {
                new Color(0f, 0f, 0f), new Color(0.3f, 0.03f, 0.005f), new Color(0.95f, 0.2f, 0.02f),
                new Color(1f, 0.34f, 0.03f), new Color(1f, 0.5f, 0.1f), new Color(1f, 0.66f, 0.28f),
            },
        },
    };

    // Simulation commune à toutes les familles (vitesse, traînée, poussée d'Archimède, gravité, rebonds, taille).
    private const string ProcessCode = @"
shader_type particles;
render_mode disable_force;
uniform vec3 wind = vec3(1.1, 0.0, 0.5);

mat3 axis_angle(vec3 a, float t) {
    float c = cos(t), s = sin(t), k = 1.0 - c;
    return mat3(vec3(c + a.x * a.x * k, a.y * a.x * k + a.z * s, a.z * a.x * k - a.y * s),
                vec3(a.x * a.y * k - a.z * s, c + a.y * a.y * k, a.z * a.y * k + a.x * s),
                vec3(a.x * a.z * k + a.y * s, a.y * a.z * k - a.x * s, c + a.z * a.z * k));
}

void start() {
    // Seules les particules émises depuis le code existent : un redémarrage automatique est éteint.
    if (RESTART_CUSTOM) { ACTIVE = false; }
}

void process() {
    int type = int(CUSTOM.w / 64.0);
    float life = max(CUSTOM.w - float(type) * 64.0, 0.01);
    CUSTOM.y += DELTA;
    float t = CUSTOM.y / life;
    if (t >= 1.0) { ACTIVE = false; }
    float size = CUSTOM.z;
    vec3 v = VELOCITY;
    float s = size;
    if (type == 0) {
        // Boule de feu : expansion violente freinée net, puis le gaz chaud monte.
        v.y += 5.5 * (1.0 - t) * DELTA;
        v = mix(wind * 0.3, v, exp(-4.5 * DELTA));
        s = size * mix(0.5, 1.5, 1.0 - pow(1.0 - min(t * 1.6, 1.0), 3.0));
    } else if (type == 1) {
        // Fumée : monte en ralentissant, dérive avec le vent, gonfle longtemps.
        v.y += 2.4 * (1.0 - t) * (1.0 - t) * DELTA;
        v = mix(wind, v, exp(-0.9 * DELTA));
        s = size * mix(0.55, 1.8, 1.0 - pow(1.0 - t, 2.2));
    } else if (type == 2) {
        // Poussière au sol : anneau projeté par l'onde de choc, s'arrête vite et traîne.
        v.y += 0.25 * DELTA;
        v = mix(wind * 0.5, v, exp(-3.2 * DELTA));
        s = size * mix(0.5, 2.2, 1.0 - pow(1.0 - t, 2.0));
    } else if (type == 3) {
        // Gerbe de terre : balistique, freinée par l'air, se disperse en poussière.
        v.y -= 9.0 * DELTA;
        v = mix(wind * 0.4, v, exp(-0.9 * DELTA));
        s = size * mix(0.6, 2.2, t);
        if (TRANSFORM[3].y < 0.3 && v.y < 0.0) { v.y = 0.0; v.xz *= 0.7; }
    } else if (type == 4) {
        // Plasma : sphère d'énergie qui se dilate puis s'éteint.
        v = mix(vec3(0.0, 0.6, 0.0), v, exp(-6.0 * DELTA));
        s = size * mix(0.5, 1.7, 1.0 - pow(1.0 - t, 3.0));
    } else if (type == 5) {
        // Gaz acide : lourd, rampe au sol, s'étale lentement.
        v.y -= 0.2 * DELTA;
        v = mix(wind * 0.35, v, exp(-1.8 * DELTA));
        s = size * mix(0.6, 2.4, 1.0 - pow(1.0 - t, 2.0));
        if (TRANSFORM[3].y < size * 0.35 && v.y < 0.0) v.y = 0.0;
    } else if (type == 6) {
        // Braise / étincelle : gravité, faible traînée, rebond.
        v.y -= 9.8 * DELTA;
        v *= exp(-0.55 * DELTA);
        if (TRANSFORM[3].y < 0.05 && v.y < 0.0) { v.y *= -0.3; v.xz *= 0.45; }
    } else if (type == 7) {
        // Éclat : chute, rebonds amortis, culbute.
        v.y -= 11.0 * DELTA;
        v *= exp(-0.15 * DELTA);
        if (TRANSFORM[3].y < size * 0.5 && v.y < 0.0) { TRANSFORM[3].y = size * 0.5; v.y *= -0.32; v.xz *= 0.55; }
    }
    // Les nuages ne s'enfoncent pas dans le sol.
    if (type <= 2 && v.y < 0.0 && TRANSFORM[3].y < s * 0.45) v.y *= 0.2;
    // Le bas des nuages ne doit pas être tranché net par le sol : leur centre remonte à mi-taille.
    if ((type <= 2 || type == 5) && TRANSFORM[3].y < s * 0.45) TRANSFORM[3].y += (s * 0.45 - TRANSFORM[3].y) * min(DELTA * 4.0, 1.0);
    VELOCITY = v;
    if (type == 6) {
        // Axe Y = vitesse (traînée lumineuse), largeur dans X.
        float w = size * (1.0 - t * 0.6);
        vec3 tail = v * 0.03;
        if (length(tail) > 0.7) tail = normalize(tail) * 0.7;
        if (length(tail) < w * 2.0) tail = normalize(v + vec3(0.0, 1e-3, 0.0)) * w * 2.0;
        TRANSFORM[0].xyz = vec3(w, 0.0, 0.0);
        TRANSFORM[1].xyz = tail;
        TRANSFORM[2].xyz = vec3(0.0, 0.0, w);
    } else if (type == 7) {
        // Culbute : rotation incrémentale de la base (l'échelle non uniforme de l'émission est conservée).
        float h = fract(CUSTOM.x * 43.17);
        vec3 axis = normalize(vec3(h - 0.5, fract(h * 7.3) - 0.5, fract(h * 3.1) + 0.1));
        float rate = (h * 14.0 + 5.0) * clamp(length(v) * 0.2, 0.0, 1.0);
        mat3 b = mat3(TRANSFORM[0].xyz, TRANSFORM[1].xyz, TRANSFORM[2].xyz);
        b = axis_angle(axis, rate * DELTA) * b;
        TRANSFORM[0].xyz = b[0];
        TRANSFORM[1].xyz = b[1];
        TRANSFORM[2].xyz = b[2];
    } else {
        if (type == 8) s = size * (0.6 + t * 0.8);
        TRANSFORM[0].xyz = vec3(s, 0.0, 0.0);
        TRANSFORM[1].xyz = vec3(0.0, type == 2 ? s * 0.6 : s, 0.0); // poussière : nuages aplatis
        TRANSFORM[2].xyz = vec3(0.0, 0.0, s);
    }
}";

    // Nuages (feu, fumée, poussière, plasma, gaz) : billboard bouillonnant éclairé par la scène, transition chaud → suie,
    // fondu doux contre le décor et près de la caméra.
    private const string BillowCode = @"
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, shadows_disabled, skip_vertex_transform;
uniform sampler2D noise_tex : filter_linear_mipmap, repeat_enable;
uniform sampler2D fire_ramp : filter_linear, repeat_disable;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform float fire_energy = 2.2;
uniform vec3 sun_dir = vec3(0.3, 0.8, 0.4); // vers le soleil (monde), mis à jour depuis la scène
uniform vec3 sun_col = vec3(0.9);
uniform vec3 amb_col = vec3(0.4);
varying float v_age;
varying float v_t;
varying float v_seed;
varying float v_size;
varying float v_rot;
varying flat int v_type;

void vertex() {
    int type = int(INSTANCE_CUSTOM.w / 64.0);
    float life = max(INSTANCE_CUSTOM.w - float(type) * 64.0, 0.01);
    v_type = type;
    v_age = INSTANCE_CUSTOM.y;
    v_t = clamp(v_age / life, 0.0, 1.0);
    v_seed = INSTANCE_CUSTOM.x;
    v_size = length(MODEL_MATRIX[0].xyz);
    v_rot = v_seed * 6.2832 + (fract(v_seed * 13.7) - 0.5) * v_age * 0.5;
    float c = cos(v_rot), s = sin(v_rot);
    vec2 q = vec2(VERTEX.x * c - VERTEX.y * s, VERTEX.x * s + VERTEX.y * c) * v_size;
    q.y *= length(MODEL_MATRIX[1].xyz) / max(v_size, 1e-4);
    vec3 center = (VIEW_MATRIX * vec4(MODEL_MATRIX[3].xyz, 1.0)).xyz;
    // Léger recul vers l'arrière : le nuage reste en volume autour de son centre.
    VERTEX = center + vec3(q, 0.0) + normalize(center) * v_size * 0.15;
    NORMAL = vec3(0.0, 0.0, 1.0);
}

void fragment() {
    vec2 c = UV * 2.0 - 1.0;
    float r = length(c);
    if (r > 1.0) discard;
    vec2 base = UV * 0.42 + vec2(v_seed * 7.31, fract(v_seed * 3.17) * 5.0);
    float boil = v_age * (v_type == 0 ? 0.11 : 0.035);
    vec2 uv1 = base + vec2(0.0, -boil);
    vec4 t1 = texture(noise_tex, uv1);
    float n1 = t1.r;
    vec2 uv2 = base * 1.9 + vec2(boil * 1.3, 0.0) + n1 * 0.12;
    float n2 = texture(noise_tex, uv2).g;
    float n = n1 * 0.62 + n2 * 0.38;
    float sphere = 1.0 - r * r;
    float dens = sphere * (v_type == 0 ? 1.3 : 1.05) + (n - 0.5) * (v_type == 0 ? 1.5 : 1.9);
    float erode = mix(0.12, 0.95, pow(v_t, v_type == 0 ? 1.2 : 1.6));
    float fadein = smoothstep(0.0, v_type == 1 || v_type == 5 ? 0.6 : 0.04, v_age);
    float a = smoothstep(erode, erode + 0.32, dens) * COLOR.a * fadein;
    // Rien à dessiner : on s'arrête avant l'éclairage et la lecture de profondeur (beaucoup de pixels en salve).
    if (a < 0.004) discard;
    // Normale (espace vue) : bombé de sphère + relief des volutes (pente précalculée dans B et A), tournée comme le billboard.
    vec2 g = (t1.ba - 0.5) * vec2(8.4, -8.4);
    vec3 nl = normalize(vec3(c.x * 0.9 - g.x, -c.y * 0.9 - g.y, 0.55));
    float cr = cos(v_rot), sr = sin(v_rot);
    vec3 nv = normalize(vec3(nl.x * cr - nl.y * sr, nl.x * sr + nl.y * cr, nl.z));
    // Éclairage maison (soleil enveloppant + ambiance de la scène) : bien moins coûteux que l'éclairage complet
    // sur des couches de fumée superposées.
    vec3 l = normalize((VIEW_MATRIX * vec4(sun_dir, 0.0)).xyz);
    float wrap = clamp((dot(nv, l) + 0.6) / 1.6, 0.0, 1.0);
    vec3 light = amb_col * 0.7 + sun_col * wrap * 0.55;
    vec3 tint = COLOR.rgb;
    float shade = 0.75 + 0.25 * n;
    vec3 col = tint * shade * light;
    if (v_type == 0) {
        // Température : cœur jaune-orangé, bords qui refroidissent d'abord, tout s'éteint en suie noire.
        float heat = clamp(1.2 - v_t * 3.8, 0.0, 1.2) + (n - 0.5) * 1.35 + sphere * 0.3 - 0.25;
        heat = clamp(heat, 0.0, 1.0);
        vec3 fire = texture(fire_ramp, vec2(heat, 0.5)).rgb;
        col = mix(col, vec3(0.0), heat) + fire * heat * heat * fire_energy * (1.0 + 0.8 * (1.0 - smoothstep(0.0, 0.06, v_age)));
        a = min(a * mix(1.0, 1.25, heat), 1.0);
    } else if (v_type == 4) {
        float heat = clamp(1.0 - v_t * 1.6 + (n - 0.5) * 0.6 + sphere * 0.3 - 0.15, 0.0, 1.0);
        col = vec3(0.05) * light + mix(tint, vec3(0.8, 0.9, 1.0), heat * heat * 0.35) * heat * heat * fire_energy * 0.65;
        a *= mix(0.35, 1.0, heat);
    } else if (v_type == 5) {
        col += tint * 0.12 * (1.0 - v_t);
    }
    if (v_type <= 1) {
        // Lueur du feu sur le dessous de la fumée pendant la première seconde.
        col += vec3(1.0, 0.36, 0.08) * 0.3 * max(0.0, 1.0 - v_age * 1.1) * clamp(-nl.y, 0.0, 1.0) * (v_type == 1 ? 1.0 : 0.5);
    }
    ALBEDO = col;
    // Fondu doux contre le décor (pas de coupe nette au sol) et près de la caméra.
    float depth = textureLod(depth_tex, SCREEN_UV, 0.0).r;
    vec4 up = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
    float scene_z = up.z / up.w;
    float soft = clamp((VERTEX.z - scene_z) / clamp(v_size * 0.45, 0.3, 5.0), 0.0, 1.0);
    float near = smoothstep(0.6, 2.5, -VERTEX.z);
    ALPHA = clamp(a, 0.0, 1.0) * soft * near;
}";

    // Braises et étincelles : traînée alignée sur la vitesse, additive, qui refroidit du jaune au rouge.
    private const string SparkCode = @"
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, skip_vertex_transform, fog_disabled;
varying float v_t;
varying float v_near;
void vertex() {
    int type = int(INSTANCE_CUSTOM.w / 64.0);
    float life = max(INSTANCE_CUSTOM.w - float(type) * 64.0, 0.01);
    v_t = clamp(INSTANCE_CUSTOM.y / life, 0.0, 1.0);
    vec3 pos = MODEL_MATRIX[3].xyz;
    vec3 axis = MODEL_MATRIX[1].xyz;
    float w = length(MODEL_MATRIX[0].xyz);
    float len = max(length(axis), 1e-4);
    vec3 dir = axis / len;
    vec3 to_cam = normalize(CAMERA_POSITION_WORLD - pos);
    v_near = smoothstep(2.0, 7.0, distance(CAMERA_POSITION_WORLD, pos));
    vec3 side = cross(dir, to_cam);
    side = length(side) < 1e-3 ? vec3(w, 0.0, 0.0) : normalize(side) * w;
    vec3 wp = pos + side * VERTEX.x * 2.0 + axis * (VERTEX.y - 0.5);
    VERTEX = (VIEW_MATRIX * vec4(wp, 1.0)).xyz;
    NORMAL = vec3(0.0, 0.0, 1.0);
}
void fragment() {
    float x = 1.0 - abs(UV.x * 2.0 - 1.0);
    float along = 1.0 - UV.y;
    float shape = x * x * along;
    float cool = v_t;
    vec3 hot = mix(COLOR.rgb, vec3(1.0, 0.95, 0.8), clamp(0.6 - cool, 0.0, 0.6));
    vec3 col = mix(hot, COLOR.rgb * vec3(1.0, 0.35, 0.15), smoothstep(0.3, 1.0, cool));
    ALBEDO = col * shape * COLOR.a * (1.0 - cool * cool) * 5.0 * v_near;
}";

    // Éclair initial : halo additif très lumineux, quelques images seulement.
    private const string FlashCode = @"
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled, skip_vertex_transform, fog_disabled;
varying float v_t;
varying float v_size;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
void vertex() {
    int type = int(INSTANCE_CUSTOM.w / 64.0);
    float life = max(INSTANCE_CUSTOM.w - float(type) * 64.0, 0.01);
    v_t = clamp(INSTANCE_CUSTOM.y / life, 0.0, 1.0);
    v_size = length(MODEL_MATRIX[0].xyz);
    vec3 center = (VIEW_MATRIX * vec4(MODEL_MATRIX[3].xyz, 1.0)).xyz;
    VERTEX = center + vec3(VERTEX.xy * v_size, 0.0) - normalize(center) * v_size * 0.3;
}
void fragment() {
    float r = length(UV * 2.0 - 1.0);
    float core = pow(clamp(1.0 - r, 0.0, 1.0), 3.0);
    float halo = pow(clamp(1.0 - r, 0.0, 1.0), 1.5) * 0.25;
    float depth = textureLod(depth_tex, SCREEN_UV, 0.0).r;
    vec4 up = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
    float soft = clamp((VERTEX.z - up.z / up.w) / max(v_size * 0.3, 0.3), 0.0, 1.0);
    float k = (1.0 - v_t) * (1.0 - v_t);
    ALBEDO = COLOR.rgb * (core * 4.0 + halo) * k * COLOR.a * soft;
}";

    // Onde de choc : anneau de distorsion de l'air (réfraction de l'image) qui s'élargit en un instant.
    private const string HazeCode = @"
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
instance uniform float progress = 0.0;
instance uniform float strength = 0.02;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
    MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0), vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0), vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0), vec4(0.0, 0.0, 0.0, 1.0));
}
void fragment() {
    vec2 c = UV * 2.0 - 1.0;
    float r = length(c);
    float p = progress;
    float ring = smoothstep(p - 0.3, p, r) * (1.0 - smoothstep(p, p + 0.06, r));
    float k = ring * (1.0 - p) * strength;
    vec2 dir = c / max(r, 1e-3);
    vec2 off = vec2(dir.x, -dir.y) * k;
    ALBEDO = textureLod(screen_tex, SCREEN_UV - off, 0.0).rgb;
    ALPHA = smoothstep(0.0, 0.02, ring * (1.0 - p));
}";

    private static ShaderMaterial Mat(ref Shader cache, string code)
    {
        cache ??= new Shader { Code = code };
        return new ShaderMaterial { Shader = cache };
    }

    private GpuParticles3D Emitter(string name, int amount, Mesh mesh, bool sorted, float maxLife)
    {
        var p = new GpuParticles3D
        {
            Name = name,
            Amount = amount,
            Lifetime = maxLife,
            Emitting = false,
            OneShot = false,
            LocalCoords = false,
            ProcessMaterial = Mat(ref _processShader, ProcessCode),
            DrawPass1 = mesh,
            DrawOrder = sorted ? GpuParticles3D.DrawOrderEnum.ViewDepth : GpuParticles3D.DrawOrderEnum.Index,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityAabb = new Aabb(new Vector3(-3000, -100, -3000), new Vector3(6000, 800, 6000)),
            FixedFps = 0,
            Interpolate = false,
        };
        AddChild(p);
        return p;
    }

    public override void _Ready()
    {
        var billowMat = _billowMat = Mat(ref _billowShader, BillowCode);
        billowMat.SetShaderParameter("noise_tex", Noise);
        billowMat.SetShaderParameter("fire_ramp", FireRamp);
        // Les explosions passent après les autres objets transparents du décor (façades, vitres) : sinon le tri par
        // distance des émetteurs (centrés sur la carte) les ferait dessiner par-dessus le feu.
        billowMat.RenderPriority = 10;
        _billows = Emitter("Billows", 2600, new QuadMesh { Size = Vector2.One * 2f, Material = billowMat }, true, 20f);
        var sparkMat = Mat(ref _sparkShader, SparkCode);
        sparkMat.RenderPriority = 11;
        _sparks = Emitter("Sparks", 1500, new QuadMesh { Size = Vector2.One, Material = sparkMat }, false, 6f);
        var flashMat = Mat(ref _flashShader, FlashCode);
        flashMat.RenderPriority = 11;
        _flashes = Emitter("Flashes", 64, new QuadMesh { Size = Vector2.One * 2f, Material = flashMat }, false, 1f);
        var rock = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 5, Rings = 3 };
        rock.Material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f, AlbedoColor = Colors.White };
        _chunks = Emitter("Chunks", 600, rock, false, 8f);
        _chunks.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        for (var i = 0; i < MaxLights; i++)
        {
            var l = new OmniLight3D { ShadowEnabled = false, Visible = false, OmniAttenuation = 1.6f, LightSpecular = 0.3f };
            AddChild(l);
            _lights.Add(new FxLight { L = l });
        }
        var hazeMat = Mat(ref _hazeShader, HazeCode);
        hazeMat.RenderPriority = 9; // juste avant les nuages : la distorsion ne recouvre jamais le feu
        for (var i = 0; i < MaxHaze; i++)
        {
            var m = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * 2f }, MaterialOverride = hazeMat, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(m);
            _haze.Add(new FxHaze { M = m });
        }
        for (var i = 0; i < MaxDecals; i++)
        {
            var d = new Decal { TextureAlbedo = Scorch, Visible = false, CullMask = 1, NormalFade = 0.4f, UpperFade = 0.3f, LowerFade = 0.3f };
            AddChild(d);
            _decals.Add(new FxDecal { D = d });
        }
        // Préchauffage pendant le chargement : chaque shader (simulation, rendu, distorsion), l'atlas des traces au sol et
        // une lumière servent une fois, loin sous le sol, pour que la première vraie explosion ne fige pas l'image.
        var under = new Vector3(0, -500, 0);
        StartHaze(under, 1f, 0f);
        _haze[0].Life = 0.1f;
        StartLight(under, Colors.Black, 0f, 1f, 0.1f);
        StartDecal(under, 1f, new Color(1, 1, 1, 0));
        _decals[0].Age = DecalLife - 0.1f;
        Emit(_billows, TFire, under, Vector3.Zero, 0.1f, 0.2f, Colors.Black);
        Emit(_sparks, TSpark, under, Vector3.Up, 0.1f, 0.2f, Colors.Black);
        Emit(_flashes, TFlash, under, Vector3.Zero, 0.1f, 0.2f, Colors.Black);
        Emit(_chunks, TChunk, under, Vector3.Zero, 0.1f, 0.2f, Colors.Black);
    }

    public override void _Process(double delta)
    {
        // --boom-skip=billows,sparks,chunks,lights,haze,decals : masque des familles d'effets (mesure de leur coût).
        if (ProfileSkip is { } skip)
        {
            _billows.Visible = !skip.Contains("billows");
            _sparks.Visible = !skip.Contains("sparks");
            _chunks.Visible = !skip.Contains("chunks");
            if (skip.Contains("lights")) foreach (var l in _lights) l.L.Visible = false;
            if (skip.Contains("haze")) foreach (var h in _haze) h.M.Visible = false;
            if (skip.Contains("decals")) foreach (var d in _decals) d.D.Visible = false;
        }
        var dt = (float)delta;
        _load *= Mathf.Exp(-dt / 1.5f);
        _sceneT -= dt;
        if (_sceneT <= 0f)
        {
            _sceneT = 1f;
            SyncSceneLight();
        }
        foreach (var l in _lights)
        {
            if (!l.L.Visible) continue;
            l.Age += dt;
            if (l.Age >= l.Life) { l.L.Visible = false; continue; }
            // Éclair bref puis lueur du feu qui décroît.
            l.L.LightEnergy = l.Peak * (Mathf.Exp(-l.Age * 22f) * 0.8f + Mathf.Exp(-l.Age * 3.2f) * 0.2f) * (1f - l.Age / l.Life);
        }
        foreach (var h in _haze)
        {
            if (!h.M.Visible) continue;
            h.Age += dt;
            if (h.Age >= h.Life) { h.M.Visible = false; continue; }
            h.M.SetInstanceShaderParameter("progress", Mathf.Sqrt(h.Age / h.Life));
        }
        foreach (var d in _decals)
        {
            if (!d.D.Visible) continue;
            d.Age += dt;
            if (d.Age >= DecalLife) { d.D.Visible = false; continue; }
            var fade = 1f - Mathf.SmoothStep(DecalLife - 5f, DecalLife, d.Age);
            d.D.Modulate = new Color(d.D.Modulate, d.Alpha * fade);
        }
    }

    /// <summary>Recopie le soleil et l'ambiance de la mission dans l'éclairage simplifié des nuages (jour, crépuscule, nuit, orage).</summary>
    private void SyncSceneLight()
    {
        var sun = _world?.Sun;
        var env = _world?.Env;
        if (sun == null || env == null || _billowMat == null) return;
        static Vector3 V(Color c) => new(c.R, c.G, c.B);
        _billowMat.SetShaderParameter("sun_dir", sun.GlobalBasis.Z.Normalized());
        _billowMat.SetShaderParameter("sun_col", V(sun.LightColor) * (sun.Visible ? sun.LightEnergy : 0f));
        _billowMat.SetShaderParameter("amb_col", V(env.AmbientLightColor) * env.AmbientLightEnergy);
    }

    private int ActiveLights() { var n = 0; foreach (var l in _lights) if (l.L.Visible) n++; return n; }
    private int ActiveDecals() { var n = 0; foreach (var d in _decals) if (d.D.Visible) n++; return n; }

    // ------------------------------------------------------------------ Émission

    private float R(float a, float b) => _rng.RandfRange(a, b);

    private Vector3 RandDir(float minY)
    {
        for (var i = 0; i < 8; i++)
        {
            var v = new Vector3(R(-1, 1), R(-1, 1), R(-1, 1));
            if (v.LengthSquared() > 1f || v.LengthSquared() < 0.01f) continue;
            v = v.Normalized();
            if (v.Y < minY) v.Y = Mathf.Abs(v.Y) * (1f - minY) + minY;
            return v.Normalized();
        }
        return Vector3.Up;
    }

    private void Emit(GpuParticles3D p, int type, Vector3 pos, Vector3 vel, float size, float life, Color color)
    {
        var custom = new Color(_rng.Randf(), 0f, size, Mathf.Min(life, 63.9f) + 64f * type);
        // Éclats : forme irrégulière (échelle non uniforme) et orientation de départ aléatoire.
        var basis = type == TChunk
            ? new Basis(RandDir(-1f), R(0, Mathf.Tau)).Scaled(new Vector3(R(0.6f, 1.4f), R(0.4f, 0.9f), 1f) * size)
            : Basis.Identity.Scaled(Vector3.One * size);
        p.EmitParticle(new Transform3D(basis, pos), vel, color, custom, EmitAll);
    }

    /// <summary>Éclair assombri quand l'option « Atténuer les flashs » est active.</summary>
    private static Color FlashTint(Color c) => Settings.I.ReduceFlashes ? c * 0.35f : c;

    private void StartLight(Vector3 pos, Color color, float peak, float range, float life)
    {
        FxLight pick = null;
        foreach (var l in _lights)
        {
            if (!l.L.Visible) { pick = l; break; }
            if (pick == null || l.L.LightEnergy < pick.L.LightEnergy) pick = l;
        }
        if (pick == null) return;
        if (Settings.I.ReduceFlashes) peak *= 0.3f;
        pick.Age = 0;
        pick.Peak = peak;
        pick.Life = life;
        pick.L.LightColor = color;
        pick.L.OmniRange = range;
        pick.L.LightEnergy = peak;
        pick.L.GlobalPosition = pos;
        pick.L.Visible = true;
    }

    private void StartHaze(Vector3 pos, float size, float strength)
    {
        FxHaze pick = null;
        foreach (var h in _haze)
        {
            if (!h.M.Visible) { pick = h; break; }
            if (pick == null || h.Age > pick.Age) pick = h;
        }
        if (pick == null) return;
        pick.Age = 0;
        pick.Life = 0.32f;
        pick.M.GlobalPosition = pos;
        pick.M.Scale = Vector3.One * size;
        pick.M.SetInstanceShaderParameter("progress", 0f);
        pick.M.SetInstanceShaderParameter("strength", strength);
        pick.M.Visible = true;
    }

    private void StartDecal(Vector3 pos, float size, Color tint)
    {
        var d = _decals[_nextDecal];
        _nextDecal = (_nextDecal + 1) % _decals.Count;
        d.Age = 0;
        d.Alpha = tint.A;
        d.D.Size = new Vector3(size, 2.4f, size);
        d.D.Modulate = tint;
        d.D.GlobalPosition = new Vector3(pos.X, Mathf.Max(pos.Y - 0.6f, 0f) + 0.3f, pos.Z);
        d.D.Rotation = new Vector3(0, R(0, Mathf.Tau), 0);
        d.D.Visible = true;
    }

    /// <summary>Point d'entrée : <paramref name="radius"/> est le rayon de dégâts (4 à 22 m), la taille visuelle en découle.</summary>
    public void Spawn(Vector3 pos, float radius, int kind)
    {
        _total++;
        _load += 1f;
        var cam = GetViewport()?.GetCamera3D();
        var dist = cam != null ? cam.GlobalPosition.DistanceTo(pos) : 30f;
        // Niveau de détail : distance (au-delà de 70 m on perd des particules), puis cadence (salves).
        var lod = Mathf.Clamp(1f - (dist - 70f) / 200f, 0.35f, 1f);
        var busy = _busy = _load > 5f ? Mathf.Clamp(5f / _load, 0.25f, 1f) : 1f;
        var q = lod * busy;
        var vis = Mathf.Clamp(radius, 3f, 24f);
        var big = vis >= 12f;
        var onGround = pos.Y < 2.5f;
        var ground = new Vector3(pos.X, 0.05f, pos.Z);
        int N(float n) => Mathf.Max(1, Mathf.RoundToInt(n * q));

        switch (kind)
        {
            case Plasma: SpawnPlasma(pos, vis, dist, q, onGround, ground, N); break;
            case Acid: SpawnAcid(pos, vis, dist, q, onGround, ground, N); break;
            default: SpawnExplosive(pos, vis, dist, q, big, onGround, ground, N); break;
        }
    }

    private delegate int Count(float n);

    private void SpawnExplosive(Vector3 pos, float vis, float dist, float q, bool big, bool onGround, Vector3 ground, Count N)
    {
        var fb = vis * 0.32f; // rayon de la boule de feu
        // Éclair.
        Emit(_flashes, TFlash, pos + Vector3.Up * fb * 0.4f, Vector3.Zero, fb * 1.6f, 0.12f, FlashTint(new Color(1f, 0.78f, 0.5f)));
        if (dist < (_busy < 1f && !big ? 60f : 130f)) StartLight(pos + Vector3.Up * (1f + fb * 0.5f), new Color(1f, 0.6f, 0.3f), 5f + vis * 0.9f, vis * 1.4f + 6f, big ? 1.4f : 0.7f);
        if (dist < 55f && onGround && (big || _busy >= 1f)) StartHaze(pos + Vector3.Up * fb * 0.5f, vis * 0.9f, big ? 0.016f : 0.011f);

        // Boule de feu : boules serrées projetées vers l'extérieur (hémisphère haut au sol), freinées net.
        var nFire = N(big ? 22 : 12);
        for (var i = 0; i < nFire; i++)
        {
            var d = RandDir(onGround ? 0.25f : -1f);
            var sp = R(0.6f, 1.5f) * fb * 3.4f;
            var p = pos + d * fb * R(0.05f, 0.45f);
            var sz = fb * R(0.55f, 0.95f) * (1f + (1f - q) * 0.25f);
            var g = R(0.04f, 0.09f);
            Emit(_billows, TFire, p, d * sp, sz, R(1.6f, 2.6f) * (big ? 1.4f : 1f), new Color(g, g * 0.95f, g * 0.9f, 0.95f));
        }
        // Colonne : pour les grosses, un noyau qui monte (champignon).
        if (big)
            for (var i = 0; i < N(6); i++)
            {
                var p = pos + new Vector3(R(-0.3f, 0.3f), R(0.2f, 0.8f), R(-0.3f, 0.3f)) * fb;
                Emit(_billows, TFire, p, new Vector3(R(-1, 1), R(7f, 12f), R(-1, 1)) * fb * 0.35f, fb * R(0.7f, 1f), R(2.2f, 3f), new Color(0.05f, 0.048f, 0.045f, 0.95f));
            }

        // Fumée noire épaisse qui reste et dérive.
        var nSmoke = N(big ? 16 : 8);
        var smokeLife = (big ? R(9f, 13f) : R(5.5f, 8f)) * Mathf.Lerp(0.6f, 1f, q);
        for (var i = 0; i < nSmoke; i++)
        {
            var d = RandDir(onGround ? 0.2f : -0.4f);
            var up = (float)i / nSmoke;
            var p = pos + d * fb * R(0.2f, 0.8f) + Vector3.Up * fb * 0.3f;
            var v = d * fb * R(0.5f, 1.3f) + Vector3.Up * fb * R(0.8f, 2.4f) * (0.4f + up);
            var g = R(0.045f, 0.085f);
            Emit(_billows, TSmoke, p, v, fb * R(0.8f, 1.2f), smokeLife * R(0.75f, 1.1f), new Color(g, g * 0.97f, g * 0.94f, R(0.65f, 0.85f)));
        }

        if (onGround)
        {
            // Onde de choc : anneau de poussière rasant.
            var nRing = N(big ? 26 : 14);
            for (var i = 0; i < nRing; i++)
            {
                var a = (i + R(-0.3f, 0.3f)) / nRing * Mathf.Tau;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var g = R(0.24f, 0.32f);
                Emit(_billows, TDust, ground + d * fb * 0.6f + Vector3.Up * fb * 0.25f, d * vis * R(1.1f, 1.9f) + Vector3.Up * R(0.2f, 1f),
                    fb * R(0.35f, 0.55f), R(2f, 3.6f) * Mathf.Lerp(0.6f, 1f, q), new Color(g, g * 0.95f, g * 0.86f, R(0.3f, 0.45f)));
            }
            // Gerbes de terre : quelques jets en cône, chacun fait de mottes de plus en plus lentes (traînée), qui
            // retombent en poussière brune.
            var nJets = N(big ? 8 : 5);
            for (var i = 0; i < nJets; i++)
            {
                var d = RandDir(0.6f);
                var sp = Mathf.Sqrt(vis) * R(3f, 5.5f);
                for (var k = 0; k < 3; k++)
                {
                    var g = R(0.09f, 0.14f);
                    Emit(_billows, TDirt, ground + Vector3.Up * 0.3f + d * k * 0.3f, d * sp * (1f - k * 0.22f), fb * R(0.16f, 0.26f) * (1f - k * 0.15f), R(1.3f, 2.2f),
                        new Color(g, g * 0.88f, g * 0.74f, R(0.65f, 0.85f)));
                }
            }
            StartDecal(pos, vis * 0.9f + 1.5f, new Color(1, 1, 1, 0.92f));
        }

        // Braises traçantes et étincelles.
        var nSparks = N(big ? 46 : 22);
        for (var i = 0; i < nSparks; i++)
        {
            var d = RandDir(onGround ? 0.12f : -1f);
            var slow = i % 3 == 0;
            var sp = slow ? R(3f, 8f) : R(10f, 26f) * Mathf.Sqrt(vis / 6f);
            Emit(_sparks, TSpark, pos + d * fb * 0.3f, d * sp, slow ? R(0.05f, 0.09f) : R(0.03f, 0.06f), slow ? R(1.5f, 3f) : R(0.5f, 1.4f),
                new Color(1f, R(0.45f, 0.65f), 0.15f, R(0.7f, 1f)));
        }
        // Éclats sombres (béton, terre) qui rebondissent.
        if (dist < 120f)
        {
            var nChunks = N(big ? 18 : 9);
            for (var i = 0; i < nChunks; i++)
            {
                var d = RandDir(onGround ? 0.35f : -0.6f);
                var g = R(0.05f, 0.14f);
                Emit(_chunks, TChunk, pos + d * 0.4f, d * R(6f, 16f) * Mathf.Sqrt(vis / 6f), R(0.06f, 0.2f) * (big ? 1.5f : 1f), R(3.5f, 6f),
                    new Color(g, g * 0.95f, g * 0.88f));
            }
        }
    }

    private void SpawnPlasma(Vector3 pos, float vis, float dist, float q, bool onGround, Vector3 ground, Count N)
    {
        var fb = vis * 0.3f;
        var blue = new Color(0.2f, 0.5f, 1f);
        Emit(_flashes, TFlash, pos + Vector3.Up * fb * 0.3f, Vector3.Zero, fb * 2.6f, 0.16f, FlashTint(new Color(0.7f, 0.85f, 1f)));
        if (dist < (_busy < 1f ? 60f : 130f)) StartLight(pos + Vector3.Up * (1f + fb * 0.5f), new Color(0.5f, 0.72f, 1f), 6f + vis * 1f, vis * 1.6f + 6f, 0.6f);
        if (dist < 55f && _busy >= 1f) StartHaze(pos + Vector3.Up * fb * 0.4f, vis * 1f, 0.018f);
        for (var i = 0; i < N(10); i++)
        {
            var d = RandDir(onGround ? 0f : -1f);
            Emit(_billows, TEnergy, pos + d * fb * 0.2f, d * fb * R(2f, 5f), fb * R(0.6f, 0.9f), R(0.5f, 0.9f), new Color(blue, 0.9f));
        }
        // Peu de fumée : vapeur gris-bleu légère.
        for (var i = 0; i < N(5); i++)
        {
            var d = RandDir(0.2f);
            var g = R(0.3f, 0.4f);
            Emit(_billows, TSmoke, pos + d * fb * 0.5f, d * fb * 0.8f + Vector3.Up * fb * R(0.8f, 1.6f), fb * R(0.7f, 1f), R(2.5f, 4f), new Color(g * 0.9f, g * 0.95f, g, 0.4f));
        }
        if (onGround)
        {
            var nRing = N(12);
            for (var i = 0; i < nRing; i++)
            {
                var a = (i + R(-0.3f, 0.3f)) / nRing * Mathf.Tau;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var g = R(0.26f, 0.33f);
                Emit(_billows, TDust, ground + d * fb * 0.5f + Vector3.Up * fb * 0.2f, d * vis * R(2.4f, 3.4f), fb * R(0.4f, 0.65f), R(2f, 3.5f), new Color(g, g, g * 0.95f, 0.5f));
            }
            StartDecal(pos, vis * 0.75f + 1f, new Color(0.85f, 0.9f, 1f, 0.8f));
        }
        for (var i = 0; i < N(30); i++)
        {
            var d = RandDir(onGround ? 0.1f : -1f);
            Emit(_sparks, TSpark, pos + d * fb * 0.2f, d * R(8f, 22f), R(0.03f, 0.06f), R(0.4f, 1.1f), new Color(0.55f, 0.8f, 1f, 1f));
        }
    }

    private void SpawnAcid(Vector3 pos, float vis, float dist, float q, bool onGround, Vector3 ground, Count N)
    {
        var fb = vis * 0.35f;
        var green = new Color(0.55f, 0.85f, 0.12f);
        if (dist < 90f) StartLight(pos + Vector3.Up * 1.2f, new Color(0.55f, 1f, 0.3f), 2.5f + vis * 0.4f, vis * 2f + 5f, 0.6f);
        // Gerbe de gouttes (éclats verts luisants) et giclée.
        for (var i = 0; i < N(22); i++)
        {
            var d = RandDir(onGround ? 0.25f : -0.8f);
            Emit(_chunks, TChunk, pos + d * 0.2f, d * R(3f, 9f) * Mathf.Sqrt(vis / 3f), R(0.06f, 0.16f), R(1.2f, 2.2f), new Color(0.5f, 0.78f, 0.1f));
        }
        // Filets d'acide luisants (traînées vertes, faiblement lumineuses).
        for (var i = 0; i < N(18); i++)
        {
            var d = RandDir(onGround ? 0.3f : -0.8f);
            Emit(_sparks, TSpark, pos + d * 0.2f, d * R(4f, 10f) * Mathf.Sqrt(vis / 3f), R(0.04f, 0.08f), R(0.6f, 1.2f), new Color(0.45f, 1f, 0.12f, 0.35f));
        }
        for (var i = 0; i < N(4); i++)
        {
            var d = RandDir(0.1f);
            Emit(_billows, TEnergy, pos + d * fb * 0.2f, d * fb * R(1.5f, 3f), fb * R(0.4f, 0.7f), R(0.25f, 0.4f), new Color(0.45f, 0.95f, 0.15f, 0.4f));
        }
        // Gaz toxique jaune-vert, lourd, qui stagne.
        for (var i = 0; i < N(12); i++)
        {
            var d = RandDir(0.05f);
            var p = pos + d * fb * 0.6f;
            Emit(_billows, TGas, p, new Vector3(d.X, 0.3f, d.Z) * fb * R(1f, 3f), fb * R(0.55f, 0.95f), R(4f, 6.5f), new Color(green.R * 0.55f, green.G * 0.6f, green.B * 0.5f, R(0.22f, 0.4f)));
        }
        if (onGround) StartDecal(pos, vis * 0.8f + 1f, new Color(0.55f, 0.75f, 0.3f, 0.75f));
    }
}
