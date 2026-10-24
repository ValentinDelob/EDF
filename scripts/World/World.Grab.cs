using Godot;

namespace Rempart;

/// <summary>Saisies des fourmis rouges : victimes (joueur local, coéquipiers, alliés PNJ) tenues dans les mandibules.</summary>
public partial class World
{
    /// <summary>Ordre de l'hôte : début (victime désignée) ou fin (peer = ally = 0) d'une saisie.</summary>
    public void OnGrab(int enemyId, long peer, int ally)
    {
        if (!EnemiesById.TryGetValue(enemyId, out var e)) return;
        if (peer == 0 && ally == 0) e.ReleaseGrab();
        else e.StartGrab(peer, ally);
    }

    /// <summary>Cette victime est-elle déjà tenue par une fourmi ?</summary>
    public bool IsGrabbed(long peer, int ally)
    {
        foreach (var e in EnemiesById.Values)
            if (e.Grabbing && (peer != 0 ? e.GrabPeer == peer : e.GrabAlly == ally)) return true;
        return false;
    }

    /// <summary>Nœud de la victime (soldat local, coéquipier ou allié PNJ), ou null.</summary>
    public Node3D GrabVictim(long peer, int ally)
    {
        if (peer != 0)
        {
            if (peer == Net.I.MyId) return LocalPlayer != null && IsInstanceValid(LocalPlayer) ? LocalPlayer : null;
            return Remotes.TryGetValue(peer, out var r) && IsInstanceValid(r) ? r : null;
        }
        // Un allié tué dans les mandibules reste une victime : il doit retomber au sol quand la fourmi le lâche.
        return Allies != null && Allies.ById.TryGetValue(ally, out var a) && IsInstanceValid(a) ? a : null;
    }

    /// <summary>Accroche ou décroche la victime de la fourmi (localement).</summary>
    public void SetGrabVictim(Enemy e, long peer, int ally, bool on)
    {
        switch (GrabVictim(peer, ally))
        {
            case Player p: if (on) p.OnGrabbed(e); else p.OnReleased(e); break;
            case RemotePlayer r: r.GrabbedBy = on ? e : r.GrabbedBy == e ? null : r.GrabbedBy; break;
            case Ally a: if (on) a.GrabbedBy = e; else a.Release(e); break;
        }
    }

    /// <summary>
    /// Place chaque victime dans les mandibules du modèle affiché, après l'animation des insectes de cette image
    /// (la tête secoue vite : une image de retard décollerait la victime des mandibules).
    /// </summary>
    private void UpdateGrabs()
    {
        foreach (var e in EnemiesById.Values)
        {
            if (!e.Grabbing) continue;
            if (GrabVictim(e.GrabPeer, e.GrabAlly) is { } v) v.GlobalPosition = e.GrabFeet;
        }
    }
}
