using Godot;

namespace Rempart;

/// <summary>
/// Parvis central du quartier d'affaires : dalle de granit, deux tours d'angle (l'une avec hélisurface), pavillons
/// vitrés bas avec escalier de secours (accès aux toits), bassin asséché et sculpture, bacs d'arbres feuillus, entrée
/// de métro. L'aire de départ (joueurs, char, hélicoptère, escouades alliées) reste dégagée.
/// </summary>
public static partial class BusinessBuilder
{
    private static void BuildPlaza(Ctx c)
    {
        var rng = new RandomNumberGenerator { Seed = 909 };
        // Dallage à 0,15 m percé par la trémie du métro, bordure de granit.
        SlabAround(c, Plaza, MetroPit, -0.15f, Walk, Mats.Sidewalk());
        var g = c.Ground.At(Vector3.Zero);
        g.Col = Colors.White;
        g.Xf = new Transform3D(Basis.Identity, new Vector3(Plaza.GetCenter().X, 0, Plaza.GetCenter().Y));
        g.Ring(Mats.Curb(), Plaza.Size.X / 2, Plaza.Size.Y / 2, -0.1f, 0.155f, 0.28f, 0.02f);
        g.Xf = Transform3D.Identity;
        // Calepinage : bandes de granit sombre tous les 12 m (relief nul, sans collision).
        g.Col = new Color(0.62f, 0.62f, 0.62f);
        for (var x = Plaza.Position.X + 6f; x < Plaza.End.X - 1f; x += 12f)
            FlatStrip(g, Mats.Curb(), new Vector2(x - 0.3f, Plaza.Position.Y + 0.5f), new Vector2(x + 0.3f, Plaza.End.Y - 0.5f), Walk + 0.004f, MetroPit);
        for (var z = Plaza.Position.Y + 6f; z < Plaza.End.Y - 1f; z += 12f)
            FlatStrip(g, Mats.Curb(), new Vector2(Plaza.Position.X + 0.5f, z - 0.3f), new Vector2(Plaza.End.X - 0.5f, z + 0.3f), Walk + 0.005f, MetroPit);

        // Tours d'angle au nord du parvis (la plus haute porte une hélisurface).
        AddTower(c, new Vector2(-46, -46), 26f, 26f, 36, 3f, rng, 0);
        AddTower(c, new Vector2(46, -46), 26f, 26f, 44, 3f, rng, 0, true);
        // Pavillons vitrés bas au sud : escaliers de secours tournés vers le centre (toits à 10 m et 7 m).
        var west = new BuildingStyle { Kind = BuildingKind.Office, Variant = 5, Clearance = 4f, FireEscape = true, Facing = 2 };
        AddBuilding(c, new Vector3(-46, Walk, 32), new Vector3(20, 3 * 3.2f + 0.6f, 14), null, west);
        var east = new BuildingStyle { Kind = BuildingKind.Office, Variant = 2, Clearance = 4f, FireEscape = true, Facing = 3 };
        AddBuilding(c, new Vector3(46, Walk, 34), new Vector3(18, 2 * 3.2f + 0.6f, 14), null, east);

        PlazaFountain(c, new Vector3(0, Walk, -38));
        BuildMetro(c);

        // Bacs d'arbres (béton, terre, arbre feuillu) le long des bords du parvis, ouverts sur les axes.
        foreach (var x in new[] { -26f, -16f, 16f, 26f })
        {
            Planter(c, new Vector3(x, Walk, -57f), rng);
            Planter(c, new Vector3(x, Walk, 47f), rng);
        }
        foreach (var z in new[] { -24f, -14f, 14f })
        {
            Planter(c, new Vector3(-57f, Walk, z), rng);
            Planter(c, new Vector3(57f, Walk, z), rng);
        }
        // Bancs entre les bacs, corbeilles, mâts d'éclairage.
        foreach (var x in new[] { -21f, 21f })
        {
            c.Props.Add(Props.Bench(), new Vector3(x, Walk, -56.5f), 0, range: 140);
            c.Props.Add(Props.Bench(), new Vector3(x, Walk, 46.5f), Mathf.Pi, range: 140);
            c.Props.Add(Props.Bin(), new Vector3(x + 1.7f, Walk, -56.8f), 0, range: 120);
        }
        foreach (var z in new[] { -19f, 19f })
        {
            c.Props.Add(Props.Bench(), new Vector3(-56.5f, Walk, z), Mathf.Pi / 2, range: 140);
            c.Props.Add(Props.Bench(), new Vector3(56.5f, Walk, z), -Mathf.Pi / 2, range: 140);
        }
        foreach (var p in new[] { new Vector3(-30, Walk, -30), new Vector3(30, Walk, -30), new Vector3(-38, Walk, 8), new Vector3(36, Walk, 18), new Vector3(-10, Walk, 46), new Vector3(10, Walk, -54) })
            Lamp(c, p, Mathf.Atan2(-p.X, -p.Z), true); // éclairage de secours du parvis (aire de départ lisible la nuit)
    }

