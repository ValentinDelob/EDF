using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Quartier d'affaires envahi : tours éventrées (panneaux arrachés, incendies d'étage, panaches noirs au sommet),
/// immeubles bas noircis et gravats au pied, terrains dévastés où la colonie a creusé ses nids, épaves dans les
/// voies, carambolage, bus encastré, barrages militaires, cratères, verre et débris partout, toiles entre les tours.
/// Graines fixes et hachages de position uniquement (identiques chez tous les pairs).
/// </summary>
public static partial class BusinessBuilder
{
    private static void DressBusiness(Ctx c)
    {
        var r = c.Ruins;
        var rng = r.Rng;
        foreach (var v in c.Vacant) r.Block(v, 11f, 11f);
        var hot = new List<(Vector3 p, float rad, float dens)>();

        // Scènes principales d'abord (elles reçoivent en priorité les lumières de foyers).
        foreach (var v in c.Vacant) DevastatedLot(c, v, hot);
        Pileup(c, hot);
        BusCrash(c, hot);
        Checkpoint(c, new Vector3(0, 0, -66), hot);
        Checkpoint2(c, new Vector3(-130, 0, 60), hot);
        // Mâts d'éclairage militaires (seules vraies sources de lumière quand le réseau est coupé).
        FloodLight(c, new Vector3(16, Walk, -50), new Vector3(0, 0, -10));
        FloodLight(c, new Vector3(-24, Walk, 46), new Vector3(-4, 0, 10));
        FloodLight(c, new Vector3(39, Walk, -24), new Vector3(10, 0, 0));
        FloodLight(c, new Vector3(-121, 0, 72), new Vector3(-130, 0, 40));
        FloodLight(c, new Vector3(-200, 0, 132), new Vector3(-170, 0, 128));

        // Tours : panneaux arrachés, calque de vitres brisées, incendies d'étage et panaches au sommet.
        var plumes = 0;
        foreach (var b in c.Res.Buildings)
        {
            if (b.Standing)
            {
                if (b.Position.Y > 1f) { b.AddPanelScars(1 + (int)(RuinKit.Hash(b.Id, 2, 5) * 4), b.Id * 3 + 1, c.Night); continue; }
                var hd = RuinKit.Hash(b.Id, 3, 5);
                b.AddPanelScars(4 + (int)(hd * 12), b.Id * 3, c.Night);
                // (pas de calque de vitres brisées sur 150 m de mur-rideau : illisible et bruité ; les panneaux arrachés suffisent)
                if (hd < 0.3f && plumes < 5)
                {
                    // Étages en feu : flammes à une fenêtre haute, fumée noire qui monte du toit.
                    plumes++;
                    var side = RuinKit.Hash(b.Id, 4, 5) < 0.5f ? 1f : -1f;
                    var fy = Mathf.Floor(b.Size.Y * (0.5f + RuinKit.Hash(b.Id, 6, 5) * 0.35f) / 3.2f) * 3.2f - 1.2f;
                    var fp = b.Position + new Vector3((RuinKit.Hash(b.Id, 7, 5) - 0.5f) * b.Size.X * 0.5f, fy, side * (b.Size.Z / 2 + 0.5f));
                    r.Fire(fp, 1.6f, r.Lights < 4);
                    r.Smolder(b.Position + Vector3.Up * (b.Size.Y + 2f), 2.6f);
                }
                // Verre et fragments au pied.
                var foot = new Vector3(b.Position.X, 0, b.Position.Z);
                hot.Add((foot, Mathf.Max(b.Size.X, b.Size.Z) * 0.75f, 0.22f));
                continue;
            }
            if (b.Position.Y > 1f) continue;
            var hb = RuinKit.Hash(b.Id, 3, 2);
            if (hb < 0.5f && Mats.FacadeVariantOf(b.FacadeMat) >= 0) // pas de calque sur le parking ouvert
            {
                RuinKit.DamageFacades(b, false, b.Id); // calque léger seulement : le lourd devient un damier illisible de loin sur les grands volumes
                var roofY = b.Position.Y + b.Size.Y + 0.04f;
                var rs = Mathf.Min(b.Size.X, b.Size.Z) * 0.9f;
                r.Decal(hb < 0.25f ? Mats.Decal.Crater : Mats.Decal.Scorch, b.Position + new Vector3((hb - 0.3f) * b.Size.X * 0.5f, 0, 0), rs * (hb < 0.25f ? 0.8f : 1f), b.Id, roofY);
            }
            if (RuinKit.Hash(b.Id, 4, 2) < (hb < 0.25f ? 0.7f : 0.15f)) RubbleAt(c, b, hb < 0.25f, hot);
            if (hb < 0.25f && RuinKit.Hash(b.Id, 13, 2) < 0.35f && b.Size.Y > 9f)
            {
                var side = RuinKit.Hash(b.Id, 14, 2) < 0.5f ? 1f : -1f;
                var fy = Mathf.Floor(b.Size.Y * (0.45f + RuinKit.Hash(b.Id, 15, 2) * 0.4f) / 3.2f) * 3.2f - 1.4f;
                var fp = b.Position + new Vector3((RuinKit.Hash(b.Id, 16, 2) - 0.5f) * b.Size.X * 0.6f, fy, side * (b.Size.Z / 2 + 0.4f));
                if (b.Id % 2 == 0) r.Fire(fp, 1.1f, false);
                r.Smolder(b.Position + Vector3.Up * (b.Size.Y + 1f), 2f);
            }
        }

        // Épaves abandonnées dans les voies (de travers, parfois retournées).
        foreach (var st in Streets())
            for (var i = 0; i + 1 < st.Nodes.Length; i++)
            {
                var s0 = st.Nodes[i] + (st.Crossing[i] ? 12f : 2f);
                var s1 = st.Nodes[i + 1] - (st.Crossing[i + 1] ? 12f : 2f);
                for (var s = s0; s < s1; s += 9f)
                {
                    if (rng.Randf() > 0.12f) continue;
                    var lane = rng.Randf() < 0.5f ? -1 : 1;
                    var t = lane * rng.RandfRange(1.6f, 5.6f);
                    var p = st.Vertical ? new Vector3(st.Axis + t, 0, s + rng.RandfRange(-2, 2)) : new Vector3(s + rng.RandfRange(-2, 2), 0, st.Axis + t);
                    if (Mathf.Abs(p.X) > Half - 4f || p.Z < -Half + 4f || p.Z > South - 4f) continue;
                    var yaw = (st.Vertical ? (lane > 0 ? 0 : Mathf.Pi) : (lane > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2)) + rng.RandfRange(-0.7f, 0.7f);
                    TryWreck(c, p, yaw, 3.2f);
                }
            }

        // Cratères d'impact sur la chaussée.
        var craters = 0;
        for (var tries = 0; tries < 80 && craters < 10; tries++)
        {
            var p = RandomStreetPoint(c, rng);
            if (p.Y > 0.05f || !r.Free(p, 4f)) continue;
            r.Take(p, 3f);
            Crater(c, p, rng.RandfRange(5f, 9f), hot);
            craters++;
        }

        // Fissures, flaques d'hémolymphe, brûlures d'acide, huile, cendres, poussière.
        for (var i = 0; i < 220; i++)
        {
            var p = RandomStreetPoint(c, rng);
            var roll = rng.Randf();
            var kind = roll < 0.3f ? Mats.Decal.Cracks : roll < 0.5f ? Mats.Decal.Ichor : roll < 0.58f ? Mats.Decal.Acid : roll < 0.66f ? Mats.Decal.Oil
                : roll < 0.8f ? Mats.Decal.Ash : roll < 0.9f ? Mats.Decal.Scorch : Mats.Decal.Dust;
            var size = kind switch { Mats.Decal.Cracks => rng.RandfRange(4f, 9f), Mats.Decal.Scorch or Mats.Decal.Ash => rng.RandfRange(5f, 11f), _ => rng.RandfRange(1.8f, 4.5f) };
            r.Decal(kind, p, size, rng.RandfRange(0, Mathf.Tau), p.Y + 0.02f + i * 0.0002f, rng.RandfRange(0.7f, 1.3f));
        }
        // Parvis : traces de combat autour de l'aire de départ (rien de solide).
        foreach (var (x, z, s, k) in new[] { (18f, -10f, 8f, Mats.Decal.Ash), (-12f, 30f, 7f, Mats.Decal.Scorch), (24f, 36f, 6f, Mats.Decal.Crater), (-20f, -6f, 5f, Mats.Decal.Ichor), (6f, -24f, 9f, Mats.Decal.Cracks) })
            r.Decal(k, new Vector3(x, 0, z), s, x * 0.3f, Walk + 0.025f);

        // Valises, matelas, canapés : traces d'exode sur les trottoirs et le parvis.
        for (var i = 0; i < 45; i++)
        {
            var p = RandomStreetPoint(c, rng);
            if (p.Y < 0.1f || !r.Free(p, 1.2f) || !Clear(c, p, 0.8f)) continue;
            var roll = rng.Randf();
            var yaw = rng.RandfRange(0, Mathf.Tau);
            if (roll < 0.55f) r.Inst(Props.Suitcase(), RuinKit.At(p, yaw, 0, roll < 0.2f ? Mathf.Pi / 2 : 0) with { Origin = p + Vector3.Up * (roll < 0.2f ? 0.35f : 0) }, false, 110f);
            else if (roll < 0.8f) r.Inst(Props.Mattress(), RuinKit.At(p, yaw), false, 140f);
            else
            {
                r.Inst(Props.Sofa(), RuinKit.At(p, yaw), true, 160f);
                r.BoxOnGround(p, yaw, new Vector3(1.9f, 0.85f, 0.85f));
                r.Take(p, 1.2f);
            }
        }

        Webs(c);
        foreach (var n in c.Vacant) NestGrowth(c, n);
        Litter(c, hot);

        // Panaches lointains : le reste de la ville brûle.
        r.DistantPlumes(new[]
        {
            new Vector3(-420, 30, -380), new Vector3(-80, 35, -520), new Vector3(310, 30, -430),
            new Vector3(520, 25, -90), new Vector3(-540, 25, 60), new Vector3(220, 10, 480), new Vector3(-300, 10, 420),
        });
    }

