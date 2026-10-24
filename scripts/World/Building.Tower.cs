using Godot;

namespace Rempart;

/// <summary>
/// Quartier d'affaires : gratte-ciel (mur-rideau, noyau béton, bandeaux techniques, couronnement, hélisurface),
/// parking à étages ouvert, escalier de secours extérieur (accès au toit), et dégâts des tours « debout ».
/// Spécification §9.4 : une tour de 20 étages et plus ne s'effondre jamais (lisibilité, performances) ; chaque
/// impact y arrache des panneaux de façade (vitres, montants), parfois un incendie d'étage.
/// </summary>
public partial class Building
{
    /// <summary>Structure indestructible (tour ≥ 20 étages) : les dégâts arrachent des panneaux de façade.</summary>
    public bool Standing;

    private float _panelDmg;
    /// <summary>Dégâts cumulés qui arrachent un groupe de panneaux (≈ deux roquettes).</summary>
    private const float PanelStep = 280f;
    /// <summary>Nombre maximal d'arrachements par tour (nœuds et effets bornés).</summary>
    private const int MaxPanelStages = 36;

    /// <summary>Trame de façade des murs-rideaux : 5 montants par module de 7 m.</summary>
    private const float Bay = 1.4f;

    /// <summary>
    /// Hôte : cumule les dégâts d'une tour debout et renvoie le palier à diffuser (un arrachement de panneaux),
    /// ou 0. Un gros impact n'arrache qu'un groupe à la fois.
    /// </summary>
    public int HostStandingDamage(float dmg)
    {
        if (HostStage >= MaxPanelStages) return 0;
        _panelDmg += dmg;
        if (_panelDmg < PanelStep) return 0;
        _panelDmg = 0f;
        return ++HostStage;
    }

    // ───────────────────────── Gratte-ciel ─────────────────────────

    private static ArrayMesh BuildSkyscraperMesh(Vector3 s, Material facade, BuildingStyle style, RandomNumberGenerator rng, Color tint)
    {
        var kit = new MeshKit { Record = true };
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var h = s.Y;
        var detail = Mats.Detail();
        var metal = Mats.DetailMetal();
        var sides = Sides(hx, hz);
        var stone = new Color(0.46f, 0.45f, 0.43f) * tint;
        var frame = new Color(0.25f, 0.26f, 0.27f);

        var baseY = style.Lobby ? ShopH : 0f;
        BeveledWalls(kit, facade, s, baseY, Bevel, 7f, 3.2f, tint);
        kit.Col = new Color(0.27f, 0.27f, 0.27f);
        BeveledRoof(kit, detail, s, Bevel);

        if (style.Lobby)
        {
            // Hall : vitrage sombre et piliers (sans enseigne), bandeau de transfert en béton au-dessus.
            var lobby = style;
            lobby.Kind = BuildingKind.Tower;
            BuildShops(kit, sides, lobby, rng, stone, detail);
            kit.Col = stone.Darkened(0.1f);
            kit.Ring(detail, hx, hz, ShopH + 0.26f, ShopH + 1.1f, 0.5f, 0.12f, Bevel);
            // Auvent d'entrée sur la façade principale, s'il y a la place devant.
            if (style.Clearance > 3.6f)
            {
                var sd = sides[Mathf.Clamp(style.Facing, 0, 3)];
                var mid = sd.Start + sd.Along * (sd.Length / 2);
                var depth = Mathf.Min(3f, style.Clearance - 0.8f);
                var w = Mathf.Min(sd.Length * 0.4f, 12f);
                kit.Col = frame;
                kit.Box(metal, mid + sd.Normal * (depth / 2) + Vector3.Up * 4.3f, Abs(sd.Along) * w + Abs(sd.Normal) * depth + Vector3.Up * 0.35f);
                foreach (var e in new[] { -0.45f, 0.45f })
                    kit.Box(metal, mid + sd.Along * (w * e) + sd.Normal * (depth - 0.2f) + Vector3.Up * 2.07f, new Vector3(0.22f, 4.14f, 0.22f));
            }
        }
        else
        {
            // Volume en retrait : bordure basse au pied (relevé d'étanchéité sur le toit inférieur).
            kit.Col = frame;
            kit.Ring(metal, hx, hz, -0.05f, 0.45f, 0.3f, 0.06f, Bevel);
        }

        // Profilés d'angle sombres (le pan coupé du mur-rideau n'affiche qu'un montant étiré).
        kit.Solid = false;
        kit.Col = new Color(0.16f, 0.165f, 0.17f);
        foreach (var sx in new[] { -1f, 1f })
        foreach (var sz in new[] { -1f, 1f })
            kit.Box(metal, new Vector3(sx * (hx - 0.16f), (baseY + h) / 2, sz * (hz - 0.16f)), new Vector3(0.46f, h - baseY, 0.46f));
        kit.Solid = true;

        // Étages techniques : bandeaux de grilles sombres alignés sur la trame d'étages.
        var every = rng.RandiRange(10, 14) * 3.2f;
        kit.Col = new Color(0.13f, 0.135f, 0.14f);
        for (var y = h - every; y > baseY + 9f; y -= every)
            kit.Ring(metal, hx, hz, y - 1.7f, y + 0.1f, 0.3f, 0.1f, Bevel);

        if (style.Fins)
        {
            // Montants verticaux saillants (léger relief, non solides) : la lumière rasante accroche la façade.
            kit.Solid = false;
            kit.Col = new Color(0.3f, 0.31f, 0.32f);
            foreach (var sd in sides)
                for (var a = Bay; a < sd.Length - 0.8f; a += Bay * 2)
                    kit.Box(metal, sd.Start + sd.Along * a + sd.Normal * 0.17f + Vector3.Up * ((baseY + h) / 2), Abs(sd.Along) * 0.1f + Abs(sd.Normal) * 0.34f + Vector3.Up * (h - baseY));
            kit.Solid = true;
        }
        else
        {
            // Piles d'angle en béton (structure apparente), sur toute la hauteur.
            kit.Col = stone;
            foreach (var sd in sides)
            {
                foreach (var a in new[] { 0.95f, sd.Length - 0.95f })
                    kit.Box(detail, sd.Start + sd.Along * a + sd.Normal * 0.15f + Vector3.Up * ((baseY + h + 0.4f) / 2), Abs(sd.Along) * 1.3f + Abs(sd.Normal) * 0.5f + Vector3.Up * (h + 0.4f - baseY));
            }
        }

        // Noyau béton apparent sur un côté : il dépasse du toit (machinerie des ascenseurs).
        var coreTop = h;
        if (style.Core is >= 1 and <= 4)
        {
            var sd = sides[style.Core - 1];
            var w = Mathf.Min(sd.Length * 0.34f, 10.5f);
            var mid = sd.Start + sd.Along * (sd.Length / 2);
            coreTop = h + rng.RandfRange(4.5f, 7f);
            kit.Col = new Color(0.5f, 0.49f, 0.46f) * tint;
            kit.Box(detail, mid + sd.Normal * 0.5f + Vector3.Up * (coreTop / 2), Abs(sd.Along) * w + Abs(sd.Normal) * 1.2f + Vector3.Up * coreTop);
            // Joints de banches tous les deux étages (relief non solide).
            kit.Solid = false;
            kit.Col = new Color(0.3f, 0.29f, 0.28f);
            for (var y = 6.4f; y < coreTop - 0.5f; y += 6.4f)
                kit.Box(detail, mid + sd.Normal * 1.11f + Vector3.Up * y, Abs(sd.Along) * (w - 0.1f) + Abs(sd.Normal) * 0.02f + Vector3.Up * 0.06f);
            kit.Solid = true;
        }

        // Acrotère haut (garde-corps du toit accessible).
        kit.Col = frame;
        kit.Ring(metal, hx, hz, h - 0.3f, h + 1.25f, 0.3f, 0.06f, Bevel);
        kit.Col = frame.Darkened(0.3f);
        kit.Ring(metal, hx, hz, h + 1.25f, h + 1.35f, 0.4f, 0.1f, Bevel);

        if (style.Above != Vector2.Zero) RoofUnder(kit, s, style.Above, rng);
        else if (style.Helipad) Helipad(kit, s, style.Night);
        else RoofCrown(kit, s, style, rng, coreTop);

        // Feux d'obstacle rouges aux angles des tours hautes.
        if (h > 60f && style.Above == Vector2.Zero)
        {
            kit.Solid = false;
            kit.Col = new Color(0.9f, 0.08f, 0.04f);
            foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                kit.Box(Mats.Lamps(), new Vector3(sx * (hx - 0.3f), h + 1.5f, sz * (hz - 0.3f)), new Vector3(0.22f, 0.22f, 0.22f));
            kit.Solid = true;
        }
        return kit.Commit();
    }

