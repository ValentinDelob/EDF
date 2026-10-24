using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Outils communs des machines extraterrestres (Marcheur, Tripode, vaisseau de largage) : blindage usé en repère local
/// (les salissures suivent la machine qui marche, contrairement au shader « Weathered » des immeubles), plaques
/// chanfreinées, cylindres à bords cassés, vérins, grilles d'aération, câbles, et cinématique inverse plane.
/// </summary>
public partial class BugModel
{
    /// <summary>Machine extraterrestre (Marcheur, Tripode, vaisseau de largage) : blindage, IA à distance, pas de giclée.</summary>
    public bool IsMachine => Type is EnemyType.Walker or EnemyType.Tripod or EnemyType.DropShip;

    /// <summary>Anime la machine (vitesse en m/s, attaque 0..1).</summary>
    public void AnimateMachine(float dt, float speed, float attack)
    {
        switch (Type)
        {
            case EnemyType.Walker: AnimateWalker(dt, speed, attack); break;
            case EnemyType.Tripod: AnimateTripod(dt, speed, attack); break;
            case EnemyType.DropShip: AnimateDropShip(dt, speed, attack); break;
        }
    }

    /// <summary>Animation de destruction propre à chaque machine.</summary>
    public void MachineDeath()
    {
        switch (Type)
        {
            case EnemyType.Walker: WalkerDeath(); break;
            case EnemyType.Tripod: TripodDeath(); break;
            case EnemyType.DropShip: DropShipDeath(); break;
        }
    }

    // ---------------- Palette ----------------

    /// <summary>Acier gris canon (plaques principales).</summary>
    private static readonly Color MGun = new(0.47f, 0.48f, 0.48f);
    /// <summary>Acier plus sombre (sous-ensembles, plaques secondaires).</summary>
    private static readonly Color MSteel = new(0.33f, 0.335f, 0.34f);
    /// <summary>Mécanique noircie (articulations, intérieurs, joints).</summary>
    private static readonly Color MDark = new(0.085f, 0.085f, 0.09f);
    /// <summary>Peinture d'origine délavée, ocre terne (quelques panneaux).</summary>
    private static readonly Color MOchre = new(0.44f, 0.4f, 0.3f);
    /// <summary>Joints de panneaux.</summary>
    private static readonly Color MGroove = new(0.035f, 0.035f, 0.04f);
    /// <summary>Métal nu poli par l'usure (tiges de vérins, pignons).</summary>
    private static readonly Color MBare = new(0.62f, 0.62f, 0.6f);

    // ---------------- Matériaux ----------------

