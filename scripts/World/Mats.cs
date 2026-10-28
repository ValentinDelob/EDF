using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Matériaux PBR générés procéduralement (textures de bruit), mis en cache.
/// Les surfaces du décor (façades, voirie, sol, détails) passent par le shader « Weathered » (Mats.Weather.cs) :
/// matières ternes et sales, salissures en coordonnées monde.
/// </summary>
public static partial class Mats
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

    private static bool TryCache<T>(string key, out T mat) where T : Material
    {
        if (Cache.TryGetValue(key, out var m)) { mat = (T)m; return true; }
        mat = null;
        return false;
    }

    /// <summary>Béton (sols de quai, toits, gravats) : teinte tachée, relief de coffrage, salissures.</summary>
    public static ShaderMaterial Concrete(Color tint, int seed = 1)
    {
        var key = $"concrete{tint}{seed}";
        if (TryCache<ShaderMaterial>(key, out var c)) return c;
        var mat = Weathered(ColorNoise(seed, 0.02f, tint.Darkened(0.2f), tint.Lightened(0.04f)), Noise(seed + 7, 0.05f, true, 512, 3f), null, 1, new Vector3(0.25f, 0.25f, 0.25f))
            .With("roughness", 0.92f).With("normal_depth", 0.7f).With("puddles", 0.6f).With("streaks", 0.8f).With("desat", 0.2f);
        Cache[key] = mat;
        return mat;
    }

    /// <summary>
    /// Asphalte usé (16 m, 64 px/m) : granulat, usure, réparations rectangulaires bordées de joint, fissures en
    /// réseau, taches d'huile ; flaques et taches à grande échelle ajoutées par le shader (coordonnées monde).
    /// </summary>
    public static ShaderMaterial Asphalt()
    {
        if (TryCache<ShaderMaterial>("asphalt", out var c)) return c;
        const int s = 1024;
        const int cells = 16; // cellules de Voronoï (1 m)
        const int cell = s / cells;
        var alb = new Px(s, s);
        var rough = new Px(s, s, 1);
        var bump = new Px(s, s);
        // Réparations : rectangles alignés (périodiques), quelques-uns par tuile.
        var patches = new List<Rect2I>();
        for (var i = 0; i < 9; i++)
        {
            var pw = 90 + (int)(Hash(i, 1, 301) * 260);
            var ph = 60 + (int)(Hash(i, 2, 301) * 200);
            patches.Add(new Rect2I((int)(Hash(i, 3, 301) * s), (int)(Hash(i, 4, 301) * s), pw, ph));
        }
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                // Domaine déformé pour des fissures sinueuses.
                var wx = x + (ValueNoise(x, y, 32, s / 32, 311) - 0.5f) * 26f;
                var wy = y + (ValueNoise(x, y, 32, s / 32, 312) - 0.5f) * 26f;
                var f1 = 1e9f; var f2 = 1e9f;
                var cx = Mathf.FloorToInt(wx / cell);
                var cy = Mathf.FloorToInt(wy / cell);
                for (var j = -1; j <= 1; j++)
                for (var i = -1; i <= 1; i++)
                {
                    var gx = cx + i; var gy = cy + j;
                    var hx = ((gx % cells) + cells) % cells;
                    var hy = ((gy % cells) + cells) % cells;
                    var px = (gx + 0.15f + Hash(hx, hy, 321) * 0.7f) * cell;
                    var py = (gy + 0.15f + Hash(hx, hy, 322) * 0.7f) * cell;
                    var dd = (px - wx) * (px - wx) + (py - wy) * (py - wy);
                    if (dd < f1) { f2 = f1; f1 = dd; } else if (dd < f2) f2 = dd;
                }
                var edge = Mathf.Sqrt(f2) - Mathf.Sqrt(f1);
                var crackZone = ValueNoise(x, y, 128, s / 128, 331) * 0.7f + ValueNoise(x, y, 32, s / 32, 332) * 0.3f;
                var crackW = crackZone > 0.5f ? 1.4f + (crackZone - 0.5f) * 5f : 0f;
                var crack = edge < crackW ? 1f - edge / Mathf.Max(crackW, 0.01f) * 0.5f : 0f;
                // Fines fissures en long (retrait).
                var hair = VNoise(x, y, 2, 64, s, 333);
                if (hair > 0.93f && crackZone > 0.35f) crack = Mathf.Max(crack, 0.6f);

                var grain = Hash(x, y, 341);
                var grain2 = ValueNoise(x, y, 4, s / 4, 342);
                var wear = ValueNoise(x, y, 128, s / 128, 343) * 0.6f + ValueNoise(x, y, 64, s / 64, 344) * 0.4f;
                var tone = Mathf.Lerp(0.13f, 0.27f, wear) + (grain - 0.5f) * 0.05f + (grain2 - 0.5f) * 0.04f;
                // Granulats clairs dans l'enrobé usé.
                if (grain > 0.965f && wear > 0.45f) tone += 0.1f;
                var r = 0.86f + (grain2 - 0.5f) * 0.1f;
                var hgt = 0.5f + (grain - 0.5f) * 0.12f + (grain2 - 0.5f) * 0.1f;

                foreach (var p in patches)
                {
                    var lx = ((x - p.Position.X) % s + s) % s;
                    var ly = ((y - p.Position.Y) % s + s) % s;
                    if (lx >= p.Size.X || ly >= p.Size.Y) continue;
                    var border = Mathf.Min(Mathf.Min(lx, p.Size.X - 1 - lx), Mathf.Min(ly, p.Size.Y - 1 - ly));
                    var pk = Hash(p.Position.X, p.Position.Y, 351);
                    tone = (pk < 0.5f ? 0.11f : 0.2f) + (grain - 0.5f) * 0.035f + (grain2 - 0.5f) * 0.03f;
                    r = pk < 0.5f ? 0.8f : 0.9f;
                    hgt = 0.56f + (grain - 0.5f) * 0.06f;
                    crack *= 0.15f;
                    if (border < 5) { tone = 0.06f; r = 0.55f; hgt = 0.6f; } // joint de bitume
                    break;
                }
                if (crack > 0) { tone = Mathf.Lerp(tone, 0.035f, crack); hgt = Mathf.Lerp(hgt, 0.05f, crack); r = Mathf.Lerp(r, 1f, crack); }

                // Taches d'huile.
                var oil = ValueNoise(x, y, 24, s / 24, 361) * 0.6f + ValueNoise(x, y, 8, s / 8, 362) * 0.4f;
                var oilMask = ValueNoise(x, y, 128, s / 128, 363);
                if (oil > 0.66f && oilMask > 0.55f)
                {
                    var k = Mathf.Clamp((oil - 0.66f) * 8f, 0, 1);
                    tone = Mathf.Lerp(tone, 0.05f, k * 0.8f);
                    r = Mathf.Lerp(r, 0.38f, k);
                }
                alb.Set(x, y, new Color(tone, tone * 0.99f, tone * 0.97f));
                rough.Set(x, y, r);
                bump.Set(x, y, new Color(hgt, hgt, hgt));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 5f), ImageTexture.CreateFromImage(rough.ToImage()), 2, Vector3.One / 16f)
            .With("roughness", 1f).With("normal_depth", 0.9f).With("puddles", 1f).With("grime", 0.9f).With("specular", 0.45f);
        Cache["asphalt"] = mat;
        return mat;
    }

    public static ShaderMaterial Sidewalk()
    {
        if (TryCache<ShaderMaterial>("sidewalk", out var c)) return c;
        var mat = Paving();
        Cache["sidewalk"] = mat;
        return mat;
    }

    public static StandardMaterial3D Sand()
    {
        if (TryCache<StandardMaterial3D>("sand", out var c)) return c;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(51, 0.03f, new Color(0.46f, 0.43f, 0.37f), new Color(0.6f, 0.57f, 0.5f)),
            NormalEnabled = true,
            NormalTexture = Noise(52, 0.12f, true, 512, 5f),
            Roughness = 0.95f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.15f, 0.15f, 0.15f),
        };
        Cache["sand"] = mat;
        return mat;
    }

    /// <summary>
    /// Pelouse (8 m) : herbe verte de parc et touffes jaunies, quelques plaques de terre piétinée.
    /// </summary>
    public static ShaderMaterial Grass()
    {
        if (TryCache<ShaderMaterial>("grass", out var c)) return c;
        var (alb, bump, rough) = DeadGrass(false);
        var mat = Weathered(alb, bump, rough, 2, Vector3.One / 8f)
            .With("roughness", 1f).With("normal_depth", 1f).With("puddles", 0.3f).With("grime", 0.5f);
        Cache["grass"] = mat;
        return mat;
    }

    /// <summary>Textures de pelouse morte : couleurs réelles, ou neutres (multipliées par la couleur de sommet).</summary>
    private static (ImageTexture alb, ImageTexture normal, ImageTexture rough) DeadGrass(bool neutral)
    {
        const int s = 512;
        var alb = new Px(s, s);
        var rough = new Px(s, s, 1);
        var bump = new Px(s, s);
        var straw = neutral ? new Color(1f, 0.92f, 0.78f) : new Color(0.4f, 0.4f, 0.2f);
        var dry = neutral ? new Color(0.72f, 0.7f, 0.6f) : new Color(0.24f, 0.32f, 0.13f);
        var green = neutral ? new Color(0.62f, 0.66f, 0.56f) : new Color(0.17f, 0.33f, 0.1f);
        var mud = neutral ? new Color(0.55f, 0.43f, 0.36f) : new Color(0.27f, 0.25f, 0.16f);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                // Brins dans toutes les directions (des bruits étirés en x et en y croisés) : aucune strie de loin.
                var blades = VNoise(x, y, 1, 4, s, 401) * 0.25f + VNoise(x, y, 4, 1, s, 414) * 0.25f + VNoise(x, y, 2, 2, s, 402) * 0.3f + Hash(x, y, 403) * 0.2f;
                var patch = ValueNoise(x, y, 64, s / 64, 404) * 0.6f + ValueNoise(x, y, 16, s / 16, 405) * 0.4f;
                var greenM = ValueNoise(x, y, 32, s / 32, 406) * 0.7f + ValueNoise(x, y, 8, s / 8, 407) * 0.3f;
                var mudM = ValueNoise(x, y, 128, s / 128, 408) * 0.55f + ValueNoise(x, y, 32, s / 32, 409) * 0.3f + ValueNoise(x, y, 4, s / 4, 410) * 0.15f;
                var col = dry.Lerp(straw, Mathf.SmoothStep(0.3f, 0.7f, patch));
                col = col.Lerp(green, Mathf.SmoothStep(neutral ? 0.55f : 0.35f, neutral ? 0.75f : 0.6f, greenM) * (neutral ? 0.8f : 0.9f));
                col *= 0.72f + blades * 0.5f;
                var hgt = 0.35f + blades * 0.6f;
                var r = 0.95f;
                var m = neutral ? Mathf.SmoothStep(0.58f, 0.66f, mudM) : Mathf.SmoothStep(0.72f, 0.78f, mudM) * 0.7f;
                if (m > 0)
                {
                    var mc = mud * (0.8f + Hash(x, y, 411) * 0.15f + ValueNoise(x, y, 8, s / 8, 412) * 0.2f);
                    col = col.Lerp(mc, m);
                    hgt = Mathf.Lerp(hgt, 0.25f + ValueNoise(x, y, 8, s / 8, 413) * 0.2f, m);
                    r = Mathf.Lerp(r, 0.55f, m * Mathf.SmoothStep(0.64f, 0.72f, mudM));
                }
                alb.Set(x, y, col);
                rough.Set(x, y, r);
                bump.Set(x, y, new Color(hgt, hgt, hgt));
            }
        });
        return (ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 3f), ImageTexture.CreateFromImage(rough.ToImage()));
    }

    public static StandardMaterial3D Dirt()
    {
        if (TryCache<StandardMaterial3D>("dirt", out var c)) return c;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(71, 0.04f, new Color(0.18f, 0.155f, 0.13f), new Color(0.31f, 0.28f, 0.23f)),
            NormalEnabled = true,
            NormalTexture = Noise(72, 0.08f, true, 512, 8f),
            Roughness = 1f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.2f, 0.2f, 0.2f),
        };
        Cache["dirt"] = mat;
        return mat;
    }

    // ───────────────────────── Façades ─────────────────────────

    /// <summary>Nombre de variantes de façade disponibles (0-5 : génériques, 6-7 : murs-rideaux, 8-9 : résidentiel).</summary>
    public const int FacadeCount = 10;

    private enum FKind { Punched, Ribbon, Curtain, Brick }

    /// <summary>Style de façade ; <c>Panels</c> : panneaux préfabriqués (joints au lieu de bandeaux).</summary>
    private readonly record struct FStyle(FKind Kind, Color Wall, int Cols, float WinW, float WinTop, float WinBot, Color Frame, bool Mullion, float LitRate, bool Panels);

    // Ville de Monster Attack : enduits clairs (crème, sable, blanc cassé, gris perle), brique orangée, murs-rideaux bleutés.
    // La teinte propre à chaque immeuble (Building.Tint) varie ensuite la couleur d'un immeuble à l'autre.
    private static FStyle StyleOf(int v) => v switch
    {
        0 => new(FKind.Punched, new Color(0.62f, 0.6f, 0.56f), 2, 0.5f, 0.26f, 0.8f, new Color(0.22f, 0.23f, 0.25f), true, 0.32f, true),
        1 => new(FKind.Punched, new Color(0.68f, 0.62f, 0.52f), 3, 0.5f, 0.24f, 0.8f, new Color(0.62f, 0.6f, 0.56f), false, 0.35f, false),
        2 => new(FKind.Ribbon, new Color(0.6f, 0.62f, 0.64f), 4, 1f, 0.3f, 0.78f, new Color(0.14f, 0.15f, 0.17f), true, 0.4f, true),
        3 => new(FKind.Punched, new Color(0.72f, 0.68f, 0.6f), 2, 0.58f, 0.22f, 0.82f, new Color(0.3f, 0.26f, 0.22f), true, 0.3f, false),
        4 => new(FKind.Brick, new Color(0.56f, 0.36f, 0.27f), 3, 0.5f, 0.24f, 0.82f, new Color(0.66f, 0.64f, 0.6f), false, 0.35f, false),
        5 => new(FKind.Ribbon, new Color(0.7f, 0.7f, 0.68f), 3, 1f, 0.32f, 0.8f, new Color(0.5f, 0.52f, 0.54f), true, 0.38f, true),
        6 => new(FKind.Curtain, new Color(0.13f, 0.2f, 0.28f), 5, 1f, 0f, 0.8f, new Color(0.5f, 0.52f, 0.55f), true, 0.45f, false),
        7 => new(FKind.Curtain, new Color(0.17f, 0.24f, 0.24f), 4, 1f, 0f, 0.8f, new Color(0.34f, 0.35f, 0.34f), true, 0.45f, false),
        8 => new(FKind.Brick, new Color(0.6f, 0.4f, 0.3f), 3, 0.48f, 0.2f, 0.84f, new Color(0.7f, 0.69f, 0.66f), true, 0.35f, false),
        _ => new(FKind.Punched, new Color(0.76f, 0.74f, 0.7f), 2, 0.42f, 0.16f, 0.95f, new Color(0.24f, 0.26f, 0.29f), true, 0.35f, true),
    };

    /// <summary>Indique si la variante est un mur-rideau (tour de bureaux vitrée).</summary>
    public static bool IsCurtain(int variant) => StyleOf(variant).Kind == FKind.Curtain;

    /// <summary>Hachage entier → [0, 1[ (stable, sans état).</summary>
    private static float Hash(int a, int b, int c)
    {
        unchecked
        {
            var h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    /// <summary>Bruit de valeur lissé, périodique sur <paramref name="period"/> cellules.</summary>
    private static float ValueNoise(float x, float y, int cell, int period, int seed)
    {
        var fx = x / cell;
        var fy = y / cell;
        var ix = Mathf.FloorToInt(fx);
        var iy = Mathf.FloorToInt(fy);
        var tx = fx - ix;
        var ty = fy - iy;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        float H(int i, int j) => Hash(((i % period) + period) % period, ((j % period) + period) % period, seed);
        var a = Mathf.Lerp(H(ix, iy), H(ix + 1, iy), tx);
        var b = Mathf.Lerp(H(ix, iy + 1), H(ix + 1, iy + 1), tx);
        return Mathf.Lerp(a, b, ty);
    }

    /// <summary>Image RGBA8 construite en mémoire (bien plus rapide que SetPixel).</summary>
    private sealed class Px
    {
        public readonly int W, H;
        private readonly byte[] _d;
        private readonly int _ch;

        public Px(int w, int h, int ch = 4)
        {
            W = w; H = h; _ch = ch;
            _d = new byte[w * h * ch];
        }

        private static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        public void Set(int x, int y, Color c)
        {
            var i = (y * W + x) * _ch;
            _d[i] = B(c.R);
            if (_ch == 1) return;
            _d[i + 1] = B(c.G);
            _d[i + 2] = B(c.B);
            if (_ch == 4) _d[i + 3] = B(c.A);
        }

        public void Set(int x, int y, float v) => _d[(y * W + x) * _ch] = B(v);

        public Image ToImage(bool mipmaps = true)
        {
            var fmt = _ch switch { 1 => Image.Format.L8, 3 => Image.Format.Rgb8, _ => Image.Format.Rgba8 };
            var img = Image.CreateFromData(W, H, false, fmt, _d);
            if (mipmaps) img.GenerateMipmaps();
            return img;
        }
    }

    /// <summary>Panache de suie au-dessus d'une fenêtre incendiée (<paramref name="up"/> en étages, 0 = linteau).</summary>
    private static float Soot(float up, float lx, float winW, float noise)
    {
        if (up < 0 || up > 1.4f) return 0;
        var half = winW * 0.5f + 0.04f + up * 0.32f;
        var dx = Mathf.Abs(lx - 0.5f) / half;
        if (dx >= 1) return 0;
        return Mathf.Clamp((1 - dx * dx) * Mathf.Exp(-up * 1.7f) * (0.6f + 0.8f * noise), 0, 1);
    }

    /// <summary>
    /// Façade d'immeuble : texture de fenêtres générée, UV en mètres (1 U = 7 m, 1 V = un étage de 3,2 m).
    /// La texture couvre 2 U × 4 étages pour varier stores, vitres brisées, fenêtres incendiées (panache de suie)
    /// et fenêtres éclairées d'un étage à l'autre. Murs : grain, joints de panneaux, coulures sous les appuis.
    /// </summary>
    public static ShaderMaterial Facade(int variant, bool night)
    {
        var key = $"facade{variant}{night}";
        if (TryCache<ShaderMaterial>(key, out var cached)) return cached;
        var s = StyleOf(variant);
        const int perU = 256, perFloor = 128, uSpan = 2, floors = 4;
        const int w = perU * uSpan, h = perFloor * floors;
        var alb = new Px(w, h);
        var rough = new Px(w, h, 1);
        var bump = new Px(w, h);
        var emit = night ? new Px(w, h, 3) : null;
        var curtain = s.Kind == FKind.Curtain;
        var soot = new Color(0.035f, 0.032f, 0.03f);
        var dirt = new Color(0.17f, 0.16f, 0.145f);

        // Quelques fenêtres incendiées ou brisées seulement : la ville est encore debout quand l'invasion commence.
        bool Burnt(int cx, int f) => Hash(cx, ((f % floors) + floors) % floors, variant * 31 + 9) < (curtain ? 0.008f : 0.03f);

        System.Threading.Tasks.Parallel.For(0, h, y =>
        {
        for (var x = 0; x < w; x++)
        {
            var u = x / (float)perU;
            var fl = y / perFloor;
            var ly = (y % perFloor) / (float)perFloor;
            var fx = u * s.Cols;
            var cellX = (int)fx;
            var lx = fx - cellX;
            // Salissures à grande échelle (périodiques sur la tuile) et grain fin.
            var grime = ValueNoise(x, y, 32, w / 32, variant + 5) * 0.6f + ValueNoise(x, y, 8, w / 8, variant + 9) * 0.4f;
            var grain = ValueNoise(x, y, 2, w / 2, variant + 21) * 0.5f + Hash(x, y, variant + 22) * 0.5f;
            var wh = Hash(cellX, fl, variant * 31 + 1);   // tirage par fenêtre
            var wh2 = Hash(cellX, fl, variant * 31 + 2);
            var wh3 = Hash(cellX, fl, variant * 31 + 3);
            var burnt = Burnt(cellX, fl);
            var broken = !burnt && Hash(cellX, fl, variant * 31 + 10) < (curtain ? 0.012f : 0.05f);
            // Suie : panache de la fenêtre de l'étage (au-dessus du linteau) ou de celle de l'étage du dessous.
            var sn = VNoise(x, y, 3, 16, w, variant + 31);
            var sootK = 0f;
            if (Burnt(cellX, fl) && ly < s.WinTop) sootK = Soot(s.WinTop - ly, lx, s.WinW, sn);
            if (Burnt(cellX, fl + 1)) sootK = Mathf.Max(sootK, Soot(1 - ly + s.WinTop, lx, s.WinW, sn));

            Color c;
            float r;
            float height;
            var e = new Color(0, 0, 0);

            bool inWin;
            bool inFrame;
            float wy; // position verticale dans la fenêtre (0 = haut)
            if (curtain)
            {
                var mull = lx < 0.035f || lx > 0.965f;
                var transom = ly > 0.775f && ly < 0.8f;
                var spandrel = ly >= 0.8f;
                inWin = !mull && !transom && !spandrel;
                inFrame = mull || transom || ly > 0.975f;
                wy = ly / 0.775f;
                if (!inWin && !inFrame)
                {
                    // Allège opaque (verre émaillé) au droit des dalles, encrassée.
                    c = s.Wall.Darkened(0.2f) * (0.9f + grime * 0.2f);
                    c = c.Lerp(soot, sootK * 0.9f);
                    r = 0.25f + grime * 0.2f;
                    height = 0.75f;
                    alb.Set(x, y, c); rough.Set(x, y, r); bump.Set(x, y, new Color(height, height, height));
                    emit?.Set(x, y, e);
                    continue;
                }
            }
            else
            {
                var half = s.WinW * 0.5f;
                var dx = Mathf.Abs(lx - 0.5f);
                inWin = dx < half && ly > s.WinTop && ly < s.WinBot;
                var fw = s.Kind == FKind.Ribbon ? 0.0f : 0.03f;
                inFrame = !inWin && dx < half + fw && ly > s.WinTop - 0.03f && ly < s.WinBot + 0.025f;
                if (s.Kind == FKind.Ribbon) inFrame = !inWin && ly > s.WinTop - 0.025f && ly < s.WinBot + 0.02f;
                // Montants et meneau central.
                if (inWin && s.Mullion && (Mathf.Abs(lx - 0.5f) < 0.012f || (s.Kind == FKind.Ribbon && (lx < 0.012f || lx > 0.988f)))) { inWin = false; inFrame = true; }
                wy = (ly - s.WinTop) / (s.WinBot - s.WinTop);
            }

            if (inWin)
            {
                // Stores intérieurs, intérieur sombre, reflet bleuté du ciel ; vitres brisées ou incendiées.
                var blinds = wh < 0.38f ? 0.15f + wh2 * 0.75f : 0f;
                var refl = Mathf.Lerp(0.34f, 0.12f, wy) * (0.7f + 0.6f * ValueNoise(x, y, 64, w / 64, variant + 11));
                var film = grime * 0.03f;
                Color glass = curtain
                    ? s.Wall * (0.85f + wh3 * 0.3f) + new Color(refl * 0.8f, refl * 0.95f, refl * 1.15f) * 0.5f
                    : new Color(0.05f, 0.065f, 0.085f) + new Color(refl * 0.6f, refl * 0.78f, refl * 1.0f);
                glass += new Color(film, film * 0.95f, film * 0.85f);
                r = 0.06f + grime * 0.08f;
                height = 0.2f;
                if (burnt)
                {
                    c = new Color(0.018f, 0.016f, 0.015f) * (0.8f + grain * 0.4f);
                    r = 0.95f;
                    height = 0.05f;
                }
                else if (broken)
                {
                    // Trou noir et quelques éclats de verre restés dans le cadre.
                    var shard = ValueNoise(x, y, 6, w / 6, variant + 41) > 0.62f && (wy < 0.25f || wy > 0.8f || Mathf.Abs(lx - 0.5f) > s.WinW * 0.33f);
                    c = shard ? glass : new Color(0.025f, 0.025f, 0.028f);
                    r = shard ? 0.1f : 0.9f;
                    height = shard ? 0.22f : 0.1f;
                }
                else if (wy < blinds)
                {
                    var slat = ((y % perFloor) % 3) == 0 ? 0.85f : 1f;
                    var bc = wh3 < 0.5f ? new Color(0.5f, 0.48f, 0.44f) : new Color(0.42f, 0.43f, 0.44f);
                    c = (bc * slat).Lerp(glass, curtain ? 0.65f : 0.35f);
                }
                else c = glass;
                c = c.Lerp(soot, sootK * 0.5f);
                if (night && !burnt && !broken && wh3 < s.LitRate)
                {
                    var warm = Hash(cellX, fl, variant * 31 + 7) < (curtain ? 0.25f : 0.75f);
                    var lc = warm ? new Color(1f, 0.74f, 0.42f) : new Color(0.66f, 0.72f, 0.78f);
                    var k = (0.5f + 0.4f * Hash(cellX, fl, variant * 31 + 8)) * (0.7f + 0.3f * (1 - wy));
                    if (wy < blinds) k *= 0.65f;
                    e = lc * k;
                }
            }
            else if (inFrame)
            {
                c = burnt ? new Color(0.04f, 0.036f, 0.033f) : s.Frame * (0.85f + grime * 0.25f);
                c = c.Lerp(soot, sootK * 0.8f);
                r = burnt ? 0.95f : 0.55f;
                height = curtain ? 0.6f : 0.45f;
            }
            else
            {
                // Mur : enduit, béton ou brique ; bandeau d'étage (ou joint de panneau), appuis, coulures, suie.
                var ledge = !s.Panels && ly > 0.955f;
                var sill = !curtain && Mathf.Abs(lx - 0.5f) < s.WinW * 0.5f + 0.045f && ly >= s.WinBot && ly < s.WinBot + 0.035f && s.Kind != FKind.Ribbon;
                var lintel = s.Kind == FKind.Brick && Mathf.Abs(lx - 0.5f) < s.WinW * 0.5f + 0.05f && ly > s.WinTop - 0.08f && ly <= s.WinTop - 0.03f;
                var panelTone = s.Panels ? 0.94f + Hash(cellX, fl, variant * 31 + 12) * 0.12f : 1f;
                c = s.Wall * panelTone * (0.93f + grain * 0.14f);
                height = 0.86f + grain * 0.08f;
                r = 0.9f;
                if (s.Kind == FKind.Brick && !ledge)
                {
                    var row = y / 3;
                    var off = (row % 2) * 5;
                    var col = (x + off) / 10;
                    var mortar = y % 3 == 0 || (x + off) % 10 == 0;
                    var bv = Hash(col, row, variant) - 0.5f;
                    var burned = Hash(col, row, variant + 77) < 0.12f; // briques plus cuites, presque noires
                    c = mortar ? new Color(0.4f, 0.39f, 0.37f) : s.Wall * (1f + bv * 0.3f) * (burned ? 0.6f : 1f);
                    height = mortar ? 0.78f : 0.9f + grain * 0.05f;
                }
                if (s.Panels)
                {
                    // Joints de panneaux préfabriqués : horizontaux au niveau des dalles, verticaux entre travées.
                    var joint = ly > 0.982f || ly < 0.006f || lx < 0.006f || lx > 0.994f;
                    var nearJ = Mathf.Min(Mathf.Min(1 - ly, ly), Mathf.Min(lx, 1 - lx) * 0.5f);
                    if (joint) { c = c * 0.45f; height = 0.7f; r = 0.95f; }
                    else if (nearJ < 0.05f) c = c.Darkened(0.12f * (1 - nearJ / 0.05f) * grime);
                }
                if (ledge) { c = s.Kind == FKind.Brick ? new Color(0.5f, 0.49f, 0.46f) : s.Wall.Darkened(0.14f); height = 1f; }
                if (sill || lintel) { c = new Color(0.55f, 0.54f, 0.51f) * (0.9f + grain * 0.15f); height = 1f; r = 0.75f; }
                // Coulures sous les appuis (sur l'étage, puis prolongées sur l'étage du dessous).
                if (!curtain)
                {
                    var down = ly > s.WinBot ? ly - s.WinBot : ly + 1 - s.WinBot;
                    var winAbove = ly > s.WinBot ? wh : Hash(cellX, ((fl - 1) % floors + floors) % floors, variant * 31 + 1);
                    var within = Mathf.Abs(lx - 0.5f) < s.WinW * 0.5f + 0.03f ? 1f : 0.25f;
                    var sc = VNoise(x, y, 2, 48, w, variant + 51) * 0.65f + VNoise(x, y, 5, 128, w, variant + 52) * 0.35f;
                    var streak = Mathf.Clamp((sc - 0.42f) * 2.4f, 0, 1) * Mathf.Exp(-down * 2.2f) * within * (0.45f + winAbove * 0.8f);
                    // Coulures sous le bandeau d'étage.
                    if (!s.Panels) streak = Mathf.Max(streak, Mathf.Clamp((sc - 0.55f) * 2f, 0, 1) * Mathf.Exp(-ly * 6f) * 0.6f);
                    c = c.Lerp(dirt, streak * 0.35f);
                    r -= streak * 0.15f;
                }
                // Impacts de balles et d'éclats, groupés en rafales : trou sombre, bord épaufré plus clair.
                if (ValueNoise(x, y, 64, w / 64, variant + 61) > 0.76f)
                {
                    int icx = x / 12, icy = y / 12;
                    if (Hash(icx, icy, variant + 62) < 0.45f)
                    {
                        var ox = icx * 12 + 3 + Hash(icx, icy, variant + 63) * 6;
                        var oy = icy * 12 + 3 + Hash(icx, icy, variant + 64) * 6;
                        var rad = 1.2f + Hash(icx, icy, variant + 65) * 2.2f;
                        var d = Mathf.Sqrt((x - ox) * (x - ox) + (y - oy) * (y - oy));
                        if (d < rad * 0.55f) { c = new Color(0.07f, 0.065f, 0.06f); height = 0.4f; r = 1f; }
                        else if (d < rad * 1.6f) { c = c.Lightened(0.18f); height = 0.75f; }
                    }
                }
                var g = (grime - 0.5f) * 0.1f;
                c = new Color(c.R + g, c.G + g, c.B + g * 0.9f);
                c = c.Lerp(soot, sootK);
            }
            alb.Set(x, y, c);
            rough.Set(x, y, r);
            bump.Set(x, y, new Color(height, height, height));
            emit?.Set(x, y, e);
        }
        }); // lignes calculées en parallèle (code C# pur, sans appel au moteur)

        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, curtain ? 1.5f : 3f), ImageTexture.CreateFromImage(rough.ToImage()), 0, new Vector3(1f / uSpan, 1f / floors, 1f))
            .With("roughness", 1f).With("metallic", curtain ? 0.35f : 0.03f).With("specular", 0.6f).With("normal_depth", 0.9f)
            .With("vertex_color", true).With("streaks", curtain ? 0.2f : 0.5f).With("grime", curtain ? 0.4f : 0.55f)
            // Pièces derrière les vitres (shader Weathered) : trame des fenêtres de la variante.
            .With("interior", curtain ? 2 : 1).With("win_rect", new Vector4(s.Cols, s.WinW * 0.5f, s.WinTop, s.WinBot))
            .With("cell_m", new Vector2(7f / s.Cols, 3.2f)).With("room_depth", curtain ? 5f : 3.6f);
        if (night)
        {
            mat.With("emission_tex", ImageTexture.CreateFromImage(emit.ToImage())).With("emission_energy", 1.4f);
        }
        Cache[key] = mat;
        return mat;
    }

    /// <summary>
    /// Vitrines du rez-de-chaussée : UV en mètres (1 U = une travée de 3,5 m, 1 V = hauteur du socle commercial).
    /// La texture couvre 4 travées : rideaux de fer baissés, vitrines barricadées (contreplaqué), vitres brisées.
    /// La nuit, seules les boutiques intactes sont éclairées.
    /// </summary>
    public static ShaderMaterial Storefront(bool night)
    {
        var key = $"storefront{night}";
        if (TryCache<ShaderMaterial>(key, out var cached)) return cached;
        const int bayW = 128, bays = 4, w = bayW * bays, h = 128;
        var alb = new Px(w, h);
        var rough = new Px(w, h, 1);
        var bump = new Px(w, h);
        var emit = night ? new Px(w, h, 3) : null;
        System.Threading.Tasks.Parallel.For(0, h, y =>
        {
        for (var x = 0; x < w; x++)
        {
            var bay = x / bayW;
            var lx = (x % bayW) / (float)bayW;
            var ly = y / (float)h;
            var state = Hash(bay, 0, 501);
            var grime = ValueNoise(x, y, 16, w / 16, 502) * 0.6f + ValueNoise(x, y, 4, w / 4, 503) * 0.4f;
            Color c;
            float r, ht;
            var e = new Color(0, 0, 0);
            var fascia = ly < 0.2f;
            var kick = ly > 0.91f;
            var frame = !fascia && !kick && (lx < 0.022f || lx > 0.978f || (ly >= 0.2f && ly < 0.235f) || Mathf.Abs(ly - 0.36f) < 0.012f || (ly > 0.885f) || Mathf.Abs(lx - 0.5f) < 0.008f);
            // Rideau de fer : entièrement baissé (state < 0.3) ou à mi-hauteur (state < 0.42).
            var shutterTo = state < 0.3f ? 0.91f : state < 0.42f ? 0.58f : 0f;
            var boarded = state >= 0.42f && state < 0.56f;
            var smashed = state >= 0.56f && state < 0.7f;
            if (fascia) { c = new Color(0.17f, 0.17f, 0.175f) * (0.85f + grime * 0.3f); r = 0.6f; ht = 1f; }
            else if (kick) { c = new Color(0.24f, 0.235f, 0.225f) * (0.85f + 0.2f * Hash(x / 8, y / 8, 3)); r = 0.8f; ht = 1f; }
            else if (ly < shutterTo && !(lx < 0.022f || lx > 0.978f))
            {
                // Lames de tôle galvanisée, rouillées vers le bas.
                var slat = (y % 5) == 0 ? 0.7f : 1f;
                var rust = Mathf.Clamp((ly - 0.4f) * 1.5f * (0.5f + grime), 0, 1);
                c = new Color(0.36f, 0.37f, 0.38f).Lerp(new Color(0.22f, 0.14f, 0.09f), rust * 0.6f) * slat * (0.85f + grime * 0.3f);
                r = 0.6f + rust * 0.3f;
                ht = (y % 5) == 0 ? 0.6f : 0.9f;
            }
            else if (frame) { c = new Color(0.1f, 0.1f, 0.105f); r = 0.5f; ht = 0.7f; }
            else if (boarded)
            {
                // Panneaux de contreplaqué grisé, vissés, avec traces d'eau.
                var sheet = (int)(lx * 2.0f);
                var seam = Mathf.Abs(lx * 2.0f - Mathf.Round(lx * 2.0f)) < 0.012f;
                var wood = ValueNoise(x * 0.25f, y * 3f, 4, 64, 504 + sheet) * 0.12f;
                c = seam ? new Color(0.1f, 0.09f, 0.08f) : new Color(0.38f, 0.34f, 0.28f) * (0.8f + wood + grime * 0.25f);
                r = 0.92f;
                ht = seam ? 0.5f : 0.85f;
            }
            else
            {
                // Intérieur : silhouettes de rayonnages et de produits derrière le verre (sale ou brisé).
                var shelfCol = x / 12;
                var top = 0.42f + Hash(shelfCol, 0, 17) * 0.3f;
                var shelf = ly > top && ly < 0.86f;
                var band = shelf && ((y / 6) % 3 == 0);
                var interior = shelf ? (band ? 0.12f : 0.05f + 0.06f * Hash(shelfCol, y / 6, 19)) : 0.07f;
                var refl = Mathf.Lerp(0.24f, 0.08f, (ly - 0.2f) / 0.7f);
                if (smashed && ValueNoise(x, y, 8, w / 8, 505) < 0.7f) refl = 0f;
                var film = grime * 0.06f;
                c = new Color(interior + refl * 0.6f + film, interior + refl * 0.65f + film, interior + refl * 0.7f + film * 0.9f);
                r = refl > 0 ? 0.08f + grime * 0.1f : 0.9f;
                ht = 0.3f;
                if (night && !smashed)
                {
                    var k = shelf ? (band ? 0.9f : 0.45f + 0.4f * Hash(shelfCol, y / 6, 23)) : 1f;
                    e = new Color(1f, 0.82f, 0.6f) * k * (ly < 0.36f ? 0.7f : 1f);
                }
            }
            alb.Set(x, y, c);
            rough.Set(x, y, r);
            bump.Set(x, y, new Color(ht, ht, ht));
            emit?.Set(x, y, e);
        }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 2f), ImageTexture.CreateFromImage(rough.ToImage()), 0, new Vector3(1f / bays, 1f, 1f))
            .With("roughness", 1f).With("specular", 0.6f).With("normal_depth", 0.7f).With("streaks", 0.6f).With("desat", 0.1f);
        if (night) mat.With("emission_tex", ImageTexture.CreateFromImage(emit.ToImage())).With("emission_energy", 0.55f);
        Cache[key] = mat;
        return mat;
    }

    // ───────────────────────── Voirie et détails ─────────────────────────

    /// <summary>
    /// Dallage de trottoir (4 m, 8 × 8 dalles de 50 cm à joints décalés) : dalles inégales et basculées, fendues,
    /// épaufrées ou manquantes (terre), joints encrassés, taches.
    /// </summary>
    private static ShaderMaterial Paving()
    {
        if (TryCache<ShaderMaterial>("paving", out var c)) return c;
        const int size = 512, tile = 64;
        var alb = new Px(size, size);
        var rough = new Px(size, size, 1);
        var bump = new Px(size, size);
        System.Threading.Tasks.Parallel.For(0, size, y =>
        {
            for (var x = 0; x < size; x++)
            {
                var ty = y / tile;
                var ox = x + (ty % 2) * (tile / 2); // pose à joints décalés
                var tx = (ox / tile) % (size / tile);
                var lx = (ox % tile) / (float)tile;
                var ly = (y % tile) / (float)tile;
                var joint = ox % tile < 2 || y % tile < 2;
                var t = Hash(tx, ty, 41) - 0.5f;
                var speck = Hash(x, y, 43);
                var stain = ValueNoise(x, y, 32, size / 32, 44) * 0.6f + ValueNoise(x, y, 8, size / 8, 47) * 0.4f;
                var missing = Hash(tx, ty, 45) < 0.035f;
                var cracked = Hash(tx, ty, 46) < 0.2f;
                // Dalle légèrement basculée : pente propre à chaque dalle.
                var sx = Hash(tx, ty, 48) - 0.5f;
                var sy = Hash(tx, ty, 49) - 0.5f;
                var v = 0.4f + t * 0.08f + (speck - 0.5f) * 0.05f - (stain - 0.5f) * 0.12f;
                var col = new Color(v, v * 0.985f, v * 0.955f);
                var hgt = 0.8f + (lx - 0.5f) * sx * 0.25f + (ly - 0.5f) * sy * 0.25f + speck * 0.04f;
                var r = 0.88f + (speck - 0.5f) * 0.08f;
                // Épaufrures aux coins.
                var corner = Mathf.Min(Mathf.Min(lx, 1 - lx), 0.5f) + Mathf.Min(Mathf.Min(ly, 1 - ly), 0.5f);
                var chip = corner < 0.08f + Hash(tx, ty, 50) * 0.1f && Hash(tx, ty, 51) < 0.5f;
                if (cracked)
                {
                    var a = Hash(tx, ty, 52) * Mathf.Pi;
                    var o = (Hash(tx, ty, 53) - 0.5f) * 0.4f;
                    var dl = Mathf.Abs((lx - 0.5f) * Mathf.Cos(a) + (ly - 0.5f) * Mathf.Sin(a) - o + (ValueNoise(x, y, 4, size / 4, 54) - 0.5f) * 0.05f);
                    if (dl < 0.014f) { col = new Color(0.1f, 0.095f, 0.085f); hgt = 0.35f; r = 1f; }
                    else if (dl < 0.04f) col *= 0.85f;
                }
                if (chip) { col *= 0.62f; hgt = 0.5f; r = 0.95f; }
                if (joint) { col = new Color(0.15f, 0.145f, 0.12f) * (0.8f + speck * 0.4f); hgt = 0.25f; r = 1f; }
                if (missing)
                {
                    var g = ValueNoise(x, y, 3, size / 3, 55);
                    col = new Color(0.19f, 0.165f, 0.13f) * (0.7f + g * 0.5f);
                    hgt = 0.15f + g * 0.2f;
                    r = 1f;
                }
                alb.Set(x, y, col);
                rough.Set(x, y, r);
                bump.Set(x, y, new Color(hgt, hgt, hgt));
            }
        });
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 4f), ImageTexture.CreateFromImage(rough.ToImage()), 2, Vector3.One / 4f)
            .With("roughness", 1f).With("normal_depth", 0.9f).With("puddles", 0.55f).With("foot", 0f);
        Cache["paving"] = mat;
        return mat;
    }

    /// <summary>Peinture routière (marquages, passages piétons) usée et encrassée.</summary>
    public static StandardMaterial3D RoadPaint()
    {
        if (TryCache<StandardMaterial3D>("roadpaint", out var c)) return c;
        // Peinture effacée par plaques (l'enrobé réapparaît), grain et salissures.
        const int s = 256;
        var px = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                var wear = ValueNoise(x, y, 32, s / 32, 142) * 0.5f + ValueNoise(x, y, 8, s / 8, 143) * 0.3f + ValueNoise(x, y, 2, s / 2, 144) * 0.2f;
                var grain = Hash(x, y, 145);
                var paint = 0.42f + grain * 0.08f - ValueNoise(x, y, 16, s / 16, 146) * 0.1f;
                var v = Mathf.Lerp(0.12f + grain * 0.04f, paint, Mathf.SmoothStep(0.36f, 0.46f, wear));
                px.Set(x, y, new Color(v, v * 0.99f, v * 0.96f));
            }
        });
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(px.ToImage()),
            Roughness = 0.8f,
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = new Vector3(0.15f, 0.15f, 0.15f),
        };
        Cache["roadpaint"] = mat;
        return mat;
    }

    /// <summary>Bordure de trottoir en granit, plus sombre que le dallage.</summary>
    public static ShaderMaterial Curb()
    {
        if (TryCache<ShaderMaterial>("curb", out var c)) return c;
        var mat = (ShaderMaterial)Concrete(new Color(0.36f, 0.36f, 0.355f), 151).Duplicate();
        mat.With("uv_scale", new Vector3(0.8f, 0.8f, 0.8f)).With("roughness", 0.85f);
        Cache["curb"] = mat;
        return mat;
    }

    /// <summary>Matériau de détail : teinte par sommet × grain de béton, salissures (fusion de nombreux petits éléments).</summary>
    public static ShaderMaterial Detail()
    {
        if (TryCache<ShaderMaterial>("detail", out var c)) return c;
        var mat = Weathered(ColorNoise(161, 0.05f, new Color(0.74f, 0.74f, 0.74f), new Color(1f, 1f, 1f), 256), Noise(162, 0.1f, true, 256, 2f), null, 1, new Vector3(0.5f, 0.5f, 0.5f))
            .With("roughness", 0.86f).With("normal_depth", 0.5f).With("vertex_color", true).With("streaks", 0.7f).With("foot", 0.45f).With("desat", 0.3f);
        Cache["detail"] = mat;
        return mat;
    }

    /// <summary>Variante métallique du matériau de détail (poteaux, rambardes, carrosseries) : peinture terne, rouille.</summary>
    public static ShaderMaterial DetailMetal()
    {
        if (TryCache<ShaderMaterial>("detailmetal", out var c)) return c;
        var mat = Weathered(ColorNoise(163, 0.12f, new Color(0.78f, 0.78f, 0.78f), new Color(1f, 1f, 1f), 256), Noise(164, 0.2f, true, 256, 1.5f), null, 1, new Vector3(0.7f, 0.7f, 0.7f))
            .With("roughness", 0.62f).With("metallic", 0.4f).With("normal_depth", 0.4f).With("vertex_color", true)
            .With("streaks", 0.6f).With("foot", 0.4f).With("desat", 0.3f).With("rust", 0.45f);
        Cache["detailmetal"] = mat;
        return mat;
    }

    /// <summary>Feuillage : couleur par sommet, marbrures, visible des deux côtés (palmes, haies, résineux).</summary>
    public static StandardMaterial3D Foliage()
    {
        if (TryCache<StandardMaterial3D>("foliage", out var c)) return c;
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoTexture = ColorNoise(181, 0.25f, new Color(0.55f, 0.56f, 0.52f), new Color(1f, 1f, 1f), 256),
            NormalEnabled = true,
            NormalTexture = Noise(182, 0.35f, true, 256, 6f),
            NormalScale = 0.9f,
            Roughness = 0.9f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.7f, 0.7f, 0.7f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BacklightEnabled = true,
            Backlight = new Color(0.08f, 0.1f, 0.05f),
        };
        Cache["foliage"] = mat;
        return mat;
    }

    /// <summary>
    /// Feuillage ajouré des houppiers : touffes en découpe (alpha scissor sur un bruit cellulaire triplanaire),
    /// silhouette irrégulière et trouée au lieu d'une boule pleine. Jamais solide (comme <see cref="Foliage"/>).
    /// </summary>
    public static StandardMaterial3D Leaves() => LeafMat("leaves", 0.5f, 0.42f);

    /// <summary>Ramilles des arbres morts : même découpe, beaucoup plus clairsemée.</summary>
    public static StandardMaterial3D Twigs() => LeafMat("twigs", 0.6f, 1.1f);

    private static StandardMaterial3D LeafMat(string key, float threshold, float scale)
    {
        if (TryCache<StandardMaterial3D>(key, out var c)) return c;
        const int s = 256;
        var px = new Px(s, s);
        System.Threading.Tasks.Parallel.For(0, s, y =>
        {
            for (var x = 0; x < s; x++)
            {
                // Touffes (bruit à deux échelles) et feuilles ; trous entre les touffes.
                var clump = ValueNoise(x, y, 64, s / 64, 601) * 0.5f + ValueNoise(x, y, 16, s / 16, 602) * 0.3f;
                var leaf = ValueNoise(x, y, 4, s / 4, 603);
                var a = clump * 0.82f + leaf * 0.3f;
                var v = 0.55f + leaf * 0.45f;
                px.Set(x, y, new Color(v, v, v * 0.95f, Mathf.Clamp(a, 0, 1)));
            }
        });
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoTexture = ImageTexture.CreateFromImage(px.ToImage()),
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = threshold,
            AlphaAntialiasingMode = BaseMaterial3D.AlphaAntiAliasing.Off,
            NormalEnabled = true,
            NormalTexture = Noise(604, 0.35f, true, 256, 7f),
            NormalScale = 1f,
            Roughness = 0.9f,
            Uv1Triplanar = true,
            Uv1Scale = Vector3.One * scale,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BacklightEnabled = true,
            Backlight = new Color(0.06f, 0.07f, 0.04f),
        };
        Cache[key] = mat;
        return mat;
    }

    /// <summary>Feuillage (jamais solide) : <see cref="Foliage"/> ou <see cref="Leaves"/>.</summary>
    public static bool IsFoliage(Material m) => m == Foliage() || m == Leaves() || m == Twigs();

    /// <summary>Toile teintée par instance de MultiMesh (parasols, serviettes), visible des deux côtés.</summary>
    public static StandardMaterial3D Instanced()
    {
        if (TryCache<StandardMaterial3D>("instanced", out var c)) return c;
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoTexture = ColorNoise(171, 0.3f, new Color(0.7f, 0.7f, 0.7f), new Color(0.92f, 0.92f, 0.92f), 128),
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(2f, 2f, 2f),
            Roughness = 0.95f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        Cache["instanced"] = mat;
        return mat;
    }

    /// <summary>Feux et voyants : non éclairé, couleur par sommet.</summary>
    public static StandardMaterial3D Lamps()
    {
        if (TryCache<StandardMaterial3D>("lamps", out var c)) return c;
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        Cache["lamps"] = mat;
        return mat;
    }

    /// <summary>Verre transparent (abribus, garde-corps de balcon), terni.</summary>
    public static StandardMaterial3D Glass()
    {
        if (TryCache<StandardMaterial3D>("glass", out var c)) return c;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.42f, 0.46f, 0.47f, 0.38f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.2f,
            Metallic = 0.2f,
            MetallicSpecular = 0.7f,
        };
        Cache["glass"] = mat;
        return mat;
    }

    /// <summary>Platelage en bois (ponton) : lames de 15 cm grisées par les intempéries, usure.</summary>
    public static ShaderMaterial Planks()
    {
        if (TryCache<ShaderMaterial>("planks", out var c)) return c;
        const int size = 256; // 2 m
        var alb = new Px(size, size);
        var bump = new Px(size, size);
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var board = y / 19;
            var gap = y % 19 < 2;
            var seg = (x + board * 37) / 128;
            var bv = Hash(board, seg, 71);
            var grain = ValueNoise(x * 0.25f, y * 4f, 4, 16, 72) * 0.12f;
            var v = gap ? new Color(0.06f, 0.055f, 0.05f) : new Color(0.36f, 0.32f, 0.27f) * (0.75f + bv * 0.35f + grain);
            alb.Set(x, y, v);
            var hh = gap ? 0.1f : 0.9f;
            bump.Set(x, y, new Color(hh, hh, hh));
        }
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 4f), null, 2, new Vector3(0.5f, 0.5f, 0.5f))
            .With("roughness", 0.88f).With("puddles", 0.4f);
        Cache["planks"] = mat;
        return mat;
    }

    /// <summary>Carrosserie calcinée (épaves) : tôle noircie et rouillée.</summary>
    public static readonly Color BurntPaint = new(0.1f, 0.078f, 0.064f);

    /// <summary>Peinture de carrosserie ternie : vernis mat, poussière, bas de caisse crotté, rouille.</summary>
    public static ShaderMaterial CarPaint(Color c)
    {
        var key = $"carpaint{c}";
        if (TryCache<ShaderMaterial>(key, out var m)) return m;
        var mat = Weathered(ColorNoise(191, 0.08f, new Color(0.8f, 0.8f, 0.8f), new Color(1f, 1f, 1f), 256), Noise(192, 0.3f, true, 256, 1f), null, 1, new Vector3(0.6f, 0.6f, 0.6f))
            .With("albedo", c).With("roughness", 0.5f).With("metallic", 0.3f).With("specular", 0.5f).With("normal_depth", 0.3f)
            .With("grime", 1.1f).With("foot", 1.3f).With("streaks", 0.6f).With("desat", 0.35f).With("rust", 0.3f);
        if (c == BurntPaint) mat.With("rust", 1.4f).With("roughness", 0.95f).With("metallic", 0.1f).With("desat", 0.1f);
        Cache[key] = mat;
        return mat;
    }

    public static ShaderMaterial Roof()
    {
        if (TryCache<ShaderMaterial>("roof", out var c)) return c;
        var mat = Concrete(new Color(0.3f, 0.295f, 0.285f), 81);
        Cache["roof"] = mat;
        return mat;
    }

    public static StandardMaterial3D Painted(Color c, float rough = 0.5f, float metal = 0f)
    {
        var key = $"paint{c}{rough}{metal}";
        if (TryCache<StandardMaterial3D>(key, out var m)) return m;
        var mat = new StandardMaterial3D { AlbedoColor = c, Roughness = rough, Metallic = metal };
        Cache[key] = mat;
        return mat;
    }

    /// <summary>Peinture industrielle écaillée et salie (grues, cuves, bandes de quai), en coordonnées monde.</summary>
    public static ShaderMaterial Worn(Color c, float rough = 0.6f, float metal = 0.3f)
    {
        var key = $"worn{c}{rough}{metal}";
        if (TryCache<ShaderMaterial>(key, out var m)) return m;
        var mat = Weathered(ColorNoise(195, 0.06f, new Color(0.72f, 0.72f, 0.72f), new Color(1f, 1f, 1f), 256), Noise(196, 0.15f, true, 256, 1.5f), null, 2, new Vector3(0.3f, 0.3f, 0.3f))
            .With("albedo", c).With("roughness", rough).With("metallic", metal).With("normal_depth", 0.4f)
            .With("desat", 0.3f).With("rust", 0.5f).With("grime", 1.1f);
        Cache[key] = mat;
        return mat;
    }

    public static StandardMaterial3D Glow(Color c, float energy = 3f)
    {
        var key = $"glow{c}{energy}";
        if (TryCache<StandardMaterial3D>(key, out var m)) return m;
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
        if (TryCache<StandardMaterial3D>(key, out var m)) return m;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(101, 0.06f, c.Darkened(0.35f), c.Lightened(0.08f), 256),
            NormalEnabled = true,
            NormalTexture = Noise(102, 0.12f, true, 256, 6f),
            NormalScale = 0.8f,
            // Chitine brillante mais pas miroir : sous le grand ciel bleu, un vernis trop net rendait les fourmis noires bleues.
            Roughness = 0.55f,
            Metallic = 0.05f,
            MetallicSpecular = 0.2f,
            ClearcoatEnabled = true,
            Clearcoat = 0.3f,
            ClearcoatRoughness = 0.18f,
            RimEnabled = true,
            Rim = 0.18f,
            RimTint = 0.8f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.6f, 0.6f, 0.6f),
        };
        Cache[key] = mat;
        return mat;
    }

    public static StandardMaterial3D Fabric(Color c)
    {
        var key = $"fabric{c}";
        if (TryCache<StandardMaterial3D>(key, out var m)) return m;
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
        if (TryCache<StandardMaterial3D>(key, out var m)) return m;
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
        if (TryCache<ShaderMaterial>(key, out var m)) return m;
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

    // ───────────────────────── Banlieue ─────────────────────────

    /// <summary>
    /// Tuiles (ou ardoises) teintées par sommet : rangs de 25 cm à joints décalés, ombre sous le rang supérieur,
    /// tuiles cassées ou manquantes, mousse et lichen. UV en mètres / 2 : U le long de l'égout, V vers le bas.
    /// </summary>
    public static ShaderMaterial RoofTiles()
    {
        if (TryCache<ShaderMaterial>("rooftiles", out var c)) return c;
        const int size = 256; // 2 m : 8 rangs de 32 px, tuiles de 32 px de large
        var alb = new Px(size, size);
        var bump = new Px(size, size);
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var row = y / 32;
            var ly = (y % 32) / 32f;
            var ox = x + (row % 2) * 16;
            var col = (ox / 32) % 8;
            var lx = (ox % 32) / 32f;
            var tile = Hash(col, row, 201);
            var missing = Hash(col, row, 204) < 0.03f;
            // Profil galbé de la tuile, épaississement vers son bord bas, ombre portée du rang du dessus.
            var round = Mathf.Sin(lx * Mathf.Pi);
            var shade = Mathf.Lerp(0.5f, 1f, Mathf.SmoothStep(0f, 0.3f, ly));
            var grain = ValueNoise(x, y, 8, size / 8, 202) * 0.12f;
            var moss = Mathf.Max(0, ValueNoise(x, y, 32, size / 32, 203) - 0.62f) * 1.6f;
            var v = (0.62f + tile * 0.24f + grain) * shade * (0.82f + 0.18f * round);
            var cc = new Color(v, v, v).Lerp(new Color(0.3f, 0.32f, 0.24f), Mathf.Clamp(moss, 0, 0.8f));
            var hh = 0.25f + 0.55f * ly + 0.2f * round;
            if (lx < 0.04f) hh *= 0.6f;
            if (missing) { cc = new Color(0.12f, 0.1f, 0.09f); hh = 0.05f; } // trou : liteaux et ombre
            alb.Set(x, y, cc);
            bump.Set(x, y, new Color(hh, hh, hh));
        }
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 5f), null)
            .With("roughness", 0.8f).With("normal_depth", 0.9f).With("vertex_color", true).With("streaks", 0.8f).With("desat", 0.35f);
        Cache["rooftiles"] = mat;
        return mat;
    }

    /// <summary>Maçonnerie de moellons équarris (église, ponts, murets), teintée par sommet, triplanaire en coordonnées monde.</summary>
    public static ShaderMaterial Masonry()
    {
        if (TryCache<ShaderMaterial>("masonry", out var c)) return c;
        const int size = 256; // 2 m
        var alb = new Px(size, size);
        var bump = new Px(size, size);
        // Assises de hauteur variable, blocs de longueur variable.
        var rowTop = new List<int> { 0 };
        while (rowTop[^1] < size)
        {
            var hgt = 26 + (int)(Hash(rowTop.Count, 0, 211) * 18);
            rowTop.Add(Mathf.Min(size, rowTop[^1] + hgt));
        }
        rowTop[^1] = size;
        for (var r = 0; r + 1 < rowTop.Count; r++)
        {
            var y0 = rowTop[r];
            var y1 = rowTop[r + 1];
            var x = (int)(Hash(r, 1, 212) * 40);
            var bi = 0;
            while (x < size + 60)
            {
                var len = 34 + (int)(Hash(r, bi, 213) * 50);
                var tone = 0.66f + Hash(r, bi, 214) * 0.3f;
                for (var y = y0; y < y1; y++)
                for (var xx = x; xx < x + len; xx++)
                {
                    var px = ((xx % size) + size) % size;
                    var joint = y - y0 < 3 || xx - x < 3;
                    var n = ValueNoise(px, y, 6, size / 6, 215) * 0.18f;
                    var v = joint ? 0.42f : tone * (0.84f + n);
                    alb.Set(px, y, new Color(v, v * 0.98f, v * 0.95f));
                    var edge = Mathf.Min(Mathf.Min(y - y0, y1 - 1 - y), Mathf.Min(xx - x, x + len - 1 - xx));
                    var hh = joint ? 0.1f : Mathf.Clamp(0.5f + edge * 0.08f, 0, 0.9f) + n;
                    bump.Set(px, y, new Color(hh, hh, hh));
                }
                x += len;
                bi++;
            }
        }
        var mat = Weathered(ImageTexture.CreateFromImage(alb.ToImage()), BumpToNormal(bump, 5f), null, 2, new Vector3(0.5f, 0.5f, 0.5f))
            .With("roughness", 0.92f).With("vertex_color", true).With("desat", 0.3f);
        Cache["masonry"] = mat;
        return mat;
    }

    /// <summary>Vitrail : losanges colorés sombres, lumineux la nuit.</summary>
    public static StandardMaterial3D StainedGlass(bool night)
    {
        var key = $"stained{night}";
        if (TryCache<StandardMaterial3D>(key, out var m)) return m;
        const int size = 64;
        var alb = new Px(size, size);
        Color[] pal = { new(0.1f, 0.18f, 0.45f), new(0.5f, 0.08f, 0.08f), new(0.55f, 0.42f, 0.1f), new(0.12f, 0.32f, 0.18f), new(0.25f, 0.12f, 0.35f) };
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var u = x + y;
            var v = x - y + size;
            var lead = u % 16 < 2 || v % 16 < 2;
            var c = lead ? new Color(0.03f, 0.03f, 0.03f) : pal[(int)(Hash(u / 16, v / 16, 221) * pal.Length) % pal.Length];
            alb.Set(x, y, c);
        }
        var tex = ImageTexture.CreateFromImage(alb.ToImage());
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = tex,
            Roughness = 0.15f,
            Metallic = 0.2f,
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = new Vector3(1.5f, 1.5f, 1.5f),
        };
        if (night)
        {
            mat.EmissionEnabled = true;
            mat.EmissionTexture = tex;
            mat.EmissionEnergyMultiplier = 1.6f;
        }
        Cache[key] = mat;
        return mat;
    }

    /// <summary>Gravier et galets (allées, berges), teinté par sommet.</summary>
    public static ShaderMaterial Gravel()
    {
        if (TryCache<ShaderMaterial>("gravel", out var c)) return c;
        var mat = Weathered(ColorNoise(231, 0.6f, new Color(0.5f, 0.49f, 0.46f), new Color(0.9f, 0.88f, 0.85f), 256), Noise(232, 0.5f, true, 256, 8f, FastNoiseLite.NoiseTypeEnum.Cellular, 2), null, 2, new Vector3(0.35f, 0.35f, 0.35f))
            .With("roughness", 0.95f).With("normal_depth", 0.8f).With("vertex_color", true).With("puddles", 0.6f).With("desat", 0.3f);
        Cache["gravel"] = mat;
        return mat;
    }

    /// <summary>
    /// Sol naturel teinté par sommet (pelouses, prés, sous-bois) : herbe morte neutre (paille, touffes, boue)
    /// multipliée par la couleur, désaturée.
    /// </summary>
    public static ShaderMaterial GroundTint()
    {
        if (TryCache<ShaderMaterial>("groundtint", out var c)) return c;
        var (alb, bump, rough) = DeadGrass(true);
        var mat = Weathered(alb, bump, rough, 2, Vector3.One / 8f)
            .With("roughness", 1f).With("vertex_color", true).With("puddles", 0.45f).With("desat", 0.4f).With("streaks", 0f);
        Cache["groundtint"] = mat;
        return mat;
    }

    /// <summary>
    /// Eau de rivière peu profonde : transparente, courant vers +X, opacité par sommet (alpha) pour fondre les berges.
    /// </summary>
    public static ShaderMaterial RiverWater(TimeOfDay time)
    {
        var key = $"river{time}";
        if (TryCache<ShaderMaterial>(key, out var m)) return m;
        var shader = new Shader
        {
            Code = @"
shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_opaque;
uniform vec3 deep : source_color = vec3(0.025, 0.07, 0.075);
uniform vec3 shallow : source_color = vec3(0.13, 0.19, 0.15);
uniform sampler2D n1 : hint_normal, filter_linear_mipmap, repeat_enable;
uniform sampler2D n2 : hint_normal, filter_linear_mipmap, repeat_enable;
varying vec3 wpos;
void vertex() {
    wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}
void fragment() {
    vec2 uv = wpos.xz * 0.11;
    vec3 a = texture(n1, uv + vec2(TIME * 0.045, 0.004 * TIME)).rgb;
    vec3 b = texture(n2, uv * 1.9 + vec2(TIME * 0.07, -0.01 * TIME)).rgb;
    NORMAL_MAP = normalize(mix(a, b, 0.5));
    NORMAL_MAP_DEPTH = 0.9;
    float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
    ALBEDO = mix(shallow, deep, COLOR.a);
    ALPHA = clamp(COLOR.a * (0.78 + 0.2 * fres), 0.0, 1.0);
    ROUGHNESS = 0.02;
    METALLIC = 0.0;
    SPECULAR = 0.65;
}"
        };
        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("n1", Noise(251, 0.03f, true, 512, 5f));
        mat.SetShaderParameter("n2", Noise(252, 0.05f, true, 512, 3f));
        if (time == TimeOfDay.Night)
        {
            mat.SetShaderParameter("deep", new Color(0.01f, 0.02f, 0.025f));
            mat.SetShaderParameter("shallow", new Color(0.03f, 0.05f, 0.05f));
        }
        Cache[key] = mat;
        return mat;
    }

    public static void ClearNightVariants()
    {
        // Les façades nuit/jour sont mises en cache séparément : rien à faire pour l'instant.
    }
}
