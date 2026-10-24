using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Relief de la vallée : pistes (profil en long lissé, nivelées en travers), plates-formes, grille de hauteurs de 2 m
/// (collision HeightMapShape3D), maillage du sol par secteurs de 64 m (couleurs de sommet : herbe, mousse, ombre du
/// couvert, piste), et blocage du champ de flux (rivière hors gués et pont, lac, pentes trop raides avec leur hauteur).
/// </summary>
public static partial class ForestBuilder
{
    // ───────────────────────── Pistes ─────────────────────────

    /// <summary>Extrémités du pont (est, ouest).</summary>
    private static Vector2 BridgeE => BridgeC - BridgeDir * BridgeHalf;
    private static Vector2 BridgeW => BridgeC + BridgeDir * BridgeHalf;

    /// <summary>Route de la vallée, de la maison forestière à la centrale (pont, rive ouest, gué nord) : points de contrôle.</summary>
    private static List<Vector2> MainNorthCtl() => new()
    {
        new(6, 46), new(5, 10), new(3, -24), new(-8, -46), new(-28, -64),
        BridgeE - BridgeDir * 16f, BridgeE, BridgeW, BridgeW + BridgeDir * 10f,
        new(-130, -72), new(-135, -92), new(-133, -112), new(-126, -127), new(-113, -133.5f),
        new(-97, -134), new(-81, -134.5f), new(-67, -142), new(-58, -156), new(-53, -171),
    };

    private static readonly Vector2[] MainSouthCtl = { new(6, 46), new(8, 80), new(14, 112), new(24, 146), new(35, 182), new(46, 230), new(52, 270) };
    private static readonly Vector2[] LoggingCtl = { new(4, -18), new(28, -36), new(56, -56), new(82, -80), new(101, -103), new(116, -120) };
    private static readonly Vector2[] FordSouthCtl =
    {
        new(7, 72), new(-16, 86), new(-40, 95), new(-54, 97), new(-68, 97.5f), new(-84, 98.5f), new(-106, 104), new(-134, 112), new(-160, 120), new(-176, 127),
    };

