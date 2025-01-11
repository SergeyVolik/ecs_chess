using Unity.Entities;
using UnityEngine;

public class ChessSocketAuthoring : MonoBehaviour
{
    public class Baker : Baker<ChessSocketAuthoring>
    {
        public override void Bake(ChessSocketAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ChessSocket>(entity, new ChessSocket
            {

            });
        }
    }
}

public struct ChessSocket : IComponentData
{

}