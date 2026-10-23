using Godot;

namespace Rempart;

/// <summary>
/// Carte « Zone portuaire » : entrepôts en tôle, parc à conteneurs (piles destructibles), grues portiques,
/// cuves et quai. Génération déterministe, mêmes zones d'apparition que la ville.
/// </summary>
public static partial class PortBuilder
{
    private static readonly Color[] ContainerColors =
    {
        new(0.62f, 0.12f, 0.08f), new(0.1f, 0.25f, 0.5f), new(0.15f, 0.38f, 0.2f), new(0.8f, 0.42f, 0.1f),
        new(0.55f, 0.56f, 0.58f), new(0.85f, 0.85f, 0.82f), new(0.42f, 0.18f, 0.32f), new(0.75f, 0.62f, 0.15f),
    };

    public static CityBuilder.Result Build(Node3D root, TimeOfDay time)
    {
        var res = new CityBuilder.Result();
        var rng = new RandomNumberGenerator { Seed = 9001 };
        var night = time == TimeOfDay.Night;
        var statics = new StaticBody3D { Name = "Ground", CollisionLayer = 1, CollisionMask = 0 };
        root.AddChild(statics);
        const float half = CityBuilder.Half;

        // Sol en béton, quai et mer.
        CityBuilder.AddSlab(root, statics, new Vector3(0, -0.5f, (-half + CityBuilder.SeaStart) / 2), new Vector3(half * 2 + 40, 1f, CityBuilder.SeaStart + half), Mats.Concrete(new Color(0.5f, 0.49f, 0.47f), 77));
        CityBuilder.AddSlab(root, statics, new Vector3(0, 0.02f, CityBuilder.SeaStart - 3), new Vector3(half * 2, 0.1f, 6), Mats.Painted(new Color(0.75f, 0.62f, 0.12f), 0.7f), collide: false); // bande jaune du quai
        root.AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(2400, 1400), SubdivideWidth = 120, SubdivideDepth = 60, Material = Mats.Water(time) },
            Position = new Vector3(0, -2.5f, CityBuilder.SeaStart + 690),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        // Mur de quai.
        CityBuilder.AddSlab(root, statics, new Vector3(0, -1.5f, CityBuilder.SeaStart + 1), new Vector3(half * 2 + 40, 3f, 2), Mats.Concrete(new Color(0.42f, 0.41f, 0.4f), 78));
        CityBuilder.BuildHills(root);

        var id = 0;
        var roof = Mats.Roof();
        var industrial = new BuildingStyle { Kind = BuildingKind.Industrial };

        // Entrepôts au nord.
        var shedMats = new[] { Corrugated(new Color(0.6f, 0.62f, 0.64f), 1), Corrugated(new Color(0.35f, 0.42f, 0.5f), 2), Corrugated(new Color(0.55f, 0.5f, 0.42f), 3) };
        for (var ix = -2; ix <= 2; ix++)
        for (var iz = 0; iz < 2; iz++)
        {
            var size = new Vector3(rng.RandfRange(44, 56), rng.RandfRange(10, 15), rng.RandfRange(24, 32));
            var pos = new Vector3(ix * 80 + rng.RandfRange(-6, 6), 0, -175 + iz * 58);
            var b = Building.Create(id++, pos, size, shedMats[rng.RandiRange(0, shedMats.Length - 1)], roof, industrial);
            root.AddChild(b);
            res.Buildings.Add(b);
        }

        // Parc à conteneurs : îlots de piles, allées de 14 m, place dégagée autour du départ.
        for (var bx = -190f; bx <= 190f; bx += 38f)
        for (var bz = -80f; bz <= 70f; bz += 34f)
        {
            if (Mathf.Abs(bx) < 40 && Mathf.Abs(bz) < 40) continue; // zone de départ
            if (rng.Randf() < 0.15f) continue;
            for (var row = 0; row < 3; row++)
            {
                var levels = rng.RandiRange(1, 4);
                var size = new Vector3(12.2f, 2.6f * levels, 2.6f);
                var pos = new Vector3(bx, 0, bz + row * 2.9f);
                var mat = ContainerMat(ContainerColors[rng.RandiRange(0, ContainerColors.Length - 1)]);
                var b = Building.Create(id++, pos, size, mat, mat, industrial);
                b.MaxHealth = b.Health = 220 + 120 * levels;
                root.AddChild(b);
                res.Buildings.Add(b);
            }
        }

