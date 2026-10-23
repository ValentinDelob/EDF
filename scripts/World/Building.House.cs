using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Bâtiments de banlieue (<see cref="BuildingKind.House"/>) : pavillons à toit en pente, garages,
/// église et clocher, gymnase, supérette. Toute la géométrie (fenêtres, volets, toiture, cheminée)
/// est fusionnée dans le maillage unique du bâtiment pour s'effondrer avec lui.
/// Repère local : centre de l'empreinte au sol, murs de 0 à Size.Y, toit au-dessus.
/// </summary>
public partial class Building
{
    /// <summary>Ajoute des boîtes dans le repère d'une façade (a = le long, y = hauteur, n = saillie).</summary>
    private readonly struct Facade
    {
        private readonly MeshKit _k;
        public readonly Side S;
        private readonly Vector3 _ax, _an;

        public Facade(MeshKit k, Side s)
        {
            _k = k;
            S = s;
            _ax = Abs(s.Along);
            _an = Abs(s.Normal);
        }

        /// <summary>Couleur de sommet courante du MeshKit sous-jacent.</summary>
        public void Col(Color c) => _k.Col = c;

        /// <summary>Les boîtes suivantes comptent dans la collision (les reliefs de quelques centimètres n'y entrent pas).</summary>
        public void Solid(bool s) => _k.Solid = s;

        public Vector3 At(float a, float y, float n) => S.Start + S.Along * a + Vector3.Up * y + S.Normal * n;

        public void Box(Material m, float a, float y, float n, float sa, float sy, float sn) =>
            _k.Box(m, At(a, y, n), _ax * sa + Vector3.Up * sy + _an * sn);
    }

    /// <summary>Quad dont la face visible est tournée vers <paramref name="n"/> (l'ordre des coins est inversé si besoin).</summary>
    private static void QuadN(MeshKit k, Vector3 n, Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector2 ua = default, Vector2 ub = default, Vector2 uc = default, Vector2 ud = default)
    {
        if ((b - a).Cross(c - a).Dot(n) >= 0 || (b - a).Cross(c - a).LengthSquared() < 1e-8f && (c - a).Cross(d - a).Dot(n) >= 0)
            k.Quad(m, a, b, c, d, ua, ub, uc, ud);
        else
            k.Quad(m, a, d, c, b, ua, ud, uc, ub);
    }

    private static Material Pane() => Mats.Painted(new Color(0.05f, 0.06f, 0.075f), 0.06f, 0.3f);

    private static ArrayMesh BuildHouseMesh(Vector3 s, BuildingStyle st, RandomNumberGenerator rng, out Shape3D roofShape)
    {
        var kit = new MeshKit { Record = true };
        roofShape = null;
        switch (st.Use)
        {
            case HouseUse.Garage: BuildGarage(kit, s, st, rng); break;
            case HouseUse.Church: roofShape = BuildChurch(kit, s, st, rng); break;
            case HouseUse.Belfry: roofShape = BuildBelfry(kit, s, st, rng); break;
            case HouseUse.Gym: roofShape = BuildGym(kit, s, st, rng); break;
            case HouseUse.Market: BuildMarket(kit, s, st, rng); break;
            default: roofShape = BuildHome(kit, s, st, rng); break; // Home, Hall
        }
        return kit.Commit();
    }

    /// <summary>Quatre murs pleins (enduit ou pierre teintés par sommet).</summary>
    private static void Walls(MeshKit kit, Material m, Vector3 s, float y0, float y1, Color c)
    {
        kit.Col = c;
        foreach (var sd in Sides(s.X / 2, s.Z / 2))
        {
            var a = sd.Start + Vector3.Up * y0;
            var b = sd.Start + sd.Along * sd.Length + Vector3.Up * y0;
            kit.Quad(m, a, b, b with { Y = y1 }, a with { Y = y1 });
        }
    }

    // ───────────────────────── Pavillon ─────────────────────────

    private static Shape3D BuildHome(MeshKit kit, Vector3 s, BuildingStyle st, RandomNumberGenerator rng)
    {
        var d = Mats.Detail();
        var h = s.Y;
        var sides = Sides(s.X / 2, s.Z / 2);
        var floors = h > 7.5f ? 3 : h > 4.2f ? 2 : 1;
        const float floor0 = 0.3f;
        var fh = (h - floor0) / floors;
        var hall = st.Use == HouseUse.Hall;
        var winH = Mathf.Clamp(fh - 1.45f, 1.3f, 1.9f);

        Walls(kit, d, s, 0, h, st.WallColor);
        // Soubassement plus sombre.
        kit.Col = st.WallColor.Darkened(0.32f);
        kit.Ring(d, s.X / 2, s.Z / 2, 0f, 0.45f, 0.08f, 0.04f);
        // Bandeau d'étage et chaînages d'angle (maisons de caractère).
        var stone = st.WallColor.Lightened(0.25f).Lerp(new Color(0.86f, 0.83f, 0.76f), 0.6f);
        if (floors == 2 && rng.Randf() < 0.45f)
        {
            kit.Col = stone;
            kit.Ring(d, s.X / 2, s.Z / 2, floor0 + fh - 0.12f, floor0 + fh + 0.06f, 0.06f, 0.03f);
        }
        if (rng.Randf() < 0.3f)
        {
            kit.Col = stone;
            for (var y = 0.45f; y < h - 0.2f; y += 0.5f)
            {
                var alt = Mathf.RoundToInt(y / 0.5f) % 2 == 0;
                foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    var c = new Vector3(sx * s.X / 2, y + 0.22f, sz * s.Z / 2);
                    var lx = alt ? 0.6f : 0.32f;
                    var lz = alt ? 0.32f : 0.6f;
                    kit.Box(d, c - new Vector3(sx * (lx / 2 - 0.03f), 0, sz * (lz / 2 - 0.03f)), new Vector3(lx, 0.44f, lz));
                }
            }
        }

        // Toiture : faîtage parallèle à la façade principale (mur gouttereau sur rue).
        var ridgeX = st.Facing <= 1;
        var shutters = rng.Randf() < 0.6f;
        var frame = rng.Randf() < 0.75f ? new Color(0.93f, 0.93f, 0.91f) : new Color(0.32f, 0.3f, 0.29f);
        var lit = st.Night ? 0.4f : 0f;

