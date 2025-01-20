using TMPro;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public struct ChessBoardUIPoints : IComponentData
{
    public Entity whiteTurnUiPoint;
    public Entity blackTurnUiPoint;
}

public class ChessBoardInstanceAuthoring : MonoBehaviour
{
    public Transform whiteTurnUiPoint;
    public Transform blackTurnUiPoint;

    public class Baker : Baker<ChessBoardInstanceAuthoring>
    {

        public override void Bake(ChessBoardInstanceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ChessBoardInstanceT>(entity);
            AddComponent<ChessBoardTurnC>(entity);
            AddBuffer<ChessBoardBlackPiecesBuffer>(entity);
            AddBuffer<ChessBoardWhitePiecesBuffer>(entity);

            AddComponent<ChessBoardUIPoints>(entity, new ChessBoardUIPoints
            {
                blackTurnUiPoint = GetEntity(authoring.blackTurnUiPoint, TransformUsageFlags.Dynamic),
                whiteTurnUiPoint = GetEntity(authoring.whiteTurnUiPoint, TransformUsageFlags.Dynamic),
            });
        }
    }
}