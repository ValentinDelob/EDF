using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rempart;

public class PlayerInfo
{
    public long Id;
    public string Name = "Soldat";
    public SoldierClass Class;
    public bool Ready;
    public float MaxArmor = 250;
}

/// <summary>
/// Réseau : salon LAN (ENet), découverte UDP et toutes les RPC de jeu.
/// L'hôte (id 1) fait autorité sur les ennemis, les caisses, la destruction et le déroulé de mission.
/// </summary>
public partial class Net : Node
{
    public static Net I { get; private set; }

    public const int GamePort = 27015;
    public const int DiscoveryPort = 27016;
    public const int MaxPlayers = 4;

    public readonly Dictionary<long, PlayerInfo> Players = new();
    public bool InSession { get; private set; }
    public bool IsHost => Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer || Multiplayer.IsServer();
    public bool IsSolo => Multiplayer.MultiplayerPeer is OfflineMultiplayerPeer;
    public long MyId => Multiplayer.GetUniqueId();
    public string SessionName = "Partie";
    public int LobbyMission;
    public Difficulty LobbyDifficulty = Difficulty.Normal;

    public event Action LobbyChanged;
    public event Action<string> SessionEnded;
    public event Action<int, Difficulty, int> MissionStarting;
    public event Action ReturnToLobby;

    // Découverte LAN
    public record FoundGame(string Ip, string Name, int Players, string Mission, double Seen);
    public readonly Dictionary<string, FoundGame> FoundGames = new();
    private PacketPeerUdp _broadcaster;
    private PacketPeerUdp _listener;
    private double _broadcastTimer;

    public override void _EnterTree()
    {
        I = this;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += () => EndSession("Connexion impossible à l'hôte.");
        Multiplayer.ServerDisconnected += () => EndSession("L'hôte a quitté la partie.");
    }

    // ---------- Session ----------

    public void StartSolo()
    {
        Close();
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
        InSession = true;
        SessionName = "Solo";
        Players.Clear();
        Players[1] = MakeLocalInfo(1);
        LobbyChanged?.Invoke();
    }

    public Error Host()
    {
        Close();
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateServer(GamePort, MaxPlayers);
        if (err != Error.Ok) return err;
        Multiplayer.MultiplayerPeer = peer;
        InSession = true;
        SessionName = $"Partie de {Settings.I.PlayerName}";
        Players.Clear();
        Players[1] = MakeLocalInfo(1);
        _broadcaster = new PacketPeerUdp();
        _broadcaster.SetBroadcastEnabled(true);
        _broadcaster.SetDestAddress("255.255.255.255", DiscoveryPort);
        LobbyChanged?.Invoke();
        return Error.Ok;
    }

    public Error Join(string ip)
    {
        Close();
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateClient(ip, GamePort);
        if (err != Error.Ok) return err;
        Multiplayer.MultiplayerPeer = peer;
        InSession = true;
        Players.Clear();
        return Error.Ok;
    }

    public void Close()
    {
        if (Multiplayer.MultiplayerPeer != null && Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer)
            Multiplayer.MultiplayerPeer.Close();
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
        _broadcaster?.Close();
        _broadcaster = null;
        InSession = false;
        Players.Clear();
    }

    public void Leave() => EndSession(null);

    private void EndSession(string reason)
    {
        var was = InSession;
        Close();
        if (was) SessionEnded?.Invoke(reason);
    }

    private PlayerInfo MakeLocalInfo(long id) => new()
    {
        Id = id,
        Name = Settings.I.PlayerName,
        Class = Profile.I.Data.SelectedClass,
        MaxArmor = Profile.I.Armor(Profile.I.Data.SelectedClass),
    };

    public void StartListening()
    {
        if (_listener != null) return;
        _listener = new PacketPeerUdp();
        if (_listener.Bind(DiscoveryPort) != Error.Ok) { _listener = null; return; }
    }