        for (var si = 0; si < 4; si++)
        {
            var sd = sides[si];
            var f = new Facade(kit, sd);
            var front = si == st.Facing;
            var back = si == (st.Facing ^ 1);
            var gableSide = ridgeX ? si >= 2 : si <= 1;
            var n = Mathf.Max(1, Mathf.FloorToInt((sd.Length - 1.2f) / 2.7f));
            if (hall && n % 2 == 0) n--;   // façade symétrique, porte au centre
            var step = sd.Length / n;
            var door = front ? hall ? n / 2 : rng.RandiRange(0, n - 1) : -1;
            var bay = back && n >= 2 && !hall ? rng.RandiRange(0, n - 1) : -1;
            var shop = front && st.Shops;
            if (shop) ShopFront(kit, f, st, rng, floor0 + fh);
            for (var fl = 0; fl < floors; fl++)
            {
                var fy = floor0 + fl * fh;
                if (fl == 0 && shop) continue;
                for (var i = 0; i < n; i++)
                {
                    var a = (i + 0.5f) * step;
                    if (fl == 0 && i == door)
                    {
                        HomeDoor(f, a, st, frame, !hall);
                        if (hall)
                        {
                            kit.Col = new Color(0.15f, 0.15f, 0.17f);
                            PixelText(kit, f, "MAIRIE", a, floor0 + fh - 0.42f, 0.06f, d, 0.1f);
                            // Drapeau tricolore sur hampe inclinée au-dessus de l'entrée.
                            var p0 = f.At(a, floor0 + fh + 0.4f, 0.05f);
                            var p1 = p0 + f.S.Normal * 1.6f + Vector3.Up * 1.1f;
                            kit.Col = new Color(0.8f, 0.8f, 0.78f);
                            kit.Tube(Mats.DetailMetal(), p0, p1, 0.03f, 5);
                            var cols = new[] { new Color(0.1f, 0.2f, 0.55f), new Color(0.95f, 0.95f, 0.93f), new Color(0.8f, 0.12f, 0.12f) };
                            for (var k = 0; k < 3; k++)
                            {
                                kit.Col = cols[k];
                                kit.Box(d, p1 + f.S.Normal * (-0.15f - k * 0.32f) + Vector3.Down * 0.55f + f.S.Along * 0.03f, Abs(f.S.Normal) * 0.32f + Vector3.Up * 0.95f + Abs(f.S.Along) * 0.02f);
                            }
                        }
                        continue;
                    }
                    if (fl == 0 && i == bay)
                    {
                        HomeWindow(f, a, fy + 0.05f, Mathf.Min(2.4f, step - 0.5f), 2.15f, frame, st, false, rng.Randf() < lit, rng, true);
                        continue;
                    }
                    // Murs pignons et latéraux : moins de fenêtres.
                    if (!front && !back && rng.Randf() < (hall ? 0.2f : 0.45f)) continue;
                    if (back && rng.Randf() < 0.2f) continue;
                    var w = fl == 0 && front || hall ? 1.2f : rng.Randf() < 0.3f ? 0.8f : 1.1f;
                    HomeWindow(f, a, fy + (w < 0.9f ? 1.2f : 0.95f), w, w < 0.9f ? 0.95f : winH, frame, st, shutters, rng.Randf() < lit, rng, false);
                }
            }
            // Fenêtre de comble dans le pignon des maisons de plain-pied.
            if (gableSide && floors == 1 && st.Roof == RoofShape.Gable)
                HomeWindow(f, sd.Length / 2, h + 0.35f, 0.7f, 0.8f, frame, st, false, rng.Randf() < lit, rng, false);
        }

