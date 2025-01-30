using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class ClientSetupPlayerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, command, entity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<SetupPlayerRPC>>().WithEntityAccess())
        {
            if (command.ValueRO.isWhite)
            {
                ecb.AddComponent<ChessPlayerC>(request.ValueRO.SourceConnection, new ChessPlayerC { 
                     isWhite = true
                });
            }
            else
            {
                ecb.AddComponent<ChessPlayerC>(request.ValueRO.SourceConnection, new ChessPlayerC
                {
                    isWhite = false
                });
            }

            ChatWindow.Instance.ClearChat();
            ChatWindow.Instance.Show(false);

           CameraController.Instance.SetupPlayerCamera(command.ValueRO.isWhite);           
            Debug.Log("[Client] setup camera");
            ecb.DestroyEntity(entity);
        }

        ecb.Playback(EntityManager);
    }
}
