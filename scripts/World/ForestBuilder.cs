using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Carte « Vallée forestière » (spécification §9.1 : relief, rivière, visibilité réduite). Une vallée encaissée orientée
/// nord-sud : la rivière descend du barrage d'une petite centrale hydroélectrique (nord) en méandres, rapides et gués,
/// sous un pont routier ; la route de la vallée longe la rive est, passe devant la maison forestière (aire de départ,
/// terrasse aplanie) ; une piste monte au camp de bûcherons sur un replat du versant est ; une autre franchit le gué sud
/// vers l'affût du versant ouest. Versants couverts de résineux et de feuillus, falaises et chaos rocheux, arbres
/// abattus, sous-bois, brume dans les fonds.
///
/// <b>Relief réel</b> : la hauteur du sol est une grille de 2 m (<see cref="Heightfield"/>, collision HeightMapShape3D)
/// installée dans <see cref="World.Terrain"/> ; tout ce qui se pose au sol sans physique la lit par
/// <see cref="World.GroundHeight(float, float)"/>. Champ de flux : la rivière (hors gués et pont), le lac et les pentes
/// trop raides sont bloqués (les falaises avec leur hauteur : les grimpeurs les escaladent si c'est plus court).
///
/// Génération déterministe (graines fixes et hachages de position, jamais GD.Randf) : mêmes identifiants chez tous les pairs.
/// </summary>
public static partial class ForestBuilder
{
    private const float Half = CityBuilder.Half;
    private const float South = CityBuilder.SeaStart;

    // ───────────────────────── Grille du relief ─────────────────────────

    /// <summary>Grille de 257 × 257 points au pas de 2 m (x et z de -256 à 256) : collision HeightMapShape3D carrée.</summary>
    private const float GridX0 = -256f, GridZ0 = -256f, GridStep = 2f;
    private const int GridN = 257;

    // ───────────────────────── Rivière ─────────────────────────

    /// <summary>Axe de la rivière (x en fonction de z) : elle coule du nord (barrage) vers le sud.</summary>
    public static float RiverX(float z) => -80f + 16f * Mathf.Sin(0.0105f * z + 0.4f) + 6f * Mathf.Sin(0.027f * z + 1.3f);

    /// <summary>Pente de l'axe (dx/dz).</summary>
    private static float RiverSlope(float z) => 16f * 0.0105f * Mathf.Cos(0.0105f * z + 0.4f) + 6f * 0.027f * Mathf.Cos(0.027f * z + 1.3f);

    /// <summary>Barrage : axe (z), parement amont, pied aval, couronnement, retenue, demi-longueur.</summary>
    public const float DamZ = -206f, DamUp = -211f, DamToe = -195f, DamCrest = 24.5f, LakeY = 22.4f, DamHalf = 46f;

    /// <summary>Gués (z de l'axe de franchissement).</summary>
    public static readonly float[] Fords = { 97f, -134f };

    /// <summary>Pont routier : centre sur l'axe de la rivière.</summary>
    public const float BridgeZ = -70f;

    /// <summary>Niveau de l'eau le long de la rivière : retenue, bassin du pied du barrage, courant, rapides, aval.</summary>
    public static float WaterY(float z)
    {
        if (z < DamZ) return LakeY;
        return Pw(z, (-206f, 10.2f), (-180f, 9.4f), (-20f, 6.4f), (15f, 6.0f), (56f, 3.1f), (260f, 0.5f));
    }

    /// <summary>Demi-largeur du lit : bassin large au pied du barrage, resserrée dans les rapides, élargie aux gués.</summary>
    public static float HalfWidth(float z)
    {
        var w = 9f + 1.4f * Mathf.Sin(z * 0.05f);
        w += 6f * Bump(z, -186f, 14f);           // bassin de dissipation
        w -= 2.2f * Bump(z, 36f, 22f);           // rapides
        foreach (var f in Fords) w += 3.5f * Bump(z, f, 10f);
        return w;
    }

    /// <summary>Profondeur au milieu du lit (sous le niveau de l'eau).</summary>
    public static float Depth(float z)
    {
        var d = 1.7f + 0.9f * Bump(z, -186f, 14f) - 1f * Bump(z, 36f, 20f);
        foreach (var f in Fords) d = Mathf.Lerp(d, 0.32f, Bump(z, f, 7f));
        return d;
    }

