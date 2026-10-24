using Godot;

namespace Rempart;

/// <summary>
/// Bâtiments de la zone industrielle : ateliers à toiture en sheds (brique, verrières d'acier), chaufferie,
/// cheminées de brique et de béton, silos à ciment et leur tour d'escalier, entrepôts en bardage à quais de
/// chargement, halle de transbordement, piles de conteneurs. Tous destructibles (immeubles « industriels » au
/// maillage sur mesure), sauf les silos (ouvrage fixe, escaladable).
/// </summary>
public static partial class IndustrialBuilder
{
    private static readonly Color Brick = new(0.92f, 0.9f, 0.88f);
    private static readonly Color ConcreteGrey = new(0.43f, 0.42f, 0.4f);

    // ───────────────────────── Outils de maillage ─────────────────────────

    /// <summary>Pan de mur entre deux points au sol, de <paramref name="y0"/> à <paramref name="y1"/>, UV en unités de texture.</summary>
    private static void WallBand(MeshKit k, Material m, Vector3 a, Vector3 b, float y0, float y1, Vector3 n, float uLen, float vLen, Color col, float vTop = -1f)
    {
        var len = (b - a).Length();
        var top = vTop < 0 ? y1 : vTop;
        k.QuadC(m, a with { Y = y0 }, b with { Y = y0 }, b with { Y = y1 }, a with { Y = y1 },
            new Vector2(0, (top - y0) / vLen), new Vector2(len / uLen, (top - y0) / vLen), new Vector2(len / uLen, (top - y1) / vLen), new Vector2(0, (top - y1) / vLen),
            col, col, col * 0.85f, col * 0.85f, n);
    }

    /// <summary>Les quatre façades d'un volume centré à l'origine : coin de départ, direction, normale, longueur.</summary>
    private static (Vector3 start, Vector3 along, Vector3 n, float len)[] Faces(float hx, float hz) => new[]
    {
        (new Vector3(-hx, 0, hz), Vector3.Right, Vector3.Back, hx * 2),
        (new Vector3(hx, 0, -hz), Vector3.Left, Vector3.Forward, hx * 2),
        (new Vector3(hx, 0, hz), Vector3.Forward, Vector3.Right, hz * 2),
        (new Vector3(-hx, 0, -hz), Vector3.Back, Vector3.Left, hz * 2),
    };

    /// <summary>
    /// Tour d'escalier métallique (volées droites alternées sur deux files, paliers aux deux bouts) dans un repère
    /// local : volées le long de Z, files en x = ±0,65 ; arrivée en haut sur un palier prolongé de
    /// <paramref name="exit"/> m vers -Z (passerelle). Plaques inclinées solides sous le nez des marches.
    /// </summary>
    private static void StairTower(MeshKit k, Transform3D xf, float height, float exit = 0f, bool frame = true)
    {
        var keep = k.Xf;
        k.Xf = xf;
        var metal = Mats.DetailMetal();
        const float run = 4.2f, half = run / 2, land = 1.3f;
        var flights = Mathf.Max(1, Mathf.CeilToInt(height / 2.9f));
        var rise = height / flights;
        for (var f = 0; f < flights; f++)
        {
            var x = f % 2 == 0 ? -0.65f : 0.65f;
            var zA = f % 2 == 0 ? -half : half;
            var zB = -zA;
            var y0 = f * rise;
            var y1 = (f + 1) * rise;
            k.Col = new Color(0.2f, 0.2f, 0.19f);
            k.Quad(metal, new Vector3(x - 0.6f, y0, zA), new Vector3(x + 0.6f, y0, zA), new Vector3(x + 0.6f, y1, zB), new Vector3(x - 0.6f, y1, zB));
            k.Quad(metal, new Vector3(x - 0.6f, y1, zB), new Vector3(x + 0.6f, y1, zB), new Vector3(x + 0.6f, y0, zA), new Vector3(x - 0.6f, y0, zA));
            k.Solid = false;
            var steps = Mathf.CeilToInt(rise / 0.2f);
            for (var s = 0; s < steps; s++)
            {
                var u = (s + 0.5f) / steps;
                k.Box(metal, new Vector3(x, Mathf.Lerp(y0, y1, u) + 0.05f, Mathf.Lerp(zA, zB, u)), new Vector3(1.15f, 0.04f, 0.28f));
            }
            k.Col = new Color(0.42f, 0.36f, 0.16f);
            var xr = x + (f % 2 == 0 ? -0.62f : 0.62f);
            k.Tube(metal, new Vector3(xr, y0 + 1f, zA), new Vector3(xr, y1 + 1f, zB), 0.025f, 4);
            k.Tube(metal, new Vector3(xr, y0, zA), new Vector3(xr, y0 + 1f, zA), 0.025f, 4);
            k.Solid = true;
            // Palier d'arrivée (deux files), solide.
            k.Col = new Color(0.22f, 0.22f, 0.21f);
            var lz = zB + Mathf.Sign(zB) * land / 2;
            var last = f == flights - 1;
            if (last && exit > 0f)
            {
                // Plateforme sommitale : les deux paliers, la file d'en face (pas au-dessus de la dernière volée, on s'y
                // cognerait la tête), prolongée vers -Z jusqu'à l'ouvrage desservi.
                k.BoxMinMax(metal, new Vector3(-1.3f, y1 - 0.1f, -half - land - exit), new Vector3(1.3f, y1, -half));
                k.BoxMinMax(metal, new Vector3(-1.3f, y1 - 0.1f, half), new Vector3(1.3f, y1, half + land));
                var ox = x < 0 ? 0.05f : -1.3f;
                k.BoxMinMax(metal, new Vector3(ox, y1 - 0.1f, -half), new Vector3(ox + 1.25f, y1, half));
            }
            else k.Box(metal, new Vector3(0, y1 - 0.05f, lz), new Vector3(2.6f, 0.1f, land));
        }
        // Palier de départ, poteaux d'angle et lisses du cadre.
        if (frame)
        {
            k.Col = new Color(0.24f, 0.24f, 0.23f);
            foreach (var px in new[] { -1.3f, 1.3f })
            foreach (var pz in new[] { -half - land, half + land })
                k.Box(metal, new Vector3(px, height / 2, pz), new Vector3(0.18f, height, 0.18f));
            k.Solid = false;
            k.Col = new Color(0.42f, 0.36f, 0.16f);
            for (var f = 1; f <= flights; f++)
            {
                var y = f * rise + 1f;
                k.Tube(metal, new Vector3(-1.3f, y, half + land), new Vector3(1.3f, y, half + land), 0.025f, 4);
                if (!(f == flights && exit > 0f)) k.Tube(metal, new Vector3(-1.3f, y, -half - land), new Vector3(1.3f, y, -half - land), 0.025f, 4);
                foreach (var px in new[] { -1.3f, 1.3f })
                {
                    k.Tube(metal, new Vector3(px, y, half), new Vector3(px, y, half + land), 0.025f, 4);
                    k.Tube(metal, new Vector3(px, y, -half), new Vector3(px, y, -half - land), 0.025f, 4);
                }
            }
            k.Solid = true;
        }
        k.Xf = keep;
    }

