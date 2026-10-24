using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Atelier de géométrie monumentale de la capitale : surfaces de révolution (dômes, tambours, fûts), colonnes à base
/// et chapiteau, ouvertures en arc (arc de triomphe, ponts, viaduc, portails), frontons, baies en creux, balustrades.
/// Tout passe par <see cref="MeshKit"/> (triangles solides enregistrés si le kit l'est).
/// </summary>
public static partial class CapitalBuilder
{
    /// <summary>
    /// Surface de révolution autour de l'axe vertical passant par <paramref name="c"/> : profil (rayon, hauteur) de bas en
    /// haut, normales lissées le long du profil (dômes, tambours, lanternons, vasques).
    /// </summary>
    private static void Lathe(MeshKit k, Material m, Vector3 c, IList<Vector2> profile, int seg, Color col, float a0 = 0f, float a1 = Mathf.Tau)
    {
        var n = profile.Count;
        var nrm = new Vector2[n];
        for (var i = 0; i < n; i++)
        {
            var p0 = profile[Mathf.Max(0, i - 1)];
            var p1 = profile[Mathf.Min(n - 1, i + 1)];
            var t = (p1 - p0).Normalized();
            nrm[i] = new Vector2(t.Y, -t.X); // tangente (dr, dy) tournée : vers l'extérieur
            if (nrm[i].X < 0 && profile[i].X > 0.01f) nrm[i] = -nrm[i];
        }
        for (var s = 0; s < seg; s++)
        {
            var b0 = a0 + (a1 - a0) * s / seg;
            var b1 = a0 + (a1 - a0) * (s + 1) / seg;
            var d0 = new Vector3(Mathf.Cos(b0), 0, Mathf.Sin(b0));
            var d1 = new Vector3(Mathf.Cos(b1), 0, Mathf.Sin(b1));
            for (var i = 0; i + 1 < n; i++)
            {
                var pa = profile[i];
                var pb = profile[i + 1];
                var v00 = c + d0 * pa.X + Vector3.Up * pa.Y;
                var v10 = c + d1 * pa.X + Vector3.Up * pa.Y;
                var v01 = c + d0 * pb.X + Vector3.Up * pb.Y;
                var v11 = c + d1 * pb.X + Vector3.Up * pb.Y;
                var n00 = (d0 * nrm[i].X + Vector3.Up * nrm[i].Y).Normalized();
                var n10 = (d1 * nrm[i].X + Vector3.Up * nrm[i].Y).Normalized();
                var n01 = (d0 * nrm[i + 1].X + Vector3.Up * nrm[i + 1].Y).Normalized();
                var n11 = (d1 * nrm[i + 1].X + Vector3.Up * nrm[i + 1].Y).Normalized();
                if (pa.X > 0.001f) k.TriSmooth(m, v00, v10, v11, n00, n10, n11, col, col, col);
                if (pb.X > 0.001f) k.TriSmooth(m, v00, v11, v01, n00, n11, n01, col, col, col);
            }
        }
    }

    /// <summary>Colonne : base moulurée, fût galbé (cannelures suggérées par 14 pans), chapiteau et abaque.</summary>
    private static void Column(MeshKit k, Material m, Vector3 p, float r, float h, Color col)
    {
        k.Col = col;
        k.Box(m, p + Vector3.Up * 0.2f * r, new Vector3(r * 2.7f, 0.4f * r, r * 2.7f));
        Lathe(k, m, p, new[] { new Vector2(r * 1.25f, 0.4f * r), new Vector2(r * 1.1f, 0.65f * r), new Vector2(r, 0.8f * r), new Vector2(r * 1.02f, h * 0.35f), new Vector2(r * 0.86f, h - 0.9f * r), new Vector2(r * 1.15f, h - 0.55f * r), new Vector2(r * 1.3f, h - 0.4f * r) }, 14, col);
        k.Box(m, p + Vector3.Up * (h - 0.2f * r), new Vector3(r * 2.8f, 0.4f * r, r * 2.8f));
    }

