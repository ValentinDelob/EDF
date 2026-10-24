using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Carte « Quartier d'affaires » (spécification §9.1 / §9.4) : grille dense de tours de bureaux (6 à 50 étages),
/// grand parvis central, viaduc autoroutier au sud, parkings à étages, entrée de métro, passerelles entre tours.
/// L'aire de jeu reprend les limites des autres cartes (champ de flux) ; le quartier se prolonge au-delà des
/// murs invisibles par une ligne d'horizon de tours (décor sans collision), pour environ 1,2 km² visibles.
///
/// Combat vertical : toits plats accessibles (escaliers de secours, rampes des parkings, bretelle du viaduc,
/// vol du Commando, hélisurfaces), insectes grimpeurs sur toutes les façades. Les tours de 20 étages et plus
/// ne s'effondrent pas (panneaux de façade arrachés) ; les immeubles plus bas s'effondrent comme en ville.
///
/// Génération déterministe (graines fixes, jamais GD.Randf) : mêmes identifiants d'immeubles et d'accessoires
/// chez tous les pairs.
/// </summary>
public static partial class BusinessBuilder
{
    private const float Half = CityBuilder.Half;
    /// <summary>Limite sud de l'aire de jeu (champ de flux, mur invisible à +8 m).</summary>
    private const float South = CityBuilder.SeaStart;
    private const float RoadHalf = 8f;
    private const float Walk = 0.15f;
    private const float PaintY = 0.012f;

    /// <summary>Axes des rues nord-sud et est-ouest (l'avenue x = 0 et la traverse z = 0 s'arrêtent au parvis).</summary>
    private static readonly float[] Xs = { -190, -130, -70, 70, 130, 190 };
    private static readonly float[] Zs = { -190, -130, -70, 60, 128 };

    /// <summary>Parvis : entre les rues x = ±70, z = -70 et z = 60 (bords de trottoir).</summary>
    private static readonly Rect2 Plaza = new(-62, -62, 124, 114);

    /// <summary>Viaduc autoroutier : axe au-dessus de la rue z = 128, dessus du tablier.</summary>
    public const float HighwayZ = 128f;
    public const float DeckTop = 9.4f;
    private const float DeckHalf = 8.4f;

    /// <summary>Trémie de l'entrée de métro sur le parvis (escalier vers le sud).</summary>
    private static readonly Rect2 MetroPit = new(42.2f, -13f, 3.6f, 14f);
    private const float MetroDepth = 4.6f;

    private sealed class Ctx
    {
        public Node3D Root;
        public StaticBody3D Body;
        public CityBuilder.Result Res;
        public TimeOfDay Time;
        public bool Night;
        public readonly PropScatter Props = new();
        public readonly Chunks Ground = new();          // bordures, marquages, avaloirs (sans ombres)
        public readonly Chunks Solid = new(true);       // bacs, fontaine, viaduc, passerelles (collision exacte)
        public RuinKit Ruins;
        public readonly List<Rect2> Blocks = new();     // îlots (trottoir à 0,15 m)
        public readonly List<Rect2> Footprints = new(); // emprises d'immeubles (hors gravats)
        public readonly List<Rect2> KeepOut = new();    // emprises élargies (escaliers de secours) : pas de mobilier
        public readonly List<Building> Towers = new();  // tours debout (base de chaque tour)
        public readonly List<Vector3> Vacant = new();   // terrains dévastés (nids)
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
        var c = new Ctx { Root = root, Time = time, Night = time == TimeOfDay.Night, Res = new CityBuilder.Result() };
        c.Body = new StaticBody3D { Name = "Ground", CollisionLayer = 1, CollisionMask = 0 };
        root.AddChild(c.Body);
        c.Ruins = new RuinKit(root, c.Body, 0xB0B5, c.Night);
        // Aire de départ (joueurs, char, hélicoptère, escouades) : rien de solide.
        c.Ruins.Block(new Rect2(-34, -32, 68, 74));

        BuildGround(c);
        BuildBlocks(c);
        BuildPlaza(c);
        BuildHighway(c);
        BuildSkybridges(c);
        BuildRoadMarkings(c);
        BuildStreetFurniture(c);
        DressBusiness(c);
        BuildSkyline(c);

        c.Ground.Flush(root, "BizGround", false);
        c.Solid.Flush(root, "BizSolid", true, c.Body);
        c.Props.Flush(root);
        c.Ruins.Flush();

