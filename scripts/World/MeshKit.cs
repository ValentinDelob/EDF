using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Assembleur de géométrie statique : accumule boîtes, cylindres et quads dans des tableaux C#
/// par matériau, puis produit un seul ArrayMesh (une surface par matériau).
/// Les sommets restent côté C# jusqu'au Commit (pas d'appel moteur par sommet, contrairement à SurfaceTool).
/// L'ordre des sommets est corrigé automatiquement (Godot : faces avant dans le sens horaire).
/// </summary>
public class MeshKit
{
    /// <summary>Sommets d'une surface (triangles non indexés).</summary>
    private sealed class Surf
    {
        public readonly List<Vector3> Pos = new();
        public readonly List<Vector3> Nrm = new();
        public readonly List<Color> Col = new();
        public readonly List<Vector2> Uv = new();
    }

    private readonly Dictionary<Material, Surf> _st = new();
    private readonly List<Material> _order = new();

    /// <summary>Transformation appliquée à tout ce qui est ajouté (repère local du sous-objet).</summary>
    public Transform3D Xf = Transform3D.Identity;

    /// <summary>Couleur de sommet courante (utilisée par les matériaux « Detail »).</summary>
    public Color Col = Colors.White;

    public bool Empty => _order.Count == 0;

    /// <summary>
    /// Enregistre aussi les triangles solides (forme de collision exacte du visuel) ; désactivé par défaut.
    /// Le feuillage n'est jamais solide.
    /// </summary>
    public bool Record;

    /// <summary>Les primitives suivantes sont solides (si <see cref="Record"/>) : à couper pour le décor traversable.</summary>
    public bool Solid = true;

    private readonly List<Vector3> _solid = new();
    private bool _curSolid;

    /// <summary>Triangles solides par maillage produit (repère du maillage), pour les formes de collision.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Mesh, Vector3[]> SolidFaces = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Mesh, ConcavePolygonShape3D> SolidShapes = new();

    /// <summary>Triangles solides d'un maillage produit avec <see cref="Record"/> (vide sinon).</summary>
    public static Vector3[] FacesOf(Mesh m) => m != null && SolidFaces.TryGetValue(m, out var f) ? f : System.Array.Empty<Vector3>();

    /// <summary>Forme de collision exacte (triangles, deux faces) partagée par toutes les instances du maillage, ou null.</summary>
    public static ConcavePolygonShape3D ShapeOf(Mesh m)
    {
        if (m == null) return null;
        if (SolidShapes.TryGetValue(m, out var s)) return s;
        var f = FacesOf(m);
        s = f.Length == 0 ? null : new ConcavePolygonShape3D { Data = f, BackfaceCollision = true };
        SolidShapes.AddOrUpdate(m, s);
        return s;
    }

    /// <summary>Forme de collision des triangles solides enregistrés jusqu'ici (null si aucun).</summary>
    public ConcavePolygonShape3D SolidShape() =>
        _solid.Count == 0 ? null : new ConcavePolygonShape3D { Data = _solid.ToArray(), BackfaceCollision = true };

    private Surf S(Material m)
    {
        _curSolid = Record && Solid && !Mats.IsFoliage(m);
        if (_st.TryGetValue(m, out var st)) return st;
        st = new Surf();
        _st[m] = st;
        _order.Add(m);
        return st;
    }

    private void V(Surf st, Vector3 p, Vector3 n, Vector2 uv)
    {
        if (_curSolid) _solid.Add(p);
        st.Pos.Add(p);
        st.Nrm.Add(n);
        st.Col.Add(Col);
        st.Uv.Add(uv);
    }

