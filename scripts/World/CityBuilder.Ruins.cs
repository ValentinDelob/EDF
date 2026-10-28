using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Ville côtière envahie : épaves calcinées, carambolage, bus encastré, barrages militaires abandonnés,
/// gravats au pied des immeubles, débris, cratères, flaques d'hémolymphe, incendies, toiles et œufs.
/// Tout est placé avec des générateurs à graine fixe ou des hachages de position (identique chez tous les pairs) ;
/// rien ici ne tire dans les générateurs historiques (identifiants d'immeubles inchangés).
/// </summary>
public static partial class CityBuilder
{
    private static RuinKit _ruins;

    private static void BeginRuins(Node3D root, StaticBody3D statics, bool night)
    {
        _ruins = new RuinKit(root, statics, 0xC17E, night);
        // Aire de départ des véhicules et des joueurs : rien de solide.
        _ruins.Block(new Rect2(-30, -30, 60, 60));
    }

    /// <summary>Immeubles encore alimentés la nuit (groupe électrogène) : une minorité.</summary>
    private static bool Powered(int id) => RuinKit.Hash(id, 7, 1) < 0.16f;

    /// <summary>Hauteur du sol : trottoirs des pâtés et promenade à 0,15 m, pelouse du square, chaussée à 0.</summary>
    private static float GroundY(float x, float z)
    {
        if (z >= BeachStart) return -0.02f;
        if (z > BlvdSouth) return 0.15f;
        if (z >= BlvdNorth - 0.5f) return 0f;
        var lx = Mathf.PosMod(x + Half, RoadEvery) - RoadEvery / 2;
        var lz = Mathf.PosMod(z + Half, RoadEvery) - RoadEvery / 2;
        const float h = (RoadEvery - RoadWidth) / 2;
        if (Mathf.Abs(lx) >= h || Mathf.Abs(lz) >= h) return 0f;
        var bx = x - lx;
        var bz = z - lz;
        if (Mathf.Abs(bx) < 30 && Mathf.Abs(bz) < 30 && Mathf.Abs(lx) < h - 2 && Mathf.Abs(lz) < h - 2) return 0.23f; // pelouse
        return 0.15f;
    }

    private static Vector3 OnGround(float x, float z) => new(x, GroundY(x, z), z);

    // ───────────────────────── Remplacements (appelés pendant la génération) ─────────────────────────

    /// <summary>
    /// Véhicule garé : une bonne part est remplacée par une épave calcinée ; certains restent intacts,
    /// portière ouverte (fuite précipitée). Renvoie vrai si l'épave remplace le véhicule.
    /// </summary>
    private static bool RuinVehicle(Node3D root, Vector3 p, float yaw, Props.VehicleType type, ref Color paint, float len)
    {
        var r = _ruins;
        if (r == null) return false;
        // Carrosseries ternies par la cendre et la poussière (teintes groupées : peu de matériaux).
        paint = paint.Lerp(new Color(0.33f, 0.32f, 0.3f), 0.55f).Darkened(0.12f);
        var fate = RuinKit.Hash(p.X, p.Z, 77);
        r.Take(p, Props.VehicleSize(type, len).Z * 0.5f);
        if (fate < 0.52f)
        {
            var v = (int)(RuinKit.Hash(p.X, p.Z, 78) * 4);
            PlaceWreck(p, yaw + (RuinKit.Hash(p.X, p.Z, 79) - 0.5f) * 0.12f, type, v, 0, fate < 0.012f ? 1 : 0);
            return true;
        }
        if (fate < 0.66f && type == Props.VehicleType.Car)
        {
            // Portière conducteur ouverte (côté gauche, charnière à l'avant).
            var b = new Basis(Vector3.Up, yaw);
            var hinge = p + b * new Vector3(-0.92f, 0, len * 0.18f);
            r.Inst(Props.CarDoor(paint), new Transform3D(new Basis(Vector3.Up, yaw - 1.0f), hinge), true, 160f);
        }
        return false;
    }

    /// <summary>Lampadaire : abattu (renvoie vrai) ou hors service la nuit (<paramref name="night"/> passe à faux).</summary>
    private static bool RuinLamp(Vector3 p, float yaw, ref bool night)
    {
        var r = _ruins;
        if (r == null) return false;
        var fate = RuinKit.Hash(p.X, p.Z, 81);
        if (fate < 0.2f)
        {
            // Couché en travers du trottoir et de la chaussée.
            var fall = yaw + (RuinKit.Hash(p.X, p.Z, 82) - 0.5f) * 1.6f;
            var basis = new Basis(Vector3.Up, fall) * new Basis(Vector3.Right, Mathf.Pi / 2 - 0.04f) * new Basis(Vector3.Up, Mathf.Pi / 2);
            r.Inst(Props.StreetLamp(false), new Transform3D(basis, p + Vector3.Up * 0.17f), true, 260f);
            var dir = new Basis(Vector3.Up, fall) * Vector3.Back;
            r.Box(new Transform3D(new Basis(Vector3.Up, fall), p + dir * 3.6f + Vector3.Up * 0.18f), new Vector3(0.32f, 0.32f, 7.2f));
            r.Kit(RuinKit.Layer.Solid, p).Col = new Color(0.24f, 0.26f, 0.27f);
            r.Kit(RuinKit.Layer.Solid, p).Cylinder(Mats.DetailMetal(), p, 0.16f, 0.13f, 0.35f, 8); // moignon de pied
            return true;
        }
        if (night && fate < 0.62f) night = false;
        return false;
    }

