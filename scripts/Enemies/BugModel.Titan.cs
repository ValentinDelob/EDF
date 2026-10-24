using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Titan reptilien (spécification §10.2) : kaiju bipède d'environ 60 m, posture semi-dressée, bassin massif, longue
/// queue, cou épais, crâne lourd à arcades saillantes. Peau en écailles (shader à cellules de Voronoï en UV métriques),
/// rangée dorsale d'épines dentelées, rangées latérales d'ostéodermes, huit plaques d'armure osseuses (épaules, dos,
/// flancs, hanches) à briser : sous chacune, une chair à vif incandescente (point faible), cachée tant que la plaque tient.
///
/// Un seul maillage à os (43 os, deux os par sommet pour les plis de la queue, du cou et des membres), construit une fois
/// et partagé ; l'armature vit côté C# (voir BugModel.Titan.Anim.cs) et n'envoie au serveur de rendu que des matrices.
/// Pose de liaison : chaque os a une base identité et son pivot pour origine (matrice envoyée = pose · T(-pivot)).
/// </summary>
public partial class BugModel
{
    // ---------------- Gabarit (repère du modèle, avant = +Z, mètres) ----------------

    /// <summary>Échelle du gabarit (dessiné à 56 m, porté à ~60 m).</summary>
    private const float TS = 1.08f;

    /// <summary>Échelle du crâne (un peu plus massif que le gabarit).</summary>
    private const float THS = TS * 1.12f;

    private static Vector3 TP(float x, float y, float z) => new Vector3(x, y, z) * TS;

    /// <summary>Point du crâne (dessiné pour une base du crâne en (0, 50, 22,5), décalé avec elle).</summary>
    private static Vector3 TH(float x, float y, float z) => TJ[14] + (new Vector3(x, y, z) - new Vector3(0, 50f, 22.5f)) * (TS * 1.12f);

    /// <summary>Articulations de la chaîne principale, du bout de la queue à la base du crâne.</summary>
    private static readonly Vector3[] TJ =
    {
        TP(0, 2.5f, -66f), TP(0, 3.6f, -59f), TP(0, 5.2f, -51f), TP(0, 7.3f, -43.5f), TP(0, 9.8f, -36f),
        TP(0, 12.6f, -29f), TP(0, 15.6f, -22.5f), TP(0, 18.6f, -16.5f), TP(0, 21.4f, -10.5f),
        TP(0, 25f, -1f), TP(0, 29.3f, 7.5f), TP(0, 34.6f, 14f), TP(0, 40.8f, 18.6f), TP(0, 45.6f, 22.6f), TP(0, 49.2f, 26f),
    };

    // Rayons de la section (latéral, dorsal) et affaissement du ventre à chaque point de contrôle (+ prolongement dans le crâne).
    private static readonly float[] TRx = { 0.35f, 1.0f, 1.7f, 2.5f, 3.3f, 4.1f, 4.9f, 5.7f, 6.6f, 7.8f, 8.0f, 7.9f, 6.2f, 5.0f, 4.1f, 3.4f };
    private static readonly float[] TRy = { 0.35f, 1.15f, 1.95f, 2.9f, 3.8f, 4.7f, 5.6f, 6.5f, 7.3f, 8.4f, 9.0f, 9.4f, 7.2f, 5.6f, 4.4f, 3.6f };
    private static readonly float[] TSag = { 0f, 0f, 0f, 0.02f, 0.04f, 0.05f, 0.06f, 0.07f, 0.07f, 0.07f, 0.09f, 0.08f, 0.06f, 0.04f, 0.02f, 0f };

    // Os.
    private const int TBPelvis = 0, TBBelly = 1, TBChest = 2, TBNeck1 = 3, TBNeck2 = 4, TBHead = 5, TBJaw = 6, TBTail = 7;
    private const int TBLeg = 15, TBArm = 21, TBPlate = 27, TBWound = 35, TBCount = 43, TTailN = 8, TPlates = 8;

    private static int TThigh(int leg) => TBLeg + leg * 3;
    private static int TShin(int leg) => TBLeg + leg * 3 + 1;
    private static int TFoot(int leg) => TBLeg + leg * 3 + 2;
    private static int TUpper(int arm) => TBArm + arm * 3;
    private static int TFore(int arm) => TBArm + arm * 3 + 1;
    private static int THand(int arm) => TBArm + arm * 3 + 2;
    private static float TSide(int i) => i == 0 ? 1f : -1f;

    // Membres (côté 0 = droite, +X).
    private static Vector3 TiHip(int i) => TP(7.6f * TSide(i), 22.5f, -1.5f);
    private static Vector3 TiKnee(int i) => TP(8.4f * TSide(i), 12f, 4.5f);
    private static Vector3 TiAnkle(int i) => TP(8.0f * TSide(i), 4.6f, -2.6f);
    private static Vector3 TiBall(int i) => TP(8.0f * TSide(i), 1.3f, 1.6f);
    /// <summary>Point d'appui du pied au sol (pose de liaison).</summary>
    private static Vector3 TiSole(int i) => TP(8.0f * TSide(i), 0f, 0.8f);
    private static Vector3 TiShoulder(int i) => TP(6.0f * TSide(i), 36.1f, 17f);
    private static Vector3 TiElbow(int i) => TP(8.2f * TSide(i), 30.2f, 20.6f);
    private static Vector3 TiWrist(int i) => TP(7.0f * TSide(i), 26.6f, 25.2f);
    private static readonly Vector3 TJawPivot = TH(0, 48.4f, 24.8f);
    /// <summary>Sortie du souffle, entre les mâchoires (repère de liaison du crâne).</summary>
    private static readonly Vector3 TMouth = TH(0, 48.4f, 33.3f);
    /// <summary>Centre du crâne (forme de collision de la tête).</summary>
    private static readonly Vector3 TSkull = TH(0, 51f, 27.5f);

    /// <summary>Hauteur du centre du torse (cible des alliés, centre de l'ennemi).</summary>
    public const float TitanCenterY = 32f * TS;

    /// <summary>Plaque d'armure : plage le long du corps (abscisses en m) et autour (angle depuis le dos, vers +X).</summary>
    private readonly record struct TitanPlateDef(float S0, float S1, float A0, float A1, float Thick);

    /// <summary>Données partagées du modèle (construites une fois).</summary>
    private sealed class TitanShared
    {
        public ArrayMesh Mesh;
        public readonly ArrayMesh[] Debris = new ArrayMesh[TPlates];
        public readonly Vector3[] PlateCenter = new Vector3[TPlates], PlateNormal = new Vector3[TPlates], PlateSkin = new Vector3[TPlates];
        public readonly float[] PlateRadius = new float[TPlates];
        public readonly int[] PlateParent = new int[TPlates];
        public readonly Vector3[] Pivot = new Vector3[TBCount];
        public int Triangles;
    }

    private static TitanShared _titan;

    // ---------------- Ligne médiane (spline) et section ----------------

    private static Vector3[] _tCurve;
    private static float[] _tTabU, _tTabS;
    private static float[] _tJointS;
    private static float _tLen;

    private static Vector3 TCat(Vector3[] p, float u)
    {
        var n = p.Length;
        u = Mathf.Clamp(u, 0f, n - 1.0001f);
        var i = (int)u;
        var t = u - i;
        var p0 = p[Math.Max(i - 1, 0)];
        var p1 = p[i];
        var p2 = p[Math.Min(i + 1, n - 1)];
        var p3 = p[Math.Min(i + 2, n - 1)];
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
    }

    private static float TCatF(float[] k, float u)
    {
        var n = k.Length;
        u = Mathf.Clamp(u, 0f, n - 1.0001f);
        var i = (int)u;
        var t = u - i;
        var p0 = k[Math.Max(i - 1, 0)];
        var p1 = k[i];
        var p2 = k[Math.Min(i + 1, n - 1)];
        var p3 = k[Math.Min(i + 2, n - 1)];
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
    }