    // ───────────────────────── Remplacements et utilitaires ─────────────────────────

    /// <summary>Épave : <paramref name="pose"/> 0 sur roues, 1 sur le toit, 2 sur le flanc ; <paramref name="burn"/> 0, 1 fumante, 2 en flammes.</summary>
    private static void PlaceWreck(Ctx c, Vector3 p, float yaw, Props.VehicleType type, int v, int pose, int burn)
    {
        var r = c.Ruins;
        var size = Props.WreckSize(type, v);
        var mesh = type switch { Props.VehicleType.Van => Props.WreckVan(v % 2), Props.VehicleType.Truck => Props.WreckTruck(), _ => Props.WreckCar(v) };
        if (type != Props.VehicleType.Car) pose = 0;
        var b = new Basis(Vector3.Up, yaw);
        var origin = p;
        if (pose == 1) { b *= new Basis(Vector3.Back, Mathf.Pi); origin += Vector3.Up * (size.Y + 0.02f); }
        else if (pose == 2) { b *= new Basis(Vector3.Back, Mathf.Pi / 2); origin += new Basis(Vector3.Up, yaw) * new Vector3(size.Y * 0.5f, size.X * 0.5f, 0); }
        var xf = new Transform3D(b, origin);
        r.Inst(mesh, xf, true, 320f);
        r.Box(new Transform3D(b, xf * new Vector3(0, size.Y / 2, 0)), size);
        r.Take(p, size.Z * 0.5f);
        r.Decal(Mats.Decal.Scorch, p, size.Z * 1.5f, yaw, p.Y + 0.025f, 0.75f);
        if (type == Props.VehicleType.Truck) r.Decal(Mats.Decal.Oil, p + new Basis(Vector3.Up, yaw) * new Vector3(0.6f, 0, 1.5f), 3.2f, yaw, p.Y + 0.03f);
        if (burn == 2) r.Fire(p + new Basis(Vector3.Up, yaw) * new Vector3(0, size.Y * 0.45f, size.Z * 0.25f), type == Props.VehicleType.Car ? 1f : 1.4f);
        else if (burn == 1) r.Smolder(p + Vector3.Up * size.Y * 0.6f, 0.9f);
    }