    /// <summary>Triangle déjà transformé, normales par sommet ; l'ordre est corrigé selon la normale moyenne.</summary>
    private void TriRaw(Surf st,Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector2 ua, Vector2 ub, Vector2 uc)
    {
        var face = (b - a).Cross(c - a);
        if (face.Dot(na + nb + nc) > 0)
        {
            // Inverser pour obtenir le sens horaire vu de l'extérieur.
            V(st, a, na, ua); V(st, c, nc, uc); V(st, b, nb, ub);
        }
        else
        {
            V(st, a, na, ua); V(st, b, nb, ub); V(st, c, nc, uc);
        }
    }

    private Vector3 P(Vector3 p) => Xf * p;
    private Vector3 N(Vector3 n) => (Xf.Basis * n).Normalized();

    /// <summary>Triangle à facette plate, orienté pour que sa face avant s'éloigne de <paramref name="inside"/>.</summary>
    public void Tri(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
    {
        var n = (b - a).Cross(c - a).Normalized();
        if (n.Dot((a + b + c) / 3 - inside) < 0) n = -n;
        var wn = N(n);
        TriRaw(S(m), P(a), P(b), P(c), wn, wn, wn, Vector2.Zero, Vector2.Zero, Vector2.Zero);
    }

    /// <summary>Triangle avec une couleur par sommet (dégradés de feuillage).</summary>
    public void Tri(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 inside, Color ca, Color cb, Color cc)
    {
        var st = S(m);
        var n = (b - a).Cross(c - a).Normalized();
        if (n.Dot((a + b + c) / 3 - inside) < 0) n = -n;
        var wn = N(n);
        var pa = P(a); var pb = P(b); var pc = P(c);
        var keep = Col;
        // Même règle d'ordre que TriRaw, en conservant la couleur de chaque sommet.
        var flip = (pb - pa).Cross(pc - pa).Dot(wn) > 0;
        Col = ca; V(st, pa, wn, Vector2.Zero);
        if (flip) { Col = cc; V(st, pc, wn, Vector2.Zero); Col = cb; V(st, pb, wn, Vector2.Zero); }
        else { Col = cb; V(st, pb, wn, Vector2.Zero); Col = cc; V(st, pc, wn, Vector2.Zero); }
        Col = keep;
    }

    /// <summary>Triangle à normales lissées et couleurs par sommet (formes organiques).</summary>
    public void TriSmooth(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Color ca, Color cb, Color cc)
    {
        var st = S(m);
        var pa = P(a); var pb = P(b); var pc = P(c);
        var wa = N(na); var wb = N(nb); var wc = N(nc);
        var keep = Col;
        var flip = (pb - pa).Cross(pc - pa).Dot(wa + wb + wc) > 0;
        Col = ca; V(st, pa, wa, Vector2.Zero);
        if (flip) { Col = cc; V(st, pc, wc, Vector2.Zero); Col = cb; V(st, pb, wb, Vector2.Zero); }
        else { Col = cb; V(st, pb, wb, Vector2.Zero); Col = cc; V(st, pc, wc, Vector2.Zero); }
        Col = keep;
    }

    /// <summary>Quad plan (coins dans l'ordre antihoraire vus de l'extérieur), normale déduite.</summary>
    public void Quad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d) =>
        Quad(m, a, b, c, d, Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero);

    public void Quad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
    {
        var st = S(m);
        var n = N((b - a).Cross(c - a).Normalized());
        if (n == Vector3.Zero || !n.IsFinite()) n = N((c - a).Cross(d - a).Normalized());
        var pa = P(a); var pb = P(b); var pc = P(c); var pd = P(d);
        TriRaw(st, pa, pb, pc, n, n, n, ua, ub, uc);
        TriRaw(st, pa, pc, pd, n, n, n, ua, uc, ud);
    }

