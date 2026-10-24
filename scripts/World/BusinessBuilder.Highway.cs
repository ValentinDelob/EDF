using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Ouvrages du quartier d'affaires : viaduc autoroutier au-dessus de la rue z = 128 (tablier à 9,4 m, piles en
/// T, glissières béton), bretelle d'accès à l'ouest, travée effondrée à l'est (rampe de gravats praticable),
/// mur antibruit, et passerelles vitrées entre tours. Tout ce qui est accessible a une collision exacte.
/// </summary>
public static partial class BusinessBuilder
{
    private const float PierEvery = 32f;
    /// <summary>Travée effondrée : entre les piles x = 144 et x = 176.</summary>
    private const float FallenX0 = 144f, FallenX1 = 176f;
    /// <summary>Bretelle : du sol (x = -214) au niveau du tablier (x = -112), raccord jusqu'à x = -86.</summary>
    private const float RampX0 = -214f, RampX1 = -112f, RampJoin = -86f;
    private const float RampZ0 = 139.6f, RampZ1 = 146.6f;

    private static void BuildHighway(Ctx c)
    {
        var far = new Chunks();
        var concrete = new Color(0.5f, 0.49f, 0.46f);
        var d = Mats.Detail();
        var z0 = HighwayZ - DeckHalf;
        var z1 = HighwayZ + DeckHalf;
        const float slab = 1.1f;
        // Tablier par travées de 32 m (à l'intérieur : solide ; au-delà des murs : décor seul).
        for (var x = -800f; x < 800f; x += PierEvery)
        {
            var xa = x;
            var xb = x + PierEvery;
            var inside = xb > -262f && xa < 262f;
            var k = inside ? c.Solid.At(new Vector3((xa + xb) / 2, 0, HighwayZ)) : far.At(new Vector3((xa + xb) / 2, 0, HighwayZ));
            var pierX = xa + PierEvery / 2;  // piles au milieu des pas de 32 m : x = 16 + 32 k
            // Pile en T (fût dans le terre-plein de la rue, chevêtre sous le caisson).
            if (!(pierX > FallenX0 + 1f && pierX < FallenX1 - 1f))
            {
                k.Col = concrete.Darkened(0.05f);
                k.Box(d, new Vector3(pierX, (DeckTop - 3.6f) / 2, HighwayZ), new Vector3(2.2f, DeckTop - 3.6f, 3.2f));
                k.Col = concrete;
                k.Frustum(d, new Vector3(pierX, DeckTop - 3.6f, HighwayZ), new Vector2(2.4f, 3.4f), new Vector2(2.4f, 13.5f), 1.2f);
            }
            // Tronçons de tablier (la travée effondrée est traitée à part).
            foreach (var (sa, sb) in Split(xa, xb, FallenX0, FallenX1))
                DeckSpan(c, k, sa, sb, inside, z0, z1, slab);
        }
        far.Flush(c.Root, "HighwayFar", false);
        FallenSpan(c, z0, z1, slab);
        BuildRamp(c, slab);

        // Mur antibruit au sud (limite de l'aire de jeu), panneaux de béton entre poteaux métalliques.
        var wk = c.Solid.At(new Vector3(0, 0, 152));
        for (var x = -240f; x < 240f; x += 4f)
        {
            wk.Col = new Color(0.44f, 0.43f, 0.4f) * (0.92f + 0.12f * RuinKit.Hash(x, 152, 3));
            var hgt = RuinKit.Hash(x, 152, 4) < 0.08f ? 1.6f : 3.6f;   // quelques panneaux tombés
            wk.Box(d, new Vector3(x + 2f, hgt / 2, 152f), new Vector3(3.9f, hgt, 0.25f));
            wk.Col = new Color(0.22f, 0.23f, 0.24f);
            wk.Box(Mats.DetailMetal(), new Vector3(x, 1.9f, 152f), new Vector3(0.22f, 3.8f, 0.3f));
        }
    }

