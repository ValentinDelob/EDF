using Godot;

namespace Rempart;

/// <summary>
/// Encombrement des cours : bâtiment administratif et poste de garde à l'entrée, poste électrique clôturé,
/// mâts d'éclairage (ils basculent quand on les abat), tas de ferraille, palettes, bennes, semi-remorques
/// abandonnées, tourets de câble. Rien sur l'aire de départ ni sur les routes.
/// </summary>
public static partial class IndustrialBuilder
{
    private static void BuildYards(Ctx c)
    {
        var rng = new RandomNumberGenerator { Seed = 6262 };
        // Bâtiment administratif (bureaux de trois niveaux, destructible) et poste de garde.
        var office = new BuildingStyle { Kind = BuildingKind.Office, Variant = 3, Clearance = 3f, Night = false };
        var ob = Building.Create(c.NextId++, new Vector3(64, 0, 36), new Vector3(28, 3 * 3.2f + 0.6f, 13), Mats.Facade(3, false), Mats.Roof(), office);
        c.Root.AddChild(ob);
        c.Res.Buildings.Add(ob);
        Take(c, new Rect2(50, 29.5f, 28, 13), 1.5f);
        RuinKit.DamageFacades(ob, true, ob.Id);
        var gs = new Vector3(6, 3.4f, 4.5f);
        AddPlant(c, new Vector3(27, 0, -42), gs, FactoryHallMesh(gs, 15, false, false), Mats.FactoryBrick());

        Substation(c, new Vector3(-80, 0, 45));

        // Mâts d'éclairage dans les cours et le long des voies.
        foreach (var (x, z) in new[] { (-140f, -22f), (-200f, -22f), (80f, -36f), (160f, -36f), (216f, -50f), (-60f, 96f), (40f, 104f), (-110f, -150f), (60f, -212f), (170f, -140f), (-26f, -60f), (100f, 10f) })
        {
            var p = new Vector3(x, 0, z);
            if (IsTaken(c, p, 1.2f)) continue;
            c.Props.Add(Props.FloodMast(c.Night ? 1 : 0), p, RuinKit.Hash(x, z, 5) * Mathf.Tau, range: 520f, shadows: true);
            Take(c, p, 0.8f, 0.8f, 0f);
        }

        // Tas de ferraille rouillée (buttes solides) près des ateliers et de la gare.
        foreach (var (x, z, s) in new[] { (-130f, -84f, 1f), (-28f, -150f, 0.8f), (210f, 20f, 1.1f), (-206f, -100f + 14f, 0.9f), (88f, -86f, 0.7f) })
        {
            var p = new Vector3(x, 0, z);
            if (IsTaken(c, p, 4f * s)) continue;
            var k = c.Ruins.Kit(RuinKit.Layer.Solid, p);
            var pts = Props.RubblePile(k, p, x * 0.1f, new Vector3(9f, 2.4f, 7f) * s, (int)(x * 3 + z), new Color(0.32f, 0.2f, 0.14f), false);
            c.Ruins.Convex(pts);
            // Poutrelles et tôles qui dépassent (relief).
            var keep = k.Solid;
            k.Col = new Color(0.24f, 0.14f, 0.09f);
            for (var i = 0; i < 5; i++)
            {
                var a = RuinKit.Hash(i, (int)x, 61) * Mathf.Tau;
                var q = p + new Vector3(Mathf.Cos(a) * 2f, 1.2f * s, Mathf.Sin(a) * 1.6f);
                k.Box(Mats.DetailMetal(), q, new Vector3(5f * s, 0.25f, 0.25f), new Basis(Vector3.Up, a) * new Basis(Vector3.Back, 0.4f + RuinKit.Hash(i, (int)x, 62) * 0.5f));
            }
            k.Solid = keep;
            Take(c, p, 5f * s, 4f * s);
            c.Ruins.Decal(Mats.Decal.Dust, p, 12f * s, x, 0.022f);
        }

        // Semi-remorques abandonnées devant les quais.
        foreach (var (x, z, yaw) in new[] { (-160f, -30f, 0f), (-184f, -26f, 0.06f), (-172f, -12f, Mathf.Pi), (210f, 108f, 0.1f), (120f, 52f, Mathf.Pi / 2 + 0.05f), (-62f, -110f + 3f, Mathf.Pi / 2) })
        {
            var p = new Vector3(x, 0, z);
            if (IsTaken(c, p, 3f) || !c.Ruins.TryTake(p, 6f)) continue;
            c.Props.Add(Props.BoxTrailer(RuinKit.Hash(x, z, 3) < 0.5f ? 0 : 1), p, yaw, range: 400f, shadows: true);
        }

        // Palettes, bennes et tourets près des ateliers, des entrepôts et du quai.
        foreach (var (cx, cz, n) in new[] { (-140f, -60f, 10), (-140f, 6f, 8), (-96f, -140f, 6), (-20f, -140f, 6), (150f, 24f, 6), (196f, 108f, 8), (64f, 50f, 5), (-150f, -108f + 2f, 4), (40f, 120f, 5) })
            for (var i = 0; i < n; i++)
            {
                var p = new Vector3(cx + rng.RandfRange(-9f, 9f), 0, cz + rng.RandfRange(-6f, 6f));
                var roll = rng.Randf();
                var rad = roll < 0.15f ? 2.4f : 1f;
                if (IsTaken(c, p, rad) || RoadCross(p, 0.5f) || !c.Ruins.TryTake(p, rad)) continue;
                var yaw = rng.RandfRange(0, Mathf.Tau);
                if (roll < 0.15f) c.Props.Add(Props.Skip(), p, yaw, range: 240f, shadows: true);
                else if (roll < 0.3f) c.Props.Add(Props.CableReel(), p, yaw, range: 180f, shadows: true);
                else c.Props.Add(Props.PalletStack(rng.Randf() < 0.4f ? 1 : 0), p, yaw, range: 180f, shadows: true);
            }
    }

