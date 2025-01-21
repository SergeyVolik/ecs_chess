using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class ClientSetupPlayerSystem : SystemBase
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
                     isWhite = true
                });
            }
            else
            {
                cameraLtw = SystemAPI.GetComponent<LocalTransform>(boardData.blackCameraPos);
                ecb.AddComponent<ChessPlayerC>(request.ValueRO.SourceConnection, new ChessPlayerC
                {
                    isWhite = false
                });
            }

            var camera = CameraController.Instance.GetCameraTarget();
            camera.position = cameraLtw.Position;
            camera.rotation = cameraLtw.Rotation;
            Debug.Log("[Client] setup camera");
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(EntityManager);
    }
}