    /// <summary>Catmull-Rom centripète simplifiée, un point tous les ~4 m (courbes douces, alignements conservés).</summary>
    private static List<Vector2> Smooth(IReadOnlyList<Vector2> ctl, float step = 4f)
    {
        var o = new List<Vector2> { ctl[0] };
        for (var i = 0; i + 1 < ctl.Count; i++)
        {
            var p0 = ctl[Mathf.Max(i - 1, 0)];
            var p1 = ctl[i];
            var p2 = ctl[i + 1];
            var p3 = ctl[Mathf.Min(i + 2, ctl.Count - 1)];
            var n = Mathf.Max(1, Mathf.CeilToInt(p1.DistanceTo(p2) / step));
            for (var k = 1; k <= n; k++)
            {
                var t = k / (float)n;
                var t2 = t * t;
                var t3 = t2 * t;
                o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        return o;
    }

    /// <summary>Trajet du convoi : la route de la vallée lissée (mêmes points que la piste).</summary>
    private static float[] BuildRoute()
    {
        var pts = Smooth(MainNorthCtl());
        var r = new List<float>();
        // Un point tous les ~8 m suffit au convoi (cordes de ±5 m pour le cap).
        for (var i = 0; i < pts.Count; i += 2) { r.Add(pts[i].X); r.Add(pts[i].Y); }
        if ((pts.Count - 1) % 2 != 0) { r.Add(pts[^1].X); r.Add(pts[^1].Y); }
        return r.ToArray();
    }

    /// <summary>Trajet du convoi de la mission d'escorte (x0, z0, x1, z1…).</summary>
    public static float[] ConvoyRoute => _route ??= BuildRoute();
    private static float[] _route;

    private static Track AddTrack(Ctx c, IReadOnlyList<Vector2> ctl, float hw, bool main)
    {
        var t = new Track { Hw = hw, Main = main };
        foreach (var p in Smooth(ctl)) t.Add(p.X, p.Y);
        c.Tracks.Add(t);
        return t;
    }

    private static void PlanTracks(Ctx c)
    {
        // Plates-formes d'abord (le profil des pistes les traverse à leur hauteur).
        var ty = 0f;
        var n = 0;
        for (var x = Terrace.Position.X; x <= Terrace.End.X; x += 8f)
        for (var z = Terrace.Position.Y; z <= Terrace.End.Y; z += 8f) { ty += Raw(x, z); n++; }
        c.TerraceY = Mathf.Snapped(ty / n + 0.3f, 0.1f);
        c.Pads.Add(new Pad(Terrace, c.TerraceY, 9f, false));
        c.Pads.Add(new Pad(StationYard, c.TerraceY + 0.4f, 9f, true));
        c.Pads.Add(new Pad(CampYard, PadHeight(CampYard), 10f, true));
        c.Pads.Add(new Pad(PlantYard, PadHeight(PlantYard), 7f, true));
        c.Pads.Add(new Pad(LookoutYard, PadHeight(LookoutYard), 8f, false));
        // Culées du pont : sol remblayé au niveau du tablier.
        var deckY = DeckY();
        foreach (var e in new[] { BridgeE - BridgeDir * 4f, BridgeW + BridgeDir * 4f })
            c.Pads.Add(new Pad(new Rect2(e - new Vector2(6, 6), new Vector2(12, 12)), deckY - 0.05f, 9f, true));

        c.MainRoad = AddTrack(c, MainNorthCtl(), 3.8f, true);
        AddTrack(c, MainSouthCtl, 3.8f, true);
        AddTrack(c, LoggingCtl, 2.8f, false);
        AddTrack(c, FordSouthCtl, 2.5f, false);

        // Profils en long : sol aplani (plates-formes) lissé sur ±16 m ; tablier du pont imposé.
        foreach (var t in c.Tracks)
        {
            var raw = new float[t.P.Count];
            for (var i = 0; i < raw.Length; i++) raw[i] = PaddedRaw(c, t.P[i].X, t.P[i].Y);
            t.Y = new float[raw.Length];
            for (var i = 0; i < raw.Length; i++)
            {
                var sum = 0f;
                var wsum = 0f;
                for (var k = -4; k <= 4; k++)
                {
                    var j = Mathf.Clamp(i + k, 0, raw.Length - 1);
                    var wk = 1f - Mathf.Abs(k) / 5f;
                    sum += raw[j] * wk;
                    wsum += wk;
                }
                t.Y[i] = sum / wsum;
                // Dans une plate-forme : exactement à sa hauteur.
                foreach (var p in c.Pads)
                    if (p.R.HasPoint(t.P[i])) t.Y[i] = p.Y;
            }
        }
        // Pont : tablier droit entre les culées, rampes de raccord sur 18 m de part et d'autre.
        var main = c.MainRoad;
        main.Nearest(BridgeE, 50f, out _, out var sE, out _);
        main.Nearest(BridgeW, 50f, out _, out var sW, out _);
        main.SkipA = Mathf.Min(sE, sW);
        main.SkipB = Mathf.Max(sE, sW);
        for (var i = 0; i < main.P.Count; i++)
        {
            var s = main.S[i];
            var outside = s < main.SkipA ? main.SkipA - s : s > main.SkipB ? s - main.SkipB : 0f;
            main.Y[i] = Mathf.Lerp(deckY, main.Y[i], Mathf.SmoothStep(0f, 18f, outside));
        }
    }

    private static float DeckY() => WaterY(BridgeZ) + 2.7f;

    /// <summary>Hauteur moyenne du relief brut sur une plate-forme (arrondie au décimètre).</summary>
    private static float PadHeight(Rect2 r)
    {
        var sum = 0f;
        var n = 0;
        for (var x = r.Position.X; x <= r.End.X; x += 4f)
        for (var z = r.Position.Y; z <= r.End.Y; z += 4f) { sum += Raw(x, z); n++; }
        return Mathf.Snapped(sum / n, 0.1f);
    }

    /// <summary>Distance hors du rectangle (0 dedans).</summary>
    private static float RectOut(Rect2 r, float x, float z)
    {
        var dx = Mathf.Max(Mathf.Max(r.Position.X - x, x - r.End.X), 0f);
        var dz = Mathf.Max(Mathf.Max(r.Position.Y - z, z - r.End.Y), 0f);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Relief brut puis plates-formes (talus de raccord en pente adoucie).</summary>
    private static float PaddedRaw(Ctx c, float x, float z)
    {
        var h = Raw(x, z);
        foreach (var p in c.Pads)
        {
            var e = RectOut(p.R, x, z);
            if (e >= p.Margin) continue;
            h = Mathf.Lerp(h, p.Y, 1f - Mathf.SmoothStep(0f, p.Margin, e));
        }
        return h;
    }

    // ───────────────────────── Grille, collision, maillage ─────────────────────────

    private static void BuildTerrain(Ctx c)
    {
        var hf = new Heightfield(GridX0, GridZ0, GridStep, GridN, GridN);
        c.Hf = hf;
        var path = new float[GridN * GridN];
        for (var j = 0; j < GridN; j++)
        for (var i = 0; i < GridN; i++)
        {
            var x = GridX0 + i * GridStep;
            var z = GridZ0 + j * GridStep;
            var h = PaddedRaw(c, x, z);
            var pa = 0f;
            foreach (var p in c.Pads)
                if (p.Gravel) pa = Mathf.Max(pa, 1f - Mathf.SmoothStep(0f, 2.5f, RectOut(p.R, x, z) + Patches.GetNoise2D(x * 3f, z * 3f) * 1.2f));
            // Pistes : nivelées en travers (sauf dans le lit de la rivière : gués), gravier ou terre battue.
            var inRiver = z > DamToe && Mathf.Abs(RiverOffset(x, z)) < HalfWidth(z) + 4.5f;
            var q = new Vector2(x, z);
            foreach (var t in c.Tracks)
            {
                if (!t.Nearest(q, t.Hw + 6f, out var d, out var s, out _)) continue;
                if (s > t.SkipA && s < t.SkipB) continue;
                var ty = t.HeightAt(s);
                if (!inRiver) h = Mathf.Lerp(h, ty, 1f - Mathf.SmoothStep(t.Hw + 0.6f, t.Hw + 6f, d));
                pa = Mathf.Max(pa, (1f - Mathf.SmoothStep(t.Hw - 0.8f, t.Hw + 0.9f, d + Patches.GetNoise2D(x * 4f, z * 4f) * 0.6f)) * (t.Main ? 1f : 0.85f));
            }
            hf.H[j * GridN + i] = h;
            path[j * GridN + i] = pa;
        }
        // Tablier du pont : le sol praticable passe au-dessus de la rivière.
        hf.AddDeck(new Heightfield.Deck(BridgeC, BridgeDir, BridgeHalf + 1f, BridgeHw, DeckY(), DeckY()));
        c.Body.AddChild(hf.CollisionShape());
        BuildGroundMesh(c, path);
        BlockFlow(c);
    }

    /// <summary>Densité du couvert forestier (0 : découvert, 1 : futaie dense), hors eau, pistes et plates-formes.</summary>
    private static float Canopy(Ctx c, float x, float z)
    {
        var d = Mathf.Abs(RiverOffset(x, z));
        if (z > DamUp && d < HalfWidth(z) + 3f) return 0f;
        if (z < DamUp && Raw(x, z) < LakeY + 0.5f) return 0f;
        // Plaine : bosquets clairsemés (aulnes, saules) ; versants : futaie, clairières.
        var plain = 1f - Mathf.SmoothStep(18f, 44f, d);
        var v = Mathf.Lerp(0.95f, 0.25f, plain);
        var gap = Patches.GetNoise2D(x, z);
        v *= Mathf.SmoothStep(-0.55f, -0.25f, gap);
        // Coupe rase autour du camp de bûcherons, jardin de la maison forestière.
        v *= Mathf.SmoothStep(18f, 46f, RectOut(CampYard, x, z));
        v *= Mathf.SmoothStep(4f, 22f, RectOut(Terrace, x, z));
        v *= Mathf.SmoothStep(3f, 14f, RectOut(PlantYard, x, z));
        return Mathf.Clamp(v, 0f, 1f);
    }

    private static Color GroundColor(Ctx c, float x, float z, float canopy, float pathA)
    {
        var d = Mathf.Abs(RiverOffset(x, z));
        var n = Patches.GetNoise2D(x * 1.7f, z * 1.7f);
        // Herbe sèche : plaine, clairières, bords de piste ; mousse sous le couvert et près de l'eau.
        var grass = Mathf.Clamp((1f - canopy) * 0.9f + n * 0.25f, 0f, 1f) * Mathf.SmoothStep(HalfWidth(z) - 1f, HalfWidth(z) + 3f, d);
        var moss = Mathf.Clamp(canopy * 0.8f + (1f - Mathf.SmoothStep(8f, 24f, d)) * 0.4f, 0f, 1f);
        var shade = canopy * 0.45f;
        return new Color(grass, moss, shade, pathA);
    }

    private static void BuildGroundMesh(Ctx c, float[] path)
    {
        var hf = c.Hf;
        var mat = Mats.ForestGround(c.Wet);
        const int chunk = 32;
        var cols = new Color[GridN * GridN];
        var nrms = new Vector3[GridN * GridN];
        for (var j = 0; j < GridN; j++)
        for (var i = 0; i < GridN; i++)
        {
            var x = GridX0 + i * GridStep;
            var z = GridZ0 + j * GridStep;
            var k = j * GridN + i;
            cols[k] = GroundColor(c, x, z, Canopy(c, x, z), path[k]);
            var dx = hf.At(i + 1, j) - hf.At(i - 1, j);
            var dz = hf.At(i, j + 1) - hf.At(i, j - 1);
            nrms[k] = new Vector3(-dx, 2f * GridStep, -dz).Normalized();
        }
        for (var cj = 0; cj < GridN - 1; cj += chunk)
        for (var ci = 0; ci < GridN - 1; ci += chunk)
        {
            var w = Mathf.Min(chunk, GridN - 1 - ci) + 1;
            var hgt = Mathf.Min(chunk, GridN - 1 - cj) + 1;
            var verts = new Vector3[w * hgt];
            var nv = new Vector3[w * hgt];
            var cv = new Color[w * hgt];
            var tan = new float[w * hgt * 4];
            for (var j = 0; j < hgt; j++)
            for (var i = 0; i < w; i++)
            {
                var gi = ci + i;
                var gj = cj + j;
                var k = gj * GridN + gi;
                var v = j * w + i;
                verts[v] = new Vector3(GridX0 + gi * GridStep, hf.H[k], GridZ0 + gj * GridStep);
                nv[v] = nrms[k];
                cv[v] = cols[k];
                var t = (Vector3.Right - nrms[k] * nrms[k].X).Normalized();
                tan[v * 4] = t.X; tan[v * 4 + 1] = t.Y; tan[v * 4 + 2] = t.Z; tan[v * 4 + 3] = -1f;
            }
            var idx = new int[(w - 1) * (hgt - 1) * 6];
            var n = 0;
            for (var j = 0; j < hgt - 1; j++)
            for (var i = 0; i < w - 1; i++)
            {
                // Même diagonale que la forme de collision : (i + 1, j) → (i, j + 1).
                var a = j * w + i;
                var b = a + 1;
                var d = a + w + 1;
                var e = a + w;
                idx[n++] = a; idx[n++] = b; idx[n++] = e;
                idx[n++] = b; idx[n++] = d; idx[n++] = e;
            }
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = verts;
            arrays[(int)Mesh.ArrayType.Normal] = nv;
            arrays[(int)Mesh.ArrayType.Tangent] = tan;
            arrays[(int)Mesh.ArrayType.Color] = cv;
            arrays[(int)Mesh.ArrayType.Index] = idx;
            var mesh = new ArrayMesh();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            mesh.SurfaceSetMaterial(0, mat);
            c.Root.AddChild(new MeshInstance3D { Name = $"Terrain_{ci}_{cj}", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.On });
        }
    }

    // ───────────────────────── Champ de flux ─────────────────────────

    /// <summary>
    /// Cellules de 4 m : rivière bloquée sans hauteur (infranchissable, même pour les grimpeurs) sauf aux gués et sous le
    /// pont ; lac bloqué ; pentes de plus de ~42° bloquées avec leur dénivelé (contournées par les marcheurs, escaladées
    /// par les grimpeurs si c'est plus court).
    /// </summary>
    private static void BlockFlow(Ctx c)
    {
        if (c.Flow == null) return;
        const float cell = FlowField.Cell;
        var hf = c.Hf;
        var cliffs = 0;
        var water = 0;
        for (var z = -FlowField.Extent + cell / 2; z < FlowField.Extent; z += cell)
        for (var x = -FlowField.Extent + cell / 2; x < FlowField.Extent; x += cell)
        {
            var p = new Vector3(x, 0, z);
            // Tablier du pont : la cellule n'est praticable que si son centre et ses abords (±1,2 m) sont sur le tablier
            // (sinon un insecte au bord de la cellule tomberait à côté, dans le lit, sous le pont).
            bool OnDeck(float px, float pz) => hf.Height(px, pz) > hf.Terrain(px, pz) + 0.5f;
            var deck = OnDeck(x, z) && OnDeck(x + 1.2f, z) && OnDeck(x - 1.2f, z) && OnDeck(x, z + 1.2f) && OnDeck(x, z - 1.2f);
            var river = z > DamToe && Mathf.Abs(RiverOffset(x, z)) < HalfWidth(z) + 1.5f && FordAt(z) < 0.5f;
            var lake = z < DamUp && hf.Terrain(x, z) < LakeY + 0.2f;
            if ((river || lake) && !deck)
            {
                c.Flow.SetBlockedRect(p, Vector3.Zero, true, 0f);
                water++;
                continue;
            }
            // Pente : dénivelé maximal entre coins de la cellule.
            float lo = float.MaxValue, hi = float.MinValue;
            for (var k = 0; k < 9; k++)
            {
                var h = hf.Terrain(x + (k % 3 - 1) * cell * 0.5f, z + (k / 3 - 1) * cell * 0.5f);
                lo = Mathf.Min(lo, h);
                hi = Mathf.Max(hi, h);
            }
            if (hi - lo > cell * 0.9f)
            {
                c.Flow.SetBlockedRect(p, new Vector3(0.1f, hi - lo, 0.1f), true, 0f);
                cliffs++;
            }
        }
        if (Screenshot.CmdArg("fps") != null) GD.Print($"[vallée] champ de flux : {water} cellules d'eau, {cliffs} cellules de falaise");
    }
}