    /// <summary>Table abscisse curviligne ↔ paramètre de la spline (une fois).</summary>
    private static void TitanCurve()
    {
        if (_tCurve != null) return;
        var pts = new List<Vector3>(TJ) { TJ[^1] + (TJ[^1] - TJ[^2]).Normalized() * 3.2f * TS };
        _tCurve = pts.ToArray();
        const int per = 80;
        var n = (_tCurve.Length - 1) * per + 1;
        _tTabU = new float[n];
        _tTabS = new float[n];
        var prev = TCat(_tCurve, 0f);
        var s = 0f;
        for (var i = 0; i < n; i++)
        {
            var u = i / (float)per;
            var p = TCat(_tCurve, u);
            s += p.DistanceTo(prev);
            prev = p;
            _tTabU[i] = u;
            _tTabS[i] = s;
        }
        _tLen = s;
        _tJointS = new float[_tCurve.Length];
        for (var k = 0; k < _tCurve.Length; k++) _tJointS[k] = _tTabS[Math.Min(k * per, n - 1)];
    }

    private static float TU(float s)
    {
        s = Mathf.Clamp(s, 0f, _tLen);
        int lo = 0, hi = _tTabS.Length - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (_tTabS[mid] < s) lo = mid; else hi = mid;
        }
        var d = _tTabS[hi] - _tTabS[lo];
        var t = d > 1e-6f ? (s - _tTabS[lo]) / d : 0f;
        return Mathf.Lerp(_tTabU[lo], _tTabU[hi], t);
    }

    /// <summary>Section du corps à l'abscisse s : centre, axe latéral, axe dorsal, demi-rayons.</summary>
    private static TitanMesh.Ring TRing(float s)
    {
        var u = TU(s);
        var c = TCat(_tCurve, u);
        var tan = (TCat(_tCurve, u + 0.02f) - TCat(_tCurve, u - 0.02f)).Normalized();
        var x = Vector3.Right;
        var y = tan.Cross(x).Normalized();
        var rx = TCatF(TRx, u) * TS;
        var ry = TCatF(TRy, u) * TS;
        c -= y * (ry * TCatF(TSag, u));
        return new TitanMesh.Ring(c, x, y, rx, ry);
    }

    private static Vector3 TTangent(float s)
    {
        var u = TU(s);
        return (TCat(_tCurve, u + 0.02f) - TCat(_tCurve, u - 0.02f)).Normalized();
    }

    private const float TSq = 2.25f;

    /// <summary>Relief musculaire : arête dorsale, anneaux de la queue, plis du cou, renflement des flancs.</summary>
    private static float TMod(float s, float th)
    {
        var a = Mathf.Wrap(th, -Mathf.Pi, Mathf.Pi);
        var m = 1f + 0.06f * Mathf.Exp(-a * a / 0.05f);
        var sNeck = _tJointS[11];
        var sTail = _tJointS[8];
        if (s < sTail) m *= 1f + 0.018f * Mathf.Sin(s * 2.1f) * Mathf.Clamp((sTail - s) / 10f, 0, 1);
        if (s > sNeck) m *= 1f - 0.022f * Mathf.Max(0f, Mathf.Cos(s * 1.9f)) * Mathf.Clamp((s - sNeck) / 4f, 0, 1);
        // Flancs gonflés (côtes et ventre) entre le bassin et la poitrine.
        var flank = Mathf.Exp(-Mathf.Pow((s - _tJointS[10]) / 9f, 2)) * Mathf.Pow(Mathf.Abs(Mathf.Sin(a)), 2f);
        return m * (1f + 0.05f * flank);
    }

    private static Vector3 TBody(float s, float th, float lift = 0f)
    {
        var r = TRing(s);
        var e = 2f / TSq;
        var sn = Mathf.Sin(th);
        var cs = Mathf.Cos(th);
        var m = TMod(s, th);
        var p = r.C + r.X * (r.Rx * Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), e) * m) + r.Y * (r.Ry * Mathf.Sign(cs) * Mathf.Pow(Mathf.Abs(cs), e) * m);
        return lift == 0f ? p : p + TBodyNormal(s, th) * lift;
    }

    private static Vector3 TBodyNormal(float s, float th)
    {
        const float ds = 0.25f, dt = 0.02f;
        var a = TBody(s + ds, th) - TBody(s - ds, th);
        var b = TBody(s, th + dt) - TBody(s, th - dt);
        var n = a.Cross(b).Normalized();
        return n.Dot(TBody(s, th) - TRing(s).C) < 0 ? -n : n;
    }

    // ---------------- Poids ----------------

    /// <summary>Os de la chaîne principale classés le long du corps, et l'abscisse de leur centre.</summary>
    private static (int Bone, float S)[] _tChain;

    private static void TitanChain()
    {
        if (_tChain != null) return;
        var l = new List<(int, float)>();
        for (var k = TTailN - 1; k >= 0; k--) l.Add((TBTail + k, 0.5f * (_tJointS[8 - k] + _tJointS[7 - k])));
        l.Add((TBPelvis, _tJointS[9]));
        l.Add((TBBelly, 0.5f * (_tJointS[10] + _tJointS[11])));
        l.Add((TBChest, 0.5f * (_tJointS[11] + _tJointS[12])));
        l.Add((TBNeck1, 0.5f * (_tJointS[12] + _tJointS[13])));
        l.Add((TBNeck2, 0.5f * (_tJointS[13] + _tJointS[14])));
        l.Add((TBHead, _tJointS[14] + 3f * TS));
        _tChain = l.ToArray();
    }

    private static (int, int, float) TBodyWeight(float s)
    {
        var c = _tChain;
        if (s <= c[0].S) return (c[0].Bone, c[0].Bone, 0f);
        for (var i = 0; i < c.Length - 1; i++)
        {
            if (s > c[i + 1].S) continue;
            var t = (s - c[i].S) / Mathf.Max(c[i + 1].S - c[i].S, 1e-3f);
            return (c[i].Bone, c[i + 1].Bone, Mathf.Clamp(t, 0f, 1f));
        }
        return (c[^1].Bone, c[^1].Bone, 0f);
    }

    /// <summary>Abscisse du point de la ligne médiane le plus proche (poids des pièces posées sur le corps).</summary>
    private static float TNearestS(Vector3 p)
    {
        var best = 0f;
        var bd = float.MaxValue;
        for (var i = 0; i < _tTabS.Length; i += 4)
        {
            var d = TCat(_tCurve, _tTabU[i]).DistanceSquaredTo(p);
            if (d < bd) { bd = d; best = _tTabS[i]; }
        }
        return best;
    }

    /// <summary>Poids le long d'un segment de membre : mélange avec l'os précédent près de a, avec le suivant près de b.</summary>
    private static Func<Vector3, (int, int, float)> TSegWeight(Vector3 a, Vector3 b, int bone, int prev, int next, float blendA, float blendB)
    {
        var d = b - a;
        var len2 = Mathf.Max(d.LengthSquared(), 1e-4f);
        return p =>
        {
            var u = (p - a).Dot(d) / len2;
            if (prev >= 0 && u < blendA) return (bone, prev, 0.5f * (1f - Mathf.Max(u, 0f) / blendA));
            if (next >= 0 && u > 1f - blendB) return (bone, next, 0.5f * Mathf.Min((u - (1f - blendB)) / blendB, 1f));
            return (bone, bone, 0f);
        };
    }

    // ---------------- Couleurs ----------------

    private static FastNoiseLite _tNoise;

    private static float TNoise(Vector3 p, float f) => (_tNoise ??= new FastNoiseLite { Seed = 61, Frequency = 1f, FractalOctaves = 3 }).GetNoise3D(p.X * f, p.Y * f, p.Z * f);

    private static Color TLin(float r, float g, float b) => new Color(r, g, b).SrgbToLinear();

    /// <summary>
    /// Peau : dos presque noir, flancs ardoise olivâtre marbrés, ventre gris sable rayé d'écailles transversales,
    /// d'après la normale approchée <paramref name="n"/> au point <paramref name="p"/>.
    /// </summary>
    private static Color THide(Vector3 p, Vector3 n, float scutes = 0f)
    {
        var dorsal = TLin(0.075f, 0.08f, 0.075f);
        var flank = TLin(0.15f, 0.155f, 0.135f);
        var belly = TLin(0.30f, 0.275f, 0.235f);
        var up = n.Y;
        var c = up > 0.35f ? flank.Lerp(dorsal, Mathf.Clamp((up - 0.35f) / 0.5f, 0, 1)) : belly.Lerp(flank, Mathf.Clamp((up + 0.55f) / 0.9f, 0, 1));
        // Marbrure : taches sombres à deux échelles, quelques zones plus claires (vieilles cicatrices).
        var m1 = TNoise(p, 0.045f);
        var m2 = TNoise(p + new Vector3(31, 7, 3), 0.16f);
        c *= 0.82f + 0.3f * Mathf.Clamp(m1 * 1.4f + 0.5f, 0, 1);
        c *= 0.9f + 0.18f * Mathf.Clamp(m2 * 1.6f + 0.5f, 0, 1);
        if (m2 > 0.45f) c = c.Lerp(TLin(0.32f, 0.3f, 0.27f), (m2 - 0.45f) * 1.2f);
        // Écailles ventrales transversales : sillons sombres réguliers.
        if (scutes > 0f && up < -0.2f)
        {
            var band = Mathf.Abs(Mathf.Sin(scutes * Mathf.Pi));
            c *= 1f - 0.45f * Mathf.Clamp((-up - 0.2f) * 2f, 0, 1) * Mathf.Pow(1f - band, 6f);
        }
        return new Color(c.R, c.G, c.B);
    }

    private static Color TBoneCol(Vector3 p, float wear)
    {
        var dark = TLin(0.07f, 0.065f, 0.058f);
        var mid = TLin(0.19f, 0.175f, 0.15f);
        var light = TLin(0.38f, 0.355f, 0.31f);
        var n = TNoise(p, 0.6f) * 0.5f + 0.5f;
        var c = wear < 0.5f ? dark.Lerp(mid, wear * 2f) : mid.Lerp(light, (wear - 0.5f) * 2f);
        return c * (0.85f + 0.3f * n);
    }

    // ---------------- Matériaux et textures ----------------

    private const string TitanSkinCode = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D scale_tex : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D scale_nrm : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float tile = 1.0;