    // ───────────────────────── Ateliers ─────────────────────────

    /// <summary>
    /// Atelier en brique à toiture en sheds : socle de béton, travées de verrières d'acier entre pilastres, portes
    /// roulantes en pignon, corniche, sheds vitrés tournés vers le nord, descentes d'eau pluviale, édicules.
    /// </summary>
    private static ArrayMesh FactoryHallMesh(Vector3 s, int seed, bool sawtooth, bool monitor)
    {
        var k = new MeshKit { Record = true };
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var h = s.Y;
        var brick = Mats.FactoryBrick();
        var glazing = Mats.FactoryGlazing();
        var concrete = Mats.Detail();
        var metal = Mats.DetailMetal();
        var shutter = Mats.Corrugated(new Color(0.3f, 0.31f, 0.3f), 77, 4f);
        foreach (var (start, along, n, len) in Faces(hx, hz))
        {
            // Socle de béton, mur de brique.
            WallBand(k, concrete, start, start + along * len, 0, 1.3f, n, 3f, 3f, ConcreteGrey);
            WallBand(k, brick, start, start + along * len, 1.3f, h, n, 3f, 3f, Brick * 0.9f);
            var off = n * 0.04f;
            var gable = Mathf.Abs(n.X) > 0.5f;
            var bays = Mathf.Max(1, Mathf.FloorToInt(len / 6f));
            var bw = len / bays;
            for (var i = 0; i < bays; i++)
            {
                var a = start + along * (i * bw + 0.7f) + off;
                var b = start + along * ((i + 1) * bw - 0.7f) + off;
                var door = gable && (i == bays / 2 || (bays > 6 && i == bays / 4));
                if (door)
                {
                    // Porte roulante (rideau de tôle, parfois arraché en bas).
                    var torn = RuinKit.Hash(seed, i, 3) < 0.4f;
                    WallBand(k, shutter, a, b, torn ? 1.6f : 0.05f, 6.2f, n, 3f, 3f, new Color(0.7f, 0.68f, 0.64f));
                    if (torn) WallBand(k, concrete, a, b, 0.05f, 1.6f, n, 3f, 3f, new Color(0.03f, 0.03f, 0.03f));
                    k.Col = new Color(0.18f, 0.18f, 0.17f);
                    k.Box(metal, (a + b) / 2 + Vector3.Up * 6.5f + n * 0.25f, new Vector3(Mathf.Abs(along.X) * bw + Mathf.Abs(along.Z) * 0.6f, 0.6f, Mathf.Abs(along.Z) * bw + Mathf.Abs(along.X) * 0.6f));
                    if (h > 10f) WallBand(k, glazing, a, b, 8f, h - 1.8f, n, 3f, 3f, Colors.White);
                    continue;
                }
                var top = h - 1.8f;
                var bottom = gable ? Mathf.Max(3f, h * 0.45f) : 2.6f;
                if (top - bottom > 1.5f) WallBand(k, glazing, a, b, bottom, top, n, 3f, 3f, new Color(0.9f, 0.9f, 0.88f));
                // Linteau et appui en béton.
                k.Col = ConcreteGrey;
                k.Box(concrete, (a + b) / 2 + Vector3.Up * (bottom - 0.12f) + n * 0.12f, new Vector3(Mathf.Abs(along.X) * (bw - 1.2f) + Mathf.Abs(along.Z) * 0.3f, 0.24f, Mathf.Abs(along.Z) * (bw - 1.2f) + Mathf.Abs(along.X) * 0.3f));
            }
            // Pilastres.
            k.Col = Brick * 0.75f;
            for (var i = 0; i <= bays; i++)
            {
                var p = start + along * Mathf.Clamp(i * bw, 0.5f, len - 0.5f) + n * 0.2f;
                k.Box(brick, p + Vector3.Up * h / 2, new Vector3(Mathf.Abs(along.X) * 0.9f + Mathf.Abs(along.Z) * 0.4f, h, Mathf.Abs(along.Z) * 0.9f + Mathf.Abs(along.X) * 0.4f));
            }
            // Descentes d'eau pluviale (relief non solide).
            k.Solid = false;
            k.Col = new Color(0.22f, 0.21f, 0.2f);
            for (var i = 1; i < bays; i += 3)
                k.Cylinder(metal, start + along * (i * bw + 0.9f) + n * 0.35f, 0.09f, 0.09f, h, 6, false);
            k.Solid = true;
        }
        // Corniche et toit.
        k.Col = ConcreteGrey * 0.9f;
        k.Ring(concrete, hx, hz, h - 0.5f, h + 0.5f, 0.6f, 0.3f);
        k.Col = new Color(0.3f, 0.29f, 0.28f);
        k.Quad(concrete, new Vector3(-hx, h, hz), new Vector3(hx, h, hz), new Vector3(hx, h, -hz), new Vector3(-hx, h, -hz));
        var roof = Mats.Corrugated(new Color(0.34f, 0.33f, 0.31f), 78, 2.4f);
        if (sawtooth)
        {
            // Sheds : rampants de tôle montant vers le sud, verrière verticale tournée vers le nord.
            const float pitch = 8f, rise = 3.2f;
            var teeth = Mathf.FloorToInt((s.Z - 2f) / pitch);
            var z0 = -teeth * pitch / 2;
            for (var t = 0; t < teeth; t++)
            {
                var za = z0 + t * pitch;
                var zb = za + pitch;
                var x0 = -hx + 0.8f;
                var x1 = hx - 0.8f;
                k.QuadC(roof, new Vector3(x0, h, zb), new Vector3(x1, h, zb), new Vector3(x1, h + rise, za + 0.3f), new Vector3(x0, h + rise, za + 0.3f),
                    new Vector2(0, 0), new Vector2((x1 - x0) / 3f, 0), new Vector2((x1 - x0) / 3f, pitch / 3f), new Vector2(0, pitch / 3f),
                    Colors.White, Colors.White, Colors.White, Colors.White, new Vector3(0, pitch, rise));
                k.QuadC(glazing, new Vector3(x0, h, za), new Vector3(x1, h, za), new Vector3(x1, h + rise, za + 0.3f), new Vector3(x0, h + rise, za + 0.3f),
                    new Vector2(0, rise / 3f), new Vector2((x1 - x0) / 3f, rise / 3f), new Vector2((x1 - x0) / 3f, 0), new Vector2(0, 0),
                    Colors.White, Colors.White, Colors.White, Colors.White, Vector3.Forward);
                k.Col = Brick * 0.8f;
                foreach (var x in new[] { x0, x1 })
                    k.Tri(brick, new Vector3(x, h, za), new Vector3(x, h, zb), new Vector3(x, h + rise, za + 0.3f), new Vector3(x - Mathf.Sign(x), h, za + 1f));
            }
        }
        if (monitor)
        {
            // Lanterneau (surélévation vitrée sur le faîtage) de la halle de fonderie.
            var mw = Mathf.Min(10f, s.Z * 0.3f);
            var mh = 3.4f;
            k.Col = new Color(0.9f, 0.9f, 0.88f);
            foreach (var sz in new[] { -1f, 1f })
                WallBand(k, glazing, new Vector3(-hx + 3f, 0, sz * mw / 2), new Vector3(hx - 3f, 0, sz * mw / 2), h, h + mh, new Vector3(0, 0, sz), 3f, 3f, Colors.White);
            k.Col = Colors.White;
            k.QuadC(roof, new Vector3(-hx + 2.5f, h + mh, mw / 2 + 0.6f), new Vector3(hx - 2.5f, h + mh, mw / 2 + 0.6f), new Vector3(hx - 2.5f, h + mh + 1.2f, 0), new Vector3(-hx + 2.5f, h + mh + 1.2f, 0),
                Vector2.Zero, new Vector2(s.X / 3f, 0), new Vector2(s.X / 3f, 1f), new Vector2(0, 1f), Colors.White, Colors.White, Colors.White, Colors.White, Vector3.Up);
            k.QuadC(roof, new Vector3(-hx + 2.5f, h + mh, -mw / 2 - 0.6f), new Vector3(hx - 2.5f, h + mh, -mw / 2 - 0.6f), new Vector3(hx - 2.5f, h + mh + 1.2f, 0), new Vector3(-hx + 2.5f, h + mh + 1.2f, 0),
                Vector2.Zero, new Vector2(s.X / 3f, 0), new Vector2(s.X / 3f, 1f), new Vector2(0, 1f), Colors.White, Colors.White, Colors.White, Colors.White, Vector3.Up);
            k.Col = Brick * 0.8f;
            foreach (var sx in new[] { -1f, 1f })
                k.Box(brick, new Vector3(sx * (hx - 3f), h + mh / 2, 0), new Vector3(0.4f, mh, mw));
        }
        // Édicules et extracteurs de toit (non solides au-delà de leur caisson).
        k.Col = new Color(0.27f, 0.27f, 0.26f);
        for (var i = 0; i < 3; i++)
        {
            var px = (RuinKit.Hash(seed, i, 5) - 0.5f) * (s.X - 10f);
            var pz = (RuinKit.Hash(seed, i, 6) - 0.5f) * (s.Z - 10f);
            if (sawtooth) pz = -hz + 1.6f;
            k.Box(metal, new Vector3(px, h + 0.9f, pz), new Vector3(2.2f, 1.8f, 2.2f));
            k.Solid = false;
            k.Cylinder(metal, new Vector3(px, h + 1.8f, pz), 0.5f, 0.3f, 1.4f, 10);
            k.Solid = true;
        }
        return k.Commit();
    }

