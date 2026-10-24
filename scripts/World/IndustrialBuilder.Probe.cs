using System.Collections.Generic;
using Godot;

namespace Rempart;

/// <summary>
/// Outil de développement « --indprobe » (zone industrielle) : des capsules de la taille d'un soldat suivent avec la
/// vraie physique les accès en hauteur (tour d'escalier du rack, escalier hélicoïdal d'une cuve, escalier du toit
/// d'entrepôt, tour des silos, portique, convoyeur, cale du canal) ; journal « [probe] … ok » à l'arrivée.
/// Puis l'hôte perce un réservoir de GPL et le journal « [cuves] » suit la réaction en chaîne.
/// </summary>
public static partial class IndustrialBuilder
{
    private static void AttachProbes(Ctx c)
    {
        // « --tankboom=x,z,t;x,z,t » : la citerne la plus proche de (x, z) saute à l'instant t (captures avec --citycam).
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
        if (Screenshot.CmdArg("indprobe") == null) return;
        var walkers = new List<BizWalker>
        {
            // Tour d'escalier ouest du rack est-ouest (passerelle à 7,3 m).
            new() { Label = "rack de tuyauteries", GoalY = RackTop, Path = TowerPath(new Transform3D(new Basis(Vector3.Up, -Mathf.Pi / 2), new Vector3(-36.2f, 0, -112)), RackTop, new Vector3(-24, 0, -112.9f)) },
            // Portique de la gare (passerelle à 18,5 m).
            new() { Label = "portique à conteneurs", GoalY = 18.5f, Path = TowerPath(new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2), new Vector3(137.6f, 0, -52.5f)), 18.5f, new Vector3(128, 0, -40)) },
            // Galerie des silos (25,3 m).
            new() { Label = "silos", GoalY = 25.3f, Path = TowerPath(new Transform3D(new Basis(Vector3.Up, Mathf.Pi / 2), new Vector3(-96 + 18.4f + 4.2f + 6f - 1.3f + 4.6f, 0, -124)), 25.3f, new Vector3(-80, 0, -124)) },
            // Toit de l'entrepôt ouest (escalier contre le pignon, puis toiture).
            new() { Label = "toit d'entrepôt", GoalY = 11f, Path = TowerPath(new Transform3D(new Basis(Vector3.Up, Mathf.Pi), new Vector3(-176 - 32 - 1.6f, 0, 12 + 16 - 3.6f)), 11f, new Vector3(-206.5f, 0, 27.5f)) },
            // Convoyeur incliné jusqu'à la tour de transfert (13,5 m).
            new() { Label = "convoyeur", GoalY = 13.5f, Path = new List<Vector3> { new(64, 0, 79.3f), new(70, 0, 79.3f), new(111, 0, 79.3f) } },
            // Sortie du canal par une cale (depuis le fond).
            new() { Label = "cale du canal", GoalY = 0f, Path = new List<Vector3> { new(84, CanalBed, 135), new(80, CanalBed, 129.7f), new(70.4f, 0, 129.7f), new(69.1f, 0, 129.7f), new(69.1f, 0, 123) } },
        };
        // Escalier hélicoïdal d'une cuve de stockage (cuve n° 0 : (32, -182), variante 0, lacet 0).
        {
            var (r, h) = Props.StorageTankDims[0];
            var rm = r + 0.55f;
            var span = h / Mathf.Tan(Mathf.DegToRad(34f)) / rm;
            var path = new List<Vector3>();
            const float a0 = 1.2f;
            var start = new Vector3(32, 0, -182);
            path.Add(start + new Vector3(Mathf.Cos(a0 - 0.15f) * rm, 0, Mathf.Sin(a0 - 0.15f) * rm));
            for (var i = 0; i <= 24; i++)
            {
                var a = a0 + span * i / 24f;
                path.Add(start + new Vector3(Mathf.Cos(a) * rm, 0, Mathf.Sin(a) * rm));
            }
            var end = a0 + span + 0.12f; // sur le palier d'arrivée, puis vers le centre du toit
            path.Add(start + new Vector3(Mathf.Cos(end) * rm, 0, Mathf.Sin(end) * rm));
            path.Add(start + new Vector3(Mathf.Cos(end) * (r - 2f), 0, Mathf.Sin(end) * (r - 2f)));
            walkers.Add(new BizWalker { Label = "escalier de cuve", GoalY = h + 0.5f, Path = path });
        }
        foreach (var w in walkers)
        {
            w.GlobalPositionOnReady = w.Path[0] + Vector3.Up * 1.2f;
            c.Root.AddChild(w);
        }
        c.Root.AddChild(new TankChainProbe());
    }

    /// <summary>Parcours d'une tour d'escalier (même géométrie que <see cref="StairTower"/>), puis sortie vers <paramref name="exitTo"/>.</summary>
    private static List<Vector3> TowerPath(Transform3D xf, float height, Vector3 exitTo)
    {
        const float half = 2.1f, land = 1.3f;
        var flights = Mathf.Max(1, Mathf.CeilToInt(height / 2.9f));
        var path = new List<Vector3> { xf * new Vector3(-0.65f, 0, -half - land - 0.2f) };
        for (var f = 0; f < flights; f++)
        {
            var x = f % 2 == 0 ? -0.65f : 0.65f;
            var zB = f % 2 == 0 ? half : -half;
            path.Add(xf * new Vector3(x, 0, zB + Mathf.Sign(zB) * 0.65f));
            if (f + 1 < flights) path.Add(xf * new Vector3(-x, 0, zB + Mathf.Sign(zB) * 0.65f));
        }
        if ((flights - 1) % 2 == 0)
        {
            // Arrivée côté +Z : on revient vers -Z par la file d'en face (au-dessus de l'avant-dernière volée).
            path.Add(xf * new Vector3(0.65f, 0, half + 0.65f));
            path.Add(xf * new Vector3(0.65f, 0, -half - 0.6f));
        }
        path.Add(xf * new Vector3(0, 0, -half - land - 0.5f));
        path.Add(exitTo);
        return path;
    }
}

