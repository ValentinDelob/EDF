using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Base submergée : avion de transport écrasé en feu sur la piste, chasseur abattu au nord, épaves de chars et de
/// blindés calcinées (destructibles, aplaties par les gros ennemis), camions et véhicules légers brûlés, cratères
/// d'obus (lèvres de sable, éclats de béton), dunes qui envahissent la base (collision exacte, escaladables),
/// affleurements rocheux, filets de camouflage, toiles et nids, débris épars, panaches à l'horizon.
/// Véhicules pilotables supplémentaires (« véhicules abondants »).
/// </summary>
public static partial class DesertBuilder
{
    /// <summary>Véhicules pilotables en plus du char et de l'hélicoptère de départ : trois chars, deux hélicoptères.</summary>
    private static readonly (bool heli, Vector3 pos, float yaw)[] ExtraVehicles =
    {
        (false, new Vector3(70, 0.3f, 4), 0f),
        (false, new Vector3(88, 0.3f, 4), 0f),
        (false, new Vector3(-88, 0.3f, 4), Mathf.Pi / 2),
        (true, new Vector3(-78, 0.3f, 62), Mathf.Pi),
        (true, new Vector3(-50, 0.3f, 112), Mathf.Pi),
    };

    private static void BuildVehicles(Ctx c)
    {
        foreach (var (heli, p, yaw) in ExtraVehicles)
        {
            c.Res.Vehicles.Add((heli, p, yaw));
            Take(c, p, heli ? 7f : 4.5f, heli ? 7f : 5f, 0f);
            c.Ruins.Take(p, heli ? 8f : 5f);
        }
    }

    // ───────────────────────── Habillage ─────────────────────────