    /// <summary>Toit d'un volume surmonté d'un retrait : quelques groupes techniques le long des bords.</summary>
    private static void RoofUnder(MeshKit kit, Vector3 s, Vector2 above, RandomNumberGenerator rng)
    {
        var metal = Mats.DetailMetal();
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var n = rng.RandiRange(2, 4);
        for (var i = 0; i < n; i++)
        {
            var size = new Vector3(rng.RandfRange(1.4f, 2.6f), rng.RandfRange(0.9f, 1.4f), rng.RandfRange(1.2f, 1.8f));
            // Bande libre entre le retrait et l'acrotère.
            var gx = hx - above.X / 2;
            var gz = hz - above.Y / 2;
            Vector3 p;
            if (gx > gz) p = new Vector3((rng.Randf() < 0.5f ? -1 : 1) * (hx - gx / 2), s.Y, rng.RandfRange(-hz + 2f, hz - 2f));
            else p = new Vector3(rng.RandfRange(-hx + 2f, hx - 2f), s.Y, (rng.Randf() < 0.5f ? -1 : 1) * (hz - gz / 2));
            if (Mathf.Min(gx, gz) < size.X + 1.2f && Mathf.Max(gx, gz) < size.X + 1.2f) continue;
            kit.Col = new Color(0.45f, 0.46f, 0.45f);
            kit.Box(metal, p + Vector3.Up * (size.Y / 2), size);
        }
    }