    /// <summary>Découpe [a, b] en retirant l'intervalle [cut0, cut1].</summary>
    private static IEnumerable<(float, float)> Split(float a, float b, float cut0, float cut1)
    {
        if (b <= cut0 || a >= cut1) { yield return (a, b); yield break; }
        if (a < cut0) yield return (a, cut0);
        if (b > cut1) yield return (cut1, b);
    }

    /// <summary>Tronçon droit de tablier : caisson, dalle, enrobé, glissières, terre-plein central, marquages, candélabres.</summary>
    private static void DeckSpan(Ctx c, MeshKit k, float xa, float xb, bool solid, float z0, float z1, float slab)
    {
        var d = Mats.Detail();
        var concrete = new Color(0.5f, 0.49f, 0.46f);
        var mid = (xa + xb) / 2;
        var len = xb - xa;
        k.Col = concrete.Darkened(0.12f);
        k.Box(d, new Vector3(mid, DeckTop - slab - 1.2f, HighwayZ), new Vector3(len, 2.4f, 10f));
        k.Col = concrete;
        k.Box(d, new Vector3(mid, DeckTop - slab / 2 - 0.05f, HighwayZ), new Vector3(len, slab - 0.1f, z1 - z0));
        k.Col = Colors.White;
        // Raccord de la bretelle : la glissière sud est ouverte sur le raccord.
        var southOpen = xb > RampX1 - 0.5f && xa < RampJoin + 0.5f;
        k.Box(Mats.Asphalt(), new Vector3(mid, DeckTop - 0.05f, HighwayZ), new Vector3(len, 0.1f, z1 - z0 - 0.9f));
        k.Col = concrete.Lightened(0.05f);
        // Glissières (profil à deux pentes : base large, haut étroit).
        Barrier(k, xa, xb, z0 + 0.32f, DeckTop - 0.1f);
        if (!southOpen) Barrier(k, xa, xb, z1 - 0.32f, DeckTop - 0.1f);
        else
        {
            foreach (var (sa, sb) in Split(xa, xb, RampX1, RampJoin))
                Barrier(k, sa, sb, z1 - 0.32f, DeckTop - 0.1f);
        }
        Barrier(k, xa, xb, HighwayZ, DeckTop - 0.1f, 0.8f);
        // Marquages (non solides).
        k.Solid = false;
        k.Col = Colors.White;
        var paint = Mats.RoadPaint();
        foreach (var lz in new[] { HighwayZ - 4f, HighwayZ + 4f })
            for (var x = xa + 1f; x + 3f < xb; x += 10f)
                k.Quad(paint, new Vector3(x, DeckTop + 0.004f, lz + 0.08f), new Vector3(x + 3f, DeckTop + 0.004f, lz + 0.08f), new Vector3(x + 3f, DeckTop + 0.004f, lz - 0.08f), new Vector3(x, DeckTop + 0.004f, lz - 0.08f));
        foreach (var lz in new[] { z0 + 1.1f, HighwayZ - 0.9f, HighwayZ + 0.9f, z1 - 1.1f })
            k.Quad(paint, new Vector3(xa, DeckTop + 0.004f, lz + 0.07f), new Vector3(xb, DeckTop + 0.004f, lz + 0.07f), new Vector3(xb, DeckTop + 0.004f, lz - 0.07f), new Vector3(xa, DeckTop + 0.004f, lz - 0.07f));
        // Joint de dilatation sombre.
        k.Col = new Color(0.1f, 0.1f, 0.1f);
        k.Box(Mats.DetailMetal(), new Vector3(xa + 0.15f, DeckTop - 0.03f, HighwayZ), new Vector3(0.3f, 0.06f, z1 - z0 - 0.9f));
        k.Solid = true;
        // Candélabres doubles sur le terre-plein (éteints).
        if (len > 20f)
        {
            var lp = new Vector3(mid, DeckTop + 0.7f, HighwayZ);
            k.Col = new Color(0.32f, 0.33f, 0.34f);
            k.Cylinder(Mats.DetailMetal(), lp, 0.14f, 0.09f, 9f, 6);
            foreach (var s in new[] { -1f, 1f })
            {
                k.Tube(Mats.DetailMetal(), lp + Vector3.Up * 9f, lp + new Vector3(0, 9.3f, s * 2.4f), 0.06f, 4);
                k.Box(Mats.DetailMetal(), lp + new Vector3(0, 9.25f, s * 2.6f), new Vector3(0.35f, 0.16f, 0.8f));
            }
        }
        _ = solid;
    }

