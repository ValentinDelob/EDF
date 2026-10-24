using Godot;

namespace Rempart;

/// <summary>
/// Appel de véhicule de l'Opérateur d'appui : demande à l'hôte (type + point visé), l'hôte choisit le point de largage,
/// l'identifiant et le véhicule éventuellement remplacé, puis diffuse la livraison à tous les pairs.
/// Les crédits d'appel restent locaux au joueur : ils ne sont débités qu'à la confirmation.
/// </summary>
public partial class Net
{
    public void RequestVehicleCall(int type, Vector3 at) => ToHost(MethodName.VehicleCallRequest, type, at);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void VehicleCallRequest(int type, Vector3 at)
    {
        if (!IsHost || W == null) return;
        var who = Sender;
        if (!W.HostVehicleCall(who, type, at))
        {
            if (who == MyId) W.OnVehicleCallDenied();
            else RpcId(who, MethodName.VehicleCallDenied);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void VehicleCallDenied() => W?.OnVehicleCallDenied();

    /// <summary>Hôte : livraison d'un véhicule appelé (identifiant, type, point de pose, cap, demandeur, véhicule remplacé ou 0).</summary>
    public void BroadcastVehicleCalled(int id, int type, Vector3 pos, float yaw, long owner, int replaced) =>
        ToAll(MethodName.VehicleCalled, id, type, pos, yaw, owner, replaced);

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void VehicleCalled(int id, int type, Vector3 pos, float yaw, long owner, int replaced) =>
        W?.OnVehicleCalled(id, type, pos, yaw, owner, replaced);
}
