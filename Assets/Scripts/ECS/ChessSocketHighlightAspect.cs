using System.Diagnostics;
using Unity.Entities;
using Unity.Transforms;

readonly partial struct ChessSocketHighlightAspect : IAspect
{
    private readonly RefRO<LocalTransform> transform;

    public readonly Entity selfE;
    private readonly RefRO<ChessSocketHighlightC> prefabsC;
    [Optional]
    private readonly RefRW<ChessSocketHighlightInstanceC> instanceC;
    [Optional]
    private readonly RefRW<ChessSocketPrevMoveC> instance1C;
    public void DestoryHighlight(EntityCommandBuffer ecb)
    {
        if (instanceC.IsValid)
        {
            ecb.DestroyEntity(instanceC.ValueRO.entity);
            ecb.RemoveComponent<ChessSocketHighlightInstanceC>(selfE);
        }
    }

    public void DestoryPrevMove(EntityCommandBuffer ecb)
    {
        if (instance1C.IsValid)
        {
            ecb.DestroyEntity(instance1C.ValueRO.entity);
            ecb.RemoveComponent<ChessSocketPrevMoveC>(selfE);
        }
    }

    public void ShowPrevMove(EntityCommandBuffer ecb)
    {
        DestoryPrevMove(ecb);

        var e = ecb.Instantiate(prefabsC.ValueRO.highlightSelectedPrefab);
        ecb.AddComponent<ChessSocketPrevMoveC>(selfE, new ChessSocketPrevMoveC
        {
            entity = e
        });
        ecb.SetComponent<LocalTransform>(e, transform.ValueRO);
    }

    public void ShowMovePos(EntityCommandBuffer ecb)
    {
        ShowObject(ecb, prefabsC.ValueRO.highlightMovePosPrefab);
    }

    public void ShowObject(EntityCommandBuffer ecb, Entity prefab)
    {
        DestoryHighlight(ecb);

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