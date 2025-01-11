using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

//[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Default)]
public partial struct SpawnSocketsSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer buffer = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        if (!SystemAPI.HasSingleton<ChessGameStartedT>())
        {
            var e1 = buffer.CreateEntity();
            buffer.AddComponent<ChessGameStartedT>(e1);
        }

        foreach (var (bC, e) in SystemAPI.Query<RefRO<ChessBoardC>>().WithEntityAccess().WithNone<ChessBoardSockets>())
        {
            var sockets = buffer.AddBuffer<ChessBoardSockets>(e);

            for (int z = 0; z < 8; z++)
            {
                for (int x = 0; x < 8; x++)
                {
                    var socketInstance = buffer.Instantiate(bC.ValueRO.socketPrefab);
                    float3 spawnPos = bC.ValueRO.spawnGridOffset + new float3(x * bC.ValueRO.offsetBetweenSockets.x, 0, z * bC.ValueRO.offsetBetweenSockets.z);
                    buffer.SetComponent<LocalTransform>(socketInstance, LocalTransform.FromPosition(spawnPos));
                    buffer.AddComponent<Parent>(socketInstance, new Parent
                    {
                        Value = e
                    });

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
