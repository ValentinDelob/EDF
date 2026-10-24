using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

/// <summary>Déroulé de mission côté hôte : phases, vagues, nids, conditions de victoire.</summary>
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

    public MissionDirector(World w)
    {
        _w = w;
        _m = w.Mission;
        if (_m.Goal == MissionGoal.DestroyNests)
        {
            for (var i = 0; i < _m.NestCount && i < w.City.NestSpots.Count; i++)
            {
                var n = w.HostSpawnEnemy(EnemyType.Nest, w.City.NestSpots[i]);
                _nests.Add(n);
                _nestTimers[n] = 2f + i;
            }
        }
    }

    /// <summary>Premier boss annoncé par les vagues de la mission (la reine par défaut).</summary>
    private EnemyType BossType => _m.Phases.SelectMany(p => p.Waves).SelectMany(w => w.Groups)
        .Select(g => g.type).Where(t => Enemies.Get(t).Boss).DefaultIfEmpty(EnemyType.Queen).First();

    private float PlayerScale => 1f + 0.4f * (Net.I.Players.Count - 1);

    public void Tick(float dt)
    {
        if (_finished || _w.Ended) return;
        var t = _w.Elapsed;
        if (_phase < 0) StartPhase(0);

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
            if (nest.State == EnemyState.Dead) continue;
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
        if (_phase + 1 < _m.Phases.Length && !_bossKilled)
        {
            var next = _m.Phases[_phase + 1];
            var pendingThisPhase = _queue.Count;
            var ready = next.TriggerTime > 0
                ? t - 0 >= next.TriggerTime
                : pendingThisPhase == 0 && active <= next.TriggerRemaining && t - _phaseStart > 4f;
            if (ready) StartPhase(_phase + 1);
        }

        UpdateObjective(active);
        CheckVictory(active, dt);
    }

    private void StartPhase(int index)
    {
        _phase = index;
        _phaseStart = _w.Elapsed;
        var phase = _m.Phases[index];
        foreach (var wave in phase.Waves)
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
            var zone = _w.City.Zones.TryGetValue(wave.Zone, out var z) ? z : Vector3.Zero;
            foreach (var (type, count) in wave.Groups)
            {
                var n = Enemies.Get(type).Boss ? count : Mathf.CeilToInt(count * PlayerScale);
                for (var i = 0; i < n; i++)
                    _queue.Add((_w.Elapsed + wave.Delay + i * 0.25f, type, PickSpawn(zone, type)));
            }
        }
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
                _w.ObjectiveText = alive > 0 ? $"Détruire les nids · {alive} restant(s)" : "Éliminer les derniers ennemis";
                break;
            case MissionGoal.Boss:
                _w.ObjectiveText = _bossKilled ? "Éliminer les derniers ennemis"
                    : BossType == EnemyType.Fortress ? "Détruire la forteresse marcheuse" : "Abattre la reine";
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
            _w.BossName = "Nids";
        }
        else _w.BossHealth = -1;
    }

    public void OnEnemyKilled(Enemy e)
    {
        if (e == _boss)
        {
            _bossKilled = true;
            _queue.Clear();
            Net.I.BroadcastMessage(e.Def.Type == EnemyType.Fortress ? "La forteresse s'effondre ! Nettoyez la zone." : "La reine est morte ! Nettoyez la zone.", 1);
        }
        if (e.Def.Type == EnemyType.Nest)
        {
            var alive = _nests.Count(n => n.State != EnemyState.Dead && n != e);
            Net.I.BroadcastMessage(alive > 0 ? $"Nid détruit ! Encore {alive}." : "Tous les nids sont détruits ! Éliminez les survivants.", 1);
            if (alive == 0)
            {
                _queue.Clear();
                _phase = _m.Phases.Length - 1;
            }
        }
    }

    private void CheckVictory(int active, float dt)
    {
        var allPhasesStarted = _phase >= _m.Phases.Length - 1;
        var done = _m.Goal switch
        {
            MissionGoal.Exterminate => allPhasesStarted && _queue.Count == 0 && active == 0,
            MissionGoal.DestroyNests => _nests.All(n => n.State == EnemyState.Dead) && _queue.Count == 0 && active == 0,
            MissionGoal.Boss => _bossKilled && _queue.Count == 0 && active == 0,
            _ => false,
        };
        if (!done) { _endCheckDelay = 3f; return; }
        _endCheckDelay -= dt;
        if (_endCheckDelay > 0) return;
        _finished = true;
        Net.I.BroadcastMessage("Zone sécurisée. Ramassez le matériel, extraction dans 15 secondes !", 1);
        var timer = _w.GetTree().CreateTimer(15f, false);
        timer.Timeout += () => { if (!_w.Ended) _w.EndMission(true); };
    }
}
