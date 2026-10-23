using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Décor de l'invasion : épaves calcinées (voitures, fourgons, camions, bus, conteneurs, bateau), matériel
/// militaire abandonné (camion, sacs de sable, séparateurs, barbelés, hérissons), gravats et débris, arbres
/// morts, mobilier renversé, résine et œufs d'insectes. Convention : avant vers +Z, posé sur y = 0.
/// </summary>
public static partial class Props
{
    // Teintes de tôle brûlée (multipliées par la texture Mats.Charred).
    private static readonly Color BurnBase = new(0.36f, 0.3f, 0.26f);
    private static readonly Color BurnRust = new(0.42f, 0.32f, 0.26f);
    private static readonly Color BurnSoot = new(0.14f, 0.13f, 0.12f);
    private static readonly Color BurnAsh = new(0.55f, 0.53f, 0.5f);
    private static readonly Color Olive = new(0.2f, 0.22f, 0.14f);
    private static readonly Color Khaki = new(0.42f, 0.38f, 0.27f);

    private static float H(int a, int b, int c)
    {
        unchecked
        {
            var h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    private static Basis Rot(float yaw, float pitch = 0, float roll = 0) =>
        new Basis(Vector3.Up, yaw) * new Basis(Vector3.Right, pitch) * new Basis(Vector3.Back, roll);

    /// <summary>Jante nue (pneu brûlé) : disque d'acier noirci, axe selon X.</summary>
    private static void Rim(MeshKit k, Vector3 c, float r, float w)
    {
        var rot = new Basis(Vector3.Back, -Mathf.Pi / 2);
        k.Col = BurnSoot;
        k.Cylinder(Mats.Charred(), c - Vector3.Right * (w / 2), r, r, w, 10, true, rot);
        k.Col = BurnRust;
        k.Cylinder(Mats.Charred(), c - Vector3.Right * (w / 2 + 0.01f), r * 0.55f, r * 0.55f, w + 0.02f, 8, true, rot);
    }

    /// <summary>Roue complète (pneu, jante) pour les véhicules abandonnés intacts.</summary>
    private static void Tyre(MeshKit k, Vector3 c, float r, float w, Color hub, bool flat = false)
    {
        var rot = new Basis(Vector3.Back, -Mathf.Pi / 2);
        k.Col = new Color(0.05f, 0.05f, 0.05f);
        var rr = flat ? r * 0.92f : r;
        k.Cylinder(Mats.Detail(), c - Vector3.Right * (w / 2) + (flat ? Vector3.Down * (r - rr) : Vector3.Zero), rr, rr, w, 12, true, rot);
        k.Col = hub;
        k.Cylinder(Mats.Detail(), c - Vector3.Right * (w / 2 + 0.01f), r * 0.55f, r * 0.55f, w + 0.02f, 8, true, rot);
    }

    // ───────────────────────── Épaves ─────────────────────────

    /// <summary>
    /// Voiture calcinée (variantes 0 à 3) : caisse rouillée et noircie posée sur ses jantes, sans vitres,
    /// pavillon affaissé, intérieur brûlé (carcasses de sièges), capot ou coffre entrouverts.
    /// </summary>
    public static ArrayMesh WreckCar(int v) => Cached($"wreckcar{v}", k =>
    {
        var ch = Mats.Charred();
        var l = v switch { 1 => 4.1f, 2 => 4.7f, 3 => 4.35f, _ => 4.45f };
        var drop = 0.11f;   // la caisse s'est affaissée sur les jantes
        var wz = l * 0.33f;
        foreach (var z in new[] { wz, -wz })
        foreach (var x in new[] { -0.8f, 0.8f })
        {
            if (v == 3 && z > 0 && x < 0) continue;   // roue arrachée
            Rim(k, new Vector3(x, 0.21f, z), 0.21f, 0.16f);
        }
        var y0 = 0.3f - drop;
        // Bas de caisse, ailes et portières : teintes inégales (rouille, suie, cendre).
        k.Col = BurnBase;
        k.Frustum(ch, new Vector3(0, y0, 0), new Vector2(1.8f, l), new Vector2(1.77f, l - 0.12f), 0.48f);
        k.Col = BurnRust;
        k.Box(ch, new Vector3(-0.89f, y0 + 0.28f, -0.1f), new Vector3(0.04f, 0.36f, l * 0.48f));
        k.Col = BurnSoot;
        k.Box(ch, new Vector3(0.89f, y0 + 0.3f, 0.2f), new Vector3(0.04f, 0.3f, l * 0.35f));
        // Joints de portières béants.
        k.Col = new Color(0.03f, 0.03f, 0.03f);
        foreach (var x in new[] { -0.905f, 0.905f })
        {
            k.Box(ch, new Vector3(x, y0 + 0.28f, l * 0.13f), new Vector3(0.02f, 0.42f, 0.03f));
            k.Box(ch, new Vector3(x, y0 + 0.28f, -l * 0.12f), new Vector3(0.02f, 0.42f, 0.03f));
        }
        // Capot : gondolé (v0, v2), soulevé (v1).
        var hoodPivot = new Vector3(0, y0 + 0.5f, l * 0.16f);
        var hoodLen = l * 0.34f;
        k.Col = v == 2 ? BurnAsh : BurnRust;
        if (v == 1)
            k.Box(ch, hoodPivot + Rot(0, Mathf.DegToRad(-28)) * new Vector3(0, 0.02f, hoodLen / 2), new Vector3(1.66f, 0.04f, hoodLen), Rot(0, Mathf.DegToRad(-28)));
        else
        {
            k.Box(ch, hoodPivot + new Vector3(0, 0.03f, hoodLen * 0.25f), new Vector3(1.7f, 0.05f, hoodLen * 0.5f), Rot(0, Mathf.DegToRad(-6), 0.03f));
            k.Box(ch, hoodPivot + new Vector3(0, 0.0f, hoodLen * 0.75f), new Vector3(1.7f, 0.05f, hoodLen * 0.5f), Rot(0, Mathf.DegToRad(5), -0.02f));
        }
        // Compartiment moteur sombre visible.
        k.Col = BurnSoot;
        k.Box(ch, new Vector3(0, y0 + 0.4f, l * 0.33f), new Vector3(1.4f, 0.2f, l * 0.28f));
        // Coffre.
        k.Col = BurnBase;
        var trunkRot = v == 2 ? Rot(0, Mathf.DegToRad(35)) : Rot(0, Mathf.DegToRad(4));
        var trunkPivot = new Vector3(0, y0 + 0.55f, -l * 0.3f);
        k.Box(ch, trunkPivot + trunkRot * new Vector3(0, 0, -l * 0.09f), new Vector3(1.66f, 0.05f, l * 0.18f), trunkRot);

        // Habitacle sans vitres : montants, pavillon affaissé.
        var roofY = v == 2 ? 1.12f : 1.27f;
        var zf = l * 0.1f;   // pied de pare-brise
        var zr = -l * 0.33f; // pied de lunette
        k.Col = BurnSoot;
        foreach (var x in new[] { -0.78f, 0.78f })
        {
            var xi = x * 0.86f;
            k.Tube(ch, new Vector3(x, y0 + 0.5f, zf), new Vector3(xi, roofY, zf - l * 0.12f), 0.045f, 5);    // montant A
            k.Tube(ch, new Vector3(x, y0 + 0.5f, -l * 0.1f), new Vector3(xi, roofY, -l * 0.1f), 0.05f, 5);  // montant B
            k.Tube(ch, new Vector3(x, y0 + 0.5f, zr), new Vector3(xi, roofY, zr + l * 0.08f), 0.06f, 5);    // montant C
            k.Tube(ch, new Vector3(xi, roofY, zf - l * 0.12f), new Vector3(xi, roofY, zr + l * 0.08f), 0.04f, 5);
        }
        k.Col = BurnRust;
        var roofLen = (zf - l * 0.12f) - (zr + l * 0.08f);
        var roofMid = ((zf - l * 0.12f) + (zr + l * 0.08f)) / 2;
        k.Box(ch, new Vector3(0, roofY - 0.03f, roofMid + roofLen * 0.25f), new Vector3(1.32f, 0.04f, roofLen * 0.52f), Rot(0, Mathf.DegToRad(-5)));
        k.Box(ch, new Vector3(0, roofY - 0.03f, roofMid - roofLen * 0.25f), new Vector3(1.32f, 0.04f, roofLen * 0.52f), Rot(0, Mathf.DegToRad(5)));
        // Intérieur : plancher cendreux, carcasses de sièges (ressorts), volant.
        k.Col = BurnAsh * 0.55f;
        k.Box(ch, new Vector3(0, y0 + 0.36f, -l * 0.08f), new Vector3(1.6f, 0.04f, l * 0.48f));
        k.Col = BurnSoot;
        foreach (var x in new[] { -0.4f, 0.4f })
        {
            k.Box(ch, new Vector3(x, y0 + 0.52f, -l * 0.02f), new Vector3(0.48f, 0.05f, 0.48f));
            k.Box(ch, new Vector3(x, y0 + 0.78f, -l * 0.08f), new Vector3(0.46f, 0.55f, 0.05f), Rot(0, Mathf.DegToRad(-12)));
        }
        k.Box(ch, new Vector3(0, y0 + 0.52f, -l * 0.22f), new Vector3(1.3f, 0.05f, 0.45f));
        k.Box(ch, new Vector3(0, y0 + 0.75f, -l * 0.27f), new Vector3(1.3f, 0.5f, 0.05f), Rot(0, Mathf.DegToRad(-10)));
        k.Box(ch, new Vector3(0, y0 + 0.62f, l * 0.06f), new Vector3(1.5f, 0.18f, 0.25f)); // tableau de bord
        k.Tube(ch, new Vector3(-0.4f, y0 + 0.62f, l * 0.02f), new Vector3(-0.4f, y0 + 0.86f, -l * 0.03f), 0.18f, 8);
        // Pare-chocs : l'avant pend (v0, v3), l'arrière tient.
        k.Col = BurnSoot;
        if (v is 0 or 3) k.Box(ch, new Vector3(0.1f, 0.16f, l / 2 + 0.02f), new Vector3(1.8f, 0.18f, 0.1f), Rot(0, 0, Mathf.DegToRad(v == 0 ? 7 : -9)));
        else k.Box(ch, new Vector3(0, y0 + 0.12f, l / 2 + 0.02f), new Vector3(1.82f, 0.2f, 0.1f));
        k.Box(ch, new Vector3(0, y0 + 0.12f, -l / 2 - 0.02f), new Vector3(1.82f, 0.2f, 0.1f));
        // Optiques éclatées.
        k.Col = new Color(0.02f, 0.02f, 0.02f);
        foreach (var x in new[] { -0.62f, 0.62f })
        {
            k.Box(ch, new Vector3(x, y0 + 0.36f, l / 2 + 0.005f), new Vector3(0.36f, 0.1f, 0.02f));
            k.Box(ch, new Vector3(x, y0 + 0.4f, -l / 2 - 0.005f), new Vector3(0.32f, 0.12f, 0.02f));
        }
    });

    /// <summary>Fourgon calciné : caisse cloquée, cabine vidée, pavillon arraché à l'arrière.</summary>
    public static ArrayMesh WreckVan(int v) => Cached($"wreckvan{v}", k =>
    {
        var ch = Mats.Charred();
        const float l = 5.2f;
        foreach (var z in new[] { 1.65f, -1.65f })
        foreach (var x in new[] { -0.85f, 0.85f })
            Rim(k, new Vector3(x, 0.24f, z), 0.24f, 0.18f);
        var y0 = 0.22f;
        // Caisse (parois minces : on voit l'intérieur par l'arrière ouvert).
        var top = y0 + 1.95f;
        var zb = -l / 2;
        var zc = l / 2 - 1.0f;
        k.Col = BurnBase;
        k.BoxMinMax(ch, new Vector3(-1f, y0 + 0.2f, zb), new Vector3(-0.96f, top, zc));
        k.Col = BurnRust;
        k.BoxMinMax(ch, new Vector3(0.96f, y0 + 0.2f, zb), new Vector3(1f, top, zc));
        k.Col = BurnSoot;
        k.BoxMinMax(ch, new Vector3(-1f, y0 + 0.1f, zb), new Vector3(1f, y0 + 0.24f, zc + 0.9f)); // plancher
        k.Col = BurnAsh * 0.4f;
        k.BoxMinMax(ch, new Vector3(-0.95f, y0 + 0.24f, zb + 0.1f), new Vector3(0.95f, y0 + 0.3f, zc));
        // Pavillon : avant en place, arrière effondré (v0) ou déchiré (v1).
        k.Col = BurnBase;
        k.BoxMinMax(ch, new Vector3(-1f, top - 0.04f, zc - 1.6f), new Vector3(1f, top, zc));
        if (v == 0) k.Box(ch, new Vector3(0, top - 0.55f, zb + 0.9f), new Vector3(1.9f, 0.04f, 1.9f), Rot(0, Mathf.DegToRad(28)));
        else k.Box(ch, new Vector3(0.3f, top + 0.1f, zb + 0.6f), new Vector3(1.3f, 0.04f, 1.2f), Rot(0.2f, Mathf.DegToRad(-14), Mathf.DegToRad(8)));
        // Portes arrière : l'une ouverte à 100°.
        k.Col = BurnSoot;
        k.Box(ch, new Vector3(-0.5f, y0 + 1.05f, zb - 0.02f), new Vector3(0.95f, 1.7f, 0.04f));
        k.Box(ch, new Vector3(1.0f + 0.45f, y0 + 1.05f, zb - 0.1f), new Vector3(0.04f, 1.7f, 0.9f), Rot(0.15f));
        // Cabine : capot, montants, pas de vitres.
        k.Col = BurnRust;
        k.Frustum(ch, new Vector3(0, y0 + 0.15f, l / 2 - 0.45f), new Vector2(1.96f, 0.9f), new Vector2(1.9f, 0.8f), 0.72f, new Vector2(0, -0.04f));
        k.Col = BurnSoot;
        foreach (var x in new[] { -0.93f, 0.93f })
        {
            k.Tube(ch, new Vector3(x, y0 + 0.87f, l / 2 - 0.9f), new Vector3(x, top - 0.05f, zc - 0.2f), 0.05f, 5);
            k.Tube(ch, new Vector3(x, y0 + 0.4f, zc - 1.1f), new Vector3(x, top - 0.05f, zc - 1.1f), 0.06f, 5);
            k.BoxMinMax(ch, new Vector3(x - 0.03f, y0 + 0.2f, zc - 1.1f), new Vector3(x + 0.03f, y0 + 0.95f, zc - 0.1f));
        }
        k.Box(ch, new Vector3(0, y0 + 0.75f, zc - 0.55f), new Vector3(1.7f, 0.08f, 0.8f));
        k.Box(ch, new Vector3(0, y0 + 0.95f, l / 2 - 0.85f), new Vector3(1.8f, 0.2f, 0.3f));
        k.Col = new Color(0.03f, 0.03f, 0.03f);
        k.Box(ch, new Vector3(0, y0 + 0.3f, l / 2 + 0.02f), new Vector3(2.0f, 0.22f, 0.1f), Rot(0, 0, 0.05f));
    });

    /// <summary>Camion porteur calciné : cabine noircie, caisse dont il ne reste que l'ossature et quelques tôles.</summary>
    public static ArrayMesh WreckTruck() => Cached("wrecktruck", k =>
    {
        var ch = Mats.Charred();
        const float l = 7.4f;
        foreach (var z in new[] { 2.5f, -1.6f, -2.6f })
        foreach (var x in new[] { -1.0f, 1.0f })
            Rim(k, new Vector3(x, 0.3f, z), 0.3f, 0.22f);
        k.Col = BurnSoot;
        k.Box(ch, new Vector3(0, 0.62f, -0.3f), new Vector3(1.9f, 0.3f, l - 1.0f));
        // Cabine.
        k.Col = BurnRust;
        k.BoxMinMax(ch, new Vector3(-1.15f, 0.75f, l / 2 - 2.05f), new Vector3(1.15f, 1.75f, l / 2));
        k.Col = BurnBase;
        k.BoxMinMax(ch, new Vector3(-1.15f, 2.7f, l / 2 - 2.05f), new Vector3(1.15f, 2.78f, l / 2 - 0.15f));
        k.Col = BurnSoot;
        foreach (var x in new[] { -1.12f, 1.12f })
        {
            k.BoxMinMax(ch, new Vector3(x - 0.04f, 1.75f, l / 2 - 0.2f), new Vector3(x + 0.04f, 2.72f, l / 2 - 0.08f));
            k.BoxMinMax(ch, new Vector3(x - 0.04f, 1.75f, l / 2 - 2.05f), new Vector3(x + 0.04f, 2.72f, l / 2 - 1.9f));
        }
        k.BoxMinMax(ch, new Vector3(-1.1f, 1.6f, l / 2 - 1.9f), new Vector3(1.1f, 2.7f, l / 2 - 1.8f)); // cloison arrière
        k.Col = BurnAsh * 0.5f;
        k.Box(ch, new Vector3(0, 1.3f, l / 2 - 1.0f), new Vector3(2.1f, 0.5f, 1.6f));
        // Caisse : ossature (montants, longerons) et tôles restantes gondolées.
        var z0 = l / 2 - 2.2f;
        var z1 = -l / 2;
        k.Col = BurnSoot;
        k.BoxMinMax(ch, new Vector3(-1.22f, 0.8f, z1), new Vector3(1.22f, 0.95f, z0));
        for (var z = z1; z <= z0 + 0.01f; z += (z0 - z1) / 7f)
        foreach (var x in new[] { -1.2f, 1.2f })
            k.Box(ch, new Vector3(x, 2.3f, z), new Vector3(0.07f, 2.8f, 0.07f), Rot(0, 0, x * 0.02f));
        foreach (var x in new[] { -1.2f, 1.2f })
            k.Tube(ch, new Vector3(x, 3.68f, z1), new Vector3(x, 3.6f, z0), 0.05f, 4);
        k.Col = BurnRust;
        k.Box(ch, new Vector3(-1.23f, 1.6f, z0 - 1.2f), new Vector3(0.03f, 1.5f, 2.2f), Rot(0, 0.04f, 0.05f));
        k.Box(ch, new Vector3(1.23f, 1.4f, z1 + 1.0f), new Vector3(0.03f, 1.1f, 1.8f), Rot(0, -0.06f, -0.04f));
        k.Col = BurnBase;
        k.Box(ch, new Vector3(0.6f, 3.0f, z1 + 2.2f), new Vector3(1.3f, 0.03f, 1.6f), Rot(0.3f, 0.5f, 0.2f)); // toit effondré
        // Chargement brûlé (palettes, cartons carbonisés).
        k.Col = BurnSoot;
        for (var i = 0; i < 6; i++)
            k.Box(ch, new Vector3(-0.6f + (i % 2) * 1.1f, 1.15f + (i / 4) * 0.5f, z1 + 0.8f + (i / 2) * 1.3f), new Vector3(0.9f, 0.45f, 1f), Rot(H(i, 1, 9) * 0.6f, 0, H(i, 2, 9) * 0.2f));
    });

    /// <summary>Autobus articulé calciné : coque sans vitres, sièges carbonisés, avant enfoncé (accident).</summary>
    public static ArrayMesh WreckBus() => Cached("wreckbus", k =>
    {
        var ch = Mats.Charred();
        const float l = 11.8f, hw = 1.25f, top = 3.0f;
        foreach (var z in new[] { 3.6f, -3.0f })
        foreach (var x in new[] { -1.05f, 1.05f })
            Rim(k, new Vector3(x, 0.32f, z), 0.32f, 0.26f);
        var zf = l / 2 - 0.7f;   // avant enfoncé
        var zb = -l / 2;
        // Jupe et plancher.
        k.Col = BurnRust;
        k.BoxMinMax(ch, new Vector3(-hw, 0.35f, zb), new Vector3(hw, 1.05f, zf));
        k.Col = BurnAsh * 0.45f;
        k.BoxMinMax(ch, new Vector3(-hw + 0.05f, 1.05f, zb + 0.1f), new Vector3(hw - 0.05f, 1.1f, zf - 0.1f));
        // Montants de baies et bandeau de toit.
        k.Col = BurnSoot;
        for (var z = zb + 0.15f; z < zf; z += 1.35f)
        foreach (var x in new[] { -hw + 0.03f, hw - 0.03f })
            k.Box(ch, new Vector3(x, 2.05f, z), new Vector3(0.07f, 1.9f, 0.14f), Rot(0, 0, x * 0.01f));
        k.Col = BurnBase;
        k.BoxMinMax(ch, new Vector3(-hw, top - 0.06f, zb), new Vector3(hw, top, zb + 4.5f));
        k.Box(ch, new Vector3(0, top - 0.5f, zb + 6.4f), new Vector3(2.4f, 0.05f, 3.6f), Rot(0, Mathf.DegToRad(-14), 0.05f)); // toit effondré
        k.BoxMinMax(ch, new Vector3(-hw, top - 0.06f, zf - 2.2f), new Vector3(hw, top, zf));
        foreach (var x in new[] { -hw + 0.03f, hw - 0.03f })
            k.Tube(ch, new Vector3(x, top - 0.04f, zb), new Vector3(x, top - 0.04f, zf), 0.05f, 4);
        k.BoxMinMax(ch, new Vector3(-hw, 1.05f, zb), new Vector3(hw, top, zb + 0.06f)); // face arrière
        // Sièges carbonisés.
        k.Col = BurnSoot;
        for (var z = zb + 1.2f; z < zf - 2.2f; z += 0.85f)
        foreach (var x in new[] { -0.75f, 0.75f })
        {
            k.Box(ch, new Vector3(x, 1.5f, z), new Vector3(0.85f, 0.06f, 0.42f));
            k.Box(ch, new Vector3(x, 1.8f, z - 0.2f), new Vector3(0.85f, 0.55f, 0.05f), Rot(0, -0.15f));
        }
        // Avant enfoncé : tôles froissées repliées vers l'arrière.
        k.Col = BurnRust;
        k.Box(ch, new Vector3(-0.6f, 1.4f, zf + 0.15f), new Vector3(1.3f, 1.6f, 0.08f), Rot(0.35f, 0.1f, 0.1f));
        k.Box(ch, new Vector3(0.7f, 1.1f, zf + 0.05f), new Vector3(1.2f, 1.3f, 0.08f), Rot(-0.45f, -0.15f, -0.08f));
        k.Col = BurnSoot;
        k.Box(ch, new Vector3(0, 2.4f, zf - 0.1f), new Vector3(2.3f, 1.0f, 0.06f), Rot(0, -0.5f, 0.06f));
        k.Box(ch, new Vector3(0.2f, 0.55f, zf + 0.35f), new Vector3(2.2f, 0.3f, 0.12f), Rot(0.2f, 0, 0.1f));
    });

    /// <summary>Taille de collision d'une épave (largeur, hauteur, longueur).</summary>
    public static Vector3 WreckSize(VehicleType t, int v) => t switch
    {
        VehicleType.Van => new Vector3(2.0f, 2.1f, 5.2f),
        VehicleType.Truck => new Vector3(2.4f, 3.5f, 7.4f),
        _ => new Vector3(1.8f, v == 2 ? 1.1f : 1.25f, v switch { 1 => 4.1f, 2 => 4.7f, 3 => 4.35f, _ => 4.45f }),
    };

    /// <summary>Portière ouverte (charnière à l'avant, en x = 0) d'une voiture abandonnée.</summary>
    public static ArrayMesh CarDoor(Color paint) => Cached($"cardoor{paint}", k =>
    {
        var p = Mats.CarPaint(paint);
        var glass = Mats.Painted(new Color(0.04f, 0.05f, 0.06f), 0.05f, 0.3f);
        k.Box(p, new Vector3(0, 0.55f, -0.55f), new Vector3(0.06f, 0.5f, 1.1f));
        k.Box(glass, new Vector3(0, 1.0f, -0.6f), new Vector3(0.03f, 0.4f, 0.9f));
        k.Col = new Color(0.08f, 0.08f, 0.09f);
        k.Box(Mats.Detail(), new Vector3(-0.03f, 0.55f, -0.55f), new Vector3(0.02f, 0.45f, 1.0f));
    });

    // ───────────────────────── Militaire ─────────────────────────

    /// <summary>Camion militaire bâché abandonné (kaki terne, boue), un pneu crevé.</summary>
    public static ArrayMesh ArmyTruck() => Cached("armytruck", k =>
    {
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        var glass = Mats.Painted(new Color(0.05f, 0.06f, 0.06f), 0.1f, 0.3f);
        const float l = 7.6f;
        foreach (var z in new[] { 2.6f, -1.5f, -2.7f })
        foreach (var x in new[] { -1.05f, 1.05f })
            Tyre(k, new Vector3(x, 0.55f, z), 0.55f, 0.36f, Olive.Darkened(0.2f), z < -2f && x > 0);
        k.Col = new Color(0.08f, 0.08f, 0.07f);
        k.Box(m, new Vector3(0, 0.95f, -0.2f), new Vector3(1.6f, 0.35f, l - 0.6f));
        // Capot et cabine.
        k.Col = Olive;
        k.BoxMinMax(d, new Vector3(-1.1f, 1.0f, l / 2 - 1.4f), new Vector3(1.1f, 1.85f, l / 2));
        k.BoxMinMax(d, new Vector3(-1.2f, 1.0f, l / 2 - 3.1f), new Vector3(1.2f, 2.85f, l / 2 - 1.4f));
        k.Col = Olive.Darkened(0.15f);
        foreach (var x in new[] { -1.3f, 1.3f })
            k.Box(d, new Vector3(x, 1.4f, l / 2 - 0.6f), new Vector3(0.25f, 0.12f, 1.4f)); // ailes
        k.Box(glass, new Vector3(0, 2.35f, l / 2 - 1.38f), new Vector3(2.1f, 0.75f, 0.04f));
        k.Box(glass, new Vector3(0, 2.35f, l / 2 - 2.2f), new Vector3(2.42f, 0.6f, 0.8f));
        k.Col = new Color(0.1f, 0.1f, 0.09f);
        k.Box(m, new Vector3(0, 1.3f, l / 2 + 0.05f), new Vector3(2.0f, 0.5f, 0.12f)); // calandre / pare-buffle
        // Plateau, ridelles et bâche sur arceaux.
        var z0 = l / 2 - 3.2f;
        var z1 = -l / 2;
        k.Col = Olive.Darkened(0.1f);
        k.BoxMinMax(d, new Vector3(-1.25f, 1.15f, z1), new Vector3(1.25f, 1.25f, z0));
        foreach (var x in new[] { -1.23f, 1.23f })
            k.BoxMinMax(d, new Vector3(x - 0.03f, 1.25f, z1), new Vector3(x + 0.03f, 1.85f, z0));
        k.Col = Khaki.Darkened(0.25f);
        var mid = (z0 + z1) / 2;
        var len = z0 - z1;
        k.BoxMinMax(d, new Vector3(-1.26f, 1.85f, z1), new Vector3(-1.2f, 3.1f, z0));
        k.BoxMinMax(d, new Vector3(1.2f, 1.85f, z1), new Vector3(1.26f, 3.1f, z0));
        k.Box(d, new Vector3(-0.62f, 3.3f, mid), new Vector3(1.32f, 0.06f, len), Rot(0, 0, 0.32f));
        k.Box(d, new Vector3(0.62f, 3.3f, mid), new Vector3(1.32f, 0.06f, len), Rot(0, 0, -0.32f));
        k.BoxMinMax(d, new Vector3(-1.24f, 1.85f, z0 - 0.04f), new Vector3(1.24f, 3.15f, z0));
        // Arrière : bâche relevée (rabat roulé), caisses de munitions.
        k.Box(d, new Vector3(0, 3.0f, z1 + 0.05f), new Vector3(2.4f, 0.25f, 0.25f));
        k.Col = Olive.Darkened(0.3f);
        for (var i = 0; i < 4; i++)
            k.Box(d, new Vector3(-0.6f + (i % 2) * 1.0f, 1.45f + (i / 2) * 0.42f, z1 + 0.8f), new Vector3(0.8f, 0.4f, 0.5f));
        // Boue sur le bas de caisse.
        k.Col = new Color(0.22f, 0.18f, 0.13f);
        k.BoxMinMax(d, new Vector3(-1.27f, 0.85f, z1 + 0.2f), new Vector3(1.27f, 1.12f, z0));
    });

    /// <summary>Feu tricolore éteint (plus de courant), boîtier sali.</summary>
    public static ArrayMesh TrafficLightDead() => Cached("tldead", k =>
    {
        var m = Mats.DetailMetal();
        var d = Mats.Detail();
        k.Col = PoleGray;
        k.Cylinder(m, Vector3.Zero, 0.08f, 0.065f, 3.6f, 8);
        k.Col = new Color(0.06f, 0.06f, 0.06f);
        k.Box(m, new Vector3(0, 3.0f, 0.12f), new Vector3(0.34f, 1.0f, 0.26f));
        k.Box(d, new Vector3(0, 3.0f, -0.02f), new Vector3(0.52f, 1.18f, 0.03f));
        k.Col = new Color(0.5f, 0.5f, 0.48f);
        k.Box(d, new Vector3(0, 3.0f, -0.04f), new Vector3(0.56f, 1.22f, 0.01f));
        var colors = new[] { new Color(0.16f, 0.03f, 0.02f), new Color(0.16f, 0.1f, 0.02f), new Color(0.03f, 0.12f, 0.06f) };
        for (var i = 0; i < 3; i++)
        {
            var y = 3.3f - i * 0.3f;
            k.Col = colors[i];
            k.Cylinder(d, new Vector3(0, y, 0.25f), 0.1f, 0.1f, 0.02f, 10, true, new Basis(Vector3.Right, Mathf.Pi / 2));
            k.Col = new Color(0.05f, 0.05f, 0.05f);
            k.Box(m, new Vector3(0, y + 0.11f, 0.33f), new Vector3(0.26f, 0.02f, 0.16f));
        }
        k.Col = new Color(0.06f, 0.06f, 0.06f);
        k.Box(m, new Vector3(0.0f, 2.25f, 0.1f), new Vector3(0.26f, 0.42f, 0.16f));
        k.Col = new Color(0.35f, 0.32f, 0.1f);
        k.Box(m, new Vector3(0, 1.15f, 0.1f), new Vector3(0.12f, 0.2f, 0.1f));
    });

    /// <summary>Taille de collision du camion militaire.</summary>
    public static readonly Vector3 ArmyTruckSize = new(2.5f, 3.4f, 7.6f);

    /// <summary>Séparateur en béton sale, ébréché, traces de suie (variante 1 : cassé en biais).</summary>
    public static ArrayMesh Barrier(int v) => Cached($"barrier{v}", k =>
    {
        var d = Mats.Detail();
        k.Col = new Color(0.5f, 0.49f, 0.46f);
        k.Frustum(d, Vector3.Zero, new Vector2(0.6f, 2.98f), new Vector2(0.36f, 2.98f), 0.3f);
        k.Frustum(d, new Vector3(0, 0.3f, 0), new Vector2(0.36f, 2.98f), new Vector2(0.2f, 2.98f), 0.5f);
        k.Col = new Color(0.32f, 0.31f, 0.29f);
        k.Box(d, new Vector3(0, 0.06f, 0), new Vector3(0.62f, 0.12f, 3f)); // salissures au pied
        k.Col = new Color(0.65f, 0.55f, 0.15f);
        k.Box(d, new Vector3(0.105f, 0.62f, 0.9f), new Vector3(0.02f, 0.1f, 0.6f), Rot(0, 0, 0.2f)); // bande jaune délavée
        if (v == 1)
        {
            // Éclat arraché à une extrémité, fers d'armature apparents.
            k.Col = new Color(0.62f, 0.6f, 0.56f);
            k.Box(d, new Vector3(0, 0.62f, 1.35f), new Vector3(0.3f, 0.35f, 0.4f), Rot(0.4f, 0.5f));
            k.Col = new Color(0.3f, 0.18f, 0.1f);
            k.Tube(Mats.DetailMetal(), new Vector3(0.05f, 0.5f, 1.2f), new Vector3(0.1f, 0.95f, 1.6f), 0.012f, 4);
            k.Tube(Mats.DetailMetal(), new Vector3(-0.05f, 0.45f, 1.25f), new Vector3(-0.15f, 0.85f, 1.65f), 0.012f, 4);
        }
    });

    /// <summary>Taille de collision d'un séparateur (3 m).</summary>
    public static readonly Vector3 BarrierSize = new(0.6f, 0.8f, 3f);

    /// <summary>Hérisson antichar : trois poutrelles soudées en étoile.</summary>
    public static ArrayMesh Hedgehog() => Cached("hedgehog", k =>
    {
        var m = Mats.Charred();
        k.Col = new Color(0.32f, 0.26f, 0.22f);
        var c = new Vector3(0, 0.62f, 0);
        var axes = new[] { new Vector3(1, 1, 0).Normalized(), new Vector3(-1, 1, 0.9f).Normalized(), new Vector3(0.1f, 1, -1).Normalized() };
        foreach (var a in axes)
        {
            var x = a.Cross(Vector3.Forward).Normalized();
            if (x.LengthSquared() < 0.1f) x = Vector3.Right;
            var b = new Basis(x, a, x.Cross(a).Normalized());
            k.Box(m, c, new Vector3(0.18f, 1.9f, 0.04f), b);
            k.Box(m, c, new Vector3(0.04f, 1.9f, 0.18f), b);
        }
    });

    /// <summary>Gabion de type Hesco : caisse grillagée remplie de sable (1,1 × 1,3 × 1,1 m).</summary>
    public static ArrayMesh Hesco() => Cached("hesco", k =>
    {
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        k.Col = Khaki.Lightened(0.08f);
        k.Box(d, new Vector3(0, 0.65f, 0), new Vector3(1.06f, 1.3f, 1.06f));
        k.Col = new Color(0.45f, 0.42f, 0.33f);
        k.Box(d, new Vector3(0, 1.31f, 0), new Vector3(1.0f, 0.04f, 1.0f));
        // Grillage : barreaux verticaux et horizontaux à la surface.
        k.Col = new Color(0.36f, 0.36f, 0.34f);
        for (var i = 0; i <= 4; i++)
        {
            var t = -0.54f + i * 0.27f;
            foreach (var s in new[] { -0.54f, 0.54f })
            {
                k.Box(m, new Vector3(t, 0.65f, s), new Vector3(0.015f, 1.3f, 0.015f));
                k.Box(m, new Vector3(s, 0.65f, t), new Vector3(0.015f, 1.3f, 0.015f));
            }
        }
        for (var j = 0; j <= 4; j++)
        {
            var y = 0.02f + j * 0.32f;
            k.Box(m, new Vector3(0, y, 0.545f), new Vector3(1.09f, 0.015f, 0.015f));
            k.Box(m, new Vector3(0, y, -0.545f), new Vector3(1.09f, 0.015f, 0.015f));
            k.Box(m, new Vector3(0.545f, y, 0), new Vector3(0.015f, 0.015f, 1.09f));
            k.Box(m, new Vector3(-0.545f, y, 0), new Vector3(0.015f, 0.015f, 1.09f));
        }
    });

    /// <summary>Fût métallique (rouillé ; variante 1 : brasero noirci).</summary>
    public static ArrayMesh Drum(int v) => Cached($"drum{v}", k =>
    {
        var m = Mats.Charred();
        k.Col = v == 1 ? BurnSoot : new Color(0.3f, 0.32f, 0.22f);
        k.Cylinder(m, Vector3.Zero, 0.29f, 0.29f, 0.88f, 12, v == 0);
        k.Col = v == 1 ? BurnRust : new Color(0.22f, 0.24f, 0.16f);
        foreach (var y in new[] { 0.29f, 0.58f }) k.Cylinder(m, new Vector3(0, y, 0), 0.3f, 0.3f, 0.03f, 12, false);
    });

    /// <summary>
    /// Mur de sacs de sable entre deux points (rangs décalés, sacs bombés, teintes variées).
    /// Renvoie la boîte de collision (centre, taille, lacet).
    /// </summary>
    public static (Vector3 c, Vector3 size, float yaw) SandbagWall(MeshKit k, Vector3 a, Vector3 b, int rows, int seed)
    {
        var d = Mats.Detail();
        var dir = b - a;
        dir.Y = 0;
        var len = dir.Length();
        var yaw = Mathf.Atan2(dir.X, dir.Z);
        var basis = new Basis(Vector3.Up, yaw);
        const float bagL = 0.62f, bagW = 0.36f, bagH = 0.17f;
        var keep = k.Xf;
        for (var r = 0; r < rows; r++)
        {
            var off = r % 2 == 0 ? 0 : bagL / 2;
            var n = Mathf.FloorToInt((len - off) / bagL);
            for (var i = 0; i < n; i++)
            {
                var t = off + (i + 0.5f) * bagL;
                var h = H(seed + r, i, 61);
                // Rang du haut parfois éventré (sacs manquants).
                if (r == rows - 1 && h < 0.18f) continue;
                var bb = basis * new Basis(Vector3.Up, (h - 0.5f) * 0.2f) * new Basis(Vector3.Back, (H(i, r, seed) - 0.5f) * 0.12f);
                foreach (var depthOff in new[] { 0f, -bagW * 0.98f })
                {
                    var p = a + basis * new Vector3((h - 0.5f) * 0.06f + depthOff, bagH * r, t);
                    var tone = 0.75f + 0.3f * H(seed, i + r * 40 + (depthOff < 0 ? 900 : 0), 62);
                    k.Col = new Color(0.34f, 0.32f, 0.25f).Lerp(new Color(0.28f, 0.27f, 0.23f), h) * tone;
                    // Sac bombé : base large, dessus arrondi (tronc de pyramide + coussin).
                    k.Xf = keep * new Transform3D(bb, p);
                    k.Frustum(d, Vector3.Zero, new Vector2(bagW, bagL * 0.97f), new Vector2(bagW * 0.72f, bagL * 0.8f), bagH * 0.8f);
                    k.Frustum(d, new Vector3(0, bagH * 0.8f, 0), new Vector2(bagW * 0.72f, bagL * 0.8f), new Vector2(bagW * 0.35f, bagL * 0.55f), bagH * 0.22f);
                }
            }
        }
        k.Xf = keep;
        var c = a + basis * new Vector3(-bagW * 0.45f, rows * bagH / 2, len / 2);
        return (c, new Vector3(bagW * 2f, rows * bagH, len), yaw);
    }

    /// <summary>Rouleau de barbelé concertina entre deux points (hélice de fil fin), sans collision.</summary>
    public static void RazorWire(MeshKit k, Vector3 a, Vector3 b, float radius = 0.42f)
    {
        var m = Mats.DetailMetal();
        var dir = b - a;
        var len = dir.Length();
        if (len < 0.3f) return;
        var z = dir / len;
        var x = Vector3.Up.Cross(z).Normalized();
        var y = z.Cross(x);
        k.Col = new Color(0.42f, 0.4f, 0.38f);
        var turns = len / 0.22f;
        var steps = Mathf.CeilToInt(turns * 10);
        var prev = Vector3.Zero;
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var ang = t * turns * Mathf.Tau;
            var rr = radius * (0.92f + 0.08f * Mathf.Sin(t * 37f));
            var p = a + z * (t * len) + (x * Mathf.Cos(ang) + y * Mathf.Sin(ang)) * rr + y * radius;
            if (i > 0) k.Tube(m, prev, p, 0.008f, 3);
            prev = p;
        }
    }

    // ───────────────────────── Gravats et débris ─────────────────────────

    /// <summary>
    /// Tas de gravats (repère monde) : butte de béton concassé, blocs, dalles brisées hérissées de fers,
    /// briques. Renvoie les points de l'enveloppe convexe de collision (la butte, sans les éléments saillants).
    /// </summary>
    public static Vector3[] RubblePile(MeshKit k, Vector3 center, float yaw, Vector3 size, int seed, Color tint, bool bricks)
    {
        var d = Mats.Detail();
        var basis = new Basis(Vector3.Up, yaw);
        var hx = size.X / 2;
        var hz = size.Z / 2;
        var hh = size.Y;
        float Hgt(float x, float z)
        {
            var q = 1f - (x * x) / (hx * hx) - (z * z) / (hz * hz);
            return q <= 0 ? 0 : hh * Mathf.Pow(q, 0.75f);
        }
        // Butte : icosaèdre aplati, bosselé, normales lissées, teinte de poussière de béton.
        var dust = tint.Lerp(new Color(0.3f, 0.29f, 0.27f), 0.65f);
        Vector3 Pm(Vector3 q)
        {
            var bump = 1f + 0.2f * Mathf.Sin(q.X * 7f + seed) * Mathf.Cos(q.Z * 6f + seed * 0.7f) + 0.1f * Mathf.Sin(q.Y * 13f + seed)
                + 0.05f * Mathf.Sin(q.X * 23f + q.Z * 19f + seed);
            var y = Mathf.Max(q.Y, -0.05f);
            return center + basis * new Vector3(q.X * hx * bump, y * hh * bump, q.Z * hz * bump);
        }
        Color Cm(Vector3 q) => dust * (0.72f + 0.28f * (0.5f + 0.5f * Mathf.Sin(q.X * 17f + q.Z * 13f + seed)));
        for (var i = 0; i < Ico.Length; i += 3)
        {
            if (Ico[i].Y < -0.05f && Ico[i + 1].Y < -0.05f && Ico[i + 2].Y < -0.05f) continue;
            Vector3 N(Vector3 q) => (basis * new Vector3(q.X / hx, q.Y / hh, q.Z / hz)).Normalized();
            k.TriSmooth(d, Pm(Ico[i]), Pm(Ico[i + 1]), Pm(Ico[i + 2]), N(Ico[i]), N(Ico[i + 1]), N(Ico[i + 2]), Cm(Ico[i]), Cm(Ico[i + 1]), Cm(Ico[i + 2]));
        }
        // Blocs de béton à moitié enfouis : beaucoup de petits, quelques gros.
        var nBlocks = Mathf.Clamp((int)(size.X * size.Z * 7f), 40, 200);
        for (var i = 0; i < nBlocks; i++)
        {
            var u = (H(i, seed, 72) * 2 - 1);
            var w = (H(i, seed, 73) * 2 - 1);
            if (u * u + w * w > 1.1f) continue;
            var lx = u * hx;
            var lz = w * hz;
            var y = Hgt(lx, lz);
            var big = H(i, seed, 74) < 0.12f;
            var s = big ? 0.45f + H(i, seed, 75) * 0.5f * Mathf.Min(1f, hh) : 0.08f + H(i, seed, 75) * 0.28f;
            var bs = new Vector3(s * (0.7f + H(i, seed, 76) * 0.8f), s * (0.4f + H(i, seed, 77) * 0.5f), s * (0.7f + H(i, seed, 78) * 0.8f));
            var rb = basis * Rot(H(i, seed, 79) * Mathf.Tau, (H(i, seed, 80) - 0.5f) * 1.5f, (H(i, seed, 81) - 0.5f) * 1.5f);
            var t = H(i, seed, 82);
            k.Col = (t < 0.35f ? tint * 0.7f : new Color(0.34f, 0.33f, 0.31f)) * (0.5f + 0.45f * H(i, seed, 83));
            k.Box(d, center + basis * new Vector3(lx, y - bs.Y * 0.25f, lz), bs, rb);
        }
        // Dalles brisées inclinées, fers d'armature tordus.
        var nSlabs = size.X > 3.5f ? 2 + (int)(H(seed, 1, 83) * 2) : 1;
        for (var i = 0; i < nSlabs; i++)
        {
            var lx = (H(i, seed, 84) - 0.5f) * hx * 1.1f;
            var lz = (H(i, seed, 85) - 0.5f) * hz * 1.1f;
            var y = Hgt(lx, lz) * 0.7f;
            var sw = 1.2f + H(i, seed, 86) * Mathf.Min(1.8f, size.X * 0.4f);
            var sd = 0.8f + H(i, seed, 87) * 1.2f;
            var rb = basis * Rot(H(i, seed, 88) * Mathf.Tau, 0.35f + H(i, seed, 89) * 0.6f, (H(i, seed, 90) - 0.5f) * 0.5f);
            var c = center + basis * new Vector3(lx, y + 0.1f, lz);
            k.Col = new Color(0.58f, 0.57f, 0.54f) * (0.8f + 0.2f * H(i, seed, 91));
            k.Box(d, c, new Vector3(sw, 0.18f, sd), rb);
            k.Col = new Color(0.3f, 0.17f, 0.1f);
            for (var j = 0; j < 5; j++)
            {
                var along = (j / 4f - 0.5f) * sw * 0.85f;
                var p0 = c + rb * new Vector3(along, 0, sd / 2);
                var bend = rb * new Vector3((H(j, i, seed) - 0.5f) * 0.5f, 0.25f + H(j, i, seed + 1) * 0.4f, 0.35f + H(j, i, seed + 2) * 0.6f);
                var p1 = p0 + bend * 0.6f;
                var p2 = p1 + bend.Rotated(Vector3.Up, 0.8f) * 0.4f + Vector3.Down * 0.15f;
                k.Tube(Mats.DetailMetal(), p0, p1, 0.012f, 3);
                k.Tube(Mats.DetailMetal(), p1, p2, 0.012f, 3);
            }
        }
        // Briques éparses.
        if (bricks)
        {
            for (var i = 0; i < 24; i++)
            {
                var lx = (H(i, seed, 92) * 2 - 1) * hx * 1.15f;
                var lz = (H(i, seed, 93) * 2 - 1) * hz * 1.15f;
                var y = Hgt(lx, lz);
                k.Col = new Color(0.45f, 0.22f, 0.15f) * (0.75f + 0.35f * H(i, seed, 94));
                k.Box(d, center + basis * new Vector3(lx, y + 0.03f, lz), new Vector3(0.22f, 0.07f, 0.11f), basis * Rot(H(i, seed, 95) * Mathf.Tau, (H(i, seed, 96) - 0.5f), (H(i, seed, 97) - 0.5f)));
            }
        }
        // Enveloppe de collision : ellipse au sol, couronne à mi-hauteur, sommet.
        var pts = new List<Vector3>();
        for (var i = 0; i < 12; i++)
        {
            var a = i * Mathf.Tau / 12;
            pts.Add(center + basis * new Vector3(Mathf.Cos(a) * hx * 0.95f, 0, Mathf.Sin(a) * hz * 0.95f));
        }
        for (var i = 0; i < 8; i++)
        {
            var a = i * Mathf.Tau / 8 + 0.2f;
            pts.Add(center + basis * new Vector3(Mathf.Cos(a) * hx * 0.6f, hh * 0.6f, Mathf.Sin(a) * hz * 0.6f));
        }
        pts.Add(center + Vector3.Up * hh * 0.92f);
        return pts.ToArray();
    }

    /// <summary>
    /// Carcasse d'immeuble éventré (repère monde) : murs de façade troués de baies vides à hauteur déchiquetée,
    /// un pan effondré, morceaux de planchers en porte-à-faux, fers d'armature, suie au-dessus des baies.
    /// Ajoute dans <paramref name="boxes"/> une boîte de collision par travée de mur.
    /// </summary>
    public static void RuinedShell(MeshKit k, Vector3 center, Vector2 size, float height, int seed, Color wall, List<(Transform3D xf, Vector3 size)> boxes)
    {
        var d = Mats.Detail();
        var m = Mats.DetailMetal();
        const float fh = 3.2f, th = 0.32f;
        var hx = size.X / 2;
        var hz = size.Y / 2;
        var sides = new[]
        {
            (new Vector3(-hx, 0, hz), Vector3.Right, Vector3.Back, size.X),
            (new Vector3(hx, 0, -hz), Vector3.Left, Vector3.Forward, size.X),
            (new Vector3(hx, 0, hz), Vector3.Forward, Vector3.Right, size.Y),
            (new Vector3(-hx, 0, -hz), Vector3.Back, Vector3.Left, size.Y),
        };
        var fallen = (int)(H(seed, 1, 101) * 4);
        var soot = new Color(0.07f, 0.065f, 0.06f);
        for (var si = 0; si < 4; si++)
        {
            var (start, along, normal, len) = sides[si];
            var nb = Mathf.Max(2, Mathf.RoundToInt(len / 3.4f));
            var bw = len / nb;
            var heights = new float[nb];
            for (var j = 0; j < nb; j++)
            {
                var edge = Mathf.Min(j, nb - 1 - j) / Mathf.Max(1f, nb / 2f);   // 0 aux angles, 1 au milieu
                var f = 1f - edge * 0.55f + (H(seed + si, j, 102) - 0.5f) * 0.5f + 0.2f * Mathf.Sin(j * 1.7f + seed);
                heights[j] = si == fallen ? 0.6f + H(seed + si, j, 103) * 2.2f : Mathf.Clamp(f, 0.12f, 1f) * height;
            }
            for (var j = 0; j < nb; j++)
            {
                var hj = heights[j];
                var a0 = j * bw;
                var mid = start + along * (a0 + bw / 2) - normal * (th / 2);
                Vector3 W(float a, float y) => center + start + along * a - normal * (th / 2) + Vector3.Up * y;
                // Trumeau gauche (plus haut des deux travées voisines).
                var ph = Mathf.Max(hj, j > 0 ? heights[j - 1] : hj) * (0.92f + 0.08f * H(seed + si, j, 104));
                k.Col = wall * (0.85f + 0.15f * H(seed + si, j, 105));
                k.Box(d, W(a0 + 0.3f, ph / 2), Abs(along) * 0.6f + Abs(normal) * th + Vector3.Up * ph);
                if (j == nb - 1)
                    k.Box(d, W(len - 0.3f, hj / 2), Abs(along) * 0.6f + Abs(normal) * th + Vector3.Up * hj);
                // Allèges et linteaux étage par étage, jusqu'à la hauteur déchiquetée.
                var ww = bw - 0.6f;
                var wc = a0 + 0.6f + ww / 2 - 0.3f;
                for (var fl = 0; fl * fh < hj; fl++)
                {
                    var yb = fl * fh;
                    var sill = Mathf.Min(yb + (fl == 0 ? 0.6f : 1.0f), hj);
                    k.Col = wall * (0.8f + 0.2f * H(seed + fl, j + si * 30, 106));
                    if (sill > yb) k.Box(d, W(wc, (yb + sill) / 2), Abs(along) * ww + Abs(normal) * th + Vector3.Up * (sill - yb));
                    if (hj > yb + 2.75f)
                    {
                        var top = Mathf.Min(yb + fh, hj);
                        k.Col = soot.Lerp(wall, 0.25f);   // suie au-dessus de la baie
                        k.Box(d, W(wc, (yb + 2.6f + top) / 2), Abs(along) * ww + Abs(normal) * (th + 0.02f) + Vector3.Up * (top - yb - 2.6f));
                    }
                    else if (hj > sill + 0.3f)
                    {
                        // Morceau d'allège resté debout, cassé en biais.
                        k.Col = wall * 0.85f;
                        var bh = hj - sill;
                        k.Box(d, W(wc - ww * 0.25f, sill + bh / 2), Abs(along) * (ww * 0.45f) + Abs(normal) * th + Vector3.Up * bh,
                            new Basis(normal.Cross(Vector3.Up).Normalized(), (H(seed, j + fl, 107) - 0.5f) * 0.4f));
                    }
                }
                // Fers d'armature au sommet.
                k.Col = new Color(0.28f, 0.16f, 0.1f);
                for (var rb = 0; rb < 3; rb++)
                {
                    var p0 = W(a0 + 0.15f + rb * 0.2f, ph);
                    k.Tube(m, p0, p0 + Vector3.Up * (0.5f + H(seed, j * 3 + rb, 108) * 0.6f) + along * (H(seed, rb, 109) - 0.5f) * 0.6f, 0.014f, 3);
                }
                boxes.Add((new Transform3D(Basis.Identity, center + mid + Vector3.Up * (hj / 2)), Abs(along) * bw + Abs(normal) * th + Vector3.Up * hj));
            }
            // Planchers en porte-à-faux le long des pans encore debout.
            for (var fl = 1; fl * fh < height - 1f; fl++)
            {
                var y = fl * fh;
                for (var j = 0; j < nb; j++)
                {
                    if (heights[j] < y + 0.8f || H(seed + si, j + fl * 20, 110) < 0.35f) continue;
                    var depth = 1.2f + H(seed + si, j + fl * 20, 111) * 2.4f;
                    var c = center + start + along * (j * bw + bw / 2) - normal * (th + depth / 2) + Vector3.Up * (y - 0.1f);
                    k.Col = new Color(0.42f, 0.41f, 0.39f) * (0.8f + 0.2f * H(seed, j, 112));
                    k.Box(d, c, Abs(along) * (bw * 0.95f) + Abs(normal) * depth + Vector3.Up * 0.22f);
                    // Bord cassé qui pend vers l'intérieur.
                    var hinge = normal.Cross(Vector3.Up).Normalized();
                    var tilt = 0.35f + H(seed, j + fl, 113) * 0.6f;
                    var piece = new Basis(hinge, -tilt);
                    var pc = c - normal * (depth / 2) + piece * (-normal * 0.7f);
                    k.Box(d, pc + Vector3.Down * 0.05f, Abs(along) * (bw * 0.7f) + Abs(normal) * 1.4f + Vector3.Up * 0.2f, piece);
                }
            }
        }
    }

    private static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.X), Mathf.Abs(v.Y), Mathf.Abs(v.Z));

    /// <summary>Petit débris (sans collision) : 0-3 blocs de béton, 4 brique, 5 papier, 6 planche, 7 pneu brûlé, 8 tôle tordue, 9 éclats de verre.</summary>
    public const int DebrisKinds = 10;

    public static ArrayMesh Debris(int v) => Cached($"debris{v}", k =>
    {
        var d = Mats.Detail();
        switch (v)
        {
            case <= 3:
            {
                k.Col = new Color(0.55f, 0.54f, 0.51f) * (0.8f + v * 0.06f);
                var s = 0.18f + v * 0.1f;
                k.Box(d, new Vector3(0, s * 0.3f, 0), new Vector3(s * 1.3f, s * 0.7f, s), Rot(v * 0.7f, 0.25f, 0.15f));
                k.Box(d, new Vector3(s * 0.4f, s * 0.2f, s * 0.2f), new Vector3(s * 0.7f, s * 0.45f, s * 0.6f), Rot(v * 1.3f, -0.3f, 0.4f));
                if (v >= 2)
                {
                    k.Col = new Color(0.3f, 0.17f, 0.1f);
                    k.Tube(Mats.DetailMetal(), new Vector3(0, s * 0.3f, 0), new Vector3(s * 1.4f, s * 0.9f, s * 0.6f), 0.01f, 3);
                }
                break;
            }
            case 4:
                k.Col = new Color(0.47f, 0.24f, 0.16f);
                k.Box(d, new Vector3(0, 0.035f, 0), new Vector3(0.22f, 0.07f, 0.11f));
                k.Box(d, new Vector3(0.18f, 0.03f, 0.12f), new Vector3(0.11f, 0.06f, 0.1f), Rot(0.7f));
                break;
            case 5:
                k.Col = new Color(0.62f, 0.61f, 0.57f);
                k.Box(d, new Vector3(0, 0.006f, 0), new Vector3(0.3f, 0.004f, 0.21f), null, false);
                k.Col = new Color(0.5f, 0.49f, 0.46f);
                k.Box(d, new Vector3(0.25f, 0.02f, 0.1f), new Vector3(0.21f, 0.004f, 0.28f), Rot(0.6f, 0.12f), false);
                break;
            case 6:
                k.Col = new Color(0.3f, 0.24f, 0.18f);
                k.Box(d, new Vector3(0, 0.03f, 0), new Vector3(0.12f, 0.03f, 1.6f), Rot(0, 0.03f));
                break;
            case 7:
            {
                k.Col = new Color(0.05f, 0.05f, 0.05f);
                for (var i = 0; i < 10; i++)
                {
                    var a0 = i * Mathf.Tau / 10; var a1 = (i + 1) * Mathf.Tau / 10;
                    k.Tube(d, new Vector3(Mathf.Cos(a0) * 0.3f, 0.09f, Mathf.Sin(a0) * 0.3f), new Vector3(Mathf.Cos(a1) * 0.3f, 0.09f, Mathf.Sin(a1) * 0.3f), 0.09f, 5);
                }
                break;
            }
            case 8:
                k.Col = new Color(0.32f, 0.27f, 0.22f);
                k.Box(Mats.Charred(), new Vector3(0, 0.08f, 0), new Vector3(0.9f, 0.02f, 0.6f), Rot(0, 0.15f, 0.2f));
                k.Box(Mats.Charred(), new Vector3(0.4f, 0.15f, 0.1f), new Vector3(0.5f, 0.02f, 0.6f), Rot(0.3f, 0.1f, -0.6f));
                break;
            default:
                k.Col = new Color(0.3f, 0.34f, 0.36f);
                for (var i = 0; i < 7; i++)
                    k.Tri(Mats.Glass(), new Vector3(H(i, 1, 99) * 0.6f, 0.01f, H(i, 2, 99) * 0.6f), new Vector3(H(i, 3, 99) * 0.6f, 0.01f, H(i, 4, 99) * 0.6f),
                        new Vector3(H(i, 5, 99) * 0.6f, 0.012f, H(i, 6, 99) * 0.6f), Vector3.Down);
                break;
        }
    });

    // ───────────────────────── Végétation morte, mobilier ─────────────────────────

    /// <summary>Arbre mort ou brûlé, sans feuilles : tronc, charpentières et rameaux (variantes 0 à 3).</summary>
    public static ArrayMesh DeadTree(int v) => Cached($"deadtree{v}", k =>
    {
        var bark = Mats.Detail();
        var burnt = v >= 2;
        var col = burnt ? new Color(0.09f, 0.08f, 0.075f) : new Color(0.26f, 0.22f, 0.18f);
        var h = 6.5f + v * 0.8f;
        k.Col = col;
        k.Cylinder(bark, Vector3.Zero, 0.3f, 0.17f, h * 0.6f, 8, false);
        void Branch(Vector3 from, Vector3 dir, float len, float r, int depth, int id)
        {
            var to = from + dir * len;
            k.Col = col * (0.9f + 0.2f * H(id, v, 31));
            k.Tube(bark, from, to, r, depth == 0 ? 6 : 4);
            if (depth >= 3 || (burnt && depth >= 2)) return;
            var n = depth == 0 ? 3 : 2;
            for (var i = 0; i < n; i++)
            {
                var yaw = H(id, i, 32 + v) * Mathf.Tau;
                var tilt = 0.35f + H(id, i, 33 + v) * 0.5f;
                var nd = (dir * 0.6f + new Vector3(Mathf.Cos(yaw) * tilt, 0.35f, Mathf.Sin(yaw) * tilt)).Normalized();
                Branch(to, nd, len * (0.55f + H(id, i, 34) * 0.2f), r * 0.6f, depth + 1, id * 3 + i + 1);
            }
        }
        var top = new Vector3(0, h * 0.6f, 0);
        for (var i = 0; i < 4; i++)
        {
            var yaw = i * Mathf.Tau / 4 + H(i, v, 35);
            var d = new Vector3(Mathf.Cos(yaw) * 0.55f, 1f, Mathf.Sin(yaw) * 0.55f).Normalized();
            var start = Vector3.Up * (h * (0.38f + i * 0.07f));
            Branch(start, d, h * 0.35f, 0.11f, 0, i + 1);
        }
        Branch(top, Vector3.Up, h * 0.35f, 0.12f, 1, 9);
    });

    /// <summary>Palmier calciné : stipe noirci, palmes mortes pendantes.</summary>
    public static ArrayMesh BurntPalm(float hgt) => Cached($"burntpalm{Mathf.Snapped(hgt, 0.5f)}", k =>
    {
        var bark = Mats.Detail();
        var top = new Vector3(0.3f, hgt, 0.2f);
        k.Col = new Color(0.1f, 0.09f, 0.08f);
        k.Tube(bark, Vector3.Zero, top * 0.5f, 0.26f, 8);
        k.Tube(bark, top * 0.5f, top, 0.2f, 8);
        for (var f = 0; f < 7; f++)
        {
            var yaw = f * Mathf.Tau / 7;
            var dir = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw));
            var p1 = top + dir * 0.6f + Vector3.Up * 0.1f;
            var p2 = p1 + dir * 0.5f + Vector3.Down * 1.6f;
            k.Col = f % 2 == 0 ? new Color(0.2f, 0.16f, 0.1f) : new Color(0.07f, 0.06f, 0.05f);
            k.Tube(bark, top, p1, 0.05f, 4);
            k.Tube(bark, p1, p2, 0.035f, 4);
            k.Box(bark, (p1 + p2) / 2 + dir * 0.05f, new Vector3(0.3f, 1.4f, 0.04f), new Basis(Vector3.Up, -yaw + Mathf.Pi / 2) * new Basis(Vector3.Right, 0.3f));
        }
    });

    /// <summary>Abribus dévasté : vitres soufflées (éclats au sol), toit arraché d'un côté, panneau noirci.</summary>
    public static ArrayMesh BusStopRuined() => Cached("busstopruined", k =>
    {
        var m = Mats.DetailMetal();
        var d = Mats.Detail();
        k.Col = new Color(0.22f, 0.23f, 0.24f);
        foreach (var x in new[] { -2.05f, 2.05f })
        foreach (var z in new[] { -0.7f, 0.55f })
        {
            if (x > 0 && z > 0) { k.Box(m, new Vector3(x - 0.3f, 0.6f, z + 0.3f), new Vector3(0.08f, 2.5f, 0.08f), Rot(0, 0.2f, 1.2f)); continue; } // poteau couché
            k.Box(m, new Vector3(x, 1.25f, z), new Vector3(0.08f, 2.5f, 0.08f));
        }
        k.Box(m, new Vector3(-0.2f, 2.2f, -0.05f), new Vector3(4.4f, 0.12f, 1.75f), Rot(0, 0.1f, -0.18f)); // toit affaissé
        k.Col = new Color(0.3f, 0.34f, 0.36f);
        // Restes de vitres dans les cadres.
        k.Tri(Mats.Glass(), new Vector3(-2f, 0.2f, -0.72f), new Vector3(-1.1f, 0.2f, -0.72f), new Vector3(-2f, 1.4f, -0.72f), Vector3.Forward * 2);
        k.Tri(Mats.Glass(), new Vector3(-2.05f, 2.3f, 0.4f), new Vector3(-2.05f, 1.6f, 0.4f), new Vector3(-2.05f, 2.3f, -0.2f), Vector3.Right * 2);
        // Panneau publicitaire carbonisé.
        k.Col = new Color(0.08f, 0.075f, 0.07f);
        k.Box(d, new Vector3(2.05f, 1.35f, -0.08f), new Vector3(0.18f, 2.0f, 1.3f));
        k.Col = new Color(0.4f, 0.36f, 0.3f);
        k.Box(d, new Vector3(2.15f, 1.6f, -0.1f), new Vector3(0.02f, 0.6f, 0.5f), Rot(0, 0, 0.2f));
        // Banc tordu.
        k.Col = new Color(0.45f, 0.46f, 0.47f);
        k.Box(m, new Vector3(-0.3f, 0.4f, -0.5f), new Vector3(2.4f, 0.05f, 0.35f), Rot(0, 0, 0.12f));
        // Éclats de verre au sol.
        k.Col = new Color(0.4f, 0.45f, 0.47f);
        for (var i = 0; i < 30; i++)
        {
            var p = new Vector3(-2.2f + H(i, 0, 41) * 4.4f, 0.01f, -1.6f + H(i, 1, 41) * 2.6f);
            k.Tri(Mats.Glass(), p, p + new Vector3(0.12f * H(i, 2, 41), 0, 0.05f), p + new Vector3(0, 0.002f, 0.1f * H(i, 3, 41) + 0.03f), Vector3.Down);
        }
    });

    /// <summary>Chaise de jardin en plastique (blanc sali).</summary>
    public static ArrayMesh GardenChair() => Cached("gchair", k =>
    {
        var d = Mats.Detail();
        k.Col = new Color(0.66f, 0.65f, 0.6f);
        k.Box(d, new Vector3(0, 0.44f, 0), new Vector3(0.46f, 0.04f, 0.44f));
        k.Box(d, new Vector3(0, 0.72f, -0.22f), new Vector3(0.46f, 0.5f, 0.04f), Rot(0, -0.12f));
        foreach (var x in new[] { -0.2f, 0.2f })
        foreach (var z in new[] { -0.19f, 0.19f })
            k.Box(d, new Vector3(x, 0.22f, z), new Vector3(0.04f, 0.44f, 0.04f));
    });

    /// <summary>Table de jardin ronde.</summary>
    public static ArrayMesh GardenTable() => Cached("gtable", k =>
    {
        var d = Mats.Detail();
        k.Col = new Color(0.6f, 0.59f, 0.55f);
        k.Cylinder(d, new Vector3(0, 0.7f, 0), 0.5f, 0.5f, 0.04f, 14);
        foreach (var a in new[] { 0.4f, 2.0f, 3.6f, 5.2f })
            k.Box(d, new Vector3(Mathf.Cos(a) * 0.35f, 0.35f, Mathf.Sin(a) * 0.35f), new Vector3(0.04f, 0.7f, 0.04f));
    });

    /// <summary>Canapé abandonné, tissu délavé et taché.</summary>
    public static ArrayMesh Sofa() => Cached("sofa", k =>
    {
        var d = Mats.Detail();
        var c = new Color(0.27f, 0.25f, 0.22f);
        k.Col = c;
        k.Box(d, new Vector3(0, 0.25f, 0), new Vector3(1.9f, 0.4f, 0.85f));
        k.Box(d, new Vector3(0, 0.62f, -0.33f), new Vector3(1.9f, 0.55f, 0.22f));
        k.Col = c.Darkened(0.15f);
        foreach (var x in new[] { -0.88f, 0.88f }) k.Box(d, new Vector3(x, 0.48f, 0.02f), new Vector3(0.2f, 0.32f, 0.82f));
        k.Col = c.Lightened(0.06f);
        k.Box(d, new Vector3(-0.38f, 0.5f, 0.06f), new Vector3(0.74f, 0.12f, 0.65f), Rot(0.1f, 0.05f));
    });

    /// <summary>Matelas abandonné.</summary>
    public static ArrayMesh Mattress() => Cached("mattress", k =>
    {
        k.Col = new Color(0.52f, 0.5f, 0.45f);
        k.Box(Mats.Detail(), new Vector3(0, 0.1f, 0), new Vector3(1.4f, 0.2f, 1.95f));
        k.Col = new Color(0.36f, 0.32f, 0.24f);
        k.Box(Mats.Detail(), new Vector3(0.2f, 0.205f, 0.3f), new Vector3(0.6f, 0.01f, 0.5f), null, false);
    });

    /// <summary>Valise abandonnée (exode).</summary>
    public static ArrayMesh Suitcase() => Cached("suitcase", k =>
    {
        k.Col = new Color(0.12f, 0.13f, 0.15f);
        k.Box(Mats.Detail(), new Vector3(0, 0.12f, 0), new Vector3(0.7f, 0.24f, 0.45f));
        k.Col = new Color(0.4f, 0.4f, 0.4f);
        k.Box(Mats.DetailMetal(), new Vector3(0, 0.25f, 0), new Vector3(0.2f, 0.03f, 0.04f));
    });

    /// <summary>Panneau de clôture à claire-voie couché (bois grisé).</summary>
    public static ArrayMesh FencePanel() => Cached("fencepanel", k =>
    {
        var d = Mats.Detail();
        k.Col = new Color(0.36f, 0.32f, 0.27f);
        for (var i = 0; i < 9; i++) k.Box(d, new Vector3(-0.9f + i * 0.225f, 0.03f, 0), new Vector3(0.12f, 0.025f, 1.5f + (i % 3) * 0.06f));
        k.Col = new Color(0.3f, 0.26f, 0.22f);
        foreach (var z in new[] { -0.45f, 0.45f }) k.Box(d, new Vector3(0, 0.06f, z), new Vector3(2f, 0.03f, 0.1f));
    });

    // ───────────────────────── Port ─────────────────────────

    /// <summary>Conteneur calciné (12,2 × 2,6 × 2,6 m) : tôle rouillée et noircie, portes ouvertes, toit enfoncé.</summary>
    public static ArrayMesh ContainerWreck(int v) => Cached($"contwreck{v}", k =>
    {
        var ch = Mats.Charred();
        const float l = 12.2f, w = 2.6f, h = 2.6f;
        // Parois ondulées (nervures) : bandes alternées.
        for (var i = 0; i < 40; i++)
        {
            var z = -l / 2 + (i + 0.5f) * l / 40f;
            var c = H(i, v, 51) < 0.3f ? BurnSoot : H(i, v, 52) < 0.6f ? BurnRust : BurnBase;
            k.Col = c;
            foreach (var x in new[] { -w / 2, w / 2 })
                k.Box(ch, new Vector3(x + (i % 2 == 0 ? 0 : Mathf.Sign(x) * 0.03f), h / 2, z), new Vector3(0.04f, h - 0.1f, l / 40f + 0.01f));
        }
        k.Col = BurnSoot;
        k.BoxMinMax(ch, new Vector3(-w / 2, 0, -l / 2), new Vector3(w / 2, 0.12f, l / 2));
        foreach (var x in new[] { -w / 2, w / 2 })
        foreach (var y in new[] { 0.06f, h - 0.06f })
            k.Box(ch, new Vector3(x, y, 0), new Vector3(0.12f, 0.12f, l));
        // Toit : enfoncé au milieu (v0) ou soulevé (v1).
        k.Col = BurnRust;
        k.Box(ch, new Vector3(0, h - 0.03f, -l * 0.3f), new Vector3(w, 0.04f, l * 0.4f));
        k.Box(ch, new Vector3(0, v == 0 ? h - 0.45f : h + 0.25f, l * 0.05f), new Vector3(w - 0.1f, 0.04f, l * 0.32f), Rot(0, v == 0 ? 0.1f : -0.15f, 0.05f));
        k.Box(ch, new Vector3(0, h - 0.03f, l * 0.36f), new Vector3(w, 0.04f, l * 0.28f));
        // Fond (avant), portes arrière ouvertes.
        k.Col = BurnBase;
        k.BoxMinMax(ch, new Vector3(-w / 2, 0, l / 2 - 0.05f), new Vector3(w / 2, h, l / 2));
        k.Col = BurnSoot;
        k.Box(ch, new Vector3(-w / 2 - 0.6f, h / 2, -l / 2 - 0.55f), new Vector3(1.3f, h - 0.1f, 0.05f), Rot(1.2f));
        k.Box(ch, new Vector3(w / 2 + 0.2f, h / 2, -l / 2 - 0.62f), new Vector3(1.3f, h - 0.1f, 0.05f), Rot(-1.4f));
        // Intérieur : cargaison carbonisée.
        k.Col = BurnAsh * 0.5f;
        for (var i = 0; i < 7; i++)
            k.Box(ch, new Vector3((H(i, v, 53) - 0.5f) * 1.2f, 0.4f + H(i, v, 54) * 0.4f, -l / 2 + 1.2f + i * 1.4f), new Vector3(1f, 0.6f + H(i, v, 55) * 0.6f, 1f), Rot(H(i, v, 56)));
    });

    /// <summary>Remorqueur coulé, gîté : coque sombre, superstructure brûlée, mât brisé (la ligne de flottaison est vers y = 0).</summary>
    public static ArrayMesh BoatWreck() => Cached("boatwreck", k =>
    {
        var ch = Mats.Charred();
        var d = Mats.Detail();
        const float l = 22f, w = 7f;
        // Coque : sections de plus en plus étroites vers la proue.
        for (var i = 0; i < 10; i++)
        {
            var z0 = -l / 2 + i * l / 10f;
            var z1 = z0 + l / 10f;
            var t0 = i / 10f; var t1 = (i + 1) / 10f;
            var w0 = w * (t0 > 0.6f ? Mathf.Lerp(1f, 0.15f, (t0 - 0.6f) / 0.4f) : 1f);
            var w1 = w * (t1 > 0.6f ? Mathf.Lerp(1f, 0.15f, (t1 - 0.6f) / 0.4f) : 1f);
            k.Col = i % 3 == 0 ? new Color(0.12f, 0.1f, 0.09f) : new Color(0.2f, 0.11f, 0.08f);
            k.Frustum(ch, new Vector3(0, -3f, (z0 + z1) / 2), new Vector2(w0 * 0.7f, z1 - z0), new Vector2((w0 + w1) / 2, z1 - z0), 5f);
        }
        k.Col = new Color(0.45f, 0.1f, 0.06f);
        k.Box(ch, new Vector3(0, -0.2f, -1f), new Vector3(w + 0.05f, 0.5f, l * 0.6f)); // ligne de flottaison rouge sale
        // Pont et superstructure brûlée.
        k.Col = BurnSoot;
        k.Box(ch, new Vector3(0, 2.05f, 0), new Vector3(w - 0.2f, 0.1f, l * 0.8f));
        k.Col = BurnBase;
        k.BoxMinMax(ch, new Vector3(-2.2f, 2.1f, -3f), new Vector3(2.2f, 5f, 3f));
        k.Col = BurnRust;
        k.BoxMinMax(ch, new Vector3(-1.8f, 5f, -1.5f), new Vector3(1.8f, 7f, 2.5f));
        k.Col = new Color(0.02f, 0.02f, 0.02f);
        for (var i = 0; i < 4; i++) k.Box(ch, new Vector3(-1.2f + i * 0.8f, 6.1f, 2.52f), new Vector3(0.6f, 0.7f, 0.04f));
        for (var i = 0; i < 5; i++) k.Box(ch, new Vector3(2.22f, 3.6f, -2.2f + i * 1.1f), new Vector3(0.04f, 0.8f, 0.7f));
        // Cheminée et mât brisé.
        k.Col = BurnSoot;
        k.Cylinder(ch, new Vector3(0, 7f, -1f), 0.6f, 0.5f, 2.5f, 10);
        k.Col = new Color(0.3f, 0.3f, 0.3f);
        k.Tube(Mats.DetailMetal(), new Vector3(0, 7f, 1.5f), new Vector3(0.3f, 11f, 1.8f), 0.12f, 6);
        k.Tube(Mats.DetailMetal(), new Vector3(0.3f, 11f, 1.8f), new Vector3(2.8f, 9.5f, 3.4f), 0.1f, 6);
        // Pneus de défense le long du bordé.
        k.Col = new Color(0.05f, 0.05f, 0.05f);
        for (var i = 0; i < 6; i++)
            foreach (var x in new[] { -w / 2 - 0.15f, w / 2 + 0.15f })
                k.Cylinder(d, new Vector3(x, 1.2f, -7f + i * 2.4f), 0.35f, 0.35f, 0.3f, 8, true, new Basis(Vector3.Back, Mathf.Pi / 2));
    });

    // ───────────────────────── Insectes ─────────────────────────

    /// <summary>Masse organique (résine) : icosaèdre bosselé, normales lissées, teinte sombre veinée.</summary>
    public static void ResinBlob(MeshKit k, Vector3 c, Vector3 r, int seed)
    {
        var m = Mats.Resin();
        var ico = Ico1;
        var s = seed * 1.37f;
        Vector3 D(Vector3 q)
        {
            var bump = 1f + 0.2f * Mathf.Sin(q.X * 5.3f + s) * Mathf.Cos(q.Z * 4.7f + s * 1.2f) + 0.1f * Mathf.Sin(q.Y * 9f + s * 0.6f);
            return c + new Vector3(q.X * r.X, q.Y * r.Y, q.Z * r.Z) * bump;
        }
        Color C(Vector3 q)
        {
            var t = 0.5f + 0.5f * Mathf.Sin(q.X * 11f + q.Y * 7f + s);
            return new Color(0.05f, 0.055f, 0.035f).Lerp(new Color(0.12f, 0.1f, 0.05f), t * 0.6f);
        }
        for (var i = 0; i < ico.Length; i += 3)
        {
            Vector3 a = D(ico[i]), b = D(ico[i + 1]), e = D(ico[i + 2]);
            k.TriSmooth(m, a, b, e, ico[i], ico[i + 1], ico[i + 2], C(ico[i]), C(ico[i + 1]), C(ico[i + 2]));
        }
    }

    /// <summary>Grappe d'œufs nichée dans la résine.</summary>
    public static void EggCluster(MeshKit k, Vector3 c, float scale, int seed)
    {
        ResinBlob(k, c + Vector3.Up * 0.1f * scale, new Vector3(1.3f, 0.45f, 1.1f) * scale, seed);
        var n = 7 + seed % 6;
        for (var i = 0; i < n; i++)
        {
            var a = H(i, seed, 61) * Mathf.Tau;
            var rr = H(i, seed, 62) * 0.85f * scale;
            var s = (0.32f + H(i, seed, 63) * 0.18f) * scale;
            var p = c + new Vector3(Mathf.Cos(a) * rr, 0.35f * scale + s * 0.6f, Mathf.Sin(a) * rr);
            var col = new Color(0.62f, 0.62f, 0.4f).Lerp(new Color(0.42f, 0.48f, 0.24f), H(i, seed, 64));
            var ax = new Vector3(H(i, seed, 65) - 0.5f, 0, H(i, seed, 66) - 0.5f);
            ax = ax.LengthSquared() < 0.01f ? Vector3.Right : ax.Normalized();
            var tilt = new Basis(ax, 0.35f);
            for (var t = 0; t < Ico1.Length; t += 3)
            {
                Vector3 P(Vector3 q) => p + tilt * new Vector3(q.X * s * 0.75f, q.Y * s * 1.15f, q.Z * s * 0.75f);
                k.TriSmooth(Mats.Eggs(), P(Ico1[t]), P(Ico1[t + 1]), P(Ico1[t + 2]), tilt * Ico1[t], tilt * Ico1[t + 1], tilt * Ico1[t + 2], col, col, col.Lightened(0.05f));
            }
        }
    }

    /// <summary>
    /// Toile géante tendue entre des points d'ancrage (nappe translucide + fils porteurs qui fléchissent).
    /// </summary>
    public static void Web(MeshKit k, Vector3[] anchors, int seed)
    {
        var sheet = Mats.WebSheet();
        var strand = Mats.WebStrand();
        var c = Vector3.Zero;
        foreach (var a in anchors) c += a;
        c /= anchors.Length;
        c += Vector3.Down * 1.2f;   // la nappe s'affaisse sous son poids
        k.Col = Colors.White;
        for (var i = 0; i < anchors.Length; i++)
        {
            var a = anchors[i];
            var b = anchors[(i + 1) % anchors.Length];
            // Bord affaissé entre deux ancrages.
            var mid = (a + b) / 2 + Vector3.Down * (a.DistanceTo(b) * 0.08f) + (c - (a + b) / 2) * 0.12f;
            k.Tri(sheet, a, mid, c, c + Vector3.Up);
            k.Tri(sheet, mid, b, c, c + Vector3.Up);
            // Fils porteurs (radiaux) et bord.
            for (var s = 0; s < 6; s++)
            {
                var t0 = s / 6f; var t1 = (s + 1) / 6f;
                Vector3 Sag(Vector3 p, Vector3 q, float t) => p.Lerp(q, t) + Vector3.Down * (Mathf.Sin(t * Mathf.Pi) * p.DistanceTo(q) * 0.05f);
                k.Tube(strand, Sag(a, c, t0), Sag(a, c, t1), 0.035f, 3);
                k.Tube(strand, Sag(a, mid, t0), Sag(a, mid, t1), 0.025f, 3);
                k.Tube(strand, Sag(mid, b, t0), Sag(mid, b, t1), 0.025f, 3);
            }
            // Fils secondaires entre radiaux (spirale grossière).
            for (var r = 1; r < 5; r++)
            {
                var t = r / 5f;
                k.Tube(strand, a.Lerp(c, t), b.Lerp(c, t) + Vector3.Down * 0.1f, 0.015f, 3);
            }
        }
    }
}