    /// <summary>
    /// Quad plan à couleur par sommet (dégradés d'encrassement sur les façades), tourné vers <paramref name="outward"/>.
    /// </summary>
    public void QuadC(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud,
        Color ca, Color cb, Color cc, Color cd, Vector3 outward)
    {
        var st = S(m);
        var n = (b - a).Cross(c - a).Normalized();
        if (n.Dot(outward) < 0) n = -n;
        var wn = N(n);
        var pa = P(a); var pb = P(b); var pc = P(c); var pd = P(d);
        var keep = Col;
        void T(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 u0, Vector2 u1, Vector2 u2, Color c0, Color c1, Color c2)
        {
            Col = c0; V(st, p0, wn, u0);
            if ((p1 - p0).Cross(p2 - p0).Dot(wn) > 0) { Col = c2; V(st, p2, wn, u2); Col = c1; V(st, p1, wn, u1); }
            else { Col = c1; V(st, p1, wn, u1); Col = c2; V(st, p2, wn, u2); }
        }
        T(pa, pb, pc, ua, ub, uc, ca, cb, cc);
        T(pa, pc, pd, ua, uc, ud, ca, cc, cd);
        Col = keep;
    }

    /// <summary>Boîte centrée en <paramref name="c"/>, éventuellement tournée (base locale).</summary>
    public void Box(Material m, Vector3 c, Vector3 size, Basis? rot = null, bool bottom = true, bool top = true)
    {
        var b = rot ?? Basis.Identity;
        var e = size / 2;
        var ax = b.X * e.X;
        var ay = b.Y * e.Y;
        var az = b.Z * e.Z;
        // (normale, tangente u, tangente v) avec u × v = normale.
        void F(Vector3 n, Vector3 u, Vector3 v)
        {
            var o = c + n;
            Quad(m, o - u - v, o + u - v, o + u + v, o - u + v);
        }
        F(az, ax, ay);
        F(-az, -ax, ay);
        F(ax, -az, ay);
        F(-ax, az, ay);
        if (top) F(ay, ax, -az);
        if (bottom) F(-ay, ax, az);
    }

    /// <summary>Boîte définie par ses coins min/max (non tournée).</summary>
    public void BoxMinMax(Material m, Vector3 min, Vector3 max, bool bottom = true, bool top = true) =>
        Box(m, (min + max) / 2, max - min, null, bottom, top);

    /// <summary>
    /// Tronc de pyramide : base rectangulaire (bx, bz) à y0, sommet (tx, tz) à y1 décalé de <paramref name="topOffset"/>.
    /// </summary>
    public void Frustum(Material m, Vector3 baseCenter, Vector2 bottomSize, Vector2 topSize, float height, Vector2 topOffset = default)
    {
        var bx = bottomSize.X / 2; var bz = bottomSize.Y / 2;
        var tx = topSize.X / 2; var tz = topSize.Y / 2;
        var c = baseCenter;
        var t = c + new Vector3(topOffset.X, height, topOffset.Y);
        Vector3 B(float sx, float sz) => c + new Vector3(sx * bx, 0, sz * bz);
        Vector3 T(float sx, float sz) => t + new Vector3(sx * tx, 0, sz * tz);
        Quad(m, B(-1, 1), B(1, 1), T(1, 1), T(-1, 1));
        Quad(m, B(1, -1), B(-1, -1), T(-1, -1), T(1, -1));
        Quad(m, B(1, 1), B(1, -1), T(1, -1), T(1, 1));
        Quad(m, B(-1, -1), B(-1, 1), T(-1, 1), T(-1, -1));
        Quad(m, T(-1, 1), T(1, 1), T(1, -1), T(-1, -1));
        Quad(m, B(-1, -1), B(1, -1), B(1, 1), B(-1, 1));
    }

