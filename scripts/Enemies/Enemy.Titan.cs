using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>
/// Titan reptilien (voir docs/ARCHITECTURE.md, « Titan reptilien ») : IA hôte, effets rejoués chez chaque pair, plaques
/// d'armure à briser et dégâts localisés.
///
/// <list type="bullet">
/// <item>Marche lourde vers le joueur le plus proche en traversant les immeubles (effondrés au contact) et en écrasant
/// mobilier et véhicules ; chaque pas secoue le sol et blesse qui se trouve dessous.</item>
/// <item>Souffle de feu : la gorge s'embrase et la tête se cabre (charge de 1,8 s, tracé marqué au sol), puis un jet
/// long balaie la cible en travers ; il s'arrête au premier obstacle, blesse soldats (et véhicules), alliés, immeubles
/// et mobilier, et laisse le sol en feu.</item>
/// <item>Balayage de la queue contre les soldats qui le prennent à revers ; rugissement (secousse de caméra) à
/// l'arrivée et à chaque phase ; titubation quand une plaque cède.</item>
/// <item>Phases : à 50 % puis 20 % des PV, il enrage (plus rapide, souffles plus longs et plus fréquents, double
/// balayage en dernière phase).</item>
/// <item>Huit plaques d'armure (PV propres) : intactes, la zone ne prend que 25 % des dégâts ; brisées, elles tombent et
/// découvrent une chair incandescente (×3 au total).</item>
/// </list>
/// Réseau : aucune RPC nouvelle, genres 80 à 97 d'<c>EnemyAttack</c> (80-87 plaque brisée, 90/91 début et fin du balayage
/// du souffle, 94 coup de queue, 95 rugissement, 96 phase, 97 titubation).
/// </summary>
public partial class Enemy
{
    public bool IsTitan => Def.Type == EnemyType.Titan;

    /// <summary>PV d'une plaque d'armure (Normal, un joueur), multipliés comme les PV du titan.</summary>
    public const float TitanPlateHp = 1600f;

    private const float BreathCharge = 1.8f;

    private (int Bone, Vector3 Center, Vector3 Axis, float Radius, float Half)[] _tShapeDefs;
    private CollisionShape3D[] _tShapes;
    private readonly float[] _tPlateHp = new float[BugModel.TitanPlateCount];
    private readonly bool[] _tPlateBroken = new bool[BugModel.TitanPlateCount];
    private TitanFire _tFire;
    private AudioStreamPlayer3D _tVoice, _tFireSnd;

    // IA (hôte).
    private float _tBreathCd = 6f, _tTailCd = 5f, _tCrushT, _tIntro = 2.2f, _tGroundT, _tSecondT = -1f, _tTurn;
    private int _tPhase;
    private bool _tHinted;

    // Déroulés (tous les pairs).
    private float _brT = -1f, _brDur = 3.6f, _brDmgT, _brDecorT, _brPatchT;
    private Vector3 _brStart, _brEnd, _brImpact;
    private bool _brFiring;
    private float _tlT = -1f, _tlSide = 1f;
    private float _trT = -1f, _tsT = -1f, _tShakeT;
    private readonly HashSet<long> _tlHitPeers = new();
    private readonly HashSet<int> _tlHitAllies = new();

    /// <summary>Foyers au sol (hôte) : position et fin.</summary>
    private readonly List<(Vector3 P, float End)> _tGround = new();

    /// <summary>Journal du titan (tests : --fps, --titan, --machines).</summary>
    private static readonly bool TitanLog = Screenshot.CmdArg("fps") != null || Screenshot.CmdArg("titan-log") != null || Screenshot.CmdArg("machines") != null || Screenshot.CmdArg("showcase") == "titan";

    private void TLog(string msg)
    {
        if (TitanLog) GD.Print($"[titan] n°{Id} t={_world.Elapsed:0.0} PV {Health:0}/{MaxHealth:0} : {msg}");
    }

    /// <summary>Phase en cours (0, 1 sous 50 %, 2 sous 20 %), connue de chaque pair (genre 96).</summary>
    public int TitanPhase => _tPhase;

    /// <summary>Plaque brisée ? (tous les pairs)</summary>
    public bool TitanPlateBroken(int i) => _tPlateBroken[i];

    /// <summary>PV restants d'une plaque (hôte).</summary>
    public float TitanPlateHealth(int i) => _tPlateHp[i];

    /// <summary>Souffle en cours (charge ou jet) : temps écoulé, ou -1.</summary>
    public float TitanBreathTime => _brT;

    // ---------------- Création ----------------

    private void CreateTitanShapes(float hpMul)
    {
        _tShapeDefs = BugModel.TitanShapes();
        _tShapes = new CollisionShape3D[_tShapeDefs.Length];
        for (var i = 0; i < _tShapeDefs.Length; i++)
        {
            var d = _tShapeDefs[i];
            Shape3D s = d.Axis == Vector3.Zero ? new SphereShape3D { Radius = d.Radius } : new CapsuleShape3D { Radius = d.Radius, Height = 2f * (d.Half + d.Radius) };
            _tShapes[i] = new CollisionShape3D { Shape = s, Position = d.Center };
            AddChild(_tShapes[i]);
        }
        _mShapes = _tShapes;
        for (var i = 0; i < _tPlateHp.Length; i++) _tPlateHp[i] = TitanPlateHp * hpMul;
        State = EnemyState.Moving;
        Model.Stomp += OnTitanStomp;
    }