    /// <summary>Glissière en béton (profil trapézoïdal) le long de X.</summary>
    private static void Barrier(MeshKit k, float xa, float xb, float z, float y, float h = 1.05f)
    {
        if (xb - xa < 0.3f) return;
        k.Frustum(Mats.Detail(), new Vector3((xa + xb) / 2, y, z), new Vector2(xb - xa, 0.62f), new Vector2(xb - xa, 0.26f), h);
    }

    /// <summary>
    /// Travée effondrée : le tablier, encore accroché à la pile ouest, s'est couché jusqu'à la chaussée (pente de
    /// 16° praticable par les joueurs et les insectes) ; armatures à nu, gravats au pied, pile est écorchée.
    /// </summary>
    private static void FallenSpan(Ctx c, float z0, float z1, float slab)
    {
        var a = new Vector3(FallenX0 + 0.4f, DeckTop - 0.06f, HighwayZ);
        var b = new Vector3(FallenX1 - 3.2f, 0.1f, HighwayZ + 1.6f);
        var dir = (b - a).Normalized();
        var len = a.DistanceTo(b);
        var x = dir;
        var zAxis = x.Cross(Vector3.Up).Normalized();   // vers +Z
        var y = zAxis.Cross(x).Normalized();
        var roll = new Basis(x, 0.015f);   // léger dévers (au-delà, la jonction avec le tablier ferait une marche)
        var basis = roll * new Basis(x, y, zAxis);
        var k = c.Solid.At((a + b) / 2);
        var d = Mats.Detail();
        var concrete = new Color(0.47f, 0.46f, 0.43f);
        var mid = (a + b) / 2;
        k.Col = concrete;
        k.Box(d, mid - basis.Y * (slab / 2), new Vector3(len, slab, z1 - z0), basis);
        k.Col = concrete.Darkened(0.15f);
        k.Box(d, mid - basis.Y * (slab + 1.1f), new Vector3(len - 1f, 2.2f, 10f), basis);
        k.Col = Colors.White;
        k.Box(Mats.Asphalt(), mid + basis.Y * 0.04f, new Vector3(len, 0.08f, z1 - z0 - 0.9f), basis);
        // Glissière nord encore en place, glissière sud arrachée sur la moitié haute.
        k.Col = concrete.Lightened(0.05f);
        k.Box(d, mid + basis.Y * 0.5f + basis.Z * ((z0 - HighwayZ) + 0.32f), new Vector3(len, 1.05f, 0.45f), basis);
        k.Box(d, mid + basis.Y * 0.5f + basis.Z * ((z1 - HighwayZ) - 0.32f) + basis.X * (len * 0.25f), new Vector3(len * 0.5f, 1.05f, 0.45f), basis);
        // Armatures tordues aux deux bords cassés (non solides).
        k.Solid = false;
        k.Col = new Color(0.22f, 0.15f, 0.11f);
        for (var i = 0; i < 14; i++)
        {
            var zz = Mathf.Lerp(z0 + 0.6f, z1 - 0.6f, i / 13f);
            var h1 = RuinKit.Hash(i, 1, 71);
            var h2 = RuinKit.Hash(i, 2, 71);
            var top = new Vector3(FallenX1 + 0.2f, DeckTop - 0.7f + h1 * 0.4f, zz);
            k.Tube(Mats.DetailMetal(), top, top + new Vector3(-0.8f - h2 * 1.4f, -0.6f - h1 * 1.6f, (h2 - 0.5f) * 0.6f), 0.03f, 4);
            var low = b + basis.Z * (zz - HighwayZ) + basis.X * 0.2f;
            k.Tube(Mats.DetailMetal(), low, low + new Vector3(0.6f + h1 * 1.2f, 0.4f + h2 * 1.5f, (h1 - 0.5f) * 0.5f), 0.03f, 4);
        }
        k.Solid = true;
        // Bord cassé du tablier est (face d'arrachement sombre).
        k.Col = new Color(0.3f, 0.29f, 0.27f);
        k.Box(d, new Vector3(FallenX1 + 0.3f, DeckTop - 0.6f, HighwayZ), new Vector3(0.6f, 1.2f, z1 - z0 - 0.4f));
        // Gravats au pied et sous la dalle, poussière, fumée.
        var r = c.Ruins;
        var foot = new Vector3(FallenX1 - 4f, 0, HighwayZ + 1f);
        r.Convex(Props.RubblePile(r.Kit(RuinKit.Layer.Solid, foot), foot, 0.3f, new Vector3(12f, 1.8f, 9f), 4401, concrete, false));
        var under = new Vector3(FallenX0 + 9f, 0, HighwayZ - 6.5f);
        r.Convex(Props.RubblePile(r.Kit(RuinKit.Layer.Solid, under), under, 1.2f, new Vector3(7f, 1.3f, 4f), 4402, concrete, false));
        r.Decal(Mats.Decal.Dust, foot, 22f, 0.4f, 0.03f);
        r.Decal(Mats.Decal.Cracks, foot + new Vector3(-6, 0, -4), 9f, 1.1f, 0.032f);
        r.Smolder(foot + Vector3.Up * 1.5f, 1.4f);
        r.Block(new Rect2(FallenX0 - 2f, z0 - 2f, FallenX1 - FallenX0 + 4f, z1 - z0 + 4f));
    }

