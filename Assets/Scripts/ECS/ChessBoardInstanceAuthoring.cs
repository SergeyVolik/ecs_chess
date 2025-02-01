using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

public struct ChessBoardUIPoints : IComponentData
{
    public Entity whiteTurnUiPoint;
    public Entity blackTurnUiPoint;
}

public struct ChessBoardTimerC : IComponentData
{
    [GhostField] public float duration;
}

public struct ChessBoardBoundsC : IComponentData
{
    public Bounds bounds;
}

public struct KilledPieces : IBufferElementData
{
    [GhostField] public bool isWhite;
    [GhostField] public ChessType chessType;
}

public struct AddKilledPiecesRPC : IRpcCommand
{
    public ChessPieceC data;
}

public class ChessBoardInstanceAuthoring : MonoBehaviour
{
    public Transform whiteTurnUiPoint;
    public Transform blackTurnUiPoint;

    public Bounds bounds;

    public ChessBoardConfigurationSO config;
    public class Baker : Baker<ChessBoardInstanceAuthoring>
    {
        public override void Bake(ChessBoardInstanceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ChessBoardInstanceC>(entity);
            AddComponent<ChessBoardTimerC>(entity);
            AddComponentObject<ChessBoardInstanceSpawnConfigC>(entity, new ChessBoardInstanceSpawnConfigC
            {
                config = authoring.config
            });

            AddComponent<ChessBoardTurnC>(entity, new ChessBoardTurnC
            {
                isWhite = true
            });

            AddBuffer<KilledPieces>(entity);
            AddBuffer<ChessBoardBlackPiecesBuffer>(entity);
            AddBuffer<ChessBoardWhitePiecesBuffer>(entity);
            AddBuffer<ChessBoardAllPiecesMeshes>(entity);
            AddBuffer<ChessBoardAllPiecesData>(entity);


            AddComponent<ChessBoardUIPoints>(entity, new ChessBoardUIPoints
            {
                blackTurnUiPoint = GetEntity(authoring.blackTurnUiPoint, TransformUsageFlags.Dynamic),
                whiteTurnUiPoint = GetEntity(authoring.whiteTurnUiPoint, TransformUsageFlags.Dynamic),
            });
            AddComponent<ChessBoardBoundsC>(entity, new ChessBoardBoundsC { 
                 bounds = authoring.bounds
            });
        }
    }
}