        // Grues portiques sur le quai.
        for (var i = -1; i <= 1; i++) BuildCrane(root, statics, new Vector3(i * 120, 0, CityBuilder.SeaStart - 18));
        // Cuves à l'est.
        for (var i = 0; i < 4; i++) BuildTank(root, statics, new Vector3(150 + (i % 2) * 34, 0, 95 - (i / 2) * 34), 13f);
        // Mâts d'éclairage.
        for (var x = -180f; x <= 180f; x += 60f)
        for (var z = -120f; z <= 100f; z += 110f)
            BuildMast(root, statics, new Vector3(x, 0, z + 18), night);

        CityBuilder.AddWall(statics, new Vector3(0, 20, -half - 10), new Vector3(half * 2 + 40, 60, 2));
        CityBuilder.AddWall(statics, new Vector3(-half - 10, 20, 0), new Vector3(2, 60, half * 2 + 100));
        CityBuilder.AddWall(statics, new Vector3(half + 10, 20, 0), new Vector3(2, 60, half * 2 + 100));
        CityBuilder.AddWall(statics, new Vector3(0, 20, CityBuilder.SeaStart + 8), new Vector3(half * 2 + 40, 60, 2));

        res.Zones["north"] = new Vector3(0, 0, -125);
        res.Zones["south"] = new Vector3(-60, 0, 125);
        res.Zones["beach"] = new Vector3(40, 0, 135);
        res.Zones["east"] = new Vector3(125, 0, -25);
        res.Zones["west"] = new Vector3(-125, 0, -25);
        res.Zones["center"] = new Vector3(0, 0, -95);
        res.NestSpots.Add(new Vector3(-100, 0, -100));
        res.NestSpots.Add(new Vector3(100, 0, -100));
        res.NestSpots.Add(new Vector3(-100, 0, 100));
        res.NestSpots.Add(new Vector3(100, 0, 100));
        res.PlayerSpawns.Add(new Vector3(-4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(-4, 1, 12));
        res.PlayerSpawns.Add(new Vector3(4, 1, 12));
        DressPort(root, statics, res, night);
        return res;
    }

