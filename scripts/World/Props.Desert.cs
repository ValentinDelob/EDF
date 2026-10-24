using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Matériel de la base militaire du désert : gabions de rempart et murs en T, miradors, guérite, véhicules légers et
/// camions sable (avec leur épave calcinée), épaves de chars et de blindés, soute à munitions enterrée (citerne
/// explosive, voir <see cref="Destructibles.AddTank"/>), épaves d'avions et d'hélicoptère, caisses de munitions,
/// balises de piste. Convention : avant vers +Z, posé sur y = 0. Teintes ternes, salissures du shader d'usure.
/// </summary>
public static partial class Props
{
    private static readonly Color DesertTan = new(0.44f, 0.39f, 0.3f);
    private static readonly Color DesertTanDark = new(0.33f, 0.29f, 0.22f);
    private static readonly Color MilGrey = new(0.36f, 0.37f, 0.36f);

    /// <summary>Épave calcinée propre à un modèle de véhicule (remplace l'épave générique de sa famille), ou null.</summary>
    private static readonly Dictionary<Mesh, Mesh> Wrecks = new();

    /// <summary>Épave à montrer quand ce véhicule explose (null : épave générique de la famille).</summary>
    public static Mesh WreckOf(Mesh m) => m != null && Wrecks.TryGetValue(m, out var w) ? w : null;

    // ───────────────────────── Rempart ─────────────────────────

    /// <summary>Longueur d'un module de gabions du rempart (trois cellules).</summary>
    public const float HescoLen = 3.3f, HescoH = 2.15f, HescoD = 1.1f;

    /// <summary>
    /// Module de rempart en gabions (trois cellules grillagées de 1,1 m remplies de sable, 2,15 m), long selon X.
    /// Variante 1 : cellule du milieu éventrée (géotextile déchiré, sable répandu, plus basse).
    /// </summary>
    public static ArrayMesh HescoWall(int v) => CachedKind($"deshesco{v}", PropKind.Barrier, k =>
    {
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        for (var i = 0; i < 3; i++)
        {
            var x = (i - 1) * 1.1f;
            var torn = v == 1 && i == 1;
            var h = torn ? 1.35f : HescoH - H(v, i, 5) * 0.12f;
            // Sable tassé dans le géotextile (bombé), crête plus claire.
            k.Col = new Color(0.66f, 0.6f, 0.47f) * (0.9f + H(v, i, 6) * 0.14f);
            k.Box(d, new Vector3(x, h / 2, 0), new Vector3(1.04f, h, 1.04f));
            // Ventre des cellules (le sable pousse le géotextile entre les montants) et joints sombres entre cellules.
            k.Col = new Color(0.62f, 0.56f, 0.44f) * (0.9f + H(v, i, 7) * 0.12f);
            k.Box(d, new Vector3(x, h * 0.45f, 0), new Vector3(0.9f, h * 0.7f, 1.12f));
            k.Box(d, new Vector3(x, h * 0.45f, 0), new Vector3(1.12f, h * 0.7f, 0.9f));
            k.Col = new Color(0.6f, 0.55f, 0.44f);
            k.Frustum(d, new Vector3(x, h, 0), new Vector2(1.0f, 1.0f), new Vector2(0.7f, 0.7f), 0.08f);
            if (torn)
            {
                // Coulée de sable au pied (praticable).
                k.Col = new Color(0.5f, 0.45f, 0.35f);
                k.Frustum(d, new Vector3(x, 0, 0.75f), new Vector2(1.3f, 1.2f), new Vector2(0.5f, 0.3f), 0.45f);
                k.Frustum(d, new Vector3(x, 0, -0.75f), new Vector2(1.2f, 1.0f), new Vector2(0.4f, 0.3f), 0.3f);
            }
            // Grillage soudé : montants et lisses en léger relief (non solides).
            k.Solid = false;
            k.Col = new Color(0.3f, 0.3f, 0.28f);
            for (var j = 0; j <= 3; j++)
            {
                var t = x - 0.54f + j * 0.36f;
                foreach (var s in new[] { -0.575f, 0.575f })
                    k.Box(m, new Vector3(t, h / 2, s), new Vector3(0.03f, h, 0.03f));
            }
            for (var j = 0; j <= 5; j++)
            {
                var y = 0.03f + j * (h - 0.06f) / 5f;
                foreach (var s in new[] { -0.575f, 0.575f })
                    k.Box(m, new Vector3(x, y, s), new Vector3(1.12f, 0.028f, 0.028f));
            }
            k.Solid = true;
        }
    });

    /// <summary>Mur en T (« Bremer ») en béton préfabriqué, 3,6 m × 1,5 m, semelle en T ; tagué de numéros au pochoir.</summary>
    public static ArrayMesh TWall(int v) => CachedKind($"destwall{v}", PropKind.Barrier, k =>
    {
        var d = Mats.Detail();
        k.Col = new Color(0.52f, 0.5f, 0.46f) * (0.92f + v * 0.06f);
        k.Box(d, new Vector3(0, 0.2f, 0), new Vector3(1.5f, 0.4f, 1.3f));
        k.Frustum(d, new Vector3(0, 0.4f, 0), new Vector2(1.5f, 0.32f), new Vector2(1.5f, 0.22f), 3.2f);
        k.Col = new Color(0.43f, 0.41f, 0.37f);
        k.Box(d, new Vector3(0, 0.05f, 0), new Vector3(1.54f, 0.1f, 1.34f));
        // Anneaux de levage et numéro au pochoir (relief non solide).
        k.Solid = false;
        k.Col = new Color(0.2f, 0.19f, 0.18f);
        k.Box(d, new Vector3(0, 3.55f, 0), new Vector3(0.22f, 0.12f, 0.05f));
        k.Col = new Color(0.16f, 0.15f, 0.13f);
        k.Box(d, new Vector3(-0.2f + v * 0.15f, 2.2f, 0.135f), new Vector3(0.55f, 0.32f, 0.01f));
        k.Solid = true;
    });

    // ───────────────────────── Miradors et guérite ─────────────────────────

    /// <summary>Hauteur du plancher du mirador.</summary>
    public const float TowerDeck = 6.4f;

