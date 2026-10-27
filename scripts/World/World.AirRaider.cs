using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Air Raider côté monde : cibles de guidage (balises collées, laser des kits de guidage des alliés) que visent les
/// lance-missiles à kit de guidage du Fencer, estimation des points gagnés par une explosion, leurres pris pour cible.
/// </summary>
public partial class World
{
    private readonly List<Node3D> _guideTargets = new();
    /// <summary>Point de guidage laser de chaque joueur (kit de guidage tenu), effacé sans mise à jour récente.</summary>
    private readonly Dictionary<long, (Node3D node, float t)> _guideLasers = new();

    public void AddGuideTarget(Node3D n) => _guideTargets.Add(n);

    /// <summary>Laser de guidage d'un joueur (kit tenu) : le point se met à jour, il s'efface 0,5 s après le dernier envoi.</summary>
    public void UpdateGuideLaser(long peer, Vector3 at)
    {
        if (!_guideLasers.TryGetValue(peer, out var g) || !IsInstanceValid(g.node))
        {
            var n = new Node3D();
            n.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.4f, Height = 0.8f, RadialSegments = 10, Rings = 5 },
                MaterialOverride = Mats.Glow(new Color(1f, 0.35f, 0.1f), 6f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
            FxRoot.AddChild(n);
            g = (n, 0f);
        }
        g.node.GlobalPosition = at;
        _guideLasers[peer] = (g.node, (float)Time.GetTicksMsec() / 1000f);
    }

    /// <summary>Cibles de guidage valides (balises encore en place, lasers récents).</summary>
    public IEnumerable<Node3D> GuideTargets()
    {
        _guideTargets.RemoveAll(n => !IsInstanceValid(n));
        var now = (float)Time.GetTicksMsec() / 1000f;
        foreach (var (peer, g) in _guideLasers.ToList())
            if (!IsInstanceValid(g.node) || now - g.t > 0.5f)
            {
                if (IsInstanceValid(g.node)) g.node.QueueFree();
                _guideLasers.Remove(peer);
            }
        return _guideTargets.Concat(_guideLasers.Values.Select(g => g.node));
    }

    /// <summary>Cible de guidage la plus proche du réticule dans la zone de verrouillage et à portée (null : aucune).</summary>
    public Node3D GuideTargetFor(Camera3D cam, float range, Rect2 box)
    {
        Node3D best = null;
        var bd = float.MaxValue;
        var c = box.GetCenter();
        foreach (var n in GuideTargets())
        {
            var p = n.GlobalPosition;
            if (cam.IsPositionBehind(p) || cam.GlobalPosition.DistanceTo(p) > range) continue;
            var sp = cam.UnprojectPosition(p);
            if (!box.HasPoint(sp)) continue;
            var d = sp.DistanceSquaredTo(c);
            if (d < bd) { bd = d; best = n; }
        }
        return best;
    }

    /// <summary>Dégâts qu'une explosion infligera (même règle que <see cref="HostExplosion"/>) : points de l'Air Raider.</summary>
    public float BlastEstimate(Vector3 pos, float radius, float dmg)
    {
        var total = 0f;
        foreach (var e in EnemiesById.Values)
        {
            if (e.State == EnemyState.Dead) continue;
            var d = e.BodyDistance(pos) - e.Def.Size * 1.2f;
            if (d > radius) continue;
            total += dmg;
        }
        return total;
    }

    /// <summary>
    /// Hôte : un leurre attire l'ennemi s'il est à portée de leurre et pas beaucoup plus loin que le soldat visé (identifiant
    /// négatif : les coups « au joueur » vont alors au leurre, voir <see cref="Net.DamagePlayer"/>).
    /// </summary>
    public bool NearestDecoy(Vector3 pos, float playerDist, out long id, out Vector3 at)
    {
        id = 0;
        at = default;
        var best = float.MaxValue;
        foreach (var d in Decoy.All.Values)
        {
            if (!IsInstanceValid(d) || d.Health <= 0) continue;
            var p = d.Point;
            var dist = pos.DistanceTo(p);
            if (dist > d.Lure || dist > playerDist * 1.6f + 10f || dist >= best) continue;
            best = dist;
            id = d.Id;
            at = p;
        }
        return id != 0;
    }

    /// <summary>Hôte : coup porté à un leurre.</summary>
    public void DamageDecoy(long id, float dmg)
    {
        if (Decoy.All.TryGetValue((int)id, out var d) && IsInstanceValid(d)) d.Hit(dmg);
    }
}