    private static void DressDesert(Ctx c)
    {
        var r = c.Ruins;
        var rng = r.Rng;
        foreach (var n in NestSpots) r.Block(n, 10f, 10f);

        // Dunes qui envahissent la base (d'abord : le reste du décor les évite).
        foreach (var (x, z, rx, rz, h, yaw) in Dunes) Dune(c, new Vector3(x, 0, z), rx, rz, h, yaw);
        // Affleurements rocheux.
        foreach (var (x, z, s) in new[] { (168f, -190f, 1.3f), (-150f, -193f, 1f), (-192f, 74f, 0.8f), (-112f, 150f, 1.1f), (150f, 150f, 0.9f), (192f, -98f, 0.7f) })
            Outcrop(c, new Vector3(x, 0, z), s, (int)(x * 7 + z));

        // Avion de transport écrasé sur la piste : incendie, colonne de fumée visible de partout, traînée de débris.
        var crash = new Vector3(44, 0, -160);
        {
            var k = c.Solid.At(crash);
            k.Xf = new Transform3D(new Basis(Vector3.Up, -1.75f), crash);
            Props.TransportWreck(k);
            k.Xf = Transform3D.Identity;
            r.Fire(crash + new Vector3(0, 2.2f, 1), 3.4f);
            r.Fire(crash + new Vector3(-6, 0.5f, -12), 2f, false);
            c.Root.AddChild(RuinFire.SmokeColumn(crash + Vector3.Up * 8f, 4.5f, true));
            Decal(c, Mats.Decal.Scorch, crash, 44f, 0.2f, 0.03f, 1.4f);
            Decal(c, Mats.Decal.Oil, crash + new Vector3(12, 0, 6), 18f, 0.9f, 0.031f, 1.5f);
            for (var i = 0; i < 26; i++)
            {
                var p = crash + new Vector3(18f + i * 4.2f + RuinKit.Hash(i, 1, 104) * 3f, 0, (RuinKit.Hash(i, 2, 104) - 0.5f) * 14f);
                Decal(c, i % 3 == 0 ? Mats.Decal.Scorch : Mats.Decal.Cracks, p, 4f + RuinKit.Hash(i, 3, 104) * 5f, RuinKit.Hash(i, 4, 104) * 6f, 0.032f + i * 0.0002f, 1.6f);
                r.Inst(Props.Debris((int)(RuinKit.Hash(i, 5, 104) * Props.DebrisKinds)), RuinKit.At(p, RuinKit.Hash(i, 6, 104) * 6f, 0, 0, 1.4f), true, 220f);
            }
            Take(c, crash, 22f, 16f, 0f);
        }
        // Chasseur abattu dans la bande nord.
        {
            var p = new Vector3(118, 0, -191);
            var k = c.Solid.At(p);
            k.Xf = new Transform3D(new Basis(Vector3.Up, 2.4f) * new Basis(Vector3.Back, 0.12f), p + Vector3.Down * 0.4f);
            Props.FighterWreck(k);
            k.Xf = Transform3D.Identity;
            r.Smolder(p + Vector3.Up * 2f, 2f);
            Decal(c, Mats.Decal.Dust, p + new Vector3(6, 0, 3), 16f, 0.3f, 0.03f);
            Take(c, p, 9f, 9f, 0f);
        }

        // Épaves de chars et de blindés (destructibles « épave »), certaines brûlent encore.
        foreach (var (x, z, yaw, kind, fire) in new[]
        {
            (-110f, -26f, 0.7f, 0, true), (126f, -31f, -0.4f, 1, false), (72f, -32f, 1.2f, 2, true), (-30f, 72f, 2.6f, 0, false),
            (118f, 72f, 0.3f, 2, false), (58f, -194f, 1.9f, 1, true), (-150f, 150f, 0.4f, 0, false), (-58f, -128f, 1.57f, 1, false),
        })
        {
            var p = new Vector3(x, 0, z);
            if (!r.TryTake(p, 4.5f)) continue;
            var mesh = kind == 2 ? Props.ApcWreck() : Props.TankWreck(kind);
            c.Props.Add(mesh, p, yaw, range: 380f, shadows: true);
            Decal(c, Mats.Decal.Scorch, p, 6f, yaw, 0.03f);
            if (fire) r.Fire(p + Vector3.Up * 1.6f, 1.5f);
            else r.Smolder(p + Vector3.Up * 1.8f, 1.1f);
        }
        // Camions et véhicules légers calcinés (décor fixe), éparpillés.
        foreach (var (x, z, yaw, truck) in new[]
        {
            (-130f, -36f, 0.4f, true), (96f, -36f, 2.2f, false), (-96f, 76f, 1.1f, false), (30f, 64f, 2.9f, true),
            (140f, 2f, 0.2f, false), (-60f, 128f, 1.5f, true), (178f, 40f, 1.8f, false), (-178f, -108f, 0.9f, true),
        })
        {
            var p = new Vector3(x, 0, z);
            if (!r.TryTake(p, truck ? 4.5f : 3f) || IsTaken(c, p, 2f)) continue;
            var mesh = truck ? (Mesh)Props.WreckOf(Props.MilTruck()) : Props.MilJeepWreck(((int)x & 1));
            r.Inst(mesh, RuinKit.At(p, yaw), true, 330f);
            r.BoxOnGround(p, yaw, truck ? new Vector3(2.6f, 3f, 7.8f) : Props.MilJeepSize);
            Decal(c, Mats.Decal.Scorch, p, truck ? 6.5f : 5f, yaw, 0.029f);
            if (RuinKit.Hash(x, z, 105) < 0.35f) r.Smolder(p + Vector3.Up * 1.4f, 0.9f);
        }

        // Cratères d'obus : marque au sol, lèvre de sable (basse, praticable), éclats de béton sur le béton.
        for (var i = 0; i < 22; i++)
        {
            var p = new Vector3(rng.RandfRange(-200f, 200f), 0, rng.RandfRange(-200f, 130f));
            var size = rng.RandfRange(7f, 14f);
            if (IsTaken(c, p, size * 0.5f) || !r.Free(p, size * 0.5f)) continue;
            Crater(c, p, size, i);
        }
        // Cratères sur la piste (béton éclaté).
        foreach (var (x, z, s) in new[] { (-120f, -150f, 12f), (-60f, -166f, 9f), (130f, -146f, 11f), (-180f, -160f, 10f), (10f, -112f, 8f), (-20f, -80f, 9f) })
        {
            var p = new Vector3(x, 0, z);
            if (!r.Free(p, s * 0.5f)) continue;
            Crater(c, p, s, (int)(x + z));
        }

        // Filets de camouflage (perches solides, filet traversable).
        foreach (var (x, z, yaw) in new[] { (-112f, 58f, 0.2f), (64f, 76f, -0.3f), (-40f, -60f, 0.1f) })
        {
            var p = new Vector3(x, 0, z);
            if (!r.TryTake(p, 6f)) continue;
            Props.CamoCanopy(c.Solid.At(p), p, yaw, 4.5f, 3.5f, 3.4f, (int)x);
            c.Props.Add(Props.AmmoCrates(2), p + new Vector3(1.5f, 0, 0.5f), yaw, range: 150f, shadows: true);
        }

        // Toiles géantes : entre le radôme et le pylône, devant un abri, entre deux miradors du sud.
        Props.Web(r.Kit(RuinKit.Layer.Organic, Shelters[1]), new[]
        {
            Shelters[1] + new Vector3(-11f, 1f, -17.5f), Shelters[1] + new Vector3(-4f, 9.5f, -17.5f), Shelters[1] + new Vector3(5f, 9f, -17.5f), Shelters[1] + new Vector3(11f, 1.5f, -17.5f),
        }, 91);
        Props.Web(r.Kit(RuinKit.Layer.Organic, Tower), new[]
        {
            Tower + new Vector3(11.2f, 7.5f, -3f), Tower + new Vector3(12.4f, 20f, -3f), Tower + new Vector3(13f, 20f, 3f), Tower + new Vector3(11.2f, 6f, 5f),
        }, 92);
        Props.Web(r.Kit(RuinKit.Layer.Organic, HangarA), new[]
        {
            HangarA + new Vector3(-29f, 0.5f, -23.4f), HangarA + new Vector3(-29f, 15f, -23.4f), HangarA + new Vector3(-12f, 15.5f, -23.4f), HangarA + new Vector3(-18f, 0.5f, -23.4f),
        }, 93);

        // Nids : œufs, résine et hémolymphe autour des emplacements.
        foreach (var n in NestSpots) NestGrowth(c, n);

        // Marques au sol discrètes : cendres, fissures, hémolymphe, brûlures ; rares.
        for (var i = 0; i < 70; i++)
        {
            var p = new Vector3(rng.RandfRange(-215f, 215f), 0, rng.RandfRange(-215f, 155f));
            if (IsTaken(c, p, 1f) && rng.Randf() < 0.7f) continue;
            var roll = rng.Randf();
            var kind = Mats.Decal.Dust; // sur le sable clair, cendres et brûlures font des taches noires
            var size = kind is Mats.Decal.Scorch or Mats.Decal.Crater or Mats.Decal.Ash or Mats.Decal.Dust ? rng.RandfRange(5f, 11f) : rng.RandfRange(2f, 4.5f);
            Decal(c, kind, p, size, rng.RandfRange(0, Mathf.Tau), 0.035f + i * 0.0001f, rng.RandfRange(0.7f, 1.6f));
        }

        Litter(c);
        r.DistantPlumes(new[]
        {
            new Vector3(-520, 30, -420), new Vector3(80, 40, -640), new Vector3(460, 30, -380),
            new Vector3(620, 25, 120), new Vector3(-600, 25, 60), new Vector3(-180, 10, 560), new Vector3(300, 10, 520),
        });
    }