    /// <summary>
    /// Bretelle d'accès au sud du viaduc : rampe à 5° depuis la chaussée, petites piles, glissières des deux côtés,
    /// raccord à niveau avec le tablier (glissière sud du tablier ouverte sur le raccord).
    /// </summary>
    private static void BuildRamp(Ctx c, float slab)
    {
        var d = Mats.Detail();
        var concrete = new Color(0.5f, 0.49f, 0.46f);
        var len = RampX1 - RampX0;
        var ang = Mathf.Atan2(DeckTop, len);
        var basis = new Basis(Vector3.Back, ang);
        var zc = (RampZ0 + RampZ1) / 2;
        var w = RampZ1 - RampZ0;
        var mid = new Vector3((RampX0 + RampX1) / 2, DeckTop / 2, zc);
        var k = c.Solid.At(mid);
        // Rampe : dalle (dessus au niveau de la chaussée en bas, du tablier en haut).
        k.Col = concrete;
        k.Box(d, mid - basis.Y * (slab / 2), new Vector3(len / Mathf.Cos(ang) + 0.6f, slab, w), basis);
        k.Col = Colors.White;
        k.Box(Mats.Asphalt(), mid + basis.Y * 0.03f, new Vector3(len / Mathf.Cos(ang), 0.06f, w - 0.9f), basis);
        k.Col = concrete.Lightened(0.05f);
        foreach (var bz in new[] { RampZ0 + 0.3f, RampZ1 - 0.3f })
            k.Box(d, mid + basis.Y * 0.5f + new Vector3(0, 0, bz - zc), new Vector3(len / Mathf.Cos(ang), 1.05f, 0.42f), basis);
        // Piles sous la rampe.
        k.Col = concrete.Darkened(0.05f);
        for (var x = RampX0 + 30f; x < RampX1; x += 24f)
        {
            var top = (x - RampX0) / len * DeckTop - slab;
            if (top < 1.2f) continue;
            k.Box(d, new Vector3(x, top / 2, zc), new Vector3(1.2f, top, 2.4f));
        }
        // Raccord à niveau entre la bretelle et le tablier.
        var jk = c.Solid.At(new Vector3((RampX1 + RampJoin) / 2, 0, zc));
        var jz0 = HighwayZ + DeckHalf - 0.2f;
        jk.Col = concrete;
        jk.BoxMinMax(d, new Vector3(RampX1 - 0.3f, DeckTop - slab, jz0), new Vector3(RampJoin, DeckTop - 0.06f, RampZ1));
        jk.Col = Colors.White;
        jk.BoxMinMax(Mats.Asphalt(), new Vector3(RampX1 - 0.3f, DeckTop - 0.1f, jz0), new Vector3(RampJoin, DeckTop, RampZ1 - 0.45f));
        jk.Col = concrete.Lightened(0.05f);
        Barrier(jk, RampX1, RampJoin, RampZ1 - 0.3f, DeckTop - 0.1f);
        // Musoir : séparateur au bout du raccord.
        jk.Box(d, new Vector3(RampJoin - 0.4f, DeckTop + 0.4f, (jz0 + RampZ1) / 2 + 0.6f), new Vector3(0.8f, 1f, RampZ1 - jz0 - 1.2f));
        jk.Col = new Color(0.32f, 0.33f, 0.34f);
        jk.Box(d, new Vector3(RampJoin - 0.1f, DeckTop - slab - 1.2f, (jz0 + RampZ1) / 2), new Vector3(1.2f, 2.4f, 4f));
        jk.Box(d, new Vector3((RampX1 + RampJoin) / 2, (DeckTop - slab - 2.4f) / 2, (jz0 + RampZ1) / 2 + 1f), new Vector3(1.2f, DeckTop - slab - 2.4f, 2f));
        c.Ruins.Block(new Rect2(RampX0, RampZ0 - 0.5f, RampJoin - RampX0, RampZ1 - RampZ0 + 1f));
    }

