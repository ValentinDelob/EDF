using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Atelier de maillages organiques pour les insectes : lofts à sections arrondies (superellipses),
/// tubes effilés, cônes (soies, épines, griffes), ellipsoïdes, tores, voiles plats et blocs.
/// Toute la géométrie émise est fusionnée en un seul ArrayMesh (une surface par matériau).
/// Les couleurs passent par les couleurs de sommets : un même matériau sert à tout un insecte.
/// </summary>
public sealed class InsectMesh
{
    /// <summary>Anneau de loft : centre, axes de la section, demi-rayons et couleur.</summary>
    public struct Ring
    {
        public Vector3 C, X, Y;
        public float Rx, Ry;
        public Color Col;

        public Ring(Vector3 c, Vector3 x, Vector3 y, float rx, float ry, Color col)
        {
            C = c; X = x; Y = y; Rx = rx; Ry = ry; Col = col;
        }
    }

    private sealed class Surf
    {
        public readonly List<Vector3> V = new();
        public readonly List<Vector3> N = new();
        public readonly List<float> T = new();
        public readonly List<Color> C = new();
        public readonly List<int> I = new();
    }

    private readonly List<(Material Mat, Surf S)> _surfs = new();
    private Surf _cur;

    /// <summary>Transformation appliquée à toute la géométrie émise (pose d'une pièce).</summary>
    public Transform3D Xf = Transform3D.Identity;

    /// <summary>Miroir gauche/droite (X → -X), appliqué après Xf.</summary>
    public bool Mirror;

    /// <summary>Sélectionne (ou crée) la surface associée à ce matériau.</summary>
    public InsectMesh Use(Material m)
    {
        foreach (var (mat, s) in _surfs)
            if (mat == m) { _cur = s; return this; }
        _cur = new Surf();
        _surfs.Add((m, _cur));
        return this;
    }

    public int Triangles
    {
        get
        {
            var n = 0;
            foreach (var (_, s) in _surfs) n += s.I.Count / 3;
            return n;
        }
    }

    private Vector3 P(Vector3 p)
    {
        p = Xf * p;
        if (Mirror) p.X = -p.X;
        return p;
    }

