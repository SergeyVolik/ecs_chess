using System;
using System.ComponentModel;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

readonly partial struct ChessBoardPersistentAspect : IAspect
{
    public readonly RefRO<ChessBoardPersistentC> boardC;
    public ChessPiecesPrefabs GetWhitePrefabs() => boardC.ValueRO.whitePiecesPrefabs;
    public ChessPiecesPrefabs GetBlackPrefabs() => boardC.ValueRO.blackPiecesPrefabs;
}

readonly partial struct ChessBoardInstanceAspect : IAspect
{
    public readonly Entity Self;
    readonly DynamicBuffer<ChessBoardInstanceSockets> boardSocketsB;
    private readonly DynamicBuffer<ChessBoardBlackPiecesBuffer> boardPiecesBlack;
    private readonly DynamicBuffer<ChessBoardWhitePiecesBuffer> boardPiecesWhite;

    public readonly RefRW<ChessBoardInstanceT> instanceC;
    public readonly RefRW<ChessBoardTurnC> turnC;

    public readonly DynamicBuffer<LinkedEntityGroup> liked;

    public const int GRID_X = 8;
    public const int GRID_Y = 8;

    public bool IsWhiteStep()
    {
        return turnC.ValueRW.isWhite;
    }

    public Entity GetCurrentKing()
    {
        return IsWhiteStep() ? instanceC.ValueRO.whiteKingE : instanceC.ValueRO.blackKingE;
    }

    public Entity GetWhiteKing()
    {
        return instanceC.ValueRO.whiteKingE;
    }

    public Entity GetBlackKing()
    {
        return instanceC.ValueRO.whiteKingE;
    }

    public ChessBoardInstanceSockets GetSocket(int x, int y)
    {
        return boardSocketsB[y * GRID_Y + x];
    }


    public NativeArray<Entity> GetBlackPieces()
    {
        return boardPiecesBlack.Reinterpret<Entity>().AsNativeArray();
    }

    public NativeArray<Entity> GetWhitePieces()
    {
        return boardPiecesWhite.Reinterpret<Entity>().AsNativeArray();
    }

    public NativeArray<Entity> GetOponentPieces()
    {
       return IsWhiteStep() ?
           boardPiecesBlack.Reinterpret<Entity>().AsNativeArray() :
           boardPiecesWhite.Reinterpret<Entity>().AsNativeArray();
    }

    public NativeArray<Entity> GetCurrentPlayerPieces()
    {
        return IsWhiteStep() ?
            boardPiecesWhite.Reinterpret<Entity>().AsNativeArray() :
            boardPiecesBlack.Reinterpret<Entity>().AsNativeArray();
    }

    public ChessBoardInstanceSockets GetSocket(int index)
    {
        return boardSocketsB[index];
    }

    public bool SocketPosition(Entity socket, out int2 xy)
    {

        xy.x = -1;
        xy.y = -1;

        for (int i = 0; i < boardSocketsB.Length; i++)
        {
            if (boardSocketsB[i].socketE == socket)
            {
                xy.x = i % GRID_X;
                xy.y = i / GRID_X;

                return true;
            }
        }

        return false;
    }
    public int IndexOf(Entity socket)
    {
        for (int i = 0; i < boardSocketsB.Length; i++)
        {
            if (boardSocketsB[i].socketE == socket)
                return i;
        }

        return -1;
    }

    public bool IsBoardEnd(bool isWhite, int index)
    {
        return isWhite == false && index >= 0 && index < GRID_Y
            || isWhite == true && index >= GRID_Y * GRID_X - GRID_Y && index < GRID_Y * GRID_X;
    }

    private void GetRow(NativeList<ChessBoardInstanceSockets> sockets, int rowIndex)
    {
        sockets.Clear();
        sockets.Add(GetSocket(0, rowIndex));
        sockets.Add(GetSocket(1, rowIndex));
        sockets.Add(GetSocket(2, rowIndex));
        sockets.Add(GetSocket(3, rowIndex));
        sockets.Add(GetSocket(4, rowIndex));
        sockets.Add(GetSocket(5, rowIndex));
        sockets.Add(GetSocket(6, rowIndex));
        sockets.Add(GetSocket(7, rowIndex));
    }

    public NativeList<ChessBoardInstanceSockets> GetPawnSocketsWhite(NativeList<ChessBoardInstanceSockets> sockets)
    {
        GetRow(sockets, 1);
        return sockets;
    }

    public NativeList<ChessBoardInstanceSockets> GetPawnSocketsBlack(NativeList<ChessBoardInstanceSockets> sockets)
    {
        GetRow(sockets, GRID_Y - 2);
        return sockets;
    }

    public NativeList<ChessBoardInstanceSockets> GetRookSocketsWhite(NativeList<ChessBoardInstanceSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetSocket(0, 0));
        sockets.Add(GetSocket(GRID_X - 1, 0));
        return sockets;
    }

    public NativeList<ChessBoardInstanceSockets> GetRookSocketsBlack(NativeList<ChessBoardInstanceSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetSocket(0, GRID_Y - 1));
        sockets.Add(GetSocket(GRID_X - 1, GRID_Y - 1));
        return sockets;
    }

    public NativeList<ChessBoardInstanceSockets> GetKnightSocketsBlack(NativeList<ChessBoardInstanceSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetSocket(1, GRID_Y - 1));
        sockets.Add(GetSocket(GRID_X - 2, GRID_Y - 1));
        return sockets;
    }

    public NativeList<ChessBoardInstanceSockets> GetKnightSocketsWhite(NativeList<ChessBoardInstanceSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetSocket(1, 0));
        sockets.Add(GetSocket(GRID_X - 2, 0));
        return sockets;
    }

    public NativeList<ChessBoardInstanceSockets> GetBishopSocketsBlack(NativeList<ChessBoardInstanceSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetSocket(2, GRID_Y - 1));
        sockets.Add(GetSocket(GRID_X - 3, GRID_Y - 1));
        return sockets;
    }

    public NativeList<ChessBoardInstanceSockets> GetBishopSocketsWhite(NativeList<ChessBoardInstanceSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetSocket(2, 0));
        sockets.Add(GetSocket(GRID_X - 3, 0));
        return sockets;
    }

    public NativeList<ChessBoardInstanceSockets> GetKingSocketsWhite(NativeList<ChessBoardInstanceSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetKingSocketWhite());
        return sockets;
    }

    public NativeList<ChessBoardInstanceSockets> GetKingSocketsBlack(NativeList<ChessBoardInstanceSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetKingSocketBlack());
        return sockets;
    }

    public NativeList<ChessBoardInstanceSockets> GetQueenSocketsWhite(NativeList<ChessBoardInstanceSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetQueenSocketWhite());
        return sockets;
    }

    public NativeList<ChessBoardInstanceSockets> GetQueenSocketsBlack(NativeList<ChessBoardInstanceSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetQueenSocketBlack());
        return sockets;
    }
    public ChessBoardInstanceSockets GetQueenSocketWhite() => GetSocket(4, 0);
    public ChessBoardInstanceSockets GetQueenSocketBlack() => GetSocket(4, GRID_Y - 1);

    public ChessBoardInstanceSockets GetKingSocketWhite() => GetSocket(3, 0);
    public ChessBoardInstanceSockets GetKingSocketBlack() => GetSocket(3, GRID_Y - 1);

    internal void NextTurn()
    {
        turnC.ValueRW.isWhite = !turnC.ValueRW.isWhite;
    }
}