using DG.Tweening;
using Unity.Entities;
using UnityEngine;

public class ShowScaleAuthoring : MonoBehaviour
{
    public float duration;
    public Ease ease;
    public float scale = 1f;

    public class Baker : Baker<ShowScaleAuthoring>
    {
        public override void Bake(ShowScaleAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);


            AddComponent<ShowScaleTweenC>(entity, new ShowScaleTweenC
            {
                ease = authoring.ease,
                duration = authoring.duration,
                scale = authoring.scale
            });
        }
    }
}