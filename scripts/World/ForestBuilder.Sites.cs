using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Lieux de la vallée : maison forestière et son garage (aire de départ, bivouac de l'armée), camp de bûcherons sur le
/// replat est (baraque, hangars ouverts, grumes, grumiers), centrale hydroélectrique au pied du barrage (salle des
/// machines en brique, poste de transformation, ligne à haute tension), mirador de l'affût sur la rive ouest.
/// Immeubles posés dans un ordre fixe (identifiants identiques chez tous les pairs).
/// </summary>
public static partial class ForestBuilder
{
    private static readonly Rect2 CampYard = new(100, -142, 44, 36);
    private static readonly Rect2 PlantYard = new(-74, -198, 44, 34);
    private static readonly Rect2 LookoutYard = new(-186, 118, 18, 18);

    private static void BuildSites(Ctx c)
    {
        BuildStation(c);
        BuildCamp(c);
        BuildPlant(c);
        BuildLookout(c);
        BuildPowerLine(c);
    }

    // ───────────────────────── Outils ─────────────────────────

    private static Building AddHouse(Ctx c, Vector3 center, Vector3 size, BuildingStyle st, float hp, Material facade = null)
    {
        st.Kind = BuildingKind.House;
        st.Night = c.Night;
        var b = Building.Create(c.NextId++, center, size, facade ?? Mats.Detail(), Mats.RoofTiles(), st);
        b.MaxHealth = b.Health = hp;
        c.Root.AddChild(b);
        c.Res.Buildings.Add(b);
        Take(c, center, size.X / 2 + 1.5f, size.Z / 2 + 1.5f);
        return b;
    }

    /// <summary>Bâtiment au maillage sur mesure : boîte pleine + forme exacte de ce qui dépasse, ou ouvert (collision exacte seule).</summary>
    private static Building AddCustom(Ctx c, Vector3 pos, Vector3 size, ArrayMesh mesh, Material facade, float hpMul, bool open)
    {
        var b = Building.Create(c.NextId++, pos, size, facade, Mats.Roof(), new BuildingStyle { Kind = BuildingKind.Industrial });
        b.Visual.Mesh = mesh;
        if (open)
            foreach (var ch in b.GetChildren())
                if (ch is CollisionShape3D cs) { b.RemoveChild(cs); cs.Free(); }
        if (MeshKit.ShapeOf(mesh) is { } detail) b.AddChild(new CollisionShape3D { Name = "Detail", Shape = detail });
        b.Open = open;
        b.MaxHealth = b.Health = b.MaxHealth * hpMul;
        c.Root.AddChild(b);
        c.Res.Buildings.Add(b);
        Take(c, pos, size.X / 2 + 1.5f, size.Z / 2 + 1.5f);
        return b;
    }

    /// <summary>Accessoire posé au sol (le plus bas du disque de rayon r, pour ne jamais flotter).</summary>
    private static int Put(Ctx c, Mesh mesh, float x, float z, float yaw, float r = 1f, float scale = 1f, Color? col = null, bool shadows = true, float range = 160f, float sink = 0.05f)
    {
        var p = new Vector3(x, MinGround(c, new Vector3(x, 0, z), r) - sink, z);
        return c.Props.Add(mesh, p, yaw, col, range, shadows, scale);
    }

    /// <summary>Façade : bande de mur avec UV en mètres (u = longueur / uLen, v vers le bas).</summary>
    private static void Band(MeshKit k, Material m, Vector3 a, Vector3 b, float y0, float y1, Vector3 n, float uLen, float vLen, Color col)
    {
        var len = (b - a).Length();
        k.QuadC(m, a with { Y = y0 }, b with { Y = y0 }, b with { Y = y1 }, a with { Y = y1 },
            new Vector2(0, (y1 - y0) / vLen), new Vector2(len / uLen, (y1 - y0) / vLen), new Vector2(len / uLen, 0), new Vector2(0, 0),
            col, col, col * 0.85f, col * 0.85f, n);
    }

    private static (Vector3 start, Vector3 along, Vector3 n, float len)[] Faces4(float hx, float hz) => new[]
    {
        (new Vector3(-hx, 0, hz), Vector3.Right, Vector3.Back, hx * 2),
        (new Vector3(hx, 0, -hz), Vector3.Left, Vector3.Forward, hx * 2),
        (new Vector3(hx, 0, hz), Vector3.Forward, Vector3.Right, hz * 2),
        (new Vector3(-hx, 0, -hz), Vector3.Back, Vector3.Left, hz * 2),
    };

    // ───────────────────────── Maison forestière ─────────────────────────

    private static void BuildStation(Ctx c)
    {
        var y = c.TerraceY + 0.4f;
        AddHouse(c, new Vector3(51, y, 3), new Vector3(12, 6, 9.5f), new BuildingStyle
        {
            Use = HouseUse.Home, Roof = RoofShape.Gable, Facing = 3, RoofPitch = 0.95f,
            WallColor = new Color(0.5f, 0.48f, 0.43f), RoofColor = new Color(0.16f, 0.17f, 0.18f), TrimColor = new Color(0.2f, 0.25f, 0.2f),
        }, 1500f, Mats.Masonry());
        AddHouse(c, new Vector3(51, y, -10), new Vector3(7, 3.4f, 6.5f), new BuildingStyle
        {
            Use = HouseUse.Garage, Roof = RoofShape.Gable, Facing = 3, RoofPitch = 0.6f,
            WallColor = new Color(0.32f, 0.26f, 0.2f), RoofColor = new Color(0.2f, 0.19f, 0.18f), TrimColor = new Color(0.25f, 0.22f, 0.18f),
        }, 700f);
        // Bois de chauffage, véhicule de service, panneau d'accueil, bivouac de l'armée en bordure de l'aire de départ.
        Put(c, Props.LogPile(0), 59.5f, 14f, 0f, 3f, 0.55f);
        Put(c, Props.Vehicle(Props.VehicleType.Van, new Color(0.24f, 0.3f, 0.24f), 5.2f), 43f, 16f, 0.2f, 2.5f);
        Put(c, Props.Bench(), 44f, -3f, Mathf.Pi / 2, 1f);
        Put(c, Props.Bench(), 44f, 9f, Mathf.Pi / 2, 1f);
        SignBoard(c, new Vector3(13, 0, 49.5f), 0.2f);
        Put(c, Props.MilTruck(), -24f, -39.5f, Mathf.Pi / 2, 3f);
        Put(c, Props.MilTruck(), -6f, -39.5f, Mathf.Pi / 2, 3f);
        for (var i = 0; i < 6; i++) Put(c, Props.HescoWall(i % 3 == 1 ? 1 : 0), 14f + i * Props.HescoLen, -40.5f, 0f, 1.6f);
        Put(c, Props.AmmoCrates(0), 35f, -38f, 0.3f, 1f);
        Put(c, Props.AmmoCrates(1), 38.5f, -36.5f, 1.2f, 1f);
        Put(c, Props.JerseyBarrier(), 26f, 47.5f, 0.1f, 1.5f);
        Put(c, Props.JerseyBarrier(), -14f, 48f, -0.1f, 1.5f);
        if (c.Time != TimeOfDay.Day)
        {
            // Lampe de la véranda et projecteur du bivouac.
            c.Root.AddChild(new OmniLight3D { Position = new Vector3(44.6f, y + 3f, 3f), LightColor = new Color(1f, 0.72f, 0.45f), LightEnergy = 1.6f, OmniRange = 16f, ShadowEnabled = false });
            c.Root.AddChild(new OmniLight3D { Position = new Vector3(-15f, c.TerraceY + 6f, -37f), LightColor = new Color(0.85f, 0.88f, 1f), LightEnergy = 2.2f, OmniRange = 30f, ShadowEnabled = false });
        }
    }

    /// <summary>Panneau de bois sur deux poteaux (« Poste forestier »), sans collision notable.</summary>
    private static void SignBoard(Ctx c, Vector3 at, float yaw)
    {
        var p = OnGround(c, at.X, at.Z);
        var k = c.Solid.At(p);
        k.Xf = new Transform3D(new Basis(Vector3.Up, yaw), p);
        var wood = Mats.Detail();
        k.Col = new Color(0.25f, 0.19f, 0.13f);
        foreach (var x in new[] { -1.1f, 1.1f }) k.Box(wood, new Vector3(x, 1.1f, 0), new Vector3(0.14f, 2.2f, 0.14f));
        k.Col = new Color(0.2f, 0.26f, 0.19f);
        k.Box(wood, new Vector3(0, 1.75f, 0), new Vector3(2.6f, 0.9f, 0.08f));
        k.Col = new Color(0.62f, 0.6f, 0.52f);
        k.Solid = false;
        for (var i = 0; i < 3; i++) k.Box(wood, new Vector3(-0.2f + (i - 1) * 0.15f, 1.9f - i * 0.2f, 0.045f), new Vector3(1.6f - i * 0.3f, 0.07f, 0.01f));
        k.Solid = true;
        k.Xf = Transform3D.Identity;
    }

    // ───────────────────────── Camp de bûcherons ─────────────────────────

    private static void BuildCamp(Ctx c)
    {
        var y = PadHeight(CampYard);
        AddHouse(c, new Vector3(106, y, -135), new Vector3(8.5f, 3f, 6f), new BuildingStyle
        {
            Use = HouseUse.Home, Roof = RoofShape.Gable, Facing = 0, RoofPitch = 0.55f,
            WallColor = new Color(0.27f, 0.21f, 0.16f), RoofColor = new Color(0.22f, 0.21f, 0.2f), TrimColor = new Color(0.18f, 0.16f, 0.13f),
        }, 700f);
        var shed = new Vector3(14f, 5f, 7f);
        AddCustom(c, new Vector3(126, y, -136), shed, ShedMesh(shed, 31), Mats.Detail(), 0.6f, true);
        var shed2 = new Vector3(11f, 4.6f, 7f);
        AddCustom(c, new Vector3(139, y, -116), shed2, ShedMesh(shed2, 32), Mats.Detail(), 0.6f, true);
        // Grumes sous les hangars et dans la cour, grumiers, fûts, rémanents.
        Put(c, Props.LogPile(1), 126f, -136.5f, 0f, 3f);
        Put(c, Props.LogPile(2), 139f, -116.5f, 0.02f, 3f, 0.85f);
        Put(c, Props.LogPile(0), 121f, -128.5f, 0.1f, 3f);
        Put(c, Props.LogPile(1), 131f, -122f, 1.5f, 3f, 1.1f);
        Put(c, Props.LogTrailer(), 140f, -127.5f, 1.5f, 3f);
        Put(c, Props.Vehicle(Props.VehicleType.Truck, new Color(0.42f, 0.3f, 0.13f), 7.5f), 116f, -131f, 0.6f, 3f);
        Put(c, Props.Vehicle(Props.VehicleType.Truck, new Color(0.3f, 0.32f, 0.3f), 7.2f), 140f, -136.5f, 1.5f, 3f);
        for (var i = 0; i < 5; i++) Put(c, Props.Drum(i % 3), 103.5f + (i % 3) * 0.75f, -128f + (i / 3) * 0.8f, i * 0.7f, 0.5f);
        Put(c, Props.SlashPile(0), 144f, -138f, 0.4f, 2f, 1f, null, false);
        Put(c, Props.SlashPile(1), 101f, -111f, 1.9f, 2f, 1f, null, false);
        if (c.Time != TimeOfDay.Day)
            c.Root.AddChild(new OmniLight3D { Position = new Vector3(110f, y + 3.4f, -131.5f), LightColor = new Color(1f, 0.62f, 0.32f), LightEnergy = 1.4f, OmniRange = 18f, ShadowEnabled = false });
    }

    /// <summary>Hangar ouvert de bûcherons : poteaux et pannes en bois, toit de tôle en appentis, bardage arrière.</summary>
    private static ArrayMesh ShedMesh(Vector3 s, int seed)
    {
        var k = new MeshKit { Record = true };
        var wood = Mats.Detail();
        var roof = Mats.Corrugated(new Color(0.3f, 0.27f, 0.24f), seed, 2.4f);
        var hx = s.X / 2;
        var hz = s.Z / 2;
        float H(float z) => Mathf.Lerp(s.Y - 1f, s.Y, (hz - z) / (2f * hz)); // appentis : haut à l'arrière (-Z)
        k.Col = new Color(0.3f, 0.23f, 0.16f);
        for (var x = -hx + 0.2f; x <= hx - 0.1f; x += (s.X - 0.4f) / Mathf.Max(1, Mathf.RoundToInt(s.X / 3.5f)))
            foreach (var z in new[] { -hz + 0.2f, hz - 0.2f })
                k.Box(wood, new Vector3(x, H(z) / 2f, z), new Vector3(0.24f, H(z), 0.24f));
        foreach (var z in new[] { -hz + 0.2f, hz - 0.2f })
            k.Box(wood, new Vector3(0, H(z) - 0.15f, z), new Vector3(s.X, 0.3f, 0.2f));
        for (var x = -hx + 1f; x < hx; x += 2.2f)
            k.Box(wood, new Vector3(x, s.Y - 0.6f, 0), new Vector3(0.14f, 0.18f, s.Z + 0.4f), new Basis(Vector3.Right, Mathf.Atan2(1f, s.Z)));
        // Toit (tôle) : légèrement débordant.
        k.Col = Colors.White;
        var a = new Vector3(-hx - 0.4f, H(-hz) + 0.12f, -hz - 0.6f);
        var b = new Vector3(hx + 0.4f, H(-hz) + 0.12f, -hz - 0.6f);
        var cc = new Vector3(hx + 0.4f, H(hz) + 0.12f - 0.1f, hz + 0.6f);
        var d = new Vector3(-hx - 0.4f, H(hz) + 0.12f - 0.1f, hz + 0.6f);
        k.QuadC(roof, a, b, cc, d, new Vector2(0, 0), new Vector2(s.X / 2.4f, 0), new Vector2(s.X / 2.4f, s.Z / 2.4f), new Vector2(0, s.Z / 2.4f),
            Colors.White, Colors.White, Colors.White, Colors.White, Vector3.Up);
        k.QuadC(roof, a, b, cc, d, new Vector2(0, 0), new Vector2(s.X / 2.4f, 0), new Vector2(s.X / 2.4f, s.Z / 2.4f), new Vector2(0, s.Z / 2.4f),
            new Color(0.5f, 0.5f, 0.5f), new Color(0.5f, 0.5f, 0.5f), new Color(0.5f, 0.5f, 0.5f), new Color(0.5f, 0.5f, 0.5f), Vector3.Down);
        // Bardage arrière et pignons (planches verticales disjointes).
        var r = new RandomNumberGenerator { Seed = (ulong)seed };
        for (var x = -hx; x < hx - 0.1f; x += 0.32f)
        {
            if (r.Randf() < 0.08f) continue; // planche manquante
            k.Col = new Color(0.3f, 0.24f, 0.18f) * r.RandfRange(0.75f, 1.1f);
            k.Box(wood, new Vector3(x + 0.15f, H(-hz) / 2f, -hz + 0.05f), new Vector3(0.28f, H(-hz), 0.05f));
        }
        foreach (var sx in new[] { -1f, 1f })
        for (var z = -hz; z < -hz + s.Z * 0.6f; z += 0.32f)
        {
            k.Col = new Color(0.3f, 0.24f, 0.18f) * r.RandfRange(0.75f, 1.1f);
            k.Box(wood, new Vector3(sx * (hx - 0.05f), H(z) / 2f, z + 0.15f), new Vector3(0.05f, H(z), 0.28f));
        }
        return k.Commit();
    }

    // ───────────────────────── Centrale ─────────────────────────

    private static void BuildPlant(Ctx c)
    {
        var y = PadHeight(PlantYard);
        var size = new Vector3(26f, 13f, 14f);
        AddCustom(c, new Vector3(-60f, y, PlantZ), size, PowerhouseMesh(size, c.Night), Mats.FactoryBrick(), 1.3f, false);
        // Poste de transformation : transformateurs, portiques, clôture grillagée, mât d'éclairage.
        foreach (var x in new[] { -42f, -36f, -30f }) Put(c, Props.Transformer(), x, -172f, 0f, 2f);
        var k = c.Solid.At(new Vector3(-36, y, -172));
        var steel = Mats.DetailMetal();
        k.Col = new Color(0.4f, 0.41f, 0.4f);
        foreach (var x in new[] { -45f, -27f })
        foreach (var z in new[] { -176f, -168f })
            k.Tube(steel, new Vector3(x, y, z), new Vector3(x, y + 9f, z), 0.14f, 6, true);
        foreach (var x in new[] { -45f, -27f }) k.Tube(steel, new Vector3(x, y + 9f, -176f), new Vector3(x, y + 9f, -168f), 0.12f, 6, true);
        foreach (var z in new[] { -176f, -168f }) k.Tube(steel, new Vector3(-45f, y + 9f, z), new Vector3(-27f, y + 9f, z), 0.12f, 6, true);
        for (var x = -46f; x <= -26f; x += 2.6f)
        {
            Put(c, Props.ChainFence((int)(x * 7) & 1), x + 1.3f, -179.5f, 0f, 0.5f);
            Put(c, Props.ChainFence((int)(x * 5) & 1), x + 1.3f, -164.5f, 0f, 0.5f);
        }
        Put(c, Props.FloodMast(0), -24f, -180f, 0.4f, 1f);
        Put(c, Props.Drum(0), -48f, -170f, 0.2f, 0.5f);
        Put(c, Props.Drum(2), -48.8f, -169.3f, 1.2f, 0.5f);
        Put(c, Props.MilJeepWreck(), -31f, -186f, 2.2f, 2.5f);
        if (c.Time != TimeOfDay.Day)
            c.Root.AddChild(new OmniLight3D { Position = new Vector3(-46f, y + 8f, -176.5f), LightColor = new Color(1f, 0.58f, 0.25f), LightEnergy = 2.2f, OmniRange = 26f, ShadowEnabled = false });
    }

    /// <summary>
    /// Salle des machines : socle de béton, brique, hautes baies vitrées cintrées (carreaux brisés), corniche, toit plat
    /// à acrotère, porte roulante en pignon est, édicules de ventilation, sortie des conduites forcées à l'arrière.
    /// </summary>
    private static ArrayMesh PowerhouseMesh(Vector3 s, bool night)
    {
        var k = new MeshKit { Record = true };
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var h = s.Y;
        var brick = Mats.FactoryBrick();
        var glazing = Mats.FactoryGlazing();
        var concrete = Mats.Detail();
        var metal = Mats.DetailMetal();
        var concreteCol = new Color(0.55f, 0.54f, 0.5f);
        var brickCol = new Color(0.44f, 0.3f, 0.24f);
        foreach (var (start, along, n, len) in Faces4(hx, hz))
        {
            Band(k, concrete, start, start + along * len, 0f, 1.6f, n, 3f, 3f, concreteCol);
            Band(k, brick, start, start + along * len, 1.6f, h, n, 3f, 3f, brickCol);
            var off = n * 0.05f;
            var gable = Mathf.Abs(n.X) > 0.5f;
            var bays = Mathf.Max(1, Mathf.RoundToInt(len / 5.2f));
            var bw = len / bays;
            for (var i = 0; i < bays; i++)
            {
                var a = start + along * (i * bw + 0.9f) + off;
                var b = start + along * ((i + 1) * bw - 0.9f) + off;
                if (gable && n.X > 0 && i == bays / 2)
                {
                    // Porte roulante de la salle des machines (est).
                    Band(k, Mats.Corrugated(new Color(0.32f, 0.33f, 0.31f), 71, 4f), a, b, 0.05f, 6.5f, n, 3f, 3f, new Color(0.7f, 0.68f, 0.64f));
                    continue;
                }
                if (gable && n.X < 0) continue; // pignon ouest aveugle
                if (n.Z < 0 && Mathf.Abs((a + b).X / 2f + 9f) < 6f) continue; // arrière : arrivée des conduites
                Band(k, glazing, a, b, 3.2f, h - 2.4f, n, 3f, 3f, night ? new Color(0.55f, 0.5f, 0.42f) : new Color(0.9f, 0.9f, 0.88f));
                k.Col = concreteCol;
                k.Box(concrete, (a + b) / 2 + Vector3.Up * 3.05f + n * 0.12f, new Vector3(Mathf.Abs(along.X) * (bw - 1.4f) + Mathf.Abs(along.Z) * 0.3f, 0.3f, Mathf.Abs(along.Z) * (bw - 1.4f) + Mathf.Abs(along.X) * 0.3f));
                // Arc de décharge en briques au-dessus de la baie.
                k.Col = brickCol * 0.7f;
                k.Box(brick, (a + b) / 2 + Vector3.Up * (h - 2.15f) + n * 0.1f, new Vector3(Mathf.Abs(along.X) * (bw - 1.2f) + Mathf.Abs(along.Z) * 0.25f, 0.5f, Mathf.Abs(along.Z) * (bw - 1.2f) + Mathf.Abs(along.X) * 0.25f));
            }
            // Pilastres.
            k.Col = brickCol * 0.78f;
            for (var i = 0; i <= bays; i++)
            {
                var p = start + along * Mathf.Clamp(i * bw, 0.45f, len - 0.45f) + n * 0.2f;
                k.Box(brick, p + Vector3.Up * h / 2, new Vector3(Mathf.Abs(along.X) * 0.9f + Mathf.Abs(along.Z) * 0.4f, h, Mathf.Abs(along.Z) * 0.9f + Mathf.Abs(along.X) * 0.4f));
            }
        }
        // Corniche, toit, acrotère, édicules.
        k.Col = concreteCol * 0.9f;
        k.Ring(concrete, hx, hz, h - 0.6f, h + 0.9f, 0.5f, 0.3f);
        k.Col = new Color(0.28f, 0.27f, 0.26f);
        k.OrientedQuad(concrete, new Vector3(-hx, h, hz), new Vector3(hx, h, hz), new Vector3(hx, h, -hz), new Vector3(-hx, h, -hz), Vector3.Up);
        k.Col = new Color(0.36f, 0.35f, 0.33f);
        k.Box(metal, new Vector3(-6f, h + 1f, 0f), new Vector3(3f, 2f, 3f));
        k.Box(metal, new Vector3(5f, h + 0.8f, -2f), new Vector3(2.2f, 1.6f, 2.2f));
        // Portique du pont roulant visible par les baies : poutre sombre.
        k.Solid = false;
        k.Col = new Color(0.12f, 0.1f, 0.08f);
        k.Box(metal, new Vector3(0, h - 3f, 0), new Vector3(s.X - 2f, 0.8f, 0.8f));
        k.Solid = true;
        return k.Commit();
    }

    // ───────────────────────── Affût, ligne électrique ─────────────────────────

    private static void BuildLookout(Ctx c)
    {
        var at = LookoutYard.GetCenter();
        Put(c, Props.Watchtower(), at.X, at.Y, 0.6f, 2.5f, 1f, null, true, 220f);
        Put(c, Props.LogPile(2), at.X + 6f, at.Y - 4f, 2.2f, 3f, 0.6f);
        Put(c, Props.Bench(), at.X - 4f, at.Y + 3f, 0.4f, 1f);
        Take(c, new Vector3(at.X, 0, at.Y), 5f, 5f);
    }

    /// <summary>Pylônes de la ligne qui part de la centrale vers l'est (au-delà de la carte) et câbles en chaînette.</summary>
    private static void BuildPowerLine(Ctx c)
    {
        var spots = new List<Vector3>();
        foreach (var (x, z) in new[] { (-14f, -160f), (40f, -166f), (96f, -158f), (150f, -163f), (205f, -157f), (262f, -161f), (330f, -165f), (400f, -158f) })
        {
            var p = new Vector3(x, MinGround(c, new Vector3(x, 0, z), 3.5f) - 0.3f, z);
            if (Mathf.Abs(x) < 256f) Put(c, Props.Pylon(), x, z, Mathf.Pi / 2f, 3.5f, 1f, null, true, 600f, 0.3f);
            else c.Props.Add(Props.Pylon(), new Vector3(x, Raw(x, z) - 0.3f, z), Mathf.Pi / 2f, null, 900f, false);
            if (Mathf.Abs(x) >= 256f) p.Y = Raw(x, z) - 0.3f;
            spots.Add(p);
            if (Mathf.Abs(x) < 256f) Take(c, p, 4f, 4f);
        }
        var cable = Mats.DetailMetal();
        for (var i = 0; i + 1 < spots.Count; i++)
        {
            var a = spots[i];
            var b = spots[i + 1];
            var k = c.Ground.At((a + b) / 2f);
            k.Col = new Color(0.08f, 0.08f, 0.08f);
            foreach (var w in Props.PylonWires)
            {
                // Pylône tourné de 90° : la console est le long de z.
                var wa = a + new Vector3(0, w.Y, -w.X);
                var wb = b + new Vector3(0, w.Y, -w.X);
                const int segs = 10;
                var prev = wa;
                for (var s = 1; s <= segs; s++)
                {
                    var t = s / (float)segs;
                    var p = wa.Lerp(wb, t) + Vector3.Down * (4f * t * (1f - t) * 4.5f);
                    k.Tube(cable, prev, p, 0.035f, 3);
                    prev = p;
                }
            }
        }
    }
}
