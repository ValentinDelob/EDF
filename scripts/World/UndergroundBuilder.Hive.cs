using Godot;

namespace Rempart;

/// <summary>
/// Réseau souterrain : présence de la colonie. Éboulement du tunnel sud-ouest (butte praticable, voussoirs brisés,
/// ferraillage), fissure creusée par les insectes, chambre de ponte, grande caverne (concrétions, colonnes de résine,
/// champs d'œufs, excroissances luminescentes, toiles, brume) et invasion de la ruche autour des nids.
/// </summary>
public static partial class UndergroundBuilder
{
    private static void BuildHive(Ctx c)
    {
        BuildCollapse(c);
        BuildCavern(c, new Vector2(158, 104), 46f, 0, 1);
        BuildCavern(c, new Vector2(-170, 120), 28f, 1, 0);
        // Fissure : résine, quelques œufs, une lueur.
        foreach (var (x, z, i) in new[] { (-147f, 46f, 0), (-137f, 62f, 1), (-135f, 80f, 2), (-143f, 92f, 3) })
        {
            var p = new Vector3(x, 0, z);
            var k = c.Solid.At(p);
            Props.ResinBlob(k, p + new Vector3(3f, 0.3f, 0), new Vector3(1.6f, 1.2f, 2.4f), 40 + i);
            if (i % 2 == 0) Props.EggCluster(k, p + new Vector3(-2.5f, 0, 1.5f), 1.2f, 50 + i);
            if (i == 1) Pustules(c, p + new Vector3(3.5f, 1.5f, 0), 7, 60 + i, true);
        }
        // La ruche gagne l'atelier et la salle des pompes autour de leurs nids.
        HivePatch(c, new Vector2(-178, -176), 34f, 70);
        HivePatch(c, new Vector2(164, -150), 26f, 80);
    }

    // ───────────────────────── Éboulement ─────────────────────────

