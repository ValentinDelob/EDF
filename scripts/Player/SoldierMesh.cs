using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Constructeur de maillages lissés pour les soldats : lofts à sections superelliptiques,
/// ellipsoïdes, boîtes arrondies et coques, fusionnés en une seule ArrayMesh (une surface par matériau).
/// </summary>
public sealed class SoldierMesh
{
    private sealed class Part
    {
        public readonly SurfaceTool St = new();
        public int Count;
    }

    private readonly Dictionary<Material, Part> _parts = new();
    private readonly List<Material> _order = new();

    /// <summary>Nombre de triangles émis (diagnostic).</summary>
    public int Triangles { get; private set; }

    private Part Get(Material m)
    {
        if (_parts.TryGetValue(m, out var p)) return p;
        p = new Part();
        p.St.Begin(Mesh.PrimitiveType.Triangles);
        _parts[m] = p;
        _order.Add(m);
        return p;
    }

    private static int Add(Part p, Vector3 v, Vector3 n, Color c)
    {
        p.St.SetNormal(n);
        p.St.SetColor(c);
        p.St.AddVertex(v);
        return p.Count++;
    }

    // Godot : faces avant dans le sens horaire.
    private void Tri(Part p, int a, int b, int c)
    {
        p.St.AddIndex(a);
        p.St.AddIndex(b);
        p.St.AddIndex(c);
        Triangles++;
    }

    /// <summary>Repère orthonormé (A, B) perpendiculaire à la tangente t, orienté selon l'axe de référence.</summary>
    private static (Vector3 A, Vector3 B) Frame(Vector3 t, Vector3 refAxis)
    {
        var a = refAxis - t * refAxis.Dot(t);
        if (a.LengthSquared() < 1e-6f)
        {
            var alt = Mathf.Abs(t.Y) < 0.9f ? Vector3.Up : Vector3.Right;
            a = alt - t * alt.Dot(t);
        }
        a = a.Normalized();
        return (a, t.Cross(a));
    }

