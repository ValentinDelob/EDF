using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Carte « Zone industrielle » (spécification §9.1) : raffinerie et parc de cuves au nord-est, ateliers et cheminées
/// au nord-ouest, entrepôts et parc à conteneurs à l'ouest, gare de triage à l'est, bassins de décantation et quai
/// de vrac au sud, canal derrière. L'aire de jeu garde les limites communes (champ de flux, murs à ±230 m et z = 166) ;
/// au-delà, décor sans collision (autres usines, tours de refroidissement, pylônes, panaches).
///
/// Les citernes (cuves de stockage, sphères de gaz, réservoirs, cuves de procédé, wagons-citernes) sont des accessoires
/// destructibles explosifs (<see cref="PropKind.Tank"/>, voir <c>Destructibles.Tanks.cs</c>) : réactions en chaîne.
/// Ateliers, entrepôts, cheminées et piles de conteneurs sont des immeubles destructibles (<see cref="Building"/>).
/// Combat vertical : escaliers des cuves, racks de tuyauteries à passerelle, toit d'entrepôt, silos, portique.
///
/// Génération déterministe (graines fixes, jamais GD.Randf) : mêmes identifiants chez tous les pairs.
/// </summary>
public static partial class IndustrialBuilder
{
    private const float Half = CityBuilder.Half;
    /// <summary>Limite sud de l'aire de jeu (champ de flux, mur invisible à +8 m).</summary>
    private const float South = CityBuilder.SeaStart;
    private const float RoadHalf = 7f;

    /// <summary>Canal : rive nord, rive sud, niveau de l'eau et fond.</summary>
    public const float CanalZ0 = 128f, CanalZ1 = 146f, WaterY = -1.6f, CanalBed = -2.4f;

    /// <summary>Hauteur du dessus des rails (ballast compris).</summary>
    public const float RailTop = 0.34f;

    /// <summary>Aire de départ (joueurs, char en (14, 22), hélicoptère en (-16, 24), escouades) : rien de solide.</summary>
    private static readonly Rect2 SpawnArea = new(-36, -34, 72, 78);

    private sealed class Ctx
    {
        public Node3D Root;
        public StaticBody3D Body;
        public CityBuilder.Result Res;
        public TimeOfDay Time;
        public bool Night;
        public FlowField Flow;
        public readonly PropScatter Props = new();
        public readonly Chunks Ground = new();          // routes, marquages, voies (sans ombres ni collision)
        public readonly Chunks Solid = new(true);       // ouvrages : racks, silos, quais, ponts (collision exacte)
        public RuinKit Ruins;
        public readonly List<Rect2> Taken = new();      // emprises réservées (pas de mobilier ni de clôture)
        public readonly List<Vector3> Tanks = new();    // pieds des citernes (feux, flaques)
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
        var c = new Ctx { Root = root, Time = time, Night = time == TimeOfDay.Night, Res = new CityBuilder.Result(), Flow = World.Current?.Flow };
        c.Body = new StaticBody3D { Name = "Ground", CollisionLayer = 1, CollisionMask = 0 };
        root.AddChild(c.Body);
        c.Ruins = new RuinKit(root, c.Body, 0x1D57, c.Night);
        c.Ruins.Block(SpawnArea);
        c.Taken.Add(SpawnArea);

        BuildGround(c);
        BuildRoads(c);
        BuildCanal(c);
        BuildPlants(c);        // ateliers, cheminées, silos (immeubles d'abord : identifiants stables)
        BuildWarehouses(c);
        BuildContainers(c);
        BuildRailYard(c);
        BuildRefinery(c);
        BuildTankFarm(c);
        BuildPonds(c);
        BuildWharf(c);
        BuildYards(c);
        BuildFences(c);
        DressIndustrial(c);
        BuildSkyline(c);

        c.Ground.Flush(root, "IndGround", false);
        c.Solid.Flush(root, "IndSolid", true, c.Body);
        c.Props.Flush(root);
        c.Ruins.Flush();

