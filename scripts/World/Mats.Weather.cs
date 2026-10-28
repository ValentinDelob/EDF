using Godot;

namespace Rempart;

/// <summary>
/// Usure des surfaces : un seul shader « Weathered » partagé par les façades, le béton, la voirie, le sol et le
/// matériau de détail. Il reproduit l'essentiel d'un StandardMaterial3D (albédo × couleur de sommet, relief,
/// rugosité, émission, UV ou triplanaire) et ajoute des salissures en coordonnées monde, qui ne se répètent donc
/// pas d'un immeuble à l'autre : coulures verticales sur les murs, taches à deux échelles, pied des murs encrassé,
/// poussière et flaques sur les surfaces horizontales, rouille sur les métaux, variation de teinte à grande échelle.
/// Façades : pièces derrière les vitres (« interior mapping » : murs, sol, plafond et mobilier en perspective, couleur
/// propre à chaque pièce, éclairées la nuit quand la fenêtre l'est).
/// </summary>
public static partial class Mats
{
    private const string WeatheredCode = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

// Humidité de la mission (paramètre global déclaré dans project.godot, réglé par World.BuildEnvironment) : flaques
// nombreuses sous la pluie, rares et petites par beau temps.
global uniform float wetness;
uniform vec4 albedo : source_color = vec4(1.0);
uniform sampler2D albedo_tex : source_color, hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rough_tex : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D emission_tex : source_color, hint_default_black, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D grime_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform float roughness = 1.0;
uniform float metallic = 0.0;
uniform float specular = 0.5;
uniform float normal_depth = 1.0;
uniform float emission_energy = 0.0;
uniform vec3 uv_scale = vec3(1.0);
uniform int mapping = 0; // 0 : UV ; 1 : triplanaire (repère local) ; 2 : triplanaire (repère monde)
uniform bool vertex_color = false;
uniform float grime = 1.0;      // intensité générale des salissures
uniform float streaks = 1.0;    // coulures verticales sur les murs
uniform float foot = 1.0;       // pied des murs encrassé (éclaboussures, poussière)
uniform float puddles = 0.0;    // flaques sur les surfaces horizontales
uniform float macro_var = 1.0;  // variation de teinte à grande échelle
uniform float desat = 0.0;      // désaturation de l'albédo
uniform float rust = 0.0;       // rouille (métaux peints)
uniform float emission_patch = 0.0; // émission limitée à des plaques (échelle du masque monde ; 0 : partout)
// Pièces derrière les vitres (façades) : 0 = aucune, 1 = fenêtres percées ou en bandeau, 2 = mur-rideau.
uniform int interior = 0;
uniform vec4 win_rect = vec4(2.0, 0.25, 0.26, 0.8); // travées par U, demi-largeur, haut et bas de la vitre (fractions d'étage)
uniform vec2 cell_m = vec2(3.5, 3.2);               // taille d'une travée et d'un étage (m)
uniform float room_depth = 4.0;

varying vec3 v_wpos;
varying vec3 v_wnrm;
varying vec3 v_tpos;
varying vec3 v_tw;

void vertex() {
	v_wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_wnrm = normalize(mat3(MODEL_MATRIX) * NORMAL);
	if (vertex_color) {
		COLOR.rgb = mix(pow((COLOR.rgb + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), COLOR.rgb * (1.0 / 12.92), lessThan(COLOR.rgb, vec3(0.04045)));
	}
	if (mapping == 0) {
		UV = UV * uv_scale.xy;
	} else {
		TANGENT = normalize(vec3(0.0, 0.0, -1.0) * abs(NORMAL.x) + vec3(1.0, 0.0, 0.0) * abs(NORMAL.y) + vec3(1.0, 0.0, 0.0) * abs(NORMAL.z));
		BINORMAL = normalize(vec3(0.0, 1.0, 0.0) * abs(NORMAL.x) + vec3(0.0, 0.0, -1.0) * abs(NORMAL.y) + vec3(0.0, 1.0, 0.0) * abs(NORMAL.z));
		vec3 n = mapping == 2 ? v_wnrm : NORMAL;
		v_tw = abs(n);
		v_tw /= dot(v_tw, vec3(1.0));
		v_tpos = (mapping == 2 ? v_wpos : VERTEX) * uv_scale * vec3(1.0, -1.0, 1.0);
	}
}

float h21(vec2 p) {
	p = fract(p * vec2(0.1031, 0.1030));
	p += dot(p, p.yx + 33.33);
	return fract((p.x + p.y) * p.x);
}

// Pièce derrière une vitre : rayon de vue dans le repère de la travée (x le long du mur, y vers le haut, z vers l'intérieur,
// en mètres), point d'entrée (px, py) ; renvoie la couleur vue et, en .a, 1 si le plafond (lampe) est touché.
vec4 room(vec3 r, vec2 p, vec2 cell, float id) {
	vec2 c = cell_m;
	float tx = r.x > 0.0 ? (c.x - p.x) / r.x : -p.x / min(r.x, -1e-4);
	float ty = r.y > 0.0 ? (c.y - p.y) / r.y : -p.y / min(r.y, -1e-4);
	float tz = room_depth / max(r.z, 1e-3);
	float t = min(min(tx, ty), tz);
	vec3 h = vec3(p, 0.0) + r * t;
	// Teinte de la pièce : enduit pâle (beige, gris, bleuté, vert d'eau, rosé).
	vec3 wallc = mix(vec3(0.62, 0.58, 0.5), vec3(0.5, 0.55, 0.6), h21(cell + id));
	wallc = mix(wallc, vec3(0.55, 0.6, 0.52), step(0.7, h21(cell * 1.7 + id)));
	wallc = mix(wallc, vec3(0.64, 0.52, 0.5), step(0.85, h21(cell * 2.3 + id)));
	vec3 col;
	float ceil = 0.0;
	if (t == tz) {
		col = wallc * 0.9;
		// Mobilier le long du mur du fond : meuble bas, cadre ou étagère.
		float f = h21(cell * 3.1 + id);
		float u = h.x / c.x;
		if (h.y < 0.95 && abs(u - 0.3 - f * 0.4) < 0.22) col = mix(vec3(0.22, 0.16, 0.11), vec3(0.3, 0.3, 0.32), step(0.5, f)) * (0.8 + 0.2 * step(0.9, h.y));
		else if (h.y > 1.3 && h.y < 2.0 && abs(u - 0.7 + f * 0.3) < 0.1) col = vec3(0.2, 0.26, 0.3);
	} else if (t == tx) {
		col = wallc * 0.72;
		// Porte sur un mur de côté.
		float dz = h.z / room_depth;
		if (h.y < 2.1 && abs(dz - 0.7) < 0.12 && h21(cell + id + 5.0) > 0.5) col = vec3(0.3, 0.22, 0.15);
	} else if (r.y < 0.0) {
		// Sol : parquet ou moquette, plus sombre au fond.
		col = mix(vec3(0.3, 0.21, 0.13), vec3(0.28, 0.28, 0.3), step(0.5, h21(cell * 0.7 + id)));
		col *= 0.75 + 0.25 * step(0.5, fract(h.x * 1.6));
	} else {
		col = vec3(0.8, 0.79, 0.76);
		vec2 lc = h.xz - vec2(c.x * 0.5, room_depth * 0.5);
		ceil = step(abs(lc.x), 0.5) * step(abs(lc.y), 0.35);
		col = mix(col, vec3(1.0), ceil);
	}
	// Assombri vers le fond (la lumière vient de la fenêtre).
	col *= mix(1.0, 0.55, clamp(h.z / room_depth, 0.0, 1.0));
	return vec4(col, ceil);
}

vec4 tri(sampler2D s) {
	return texture(s, v_tpos.xy) * v_tw.z + texture(s, v_tpos.xz) * v_tw.y + texture(s, v_tpos.zy * vec2(-1.0, 1.0)) * v_tw.x;
}

void fragment() {
	vec4 a;
	vec3 nm;
	float r;
	vec3 em = vec3(0.0);
	if (mapping == 0) {
		a = texture(albedo_tex, UV);
		nm = texture(normal_tex, UV).rgb;
		r = texture(rough_tex, UV).r;
		if (emission_energy > 0.0) em = texture(emission_tex, UV).rgb;
	} else {
		a = tri(albedo_tex);
		nm = tri(normal_tex).rgb;
		r = tri(rough_tex).r;
		if (emission_energy > 0.0) em = tri(emission_tex).rgb;
	}
	vec3 col = albedo.rgb * a.rgb;
	if (vertex_color) col *= COLOR.rgb;

	// Repère des salissures : (horizontale le long du mur, hauteur) sur les murs, (x, z) au sol.
	vec3 n = normalize(v_wnrm);
	float wall = 1.0 - smoothstep(0.5, 0.85, abs(n.y));
	float hu = abs(n.x) > abs(n.z) ? v_wpos.z : v_wpos.x;
	vec2 wp = wall > 0.5 ? vec2(hu, v_wpos.y) : v_wpos.xz;
	vec4 g1 = texture(grime_tex, wp * 0.043);
	vec4 g2 = texture(grime_tex, wp * 0.17 + vec2(0.37, 0.61));
	float blot = g1.g * 0.6 + g2.g * 0.4;
	float st = texture(grime_tex, vec2(hu * 0.21, v_wpos.y * 0.017)).r * 0.6
		+ texture(grime_tex, vec2(hu * 0.53 + 0.3, v_wpos.y * 0.041 + 0.2)).r * 0.4;
	float mac = texture(grime_tex, v_wpos.xz * 0.0037 + vec2(0.13, 0.71)).b;

	float d = smoothstep(0.42, 0.95, blot) * 0.38;
	float wet = wall * streaks * smoothstep(0.38, 0.85, st);
	d += wet * 0.55;
	float base = wall * foot * (1.0 - smoothstep(0.0, 1.2 + 1.8 * g2.g, v_wpos.y));
	d += base * 0.6;
	d = clamp(d * grime, 0.0, 0.85);
	col *= mix(vec3(1.0), vec3(0.46, 0.43, 0.39), d);
	// Poussière grise sur les surfaces horizontales.
	float dust = (1.0 - wall) * smoothstep(0.5, 0.85, g2.b) * 0.22 * grime;
	col = mix(col, vec3(0.085, 0.08, 0.072), dust);
	// Rouille : coulures et taches brun-orangé ternes.
	float rs = rust * clamp(smoothstep(0.55, 0.85, blot) + wet * 0.6 + base * 0.5, 0.0, 1.0);
	col = mix(col, vec3(0.09, 0.045, 0.022) * (0.7 + 0.6 * g2.r), rs);
	col *= mix(1.0, 0.78 + 0.36 * mac, macro_var);
	col = mix(col, vec3(dot(col, vec3(0.299, 0.587, 0.114))), desat);

	float rr = roughness * r;
	rr = mix(rr, rr * 0.62, wet * grime);
	rr = mix(rr, 0.95, rs);
	// Flaques : sombres, lisses, sans relief.
	// Masque tourné et déformé : contours sinueux, sans alignement sur la grille du bruit.
	vec2 pw = mat2(vec2(0.88, 0.47), vec2(-0.47, 0.88)) * wp * 0.037 + (g2.rg - 0.5) * 0.08;
	float pa = texture(grime_tex, pw).a;
	// Par temps sec, seuil relevé : quelques petites flaques franches (et non des taches translucides partout).
	float pth = 0.6 + (1.0 - wetness) * 0.3;
	float pud = puddles * (1.0 - wall) * step(0.0, n.y) * smoothstep(pth, pth + 0.05, pa + (g2.a - 0.5) * 0.14);
	col = mix(col, col * 0.42, pud);
	rr = mix(rr, 0.04, pud);
	nm = mix(nm, vec3(0.5, 0.5, 1.0), pud);

	// Pièces derrière les vitres : la vitre ne garde que son reflet (Fresnel), la pièce vue en perspective est ajoutée
	// en émission (elle ne reçoit ni soleil ni ombre de la façade), faible de jour, éclairée la nuit si la fenêtre l'est.
	vec3 room_em = vec3(0.0);
	// Repère de la façade par dérivées écran (aucune tangente de maillage) : calculées pour tous les pixels, avant tout
	// branchement (dans un flot non uniforme, dFdx et dFdy sont indéfinis : points noirs au bord des fenêtres).
	vec2 uvm = UV / uv_scale.xy;
	vec3 dpx = dFdx(v_wpos), dpy = dFdy(v_wpos);
	vec2 dux = dFdx(uvm), duy = dFdy(uvm);
	if (interior > 0 && mapping == 0 && r < 0.3) {
		float fx = uvm.x * win_rect.x;
		vec2 cell = vec2(floor(fx), floor(uvm.y));
		vec2 l = vec2(fract(fx), fract(uvm.y));
		bool inwin = interior == 2
			? (l.x > 0.035 && l.x < 0.965 && l.y < 0.775)
			: (abs(l.x - 0.5) < win_rect.y && l.y > win_rect.z && l.y < win_rect.w);
		if (inwin) {
			// Axes U et V (V descend) de la façade en monde.
			float det = dux.x * duy.y - dux.y * duy.x;
			if (abs(det) > 1e-12) {
				vec3 tu = normalize((dpx * duy.y - dpy * dux.y) / det);
				vec3 tv = normalize((dpy * dux.x - dpx * duy.x) / det);
				vec3 rd = normalize(v_wpos - CAMERA_POSITION_WORLD);
				vec3 rl = vec3(dot(rd, tu), -dot(rd, tv), -dot(rd, n));
				if (rl.z > 0.02) {
					vec2 p = vec2(l.x * cell_m.x, (1.0 - l.y) * cell_m.y);
					// Identifiant d'immeuble : sa teinte (couleur de sommet), sinon la même série de pièces partout.
					vec4 rm = room(rl, p, cell, vertex_color ? dot(COLOR.rgb, vec3(13.1, 7.7, 3.3)) : 0.0);
					float fres = 0.18 + 0.6 * pow(1.0 - clamp(rl.z, 0.0, 1.0), 4.0);
					float lit = dot(em, vec3(0.33));
					vec3 inside = rm.rgb * (0.22 + lit * 1.8) + vec3(1.0, 0.92, 0.75) * rm.a * lit * 2.5;
					room_em = inside * (1.0 - fres);
					em *= 0.35; // la lueur plate de la fenêtre éclairée cède la place à la pièce
					col *= fres + 0.25;
				}
			}
		}
	}

	ALBEDO = col;
	ROUGHNESS = clamp(rr, 0.02, 1.0);
	METALLIC = metallic * (1.0 - rs) * (1.0 - pud);
	SPECULAR = specular;
	NORMAL_MAP = nm;
	NORMAL_MAP_DEPTH = normal_depth;
	// Lueur par plaques (paroi de ruche) : masque de variation lente en coordonnées monde (0 : partout).
	if (emission_patch > 0.0) em *= smoothstep(0.5, 0.72, texture(grime_tex, wp * emission_patch + vec2(0.41, 0.17)).b);
	EMISSION = em * emission_energy + room_em;
}
";

    private static Shader _weathered;
    private static ImageTexture _grime;

    /// <summary>
    /// Texture de salissures partagée (512², périodique) : R = coulures (étirées verticalement), G = taches,
    /// B = variation lente, A = masque de flaques.
    /// </summary>
    private static ImageTexture GrimeTex()
    {
        if (_grime != null) return _grime;
        const int s = 512;
        var px = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var streak = VNoise(x, y, 4, 64, s, 901) * 0.5f + VNoise(x, y, 8, 128, s, 902) * 0.3f + VNoise(x, y, 2, 16, s, 903) * 0.2f;
                streak = Mathf.Clamp((streak - 0.5f) * 1.8f + 0.5f, 0, 1);
                var blot = ValueNoise(x, y, 64, s / 64, 911) * 0.4f + ValueNoise(x, y, 32, s / 32, 912) * 0.25f + ValueNoise(x, y, 16, s / 16, 913) * 0.17f
                    + ValueNoise(x, y, 8, s / 8, 914) * 0.1f + ValueNoise(x, y, 4, s / 4, 915) * 0.08f;
                blot = Mathf.Clamp((blot - 0.5f) * 1.6f + 0.5f, 0, 1);
                var slow = ValueNoise(x, y, 128, s / 128, 921) * 0.65f + ValueNoise(x, y, 64, s / 64, 922) * 0.35f;
                var pud = ValueNoise(x, y, 128, s / 128, 931) * 0.4f + ValueNoise(x, y, 64, s / 64, 932) * 0.3f + ValueNoise(x, y, 32, s / 32, 934) * 0.18f + ValueNoise(x, y, 16, s / 16, 933) * 0.12f;
                px.Set(x, y, new Color(streak, blot, slow, pud));
            }
        });
        _grime = ImageTexture.CreateFromImage(px.ToImage());
        return _grime;
    }

    /// <summary>Bruit de valeur anisotrope (cellules cx × cy pixels), périodique sur <paramref name="size"/> pixels.</summary>
    private static float VNoise(float x, float y, int cx, int cy, int size, int seed)
    {
        var fx = x / cx;
        var fy = y / cy;
        var ix = Mathf.FloorToInt(fx);
        var iy = Mathf.FloorToInt(fy);
        var tx = fx - ix;
        var ty = fy - iy;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        int px = size / cx, py = size / cy;
        float H(int i, int j) => Hash(((i % px) + px) % px, ((j % py) + py) % py, seed);
        var a = Mathf.Lerp(H(ix, iy), H(ix + 1, iy), tx);
        var b = Mathf.Lerp(H(ix, iy + 1), H(ix + 1, iy + 1), tx);
        return Mathf.Lerp(a, b, ty);
    }

    /// <summary>Matériau « usé » (voir le shader ci-dessus) ; les réglages se font par paramètres de shader.</summary>
    private static ShaderMaterial Weathered(Texture2D albedoTex, Texture2D normalTex = null, Texture2D roughTex = null, int mapping = 0, Vector3? uvScale = null)
    {
        _weathered ??= new Shader { Code = WeatheredCode };
        var m = new ShaderMaterial { Shader = _weathered };
        if (albedoTex != null) m.SetShaderParameter("albedo_tex", albedoTex);
        if (normalTex != null) m.SetShaderParameter("normal_tex", normalTex);
        if (roughTex != null) m.SetShaderParameter("rough_tex", roughTex);
        m.SetShaderParameter("grime_tex", GrimeTex());
        m.SetShaderParameter("mapping", mapping);
        m.SetShaderParameter("uv_scale", uvScale ?? Vector3.One);
        return m;
    }

    private static ShaderMaterial With(this ShaderMaterial m, string p, Variant v)
    {
        m.SetShaderParameter(p, v);
        return m;
    }

    /// <summary>
    /// Tôle ondulée peinte (entrepôts, conteneurs), UV en mètres / 3 : ondes verticales, coulures de rouille depuis
    /// les rivets et le bas des tôles, peinture écaillée, bosses ; teinte par sommet (immeuble).
    /// </summary>
    public static ShaderMaterial Corrugated(Color c, int seed, float uScale)
    {
        var key = $"corrugated{c}{seed}{uScale}";
        if (TryCache<ShaderMaterial>(key, out var cached)) return cached;
        const int w = 128, h = 256;
        var alb = new Px(w, h);
        var rough = new Px(w, h, 1);
        var bump = new Px(w, h);
        var rustCol = new Color(0.25f, 0.14f, 0.08f);
        System.Threading.Tasks.Parallel.For(0, h, y =>
        {
        for (var x = 0; x < w; x++)
        {
            var wave = 0.5f + 0.5f * Mathf.Sin(x / (float)w * Mathf.Tau * 8f);
            var ly = y / (float)h;
            var grain = ValueNoise(x, y, 4, 32, seed + 1) * 0.5f + Hash(x, y, seed + 2) * 0.5f;
            var drip = VNoise(x, y, 2, 64, w, seed + 3) * 0.6f + VNoise(x, y, 4, 128, w, seed + 4) * 0.4f;
            // Rouille : coulures sous la ligne de rivets du haut, et remontée depuis le bas de la tôle.
            var rust = Mathf.Clamp((drip - 0.5f) * 2.5f, 0, 1) * Mathf.Exp(-ly * 3f) * 0.8f
                + Mathf.Clamp((ValueNoise(x, y, 16, w / 16, seed + 5) - 0.45f) * 2f, 0, 1) * Mathf.SmoothStep(0.6f, 1f, ly) * 0.9f;
            // Peinture écaillée (métal gris apparent).
            var chip = ValueNoise(x, y, 8, w / 8, seed + 6) * 0.6f + ValueNoise(x, y, 2, w / 2, seed + 7) * 0.4f > 0.7f;
            var col = c * (0.85f + wave * 0.15f) * (0.9f + grain * 0.2f);
            if (chip) col = new Color(0.3f, 0.3f, 0.3f) * (0.8f + grain * 0.3f);
            col = col.Lerp(rustCol * (0.8f + grain * 0.4f), Mathf.Clamp(rust, 0, 1));
            alb.Set(x, y, col);
            rough.Set(x, y, Mathf.Lerp(chip ? 0.5f : 0.65f, 0.95f, Mathf.Clamp(rust, 0, 1)));
            var dent = ValueNoise(x, y, 32, w / 32, seed + 8) * 0.25f;
            bump.Set(x, y, new Color(wave * 0.75f + dent, wave * 0.75f + dent, wave * 0.75f + dent));
        }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 4f), ImageTexture.CreateFromImage(rough.ToImage()), 0, new Vector3(uScale, 1f, 1f))
            .With("roughness", 1f).With("metallic", 0.35f).With("normal_depth", 0.65f).With("vertex_color", true).With("rust", 0.35f).With("desat", 0.15f);
        Cache[key] = mat;
        return mat;
    }

    /// <summary>
    /// Dalles de béton d'un terre-plein portuaire (8 m, dalles de 4 m) : joints de dilatation goudronnés, teinte
    /// par dalle, fissures d'angle, épaufrures, taches d'huile et de rouille.
    /// </summary>
    public static ShaderMaterial Apron()
    {
        if (TryCache<ShaderMaterial>("apron", out var cached)) return cached;
        const int s = 512, slab = 256;
        var alb = new Px(s, s);
        var rough = new Px(s, s, 1);
        var bump = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var sx = x / slab;
                var sy = y / slab;
                var lx = (x % slab) / (float)slab;
                var ly = (y % slab) / (float)slab;
                var grain = Hash(x, y, 701);
                var n = ValueNoise(x, y, 8, s / 8, 702) * 0.5f + ValueNoise(x, y, 32, s / 32, 703) * 0.5f;
                var tone = 0.44f + (Hash(sx, sy, 704) - 0.5f) * 0.08f + (n - 0.5f) * 0.1f + (grain - 0.5f) * 0.05f;
                var hgt = 0.7f + grain * 0.06f + (n - 0.5f) * 0.1f;
                var r = 0.9f;
                // Fissure d'angle (dalle cassée) sur une dalle sur deux environ.
                if (Hash(sx, sy, 705) < 0.6f)
                {
                    var a = Hash(sx, sy, 706) * 0.6f + 0.2f;
                    var d = Mathf.Abs(lx * a + ly * (1 - a) - 0.5f - (Hash(sx, sy, 707) - 0.5f) * 0.3f + (ValueNoise(x, y, 6, s / 6, 708) - 0.5f) * 0.03f);
                    if (d < 0.004f) { tone = 0.12f; hgt = 0.3f; r = 1f; }
                }
                // Épaufrures le long des joints.
                var edge = Mathf.Min(Mathf.Min(lx, 1 - lx), Mathf.Min(ly, 1 - ly)) * slab;
                if (edge < 6 && ValueNoise(x, y, 4, s / 4, 709) > 0.68f) { tone *= 0.7f; hgt = 0.45f; }
                // Joint goudronné.
                if (edge < 2.2f) { tone = 0.08f; hgt = 0.35f; r = 0.6f; }
                // Taches d'huile et coulures de rouille.
                var oil = ValueNoise(x, y, 16, s / 16, 710) * 0.65f + ValueNoise(x, y, 4, s / 4, 711) * 0.35f;
                var col = new Color(tone, tone * 0.985f, tone * 0.955f);
                if (oil > 0.73f) { var k = Mathf.Clamp((oil - 0.73f) * 7f, 0, 1); col = col.Lerp(new Color(0.07f, 0.065f, 0.06f), k * 0.75f); r = Mathf.Lerp(r, 0.4f, k); }
                var rust = ValueNoise(x, y, 32, s / 32, 712);
                if (rust > 0.72f) col = col.Lerp(new Color(0.24f, 0.15f, 0.09f), (rust - 0.72f) * 2.2f);
                alb.Set(x, y, col);
                rough.Set(x, y, r);
                bump.Set(x, y, new Color(hgt, hgt, hgt));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 4f), ImageTexture.CreateFromImage(rough.ToImage()), 2, Vector3.One / 8f)
            .With("roughness", 1f).With("normal_depth", 0.9f).With("puddles", 0.8f).With("grime", 0.8f);
        Cache["apron"] = mat;
        return mat;
    }

    /// <summary>Normal map à partir d'une carte de hauteur (avec mipmaps).</summary>
    private static ImageTexture BumpToNormal(Px bump, float strength)
    {
        var img = bump.ToImage(false);
        img.BumpMapToNormalMap(strength);
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }
}
