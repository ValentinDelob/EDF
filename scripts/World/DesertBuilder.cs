using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Carte « Base militaire du désert » (spécification §9.1 : grands espaces, véhicules abondants) : base aérienne
/// fortifiée submergée par l'invasion. Piste et voie de circulation au nord, tarmac avec deux grands hangars ouverts,
/// abris bétonnés et tour de contrôle ; casernements et soutes à munitions à l'ouest, parc automobile, dépôt de
/// carburant, radôme et pylône de transmissions à l'est ; rempart de gabions et miradors tout autour, poste de
/// contrôle à la porte sud, route du désert au-delà. L'aire de jeu garde les limites communes (champ de flux, murs à
/// ±230 m et z = 166) ; au-delà, dunes, mesas et radômes lointains sans collision.
///
/// Véhicules abondants : trois chars et deux hélicoptères pilotables en plus du char et de l'hélicoptère de départ
/// (<see cref="CityBuilder.Result.Vehicles"/>, identifiants 3 à 7). Bâtiments destructibles (<see cref="Building"/>,
/// les hangars sont des bâtiments ouverts à collision exacte), gabions, murs en T, miradors, véhicules et caisses
/// destructibles, cuves et soutes à munitions explosives.
///
/// Génération déterministe (graines fixes et hachages de position, jamais GD.Randf) : mêmes identifiants chez tous les pairs.
/// </summary>
public static partial class DesertBuilder
{
    private const float Half = CityBuilder.Half;
    private const float South = CityBuilder.SeaStart;

    /// <summary>Piste (béton) : bords nord et sud ; voie de circulation (axe) ; tarmac (bords nord et sud).</summary>
    public const float RunwayZ0 = -178f, RunwayZ1 = -138f, TaxiZ = -112f, ApronZ0 = -104f, ApronZ1 = -40f;

    /// <summary>Rempart : lignes de gabions (nord, sud, ouest, est) et demi-largeur de la porte sud.</summary>
    public const float WallN = -205f, WallS = 136f, WallW = -205f, WallE = 205f, GateHalf = 9f;

    private const float RoadHalf = 6f;

    /// <summary>Aire de départ (joueurs, char en (14, 22), hélicoptère en (-16, 24), escouades) : rien de solide.</summary>
    private static readonly Rect2 SpawnArea = new(-36, -34, 72, 78);

    private sealed class Ctx
    {
        public Node3D Root;
        public StaticBody3D Body;
        public CityBuilder.Result Res;
        public TimeOfDay Time;
        public bool Night;
        /// <summary>Tempête de sable : pas de marques sombres (non soumises au brouillard, elles flotteraient en noir dans la poussière).</summary>
        public bool Storm;
        public FlowField Flow;
        public readonly PropScatter Props = new();
        public readonly Chunks Ground = new();          // pistes, routes, marquages (sans ombres ni collision)
        public readonly Chunks Solid = new(true);       // dunes, rochers, ouvrages, épaves fixes (collision exacte)
        public RuinKit Ruins;
        public readonly List<Rect2> Taken = new();      // emprises réservées (pas de mobilier ni de débris)
        public int NextId;
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
        var c = new Ctx
        {
            Root = root, Time = time, Night = time == TimeOfDay.Night, Res = new CityBuilder.Result(), Flow = World.Current?.Flow,
            Storm = World.Current?.Mission.Weather == Weather.Sandstorm,
        };
        c.Body = new StaticBody3D { Name = "Ground", CollisionLayer = 1, CollisionMask = 0 };
        root.AddChild(c.Body);
        c.Ruins = new RuinKit(root, c.Body, 0xDE5E, c.Night);
        c.Ruins.Block(SpawnArea);
        c.Taken.Add(SpawnArea);
        foreach (var n in NestSpots) Take(c, n, 10f, 10f, 0f);
        Props.SandbagTint = new Color(0.55f, 0.5f, 0.39f); // sacs de sable couleur sable (remis à zéro en fin de génération)

        BuildGround(c);
        BuildAirfield(c);      // piste, voie de circulation, tarmac, marquages
        BuildRoads(c);
        BuildHangars(c);       // immeubles d'abord (identifiants stables) : hangars, abris, tour de contrôle
        BuildCamp(c);          // casernements, parc automobile, radôme, transmissions
        BuildDepots(c);        // dépôt de carburant, soutes à munitions
        BuildPerimeter(c);     // rempart, miradors, porte sud
        BuildVehicles(c);      // véhicules pilotables supplémentaires
        DressDesert(c);        // épaves, cratères, dunes, rochers, débris, nids
        BuildSkyline(c);

        c.Ground.Flush(root, "DesGround", false);
        c.Solid.Flush(root, "DesSolid", true, c.Body);
        c.Props.Flush(root);
        c.Ruins.Flush();
        Props.SandbagTint = null;

