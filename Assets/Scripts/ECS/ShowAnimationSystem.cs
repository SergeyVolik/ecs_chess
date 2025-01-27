using DG.Tweening;
using DG.Tweening.Core;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public struct ShowScaleTweenC : IComponentData
{
    public float targetScale;
    public float t;
    public float duration;
    public Ease ease;
    internal float scale;
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class ShowAnimationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach (var (data, ltw, e) in SystemAPI.Query<RefRW<ShowScaleTweenC>, RefRW<LocalTransform>>().WithEntityAccess())
        {

            ltw.ValueRW.Scale = DOVirtual.EasedValue(0, data.ValueRW.scale, data.ValueRW.t/ data.ValueRW.duration, data.ValueRW.ease);

            data.ValueRW.t += SystemAPI.Time.DeltaTime;

            var percent = data.ValueRW.t / data.ValueRW.duration;
            if (percent >= 1)
            {
                ecb.RemoveComponent<ShowScaleTweenC>(e);
            }
        }

        ecb.Playback(EntityManager);
    }
}
