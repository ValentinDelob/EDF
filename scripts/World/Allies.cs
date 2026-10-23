using System.Collections.Generic;
using System.IO;
using Godot;

namespace Rempart;

/// <summary>Soldat allié non joueur : suit le joueur le plus proche et tire sur les insectes.</summary>
public partial class Ally : Node3D
{
    public int Id;
    public float Health = 120;
    public float MaxHealth = 120;
    public bool Dead;
    public long Leader;
    public SoldierModel Model;
    private Label3D _label;
    private float _yaw, _pitch, _speed, _recoil;
    private Vector3 _netPos;
    private float _netYaw;
    private float _fireCd;
    private Enemy _target;
    private float _retarget;
    private Vector3 _formation;

    public static Ally Create(int id, Vector3 pos, Vector3 formation)
    {
        var a = new Ally { Id = id, Name = $"Ally{id}", _formation = formation };
        a.Model = SoldierModel.Create(SoldierClass.Rifleman);
        a.AddChild(a.Model);
        a._label = new Label3D
        {
            Text = "Soldat", Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 36, OutlineSize = 8, PixelSize = 0.005f,
            Position = new Vector3(0, 2.2f, 0), Modulate = new Color(0.65f, 0.85f, 1f, 0.8f), Font = UiKit.BodyFont, VisibilityRangeEnd = 40f,
        };
        a.AddChild(a._label);
        a.Position = pos;
        a._netPos = pos;
        return a;
    }

    public void HostTick(World w, float dt)
    {
        if (Dead) return;
        _fireCd -= dt;
        _retarget -= dt;
        _recoil = Mathf.MoveToward(_recoil, 0, dt * 4f);
        var pos = GlobalPosition;

        // Choix du chef : le joueur vivant le plus proche dans 25 m.
        if (Leader == 0 || !w.PlayerPosition(Leader, out _))
            if (w.NearestPlayer(pos, out var p, out var pp) && pp.DistanceTo(pos) < 25f) Leader = p;

        var goal = pos;
        if (Leader != 0 && w.PlayerPosition(Leader, out var lp))
        {
            var behind = lp + _formation;
            if (behind.DistanceTo(pos) > 3f) goal = behind;
        }
        var to = goal - pos;
        to.Y = 0;
        var dist = to.Length();
        var speed = dist > 12 ? 8.5f : 5.5f;
        if (dist > 0.5f)
        {
            var dir = dist < 14f ? to / dist : w.Flow.Direction(pos);
            if (dir == Vector3.Zero) dir = to / dist;
            var next = pos + dir * Mathf.Min(speed * dt, dist);
            if (!w.Flow.IsBlocked(next) || w.Flow.IsBlocked(pos)) GlobalPosition = next;
            _speed = speed;
        }
        else _speed = 0;
        // Téléportation de secours si le groupe est distancé.
        if (Leader != 0 && w.PlayerPosition(Leader, out var lp2) && lp2.DistanceTo(pos) > 80f) GlobalPosition = lp2 + _formation;

        if (_retarget <= 0 || _target == null || !IsInstanceValid(_target) || _target.State == EnemyState.Dead)
        {
            _retarget = 0.5f;
            _target = w.NearestEnemy(pos, 70f);
        }
        if (_target != null)
        {
            var aim = _target.Center - (pos + Vector3.Up * 1.5f);
            _yaw = Mathf.LerpAngle(_yaw, Mathf.Atan2(aim.X, aim.Z), Mathf.Clamp(dt * 8f, 0, 1));
            _pitch = Mathf.Atan2(aim.Y, new Vector2(aim.X, aim.Z).Length());
            if (_fireCd <= 0)
            {
                _fireCd = (float)GD.RandRange(0.12, 0.2);
                var hit = _target.Center + new Vector3((float)GD.RandRange(-0.8, 0.8), (float)GD.RandRange(-0.5, 0.8), (float)GD.RandRange(-0.8, 0.8));
                var from = Model.Muzzle.GlobalPosition;
                if (GD.Randf() < 0.75f) w.HostDamageEnemy(_target.Id, 5f * (1f + 0.25f * (int)w.Difficulty), hit, 0, false);
                Shot(w, from, hit);
                Net.I.BroadcastAllyShot(Id, hit);
            }
        }
        else if (_speed > 0.1f) _yaw = Mathf.LerpAngle(_yaw, Mathf.Atan2(to.X, to.Z), Mathf.Clamp(dt * 6f, 0, 1));
        Model.Rotation = new Vector3(0, _yaw, 0);
        Model.Animate(dt, _speed, _pitch, false, false, false, _target != null, _recoil);
    }

    public void Shot(World w, Vector3 from, Vector3 to)
    {
        _recoil = 0.5f;
        Effects.Tracer(w.FxRoot, from, to, new Color(1f, 0.75f, 0.35f), 0.035f);
        Sfx.At("rifle", from, -8f, 1.05f, 50);
    }

    public void PackSnapshot(BinaryWriter wr)
    {
        var p = GlobalPosition;
        wr.Write(Id);
        wr.Write(p.X); wr.Write(p.Z);
        wr.Write((short)(Mathf.Wrap(_yaw, -Mathf.Pi, Mathf.Pi) * 10000f));
        wr.Write((sbyte)Mathf.Clamp(_pitch * 100f, -127, 127));
        wr.Write((byte)Mathf.Clamp(_speed * 20f, 0, 255));
        wr.Write((byte)Mathf.Clamp(Health / MaxHealth * 255f, 0, 255));
    }

