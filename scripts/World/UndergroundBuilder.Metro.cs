using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Réseau souterrain : station centrale (quai central, piliers carrelés, escalators vers une passerelle, poste de
/// combat de l'armée, rames abandonnées), voies et tunnels (ballast, traverses, rails, chemins de câbles, lampes de
/// secours, signaux, rame déraillée), atelier de maintenance.
/// </summary>
public static partial class UndergroundBuilder
{
    /// <summary>Quai central de la station : x ±78, z -23,4 → 14,8 (bords au ras des rames).</summary>
    private static readonly Rect2 Platform = new(-78, -23.4f, 156, 38.2f);

    /// <summary>Axes des voies : deux voies par tube, à ±2,6 m de l'axe du tunnel.</summary>
    private const float TrackOff = 2.6f;

    // ───────────────────────── Station ─────────────────────────

    private static void BuildStation(Ctx c)
    {
        var solid = c.Solid;
        var det = Mats.Detail();
        var tiles = Mats.StationTiles();
        var p = Platform;
        float x0 = p.Position.X, x1 = p.End.X, z0 = p.Position.Y, z1 = p.End.Y;
        // Corps du quai (béton sombre), dallage, bordure claire et bande d'éveil.
        var k = solid.At(new Vector3(0, 0, 0));
        k.Col = new Color(0.36f, 0.35f, 0.33f);
        k.BoxMinMax(Mats.Concrete(new Color(0.42f, 0.41f, 0.39f), 1811), new Vector3(x0, 0, z0 + 0.35f), new Vector3(x1, PlatformH - 0.08f, z1 - 0.35f), false);
        k.Col = new Color(0.72f, 0.71f, 0.68f);
        k.BoxMinMax(Mats.Sidewalk(), new Vector3(x0, PlatformH - 0.08f, z0 + 0.6f), new Vector3(x1, PlatformH, z1 - 0.6f), false);
        foreach (var (za, zb) in new[] { (z0, z0 + 0.6f), (z1 - 0.6f, z1) })
        {
            k.Col = new Color(0.78f, 0.77f, 0.72f);
            k.BoxMinMax(det, new Vector3(x0, PlatformH - 0.22f, za), new Vector3(x1, PlatformH, zb), false);
            // Bande podotactile jaune délavé.
            k.Col = new Color(0.62f, 0.52f, 0.2f);
            var zm = za < 0 ? zb + 0.3f : za - 0.3f;
            k.BoxMinMax(det, new Vector3(x0 + 0.5f, PlatformH, zm - 0.2f), new Vector3(x1 - 0.5f, PlatformH + 0.012f, zm + 0.2f), false);
        }
        // Rampes de service aux deux bouts du quai (jusqu'au mur de tête).
        foreach (var sgn in new[] { -1f, 1f })
        {
            var xa = sgn > 0 ? x1 : x0;
            var xb = xa + sgn * 5.5f;
            var rk = solid.At(new Vector3(xa, 0, 0));
            rk.Col = new Color(0.4f, 0.39f, 0.37f);
            var a0 = new Vector3(xa, PlatformH, z0 + 4);
            var a1 = new Vector3(xa, PlatformH, z1 - 4);
            var b0 = new Vector3(xb, 0.02f, z0 + 4);
            var b1 = new Vector3(xb, 0.02f, z1 - 4);
            rk.OrientedQuad(Mats.Concrete(new Color(0.42f, 0.41f, 0.39f), 1811), a0, a1, b1, b0, Vector3.Up);
            rk.Tri(Mats.Concrete(new Color(0.42f, 0.41f, 0.39f), 1811), a0, b0, new Vector3(xa, 0, z0 + 4), new Vector3(xa, 0.3f, 0));
            rk.Tri(Mats.Concrete(new Color(0.42f, 0.41f, 0.39f), 1811), a1, b1, new Vector3(xa, 0, z1 - 4), new Vector3(xa, 0.3f, 0));
        }
        // Le quai se franchit en grimpant (insectes) ; les escouades restent sur les voies (elles marchent au niveau du sol).
        FlowObstacle(c, new Vector3(0, 0, (z0 + z1) / 2), new Vector3(x1 - x0, PlatformH, z1 - z0));

        // Piliers carrelés jusqu'à la voûte (deux files) : couvert et prises pour les insectes.
        for (var x = -65f; x <= 65f; x += 13f)
            foreach (var z in new[] { -13f, 4.5f })
            {
                if (Mathf.Abs(x - 26f) < 9f && z < 0) continue; // escalators
                var top = CeilAt(c, x, z, out _) + 0.6f;
                var pk = solid.At(new Vector3(x, 0, z));
                pk.Col = new Color(0.85f, 0.84f, 0.8f);
                pk.BoxMinMax(tiles, new Vector3(x - 0.65f, PlatformH, z - 0.65f), new Vector3(x + 0.65f, top, z + 0.65f), false);
                pk.Col = new Color(0.25f, 0.25f, 0.24f);
                pk.BoxMinMax(det, new Vector3(x - 0.72f, PlatformH, z - 0.72f), new Vector3(x + 0.72f, PlatformH + 0.3f, z + 0.72f), false);
                // Plaque de nom de station (émail bleu sombre, liseré clair) sur deux faces.
                foreach (var face in new[] { -1f, 1f })
                {
                    var dk = c.Dressing.At(new Vector3(x, 0, z));
                    dk.Col = new Color(0.08f, 0.13f, 0.22f);
                    dk.BoxMinMax(Mats.DetailMetal(), new Vector3(x - 0.55f, PlatformH + 2.4f, z + face * 0.66f - 0.02f), new Vector3(x + 0.55f, PlatformH + 2.75f, z + face * 0.66f + 0.02f));
                    dk.Col = new Color(0.75f, 0.75f, 0.7f);
                    dk.BoxMinMax(Mats.Detail(), new Vector3(x - 0.42f, PlatformH + 2.55f, z + face * 0.685f - 0.01f), new Vector3(x + 0.3f, PlatformH + 2.6f, z + face * 0.685f + 0.01f));
                }
            }

        BuildEscalators(c);
        BuildArmyPost(c);

        // Mobilier du quai (destructible) : bancs, corbeilles, distributeurs, panneaux d'information.
        for (var x = -58f; x <= 58f; x += 19f)
        {
            c.Props.Add(Props.Bench(), new Vector3(x + 4f, PlatformH, -17.5f), 0f);
            c.Props.Add(Props.Bench(), new Vector3(x - 3f, PlatformH, 9f), Mathf.Pi);
            c.Props.Add(Props.Bin(), new Vector3(x + 7.5f, PlatformH, -18f), 0.3f);
            if (Hh(x, 1, 31) > 0.4f) Vending(c, new Vector3(x - 8f, PlatformH, -4.5f), Hh(x, 2, 31) > 0.5f ? 0f : Mathf.Pi, Hh(x, 3, 31) > 0.45f);
        }
        // Rames : une rame abandonnée portes ouvertes le long du quai nord, une rame incendiée sur la voie sud du fond.
        Train(c, new Vector3(-66, 0, -28f + TrackOff), Mathf.Pi / 2, 4, false, 11);
        Train(c, new Vector3(18, 0, 19f + TrackOff), Mathf.Pi / 2, 3, true, 12);
        Fire(c, new Vector3(30f, 1.2f, 19f + TrackOff), 1.3f);
        Fire(c, new Vector3(41f, 1.2f, 19f + TrackOff), 0.8f, false);
        // Voies de la station.
        Track(c, new[] { new Vector2(-84, -28 - TrackOff), new Vector2(84, -28 - TrackOff) });
        Track(c, new[] { new Vector2(-84, -28 + TrackOff), new Vector2(84, -28 + TrackOff) });
        Track(c, new[] { new Vector2(-84, 19 - TrackOff), new Vector2(84, 19 - TrackOff) });
        Track(c, new[] { new Vector2(-84, 19 + TrackOff), new Vector2(84, 19 + TrackOff) });
        // Débris sur le quai et les voies.
        for (var i = 0; i < 46; i++)
        {
            var x = -80f + Hh(i, 7, 41) * 160f;
            var z = -34f + Hh(i, 8, 41) * 60f;
            var onPlat = z > z0 && z < z1;
            Debris(c, new Vector3(x, onPlat ? PlatformH : 0f, z), i);
        }
    }

    /// <summary>Distributeur automatique (vitrine éclairée ou éteinte), solide.</summary>
    private static void Vending(Ctx c, Vector3 p, float yaw, bool lit)
    {
        var k = c.Solid.At(p);
        var keep = k.Xf;
        k.Xf = new Transform3D(new Basis(Vector3.Up, yaw), p);
        k.Col = new Color(0.32f, 0.08f, 0.07f);
        k.Box(Mats.DetailMetal(), new Vector3(0, 0.95f, 0), new Vector3(1.0f, 1.9f, 0.8f));
        k.Xf = keep;
        var g = c.Glows.At(p);
        g.Xf = new Transform3D(new Basis(Vector3.Up, yaw), p);
        g.Box(lit ? Mats.Glow(new Color(0.75f, 0.85f, 0.9f), 1.4f) : Mats.Glass(), new Vector3(-0.12f, 1.15f, 0.41f), new Vector3(0.62f, 1.1f, 0.02f));
        g.Xf = Transform3D.Identity;
        if (lit) Omni(c, p + new Basis(Vector3.Up, yaw) * new Vector3(0, 1.2f, 1.2f), new Color(0.7f, 0.85f, 0.95f), 0.6f, 5f, 0f);
    }

    /// <summary>
    /// Banc de trois escalators (montée vers l'est, 30°) jusqu'à une passerelle à 9 m qui franchit les voies nord
    /// jusqu'à une issue murée : point haut au-dessus de la station. Collision exacte (marches lissées en rampe).
    /// </summary>
    private static void BuildEscalators(Ctx c)
    {
        const float xa = 18f, rise = 8.1f, run = 14f, deckY = PlatformH + rise;
        const float za = -12.5f, zb = -2.5f;
        var xb = xa + run;
        var k = c.Solid.At(new Vector3(xa + run / 2, 0, -7));
        var met = Mats.DetailMetal();
        var det = Mats.Detail();
        for (var i = 0; i < 3; i++)
        {
            var zc = za + 1.7f + i * 3.3f;
            // Rampe praticable (dessus des marches), flancs, main courante.
            k.Col = new Color(0.22f, 0.22f, 0.21f);
            var a0 = new Vector3(xa, PlatformH + 0.05f, zc - 1.1f);
            var a1 = new Vector3(xa, PlatformH + 0.05f, zc + 1.1f);
            var b0 = new Vector3(xb, deckY, zc - 1.1f);
            var b1 = new Vector3(xb, deckY, zc + 1.1f);
            k.OrientedQuad(met, a0, b0, b1, a1, Vector3.Up);
            k.Solid = false;
            for (var s = 0.4f; s < run; s += 0.42f)
            {
                var y = PlatformH + rise * s / run;
                k.Col = new Color(0.3f, 0.3f, 0.28f);
                k.BoxMinMax(met, new Vector3(xa + s - 0.21f, y, zc - 1.0f), new Vector3(xa + s + 0.02f, y + 0.22f, zc + 1.0f));
            }
            k.Solid = true;
            foreach (var side in new[] { -1.25f, 1.25f })
            {
                k.Col = new Color(0.55f, 0.55f, 0.52f);
                var lo = new Vector3(xa - 1.2f, PlatformH, zc + side);
                var hi = new Vector3(xb + 0.5f, deckY, zc + side);
                var dir = (hi - lo).Normalized();
                var len = lo.DistanceTo(hi);
                var basis = Basis.LookingAt(dir, Vector3.Up);
                k.Box(det, (lo + hi) / 2 + Vector3.Up * 0.45f, new Vector3(0.18f, 1.1f, len), basis);
                k.Col = new Color(0.08f, 0.08f, 0.08f);
                k.Solid = false;
                k.Box(met, (lo + hi) / 2 + Vector3.Up * 1.05f, new Vector3(0.1f, 0.08f, len), basis);
                k.Solid = true;
            }
            // Caisson sous la rampe (soute des machines).
            k.Col = new Color(0.3f, 0.3f, 0.29f);
            k.Tri(det, new Vector3(xa, PlatformH, zc - 1.25f), new Vector3(xb, PlatformH, zc - 1.25f), new Vector3(xb, deckY, zc - 1.25f), new Vector3(xa, 0, zc));
            k.Tri(det, new Vector3(xa, PlatformH, zc + 1.25f), new Vector3(xb, PlatformH, zc + 1.25f), new Vector3(xb, deckY, zc + 1.25f), new Vector3(xa, 0, zc));
        }
        // Palier et passerelle vers le mur nord (z = -36), garde-corps ; issue murée de parpaings.
        k.Col = new Color(0.45f, 0.44f, 0.42f);
        k.BoxMinMax(Mats.Concrete(new Color(0.42f, 0.41f, 0.39f), 1811), new Vector3(xb, deckY - 0.5f, za - 1f), new Vector3(xb + 8f, deckY, zb + 1f));
        k.BoxMinMax(Mats.Concrete(new Color(0.42f, 0.41f, 0.39f), 1811), new Vector3(xb + 1.5f, deckY - 0.5f, -36.5f), new Vector3(xb + 6.5f, deckY, za - 1f));
        k.Col = new Color(0.3f, 0.3f, 0.28f);
        foreach (var (z, x) in new[] { (-30f, xb + 4f), (-24f, xb + 4f), (za - 1f, xb + 4f), (zb + 1f, xb + 1f), (zb + 1f, xb + 7f) })
            k.BoxMinMax(det, new Vector3(x - 0.3f, 0, z - 0.3f), new Vector3(x + 0.3f, deckY - 0.5f, z + 0.3f));
        foreach (var x in new[] { xb + 1.55f, xb + 6.45f })
        {
            k.BoxMinMax(met, new Vector3(x - 0.04f, deckY, -35.8f), new Vector3(x + 0.04f, deckY + 1.1f, za - 1f));
            k.BoxMinMax(met, new Vector3(x - 0.05f, deckY + 1.05f, -35.8f), new Vector3(x + 0.05f, deckY + 1.15f, za - 1f));
        }
        k.BoxMinMax(met, new Vector3(xb + 7.95f, deckY, za - 1f), new Vector3(xb + 8.05f, deckY + 1.1f, zb + 1f));
        k.Col = new Color(0.38f, 0.37f, 0.35f);
        k.BoxMinMax(det, new Vector3(xb + 1.2f, deckY, -36.6f), new Vector3(xb + 6.8f, deckY + 3.4f, -35.6f));
        FlowObstacle(c, new Vector3(xa + run / 2 + 2f, 0, (za + zb) / 2), new Vector3(run + 6f, 2f, zb - za + 2f));
    }

    /// <summary>
    /// Dernier poste de combat de l'armée sur le quai : murets de sacs de sable, caisses de munitions, fûts, projecteurs
    /// de chantier sur trépied braqués vers les tunnels, civière, fusées éclairantes sur les voies.
    /// </summary>
    private static void BuildArmyPost(Ctx c)
    {
        var k = c.Solid.At(new Vector3(-10, 0, 0));
        foreach (var (a, b, seed) in new[]
        {
            (new Vector2(-16, -21.5f), new Vector2(-16, -12f), 3), (new Vector2(-16, 4f), new Vector2(-16, 13f), 4),
            (new Vector2(12, -21.5f), new Vector2(12, -15f), 5), (new Vector2(-30, -2f), new Vector2(-24, -6f), 6),
        })
        {
            var (bc, size, yaw) = Props.SandbagWall(k, new Vector3(a.X, PlatformH, a.Y), new Vector3(b.X, PlatformH, b.Y), 6, seed);
            c.Body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Transform = new Transform3D(new Basis(Vector3.Up, yaw), bc) });
        }
        // Caisses et fûts (destructibles), projecteurs.
        c.Props.Add(Props.Drum(0), new Vector3(-13.5f, PlatformH, -9f), 0.2f);
        c.Props.Add(Props.Drum(1), new Vector3(-12.6f, PlatformH, -9.6f), 1.1f);
        c.Props.Add(Props.Barrier(0), new Vector3(-20f, PlatformH, 8f), 0.4f);
        c.Props.Add(Props.Barrier(1), new Vector3(10f, PlatformH, 10f), -0.3f);
        for (var i = 0; i < 5; i++) AmmoCrate(c, new Vector3(-12f + i * 0.9f, PlatformH, -1.5f - (i % 2) * 0.7f), i * 0.4f);
        Stretcher(c, new Vector3(-8f, PlatformH, 6f), 0.3f);
        WorkLight(c, new Vector3(-13f, PlatformH, -19.8f), new Vector3(-84, 3, -30), false);
        WorkLight(c, new Vector3(9.5f, PlatformH, 12.5f), new Vector3(84, 2, 22), false);
        Flare(c, new Vector3(-40f, 0.02f, -30f), 0.4f);
        Flare(c, new Vector3(52f, 0.02f, 23f), 2.1f);
        Flare(c, new Vector3(-74f, 0.02f, 17f), 1.4f);
    }

    /// <summary>Caisse de munitions olive (petit mobilier, solide).</summary>
    private static void AmmoCrate(Ctx c, Vector3 p, float yaw)
    {
        var k = c.Solid.At(p);
        var keep = k.Xf;
        k.Xf = new Transform3D(new Basis(Vector3.Up, yaw), p);
        k.Col = new Color(0.22f, 0.24f, 0.15f);
        k.Box(Mats.Detail(), new Vector3(0, 0.22f, 0), new Vector3(0.8f, 0.44f, 0.42f));
        k.Col = new Color(0.12f, 0.12f, 0.1f);
        k.Box(Mats.DetailMetal(), new Vector3(0, 0.45f, 0), new Vector3(0.2f, 0.04f, 0.1f));
        k.Xf = keep;
    }

    /// <summary>Civière abandonnée, couverture tachée.</summary>
    private static void Stretcher(Ctx c, Vector3 p, float yaw)
    {
        var k = c.Dressing.At(p);
        k.Xf = new Transform3D(new Basis(Vector3.Up, yaw), p);
        k.Col = new Color(0.25f, 0.27f, 0.2f);
        k.Box(Mats.Detail(), new Vector3(0, 0.25f, 0), new Vector3(0.6f, 0.06f, 2.0f));
        k.Col = new Color(0.3f, 0.12f, 0.1f);
        k.Box(Mats.Detail(), new Vector3(0.05f, 0.31f, 0.2f), new Vector3(0.55f, 0.08f, 1.1f));
        k.Col = new Color(0.4f, 0.4f, 0.4f);
        foreach (var x in new[] { -0.3f, 0.3f }) k.Box(Mats.DetailMetal(), new Vector3(x, 0.12f, 0), new Vector3(0.04f, 0.24f, 2.2f));
        k.Xf = Transform3D.Identity;
    }

    /// <summary>Projecteur de chantier sur trépied (deux lampes), braqué vers <paramref name="aim"/> ; ombres en option.</summary>
    private static void WorkLight(Ctx c, Vector3 p, Vector3 aim, bool shadows)
    {
        var k = c.Dressing.At(p);
        var met = Mats.DetailMetal();
        k.Col = new Color(0.12f, 0.12f, 0.12f);
        for (var i = 0; i < 3; i++)
        {
            var a = i * Mathf.Tau / 3f;
            k.Tube(met, p + new Vector3(Mathf.Cos(a) * 0.7f, 0.02f, Mathf.Sin(a) * 0.7f), p + Vector3.Up * 1.6f, 0.025f, 4);
        }
        k.Tube(met, p + Vector3.Up * 1.6f, p + Vector3.Up * 2.6f, 0.03f, 4);
        var head = p + Vector3.Up * 2.7f;
        var dir = (aim - head).Normalized();
        var basis = Basis.LookingAt(dir, Vector3.Up);
        k.Col = new Color(0.55f, 0.45f, 0.1f);
        k.Box(met, head, new Vector3(0.9f, 0.3f, 0.22f), basis);
        var g = c.Glows.At(p);
        g.Box(Mats.Glow(new Color(1f, 0.95f, 0.85f), 6f), head + dir * 0.12f, new Vector3(0.8f, 0.24f, 0.04f), basis);
        var spot = new SpotLight3D
        {
            Position = head + dir * 0.2f, LightColor = new Color(1f, 0.92f, 0.78f), LightEnergy = 9f, SpotRange = 90f, SpotAngle = 32f,
            SpotAttenuation = 0.9f, ShadowEnabled = shadows, LightVolumetricFogEnergy = 1.4f, LightBakeMode = Light3D.BakeMode.Disabled,
            DistanceFadeEnabled = true, DistanceFadeBegin = 110f, DistanceFadeLength = 30f,
        };
        c.Root.AddChild(spot);
        spot.LookAt(aim, Vector3.Up);
        c.Lights++;
    }

    /// <summary>Débris épars (morceaux de béton, carreaux, papiers, câble), sans collision.</summary>
    private static void Debris(Ctx c, Vector3 p, int seed)
    {
        var k = c.Dressing.At(p);
        var n = 3 + seed % 4;
        for (var i = 0; i < n; i++)
        {
            var q = p + new Vector3((Hh(seed, i, 51) - 0.5f) * 2.4f, 0, (Hh(seed, i, 52) - 0.5f) * 2.4f);
            var s = 0.12f + Hh(seed, i, 53) * 0.3f;
            k.Col = new Color(0.4f, 0.39f, 0.37f) * (0.6f + Hh(seed, i, 54) * 0.6f);
            k.Box(Mats.Detail(), q + Vector3.Up * s * 0.4f, new Vector3(s * 1.4f, s * 0.8f, s), new Basis(Vector3.Up, Hh(seed, i, 55) * 3f) * new Basis(Vector3.Right, (Hh(seed, i, 56) - 0.5f) * 0.6f));
        }
    }

    // ───────────────────────── Voies ─────────────────────────

    /// <summary>Voie le long d'une polyligne : ballast, traverses de béton tous les 0,9 m, deux rails (sans collision).</summary>
    private static void Track(Ctx c, Vector2[] pts)
    {
        var ballast = Mats.Ballast();
        var met = Mats.DetailMetal();
        var det = Mats.Detail();
        var acc = 0f;
        for (var i = 0; i + 1 < pts.Length; i++)
        {
            var a = new Vector3(pts[i].X, 0, pts[i].Y);
            var b = new Vector3(pts[i + 1].X, 0, pts[i + 1].Y);
            var dir = (b - a).Normalized();
            var side = new Vector3(-dir.Z, 0, dir.X);
            var len = a.DistanceTo(b);
            var basis = Basis.LookingAt(dir, Vector3.Up);
            for (var s = 0f; s < len; s += 24f)
            {
                var s1 = Mathf.Min(len, s + 24f);
                var p0 = a + dir * s;
                var p1 = a + dir * s1;
                var k = c.Dressing.At((p0 + p1) / 2);
                k.Col = new Color(0.8f, 0.78f, 0.75f);
                // Ballast : bombé (deux pans), à peine relevé.
                k.OrientedQuad(ballast, p0 - side * 1.6f + Vector3.Up * 0.02f, p1 - side * 1.6f + Vector3.Up * 0.02f, p1 + Vector3.Up * 0.14f, p0 + Vector3.Up * 0.14f, Vector3.Up);
                k.OrientedQuad(ballast, p0 + Vector3.Up * 0.14f, p1 + Vector3.Up * 0.14f, p1 + side * 1.6f + Vector3.Up * 0.02f, p0 + side * 1.6f + Vector3.Up * 0.02f, Vector3.Up);
                foreach (var r in new[] { -0.72f, 0.72f })
                {
                    k.Col = new Color(0.32f, 0.28f, 0.24f);
                    var mid = (p0 + p1) / 2 + side * r;
                    k.Box(met, mid + Vector3.Up * 0.26f, new Vector3(0.07f, 0.16f, s1 - s), basis);
                    k.Col = new Color(0.62f, 0.6f, 0.58f);
                    k.Box(met, mid + Vector3.Up * 0.345f, new Vector3(0.075f, 0.012f, s1 - s), basis);
                }
                for (var t = Mathf.Ceil((acc + s) / 0.9f) * 0.9f - acc; t < s1; t += 0.9f)
                {
                    var q = a + dir * t;
                    k.Col = new Color(0.5f, 0.49f, 0.46f) * (0.85f + Hh(q.X, q.Z, 61) * 0.25f);
                    k.Box(det, q + Vector3.Up * 0.16f, new Vector3(2.4f, 0.1f, 0.24f), basis);
                }
            }
            acc += len;
        }
    }

    /// <summary>Points d'une polyligne décalés latéralement (voies d'un tube).</summary>
    private static Vector2[] Offset(Vector2[] pts, float off)
    {
        var o = new Vector2[pts.Length];
        for (var i = 0; i < pts.Length; i++)
        {
            var d0 = i > 0 ? (pts[i] - pts[i - 1]).Normalized() : (pts[1] - pts[0]).Normalized();
            var d1 = i + 1 < pts.Length ? (pts[i + 1] - pts[i]).Normalized() : d0;
            var d = (d0 + d1).Normalized();
            var n = new Vector2(-d.Y, d.X);
            var miter = 1f / Mathf.Max(0.5f, n.Dot(new Vector2(-d0.Y, d0.X)));
            o[i] = pts[i] + n * off * miter;
        }
        return o;
    }

    // ───────────────────────── Tunnels ─────────────────────────

    private static void BuildTunnels(Ctx c)
    {
        foreach (var r in c.Regions)
        {
            if (r.Kind != Kind.Tunnel) continue;
            var pts = TunnelPoints(r.Name);
            Track(c, Offset(pts, -TrackOff));
            Track(c, Offset(pts, TrackOff));
            TunnelDressing(c, pts, r.Name.Length * 13);
        }
        // Rame déraillée en travers du tunnel nord-est, couchée contre la paroi.
        DerailedCar(c, new Vector3(120, 0, -26.5f), Mathf.Pi / 2 + 0.22f, 0.32f, 21);
        DerailedCar(c, new Vector3(-108, 0, 20.5f), -Mathf.Pi / 2 - 0.15f, -0.18f, 22);
        // Barrage de l'armée à l'entrée du tunnel sud-est (vers la caverne) : sacs de sable, fûts, fusées.
        var k = c.Solid.At(new Vector3(96, 0, 19));
        foreach (var (a, b, seed) in new[] { (new Vector2(98, 12f), new Vector2(98, 17.5f), 31), (new Vector2(98, 21f), new Vector2(98, 26.5f), 32) })
        {
            var (bc, size, yaw) = Props.SandbagWall(k, new Vector3(a.X, 0, a.Y), new Vector3(b.X, 0, b.Y), 7, seed);
            c.Body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Transform = new Transform3D(new Basis(Vector3.Up, yaw), bc) });
        }
        c.Props.Add(Props.Drum(1), new Vector3(100f, 0, 15f), 0.6f);
        c.Props.Add(Props.Barrier(0), new Vector3(102f, 0, 23f), 1.3f);
        Flare(c, new Vector3(118f, 0.02f, 21f), 0.7f);
        Flare(c, new Vector3(-134f, 0.02f, -27f), 2.7f);
    }

    /// <summary>Axes des tunnels (mêmes points que le plan).</summary>
    private static Vector2[] TunnelPoints(string name)
    {
        static Vector2 V(float x, float z) => new(x, z);
        return name switch
        {
            "tunnel-no" => new[] { V(-84, -28), V(-138, -28), V(-158, -34), V(-171, -46), V(-177, -62), V(-178, -128) },
            "tunnel-so" => new[] { V(-84, 19), V(-126, 19), V(-146, 26), V(-158, 40), V(-163, 58), V(-165, 92) },
            "tunnel-ne" => new[] { V(84, -28), V(138, -28), V(158, -34), V(171, -46), V(177, -62), V(178, -100) },
            _ => new[] { V(84, 19), V(110, 19), V(126, 27), V(136, 42), V(140, 56) },
        };
    }

    /// <summary>
    /// Habillage d'un tube : chemins de câbles sur les deux parois, lampes de secours en quinconce (une sur trois
    /// éteinte), signaux, armoires techniques, débris.
    /// </summary>
    private static void TunnelDressing(Ctx c, Vector2[] pts, int seed)
    {
        var met = Mats.DetailMetal();
        var acc = 0f;
        var lamp = 0;
        for (var i = 0; i + 1 < pts.Length; i++)
        {
            var a = new Vector3(pts[i].X, 0, pts[i].Y);
            var b = new Vector3(pts[i + 1].X, 0, pts[i + 1].Y);
            var dir = (b - a).Normalized();
            var side = new Vector3(-dir.Z, 0, dir.X);
            var len = a.DistanceTo(b);
            var basis = Basis.LookingAt(dir, Vector3.Up);
            foreach (var sgn in new[] { -1f, 1f })
            {
                // Chemin de câbles : tablette et faisceau de câbles qui pend légèrement entre les consoles.
                for (var s = 0f; s < len; s += 12f)
                {
                    var s1 = Mathf.Min(len, s + 12f);
                    var p0 = a + dir * s + side * sgn * 7.05f;
                    var p1 = a + dir * s1 + side * sgn * 7.05f;
                    var k = c.Dressing.At((p0 + p1) / 2);
                    k.Col = new Color(0.3f, 0.3f, 0.28f);
                    k.Box(met, (p0 + p1) / 2 + Vector3.Up * 3.1f, new Vector3(0.5f, 0.05f, s1 - s), basis);
                    for (var t = s; t < s1; t += 3f)
                    {
                        var q = a + dir * t + side * sgn * 7.25f;
                        k.Box(met, q + Vector3.Up * 3.0f, new Vector3(0.6f, 0.06f, 0.06f), basis);
                    }
                    k.Col = new Color(0.07f, 0.07f, 0.07f);
                    for (var w = 0; w < 3; w++)
                    {
                        var o = side * sgn * (-0.15f + w * 0.12f);
                        var sag = 0.12f + w * 0.05f;
                        for (var t = s; t < s1 - 0.01f; t += 3f)
                        {
                            var t1 = Mathf.Min(t + 3f, s1);
                            var q0 = a + dir * t + side * sgn * 7.05f + o + Vector3.Up * 3.2f;
                            var q1 = a + dir * t1 + side * sgn * 7.05f + o + Vector3.Up * 3.2f;
                            var qm = (q0 + q1) / 2 + Vector3.Down * sag;
                            k.Tube(Mats.Detail(), q0, qm, 0.035f + w * 0.01f, 4);
                            k.Tube(Mats.Detail(), qm, q1, 0.035f + w * 0.01f, 4);
                        }
                    }
                }
            }
            // Lampes de secours en quinconce, tous les ~22 m.
            for (var t = 8f; t < len - 4f; t += 22f)
            {
                var sgn = lamp % 2 == 0 ? -1f : 1f;
                var q = a + dir * t + side * sgn * 7.2f + Vector3.Up * 4.1f;
                var lit = (lamp + seed) % 3 != 0;
                WallLamp(c, q, -side * sgn, lit, 2.4f, 16f);
                // Signal lumineux (rouge) au pied de la paroi opposée, une fois sur deux.
                if (lamp % 2 == 1)
                {
                    var sp = a + dir * (t + 9f) - side * sgn * 6.6f;
                    var sk = c.Dressing.At(sp);
                    sk.Col = new Color(0.1f, 0.1f, 0.1f);
                    sk.Box(met, sp + Vector3.Up * 1.1f, new Vector3(0.12f, 2.2f, 0.12f));
                    sk.Box(met, sp + Vector3.Up * 2.4f, new Vector3(0.36f, 0.7f, 0.22f), basis);
                    var g = c.Glows.At(sp);
                    g.Box(Mats.Glow(new Color(1f, 0.1f, 0.06f), 7f), sp + Vector3.Up * 2.55f + dir * -0.12f, new Vector3(0.14f, 0.14f, 0.02f), basis);
                }
                // Armoire technique contre la paroi.
                if ((lamp + seed) % 4 == 1)
                {
                    var ap = a + dir * (t + 4f) + side * sgn * 6.9f;
                    var ak = c.Solid.At(ap);
                    ak.Col = new Color(0.28f, 0.3f, 0.28f);
                    ak.Box(met, ap + Vector3.Up * 1f, new Vector3(0.6f, 2f, 1.2f), basis);
                }
                lamp++;
            }
            for (var t = 5f; t < len; t += 17f) Debris(c, a + dir * t + side * ((Hh(t, seed, 71) - 0.5f) * 10f), (int)(t * 7) + seed);
            acc += len;
        }
    }

    // ───────────────────────── Rames ─────────────────────────

    private static readonly Dictionary<string, ArrayMesh> CarMeshes = new();

    /// <summary>
    /// Voiture de métro (15 × 2,9 m, caisse à 1,1 m du rail) : bogies, châssis, caisse blanc sale à bande bleue, baies
    /// vitrées (vitres sombres ou brisées), portes (ouvertes : intérieur noir), toit bombé ; cabine aux extrémités.
    /// <paramref name="burnt"/> : calcinée (tôles noircies, vitres soufflées).
    /// </summary>
    private static ArrayMesh CarMesh(bool burnt, bool cab, bool open)
    {
        var key = $"car{burnt}{cab}{open}";
        if (CarMeshes.TryGetValue(key, out var m)) return m;
        var k = new MeshKit();
        var body = burnt ? (Material)Mats.Charred() : Mats.Worn(new Color(0.64f, 0.65f, 0.62f), 0.55f, 0.35f);
        var band = burnt ? (Material)Mats.Charred() : Mats.Worn(new Color(0.12f, 0.24f, 0.42f), 0.5f, 0.3f);
        var met = Mats.DetailMetal();
        var dark = Mats.Painted(new Color(0.015f, 0.015f, 0.016f), 0.9f);
        var glass = Mats.Glass();
        const float L = 15f, W = 2.9f, y0 = 1.1f, y1 = 3.6f;
        k.Col = burnt ? new Color(0.16f, 0.14f, 0.13f) : Colors.White;
        // Bogies et châssis.
        k.Col = new Color(0.12f, 0.12f, 0.11f);
        foreach (var z in new[] { -5.2f, 5.2f })
        {
            k.Box(met, new Vector3(0, 0.6f, z), new Vector3(2.2f, 0.5f, 2.6f));
            foreach (var zz in new[] { -0.9f, 0.9f })
            foreach (var x in new[] { -0.72f, 0.72f })
                k.Cylinder(met, new Vector3(x, 0.45f, z + zz), 0.42f, 0.42f, 0.12f, 10, true, new Basis(Vector3.Back, Mathf.Pi / 2));
        }
        k.Box(met, new Vector3(0, 0.95f, 0), new Vector3(W - 0.3f, 0.3f, L - 0.4f));
        // Caisse : bas de caisse, bande, montants entre baies, bandeau haut.
        k.Col = burnt ? new Color(0.2f, 0.17f, 0.15f) : Colors.White;
        k.Box(body, new Vector3(0, y0 + 0.5f, 0), new Vector3(W, 1f, L));
        k.Col = burnt ? new Color(0.14f, 0.12f, 0.11f) : Colors.White;
        k.Box(band, new Vector3(0, y0 + 1.06f, 0), new Vector3(W + 0.02f, 0.14f, L));
        k.Col = burnt ? new Color(0.2f, 0.17f, 0.15f) : Colors.White;
        k.Box(body, new Vector3(0, y1 - 0.25f, 0), new Vector3(W, 0.5f, L));
        // Baies et portes : 4 portes par face (ouvertes ou non), fenêtres entre.
        var doors = new[] { -5.8f, -2f, 2f, 5.8f };
        foreach (var sx in new[] { -1f, 1f })
        {
            var x = sx * W / 2;
            for (var zi = -7.5f; zi < 7.5f; zi += 0.5f)
            {
                var zc = zi + 0.25f;
                var isDoor = false;
                foreach (var d in doors) if (Mathf.Abs(zc - d) < 0.65f) isDoor = true;
                if (isDoor) continue;
                // Montants pleins tous les 1,5 m, sinon vitre.
                var post = Mathf.PosMod(zc + 7.5f, 1.5f) < 0.5f;
                k.Col = burnt ? new Color(0.2f, 0.17f, 0.15f) : Colors.White;
                if (post) k.Box(body, new Vector3(x * 0.995f, y0 + 1.6f, zc), new Vector3(0.06f, 1.0f, 0.5f));
                else
                {
                    k.Col = Colors.White;
                    k.Box(burnt || Mathf.PosMod(zc * 3.7f, 1f) < 0.25f ? dark : glass, new Vector3(x * 0.99f, y0 + 1.6f, zc), new Vector3(0.03f, 1.0f, 0.5f));
                }
            }
            foreach (var d in doors)
            {
                k.Col = Colors.White;
                if (open) k.Box(dark, new Vector3(x * 0.98f, y0 + 1.05f, d), new Vector3(0.04f, 1.9f, 1.3f));
                else
                {
                    k.Col = burnt ? new Color(0.2f, 0.17f, 0.15f) : new Color(0.9f, 0.88f, 0.84f);
                    k.Box(body, new Vector3(x * 1.005f, y0 + 1.05f, d), new Vector3(0.05f, 1.9f, 1.3f));
                    k.Col = Colors.White;
                    k.Box(glass, new Vector3(x * 1.01f, y0 + 1.4f, d), new Vector3(0.03f, 0.8f, 1.0f));
                }
            }
        }
        // Intérieur noir (vu par les portes et les vitres) et toit bombé.
        k.Col = Colors.White;
        k.Box(dark, new Vector3(0, y0 + 1.25f, 0), new Vector3(W - 0.12f, 2.1f, L - 0.12f));
        k.Col = burnt ? new Color(0.16f, 0.14f, 0.13f) : new Color(0.75f, 0.75f, 0.72f);
        k.Frustum(body, new Vector3(0, y1, 0), new Vector2(W, L), new Vector2(W * 0.62f, L - 0.2f), 0.32f);
        // Extrémités : cabine (pare-brise sombre, feux) ou intercirculation.
        foreach (var sz in new[] { -1f, 1f })
        {
            var z = sz * L / 2;
            k.Col = burnt ? new Color(0.2f, 0.17f, 0.15f) : Colors.White;
            // Face d'extrémité pleine à hauteur des baies.
            k.Box(body, new Vector3(0, y0 + 1.6f, z - sz * 0.05f), new Vector3(W, 1.0f, 0.1f));
            if (cab && sz > 0)
            {
                k.Box(body, new Vector3(0, y0 + 0.5f, z + 0.15f), new Vector3(W, 1f, 0.3f));
                k.Col = Colors.White;
                k.Box(dark, new Vector3(0, y0 + 1.6f, z + 0.12f), new Vector3(W - 0.3f, 1.0f, 0.12f));
                k.Col = new Color(0.8f, 0.78f, 0.7f);
                foreach (var x in new[] { -0.95f, 0.95f }) k.Box(met, new Vector3(x, y0 + 0.75f, z + 0.31f), new Vector3(0.3f, 0.16f, 0.04f));
            }
            else
            {
                k.Col = new Color(0.16f, 0.16f, 0.15f);
                k.Box(met, new Vector3(0, y0 + 1.15f, z + sz * 0.12f), new Vector3(1.1f, 2.0f, 0.22f));
            }
        }
        m = k.Commit();
        CarMeshes[key] = m;
        return m;
    }

    /// <summary>
    /// Rame de <paramref name="cars"/> voitures centrée en <paramref name="center"/>, orientée selon <paramref name="yaw"/>
    /// (axe local Z). Boîte de collision par voiture : on grimpe dessus, les insectes l'escaladent.
    /// </summary>
    private static void Train(Ctx c, Vector3 center, float yaw, int cars, bool burnt, int seed)
    {
        var basis = new Basis(Vector3.Up, yaw);
        for (var i = 0; i < cars; i++)
        {
            var off = (i - (cars - 1) / 2f) * 15.4f;
            var at = center + basis * new Vector3(0, 0, off);
            var lean = burnt ? (Hh(i, seed, 81) - 0.5f) * 0.04f : 0f;
            var xf = new Transform3D(basis * new Basis(Vector3.Back, lean), at);
            c.Root.AddChild(new MeshInstance3D { Mesh = CarMesh(burnt, i == cars - 1 || i == 0, !burnt && i % 2 == 0), Transform = i == 0 ? xf * new Transform3D(new Basis(Vector3.Up, Mathf.Pi), Vector3.Zero) : xf });
            c.Body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.9f, 2.6f, 15f) }, Transform = new Transform3D(xf.Basis, at + Vector3.Up * 2.35f) });
            c.Body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.2f, 1.1f, 14f) }, Transform = new Transform3D(xf.Basis, at + Vector3.Up * 0.55f) });
            FlowObstacle(c, at, new Vector3(Mathf.Abs((basis * new Vector3(3f, 0, 15f)).X), 3.6f, Mathf.Abs((basis * new Vector3(3f, 0, 15f)).Z)));
            // Éclairage intérieur de secours encore allumé dans une voiture sur deux de la rame abandonnée.
            if (!burnt && i % 2 == 1)
            {
                var g = c.Glows.At(at);
                g.Box(Mats.Glow(new Color(0.85f, 0.95f, 0.9f), 1.6f), at + Vector3.Up * 3.35f, new Vector3(0.25f, 0.04f, 12f), basis);
                Omni(c, at + Vector3.Up * 2.8f, new Color(0.75f, 0.9f, 0.85f), 0.9f, 7f, 0f);
            }
        }
    }

    /// <summary>Voiture déraillée : couchée sur le flanc (<paramref name="roll"/>), en biais dans le tube, collision à sa forme.</summary>
    private static void DerailedCar(Ctx c, Vector3 at, float yaw, float roll, int seed)
    {
        var basis = new Basis(Vector3.Up, yaw) * new Basis(Vector3.Back, roll);
        var lift = Mathf.Abs(roll) * 1.4f;
        var xf = new Transform3D(basis, at + Vector3.Up * lift * 0.3f);
        c.Root.AddChild(new MeshInstance3D { Mesh = CarMesh(seed % 2 == 0, true, false), Transform = xf });
        c.Body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(2.9f, 3.7f, 15f) }, Transform = new Transform3D(basis, xf * new Vector3(0, 1.85f, 0)) });
        var d = new Basis(Vector3.Up, yaw) * new Vector3(0, 0, 1);
        FlowObstacle(c, at, new Vector3(Mathf.Abs(d.X) * 15f + 3f, 3.7f, Mathf.Abs(d.Z) * 15f + 3f));
        // Rails arrachés et ballast répandu autour.
        for (var i = 0; i < 6; i++) Debris(c, at + new Vector3((Hh(i, seed, 91) - 0.5f) * 14f, 0, (Hh(i, seed, 92) - 0.5f) * 8f), seed * 10 + i);
    }

    // ───────────────────────── Atelier de maintenance ─────────────────────────

    /// <summary>
    /// Atelier de maintenance (x -214 → -142, z -208 → -128) : trois voies de remisage, deux rames garées, chemin de
    /// roulement et pont roulant sous le plafond, établis, armoires, fûts ; nid au centre, ruche qui gagne les murs.
    /// </summary>
    private static void BuildDepot(Ctx c)
    {
        foreach (var x in new[] { -200f, -178f, -156f })
            Track(c, new[] { new Vector2(x, -128), new Vector2(x, -205) });
        Train(c, new Vector3(-200, 0, -170), 0f, 3, false, 31);
        Train(c, new Vector3(-156, 0, -186), 0f, 2, true, 32);
        Fire(c, new Vector3(-156f, 1.2f, -190f), 1.1f);
        // Poutres du chemin de roulement et pont roulant.
        var k = c.Solid.At(new Vector3(-178, 0, -168));
        var met = Mats.Worn(new Color(0.55f, 0.42f, 0.12f), 0.6f, 0.4f);
        k.Col = Colors.White;
        foreach (var x in new[] { -209f, -147f })
        {
            k.BoxMinMax(met, new Vector3(x - 0.4f, 11.5f, -206f), new Vector3(x + 0.4f, 12.3f, -130f));
            for (var z = -200f; z <= -136f; z += 16f) k.BoxMinMax(Mats.DetailMetal(), new Vector3(x - 0.3f, 0, z - 0.3f), new Vector3(x + 0.3f, 11.5f, z + 0.3f));
        }
        k.BoxMinMax(met, new Vector3(-209f, 12.3f, -153f), new Vector3(-147f, 13.4f, -150.6f));
        k.Col = new Color(0.2f, 0.2f, 0.2f);
        k.BoxMinMax(Mats.DetailMetal(), new Vector3(-182f, 11.2f, -153.5f), new Vector3(-177f, 12.3f, -150f));
        var dk = c.Dressing.At(new Vector3(-180, 0, -152));
        dk.Col = new Color(0.1f, 0.1f, 0.1f);
        dk.Tube(Mats.DetailMetal(), new Vector3(-179.5f, 11.2f, -151.8f), new Vector3(-179.5f, 4.2f, -151.8f), 0.03f, 4);
        dk.Box(Mats.DetailMetal(), new Vector3(-179.5f, 4f, -151.8f), new Vector3(0.5f, 0.4f, 0.3f));
        // Établis et armoires le long des murs, fûts, tourets de câble.
        for (var z = -200f; z <= -136f; z += 9f)
        {
            var bk = c.Solid.At(new Vector3(-212, 0, z));
            bk.Col = new Color(0.28f, 0.3f, 0.3f);
            bk.BoxMinMax(Mats.DetailMetal(), new Vector3(-213.2f, 0, z - 1.6f), new Vector3(-212.4f, 2f, z + 1.6f));
            bk.Col = new Color(0.35f, 0.28f, 0.2f);
            bk.BoxMinMax(Mats.Detail(), new Vector3(-145f, 0.85f, z - 1.5f), new Vector3(-143.6f, 0.95f, z + 1.5f));
            bk.Col = new Color(0.2f, 0.2f, 0.2f);
            bk.BoxMinMax(Mats.DetailMetal(), new Vector3(-145f, 0, z - 1.4f), new Vector3(-143.6f, 0.85f, z - 1.2f));
            bk.BoxMinMax(Mats.DetailMetal(), new Vector3(-145f, 0, z + 1.2f), new Vector3(-143.6f, 0.85f, z + 1.4f));
            if (Hh(z, 3, 101) > 0.5f) c.Props.Add(Props.Drum(Hh(z, 4, 101) > 0.6f ? 1 : 0), new Vector3(-148f + Hh(z, 5, 101) * 2f, 0, z + 3f), z);
        }
        WallLamp(c, new Vector3(-213.3f, 6f, -150f), Vector3.Right, true, 2.6f, 18f);
        WallLamp(c, new Vector3(-142.7f, 6f, -190f), Vector3.Left, true, 2.6f, 18f);
        WallLamp(c, new Vector3(-170f, 6f, -207.3f), Vector3.Back, false);
        WorkLight(c, new Vector3(-166f, 0, -134f), new Vector3(-182, 0, -176), false);
        for (var i = 0; i < 18; i++) Debris(c, new Vector3(-210f + Hh(i, 1, 111) * 64f, 0, -205f + Hh(i, 2, 111) * 74f), 300 + i);
    }
}
