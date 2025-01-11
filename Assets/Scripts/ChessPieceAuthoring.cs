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

public enum PieceColor
{
    White,
    Black
}

public class ChessPieceAuthoring : MonoBehaviour
{
    public ChessType chessType;
    public PieceColor color;

    public class Baker : Baker<ChessPieceAuthoring>
    {
        public override void Bake(ChessPieceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<ChessPieceC>(entity, new ChessPieceC
            {
                chessType = authoring.chessType,
                color = authoring.color,
            });
        }
    }
}