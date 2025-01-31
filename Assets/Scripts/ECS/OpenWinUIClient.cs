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
            string reasonText = "";
            switch (rpc.winReason)
            {
                case WinReason.Win:
                    break;
                case WinReason.OponentSurrendreed:
                    reasonText = $"Oponent Surrendered!";
                    break;
                default:
                    break;
            }

            string winnerText = rpc.isWhiteWin ? "White" : "Black";        
            UIPages.Instance.winUi.ShowWin($"{winnerText} Won!", reasonText);
        }
        ecb.Playback(EntityManager);
    }
}
