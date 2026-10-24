using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Déroulé de mission côté hôte : phases, vagues, nids, conditions de victoire et d'échec.</summary>
public class MissionDirector
{
    private readonly World _w;
    private readonly MissionDef _m;
    private int _phase = -1;
    private float _phaseStart;
    private readonly List<(float at, EnemyType type, Vector3 pos)> _queue = new();
    private readonly List<Enemy> _nests = new();
    private readonly Dictionary<Enemy, float> _nestTimers = new();
    private Enemy _boss;
    private bool _bossKilled;
    private bool _finished;
    private float _endCheckDelay = 3f;
    private const int MaxActive = 140;
    // Survie : prochaine vague, vagues lancées, début du compte à rebours.
    private float _nextWave = -1f, _surviveStart = -1f;
    private int _wave;
    private float _failDelay = -1f;

    public MissionDirector(World w)
    {
        _w = w;
        _m = w.Mission;
        if (_m.Goal == MissionGoal.DestroyNests)
        {
            for (var i = 0; i < _m.NestCount && i < w.City.NestSpots.Count; i++)
            {
                // Nids d'insectes, ou portails extraterrestres (MissionDef.NestType) : mêmes emplacements, même objectif.
                var n = w.HostSpawnEnemy(_m.NestType, w.City.NestSpots[i]);
                _nests.Add(n);
                _nestTimers[n] = 2f + i;
            }
        }
    }

    /// <summary>Premier boss annoncé par les vagues de la mission (la reine par défaut).</summary>
    private EnemyType BossType => _m.Phases.SelectMany(p => p.Waves).SelectMany(w => w.Groups)
        .Select(g => g.type).Where(t => Enemies.Get(t).Boss).DefaultIfEmpty(EnemyType.Queen).First();

    /// <summary>Nombre d'ennemis selon la taille de l'escouade (cahier des charges §9.3) : ×1, ×1,4, ×1,7, ×2.</summary>
    public static float ScaleForPlayers(int players) => players switch { <= 1 => 1f, 2 => 1.4f, 3 => 1.7f, _ => 2f };

    private float PlayerScale => ScaleForPlayers(Net.I.Players.Count);

    private MissionObjective Obj => _w.Objective;

    /// <summary>Mission de destruction de portails (au lieu de nids).</summary>
    private bool Portals => _m.NestType == EnemyType.Portal;

    public void Tick(float dt)
    {
        if (_finished || _w.Ended) return;
        var t = _w.Elapsed;
        if (_m.Goal == MissionGoal.Survival) TickSurvival(t);
        else if (_phase < 0) StartPhase(0);

        // Apparitions en attente (plafond d'ennemis actifs).
        var active = _w.EnemiesById.Values.Count(e => e.State != EnemyState.Dead && !e.IsStatic);
        for (var i = 0; i < _queue.Count && active < MaxActive; i++)
        {
            if (_queue[i].at > t) continue;
            var (_, type, pos) = _queue[i];
            var e = _w.HostSpawnEnemy(type, pos);
            if (e.Def.Boss) _boss = e;
            _queue.RemoveAt(i--);
            active++;
        }

        // Nids : génèrent des fourmis en continu.
        foreach (var nest in _nests)
        {
            // Un portail fait passer ses groupes lui-même (Enemy.HostTickPortal).
            if (nest.State == EnemyState.Dead || nest.IsPortal) continue;
            _nestTimers[nest] -= dt;
            if (_nestTimers[nest] > 0) continue;
            _nestTimers[nest] = 3.2f / PlayerScale;
            if (active >= MaxActive) continue;
            var near = _w.EnemiesById.Values.Count(e => !e.IsStatic && e.GlobalPosition.DistanceTo(nest.GlobalPosition) < 40f);
            if (near >= 10) continue;
            var type = _w.Rng.Randf() < 0.2f ? EnemyType.RedAnt : EnemyType.BlackAnt;
            var a = _w.Rng.Randf() * Mathf.Tau;
            _w.HostSpawnEnemy(type, nest.GlobalPosition + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (9f * nest.Def.Size + 2f * Enemies.Get(type).Size));
            active++;
        }

        // Phase suivante ?
        if (_m.Goal != MissionGoal.Survival && _phase + 1 < _m.Phases.Length && !_bossKilled)
        {
            var next = _m.Phases[_phase + 1];
            var pendingThisPhase = _queue.Count;
            var ready = next.TriggerTime > 0
                ? t - 0 >= next.TriggerTime
                : pendingThisPhase == 0 && active <= next.TriggerRemaining && t - _phaseStart > 4f;
            if (ready) StartPhase(_phase + 1);
        }

        UpdateObjective(active);
        if (CheckDefeat(dt)) return;
        CheckVictory(active, dt);
    }

