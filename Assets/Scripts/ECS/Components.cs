using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

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

    public ChessPiecesPrefabs blackPiecesMeshPrefabs;
    public ChessPiecesPrefabs whitePiecesMeshPrefabs;

    public ChessPiecesPrefabs blackPiecesDataPrefabs;
    public ChessPiecesPrefabs whitePiecesDataPrefabs;
}

public struct ChessBoardInstanceT : IComponentData
{
    [GhostField] public Entity whiteKingE;
    [GhostField] public Entity blackKingE;

    [GhostField] public bool inited;
    internal bool blockInput;
}
public struct ChessBoardStepsInitedT : IComponentData { }

[InternalBufferCapacity(64)]
public struct ChessBoardInstanceSockets : IBufferElementData
{
    public Entity socketE;
}

public struct GrabChessRpc : IRpcCommand
{
    public float3 rayFrom;
    public float3 rayTo;
}

public struct MoveChessRpc : IRpcCommand
{
    public float3 rayFrom;
    public float3 rayTo;
}

public struct DropChessRpc : IRpcCommand
{
    public float3 rayFrom;
    public float3 rayTo;
}


public struct EnablePlayerInputT : IComponentData { }
public struct ChessGameStartT : IComponentData { }

public struct ChessBoardTurnC : IComponentData
{
    [GhostField] public bool isWhite;
}
public struct ChessBoardWhitePiecesBuffer : IBufferElementData
{
    [GhostField] public int pieceId;
}

public struct ChessBoardBlackPiecesBuffer : IBufferElementData
{
    [GhostField] public int pieceId;
}

public struct ChessBoardAllPiecesMeshes : IBufferElementData
{
    [GhostField] public Entity meshPieceE;  
}

public struct ChessBoardAllPiecesData : IBufferElementData
{
    [GhostField] public Entity dataPieceE;
}

    public struct ChessPieceC : IComponentData
{
    [GhostField] public ChessType chessType;
    [GhostField] public bool isWhite;
    [GhostField] public bool isMovedOnce;
    [GhostField] public bool isNotActive;
    public int numberOfMoves;
    public override string ToString()
    {
        return $"chessType: {chessType}";
    }
}

public struct ChessPiecePossibleSteps : IBufferElementData
{
    [GhostField] public ChessSocketC defaultMoveTO;
    [GhostField] public bool hasEnemy;

    [GhostField] public bool is—astling;
    [GhostField] public —astlingData castlingMove;

    [GhostField] public bool isTakeOfThePass;
    [GhostField] public TakeOfThePassData TakeOfThePassData;
}

[System.Serializable]
public struct —astlingData
{
    public ChessSocketC kingMoveTo;
    public ChessSocketC rookMoveTo;
}


[System.Serializable]
public struct TakeOfThePassData
{
    public ChessSocketC moveToSocket;
    public ChessSocketC destoryPieceSocket;
}


public struct ChessSocketSelectedT : IComponentData
{

}

public struct ChessSocketC : IComponentData
{
    [GhostField] public int x;
    [GhostField] public int y;
    [GhostField] public Entity socketE;
}

public struct ChessSocketInitedT : IComponentData
{

}

public struct ChessSocketHighlightInstanceC : IComponentData
{
    public Entity entity;
}

public struct ChessSocketPrevMoveC : IComponentData
{
    public Entity entity;
}

public struct ChessSocketHighlightC : IComponentData
{
    public Entity highlightSelectedPrefab;
    public Entity highlightMovePosPrefab;
    public Entity highlightEnemyPrefab;
}

public struct ChessSocketPieceIdC : IComponentData
{
    [GhostField] public int pieceId;

    public void Reset()
    {
        pieceId = -1;
    }
}