    /// <summary>
    /// Ouverture en arc (plein cintre ou surbaissé) dans un massif : remplit les tympans entre l'intrados et
    /// <paramref name="topY"/> sur les deux faces, et l'intrados (douelle) sur l'épaisseur. Repère : <paramref name="o"/> au
    /// centre de l'ouverture au niveau des naissances (y = 0 local), <paramref name="across"/> en travers de l'ouverture,
    /// <paramref name="through"/> dans l'épaisseur (de -depth/2 à +depth/2).
    /// </summary>
    private static void ArchFill(MeshKit k, Material m, Vector3 o, Vector3 across, Vector3 through, float half, float rise, float top, float depth, int seg, Color col)
    {
        k.Col = col;
        var f = through * (depth / 2);
        for (var i = 0; i < seg; i++)
        {
            var t0 = Mathf.Pi * i / seg;
            var t1 = Mathf.Pi * (i + 1) / seg;
            var a = o + across * (Mathf.Cos(t0) * half) + Vector3.Up * (Mathf.Sin(t0) * rise);
            var b = o + across * (Mathf.Cos(t1) * half) + Vector3.Up * (Mathf.Sin(t1) * rise);
            var at = a with { Y = o.Y + top };
            var bt = b with { Y = o.Y + top };
            // Tympans (deux faces).
            k.OrientedQuad(m, a + f, b + f, bt + f, at + f, through);
            k.OrientedQuad(m, a - f, b - f, bt - f, at - f, -through);
            // Douelle (normale vers le centre de l'arc).
            var mid = (a + b) / 2;
            var toward = (o - mid).Normalized();
            k.OrientedQuad(m, a + f, b + f, b - f, a - f, toward);
        }
    }

    /// <summary>Fronton triangulaire (deux rampants saillants, tympan en retrait) posé sur une corniche.</summary>
    private static void Pediment(MeshKit k, Material m, Vector3 baseCenter, Vector3 along, Vector3 outward, float halfW, float rise, float depth, Color col)
    {
        k.Col = col;
        var l = baseCenter - along * halfW;
        var r = baseCenter + along * halfW;
        var apex = baseCenter + Vector3.Up * rise;
        var back = -outward * depth;
        // Tympan (face avant), dos, rampants (dessus).
        k.Tri(m, l, r, apex, l + back);
        k.Tri(m, l + back, r + back, apex + back, l);
        k.OrientedQuad(m, l, apex, apex + back, l + back, Vector3.Up - along);
        k.OrientedQuad(m, r, apex, apex + back, r + back, Vector3.Up + along);
        // Corniches rampantes saillantes.
        var lip = outward * 0.35f;
        var up = Vector3.Up * 0.45f;
        k.OrientedQuad(m, l + lip + up, apex + lip + up, apex + lip, l + lip, outward);
        k.OrientedQuad(m, r + lip + up, apex + lip + up, apex + lip, r + lip, outward);
        k.OrientedQuad(m, l + lip + up, apex + lip + up, apex + up, l + up, Vector3.Up - along);
        k.OrientedQuad(m, r + lip + up, apex + lip + up, apex + up, r + up, Vector3.Up + along);
    }

    /// <summary>Flèche des arcs de <see cref="WindowBay"/> en demi-ouvertures : 1 plein cintre, 1,8 arc brisé (cathédrale).</summary>
    private static float _pointy = 1f;