    public void ApplySnapshot(Vector3 pos, float yaw, float pitch, float speed, float hp)
    {
        _netPos = pos;
        _netYaw = yaw;
        _pitch = pitch;
        _speed = speed;
        Health = hp * MaxHealth;
    }

    public void ClientTick(float dt)
    {
        if (Dead) return;
        GlobalPosition = GlobalPosition.DistanceSquaredTo(_netPos) > 100 ? _netPos : GlobalPosition.Lerp(_netPos, Mathf.Clamp(dt * 10f, 0, 1));
        _yaw = Mathf.LerpAngle(_yaw, _netYaw, Mathf.Clamp(dt * 10f, 0, 1));
        _recoil = Mathf.MoveToward(_recoil, 0, dt * 4f);
        Model.Rotation = new Vector3(0, _yaw, 0);
        Model.Animate(dt, _speed, _pitch, false, false, false, true, _recoil);
    }

    public void Kill(World w)
    {
        if (Dead) return;
        Dead = true;
        _label.Visible = false;
        Model.Animate(0.016f, 0, 0, false, false, true, false, 0);
        var tw = CreateTween();
        tw.TweenInterval(0.2f);
        tw.TweenCallback(Callable.From(() => Model.Animate(1f, 0, 0, false, false, true, false, 0)));
        tw.TweenInterval(8f);
        tw.TweenProperty(this, "scale", Vector3.One * 0.01f, 0.6f);
        tw.TweenCallback(Callable.From(QueueFree));
        Sfx.At("hurt", GlobalPosition, 0f, 0.8f);
    }
}

/// <summary>Gestion des escouades alliées (simulation hôte, synchronisation réseau).</summary>
public class AllyManager
{
    private readonly World _w;
    public readonly Dictionary<int, Ally> ById = new();
    private int _nextId = 1;
    private float _snap;

    public AllyManager(World w) => _w = w;

    public void HostSpawnSquad(Vector3 center, int count)
    {
        for (var i = 0; i < count; i++)
        {
            // Formation en éventail sur les flancs du chef.
            var side = i % 2 == 0 ? -1f : 1f;
            var formation = new Vector3(side * (4f + (i / 2) * 2.5f), 0, 1f + (i / 2) * 1.5f);
            var id = _nextId++;
            var pos = center + new Vector3(i * 1.8f - count, 0, (i % 2) * 1.5f);
            Spawn(id, pos, formation);
            Net.I.BroadcastAllySpawn(id, pos, formation);
        }
    }

    public void Spawn(int id, Vector3 pos, Vector3 formation)
    {
        if (ById.ContainsKey(id)) return;
        var a = Ally.Create(id, pos, formation);
        _w.AddChild(a);
        ById[id] = a;
    }

    public void HostTick(float dt)
    {
        foreach (var a in ById.Values) a.HostTick(_w, dt);
        _snap -= dt;
        if (_snap > 0 || Net.I.IsSolo || ById.Count == 0) return;
        _snap = 0.1f;
        using var ms = new MemoryStream();
        using var wr = new BinaryWriter(ms);
        foreach (var a in ById.Values) if (!a.Dead) a.PackSnapshot(wr);
        wr.Flush();
        Net.I.BroadcastAllySnapshot(ms.ToArray());
    }

    public void ClientTick(float dt)
    {
        foreach (var a in ById.Values) a.ClientTick(dt);
    }

    public void ApplySnapshot(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var r = new BinaryReader(ms);
        while (ms.Position < ms.Length)
        {
            var id = r.ReadInt32();
            var pos = new Vector3(r.ReadSingle(), 0, r.ReadSingle());
            var yaw = r.ReadInt16() / 10000f;
            var pitch = r.ReadSByte() / 100f;
            var speed = r.ReadByte() / 20f;
            var hp = r.ReadByte() / 255f;
            if (ById.TryGetValue(id, out var a)) a.ApplySnapshot(pos, yaw, pitch, speed, hp);
        }
    }

    /// <summary>Allié le plus proche (cible secondaire des insectes).</summary>
    public Ally Nearest(Vector3 pos, float range)
    {
        Ally best = null;
        var bd = range * range;
        foreach (var a in ById.Values)
        {
            if (a.Dead) continue;
            var d = a.GlobalPosition.DistanceSquaredTo(pos);
            if (d < bd) { bd = d; best = a; }
        }
        return best;
    }

    public void HostDamage(Ally a, float dmg)
    {
        if (a.Dead) return;
        a.Health -= dmg;
        if (a.Health <= 0) Net.I.BroadcastAllyDeath(a.Id);
    }

    public void OnDeath(int id)
    {
        if (ById.TryGetValue(id, out var a)) a.Kill(_w);
    }

    public void OnShot(int id, Vector3 to)
    {
        if (ById.TryGetValue(id, out var a) && !a.Dead) a.Shot(_w, a.Model.Muzzle.GlobalPosition, to);
    }

    public int Alive
    {
        get
        {
            var n = 0;
            foreach (var a in ById.Values) if (!a.Dead) n++;
            return n;
        }
    }
}
