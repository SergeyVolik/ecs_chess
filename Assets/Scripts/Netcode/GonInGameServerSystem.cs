using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

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
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(state.EntityManager);
    }
}