    private const string MachineArmorCode = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D grime_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform sampler2D detail_tex : hint_normal, filter_linear_mipmap, repeat_enable;
uniform vec3 tint = vec3(1.0);
uniform float metallic = 0.55;
uniform float roughness = 0.6;
uniform float grime = 1.0;
uniform float rust = 0.5;
uniform float wear = 0.5;
uniform float detail = 0.35;

varying vec3 v_p;
varying vec3 v_n;
varying vec3 v_w;

void vertex() {
	v_p = VERTEX;
	v_n = NORMAL;
	vec3 a = abs(NORMAL);
	v_w = pow(a, vec3(4.0));
	v_w /= dot(v_w, vec3(1.0));
	// Repère triplanaire local pour la carte de relief.
	TANGENT = normalize(vec3(0.0, 0.0, -1.0) * a.x + vec3(1.0, 0.0, 0.0) * a.y + vec3(1.0, 0.0, 0.0) * a.z);
	BINORMAL = normalize(vec3(0.0, 1.0, 0.0) * a.x + vec3(0.0, 0.0, -1.0) * a.y + vec3(0.0, 1.0, 0.0) * a.z);
	COLOR.rgb = mix(pow((COLOR.rgb + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), COLOR.rgb * (1.0 / 12.92), lessThan(COLOR.rgb, vec3(0.04045)));
}

vec4 tri(sampler2D s, float k) {
	return texture(s, v_p.zy * k) * v_w.x + texture(s, v_p.xz * k) * v_w.y + texture(s, v_p.xy * k) * v_w.z;
}

void fragment() {
	vec3 n = normalize(v_n);
	vec4 g = tri(grime_tex, 0.09);
	vec4 g2 = tri(grime_tex, 0.31);
	float side = 1.0 - smoothstep(0.55, 0.9, abs(n.y));
	float hu = abs(n.x) > abs(n.z) ? v_p.z : v_p.x;
	float st = texture(grime_tex, vec2(hu * 0.37, v_p.y * 0.03)).r * 0.65 + texture(grime_tex, vec2(hu * 0.91 + 0.3, v_p.y * 0.07 + 0.5)).r * 0.35;
	vec3 col = COLOR.rgb * tint;
	// Taches, coulures sous les arêtes, dessous encrassé.
	float dirt = smoothstep(0.42, 0.92, g.g * 0.65 + g2.g * 0.35) * 0.5 + side * smoothstep(0.5, 0.88, st) * 0.55 + max(-n.y, 0.0) * 0.3;
	dirt = clamp(dirt * grime, 0.0, 0.82);
	col *= mix(vec3(1.0), vec3(0.4, 0.38, 0.35), dirt);
	// Rouille brune terne (plaques, coulures).
	float rs = rust * smoothstep(0.6, 0.86, g2.g * 0.45 + st * side * 0.35 + g.b * 0.4);
	col = mix(col, vec3(0.13, 0.065, 0.032) * (0.7 + 0.6 * g2.r), rs);
	// Éraflures : métal nu plus clair.
	float sc = wear * smoothstep(0.78, 0.93, g2.b) * (1.0 - rs);
	col = mix(col, vec3(0.45, 0.45, 0.44), sc * 0.55);
	// Poussière de gravats sur les surfaces horizontales.
	float dust = smoothstep(0.55, 0.95, n.y) * smoothstep(0.3, 0.75, g.a) * 0.45 * grime;
	col = mix(col, vec3(0.21, 0.2, 0.18), dust);
	ALBEDO = col;
	NORMAL_MAP = tri(detail_tex, 0.45).rgb;
	NORMAL_MAP_DEPTH = detail;
	ROUGHNESS = clamp(roughness * (0.8 + 0.4 * g.g) + rs * 0.35 + dust * 0.35 - sc * 0.3, 0.05, 1.0);
	METALLIC = clamp(metallic * (1.0 - rs) * (1.0 - dust * 0.6) + sc * 0.3, 0.0, 1.0);
}
";

    private static Shader _machShader;
    private static ImageTexture _machGrime, _machDetail;
    private static ShaderMaterial _machArmor, _machArmorElite, _machMech;

    /// <summary>
    /// Textures périodiques 256² des machines (une fois) : R = coulures (bruit étiré verticalement), G = taches,
    /// B = piqûres et éraflures, A = poussière ; relief fin (martelage, piqûres) pour la carte de normales.
    /// </summary>
    private static void MachineTextures()
    {
        if (_machGrime != null) return;
        const int s = 256;
        var img = Image.CreateEmpty(s, s, false, Image.Format.Rgba8);
        var bump = Image.CreateEmpty(s, s, false, Image.Format.Rgba8);
        var data = new byte[s * s * 4];
        var bdata = new byte[s * s * 4];
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var streak = PNoise(x, y, 4, 48, s, 501) * 0.55f + PNoise(x, y, 2, 96, s, 502) * 0.3f + PNoise(x, y, 8, 24, s, 503) * 0.15f;
                streak = Mathf.Clamp((streak - 0.5f) * 1.9f + 0.5f, 0, 1);
                var blot = PNoise(x, y, 64, 64, s, 511) * 0.42f + PNoise(x, y, 32, 32, s, 512) * 0.28f + PNoise(x, y, 16, 16, s, 513) * 0.18f + PNoise(x, y, 4, 4, s, 514) * 0.12f;
                blot = Mathf.Clamp((blot - 0.5f) * 1.7f + 0.5f, 0, 1);
                var pits = PNoise(x, y, 8, 8, s, 521) * 0.5f + PNoise(x, y, 2, 2, s, 522) * 0.5f;
                var dust = PNoise(x, y, 32, 32, s, 531) * 0.6f + PNoise(x, y, 8, 8, s, 532) * 0.4f;
                var i = (y * s + x) * 4;
                data[i] = (byte)(streak * 255);
                data[i + 1] = (byte)(blot * 255);
                data[i + 2] = (byte)(pits * 255);
                data[i + 3] = (byte)(dust * 255);
                var h = PNoise(x, y, 16, 16, s, 541) * 0.45f + PNoise(x, y, 4, 4, s, 542) * 0.35f + PNoise(x, y, 2, 2, s, 543) * 0.2f;
                var hb = (byte)(Mathf.Clamp(h, 0, 1) * 255);
                bdata[i] = bdata[i + 1] = bdata[i + 2] = hb;
                bdata[i + 3] = 255;
            }
        });
        img.SetData(s, s, false, Image.Format.Rgba8, data);
        img.GenerateMipmaps();
        _machGrime = ImageTexture.CreateFromImage(img);
        bump.SetData(s, s, false, Image.Format.Rgba8, bdata);
        bump.BumpMapToNormalMap(3f);
        bump.GenerateMipmaps();
        _machDetail = ImageTexture.CreateFromImage(bump);
    }

    /// <summary>Bruit de valeur périodique (cellules cx × cy pixels sur une tuile de <paramref name="size"/> pixels).</summary>
    private static float PNoise(int x, int y, int cx, int cy, int size, int seed)
    {
        var fx = x / (float)cx;
        var fy = y / (float)cy;
        var ix = Mathf.FloorToInt(fx);
        var iy = Mathf.FloorToInt(fy);
        var tx = fx - ix;
        var ty = fy - iy;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        int px = Math.Max(1, size / cx), py = Math.Max(1, size / cy);
        float H(int i, int j)
        {
            i = ((i % px) + px) % px;
            j = ((j % py) + py) % py;
            var h = (uint)(i * 374761393 + j * 668265263 + seed * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
        var a = Mathf.Lerp(H(ix, iy), H(ix + 1, iy), tx);
        var b = Mathf.Lerp(H(ix, iy + 1), H(ix + 1, iy + 1), tx);
        return Mathf.Lerp(a, b, ty);
    }

    private static ShaderMaterial MachMat(Vector3 tint, float metallic, float rough, float grime, float rust, float wear)
    {
        MachineTextures();
        _machShader ??= new Shader { Code = MachineArmorCode };
        var m = new ShaderMaterial { Shader = _machShader };
        m.SetShaderParameter("grime_tex", _machGrime);
        m.SetShaderParameter("detail_tex", _machDetail);
        m.SetShaderParameter("tint", tint);
        m.SetShaderParameter("metallic", metallic);
        m.SetShaderParameter("roughness", rough);
        m.SetShaderParameter("grime", grime);
        m.SetShaderParameter("rust", rust);
        m.SetShaderParameter("wear", wear);
        return m;
    }

    /// <summary>Blindage des machines : acier satiné teinté par les couleurs de sommets, salissures et rouille en repère local.</summary>
    private static ShaderMaterial MachArmor() => _machArmor ??= MachMat(Vector3.One, 0.38f, 0.58f, 1f, 0.55f, 0.5f);

    /// <summary>Variante élite : blindage bronze sombre (même usure).</summary>
    private static ShaderMaterial MachArmorElite() => _machArmorElite ??= MachMat(new Vector3(1.55f, 1.12f, 0.5f), 0.7f, 0.45f, 0.8f, 0.25f, 0.6f);

    /// <summary>Mécanique graissée (articulations, vérins, intérieurs) : plus sombre et plus lisse.</summary>
    private static ShaderMaterial MachMech() => _machMech ??= MachMat(Vector3.One, 0.75f, 0.42f, 0.7f, 0.25f, 0.8f);

    /// <summary>Capteurs et lueurs hostiles (rouge).</summary>
    private static StandardMaterial3D MachRed() => Mats.Glow(new Color(1f, 0.12f, 0.06f), 5f);

    /// <summary>Énergie (noyau, émetteur, réacteurs) : orange incandescent.</summary>
    private static StandardMaterial3D MachHot() => Mats.Glow(new Color(1f, 0.42f, 0.12f), 4f);

    // ---------------- Formes ----------------

    /// <summary>
    /// Plaque chanfreinée : boîte aux arêtes adoucies (loft de section superelliptique le long de l'axe Z local de
    /// <paramref name="t"/>), demi-tailles <paramref name="half"/>, chanfrein <paramref name="bev"/> aux deux bouts.
    /// <paramref name="taper"/> réduit la section à l'avant (+Z) : plaques en biseau, capots, carénages.
    /// </summary>
    private static void Plate(InsectMesh m, Transform3D t, Vector3 half, Color col, float bev = 0.12f, float sq = 9f, float taper = 1f, float taperY = -1f)
    {
        var b = t.Basis.Orthonormalized();
        var x = b.X;
        var y = b.Y;
        var z = b.Z;
        bev = Mathf.Min(bev, Mathf.Min(half.Z * 0.45f, Mathf.Min(half.X, half.Y) * 0.6f));
        if (taperY < 0) taperY = taper;
        Vector2 Sec(float k) => new(half.X * Mathf.Lerp(1f, taper, k), half.Y * Mathf.Lerp(1f, taperY, k));
        var rings = new List<InsectMesh.Ring>();
        void R(float zz, Vector2 r) => rings.Add(new InsectMesh.Ring(t.Origin + z * zz, x, y, Mathf.Max(r.X, 0.001f), Mathf.Max(r.Y, 0.001f), col));
        var s0 = Sec(0f);
        var s1 = Sec(1f);
        rings.Add(new InsectMesh.Ring(t.Origin - z * half.Z, x, y, 0, 0, col));
        R(-half.Z, s0 - new Vector2(bev, bev));
        R(-half.Z + bev, s0);
        R(half.Z - bev, s1);
        R(half.Z, s1 - new Vector2(bev, bev));
        rings.Add(new InsectMesh.Ring(t.Origin + z * half.Z, x, y, 0, 0, col));
        m.Loft(rings, 20, sq);
    }

    /// <summary>Plaque orientée par ses angles (lacet, tangage, roulis en radians) : raccourci de <see cref="Plate"/>.</summary>
    private static void PlateAt(InsectMesh m, Vector3 c, Vector3 half, Color col, float yaw = 0, float pitch = 0, float roll = 0, float bev = 0.12f, float taper = 1f)
    {
        var b = Basis.FromEuler(new Vector3(pitch, yaw, roll));
        Plate(m, new Transform3D(b, c), half, col, bev, 9f, taper);
    }

    /// <summary>Cylindre à bords cassés de a à b (rayon r), bouts plats.</summary>
    private static void Cyl(InsectMesh m, Vector3 a, Vector3 b, float r, int sides, Color col, float bev = -1f, float r1 = -1f)
    {
        if (r1 < 0) r1 = r;
        var d = b - a;
        var len = d.Length();
        if (len < 1e-4f) return;
        d /= len;
        var (x, y) = InsectMesh.Frame(d, Mathf.Abs(d.Y) > 0.95f ? Vector3.Forward : Vector3.Up);
        if (bev < 0) bev = Mathf.Min(r, r1) * 0.18f;
        bev = Mathf.Min(bev, len * 0.3f);
        var rings = new List<InsectMesh.Ring>
        {
            new(a, x, y, 0, 0, col),
            new(a, x, y, r - bev, r - bev, col),
            new(a + d * bev, x, y, r, r, col),
            new(b - d * bev, x, y, r1, r1, col),
            new(b, x, y, r1 - bev, r1 - bev, col),
            new(b, x, y, 0, 0, col),
        };
        m.Loft(rings, sides);
    }

    /// <summary>Vérin hydraulique : corps de a vers b sur 55 %, tige polie jusqu'à b, rotules aux deux bouts.</summary>
    private static void Piston(InsectMesh m, Vector3 a, Vector3 b, float r, Color body)
    {
        var mid = a.Lerp(b, 0.55f);
        Cyl(m, a, mid, r, 10, body);
        Cyl(m, mid - (b - a).Normalized() * 0.1f, b, r * 0.48f, 8, MBare);
        Cyl(m, mid - (b - a).Normalized() * 0.05f, mid + (b - a).Normalized() * 0.08f, r * 1.12f, 10, MDark);
        m.Ellipsoid(a, Vector3.One * r * 1.15f, 3, 8, MDark);
        m.Ellipsoid(b, Vector3.One * r * 0.85f, 3, 8, MDark);
    }

    /// <summary>Grille d'aération : <paramref name="n"/> lames sombres sur un cadre, dans le plan (X, Y) de <paramref name="t"/> (normale +Z).</summary>
    private static void Vents(InsectMesh m, Transform3D t, Vector2 size, int n, Color frame)
    {
        Plate(m, t * new Transform3D(Basis.Identity, new Vector3(0, 0, -0.04f)), new Vector3(size.X / 2 + 0.08f, size.Y / 2 + 0.08f, 0.06f), frame, 0.03f);
        for (var i = 0; i < n; i++)
        {
            var yy = -size.Y / 2 + (i + 0.5f) * size.Y / n;
            m.Box(t * new Transform3D(new Basis(Vector3.Right, 0.6f), new Vector3(0, yy, 0.02f)), new Vector3(size.X / 2, size.Y / n * 0.32f, 0.02f), MDark);
        }
    }

    /// <summary>Joint de panneau (fine baguette sombre) de a à b, saillie vers <paramref name="n"/>.</summary>
    private static void Groove(InsectMesh m, Vector3 a, Vector3 b, Vector3 n, float w = 0.05f)
    {
        var d = b - a;
        var len = d.Length();
        if (len < 1e-3f) return;
        var z = d / len;
        var x = n.Cross(z).Normalized();
        var y = z.Cross(x);
        m.Box(new Transform3D(new Basis(x, y, z), (a + b) / 2), new Vector3(w, 0.02f, len / 2), MGroove);
    }

    /// <summary>Câble ou durite qui pend entre deux points (chaînette approchée).</summary>
    private static void Cable(InsectMesh m, Vector3 a, Vector3 b, float r, float sag, Color col)
    {
        var pts = new Vector3[7];
        var rad = new float[7];
        for (var i = 0; i < 7; i++)
        {
            var k = i / 6f;
            pts[i] = a.Lerp(b, k) + Vector3.Down * (sag * 4f * k * (1 - k));
            rad[i] = r;
        }
        m.Tube(pts, rad, 6, col, Vector3.Up);
    }

    // ---------------- Cinématique inverse plane ----------------

    /// <summary>
    /// Deux os dans un plan vertical (y vers le haut, z vers l'avant) : angles (depuis la verticale descendante, positifs
    /// vers l'avant) de la cuisse et du tibia pour amener la cheville en (dy, dz) depuis la hanche. <paramref name="kneeFwd"/> :
    /// genou devant (jambe digitigrade dont le tibia repart vers l'arrière).
    /// </summary>
    private static void PlanarIk(float lf, float lt, float dy, float dz, bool kneeFwd, out float thigh, out float shin)
    {
        var d = Mathf.Clamp(Mathf.Sqrt(dy * dy + dz * dz), Mathf.Abs(lf - lt) + 0.02f, lf + lt - 0.02f);
        var phi = Mathf.Atan2(dz, -dy);
        var a = Mathf.Acos(Mathf.Clamp((lf * lf + d * d - lt * lt) / (2 * lf * d), -1, 1));
        thigh = phi + (kneeFwd ? a : -a);
        // Genou → cheville.
        var ky = -Mathf.Cos(thigh) * lf;
        var kz = Mathf.Sin(thigh) * lf;
        var sy = dy * (d / Mathf.Max(Mathf.Sqrt(dy * dy + dz * dz), 1e-4f)) - ky;
        var sz = dz * (d / Mathf.Max(Mathf.Sqrt(dy * dy + dz * dz), 1e-4f)) - kz;
        shin = Mathf.Atan2(sz, -sy);
    }

    /// <summary>Phase de marche : avance du pied (1 = devant, -1 = derrière) et levée (0..1) pour u ∈ [0, 1[.</summary>
    private static (float fa, float lift) GaitCurve(float u, float stance)
    {
        if (u < stance) return (1f - 2f * u / stance, 0f);
        var w = (u - stance) / (1f - stance);
        return (-1f + 2f * w * w * (3f - 2f * w), Mathf.Sin(w * Mathf.Pi));
    }

    /// <summary>
    /// Préchauffage pendant l'écran de déploiement si la mission compte des machines : maillages construits (aucun à-coup
    /// à la première apparition) et un exemplaire minuscule de chaque machine devant la caméra, effets visibles (rayon,
    /// visée, charges, flammes), le temps de compiler les shaders.
    /// </summary>
    public static void PrewarmMachines(Camera3D cam, MissionDef mission)
    {
        var types = mission.Phases.SelectMany(p => p.Waves).SelectMany(w => w.Groups).Select(g => g.type)
            .Where(t => t is EnemyType.Walker or EnemyType.Tripod or EnemyType.DropShip).Distinct().ToList();
        if (types.Count == 0 || cam == null) return;
        var root = new Node3D { Name = "MachinePrewarm", Position = new Vector3(0.3f, 0, -4f), Scale = Vector3.One * 0.004f };
        cam.AddChild(root);
        foreach (var t in types)
        {
            var m = Create(t);
            root.AddChild(m);
            foreach (var fx in new Node3D[] { m._wSight, m._tBeam, m._tImpact })
                if (fx != null) { fx.TopLevel = false; fx.Visible = true; }
            foreach (var b in m._wChargeBall) if (b != null) b.Visible = true;
        }
        cam.GetTree().CreateTimer(1.5f).Timeout += () => { if (IsInstanceValid(root)) root.QueueFree(); };
    }

    /// <summary>Le pied vient de se poser : début de l'appui (u passe par 0 en marche avant, par la fin de l'appui à reculons).</summary>
    private static bool FootDown(float prev, float u, float stance) =>
        prev > 0.85f && u < 0.15f || prev > stance && u <= stance && prev - u < 0.2f;

    /// <summary>Remplace le blindage par sa variante élite (bronze sombre) sur toutes les pièces.</summary>
    private void ApplyMachineElite()
    {
        var armor = MachArmor();
        var elite = MachArmorElite();
        foreach (var node in FindChildren("*", "MeshInstance3D", true, false))
        {
            var mi = (MeshInstance3D)node;
            if (mi.Mesh == null) continue;
            for (var s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
                if (mi.Mesh.SurfaceGetMaterial(s) == armor) mi.SetSurfaceOverrideMaterial(s, elite);
        }
    }

    private static readonly Dictionary<Color, StandardMaterial3D> FlameMats = new();

    /// <summary>Flamme de tuyère : additive, opaque à la tuyère et transparente au bout (dégradé le long de la hauteur).</summary>
    private static StandardMaterial3D FlameMat(Color c)
    {
        if (FlameMats.TryGetValue(c, out var m)) return m;
        var g = new Gradient();
        g.SetColor(0, c);
        g.SetColor(1, new Color(c.R, c.G * 0.6f, c.B * 0.4f, 0f));
        g.AddPoint(0.35f, new Color(c.R, c.G * 0.85f, c.B * 0.7f, c.A * 0.55f));
        m = new StandardMaterial3D
        {
            AlbedoTexture = new GradientTexture2D { Gradient = g, Width = 4, Height = 64, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1) },
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
        };
        FlameMats[c] = m;
        return m;
    }

    /// <summary>Faisceau lumineux (cylindre unitaire le long de -Z, à étirer par l'échelle Z).</summary>
    private static MeshInstance3D BeamMesh(float radius, Color c, float energy, bool additive)
    {
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(c, additive ? c.A : 1f),
            EmissionEnabled = true,
            Emission = c,
            EmissionEnergyMultiplier = energy,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = additive ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
            BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var holder = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 1f, RadialSegments = 12, Rings = 1, CapTop = false, CapBottom = false },
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Rotation = new Vector3(Mathf.Pi / 2, 0, 0),
            Position = new Vector3(0, 0, -0.5f),
        };
        return holder;
    }
}
