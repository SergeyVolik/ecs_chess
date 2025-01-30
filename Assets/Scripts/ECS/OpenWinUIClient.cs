using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class OpenWinUIClientSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach (var (rpc, entity) in SystemAPI.Query<EndGameRPC>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
        {
            ecb.DestroyEntity(entity);
            UIPages.Instance.winUi.ShowWin(rpc.isWhiteWin ? "White Win!" : "Black Win!");
        }
        ecb.Playback(EntityManager);
    }
}
