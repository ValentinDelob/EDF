using Godot;

namespace Rempart;

/// <summary>
/// Modèles procéduraux des objectifs de mission : antenne relais de campagne, poste de commandement, camion de convoi.
/// Convention des accessoires : l'avant regarde +Z, posé sur y = 0.
/// </summary>
public static class ObjectiveModels
{
    private static readonly Color Drab = new(0.23f, 0.27f, 0.17f);
    private static readonly Color DrabDark = new(0.16f, 0.19f, 0.12f);
    private static readonly Color Canvas = new(0.36f, 0.37f, 0.25f);
    private static readonly Color Steel = new(0.48f, 0.5f, 0.52f);

    private static StandardMaterial3D _vc, _vcMetal;

    /// <summary>Peinture à la couleur des sommets, sans salissures en coordonnées monde (modèles qui se déplacent).</summary>
    private static StandardMaterial3D Vc => _vc ??= new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.8f };
    private static StandardMaterial3D VcMetal => _vcMetal ??= new StandardMaterial3D { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.45f, Metallic = 0.5f };

    private static MeshInstance3D Inst(MeshKit k, string name) => new() { Name = name, Mesh = k.Commit() };

    private static MeshInstance3D Light(string name, Color c, Vector3 pos, float r) => new()
    {
        Name = name, Position = pos, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        Mesh = new SphereMesh { Radius = r, Height = r * 2, RadialSegments = 10, Rings = 5, Material = Mats.Glow(c, 6f) },
    };

    // ------------------------------------------------------------------ Antenne relais

    /// <summary>
    /// Antenne relais de campagne : dalle, murets de sacs de sable (entrée au sud), abri technique, groupe électrogène,
    /// pylône en treillis de 24 m (nœud « Mast », pivot au pied : il s'abat quand la structure tombe), panneaux, paraboles, haubans.
    /// </summary>
    public static Node3D RelayAntenna(out MeshInstance3D beacon)
    {
        var root = new Node3D { Name = "RelayAntenna" };
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        var k = new MeshKit();
        k.Col = new Color(0.55f, 0.54f, 0.51f);
        k.BoxMinMax(d, new Vector3(-4.4f, 0, -4.4f), new Vector3(4.4f, 0.3f, 4.4f));
        // Sacs de sable sur le pourtour, ouverture de 2,4 m au sud (+Z).
        Props.SandbagWall(k, new Vector3(-4.4f, 0.3f, -4.4f), new Vector3(4.4f, 0.3f, -4.4f), 4, 11);
        Props.SandbagWall(k, new Vector3(4.4f, 0.3f, -4.4f), new Vector3(4.4f, 0.3f, 4.4f), 4, 12);
        Props.SandbagWall(k, new Vector3(-4.4f, 0.3f, 4.4f), new Vector3(-4.4f, 0.3f, -4.4f), 4, 13);
        Props.SandbagWall(k, new Vector3(4.4f, 0.3f, 4.4f), new Vector3(1.2f, 0.3f, 4.4f), 4, 14);
        Props.SandbagWall(k, new Vector3(-1.2f, 0.3f, 4.4f), new Vector3(-4.4f, 0.3f, 4.4f), 4, 15);
        // Abri technique (conteneur kaki), porte, climatiseur.
        k.Col = Drab;
        k.BoxMinMax(d, new Vector3(-3.6f, 0.3f, -0.4f), new Vector3(-1.2f, 2.8f, 3.4f));
        k.Col = DrabDark;
        k.Box(d, new Vector3(-1.17f, 1.3f, 1.5f), new Vector3(0.06f, 1.9f, 0.9f));
        k.Col = new Color(0.62f, 0.63f, 0.6f);
        k.Box(m, new Vector3(-2.4f, 2.0f, 3.45f), new Vector3(0.9f, 0.6f, 0.12f));
        k.Col = new Color(0.1f, 0.1f, 0.1f);
        k.Box(m, new Vector3(-2.4f, 3.0f, 1.5f), new Vector3(2.2f, 0.12f, 3.6f)); // toit
        // Groupe électrogène et câbles.
        k.Col = new Color(0.42f, 0.4f, 0.16f);
        k.BoxMinMax(m, new Vector3(1.8f, 0.3f, 1.6f), new Vector3(3.6f, 1.5f, 3.4f));
        k.Col = new Color(0.08f, 0.08f, 0.08f);
        k.Tube(m, new Vector3(3.2f, 1.5f, 2.0f), new Vector3(3.2f, 2.3f, 2.0f), 0.07f);
        k.Tube(m, new Vector3(1.8f, 0.36f, 2.5f), new Vector3(0.6f, 0.36f, 0.6f), 0.05f);
        k.Tube(m, new Vector3(-1.2f, 0.36f, 0.4f), new Vector3(-0.6f, 0.36f, 0.4f), 0.05f);
        // Haubans (trois ancrages au sol, sur la dalle).
        k.Col = new Color(0.25f, 0.25f, 0.25f);
        foreach (var a in new[] { 0.6f, 2.7f, 4.8f })
        {
            var anchor = new Vector3(Mathf.Cos(a) * 4f, 0.35f, Mathf.Sin(a) * 4f);
            k.Tube(m, anchor, new Vector3(Mathf.Cos(a) * 0.5f, 15f, Mathf.Sin(a) * 0.5f), 0.025f, 4);
            k.Box(m, anchor, new Vector3(0.3f, 0.12f, 0.3f));
        }
        root.AddChild(Inst(k, "Base"));

        var mast = new Node3D { Name = "Mast" };
        root.AddChild(mast);
        var t = new MeshKit();
        const float h = 24f;
        const int legs = 3;
        Vector3 Leg(int i, float y)
        {
            var r = Mathf.Lerp(1.1f, 0.38f, y / h);
            var a = i * Mathf.Tau / legs + 0.3f;
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        }
        t.Col = Steel;
        for (var i = 0; i < legs; i++) t.Tube(m, Leg(i, 0.3f), Leg(i, h), 0.07f, 6, true);
        for (var y = 0.3f; y < h - 0.5f; y += 2.4f)
        for (var i = 0; i < legs; i++)
        {
            // Bandes rouges et blanches sur le dernier tiers (balisage aérien).
            t.Col = y > h * 0.62f ? (Mathf.FloorToInt(y / 2.4f) % 2 == 0 ? new Color(0.7f, 0.1f, 0.08f) : new Color(0.85f, 0.85f, 0.82f)) : Steel;
            var j = (i + 1) % legs;
            t.Tube(m, Leg(i, y + 2.4f), Leg(j, y + 2.4f), 0.035f, 4);
            t.Tube(m, Leg(i, y), Leg(j, y + 2.4f), 0.03f, 4);
        }
        // Panneaux d'antenne, paraboles, fouet au sommet.
        t.Col = new Color(0.82f, 0.82f, 0.8f);
        for (var i = 0; i < 3; i++)
        {
            var a = i * Mathf.Tau / 3 + 0.3f + Mathf.Pi / 3;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            t.Box(m, dir * 0.75f + Vector3.Up * (h - 2.2f), new Vector3(0.45f, 2.2f, 0.16f), new Basis(Vector3.Up, -a + Mathf.Pi / 2));
        }
        foreach (var (y, a) in new[] { (h * 0.7f, 1.2f), (h * 0.55f, 3.6f) })
        {
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var basis = new Basis(Vector3.Up, -a + Mathf.Pi / 2) * new Basis(Vector3.Right, Mathf.Pi / 2);
            t.Cylinder(m, dir * 0.9f + Vector3.Up * y, 0.75f, 0.55f, 0.25f, 14, true, basis);
        }
        t.Col = new Color(0.12f, 0.12f, 0.12f);
        t.Tube(m, Vector3.Up * h, Vector3.Up * (h + 3.5f), 0.04f, 4);
        mast.AddChild(Inst(t, "Lattice"));
        beacon = Light("Beacon", new Color(1f, 0.12f, 0.06f), Vector3.Up * (h + 0.3f), 0.28f);
        mast.AddChild(beacon);
        return root;
    }

    // ------------------------------------------------------------------ Poste de commandement

    /// <summary>Poste de commandement de campagne : conteneur, filet de camouflage, antennes fouet, sacs de sable.</summary>
    public static Node3D CommandPost(out MeshInstance3D beacon)
    {
        var root = new Node3D { Name = "CommandPost" };
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        var k = new MeshKit();
        k.Col = new Color(0.55f, 0.54f, 0.51f);
        k.BoxMinMax(d, new Vector3(-5f, 0, -4f), new Vector3(5f, 0.25f, 4f));
        k.Col = Drab;
        k.BoxMinMax(d, new Vector3(-4.2f, 0.25f, -2.8f), new Vector3(1.8f, 2.85f, -0.3f));
        k.Col = Canvas;
        k.Box(d, new Vector3(0, 3.6f, 0), new Vector3(9.4f, 0.08f, 7.2f), new Basis(Vector3.Right, 0.08f));
        foreach (var x in new[] { -4.5f, 4.5f })
        foreach (var z in new[] { -3.4f, 3.4f })
        {
            k.Col = new Color(0.2f, 0.18f, 0.14f);
            k.Tube(m, new Vector3(x, 0.25f, z), new Vector3(x, 3.6f - z * 0.08f, z), 0.06f);
        }
        k.Col = new Color(0.3f, 0.28f, 0.22f);
        k.BoxMinMax(d, new Vector3(0.6f, 0.25f, 0.6f), new Vector3(3.2f, 1.05f, 1.8f)); // table des cartes
        k.Col = new Color(0.1f, 0.1f, 0.1f);
        foreach (var x in new[] { -3.6f, -2.8f }) k.Tube(m, new Vector3(x, 2.85f, -2.2f), new Vector3(x, 9.5f, -2.2f), 0.03f, 4);
        Props.SandbagWall(k, new Vector3(-5f, 0.25f, 4f), new Vector3(-1.2f, 0.25f, 4f), 4, 21);
        Props.SandbagWall(k, new Vector3(1.2f, 0.25f, 4f), new Vector3(5f, 0.25f, 4f), 4, 22);
        root.AddChild(Inst(k, "Post"));
        beacon = Light("Beacon", new Color(0.3f, 1f, 0.4f), new Vector3(-3.6f, 9.6f, -2.2f), 0.18f);
        root.AddChild(beacon);
        return root;
    }

    // ------------------------------------------------------------------ Camion de convoi

    /// <summary>Camion militaire de ravitaillement (bâché, six roues), phares allumés, gyrophare orange sur la cabine.</summary>
    public static Node3D Truck(out MeshInstance3D beacon)
    {
        var root = new Node3D { Name = "Truck" };
        var p = Vc;
        var m = VcMetal;
        var glass = Mats.Painted(new Color(0.05f, 0.07f, 0.08f), 0.08f, 0.4f);
        var k = new MeshKit();
        const float l = 7.6f;
        var rot = new Basis(Vector3.Back, -Mathf.Pi / 2);
        foreach (var z in new[] { 2.6f, -1.4f, -2.65f })
        foreach (var x in new[] { -1.05f, 1.05f })
        {
            k.Col = new Color(0.05f, 0.05f, 0.05f);
            k.Cylinder(p, new Vector3(x - 0.18f, 0.55f, z), 0.55f, 0.55f, 0.36f, 14, true, rot);
            k.Col = DrabDark;
            k.Cylinder(p, new Vector3(x - 0.19f, 0.55f, z), 0.3f, 0.3f, 0.38f, 8, true, rot);
        }
        k.Col = new Color(0.08f, 0.08f, 0.07f);
        k.Box(m, new Vector3(0, 0.95f, -0.2f), new Vector3(1.6f, 0.35f, l - 0.6f));
        // Capot, cabine, pare-brise.
        k.Col = Drab;
        k.BoxMinMax(p, new Vector3(-1.1f, 1.0f, l / 2 - 1.4f), new Vector3(1.1f, 1.85f, l / 2));
        k.BoxMinMax(p, new Vector3(-1.2f, 1.0f, l / 2 - 3.1f), new Vector3(1.2f, 2.85f, l / 2 - 1.4f));
        k.Col = DrabDark;
        foreach (var x in new[] { -1.3f, 1.3f }) k.Box(p, new Vector3(x, 1.4f, l / 2 - 0.6f), new Vector3(0.25f, 0.12f, 1.4f));
        k.Box(glass, new Vector3(0, 2.35f, l / 2 - 1.38f), new Vector3(2.1f, 0.7f, 0.04f));
        k.Box(glass, new Vector3(0, 2.35f, l / 2 - 2.25f), new Vector3(2.42f, 0.55f, 0.8f));
        k.Col = new Color(0.1f, 0.1f, 0.09f);
        k.Box(m, new Vector3(0, 1.3f, l / 2 + 0.05f), new Vector3(2.0f, 0.5f, 0.12f));
        // Plateau et bâche.
        var z0 = l / 2 - 3.2f;
        var z1 = -l / 2;
        k.Col = DrabDark;
        k.BoxMinMax(p, new Vector3(-1.25f, 1.15f, z1), new Vector3(1.25f, 1.35f, z0));
        k.Col = Canvas;
        k.BoxMinMax(p, new Vector3(-1.26f, 1.35f, z1), new Vector3(1.26f, 3.0f, z0));
        k.Box(p, new Vector3(-0.62f, 3.2f, (z0 + z1) / 2), new Vector3(1.32f, 0.06f, z0 - z1), new Basis(Vector3.Back, 0.32f));
        k.Box(p, new Vector3(0.62f, 3.2f, (z0 + z1) / 2), new Vector3(1.32f, 0.06f, z0 - z1), new Basis(Vector3.Back, -0.32f));
        // Marquage blanc sur les flancs de la bâche (repère du convoi).
        k.Col = new Color(0.85f, 0.85f, 0.8f);
        foreach (var x in new[] { -1.27f, 1.27f }) k.Box(p, new Vector3(x, 2.2f, (z0 + z1) / 2), new Vector3(0.02f, 0.35f, 2.2f));
        // Phares.
        k.Col = Colors.White;
        foreach (var x in new[] { -0.8f, 0.8f }) k.Box(Mats.Glow(new Color(1f, 0.92f, 0.75f), 2.5f), new Vector3(x, 1.55f, l / 2 + 0.01f), new Vector3(0.32f, 0.2f, 0.04f));
        root.AddChild(Inst(k, "Body"));
        // Socle du gyrophare.
        root.AddChild(new MeshInstance3D
        {
            Name = "BeaconBase", Position = new Vector3(0, 2.92f, l / 2 - 2.2f),
            Mesh = new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.18f, Height = 0.12f, Material = Mats.Painted(new Color(0.1f, 0.1f, 0.1f)) },
        });
        beacon = Light("Beacon", new Color(1f, 0.55f, 0.05f), new Vector3(0, 3.05f, l / 2 - 2.2f), 0.16f);
        root.AddChild(beacon);
        return root;
    }

    /// <summary>Épave : modèle noirci (voile sombre par-dessus tous les maillages).</summary>
    public static void Char(Node3D model)
    {
        if (model == null) return;
        var veil = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.03f, 0.025f, 0.02f, 0.78f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        foreach (var n in model.FindChildren("*", "MeshInstance3D", true, false))
            ((MeshInstance3D)n).MaterialOverlay = veil;
    }
}