    public ArrayMesh Commit()
    {
        var mesh = new ArrayMesh();
        foreach (var (mat, s) in _surfs)
        {
            if (s.I.Count == 0) continue;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = s.V.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = s.N.ToArray();
            arrays[(int)Mesh.ArrayType.Tangent] = s.T.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = s.C.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = s.I.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, mat);
        }
        return mesh;
    }

    private int Vert(Vector3 p, Vector3 n, Vector3 t, Color c)
    {
        var s = _cur;
        s.V.Add(p);
        s.N.Add(n);
        s.T.Add(t.X); s.T.Add(t.Y); s.T.Add(t.Z); s.T.Add(1f);
        s.C.Add(c);
        return s.V.Count - 1;
    }

    /// <summary>Triangle orienté automatiquement d'après les normales (Godot : face avant = sens horaire).</summary>
    private void Tri(int a, int b, int c)
    {
        var s = _cur;
        var n = s.N[a] + s.N[b] + s.N[c];
        var f = (s.V[b] - s.V[a]).Cross(s.V[c] - s.V[a]);
        if (f.LengthSquared() < 1e-14f) return;
        s.I.Add(a);
        if (f.Dot(n) > 0) { s.I.Add(c); s.I.Add(b); }
        else { s.I.Add(b); s.I.Add(c); }
    }

    /// <summary>
    /// Grille d'anneaux fermés (radial bouclé). Les normales sont calculées par différences finies
    /// et orientées vers l'extérieur de chaque anneau (ou l'intérieur si <paramref name="inward"/>).
    /// </summary>
    public void Grid(Vector3[,] pos, Color[,] col, Vector3[] centers, bool inward = false, bool wrapRings = false)
    {
        var rn = pos.GetLength(0);
        var m = pos.GetLength(1);
        var p = new Vector3[rn, m];
        var c = new Vector3[rn];
        var pole = new bool[rn];
        for (var i = 0; i < rn; i++)
        {
            c[i] = P(centers[i]);
            var maxD = 0f;
            for (var j = 0; j < m; j++)
            {
                p[i, j] = P(pos[i, j]);
                maxD = Mathf.Max(maxD, p[i, j].DistanceSquaredTo(p[i, 0]));
            }
            pole[i] = maxD < 1e-10f;
        }
        var baseIdx = _cur.V.Count;
        for (var i = 0; i < rn; i++)
        {
            int ip = i + 1, im = i - 1;
            if (wrapRings) { ip %= rn; im = (im + rn) % rn; }
            else { ip = Math.Min(ip, rn - 1); im = Math.Max(im, 0); }
            for (var j = 0; j < m; j++)
            {
                var du = p[i, (j + 1) % m] - p[i, (j + m - 1) % m];
                var dv = p[ip, j] - p[im, j];
                Vector3 n;
                if (pole[i])
                {
                    // Pôle : normale = du pôle vers l'extérieur, à l'opposé de l'anneau voisin.
                    var nb = i == 0 ? 1 : i - 1;
                    var avg = Vector3.Zero;
                    for (var k = 0; k < m; k++) avg += p[nb, k];
                    n = p[i, j] - avg / m;
                    if (inward) n = -n;
                    du = n.Cross(Vector3.Up);
                    if (du.LengthSquared() < 1e-8f) du = n.Cross(Vector3.Right);
                }
                else
                {
                    n = du.Cross(dv);
                    var outward = p[i, j] - c[i];
                    if (inward) outward = -outward;
                    if (n.Dot(outward) < 0) n = -n;
                }
                n = n.LengthSquared() > 1e-14f ? n.Normalized() : Vector3.Up;
                var t = du.LengthSquared() > 1e-14f ? du.Normalized() : Vector3.Right;
                Vert(p[i, j], n, t, col[i, j]);
            }
        }
        var last = wrapRings ? rn : rn - 1;
        for (var i = 0; i < last; i++)
        {
            var i2 = (i + 1) % rn;
            if (pole[i] && pole[i2]) continue;
            for (var j = 0; j < m; j++)
            {
                var j2 = (j + 1) % m;
                int a = baseIdx + i * m + j, b = baseIdx + i * m + j2, cc = baseIdx + i2 * m + j2, d = baseIdx + i2 * m + j;
                if (pole[i]) Tri(a, cc, d);
                else if (pole[i2]) Tri(a, b, cc);
                else { Tri(a, b, cc); Tri(a, cc, d); }
            }
        }
    }

    /// <summary>
    /// Loft d'anneaux à section superelliptique (sq = 2 : ellipse, plus grand : plus carré).
    /// <paramref name="belly"/> aplatit le dessous. <paramref name="paint"/> colore chaque sommet
    /// (indice d'anneau, angle, position locale, couleur de l'anneau).
    /// </summary>
    public void Loft(IList<Ring> rings, int radial, float sq = 2f, float belly = 0f, Func<int, float, Vector3, Color, Color> paint = null, Func<int, float, float> radius = null)
    {
        var rn = rings.Count;
        var pos = new Vector3[rn, radial];
        var col = new Color[rn, radial];
        var centers = new Vector3[rn];
        var e = 2f / sq;
        for (var i = 0; i < rn; i++)
        {
            var r = rings[i];
            centers[i] = r.C;
            for (var j = 0; j < radial; j++)
            {
                var th = Mathf.Pi / 2 + Mathf.Tau * j / radial;
                var cs = Mathf.Cos(th);
                var sn = Mathf.Sin(th);
                var ex = Mathf.Sign(cs) * Mathf.Pow(Mathf.Abs(cs), e);
                var ey = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), e);
                if (ey < 0) ey *= 1f - belly;
                var k = radius?.Invoke(i, th) ?? 1f;
                var p = r.C + r.X * (r.Rx * ex * k) + r.Y * (r.Ry * ey * k);
                pos[i, j] = p;
                col[i, j] = paint?.Invoke(i, th, p, r.Col) ?? r.Col;
            }
        }
        Grid(pos, col, centers);
    }

    /// <summary>Repère orthonormé autour d'une direction (Y aussi proche que possible de « up »).</summary>
    public static (Vector3 X, Vector3 Y) Frame(Vector3 dir, Vector3 up)
    {
        var x = up.Cross(dir);
        if (x.LengthSquared() < 1e-6f) x = Vector3.Forward.Cross(dir);
        if (x.LengthSquared() < 1e-6f) x = Vector3.Right.Cross(dir);
        x = x.Normalized();
        return (x, dir.Cross(x).Normalized());
    }

    /// <summary>Tube le long d'une polyligne (rayons par point, aplatissement ry = rx·flat).</summary>
    public void Tube(IList<Vector3> pts, IList<float> radii, int radial, Color col, Vector3 up, float flat = 1f, IList<Color> cols = null, float sq = 2f)
    {
        var n = pts.Count;
        var rings = new Ring[n];
        for (var i = 0; i < n; i++)
        {
            var d = (pts[Math.Min(i + 1, n - 1)] - pts[Math.Max(i - 1, 0)]).Normalized();
            var (x, y) = Frame(d, up);
            rings[i] = new Ring(pts[i], x, y, radii[i], radii[i] * flat, cols != null ? cols[i] : col);
        }
        Loft(rings, radial, sq);
    }

    /// <summary>
    /// Segment de patte effilé de a à b, aux extrémités arrondies, avec renflement articulaire
    /// optionnel près de l'extrémité distale (knob) et profil personnalisé (shape(t) multiplie le rayon).
    /// </summary>
    public void Segment(Vector3 a, Vector3 b, float r0, float r1, int radial, Color col, Vector3 up, float knob = 0f, int mid = 3, Func<float, float> shape = null, Func<float, Color> paint = null, float flat = 1f)
    {
        var len = a.DistanceTo(b);
        var c0 = Mathf.Min(0.4f, r0 * 0.9f / len);
        var c1 = Mathf.Min(0.4f, r1 * 0.9f / len);
        var ts = new List<float> { 0f, c0 * 0.45f, c0 };
        for (var k = 1; k <= mid; k++) ts.Add(Mathf.Lerp(c0, 1 - c1, k / (float)(mid + 1)));
        ts.Add(1 - c1); ts.Add(1 - c1 * 0.45f); ts.Add(1f);
        var pts = new Vector3[ts.Count];
        var rad = new float[ts.Count];
        var cols = new Color[ts.Count];
        for (var i = 0; i < ts.Count; i++)
        {
            var t = ts[i];
            pts[i] = a.Lerp(b, t);
            var round = 1f;
            if (t < c0) round = Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow(1 - t / c0, 2)));
            else if (t > 1 - c1) round = Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow(1 - (1 - t) / c1, 2)));
            var r = Mathf.Lerp(r0, r1, t) * round * (shape?.Invoke(t) ?? 1f);
            r *= 1f + knob * Mathf.Exp(-Mathf.Pow((t - 0.9f) / 0.07f, 2));
            rad[i] = r;
            cols[i] = paint?.Invoke(t) ?? col;
        }
        Tube(pts, rad, radial, col, up, flat, cols);
    }

    /// <summary>Ellipsoïde (axes locaux tournés par <paramref name="rot"/>).</summary>
    public void Ellipsoid(Vector3 c, Vector3 r, int rings, int radial, Color col, Basis? rot = null, Func<int, float, Vector3, Color, Color> paint = null)
    {
        var b = rot ?? Basis.Identity;
        var list = new Ring[rings + 1];
        for (var i = 0; i <= rings; i++)
        {
            var a = Mathf.Pi * i / rings;
            var z = -Mathf.Cos(a) * r.Z;
            var s = Mathf.Sin(a);
            list[i] = new Ring(c + b * new Vector3(0, 0, z), b.X, b.Y, r.X * s, r.Y * s, col);
        }
        Loft(list, radial, 2f, 0f, paint);
    }

    /// <summary>Cône fin (soie, épine, dent, griffe) de la base a à la pointe b.</summary>
    public void Cone(Vector3 a, Vector3 b, float r, int sides, Color col, bool closed = false)
    {
        var d = (b - a).Normalized();
        var (x, y) = Frame(d, Vector3.Up);
        var list = new List<Ring>();
        if (closed) list.Add(new Ring(a - d * r * 0.5f, x, y, 0, 0, col));
        list.Add(new Ring(a, x, y, r, r, col));
        list.Add(new Ring(b, x, y, 0, 0, col));
        Loft(list, sides);
    }

    /// <summary>Cône courbe (griffe, crochet, dard) le long d'une polyligne.</summary>
    public void Hook(IList<Vector3> pts, float r, int sides, Color col, Vector3 up)
    {
        var rad = new float[pts.Count];
        for (var i = 0; i < pts.Count; i++) rad[i] = r * (1f - i / (float)(pts.Count - 1));
        Tube(pts, rad, sides, col, up);
    }

    /// <summary>Tore (axe local Y tourné par rot), grand rayon R, rayon de tube r.</summary>
    public void Torus(Vector3 c, Basis rot, float bigR, float r, int segs, int radial, Color col, Func<float, float> rMod = null)
    {
        var pos = new Vector3[segs, radial];
        var cols = new Color[segs, radial];
        var centers = new Vector3[segs];
        for (var i = 0; i < segs; i++)
        {
            var a = Mathf.Tau * i / segs;
            var rr = bigR * (rMod?.Invoke(a) ?? 1f);
            var dir = rot * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var cc = c + dir * rr;
            centers[i] = cc;
            for (var j = 0; j < radial; j++)
            {
                var b = Mathf.Tau * j / radial;
                pos[i, j] = cc + dir * (Mathf.Cos(b) * r) + rot.Y * (Mathf.Sin(b) * r);
                cols[i, j] = col;
            }
        }
        Grid(pos, cols, centers, false, true);
    }

    /// <summary>Voile plat (aile) en éventail depuis le centroïde, couleurs par sommet de contour.</summary>
    public void Flat(IList<Vector3> outline, Vector3 normal, IList<Color> cols, Color centerCol)
    {
        var center = Vector3.Zero;
        foreach (var p in outline) center += p;
        center /= outline.Count;
        var n = (Xf.Basis * normal).Normalized();
        if (Mirror) n.X = -n.X;
        var t = (P(outline[0]) - P(center)).Normalized();
        var ci = Vert(P(center), n, t, centerCol);
        var first = _cur.V.Count;
        for (var i = 0; i < outline.Count; i++) Vert(P(outline[i]), n, t, cols[i]);
        for (var i = 0; i < outline.Count; i++) Tri(ci, first + i, first + (i + 1) % outline.Count);
    }

    /// <summary>Bloc à faces planes (gravats, panneaux).</summary>
    public void Box(Transform3D t, Vector3 half, Color col)
    {
        for (var axis = 0; axis < 3; axis++)
        for (var s = -1; s <= 1; s += 2)
        {
            var n = Vector3.Zero; n[axis] = s;
            var u = Vector3.Zero; u[(axis + 1) % 3] = 1;
            var v = Vector3.Zero; v[(axis + 2) % 3] = 1;
            var c = n * half[axis];
            var du = u * half[(axis + 1) % 3];
            var dv = v * half[(axis + 2) % 3];
            var wn = (Xf.Basis * (t.Basis * n)).Normalized();
            if (Mirror) wn.X = -wn.X;
            var wt = (Xf.Basis * (t.Basis * u)).Normalized();
            var i0 = Vert(P(t * (c - du - dv)), wn, wt, col);
            var i1 = Vert(P(t * (c + du - dv)), wn, wt, col);
            var i2 = Vert(P(t * (c + du + dv)), wn, wt, col);
            var i3 = Vert(P(t * (c - du + dv)), wn, wt, col);
            Tri(i0, i1, i2);
            Tri(i0, i2, i3);
        }
    }

    /// <summary>Interpolation Catmull-Rom d'une suite de clés uniformément réparties sur [0, 1].</summary>
    public static float Spline(float[] keys, float t)
    {
        var n = keys.Length - 1;
        var f = Mathf.Clamp(t, 0f, 1f) * n;
        var i = Math.Min((int)f, n - 1);
        var u = f - i;
        var p0 = keys[Math.Max(i - 1, 0)];
        var p1 = keys[i];
        var p2 = keys[i + 1];
        var p3 = keys[Math.Min(i + 2, n)];
        return 0.5f * (2 * p1 + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (-p0 + 3 * p1 - 3 * p2 + p3) * u * u * u);
    }
}
