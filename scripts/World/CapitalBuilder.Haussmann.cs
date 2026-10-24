using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Îlots haussmanniens : immeubles de pierre de taille alignés sur la rue autour d'une cour (ou en une seule rangée
/// pour les îlots minces), angles coupés aux carrefours. Chaque immeuble : rez-de-chaussée à bossages et devantures,
/// cinq ou six étages à portes-fenêtres, balcons filants de fonte au deuxième et au dernier étage, corniche saillante,
/// comble mansardé de zinc ou d'ardoise à lucarnes, souches de cheminées. Une partie des combles a brûlé (chevrons
/// calcinés à nu), certains lots ne sont plus que des carcasses éventrées ou des cratères.
/// Collision : volume plein convexe jusqu'à la corniche (angles coupés compris), forme exacte de ce qui dépasse
/// (corniches, balcons, comble, lucarnes, cheminées) ; destructibles comme les immeubles de la ville.
/// </summary>
public static partial class CapitalBuilder
{
    /// <summary>Profondeur des immeubles depuis la rue (m), largeur du trottoir, pan coupé des angles.</summary>
    private const float HDepth = 14f, HSidewalk = 3.6f, HChamfer = 4.5f;

    /// <summary>Paramètres d'un immeuble haussmannien (repère local : base au centre, y = 0 au niveau du trottoir).</summary>
    private struct HStyle
    {
        public float W, D;
        public int Floors;      // étages courants au-dessus du rez-de-chaussée
        public int Chamfers;    // pans coupés : bit 0 (-x,-z), 1 (+x,-z), 2 (+x,+z), 3 (-x,+z)
        public int Street;      // façades sur rue : 1 = +Z, 2 = -Z, 4 = +X, 8 = -X (les pans coupés le sont toujours)
        public int Seed;
        public bool Slate;
        public int BurntEdge;   // -1 : comble intact ; sinon côté dont le comble a brûlé
        public Color Tint;
    }

    private static void BuildHaussmann(Ctx c)
    {
        for (var i = 0; i < Blocks.Length; i++) FillBlock(c, Blocks[i], i);
    }

    /// <summary>Découpe un îlot en lots (angles, côtés) et y pose immeubles, carcasses ou terrains bombardés.</summary>
    private static void FillBlock(Ctx c, Rect2 block, int bi)
    {
        var lot = block.Grow(-HSidewalk);
        var w = lot.Size.X;
        var d = lot.Size.Y;
        var x0 = lot.Position.X; var x1 = lot.End.X;
        var z0 = lot.Position.Y; var z1 = lot.End.Y;
        if (Mathf.Min(w, d) < HDepth * 2f + 6f)
        {
            // Îlot mince : une seule rangée d'immeubles traversants (rue des deux côtés).
            var alongX = w >= d;
            var len = alongX ? w : d;
            var n = Mathf.Max(1, Mathf.RoundToInt(len / 17f));
            var part = len / n;
            for (var i = 0; i < n; i++)
            {
                var a0 = i * part;
                var r = alongX ? new Rect2(x0 + a0, z0, part, d) : new Rect2(x0, z0 + a0, w, part);
                var street = alongX ? 1 | 2 : 4 | 8;
                var ch = 0;
                if (i == 0) { street |= alongX ? 8 : 2; ch |= alongX ? (1 << 0) | (1 << 3) : (1 << 0) | (1 << 1); }
                if (i == n - 1) { street |= alongX ? 4 : 1; ch |= alongX ? (1 << 1) | (1 << 2) : (1 << 2) | (1 << 3); }
                Lot(c, r, street, ch, bi * 100 + i);
            }
            return;
        }
        // Îlot à cour : quatre angles à pan coupé, côtés découpés en lots de 12 à 21 m.
        var D = HDepth;
        Lot(c, new Rect2(x0, z0, D, D), 2 | 8, 1 << 0, bi * 100 + 1);
        Lot(c, new Rect2(x1 - D, z0, D, D), 2 | 4, 1 << 1, bi * 100 + 2);
        Lot(c, new Rect2(x1 - D, z1 - D, D, D), 1 | 4, 1 << 2, bi * 100 + 3);
        Lot(c, new Rect2(x0, z1 - D, D, D), 1 | 8, 1 << 3, bi * 100 + 4);
        void Side(float a0, float a1, bool alongX, float fixedLo, int street, int seed)
        {
            var len = a1 - a0;
            if (len < 6f) return;
            var n = Mathf.Max(1, Mathf.RoundToInt(len / 16f));
            var part = len / n;
            for (var i = 0; i < n; i++)
            {
                var s = a0 + i * part;
                var r = alongX ? new Rect2(s, fixedLo, part, D) : new Rect2(fixedLo, s, D, part);
                Lot(c, r, street, 0, seed + i);
            }
        }
        Side(x0 + D, x1 - D, true, z0, 2, bi * 100 + 10);
        Side(x0 + D, x1 - D, true, z1 - D, 1, bi * 100 + 30);
        Side(z0 + D, z1 - D, false, x0, 8, bi * 100 + 50);
        Side(z0 + D, z1 - D, false, x1 - D, 4, bi * 100 + 70);
    }

