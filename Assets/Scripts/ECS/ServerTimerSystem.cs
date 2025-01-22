using Unity.Entities;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class ServerTimerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var item in SystemAPI.Query<RefRW<ChessBoardTimerC>>())
        {
            item.ValueRW.duration += SystemAPI.Time.DeltaTime;
        }
    }
}