    /// <summary>
    /// Entrepôt en bardage : soubassement de béton, tôle ondulée, toiture à deux pans très plats avec bandes
    /// translucides, quais de chargement (portes, joints, auvent) sur le côté <paramref name="dock"/> (+1 : +Z).
    /// </summary>
    private static ArrayMesh WarehouseMesh(Vector3 s, Color cladding, int seed, int dock, bool roofStair)
    {
        var k = new MeshKit { Record = true };
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var h = s.Y;
        var concrete = Mats.Detail();
        var metal = Mats.DetailMetal();
        var clad = Mats.Corrugated(cladding, 90 + seed % 3, 2.4f);
        var shutter = Mats.Corrugated(new Color(0.26f, 0.27f, 0.27f), 79, 4f);
        foreach (var (start, along, n, len) in Faces(hx, hz))
        {
            WallBand(k, concrete, start, start + along * len, 0, 1.6f, n, 3f, 3f, ConcreteGrey);
            WallBand(k, clad, start, start + along * len, 1.6f, h, n, 3f, 3f, Colors.White);
            if (Mathf.Abs(n.Z) > 0.5f && Mathf.Sign(n.Z) == dock)
            {
                // Quais : portes sectionnelles tous les 8 m, joints d'étanchéité, butoirs, auvent.
                for (var x = 6f; x < len - 4f; x += 8f)
                {
                    var a = start + along * (x - 1.7f) + n * 0.05f;
                    var b = start + along * (x + 1.7f) + n * 0.05f;
                    var open = RuinKit.Hash(seed, (int)x, 7) < 0.25f;
                    if (open) WallBand(k, concrete, a, b, 1.2f, 4.8f, n, 3f, 3f, new Color(0.025f, 0.025f, 0.025f));
                    else WallBand(k, shutter, a, b, 1.2f, 4.8f, n, 3f, 3f, new Color(0.75f, 0.72f, 0.66f));
                    k.Col = new Color(0.05f, 0.05f, 0.05f);
                    k.Box(metal, start + along * (x - 1.95f) + n * 0.25f + Vector3.Up * 3f, new Vector3(0.35f, 3.8f, 0.35f));
                    k.Box(metal, start + along * (x + 1.95f) + n * 0.25f + Vector3.Up * 3f, new Vector3(0.35f, 3.8f, 0.35f));
                    k.Box(metal, start + along * x + n * 0.25f + Vector3.Up * 5f, new Vector3(Mathf.Abs(along.X) * 4.2f + 0.35f * Mathf.Abs(along.Z), 0.35f, 0.35f + Mathf.Abs(along.Z) * 4f));
                }
                // Auvent sur toute la longueur.
                k.Col = new Color(0.3f, 0.3f, 0.29f);
                k.Box(metal, start + along * (len / 2) + n * 2.2f + Vector3.Up * 5.6f, new Vector3(Mathf.Abs(along.X) * (len - 2f) + 4.4f * Mathf.Abs(n.X), 0.25f, Mathf.Abs(along.Z) * (len - 2f) + 4.4f * Mathf.Abs(n.Z)));
            }
            else if (Mathf.Abs(n.X) > 0.5f)
            {
                // Pignon : porte de service et grande porte.
                var a = start + along * (len / 2 - 3f) + n * 0.05f;
                var b = start + along * (len / 2 + 3f) + n * 0.05f;
                WallBand(k, shutter, a, b, 0.05f, 5.5f, n, 3f, 3f, new Color(0.7f, 0.67f, 0.62f));
            }
        }
        // Toiture à deux pans (faîtage selon X), pignons triangulaires, bandes translucides.
        var ridge = h + hz * 0.1f;
        var roof = Mats.Corrugated(cladding * 0.85f, 91, 2.4f);
        foreach (var sz in new[] { -1f, 1f })
        {
            k.QuadC(roof, new Vector3(-hx - 0.3f, h - 0.1f, sz * (hz + 0.3f)), new Vector3(hx + 0.3f, h - 0.1f, sz * (hz + 0.3f)), new Vector3(hx + 0.3f, ridge, 0), new Vector3(-hx - 0.3f, ridge, 0),
                Vector2.Zero, new Vector2(s.X / 3f, 0), new Vector2(s.X / 3f, hz / 3f), new Vector2(0, hz / 3f), Colors.White, Colors.White, Colors.White, Colors.White, new Vector3(0, 1, sz * 0.1f));
            k.Solid = false;
            k.Col = new Color(0.55f, 0.56f, 0.52f);
            for (var x = -hx + 6f; x < hx - 4f; x += 9f)
            {
                var p0 = new Vector3(x, Mathf.Lerp(h, ridge, 0.3f) + 0.03f, sz * hz * 0.7f);
                var p1 = new Vector3(x, Mathf.Lerp(h, ridge, 0.85f) + 0.03f, sz * hz * 0.15f);
                k.Quad(metal, p0 + Vector3.Left * 1f, p0 + Vector3.Right * 1f, p1 + Vector3.Right * 1f, p1 + Vector3.Left * 1f);
                k.Quad(metal, p1 + Vector3.Left * 1f, p1 + Vector3.Right * 1f, p0 + Vector3.Right * 1f, p0 + Vector3.Left * 1f);
            }
            k.Solid = true;
        }
        k.Col = cladding;
        foreach (var sx in new[] { -1f, 1f })
            k.Tri(clad, new Vector3(sx * hx, h, -hz), new Vector3(sx * hx, h, hz), new Vector3(sx * hx, ridge, 0), Vector3.Zero);
        if (roofStair)
        {
            // Escalier extérieur jusqu'au toit, contre le pignon ouest (arrivée au coin de l'égout côté +Z).
            // Le palier sommital arrive au ras de l'égout (débord de toiture à x = -hx - 0,3).
            StairTower(k, new Transform3D(new Basis(Vector3.Up, Mathf.Pi), new Vector3(-hx - 1.6f, 0, hz - 3.6f)), h, 1.2f);
        }
        return k.Commit();
    }

