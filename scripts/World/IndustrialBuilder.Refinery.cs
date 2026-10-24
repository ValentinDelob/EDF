using Godot;

namespace Rempart;

/// <summary>
/// Raffinerie : parc de six cuves de stockage dans une cuvette de rétention (merlons de terre praticables), sphères
/// de gaz, réservoirs de GPL, cuves de procédé, colonnes de distillation, four, torchère encore allumée, et deux
/// racks de tuyauteries dont le dessus est une passerelle (tours d'escalier aux extrémités).
/// Toutes les citernes sont explosives (<see cref="Destructibles.AddTank"/>).
/// </summary>
public static partial class IndustrialBuilder
{
    /// <summary>Cuves de stockage : centre (x, z) et variante (rayon, hauteur : <see cref="Props.StorageTankDims"/>).</summary>
    private static readonly (float x, float z, int v)[] StorageTanks =
    {
        (32, -182, 0), (63, -182, 1), (93, -182, 0),
        (34, -140, 2), (66, -140, 0), (96, -140, 1),
    };

    private static readonly Color[] TankPaints = { new(0.6f, 0.6f, 0.57f), new(0.33f, 0.37f, 0.31f), new(0.52f, 0.49f, 0.42f) };

    /// <summary>Cuvette de rétention : x0, z0, x1, z1.</summary>
    private static readonly Rect2 Bund = new(14, -200, 94, 78);

