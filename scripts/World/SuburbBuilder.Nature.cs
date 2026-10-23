using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>Nature de la banlieue : rivière et ponts, terrain vallonné hors carte, forêt en MultiMesh à deux niveaux de détail.</summary>
public static partial class SuburbBuilder
{
    // ───────────────────────── Rivière ─────────────────────────

    private static void BuildRiver(Ctx c)
    {
        var gravel = Mats.Gravel();
        var grass = Mats.GroundTint();
        var water = Mats.RiverWater(c.Time);
        var up = Vector3.Up;
        // Profil en travers (décalage en z, hauteur, couleur) : berge herbeuse, lèvre de galets, lit vaseux.
        var prof = new (float d, float y, Color col)[]
        {
            (-(RiverHalf + RiverBank + 1f), 0.0f, new Color(0.47f, 0.53f, 0.29f)),
            (-(RiverHalf + 1.2f), 0.17f, new Color(0.52f, 0.5f, 0.42f)),
            (-(RiverHalf - 1.5f), 0.02f, new Color(0.4f, 0.37f, 0.3f)),
            (0f, 0.005f, new Color(0.3f, 0.29f, 0.22f)),
            (RiverHalf - 1.5f, 0.02f, new Color(0.4f, 0.37f, 0.3f)),
            (RiverHalf + 1.2f, 0.17f, new Color(0.52f, 0.5f, 0.42f)),
            (RiverHalf + RiverBank + 1f, 0.0f, new Color(0.47f, 0.53f, 0.29f)),
        };
        var wprof = new (float d, float a)[] { (-(RiverHalf + 0.7f), 0f), (-(RiverHalf - 2f), 0.75f), (0f, 0.95f), (RiverHalf - 2f, 0.75f), (RiverHalf + 0.7f, 0f) };
        for (var x = -700f; x < 700f; x += 4f)
        {
            var x1 = x + 4f;
            var z0 = RiverZ(x);
            var z1 = RiverZ(x1);
            var k = c.Ground.At(new Vector3(x, 0, z0));
            for (var i = 0; i + 1 < prof.Length; i++)
            {
                var a = new Vector3(x, prof[i].y, z0 + prof[i].d);
                var b = new Vector3(x1, prof[i].y, z1 + prof[i].d);
                var e = new Vector3(x1, prof[i + 1].y, z1 + prof[i + 1].d);
                var f = new Vector3(x, prof[i + 1].y, z0 + prof[i + 1].d);
                k.TriSmooth(i == 0 || i == prof.Length - 2 ? grass : gravel, a, b, e, up, up, up, prof[i].col, prof[i].col, prof[i + 1].col);
                k.TriSmooth(i == 0 || i == prof.Length - 2 ? grass : gravel, a, e, f, up, up, up, prof[i].col, prof[i + 1].col, prof[i + 1].col);
            }
            for (var i = 0; i + 1 < wprof.Length; i++)
            {
                var ca = new Color(1, 1, 1, wprof[i].a);
                var cb = new Color(1, 1, 1, wprof[i + 1].a);
                var a = new Vector3(x, WaterY, z0 + wprof[i].d);
                var b = new Vector3(x1, WaterY, z1 + wprof[i].d);
                var e = new Vector3(x1, WaterY, z1 + wprof[i + 1].d);
                var f = new Vector3(x, WaterY, z0 + wprof[i + 1].d);
                k.TriSmooth(water, a, b, e, up, up, up, ca, ca, cb);
                k.TriSmooth(water, a, e, f, up, up, up, ca, cb, cb);
            }
        }

        // Roseaux, rochers et arbres des berges (jamais sur les ponts ni les bâtiments).
        var r = new RandomNumberGenerator { Seed = 81 };
        for (var x = -236f; x < 236f; x += 3f)
        {
            var rz = RiverZ(x);
            foreach (var sg in new[] { -1f, 1f })
            {
                if (r.Randf() < 0.45f)
                {
                    var p = new Vector3(x + r.RandfRange(-1, 1), 0, rz + sg * (RiverHalf + r.RandfRange(-0.8f, 1.2f)));
                    if (!NearRoad(c, p, 6f)) c.Props.Add(Props.Reeds(), p, r.RandfRange(0, Mathf.Tau), null, 140, false, r.RandfRange(0.7f, 1.2f));
                }
                if (r.Randf() < 0.08f)
                {
                    var p = new Vector3(x, 0, rz + sg * r.RandfRange(2f, RiverHalf + 1f));
                    if (!NearRoad(c, p, 6f)) c.Props.Add(Props.Rock(r.RandiRange(0, 3)), p, r.RandfRange(0, Mathf.Tau), null, 160, false, r.RandfRange(0.5f, 1.2f));
                }
            }
            if (r.Randf() < 0.17f)
            {
                var sg = r.Randf() < 0.5f ? -1f : 1f;
                var p = new Vector3(x, 0, rz + sg * r.RandfRange(RiverHalf + RiverBank + 1.5f, RiverHalf + RiverBank + 6f));
                var code = c.Mask.Get(p.X, p.Z);
                if (code == BuiltC || c.Mask.AsphaltAt(p.X, p.Z) != 0 || NearRoad(c, p, 7f)) continue;
                StreetTree(c, p, r.RandiRange(0, 4), r.RandfRange(0.75f, 1.05f), r.RandfRange(0, Mathf.Tau), false);
            }
        }

        foreach (var road in new[] { _r11, _r12 }) BuildBridge(c, road);
    }