    // ───────────────────────── Passerelles ─────────────────────────

    /// <summary>
    /// Passerelles vitrées entre deux tours qui se font face de part et d'autre d'une rue : plancher, toiture
    /// praticable, montants d'acier, vitrages en partie soufflés, poutre treillis dessous.
    /// </summary>
    private static void BuildSkybridges(Ctx c)
    {
        var made = 0;
        var pairs = new List<(Building a, Building b, bool alongX, float gap)>();
        foreach (var a in c.Towers)
        foreach (var b in c.Towers)
        {
            if (a == b) continue;
            var ax1 = a.Position.X + a.Size.X / 2;
            var bx0 = b.Position.X - b.Size.X / 2;
            var gx = bx0 - ax1;
            var oz = Mathf.Min(a.Position.Z + a.Size.Z / 2, b.Position.Z + b.Size.Z / 2) - Mathf.Max(a.Position.Z - a.Size.Z / 2, b.Position.Z - b.Size.Z / 2);
            if (gx >= 16f && gx <= 34f && oz >= 10f) pairs.Add((a, b, true, gx));
            var az1 = a.Position.Z + a.Size.Z / 2;
            var bz0 = b.Position.Z - b.Size.Z / 2;
            var gz = bz0 - az1;
            var ox = Mathf.Min(a.Position.X + a.Size.X / 2, b.Position.X + b.Size.X / 2) - Mathf.Max(a.Position.X - a.Size.X / 2, b.Position.X - b.Size.X / 2);
            if (gz >= 16f && gz <= 34f && ox >= 10f) pairs.Add((a, b, false, gz));
        }
        // Les plus proches du parvis d'abord.
        pairs.Sort((p, q) => ((p.a.Position + p.b.Position).Length()).CompareTo((q.a.Position + q.b.Position).Length()));
        var used = new HashSet<Building>();
        foreach (var (a, b, alongX, _) in pairs)
        {
            if (made >= 3 || used.Contains(a) || used.Contains(b)) continue;
            var top = Mathf.Min(a.Size.Y, b.Size.Y);
            if (top < 40f) continue;
            // Plancher sur la trame d'étages (0,6 m + n × 3,2 m), vers le tiers de la hauteur.
            var floor = Walk + 0.6f + 3.2f * Mathf.Round((top * (0.3f + 0.15f * made) - 0.6f) / 3.2f);
            Vector3 from, to;
            if (alongX)
            {
                var z = (Mathf.Max(a.Position.Z - a.Size.Z / 2, b.Position.Z - b.Size.Z / 2) + Mathf.Min(a.Position.Z + a.Size.Z / 2, b.Position.Z + b.Size.Z / 2)) / 2;
                from = new Vector3(a.Position.X + a.Size.X / 2 - 0.3f, floor, z);
                to = new Vector3(b.Position.X - b.Size.X / 2 + 0.3f, floor, z);
            }
            else
            {
                var x = (Mathf.Max(a.Position.X - a.Size.X / 2, b.Position.X - b.Size.X / 2) + Mathf.Min(a.Position.X + a.Size.X / 2, b.Position.X + b.Size.X / 2)) / 2;
                from = new Vector3(x, floor, a.Position.Z + a.Size.Z / 2 - 0.3f);
                to = new Vector3(x, floor, b.Position.Z - b.Size.Z / 2 + 0.3f);
            }
            Skybridge(c, from, to, made);
            used.Add(a);
            used.Add(b);
            made++;
        }
    }