    /// <summary>
    /// Loft le long d'une polyligne : section superelliptique (sq = 2 : ellipse, plus grand : carré arrondi),
    /// rayon X le long de l'axe de référence projeté, rayon Y le long de l'axe perpendiculaire.
    /// </summary>
    public void Tube(Material m, Color col, IList<Vector3> pts, IList<Vector2> rad, int sides = 10, float sq = 2f,
        Vector3? refAxis = null, bool cap0 = true, bool cap1 = true, bool loop = false, IList<Color> ringCols = null)
    {
        var p = Get(m);
        var n = pts.Count;
        var rf = refAxis ?? Vector3.Right;
        var e = 2f / sq;
        var grid = new Vector3[n, sides];
        var tans = new Vector3[n];
        for (var i = 0; i < n; i++)
        {
            Vector3 t;
            if (loop) t = pts[(i + 1) % n] - pts[(i - 1 + n) % n];
            else if (i == 0) t = pts[1] - pts[0];
            else if (i == n - 1) t = pts[n - 1] - pts[n - 2];
            else t = (pts[i + 1] - pts[i]).Normalized() + (pts[i] - pts[i - 1]).Normalized();
            t = t.Normalized();
            tans[i] = t;
            var (a, b) = Frame(t, rf);
            for (var j = 0; j < sides; j++)
            {
                var th = Mathf.Tau * j / sides;
                var c = Mathf.Cos(th);
                var s = Mathf.Sin(th);
                var u = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e);
                var v = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e);
                grid[i, j] = pts[i] + a * (rad[i].X * u) + b * (rad[i].Y * v);
            }
        }
        var idx = new int[n, sides];
        for (var i = 0; i < n; i++)
        {
            var ip = loop ? (i + 1) % n : Mathf.Min(i + 1, n - 1);
            var im = loop ? (i - 1 + n) % n : Mathf.Max(i - 1, 0);
            var cc = ringCols != null ? ringCols[i] : col;
            for (var j = 0; j < sides; j++)
            {
                var dth = grid[i, (j + 1) % sides] - grid[i, (j - 1 + sides) % sides];
                var dt = grid[ip, j] - grid[im, j];
                var nn = dth.Cross(dt);
                if (nn.LengthSquared() < 1e-12f)
                {
                    var radial = grid[i, j] - pts[i];
                    nn = radial.LengthSquared() > 1e-10f && i != 0 && i != n - 1 ? radial : (i == 0 ? -tans[i] : tans[i]);
                }
                idx[i, j] = Add(p, grid[i, j], nn.Normalized(), cc);
            }
        }
        var segs = loop ? n : n - 1;
        for (var i = 0; i < segs; i++)
        {
            var i1 = (i + 1) % n;
            for (var j = 0; j < sides; j++)
            {
                var j1 = (j + 1) % sides;
                Tri(p, idx[i, j], idx[i1, j], idx[i, j1]);
                Tri(p, idx[i1, j], idx[i1, j1], idx[i, j1]);
            }
        }
        if (loop) return;
        if (cap0 && rad[0].LengthSquared() > 1e-8f)
        {
            var cc = ringCols != null ? ringCols[0] : col;
            var ctr = Add(p, pts[0], -tans[0], cc);
            var ring = new int[sides];
            for (var j = 0; j < sides; j++) ring[j] = Add(p, grid[0, j], -tans[0], cc);
            for (var j = 0; j < sides; j++) Tri(p, ctr, ring[j], ring[(j + 1) % sides]);
        }
        if (cap1 && rad[n - 1].LengthSquared() > 1e-8f)
        {
            var cc = ringCols != null ? ringCols[n - 1] : col;
            var ctr = Add(p, pts[n - 1], tans[n - 1], cc);
            var ring = new int[sides];
            for (var j = 0; j < sides; j++) ring[j] = Add(p, grid[n - 1, j], tans[n - 1], cc);
            for (var j = 0; j < sides; j++) Tri(p, ctr, ring[(j + 1) % sides], ring[j]);
        }
    }

    /// <summary>Ellipsoïde orienté (axe des pôles = Y local).</summary>
    public void Ellipsoid(Material m, Color col, Vector3 center, Vector3 radii, Basis? rot = null, int sides = 10, int rings = 6)
    {
        var r = rot ?? Basis.Identity;
        var pts = new Vector3[rings + 1];
        var rad = new Vector2[rings + 1];
        for (var k = 0; k <= rings; k++)
        {
            var ph = Mathf.Pi * k / rings;
            pts[k] = center + r * new Vector3(0, -radii.Y * Mathf.Cos(ph), 0);
            var s = Mathf.Max(Mathf.Sin(ph), 0.0f);
            rad[k] = new Vector2(radii.X * s, radii.Z * s);
        }
        Tube(m, col, pts, rad, sides, 2f, r * Vector3.Right, false, false);
    }

    /// <summary>Boîte aux arêtes adoucies (poches, plaques, boîtiers).</summary>
    public void Box(Material m, Color col, Vector3 center, Vector3 size, Basis? rot = null, float bevel = -1f, float sq = 7f, int sides = 8)
    {
        var r = rot ?? Basis.Identity;
        var b = bevel < 0 ? Mathf.Min(Mathf.Min(size.X, size.Y), size.Z) * 0.22f : bevel;
        var hy = size.Y * 0.5f;
        var rx = size.X * 0.5f;
        var rz = size.Z * 0.5f;
        var up = r * Vector3.Up;
        var pts = new[] { center - up * hy, center - up * (hy - b), center + up * (hy - b), center + up * hy };
        var rad = new[] { new Vector2(rx - b, rz - b), new Vector2(rx, rz), new Vector2(rx, rz), new Vector2(rx - b, rz - b) };
        Tube(m, col, pts, rad, sides, sq, r * Vector3.Right);
    }

    /// <summary>
    /// Coque de casque : portion d'ellipsoïde dont le bord inférieur suit une hauteur variable selon l'azimut
    /// (front, côtés, nuque), avec un bourrelet le long du bord. Renvoie les points du bord.
    /// </summary>
    public Vector3[] Shell(Material m, Color col, Vector3 c, Vector3 r, System.Func<float, float> bottomY, int sides = 18, int rows = 7)
    {
        var p = Get(m);
        var idx = new int[rows + 1, sides];
        var top = Add(p, c + new Vector3(0, r.Y, 0), Vector3.Up, col);
        var rim = new Vector3[sides];
        for (var j = 0; j < sides; j++)
        {
            var th = Mathf.Tau * j / sides; // 0 = avant (+Z)
            var yb = Mathf.Clamp((bottomY(th) - c.Y) / r.Y, -0.95f, 0.95f);
            var phMax = Mathf.Acos(yb);
            for (var k = 1; k <= rows; k++)
            {
                var ph = phMax * k / rows;
                var dir = new Vector3(Mathf.Sin(ph) * Mathf.Sin(th), Mathf.Cos(ph), Mathf.Sin(ph) * Mathf.Cos(th));
                var v = c + new Vector3(dir.X * r.X, dir.Y * r.Y, dir.Z * r.Z);
                var nn = new Vector3(dir.X / r.X, dir.Y / r.Y, dir.Z / r.Z).Normalized();
                idx[k, j] = Add(p, v, nn, col);
                if (k == rows) rim[j] = v;
            }
        }
        for (var j = 0; j < sides; j++)
        {
            var j1 = (j + 1) % sides;
            Tri(p, top, idx[1, j1], idx[1, j]);
            for (var k = 1; k < rows; k++)
            {
                Tri(p, idx[k, j], idx[k, j1], idx[k + 1, j]);
                Tri(p, idx[k, j1], idx[k + 1, j1], idx[k + 1, j]);
            }
        }
        return rim;
    }

    /// <summary>Produit le MeshInstance3D (une surface par matériau, ombres actives).</summary>
    public MeshInstance3D Build() => new() { Mesh = Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.On };

    public ArrayMesh Commit()
    {
        var mesh = new ArrayMesh();
        foreach (var m in _order)
        {
            var st = _parts[m].St;
            st.SetMaterial(m);
            st.Commit(mesh);
        }
        return mesh;
    }
}