    // ───────────────────────── Dunes ─────────────────────────

    /// <summary>Dunes dans l'aire de jeu : centre (x, z), demi-axes, hauteur, orientation de la crête.</summary>
    private static readonly (float x, float z, float rx, float rz, float h, float yaw)[] Dunes =
    {
        // Bande nord, contre le rempart (en dehors de la zone nord et du nid).
        (-170f, -193f, 22f, 8f, 2.6f, 0.05f), (-118f, -190f, 14f, 7f, 1.9f, -0.1f), (40f, -190f, 18f, 8f, 2.2f, 0.08f), (165f, -192f, 20f, 7f, 2.4f, 0f),
        // Sable libre entre les abris et la place d'armes, entre le hangar est et l'auvent, derrière l'auvent.
        (-72f, 26f, 15f, 10f, 2.8f, 0.4f), (122f, -10f, 12f, 8f, 2.2f, -0.3f), (86f, 66f, 14f, 8f, 2.4f, 0.2f),
        // Coin sud-ouest et route du désert, hors du rempart.
        (-186f, 130f, 14f, 5f, 1.6f, 0f), (-40f, 150f, 16f, 7f, 2.6f, 0.1f), (120f, 151f, 18f, 6f, 2.4f, -0.05f), (-170f, 151f, 20f, 6f, 2.8f, 0.05f),
        // Contre le rempart est.
        (199f, -28f, 6f, 15f, 1.8f, 0f), (-199f, -140f, 5f, 12f, 1.5f, 0f),
    };