    /// <summary>Couronnement : édicule technique en béton à grilles, groupes froids, mât d'antenne sur les plus hautes.</summary>
    private static void RoofCrown(MeshKit kit, Vector3 s, BuildingStyle style, RandomNumberGenerator rng, float coreTop)
    {
        var detail = Mats.Detail();
        var metal = Mats.DetailMetal();
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var h = s.Y;
        var pw = s.X * rng.RandfRange(0.36f, 0.52f);
        var pd = s.Z * rng.RandfRange(0.36f, 0.52f);
        var ph = rng.RandfRange(4.2f, 6.5f);
        var pc = new Vector3(rng.RandfRange(-1, 1) * (hx - pw / 2 - 3f) * 0.4f, h, rng.RandfRange(-1, 1) * (hz - pd / 2 - 3f) * 0.4f);
        kit.Col = new Color(0.44f, 0.435f, 0.42f);
        kit.Box(detail, pc + Vector3.Up * (ph / 2), new Vector3(pw, ph, pd));
        // Grilles de ventilation sombres en haut de l'édicule.
        kit.Col = new Color(0.12f, 0.125f, 0.13f);
        kit.Box(metal, pc + Vector3.Up * (ph - 1f), new Vector3(pw + 0.08f, 1.4f, pd + 0.08f));
        kit.Col = new Color(0.32f, 0.32f, 0.31f);
        kit.Box(detail, pc + Vector3.Up * (ph + 0.08f), new Vector3(pw + 0.3f, 0.16f, pd + 0.3f));
        // Groupes froids autour.
        var nac = rng.RandiRange(2, 5);
        for (var i = 0; i < nac; i++)
        {
            var size = new Vector3(rng.RandfRange(1.6f, 3f), rng.RandfRange(1f, 1.6f), rng.RandfRange(1.4f, 2.2f));
            var p = new Vector3(rng.RandfRange(-hx + size.X / 2 + 1f, hx - size.X / 2 - 1f), h, rng.RandfRange(-hz + size.Z / 2 + 1f, hz - size.Z / 2 - 1f));
            if (Mathf.Abs(p.X - pc.X) < (pw + size.X) / 2 + 0.6f && Mathf.Abs(p.Z - pc.Z) < (pd + size.Z) / 2 + 0.6f) continue;
            kit.Col = new Color(0.48f, 0.49f, 0.48f);
            kit.Box(metal, p + Vector3.Up * (size.Y / 2), size);
            kit.Col = new Color(0.08f, 0.08f, 0.09f);
            kit.Cylinder(detail, p + Vector3.Up * size.Y, size.Z * 0.34f, size.Z * 0.34f, 0.03f, 10);
        }
        // Mât de télécommunication sur les plus hautes, sur l'édicule.
        if (h > 90f)
        {
            var mh = rng.RandfRange(10f, 22f);
            var mp = pc + Vector3.Up * ph;
            kit.Col = new Color(0.55f, 0.55f, 0.57f);
            kit.Cylinder(metal, mp, 0.35f, 0.1f, mh, 6);
            for (var y = 2.5f; y < mh - 2f; y += 3.5f)
                kit.Tube(metal, mp + new Vector3(-0.9f, y, 0), mp + new Vector3(0.9f, y, 0), 0.04f, 4);
            kit.Col = new Color(0.95f, 0.1f, 0.05f);
            kit.Solid = false;
            kit.Box(Mats.Lamps(), mp + Vector3.Up * (mh + 0.12f), new Vector3(0.3f, 0.24f, 0.3f));
            kit.Solid = true;
        }
    }

    /// <summary>Hélisurface : aire peinte (cercle jaune, H blanc), balises et manche à air.</summary>
    private static void Helipad(MeshKit kit, Vector3 s, bool night)
    {
        var metal = Mats.DetailMetal();
        var paint = Mats.RoadPaint();
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var y = s.Y + 0.015f;
        var r = Mathf.Min(hx, hz) - 2.2f;
        kit.Solid = false;
        // Aire sombre (résine), cercle de toucher, H.
        kit.Col = new Color(0.16f, 0.17f, 0.17f);
        kit.Cylinder(Mats.Detail(), new Vector3(0, s.Y - 0.01f, 0), r, r, 0.03f, 32);
        kit.Col = new Color(0.62f, 0.5f, 0.16f);
        const int seg = 40;
        for (var i = 0; i < seg; i++)
        {
            var a0 = i * Mathf.Tau / seg;
            var a1 = (i + 1) * Mathf.Tau / seg;
            var ri = r * 0.62f;
            var ro = ri + 0.5f;
            Vector3 P(float a, float rad) => new(Mathf.Cos(a) * rad, y + 0.01f, Mathf.Sin(a) * rad);
            kit.Quad(Mats.Detail(), P(a0, ri), P(a1, ri), P(a1, ro), P(a0, ro));
            // Bord : liseré blanc.
            kit.Col = new Color(0.7f, 0.7f, 0.68f);
            kit.Quad(Mats.Detail(), P(a0, r - 0.35f), P(a1, r - 0.35f), P(a1, r - 0.05f), P(a0, r - 0.05f));
            kit.Col = new Color(0.62f, 0.5f, 0.16f);
        }
        kit.Col = Colors.White;
        var hs = r * 0.42f;
        void Bar(float x0, float z0, float x1, float z1) =>
            kit.Quad(paint, new Vector3(x0, y + 0.012f, z1), new Vector3(x1, y + 0.012f, z1), new Vector3(x1, y + 0.012f, z0), new Vector3(x0, y + 0.012f, z0));
        Bar(-hs * 0.55f, -hs, -hs * 0.3f, hs);
        Bar(hs * 0.3f, -hs, hs * 0.55f, hs);
        Bar(-hs * 0.3f, -0.25f, hs * 0.3f, 0.25f);
        // Balises de périmètre (vertes la nuit).
        for (var i = 0; i < 12; i++)
        {
            var a = i * Mathf.Tau / 12;
            kit.Col = night ? new Color(0.3f, 0.95f, 0.4f) : new Color(0.25f, 0.3f, 0.25f);
            kit.Box(night ? Mats.Lamps() : Mats.Detail(), new Vector3(Mathf.Cos(a) * (r + 0.4f), s.Y + 0.08f, Mathf.Sin(a) * (r + 0.4f)), new Vector3(0.2f, 0.16f, 0.2f));
        }
        kit.Solid = true;
        // Manche à air dans un angle.
        var wp = new Vector3(hx - 1.2f, s.Y, hz - 1.2f);
        kit.Col = new Color(0.3f, 0.3f, 0.31f);
        kit.Tube(metal, wp, wp + Vector3.Up * 4f, 0.05f, 5);
        kit.Solid = false;
        kit.Col = new Color(0.6f, 0.3f, 0.12f);
        kit.Cylinder(Mats.Detail(), wp + new Vector3(0.1f, 3.8f, 0), 0.25f, 0.12f, 1.4f, 8, false, new Basis(Vector3.Back, -1.35f));
        kit.Solid = true;
    }