/// <summary>Matériaux des soldats : camouflage généré, sangles et équipements teintés par sommet, peau.</summary>
public static class SoldierMats
{
    private static readonly Dictionary<string, Material> Cache = new();

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

    /// <summary>Bruit de valeur périodique (tuile sans raccord).</summary>
    private static float VNoise(float x, float y, float cell, int seed, int size)
    {
        var period = Mathf.Max(1, Mathf.RoundToInt(size / cell));
        var fx = x / cell;
        var fy = y / cell;
        var ix = Mathf.FloorToInt(fx);
        var iy = Mathf.FloorToInt(fy);
        var tx = fx - ix;
        var ty = fy - iy;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        float H(int i, int j) => Hash(((i % period) + period) % period, ((j % period) + period) % period, seed);
        return Mathf.Lerp(Mathf.Lerp(H(ix, iy), H(ix + 1, iy), tx), Mathf.Lerp(H(ix, iy + 1), H(ix + 1, iy + 1), tx), ty);
    }

    private static float Fbm(float x, float y, float cell, int seed, int size)
        => VNoise(x, y, cell, seed, size) * 0.6f + VNoise(x, y, cell / 2, seed + 1, size) * 0.28f + VNoise(x, y, cell / 4, seed + 2, size) * 0.12f;