    private void TitanReady()
    {
        _tFire = TitanFire.Create(_world, this);
        _world.FxRoot.AddChild(_tFire);
        _tVoice = new AudioStreamPlayer3D { Bus = "Sfx", MaxDistance = 900f, UnitSize = 90f, VolumeDb = 4f, AttenuationFilterCutoffHz = 12000f };
        _tFireSnd = new AudioStreamPlayer3D { Bus = "Sfx", MaxDistance = 500f, UnitSize = 45f, VolumeDb = 0f };
        AddChild(_tVoice);
        AddChild(_tFireSnd);
    }

    private void SyncTitanShapes()
    {
        if (_tShapes == null || !IsInsideTree()) return;
        for (var i = 0; i < _tShapes.Length; i++)
        {
            var d = _tShapeDefs[i];
            _tShapes[i].GlobalTransform = Model.TitanShapeXf(d.Bone, d.Center, d.Axis);
        }
    }

    // ---------------- Dégâts localisés ----------------

    /// <summary>Zone de plaque la plus proche d'un point (tolérance × rayon), ou -1.</summary>
    private int TitanZone(Vector3 p, float tol)
    {
        var best = -1;
        var bd = float.MaxValue;
        for (var i = 0; i < BugModel.TitanPlateCount; i++)
        {
            var d = p.DistanceTo(Model.TitanPlatePoint(i));
            if (d < Model.TitanPlateRadius(i) * tol && d < bd) { bd = d; best = i; }
        }
        return best;
    }

    /// <summary>
    /// Dégâts réellement subis (hôte) : plaque intacte → la plaque encaisse et le titan ne prend que 25 % ; chair à vif
    /// (plaque brisée) → ×3 au total (le tireur a déjà compté ×1,5 s'il a visé la plaie) ; tête ×1,5 ; peau ×1.
    /// </summary>
    public float TitanAdjustDamage(float dmg, bool weak, Vector3 at)
    {
        var blast = BlastOrigin;
        var zone = TitanZone(blast ?? at, blast != null ? 1.6f : 1.2f);
        if (zone < 0) return dmg;
        if (!_tPlateBroken[zone])
        {
            _tPlateHp[zone] -= dmg;
            if (_tPlateHp[zone] <= 0f) HostBreakPlate(zone);
            return dmg * 0.25f;
        }
        return weak ? dmg * 2f : dmg * 3f;
    }

    /// <summary>Multiplicateur affiché (chiffres de dégâts du tireur) selon la zone touchée, sans rien modifier.</summary>
    public float TitanDisplayScale(Vector3 at, bool weak)
    {
        var zone = TitanZone(at, 1.2f);
        if (zone < 0) return 1f;
        return _tPlateBroken[zone] ? (weak ? 2f : 3f) : 0.25f;
    }

