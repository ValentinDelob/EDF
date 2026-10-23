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
        new(0.55f, 0.08f, 0.07f), new(0.08f, 0.14f, 0.32f), new(0.1f, 0.3f, 0.18f), new(0.92f, 0.9f, 0.86f),
        new(0.08f, 0.08f, 0.09f), new(0.85f, 0.42f, 0.08f), new(0.05f, 0.4f, 0.42f), new(0.62f, 0.5f, 0.1f),
    };

    private static readonly Color[] LetterColors =
    {
        new(0.97f, 0.96f, 0.92f), new(1f, 0.85f, 0.3f), new(0.95f, 0.95f, 0.95f), new(0.1f, 0.1f, 0.1f),
    };

    private static readonly Color[] AwningColors =
    {
        new(0.12f, 0.28f, 0.16f), new(0.45f, 0.08f, 0.09f), new(0.1f, 0.14f, 0.3f), new(0.72f, 0.66f, 0.54f),
        new(0.12f, 0.12f, 0.12f), new(0.75f, 0.38f, 0.1f), new(0.32f, 0.3f, 0.28f),
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
            return b;
        }
        b.Visual = new MeshInstance3D { Mesh = style.Kind == BuildingKind.Industrial ? BuildPlainMesh(size, facade, roof) : BuildMesh(size, facade, roof, style, rng) };
        b.AddChild(b.Visual);
        b.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = new Vector3(0, size.Y / 2, 0) });
        return b;
    }

    /// <summary>Volume simple (entrepôt, conteneurs) : quatre façades à UV en mètres (1 U = 3 m) et un toit.</summary>
    private static ArrayMesh BuildPlainMesh(Vector3 s, Material facade, Material roof)
    {
        var kit = new MeshKit();
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var h = s.Y;
        foreach (var sd in Sides(hx, hz))
        {
            var a = sd.Start;
            var bb = sd.Start + sd.Along * sd.Length;
            var c = bb with { Y = h };
            var d = a with { Y = h };
            var u = sd.Length / 3f;
            var v = h / 3f;
            kit.Quad(facade, a, bb, c, d, new Vector2(0, v), new Vector2(u, v), new Vector2(u, 0), new Vector2(0, 0));
        }
        kit.Quad(roof, new Vector3(-hx, h, hz), new Vector3(hx, h, hz), new Vector3(hx, h, -hz), new Vector3(-hx, h, -hz),
            new Vector2(0, s.Z / 3f), new Vector2(s.X / 3f, s.Z / 3f), new Vector2(s.X / 3f, 0), new Vector2(0, 0));
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
    private static ArrayMesh BuildMesh(Vector3 s, Material facade, Material roof, BuildingStyle style, RandomNumberGenerator rng)
    {
        var kit = new MeshKit();
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var h = s.Y;
        var detail = Mats.Detail();
        var metal = Mats.DetailMetal();
        var shops = style.Shops && h > 7f;
        var baseY = shops ? ShopH : 0f;
        var sides = Sides(hx, hz);

        // Façades courantes.
        foreach (var sd in sides)
        {
            var a = sd.Start + Vector3.Up * baseY;
            var bb = sd.Start + sd.Along * sd.Length + Vector3.Up * baseY;
            var c = bb with { Y = h };
            var d = a with { Y = h };
            var u = sd.Length / 7f;
            var v0 = (h - baseY) / 3.2f;
            kit.Quad(facade, a, bb, c, d, new Vector2(0, v0), new Vector2(u, v0), new Vector2(u, 0), new Vector2(0, 0));
        }
        // Toit : gravillons, étanchéité sombre ou claire, parfois végétalisé.
        var rr = rng.Randf();
        var roofMat = roof;
        if (rr < 0.7f)
        {
            roofMat = Mats.Detail();
            kit.Col = rr < 0.25f ? new Color(0.56f, 0.55f, 0.51f)
                : rr < 0.45f ? new Color(0.3f, 0.3f, 0.32f)
                : rr < 0.62f ? new Color(0.74f, 0.74f, 0.72f)
                : new Color(0.33f, 0.42f, 0.22f);
        }
        kit.Quad(roofMat,new Vector3(-hx, h, hz), new Vector3(hx, h, hz), new Vector3(hx, h, -hz), new Vector3(-hx, h, -hz));

        var tower = style.Kind == BuildingKind.Tower;
        var trim = style.Variant is 4 or 8 ? new Color(0.78f, 0.76f, 0.7f)
            : style.Variant == 9 ? new Color(0.9f, 0.89f, 0.86f)
            : new Color(0.7f, 0.69f, 0.65f);
        var trimMat = tower ? metal : detail;
        if (tower) trim = new Color(0.55f, 0.57f, 0.6f);

        // Rez-de-chaussée : vitrines, piliers, bandeau, enseignes et stores ; sinon un socle en pierre.
        if (shops) BuildShops(kit, sides, style, rng, trim, trimMat);
        else
        {
            kit.Col = new Color(0.38f, 0.37f, 0.35f);
            kit.Ring(detail, hx, hz, -0.1f, 0.6f, 0.3f, 0.06f);
        }

        // Couronnement : corniche + acrotère (tours : bandeau métallique haut qui masque les équipements).
        kit.Col = trim;
        if (tower)
        {
            kit.Ring(trimMat, hx, hz, h - 0.25f, h + 1.8f, 0.25f, 0.06f);
        }
        else
        {
            kit.Ring(trimMat, hx, hz, h - 0.5f, h - 0.2f, 0.5f, 0.24f);
            kit.Ring(trimMat, hx, hz, h - 0.2f, h + 0.75f, 0.28f, 0.06f);
            kit.Col = trim.Darkened(0.15f);
            kit.Ring(trimMat, hx, hz, h + 0.75f, h + 0.85f, 0.36f, 0.1f); // couvertine
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
                    var lm = style.Night && lc.Luminance > 0.5f ? Mats.Lamps() : detail;
                    var textW = sw * rng.RandfRange(0.5f, 0.75f);
                    var words = rng.RandiRange(1, 3);
                    var x0 = -textW / 2;
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
        kit.Ring(trimMat, hx, hz, ShopH - 0.04f, ShopH + 0.26f, 0.45f, 0.22f);
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
        var slab = style.Variant == 9 ? new Color(0.9f, 0.89f, 0.86f) : new Color(0.72f, 0.7f, 0.66f);

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
        kit.Col = new Color(0.62f, 0.61f, 0.58f);
        kit.Box(detail, sp + Vector3.Up * (sh / 2), new Vector3(sx, sh, sz));
        kit.Col = new Color(0.5f, 0.49f, 0.47f);
        kit.Box(detail, sp + Vector3.Up * (sh + 0.06f), new Vector3(sx + 0.2f, 0.12f, sz + 0.2f));
        kit.Col = new Color(0.25f, 0.26f, 0.27f);
        kit.Box(metal, sp + new Vector3(0, 1.05f, sz / 2 + 0.01f), new Vector3(0.95f, 2.05f, 0.04f)); // porte

        // Groupes de climatisation (caisson + ventilateur).
        var nac = rng.RandiRange(1, tower ? 5 : 3);
        for (var i = 0; i < nac; i++)
        {
            var size = new Vector3(rng.RandfRange(1.1f, 2.4f), rng.RandfRange(0.8f, 1.3f), rng.RandfRange(1f, 1.6f));
            var p = new Vector3(Rx(size.X / 2 + 0.8f), h, Rz(size.Z / 2 + 0.8f));
            kit.Col = new Color(0.72f, 0.73f, 0.72f);
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
                kit.Col = new Color(0.85f, 0.85f, 0.83f);
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
        }));
        Effects.Debris(world, pos, Size, FacadeMat);
    }
}
