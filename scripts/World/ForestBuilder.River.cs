using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Eau de la vallée : rivière en ruban (courant, écume des rapides et du bassin du barrage, gués plus clairs), retenue
/// au-dessus du barrage, déversoir. Barrage-poids en béton (couronnement praticable, passerelle sur le déversoir,
/// maison de vannes, conduites forcées) ; pont routier en béton sur deux piles (tablier praticable).
/// </summary>
public static partial class ForestBuilder
{
    // ───────────────────────── Rivière ─────────────────────────

    private static void BuildRiver(Ctx c)
    {
        var mat = Mats.ForestRiver(c.Time);
        // Profil en travers : décalage relatif à la demi-largeur, opacité.
        var cols = new (float off, float abs, float a)[] { (-1f, 2.2f, 0.35f), (-1f, -1.4f, 0.8f), (-0.45f, 0f, 0.93f), (0f, 0f, 0.96f), (0.45f, 0f, 0.93f), (1f, -1.4f, 0.8f), (1f, 2.2f, 0.35f) };
        const float step = 2.5f;
        var s = 0f;
        var prevZ = DamToe + 0.5f;
        for (var z = prevZ; z < 262f; z += step)
        {
            var z1 = z + step;
            s += step * Mathf.Sqrt(1f + RiverSlope(z) * RiverSlope(z));
            var s0 = s - step;
            Row(z, out var c0, out var n0, out var y0, out var hw0);
            Row(z1, out var c1, out var n1, out var y1, out var hw1);
            var k = c.Ground.At(new Vector3(c0.X, 0, z));
            for (var i = 0; i + 1 < cols.Length; i++)
            {
                Vector3 P(Vector2 cc, Vector2 nn, float y, float hw, int j) => new Vector3(cc.X, y, cc.Y) + new Vector3(nn.X, 0, nn.Y) * (cols[j].off * hw + Mathf.Sign(cols[j].off) * cols[j].abs);
                float U(float hw, int j) => cols[j].off * hw + Mathf.Sign(cols[j].off) * cols[j].abs;
                var a = P(c0, n0, y0, hw0, i);
                var b = P(c1, n1, y1, hw1, i);
                var e = P(c1, n1, y1, hw1, i + 1);
                var f = P(c0, n0, y0, hw0, i + 1);
                k.QuadC(mat, a, b, e, f, new Vector2(s0, U(hw0, i)), new Vector2(s, U(hw1, i)), new Vector2(s, U(hw1, i + 1)), new Vector2(s0, U(hw0, i + 1)),
                    WaterCol(z, cols[i]), WaterCol(z1, cols[i]), WaterCol(z1, cols[i + 1]), WaterCol(z, cols[i + 1]), Vector3.Up);
            }
        }

        // Retenue : nappe calme au-dessus du barrage, jusqu'aux collines lointaines.
        var lake = new MeshKit();
        var lc = new Color(0, 0.03f, 0.05f, 0.97f);
        for (var x = -420f; x < 380f; x += 100f)
        for (var z = DamUp + 0.4f; z > -1100f; z -= 100f)
        {
            var z1 = Mathf.Max(z - 100f, -1100f);
            lake.QuadC(mat, new Vector3(x, LakeY, z), new Vector3(x + 100, LakeY, z), new Vector3(x + 100, LakeY, z1), new Vector3(x, LakeY, z1),
                new Vector2(-z, x), new Vector2(-z, x + 100), new Vector2(-z1, x + 100), new Vector2(-z1, x), lc, lc, lc, lc, Vector3.Up);
        }
        c.Root.AddChild(new MeshInstance3D { Name = "Lake", Mesh = lake.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Section de la rivière à l'abscisse z : centre, normale latérale (vers l'est), niveau, demi-largeur.</summary>
    private static void Row(float z, out Vector2 center, out Vector2 normal, out float y, out float hw)
    {
        center = new Vector2(RiverX(z), z);
        normal = new Vector2(1f, -RiverSlope(z)).Normalized();
        y = WaterY(z);
        hw = HalfWidth(z);
    }

    /// <summary>Couleur de sommet de l'eau : R écume, G vitesse, A opacité.</summary>
    private static Color WaterCol(float z, (float off, float abs, float a) col)
    {
        var rapids = RapidsAt(z);
        var ford = FordAt(z);
        var toe = Mathf.SmoothStep(-176f, DamToe, z);
        var edge = Mathf.Abs(col.off) > 0.9f ? 0.25f : 0f;
        var n = 0.5f + 0.5f * Mathf.Sin(z * 0.31f + col.off * 2.3f);
        var foam = Mathf.Clamp(rapids * (0.55f + 0.35f * n) + ford * 0.3f + toe * 0.95f + edge * (0.3f + rapids), 0f, 1f);
        var speed = Mathf.Clamp(0.22f + 0.7f * rapids + 0.25f * ford + 0.6f * toe, 0f, 1f);
        var a = col.a * Mathf.Lerp(1f, 0.75f, ford);
        return new Color(foam, speed, 0f, a);
    }

    // ───────────────────────── Barrage ─────────────────────────

    /// <summary>Déversoir : demi-largeur, seuil.</summary>
    private const float SpillHalf = 10f, SpillY = 22.1f;

    private static void BuildDam(Ctx c)
    {
        var cx = RiverX(DamZ);
        var concrete = Mats.Concrete(new Color(0.62f, 0.62f, 0.6f), 7);
        var k = c.Solid.At(new Vector3(cx, 0, DamZ));
        const float baseY = 3f;
        const float crestW = 5f;
        var crestZ1 = DamUp + crestW;   // arête aval du couronnement
        // Profil : parement amont vertical, couronnement, parement aval incliné jusqu'au pied.
        void Section(float x0, float x1, float top, bool spill)
        {
            k.Col = new Color(0.78f, 0.77f, 0.74f);
            // Parement amont.
            k.OrientedQuad(concrete, new Vector3(x0, baseY, DamUp), new Vector3(x0, top, DamUp), new Vector3(x1, top, DamUp), new Vector3(x1, baseY, DamUp), Vector3.Forward);
            // Couronnement (ou seuil arrondi du déversoir).
            var crestEnd = spill ? DamUp + 2.5f : crestZ1;
            k.OrientedQuad(concrete, new Vector3(x0, top, DamUp), new Vector3(x0, top, crestEnd), new Vector3(x1, top, crestEnd), new Vector3(x1, top, DamUp), Vector3.Up);
            // Parement aval (coursier du déversoir : plus long et plus doux).
            var toeZ = spill ? DamToe + 1.5f : DamToe;
            k.Col = spill ? new Color(0.55f, 0.56f, 0.52f) : new Color(0.72f, 0.71f, 0.68f);
            k.OrientedQuad(concrete, new Vector3(x0, top, crestEnd), new Vector3(x0, baseY + 4f, toeZ), new Vector3(x1, baseY + 4f, toeZ), new Vector3(x1, top, crestEnd), new Vector3(0, 1, 1));
            k.OrientedQuad(concrete, new Vector3(x0, baseY + 4f, toeZ), new Vector3(x0, baseY, toeZ + 1f), new Vector3(x1, baseY, toeZ + 1f), new Vector3(x1, baseY + 4f, toeZ), new Vector3(0, 0.3f, 1));
        }
        var xl = cx - DamHalf;
        var xr = cx + DamHalf;
        var s0 = cx - SpillHalf;
        var s1 = cx + SpillHalf;
        Section(xl, s0 - 1.2f, DamCrest, false);
        Section(s1 + 1.2f, xr, DamCrest, false);
        Section(s0, s1, SpillY, true);
        // Bajoyers du déversoir (murs latéraux) et piles de la passerelle.
        foreach (var sx in new[] { s0 - 0.6f, s1 + 0.6f })
        {
            k.Col = new Color(0.7f, 0.69f, 0.66f);
            k.Box(concrete, new Vector3(sx, (baseY + DamCrest + 1f) / 2f, (DamUp + DamToe) / 2f), new Vector3(1.2f, DamCrest + 1f - baseY, DamToe - DamUp));
        }
        for (var i = 1; i < 4; i++)
        {
            var px = Mathf.Lerp(s0, s1, i / 4f);
            k.Box(concrete, new Vector3(px, (SpillY + DamCrest) / 2f + 0.2f, DamUp + 1.3f), new Vector3(1f, DamCrest - SpillY + 0.4f, 2.4f));
        }
        // Passerelle sur le déversoir.
        k.Col = new Color(0.6f, 0.6f, 0.58f);
        k.Box(concrete, new Vector3(cx, DamCrest - 0.25f, DamUp + 1.3f), new Vector3(SpillHalf * 2f + 2.4f, 0.5f, 2.6f));
        // Garde-corps du couronnement et de la passerelle (montants et lisses, rouille).
        var steel = Mats.Worn(new Color(0.28f, 0.24f, 0.2f), 0.7f, 0.5f);
        k.Col = Colors.White;
        foreach (var (z, x0, x1) in new[] { (DamUp + 0.15f, xl + 6f, xr - 6f), (crestZ1 - 0.15f, xl + 6f, s0 - 1.2f), (crestZ1 - 0.15f, s1 + 1.2f, xr - 6f), (DamUp + 2.5f, s0 - 1.2f, s1 + 1.2f) })
        {
            var y = z == DamUp + 2.5f ? DamCrest : DamCrest;
            for (var x = x0; x <= x1; x += 2.5f) k.Box(steel, new Vector3(x, y + 0.55f, z), new Vector3(0.07f, 1.1f, 0.07f));
            k.Box(steel, new Vector3((x0 + x1) / 2f, y + 1.08f, z), new Vector3(x1 - x0, 0.06f, 0.06f));
            k.Box(steel, new Vector3((x0 + x1) / 2f, y + 0.6f, z), new Vector3(x1 - x0, 0.05f, 0.05f));
        }
        // Maison des vannes sur le couronnement (rive est) : béton, porte rouillée, fenêtre aveugle.
        var gh = new Vector3(s1 + 9f, DamCrest, DamUp + crestW / 2f);
        k.Col = new Color(0.66f, 0.64f, 0.6f);
        k.Box(concrete, gh + new Vector3(0, 2.1f, 0), new Vector3(6f, 4.2f, 4.2f));
        k.Box(concrete, gh + new Vector3(0, 4.35f, 0), new Vector3(6.6f, 0.3f, 4.8f));
        var dark = Mats.Detail();
        k.Col = new Color(0.2f, 0.12f, 0.08f);
        k.Box(dark, gh + new Vector3(-1.4f, 1.1f, 2.11f), new Vector3(1.1f, 2.1f, 0.04f));
        k.Col = new Color(0.04f, 0.045f, 0.05f);
        k.Box(dark, gh + new Vector3(1.3f, 2.6f, 2.11f), new Vector3(1.4f, 0.9f, 0.04f));
        // Coulures et salissures en bandes sur le parement aval (relief non solide).
        k.Solid = false;
        var r = new RandomNumberGenerator { Seed = 9101 };
        for (var x = xl + 4f; x < xr - 4f; x += r.RandfRange(1.5f, 4f))
        {
            if (x > s0 - 2f && x < s1 + 2f) continue;
            var len = r.RandfRange(0.3f, 0.85f);
            var t0 = 0.02f;
            var a = new Vector3(x, Mathf.Lerp(DamCrest, baseY + 4f, t0), Mathf.Lerp(crestZ1, DamToe, t0)) + new Vector3(0, 0.02f, 0.03f);
            var b = new Vector3(x, Mathf.Lerp(DamCrest, baseY + 4f, len), Mathf.Lerp(crestZ1, DamToe, len)) + new Vector3(0, 0.02f, 0.03f);
            var wdt = r.RandfRange(0.25f, 0.9f);
            k.Col = new Color(0.3f, 0.3f, 0.28f) * r.RandfRange(0.7f, 1.1f);
            k.OrientedQuad(concrete, a - new Vector3(wdt, 0, 0), b - new Vector3(wdt * 0.6f, 0, 0), b + new Vector3(wdt * 0.6f, 0, 0), a + new Vector3(wdt, 0, 0), new Vector3(0, 1, 1));
        }
        k.Solid = true;
        // Conduites forcées : du parement aval (rive est) jusqu'à la centrale.
        var pipe = Mats.Worn(new Color(0.24f, 0.2f, 0.16f), 0.65f, 0.6f);
        foreach (var px in new[] { s1 + 4.5f, s1 + 8f })
        {
            var from = new Vector3(px, 15.5f, Mathf.Lerp(crestZ1, DamToe, 0.62f) + 0.5f);
            var to = new Vector3(px + 3f, PadHeight(PlantYard) + 2.6f, PlantZ - 6.5f);
            k.Col = Colors.White;
            k.Tube(pipe, from, to, 1.05f, 12, true);
            for (var t = 0.15f; t < 1f; t += 0.3f)
                k.Tube(pipe, from.Lerp(to, t) - (to - from).Normalized() * 0.15f, from.Lerp(to, t) + (to - from).Normalized() * 0.15f, 1.18f, 12, true);
            // Massif d'ancrage.
            k.Col = new Color(0.6f, 0.6f, 0.57f);
            k.Box(concrete, from.Lerp(to, 0.55f) + new Vector3(0, -2.2f, 0), new Vector3(3.2f, 3.6f, 3f));
        }
        // Champ de flux : le barrage est un obstacle haut (les grimpeurs l'escaladent).
        FlowBlock(c, new Vector3(cx, 0, (DamUp + DamToe) / 2f), new Vector3(DamHalf * 2f, DamCrest - 8f, DamToe - DamUp));

        // Lame d'eau du déversoir : du seuil au bassin, écumante.
        var water = Mats.ForestRiver(c.Time);
        var wk = c.Ground.At(new Vector3(cx, 0, DamZ));
        // Coursier : du seuil (DamUp + 2,5) au pied (DamToe + 1,5) ; la lame plonge dans le bassin à son niveau.
        var pool = WaterY(DamToe + 3f);
        var faceZ0 = DamUp + 2.5f;
        var faceZ1 = DamToe + 1.5f;
        var hitZ = Mathf.Lerp(faceZ0, faceZ1, (SpillY - pool) / (SpillY - (baseY + 4f)));
        var steps = new[] { (DamUp - 0.5f, LakeY), (faceZ0, SpillY + 0.25f), (faceZ0 + 1.5f, SpillY - 1.3f), (hitZ, pool + 0.25f), (hitZ + 4f, pool + 0.04f) };
        for (var i = 0; i + 1 < steps.Length; i++)
        {
            var (za, ya) = steps[i];
            var (zb, yb) = steps[i + 1];
            var ca = new Color(i == 0 ? 0.1f : 1f, i == 0 ? 0.5f : 1f, 0, 0.97f);
            var cb = new Color(1f, 1f, 0, 0.97f);
            wk.QuadC(water, new Vector3(s0, ya, za), new Vector3(s1, ya, za), new Vector3(s1, yb, zb), new Vector3(s0, yb, zb),
                new Vector2(za * 1.6f, s0), new Vector2(za * 1.6f, s1), new Vector2(zb * 1.6f, s1), new Vector2(zb * 1.6f, s0), ca, ca, cb, cb, Vector3.Up);
        }
    }

    // ───────────────────────── Pont ─────────────────────────

    private static void BuildBridge(Ctx c)
    {
        var deckY = DeckY();
        var center = new Vector3(BridgeC.X, 0, BridgeC.Y);
        var along = new Vector3(BridgeDir.X, 0, BridgeDir.Y);
        var side = new Vector3(along.Z, 0, -along.X); // repère direct (X, Y, Z = axe du pont)
        var xf = new Transform3D(new Basis(side, Vector3.Up, along), center);
        var k = c.Solid.At(center);
        k.Xf = xf;
        var concrete = Mats.Concrete(new Color(0.58f, 0.58f, 0.56f), 11);
        var L = BridgeHalf + 1f;
        const float thick = 1.2f;
        // Tablier (dalle et poutres de rive), trottoirs.
        k.Col = new Color(0.66f, 0.65f, 0.62f);
        k.Box(concrete, new Vector3(0, deckY - thick / 2f, 0), new Vector3(BridgeHw * 2f, thick, L * 2f));
        k.Col = new Color(0.56f, 0.55f, 0.52f);
        foreach (var sx in new[] { -1f, 1f })
            k.Box(concrete, new Vector3(sx * (BridgeHw - 0.35f), deckY - thick - 0.45f, 0), new Vector3(0.7f, 0.9f, L * 2f));
        var walk = Mats.Sidewalk();
        k.Col = new Color(0.8f, 0.79f, 0.76f);
        foreach (var sx in new[] { -1f, 1f }) k.Box(walk, new Vector3(sx * (BridgeHw - 0.7f), deckY + 0.11f, 0), new Vector3(1.4f, 0.22f, L * 2f));
        // Chaussée (enrobé fatigué).
        k.Col = new Color(0.82f, 0.82f, 0.8f);
        k.OrientedQuad(Mats.Asphalt(), new Vector3(-BridgeHw + 1.4f, deckY + 0.01f, -L), new Vector3(-BridgeHw + 1.4f, deckY + 0.01f, L),
            new Vector3(BridgeHw - 1.4f, deckY + 0.01f, L), new Vector3(BridgeHw - 1.4f, deckY + 0.01f, -L), Vector3.Up);
        // Piles dans le lit et culées.
        var bed = WaterY(BridgeZ) - Depth(BridgeZ) - 1.5f;
        k.Col = new Color(0.6f, 0.59f, 0.56f);
        foreach (var sz in new[] { -8f, 8f })
        {
            k.Box(concrete, new Vector3(0, (bed + deckY - thick - 0.9f) / 2f, sz), new Vector3(BridgeHw * 1.6f, deckY - thick - 0.9f - bed, 1.4f));
            // Avant-bec (vers l'amont) et chapeau.
            k.Box(concrete, new Vector3(0, deckY - thick - 0.7f, sz), new Vector3(BridgeHw * 2f - 0.4f, 0.5f, 1.8f));
        }
        foreach (var sz in new[] { -L + 0.8f, L - 0.8f })
            k.Box(concrete, new Vector3(0, (bed + 3f + deckY - thick) / 2f, sz), new Vector3(BridgeHw * 2f + 0.6f, deckY - thick - bed - 3f, 1.6f));
        // Garde-corps : muret bas et lisse d'acier ; un tronçon arraché (choc), lisse tordue pendante.
        var steel = Mats.Worn(new Color(0.3f, 0.3f, 0.29f), 0.6f, 0.6f);
        foreach (var sx in new[] { -1f, 1f })
        {
            k.Col = new Color(0.7f, 0.69f, 0.66f);
            k.Box(concrete, new Vector3(sx * (BridgeHw - 0.15f), deckY + 0.45f, 0), new Vector3(0.3f, 0.7f, L * 2f));
            k.Col = Colors.White;
            for (var z = -L + 1f; z <= L - 1f; z += 2f)
            {
                if (sx > 0 && z > 2f && z < 9f) continue;
                k.Box(steel, new Vector3(sx * (BridgeHw - 0.15f), deckY + 1.05f, z), new Vector3(0.08f, 0.5f, 0.08f));
            }
            if (sx > 0)
            {
                k.Box(steel, new Vector3(BridgeHw - 0.15f, deckY + 1.3f, (-L + 2.5f) / 2f), new Vector3(0.1f, 0.08f, L + 2.5f - 1f));
                k.Box(steel, new Vector3(BridgeHw - 0.15f, deckY + 1.3f, (L + 9f) / 2f), new Vector3(0.1f, 0.08f, L - 9f - 1f));
                k.Box(steel, new Vector3(BridgeHw + 0.4f, deckY - 0.4f, 5.5f), new Vector3(0.1f, 0.08f, 6.5f), new Basis(Vector3.Right, 0.9f));
            }
            else k.Box(steel, new Vector3(-BridgeHw + 0.15f, deckY + 1.3f, 0), new Vector3(0.1f, 0.08f, L * 2f - 1f));
        }
        k.Xf = Transform3D.Identity;
    }

    /// <summary>Profondeur du pied de la centrale (z) : contre le pied du barrage.</summary>
    private const float PlantZ = -184f;
}