    private void StartPhase(int index)
    {
        _phase = index;
        _phaseStart = _w.Elapsed;
        SpawnWaves(_m.Phases[index].Waves, 1f);
    }

    /// <summary>Met en file les groupes des vagues (nombre × taille de l'escouade × <paramref name="mul"/>, boss exceptés).</summary>
    private void SpawnWaves(WaveDef[] waves, float mul)
    {
        foreach (var wave in waves)
        {
            if (!string.IsNullOrEmpty(wave.Message))
            {
                var msg = wave.Message;
                if (wave.Delay <= 0.1f) Net.I.BroadcastMessage(msg, 1);
                else
                {
                    var timer = _w.GetTree().CreateTimer(wave.Delay, false);
                    timer.Timeout += () => { if (!_w.Ended) Net.I.BroadcastMessage(msg, 1); };
                }
            }
            var zone = ZoneOf(wave.Zone);
            foreach (var (type, count) in wave.Groups)
            {
                var n = Enemies.Get(type).Boss ? count : Mathf.CeilToInt(count * PlayerScale * mul);
                for (var i = 0; i < n; i++)
                    _queue.Add((_w.Elapsed + wave.Delay + i * 0.25f, type, PickSpawn(zone, type)));
            }
        }
    }

    /// <summary>
    /// Zone d'apparition : zones de la carte, ou (Escorte) « ahead » / « behind » : 110 m devant le camion de tête
    /// ou 70 m derrière le dernier camion, le long du trajet du convoi.
    /// </summary>
    private Vector3 ZoneOf(string name)
    {
        if (Obj != null && Obj.Goal == MissionGoal.Escort && (name == "ahead" || name == "behind"))
        {
            var alive = Obj.Trucks.Where(x => !x.Destroyed).ToList();
            if (alive.Count > 0)
            {
                if (name == "ahead") return Obj.RoutePoint(Mathf.Min(alive[0].S + 110f, Obj.RouteLength));
                if (alive[^1].S >= 70f) return Obj.RoutePoint(alive[^1].S - 70f);
                // Convoi encore au départ : zone de la carte la plus éloignée du camion de tête.
                var lead = alive[0].GlobalPosition;
                return _w.City.Zones.Values.OrderByDescending(z => z.DistanceSquaredTo(lead)).FirstOrDefault();
            }
        }
        return _w.City.Zones.TryGetValue(name, out var z) ? z : Vector3.Zero;
    }

    private Vector3 PickSpawn(Vector3 zone, EnemyType type)
    {
        var def = Enemies.Get(type);
        // Les grands insectes sortent de terre là où leur corps tient (centre et quatre points à une demi-longueur).
        var r = 2f * def.Size;
        for (var tries = 0; tries < 20; tries++)
        {
            var p = zone + new Vector3(_w.Rng.RandfRange(-28, 28), 0, _w.Rng.RandfRange(-22, 22));
            if (def.Flying) return p;
            if (_w.Flow.IsBlocked(p)) continue;
            if (tries < 14 && (_w.Flow.IsBlocked(p + new Vector3(r, 0, 0)) || _w.Flow.IsBlocked(p - new Vector3(r, 0, 0))
                || _w.Flow.IsBlocked(p + new Vector3(0, 0, r)) || _w.Flow.IsBlocked(p - new Vector3(0, 0, r)))) continue;
            return p;
        }
        return zone;
    }

    // ------------------------------------------------------------------ Survie

