using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public struct SetupPlayerRPC : IRpcCommand
{
    public bool isWhite;
}

public struct WhitePlayer : IComponentData
{

}

public struct BlackPlayer : IComponentData
{

}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct GonInGameServerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        var builder = new EntityQueryBuilder(Allocator.Temp);
        builder.WithAll<ReceiveRpcCommandRequest, GoInGameCommand>();
        state.RequireForUpdate(state.GetEntityQuery(builder));
    }

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, command, entity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<GoInGameCommand>>().WithEntityAccess())
        {
            ecb.AddComponent<NetworkStreamInGame>(request.ValueRO.SourceConnection);
        
            var sendReq = ecb.CreateEntity();
            ecb.AddComponent<SendRpcCommandRequest>(sendReq, new SendRpcCommandRequest
            {
                TargetConnection = request.ValueRO.SourceConnection
            });

            bool hasWhite = SystemAPI.HasSingleton<WhitePlayer>();
            bool hasBlack = SystemAPI.HasSingleton<BlackPlayer>();

            if (!hasWhite)
            {
                ecb.AddComponent<SetupPlayerRPC>(sendReq, new SetupPlayerRPC
                {
                    isWhite = true
                });
                ecb.AddComponent<WhitePlayer>(request.ValueRO.SourceConnection);
               
            }
            else if(!hasBlack)
            {
                ecb.AddComponent<SetupPlayerRPC>(sendReq, new SetupPlayerRPC
                {
                    isWhite = false
                });
                ecb.AddComponent<BlackPlayer>(request.ValueRO.SourceConnection);
            }

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
    }
}
