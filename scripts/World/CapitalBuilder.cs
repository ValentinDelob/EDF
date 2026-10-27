using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Carte « Capitale en ruines » (spécification §9.1, finale de la campagne) : le cœur historique d'une capitale
/// européenne bombardé et à demi effondré. Grande place pavée à obélisque et fontaines (point de départ), avenue
/// monumentale jusqu'à un arc de triomphe sur son rond-point, cathédrale gothique et opéra face à face sur leurs parvis,
/// boulevard sous un viaduc de métro aérien, îlots haussmanniens (pierre de taille, balcons filants, combles de zinc),
/// jardin aux arbres morts, fleuve encaissé entre ses quais de pierre (berges basses, rampes), trois ponts de pierre
/// dont un éventré, parlement à dôme et ministères à colonnade sur la rive sud. Barricades, convois militaires calcinés,
/// cratères, incendies et fumées partout. Au-delà des murs : toits de zinc à perte de vue, dômes, colline à basilique,
/// tour de treillis brisée.
///
/// Tout se casse : immeubles et monuments (bâtiments destructibles, collision boîte + forme exacte de ce qui dépasse),
/// mobilier, véhicules, lampadaires, arbres. Escalier du viaduc, rampes des berges, toits accessibles aux grimpeurs.
/// Génération déterministe (graines fixes et hachages de position, jamais GD.Randf) : mêmes identifiants chez tous les pairs.
/// </summary>
public static partial class CapitalBuilder
{
    private const float Half = CityBuilder.Half;
    private const float South = CityBuilder.SeaStart;
    private const float Walk = 0.15f;
    private const float PaintY = 0.012f;

    // ───────────── Plan (x : ouest négatif, z : nord négatif) ─────────────

    /// <summary>Fleuve : murs de quai (nord, sud), bords des berges basses, niveaux (berges, eau, fond).</summary>
    public const float QuayN = 86f, QuayS = 126f, BergeN = 93f, BergeS = 119f;
    public const float BergeY = -4.2f, WaterY = -5f, BedY = -7.2f;

    /// <summary>Grande place (pavés) : départ des joueurs, obélisque au nord, fontaines est et ouest.</summary>
    private static readonly Rect2 Plaza = new(-76, -60, 152, 122);

    /// <summary>Boulevard du métro aérien : chaussées de part et d'autre du terre-plein central sous le viaduc.</summary>
    public const float BlvdZ0 = -120f, BlvdZ1 = -90f, MedianZ0 = -109f, MedianZ1 = -101f, ViaductZ = -105f, ViaductTop = 8.6f;

    /// <summary>Rond-point de l'arc de triomphe (centre, rayon de l'îlot central, rayon extérieur de l'anneau).</summary>
    private static readonly Vector3 ArchCenter = new(0, 0, -205);
    private const float ArchIsland = 30f, ArchRing = 46f;

    /// <summary>Ponts : centre x, demi-largeur, effondré (travée centrale soufflée).</summary>
    private static readonly (float x, float half, bool broken)[] Bridges = { (0f, 17f, false), (-152f, 8f, true), (152f, 8f, false) };

    /// <summary>Aire de départ (joueurs, char en (14, 22), hélicoptère en (-16, 24), escouades) : rien de solide.</summary>
    private static readonly Rect2 SpawnArea = new(-36, -34, 72, 78);

    /// <summary>Îlots haussmanniens (bords de trottoir).</summary>
    private static readonly Rect2[] Blocks =
    {
        // Rangée nord (au-delà de la rue z = -180), de part et d'autre du rond-point.
        R(-236, -236, -126, -188), R(-114, -236, -54, -188), R(54, -236, 114, -188), R(126, -236, 236, -188),
        // Colonnes de bordure de part et d'autre de la cathédrale et de l'opéra.
        R(-236, -172, -196, -120), R(196, -172, 236, -120),
        // Rangée entre le boulevard et la place.
        R(-236, -90, -158, -72), R(-146, -90, -88, -72), R(-76, -90, -16, -72), R(16, -90, 76, -72), R(88, -90, 146, -72), R(158, -90, 236, -72),
        // Est de la place.
        R(88, -60, 146, 62), R(158, -60, 236, 62),
    };

