using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Carte « Banlieue boisée » : bourg pavillonnaire en lisière de forêt (nord et ouest), traversé au sud
/// par une rivière. Génération déterministe (graines fixes, jamais GD.Randf) : mêmes identifiants
/// d'immeubles chez tous les pairs, mêmes zones d'apparition que les autres cartes.
///
/// Choix de jeu pour la rivière : elle est <b>guéable</b>. Le sol de jeu reste plat à y = 0 (les ennemis
/// marchent à y = 0 et le champ de flux ne connaît que les empreintes des bâtiments) ; l'eau est une nappe
/// peu profonde (≈ 13 cm) posée sur le lit, que joueurs et insectes traversent en pataugeant.
/// Les deux ponts routiers sont de simples tabliers bas (rampes de collision) pour le décor et la circulation.
///
/// Organisation : un masque au mètre près (routes, rivière, forêt, réserves, jardins) sert à placer les
/// lots de façon sûre le long des rues, puis le décor est fusionné par secteurs de 110 m (MeshKit)
/// et les objets répétés (arbres, mobilier) sont regroupés en MultiMesh.
/// </summary>
public static partial class SuburbBuilder
{
    private const float Half = CityBuilder.Half;
    /// <summary>Limite sud du champ de flux (les murs invisibles suivent les autres cartes).</summary>
    private const float SouthEdge = CityBuilder.SeaStart;
    /// <summary>Lisières de la forêt (au nord : z inférieur ; à l'ouest : x inférieur).</summary>
    private const float ForestN = -160f;
    private const float ForestW = -152f;
    private const float RiverHalf = 9f;
    private const float RiverBank = 3.5f;
    private const float WaterY = 0.13f;

    /// <summary>Axe de la rivière (z en fonction de x), d'ouest en est.</summary>
    public static float RiverZ(float x) => 96f + 7f * Mathf.Sin(x * 0.016f + 0.8f) + 3f * Mathf.Sin(x * 0.043f);

    private static float GrandeZ(float x) => 34f + 5f * Mathf.Sin((x + 40f) * 0.011f);

    // Codes du masque.
    private const byte Free = 0, RoadC = 1, RiverC = 2, Reserved = 3, LotC = 4, ForestC = 5, BuiltC = 6;

    /// <summary>Masque au mètre (x de -240 à 240, z de -240 à 170).</summary>
    private sealed class Grid
    {
        public const int X0 = -240, Z0 = -240, W = 480, H = 410;
        public readonly byte[] C = new byte[W * H];
        /// <summary>Indice + 1 de la route dont la chaussée couvre la cellule (0 : aucune).</summary>
        public readonly short[] Asphalt = new short[W * H];

        public static bool Cell(float x, float z, out int idx)
        {
            var i = Mathf.FloorToInt(x) - X0;
            var j = Mathf.FloorToInt(z) - Z0;
            idx = j * W + i;
            return i >= 0 && j >= 0 && i < W && j < H;
        }

        public byte Get(float x, float z) => Cell(x, z, out var i) ? C[i] : ForestC;

        public int AsphaltAt(float x, float z) => Cell(x, z, out var i) ? Asphalt[i] : 0;

        /// <summary>Remplit les cellules dont le centre est dans le rectangle (seulement les cellules libres si demandé).</summary>
        public void Fill(float x0, float z0, float x1, float z1, byte v, bool onlyFree = true)
        {
            for (var z = Mathf.FloorToInt(z0); z < Mathf.CeilToInt(z1); z++)
            for (var x = Mathf.FloorToInt(x0); x < Mathf.CeilToInt(x1); x++)
            {
                if (x + 0.5f < x0 || x + 0.5f > x1 || z + 0.5f < z0 || z + 0.5f > z1) continue;
                if (!Cell(x, z, out var i)) continue;
                if (!onlyFree || C[i] == Free) C[i] = v;
            }
        }

        /// <summary>Vrai si toutes les cellules du rectangle sont libres.</summary>
        public bool IsFree(float x0, float z0, float x1, float z1)
        {
            for (var z = Mathf.FloorToInt(z0); z < Mathf.CeilToInt(z1); z++)
            for (var x = Mathf.FloorToInt(x0); x < Mathf.CeilToInt(x1); x++)
            {
                if (x + 0.5f < x0 || x + 0.5f > x1 || z + 0.5f < z0 || z + 0.5f > z1) continue;
                if (!Cell(x, z, out var i) || C[i] != Free) return false;
            }
            return true;
        }
    }

    /// <summary>Décor fusionné par secteurs carrés (culling par secteur).</summary>
    private sealed class Chunks
    {
        private const float Size = 110f;
        private readonly Dictionary<(int, int), MeshKit> _kits = new();
        private readonly List<(int, int)> _order = new();

        public MeshKit At(Vector3 p)
        {
            var key = (Mathf.FloorToInt(p.X / Size), Mathf.FloorToInt(p.Z / Size));
            if (_kits.TryGetValue(key, out var k)) return k;
            k = new MeshKit();
            _kits[key] = k;
            _order.Add(key);
            return k;
        }

