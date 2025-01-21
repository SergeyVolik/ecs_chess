using Unity.Entities;
using Unity.NetCode;

public struct PlaySfxRpc : IRpcCommand
{
    public SfxType Type;
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class PlaySfxClientSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (sfx, e) in SystemAPI.Query<PlaySfxRpc>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
        {
            AudioManager.Instance.PlaySfx(sfx.Type);
            ecb.DestroyEntity(e);
        }

        ecb.Playback(EntityManager);
    }
}