    /// <summary>Un lot : immeuble (le plus souvent), carcasse éventrée ou terrain bombardé.</summary>
    private static void Lot(Ctx c, Rect2 r, int street, int chamfers, int seed)
    {
        var center = new Vector3(r.GetCenter().X, Walk, r.GetCenter().Y);
        var fate = RuinKit.Hash(center.X, center.Z, 401);
        if (fate < 0.07f)
        {
            RuinLot(c, r, seed, false);
            return;
        }
        if (fate < 0.1f)
        {
            RuinLot(c, r, seed, true);
            return;
        }
        var f = RuinKit.Hash(center.X, center.Z, 402);
        var st = new HStyle
        {
            W = r.Size.X - 0.1f, D = r.Size.Y - 0.1f,
            Floors = f < 0.12f ? 4 : f < 0.7f ? 5 : 6,
            Chamfers = chamfers, Street = street, Seed = seed,
            Slate = RuinKit.Hash(center.X, center.Z, 403) < 0.3f,
            BurntEdge = RuinKit.Hash(center.X, center.Z, 404) < 0.22f ? (int)(RuinKit.Hash(center.X, center.Z, 405) * 4) : -1,
            Tint = StoneTint(center),
        };
        var mesh = HaussmannMesh(st, c.Night, out var hull, out var hc);
        var b = AddBuilding(c, center, new Vector3(st.W, hc, st.D), mesh, Mats.HaussmannFacade(c.Night), 1.1f, hull);
        if (st.BurntEdge >= 0 && RuinKit.Hash(center.X, center.Z, 406) < 0.5f)
            c.Ruins.Smolder(center + Vector3.Up * (hc + 2.5f), 1.6f);
        _ = b;
    }

    /// <summary>Teinte de la pierre d'un immeuble (calcaire plus ou moins noirci), sans tirage aléatoire.</summary>
    private static Color StoneTint(Vector3 p)
    {
        var a = RuinKit.Hash(p.X, p.Z, 411);
        var b = RuinKit.Hash(p.X, p.Z, 412);
        var k = 0.8f + 0.24f * a;
        var t = (b - 0.5f) * 0.06f;
        return new Color(k * (1 + t), k, k * (1 - t * 1.5f));
    }

    /// <summary>Lot détruit : carcasse de murs éventrée (non destructible) avec gravats, ou cratère et buttes.</summary>
    private static void RuinLot(Ctx c, Rect2 r, int seed, bool crater)
    {
        var rk = c.Ruins;
        var center = new Vector3(r.GetCenter().X, Walk, r.GetCenter().Y);
        var wall = new Color(0.6f, 0.56f, 0.48f);
        if (!crater)
        {
            var boxes = new List<(Transform3D, Vector3)>();
            var h = Mats.ShopStoryH + Mats.StoryH * (3 + (int)(RuinKit.Hash(center.X, center.Z, 421) * 3));
            Props.RuinedShell(rk.Kit(RuinKit.Layer.Solid, center), center, new Vector2(r.Size.X - 0.6f, r.Size.Y - 0.6f), h, seed, wall, boxes);
            foreach (var (xf, s) in boxes) rk.Box(xf, s);
            FlowBlock(c, center, new Vector3(r.Size.X, h, r.Size.Y));
        }
        var ps = new Vector3(r.Size.X * 0.7f, crater ? 2.4f : 3.2f, r.Size.Y * 0.6f);
        rk.Convex(Props.RubblePile(rk.Kit(RuinKit.Layer.Solid, center), center, 0f, ps, seed, wall, false));
        rk.Decal(Mats.Decal.Dust, center, Mathf.Max(r.Size.X, r.Size.Y) * 1.6f, seed, Walk + 0.02f);
        if (crater) rk.Decal(Mats.Decal.Crater, center, Mathf.Min(r.Size.X, r.Size.Y) * 0.9f, seed * 0.3f, Walk + 0.03f);
        if (RuinKit.Hash(center.X, center.Z, 422) < 0.35f) rk.Fire(center + Vector3.Up * 1.5f, 1.4f, rk.Lights < 5);
        else rk.Smolder(center + Vector3.Up * 2.5f, 1.8f);
        c.KeepOut.Add(r);
        c.HotSpots.Add((center, Mathf.Max(r.Size.X, r.Size.Y) * 0.9f, 0.5f));
    }

    // ───────────────────────── Maillage ─────────────────────────

