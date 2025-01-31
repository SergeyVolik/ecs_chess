using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public struct SurrenderRPC : IRpcCommand
{
    
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class SurrenderServerSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<SurrenderRPC>();
    }

    protected override void OnUpdate()
    {
        var query = SystemAPI.QueryBuilder().WithAll<SurrenderRPC, ReceiveRpcCommandRequest>().Build();

        if (!query.IsEmpty)
        {
            var sourceE = query.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp)[0].SourceConnection;
            EntityManager.DestroyEntity(query);
            Debug.Log("[Server] Surrender");
            if (SystemAPI.HasComponent<ChessPlayerC>(sourceE))
            {
                var playerData = SystemAPI.GetComponent<ChessPlayerC>(sourceE);
                var colorText = playerData.isWhite ? "White" : "Black";
                var endGameE = EntityManager.CreateEntity();
                EntityManager.AddComponentData<ExecuteEndGameC>(endGameE, new ExecuteEndGameC
                {
                    isDraw = false,
                    isWhiteWin = !playerData.isWhite,
                     winReason = WinReason.OponentSurrendreed 
                });
            }
        }
    }
}
