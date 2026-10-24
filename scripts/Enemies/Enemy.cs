using System.Linq;
using Godot;

namespace Rempart;

public enum EnemyState : byte { Emerging = 0, Moving = 1, Attacking = 2, Dead = 3 }

/// <summary>
/// Ennemi. Sur l'hôte, l'IA le pilote ; sur les clients, il interpole les instantanés reçus.
/// Formes de collision : 0 = corps, 1 = tête (point faible).
/// Corps statique déplacé par position : aucun corps physique ne heurte les ennemis (ils ne sont que la cible des tirs
/// et des rayons) ; un corps cinématique coûtait un pas de physique pour chacun, à chaque image.
/// </summary>
public partial class Enemy : StaticBody3D
{
    public int Id;
    public EnemyDef Def;
    public float Health;
    public float MaxHealth;
    public EnemyState State = EnemyState.Emerging;
    public BugModel Model;
    public int EatenCrates;
    /// <summary>Variante élite : dorée, PV ×3, dégâts ×1,5, butin garanti.</summary>
    public bool Elite;
    public float DamageScale = 1f;
    public bool IsBoss => Def.Boss;
    public bool IsStatic => Def.Static;

    private World _world;
    private float _emergeT;
    private float _attackCd;
    private float _attackAnim;
    private float _thinkT;
    private Vector3 _moveDir;
    private Vector3 _velocity;
    private float _speedNow;
    private float _yaw;
    private float _altitude;
    private float _orbit;
    private int _targetCrate = -1;
    /// <summary>Élément de l'objectif visé ce tic (hôte), ou null : voir <see cref="MissionObjective.HostPickTarget"/>.</summary>
    private Guard _guard;
    private float _leapT = -1f;
    private Vector3 _leapFrom, _leapTo;
    private float _specialCd;

    // Interpolation côté client
    private Vector3 _netPos;
    private Quaternion _netRot = Quaternion.Identity;
    private Vector3 _prevPos;

