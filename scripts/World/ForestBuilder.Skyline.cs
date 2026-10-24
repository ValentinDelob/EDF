using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Au-delà de l'aire de jeu : le relief brut prolongé jusqu'à 1,6 km (grille de 16 m, sans collision ni ombres, canopée
/// sombre bosselée peinte dans le shader du sol), forêt lointaine en silhouettes (cônes et boules, sans ombres) jusqu'à
/// ~750 m ; brume des fonds (volumes de brouillard le long de la rivière et dans la gorge du barrage).
/// </summary>
public static partial class ForestBuilder
{
    private static void BuildSkyline(Ctx c)
    {
        var mat = Mats.ForestGround(c.Wet);
        const float step = 16f;
        const float ext = 1600f;
        var n = Mathf.RoundToInt(ext * 2 / step) + 1;
        var pos = new Vector3[n * n];
        var col = new Color[n * n];
        for (var j = 0; j < n; j++)
        for (var i = 0; i < n; i++)
        {
            var x = -ext + i * step;
            var z = -ext + j * step;
            var inner = Mathf.Abs(x) < 250f && Mathf.Abs(z) < 250f;
            var h = Raw(x, z) - (inner ? 0.6f : 0.15f);
            pos[j * n + i] = new Vector3(x, h, z);
            var lake = z < DamUp && h < LakeY + 0.3f;
            var river = z > DamToe && Mathf.Abs(RiverOffset(x, z)) < HalfWidth(z) + 6f;
            var dist = new Vector2(x, z).Length();
            // Canopée lointaine (B > 0,65) sauf eau, plaine et clairières.
            var gap = Patches.GetNoise2D(x * 0.6f, z * 0.6f);
            var plain = 1f - Mathf.SmoothStep(20f, 60f, Mathf.Abs(RiverOffset(x, z)));
            var canopy = lake || river ? 0f : Mathf.Clamp(0.95f - plain * 0.7f - Mathf.Max(0f, -gap - 0.3f) * 2f, 0f, 1f);
            col[j * n + i] = new Color(0.6f * (1f - canopy), 0.4f, canopy > 0.4f ? Mathf.Lerp(0.66f, 0.95f, canopy) : 0.2f, 0f);
            _ = dist;
        }
        var nrm = new Vector3[n * n];
        for (var j = 0; j < n; j++)
        for (var i = 0; i < n; i++)
        {
            var l = pos[j * n + Mathf.Max(i - 1, 0)];
            var r = pos[j * n + Mathf.Min(i + 1, n - 1)];
            var d = pos[Mathf.Max(j - 1, 0) * n + i];
            var u = pos[Mathf.Min(j + 1, n - 1) * n + i];
            var nn = (u - d).Cross(r - l).Normalized();
            nrm[j * n + i] = nn.Y < 0 ? -nn : nn;
        }
        // Secteurs de 25 × 25 cases (400 m) ; les cases entièrement sous la grille de jeu sont omises.
        const int sector = 25;
        for (var cj = 0; cj < n - 1; cj += sector)
        for (var ci = 0; ci < n - 1; ci += sector)
        {
            var verts = new List<Vector3>();
            var nv = new List<Vector3>();
            var cv = new List<Color>();
            var tan = new List<float>();
            var idx = new List<int>();
            var map = new Dictionary<int, int>();
            int V(int k)
            {
                if (map.TryGetValue(k, out var v)) return v;
                v = verts.Count;
                map[k] = v;
                verts.Add(pos[k]);
                nv.Add(nrm[k]);
                cv.Add(col[k]);
                var t = (Vector3.Right - nrm[k] * nrm[k].X).Normalized();
                tan.Add(t.X); tan.Add(t.Y); tan.Add(t.Z); tan.Add(-1f);
                return v;
            }
            for (var j = cj; j < Mathf.Min(cj + sector, n - 1); j++)
            for (var i = ci; i < Mathf.Min(ci + sector, n - 1); i++)
            {
                var a = j * n + i;
                var x0 = pos[a].X;
                var z0 = pos[a].Z;
                if (x0 >= GridX0 + 8f && x0 + step <= GridX0 + (GridN - 1) * GridStep - 8f && z0 >= GridZ0 + 8f && z0 + step <= GridZ0 + (GridN - 1) * GridStep - 8f) continue;
                if (new Vector2(x0, z0).Length() > ext + 10f) continue;
                int b = a + 1, e = a + n + 1, f = a + n;
                idx.Add(V(a)); idx.Add(V(b)); idx.Add(V(e));
                idx.Add(V(a)); idx.Add(V(e)); idx.Add(V(f));
            }
            if (idx.Count == 0) continue;
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = nv.ToArray();
            arrays[(int)Mesh.ArrayType.Tangent] = tan.ToArray();
            arrays[(int)Mesh.ArrayType.Color] = cv.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(0, mat);
            c.Root.AddChild(new MeshInstance3D { Name = $"FarTerrain_{ci}_{cj}", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }

        // Forêt lointaine : silhouettes sur le relief brut, au-delà de la grille de jeu, jusqu'à ~720 m.
        var far = new Instancer(160f);
        for (var z = -720f; z < 720f; z += 8.5f)
        for (var x = -720f; x < 720f; x += 8.5f)
        {
            if (Mathf.Abs(x) < 254f && Mathf.Abs(z) < 254f) continue;
            var px = x + (H01(x, z, 111) - 0.5f) * 7f;
            var pz = z + (H01(x, z, 112) - 0.5f) * 7f;
            var r = new Vector2(px, pz).Length();
            if (r > 720f) continue;
            var h = Raw(px, pz);
            if (pz < DamUp && h < LakeY + 0.5f) continue;
            var d = Mathf.Abs(RiverOffset(px, pz));
            if (pz > DamToe && d < HalfWidth(pz) + 10f) continue;
            var dens = Mathf.SmoothStep(25f, 60f, d) * Mathf.SmoothStep(-0.55f, -0.25f, Patches.GetNoise2D(px * 0.6f, pz * 0.6f)) * (r > 480f ? 0.6f : 0.85f);
            if (H01(x, z, 113) > dens) continue;
            var conifer = H01(x, z, 114) < 0.65f + Mathf.Clamp((h - 20f) * 0.01f, -0.2f, 0.3f);
            var s = Mathf.Lerp(0.85f, 1.35f, H01(x, z, 115));
            var tint = Mathf.Lerp(0.75f, 1.05f, H01(x, z, 116));
            far.Add(conifer ? Props.ConiferFar(0) : Props.BroadleafFar(2 * (int)(H01(x, z, 117) * 2.99f)),
                new Transform3D(new Basis(Vector3.Up, H01(x, z, 118) * Mathf.Tau).Scaled(Vector3.One * s), new Vector3(px, h - 0.3f, pz)), new Color(tint, tint, tint));
        }
        far.Flush(c.Root, "FarForest", 0f, 900f, false);
    }

    // ───────────────────────── Brume ─────────────────────────

    /// <summary>
    /// Brume des fonds : volumes de brouillard (rendus avec le brouillard volumétrique, qualité « haute » et plus) posés
    /// sur la rivière, dans la gorge du barrage (embruns du déversoir) et dans les creux de la plaine. En qualité basse, le
    /// brouillard de hauteur de l'environnement (<see cref="ApplyGrade"/>) fait le même travail.
    /// </summary>
    private static void BuildMist(Ctx c)
    {
        var night = c.Night;
        FogMaterial Mat(float density) => new()
        {
            Density = density,
            Albedo = night ? new Color(0.35f, 0.38f, 0.42f) : new Color(0.78f, 0.8f, 0.8f),
            HeightFalloff = 0.25f,
            EdgeFade = 0.6f,
        };
        var river = Mat(0.05f);
        for (var z = -170f; z < 175f; z += 32f)
        {
            var x = RiverX(z);
            c.Root.AddChild(new FogVolume
            {
                Name = $"Mist_{z:0}",
                Shape = RenderingServer.FogVolumeShape.Ellipsoid,
                Size = new Vector3(70f, 9f, 44f),
                Position = new Vector3(x + 10f * Mathf.Sin(z * 0.07f), WaterY(z) + 2.2f, z),
                Material = river,
            });
        }
        // Embruns du déversoir et brume stagnante de la gorge.
        c.Root.AddChild(new FogVolume
        {
            Name = "Spray", Shape = RenderingServer.FogVolumeShape.Ellipsoid, Size = new Vector3(40f, 18f, 30f),
            Position = new Vector3(RiverX(DamZ), WaterY(DamToe) + 5f, DamToe + 6f), Material = Mat(0.11f),
        });
        foreach (var (x, z, s) in new[] { (-30f, -150f, 60f), (-10f, 95f, 70f), (-150f, 60f, 50f), (60f, -120f, 45f) })
            c.Root.AddChild(new FogVolume
            {
                Shape = RenderingServer.FogVolumeShape.Ellipsoid, Size = new Vector3(s, 10f, s * 0.8f),
                Position = new Vector3(x, c.Hf.Terrain(x, z) + 2.5f, z), Material = Mat(0.035f),
            });
    }
}