    /// <summary>Tôle ondulée : bandes verticales (albédo + relief), UV en mètres.</summary>
    private static StandardMaterial3D Corrugated(Color c, int seed)
    {
        const int w = 64, h = 64;
        var img = Image.CreateEmpty(w, h, true, Image.Format.Rgba8);
        var bump = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        var noise = new FastNoiseLite { Seed = seed, Frequency = 0.08f };
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var wave = 0.5f + 0.5f * Mathf.Sin(x / (float)w * Mathf.Tau * 8f);
            var n = noise.GetNoise2D(x, y) * 0.06f;
            var rust = Mathf.Max(0, noise.GetNoise2D(x * 0.5f, y * 2f)) * 0.25f * (y / (float)h);
            img.SetPixel(x, y, (c * (0.88f + wave * 0.12f) + new Color(n, n, n)).Lerp(new Color(0.42f, 0.25f, 0.14f), rust));
            bump.SetPixel(x, y, new Color(wave, wave, wave));
        }
        img.GenerateMipmaps();
        bump.BumpMapToNormalMap(4f);
        bump.GenerateMipmaps();
        return new StandardMaterial3D
        {
            AlbedoTexture = ImageTexture.CreateFromImage(img),
            NormalEnabled = true,
            NormalTexture = ImageTexture.CreateFromImage(bump),
            Roughness = 0.55f,
            Metallic = 0.45f,
            Uv1Scale = new Vector3(2.4f, 1f, 1f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
    }

    private static readonly System.Collections.Generic.Dictionary<Color, StandardMaterial3D> ContainerCache = new();

    private static StandardMaterial3D ContainerMat(Color c)
    {
        if (ContainerCache.TryGetValue(c, out var m)) return m;
        m = Corrugated(c, (int)(c.R * 100 + c.G * 10 + c.B * 1000));
        m.Uv1Scale = new Vector3(4f, 1f, 1f);
        ContainerCache[c] = m;
        return m;
    }

    private static void BuildCrane(Node3D root, StaticBody3D body, Vector3 p)
    {
        var paint = Mats.Painted(new Color(0.85f, 0.55f, 0.1f), 0.5f, 0.4f);
        var dark = Mats.Metal(new Color(0.2f, 0.2f, 0.2f), 0.5f);
        foreach (var dx in new[] { -9f, 9f })
        foreach (var dz in new[] { -7f, 7f })
        {
            root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(1.4f, 34f, 1.4f), Material = paint }, Position = p + new Vector3(dx, 17f, dz) });
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.4f, 34f, 1.4f) }, Position = p + new Vector3(dx, 17f, dz) });
        }
        // Poutres, chariot et conteneur suspendu : chaque boîte visible est solide (on peut marcher sur les poutres).
        void Solid(Vector3 size, Vector3 at, Material m)
        {
            root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size, Material = m }, Position = p + at });
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = p + at });
        }
        Solid(new Vector3(2.5f, 3f, 70f), new Vector3(-9f, 35f, 10f), paint);
        Solid(new Vector3(2.5f, 3f, 70f), new Vector3(9f, 35f, 10f), paint);
        Solid(new Vector3(20f, 2.5f, 3f), new Vector3(0, 33f, -7f), paint);
        Solid(new Vector3(20f, 2.5f, 3f), new Vector3(0, 33f, 7f), paint);
        Solid(new Vector3(6f, 5f, 6f), new Vector3(0, 31f, 22f), Mats.Painted(new Color(0.9f, 0.9f, 0.88f), 0.5f)); // chariot
        root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 20f, Material = dark }, Position = p + new Vector3(0, 18.5f, 22f) });
        Solid(new Vector3(12.2f, 2.6f, 2.6f), new Vector3(0, 7.5f, 22f), ContainerMat(ContainerColors[1])); // conteneur suspendu
    }

    private static void BuildTank(Node3D root, StaticBody3D body, Vector3 p, float r)
    {
        var white = Mats.Painted(new Color(0.86f, 0.86f, 0.84f), 0.45f, 0.3f);
        root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = r, BottomRadius = r, Height = 16f, RadialSegments = 32, Material = white }, Position = p + new Vector3(0, 8f, 0) });
        root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = r * 0.2f, BottomRadius = r, Height = 2.5f, RadialSegments = 32, Material = white }, Position = p + new Vector3(0, 17.2f, 0) });
        root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = r + 0.05f, BottomRadius = r + 0.05f, Height = 0.6f, RadialSegments = 32, Material = Mats.Painted(new Color(0.7f, 0.15f, 0.1f), 0.6f) }, Position = p + new Vector3(0, 12f, 0) });
        // Fût, ceinture rouge et toit conique : mêmes volumes que le visuel (le toit n'est pas un cylindre plein).
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = r, Height = 16f }, Position = p + new Vector3(0, 8f, 0) });
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = r + 0.05f, Height = 0.6f }, Position = p + new Vector3(0, 12f, 0) });
        body.AddChild(new CollisionShape3D
        {
            Shape = new CylinderMesh { TopRadius = r * 0.2f, BottomRadius = r, Height = 2.5f, RadialSegments = 32 }.CreateConvexShape(),
            Position = p + new Vector3(0, 17.2f, 0),
        });
    }

    private static void BuildMast(Node3D root, StaticBody3D body, Vector3 p, bool night)
    {
        var metal = Mats.Metal(new Color(0.4f, 0.42f, 0.44f), 0.5f);
        root.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.35f, Height = 24f, RadialSegments = 8, Material = metal }, Position = p + new Vector3(0, 12f, 0) });
        root.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(3f, 0.8f, 0.8f), Material = night ? Mats.Glow(new Color(1f, 0.92f, 0.75f), 5f) : metal }, Position = p + new Vector3(0, 24.2f, 0) });
        if (night)
            root.AddChild(new OmniLight3D { Position = p + new Vector3(0, 22f, 0), LightColor = new Color(1f, 0.9f, 0.72f), LightEnergy = 4f, OmniRange = 45f, OmniAttenuation = 1.1f });
        body.AddChild(new CollisionShape3D
        {
            Shape = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.35f, Height = 24f, RadialSegments = 8 }.CreateConvexShape(),
            Position = p + new Vector3(0, 12f, 0),
        });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3f, 0.8f, 0.8f) }, Position = p + new Vector3(0, 24.2f, 0) });
    }
}
