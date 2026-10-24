using Godot;

namespace Rempart;

/// <summary>
/// RÃ©seau souterrain : galeries techniques (conduites, chemins de cÃ¢bles, rÃ©glettes), salle des pompes, couloir de
/// correspondance carrelÃ© (portillons, cadres d'affiches), puits de ventilation (jour blafard qui tombe d'une grille Ã 
/// 42 m, Ã©chelons), collecteur d'Ã©gout (cunette d'eau noire, salle de dÃ©cantation).
/// </summary>
public static partial class UndergroundBuilder
{
    private static void BuildGalleries(Ctx c)
    {
        var seed = 0;
        foreach (var r in c.Regions)
        {
            if (r.Kind != Kind.Gallery) continue;
            GalleryDressing(c, r, GalleryPoints(r.Name), seed++);
        }
        BuildPumpRoom(c);
        BuildCorridor(c);
        BuildShaft(c);
        BuildSewer(c);
    }

    private static Vector2[] GalleryPoints(string name)
    {
        static Vector2 V(float x, float z) => new(x, z);
        return name switch
        {
            "galerie" => new[] { V(-178, -104), V(210, -104) },
            "rameau-depot" => new[] { V(-150, -104), V(-150, -130) },
            "galerie-n1" => new[] { V(116, -104), V(116, -198), V(210, -198), V(210, -104) },
            "galerie-n2" => new[] { V(116, -150), V(210, -150) },
            _ => new[] { V(164, -104), V(164, -198) },
        };
    }

    /// <summary>Hors des grandes salles (jonctions avec la station, la salle des pompes, le puits) : pas d'habillage de galerie.</summary>
    private static bool InBigRoom(Ctx c, Vector3 p)
    {
        foreach (var r in c.Regions)
            if (r.Kind is Kind.Hall or Kind.Station or Kind.Shaft or Kind.Tunnel && r.Box.HasPoint(new Vector2(p.X, p.Z)) && r.Sdf(p.X, p.Z) < 1f) return true;
        return false;
    }

    /// <summary>Jonction avec une autre galerie, le couloir ou un tunnel : les conduites ne la barrent pas.</summary>
    private static bool NearOtherGallery(Ctx c, Region self, Vector3 p)
    {
        foreach (var r in c.Regions)
            if (r != self && r.Kind is Kind.Gallery or Kind.Corridor or Kind.Tunnel && r.Box.HasPoint(new Vector2(p.X, p.Z)) && r.Sdf(p.X, p.Z) < 3.5f) return true;
        return false;
    }

