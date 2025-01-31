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

public struct ChessPlayerC : IComponentData
{
    public bool isWhite;
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct GoInGameServerSystem : ISystem
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

            bool hasWhite = false;
            bool hasBlack = false;

            
            foreach (var item in SystemAPI.Query<ChessPlayerC>())
            {
                if (item.isWhite)
                {
                    hasWhite = true;
                }
                else
                {
                    hasBlack = true;
                }
            }
          
            if (!hasWhite)
            {
                ecb.AddComponent<SetupPlayerRPC>(sendReq, new SetupPlayerRPC
                {
                    isWhite = true
                });
                ecb.AddComponent<ChessPlayerC>(request.ValueRO.SourceConnection, new ChessPlayerC { 
                     isWhite = true
                });

                ChatWindow.Instance.RequestText("[Sys] white player connected", ecb);
                Debug.Log($"[Server] go in game as white");

            }
            else if(!hasBlack)
            {
                ecb.AddComponent<SetupPlayerRPC>(sendReq, new SetupPlayerRPC
                {
                    isWhite = false
                });
                ecb.AddComponent<ChessPlayerC>(request.ValueRO.SourceConnection, new ChessPlayerC
                {
                    isWhite = false
                });

                ChatWindow.Instance.RequestText("[Sys] black player connected", ecb);
                Debug.Log($"[Server] go in game as black");
            }
            else 
            {
                ChatWindow.Instance.RequestText("[Sys] spectator connected", ecb);
            }

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
    }
}