    public void StopListening()
    {
        _listener?.Close();
        _listener = null;
        FoundGames.Clear();
    }

    public override void _Process(double delta)
    {
        if (_broadcaster != null && IsHost && !IsSolo)
        {
            _broadcastTimer -= delta;
            if (_broadcastTimer <= 0)
            {
                _broadcastTimer = 1.0;
                var msg = $"RMP1|{SessionName}|{Players.Count}|{Missions.All[LobbyMission].Name}";
                _broadcaster.PutPacket(msg.ToUtf8Buffer());
            }
        }
        if (_listener != null)
        {
            var now = Time.GetTicksMsec() / 1000.0;
            while (_listener.GetAvailablePacketCount() > 0)
            {
                var data = _listener.GetPacket().GetStringFromUtf8();
                var ip = _listener.GetPacketIP();
                var parts = data.Split('|');
                if (parts.Length >= 4 && parts[0] == "RMP1")
                    FoundGames[ip] = new FoundGame(ip, parts[1], int.TryParse(parts[2], out var n) ? n : 0, parts[3], now);
            }
            foreach (var k in FoundGames.Where(kv => now - kv.Value.Seen > 4).Select(kv => kv.Key).ToList())
                FoundGames.Remove(k);
        }
    }

    // ---------- Salon ----------

    private void OnPeerConnected(long id)
    {
        if (!IsHost) return;
        if (World.Current != null)
        {
            // Pas de connexion en cours de mission : l'hôte refuse poliment.
            RpcId(id, MethodName.Kick, "Mission en cours, réessayez entre deux missions.");
            return;
        }
    }

    private void OnPeerDisconnected(long id)
    {
        if (Players.Remove(id))
        {
            if (IsHost) BroadcastLobby();
            World.Current?.OnPeerLeft(id);
            LobbyChanged?.Invoke();
        }
    }

