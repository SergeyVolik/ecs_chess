using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct SpawnSocketsSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {

    }

    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer buffer = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (bC, e) in SystemAPI.Query<RefRO<ChessBoardC>>().WithEntityAccess().WithNone<ChessBoardSockets>())
        {
            var sockets = buffer.AddBuffer<ChessBoardSockets>(e);
            buffer.AddComponent<StartGameT>(e);
            for (int z = 0; z < 8; z++)
            {
                for (int x = 0; x < 8; x++)
                {
                    var socketInstance = buffer.Instantiate(bC.ValueRO.socketPrefab);
                    float3 spawnPos = bC.ValueRO.spawnGridOffset + new float3(x * bC.ValueRO.offsetBetweenSockets.x, 0, z * bC.ValueRO.offsetBetweenSockets.z);
                    buffer.SetComponent<LocalTransform>(socketInstance, LocalTransform.FromPosition(spawnPos));
                    sockets.Add(new ChessBoardSockets
                    {
                        socketEntity = socketInstance,
                    });
                }
            }
        }

        buffer.Playback(state.EntityManager);
    }
}