    /// <summary>
    /// Mirador en charpente d'acier : quatre pieds contreventés, plancher à 6,4 m ceint de sacs de sable (ouverture
    /// côté +Z), auvent de tôle ; escalier droit qui descend vers +Z (plaque inclinée solide sous les marches).
    /// Famille « poteau » : il bascule d'un bloc quand on l'abat.
    /// </summary>
    public static ArrayMesh Watchtower() => CachedKind("deswatch", PropKind.Pole, k =>
    {
        var m = Mats.DetailMetal();
        var d = Mats.Detail();
        var roof = Mats.Corrugated(new Color(0.34f, 0.32f, 0.28f), 1601, 1.2f);
        const float hw = 1.8f, deck = TowerDeck;
        // Pieds évasés et contreventement.
        k.Col = new Color(0.27f, 0.26f, 0.23f);
        foreach (var sx in new[] { -1f, 1f })
        foreach (var sz in new[] { -1f, 1f })
            k.Tube(m, new Vector3(sx * (hw + 0.5f), 0, sz * (hw + 0.5f)), new Vector3(sx * (hw - 0.1f), deck, sz * (hw - 0.1f)), 0.11f, 6, true);
        k.Solid = false;
        for (var lv = 0; lv < 3; lv++)
        {
            var y0 = lv * deck / 3f;
            var y1 = (lv + 1) * deck / 3f;
            float R(float y) => hw + 0.5f - 0.6f * (y / deck);
            foreach (var (ax, az) in new[] { (1f, 0f), (0f, 1f) })
            foreach (var s in new[] { -1f, 1f })
            {
                Vector3 P(float t, float y) => ax > 0 ? new Vector3(t * R(y), y, s * R(y)) : new Vector3(s * R(y), y, t * R(y));
                k.Tube(m, P(-1, y0), P(1, y1), 0.035f, 4);
                k.Tube(m, P(1, y0), P(-1, y1), 0.035f, 4);
            }
        }
        k.Solid = true;
        // Plancher (caillebotis plein) et sacs de sable sur trois côtés et demi-côté avant.
        k.Col = new Color(0.25f, 0.24f, 0.22f);
        k.Box(m, new Vector3(0, deck - 0.1f, 0), new Vector3(hw * 2 + 0.4f, 0.2f, hw * 2 + 0.4f));
        SandbagWall(k, new Vector3(-hw, deck, -hw), new Vector3(hw, deck, -hw), 6, 11);
        SandbagWall(k, new Vector3(hw, deck, -hw), new Vector3(hw, deck, hw), 6, 12);
        SandbagWall(k, new Vector3(-hw, deck, hw), new Vector3(-hw, deck, -hw), 6, 13);
        SandbagWall(k, new Vector3(hw, deck, hw), new Vector3(0.65f, deck, hw), 6, 14);
        SandbagWall(k, new Vector3(-0.65f, deck, hw), new Vector3(-hw, deck, hw), 6, 15);
        // Poteaux et auvent de tôle (solide : on peut s'y hisser en volant).
        k.Col = new Color(0.27f, 0.26f, 0.23f);
        foreach (var sx in new[] { -1f, 1f })
        foreach (var sz in new[] { -1f, 1f })
            k.Box(m, new Vector3(sx * (hw - 0.05f), deck + 1.25f, sz * (hw - 0.05f)), new Vector3(0.12f, 2.5f, 0.12f));
        k.Col = Colors.White;
        k.QuadC(roof, new Vector3(-hw - 0.4f, deck + 2.7f, -hw - 0.4f), new Vector3(hw + 0.4f, deck + 2.7f, -hw - 0.4f), new Vector3(hw + 0.4f, deck + 2.4f, hw + 0.4f), new Vector3(-hw - 0.4f, deck + 2.4f, hw + 0.4f),
            Vector2.Zero, new Vector2(1.5f, 0), new Vector2(1.5f, 1.5f), new Vector2(0, 1.5f), Colors.White, Colors.White, Colors.White, Colors.White, Vector3.Up);
        k.QuadC(roof, new Vector3(-hw - 0.4f, deck + 2.62f, -hw - 0.4f), new Vector3(hw + 0.4f, deck + 2.62f, -hw - 0.4f), new Vector3(hw + 0.4f, deck + 2.32f, hw + 0.4f), new Vector3(-hw - 0.4f, deck + 2.32f, hw + 0.4f),
            Vector2.Zero, new Vector2(1.5f, 0), new Vector2(1.5f, 1.5f), new Vector2(0, 1.5f), Colors.White, Colors.White, Colors.White, Colors.White, Vector3.Down);
        // Projecteur (non solide).
        k.Solid = false;
        k.Col = new Color(0.12f, 0.12f, 0.11f);
        k.Cylinder(m, new Vector3(hw - 0.4f, deck + 1.9f, -hw + 0.4f), 0.22f, 0.26f, 0.35f, 10, true, new Basis(Vector3.Right, -1.2f));
        k.Solid = true;
        // Escalier : de (0, 0, hw + 9) au plancher en (0, deck, hw + 0.2), largeur 1 m.
        const float run = 8.6f;
        var z0 = hw + 0.2f + run;
        var z1 = hw + 0.2f;
        k.Col = new Color(0.22f, 0.21f, 0.2f);
        k.Quad(m, new Vector3(-0.55f, 0, z0), new Vector3(0.55f, 0, z0), new Vector3(0.55f, deck, z1), new Vector3(-0.55f, deck, z1));
        k.Quad(m, new Vector3(-0.55f, deck, z1), new Vector3(0.55f, deck, z1), new Vector3(0.55f, 0, z0), new Vector3(-0.55f, 0, z0));
        k.Solid = false;
        var steps = Mathf.CeilToInt(deck / 0.21f);
        for (var s = 0; s < steps; s++)
        {
            var u = (s + 0.5f) / steps;
            k.Box(m, new Vector3(0, Mathf.Lerp(0, deck, u) + 0.05f, Mathf.Lerp(z0, z1, u)), new Vector3(1.05f, 0.04f, 0.26f));
        }
        k.Col = new Color(0.4f, 0.34f, 0.16f);
        foreach (var x in new[] { -0.58f, 0.58f })
        {
            k.Tube(m, new Vector3(x, 1f, z0), new Vector3(x, deck + 1f, z1), 0.025f, 4);
            k.Tube(m, new Vector3(x, 0, z0 - 0.3f), new Vector3(x, 1f, z0 - 0.3f), 0.03f, 4);
            k.Tube(m, new Vector3(x, 0, (z0 + z1) / 2), new Vector3(x, deck / 2 + 0.05f, (z0 + z1) / 2), 0.05f, 4);
        }
        k.Solid = true;
    });

    /// <summary>Guérite de contrôle : cabine de béton à bandeau vitré, auvent, sacs de sable au pied.</summary>
    public static ArrayMesh GuardBooth() => CachedKind("desbooth", PropKind.Shelter, k =>
    {
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        var glass = Mats.Painted(new Color(0.05f, 0.06f, 0.065f), 0.08f, 0.3f);
        k.Col = new Color(0.5f, 0.47f, 0.41f);
        k.Box(d, new Vector3(0, 0.55f, 0), new Vector3(2.4f, 1.1f, 2.4f));
        k.Col = new Color(0.22f, 0.22f, 0.2f);
        foreach (var sx in new[] { -1.15f, 1.15f })
        foreach (var sz in new[] { -1.15f, 1.15f })
            k.Box(m, new Vector3(sx, 1.65f, sz), new Vector3(0.12f, 1.1f, 0.12f));
        k.Box(glass, new Vector3(0, 1.65f, 0), new Vector3(2.2f, 1.05f, 2.2f));
        k.Col = new Color(0.45f, 0.42f, 0.37f);
        k.Box(d, new Vector3(0, 2.4f, 0), new Vector3(2.9f, 0.4f, 2.9f));
        k.Col = new Color(0.2f, 0.2f, 0.19f);
        k.Box(d, new Vector3(0, 0.5f, 1.21f), new Vector3(0.9f, 1.0f, 0.03f));
        SandbagWall(k, new Vector3(-1.9f, 0, 1.9f), new Vector3(1.9f, 0, 1.9f), 5, 21);
    });

    /// <summary>Barrière levante (bras rayé abaissé sur sa béquille) ; famille « petit mobilier ».</summary>
    public static ArrayMesh BoomGate() => CachedKind("desboom", PropKind.Small, k =>
    {
        var m = Mats.DetailMetal();
        var d = Mats.Detail();
        k.Col = new Color(0.4f, 0.38f, 0.33f);
        k.Box(d, new Vector3(0, 0.55f, 0), new Vector3(0.5f, 1.1f, 0.5f));
        for (var i = 0; i < 7; i++)
        {
            k.Col = i % 2 == 0 ? new Color(0.55f, 0.53f, 0.48f) : new Color(0.38f, 0.1f, 0.08f);
            k.Box(m, new Vector3(0.4f + i * 0.9f + 0.45f, 1.0f, 0), new Vector3(0.9f, 0.12f, 0.1f));
        }
        k.Col = new Color(0.25f, 0.24f, 0.22f);
        k.Box(m, new Vector3(6.6f, 0.47f, 0), new Vector3(0.08f, 0.95f, 0.08f));
    });

    // ───────────────────────── Véhicules ─────────────────────────

    /// <summary>
    /// Véhicule léger tactique (4 × 4 large et bas) : caisse sable, capot plongeant, cabine à vitres blindées, plateau
    /// arrière, roue de secours ; variante 1 avec tourelleau et bouclier. Famille « voiture » (explose).
    /// </summary>
    public static ArrayMesh MilJeep(int v)
    {
        var mesh = CachedKind($"desjeep{v}", PropKind.Car, k => Jeep(k, v, false));
        if (!Wrecks.ContainsKey(mesh)) Wrecks[mesh] = Cached($"desjeepw{v}", k => Jeep(k, v, true));
        return mesh;
    }