    /// <summary>Gué : 1 au droit du gué, 0 au-delà de ±7 m.</summary>
    public static float FordAt(float z)
    {
        var k = 0f;
        foreach (var f in Fords) k = Mathf.Max(k, Bump(z, f, 7f));
        return k;
    }

    /// <summary>Rapides (écume, blocs) : 1 au cœur, 0 hors de la zone.</summary>
    public static float RapidsAt(float z) => Mathf.Max(Bump(z, 36f, 20f), Bump(z, -182f, 12f) * 0.8f);

    // ───────────────────────── Relief analytique ─────────────────────────

    private static readonly FastNoiseLite Hills = new() { Seed = 401, Frequency = 0.0085f, FractalOctaves = 4, NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth };
    private static readonly FastNoiseLite Rough = new() { Seed = 402, Frequency = 0.045f, FractalOctaves = 2 };
    private static readonly FastNoiseLite Ridges = new() { Seed = 403, Frequency = 0.012f, FractalType = FastNoiseLite.FractalTypeEnum.Ridged, FractalOctaves = 3 };
    private static readonly FastNoiseLite Patches = new() { Seed = 404, Frequency = 0.02f, FractalOctaves = 2 };

    /// <summary>Gorge du barrage : 1 sur l'axe du barrage, 0 à 40 m en aval et à ~60 m en amont (la vallée s'ouvre sur la retenue).</summary>
    private static float Gorge(float z) => Mathf.SmoothStep(-165f, -205f, z) * Mathf.SmoothStep(-290f, -232f, z);

    /// <summary>Plancher de la vallée (plaine alluviale) : un peu au-dessus de l'eau, rive du lac au-delà du barrage.</summary>
    private static float Floor(float z)
    {
        var below = WaterY(Mathf.Max(z, DamZ + 0.5f)) + 1.8f;
        return Mathf.Lerp(below, LakeY + 2.2f, Mathf.SmoothStep(DamToe - 4f, DamUp, z));
    }

    /// <summary>Début du versant est (distance à l'axe) : plus loin devant la maison forestière, resserré dans la gorge.</summary>
    private static float EastStart(float z) => Mathf.Lerp(70f + 32f * Mathf.Exp(-(z * z) / (75f * 75f)), 20f, Gorge(z));

    /// <summary>
    /// Relief brut (sans pistes ni plates-formes) : plaine, versants, ondulations, crêtes, falaises, lit de la rivière.
    /// Évalué aussi pour le décor lointain (même fonction : raccord exact).
    /// </summary>
    public static float Raw(float x, float z)
    {
        var rx = RiverX(z);
        var d = x - rx;
        var g = Gorge(z);
        var h = Floor(z);
        float t;
        if (d >= 0)
        {
            var start = EastStart(z);
            t = Mathf.Clamp((d - start) / Mathf.Lerp(190f, 80f, g), 0f, 1f);
            h += Mathf.Lerp(50f, 46f, g) * Mathf.Pow(t, 1.3f);
            // Escarpement rocheux du versant est (au-dessus de la maison forestière, jusqu'au sud).
            h += 11f * Mathf.SmoothStep(158f, 166f, d) * Bump(z, 55f, 75f);
        }
        else
        {
            t = Mathf.Clamp((-d - Mathf.Lerp(22f, 17f, g)) / Mathf.Lerp(125f, 70f, g), 0f, 1f);
            h += Mathf.Lerp(46f, 48f, g) * Mathf.Pow(t, 1.15f);
            // Barre rocheuse de la rive ouest (en face des rapides).
            h += 9f * Mathf.SmoothStep(46f, 53f, -d) * Bump(z, 20f, 45f);
        }
        // Parois de la gorge (falaises) de part et d'autre du barrage.
        h += 15f * Mathf.SmoothStep(30f, 40f, Mathf.Abs(d)) * g;
        // Ondulations et ravines des versants (nulles dans la plaine), petites bosses partout.
        var slopeK = Mathf.SmoothStep(0f, 0.2f, t);
        h += Hills.GetNoise2D(x, z) * 9f * slopeK;
        h -= Mathf.Max(0f, Ridges.GetNoise2D(x * 0.8f, z) - 0.55f) * 22f * slopeK; // ravines
        h += Rough.GetNoise2D(x, z) * (0.45f + 1.4f * slopeK);
        // Lit de la rivière.
        var hw = z < DamZ ? Mathf.Lerp(26f, 40f, Mathf.SmoothStep(DamZ, DamZ - 40f, z)) : HalfWidth(z);
        var bed = (z < DamZ ? LakeY - 6f : WaterY(z) - Depth(z)) + Rough.GetNoise2D(x * 2f, z * 2f) * 0.25f;
        var a = Mathf.Abs(d);
        var k = 1f - Mathf.SmoothStep(hw - 3f, hw + 4.5f, a);
        if (k > 0f)
        {
            // Profil en auge : plus profond au milieu, berges en pente douce.
            var mid = Mathf.SmoothStep(hw, 0f, a);
            var b = Mathf.Lerp(Mathf.Min(h, WaterY(z) + 0.2f), bed, mid * 0.85f + 0.15f);
            h = Mathf.Lerp(h, Mathf.Min(h, b), k);
        }
        return h;
    }