    /// <summary>Cheminée d'usine : socle, fût conique en brique ou en béton à bandes de balisage délavées, couronnement.</summary>
    private static ArrayMesh ChimneyMesh(float r0, float r1, float h, bool concrete, int seed)
    {
        var k = new MeshKit { Record = true };
        var m = concrete ? Mats.Detail() : Mats.FactoryBrick();
        var metal = Mats.DetailMetal();
        var bs = r0 * 2.6f;
        k.Col = ConcreteGrey;
        k.Box(Mats.Detail(), new Vector3(0, 2f, 0), new Vector3(bs, 4f, bs));
        // Fût en tronçons (bandes de balisage rouges et blanches sur le haut d'une cheminée en béton).
        const int parts = 12;
        for (var i = 0; i < parts; i++)
        {
            var y0 = 4f + (h - 4f) * i / parts;
            var y1 = 4f + (h - 4f) * (i + 1) / parts;
            var ra = Mathf.Lerp(r0, r1, (y0 - 4f) / (h - 4f));
            var rb = Mathf.Lerp(r0, r1, (y1 - 4f) / (h - 4f));
            k.Col = concrete
                ? (i >= parts - 3 ? (i % 2 == 0 ? new Color(0.42f, 0.2f, 0.16f) : new Color(0.62f, 0.6f, 0.56f)) : new Color(0.5f, 0.49f, 0.46f))
                : Brick * (0.85f + 0.1f * RuinKit.Hash(seed, i, 9));
            k.Cylinder(m, Vector3.Up * y0, ra, rb, y1 - y0, 20, false);
        }
        // Couronnement noirci, anneaux de cerclage, échelle.
        k.Col = new Color(0.08f, 0.075f, 0.07f);
        k.Cylinder(m, Vector3.Up * (h - 1.5f), r1 + 0.2f, r1 + 0.25f, 1.5f, 20, false);
        k.Cylinder(m, Vector3.Up * h, r1 + 0.25f, r1 - 0.3f, 0.3f, 20, false);
        k.Solid = false;
        k.Col = new Color(0.2f, 0.2f, 0.19f);
        for (var y = 10f; y < h - 2f; y += 8f)
        {
            var r = Mathf.Lerp(r0, r1, (y - 4f) / (h - 4f));
            k.Cylinder(metal, Vector3.Up * y, r + 0.05f, r + 0.05f, 0.2f, 20, false);
        }
        foreach (var dz in new[] { -0.22f, 0.22f })
            k.Tube(metal, new Vector3(r0 + 0.25f, 4f, dz), new Vector3(r1 + 0.25f, h, dz), 0.03f, 4);
        k.Solid = true;
        return k.Commit();
    }

