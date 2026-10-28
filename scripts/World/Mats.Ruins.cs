using Godot;

namespace Rempart;

/// <summary>
/// Matériaux des ruines (invasion) : tôles calcinées, décalcomanies au sol (brûlures, cratères, fissures,
/// hémolymphe), suie sur les façades, résine et œufs d'insectes, toiles, fumées et flammes.
/// Toutes les textures sont générées en mémoire, sans tirage aléatoire (hachages fixes).
/// </summary>
public static partial class Mats
{
    /// <summary>Lissage d'Hermite borné (bord0 &lt; bord1 ou l'inverse).</summary>
    private static float Smooth(float e0, float e1, float x)
    {
        var t = Mathf.Clamp((x - e0) / (e1 - e0), 0f, 1f);
        return t * t * (3 - 2 * t);
    }

    /// <summary>Bruit fractal (3 octaves de bruit de valeur périodique), dans [0, 1].</summary>
    private static float Fbm(float x, float y, int cell, int period, int seed)
    {
        var a = ValueNoise(x, y, cell, period, seed);
        var b = ValueNoise(x, y, Mathf.Max(1, cell / 2), period * 2, seed + 1);
        var c = ValueNoise(x, y, Mathf.Max(1, cell / 4), period * 4, seed + 2);
        return a * 0.55f + b * 0.3f + c * 0.15f;
    }

    // ───────────────────────── Épaves ─────────────────────────

    /// <summary>
    /// Tôle calcinée : suie noire, rouille, cendre et peinture cloquée en plaques ; la couleur de sommet
    /// module la teinte de chaque pièce (carrosserie, jantes, châssis). Triplanaire en repère objet.
    /// </summary>
    public static StandardMaterial3D Charred()
    {
        if (Cache.TryGetValue("charred", out var m)) return (StandardMaterial3D)m;
        const int size = 256;
        var alb = new Px(size, size);
        var rough = new Px(size, size, 1);
        var bump = new Px(size, size);
        System.Threading.Tasks.Parallel.For(0, size, y =>
        {
            for (var x = 0; x < size; x++)
            {
                var n = Fbm(x, y, 64, size / 64, 301);
                var fine = ValueNoise(x, y, 4, size / 4, 304);
                var flake = ValueNoise(x, y, 8, size / 8, 305);
                // Suie (noir mat), rouille brune éteinte, cendre grise poudreuse.
                var rust = new Color(0.62f, 0.44f, 0.33f);
                var soot = new Color(0.2f, 0.19f, 0.18f);
                var ash = new Color(0.62f, 0.61f, 0.6f);
                var t = Smooth(0.3f, 0.75f, n);
                var c = soot.Lerp(rust, t);
                c = c.Lerp(ash, Smooth(0.62f, 0.9f, n + flake * 0.12f) * 0.6f);
                // Cloques et écailles de peinture brûlée.
                if (flake > 0.8f) c = c.Darkened(0.3f);
                c *= 0.85f + fine * 0.25f;
                c.A = 1;
                alb.Set(x, y, c);
                rough.Set(x, y, Mathf.Lerp(0.95f, 0.72f, t) + fine * 0.05f);
                var hgt = 0.5f + (flake - 0.5f) * 0.6f + (fine - 0.5f) * 0.3f;
                bump.Set(x, y, new Color(hgt, hgt, hgt));
            }
        });
        var nimg = bump.ToImage(false);
        nimg.BumpMapToNormalMap(3f);
        nimg.GenerateMipmaps();
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoTexture = ImageTexture.CreateFromImage(alb.ToImage()),
            RoughnessTexture = ImageTexture.CreateFromImage(rough.ToImage()),
            Roughness = 1f,
            Metallic = 0.25f,
            NormalEnabled = true,
            NormalTexture = ImageTexture.CreateFromImage(nimg),
            NormalScale = 0.9f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.55f, 0.55f, 0.55f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        Cache["charred"] = mat;
        return mat;
    }

