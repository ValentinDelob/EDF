using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Outil de développement « --desprobe » (base du désert) : des capsules de la taille d'un soldat montent avec la vraie
/// physique l'escalier de la tour de contrôle (balcon de la vigie), celui du hangar est (toit), du radôme (terrasse),
/// de l'auvent du parc automobile (toit), d'un mirador (plancher) et la butte d'une soute à munitions ; journal
/// « [probe] … ok ». Puis l'hôte fait sauter une soute (6 s) et une cuve du dépôt (14 s) : journal « [cuves] ».
/// « --tankboom=x,z,t » marche aussi sur cette carte (voir la zone industrielle).
/// </summary>
public static partial class DesertBuilder
{
    private static void AttachProbes(Ctx c)
    {
        if (Screenshot.CmdArg("tankboom") is { Length: > 0 } boom && boom != "true")
        {
            var probe = new TankChainProbe();
            foreach (var part in boom.Split(';'))
            {
                var v = part.Split(',');
                if (v.Length < 3) continue;
                float F(int i) => float.Parse(v[i], System.Globalization.CultureInfo.InvariantCulture);
                probe.Targets.Add((new Vector3(F(0), 0, F(1)), F(2)));
            }
            c.Root.AddChild(probe);
        }
        if (Screenshot.CmdArg("desprobe") == null) return;
        var hx = HangarCSize.X / 2;
        var hz = HangarCSize.Z / 2;
        var walkers = new List<BizWalker>
        {
            new() { Label = "tour de contrôle (balcon)", GoalY = CabFloor, Path = StairPath(new Transform3D(Basis.Identity, Tower + new Vector3(13.5f, 0, 0)), CabFloor, 0f, Tower + new Vector3(11.4f, 0, 0)) },
            new() { Label = "toit du hangar est", GoalY = Mathf.Lerp(HangarCSize.Y, HangarCEave, 20f / hx),
                Path = StairPath(new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2), HangarC + new Vector3(hx + 3.7f, 0, hz - 6f)), HangarCEave, 1.2f, HangarC + new Vector3(20f, 0, hz - 6f)) },
            new() { Label = "terrasse du radôme", GoalY = RadarBase.Y,
                Path = StairPath(new Transform3D(new Basis(Vector3.Up, -Mathf.Pi / 2), Radar + new Vector3(-RadarBase.X / 2 - 3.5f, 0, -RadarBase.Z / 2 + 4.5f)), RadarBase.Y, 1.5f, Radar + new Vector3(-8f, 0, -4.5f)) },
            new() { Label = "toit de l'auvent", GoalY = MotorShedSize.Y - 0.45f,
                Path = StairPath(new Transform3D(new Basis(Vector3.Up, -Mathf.Pi / 2), MotorShed + new Vector3(-MotorShedSize.X / 2 - 3.9f, 0, 0)), MotorShedSize.Y - 0.45f + 0.05f, 1.6f, MotorShed + new Vector3(-15f, 0, 0)) },
            new() { Label = "mirador nord", GoalY = Props.TowerDeck, Path = new List<Vector3> { new(0, 0, -185f), new(0, 0, -186.4f), new(0, 0, -195.5f), new(0, 0, -196.6f) } },
            new() { Label = "butte de soute", GoalY = Props.BunkerCrest - 0.05f, Path = new List<Vector3> { new(-141f, 0, 114f), new(-139.6f, 0, 114f), new(-126.3f, 0, 114f) } },
        };
        foreach (var w in walkers)
        {
            w.GlobalPositionOnReady = w.Path[0] + Vector3.Up * 1.2f;
            c.Root.AddChild(w);
        }
        var chain = new TankChainProbe();
        chain.Targets.Add((new Vector3(-156f, 0, BunkerZ), 6f));
        chain.Targets.Add((new Vector3(176f, 0, 72f), 14f));
        c.Root.AddChild(chain);
    }

    /// <summary>Parcours d'une tour d'escalier (même géométrie que <see cref="StairTower"/>), puis sortie vers <paramref name="exitTo"/>.</summary>
    private static List<Vector3> StairPath(Transform3D xf, float height, float exit, Vector3 exitTo)
    {
        const float half = 2.1f, land = 1.3f;
        var flights = Mathf.Max(1, Mathf.CeilToInt(height / 2.9f));
        // Départ au pied de la première volée (le côté -Z donne souvent contre le bâtiment desservi).
        var path = new List<Vector3> { xf * new Vector3(-0.65f, 0, -half + 0.2f) };
        for (var f = 0; f < flights; f++)
        {
            var x = f % 2 == 0 ? -0.65f : 0.65f;
            var zB = f % 2 == 0 ? half : -half;
            path.Add(xf * new Vector3(x, 0, zB + Mathf.Sign(zB) * 0.65f));
            if (f + 1 < flights) path.Add(xf * new Vector3(-x, 0, zB + Mathf.Sign(zB) * 0.65f));
        }
        if ((flights - 1) % 2 == 0)
        {
            // Arrivée côté +Z : retour vers -Z par la file d'en face.
            path.Add(xf * new Vector3(0.65f, 0, half + 0.65f));
            path.Add(xf * new Vector3(0.65f, 0, -half - 0.6f));
        }
        path.Add(xf * new Vector3(0, 0, -half - land * 0.5f - exit * 0.6f));
        path.Add(exitTo);
        return path;
    }
}