    private void OnConnectedToServer()
    {
        var p = MakeLocalInfo(MyId);
        RpcId(1, MethodName.RegisterPlayer, p.Name, (int)p.Class, p.MaxArmor);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Kick(string reason)
    {
        EndSession(reason);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RegisterPlayer(string name, int cls, float armor)
    {
        if (!IsHost) return;
        var id = Multiplayer.GetRemoteSenderId();
        if (Players.Count >= MaxPlayers) { RpcId(id, MethodName.Kick, "La partie est pleine."); return; }
        Players[id] = new PlayerInfo { Id = id, Name = name, Class = (SoldierClass)cls, MaxArmor = armor };
        BroadcastLobby();
    }

    public void SetLocalClass(SoldierClass c)
    {
        Profile.I.Data.SelectedClass = c;
        Profile.I.Save();
        if (!InSession) return;
        var armor = Profile.I.Armor(c);
        if (IsHost) ApplyClass(MyId, c, armor);
        else RpcId(1, MethodName.RequestClass, (int)c, armor);
    }

    public void SetLocalReady(bool ready)
    {
        if (IsHost) { if (Players.TryGetValue(MyId, out var p)) { p.Ready = ready; BroadcastLobby(); } }
        else RpcId(1, MethodName.RequestReady, ready);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestClass(int cls, float armor)
    {
        if (!IsHost) return;
        ApplyClass(Multiplayer.GetRemoteSenderId(), (SoldierClass)cls, armor);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestReady(bool ready)
    {
        if (!IsHost) return;
        if (Players.TryGetValue(Multiplayer.GetRemoteSenderId(), out var p)) { p.Ready = ready; BroadcastLobby(); }
    }

    private void ApplyClass(long id, SoldierClass c, float armor)
    {
        if (!Players.TryGetValue(id, out var p)) return;
        p.Class = c;
        p.MaxArmor = armor;
        BroadcastLobby();
    }

    public void SetLobbyMission(int mission, Difficulty d)
    {
        LobbyMission = mission;
        LobbyDifficulty = d;
        if (IsHost) BroadcastLobby();
    }

    public void BroadcastLobby()
    {
        var arr = new Godot.Collections.Array();
        foreach (var p in Players.Values.OrderBy(p => p.Id))
            arr.Add(new Godot.Collections.Array { p.Id, p.Name, (int)p.Class, p.Ready, p.MaxArmor });
        if (IsSolo) { LobbyChanged?.Invoke(); return; }
        Rpc(MethodName.LobbySync, arr, LobbyMission, (int)LobbyDifficulty, SessionName);
        LobbyChanged?.Invoke();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void LobbySync(Godot.Collections.Array arr, int mission, int diff, string sessionName)
    {
        Players.Clear();
        foreach (var v in arr)
        {
            var a = v.AsGodotArray();
            var id = a[0].AsInt64();
            Players[id] = new PlayerInfo
            {
                Id = id, Name = a[1].AsString(), Class = (SoldierClass)a[2].AsInt32(), Ready = a[3].AsBool(), MaxArmor = a[4].AsSingle(),
            };
        }
        LobbyMission = mission;
        LobbyDifficulty = (Difficulty)diff;
        SessionName = sessionName;
        LobbyChanged?.Invoke();
    }

    public void HostStartMission()
    {
        if (!IsHost) return;
        var seed = (int)(GD.Randi() & 0x7fffffff);
        if (IsSolo) DoStartMission(LobbyMission, (int)LobbyDifficulty, seed);
        else Rpc(MethodName.DoStartMission, LobbyMission, (int)LobbyDifficulty, seed);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DoStartMission(int mission, int diff, int seed)
    {
        foreach (var p in Players.Values) p.Ready = false;
        MissionStarting?.Invoke(mission, (Difficulty)diff, seed);
    }

    public void HostReturnToLobby()
    {
        if (!IsHost) return;
        if (IsSolo) DoReturnToLobby();
        else Rpc(MethodName.DoReturnToLobby);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DoReturnToLobby() => ReturnToLobby?.Invoke();

    // ---------- Aides d'envoi ----------

    /// <summary>Envoie à l'hôte (ou exécute localement si l'on est l'hôte).</summary>
    private void ToHost(StringName method, params Variant[] args)
    {
        if (IsHost) Call(method, args);
        else RpcId(1, method, args);
    }

    /// <summary>Envoie à tous les pairs, soi compris.</summary>
    private void ToAll(StringName method, params Variant[] args)
    {
        if (IsSolo) Call(method, args);
        else Rpc(method, args);
    }

    /// <summary>Envoie aux autres pairs uniquement.</summary>
    private void ToOthers(StringName method, params Variant[] args)
    {
        if (IsSolo) return;
        foreach (var id in Players.Keys)
            if (id != MyId) RpcId(id, method, args);
    }

    private long Sender
    {
        get
        {
            var s = Multiplayer.GetRemoteSenderId();
            return s == 0 ? MyId : s;
        }
    }

    private static World W => World.Current;

    // ---------- Joueurs ----------

    public void SendPlayerState(Vector3 pos, float yaw, float pitch, int flags, float hp, int weapon, Vector3 vel)
    {
        if (IsSolo) return;
        foreach (var id in Players.Keys)
            if (id != MyId) RpcId(id, MethodName.PlayerState, pos, yaw, pitch, flags, hp, weapon, vel);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 1)]
    private void PlayerState(Vector3 pos, float yaw, float pitch, int flags, float hp, int weapon, Vector3 vel) =>
        W?.OnRemotePlayerState(Sender, pos, yaw, pitch, flags, hp, weapon, vel);

    public void SendShotFx(int kind, Vector3 from, Vector3 to) => ToOthers(MethodName.ShotFx, kind, from, to);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 2)]
    private void ShotFx(int kind, Vector3 from, Vector3 to) => W?.OnShotFx(Sender, kind, from, to);

    public void SendRevive(long target) => ToAll(MethodName.Revive, target);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Revive(long target) => W?.OnRevive(target, Sender);

    public void SendPlayerDowned(bool downed) => ToAll(MethodName.PlayerDowned, MyId, downed);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void PlayerDowned(long id, bool downed) => W?.OnPlayerDowned(id, downed);

    public void SendPing(Vector3 pos, int kind) => ToAll(MethodName.Ping, pos, kind);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Ping(Vector3 pos, int kind) => W?.OnPing(Sender, pos, kind);

    public void SendRally(Vector3 pos) => ToAll(MethodName.Rally, pos);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Rally(Vector3 pos) => W?.OnRally(Sender, pos);

    // ---------- Combat ----------

    public void SendDamageEnemy(int id, float dmg, Vector3 at, bool weak) => ToHost(MethodName.DamageEnemy, id, dmg, at, weak);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DamageEnemy(int id, float dmg, Vector3 at, bool weak)
    {
        if (IsHost) W?.HostDamageEnemy(id, dmg, at, Sender, weak);
    }

    public void SendExplosion(Vector3 pos, float radius, float dmg, int kind) => ToHost(MethodName.Explosion, pos, radius, dmg, kind);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Explosion(Vector3 pos, float radius, float dmg, int kind)
    {
        if (!IsHost) return;
        W?.HostExplosion(pos, radius, dmg, kind, Sender);
    }

    public void BroadcastExplosionFx(Vector3 pos, float radius, int kind) => ToAll(MethodName.ExplosionFx, pos, radius, kind);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ExplosionFx(Vector3 pos, float radius, int kind) => W?.OnExplosionFx(pos, radius, kind);

    public void SendAirstrike(Vector3 target, Vector3 dir, float dmg, int count, float radius) =>
        ToAll(MethodName.Airstrike, target, dir, dmg, count, radius);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Airstrike(Vector3 target, Vector3 dir, float dmg, int count, float radius) =>
        W?.OnAirstrike(Sender, target, dir, dmg, count, radius);

    public void SendDeploy(int kind, Vector3 pos, float yaw, float power, float radius, float duration) =>
        ToAll(MethodName.Deploy, kind, pos, yaw, power, radius, duration);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Deploy(int kind, Vector3 pos, float yaw, float power, float radius, float duration) =>
        W?.OnDeploy(Sender, kind, pos, yaw, power, radius, duration);

    // ---------- Ennemis (hôte → clients) ----------

    public void BroadcastEnemySpawn(int id, int type, Vector3 pos, float yaw, float hp) =>
        ToOthers(MethodName.EnemySpawn, id, type, pos, yaw, hp);

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void EnemySpawn(int id, int type, Vector3 pos, float yaw, float hp) => W?.OnEnemySpawn(id, (EnemyType)type, pos, yaw, hp);

    public void BroadcastEnemySnapshot(byte[] data) => ToOthers(MethodName.EnemySnapshot, data);

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 3)]
    private void EnemySnapshot(byte[] data) => W?.OnEnemySnapshot(data);

    public void BroadcastEnemyDeath(int id, Vector3 pos) => ToOthers(MethodName.EnemyDeath, id, pos);

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void EnemyDeath(int id, Vector3 pos) => W?.OnEnemyDeath(id, pos);

    /// <summary>Retrait silencieux d'un ennemi bloqué (ni butin, ni élimination comptée) : il réapparaît ailleurs.</summary>
    public void BroadcastEnemyDespawn(int id) => ToOthers(MethodName.EnemyDespawn, id);

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void EnemyDespawn(int id) => W?.OnEnemyDespawn(id);

    public void BroadcastEnemyAttack(int id, int kind, Vector3 target) => ToOthers(MethodName.EnemyAttack, id, kind, target);

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void EnemyAttack(int id, int kind, Vector3 target) => W?.OnEnemyAttack(id, kind, target);

    public void DamagePlayer(long peer, float dmg, Vector3 from)
    {
        if (peer == MyId) W?.OnLocalPlayerDamaged(dmg, from);
        else if (!IsSolo) RpcId(peer, MethodName.PlayerDamaged, dmg, from);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void PlayerDamaged(float dmg, Vector3 from) => W?.OnLocalPlayerDamaged(dmg, from);

    public void BroadcastEnemyHit(int id, Vector3 at) => ToOthers(MethodName.EnemyHitFx, id, at);

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 2)]
    private void EnemyHitFx(int id, Vector3 at) => W?.OnEnemyHitFx(id, at);

    // ---------- Véhicules ----------

    /// <summary>
    /// État d'un véhicule, diffusé par son conducteur. <paramref name="a"/>, <paramref name="b"/> et <paramref name="c"/>
    /// dépendent du type (char : tourelle et canon ; hélicoptère : régime du rotor, lacet et site de la tourelle de nez).
    /// <paramref name="ammo"/> : munitions totales des deux armes de bord (16 bits chacune).
    /// </summary>
    public void SendVehicleState(int id, Vector3 pos, Vector3 rot, float a, float b, float c, float hp, float speed, int ammo)
    {
        if (IsSolo) return;
        foreach (var pid in Players.Keys)
            if (pid != MyId) RpcId(pid, MethodName.VehicleState, id, pos, rot, a, b, c, hp, speed, ammo);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 6)]
    private void VehicleState(int id, Vector3 pos, Vector3 rot, float a, float b, float c, float hp, float speed, int ammo) =>
        W?.OnVehicleState(id, pos, rot, a, b, c, hp, speed, ammo);

