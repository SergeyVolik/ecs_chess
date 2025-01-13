using Unity.Entities;
using Unity.Mathematics;

public struct ChessPiecesPrefabs
{
    public Entity bishop;
    public Entity king;
    public Entity knight;
    public Entity pawn;
    public Entity queen;
    public Entity rook;
}

public struct ChessBoardPersistentC : IComponentData
{
    public float3 spawnGridOffset;
    public float3 offsetBetweenSockets;
    public Entity socketPrefab;
    public Entity chessBoardPrefab;

    public ChessPiecesPrefabs blackPiecesPrefabs;
    public ChessPiecesPrefabs whitePiecesPrefabs;
}

public struct ChessBoardInstanceT : IComponentData
{
    public Entity whiteKingE;
    public Entity blackKingE;

    public bool inisted;
}
public struct ChessBoardStepsInitedT : IComponentData { }

    [InternalBufferCapacity(64)]
public struct ChessBoardInstanceSockets : IBufferElementData
{
    public Entity socketE;
}

public struct ChessGameStartT : IComponentData { }

public struct ChessBoardTurnC : IComponentData
{
    public PieceColor turnColor;
}
public struct ChessBoardWhitePiecesBuffer : IBufferElementData
{
    public Entity pieceE;
}

public struct ChessBoardBlackPiecesBuffer : IBufferElementData
{
    public Entity pieceE;
}

public struct ChessPieceC : IComponentData
{
    public ChessType chessType;
    public PieceColor color;
    public bool isMovedOnce;

    public override string ToString()
    {
        return $"chessType: {chessType}";
    }
}

public struct ChessPiecePossibleSteps : IBufferElementData
{
    public ChessSocketC socketC;
    public bool hasEnemy;
}

public struct ChessSocketSelectedT : IComponentData
{

}

public struct ChessSocketC : IComponentData
{
    public int x;
    public int y;
    public Entity socketE;
}

public struct ChessSocketInitedT : IComponentData
{

}

public struct ChessSocketHighlightInstanceC : IComponentData
{
    public Entity entity;
}

public struct ChessSocketHighlightC : IComponentData
{
    public Entity highlightSelectedPrefab;
    public Entity highlightMovePosPrefab;
    public Entity highlightEnemyPrefab;
}

public struct ChessSocketPieceLinkC : IComponentData
{
    public Entity pieceE;
}