    /// <summary>Hauteur d'une dune (repère local, unités des demi-axes) : versant au vent doux, face sous le vent raide, rides.</summary>
    private static float DuneHeight(float u, float v, float h, int seed)
    {
        var d = Mathf.Sqrt(u * u + v * v);
        if (d >= 1f) return -0.03f;
        var t = 1f - d;
        // Crête décalée sous le vent (+u), face raide.
        var lee = u > 0.15f ? Mathf.Pow(t, 0.75f) : Mathf.Pow(t, 1.25f);
        var y = h * Mathf.SmoothStep(0f, 1f, lee) * (1f + 0.25f * Mathf.Clamp(u, -1f, 1f));
        y += Mathf.Sin(u * 21f + v * 4f + seed) * 0.06f * t;
        return y - 0.03f;
    }

    /// <summary>Dune de sable (grille 18 × 12, normales lissées, crête plus claire) ; collision exacte.</summary>
    private static void Dune(Ctx c, Vector3 center, float rx, float rz, float h, float yaw)
    {
        var k = c.Solid.At(center);
        var sand = Mats.DesertSand();
        var b = new Basis(Vector3.Up, yaw);
        const int nu = 18, nv = 12;
        var seed = (int)(center.X + center.Z);
        var pts = new Vector3[(nu + 1) * (nv + 1)];
        var cols = new Color[pts.Length];
        for (var j = 0; j <= nv; j++)
        for (var i = 0; i <= nu; i++)
        {
            var u = -1f + 2f * i / nu;
            var v = -1f + 2f * j / nv;
            var y = DuneHeight(u, v, h, seed);
            pts[j * (nu + 1) + i] = center + b * new Vector3(u * rx, y, v * rz);
            var t = Mathf.Clamp(y / h, 0f, 1f);
            cols[j * (nu + 1) + i] = new Color(0.94f, 0.92f, 0.88f).Lerp(new Color(1.04f, 1.02f, 0.98f), t);
        }
        Vector3 N(int i, int j)
        {
            var l = pts[j * (nu + 1) + Mathf.Max(i - 1, 0)];
            var rr = pts[j * (nu + 1) + Mathf.Min(i + 1, nu)];
            var d = pts[Mathf.Max(j - 1, 0) * (nu + 1) + i];
            var f = pts[Mathf.Min(j + 1, nv) * (nu + 1) + i];
            var n = (f - d).Cross(rr - l).Normalized();
            return n.Y < 0 ? -n : n;
        }
        for (var j = 0; j < nv; j++)
        for (var i = 0; i < nu; i++)
        {
            var a = j * (nu + 1) + i;
            var bb = a + 1;
            var e = a + nu + 2;
            var f = a + nu + 1;
            if (pts[a].Y < 0 && pts[bb].Y < 0 && pts[e].Y < 0 && pts[f].Y < 0) continue;
            k.TriSmooth(sand, pts[a], pts[bb], pts[e], N(i, j), N(i + 1, j), N(i + 1, j + 1), cols[a], cols[bb], cols[e]);
            k.TriSmooth(sand, pts[a], pts[e], pts[f], N(i, j), N(i + 1, j + 1), N(i, j + 1), cols[a], cols[e], cols[f]);
        }
        var ext = Mathf.Max(rx, rz);
        c.Ruins.Block(new Rect2(center.X - ext * 0.8f, center.Z - ext * 0.8f, ext * 1.6f, ext * 1.6f));
        c.Taken.Add(new Rect2(center.X - ext * 0.8f, center.Z - ext * 0.8f, ext * 1.6f, ext * 1.6f));
    }

    // ───────────────────────── Rochers ─────────────────────────

