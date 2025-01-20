using Unity.Entities;
using UnityEngine;

public class ChessBoardInstanceAuthoring : MonoBehaviour
{
    public class Baker : Baker<ChessBoardInstanceAuthoring>
    {
        public override void Bake(ChessBoardInstanceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ChessBoardInstanceT>(entity);
            AddComponent<ChessBoardTurnC>(entity);
            AddBuffer<ChessBoardBlackPiecesBuffer>(entity);
            AddBuffer<ChessBoardWhitePiecesBuffer>(entity);
        }
    }
}