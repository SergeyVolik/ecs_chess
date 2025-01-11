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

        foreach (var (bC, e) in SystemAPI.Query<ChessBoard>().WithEntityAccess().WithNone<ChessBoardSockets>())
        {
            var sockets = buffer.AddBuffer<ChessBoardSockets>(e);

            for (int x = 0; x < 8; x++)
            {
                for (int z = 0; z < 8; z++)
                {
                    var socketInstance = buffer.Instantiate(bC.socketPrefab);
                    buffer.SetComponent<LocalTransform>(socketInstance, LocalTransform.FromPosition(bC.spawnGridOffset + new float3(x * bC.offsetBetweenSockets.x, 0, z * bC.offsetBetweenSockets.z)));
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