    private static void Jeep(MeshKit k, int v, bool burnt)
    {
        var d = burnt ? (Material)Mats.Charred() : Mats.Detail();
        var m = burnt ? (Material)Mats.Charred() : Mats.DetailMetal();
        var glass = Mats.Painted(new Color(0.06f, 0.07f, 0.07f), 0.1f, 0.3f);
        var paint = burnt ? BurnRust : DesertTan;
        var dark = burnt ? BurnSoot : DesertTanDark;
        var drop = burnt ? 0.14f : 0f;
        foreach (var z in new[] { 1.6f, -1.55f })
        foreach (var x in new[] { -0.92f, 0.92f })
        {
            if (burnt) Rim(k, new Vector3(x, 0.32f, z), 0.32f, 0.3f);
            else Tyre(k, new Vector3(x, 0.47f, z), 0.47f, 0.37f, dark);
        }
        var y0 = 0.6f - drop;
        // Châssis et bas de caisse.
        k.Col = burnt ? BurnSoot : new Color(0.1f, 0.1f, 0.09f);
        k.Box(m, new Vector3(0, y0 - 0.05f, 0), new Vector3(1.3f, 0.25f, 4.5f));
        k.Col = paint;
        k.BoxMinMax(d, new Vector3(-1.1f, y0 + 0.05f, -2.4f), new Vector3(1.1f, y0 + 0.6f, 2.35f));
        // Ailes avant larges, capot plongeant.
        k.Col = dark;
        foreach (var x in new[] { -1f, 1f })
            k.Box(d, new Vector3(x * 0.95f, y0 + 0.62f, 1.6f), new Vector3(0.5f, 0.12f, 1.3f));
        k.Col = paint;
        k.Frustum(d, new Vector3(0, y0 + 0.6f, 1.55f), new Vector2(1.9f, 1.6f), new Vector2(1.7f, 1.4f), 0.18f, new Vector2(0, -0.1f));
        k.Col = new Color(0.08f, 0.08f, 0.07f);
        k.Box(m, new Vector3(0, y0 + 0.4f, 2.37f), new Vector3(1.4f, 0.45f, 0.06f)); // calandre
        // Cabine (vitres blindées épaisses).
        var roofY = burnt ? y0 + 1.2f : y0 + 1.35f;
        k.Col = paint;
        foreach (var x in new[] { -1.03f, 1.03f })
        {
            k.Box(d, new Vector3(x, (y0 + 0.6f + roofY) / 2, 0.75f), new Vector3(0.1f, roofY - y0 - 0.6f, 0.12f));
            k.Box(d, new Vector3(x, (y0 + 0.6f + roofY) / 2, -0.25f), new Vector3(0.1f, roofY - y0 - 0.6f, 0.14f));
            k.Box(d, new Vector3(x, (y0 + 0.6f + roofY) / 2, -1.2f), new Vector3(0.1f, roofY - y0 - 0.6f, 0.14f));
        }
        if (burnt)
        {
            k.Box(d, new Vector3(0, roofY, -0.25f), new Vector3(2.0f, 0.06f, 1.9f), Rot(0, 0.06f, 0.04f));
        }
        else
        {
            k.Box(d, new Vector3(0, roofY, -0.22f), new Vector3(2.1f, 0.1f, 2.05f));
            k.Box(glass, new Vector3(0, (y0 + 0.6f + roofY) / 2, 0.78f), new Vector3(1.9f, roofY - y0 - 0.7f, 0.06f), Rot(0, -0.2f));
            foreach (var x in new[] { -1.04f, 1.04f })
            {
                k.Box(glass, new Vector3(x, y0 + 1.05f, 0.25f), new Vector3(0.05f, 0.45f, 0.85f));
                k.Box(glass, new Vector3(x, y0 + 1.05f, -0.72f), new Vector3(0.05f, 0.45f, 0.8f));
            }
            // Portières : joints sombres.
            k.Col = new Color(0.12f, 0.11f, 0.09f);
            foreach (var x in new[] { -1.11f, 1.11f })
                k.Box(d, new Vector3(x, y0 + 0.6f, -0.25f), new Vector3(0.02f, 0.9f, 0.03f));
        }
        // Plateau arrière, ridelles, roue de secours et jerricans.
        k.Col = paint;
        foreach (var x in new[] { -1.06f, 1.06f })
            k.Box(d, new Vector3(x, y0 + 0.8f, -1.85f), new Vector3(0.08f, 0.4f, 1.1f));
        k.Box(d, new Vector3(0, y0 + 0.8f, -2.38f), new Vector3(2.1f, 0.4f, 0.06f));
        if (!burnt)
        {
            Tyre(k, new Vector3(0.55f, y0 + 1.05f, -2.0f), 0.45f, 0.3f, dark);
            k.Col = new Color(0.22f, 0.24f, 0.17f);
            for (var i = 0; i < 3; i++) k.Box(d, new Vector3(-0.7f + i * 0.3f, y0 + 0.85f, -2.05f), new Vector3(0.18f, 0.48f, 0.34f));
        }
        if (v == 1)
        {
            // Tourelleau : anneau, bouclier, mitrailleuse.
            k.Col = burnt ? BurnSoot : dark;
            k.Cylinder(m, new Vector3(0, roofY + 0.05f, -0.3f), 0.55f, 0.55f, 0.18f, 12);
            k.Box(d, new Vector3(0, roofY + 0.6f, 0.15f), new Vector3(1.0f, 0.75f, 0.06f), Rot(0, 0.1f));
            k.Solid = false;
            k.Col = new Color(0.08f, 0.08f, 0.08f);
            k.Tube(m, new Vector3(0, roofY + 0.7f, -0.1f), new Vector3(0, roofY + 0.75f + (burnt ? -0.3f : 0f), 1.1f), 0.05f, 6);
            k.Solid = true;
        }
        // Antenne fouet (non solide).
        if (!burnt)
        {
            k.Solid = false;
            k.Col = new Color(0.1f, 0.1f, 0.1f);
            k.Tube(m, new Vector3(-0.95f, y0 + 0.6f, -2.3f), new Vector3(-1.05f, y0 + 3.4f, -2.6f), 0.012f, 3);
            k.Solid = true;
        }
    }

    /// <summary>Épave calcinée du véhicule léger (décor fixe, indestructible).</summary>
    public static ArrayMesh MilJeepWreck(int v = 0)
    {
        MilJeep(v);
        return (ArrayMesh)Wrecks[MilJeep(v)];
    }

    /// <summary>Taille de collision du véhicule léger.</summary>
    public static readonly Vector3 MilJeepSize = new(2.3f, 2f, 4.9f);

    /// <summary>
    /// Camion tactique sable à plateau bâché (6 × 6) ; famille « camion » (fume, brûle, explose), épave propre.
    /// </summary>
    public static ArrayMesh MilTruck()
    {
        var mesh = CachedKind("destruck", PropKind.Truck, k => DesertTruck(k, false));
        if (!Wrecks.ContainsKey(mesh)) Wrecks[mesh] = Cached("destruckw", k => DesertTruck(k, true));
        return mesh;
    }

