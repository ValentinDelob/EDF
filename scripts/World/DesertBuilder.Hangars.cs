using Godot;

namespace Rempart;

/// <summary>
/// Bâtiments de l'aérodrome : grand hangar cintré (64 m) et hangar à charpente (58 m), ouverts au nord avec leurs
/// portes coulissantes arrachées ; abris bétonnés pour avions de combat ; tour de contrôle (socle, fût, vigie vitrée
/// à balcon, escalier extérieur jusqu'à la vigie). Hangars et abris : bâtiments ouverts à collision exacte (on y entre,
/// les insectes escaladent les parois et les toits) ; tour : socle plein + forme exacte du reste.
/// </summary>
public static partial class DesertBuilder
{
    private static readonly Vector3 HangarA = new(-150, 0, -70), HangarASize = new(64, 22, 46);
    private static readonly Vector3 HangarC = new(150, 0, -70), HangarCSize = new(58, 18, 44);
    private static readonly Vector3[] Shelters = { new(-92, 0, -74), new(-60, 0, -74) };
    private static readonly Vector3 ShelterSize = new(24, 10, 34);
    private static readonly Vector3 Tower = new(50, 0, -66), TowerBase = new(22, 7, 14);
    /// <summary>Hauteurs de la tour de contrôle : plancher de la vigie (balcon), toit.</summary>
    public const float CabFloor = 21f, CabRoof = 24.4f;
    /// <summary>Hangar à charpente : hauteur des murs (égout du toit) et du faîtage.</summary>
    public const float HangarCEave = 15f;

    private static void BuildHangars(Ctx c)
    {
        var clad = Mats.Corrugated(new Color(0.5f, 0.47f, 0.41f), 1701, 2.4f);
        // Grand hangar cintré (ouest) : épave d'avion de transport à l'intérieur.
        AddOpen(c, HangarA, HangarASize, ArchHangarMesh(HangarASize, 17), clad, 1.6f);
        WallBlocks(c, HangarA, HangarASize);
        {
            var k = c.Solid.At(HangarA);
            k.Xf = new Transform3D(new Basis(Vector3.Up, Mathf.Pi + 0.12f), HangarA + new Vector3(4, 0, 2));
            Props.TransportWreck(k);
            k.Xf = Transform3D.Identity;
            c.Ruins.Smolder(HangarA + new Vector3(2, 2.5f, 6), 2.2f);
            Decal(c, Mats.Decal.Scorch, HangarA + new Vector3(3, 0, 4), 30f, 0.4f, 0.024f);
        }
        // Hangar à charpente (est) : toit praticable par l'escalier extérieur.
        AddOpen(c, HangarC, HangarCSize, GableHangarMesh(HangarCSize, 23), clad, 1.4f);
        WallBlocks(c, HangarC, HangarCSize);
        // Abris bétonnés, chasseur calciné dans le premier.
        for (var i = 0; i < Shelters.Length; i++)
        {
            var p = Shelters[i];
            AddOpen(c, p, ShelterSize, ShelterMesh(ShelterSize, i), Mats.Concrete(new Color(0.5f, 0.48f, 0.44f), 61), 2.4f);
            WallBlocks(c, p, ShelterSize);
            if (i == 0)
            {
                var k = c.Solid.At(p);
                k.Xf = new Transform3D(new Basis(Vector3.Up, 0.08f), p + new Vector3(0.5f, 0, 1));
                Props.FighterWreck(k);
                k.Xf = Transform3D.Identity;
                c.Ruins.Fire(p + new Vector3(0.5f, 1.2f, -1f), 1.3f);
            }
        }
        // Tour de contrôle.
        AddBuilding(c, Tower, TowerBase, ControlTowerMesh(TowerBase), Mats.Concrete(new Color(0.55f, 0.52f, 0.46f), 63), 1.3f);
    }

    /// <summary>Bloque dans le champ de flux les murs latéraux et le fond d'un bâtiment ouvert au nord (-Z).</summary>
    private static void WallBlocks(Ctx c, Vector3 p, Vector3 s)
    {
        FlowBlock(c, p + new Vector3(-s.X / 2, 0, 0), new Vector3(1f, s.Y, s.Z), 0.6f);
        FlowBlock(c, p + new Vector3(s.X / 2, 0, 0), new Vector3(1f, s.Y, s.Z), 0.6f);
        FlowBlock(c, p + new Vector3(0, 0, s.Z / 2), new Vector3(s.X, s.Y, 1f), 0.6f);
    }

    /// <summary>Panneau de bardage entre deux points au sol, de y0 à y1, UV en m / 3 (tôle ondulée).</summary>
    private static void Clad(MeshKit k, Material m, Vector3 a, Vector3 b, float y0a, float y1a, float y0b, float y1b, Vector3 n, Color col)
    {
        var len = (b - a).Length();
        k.QuadC(m, a with { Y = y0a }, b with { Y = y0b }, b with { Y = y1b }, a with { Y = y1a },
            new Vector2(0, y1a / 3f - y0a / 3f), new Vector2(len / 3f, y1b / 3f - y0b / 3f), new Vector2(len / 3f, 0), new Vector2(0, 0),
            col, col, col * 0.82f, col * 0.82f, n);
    }