/// <summary>Outil « --indprobe » : perce un réservoir de GPL (hôte) et suit la réaction en chaîne des citernes.</summary>
public partial class TankChainProbe : Node
{
    /// <summary>Cibles (point le plus proche, instant) ; par défaut, le premier réservoir de GPL à 6 s.</summary>
    public readonly List<(Vector3 at, float time)> Targets = new();
    private float _t;
    private bool _fired;
    private float _logT = 1f;

    public override void _Process(double delta)
    {
        _t += (float)delta;
        var w = World.Current;
        if (w?.Decor == null || !Net.I.IsHost) return;
        if (Targets.Count == 0 && !_fired) Targets.Add((new Vector3(150, 0, -152), 6f));
        for (var i = Targets.Count - 1; i >= 0; i--)
        {
            var (at, time) = Targets[i];
            if (_t < time) continue;
            Targets.RemoveAt(i);
            _fired = true;
            // Citerne la plus proche du point : dégâts d'explosion suffisants pour la faire sauter.
            PropEntry best = null;
            foreach (var id in w.Decor.Near(at, 16f))
                if (w.Decor.Get(id) is { Kind: PropKind.Tank, State: < 3 } e && (best == null || e.Xf.Origin.DistanceTo(at) < best.Xf.Origin.DistanceTo(at))) best = e;
            if (best == null) continue;
            w.Decor.HostDamage(best.Id, 5000f, PropHit.Blast, best.Xf.Origin + Vector3.Left * 6f, 0);
            GD.Print($"[cuves] citerne n°{best.Id} percée à {_t:0.0} s");
        }
        if (!_fired || (_logT -= (float)delta) > 0) return;
        _logT = 2f;
        int total = 0, burning = 0, gone = 0;
        for (var id = 0; id < w.Decor.Count; id++)
        {
            var e = w.Decor.Get(id);
            if (e.Kind != PropKind.Tank) continue;
            total++;
            if (e.Shown >= 3) gone++;
            else if (e.Shown == 2) burning++;
        }
        GD.Print($"[cuves] t={_t:0} s : {gone} explosées, {burning} en feu, {total - gone - burning} intactes sur {total}");
        if (_t > 40f) SetProcess(false);
    }
}