        // Murs invisibles en bordure (mêmes limites que les autres cartes).
        CityBuilder.AddWall(c.Body, new Vector3(0, 20, -Half - 10), new Vector3(Half * 2 + 40, 60, 2));
        CityBuilder.AddWall(c.Body, new Vector3(-Half - 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(Half + 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(0, 20, South + 8), new Vector3(Half * 2 + 40, 60, 2));

        var res = c.Res;
        res.Zones["north"] = new Vector3(0, 0, -158);    // avenue nord
        res.Zones["center"] = new Vector3(0, 0, -100);   // avenue nord, devant le parvis
        res.Zones["east"] = new Vector3(130, 0, 0);      // carrefour est
        res.Zones["west"] = new Vector3(-130, 0, 0);     // carrefour ouest
        res.Zones["south"] = new Vector3(-70, 0, HighwayZ);  // sous le viaduc
        res.Zones["beach"] = new Vector3(70, 0, HighwayZ);   // sous le viaduc (est)
        foreach (var v in c.Vacant) res.NestSpots.Add(v);
        res.PlayerSpawns.Add(new Vector3(-4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(-4, 1, 12));
        res.PlayerSpawns.Add(new Vector3(4, 1, 12));

        CityCam.Attach(root);
        AttachProbes(c);
        return res;
    }

    // ───────────────────────── Sol ─────────────────────────

    /// <summary>Hauteur du sol : trottoirs des îlots et parvis à 0,15 m, chaussée à 0.</summary>
    private static float GroundY(Ctx c, float x, float z)
    {
        if (Plaza.HasPoint(new Vector2(x, z))) return MetroPit.HasPoint(new Vector2(x, z)) ? -MetroDepth : Walk;
        foreach (var b in c.Blocks)
            if (b.HasPoint(new Vector2(x, z))) return Walk;
        return 0f;
    }

    private static Vector3 OnGround(Ctx c, float x, float z) => new(x, GroundY(c, x, z), z);

    /// <summary>Boîtes (visuel + collision) couvrant un rectangle percé d'un trou (jusqu'à quatre morceaux).</summary>
    private static void SlabAround(Ctx c, Rect2 r, Rect2 hole, float y0, float y1, Material m, bool collide = true)
    {
        void Piece(float x0, float z0, float x1, float z1)
        {
            if (x1 - x0 < 0.01f || z1 - z0 < 0.01f) return;
            CityBuilder.AddSlab(c.Root, c.Body, new Vector3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), new Vector3(x1 - x0, y1 - y0, z1 - z0), m, collide);
        }
        if (!r.Intersects(hole))
        {
            Piece(r.Position.X, r.Position.Y, r.End.X, r.End.Y);
            return;
        }
        Piece(r.Position.X, r.Position.Y, r.End.X, hole.Position.Y);
        Piece(r.Position.X, hole.End.Y, r.End.X, r.End.Y);
        Piece(r.Position.X, hole.Position.Y, hole.Position.X, hole.End.Y);
        Piece(hole.End.X, hole.Position.Y, r.End.X, hole.End.Y);
    }

    private static void BuildGround(Ctx c)
    {
        // Chaussée (aire de jeu et premier rang de décor au-delà des murs), percée par la trémie du métro.
        SlabAround(c, new Rect2(-262, -262, 524, 262 + 200), MetroPit, -1f, 0f, Mats.Asphalt());
        // Fond de la trémie (palier bas de l'escalier).
        CityBuilder.AddSlab(c.Root, c.Body, new Vector3(MetroPit.GetCenter().X, -MetroDepth - 0.5f, MetroPit.GetCenter().Y), new Vector3(MetroPit.Size.X + 0.4f, 1f, MetroPit.Size.Y + 0.4f), Mats.Sidewalk());
    }

    /// <summary>Trottoir d'un îlot : dalle à 0,15 m, bordure de granit.</summary>
    private static void Sidewalk(Ctx c, Rect2 r)
    {
        c.Blocks.Add(r);
        CityBuilder.AddSlab(c.Root, c.Body, new Vector3(r.GetCenter().X, 0, r.GetCenter().Y), new Vector3(r.Size.X, 0.3f, r.Size.Y), Mats.Sidewalk());
        var k = c.Ground.At(new Vector3(r.GetCenter().X, 0, r.GetCenter().Y));
        k.Col = Colors.White;
        k.Xf = new Transform3D(Basis.Identity, new Vector3(r.GetCenter().X, 0, r.GetCenter().Y));
        k.Ring(Mats.Curb(), r.Size.X / 2, r.Size.Y / 2, -0.1f, 0.155f, 0.28f, 0.02f);
        k.Xf = Transform3D.Identity;
    }

    // ───────────────────────── Îlots et immeubles ─────────────────────────

    /// <summary>Intervalles (bords de trottoir) entre axes successifs, avec des îlots de bordure au-delà des murs.</summary>
    private static List<(float a0, float a1)> Bands(float[] axes, float min, float max)
    {
        var list = new List<(float, float)>();
        var prev = min;
        foreach (var a in axes)
        {
            list.Add((prev, a - RoadHalf));
            prev = a + RoadHalf;
        }
        list.Add((prev, max));
        return list;
    }

    private static void BuildBlocks(Ctx c)
    {
        var rng = new RandomNumberGenerator { Seed = 2077 };
        // Colonnes : l'avenue x = 0 coupe les îlots du nord et du sud ; la traverse z = 0 ceux de l'est et de l'ouest.
        var xBands = Bands(Xs, -236f, 236f);
        var zBands = Bands(Zs, -236f, 166f);
        foreach (var (z0, z1) in zBands)
        foreach (var (x0, x1) in xBands)
        {
            if (z0 > HighwayZ) continue; // bande sud : bretelle et talus (voir viaduc)
            var central = x0 < 0 && x1 > 0;          // colonne du parvis (-62 .. 62)
            var plazaRow = z0 < 0 && z1 > 0;         // rangée du parvis (-62 .. 52)
            if (central && plazaRow) continue;        // parvis
            var rects = new List<Rect2>();
            if (central)
            {
                rects.Add(new Rect2(x0, z0, -RoadHalf - x0, z1 - z0));
                rects.Add(new Rect2(RoadHalf, z0, x1 - RoadHalf, z1 - z0));
            }
            else if (plazaRow)
            {
                rects.Add(new Rect2(x0, z0, x1 - x0, -RoadHalf - z0));
                rects.Add(new Rect2(x0, RoadHalf, x1 - x0, z1 - RoadHalf));
            }
            else rects.Add(new Rect2(x0, z0, x1 - x0, z1 - z0));
            foreach (var r in rects)
            {
                Sidewalk(c, r);
                FillBlock(c, r, rng);
            }
        }
    }

    /// <summary>Îlots particuliers : parkings à étages et terrains dévastés (nids), repérés par leur centre.</summary>
    private static readonly Vector2[] ParkingLots = { new(-100, 94), new(100, -35) };
    private static readonly Vector2[] VacantLots = { new(-100, -100), new(100, -160), new(-160, -35), new(160, 94) };

    private static bool Near(Vector2[] list, Vector2 p)
    {
        foreach (var q in list) if (q.DistanceTo(p) < 12f) return true;
        return false;
    }

    private static void FillBlock(Ctx c, Rect2 r, RandomNumberGenerator rng)
    {
        var center = r.GetCenter();
        var dist = center.Length();
        var w = r.Size.X;
        var d = r.Size.Y;
        if (Near(ParkingLots, center))
        {
            var size = new Vector3(Mathf.Min(40f, w - 4f), 0, Mathf.Min(32f, d - 6f));
            var levels = 4;
            size.Y = levels * Building.ParkingLevel + 1.15f;
            var style = new BuildingStyle { Kind = BuildingKind.Parking, Levels = levels, Clearance = 2f };
            AddBuilding(c, new Vector3(center.X, Walk, center.Y), size, Mats.Detail(), style);
            return;
        }
        if (Near(VacantLots, center))
        {
            c.Vacant.Add(new Vector3(center.X, Walk, center.Y));
            return; // gravats et incendies posés par l'habillage
        }

        var edge = Mathf.Abs(center.X) > 200f || center.Y < -200f; // îlots de bordure (à cheval sur les murs)
        var towerChance = edge ? 0.25f : dist < 150f ? 0.6f : dist < 230f ? 0.42f : 0.3f;
        if (w >= 38f && d >= 38f && rng.Randf() < towerChance)
        {
            TowerBlock(c, r, rng, dist);
            return;
        }
        // Immeubles de bureaux moyens : un, deux ou trois volumes séparés par des passages de service.
        var alongX = w >= d;
        var len = alongX ? w : d;
        var gap = 7f;
        // Lots d'au moins 17 m (immeuble de 9 m minimum après les retraits).
        var maxSplit = Mathf.Clamp(Mathf.FloorToInt((len + gap) / (17f + gap)), 1, 3);
        var split = Mathf.Clamp(rng.RandiRange(1, 3), 1, maxSplit);
        var part = (len - gap * (split - 1)) / split;
        for (var i = 0; i < split; i++)
        {
            var a0 = i * (part + gap);
            var lot = alongX ? new Rect2(r.Position.X + a0, r.Position.Y, part, d) : new Rect2(r.Position.X, r.Position.Y + a0, w, part);
            MidRise(c, lot, r, rng, dist, edge);
        }
    }

    /// <summary>Immeuble de bureaux de 6 à 19 étages (destructible), dans un lot avec retrait sur le trottoir.</summary>
    private static void MidRise(Ctx c, Rect2 lot, Rect2 block, RandomNumberGenerator rng, float dist, bool edge)
    {
        var setback = rng.RandfRange(2.2f, 4.4f);
        var w = lot.Size.X - setback * 2;
        var d = lot.Size.Y - setback * 2;
        if (w < 9f || d < 9f) return;
        var maxF = edge ? 12 : dist < 160f ? 19 : 15;
        var floors = rng.Randf() < 0.22f ? rng.RandiRange(3, 5) : rng.RandiRange(6, maxF);
        var size = new Vector3(w, floors * 3.2f + 0.6f, d);
        var center = lot.GetCenter();
        var pick = rng.Randf();
        var style = new BuildingStyle { Kind = BuildingKind.Office, Clearance = setback };
        if (floors >= 8 && pick < 0.45f)
        {
            style.Kind = BuildingKind.Tower;
            style.Variant = rng.Randf() < 0.55f ? 6 : 7;
            style.Shops = true;
        }
        else if (pick < 0.55f)
        {
            style.Kind = BuildingKind.Residential;
            style.Variant = rng.Randf() < 0.5f ? 9 : 3;
            style.Shops = rng.Randf() < 0.6f;
        }
        else
        {
            style.Variant = rng.RandiRange(0, 5);
            style.Shops = rng.Randf() < 0.55f;
        }
        // Escalier de secours vers le toit sur la façade côté rue, pour les bâtiments bas proches du parvis.
        var fe = rng.Randf();
        if (floors <= 10 && setback >= 3.5f && fe < (dist < 170f ? 0.6f : 0.25f) && style.Kind != BuildingKind.Residential)
        {
            style.FireEscape = true;
            style.Shops = false;
            style.Facing = FacingAway(center, block.GetCenter(), w, d);
        }
        AddBuilding(c, new Vector3(center.X, Walk, center.Y), size, null, style);
    }

    /// <summary>Façade tournée vers l'extérieur de l'îlot (côté rue) : 0 = +Z, 1 = -Z, 2 = +X, 3 = -X.</summary>
    private static int FacingAway(Vector2 p, Vector2 blockCenter, float w, float d)
    {
        var dx = p.X - blockCenter.X;
        var dz = p.Y - blockCenter.Y;
        if (Mathf.Abs(dx) < 1f && Mathf.Abs(dz) < 1f) return w >= d ? 0 : 2;
        if (Mathf.Abs(dx) > Mathf.Abs(dz)) return dx > 0 ? 2 : 3;
        return dz > 0 ? 0 : 1;
    }

    /// <summary>Côté tourné vers un point (même codage que <see cref="FacingAway"/>).</summary>
    private static int FacingToward(Vector2 p, Vector2 target)
    {
        var dx = target.X - p.X;
        var dz = target.Y - p.Y;
        if (Mathf.Abs(dx) > Mathf.Abs(dz)) return dx > 0 ? 2 : 3;
        return dz > 0 ? 0 : 1;
    }

    /// <summary>
    /// Îlot de tour : une tour debout (20 à 50 étages, parfois en deux volumes avec retrait), décalée vers un angle,
    /// avec un parvis privé et parfois une annexe basse (destructible) sur la bande restante.
    /// </summary>
    private static void TowerBlock(Ctx c, Rect2 r, RandomNumberGenerator rng, float dist)
    {
        var w = r.Size.X;
        var d = r.Size.Y;
        var tw = Mathf.Min(rng.RandfRange(24f, 34f), w - 7f);
        var td = Mathf.Min(tw + rng.RandfRange(-5f, 4f), d - 7f);
        var floors = dist < 120f ? rng.RandiRange(30, 50) : dist < 220f ? rng.RandiRange(24, 42) : rng.RandiRange(20, 30);
        // Décalage vers un angle de l'îlot : il reste une bande pour une annexe ou un jardin minéral.
        var sx = rng.Randf() < 0.5f ? -1f : 1f;
        var sz = rng.Randf() < 0.5f ? -1f : 1f;
        var setX = rng.RandfRange(3.2f, 5f);
        var setZ = rng.RandfRange(3.2f, 5f);
        var cx = r.GetCenter().X + sx * (w / 2 - setX - tw / 2);
        var cz = r.GetCenter().Y + sz * (d / 2 - setZ - td / 2);
        var tc = new Vector2(cx, cz);
        AddTower(c, tc, tw, td, floors, Mathf.Min(setX, setZ), rng, FacingToward(tc, Vector2.Zero), rng.Randf() < 0.18f);

        // Annexe sur la bande libre la plus large.
        var freeX = w - tw - setX * 2;
        var freeZ = d - td - setZ * 2;
        if (Mathf.Max(freeX, freeZ) >= 13f && rng.Randf() < 0.75f)
        {
            Rect2 lot;
            if (freeX >= freeZ)
            {
                var x0 = sx > 0 ? r.Position.X : cx + tw / 2 + 2f;
                var x1 = sx > 0 ? cx - tw / 2 - 2f : r.End.X;
                lot = new Rect2(x0, r.Position.Y, x1 - x0, d);
            }
            else
            {
                var z0 = sz > 0 ? r.Position.Y : cz + td / 2 + 2f;
                var z1 = sz > 0 ? cz - td / 2 - 2f : r.End.Y;
                lot = new Rect2(r.Position.X, z0, w, z1 - z0);
            }
            var setback = 2.6f;
            var aw = lot.Size.X - setback * 2;
            var ad = lot.Size.Y - setback * 2;
            if (aw >= 8f && ad >= 8f)
            {
                var af = rng.RandiRange(3, 7);
                var style = new BuildingStyle { Kind = BuildingKind.Office, Variant = rng.RandiRange(0, 5), Shops = rng.Randf() < 0.6f, Clearance = setback };
                AddBuilding(c, new Vector3(lot.GetCenter().X, Walk, lot.GetCenter().Y), new Vector3(aw, af * 3.2f + 0.6f, ad), null, style);
            }
        }
    }

    /// <summary>Tour debout : volume de base (et volume en retrait éventuel), toutes deux indestructibles.</summary>
    private static Building AddTower(Ctx c, Vector2 p, float tw, float td, int floors, float clearance, RandomNumberGenerator rng, int facing, bool helipad = false)
    {
        var pick = rng.Randf();
        var variant = pick < 0.45f ? 6 : pick < 0.8f ? 7 : pick < 0.9f ? 2 : 5;
        var curtain = variant is 6 or 7;
        var tiered = floors >= 30 && rng.Randf() < 0.45f;
        var f1 = tiered ? Mathf.RoundToInt(floors * rng.RandfRange(0.55f, 0.72f)) : floors;
        var inset = rng.RandfRange(3.2f, 5f);
        var upper = new Vector2(tw - inset * 2, td - inset * 2);
        var core = rng.Randf() < 0.5f ? rng.RandiRange(1, 4) : 0;
        var style = new BuildingStyle
        {
            Kind = BuildingKind.Skyscraper, Variant = variant, Clearance = clearance, Lobby = true, Facing = facing,
            Fins = curtain && rng.Randf() < 0.5f, Core = tiered || helipad ? 0 : core,
            Above = tiered ? upper : Vector2.Zero, Helipad = helipad && !tiered,
        };
        var baseH = f1 * 3.2f + 0.6f;
        var b = AddBuilding(c, new Vector3(p.X, Walk, p.Y), new Vector3(tw, baseH, td), null, style);
        b.Standing = true;
        c.Towers.Add(b);
        if (tiered)
        {
            var top = style;
            top.Lobby = false;
            top.Above = Vector2.Zero;
            top.Core = helipad ? 0 : core;
            top.Helipad = helipad;
            var t = AddBuilding(c, new Vector3(p.X, Walk + baseH, p.Y), new Vector3(upper.X, (floors - f1) * 3.2f, upper.Y), null, top);
            t.Standing = true;
        }
        return b;
    }

    /// <summary>Crée l'immeuble (identifiant suivant), réserve son emprise et l'ajoute à la carte.</summary>
    private static Building AddBuilding(Ctx c, Vector3 pos, Vector3 size, Material facade, BuildingStyle style)
    {
        var id = c.NextId++;
        // Réseau électrique coupé : la nuit, seule une minorité d'immeubles a encore de la lumière.
        style.Night = c.Night && RuinKit.Hash(id, 7, 1) < 0.2f;
        facade ??= Mats.Facade(style.Variant, style.Night);
        var b = Building.Create(id, pos, size, facade, Mats.Roof(), style);
        c.Root.AddChild(b);
        c.Res.Buildings.Add(b);
        var rect = new Rect2(pos.X - size.X / 2, pos.Z - size.Z / 2, size.X, size.Z);
        if (pos.Y < 1f)
        {
            c.Footprints.Add(rect);
            c.Ruins.Block(rect.Grow(style.FireEscape ? 3f : 0.4f));
            c.KeepOut.Add(rect.Grow(style.FireEscape ? 3f : 0.3f));
        }
        return b;
    }
}
