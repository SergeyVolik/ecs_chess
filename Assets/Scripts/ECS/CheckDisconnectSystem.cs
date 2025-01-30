using DG.Tweening;
using Unity.Entities;
using Unity.NetCode;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class CheckDisconnectSystem : SystemBase
{
    float t;
    bool hadConnection = false;
    protected override void OnUpdate()
    {
        var hasConnection = !SystemAPI.QueryBuilder().WithAll<NetworkId, NetworkStreamInGame>().Build().IsEmpty;

        if (hadConnection == false && hasConnection == true)
            hadConnection = true;

        if (!hadConnection)
            return;
       
        t += SystemAPI.Time.DeltaTime;

        if (hasConnection)
        {
            t = 0;
        }

        if (t > 5)
        {
            t = 0;
            Diconnect();
        }
        
    }

    private static void Diconnect()
    {
        DOVirtual.DelayedCall(0.1f, () =>
        {
            ConnectionManager.Instance.Disconnect();
            MainMenuUI.Instance.Show();
        });
    }
}
