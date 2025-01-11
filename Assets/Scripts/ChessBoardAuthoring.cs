using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ChessBoardAuthoring : MonoBehaviour
{
    public Vector3 spawnGridOffset;
    public Vector3 offsetBetweenSockets;
    public GameObject socketPrefab;

    public class Baker : Baker<ChessBoardAuthoring>
    {
        public override void Bake(ChessBoardAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ChessBoard>(entity, new ChessBoard
            {
                offsetBetweenSockets = authoring.offsetBetweenSockets,
                spawnGridOffset = authoring.spawnGridOffset,
                socketPrefab = GetEntity(authoring.socketPrefab, TransformUsageFlags.Dynamic)
            });
        }
    }
}

public struct ChessBoard : IComponentData
{
    public float3 spawnGridOffset;
    public float3 offsetBetweenSockets;
    public Entity socketPrefab;
}

[InternalBufferCapacity(64)]
public struct ChessBoardSockets : IBufferElementData
{
    public Entity socketEntity;
}