    public static Enemy Create(World world, int id, EnemyType type, Vector3 pos, float yaw, float hpMul)
    {
        var def = Enemies.Get(type);
        // Un cap décalé de +10 rad signale un largage (insecte lâché par la soute d'un vaisseau : il tombe, sans sortir de terre).
        var airdrop = yaw > 8f;
        if (airdrop) yaw -= 10f;
        // Le vaisseau de largage arrive très haut (même altitude chez chaque pair).
        if (type == EnemyType.DropShip) pos.Y = 78f;
        var e = new Enemy
        {
            Id = id,
            Def = def,
            Name = $"Enemy{id}",
            CollisionLayer = 1 << 2,
            CollisionMask = 0,
            _world = world,
        };
        // Un multiplicateur négatif transmis par le réseau signale une variante élite.
        e.Elite = hpMul < 0;
        hpMul = Mathf.Abs(hpMul);
        e.MaxHealth = e.Health = def.Health * hpMul * (e.Elite ? 3f : 1f);
        e.DamageScale = e.Elite ? 1.5f : 1f;
        e.Model = BugModel.Create(type);
        e.AddChild(e.Model);
        if (e.Elite)
        {
            var gold = Mats.Chitin(new Color(0.78f, 0.58f, 0.14f));
            foreach (var n in e.Model.FindChildren("*", "MeshInstance3D", true, false))
            {
                var mi = (MeshInstance3D)n;
                if (mi.Mesh is SphereMesh && mi.Scale.X > 0.5f) mi.MaterialOverride = gold;
            }
            e.Model.Scale *= 1.12f;
        }
        var s = def.Size;
        if (e.IsMachine) e.CreateMachineShapes();
        else if (type == EnemyType.Titan) e.CreateTitanShapes(hpMul);
        else if (e.IsAlienKind) e.CreateAlienShapes();
        else if (type == EnemyType.Nest)
        {
            e.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 6f * s, Height = 4.5f * s }, Position = new Vector3(0, 2.2f, 0) * s });
            e.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 2.2f * s }, Position = new Vector3(0, 4.3f, 0) * s });
        }
        else if (type == EnemyType.Fortress)
        {
            // 0 = coque, 1 = noyau (point faible), puis une colonne par patte (position de repos).
            e.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(18.5f, 10f, 24f) }, Position = new Vector3(0, BugModel.FortHullY, -0.5f) });
            e.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = BugModel.FortCoreRadius }, Position = BugModel.FortCorePos });
            for (var i = 0; i < 4; i++)
            {
                var f = BugModel.FortressFootRest(i);
                e.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(3.4f, 24f, 3.4f) }, Position = new Vector3(f.X, 12f, f.Z) });
            }
            // Arrive déjà debout (pas de sortie de terre) ; chaque pas secoue le sol.
            e.State = EnemyState.Moving;
            e.Model.Stomp += e.OnFortressStomp;
        }
        else if (type == EnemyType.Centipede)
        {
            // 0 = cou, 1 = tête (point faible), puis une sphère par segment, recalée chaque image sur le corps.
            e.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.1f, 0.8f, 0.7f) * s }, Position = new Vector3(0, 0.6f, -0.15f) * s });
            e.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.5f * s }, Position = new Vector3(0, 0.65f, 0.4f) * s });
            e._segShapes = new CollisionShape3D[e.Model.CentipedeSegmentCount];
            for (var i = 0; i < e._segShapes.Length; i++)
            {
                e._segShapes[i] = new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.55f * s * e.Model.CentipedeSegmentScale(i) } };
                e.AddChild(e._segShapes[i]);
            }
            e.SyncSegmentShapes();
        }
        else if (type is EnemyType.Wasp or EnemyType.Drone)
        {
            e.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.6f, 1.4f, 3.4f) * s }, Position = new Vector3(0, 0, -0.5f) * s });
            e.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.55f * s }, Position = new Vector3(0, 0, 0.75f) * s });
        }
        else
        {
            e.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.8f, 1.5f, 4.0f) * s }, Position = new Vector3(0, 1.1f, -0.7f) * s });
            e._headShape = new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.6f * s }, Position = new Vector3(0, 1.25f, 1.35f) * s };
            e.AddChild(e._headShape);
        }
        e.Position = pos;
        e._yaw = yaw;
        e.Rotation = new Vector3(0, yaw, 0);
        e._fwd = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        e._netPos = e._prevPos = pos;
        e._netRot = e.Quaternion;
        e.LandingPoint = world.OnGround(pos);
        e._altitude = (float)GD.RandRange(12, 22);
        e._orbit = (float)GD.RandRange(0, Mathf.Tau);
        e._attackCd = (float)GD.RandRange(0.5, 1.5) * def.AttackCooldown;
        e._specialCd = 6f;
        if (def.Static) e.State = EnemyState.Moving;
        if (airdrop)
        {
            e.State = EnemyState.Moving;
            e._falling = !def.Flying;
        }
        return e;
    }

    public override void _Ready()
    {
        if (IsMachine) MachineReady();
        if (IsTitan) TitanReady();
        if (IsAlienKind) AlienReady();
        if (State == EnemyState.Emerging)
        {
            Model.Position = new Vector3(0, -2.5f * Def.Size, 0);
            if (!Def.Flying) Effects.Emerge(_world, GlobalPosition, 2f * Def.Size);
            else Model.Position = Vector3.Zero;
        }
    }

    /// <summary>Centre du corps (au-dessus des pattes, selon la normale de la surface pour les grimpeurs).</summary>
    public Vector3 Center => IsTitan ? (IsInsideTree() ? Model.TitanChest : GlobalPosition + Vector3.Up * BugModel.TitanCenterY)
        : IsFortress ? GlobalPosition + Vector3.Up * BugModel.FortHullY
        : IsMachine ? MachineCenter
        : IsAlienKind ? AlienCenter
        :GlobalPosition + (Def.Flying ? Vector3.Zero : Basis.Y * ((_segShapes != null ? 0.65f : 1.1f) * Def.Size));

    private bool IsFortress => Def.Type == EnemyType.Fortress;
    /// <summary>Zone visée par le canon principal pendant sa charge (hôte uniquement), sinon null.</summary>
    public Vector3? CannonZone => IsFortress && _fChargeT >= 0 ? _fCannonTarget : IsMachine ? MachineDangerZone : IsTitan ? BreathAim() : null;

    /// <summary>Formes de collision des segments du mille-pattes (le corps suit la trace de la tête).</summary>
    private CollisionShape3D[] _segShapes;

    private void AnimateModel(float dt, float speed, float attack)
    {
        Perf.Begin(Perf.S.Anim);
        // Niveau de détail de l'animation (visuel seulement) : les insectes lointains ou hors champ s'animent moins souvent,
        // avec le temps cumulé (la démarche reste continue). Jamais pour ceux dont les formes de collision suivent le modèle.
        _animDt += dt;
        var step = AnimStep();
        if (step <= 1 || (int)((Engine.GetProcessFrames() + (ulong)Id) % (ulong)step) == 0)
        {
            AnimateModelCore(_animDt, speed, attack);
            _animDt = 0f;
            Perf.Counters[step <= 1 ? 0 : 1]++;
        }
        Perf.End(Perf.S.Anim);
    }

    private float _animDt;

    /// <summary>Caméra de l'image en cours : position et plans du champ de vision (calculés une fois par image).</summary>
    private static ulong _viewFrame = ulong.MaxValue;
    private static Vector3 _viewPos;
    private static readonly Plane[] _viewPlanes = new Plane[6];
    private static bool _viewOk;

    private static void UpdateView(Node from)
    {
        var frame = Engine.GetProcessFrames();
        if (frame == _viewFrame) return;
        _viewFrame = frame;
        _viewOk = false;
        if (from.GetViewport()?.GetCamera3D() is not { } cam) return;
        _viewPos = cam.GlobalPosition;
        var planes = cam.GetFrustum();
        if (planes.Count != 6) return;
        for (var i = 0; i < 6; i++) _viewPlanes[i] = planes[i];
        _viewOk = true;
    }

    /// <summary>
    /// Une image sur N : 1 près de la caméra (moins de 60 m) et dans le champ ; 2 jusqu'à 150 m ; 3 au-delà ;
    /// 4 hors du champ de vision (le corps garde son ombre, les pattes n'en projettent pas).
    /// </summary>
    private int AnimStep()
    {
        if (!AnimLod) return 1;
        UpdateView(this);
        if (!_viewOk) return 1;
        var p = GlobalPosition;
        var d2 = p.DistanceSquaredTo(_viewPos);
        var r = 4f * Def.Size;
        if (d2 < r * r * 4f) return 1;
        foreach (var pl in _viewPlanes)
            if (pl.DistanceTo(p) > r) return 4;
        return d2 < 60f * 60f ? 1 : d2 < 150f * 150f ? 2 : 3;
    }

    /// <summary>Cadence réduite permise : insectes cuits sans forme de collision recalée sur le modèle (ni mille-pattes, ni saisie).</summary>
    private bool AnimLod => Model.Baked && _segShapes == null && !Grabbing && !_headMoved && State != EnemyState.Dead && !Frozen;

    private void AnimateModelCore(float dt, float speed, float attack)
    {
        if (IsMachine)
        {
            Model.AnimateMachine(dt, speed, attack);
            SyncMachineShapes();
            if (IsShip) MachineAudio();
            return;
        }
        if (IsTitan)
        {
            Model.AnimateTitan(dt, speed, attack);
            SyncTitanShapes();
            return;
        }
        if (IsPortal) { Model.AnimatePortal(dt); PortalAudio(); return; }
        if (IsAlien) { Model.AnimateAlien(dt, speed, attack); SyncAlienShapes(); return; }
        if (IsFortress) Model.AnimateFortress(dt, speed, attack);
        else Model.Animate(dt, speed, attack);
        if (_segShapes != null) SyncSegmentShapes();
        if (_headShape != null && (Grabbing || _headMoved)) SyncHeadShape();
    }

    /// <summary>Recale les formes de collision sur les segments affichés (hôte et clients : les tirs touchent tout le corps).</summary>
    private void SyncSegmentShapes()
    {
        var xf = Model.Transform;
        for (var i = 0; i < _segShapes.Length; i++) _segShapes[i].Position = xf * Model.CentipedeSegmentLocal(i);
    }

    /// <summary>Distance d'un point au corps : le centre, ou le segment le plus proche pour le mille-pattes.</summary>
    public float BodyDistance(Vector3 p)
    {
        var d = Center.DistanceTo(p);
        if (_segShapes != null)
            foreach (var sh in _segShapes) d = Mathf.Min(d, sh.GlobalPosition.DistanceTo(p));
        // Machines : la forme la plus proche (une roquette au pied d'un Marcheur le touche).
        if (_mShapes != null && IsInsideTree())
            foreach (var sh in _mShapes) d = Mathf.Min(d, sh.GlobalPosition.DistanceTo(p));
        return d;
    }

    // ---------------- Hôte ----------------

    /// <summary>Fige l'IA (mode vitrine pour contrôler les modèles).</summary>
    public static bool Frozen;

    /// <summary>
    /// Position pour la séparation entre voisins (<see cref="World.Separation"/>) : relevée à la reconstruction de la grille
    /// puis à la fin du tic de l'ennemi, soit la position que lirait le moteur, sans un appel par voisin.
    /// </summary>
    public Vector3 SepPos;

    public void HostTick(float dt)
    {
        HostTickCore(dt);
        SepPos = GlobalPosition;
    }

    private void HostTickCore(float dt)
    {
        if (State == EnemyState.Dead) return;
        if (IsAlienKind) { HostTickAlien(dt); return; }
        if (Frozen && State != EnemyState.Emerging)
        {
            // Vitrine : le titan figé joue quand même ses attaques forcées (tests).
            if (IsTitan) { TickTitanFx(dt, true); AnimateModel(dt, 0f, 0f); return; }
            AnimateModel(dt, 2f, Mathf.Max(0, Mathf.Sin(Time.GetTicksMsec() / 600f)));
            return;
        }
        if (State == EnemyState.Emerging)
        {
            _emergeT += dt;
            var k = Mathf.Clamp(_emergeT / 1.2f, 0, 1);
            if (!Def.Flying) Model.Position = new Vector3(0, Mathf.Lerp(-2.5f * Def.Size, 0, k), 0);
            if (k >= 1) State = EnemyState.Moving;
            AnimateModel(dt, 3f, 0);
            return;
        }
        if (Def.Static) { AnimateModel(dt, 0, 0); return; }
        if (IsMachine) { HostTickMachine(dt); return; }
        if (IsTitan) { HostTickTitan(dt); return; }
        if (!Def.Boss && HostStuck(dt)) { _world.HostRespawnStuck(this); return; }

        _attackCd -= dt;
        _specialCd -= dt;
        _attackAnim = Mathf.Max(0, _attackAnim - dt * 3f);
        if (Grabbing)
        {
            HostTickGrab(dt);
            AnimateModel(dt, _speedNow, 0f);
            return;
        }
        if (!_world.NearestPlayer(GlobalPosition, out var peer, out var target))
        {
            _speedNow = Mathf.Lerp(_speedNow, 0, dt * 4);
            AnimateModel(dt, _speedNow, _attackAnim);
            return;
        }
        if (IsFortress)
        {
            TickFortress(dt, peer, target);
            AnimateModel(dt, _speedNow, _attackAnim);
            return;
        }

        // Les soldats alliés plus proches que le joueur deviennent la cible.
        Ally ally = null;
        if (!Def.Boss && !IgnoreAllies)
        {
            var a = _world.Allies?.Nearest(GlobalPosition, 45f);
            if (a != null && a.GlobalPosition.DistanceTo(GlobalPosition) < target.DistanceTo(GlobalPosition) * 0.85f)
            {
                ally = a;
                target = a.GlobalPosition;
            }
        }
        // Objectif de mission (structure défendue, camion du convoi) : cible s'il n'est guère plus loin que le soldat ou l'allié visé.
        _guard = null;
        if (!Def.Boss && _world.Objective != null && _world.Objective.HostPickTarget(this, target, out var guardPoint, out var guard))
        {
            _guard = guard;
            ally = null;
            target = guardPoint;
        }
        if (Def.Flying) TickFlying(dt, peer, target, ally);
        else TickGround(dt, peer, target, ally);
        AnimateModel(dt, _speedNow, _attackAnim);
    }

    private float _stuckT, _progressT, _noProgressT, _bestDist = float.MaxValue;
    private Vector3 _progressPos;

    /// <summary>
    /// Détection de blocage (hôte) : sous la carte ou hors limites → réapparition rapide ;
    /// presque immobile pendant 15 s, ou sans se rapprocher de sa cible pendant 25 s, alors qu'elle est loin → réapparition aussi.
    /// </summary>
    private bool HostStuck(float dt)
    {
        var p = GlobalPosition;
        if (p.Y < _world.GroundHeight(p) - 3f || Mathf.Abs(p.X) > 450f || Mathf.Abs(p.Z) > 450f || !p.IsFinite())
        {
            _stuckT += dt;
            return _stuckT > 1.5f;
        }
        _progressT += dt;
        if (_progressT < 3f) return false;
        _progressT = 0;
        var moved = p.DistanceTo(_progressPos);
        _progressPos = p;
        var hasTarget = _world.NearestPlayer(p, out _, out var target);
        var dist = hasTarget ? p.DistanceTo(target) : 0f;
        // Loin de la cible seulement : près du joueur, une fourmi bloquée par la foule n'est pas « coincée ».
        // Un insecte qui assiège l'objectif (loin des soldats) n'est pas « coincé ».
        var far = hasTarget && _guard == null && dist > Mathf.Max(30f, Def.AttackRange * 1.5f + 8f);
        if (moved < 1.2f * Def.Size && far && _attackAnim <= 0f) _stuckT += 3f;
        else _stuckT = 0;
        // Tourne en rond sans jamais se rapprocher (poche dans le décor, sous un pont…) : 25 s sans gagner 3 m.
        if (!far || dist < 45f || dist < _bestDist - 3f * Def.Size) { _bestDist = dist; _noProgressT = 0; }
        else _noProgressT += 3f;
        return _stuckT >= 15f || _noProgressT >= 25f;
    }

    private void TickGround(float dt, long peer, Vector3 target, Ally ally)
    {
        var pos = GlobalPosition;
        var goal = target;

        // Charognard : les caisses d'abord.
        if (Def.Type == EnemyType.Scavenger)
        {
            _thinkT -= dt;
            if (_thinkT <= 0)
            {
                _thinkT = 0.5f;
                _targetCrate = _world.NearestCrate(pos, 90f);
            }
            if (_targetCrate >= 0 && _world.CratePosition(_targetCrate, out var cp))
            {
                goal = cp;
                if (pos.DistanceTo(cp) < 3.5f * Def.Size)
                {
                    _world.HostEatCrate(_targetCrate, this);
                    _targetCrate = -1;
                }
            }
        }

        var climbs = Climbs;
        var toGoal3 = goal - pos;
        var toGoal = toGoal3;
        toGoal.Y = 0;
        var dist = toGoal.Length();
        // Grimpeurs : distance réelle (un joueur sur un toit n'est pas à portée depuis le pied du mur).
        var reachDist = climbs ? toGoal3.Length() : dist;

        // Araignée : bonds paraboliques vers la cible, atterrissage sur la surface visée (toits compris).
        if (Def.Type == EnemyType.Spider)
        {
            if (_leapT >= 0)
            {
                _leapT += dt / 0.9f;
                var k = Mathf.Min(_leapT, 1f);
                var p = _leapFrom.Lerp(_leapTo, k) + Vector3.Up * (Mathf.Sin(k * Mathf.Pi) * (6f + 2f * Def.Size));
                GlobalPosition = p;
                _speedNow = 0;
                OrientOnSurface(toGoal, dt);
                if (_leapT >= 1f) { _leapT = -1f; GlobalPosition = _leapTo; _probeDist = 1f; Sfx.At("hit", _leapTo, -2f, 0.6f); }
                return;
            }
            if (_specialCd <= 0 && dist > 6f + 3f * Def.Size && dist < 60f && _up.Y > 0.7f && !_falling)
            {
                _specialCd = (float)GD.RandRange(4, 7);
                var land = pos + toGoal.Normalized() * Mathf.Min(dist - 3f * Def.Size, 28f);
                var top = new Vector3(land.X, Mathf.Max(pos.Y, goal.Y) + 30f, land.Z);
                if (Ray(top, top + Vector3.Down * 80f, out var lp, out var ln) && ln.Y > 0.7f)
                {
                    _leapFrom = pos;
                    _leapTo = lp;
                    _surfN = ln;
                    _leapT = 0f;
                    Sfx.At("screech", pos, -2f, 1.4f);
                    return;
                }
            }
        }
        var reach = Def.AttackRange * (Def.Ranged ? 0.85f : 1f) + (Def.Boss ? 2f : 0);
        var desired = Vector3.Zero;
        var attacking = goal == target && reachDist <= reach;

        if (!attacking)
        {
            if (dist < 10f + 3f * Def.Size || Def.Boss) desired = (climbs ? toGoal3 : toGoal) / Mathf.Max(climbs ? reachDist : dist, 0.01f);
            else
            {
                // Les grimpeurs passent par-dessus les immeubles quand c'est plus court (champ pondéré).
                desired = climbs ? _world.Flow.ClimbDirection(pos) : _world.Flow.Direction(pos);
                if (desired == Vector3.Zero) desired = toGoal / Mathf.Max(dist, 0.01f);
            }
        }
        // Séparation entre ennemis.
        Perf.Begin(Perf.S.Separation);
        var sep = _world.Separation(this, 3.2f * Def.Size);
        Perf.End(Perf.S.Separation);
        var move = desired + sep * 2.2f;
        if (move.LengthSquared() > 1) move = move.Normalized();
        _moveDir = _moveDir.Lerp(move, Mathf.Clamp(dt * 6f, 0, 1));
        var speed = Def.Speed * (attacking ? 0.15f : 1f);
        if (climbs)
        {
            var dir = SurfaceDir(_moveDir, _surfN);
            Perf.Begin(Perf.S.Climb);
            ClimbStep(dir, dir == Vector3.Zero ? 0f : speed * dt * Mathf.Min(_moveDir.Length() * 1.5f, 1f), dt);
            Perf.End(Perf.S.Climb);
            // Sur une paroi, le corps regarde où il marche (vers le haut quand il escalade).
            OrientOnSurface(attacking || dist < 20 && _up.Y > 0.7f || dir == Vector3.Zero ? toGoal3 : dir, dt);
        }
        else
        {
            var step = _moveDir * speed * dt;
            var next = pos + step;
            if (!Def.Boss && _world.Flow.IsBlocked(next) && !_world.Flow.IsBlocked(pos)) next = pos; // ne traverse pas les immeubles
            next.Y = _world.GroundHeight(next); // sol de la carte (relief, ponts), 0 sur les cartes plates
            _speedNow = (next - pos).Length() / Mathf.Max(dt, 0.0001f);
            GlobalPosition = next;

            // Orientation
            var face = attacking || dist < 20 ? toGoal : _moveDir;
            if (face.LengthSquared() > 0.01f)
            {
                var targetYaw = Mathf.Atan2(face.X, face.Z);
                _yaw = Mathf.LerpAngle(_yaw, targetYaw, Mathf.Clamp(dt * (Def.Boss ? 2f : 7f), 0, 1));
                Rotation = new Vector3(0, _yaw, 0);
            }
        }

        // Mille-pattes : courte giclée d'acide sur une cible à mi-distance, entre deux morsures.
        if (Def.Type == EnemyType.Centipede && ally == null && _guard == null && _specialCd <= 0 && dist > reach + 3f && dist < 24f)
        {
            _specialCd = (float)GD.RandRange(4.5, 7.5);
            _attackAnim = 1f;
            _world.HostEnemyProjectile(this, 0, Center + Basis.Z * 1.4f * Def.Size, target + Vector3.Up, 1);
            Net.I.BroadcastEnemyAttack(Id, 1, target);
        }
        // Corps à corps à travers une paroi : ni morsure ni saisie (on réessaie peu après, le temps de contourner).
        if (attacking && _attackCd <= 0 && !Def.Ranged && _guard == null && Climbs && !MeleeClear(target))
            _attackCd = 0.4f;
        if (attacking && _attackCd <= 0)
        {
            // Fourmi rouge : la morsure devient une saisie (la victime reste prise dans les mandibules).
            if (CanGrab && _guard == null && HostTryGrab(peer, ally)) { State = EnemyState.Attacking; return; }
            _attackCd = Def.AttackCooldown * (float)GD.RandRange(0.85, 1.15);
            _attackAnim = 1f;
            if (_guard != null)
            {
                // Objectif : dégâts directs (projectile seulement visuel), comme pour un allié.
                _world.Objective.HostDamage(_guard, Def.Damage * _world.DamageMul * DamageScale * (Def.Ranged ? 0.6f : 1f));
                Net.I.BroadcastEnemyAttack(Id, Def.Ranged ? 1 : 0, target);
                if (Def.Ranged) Projectile.Spawn(_world, ProjectileKind.Acid, Center + Basis.Z * 1.5f * Def.Size, World.BallisticVelocity(Center, target + Vector3.Up, 30f, 9f), false, 0, 0);
                else Sfx.At("hit", pos, 0f, 0.7f);
            }
            else if (ally != null)
            {
                _world.Allies.HostDamage(ally, Def.Damage * _world.DamageMul * DamageScale * (Def.Ranged ? 0.6f : 1f));
                Net.I.BroadcastEnemyAttack(Id, Def.Ranged ? 1 : 0, target);
                if (Def.Ranged) Projectile.Spawn(_world, ProjectileKind.Acid, Center + Basis.Z * 1.5f * Def.Size, World.BallisticVelocity(Center, target + Vector3.Up, 30f, 9f), false, 0, 0);
            }
            else if (Def.Ranged && (!Def.Boss || dist > 10f))
            {
                var from = Center + Basis.Z * 1.5f * Def.Size;
                _world.HostEnemyProjectile(this, Def.Type == EnemyType.Spider ? 2 : 0, from, target + Vector3.Up, Def.Boss ? 3 : 1);
                Net.I.BroadcastEnemyAttack(Id, 1, target);
            }
            else
            {
                Net.I.DamagePlayer(peer, Def.Damage * _world.DamageMul * DamageScale, pos);
                Net.I.BroadcastEnemyAttack(Id, 0, target);
                Sfx.At("hit", pos, 0f, 0.7f);
            }
        }
        // La reine crache de l'acide en éventail de temps en temps.
        if (Def.Boss && _specialCd <= 0)
        {
            _specialCd = (float)GD.RandRange(5, 8);
            for (var i = -3; i <= 3; i++)
            {
                var side = Basis.X * i * 5f;
                _world.HostEnemyProjectile(this, 0, Center + Basis.Z * (1.05f * Def.Size) + Vector3.Up * (0.5f * Def.Size), target + side, 1);
            }
            Net.I.BroadcastEnemyAttack(Id, 2, target);
            Sfx.At("screech", pos, 8f, 0.6f);
        }
        State = attacking ? EnemyState.Attacking : EnemyState.Moving;
    }

    private void TickFlying(float dt, long peer, Vector3 target, Ally ally)
    {
        var pos = GlobalPosition;
        _orbit += dt * 0.5f;
        var ring = new Vector3(Mathf.Cos(_orbit), 0, Mathf.Sin(_orbit)) * 20f;
        var goal = target + ring + Vector3.Up * _altitude;
        var to = goal - pos;
        var sep = _world.Separation(this, 3f * Def.Size);
        var desired = (to.LengthSquared() > 1 ? to.Normalized() : Vector3.Zero) + sep;
        _velocity = _velocity.Lerp(desired * Def.Speed, Mathf.Clamp(dt * 2f, 0, 1));
        var next = pos + _velocity * dt;
        next.Y = Mathf.Max(next.Y, _world.GroundHeight(next) + 2.5f + 2f * Def.Size);
        GlobalPosition = next;
        _speedNow = _velocity.Length();
        var look = target - next;
        var targetYaw = Mathf.Atan2(look.X, look.Z);
        _yaw = Mathf.LerpAngle(_yaw, targetYaw, Mathf.Clamp(dt * 4f, 0, 1));
        Rotation = new Vector3(0, _yaw, 0);
        var dist = pos.DistanceTo(target);
        if (dist < Def.AttackRange + 15 && _attackCd <= 0)
        {
            _attackCd = Def.AttackCooldown * (float)GD.RandRange(0.8, 1.3);
            _attackAnim = 1;
            if (_guard != null)
            {
                _world.Objective.HostDamage(_guard, Def.Damage * _world.DamageMul * DamageScale);
                Projectile.Spawn(_world, Def.Type == EnemyType.Drone ? ProjectileKind.Laser : ProjectileKind.Stinger, pos + Vector3.Down * (0.8f * Def.Size), (target - pos).Normalized() * 60f, false, 0, 0);
            }
            else if (ally != null)
            {
                _world.Allies.HostDamage(ally, Def.Damage * _world.DamageMul * DamageScale);
                Projectile.Spawn(_world, ProjectileKind.Stinger, pos + Vector3.Down * (0.8f * Def.Size), (target + Vector3.Up - pos).Normalized() * 60f, false, 0, 0);
            }
            else _world.HostEnemyProjectile(this, Def.Type == EnemyType.Drone ? 3 : 1, pos + Vector3.Down * (0.8f * Def.Size), target + Vector3.Up * 1f, 1);
            Net.I.BroadcastEnemyAttack(Id, 3, target);
        }
        State = EnemyState.Moving;
    }

    /// <summary>Taille d'un ennemi dans l'instantané : id (4), position 3 × 16 bits, normale et regard octaédriques (2 + 2), état, PV, attaque, vitesse.</summary>
    public const int SnapshotBytes = 18;

    public void PackSnapshot(System.IO.BinaryWriter w)
    {
        w.Write(Id);
        PackPos(w, GlobalPosition);
        // Orientation complète (murs, toits) : normale de la surface + direction du regard.
        var b = Basis.Orthonormalized();
        w.Write(PackDir(b.Y));
        w.Write(PackDir(b.Z));
        w.Write(IsAlien ? AlienStateByte() : (byte)State);
        w.Write((byte)Mathf.Clamp(Health / MaxHealth * 255f, 0, 255));
        w.Write((byte)Mathf.Clamp(_attackAnim * 255f, 0, 255));
        // Machines : vitesse signée (marche arrière), décalée de 12,8 m/s.
        w.Write((byte)Mathf.Clamp(_speedNow * 10f + (IsMachine ? 128f : 0f), 0, 255));
    }

    // ---------------- Client ----------------

    public void ApplySnapshot(Vector3 pos, Vector3 up, Vector3 fwd, EnemyState state, float hpFrac, float attack, float speed)
    {
        // Aliens : posture dans les 4 bits hauts de l'état (voir AlienStateByte).
        if (IsAlien) { ApplyAlienFlags((int)state >> 4); state = (EnemyState)((int)state & 15); }
        _prevPos = GlobalPosition;
        _netPos = pos;
        _netRot = BasisFrom(up, fwd).GetRotationQuaternion();
        if (State != EnemyState.Dead) State = state;
        Health = hpFrac * MaxHealth;
        _attackAnim = Mathf.Max(_attackAnim, attack);
        _speedNow = IsMachine ? speed - 12.8f : speed;
    }

    public void ClientTick(float dt)
    {
        if (State == EnemyState.Dead) return;
        // Portail : fixe, hors des instantanés.
        if (IsPortal) { AnimateModel(dt, 0, 0); return; }
        if (State == EnemyState.Emerging && !Def.Flying)
        {
            _emergeT += dt;
            Model.Position = new Vector3(0, Mathf.Lerp(-2.5f * Def.Size, 0, Mathf.Clamp(_emergeT / 1.2f, 0, 1)), 0);
        }
        else if (Model.Position.Y < 0) Model.Position = Vector3.Zero;
        var p = GlobalPosition;
        if (p.DistanceSquaredTo(_netPos) > 400) GlobalPosition = _netPos;
        else GlobalPosition = p.Lerp(_netPos, Mathf.Clamp(dt * 12f, 0, 1));
        // Orientation interpolée sur la sphère (passage sol → mur → toit sans à-coup).
        var q = Quaternion.Normalized();
        Quaternion = q.Dot(_netRot) < 0 ? q.Slerp(-_netRot, Mathf.Clamp(dt * 10f, 0, 1)) : q.Slerp(_netRot, Mathf.Clamp(dt * 10f, 0, 1));
        _up = Basis.Y;
        _fwd = Basis.Z;
        _yaw = Mathf.Atan2(_fwd.X, _fwd.Z);
        _attackAnim = Mathf.Max(0, _attackAnim - dt * 3f);
        if (Grabbing) TickGrabFx(dt);
        if (IsMachine) ClientTickMachine(dt);
        if (IsTitan) ClientTickTitan(dt);
        if (IsAlien) ClientTickAlien(dt);
        AnimateModel(dt, _speedNow, Grabbing ? 0f : _attackAnim);
    }

    public void PlayAttack() => _attackAnim = 1f;

    // ---------------- Mort ----------------

    public void Die()
    {
        if (State == EnemyState.Dead) return;
        // La victime retombe avant que le cadavre ne bascule.
        ReleaseGrab();
        State = EnemyState.Dead;
        CollisionLayer = 0;
        if (IsFortress) { FortressDie(); return; }
        if (IsMachine) { MachineDie(); return; }
        if (IsTitan) { TitanDie(); return; }
        if (IsAlienKind) { AlienDie(); return; }
        var pos = Center;
        var color = Def.Type switch
        {
            EnemyType.AcidAnt => new Color(0.7f, 0.85f, 0.15f),
            EnemyType.Nest => new Color(0.35f, 0.28f, 0.2f),
            _ => new Color(0.55f, 0.5f, 0.15f),
        };
        Effects.Goo(_world, pos, color, Def.Size, Def.Boss ? 60 : 14);
        if (Def.Type == EnemyType.Nest || Def.Boss) Effects.Explosion(_world, pos, Def.Boss ? 14 : 9, 0);
        Sfx.At("death_bug", pos, Def.Boss ? 10 : 0, Def.Boss ? 0.5f : (float)GD.RandRange(0.8, 1.2), 30);
        if (Def.Type != EnemyType.Nest) Model.DeathPose();
        // Le cadavre tombe sur la surface en dessous (pied du mur, toit, sol) et se remet à plat.
        LandingPoint = Def.Static ? GlobalPosition : GroundBelow(GlobalPosition);
        var tw = CreateTween();
        if (Def.Flying)
        {
            tw.TweenProperty(this, "global_position:y", LandingPoint.Y + 0.3f, Mathf.Max(0.3f, (GlobalPosition.Y - LandingPoint.Y) / 25f)).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        }
        else if (Climbs && (GlobalPosition.DistanceTo(LandingPoint) > 0.2f || Basis.Y.Y < 0.95f))
        {
            var h = new Vector3(_fwd.X, 0, _fwd.Z);
            if (h.LengthSquared() < 0.01f) h = new Vector3(_up.X, 0, _up.Z);
            var upright = new Basis(Vector3.Up, h.LengthSquared() > 1e-4f ? Mathf.Atan2(h.X, h.Z) : _yaw).GetRotationQuaternion();
            var fall = Mathf.Clamp(Mathf.Sqrt(2f * Mathf.Max(GlobalPosition.Y - LandingPoint.Y, 0.1f) / 25f), 0.2f, 1.6f);
            tw.SetParallel();
            tw.TweenProperty(this, "global_position", LandingPoint, fall).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
            tw.TweenProperty(this, "quaternion", upright, Mathf.Min(fall, 0.5f));
            tw.SetParallel(false);
        }
        tw.TweenInterval(Def.Boss ? 10f : 3.5f);
        tw.TweenProperty(Model, "position:y", -3f * Def.Size, 1.5f);
        tw.TweenCallback(Callable.From(QueueFree));
    }

    private bool _camFaded;

    /// <summary>
    /// Translucide quand la caméra est dans le volume du corps (pattes comprises) ou que le corps cache le soldat suivi :
    /// avec des insectes de 10 m, celui qui passe entre la caméra et le soldat masquerait tout l'écran.
    /// </summary>
    public void CameraFade(Vector3 cam, Vector3 focus)
    {
        if (IsMachine) { MachineCameraFade(cam); return; }
        if (IsTitan) { TitanCameraFade(cam); return; }
        if (IsAlienKind) { AlienCameraFade(cam, focus); return; }
        if (IsFortress || Def.Static) return;
        var s = Def.Size;
        bool fade;
        if (State == EnemyState.Dead) fade = false;
        else if (_segShapes != null)
        {
            fade = false;
            foreach (var sh in _segShapes)
                if (SegmentDistance(sh.GlobalPosition, cam, focus) < 0.75f * s) { fade = true; break; }
        }
        else if (Def.Flying) fade = SegmentDistance(GlobalPosition, cam, focus) < 1.4f * s || GlobalPosition.DistanceTo(cam) < 2.6f * s;
        else
        {
            var inv = GlobalTransform.AffineInverse();
            var a = inv * cam;
            var b = inv * focus;
            // Caméra dans l'emprise des pattes, ou corps (thorax, gastre, tête) entre la caméra et le soldat.
            // La fourmi qui tient le soldat local : il est contre sa tête (le segment la traverse toujours) ;
            // seuls le thorax et le gastre comptent (caméra derrière la fourmi), la tête reste opaque et visable.
            var holdsMe = GrabPeer != 0 && GrabPeer == Net.I.MyId;
            fade = Mathf.Abs(a.X) < 2.4f * s && a.Y > -0.5f * s && a.Y < 2.6f * s && a.Z > -3.3f * s && a.Z < 2.5f * s
                || SegmentHitsBox(a, b, new Vector3(-1.1f, 0.4f, -3.1f) * s, new Vector3(1.1f, 2.2f, holdsMe ? 0.5f : 2.3f) * s);
        }
        if (fade == _camFaded) return;
        _camFaded = fade;
        Model.SetFade(fade ? 0.7f : 0f);
    }

    private static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var t = Mathf.Clamp((p - a).Dot(ab) / Mathf.Max(ab.LengthSquared(), 1e-6f), 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }

    /// <summary>Le segment [a, b] traverse-t-il la boîte alignée [min, max] (méthode des tranches) ?</summary>
    private static bool SegmentHitsBox(Vector3 a, Vector3 b, Vector3 min, Vector3 max)
    {
        var d = b - a;
        float t0 = 0f, t1 = 1f;
        for (var i = 0; i < 3; i++)
        {
            if (Mathf.Abs(d[i]) < 1e-6f)
            {
                if (a[i] < min[i] || a[i] > max[i]) return false;
                continue;
            }
            var u = (min[i] - a[i]) / d[i];
            var v = (max[i] - a[i]) / d[i];
            if (u > v) (u, v) = (v, u);
            t0 = Mathf.Max(t0, u);
            t1 = Mathf.Min(t1, v);
            if (t0 > t1) return false;
        }
        return true;
    }

    public void FlashHit()
    {
        _attackAnim = Mathf.Max(_attackAnim, 0.25f);
        if (IsAlien) Model.AlienFlinch();
        if (!IsFortress) Model.Flash(); // cible touchée en continu : pas de flash
    }

    // ---------------- Forteresse marcheuse ----------------

    private float _fLaserCd = 5f, _fMissileCd = 11f, _fCannonCd = 17f, _fDroneCd, _fCrushT;
    private int _fVolley, _fMissiles;
    private float _fVolleyT, _fMissileT, _fChargeT = -1f;
    private Vector3 _fCannonTarget;
    private bool _fEnraged;

    /// <summary>
    /// IA de la forteresse (hôte) : marche lente vers le joueur le plus proche en écrasant les immeubles,
    /// salves laser des tourelles, barrages de missiles, canon principal chargé 2 s. Sous 50 % : drones et cadence accrue.
    /// </summary>
    private void TickFortress(float dt, long peer, Vector3 target)
    {
        var pos = GlobalPosition;
        var to = target - pos;
        to.Y = 0;
        var dist = to.Length();
        if (!_fEnraged && Health < MaxHealth * 0.5f)
        {
            _fEnraged = true;
            _fDroneCd = 1.5f;
            Net.I.BroadcastMessage("La forteresse est endommagée : elle libère ses drones et redouble de violence !", 1);
        }
        var rate = _fEnraged ? 1.6f : 1f;

        // Lourde rotation sur place, puis avance tant que la cible n'est pas sous elle.
        if (dist > 0.5f)
        {
            _yaw = Mathf.LerpAngle(_yaw, Mathf.Atan2(to.X, to.Z), Mathf.Clamp(dt * 0.3f, 0, 1));
            Rotation = new Vector3(0, _yaw, 0);
        }
        var fwd = new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw));
        var align = dist > 0.5f ? fwd.Dot(to / dist) : 0f;
        var want = dist > 14f ? Def.Speed * Mathf.Clamp((align + 0.2f) / 1.2f, 0.3f, 1f) : 0f;
        _speedNow = Mathf.MoveToward(_speedNow, want, dt * 1.2f);
        GlobalPosition = pos + fwd * _speedNow * dt;

        _fCrushT -= dt;
        if (_fCrushT <= 0)
        {
            _fCrushT = 0.25f;
            CrushBuildings(GlobalPosition, 13f);
        }

        Model.FortressAim(target + Vector3.Up);
        _fLaserCd -= dt * rate;
        _fMissileCd -= dt * rate;
        _fCannonCd -= dt * rate;
        _attackAnim = Mathf.Max(_attackAnim, _fChargeT >= 0 ? _fChargeT * 0.5f : 0f);

        // Salve laser : chaque tourelle tire deux fois, en alternance.
        var turrets = Model.FortressTurretCount;
        if (_fVolley > 0)
        {
            _fVolleyT -= dt;
            if (_fVolleyT <= 0)
            {
                _fVolleyT = 0.14f;
                _fVolley--;
                var i = _fVolley % turrets;
                var aim = target + Vector3.Up + new Vector3((float)GD.RandRange(-3, 3), (float)GD.RandRange(-0.5, 1.5), (float)GD.RandRange(-3, 3));
                _world.HostEnemyProjectile(this, 3, Model.FortressTurretMuzzle(i), aim, 1);
                Net.I.BroadcastEnemyAttack(Id, 20 + i, aim);
            }
        }
        else if (_fLaserCd <= 0 && dist < Def.AttackRange)
        {
            _fLaserCd = Def.AttackCooldown * (float)GD.RandRange(0.8, 1.2);
            _fVolley = turrets * 2;
        }

        // Barrage de missiles en cloche autour de la cible.
        if (_fMissiles > 0)
        {
            _fMissileT -= dt;
            if (_fMissileT <= 0)
            {
                _fMissileT = 0.18f;
                _fMissiles--;
                var a = (float)GD.RandRange(0, Mathf.Tau);
                var r = Mathf.Sqrt((float)GD.Randf()) * 12f;
                var aim = new Vector3(target.X + Mathf.Cos(a) * r, 0, target.Z + Mathf.Sin(a) * r);
                FireBomb(_fMissiles % 2, aim, true);
                Net.I.BroadcastEnemyAttack(Id, 30 + _fMissiles % 2, aim);
            }
        }
        else if (_fMissileCd <= 0 && dist < Def.AttackRange)
        {
            _fMissileCd = (float)GD.RandRange(12, 15);
            _fMissiles = _fEnraged ? 12 : 8;
        }

        // Canon principal : 2 s de charge visible (zone marquée au sol), puis tir dévastateur.
        if (_fChargeT >= 0)
        {
            _fChargeT += dt;
            if (_fChargeT >= 2f)
            {
                _fChargeT = -1f;
                FortressFx(11, _fCannonTarget);
                Net.I.BroadcastEnemyAttack(Id, 11, _fCannonTarget);
                _world.HostEnemyBlast(_fCannonTarget, 10f, Def.Damage * 20f * _world.DamageMul * DamageScale);
            }
        }
        else if (_fCannonCd <= 0 && dist < Def.AttackRange)
        {
            _fCannonCd = (float)GD.RandRange(14, 18);
            _fChargeT = 0f;
            _fCannonTarget = new Vector3(target.X, 0.3f, target.Z);
            FortressFx(10, _fCannonTarget);
            Net.I.BroadcastEnemyAttack(Id, 10, _fCannonTarget);
        }

        // Seconde phase : lâcher de drones depuis la coque.
        if (_fEnraged)
        {
            _fDroneCd -= dt;
            if (_fDroneCd <= 0)
            {
                _fDroneCd = 20f;
                var n = _world.EnemiesById.Values.Count(e => e.Def.Type == EnemyType.Drone && e.State != EnemyState.Dead);
                for (var i = 0; i < 4 && n < 16; i++, n++)
                {
                    var a = i * Mathf.Tau / 4 + _yaw;
                    _world.HostSpawnEnemy(EnemyType.Drone, GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 12f);
                }
                FortressFx(12, target);
                Net.I.BroadcastEnemyAttack(Id, 12, target);
            }
        }
        State = _fVolley > 0 || _fMissiles > 0 || _fChargeT >= 0 ? EnemyState.Attacking : EnemyState.Moving;
    }

    /// <summary>Écrase les immeubles dont l'emprise touche le cercle donné (hôte).</summary>
    private void CrushBuildings(Vector3 c, float radius)
    {
        foreach (var b in _world.Buildings)
        {
            if (b.Destroyed) continue;
            var local = c - b.GlobalPosition;
            var dx = local.X - Mathf.Clamp(local.X, -b.Size.X / 2, b.Size.X / 2);
            var dz = local.Z - Mathf.Clamp(local.Z, -b.Size.Z / 2, b.Size.Z / 2);
            if (dx * dx + dz * dz <= radius * radius) _world.HostBuildingDamage(b.Id, b.MaxHealth + b.Health + 1e5f);
        }
    }

    /// <summary>Missile en cloche tiré d'un lance-missiles latéral (autoritaire sur l'hôte, visuel ailleurs).</summary>
    private void FireBomb(int pod, Vector3 aim, bool host)
    {
        var from = Model.FortressPod(pod) + Vector3.Up * 0.6f;
        var flat = new Vector3(aim.X - from.X, 0, aim.Z - from.Z);
        var t = Mathf.Clamp(flat.Length() / 22f, 2.4f, 5f);
        var v = flat / t + Vector3.Up * ((aim.Y - from.Y + 0.5f * Projectile.BombGravity * t * t) / t);
        var p = Projectile.Spawn(_world, ProjectileKind.Bomb, from, v, host, host ? Def.Damage * 2f * _world.DamageMul * DamageScale : 0, 5f);
        if (host) p.SourceEnemy = this;
        Sfx.At("launcher", from, 4f, 0.55f);
    }

    /// <summary>Effets des attaques de la forteresse, joués chez tous les pairs (10 = charge, 11 = tir, 12 = drones, 2x = laser, 3x = missile).</summary>
    public void FortressFx(int kind, Vector3 target)
    {
        if (IsMachine) { MachineFx(kind, target); return; }
        if (IsTitan) { TitanFx(kind, target); return; }
        if (IsAlienKind) { AlienFx(kind, target); return; }
        if (!IsFortress || State == EnemyState.Dead) return;
        switch (kind)
        {
            case 10:
                Model.FortressCharge();
                Effects.Ring(_world, target, 10f, new Color(1f, 0.15f, 0.1f), 2f);
                Effects.Ring(_world, target, 4f, new Color(1f, 0.15f, 0.1f), 2f);
                Sfx.At("wave", Model.FortressMuzzle, 12f, 0.45f, 0);
                break;
            case 11:
                Model.FortressFire();
                var from = Model.FortressMuzzle;
                Effects.Tracer(_world.FxRoot, from, target, new Color(0.6f, 0.95f, 1f), 1.3f, 0.3f);
                Effects.Tracer(_world.FxRoot, from, target, new Color(1f, 1f, 1f), 0.45f, 0.35f);
                Sfx.At("plasma", from, 12f, 0.35f, 0);
                _world.ShakeCamera(target, 1f, 60f);
                break;
            case 12:
                Sfx.At("buzz", GlobalPosition + Vector3.Up * 30f, 10f, 0.5f);
                break;
            case >= 20 and < 30:
                var m = Model.FortressTurretMuzzle(kind - 20);
                Model.FortressAim(target);
                Projectile.Spawn(_world, ProjectileKind.Laser, m, (target - m).Normalized() * 90f, false, 0, 0);
                Sfx.At("laser", m, -2f, 0.7f);
                break;
            case >= 30 and < 40:
                Model.FortressAim(target);
                FireBomb(kind - 30, target, false);
                break;
        }
    }

    /// <summary>Un pied se pose : poussière, secousse ; sur l'hôte, écrasement et dégâts de piétinement.</summary>
    private void OnFortressStomp(Vector3 p)
    {
        if (State == EnemyState.Dead) return;
        Effects.Dust(_world, p, new Vector3(9f, 3f, 9f));
        _world.ShakeCamera(p, 0.55f, 70f);
        Sfx.At("hammer", p, 10f, 0.3f, 80);
        if (!Net.I.IsHost) return;
        CrushBuildings(p, 5f);
        if (_world.NearestPlayer(p, out var peer, out var at) && at.DistanceTo(p) < 7f)
            Net.I.DamagePlayer(peer, Def.Damage * 6f * _world.DamageMul * DamageScale, p);
    }

    /// <summary>Mort de la forteresse : chapelet d'explosions sur la coque, effondrement, déflagration finale, enfouissement.</summary>
    private void FortressDie()
    {
        Model.FortressDeath();
        Sfx.At("collapse", Center, 12f, 0.6f, 0);
        var tw = CreateTween();
        var rng = new RandomNumberGenerator();
        for (var i = 0; i < 10; i++)
        {
            tw.TweenCallback(Callable.From(() =>
            {
                var o = new Vector3(rng.RandfRange(-9, 9), rng.RandfRange(-4, 5), rng.RandfRange(-11, 11));
                Effects.Explosion(_world, Model.FortressHullCenter + Basis * o, rng.RandfRange(6f, 11f), 0);
            }));
            tw.TweenInterval(0.38f);
        }
        tw.TweenCallback(Callable.From(() =>
        {
            var c = Model.FortressHullCenter;
            Effects.Explosion(_world, c, 22f, 0);
            Effects.Explosion(_world, c + Vector3.Up * 5f, 15f, 2);
            Effects.Dust(_world, _world.OnGround(c), new Vector3(46f, 14f, 46f));
            _world.ShakeCamera(c, 1.2f, 160f);
        }));
        tw.TweenInterval(12f);
        tw.TweenProperty(Model, "position:y", -32f, 6f).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(QueueFree));
    }
}
