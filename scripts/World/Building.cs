using Godot;

namespace Rempart;

/// <summary>Typologie d'immeuble (choisit les détails : balcons, couronnement, équipements de toit).</summary>
public enum BuildingKind { Office, Residential, Tower, Industrial, House }

/// <summary>Usage d'un bâtiment de type <see cref="BuildingKind.House"/> (banlieue pavillonnaire).</summary>
public enum HouseUse { Home, Garage, Church, Belfry, Gym, Market, Hall }

/// <summary>Forme de toit des bâtiments de type <see cref="BuildingKind.House"/>.</summary>
public enum RoofShape { Gable, Hip, Spire, Flat }

/// <summary>Paramètres visuels d'un immeuble (n'affectent ni l'empreinte ni la collision).</summary>
public struct BuildingStyle
{
    public BuildingKind Kind;
    public int Variant;       // variante de façade (Mats.Facade)
    public bool Shops;        // rez-de-chaussée commercial (vitrines, enseignes, stores)
    public bool Night;
    public float Clearance;   // place libre devant les façades (retrait sur le trottoir), en m

    // Bâtiments de type House uniquement (voir Building.House.cs).
    public HouseUse Use;
    public RoofShape Roof;
    public int Facing;        // façade principale : 0 = +Z, 1 = -Z, 2 = +X, 3 = -X
    public float RoofPitch;   // hauteur du faîtage / demi-portée
    public Color WallColor;
    public Color RoofColor;
    public Color TrimColor;   // volets, porte, portail
}

/// <summary>Immeuble destructible : s'effondre (enfoncement + poussière + débris) à 0 PV.</summary>
public partial class Building : StaticBody3D
{
    public int Id;
    public float Health;
    public float MaxHealth;
    public Vector3 Size;
    public bool Destroyed;
    public MeshInstance3D Visual;
    public Material FacadeMat;

    /// <summary>Hauteur du socle commercial : un étage courant (3,2 m) + le demi-niveau bas (0,6 m), aligné sur la trame de façade.</summary>
    private const float ShopH = 3.8f;

    private static readonly Color[] SignColors =
    {
        new(0.36f, 0.14f, 0.12f), new(0.13f, 0.16f, 0.24f), new(0.15f, 0.24f, 0.18f), new(0.62f, 0.61f, 0.58f),
        new(0.09f, 0.09f, 0.095f), new(0.5f, 0.32f, 0.14f), new(0.13f, 0.27f, 0.28f), new(0.44f, 0.38f, 0.17f),
    };

    private static readonly Color[] LetterColors =
    {
        new(0.76f, 0.75f, 0.71f), new(0.72f, 0.62f, 0.3f), new(0.72f, 0.72f, 0.7f), new(0.1f, 0.1f, 0.1f),
    };

    private static readonly Color[] AwningColors =
    {
        new(0.17f, 0.24f, 0.18f), new(0.32f, 0.13f, 0.12f), new(0.14f, 0.17f, 0.25f), new(0.5f, 0.47f, 0.41f),
        new(0.13f, 0.13f, 0.13f), new(0.45f, 0.3f, 0.15f), new(0.28f, 0.27f, 0.25f),
    };

    public static Building Create(int id, Vector3 center, Vector3 size, Material facade, Material roof) =>
        Create(id, center, size, facade, roof, new BuildingStyle { Kind = BuildingKind.Office, Clearance = 1.5f });

