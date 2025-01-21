using Unity.Entities;
using Unity.NetCode;

public struct ShakeCameraRpc : IRpcCommand { }

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class CameraShakeClientSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach (var (rpc, e) in SystemAPI.Query<ShakeCameraRpc>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
        {
            CameraController.Instance.ShakeCamera();
            ecb.DestroyEntity(e);
        }
        ecb.Playback(EntityManager);
    }  
}