    private static void DesertTruck(MeshKit k, bool burnt)
    {
        var d = burnt ? (Material)Mats.Charred() : Mats.Detail();
        var m = burnt ? (Material)Mats.Charred() : Mats.DetailMetal();
        var glass = Mats.Painted(new Color(0.05f, 0.06f, 0.06f), 0.1f, 0.3f);
        var paint = burnt ? BurnRust : DesertTan;
        var dark = burnt ? BurnSoot : DesertTanDark;
        const float l = 7.8f;
        foreach (var z in new[] { 2.7f, -1.5f, -2.8f })
        foreach (var x in new[] { -1.05f, 1.05f })
        {
            if (burnt) Rim(k, new Vector3(x, 0.4f, z), 0.4f, 0.36f);
            else Tyre(k, new Vector3(x, 0.58f, z), 0.58f, 0.4f, dark);
        }
        var drop = burnt ? 0.15f : 0f;
        k.Col = burnt ? BurnSoot : new Color(0.08f, 0.08f, 0.07f);
        k.Box(m, new Vector3(0, 1.0f - drop, -0.2f), new Vector3(1.6f, 0.35f, l - 0.6f));
        k.Col = paint;
        k.BoxMinMax(d, new Vector3(-1.15f, 1.05f - drop, l / 2 - 1.5f), new Vector3(1.15f, 1.95f - drop, l / 2));
        k.BoxMinMax(d, new Vector3(-1.25f, 1.05f - drop, l / 2 - 3.2f), new Vector3(1.25f, 2.95f - drop, l / 2 - 1.5f));
        k.Col = dark;
        foreach (var x in new[] { -1.32f, 1.32f })
            k.Box(d, new Vector3(x, 1.45f - drop, l / 2 - 0.65f), new Vector3(0.26f, 0.12f, 1.5f));
        if (!burnt)
        {
            k.Box(glass, new Vector3(0, 2.45f, l / 2 - 1.48f), new Vector3(2.15f, 0.75f, 0.04f));
            k.Box(glass, new Vector3(0, 2.45f, l / 2 - 2.3f), new Vector3(2.52f, 0.6f, 0.85f));
        }
        k.Col = new Color(0.1f, 0.1f, 0.09f);
        k.Box(m, new Vector3(0, 1.35f - drop, l / 2 + 0.05f), new Vector3(2.1f, 0.5f, 0.12f));
        var z0 = l / 2 - 3.3f;
        var z1 = -l / 2;
        k.Col = paint * 0.95f;
        k.BoxMinMax(d, new Vector3(-1.28f, 1.2f - drop, z1), new Vector3(1.28f, 1.3f - drop, z0));
        foreach (var x in new[] { -1.26f, 1.26f })
            k.BoxMinMax(d, new Vector3(x - 0.03f, 1.3f - drop, z1), new Vector3(x + 0.03f, 1.9f - drop, z0));
        var mid = (z0 + z1) / 2;
        var len = z0 - z1;
        if (burnt)
        {
            // Bâche brûlée : il ne reste que les arceaux, cargaison calcinée.
            k.Col = BurnSoot;
            for (var i = 0; i < 4; i++)
            {
                var z = z1 + 0.3f + i * (len - 0.6f) / 3f;
                k.Tube(m, new Vector3(-1.22f, 1.9f - drop, z), new Vector3(-0.9f, 3.0f - drop, z), 0.04f, 4);
                k.Tube(m, new Vector3(-0.9f, 3.0f - drop, z), new Vector3(0.9f, 3.0f - drop - i * 0.08f, z), 0.04f, 4);
                k.Tube(m, new Vector3(0.9f, 3.0f - drop - i * 0.08f, z), new Vector3(1.22f, 1.9f - drop, z), 0.04f, 4);
            }
            k.Col = BurnAsh * 0.5f;
            for (var i = 0; i < 5; i++) k.Box(d, new Vector3(-0.7f + (i % 3) * 0.7f, 1.55f - drop, z1 + 0.8f + i * 0.8f), new Vector3(0.6f, 0.45f, 0.55f), Rot(i * 0.4f));
        }
        else
        {
            k.Col = new Color(0.4f, 0.36f, 0.27f);
            k.BoxMinMax(d, new Vector3(-1.29f, 1.9f, z1), new Vector3(-1.23f, 3.15f, z0));
            k.BoxMinMax(d, new Vector3(1.23f, 1.9f, z1), new Vector3(1.29f, 3.15f, z0));
            k.Box(d, new Vector3(-0.64f, 3.35f, mid), new Vector3(1.36f, 0.06f, len), Rot(0, 0, 0.3f));
            k.Box(d, new Vector3(0.64f, 3.35f, mid), new Vector3(1.36f, 0.06f, len), Rot(0, 0, -0.3f));
            k.BoxMinMax(d, new Vector3(-1.27f, 1.9f, z0 - 0.04f), new Vector3(1.27f, 3.2f, z0));
            k.Box(d, new Vector3(0, 3.05f, z1 + 0.05f), new Vector3(2.45f, 0.25f, 0.25f));
        }
        // Poussière collée sur le bas de caisse.
        k.Col = burnt ? BurnSoot : new Color(0.46f, 0.41f, 0.32f);
        k.BoxMinMax(d, new Vector3(-1.3f, 0.9f - drop, z1 + 0.2f), new Vector3(1.3f, 1.15f - drop, z0));
    }

    /// <summary>
    /// Épave de char de combat calcinée : caisse à glacis, chenille déroulée, galets ; variante 0 tourelle de travers
    /// au canon affaissé, variante 1 tourelle soufflée (renversée à côté, trou de la couronne béant). Famille « épave ».
    /// </summary>
    public static ArrayMesh TankWreck(int v) => CachedKind($"destankw{v}", PropKind.Wreck, k =>
    {
        var ch = Mats.Charred();
        const float hl = 3.9f, hw = 1.75f;
        // Chenilles et galets.
        foreach (var sx in new[] { -1f, 1f })
        {
            var x = sx * (hw - 0.3f);
            var thrown = v == 1 && sx > 0;
            k.Col = BurnSoot;
            if (!thrown) k.Box(ch, new Vector3(x, 0.5f, 0), new Vector3(0.62f, 0.95f, hl * 2 - 0.3f));
            k.Col = BurnRust * 0.8f;
            for (var i = 0; i < 6; i++)
                k.Cylinder(ch, new Vector3(x - 0.33f, 0.42f, -hl + 0.9f + i * 1.25f), 0.38f, 0.38f, 0.66f, 10, true, new Basis(Vector3.Back, -Mathf.Pi / 2));
            if (thrown)
            {
                // Chenille déroulée sur le sable.
                k.Col = BurnSoot;
                k.Box(ch, new Vector3(x + 1.1f, 0.05f, -1.0f), new Vector3(0.62f, 0.1f, hl * 2.2f), Rot(0.12f));
            }
        }
        // Caisse : glacis avant incliné, plage arrière.
        var y0 = 0.55f;
        k.Col = BurnBase;
        k.BoxMinMax(ch, new Vector3(-hw + 0.6f, y0, -hl), new Vector3(hw - 0.6f, y0 + 0.6f, hl - 0.5f));
        k.Col = BurnRust;
        k.BoxMinMax(ch, new Vector3(-hw, y0 + 0.6f, -hl), new Vector3(hw, y0 + 1.0f, hl - 1.6f));
        k.Quad(ch, new Vector3(-hw, y0 + 1.0f, hl - 1.6f), new Vector3(hw, y0 + 1.0f, hl - 1.6f), new Vector3(hw, y0 + 0.3f, hl), new Vector3(-hw, y0 + 0.3f, hl));
        k.Quad(ch, new Vector3(-hw, y0 + 0.3f, hl), new Vector3(hw, y0 + 0.3f, hl), new Vector3(hw, y0 + 0.6f, hl - 0.5f), new Vector3(-hw, y0 + 0.6f, hl - 0.5f));
        foreach (var sx in new[] { -1f, 1f })
            k.Tri(ch, new Vector3(sx * hw, y0 + 1.0f, hl - 1.6f), new Vector3(sx * hw, y0 + 0.3f, hl), new Vector3(sx * hw, y0 + 0.6f, hl - 1.6f), new Vector3(0, y0 + 0.6f, hl - 1.6f));
        // Garde-boue déchirés, caissons de rangement.
        k.Col = BurnSoot;
        foreach (var sx in new[] { -1f, 1f })
            k.Box(ch, new Vector3(sx * (hw + 0.05f), y0 + 0.62f, -0.4f), new Vector3(0.12f, 0.06f, hl * 1.6f), Rot(0, 0, sx * 0.08f));
        k.Box(ch, new Vector3(0, y0 + 1.25f, -hl + 0.6f), new Vector3(hw * 1.6f, 0.5f, 0.9f));
        var ring = new Vector3(0, y0 + 1.0f, -0.4f);
        if (v == 0)
        {
            var tb = Rot(0.6f, 0.04f, 0.05f);
            k.Xf = new Transform3D(tb, ring);
            k.Col = BurnBase;
            k.Frustum(ch, Vector3.Zero, new Vector2(3.1f, 3.6f), new Vector2(2.5f, 2.6f), 0.85f, new Vector2(0, -0.2f));
            k.Col = BurnRust;
            k.Box(ch, new Vector3(0, 0.45f, 1.95f), new Vector3(1.1f, 0.6f, 0.5f));
            k.Box(ch, new Vector3(0, 0.6f, -2.0f), new Vector3(2.2f, 0.55f, 0.8f));
            k.Col = BurnSoot;
            // Canon affaissé (bouche vers le sol).
            k.Tube(ch, new Vector3(0, 0.45f, 2.1f), new Vector3(0, -0.35f, 6.4f), 0.11f, 8, true);
            k.Box(ch, new Vector3(0.7f, 1.0f, -0.4f), new Vector3(0.8f, 0.3f, 0.8f));
            k.Xf = Transform3D.Identity;
        }
        else
        {
            // Couronne béante et tourelle renversée à côté.
            k.Col = new Color(0.03f, 0.03f, 0.03f);
            k.Cylinder(ch, ring + Vector3.Up * 0.005f, 1.1f, 1.1f, 0.02f, 14);
            var tb = Rot(2.2f, 0.3f, 2.6f);
            k.Xf = new Transform3D(tb, new Vector3(-3.6f, 0.9f, -2.2f));
            k.Col = BurnBase;
            k.Frustum(ch, Vector3.Zero, new Vector2(3.1f, 3.6f), new Vector2(2.5f, 2.6f), 0.85f, new Vector2(0, -0.2f));
            k.Col = BurnRust;
            k.Box(ch, new Vector3(0, 0.45f, 1.95f), new Vector3(1.1f, 0.6f, 0.5f));
            k.Col = BurnSoot;
            k.Tube(ch, new Vector3(0, 0.45f, 2.1f), new Vector3(0, 0.45f, 5.2f), 0.11f, 8, true);
            k.Xf = Transform3D.Identity;
        }
    });

