using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class ClientSetupBoardSystem : SystemBase
{
    protected override void OnCreate() { }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, command, entity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<SetupPlayerRPC>>().WithEntityAccess())
        {
            var boardData = SystemAPI.GetSingleton<ChessBoardPersistentC>();

            LocalTransform cameraLtw;
            if (command.ValueRO.isWhite)
            {
                cameraLtw = SystemAPI.GetComponent<LocalTransform>(boardData.whiteCameraPos);
                ecb.AddComponent<ChessPlayerC>(request.ValueRO.SourceConnection, new ChessPlayerC { 
                     white = true
                });
            }
            else
            {
                cameraLtw = SystemAPI.GetComponent<LocalTransform>(boardData.blackCameraPos);
                ecb.AddComponent<ChessPlayerC>(request.ValueRO.SourceConnection, new ChessPlayerC
                {
                    white = false
                });
            }

            var camera = Camera.main;
            camera.transform.position = cameraLtw.Position;
            camera.transform.rotation = cameraLtw.Rotation;
            Debug.Log("[Client] setup camera");
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(EntityManager);
    }
}