    public static Building Create(int id, Vector3 center, Vector3 size, Material facade, Material roof, BuildingStyle style)
    {
        var b = new Building
        {
            Id = id,
            Size = size,
            Name = $"Building{id}",
            CollisionLayer = 1,
            CollisionMask = 0,
            FacadeMat = facade,
        };
        b.Position = center;
        var floors = Mathf.Max(1, size.Y / 3.2f);
        b.MaxHealth = b.Health = 350 + floors * 140 + size.X * size.Z * 0.6f;

        // Tous les détails (vitrines, corniches, balcons, équipements de toit) sont fusionnés dans
        // le maillage unique de l'immeuble : ils s'enfoncent avec lui lors de l'effondrement.
        var rng = new RandomNumberGenerator { Seed = (ulong)(id * 7919 + 3) };
        if (style.Kind == BuildingKind.House)
        {
            // Maison ou équipement de bourg : le toit en pente a sa propre forme de collision.
            b.Visual = new MeshInstance3D { Mesh = BuildHouseMesh(size, style, rng, out var roofShape) };
            b.AddChild(b.Visual);
            b.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = new Vector3(0, size.Y / 2, 0) });
            if (roofShape != null) b.AddChild(new CollisionShape3D { Shape = roofShape });
            b.AddDetailCollision();
            return b;
        }
        b.Visual = new MeshInstance3D { Mesh = style.Kind == BuildingKind.Industrial ? BuildPlainMesh(size, facade, roof, Tint(id)) : BuildMesh(size, facade, roof, style, rng, Tint(id)) };
        b.AddChild(b.Visual);
        b.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = new Vector3(0, size.Y / 2, 0) });
        b.AddDetailCollision();
        return b;
    }

    /// <summary>
    /// Collision exacte de tout ce qui dépasse du volume principal (acrotère, corniches, socle, piliers, balcons,
    /// stores, enseignes, équipements de toit, débords de toiture, cheminées...) : les triangles solides du maillage,
    /// en une seule forme par immeuble. Le volume principal reste une boîte pleine (robuste pour les tirs et les pas).
    /// </summary>
    private void AddDetailCollision()
    {
        if (MeshKit.ShapeOf(Visual.Mesh) is { } detail) AddChild(new CollisionShape3D { Name = "Detail", Shape = detail });
    }

    /// <summary>Chanfrein des arêtes verticales du volume principal (m) : la lumière accroche les angles.</summary>
    private const float Bevel = 0.22f;

    /// <summary>
    /// Teinte propre à l'immeuble (multipliée à la façade) : légère variation de valeur et de température,
    /// sans tirage aléatoire (identifiants et ordre de génération inchangés).
    /// </summary>
    private static Color Tint(int id)
    {
        var a = Mathf.PosMod(Mathf.Sin(id * 12.9898f + 4.1f) * 43758.547f, 1f);
        var b = Mathf.PosMod(Mathf.Sin(id * 78.233f + 1.7f) * 24634.635f, 1f);
        var k = 0.8f + 0.22f * a;
        var t = (b - 0.5f) * 0.07f;
        return new Color(k * (1 + t), k, k * (1 - t * 1.3f));
    }

    /// <summary>
    /// Murs du volume principal aux angles chanfreinés : UV en mètres (1 U = <paramref name="uLen"/> m, V compté
    /// depuis le haut, 1 V = <paramref name="vLen"/> m), teinte de l'immeuble, bande assombrie sous l'acrotère
    /// (ruissellement du toit) par couleur de sommet.
    /// </summary>
    private static void BeveledWalls(MeshKit kit, Material m, Vector3 s, float y0, float bevel, float uLen, float vLen, Color tint)
    {
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var h = s.Y;
        var dark = new Color(tint.R * 0.7f, tint.G * 0.69f, tint.B * 0.68f);
        var yb = h - Mathf.Min(2.6f, (h - y0) * 0.3f);
        float V(float y) => (h - y) / vLen;
        void Wall(Vector3 a, Vector3 b, float u0, float u1, Vector3 n)
        {
            kit.QuadC(m, a with { Y = y0 }, b with { Y = y0 }, b with { Y = yb }, a with { Y = yb },
                new Vector2(u0, V(y0)), new Vector2(u1, V(y0)), new Vector2(u1, V(yb)), new Vector2(u0, V(yb)), tint, tint, tint, tint, n);
            kit.QuadC(m, a with { Y = yb }, b with { Y = yb }, b with { Y = h }, a with { Y = h },
                new Vector2(u0, V(yb)), new Vector2(u1, V(yb)), new Vector2(u1, 0), new Vector2(u0, 0), tint, tint, dark, dark, n);
        }
        foreach (var sd in Sides(hx, hz))
            Wall(sd.Start + sd.Along * bevel, sd.Start + sd.Along * (sd.Length - bevel), bevel / uLen, (sd.Length - bevel) / uLen, sd.Normal);
        // Pans coupés des quatre angles (arêtes impaires de l'octogone).
        var o = MeshKit.Octagon(hx, hz, bevel);
        for (var k = 1; k < 8; k += 2)
        {
            var p = o[k];
            var q = o[(k + 1) % 8];
            Wall(new Vector3(p.X, 0, p.Y), new Vector3(q.X, 0, q.Y), 0, bevel * 1.414f / uLen, new Vector3(p.X + q.X, 0, p.Y + q.Y));
        }
    }

    /// <summary>Toit plat octogonal (suit les angles chanfreinés), en éventail depuis le centre.</summary>
    private static void BeveledRoof(MeshKit kit, Material m, Vector3 s, float bevel)
    {
        var o = MeshKit.Octagon(s.X / 2, s.Z / 2, bevel);
        var c = new Vector3(0, s.Y, 0);
        for (var k = 0; k < 8; k++)
        {
            var p = o[k];
            var q = o[(k + 1) % 8];
            kit.Tri(m, c, new Vector3(p.X, s.Y, p.Y), new Vector3(q.X, s.Y, q.Y), c + Vector3.Down);
        }
    }

    /// <summary>Volume simple (entrepôt, conteneurs) : quatre façades à UV en mètres (1 U = 3 m), angles chanfreinés, et un toit.</summary>
    private static ArrayMesh BuildPlainMesh(Vector3 s, Material facade, Material roof, Color tint)
    {
        var kit = new MeshKit();
        var bevel = Mathf.Min(0.2f, Mathf.Min(s.X, s.Z) * 0.025f);
        BeveledWalls(kit, facade, s, 0, bevel, 3f, 3f, tint);
        kit.Col = tint;
        BeveledRoof(kit, roof, s, bevel);
        return kit.Commit();
    }

    /// <summary>Côté de façade : coin gauche (vu de l'extérieur), direction le long, normale sortante, longueur.</summary>
    private readonly record struct Side(Vector3 Start, Vector3 Along, Vector3 Normal, float Length);

    private static Side[] Sides(float hx, float hz) => new[]
    {
        new Side(new Vector3(-hx, 0, hz), Vector3.Right, Vector3.Back, hx * 2),
        new Side(new Vector3(hx, 0, -hz), Vector3.Left, Vector3.Forward, hx * 2),
        new Side(new Vector3(hx, 0, hz), Vector3.Forward, Vector3.Right, hz * 2),
        new Side(new Vector3(-hx, 0, -hz), Vector3.Back, Vector3.Left, hz * 2),
    };

    /// <summary>
    /// Maillage de l'immeuble. Façades : UV en mètres (1 U = 7 m, 1 V = 3,2 m), comptés depuis le haut
    /// pour que la trame d'étages tombe juste sous l'acrotère.
    /// </summary>
    private static ArrayMesh BuildMesh(Vector3 s, Material facade, Material roof, BuildingStyle style, RandomNumberGenerator rng, Color tint)
    {
        var kit = new MeshKit { Record = true };
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var h = s.Y;
        var detail = Mats.Detail();
        var metal = Mats.DetailMetal();
        var shops = style.Shops && h > 7f;
        var baseY = shops ? ShopH : 0f;
        var sides = Sides(hx, hz);

        // Façades courantes, angles chanfreinés.
        BeveledWalls(kit, facade, s, baseY, Bevel, 7f, 3.2f, tint);
        // Toit : gravillons, étanchéité sombre ou claire, parfois une ancienne toiture végétalisée desséchée.
        var rr = rng.Randf();
        var roofMat = roof;
        if (rr < 0.7f)
        {
            roofMat = Mats.Detail();
            kit.Col = rr < 0.25f ? new Color(0.46f, 0.45f, 0.42f)
                : rr < 0.45f ? new Color(0.24f, 0.24f, 0.25f)
                : rr < 0.62f ? new Color(0.56f, 0.56f, 0.54f)
                : new Color(0.3f, 0.31f, 0.22f);
        }
        BeveledRoof(kit, roofMat, s, Bevel);

        var tower = style.Kind == BuildingKind.Tower;
        var trim = style.Variant is 4 or 8 ? new Color(0.56f, 0.55f, 0.52f)
            : style.Variant == 9 ? new Color(0.62f, 0.61f, 0.59f)
            : new Color(0.52f, 0.51f, 0.49f);
        var trimMat = tower ? metal : detail;
        if (tower) trim = new Color(0.42f, 0.43f, 0.45f);

        // Rez-de-chaussée : vitrines, piliers, bandeau, enseignes et stores ; sinon un socle en pierre.
        if (shops) BuildShops(kit, sides, style, rng, trim, trimMat);
        else
        {
            kit.Col = new Color(0.3f, 0.295f, 0.28f);
            kit.Ring(detail, hx, hz, -0.1f, 0.6f, 0.3f, 0.06f, Bevel);
        }

        // Couronnement : corniche + acrotère (tours : bandeau métallique haut qui masque les équipements).
        kit.Col = trim;
        if (tower)
        {
            kit.Ring(trimMat, hx, hz, h - 0.25f, h + 1.8f, 0.25f, 0.06f, Bevel);
        }
        else
        {
            kit.Ring(trimMat, hx, hz, h - 0.5f, h - 0.2f, 0.5f, 0.24f, Bevel);
            kit.Ring(trimMat, hx, hz, h - 0.2f, h + 0.75f, 0.28f, 0.06f, Bevel);
            kit.Col = trim.Darkened(0.2f);
            kit.Ring(trimMat, hx, hz, h + 0.75f, h + 0.85f, 0.36f, 0.1f, Bevel); // couvertine
        }

        if (style.Kind == BuildingKind.Residential) BuildBalconies(kit, sides, h, baseY, style, rng);
        BuildRoofTop(kit, s, style, rng);
        return kit.Commit();
    }

    private static void BuildShops(MeshKit kit, Side[] sides, BuildingStyle style, RandomNumberGenerator rng, Color trim, Material trimMat)
    {
        var store = Mats.Storefront(style.Night);
        var detail = Mats.Detail();
        var tower = style.Kind == BuildingKind.Tower;
        foreach (var sd in sides)
        {
            // Vitrine continue (UV : 1 U = 3,5 m, 1 V = hauteur du socle).
            var a = sd.Start;
            var bb = sd.Start + sd.Along * sd.Length;
            var u = sd.Length / 3.5f;
            kit.Quad(store, a, bb, bb + Vector3.Up * ShopH, a + Vector3.Up * ShopH, new Vector2(0, 1), new Vector2(u, 1), new Vector2(u, 0), new Vector2(0, 0));

            var n = Mathf.Max(1, Mathf.RoundToInt(sd.Length / (tower ? 10f : 7.5f)));
            var w = sd.Length / n;
            for (var i = 0; i <= n; i++)
            {
                // Piliers en pierre entre les boutiques.
                var p = sd.Start + sd.Along * (i * w) + sd.Normal * 0.04f;
                kit.Col = trim.Darkened(0.08f);
                var pw = i == 0 || i == n ? 0.5f : 0.4f;
                var off = i == 0 ? pw / 2 : i == n ? -pw / 2 : 0f;
                kit.Box(detail, p + sd.Along * off + Vector3.Up * (ShopH / 2), Vector3.Zero + Abs(sd.Along) * pw + Abs(sd.Normal) * 0.16f + Vector3.Up * ShopH);
                if (i == n || tower) continue;

                var mid = sd.Start + sd.Along * ((i + 0.5f) * w);
                // Enseigne : panneau coloré + bandeau de lettrage (éclairé la nuit).
                if (rng.Randf() < 0.8f)
                {
                    var sw = w * rng.RandfRange(0.5f, 0.82f);
                    var col = SignColors[rng.RandiRange(0, SignColors.Length - 1)];
                    kit.Col = col;
                    kit.Box(detail, mid + sd.Normal * 0.1f + Vector3.Up * 3.38f, Abs(sd.Along) * sw + Abs(sd.Normal) * 0.12f + Vector3.Up * 0.62f);
                    var lc = LetterColors[rng.RandiRange(0, LetterColors.Length - 1)];
                    if (lc.Luminance > 0.5f == col.Luminance > 0.5f) lc = col.Luminance > 0.5f ? new Color(0.08f, 0.08f, 0.1f) : new Color(0.97f, 0.96f, 0.92f);
                    // Lettrage : quelques blocs de largeur variable (mots), lumineux la nuit.
                    kit.Col = lc;
                    Material lm = style.Night && lc.Luminance > 0.5f ? Mats.Lamps() : detail;
                    var textW = sw * rng.RandfRange(0.5f, 0.75f);
                    var words = rng.RandiRange(1, 3);
                    var x0 = -textW / 2;
                    kit.Solid = false; // lettres de 3 cm d'épaisseur sur le panneau : la collision du panneau suffit
                    for (var wi = 0; wi < words; wi++)
                    {
                        var ww = (textW - (words - 1) * 0.25f) / words * rng.RandfRange(0.7f, 1.1f);
                        var letters = Mathf.Max(2, Mathf.RoundToInt(ww / 0.3f));
                        var lw = ww / letters;
                        for (var li = 0; li < letters; li++)
                        {
                            var lh = rng.Randf() < 0.2f ? 0.3f : 0.22f;
                            kit.Box(lm, mid + sd.Along * (x0 + (li + 0.5f) * lw) + sd.Normal * 0.17f + Vector3.Up * (3.27f + lh / 2),
                                Abs(sd.Along) * (lw * 0.78f) + Abs(sd.Normal) * 0.03f + Vector3.Up * lh);
                        }
                        x0 += ww + 0.25f;
                    }
                    kit.Solid = true;
                }
                // Store banne incliné avec lambrequin.
                var depth = Mathf.Min(1.4f, style.Clearance - 0.3f);
                if (depth > 0.7f && rng.Randf() < 0.5f)
                {
                    var aw = w * rng.RandfRange(0.6f, 0.85f);
                    const float drop = 0.45f;
                    var ang = Mathf.Atan2(drop, depth);
                    var z = (sd.Normal * Mathf.Cos(ang) - Vector3.Up * Mathf.Sin(ang)).Normalized();
                    var x = sd.Along;
                    var y = z.Cross(x).Normalized();
                    var len = Mathf.Sqrt(depth * depth + drop * drop);
                    kit.Col = AwningColors[rng.RandiRange(0, AwningColors.Length - 1)];
                    var top = mid + Vector3.Up * 2.95f;
                    kit.Box(detail, top + z * (len / 2), new Vector3(aw, 0.05f, len), new Basis(x, y, z));
                    var edge = top + sd.Normal * depth - Vector3.Up * drop;
                    kit.Box(detail, edge + Vector3.Down * 0.13f + sd.Normal * 0.02f, Abs(sd.Along) * aw + Abs(sd.Normal) * 0.04f + Vector3.Up * 0.26f);
                    // Bras de store.
                    kit.Col = new Color(0.2f, 0.2f, 0.2f);
                    kit.Box(detail, mid + sd.Normal * (depth * 0.5f) + Vector3.Up * 2.55f + sd.Along * (aw * 0.45f), Abs(sd.Along) * 0.04f + Abs(sd.Normal) * depth + Vector3.Up * 0.04f);
                    kit.Box(detail, mid + sd.Normal * (depth * 0.5f) + Vector3.Up * 2.55f - sd.Along * (aw * 0.45f), Abs(sd.Along) * 0.04f + Abs(sd.Normal) * depth + Vector3.Up * 0.04f);
                }
            }
        }
        // Bandeau saillant au-dessus des vitrines.
        kit.Col = trim;
        var hx = sides[0].Length / 2;
        var hz = sides[2].Length / 2;
        kit.Ring(trimMat, hx, hz, ShopH - 0.04f, ShopH + 0.26f, 0.45f, 0.22f, Bevel);
    }

    private static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.X), Mathf.Abs(v.Y), Mathf.Abs(v.Z));

    private static void BuildBalconies(MeshKit kit, Side[] sides, float h, float baseY, BuildingStyle style, RandomNumberGenerator rng)
    {
        var depth = Mathf.Min(1.3f, style.Clearance - 0.35f);
        var full = depth >= 0.8f;       // vrais balcons ; sinon simples garde-corps de fenêtre
        if (!full) depth = 0.22f;
        var detail = Mats.Detail();
        var metal = Mats.DetailMetal();
        var glass = Mats.Glass();
        var modern = style.Variant == 9;
        var cols = style.Variant switch { 9 => 2, 3 => 2, _ => 3 };
        var colW = 7f / cols;
        var every = rng.Randf() < 0.5f ? 1 : 2;
        var phase = rng.RandiRange(0, 1);
        var bw = Mathf.Min(colW * 0.85f, 2.8f);
        var rail = modern ? new Color(0.15f, 0.15f, 0.16f) : new Color(0.12f, 0.12f, 0.12f);
        var slab = style.Variant == 9 ? new Color(0.62f, 0.61f, 0.59f) : new Color(0.52f, 0.51f, 0.48f);

        foreach (var sd in sides)
        {
            if (sd.Length < 7.5f) continue;
            for (var k = 0; k < 40; k++)
            {
                var yf = h - (k + 1) * 3.2f + 0.16f; // niveau du plancher de l'étage k (compté depuis le haut)
                if (yf < baseY + 2.6f) break;
                for (var ci = 0; ; ci++)
                {
                    var along = (ci + 0.5f) * colW;
                    if (along > sd.Length - 1.2f) break;
                    if (along < 1.2f || (ci + phase) % every != 0) continue;
                    var p = sd.Start + sd.Along * along + Vector3.Up * yf;
                    var ax = Abs(sd.Along);
                    var an = Abs(sd.Normal);
                    var w = full ? bw : colW * 0.55f;
                    if (full)
                    {
                        kit.Col = slab;
                        kit.Box(detail, p + sd.Normal * (depth / 2) - Vector3.Up * 0.09f, ax * w + an * depth + Vector3.Up * 0.18f);
                    }
                    // Garde-corps : verre (moderne) ou tôle pleine, main courante métallique.
                    var front = p + sd.Normal * (depth - 0.03f) + Vector3.Up * 0.5f;
                    if (modern)
                    {
                        kit.Box(glass, front, ax * w + an * 0.03f + Vector3.Up * 0.95f);
                        if (full)
                        {
                            kit.Box(glass, p + sd.Normal * (depth / 2) + sd.Along * (w / 2 - 0.02f) + Vector3.Up * 0.5f, ax * 0.03f + an * depth + Vector3.Up * 0.95f);
                            kit.Box(glass, p + sd.Normal * (depth / 2) - sd.Along * (w / 2 - 0.02f) + Vector3.Up * 0.5f, ax * 0.03f + an * depth + Vector3.Up * 0.95f);
                        }
                    }
                    else
                    {
                        kit.Col = rail;
                        kit.Box(metal, front + Vector3.Down * 0.1f, ax * w + an * 0.03f + Vector3.Up * 0.75f);
                        if (full)
                        {
                            kit.Box(metal, p + sd.Normal * (depth / 2) + sd.Along * (w / 2 - 0.02f) + Vector3.Up * 0.4f, ax * 0.03f + an * depth + Vector3.Up * 0.75f);
                            kit.Box(metal, p + sd.Normal * (depth / 2) - sd.Along * (w / 2 - 0.02f) + Vector3.Up * 0.4f, ax * 0.03f + an * depth + Vector3.Up * 0.75f);
                        }
                    }
                    kit.Col = rail;
                    kit.Box(metal, front + Vector3.Up * 0.5f, ax * (w + 0.04f) + an * 0.06f + Vector3.Up * 0.05f);
                }
            }
        }
    }

    private static void BuildRoofTop(MeshKit kit, Vector3 s, BuildingStyle style, RandomNumberGenerator rng)
    {
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var h = s.Y;
        var detail = Mats.Detail();
        var metal = Mats.DetailMetal();
        float Rx(float margin) => rng.RandfRange(-hx + margin, hx - margin);
        float Rz(float margin) => rng.RandfRange(-hz + margin, hz - margin);
        var tower = style.Kind == BuildingKind.Tower;

        // Cage d'escalier / machinerie d'ascenseur.
        var sx = Mathf.Min(rng.RandfRange(2.6f, 4f), s.X - 2.5f);
        var sz = Mathf.Min(rng.RandfRange(2.6f, 4.5f), s.Z - 2.5f);
        var sh = rng.RandfRange(2.4f, 3.2f);
        var sp = new Vector3(Rx(sx / 2 + 1f), h, Rz(sz / 2 + 1f));
        kit.Col = new Color(0.47f, 0.46f, 0.44f);
        kit.Box(detail, sp + Vector3.Up * (sh / 2), new Vector3(sx, sh, sz));
        kit.Col = new Color(0.38f, 0.375f, 0.36f);
        kit.Box(detail, sp + Vector3.Up * (sh + 0.06f), new Vector3(sx + 0.2f, 0.12f, sz + 0.2f));
        kit.Col = new Color(0.25f, 0.26f, 0.27f);
        kit.Box(metal, sp + new Vector3(0, 1.05f, sz / 2 + 0.01f), new Vector3(0.95f, 2.05f, 0.04f)); // porte

        // Groupes de climatisation (caisson + ventilateur).
        var nac = rng.RandiRange(1, tower ? 5 : 3);
        for (var i = 0; i < nac; i++)
        {
            var size = new Vector3(rng.RandfRange(1.1f, 2.4f), rng.RandfRange(0.8f, 1.3f), rng.RandfRange(1f, 1.6f));
            var p = new Vector3(Rx(size.X / 2 + 0.8f), h, Rz(size.Z / 2 + 0.8f));
            kit.Col = new Color(0.5f, 0.51f, 0.5f);
            kit.Box(metal, p + Vector3.Up * (size.Y / 2), size);
            kit.Col = new Color(0.1f, 0.1f, 0.11f);
            kit.Cylinder(detail, p + Vector3.Up * size.Y, size.Z * 0.35f, size.Z * 0.35f, 0.03f, 10);
        }

        if (tower)
        {
            // Mât de télécommunication avec feu de balisage.
            var mh = rng.RandfRange(7f, 14f);
            var mp = new Vector3(Rx(2f), h, Rz(2f));
            kit.Col = new Color(0.6f, 0.6f, 0.62f);
            kit.Cylinder(metal, mp, 0.22f, 0.08f, mh, 6);
            kit.Col = new Color(1f, 0.1f, 0.05f);
            kit.Box(Mats.Lamps(), mp + Vector3.Up * (mh + 0.1f), new Vector3(0.25f, 0.2f, 0.25f));
            return;
        }

        // Château d'eau en bois ou en métal sur pieds.
        if (s.X > 9 && s.Z > 9 && rng.Randf() < 0.35f)
        {
            var r = rng.RandfRange(1.1f, 1.5f);
            var th = rng.RandfRange(2f, 2.8f);
            var p = new Vector3(Rx(r + 1.2f), h, Rz(r + 1.2f));
            var wood = rng.Randf() < 0.6f;
            kit.Col = new Color(0.2f, 0.2f, 0.2f);
            for (var l = 0; l < 4; l++)
            {
                var a = l * Mathf.Tau / 4 + Mathf.Pi / 4;
                kit.Box(metal, p + new Vector3(Mathf.Cos(a) * r * 0.75f, 0.65f, Mathf.Sin(a) * r * 0.75f), new Vector3(0.12f, 1.3f, 0.12f));
            }
            kit.Col = wood ? new Color(0.42f, 0.32f, 0.22f) : new Color(0.6f, 0.62f, 0.62f);
            kit.Cylinder(wood ? detail : metal, p + Vector3.Up * 1.3f, r, r, th, 12);
            kit.Col = new Color(0.22f, 0.22f, 0.23f);
            kit.Cylinder(metal, p + Vector3.Up * (1.3f + th), r + 0.08f, 0f, r * 0.6f, 12, true);
        }

        // Panneaux solaires sur les toits bas.
        if (s.Y < 18 && rng.Randf() < 0.3f && s.X > 10 && s.Z > 10)
        {
            var rows = Mathf.FloorToInt((s.Z - 4f) / 2.2f);
            var pw = s.X - 5f;
            for (var r = 0; r < Mathf.Min(rows, 5); r++)
            {
                var p = new Vector3(0, h + 0.55f, -hz + 2.5f + r * 2.2f);
                if (Mathf.Abs(p.X - sp.X) < sx / 2 + pw / 2 && Mathf.Abs(p.Z - sp.Z) < sz / 2 + 1.2f) continue;
                kit.Col = new Color(0.07f, 0.09f, 0.16f);
                kit.Box(metal, p, new Vector3(pw, 0.06f, 1.6f), new Basis(Vector3.Right, Mathf.DegToRad(-25)));
            }
        }

        // Antennes et paraboles.
        var nant = rng.RandiRange(0, 3);
        for (var i = 0; i < nant; i++)
        {
            var p = new Vector3(Rx(1f), h, Rz(1f));
            kit.Col = new Color(0.3f, 0.3f, 0.32f);
            if (rng.Randf() < 0.5f)
            {
                var ah = rng.RandfRange(2f, 5f);
                kit.Tube(metal, p, p + Vector3.Up * ah, 0.035f, 5);
                kit.Tube(metal, p + Vector3.Up * (ah * 0.8f) - Vector3.Right * 0.6f, p + Vector3.Up * (ah * 0.8f) + Vector3.Right * 0.6f, 0.02f, 4);
            }
            else
            {
                kit.Tube(metal, p, p + Vector3.Up * 1f, 0.04f, 5);
                kit.Col = new Color(0.58f, 0.58f, 0.56f);
                var yaw = rng.RandfRange(0, Mathf.Tau);
                var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, Mathf.DegToRad(60));
                kit.Cylinder(detail, p + Vector3.Up * 1f, 0.45f, 0.5f, 0.08f, 10, true, basis);
            }
        }
    }

    /// <summary>Appelé sur tous les pairs quand l'hôte annonce la destruction.</summary>
    public void Collapse(World world)
    {
        if (Destroyed) return;
        Destroyed = true;
        CollisionLayer = 0;
        foreach (var c in GetChildren())
            if (c is CollisionShape3D cs) cs.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);

        var pos = GlobalPosition;
        Sfx.At("collapse", pos + Vector3.Up * Size.Y * 0.3f, 6f, 1f, 0);
        Effects.Dust(world, pos, Size);
        world.ShakeCamera(pos, 0.9f, 80f);

        var tw = CreateTween();
        tw.SetParallel();
        foreach (var c in GetChildren())
            if (c is Node3D n)
                tw.TweenProperty(n, "position:y", n.Position.Y - Size.Y - 2f, 3.2f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        tw.TweenProperty(this, "rotation:z", (float)GD.RandRange(-0.06, 0.06), 3.2f);
        tw.Chain().TweenCallback(Callable.From(() =>
        {
            foreach (var c in GetChildren())
                if (c is MeshInstance3D m) m.Visible = false;
            Rotation = Vector3.Zero;
            // Tas de gravats.
            var rubble = new MeshInstance3D
            {
                Mesh = new CylinderMesh
                {
                    TopRadius = Mathf.Min(Size.X, Size.Z) * 0.25f,
                    BottomRadius = Mathf.Max(Size.X, Size.Z) * 0.55f,
                    Height = Mathf.Min(3.5f, Size.Y * 0.15f),
                    RadialSegments = 9,
                    Rings = 2,
                    Material = Mats.Concrete(new Color(0.45f, 0.43f, 0.4f), 9),
                },
                Position = new Vector3(0, Mathf.Min(3.5f, Size.Y * 0.15f) / 2 - 0.3f, 0),
            };
            AddChild(rubble);
            // Le tas de gravats est solide (on peut y monter, les insectes l'escaladent).
            AddChild(new CollisionShape3D { Name = "Rubble", Shape = rubble.Mesh.CreateConvexShape(), Position = rubble.Position });
            CollisionLayer = 1;
        }));
        Effects.Debris(world, pos, Size, FacadeMat);
    }
}