    /// <summary>Épave de véhicule blindé à huit roues : caisse à pans coupés, roues sur jantes, rampe arrière tombée.</summary>
    public static ArrayMesh ApcWreck() => CachedKind("desapcw", PropKind.Wreck, k =>
    {
        var ch = Mats.Charred();
        const float hl = 3.6f, hw = 1.4f;
        foreach (var z in new[] { 2.3f, 0.9f, -0.9f, -2.3f })
        foreach (var x in new[] { -hw, hw })
            Rim(k, new Vector3(x, 0.42f, z), 0.42f, 0.34f);
        var y0 = 0.6f;
        k.Col = BurnBase;
        k.BoxMinMax(ch, new Vector3(-hw + 0.2f, y0, -hl), new Vector3(hw - 0.2f, y0 + 1.3f, hl - 1.2f));
        k.Col = BurnRust;
        k.Frustum(ch, new Vector3(0, y0 + 1.3f, -0.6f), new Vector2(hw * 2 - 0.4f, hl * 2 - 1.2f), new Vector2(hw * 2 - 1.0f, hl * 2 - 2.2f), 0.45f);
        k.Quad(ch, new Vector3(-hw + 0.2f, y0 + 1.3f, hl - 1.2f), new Vector3(hw - 0.2f, y0 + 1.3f, hl - 1.2f), new Vector3(hw - 0.2f, y0 + 0.2f, hl), new Vector3(-hw + 0.2f, y0 + 0.2f, hl));
        foreach (var sx in new[] { -1f, 1f })
            k.Tri(ch, new Vector3(sx * (hw - 0.2f), y0 + 1.3f, hl - 1.2f), new Vector3(sx * (hw - 0.2f), y0 + 0.2f, hl), new Vector3(sx * (hw - 0.2f), y0, hl - 1.2f), new Vector3(0, y0 + 0.5f, hl - 1.2f));
        k.Col = BurnSoot;
        k.Cylinder(ch, new Vector3(0.3f, y0 + 1.75f, 0.2f), 0.75f, 0.65f, 0.5f, 12);
        k.Tube(ch, new Vector3(0.3f, y0 + 2.0f, 0.8f), new Vector3(0.4f, y0 + 1.85f, 3.2f), 0.06f, 6);
        // Rampe arrière tombée au sol.
        k.Col = BurnRust * 0.8f;
        k.Quad(ch, new Vector3(-0.8f, y0 + 0.1f, -hl), new Vector3(0.8f, y0 + 0.1f, -hl), new Vector3(0.8f, 0.05f, -hl - 1.6f), new Vector3(-0.8f, 0.05f, -hl - 1.6f));
        k.Quad(ch, new Vector3(-0.8f, 0.05f, -hl - 1.6f), new Vector3(0.8f, 0.05f, -hl - 1.6f), new Vector3(0.8f, y0 + 0.1f, -hl), new Vector3(-0.8f, y0 + 0.1f, -hl));
    });

    // ───────────────────────── Soute à munitions ─────────────────────────

    /// <summary>Soute à munitions enterrée : demi-largeur de la butte, longueur (avant en z = +BunkerFront).</summary>
    public const float BunkerHalfW = 13f, BunkerFront = 8f, BunkerBack = -13f, BunkerCrest = 6.8f;

    /// <summary>Hauteur de la butte de terre d'une soute (intacte ou effondrée) au point (x, z) de son repère.</summary>
    private static float BunkerMound(float x, float z, bool wreck)
    {
        var ax = Mathf.Abs(x);
        // Profil en travers : arrondi sur la voûte (|x| < 5,5), talus à ~38° au-delà (praticable).
        var across = ax < 5.5f ? BunkerCrest - 0.9f * (ax / 5.5f) * (ax / 5.5f) : Mathf.Max(0f, BunkerCrest - 0.9f - (ax - 5.5f) * 0.78f);
        // Talus arrière.
        var back = Mathf.Clamp((z - BunkerBack) / 6.5f, 0f, 1f);
        back = back * back * (3 - 2 * back);
        var h = across * back;
        if (wreck)
        {
            // Voûte effondrée : cratère au centre, butte éventrée.
            var r = new Vector2(x, (z + 1f) * 0.7f).Length();
            h = h * 0.62f - Mathf.Max(0f, 3.2f - r * 0.55f) * 1.1f;
            h += (H((int)(x * 2), (int)(z * 2), 77) - 0.5f) * 0.25f;
            h = Mathf.Max(h, -1.2f);
        }
        return h;
    }

    /// <summary>
    /// Soute à munitions en igloo : butte de terre sur voûte (praticable, escaladable), mur de tête en béton dont le
    /// couronnement suit la butte, portes d'acier, bouche d'aération, paratonnerre. Citerne explosive (voir
    /// <see cref="Destructibles.AddTank"/>) : l'épave est la butte effondrée.
    /// </summary>
    public static ArrayMesh AmmoBunker(bool wreck) => Cached(wreck ? "desbunkerw" : "desbunker", k => BuildBunker(k, wreck));