    /// <summary>Contour du lot (sens constant), angles coupés selon <see cref="HStyle.Chamfers"/>.</summary>
    private static List<Vector2> Footprint(float hx, float hz, int chamfers, float ch)
    {
        var corners = new[] { new Vector2(-hx, -hz), new Vector2(hx, -hz), new Vector2(hx, hz), new Vector2(-hx, hz) };
        var pts = new List<Vector2>();
        for (var i = 0; i < 4; i++)
        {
            var p = corners[i];
            if ((chamfers & (1 << i)) == 0) { pts.Add(p); continue; }
            var prev = corners[(i + 3) % 4];
            var next = corners[(i + 1) % 4];
            pts.Add(p + (prev - p).Normalized() * ch);
            pts.Add(p + (next - p).Normalized() * ch);
        }
        return pts;
    }

    /// <summary>Contour convexe rentré de <paramref name="d"/> (intersection des côtés décalés vers l'intérieur).</summary>
    private static List<Vector2> Inset(List<Vector2> poly, float d)
    {
        var n = poly.Count;
        var c = Vector2.Zero;
        foreach (var p in poly) c += p;
        c /= n;
        var lines = new (Vector2 p, Vector2 dir)[n];
        for (var i = 0; i < n; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            var dir = (b - a).Normalized();
            var nrm = new Vector2(-dir.Y, dir.X);
            if (nrm.Dot((a + b) / 2 - c) > 0) nrm = -nrm; // vers l'intérieur
            lines[i] = (a + nrm * d, dir);
        }
        var res = new List<Vector2>(n);
        for (var i = 0; i < n; i++)
        {
            var (p0, d0) = lines[(i + n - 1) % n];
            var (p1, d1) = lines[i];
            var den = d0.X * d1.Y - d0.Y * d1.X;
            if (Mathf.Abs(den) < 1e-5f) { res.Add(p1); continue; }
            var t = ((p1.X - p0.X) * d1.Y - (p1.Y - p0.Y) * d1.X) / den;
            res.Add(p0 + d0 * t);
        }
        return res;
    }

    /// <summary>Normale sortante (plan horizontal) du côté [a, b] d'un contour de centre <paramref name="c"/>.</summary>
    private static Vector3 OutNormal(Vector2 a, Vector2 b, Vector2 c)
    {
        var dir = (b - a).Normalized();
        var n = new Vector2(-dir.Y, dir.X);
        if (n.Dot((a + b) / 2 - c) < 0) n = -n;
        return new Vector3(n.X, 0, n.Y);
    }

    private static bool IsStreet(Vector3 n, int mask) =>
        Mathf.Abs(n.X) < 0.9f && Mathf.Abs(n.Z) < 0.9f // pan coupé
        || n.Z > 0.9f && (mask & 1) != 0 || n.Z < -0.9f && (mask & 2) != 0
        || n.X > 0.9f && (mask & 4) != 0 || n.X < -0.9f && (mask & 8) != 0;