    /// <summary>
    /// Habillage d'une galerie technique : deux conduites sur consoles le long d'une paroi, chemin de cÃ¢bles en haut de
    /// l'autre, rÃ©glettes au plafond (rares allumÃ©es, lumiÃ¨re froide), lampes de secours, plaques signalÃ©tiques.
    /// </summary>
    private static void GalleryDressing(Ctx c, Region self, Vector2[] pts, int seed)
    {
        var met = Mats.DetailMetal();
        var pipe = Mats.Worn(new Color(0.32f, 0.36f, 0.3f), 0.6f, 0.35f);
        var pipe2 = Mats.Worn(new Color(0.45f, 0.2f, 0.12f), 0.6f, 0.35f);
        var n = 0;
        for (var i = 0; i + 1 < pts.Length; i++)
        {
            var a = new Vector3(pts[i].X, 0, pts[i].Y);
            var b = new Vector3(pts[i + 1].X, 0, pts[i + 1].Y);
            var dir = (b - a).Normalized();
            var side = new Vector3(-dir.Z, 0, dir.X);
            var len = a.DistanceTo(b);
            var basis = Basis.LookingAt(dir, Vector3.Up);
            for (var s = 0f; s < len; s += 6f)
            {
                var s1 = Mathf.Min(len, s + 6f);
                var mid = a + dir * ((s + s1) / 2);
                if (InBigRoom(c, mid) || NearOtherGallery(c, self, mid)) continue;
                var k = c.Dressing.At(mid);
                // Conduites (paroi Â« - Â»).
                k.Col = Colors.White;
                k.Box(pipe, mid - side * 3.55f + Vector3.Up * 1.25f, new Vector3(0.62f, 0.62f, s1 - s), basis);
                k.Box(pipe2, mid - side * 3.75f + Vector3.Up * 2.2f, new Vector3(0.34f, 0.34f, s1 - s), basis);
                k.Col = new Color(0.25f, 0.25f, 0.24f);
                k.Box(met, mid - side * 3.6f + Vector3.Up * 0.85f, new Vector3(0.9f, 0.08f, 0.12f), basis);
                k.Box(met, mid - side * 3.75f + Vector3.Up * 1.95f, new Vector3(0.5f, 0.06f, 0.1f), basis);
                // Chemin de cÃ¢bles (paroi Â« + Â»).
                k.Col = new Color(0.3f, 0.3f, 0.28f);
                k.Box(met, mid + side * 3.6f + Vector3.Up * 3.3f, new Vector3(0.5f, 0.05f, s1 - s), basis);
                k.Col = new Color(0.06f, 0.06f, 0.06f);
                k.Box(Mats.Detail(), mid + side * 3.6f + Vector3.Up * 3.38f, new Vector3(0.4f, 0.1f, s1 - s), basis);
                // RÃ©glette au plafond tous les 12 m (une sur quatre allumÃ©e).
                if (n % 2 == 0)
                {
                    var lit = (n / 2 + seed) % 4 == 1;
                    var top = CeilAt(c, mid.X, mid.Z, out _) - 0.15f;
                    k.Col = new Color(0.4f, 0.4f, 0.4f);
                    k.Box(met, new Vector3(mid.X, top, mid.Z), new Vector3(0.3f, 0.1f, 1.5f), basis);
                    var g = c.Glows.At(mid);
                    g.Box(lit ? Mats.Glow(Neon, 3f) : Mats.Painted(new Color(0.5f, 0.5f, 0.48f), 0.4f), new Vector3(mid.X, top - 0.07f, mid.Z), new Vector3(0.14f, 0.05f, 1.3f), basis);
                    if (lit) Omni(c, new Vector3(mid.X, top - 0.6f, mid.Z), Neon, 1.3f, 11f, 0.8f);
                }
                if (n % 5 == 3) WallLamp(c, mid + side * 4.0f + Vector3.Up * 2.4f, -side, (n + seed) % 3 == 0, 1.8f, 11f);
                if (n % 7 == 2) Debris(c, mid + side * (Hh(s, seed, 121) - 0.5f) * 5f, 500 + n + seed * 50);
                n++;
            }
        }
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Salle des pompes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Salle des pompes (x 136 â†’ 194, z -180 â†’ -124) : quatre groupes motopompes sur socles, collecteurs, vannes,
    /// pupitre aux Ã©crans encore allumÃ©s ; le nid occupe le centre.
    /// </summary>
    private static void BuildPumpRoom(Ctx c)
    {
        var k = c.Solid.At(new Vector3(165, 0, -152));
        var body = Mats.Worn(new Color(0.18f, 0.34f, 0.42f), 0.5f, 0.45f);
        var met = Mats.DetailMetal();
        foreach (var (x, z) in new[] { (143f, -131f), (187f, -131f), (143f, -173f), (187f, -173f), (165f, -175f) })
        {
            k.Col = new Color(0.4f, 0.39f, 0.37f);
            k.BoxMinMax(Mats.Detail(), new Vector3(x - 2.4f, 0, z - 2.4f), new Vector3(x + 2.4f, 0.5f, z + 2.4f));
            k.Col = Colors.White;
            k.Cylinder(body, new Vector3(x, 0.5f, z), 1.25f, 1.25f, 2.4f, 16);
            k.Cylinder(body, new Vector3(x, 2.9f, z), 0.85f, 0.7f, 1.3f, 14);
            k.Col = new Color(0.22f, 0.22f, 0.21f);
            k.Cylinder(met, new Vector3(x, 4.2f, z), 0.25f, 0.25f, 3.6f, 8);
            // Tubulure vers le collecteur de la paroi.
            var wallZ = z < -152 ? -180f : -124f;
            k.Cylinder(met, new Vector3(x, 1.4f, z), 0.38f, 0.38f, Mathf.Abs(wallZ - z), 10, false, new Basis(Vector3.Right, Mathf.Pi / 2 * Mathf.Sign(z - wallZ)));
        }
        // Collecteurs le long des murs nord et sud.
        foreach (var z in new[] { -125.2f, -178.8f })
        {
            k.Col = new Color(0.4f, 0.24f, 0.14f);
            k.Cylinder(Mats.Worn(new Color(0.45f, 0.2f, 0.12f), 0.6f, 0.35f), new Vector3(137f, 1.6f, z), 0.55f, 0.55f, 56f, 12, true, new Basis(Vector3.Back, Mathf.Pi / 2));
        }
        // Pupitre de commande et Ã©crans.
        k.Col = new Color(0.26f, 0.27f, 0.27f);
        k.BoxMinMax(met, new Vector3(190.5f, 0, -158f), new Vector3(193.2f, 1.1f, -146f));
        k.BoxMinMax(met, new Vector3(192.6f, 1.1f, -158f), new Vector3(193.4f, 2.4f, -146f));
        var g = c.Glows.At(new Vector3(192, 0, -152));
        for (var i = 0; i < 4; i++)
            g.Box(Mats.Glow(i == 2 ? new Color(0.9f, 0.2f, 0.12f) : new Color(0.35f, 0.75f, 0.6f), 1.8f), new Vector3(192.55f, 1.75f, -156.2f + i * 2.8f), new Vector3(0.03f, 0.55f, 0.9f));
        Omni(c, new Vector3(190.5f, 2f, -152f), new Color(0.45f, 0.8f, 0.7f), 1.1f, 8f, 0f);
        WallLamp(c, new Vector3(136.6f, 5.5f, -152f), Vector3.Right, true, 2.6f, 18f);
        WallLamp(c, new Vector3(165f, 5.5f, -179.6f), Vector3.Back, false);
        Flare(c, new Vector3(150f, 0.02f, -140f), 0.9f);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Couloir de correspondance â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Couloir de correspondance carrelÃ© (x = 0, z -36 â†’ -98) : ligne de portillons Ã  l'entrÃ©e cÃ´tÃ© station, cadres
    /// d'affiches, main courante, signalÃ©tique, rideau de fer Ã  demi baissÃ©.
    /// </summary>
    private static void BuildCorridor(Ctx c)
    {
        var met = Mats.DetailMetal();
        var k = c.Solid.At(new Vector3(0, 0, -44));
        // Portillons (5 bornes, passages de 1,2 m).
        for (var i = 0; i < 5; i++)
        {
            var x = -3.6f + i * 1.8f;
            k.Col = new Color(0.55f, 0.56f, 0.55f);
            k.BoxMinMax(met, new Vector3(x - 0.25f, 0, -44.6f), new Vector3(x + 0.25f, 1.05f, -42.4f));
            k.Col = new Color(0.15f, 0.15f, 0.15f);
            k.BoxMinMax(met, new Vector3(x - 0.26f, 1.05f, -44.6f), new Vector3(x + 0.26f, 1.1f, -42.4f));
        }
        var g = c.Glows.At(new Vector3(0, 0, -44));
        for (var i = 0; i < 5; i++) g.Box(Mats.Glow(i % 2 == 0 ? new Color(1f, 0.15f, 0.1f) : new Color(0.2f, 1f, 0.3f), 4f), new Vector3(-3.6f + i * 1.8f, 1.12f, -44.2f), new Vector3(0.12f, 0.02f, 0.12f));
        // Rideau de fer Ã  demi baissÃ©, Ã  la jonction de la galerie.
        k.Col = new Color(0.3f, 0.3f, 0.3f);
        k.Solid = false;
        for (var y = 3.0f; y < 5.4f; y += 0.12f) k.BoxMinMax(Mats.Corrugated(new Color(0.42f, 0.42f, 0.4f), 1831, 1f), new Vector3(-4.6f, y, -96.2f), new Vector3(4.6f, y + 0.1f, -96f));
        k.Solid = true;
        // Cadres d'affiches et main courante.
        for (var z = -50f; z > -92f; z -= 6f)
            foreach (var sgn in new[] { -1f, 1f })
            {
                var x = sgn * 4.35f;
                var dk = c.Dressing.At(new Vector3(x, 0, z));
                dk.Col = new Color(0.12f, 0.12f, 0.12f);
                dk.BoxMinMax(met, new Vector3(x - 0.06f, 1.2f, z - 1.6f), new Vector3(x + 0.06f, 3.0f, z + 1.6f));
                var tone = Hh(z, sgn, 131);
                dk.Col = new Color(0.3f + tone * 0.4f, 0.25f + tone * 0.2f, 0.2f + (1 - tone) * 0.3f) * 0.55f;
                dk.BoxMinMax(Mats.Detail(), new Vector3(x - 0.08f * sgn - 0.01f, 1.3f, z - 1.5f), new Vector3(x - 0.08f * sgn + 0.01f, 2.9f, z + 1.5f));
                dk.Col = new Color(0.5f, 0.5f, 0.48f);
                dk.BoxMinMax(met, new Vector3(x - 0.2f * sgn - 0.03f, 0.95f, z - 3f), new Vector3(x - 0.2f * sgn + 0.03f, 1.0f, z + 3f));
            }
        Flare(c, new Vector3(1.5f, 0.02f, -70f), 0.2f);
        WallLamp(c, new Vector3(-4.4f, 3.2f, -60f), Vector3.Right, false);
        WallLamp(c, new Vector3(4.4f, 3.2f, -84f), Vector3.Left, true, 1.6f, 10f);
        for (var i = 0; i < 8; i++) Debris(c, new Vector3((Hh(i, 3, 133) - 0.5f) * 7f, 0, -48f - i * 6.5f), 700 + i);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Puits de ventilation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Salle basse du puits de ventilation (0, -104) : cheminÃ©e de ~6 m de rayon jusqu'Ã  44 m, grille au sommet d'oÃ¹
    /// tombe un jour gris (faisceau dans la brume), Ã©chelons scellÃ©s, ventilateur effondrÃ© au fond.
    /// </summary>
    private static void BuildShaft(Ctx c)
    {
        var met = Mats.DetailMetal();
        var k = c.Dressing.At(new Vector3(0, 0, -104));
        // Grille sommitale (poutres croisÃ©es) et Ã©chelons le long de la paroi.
        k.Col = new Color(0.16f, 0.16f, 0.15f);
        for (var i = -3; i <= 3; i++)
        {
            k.BoxMinMax(met, new Vector3(-6.5f, 41.5f, -104f + i * 1.8f - 0.08f), new Vector3(6.5f, 41.8f, -104f + i * 1.8f + 0.08f));
            k.BoxMinMax(met, new Vector3(i * 1.8f - 0.08f, 41.3f, -110.5f), new Vector3(i * 1.8f + 0.08f, 41.5f, -97.5f));
        }
        for (var y = 8.5f; y < 41f; y += 0.4f)
            k.BoxMinMax(met, new Vector3(-0.3f, y, -98.4f), new Vector3(0.3f, y + 0.04f, -98.3f));
        foreach (var x in new[] { -0.32f, 0.32f }) k.BoxMinMax(met, new Vector3(x - 0.03f, 8.5f, -98.45f), new Vector3(x + 0.03f, 41f, -98.3f));
        // Ventilateur effondrÃ© (carter et pales) au pied du puits.
        var s = c.Solid.At(new Vector3(0, 0, -104));
        s.Col = new Color(0.3f, 0.32f, 0.3f);
        var tilt = new Basis(Vector3.Right, 1.1f) * new Basis(Vector3.Up, 0.4f);
        s.Cylinder(Mats.Worn(new Color(0.35f, 0.38f, 0.36f), 0.6f, 0.4f), new Vector3(1.5f, 0.2f, -105f), 2.6f, 2.6f, 1.6f, 18, false, tilt);
        s.Col = new Color(0.25f, 0.25f, 0.24f);
        for (var i = 0; i < 6; i++)
        {
            var a = i * Mathf.Tau / 6f;
            s.Box(met, new Vector3(1.5f, 1.2f, -105f) + tilt * new Vector3(Mathf.Cos(a) * 1.2f, 0.8f, Mathf.Sin(a) * 1.2f), new Vector3(0.9f, 0.05f, 2.1f), tilt * new Basis(Vector3.Up, a) * new Basis(Vector3.Back, 0.4f));
        }
        // Jour blafard qui tombe de la grille, et lueur diffuse au fond.
        var spot = new SpotLight3D
        {
            Position = new Vector3(0, 40.5f, -104f), RotationDegrees = new Vector3(-90, 0, 0), LightColor = Daylight, LightEnergy = 5.5f,
            SpotRange = 50f, SpotAngle = 14f, SpotAttenuation = 0.4f, ShadowEnabled = false, LightVolumetricFogEnergy = 3.2f,
            LightBakeMode = Light3D.BakeMode.Disabled,
        };
        c.Root.AddChild(spot);
        c.Lights++;
        Omni(c, new Vector3(0, 3f, -104f), Daylight, 0.7f, 14f, 0.2f);
        c.Root.AddChild(Drip(new Vector3(0, 38f, -104f), 4f, 34f));
        for (var i = 0; i < 6; i++) Debris(c, new Vector3((Hh(i, 1, 141) - 0.5f) * 9f, 0, -104f + (Hh(i, 2, 141) - 0.5f) * 9f), 800 + i);
    }

    /// <summary>Gouttes d'eau qui tombent (traits clairs), sur un disque de rayon <paramref name="r"/>.</summary>
    private static GpuParticles3D Drip(Vector3 p, float r, float fall)
    {
        var pm = new ParticleProcessMaterial
        {
            Direction = Vector3.Down,
            Spread = 0,
            InitialVelocityMin = 2f,
            InitialVelocityMax = 4f,
            Gravity = new Vector3(0, -9.8f, 0),
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(r, 0.2f, r),
        };
        var life = Mathf.Sqrt(2f * fall / 9.8f);
        return new GpuParticles3D
        {
            Name = "Drip",
            Position = p,
            Amount = 40,
            Lifetime = life,
            Preprocess = life,
            ProcessMaterial = pm,
            DrawPass1 = new QuadMesh
            {
                Size = new Vector2(0.025f, 0.5f),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.7f, 0.75f, 0.8f, 0.35f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, BillboardMode = BaseMaterial3D.BillboardModeEnum.FixedY,
                },
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            VisibilityRangeEnd = 90f,
            VisibilityAabb = new Aabb(new Vector3(-r - 1, -fall - 2, -r - 1), new Vector3(2 * r + 2, fall + 4, 2 * r + 2)),
        };
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€ Collecteur â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Collecteur d'Ã©gout : cunette d'eau noire au centre (banquettes de part et d'autre, praticables), conduites,
    /// lampes rares ; salle de dÃ©cantation (bassins Ã  murets bas remplis de boues) Ã  la jonction avec la descente.
    /// </summary>
    private static void BuildSewer(Ctx c)
    {
        // Eau noire et lisse : seuls les reflets des lampes et des lampes de casque s'y lisent.
        var water = Mats.Painted(new Color(0.012f, 0.014f, 0.012f), 0.05f, 0.25f);
        foreach (var pts in new[]
        {
            new[] { new Vector2(-136, 96), new Vector2(-62, 96) }, new[] { new Vector2(-18, 96), new Vector2(108, 96) },
            new[] { new Vector2(-40, 30), new Vector2(-40, 80) },
        })
        {
            var a = new Vector3(pts[0].X, 0, pts[0].Y);
            var b = new Vector3(pts[1].X, 0, pts[1].Y);
            var dir = (b - a).Normalized();
            var side = new Vector3(-dir.Z, 0, dir.X);
            var len = a.DistanceTo(b);
            var basis = Basis.LookingAt(dir, Vector3.Up);
            for (var s = 0f; s < len; s += 16f)
            {
                var s1 = Mathf.Min(len, s + 16f);
                var p0 = a + dir * s;
                var p1 = a + dir * s1;
                var k = c.Dressing.At((p0 + p1) / 2);
                k.Col = Colors.White;
                k.OrientedQuad(water, p0 - side * 1.3f + Vector3.Up * 0.04f, p1 - side * 1.3f + Vector3.Up * 0.04f, p1 + side * 1.3f + Vector3.Up * 0.04f, p0 + side * 1.3f + Vector3.Up * 0.04f, Vector3.Up);
                k.Col = new Color(0.3f, 0.29f, 0.27f);
                foreach (var sg in new[] { -1f, 1f })
                    k.Box(Mats.Detail(), (p0 + p1) / 2 + side * sg * 1.42f + Vector3.Up * 0.06f, new Vector3(0.24f, 0.12f, s1 - s), basis);
                k.Col = new Color(0.35f, 0.2f, 0.12f);
                k.Box(Mats.Worn(new Color(0.4f, 0.24f, 0.14f), 0.6f, 0.35f), (p0 + p1) / 2 + side * 3.9f + Vector3.Up * 2.6f, new Vector3(0.45f, 0.45f, s1 - s), basis);
            }
            for (var t = 10f; t < len; t += 30f)
                WallLamp(c, a + dir * t - side * 4.4f + Vector3.Up * 2.6f, side, ((int)t / 30) % 2 == 0, 1.5f, 11f);
        }
        // Salle de dÃ©cantation : bassins de part et d'autre de l'axe central (x -62 â†’ -18, z 80 â†’ 112).
        var sk = c.Solid.At(new Vector3(-40, 0, 96));
        foreach (var (xa, xb) in new[] { (-60f, -47f), (-33f, -20f) })
        foreach (var (za, zb) in new[] { (82f, 92f), (100f, 110f) })
        {
            sk.Col = new Color(0.4f, 0.38f, 0.35f);
            sk.BoxMinMax(Mats.Detail(), new Vector3(xa, 0, za), new Vector3(xb, 0.9f, za + 0.4f));
            sk.BoxMinMax(Mats.Detail(), new Vector3(xa, 0, zb - 0.4f), new Vector3(xb, 0.9f, zb));
            sk.BoxMinMax(Mats.Detail(), new Vector3(xa, 0, za), new Vector3(xa + 0.4f, 0.9f, zb));
            sk.BoxMinMax(Mats.Detail(), new Vector3(xb - 0.4f, 0, za), new Vector3(xb, 0.9f, zb));
            var dk = c.Dressing.At(new Vector3(xa, 0, za));
            dk.Col = Colors.White;
            dk.OrientedQuad(Mats.Sludge(), new Vector3(xa + 0.4f, 0.65f, za + 0.4f), new Vector3(xb - 0.4f, 0.65f, za + 0.4f), new Vector3(xb - 0.4f, 0.65f, zb - 0.4f), new Vector3(xa + 0.4f, 0.65f, zb - 0.4f), Vector3.Up);
            FlowObstacle(c, new Vector3((xa + xb) / 2, 0, (za + zb) / 2), new Vector3(xb - xa, 0.9f, zb - za));
        }
        WallLamp(c, new Vector3(-61.4f, 4f, 96f), Vector3.Right, true, 2.2f, 16f);
        Flare(c, new Vector3(-38f, 0.02f, 60f), 1.2f);
        Fire(c, new Vector3(-26f, 0, 97f), 0.7f);
    }
}