    // ───────────────────────── Pistes et plates-formes ─────────────────────────

    /// <summary>Piste : axe échantillonné, demi-largeur, nature (route gravillonnée ou chemin de terre), profil en long.</summary>
    private sealed class Track
    {
        public readonly List<Vector2> P = new();
        public readonly List<float> S = new();
        public float[] Y;
        public float Hw;
        public bool Main;
        /// <summary>Tronçon sans nivellement (pont) : abscisses de début et de fin.</summary>
        public float SkipA = -1f, SkipB = -1f;

        public float Length => S[^1];

        /// <summary>Boîte englobante de l'axe (rejet rapide des points lointains).</summary>
        public Rect2 Box;

        public void Add(float x, float z)
        {
            var p = new Vector2(x, z);
            S.Add(P.Count == 0 ? 0f : S[^1] + P[^1].DistanceTo(p));
            P.Add(p);
            Box = P.Count == 1 ? new Rect2(p, Vector2.Zero) : Box.Expand(p);
        }

        /// <summary>Point le plus proche : distance, abscisse et hauteur du profil.</summary>
        public bool Nearest(Vector2 q, float maxD, out float dist, out float s, out float y)
        {
            dist = float.MaxValue;
            s = 0;
            y = 0;
            if (!Box.Grow(maxD).HasPoint(q)) return false;
            for (var i = 1; i < P.Count; i++)
            {
                var a = P[i - 1];
                var b = P[i];
                var ab = b - a;
                var l2 = ab.LengthSquared();
                var t = l2 > 0 ? Mathf.Clamp((q - a).Dot(ab) / l2, 0f, 1f) : 0f;
                var dd = q.DistanceTo(a + ab * t);
                if (dd >= dist) continue;
                dist = dd;
                s = Mathf.Lerp(S[i - 1], S[i], t);
                if (Y != null) y = Mathf.Lerp(Y[i - 1], Y[i], t);
            }
            return dist <= maxD;
        }

        public (Vector2 p, Vector2 dir) At(float s)
        {
            for (var i = 1; i < P.Count; i++)
            {
                if (S[i] < s && i < P.Count - 1) continue;
                var k = Mathf.Clamp((s - S[i - 1]) / Mathf.Max(S[i] - S[i - 1], 0.001f), 0f, 1f);
                return (P[i - 1].Lerp(P[i], k), (P[i] - P[i - 1]).Normalized());
            }
            return (P[^1], Vector2.Up);
        }

        public float HeightAt(float s)
        {
            for (var i = 1; i < P.Count; i++)
            {
                if (S[i] < s && i < P.Count - 1) continue;
                var k = Mathf.Clamp((s - S[i - 1]) / Mathf.Max(S[i] - S[i - 1], 0.001f), 0f, 1f);
                return Mathf.Lerp(Y[i - 1], Y[i], k);
            }
            return Y[^1];
        }
    }