        // Murs invisibles en bordure (mêmes limites que les autres cartes).
        CityBuilder.AddWall(c.Body, new Vector3(0, 20, -Half - 10), new Vector3(Half * 2 + 40, 60, 2));
        CityBuilder.AddWall(c.Body, new Vector3(-Half - 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(Half + 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(0, 20, South + 8), new Vector3(Half * 2 + 40, 60, 2));

        var res = c.Res;
        res.Zones["north"] = new Vector3(0, 0, -192);    // route nord, entre ateliers et parc de cuves
        res.Zones["center"] = new Vector3(60, 0, -100);  // route de la raffinerie, au pied des cuves
        res.Zones["east"] = new Vector3(186, 0, -36);    // gare de triage, cour entre les faisceaux
        res.Zones["west"] = new Vector3(-170, 0, -22);   // cour entre les entrepôts
        res.Zones["south"] = new Vector3(4, 0, 98);      // cour de la station d'épuration
        res.Zones["beach"] = new Vector3(-110, 0, 154);  // rive sud du canal (passage par les ponts)
        foreach (var n in NestSpots) res.NestSpots.Add(n);
        res.PlayerSpawns.Add(new Vector3(-4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(-4, 1, 12));
        res.PlayerSpawns.Add(new Vector3(4, 1, 12));

        CityCam.Attach(root);
        AttachProbes(c);
        return res;
    }

    /// <summary>Emplacements de nids : cours dégagées (cour des entrepôts, gare, station d'épuration, route nord).</summary>
    private static readonly Vector3[] NestSpots = { new(-170, 0, -22), new(186, 0, -36), new(-16, 0, 96), new(0, 0, -214) };

    // ───────────────────────── Outils ─────────────────────────

    /// <summary>Réserve une emprise (mobilier, clôtures et débris l'évitent).</summary>
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

    /// <summary>
    /// Ouvrage plein (cuve, silo, colonne) bloqué dans le champ de flux avec sa hauteur : les insectes non grimpeurs le
    /// contournent, les grimpeurs l'escaladent si c'est plus court, aucun insecte n'apparaît dedans.
    /// </summary>
    private static void FlowBlock(Ctx c, Vector3 center, Vector3 size, float margin = 0.5f) =>
        c.Flow?.SetBlockedRect(center, size, true, margin);

    /// <summary>
    /// Bâtiment destructible au maillage sur mesure : un immeuble « industriel » ordinaire (boîte de collision pleine,
    /// paliers de dégâts, effondrement) dont le visuel est remplacé par <paramref name="mesh"/> ; ce qui dépasse de la
    /// boîte (sheds, lanterneaux, auvents, escaliers) reçoit une forme exacte.
    /// </summary>
    private static Building AddPlant(Ctx c, Vector3 pos, Vector3 size, ArrayMesh mesh, Material facade, float hpMul = 1f, Shape3D mainShape = null)
    {
        var b = Building.Create(c.NextId++, pos, size, facade, Mats.Roof(), new BuildingStyle { Kind = BuildingKind.Industrial });
        b.Visual.Mesh = mesh;
        if (mainShape != null)
            foreach (var ch in b.GetChildren())
                if (ch is CollisionShape3D cs && cs.Shape is BoxShape3D) cs.Shape = mainShape;
        if (MeshKit.ShapeOf(mesh) is { } detail) b.AddChild(new CollisionShape3D { Name = "Detail", Shape = detail });
        b.MaxHealth = b.Health = b.MaxHealth * hpMul;
        c.Root.AddChild(b);
        c.Res.Buildings.Add(b);
        Take(c, new Rect2(pos.X - size.X / 2, pos.Z - size.Z / 2, size.X, size.Z), 1f);
        return b;
    }

    // ───────────────────────── Sol et routes ─────────────────────────

    private static void BuildGround(Ctx c)
    {
        var apron = Mats.Apron();
        // Dalle de béton de l'aire de jeu et du premier rang de décor, coupée par le canal.
        CityBuilder.AddSlab(c.Root, c.Body, new Vector3(0, -0.5f, (-262 + CanalZ0) / 2), new Vector3(524, 1f, CanalZ0 + 262), apron);
        CityBuilder.AddSlab(c.Root, c.Body, new Vector3(0, -0.5f, (CanalZ1 + 262) / 2), new Vector3(524, 1f, 262 - CanalZ1), apron);
    }

    /// <summary>Route (enrobé posé sur la dalle, sans collision) avec bandes de rive usées.</summary>
    private static void Road(Ctx c, Vector3 a, Vector3 b, float half = RoadHalf)
    {
        var dir = (b - a).Normalized();
        var side = new Vector3(-dir.Z, 0, dir.X) * half;
        var len = (b - a).Length();
        const float y = 0.012f;
        for (var s = 0f; s < len; s += 48f)
        {
            var s1 = Mathf.Min(len, s + 48f);
            var p0 = a + dir * s;
            var p1 = a + dir * s1;
            var k = c.Ground.At((p0 + p1) / 2);
            k.Col = Colors.White;
            k.OrientedQuad(Mats.Asphalt(), p0 - side + Vector3.Up * y, p1 - side + Vector3.Up * y, p1 + side + Vector3.Up * y, p0 + side + Vector3.Up * y, Vector3.Up);
            // Bandes de rive discontinues, très effacées.
            var paint = Mats.RoadPaint();
            k.Col = new Color(0.55f, 0.54f, 0.5f);
            foreach (var sg in new[] { -1f, 1f })
            {
                var o = side.Normalized() * (half - 0.6f) * sg;
                for (var d = s; d < s1 - 1f; d += 6f)
                {
                    if (RuinKit.Hash(p0.X + d, p0.Z + sg, 3) < 0.35f) continue;
                    var q0 = a + dir * d + o + Vector3.Up * (y + 0.004f);
                    var q1 = a + dir * Mathf.Min(d + 3f, s1) + o + Vector3.Up * (y + 0.004f);
                    var w = side.Normalized() * 0.07f;
                    k.OrientedQuad(paint, q0 - w, q1 - w, q1 + w, q0 + w, Vector3.Up);
                }
            }
        }
    }

    private static void BuildRoads(Ctx c)
    {
        Road(c, new Vector3(0, 0, -262), new Vector3(0, 0, CanalZ0));
        Road(c, new Vector3(0, 0, CanalZ1), new Vector3(0, 0, 262));
        Road(c, new Vector3(-262, 0, -100), new Vector3(262, 0, -100));
        Road(c, new Vector3(-262, 0, 62), new Vector3(262, 0, 62));
        Road(c, new Vector3(-118, 0, -262), new Vector3(-118, 0, 62));
        Road(c, new Vector3(118, 0, -262), new Vector3(118, 0, -100));
        Road(c, new Vector3(150, 0, 62), new Vector3(150, 0, CanalZ0), 5f);
        Road(c, new Vector3(150, 0, CanalZ1), new Vector3(150, 0, 175), 5f);
        // Aire de départ : enrobé (parking du poste de garde).
        var k = c.Ground.At(Vector3.Zero);
        k.Col = Colors.White;
        k.OrientedQuad(Mats.Asphalt(), new Vector3(-30, 0.011f, -28), new Vector3(30, 0.011f, -28), new Vector3(30, 0.011f, 40), new Vector3(-30, 0.011f, 40), Vector3.Up);
    }
}