    private static void BuildBunker(MeshKit k, bool wreck)
    {
        var sand = Mats.DesertSand();
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        // Butte : grille régulière, normales lissées.
        const int nx = 22, nz = 18;
        var pts = new Vector3[(nx + 1) * (nz + 1)];
        for (var j = 0; j <= nz; j++)
        for (var i = 0; i <= nx; i++)
        {
            var x = -BunkerHalfW + i * (BunkerHalfW * 2 / nx);
            var z = BunkerBack + j * ((BunkerFront - BunkerBack) / nz);
            pts[j * (nx + 1) + i] = new Vector3(x, BunkerMound(x, z, wreck), z);
        }
        Vector3 Nrm(int i, int j)
        {
            var l = pts[j * (nx + 1) + Mathf.Max(i - 1, 0)];
            var r = pts[j * (nx + 1) + Mathf.Min(i + 1, nx)];
            var b = pts[Mathf.Max(j - 1, 0) * (nx + 1) + i];
            var f = pts[Mathf.Min(j + 1, nz) * (nx + 1) + i];
            var n = (f - b).Cross(r - l).Normalized();
            return n.Y < 0 ? -n : n;
        }
        for (var j = 0; j < nz; j++)
        for (var i = 0; i < nx; i++)
        {
            var a = j * (nx + 1) + i;
            var b = a + 1;
            var e = a + nx + 2;
            var f = a + nx + 1;
            var ca = new Color(0.9f, 0.88f, 0.84f) * (0.92f + H(i, j, 31) * 0.1f);
            k.TriSmooth(sand, pts[a], pts[b], pts[e], Nrm(i, j), Nrm(i + 1, j), Nrm(i + 1, j + 1), ca, ca, ca);
            k.TriSmooth(sand, pts[a], pts[e], pts[f], Nrm(i, j), Nrm(i + 1, j + 1), Nrm(i, j + 1), ca, ca, ca);
        }
        // Mur de tête : couronnement qui suit le profil de la butte (+ 0,4 m), épaisseur 0,7 m.
        var concrete = new Color(0.5f, 0.48f, 0.43f);
        const int seg = 22;
        for (var i = 0; i < seg; i++)
        {
            var x0 = -BunkerHalfW + i * (BunkerHalfW * 2 / seg);
            var x1 = x0 + BunkerHalfW * 2 / seg;
            var top0 = BunkerMound(x0, BunkerFront, false) + 0.4f;
            var top1 = BunkerMound(x1, BunkerFront, false) + 0.4f;
            if (wreck)
            {
                // Mur fracturé : couronnement déchiqueté, plus bas au centre.
                var cut = 1.6f + H(i, 3, 41) * 2.2f + Mathf.Abs(x0) * 0.18f;
                top0 = Mathf.Min(top0, cut);
                top1 = Mathf.Min(top1, cut + (H(i, 4, 41) - 0.5f) * 0.8f);
            }
            if (top0 < 0.15f && top1 < 0.15f) continue;
            top0 = Mathf.Max(top0, 0.15f);
            top1 = Mathf.Max(top1, 0.15f);
            k.Col = concrete * (0.93f + H(i, 1, 42) * 0.1f);
            var zf = BunkerFront + 0.7f;
            k.OrientedQuad(d, new Vector3(x0, 0, zf), new Vector3(x1, 0, zf), new Vector3(x1, top1, zf), new Vector3(x0, top0, zf), Vector3.Back);
            k.OrientedQuad(d, new Vector3(x0, 0, BunkerFront), new Vector3(x1, 0, BunkerFront), new Vector3(x1, top1, BunkerFront), new Vector3(x0, top0, BunkerFront), Vector3.Forward);
            k.OrientedQuad(d, new Vector3(x0, top0, BunkerFront), new Vector3(x1, top1, BunkerFront), new Vector3(x1, top1, zf), new Vector3(x0, top0, zf), Vector3.Up);
            if (i == 0) k.OrientedQuad(d, new Vector3(x0, 0, BunkerFront), new Vector3(x0, 0, zf), new Vector3(x0, top0, zf), new Vector3(x0, top0, BunkerFront), Vector3.Left);
            if (i == seg - 1) k.OrientedQuad(d, new Vector3(x1, 0, BunkerFront), new Vector3(x1, 0, zf), new Vector3(x1, top1, zf), new Vector3(x1, top1, BunkerFront), Vector3.Right);
        }
        var zd = BunkerFront + 0.72f;
        if (!wreck)
        {
            // Portes d'acier à deux battants, linteau, numéro au pochoir, auvent.
            k.Col = new Color(0.3f, 0.31f, 0.28f);
            k.Box(m, new Vector3(-1.1f, 2.0f, zd), new Vector3(2.15f, 4f, 0.08f));
            k.Box(m, new Vector3(1.1f, 2.0f, zd), new Vector3(2.15f, 4f, 0.08f));
            k.Col = new Color(0.1f, 0.1f, 0.1f);
            k.Box(m, new Vector3(0, 2.0f, zd + 0.05f), new Vector3(0.05f, 4f, 0.02f));
            k.Col = concrete * 0.85f;
            k.Box(d, new Vector3(0, 4.35f, zd + 0.3f), new Vector3(5.4f, 0.3f, 0.7f));
            k.Solid = false;
            k.Col = new Color(0.12f, 0.11f, 0.1f);
            k.Box(d, new Vector3(-3.4f, 3.2f, zd), new Vector3(0.9f, 0.6f, 0.01f));
            k.Col = new Color(0.4f, 0.3f, 0.1f);
            for (var i = 0; i < 6; i++) k.Box(d, new Vector3(-2.6f + i * 1.04f, 0.2f, zd + 0.01f), new Vector3(0.5f, 0.4f, 0.01f), Rot(0, 0, 0.6f));
            k.Solid = true;
            // Aération et paratonnerre sur la butte.
            k.Col = new Color(0.25f, 0.25f, 0.23f);
            k.Cylinder(m, new Vector3(0, BunkerCrest - 0.2f, -3f), 0.35f, 0.35f, 1.4f, 10);
            k.Cylinder(m, new Vector3(0, BunkerCrest + 1.2f, -3f), 0.55f, 0.55f, 0.2f, 10);
            k.Solid = false;
            k.Tube(m, new Vector3(0, BunkerCrest - 0.3f, 2f), new Vector3(0, BunkerCrest + 5f, 2f), 0.04f, 4);
            k.Solid = true;
        }
        else
        {
            // Battants arrachés au sol, blocs de béton.
            k.Col = BurnSoot;
            k.Box(Mats.Charred(), new Vector3(-2.2f, 0.12f, zd + 3.2f), new Vector3(2.1f, 0.08f, 4f), Rot(0.4f, 0, 0.05f));
            k.Box(Mats.Charred(), new Vector3(3.6f, 0.4f, zd + 5f), new Vector3(2.1f, 0.08f, 4f), Rot(-0.9f, 0.3f, 0.2f));
            for (var i = 0; i < 9; i++)
            {
                k.Col = concrete * (0.6f + H(i, 2, 43) * 0.3f);
                var p = new Vector3((H(i, 5, 43) - 0.5f) * 14f, 0.3f, zd + 1f + H(i, 6, 43) * 7f);
                k.Box(d, p, new Vector3(0.6f + H(i, 7, 43), 0.5f + H(i, 8, 43) * 0.5f, 0.6f + H(i, 9, 43)), Rot(H(i, 10, 43) * 3f, 0.2f, 0.1f));
            }
        }
    }

    // ───────────────────────── Épaves d'aéronefs (décor fixe, géométrie dans un assemblage) ─────────────────────────

    /// <summary>Plaque d'aile ou d'empennage (dessus, dessous, chant) entre bord d'attaque et bord de fuite.</summary>
    private static void Plate(MeshKit k, Material m, Vector3 rootLe, Vector3 rootTe, Vector3 tipLe, Vector3 tipTe, float thick, Vector3 up)
    {
        var t = up.Normalized() * (thick / 2);
        k.OrientedQuad(m, rootLe + t, tipLe + t, tipTe + t, rootTe + t, up);
        k.OrientedQuad(m, rootLe - t, tipLe - t, tipTe - t, rootTe - t, -up);
        var c = (rootLe + rootTe + tipLe + tipTe) / 4;
        void Edge(Vector3 a, Vector3 b) => k.OrientedQuad(m, a - t, b - t, b + t, a + t, ((a + b) / 2 - c).Normalized());
        Edge(rootLe, tipLe);
        Edge(tipLe, tipTe);
        Edge(tipTe, rootTe);
    }