    private static bool TryWreck(Ctx c, Vector3 p, float yaw, float radius, int burn = -1)
    {
        var r = c.Ruins;
        if (!r.Free(p, radius)) return false;
        var roll = r.Rng.Randf();
        var type = roll < 0.12f ? Props.VehicleType.Van : roll < 0.18f ? Props.VehicleType.Truck : Props.VehicleType.Car;
        var pose = r.Rng.Randf() switch { < 0.12f => 1, < 0.2f => 2, _ => 0 };
        var b = burn >= 0 ? burn : r.Rng.Randf() < 0.04f ? 1 : 0;
        PlaceWreck(c, p, yaw, type, r.Rng.RandiRange(0, 3), pose, b);
        return true;
    }

    /// <summary>Point au hasard sur une rue, un trottoir ou le parvis (hors aire de départ).</summary>
    private static Vector3 RandomStreetPoint(Ctx c, RandomNumberGenerator rng)
    {
        for (var i = 0; i < 10; i++)
        {
            Vector3 p;
            var roll = rng.Randf();
            if (roll < 0.4f) p = OnGround(c, Xs[rng.RandiRange(0, Xs.Length - 1)] + rng.RandfRange(-10.5f, 10.5f), rng.RandfRange(-Half + 5, South - 12));
            else if (roll < 0.8f) p = OnGround(c, rng.RandfRange(-Half + 5, Half - 5), Zs[rng.RandiRange(0, Zs.Length - 1)] + rng.RandfRange(-10.5f, 10.5f));
            else p = OnGround(c, rng.RandfRange(Plaza.Position.X + 3, Plaza.End.X - 3), rng.RandfRange(Plaza.Position.Y + 3, Plaza.End.Y - 3));
            if (Mathf.Abs(p.X) < 36 && p.Z > -34 && p.Z < 44) continue;
            if (p.Y < -0.5f) continue; // trémie du métro
            return p;
        }
        return OnGround(c, -100, -70);
    }

    /// <summary>Tas de gravats contre une façade (débordant sur le trottoir), avec poussière.</summary>
    private static void RubbleAt(Ctx c, Building b, bool heavy, List<(Vector3, float, float)> hot)
    {
        var r = c.Ruins;
        var side = (int)(RuinKit.Hash(b.Id, 5, 2) * 4);
        var s = b.Size;
        var (n, along, len) = side switch
        {
            0 => (Vector3.Back, Vector3.Right, s.X),
            1 => (Vector3.Forward, Vector3.Right, s.X),
            2 => (Vector3.Right, Vector3.Back, s.Z),
            _ => (Vector3.Left, Vector3.Back, s.Z),
        };
        var half = side < 2 ? s.Z / 2 : s.X / 2;
        var w = Mathf.Min(len * 0.8f, (heavy ? 6f : 3.5f) + RuinKit.Hash(b.Id, 6, 2) * 3f);
        var depth = heavy ? 3.4f + RuinKit.Hash(b.Id, 7, 2) * 1.2f : 2.2f + RuinKit.Hash(b.Id, 7, 2) * 1f;
        var hgt = heavy ? 1.5f + RuinKit.Hash(b.Id, 8, 2) * 1.0f : 0.8f + RuinKit.Hash(b.Id, 8, 2) * 0.6f;
        var off = (RuinKit.Hash(b.Id, 9, 2) - 0.5f) * Mathf.Max(0, len - w) * 0.8f;
        var ctr = new Vector3(b.Position.X, 0, b.Position.Z) + n * (half + depth * 0.32f) + along * off;
        ctr = OnGround(c, ctr.X, ctr.Z);
        if (!Clear(c, ctr, -0.2f)) return; // pas sur un escalier de secours ni dans un immeuble voisin
        if (Mathf.Abs(ctr.X) < 36 && ctr.Z > -34 && ctr.Z < 44) return;
        var yaw = Mathf.Atan2(n.X, n.Z);
        var tint = RuinKit.Hash(b.Id, 10, 2) < 0.35f ? new Color(0.5f, 0.3f, 0.24f) : new Color(0.6f, 0.58f, 0.54f);
        var pts = Props.RubblePile(r.Kit(RuinKit.Layer.Solid, ctr), ctr, yaw, new Vector3(w, hgt, depth), b.Id * 7 + 1, tint, tint.R > tint.G + 0.1f);
        r.Convex(pts);
        r.Take(ctr, Mathf.Max(w, depth) * 0.5f);
        r.Decal(Mats.Decal.Dust, ctr, Mathf.Max(w, depth) * 1.7f, yaw, ctr.Y + 0.02f);
        hot.Add((ctr, Mathf.Max(w, depth) * 1.1f, 0.45f));
        if (heavy && RuinKit.Hash(b.Id, 11, 2) < 0.2f) r.Fire(ctr + Vector3.Up * hgt * 0.6f, 1.3f);
        else if (heavy && RuinKit.Hash(b.Id, 12, 2) < 0.15f) r.Smolder(ctr + Vector3.Up * hgt, 1.2f);
    }