    /// <summary>
    /// Baie en creux sur un mur (fenêtre ou niche) : fond sombre en retrait, tableaux, appui et linteau en saillie ;
    /// <paramref name="arched"/> : couvrement en arc. Repère : <paramref name="c"/> au centre bas de la baie, sur le nu du mur.
    /// </summary>
    private static void WindowBay(MeshKit k, Material stone, Material dark, Vector3 c, Vector3 along, Vector3 outward, float w, float h, Color stoneCol, Color glass, bool arched = false, float recess = 0.45f)
    {
        // Le mur reste plein : la vitre sombre est posée sur son nu, l'encadrement saillant (piédroits, arc ou linteau,
        // appui) donne la profondeur de l'embrasure.
        var hw = w / 2;
        var d = 0.18f + recess * 0.35f;
        var jw = Mathf.Clamp(w * 0.12f, 0.2f, 0.7f);
        var rot = new Basis(Vector3.Up, Mathf.Atan2(along.X, along.Z));
        var ph = hw * _pointy;            // flèche de l'arc (plein cintre, ou tiers-point gothique)
        var hTop = arched ? h - ph : h;
        var pane = c + outward * 0.03f;
        k.Col = glass;
        k.OrientedQuad(dark, pane - along * hw, pane + along * hw, pane + along * hw + Vector3.Up * hTop, pane - along * hw + Vector3.Up * hTop, outward);
        const int seg = 8;
        // Point de l'arc pour t ∈ [0, π] (rayon horizontal r) : plein cintre, ou arc brisé (pointe au sommet).
        Vector3 Arc(float t, float r)
        {
            var u = Mathf.Cos(t);
            var y = _pointy > 1.05f ? ph * (r / hw) * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(u)), 0.6f) : Mathf.Sin(t) * r;
            return along * (u * r) + Vector3.Up * y;
        }
        if (arched)
            for (var i = 0; i < seg; i++)
            {
                var t0 = Mathf.Pi * i / seg;
                var t1 = Mathf.Pi * (i + 1) / seg;
                var ctr = pane + Vector3.Up * hTop;
                k.Tri(dark, ctr + Arc(t0, hw), ctr + Arc(t1, hw), ctr, ctr - outward);
            }
        k.Col = stoneCol * 0.97f;
        foreach (var s in new[] { -1f, 1f })
            k.Box(stone, c + along * (s * (hw + jw / 2)) + outward * (d / 2) + Vector3.Up * (hTop / 2), new Vector3(d, hTop, jw), rot);
        if (arched)
        {
            var rr = hw + jw / 2;
            var ctr = c + outward * (d / 2) + Vector3.Up * hTop;
            for (var i = 0; i < seg; i++)
            {
                var t0 = Mathf.Pi * i / seg;
                var t1 = Mathf.Pi * (i + 1) / seg;
                k.Tube(stone, ctr + Arc(t0, rr), ctr + Arc(t1, rr), jw * 0.55f, 5, true);
            }
            k.Box(stone, ctr + Arc(Mathf.Pi / 2, rr) + Vector3.Up * (jw * 0.1f), new Vector3(d + 0.06f, jw * 1.3f, jw * 0.9f), rot);
        }
        else
            k.Box(stone, c + outward * (d / 2) + Vector3.Up * (h + jw / 2), new Vector3(d + 0.04f, jw, w + jw * 2.4f), rot);
        k.Col = stoneCol * 1.05f;
        k.Box(stone, c + outward * (d / 2 + 0.05f) - Vector3.Up * 0.07f, new Vector3(d + 0.12f, 0.14f, w + jw * 2f + 0.2f), rot);
    }

    /// <summary>Balustrade de pierre (lisse, socle, balustres) entre deux points, solide.</summary>
    private static void Balustrade(MeshKit k, Material m, Vector3 a, Vector3 b, float h, Color col)
    {
        var len = a.DistanceTo(b);
        if (len < 0.3f) return;
        var along = (b - a) / len;
        var rot = new Basis(Vector3.Up, Mathf.Atan2(along.X, along.Z));
        var mid = (a + b) / 2;
        k.Col = col;
        k.Box(m, mid + Vector3.Up * 0.12f, new Vector3(0.45f, 0.24f, len), rot);
        k.Box(m, mid + Vector3.Up * (h - 0.1f), new Vector3(0.5f, 0.2f, len + 0.1f), rot);
        var n = Mathf.Max(2, (int)(len / 0.32f));
        k.Col = col * 0.95f;
        for (var i = 0; i < n; i++)
        {
            var p = a + along * (len * (i + 0.5f) / n);
            k.Cylinder(m, p + Vector3.Up * 0.24f, 0.08f, 0.11f, h - 0.44f, 5, false);
        }
    }

    /// <summary>Tore (moulure circulaire, remplage) autour de <paramref name="axis"/>.</summary>
    private static void Torus(MeshKit k, Material m, Vector3 c, Vector3 axis, float bigR, float r, int seg)
    {
        axis = axis.Normalized();
        var u = Mathf.Abs(axis.Y) > 0.9f ? Vector3.Right : axis.Cross(Vector3.Up).Normalized();
        var v = axis.Cross(u).Normalized();
        const int rs = 6;
        for (var i = 0; i < seg; i++)
        {
            var a0 = Mathf.Tau * i / seg;
            var a1 = Mathf.Tau * (i + 1) / seg;
            var d0 = u * Mathf.Cos(a0) + v * Mathf.Sin(a0);
            var d1 = u * Mathf.Cos(a1) + v * Mathf.Sin(a1);
            for (var j = 0; j < rs; j++)
            {
                var b0 = Mathf.Tau * j / rs;
                var b1 = Mathf.Tau * (j + 1) / rs;
                Vector3 N(Vector3 d, float b) => d * Mathf.Cos(b) + axis * Mathf.Sin(b);
                var n00 = N(d0, b0); var n01 = N(d0, b1); var n10 = N(d1, b0); var n11 = N(d1, b1);
                var p00 = c + d0 * bigR + n00 * r; var p01 = c + d0 * bigR + n01 * r;
                var p10 = c + d1 * bigR + n10 * r; var p11 = c + d1 * bigR + n11 * r;
                k.TriSmooth(m, p00, p10, p11, n00, n10, n11, k.Col, k.Col, k.Col);
                k.TriSmooth(m, p00, p11, p01, n00, n11, n01, k.Col, k.Col, k.Col);
            }
        }
    }

    /// <summary>Volume creux : quatre faces extérieures et dessus (les baies creusées dedans restent visibles).</summary>
    private static void Shell(MeshKit k, Material m, Vector3 min, Vector3 max, bool top = true)
    {
        Vector3 P(float x, float y, float z) => new(x, y, z);
        k.OrientedQuad(m, P(min.X, min.Y, max.Z), P(max.X, min.Y, max.Z), P(max.X, max.Y, max.Z), P(min.X, max.Y, max.Z), Vector3.Back);
        k.OrientedQuad(m, P(min.X, min.Y, min.Z), P(max.X, min.Y, min.Z), P(max.X, max.Y, min.Z), P(min.X, max.Y, min.Z), Vector3.Forward);
        k.OrientedQuad(m, P(max.X, min.Y, min.Z), P(max.X, min.Y, max.Z), P(max.X, max.Y, max.Z), P(max.X, max.Y, min.Z), Vector3.Right);
        k.OrientedQuad(m, P(min.X, min.Y, min.Z), P(min.X, min.Y, max.Z), P(min.X, max.Y, max.Z), P(min.X, max.Y, min.Z), Vector3.Left);
        if (top) k.OrientedQuad(m, P(min.X, max.Y, min.Z), P(max.X, max.Y, min.Z), P(max.X, max.Y, max.Z), P(min.X, max.Y, max.Z), Vector3.Up);
    }

    /// <summary>Statue de bronze ou dorée stylisée (figure drapée debout sur une plinthe), sans bras détaillés.</summary>
    private static void Statue(MeshKit k, Material m, Vector3 p, float h, Color col, float yaw, bool winged = false)
    {
        k.Col = col;
        var rot = new Basis(Vector3.Up, yaw);
        Lathe(k, m, p, new[] { new Vector2(h * 0.16f, 0), new Vector2(h * 0.14f, h * 0.35f), new Vector2(h * 0.11f, h * 0.62f), new Vector2(h * 0.07f, h * 0.78f), new Vector2(h * 0.05f, h * 0.8f) }, 8, col);
        k.Box(m, p + rot * new Vector3(0, h * 0.72f, 0.02f * h), new Vector3(h * 0.3f, h * 0.1f, h * 0.12f), rot);
        k.Cylinder(m, p + Vector3.Up * h * 0.8f, h * 0.05f, h * 0.06f, h * 0.12f, 8);
        // Bras levé tenant une couronne ou une torche.
        k.Tube(m, p + rot * new Vector3(h * 0.12f, h * 0.74f, 0), p + rot * new Vector3(h * 0.2f, h * 1.05f, h * 0.05f), h * 0.03f, 5);
        if (!winged) return;
        foreach (var sx in new[] { -1f, 1f })
        {
            var root = p + rot * new Vector3(sx * h * 0.05f, h * 0.72f, -h * 0.06f);
            k.Tri(m, root, root + rot * new Vector3(sx * h * 0.45f, h * 0.35f, -h * 0.2f), root + rot * new Vector3(sx * h * 0.2f, -h * 0.3f, -h * 0.15f), root + rot * new Vector3(0, 0, h));
        }
    }
}
