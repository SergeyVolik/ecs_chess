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

public struct ChessBoardC : IComponentData
{
    public float3 spawnGridOffset;
    public float3 offsetBetweenSockets;
    public Entity socketPrefab;

    public ChessPiecesPrefabs black;
    public ChessPiecesPrefabs white;

}

[InternalBufferCapacity(64)]
public struct ChessBoardSockets : IBufferElementData
{
    public Entity socketEntity;
}

public struct ChessGameStartedT : IComponentData
{

}

public struct ChessBoardCreatedT : IComponentData
{

}

public struct ChessPieceC : IComponentData
{
    public ChessType chessType;
    public PieceColor color;
}

public struct ChessPieceSelectedT : IComponentData
{

}

public struct ChessSocketT : IComponentData
{

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