    private static void Crater(Ctx c, Vector3 p, float size, List<(Vector3, float, float)> hot)
    {
        var r = c.Ruins;
        var yaw = r.Rng.RandfRange(0, Mathf.Tau);
        r.Decal(Mats.Decal.Crater, p, size, yaw, p.Y + 0.03f);
        r.Decal(Mats.Decal.Scorch, p, size * 1.9f, yaw + 1.3f, p.Y + 0.022f);
        var k = r.Kit(RuinKit.Layer.Solid, p);
        var d = Mats.Detail();
        var n = 12 + (int)(size * 1.5f);
        for (var i = 0; i < n; i++)
        {
            var a = i * Mathf.Tau / n + r.Rng.RandfRange(-0.2f, 0.2f);
            var rad = size * r.Rng.RandfRange(0.22f, 0.32f);
            var q = p + new Vector3(Mathf.Cos(a) * rad, 0, Mathf.Sin(a) * rad);
            var s = r.Rng.RandfRange(0.3f, 0.75f);
            k.Col = new Color(0.12f, 0.12f, 0.12f).Lerp(new Color(0.3f, 0.27f, 0.24f), r.Rng.Randf());
            k.Box(d, q + Vector3.Up * 0.08f, new Vector3(s, 0.14f, s * 0.7f), new Basis(Vector3.Up, -a) * new Basis(Vector3.Back, r.Rng.RandfRange(0.25f, 0.7f)));
        }
        hot.Add((p, size * 1.2f, 0.5f));
    }

    // ───────────────────────── Scènes ─────────────────────────

    /// <summary>
    /// Terrain dévasté (immeuble pulvérisé) où la colonie a creusé son nid : carcasse de murs sur un côté, buttes de
    /// gravats en couronne, centre dégagé, brûlures et cratère, incendie ou fumée.
    /// </summary>
    private static void DevastatedLot(Ctx c, Vector3 center, List<(Vector3, float, float)> hot)
    {
        var r = c.Ruins;
        var seed = (int)(Mathf.Abs(center.X) * 7 + Mathf.Abs(center.Z) * 3);
        var block = new Rect2(center.X - 22f, center.Z - 22f, 44f, 44f);
        foreach (var b in c.Blocks)
            if (b.HasPoint(new Vector2(center.X, center.Z))) block = b;
        // Carcasse d'un immeuble éventré le long du côté le plus éloigné du parvis.
        var toPlaza = -new Vector2(center.X, center.Z).Normalized();
        var shellSide = Mathf.Abs(toPlaza.X) > Mathf.Abs(toPlaza.Y) ? (toPlaza.X > 0 ? 3 : 2) : (toPlaza.Y > 0 ? 1 : 0);
        var sw = shellSide < 2 ? block.Size.X - 6f : 10f;
        var sd = shellSide < 2 ? 10f : block.Size.Y - 6f;
        var sc = shellSide switch
        {
            0 => new Vector3(center.X, Walk, block.End.Y - 3f - sd / 2),
            1 => new Vector3(center.X, Walk, block.Position.Y + 3f + sd / 2),
            2 => new Vector3(block.End.X - 3f - sw / 2, Walk, center.Z),
            _ => new Vector3(block.Position.X + 3f + sw / 2, Walk, center.Z),
        };
        var height = 3.2f * (4 + (int)(RuinKit.Hash(seed, 1, 96) * 4)) + 0.6f;
        var wall = new Color(0.48f, 0.47f, 0.44f);
        var boxes = new List<(Transform3D, Vector3)>();
        Props.RuinedShell(r.Kit(RuinKit.Layer.Solid, sc), sc, new Vector2(sw, sd), height, seed, wall, boxes);
        foreach (var (xf, s) in boxes) r.Box(xf, s);
        var shellRect = new Rect2(sc.X - sw / 2, sc.Z - sd / 2, sw, sd);
        c.KeepOut.Add(shellRect.Grow(0.5f));
        // Buttes de gravats en couronne autour du centre (le nid reste dégagé).
        for (var i = 0; i < 6; i++)
        {
            var a = i * Mathf.Tau / 6 + RuinKit.Hash(seed, i, 97);
            var rad = 15f + RuinKit.Hash(seed, i, 98) * 3f;
            var p = new Vector3(center.X + Mathf.Cos(a) * rad, Walk, center.Z + Mathf.Sin(a) * rad);
            if (!block.Grow(-2f).HasPoint(new Vector2(p.X, p.Z)) || shellRect.Grow(1f).HasPoint(new Vector2(p.X, p.Z))) continue;
            var ps = new Vector3(5f + RuinKit.Hash(seed, i, 99) * 4f, 1.4f + RuinKit.Hash(seed, i, 100) * 1.6f, 4f + RuinKit.Hash(seed, i, 101) * 3f);
            r.Convex(Props.RubblePile(r.Kit(RuinKit.Layer.Solid, p), p, a, ps, seed + i, wall, i % 3 == 0));
        }
        r.Decal(Mats.Decal.Crater, center, 18f, seed * 0.1f, Walk + 0.03f);
        r.Decal(Mats.Decal.Scorch, center, 32f, seed * 0.3f, Walk + 0.022f);
        r.Decal(Mats.Decal.Dust, center, 40f, seed * 0.7f, Walk + 0.018f);
        if (RuinKit.Hash(seed, 2, 96) < 0.5f) r.Fire(sc + new Vector3(0, 1.2f, 0), 2f, r.Lights < 3);
        else r.Smolder(sc + Vector3.Up * 2f, 1.8f);
        hot.Add((center, 26f, 0.5f));
    }