    private static void Skybridge(Ctx c, Vector3 from, Vector3 to, int seed)
    {
        var k = c.Solid.At((from + to) / 2);
        var metal = Mats.DetailMetal();
        var d = Mats.Detail();
        var dir = (to - from).Normalized();
        var side = Vector3.Up.Cross(dir).Normalized();
        var len = from.DistanceTo(to);
        var mid = (from + to) / 2;
        const float w = 5f, h = 3.6f;
        var basis = new Basis(side, Vector3.Up, dir);
        var steel = new Color(0.24f, 0.25f, 0.26f);
        // Plancher et toiture.
        k.Col = new Color(0.42f, 0.41f, 0.39f);
        k.Box(d, mid - Vector3.Up * 0.2f, new Vector3(w, 0.4f, len), basis);
        k.Col = steel;
        k.Box(metal, mid + Vector3.Up * (h + 0.15f), new Vector3(w + 0.2f, 0.3f, len), basis);
        // Montants tous les 3 m, lisses haute et basse.
        var n = Mathf.Max(2, Mathf.RoundToInt(len / 3f));
        for (var i = 0; i <= n; i++)
        {
            var p = from + dir * (len * i / n);
            foreach (var s in new[] { -1f, 1f })
                k.Box(metal, p + side * (s * w / 2) + Vector3.Up * (h / 2), new Vector3(0.18f, h, 0.18f), basis);
        }
        foreach (var s in new[] { -1f, 1f })
        {
            k.Box(metal, mid + side * (s * w / 2) + Vector3.Up * 0.5f, new Vector3(0.12f, 0.12f, len), basis);
            // Vitrages : certains soufflés (ouverts), les autres solides.
            for (var i = 0; i < n; i++)
            {
                if (RuinKit.Hash(seed * 31 + i, s > 0 ? 1 : 2, 73) < 0.4f) continue;
                var p = from + dir * (len * (i + 0.5f) / n) + side * (s * w / 2) + Vector3.Up * (h / 2);
                k.Box(Mats.Glass(), p, new Vector3(0.04f, h - 0.2f, len / n - 0.2f), basis);
            }
        }
        // Poutre treillis sous le plancher.
        k.Col = steel.Darkened(0.2f);
        foreach (var s in new[] { -1f, 1f })
        {
            k.Box(metal, mid + side * (s * (w / 2 - 0.2f)) - Vector3.Up * 1.4f, new Vector3(0.2f, 0.2f, len), basis);
            for (var i = 0; i < n; i++)
            {
                var p0 = from + dir * (len * i / n) + side * (s * (w / 2 - 0.2f));
                var p1 = from + dir * (len * (i + 1) / n) + side * (s * (w / 2 - 0.2f));
                k.Tube(metal, p0 - Vector3.Up * 0.4f, p1 - Vector3.Up * 1.4f, 0.07f, 4);
            }
        }
    }
}
