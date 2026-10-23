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
        var mat = Paving();
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

    // ───────────────────────── Façades ─────────────────────────

    /// <summary>Nombre de variantes de façade disponibles (0-5 : génériques, 6-7 : murs-rideaux, 8-9 : résidentiel).</summary>
    public const int FacadeCount = 10;

    private enum FKind { Punched, Ribbon, Curtain, Brick }

    private readonly record struct FStyle(FKind Kind, Color Wall, int Cols, float WinW, float WinTop, float WinBot, Color Frame, bool Mullion, float LitRate);

    private static FStyle StyleOf(int v) => v switch
    {
        0 => new(FKind.Punched, new Color(0.62f, 0.6f, 0.56f), 2, 0.5f, 0.26f, 0.8f, new Color(0.22f, 0.22f, 0.23f), true, 0.32f),
        1 => new(FKind.Punched, new Color(0.6f, 0.49f, 0.41f), 3, 0.5f, 0.24f, 0.8f, new Color(0.82f, 0.8f, 0.76f), false, 0.35f),
        2 => new(FKind.Ribbon, new Color(0.45f, 0.47f, 0.5f), 4, 1f, 0.3f, 0.78f, new Color(0.14f, 0.15f, 0.16f), true, 0.4f),
        3 => new(FKind.Punched, new Color(0.72f, 0.68f, 0.6f), 2, 0.58f, 0.22f, 0.82f, new Color(0.32f, 0.26f, 0.2f), true, 0.3f),
        4 => new(FKind.Brick, new Color(0.42f, 0.32f, 0.27f), 3, 0.5f, 0.24f, 0.82f, new Color(0.86f, 0.85f, 0.82f), false, 0.35f),
        5 => new(FKind.Ribbon, new Color(0.56f, 0.58f, 0.56f), 3, 1f, 0.32f, 0.8f, new Color(0.62f, 0.64f, 0.65f), true, 0.38f),
        6 => new(FKind.Curtain, new Color(0.1f, 0.16f, 0.2f), 5, 1f, 0f, 0.8f, new Color(0.55f, 0.58f, 0.6f), true, 0.45f),
        7 => new(FKind.Curtain, new Color(0.27f, 0.25f, 0.21f), 4, 1f, 0f, 0.8f, new Color(0.3f, 0.29f, 0.27f), true, 0.45f),
        8 => new(FKind.Brick, new Color(0.55f, 0.26f, 0.19f), 3, 0.48f, 0.2f, 0.84f, new Color(0.9f, 0.9f, 0.88f), true, 0.35f),
        _ => new(FKind.Punched, new Color(0.86f, 0.85f, 0.81f), 2, 0.42f, 0.16f, 0.95f, new Color(0.24f, 0.25f, 0.26f), true, 0.35f),
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

    /// <summary>
    /// Façade d'immeuble : texture de fenêtres générée, UV en mètres (1 U = 7 m, 1 V = un étage de 3,2 m).
    /// La texture couvre 2 U × 4 étages pour varier stores et fenêtres éclairées d'un étage à l'autre.
    /// </summary>
    public static StandardMaterial3D Facade(int variant, bool night)
    {
        var key = $"facade{variant}{night}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var s = StyleOf(variant);
        const int perU = 256, perFloor = 128, uSpan = 2, floors = 4;
        const int w = perU * uSpan, h = perFloor * floors;
        var alb = new Px(w, h);
        var rough = new Px(w, h, 1);
        var bump = new Px(w, h);
        var emit = night ? new Px(w, h, 3) : null;
        var curtain = s.Kind == FKind.Curtain;

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
            // Salissures à grande échelle (périodiques sur la tuile).
            var grime = ValueNoise(x, y, 32, w / 32, variant + 5) * 0.6f + ValueNoise(x, y, 8, w / 8, variant + 9) * 0.4f;
            var wh = Hash(cellX, fl, variant * 31 + 1);   // tirage par fenêtre
            var wh2 = Hash(cellX, fl, variant * 31 + 2);
            var wh3 = Hash(cellX, fl, variant * 31 + 3);

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
                    // Allège opaque (verre émaillé) au droit des dalles.
                    c = s.Wall.Darkened(0.25f) + new Color(0.02f, 0.02f, 0.02f) * grime;
                    r = 0.18f;
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
                // Stores intérieurs (fraction de la hauteur), intérieur sombre, reflet du ciel.
                var blinds = wh < 0.38f ? 0.15f + wh2 * 0.75f : 0f;
                var refl = Mathf.Lerp(0.34f, 0.12f, wy) * (0.7f + 0.6f * ValueNoise(x, y, 64, w / 64, variant + 11));
                Color glass = curtain
                    ? s.Wall * (0.85f + wh3 * 0.3f) + new Color(refl, refl * 1.05f, refl * 1.15f) * 0.5f
                    : new Color(0.07f, 0.08f, 0.09f) + new Color(refl * 0.75f, refl * 0.85f, refl);
                if (wy < blinds)
                {
                    var slat = ((y % perFloor) % 3) == 0 ? 0.85f : 1f;
                    var bc = wh3 < 0.5f ? new Color(0.78f, 0.75f, 0.68f) : new Color(0.62f, 0.64f, 0.66f);
                    c = (bc * slat).Lerp(glass, curtain ? 0.65f : 0.35f);
                }
                else c = glass;
                r = 0.05f;
                height = 0.2f;
                if (night && wh3 < s.LitRate)
                {
                    var warm = Hash(cellX, fl, variant * 31 + 7) < (curtain ? 0.25f : 0.75f);
                    var lc = warm ? new Color(1f, 0.76f, 0.45f) : new Color(0.7f, 0.78f, 0.86f);
                    var k = (0.55f + 0.45f * Hash(cellX, fl, variant * 31 + 8)) * (0.7f + 0.3f * (1 - wy));
                    if (wy < blinds) k *= 0.65f;
                    e = lc * k;
                }
            }
            else if (inFrame)
            {
                c = s.Frame;
                r = 0.35f;
                height = curtain ? 0.6f : 0.45f;
            }
            else
            {
                // Mur : enduit, béton ou brique, bandeau d'étage et coulures sous les appuis.
                var ledge = ly > 0.955f;
                var sill = !curtain && Mathf.Abs(lx - 0.5f) < s.WinW * 0.5f + 0.045f && ly >= s.WinBot && ly < s.WinBot + 0.035f && s.Kind != FKind.Ribbon;
                var lintel = s.Kind == FKind.Brick && Mathf.Abs(lx - 0.5f) < s.WinW * 0.5f + 0.05f && ly > s.WinTop - 0.08f && ly <= s.WinTop - 0.03f;
                c = s.Wall;
                height = 0.9f;
                r = 0.88f;
                if (s.Kind == FKind.Brick && !ledge)
                {
                    var row = y / 3;
                    var off = (row % 2) * 5;
                    var col = (x + off) / 10;
                    var mortar = y % 3 == 0 || (x + off) % 10 == 0;
                    var bv = Hash(col, row, variant) - 0.5f;
                    c = mortar ? new Color(0.62f, 0.6f, 0.56f) : s.Wall * (1f + bv * 0.28f);
                    height = mortar ? 0.8f : 0.92f;
                }
                if (ledge) { c = s.Kind == FKind.Brick ? new Color(0.7f, 0.68f, 0.63f) : s.Wall.Darkened(0.12f); height = 1f; }
                if (sill || lintel) { c = new Color(0.78f, 0.76f, 0.72f); height = 1f; r = 0.7f; }
                // Coulures sous les fenêtres.
                if (!curtain && Mathf.Abs(lx - 0.5f) < s.WinW * 0.5f && ly > s.WinBot + 0.035f && ly < s.WinBot + 0.3f)
                    c = c.Darkened(0.12f * (1 - (ly - s.WinBot) / 0.3f) * (0.5f + wh));
                var g = (grime - 0.5f) * 0.09f;
                c = new Color(c.R + g, c.G + g, c.B + g * 0.9f);
            }
            alb.Set(x, y, c);
            rough.Set(x, y, r);
            bump.Set(x, y, new Color(height, height, height));
            emit?.Set(x, y, e);
        }
        }); // lignes calculées en parallèle (code C# pur, sans appel au moteur)

        var nimg = bump.ToImage(false);
        nimg.BumpMapToNormalMap(curtain ? 1.5f : 3f);
        nimg.GenerateMipmaps();
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(alb.ToImage()),
            RoughnessTexture = ImageTexture.CreateFromImage(rough.ToImage()),
            Roughness = 1f,
            Metallic = curtain ? 0.35f : 0.05f,
            MetallicSpecular = 0.6f,
            NormalEnabled = true,
            NormalTexture = ImageTexture.CreateFromImage(nimg),
            NormalScale = 0.8f,
            Uv1Scale = new Vector3(1f / uSpan, 1f / floors, 1f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        if (night)
        {
            mat.EmissionEnabled = true;
            mat.EmissionTexture = ImageTexture.CreateFromImage(emit.ToImage());
            mat.EmissionEnergyMultiplier = 1.4f;
        }
        Cache[key] = mat;
        return mat;
    }

    /// <summary>
    /// Vitrines du rez-de-chaussée : UV en mètres (1 U = une travée de 3,5 m, 1 V = hauteur du socle commercial).
    /// La nuit, l'intérieur des boutiques est éclairé.
    /// </summary>
    public static StandardMaterial3D Storefront(bool night)
    {
        var key = $"storefront{night}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        const int w = 256, h = 128;
        var alb = new Px(w, h);
        var rough = new Px(w, h, 1);
        var bump = new Px(w, h);
        var emit = night ? new Px(w, h, 3) : null;
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var lx = x / (float)w;
            var ly = y / (float)h;
            Color c;
            float r, ht;
            var e = new Color(0, 0, 0);
            var fascia = ly < 0.2f;
            var kick = ly > 0.91f;
            var frame = !fascia && !kick && (lx < 0.022f || lx > 0.978f || (ly >= 0.2f && ly < 0.235f) || Mathf.Abs(ly - 0.36f) < 0.012f || (ly > 0.885f) || Mathf.Abs(lx - 0.5f) < 0.008f);
            if (fascia) { c = new Color(0.2f, 0.2f, 0.21f); r = 0.5f; ht = 1f; }
            else if (kick) { c = new Color(0.28f, 0.27f, 0.26f) * (0.9f + 0.2f * Hash(x / 8, y / 8, 3)); r = 0.6f; ht = 1f; }
            else if (frame) { c = new Color(0.12f, 0.12f, 0.13f); r = 0.3f; ht = 0.7f; }
            else
            {
                // Intérieur : silhouettes de rayonnages et de produits derrière le verre.
                var shelfCol = x / 12;
                var top = 0.42f + Hash(shelfCol, 0, 17) * 0.3f;
                var shelf = ly > top && ly < 0.86f;
                var band = shelf && ((y / 6) % 3 == 0);
                var interior = shelf ? (band ? 0.16f : 0.07f + 0.08f * Hash(shelfCol, y / 6, 19)) : 0.1f;
                var refl = Mathf.Lerp(0.3f, 0.1f, (ly - 0.2f) / 0.7f);
                c = new Color(interior + refl * 0.6f, interior + refl * 0.68f, interior + refl * 0.78f);
                r = 0.04f;
                ht = 0.3f;
                if (night)
                {
                    var k = shelf ? (band ? 0.9f : 0.45f + 0.4f * Hash(shelfCol, y / 6, 23)) : 1f;
                    e = new Color(1f, 0.84f, 0.62f) * k * (ly < 0.36f ? 0.7f : 1f);
                }
            }
            alb.Set(x, y, c);
            rough.Set(x, y, r);
            bump.Set(x, y, new Color(ht, ht, ht));
            emit?.Set(x, y, e);
        }
        var nimg = bump.ToImage(false);
        nimg.BumpMapToNormalMap(2f);
        nimg.GenerateMipmaps();
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(alb.ToImage()),
            RoughnessTexture = ImageTexture.CreateFromImage(rough.ToImage()),
            Roughness = 1f,
            MetallicSpecular = 0.65f,
            NormalEnabled = true,
            NormalTexture = ImageTexture.CreateFromImage(nimg),
            NormalScale = 0.7f,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        if (night)
        {
            mat.EmissionEnabled = true;
            mat.EmissionTexture = ImageTexture.CreateFromImage(emit.ToImage());
            mat.EmissionEnergyMultiplier = 0.55f;
        }
        Cache[key] = mat;
        return mat;
    }

    // ───────────────────────── Voirie et détails ─────────────────────────

    /// <summary>Dallage de trottoir : dalles de 50 cm aux teintes légèrement variées, joints sombres.</summary>
    private static StandardMaterial3D Paving()
    {
        if (Cache.TryGetValue("paving", out var m)) return (StandardMaterial3D)m;
        const int size = 256; // 2 m, 4 × 4 dalles
        var alb = new Px(size, size);
        var bump = new Px(size, size);
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var ty = y / 64;
            var ox = x + (ty % 2) * 32; // pose à joints décalés
            var tx = (ox / 64) % 4;
            var joint = ox % 64 < 2 || y % 64 < 2;
            var t = Hash(tx, ty, 41) - 0.5f;
            var speck = Hash(x, y, 43) * 0.05f;
            var stain = (ValueNoise(x, y, 32, size / 32, 44) - 0.5f) * 0.08f;
            var v = joint ? 0.4f : 0.53f + t * 0.07f + speck + stain;
            alb.Set(x, y, new Color(v, v * 0.985f, v * 0.96f));
            var hgt = joint ? 0.2f : 0.85f + speck;
            bump.Set(x, y, new Color(hgt, hgt, hgt));
        }
        var nimg = bump.ToImage(false);
        nimg.BumpMapToNormalMap(4f);
        nimg.GenerateMipmaps();
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(alb.ToImage()),
            NormalEnabled = true,
            NormalTexture = ImageTexture.CreateFromImage(nimg),
            NormalScale = 0.8f,
            Roughness = 0.9f,
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = new Vector3(0.5f, 0.5f, 0.5f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        Cache["paving"] = mat;
        return mat;
    }

    /// <summary>Peinture routière (marquages, passages piétons) légèrement usée.</summary>
    public static StandardMaterial3D RoadPaint()
    {
        if (Cache.TryGetValue("roadpaint", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ColorNoise(141, 0.35f, new Color(0.66f, 0.66f, 0.64f), new Color(0.84f, 0.84f, 0.81f), 256),
            Roughness = 0.75f,
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = new Vector3(0.15f, 0.15f, 0.15f),
        };
        Cache["roadpaint"] = mat;
        return mat;
    }

    /// <summary>Bordure de trottoir en granit, plus sombre que le dallage.</summary>
    public static StandardMaterial3D Curb()
    {
        if (Cache.TryGetValue("curb", out var m)) return (StandardMaterial3D)m;
        var mat = (StandardMaterial3D)Concrete(new Color(0.4f, 0.4f, 0.4f), 151).Duplicate();
        mat.Uv1Scale = new Vector3(0.8f, 0.8f, 0.8f);
        mat.Roughness = 0.8f;
        Cache["curb"] = mat;
        return mat;
    }

    /// <summary>Matériau de détail : teinte par sommet × grain de béton (fusion de nombreux petits éléments).</summary>
    public static StandardMaterial3D Detail()
    {
        if (Cache.TryGetValue("detail", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoTexture = ColorNoise(161, 0.05f, new Color(0.84f, 0.84f, 0.84f), new Color(1f, 1f, 1f), 256),
            NormalEnabled = true,
            NormalTexture = Noise(162, 0.1f, true, 256, 2f),
            NormalScale = 0.4f,
            Roughness = 0.82f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.5f, 0.5f, 0.5f),
        };
        Cache["detail"] = mat;
        return mat;
    }

    /// <summary>Variante métallique du matériau de détail (poteaux, rambardes, carrosseries).</summary>
    public static StandardMaterial3D DetailMetal()
    {
        if (Cache.TryGetValue("detailmetal", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Metallic = 0.55f,
            Roughness = 0.4f,
        };
        Cache["detailmetal"] = mat;
        return mat;
    }

    /// <summary>Feuillage : couleur par sommet, marbrures, visible des deux côtés (palmes, houppiers).</summary>
    public static StandardMaterial3D Foliage()
    {
        if (Cache.TryGetValue("foliage", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoTexture = ColorNoise(181, 0.25f, new Color(0.7f, 0.72f, 0.66f), new Color(1f, 1f, 1f), 256),
            NormalEnabled = true,
            NormalTexture = Noise(182, 0.35f, true, 256, 6f),
            NormalScale = 0.8f,
            Roughness = 0.88f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.7f, 0.7f, 0.7f),
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            BacklightEnabled = true,
            Backlight = new Color(0.18f, 0.25f, 0.08f),
        };
        Cache["foliage"] = mat;
        return mat;
    }

    /// <summary>Toile teintée par instance de MultiMesh (parasols, serviettes), visible des deux côtés.</summary>
    public static StandardMaterial3D Instanced()
    {
        if (Cache.TryGetValue("instanced", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoTexture = ColorNoise(171, 0.3f, new Color(0.86f, 0.86f, 0.86f), new Color(1f, 1f, 1f), 128),
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(2f, 2f, 2f),
            Roughness = 0.9f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        Cache["instanced"] = mat;
        return mat;
    }

    /// <summary>Feux et voyants : non éclairé, couleur par sommet.</summary>
    public static StandardMaterial3D Lamps()
    {
        if (Cache.TryGetValue("lamps", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        Cache["lamps"] = mat;
        return mat;
    }

    /// <summary>Verre transparent (abribus, garde-corps de balcon).</summary>
    public static StandardMaterial3D Glass()
    {
        if (Cache.TryGetValue("glass", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.55f, 0.65f, 0.68f, 0.32f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.05f,
            Metallic = 0.2f,
            MetallicSpecular = 0.8f,
        };
        Cache["glass"] = mat;
        return mat;
    }

    /// <summary>Platelage en bois (ponton) : lames de 15 cm, teintes variées, usure.</summary>
    public static StandardMaterial3D Planks()
    {
        if (Cache.TryGetValue("planks", out var m)) return (StandardMaterial3D)m;
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
            var v = gap ? new Color(0.08f, 0.07f, 0.06f) : new Color(0.46f, 0.36f, 0.26f) * (0.75f + bv * 0.4f + grain);
            alb.Set(x, y, v);
            var hh = gap ? 0.1f : 0.9f;
            bump.Set(x, y, new Color(hh, hh, hh));
        }
        var nimg = bump.ToImage(false);
        nimg.BumpMapToNormalMap(4f);
        nimg.GenerateMipmaps();
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(alb.ToImage()),
            NormalEnabled = true,
            NormalTexture = ImageTexture.CreateFromImage(nimg),
            Roughness = 0.85f,
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = new Vector3(0.5f, 0.5f, 0.5f),
        };
        Cache["planks"] = mat;
        return mat;
    }

    /// <summary>Peinture de carrosserie (vernis brillant).</summary>
    public static StandardMaterial3D CarPaint(Color c)
    {
        var key = $"carpaint{c}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = c,
            Metallic = 0.45f,
            Roughness = 0.32f,
            ClearcoatEnabled = true,
            Clearcoat = 0.7f,
            ClearcoatRoughness = 0.1f,
        };
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