    private static Rect2 R(float x0, float z0, float x1, float z1) => new(x0, z0, x1 - x0, z1 - z0);

    private sealed class Ctx
    {
        public Node3D Root;
        public StaticBody3D Body;
        public CityBuilder.Result Res;
        public TimeOfDay Time;
        public bool Night;
        public FlowField Flow;
        public readonly PropScatter Props = new();
        public readonly Chunks Ground = new();          // marquages, bordures, avaloirs (sans ombres ni collision)
        public readonly Chunks Solid = new(true);       // quais, ponts, viaduc, monuments fixes (collision exacte)
        public RuinKit Ruins;
        public readonly List<Rect2> Raised = new();     // surfaces à 0,15 m (trottoirs, place, parvis, jardin)
        public readonly List<Rect2> KeepOut = new();    // emprises bâties (pas de mobilier ni de débris)
        public readonly List<Vector3> Hot = new();      // points chauds (gravats, cratères) pour les débris
        public readonly List<(Vector3 p, float r, float d)> HotSpots = new();
        public int NextId;
        public int LitLamps;
    }

    /// <summary>Décor fusionné par secteurs carrés (culling par secteur), collision exacte en option.</summary>
    private sealed class Chunks
    {
        private const float Size = 96f;
        private readonly Dictionary<(int, int), MeshKit> _kits = new();
        private readonly List<(int, int)> _order = new();
        private readonly bool _solid;

        public Chunks(bool solid = false) => _solid = solid;

        public MeshKit At(Vector3 p)
        {
            var key = (Mathf.FloorToInt(p.X / Size), Mathf.FloorToInt(p.Z / Size));
            if (_kits.TryGetValue(key, out var k)) return k;
            k = new MeshKit { Record = _solid };
            _kits[key] = k;
            _order.Add(key);
            return k;
        }

        public void Flush(Node3D root, string name, bool shadows, StaticBody3D body = null, float range = 0f)
        {
            foreach (var key in _order)
            {
                var k = _kits[key];
                if (k.Empty) continue;
                if (body != null) CityBuilder.AddSolid(body, k);
                root.AddChild(new MeshInstance3D
                {
                    Name = $"{name}_{key.Item1}_{key.Item2}",
                    Mesh = k.Commit(),
                    CastShadow = shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                    VisibilityRangeEnd = range,
                });
            }
            _kits.Clear();
            _order.Clear();
        }
    }