    private static void BuildTankFarm(Ctx c)
    {
        for (var i = 0; i < StorageTanks.Length; i++)
        {
            var (x, z, v) = StorageTanks[i];
            var (r, h) = Props.StorageTankDims[v];
            var p = new Vector3(x, 0, z);
            // Orientation variée (escalier, tuyauteries), sans tirage aléatoire.
            var yaw = i * 1.3f;
            var spec = new Destructibles.TankSpec
            {
                Wreck = Props.StorageTankWreck(v), Blast = 20f, Damage = 520f, Core = h * 0.55f, FireScale = 3.4f, FireY = 1.2f, Paint = TankPaints[v],
            };
            Destructibles.AddTank(c.Root, c.Body, Props.StorageTank(v), new Transform3D(new Basis(Vector3.Up, yaw), p), 650f + v * 50f, spec);
            FlowBlock(c, p, new Vector3(r * 2 + 1f, h, r * 2 + 1f));
            Take(c, p, r + 1.6f, r + 1.6f);
            c.Tanks.Add(p);
        }
        // Merlons de terre (talus praticables) autour de la cuvette, deux passages pour les engins.
        var k = c.Solid.At(new Vector3(Bund.GetCenter().X, 0, Bund.GetCenter().Y));
        var x0 = Bund.Position.X;
        var x1 = Bund.End.X;
        var z0 = Bund.Position.Y;
        var z1 = Bund.End.Y;
        Berm(k, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0));
        Berm(k, new Vector3(x1, 0, z0), new Vector3(x1, 0, z1));
        Berm(k, new Vector3(x1, 0, z1), new Vector3(52, 0, z1));
        Berm(k, new Vector3(42, 0, z1), new Vector3(x0, 0, z1));       // passage sud (x 42 → 52)
        Berm(k, new Vector3(x0, 0, z1), new Vector3(x0, 0, -156));
        Berm(k, new Vector3(x0, 0, -166), new Vector3(x0, 0, z0));     // passage ouest (z -166 → -156)
        // Fond de cuvette : gravier souillé, flaques d'hydrocarbures.
        var g = c.Ground.At(new Vector3(Bund.GetCenter().X, 0, Bund.GetCenter().Y));
        g.Col = new Color(0.36f, 0.34f, 0.31f);
        g.OrientedQuad(Mats.Gravel(), new Vector3(x0 + 2, 0.014f, z0 + 2), new Vector3(x1 - 2, 0.014f, z0 + 2), new Vector3(x1 - 2, 0.014f, z1 - 2), new Vector3(x0 + 2, 0.014f, z1 - 2), Vector3.Up);
        for (var i = 0; i < 7; i++)
        {
            var p = new Vector3(x0 + 6 + RuinKit.Hash(i, 1, 31) * (Bund.Size.X - 12), 0, z0 + 6 + RuinKit.Hash(i, 2, 31) * (Bund.Size.Y - 12));
            c.Ruins.Decal(Mats.Decal.Oil, p, 4f + RuinKit.Hash(i, 3, 31) * 5f, i * 0.7f, 0.03f + i * 0.0003f, 1.4f);
        }
    }

    /// <summary>Merlon trapézoïdal (base 4,6 m, crête 0,9 m, hauteur 1,3 m) entre deux points, collision exacte.</summary>
    private static void Berm(MeshKit k, Vector3 a, Vector3 b)
    {
        var dir = (b - a).Normalized();
        var side = new Vector3(-dir.Z, 0, dir.X);
        const float hb = 2.3f, ht = 0.45f, h = 1.3f;
        var m = Mats.GroundTint();
        var lo = new Color(0.36f, 0.33f, 0.27f);
        var hi = new Color(0.42f, 0.4f, 0.32f);
        // Prolongement aux extrémités : les angles se recouvrent.
        a -= dir * hb;
        b += dir * hb;
        var up = Vector3.Up;
        foreach (var sg in new[] { -1f, 1f })
        {
            var s0 = side * sg * hb;
            var s1 = side * sg * ht + up * h;
            k.QuadC(m, a + s0, b + s0, b + s1, a + s1, Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, lo, lo, hi, hi, side * sg + up);
        }
        k.QuadC(m, a - side * ht + up * h, b - side * ht + up * h, b + side * ht + up * h, a + side * ht + up * h, Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero, hi, hi, hi, hi, up);
        // Bouts (pentes vers l'extérieur).
        foreach (var (p, d) in new[] { (a, -dir), (b, dir) })
        {
            k.Col = lo;
            k.Tri(m, p - side * hb, p + side * hb, p + up * h - side * ht, p + d - up);
            k.Tri(m, p + side * hb, p + side * ht + up * h, p - side * ht + up * h, p + d - up);
        }
    }

    private static void BuildRefinery(Ctx c)
    {
        // Sphères de gaz liquéfié.
        foreach (var x in new[] { 152f, 178f, 204f })
        {
            var p = new Vector3(x, 0, -124);
            var spec = new Destructibles.TankSpec { Wreck = Props.GasSphereWreck(), Blast = 26f, Damage = 600f, Core = Props.SphereY, FireScale = 2.8f, FireY = 0.6f, Paint = TankPaints[0] };
            Destructibles.AddTank(c.Root, c.Body, Props.GasSphere(), new Transform3D(Basis.Identity, p), 800f, spec);
            FlowBlock(c, p, new Vector3(Props.SphereR * 2, Props.SphereY + Props.SphereR, Props.SphereR * 2));
            Take(c, new Rect2(x - 9f, -133f, 21f, 18f));
            c.Tanks.Add(p);
        }
        // Réservoirs de GPL horizontaux, serrés : ils sautent en cascade.
        for (var i = 0; i < 4; i++)
        {
            var p = new Vector3(150 + i * 5.2f, 0, -152);
            var spec = new Destructibles.TankSpec { Wreck = Props.BulletTankWreck(), Blast = 15f, Damage = 360f, Core = Props.BulletAxisY, FireScale = 1.8f, FireY = 1.6f, Paint = TankPaints[i % 2 == 0 ? 0 : 2] };
            Destructibles.AddTank(c.Root, c.Body, Props.BulletTank(i % 2), new Transform3D(Basis.Identity, p), 320f, spec);
            c.Tanks.Add(p);
        }
        FlowBlock(c, new Vector3(157.8f, 0, -152), new Vector3(20f, 5f, 16f));
        Take(c, new Rect2(146, -160, 24, 16));
        // Cuves de procédé.
        foreach (var (x, z) in new[] { (196f, -184f), (205f, -176f), (196f, -168f) })
        {
            var p = new Vector3(x, 0, z);
            var spec = new Destructibles.TankSpec { Wreck = Props.ProcessVesselWreck(), Blast = 12f, Damage = 260f, Core = 5f, FireScale = 1.6f, FireY = 1.7f, Paint = TankPaints[0] };
            Destructibles.AddTank(c.Root, c.Body, Props.ProcessVessel((int)x % 2), new Transform3D(new Basis(Vector3.Up, x * 0.1f), p), 300f, spec);
            FlowBlock(c, p, new Vector3(5f, Props.VesselH, 5f));
            Take(c, p, 3.4f, 3.4f);
            c.Tanks.Add(p);
        }

        // Colonnes de distillation (ouvrages fixes) : plateformes annulaires, échelles, tuyauteries.
        Column(c, new Vector3(150, 0, -205), 2.6f, 42f);
        Column(c, new Vector3(166, 0, -207), 2f, 34f);
        Column(c, new Vector3(182, 0, -212), 3f, 28f);
        // Four (destructible) et sa cheminée en acier.
        var sH = new Vector3(16, 9f, 12);
        AddPlant(c, new Vector3(160, 0, -176), sH, HeaterMesh(sH), Mats.Detail(), 1.3f);
        Flare(c, new Vector3(212, 0, -222));

        // Racks de tuyauteries : nord-sud le long de la route de la raffinerie, est-ouest au sud du parc de cuves.
        PipeRack(c, new Vector3(130, 0, -232), new Vector3(130, 0, -112));
        PipeRack(c, new Vector3(-30, 0, -112), new Vector3(132, 0, -112), roadGap: 0f);
        var k = c.Solid.At(new Vector3(-34, 0, -112));
        StairTower(k, new Transform3D(new Basis(Vector3.Up, -Mathf.Pi / 2), new Vector3(-36.2f, 0, -112)), RackTop, 3.4f);
        Take(c, new Rect2(-41, -114, 10, 4));
        var k2 = c.Solid.At(new Vector3(136, 0, -180));
        StairTower(k2, new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2), new Vector3(136.2f, 0, -180)), RackTop, 1.2f);
        Take(c, new Rect2(131, -184, 10, 8));
    }

    /// <summary>Colonne de distillation : fût, jupe, plateformes annulaires en caillebotis (solides), échelles, tuyau de tête.</summary>
    private static void Column(Ctx c, Vector3 p, float r, float h)
    {
        var k = c.Solid.At(p);
        var paint = Mats.Worn(new Color(0.46f, 0.46f, 0.44f), 0.65f, 0.3f);
        var metal = Mats.DetailMetal();
        k.Col = Colors.White;
        k.Cylinder(paint, p, r * 1.05f, r, h, 24, false);
        k.Cylinder(paint, p + Vector3.Up * h, r, r * 0.4f, r * 0.5f, 24, true);
        k.Col = new Color(0.2f, 0.2f, 0.19f);
        for (var y = 7f; y < h - 2f; y += 7f)
        {
            // Plateforme annulaire sur 3/4 de tour.
            for (var i = 0; i < 18; i++)
            {
                var a0 = 0.3f + 4.7f * i / 18f;
                var a1 = 0.3f + 4.7f * (i + 1) / 18f;
                Vector3 P(float rr, float a) => p + new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                k.Quad(metal, P(r + 0.05f, a0), P(r + 1.4f, a0), P(r + 1.4f, a1), P(r + 0.05f, a1));
                k.Quad(metal, P(r + 0.05f, a1), P(r + 1.4f, a1), P(r + 1.4f, a0), P(r + 0.05f, a0));
            }
            k.Solid = false;
            k.Col = new Color(0.42f, 0.36f, 0.16f);
            for (var i = 0; i <= 18; i += 2)
            {
                var a = 0.3f + 4.7f * i / 18f;
                var b = p + new Vector3(Mathf.Cos(a) * (r + 1.35f), y, Mathf.Sin(a) * (r + 1.35f));
                k.Tube(metal, b, b + Vector3.Up * 1.05f, 0.025f, 4);
            }
            k.Solid = true;
            k.Col = new Color(0.2f, 0.2f, 0.19f);
        }
        k.Solid = false;
        k.Col = new Color(0.42f, 0.36f, 0.16f) * 0.8f;
        foreach (var dz in new[] { -0.22f, 0.22f }) k.Tube(metal, p + new Vector3(r + 0.3f, 0.3f, dz), p + new Vector3(r + 0.3f, h - 1f, dz), 0.025f, 4);
        k.Col = new Color(0.25f, 0.24f, 0.22f);
        k.Tube(metal, p + Vector3.Up * (h + r * 0.4f), p + new Vector3(-r - 2f, h + r * 0.4f, 0), 0.3f, 8);
        k.Tube(metal, p + new Vector3(-r - 2f, h + r * 0.4f, 0), p + new Vector3(-r - 2f, 0.4f, 0), 0.3f, 8);
        k.Solid = true;
        FlowBlock(c, p, new Vector3(r * 2 + 3f, h, r * 2 + 3f));
        Take(c, p, r + 2.5f, r + 2.5f);
    }

    /// <summary>Four de procédé : caisson en tôle sur poteaux, cheminée d'acier haubanée, regards, tuyauteries.</summary>
    private static ArrayMesh HeaterMesh(Vector3 s)
    {
        var k = new MeshKit { Record = true };
        var hx = s.X / 2;
        var hz = s.Z / 2;
        var clad = Mats.Corrugated(new Color(0.36f, 0.35f, 0.33f), 95, 2.4f);
        var metal = Mats.DetailMetal();
        foreach (var (start, along, n, len) in Faces(hx, hz))
        {
            WallBand(k, Mats.Detail(), start, start + along * len, 0, 2.2f, n, 3f, 3f, new Color(0.15f, 0.15f, 0.14f));
            WallBand(k, clad, start, start + along * len, 2.2f, s.Y, n, 3f, 3f, Colors.White);
        }
        k.Col = new Color(0.25f, 0.24f, 0.23f);
        k.Quad(metal, new Vector3(-hx, s.Y, hz), new Vector3(hx, s.Y, hz), new Vector3(hx, s.Y, -hz), new Vector3(-hx, s.Y, -hz));
        k.Frustum(metal, new Vector3(0, s.Y, 0), new Vector2(s.X * 0.6f, s.Z * 0.6f), new Vector2(2.4f, 2.4f), 3f);
        k.Col = new Color(0.12f, 0.11f, 0.1f);
        k.Cylinder(metal, new Vector3(0, s.Y + 3f, 0), 1.1f, 0.95f, 24f, 16, false);
        k.Solid = false;
        k.Col = new Color(0.25f, 0.24f, 0.23f);
        foreach (var (dx, dz) in new[] { (1f, 1f), (-1f, 1f), (0f, -1.4f) })
            k.Tube(metal, new Vector3(0, s.Y + 20f, 0), new Vector3(dx * 9f, s.Y, dz * 7f), 0.02f, 3);
        k.Col = new Color(0.4f, 0.16f, 0.06f);
        for (var x = -hx + 2f; x < hx - 1f; x += 3f)
            k.Box(metal, new Vector3(x, 1.2f, hz + 0.05f), new Vector3(0.5f, 0.4f, 0.1f));
        k.Solid = true;
        return k.Commit();
    }

    /// <summary>Torchère : mât en treillis haubané, flamme encore allumée au sommet (alimentée par une fuite).</summary>
    private static void Flare(Ctx c, Vector3 p)
    {
        const float h = 54f;
        var k = c.Solid.At(p);
        var metal = Mats.DetailMetal();
        k.Col = new Color(0.32f, 0.3f, 0.28f);
        k.Cylinder(metal, p, 0.7f, 0.55f, h, 10);
        k.Solid = false;
        k.Col = new Color(0.3f, 0.2f, 0.16f);
        // Treillis autour du fût, haubans.
        for (var i = 0; i < 3; i++)
        {
            var a = i * Mathf.Tau / 3;
            var foot = p + new Vector3(Mathf.Cos(a) * 3f, 0, Mathf.Sin(a) * 3f);
            k.Tube(metal, foot, p + new Vector3(Mathf.Cos(a) * 1f, h - 4f, Mathf.Sin(a) * 1f), 0.12f, 5);
            k.Tube(metal, p + new Vector3(Mathf.Cos(a) * 1.2f, h * 0.6f, Mathf.Sin(a) * 1.2f), p + new Vector3(Mathf.Cos(a) * 14f, 0, Mathf.Sin(a) * 14f), 0.02f, 3);
        }
        for (var y = 4f; y < h - 4f; y += 4f)
        {
            var rr = Mathf.Lerp(3f, 1f, y / (h - 4f));
            k.Cylinder(metal, p + Vector3.Up * y, rr, rr, 0.12f, 6, false);
        }
        k.Col = new Color(0.08f, 0.07f, 0.06f);
        k.Cylinder(metal, p + Vector3.Up * h, 0.9f, 0.8f, 1.6f, 10);
        k.Solid = true;
        c.Ruins.Fire(p + Vector3.Up * (h + 1.6f), 2.6f);
        c.Ruins.Smolder(p + Vector3.Up * (h + 5f), 2.2f);
        FlowBlock(c, p, new Vector3(6, h, 6));
        Take(c, p, 4f, 4f);
    }

    /// <summary>Hauteur du caillebotis au sommet des racks.</summary>
    private const float RackTop = 7.3f;

    /// <summary>
    /// Rack de tuyauteries : portiques en acier tous les 6 m (deux niveaux), faisceau de tuyaux sur le niveau bas,
    /// quelques gros tuyaux et un caillebotis praticable sur le niveau haut, garde-corps. Aucun poteau sur une route.
    /// </summary>
    private static void PipeRack(Ctx c, Vector3 a, Vector3 b, float roadGap = 0f)
    {
        var dir = (b - a).Normalized();
        var side = new Vector3(-dir.Z, 0, dir.X);
        var len = (b - a).Length();
        var metal = Mats.DetailMetal();
        var grating = Mats.Grating();
        const float hw = 2.2f, t1 = 4.6f;
        var bents = Mathf.FloorToInt(len / 6f);
        for (var i = 0; i <= bents; i++)
        {
            var p = a + dir * (i * len / bents);
            var k = c.Solid.At(p);
            var onRoad = Mathf.Abs(p.X) < RoadHalf + 1f || Mathf.Abs(p.Z + 100f) < RoadHalf + 1f || Mathf.Abs(Mathf.Abs(p.X) - 118f) < RoadHalf + 1f;
            k.Col = new Color(0.25f, 0.24f, 0.22f);
            if (!onRoad)
                foreach (var sg in new[] { -1f, 1f })
                {
                    var q = p + side * hw * sg;
                    k.Box(metal, q + Vector3.Up * RackTop / 2, new Vector3(0.3f, RackTop, 0.3f));
                    k.Col = new Color(0.3f, 0.3f, 0.29f);
                    k.Box(Mats.Detail(), q + Vector3.Up * 0.2f, new Vector3(0.9f, 0.4f, 0.9f));
                    k.Col = new Color(0.25f, 0.24f, 0.22f);
                    Take(c, q, 0.6f, 0.6f, 0f);
                }
            foreach (var y in new[] { t1, RackTop - 0.2f })
                k.Box(metal, p + Vector3.Up * y, side * hw * 2 + dir * 0.3f + Vector3.Up * 0.3f);
        }
        // Longerons, faisceau de tuyaux, caillebotis supérieur : par tronçons de 24 m (secteurs).
        for (var s = 0f; s < len; s += 24f)
        {
            var s1 = Mathf.Min(len, s + 24f);
            var p0 = a + dir * s;
            var p1 = a + dir * s1;
            var k = c.Solid.At((p0 + p1) / 2);
            k.Col = new Color(0.25f, 0.24f, 0.22f);
            foreach (var sg in new[] { -1f, 1f })
                foreach (var y in new[] { t1, RackTop - 0.2f })
                    k.Tube(metal, p0 + side * hw * sg + Vector3.Up * y, p1 + side * hw * sg + Vector3.Up * y, 0.16f, 4);
            // Tuyaux du niveau bas (diamètres variés, isolés ou rouillés).
            for (var i = 0; i < 7; i++)
            {
                var off = -hw + 0.4f + i * (hw * 2 - 0.8f) / 6f;
                var rr = i % 3 == 0 ? 0.32f : i % 3 == 1 ? 0.2f : 0.13f;
                k.Col = i % 4 == 0 ? new Color(0.5f, 0.5f, 0.47f) : i % 4 == 1 ? new Color(0.28f, 0.17f, 0.12f) : new Color(0.22f, 0.22f, 0.21f);
                k.Tube(metal, p0 + side * off + Vector3.Up * (t1 + 0.15f + rr), p1 + side * off + Vector3.Up * (t1 + 0.15f + rr), rr, 8);
            }
            // Caillebotis (solide) et deux gros tuyaux le long du bord.
            k.Col = Colors.White;
            var y2 = RackTop;
            var e0 = p0 - side * (hw + 0.1f) + Vector3.Up * y2;
            var e1 = p1 - side * (hw + 0.1f) + Vector3.Up * y2;
            var f0 = p0 + side * (hw * 0.2f) + Vector3.Up * y2;
            var f1 = p1 + side * (hw * 0.2f) + Vector3.Up * y2;
            var ul = (p1 - p0).Length();
            k.Quad(grating, e0, e1, f1, f0, new Vector2(0, 0), new Vector2(ul, 0), new Vector2(ul, hw * 1.2f), new Vector2(0, hw * 1.2f));
            k.Quad(grating, f0, f1, e1, e0, new Vector2(0, hw * 1.2f), new Vector2(ul, hw * 1.2f), new Vector2(ul, 0), new Vector2(0, 0));
            k.Col = new Color(0.45f, 0.45f, 0.43f);
            k.Tube(metal, p0 + side * (hw * 0.55f) + Vector3.Up * (y2 + 0.45f), p1 + side * (hw * 0.55f) + Vector3.Up * (y2 + 0.45f), 0.45f, 10);
            k.Col = new Color(0.3f, 0.2f, 0.15f);
            k.Tube(metal, p0 + side * (hw * 0.95f) + Vector3.Up * (y2 + 0.3f), p1 + side * (hw * 0.95f) + Vector3.Up * (y2 + 0.3f), 0.28f, 8);
            RailLine(k, e0, e1);
        }
    }
}
