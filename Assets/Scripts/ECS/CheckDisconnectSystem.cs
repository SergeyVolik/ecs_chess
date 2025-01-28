using DG.Tweening;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class CheckDisconnectSystem : SystemBase
{
    float t;
    protected override void OnUpdate()
    {

        var hasConnection = !SystemAPI.QueryBuilder().WithAll<NetworkId, NetworkStreamInGame>().Build().IsEmpty;

        t += SystemAPI.Time.DeltaTime;

        if (hasConnection)
        {
            t = 0;
        }

        if (t > 5)
        {
            t = 0;
            DOVirtual.DelayedCall(0.1f, () =>
            {
                ConnectionManager.Instance.Disconnect();
                MenuUI.Instance.Show();
            });       
        }
    }
}