    /// <summary>Carambolage sur la rue z = -130, entre l'avenue centrale et la rue x = 70.</summary>
    private static void Pileup(Ctx c, List<(Vector3, float, float)> hot)
    {
        const float z = -130f;
        var spots = new (float x, float dz, float yaw, Props.VehicleType t, int v, int pose, int burn)[]
        {
            (24f, -2.6f, -1.2f, Props.VehicleType.Car, 0, 0, 2),
            (29f, -0.4f, -1.9f, Props.VehicleType.Car, 2, 0, 0),
            (33.5f, 2.6f, 1.1f, Props.VehicleType.Van, 0, 0, 1),
            (38.5f, -3.0f, -1.45f, Props.VehicleType.Car, 1, 1, 0),
            (43f, 1.8f, 2.0f, Props.VehicleType.Car, 3, 0, 0),
            (47.5f, -1.5f, -1.75f, Props.VehicleType.Car, 0, 2, 0),
            (53f, 3.2f, 1.45f, Props.VehicleType.Truck, 0, 0, 0),
        };
        foreach (var s in spots) PlaceWreck(c, new Vector3(s.x, 0, z + s.dz), s.yaw, s.t, s.v, s.pose, s.burn);
        hot.Add((new Vector3(38, 0, z), 18f, 0.35f));
        c.Ruins.Decal(Mats.Decal.Oil, new Vector3(36, 0, z + 0.5f), 6f, 0.4f, 0.04f, 1.6f);
    }