    /// <summary>
    /// Épave de chasseur (≈ 16 m) : fuselage noirci, verrière brisée, aile gauche arrachée posée à côté, dérive
    /// tordue, tuyère. Dans le repère de <paramref name="k"/> (nez vers +Z, au sol).
    /// </summary>
    public static void FighterWreck(MeshKit k)
    {
        var ch = Mats.Charred();
        var paint = Mats.Worn(new Color(0.4f, 0.41f, 0.41f), 0.6f, 0.3f);
        var along = new Basis(Vector3.Right, Mathf.Pi / 2);
        const float cy = 1.35f;
        // Fuselage (posé sur le ventre, train écrasé).
        k.Col = Colors.White;
        k.Cylinder(paint, new Vector3(0, cy, -6.5f), 0.95f, 0.95f, 9.5f, 14, false, along);
        k.Col = BurnSoot;
        k.Cylinder(ch, new Vector3(0, cy - 0.05f, -1.5f), 1.0f, 1.0f, 3.2f, 14, false, along);
        k.Col = Colors.White;
        k.Cylinder(paint, new Vector3(0, cy, 3f), 0.95f, 0.15f, 4.2f, 14, true, along);
        k.Col = BurnSoot;
        k.Cylinder(ch, new Vector3(0, cy, -8f), 0.75f, 0.9f, 1.5f, 12, false, along);
        // Entrées d'air.
        k.Col = new Color(0.3f, 0.31f, 0.31f);
        foreach (var sx in new[] { -1f, 1f })
            k.Box(paint, new Vector3(sx * 1.05f, cy - 0.15f, -0.5f), new Vector3(0.6f, 1.0f, 3.2f));
        // Verrière brisée (montants).
        k.Col = BurnSoot;
        k.Tube(ch, new Vector3(-0.45f, cy + 0.7f, 2.4f), new Vector3(0, cy + 1.25f, 1.2f), 0.04f, 4);
        k.Tube(ch, new Vector3(0.45f, cy + 0.7f, 2.4f), new Vector3(0, cy + 1.25f, 1.2f), 0.04f, 4);
        k.Tube(ch, new Vector3(0, cy + 1.25f, 1.2f), new Vector3(0, cy + 1.0f, -0.4f), 0.04f, 4);
        // Aile droite en place, aile gauche arrachée et posée au sol.
        k.Col = Colors.White;
        Plate(k, paint, new Vector3(0.9f, cy - 0.2f, 1.5f), new Vector3(0.9f, cy - 0.2f, -5.5f), new Vector3(5.2f, cy - 0.45f, -3.6f), new Vector3(5.2f, cy - 0.45f, -5.9f), 0.18f, Vector3.Up);
        k.Col = BurnRust;
        Plate(k, ch, new Vector3(-3.0f, 0.12f, 3.5f), new Vector3(-2.2f, 0.12f, -3.0f), new Vector3(-7.2f, 0.4f, -0.6f), new Vector3(-6.9f, 0.4f, -2.8f), 0.18f, Vector3.Up);
        // Dérive tordue, empennages.
        k.Col = Colors.White;
        Plate(k, paint, new Vector3(0, cy + 0.9f, -4.5f), new Vector3(0, cy + 0.9f, -7.6f), new Vector3(0.35f, cy + 3.8f, -7.3f), new Vector3(0.45f, cy + 3.7f, -8.4f), 0.14f, Vector3.Right);
        Plate(k, paint, new Vector3(1f, cy - 0.1f, -6.2f), new Vector3(1f, cy - 0.1f, -8.2f), new Vector3(3.2f, cy - 0.3f, -7.9f), new Vector3(3.2f, cy - 0.3f, -8.6f), 0.12f, Vector3.Up);
        k.Col = BurnSoot;
        Plate(k, ch, new Vector3(-1f, cy - 0.1f, -6.2f), new Vector3(-1f, cy - 0.1f, -8.2f), new Vector3(-2.6f, cy - 0.9f, -7.9f), new Vector3(-2.6f, cy - 0.9f, -8.6f), 0.12f, Vector3.Up);
    }

    /// <summary>
    /// Épave d'avion de transport quadrimoteur (≈ 30 m, envergure 40 m) cassée en deux : avant et voilure posés, queue
    /// basculée sur le côté, moteur arraché, ailes affaissées, partie centrale calcinée. Nez vers +Z.
    /// </summary>
    public static void TransportWreck(MeshKit k)
    {
        var ch = Mats.Charred();
        var paint = Mats.Worn(new Color(0.42f, 0.42f, 0.38f), 0.6f, 0.25f);
        var along = new Basis(Vector3.Right, Mathf.Pi / 2);
        var keep = k.Xf;
        const float r = 2.15f, cy = 2.0f;
        // Avant : fuselage, nez arrondi, poste de pilotage.
        k.Col = Colors.White;
        k.Cylinder(paint, new Vector3(0, cy, -1f), r, r, 10f, 18, false, along);
        SpherePart(k, paint, new Vector3(0, cy, 9f), new Vector3(r, r * 0.95f, 2.8f), 18, 6, 0, Mathf.Pi / 2, along);
        k.Col = new Color(0.05f, 0.05f, 0.05f);
        k.Box(ch, new Vector3(0, cy + 1.25f, 10.4f), new Vector3(2.4f, 0.5f, 0.6f), Rot(0, -0.6f));
        // Ventre écrasé, carénages de train.
        k.Col = new Color(0.36f, 0.36f, 0.33f);
        foreach (var sx in new[] { -1f, 1f })
            k.Box(paint, new Vector3(sx * (r + 0.2f), 0.9f, 0f), new Vector3(0.9f, 1.4f, 5f));
        // Partie centrale calcinée, ouverte sur le dessus.
        k.Col = BurnSoot;
        k.Cylinder(ch, new Vector3(0, cy - 0.1f, -5.5f), r * 0.98f, r * 0.98f, 4.5f, 18, false, along);
        // Voilure haute (posée sur le fuselage), aile gauche affaissée jusqu'au sol.
        var wy = cy + r + 0.1f;
        k.Col = Colors.White;
        Plate(k, paint, new Vector3(0, wy, 1.5f), new Vector3(0, wy, -3.5f), new Vector3(19.5f, wy - 0.4f, -0.8f), new Vector3(19.5f, wy - 0.4f, -3f), 0.55f, Vector3.Up);
        k.Col = BurnRust;
        Plate(k, ch, new Vector3(0, wy, 1.5f), new Vector3(0, wy, -3.5f), new Vector3(-11f, wy - 1.6f, -0.3f), new Vector3(-11f, wy - 1.6f, -3.2f), 0.55f, Vector3.Up);
        k.Col = Colors.White;
        Plate(k, paint, new Vector3(-11f, wy - 1.6f, -0.3f), new Vector3(-11f, wy - 1.6f, -3.2f), new Vector3(-18.5f, 0.3f, -1.6f), new Vector3(-18.5f, 0.3f, -3.4f), 0.45f, Vector3.Up);
        // Fuseaux moteurs et hélices (une manquante).
        foreach (var (x, ok) in new[] { (5.2f, true), (10.5f, true), (-5.2f, false), (-10.2f, true) })
        {
            var y = x > 0 ? wy - 0.9f - x * 0.02f : wy - 1f + x * 0.12f;
            k.Col = ok ? new Color(0.38f, 0.38f, 0.35f) : BurnSoot;
            k.Cylinder(ok ? paint : ch, new Vector3(x, y, -3.5f), 0.75f, 0.65f, 5.5f, 12, true, along);
            if (!ok) continue;
            k.Solid = false;
            k.Col = new Color(0.1f, 0.1f, 0.1f);
            for (var b = 0; b < 4; b++)
            {
                var a = b * Mathf.Pi / 2 + x;
                var tip = new Vector3(x + Mathf.Cos(a) * 2f, y + Mathf.Sin(a) * 2f * (b == 3 ? 0.4f : 1f), 2.1f + (b == 1 ? -0.5f : 0f));
                k.Tube(ch, new Vector3(x, y, 2.05f), tip, 0.12f, 4);
            }
            k.Solid = true;
        }
        // Moteur arraché, sur le sable.
        k.Col = BurnSoot;
        k.Cylinder(ch, new Vector3(-7.5f, 0.7f, 8f), 0.75f, 0.65f, 4.5f, 12, true, new Basis(Vector3.Up, 0.7f) * along);
        // Queue : tronçon arrière tombé et basculé sur le côté.
        k.Xf = keep * new Transform3D(Rot(0.25f, -0.12f, 0.35f), new Vector3(1.6f, 0f, -9.5f));
        k.Col = Colors.White;
        k.Cylinder(paint, new Vector3(0, cy, -9f), r * 0.55f, r, 9f, 16, false, along);
        k.Col = new Color(0.03f, 0.03f, 0.03f);
        k.Cylinder(ch, new Vector3(0, cy, -0.05f), r * 0.97f, r * 0.97f, 0.06f, 16, true, along);
        k.Col = Colors.White;
        Plate(k, paint, new Vector3(0, cy + 0.8f, -5f), new Vector3(0, cy + 0.6f, -9f), new Vector3(0, cy + 9f, -8.5f), new Vector3(0, cy + 9f, -11f), 0.4f, Vector3.Right);
        Plate(k, paint, new Vector3(0, cy + 0.6f, -7f), new Vector3(0, cy + 0.6f, -9.5f), new Vector3(7f, cy + 0.5f, -8.8f), new Vector3(7f, cy + 0.5f, -10.2f), 0.3f, Vector3.Up);
        Plate(k, paint, new Vector3(0, cy + 0.6f, -7f), new Vector3(0, cy + 0.6f, -9.5f), new Vector3(-7f, cy + 0.5f, -8.8f), new Vector3(-7f, cy + 0.5f, -10.2f), 0.3f, Vector3.Up);
        k.Xf = keep;
    }