    /// <summary>Plate-forme aplanie (terrasse, cour, replat) : rectangle, hauteur, largeur du talus de raccord.</summary>
    private readonly record struct Pad(Rect2 R, float Y, float Margin, bool Gravel);

    /// <summary>Terrasse de la maison forestière (aire de départ) : rectangle aplani et sa hauteur.</summary>
    private static readonly Rect2 Terrace = new(-42f, -44f, 84f, 96f);
    private static readonly Rect2 StationYard = new(38f, -14f, 26f, 36f);

    /// <summary>Aire de départ (joueurs, char en (14, 22), hélicoptère en (-16, 24), escouades) : rien de solide.</summary>
    private static readonly Rect2 SpawnArea = new(-36, -34, 72, 78);

    /// <summary>Pont : centre, axe (vers l'ouest), demi-longueur, demi-largeur, hauteur du tablier.</summary>
    private static Vector2 BridgeC => new(RiverX(BridgeZ), BridgeZ);
    private static Vector2 BridgeDir => new Vector2(-1f, RiverSlope(BridgeZ)).Normalized();
    private const float BridgeHalf = 23f, BridgeHw = 4.4f;

    // ───────────────────────── Contexte ─────────────────────────

    private sealed class Ctx
    {
        public Node3D Root;
        public World W;
        public StaticBody3D Body;
        public CityBuilder.Result Res;
        public TimeOfDay Time;
        public bool Night, Wet;
        public Heightfield Hf;
        public FlowField Flow;
        public float TerraceY;
        public readonly List<Track> Tracks = new();
        public Track MainRoad;
        public readonly List<Pad> Pads = new();
        public readonly PropScatter Props = new();
        public readonly Chunks Ground = new();          // bordures, marquages, détails sans collision
        public readonly Chunks Solid = new(true);       // ouvrages, rochers, troncs couchés (collision exacte)
        public readonly List<Rect2> Taken = new();      // emprises réservées (pas d'arbre ni de décor)
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

    // ───────────────────────── Génération ─────────────────────────