    /// <summary>
    /// Maillage d'un immeuble haussmannien et son volume plein (prisme convexe jusqu'au-dessus de la corniche,
    /// <paramref name="hc"/> = hauteur de la corniche). Murs non solides (le volume plein les porte) ; corniches, balcons,
    /// comble, lucarnes et cheminées solides (forme exacte « Detail »).
    /// </summary>
    private static ArrayMesh HaussmannMesh(HStyle s, bool night, out Vector3[] hull, out float hc)
    {
        var k = new MeshKit { Record = true };
        var facade = Mats.HaussmannFacade(night);
        var shops = Mats.HaussmannShops(night);
        var stone = Mats.Ashlar();
        var roofMat = s.Slate ? Mats.Slate() : Mats.Zinc();
        var iron = Mats.IronRail();
        var charred = Mats.Charred();
        hc = Mats.ShopStoryH + Mats.StoryH * s.Floors;
        var H = hc;
        var poly = Footprint(s.W / 2, s.D / 2, s.Chamfers, HChamfer);
        var ctr = Vector2.Zero;
        var n = poly.Count;
        float Hh(int salt) => RuinKit.Hash(s.Seed * 1.37f, salt, 431);
        var tint = s.Tint;
        var dark = tint * new Color(0.7f, 0.69f, 0.67f);
        var roofCol = s.Slate ? new Color(0.36f, 0.38f, 0.42f) : Colors.White;

        // Volume plein : prisme du contour jusqu'au-dessus de la corniche.
        var hl = new List<Vector3>();
        foreach (var p in poly) { hl.Add(new Vector3(p.X, 0, p.Y)); hl.Add(new Vector3(p.X, H + 0.4f, p.Y)); }
        hull = hl.ToArray();

        for (var i = 0; i < n; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % n];
            var A = new Vector3(a.X, 0, a.Y);
            var B = new Vector3(b.X, 0, b.Y);
            var len = A.DistanceTo(B);
            var nrm = OutNormal(a, b, ctr);
            var street = IsStreet(nrm, s.Street);
            var along = (B - A) / len;
            var bays = Mathf.Max(1, Mathf.RoundToInt(len / Mats.BayW));
            var u0 = Mathf.Floor(Hh(i + 7) * 4f);
            // Murs (non solides : le volume plein les porte).
            k.Solid = false;
            var off = nrm * 0.005f;
            // Brèche d'obus sur une façade sur rue : une à deux travées sur un à trois étages, planchers à nu.
            var breach = street && bays >= 3 && s.Floors >= 4 && Hh(i + 200) < 0.32f;
            int hb0 = 0, hb1 = 0, hf0 = 0, hf1 = 0;
            if (breach)
            {
                hb0 = 1 + (int)(Hh(i + 201) * (bays - 2));
                hb1 = Mathf.Min(bays - 1, hb0 + 1 + (int)(Hh(i + 202) * 2f));
                hf0 = 1 + (int)(Hh(i + 203) * (s.Floors - 2));
                hf1 = Mathf.Min(s.Floors, hf0 + 1 + (int)(Hh(i + 204) * 2.5f));
            }
            if (street)
            {
                var gy = Mats.ShopStoryH;
                k.QuadC(shops, A + off, B + off, B + Vector3.Up * gy + off, A + Vector3.Up * gy + off,
                    new Vector2(u0, 1), new Vector2(u0 + bays, 1), new Vector2(u0 + bays, 0), new Vector2(u0, 0), dark, dark, tint, tint, nrm);
                // Étages : un seul quad, ou quatre morceaux autour de la brèche.
                void Piece(float t0, float t1, float y0, float y1)
                {
                    if (t1 - t0 < 0.01f || y1 - y0 < 0.01f) return;
                    var p0 = A + along * (t0 * len) + off;
                    var p1 = A + along * (t1 * len) + off;
                    var c0 = tint.Lerp(dark, (y0 - gy) / (H - gy));
                    var c1 = tint.Lerp(dark, (y1 - gy) / (H - gy));
                    k.QuadC(facade, p0 + Vector3.Up * y0, p1 + Vector3.Up * y0, p1 + Vector3.Up * y1, p0 + Vector3.Up * y1,
                        new Vector2(u0 + t0 * bays, (H - y0) / Mats.StoryH), new Vector2(u0 + t1 * bays, (H - y0) / Mats.StoryH),
                        new Vector2(u0 + t1 * bays, (H - y1) / Mats.StoryH), new Vector2(u0 + t0 * bays, (H - y1) / Mats.StoryH), c0, c0, c1, c1, nrm);
                }
                if (!breach) Piece(0f, 1f, gy, H);
                else
                {
                    var tb0 = hb0 / (float)bays;
                    var tb1 = hb1 / (float)bays;
                    var yb0 = gy + (hf0 - 1) * Mats.StoryH + 0.4f;
                    var yb1 = Mathf.Min(H - 0.6f, gy + hf1 * Mats.StoryH - 0.3f);
                    Piece(0f, tb0, gy, H);
                    Piece(tb1, 1f, gy, H);
                    Piece(tb0, tb1, gy, yb0);
                    Piece(tb0, tb1, yb1, H);
                    Breach(k, A + along * (tb0 * len), A + along * (tb1 * len), nrm, along, yb0, yb1, stone, charred, tint, s.Seed * 31 + i);
                }
            }
            else
            {
                var v1 = H / Mats.StoryH;
                k.QuadC(facade, A + off, B + off, B + Vector3.Up * H + off, A + Vector3.Up * H + off,
                    new Vector2(u0, v1), new Vector2(u0 + bays, v1), new Vector2(u0 + bays, 0), new Vector2(u0, 0), dark, dark, dark, dark, nrm);
            }
            k.Solid = true;
            // Corniche du rez-de-chaussée (sur rue) et corniche principale (tout le tour).
            k.Col = tint;
            var mid = (A + B) / 2;
            var rot = new Basis(Vector3.Up, Mathf.Atan2(along.X, along.Z));
            if (street) k.Box(stone, mid + Vector3.Up * (Mats.ShopStoryH - 0.05f) + nrm * 0.12f, new Vector3(0.36f, 0.42f, len + 0.2f), rot, false);
            k.Box(stone, mid + Vector3.Up * (H - 0.2f) + nrm * 0.15f, new Vector3(0.42f, 0.4f, len + 0.3f), rot, false);
            k.Col = tint * 1.04f;
            k.Box(stone, mid + Vector3.Up * (H + 0.2f) + nrm * 0.32f, new Vector3(0.8f, 0.4f, len + 0.62f), rot);
            if (!street) continue;
            // Balcons filants : deuxième étage et dernier étage.
            foreach (var fl in new[] { 1, s.Floors - 1 })
            {
                var y = Mats.ShopStoryH + Mats.StoryH * fl + 0.1f;
                // Balcon coupé par la brèche : deux tronçons, et un morceau arraché qui pend.
                var cut = breach && fl >= hf0 - 1 && fl < hf1;
                var spans = cut ? new[] { (0.3f, hb0 * len / bays - 0.4f), (hb1 * len / bays + 0.4f, len - 0.3f) } : new[] { (0.3f, len - 0.3f) };
                foreach (var (sa, sb) in spans)
                {
                    var la = sb - sa;
                    if (la < 0.8f) continue;
                    var sm = A + along * ((sa + sb) / 2f);
                    k.Col = tint;
                    k.Box(stone, sm + Vector3.Up * (y - 0.11f) + nrm * 0.45f, new Vector3(0.9f, 0.22f, la), rot);
                    // Consoles sous la dalle, une par travée.
                    for (var j = 0; j < bays; j++)
                    {
                        var t = (j + 0.5f) / bays * len;
                        if (t < sa || t > sb) continue;
                        var cp = A + along * t + nrm * 0.38f + Vector3.Up * (y - 0.45f);
                        k.Box(stone, cp, new Vector3(0.76f, 0.46f, 0.26f), rot, false);
                    }
                    // Garde-corps de fonte (ajouré, non solide) avec retours.
                    k.Solid = false;
                    var e0 = A + along * sa + nrm * 0.88f + Vector3.Up * y;
                    var e1 = A + along * sb + nrm * 0.88f + Vector3.Up * y;
                    k.Quad(iron, e0 + Vector3.Up * 1.05f, e1 + Vector3.Up * 1.05f, e1, e0,
                        new Vector2(0, 0), new Vector2(la, 0), new Vector2(la, 1), new Vector2(0, 1));
                    foreach (var e in new[] { e0, e1 })
                    {
                        var w0 = e - nrm * 0.85f;
                        k.Quad(iron, e + Vector3.Up * 1.05f, w0 + Vector3.Up * 1.05f, w0, e, new Vector2(0, 0), new Vector2(0.85f, 0), new Vector2(0.85f, 1), new Vector2(0, 1));
                    }
                    k.Solid = true;
                }
                if (cut)
                {
                    // Dalle de balcon arrachée qui pend le long de la façade, garde-corps tordu.
                    var hp = A + along * ((hb0 + 0.15f) * len / bays) + nrm * 0.5f + Vector3.Up * (y - 1.2f);
                    var hang = rot * new Basis(Vector3.Right, 1.1f) * new Basis(Vector3.Back, 0.25f);
                    k.Col = tint * 0.85f;
                    k.Box(stone, hp, new Vector3(0.9f, 0.22f, 2.4f), hang);
                    k.Solid = false;
                    k.Col = new Color(0.08f, 0.08f, 0.08f);
                    k.Tube(Mats.DetailMetal(), hp + Vector3.Up * 1.1f, hp + Vector3.Down * 1.4f + along * 0.6f + nrm * 0.4f, 0.035f, 4);
                    k.Solid = true;
                }
            }
        }

