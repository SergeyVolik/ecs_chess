using Unity.Entities;
using UnityEngine;

public class ChessPieceAuthoring : MonoBehaviour
{
    public class Baker : Baker<ChessSocketAuthoring>
    {
        public override void Bake(ChessSocketAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ChessPiece>(entity, new ChessPiece
            {

            });
        }
    }
}

public struct ChessPiece : IComponentData
{
    public int x;
    public int y;
}