    /// <summary>Braises : orangé lumineux, non éclairé (lueur des épaves et gravats qui couvent).</summary>
    public static StandardMaterial3D Embers()
    {
        if (Cache.TryGetValue("embers", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoTexture = ColorNoise(311, 0.2f, new Color(0.35f, 0.08f, 0.02f), new Color(1f, 0.55f, 0.2f), 128),
            EmissionEnabled = true,
            Emission = new Color(1f, 0.35f, 0.08f),
            EmissionEnergyMultiplier = 1.6f,
            EmissionTexture = ColorNoise(312, 0.25f, new Color(0.1f, 0.02f, 0f), new Color(1f, 0.6f, 0.25f), 128),
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(1.5f, 1.5f, 1.5f),
            Roughness = 1f,
        };
        Cache["embers"] = mat;
        return mat;
    }

    // ───────────────────────── Décalcomanies au sol ─────────────────────────

    /// <summary>Cases de l'atlas des marques au sol (4 × 2).</summary>
    public enum Decal { Scorch, Crater, Cracks, Ichor, Oil, Ash, Acid, Dust }

    /// <summary>Coordonnées UV (coin, taille) d'une case de l'atlas.</summary>
    public static (Vector2 origin, Vector2 size) DecalUv(Decal d)
    {
        var i = (int)d;
        return (new Vector2((i % 4) / 4f, (i / 4) / 2f), new Vector2(0.25f, 0.5f));
    }

    /// <summary>
    /// Atlas des marques au sol (2048 × 1024, cases de 512 px) : brûlure, cratère, fissures, flaque
    /// d'hémolymphe, tache d'huile, cendres, brûlure d'acide, poussière de béton. Alpha progressif.
    /// </summary>
    public static StandardMaterial3D GroundDecals()
    {
        if (Cache.TryGetValue("grounddecals", out var m)) return (StandardMaterial3D)m;
        const int cs = 512, w = cs * 4, h = cs * 2;
        var alb = new Px(w, h);
        var rough = new Px(w, h, 1);
        var bump = new Px(w, h);
        var cracks = CrackField(cs, 331);
        System.Threading.Tasks.Parallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var cell = (y / cs) * 4 + x / cs;
                var lx = x % cs;
                var ly = y % cs;
                var u = (lx + 0.5f) / cs * 2f - 1f;
                var v = (ly + 0.5f) / cs * 2f - 1f;
                var r = Mathf.Sqrt(u * u + v * v);
                var ang = Mathf.Atan2(v, u);
                var seed = 340 + cell * 10;
                var n1 = Fbm(lx, ly, 64, cs / 64, seed);
                var n2 = Fbm(lx, ly, 16, cs / 16, seed + 3);
                var n3 = ValueNoise(lx, ly, 4, cs / 4, seed + 5);
                // Rayon déformé (contour irrégulier), coupé avant le bord de la case.
                var rd = r * (1f + (n1 - 0.5f) * 0.7f);
                var edge = Smooth(0.98f, 0.9f, r);
                Color c;
                float a, ro = 0.95f, ht = 0.5f;
                switch ((Decal)cell)
                {
                    case Decal.Scorch:
                    {
                        // Gerbes rayonnantes : tirage par secteur angulaire.
                        var sector = Mathf.FloorToInt((ang + Mathf.Pi) / Mathf.Tau * 23f);
                        var ray = Hash(sector, 0, 351);
                        var reach = 0.55f + ray * 0.45f;
                        a = Smooth(reach, reach * 0.35f, rd) * (0.7f + 0.3f * n2);
                        a = Mathf.Max(a, Smooth(0.75f, 0.2f, rd) * 0.9f);
                        // Cœur couvert de cendre grise, auréole noire de suie (lisible sur l'enrobé sombre).
                        var core = Smooth(0.5f, 0.1f, rd);
                        var ashRing = Smooth(0.2f, 0.45f, rd) * Smooth(0.75f, 0.5f, rd);
                        c = new Color(0.02f, 0.019f, 0.018f).Lerp(new Color(0.24f, 0.23f, 0.22f), core * (0.4f + 0.6f * n3));
                        c = c.Lerp(new Color(0.012f, 0.011f, 0.01f), ashRing * 0.7f);
                        a = Mathf.Min(1f, a * 1.15f);
                        ro = 0.98f;
                        break;
                    }
                    case Decal.Crater:
                    {
                        // Trou sombre, bourrelet de bitume éclaté, auréole de suie.
                        var hole = Smooth(0.42f, 0.34f, rd);
                        var rim = Smooth(0.3f, 0.42f, rd) * Smooth(0.68f, 0.5f, rd);
                        var halo = Smooth(1f, 0.5f, rd);
                        var chunk = Hash(lx / 10, ly / 10, 361) > 0.55f ? 1f : 0.6f;
                        c = new Color(0.03f, 0.028f, 0.026f);
                        c = c.Lerp(new Color(0.2f, 0.18f, 0.16f) * (0.6f + n3 * 0.8f) * chunk, rim);
                        c = c.Lerp(new Color(0.015f, 0.014f, 0.013f), hole);
                        a = Mathf.Max(Mathf.Max(hole, rim), halo * (0.75f + 0.25f * n2));
                        ht = 0.5f - hole * 0.45f + rim * 0.35f * chunk;
                        break;
                    }
                    case Decal.Cracks:
                    {
                        var k = cracks[ly * cs + lx];
                        var pot = Smooth(0.2f, 0.1f, rd) * (n1 > 0.5f ? 1f : 0f);
                        a = Mathf.Max(k, pot) * edge;
                        c = new Color(0.025f, 0.024f, 0.022f).Lerp(new Color(0.1f, 0.095f, 0.09f), n3 * 0.5f);
                        ht = 0.5f - a * 0.4f;
                        break;
                    }
                    case Decal.Ichor:
                    {
                        // Flaque principale + gouttelettes projetées, liseré rongé jaune-vert.
                        var pool = Smooth(0.62f, 0.5f, rd);
                        var drop = 0f;
                        for (var i = 0; i < 26; i++)
                        {
                            var da = Hash(i, 1, 371) * Mathf.Tau;
                            var dr = 0.45f + Hash(i, 2, 371) * 0.45f;
                            var rad = 0.015f + Hash(i, 3, 371) * 0.05f;
                            var dx = u - Mathf.Cos(da) * dr;
                            var dy = v - Mathf.Sin(da) * dr;
                            drop = Mathf.Max(drop, Smooth(rad, rad * 0.6f, Mathf.Sqrt(dx * dx + dy * dy)));
                        }
                        var body = Mathf.Max(pool, drop);
                        var rimC = Smooth(0.5f, 0.6f, rd) * Smooth(0.72f, 0.6f, rd);
                        c = new Color(0.025f, 0.04f, 0.015f).Lerp(new Color(0.09f, 0.14f, 0.03f), n2 * 0.7f);
                        c = c.Lerp(new Color(0.24f, 0.26f, 0.06f), rimC * 0.7f);
                        a = Mathf.Max(body * 0.94f, rimC * 0.55f) * edge;
                        ro = Mathf.Lerp(0.85f, 0.08f, body);
                        ht = 0.5f + body * 0.15f;
                        break;
                    }
                    case Decal.Oil:
                    {
                        var pool = Smooth(0.75f, 0.45f, rd);
                        var film = Smooth(0.95f, 0.6f, rd) * (1 - pool);
                        // Reflets irisés au bord de la nappe.
                        var hue = Mathf.PosMod(rd * 3.1f + n2 * 0.6f, 1f);
                        var iri = Color.FromHsv(hue, 0.55f, 0.18f);
                        c = new Color(0.012f, 0.012f, 0.014f).Lerp(iri, film);
                        a = Mathf.Max(pool * 0.95f, film * 0.6f) * edge;
                        ro = 0.05f;
                        break;
                    }
                    case Decal.Ash:
                    {
                        var patch = Smooth(0.42f, 0.62f, n1 * (1.2f - r * 0.6f));
                        c = new Color(0.06f, 0.055f, 0.05f).Lerp(new Color(0.28f, 0.27f, 0.25f), n3 * n2);
                        a = Mathf.Max(patch, Smooth(0.85f, 0.3f, rd) * 0.6f) * edge * (0.75f + 0.25f * n3);
                        ro = 1f;
                        break;
                    }
                    case Decal.Acid:
                    {
                        var etched = Smooth(0.7f, 0.4f, rd);
                        var pits = n3 > 0.62f ? 1f : 0f;
                        c = new Color(0.32f, 0.3f, 0.12f).Lerp(new Color(0.08f, 0.09f, 0.03f), Smooth(0.5f, 0.1f, rd));
                        c = c.Lerp(new Color(0.02f, 0.02f, 0.015f), pits * etched);
                        a = Mathf.Max(etched * 0.9f, Smooth(0.85f, 0.65f, rd) * 0.4f) * edge;
                        ro = 0.6f;
                        ht = 0.5f - pits * etched * 0.3f;
                        break;
                    }
                    default:
                    {
                        // Poussière de béton et gravillons au pied des gravats.
                        var dust = Smooth(0.95f, 0.25f, rd);
                        var grit = n3 > 0.7f ? 0.25f : 0f;
                        c = new Color(0.46f, 0.44f, 0.41f) * (0.8f + n2 * 0.3f) - new Color(grit, grit, grit);
                        a = dust * (0.55f + 0.35f * n1) * edge;
                        ro = 1f;
                        break;
                    }
                }
                c.A = Mathf.Clamp(a, 0, 1);
                alb.Set(x, y, c);
                rough.Set(x, y, ro);
                bump.Set(x, y, new Color(ht, ht, ht));
            }
        });
        var nimg = bump.ToImage(false);
        nimg.BumpMapToNormalMap(6f);
        nimg.GenerateMipmaps();
        var img = alb.ToImage(false);
        var lit = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(WithMips(img)),
            RoughnessTexture = ImageTexture.CreateFromImage(rough.ToImage()),
            Roughness = 1f,
            NormalEnabled = true,
            NormalTexture = ImageTexture.CreateFromImage(nimg),
            NormalScale = 1f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
            RenderPriority = -1,
        };
        Cache["grounddecalslit"] = lit;
        var mat = MulMaterial(img);
        mat.RenderPriority = -1;
        Cache["grounddecals"] = mat;
        return mat;
    }

    /// <summary>Variante éclairée (fondu alpha) de l'atlas : marques claires (poussière) ou brillantes.</summary>
    public static StandardMaterial3D GroundDecalsLit()
    {
        GroundDecals();
        return (StandardMaterial3D)Cache["grounddecalslit"];
    }

    private static Image WithMips(Image src)
    {
        var copy = (Image)src.Duplicate();
        copy.GenerateMipmaps();
        return copy;
    }

    /// <summary>
    /// Matériau « multiplicatif » non éclairé : assombrit ce qui est dessous (suie, brûlures, fissures).
    /// Bien moins coûteux qu'un fondu alpha éclairé ; la couleur est pré-mélangée vers le blanc selon l'alpha.
    /// </summary>
    private static StandardMaterial3D MulMaterial(Image rgba)
    {
        var data = rgba.GetData();
        var w = rgba.GetWidth();
        var h = rgba.GetHeight();
        var outp = new byte[w * h * 4];
        System.Threading.Tasks.Parallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w + x) * 4;
                var a = data[i + 3] / 255f;
                for (var ch = 0; ch < 3; ch++) outp[i + ch] = (byte)Mathf.RoundToInt(255f - a * (255f - data[i + ch]));
                outp[i + 3] = 255;
            }
        });
        var img = Image.CreateFromData(w, h, false, Image.Format.Rgba8, outp);
        img.GenerateMipmaps();
        return new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(img),
            BlendMode = BaseMaterial3D.BlendModeEnum.Mul,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            DisableFog = true,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
    }

    /// <summary>Réseau de fissures ramifiées (marche aléatoire déterministe), intensité par pixel.</summary>
    private static float[] CrackField(int size, int seed)
    {
        var f = new float[size * size];
        var rnd = new System.Random(seed);
        void Stamp(float px, float py, float rad, float val)
        {
            var r = Mathf.CeilToInt(rad + 1);
            for (var yy = (int)py - r; yy <= (int)py + r; yy++)
            for (var xx = (int)px - r; xx <= (int)px + r; xx++)
            {
                if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                var d = Mathf.Sqrt((xx - px) * (xx - px) + (yy - py) * (yy - py));
                var k = Smooth(rad + 1f, rad, d) * val;
                var i = yy * size + xx;
                if (k > f[i]) f[i] = k;
            }
        }
        void Walk(float x, float y, float ang, float len, float wid, int depth)
        {
            var steps = (int)(len / 3f);
            for (var s = 0; s < steps; s++)
            {
                ang += (float)(rnd.NextDouble() - 0.5) * 0.7f;
                x += Mathf.Cos(ang) * 3f;
                y += Mathf.Sin(ang) * 3f;
                var t = s / (float)steps;
                var cx = x - size / 2f;
                var cy = y - size / 2f;
                var fade = Smooth(size * 0.48f, size * 0.38f, Mathf.Sqrt(cx * cx + cy * cy));
                Stamp(x, y, wid * (1 - t * 0.7f), fade);
                Stamp(x, y, wid * (1 - t * 0.7f) + 3f, fade * 0.2f);
                if (depth < 3 && rnd.NextDouble() < 0.05)
                    Walk(x, y, ang + (rnd.NextDouble() < 0.5 ? -1f : 1f) * (0.6f + (float)rnd.NextDouble() * 0.6f), len * 0.5f, wid * 0.6f, depth + 1);
            }
        }
        for (var i = 0; i < 6; i++)
        {
            var a0 = i * Mathf.Tau / 6 + (float)rnd.NextDouble() * 0.6f;
            Walk(size / 2f, size / 2f, a0, size * 0.42f, 2.2f, 0);
        }
        return f;
    }

    // ───────────────────────── Suie et dégâts de façade ─────────────────────────

    /// <summary>Variante de façade (Mats.Facade) d'un matériau, ou -1 s'il n'en est pas une.</summary>
    public static int FacadeVariantOf(Material m)
    {
        foreach (var kv in Cache)
        {
            if (kv.Value != m || !kv.Key.StartsWith("facade")) continue;
            var rest = kv.Key.Substring(6);
            var digits = 0;
            while (digits < rest.Length && char.IsDigit(rest[digits])) digits++;
            if (digits > 0 && int.TryParse(rest.Substring(0, digits), out var v)) return v;
        }
        return -1;
    }

    /// <summary>
    /// Calque de dégâts d'une façade d'immeuble, aligné sur sa trame de fenêtres : vitres soufflées,
    /// fenêtres incendiées avec panache de suie au-dessus, coulures ; <paramref name="heavy"/> ajoute
    /// des brèches dans le mur. Tuile de 4 U × 8 étages (même convention d'UV que la façade).
    /// </summary>
    public static StandardMaterial3D FacadeDamage(int variant, bool heavy)
    {
        var key = $"facadedmg{variant}{heavy}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var s = StyleOf(variant);
        const int perU = 128, perFloor = 64, uSpan = 4, floors = 8;
        const int w = perU * uSpan, h = perFloor * floors;
        var alb = new Px(w, h);
        var rough = new Px(w, h, 1);
        var curtain = s.Kind == FKind.Curtain;
        var cols = s.Cols * uSpan;
        var seed = variant * 17 + (heavy ? 900 : 400);
        // Dégâts ponctuels (quelques fenêtres soufflées ou incendiées) : la ville reste lisible et colorée.
        float burnRate = heavy ? 0.1f : 0.035f, breakRate = heavy ? 0.22f : 0.1f;
        // Mur-rideau : cinq vitrages par travée, autant de carreaux noirs possibles ; taux divisés d'autant.
        if (curtain) { burnRate *= 0.35f; breakRate *= 0.35f; }
        // Brèches : un ou deux trous irréguliers dans la tuile (version lourde).
        var breaches = new System.Collections.Generic.List<(float u, float fl, float ru, float rf)>();
        if (heavy)
        {
            breaches.Add((0.6f + Hash(1, 2, seed) * 1.2f, 1.5f + Hash(1, 3, seed) * 2f, 0.45f + Hash(1, 4, seed) * 0.3f, 0.75f + Hash(1, 5, seed) * 0.5f));
            breaches.Add((2.6f + Hash(2, 2, seed) * 1.0f, 5f + Hash(2, 3, seed) * 2f, 0.3f + Hash(2, 4, seed) * 0.25f, 0.55f + Hash(2, 5, seed) * 0.4f));
        }
        bool Burnt(int cx, int fl) => Hash(((cx % cols) + cols) % cols, ((fl % floors) + floors) % floors, seed) < burnRate;
        bool Broken(int cx, int fl) => Hash(((cx % cols) + cols) % cols, ((fl % floors) + floors) % floors, seed + 1) < breakRate;
        var winTop = curtain ? 0f : s.WinTop;
        var winBot = curtain ? 0.775f : s.WinBot;
        var halfW = curtain ? 0.465f : s.WinW * 0.5f;

        System.Threading.Tasks.Parallel.For(0, h, y =>
        {
            for (var x = 0; x < w; x++)
            {
                var U = x / (float)perU;
                var Fy = y / (float)perFloor;     // étages comptés depuis le haut de la tuile
                var fl = (int)Fy;
                var ly = Fy - fl;
                var fx = U * s.Cols;
                var cx = (int)fx;
                var lx = fx - cx;
                var grimeN = Fbm(x, y, 64, w / 64, seed + 3);
                // Coulures verticales (suie lessivée par la pluie).
                var streak = ValueNoise(x * 6f, y * 0.25f, 32, w * 6 / 32, seed + 4);
                var col = new Color(0.025f, 0.022f, 0.02f);
                var a = Smooth(0.45f, 0.85f, grimeN) * 0.14f + Smooth(0.6f, 0.9f, streak) * 0.1f;
                var r = 0.95f;

                // Panaches de suie au-dessus des fenêtres incendiées (fenêtre du même étage ou de l'étage inférieur).
                for (var df = 0; df <= 1; df++)
                {
                    var wf = fl + df;
                    if (!Burnt(cx, wf)) continue;
                    var dy = (wf + winTop) - Fy;           // hauteur au-dessus du linteau, en étages
                    if (dy < -0.05f || dy > 1.45f) continue;
                    var spread = halfW * (1f + dy * 1.1f) + 0.04f;
                    var wob = (ValueNoise(x, y, 12, w / 12, seed + 6) - 0.5f) * 0.25f;
                    var dx = Mathf.Abs(lx - 0.5f + wob * dy);
                    var k = Smooth(spread, spread * 0.45f, dx) * Mathf.Pow(Mathf.Clamp(1f - dy / 1.45f, 0, 1), 1.2f);
                    k *= 0.75f + 0.25f * grimeN;
                    a = Mathf.Max(a, k * 0.97f);
                }

                var inWin = Mathf.Abs(lx - 0.5f) < halfW && ly > winTop && ly < winBot;
                if (inWin && (Broken(cx, fl) || Burnt(cx, fl)))
                {
                    // Vitre soufflée : pièce sombre ; éclats restants dans les angles.
                    var wx = (lx - (0.5f - halfW)) / (halfW * 2);
                    var wy = (ly - winTop) / (winBot - winTop);
                    var hs = Hash(cx, fl, seed + 9);
                    var shard = (wx + wy < 0.18f + hs * 0.2f) || (1 - wx + wy < 0.12f + hs * 0.15f) || (wx + 1 - wy < 0.1f + hs * 0.25f);
                    if (shard && !Burnt(cx, fl))
                    {
                        col = new Color(0.16f, 0.17f, 0.19f);
                        a = Mathf.Max(a, 0.8f);
                        r = 0.1f;
                    }
                    else
                    {
                        var depthShade = 0.012f + 0.03f * Fbm(x, y, 8, w / 8, seed + 10) * (1 - wy);
                        col = new Color(depthShade, depthShade * 0.95f, depthShade * 0.9f);
                        a = 1f;
                        r = 1f;
                    }
                }

                foreach (var (bu, bf, ru, rf) in breaches)
                {
                    var du = (U - bu) / ru;
                    var dv = (Fy - bf) / rf;
                    var d = Mathf.Sqrt(du * du + dv * dv) * (1f + (Fbm(x, y, 16, w / 16, seed + 12) - 0.5f) * 0.8f);
                    if (d < 1f)
                    {
                        // Intérieur éventré : obscurité, dalle d'étage visible, béton éclaté au bord.
                        var slab = Mathf.Abs(ly - 0.97f) < 0.04f || ly < 0.03f;
                        col = slab ? new Color(0.2f, 0.19f, 0.18f) : new Color(0.012f, 0.011f, 0.01f);
                        if (d > 0.82f) col = new Color(0.33f, 0.31f, 0.29f) * (0.7f + 0.5f * Hash(x / 3, y / 3, seed + 13));
                        a = 1f;
                        r = 1f;
                    }
                    else if (d < 1.8f) a = Mathf.Max(a, Smooth(1.8f, 1f, d) * 0.9f);
                }
                col.A = Mathf.Clamp(a, 0, 1);
                alb.Set(x, y, col);
                rough.Set(x, y, r);
            }
        });
        // Calque multiplicatif (suie, vitres soufflées) : pas d'éclairage ni de fondu à calculer sur les grandes façades.
        var mat = MulMaterial(alb.ToImage(false));
        mat.Uv1Scale = new Vector3(1f / uSpan, 1f / floors, 1f);
        Cache[key] = mat;
        return mat;
    }

    /// <summary>
    /// Suie générique (pavillons, hangars, cuves, conteneurs) : panaches montant depuis le bas, plaques
    /// brûlées, coulures. Tuile de 8 m × 8 m, UV en mètres / 8 (V croissant vers le bas).
    /// </summary>
    public static StandardMaterial3D Soot()
    {
        if (Cache.TryGetValue("soot", out var m)) return (StandardMaterial3D)m;
        const int size = 512;
        var alb = new Px(size, size);
        System.Threading.Tasks.Parallel.For(0, size, y =>
        {
            for (var x = 0; x < size; x++)
            {
                var n = Fbm(x, y, 64, size / 64, 381);
                var streak = ValueNoise(x * 5f, y * 0.3f, 32, size * 5 / 32, 382);
                var up = 1f - y / (float)size;   // 0 en bas, 1 en haut
                var a = 0f;
                for (var i = 0; i < 4; i++)
                {
                    var sx = (i + 0.2f + Hash(i, 0, 383) * 0.6f) * size / 4f;
                    var start = 0.15f + Hash(i, 1, 383) * 0.45f;   // hauteur de la source (fenêtre, porte)
                    var dy = up - start;
                    if (dy < -0.12f) continue;
                    var spread = (0.05f + Mathf.Max(dy, 0) * 0.55f) * size;
                    var dx = Mathf.Abs(Mathf.PosMod(x - sx + size / 2f, size) - size / 2f) + (n - 0.5f) * 40f;
                    var k = Smooth(spread, spread * 0.3f, dx) * Mathf.Clamp(1f - dy / 0.65f, 0, 1);
                    a = Mathf.Max(a, k);
                }
                a = Mathf.Max(a, Smooth(0.55f, 0.8f, n) * 0.6f);
                a = Mathf.Max(a, Smooth(0.6f, 0.9f, streak) * 0.3f);
                a *= 0.8f + 0.2f * n;
                var c = new Color(0.03f, 0.027f, 0.024f, Mathf.Clamp(a, 0, 1));
                alb.Set(x, y, c);
            }
        });
        var mat = MulMaterial(alb.ToImage(false));
        Cache["soot"] = mat;
        return mat;
    }

    /// <summary>Toiture calcinée : tuiles noircies et cendres, poutres apparentes en bandes (UV en mètres / 2).</summary>
    public static StandardMaterial3D BurntRoof()
    {
        if (Cache.TryGetValue("burntroof", out var m)) return (StandardMaterial3D)m;
        const int size = 512; // 6 m
        var alb = new Px(size, size);
        var bump = new Px(size, size);
        System.Threading.Tasks.Parallel.For(0, size, y => {
        for (var x = 0; x < size; x++)
        {
            var n = (Fbm(x, y, 128, size / 128, 391) - 0.5f) * 2.4f + 0.5f;
            var f = ValueNoise(x, y, 4, size / 4, 392);
            var hole = n < 0.43f;
            var rim = !hole && n < 0.48f;
            var rafter = hole && (x % 51) < 7;
            Color c;
            float hh;
            if (rafter) { c = new Color(0.045f, 0.035f, 0.03f) * (0.8f + f * 0.4f); hh = 0.9f; }
            else if (hole) { c = new Color(0.006f, 0.005f, 0.005f); hh = 0f; }
            else if (rim) { c = new Color(0.2f, 0.19f, 0.18f) * (0.6f + 0.6f * f); hh = 0.45f; }   // cendres au bord du trou
            else
            {
                var row = y / 21;
                var shade = Mathf.Lerp(0.6f, 1f, Smooth(0f, 0.3f, (y % 21) / 21f));
                var tileH = Hash((x + (row % 2) * 10) / 21, row, 393);
                c = new Color(0.06f, 0.055f, 0.05f).Lerp(new Color(0.3f, 0.16f, 0.12f), Smooth(0.6f, 0.9f, n) * 0.5f) * shade * (0.8f + 0.4f * tileH);
                if (tileH < 0.06f) { c = new Color(0.01f, 0.01f, 0.01f); shade = 0f; }   // tuile manquante
                hh = 0.5f + 0.3f * shade;
            }
            c.A = 1;
            alb.Set(x, y, c);
            bump.Set(x, y, new Color(hh, hh, hh));
        }
        });
        var nimg = bump.ToImage(false);
        nimg.BumpMapToNormalMap(6f);
        nimg.GenerateMipmaps();
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(alb.ToImage()),
            NormalEnabled = true,
            NormalTexture = ImageTexture.CreateFromImage(nimg),
            Roughness = 0.97f,
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = new Vector3(1f / 6f, 1f / 6f, 1f / 6f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        Cache["burntroof"] = mat;
        return mat;
    }

    // ───────────────────────── Présence des insectes ─────────────────────────

    /// <summary>Résine organique : sécrétion sombre et humide, très brillante, veinée (teinte par sommet).</summary>
    public static StandardMaterial3D Resin()
    {
        if (Cache.TryGetValue("resin", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoTexture = ColorNoise(401, 0.09f, new Color(0.45f, 0.5f, 0.4f), new Color(1f, 1f, 0.9f), 256),
            NormalEnabled = true,
            NormalTexture = Noise(402, 0.07f, true, 256, 9f, FastNoiseLite.NoiseTypeEnum.Cellular, 3),
            NormalScale = 1.2f,
            Roughness = 0.12f,
            Metallic = 0.1f,
            ClearcoatEnabled = true,
            Clearcoat = 1f,
            ClearcoatRoughness = 0.05f,
            RimEnabled = true,
            Rim = 0.35f,
            RimTint = 0.8f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(0.6f, 0.6f, 0.6f),
        };
        Cache["resin"] = mat;
        return mat;
    }

    /// <summary>Œufs : coque translucide gluante, jaune-vert maladif, faible lueur interne.</summary>
    public static StandardMaterial3D Eggs()
    {
        if (Cache.TryGetValue("eggs", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            AlbedoTexture = ColorNoise(411, 0.12f, new Color(0.7f, 0.72f, 0.6f), new Color(1f, 1f, 0.95f), 128),
            NormalEnabled = true,
            NormalTexture = Noise(412, 0.15f, true, 128, 3f),
            NormalScale = 0.5f,
            Roughness = 0.18f,
            ClearcoatEnabled = true,
            Clearcoat = 0.9f,
            ClearcoatRoughness = 0.1f,
            RimEnabled = true,
            Rim = 0.6f,
            RimTint = 0.9f,
            BacklightEnabled = true,
            Backlight = new Color(0.35f, 0.4f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(0.25f, 0.32f, 0.06f),
            EmissionEnergyMultiplier = 0.25f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(1.2f, 1.2f, 1.2f),
        };
        Cache["eggs"] = mat;
        return mat;
    }

    /// <summary>Toile géante : nappe de fils translucides (texture de fils), visible des deux côtés.</summary>
    public static StandardMaterial3D WebSheet()
    {
        if (Cache.TryGetValue("websheet", out var m)) return (StandardMaterial3D)m;
        const int size = 512;
        var a = new float[size * size];
        var rnd = new System.Random(421);
        // Fils tendus en tous sens + quelques torons épais, densité variable.
        for (var i = 0; i < 70; i++)
        {
            var x0 = (float)rnd.NextDouble() * size; var y0 = (float)rnd.NextDouble() * size;
            var ang = (float)rnd.NextDouble() * Mathf.Pi;
            var thick = rnd.NextDouble() < 0.15 ? 1.6f : 0.7f;
            var dx = Mathf.Cos(ang); var dy = Mathf.Sin(ang);
            for (var t = -size; t < size; t += 1)
            {
                var px = Mathf.PosMod(x0 + dx * t, size);
                var py = Mathf.PosMod(y0 + dy * t, size);
                var r = Mathf.CeilToInt(thick + 1);
                for (var oy = -r; oy <= r; oy++)
                for (var ox = -r; ox <= r; ox++)
                {
                    var xx = ((int)px + ox + size) % size;
                    var yy = ((int)py + oy + size) % size;
                    var d = Mathf.Sqrt(ox * ox + oy * oy);
                    var k = Smooth(thick + 0.8f, thick * 0.3f, d) * (thick > 1 ? 0.95f : 0.7f);
                    var j = yy * size + xx;
                    if (k > a[j]) a[j] = k;
                }
            }
        }
        var alb = new Px(size, size);
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var haze = Fbm(x, y, 64, size / 64, 422) * 0.18f;
            var v = Mathf.Clamp(a[y * size + x] + haze, 0, 1);
            alb.Set(x, y, new Color(0.3f, 0.29f, 0.26f, v * 0.75f)); // soie souillée de poussière et de cendre
        }
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(alb.ToImage()),
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, // fils nets, rendu opaque (bien moins coûteux)
            AlphaScissorThreshold = 0.42f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            Roughness = 0.45f,
            BacklightEnabled = true,
            Backlight = new Color(0.16f, 0.16f, 0.15f),
            Uv1Triplanar = true,
            Uv1WorldTriplanar = true,
            Uv1Scale = new Vector3(0.12f, 0.12f, 0.12f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        Cache["websheet"] = mat;
        return mat;
    }

    /// <summary>Fils de toile (tubes fins), gris sale légèrement luisant.</summary>
    public static StandardMaterial3D WebStrand()
    {
        if (Cache.TryGetValue("webstrand", out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.34f, 0.33f, 0.3f),
            Roughness = 0.35f,
            RimEnabled = true,
            Rim = 0.25f,
            BacklightEnabled = true,
            Backlight = new Color(0.12f, 0.12f, 0.11f),
        };
        Cache["webstrand"] = mat;
        return mat;
    }

    /// <summary>Nappe d'hydrocarbures sur l'eau du port : noire, miroitante, irisée sur les bords.</summary>
    public static StandardMaterial3D OilSlick()
    {
        if (Cache.TryGetValue("oilslick", out var m)) return (StandardMaterial3D)m;
        const int size = 512;
        var alb = new Px(size, size);
        System.Threading.Tasks.Parallel.For(0, size, y =>
        {
            for (var x = 0; x < size; x++)
            {
                var u = (x + 0.5f) / size * 2 - 1;
                var v = (y + 0.5f) / size * 2 - 1;
                var n = Fbm(x, y, 64, size / 64, 431);
                var rd = Mathf.Sqrt(u * u + v * v) * (1 + (n - 0.5f) * 0.9f);
                var pool = Smooth(0.8f, 0.35f, rd);
                var film = Smooth(1f, 0.7f, rd) * (1 - pool);
                var iri = Color.FromHsv(Mathf.PosMod(rd * 4f + n, 1f), 0.6f, 0.22f);
                var c = new Color(0.01f, 0.01f, 0.012f).Lerp(iri, film + pool * 0.15f * Smooth(0.4f, 0.7f, n));
                c.A = Mathf.Clamp(pool * 0.92f + film * 0.55f, 0, 1) * Smooth(0.99f, 0.92f, Mathf.Sqrt(u * u + v * v));
                alb.Set(x, y, c);
            }
        });
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(alb.ToImage()),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 0.04f,
            Metallic = 0.3f,
            MetallicSpecular = 0.9f,
        };
        Cache["oilslick"] = mat;
        return mat;
    }

    // ───────────────────────── Fumées et flammes ─────────────────────────

    /// <summary>Bouffée de fumée : disque doux à volutes (bruit), teintée par la couleur des particules.</summary>
    public static StandardMaterial3D SmokePuff(bool far)
    {
        var key = $"smokepuff{far}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        const int size = 128;
        var alb = new Px(size, size);
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var u = (x + 0.5f) / size * 2 - 1;
            var v = (y + 0.5f) / size * 2 - 1;
            var r = Mathf.Sqrt(u * u + v * v);
            var n = Fbm(x, y, 32, size / 32, 441);
            var a = Smooth(1f, 0.15f, r * (0.8f + n * 0.5f)) * (0.55f + 0.45f * n);
            var shade = 0.75f + 0.25f * n - v * 0.12f;   // dessus un peu plus clair
            alb.Set(x, y, new Color(shade, shade, shade, Mathf.Clamp(a, 0, 1)));
        }
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(alb.ToImage()),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true,
            Roughness = 1f,
            ProximityFadeEnabled = !far,
            ProximityFadeDistance = 2f,
            DistanceFadeMode = far ? BaseMaterial3D.DistanceFadeModeEnum.Disabled : BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha,
            DistanceFadeMinDistance = 1f,
            DistanceFadeMaxDistance = 6f,
            ShadingMode = far ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
        };
        Cache[key] = mat;
        return mat;
    }

    /// <summary>Flamme : langue lumineuse additive (plus chaude au cœur), teintée par les particules.</summary>
    public static StandardMaterial3D Flame()
    {
        if (Cache.TryGetValue("flame", out var m)) return (StandardMaterial3D)m;
        const int size = 128;
        var alb = new Px(size, size);
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var u = (x + 0.5f) / size * 2 - 1;
            var v = (y + 0.5f) / size * 2 - 1;   // -1 en haut
            var n = Fbm(x, y, 16, size / 16, 451);
            // Goutte inversée : large en bas, effilée en haut.
            var wdt = Mathf.Lerp(0.15f, 0.75f, (v + 1) * 0.5f) * (0.8f + n * 0.4f);
            var a = Smooth(wdt, wdt * 0.2f, Mathf.Abs(u)) * Smooth(-1f, -0.4f, v + (n - 0.5f) * 0.6f) * Smooth(1f, 0.6f, v);
            var hot = Smooth(wdt * 0.6f, 0f, Mathf.Abs(u)) * Smooth(-0.2f, 0.5f, v);
            var c = new Color(1f, 0.45f + hot * 0.4f, 0.12f + hot * 0.4f, Mathf.Clamp(a, 0, 1));
            alb.Set(x, y, c);
        }
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(alb.ToImage()),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            BillboardKeepScale = true,
            VertexColorUseAsAlbedo = true,
            ProximityFadeEnabled = true,
            ProximityFadeDistance = 0.6f,
        };
        Cache["flame"] = mat;
        return mat;
    }
}
