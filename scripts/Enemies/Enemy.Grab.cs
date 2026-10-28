using Godot;

namespace Rempart;

/// <summary>
/// Saisie de la fourmi rouge (façon EDF) : au lieu de mordre, elle attrape sa victime (joueur, à défaut soldat allié)
/// dans ses mandibules, la soulève, la secoue et la croque en continu. La victime ne peut plus bouger mais peut viser
/// et tirer ; seule la mort de la fourmi la libère (ou la victime à terre, partie, ou la fourmi retirée).
///
/// Réseau : l'hôte décide du début et de la fin (<see cref="Net.BroadcastGrab"/>) et inflige les dégâts à chaque morsure
/// par le chemin habituel (<see cref="Net.DamagePlayer"/>). Chaque pair rejoue l'animation à partir du temps écoulé
/// depuis la saisie et place lui-même la victime dans les mandibules du modèle qu'il affiche (<see cref="World.UpdateGrabs"/>).
/// </summary>
public partial class Enemy
{
    /// <summary>Seule la fourmi rouge saisit ses proies.</summary>
    public bool CanGrab => Def.Type == EnemyType.RedAnt;

    /// <summary>Joueur saisi (0 = aucun).</summary>
    public long GrabPeer { get; private set; }
    /// <summary>Soldat allié saisi (0 = aucun).</summary>
    public int GrabAlly { get; private set; }
    public bool Grabbing => GrabPeer != 0 || GrabAlly != 0;

    /// <summary>Dégâts par seconde de la saisie, en multiples de <see cref="EnemyDef.Damage"/> (fourmi rouge : 9 = morsure de fourmi noire × 1,5 par seconde), × difficulté.</summary>
    public const float GrabDpsFactor = 1f;

    /// <summary>Test : les insectes ignorent les soldats alliés (--grab).</summary>
    public static bool IgnoreAllies;
    /// <summary>
    /// Derniers ennemis de la mission (MissionDirector) : ils ignorent les soldats alliés et foncent sur les joueurs, comme
    /// dans EDF (une guêpe qui tournait sans fin autour d'une escouade au bout de la carte bloquait la fin de mission).
    /// </summary>
    public bool Hunting;

    private float _grabT = -1f;
    private int _grabBites;
    private Vector3 _grabFrom;

