using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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

}
    [InternalBufferCapacity(64)]
public struct ChessBoardSockets : IBufferElementData
{
    public Entity socketE;
}

public struct ChessStartGameT : IComponentData
{
    
}

public struct ChessBoardTurnC : IComponentData
{
    public PieceColor turnColor;
}
public struct ChessGameWhitePiecesBuffer : IBufferElementData
{
    public Entity pieceE;
}

public struct ChessGamePiecesC : IComponentData
{
    public Entity whiteKingE;
    public Entity blackKingE;
}

public struct ChessGameBlackPiecesBuffer : IBufferElementData
{
    public Entity pieceE;
}
public struct ChessBoardInstanceCreatedT : IComponentData
{

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

public struct ChessSocketSelectedT : IComponentData
{

}

public struct ChessSocketC : IComponentData
{
    public int x;
    public int y;
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

public struct ChessSocketPieceC : IComponentData
{
    public Entity pieceE;
}