    // ───────────────────────── Parking à étages ─────────────────────────

    /// <summary>Hauteur d'un niveau de parking (dalle à dalle).</summary>
    public const float ParkingLevel = 3.1f;

    /// <summary>
    /// Parking ouvert : dalles, poteaux, allèges en béton, rampes droites alternées de part et d'autre (on monte
    /// à pied ou en véhicule jusqu'au toit-terrasse), marquage des places. Tout est solide (collision exacte).
    /// Repère : rampes le long de X, sol du rez-de-chaussée à y = 0.
    /// </summary>
    private static ArrayMesh BuildParkingMesh(Vector3 s, BuildingStyle style, Color tint)
    {
        var kit = new MeshKit { Record = true };
        var d = Mats.Detail();
        var paint = Mats.RoadPaint();
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var n = Mathf.Max(1, style.Levels);
        const float lh = ParkingLevel;
        const float slab = 0.32f;
        var concrete = new Color(0.55f, 0.54f, 0.51f) * tint;
        var dark = new Color(0.36f, 0.355f, 0.34f) * tint;
        var rr = Mathf.Min(13f, hx - 7f);          // demi-longueur des rampes
        const float rw0 = 0.6f, rw1 = 6.4f;        // bande de rampe (distance au bord)
        // Bande A au sud (z négatif), B au nord : la rampe k part du niveau k.
        (float z0, float z1) Strip(int k) => k % 2 == 0 ? (-hz + rw0, -hz + rw1) : (hz - rw1, hz - rw0);

        // Dalles des niveaux 1..n, percées au-dessus de la rampe qui y arrive.
        for (var j = 1; j <= n; j++)
        {
            var y = j * lh;
            var (hz0, hz1) = Strip(j - 1);
            kit.Col = concrete;
            // Quatre morceaux autour de la trémie (x ∈ [-rr, rr], z ∈ [hz0, hz1]).
            kit.BoxMinMax(d, new Vector3(-hx, y - slab, -hz), new Vector3(hx, y, hz0));
            kit.BoxMinMax(d, new Vector3(-hx, y - slab, hz1), new Vector3(hx, y, hz));
            kit.BoxMinMax(d, new Vector3(-hx, y - slab, hz0), new Vector3(-rr, y, hz1));
            kit.BoxMinMax(d, new Vector3(rr, y - slab, hz0), new Vector3(hx, y, hz1));
            // Allège périphérique (garde-corps en béton) et nez de dalle sombre.
            var top = j == n ? 1.15f : 1.0f;
            kit.Col = dark;
            kit.Ring(d, hx, hz, y - slab - 0.35f, y, 0.25f, 0.02f);
            kit.Col = concrete;
            kit.Ring(d, hx, hz, y, y + top, 0.2f, 0f);
            // Muret le long de la trémie (côté intérieur et bout bas), ouvert là où la rampe débouche.
            var inner = hz0 < 0 ? hz1 : hz0;
            var sgn = hz0 < 0 ? 1f : -1f;
            var lowEnd = (j - 1) % 2 == 0 ? -rr : rr;   // la rampe arrive par l'autre bout
            kit.BoxMinMax(d, new Vector3(-rr, y, Mathf.Min(inner, inner + sgn * 0.2f)), new Vector3(rr, y + 1f, Mathf.Max(inner, inner + sgn * 0.2f)));
            kit.BoxMinMax(d, new Vector3(lowEnd - 0.1f, y, hz0), new Vector3(lowEnd + 0.1f, y + 1f, hz1));
            // Marquage des places (hors bande de rampe).
            kit.Col = new Color(0.82f, 0.8f, 0.74f);
            kit.Solid = false;
            var (oz0, oz1) = Strip(j);
            foreach (var row in new[] { (-hz + rw1 + 0.6f, -hz + rw1 + 5.4f), (hz - rw1 - 5.4f, hz - rw1 - 0.6f) })
            {
                if (row.Item2 - row.Item1 < 3f) continue;
                for (var x = -hx + 3f; x <= hx - 3f; x += 2.5f)
                    kit.Quad(paint, new Vector3(x - 0.06f, y + 0.012f, row.Item2), new Vector3(x + 0.06f, y + 0.012f, row.Item2), new Vector3(x + 0.06f, y + 0.012f, row.Item1), new Vector3(x - 0.06f, y + 0.012f, row.Item1));
            }
            // Flèches de sens sur la dalle, devant la rampe montante.
            if (j < n)
            {
                var ax = j % 2 == 0 ? -rr - 2f : rr + 2f;
                var az = (oz0 + oz1) / 2;
                kit.Quad(paint, new Vector3(ax - 1.2f, y + 0.012f, az + 0.12f), new Vector3(ax + 1.2f, y + 0.012f, az + 0.12f), new Vector3(ax + 1.2f, y + 0.012f, az - 0.12f), new Vector3(ax - 1.2f, y + 0.012f, az - 0.12f));
            }
            kit.Solid = true;
        }

        // Rampes : rampe k du niveau k au niveau k+1, sens alterné.
        for (var k = 0; k < n; k++)
        {
            var (z0, z1) = Strip(k);
            var up = k % 2 == 0 ? 1f : -1f;          // montée vers +X (paires) ou -X (impaires)
            var ya = k * lh;
            var yb = (k + 1) * lh;
            var len = rr * 2;
            var ang = Mathf.Atan2(yb - ya, len);
            var basis = new Basis(Vector3.Back, up * ang);
            var c = new Vector3(0, (ya + yb) / 2 - slab / 2, (z0 + z1) / 2);
            kit.Col = concrete.Darkened(0.06f);
            kit.Box(d, c, new Vector3(len / Mathf.Cos(ang) + 0.3f, slab, z1 - z0), basis);
            // Muret rampant côté intérieur.
            var innerZ = z0 < 0 ? z1 + 0.1f : z0 - 0.1f;
            kit.Col = concrete;
            kit.Box(d, new Vector3(0, (ya + yb) / 2 + 0.5f, innerZ), new Vector3(len / Mathf.Cos(ang), 1f, 0.2f), basis);
            // Bande de roulement peinte au milieu.
            kit.Solid = false;
            kit.Col = new Color(0.62f, 0.5f, 0.16f);
            kit.Box(paint, c + Vector3.Up * (slab / 2 + 0.012f), new Vector3(len / Mathf.Cos(ang) - 1f, 0.01f, 0.14f), basis);
            kit.Solid = true;
        }

        // Poteaux (trame 7,8 m), hors bandes de rampe ; pleine hauteur jusqu'à la dernière dalle.
        kit.Col = concrete.Lightened(0.03f);
        var nx = Mathf.Max(2, Mathf.RoundToInt(s.X / 7.8f));
        var colH = n * lh - slab;
        foreach (var z in new[] { -hz + 0.35f, -hz + rw1 + 0.35f, hz - rw1 - 0.35f, hz - 0.35f })
            for (var i = 0; i <= nx; i++)
            {
                var x = Mathf.Lerp(-hx + 0.35f, hx - 0.35f, i / (float)nx);
                kit.Box(d, new Vector3(x, colH / 2, z), new Vector3(0.55f, colH, 0.55f));
            }
        // Cage d'escalier en béton dans un angle (toiture-terrasse), édicule d'ascenseur.
        kit.Col = dark;
        var sc = new Vector3(hx - 2.6f, n * lh, 0);
        kit.Box(d, sc + Vector3.Up * 1.5f, new Vector3(4.2f, 3f, 4.6f));
        kit.Col = new Color(0.2f, 0.21f, 0.22f);
        kit.Box(Mats.DetailMetal(), sc + new Vector3(-2.11f, 1.05f, 0), new Vector3(0.04f, 2.1f, 1f));
        return kit.Commit();
    }