    /// <summary>Position des pieds de la victime : aspirée de sa position de départ jusqu'aux mandibules, puis tenue à la taille.</summary>
    public Vector3 GrabFeet
    {
        get
        {
            var socket = Model.GrabSocket != null ? Model.GrabSocketPosition : Center + Basis.Z * (2.2f * Def.Size);
            var hold = socket - Vector3.Up * 1.1f; // prise sous les côtes
            var k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_grabT / BugModel.GrabLift, 0f, 1f));
            return _grabFrom.Lerp(hold, k);
        }
    }

    /// <summary>Forme du point faible des fourmis (tête), recalée sur la tête animée pendant une saisie.</summary>
    private CollisionShape3D _headShape;
    private bool _headMoved;

    /// <summary>Centre de la tête (point faible) : cible du bot saisi.</summary>
    public Vector3 HeadPoint => _headShape?.GlobalPosition ?? GlobalTransform * (new Vector3(0, 1.25f, 1.35f) * Def.Size);

    /// <summary>
    /// La tête relevée qui secoue la victime sort de sa forme de collision au repos : la forme suit la tête affichée
    /// (chez chaque pair : les tirs se calculent chez le tireur), puis revient au repos à la fin de la saisie.
    /// </summary>
    private void SyncHeadShape()
    {
        if (Grabbing && Model.HeadCenter != null)
        {
            _headShape.GlobalPosition = Model.HeadCenterPosition;
            _headMoved = true;
        }
        else
        {
            _headShape.Position = new Vector3(0, 1.25f, 1.35f) * Def.Size;
            _headMoved = false;
        }
    }

    /// <summary>
    /// Corps à corps possible : aucune paroi entre le corps de l'insecte et la poitrine de la victime (mur, façade, muret,
    /// véhicule). Les obstacles fins (arbre, poteau, petit mobilier) ne bloquent pas : le rayon repart juste derrière eux.
    /// </summary>
    private bool MeleeClear(Vector3 victim)
    {
        var from = Center;
        var to = victim + Vector3.Up * 1.1f;
        var dir = to - from;
        var len = dir.Length();
        if (len < 0.01f) return true;
        dir /= len;
        var space = GetWorld3D().DirectSpaceState;
        for (var i = 0; i < 6; i++)
        {
            var q = PhysicsRayQueryParameters3D.Create(from, to, 1);
            var hit = space.IntersectRay(q);
            if (hit.Count == 0) return true;
            var id = _world.Decor?.FromHit(hit["collider"].AsGodotObject(), hit["shape"].AsInt32()) ?? -1;
            var prop = id >= 0 ? _world.Decor.Get(id) : null;
            if (prop == null || prop.Kind is not (PropKind.Tree or PropKind.Pole or PropKind.Small or PropKind.Furniture)) return false;
            from = hit["position"].AsVector3() + dir * 0.6f;
            if ((to - from).Dot(dir) <= 0) return true;
        }
        return true;
    }

    /// <summary>Hôte : la morsure devient une saisie si la cible peut être attrapée.</summary>
    private bool HostTryGrab(long peer, Ally ally)
    {
        if (ally != null)
        {
            if (ally.Dead || _world.IsGrabbed(0, ally.Id)) return false;
            Net.I.BroadcastGrab(Id, 0, ally.Id);
            return Grabbing;
        }
        if (peer <= 0 || _world.VehicleOf(peer) != null || _world.IsGrabbed(peer, 0)) return false;
        Net.I.BroadcastGrab(Id, peer, 0);
        return Grabbing;
    }

    /// <summary>Hôte : victime toujours valide, morsures (dégâts), la fourmi recule en traînant sa proie par à-coups.</summary>
    private void HostTickGrab(float dt)
    {
        Ally ally = null;
        var ok = GrabPeer != 0
            ? _world.PlayerPosition(GrabPeer, out _) && _world.VehicleOf(GrabPeer) == null
            : _world.Allies != null && _world.Allies.ById.TryGetValue(GrabAlly, out ally) && !ally.Dead;
        if (!ok)
        {
            if (Screenshot.CmdArg("grab") != null) GD.Print($"[grab] fourmi {Id} : victime à terre, partie ou disparue");
            Net.I.BroadcastGrab(Id, 0, 0);
            return;
        }
        var bites = TickGrabFx(dt);
        if (bites > 0)
        {
            var dmg = Def.Damage * GrabDpsFactor * BugModel.BitePeriod * _world.DamageMul * DamageScale * bites;
            if (GrabPeer != 0) Net.I.DamagePlayer(GrabPeer, dmg, Center);
            else _world.Allies.HostDamage(ally, dmg);
        }
        // Recule par à-coups (1,6 s sur 3,4 s) en gardant la face vers l'avant.
        var drag = _grabT > BugModel.GrabLift + 0.3f && Mathf.PosMod(_grabT, 3.4f) < 1.6f;
        var dir = SurfaceDir(-_fwd, _surfN);
        ClimbStep(dir, drag && dir != Vector3.Zero ? Def.Speed * 0.2f * dt : 0f, dt);
        OrientOnSurface(_fwd, dt);
        State = EnemyState.Attacking;
    }

    /// <summary>Temps de saisie, morsures (son, secousse de la victime locale) ; renvoie le nombre de morsures de cette image.</summary>
    private int TickGrabFx(float dt)
    {
        _grabT += dt;
        Model.GrabT = _grabT;
        var first = BugModel.GrabLift + BugModel.BiteSnap * BugModel.BitePeriod;
        var n = _grabT < first ? 0 : 1 + Mathf.FloorToInt((_grabT - first) / BugModel.BitePeriod);
        var bites = n - _grabBites;
        _grabBites = n;
        if (bites <= 0) return 0;
        var at = Model.GrabSocket != null ? Model.GrabSocketPosition : Center;
        Sfx.At("crunch", at, 2f, (float)GD.RandRange(0.85, 1.1), 0);
        var local = GrabPeer != 0 && GrabPeer == Net.I.MyId && _world.LocalPlayer != null;
        if (local)
        {
            _world.LocalPlayer.AddShake(0.4f);
            if (n % 2 == 1) Sfx.Ui("grunt", -4f, (float)GD.RandRange(0.92, 1.08), 0);
        }
        else if (n % 2 == 1) Sfx.At("grunt", at, -4f, (float)GD.RandRange(0.92, 1.08), 0);
        return bites;
    }

    /// <summary>Début de saisie (tous les pairs, sur ordre de l'hôte).</summary>
    public void StartGrab(long peer, int ally)
    {
        if (State == EnemyState.Dead || Model.GrabSocket == null) return;
        ReleaseGrab();
        var victim = _world.GrabVictim(peer, ally);
        if (victim == null) return;
        GrabPeer = peer;
        GrabAlly = ally;
        _grabT = 0f;
        _grabBites = 0;
        _grabFrom = victim.GlobalPosition;
        Model.GrabT = 0f;
        _attackAnim = 0f;
        _world.SetGrabVictim(this, peer, ally, true);
        Sfx.At("screech", Center, 6f, 0.75f, 0);
        Sfx.At("crunch", Model.GrabSocketPosition, 0f, 1.3f, 0);
        if (Screenshot.CmdArg("grab") != null) GD.Print($"[grab] fourmi {Id} saisit {(peer != 0 ? $"le joueur {peer}" : $"l'allié {ally}")} (t = {Time.GetTicksMsec() / 1000f:0.0} s)");
    }

    /// <summary>Fin de saisie : la victime retombe (ordre de l'hôte, mort ou retrait de la fourmi).</summary>
    public void ReleaseGrab()
    {
        if (!Grabbing) return;
        var peer = GrabPeer;
        var ally = GrabAlly;
        GrabPeer = 0;
        GrabAlly = 0;
        _grabT = -1f;
        if (IsInstanceValid(Model)) Model.GrabT = -1f;
        _moveDir = Vector3.Zero;
        _attackCd = Def.AttackCooldown * 1.5f;
        if (IsInstanceValid(_world)) _world.SetGrabVictim(this, peer, ally, false);
        if (Screenshot.CmdArg("grab") != null) GD.Print($"[grab] fourmi {Id} lâche sa proie après {_grabBites} morsures (PV {Mathf.Max(Health, 0):0})");
    }

    public override void _ExitTree()
    {
        // Fourmi coincée retirée, fin de mission : la victime ne reste pas suspendue dans le vide.
        if (Grabbing) ReleaseGrab();
    }
}