        public void Flush(Node3D root, string name, bool shadows, float range = 0f)
        {
            foreach (var key in _order)
            {
                var k = _kits[key];
                if (k.Empty) continue;
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

    /// <summary>Rue : axe échantillonné tous les 2 m, demi-largeur de chaussée, trottoirs éventuels.</summary>
    private sealed class Road
    {
        public readonly List<Vector3> P = new();
        public readonly List<float> S = new();   // abscisse curviligne
        public int Index;
        public float Hw;
        public bool Walks;
        public bool Main;
        public bool Gravel;
        public Road StartJoin, EndJoin;
        public bool Stop;           // carrefour avec la grande rue : panneau Stop
        public float Bulb;          // rayon de la raquette de retournement en bout d'impasse (0 : aucune)

        public float Length => S[^1];
        public float Edge => Hw + (Walks ? 2f : 0f);

        public Vector3 Tangent(int i) => (P[Mathf.Min(i + 1, P.Count - 1)] - P[Mathf.Max(i - 1, 0)]).Normalized();

        /// <summary>Normale gauche (sens de parcours).</summary>
        public Vector3 Left(int i) => Vector3.Up.Cross(Tangent(i));

        public (Vector3 p, Vector3 t) At(float s)
        {
            if (s <= 0) return (P[0], Tangent(0));
            for (var i = 1; i < P.Count; i++)
            {
                if (S[i] < s) continue;
                var k = (s - S[i - 1]) / Mathf.Max(S[i] - S[i - 1], 0.001f);
                return (P[i - 1].Lerp(P[i], k), (P[i] - P[i - 1]).Normalized());
            }
            return (P[^1], Tangent(P.Count - 1));
        }

        public void Add(Vector3 p)
        {
            S.Add(P.Count == 0 ? 0 : S[^1] + P[^1].DistanceTo(p));
            P.Add(p);
        }
    }

    private sealed class Ctx
    {
        public Node3D Root;
        public StaticBody3D Body;
        public CityBuilder.Result Res;
        public TimeOfDay Time;
        public bool Night;
        public readonly Grid Mask = new();
        public readonly PropScatter Props = new();
        public readonly Chunks Ground = new();   // allées, clôtures, haies, marquages (sans ombres)
        public readonly Chunks Solid = new();    // murets, bancs maçonnés, ponts (avec ombres)
        public readonly List<Road> Roads = new();
        /// <summary>Points d'arbres de collision (troncs) déjà posés, pour éviter les doublons.</summary>
        public readonly List<Vector3> Trunks = new();
        public int NextId;
    }

    public static CityBuilder.Result Build(Node3D root, TimeOfDay time)
    {
        var c = new Ctx { Root = root, Time = time, Night = time == TimeOfDay.Night, Res = new CityBuilder.Result() };
        c.Body = new StaticBody3D { Name = "Ground", CollisionLayer = 1, CollisionMask = 0 };
        root.AddChild(c.Body);
        // Sol de jeu plat (y = 0) : une seule boîte de collision ; le visuel est le maillage de terrain.
        CityBuilder.AddWall(c.Body, new Vector3(0, -0.5f, -30), new Vector3(Half * 2 + 120, 1f, 470));
        BeginRuins(c);

        BuildRoadNetwork(c);
        MarkRiverAndForest(c);
        ReserveAreas(c);

        // Bâtiments : équipements du bourg d'abord, puis les pavillons rue par rue (ordre fixe).
        BuildLandmarks(c);
        var hr = new RandomNumberGenerator { Seed = 5150 };
        foreach (var (road, side) in HouseSides(c)) LineHouses(c, road, side, hr);

        BuildStreets(c);
        BuildSquare(c);
        BuildCemetery(c);
        BuildPark(c);
        BuildPitch(c);
        BuildParking(c);
        BuildRiver(c);
        BuildTerrain(c);
        BuildForest(c);
        DressSuburb(c);

        c.Ground.Flush(root, "SuburbGround", false, 0f);
        c.Solid.Flush(root, "SuburbSolid", true, 0f);
        c.Props.Flush(root);

        // Murs invisibles en bordure (mêmes limites que la ville).
        CityBuilder.AddWall(c.Body, new Vector3(0, 20, -Half - 10), new Vector3(Half * 2 + 40, 60, 2));
        CityBuilder.AddWall(c.Body, new Vector3(-Half - 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(Half + 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(0, 20, SouthEdge + 8), new Vector3(Half * 2 + 40, 60, 2));

        var res = c.Res;
        res.Zones["north"] = new Vector3(0, 0, -128);        // parc au pied de la forêt
        res.Zones["south"] = new Vector3(-55, 0, 128);       // prés au-delà de la rivière
        res.Zones["beach"] = new Vector3(40, 0, 132);        // prés au-delà de la rivière (est)
        res.Zones["east"] = new Vector3(128, 0, -25);        // square de quartier à l'est
        res.Zones["west"] = new Vector3(-142, 0, -20);       // chemin de lisière à l'ouest
        res.Zones["center"] = new Vector3(0, 0, -112);
        res.NestSpots.Add(new Vector3(0, 0, -118));
        res.NestSpots.Add(new Vector3(-185, 0, -45));
        res.NestSpots.Add(new Vector3(-120, 0, 131));
        res.NestSpots.Add(new Vector3(182, 0, 130));
        res.PlayerSpawns.Add(new Vector3(-4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(4, 1, 6));
        res.PlayerSpawns.Add(new Vector3(-4, 1, 12));
        res.PlayerSpawns.Add(new Vector3(4, 1, 12));

        CityCam.Attach(root);
        return res;
    }

    // ───────────────────────── Réseau de rues ─────────────────────────

    private static Road AddRoad(Ctx c, float hw, bool walks, bool main = false, bool gravel = false)
    {
        var r = new Road { Index = c.Roads.Count, Hw = hw, Walks = walks, Main = main, Gravel = gravel };
        c.Roads.Add(r);
        return r;
    }

    /// <summary>Rue est-ouest z = f(x), parcourue vers +X.</summary>
    private static Road RoadZ(Ctx c, Func<float, float> z, float x0, float x1, float hw, bool walks, bool main = false)
    {
        var r = AddRoad(c, hw, walks, main);
        var n = Mathf.CeilToInt((x1 - x0) / 2f);
        for (var i = 0; i <= n; i++)
        {
            var x = Mathf.Lerp(x0, x1, i / (float)n);
            r.Add(new Vector3(x, 0, z(x)));
        }
        return r;
    }

    /// <summary>Rue nord-sud x = f(z), parcourue de z0 vers z1.</summary>
    private static Road RoadX(Ctx c, Func<float, float> x, float z0, float z1, float hw, bool walks, bool gravel = false)
    {
        var r = AddRoad(c, hw, walks, false, gravel);
        var n = Mathf.CeilToInt(Mathf.Abs(z1 - z0) / 2f);
        for (var i = 0; i <= n; i++)
        {
            var z = Mathf.Lerp(z0, z1, i / (float)n);
            r.Add(new Vector3(x(z), 0, z));
        }
        return r;
    }

    /// <summary>Rue nord-sud raccordée à deux rues est-ouest (extrémités sur leurs axes).</summary>
    private static Road Connector(Ctx c, Func<float, float> x, Func<float, float> zFrom, Func<float, float> zTo, float hw, bool walks)
    {
        var z0 = zFrom(x(zFrom(x(0))));
        var z1 = zTo(x(zTo(x(0))));
        return RoadX(c, x, z0, z1, hw, walks);
    }

    private static Road _grande, _r3, _r5, _r6w, _r6e, _r1, _r2, _r4, _r7, _r8, _r11, _r12, _r13, _r14, _cds1, _cds2;

    private static void BuildRoadNetwork(Ctx c)
    {
        Func<float, float> r3z = x => -145f + 4f * Mathf.Sin(x * 0.02f);
        Func<float, float> r5z = x => -90f + 9f * Mathf.Sin(x * 0.011f + 0.5f);
        Func<float, float> r6z = x => -40f + 7f * Mathf.Sin(x * 0.014f + 1.3f);
        Func<float, float> r13z = x => 134f + 4f * Mathf.Sin(x * 0.015f);
        Func<float, float> r4x = z => -138f + 3f * Mathf.Sin(z * 0.025f);
        Func<float, float> r2x = z => -38f + 3f * Mathf.Sin(z * 0.018f + 1f);
        Func<float, float> r1x = z => 32f + 3f * Mathf.Sin(z * 0.02f);
        Func<float, float> r7x = z => 90f + 10f * Mathf.Sin(z * 0.014f);
        Func<float, float> r8x = z => 155f + 8f * Mathf.Sin(z * 0.017f + 2f);
        Func<float, float> r11x = z => -60f + 2f * Mathf.Sin(z * 0.05f);
        Func<float, float> r12x = z => 100f + 2f * Mathf.Sin(z * 0.04f + 1f);
        Func<float, float> r14x = z => 60f + 4f * Mathf.Sin(z * 0.03f);
        Func<float, float> gz = GrandeZ;

        // La grande rue traverse toute la carte d'ouest en est, au sud de la place.
        _grande = RoadZ(c, gz, -300f, 300f, 4.5f, true, true);
        _r5 = RoadZ(c, r5z, r4x(r5z(-138)), 236f, 3.25f, true);
        _r3 = RoadZ(c, r3z, r4x(r3z(-138)), 236f, 3f, false);
        _r6w = RoadZ(c, r6z, r4x(r6z(-138)), r2x(r6z(-38)), 3.25f, true);
        _r6e = RoadZ(c, r6z, r1x(r6z(32)), 236f, 3.25f, true);
        _r13 = RoadZ(c, r13z, -236f, 236f, 3f, false);

        _r4 = Connector(c, r4x, gz, r3z, 3f, false);
        _r2 = Connector(c, r2x, gz, r3z, 3.5f, true);
        _r1 = Connector(c, r1x, gz, r3z, 3.5f, true);
        _r7 = Connector(c, r7x, gz, r3z, 3.25f, true);
        _r8 = Connector(c, r8x, gz, r3z, 3.25f, true);
        // Routes du sud : franchissent la rivière par un pont et sortent de la carte.
        _r11 = RoadX(c, r11x, gz(r11x(36)), 176f, 3.5f, true);
        _r12 = RoadX(c, r12x, gz(r12x(36)), 176f, 3.5f, true);
        // Chemin forestier en gravier vers le nord.
        _r14 = RoadX(c, r14x, r3z(r14x(-145)), -250f, 1.8f, false, true);

        // Impasses à raquette (lotissements) desservies par les rues nord-sud de l'est.
        Func<float, float> c1z = x => 2f + 5f * Mathf.Sin((x - 90f) * 0.05f);
        Func<float, float> c2z = x => -117f + 4f * Mathf.Sin((x - 155f) * 0.06f);
        _cds1 = RoadZ(c, c1z, r7x(c1z(r7x(2f))), 136f, 3f, true);
        _cds2 = RoadZ(c, c2z, r8x(c2z(r8x(-117f))), 204f, 3f, true);
        _cds1.Bulb = _cds2.Bulb = 8.5f;
        _cds1.StartJoin = _r7;
        _cds2.StartJoin = _r8;

        foreach (var r in new[] { _r2, _r1, _r7, _r8, _r11, _r12 }) { r.StartJoin = _grande; r.Stop = true; }
        foreach (var r in new[] { _r2, _r1, _r7, _r8 }) r.EndJoin = _r3;
        _r6w.StartJoin = _r4; _r6w.EndJoin = _r2;
        _r6e.StartJoin = _r1;
        _r5.StartJoin = _r4;
        _r14.StartJoin = _r3;

        // Rasterisation dans le masque : chaussée (indice de rue) et emprise (trottoirs, accotements).
        foreach (var r in c.Roads)
        {
            var margin = r.Walks ? 2.6f : 1.2f;
            for (var i = 0; i + 1 < r.P.Count; i++)
            {
                var a = r.P[i];
                var b = r.P[i + 1];
                var w = r.Hw + margin;
                var minX = Mathf.Min(a.X, b.X) - w;
                var maxX = Mathf.Max(a.X, b.X) + w;
                var minZ = Mathf.Min(a.Z, b.Z) - w;
                var maxZ = Mathf.Max(a.Z, b.Z) + w;
                for (var z = Mathf.FloorToInt(minZ); z <= Mathf.CeilToInt(maxZ); z++)
                for (var x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
                {
                    if (!Grid.Cell(x, z, out var idx)) continue;
                    var p = new Vector3(x + 0.5f, 0, z + 0.5f);
                    var d = DistSeg(p, a, b);
                    if (d > w) continue;
                    c.Mask.C[idx] = RoadC;
                    if (d <= r.Hw && c.Mask.Asphalt[idx] == 0) c.Mask.Asphalt[idx] = (short)(r.Index + 1);
                }
            }
            if (r.Bulb <= 0) continue;
            var e = r.P[^1];
            var rw = r.Bulb + margin;
            for (var z = Mathf.FloorToInt(e.Z - rw); z <= Mathf.CeilToInt(e.Z + rw); z++)
            for (var x = Mathf.FloorToInt(e.X - rw); x <= Mathf.CeilToInt(e.X + rw); x++)
            {
                if (!Grid.Cell(x, z, out var idx)) continue;
                var d = new Vector2(x + 0.5f - e.X, z + 0.5f - e.Z).Length();
                if (d > rw) continue;
                c.Mask.C[idx] = RoadC;
                if (d <= r.Bulb && c.Mask.Asphalt[idx] == 0) c.Mask.Asphalt[idx] = (short)(r.Index + 1);
            }
        }
    }

    private static float DistSeg(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(ab.LengthSquared(), 1e-6f), 0, 1);
        var q = a + ab * t;
        return new Vector2(p.X - q.X, p.Z - q.Z).Length();
    }

    private static void MarkRiverAndForest(Ctx c)
    {
        var m = c.Mask;
        for (var i = 0; i < Grid.W; i++)
        {
            var x = i + Grid.X0 + 0.5f;
            var rz = RiverZ(x);
            for (var j = 0; j < Grid.H; j++)
            {
                var z = j + Grid.Z0 + 0.5f;
                var idx = j * Grid.W + i;
                if (m.C[idx] != Free) continue;
                if (Mathf.Abs(z - rz) < RiverHalf + RiverBank + 1f) m.C[idx] = RiverC;
                else if (z < ForestN || x < ForestW) m.C[idx] = ForestC;
                else if (z > SouthEdge - 4f || Mathf.Abs(x) > Half - 4f) m.C[idx] = Reserved;
            }
        }
    }

    private static void ReserveAreas(Ctx c)
    {
        var m = c.Mask;
        m.Fill(-25, -24, 25, 30, Reserved);         // place de l'église (départ des joueurs, véhicules)
        m.Fill(-30, -84, 27, -22, Reserved);        // église, cimetière, jardin du presbytère
        m.Fill(-31, -141, 27, -95, Reserved);       // parc au pied de la forêt
        m.Fill(-68, -18, -42, 10, Reserved);        // mairie et son jardin
        m.Fill(-33, 42, 33, 66, Reserved);          // maisons de bourg face à la place
        m.Fill(-136, 39, -84, 82, Reserved);        // gymnase et parvis
        m.Fill(104, 44, 174, 82, Reserved);         // supérette et parking
        m.Fill(118, -37, 140, -13, Reserved);       // square de quartier (zone est)
        m.Fill(-160, 106, -84, 156, Reserved);      // terrain de football
        m.Fill(-92, 104, -20, 156, Reserved);       // prés (zone sud)
        m.Fill(14, 104, 66, 156, Reserved);         // prés (zone « plage »)
        m.Fill(148, 104, 220, 156, Reserved);       // prés et nid (sud-est)
        m.Fill(-210, -62, -168, -28, Reserved);     // clairière en forêt (nid ouest)
    }

    /// <summary>Côtés bâtis des rues, dans l'ordre de lotissement (+1 : gauche, -1 : droite).</summary>
    private static IEnumerable<(Road, int)> HouseSides(Ctx c)
    {
        // Impasses d'abord : leurs deux rives doivent trouver la place au cœur des îlots.
        foreach (var r in new[] { _cds1, _cds2 })
        {
            yield return (r, 1);
            yield return (r, -1);
        }
        yield return (_grande, 1);
        yield return (_grande, -1);
        yield return (_r5, 1);
        yield return (_r5, -1);
        yield return (_r6e, 1);
        yield return (_r6e, -1);
        yield return (_r6w, 1);
        yield return (_r6w, -1);
        yield return (_r3, -1);      // côté sud seulement (la forêt au nord)
        yield return (_r4, -1);      // côté est seulement (la forêt à l'ouest)
        foreach (var r in new[] { _r2, _r1, _r7, _r8 })
        {
            yield return (r, 1);
            yield return (r, -1);
        }
        foreach (var r in new[] { _r11, _r12 })
        {
            yield return (r, 1);
            yield return (r, -1);
        }
        yield return (_r13, 1);
        yield return (_r13, -1);
    }

    // ───────────────────────── Couleurs ─────────────────────────

    private static readonly Color[] WallColors =
    {
        new(0.87f, 0.81f, 0.7f), new(0.91f, 0.88f, 0.8f), new(0.8f, 0.79f, 0.76f), new(0.83f, 0.68f, 0.48f),
        new(0.86f, 0.74f, 0.68f), new(0.93f, 0.92f, 0.89f), new(0.78f, 0.71f, 0.58f), new(0.88f, 0.83f, 0.63f),
    };

    private static readonly Color[] RoofColors =
    {
        new(0.6f, 0.3f, 0.2f), new(0.5f, 0.26f, 0.19f), new(0.38f, 0.25f, 0.2f), new(0.3f, 0.31f, 0.34f),
        new(0.22f, 0.23f, 0.25f), new(0.68f, 0.38f, 0.24f), new(0.56f, 0.34f, 0.24f),
    };

    private static readonly Color[] TrimColors =
    {
        new(0.92f, 0.92f, 0.9f), new(0.45f, 0.55f, 0.62f), new(0.48f, 0.57f, 0.45f), new(0.45f, 0.16f, 0.15f),
        new(0.24f, 0.25f, 0.27f), new(0.46f, 0.31f, 0.19f), new(0.7f, 0.7f, 0.68f), new(0.26f, 0.38f, 0.55f),
    };

    private static readonly Color[] CarColors =
    {
        new(0.7f, 0.7f, 0.72f), new(0.08f, 0.08f, 0.09f), new(0.55f, 0.08f, 0.06f), new(0.12f, 0.2f, 0.4f),
        new(0.85f, 0.85f, 0.83f), new(0.25f, 0.27f, 0.28f), new(0.4f, 0.35f, 0.2f), new(0.6f, 0.62f, 0.64f),
    };

    private static Color Pick(Color[] a, RandomNumberGenerator r) => a[r.RandiRange(0, a.Length - 1)];

    // ───────────────────────── Bâtiments ─────────────────────────

    private static Building AddBuilding(Ctx c, Vector3 center, Vector3 size, BuildingStyle st, float hp, Material facade = null)
    {
        st.Kind = BuildingKind.House;
        st.Night = c.Night;
        var b = Building.Create(c.NextId++, center, size, facade ?? Mats.Detail(), Mats.RoofTiles(), st);
        b.MaxHealth = b.Health = hp;
        c.Root.AddChild(b);
        c.Res.Buildings.Add(b);
        _houseStyles.Add((b, st)); // pour l'habillage des ruines (pavillons incendiés)
        c.Mask.Fill(center.X - size.X / 2 - 0.5f, center.Z - size.Z / 2 - 0.5f, center.X + size.X / 2 + 0.5f, center.Z + size.Z / 2 + 0.5f, BuiltC, false);
        return b;
    }

    /// <summary>Façade principale vers une direction (vecteur unitaire axial) : 0 = +Z, 1 = -Z, 2 = +X, 3 = -X.</summary>
    private static int FacingOf(Vector3 toStreet) =>
        Mathf.Abs(toStreet.X) > Mathf.Abs(toStreet.Z) ? toStreet.X > 0 ? 2 : 3 : toStreet.Z > 0 ? 0 : 1;

    private static void BuildLandmarks(Ctx c)
    {
        var r = new RandomNumberGenerator { Seed = 777 };
        var stone = new Color(0.8f, 0.76f, 0.66f);
        // Clocher-porche (face à la place) puis nef de l'église.
        AddBuilding(c, new Vector3(0, 0, -26.5f), new Vector3(7.5f, 21f, 7.5f),
            new BuildingStyle { Use = HouseUse.Belfry, Facing = 0, WallColor = stone, RoofColor = new Color(0.27f, 0.29f, 0.32f) }, 1700f, Mats.Masonry());
        AddBuilding(c, new Vector3(0, 0, -45.5f), new Vector3(13f, 9f, 30.5f),
            new BuildingStyle { Use = HouseUse.Church, Facing = 0, WallColor = stone, RoofColor = new Color(0.28f, 0.3f, 0.33f), RoofPitch = 1.05f }, 2400f, Mats.Masonry());
        // Mairie face à la place, de l'autre côté de la rue.
        AddBuilding(c, new Vector3(-51f, 0, -4f), new Vector3(11f, 7.2f, 19f),
            new BuildingStyle { Use = HouseUse.Hall, Facing = 2, Roof = RoofShape.Hip, RoofPitch = 0.75f, WallColor = new Color(0.9f, 0.87f, 0.79f), RoofColor = new Color(0.27f, 0.29f, 0.32f), TrimColor = new Color(0.36f, 0.45f, 0.55f) }, 1200f);
        // Maisons de bourg mitoyennes avec commerces, au sud de la grande rue.
        var x = -31f;
        while (x < 30f)
        {
            var w = r.RandfRange(9.5f, 12.5f);
            if (x + w > 31.5f) w = 31.5f - x;
            if (w < 7f) break;
            var cx = x + w / 2;
            var front = GrandeZ(cx) + 4.5f + 2.3f;
            var d = r.RandfRange(10f, 12f);
            var floors = r.Randf() < 0.5f ? 3 : 2;
            var st = new BuildingStyle
            {
                Use = HouseUse.Home, Facing = 1, Shops = true, Roof = RoofShape.Gable, RoofPitch = r.RandfRange(0.7f, 0.9f),
                WallColor = Pick(WallColors, r), RoofColor = Pick(RoofColors, r), TrimColor = Pick(TrimColors, r),
            };
            AddBuilding(c, new Vector3(cx, 0, front + d / 2), new Vector3(w - 0.05f, floors == 3 ? 8.8f : 6.6f, d), st, 700f);
            x += w;
        }
        // Gymnase et supérette.
        AddBuilding(c, new Vector3(-110f, 0, 61f), new Vector3(40f, 9f, 22f),
            new BuildingStyle { Use = HouseUse.Gym, Facing = 1, WallColor = new Color(0.62f, 0.36f, 0.28f), TrimColor = new Color(0.78f, 0.8f, 0.8f) }, 1800f);
        AddBuilding(c, new Vector3(152f, 0, 64f), new Vector3(34f, 6.5f, 20f),
            new BuildingStyle { Use = HouseUse.Market, Facing = 3, WallColor = new Color(0.82f, 0.82f, 0.8f), TrimColor = new Color(0.75f, 0.2f, 0.12f) }, 1500f);
    }

    /// <summary>
    /// Lotit un côté de rue : lots alignés sur les axes (largeur 17-23 m), maison en retrait, garage éventuel,
    /// allée, clôtures et jardin. Les lots ne chevauchent jamais une emprise de rue, la rivière ou un autre lot.
    /// </summary>
    private static void LineHouses(Ctx c, Road road, int side, RandomNumberGenerator rng)
    {
        // s : début (le long de la rue) du prochain lot ; un lot trop large est rétréci avant d'abandonner.
        var s = 4f;
        while (s < road.Length - 12f)
        {
            var wanted = rng.RandfRange(14.5f, 19.5f);
            var maxD = rng.RandfRange(23f, 30f);
            var placed = false;
            for (var lotW = wanted; lotW >= 13f && !placed; lotW -= 2f)
            {
                var (p, t) = road.At(s + lotW / 2);
                var outward = Vector3.Up.Cross(t) * side;
                var dir = Mathf.Abs(outward.X) > Mathf.Abs(outward.Z) ? new Vector3(Mathf.Sign(outward.X), 0, 0) : new Vector3(0, 0, Mathf.Sign(outward.Z));
                var lat = new Vector3(Mathf.Abs(dir.Z), 0, Mathf.Abs(dir.X)); // axe le long de la rue
                var w = lotW;
                bool RowFree(float d0)
                {
                    var a = p + dir * d0 - lat * (w / 2);
                    var b = p + dir * (d0 + 1f) + lat * (w / 2);
                    return c.Mask.IsFree(Mathf.Min(a.X, b.X), Mathf.Min(a.Z, b.Z), Mathf.Max(a.X, b.X), Mathf.Max(a.Z, b.Z));
                }
                var f = road.Edge * 0.9f;
                var tries = 0;
                while (!RowFree(f) && tries < 7) { f += 1f; tries++; }
                if (!RowFree(f)) continue;
                // Profondeur libre ; si l'îlot est assez profond, on en laisse la moitié aux lots d'en face.
                var avail = 0f;
                while (avail < 64f && RowFree(f + avail)) avail += 1f;
                if (avail < 15f) continue;
                var depth = avail >= 34f ? Mathf.Min(maxD, Mathf.Floor(avail / 2f)) : Mathf.Min(avail, 32f);
                PlaceLot(c, rng, p, dir, lat, lotW, f, depth);
                s += lotW;
                placed = true;
            }
            if (!placed) s += 2f;
        }
    }

    private static void PlaceLot(Ctx c, RandomNumberGenerator rng, Vector3 p, Vector3 dir, Vector3 lat, float lotW, float f, float depth)
    {
        var lotRect = new Rect2();
        {
            var a = p + dir * f - lat * (lotW / 2);
            var b = p + dir * (f + depth) + lat * (lotW / 2);
            lotRect = new Rect2(Mathf.Min(a.X, b.X), Mathf.Min(a.Z, b.Z), Mathf.Abs(b.X - a.X), Mathf.Abs(b.Z - a.Z));
        }
        // Voisins déjà lotis (pour ne pas doubler les clôtures mitoyennes).
        bool LotAt(Vector3 q) => c.Mask.Get(q.X, q.Z) == LotC;
        var negNeighbor = LotAt(p + dir * (f + depth / 2) - lat * (lotW / 2 + 0.7f));
        var posNeighbor = LotAt(p + dir * (f + depth / 2) + lat * (lotW / 2 + 0.7f));
        var backNeighbor = LotAt(p + dir * (f + depth + 0.7f));
        c.Mask.Fill(lotRect.Position.X, lotRect.Position.Y, lotRect.End.X, lotRect.End.Y, LotC);

        // Maison : 2 niveaux (ou plain-pied), retrait de 4,5 à 7 m.
        var yard = rng.RandfRange(5.5f, 7.5f);
        var hd = rng.RandfRange(8f, 10f);
        var hwid = rng.RandfRange(8.5f, Mathf.Min(11.5f, lotW - 4.5f));
        var bungalow = rng.Randf() < 0.22f;
        var height = bungalow ? 3.1f : 5.7f;
        var garage = rng.Randf() < 0.5f && hwid + 4.6f <= lotW - 2.6f;
        var total = hwid + (garage ? 4.6f : 0f);
        var startMin = -lotW / 2 + 1.3f;
        var startMax = lotW / 2 - 1.3f - total;
        var start = Mathf.Clamp(rng.RandfRange(startMin, startMax), startMin, Mathf.Max(startMin, startMax));
        var garageFirst = rng.Randf() < 0.5f;
        var houseLat = garage && garageFirst ? start + 4.6f + hwid / 2 : start + hwid / 2;
        var garLat = garageFirst ? start + 1.7f : start + hwid + 1.2f + 1.7f;
        if (hd + yard + 4f > depth) yard = Mathf.Max(4.5f, depth - hd - 4f);
        var facing = FacingOf(-dir);
        var alongX = Mathf.Abs(dir.Z) > 0.5f;
        Vector3 Sz(float along, float h, float deep) => alongX ? new Vector3(along, h, deep) : new Vector3(deep, h, along);
        Vector3 L(float latv, float d) => p + lat * latv + dir * (f + d);

        var hip = rng.Randf() < 0.3f;
        var st = new BuildingStyle
        {
            Use = HouseUse.Home, Facing = facing, Roof = hip ? RoofShape.Hip : RoofShape.Gable,
            RoofPitch = hip ? rng.RandfRange(0.6f, 0.78f) : rng.RandfRange(0.68f, 0.95f),
            WallColor = Pick(WallColors, rng), RoofColor = Pick(RoofColors, rng), TrimColor = Pick(TrimColors, rng),
        };
        var size = Sz(hwid, height, hd);
        AddBuilding(c, L(houseLat, yard + hd / 2), size, st, 240f + hwid * hd * 1.4f);
        if (garage)
        {
            var gst = st;
            gst.Use = HouseUse.Garage;
            gst.Roof = RoofShape.Flat;
            AddBuilding(c, L(garLat, yard + 0.6f + 3f), Sz(3.4f, 2.7f, 6f), gst, 170f);
        }

        var g = c.Ground.At(L(0, depth / 2));
        // Allée carrossable (vers le garage ou une place de stationnement) et chemin d'entrée.
        var driveLat = garage ? garLat : Mathf.Clamp(houseLat + (garageFirst ? -1 : 1) * (hwid / 2 + 2.2f), -lotW / 2 + 1.8f, lotW / 2 - 1.8f);
        var driveEnd = garage ? yard + 0.6f : yard + hd * 0.6f;
        if (!garage && Mathf.Abs(driveLat - houseLat) < hwid / 2 + 1.6f) driveEnd = yard - 0.4f; // pas la place sur le côté
        var tile = rng.Randf() < 0.5f;
        g.Col = tile ? new Color(0.78f, 0.74f, 0.68f) : new Color(0.62f, 0.6f, 0.56f);
        FlatRect(g, tile ? Mats.Sidewalk() : Mats.Gravel(), L(driveLat - 1.5f, -0.6f), L(driveLat + 1.5f, driveEnd), 0.035f);
        var walkLat = houseLat;
        if (Mathf.Abs(walkLat - driveLat) < 2.5f) walkLat = driveLat;
        else
        {
            g.Col = new Color(0.74f, 0.72f, 0.68f);
            FlatRect(g, Mats.Sidewalk(), L(walkLat - 0.6f, -0.6f), L(walkLat + 0.6f, yard), 0.04f);
        }
        if (!garage || rng.Randf() < 0.3f)
        {
            var col = Pick(CarColors, rng);
            var cp = L(driveLat, Mathf.Max(3f, driveEnd - 2.8f));
            AddCar(c, cp, Mathf.Atan2(dir.X, dir.Z) + (rng.Randf() < 0.5f ? 0 : Mathf.Pi), rng.Randf() < 0.15f ? Props.VehicleType.Van : Props.VehicleType.Car, col, rng.RandfRange(4.0f, 4.6f), rng);
        }

        // Clôtures : façade basse (haie, muret à grille ou palissade) avec ouvertures, limites séparatives hautes.
        var frontKind = rng.RandiRange(0, 2);
        var gaps = new List<(float, float)> { (driveLat - 1.7f, driveLat + 1.7f), (walkLat - 0.8f, walkLat + 0.8f) };
        gaps.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        var x0 = -lotW / 2;
        foreach (var (g0, g1) in gaps)
        {
            if (g0 > x0) Boundary(c, rng, L(x0, 0.4f), L(g0, 0.4f), frontKind, true, st.WallColor);
            x0 = Mathf.Max(x0, g1);
        }
        if (x0 < lotW / 2) Boundary(c, rng, L(x0, 0.4f), L(lotW / 2, 0.4f), frontKind, true, st.WallColor);
        var sideKind = rng.RandiRange(0, 2);
        Boundary(c, rng, L(lotW / 2 - 0.2f, 0.4f), L(lotW / 2 - 0.2f, depth - 0.3f), posNeighbor ? -1 : sideKind, false, st.WallColor);
        if (!negNeighbor) Boundary(c, rng, L(-lotW / 2 + 0.2f, 0.4f), L(-lotW / 2 + 0.2f, depth - 0.3f), sideKind, false, st.WallColor);
        if (!backNeighbor) Boundary(c, rng, L(-lotW / 2 + 0.2f, depth - 0.3f), L(lotW / 2 - 0.2f, depth - 0.3f), sideKind, false, st.WallColor);

        // Boîte aux lettres et poubelles au portail.
        c.Props.Add(Props.Mailbox(), L(walkLat + 1.05f, 0.25f), Mathf.Atan2(-dir.X, -dir.Z), range: 110);
        if (rng.Randf() < 0.6f)
        {
            c.Props.Add(Props.WheelieBin(0), L(driveLat + 2.1f, 0.9f), Mathf.Atan2(-dir.X, -dir.Z) + rng.RandfRange(-0.2f, 0.2f), range: 120);
            if (rng.Randf() < 0.6f) c.Props.Add(Props.WheelieBin(1), L(driveLat + 2.8f, 0.9f), Mathf.Atan2(-dir.X, -dir.Z) + rng.RandfRange(-0.2f, 0.2f), range: 120);
        }

        // Jardin de devant : massifs le long de la façade, parfois un arbre.
        var nShrub = rng.RandiRange(1, 3);
        for (var i = 0; i < nShrub; i++)
        {
            var sl = houseLat + rng.RandfRange(-hwid / 2 + 0.8f, hwid / 2 - 0.8f);
            if (Mathf.Abs(sl - walkLat) < 1.3f || Mathf.Abs(sl - driveLat) < 2f) continue;
            c.Props.Add(Props.Shrub(rng.RandiRange(0, 3)), L(sl, yard - 1.1f), rng.RandfRange(0, Mathf.Tau), null, 140, false, rng.RandfRange(0.7f, 1.1f));
        }
        if (yard > 5.5f && rng.Randf() < 0.35f)
        {
            var tl = rng.Randf() < 0.5f ? -lotW / 2 + 2.5f : lotW / 2 - 2.5f;
            if (Mathf.Abs(tl - driveLat) > 3f && Mathf.Abs(tl - walkLat) > 2f) GardenTree(c, rng, L(tl, yard * 0.5f));
        }

        // Jardin de derrière : terrasse, arbres, jeux, abri, parfois une piscine.
        var back0 = yard + hd;
        var bd = depth - back0;
        if (bd < 3.5f) return;
        if (rng.Randf() < 0.7f)
        {
            g.Col = new Color(0.72f, 0.68f, 0.62f);
            FlatRect(g, Mats.Sidewalk(), L(houseLat - hwid * 0.35f, back0), L(houseLat + hwid * 0.35f, back0 + Mathf.Min(3f, bd - 0.8f)), 0.05f);
        }
        var used = new List<Vector3>();
        bool Clear(Vector3 q, float r)
        {
            foreach (var u in used) if (u.DistanceTo(q) < r) return false;
            used.Add(q);
            return true;
        }
        if (bd > 9f && rng.Randf() < 0.1f)
        {
            // Piscine enterrée avec margelle.
            var pc = L(houseLat, back0 + 3.5f + 2.2f);
            var pw = Sz(7f, 0, 3.6f);
            g.Col = new Color(0.86f, 0.85f, 0.8f);
            FlatRect(g, Mats.Sidewalk(), pc - new Vector3(pw.X / 2 + 0.6f, 0, pw.Z / 2 + 0.6f), pc + new Vector3(pw.X / 2 + 0.6f, 0, pw.Z / 2 + 0.6f), 0.06f);
            g.Col = Colors.White;
            FlatRect(g, Mats.Painted(new Color(0.18f, 0.55f, 0.65f), 0.05f, 0.1f), pc - new Vector3(pw.X / 2, 0, pw.Z / 2), pc + new Vector3(pw.X / 2, 0, pw.Z / 2), 0.07f);
            used.Add(pc);
        }
        var nTrees = rng.RandiRange(1, 2);
        for (var i = 0; i < nTrees; i++)
        {
            var q = L(rng.RandfRange(-lotW / 2 + 2.2f, lotW / 2 - 2.2f), back0 + rng.RandfRange(Mathf.Min(3.5f, bd - 1.5f), bd - 1.6f));
            if (Clear(q, 4f)) GardenTree(c, rng, q);
        }
        var roll = rng.Randf();
        var spot = L(rng.RandfRange(-lotW / 2 + 3f, lotW / 2 - 3f), back0 + Mathf.Max(2.5f, bd - 3f));
        if (bd > 5f && Clear(spot, 3.5f))
        {
            var yaw = Mathf.Atan2(dir.X, dir.Z);
            if (roll < 0.25f)
            {
                c.Props.Add(Props.GardenShed(), spot, yaw + Mathf.Pi, range: 150, shadows: true);
                c.Body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Sz(2.4f, 2.4f, 2f) }, Position = spot + Vector3.Up * 1.2f });
            }
            else if (roll < 0.4f) c.Props.Add(Props.Trampoline(), spot, 0, range: 140, shadows: true);
            else if (roll < 0.52f) c.Props.Add(Props.SwingSet(), spot, yaw, range: 140, shadows: true);
        }
    }

    /// <summary>Rectangle plat (coins quelconques) posé au sol à la hauteur <paramref name="y"/>.</summary>
    private static void FlatRect(MeshKit k, Material m, Vector3 a, Vector3 b, float y)
    {
        var x0 = Mathf.Min(a.X, b.X);
        var x1 = Mathf.Max(a.X, b.X);
        var z0 = Mathf.Min(a.Z, b.Z);
        var z1 = Mathf.Max(a.Z, b.Z);
        k.Quad(m, new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), new Vector3(x0, y, z0));
    }

    private static void GardenTree(Ctx c, RandomNumberGenerator rng, Vector3 q)
    {
        var v = rng.RandiRange(0, 4);
        var yaw = rng.RandfRange(0, Mathf.Tau);
        var tint = new Color(1, 1, 1) * rng.RandfRange(0.88f, 1.1f);
        var scale = rng.RandfRange(0.8f, 1.15f);
        var dead = DeadTreeAt(q); // arbre mort une fois sur trois
        c.Props.Add(dead ?? Props.GardenTree(v), q, yaw, tint, 240, true, dead != null ? scale * 0.62f : scale);
        c.Body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.25f, Height = 3f }, Position = q + Vector3.Up * 1.5f });
    }

    /// <summary>
    /// Limite de lot entre deux points (axiale). Types : 0 haie, 1 muret + grille (ou palissade bois sur les côtés),
    /// 2 grillage ; -1 : rien. Les façades sont basses (1 m), les limites séparatives hautes (1,6 m).
    /// </summary>
    private static void Boundary(Ctx c, RandomNumberGenerator rng, Vector3 a, Vector3 b, int kind, bool front, Color wall)
    {
        if (kind < 0) return;
        var len = a.DistanceTo(b);
        if (len < 0.4f) return;
        var dir = (b - a) / len;
        var alongX = Mathf.Abs(dir.X) > 0.5f;
        var k = c.Ground.At((a + b) / 2);
        var h = front ? 1.0f : 1.6f;
        switch (kind)
        {
            case 0:
            {
                // Haie taillée : tronçons de hauteur légèrement variable.
                var n = Mathf.Max(1, Mathf.RoundToInt(len / 2.2f));
                var thick = front ? 0.75f : 0.9f;
                for (var i = 0; i < n; i++)
                {
                    var p = a + dir * ((i + 0.5f) * len / n);
                    var hh = h + rng.RandfRange(-0.08f, 0.12f) + (front ? 0f : 0.2f);
                    k.Col = new Color(0.16f, 0.27f, 0.1f) * rng.RandfRange(0.85f, 1.1f);
                    var seg = len / n + 0.05f;
                    k.Box(Mats.Foliage(), p + Vector3.Up * (hh / 2), alongX ? new Vector3(seg, hh, thick) : new Vector3(thick, hh, seg), null, false);
                }
                break;
            }
            case 1 when front:
            {
                // Muret enduit surmonté d'une grille à barreaux.
                k.Col = wall.Darkened(0.05f);
                k.Box(Mats.Detail(), (a + b) / 2 + Vector3.Up * 0.3f, alongX ? new Vector3(len, 0.6f, 0.25f) : new Vector3(0.25f, 0.6f, len), null, false);
                k.Col = new Color(0.85f, 0.84f, 0.8f);
                k.Box(Mats.Detail(), (a + b) / 2 + Vector3.Up * 0.63f, alongX ? new Vector3(len + 0.04f, 0.06f, 0.32f) : new Vector3(0.32f, 0.06f, len + 0.04f));
                k.Col = new Color(0.14f, 0.15f, 0.16f);
                var m = Mats.DetailMetal();
                k.Tube(m, a + Vector3.Up * 1.15f, b + Vector3.Up * 1.15f, 0.025f, 4);
                for (var t = 0.12f; t < len; t += 0.14f)
                {
                    var q = a + dir * t + Vector3.Up * 0.66f;
                    k.Box(m, q + Vector3.Up * 0.25f, new Vector3(0.02f, 0.5f, 0.02f), null, false, false);
                }
                h = 1.2f;
                break;
            }
            case 1:
            {
                // Palissade en bois (panneaux de 1,8 m entre poteaux).
                var wood = rng.Randf() < 0.5f ? new Color(0.45f, 0.33f, 0.22f) : new Color(0.5f, 0.48f, 0.44f);
                var n = Mathf.Max(1, Mathf.RoundToInt(len / 1.8f));
                for (var i = 0; i <= n; i++)
                {
                    var p = a + dir * (i * len / n);
                    k.Col = wood.Darkened(0.2f);
                    k.Box(Mats.Detail(), p + Vector3.Up * (h / 2 + 0.05f), new Vector3(0.1f, h + 0.1f, 0.1f), null, false);
                }
                k.Col = wood;
                k.Box(Mats.Detail(), (a + b) / 2 + Vector3.Up * (h / 2 + 0.05f), alongX ? new Vector3(len, h - 0.1f, 0.04f) : new Vector3(0.04f, h - 0.1f, len), null, false);
                k.Col = wood.Darkened(0.1f);
                for (var y = 0.35f; y < h; y += 0.3f)
                    k.Box(Mats.Detail(), (a + b) / 2 + Vector3.Up * y, alongX ? new Vector3(len, 0.02f, 0.06f) : new Vector3(0.06f, 0.02f, len), null, false, false);
                break;
            }
            default:
            {
                // Grillage vert sur poteaux.
                var m = Mats.DetailMetal();
                var n = Mathf.Max(1, Mathf.RoundToInt(len / 2.5f));
                k.Col = new Color(0.17f, 0.3f, 0.2f);
                for (var i = 0; i <= n; i++)
                {
                    var p = a + dir * (i * len / n);
                    k.Cylinder(m, p, 0.03f, 0.03f, h * 0.85f, 5, true);
                }
                foreach (var y in new[] { 0.1f, h * 0.42f, h * 0.84f }) k.Tube(m, a + Vector3.Up * y, b + Vector3.Up * y, 0.012f, 3);
                k.Col = new Color(0.2f, 0.32f, 0.22f) * 0.9f;
                k.Box(Mats.Glass(), (a + b) / 2 + Vector3.Up * (h * 0.45f), alongX ? new Vector3(len, h * 0.8f, 0.01f) : new Vector3(0.01f, h * 0.8f, len), null, false, false);
                h *= 0.85f;
                break;
            }
        }
        var size = alongX ? new Vector3(len, h, 0.3f) : new Vector3(0.3f, h, len);
        c.Body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = (a + b) / 2 + Vector3.Up * (h / 2) });
    }

    /// <summary>Véhicule garé (maillage partagé, boîte de collision).</summary>
    private static void AddCar(Ctx c, Vector3 p, float yaw, Props.VehicleType type, Color paint, float len, RandomNumberGenerator rng)
    {
        if (type == Props.VehicleType.Van && rng.Randf() < 0.6f) paint = new Color(0.9f, 0.9f, 0.88f);
        if (RuinCar(c, p, yaw, type, ref paint, len)) return; // épave calcinée à la place
        var size = Props.VehicleSize(type, len);
        c.Root.AddChild(new MeshInstance3D { Mesh = Props.Vehicle(type, paint, len), Position = p, Rotation = new Vector3(0, yaw, 0), VisibilityRangeEnd = 260f });
        c.Body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = p + new Vector3(0, size.Y / 2 + 0.05f, 0), Rotation = new Vector3(0, yaw, 0) });
    }
}