    /// <summary>Bande plate (calepinage) à la hauteur donnée, interrompue par un rectangle (trémie).</summary>
    private static void FlatStrip(MeshKit k, Material m, Vector2 min, Vector2 max, float y, Rect2 skip)
    {
        void Q(float x0, float z0, float x1, float z1)
        {
            if (x1 - x0 < 0.05f || z1 - z0 < 0.05f) return;
            k.Quad(m, new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), new Vector3(x0, y, z0));
        }
        var r = new Rect2(min, max - min);
        var hole = skip.Grow(0.3f);
        if (!r.Intersects(hole)) { Q(min.X, min.Y, max.X, max.Y); return; }
        Q(min.X, min.Y, max.X, hole.Position.Y);
        Q(min.X, hole.End.Y, max.X, max.Y);
        Q(min.X, Mathf.Max(min.Y, hole.Position.Y), hole.Position.X, Mathf.Min(max.Y, hole.End.Y));
        Q(hole.End.X, Mathf.Max(min.Y, hole.Position.Y), max.X, Mathf.Min(max.Y, hole.End.Y));
    }

    /// <summary>Bac en béton (solide), terre, arbre feuillu destructible.</summary>
    private static void Planter(Ctx c, Vector3 p, RandomNumberGenerator rng)
    {
        var k = c.Solid.At(p);
        k.Col = new Color(0.5f, 0.49f, 0.46f);
        k.Xf = new Transform3D(Basis.Identity, p);
        k.Ring(Mats.Detail(), 1.3f, 1.3f, 0, 0.7f, 0.25f, 0f);
        k.Xf = Transform3D.Identity;
        k.Col = Colors.White;
        k.BoxMinMax(Mats.Dirt(), p + new Vector3(-1.05f, 0, -1.05f), p + new Vector3(1.05f, 0.55f, 1.05f), false);
        var v = rng.RandiRange(0, 3);
        c.Props.Add(Props.LeafyTree(v), p + Vector3.Up * 0.55f, rng.RandfRange(0, Mathf.Tau), null, 320, true, rng.RandfRange(0.75f, 0.95f));
    }

    /// <summary>Bassin asséché (margelle solide, fond taché) et sculpture de dalles de granit inclinées, dont une tombée.</summary>
    private static void PlazaFountain(Ctx c, Vector3 p)
    {
        var k = c.Solid.At(p);
        var d = Mats.Detail();
        const float r = 8.5f;
        const int seg = 28;
        k.Col = new Color(0.52f, 0.51f, 0.48f);
        for (var i = 0; i < seg; i++)
        {
            var a = (i + 0.5f) * Mathf.Tau / seg;
            var len = r * Mathf.Tau / seg + 0.05f;
            k.Box(d, p + new Vector3(Mathf.Cos(a) * r, 0.3f, Mathf.Sin(a) * r), new Vector3(0.6f, 0.6f, len), new Basis(Vector3.Up, -a));
        }
        k.Solid = false;
        k.Col = new Color(0.2f, 0.21f, 0.2f);
        k.Cylinder(d, p + Vector3.Up * 0.004f, r - 0.2f, r - 0.2f, 0.01f, seg);
        k.Solid = true;
        // Sculpture : trois dalles dressées, une quatrième renversée sur la margelle.
        k.Col = new Color(0.34f, 0.33f, 0.32f);
        k.Box(d, p + new Vector3(-1.2f, 3f, 0.4f), new Vector3(1.3f, 6f, 0.6f), new Basis(Vector3.Up, 0.3f) * new Basis(Vector3.Back, 0.12f));
        k.Box(d, p + new Vector3(1.1f, 2.4f, -0.5f), new Vector3(1.2f, 4.8f, 0.6f), new Basis(Vector3.Up, -0.5f) * new Basis(Vector3.Back, -0.1f));
        k.Box(d, p + new Vector3(0.2f, 1.7f, 1.4f), new Vector3(1.0f, 3.4f, 0.5f), new Basis(Vector3.Up, 1.2f));
        k.Col = new Color(0.3f, 0.29f, 0.28f);
        k.Box(d, p + new Vector3(4.2f, 0.5f, 3.4f), new Vector3(1.2f, 0.55f, 5.2f), new Basis(Vector3.Up, 0.7f) * new Basis(Vector3.Right, 0.12f));
        c.Ruins.Block(new Rect2(p.X - r - 0.5f, p.Z - r - 0.5f, r * 2 + 1f, r * 2 + 1f));
    }

    /// <summary>
    /// Entrée de métro : trémie, escalier droit vers le sud (plaque inclinée solide sous les marches), palier bas
    /// devant le tunnel muré d'un rideau de fer à moitié baissé, murs et garde-corps, mât signalétique, auvent brisé.
    /// </summary>
    private static void BuildMetro(Ctx c)
    {
        var pit = MetroPit;
        var k = c.Solid.At(new Vector3(pit.GetCenter().X, 0, pit.GetCenter().Y));
        var d = Mats.Detail();
        var metal = Mats.DetailMetal();
        var x0 = pit.Position.X;
        var x1 = pit.End.X;
        var z0 = pit.Position.Y;
        var z1 = pit.End.Y;
        const float run = 9.8f;
        var zb = z0 + run;                          // pied de l'escalier
        var yb = -MetroDepth;
        var concrete = new Color(0.48f, 0.47f, 0.44f);
        // Murs latéraux et mur du fond (jusqu'à 1 m au-dessus du dallage : garde-corps maçonnés).
        k.Col = concrete;
        k.BoxMinMax(d, new Vector3(x0 - 0.25f, yb, z0), new Vector3(x0, Walk + 1f, z1 + 0.25f));
        k.BoxMinMax(d, new Vector3(x1, yb, z0), new Vector3(x1 + 0.25f, Walk + 1f, z1 + 0.25f));
        k.BoxMinMax(d, new Vector3(x0 - 0.25f, yb, z1), new Vector3(x1 + 0.25f, Walk + 1f, z1 + 0.25f));
        k.Col = concrete.Darkened(0.25f);
        k.BoxMinMax(d, new Vector3(x0 - 0.3f, Walk + 1f, z0), new Vector3(x0 + 0.05f, Walk + 1.08f, z1 + 0.3f));
        k.BoxMinMax(d, new Vector3(x1 - 0.05f, Walk + 1f, z0), new Vector3(x1 + 0.3f, Walk + 1.08f, z1 + 0.3f));
        k.BoxMinMax(d, new Vector3(x0 - 0.3f, Walk + 1f, z1 - 0.05f), new Vector3(x1 + 0.3f, Walk + 1.08f, z1 + 0.3f));
        // Escalier : plaque porteuse inclinée (dessus juste sous le nez des marches), marches en relief.
        var steps = 24;
        var sr = (Walk - yb) / steps;
        var ang = Mathf.Atan2(Walk - yb, run);
        var basis = new Basis(Vector3.Right, ang);
        var mid = new Vector3((x0 + x1) / 2, (Walk + yb) / 2 - 0.25f + sr * 0.25f, (z0 + zb) / 2);
        k.Col = concrete.Darkened(0.1f);
        k.Box(d, mid, new Vector3(x1 - x0, 0.5f, run / Mathf.Cos(ang) + 0.2f), basis);
        k.Solid = false;
        k.Col = new Color(0.4f, 0.39f, 0.37f);
        for (var i = 0; i < steps; i++)
        {
            var z = z0 + (i + 0.5f) * run / steps;
            var y = Walk - (i + 1) * sr;
            k.Box(d, new Vector3((x0 + x1) / 2, y + sr * 0.5f, z), new Vector3(x1 - x0, sr, run / steps), null, false);
        }
        k.Solid = true;
        // Main courante sur les deux murs.
        k.Col = new Color(0.25f, 0.26f, 0.27f);
        foreach (var x in new[] { x0 + 0.08f, x1 - 0.08f })
            k.Tube(metal, new Vector3(x, Walk + 0.9f, z0 + 0.3f), new Vector3(x, yb + 0.9f, zb), 0.035f, 5);
        // Palier bas et tunnel : ouverture noire, rideau de fer à moitié baissé, carrelage sale.
        k.Col = new Color(0.02f, 0.02f, 0.02f);
        k.Solid = false;
        k.OrientedQuad(d, new Vector3(x0 + 0.2f, yb, z1 - 0.02f), new Vector3(x1 - 0.2f, yb, z1 - 0.02f), new Vector3(x1 - 0.2f, yb + 3f, z1 - 0.02f), new Vector3(x0 + 0.2f, yb + 3f, z1 - 0.02f), Vector3.Forward);
        k.Solid = true;
        k.Col = new Color(0.36f, 0.37f, 0.37f);
        k.BoxMinMax(Mats.Corrugated(new Color(0.42f, 0.43f, 0.42f), 17, 6f), new Vector3(x0 + 0.2f, yb + 1.25f, z1 - 0.1f), new Vector3(x1 - 0.2f, yb + 3f, z1 - 0.04f));
        k.Col = new Color(0.62f, 0.6f, 0.55f);
        k.BoxMinMax(d, new Vector3(x0, yb + 3f, z1 - 0.1f), new Vector3(x1, Walk, z1 - 0.02f));
        // Auvent : deux portiques d'acier, quelques vitres restées en place.
        k.Col = new Color(0.22f, 0.23f, 0.24f);
        foreach (var z in new[] { z0 + 1.2f, z0 + 6.2f })
        {
            k.Box(metal, new Vector3(x0 - 0.12f, Walk + 1.6f, z), new Vector3(0.14f, 3.2f, 0.14f));
            k.Box(metal, new Vector3(x1 + 0.12f, Walk + 1.6f, z), new Vector3(0.14f, 3.2f, 0.14f));
            k.Box(metal, new Vector3((x0 + x1) / 2, Walk + 3.2f, z), new Vector3(x1 - x0 + 0.4f, 0.16f, 0.16f));
        }
        k.Box(metal, new Vector3(x0 - 0.12f, Walk + 3.2f, z0 + 3.7f), new Vector3(0.12f, 0.12f, 5.1f));
        k.Box(metal, new Vector3(x1 + 0.12f, Walk + 3.2f, z0 + 3.7f), new Vector3(0.12f, 0.12f, 5.1f));
        k.Box(Mats.Glass(), new Vector3((x0 + x1) / 2 - 0.9f, Walk + 3.3f, z0 + 2.4f), new Vector3(1.6f, 0.03f, 2.2f));
        k.Box(Mats.Glass(), new Vector3((x0 + x1) / 2 + 0.9f, Walk + 3.3f, z0 + 5f), new Vector3(1.6f, 0.03f, 1.8f), new Basis(Vector3.Back, 0.25f));
        // Mât signalétique (disque pâle cerclé de sombre) à l'entrée.
        var tp = new Vector3(x0 - 1.4f, Walk, z0 - 0.8f);
        k.Col = new Color(0.2f, 0.22f, 0.21f);
        k.Cylinder(metal, tp, 0.09f, 0.08f, 3.4f, 8);
        k.Col = new Color(0.16f, 0.2f, 0.18f);
        k.Cylinder(d, tp + new Vector3(0, 3.6f, 0), 0.42f, 0.42f, 0.08f, 16, true, new Basis(Vector3.Right, Mathf.Pi / 2));
        k.Col = new Color(0.66f, 0.6f, 0.38f);
        k.Solid = false;
        k.Cylinder(c.Night ? Mats.Lamps() : d, tp + new Vector3(0, 3.6f, -0.045f), 0.32f, 0.32f, 0.01f, 16, true, new Basis(Vector3.Right, Mathf.Pi / 2));
        k.Solid = true;
        c.Ruins.Block(pit.Grow(1.6f));
    }
}