    /// <summary>Poste électrique : dalle, trois transformateurs, portiques en treillis et isolateurs, clôture grillagée.</summary>
    private static void Substation(Ctx c, Vector3 p)
    {
        var k = c.Solid.At(p);
        var metal = Mats.DetailMetal();
        k.Col = new Color(0.36f, 0.35f, 0.33f);
        k.BoxMinMax(Mats.Detail(), p + new Vector3(-14, 0, -7), p + new Vector3(14, 0.15f, 7));
        for (var i = 0; i < 3; i++)
        {
            var q = p + new Vector3(-8f + i * 8f, 0.15f, 1.5f);
            c.Props.Add(Props.Transformer(), q, 0f, range: 260f, shadows: true);
        }
        // Portiques (poteaux et traverses en treillis évoqués), isolateurs et câbles (relief).
        k.Col = new Color(0.32f, 0.33f, 0.33f);
        foreach (var x in new[] { -12f, 0f, 12f })
        {
            foreach (var z in new[] { -5f, -2f })
                k.Box(metal, p + new Vector3(x, 4.5f, z), new Vector3(0.3f, 9f, 0.3f));
            k.Box(metal, p + new Vector3(x, 8.8f, -3.5f), new Vector3(0.3f, 0.3f, 3.6f));
        }
        k.Box(metal, p + new Vector3(0, 9f, -3.5f), new Vector3(24.4f, 0.25f, 0.25f));
        k.Solid = false;
        k.Col = new Color(0.45f, 0.3f, 0.2f);
        for (var x = -12f; x <= 12f; x += 3f)
            k.Cylinder(metal, p + new Vector3(x, 9.1f, -3.5f), 0.1f, 0.08f, 0.8f, 6);
        k.Col = new Color(0.1f, 0.1f, 0.1f);
        for (var i = 0; i < 3; i++)
            k.Tube(metal, p + new Vector3(-8f + i * 8f, 3.6f, 1.5f), p + new Vector3(-8f + i * 8f, 9.6f, -3.5f), 0.03f, 3);
        k.Solid = true;
        Take(c, new Rect2(p.X - 14, p.Z - 7, 28, 14), 0.5f);
        // Clôture du poste (panneaux destructibles), portail au sud.
        var x0 = p.X - 15f;
        var x1 = p.X + 15f;
        var z0 = p.Z - 8.5f;
        var z1 = p.Z + 8.5f;
        void Line(Vector3 a, Vector3 b)
        {
            var dir = (b - a).Normalized();
            var n = Mathf.FloorToInt((b - a).Length() / 3f);
            var yaw = Mathf.Atan2(-dir.Z, dir.X);
            for (var i = 0; i < n; i++)
            {
                if (RuinKit.Hash(i, a.X + a.Z, 63) < 0.1f) continue;
                c.Props.Add(Props.ChainFence(RuinKit.Hash(i, a.X, 64) < 0.15f ? 1 : 0), a + dir * (i * 3f + 1.5f), yaw, range: 200f);
            }
        }
        Line(new Vector3(x0, 0, z0), new Vector3(x1, 0, z0));
        Line(new Vector3(x1, 0, z0), new Vector3(x1, 0, z1));
        Line(new Vector3(x0, 0, z1), new Vector3(p.X - 3f, 0, z1));
        Line(new Vector3(p.X + 3f, 0, z1), new Vector3(x1, 0, z1));
        Line(new Vector3(x0, 0, z0), new Vector3(x0, 0, z1));
        Take(c, new Rect2(x0 - 1f, z0 - 1f, 32f, 19f), 0f);
    }
}