    /// <summary>Panneau visible des deux côtés (bardage vu de l'intérieur).</summary>
    private static void Clad2(MeshKit k, Material m, Vector3 a, Vector3 b, float y0a, float y1a, float y0b, float y1b, Vector3 n, Color col)
    {
        Clad(k, m, a, b, y0a, y1a, y0b, y1b, n, col);
        Clad(k, m, a, b, y0a, y1a, y0b, y1b, -n, col * 0.7f);
    }

    // ───────────────────────── Hangar cintré ─────────────────────────

    /// <summary>
    /// Grand hangar cintré : murs bas de 6 m, voûte en tôle sur arcs treillis tous les 6 m (panneaux arrachés par
    /// endroits), pignon arrière fermé, pignon avant ouvert sous un linteau, portes coulissantes (deux repliées, une
    /// arrachée et couchée au sol).
    /// </summary>
    private static ArrayMesh ArchHangarMesh(Vector3 s, int seed)
    {
        var k = new MeshKit { Record = true };
        var hx = s.X / 2;
        var hz = s.Z / 2;
        const float wall = 6f;
        var rise = s.Y - wall;
        var roof = Mats.Corrugated(new Color(0.46f, 0.44f, 0.4f), 1702, 2.4f);
        var clad = Mats.Corrugated(new Color(0.5f, 0.47f, 0.41f), 1701, 2.4f);
        var metal = Mats.DetailMetal();
        var concrete = Mats.Detail();
        float Y(float x) => wall + rise * Mathf.Sqrt(Mathf.Max(0f, 1f - x / hx * (x / hx)));
        const int seg = 20;
        var bays = Mathf.RoundToInt(s.Z / 6f);
        var bw = s.Z / bays;
        // Voûte : panneaux par travée et par segment, quelques-uns arrachés (on voit les arcs).
        for (var i = 0; i < seg; i++)
        {
            var x0 = -hx + i * s.X / seg;
            var x1 = x0 + s.X / seg;
            var y0 = Y(x0);
            var y1 = Y(x1);
            var n = new Vector3(-(y1 - y0), x1 - x0, 0).Normalized();
            if (n.Y < 0) n = -n;
            for (var j = 0; j < bays; j++)
            {
                var z0 = -hz + j * bw;
                var z1 = z0 + bw;
                if (RuinKit.Hash(seed + i, j, 3) < 0.09f && i > 1 && i < seg - 2) continue;
                var col = new Color(0.95f, 0.95f, 0.95f) * (0.9f + RuinKit.Hash(seed + i, j, 4) * 0.12f);
                k.QuadC(roof, new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0),
                    new Vector2(0, 0), new Vector2(bw / 3f, 0), new Vector2(bw / 3f, (x1 - x0) / 3f), new Vector2(0, (x1 - x0) / 3f), col, col, col, col, n);
                k.QuadC(roof, new Vector3(x0, y0 - 0.05f, z0), new Vector3(x0, y0 - 0.05f, z1), new Vector3(x1, y1 - 0.05f, z1), new Vector3(x1, y1 - 0.05f, z0),
                    Vector2.Zero, new Vector2(bw / 3f, 0), new Vector2(bw / 3f, (x1 - x0) / 3f), new Vector2(0, (x1 - x0) / 3f), col * 0.55f, col * 0.55f, col * 0.55f, col * 0.55f, -n);
            }
        }
        // Murs latéraux bas (bardage, bas de tôle arraché par endroits) et longrines de béton.
        foreach (var sx in new[] { -1f, 1f })
        {
            for (var j = 0; j < bays; j++)
            {
                var z0 = -hz + j * bw;
                var z1 = z0 + bw;
                var torn = RuinKit.Hash(seed, j * 7 + (int)sx, 5) < 0.2f;
                k.Col = new Color(0.45f, 0.43f, 0.39f);
                k.Box(concrete, new Vector3(sx * hx, 0.4f, (z0 + z1) / 2), new Vector3(0.6f, 0.8f, bw));
                Clad2(k, clad, new Vector3(sx * hx, 0, z0), new Vector3(sx * hx, 0, z1), torn ? 2.6f : 0.8f, wall, torn ? 2.6f : 0.8f, wall, new Vector3(sx, 0, 0), Colors.White);
            }
        }
        // Pignon arrière fermé (suit la voûte), porte de service.
        for (var i = 0; i < seg; i++)
        {
            var x0 = -hx + i * s.X / seg;
            var x1 = x0 + s.X / seg;
            Clad2(k, clad, new Vector3(x0, 0, hz), new Vector3(x1, 0, hz), 0, Y(x0), 0, Y(x1), Vector3.Back, new Color(0.88f, 0.86f, 0.82f));
        }
        k.Col = new Color(0.2f, 0.2f, 0.19f);
        k.Box(metal, new Vector3(-hx + 8f, 1.2f, hz + 0.08f), new Vector3(1.2f, 2.4f, 0.1f));
        // Pignon avant : linteau à 16 m, bardage au-dessus, jambages.
        const float door = 16f;
        for (var i = 0; i < seg; i++)
        {
            var x0 = -hx + i * s.X / seg;
            var x1 = x0 + s.X / seg;
            var ya = Y(x0);
            var yb = Y(x1);
            if (Mathf.Abs(x0) > hx - 3f || Mathf.Abs(x1) > hx - 3f)
                Clad2(k, clad, new Vector3(x0, 0, -hz), new Vector3(x1, 0, -hz), 0, ya, 0, yb, Vector3.Forward, new Color(0.9f, 0.88f, 0.84f));
            else if (ya > door || yb > door)
                Clad2(k, clad, new Vector3(x0, 0, -hz), new Vector3(x1, 0, -hz), door, Mathf.Max(door, ya), door, Mathf.Max(door, yb), Vector3.Forward, new Color(0.9f, 0.88f, 0.84f));
        }
        k.Col = new Color(0.24f, 0.23f, 0.21f);
        k.Box(metal, new Vector3(0, door - 0.6f, -hz - 0.3f), new Vector3(s.X - 5f, 1.2f, 0.8f));
        foreach (var sx in new[] { -1f, 1f })
            k.Box(metal, new Vector3(sx * (hx - 3.2f), door / 2, -hz - 0.3f), new Vector3(0.8f, door, 0.8f));
        // Rail de porte au sol (relief), portes coulissantes : deux repliées à l'ouest, une arrachée couchée.
        k.Solid = false;
        k.Col = new Color(0.15f, 0.15f, 0.14f);
        k.Box(metal, new Vector3(0, 0.03f, -hz - 0.8f), new Vector3(s.X - 4f, 0.06f, 0.3f));
        k.Solid = true;
        var doorMat = Mats.Corrugated(new Color(0.42f, 0.41f, 0.37f), 1703, 2.4f);
        for (var d = 0; d < 2; d++)
        {
            var x = -hx + 8.5f + d * 0.7f;
            k.Col = Colors.White;
            k.Box(doorMat, new Vector3(x, door / 2 - 0.3f, -hz - 1.2f - d * 0.6f), new Vector3(10f, door - 0.6f, 0.35f));
        }
        k.Xf = new Transform3D(new Basis(Vector3.Up, 0.25f) * new Basis(Vector3.Right, -Mathf.Pi / 2 + 0.06f), new Vector3(12f, 0.5f, -hz - 9f));
        k.Box(doorMat, Vector3.Zero, new Vector3(10f, door - 0.6f, 0.35f));
        k.Xf = Transform3D.Identity;
        // Arcs treillis intérieurs (non solides) et pannes.
        k.Solid = false;
        k.Col = new Color(0.22f, 0.21f, 0.2f);
        for (var j = 0; j <= bays; j++)
        {
            var z = -hz + j * bw;
            for (var i = 0; i < seg; i++)
            {
                var x0 = -hx + i * s.X / seg;
                var x1 = x0 + s.X / seg;
                k.Tube(metal, new Vector3(x0, Y(x0) - 0.15f, z), new Vector3(x1, Y(x1) - 0.15f, z), 0.12f, 4);
                k.Tube(metal, new Vector3(x0 * 0.94f, Y(x0) - 1.3f, z), new Vector3(x1 * 0.94f, Y(x1) - 1.3f, z), 0.08f, 4);
                if (i % 2 == 0) k.Tube(metal, new Vector3(x0, Y(x0) - 0.15f, z), new Vector3(x1 * 0.94f, Y(x1) - 1.3f, z), 0.05f, 4);
            }
        }
        for (var i = 1; i < seg; i += 3)
        {
            var x = -hx + i * s.X / seg;
            k.Tube(metal, new Vector3(x, Y(x) - 0.3f, -hz), new Vector3(x, Y(x) - 0.3f, hz), 0.07f, 4);
        }
        // Projecteurs suspendus éteints.
        for (var i = 0; i < 6; i++)
        {
            var p = new Vector3((i % 3 - 1) * 16f, 0, (i / 3 - 0.5f) * 20f);
            k.Tube(metal, new Vector3(p.X, Y(p.X) - 0.3f, p.Z), new Vector3(p.X, Y(p.X) - 3f, p.Z), 0.02f, 3);
            k.Cylinder(metal, new Vector3(p.X, Y(p.X) - 3.6f, p.Z), 0.5f, 0.2f, 0.6f, 8);
        }
        k.Solid = true;
        return k.Commit();
    }

    // ───────────────────────── Hangar à charpente ─────────────────────────

    /// <summary>
    /// Hangar à charpente métallique : poteaux tous les 7 m, bardage percé de brèches, bande translucide haute, toit à
    /// deux pans peu pentu (praticable), pignon avant ouvert sous une poutre de porte, escalier extérieur jusqu'au toit
    /// sur le pignon est.
    /// </summary>
    private static ArrayMesh GableHangarMesh(Vector3 s, int seed)
    {
        var k = new MeshKit { Record = true };
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var eave = HangarCEave;
        var ridge = s.Y;
        var clad = Mats.Corrugated(new Color(0.48f, 0.46f, 0.4f), 1704, 2.4f);
        var roof = Mats.Corrugated(new Color(0.42f, 0.41f, 0.38f), 1705, 2.4f);
        var glazing = Mats.FactoryGlazing();
        var metal = Mats.DetailMetal();
        var concrete = Mats.Detail();
        float RoofY(float x) => Mathf.Lerp(ridge, eave, Mathf.Abs(x) / hx);
        var bays = Mathf.RoundToInt(s.Z / 7f);
        var bw = s.Z / bays;
        // Murs latéraux : soubassement béton, bardage, bande translucide ; brèches (panneaux soufflés).
        foreach (var sx in new[] { -1f, 1f })
            for (var j = 0; j < bays; j++)
            {
                var z0 = -hz + j * bw;
                var z1 = z0 + bw;
                var a = new Vector3(sx * hx, 0, z0);
                var b = new Vector3(sx * hx, 0, z1);
                var n = new Vector3(sx, 0, 0);
                k.Col = new Color(0.45f, 0.43f, 0.39f);
                k.Box(concrete, new Vector3(sx * hx, 0.6f, (z0 + z1) / 2), new Vector3(0.5f, 1.2f, bw));
                var breach = RuinKit.Hash(seed + j, (int)sx, 7);
                if (breach < 0.18f) Clad2(k, clad, a, b, 9f, eave - 2f, 9f, eave - 2f, n, Colors.White);
                else if (breach < 0.32f) Clad2(k, clad, a, b, 1.2f, 5.5f, 1.2f, 3.8f, n, Colors.White);
                else Clad2(k, clad, a, b, 1.2f, eave - 2f, 1.2f, eave - 2f, n, Colors.White);
                k.Col = Colors.White;
                k.QuadC(glazing, a with { Y = eave - 2f }, b with { Y = eave - 2f }, b with { Y = eave }, a with { Y = eave },
                    new Vector2(0, 0.66f), new Vector2(bw / 3f, 0.66f), new Vector2(bw / 3f, 0), Vector2.Zero, Colors.White, Colors.White, Colors.White, Colors.White, n);
            }
        // Poteaux (solides) et fermes (non solides).
        k.Col = new Color(0.26f, 0.25f, 0.23f);
        for (var j = 0; j <= bays; j++)
        {
            var z = -hz + j * bw;
            foreach (var sx in new[] { -1f, 1f })
                k.Box(metal, new Vector3(sx * (hx - 0.35f), eave / 2, z), new Vector3(0.5f, eave, 0.6f));
            k.Solid = false;
            k.Tube(metal, new Vector3(-hx, eave - 0.3f, z), new Vector3(0, ridge - 0.4f, z), 0.16f, 4);
            k.Tube(metal, new Vector3(hx, eave - 0.3f, z), new Vector3(0, ridge - 0.4f, z), 0.16f, 4);
            k.Tube(metal, new Vector3(-hx, eave - 1.6f, z), new Vector3(hx, eave - 1.6f, z), 0.1f, 4);
            for (var t = -3; t <= 3; t++) k.Tube(metal, new Vector3(t * hx / 4f, eave - 1.6f, z), new Vector3(t * hx / 4f + hx / 8f, RoofY(t * hx / 4f + hx / 8f) - 0.4f, z), 0.05f, 4);
            k.Solid = true;
        }
        // Toit : deux pans, quelques plaques manquantes.
        for (var j = 0; j < bays; j++)
        {
            var z0 = -hz + j * bw;
            var z1 = z0 + bw;
            foreach (var sx in new[] { -1f, 1f })
            {
                if (RuinKit.Hash(seed + j, (int)sx, 9) < 0.12f) continue;
                var col = new Color(0.95f, 0.95f, 0.95f) * (0.88f + RuinKit.Hash(seed + j, (int)sx, 10) * 0.14f);
                var n = new Vector3(sx * (ridge - eave), hx, 0).Normalized();
                var e = new Vector3(sx * (hx + 0.6f), eave - 0.1f, 0);
                k.QuadC(roof, new Vector3(0, ridge, z0), new Vector3(0, ridge, z1), e with { Z = z1 }, e with { Z = z0 },
                    Vector2.Zero, new Vector2(bw / 3f, 0), new Vector2(bw / 3f, hx / 3f), new Vector2(0, hx / 3f), col, col, col, col, n);
                k.QuadC(roof, new Vector3(0, ridge - 0.06f, z0), new Vector3(0, ridge - 0.06f, z1), e with { Z = z1, Y = eave - 0.16f }, e with { Z = z0, Y = eave - 0.16f },
                    Vector2.Zero, new Vector2(bw / 3f, 0), new Vector2(bw / 3f, hx / 3f), new Vector2(0, hx / 3f), col * 0.5f, col * 0.5f, col * 0.5f, col * 0.5f, -n);
            }
        }
        // Pignons : arrière fermé, avant ouvert sous la poutre de porte (14 m).
        const float door = 13.5f;
        for (var i = 0; i < 8; i++)
        {
            var x0 = -hx + i * s.X / 8f;
            var x1 = x0 + s.X / 8f;
            Clad2(k, clad, new Vector3(x0, 0, hz), new Vector3(x1, 0, hz), 0, RoofY(x0), 0, RoofY(x1), Vector3.Back, new Color(0.9f, 0.88f, 0.84f));
            Clad2(k, clad, new Vector3(x0, 0, -hz), new Vector3(x1, 0, -hz), door, RoofY(x0), door, RoofY(x1), Vector3.Forward, new Color(0.9f, 0.88f, 0.84f));
        }
        k.Col = new Color(0.24f, 0.23f, 0.21f);
        k.Box(metal, new Vector3(0, door - 0.5f, -hz - 0.3f), new Vector3(s.X, 1f, 0.7f));
        // Portes coulissantes : une repliée à l'est, une pendue de travers.
        var doorMat = Mats.Corrugated(new Color(0.4f, 0.4f, 0.37f), 1706, 2.4f);
        k.Col = Colors.White;
        k.Box(doorMat, new Vector3(hx - 6f, door / 2 - 0.3f, -hz - 1.1f), new Vector3(9.5f, door - 0.6f, 0.3f));
        k.Box(doorMat, new Vector3(-hx + 7f, door / 2 - 0.9f, -hz - 1.4f), new Vector3(9.5f, door - 0.8f, 0.3f), new Basis(Vector3.Back, 0.09f) * new Basis(Vector3.Up, 0.12f));
        // Escalier extérieur contre le long pan est, jusqu'à l'égout : la plateforme sommitale avance sur le toit.
        StairTower(k, new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2), new Vector3(hx + 3.7f, 0, hz - 6f)), eave, 1.2f);
        return k.Commit();
    }

    // ───────────────────────── Abri bétonné ─────────────────────────

    /// <summary>
    /// Abri bétonné pour avion de combat : voûte surbaissée en béton épais (praticable sur le dessus), façade avant à
    /// arc renforcé, porte blindée en deux vantaux (l'un ouvert, l'autre soufflé, couché devant), fond percé d'une bouche
    /// d'évacuation des gaz.
    /// </summary>
    private static ArrayMesh ShelterMesh(Vector3 s, int variant)
    {
        var k = new MeshKit { Record = true };
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var concrete = Mats.Detail();
        var metal = Mats.DetailMetal();
        const float t = 1.1f;
        float Y(float x, float r) => s.Y * Mathf.Sqrt(Mathf.Max(0f, 1f - x / r * (x / r)));
        const int seg = 18;
        var tone = new Color(0.55f, 0.53f, 0.48f);
        // Voûte extérieure et intérieure.
        for (var i = 0; i < seg; i++)
        {
            var a0 = Mathf.Pi * i / seg;
            var a1 = Mathf.Pi * (i + 1) / seg;
            Vector3 O(float a, float z) => new(-Mathf.Cos(a) * (hx + t), Mathf.Sin(a) * (s.Y + t * 0.6f), z);
            Vector3 I(float a, float z) => new(-Mathf.Cos(a) * hx, Mathf.Sin(a) * s.Y, z);
            k.Col = tone * (0.92f + RuinKit.Hash(i, variant, 11) * 0.1f);
            var mid = (a0 + a1) / 2;
            var on = new Vector3(-Mathf.Cos(mid), Mathf.Sin(mid), 0);
            k.OrientedQuad(concrete, O(a0, -hz), O(a1, -hz), O(a1, hz), O(a0, hz), on);
            k.Col = tone * 0.6f;
            k.OrientedQuad(concrete, I(a0, -hz), I(a1, -hz), I(a1, hz), I(a0, hz), -on);
            // Tranches avant et arrière (arc épais).
            k.Col = tone * 0.85f;
            k.OrientedQuad(concrete, I(a0, -hz), I(a1, -hz), O(a1, -hz), O(a0, -hz), Vector3.Forward);
            k.OrientedQuad(concrete, I(a0, hz), I(a1, hz), O(a1, hz), O(a0, hz), Vector3.Back);
            // Mur de fond (rempli jusqu'à la voûte intérieure).
            var x0 = -Mathf.Cos(a0) * hx;
            var x1 = -Mathf.Cos(a1) * hx;
            k.Col = tone * 0.8f;
            k.OrientedQuad(concrete, new Vector3(x0, 0, hz - 0.8f), new Vector3(x1, 0, hz - 0.8f), new Vector3(x1, Y(x1, hx), hz - 0.8f), new Vector3(x0, Y(x0, hx), hz - 0.8f), Vector3.Forward);
        }
        // Bouche d'évacuation des gaz (relief sombre) et contreforts de la façade avant.
        k.Solid = false;
        k.Col = new Color(0.05f, 0.05f, 0.05f);
        k.Box(concrete, new Vector3(0, 4.5f, hz - 0.85f), new Vector3(4f, 2.5f, 0.05f));
        k.Solid = true;
        k.Col = tone * 0.9f;
        foreach (var sx in new[] { -1f, 1f })
            k.Box(concrete, new Vector3(sx * (hx + 1.6f), 2.5f, -hz + 1.2f), new Vector3(2.2f, 5f, 2.4f));
        k.Box(concrete, new Vector3(0, s.Y + 0.9f, -hz + 0.7f), new Vector3(s.X * 0.5f, 1.2f, 1.4f));
        // Porte blindée : vantail ouvert (glissé contre le flanc), vantail soufflé couché devant l'abri.
        k.Col = new Color(0.34f, 0.35f, 0.32f);
        k.Box(metal, new Vector3(hx + 1.0f, s.Y * 0.38f, -hz - 1.6f), new Vector3(hx * 0.95f, s.Y * 0.76f, 0.6f));
        k.Xf = new Transform3D(new Basis(Vector3.Up, 0.3f * (variant == 0 ? 1 : -1)) * new Basis(Vector3.Right, -Mathf.Pi / 2 + 0.05f), new Vector3(-hx * 0.4f, 0.32f, -hz - 6.5f));
        k.Box(metal, Vector3.Zero, new Vector3(hx * 0.95f, s.Y * 0.76f, 0.6f));
        k.Xf = Transform3D.Identity;
        k.Col = new Color(0.14f, 0.14f, 0.13f);
        k.Box(metal, new Vector3(0, 0.04f, -hz - 1.6f), new Vector3(s.X + 6f, 0.08f, 0.4f));
        return k.Commit();
    }

    // ───────────────────────── Tour de contrôle ─────────────────────────

    /// <summary>
    /// Tour de contrôle : socle de deux niveaux (bandeaux vitrés, acrotère), fût de béton, vigie vitrée en
    /// encorbellement (vitres en partie brisées, pupitres), balcon ceinturant la vigie, toit à antennes et radôme
    /// d'approche ; escalier métallique extérieur du sol jusqu'au balcon.
    /// </summary>
    private static ArrayMesh ControlTowerMesh(Vector3 s)
    {
        var k = new MeshKit { Record = true };
        var concrete = Mats.Detail();
        var metal = Mats.DetailMetal();
        var glass = Mats.Painted(new Color(0.06f, 0.075f, 0.08f), 0.06f, 0.4f);
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var wall = new Color(0.58f, 0.55f, 0.49f);
        // Socle : murs, bandeaux de fenêtres sur deux niveaux, acrotère.
        foreach (var (a, b, n) in new[]
        {
            (new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), Vector3.Back), (new Vector3(hx, 0, -hz), new Vector3(-hx, 0, -hz), Vector3.Forward),
            (new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz), Vector3.Right), (new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), Vector3.Left),
        })
        {
            k.Col = wall;
            k.OrientedQuad(concrete, a, b, b with { Y = s.Y }, a with { Y = s.Y }, n);
            var len = (b - a).Length();
            var dir = (b - a) / len;
            k.Solid = false;
            foreach (var y in new[] { 1.2f, 4.4f })
                for (var x = 1.5f; x < len - 2.5f; x += 3.2f)
                {
                    var broken = RuinKit.Hash(x, y + a.X, 13) < 0.3f;
                    k.Col = broken ? new Color(0.02f, 0.02f, 0.02f) : Colors.White;
                    var p0 = a + dir * x + n * 0.04f;
                    var p1 = a + dir * (x + 2.4f) + n * 0.04f;
                    k.OrientedQuad(broken ? concrete : glass, p0 with { Y = y }, p1 with { Y = y }, p1 with { Y = y + 1.6f }, p0 with { Y = y + 1.6f }, n);
                }
            k.Solid = true;
        }
        k.Col = wall * 0.9f;
        k.Ring(concrete, hx, hz, s.Y, s.Y + 0.9f, 0.3f, 0.05f);
        k.Col = new Color(0.32f, 0.31f, 0.29f);
        k.Quad(concrete, new Vector3(-hx, s.Y, hz), new Vector3(hx, s.Y, hz), new Vector3(hx, s.Y, -hz), new Vector3(-hx, s.Y, -hz));
        // Fût (6 × 6 m) décalé vers l'est.
        var sc = new Vector3(6f, 0, 0);
        k.Col = wall * 0.95f;
        k.Box(concrete, sc + new Vector3(0, (s.Y + CabFloor) / 2, 0), new Vector3(6f, CabFloor - s.Y, 6f), null, false, false);
        k.Solid = false;
        k.Col = new Color(0.04f, 0.04f, 0.04f);
        for (var y = s.Y + 2f; y < CabFloor - 2f; y += 3.4f)
            k.Box(concrete, sc + new Vector3(0, y, -3.02f), new Vector3(0.8f, 1.6f, 0.04f));
        k.Solid = true;
        // Vigie : dalle, balcon, garde-corps, montants, vitres (non solides : on entre par une vitre brisée), pupitres, toit.
        k.Col = wall * 0.85f;
        k.Box(concrete, sc + new Vector3(0, CabFloor - 0.3f, 0), new Vector3(12.4f, 0.6f, 12.4f));
        k.Col = new Color(0.26f, 0.25f, 0.23f);
        k.Solid = false;
        foreach (var (dx, dz, lx, lz) in new[] { (0f, 6.15f, 12.4f, 0.06f), (0f, -6.15f, 12.4f, 0.06f), (6.15f, 0f, 0.06f, 12.4f), (-6.15f, 0f, 0.06f, 12.4f) })
        {
            k.Box(metal, sc + new Vector3(dx, CabFloor + 1.05f, dz), new Vector3(Mathf.Max(lx, 0.06f), 0.06f, Mathf.Max(lz, 0.06f)));
            k.Box(metal, sc + new Vector3(dx, CabFloor + 0.5f, dz), new Vector3(Mathf.Max(lx, 0.04f), 0.04f, Mathf.Max(lz, 0.04f)));
        }
        k.Solid = true;
        for (var i = 0; i < 8; i++)
        {
            var a = i * Mathf.Tau / 8f + Mathf.Pi / 8f;
            var p = sc + new Vector3(Mathf.Cos(a) * 5.2f, CabFloor + (CabRoof - CabFloor) / 2, Mathf.Sin(a) * 5.2f);
            k.Col = new Color(0.2f, 0.2f, 0.19f);
            k.Box(metal, p, new Vector3(0.2f, CabRoof - CabFloor, 0.2f));
        }
        k.Solid = false;
        for (var i = 0; i < 8; i++)
        {
            var a0 = i * Mathf.Tau / 8f + Mathf.Pi / 8f;
            var a1 = a0 + Mathf.Tau / 8f;
            if (RuinKit.Hash(i, 2, 14) < 0.35f) continue; // vitre soufflée
            var p0 = sc + new Vector3(Mathf.Cos(a0) * 5.2f, CabFloor + 0.9f, Mathf.Sin(a0) * 5.2f);
            var p1 = sc + new Vector3(Mathf.Cos(a1) * 5.2f, CabFloor + 0.9f, Mathf.Sin(a1) * 5.2f);
            var mid = (p0 + p1) / 2 - sc;
            k.Col = Colors.White;
            k.OrientedQuad(glass, p0, p1, p1 + new Vector3(Mathf.Cos(a1) * 0.4f, 2.3f, Mathf.Sin(a1) * 0.4f), p0 + new Vector3(Mathf.Cos(a0) * 0.4f, 2.3f, Mathf.Sin(a0) * 0.4f), mid with { Y = 0 });
        }
        k.Solid = true;
        k.Col = new Color(0.3f, 0.29f, 0.27f);
        for (var i = 0; i < 8; i++)
        {
            var a0 = i * Mathf.Tau / 8f + Mathf.Pi / 8f;
            var a1 = a0 + Mathf.Tau / 8f;
            var p0 = sc + new Vector3(Mathf.Cos(a0) * 5.2f, CabFloor, Mathf.Sin(a0) * 5.2f);
            var p1 = sc + new Vector3(Mathf.Cos(a1) * 5.2f, CabFloor, Mathf.Sin(a1) * 5.2f);
            var mid = (p0 + p1) / 2 - sc;
            k.OrientedQuad(concrete, p0, p1, p1 + Vector3.Up * 0.9f, p0 + Vector3.Up * 0.9f, mid with { Y = 0 });
        }
        k.Col = new Color(0.16f, 0.16f, 0.15f);
        k.Box(metal, sc + new Vector3(0, CabFloor + 0.55f, -3.4f), new Vector3(6f, 1.1f, 1.2f));
        k.Box(metal, sc + new Vector3(3.4f, CabFloor + 0.55f, 0), new Vector3(1.2f, 1.1f, 4f));
        k.Col = wall * 0.8f;
        k.Box(concrete, sc + new Vector3(0, CabRoof + 0.2f, 0), new Vector3(11.6f, 0.4f, 11.6f));
        // Toit : mâts d'antennes, radôme d'approche (non solides au-dessus du caisson).
        k.Col = new Color(0.3f, 0.3f, 0.28f);
        k.Box(metal, sc + new Vector3(-3f, CabRoof + 0.9f, -3f), new Vector3(1.6f, 1f, 1.6f));
        k.Solid = false;
        k.Col = new Color(0.62f, 0.6f, 0.56f);
        SpherePart(k, Mats.Worn(new Color(0.6f, 0.58f, 0.54f), 0.7f, 0f), sc + new Vector3(-3f, CabRoof + 2.3f, -3f), new Vector3(1.2f, 1.1f, 1.2f), 14, 6);
        k.Col = new Color(0.12f, 0.12f, 0.12f);
        k.Tube(metal, sc + new Vector3(3.5f, CabRoof + 0.4f, 3.5f), sc + new Vector3(3.5f, CabRoof + 8f, 3.5f), 0.07f, 4);
        k.Tube(metal, sc + new Vector3(3.5f, CabRoof + 6f, 3.5f), sc + new Vector3(2.2f, CabRoof + 7.6f, 3.5f), 0.03f, 3);
        k.Tube(metal, sc + new Vector3(-4f, CabRoof + 0.4f, 4f), sc + new Vector3(-4.6f, CabRoof + 4f, 4.2f), 0.05f, 4);
        k.Solid = true;
        // Escalier extérieur, contre le fût côté est, jusqu'au balcon (on débouche à l'ouest de la cage).
        StairTower(k, new Transform3D(Basis.Identity, new Vector3(13.5f, 0, 0)), CabFloor, 0f);
        return k.Commit();
    }

    /// <summary>Sphère (ou calotte) à normales lissées (radôme), sans rotation.</summary>
    private static void SpherePart(MeshKit k, Material m, Vector3 c, Vector3 radii, int segU, int segV, float lat0 = -Mathf.Pi / 2, float lat1 = Mathf.Pi / 2)
    {
        Vector3 Pt(float lon, float lat) => new(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
        for (var j = 0; j < segV; j++)
        {
            var la0 = Mathf.Lerp(lat0, lat1, j / (float)segV);
            var la1 = Mathf.Lerp(lat0, lat1, (j + 1) / (float)segV);
            for (var i = 0; i < segU; i++)
            {
                var lo0 = i * Mathf.Tau / segU;
                var lo1 = (i + 1) * Mathf.Tau / segU;
                var n00 = Pt(lo0, la0); var n10 = Pt(lo1, la0); var n11 = Pt(lo1, la1); var n01 = Pt(lo0, la1);
                Vector3 P(Vector3 n) => c + n * radii;
                var col = k.Col;
                if (Mathf.Cos(la0) > 0.001f) k.TriSmooth(m, P(n00), P(n10), P(n11), n00, n10, n11, col, col, col);
                if (Mathf.Cos(la1) > 0.001f) k.TriSmooth(m, P(n00), P(n11), P(n01), n00, n11, n01, col, col, col);
            }
        }
    }

    /// <summary>
    /// Tour d'escalier métallique (volées droites alternées sur deux files, paliers aux deux bouts) dans un repère local :
    /// volées le long de Z, files en x = ±0,65 ; arrivée en haut sur un palier prolongé de <paramref name="exit"/> m
    /// vers -Z. Plaques inclinées solides sous le nez des marches (même géométrie que la zone industrielle).
    /// </summary>
    private static void StairTower(MeshKit k, Transform3D xf, float height, float exit)
    {
        var keep = k.Xf;
        k.Xf = xf;
        var metal = Mats.DetailMetal();
        const float run = 4.2f, half = run / 2, land = 1.3f;
        var flights = Mathf.Max(1, Mathf.CeilToInt(height / 2.9f));
        var rise = height / flights;
        for (var f = 0; f < flights; f++)
        {
            var x = f % 2 == 0 ? -0.65f : 0.65f;
            var zA = f % 2 == 0 ? -half : half;
            var zB = -zA;
            var y0 = f * rise;
            var y1 = (f + 1) * rise;
            k.Col = new Color(0.2f, 0.2f, 0.19f);
            k.Quad(metal, new Vector3(x - 0.6f, y0, zA), new Vector3(x + 0.6f, y0, zA), new Vector3(x + 0.6f, y1, zB), new Vector3(x - 0.6f, y1, zB));
            k.Quad(metal, new Vector3(x - 0.6f, y1, zB), new Vector3(x + 0.6f, y1, zB), new Vector3(x + 0.6f, y0, zA), new Vector3(x - 0.6f, y0, zA));
            k.Solid = false;
            var steps = Mathf.CeilToInt(rise / 0.2f);
            for (var s = 0; s < steps; s++)
            {
                var u = (s + 0.5f) / steps;
                k.Box(metal, new Vector3(x, Mathf.Lerp(y0, y1, u) + 0.05f, Mathf.Lerp(zA, zB, u)), new Vector3(1.15f, 0.04f, 0.28f));
            }
            k.Col = new Color(0.42f, 0.36f, 0.16f);
            var xr = x + (f % 2 == 0 ? -0.62f : 0.62f);
            k.Tube(metal, new Vector3(xr, y0 + 1f, zA), new Vector3(xr, y1 + 1f, zB), 0.025f, 4);
            k.Solid = true;
            k.Col = new Color(0.22f, 0.22f, 0.21f);
            var lz = zB + Mathf.Sign(zB) * land / 2;
            if (f == flights - 1)
            {
                // Plateforme sommitale : les deux paliers et la file d'en face, prolongée vers -Z.
                k.BoxMinMax(metal, new Vector3(-1.3f, y1 - 0.1f, -half - land - exit), new Vector3(1.3f, y1, -half));
                k.BoxMinMax(metal, new Vector3(-1.3f, y1 - 0.1f, half), new Vector3(1.3f, y1, half + land));
                var ox = x < 0 ? 0.05f : -1.3f;
                k.BoxMinMax(metal, new Vector3(ox, y1 - 0.1f, -half), new Vector3(ox + 1.25f, y1, half));
            }
            else k.Box(metal, new Vector3(0, y1 - 0.05f, lz), new Vector3(2.6f, 0.1f, land));
        }
        k.Col = new Color(0.24f, 0.24f, 0.23f);
        foreach (var px in new[] { -1.3f, 1.3f })
        foreach (var pz in new[] { -half - land, half + land })
            k.Box(metal, new Vector3(px, height / 2, pz), new Vector3(0.18f, height, 0.18f));
        k.Solid = false;
        k.Col = new Color(0.42f, 0.36f, 0.16f);
        for (var f = 1; f <= flights; f++)
        {
            var y = f * rise + 1f;
            k.Tube(metal, new Vector3(-1.3f, y, half + land), new Vector3(1.3f, y, half + land), 0.025f, 4);
            foreach (var px in new[] { -1.3f, 1.3f })
                k.Tube(metal, new Vector3(px, y, half), new Vector3(px, y, half + land), 0.025f, 4);
        }
        k.Solid = true;
        k.Xf = keep;
    }
}