    /// <summary>Bus encastré dans l'angle d'un îlot au carrefour x = 130 / z = -70.</summary>
    private static void BusCrash(Ctx c, List<(Vector3, float, float)> hot)
    {
        var r = c.Ruins;
        var p = new Vector3(136.5f, 0, -60.5f);
        var fwd = new Vector3(0.38f, 0, 1f).Normalized();
        var yaw = Mathf.Atan2(fwd.X, fwd.Z);
        var b = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Back, 0.04f);
        r.Inst(Props.WreckBus(), new Transform3D(b, p), true, 360f);
        r.Box(new Transform3D(b, p + Vector3.Up * 1.55f + fwd * -0.3f), new Vector3(2.5f, 3.1f, 11.2f));
        r.Take(p, 6f);
        r.Decal(Mats.Decal.Scorch, p, 15f, yaw, 0.025f, 0.6f);
        var front = OnGround(c, p.X + fwd.X * 6.6f, p.Z + fwd.Z * 6.6f);
        r.Convex(Props.RubblePile(r.Kit(RuinKit.Layer.Solid, front), front, yaw, new Vector3(5.5f, 1.4f, 3.2f), 991, new Color(0.6f, 0.58f, 0.54f), false));
        r.Fire(p + fwd * 3.5f + Vector3.Up * 1.4f, 1.6f);
        hot.Add((front, 9f, 0.6f));
    }

    /// <summary>
    /// Barrage militaire abandonné à l'entrée nord du parvis : séparateurs en chicane, nids de sacs de sable,
    /// gabions, barbelés, camion bâché, fûts, brasero.
    /// </summary>
    private static void Checkpoint(Ctx c, Vector3 ctr, List<(Vector3, float, float)> hot)
    {
        var r = c.Ruins;
        // Séparateurs en travers de l'avenue (passage en chicane).
        foreach (var (x, dz, yaw) in new[] { (-5.5f, -4f, 0.05f), (-2.4f, -4.2f, -0.08f), (2.6f, -1f, 0.1f), (5.6f, -0.6f, 0.5f) })
        {
            var p = new Vector3(ctr.X + x, 0, ctr.Z + dz);
            var v = RuinKit.Hash(x, dz, 91) < 0.35f ? 1 : 0;
            r.Inst(Props.Barrier(v), RuinKit.At(p, yaw), true, 240f);
            r.BoxOnGround(p, yaw, Props.BarrierSize);
            r.Take(p, 1.5f);
        }
        // Nids de sacs de sable sur le parvis, de part et d'autre de l'entrée.
        foreach (var sx in new[] { -1f, 1f })
        {
            var cc = new Vector3(ctr.X + sx * 11f, Walk, ctr.Z + 8f);
            var k = r.Kit(RuinKit.Layer.Solid, cc);
            var a = cc + new Vector3(sx * 1.8f, 0, -1.8f);
            var b1 = cc + new Vector3(-sx * 1.6f, 0, -1.8f);
            var b2 = cc + new Vector3(sx * 1.8f, 0, 1.6f);
            foreach (var (p0, p1) in new[] { (b1, a), (a, b2) })
            {
                var (bc, bs, byaw) = Props.SandbagWall(k, p0, p1, 6, (int)(p0.X * 3 + p0.Z));
                r.Box(new Transform3D(new Basis(Vector3.Up, byaw), bc), bs);
            }
            r.Take(cc, 2.6f);
        }
        // Barbelés et hérissons en avant, sur la chaussée.
        var kit = r.Kit(RuinKit.Layer.Solid, ctr);
        Props.RazorWire(kit, new Vector3(ctr.X - 7f, 0, ctr.Z - 10f), new Vector3(ctr.X - 1f, 0, ctr.Z - 10.5f));
        Props.RazorWire(kit, new Vector3(ctr.X + 2f, 0, ctr.Z - 10.3f), new Vector3(ctr.X + 7f, 0, ctr.Z - 9.6f));
        for (var i = 0; i < 3; i++)
        {
            var p = new Vector3(ctr.X - 6f + i * 6f, 0, ctr.Z - 14f + i * 0.6f);
            r.Inst(Props.Hedgehog(), RuinKit.At(p, i * 0.7f), true, 220f);
            r.BoxOnGround(p, i * 0.7f, new Vector3(1.3f, 1.2f, 1.3f));
            r.Take(p, 1f);
        }
        // Camion bâché abandonné sur la rue z = -70, fûts, gabions.
        var tp = new Vector3(ctr.X + 16f, 0, ctr.Z - 2.5f);
        r.Inst(Props.ArmyTruck(), RuinKit.At(tp, Mathf.Pi / 2 + 0.06f), true, 320f);
        r.Box(new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2 + 0.06f), tp + Vector3.Up * (Props.ArmyTruckSize.Y / 2)), Props.ArmyTruckSize);
        r.Take(tp, 4.2f);
        r.Decal(Mats.Decal.Oil, tp, 3.5f, 0.3f, 0.03f);
        for (var i = 0; i < 4; i++)
        {
            var p = new Vector3(ctr.X - 16f + i * 1.12f, 0, ctr.Z + 1.5f);
            r.Inst(Props.Hesco(), RuinKit.At(p, 0), true, 260f);
            r.BoxOnGround(p, 0, new Vector3(1.1f, 1.3f, 1.1f));
        }
        r.Take(new Vector3(ctr.X - 14.3f, 0, ctr.Z + 1.5f), 2.4f);
        for (var i = 0; i < 3; i++)
        {
            var p = new Vector3(ctr.X + 9.5f, Walk, ctr.Z + 9f + i * 0.65f);
            r.Inst(Props.Drum(0), RuinKit.At(p, i), true, 160f);
            r.BoxOnGround(p, 0, new Vector3(0.6f, 0.88f, 0.6f));
        }
        var bp = new Vector3(ctr.X - 8.5f, Walk, ctr.Z + 12f);
        r.Inst(Props.Drum(1), RuinKit.At(bp, 0), true, 160f);
        r.BoxOnGround(bp, 0, new Vector3(0.6f, 0.88f, 0.6f));
        r.Fire(bp + Vector3.Up * 0.85f, 0.45f);
        r.Decal(Mats.Decal.Scorch, new Vector3(ctr.X + 3, 0, ctr.Z - 3), 7f, 0.8f, 0.026f);
        r.Decal(Mats.Decal.Ichor, new Vector3(ctr.X - 4, 0, ctr.Z - 8), 4.5f, 2.1f, 0.03f);
        hot.Add((ctr, 18f, 0.25f));
    }

    /// <summary>
    /// Mât d'éclairage militaire sur remorque (groupe électrogène) : châssis, stabilisateurs, mât télescopique,
    /// rampe de quatre projecteurs orientée vers <paramref name="target"/> ; allumé la nuit (projecteur sans ombres).
    /// </summary>
    private static void FloodLight(Ctx c, Vector3 p, Vector3 target)
    {
        var r = c.Ruins;
        if (!r.TryTake(p, 2f)) return;
        var dir = new Vector3(target.X - p.X, 0, target.Z - p.Z).Normalized();
        var yaw = Mathf.Atan2(-dir.X, -dir.Z);           // -Z local vers la cible
        var b = new Basis(Vector3.Up, yaw);
        var k = r.Kit(RuinKit.Layer.Solid, p);
        var d = Mats.Detail();
        var metal = Mats.DetailMetal();
        k.Col = new Color(0.27f, 0.29f, 0.22f);
        k.Box(d, p + Vector3.Up * 0.75f, new Vector3(1.5f, 0.9f, 2.6f), b);
        k.Col = new Color(0.08f, 0.08f, 0.08f);
        foreach (var sz in new[] { -0.6f, 0.6f })
            k.Cylinder(d, p + b * new Vector3(-0.8f, 0.36f, sz), 0.34f, 0.34f, 0.22f, 10, true, b * new Basis(Vector3.Back, Mathf.Pi / 2));
        k.Col = new Color(0.22f, 0.23f, 0.22f);
        foreach (var (sx, sz) in new[] { (-1.3f, -1.4f), (1.3f, -1.4f), (-1.3f, 1.4f), (1.3f, 1.4f) })
            k.Tube(metal, p + b * new Vector3(sx * 0.5f, 0.5f, sz * 0.7f), p + b * new Vector3(sx, 0.02f, sz), 0.04f, 4);
        k.Tube(metal, p + b * new Vector3(0, 1.2f, 0.9f), p + b * new Vector3(0, 7.2f, 0.9f), 0.09f, 6);
        var head = p + b * new Vector3(0, 7.4f, 0.9f);
        k.Box(metal, head, new Vector3(1.9f, 0.18f, 0.2f), b);
        k.Col = c.Night ? new Color(1f, 0.97f, 0.9f) : new Color(0.2f, 0.21f, 0.22f);
        foreach (var sx in new[] { -0.7f, -0.23f, 0.23f, 0.7f })
            k.Box(c.Night ? Mats.Lamps() : metal, head + b * new Vector3(sx, 0.3f, -0.08f), new Vector3(0.38f, 0.38f, 0.16f), b * new Basis(Vector3.Right, 0.5f));
        r.BoxOnGround(p, yaw, new Vector3(1.5f, 1.2f, 2.6f));
        r.Box(new Transform3D(b, p + b * new Vector3(0, 4.2f, 0.9f)), new Vector3(0.2f, 6f, 0.2f));
        if (c.Night)
            c.Root.AddChild(new SpotLight3D
            {
                Position = head + Vector3.Up * 0.2f,
                Basis = b * new Basis(Vector3.Right, -0.55f),
                LightColor = new Color(0.86f, 0.9f, 1f), LightEnergy = 7f, SpotRange = 60f, SpotAngle = 38f, SpotAttenuation = 0.8f, ShadowEnabled = false,
            });
    }

    /// <summary>Second barrage, plus léger, au carrefour x = -130 / z = 60 : séparateurs, sacs de sable, camion.</summary>
    private static void Checkpoint2(Ctx c, Vector3 ctr, List<(Vector3, float, float)> hot)
    {
        var r = c.Ruins;
        foreach (var (dx, dz, yaw) in new[] { (-4.3f, -13f, Mathf.Pi / 2), (-1.3f, -13f, Mathf.Pi / 2 + 0.05f), (2.6f, -17f, Mathf.Pi / 2 - 0.08f), (5f, 13f, Mathf.Pi / 2 + 0.4f), (1.2f, 14.2f, Mathf.Pi / 2) })
        {
            var p = new Vector3(ctr.X + dx, 0, ctr.Z + dz);
            r.Inst(Props.Barrier(RuinKit.Hash(dx, dz, 92) < 0.4f ? 1 : 0), RuinKit.At(p, yaw), true, 240f);
            r.BoxOnGround(p, yaw, Props.BarrierSize);
            r.Take(p, 1.5f);
        }
        var cc = new Vector3(ctr.X + 3.4f, 0, ctr.Z + 3.4f);
        var k = r.Kit(RuinKit.Layer.Solid, cc);
        foreach (var (p0, p1) in new[] { (cc + new Vector3(-1.5f, 0, 1.7f), cc + new Vector3(1.7f, 0, 1.7f)), (cc + new Vector3(1.7f, 0, 1.7f), cc + new Vector3(1.7f, 0, -1.5f)) })
        {
            var (bc, bs, byaw) = Props.SandbagWall(k, p0, p1, 6, (int)(p0.X * 3 + p0.Z));
            r.Box(new Transform3D(new Basis(Vector3.Up, byaw), bc), bs);
        }
        r.Take(cc, 2.4f);
        var tp = new Vector3(ctr.X - 15f, 0, ctr.Z - 3.6f);
        r.Inst(Props.ArmyTruck(), RuinKit.At(tp, -Mathf.Pi / 2 + 0.1f), true, 320f);
        r.Box(new Transform3D(new Basis(Vector3.Up, -Mathf.Pi / 2 + 0.1f), tp + Vector3.Up * (Props.ArmyTruckSize.Y / 2)), Props.ArmyTruckSize);
        r.Take(tp, 4.2f);
        r.Smolder(tp + Vector3.Up * 2.5f, 1f);
        hot.Add((ctr, 14f, 0.25f));
    }

    /// <summary>Toiles géantes tendues entre deux tours qui se font face au-dessus d'une rue.</summary>
    private static void Webs(Ctx c)
    {
        var r = c.Ruins;
        var made = 0;
        foreach (var a in c.Res.Buildings)
        {
            if (made >= 5 || a.Position.Y > 1f || a.Size.Y < 18f || RuinKit.Hash(a.Id, 21, 3) > 0.55f) continue;
            var aMaxX = a.Position.X + a.Size.X / 2;
            foreach (var b in c.Res.Buildings)
            {
                if (b == a || b.Position.Y > 1f || b.Size.Y < 14f) continue;
                var bMinX = b.Position.X - b.Size.X / 2;
                var gap = bMinX - aMaxX;
                if (gap < 14f || gap > 30f) continue;
                var z0 = Mathf.Max(a.Position.Z - a.Size.Z / 2, b.Position.Z - b.Size.Z / 2) + 2f;
                var z1 = Mathf.Min(a.Position.Z + a.Size.Z / 2, b.Position.Z + b.Size.Z / 2) - 2f;
                if (z1 - z0 < 8f) continue;
                var zm = (z0 + z1) / 2;
                if (Mathf.Abs(zm) < 50 && Mathf.Abs((aMaxX + bMinX) / 2) < 50) continue;
                var ha = Mathf.Min(a.Size.Y - 2f, 26f);
                var hb = Mathf.Min(b.Size.Y - 2f, 24f);
                var anchors = new[]
                {
                    new Vector3(aMaxX + 0.05f, ha, zm - 5f), new Vector3(aMaxX + 0.05f, ha * 0.55f, zm + 4.5f),
                    new Vector3(bMinX - 0.05f, hb * 0.6f, zm + 5f), new Vector3(bMinX - 0.05f, hb, zm - 3.5f),
                };
                Props.Web(r.Kit(RuinKit.Layer.Organic, anchors[0]), anchors, a.Id);
                var foot = OnGround(c, aMaxX + 1.2f, zm);
                Props.ResinBlob(r.Kit(RuinKit.Layer.Organic, foot), foot + Vector3.Up * 0.2f, new Vector3(1.6f, 0.9f, 2.4f), a.Id);
                made++;
                break;
            }
        }
    }

    /// <summary>Grappes d'œufs, flaques d'acide et d'hémolymphe autour d'un nid.</summary>
    private static void NestGrowth(Ctx c, Vector3 nest)
    {
        var r = c.Ruins;
        var seed = (int)(Mathf.Abs(nest.X) * 3 + Mathf.Abs(nest.Z));
        for (var i = 0; i < 4; i++)
        {
            var a = i * Mathf.Tau / 4 + RuinKit.Hash(seed, i, 2);
            var p = new Vector3(nest.X + Mathf.Cos(a) * (10f + i), Walk, nest.Z + Mathf.Sin(a) * (10f + i));
            Props.EggCluster(r.Kit(RuinKit.Layer.Organic, p), p, 0.9f + RuinKit.Hash(seed, i, 3) * 0.6f, seed + i * 7);
            r.Decal(Mats.Decal.Acid, p, 5f, a, p.Y + 0.028f);
        }
        for (var i = 0; i < 6; i++)
        {
            var a = RuinKit.Hash(seed, i, 4) * Mathf.Tau;
            var d = 4f + RuinKit.Hash(seed, i, 5) * 14f;
            var p = new Vector3(nest.X + Mathf.Cos(a) * d, Walk, nest.Z + Mathf.Sin(a) * d);
            r.Decal(Mats.Decal.Ichor, p, 2.5f + RuinKit.Hash(seed, i, 6) * 3f, a, p.Y + 0.026f + i * 0.0003f);
        }
    }

    /// <summary>Débris épars (MultiMesh, sans collision ni ombres) : denses près des gravats, cratères et pieds de tours.</summary>
    private static void Litter(Ctx c, List<(Vector3 p, float rad, float dens)> hot)
    {
        var r = c.Ruins;
        const float step = 2.6f;
        var idx = 0;
        for (var z = -Half + 2; z < South - 4; z += step)
        for (var x = -Half + 2; x < Half - 2; x += step, idx++)
        {
            var jx = x + (RuinKit.Hash(idx, 1, 4) - 0.5f) * step;
            var jz = z + (RuinKit.Hash(idx, 2, 4) - 0.5f) * step;
            var inside = false;
            foreach (var b in c.KeepOut)
                if (jx > b.Position.X + 0.2f && jx < b.End.X - 0.2f && jz > b.Position.Y + 0.2f && jz < b.End.Y - 0.2f) { inside = true; break; }
            if (inside) continue;
            var dens = 0.06f;
            foreach (var (hp, hr, hd) in hot)
            {
                var dd = new Vector2(jx - hp.X, jz - hp.Z).Length();
                if (dd < hr) dens = Mathf.Max(dens, hd * (1 - dd / hr) + 0.06f);
            }
            if (Mathf.Abs(jx) < 30 && jz > -30 && jz < 40) dens *= 0.3f;   // aire de départ plus dégagée
            if (RuinKit.Hash(idx, 3, 4) > dens) continue;
            var p = OnGround(c, jx, jz);
            if (p.Y < -0.5f || (p.Y < 0.1f && jz > HighwayZ + 10f && jx < RampJoin && jx > RampX0)) continue;
            var kind = (int)(RuinKit.Hash(idx, 4, 4) * Props.DebrisKinds);
            if (kind == 7 && RuinKit.Hash(idx, 5, 4) < 0.7f) kind = 5;
            var xf = RuinKit.At(p, RuinKit.Hash(idx, 6, 4) * Mathf.Tau, 0, 0, 0.75f + RuinKit.Hash(idx, 7, 4) * 0.6f);
            r.Inst(Props.Debris(kind), xf, false, kind == 5 || kind == 9 ? 60f : 95f);
        }
    }
}
