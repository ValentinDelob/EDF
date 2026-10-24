using System;
using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Carte « Réseau souterrain » (spécification §9.1 : tunnels étroits, nids d'insectes, pas de frappes aériennes) :
/// complexe enterré sous la ville. Grande station de correspondance au centre (voûte carrelée, quai central, rames
/// abandonnées), tunnels à double voie vers l'atelier de maintenance (nord-ouest) et les galeries techniques et la
/// salle des pompes (nord-est), couloir de correspondance et puits de ventilation au nord, collecteur d'égout au sud,
/// tronçon effondré et fissure creusée par la colonie vers une chambre de ponte (sud-ouest), et la grande caverne
/// naturelle évidée par les insectes (sud-est) : parois de ruche, grappes d'œufs, résine, excroissances luminescentes.
///
/// Coque : une seule surface « plafond » en champ de hauteur sur une grille de 1 m (voir <see cref="BuildShell"/>) :
/// chaque ouvrage est une région (distance signée) avec son profil de voûte ; la hauteur d'un sommet est la plus haute
/// voûte des régions qui le contiennent, 0 dehors (pied des murs, recalé sur le contour exact). Murs, voûtes, portails
/// aux jonctions et puits en découlent sans cas particulier ; le sol est plat (y = 0, une boîte de collision).
/// Les insectes marchent sur les murs et les voûtes (collision exacte, deux faces) ; le champ de flux (un seul niveau)
/// bloque la roche sans hauteur (infranchissable, même pour les grimpeurs). Occultation : boîtes d'occultation dans la
/// roche entre les galeries (<see cref="AddOccluders"/>).
///
/// Génération déterministe (positions fixes, hachages, jamais GD.Randf) : mêmes identifiants chez tous les pairs.
/// </summary>
public static partial class UndergroundBuilder
{
    private const float Half = CityBuilder.Half;

    /// <summary>Grille fine de la coque : pas de 1 m, origine (-224, -224), jusqu'à z = 160.</summary>
    private const float G = 1f;
    private const float GX0 = -224f, GZ0 = -224f;
    private const int NX = 448, NZ = 384;
    /// <summary>Secteurs de la coque (culling, collision) : 32 m.</summary>
    private const int Chunk = 32;

    /// <summary>Hauteur des quais au-dessus des voies.</summary>
    public const float PlatformH = 0.9f;

    /// <summary>Emprise de la grande caverne : la reine n'en sort pas (<see cref="CityBuilder.Result.BossArea"/>).</summary>
    public static readonly Rect2 CavernArea = new(114, 60, 100, 94);

    private enum Kind { Tunnel, Station, Corridor, Gallery, Hall, Sewer, Natural, Cavern, Shaft, Collapse }

    /// <summary>Ouvrage creusé : forme (distance signée, négative dedans) et profil de voûte.</summary>
    private sealed class Region
    {
        public string Name;
        public Kind Kind;
        /// <summary>Pied de voûte, faîte, distance au mur à laquelle le faîte est atteint.</summary>
        public float Hw, Hc, R;
        /// <summary>Relief naturel de la voûte (fraction du faîte).</summary>
        public float Rough;
        /// <summary>Puits : rayon intérieur (distance au mur) où la cheminée commence.</summary>
        public float ShaftFrom = -1f;
        /// <summary>Parois de ruche possibles (masque de bruit).</summary>
        public bool Organic;
        public Func<float, float, float> Sdf;
        public Rect2 Box;

        public bool Natural => Kind is Kind.Natural or Kind.Cavern or Kind.Collapse;
    }

    private sealed class Ctx
    {
        public Node3D Root;
        public StaticBody3D Body;
        public CityBuilder.Result Res;
        public FlowField Flow;
        public readonly List<Region> Regions = new();
        public readonly PropScatter Props = new();
        public RuinKit Ruins;
        /// <summary>Décor fusionné par secteurs : solide (collision exacte) et traversable.</summary>
        public readonly Chunks Solid = new(true);
        public readonly Chunks Dressing = new();
        public readonly Chunks Glows = new();
        public int Lights;
    }

    /// <summary>Décor fusionné par secteurs carrés (culling par secteur), collision exacte en option.</summary>
    private sealed class Chunks
    {
        private const float Size = 48f;
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
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var c = new Ctx { Root = root, Res = new CityBuilder.Result(), Flow = World.Current?.Flow };
        c.Body = new StaticBody3D { Name = "Ground", CollisionLayer = 1, CollisionMask = 0 };
        root.AddChild(c.Body);
        c.Ruins = new RuinKit(root, c.Body, 0x5B7E, true);

        Layout(c);
        BuildShell(c);
        var tShell = clock.ElapsedMilliseconds;
        BlockFlow(c);
        BuildStation(c);       // quai, piliers, escalators, rames
        BuildTunnels(c);       // voies, câbles, lampes de secours, rames accidentées
        BuildDepot(c);         // atelier de maintenance
        BuildGalleries(c);     // galeries techniques, salle des pompes, puits, couloir, collecteur
        BuildHive(c);          // caverne, chambre de ponte, fissure, éboulement, ruche
        BuildLights(c);

        c.Solid.Flush(root, "UndSolid", true, c.Body);
        c.Dressing.Flush(root, "UndDress", false, null, 140f);
        c.Glows.Flush(root, "UndGlow", false, null, 160f);
        c.Props.Flush(root);
        c.Ruins.Flush();
        AddOccluders(c);

        // Sol plat (collision) et murs invisibles en bordure (mêmes limites que les autres cartes).
        CityBuilder.AddWall(c.Body, new Vector3(0, -0.5f, -32f), new Vector3(Half * 2 + 40, 1f, 420f));
        CityBuilder.AddWall(c.Body, new Vector3(0, 20, -Half - 10), new Vector3(Half * 2 + 40, 60, 2));
        CityBuilder.AddWall(c.Body, new Vector3(-Half - 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(Half + 10, 20, 0), new Vector3(2, 60, Half * 2 + 100));
        CityBuilder.AddWall(c.Body, new Vector3(0, 20, CityBuilder.SeaStart + 8), new Vector3(Half * 2 + 40, 60, 2));

        var res = c.Res;
        res.Zones["north"] = new Vector3(0, 0, -104);     // salle du puits de ventilation
        res.Zones["center"] = new Vector3(-40, 0, 96);    // salle de décantation du collecteur
        res.Zones["west"] = new Vector3(-178, 0, -166);   // atelier de maintenance
        res.Zones["east"] = new Vector3(164, 0, -152);    // salle des pompes
        res.Zones["south"] = new Vector3(-168, 0, 120);   // chambre de ponte
        res.Zones["beach"] = new Vector3(156, 0, 104);    // grande caverne
        foreach (var n in NestSpots) res.NestSpots.Add(n);
        // Départ sur le quai central de la station (les escouades attendent sur les voies, au niveau du sol).
        res.PlayerSpawns.Add(new Vector3(-4, PlatformH + 1, -6));
        res.PlayerSpawns.Add(new Vector3(4, PlatformH + 1, -6));
        res.PlayerSpawns.Add(new Vector3(-4, PlatformH + 1, 0));
        res.PlayerSpawns.Add(new Vector3(4, PlatformH + 1, 0));
        res.BossArea = CavernArea;

        CityCam.Attach(root);
        // Caméra de contrôle : lampe de casque comme un soldat (sinon les vues ne montrent que les lampes du décor).
        if (root.GetNodeOrNull<Camera3D>("CityCam") is { } cityCam) AttachTorch(cityCam, true);
        if (Screenshot.CmdArg("fps") != null) GD.Print($"[und] coque {tShell} ms, total {clock.ElapsedMilliseconds} ms, lumières {c.Lights}");
        return res;
    }

    /// <summary>Nids : atelier de maintenance, chambre de ponte, salle des pompes, bord de la grande caverne.</summary>
    private static readonly Vector3[] NestSpots = { new(-178, 0, -176), new(-170, 0, 122), new(164, 0, -150), new(184, 0, 122) };

    // ───────────────────────── Plan ─────────────────────────

    private static Region Add(Ctx c, string name, Kind kind, float hw, float hc, float r, Func<float, float, float> sdf, Rect2 box, float rough = 0f, bool organic = false)
    {
        var reg = new Region { Name = name, Kind = kind, Hw = hw, Hc = hc, R = r, Sdf = sdf, Box = box.Grow(4f), Rough = rough, Organic = organic };
        c.Regions.Add(reg);
        return reg;
    }

    /// <summary>Galerie le long d'une polyligne (demi-largeur <paramref name="half"/>), bruit de paroi optionnel.</summary>
    private static Region Path(Ctx c, string name, Kind kind, float half, float hw, float hc, float r, Vector2[] pts, float wobble = 0f, float rough = 0f, bool organic = false)
    {
        var min = pts[0];
        var max = pts[0];
        foreach (var p in pts) { min = min.Min(p); max = max.Max(p); }
        var box = new Rect2(min, max - min).Grow(half + wobble + 1f);
        var seed = name.Length * 17 + (int)pts[0].X;
        return Add(c, name, kind, hw, hc, r, (x, z) =>
        {
            var q = new Vector2(x, z);
            var d = float.MaxValue;
            for (var i = 0; i + 1 < pts.Length; i++) d = Mathf.Min(d, SegDist(q, pts[i], pts[i + 1]));
            if (wobble > 0f) d += (Fbm(x * 0.09f, z * 0.09f, seed) - 0.5f) * 2f * wobble;
            return d - half;
        }, box, rough, organic);
    }

    private static Region RectR(Ctx c, string name, Kind kind, float x0, float z0, float x1, float z1, float hw, float hc, float r, bool organic = false)
    {
        var cx = (x0 + x1) / 2;
        var cz = (z0 + z1) / 2;
        var hx = (x1 - x0) / 2;
        var hz = (z1 - z0) / 2;
        return Add(c, name, kind, hw, hc, r, (x, z) =>
        {
            var dx = Mathf.Abs(x - cx) - hx;
            var dz = Mathf.Abs(z - cz) - hz;
            var outside = new Vector2(Mathf.Max(dx, 0), Mathf.Max(dz, 0)).Length();
            return outside + Mathf.Min(Mathf.Max(dx, dz), 0);
        }, new Rect2(x0, z0, x1 - x0, z1 - z0), 0f, organic);
    }

    /// <summary>Ellipse (distance approchée) au contour bruité.</summary>
    private static Region Blob(Ctx c, string name, Kind kind, float cx, float cz, float rx, float rz, float hw, float hc, float r, float wobble, float rough, bool organic)
    {
        var seed = (int)(cx * 3 + cz * 7);
        return Add(c, name, kind, hw, hc, r, (x, z) =>
        {
            var k = new Vector2((x - cx) / rx, (z - cz) / rz).Length();
            var d = (k - 1f) * Mathf.Min(rx, rz);
            return d + (Fbm(x * 0.045f, z * 0.045f, seed) - 0.5f) * 2f * wobble;
        }, new Rect2(cx - rx, cz - rz, rx * 2, rz * 2).Grow(wobble), rough, organic);
    }

    private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }

    /// <summary>
    /// Plan du réseau (x vers l'est, z vers le sud ; nord = -z). Aire de jeu commune : |x| ≤ 216, -216 ≤ z ≤ 154.
    /// </summary>
    private static void Layout(Ctx c)
    {
        static Vector2 V(float x, float z) => new(x, z);
        // Station centrale : voûte carrelée de 17 m, voies nord (z -36 → -20) et sud (z 10 → 28), quai central.
        RectR(c, "station", Kind.Station, -84, -36, 84, 28, 8f, 17f, 24f);
        // Tunnels à double voie (16 m, voûte de 10 m) vers l'ouest et l'est.
        Path(c, "tunnel-no", Kind.Tunnel, 8f, 4.5f, 10f, 8f, new[] { V(-80, -28), V(-138, -28), V(-158, -34), V(-171, -46), V(-177, -62), V(-178, -132) });
        Path(c, "tunnel-so", Kind.Tunnel, 8f, 4.5f, 10f, 8f, new[] { V(-80, 19), V(-126, 19), V(-146, 26), V(-158, 40), V(-163, 58), V(-165, 96) });
        Path(c, "tunnel-ne", Kind.Tunnel, 8f, 4.5f, 10f, 8f, new[] { V(80, -28), V(138, -28), V(158, -34), V(171, -46), V(177, -62), V(178, -108) });
        Path(c, "tunnel-se", Kind.Tunnel, 8f, 4.5f, 10f, 8f, new[] { V(80, 19), V(110, 19), V(126, 27), V(136, 42), V(142, 60) });
        // Atelier de maintenance (nord-ouest) : grande halle à plafond plat.
        RectR(c, "depot", Kind.Hall, -214, -208, -142, -128, 10f, 15f, 5f);
        // Galerie technique (z = -104), de l'ouest au réseau nord-est ; rameaux.
        Path(c, "galerie", Kind.Gallery, 4.5f, 4.5f, 6.5f, 3f, new[] { V(-178, -104), V(210, -104) });
        Path(c, "rameau-depot", Kind.Gallery, 4.5f, 4.5f, 6.5f, 3f, new[] { V(-150, -104), V(-150, -130) });
        Path(c, "galerie-n1", Kind.Gallery, 4.5f, 4.5f, 6.5f, 3f, new[] { V(116, -104), V(116, -198), V(210, -198), V(210, -104) });
        Path(c, "galerie-n2", Kind.Gallery, 4.5f, 4.5f, 6.5f, 3f, new[] { V(116, -150), V(210, -150) });
        Path(c, "galerie-n3", Kind.Gallery, 4.5f, 4.5f, 6.5f, 3f, new[] { V(164, -104), V(164, -198) });
        // Salle des pompes (nord-est).
        RectR(c, "pompes", Kind.Hall, 136, -180, 194, -124, 9f, 12.5f, 4f);
        // Couloir de correspondance (station → galerie), salle du puits de ventilation.
        Path(c, "couloir", Kind.Corridor, 5f, 3.6f, 6.2f, 4f, new[] { V(0, -34), V(0, -98) });
        var shaft = Add(c, "puits", Kind.Shaft, 6.5f, 44f, 4f, (x, z) => new Vector2(x, z + 104).Length() - 15f, new Rect2(-15, -119, 30, 30));
        shaft.ShaftFrom = 7.5f;
        // Collecteur d'égout (sud) : descente depuis la station, salle de décantation, galerie est-ouest.
        Path(c, "descente", Kind.Sewer, 5f, 3.2f, 7f, 5f, new[] { V(-40, 26), V(-40, 82) });
        RectR(c, "decantation", Kind.Hall, -62, 80, -18, 112, 7f, 10f, 4f);
        Path(c, "collecteur", Kind.Sewer, 5f, 3.2f, 7.5f, 5f, new[] { V(-136, 96), V(-62, 96), V(-18, 96), V(108, 96) });
        // Tronçon effondré du tunnel sud-ouest : voûte crevée au-dessus de l'éboulis.
        Blob(c, "eboulement", Kind.Collapse, -164, 70, 13, 15, 6f, 17f, 7f, 1.5f, 0.35f, false);
        // Fissure creusée par la colonie (contournement de l'éboulement), chambre de ponte.
        Path(c, "fissure", Kind.Natural, 5.5f, 4f, 9f, 5f, new[] { V(-152, 38), V(-136, 54), V(-134, 76), V(-146, 94) }, 1.6f, 0.3f, true);
        Blob(c, "chambre", Kind.Cavern, -170, 120, 34, 29, 3f, 22f, 20f, 3f, 0.25f, true);
        // Grande caverne (sud-est), évidée par la colonie : trois lobes.
        Blob(c, "caverne", Kind.Cavern, 158, 104, 52, 47, 3f, 44f, 36f, 4.5f, 0.22f, true);
        Blob(c, "caverne-o", Kind.Cavern, 128, 70, 24, 22, 3f, 26f, 20f, 3f, 0.25f, true);
        Blob(c, "caverne-e", Kind.Cavern, 196, 70, 17, 26, 3f, 28f, 16f, 3f, 0.25f, true);
    }

    // ───────────────────────── Coque ─────────────────────────

    private enum Surf : byte { Lining, Tiles, Concrete, Brick, Rock, Hive }

    /// <summary>Distance signée de l'union des régions (négative dedans).</summary>
    private static float Union(Ctx c, float x, float z)
    {
        var u = 9999f;
        foreach (var r in c.Regions)
            if (r.Box.HasPoint(new Vector2(x, z))) u = Mathf.Min(u, r.Sdf(x, z));
        return u;
    }

    /// <summary>Profil de voûte d'une région à la distance <paramref name="d"/> du mur.</summary>
    private static float Vault(Region r, float d, float x, float z)
    {
        var t = Mathf.Clamp(d / r.R, 0f, 1f);
        var arc = Mathf.Sqrt(1f - (1f - t) * (1f - t));
        if (r.ShaftFrom > 0f)
        {
            // Salle basse (voûte de Hw + 2 m) percée au centre d'une cheminée jusqu'à Hc.
            var hall = r.Hw + 2f * arc;
            return Mathf.Lerp(hall, r.Hc, Mathf.SmoothStep(r.ShaftFrom, r.ShaftFrom + 2.5f, d));
        }
        var h = r.Hw + (r.Hc - r.Hw) * arc;
        if (r.Natural)
        {
            // Voûte naturelle : profil plus pointu, bosses et poches.
            h = r.Hw + (r.Hc - r.Hw) * Mathf.Pow(t, 0.75f);
            h *= 1f + r.Rough * (Fbm(x * 0.07f, z * 0.07f, 911) - 0.5f) * 2f;
            h += (Fbm(x * 0.35f, z * 0.35f, 913) - 0.5f) * 1.2f;
            h = Mathf.Max(h, 3.2f);
        }
        return h;
    }

    /// <summary>Hauteur de la coque en (x, z) : plus haute voûte des régions qui contiennent le point (0 dans la roche).</summary>
    private static float CeilAt(Ctx c, float x, float z, out Region best)
    {
        best = null;
        var h = 0f;
        foreach (var r in c.Regions)
        {
            if (!r.Box.HasPoint(new Vector2(x, z))) continue;
            var s = r.Sdf(x, z);
            if (s >= 0f) continue;
            var v = Vault(r, -s, x, z);
            if (v > h) { h = v; best = r; }
        }
        return h;
    }

    /// <summary>
    /// Coque du réseau : champ de hauteur sur la grille de 1 m, découpé en secteurs de 32 m (une surface par matière,
    /// collision exacte par secteur). Pied des murs recalé sur le contour exact de l'union ; normales par différences
    /// centrées ; teinte par sommet (assombrie près des murs et dans les recoins).
    /// </summary>
    private static void BuildShell(Ctx c)
    {
        const int vx = NX + 1, vz = NZ + 1;
        var h = new float[vx * vz];
        var pos = new Vector3[vx * vz];
        var inside = new bool[vx * vz];
        var surf = new Surf[NX * NZ];
        var cellIn = new bool[NX * NZ];
        // Sommets : hauteur, position (pied recalé sur le contour).
        System.Threading.Tasks.Parallel.For(0, vz, j =>
        {
            for (var i = 0; i < vx; i++)
            {
                var x = GX0 + i * G;
                var z = GZ0 + j * G;
                var k = j * vx + i;
                var hh = CeilAt(c, x, z, out _);
                h[k] = hh;
                inside[k] = hh > 0f;
                pos[k] = new Vector3(x, hh, z);
            }
        });
        System.Threading.Tasks.Parallel.For(0, NZ, j =>
        {
            for (var i = 0; i < NX; i++)
            {
                var a = j * vx + i;
                var any = inside[a] || inside[a + 1] || inside[a + vx] || inside[a + vx + 1];
                cellIn[j * NX + i] = any;
                if (!any) continue;
                var x = GX0 + (i + 0.5f) * G;
                var z = GZ0 + (j + 0.5f) * G;
                var ch = CeilAt(c, x, z, out var r);
                r ??= NearestRegion(c, x, z);
                surf[j * NX + i] = SurfOf(r, ch);
            }
        });
        // Pied des murs : sommets dehors voisins d'un sommet dedans, poussés sur le contour (≤ 0,75 m).
        System.Threading.Tasks.Parallel.For(1, vz - 1, j =>
        {
            for (var i = 1; i < vx - 1; i++)
            {
                var k = j * vx + i;
                if (inside[k]) continue;
                if (!(inside[k - 1] || inside[k + 1] || inside[k - vx] || inside[k + vx] || inside[k - vx - 1] || inside[k - vx + 1] || inside[k + vx - 1] || inside[k + vx + 1])) continue;
                var x = pos[k].X;
                var z = pos[k].Z;
                const float e = 0.25f;
                var s = Union(c, x, z);
                var gx = (Union(c, x + e, z) - Union(c, x - e, z)) / (2 * e);
                var gz = (Union(c, x, z + e) - Union(c, x, z - e)) / (2 * e);
                var g2 = gx * gx + gz * gz;
                if (g2 < 1e-4f || s > 3f) continue;
                var move = new Vector2(gx, gz) * (-s / g2);
                if (move.Length() > 0.75f * G) move = move.Normalized() * 0.75f * G;
                pos[k] = new Vector3(x + move.X, 0f, z + move.Y);
            }
        });

        float H(int i, int j) => i < 0 || j < 0 || i >= vx || j >= vz ? 0f : h[j * vx + i];
        Vector3 Nrm(int i, int j)
        {
            var n = new Vector3((H(i + 1, j) - H(i - 1, j)) / (2 * G), -1f, (H(i, j + 1) - H(i, j - 1)) / (2 * G));
            return n.Normalized();
        }
        Color Col(int i, int j)
        {
            var k = j * vx + i;
            if (!inside[k]) return new Color(0.45f, 0.45f, 0.45f);
            var x = pos[k].X;
            var z = pos[k].Z;
            var d = Mathf.Clamp(-Union(c, x, z), 0f, 6f);
            var ao = 0.55f + 0.45f * Mathf.SmoothStep(0f, 5f, d);
            var v = 0.85f + 0.3f * Fbm(x * 0.05f, z * 0.05f, 77);
            var t = ao * v;
            return new Color(t, t * 0.98f, t * 0.95f);
        }

        var mats = new Material[] { Mats.TunnelLining(), Mats.StationTiles(), Mats.Concrete(new Color(0.5f, 0.49f, 0.46f), 1801), Mats.FactoryBrick(), Mats.CaveRock(), Mats.HiveWall() };
        var floorMats = new Material[] { Mats.WetFloor(), Mats.WetFloor(), Mats.WetFloor(), Mats.WetFloor(), Mats.CaveFloor(), Mats.CaveFloor() };
        var tris = 0;
        for (var cj = 0; cj < NZ; cj += Chunk)
        for (var ci = 0; ci < NX; ci += Chunk)
        {
            var kit = new MeshKit { Record = true };
            var floor = new MeshKit();
            var any = false;
            for (var j = cj; j < Mathf.Min(cj + Chunk, NZ); j++)
            {
                // Sol : bandes de cellules (dilatées d'une cellule, cachées sous les murs) par matière.
                var runStart = -1;
                var runMat = 0;
                for (var i = ci; i <= Mathf.Min(ci + Chunk, NX); i++)
                {
                    var on = i < Mathf.Min(ci + Chunk, NX) && FloorCell(cellIn, i, j);
                    var m = on ? FloorMat(surf, cellIn, i, j) : -1;
                    if (runStart >= 0 && (!on || m != runMat))
                    {
                        var x0 = GX0 + runStart * G;
                        var x1 = GX0 + i * G;
                        var z0 = GZ0 + j * G;
                        floor.Col = Colors.White;
                        floor.OrientedQuad(floorMats[runMat], new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z0 + G), new Vector3(x0, 0, z0 + G), Vector3.Up);
                        runStart = -1;
                    }
                    if (on && runStart < 0) { runStart = i; runMat = m; }
                }
                for (var i = ci; i < Mathf.Min(ci + Chunk, NX); i++)
                {
                    if (!cellIn[j * NX + i]) continue;
                    any = true;
                    var m = mats[(int)surf[j * NX + i]];
                    int a = j * vx + i, b = a + 1, d = a + vx, e = d + 1;
                    Vector3 pa = pos[a], pb = pos[b], pd = pos[d], pe = pos[e];
                    Vector3 na = Nrm(i, j), nb = Nrm(i + 1, j), nd = Nrm(i, j + 1), ne = Nrm(i + 1, j + 1);
                    Color ca = Col(i, j), cb = Col(i + 1, j), cd = Col(i, j + 1), ce = Col(i + 1, j + 1);
                    // Diagonale la plus courte en hauteur (moins de facettes en dents de scie au pied des murs).
                    if (Mathf.Abs(pa.Y - pe.Y) <= Mathf.Abs(pb.Y - pd.Y))
                    {
                        kit.TriSmooth(m, pa, pb, pe, na, nb, ne, ca, cb, ce);
                        kit.TriSmooth(m, pa, pe, pd, na, ne, nd, ca, ce, cd);
                    }
                    else
                    {
                        kit.TriSmooth(m, pa, pb, pd, na, nb, nd, ca, cb, cd);
                        kit.TriSmooth(m, pb, pe, pd, nb, ne, nd, cb, ce, cd);
                    }
                    tris += 2;
                }
            }
            if (!any) continue;
            CityBuilder.AddSolid(c.Body, kit);
            c.Root.AddChild(new MeshInstance3D { Name = $"Shell_{ci}_{cj}", Mesh = kit.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
            if (!floor.Empty) c.Root.AddChild(new MeshInstance3D { Name = $"Floor_{ci}_{cj}", Mesh = floor.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
        _shellIn = cellIn;
        if (Screenshot.CmdArg("fps") != null) GD.Print($"[und] coque : {tris} triangles");
    }

    /// <summary>Cellules de la coque (dedans) de la dernière génération, pour le sol et l'occultation.</summary>
    private static bool[] _shellIn;

    private static bool FloorCell(bool[] cellIn, int i, int j)
    {
        for (var dj = -1; dj <= 1; dj++)
        for (var di = -1; di <= 1; di++)
        {
            var ii = i + di;
            var jj = j + dj;
            if (ii < 0 || jj < 0 || ii >= NX || jj >= NZ) continue;
            if (cellIn[jj * NX + ii]) return true;
        }
        return false;
    }

    private static int FloorMat(Surf[] surf, bool[] cellIn, int i, int j)
    {
        var s = cellIn[j * NX + i] ? surf[j * NX + i] : Surf.Concrete;
        if (!cellIn[j * NX + i])
            for (var dj = -1; dj <= 1; dj++)
            for (var di = -1; di <= 1; di++)
            {
                var ii = Mathf.Clamp(i + di, 0, NX - 1);
                var jj = Mathf.Clamp(j + dj, 0, NZ - 1);
                if (cellIn[jj * NX + ii]) { s = surf[jj * NX + ii]; break; }
            }
        return s is Surf.Rock or Surf.Hive ? 4 : 0;
    }

    private static Region NearestRegion(Ctx c, float x, float z)
    {
        Region best = c.Regions[0];
        var bs = float.MaxValue;
        foreach (var r in c.Regions)
        {
            if (!r.Box.HasPoint(new Vector2(x, z))) continue;
            var s = r.Sdf(x, z);
            if (s < bs) { bs = s; best = r; }
        }
        return best;
    }

    /// <summary>
    /// Matière de la coque : par ouvrage ; dans les régions organiques, paroi de ruche au-dessus du pied des parois
    /// (la limite suit une courbe de niveau : pas d'escalier de cellules visible). <paramref name="h"/> : hauteur de la coque.
    /// </summary>
    private static Surf SurfOf(Region r, float h)
    {
        if (r.Organic && h > (r.Kind == Kind.Cavern ? 3.6f : 3.2f)) return Surf.Hive;
        return r.Kind switch
        {
            Kind.Tunnel => Surf.Lining,
            Kind.Station or Kind.Corridor => Surf.Tiles,
            Kind.Sewer => Surf.Brick,
            Kind.Natural or Kind.Cavern or Kind.Collapse => Surf.Rock,
            _ => Surf.Concrete,
        };
    }

    // ───────────────────────── Champ de flux ─────────────────────────

    /// <summary>
    /// Un seul niveau : une cellule de 4 m est praticable si au moins la moitié de sa surface est à plus de 1 m des
    /// parois ; la roche est bloquée sans hauteur (infranchissable, même par-dessus pour les grimpeurs).
    /// </summary>
    private static void BlockFlow(Ctx c)
    {
        if (c.Flow == null) return;
        var cell = FlowField.Cell;
        for (var z = -FlowField.Extent + cell / 2; z < FlowField.Extent; z += cell)
        for (var x = -FlowField.Extent + cell / 2; x < FlowField.Extent; x += cell)
        {
            var free = 0;
            for (var sz = 0; sz < 4; sz++)
            for (var sx = 0; sx < 4; sx++)
                if (Union(c, x - cell / 2 + (sx + 0.5f) * cell / 4, z - cell / 2 + (sz + 0.5f) * cell / 4) < -1f) free++;
            if (free < 8) c.Flow.SetBlockedRect(new Vector3(x, 0, z), new Vector3(cell * 0.9f, 0f, cell * 0.9f), true, 0f);
        }
    }

    /// <summary>Bloque une emprise praticable seulement en grimpant (quai, éboulis) : hauteur donnée.</summary>
    private static void FlowObstacle(Ctx c, Vector3 center, Vector3 size) => c.Flow?.SetBlockedRect(center, size, true, 0f);

    // ───────────────────────── Occultation ─────────────────────────

    /// <summary>
    /// Occultation (le moteur ne dessine pas ce qui est caché derrière) : la roche pleine entre les galeries est
    /// couverte de boîtes d'occultation (cellules de 8 m sans aucune cellule de coque à moins de 2 m, fusionnées en
    /// rectangles par lignes puis par colonnes). Elles ne recoupent jamais l'intérieur du réseau.
    /// </summary>
    private static void AddOccluders(Ctx c)
    {
        if (_shellIn == null) return;
        const int s = 8;
        int cx = NX / s, cz = NZ / s;
        var rock = new bool[cx * cz];
        for (var j = 0; j < cz; j++)
        for (var i = 0; i < cx; i++)
        {
            var ok = true;
            for (var jj = j * s - 2; jj < (j + 1) * s + 2 && ok; jj++)
            for (var ii = i * s - 2; ii < (i + 1) * s + 2 && ok; ii++)
            {
                if (ii < 0 || jj < 0 || ii >= NX || jj >= NZ) continue;
                if (_shellIn[jj * NX + ii]) ok = false;
            }
            rock[j * cx + i] = ok;
        }
        var used = new bool[cx * cz];
        var boxes = 0;
        for (var j = 0; j < cz; j++)
        for (var i = 0; i < cx; i++)
        {
            if (!rock[j * cx + i] || used[j * cx + i]) continue;
            var w = 1;
            while (i + w < cx && rock[j * cx + i + w] && !used[j * cx + i + w]) w++;
            var hgt = 1;
            while (j + hgt < cz)
            {
                var row = true;
                for (var k = 0; k < w && row; k++) row = rock[(j + hgt) * cx + i + k] && !used[(j + hgt) * cx + i + k];
                if (!row) break;
                hgt++;
            }
            for (var jj = 0; jj < hgt; jj++)
            for (var ii = 0; ii < w; ii++) used[(j + jj) * cx + i + ii] = true;
            var size = new Vector3(w * s * G, 70f, hgt * s * G);
            var center = new Vector3(GX0 + (i + w / 2f) * s * G, 33f, GZ0 + (j + hgt / 2f) * s * G);
            c.Root.AddChild(new OccluderInstance3D { Name = "Rock", Occluder = new BoxOccluder3D { Size = size }, Position = center });
            boxes++;
        }
        // Occultation activée pour la durée de la mission (remise à zéro par World._ExitTree).
        if (Screenshot.CmdArg("no-occlusion") == null) c.Root.GetViewport().UseOcclusionCulling = true;
        if (Screenshot.CmdArg("fps") != null) GD.Print($"[und] occultation : {boxes} boîtes");
    }

    // ───────────────────────── Bruit ─────────────────────────

    private static float HashI(int a, int b, int c)
    {
        unchecked
        {
            var h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    /// <summary>Bruit de valeur lissé (C#, sans appel moteur), [0, 1].</summary>
    private static float Vn(float x, float z, int seed)
    {
        var ix = Mathf.FloorToInt(x);
        var iz = Mathf.FloorToInt(z);
        var tx = x - ix;
        var tz = z - iz;
        tx = tx * tx * (3 - 2 * tx);
        tz = tz * tz * (3 - 2 * tz);
        var a = Mathf.Lerp(HashI(ix, iz, seed), HashI(ix + 1, iz, seed), tx);
        var b = Mathf.Lerp(HashI(ix, iz + 1, seed), HashI(ix + 1, iz + 1, seed), tx);
        return Mathf.Lerp(a, b, tz);
    }

    /// <summary>Bruit fractal à trois octaves, [0, 1].</summary>
    private static float Fbm(float x, float z, int seed) =>
        Vn(x, z, seed) * 0.55f + Vn(x * 2.03f + 17.1f, z * 2.03f - 9.3f, seed + 1) * 0.3f + Vn(x * 4.1f - 3.7f, z * 4.1f + 5.1f, seed + 2) * 0.15f;

    /// <summary>Hachage stable d'une position → [0, 1[.</summary>
    private static float Hh(float x, float z, int salt) => RuinKit.Hash(x, z, salt);
}
