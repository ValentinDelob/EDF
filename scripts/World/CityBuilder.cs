using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Génère la carte « Ville côtière » de façon déterministe (même graine chez tous les pairs,
/// donc mêmes identifiants d'immeubles).
/// </summary>
public static class CityBuilder
{
    public const float Half = 220f;
    public const float BeachStart = 112f;
    public const float SeaStart = 158f;
    public const float RoadEvery = 50f;
    public const float RoadWidth = 12f;

    public class Result
    {
        public readonly List<Building> Buildings = new();
        public readonly Dictionary<string, Vector3> Zones = new();
        public readonly List<Vector3> NestSpots = new();
        public readonly List<Vector3> PlayerSpawns = new();
    }

    public static Result Build(Node3D root, TimeOfDay time)
    {
        var res = new Result();
        var rng = new RandomNumberGenerator { Seed = 1337 };
        var night = time == TimeOfDay.Night;

        var statics = new StaticBody3D { Name = "Ground", CollisionLayer = 1, CollisionMask = 0 };
        root.AddChild(statics);

        // Sol de la ville (asphalte), de la plage et l'extérieur.
        AddSlab(root, statics, new Vector3(0, -0.5f, (-Half + BeachStart) / 2), new Vector3(Half * 2 + 40, 1f, BeachStart + Half), Mats.Asphalt());
        var beachLen = SeaStart - BeachStart + 30;
        AddSlab(root, statics, new Vector3(0, -0.52f, BeachStart + beachLen / 2), new Vector3(Half * 2 + 40, 1f, beachLen), Mats.Sand(), collide: true);
        // Promenade du front de mer.
        AddSlab(root, statics, new Vector3(0, 0.0f, BeachStart - 3), new Vector3(Half * 2, 0.3f, 6), Mats.Sidewalk());

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
        var lampPositions = new List<Vector3>();

        for (var bx = -Half + RoadEvery / 2; bx < Half; bx += RoadEvery)
        for (var bz = -Half + RoadEvery / 2; bz < BeachStart - 10; bz += RoadEvery)
        {
            var center = new Vector3(bx, 0, bz);
            var inner = RoadEvery - RoadWidth; // 38 m
            AddSlab(root, statics, center + new Vector3(0, 0.0f, 0), new Vector3(inner, 0.3f, inner), Mats.Sidewalk());

            var isPark = Mathf.Abs(bx) < 30 && Mathf.Abs(bz) < 30;
            if (isPark)
            {
                BuildPark(root, statics, center, inner, rng);
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
                if (rng.Randf() < 0.12f) continue; // terrain vague
                var setback = rng.RandfRange(1.5f, 3.5f);
                var w = lot.Size.X - setback * 2;
                var d = lot.Size.Y - setback * 2;
                if (w < 6 || d < 6) continue;
                var floors = rng.RandiRange(2, maxFloors);
                var size = new Vector3(w, floors * 3.2f + 0.6f, d);
                var pos = center + new Vector3(lot.Position.X + lot.Size.X / 2, 0.15f, lot.Position.Y + lot.Size.Y / 2);
                var b = Building.Create(id++, pos, size, mats[rng.RandiRange(0, mats.Length - 1)], roof);
                root.AddChild(b);
                res.Buildings.Add(b);
            }

            // Voitures garées le long du pâté.
            var cars = rng.RandiRange(1, 4);
            for (var c = 0; c < cars; c++)
            {
                var side = rng.RandiRange(0, 3);
                var along = rng.RandfRange(-inner / 2 + 4, inner / 2 - 4);
                var off = inner / 2 + 2.2f;
                Vector3 p;
                float yaw;
                switch (side)
                {
                    case 0: p = new Vector3(along, 0, -off); yaw = 0; break;
                    case 1: p = new Vector3(along, 0, off); yaw = Mathf.Pi; break;
                    case 2: p = new Vector3(-off, 0, along); yaw = Mathf.Pi / 2; break;
                    default: p = new Vector3(off, 0, along); yaw = -Mathf.Pi / 2; break;
                }
                BuildCar(root, statics, center + p, yaw + rng.RandfRange(-0.08f, 0.08f), rng);
            }

            // Lampadaires aux coins.
            lampPositions.Add(center + new Vector3(inner / 2 + 0.8f, 0, inner / 2 + 0.8f));
        }

        foreach (var lp in lampPositions) BuildLamp(root, statics, lp, night);

        // Palmiers sur la promenade.
        for (var x = -Half + 10; x < Half; x += 22)
            BuildPalm(root, statics, new Vector3(x + rng.RandfRange(-3, 3), 0.15f, BeachStart - 3), rng);

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
        return res;
    }

    private static void AddSlab(Node3D root, StaticBody3D body, Vector3 center, Vector3 size, Material mat, bool collide = true)
    {
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size, Material = mat }, Position = center });
        if (collide) body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = center });
    }

    private static void AddWall(StaticBody3D body, Vector3 center, Vector3 size) =>
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = center });

    private static void BuildHills(Node3D root)
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

    private static void BuildTree(Node3D root, StaticBody3D body, Vector3 p, RandomNumberGenerator rng)
    {
        var hgt = rng.RandfRange(5, 9);
        var trunk = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.35f, Height = hgt, Material = Mats.Painted(new Color(0.3f, 0.22f, 0.15f), 0.9f) },
            Position = p + new Vector3(0, hgt / 2, 0),
        };
        root.AddChild(trunk);
        var leaf = Mats.Painted(new Color(0.16f, 0.3f, 0.12f), 0.85f);
        for (var i = 0; i < 4; i++)
        {
            var r = rng.RandfRange(1.6f, 2.8f);
            root.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = r, Height = r * 1.6f, RadialSegments = 12, Rings = 6, Material = leaf },
                Position = p + new Vector3(rng.RandfRange(-1.2f, 1.2f), hgt + rng.RandfRange(-1, 1), rng.RandfRange(-1.2f, 1.2f)),
            });
        }
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.4f, Height = hgt }, Position = p + new Vector3(0, hgt / 2, 0) });
    }

    private static void BuildPalm(Node3D root, StaticBody3D body, Vector3 p, RandomNumberGenerator rng)
    {
        var hgt = rng.RandfRange(7, 10);
        var trunk = new Node3D { Position = p, RotationDegrees = new Vector3(rng.RandfRange(-6, 6), rng.RandfRange(0, 360), rng.RandfRange(-6, 6)) };
        root.AddChild(trunk);
        trunk.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.3f, Height = hgt, RadialSegments = 8, Material = Mats.Painted(new Color(0.42f, 0.34f, 0.24f), 0.95f) },
            Position = new Vector3(0, hgt / 2, 0),
        });
        var leaf = Mats.Painted(new Color(0.2f, 0.36f, 0.14f), 0.8f);
        for (var i = 0; i < 7; i++)
        {
            var frond = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.05f, 4.2f), Material = leaf },
                Position = new Vector3(0, hgt, 0),
                RotationDegrees = new Vector3(25, i * 360f / 7, 0),
            };
            frond.Translate(new Vector3(0, 0, 1.8f));
            trunk.AddChild(frond);
        }
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.35f, Height = hgt }, Position = p + new Vector3(0, hgt / 2, 0) });
    }

    private static readonly Color[] CarColors =
    {
        new(0.7f, 0.7f, 0.72f), new(0.08f, 0.08f, 0.09f), new(0.55f, 0.08f, 0.06f), new(0.12f, 0.2f, 0.4f),
        new(0.85f, 0.85f, 0.83f), new(0.25f, 0.27f, 0.28f), new(0.4f, 0.35f, 0.2f),
    };

    private static void BuildCar(Node3D root, StaticBody3D body, Vector3 p, float yaw, RandomNumberGenerator rng)
    {
        var car = new Node3D { Position = p + new Vector3(0, 0.15f, 0), Rotation = new Vector3(0, yaw, 0) };
        root.AddChild(car);
        var paint = Mats.Painted(CarColors[rng.RandiRange(0, CarColors.Length - 1)], 0.25f, 0.6f);
        var glass = Mats.Painted(new Color(0.05f, 0.07f, 0.09f), 0.05f, 0.2f);
        var tire = Mats.Painted(new Color(0.05f, 0.05f, 0.05f), 0.9f);
        var len = rng.RandfRange(4.0f, 4.8f);
        car.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.85f, 0.75f, len), Material = paint }, Position = new Vector3(0, 0.7f, 0) });
        car.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.6f, len * 0.5f), Material = glass }, Position = new Vector3(0, 1.35f, -0.15f) });
        car.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.62f, 0.08f, len * 0.48f), Material = paint }, Position = new Vector3(0, 1.67f, -0.15f) });
        foreach (var (x, z) in new[] { (-0.85f, len * 0.33f), (0.85f, len * 0.33f), (-0.85f, -len * 0.33f), (0.85f, -len * 0.33f) })
            car.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.36f, BottomRadius = 0.36f, Height = 0.28f, RadialSegments = 14, Material = tire },
                Position = new Vector3(x, 0.36f, z),
                RotationDegrees = new Vector3(0, 0, 90),
            });
        car.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.12f, 0.05f), Material = Mats.Glow(new Color(1f, 0.95f, 0.8f), 1.5f) }, Position = new Vector3(-0.6f, 0.75f, len / 2) });
        car.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.4f, 0.12f, 0.05f), Material = Mats.Glow(new Color(1f, 0.95f, 0.8f), 1.5f) }, Position = new Vector3(0.6f, 0.75f, len / 2) });
        var shape = new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.9f, 1.6f, len) }, Position = car.Position + new Vector3(0, 0.8f, 0), Rotation = car.Rotation };
        body.AddChild(shape);
    }

    private static void BuildLamp(Node3D root, StaticBody3D body, Vector3 p, bool night)
    {
        var metal = Mats.Metal(new Color(0.25f, 0.27f, 0.28f), 0.5f);
        root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.14f, Height = 7f, RadialSegments = 8, Material = metal }, Position = p + new Vector3(0, 3.65f, 0) });
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.35f, 0.15f, 1.4f), Material = metal }, Position = p + new Vector3(0, 7.1f, -0.6f) });
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.3f, 0.05f, 0.6f), Material = night ? Mats.Glow(new Color(1f, 0.8f, 0.5f), 4f) : Mats.Painted(new Color(0.8f, 0.8f, 0.75f), 0.3f) }, Position = p + new Vector3(0, 7.0f, -1.0f) });
        if (night)
            root.AddChild(new OmniLight3D { Position = p + new Vector3(0, 6.6f, -1.0f), LightColor = new Color(1f, 0.78f, 0.5f), LightEnergy = 2.2f, OmniRange = 18f, OmniAttenuation = 1.3f, ShadowEnabled = false });
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.15f, Height = 7f }, Position = p + new Vector3(0, 3.5f, 0) });
    }
}