    /// <summary>
    /// Survie : les vagues des phases tournent en boucle, une toutes les <see cref="ObjectiveDef.WaveInterval"/> s,
    /// chacune plus fournie que la précédente (+<see cref="ObjectiveDef.WaveGrowth"/> par vague) ; victoire à la fin du compte à rebours.
    /// </summary>
    private void TickSurvival(float t)
    {
        if (Obj == null || _m.Phases.Length == 0) return;
        if (_surviveStart < 0) { _surviveStart = t; _nextWave = t; }
        Obj.SurviveLeft = Mathf.Max(0, Obj.SurviveSeconds - (t - _surviveStart));
        if (t >= _nextWave && Obj.SurviveLeft > 5f)
        {
            _phase = _wave % _m.Phases.Length;
            SpawnWaves(_m.Phases[_phase].Waves, 1f + Obj.Def.WaveGrowth * _wave);
            _wave++;
            // La cadence se resserre aussi un peu (jusqu'à 60 % de l'intervalle).
            _nextWave = t + Obj.Def.WaveInterval * Mathf.Max(0.6f, 1f - 0.02f * _wave);
        }
    }

    // ------------------------------------------------------------------ Objectif et fin

    private void UpdateObjective(int active)
    {
        var remaining = active + _queue.Count;
        switch (_m.Goal)
        {
            case MissionGoal.Exterminate:
                _w.ObjectiveText = $"Éliminer toutes les vagues · phase {_phase + 1}/{_m.Phases.Length}";
                break;
            case MissionGoal.DestroyNests:
                var alive = _nests.Count(n => n.State != EnemyState.Dead);
                _w.ObjectiveText = alive > 0 ? $"Détruire les {(Portals ? "portails" : "nids")} · {alive} restant(s)" : "Éliminer les derniers ennemis";
                break;
            case MissionGoal.Boss:
                _w.ObjectiveText = _bossKilled ? "Éliminer les derniers ennemis"
                    : BossType == EnemyType.Fortress ? "Détruire la forteresse marcheuse"
                    : BossType == EnemyType.Titan ? "Abattre le titan reptilien" : "Abattre la reine";
                break;
            case MissionGoal.Defense:
                var s = Obj?.Structure;
                var hp = s != null ? Mathf.RoundToInt(100f * s.Health / Mathf.Max(1f, s.MaxHealth)) : 0;
                _w.ObjectiveText = $"Défendre : {(s?.Label ?? "structure").ToLowerInvariant()} ({hp} %) · phase {_phase + 1}/{_m.Phases.Length}";
                break;
            case MissionGoal.Escort:
                if (Obj != null)
                {
                    var left = Obj.Trucks.Count(x => !x.Destroyed);
                    var stop = Obj.StopReason == 2 ? " · à l'arrêt, sans escorte" : Obj.StopReason == 1 ? " · à l'arrêt, route barrée" : "";
                    _w.ObjectiveText = $"Escorter le convoi · {left}/{Obj.Trucks.Count} camions · {Obj.ConvoyProgress * 100:0} % du trajet{stop}";
                }
                break;
            case MissionGoal.Survival:
                if (Obj != null) _w.ObjectiveText = $"Tenir jusqu'à l'évacuation · {UiKit.Time(Obj.SurviveLeft)}";
                break;
        }
        _w.EnemiesRemaining = remaining;
        if (_boss != null && !_bossKilled)
        {
            _w.BossHealth = Mathf.Clamp(_boss.Health / _boss.MaxHealth, 0, 1);
            _w.BossName = _boss.Def.Name;
        }
        else if (_m.Goal == MissionGoal.DestroyNests && _nests.Any(n => n.State != EnemyState.Dead))
        {
            _w.BossHealth = _nests.Where(n => n.State != EnemyState.Dead).Sum(n => n.Health / n.MaxHealth) / _nests.Count;
            _w.BossName = Portals ? "Portails" : "Nids";
        }
        else _w.BossHealth = -1;
    }