uniform float depth = 1.0;
uniform float crevice = 0.6;
uniform float rough = 0.78;
uniform float spec = 0.35;
uniform float cell_var = 0.35;
uniform vec3 glow_col : source_color = vec3(1.0, 0.3, 0.04);
instance uniform float charge = 0.0;

varying float v_glow;

void vertex() {
	v_glow = UV2.y;
}

void fragment() {
	vec2 uv = UV * tile;
	vec4 s = texture(scale_tex, uv);
	float h = s.r;
	vec3 col = COLOR.rgb;
	col *= 1.0 - cell_var + 2.0 * cell_var * s.g;
	col *= mix(1.0 - crevice, 1.0, smoothstep(0.04, 0.5, h));
	col *= 0.92 + 0.16 * s.b;
	// Lueur de la gorge avant le souffle : le feu luit entre les écailles (sillons), les écailles restent sombres.
	float g = v_glow * charge;
	float crack = 1.0 - smoothstep(0.03, 0.42, h);
	ALBEDO = col * (1.0 - g * 0.55);
	NORMAL_MAP = texture(scale_nrm, uv).rgb;
	NORMAL_MAP_DEPTH = depth;
	ROUGHNESS = clamp(rough - h * 0.2 + s.b * 0.08, 0.15, 1.0);
	SPECULAR = spec;
	float flick = 4.5 + 1.3 * sin(TIME * 11.0 + uv.y * 5.0) + 0.8 * sin(TIME * 23.0 + uv.x * 7.0);
	EMISSION = glow_col * g * (crack * 3.0 + 0.08 + g * g * 0.18) * flick;
}";

    private const string TitanWoundCode = @"
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D scale_tex : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform sampler2D scale_nrm : hint_normal, filter_linear_mipmap, repeat_enable;

