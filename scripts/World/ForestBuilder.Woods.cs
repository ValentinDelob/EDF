using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Végétation et minéral de la vallée : futaie de résineux et de feuillus en MultiMesh à deux niveaux de détail (cellules
/// de 64 m, épicéas détaillés près de la caméra avec ombres, cônes et boules au-delà), arbres destructibles le long des
/// pistes et autour des lieux (<see cref="PropScatter"/>, famille « arbre »), troncs solides, sous-bois (fougères,
/// buissons, semis), arbres couchés et souches, toiles d'araignée ; blocs et chaos rocheux sur les falaises et dans les
/// rapides, pierres de gué.
/// </summary>
public static partial class ForestBuilder
{
    /// <summary>Instances d'un même maillage par cellule (repères complets : inclinaison permise).</summary>
    private sealed class Instancer
    {
        private readonly float _cell;
        private readonly Dictionary<(Mesh, int, int), (List<Transform3D> xf, List<Color> col)> _b = new();
        private readonly List<(Mesh, int, int)> _order = new();

        public Instancer(float cell) => _cell = cell;

        public int Count { get; private set; }

        public void Add(Mesh m, Transform3D xf, Color col)
        {
            var key = (m, Mathf.FloorToInt(xf.Origin.X / _cell), Mathf.FloorToInt(xf.Origin.Z / _cell));
            if (!_b.TryGetValue(key, out var l))
            {
                l = (new List<Transform3D>(), new List<Color>());
                _b[key] = l;
                _order.Add(key);
            }
            l.xf.Add(xf);
            l.col.Add(col);
            Count++;
        }

        /// <summary>Un MultiMesh par (maillage, cellule), distances de visibilité mesurées au centre de la cellule.</summary>
        public void Flush(Node3D root, string name, float begin, float end, bool shadows, bool keep = false)
        {
            var n = 0;
            foreach (var key in _order)
            {
                var (xfs, cols) = _b[key];
                var origin = new Vector3((key.Item2 + 0.5f) * _cell, 0, (key.Item3 + 0.5f) * _cell);
                var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = key.Item1, InstanceCount = xfs.Count };
                for (var i = 0; i < xfs.Count; i++)
                {
                    var xf = xfs[i];
                    xf.Origin -= origin;
                    mm.SetInstanceTransform(i, xf);
                    mm.SetInstanceColor(i, cols[i]);
                }
                root.AddChild(new MultiMeshInstance3D
                {
                    Name = $"{name}_{n++}",
                    Multimesh = mm,
                    Position = origin,
                    CastShadow = shadows ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                    VisibilityRangeBegin = begin,
                    VisibilityRangeEnd = end,
                });
            }
            if (keep) return;
            _b.Clear();
            _order.Clear();
        }
    }

    /// <summary>Distance de bascule entre arbres détaillés et silhouettes lointaines (centre de cellule).</summary>
    private const float TreeNear = 150f;

    /// <summary>Au-delà (centre de cellule), les arbres détaillés ne projettent plus d'ombre (cartes en découpe : passe d'ombre coûteuse).</summary>
    private const float TreeShadow = 85f;

    private static float H01(float x, float z, int salt) => RuinKit.Hash(x, z, salt);

    // ───────────────────────── Rochers ─────────────────────────

    private static void BuildRocks(Ctx c)
    {
        Mats.ForestRock().SetShaderParameter("wet", c.Wet ? 0.85f : 0.2f);
        var rocks = new Instancer(64f);
        var hf = c.Hf;
        var count = 0;
        void Rock(Vector3 p, float scale, int v, float tilt, float yaw, bool solid = true)
        {
            var basis = new Basis(Vector3.Up, yaw) * new Basis(new Vector3(Mathf.Cos(yaw * 1.7f), 0, Mathf.Sin(yaw * 1.7f)), tilt);
            var xf = new Transform3D(basis.Scaled(Vector3.One * scale), p);
            var mesh = Props.Boulder(v);
            rocks.Add(mesh, xf, new Color(1f, 1f, 1f) * (0.85f + 0.25f * H01(p.X, p.Z, 77)));
            if (solid) c.Body.AddChild(new CollisionShape3D { Shape = MeshKit.ShapeOf(mesh), Transform = xf });
            count++;
        }
        // Falaises et pentes raides : gros blocs à demi enterrés, en grappes.
        for (var z = -250f; z < 250f; z += 5f)
        for (var x = -250f; x < 250f; x += 5f)
        {
            var jx = x + (H01(x, z, 11) - 0.5f) * 4f;
            var jz = z + (H01(x, z, 12) - 0.5f) * 4f;
            var i = Mathf.RoundToInt((jx - GridX0) / GridStep);
            var j = Mathf.RoundToInt((jz - GridZ0) / GridStep);
            var slope = hf.SlopeAt(i, j);
            if (slope < 0.2f || H01(x, z, 13) > Mathf.Lerp(0.15f, 0.65f, Mathf.InverseLerp(0.2f, 0.45f, slope))) continue;
            if (IsTaken(c, new Vector3(jx, 0, jz), 2f) || TrackEdge(c, jx, jz, 8f) < 2f) continue;
            if (z > DamUp - 4f && z < DamToe + 3f && Mathf.Abs(RiverOffset(jx, jz)) < DamHalf + 2f) continue;
            var s = Mathf.Lerp(1.6f, 5.5f, H01(x, z, 14)) * (slope > 0.35f ? 1.3f : 1f);
            var p = new Vector3(jx, MinGround(c, new Vector3(jx, 0, jz), s * 0.8f) - s * 0.35f, jz);
            Rock(p, s, (int)(H01(x, z, 15) * 6f) % 6, (H01(x, z, 16) - 0.5f) * 0.7f, H01(x, z, 17) * Mathf.Tau);
        }
        // Rapides et bassin du barrage : blocs émergés dans le courant.
        foreach (var (z0, z1, n) in new[] { (16f, 58f, 34), (-192f, -170f, 12) })
        for (var k = 0; k < n; k++)
        {
            var z = Mathf.Lerp(z0, z1, H01(k, z0, 21));
            var off = (H01(k, z0, 22) - 0.5f) * 2f * HalfWidth(z) * 0.85f;
            var x = RiverX(z) + off;
            var s = Mathf.Lerp(0.8f, 2.3f, H01(k, z0, 23));
            Rock(new Vector3(x, hf.Terrain(x, z) - 0.25f * s, z), s, k % 5, (H01(k, z0, 24) - 0.5f) * 0.5f, H01(k, z0, 25) * Mathf.Tau);
        }
        // Gués : pierres plates et galets le long du passage.
        foreach (var fz in Fords)
        for (var k = 0; k < 14; k++)
        {
            var off = (k / 13f - 0.5f) * 2f * HalfWidth(fz);
            var z = fz + (H01(k, fz, 31) - 0.5f) * 9f;
            var x = RiverX(z) + off;
            Rock(new Vector3(x, hf.Terrain(x, z) - 0.12f, z), Mathf.Lerp(0.35f, 0.7f, H01(k, fz, 32)), 5, 0.05f, H01(k, fz, 33) * Mathf.Tau, false); // traversables : on passe le gué sans buter
        }
        // Blocs épars dans la forêt (et quelques erratiques dans la plaine).
        for (var k = 0; k < 220; k++)
        {
            var x = Mathf.Lerp(-245f, 245f, H01(k, 1, 41));
            var z = Mathf.Lerp(-245f, 245f, H01(k, 2, 42));
            if (InWater(x, z, 3f) || IsTaken(c, new Vector3(x, 0, z), 3f) || TrackEdge(c, x, z, 6f) < 2.5f) continue;
            if (z > DamUp - 6f && z < DamToe + 4f && Mathf.Abs(RiverOffset(x, z)) < DamHalf + 4f) continue;
            var s = Mathf.Lerp(0.7f, 2.6f, H01(k, 3, 43) * H01(k, 4, 44));
            Rock(new Vector3(x, MinGround(c, new Vector3(x, 0, z), s * 0.7f) - 0.3f * s, z), s, k % 6, (H01(k, 5, 45) - 0.5f) * 0.4f, H01(k, 6, 46) * Mathf.Tau);
        }
        rocks.Flush(c.Root, "Rocks", 0f, 110f, true, true);
        rocks.Flush(c.Root, "RocksFar", 110f, 420f, false);
        if (Screenshot.CmdArg("fps") != null) GD.Print($"[vallée] {count} rochers");
    }

    // ───────────────────────── Forêt ─────────────────────────

    private static void BuildWoods(Ctx c)
    {
        var near = new Instancer(64f);
        var mid = new Instancer(64f);
        var far = new Instancer(64f);
        var under = new Instancer(48f);
        var bigUnder = new Instancer(64f);
        var logs = new Instancer(64f);
        var hf = c.Hf;
        var trunks = new List<(Vector3 p, float r)>();
        var destructible = 0;
        var mixNoise = new FastNoiseLite { Seed = 431, Frequency = 0.011f };
        const float step = 5.4f;
        for (var z = GridZ0 + 2f; z < GridZ0 + (GridN - 1) * GridStep - 2f; z += step)
        for (var x = GridX0 + 2f; x < GridX0 + (GridN - 1) * GridStep - 2f; x += step)
        {
            var px = x + (H01(x, z, 51) - 0.5f) * step * 0.8f;
            var pz = z + (H01(x, z, 52) - 0.5f) * step * 0.8f;
            var dens = Canopy(c, px, pz);
            if (H01(x, z, 53) > dens) continue;
            if (InWater(px, pz, 2.5f) || IsTaken(c, new Vector3(px, 0, pz), 1.5f)) continue;
            if (pz > DamUp - 8f && pz < DamToe + 6f && Mathf.Abs(RiverOffset(px, pz)) < DamHalf + 6f) continue;
            var edge = TrackEdge(c, px, pz, 14f);
            if (edge < 2.2f) continue;
            var i = Mathf.RoundToInt((px - GridX0) / GridStep);
            var j = Mathf.RoundToInt((pz - GridZ0) / GridStep);
            var slope = hf.SlopeAt(i, j);
            if (slope > 0.38f && H01(x, z, 54) < 0.7f) continue;
            var y = hf.Terrain(px, pz);
            // Essences : résineux en altitude et sur les versants, feuillus dans les fonds et par taches.
            var d = Mathf.Abs(RiverOffset(px, pz));
            var share = 0.5f + 0.45f * mixNoise.GetNoise2D(px, pz) + Mathf.Clamp((y - 14f) * 0.012f, -0.2f, 0.3f) - (1f - Mathf.SmoothStep(20f, 50f, d)) * 0.5f;
            var conifer = H01(x, z, 55) < share;
            var dead = H01(x, z, 56) < 0.08f;
            var v = dead ? 3 : (int)(H01(x, z, 57) * 3f);
            var bv = dead ? 3 + (int)(H01(x, z, 58) * 1.99f) : (int)(H01(x, z, 59) * 2.99f);
            var mesh = conifer ? Props.Spruce(v) : Props.Beech(bv);
            var yaw = H01(x, z, 60) * Mathf.Tau;
            var scale = conifer ? Mathf.Lerp(0.75f, 1.3f, H01(x, z, 61)) : Mathf.Lerp(0.8f, 1.25f, H01(x, z, 61));
            var tint = Mathf.Lerp(0.82f, 1.12f, H01(x, z, 62));
            var col = new Color(tint, tint, tint * 0.96f);
            var p = new Vector3(px, MinGround(c, new Vector3(px, 0, pz), 0.5f) - 0.15f, pz);
            var inside = Mathf.Abs(px) < Half + 8f && pz > -Half - 8f && pz < South + 8f;
            // Le long des pistes et autour des lieux : arbres destructibles (ils basculent sous les tirs et les explosions).
            var nearSite = RectOut(Terrace, px, pz) < 10f || RectOut(CampYard, px, pz) < 12f || RectOut(PlantYard, px, pz) < 10f || RectOut(StationYard, px, pz) < 10f;
            if (inside && (edge < 11f || nearSite))
            {
                c.Props.Add(mesh, p, yaw, col, 120f, true, scale);
                destructible++;
                continue;
            }
            var xf = new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * scale), p);
            near.Add(mesh, xf, col);
            mid.Add(mesh, xf, col);
            far.Add(conifer ? Props.ConiferFar(v == 3 ? 3 : 0) : Props.BroadleafFar(dead ? 1 : 0), xf, col);
            if (inside) trunks.Add((p, (conifer ? 0.32f : 0.4f) * scale));
        }
        near.Flush(c.Root, "Trees", 0f, TreeShadow, true);
        mid.Flush(c.Root, "TreesMid", TreeShadow, TreeNear, false);
        far.Flush(c.Root, "TreesFar", TreeNear, 760f, false);
        // Troncs solides (formes partagées par diamètre).
        var shapes = new Dictionary<int, CylinderShape3D>();
        foreach (var (p, r) in trunks)
        {
            var key = Mathf.RoundToInt(r * 20f);
            if (!shapes.TryGetValue(key, out var sh)) shapes[key] = sh = new CylinderShape3D { Radius = key / 20f, Height = 8f };
            c.Body.AddChild(new CollisionShape3D { Shape = sh, Position = p + Vector3.Up * 4f });
        }

        // Sous-bois : fougères sous le couvert, buissons en lisière, semis de résineux, roseaux des berges.
        for (var z = -250f; z < 250f; z += 2.7f)
        for (var x = -250f; x < 250f; x += 2.7f)
        {
            var px = x + (H01(x, z, 71) - 0.5f) * 2.4f;
            var pz = z + (H01(x, z, 72) - 0.5f) * 2.4f;
            if (InWater(px, pz, 0.5f) || IsTaken(c, new Vector3(px, 0, pz), 0.5f) || TrackEdge(c, px, pz, 4f) < 0.8f) continue;
            var dens = Canopy(c, px, pz);
            var roll = H01(x, z, 73);
            var y = hf.Terrain(px, pz) - 0.05f;
            var yaw = H01(x, z, 74) * Mathf.Tau;
            var d = Mathf.Abs(RiverOffset(px, pz));
            Transform3D Xf(float s) => new(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * s), new Vector3(px, y, pz));
            if (d < HalfWidth(pz) + 4f && pz > DamToe)
            {
                if (roll < 0.35f) under.Add(Props.Reeds(), Xf(Mathf.Lerp(0.7f, 1.3f, H01(x, z, 75))), Colors.White);
                continue;
            }
            if (roll < dens * 0.45f)
                under.Add(Props.Fern((int)(H01(x, z, 76) * 4f)), Xf(Mathf.Lerp(0.8f, 1.5f, H01(x, z, 77))), new Color(1, 1, 1) * Mathf.Lerp(0.8f, 1.15f, H01(x, z, 78)));
            else if (roll < dens * 0.45f + 0.035f + (1f - dens) * 0.05f)
                bigUnder.Add(Props.Bush((int)(H01(x, z, 79) * 3f)), Xf(Mathf.Lerp(0.9f, 1.8f, H01(x, z, 80))), Colors.White);
            else if (roll > 0.985f && dens > 0.3f)
                bigUnder.Add(Props.Spruce((int)(H01(x, z, 81) * 3f)), Xf(Mathf.Lerp(0.12f, 0.3f, H01(x, z, 82))), Colors.White);
        }
        under.Flush(c.Root, "Ferns", 0f, 58f, false);
        bigUnder.Flush(c.Root, "Shrubs", 0f, 130f, false);

        // Arbres couchés (chablis) sur les versants, souches et rémanents autour du camp et le long des pistes.
        var logCount = 0;
        for (var k = 0; k < 400 && logCount < 120; k++)
        {
            var x = Mathf.Lerp(-246f, 246f, H01(k, 7, 91));
            var z = Mathf.Lerp(-246f, 246f, H01(k, 8, 92));
            if (Canopy(c, x, z) < 0.45f || InWater(x, z, 4f) || IsTaken(c, new Vector3(x, 0, z), 6f) || TrackEdge(c, x, z, 12f) < 6f) continue;
            var yaw = H01(k, 9, 93) * Mathf.Tau;
            var dir = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
            var a = new Vector3(x, 0, z) - dir * 4f;
            var b = new Vector3(x, 0, z) + dir * 8f;
            if (TrackEdge(c, a.X, a.Z, 8f) < 4f || TrackEdge(c, b.X, b.Z, 8f) < 4f) continue;
            var ha = hf.Terrain(a.X, a.Z);
            var hb = hf.Terrain(b.X, b.Z);
            if (Mathf.Abs(hb - ha) > 6f) continue;
            var tilt = Mathf.Atan2(hb - ha, 12f);
            var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Back, tilt);
            var mesh = Props.FallenLog(k % 6);
            var xf = new Transform3D(basis, new Vector3(x, Mathf.Min(hf.Terrain(x, z), (ha + hb) / 2f) - 0.12f, z));
            logs.Add(mesh, xf, new Color(1, 1, 1) * Mathf.Lerp(0.8f, 1.1f, H01(k, 10, 94)));
            c.Body.AddChild(new CollisionShape3D { Shape = MeshKit.ShapeOf(mesh), Transform = xf });
            logCount++;
        }
        var stumps = 0;
        for (var k = 0; k < 900 && stumps < 260; k++)
        {
            // Coupe rase autour du camp (densité forte), puis bords des pistes.
            var aroundCamp = k < 500;
            float x, z;
            if (aroundCamp)
            {
                var ang = H01(k, 11, 95) * Mathf.Tau;
                var r = Mathf.Lerp(14f, 52f, Mathf.Sqrt(H01(k, 12, 96)));
                x = CampYard.GetCenter().X + Mathf.Cos(ang) * r * 1.2f;
                z = CampYard.GetCenter().Y + Mathf.Sin(ang) * r;
            }
            else
            {
                x = Mathf.Lerp(-240f, 240f, H01(k, 13, 97));
                z = Mathf.Lerp(-240f, 240f, H01(k, 14, 98));
                var e = TrackEdge(c, x, z, 10f);
                if (e < 1.5f || e > 7f) continue;
            }
            if (InWater(x, z, 2f) || IsTaken(c, new Vector3(x, 0, z), 1f) || TrackEdge(c, x, z, 3f) < 1f) continue;
            var y = hf.Terrain(x, z) - 0.08f;
            var yaw = H01(k, 15, 99) * Mathf.Tau;
            if (aroundCamp && H01(k, 16, 100) < 0.12f)
                logs.Add(Props.SlashPile(k % 2), new Transform3D(new Basis(Vector3.Up, yaw), new Vector3(x, y, z)), Colors.White);
            else
                logs.Add(Props.Stump(k % 3), new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * Mathf.Lerp(0.8f, 1.3f, H01(k, 17, 101))), new Vector3(x, y, z)), Colors.White);
            stumps++;
        }
        logs.Flush(c.Root, "Logs", 0f, 90f, true, true);
        logs.Flush(c.Root, "LogsFar", 90f, 200f, false);

        // Toiles d'araignée tendues entre les fûts, dans la forêt de la rive ouest et au nord.
        var webs = 0;
        for (var k = 0; k + 2 < trunks.Count && webs < 14; k += 37)
        {
            var (a, ra) = trunks[k];
            if (a.X > -120f && a.Z > -100f) continue;
            var b = trunks[k + 1].p;
            var cc = trunks[k + 2].p;
            if (a.DistanceTo(b) > 11f || a.DistanceTo(cc) > 11f || b.DistanceTo(cc) > 11f) continue;
            var h = 3f + H01(k, 18, 102) * 5f;
            var gk = c.Ground.At(a);
            Props.Web(gk, new[] { a + Vector3.Up * h + (b - a).Normalized() * ra, b + Vector3.Up * (h + 1.5f), cc + Vector3.Up * (h + 0.8f) }, k);
            webs++;
        }
        if (Screenshot.CmdArg("fps") != null)
            GD.Print($"[vallée] arbres : {near.Count} + {destructible} destructibles, {trunks.Count} troncs solides, sous-bois {under.Count + bigUnder.Count}, chablis {logCount}, souches {stumps}, toiles {webs}");
    }
}