    /// <summary>
    /// Tronçon effondré du tunnel sud-ouest (z 56 → 84) : butte de gravats et de roche jusqu'à 7 m (pentes ≤ 40°,
    /// praticable), voussoirs brisés à demi enfouis, ferraillage tordu ; la voûte crevée laisse voir la roche.
    /// </summary>
    private static void BuildCollapse(Ctx c)
    {
        const float cx = -164f, cz = 70f, len = 14f, top = 7f;
        float Mound(float x, float z)
        {
            var u = (z - cz) / len;
            if (Mathf.Abs(u) >= 1f) return 0f;
            var h = top * (1f + Mathf.Cos(u * Mathf.Pi)) / 2f;
            h *= 0.85f + 0.3f * Fbm(x * 0.2f, z * 0.2f, 151);
            h += (Fbm(x * 0.8f, z * 0.8f, 152) - 0.5f) * 0.5f;
            return Mathf.Max(0f, Mathf.Min(h, CeilAt(c, x, z, out _) - 3f));
        }
        var k = c.Solid.At(new Vector3(cx, 0, cz));
        var rock = Mats.CaveRock();
        const int nx = 26, nz = 30;
        var x0 = cx - 13f;
        var z0 = cz - 15f;
        var hgt = new float[(nx + 1) * (nz + 1)];
        for (var j = 0; j <= nz; j++)
        for (var i = 0; i <= nx; i++)
            hgt[j * (nx + 1) + i] = Mound(x0 + i, z0 + j);
        Vector3 P(int i, int j) => new(x0 + i, hgt[j * (nx + 1) + i] - 0.05f, z0 + j);
        Vector3 N(int i, int j)
        {
            float H(int a, int b) => hgt[Mathf.Clamp(b, 0, nz) * (nx + 1) + Mathf.Clamp(a, 0, nx)];
            return new Vector3(H(i - 1, j) - H(i + 1, j), 2f, H(i, j - 1) - H(i, j + 1)).Normalized();
        }
        Color C(int i, int j)
        {
            var t = 0.55f + 0.5f * Fbm((x0 + i) * 0.3f, (z0 + j) * 0.3f, 153);
            return new Color(0.75f, 0.73f, 0.7f) * t;
        }
        for (var j = 0; j < nz; j++)
        for (var i = 0; i < nx; i++)
        {
            var a = j * (nx + 1) + i;
            if (hgt[a] <= 0 && hgt[a + 1] <= 0 && hgt[a + nx + 1] <= 0 && hgt[a + nx + 2] <= 0) continue;
            k.TriSmooth(rock, P(i, j), P(i + 1, j), P(i + 1, j + 1), N(i, j), N(i + 1, j), N(i + 1, j + 1), C(i, j), C(i + 1, j), C(i + 1, j + 1));
            k.TriSmooth(rock, P(i, j), P(i + 1, j + 1), P(i, j + 1), N(i, j), N(i + 1, j + 1), N(i, j + 1), C(i, j), C(i + 1, j + 1), C(i, j + 1));
        }
        // Voussoirs brisés et blocs à demi enfouis, ferraillage.
        var lining = Mats.TunnelLining();
        for (var i = 0; i < 16; i++)
        {
            var x = cx + (Hh(i, 1, 161) - 0.5f) * 13f;
            var z = cz + (Hh(i, 2, 161) - 0.5f) * 24f;
            var y = Mound(x, z);
            var rot = new Basis(Vector3.Up, Hh(i, 3, 161) * 6f) * new Basis(Vector3.Right, (Hh(i, 4, 161) - 0.5f) * 1.6f) * new Basis(Vector3.Back, (Hh(i, 5, 161) - 0.5f) * 0.9f);
            k.Col = new Color(0.8f, 0.78f, 0.74f);
            if (i % 3 == 0) k.Box(lining, new Vector3(x, y + 0.2f, z), new Vector3(3.2f, 0.45f, 1.6f), rot);
            else
            {
                var s = 0.7f + Hh(i, 6, 161) * 1.3f;
                k.Box(rock, new Vector3(x, y + s * 0.2f, z), new Vector3(s * 1.3f, s, s * 1.1f), rot);
            }
            if (i % 4 == 1)
            {
                var d = c.Dressing.At(new Vector3(x, 0, z));
                d.Col = new Color(0.25f, 0.15f, 0.1f);
                for (var r = 0; r < 4; r++)
                {
                    var a0 = new Vector3(x + r * 0.25f, y + 0.2f, z);
                    d.Tube(Mats.DetailMetal(), a0, a0 + rot * new Vector3(0.4f, 1.6f + r * 0.3f, 0.6f), 0.02f, 3);
                }
            }
        }
        // Champ de flux : la butte se franchit en grimpant (les insectes la passent, les escouades la contournent).
        for (var z = z0; z < z0 + nz; z += FlowField.Cell)
        for (var x = x0; x < x0 + nx; x += FlowField.Cell)
        {
            var h = Mound(x + 2f, z + 2f);
            if (h > 1.4f) FlowObstacle(c, new Vector3(x + 2f, 0, z + 2f), new Vector3(3.6f, h, 3.6f));
        }
        c.Root.AddChild(Drip(new Vector3(cx, 14f, cz), 6f, 12f));
        WallLamp(c, new Vector3(cx + 7.2f, 4f, 52f), Vector3.Left, true, 2f, 14f);
        Fire(c, new Vector3(cx - 3f, Mound(cx - 3f, 84.5f), 84.5f), 0.6f);
    }

    // ───────────────────────── Cavernes ─────────────────────────

