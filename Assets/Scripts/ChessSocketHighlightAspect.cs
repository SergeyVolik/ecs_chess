using Unity.Entities;
using Unity.Transforms;

readonly partial struct ChessSocketHighlightAspect : IAspect
{
    private readonly RefRO<LocalTransform> transform;
    private readonly RefRO<LocalToWorld> ltw;

    public readonly Entity selfE;
    private readonly RefRO<ChessSocketHighlightC> prefabsC;
    private readonly RefRW<ChessSocketHighlightInstanceC> instanceC;

    public void Destory(EntityCommandBuffer ecb)
    {
        ecb.DestroyEntity(instanceC.ValueRO.entity);
    }

    public void ShowMovePos(EntityCommandBuffer ecb)
    {
        ShowObject(ecb, prefabsC.ValueRO.highlightMovePosPrefab);
    }

    public void ShowObject(EntityCommandBuffer ecb, Entity prefab)
    {
        var e = ecb.Instantiate(prefab);
        ecb.SetComponent<ChessSocketHighlightInstanceC>(selfE, new ChessSocketHighlightInstanceC
        {
            entity = e
        });
        ecb.SetComponent<LocalTransform>(e, transform.ValueRO);
        ecb.SetComponent<LocalToWorld>(e, ltw.ValueRO);
    }

    public void ShowEnemy(EntityCommandBuffer ecb)
    {
        ShowObject(ecb, prefabsC.ValueRO.highlightEnemyPrefab);
    }

    public void ShowSelected(EntityCommandBuffer ecb)
    {
        ShowObject(ecb, prefabsC.ValueRO.highlightSelectedPrefab);
    }
}