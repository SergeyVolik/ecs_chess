using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public struct ExecuteEndGameC : IComponentData
{
    public bool isDraw;
    public bool isWhiteWin;
    public WinReason winReason;
}

public struct GameEndedC : IComponentData
{
    public ExecuteEndGameC endGaemData;
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class EndGameServerSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<ExecuteEndGameC>();
    }

    protected override void OnUpdate()
    {
        var query = SystemAPI.QueryBuilder().WithAll<ExecuteEndGameC>().Build();

        if (!query.IsEmpty)
        {
            var endGame = query.ToComponentDataArray<ExecuteEndGameC>(Allocator.Temp)[0];
            EntityManager.DestroyEntity(query);
            var endGameE = EntityManager.CreateEntity();
            EntityManager.AddComponentData<GameEndedC>(endGameE, new GameEndedC
            {
                endGaemData = endGame
            });

            var request = EntityManager.CreateEntity();
            EntityManager.AddComponent<SendRpcCommandRequest>(request);
            EntityManager.AddComponentData<EndGameRPC>(request, new EndGameRPC
            {
                isWhiteWin = endGame.isWhiteWin,
                winReason = endGame.winReason
            });

            Debug.Log($"[Server] winner white:{endGame.isWhiteWin}");
        }
    }
}