    // ───────────────────────── Escalier de secours ─────────────────────────

    /// <summary>
    /// Escalier métallique extérieur sur la façade <see cref="BuildingStyle.Facing"/> : volées droites alternées
    /// sur deux files, paliers à chaque bout, jusqu'à une passerelle au-dessus de l'acrotère (on descend sur le toit).
    /// Collision : plaque inclinée sous les marches (plan des milieux de marches), paliers, limons et garde-corps.
    /// </summary>
    private static void BuildFireEscape(MeshKit kit, Vector3 s, BuildingStyle style)
    {
        var metal = Mats.DetailMetal();
        var sd = Sides(s.X / 2, s.Z / 2)[Mathf.Clamp(style.Facing, 0, 3)];
        const float run = 5.2f, land = 1.4f, laneW = 1.1f;
        if (sd.Length < run + land * 2 + 2f) return;
        var top = s.Y + 0.95f;                      // au-dessus de la couvertine de l'acrotère
        var flights = Mathf.CeilToInt(top / 3.2f);
        var rise = top / flights;
        var c = sd.Length / 2;
        var aA = c - run / 2;
        var aB = c + run / 2;
        var steel = new Color(0.17f, 0.17f, 0.17f);
        Vector3 W(float a, float o, float y) => sd.Start + sd.Along * a + sd.Normal * o + Vector3.Up * y;
        var ax = Abs(sd.Along);
        var an = Abs(sd.Normal);

        for (var k = 0; k < flights; k++)
        {
            var outer = k % 2 == 1;
            var o0 = outer ? 0.2f + laneW + 0.05f : 0.2f;
            var oc = o0 + laneW / 2;
            var y0 = k * rise;
            var y1 = (k + 1) * rise;
            var from = outer ? aB : aA;
            var to = outer ? aA : aB;
            var p0 = W(from, oc, y0);
            var p1 = W(to, oc, y1);
            var dir = (p1 - p0).Normalized();
            var len = p0.DistanceTo(p1);
            var x = Vector3.Up.Cross(dir).Normalized();
            var y = dir.Cross(x).Normalized();
            var basis = new Basis(x, y, dir);
            // Plaque porteuse (sous-face visible) : son dessus passe juste sous le nez des marches (pas de ressaut aux paliers).
            var steps = Mathf.Max(4, Mathf.RoundToInt(rise / 0.19f));
            var sr = rise / steps;
            kit.Col = steel;
            kit.Box(metal, (p0 + p1) / 2 + y * (-0.05f + sr * 0.25f * Mathf.Cos(Mathf.Atan2(rise, run))), new Vector3(laneW, 0.1f, len), basis);
            // Marches (relief non solide) et limons.
            kit.Solid = false;
            kit.Col = new Color(0.22f, 0.22f, 0.21f);
            for (var i = 0; i < steps; i++)
            {
                var t = (i + 0.5f) / steps;
                var a = Mathf.Lerp(from, to, t);
                kit.Box(metal, W(a, oc, y0 + (i + 1) * sr - 0.02f), ax * (run / steps) + an * laneW + Vector3.Up * 0.04f);
            }
            kit.Solid = true;
            kit.Col = steel;
            foreach (var side in new[] { o0 + 0.03f, o0 + laneW - 0.03f })
            {
                var q0 = W(from, side, y0);
                var q1 = W(to, side, y1);
                kit.Box(metal, (q0 + q1) / 2 + Vector3.Up * 0.05f, new Vector3(0.06f, 0.28f, len), basis);
            }
            // Main courante côté vide (côté rue pour la file extérieure, côté file extérieure pour l'autre).
            var rail = o0 + laneW;
            kit.Tube(metal, W(from, rail, y0 + 1f), W(to, rail, y1 + 1f), 0.03f, 5);
            kit.Tube(metal, W(from, rail, y0), W(from, rail, y0 + 1f), 0.025f, 4);
            kit.Tube(metal, W(to, rail, y1), W(to, rail, y1 + 1f), 0.025f, 4);
        }

        // Paliers : au bout B aux niveaux impairs, au bout A aux niveaux pairs ; garde-corps sur l'extérieur.
        for (var j = 1; j <= flights; j++)
        {
            var atB = j % 2 == 1;
            var a0 = atB ? aB : aA - land;
            var a1 = atB ? aB + land : aA;
            var y = j * rise;
            var last = j == flights;
            var oIn = last ? -1.2f : 0.2f;          // dernier palier : passerelle au-dessus de l'acrotère
            var oOut = 0.2f + laneW * 2 + 0.05f;
            kit.Col = steel;
            var c0 = W(a0, oIn, y - 0.08f);
            var c1 = W(a1, oOut, y);
            kit.BoxMinMax(metal, new Vector3(Mathf.Min(c0.X, c1.X), c0.Y, Mathf.Min(c0.Z, c1.Z)), new Vector3(Mathf.Max(c0.X, c1.X), c1.Y, Mathf.Max(c0.Z, c1.Z)));
            // Garde-corps : côté rue et bout du palier.
            var ends = atB ? a1 : a0;
            kit.Tube(metal, W(a0, oOut, y + 1f), W(a1, oOut, y + 1f), 0.03f, 5);
            kit.Tube(metal, W(ends, 0.2f, y + 1f), W(ends, oOut, y + 1f), 0.03f, 5);
            foreach (var pa in new[] { a0, a1 })
                kit.Tube(metal, W(pa, oOut, y), W(pa, oOut, y + 1f), 0.025f, 4);
            kit.Tube(metal, W(ends, 0.2f, y), W(ends, 0.2f, y + 1f), 0.025f, 4);
            // Consoles fixées à la façade.
            kit.Box(metal, W((a0 + a1) / 2, (0.2f + oOut) / 2, y - 0.25f), ax * 0.1f + an * (oOut - 0.2f) + Vector3.Up * 0.3f);
        }
        // Palier de départ au sol (bout A) : rien à poser, la première volée part du trottoir.
    }