    /// <summary>Impact visible : éclats d'os et étincelles sur une plaque, braises sur la chair à vif, sang sombre sur la peau.</summary>
    public void TitanHitFx(Vector3 at)
    {
        var fx = ExplosionFx.For(_world);
        var zone = TitanZone(at, 1.2f);
        var n = (at - Center).Normalized();
        if (zone >= 0 && !_tPlateBroken[zone])
        {
            for (var i = 0; i < 3; i++)
                fx.Spark(at, (n + new Vector3((float)GD.RandRange(-0.8, 0.8), (float)GD.RandRange(-0.2, 1), (float)GD.RandRange(-0.8, 0.8))).Normalized() * (float)GD.RandRange(4, 9),
                    0.1f, (float)GD.RandRange(0.15, 0.3), new Color(1f, 0.85f, 0.55f));
            if (GD.Randf() < 0.3f) fx.Chunk(at, n * 5f + Vector3.Up * 3f, 0.25f, 3f, new Color(0.3f, 0.28f, 0.24f));
        }
        else if (zone >= 0)
        {
            for (var i = 0; i < 3; i++)
                fx.Spark(at, (n + new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(0, 1), (float)GD.RandRange(-1, 1))).Normalized() * (float)GD.RandRange(3, 8),
                    0.14f, (float)GD.RandRange(0.4, 0.8), new Color(1f, 0.4f, 0.1f));
            Effects.Goo(_world, at, new Color(0.35f, 0.04f, 0.02f), 0.6f, 3);
        }
        else Effects.Goo(_world, at, new Color(0.18f, 0.03f, 0.02f), 0.6f, 3);
    }

    /// <summary>Plaque brisée (hôte) : diffusée (genre 80 + i), titubation, rugissement de douleur une fois sur deux.</summary>
    private void HostBreakPlate(int i)
    {
        if (_tPlateBroken[i]) return;
        var p = Model.TitanPlatePoint(i);
        TitanFx(80 + i, p);
        Net.I.BroadcastEnemyAttack(Id, 80 + i, p);
        var broken = _tPlateBroken.Count(b => b);
        TLog($"plaque {i} brisée ({broken}/{BugModel.TitanPlateCount})");
        if (!_tHinted)
        {
            _tHinted = true;
            Net.I.BroadcastMessage("Une plaque du titan a cédé : visez la chair à vif !", 1);
        }
        if (!_brFiring && _tlT < 0f && _trT < 0f) TitanSend(97, GlobalPosition);
    }

    // ---------------- IA (hôte) ----------------

    private void TitanSend(int kind, Vector3 target)
    {
        TitanFx(kind, target);
        Net.I.BroadcastEnemyAttack(Id, kind, target);
    }

    private void HostTickTitan(float dt)
    {
        _attackAnim = Mathf.Max(0, _attackAnim - dt * 1.5f);
        var hasTarget = _world.NearestPlayer(GlobalPosition, out _, out var target);
        // Phases : 50 % et 20 % des PV.
        var frac = Health / MaxHealth;
        var want = frac <= 0.2f ? 2 : frac <= 0.5f ? 1 : 0;
        if (want > _tPhase)
        {
            TitanSend(96, new Vector3(want, 0, 0));
            TitanSend(95, GlobalPosition);
            Net.I.BroadcastMessage(want == 1 ? "Le titan enrage ! Ses souffles redoublent." : "Le titan est à l'agonie et devient fou furieux !", 1);
            TLog($"phase {want}");
        }
        var rate = _tPhase switch { 0 => 1f, 1 => 1.3f, _ => 1.6f };
        if (_tIntro > 0f)
        {
            _tIntro -= dt;
            if (_tIntro <= 0f) TitanSend(95, GlobalPosition);
        }
        _tBreathCd -= dt * rate;
        _tTailCd -= dt * rate;
        TickTitanFx(dt, true);

        // Second balayage (dernière phase) : en sens inverse, juste après le premier.
        if (_tSecondT >= 0f)
        {
            _tSecondT -= dt;
            if (_tSecondT < 0f && _brT < 0f && hasTarget) StartBreath(target, true);
        }

        var pos = GlobalPosition;
        var to = hasTarget ? target - pos : Vector3.Zero;
        to.Y = 0;
        var dist = to.Length();
        var busy = _brT >= 0f || _tlT >= 0f || _trT >= 0f || _tsT >= 0f;
        if (hasTarget && !busy)
        {
            if (_tTailCd <= 0f && TailTarget(out var side)) StartTail(side);
            else if (_tBreathCd <= 0f && dist > 16f && dist < Def.AttackRange && Mathf.Abs(Mathf.Wrap(Mathf.Atan2(to.X, to.Z) - _yaw, -Mathf.Pi, Mathf.Pi)) < 0.55f)
                StartBreath(target, false);
        }

        // Déplacement : lourde rotation, avance tant que la cible est loin ; immobile pendant charge, queue, rugissement.
        busy = _brT >= 0f || _tlT >= 0f || _trT >= 0f || _tsT >= 0f;
        var fwd = new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw));
        if (hasTarget && dist > 0.5f && _tlT < 0f && _tsT < 0f)
        {
            var aim = _brFiring ? _brImpact - pos : to;
            var dy = Mathf.Wrap(Mathf.Atan2(aim.X, aim.Z) - _yaw, -Mathf.Pi, Mathf.Pi);
            var turn = (_brT >= 0f ? 0.12f : 0.2f) * rate;
            _tTurn = Mathf.MoveToward(_tTurn, Mathf.Clamp(dy * 0.8f, -turn, turn), dt * 0.25f);
            _yaw += _tTurn * dt;
            Rotation = new Vector3(0, _yaw, 0);
        }
        else _tTurn = Mathf.MoveToward(_tTurn, 0f, dt * 0.3f);
        var align = dist > 0.5f ? fwd.Dot(to / dist) : 0f;
        var wantSpeed = !hasTarget || busy || dist < 9f ? 0f : Def.Speed * (1f + 0.22f * _tPhase) * Mathf.Clamp((align + 0.3f) / 1.3f, 0.2f, 1f);
        _speedNow = Mathf.MoveToward(_speedNow, wantSpeed, dt * 0.9f);
        var next = pos + fwd * _speedNow * dt;
        next.X = Mathf.Clamp(next.X, -205f, 205f);
        next.Z = Mathf.Clamp(next.Z, -205f, 135f);
        next.Y = 0f;
        GlobalPosition = next;

        // Le corps traverse les immeubles ; les pieds font le reste (voir OnTitanStomp).
        _tCrushT -= dt;
        if (_tCrushT <= 0f)
        {
            _tCrushT = 0.25f;
            CrushBuildings(GlobalPosition + fwd * 6f, 13f);
            CrushBuildings(Model.TitanChest with { Y = 0f }, 11f);
        }
        TickGroundFires(dt);
        if (hasTarget) Model.TitanAim(BreathAim() ?? target + Vector3.Up * 2f);
        AnimateModel(dt, _speedNow, _attackAnim);
        State = busy ? EnemyState.Attacking : EnemyState.Moving;
    }

    /// <summary>Soldat ou allié à revers ou tout près (coup de queue) ; côté +1 = droite du titan.</summary>
    private bool TailTarget(out float side)
    {
        var s = 1f;
        var inv = GlobalTransform.AffineInverse();
        var best = float.MaxValue;
        void Consider(Vector3 p)
        {
            var l = inv * p;
            var d = new Vector2(l.X, l.Z).Length();
            if (d > 44f) return;
            // Derrière ou sur les côtés (hors du cône du souffle) ; devant, il piétine.
            var behind = l.Z < 6f || Mathf.Abs(l.X) > l.Z * 1.2f;
            if (!behind) return;
            if (d < best) { best = d; s = l.X >= 0 ? 1f : -1f; }
        }
        foreach (var (_, p) in TargetsNear(GlobalPosition, 48f)) Consider(p);
        if (_world.Allies != null)
            foreach (var a in _world.Allies.ById.Values)
                if (!a.Dead && a.GlobalPosition.DistanceTo(GlobalPosition) < 48f) Consider(a.GlobalPosition);
        side = s;
        return best < float.MaxValue;
    }

    private void StartTail(float side)
    {
        _tTailCd = (float)GD.RandRange(7, 10);
        TitanSend(94, new Vector3(side, 0, 0));
        TLog($"coup de queue {(side > 0 ? "à droite" : "à gauche")}");
    }

    private void StartBreath(Vector3 target, bool second)
    {
        var pos = GlobalPosition;
        var dir = new Vector3(target.X - pos.X, 0, target.Z - pos.Z);
        var dist = dir.Length();
        dir = dist > 0.01f ? dir / dist : new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw));
        var side = dir.Cross(Vector3.Up) * (second ? -_brSide : (GD.Randf() < 0.5f ? -1f : 1f));
        _brSide = side.Dot(dir.Cross(Vector3.Up)) > 0 ? 1f : -1f;
        var span = 20f + 4f * _tPhase;
        var g = new Vector3(target.X, Mathf.Max(target.Y, 0f), target.Z);
        var start = g + side * span - dir * 8f;
        var end = g - side * span + dir * 14f;
        _tBreathCd = (float)GD.RandRange(10, 13);
        _tSecondT = _tPhase >= 2 && !second ? BreathCharge + BreathDuration + 0.4f : -1f;
        TitanSend(90, start);
        TitanSend(91, end);
        TLog($"souffle {(second ? "(second balayage) " : "")}{start.Snapped(Vector3.One)} → {end.Snapped(Vector3.One)}");
    }

    private float _brSide = 1f;

    private float BreathDuration => _tPhase switch { 0 => 3.6f, 1 => 4.6f, _ => 5.4f };

    /// <summary>Point visé par le souffle en cours (charge : début du tracé), ou null.</summary>
    private Vector3? BreathAim()
    {
        if (_brT < 0f) return null;
        if (_brT < BreathCharge) return _brStart + Vector3.Up * 6f;
        var k = Mathf.Clamp((_brT - BreathCharge) / _brDur, 0f, 1f);
        return _brStart.Lerp(_brEnd, k * k * (3f - 2f * k));
    }

    /// <summary>Tests (hôte) : force une action — breath, breath2 (double balayage), tail, roar, stagger, plate (prochaine plaque intacte).</summary>
    public void TitanForce(string act)
    {
        var hasTarget = _world.NearestPlayer(GlobalPosition, out _, out var target);
        if (!hasTarget) target = GlobalPosition + new Vector3(Mathf.Sin(_yaw), 0, Mathf.Cos(_yaw)) * 60f;
        switch (act)
        {
            case "breath": StopBreath(); StartBreath(target, false); break;
            case "breath2": StopBreath(); _tPhase = 2; StartBreath(target, false); break;
            case "tail": StartTail(TailTarget(out var side) ? side : 1f); break;
            case "roar": TitanSend(95, GlobalPosition); break;
            case "stagger": TitanSend(97, GlobalPosition); break;
            case "plate":
                for (var i = 0; i < _tPlateBroken.Length; i++)
                    if (!_tPlateBroken[i]) { _tPlateHp[i] = 0f; HostBreakPlate(i); break; }
                break;
        }
    }

    // ---------------- Déroulés (tous les pairs) ----------------

    /// <summary>Client : regard vers le joueur le plus proche ou le souffle, déroulés locaux.</summary>
    private void ClientTickTitan(float dt)
    {
        if (BreathAim() is { } a) Model.TitanAim(a);
        else if (_world.NearestPlayer(GlobalPosition, out _, out var t)) Model.TitanAim(t + Vector3.Up * 2f);
        TickTitanFx(dt, false);
    }

    private void TickTitanFx(float dt, bool host)
    {
        // Souffle.
        if (_brT >= 0f)
        {
            _brT += dt;
            _attackAnim = Mathf.Max(_attackAnim, 0.7f);
            if (_brT < BreathCharge)
            {
                var k = _brT / BreathCharge;
                Model.TitanCharge = k * k;
                Model.TitanRear = 0.35f * Mathf.Sin(k * Mathf.Pi * 0.5f);
                Model.TitanJaw = 0.15f * k;
                Model.TitanAim(_brStart + Vector3.Up * 6f);
                if (GD.Randf() < dt * 25f)
                {
                    var fx = ExplosionFx.For(_world);
                    var m = Model.TitanMouth;
                    fx.Spark(m, (Model.TitanSnoutDir + new Vector3((float)GD.RandRange(-0.4, 0.4), 0.3f, (float)GD.RandRange(-0.4, 0.4))).Normalized() * 4f, 0.12f, 0.8f, new Color(1f, 0.5f, 0.15f));
                }
            }
            else if (_brT < BreathCharge + _brDur && State != EnemyState.Dead)
            {
                Model.TitanRear = 0f;
                Model.TitanCharge = 1f;
                Model.TitanFiring = true;
                var aim = BreathAim() ?? _brEnd;
                Model.TitanAim(aim);
                BreathTick(dt, aim, host);
            }
            else StopBreath();
        }
        // Queue : armé 0,8 s, frappe 0,7 s, retour 1,1 s.
        if (_tlT >= 0f)
        {
            _tlT += dt;
            float f;
            if (_tlT < 0.8f) f = -0.85f * Mathf.SmoothStep(0, 1, _tlT / 0.8f);
            else if (_tlT < 1.5f) f = Mathf.Lerp(-0.85f, 1.75f, Mathf.SmoothStep(0, 1, (_tlT - 0.8f) / 0.7f));
            else f = 1.75f * (1f - Mathf.SmoothStep(0, 1, (_tlT - 1.5f) / 1.1f));
            Model.TitanTailSwing = -_tlSide * f;
            _attackAnim = Mathf.Max(_attackAnim, 0.6f);
            if (host && _tlT is > 0.95f and < 1.6f) TailHits();
            if (_tlT >= 2.6f) { _tlT = -1f; Model.TitanTailSwing = 0f; }
        }
        // Rugissement : 3,2 s, secousses répétées.
        if (_trT >= 0f)
        {
            _trT += dt;
            var e = Mathf.Clamp(_trT / 0.5f, 0f, 1f) * (1f - Mathf.SmoothStep(0, 1, (_trT - 2.5f) / 0.7f));
            Model.TitanRear = Mathf.Max(Model.TitanRear, e);
            Model.TitanJaw = Mathf.Max(Model.TitanJaw, e);
            _tShakeT -= dt;
            if (_trT is > 0.3f and < 2.7f && _tShakeT <= 0f)
            {
                _tShakeT = 0.22f;
                _world.ShakeCamera(Model.TitanSkull, 0.45f, 320f);
            }
            if (_trT >= 3.2f) { _trT = -1f; Model.TitanRear = 0f; Model.TitanJaw = 0f; }
        }
        // Titubation : 1,5 s.
        if (_tsT >= 0f)
        {
            _tsT += dt;
            Model.TitanStagger = Mathf.Clamp(1f - _tsT / 1.5f, 0f, 1f);
            if (_tsT >= 1.5f) { _tsT = -1f; Model.TitanStagger = 0f; }
        }
    }

    private void StopBreath()
    {
        _brT = -1f;
        _brFiring = false;
        Model.TitanFiring = false;
        Model.TitanCharge = 0f;
        Model.TitanJaw = 0f;
        Model.TitanRear = 0f;
        _tFire?.HideJet();
        if (_tFireSnd != null && _tFireSnd.Playing) _tFireSnd.Stop();
    }

    /// <summary>Jet de feu de la gueule au point visé (arrêté par le premier obstacle) ; l'hôte inflige les dégâts.</summary>
    private void BreathTick(float dt, Vector3 aim, bool host)
    {
        var from = Model.TitanMouth;
        var dir = (aim - from).Normalized();
        var reach = Mathf.Min(from.DistanceTo(aim) + 22f, 120f);
        var q = PhysicsRayQueryParameters3D.Create(from, from + dir * reach, 1u);
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(q);
        var impact = hit.Count > 0 ? (Vector3)hit["position"] : from + dir * reach;
        var normal = hit.Count > 0 ? (Vector3)hit["normal"] : Vector3.Up;
        _brImpact = impact;
        var ramp = Mathf.Clamp((_brT - BreathCharge) / 0.3f, 0f, 1f) * Mathf.Clamp((BreathCharge + _brDur - _brT) / 0.35f, 0f, 1f);
        _tFire?.Show(from, impact, Mathf.Max(ramp, 0.05f), normal, hit.Count > 0);
        if (!_brFiring)
        {
            _brFiring = true;
            TLog($"souffle allumé, impact {impact.Snapped(Vector3.One)}");
            if (Sfx.Loop("titan_fire") is { } loop && _tFireSnd != null)
            {
                _tFireSnd.Stream = loop;
                _tFireSnd.Play();
            }
            Sfx.At("titan_flare", from, 10f, 1f, 0);
            _world.ShakeCamera(impact, 0.4f, 80f);
        }
        if (_tFireSnd != null) _tFireSnd.GlobalPosition = from.Lerp(impact, 0.4f);
        // Le sol prend feu le long du balayage.
        _brPatchT -= dt;
        if (_brPatchT <= 0f && hit.Count > 0 && normal.Y > 0.45f)
        {
            _brPatchT = 0.26f;
            var life = 10f + 2f * _tPhase;
            _tFire?.AddGround(impact, life);
            if (host) _tGround.Add((impact, _world.Elapsed + life));
        }
        if (!host) return;
        // Dégâts toutes les 0,1 s : soldats et alliés dans le jet (plus large au bout), immeuble touché, mobilier.
        _brDmgT -= dt;
        if (_brDmgT > 0f) return;
        _brDmgT = 0.1f;
        var dmg = Def.Damage * _world.DamageMul * DamageScale;
        foreach (var (peer, ppos) in TargetsNear(from, 140f))
        {
            var c = ppos + Vector3.Up;
            var cp = Geometry3D.GetClosestPointToSegment(c, from, impact);
            var t = from.DistanceTo(cp) / Mathf.Max(from.DistanceTo(impact), 1f);
            if (cp.DistanceTo(c) < Mathf.Lerp(2.2f, 6.5f, t) || c.DistanceTo(impact) < 6f) Net.I.DamagePlayer(peer, dmg, impact);
        }
        if (_world.Allies != null)
            foreach (var a in _world.Allies.ById.Values.ToList())
            {
                if (a.Dead) continue;
                var c = a.GlobalPosition + Vector3.Up;
                var cp = Geometry3D.GetClosestPointToSegment(c, from, impact);
                var t = from.DistanceTo(cp) / Mathf.Max(from.DistanceTo(impact), 1f);
                if (cp.DistanceTo(c) < Mathf.Lerp(2.2f, 6.5f, t) || c.DistanceTo(impact) < 6f) _world.Allies.HostDamage(a, dmg * 1.5f);
            }
        if (hit.Count > 0 && hit["collider"].AsGodotObject() is Building b && !b.Destroyed) _world.HostBuildingDamage(b.Id, 120f, impact);
        _brDecorT -= 0.1f;
        if (_brDecorT <= 0f)
        {
            _brDecorT = 0.2f;
            _world.Decor?.HostBlast(impact, 6f, 70f, 0);
        }
    }

    /// <summary>Foyers au sol (hôte) : brûlures toutes les 0,25 s, mobilier qui s'embrase.</summary>
    private void TickGroundFires(float dt)
    {
        if (_tGround.Count == 0) return;
        _tGroundT -= dt;
        if (_tGroundT > 0f) return;
        _tGroundT = 0.25f;
        var now = _world.Elapsed;
        _tGround.RemoveAll(g => g.End <= now);
        var dmg = Def.Damage * 0.8f * _world.DamageMul;
        var burnDecor = (int)(now * 4f) % 4 == 0;
        foreach (var (p, _) in _tGround)
        {
            foreach (var (peer, ppos) in TargetsNear(p, 5f))
                if (ppos.Y - p.Y < 3f) Net.I.DamagePlayer(peer, dmg, p);
            if (_world.Allies != null)
                foreach (var a in _world.Allies.ById.Values)
                    if (!a.Dead && a.GlobalPosition.DistanceTo(p) < 4.5f) _world.Allies.HostDamage(a, dmg);
            if (burnDecor) _world.Decor?.HostBlast(p, 4f, 30f, 0);
        }
    }

    /// <summary>Coup de queue (hôte) : chaque soldat ou allié sur la trajectoire est touché une fois ; immeubles et mobilier écrasés.</summary>
    private void TailHits()
    {
        var dmg = Def.Damage * 12f * _world.DamageMul * DamageScale;
        for (var k = 2; k <= 8; k++)
        {
            var tp = Model.TitanTail(k);
            var r = (k < 5 ? 6.5f : 4.5f) + 1.5f;
            foreach (var (peer, ppos) in TargetsNear(tp with { Y = 0 }, r + 4f))
                if (!_tlHitPeers.Contains(peer) && (ppos + Vector3.Up).DistanceTo(tp) < r + 2.5f)
                {
                    _tlHitPeers.Add(peer);
                    Net.I.DamagePlayer(peer, dmg, tp);
                }
            if (_world.Allies != null)
                foreach (var a in _world.Allies.ById.Values.ToList())
                    if (!a.Dead && !_tlHitAllies.Contains(a.Id) && (a.GlobalPosition + Vector3.Up).DistanceTo(tp) < r + 2.5f)
                    {
                        _tlHitAllies.Add(a.Id);
                        _world.Allies.HostDamage(a, dmg);
                    }
            if (k % 2 == 1)
            {
                CrushBuildings(tp with { Y = 0f }, r);
                _world.Decor?.HostCrushArea(tp with { Y = 0f }, r, r, 6);
            }
        }
    }

    /// <summary>
    /// Effets du titan (tous les pairs) : 80-87 plaque brisée (centre), 90 début du balayage du souffle, 91 fin et
    /// départ de la charge, 94 coup de queue (X = côté), 95 rugissement, 96 phase (X), 97 titubation.
    /// </summary>
    public void TitanFx(int kind, Vector3 target)
    {
        if (State == EnemyState.Dead) return;
        if (!Net.I.IsHost) TLog($"effet {kind} reçu de l'hôte");
        switch (kind)
        {
            case >= 80 and < 88:
                BreakPlateFx(kind - 80);
                break;
            case 90:
                _brStart = target;
                break;
            case 91:
                _brEnd = target;
                _brT = 0f;
                _brDur = BreathDuration;
                _brDmgT = 0f;
                _brPatchT = 0f;
                _brFiring = false;
                // Tracé du balayage marqué au sol pendant la charge.
                for (var i = 0; i <= 4; i++)
                    Effects.Ring(_world, _brStart.Lerp(_brEnd, i / 4f) with { Y = Mathf.Max(_brStart.Y, 0f) + 0.2f }, i is 0 or 4 ? 6f : 3.2f, new Color(1f, 0.38f, 0.08f), BreathCharge + 0.8f);
                PlayVoice("titan_charge", 2f);
                break;
            case 94:
                _tlT = 0f;
                _tlSide = target.X >= 0 ? 1f : -1f;
                _tlHitPeers.Clear();
                _tlHitAllies.Clear();
                Sfx.At("titan_tail", Model.TitanTail(5), 8f, 1f, 0);
                break;
            case 95:
                _trT = 0f;
                _tShakeT = 0f;
                PlayVoice("titan_roar", 6f);
                break;
            case 96:
                _tPhase = Mathf.Clamp((int)target.X, 0, 2);
                break;
            case 97:
                _tsT = 0f;
                PlayVoice("titan_pain", 4f);
                break;
        }
    }

    private void PlayVoice(string name, float db)
    {
        if (_tVoice == null || Sfx.Stream(name) is not { } s) return;
        _tVoice.Stream = s;
        _tVoice.VolumeDb = db;
        _tVoice.PitchScale = (float)GD.RandRange(0.95, 1.04);
        _tVoice.Play();
    }

    /// <summary>Plaque brisée (tous les pairs) : elle se détache et tombe, la chair à vif s'ouvre, éclats et poussière.</summary>
    private void BreakPlateFx(int i)
    {
        if (_tPlateBroken[i]) return;
        _tPlateBroken[i] = true;
        var (mesh, xf) = Model.TitanBreakPlate(i);
        var n = (xf.Origin - Center).Normalized();
        TitanPlateDebris.Spawn(_world, mesh, xf, n * 6f + Vector3.Up * 4f + Basis.Z * 2f);
        var fx = ExplosionFx.For(_world);
        var p = xf.Origin;
        for (var k = 0; k < 14; k++)
        {
            var v = (n + new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(-0.3, 1), (float)GD.RandRange(-1, 1))).Normalized() * (float)GD.RandRange(5, 14);
            fx.Chunk(p + v * 0.1f, v, (float)GD.RandRange(0.35, 0.9), 6f, new Color(0.32f, 0.29f, 0.25f));
        }
        for (var k = 0; k < 10; k++)
            fx.Spark(p, (n + new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(0, 1), (float)GD.RandRange(-1, 1))).Normalized() * (float)GD.RandRange(6, 16), 0.2f, (float)GD.RandRange(0.5, 1.2), new Color(1f, 0.45f, 0.12f));
        fx.DustPuff(p, n * 4f, 7f, 3f, new Color(0.35f, 0.32f, 0.28f));
        fx.SmokePuff(p + n * 2f, n * 3f + Vector3.Up * 2f, 6f, 3f, new Color(0.25f, 0.05f, 0.03f));
        Effects.Goo(_world, p, new Color(0.3f, 0.04f, 0.02f), 2.5f, 12);
        Sfx.At("titan_plate", p, 10f, (float)GD.RandRange(0.9, 1.05), 0);
        _world.ShakeCamera(p, 0.5f, 120f);
    }

    /// <summary>Un pied se pose : poussière, secousse, grondement ; sur l'hôte, immeubles et mobilier écrasés, soldats piétinés.</summary>
    private void OnTitanStomp(Vector3 p)
    {
        if (State == EnemyState.Dead) return;
        var fx = ExplosionFx.For(_world);
        var far = fx.CameraDistance(p) > 200f;
        for (var i = 0; i < (far ? 3 : 8); i++)
        {
            var d = new Vector3(Mathf.Cos(i * 0.785f), 0, Mathf.Sin(i * 0.785f));
            fx.DustPuff(p + d * 3f + Vector3.Up * 0.6f, d * 7f + Vector3.Up * 1.2f, 5.5f, 3.2f, new Color(0.46f, 0.43f, 0.38f));
        }
        if (!far)
            for (var i = 0; i < 6; i++)
                fx.Chunk(p + Vector3.Up, new Vector3((float)GD.RandRange(-6, 6), (float)GD.RandRange(4, 9), (float)GD.RandRange(-6, 6)), (float)GD.RandRange(0.2, 0.5), 4f, new Color(0.36f, 0.34f, 0.31f));
        _world.ShakeCamera(p, 0.6f, 140f);
        Sfx.At("titan_step", p, 12f, (float)GD.RandRange(0.9, 1.05), 60);
        if (!Net.I.IsHost) return;
        CrushBuildings(p, 7f);
        _world.Decor?.HostCrushArea(p, 6.5f, 6.5f, 6);
        foreach (var (peer, at) in TargetsNear(p, 8f))
            if (at.Y - p.Y < 4f) Net.I.DamagePlayer(peer, Def.Damage * 10f * _world.DamageMul * DamageScale, p);
        if (_world.Allies != null)
            foreach (var a in _world.Allies.ById.Values.ToList())
                if (!a.Dead && a.GlobalPosition.DistanceTo(p) < 7.5f) _world.Allies.HostDamage(a, Def.Damage * 10f * _world.DamageMul);
    }

    // ---------------- Mort ----------------

    /// <summary>
    /// Mort (tous les pairs) : râle d'agonie, le colosse bascule sur le côté et s'abat (souffle de poussière, secousse,
    /// immeubles écrasés sous le corps), puis s'enfonce. Le butin tombe le long du corps.
    /// </summary>
    private void TitanDie()
    {
        StopBreath();
        _tlT = _trT = _tsT = -1f;
        TLog("abattu");
        var side = GD.Randf() < 0.5f ? 1f : -1f;
        if (Net.I.IsHost) side = 1f;
        Model.TitanDeath(side);
        PlayVoice("titan_death", 8f);
        var body = GlobalTransform * new Vector3(side * 26f, 0, 2f);
        LandingPoint = body with { Y = 0f };
        var tw = CreateTween();
        tw.TweenInterval(BugModel.TitanFallTime);
        tw.TweenCallback(Callable.From(() =>
        {
            var fx = ExplosionFx.For(_world);
            var root = GlobalPosition;
            for (var i = 0; i <= 10; i++)
            {
                // Poussière soulevée tout le long du corps abattu (de la queue à la tête).
                var p = GlobalTransform * new Vector3(side * Mathf.Lerp(18f, 50f, i / 10f), 0, Mathf.Lerp(-40f, 30f, (i % 5) / 4f));
                p.Y = 1f;
                for (var k = 0; k < 4; k++)
                {
                    var d = new Vector3((float)GD.RandRange(-1, 1), 0, (float)GD.RandRange(-1, 1)).Normalized();
                    fx.DustPuff(p + d * 4f, d * 12f + Vector3.Up * 3f, 9f, 5f, new Color(0.45f, 0.42f, 0.37f));
                }
            }
            Effects.Dust(_world, LandingPoint, new Vector3(70f, 16f, 70f));
            _world.ShakeCamera(root, 1.4f, 380f);
            Sfx.At("collapse", LandingPoint, 14f, 0.55f, 0);
            Sfx.At("titan_step", LandingPoint, 16f, 0.6f, 0);
            if (!Net.I.IsHost) return;
            for (var i = 0; i <= 6; i++)
            {
                var p = GlobalTransform * new Vector3(side * Mathf.Lerp(14f, 52f, i / 6f), 0, 0);
                CrushBuildings(p with { Y = 0 }, 12f);
                _world.Decor?.HostCrushArea(p with { Y = 0 }, 12f, 12f, 6);
            }
        }));
        tw.TweenInterval(18f);
        tw.TweenProperty(Model, "position:y", -38f, 9f).SetEase(Tween.EaseType.In);
        tw.TweenCallback(Callable.From(QueueFree));
    }

    // ---------------- Caméra ----------------

    /// <summary>Caméra dans le corps (torse, bassin, cou, tête) : le modèle devient translucide.</summary>
    private void TitanCameraFade(Vector3 cam)
    {
        var fade = false;
        if (State != EnemyState.Dead && _tShapes != null)
            for (var i = 0; i < 4 && !fade; i++)
                fade = _tShapes[i].GlobalPosition.DistanceTo(cam) < _tShapeDefs[i].Radius + _tShapeDefs[i].Half * 0.8f;
        if (fade == _camFaded) return;
        _camFaded = fade;
        Model.SetFade(fade ? 0.7f : 0f);
    }
}

