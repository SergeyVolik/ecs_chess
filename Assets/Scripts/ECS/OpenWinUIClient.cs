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
            switch (rpc.endGameData.endReason)
            {
                case EndReason.Win:
                    reasonText = "Checkmate!";
                    break;
                case EndReason.OponentSurrendreed:
                    reasonText = $"Oponent Surrendered!";
                    break;
                case EndReason.Draw:
                    reasonText = $"Draw!";
                    break;
                default:
                    break;
            }
            string winnerText;

            if (rpc.endGameData.isDraw)
            {
                winnerText = "No Winner";
            }
            else if (rpc.endGameData.isWhiteWin)
            {
                winnerText = "White Won!";
            }
            else {
                winnerText = "Black Won!";

            }
           
            UIPages.Instance.winUi.ShowWin(winnerText, reasonText);
        }
        ecb.Playback(EntityManager);
    }
}