void fragment() {
	vec2 uv = UV * 1.7;
	vec4 s = texture(scale_tex, uv);
	vec4 s2 = texture(scale_tex, uv * 2.3 + vec2(0.31, 0.17));
	// Chair à vif : nodules sombres, veines incandescentes qui battent.
	float pulse = 0.62 + 0.38 * sin(TIME * 3.4 + s.g * 3.0);
	float vein = 1.0 - smoothstep(0.0, 0.22, s.r);
	float vein2 = 1.0 - smoothstep(0.0, 0.18, s2.r);
	vec3 flesh = mix(vec3(0.16, 0.012, 0.01), vec3(0.42, 0.05, 0.025), s2.g);
	ALBEDO = flesh * (0.7 + 0.5 * s.r);
	ROUGHNESS = 0.32 + 0.3 * s.b;
	SPECULAR = 0.65;
	NORMAL_MAP = texture(scale_nrm, uv).rgb;
	NORMAL_MAP_DEPTH = 1.8;
	float v = max(vein, vein2 * 0.7);
	EMISSION = mix(vec3(0.9, 0.12, 0.02), vec3(1.0, 0.5, 0.12), v) * (v * 4.0 + 0.35) * pulse;
}";

    private static ImageTexture _tScaleTex, _tScaleNrm;
    private static Shader _tSkinShader, _tWoundShader;
    private static ShaderMaterial _tHide, _tBone, _tWound;
    private static StandardMaterial3D _tEye;

    /// <summary>
    /// Écailles (256², périodique) : 8 × 8 cellules de Voronoï jitterées en quinconce. R = relief (bombé, plus épais vers le
    /// bas de l'écaille : recouvrement), G = teinte propre à l'écaille, B = grain fin ; relief converti en carte de normales.
    /// </summary>
    private static void TitanTextures()
    {
        if (_tScaleTex != null) return;
        const int s = 256, cells = 8;
        var cs = s / (float)cells;
        var rng = new Random(1606);
        var pts = new Vector2[cells * cells];
        var ids = new float[cells * cells];
        for (var y = 0; y < cells; y++)
        for (var x = 0; x < cells; x++)
        {
            var ox = (y % 2) * 0.5f;
            pts[y * cells + x] = new Vector2((x + 0.5f + ox + ((float)rng.NextDouble() - 0.5f) * 0.55f) * cs, (y + 0.5f + ((float)rng.NextDouble() - 0.5f) * 0.45f) * cs);
            ids[y * cells + x] = (float)rng.NextDouble();
        }
        var data = new byte[s * s * 4];
        var bump = new byte[s * s * 4];
        System.Threading.Tasks.Parallel.For(0, s, py =>
        {
            for (var px = 0; px < s; px++)
            {
                float f1 = 1e9f, f2 = 1e9f;
                var best = 0;
                var dyBest = 0f;
                for (var k = 0; k < pts.Length; k++)
                {
                    var dx = px + 0.5f - pts[k].X;
                    var dy = py + 0.5f - pts[k].Y;
                    dx -= Mathf.Round(dx / s) * s;
                    dy -= Mathf.Round(dy / s) * s;
                    var d = dx * dx + dy * dy;
                    if (d < f1) { f2 = f1; f1 = d; best = k; dyBest = dy; }
                    else if (d < f2) f2 = d;
                }
                var edge = (Mathf.Sqrt(f2) - Mathf.Sqrt(f1)) / cs;
                var h = Mathf.Sqrt(Mathf.Clamp(edge / 0.42f, 0f, 1f));
                // Recouvrement : l'écaille est plus épaisse vers son bord bas (sens de la croissance).
                h *= 0.62f + 0.5f * Mathf.Clamp(0.5f + dyBest / cs, 0f, 1f);
                var grit = PNoise(px, py, 4, 4, s, 1617) * 0.6f + PNoise(px, py, 2, 2, s, 1618) * 0.4f;
                var i = (py * s + px) * 4;
                data[i] = (byte)(Mathf.Clamp(h, 0, 1) * 255);
                data[i + 1] = (byte)(ids[best] * 255);
                data[i + 2] = (byte)(grit * 255);
                data[i + 3] = 255;
                var b = (byte)(Mathf.Clamp(h * 0.88f + grit * 0.12f, 0, 1) * 255);
                bump[i] = bump[i + 1] = bump[i + 2] = b;
                bump[i + 3] = 255;
            }
        });
        var img = Image.CreateFromData(s, s, false, Image.Format.Rgba8, data);
        img.GenerateMipmaps();
        _tScaleTex = ImageTexture.CreateFromImage(img);
        var bimg = Image.CreateFromData(s, s, false, Image.Format.Rgba8, bump);
        bimg.BumpMapToNormalMap(5f);
        bimg.GenerateMipmaps();
        _tScaleNrm = ImageTexture.CreateFromImage(bimg);
    }

    private static ShaderMaterial TSkinMat(float tile, float depth, float crevice, float rough, float spec, float cellVar)
    {
        TitanTextures();
        _tSkinShader ??= new Shader { Code = TitanSkinCode };
        var m = new ShaderMaterial { Shader = _tSkinShader };
        m.SetShaderParameter("scale_tex", _tScaleTex);
        m.SetShaderParameter("scale_nrm", _tScaleNrm);
        m.SetShaderParameter("tile", tile);
        m.SetShaderParameter("depth", depth);
        m.SetShaderParameter("crevice", crevice);
        m.SetShaderParameter("rough", rough);
        m.SetShaderParameter("spec", spec);
        m.SetShaderParameter("cell_var", cellVar);
        return m;
    }

    /// <summary>Peau écailleuse (couleurs par sommet).</summary>
    private static ShaderMaterial THideMat() => _tHide ??= TSkinMat(1f, 1.15f, 0.62f, 0.74f, 0.4f, 0.3f);

    /// <summary>Os et kératine (plaques, épines, dents, griffes) : grandes cellules fissurées, mat.</summary>
    private static ShaderMaterial TBoneMat() => _tBone ??= TSkinMat(0.45f, 1.6f, 0.5f, 0.88f, 0.25f, 0.18f);

    private static ShaderMaterial TWoundMat()
    {
        if (_tWound != null) return _tWound;
        TitanTextures();
        _tWoundShader ??= new Shader { Code = TitanWoundCode };
        _tWound = new ShaderMaterial { Shader = _tWoundShader };
        _tWound.SetShaderParameter("scale_tex", _tScaleTex);
        _tWound.SetShaderParameter("scale_nrm", _tScaleNrm);
        return _tWound;
    }

    private static StandardMaterial3D TEyeMat() => _tEye ??= new StandardMaterial3D
    {
        AlbedoColor = new Color(0.32f, 0.2f, 0.03f),
        Roughness = 0.08f,
        Metallic = 0.25f,
        ClearcoatEnabled = true,
        Clearcoat = 1f,
        EmissionEnabled = true,
        Emission = new Color(1f, 0.55f, 0.1f),
        EmissionEnergyMultiplier = 0.45f,
    };

    // ---------------- Construction ----------------

    private static TitanShared TitanBuild()
    {
        if (_titan != null) return _titan;
        TitanCurve();
        TitanChain();
        var sh = new TitanShared();
        // Pivots.
        sh.Pivot[TBPelvis] = TJ[9];
        sh.Pivot[TBBelly] = TJ[10];
        sh.Pivot[TBChest] = TJ[11];
        sh.Pivot[TBNeck1] = TJ[12];
        sh.Pivot[TBNeck2] = TJ[13];
        sh.Pivot[TBHead] = TJ[14];
        sh.Pivot[TBJaw] = TJawPivot;
        for (var k = 0; k < TTailN; k++) sh.Pivot[TBTail + k] = TJ[8 - k];
        for (var i = 0; i < 2; i++)
        {
            sh.Pivot[TThigh(i)] = TiHip(i);
            sh.Pivot[TShin(i)] = TiKnee(i);
            sh.Pivot[TFoot(i)] = TiAnkle(i);
            sh.Pivot[TUpper(i)] = TiShoulder(i);
            sh.Pivot[TFore(i)] = TiElbow(i);
            sh.Pivot[THand(i)] = TiWrist(i);
        }

        var m = new TitanMesh();
        BuildTitanBody(m);
        BuildTitanSpines(m);
        BuildTitanHead(m);
        for (var i = 0; i < 2; i++)
        {
            BuildTitanLeg(m, i);
            BuildTitanArm(m, i);
        }
        var plates = TitanPlates();
        for (var i = 0; i < TPlates; i++)
        {
            var d = plates[i];
            var sMid = 0.5f * (d.S0 + d.S1);
            var aMid = 0.5f * (d.A0 + d.A1);
            var c = TBody(sMid, aMid);
            var n = TBodyNormal(sMid, aMid);
            var (a, b, w) = TBodyWeight(sMid);
            var parent = w < 0.5f ? a : b;
            sh.Pivot[TBPlate + i] = sh.Pivot[parent];
            sh.Pivot[TBWound + i] = sh.Pivot[parent];
            sh.PlateParent[i] = parent;
            sh.PlateCenter[i] = c + n * d.Thick * 0.6f;
            sh.PlateNormal[i] = n;
            sh.PlateSkin[i] = c;
            var r0 = TBody(d.S0, aMid).DistanceTo(TBody(d.S1, aMid));
            var r1 = TBody(sMid, d.A0).DistanceTo(TBody(sMid, d.A1));
            sh.PlateRadius[i] = 0.5f * Mathf.Max(r0, r1) * 0.95f;
            var plateBone = TBPlate + i;
            m.Weight = _ => (plateBone, plateBone, 0f);
            BuildTitanPlate(m, d, false);
            var woundBone = TBWound + i;
            m.Weight = _ => (woundBone, woundBone, 0f);
            BuildTitanWound(m, d);
            // Débris : la même plaque, sans os, centrée sur son milieu.
            var deb = new TitanMesh { Rigid = true, Offset = c };
            BuildTitanPlate(deb, d, true);
            sh.Debris[i] = deb.Commit();
        }
        sh.Triangles = m.Triangles;
        sh.Mesh = m.Commit();
        _titan = sh;
        return sh;
    }

    /// <summary>Plaques : 0/1 épaules, 2/3 haut du dos, 4/5 flancs, 6/7 hanches (pair = droite).</summary>
    private static TitanPlateDef[] TitanPlates()
    {
        var j = _tJointS;
        var r = new List<TitanPlateDef>();
        void Pair(float s0, float s1, float a0, float a1, float thick)
        {
            r.Add(new TitanPlateDef(s0, s1, a0, a1, thick));
            r.Add(new TitanPlateDef(s0, s1, -a1, -a0, thick));
        }
        Pair(j[11] - 1.5f * TS, j[12] + 0.5f * TS, 0.62f, 1.62f, 2.3f * TS);
        Pair(j[10] - 2.0f * TS, j[11] - 2.0f * TS, 0.3f, 1.12f, 2.5f * TS);
        Pair(j[9] + 1.0f * TS, j[10] + 2.5f * TS, 1.18f, 2.05f, 2.1f * TS);
        Pair(j[8] - 1.0f * TS, j[9] - 0.5f * TS, 0.32f, 1.2f, 2.3f * TS);
        return r.ToArray();
    }

    /// <summary>Corps : loft unique de la pointe de la queue au cou (prolongé dans le crâne), poids le long de la chaîne.</summary>
    private static void BuildTitanBody(TitanMesh m)
    {
        m.Use(THideMat());
        var rings = new List<TitanMesh.Ring>();
        var ss = new List<float>();
        // Pointe fermée, anneaux tous les ~0,75 m (plus serrés au bout de la queue).
        for (var s = 0f; s <= _tLen; s += s < 6f ? 0.4f : 0.78f) ss.Add(s);
        ss.Add(_tLen);
        var tip = TRing(0f);
        rings.Add(new TitanMesh.Ring(tip.C - TTangent(0f) * 0.25f, tip.X, tip.Y, 0f, 0f));
        foreach (var s in ss) rings.Add(TRing(s));
        var end = TRing(_tLen);
        rings.Add(new TitanMesh.Ring(end.C + TTangent(_tLen) * 0.2f, end.X, end.Y, 0f, 0f));
        float SOf(int i) => i <= 0 ? 0f : i > ss.Count ? _tLen : ss[i - 1];
        var sNeck = _tJointS[11];
        m.Weight = p => TBodyWeight(TNearestS(p));
        m.Glow = p =>
        {
            // Gorge et dessous du cou : la lueur monte de la poitrine vers la mâchoire.
            var s = TNearestS(p);
            if (s < sNeck - 2f) return 0f;
            var r = TRing(s);
            var ventral = -(p - r.C).Normalized().Dot(r.Y);
            return Mathf.SmoothStep(-0.25f, 0.65f, ventral) * Mathf.SmoothStep(0f, 1f, (s - sNeck + 3f) / 9f);
        };
        m.Loft(rings, 44, TSq, (i, th, p) =>
        {
            var s = SOf(i);
            var r = TRing(s);
            var n = (p - r.C).Normalized();
            return THide(p, n, s / (1.25f * TS));
        }, 7f * TS, 0.12f, (i, th) => TMod(SOf(i), th));
        m.Glow = null;
    }

    /// <summary>
    /// Rangée dorsale d'épines osseuses dentelées (du cou au milieu de la queue), en lames balayées vers l'arrière, et
    /// rangées latérales d'ostéodermes.
    /// </summary>
    private static void BuildTitanSpines(TitanMesh m)
    {
        m.Use(TBoneMat());
        var j = _tJointS;
        // Hauteur des épines selon l'abscisse (clés aux articulations 3 → 13).
        float[] keyS = { j[3], j[4], j[6], j[8], j[9], j[10], j[11], j[12], j[13] };
        float[] keyH = { 1.3f, 2.2f, 4.0f, 6.2f, 8.0f, 8.8f, 7.0f, 4.6f, 2.8f };
        float H(float s)
        {
            if (s <= keyS[0]) return keyH[0];
            for (var i = 0; i < keyS.Length - 1; i++)
                if (s <= keyS[i + 1]) return Mathf.Lerp(keyH[i], keyH[i + 1], Mathf.SmoothStep(0, 1, (s - keyS[i]) / (keyS[i + 1] - keyS[i])));
            return keyH[^1];
        }
        var rng = new Random(4242);
        var sStart = j[3];
        var sEnd = j[13] + 0.5f * TS;
        for (var s = sStart; s <= sEnd; s += 3.3f * TS * Mathf.Lerp(0.75f, 1f, H(s) / 8.8f))
        {
            var h = H(s) * TS * (0.88f + 0.24f * (float)rng.NextDouble());
            var baseS = s;
            m.Weight = _ => TBodyWeight(baseS);
            var b = TBody(s, 0f, -0.35f * TS);
            var up = TBodyNormal(s, 0f);
            var back = -TTangent(s);
            back = (back - up * back.Dot(up)).Normalized();
            var lenB = Mathf.Max(1.2f, h * 0.42f);
            var thick = Mathf.Max(0.18f, h * 0.075f);
            // Lame : anneaux de la base à la pointe, lobes dentelés (silhouette en feuille d'érable rongée).
            var rings = new List<TitanMesh.Ring>();
            const int nr = 9;
            rings.Add(new TitanMesh.Ring(b - up * 0.3f, Vector3.Right, back, 0f, 0f));
            for (var i = 0; i <= nr; i++)
            {
                var k = i / (float)nr;
                var c = b + up * (h * k) + back * (h * 0.38f * k * k);
                var lobe = 1f + 0.32f * Mathf.Sin(k * Mathf.Pi * 3.2f + s) * k;
                var ry = lenB * Mathf.Pow(1f - k, 0.85f) * lobe + 0.02f;
                var rx = thick * Mathf.Pow(1f - k, 0.7f) + 0.02f;
                var dir = (up + back * (0.76f * k)).Normalized();
                var bx = Vector3.Right;
                var by = dir.Cross(bx).Normalized();
                rings.Add(new TitanMesh.Ring(c, bx, by, rx, ry));
            }
            var tipC = b + up * (h * 1.04f) + back * (h * 0.42f);
            rings.Add(new TitanMesh.Ring(tipC, Vector3.Right, back, 0f, 0f));
            var hh = h;
            var bb = b;
            m.Loft(rings, 10, 2.2f, (i, th, p) =>
            {
                var k = Mathf.Clamp((p - bb).Dot(up) / hh, 0, 1);
                return TBoneCol(p, 0.15f + 0.7f * k);
            }, 2.5f * TS);
            // Pointes secondaires sur l'arête arrière des grandes épines.
            if (h > 4f * TS)
                for (var q = 0; q < 2; q++)
                {
                    var k = 0.35f + q * 0.25f;
                    var c = b + up * (h * k) + back * (h * 0.38f * k * k + lenB * (1f - k) * 0.85f);
                    var tipQ = c + (back * 1.2f + up).Normalized() * (h * 0.22f);
                    m.Horn(new[] { c, c.Lerp(tipQ, 0.55f) + up * 0.15f, tipQ }, thick * 0.9f, 2.2f, 6, TBoneCol(c, 0.3f), TBoneCol(tipQ, 0.9f), up);
                }
        }
        // Ostéodermes latéraux : deux rangées de boutons coniques le long des flancs et de la queue.
        for (var side = -1; side <= 1; side += 2)
        for (var s = j[2]; s < j[12]; s += 2.6f * TS)
        {
            var a = side * (0.78f + 0.08f * Mathf.Sin(s * 0.7f));
            var baseS = s;
            m.Weight = _ => TBodyWeight(baseS);
            var p = TBody(s, a, -0.15f * TS);
            var n = TBodyNormal(s, a);
            var size = Mathf.Clamp(TRing(s).Rx / (8f * TS), 0.25f, 1f) * TS;
            var tip = p + n * (1.3f * size) - TTangent(s) * (0.5f * size);
            m.Horn(new[] { p - n * 0.2f, p.Lerp(tip, 0.5f), tip }, 0.55f * size, 1f, 6, TBoneCol(p, 0.25f), TBoneCol(tip, 0.8f), n.Cross(Vector3.Right).Normalized());
        }
    }

    /// <summary>Crâne lourd (arcades, joues, crête de cornes), yeux enfoncés, mâchoire articulée, dents, gueule incandescente.</summary>
    private static void BuildTitanHead(TitanMesh m)
    {
        m.Weight = _ => (TBHead, TBHead, 0f);
        m.Use(THideMat());
        // Crâne supérieur : (z, centre y, rx, ry) ; dessous plat (palais).
        float[,] sk =
        {
            { 20.6f, 51.0f, 3.6f, 3.9f }, { 22.5f, 51.4f, 4.4f, 4.4f }, { 24.5f, 51.4f, 4.6f, 4.0f }, { 26.6f, 51.2f, 4.1f, 3.4f },
            { 28.6f, 50.7f, 3.5f, 3.1f }, { 30.5f, 50.3f, 3.0f, 2.65f }, { 32.3f, 49.9f, 2.45f, 2.15f }, { 33.6f, 49.6f, 1.7f, 1.55f },
        };
        var rings = new List<TitanMesh.Ring> { new(TH(0, sk[0, 1], sk[0, 0] - 0.4f), Vector3.Right, Vector3.Up, 0f, 0f) };
        for (var i = 0; i < sk.GetLength(0); i++) rings.Add(new TitanMesh.Ring(TH(0, sk[i, 1], sk[i, 0]), Vector3.Right, Vector3.Up, sk[i, 2] * THS, sk[i, 3] * THS));
        rings.Add(new TitanMesh.Ring(TH(0, 49.45f, 34.4f), Vector3.Right, Vector3.Up, 0f, 0f));
        m.Loft(rings, 32, 2.35f, (i, th, p) =>
        {
            var n = new Vector3(Mathf.Sin(th), Mathf.Cos(th), 0f);
            return THide(p, n) * (i >= rings.Count - 3 ? 0.85f : 1f);
        }, 4f * THS, 0.55f, (i, th) =>
        {
            // Bosselures : narines, crête médiane du museau.
            var a = Mathf.Wrap(th, -Mathf.Pi, Mathf.Pi);
            return 1f + 0.05f * Mathf.Exp(-a * a / 0.04f) * (i > 3 ? 1f : 0.3f);
        });
        // Joues (muscles des mâchoires) et arcades sourcilières.
        for (var sd = -1; sd <= 1; sd += 2)
        {
            m.Ellipsoid(TH(3.6f * sd, 50.4f, 23.6f), new Vector3(1.6f, 2.2f, 2.7f) * THS, new Basis(Vector3.Up, 0.2f * sd), 7, 14, THide(TH(3.6f * sd, 50f, 23f), new Vector3(sd, 0.2f, 0)), 3f * THS);
            m.Ellipsoid(TH(2.95f * sd, 53.25f, 26.4f), new Vector3(1.05f, 0.72f, 2.5f) * THS, new Basis(Vector3.Up, 0.18f * sd) * new Basis(Vector3.Right, 0.12f), 6, 12, THide(TH(3f * sd, 53f, 26f), Vector3.Up) * 0.8f, 3f * THS);
        }
        // Gueule (intérieur rouge sombre, s'embrase avant le souffle), mêlée crâne / mâchoire selon la hauteur.
        var mouthY = TH(0, 48.7f, 0).Y;
        m.Weight = p => (TBHead, TBJaw, Mathf.Clamp((mouthY - p.Y) / (1.1f * THS) + 0.5f, 0f, 1f));
        m.Glow = _ => 1f;
        m.Ellipsoid(TH(0, 48.7f, 29.1f), new Vector3(2.55f, 1.05f, 4.5f) * THS, Basis.Identity, 8, 16, TLin(0.22f, 0.035f, 0.03f), 2f * THS);
        m.Glow = null;
        // Yeux enfoncés sous les arcades.
        m.Weight = _ => (TBHead, TBHead, 0f);
        m.Use(TEyeMat());
        for (var sd = -1; sd <= 1; sd += 2)
            m.Ellipsoid(TH(3.32f * sd, 52.35f, 27.3f), new Vector3(0.36f, 0.42f, 0.52f) * THS, new Basis(Vector3.Up, 0.5f * sd), 6, 10, Colors.White, 1f);
        // Narines, crête de cornes à l'arrière du crâne, dents supérieures.
        m.Use(THideMat());
        for (var sd = -1; sd <= 1; sd += 2)
            m.Ellipsoid(TH(0.85f * sd, 51.0f, 33.4f), new Vector3(0.32f, 0.2f, 0.45f) * THS, Basis.Identity, 4, 8, TLin(0.02f, 0.02f, 0.02f), 1f);
        m.Use(TBoneMat());
        for (var sd = -1; sd <= 1; sd += 2)
            for (var q = 0; q < 3; q++)
            {
                var b0 = TH((1.2f + q * 1.0f) * sd, 54.2f - q * 0.7f, 22.8f - q * 0.6f);
                var dir = new Vector3(0.35f * sd, 0.55f - q * 0.1f, -1f).Normalized();
                var len = (3.2f - q * 0.6f) * THS;
                var tip = b0 + dir * len;
                m.Horn(new[] { b0 - dir * 0.3f, b0 + dir * len * 0.5f + Vector3.Up * 0.25f, tip }, 0.55f * THS, 1f, 7, TBoneCol(b0, 0.2f), TBoneCol(tip, 0.95f), Vector3.Up);
            }
        for (var q = 0; q < 4; q++)
        {
            var b0 = TH(0, 55.0f - q * 0.25f, 23.6f - q * 1.6f);
            var tip = b0 + new Vector3(0, 1.4f - q * 0.15f, -1.4f) * THS;
            m.Horn(new[] { b0 - Vector3.Up * 0.3f, b0.Lerp(tip, 0.5f), tip }, 0.45f * THS, 1.4f, 6, TBoneCol(b0, 0.2f), TBoneCol(tip, 0.9f), Vector3.Back);
        }
        var toothDark = TLin(0.42f, 0.38f, 0.3f);
        var toothTip = TLin(0.66f, 0.62f, 0.52f);
        for (var sd = -1; sd <= 1; sd += 2)
            for (var z = 27.4f; z < 33.4f; z += 0.85f)
            {
                var k = (z - 27f) / 8f;
                var x = Mathf.Lerp(3.05f, 1.15f, k) * sd;
                var y = Mathf.Lerp(48.75f, 48.45f, k);
                var len = Mathf.Lerp(1.15f, 0.7f, k) * (1f + 0.25f * Mathf.Sin(z * 3.1f));
                var b0 = TH(x, y + 0.35f, z);
                var tip = TH(x * 0.97f, y - len, z + 0.15f);
                m.Horn(new[] { b0, b0.Lerp(tip, 0.6f) + new Vector3(0, 0, 0.05f), tip }, 0.24f * THS, 1f, 5, toothDark, toothTip, Vector3.Back);
            }
        // Mâchoire inférieure.
        m.Weight = _ => (TBJaw, TBJaw, 0f);
        m.Use(THideMat());
        float[,] jw = { { 23.4f, 47.9f, 3.7f, 1.95f }, { 25.8f, 47.5f, 3.6f, 1.7f }, { 28.4f, 47.4f, 3.1f, 1.45f }, { 30.8f, 47.45f, 2.55f, 1.2f }, { 32.6f, 47.6f, 1.9f, 0.95f }, { 33.7f, 47.75f, 1.1f, 0.7f } };
        var jr = new List<TitanMesh.Ring> { new(TH(0, jw[0, 1], jw[0, 0] - 0.5f), Vector3.Right, Vector3.Up, 0f, 0f) };
        for (var i = 0; i < jw.GetLength(0); i++) jr.Add(new TitanMesh.Ring(TH(0, jw[i, 1], jw[i, 0]), Vector3.Right, Vector3.Up, jw[i, 2] * THS, jw[i, 3] * THS));
        jr.Add(new TitanMesh.Ring(TH(0, 47.85f, 34.2f), Vector3.Right, Vector3.Up, 0f, 0f));
        var jawLow = TH(0, 47.5f, 0).Y;
        m.Glow = p => Mathf.Clamp((jawLow - p.Y) / (1.2f * THS), 0f, 1f) * 0.6f;
        m.Loft(jr, 26, 2.3f, (i, th, p) => THide(p, new Vector3(Mathf.Sin(th), Mathf.Cos(th), 0f), p.Z / (0.9f * THS)), 3.5f * THS, 0.1f);
        m.Glow = null;
        m.Use(TBoneMat());
        for (var sd = -1; sd <= 1; sd += 2)
            for (var z = 27.9f; z < 33.2f; z += 0.9f)
            {
                var k = (z - 27f) / 8f;
                var x = Mathf.Lerp(2.75f, 1.0f, k) * sd;
                var y = Mathf.Lerp(48.2f, 48.2f, k);
                var len = Mathf.Lerp(0.95f, 0.6f, k) * (1f + 0.25f * Mathf.Cos(z * 2.7f));
                var b0 = TH(x, y - 0.35f, z);
                var tip = TH(x * 0.96f, y + len, z + 0.1f);
                m.Horn(new[] { b0, b0.Lerp(tip, 0.6f), tip }, 0.22f * THS, 1f, 5, toothDark, toothTip, Vector3.Back);
            }
    }

    /// <summary>Jambe : cuisse massive, tibia à mollet, métatarse incliné, pied à trois doigts griffus et ergot.</summary>
    private static void BuildTitanLeg(TitanMesh m, int i)
    {
        var sd = TSide(i);
        var h = TiHip(i);
        var k = TiKnee(i);
        var a = TiAnkle(i);
        var b = TiBall(i);
        var fwd = Vector3.Back; // +Z
        m.Use(THideMat());
        Color Paint(Vector3 p, Vector3 axisA, Vector3 axisB)
        {
            var d = axisB - axisA;
            var t = Mathf.Clamp((p - axisA).Dot(d) / d.LengthSquared(), 0, 1);
            var n = (p - axisA.Lerp(axisB, t)).Normalized();
            return THide(p, n * 0.8f + Vector3.Up * 0.2f);
        }
        // Cuisse : prend naissance dans le bassin.
        var thighPts = new[] { h + new Vector3(-1.4f * sd, 2.6f, -0.6f) * TS, h, h.Lerp(k, 0.33f) + fwd * (0.8f * TS) + new Vector3(0.5f * sd, 0, 0) * TS, h.Lerp(k, 0.7f) + fwd * (0.3f * TS), k, k + (k - h).Normalized() * (1.1f * TS) };
        var trx = new[] { 4.6f, 5.5f, 5.7f, 4.5f, 3.45f, 2.6f };
        var thy = new[] { 5.0f, 5.9f, 5.9f, 4.7f, 3.5f, 2.6f };
        for (var q = 0; q < trx.Length; q++) { trx[q] *= TS; thy[q] *= TS; }
        m.Weight = TSegWeight(h, k, TThigh(i), TBPelvis, TShin(i), 0.22f, 0.18f);
        m.Tube(thighPts, trx, thy, 30, (_, _, p) => Paint(p, h, k), (k - h).Cross(Vector3.Right * sd).Normalized(), 4.5f * TS);
        // Tibia : mollet en arrière.
        var shinPts = new[] { k + (k - a).Normalized() * (1.3f * TS), k, k.Lerp(a, 0.3f) - fwd * (0.7f * TS), k.Lerp(a, 0.68f) - fwd * (0.25f * TS), a };
        var srx = new[] { 2.6f * TS, 3.35f * TS, 3.1f * TS, 2.55f * TS, 2.2f * TS };
        m.Weight = TSegWeight(k, a, TShin(i), TThigh(i), TFoot(i), 0.18f, 0.16f);
        m.Tube(shinPts, srx, srx, 24, (_, _, p) => Paint(p, k, a), Vector3.Back, 4f * TS);
        // Pied (métatarse, coussinet, doigts, griffes) : rigide sur l'os du pied.
        m.Weight = TSegWeight(a, b, TFoot(i), TShin(i), -1, 0.25f, 0f);
        var meta = new[] { a + (a - k).Normalized() * (0.4f * TS) * -1f, a, a.Lerp(b, 0.55f), b };
        var mr = new[] { 2.4f * TS, 2.35f * TS, 2.1f * TS, 1.9f * TS };
        m.Tube(meta, mr, mr, 20, (_, _, p) => Paint(p, a, b), Vector3.Back, 3f * TS);
        m.Weight = _ => (TFoot(i), TFoot(i), 0f);
        m.Ellipsoid(b + new Vector3(0, -0.15f, 0.6f) * TS, new Vector3(2.5f, 1.25f, 2.7f) * TS, Basis.Identity, 8, 16, THide(b, Vector3.Down) * 0.8f, 3f * TS);
        foreach (var (yaw, len) in new[] { (-0.42f, 3.6f), (0f, 4.3f), (0.42f, 3.6f) })
        {
            var dir = new Basis(Vector3.Up, yaw * sd) * new Vector3(0, -0.12f, 1f).Normalized();
            var t0 = b + new Vector3(0, -0.1f, 0.8f) * TS + dir * (0.6f * TS);
            var t1 = t0 + dir * (len * TS * 0.55f) + Vector3.Down * (0.15f * TS);
            var t2 = t0 + dir * (len * TS) + Vector3.Down * (0.5f * TS);
            m.Use(THideMat());
            m.Tube(new[] { t0, t1, t2 }, new[] { 1.05f * TS, 0.9f * TS, 0.7f * TS }, new[] { 0.9f * TS, 0.75f * TS, 0.6f * TS }, 12, (_, _, p) => THide(p, Vector3.Up) * 0.9f, Vector3.Up, 2f * TS);
            m.Use(TBoneMat());
            var c0 = t2 - dir * (0.2f * TS);
            var c2 = t2 + dir * (1.5f * TS) + Vector3.Down * (0.75f * TS);
            m.Horn(new[] { c0, t2 + dir * (0.9f * TS) - Vector3.Down * (0.05f * TS), c2 }, 0.62f * TS, 1.25f, 8, TBoneCol(c0, 0.2f), TBoneCol(c2, 0.85f), Vector3.Up);
        }
        // Ergot à l'arrière de la cheville.
        var e0 = a + new Vector3(0, -1.2f, -1.6f) * TS;
        var e2 = e0 + new Vector3(0, -1.6f, -1.2f) * TS;
        m.Horn(new[] { e0, e0.Lerp(e2, 0.5f) + Vector3.Back * (-0.2f * TS), e2 }, 0.55f * TS, 1.2f, 7, TBoneCol(e0, 0.2f), TBoneCol(e2, 0.85f), Vector3.Up);
    }

    /// <summary>Bras court et puissant : épaule, avant-bras, main à quatre doigts griffus.</summary>
    private static void BuildTitanArm(TitanMesh m, int i)
    {
        var sd = TSide(i);
        var s = TiShoulder(i);
        var e = TiElbow(i);
        var w = TiWrist(i);
        m.Use(THideMat());
        Color Paint(Vector3 p, Vector3 a, Vector3 b)
        {
            var d = b - a;
            var t = Mathf.Clamp((p - a).Dot(d) / d.LengthSquared(), 0, 1);
            return THide(p, (p - a.Lerp(b, t)).Normalized());
        }
        m.Weight = TSegWeight(s, e, TUpper(i), TBChest, TFore(i), 0.25f, 0.2f);
        m.Tube(new[] { s + new Vector3(-1.4f * sd, 0.8f, -1.2f) * TS, s, s.Lerp(e, 0.45f) + new Vector3(0.25f * sd, 0, 0.3f) * TS, e }, new[] { 2.4f * TS, 2.75f * TS, 2.75f * TS, 1.95f * TS },
            new[] { 2.6f * TS, 2.9f * TS, 2.6f * TS, 2.0f * TS }, 20, (_, _, p) => Paint(p, s, e), Vector3.Back, 3f * TS);
        m.Weight = TSegWeight(e, w, TFore(i), TUpper(i), THand(i), 0.2f, 0.2f);
        m.Tube(new[] { e, e.Lerp(w, 0.4f) + new Vector3(0.2f * sd, 0, 0) * TS, w }, new[] { 2.3f * TS, 2.2f * TS, 1.65f * TS }, new[] { 2.35f * TS, 2.05f * TS, 1.6f * TS }, 18,
            (_, _, p) => Paint(p, e, w), Vector3.Back, 3f * TS);
        m.Weight = _ => (THand(i), THand(i), 0f);
        var dir = (w - e).Normalized();
        var palm = w + dir * (0.9f * TS);
        m.Ellipsoid(palm, new Vector3(1.6f, 1.0f, 1.6f) * TS, TFrameBasis(dir, Vector3.Right * sd), 6, 12, THide(palm, Vector3.Down), 2f * TS);
        for (var f = 0; f < 4; f++)
        {
            var spread = (f - 1.5f) * 0.32f;
            var fd = (new Basis(TFrameBasis(dir, Vector3.Right * sd).Y, spread) * dir).Normalized();
            var f0 = palm + fd * (1.0f * TS);
            var f1 = f0 + fd * (1.0f * TS) + Vector3.Down * (0.45f * TS);
            m.Use(THideMat());
            m.Tube(new[] { f0, f1 }, new[] { 0.5f * TS, 0.4f * TS }, new[] { 0.5f * TS, 0.4f * TS }, 8, (_, _, p) => THide(p, Vector3.Up) * 0.9f, Vector3.Up, 1.5f * TS);
            m.Use(TBoneMat());
            var c2 = f1 + fd * (0.7f * TS) + Vector3.Down * (0.9f * TS);
            m.Horn(new[] { f1 - fd * 0.1f, f1 + fd * (0.45f * TS), c2 }, 0.4f * TS, 1.3f, 6, TBoneCol(f1, 0.2f), TBoneCol(c2, 0.9f), Vector3.Up);
        }
    }

    private static Basis TFrameBasis(Vector3 z, Vector3 xHint)
    {
        z = z.Normalized();
        var x = (xHint - z * xHint.Dot(z)).Normalized();
        return new Basis(x, z.Cross(x), z);
    }

    /// <summary>Correspondance carré → disque (bords de plaque arrondis) : (x, y) ∈ [-1, 1]².</summary>
    private static Vector2 TSquircle(float x, float y) => new(x * Mathf.Sqrt(1f - y * y * 0.5f), y * Mathf.Sqrt(1f - x * x * 0.5f));

    /// <summary>
    /// Plaque d'armure osseuse : bouclier bombé épousant le corps, bord épais qui plonge sous la peau, stries de croissance
    /// concentriques, sillons rayonnants, rangée de bosses coniques sur le sommet.
    /// </summary>
    private static void BuildTitanPlate(TitanMesh m, TitanPlateDef d, bool debris)
    {
        m.Use(TBoneMat());
        const int nu = 18, nv = 14;
        var pos = new Vector3[nu + 1, nv + 1];
        var col = new Color[nu + 1, nv + 1];
        var uv = new Vector2[nu + 1, nv + 1];
        var sMid = 0.5f * (d.S0 + d.S1);
        var aMid = 0.5f * (d.A0 + d.A1);
        var hs = 0.5f * (d.S1 - d.S0);
        var ha = 0.5f * (d.A1 - d.A0);
        var seed = d.S0 * 0.37f + d.A0 * 3.1f;
        for (var i = 0; i <= nu; i++)
        for (var j = 0; j <= nv; j++)
        {
            var x = i / (float)nu * 2f - 1f;
            var y = j / (float)nv * 2f - 1f;
            var border = i == 0 || j == 0 || i == nu || j == nv;
            var bevel = !border && (i == 1 || j == 1 || i == nu - 1 || j == nv - 1);
            // Contour en écusson (rectangle aux coins arrondis), un peu rétréci vers l'arrière.
            var q = new Vector2(x, y).Lerp(TSquircle(x, y), 0.55f) * (border ? 1f : bevel ? 0.985f : 0.955f);
            q.Y *= 1f - 0.06f * (1f - q.X);
            var s = sMid + q.X * hs;
            var a = aMid + q.Y * ha;
            var p = TBody(s, a);
            var n = TBodyNormal(s, a);
            // Bouclier plat et épais : bombé léger, trois carènes longitudinales qui montent vers l'avant, sutures, éclats.
            var dome = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(q.X), 4f))) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(q.Y), 4f)));
            var rise = 0.6f + 0.4f * (q.X + 1f) * 0.5f;
            var keel = (0.42f * Mathf.Exp(-q.Y * q.Y / 0.018f) + 0.26f * Mathf.Exp(-(q.Y - 0.56f) * (q.Y - 0.56f) / 0.012f)
                + 0.26f * Mathf.Exp(-(q.Y + 0.56f) * (q.Y + 0.56f) / 0.012f)) * rise * dome;
            var seam = Mathf.Pow(Mathf.Abs(Mathf.Sin(q.X * 2.6f + seed)), 24f) * 0.18f * dome;
            var chip = TNoise(p, 0.9f) * 0.12f + TNoise(p, 2.6f) * 0.05f;
            var off = border ? -0.45f * TS : bevel ? d.Thick * 0.5f : d.Thick * (0.7f + 0.3f * dome) + (keel - seam + chip) * TS * 1.4f;
            pos[i, j] = p + n * off;
            var wear = border || bevel ? 0.05f : Mathf.Clamp(0.22f + keel * 1.6f + dome * 0.25f - seam * 3f + chip * 1.5f, 0f, 1f);
            col[i, j] = TBoneCol(p, wear) * 1.35f;
            uv[i, j] = new Vector2(q.X * hs, q.Y * ha * 8f) / (5f * TS);
        }
        m.Patch(pos, col, uv, -TBodyNormal(sMid, aMid));
        // Pointes basses le long de la carène centrale, couchées vers l'arrière.
        for (var q = -1; q <= 1; q++)
        {
            var s = sMid + q * hs * 0.55f;
            var p = TBody(s, aMid);
            var n = TBodyNormal(s, aMid);
            var b0 = p + n * (d.Thick * 1.1f);
            var tip = b0 + (n - TTangent(s) * 0.9f).Normalized() * (d.Thick * (q == 1 ? 1.3f : 0.95f));
            m.Horn(new[] { b0 - n * 0.4f, b0.Lerp(tip, 0.5f), tip }, d.Thick * 0.42f, 1.6f, 7, TBoneCol(b0, 0.35f) * 1.3f, TBoneCol(tip, 0.95f) * 1.3f, TTangent(s));
        }
    }

    /// <summary>Chair à vif sous la plaque : nappe bosselée au bord irrégulier, légèrement en relief sur la peau.</summary>
    private static void BuildTitanWound(TitanMesh m, TitanPlateDef d)
    {
        m.Use(TWoundMat());
        const int nu = 14, nv = 11;
        var pos = new Vector3[nu + 1, nv + 1];
        var col = new Color[nu + 1, nv + 1];
        var uv = new Vector2[nu + 1, nv + 1];
        var sMid = 0.5f * (d.S0 + d.S1);
        var aMid = 0.5f * (d.A0 + d.A1);
        var hs = 0.5f * (d.S1 - d.S0) * 0.86f;
        var ha = 0.5f * (d.A1 - d.A0) * 0.86f;
        for (var i = 0; i <= nu; i++)
        for (var j = 0; j <= nv; j++)
        {
            var x = i / (float)nu * 2f - 1f;
            var y = j / (float)nv * 2f - 1f;
            var q = TSquircle(x, y);
            var border = i == 0 || j == 0 || i == nu || j == nv;
            // Bord déchiqueté.
            var ang = Mathf.Atan2(q.Y, q.X);
            q *= border ? 0.88f + 0.12f * Mathf.Sin(ang * 7f + sMid) : 1f;
            var s = sMid + q.X * hs;
            var a = aMid + q.Y * ha;
            var p = TBody(s, a);
            var n = TBodyNormal(s, a);
            var r = Mathf.Min(q.Length(), 1f);
            var bump = TNoise(p, 0.55f) * 0.35f * TS;
            pos[i, j] = p + n * (border ? -0.05f * TS : 0.12f * TS + (1f - r * r) * 0.25f * TS + bump);
            col[i, j] = Colors.White;
            uv[i, j] = new Vector2(q.X * hs, q.Y * ha * 8f) / (4f * TS);
        }
        m.Patch(pos, col, uv, -TBodyNormal(sMid, aMid));
    }
}