    private static bool NearRoad(Ctx c, Vector3 p, float d)
    {
        foreach (var r in new[] { _r11, _r12 })
        {
            var (q, _) = r.At(ArcAtZ(r, p.Z));
            if (Mathf.Abs(q.X - p.X) < r.Edge + d) return true;
        }
        return false;
    }

    /// <summary>Abscisse curviligne d'une rue nord-sud à la hauteur z.</summary>
    private static float ArcAtZ(Road r, float z)
    {
        for (var i = 1; i < r.P.Count; i++)
        {
            var za = r.P[i - 1].Z;
            var zb = r.P[i].Z;
            if ((z - za) * (z - zb) <= 0) return r.S[i - 1] + (z - za) / Mathf.Max(Mathf.Abs(zb - za), 0.001f) * Mathf.Sign(zb - za) * (r.S[i] - r.S[i - 1]);
        }
        return z < r.P[0].Z == r.P[^1].Z > r.P[0].Z ? 0 : r.Length;
    }

    /// <summary>
    /// Pont-cadre en maçonnerie : tablier bas (0,6 m) sur la rivière, rampes d'accès, trottoirs, parapets,
    /// ouvertures des buses sur les faces latérales.
    /// </summary>
    private static void BuildBridge(Ctx c, Road r)
    {
        // Point de franchissement : écart minimal entre la rue et l'axe de la rivière.
        var best = 0f;
        var bestD = float.MaxValue;
        for (var s = 0f; s < r.Length; s += 0.5f)
        {
            var (p, _) = r.At(s);
            var d = Mathf.Abs(p.Z - RiverZ(p.X));
            if (d < bestD) { bestD = d; best = s; }
        }
        var (center, t) = r.At(best);
        var lft = Vector3.Up.Cross(t);
        var basis = new Basis(lft, Vector3.Up, t);
        var xf = new Transform3D(basis, center);
        const float lc = 2 * (RiverHalf + RiverBank) + 3f;  // partie horizontale
        const float ramp = 6f;
        const float top = 0.6f;
        var w = r.Hw * 2 + 4f + 0.9f;
        var k = c.Solid.At(center);
        k.Xf = xf;
        var stone = Mats.Masonry();
        k.Col = new Color(0.72f, 0.68f, 0.6f);
        k.Box(stone, new Vector3(0, top / 2 - 0.1f, 0), new Vector3(w, top + 0.2f, lc), null, false);
        // Ouvertures des buses (deux faces), arcs sombres au ras de l'eau.
        var dark = Mats.Detail();
        foreach (var sx in new[] { -1f, 1f })
        for (var i = -2; i <= 2; i++)
        {
            var z = i * 3.6f;
            k.Col = new Color(0.04f, 0.05f, 0.05f);
            k.Box(dark, new Vector3(sx * (w / 2 + 0.005f), 0.3f, z), new Vector3(0.02f, 0.36f, 2.4f), null, false);
            k.Col = new Color(0.82f, 0.78f, 0.7f);
            k.Box(stone, new Vector3(sx * (w / 2 + 0.06f), 0.52f, z), new Vector3(0.12f, 0.1f, 2.8f), null, false);
        }
        // Chaussée et trottoirs sur le tablier et les rampes.
        var asphalt = Mats.Asphalt();
        var walk = Mats.Sidewalk();
        var curb = Mats.Curb();
        float H(float z) => Mathf.Abs(z) <= lc / 2 ? top : Mathf.Lerp(top, 0.02f, (Mathf.Abs(z) - lc / 2) / ramp);
        var zs = new[] { -(lc / 2 + ramp), -lc / 2, lc / 2, lc / 2 + ramp };
        for (var i = 0; i + 1 < zs.Length; i++)
        {
            var za = zs[i];
            var zb = zs[i + 1];
            var ya = H(za) + 0.005f;
            var yb = H(zb) + 0.005f;
            k.Col = Colors.White;
            QuadUp(k, asphalt, new Vector3(-r.Hw, ya, za), new Vector3(r.Hw, ya, za), new Vector3(r.Hw, yb, zb), new Vector3(-r.Hw, yb, zb));
            foreach (var sx in new[] { -1f, 1f })
            {
                QuadUp(k, walk, new Vector3(sx * r.Hw, ya + 0.13f, za), new Vector3(sx * (r.Hw + 2f), ya + 0.13f, za), new Vector3(sx * (r.Hw + 2f), yb + 0.13f, zb), new Vector3(sx * r.Hw, yb + 0.13f, zb));
                QuadFacing(k, curb, new Vector3(sx * r.Hw, ya, za), new Vector3(sx * r.Hw, yb, zb), new Vector3(sx * r.Hw, yb + 0.13f, zb), new Vector3(sx * r.Hw, ya + 0.13f, za), new Vector3(-sx, 0, 0));
                if (i == 1) continue;
                // Flancs des rampes.
                k.Col = new Color(0.72f, 0.68f, 0.6f);
                var zIn = Mathf.Abs(za) < Mathf.Abs(zb) ? za : zb;
                var zOut = Mathf.Abs(za) < Mathf.Abs(zb) ? zb : za;
                k.Tri(stone, new Vector3(sx * w / 2, -0.05f, zIn), new Vector3(sx * w / 2, top + 0.13f, zIn), new Vector3(sx * w / 2, -0.05f, zOut), new Vector3(0, 0, zIn));
                QuadUp(k, walk, new Vector3(sx * (r.Hw + 2f), H(za) + 0.135f, za), new Vector3(sx * w / 2, H(za) + 0.135f, za), new Vector3(sx * w / 2, H(zb) + 0.135f, zb), new Vector3(sx * (r.Hw + 2f), H(zb) + 0.135f, zb));
                k.Col = Colors.White;
            }
        }
        // Parapets en pierre avec couvertine, piles d'extrémité.
        foreach (var sx in new[] { -1f, 1f })
        {
            k.Col = new Color(0.76f, 0.72f, 0.64f);
            k.Box(stone, new Vector3(sx * (w / 2 - 0.22f), top + 0.13f + 0.45f, 0), new Vector3(0.44f, 0.9f, lc), null, false);
            k.Col = new Color(0.84f, 0.81f, 0.74f);
            k.Box(stone, new Vector3(sx * (w / 2 - 0.22f), top + 0.13f + 0.95f, 0), new Vector3(0.56f, 0.12f, lc + 0.1f));
            foreach (var sz in new[] { -1f, 1f })
                k.Box(stone, new Vector3(sx * (w / 2 - 0.22f), top + 0.13f + 0.6f, sz * (lc / 2 + 0.3f)), new Vector3(0.7f, 1.2f, 0.7f));
        }
        k.Xf = Transform3D.Identity;
        // Collision exacte (tablier, rampes, trottoirs, parapets, piles) : secteur solide.
    }