    /// <summary>Épave d'hélicoptère de transport couchée : cellule calcinée, poutre de queue brisée, pales tordues au sol.</summary>
    public static void HeliWreck(MeshKit k)
    {
        var ch = Mats.Charred();
        var paint = Mats.Worn(new Color(0.3f, 0.32f, 0.27f), 0.6f, 0.25f);
        var along = new Basis(Vector3.Right, Mathf.Pi / 2);
        var keep = k.Xf;
        k.Xf = keep * new Transform3D(Rot(0, 0, 0.5f), new Vector3(0, 0.1f, 0));
        k.Col = Colors.White;
        k.Cylinder(paint, new Vector3(0, 1.6f, -3f), 1.5f, 1.5f, 6f, 14, false, along);
        SpherePart(k, paint, new Vector3(0, 1.6f, 3f), new Vector3(1.5f, 1.5f, 1.6f), 14, 5, 0, Mathf.Pi / 2, along);
        k.Col = BurnSoot;
        k.Cylinder(ch, new Vector3(0, 1.6f, -1.5f), 1.52f, 1.52f, 2.2f, 14, false, along);
        k.Col = new Color(0.06f, 0.06f, 0.06f);
        k.Box(ch, new Vector3(0, 2.3f, 3.6f), new Vector3(1.8f, 0.7f, 0.6f), Rot(0, -0.5f));
        k.Col = Colors.White;
        k.Box(paint, new Vector3(0, 3.3f, -0.5f), new Vector3(1.4f, 0.7f, 3f));
        k.Cylinder(paint, new Vector3(0, 2.2f, -3f), 0.45f, 0.3f, 4.5f, 10, false, new Basis(Vector3.Right, Mathf.Pi / 2 + 0.25f));
        k.Xf = keep;
        // Queue arrachée plus loin, pales au sol.
        k.Col = BurnRust;
        k.Cylinder(ch, new Vector3(2.5f, 0.35f, -9f), 0.32f, 0.2f, 5f, 10, false, new Basis(Vector3.Up, 2.6f) * along);
        k.Col = BurnSoot;
        Plate(k, ch, new Vector3(3.6f, 0.2f, -12.6f), new Vector3(3.6f, 0.2f, -13.4f), new Vector3(3.6f, 2.0f, -13f), new Vector3(3.6f, 2.0f, -13.8f), 0.1f, Vector3.Right);
        for (var b = 0; b < 3; b++)
        {
            var a = 0.4f + b * 2.1f;
            var start = new Vector3(Mathf.Cos(a) * 2f, 0.12f, Mathf.Sin(a) * 2f - 1f);
            var mid = start + new Vector3(Mathf.Cos(a) * 3f, 0.25f, Mathf.Sin(a) * 3f);
            var end = mid + new Vector3(Mathf.Cos(a + 0.6f) * 2.5f, -0.2f, Mathf.Sin(a + 0.6f) * 2.5f);
            k.Box(ch, (start + mid) / 2, new Vector3(0.5f, 0.06f, 3.1f), new Basis(Vector3.Up, Mathf.Atan2(mid.X - start.X, mid.Z - start.Z)));
            k.Box(ch, (mid + end) / 2 + Vector3.Up * 0.05f, new Vector3(0.5f, 0.06f, 2.6f), new Basis(Vector3.Up, Mathf.Atan2(end.X - mid.X, end.Z - mid.Z)));
        }
    }

    // ───────────────────────── Petit matériel ─────────────────────────

    /// <summary>Pile de caisses de munitions (bois et métal vert sable), palette ; variantes 0 à 2.</summary>
    public static ArrayMesh AmmoCrates(int v) => CachedKind($"desammo{v}", PropKind.Small, k =>
    {
        var d = Mats.Detail();
        var wood = Mats.Planks();
        k.Col = new Color(0.36f, 0.3f, 0.22f);
        k.Box(wood, new Vector3(0, 0.07f, 0), new Vector3(1.2f, 0.14f, 1.0f));
        var n = 3 + v * 2;
        for (var i = 0; i < n; i++)
        {
            var layer = i / 3;
            var x = (i % 3 - 1) * 0.4f;
            k.Col = (H(v, i, 51) < 0.5f ? new Color(0.3f, 0.31f, 0.22f) : new Color(0.42f, 0.37f, 0.27f)) * (0.85f + H(v, i, 52) * 0.2f);
            k.Box(d, new Vector3(x, 0.14f + 0.16f + layer * 0.32f, (H(v, i, 53) - 0.5f) * 0.1f), new Vector3(0.38f, 0.32f, 0.9f), Rot((H(v, i, 54) - 0.5f) * 0.1f));
        }
    });

    /// <summary>Balise de bord de piste (feu éteint sur pied frangible).</summary>
    public static ArrayMesh RunwayLight() => CachedKind("desrwlight", PropKind.Small, k =>
    {
        var m = Mats.DetailMetal();
        k.Col = new Color(0.4f, 0.38f, 0.3f);
        k.Cylinder(m, Vector3.Zero, 0.06f, 0.05f, 0.35f, 6);
        k.Col = new Color(0.3f, 0.27f, 0.16f);
        k.Cylinder(m, new Vector3(0, 0.35f, 0), 0.11f, 0.09f, 0.12f, 8);
    });

    /// <summary>
    /// Filet de camouflage tendu sur perches (≈ 9 × 7 m) au-dessus d'un poste ou d'un véhicule : seuls les perches et
    /// la bordure sont solides, le filet est traversable (pas de forme).
    /// </summary>
    public static void CamoCanopy(MeshKit k, Vector3 c, float yaw, float hx, float hz, float h, int seed)
    {
        var m = Mats.DetailMetal();
        var net = Mats.CamoNet();
        var b = new Basis(Vector3.Up, yaw);
        Vector3 P(float x, float y, float z) => c + b * new Vector3(x, y, z);
        k.Col = new Color(0.3f, 0.27f, 0.2f);
        foreach (var sx in new[] { -1f, 1f })
        foreach (var sz in new[] { -1f, 1f })
            k.Tube(m, P(sx * hx, 0, sz * hz), P(sx * hx * 0.97f, h, sz * hz * 0.97f), 0.05f, 5);
        k.Tube(m, P(0, 0, 0), P(0, h + 0.8f, 0), 0.06f, 5);
        var keep = k.Solid;
        k.Solid = false;
        k.Col = Colors.White;
        // Filet affaissé entre les perches (quatre pans vers le mât central), quelques lambeaux qui pendent.
        var top = P(0, h + 0.8f, 0);
        Vector3 Sag(float x, float z) => P(x, h - 0.35f - 0.25f * H(seed, (int)(x * 3), (int)(z * 3)), z);
        var corners = new[] { Sag(-hx, -hz), Sag(hx, -hz), Sag(hx, hz), Sag(-hx, hz) };
        for (var i = 0; i < 4; i++)
        {
            var a = corners[i];
            var bb = corners[(i + 1) % 4];
            var mid = (a + bb) / 2 + Vector3.Down * 0.5f;
            k.Tri(net, top, a, mid, top + Vector3.Up);
            k.Tri(net, top, mid, bb, top + Vector3.Up);
            k.Quad(net, a, mid, mid + Vector3.Down * 1.4f, a + Vector3.Down * 1.1f);
        }
        k.Solid = keep;
    }
}