    // ───────────────────────── Panneaux arrachés ─────────────────────────

    /// <summary>
    /// Panneaux de mur-rideau arrachés sur un étage (repère de l'immeuble) : cavité sombre, nez de dalles, éclats de
    /// verre restés dans le châssis, montant tordu, suie au-dessus. Relief non solide.
    /// </summary>
    private static void PanelHole(MeshKit k, Vector3 c, Vector3 n, Vector3 along, float w, float hgt, int seed, bool glow, bool soot = true)
    {
        var keepSolid = k.Solid;
        k.Solid = false;
        var d = Mats.Detail();
        float R(int i) => RuinKit.Hash(seed, i, 61);
        var face = c + n * 0.05f;
        var hw = w / 2;
        var hh = hgt / 2;
        // Cavité (plateau de bureaux éventré, rougeoyant parfois la nuit).
        k.Col = glow ? new Color(0.32f, 0.11f, 0.03f) : new Color(0.026f, 0.025f, 0.024f);
        Material inner = glow ? Mats.Lamps() : d;
        k.OrientedQuad(inner, face - along * hw - Vector3.Up * hh, face + along * hw - Vector3.Up * hh, face + along * hw + Vector3.Up * hh, face - along * hw + Vector3.Up * hh, n);
        // Nez de dalle en haut et en bas (béton à nu, noirci), à peine saillants.
        k.Col = new Color(0.2f, 0.195f, 0.185f);
        k.Box(d, c + n * 0.08f - Vector3.Up * (hh - 0.14f), Abs(along) * w + Abs(n) * 0.16f + Vector3.Up * 0.28f);
        k.Box(d, c + n * 0.08f + Vector3.Up * (hh - 0.1f), Abs(along) * w + Abs(n) * 0.16f + Vector3.Up * 0.2f);
        // Éclats de verre restés pris dans les traverses : dents irrégulières, fines et de tailles variées.
        k.Col = new Color(0.19f, 0.22f, 0.24f);
        foreach (var sy in new[] { -1f, 1f })
        {
            var edgeY = sy * (hh - (sy < 0 ? 0.34f : 0.24f));
            var x = -hw + 0.05f;
            for (var i = 0; x < hw - 0.1f && i < 12; i++)
            {
                var wd = 0.12f + R(20 + i + (sy > 0 ? 12 : 0)) * 0.55f;
                var x1 = Mathf.Min(hw - 0.05f, x + wd);
                var len = R(40 + i + (sy > 0 ? 12 : 0));
                if (len > 0.3f)
                {
                    var tip = x + (x1 - x) * R(60 + i);
                    var b0 = face + n * 0.012f + along * x + Vector3.Up * edgeY;
                    var b1 = face + n * 0.012f + along * x1 + Vector3.Up * edgeY;
                    var t = face + n * 0.012f + along * tip + Vector3.Up * (edgeY - sy * len * (sy > 0 ? 1.1f : 0.6f));
                    k.Tri(d, b0, b1, t, face - n);
                }
                x = x1 + R(80 + i) * 0.3f;
            }
        }
        // Montant arraché qui pend.
        if (R(9) < 0.5f)
        {
            k.Col = new Color(0.12f, 0.12f, 0.125f);
            var a = (R(10) - 0.5f) * w * 0.7f;
            var top = c + along * a + n * 0.15f + Vector3.Up * (hh - 0.2f);
            var tilt = (R(11) - 0.5f) * 0.9f;
            var dir = (Vector3.Down + along * tilt + n * (0.15f + R(12) * 0.3f)).Normalized();
            k.Tube(Mats.DetailMetal(), top, top + dir * (hgt * (0.4f + R(13) * 0.4f)), 0.035f, 4, true);
        }
        // Suie au-dessus de l'ouverture.
        if (soot && R(14) < 0.7f)
        {
            var u0 = R(15) * 4f;
            var sh = 3f + R(16) * 4f;
            var b0 = face + n * 0.015f + Vector3.Up * hh;
            var sw = hw * 1.2f;
            k.Col = Colors.White;
            // (le long × haut) = normale sortante : face avant vers l'extérieur ; V croissant vers le bas (suie montante).
            k.Quad(Mats.Soot(), b0 - along * sw, b0 + along * sw, b0 + along * sw + Vector3.Up * sh, b0 - along * sw + Vector3.Up * sh,
                new Vector2(u0, 1), new Vector2(u0 + sw / 4f, 1), new Vector2(u0 + sw / 4f, 1 - sh / 8f), new Vector2(u0, 1 - sh / 8f));
        }
        k.Solid = keepSolid;
    }