    private static Building Chimney(Ctx c, Vector3 p, float r0, float r1, float h, bool concrete, int seed)
    {
        var mesh = ChimneyMesh(r0, r1, h, concrete, seed);
        var shape = new CylinderMesh { TopRadius = r1, BottomRadius = r0, Height = h, RadialSegments = 16 }.CreateConvexShape();
        // Emprise = le fût (les plaques de dégâts se posent sur les faces de la boîte : elles restent contre la brique) ;
        // le socle, plus large, a sa forme exacte « Detail ».
        var b = AddPlant(c, p, new Vector3(r0 * 1.9f, h, r0 * 1.9f), mesh, Mats.FactoryBrick(), 1.4f);
        Take(c, p, r0 * 1.4f, r0 * 1.4f);
        foreach (var ch in b.GetChildren())
            if (ch is CollisionShape3D cs && cs.Shape is BoxShape3D)
            {
                cs.Shape = shape;
                cs.Position = new Vector3(0, h / 2, 0);
            }
        return b;
    }

    // ───────────────────────── Implantation ─────────────────────────

    private static void BuildPlants(Ctx c)
    {
        // Atelier de mécanique à sheds (nord-ouest), chaufferie et sa cheminée de brique.
        var hallA = new Vector3(-172, 0, -160);
        var sA = new Vector3(76, 13f, 52);
        AddPlant(c, hallA, sA, FactoryHallMesh(sA, 11, true, false), Mats.FactoryBrick(), 1.1f);
        var boiler = new Vector3(-150, 0, -121);
        var sB = new Vector3(26, 17f, 14);
        AddPlant(c, boiler, sB, FactoryHallMesh(sB, 12, false, false), Mats.FactoryBrick());
        Chimney(c, new Vector3(-196, 0, -121), 3.3f, 2.3f, 58f, false, 1);

        // Fonderie (halle haute à lanterneau) et cheminées jumelles en béton.
        var hallB = new Vector3(-62, 0, -168);
        var sF = new Vector3(60, 19f, 42);
        AddPlant(c, hallB, sF, FactoryHallMesh(sF, 13, false, true), Mats.FactoryBrick(), 1.2f);
        Chimney(c, new Vector3(-24, 0, -126), 2.8f, 2f, 48f, true, 2);
        Chimney(c, new Vector3(-40, 0, -126), 2.8f, 2f, 48f, true, 3);

        BuildSilos(c, new Vector3(-96, 0, -124));
    }