    /// <summary>
    /// Habillage d'une caverne de la colonie autour de <paramref name="center"/> : concrétions (solides), stalactites,
    /// colonnes de résine qui joignent sol et voûte, champs d'œufs, excroissances luminescentes (lumières vertes),
    /// toiles et brume au ras du sol. Le centre et le nid restent dégagés (apparitions, combat de la reine).
    /// </summary>
    private static void BuildCavern(Ctx c, Vector2 center, float radius, int seed, int lights)
    {
        var rock = Mats.CaveRock();
        var count = (int)(radius * 0.9f);
        var placed = 0;
        for (var i = 0; i < count * 8 && placed < count; i++)
        {
            var a = Hh(i, seed, 171) * Mathf.Tau;
            var r = radius * Mathf.Sqrt(0.15f + 0.85f * Hh(i, seed, 172));
            var p = new Vector3(center.X + Mathf.Cos(a) * r, 0, center.Y + Mathf.Sin(a) * r * 0.9f);
            var u = Union(c, p.X, p.Z);
            if (u > -2.5f || NearNest(p, 19f) || new Vector2(p.X - center.X, p.Z - center.Y).Length() < radius * 0.28f) continue;
            var ceil = CeilAt(c, p.X, p.Z, out _);
            var kind = placed % 5;
            var k = c.Solid.At(p);
            if (kind == 0 && u < -5f)
            {
                // Concrétion : bloc de roche bosselé et effilé, parfois flanqué d'un second plus bas.
                var h = 2.5f + Hh(i, seed, 173) * Mathf.Min(7f, ceil * 0.3f);
                var rb = 1.1f + h * 0.25f;
                RockLump(c, p, rb, h, 0.75f, i + seed * 100);
                if (Hh(i, seed, 178) > 0.4f) RockLump(c, p + new Vector3(rb * 1.1f, 0, rb * 0.5f), rb * 0.6f, h * 0.45f, 0.5f, i + seed * 100 + 50);
            }
            else if (kind == 1 && ceil < 34f && u < -4f)
            {
                // Colonne organique du sol à la voûte (évasée aux deux bouts, paroi de ruche), collision cylindrique.
                var rr = 1.1f + Hh(i, seed, 174) * 0.8f;
                RockLump(c, p - Vector3.Up * 0.5f, rr, ceil + 1.5f, 0f, i * 31 + seed, Mats.HiveWall(), false, 1.1f);
                c.Body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = rr * 0.9f, Height = ceil + 1f }, Position = p + Vector3.Up * (ceil + 1f) / 2 });
                FlowObstacle(c, p, new Vector3(rr * 2f, ceil, rr * 2f));
            }
            else if (kind == 2)
            {
                Props.EggCluster(k, p, 1.3f + Hh(i, seed, 175) * 0.8f, i + seed * 100);
                Props.EggCluster(k, p + new Vector3(2.2f, 0, -1.4f), 0.9f, i + seed * 100 + 7);
            }
            else if (kind == 3)
            {
                Pustules(c, p + Vector3.Up * 0.3f, 9, i + seed * 50, placed % 2 == 0 && lights > 0);
                Props.ResinBlob(k, p + new Vector3(-1.5f, 0.2f, 0.8f), new Vector3(2.2f, 0.9f, 1.8f), i + 900);
            }
            else RockLump(c, p - Vector3.Up * 0.3f, 2.2f + Hh(i, seed, 176) * 1.6f, 1.6f + Hh(i, seed, 177) * 1.4f, 0.45f, i + 950, Mats.HiveWall());
            placed++;
        }
        // Stalactites (visuel seul) et excroissances luminescentes sous la voûte.
        for (var i = 0; i < count * 2; i++)
        {
            var a = Hh(i, seed, 181) * Mathf.Tau;
            var r = radius * Mathf.Sqrt(Hh(i, seed, 182));
            var p = new Vector3(center.X + Mathf.Cos(a) * r, 0, center.Y + Mathf.Sin(a) * r);
            if (Union(c, p.X, p.Z) > -2f) continue;
            var ceil = CeilAt(c, p.X, p.Z, out _);
            var d = c.Dressing.At(p);
            var len = 1.5f + Hh(i, seed, 183) * Mathf.Min(6f, ceil * 0.15f);
            d.Col = new Color(0.55f, 0.53f, 0.5f);
            d.Cylinder(rock, new Vector3(p.X, ceil - len, p.Z), 0.05f, 0.35f + len * 0.12f, len + 0.6f, 7);
            if (i % 6 == 0) Pustules(c, new Vector3(p.X + 1.5f, ceil - 0.4f, p.Z), 6, i + 400 + seed * 10, false);
        }
        // Toiles tendues entre concrétions et parois, côté opposé au tunnel d'accès.
        for (var i = 0; i < 3; i++)
        {
            var a = 0.8f + i * 1.7f + seed;
            var p = new Vector3(center.X + Mathf.Cos(a) * radius * 0.75f, 0, center.Y + Mathf.Sin(a) * radius * 0.7f);
            if (Union(c, p.X, p.Z) > -3f) continue;
            var y = Mathf.Min(CeilAt(c, p.X, p.Z, out _) - 2f, 9f);
            var w = c.Dressing.At(p);
            Props.Web(w, new[] { p + new Vector3(-5, y, -2), p + new Vector3(4, y + 1.5f, -3), p + new Vector3(5, 1.2f, 3), p + new Vector3(-4, 0.8f, 2) }, 190 + i);
        }
        // Grandes grappes luminescentes sous la voûte : elles dessinent le volume de la caverne.
        for (var i = 0; i < 2 + lights * 4; i++)
        {
            var a = i * 1.9f + 0.7f + seed;
            var p = new Vector3(center.X + Mathf.Cos(a) * radius * 0.45f, 0, center.Y + Mathf.Sin(a) * radius * 0.4f);
            if (Union(c, p.X, p.Z) > -6f) continue;
            var ceil = CeilAt(c, p.X, p.Z, out _);
            for (var j = 0; j < 4; j++)
                Pustules(c, new Vector3(p.X + (Hh(i, j, 221) - 0.5f) * 6f, ceil - 0.6f, p.Z + (Hh(i, j, 222) - 0.5f) * 6f), 10, 600 + i * 7 + j, false);
            Omni(c, new Vector3(p.X, ceil - 3f, p.Z), Bio, 2.2f, Mathf.Min(36f, ceil + 4f), 1.2f, 0.7f);
        }
        // Lueur diffuse de la ruche, haute et large : on devine les parois et la voûte au loin.
        for (var i = 0; i < 2 + lights * 3; i++)
        {
            var a = i * Mathf.Tau / (2 + lights * 3) + 0.3f;
            var p = new Vector3(center.X + Mathf.Cos(a) * radius * 0.55f, 0, center.Y + Mathf.Sin(a) * radius * 0.5f);
            if (Union(c, p.X, p.Z) > -4f) continue;
            var ceil = CeilAt(c, p.X, p.Z, out _);
            Omni(c, new Vector3(p.X, ceil * 0.4f, p.Z), new Color(0.5f, 0.78f, 0.6f), 2f, Mathf.Max(34f, ceil * 1.3f), 0.5f, 0.35f);
        }
        if (lights > 0)
        {
            // Fissure de la voûte jusqu'à la surface : un mince jour gris tombe au centre de la caverne.
            var top = CeilAt(c, center.X + 6f, center.Y - 4f, out _);
            var spot = new SpotLight3D
            {
                Position = new Vector3(center.X + 6f, top - 1f, center.Y - 4f), RotationDegrees = new Vector3(-90, 0, 0), LightColor = Daylight,
                LightEnergy = 14f, SpotRange = top + 6f, SpotAngle = 12f, SpotAttenuation = 0.5f, ShadowEnabled = false,
                LightVolumetricFogEnergy = 2.5f, LightBakeMode = Light3D.BakeMode.Disabled,
            };
            c.Root.AddChild(spot);
            c.Lights++;
            c.Root.AddChild(Drip(new Vector3(center.X + 6f, top - 2f, center.Y - 4f), 3f, top - 2f));
        }
        // Lumières vertes de la ruche (en plus des grappes).
        for (var i = 0; i < 3 + lights * 4; i++)
        {
            var a = i * 2.4f + seed;
            var p = new Vector3(center.X + Mathf.Cos(a) * radius * 0.6f, 0, center.Y + Mathf.Sin(a) * radius * 0.55f);
            if (Union(c, p.X, p.Z) > -3f) continue;
            Omni(c, p + Vector3.Up * 4f, Bio, 1.6f, 18f, 1.1f);
        }
    }

    /// <summary>Icosphère subdivisée deux fois (320 faces), triangles à plat.</summary>
    private static readonly Vector3[] Ico2 = BuildIco2();

    private static Vector3[] BuildIco2()
    {
        var t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] v =
        {
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
            new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
        };
        int[] f =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        var tris = new System.Collections.Generic.List<Vector3>();
        foreach (var i in f) tris.Add(v[i].Normalized());
        for (var level = 0; level < 2; level++)
        {
            var res = new System.Collections.Generic.List<Vector3>();
            for (var i = 0; i < tris.Count; i += 3)
            {
                var a = tris[i]; var b = tris[i + 1]; var c = tris[i + 2];
                var ab = (a + b).Normalized(); var bc = (b + c).Normalized(); var ca = (c + a).Normalized();
                res.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
            }
            tris = res;
        }
        return tris.ToArray();
    }

    /// <summary>
    /// Concrétion rocheuse posée au sol : sphère bosselée (bruit continu, sans trous), étirée en hauteur et effilée
    /// vers le haut (<paramref name="taper"/>), base aplatie ; collision convexe. Remplace les cônes trop réguliers.
    /// </summary>
    private static void RockLump(Ctx c, Vector3 p, float radius, float height, float taper, int seed, Material mat = null, bool solid = true, float flare = 0f)
    {
        var k = solid ? c.Solid.At(p) : c.Dressing.At(p);
        var rock = mat ?? Mats.CaveRock();
        var s = seed * 1.731f;
        var lean = new Vector3(Mathf.Sin(s) * 0.12f, 0, Mathf.Cos(s * 1.3f) * 0.12f);
        Vector3 D(Vector3 d)
        {
            var y01 = d.Y * 0.5f + 0.5f;
            var bump = 1f + 0.18f * Mathf.Sin(d.X * 5.1f + s) * Mathf.Cos(d.Z * 4.3f + s * 1.7f) + 0.1f * Mathf.Sin(d.Y * 9f + s * 0.6f)
                + 0.06f * Mathf.Sin(d.X * 13f + s) * Mathf.Sin(d.Z * 11f - s);
            var w = radius * bump * (1f - taper * y01 * y01) * (1f + flare * (2f * y01 - 1f) * (2f * y01 - 1f));
            var y = Mathf.Max(d.Y, -0.15f) * 0.5f + 0.5f;
            return p + new Vector3(d.X * w, y * height, d.Z * w) + lean * (y * height);
        }
        Color C(Vector3 d)
        {
            var g = 0.55f + 0.35f * (d.Y * 0.5f + 0.5f) + 0.1f * Mathf.Sin(d.X * 7f + d.Z * 5f + s);
            return new Color(0.7f, 0.68f, 0.64f) * g;
        }
        // Normales lissées : somme des normales des faces autour de chaque sommet de l'icosphère.
        var ico = Ico2;
        var sum = new System.Collections.Generic.Dictionary<Vector3I, Vector3>();
        static Vector3I Key(Vector3 d) => new(Mathf.RoundToInt(d.X * 1000f), Mathf.RoundToInt(d.Y * 1000f), Mathf.RoundToInt(d.Z * 1000f));
        var center = p + Vector3.Up * height * 0.5f;
        for (var i = 0; i < ico.Length; i += 3)
        {
            Vector3 a = D(ico[i]), b = D(ico[i + 1]), e = D(ico[i + 2]);
            var fn = (b - a).Cross(e - a);
            if (fn.Dot((a + b + e) / 3 - center) < 0) fn = -fn;
            for (var j = 0; j < 3; j++) sum[Key(ico[i + j])] = (sum.TryGetValue(Key(ico[i + j]), out var acc) ? acc : Vector3.Zero) + fn;
        }
        Vector3 N(Vector3 d) => sum[Key(d)].Normalized();
        for (var i = 0; i < ico.Length; i += 3)
        {
            Vector3 a = D(ico[i]), b = D(ico[i + 1]), e = D(ico[i + 2]);
            if ((b - a).Cross(e - a).LengthSquared() < 1e-8f) continue;
            k.TriSmooth(rock, a, b, e, N(ico[i]), N(ico[i + 1]), N(ico[i + 2]), C(ico[i]), C(ico[i + 1]), C(ico[i + 2]));
        }
        if (!solid) return;
        var hull = new System.Collections.Generic.List<Vector3>();
        for (var i = 0; i < 12; i++)
        {
            var a = i * Mathf.Tau / 12f;
            hull.Add(D(new Vector3(Mathf.Cos(a), -0.15f, Mathf.Sin(a))));
            hull.Add(D(new Vector3(Mathf.Cos(a) * 0.7f, 0.4f, Mathf.Sin(a) * 0.7f).Normalized()));
        }
        hull.Add(D(Vector3.Up));
        c.Ruins.Convex(hull.ToArray());
    }

    private static bool NearNest(Vector3 p, float r)
    {
        foreach (var n in NestSpots) if (new Vector2(p.X - n.X, p.Z - n.Z).Length() < r) return true;
        return false;
    }

    /// <summary>
    /// Grappe d'excroissances luminescentes (pustules translucides vert maladif sur un coussin de résine) ;
    /// <paramref name="light"/> : avec une lumière verte.
    /// </summary>
    private static void Pustules(Ctx c, Vector3 p, int n, int seed, bool light)
    {
        var g = c.Glows.At(p);
        RockLump(c, p - Vector3.Up * 0.25f, 1.3f, 1.0f, 0.35f, seed, Mats.HiveWall(), false);
        for (var i = 0; i < n; i++)
        {
            var a = Hh(i, seed, 201) * Mathf.Tau;
            var r = Hh(i, seed, 202) * 1.1f;
            var s = 0.12f + Hh(i, seed, 203) * 0.22f;
            var q = p + new Vector3(Mathf.Cos(a) * r, 0.35f + (1f - r / 1.1f) * 0.4f + s * 0.4f, Mathf.Sin(a) * r);
            g.Col = Colors.White;
            g.Cylinder(Mats.Glow(new Color(0.4f, 0.95f, 0.5f), 2.2f + Hh(i, seed, 204) * 2f), q - Vector3.Up * s * 0.5f, s * 0.8f, s * 0.35f, s * 1.3f, 7);
        }
        if (light) Omni(c, p + Vector3.Up * 1.2f, Bio, 1.2f, 12f, 0.3f);
    }

    /// <summary>La ruche autour d'un nid : résine sur les murs et au sol, grappes d'œufs, pustules, au-delà de 19 m du nid.</summary>
    private static void HivePatch(Ctx c, Vector2 nest, float radius, int seed)
    {
        for (var i = 0; i < 18; i++)
        {
            var a = Hh(i, seed, 211) * Mathf.Tau;
            var r = 19f + Hh(i, seed, 212) * (radius - 19f);
            var p = new Vector3(nest.X + Mathf.Cos(a) * r, 0, nest.Y + Mathf.Sin(a) * r);
            if (Union(c, p.X, p.Z) > -1.5f) continue;
            var k = c.Solid.At(p);
            if (i % 3 == 0) Props.EggCluster(k, p, 1.2f, seed + i);
            else Props.ResinBlob(k, p + Vector3.Up * 0.2f, new Vector3(1.8f + Hh(i, seed, 213) * 1.5f, 0.7f + Hh(i, seed, 214) * 1.5f, 1.6f), seed * 10 + i);
            if (i % 5 == 1) Pustules(c, p + new Vector3(1.2f, 0.4f, 0), 6, seed + i * 3, i % 10 == 1);
            c.Ruins.Decal(Mats.Decal.Ichor, p, 5f, a, 0.025f + i * 0.0004f);
        }
        // Excroissances qui montent sur les murs autour du nid.
        for (var i = 0; i < 24; i++)
        {
            var a = i * Mathf.Tau / 24f + seed;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            for (var r = 10f; r < radius + 14f; r += 1f)
            {
                var p = new Vector3(nest.X, 0, nest.Y) + dir * r;
                if (Union(c, p.X, p.Z) < -1.3f) continue;
                var k = c.Solid.At(p);
                var y = 1.2f + Hh(i, seed, 215) * 3.5f;
                Props.ResinBlob(k, p - dir * 0.5f + Vector3.Up * y, new Vector3(1.5f, 1.3f + Hh(i, seed, 216) * 1.6f, 1.5f), seed * 20 + i);
                Props.ResinBlob(k, p - dir * 1.1f + Vector3.Up * 0.3f, new Vector3(2.2f, 0.9f, 2f), seed * 20 + i + 500);
                if (i % 4 == 0) Props.EggCluster(k, p - dir * 2.4f, 1f, seed * 30 + i);
                break;
            }
        }
        // Poches de résine suspendues à la voûte au-dessus du nid, filaments qui pendent.
        for (var i = 0; i < 7; i++)
        {
            var a = i * 0.9f + seed;
            var p = new Vector3(nest.X + Mathf.Cos(a) * (6f + i * 1.8f), 0, nest.Y + Mathf.Sin(a) * (6f + i * 1.8f));
            if (Union(c, p.X, p.Z) > -3f) continue;
            var ceil = CeilAt(c, p.X, p.Z, out _);
            var k = c.Dressing.At(p);
            Props.ResinBlob(k, new Vector3(p.X, ceil - 0.6f, p.Z), new Vector3(2.2f, 1.4f, 2f), seed * 40 + i);
            k.Col = new Color(0.16f, 0.14f, 0.08f);
            for (var s = 0; s < 3; s++)
            {
                var off = new Vector3((Hh(i, s, 217) - 0.5f) * 2f, 0, (Hh(i, s, 218) - 0.5f) * 2f);
                k.Tube(Mats.Resin(), new Vector3(p.X, ceil - 1f, p.Z) + off, new Vector3(p.X, ceil - 3.5f - Hh(i, s, 219) * 4f, p.Z) + off * 1.3f, 0.06f, 4);
            }
        }
    }
}
