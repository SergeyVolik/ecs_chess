using Unity.Entities;
using Unity.Transforms;

readonly partial struct ChessSocketHighlightAspect : IAspect
{
    private readonly RefRO<LocalTransform> transform;

    public readonly Entity selfE;
    private readonly RefRO<ChessSocketHighlightC> prefabsC;
    [Optional]
    private readonly RefRW<ChessSocketHighlightInstanceC> instanceC;

    public void Destory(EntityCommandBuffer ecb)
    {
        if (instanceC.IsValid)
        {
            ecb.DestroyEntity(instanceC.ValueRO.entity);
            ecb.RemoveComponent<ChessSocketHighlightInstanceC>(selfE);
        }
    }

    public void ShowMovePos(EntityCommandBuffer ecb)
    {
        ShowObject(ecb, prefabsC.ValueRO.highlightMovePosPrefab);
    }

    public void ShowObject(EntityCommandBuffer ecb, Entity prefab)
    {
        var e = ecb.Instantiate(prefab);
        ecb.AddComponent<ChessSocketHighlightInstanceC>(selfE, new ChessSocketHighlightInstanceC
        {
            entity = e
        });
        ecb.SetComponent<LocalTransform>(e, transform.ValueRO);
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