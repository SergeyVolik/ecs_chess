using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public struct InitializedClient : IComponentData
{

}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class ServerSystem : SystemBase
{
    ComponentLookup<NetworkId> m_Clients;

    protected override void OnCreate()
    {
        m_Clients = SystemAPI.GetComponentLookup<NetworkId>(true);
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (request, command, entity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<MessageRPC>>().WithEntityAccess())
        {
            Debug.Log($"{command.ValueRO.message} from client {request.ValueRO.SourceConnection.Index}");
            ecb.DestroyEntity(entity);
        }

        foreach (var (id, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithNone<InitializedClient>().WithEntityAccess())
        {
            ecb.AddComponent<InitializedClient>(entity);
            Debug.Log($"Client Connected id:{id.ValueRO.Value}");
        }
        ecb.Playback(EntityManager);
    }

}