    /// <summary>Feu tricolore éteint (plus de courant), parfois penché ou abattu.</summary>
    private static void AddTrafficLight(PropScatter props, bool green, Vector3 p, float yaw, float range)
    {
        var fate = RuinKit.Hash(p.X, p.Z, 83);
        var r = _ruins;
        if (r != null && fate < 0.16f)
        {
            var fall = yaw + (fate - 0.08f) * 12f;
            var basis = new Basis(Vector3.Up, fall) * new Basis(Vector3.Right, Mathf.Pi / 2 - 0.05f);
            r.Inst(Props.TrafficLightDead(), new Transform3D(basis, p + Vector3.Up * 0.1f), true, range);
            r.Box(new Transform3D(new Basis(Vector3.Up, fall), p + new Basis(Vector3.Up, fall) * new Vector3(0, 0.25f, 1.8f)), new Vector3(0.6f, 0.5f, 3.6f));
            return;
        }
        if (r != null && fate < 0.3f)
        {
            var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, 0.25f + fate);
            r.Inst(Props.TrafficLightDead(), new Transform3D(basis, p), true, range);
            return;
        }
        props.Add(Props.TrafficLightDead(), p, yaw, range: range);
    }

    /// <summary>Arbre du square ou de la place : mort (sans feuilles) une fois sur trois.</summary>
    private static ArrayMesh RuinTree(Vector3 p) =>
        RuinKit.Hash(p.X, p.Z, 85) < 0.38f ? Props.DeadTree((int)(RuinKit.Hash(p.X, p.Z, 86) * 4)) : null;

    private static ArrayMesh RuinPalm(Vector3 p, float hgt) =>
        RuinKit.Hash(p.X, p.Z, 87) < 0.5f ? Props.BurntPalm(hgt) : null;

    /// <summary>Plage : parasols renversés, serviettes ternies, transats couchés.</summary>
    private static void BeachLeftovers(PropScatter props, Vector3 p, float yaw, Color col, Vector3 tp, float tyaw, bool towel, Color tcol)
    {
        var r = _ruins;
        var dull = col.Lerp(new Color(0.35f, 0.33f, 0.3f), 0.55f);
        var fate = RuinKit.Hash(p.X, p.Z, 88);
        if (r != null && fate < 0.65f)
        {
            // Parasol arraché, couché dans le sable.
            var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, 1.35f);
            r.Inst(Props.UmbrellaPole(), new Transform3D(basis, p + Vector3.Up * 0.3f), false, 220f);
            r.Inst(Props.UmbrellaTop(), new Transform3D(basis, p + Vector3.Up * 0.3f), true, 260f, dull);
        }
        else
        {
            props.Add(Props.UmbrellaPole(), p, yaw, range: 220);
            props.Add(Props.UmbrellaTop(), p, yaw, dull, range: 260, shadows: true);
        }
        if (towel)
        {
            if (fate > 0.3f) props.Add(Props.Towel(), tp, tyaw, tcol.Lerp(new Color(0.3f, 0.28f, 0.25f), 0.6f), range: 140);
        }
        else if (r != null)
            r.Inst(Props.Lounger(), new Transform3D(new Basis(Vector3.Up, tyaw) * new Basis(Vector3.Back, Mathf.Pi * 0.95f), tp + Vector3.Up * 0.62f), true, 140f);
    }

    /// <summary>
    /// Terrain vague : une fois sur deux, carcasse d'immeuble éventré (murs déchiquetés, planchers effondrés,
    /// tas de gravats intérieur, incendie). Renvoie vrai si la carcasse remplace l'aménagement prévu.
    /// </summary>
    private static bool RuinShell(Rect2 lot)
    {
        var r = _ruins;
        if (r == null) return false;
        var c2 = lot.GetCenter();
        if (RuinKit.Hash(c2.X, c2.Y, 95) > 0.55f || (Mathf.Abs(c2.X) < 40 && Mathf.Abs(c2.Y) < 40)) return false;
        var inset = 2.4f;
        var size = lot.Size - Vector2.One * inset * 2;
        if (size.X < 8 || size.Y < 8) return false;
        var center = new Vector3(c2.X, 0.15f, c2.Y);
        var seed = (int)(Mathf.Abs(c2.X) * 7 + Mathf.Abs(c2.Y) * 3);
        var height = 3.2f * (3 + (int)(RuinKit.Hash(c2.X, c2.Y, 96) * 3)) + 0.6f;
        var wall = RuinKit.Hash(c2.X, c2.Y, 97) < 0.35f ? new Color(0.42f, 0.28f, 0.22f) : new Color(0.48f, 0.47f, 0.44f);
        var boxes = new List<(Transform3D, Vector3)>();
        Props.RuinedShell(r.Kit(RuinKit.Layer.Solid, center), center, size, height, seed, wall, boxes);
        foreach (var (xf, s) in boxes) r.Box(xf, s);
        // Gravats intérieurs, débordant par le pan effondré.
        // Plusieurs tas qui se chevauchent : relief irrégulier plutôt qu'un dôme.
        var pile = new Vector3(size.X * 0.5f, Mathf.Min(3.6f, height * 0.25f), size.Y * 0.5f);
        for (var i = 0; i < 4; i++)
        {
            var off = new Vector3((i % 2 - 0.5f) * size.X * 0.42f, 0, (i / 2 - 0.5f) * size.Y * 0.42f);
            var ps = pile * (0.7f + 0.5f * RuinKit.Hash(seed, i, 99));
            r.Convex(Props.RubblePile(r.Kit(RuinKit.Layer.Solid, center), center + off, i * 0.9f, ps, seed + 3 + i, wall, wall.R > wall.G + 0.1f));
        }
        r.Block(new Rect2(lot.Position + Vector2.One * 1.5f, lot.Size - Vector2.One * 3f));
        r.Decal(Mats.Decal.Dust, center, Mathf.Max(size.X, size.Y) * 1.5f, 0.3f, 0.172f);
        r.Decal(Mats.Decal.Scorch, center, Mathf.Max(size.X, size.Y) * 1.2f, 1.1f, 0.176f);
        if (RuinKit.Hash(c2.X, c2.Y, 98) < 0.5f) r.Fire(center + new Vector3(size.X * 0.2f, pile.Y * 0.8f, -size.Y * 0.15f), 2f, r.Lights < 2);
        else r.Smolder(center + Vector3.Up * pile.Y, 1.6f);
        return true;
    }

    // ───────────────────────── Épaves ─────────────────────────

    /// <summary>
    /// Épave (repère au sol) : <paramref name="pose"/> 0 sur roues, 1 sur le toit, 2 sur le flanc.
    /// <paramref name="burn"/> 0 éteinte, 1 fumante, 2 en flammes. Collision ajustée au volume.
    /// </summary>
    private static void PlaceWreck(Vector3 p, float yaw, Props.VehicleType type, int v, int pose, int burn)
    {
        var r = _ruins;
        var size = Props.WreckSize(type, v);
        var mesh = type switch { Props.VehicleType.Van => Props.WreckVan(v % 2), Props.VehicleType.Truck => Props.WreckTruck(), _ => Props.WreckCar(v) };
        if (type != Props.VehicleType.Car) pose = 0;
        Basis b = new Basis(Vector3.Up, yaw);
        var origin = p;
        if (pose == 1) { b *= new Basis(Vector3.Back, Mathf.Pi); origin += Vector3.Up * (size.Y + 0.02f); }
        else if (pose == 2) { b *= new Basis(Vector3.Back, Mathf.Pi / 2); origin += b.Y * 0f + new Basis(Vector3.Up, yaw) * new Vector3(size.Y * 0.5f, size.X * 0.5f, 0); }
        var xf = new Transform3D(b, origin);
        r.Inst(mesh, xf, true, 320f);
        r.Box(new Transform3D(b, xf * new Vector3(0, size.Y / 2, 0)), size);
        r.Take(p, size.Z * 0.5f);
        r.Decal(Mats.Decal.Scorch, p, size.Z * 1.5f, yaw, p.Y + 0.025f, 0.75f);
        if (type == Props.VehicleType.Truck) r.Decal(Mats.Decal.Oil, p + new Basis(Vector3.Up, yaw) * new Vector3(0.6f, 0, 1.5f), 3.2f, yaw, p.Y + 0.03f);
        if (burn == 2) r.Fire(p + new Basis(Vector3.Up, yaw) * new Vector3(0, size.Y * 0.45f, size.Z * 0.25f), type == Props.VehicleType.Car ? 1f : 1.4f);
        else if (burn == 1) r.Smolder(p + Vector3.Up * size.Y * 0.6f, 0.9f);
    }

    /// <summary>Choisit une épave au hasard (générateur du décor de ruines) et la pose si la place est libre.</summary>
    private static bool TryWreck(Vector3 p, float yaw, float radius, int burn = -1)
    {
        var r = _ruins;
        if (!r.Free(p, radius)) return false;
        var roll = r.Rng.Randf();
        var type = roll < 0.12f ? Props.VehicleType.Van : roll < 0.18f ? Props.VehicleType.Truck : Props.VehicleType.Car;
        var pose = r.Rng.Randf() switch { < 0.12f => 1, < 0.2f => 2, _ => 0 };
        var b = burn >= 0 ? burn : r.Rng.Randf() < 0.04f ? 1 : 0;
        PlaceWreck(p, yaw, type, r.Rng.RandiRange(0, 3), pose, b);
        return true;
    }

    // ───────────────────────── Habillage général ─────────────────────────

    private static void DressCity(Result res, PropScatter props)
    {
        var r = _ruins;
        var rng = r.Rng;
        var buildings = new List<Rect2>();
        foreach (var b in res.Buildings)
        {
            var rect = new Rect2(b.Position.X - b.Size.X / 2, b.Position.Z - b.Size.Z / 2, b.Size.X, b.Size.Z);
            buildings.Add(rect);
            r.Block(rect.Grow(0.4f));
        }
        foreach (var n in res.NestSpots) r.Block(n, 9f, 9f);
        var hot = new List<(Vector3 p, float rad, float dens)>();

        // Scènes principales d'abord (elles reçoivent en priorité les lumières de foyers).
        BuildPileup(hot);
        BuildBusCrash(hot);
        BuildCheckpoint(new Vector3(-70, 0, 30), 0, hot);
        BuildCheckpoint(new Vector3(80, 0, -120), 1, hot);
        BuildCoastDefense();

        // Dégâts de façade et gravats au pied des immeubles.
        foreach (var b in res.Buildings)
        {
            var hd = RuinKit.Hash(b.Id, 3, 2);
            if (hd < 0.62f)
            {
                // Calque de façade sur un immeuble sur trois seulement (lourd sur un sur dix) ; les autres gardent leurs
                // couleurs intactes, comme la ville de Monster Attack au début de l'invasion.
                if (hd < 0.3f) RuinKit.DamageFacades(b, hd < 0.1f, b.Id);
                // Toit noirci (et percé pour les plus touchés), sur les mêmes immeubles que le calque de façade.
                var roofY = b.Position.Y + b.Size.Y + 0.04f;
                var rs = Mathf.Min(b.Size.X, b.Size.Z) * 0.9f;
                if (hd < 0.3f) r.Decal(hd < 0.24f ? Mats.Decal.Crater : Mats.Decal.Scorch, b.Position + new Vector3((hd - 0.3f) * b.Size.X * 0.5f, 0, 0), rs * (hd < 0.24f ? 0.8f : 1f), b.Id, roofY);
                if (hd < 0.15f) r.Decal(Mats.Decal.Ash, b.Position, rs * 1.1f, b.Id * 0.7f, roofY - 0.01f);
            }
            if (RuinKit.Hash(b.Id, 4, 2) < (hd < 0.24f ? 0.75f : 0.16f)) RubbleAt(b, hd < 0.24f, hot);
            // Incendie dans les étages : flammes à une fenêtre, fumée noire au-dessus du toit.
            if (hd < 0.24f && RuinKit.Hash(b.Id, 13, 2) < 0.28f && b.Size.Y > 9f)
            {
                var side = RuinKit.Hash(b.Id, 14, 2) < 0.5f ? 1f : -1f;
                var fy = Mathf.Floor(b.Size.Y * (0.45f + RuinKit.Hash(b.Id, 15, 2) * 0.4f) / 3.2f) * 3.2f - 1.4f;
                var fp = b.Position + new Vector3((RuinKit.Hash(b.Id, 16, 2) - 0.5f) * b.Size.X * 0.6f, fy, side * (b.Size.Z / 2 + 0.4f));
                if (b.Id % 2 == 0) r.Fire(fp, 1.1f, false);
                r.Smolder(b.Position + Vector3.Up * (b.Size.Y + 1f), 2.2f);
            }
        }

        // Épaves abandonnées sur les voies de circulation (de travers, parfois retournées).
        foreach (var xr in RoadXs)
            for (var z = -Half + 8; z < BlvdNorth - 6; z += 9f)
            {
                if (NearCrossing(z, RoadZs) || rng.Randf() > 0.13f) continue;
                var lane = rng.Randf() < 0.5f ? -1.5f : 1.5f;
                var p = OnGround(xr + lane + rng.RandfRange(-0.4f, 0.4f), z + rng.RandfRange(-2, 2));
                TryWreck(p, (lane > 0 ? 0 : Mathf.Pi) + rng.RandfRange(-0.7f, 0.7f), 3.2f);
            }
        foreach (var zr in RoadZs)
            for (var x = -Half + 8; x < Half - 8; x += 9f)
            {
                if (NearCrossing(x, RoadXs) || rng.Randf() > 0.13f) continue;
                var lane = rng.Randf() < 0.5f ? -1.5f : 1.5f;
                var p = OnGround(x + rng.RandfRange(-2, 2), zr + lane + rng.RandfRange(-0.4f, 0.4f));
                TryWreck(p, (lane > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2) + rng.RandfRange(-0.7f, 0.7f), 3.2f);
            }
        for (var x = -Half + 10; x < Half - 10; x += 11f)
        {
            if (rng.Randf() > 0.2f) continue;
            var lane = rng.RandfRange(-6.5f, 6.5f);
            TryWreck(OnGround(x, 83 + lane), (lane > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2) + rng.RandfRange(-0.8f, 0.8f), 3.2f);
        }

        // Cratères d'impact sur la chaussée.
        var craters = 0;
        for (var tries = 0; tries < 60 && craters < 9; tries++)
        {
            var onX = rng.Randf() < 0.5f;
            var p = onX
                ? OnGround(RoadXs[rng.RandiRange(0, RoadXs.Length - 1)] + rng.RandfRange(-3, 3), rng.RandfRange(-Half + 10, BlvdNorth - 10))
                : OnGround(rng.RandfRange(-Half + 10, Half - 10), RoadZs[rng.RandiRange(0, RoadZs.Length - 1)] + rng.RandfRange(-3, 3));
            if (!r.Free(p, 4f)) continue;
            r.Take(p, 3f);
            Crater(p, rng.RandfRange(5f, 8.5f), hot);
            craters++;
        }

        // Fissures, flaques d'hémolymphe, brûlures d'acide, taches d'huile.
        for (var i = 0; i < 170; i++)
        {
            var p = RandomStreetPoint(rng);
            var roll = rng.Randf();
            var kind = roll < 0.32f ? Mats.Decal.Cracks : roll < 0.52f ? Mats.Decal.Ichor : roll < 0.6f ? Mats.Decal.Acid : roll < 0.68f ? Mats.Decal.Oil
                : roll < 0.8f ? Mats.Decal.Ash : roll < 0.9f ? Mats.Decal.Scorch : Mats.Decal.Dust;
            var size = kind switch { Mats.Decal.Cracks => rng.RandfRange(4f, 9f), Mats.Decal.Scorch or Mats.Decal.Ash => rng.RandfRange(5f, 11f), _ => rng.RandfRange(1.8f, 4.5f) };
            r.Decal(kind, p, size, rng.RandfRange(0, Mathf.Tau), p.Y + 0.02f + i * 0.0002f, rng.RandfRange(0.7f, 1.3f));
        }

        // Square central : pelouse brûlée par endroits (aucun objet solide près du départ).
        foreach (var (x, z, s, k) in new[] { (14f, -6f, 9f, Mats.Decal.Ash), (-6f, 17f, 7f, Mats.Decal.Scorch), (18f, 16f, 6f, Mats.Decal.Crater), (-8f, -8f, 5f, Mats.Decal.Ichor) })
            r.Decal(k, new Vector3(x, 0, z), s, x * 0.3f, 0.25f);

        // Valises, matelas, canapés : traces d'exode sur les trottoirs.
        for (var i = 0; i < 40; i++)
        {
            var p = RandomStreetPoint(rng);
            if (p.Y < 0.1f || !r.Free(p, 1.2f)) continue;
            var roll = rng.Randf();
            var yaw = rng.RandfRange(0, Mathf.Tau);
            if (roll < 0.5f) r.Inst(Props.Suitcase(), RuinKit.At(p, yaw, 0, roll < 0.2f ? Mathf.Pi / 2 : 0) with { Origin = p + Vector3.Up * (roll < 0.2f ? 0.35f : 0) }, false, 110f);
            else if (roll < 0.75f) r.Inst(Props.Mattress(), RuinKit.At(p, yaw), false, 140f);
            else
            {
                r.Inst(Props.Sofa(), RuinKit.At(p, yaw), true, 160f);
                r.BoxOnGround(p, yaw, new Vector3(1.9f, 0.85f, 0.85f));
                r.Take(p, 1.2f);
            }
        }

        // Insectes : toiles entre façades, résine et œufs près des nids.
        BuildWebs(res);
        foreach (var n in res.NestSpots) NestGrowth(n, buildings);

        // Débris épars (sans collision) : denses près des gravats et des cratères.
        Litter(buildings, hot);

        // Panaches lointains (incendies hors de la ville).
        r.DistantPlumes(new[]
        {
            new Vector3(-340, 30, -250), new Vector3(-60, 35, -420), new Vector3(270, 30, -330),
            new Vector3(420, 25, -70), new Vector3(-430, 25, 30), new Vector3(170, 5, 430),
        });
        r.Flush();
        _ruins = null;
    }

    private static bool NearCrossing(float a, float[] crossings)
    {
        foreach (var c in crossings) if (Mathf.Abs(a - c) < 11f) return true;
        return false;
    }

    /// <summary>Point au hasard sur une rue ou un trottoir (hors square et hors plage).</summary>
    private static Vector3 RandomStreetPoint(RandomNumberGenerator rng)
    {
        for (var i = 0; i < 8; i++)
        {
            Vector3 p;
            if (rng.Randf() < 0.5f)
                p = OnGround(RoadXs[rng.RandiRange(0, RoadXs.Length - 1)] + rng.RandfRange(-8.5f, 8.5f), rng.RandfRange(-Half + 5, BlvdNorth));
            else
                p = OnGround(rng.RandfRange(-Half + 5, Half - 5), RoadZs[rng.RandiRange(0, RoadZs.Length - 1)] + rng.RandfRange(-8.5f, 8.5f));
            if (Mathf.Abs(p.X - 5) < 22 && Mathf.Abs(p.Z - 5) < 22) continue;
            return p;
        }
        return OnGround(-70, -45);
    }

    /// <summary>Tas de gravats contre une façade (débordant sur le trottoir), avec poussière et débris autour.</summary>
    private static void RubbleAt(Building b, bool heavy, List<(Vector3, float, float)> hot)
    {
        var r = _ruins;
        var side = (int)(RuinKit.Hash(b.Id, 5, 2) * 4);
        var s = b.Size;
        var (n, along, len) = side switch
        {
            0 => (Vector3.Back, Vector3.Right, s.X),
            1 => (Vector3.Forward, Vector3.Right, s.X),
            2 => (Vector3.Right, Vector3.Back, s.Z),
            _ => (Vector3.Left, Vector3.Back, s.Z),
        };
        var half = side < 2 ? s.Z / 2 : s.X / 2;
        var w = Mathf.Min(len * 0.8f, (heavy ? 6f : 3.5f) + RuinKit.Hash(b.Id, 6, 2) * 3f);
        var depth = heavy ? 4.2f + RuinKit.Hash(b.Id, 7, 2) * 1.5f : 2.4f + RuinKit.Hash(b.Id, 7, 2) * 1.2f;
        var hgt = heavy ? 1.6f + RuinKit.Hash(b.Id, 8, 2) * 1.0f : 0.8f + RuinKit.Hash(b.Id, 8, 2) * 0.6f;
        var off = (RuinKit.Hash(b.Id, 9, 2) - 0.5f) * Mathf.Max(0, len - w) * 0.8f;
        var c = new Vector3(b.Position.X, 0, b.Position.Z) + n * (half + depth * 0.32f) + along * off;
        c = OnGround(c.X, c.Z);
        // Le tas ne doit pas déborder sur la voie de circulation ni toucher l'aire de départ.
        if (Mathf.Abs(c.X) < 32 && Mathf.Abs(c.Z) < 32) return;
        var yaw = Mathf.Atan2(n.X, n.Z);
        var tint = RuinKit.Hash(b.Id, 10, 2) < 0.4f ? new Color(0.5f, 0.3f, 0.24f) : new Color(0.6f, 0.58f, 0.54f);
        var pts = Props.RubblePile(r.Kit(RuinKit.Layer.Solid, c), c, yaw, new Vector3(w, hgt, depth), b.Id * 7 + 1, tint, tint.R > tint.G + 0.1f);
        r.Convex(pts);
        r.Take(c, Mathf.Max(w, depth) * 0.5f);
        r.Decal(Mats.Decal.Dust, c, Mathf.Max(w, depth) * 1.7f, yaw, c.Y + 0.02f);
        hot.Add((c, Mathf.Max(w, depth) * 1.1f, 0.45f));
        if (heavy && RuinKit.Hash(b.Id, 11, 2) < 0.2f)
        {
            r.Fire(c + Vector3.Up * hgt * 0.6f, 1.3f);
            r.Decal(Mats.Decal.Ash, c, Mathf.Max(w, depth) * 1.4f, yaw + 1f, c.Y + 0.024f);
        }
        else if (heavy && RuinKit.Hash(b.Id, 12, 2) < 0.15f) r.Smolder(c + Vector3.Up * hgt, 1.2f);
    }

    /// <summary>Cratère : marque au sol, bourrelet de blocs d'enrobé arrachés, débris projetés.</summary>
    private static void Crater(Vector3 p, float size, List<(Vector3, float, float)> hot)
    {
        var r = _ruins;
        var yaw = r.Rng.RandfRange(0, Mathf.Tau);
        r.Decal(Mats.Decal.Crater, p, size, yaw, p.Y + 0.03f);
        r.Decal(Mats.Decal.Scorch, p, size * 1.9f, yaw + 1.3f, p.Y + 0.022f);
        var k = r.Kit(RuinKit.Layer.Solid, p);
        var d = Mats.Detail();
        var n = 12 + (int)(size * 1.5f);
        for (var i = 0; i < n; i++)
        {
            var a = i * Mathf.Tau / n + r.Rng.RandfRange(-0.2f, 0.2f);
            var rad = size * r.Rng.RandfRange(0.22f, 0.32f);
            var q = p + new Vector3(Mathf.Cos(a) * rad, 0, Mathf.Sin(a) * rad);
            var s = r.Rng.RandfRange(0.3f, 0.75f);
            k.Col = new Color(0.12f, 0.12f, 0.12f).Lerp(new Color(0.3f, 0.27f, 0.24f), r.Rng.Randf());
            // Plaques d'enrobé soulevées, inclinées vers l'extérieur.
            k.Box(d, q + Vector3.Up * 0.08f, new Vector3(s, 0.14f, s * 0.7f), new Basis(Vector3.Up, -a) * new Basis(Vector3.Back, r.Rng.RandfRange(0.25f, 0.7f)));
        }
        hot.Add((p, size * 1.2f, 0.5f));
    }

    // ───────────────────────── Scènes ─────────────────────────

    /// <summary>Carambolage sur la rue est-ouest z = -70 : voitures encastrées, fourgon couché, un véhicule en feu.</summary>
    private static void BuildPileup(List<(Vector3, float, float)> hot)
    {
        var r = _ruins;
        const float z = -70f;
        var spots = new (float x, float dz, float yaw, Props.VehicleType t, int v, int pose, int burn)[]
        {
            (-58f, -1.6f, -1.2f, Props.VehicleType.Car, 0, 0, 2),
            (-53.5f, -0.4f, -1.9f, Props.VehicleType.Car, 2, 0, 0),
            (-49.5f, 1.8f, 1.1f, Props.VehicleType.Van, 0, 0, 1),
            (-44.5f, -2.0f, -1.45f, Props.VehicleType.Car, 1, 1, 0),
            (-40.5f, 1.2f, 2.0f, Props.VehicleType.Car, 3, 0, 0),
            (-36f, -1.5f, -1.75f, Props.VehicleType.Car, 0, 2, 0),
            (-32.5f, 2.2f, 1.45f, Props.VehicleType.Truck, 0, 0, 0),
        };
        foreach (var s in spots)
        {
            var p = OnGround(s.x, z + s.dz);
            PlaceWreck(p, s.yaw, s.t, s.v, s.pose, s.burn);
        }
        hot.Add((OnGround(-45, z), 16f, 0.35f));
        r.Decal(Mats.Decal.Oil, OnGround(-47, z + 0.5f), 6f, 0.4f, 0.04f, 1.6f);
    }

    /// <summary>Bus de ville encastré dans l'angle d'un pâté, avant éventré, gravats et incendie.</summary>
    private static void BuildBusCrash(List<(Vector3, float, float)> hot)
    {
        var r = _ruins;
        var p = OnGround(-121.5f, 47f);
        var fwd = new Vector3(-1f, 0, -0.38f).Normalized();
        var yaw = Mathf.Atan2(fwd.X, fwd.Z);
        var b = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Back, 0.04f);
        r.Inst(Props.WreckBus(), new Transform3D(b, p), true, 360f);
        r.Box(new Transform3D(b, p + Vector3.Up * 1.55f + fwd * -0.3f), new Vector3(2.5f, 3.1f, 11.2f));
        r.Take(p, 6f);
        r.Decal(Mats.Decal.Scorch, p, 15f, yaw, 0.025f, 0.6f);
        var front = OnGround(p.X + fwd.X * 6.6f, p.Z + fwd.Z * 6.6f);
        var pts = Props.RubblePile(r.Kit(RuinKit.Layer.Solid, front), front, yaw, new Vector3(5.5f, 1.4f, 3.2f), 991, new Color(0.6f, 0.58f, 0.54f), false);
        r.Convex(pts);
        r.Fire(p + fwd * 3.5f + Vector3.Up * 1.4f, 1.6f);
        hot.Add((front, 9f, 0.6f));
    }

    /// <summary>
    /// Barrage militaire abandonné à un carrefour : chicanes de séparateurs, nids de sacs de sable,
    /// gabions, barbelés, hérissons antichars, camion bâché, brasero.
    /// </summary>
    private static void BuildCheckpoint(Vector3 c, int variant, List<(Vector3, float, float)> hot)
    {
        var r = _ruins;
        var flip = variant == 1 ? -1f : 1f;
        // Chicanes de séparateurs sur les branches nord et sud.
        foreach (var arm in new[] { -1f, 1f })
        {
            var z0 = c.Z + arm * 14f;
            var z1 = c.Z + arm * 19f;
            foreach (var (x, z, yaw) in new[] { (c.X - 4.3f, z0, Mathf.Pi / 2), (c.X - 1.3f, z0, Mathf.Pi / 2 + 0.05f), (c.X + 2.6f, z1, Mathf.Pi / 2 - 0.08f), (c.X + 5.0f, z1 + 0.6f * arm, Mathf.Pi / 2 + 0.5f) })
            {
                var p = OnGround(x, z);
                var v = RuinKit.Hash(x, z, 91) < 0.35f ? 1 : 0;
                r.Inst(Props.Barrier(v), RuinKit.At(p, yaw), true, 240f);
                r.BoxOnGround(p, yaw, Props.BarrierSize);
                r.Take(p, 1.5f);
            }
        }
        // Branche est : barbelés en travers, hérissons derrière.
        var kit = r.Kit(RuinKit.Layer.Solid, c);
        var wx = c.X + 12f * flip;
        Props.RazorWire(kit, OnGround(wx, c.Z - 5.8f), OnGround(wx, c.Z + 1.5f));
        Props.RazorWire(kit, OnGround(wx + 0.9f * flip, c.Z - 5.5f), OnGround(wx + 0.9f * flip, c.Z + 1.2f), 0.36f);
        for (var i = 0; i < 3; i++)
        {
            var p = OnGround(wx + 4.5f * flip, c.Z - 4f + i * 3.6f);
            r.Inst(Props.Hedgehog(), RuinKit.At(p, i * 0.7f), true, 220f);
            r.BoxOnGround(p, i * 0.7f, new Vector3(1.3f, 1.2f, 1.3f));
            r.Take(p, 1f);
        }
        // Branche ouest : camion bâché abandonné le long du trottoir, caisses et fûts.
        var tp = OnGround(c.X - 17f * flip, c.Z + 3.6f);
        var tyaw = flip > 0 ? -Mathf.Pi / 2 + 0.06f : Mathf.Pi / 2 + 0.06f;
        r.Inst(Props.ArmyTruck(), RuinKit.At(tp, tyaw), true, 320f);
        r.Box(new Transform3D(new Basis(Vector3.Up, tyaw), tp + Vector3.Up * (Props.ArmyTruckSize.Y / 2)), Props.ArmyTruckSize);
        r.Take(tp, 4.2f);
        r.Decal(Mats.Decal.Oil, tp, 3.5f, 0.3f, tp.Y + 0.03f);
        for (var i = 0; i < 3; i++)
        {
            var p = OnGround(c.X - 11.5f * flip, c.Z + 5.3f + i * 0.65f);
            r.Inst(Props.Drum(0), RuinKit.At(p, i), true, 160f);
            r.BoxOnGround(p, 0, new Vector3(0.6f, 0.88f, 0.6f));
        }
        // Nids de sacs de sable dans deux angles du carrefour (ouverts vers le centre).
        foreach (var corner in new[] { new Vector2(1, 1), new Vector2(-1, -1) })
        {
            var cc = OnGround(c.X + corner.X * 3.6f, c.Z + corner.Y * 3.6f);
            var k = r.Kit(RuinKit.Layer.Solid, cc);
            var sx = corner.X; var sz = corner.Y;
            // L + retour : deux côtés face aux rues, un petit retour.
            var a = cc + new Vector3(sx * 1.7f, 0, sz * 1.7f);
            var b1 = cc + new Vector3(-sx * 1.5f, 0, sz * 1.7f);
            var b2 = cc + new Vector3(sx * 1.7f, 0, -sz * 1.5f);
            foreach (var (p0, p1) in new[] { (b1, a), (a, b2), (b1 + new Vector3(0, 0, -sz * 1.3f), b1) })
            {
                var (bc, bs, byaw) = Props.SandbagWall(k, p0, p1, 6, (int)(p0.X * 3 + p0.Z));
                r.Box(new Transform3D(new Basis(Vector3.Up, byaw), bc), bs);
            }
            r.Take(cc, 2.4f);
        }
        // Gabions en travers de la branche ouest (passage laissé côté nord).
        for (var i = 0; i < 4; i++)
        {
            var p = OnGround(c.X - 8.5f * flip, c.Z + 5.2f - i * 1.12f);
            r.Inst(Props.Hesco(), RuinKit.At(p, 0), true, 260f);
            r.BoxOnGround(p, 0, new Vector3(1.1f, 1.3f, 1.1f));
        }
        r.Take(OnGround(c.X - 8.5f * flip, c.Z + 3.5f), 2.2f);
        // Brasero encore rougeoyant.
        var bp = OnGround(c.X + 4.6f, c.Z + 11.5f);
        r.Inst(Props.Drum(1), RuinKit.At(bp, 0), true, 160f);
        r.BoxOnGround(bp, 0, new Vector3(0.6f, 0.88f, 0.6f));
        r.Fire(bp + Vector3.Up * 0.85f, 0.45f);
        // Douilles, caisses vides, brûlures.
        r.Decal(Mats.Decal.Scorch, OnGround(c.X + 3, c.Z - 3), 7f, 0.8f, 0.026f);
        r.Decal(Mats.Decal.Ichor, OnGround(c.X - 4, c.Z + 6), 4.5f, 2.1f, 0.03f);
        hot.Add((c, 18f, 0.25f));
    }

    /// <summary>Défense côtière : hérissons et barbelés sur la plage, épave de char d'assaut amphibie absente (décor léger).</summary>
    private static void BuildCoastDefense()
    {
        var r = _ruins;
        var rng = r.Rng;
        for (var x = -Half + 14; x < Half - 14; x += 9f)
        {
            if (Mathf.Abs(x - PierX) < 10 || Mathf.Abs(x - 65) < 8) continue;
            var z = BeachStart + 30 + rng.RandfRange(-3, 3);
            var p = new Vector3(x + rng.RandfRange(-2, 2), -0.12f, z);
            if (rng.Randf() < 0.75f)
            {
                var yaw = rng.RandfRange(0, Mathf.Tau);
                r.Inst(Props.Hedgehog(), RuinKit.At(p, yaw, rng.RandfRange(-0.15f, 0.15f)), true, 240f);
                r.BoxOnGround(p, yaw, new Vector3(1.3f, 1.2f, 1.3f));
            }
        }
        var k = r.Kit(RuinKit.Layer.Solid, new Vector3(0, 0, BeachStart + 22));
        for (var x = -Half + 10; x < Half - 30; x += 26f)
        {
            if (Mathf.Abs(x - PierX) < 14) continue;
            Props.RazorWire(k, new Vector3(x, -0.05f, BeachStart + 22 + rng.RandfRange(-1, 1)), new Vector3(x + rng.RandfRange(12, 20), -0.05f, BeachStart + 22 + rng.RandfRange(-1, 1)));
        }
    }

    /// <summary>Toiles géantes tendues au-dessus des rues, entre deux immeubles hauts qui se font face.</summary>
    private static void BuildWebs(Result res)
    {
        var r = _ruins;
        var made = 0;
        foreach (var a in res.Buildings)
        {
            if (made >= 4 || a.Size.Y < 14f || RuinKit.Hash(a.Id, 21, 3) > 0.5f) continue;
            var aMaxX = a.Position.X + a.Size.X / 2;
            foreach (var b in res.Buildings)
            {
                if (b == a || b.Size.Y < 11f) continue;
                var bMinX = b.Position.X - b.Size.X / 2;
                var gap = bMinX - aMaxX;
                if (gap < 12f || gap > 24f) continue;
                var z0 = Mathf.Max(a.Position.Z - a.Size.Z / 2, b.Position.Z - b.Size.Z / 2) + 2f;
                var z1 = Mathf.Min(a.Position.Z + a.Size.Z / 2, b.Position.Z + b.Size.Z / 2) - 2f;
                if (z1 - z0 < 8f) continue;
                var zm = (z0 + z1) / 2;
                if (Mathf.Abs(zm) < 40 && Mathf.Abs((aMaxX + bMinX) / 2) < 40) continue;
                var ha = Mathf.Min(a.Size.Y - 2f, 22f);
                var hb = Mathf.Min(b.Size.Y - 2f, 20f);
                var anchors = new[]
                {
                    new Vector3(aMaxX + 0.05f, ha, zm - 5f), new Vector3(aMaxX + 0.05f, ha * 0.55f, zm + 4.5f),
                    new Vector3(bMinX - 0.05f, hb * 0.6f, zm + 5f), new Vector3(bMinX - 0.05f, hb, zm - 3.5f),
                };
                Props.Web(r.Kit(RuinKit.Layer.Organic, anchors[0]), anchors, a.Id);
                // Cocon et résine au pied de la façade d'ancrage.
                var foot = OnGround(aMaxX + 1.2f, zm);
                Props.ResinBlob(r.Kit(RuinKit.Layer.Organic, foot), foot + Vector3.Up * 0.2f, new Vector3(1.6f, 0.9f, 2.4f), a.Id);
                made++;
                break;
            }
        }
    }

    /// <summary>Résine qui grimpe sur la façade la plus proche d'un nid, grappes d'œufs, flaques d'acide.</summary>
    private static void NestGrowth(Vector3 nest, List<Rect2> buildings)
    {
        var r = _ruins;
        var best = new Rect2();
        var bestD = 1e9f;
        foreach (var b in buildings)
        {
            var cx = Mathf.Clamp(nest.X, b.Position.X, b.End.X);
            var cz = Mathf.Clamp(nest.Z, b.Position.Y, b.End.Y);
            var d = new Vector2(cx - nest.X, cz - nest.Z).Length();
            if (d < bestD) { bestD = d; best = b; }
        }
        var seed = (int)(Mathf.Abs(nest.X) * 3 + Mathf.Abs(nest.Z));
        if (bestD < 40f)
        {
            // Coin du bâtiment le plus proche du nid : coulée de résine montant le long de l'arête.
            var corner = new Vector3(nest.X < best.GetCenter().X ? best.Position.X : best.End.X, 0, nest.Z < best.GetCenter().Y ? best.Position.Y : best.End.Y);
            var out1 = new Vector3(Mathf.Sign(corner.X - best.GetCenter().X), 0, Mathf.Sign(corner.Z - best.GetCenter().Y));
            var k = r.Kit(RuinKit.Layer.Organic, corner);
            for (var i = 0; i < 9; i++)
            {
                var y = 0.3f + i * 1.25f + RuinKit.Hash(seed, i, 1) * 0.5f;
                var s = 1.6f - i * 0.14f;
                Props.ResinBlob(k, OnGround(corner.X, corner.Z) + out1 * (0.25f + s * 0.2f) + Vector3.Up * y, new Vector3(s, s * 0.9f, s), seed + i);
            }
            Props.ResinBlob(k, OnGround(corner.X, corner.Z) + out1 * 1.5f, new Vector3(3.2f, 0.9f, 2.8f), seed + 20);
        }
        // Grappes d'œufs autour du nid (hors de son emplacement).
        for (var i = 0; i < 4; i++)
        {
            var a = i * Mathf.Tau / 4 + RuinKit.Hash(seed, i, 2);
            var p = OnGround(nest.X + Mathf.Cos(a) * (10f + i), nest.Z + Mathf.Sin(a) * (10f + i));
            Props.EggCluster(r.Kit(RuinKit.Layer.Organic, p), p, 0.9f + RuinKit.Hash(seed, i, 3) * 0.6f, seed + i * 7);
            r.Decal(Mats.Decal.Acid, p, 5f, a, p.Y + 0.028f);
        }
        for (var i = 0; i < 6; i++)
        {
            var a = RuinKit.Hash(seed, i, 4) * Mathf.Tau;
            var d = 4f + RuinKit.Hash(seed, i, 5) * 14f;
            var p = OnGround(nest.X + Mathf.Cos(a) * d, nest.Z + Mathf.Sin(a) * d);
            r.Decal(Mats.Decal.Ichor, p, 2.5f + RuinKit.Hash(seed, i, 6) * 3f, a, p.Y + 0.026f + i * 0.0003f);
        }
    }

    /// <summary>Débris épars sur toute la ville (MultiMesh, sans collision ni ombres).</summary>
    private static void Litter(List<Rect2> buildings, List<(Vector3 p, float rad, float dens)> hot)
    {
        var r = _ruins;
        const float step = 2.4f;
        var idx = 0;
        for (var z = -Half + 2; z < BeachStart - 1; z += step)
        for (var x = -Half + 2; x < Half - 2; x += step, idx++)
        {
            var jx = x + (RuinKit.Hash(idx, 1, 4) - 0.5f) * step;
            var jz = z + (RuinKit.Hash(idx, 2, 4) - 0.5f) * step;
            var inside = false;
            foreach (var b in buildings)
                if (jx > b.Position.X - 0.2f && jx < b.End.X + 0.2f && jz > b.Position.Y - 0.2f && jz < b.End.Y + 0.2f) { inside = true; break; }
            if (inside) continue;
            var dens = 0.07f;
            foreach (var (hp, hr, hd) in hot)
            {
                var dd = new Vector2(jx - hp.X, jz - hp.Z).Length();
                if (dd < hr) dens = Mathf.Max(dens, hd * (1 - dd / hr) + 0.07f);
            }
            if (Mathf.Abs(jx - 5) < 18 && Mathf.Abs(jz - 5) < 18) dens *= 0.3f;   // square de départ plus dégagé
            if (RuinKit.Hash(idx, 3, 4) > dens) continue;
            var kind = (int)(RuinKit.Hash(idx, 4, 4) * Props.DebrisKinds);
            if (kind == 7 && RuinKit.Hash(idx, 5, 4) < 0.7f) kind = 5;   // pneus rares, papiers fréquents
            var p = OnGround(jx, jz);
            var xf = RuinKit.At(p, RuinKit.Hash(idx, 6, 4) * Mathf.Tau, 0, 0, 0.75f + RuinKit.Hash(idx, 7, 4) * 0.6f);
            r.Inst(Props.Debris(kind), xf, false, kind == 5 || kind == 9 ? 60f : 95f);
        }
    }
}