    /// <summary>
    /// Trois silos à ciment sur pieds (trémies coniques), galerie de liaison en tête et tour d'escalier : le toit
    /// des silos (24 m) est accessible. Ouvrage fixe, collision exacte.
    /// </summary>
    private static void BuildSilos(Ctx c, Vector3 p0)
    {
        var k = c.Solid.At(p0);
        var paint = Mats.Worn(new Color(0.5f, 0.5f, 0.48f), 0.7f, 0.2f);
        var metal = Mats.DetailMetal();
        const float r = 4.2f, y0 = 7f, top = 24f;
        for (var i = 0; i < 3; i++)
        {
            var p = p0 + new Vector3(i * 9.2f, 0, 0);
            k.Col = Colors.White;
            k.Cylinder(paint, p + Vector3.Up * y0, r, r, top - y0, 28, false);
            k.Cylinder(paint, p + Vector3.Up * top, r, 0.8f, 1.2f, 28, true);
            k.Cylinder(paint, p + Vector3.Up * 3.4f, 0.6f, r, y0 - 3.4f, 28, false);
            k.Col = new Color(0.22f, 0.22f, 0.21f);
            foreach (var (dx, dz) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                k.Box(metal, p + new Vector3(dx * r * 0.7f, y0 / 2 + 0.3f, dz * r * 0.7f), new Vector3(0.4f, y0 + 0.6f, 0.4f));
            k.Solid = false;
            k.Col = new Color(0.25f, 0.24f, 0.23f);
            foreach (var (dx, dz) in new[] { (-1f, -1f), (1f, 1f) })
                k.Tube(metal, p + new Vector3(dx * r * 0.7f, 0.5f, dz * r * 0.7f), p + new Vector3(-dx * r * 0.7f, y0, dz * r * 0.7f), 0.08f, 5);
            k.Cylinder(metal, p + Vector3.Up * 1.5f, 0.35f, 0.35f, 2f, 8);
            k.Solid = true;
            FlowBlock(c, p, new Vector3(r * 2, top, r * 2));
        }
        // Galerie en tête (passerelle couverte de tôle) sur les trois silos.
        var gx0 = p0.X - r;
        var gx1 = p0.X + 18.4f + r + 6f;
        k.Col = new Color(0.24f, 0.24f, 0.23f);
        k.BoxMinMax(metal, new Vector3(gx0, top + 1.1f, p0.Z - 1.4f), new Vector3(gx1, top + 1.3f, p0.Z + 1.4f));
        RailLine(k, new Vector3(gx0, top + 1.3f, p0.Z - 1.35f), new Vector3(gx1, top + 1.3f, p0.Z - 1.35f));
        RailLine(k, new Vector3(gx0, top + 1.3f, p0.Z + 1.35f), new Vector3(gx1 - 3f, top + 1.3f, p0.Z + 1.35f));
        // Tour d'escalier à l'est, arrivée sur la galerie.
        StairTower(k, new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2), new Vector3(gx1 - 1.3f + 4.6f, 0, p0.Z)), top + 1.3f, 0.8f);
        Take(c, new Rect2(p0.X - r - 1f, p0.Z - r - 1f, 18.4f + 2 * r + 12f, 2 * r + 2f));
    }

    /// <summary>Garde-corps droit (poteaux tous les 1,5 m, lisse et sous-lisse) non solide.</summary>
    private static void RailLine(MeshKit k, Vector3 a, Vector3 b)
    {
        var metal = Mats.DetailMetal();
        var keep = k.Solid;
        k.Solid = false;
        k.Col = new Color(0.42f, 0.36f, 0.16f);
        var len = (b - a).Length();
        var n = Mathf.Max(1, Mathf.CeilToInt(len / 1.5f));
        for (var i = 0; i <= n; i++)
        {
            var p = a.Lerp(b, i / (float)n);
            k.Tube(metal, p, p + Vector3.Up * 1.05f, 0.025f, 4);
        }
        k.Tube(metal, a + Vector3.Up * 1.05f, b + Vector3.Up * 1.05f, 0.025f, 4);
        k.Tube(metal, a + Vector3.Up * 0.55f, b + Vector3.Up * 0.55f, 0.02f, 4);
        k.Solid = keep;
    }

    private static void BuildWarehouses(Ctx c)
    {
        // Deux entrepôts à l'ouest, quais tournés vers la cour qui les sépare ; escalier vers le toit du second.
        var s1 = new Vector3(64, 12f, 34);
        AddPlant(c, new Vector3(-176, 0, -56), s1, WarehouseMesh(s1, new Color(0.46f, 0.47f, 0.46f), 1, 1, false), Mats.Detail());
        var s2 = new Vector3(64, 11f, 32);
        AddPlant(c, new Vector3(-176, 0, 12), s2, WarehouseMesh(s2, new Color(0.38f, 0.4f, 0.42f), 2, -1, true), Mats.Detail());
        // Halle de transbordement de la gare (quais côté voies, au nord).
        var s3 = new Vector3(84, 10f, 20);
        AddPlant(c, new Vector3(152, 0, 42), s3, WarehouseMesh(s3, new Color(0.44f, 0.36f, 0.3f), 3, -1, false), Mats.Detail());
        // Hangar du quai de vrac.
        var s4 = new Vector3(36, 9f, 22);
        AddPlant(c, new Vector3(196, 0, 90), s4, WarehouseMesh(s4, new Color(0.5f, 0.5f, 0.47f), 4, 1, false), Mats.Detail());
        // Station de pompage (brique) entre les bassins.
        var s5 = new Vector3(22, 8f, 14);
        AddPlant(c, new Vector3(-40, 0, 116), s5, FactoryHallMesh(s5, 14, false, false), Mats.FactoryBrick());
    }

    private static readonly Color[] ContainerColors =
    {
        new(0.42f, 0.14f, 0.1f), new(0.16f, 0.24f, 0.36f), new(0.2f, 0.3f, 0.22f), new(0.5f, 0.32f, 0.12f),
        new(0.45f, 0.46f, 0.46f), new(0.6f, 0.6f, 0.57f), new(0.3f, 0.17f, 0.24f), new(0.12f, 0.12f, 0.12f),
    };

    /// <summary>
    /// Pile de conteneurs (immeuble destructible, s'effondre comme au port) : chaque niveau a sa couleur, tôle
    /// nervurée, cadre et pièces de coin sombres, portes à barres de verrouillage côté +X, toit cabossé.
    /// </summary>
    private static void ContainerStack(Ctx c, Vector3 p, int levels, int color)
    {
        var size = new Vector3(12.2f, 2.6f * levels, 2.44f);
        var b = AddPlant(c, p, size, ContainerStackMesh(levels, color), Mats.Detail());
        b.MaxHealth = b.Health = 220 + 120 * levels;
    }

    private static ArrayMesh ContainerStackMesh(int levels, int seed)
    {
        var k = new MeshKit { Record = true };
        var wall = Mats.Corrugated(new Color(0.85f, 0.85f, 0.85f), 101, 4f);
        var metal = Mats.DetailMetal();
        const float l = 12.2f, w = 2.44f, h = 2.6f;
        for (var lv = 0; lv < levels; lv++)
        {
            var ci = (seed + lv * 3 + (int)(RuinKit.Hash(seed, lv, 57) * 5)) % ContainerColors.Length;
            var col = ContainerColors[ci];
            col = col.Lerp(new Color(col.Luminance, col.Luminance, col.Luminance), 0.35f) * 1.25f;
            col.A = 1f;
            var y0 = lv * h;
            foreach (var (start, along, n, len) in Faces(l / 2, w / 2))
            {
                var door = n.X > 0.5f;
                WallBand(k, wall, start + Vector3.Up * y0, start + along * len + Vector3.Up * y0, y0 + 0.12f, y0 + h - 0.12f, n, 3f, 3f, door ? col * 0.85f : col);
                if (!door) continue;
                // Portes : joint central, quatre barres de verrouillage (relief non solide).
                k.Solid = false;
                k.Col = new Color(0.12f, 0.11f, 0.1f);
                k.Box(metal, new Vector3(l / 2 + 0.03f, y0 + h / 2, 0), new Vector3(0.04f, h - 0.3f, 0.05f));
                k.Col = new Color(0.3f, 0.29f, 0.27f);
                foreach (var z in new[] { -0.95f, -0.35f, 0.35f, 0.95f })
                    k.Tube(metal, new Vector3(l / 2 + 0.07f, y0 + 0.2f, z), new Vector3(l / 2 + 0.07f, y0 + h - 0.2f, z), 0.025f, 4);
                k.Solid = true;
            }
            // Cadre : longerons haut et bas, montants d'angle (plus sombres), toit.
            k.Col = col * 0.55f;
            foreach (var y in new[] { y0 + 0.06f, y0 + h - 0.06f })
            {
                k.Box(metal, new Vector3(0, y, w / 2 - 0.06f), new Vector3(l, 0.14f, 0.14f));
                k.Box(metal, new Vector3(0, y, -w / 2 + 0.06f), new Vector3(l, 0.14f, 0.14f));
                k.Box(metal, new Vector3(l / 2 - 0.06f, y, 0), new Vector3(0.14f, 0.14f, w));
                k.Box(metal, new Vector3(-l / 2 + 0.06f, y, 0), new Vector3(0.14f, 0.14f, w));
            }
            k.Col = new Color(0.1f, 0.095f, 0.09f);
            foreach (var x in new[] { -l / 2 + 0.08f, l / 2 - 0.08f })
                foreach (var z in new[] { -w / 2 + 0.08f, w / 2 - 0.08f })
                    k.Box(metal, new Vector3(x, y0 + h / 2, z), new Vector3(0.17f, h, 0.17f));
            k.Col = col * 0.8f;
            k.Quad(wall, new Vector3(-l / 2, y0 + h - 0.02f, w / 2), new Vector3(l / 2, y0 + h - 0.02f, w / 2), new Vector3(l / 2, y0 + h - 0.02f, -w / 2), new Vector3(-l / 2, y0 + h - 0.02f, -w / 2),
                new Vector2(0, 0), new Vector2(l / 3f, 0), new Vector2(l / 3f, w / 3f), new Vector2(0, w / 3f));
        }
        return k.Commit();
    }

    private static void BuildContainers(Ctx c)
    {
        var rng = new RandomNumberGenerator { Seed = 4401 };
        // Parc à conteneurs entre la route ouest et l'aire de départ : îlots de trois rangées, allées de 8 m.
        foreach (var x in new[] { -98f, -84f, -66f, -52f })
        for (var z = -84f; z < 30f; z += 17f)
        {
            if (rng.Randf() < 0.22f) continue;
            for (var row = 0; row < 3; row++)
            {
                if (rng.Randf() < 0.12f) continue;
                ContainerStack(c, new Vector3(x, 0, z + row * 2.6f), rng.RandiRange(1, 4), rng.RandiRange(0, 7));
            }
        }
        // Gare : stock sous le portique (orientés le long des voies).
        foreach (var x in new[] { 112f, 126f, 140f })
        for (var row = 0; row < 3; row++)
            if (rng.Randf() > 0.2f) ContainerStack(c, new Vector3(x, 0, -40f + row * 2.6f), rng.RandiRange(1, 3), rng.RandiRange(0, 7));
    }
}