        // Murs invisibles en bordure (mêmes limites que les autres cartes).
        CityBuilder.AddWall(c.Body, new Vector3(0, 20, -Half - 10), new Vector3(Half * 2 + 40, 60, 2));
        CityBuilder.AddWall(c.Body, new Vector3(-Half - 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(Half + 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(0, 20, South + 8), new Vector3(Half * 2 + 40, 60, 2));

        // Brume de sable : brouillard beige de l'étalonnage (ApplyGrade) ; nappes de poussière seulement par tempête
        // (WeatherFx : par beau temps, leur surcoût de remplissage au ras de la caméra doublait le temps GPU).

        var res = c.Res;
        res.Zones["north"] = new Vector3(-20, 0, -193);   // bande de sable au nord de la piste
        res.Zones["center"] = new Vector3(22, 0, -90);    // tarmac, devant la tour de contrôle
        res.Zones["east"] = new Vector3(182, 0, -12);     // entre le hangar est et le dépôt de carburant
        res.Zones["west"] = new Vector3(-165, 0, -30);    // entre le grand hangar et les casernements
        res.Zones["south"] = new Vector3(-20, 0, 120);    // derrière la porte sud
        res.Zones["beach"] = new Vector3(70, 0, 148);     // route du désert, hors du rempart
        foreach (var n in NestSpots) res.NestSpots.Add(n);
        res.PlayerSpawns.Add(new Vector3(-4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(-4, 1, 12));
        res.PlayerSpawns.Add(new Vector3(4, 1, 12));

        CityCam.Attach(root);
        AttachProbes(c);
        return res;
    }

    /// <summary>Emplacements de nids : sable dégagé (ouest, est, nord de la piste, sud-est derrière le radôme).</summary>
    private static readonly Vector3[] NestSpots = { new(-165, 0, -30), new(180, 0, -12), new(-60, 0, -192), new(64, 0, 124) };

    // ───────────────────────── Étalonnage ─────────────────────────

    /// <summary>
    /// Lumière du désert, appliquée après <see cref="Grade.Apply"/> : soleil plus franc et plus chaud, ciel voilé de
    /// poussière, brume beige ; la tempête de sable noie tout dans un brouillard brun. Le gris de l'invasion reste
    /// (désaturation et table de couleurs communes).
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
        var storm = weather == Weather.Sandstorm;
        switch (time)
        {
            case TimeOfDay.Day:
                // Chaleur blanche : ciel délavé, voile de poussière à l'horizon, soleil haut et dur.
                Clouds(new Color(0.42f, 0.44f, 0.45f), new Color(0.66f, 0.61f, 0.53f), new Color(0.3f, 0.27f, 0.22f),
                    new Color(0.72f, 0.69f, 0.64f), new Color(0.38f, 0.36f, 0.33f), 0.42f);
                sun.RotationDegrees = new Vector3(-52, -28, 0);
                sun.LightEnergy = 1.65f;
                sun.LightColor = new Color(1f, 0.92f, 0.8f);
                env.AmbientLightColor = new Color(0.5f, 0.47f, 0.42f);
                env.AmbientLightEnergy = 0.75f;
                env.FogLightColor = new Color(0.64f, 0.58f, 0.49f);
                env.FogDensity = fog * 1.1f + 0.0015f;
                env.TonemapExposure = 1.0f;
                env.AdjustmentSaturation = 0.56f;
                break;
            case TimeOfDay.Dusk:
                // Soleil bas cuivré dans la poussière, ombres longues.
                Clouds(new Color(0.16f, 0.15f, 0.16f), new Color(0.6f, 0.38f, 0.24f), new Color(0.1f, 0.08f, 0.07f),
                    new Color(0.62f, 0.42f, 0.3f), new Color(0.16f, 0.13f, 0.12f), 0.5f);
                sun.RotationDegrees = new Vector3(-13, 75, 0);
                sun.LightEnergy = 1.45f;
                sun.LightColor = new Color(1f, 0.64f, 0.4f);
                env.AmbientLightColor = new Color(0.42f, 0.35f, 0.31f);
                env.AmbientLightEnergy = 0.85f;
                env.FogLightColor = new Color(0.52f, 0.38f, 0.28f);
                env.FogDensity = fog * 1.0f + 0.0018f;
                env.TonemapExposure = 1.15f;
                break;
            default:
                // Nuit froide du désert : ciel dégagé, brume sableuse à peine teintée.
                env.FogLightColor = new Color(0.15f, 0.13f, 0.12f);
                break;
        }
        if (storm)
        {
            // Tempête de sable : plafond brun opaque, soleil réduit à une tache, visibilité ~120 m (rafales : WeatherFx).
            Clouds(new Color(0.36f, 0.3f, 0.23f), new Color(0.5f, 0.42f, 0.32f), new Color(0.3f, 0.25f, 0.19f),
                new Color(0.52f, 0.44f, 0.34f), new Color(0.28f, 0.23f, 0.18f), 0.95f);
            sky.SetShaderParameter("wind", 0.03f);
            sun.LightEnergy = time == TimeOfDay.Night ? 0.25f : 0.55f;
            sun.LightColor = new Color(1f, 0.78f, 0.55f);
            env.AmbientLightColor = new Color(0.52f, 0.44f, 0.35f);
            env.AmbientLightEnergy = time == TimeOfDay.Night ? 0.7f : 1.15f;
            env.FogLightColor = time == TimeOfDay.Night ? new Color(0.2f, 0.16f, 0.12f) : new Color(0.5f, 0.42f, 0.32f);
            env.FogDensity = 0.014f + fog;
            env.FogSkyAffect = 0.85f;
            env.FogAerialPerspective = 0.3f;
            env.AdjustmentSaturation = 0.6f;
        }
    }

    /// <summary>Facteur du brouillard volumétrique : air sec et clair par beau temps, sable en suspension par tempête.</summary>
    public static float VolumetricFactor(Weather weather) => weather == Weather.Sandstorm ? 2.2f : 0.45f;

    // ───────────────────────── Outils ─────────────────────────

    /// <summary>Réserve une emprise (mobilier, débris et dunes l'évitent).</summary>
    private static void Take(Ctx c, Rect2 r, float grow = 0.5f)
    {
        c.Taken.Add(r.Grow(grow));
        c.Ruins.Block(r.Grow(grow));
    }

    private static void Take(Ctx c, Vector3 center, float halfX, float halfZ, float grow = 0.5f) =>
        Take(c, new Rect2(center.X - halfX, center.Z - halfZ, halfX * 2, halfZ * 2), grow);

    private static bool IsTaken(Ctx c, Vector3 p, float r = 0f)
    {
        foreach (var t in c.Taken)
            if (p.X > t.Position.X - r && p.X < t.End.X + r && p.Z > t.Position.Y - r && p.Z < t.End.Y + r) return true;
        return false;
    }

    /// <summary>Ouvrage plein bloqué dans le champ de flux avec sa hauteur (contourné, ou escaladé par les grimpeurs).</summary>
    private static void FlowBlock(Ctx c, Vector3 center, Vector3 size, float margin = 0.5f) =>
        c.Flow?.SetBlockedRect(center, size, true, margin);

    /// <summary>
    /// Bâtiment destructible au maillage sur mesure : immeuble « industriel » (boîte pleine, paliers de dégâts,
    /// effondrement) dont le visuel est remplacé par <paramref name="mesh"/> ; ce qui dépasse reçoit la forme exacte.
    /// </summary>
    private static Building AddBuilding(Ctx c, Vector3 pos, Vector3 size, ArrayMesh mesh, Material facade, float hpMul = 1f)
    {
        var b = Building.Create(c.NextId++, pos, size, facade, Mats.Roof(), new BuildingStyle { Kind = BuildingKind.Industrial });
        b.Visual.Mesh = mesh;
        if (MeshKit.ShapeOf(mesh) is { } detail) b.AddChild(new CollisionShape3D { Name = "Detail", Shape = detail });
        b.MaxHealth = b.Health = b.MaxHealth * hpMul;
        c.Root.AddChild(b);
        c.Res.Buildings.Add(b);
        Take(c, new Rect2(pos.X - size.X / 2, pos.Z - size.Z / 2, size.X, size.Z), 1f);
        return b;
    }

    /// <summary>
    /// Bâtiment ouvert (hangar, abri, auvent) : aucun volume plein, la collision suit exactement le maillage (on entre,
    /// on monte, les insectes escaladent les parois) ; l'emprise n'est pas bloquée dans le champ de flux (les parois le
    /// sont par le générateur). Destructible comme les autres.
    /// </summary>
    private static Building AddOpen(Ctx c, Vector3 pos, Vector3 size, ArrayMesh mesh, Material facade, float hpMul = 1f)
    {
        var b = Building.Create(c.NextId++, pos, size, facade, Mats.Roof(), new BuildingStyle { Kind = BuildingKind.Industrial });
        b.Visual.Mesh = mesh;
        foreach (var ch in b.GetChildren())
            if (ch is CollisionShape3D cs) { b.RemoveChild(cs); cs.Free(); }
        if (MeshKit.ShapeOf(mesh) is { } exact) b.AddChild(new CollisionShape3D { Name = "Detail", Shape = exact });
        b.Open = true;
        b.MaxHealth = b.Health = b.MaxHealth * hpMul;
        c.Root.AddChild(b);
        c.Res.Buildings.Add(b);
        Take(c, new Rect2(pos.X - size.X / 2, pos.Z - size.Z / 2, size.X, size.Z), 1f);
        return b;
    }

    /// <summary>
    /// Marque au sol (voir <see cref="RuinKit.Decal"/>). Les marques sombres sont multiplicatives et ignorent le
    /// brouillard : par tempête de sable, seules la poussière claire et l'acide sont posés.
    /// </summary>
    private static void Decal(Ctx c, Mats.Decal d, Vector3 p, float size, float yaw, float y = 0.03f, float stretch = 1f)
    {
        if (c.Storm && d is not (Mats.Decal.Dust or Mats.Decal.Acid)) return;
        c.Ruins.Decal(d, p, size, yaw, y, stretch);
    }

    /// <summary>Quad au sol (sans collision) dans le secteur de son centre.</summary>
    private static void GroundQuad(Ctx c, Material m, Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color col)
    {
        var k = c.Ground.At((a + cc) / 2);
        k.Col = col;
        k.OrientedQuad(m, a, b, cc, d, Vector3.Up);
    }

    /// <summary>Rectangle au sol (x0, z0) → (x1, z1) à la hauteur <paramref name="y"/>, découpé en dalles de 48 m.</summary>
    private static void GroundRect(Ctx c, Material m, float x0, float z0, float x1, float z1, float y, Color col)
    {
        for (var x = x0; x < x1 - 0.01f; x += 48f)
        for (var z = z0; z < z1 - 0.01f; z += 48f)
        {
            var xa = Mathf.Min(x + 48f, x1);
            var za = Mathf.Min(z + 48f, z1);
            GroundQuad(c, m, new Vector3(x, y, z), new Vector3(xa, y, z), new Vector3(xa, y, za), new Vector3(x, y, za), col);
        }
    }

    // ───────────────────────── Sol ─────────────────────────

    private static void BuildGround(Ctx c)
    {
        // Sable de l'aire de jeu et du premier rang de décor (collision), prolongé par les dunes lointaines (Skyline).
        CityBuilder.AddSlab(c.Root, c.Body, new Vector3(0, -0.5f, 0), new Vector3(524, 1f, 524), Mats.DesertSand());
    }

    /// <summary>Route (enrobé sablé, sans collision) avec rives usées et langues de sable.</summary>
    private static void Road(Ctx c, Vector3 a, Vector3 b, float half = RoadHalf)
    {
        var dir = (b - a).Normalized();
        var side = new Vector3(-dir.Z, 0, dir.X) * half;
        var len = (b - a).Length();
        const float y = 0.012f;
        var asphalt = Mats.DesertAsphalt();
        for (var s = 0f; s < len; s += 48f)
        {
            var s1 = Mathf.Min(len, s + 48f);
            var p0 = a + dir * s;
            var p1 = a + dir * s1;
            var k = c.Ground.At((p0 + p1) / 2);
            k.Col = new Color(0.86f, 0.84f, 0.8f);
            k.OrientedQuad(asphalt, p0 - side + Vector3.Up * y, p1 - side + Vector3.Up * y, p1 + side + Vector3.Up * y, p0 + side + Vector3.Up * y, Vector3.Up);
            // Langues de sable poussées par le vent en travers de la chaussée.
            for (var d = s + 4f; d < s1 - 4f; d += 9f)
            {
                var h = RuinKit.Hash(p0.X + d, p0.Z - d, 81);
                if (h < 0.55f) continue;
                var q = a + dir * d + side.Normalized() * (RuinKit.Hash(d, p0.X, 82) - 0.5f) * half * 1.4f;
                Decal(c, Mats.Decal.Dust, q, 5f + h * 7f, Mathf.Atan2(dir.X, dir.Z) + 0.8f, 0.03f + (d % 7) * 0.0004f, 1.8f);
            }
        }
    }

    private static void BuildRoads(Ctx c)
    {
        // Route du désert : de l'horizon sud jusqu'au tarmac, par la porte.
        Road(c, new Vector3(0, 0, 900), new Vector3(0, 0, 46));
        // Rocade intérieure (casernements → dépôt), bretelles vers le tarmac.
        Road(c, new Vector3(-200, 0, 88), new Vector3(152, 0, 88));
        Road(c, new Vector3(-105, 0, 88), new Vector3(-105, 0, ApronZ1));
        Road(c, new Vector3(145, 0, 82), new Vector3(145, 0, ApronZ1));
        // Aire de départ : dalle de béton (place d'armes).
        GroundRect(c, Mats.DesertApron(), -34, -40, 34, 46, 0.011f, new Color(0.93f, 0.9f, 0.85f));
    }
}