    private static byte B(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

    private static ImageTexture Tex(byte[] d, int size, Image.Format f)
    {
        var img = Image.CreateFromData(size, size, false, f, d);
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Carte de normales de tissu : trame fine + plis doux.</summary>
    private static ImageTexture FabricNormal(int seed, float folds)
    {
        const int size = 256;
        var d = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var weave = ((x / 2 + y / 2) % 2 == 0 ? 0.04f : 0f) + Hash(x, y, seed) * 0.05f;
            var fold = Fbm(x, y * 0.5f, 48, seed + 3, size) * folds;
            var h = 0.5f + weave + fold;
            var i = (y * size + x) * 4;
            d[i] = d[i + 1] = d[i + 2] = B(h);
            d[i + 3] = 255;
        }
        var img = Image.CreateFromData(size, size, false, Image.Format.Rgba8, d);
        img.BumpMapToNormalMap(3f);
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Camouflage à quatre teintes (taches nettes déformées), appliqué en triplanaire local.</summary>
    public static StandardMaterial3D Camo(string key, Color c0, Color c1, Color c2, Color c3, int seed)
    {
        key = "camo" + key;
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        const int size = 256;
        var d = new byte[size * size * 4];
        System.Threading.Tasks.Parallel.For(0, size, y =>
        {
            for (var x = 0; x < size; x++)
            {
                // Déformation du domaine : taches organiques plutôt que des blobs ronds.
                var wx = x + (VNoise(x, y, 32, seed + 9, size) - 0.5f) * 26f;
                var wy = y + (VNoise(x, y, 32, seed + 11, size) - 0.5f) * 26f;
                var c = c0;
                if (Fbm(wx, wy * 1.3f, 64, seed, size) > 0.53f) c = c1;
                if (Fbm(wx + 40, wy, 32, seed + 20, size) > 0.6f) c = c2;
                if (Fbm(wx, wy + 70, 16, seed + 40, size) > 0.66f) c = c3;
                var grain = (Hash(x, y, seed + 5) - 0.5f) * 0.05f + (VNoise(x, y, 4, seed + 7, size) - 0.5f) * 0.06f;
                c = new Color(c.R + grain, c.G + grain, c.B + grain);
                var i = (y * size + x) * 4;
                d[i] = B(c.R); d[i + 1] = B(c.G); d[i + 2] = B(c.B); d[i + 3] = 255;
            }
        });
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = Tex(d, size, Image.Format.Rgba8),
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            NormalEnabled = true,
            NormalTexture = FabricNormal(seed + 100, 0.6f),
            NormalScale = 0.7f,
            Roughness = 0.92f,
            Uv1Triplanar = true,
            Uv1TriplanarSharpness = 2f,
            Uv1Scale = new Vector3(3.2f, 3.2f, 3.2f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        Cache[key] = mat;
        return mat;
    }

    /// <summary>Nylon / cordura : blanc légèrement bruité, teinté par la couleur de sommet.</summary>
    public static StandardMaterial3D Gear()
    {
        if (Cache.TryGetValue("gear", out var m)) return (StandardMaterial3D)m;
        const int size = 128;
        var d = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var v = 0.86f + Fbm(x, y, 16, 301, size) * 0.12f + (Hash(x, y, 303) - 0.5f) * 0.05f;
            var i = (y * size + x) * 4;
            d[i] = d[i + 1] = d[i + 2] = B(v);
            d[i + 3] = 255;
        }
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = Tex(d, size, Image.Format.Rgba8),
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            NormalEnabled = true,
            NormalTexture = FabricNormal(310, 0.3f),
            NormalScale = 0.6f,
            Roughness = 0.86f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(3.5f, 3.5f, 3.5f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        Cache["gear"] = mat;
        return mat;
    }

    /// <summary>Surface dure (polymère, peinture, métal) teintée par sommet.</summary>
    public static StandardMaterial3D Hard(float rough, float metal)
    {
        var key = $"hard{rough}{metal}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Roughness = rough,
            Metallic = metal,
            NormalEnabled = true,
            NormalTexture = FabricNormal(320, 0.15f),
            NormalScale = 0.18f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(4f, 4f, 4f),
        };
        Cache[key] = mat;
        return mat;
    }

    /// <summary>Peau : teinte de base × couleur de sommet (zones plus sombres : lèvres, barbe naissante).</summary>
    public static StandardMaterial3D Skin(Color tone)
    {
        var key = $"skin{tone}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = tone,
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Roughness = 0.62f,
            RimEnabled = true,
            Rim = 0.12f,
            RimTint = 0.7f,
        };
        Cache[key] = mat;
        return mat;
    }

    /// <summary>Verre teinté (lunettes balistiques, visière, optiques).</summary>
    public static StandardMaterial3D Lens(Color tint)
    {
        var key = $"lens{tint}";
        if (Cache.TryGetValue(key, out var m)) return (StandardMaterial3D)m;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = tint,
            Roughness = 0.06f,
            Metallic = 0.5f,
            MetallicSpecular = 0.9f,
        };
        Cache[key] = mat;
        return mat;
    }
}
