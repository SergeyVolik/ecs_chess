using Unity.Entities;
using UnityEditor.SceneManagement;
using UnityEngine;


public class ChessSocketAuthoring : MonoBehaviour
{
    public GameObject highlightSelected;
    public GameObject highlightMovePos;
    public GameObject highlightEnemy;

    public class Baker : Baker<ChessSocketAuthoring>
    {
        public override void Bake(ChessSocketAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ChessSocketC>(entity, new ChessSocketC
            {

            });
            AddComponent<ChessSocketPieceLinkC>(entity);
            AddComponent<ChessSocketHighlightC>(entity, new ChessSocketHighlightC
            {
                highlightEnemyPrefab = GetEntity(authoring.highlightEnemy, TransformUsageFlags.Dynamic),
                highlightSelectedPrefab = GetEntity(authoring.highlightSelected, TransformUsageFlags.Dynamic),
                highlightMovePosPrefab = GetEntity(authoring.highlightMovePos, TransformUsageFlags.Dynamic),
            });
        }
    }
}

readonly partial struct ChessSicketAspect : IAspect
{
    public readonly Entity Self;
    public readonly RefRW<ChessSocketC> socketC;
    readonly RefRW<ChessSocketPieceLinkC> link;

    public bool IsPieceLinked() => link.ValueRO.pieceE != Entity.Null;

    public void ResetPiece()
    {
        link.ValueRW.pieceE = Entity.Null;
    }

    public void SetPiece(Entity piece)
    {
        link.ValueRW.pieceE = piece;
    }
}