        // Comble mansardé : brisis raide (3,2 m), terrasson faible (1,2 m), plat de zinc au sommet.
        var y0 = H + 0.4f;
        var minHalf = Mathf.Min(s.W, s.D) / 2f;
        var r0 = Inset(poly, 0.3f);
        var r1 = Inset(poly, Mathf.Min(1.4f, minHalf - 1.5f));
        var r2 = Inset(poly, Mathf.Min(3.6f, minHalf - 0.8f));
        var y1 = y0 + 3.2f;
        var y2 = y1 + 1.3f;
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            var a0 = new Vector3(r0[i].X, y0, r0[i].Y); var b0 = new Vector3(r0[j].X, y0, r0[j].Y);
            var a1 = new Vector3(r1[i].X, y1, r1[i].Y); var b1 = new Vector3(r1[j].X, y1, r1[j].Y);
            var a2 = new Vector3(r2[i].X, y2, r2[i].Y); var b2 = new Vector3(r2[j].X, y2, r2[j].Y);
            var nrm = OutNormal(poly[i], poly[j], ctr);
            var len = a0.DistanceTo(b0);
            var burnt = s.BurntEdge >= 0 && i % 4 == s.BurntEdge;
            var sl1 = a0.DistanceTo(a1);
            var sl2 = a1.DistanceTo(a2);
            k.Col = roofCol;
            if (burnt)
            {
                // Comble incendié : brisis éventré, chevrons calcinés à nu, plancher noirci.
                k.Col = new Color(0.5f, 0.5f, 0.5f);
                var holeA = 0.2f + Hh(i + 40) * 0.15f;
                var holeB = 0.65f + Hh(i + 41) * 0.2f;
                void Piece(float t0, float t1)
                {
                    if (t1 - t0 < 0.02f) return;
                    k.QuadC(charred, a0.Lerp(b0, t0), a0.Lerp(b0, t1), a1.Lerp(b1, t1), a1.Lerp(b1, t0),
                        new Vector2(t0 * len, 0), new Vector2(t1 * len, 0), new Vector2(t1 * len, sl1), new Vector2(t0 * len, sl1), k.Col, k.Col, k.Col, k.Col, nrm + Vector3.Up * 0.3f);
                }
                Piece(0f, holeA);
                Piece(holeB, 1f);
                k.OrientedQuad(charred, a1, b1, b2, a2, nrm + Vector3.Up * 2f);
                // Plancher du comble (noir) sous le trou.
                var fA = a0.Lerp(b0, holeA) - nrm * 0.2f; var fB = a0.Lerp(b0, holeB) - nrm * 0.2f;
                k.Col = new Color(0.25f, 0.24f, 0.23f);
                k.OrientedQuad(charred, fA + Vector3.Up * 0.05f, fB + Vector3.Up * 0.05f, fB - nrm * 3f + Vector3.Up * 0.05f, fA - nrm * 3f + Vector3.Up * 0.05f, Vector3.Up);
                var rafters = Mathf.Max(2, (int)((holeB - holeA) * len / 0.7f));
                for (var q = 0; q <= rafters; q++)
                {
                    var t = Mathf.Lerp(holeA, holeB, q / (float)rafters);
                    var p0 = a0.Lerp(b0, t);
                    var p1 = a1.Lerp(b1, t);
                    var p2 = a2.Lerp(b2, t);
                    var cut = Hh(i * 50 + q);
                    k.Col = new Color(0.16f, 0.13f, 0.11f);
                    if (cut < 0.3f) k.Tube(charred, p0, p0.Lerp(p1, 0.3f + cut), 0.08f, 4);
                    else
                    {
                        k.Tube(charred, p0, p1, 0.08f, 4);
                        if (cut > 0.55f) k.Tube(charred, p1, p1.Lerp(p2, cut), 0.07f, 4);
                    }
                }
                continue;
            }
            k.QuadC(roofMat, a0, b0, b1, a1, new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, sl1), new Vector2(0, sl1),
                roofCol, roofCol, roofCol * 0.92f, roofCol * 0.92f, nrm + Vector3.Up * 0.3f);
            k.QuadC(roofMat, a1, b1, b2, a2, new Vector2(0, sl1), new Vector2(len, sl1), new Vector2(len, sl1 + sl2), new Vector2(0, sl1 + sl2),
                roofCol * 0.92f, roofCol * 0.92f, roofCol * 0.85f, roofCol * 0.85f, nrm + Vector3.Up * 2f);
            // Lucarnes à fronton sur les côtés rue (une par travée, en retrait des angles).
            if (!IsStreet(nrm, s.Street)) continue;
            var along = (b0 - a0) / len;
            var bays = Mathf.Max(1, Mathf.RoundToInt(len / Mats.BayW));
            var rot = new Basis(Vector3.Up, Mathf.Atan2(along.X, along.Z));
            for (var q = 0; q < bays; q++)
            {
                var t = (q + 0.5f) / bays;
                if (t * len < 1.6f || (1 - t) * len < 1.6f) continue;
                var baseP = a0.Lerp(b0, t);
                Dormer(k, baseP, nrm, along, rot, facade, stone, roofMat, tint, roofCol, s.Seed * 7 + q + i * 13);
            }
        }
        // Plat du sommet (éventail depuis le centre).
        var top = Vector3.Zero;
        foreach (var p in r2) top += new Vector3(p.X, y2, p.Y);
        top /= r2.Count;
        k.Col = roofCol * 0.8f;
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            k.Tri(roofMat, top, new Vector3(r2[i].X, y2, r2[i].Y), new Vector3(r2[j].X, y2, r2[j].Y), top + Vector3.Down);
        }
        // Souches de cheminées sur le comble, avec leurs mitrons.
        var chimneys = 2 + (int)(Hh(90) * 2);
        for (var q = 0; q < chimneys; q++)
        {
            var i = (int)(Hh(91 + q) * n) % n;
            var j = (i + 1) % n;
            var t = 0.2f + Hh(95 + q) * 0.6f;
            var p = new Vector3(Mathf.Lerp(r2[i].X, r2[j].X, t), y2, Mathf.Lerp(r2[i].Y, r2[j].Y, t));
            p = p.Lerp(top, 0.25f);
            var along = new Vector3(r2[j].X - r2[i].X, 0, r2[j].Y - r2[i].Y).Normalized();
            var rot = new Basis(Vector3.Up, Mathf.Atan2(along.X, along.Z));
            var hgt = 1.6f + Hh(99 + q) * 1.2f;
            k.Col = tint * 0.85f;
            k.Box(stone, p + Vector3.Up * (hgt / 2 - 0.2f), new Vector3(0.75f, hgt + 0.4f, 2.2f), rot);
            k.Box(stone, p + Vector3.Up * (hgt + 0.25f), new Vector3(0.9f, 0.12f, 2.35f), rot);
            k.Col = new Color(0.42f, 0.27f, 0.2f);
            for (var m = 0; m < 4; m++)
            {
                var pp = p + rot * new Vector3(0, hgt + 0.3f, -0.8f + m * 0.53f);
                k.Cylinder(Mats.Detail(), pp, 0.13f, 0.1f, 0.42f + Hh(110 + m + q * 4) * 0.25f, 6);
            }
        }
        return k.Commit();
    }

    /// <summary>
    /// Lucarne à fronton sur le brisis : jouées de zinc, fenêtre (cellule de la texture de façade : vitres soufflées
    /// ou brûlées comme aux étages), encadrement et fronton de pierre, petit toit à deux pans.
    /// </summary>
    private static void Dormer(MeshKit k, Vector3 baseP, Vector3 nrm, Vector3 along, Basis rot, Material facade, Material stone, Material roof, Color tint, Color roofCol, int seed)
    {
        const float hw = 0.68f, depth = 1.3f;
        var y0 = baseP.Y + 0.55f;
        var y1 = baseP.Y + 2.35f;
        var f = baseP - nrm * 0.18f;  // plan de la fenêtre (un peu en retrait de l'égout)
        Vector3 P(float x, float y, float d) => f + along * x + Vector3.Up * (y - baseP.Y) - nrm * d;
        // Fenêtre : une cellule de la texture des étages (même tirage d'état que les fenêtres).
        var bu = Mathf.Floor(RuinKit.Hash(seed, 1, 441) * 4f);
        var bv = Mathf.Floor(RuinKit.Hash(seed, 2, 441) * 4f);
        k.Col = tint;
        k.QuadC(facade, P(-hw + 0.12f, baseP.Y + 0.6f, -0.01f), P(hw - 0.12f, baseP.Y + 0.6f, -0.01f), P(hw - 0.12f, y1 - 0.12f, -0.01f), P(-hw + 0.12f, y1 - 0.12f, -0.01f),
            new Vector2(bu + 0.29f, bv + 0.93f), new Vector2(bu + 0.71f, bv + 0.93f), new Vector2(bu + 0.71f, bv + 0.17f), new Vector2(bu + 0.29f, bv + 0.17f), tint, tint, tint, tint, nrm);
        // Encadrement de pierre (montants, appui, linteau) et fronton triangulaire.
        k.Box(stone, P(-hw + 0.06f, (y0 + y1) / 2, 0.05f), new Vector3(0.14f, y1 - y0, 0.14f), rot);
        k.Box(stone, P(hw - 0.06f, (y0 + y1) / 2, 0.05f), new Vector3(0.14f, y1 - y0, 0.14f), rot);
        k.Box(stone, P(0, y0 + 0.03f, 0.05f), new Vector3(0.18f, 0.12f, hw * 2 + 0.1f), rot);
        k.Box(stone, P(0, y1 + 0.02f, 0.05f), new Vector3(0.2f, 0.14f, hw * 2 + 0.16f), rot);
        var apex = P(0, y1 + 0.6f, -0.02f);
        k.Tri(stone, P(-hw - 0.08f, y1 + 0.09f, -0.02f), P(hw + 0.08f, y1 + 0.09f, -0.02f), apex, apex - nrm);
        // Jouées (dans le plan de la lucarne vers le comble) et toit à deux pans.
        k.Col = roofCol;
        foreach (var sx in new[] { -hw, hw })
            k.OrientedQuad(roof, P(sx, y0, 0), P(sx, y0, depth), P(sx, y1 + 0.1f, depth), P(sx, y1 + 0.1f, 0), along * Mathf.Sign(sx));
        var ridgeF = P(0, y1 + 0.62f, 0.02f);
        var ridgeB = P(0, y1 + 0.62f, depth);
        foreach (var sx in new[] { -hw - 0.12f, hw + 0.12f })
            k.OrientedQuad(roof, P(sx, y1 + 0.08f, 0.02f), P(sx, y1 + 0.08f, depth), ridgeB, ridgeF, Vector3.Up + along * Mathf.Sign(sx));
    }

    /// <summary>
    /// Brèche d'obus dans une façade (entre <paramref name="a"/> et <paramref name="b"/>, de <paramref name="y0"/> à
    /// <paramref name="y1"/>) : cavité calcinée (fond, tableaux, plafond, sol), planchers intermédiaires brisés qui pendent,
    /// bords déchiquetés de pierre, gravats sur l'appui, panache de suie au-dessus. Visuel seul (le volume plein reste).
    /// </summary>
    private static void Breach(MeshKit k, Vector3 a, Vector3 b, Vector3 nrm, Vector3 along, float y0, float y1, Material stone, Material charred, Color tint, int seed)
    {
        var solid = k.Solid;
        k.Solid = false;
        const float depth = 3.4f;
        var back = -nrm * depth;
        var up = Vector3.Up;
        var len = a.DistanceTo(b);
        var rot = new Basis(Vector3.Up, Mathf.Atan2(along.X, along.Z));
        float Hx(int salt) => RuinKit.Hash(seed * 0.73f, salt, 457);
        k.Col = new Color(0.22f, 0.21f, 0.2f);
        k.OrientedQuad(charred, a + back + up * y0, b + back + up * y0, b + back + up * y1, a + back + up * y1, nrm);
        k.Col = tint * 0.45f;
        k.OrientedQuad(stone, a + up * y0, a + back + up * y0, a + back + up * y1, a + up * y1, along);
        k.OrientedQuad(stone, b + up * y0, b + back + up * y0, b + back + up * y1, b + up * y1, -along);
        k.OrientedQuad(stone, a + up * y0, b + up * y0, b + back + up * y0, a + back + up * y0, Vector3.Up);
        k.Col = new Color(0.25f, 0.24f, 0.22f);
        k.OrientedQuad(charred, a + up * y1, b + up * y1, b + back + up * y1, a + back + up * y1, Vector3.Down);
        // Planchers intermédiaires : dalles à nu, bord cassé plié vers la rue.
        for (var yf = Mats.ShopStoryH + Mats.StoryH; yf < y1 - 0.4f; yf += Mats.StoryH)
        {
            if (yf < y0 + 0.4f) continue;
            var c = (a + b) / 2 + back * 0.55f + up * yf;
            k.Col = new Color(0.4f, 0.39f, 0.37f) * (0.8f + 0.2f * Hx((int)yf));
            k.Box(stone, c, new Vector3(depth * 0.55f, 0.3f, len), rot);
            var tilt = 0.4f + Hx((int)yf + 3) * 0.6f;
            var hinge = c - back * 0.275f;
            var flap = new Basis(along, -tilt);
            k.Box(stone, hinge + flap * (nrm * 0.7f) + Vector3.Down * 0.15f, new Vector3(1.4f, 0.26f, len * 0.8f), flap * rot);
            k.Col = new Color(0.3f, 0.17f, 0.1f);
            for (var q = 0; q < 4; q++)
            {
                var p0 = hinge + along * ((q - 1.5f) * len * 0.22f);
                k.Tube(Mats.DetailMetal(), p0, p0 + nrm * (0.8f + Hx(q + (int)yf) * 0.8f) + Vector3.Down * 0.6f, 0.025f, 3);
            }
        }
        // Bords déchiquetés : blocs de pierre épaufrés qui mordent sur l'ouverture.
        k.Col = tint * 0.9f;
        for (var t = 0.3f; t < len; t += 0.7f)
        {
            var h0 = Hx((int)(t * 10f) + 11);
            var h1 = Hx((int)(t * 10f) + 13);
            k.Box(stone, a + along * t + up * (y1 - 0.2f - h0 * 0.5f) - nrm * 0.1f, new Vector3(0.5f, 0.4f + h0 * 0.9f, 0.6f), rot * new Basis(Vector3.Back, (h1 - 0.5f) * 0.5f));
            k.Box(stone, a + along * t + up * (y0 + 0.15f + h1 * 0.35f) - nrm * 0.1f, new Vector3(0.5f, 0.3f + h1 * 0.6f, 0.6f), rot);
        }
        for (var y = y0 + 0.5f; y < y1 - 0.3f; y += 0.8f)
        {
            var h0 = Hx((int)(y * 10f) + 17);
            k.Box(stone, a + along * (0.15f + h0 * 0.3f) + up * y - nrm * 0.1f, new Vector3(0.5f, 0.6f, 0.4f + h0 * 0.6f), rot);
            k.Box(stone, b - along * (0.15f + h0 * 0.3f) + up * y - nrm * 0.1f, new Vector3(0.5f, 0.6f, 0.4f + Hx((int)(y * 10f) + 19) * 0.6f), rot);
        }
        // Gravats sur l'appui de la brèche.
        k.Col = new Color(0.45f, 0.43f, 0.4f);
        for (var q = 0; q < 6; q++)
        {
            var p = a + along * (len * (0.15f + 0.7f * Hx(q + 23))) + back * (0.2f + 0.6f * Hx(q + 29)) + up * (y0 + 0.2f);
            var sz = 0.4f + Hx(q + 31) * 0.7f;
            k.Box(stone, p, new Vector3(sz, sz * 0.6f, sz * 1.2f), new Basis(Vector3.Up, Hx(q + 37) * 3f) * new Basis(Vector3.Right, Hx(q + 41) * 0.6f));
        }
        // Suie au-dessus de la brèche.
        const float top = 3.6f;
        var u0 = Hx(43) * 4f;
        k.Col = Colors.White;
        k.Quad(Mats.Soot(), a + up * y1 + nrm * 0.03f, b + up * y1 + nrm * 0.03f, b + up * (y1 + top) + nrm * 0.03f, a + up * (y1 + top) + nrm * 0.03f,
            new Vector2(u0, 1), new Vector2(u0 + len / 8f, 1), new Vector2(u0 + len / 8f, 1 - top / 8f), new Vector2(u0, 1 - top / 8f));
        k.Solid = solid;
    }
}