/// <summary>Plaque d'armure arrachée : tombe en tournoyant, rebondit lourdement (poussière, secousse), reste au sol puis s'enfonce.</summary>
public partial class TitanPlateDebris : MeshInstance3D
{
    private Vector3 _vel, _spinAxis;
    private float _spin, _age;
    private bool _landed;
    private World _world;

    public static void Spawn(World world, ArrayMesh mesh, Transform3D xf, Vector3 vel)
    {
        var d = new TitanPlateDebris { Mesh = mesh, _world = world, _vel = vel, CastShadow = ShadowCastingSetting.On };
        d._spinAxis = new Vector3((float)GD.RandRange(-1, 1), (float)GD.RandRange(-0.3, 0.3), (float)GD.RandRange(-1, 1)).Normalized();
        d._spin = (float)GD.RandRange(0.6, 1.4);
        world.FxRoot.AddChild(d);
        d.GlobalTransform = xf;
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;
        _age += dt;
        if (_age > 40f) { QueueFree(); return; }
        if (_age > 30f)
        {
            GlobalPosition += Vector3.Down * dt * 0.6f;
            return;
        }
        if (_landed) return;
        _vel += Vector3.Down * 22f * dt;
        var p = GlobalPosition + _vel * dt;
        var b = GlobalBasis.Rotated(_spinAxis, _spin * dt);
        if (p.Y < 1.2f)
        {
            p.Y = 1.2f;
            if (_vel.Y < -6f)
            {
                // Rebond lourd.
                var fx = ExplosionFx.For(_world);
                for (var k = 0; k < 6; k++)
                {
                    var d = new Vector3(Mathf.Cos(k * 1.05f), 0, Mathf.Sin(k * 1.05f));
                    fx.DustPuff(p + d * 2f, d * 6f + Vector3.Up, 4.5f, 2.5f, new Color(0.45f, 0.42f, 0.37f));
                }
                _world.ShakeCamera(p, 0.35f, 60f);
                Sfx.At("titan_step", p, 2f, 1.5f, 0);
                _vel = new Vector3(_vel.X * 0.35f, -_vel.Y * 0.22f, _vel.Z * 0.35f);
                _spin *= 0.4f;
            }
            else _landed = true;
        }
        GlobalTransform = new Transform3D(b.Orthonormalized(), p);
    }
}
