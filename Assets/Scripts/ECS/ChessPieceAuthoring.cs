using Unity.Entities;
using UnityEngine;

public enum ChessType
{
    Pawn,
    Bishop,
    Rook,
    Knight,
    Queen,
    King
}

public class ChessPieceAuthoring : MonoBehaviour
{
    public ChessType chessType;
    public bool isWhite;

    public class Baker : Baker<ChessPieceAuthoring>
    {
        public override void Bake(ChessPieceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ChessPieceC>(entity, new ChessPieceC
            {
                chessType = authoring.chessType,
                isWhite = authoring.isWhite,
            });
            AddComponent<ChessSocketC>(entity);
            AddBuffer<ChessPiecePossibleSteps>(entity);
        }
    }
}