    public static CityBuilder.Result Build(Node3D root, TimeOfDay time)
    {
        var w = root as World ?? World.Current;
        var c = new Ctx
        {
            Root = root, W = w, Time = time, Night = time == TimeOfDay.Night, Res = new CityBuilder.Result(), Flow = w?.Flow,
            Wet = w?.Mission.Weather is Weather.Rain or Weather.Storm,
        };
        c.Body = new StaticBody3D { Name = "Ground", CollisionLayer = 1, CollisionMask = 0 };
        root.AddChild(c.Body);
        c.Taken.Add(SpawnArea);

        PlanTracks(c);          // pistes et plates-formes (avant le relief : nivellement)
        BuildTerrain(c);        // grille de hauteurs, collision, maillage, champ de flux
        if (w != null) w.Terrain = c.Hf;
        BuildRiver(c);          // eau, berges, rapides, gués
        BuildDam(c);            // barrage, déversoir, retenue
        BuildBridge(c);         // pont routier (tablier praticable)
        BuildSites(c);          // maison forestière, camp de bûcherons, centrale (immeubles : identifiants stables)
        BuildRocks(c);          // falaises, chaos, blocs
        BuildWoods(c);          // forêt, sous-bois, arbres couchés, souches
        BuildMist(c);           // brume dans les fonds
        BuildSkyline(c);        // relief et forêt lointains

        c.Ground.Flush(root, "ForGround", false);
        c.Solid.Flush(root, "ForSolid", true, c.Body);
        c.Props.Flush(root);

        // Murs invisibles en bordure (mêmes limites que les autres cartes), assez hauts pour les crêtes.
        CityBuilder.AddWall(c.Body, new Vector3(0, 40, -Half - 10), new Vector3(Half * 2 + 40, 120, 2));
        CityBuilder.AddWall(c.Body, new Vector3(-Half - 10, 40, 0), new Vector3(2, 120, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(Half + 10, 40, 0), new Vector3(2, 120, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(0, 40, South + 8), new Vector3(Half * 2 + 40, 120, 2));

        var res = c.Res;
        Vector3 G(float x, float z) => new(x, c.Hf.Height(x, z), z);
        res.Zones["north"] = G(-42, -150);      // plaine en aval du barrage, rive est
        res.Zones["center"] = G(18, -100);      // route de la vallée, au nord de la maison forestière
        res.Zones["east"] = G(122, -40);        // versant est, sous l'escarpement
        res.Zones["west"] = G(-160, -30);       // forêt de la rive ouest
        res.Zones["south"] = G(26, 140);        // route de la vallée, au sud
        res.Zones["beach"] = G(-122, 112);      // rive ouest, au-delà du gué sud
        foreach (var n in NestSpots) res.NestSpots.Add(G(n.X, n.Y));
        var ty = c.TerraceY;
        res.PlayerSpawns.Add(new Vector3(-4, ty + 1, 6));
        res.PlayerSpawns.Add(new Vector3(4, ty + 1, 6));
        res.PlayerSpawns.Add(new Vector3(-4, ty + 1, 12));
        res.PlayerSpawns.Add(new Vector3(4, ty + 1, 12));

        CityCam.Attach(root);
        AttachProbe(c);
        return res;
    }

    /// <summary>Emplacements de nids : clairières de la rive ouest, versant est, prés du sud, plaine du nord.</summary>
    private static readonly Vector2[] NestSpots = { new(-150, -112), new(118, 34), new(-24, 128), new(-30, -138) };

    // ───────────────────────── Outils ─────────────────────────

    private static float Bump(float v, float center, float width) => Mathf.Exp(-((v - center) * (v - center)) / (width * width));

    /// <summary>Interpolation linéaire par morceaux (abscisses croissantes).</summary>
    private static float Pw(float z, params (float z, float v)[] pts)
    {
        if (z <= pts[0].z) return pts[0].v;
        for (var i = 1; i < pts.Length; i++)
            if (z <= pts[i].z) return Mathf.Lerp(pts[i - 1].v, pts[i].v, (z - pts[i - 1].z) / (pts[i].z - pts[i - 1].z));
        return pts[^1].v;
    }

    private static bool IsTaken(Ctx c, Vector3 p, float r = 0f)
    {
        foreach (var t in c.Taken)
            if (p.X > t.Position.X - r && p.X < t.End.X + r && p.Z > t.Position.Y - r && p.Z < t.End.Y + r) return true;
        return false;
    }

    private static void Take(Ctx c, Vector3 center, float halfX, float halfZ) =>
        c.Taken.Add(new Rect2(center.X - halfX, center.Z - halfZ, halfX * 2, halfZ * 2));

    /// <summary>Distance au bord de la piste la plus proche (négative sur la chaussée).</summary>
    private static float TrackEdge(Ctx c, float x, float z, float maxD = 30f)
    {
        var best = float.MaxValue;
        var q = new Vector2(x, z);
        foreach (var t in c.Tracks)
            if (t.Nearest(q, maxD + t.Hw, out var d, out _, out _)) best = Mathf.Min(best, d - t.Hw);
        return best;
    }

    /// <summary>Distance horizontale à l'axe de la rivière (au signe près : positive à l'est).</summary>
    private static float RiverOffset(float x, float z) => x - RiverX(z);

    /// <summary>Dans l'eau (lit, retenue) ou sur la berge immédiate.</summary>
    private static bool InWater(float x, float z, float margin = 0f) =>
        z < DamUp ? Raw(x, z) < LakeY + 0.3f + margin * 0.2f : Mathf.Abs(RiverOffset(x, z)) < HalfWidth(z) + margin;

    private static Vector3 OnGround(Ctx c, float x, float z, float lift = 0f) => new(x, c.Hf.Height(x, z) + lift, z);

    /// <summary>Hauteur minimale du sol sur un disque (pour poser un objet sans qu'il flotte).</summary>
    private static float MinGround(Ctx c, Vector3 p, float r)
    {
        var m = c.Hf.Height(p.X, p.Z);
        for (var k = 0; k < 6; k++)
        {
            var a = k * Mathf.Tau / 6f;
            m = Mathf.Min(m, c.Hf.Height(p.X + Mathf.Cos(a) * r, p.Z + Mathf.Sin(a) * r));
        }
        return m;
    }

    /// <summary>Bloc du champ de flux (ouvrage plein, falaise : contourné, ou escaladé par les grimpeurs selon sa hauteur).</summary>
    private static void FlowBlock(Ctx c, Vector3 center, Vector3 size, float margin = 0.5f) => c.Flow?.SetBlockedRect(center, size, true, margin);

    // ───────────────────────── Étalonnage ─────────────────────────

    /// <summary>
    /// Lumière de la vallée, après <see cref="Grade.Apply"/> : ciel bas et laiteux, soleil voilé, brouillard vert-de-gris
    /// plus dense dans les fonds (brouillard de hauteur : les crêtes émergent de la brume), nuit d'encre sous la pluie.
    /// </summary>
    public static void ApplyGrade(Godot.Environment env, DirectionalLight3D sun, TimeOfDay time, Weather weather, float fog)
    {
        var wet = weather is Weather.Rain or Weather.Storm;
        if (env.Sky?.SkyMaterial is ShaderMaterial sky)
        {
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
                case TimeOfDay.Day:
                    Clouds(new Color(0.3f, 0.32f, 0.33f), new Color(0.46f, 0.48f, 0.47f), new Color(0.16f, 0.17f, 0.16f),
                        new Color(0.56f, 0.57f, 0.56f), new Color(0.36f, 0.37f, 0.38f), wet ? 0.93f : 0.8f);
                    break;
                case TimeOfDay.Dusk:
                    Clouds(new Color(0.1f, 0.1f, 0.12f), new Color(0.36f, 0.27f, 0.22f), new Color(0.06f, 0.06f, 0.06f),
                        new Color(0.42f, 0.32f, 0.27f), new Color(0.1f, 0.1f, 0.11f), wet ? 0.92f : 0.75f);
                    break;
            }
        }
        switch (time)
        {
            case TimeOfDay.Day:
                sun.RotationDegrees = new Vector3(-34, -20, 0);
                sun.LightEnergy = wet ? 0.55f : 0.95f;
                sun.LightColor = new Color(0.9f, 0.92f, 0.9f);
                env.AmbientLightColor = new Color(0.4f, 0.43f, 0.42f);
                env.AmbientLightEnergy = wet ? 1.35f : 1.05f;
                env.FogLightColor = new Color(0.42f, 0.45f, 0.44f);
                env.FogDensity = fog * 1.4f + 0.003f;
                env.TonemapExposure = 1.35f;
                break;
            case TimeOfDay.Dusk:
                sun.LightEnergy *= wet ? 0.6f : 0.85f;
                env.FogLightColor = new Color(0.33f, 0.29f, 0.27f);
                env.FogDensity = fog * 1.2f + 0.003f;
                break;
            default:
                // Nuit sans lune sous la pluie : lueur froide d'appoint, fonds noyés de brume.
                sun.LightEnergy = 0.22f;
                sun.LightColor = new Color(0.55f, 0.62f, 0.8f);
                env.AmbientLightColor = new Color(0.2f, 0.23f, 0.3f);
                env.AmbientLightEnergy = 1.5f;
                env.TonemapExposure = 1.85f;
                env.FogLightColor = new Color(0.09f, 0.1f, 0.115f);
                env.FogDensity = fog * 0.8f + 0.003f;
                break;
        }
        // Pas de brouillard de hauteur (contre le ciel, il dessinait des bandes sombres dentelées au-dessus des crêtes) :
        // la brume des fonds vient des volumes de brouillard (BuildMist).
        env.FogSkyAffect = time == TimeOfDay.Night ? 0.8f : 0.92f; // ciel noyé : jamais plus sombre que la forêt dans la brume
        env.FogAerialPerspective = 0.55f;
        env.AdjustmentSaturation = 0.46f;
        env.AdjustmentContrast = 1.1f;
        // Outil de développement : « --nofog » (vues aériennes de contrôle du relief).
        if (Screenshot.CmdArg("nofog") != null) env.FogDensity = 0.0004f;
    }

    /// <summary>Facteur du brouillard volumétrique : air chargé d'humidité, plus encore la nuit.</summary>
    public static float VolumetricFactor(TimeOfDay time) => time == TimeOfDay.Night ? 1.9f : 1.5f;
}