    public static CityBuilder.Result Build(Node3D root, TimeOfDay time)
    {
        var c = new Ctx { Root = root, Time = time, Night = time == TimeOfDay.Night, Res = new CityBuilder.Result(), Flow = World.Current?.Flow };
        c.Body = new StaticBody3D { Name = "Ground", CollisionLayer = 1, CollisionMask = 0 };
        root.AddChild(c.Body);
        c.Ruins = new RuinKit(root, c.Body, 0xCA91, c.Night);
        c.Ruins.Block(SpawnArea);
        foreach (var n in NestSpots) c.Ruins.Block(n, 9f, 9f);

        BuildGround(c);
        BuildRiver(c);
        BuildMonuments(c);     // immeubles d'abord (identifiants stables) : parlement, ministères, cathédrale, opéra
        BuildHaussmann(c);     // îlots haussmanniens
        BuildBridges(c);
        BuildViaduct(c);
        BuildArch(c);
        BuildPlaza(c);
        BuildPark(c);
        BuildStreets(c);
        DressCapital(c);
        BuildSkyline(c);

        c.Ground.Flush(root, "CapGround", false);
        c.Solid.Flush(root, "CapSolid", true, c.Body);
        c.Props.Flush(root);
        c.Ruins.Flush();

        // Murs invisibles en bordure (mêmes limites que les autres cartes).
        CityBuilder.AddWall(c.Body, new Vector3(0, 20, -Half - 10), new Vector3(Half * 2 + 40, 60, 2));
        CityBuilder.AddWall(c.Body, new Vector3(-Half - 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(Half + 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(0, 20, South + 8), new Vector3(Half * 2 + 40, 60, 2));

        var res = c.Res;
        res.Zones["north"] = new Vector3(0, 0, -150);     // avenue, entre la cathédrale et l'opéra
        res.Zones["center"] = new Vector3(-40, 0, -112);  // boulevard, sous le viaduc
        res.Zones["east"] = new Vector3(152, 0, 0);       // rue entre les îlots est
        res.Zones["west"] = new Vector3(-160, 0, -10);    // jardin
        res.Zones["south"] = new Vector3(0, 0, 138);      // quai sud, devant le parlement (au-delà du grand pont)
        res.Zones["beach"] = new Vector3(-120, 0, 72);    // quai nord, près du pont éventré
        foreach (var n in NestSpots) res.NestSpots.Add(n);
        res.PlayerSpawns.Add(new Vector3(-4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(-4, 1, 12));
        res.PlayerSpawns.Add(new Vector3(4, 1, 12));

        CityCam.Attach(root);
        AttachProbes(c);
        return res;
    }

    /// <summary>Emplacements de nids (ou de portails) : jardin, parvis de la cathédrale et de l'opéra, quai sud est.</summary>
    private static readonly Vector3[] NestSpots = { new(-170, Walk, -12), new(-62, Walk, -146), new(62, Walk, -146), new(160, Walk, 137) };

    // ───────────────────────── Étalonnage ─────────────────────────

    /// <summary>
    /// Lumière de la capitale, appliquée après <see cref="Grade.Apply"/> : ciel bas chargé de fumée, horizon rougi par les
    /// incendies, brume brune ; couleurs plus sourdes que dans les autres cartes (finale de la campagne).
    /// </summary>
    public static void ApplyGrade(Godot.Environment env, DirectionalLight3D sun, TimeOfDay time, Weather weather, float fog)
    {
        if (env.Sky?.SkyMaterial is not ShaderMaterial sky) return;
        void Clouds(Color zenith, Color horizon, Color ground, Color lit, Color dark, float coverage)
        {
            sky.SetShaderParameter("zenith", zenith);
            sky.SetShaderParameter("horizon", horizon);
            sky.SetShaderParameter("ground", ground);
            sky.SetShaderParameter("cloud_lit", lit);
            sky.SetShaderParameter("cloud_dark", dark);
            sky.SetShaderParameter("coverage", coverage);
        }
        switch (time)
        {
            case TimeOfDay.Dusk:
                // Crépuscule de fin du monde : bande de feu sale sous un plafond de fumée.
                Clouds(new Color(0.1f, 0.095f, 0.1f), new Color(0.46f, 0.25f, 0.15f), new Color(0.07f, 0.06f, 0.055f),
                    new Color(0.5f, 0.3f, 0.2f), new Color(0.09f, 0.085f, 0.09f), 0.74f);
                sun.RotationDegrees = new Vector3(-11, 62, 0);
                sun.LightEnergy = 1.25f;
                sun.LightColor = new Color(1f, 0.6f, 0.38f);
                env.AmbientLightColor = new Color(0.36f, 0.32f, 0.31f);
                env.AmbientLightEnergy = 0.85f;
                env.FogLightColor = new Color(0.38f, 0.28f, 0.23f);
                // Brume modérée : le vaisseau-mère, à 165 m au-dessus des têtes, doit rester lisible (tourelles, canon).
                env.FogDensity = fog * 1.2f + 0.0012f;
                env.TonemapExposure = 1.15f;
                env.AdjustmentSaturation = 0.85f;
                break;
            case TimeOfDay.Day:
                // Jour cendré : soleil voilé de fumée, lumière plate et sale.
                Clouds(new Color(0.3f, 0.3f, 0.31f), new Color(0.5f, 0.47f, 0.44f), new Color(0.18f, 0.17f, 0.16f),
                    new Color(0.6f, 0.58f, 0.55f), new Color(0.2f, 0.2f, 0.21f), 0.8f);
                sun.LightEnergy = 1.05f;
                sun.LightColor = new Color(0.98f, 0.9f, 0.8f);
                env.FogLightColor = new Color(0.46f, 0.44f, 0.41f);
                env.FogDensity = fog * 1.6f + 0.0016f;
                env.AdjustmentSaturation = 0.75f;
                break;
            default:
                // Nuit : plafond éclairé par en dessous par la ville qui brûle.
                Clouds(new Color(0.03f, 0.028f, 0.03f), new Color(0.2f, 0.11f, 0.07f), new Color(0.015f, 0.012f, 0.01f),
                    new Color(0.24f, 0.13f, 0.08f), new Color(0.035f, 0.032f, 0.035f), 0.7f);
                env.FogLightColor = new Color(0.16f, 0.11f, 0.09f);
                break;
        }
    }

    /// <summary>
    /// Brouillard volumétrique : fumée légère en suspension. Réduit : sur plusieurs centaines de mètres de visée vers
    /// le ciel, la densité par défaut noyait le vaisseau-mère dans une silhouette plate couleur de brume.
    /// </summary>
    public static float VolumetricFactor(Weather weather) => 0.3f;

    // ───────────────────────── Outils ─────────────────────────

    /// <summary>Réserve une emprise (mobilier et débris l'évitent).</summary>
    private static void Take(Ctx c, Rect2 r, float grow = 0.5f)
    {
        c.KeepOut.Add(r.Grow(grow));
        c.Ruins.Block(r.Grow(grow));
    }

    private static bool Clear(Ctx c, Vector3 p, float r = 0.6f)
    {
        foreach (var k in c.KeepOut)
            if (p.X > k.Position.X - r && p.X < k.End.X + r && p.Z > k.Position.Y - r && p.Z < k.End.Y + r) return false;
        return true;
    }

    /// <summary>Hauteur du sol (trottoirs, place, jardin à 0,15 m ; chaussée à 0 ; berges et fond du fleuve).</summary>
    private static float GroundY(Ctx c, float x, float z)
    {
        if (z > QuayN && z < QuayS)
        {
            foreach (var (bx, half, _) in Bridges)
                if (Mathf.Abs(x - bx) < half) return 0f;
            return z < BergeN || z > BergeS ? BergeY : BedY;
        }
        foreach (var r in c.Raised)
            if (r.HasPoint(new Vector2(x, z))) return Walk;
        return 0f;
    }

    private static Vector3 OnGround(Ctx c, float x, float z) => new(x, GroundY(c, x, z), z);

    /// <summary>Ouvrage bloqué dans le champ de flux (contourné, ou escaladé par les grimpeurs).</summary>
    private static void FlowBlock(Ctx c, Vector3 center, Vector3 size, float margin = 0.5f) =>
        c.Flow?.SetBlockedRect(center, size, true, margin);

    /// <summary>
    /// Bâtiment destructible au maillage sur mesure : immeuble ordinaire (volume plein, paliers de dégâts, effondrement)
    /// dont le visuel est remplacé par <paramref name="mesh"/> ; ce qui dépasse reçoit la forme exacte. <paramref name="hull"/> :
    /// volume plein convexe (angles coupés) à la place de la boîte.
    /// </summary>
    private static Building AddBuilding(Ctx c, Vector3 pos, Vector3 size, ArrayMesh mesh, Material facade, float hpMul = 1f, Vector3[] hull = null, bool open = false)
    {
        var b = Building.Create(c.NextId++, pos, size, facade, Mats.Roof(), new BuildingStyle { Kind = BuildingKind.Industrial });
        b.Visual.Mesh = mesh;
        // Volume plein remplacé : prisme convexe (angles coupés), ou rien du tout pour un monument ouvert (arc : la
        // collision suit exactement le maillage, l'emprise n'est pas bloquée dans le champ de flux).
        if (hull != null || open)
            foreach (var ch in b.GetChildren())
                if (ch is CollisionShape3D cs && cs.Shape is BoxShape3D) { b.RemoveChild(cs); cs.Free(); }
        b.Open = open;
        if (hull != null) b.AddChild(new CollisionShape3D { Name = "Hull", Shape = new ConvexPolygonShape3D { Points = hull } });
        if (MeshKit.ShapeOf(mesh) is { } detail) b.AddChild(new CollisionShape3D { Name = "Detail", Shape = detail });
        b.MaxHealth = b.Health = b.MaxHealth * hpMul;
        c.Root.AddChild(b);
        c.Res.Buildings.Add(b);
        Take(c, new Rect2(pos.X - size.X / 2, pos.Z - size.Z / 2, size.X, size.Z), 0.4f);
        return b;
    }

    /// <summary>Quad au sol (sans collision) dans le secteur de son centre.</summary>
    private static void GroundQuad(Ctx c, Material m, Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color col)
    {
        var k = c.Ground.At((a + cc) / 2);
        k.Col = col;
        k.OrientedQuad(m, a, b, cc, d, Vector3.Up);
    }

    // ───────────────────────── Sol ─────────────────────────

    /// <summary>Dalle surélevée (trottoir, place, jardin) à 0,15 m, bordure de granit ; collision.</summary>
    private static void Raised(Ctx c, Rect2 r, Material m, bool curb = true)
    {
        c.Raised.Add(r);
        CityBuilder.AddSlab(c.Root, c.Body, new Vector3(r.GetCenter().X, 0, r.GetCenter().Y), new Vector3(r.Size.X, 0.3f, r.Size.Y), m);
        if (!curb) return;
        var k = c.Ground.At(new Vector3(r.GetCenter().X, 0, r.GetCenter().Y));
        k.Col = Colors.White;
        k.Xf = new Transform3D(Basis.Identity, new Vector3(r.GetCenter().X, 0, r.GetCenter().Y));
        k.Ring(Mats.Curb(), r.Size.X / 2, r.Size.Y / 2, -0.1f, 0.155f, 0.28f, 0.02f);
        k.Xf = Transform3D.Identity;
    }

    private static void BuildGround(Ctx c)
    {
        // Chaussée de part et d'autre du fleuve (aire de jeu et premier rang de décor au-delà des murs).
        CityBuilder.AddSlab(c.Root, c.Body, new Vector3(0, -0.5f, (-262f + QuayN) / 2), new Vector3(524, 1f, QuayN + 262f), Mats.Asphalt());
        CityBuilder.AddSlab(c.Root, c.Body, new Vector3(0, -0.5f, (QuayS + 262f) / 2), new Vector3(524, 1f, 262f - QuayS), Mats.Asphalt());
        // Trottoirs des îlots.
        foreach (var b in Blocks) Raised(c, b, Mats.Sidewalk());
        // Grande place, parvis de la cathédrale et de l'opéra, îlot de l'arc : pavés de granit.
        Raised(c, Plaza, Mats.Cobbles());
        Raised(c, R(-184, -172, -16, -120), Mats.Cobbles());
        Raised(c, R(16, -172, 184, -120), Mats.Cobbles());
        // Terre-plein central du boulevard (piles du viaduc).
        Raised(c, R(-262, MedianZ0, 262, MedianZ1), Mats.Sidewalk());
        // Jardin : herbe morte et allées (voir BuildPark), muret de pierre.
        Raised(c, R(-262, -60, -88, 62), Mats.GroundTint());
        // Trottoirs des quais (parapet côté fleuve) et rive sud.
        Raised(c, R(-262, 62, 262, 66), Mats.Sidewalk());
        Raised(c, R(-262, 80, 262, QuayN), Mats.Sidewalk(), false);
        Raised(c, R(-262, QuayS, 262, 132), Mats.Sidewalk(), false);
        Raised(c, R(-70, 144, 70, 262), Mats.Cobbles());           // parvis du parlement
        Raised(c, R(-262, 144, -82, 262), Mats.Sidewalk());         // ministère ouest
        Raised(c, R(82, 144, 262, 262), Mats.Sidewalk());           // ministère est
    }
}
