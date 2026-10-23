using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Génère la carte « Ville côtière » de façon déterministe (même graine chez tous les pairs,
/// donc mêmes identifiants d'immeubles).
/// Le générateur principal (graine 1337) ne sert qu'aux éléments historiques (lots, immeubles,
/// voitures, arbres, palmiers) dans un ordre de tirage inchangé ; tout le décor ajouté utilise
/// des générateurs séparés à graine fixe pour ne jamais décaler les identifiants réseau.
/// </summary>
public static class CityBuilder
{
    public const float Half = 220f;
    public const float BeachStart = 112f;
    public const float SeaStart = 158f;
    public const float RoadEvery = 50f;
    public const float RoadWidth = 12f;

    /// <summary>Boulevard du front de mer, entre la dernière rangée de pâtés et la promenade.</summary>
    private const float BlvdNorth = 74f;
    private const float BlvdSouth = 92f;
    private const float PaintY = 0.012f;
    private const float PierX = -170f;

    public class Result
    {
        public readonly List<Building> Buildings = new();
        public readonly Dictionary<string, Vector3> Zones = new();
        public readonly List<Vector3> NestSpots = new();
        public readonly List<Vector3> PlayerSpawns = new();
    }

    /// <summary>Axes des rues nord-sud (x) et est-ouest (z) entre les pâtés.</summary>
    private static readonly float[] RoadXs = { -170, -120, -70, -20, 30, 80, 130, 180 };
    private static readonly float[] RoadZs = { -170, -120, -70, -20, 30 };

    public static Result Build(Node3D root, TimeOfDay time)
    {
        var res = new Result();
        var rng = new RandomNumberGenerator { Seed = 1337 };
        var deco = new RandomNumberGenerator { Seed = 4242 };   // décor ajouté (indépendant des identifiants)
        var vrng = new RandomNumberGenerator { Seed = 7331 };   // types de véhicules
        var night = time == TimeOfDay.Night;
        var props = new PropScatter();
        var ground = new MeshKit();   // bordures, plaques d'égout, avaloirs (un seul maillage)

        var statics = new StaticBody3D { Name = "Ground", CollisionLayer = 1, CollisionMask = 0 };
        root.AddChild(statics);

        // Sol de la ville (asphalte), de la plage et l'extérieur.
        AddSlab(root, statics, new Vector3(0, -0.5f, (-Half + BeachStart) / 2), new Vector3(Half * 2 + 40, 1f, BeachStart + Half), Mats.Asphalt());
        var beachLen = SeaStart - BeachStart + 30;
        AddSlab(root, statics, new Vector3(0, -0.52f, BeachStart + beachLen / 2), new Vector3(Half * 2 + 40, 1f, beachLen), Mats.Sand(), collide: true);
        // Promenade du front de mer (élargie jusqu'au boulevard).
        AddSlab(root, statics, new Vector3(0, 0.0f, BeachStart - 3), new Vector3(Half * 2, 0.3f, 6), Mats.Sidewalk());
        AddSlab(root, statics, new Vector3(0, 0.0f, (BlvdSouth + BeachStart - 6) / 2), new Vector3(Half * 2, 0.3f, BeachStart - 6 - BlvdSouth), Mats.Sidewalk());
        ground.Col = Colors.White;
        ground.BoxMinMax(Mats.Curb(), new Vector3(-Half, -0.1f, BlvdSouth - 0.02f), new Vector3(Half, 0.155f, BlvdSouth + 0.26f));

        // Mer.
        var sea = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(2400, 1400), SubdivideWidth = 120, SubdivideDepth = 60, Material = Mats.Water(time) },
            Position = new Vector3(0, -0.45f, SeaStart + 690),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        root.AddChild(sea);

        // Terrain lointain (collines) pour l'horizon.
        BuildHills(root);

        // Pâtés de maisons.
        var id = 0;
        var mats = new Material[6];
        for (var i = 0; i < mats.Length; i++) mats[i] = Mats.Facade(i, night);
        var roof = Mats.Roof();
        var lamps = new List<(Vector3 pos, float yaw)>();
        var vacant = new List<Rect2>();

        for (var bx = -Half + RoadEvery / 2; bx < Half; bx += RoadEvery)
        for (var bz = -Half + RoadEvery / 2; bz < BeachStart - 10; bz += RoadEvery)
        {
            var center = new Vector3(bx, 0, bz);
            var inner = RoadEvery - RoadWidth; // 38 m
            AddSlab(root, statics, center + new Vector3(0, 0.0f, 0), new Vector3(inner, 0.3f, inner), Mats.Sidewalk());
            // Bordure de trottoir en granit, légèrement plus haute que le dallage.
            ground.Xf = new Transform3D(Basis.Identity, center);
            ground.Ring(Mats.Curb(), inner / 2, inner / 2, -0.1f, 0.155f, 0.28f, 0.02f);
            ground.Xf = Transform3D.Identity;

            var isPark = Mathf.Abs(bx) < 30 && Mathf.Abs(bz) < 30;
            if (isPark)
            {
                BuildPark(root, statics, center, inner, rng);
                DecoratePark(props, center, inner);
                continue;
            }

            var distCenter = new Vector2(bx, bz).Length();
            var coastal = bz > 40;
            var maxFloors = coastal ? 5 : Mathf.Clamp((int)(16 - distCenter / 18f), 4, 14);

            // Découpage du pâté en 1 à 4 lots.
            var split = rng.RandiRange(0, 3);
            var lots = new List<Rect2>();
            var h = inner / 2;
            switch (split)
            {
                case 0: lots.Add(new Rect2(-h, -h, inner, inner)); break;
                case 1: lots.Add(new Rect2(-h, -h, inner, h)); lots.Add(new Rect2(-h, 0, inner, h)); break;
                case 2: lots.Add(new Rect2(-h, -h, h, inner)); lots.Add(new Rect2(0, -h, h, inner)); break;
                default:
                    lots.Add(new Rect2(-h, -h, h, h)); lots.Add(new Rect2(0, -h, h, h));
                    lots.Add(new Rect2(-h, 0, h, h)); lots.Add(new Rect2(0, 0, h, h));
                    break;
            }
            foreach (var lot in lots)
            {
                if (rng.Randf() < 0.12f)
                {
                    // Terrain vague : devient un petit chantier (décor seulement).
                    vacant.Add(new Rect2(lot.Position + new Vector2(bx, bz), lot.Size));
                    continue;
                }
                var setback = rng.RandfRange(1.5f, 3.5f);
                var w = lot.Size.X - setback * 2;
                var d = lot.Size.Y - setback * 2;
                if (w < 6 || d < 6) continue;
                var floors = rng.RandiRange(2, maxFloors);
                var size = new Vector3(w, floors * 3.2f + 0.6f, d);
                var pos = center + new Vector3(lot.Position.X + lot.Size.X / 2, 0.15f, lot.Position.Y + lot.Size.Y / 2);
                var variant = rng.RandiRange(0, mats.Length - 1); // même tirage qu'auparavant : ordre des ID inchangé
                var style = PickStyle(id, variant, floors, distCenter, coastal, setback, night);
                var b = Building.Create(id++, pos, size, Mats.Facade(style.Variant, night), roof, style);
                root.AddChild(b);
                res.Buildings.Add(b);
            }

            // Véhicules garés en créneau le long du pâté (voitures, fourgons, camions).
            var parked = new List<(int side, float a0, float a1)>();
            var cars = rng.RandiRange(1, 4);
            for (var c = 0; c < cars; c++)
            {
                // Tirages du générateur principal dans l'ordre historique : côté, position, lacet, couleur, longueur.
                var side = rng.RandiRange(0, 3);
                var along = rng.RandfRange(-inner / 2 + 4, inner / 2 - 4);
                var jitter = rng.RandfRange(-0.08f, 0.08f);
                var colorIdx = rng.RandiRange(0, CarColors.Length - 1);
                var len = rng.RandfRange(4.0f, 4.8f);
                var roll = vrng.Randf();
                var type = roll < 0.1f ? Props.VehicleType.Truck : roll < 0.28f ? Props.VehicleType.Van : Props.VehicleType.Car;
                var vsize = Props.VehicleSize(type, len);
                // Pas de stationnement à moins de 5 m des passages piétons ; pas de chevauchement.
                var limit = inner / 2 - 5f - vsize.Z / 2;
                along = Mathf.Clamp(along, -limit, limit);
                if (!FindParkingSpot(parked, side, ref along, vsize.Z / 2 + 0.6f, limit)) continue;
                parked.Add((side, along - vsize.Z / 2, along + vsize.Z / 2));
                var off = inner / 2 + 0.3f + vsize.X / 2;
                Vector3 p;
                float yaw;
                switch (side)
                {
                    // Circulation à droite : le véhicule regarde dans le sens de sa voie.
                    case 0: p = new Vector3(along, 0, -off); yaw = Mathf.Pi / 2; break;
                    case 1: p = new Vector3(along, 0, off); yaw = -Mathf.Pi / 2; break;
                    case 2: p = new Vector3(-off, 0, along); yaw = Mathf.Pi; break;
                    default: p = new Vector3(off, 0, along); yaw = 0; break;
                }
                BuildVehicle(root, statics, center + p, yaw + jitter * 0.3f, type, CarColors[colorIdx], len, vrng);
            }

            // Lampadaire au coin du trottoir, crosse tournée vers le carrefour.
            lamps.Add((center + new Vector3(inner / 2 - 0.7f, 0.15f, inner / 2 - 0.7f), Mathf.Pi / 4));
            DecorateBlock(props, center, inner, deco);
        }

        foreach (var (lp, yaw) in lamps) BuildLamp(root, statics, props, lp, yaw, night);

        // Palmiers sur la promenade.
        for (var x = -Half + 10; x < Half; x += 22)
            BuildPalm(root, statics, new Vector3(x + rng.RandfRange(-3, 3), 0.15f, BeachStart - 3), rng);

        BuildRoadMarkings(root, ground, deco);
        BuildTrafficLights(props, deco);
        BuildSeafront(root, statics, props, deco, night);
        BuildBeach(root, statics, props, deco);
        BuildPier(root, statics, night);
        // Terrains vagues : chantier, parking à ciel ouvert ou petite place arborée.
        foreach (var lot in vacant)
        {
            var roll = deco.Randf();
            if (roll < 0.4f) BuildWorksite(root, statics, props, lot, deco);
            else if (roll < 0.72f) BuildParkingLot(root, statics, lot, deco);
            else BuildPlaza(root, statics, props, lot, deco);
        }

        root.AddChild(new MeshInstance3D { Name = "StreetDetail", Mesh = ground.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        props.Flush(root);

        // Murs invisibles en bordure.
        AddWall(statics, new Vector3(0, 20, -Half - 10), new Vector3(Half * 2 + 40, 60, 2));
        AddWall(statics, new Vector3(-Half - 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        AddWall(statics, new Vector3(Half + 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        AddWall(statics, new Vector3(0, 20, SeaStart + 8), new Vector3(Half * 2 + 40, 60, 2));

        res.Zones["north"] = new Vector3(0, 0, -125);
        res.Zones["south"] = new Vector3(-60, 0, BeachStart + 18);
        res.Zones["beach"] = new Vector3(40, 0, BeachStart + 28);
        res.Zones["east"] = new Vector3(125, 0, -25);
        res.Zones["west"] = new Vector3(-125, 0, -25);
        res.Zones["center"] = new Vector3(0, 0, -75);

        res.NestSpots.Add(new Vector3(-100, 0, -100));
        res.NestSpots.Add(new Vector3(100, 0, -100));
        res.NestSpots.Add(new Vector3(-100, 0, 50));
        res.NestSpots.Add(new Vector3(100, 0, 50));

        res.PlayerSpawns.Add(new Vector3(-4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(-4, 1, 12));
        res.PlayerSpawns.Add(new Vector3(4, 1, 12));

        // Caméra de contrôle visuel (outil de développement, inactif sans « --citycam »).
        CityCam.Attach(root);
        return res;
    }

    /// <summary>
    /// Typologie de l'immeuble (tour vitrée, résidentiel à balcons, bureaux), tirée d'un générateur
    /// propre à l'immeuble : n'influence ni l'empreinte ni l'ordre des identifiants.
    /// </summary>
    private static BuildingStyle PickStyle(int id, int variant, int floors, float distCenter, bool coastal, float setback, bool night)
    {
        var r = new RandomNumberGenerator { Seed = (ulong)(id * 104729 + 17) };
        var st = new BuildingStyle { Kind = BuildingKind.Office, Variant = variant, Night = night, Clearance = setback };
        if (floors >= 8 && distCenter < 150 && r.Randf() < 0.7f)
        {
            st.Kind = BuildingKind.Tower;
            st.Variant = r.Randf() < 0.55f ? 6 : 7;
            st.Shops = true; // hall vitré
        }
        else if ((coastal || floors <= 7) && r.Randf() < 0.55f)
        {
            st.Kind = BuildingKind.Residential;
            var pick = r.Randf();
            st.Variant = pick < 0.45f ? 9 : pick < 0.75f ? 8 : variant % 2 == 0 ? 3 : 1;
            st.Shops = r.Randf() < 0.6f;
        }
        else
        {
            st.Shops = r.Randf() < 0.5f;
        }
        return st;
    }

    /// <summary>Cherche une place libre sur un côté du pâté (glisse la position si besoin).</summary>
    private static bool FindParkingSpot(List<(int side, float a0, float a1)> parked, int side, ref float along, float half, float limit)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var clash = false;
            foreach (var (s, a0, a1) in parked)
            {
                if (s != side || along + half <= a0 || along - half >= a1) continue;
                clash = true;
                along = along >= (a0 + a1) / 2 ? a1 + half : a0 - half;
                break;
            }
            if (!clash) return Mathf.Abs(along) <= limit + 0.01f;
            if (Mathf.Abs(along) > limit) return false;
        }
        return false;
    }

    public static void AddSlab(Node3D root, StaticBody3D body, Vector3 center, Vector3 size, Material mat, bool collide = true)
    {
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size, Material = mat }, Position = center });
        if (collide) body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = center });
    }

    public static void AddWall(StaticBody3D body, Vector3 center, Vector3 size) =>
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = center });

    public static void BuildHills(Node3D root)
    {
        var noise = new FastNoiseLite { Seed = 7, Frequency = 0.004f, FractalOctaves = 4 };
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int seg = 96;
        const float inner = 300f, outer = 1400f;
        for (var i = 0; i < seg; i++)
        {
            var a0 = i / (float)seg * Mathf.Tau;
            var a1 = (i + 1) / (float)seg * Mathf.Tau;
            for (var r = 0; r < 6; r++)
            {
                var r0 = Mathf.Lerp(inner, outer, r / 6f);
                var r1 = Mathf.Lerp(inner, outer, (r + 1) / 6f);
                Vector3 P(float a, float rad)
                {
                    var x = Mathf.Cos(a) * rad;
                    var z = Mathf.Sin(a) * rad;
                    if (z > SeaStart) return new Vector3(x, -30, z); // côté mer : sous l'eau
                    var hgt = (noise.GetNoise2D(x, z) * 0.5f + 0.5f) * Mathf.Clamp((rad - inner) / 200f, 0, 1) * 160f;
                    return new Vector3(x, hgt - 1, z);
                }
                var q0 = P(a0, r0); var q1 = P(a1, r0); var q2 = P(a1, r1); var q3 = P(a0, r1);
                st.AddVertex(q0); st.AddVertex(q2); st.AddVertex(q1);
                st.AddVertex(q0); st.AddVertex(q3); st.AddVertex(q2);
            }
        }
        st.GenerateNormals();
        var mesh = st.Commit();
        root.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = Mats.Grass(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        // Raccord entre la ville et les collines.
        root.AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(700, 700), Material = Mats.Dirt() },
            Position = new Vector3(0, -1.02f, -120),
        });
    }

    private static void BuildPark(Node3D root, StaticBody3D body, Vector3 center, float inner, RandomNumberGenerator rng)
    {
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(inner - 4, 0.1f, inner - 4), Material = Mats.Grass() }, Position = center + new Vector3(0, 0.18f, 0) });
        for (var i = 0; i < 5; i++)
        {
            var p = center + new Vector3(rng.RandfRange(-inner / 2 + 4, inner / 2 - 4), 0.2f, rng.RandfRange(-inner / 2 + 4, inner / 2 - 4));
            if (p.Length() < 9) continue; // garder la zone de départ dégagée
            BuildTree(root, body, p, rng);
        }
    }

    /// <summary>Mobilier du square central : bancs tournés vers la pelouse, corbeilles, abribus.</summary>
    private static void DecoratePark(PropScatter props, Vector3 center, float inner)
    {
        var e = inner / 2 - 1.0f;
        foreach (var t in new[] { -10f, 10f })
        {
            props.Add(Props.Bench(), center + new Vector3(t, 0.15f, -e), 0);
            props.Add(Props.Bench(), center + new Vector3(t, 0.15f, e), Mathf.Pi);
            props.Add(Props.Bench(), center + new Vector3(-e, 0.15f, t), Mathf.Pi / 2);
            props.Add(Props.Bench(), center + new Vector3(e, 0.15f, t), -Mathf.Pi / 2);
            props.Add(Props.Bin(), center + new Vector3(t + 1.6f, 0.15f, -e - 0.2f), 0);
            props.Add(Props.Bin(), center + new Vector3(t + 1.6f, 0.15f, e + 0.2f), Mathf.Pi);
        }
        // Abribus côté nord, face à la rue.
        props.Add(Props.BusStop(false), center + new Vector3(-2f, 0.15f, -inner / 2 + 1.0f), Mathf.Pi, shadows: true, range: 220);
    }

    /// <summary>Mobilier de trottoir d'un pâté : potelets aux angles, corbeilles, borne d'incendie.</summary>
    private static void DecorateBlock(PropScatter props, Vector3 center, float inner, RandomNumberGenerator deco)
    {
        var h = inner / 2;
        // Potelets de part et d'autre de chaque angle (empêchent le stationnement sur les passages).
        foreach (var sx in new[] { -1f, 1f })
        foreach (var sz in new[] { -1f, 1f })
        {
            if (deco.Randf() < 0.35f) continue;
            for (var i = 0; i < 3; i++)
            {
                var a = 0.9f + i * 0.8f;
                props.Add(Props.Bollard(), center + new Vector3(sx * (h - 0.35f), 0.15f, sz * (h - a)), 0, range: 110);
                props.Add(Props.Bollard(), center + new Vector3(sx * (h - a), 0.15f, sz * (h - 0.35f)), 0, range: 110);
            }
        }
        // Corbeilles et borne d'incendie le long des bordures.
        var n = deco.RandiRange(1, 2);
        for (var i = 0; i <= n; i++)
        {
            var side = deco.RandiRange(0, 3);
            var a = deco.RandfRange(-h + 6, h - 6);
            var inset = h - 0.5f;
            var (p, yaw) = side switch
            {
                0 => (new Vector3(a, 0.15f, -inset), Mathf.Pi),
                1 => (new Vector3(a, 0.15f, inset), 0f),
                2 => (new Vector3(-inset, 0.15f, a), -Mathf.Pi / 2),
                _ => (new Vector3(inset, 0.15f, a), Mathf.Pi / 2),
            };
            props.Add(i == n ? Props.Hydrant() : Props.Bin(), center + p, yaw, range: 120);
        }
    }

    private static void BuildTree(Node3D root, StaticBody3D body, Vector3 p, RandomNumberGenerator rng)
    {
        // Mêmes tirages qu'auparavant (hauteur, puis rayon et décalage de 4 houppiers).
        var hgt = rng.RandfRange(5, 9);
        var blobs = new (float r, Vector3 off)[4];
        for (var i = 0; i < 4; i++)
        {
            var r = rng.RandfRange(1.6f, 2.8f);
            var ox = rng.RandfRange(-1.2f, 1.2f);
            var oy = rng.RandfRange(-1, 1);
            var oz = rng.RandfRange(-1.2f, 1.2f);
            blobs[i] = (r, new Vector3(ox, oy, oz));
        }
        AddTree(root, body, p, hgt, blobs);
    }

    private static void AddTree(Node3D root, StaticBody3D body, Vector3 p, float hgt, (float r, Vector3 off)[] blobs)
    {
        root.AddChild(new MeshInstance3D { Mesh = Props.Tree(hgt, blobs, p.X * 0.37f + p.Z * 0.11f), Position = p });
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.4f, Height = hgt }, Position = p + new Vector3(0, hgt / 2, 0) });
    }

    private static void BuildPalm(Node3D root, StaticBody3D body, Vector3 p, RandomNumberGenerator rng)
    {
        var hgt = rng.RandfRange(7, 10);
        var trunk = new Node3D { Position = p, RotationDegrees = new Vector3(rng.RandfRange(-6, 6), rng.RandfRange(0, 360), rng.RandfRange(-6, 6)) };
        root.AddChild(trunk);
        trunk.AddChild(new MeshInstance3D { Mesh = Props.Palm(hgt, p.X * 0.71f) });
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.35f, Height = hgt }, Position = p + new Vector3(0, hgt / 2, 0) });
    }

    private static readonly Color[] CarColors =
    {
        new(0.5f, 0.5f, 0.51f), new(0.08f, 0.08f, 0.085f), new(0.33f, 0.13f, 0.11f), new(0.15f, 0.18f, 0.24f),
        new(0.6f, 0.6f, 0.58f), new(0.22f, 0.23f, 0.24f), new(0.3f, 0.28f, 0.2f),
    };

    private static void BuildVehicle(Node3D root, StaticBody3D body, Vector3 p, float yaw, Props.VehicleType type, Color paint, float len, RandomNumberGenerator vrng)
    {
        // Les fourgons sont surtout blancs ; les camions portent une bande de couleur sur leur caisse.
        if (type == Props.VehicleType.Van && vrng.Randf() < 0.6f) paint = new Color(0.6f, 0.6f, 0.58f);
        if (Props.Burnt(p)) paint = Mats.BurntPaint; // épave calcinée (sans tirage aléatoire)
        var mesh = Props.Vehicle(type, paint, len);
        root.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            Position = p,
            Rotation = new Vector3(0, yaw, 0),
            VisibilityRangeEnd = 260f,
        });
        body.AddChild(VehicleShape(mesh, type, len, p, yaw));
    }

    /// <summary>Collision exacte d'un véhicule garé (carrosserie, cabine, roues), partagée par les véhicules du même modèle.</summary>
    public static CollisionShape3D VehicleShape(Mesh mesh, Props.VehicleType type, float len, Vector3 p, float yaw)
    {
        if (MeshKit.ShapeOf(mesh) is { } exact) return new CollisionShape3D { Shape = exact, Position = p, Rotation = new Vector3(0, yaw, 0) };
        var size = Props.VehicleSize(type, len);
        return new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = p + new Vector3(0, size.Y / 2 + 0.05f, 0), Rotation = new Vector3(0, yaw, 0) };
    }

    /// <summary>Ajoute la collision exacte des triangles solides d'un assemblage (repère <paramref name="at"/>).</summary>
    public static void AddSolid(StaticBody3D body, MeshKit kit, Vector3 at = default)
    {
        if (kit.SolidShape() is { } s) body.AddChild(new CollisionShape3D { Shape = s, Position = at });
    }

    private static void BuildLamp(Node3D root, StaticBody3D body, PropScatter props, Vector3 p, float yaw, bool night)
    {
        props.Add(Props.StreetLamp(night), p, yaw, range: 260, shadows: true);
        if (night)
        {
            var head = p + new Basis(Vector3.Up, yaw) * new Vector3(0, 6.8f, 1.45f);
            root.AddChild(new OmniLight3D { Position = head, LightColor = new Color(1f, 0.78f, 0.5f), LightEnergy = 3f, OmniRange = 20f, OmniAttenuation = 1.2f, ShadowEnabled = false });
        }
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.15f, Height = 7f }, Position = p + new Vector3(0, 3.5f, 0) });
    }

    // ───────────────────────── Marquage au sol ─────────────────────────

    /// <summary>Bande de peinture rectangulaire à plat (coordonnées monde).</summary>
    private static void Paint(MeshKit kit, float x0, float z0, float x1, float z1)
    {
        var m = Mats.RoadPaint();
        kit.Quad(m, new Vector3(x0, PaintY, z1), new Vector3(x1, PaintY, z1), new Vector3(x1, PaintY, z0), new Vector3(x0, PaintY, z0));
    }

    /// <summary>
    /// Marquages d'un tronçon de rue (repère local : a = long de la rue, t = travers, ±6 m) :
    /// passages piétons, lignes d'arrêt, axe discontinu, lignes de stationnement.
    /// </summary>
    private static void RoadSegment(MeshKit kit, MeshKit ground, bool vertical, float c, float s0, float s1, bool cross0, bool cross1, RandomNumberGenerator deco)
    {
        void P(float a0, float a1, float t0, float t1)
        {
            if (a1 <= a0) return;
            if (vertical) Paint(kit, c + t0, a0, c + t1, a1);
            else Paint(kit, a0, c + t0, a1, c + t1);
        }
        Vector3 W(float a, float t, float y) => vertical ? new Vector3(c + t, y, a) : new Vector3(a, y, c + t);

        // Passages piétons (bandes parallèles à la circulation) et lignes d'arrêt.
        void Zebra(float a0, float a1)
        {
            for (var t = -5.3f; t < 5.4f; t += 1.1f) P(a0, a1, t - 0.27f, t + 0.27f);
        }
        // Voie entrante côté s0 : t > 0 pour les rues nord-sud, t < 0 pour les rues est-ouest (circulation à droite).
        var in0 = vertical ? 1f : -1f;
        if (cross0)
        {
            Zebra(s0 + 0.6f, s0 + 3.4f);
            if (in0 > 0) P(s0 + 4.0f, s0 + 4.35f, 0.15f, 5.6f); else P(s0 + 4.0f, s0 + 4.35f, -5.6f, -0.15f);
        }
        if (cross1)
        {
            Zebra(s1 - 3.4f, s1 - 0.6f);
            if (in0 > 0) P(s1 - 4.35f, s1 - 4.0f, -5.6f, -0.15f); else P(s1 - 4.35f, s1 - 4.0f, 0.15f, 5.6f);
        }

        var a0L = s0 + (cross0 ? 4.6f : 1f);
        var a1L = s1 - (cross1 ? 4.6f : 1f);
        // Axe : continu à l'approche des carrefours, discontinu (3 m / 5 m) entre les deux.
        var solid0 = cross0 ? a0L + 10f : a0L;
        var solid1 = cross1 ? a1L - 10f : a1L;
        P(a0L, solid0, -0.07f, 0.07f);
        P(solid1, a1L, -0.07f, 0.07f);
        for (var a = solid0 + 2.5f; a + 3f < solid1 - 2.5f; a += 8f) P(a, a + 3f, -0.07f, 0.07f);
        // Limites des voies de stationnement.
        P(a0L, a1L, 2.64f, 2.76f);
        P(a0L, a1L, -2.76f, -2.64f);

        // Plaque d'égout dans une voie et avaloirs au fil d'eau près des carrefours.
        var dark = Mats.Metal(new Color(0.11f, 0.11f, 0.11f), 0.7f);
        if (deco.Randf() < 0.75f)
        {
            var a = deco.RandfRange(a0L + 6, Mathf.Max(a0L + 6.5f, a1L - 6));
            var t = deco.Randf() < 0.5f ? -1.4f : 1.4f;
            ground.Col = Colors.White;
            ground.Cylinder(dark, W(a, t, -0.01f), 0.34f, 0.34f, 0.025f, 14);
        }
        foreach (var (a, t) in new[] { (s0 + 5.5f, 5.55f), (s1 - 5.5f, -5.55f) })
        {
            if (!(a > s0 && a < s1)) continue;
            var size = vertical ? new Vector3(0.38f, 0.03f, 0.85f) : new Vector3(0.85f, 0.03f, 0.38f);
            ground.Box(dark, W(a, t, 0.0f), size, null, false);
        }
    }

    private static void BuildRoadMarkings(Node3D root, MeshKit ground, RandomNumberGenerator deco)
    {
        var kit = new MeshKit();
        var edge = Half;
        // Rues nord-sud : de la limite nord jusqu'au boulevard.
        foreach (var xr in RoadXs)
        {
            var nodes = new List<float> { -edge };
            foreach (var zr in RoadZs) { nodes.Add(zr - 6); nodes.Add(zr + 6); }
            nodes.Add(BlvdNorth);
            for (var i = 0; i + 1 < nodes.Count; i += 2)
                RoadSegment(kit, ground, true, xr, nodes[i], nodes[i + 1], i > 0, true, deco);
        }
        // Rues est-ouest.
        foreach (var zr in RoadZs)
        {
            var nodes = new List<float> { -edge };
            foreach (var xr in RoadXs) { nodes.Add(xr - 6); nodes.Add(xr + 6); }
            nodes.Add(edge);
            for (var i = 0; i + 1 < nodes.Count; i += 2)
                RoadSegment(kit, ground, false, zr, nodes[i], nodes[i + 1], i > 0, i + 2 < nodes.Count, deco);
        }

        // Boulevard du front de mer : 2 × 2 voies, double ligne axiale, interrompu aux carrefours en T.
        const float mid = (BlvdNorth + BlvdSouth) / 2;
        var cuts = new List<(float, float)>();
        foreach (var xr in RoadXs) cuts.Add((xr - 6.5f, xr + 11.5f));
        var runs = new List<(float, float)>();
        var start = -edge;
        foreach (var (c0, c1) in cuts) { runs.Add((start, c0)); start = c1; }
        runs.Add((start, edge));
        foreach (var (r0, r1) in runs)
        {
            Paint(kit, r0, mid - 0.27f, r1, mid - 0.15f);
            Paint(kit, r0, mid + 0.15f, r1, mid + 0.27f);
            Paint(kit, r0, BlvdNorth + 0.5f, r1, BlvdNorth + 0.65f);
            Paint(kit, r0, BlvdSouth - 0.65f, r1, BlvdSouth - 0.5f);
            foreach (var lz in new[] { (BlvdNorth + mid) / 2, (BlvdSouth + mid) / 2 })
                for (var a = r0 + 1.5f; a + 3f < r1 - 1f; a += 9f) Paint(kit, a, lz - 0.07f, a + 3f, lz + 0.07f);
        }
        // Passages piétons vers la promenade, juste à l'est de chaque carrefour en T.
        foreach (var xr in RoadXs)
            for (var z = BlvdNorth + 0.8f; z < BlvdSouth - 0.8f; z += 1.1f)
                Paint(kit, xr + 7.5f, z, xr + 10.5f, z + 0.55f);

        root.AddChild(new MeshInstance3D { Name = "RoadMarkings", Mesh = kit.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Feux tricolores aux quatre coins de chaque carrefour, tournés vers les véhicules qui arrivent.</summary>
    private static void BuildTrafficLights(PropScatter props, RandomNumberGenerator deco)
    {
        foreach (var xr in RoadXs)
        foreach (var zr in RoadZs)
        {
            var nsGreen = deco.Randf() < 0.5f;
            const float y = 0.15f;
            // Approche depuis le nord (vers +Z) : coin nord-ouest, feu tourné vers -Z.
            props.Add(Props.TrafficLight(nsGreen), new Vector3(xr - 6.6f, y, zr - 10f), Mathf.Pi, range: 200);
            // Approche depuis le sud (vers -Z) : coin sud-est.
            props.Add(Props.TrafficLight(nsGreen), new Vector3(xr + 6.6f, y, zr + 10f), 0, range: 200);
            // Approche depuis l'est (vers -X) : coin nord-est.
            props.Add(Props.TrafficLight(!nsGreen), new Vector3(xr + 10f, y, zr - 6.6f), Mathf.Pi / 2, range: 200);
            // Approche depuis l'ouest (vers +X) : coin sud-ouest.
            props.Add(Props.TrafficLight(!nsGreen), new Vector3(xr - 10f, y, zr + 6.6f), -Mathf.Pi / 2, range: 200);
        }
    }

    // ───────────────────────── Front de mer ─────────────────────────

    private static void BuildSeafront(Node3D root, StaticBody3D body, PropScatter props, RandomNumberGenerator deco, bool night)
    {
        const float y = 0.15f;
        // Abribus sur la promenade, face au boulevard.
        var stops = new[] { -145f, -45f, 55f, 155f };
        foreach (var x in stops) props.Add(Props.BusStop(night), new Vector3(x, y, BlvdSouth + 1.3f), Mathf.Pi, range: 220, shadows: true);

        // Lampadaires du boulevard (éclairent aussi la promenade).
        for (var x = -Half + 12.5f; x < Half; x += 25f)
        {
            var near = false;
            foreach (var s in stops) near |= Mathf.Abs(x - s) < 5f;
            if (near) continue;
            BuildLamp(root, body, props, new Vector3(x, y, BlvdSouth + 0.7f), Mathf.Pi, night);
        }

        // Bancs face à la mer entre les palmiers, avec corbeilles.
        var i = 0;
        for (var x = -Half + 21; x < Half - 5; x += 22, i++)
        {
            props.Add(Props.Bench(), new Vector3(x, y, BeachStart - 5.2f), 0, range: 140);
            if (i % 2 == 0) props.Add(Props.Bin(), new Vector3(x + 1.7f, y, BeachStart - 5.4f), 0, range: 120);
        }

        // Garde-corps de la promenade (ouvert face aux rues et au ponton pour descendre sur la plage).
        var kit = new MeshKit { Record = true };
        var metal = Mats.DetailMetal();
        kit.Col = new Color(0.78f, 0.8f, 0.8f);
        var openings = new List<(float, float)>();
        foreach (var xr in RoadXs) openings.Add((xr - (Mathf.IsEqualApprox(xr, PierX) ? 2.8f : 2.2f), xr + (Mathf.IsEqualApprox(xr, PierX) ? 2.8f : 2.2f)));
        var z = BeachStart - 0.15f;
        var x0 = -Half;
        foreach (var (o0, o1) in openings)
        {
            Railing(kit, metal, x0, o0, z, y);
            x0 = o1;
        }
        Railing(kit, metal, x0, Half, z, y);
        // Muret en pierre sous le garde-corps (bord de la promenade).
        kit.Col = new Color(0.66f, 0.64f, 0.6f);
        kit.BoxMinMax(Mats.Detail(), new Vector3(-Half, -0.6f, BeachStart - 0.3f), new Vector3(Half, 0.2f, BeachStart + 0.05f));
        root.AddChild(new MeshInstance3D { Name = "Railing", Mesh = kit.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        AddSolid(body, kit);
    }

    /// <summary>Garde-corps tubulaire : poteaux tous les 2 m, main courante et lisse intermédiaire.</summary>
    private static void Railing(MeshKit kit, Material m, float x0, float x1, float z, float y)
    {
        if (x1 - x0 < 0.5f) return;
        var n = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / 2f));
        for (var i = 0; i <= n; i++)
        {
            var x = Mathf.Lerp(x0, x1, i / (float)n);
            kit.Box(m, new Vector3(x, y + 0.5f, z), new Vector3(0.06f, 1.0f, 0.06f));
        }
        kit.Tube(m, new Vector3(x0, y + 1.0f, z), new Vector3(x1, y + 1.0f, z), 0.035f, 6);
        kit.Tube(m, new Vector3(x0, y + 0.5f, z), new Vector3(x1, y + 0.5f, z), 0.02f, 5);
    }

    private static readonly Color[] BeachColors =
    {
        new(0.85f, 0.15f, 0.12f), new(0.1f, 0.35f, 0.7f), new(0.95f, 0.75f, 0.15f), new(0.95f, 0.95f, 0.92f),
        new(0.15f, 0.55f, 0.5f), new(0.9f, 0.45f, 0.1f), new(0.35f, 0.6f, 0.85f),
    };

    private static void BuildBeach(Node3D root, StaticBody3D body, PropScatter props, RandomNumberGenerator deco)
    {
        const float sandY = -0.02f;
        // Groupes de parasols, serviettes et transats.
        for (var g = 0; g < 16; g++)
        {
            var cx = deco.RandfRange(-Half + 15, Half - 15);
            if (Mathf.Abs(cx - PierX) < 14 || Mathf.Abs(cx - 65) < 12) continue;
            var cz = deco.RandfRange(BeachStart + 9, SeaStart - 9);
            var n = deco.RandiRange(2, 5);
            for (var i = 0; i < n; i++)
            {
                var p = new Vector3(cx + deco.RandfRange(-7, 7), sandY, cz + deco.RandfRange(-4, 4));
                var col = BeachColors[deco.RandiRange(0, BeachColors.Length - 1)];
                var yaw = deco.RandfRange(0, Mathf.Tau);
                props.Add(Props.UmbrellaPole(), p, yaw, range: 220);
                props.Add(Props.UmbrellaTop(), p, yaw, col, range: 260, shadows: true);
                var tp = p + new Basis(Vector3.Up, yaw) * new Vector3(0.6f, 0, 0.9f);
                if (deco.Randf() < 0.6f)
                    props.Add(Props.Towel(), tp, yaw + deco.RandfRange(-0.3f, 0.3f), BeachColors[deco.RandiRange(0, BeachColors.Length - 1)], range: 140);
                else
                    props.Add(Props.Lounger(), tp, yaw + deco.RandfRange(-0.2f, 0.2f), range: 140, shadows: true);
            }
        }
        BuildLifeguardTower(root, body, new Vector3(65, sandY, BeachStart + 26));
    }

    /// <summary>Poste de secours sur pilotis avec rampe, drapeau et bouée.</summary>
    private static void BuildLifeguardTower(Node3D root, StaticBody3D body, Vector3 p)
    {
        var k = new MeshKit { Record = true };
        var d = Mats.Detail();
        var wood = Mats.Planks();
        k.Col = new Color(0.85f, 0.84f, 0.8f);
        foreach (var x in new[] { -1.3f, 1.3f })
        foreach (var z in new[] { -1.1f, 1.1f })
            k.Box(d, new Vector3(x, 1.25f, z), new Vector3(0.18f, 2.5f, 0.18f));
        k.Col = Colors.White;
        k.Box(wood, new Vector3(0, 2.55f, 0.3f), new Vector3(3.2f, 0.12f, 3.4f));
        // Cabine.
        k.Col = new Color(0.92f, 0.9f, 0.86f);
        k.Box(d, new Vector3(0, 3.65f, -0.35f), new Vector3(2.8f, 2.1f, 2.2f));
        k.Col = new Color(0.75f, 0.12f, 0.1f);
        k.Box(d, new Vector3(0, 4.05f, 0.77f), new Vector3(2.82f, 0.25f, 0.06f));
        k.Col = new Color(0.08f, 0.09f, 0.1f);
        k.Box(Mats.Painted(new Color(0.06f, 0.07f, 0.08f), 0.05f, 0.3f), new Vector3(0, 3.55f, 0.76f), new Vector3(2.2f, 0.7f, 0.04f));
        k.Col = new Color(0.75f, 0.12f, 0.1f);
        k.Frustum(d, new Vector3(0, 4.7f, -0.35f), new Vector2(3.3f, 2.8f), new Vector2(0.4f, 0.4f), 0.7f);
        // Garde-corps de la terrasse.
        k.Col = new Color(0.9f, 0.9f, 0.88f);
        k.Box(d, new Vector3(0, 3.1f, 1.95f), new Vector3(3.2f, 0.08f, 0.08f));
        k.Box(d, new Vector3(-1.56f, 3.1f, 1.25f), new Vector3(0.08f, 0.08f, 1.5f));
        k.Box(d, new Vector3(1.56f, 3.1f, 1.25f), new Vector3(0.08f, 0.08f, 1.5f));
        foreach (var x in new[] { -1.56f, -0.5f, 0.5f, 1.56f }) k.Box(d, new Vector3(x, 2.85f, 1.95f), new Vector3(0.06f, 0.5f, 0.06f));
        // Rampe d'accès vers l'arrière.
        k.Col = Colors.White;
        var ang = Mathf.Atan2(2.5f, 5.5f);
        k.Box(wood, new Vector3(0, 1.28f, -1.9f - 2.75f), new Vector3(1.2f, 0.1f, Mathf.Sqrt(2.5f * 2.5f + 5.5f * 5.5f)), new Basis(Vector3.Right, -ang));
        // Mât et drapeau.
        k.Col = new Color(0.8f, 0.8f, 0.8f);
        k.Cylinder(Mats.DetailMetal(), new Vector3(1.6f, 2.6f, -1.3f), 0.04f, 0.03f, 4.2f, 6);
        k.Col = new Color(0.95f, 0.8f, 0.1f);
        k.Box(d, new Vector3(2.05f, 6.45f, -1.3f), new Vector3(0.9f, 0.3f, 0.02f));
        k.Col = new Color(0.85f, 0.1f, 0.08f);
        k.Box(d, new Vector3(2.05f, 6.15f, -1.3f), new Vector3(0.9f, 0.3f, 0.02f));
        // Bouée.
        k.Col = new Color(0.95f, 0.4f, 0.08f);
        k.Cylinder(d, new Vector3(-1.45f, 3.0f, 0.8f), 0.28f, 0.28f, 0.1f, 10, true, new Basis(Vector3.Back, -Mathf.Pi / 2));

        root.AddChild(new MeshInstance3D { Name = "LifeguardTower", Mesh = k.Commit(), Position = p });
        AddSolid(body, k, p);
    }

    /// <summary>Ponton décoratif en bois sur pilotis, prolongeant une rue jusqu'au large.</summary>
    private static void BuildPier(Node3D root, StaticBody3D body, bool night)
    {
        var k = new MeshKit { Record = true };
        var wood = Mats.Planks();
        var d = Mats.Detail();
        var metal = Mats.DetailMetal();
        const float w = 5f;
        const float z0 = BeachStart;
        const float zRamp = SeaStart - 6;
        const float z1 = 330f;
        const float yTop = 1.4f;
        float Top(float z) => z < zRamp ? Mathf.Lerp(0.15f, yTop, (z - z0) / (zRamp - z0)) : yTop;

        // Platelage : rampe douce au-dessus du sable, puis partie horizontale.
        k.Col = Colors.White;
        var rampLen = zRamp - z0;
        var ang = Mathf.Atan2(yTop - 0.15f, rampLen);
        var rampBasis = new Basis(Vector3.Right, -ang);
        var rampC = new Vector3(PierX, (0.15f + yTop) / 2 - 0.1f, (z0 + zRamp) / 2);
        k.Box(wood, rampC, new Vector3(w, 0.2f, rampLen / Mathf.Cos(ang)), rampBasis);
        k.Box(wood, new Vector3(PierX, yTop - 0.1f, (zRamp + z1) / 2), new Vector3(w, 0.2f, z1 - zRamp));
        // Pilotis par paires et longerons.
        k.Col = new Color(0.3f, 0.24f, 0.18f);
        for (var z = z0 + 6; z <= z1; z += 5f)
        {
            var top = Top(z) - 0.2f;
            foreach (var sx in new[] { -1f, 1f })
                k.Cylinder(d, new Vector3(PierX + sx * (w / 2 - 0.3f), -4f, z), 0.2f, 0.18f, top + 4f, 8, false);
            k.Box(d, new Vector3(PierX, top - 0.15f, z), new Vector3(w, 0.25f, 0.25f));
        }
        // Garde-corps et lampadaires.
        k.Col = new Color(0.82f, 0.84f, 0.84f);
        for (var z = z0 + 2; z <= z1; z += 2.5f)
            foreach (var sx in new[] { -1f, 1f })
                k.Box(metal, new Vector3(PierX + sx * (w / 2 - 0.08f), Top(z) + 0.5f, z), new Vector3(0.06f, 1.0f, 0.06f));
        foreach (var sx in new[] { -1f, 1f })
        {
            var x = PierX + sx * (w / 2 - 0.08f);
            k.Tube(metal, new Vector3(x, Top(z0 + 2) + 1f, z0 + 2), new Vector3(x, yTop + 1f, zRamp), 0.035f, 6);
            k.Tube(metal, new Vector3(x, yTop + 1f, zRamp), new Vector3(x, yTop + 1f, z1), 0.035f, 6);
            k.Tube(metal, new Vector3(x, Top(z0 + 2) + 0.5f, z0 + 2), new Vector3(x, yTop + 0.5f, zRamp), 0.02f, 5);
            k.Tube(metal, new Vector3(x, yTop + 0.5f, zRamp), new Vector3(x, yTop + 0.5f, z1), 0.02f, 5);
        }
        for (var z = zRamp + 10; z < z1; z += 24f)
            foreach (var sx in new[] { -1f, 1f })
            {
                var x = PierX + sx * (w / 2 - 0.08f);
                k.Col = new Color(0.22f, 0.24f, 0.25f);
                k.Cylinder(metal, new Vector3(x, yTop, z), 0.07f, 0.05f, 3.6f, 6);
                k.Col = night ? new Color(1f, 0.85f, 0.6f) : new Color(0.9f, 0.9f, 0.86f);
                k.Cylinder(night ? Mats.Lamps() : d, new Vector3(x, yTop + 3.6f, z), 0.18f, 0.18f, 0.35f, 8);
                k.Col = new Color(0.22f, 0.24f, 0.25f);
                k.Cylinder(metal, new Vector3(x, yTop + 3.95f, z), 0.25f, 0.02f, 0.18f, 8);
            }
        // Kiosque en bout de ponton.
        k.Col = new Color(0.88f, 0.86f, 0.8f);
        k.Box(d, new Vector3(PierX, yTop + 1.4f, z1 - 4f), new Vector3(3.4f, 2.8f, 3.4f));
        k.Col = new Color(0.12f, 0.3f, 0.45f);
        k.Frustum(d, new Vector3(PierX, yTop + 2.8f, z1 - 4f), new Vector2(4.2f, 4.2f), new Vector2(0.3f, 0.3f), 1.3f);
        root.AddChild(new MeshInstance3D { Name = "Pier", Mesh = k.Commit() });
        // Platelage, garde-corps, lampadaires et kiosque : collision exacte (au-delà du mur invisible, hors d'atteinte).
        AddSolid(body, k);
    }

    // ───────────────────────── Terrains vagues ─────────────────────────

    /// <summary>Parking de surface : enrobé, places peintes en épi droit, voitures garées.</summary>
    private static void BuildParkingLot(Node3D root, StaticBody3D body, Rect2 lot, RandomNumberGenerator deco)
    {
        var k = new MeshKit();
        var paint = Mats.RoadPaint();
        var min = lot.Position + Vector2.One * 0.9f;
        var max = lot.End - Vector2.One * 0.9f;
        const float y = 0.175f;
        k.Col = Colors.White;
        k.BoxMinMax(Mats.Asphalt(), new Vector3(min.X, 0.12f, min.Y), new Vector3(max.X, 0.17f, max.Y), false);
        void Line(float x0, float z0, float x1, float z1) =>
            k.Quad(paint, new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), new Vector3(x0, y, z0));

        // Rangées de places (5 m de profondeur) le long des bords, et une double rangée centrale si le terrain est profond.
        var rows = new List<(float z0, float z1, float yaw)> { (min.Y + 0.3f, min.Y + 5.3f, Mathf.Pi), (max.Y - 5.3f, max.Y - 0.3f, 0) };
        var depth = max.Y - min.Y;
        if (depth > 28)
        {
            var c = (min.Y + max.Y) / 2;
            rows.Add((c - 5f, c, 0));
            rows.Add((c, c + 5f, Mathf.Pi));
        }
        foreach (var (z0, z1, yaw) in rows)
        {
            var n = Mathf.FloorToInt((max.X - min.X - 1f) / 2.5f);
            var x0 = (min.X + max.X) / 2 - n * 1.25f;
            for (var i = 0; i <= n; i++) Line(x0 + i * 2.5f - 0.06f, z0, x0 + i * 2.5f + 0.06f, z1);
            for (var i = 0; i < n; i++)
            {
                if (deco.Randf() > 0.6f) continue;
                var len = deco.RandfRange(4.0f, 4.7f);
                var col = CarColors[deco.RandiRange(0, CarColors.Length - 1)];
                var p = new Vector3(x0 + (i + 0.5f) * 2.5f, 0.17f, (z0 + z1) / 2);
                BuildVehicle(root, body, p, yaw + deco.RandfRange(-0.04f, 0.04f), Props.VehicleType.Car, col, len, deco);
            }
        }
        root.AddChild(new MeshInstance3D { Name = "ParkingLot", Mesh = k.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Petite place : pelouses plantées, fontaine centrale, bancs.</summary>
    private static void BuildPlaza(Node3D root, StaticBody3D body, PropScatter props, Rect2 lot, RandomNumberGenerator deco)
    {
        var k = new MeshKit { Record = true };
        var c = lot.GetCenter();
        var hx = lot.Size.X / 2;
        var hz = lot.Size.Y / 2;
        // Pelouses dans les quatre coins, bordées de pierre.
        foreach (var sx in new[] { -1f, 1f })
        foreach (var sz in new[] { -1f, 1f })
        {
            var gmin = new Vector3(c.X + (sx < 0 ? -hx + 2.5f : 3.5f), 0.15f, c.Y + (sz < 0 ? -hz + 2.5f : 3.5f));
            var gmax = new Vector3(c.X + (sx < 0 ? -3.5f : hx - 2.5f), 0.27f, c.Y + (sz < 0 ? -3.5f : hz - 2.5f));
            if (gmax.X - gmin.X < 2 || gmax.Z - gmin.Z < 2) continue;
            k.Col = new Color(0.62f, 0.61f, 0.58f);
            k.BoxMinMax(Mats.Detail(), gmin - new Vector3(0.15f, 0, 0.15f), gmax + new Vector3(0.15f, -0.02f, 0.15f), false);
            k.Col = Colors.White;
            k.BoxMinMax(Mats.Grass(), gmin + new Vector3(0, 0.01f, 0), gmax, false);
            var tp = new Vector3((gmin.X + gmax.X) / 2 + deco.RandfRange(-1, 1), 0.27f, (gmin.Z + gmax.Z) / 2 + deco.RandfRange(-1, 1));
            var hgt = deco.RandfRange(4.5f, 7f);
            var blobs = new (float r, Vector3 off)[3];
            for (var i = 0; i < 3; i++) blobs[i] = (deco.RandfRange(1.3f, 2.1f), new Vector3(deco.RandfRange(-1, 1), deco.RandfRange(-0.8f, 0.8f), deco.RandfRange(-1, 1)));
            AddTree(root, body, tp, hgt, blobs);
            // Banc tourné vers la fontaine.
            var bp = new Vector3(c.X + sx * 2.6f, 0.15f, c.Y + sz * 4.5f);
            props.Add(Props.Bench(), bp, sz < 0 ? 0 : Mathf.Pi, range: 140);
        }
        // Fontaine.
        var fc = new Vector3(c.X, 0.15f, c.Y);
        k.Col = new Color(0.7f, 0.68f, 0.64f);
        k.Cylinder(Mats.Detail(), fc, 2.2f, 2.2f, 0.5f, 20);
        k.Col = new Color(0.66f, 0.64f, 0.6f);
        k.Cylinder(Mats.Detail(), fc + Vector3.Up * 0.5f, 0.35f, 0.25f, 1.2f, 10);
        k.Cylinder(Mats.Detail(), fc + Vector3.Up * 1.7f, 0.8f, 0.9f, 0.18f, 14);
        k.Col = Colors.White;
        k.Cylinder(Mats.Painted(new Color(0.08f, 0.16f, 0.17f), 0.03f, 0.1f), fc + Vector3.Up * 0.5f, 2.0f, 2.0f, 0.01f, 20);
        root.AddChild(new MeshInstance3D { Name = "Plaza", Mesh = k.Commit() });
        AddSolid(body, k);
    }

    // ───────────────────────── Chantiers ─────────────────────────

    /// <summary>Terrain vague transformé en petit chantier : sol en terre, barrières, conteneur, cônes.</summary>
    private static void BuildWorksite(Node3D root, StaticBody3D body, PropScatter props, Rect2 lot, RandomNumberGenerator deco)
    {
        var c = lot.GetCenter();
        var k = new MeshKit { Record = true };
        var inset = 1.4f;
        var min = lot.Position + Vector2.One * inset;
        var max = lot.End - Vector2.One * inset;
        k.Col = Colors.White;
        k.BoxMinMax(Mats.Dirt(), new Vector3(min.X, 0.14f, min.Y), new Vector3(max.X, 0.19f, max.Y), false);
        // Tas de gravats et conteneur de chantier.
        k.Col = new Color(0.55f, 0.52f, 0.48f);
        var pile = new Vector3(c.X + deco.RandfRange(-3, 3), 0.18f, c.Y + deco.RandfRange(-3, 3));
        k.Frustum(Mats.Detail(), pile, new Vector2(6, 5), new Vector2(1.5f, 1.2f), 1.8f);
        var contCol = deco.Randf() < 0.5f ? new Color(0.12f, 0.3f, 0.55f) : new Color(0.75f, 0.35f, 0.08f);
        var cp = new Vector3(min.X + 4f, 0.18f, max.Y - 2.5f);
        k.Col = contCol;
        k.Box(Mats.DetailMetal(), cp + Vector3.Up * 1.3f, new Vector3(6f, 2.6f, 2.45f));
        k.Col = contCol.Darkened(0.3f);
        for (var i = 0; i < 12; i++) k.Box(Mats.DetailMetal(), cp + new Vector3(-2.75f + i * 0.5f, 1.3f, 1.24f), new Vector3(0.12f, 2.5f, 0.04f));
        root.AddChild(new MeshInstance3D { Name = "Worksite", Mesh = k.Commit() });
        AddSolid(body, k);

        // Barrières le long du périmètre (ouverture au milieu d'un côté), cônes devant.
        void Fence(Vector2 a, Vector2 b, bool gap)
        {
            var len = a.DistanceTo(b);
            var n = Mathf.FloorToInt(len / 2.1f);
            var dir = (b - a) / len;
            var yaw = Mathf.Atan2(dir.X, dir.Y) + Mathf.Pi / 2;
            for (var i = 0; i < n; i++)
            {
                var t = (i + 0.5f) / n;
                if (gap && Mathf.Abs(t - 0.5f) < 0.15f) continue;
                var p = a.Lerp(b, t);
                props.Add(Props.Barricade(), new Vector3(p.X, 0.15f, p.Y), yaw, range: 150);
            }
        }
        Fence(new Vector2(min.X, min.Y), new Vector2(max.X, min.Y), deco.Randf() < 0.5f);
        Fence(new Vector2(max.X, min.Y), new Vector2(max.X, max.Y), false);
        Fence(new Vector2(max.X, max.Y), new Vector2(min.X, max.Y), false);
        Fence(new Vector2(min.X, max.Y), new Vector2(min.X, min.Y), true);
        for (var i = 0; i < 4; i++)
            props.Add(Props.Cone(), new Vector3(c.X + deco.RandfRange(-lot.Size.X / 2 + 0.6f, lot.Size.X / 2 - 0.6f), 0.15f, min.Y - 0.7f), deco.RandfRange(0, Mathf.Tau), range: 110);
        // Séparateurs en béton côté rue.
        for (var i = 0; i < 3; i++)
            props.Add(Props.JerseyBarrier(), new Vector3(max.X - 2.5f - i * 2.05f, 0.15f, max.Y + 0.6f), Mathf.Pi / 2, range: 150, shadows: true);
    }
}
