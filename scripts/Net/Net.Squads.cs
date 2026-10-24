using Godot;

namespace Rempart;

/// <summary>Escouades PNJ : ralliement demandé à l'hôte, état (chef, poste, membres) diffusé à tous.</summary>
public partial class Net
{
    public void RequestRally(int squad) => ToHost(MethodName.RallyRequest, squad);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RallyRequest(int squad)
    {
        if (!IsHost || W == null) return;
        W.Allies.HostRally(squad, Sender);
    }

    public void BroadcastSquadState(int squad, long leader, Vector3 post, int[] members) => ToAll(MethodName.SquadState, squad, leader, post, members);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SquadState(int squad, long leader, Vector3 post, int[] members)
    {
        if (W == null) return;
        var was = W.Allies.SquadLeader.TryGetValue(squad, out var l) ? l : 0;
        W.Allies.ApplySquad(squad, leader, post, members);
        if (leader == MyId && was != MyId) Sfx.Ui("radio", -4f);
    }
}