    /// <summary>Position d'un groupe de panneaux sur la trame (centre local, normale, direction le long de la façade).</summary>
    private (Vector3 c, Vector3 n, Vector3 along, float w) PanelSpot(Vector3 local, Vector3 n, int salt)
    {
        var along = new Vector3(n.Z, 0, -n.X);
        var half = Mathf.Abs(n.X) > 0.5f ? Size.Z / 2 : Size.X / 2;
        var bays = 2 + (int)(RuinKit.Hash(Id, salt, 62) * 2.99f);
        var w = bays * Bay;
        // Trame comptée depuis l'angle de la façade (comme les UV du mur-rideau) : le trou tombe entre deux montants.
        var u = along.Dot(local) + half;
        var left = Mathf.Round((u - w / 2) / Bay) * Bay;
        left = Mathf.Clamp(left, Bay, Mathf.Max(Bay, Mathf.Floor((half * 2 - w - Bay) / Bay) * Bay));
        var a = left + w / 2 - half;
        // Étages comptés depuis le toit (trame de la façade), hors hall.
        var fl = Mathf.Max(0, Mathf.Floor((Size.Y - local.Y) / 3.2f));
        var yc = Size.Y - (fl + 0.5f) * 3.2f;
        if (yc < ShopH + 2f) yc = ShopH + 1.6f + 1.6f;
        var depth = Mathf.Abs(n.X) > 0.5f ? Size.X / 2 : Size.Z / 2;
        return (n * depth + along * a + Vector3.Up * yc, n, along, w);
    }