    /// <summary>Affleurement rocheux : quelques blocs de grès en strates (sphères déformées aplaties), collision exacte.</summary>
    private static void Outcrop(Ctx c, Vector3 p, float scale, int seed)
    {
        var k = c.Solid.At(p);
        var rock = Mats.DesertRock();
        var n = 3 + (int)(RuinKit.Hash(seed, 1, 106) * 3);
        for (var i = 0; i < n; i++)
        {
            var a = RuinKit.Hash(seed, i, 107) * Mathf.Tau;
            var d = i == 0 ? 0f : (2.5f + RuinKit.Hash(seed, i, 108) * 4f) * scale;
            var size = (i == 0 ? 4.5f : 1.6f + RuinKit.Hash(seed, i, 109) * 2.4f) * scale;
            var height = size * (i == 0 ? 1.1f : 0.6f + RuinKit.Hash(seed, i, 110) * 0.6f);
            Boulder(k, rock, p + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d), size, height, seed * 13 + i);
        }
        Decal(c, Mats.Decal.Dust, p, 12f * scale, seed * 0.3f, 0.03f);
        Take(c, p, 7f * scale, 7f * scale, 0f);
        FlowBlock(c, p, new Vector3(6f * scale, 4f * scale, 6f * scale), 0f);
    }

    /// <summary>Bloc rocheux : sphère grossière déformée par hachage, base enterrée, sommet tronqué (table).</summary>
    private static void Boulder(MeshKit k, Material m, Vector3 c, float r, float h, int seed)
    {
        const int segU = 14, segV = 7;
        var pts = new Vector3[(segU + 1) * (segV + 1)];
        for (var j = 0; j <= segV; j++)
        for (var i = 0; i <= segU; i++)
        {
            var lon = (i % segU) * Mathf.Tau / segU;
            var lat = Mathf.Lerp(-0.35f, Mathf.Pi / 2, j / (float)segV);
            // Rayon bosselé (deux échelles) ; les lits durs font saillie par endroits.
            var rr = r * (0.7f + 0.3f * RuinKit.Hash(seed + i % segU, j, 111) + 0.18f * Mathf.Sin(lon * 2f + seed) * Mathf.Cos(lat));
            if (j % 2 == 1) rr *= 1.06f;
            var y = Mathf.Min(Mathf.Sin(lat) * h * 1.1f, h * 0.92f) + (RuinKit.Hash(seed + i % segU, j, 114) - 0.5f) * h * 0.08f;
            pts[j * (segU + 1) + i] = c + new Vector3(Mathf.Cos(lon) * Mathf.Cos(lat) * rr, y, Mathf.Sin(lon) * Mathf.Cos(lat) * rr);
        }
        var col = new Color(0.95f, 0.93f, 0.9f) * (0.85f + RuinKit.Hash(seed, 3, 112) * 0.2f);
        k.Col = col;
        for (var j = 0; j < segV; j++)
        for (var i = 0; i < segU; i++)
        {
            var a = pts[j * (segU + 1) + i];
            var b = pts[j * (segU + 1) + i + 1];
            var e = pts[(j + 1) * (segU + 1) + i + 1];
            var f = pts[(j + 1) * (segU + 1) + i];
            k.Tri(m, a, b, e, c + Vector3.Up * h * 0.3f);
            k.Tri(m, a, e, f, c + Vector3.Up * h * 0.3f);
        }
        // Dessus plat (table).
        var top = c + Vector3.Up * h * 0.92f;
        for (var i = 0; i < segU; i++)
            k.Tri(m, pts[segV * (segU + 1) + i], pts[segV * (segU + 1) + i + 1], top, top + Vector3.Down);
    }

    // ───────────────────────── Cratères, nids, débris ─────────────────────────

    /// <summary>Cratère : marque au sol, lèvre de sable en anneau bas (praticable), quelques éclats.</summary>
    private static void Crater(Ctx c, Vector3 p, float size, int seed)
    {
        var r = c.Ruins;
        // Fond de cendre et de sable brûlé (pas de disque noir : on lirait un trou), fissures sur le béton.
        Decal(c, Mats.Decal.Dust, p, size * 1.2f, seed * 0.3f, 0.034f + (seed & 15) * 0.0003f);
        if (p.Z < ApronZ1 && p.Z > RunwayZ0) Decal(c, Mats.Decal.Scorch, p, size * 0.7f, seed * 0.9f, 0.035f + (seed & 15) * 0.0003f); // béton noirci au point d'impact
        var k = c.Solid.At(p);
        var sand = Mats.DesertSand();
        const int seg = 20;
        // Lèvre déchiquetée : rayons et hauteurs irréguliers, bourrelets de terre projetée.
        float Rad(int i, float b, int salt) => b * (0.82f + 0.36f * RuinKit.Hash(seed, i % seg, salt));
        float Hgt(int i) => 0.12f + RuinKit.Hash(seed, i % seg, 113) * 0.42f;
        Vector3 P(int i, float rr, float y)
        {
            var a = i * Mathf.Tau / seg;
            return p + new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
        }
        for (var i = 0; i < seg; i++)
        {
            var j = i + 1;
            var out0 = new Vector3(Mathf.Cos(i * Mathf.Tau / seg), 0, Mathf.Sin(i * Mathf.Tau / seg));
            k.Col = new Color(0.8f, 0.76f, 0.7f) * (0.9f + RuinKit.Hash(seed, i, 117) * 0.15f);
            k.OrientedQuad(sand, P(i, Rad(i, size * 0.3f, 118), 0.03f), P(j, Rad(j, size * 0.3f, 118), 0.03f), P(j, Rad(j, size * 0.45f, 119), Hgt(j)), P(i, Rad(i, size * 0.45f, 119), Hgt(i)), Vector3.Up - out0);
            k.Col = new Color(0.9f, 0.87f, 0.82f);
            k.OrientedQuad(sand, P(i, Rad(i, size * 0.45f, 119), Hgt(i)), P(j, Rad(j, size * 0.45f, 119), Hgt(j)), P(j, Rad(j, size * 0.7f, 120), -0.03f), P(i, Rad(i, size * 0.7f, 120), -0.03f), Vector3.Up + out0);
        }
        var ro = size * 0.7f;
        for (var i = 0; i < 5; i++)
        {
            var a = RuinKit.Hash(seed, i, 114) * Mathf.Tau;
            var d = ro + RuinKit.Hash(seed, i, 115) * size * 0.5f;
            r.Inst(Props.Debris((int)(RuinKit.Hash(seed, i, 116) * Props.DebrisKinds)), RuinKit.At(p + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d), a, 0, 0, 1.2f), false, 120f);
        }
        r.Take(p, ro);
    }

    /// <summary>Œufs, résine et hémolymphe autour d'un emplacement de nid.</summary>
    private static void NestGrowth(Ctx c, Vector3 nest)
    {
        var r = c.Ruins;
        var seed = (int)(Mathf.Abs(nest.X) * 3 + Mathf.Abs(nest.Z));
        for (var i = 0; i < 4; i++)
        {
            var a = i * Mathf.Tau / 4 + RuinKit.Hash(seed, i, 2);
            var p = nest + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (10f + i);
            Props.EggCluster(r.Kit(RuinKit.Layer.Organic, p), p, 0.9f + RuinKit.Hash(seed, i, 3) * 0.6f, seed + i * 7);
            Decal(c, Mats.Decal.Acid, p, 5f, a, 0.038f);
        }
        for (var i = 0; i < 6; i++)
        {
            var a = RuinKit.Hash(seed, i, 4) * Mathf.Tau;
            var d = 4f + RuinKit.Hash(seed, i, 5) * 14f;
            var p = nest + new Vector3(Mathf.Cos(a) * d, 0, Mathf.Sin(a) * d);
            Decal(c, Mats.Decal.Acid, p, 2f + RuinKit.Hash(seed, i, 6) * 2.5f, a, 0.036f + i * 0.0003f);
        }
    }

    /// <summary>Débris épars (MultiMesh, sans collision ni ombres), plus rares que dans les cartes urbaines.</summary>
    private static void Litter(Ctx c)
    {
        var r = c.Ruins;
        const float step = 3.2f;
        var idx = 0;
        for (var z = -Half + 2; z < South - 2; z += step)
        for (var x = -Half + 2; x < Half - 2; x += step, idx++)
        {
            var jx = x + (RuinKit.Hash(idx, 1, 6) - 0.5f) * step;
            var jz = z + (RuinKit.Hash(idx, 2, 6) - 0.5f) * step;
            var p = new Vector3(jx, 0, jz);
            var spawn = SpawnArea.HasPoint(new Vector2(jx, jz));
            if (RuinKit.Hash(idx, 3, 6) > (spawn ? 0.01f : 0.03f) || IsTaken(c, p, 0.3f) && !spawn) continue;
            var kind = (int)(RuinKit.Hash(idx, 4, 6) * Props.DebrisKinds);
            r.Inst(Props.Debris(kind), RuinKit.At(p, RuinKit.Hash(idx, 6, 6) * Mathf.Tau, 0, 0, 0.8f + RuinKit.Hash(idx, 7, 6) * 0.6f), false, 95f);
        }
    }
}
