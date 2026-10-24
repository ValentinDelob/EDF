using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Désert au-delà des murs invisibles (décor sans collision ni ombres) : champ de dunes jusqu'à ~1,5 km (couloirs
/// aplanis pour la piste et la route), mesas de grès à l'horizon, station radar lointaine (radômes sur une crête),
/// pylônes de transmission, carcasses d'avions sur le prolongement de la piste.
/// </summary>
public static partial class DesertBuilder
{
    private static readonly FastNoiseLite DuneNoise = new() { Seed = 29, Frequency = 0.006f, FractalOctaves = 3 };
    private static readonly FastNoiseLite DuneDetail = new() { Seed = 31, Frequency = 0.03f, FractalOctaves = 2 };

    /// <summary>Distance hors de l'aire de jeu (0 à l'intérieur, rempart compris).</summary>
    private static float Outside(float x, float z)
    {
        var dx = Mathf.Max(0, Mathf.Abs(x) - 238f);
        var dz = Mathf.Max(Mathf.Max(0, -z - 238f), Mathf.Max(0, z - 172f));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Relief des dunes : nul dans l'aire de jeu, cordons orientés par le vent au-delà, couloirs aplanis.</summary>
    private static float DuneField(float x, float z)
    {
        var d = Outside(x, z);
        if (d <= 0) return 0f;
        // Cordons de dunes (crêtes perpendiculaires au vent, déformées), plus hauts au loin.
        var w = DuneNoise.GetNoise2D(x, z);
        var ridge = 1f - Mathf.Abs(Mathf.Sin((x * 0.9f + z * 0.35f) * 0.016f + w * 3.2f));
        var h = (ridge * ridge * 0.7f + 0.3f * (w * 0.5f + 0.5f)) * (10f + 26f * Mathf.SmoothStep(0, 900, d));
        h += DuneDetail.GetNoise2D(x, z) * 1.6f;
        h *= Mathf.SmoothStep(0, 140, d);
        // Couloir de la piste (est-ouest) et de la route du désert (sud).
        h *= Mathf.SmoothStep(30f, 110f, Mathf.Abs(z - (RunwayZ0 + RunwayZ1) / 2));
        if (z > 0) h *= Mathf.SmoothStep(14f, 70f, Mathf.Abs(x));
        return Mathf.Max(h, 0f);
    }

    private static Color DuneColor(float x, float z, float h)
    {
        var n = DuneDetail.GetNoise2D(x * 0.3f, z * 0.3f);
        var c = new Color(0.94f, 0.91f, 0.86f).Lerp(new Color(1.05f, 1.02f, 0.97f), Mathf.Clamp(h / 30f, 0, 1));
        return c * (0.95f + n * 0.08f);
    }

    private static void BuildSkyline(Ctx c)
    {
        var sand = Mats.DesertSand();
        // Grille non uniforme : 8 m près de l'aire de jeu, 24 m puis 60 m au-delà.
        var axis = new List<float>();
        for (var v = -1500f; v < -600f; v += 60f) axis.Add(v);
        for (var v = -600f; v < -300f; v += 24f) axis.Add(v);
        for (var v = -300f; v < 300f; v += 8f) axis.Add(v);
        for (var v = 300f; v < 600f; v += 24f) axis.Add(v);
        for (var v = 600f; v <= 1500f; v += 60f) axis.Add(v);
        var n = axis.Count;
        var pos = new Vector3[n * n];
        var col = new Color[n * n];
        for (var j = 0; j < n; j++)
        for (var i = 0; i < n; i++)
        {
            var x = axis[i];
            var z = axis[j];
            var h = DuneField(x, z);
            // Sous la dalle de l'aire de jeu, le relief plat passe 30 cm plus bas (pas de scintillement) ; sauf sous la
            // piste et la route prolongées, posées au niveau 0.
            var corridor = Mathf.Abs(z - (RunwayZ0 + RunwayZ1) / 2) < 26f || z > 0 && Mathf.Abs(x) < 9f;
            var y = corridor ? h - 0.005f : h - 0.3f * (1f - Mathf.Clamp(h / 0.6f, 0f, 1f));
            pos[j * n + i] = new Vector3(x, y, z);
            col[j * n + i] = DuneColor(x, z, h);
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
        for (var cj = 0; cj < n - 1; cj += 24)
        for (var ci = 0; ci < n - 1; ci += 24)
        {
            var k = new MeshKit();
            for (var j = cj; j < Mathf.Min(cj + 24, n - 1); j++)
            for (var i = ci; i < Mathf.Min(ci + 24, n - 1); i++)
            {
                var a = j * n + i;
                var b = a + 1;
                var e = a + n + 1;
                var f = a + n;
                // Cases entièrement dans la dalle de l'aire de jeu : la dalle de sable suffit.
                if (Inner(pos[a]) && Inner(pos[e]) && Inner(pos[b]) && Inner(pos[f])) continue;
                k.TriSmooth(sand, pos[a], pos[b], pos[e], nrm[a], nrm[b], nrm[e], col[a], col[b], col[e]);
                k.TriSmooth(sand, pos[a], pos[e], pos[f], nrm[a], nrm[e], nrm[f], col[a], col[e], col[f]);
            }
            if (k.Empty) continue;
            c.Root.AddChild(new MeshInstance3D { Name = $"DesDunes_{ci}_{cj}", Mesh = k.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }

        // Décor lointain fusionné par secteurs de 400 m.
        var sectors = new Dictionary<(int, int), MeshKit>();
        MeshKit At(Vector3 p)
        {
            var key = (Mathf.FloorToInt(p.X / 400f), Mathf.FloorToInt(p.Z / 400f));
            if (!sectors.TryGetValue(key, out var kk)) sectors[key] = kk = new MeshKit();
            return kk;
        }
        var rock = Mats.DesertRock();
        foreach (var (x, z, rx, rz, h, seed) in new[]
        {
            (-1150f, -900f, 220f, 120f, 95f, 1), (-300f, -1250f, 160f, 90f, 70f, 2), (700f, -1100f, 260f, 110f, 120f, 3),
            (1250f, -200f, 140f, 200f, 85f, 4), (1100f, 700f, 200f, 120f, 75f, 5), (-1250f, 450f, 180f, 160f, 105f, 6), (-500f, 1200f, 240f, 100f, 60f, 7),
        })
            Mesa(At(new Vector3(x, 0, z)), rock, new Vector3(x, DuneField(x, z) - 4f, z), rx, rz, h, seed);
        // Station radar sur une crête au nord-ouest, relais à l'est.
        var dome = Mats.Worn(new Color(0.66f, 0.64f, 0.6f), 0.7f, 0f);
        var metal = Mats.DetailMetal();
        var detail = Mats.Detail();
        foreach (var (x, z, r) in new[] { (-760f, -720f, 14f), (-720f, -760f, 10f), (-800f, -700f, 9f), (900f, 260f, 12f) })
        {
            var p = new Vector3(x, DuneField(x, z), z);
            var k = At(p);
            k.Col = new Color(0.55f, 0.53f, 0.48f);
            k.Box(detail, p + Vector3.Up * 6f, new Vector3(r * 1.8f, 12f, r * 1.8f));
            k.Col = Colors.White;
            SpherePart(k, dome, p + Vector3.Up * (12f + r * 0.6f), Vector3.One * r, 18, 7, -0.6f, Mathf.Pi / 2);
        }
        foreach (var (x, z) in new[] { (-690f, -820f), (940f, 330f), (-430f, -980f) })
        {
            var p = new Vector3(x, DuneField(x, z), z);
            var k = At(p);
            k.Col = new Color(0.4f, 0.38f, 0.34f);
            for (var i = 0; i < 3; i++)
            {
                var a = i * Mathf.Tau / 3f;
                k.Tube(metal, p + new Vector3(Mathf.Cos(a) * 4f, 0, Mathf.Sin(a) * 4f), p + new Vector3(Mathf.Cos(a) * 0.8f, 70f, Mathf.Sin(a) * 0.8f), 0.4f, 4);
            }
        }
        // Carcasses sur le prolongement de la piste (est et ouest).
        foreach (var (x, yaw) in new[] { (-420f, 0.3f), (520f, -2.6f) })
        {
            var p = new Vector3(x, 0, (RunwayZ0 + RunwayZ1) / 2 + 6f);
            var k = At(p);
            k.Xf = new Transform3D(new Basis(Vector3.Up, yaw), p);
            Props.TransportWreck(k);
            k.Xf = Transform3D.Identity;
        }
        foreach (var (key, k) in sectors)
            c.Root.AddChild(new MeshInstance3D { Name = $"DesSkyline_{key.Item1}_{key.Item2}", Mesh = k.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        c.Ruins.Smolder(new Vector3(520, 3f, (RunwayZ0 + RunwayZ1) / 2 + 6f), 3f);
    }

    /// <summary>Le point est sous la dalle de sable de l'aire de jeu (±262 m) et sans relief.</summary>
    private static bool Inner(Vector3 p) => Mathf.Abs(p.X) < 260f && Mathf.Abs(p.Z) < 260f && p.Y <= 0f;

    /// <summary>
    /// Mesa de grès : falaise à contour irrégulier (corniche dure en haut, éboulis évasé au pied), plateau sommital
    /// légèrement bombé. Repère monde.
    /// </summary>
    private static void Mesa(MeshKit k, Material m, Vector3 c, float rx, float rz, float h, int seed)
    {
        const int seg = 40;
        var rim = new Vector3[seg];
        var foot = new Vector3[seg];
        var mid = new Vector3[seg];
        for (var i = 0; i < seg; i++)
        {
            var a = i * Mathf.Tau / seg;
            var w = 0.82f + 0.25f * RuinKit.Hash(seed, i, 121) + 0.12f * Mathf.Sin(a * 3f + seed);
            var dir = new Vector3(Mathf.Cos(a) * rx, 0, Mathf.Sin(a) * rz) * w;
            rim[i] = c + dir + Vector3.Up * h * (0.97f + 0.05f * RuinKit.Hash(seed, i, 122));
            mid[i] = c + dir * 1.04f + Vector3.Up * h * 0.55f;
            foot[i] = c + dir * 1.35f;
        }
        for (var i = 0; i < seg; i++)
        {
            var j = (i + 1) % seg;
            k.Col = new Color(0.95f, 0.93f, 0.9f);
            k.Tri(m, rim[i], rim[j], mid[j], c + Vector3.Up * h * 0.6f);
            k.Tri(m, rim[i], mid[j], mid[i], c + Vector3.Up * h * 0.6f);
            k.Col = new Color(0.88f, 0.85f, 0.8f);
            k.Tri(m, mid[i], mid[j], foot[j], c + Vector3.Up * h * 0.2f);
            k.Tri(m, mid[i], foot[j], foot[i], c + Vector3.Up * h * 0.2f);
            k.Col = new Color(1f, 0.98f, 0.94f);
            k.Tri(m, rim[i], rim[j], c + Vector3.Up * h * 1.02f, c);
        }
    }
}