    public void OnEnemyKilled(Enemy e)
    {
        if (e == _boss)
        {
            _bossKilled = true;
            _queue.Clear();
            Net.I.BroadcastMessage(e.Def.Type switch
            {
                EnemyType.Fortress => "La forteresse s'effondre ! Nettoyez la zone.",
                EnemyType.Titan => "Le titan s'abat ! Nettoyez la zone.",
                _ => "La reine est morte ! Nettoyez la zone.",
            }, 1);
        }
        if (e.Def.Type == EnemyType.Nest || e.IsPortal)
        {
            var alive = _nests.Count(n => n.State != EnemyState.Dead && n != e);
            if (e.IsPortal) Net.I.BroadcastMessage(alive > 0 ? $"Portail détruit ! Encore {alive}." : "Tous les portails sont détruits ! Éliminez les survivants.", 1);
            else Net.I.BroadcastMessage(alive > 0 ? $"Nid détruit ! Encore {alive}." : "Tous les nids sont détruits ! Éliminez les survivants.", 1);
            if (alive == 0)
            {
                _queue.Clear();
                _phase = _m.Phases.Length - 1;
            }
        }
    }

    /// <summary>Échec propre à l'objectif : structure détruite, ou tous les camions détruits (fin 3 s plus tard, le temps de voir la chute).</summary>
    private bool CheckDefeat(float dt)
    {
        if (Obj == null || Obj.Completed) return false;
        var lost = Obj.StructureLost || Obj.ConvoyLost;
        if (!lost) return false;
        if (_failDelay < 0)
        {
            _failDelay = 3f;
            Net.I.BroadcastMessage(Obj.StructureLost ? "La position est tombée. Mission compromise." : "Le convoi est anéanti. Mission compromise.", 1);
        }
        _failDelay -= dt;
        if (_failDelay > 0) return true;
        _finished = true;
        _w.EndMission(false);
        return true;
    }

    private void CheckVictory(int active, float dt)
    {
        var allPhasesStarted = _phase >= _m.Phases.Length - 1;
        var cleared = allPhasesStarted && _queue.Count == 0 && active == 0;
        var done = _m.Goal switch
        {
            MissionGoal.Exterminate => cleared,
            MissionGoal.DestroyNests => _nests.All(n => n.State == EnemyState.Dead) && _queue.Count == 0 && active == 0,
            MissionGoal.Boss => _bossKilled && _queue.Count == 0 && active == 0,
            MissionGoal.Defense => cleared,
            // Escorte et survie : l'objectif suffit, même avec des insectes encore en vie.
            MissionGoal.Escort => Obj?.ConvoyArrived ?? false,
            MissionGoal.Survival => _surviveStart >= 0 && Obj != null && Obj.SurviveLeft <= 0,
            _ => false,
        };
        if (!done) { _endCheckDelay = _m.Goal is MissionGoal.Escort or MissionGoal.Survival ? 0f : 3f; return; }
        _endCheckDelay -= dt;
        if (_endCheckDelay > 0) return;
        _finished = true;
        _queue.Clear();
        Net.I.BroadcastMessage(_m.Goal switch
        {
            MissionGoal.Escort => "Le convoi est arrivé ! Ramassez le matériel, extraction dans 15 secondes !",
            MissionGoal.Survival => "Les hélicoptères arrivent ! Ramassez le matériel, extraction dans 15 secondes !",
            _ => "Zone sécurisée. Ramassez le matériel, extraction dans 15 secondes !",
        }, 1);
        if (_m.Goal is MissionGoal.Escort or MissionGoal.Survival) RetreatFarEnemies();
        // Compte à rebours d'extraction (affiché discrètement), puis fin de mission ; plus d'échec possible entre-temps.
        if (Obj != null) Obj.HostComplete();
        else
        {
            var timer = _w.GetTree().CreateTimer(15f, false);
            timer.Timeout += () => { if (!_w.Ended) _w.EndMission(true); };
        }
    }

    /// <summary>Objectif rempli avec des insectes encore en vie : ceux qui sont loin des soldats (> 60 m) se replient (retirés sans butin).</summary>
    private void RetreatFarEnemies()
    {
        var players = _w.AlivePlayerPositions().ToList();
        foreach (var e in _w.EnemiesById.Values.ToList())
        {
            if (e.State == EnemyState.Dead || e.IsStatic || e.Def.Boss) continue;
            if (players.Any(p => p.DistanceTo(e.GlobalPosition) < 60f)) continue;
            _w.HostRetreatEnemy(e);
        }
    }
}