    public void RequestVehicleSeat(int id, bool enter) => ToHost(MethodName.VehicleSeatRequest, id, enter);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void VehicleSeatRequest(int id, bool enter)
    {
        if (!IsHost || W == null || !W.Vehicles.TryGetValue(id, out var v) || v.Wrecked) return;
        var who = Sender;
        // Une seule place par joueur : pas de second véhicule tant qu'on en conduit un.
        if (enter && v.Driver == 0 && !W.Vehicles.Values.Any(o => o.Driver == who)) ToAll(MethodName.VehicleDriver, id, who);
        else if (!enter && v.Driver == who) ToAll(MethodName.VehicleDriver, id, 0L);
    }

    /// <summary>Hôte : libère les places tenues par un joueur parti (sinon le véhicule reste bloqué).</summary>
    public void ReleaseVehicleSeats(long who)
    {
        if (!IsHost || W == null) return;
        foreach (var (id, v) in W.Vehicles)
            if (v.Driver == who) ToAll(MethodName.VehicleDriver, id, 0L);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void VehicleDriver(int id, long driver) => W?.OnVehicleDriver(id, driver);

    public void SendVehicleWrecked(int id) => ToAll(MethodName.VehicleWrecked, id);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void VehicleWrecked(int id) => W?.OnVehicleWrecked(id);

    // ---------- Alliés PNJ ----------

    public void BroadcastAllySpawn(int id, Vector3 pos, Vector3 formation) => ToOthers(MethodName.AllySpawn, id, pos, formation);

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void AllySpawn(int id, Vector3 pos, Vector3 formation) => W?.Allies.Spawn(id, pos, formation);

    public void BroadcastAllySnapshot(byte[] data) => ToOthers(MethodName.AllySnapshot, data);

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 5)]
    private void AllySnapshot(byte[] data) => W?.Allies.ApplySnapshot(data);

    public void BroadcastAllyShot(int id, Vector3 to) => ToOthers(MethodName.AllyShot, id, to);

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 2)]
    private void AllyShot(int id, Vector3 to) => W?.Allies.OnShot(id, to);

    public void BroadcastAllyDeath(int id) => ToAll(MethodName.AllyDeath, id);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void AllyDeath(int id) => W?.Allies.OnDeath(id);

    // ---------- Caisses ----------

    public void BroadcastCrateSpawn(int id, int type, Vector3 pos) => ToOthers(MethodName.CrateSpawn, id, type, pos);

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void CrateSpawn(int id, int type, Vector3 pos) => W?.OnCrateSpawn(id, (CrateType)type, pos);

    public void RequestPickup(int id) => ToHost(MethodName.PickupRequest, id);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void PickupRequest(int id)
    {
        if (IsHost) W?.HostPickup(id, Sender);
    }

    public void BroadcastCratePicked(int id, long by, int type, Vector3 pos) => ToAll(MethodName.CratePicked, id, by, type, pos);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void CratePicked(int id, long by, int type, Vector3 pos) => W?.OnCratePicked(id, by, (CrateType)type, pos);

    public void BroadcastCrateGone(int id) => ToOthers(MethodName.CrateGone, id);

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void CrateGone(int id) => W?.OnCrateGone(id);

    // ---------- Décor ----------

    public void SendBuildingDamage(int id, float dmg, Vector3 at) => ToHost(MethodName.BuildingDamage, id, dmg, at);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void BuildingDamage(int id, float dmg, Vector3 at)
    {
        if (IsHost) W?.HostBuildingDamage(id, dmg, at);
    }

    public void BroadcastBuildingDestroyed(int id) => ToAll(MethodName.BuildingDestroyed, id);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void BuildingDestroyed(int id) => W?.OnBuildingDestroyed(id);

    /// <summary>Palier de dégâts d'un immeuble (1 à 3) et point d'impact sur la façade.</summary>
    public void BroadcastBuildingStage(int id, int stage, Vector3 at) => ToAll(MethodName.BuildingStage, id, stage, at);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void BuildingStage(int id, int stage, Vector3 at) => W?.OnBuildingStage(id, stage, at);

    // Accessoires destructibles : identifiants dans l'ordre de génération (voir Destructibles).

    public void SendPropDamage(int id, float dmg, int type, Vector3 from) => ToHost(MethodName.PropDamage, id, dmg, type, from);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void PropDamage(int id, float dmg, int type, Vector3 from)
    {
        if (IsHost) W?.Decor?.HostDamage(id, dmg, (PropHit)type, from, Sender);
    }

    public void SendPropCrush(int id, int strength, Vector3 from) => ToHost(MethodName.PropCrush, id, strength, from);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void PropCrush(int id, int strength, Vector3 from)
    {
        if (IsHost) W?.Decor?.HostCrush(id, strength, from, Sender);
    }

    /// <summary>Événement d'accessoire : code (8 bits) | direction (8 bits) &lt;&lt; 8 | amplitude (8 bits) &lt;&lt; 16.</summary>
    public void BroadcastPropEvent(int id, int packed) => ToAll(MethodName.PropEvent, id, packed);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void PropEvent(int id, int packed) => W?.Decor?.OnEvent(id, packed);

    // ---------- Mission ----------

    public void BroadcastMessage(string text, int kind) => ToAll(MethodName.MissionMessage, text, kind);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void MissionMessage(string text, int kind) => W?.OnMissionMessage(text, kind);

    public void BroadcastObjective(string text, int remaining, float bossHp, string bossName) =>
        ToAll(MethodName.Objective, text, remaining, bossHp, bossName);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 4)]
    private void Objective(string text, int remaining, float bossHp, string bossName) => W?.OnObjective(text, remaining, bossHp, bossName);

    public void BroadcastScores(string data) => ToAll(MethodName.Scores, data);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void Scores(string data) => W?.OnScores(data);

    public void BroadcastMissionEnd(bool success, float elapsed) => ToAll(MethodName.MissionEnd, success, elapsed);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void MissionEnd(bool success, float elapsed) => W?.OnMissionEnd(success, elapsed);

    public void SendLeftMission() => ToHost(MethodName.LeftMission);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void LeftMission()
    {
        if (IsHost) W?.OnPeerLeft(Sender);
    }
}