    /// <summary>Cylindre (ou cône) vertical, base en <paramref name="b"/>, normales lissées.</summary>
    public void Cylinder(Material m, Vector3 b, float rBottom, float rTop, float h, int seg = 10, bool caps = true, Basis? rot = null)
    {
        var st = S(m);
        var basis = rot ?? Basis.Identity;
        var up = basis.Y;
        var slope = (rBottom - rTop) / Mathf.Max(h, 0.001f);
        for (var i = 0; i < seg; i++)
        {
            var a0 = i * Mathf.Tau / seg;
            var a1 = (i + 1) * Mathf.Tau / seg;
            var d0 = basis * new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
            var d1 = basis * new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            var p0 = P(b + d0 * rBottom); var p1 = P(b + d1 * rBottom);
            var q0 = P(b + up * h + d0 * rTop); var q1 = P(b + up * h + d1 * rTop);
            var n0 = N((d0 + up * slope).Normalized()); var n1 = N((d1 + up * slope).Normalized());
            if (rTop > 0.0001f)
            {
                TriRaw(st, p0, p1, q1, n0, n1, n1, Vector2.Zero, Vector2.Zero, Vector2.Zero);
                TriRaw(st, p0, q1, q0, n0, n1, n0, Vector2.Zero, Vector2.Zero, Vector2.Zero);
            }
            else
            {
                TriRaw(st, p0, p1, q0, n0, n1, (n0 + n1).Normalized(), Vector2.Zero, Vector2.Zero, Vector2.Zero);
            }
            if (!caps) continue;
            var nu = N(up);
            var cb = P(b);
            var ct = P(b + up * h);
            TriRaw(st, cb, p1, p0, -nu, -nu, -nu, Vector2.Zero, Vector2.Zero, Vector2.Zero);
            if (rTop > 0.0001f) TriRaw(st, ct, q0, q1, nu, nu, nu, Vector2.Zero, Vector2.Zero, Vector2.Zero);
        }
    }