    /// <summary>Façades déjà éventrées au chargement (décor déterministe, sans effets).</summary>
    public void AddPanelScars(int count, int seed, bool night)
    {
        var k = new MeshKit();
        // Grappes d'arrachements (zones touchées) : plusieurs groupes de panneaux sur des étages voisins, décalés,
        // qui forment une brèche irrégulière plutôt que des trous isolés.
        var i = 0;
        for (var cl = 0; i < count; cl++)
        {
            var side = (int)(RuinKit.Hash(seed, cl, 63) * 4);
            var n = side switch { 0 => Vector3.Back, 1 => Vector3.Forward, 2 => Vector3.Right, _ => Vector3.Left };
            var along = new Vector3(n.Z, 0, -n.X);
            var half = Mathf.Abs(n.X) > 0.5f ? Size.Z / 2 : Size.X / 2;
            var cy = Size.Y * (0.2f + 0.7f * RuinKit.Hash(seed, cl, 64));
            var ca = (RuinKit.Hash(seed, cl, 66) - 0.5f) * half * 1.3f;
            var m = 2 + (int)(RuinKit.Hash(seed, cl, 69) * 3.99f);
            var topY = float.MinValue;
            var parts = new System.Collections.Generic.List<(Vector3 c, Vector3 n, Vector3 al, float w, int s)>();
            for (var j = 0; j < m && i < count; j++, i++)
            {
                var dy = j == 0 ? 0 : Mathf.RoundToInt((RuinKit.Hash(seed, i, 65) - 0.5f) * 3.2f);
                var dx = (RuinKit.Hash(seed, i, 67) - 0.5f) * 5f;
                var local = n * half + along * (ca + dx) + Vector3.Up * Mathf.Clamp(cy + dy * 3.2f, 8f, Size.Y - 3f);
                var spot = PanelSpot(local, n, seed + i);
                parts.Add((spot.c, spot.n, spot.along, spot.w, seed * 7 + i));
                topY = Mathf.Max(topY, spot.c.Y);
            }
            foreach (var p in parts)
                PanelHole(k, p.c, p.n, p.al, p.w, 3.2f, p.s, night && RuinKit.Hash(seed, p.s, 68) < 0.15f, Mathf.IsEqualApprox(p.c.Y, topY));
        }
        if (!k.Empty) AddChild(new MeshInstance3D { Name = "Scars", Mesh = k.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Arrachement de panneaux (tous les pairs) : vitres qui pleuvent, plaques qui tombent, incendie parfois.</summary>
    private void StandingFx(World w, int stage, Vector3 at)
    {
        var (p, n, _) = FacadePoint(at, stage);
        var (c, nn, along, width) = PanelSpot(p - GlobalPosition, n, stage * 13 + 5);
        var night = w.Mission.Time == TimeOfDay.Night;
        var k = new MeshKit();
        PanelHole(k, c, nn, along, width, 3.2f, Id * 101 + stage, night && H(20, stage) < 0.2f);
        var hole = new MeshInstance3D { Mesh = k.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, VisibilityRangeEnd = 600f };
        AddChild(hole);
        _stageFx ??= new System.Collections.Generic.List<Node>();
        _stageFx.Add(hole);

        var wp = GlobalPosition + c;
        var fx = ExplosionFx.For(w);
        var dist = fx.CameraDistance(wp);
        if (dist > 380f) return;
        var rng = new RandomNumberGenerator { Seed = (ulong)(Id * 37 + stage) };
        float R(float a, float b) => rng.RandfRange(a, b);
        // Pluie de verre : éclats qui scintillent en tombant le long de la façade.
        var glass = new Color(0.72f, 0.82f, 0.88f, 0.5f);
        for (var i = 0; i < 36; i++)
            fx.Spark(wp + nn * 0.3f + along * R(-width / 2, width / 2) + Vector3.Up * R(-1.4f, 1.4f), nn * R(0.6f, 3.5f) + Vector3.Up * R(-2f, 1.5f) + along * R(-1f, 1f), R(0.02f, 0.05f), R(1.6f, 3.2f), glass);
        // Plaques de verre et d'aluminium (corps rigides près de la caméra).
        if (dist < 110f)
        {
            var pool = DebrisPool.For(w);
            for (var i = 0; i < 3; i++)
            {
                var size = new Vector3(R(0.9f, 1.4f), R(1.4f, 2.6f), 0.06f);
                var pos = wp + nn * 0.6f + along * R(-width / 2, width / 2);
                var basis = new Basis(along, Vector3.Up, nn) * new Basis(new Vector3(R(-1, 1), R(-1, 1), R(-1, 1)).Normalized() + Vector3.Up * 0.01f, R(0f, 0.6f));
                pool.Spawn(new Transform3D(basis, pos), size, nn * R(1.5f, 4.5f) + Vector3.Up * R(-0.5f, 1.5f), new Vector3(R(-3, 3), R(-3, 3), R(-3, 3)),
                    i == 0 ? new Color(0.3f, 0.31f, 0.32f) : new Color(0.14f, 0.17f, 0.19f), R(10f, 14f));
            }
        }
        for (var i = 0; i < 10; i++)
            fx.Chunk(wp + nn * 0.4f + along * R(-1.5f, 1.5f), nn * R(1f, 5f) + Vector3.Up * R(-1f, 2f), R(0.05f, 0.2f), R(3f, 5f), new Color(0.32f, 0.31f, 0.3f));
        var dust = new Color(0.34f, 0.32f, 0.29f, 0.5f);
        for (var i = 0; i < 4; i++)
            fx.DustPuff(wp + nn * 1.1f + along * R(-1.5f, 1.5f), nn * R(1.5f, 4f) + new Vector3(R(-1, 1), R(-0.5f, 1f), R(-1, 1)), R(1.6f, 2.6f), R(2.5f, 4f), dust);
        Sfx.At("glass", wp, 3f, R(0.75f, 0.95f), 40);
        Sfx.At("smash", wp, -2f, R(0.45f, 0.55f), 60);

        // Incendie d'étage de temps en temps, fumée noire qui lèche la façade.
        if (stage % 5 == 3 && _fires < MaxFires)
        {
            var light = night && _fireLights < 3;
            if (light) _fireLights++;
            var fire = RuinFire.Create(Vector3.Zero, 1.1f, light, night, Id + stage);
            fire.SetMeta("light", light);
            AddChild(fire);
            fire.GlobalPosition = wp + nn * 1.0f - Vector3.Up * 1.2f;
            _stageFx.Add(fire);
            _fires++;
        }
        else if (stage % 7 == 4)
        {
            var smoke = RuinFire.SmokeColumn(c + nn * 1.2f, 1.4f, true);
            AddChild(smoke);
            _stageFx.Add(smoke);
        }
    }
}