        return HomeRoof(kit, s, st, rng, ridgeX);
    }

    private static void HomeWindow(Facade f, float a, float y0, float w, float hgt, Color frame, BuildingStyle st, bool shutters, bool lit, RandomNumberGenerator rng, bool bay)
    {
        var d = Mats.Detail();
        // Vitre (éclairée la nuit), encadrement, meneau, appui.
        var paneMat = lit ? Mats.Lamps() : Pane();
        if (lit) f.Col(new Color(1f, 0.78f, 0.5f));
        // Vitre, châssis et volets ne saillent que de quelques centimètres : hors collision (appui et coffre y sont).
        f.Solid(false);
        f.Box(paneMat, a, y0 + hgt / 2, 0.01f, w, hgt, 0.03f);
        f.Col(frame);
        f.Box(d, a, y0 + hgt + 0.03f, 0.04f, w + 0.12f, 0.07f, 0.08f);
        f.Box(d, a - w / 2 - 0.03f, y0 + hgt / 2, 0.04f, 0.07f, hgt, 0.08f);
        f.Box(d, a + w / 2 + 0.03f, y0 + hgt / 2, 0.04f, 0.07f, hgt, 0.08f);
        f.Box(d, a, y0 + hgt / 2, 0.04f, 0.05f, hgt, 0.06f);
        if (!bay && hgt > 1f) f.Box(d, a, y0 + hgt * 0.68f, 0.04f, w, 0.04f, 0.05f);
        // Appui en pierre.
        f.Col(new Color(0.82f, 0.8f, 0.75f));
        f.Solid(true);
        f.Box(d, a, y0 - 0.04f, 0.09f, w + 0.3f, 0.08f, 0.2f);
        f.Solid(false);
        if (shutters)
        {
            // Volets battants ouverts de part et d'autre, avec barres horizontales.
            f.Col(st.TrimColor);
            foreach (var sg in new[] { -1f, 1f })
            {
                var c = a + sg * (w * 0.75f + 0.1f);
                f.Box(d, c, y0 + hgt / 2, 0.05f, w / 2, hgt + 0.06f, 0.04f);
                f.Col(st.TrimColor.Darkened(0.25f));
                f.Box(d, c, y0 + hgt * 0.2f, 0.075f, w / 2 - 0.06f, 0.08f, 0.02f);
                f.Box(d, c, y0 + hgt * 0.8f, 0.075f, w / 2 - 0.06f, 0.08f, 0.02f);
                f.Col(st.TrimColor);
            }
        }
        else
        {
            // Coffre de volet roulant et tablier à moitié descendu.
            f.Col(frame.Lerp(new Color(0.8f, 0.8f, 0.78f), 0.5f));
            f.Solid(true);
            f.Box(d, a, y0 + hgt + 0.17f, 0.08f, w + 0.1f, 0.22f, 0.18f);
            f.Solid(false);
            var drop = rng.Randf() < 0.5f ? 0f : rng.RandfRange(0.15f, 0.7f);
            if (drop > 0)
            {
                f.Col(new Color(0.72f, 0.72f, 0.7f));
                f.Box(d, a, y0 + hgt - hgt * drop / 2, 0.035f, w, hgt * drop, 0.03f);
            }
        }
        f.Solid(true);
    }


    /// <summary>Rez-de-chaussée commercial d'une maison de bourg : vitrine, enseigne à lettrage, store.</summary>
    private static void ShopFront(MeshKit kit, Facade f, BuildingStyle st, RandomNumberGenerator rng, float top)
    {
        var d = Mats.Detail();
        var L = f.S.Length;
        var a0 = f.At(0.45f, 0, 0.02f);
        var a1 = f.At(L - 0.45f, 0, 0.02f);
        var u = (L - 0.9f) / 3.5f;
        kit.Col = Colors.White;
        kit.Quad(Mats.Storefront(st.Night), a0, a1, a1 + Vector3.Up * top, a0 + Vector3.Up * top, new Vector2(0, 1), new Vector2(u, 1), new Vector2(u, 0), new Vector2(0, 0));
        var sc = SignColors[rng.RandiRange(0, SignColors.Length - 1)];
        kit.Col = sc;
        f.Box(d, L / 2, top - 0.35f, 0.08f, L * 0.7f, 0.6f, 0.12f);
        var lc = sc.Luminance > 0.5f ? new Color(0.08f, 0.08f, 0.1f) : new Color(0.97f, 0.96f, 0.92f);
        kit.Col = lc;
        Material lm = st.Night && lc.Luminance > 0.5f ? Mats.Lamps() : d;
        var tw = L * 0.45f;
        var letters = Mathf.RoundToInt(tw / 0.32f);
        for (var i = 0; i < letters; i++)
        {
            if (i == letters / 2 && rng.Randf() < 0.6f) continue; // espace entre deux mots
            var lh = rng.Randf() < 0.2f ? 0.32f : 0.24f;
            f.Box(lm, L / 2 - tw / 2 + (i + 0.5f) * tw / letters, top - 0.38f + lh / 2 - 0.12f, 0.15f, tw / letters * 0.75f, lh, 0.03f);
        }
        if (rng.Randf() < 0.6f)
        {
            // Store banne incliné.
            kit.Col = AwningColors[rng.RandiRange(0, AwningColors.Length - 1)];
            var ang = Mathf.Atan2(0.5f, 1.3f);
            var z = (f.S.Normal * Mathf.Cos(ang) - Vector3.Up * Mathf.Sin(ang)).Normalized();
            var x = f.S.Along;
            var y = z.Cross(x).Normalized();
            kit.Box(d, f.At(L / 2, top - 0.75f, 0) + z * 0.7f, new Vector3(L * 0.75f, 0.05f, 1.4f), new Basis(x, y, z));
        }
        // Pilastres d'encadrement.
        kit.Col = st.WallColor.Darkened(0.15f);
        f.Box(d, 0.25f, top / 2, 0.05f, 0.5f, top, 0.12f);
        f.Box(d, L - 0.25f, top / 2, 0.05f, 0.5f, top, 0.12f);
    }

    private static void HomeDoor(Facade f, float a, BuildingStyle st, Color frame, bool canopy = true)
    {
        var d = Mats.Detail();
        // Seuil, porte pleine colorée, imposte vitrée, marquise.
        f.Col(new Color(0.6f, 0.59f, 0.56f));
        f.Box(d, a, 0.12f, 0.45f, 1.7f, 0.24f, 0.9f);
        f.Col(st.TrimColor.Darkened(0.1f));
        f.Box(d, a, 1.4f, 0.03f, 1.0f, 2.2f, 0.06f);
        f.Col(st.TrimColor.Darkened(0.35f));
        f.Box(d, a, 1.4f, 0.065f, 0.7f, 1.6f, 0.02f);
        f.Box(Pane(), a, 2.65f, 0.02f, 1.0f, 0.25f, 0.03f);
        f.Col(frame);
        f.Box(d, a, 2.82f, 0.05f, 1.2f, 0.08f, 0.1f);
        f.Box(d, a - 0.56f, 1.55f, 0.05f, 0.08f, 2.55f, 0.1f);
        f.Box(d, a + 0.56f, 1.55f, 0.05f, 0.08f, 2.55f, 0.1f);
        f.Col(new Color(0.8f, 0.7f, 0.35f));
        f.Box(d, a + 0.35f, 1.35f, 0.08f, 0.12f, 0.04f, 0.04f);
        f.Col(new Color(0.18f, 0.19f, 0.2f));
        if (canopy) f.Box(Mats.DetailMetal(), a, 3.02f, 0.5f, 1.7f, 0.06f, 1.0f);
        // Applique murale.
        f.Col(new Color(0.95f, 0.85f, 0.6f));
        f.Box(st.Night ? Mats.Lamps() : d, a - 0.85f, 2.3f, 0.1f, 0.14f, 0.24f, 0.14f);
    }

    /// <summary>Toit à deux pans ou à quatre pans avec débords, rives, gouttières, cheminée et châssis de toit.</summary>
    private static Shape3D HomeRoof(MeshKit kit, Vector3 s, BuildingStyle st, RandomNumberGenerator rng, bool ridgeX)
    {
        var d = Mats.Detail();
        var tiles = Mats.RoofTiles();
        var h = s.Y;
        var lu = ridgeX ? s.X : s.Z;   // longueur le long du faîtage
        var lv = ridgeX ? s.Z : s.X;   // portée
        var sv = lv / 2;
        var pitch = st.RoofPitch > 0 ? st.RoofPitch : 0.8f;
        var r = sv * pitch;
        const float oe = 0.45f, og = 0.35f, t = 0.16f;
        var hip = st.Roof == RoofShape.Hip;
        Vector3 W(float u, float v, float y) => ridgeX ? new Vector3(u, y, v) : new Vector3(v, y, u);
        var yE = h - oe * pitch;
        var yR = h + r;
        var ue = lu / 2 + (hip ? oe : og);
        var rh = hip ? Mathf.Max(0, lu / 2 - sv) : ue;
        var slopeLen = Mathf.Sqrt((sv + oe) * (sv + oe) + (r + oe * pitch) * (r + oe * pitch));

        // Pans (dessus en tuiles, sous-face claire), UV en mètres / 2 le long de l'égout et de la pente.
        var tileCol = st.RoofColor;
        foreach (var sg in new[] { -1f, 1f })
        {
            kit.Col = tileCol;
            Vector2 U(float u, float v) => new((u + ue) / 2f, (Mathf.Abs(v) / (sv + oe)) * slopeLen / 2f);
            var a = W(-ue, sg * (sv + oe), yE + t);
            var b = W(ue, sg * (sv + oe), yE + t);
            var c = W(rh, 0, yR + t);
            var e = W(-rh, 0, yR + t);
            QuadN(kit, Vector3.Up, tiles, a, b, c, e, U(-ue, sv + oe), U(ue, sv + oe), U(rh, 0), U(-rh, 0));
            kit.Col = new Color(0.9f, 0.89f, 0.86f);
            QuadN(kit, Vector3.Down, d, W(-ue, sg * (sv + oe), yE), W(ue, sg * (sv + oe), yE), W(rh, 0, yR), W(-rh, 0, yR));
            // Planche d'égout.
            kit.Col = st.TrimColor.Lerp(new Color(0.9f, 0.9f, 0.88f), 0.6f);
            QuadN(kit, W(0, sg, 0), d, W(-ue, sg * (sv + oe), yE - 0.04f), W(ue, sg * (sv + oe), yE - 0.04f), W(ue, sg * (sv + oe), yE + t), W(-ue, sg * (sv + oe), yE + t));
            // Gouttière et descente.
            kit.Col = new Color(0.62f, 0.63f, 0.62f);
            kit.Tube(Mats.DetailMetal(), W(-ue, sg * (sv + oe + 0.06f), yE - 0.02f), W(ue, sg * (sv + oe + 0.06f), yE - 0.02f), 0.07f, 6, true);
            var px = sg > 0 ? lu / 2 - 0.15f : -lu / 2 + 0.15f;
            kit.Tube(Mats.DetailMetal(), W(px, sg * (sv + 0.08f), 0.1f), W(px, sg * (sv + 0.08f), yE - 0.05f), 0.045f, 5);
            kit.Tube(Mats.DetailMetal(), W(px, sg * (sv + 0.08f), yE - 0.05f), W(px, sg * (sv + oe + 0.06f), yE - 0.02f), 0.045f, 5);
        }
        if (hip)
        {
            foreach (var sg in new[] { -1f, 1f })
            {
                // Croupes : triangles entre l'égout du petit côté et l'extrémité du faîtage.
                kit.Col = tileCol;
                var run = sv + oe;
                Vector2 U(float v, float u) => new((v + run) / 2f, Mathf.Abs(ue - Mathf.Abs(u)) / run * slopeLen / 2f);
                var a = W(sg * ue, -run, yE + t);
                var b = W(sg * ue, run, yE + t);
                var c = W(sg * rh, 0, yR + t);
                QuadN(kit, Vector3.Up, tiles, a, b, c, c, U(-run, ue), U(run, ue), U(0, rh), U(0, rh));
                kit.Col = new Color(0.9f, 0.89f, 0.86f);
                QuadN(kit, Vector3.Down, d, W(sg * ue, -run, yE), W(sg * ue, run, yE), W(sg * rh, 0, yR), W(sg * rh, 0, yR));
                kit.Col = st.TrimColor.Lerp(new Color(0.9f, 0.9f, 0.88f), 0.6f);
                QuadN(kit, W(sg, 0, 0), d, W(sg * ue, -run, yE - 0.04f), W(sg * ue, run, yE - 0.04f), W(sg * ue, run, yE + t), W(sg * ue, -run, yE + t));
                // Arêtiers.
                kit.Col = tileCol.Darkened(0.2f);
                foreach (var sv2 in new[] { -1f, 1f })
                    kit.Tube(d, W(sg * ue, sv2 * run, yE + t + 0.02f), W(sg * rh, 0, yR + t + 0.04f), 0.09f, 4);
            }
        }
        else
        {
            // Pignons (triangles de mur) et rives.
            foreach (var sg in new[] { -1f, 1f })
            {
                kit.Col = st.WallColor;
                kit.Tri(d, W(sg * lu / 2, -sv, h), W(sg * lu / 2, sv, h), W(sg * lu / 2, 0, yR), Vector3.Up * h);
                kit.Col = st.TrimColor.Lerp(new Color(0.9f, 0.9f, 0.88f), 0.6f);
                foreach (var sv2 in new[] { -1f, 1f })
                {
                    var p0 = W(sg * ue, sv2 * (sv + oe), yE + t * 0.5f);
                    var p1 = W(sg * ue, 0, yR + t * 0.5f);
                    kit.Tube(d, p0, p1, 0.11f, 4);
                }
            }
        }
        // Faîtage.
        kit.Col = tileCol.Darkened(0.22f);
        if (rh > 0.01f) kit.Tube(d, W(-rh, 0, yR + t + 0.03f), W(rh, 0, yR + t + 0.03f), 0.14f, 6, true);

        // Cheminée.
        if (rng.Randf() < 0.75f)
        {
            var cu = rng.RandfRange(-lu / 2 + 1.2f, lu / 2 - 1.2f);
            var cv = rng.RandfRange(-sv * 0.5f, sv * 0.5f);
            var roofY = h + r * (1 - Mathf.Abs(cv) / sv);
            var top = yR + rng.RandfRange(0.5f, 1.0f);
            kit.Col = rng.Randf() < 0.5f ? st.WallColor : new Color(0.55f, 0.28f, 0.2f);
            var cc = W(cu, cv, (roofY - 0.4f + top) / 2);
            kit.Box(d, cc, new Vector3(0.6f, top - roofY + 0.4f, 0.6f));
            kit.Col = new Color(0.45f, 0.44f, 0.42f);
            kit.Box(d, W(cu, cv, top + 0.05f), new Vector3(0.75f, 0.1f, 0.75f));
            kit.Col = new Color(0.55f, 0.32f, 0.22f);
            kit.Cylinder(d, W(cu, cv, top + 0.1f), 0.1f, 0.1f, 0.3f, 6);
        }
        // Châssis de toit (côté arrière) et parabole.
        var backSg = st.Facing is 0 or 2 ? -1f : 1f;
        if (rng.Randf() < 0.45f)
        {
            var nV = rng.RandiRange(1, 2);
            var uDir = ridgeX ? Vector3.Right : Vector3.Back;
            var vDir = (ridgeX ? Vector3.Back : Vector3.Right) * backSg;
            var down = (vDir * 1f + Vector3.Down * pitch).Normalized();
            var nrm = uDir.Cross(down);
            if (nrm.Y < 0) nrm = -nrm;
            var basis = new Basis(uDir, nrm, down);
            for (var i = 0; i < nV; i++)
            {
                var u = (i - (nV - 1) / 2f) * 2.2f;
                var v = sv * 0.5f;
                var y = h + r * 0.5f + t + 0.04f;
                kit.Col = new Color(0.25f, 0.26f, 0.27f);
                kit.Box(Mats.DetailMetal(), W(u, v * backSg, y), new Vector3(0.85f, 0.08f, 1.1f), basis);
                kit.Box(Pane(), W(u, v * backSg, y) + nrm * 0.045f, new Vector3(0.7f, 0.02f, 0.95f), basis);
            }
        }
        if (rng.Randf() < 0.25f)
        {
            var p = W(rng.RandfRange(-lu / 4, lu / 4), sv * 0.3f * backSg, h + r * 0.7f + t);
            kit.Col = new Color(0.3f, 0.3f, 0.32f);
            kit.Tube(Mats.DetailMetal(), p, p + Vector3.Up * 0.7f, 0.03f, 5);
            kit.Col = new Color(0.86f, 0.86f, 0.84f);
            kit.Cylinder(d, p + Vector3.Up * 0.7f, 0.35f, 0.4f, 0.06f, 10, true, new Basis(Vector3.Up, rng.RandfRange(0, Mathf.Tau)) * new Basis(Vector3.Right, Mathf.DegToRad(65)));
        }

        // Collision du toit : prisme (deux pans) ou tronc de pyramide (quatre pans) sans les débords.
        var pts = hip
            ? new[] { W(-lu / 2, -sv, h), W(lu / 2, -sv, h), W(lu / 2, sv, h), W(-lu / 2, sv, h), W(-Mathf.Max(rh - oe, 0), 0, yR), W(Mathf.Max(rh - oe, 0), 0, yR) }
            : new[] { W(-lu / 2, -sv, h), W(lu / 2, -sv, h), W(lu / 2, sv, h), W(-lu / 2, sv, h), W(-lu / 2, 0, yR), W(lu / 2, 0, yR) };
        return new ConvexPolygonShape3D { Points = pts };
    }

    // ───────────────────────── Garage ─────────────────────────

    private static void BuildGarage(MeshKit kit, Vector3 s, BuildingStyle st, RandomNumberGenerator rng)
    {
        var d = Mats.Detail();
        var h = s.Y;
        Walls(kit, d, s, 0, h, st.WallColor);
        kit.Col = st.WallColor.Darkened(0.32f);
        kit.Ring(d, s.X / 2, s.Z / 2, 0f, 0.35f, 0.08f, 0.04f);
        // Toit-terrasse : étanchéité sombre et couvertine.
        kit.Col = new Color(0.27f, 0.27f, 0.28f);
        kit.Quad(d, new Vector3(-s.X / 2, h, s.Z / 2), new Vector3(s.X / 2, h, s.Z / 2), new Vector3(s.X / 2, h, -s.Z / 2), new Vector3(-s.X / 2, h, -s.Z / 2));
        kit.Col = st.WallColor.Lerp(new Color(0.8f, 0.8f, 0.78f), 0.5f);
        kit.Ring(d, s.X / 2, s.Z / 2, h - 0.05f, h + 0.22f, 0.22f, 0.04f);
        // Porte sectionnelle à rainures.
        var f = new Facade(kit, Sides(s.X / 2, s.Z / 2)[st.Facing]);
        var w = Mathf.Min(2.5f, f.S.Length - 0.6f);
        var c = f.S.Length / 2;
        var doorCol = rng.Randf() < 0.6f ? new Color(0.9f, 0.9f, 0.88f) : st.TrimColor;
        kit.Col = doorCol;
        f.Box(d, c, 1.05f, 0.02f, w, 2.1f, 0.05f);
        kit.Col = doorCol.Darkened(0.2f);
        for (var i = 1; i < 5; i++) f.Box(d, c, i * 0.42f, 0.05f, w, 0.025f, 0.02f);
        kit.Col = new Color(0.8f, 0.8f, 0.77f);
        f.Box(d, c, 2.17f, 0.05f, w + 0.2f, 0.12f, 0.1f);
    }

    // ───────────────────────── Église et clocher ─────────────────────────

    private static Shape3D BuildChurch(MeshKit kit, Vector3 s, BuildingStyle st, RandomNumberGenerator rng)
    {
        var stone = Mats.Masonry();
        var d = Mats.Detail();
        var h = s.Y;
        Walls(kit, stone, s, 0, h, st.WallColor);
        kit.Col = st.WallColor.Darkened(0.25f);
        kit.Ring(stone, s.X / 2, s.Z / 2, 0f, 0.8f, 0.2f, 0.12f);
        kit.Col = st.WallColor.Lightened(0.1f);
        kit.Ring(stone, s.X / 2, s.Z / 2, h - 0.35f, h, 0.3f, 0.12f);
        var sides = Sides(s.X / 2, s.Z / 2);
        var glass = Mats.StainedGlass(st.Night);
        // Murs gouttereaux (le long de la nef) : contreforts et baies en arc brisé.
        foreach (var si in new[] { 2, 3 })
        {
            var f = new Facade(kit, sides[si]);
            var n = Mathf.FloorToInt(f.S.Length / 4.6f);
            var step = f.S.Length / n;
            for (var i = 0; i <= n; i++)
            {
                var a = i * step;
                kit.Col = st.WallColor.Darkened(0.08f);
                f.Box(stone, Mathf.Clamp(a, 0.5f, f.S.Length - 0.5f), h * 0.4f, 0.5f, 1.0f, h * 0.8f, 1.0f);
                f.Box(stone, Mathf.Clamp(a, 0.5f, f.S.Length - 0.5f), h * 0.8f + 0.3f, 0.3f, 0.9f, 0.6f, 0.6f);
                if (i == n) continue;
                Lancet(kit, f, a + step / 2, 2.4f, 1.3f, h - 4.0f, glass, st.WallColor.Lightened(0.15f));
            }
        }
        // Chevet : grande baie.
        var back = new Facade(kit, sides[st.Facing ^ 1]);
        Lancet(kit, back, back.S.Length / 2, 3f, 2.4f, h - 3.5f, glass, st.WallColor.Lightened(0.15f));

        // Toiture en ardoise, très pentue.
        var roofSt = st;
        roofSt.Facing = 2;  // faîtage selon Z (nef orientée nord-sud)
        roofSt.Roof = RoofShape.Gable;
        roofSt.TrimColor = new Color(0.3f, 0.3f, 0.32f);
        return ChurchRoof(kit, s, roofSt);
    }

    /// <summary>Toit à deux pans sans accessoires (église), faîtage selon Z.</summary>
    private static Shape3D ChurchRoof(MeshKit kit, Vector3 s, BuildingStyle st)
    {
        var tiles = Mats.RoofTiles();
        var d = Mats.Detail();
        var h = s.Y;
        var sv = s.X / 2;
        var r = sv * st.RoofPitch;
        const float oe = 0.5f, og = 0.3f, t = 0.2f;
        var yE = h - oe * st.RoofPitch;
        var yR = h + r;
        var ue = s.Z / 2 + og;
        var slopeLen = Mathf.Sqrt((sv + oe) * (sv + oe) + (r + oe * st.RoofPitch) * (r + oe * st.RoofPitch));
        Vector3 W(float u, float v, float y) => new(v, y, u);
        foreach (var sg in new[] { -1f, 1f })
        {
            kit.Col = st.RoofColor;
            QuadN(kit, Vector3.Up, tiles, W(-ue, sg * (sv + oe), yE + t), W(ue, sg * (sv + oe), yE + t), W(ue, 0, yR + t), W(-ue, 0, yR + t),
                new Vector2(0, slopeLen / 2), new Vector2(ue, slopeLen / 2), new Vector2(ue, 0), new Vector2(0, 0));
            kit.Col = new Color(0.4f, 0.38f, 0.35f);
            QuadN(kit, Vector3.Down, d, W(-ue, sg * (sv + oe), yE), W(ue, sg * (sv + oe), yE), W(ue, 0, yR), W(-ue, 0, yR));
            QuadN(kit, W(0, sg, 0), d, W(-ue, sg * (sv + oe), yE), W(ue, sg * (sv + oe), yE), W(ue, sg * (sv + oe), yE + t), W(-ue, sg * (sv + oe), yE + t));
            kit.Col = st.WallColor;
            kit.Tri(Mats.Masonry(), W(sg * s.Z / 2, -sv, h), W(sg * s.Z / 2, sv, h), W(sg * s.Z / 2, 0, yR), Vector3.Up * h);
            kit.Col = st.WallColor.Lightened(0.1f);
            foreach (var sv2 in new[] { -1f, 1f })
                kit.Tube(Mats.Masonry(), W(sg * (s.Z / 2 + 0.1f), sv2 * (sv + 0.3f), yE + 0.3f), W(sg * (s.Z / 2 + 0.1f), 0, yR + 0.35f), 0.25f, 4);
        }
        kit.Col = st.RoofColor.Darkened(0.2f);
        kit.Tube(d, W(-ue, 0, yR + t + 0.05f), W(ue, 0, yR + t + 0.05f), 0.16f, 6, true);
        // Croix sur le pignon du chevet.
        kit.Col = new Color(0.75f, 0.73f, 0.68f);
        var cp = W(-s.Z / 2, 0, yR + 0.3f);
        kit.Box(Mats.Masonry(), cp + Vector3.Up * 0.8f, new Vector3(0.22f, 1.6f, 0.22f));
        kit.Box(Mats.Masonry(), cp + Vector3.Up * 1.1f, new Vector3(0.9f, 0.22f, 0.22f));
        return new ConvexPolygonShape3D
        {
            Points = new[] { W(-s.Z / 2, -sv, h), W(s.Z / 2, -sv, h), W(s.Z / 2, sv, h), W(-s.Z / 2, sv, h), W(-s.Z / 2, 0, yR), W(s.Z / 2, 0, yR) },
        };
    }

    /// <summary>Baie en arc brisé : vitrail, ébrasement en pierre, arc (deux pans inclinés).</summary>
    private static void Lancet(MeshKit kit, Facade f, float a, float y0, float w, float hgt, Material glass, Color frame)
    {
        var stone = Mats.Masonry();
        kit.Col = Colors.White;
        f.Box(glass, a, y0 + hgt / 2, 0.02f, w, hgt, 0.03f);
        // Pointe de l'arc : triangle de vitrail.
        var p0 = f.At(a - w / 2, y0 + hgt, 0.035f);
        var p1 = f.At(a + w / 2, y0 + hgt, 0.035f);
        var p2 = f.At(a, y0 + hgt + w * 0.75f, 0.035f);
        kit.Tri(glass, p0, p1, p2, f.At(a, y0 + hgt, -1f));
        kit.Col = frame;
        f.Box(stone, a - w / 2 - 0.12f, y0 + hgt / 2, 0.08f, 0.24f, hgt, 0.16f);
        f.Box(stone, a + w / 2 + 0.12f, y0 + hgt / 2, 0.08f, 0.24f, hgt, 0.16f);
        f.Box(stone, a, y0 - 0.1f, 0.12f, w + 0.5f, 0.2f, 0.25f);
        kit.Tube(stone, f.At(a - w / 2 - 0.12f, y0 + hgt, 0.08f), f.At(a + 0.02f, y0 + hgt + w * 0.75f + 0.16f, 0.08f), 0.13f, 4);
        kit.Tube(stone, f.At(a + w / 2 + 0.12f, y0 + hgt, 0.08f), f.At(a - 0.02f, y0 + hgt + w * 0.75f + 0.16f, 0.08f), 0.13f, 4);
        // Remplage : meneau central.
        f.Box(stone, a, y0 + hgt / 2, 0.05f, 0.1f, hgt, 0.06f);
    }

    private static Shape3D BuildBelfry(MeshKit kit, Vector3 s, BuildingStyle st, RandomNumberGenerator rng)
    {
        var stone = Mats.Masonry();
        var d = Mats.Detail();
        var h = s.Y;
        Walls(kit, stone, s, 0, h, st.WallColor);
        kit.Col = st.WallColor.Darkened(0.25f);
        kit.Ring(stone, s.X / 2, s.Z / 2, 0f, 1.0f, 0.25f, 0.15f);
        kit.Col = st.WallColor.Lightened(0.1f);
        kit.Ring(stone, s.X / 2, s.Z / 2, h * 0.45f, h * 0.45f + 0.3f, 0.2f, 0.1f);
        kit.Ring(stone, s.X / 2, s.Z / 2, h - 5.6f, h - 5.3f, 0.2f, 0.1f);
        kit.Ring(stone, s.X / 2, s.Z / 2, h - 0.4f, h + 0.1f, 0.4f, 0.25f);
        var sides = Sides(s.X / 2, s.Z / 2);
        // Contreforts d'angle.
        kit.Col = st.WallColor.Darkened(0.06f);
        foreach (var sx in new[] { -1f, 1f })
        foreach (var sz in new[] { -1f, 1f })
            kit.Box(stone, new Vector3(sx * (s.X / 2 + 0.1f), h * 0.3f, sz * (s.Z / 2 + 0.1f)), new Vector3(1.1f, h * 0.6f, 1.1f));
        var dark = new Color(0.06f, 0.05f, 0.05f);
        for (var si = 0; si < 4; si++)
        {
            var f = new Facade(kit, sides[si]);
            // Abat-sons : deux baies jumelles sombres à lames de bois.
            foreach (var off in new[] { -0.9f, 0.9f })
            {
                var a = f.S.Length / 2 + off * (s.X / 7f) * 1.6f;
                kit.Col = dark;
                f.Box(d, a, h - 3.0f, 0.02f, 1.1f, 3.4f, 0.03f);
                kit.Col = new Color(0.32f, 0.24f, 0.17f);
                for (var j = 0; j < 7; j++) f.Box(d, a, h - 4.4f + j * 0.42f, 0.06f, 1.1f, 0.08f, 0.05f);
                kit.Col = st.WallColor.Lightened(0.15f);
                kit.Tube(stone, f.At(a - 0.7f, h - 1.3f, 0.06f), f.At(a, h - 0.6f, 0.06f), 0.12f, 4);
                kit.Tube(stone, f.At(a + 0.7f, h - 1.3f, 0.06f), f.At(a, h - 0.6f, 0.06f), 0.12f, 4);
                f.Box(stone, a - 0.66f, h - 3.0f, 0.06f, 0.2f, 3.4f, 0.14f);
                f.Box(stone, a + 0.66f, h - 3.0f, 0.06f, 0.2f, 3.4f, 0.14f);
            }
            // Meurtrières.
            kit.Col = dark;
            f.Box(d, f.S.Length / 2, h * 0.3f, 0.02f, 0.25f, 1.4f, 0.03f);
            if (si == st.Facing)
            {
                // Horloge.
                var cp = f.At(f.S.Length / 2, h - 7.4f, 0.05f);
                var rot = new Basis(Vector3.Up, Mathf.Atan2(f.S.Normal.X, f.S.Normal.Z)) * new Basis(Vector3.Right, Mathf.Pi / 2);
                kit.Col = new Color(0.12f, 0.12f, 0.13f);
                kit.Cylinder(d, cp, 1.25f, 1.25f, 0.08f, 24, true, rot);
                kit.Col = new Color(0.93f, 0.92f, 0.88f);
                kit.Cylinder(d, cp + f.S.Normal * 0.08f, 1.1f, 1.1f, 0.03f, 24, true, rot);
                kit.Col = new Color(0.1f, 0.1f, 0.1f);
                for (var hI = 0; hI < 12; hI++)
                {
                    var ang = hI * Mathf.Tau / 12;
                    f.Box(d, f.S.Length / 2 + Mathf.Sin(ang) * 0.9f, h - 7.4f + Mathf.Cos(ang) * 0.9f, 0.13f, 0.08f, 0.08f, 0.02f);
                }
                kit.Tube(d, cp + f.S.Normal * 0.14f, cp + f.S.Normal * 0.14f + Vector3.Up * 0.75f, 0.04f, 4);
                kit.Tube(d, cp + f.S.Normal * 0.14f, cp + f.S.Normal * 0.14f + f.S.Along * 0.5f + Vector3.Down * 0.2f, 0.05f, 4);
                // Portail en arc brisé.
                kit.Col = new Color(0.3f, 0.2f, 0.12f);
                f.Box(d, f.S.Length / 2, 1.5f, 0.03f, 1.8f, 3.0f, 0.06f);
                kit.Tri(d, f.At(f.S.Length / 2 - 0.9f, 3f, 0.06f), f.At(f.S.Length / 2 + 0.9f, 3f, 0.06f), f.At(f.S.Length / 2, 4.1f, 0.06f), f.At(f.S.Length / 2, 3f, -1f));
                kit.Col = st.WallColor.Lightened(0.15f);
                f.Box(stone, f.S.Length / 2 - 1.1f, 1.6f, 0.1f, 0.4f, 3.2f, 0.2f);
                f.Box(stone, f.S.Length / 2 + 1.1f, 1.6f, 0.1f, 0.4f, 3.2f, 0.2f);
                kit.Tube(stone, f.At(f.S.Length / 2 - 1.1f, 3.1f, 0.1f), f.At(f.S.Length / 2, 4.4f, 0.1f), 0.2f, 4);
                kit.Tube(stone, f.At(f.S.Length / 2 + 1.1f, 3.1f, 0.1f), f.At(f.S.Length / 2, 4.4f, 0.1f), 0.2f, 4);
            }
        }
        // Flèche en ardoise (pyramide à huit pans) surmontée d'une croix et d'un coq.
        var spireH = s.X * 1.9f;
        var r0 = s.X / 2 + 0.2f;
        var top = new Vector3(0, h + spireH, 0);
        kit.Col = st.RoofColor;
        var slate = Mats.RoofTiles();
        for (var i = 0; i < 8; i++)
        {
            var a0 = (i + 0.5f) * Mathf.Tau / 8;
            var a1 = (i + 1.5f) * Mathf.Tau / 8;
            var k0 = r0 / Mathf.Cos(Mathf.Pi / 8);
            var p0 = new Vector3(Mathf.Cos(a0) * k0, h + 0.1f, Mathf.Sin(a0) * k0);
            var p1 = new Vector3(Mathf.Cos(a1) * k0, h + 0.1f, Mathf.Sin(a1) * k0);
            var w = p0.DistanceTo(p1);
            var sl = p0.DistanceTo(top);
            QuadN(kit, new Vector3(p0.X + p1.X, 0, p0.Z + p1.Z), slate, p0, p1, top, top, new Vector2(0, sl / 2), new Vector2(w / 2, sl / 2), new Vector2(w / 4, 0), new Vector2(w / 4, 0));
        }
        kit.Col = new Color(0.3f, 0.28f, 0.22f);
        kit.Box(Mats.DetailMetal(), top + Vector3.Up * 0.9f, new Vector3(0.1f, 1.8f, 0.1f));
        kit.Box(Mats.DetailMetal(), top + Vector3.Up * 1.3f, new Vector3(0.8f, 0.1f, 0.1f));
        kit.Box(Mats.DetailMetal(), top + Vector3.Up * 1.95f, new Vector3(0.5f, 0.3f, 0.05f));
        return new ConvexPolygonShape3D
        {
            Points = new[] { new Vector3(-s.X / 2, h, -s.Z / 2), new Vector3(s.X / 2, h, -s.Z / 2), new Vector3(s.X / 2, h, s.Z / 2), new Vector3(-s.X / 2, h, s.Z / 2), top },
        };
    }

    // ───────────────────────── Gymnase et supérette ─────────────────────────

    private static Shape3D BuildGym(MeshKit kit, Vector3 s, BuildingStyle st, RandomNumberGenerator rng)
    {
        var d = Mats.Detail();
        var metal = Mats.DetailMetal();
        var h = s.Y;
        Walls(kit, d, s, 0, 3.2f, st.WallColor);
        Walls(kit, d, s, 3.2f, h, st.TrimColor);
        kit.Col = st.WallColor.Darkened(0.2f);
        kit.Ring(d, s.X / 2, s.Z / 2, 3.1f, 3.3f, 0.1f, 0.05f);
        var sides = Sides(s.X / 2, s.Z / 2);
        // Bandeaux vitrés hauts et bardage à joints verticaux.
        for (var si = 0; si < 4; si++)
        {
            var f = new Facade(kit, sides[si]);
            var L = f.S.Length;
            if (si <= 1)
            {
                f.Box(st.Night ? Mats.Lamps() : Pane(), L / 2, h - 2.3f, 0.02f, L - 3f, 1.8f, 0.03f);
                kit.Col = new Color(0.3f, 0.32f, 0.34f);
                for (var a = 1.5f; a <= L - 1.4f; a += 1.8f) f.Box(metal, a, h - 2.3f, 0.05f, 0.08f, 1.9f, 0.06f);
                f.Box(metal, L / 2, h - 1.35f, 0.05f, L - 3f, 0.1f, 0.08f);
                f.Box(metal, L / 2, h - 3.25f, 0.05f, L - 3f, 0.1f, 0.08f);
            }
            kit.Col = st.TrimColor.Darkened(0.12f);
            for (var a = 1.2f; a < L - 0.6f; a += 2.4f) f.Box(d, a, (3.3f + (si <= 1 ? h - 3.3f : h)) / 2, 0.02f, 0.05f, (si <= 1 ? h - 3.3f : h) - 3.3f, 0.03f);
        }
        // Entrée : portes vitrées, auvent, enseigne.
        var fr = new Facade(kit, sides[st.Facing]);
        var c = fr.S.Length / 2;
        kit.Col = Colors.White;
        fr.Box(st.Night ? Mats.Lamps() : Pane(), c, 1.3f, 0.02f, 5f, 2.6f, 0.03f);
        kit.Col = new Color(0.25f, 0.27f, 0.28f);
        for (var i = -2; i <= 2; i++) fr.Box(metal, c + i * 1.25f, 1.3f, 0.05f, 0.08f, 2.6f, 0.06f);
        fr.Box(metal, c, 2.62f, 0.05f, 5.1f, 0.08f, 0.06f);
        kit.Col = new Color(0.82f, 0.82f, 0.8f);
        fr.Box(d, c, 3.0f, 1.5f, 7.5f, 0.25f, 3f);
        kit.Col = new Color(0.3f, 0.3f, 0.32f);
        fr.Box(metal, c - 3.4f, 1.45f, 2.8f, 0.15f, 2.9f, 0.15f);
        fr.Box(metal, c + 3.4f, 1.45f, 2.8f, 0.15f, 2.9f, 0.15f);
        kit.Col = new Color(0.08f, 0.25f, 0.5f);
        PixelText(kit, fr, "GYMNASE", c, h - 0.95f, 0.13f, d);

        // Toiture métallique à faible pente, faîtage selon X.
        var sv = s.Z / 2;
        var r = sv * 0.16f;
        const float oe = 0.6f;
        kit.Col = new Color(0.55f, 0.57f, 0.58f);
        foreach (var sg in new[] { -1f, 1f })
        {
            var yE = h - oe * 0.16f;
            QuadN(kit, Vector3.Up, metal, new Vector3(-s.X / 2 - oe, yE + 0.15f, sg * (sv + oe)), new Vector3(s.X / 2 + oe, yE + 0.15f, sg * (sv + oe)), new Vector3(s.X / 2 + oe, h + r + 0.15f, 0), new Vector3(-s.X / 2 - oe, h + r + 0.15f, 0));
            QuadN(kit, new Vector3(0, 0, sg), metal, new Vector3(-s.X / 2 - oe, yE, sg * (sv + oe)), new Vector3(s.X / 2 + oe, yE, sg * (sv + oe)), new Vector3(s.X / 2 + oe, yE + 0.15f, sg * (sv + oe)), new Vector3(-s.X / 2 - oe, yE + 0.15f, sg * (sv + oe)));
            kit.Col = st.TrimColor;
            kit.Tri(d, new Vector3(sg * s.X / 2, h, -sv), new Vector3(sg * s.X / 2, h, sv), new Vector3(sg * s.X / 2, h + r, 0), Vector3.Up * h);
            kit.Col = new Color(0.55f, 0.57f, 0.58f);
        }
        kit.Col = new Color(0.45f, 0.47f, 0.48f);
        for (var x = -s.X / 2; x <= s.X / 2; x += 3f)
            foreach (var sg in new[] { -1f, 1f })
                kit.Box(metal, new Vector3(x, h + r * 0.5f + 0.2f, sg * sv * 0.5f), new Vector3(0.06f, 0.06f, sv + 0.4f), new Basis(Vector3.Right, -sg * Mathf.Atan(0.16f)));
        return new ConvexPolygonShape3D
        {
            Points = new[] { new Vector3(-s.X / 2, h, -sv), new Vector3(s.X / 2, h, -sv), new Vector3(s.X / 2, h, sv), new Vector3(-s.X / 2, h, sv), new Vector3(-s.X / 2, h + r, 0), new Vector3(s.X / 2, h + r, 0) },
        };
    }

    private static void BuildMarket(MeshKit kit, Vector3 s, BuildingStyle st, RandomNumberGenerator rng)
    {
        var d = Mats.Detail();
        var metal = Mats.DetailMetal();
        var h = s.Y;
        Walls(kit, d, s, 0, h, st.WallColor);
        kit.Col = new Color(0.4f, 0.4f, 0.4f);
        kit.Ring(d, s.X / 2, s.Z / 2, 0f, 0.5f, 0.08f, 0.04f);
        kit.Col = new Color(0.3f, 0.3f, 0.31f);
        kit.Quad(d, new Vector3(-s.X / 2, h, s.Z / 2), new Vector3(s.X / 2, h, s.Z / 2), new Vector3(s.X / 2, h, -s.Z / 2), new Vector3(-s.X / 2, h, -s.Z / 2));
        kit.Col = st.WallColor.Lightened(0.2f);
        kit.Ring(d, s.X / 2, s.Z / 2, h - 0.1f, h + 0.7f, 0.25f, 0.05f);
        var sides = Sides(s.X / 2, s.Z / 2);
        for (var si = 0; si < 4; si++)
        {
            var f = new Facade(kit, sides[si]);
            kit.Col = st.WallColor.Darkened(0.1f);
            for (var a = 1.2f; a < f.S.Length - 0.6f; a += 1.2f) f.Box(d, a, h / 2 + 0.25f, 0.015f, 0.04f, h - 0.5f, 0.02f);
        }
        // Façade vitrée, marquise sur poteaux, bandeau d'enseigne.
        var fr = new Facade(kit, sides[st.Facing]);
        var L = fr.S.Length;
        var gw = L * 0.62f;
        var store = Mats.Storefront(st.Night);
        var a0 = fr.At(L / 2 - gw / 2, 0, 0.03f);
        var a1 = fr.At(L / 2 + gw / 2, 0, 0.03f);
        var u = gw / 3.5f;
        kit.Col = Colors.White;
        kit.Quad(store, a0, a1, a1 + Vector3.Up * 3.8f, a0 + Vector3.Up * 3.8f, new Vector2(0, 1), new Vector2(u, 1), new Vector2(u, 0), new Vector2(0, 0));
        kit.Col = new Color(0.86f, 0.86f, 0.84f);
        fr.Box(d, L / 2, 4.05f, 1.6f, gw + 2f, 0.3f, 3.2f);
        kit.Col = new Color(0.3f, 0.3f, 0.32f);
        for (var a = L / 2 - gw / 2; a <= L / 2 + gw / 2 + 0.1f; a += gw / 4) fr.Box(metal, a, 2f, 3f, 0.16f, 3.9f, 0.16f);
        kit.Col = st.TrimColor;
        fr.Box(d, L / 2, h - 0.6f, 0.12f, L * 0.55f, 1.5f, 0.2f);
        kit.Col = new Color(0.97f, 0.96f, 0.92f);
        PixelText(kit, fr, "MARCHE", L / 2, h - 0.6f, 0.25f, st.Night ? Mats.Lamps() : d, 0.23f);
        // Quai de livraison à l'arrière.
        var bk = new Facade(kit, sides[st.Facing ^ 1]);
        kit.Col = new Color(0.7f, 0.7f, 0.68f);
        bk.Box(d, bk.S.Length * 0.3f, 1.6f, 0.03f, 3.2f, 3.2f, 0.05f);
        kit.Col = new Color(0.55f, 0.55f, 0.54f);
        for (var i = 1; i < 6; i++) bk.Box(d, bk.S.Length * 0.3f, i * 0.55f, 0.06f, 3.2f, 0.03f, 0.02f);
        kit.Col = new Color(0.08f, 0.08f, 0.08f);
        bk.Box(d, bk.S.Length * 0.3f, 0.5f, 0.6f, 3.6f, 1.0f, 1.2f);
        // Groupes froids sur le toit.
        for (var i = 0; i < 4; i++)
        {
            var p = new Vector3(rng.RandfRange(-s.X / 2 + 3, s.X / 2 - 3), h, rng.RandfRange(-s.Z / 2 + 3, s.Z / 2 - 3));
            kit.Col = new Color(0.75f, 0.76f, 0.75f);
            kit.Box(metal, p + Vector3.Up * 0.6f, new Vector3(2.2f, 1.2f, 1.3f));
            kit.Col = new Color(0.1f, 0.1f, 0.11f);
            kit.Cylinder(d, p + Vector3.Up * 1.2f, 0.45f, 0.45f, 0.03f, 10);
        }
    }

    // ───────────────────────── Lettrage ─────────────────────────

    /// <summary>Police bâton 5 × 7 (lignes de haut en bas, bit 4 = colonne de gauche).</summary>
    private static readonly Dictionary<char, int[]> Font = new()
    {
        ['A'] = new[] { 14, 17, 17, 31, 17, 17, 17 },
        ['C'] = new[] { 14, 17, 16, 16, 16, 17, 14 },
        ['E'] = new[] { 31, 16, 16, 30, 16, 16, 31 },
        ['G'] = new[] { 14, 17, 16, 23, 17, 17, 15 },
        ['H'] = new[] { 17, 17, 17, 31, 17, 17, 17 },
        ['I'] = new[] { 14, 4, 4, 4, 4, 4, 14 },
        ['M'] = new[] { 17, 27, 21, 21, 17, 17, 17 },
        ['N'] = new[] { 17, 25, 21, 19, 17, 17, 17 },
        ['R'] = new[] { 30, 17, 17, 30, 20, 18, 17 },
        ['S'] = new[] { 15, 16, 16, 14, 1, 1, 30 },
        ['Y'] = new[] { 17, 17, 10, 4, 4, 4, 4 },
    };

    /// <summary>Lettres en relief sur une façade, centrées en (a, y) ; pixels fusionnés par segments horizontaux.</summary>
    private static void PixelText(MeshKit kit, Facade f, string text, float a, float y, float n, Material m, float px = 0.16f)
    {
        var w = text.Length * 6 * px - px;
        var x0 = a - w / 2;
        f.Solid(false); // lettres en léger relief : hors collision
        try { PixelGlyphs(f, text, x0, y, n, m, px); }
        finally { f.Solid(true); }
    }

    private static void PixelGlyphs(Facade f, string text, float x0, float y, float n, Material m, float px)
    {
        for (var ci = 0; ci < text.Length; ci++)
        {
            if (!Font.TryGetValue(text[ci], out var rows)) continue;
            for (var r = 0; r < 7; r++)
            {
                var bits = rows[r];
                var c = 0;
                while (c < 5)
                {
                    if ((bits & (16 >> c)) == 0) { c++; continue; }
                    var start = c;
                    while (c < 5 && (bits & (16 >> c)) != 0) c++;
                    var len = c - start;
                    f.Box(m, x0 + (ci * 6 + start + len / 2f) * px, y + (3 - r) * px, n, len * px, px, 0.06f);
                }
            }
        }
    }
}