    // ───────────────────────── Terrain ─────────────────────────

    private static readonly FastNoiseLite HillNoise = new() { Seed = 17, Frequency = 0.0045f, FractalOctaves = 4 };
    private static readonly FastNoiseLite PatchNoise = new() { Seed = 23, Frequency = 0.012f, FractalOctaves = 2 };

    /// <summary>Distance hors de l'aire de jeu (0 à l'intérieur).</summary>
    private static float Outside(float x, float z)
    {
        var dx = Mathf.Max(0, Mathf.Abs(x) - 236f);
        var dz = Mathf.Max(Mathf.Max(0, -z - 236f), Mathf.Max(0, z - 176f));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Relief : nul dans l'aire de jeu, collines au-delà, vallée de la rivière et sorties de routes à plat.</summary>
    private static float Height(float x, float z)
    {
        var d = Outside(x, z);
        if (d <= 0) return 0f;
        var h = (HillNoise.GetNoise2D(x, z) * 0.5f + 0.6f) * Mathf.SmoothStep(0, 190, d) * 85f;
        h *= Mathf.SmoothStep(16f, 120f, Mathf.Abs(z - RiverZ(x)));
        if (Mathf.Abs(x) > 230) h *= Mathf.SmoothStep(10f, 60f, Mathf.Abs(z - GrandeZ(x)));
        if (z > 170) h *= Mathf.Min(Mathf.SmoothStep(8f, 50f, Mathf.Abs(x + 60f)), Mathf.SmoothStep(8f, 50f, Mathf.Abs(x - 100f)));
        if (z < -230) h *= Mathf.SmoothStep(5f, 30f, Mathf.Abs(x - 60f));
        return h;
    }

    private static readonly Color Lawn = new(0.42f, 0.55f, 0.27f);
    private static readonly Color Meadow = new(0.5f, 0.54f, 0.3f);
    private static readonly Color ForestFloor = new(0.33f, 0.3f, 0.2f);

    private static Color GroundColor(Ctx c, float x, float z)
    {
        var n = PatchNoise.GetNoise2D(x, z);
        if (Outside(x, z) <= 0)
        {
            var code = c.Mask.Get(x, z);
            return code switch
            {
                LotC or BuiltC => Lawn.Lerp(Meadow, 0.2f + 0.2f * n),
                ForestC => ForestFloor.Lerp(Meadow, Mathf.Clamp(0.1f + n * 0.3f, 0, 0.25f)),
                _ => Meadow.Lerp(Lawn, 0.4f + 0.3f * n),
            };
        }
        // Hors carte : prés et champs au sud, sous-bois ailleurs.
        if (z > 176 && n > 0.05f) return n > 0.3f ? new Color(0.7f, 0.62f, 0.36f) : new Color(0.48f, 0.38f, 0.26f);
        if (Mathf.Abs(z - RiverZ(x)) < 40) return Meadow;
        return ForestFloor.Lerp(new Color(0.25f, 0.3f, 0.16f), 0.5f + n * 0.5f);
    }

    private static void BuildTerrain(Ctx c)
    {
        var mat = Mats.GroundTint();
        // Grille non uniforme : 4 m dans l'aire de jeu, 12 m au-delà.
        var xs = new List<float>();
        for (var v = -372f; v < -252f; v += 12f) xs.Add(v);
        for (var v = -252f; v < 252f; v += 4f) xs.Add(v);
        for (var v = 252f; v <= 372f; v += 12f) xs.Add(v);
        var zs = new List<float>();
        for (var v = -372f; v < -252f; v += 12f) zs.Add(v);
        for (var v = -252f; v < 196f; v += 4f) zs.Add(v);
        for (var v = 196f; v <= 372f; v += 12f) zs.Add(v);
        var nx = xs.Count;
        var nz = zs.Count;
        var pos = new Vector3[nx * nz];
        var col = new Color[nx * nz];
        for (var j = 0; j < nz; j++)
        for (var i = 0; i < nx; i++)
        {
            var x = xs[i];
            var z = zs[j];
            pos[j * nx + i] = new Vector3(x, Height(x, z) - 0.01f, z);
            col[j * nx + i] = GroundColor(c, x, z);
        }
        Vector3 Nrm(int i, int j)
        {
            var l = pos[j * nx + Mathf.Max(i - 1, 0)];
            var r = pos[j * nx + Mathf.Min(i + 1, nx - 1)];
            var d = pos[Mathf.Max(j - 1, 0) * nx + i];
            var u = pos[Mathf.Min(j + 1, nz - 1) * nx + i];
            return (u - d).Cross(r - l).Normalized();
        }
        var nrm = new Vector3[nx * nz];
        for (var j = 0; j < nz; j++)
        for (var i = 0; i < nx; i++)
        {
            var n = Nrm(i, j);
            nrm[j * nx + i] = n.Y < 0 ? -n : n;
        }
        // Secteurs de 30 × 30 sommets pour le culling.
        for (var cj = 0; cj < nz - 1; cj += 30)
        for (var ci = 0; ci < nx - 1; ci += 30)
        {
            var k = new MeshKit();
            for (var j = cj; j < Mathf.Min(cj + 30, nz - 1); j++)
            for (var i = ci; i < Mathf.Min(ci + 30, nx - 1); i++)
            {
                var a = j * nx + i;
                var b = a + 1;
                var e = a + nx + 1;
                var f = a + nx;
                // Cases au-delà du cercle des collines lointaines : laissées à l'anneau extérieur.
                if (new Vector2(pos[a].X, pos[a].Z).Length() > 380f && new Vector2(pos[e].X, pos[e].Z).Length() > 380f) continue;
                k.TriSmooth(mat, pos[a], pos[b], pos[e], nrm[a], nrm[b], nrm[e], col[a], col[b], col[e]);
                k.TriSmooth(mat, pos[a], pos[e], pos[f], nrm[a], nrm[e], nrm[f], col[a], col[e], col[f]);
            }
            if (k.Empty) continue;
            c.Root.AddChild(new MeshInstance3D { Name = $"Terrain_{ci}_{cj}", Mesh = k.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }

        // Anneau de collines lointaines (couvert forestier sombre).
        var ring = new MeshKit();
        const int seg = 96;
        var radii = new[] { 360f, 430f, 520f, 640f, 800f, 1000f, 1250f, 1500f };
        for (var i = 0; i < seg; i++)
        {
            var a0 = i / (float)seg * Mathf.Tau;
            var a1 = (i + 1) / (float)seg * Mathf.Tau;
            for (var rI = 0; rI + 1 < radii.Length; rI++)
            {
                Vector3 P(float a, float rad) => new(Mathf.Cos(a) * rad, Height(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad) - 0.3f, Mathf.Sin(a) * rad);
                Color C(float a, float rad) => GroundColor(c, Mathf.Cos(a) * rad, Mathf.Sin(a) * rad).Lerp(new Color(0.2f, 0.26f, 0.15f), Mathf.Clamp((rad - 500f) / 600f, 0, 0.7f));
                var q0 = P(a0, radii[rI]);
                var q1 = P(a1, radii[rI]);
                var q2 = P(a1, radii[rI + 1]);
                var q3 = P(a0, radii[rI + 1]);
                var n = (q2 - q0).Cross(q1 - q0).Normalized();
                if (n.Y < 0) n = -n;
                ring.TriSmooth(mat, q0, q1, q2, n, n, n, C(a0, radii[rI]), C(a1, radii[rI]), C(a1, radii[rI + 1]));
                ring.TriSmooth(mat, q0, q2, q3, n, n, n, C(a0, radii[rI]), C(a1, radii[rI + 1]), C(a0, radii[rI + 1]));
            }
        }
        c.Root.AddChild(new MeshInstance3D { Name = "FarHills", Mesh = ring.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    // ───────────────────────── Forêt ─────────────────────────

    /// <summary>
    /// Arbres de forêt regroupés par cellule de 96 m : quatre variantes détaillées par essence de près
    /// (ombres portées), une seule version allégée par essence au-delà de <see cref="Near"/> (peu d'appels de rendu).
    /// </summary>
    private sealed class Forest
    {
        private const float Cell = 96f;
        public const float Near = 170f;
        private readonly Dictionary<(int kind, int cx, int cz), List<(Transform3D xf, Color col)>> _near = new();
        private readonly Dictionary<(int kind, int cx, int cz), List<(Transform3D xf, Color col)>> _far = new();

        public void Add(int kind, Vector3 p, float yaw, float scale, Color tint)
        {
            var cx = Mathf.FloorToInt(p.X / Cell);
            var cz = Mathf.FloorToInt(p.Z / Cell);
            var item = (new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * scale), p), tint);
            Put(_near, (kind, cx, cz), item);
            Put(_far, (kind < 4 ? 0 : 4 + kind % 2, cx, cz), item); // feuillus lointains : vivants (4) ou morts (5)
        }

        private static void Put(Dictionary<(int, int, int), List<(Transform3D, Color)>> d, (int, int, int) key, (Transform3D, Color) item)
        {
            if (!d.TryGetValue(key, out var list)) d[key] = list = new List<(Transform3D, Color)>();
            list.Add(item);
        }

        public void Flush(Node3D root)
        {
            foreach (var far in new[] { false, true })
            foreach (var (key, list) in far ? _far : _near)
            {
                var origin = new Vector3((key.cx + 0.5f) * Cell, 0, (key.cz + 0.5f) * Cell);
                var conifer = key.kind < 4;
                var v = key.kind % 4;
                var mesh = conifer ? far ? Props.ConiferFar(0) : Props.Conifer(v) : far ? Props.BroadleafFar(v) : Props.Broadleaf(v);
                var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh, InstanceCount = list.Count };
                for (var i = 0; i < list.Count; i++)
                {
                    var xf = list[i].xf;
                    xf.Origin -= origin;
                    mm.SetInstanceTransform(i, xf);
                    mm.SetInstanceColor(i, list[i].col);
                }
                root.AddChild(new MultiMeshInstance3D
                {
                    Multimesh = mm,
                    Position = origin,
                    CastShadow = far ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
                    VisibilityRangeBegin = far ? Near : 0f,
                    VisibilityRangeEnd = far ? 1000f : Near,
                });
            }
            _near.Clear();
            _far.Clear();
        }
    }

    private static void BuildForest(Ctx c)
    {
        var forest = new Forest();
        var r = new RandomNumberGenerator { Seed = 2024 };
        var mixNoise = new FastNoiseLite { Seed = 29, Frequency = 0.01f };
        var gapNoise = new FastNoiseLite { Seed = 31, Frequency = 0.02f };
        var trunks = new List<Vector3>();
        const float step = 6.5f;
        for (var z = -560f; z < 560f; z += step)
        for (var x = -560f; x < 560f; x += step)
        {
            var p = new Vector3(x + r.RandfRange(-2.6f, 2.6f), 0, z + r.RandfRange(-2.6f, 2.6f));
            var dist = new Vector2(p.X, p.Z).Length();
            var roll = r.Randf();
            var kindRoll = r.Randf();
            var yaw = r.RandfRange(0, Mathf.Tau);
            var scale = r.RandfRange(0.75f, 1.3f);
            var tint = r.RandfRange(0.82f, 1.12f);
            if (dist > 560f) continue;
            var outside = Outside(p.X, p.Z);
            var inside = Mathf.Abs(p.X) < Half + 12 && p.Z > -Half - 12 && p.Z < SouthEdge + 12;
            if (outside <= 0 || inside)
            {
                if (c.Mask.Get(p.X, p.Z) != ForestC) continue;
                if (c.Mask.Get(p.X, p.Z - 6f) != ForestC && c.Mask.Get(p.X + 6f, p.Z) != ForestC && roll < 0.5f) continue; // lisière moins dense
            }
            else
            {
                if (Mathf.Abs(p.Z - RiverZ(p.X)) < 26f) continue;
                if (Mathf.Abs(p.X) > 228 && Mathf.Abs(p.Z - GrandeZ(p.X)) < 12f) continue;
                if (p.Z > 160 && (Mathf.Abs(p.X + 60f) < 8f || Mathf.Abs(p.X - 100f) < 8f)) continue;
                if (p.Z < -150 && Mathf.Abs(p.X - 60f - 4f * Mathf.Sin(p.Z * 0.03f)) < 5f) continue;
                if (p.Z > 176 && PatchNoise.GetNoise2D(p.X, p.Z) > 0.05f) continue;   // champs
                if (dist > 380 && roll < 0.45f) continue;
            }
            if (gapNoise.GetNoise2D(p.X, p.Z) < -0.45f) continue;                          // clairières
            if (roll > 0.9f) continue;
            var coniferShare = 0.4f + 0.45f * mixNoise.GetNoise2D(p.X, p.Z) + (p.Z < -200 ? 0.15f : 0f);
            var conifer = kindRoll < coniferShare;
            var kind = conifer ? r.RandiRange(0, 3) : 4 + r.RandiRange(0, 3);
            p.Y = Height(p.X, p.Z) - 0.15f;
            forest.Add(kind, p, yaw, scale, new Color(tint, tint, tint * 0.95f));
            if (inside) trunks.Add(p);
        }
        forest.Flush(c.Root);
        // Troncs solides dans l'aire de jeu seulement.
        foreach (var t in trunks)
            c.Body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.45f, Height = 6f }, Position = t + Vector3.Up * 3f });

        // Jeunes arbres et buissons en lisière (prés entre les derniers jardins et la forêt).
        for (var i = 0; i < 160; i++)
        {
            var p = new Vector3(r.RandfRange(-232, 232), 0, r.RandfRange(-232, 170));
            if (c.Mask.Get(p.X, p.Z) != Free) continue;
            var nearForest = c.Mask.Get(p.X, p.Z - 8f) == ForestC || c.Mask.Get(p.X - 8f, p.Z) == ForestC;
            if (!nearForest) continue;
            c.Props.Add(Props.Shrub(r.RandiRange(0, 2)), p, r.RandfRange(0, Mathf.Tau), null, 160, false, r.RandfRange(1f, 1.8f));
        }
    }
}
