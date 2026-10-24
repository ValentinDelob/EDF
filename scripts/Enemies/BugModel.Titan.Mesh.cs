using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Atelier de maillage du titan reptilien : lofts à section superelliptique avec UV en mètres (écailles de taille
/// régulière, couture invisible), poids d'os mélangés (deux os par sommet : la queue et le cou se plient sans cassure),
/// masque de lueur de la gorge (UV2.y). Toute la géométrie émise est fusionnée en un seul maillage à os.
/// </summary>
public partial class BugModel
{
    private sealed class TitanMesh
    {
        private sealed class Surf
        {
            public readonly List<Vector3> V = new(), N = new();
            public readonly List<float> T = new(), W = new();
            public readonly List<Color> C = new();
            public readonly List<Vector2> UV = new(), UV2 = new();
            public readonly List<int> B = new(), I = new();
        }

        private readonly List<(Material M, Surf S)> _surfs = new();
        private Surf _cur;

        /// <summary>Os du sommet (position en pose de liaison) : premier os, second os, part du second.</summary>
        public Func<Vector3, (int A, int B, float T)> Weight = _ => (0, 0, 0f);
        /// <summary>Masque de lueur (gorge, bouche) du sommet, ou null.</summary>
        public Func<Vector3, float> Glow;
        /// <summary>Retranché des positions émises (débris de plaque centrés sur leur origine).</summary>
        public Vector3 Offset;
        /// <summary>Sans os (débris) : ni os ni poids.</summary>
        public bool Rigid;

        public TitanMesh Use(Material m)
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

        private int Vert(Vector3 p, Vector3 n, Vector3 t, Color c, Vector2 uv)
        {
            var s = _cur;
            s.V.Add(p - Offset);
            s.N.Add(n);
            s.T.Add(t.X); s.T.Add(t.Y); s.T.Add(t.Z); s.T.Add(1f);
            s.C.Add(c);
            s.UV.Add(uv);
            s.UV2.Add(new Vector2(0f, Glow?.Invoke(p) ?? 0f));
            if (!Rigid)
            {
                var (a, b, w) = Weight(p);
                if (a == b) w = 0f;
                s.B.Add(a); s.B.Add(b); s.B.Add(0); s.B.Add(0);
                s.W.Add(1f - w); s.W.Add(w); s.W.Add(0f); s.W.Add(0f);
            }
            return s.V.Count - 1;
        }

        private void Tri(int a, int b, int c)
        {
            var s = _cur;
            var n = s.N[a] + s.N[b] + s.N[c];
            var f = (s.V[b] - s.V[a]).Cross(s.V[c] - s.V[a]);
            if (f.LengthSquared() < 1e-12f) return;
            s.I.Add(a);
            if (f.Dot(n) > 0) { s.I.Add(c); s.I.Add(b); }
            else { s.I.Add(b); s.I.Add(c); }
        }

        /// <summary>
        /// Grille d'anneaux (rn × cn). <paramref name="wrap"/> : la dernière colonne répète la première (couture des UV) ;
        /// normales par différences finies, orientées vers l'extérieur de chaque anneau (vers <paramref name="centers"/>).
        /// </summary>
        public void Grid(Vector3[,] pos, Color[,] col, Vector2[,] uv, Vector3[] centers, bool wrap, Vector3[] axis = null)
        {
            var rn = pos.GetLength(0);
            var cn = pos.GetLength(1);
            var idx = new int[rn, cn];
            for (var i = 0; i < rn; i++)
            for (var j = 0; j < cn; j++)
            {
                var jm = j - 1;
                var jp = j + 1;
                if (wrap)
                {
                    if (jm < 0) jm = cn - 2;
                    if (jp > cn - 1) jp = 1;
                }
                else
                {
                    jm = Math.Max(jm, 0);
                    jp = Math.Min(jp, cn - 1);
                }
                var du = pos[i, jp] - pos[i, jm];
                var dv = pos[Math.Min(i + 1, rn - 1), j] - pos[Math.Max(i - 1, 0), j];
                var n = dv.Cross(du);
                var outward = pos[i, j] - centers[i];
                if (n.LengthSquared() < 1e-10f)
                {
                    // Bout d'un loft (rayon nul) : normale selon l'axe.
                    var ax = axis != null ? axis[i] : dv;
                    n = i == 0 ? -ax : ax;
                    if (n.LengthSquared() < 1e-10f) n = outward;
                }
                else if (outward.LengthSquared() > 1e-8f && n.Dot(outward) < 0) n = -n;
                else if (outward.LengthSquared() <= 1e-8f && axis != null) n = i == 0 ? -axis[i] : axis[i];
                n = n.Normalized();
                var t = du.LengthSquared() > 1e-10f ? du.Normalized() : Vector3.Right;
                idx[i, j] = Vert(pos[i, j], n, t, col[i, j], uv[i, j]);
            }
            for (var i = 0; i < rn - 1; i++)
            for (var j = 0; j < cn - 1; j++)
            {
                Tri(idx[i, j], idx[i + 1, j], idx[i + 1, j + 1]);
                Tri(idx[i, j], idx[i + 1, j + 1], idx[i, j + 1]);
            }
        }

        /// <summary>Anneau de loft : centre, axes de la section (X latéral, Y dorsal), demi-rayons.</summary>
        public struct Ring
        {
            public Vector3 C, X, Y;
            public float Rx, Ry;

            public Ring(Vector3 c, Vector3 x, Vector3 y, float rx, float ry)
            {
                C = c; X = x; Y = y; Rx = rx; Ry = ry;
            }
        }

        /// <summary>
        /// Loft d'anneaux à section superelliptique (sq = 2 : ellipse). Angle θ = 0 sur le dos (+Y), π/2 sur le flanc +X.
        /// UV en mètres : <paramref name="cell"/> m par tuile ; le tour d'anneau compte un nombre entier de tuiles et
        /// l'avancée le long du loft suit le périmètre local (les écailles rapetissent avec la section, sans s'étirer).
        /// <paramref name="radius"/>(i, θ) module le rayon, <paramref name="paint"/>(i, θ, position) colore.
        /// </summary>
        public void Loft(IList<Ring> rings, int radial, float sq, Func<int, float, Vector3, Color> paint, float cell = 1.2f,
            float belly = 0f, Func<int, float, float> radius = null, float uShift = 0f)
        {
            var rn = rings.Count;
            var cn = radial + 1;
            var pos = new Vector3[rn, cn];
            var col = new Color[rn, cn];
            var uv = new Vector2[rn, cn];
            var centers = new Vector3[rn];
            var axis = new Vector3[rn];
            var e = 2f / sq;
            var perim = new float[rn];
            var maxP = 0.01f;
            for (var i = 0; i < rn; i++)
            {
                var r = rings[i];
                perim[i] = Mathf.Tau * Mathf.Sqrt((r.Rx * r.Rx + r.Ry * r.Ry) * 0.5f);
                maxP = Mathf.Max(maxP, perim[i]);
            }
            var k = Mathf.Max(1, Mathf.RoundToInt(maxP / cell));
            var v = 0f;
            for (var i = 0; i < rn; i++)
            {
                var r = rings[i];
                centers[i] = r.C;
                axis[i] = (rings[Math.Min(i + 1, rn - 1)].C - rings[Math.Max(i - 1, 0)].C).Normalized();
                if (i > 0)
                {
                    // Avancée en tuiles : longueur parcourue rapportée à la taille locale d'une tuile.
                    var ds = r.C.DistanceTo(rings[i - 1].C);
                    var local = Mathf.Max(0.5f * (perim[i] + perim[i - 1]) / k, 0.05f);
                    v += ds / local;
                }
                for (var j = 0; j < cn; j++)
                {
                    var th = Mathf.Tau * (j % radial) / radial;
                    var sn = Mathf.Sin(th);
                    var cs = Mathf.Cos(th);
                    var ex = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), e);
                    var ey = Mathf.Sign(cs) * Mathf.Pow(Mathf.Abs(cs), e);
                    if (ey < 0) ey *= 1f - belly;
                    var m = radius?.Invoke(i, th) ?? 1f;
                    var p = r.C + r.X * (r.Rx * ex * m) + r.Y * (r.Ry * ey * m);
                    pos[i, j] = p;
                    col[i, j] = paint(i, th, p);
                    uv[i, j] = new Vector2((float)j / radial * k + uShift, v);
                }
            }
            Grid(pos, col, uv, centers, true, axis);
        }

        /// <summary>Repère orthonormé autour d'une direction (Y aussi proche que possible de <paramref name="up"/>).</summary>
        public static (Vector3 X, Vector3 Y) Frame(Vector3 dir, Vector3 up)
        {
            var x = up.Cross(dir);
            if (x.LengthSquared() < 1e-6f) x = Vector3.Forward.Cross(dir);
            if (x.LengthSquared() < 1e-6f) x = Vector3.Right.Cross(dir);
            x = x.Normalized();
            return (x, dir.Cross(x).Normalized());
        }

        /// <summary>Tube le long d'une polyligne (rayons par point, aplatissement Y), bouts fermés.</summary>
        public void Tube(IList<Vector3> pts, IList<float> rx, IList<float> ry, int radial, Func<int, float, Vector3, Color> paint, Vector3 up,
            float cell = 1.2f, float sq = 2f, Func<int, float, float> radius = null)
        {
            var n = pts.Count;
            var rings = new List<Ring>();
            var d0 = (pts[1] - pts[0]).Normalized();
            var (x0, y0) = Frame(d0, up);
            rings.Add(new Ring(pts[0] - d0 * 0.0001f, x0, y0, 0f, 0f));
            for (var i = 0; i < n; i++)
            {
                var d = (pts[Math.Min(i + 1, n - 1)] - pts[Math.Max(i - 1, 0)]).Normalized();
                var (x, y) = Frame(d, up);
                rings.Add(new Ring(pts[i], x, y, rx[i], ry[i]));
            }
            var dn = (pts[n - 1] - pts[n - 2]).Normalized();
            var (xn, yn) = Frame(dn, up);
            rings.Add(new Ring(pts[n - 1] + dn * 0.0001f, xn, yn, 0f, 0f));
            Loft(rings, radial, sq, (i, th, p) => paint(Math.Clamp(i - 1, 0, n - 1), th, p), cell, 0f,
                radius == null ? null : (i, th) => radius(Math.Clamp(i - 1, 0, n - 1), th));
        }

        /// <summary>Ellipsoïde (axes locaux tournés par <paramref name="rot"/>).</summary>
        public void Ellipsoid(Vector3 c, Vector3 r, Basis rot, int rings, int radial, Color col, float cell = 1f)
        {
            var list = new List<Ring>();
            for (var i = 0; i <= rings; i++)
            {
                var a = Mathf.Pi * i / rings;
                var z = -Mathf.Cos(a) * r.Z;
                var s = Mathf.Sin(a);
                list.Add(new Ring(c + rot * new Vector3(0, 0, z), rot.X, rot.Y, r.X * s, r.Y * s));
            }
            Loft(list, radial, 2f, (_, _, _) => col, cell);
        }

        /// <summary>Corne, griffe ou dent courbe le long d'une polyligne (rayon décroissant jusqu'à la pointe).</summary>
        public void Horn(IList<Vector3> pts, float r, float flat, int radial, Color baseCol, Color tipCol, Vector3 up)
        {
            var n = pts.Count;
            var rx = new float[n];
            var ry = new float[n];
            for (var i = 0; i < n; i++)
            {
                var k = i / (float)(n - 1);
                rx[i] = Mathf.Max(r * (1f - k) * (1f - 0.15f * k), 0.002f);
                ry[i] = rx[i] * flat;
            }
            Tube(pts, rx, ry, radial, (i, _, _) => baseCol.Lerp(tipCol, i / (float)(n - 1)), up, 0.5f);
        }

        /// <summary>Plaque bombée posée sur une surface : grille (u, v) dont les rangées du bord plongent sous la peau (bord épais).</summary>
        public void Patch(Vector3[,] pos, Color[,] col, Vector2[,] uv, Vector3 inward)
        {
            var rn = pos.GetLength(0);
            var centers = new Vector3[rn];
            // Centre de référence sous la plaque : les normales pointent vers l'extérieur de la plaque.
            for (var i = 0; i < rn; i++) centers[i] = pos[rn / 2, pos.GetLength(1) / 2] + inward * 6f;
            Grid(pos, col, uv, centers, false);
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
                arrays[(int)Mesh.ArrayType.TexUV] = s.UV.ToArray();
                arrays[(int)Mesh.ArrayType.TexUV2] = s.UV2.ToArray();
                if (!Rigid)
                {
                    arrays[(int)Mesh.ArrayType.Bones] = s.B.ToArray();
                    arrays[(int)Mesh.ArrayType.Weights] = s.W.ToArray();
                }
                arrays[(int)Mesh.ArrayType.Index] = s.I.ToArray();
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, mat);
            }
            return mesh;
        }
    }
}