    /// <summary>Cylindre entre deux points quelconques (tubes, rambardes, mâts inclinés).</summary>
    public void Tube(Material m, Vector3 from, Vector3 to, float r, int seg = 6, bool caps = false)
    {
        var dir = to - from;
        var len = dir.Length();
        if (len < 0.0001f) return;
        var y = dir / len;
        var x = Mathf.Abs(y.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up.Cross(y).Normalized();
        var z = x.Cross(y).Normalized();
        Cylinder(m, from, r, r, len, seg, caps, new Basis(x, y, z));
    }

    /// <summary>Anneau de 4 boîtes autour d'un rectangle (acrotère, corniche, socle, bordure).</summary>
    public void Ring(Material m, float hx, float hz, float y0, float y1, float thick, float outset, float bevel = 0f)
    {
        if (bevel > 0.001f)
        {
            RingBevel(m, hx + outset, hz + outset, y0, y1, thick, bevel + outset * 0.586f);
            return;
        }
        var ox = hx + outset;
        var oz = hz + outset;
        var ix = ox - thick;
        var iz = oz - thick;
        BoxMinMax(m, new Vector3(-ox, y0, iz), new Vector3(ox, y1, oz));
        BoxMinMax(m, new Vector3(-ox, y0, -oz), new Vector3(ox, y1, -iz));
        BoxMinMax(m, new Vector3(ix, y0, -iz), new Vector3(ox, y1, iz));
        BoxMinMax(m, new Vector3(-ox, y0, -iz), new Vector3(-ix, y1, iz));
    }

    /// <summary>Contour octogonal (rectangle ox × oz aux coins chanfreinés de <paramref name="b"/>), sens antihoraire vu du dessus.</summary>
    public static Vector2[] Octagon(float ox, float oz, float b) => new[]
    {
        new Vector2(-ox + b, oz), new Vector2(ox - b, oz), new Vector2(ox, oz - b), new Vector2(ox, -oz + b),
        new Vector2(ox - b, -oz), new Vector2(-ox + b, -oz), new Vector2(-ox, -oz + b), new Vector2(-ox, oz - b),
    };

    /// <summary>Anneau aux angles chanfreinés (acrotère, corniche, socle) : faces extérieures, intérieures, dessus et dessous.</summary>
    private void RingBevel(Material m, float ox, float oz, float y0, float y1, float thick, float b)
    {
        var o = Octagon(ox, oz, b);
        var i = Octagon(ox - thick, oz - thick, Mathf.Max(0.01f, b - thick * 0.586f));
        Vector3 At(Vector2 p, float y) => new(p.X, y, p.Y);
        for (var k = 0; k < 8; k++)
        {
            var k1 = (k + 1) % 8;
            var oa = o[k]; var ob = o[k1]; var ia = i[k]; var ib = i[k1];
            // Extérieur (normale opposée au centre), intérieur (vers le centre).
            var on = new Vector3((oa + ob).X, 0, (oa + ob).Y);
            OrientedQuad(m, At(oa, y0), At(ob, y0), At(ob, y1), At(oa, y1), on);
            OrientedQuad(m, At(ia, y0), At(ib, y0), At(ib, y1), At(ia, y1), -new Vector3((ia + ib).X, 0, (ia + ib).Y));
            OrientedQuad(m, At(oa, y1), At(ob, y1), At(ib, y1), At(ia, y1), Vector3.Up);
            OrientedQuad(m, At(oa, y0), At(ob, y0), At(ib, y0), At(ia, y0), Vector3.Down);
        }
    }

    /// <summary>Quad plan dont la face avant regarde vers <paramref name="outward"/>.</summary>
    public void OrientedQuad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
    {
        var n = (b - a).Cross(c - a);
        if (n.LengthSquared() < 1e-12f) n = (c - a).Cross(d - a);
        if (n.Dot(outward) >= 0) Quad(m, a, b, c, d);
        else Quad(m, d, c, b, a);
    }

    /// <summary>Produit le maillage (ajoute les surfaces à <paramref name="into"/> si fourni).</summary>
    public ArrayMesh Commit(ArrayMesh into = null)
    {
        var mesh = into ?? new ArrayMesh();
        foreach (var m in _order)
        {
            var st = _st[m];
            var n = st.Pos.Count;
            if (n == 0) continue;
            // Tangentes par triangle (méthode des dérivées d'UV), repli perpendiculaire à la normale
            // pour les surfaces sans UV (matériaux triplanaires, qui recalculent leur repère).
            var tan = new float[n * 4];
            for (var i = 0; i + 2 < n; i += 3)
            {
                var e1 = st.Pos[i + 1] - st.Pos[i];
                var e2 = st.Pos[i + 2] - st.Pos[i];
                var d1 = st.Uv[i + 1] - st.Uv[i];
                var d2 = st.Uv[i + 2] - st.Uv[i];
                var r = d1.X * d2.Y - d2.X * d1.Y;
                Vector3 t, b;
                if (Mathf.Abs(r) > 1e-8f)
                {
                    t = (e1 * d2.Y - e2 * d1.Y) / r;
                    b = (e2 * d1.X - e1 * d2.X) / r;
                }
                else
                {
                    var nn = st.Nrm[i];
                    t = Mathf.Abs(nn.Y) > 0.9f ? Vector3.Right : Vector3.Up.Cross(nn);
                    b = nn.Cross(t);
                }
                for (var k = 0; k < 3; k++)
                {
                    var nv = st.Nrm[i + k];
                    var tv = (t - nv * nv.Dot(t)).Normalized();
                    if (!tv.IsFinite() || tv == Vector3.Zero) tv = Mathf.Abs(nv.Y) > 0.9f ? Vector3.Right : Vector3.Up.Cross(nv).Normalized();
                    var j = (i + k) * 4;
                    tan[j] = tv.X; tan[j + 1] = tv.Y; tan[j + 2] = tv.Z;
                    tan[j + 3] = nv.Cross(tv).Dot(b) < 0 ? -1f : 1f;
                }
            }
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = st.Pos.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = st.Nrm.ToArray();
            arrays[(int)Mesh.ArrayType.Tangent] = tan;
            arrays[(int)Mesh.ArrayType.Color] = st.Col.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = st.Uv.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, m);
        }
        if (Record && _solid.Count > 0) SolidFaces.AddOrUpdate(mesh, _solid.ToArray());
        return mesh;
    }
}
