using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public static class BoardPositions
{
    public static readonly string[] horizontal = {
        "a",
        "b",
        "c",
        "d",
        "e",
        "f",
        "g",
        "h"
    };
    public static readonly string[] vertical = {
        "1",
        "2",
        "3",
        "4",
        "5",
        "6",
        "7",
        "8"
    };
}

public enum ChessPositionHorizontal
{
    a,
    b,
    c,
    d,
    e,
    f,
    g,
    h
}
public enum ChessPositionVertical
{
    one,
    two,
    three,
    four,
    five,
    fix,
    seven,
    eight
}

readonly partial struct ChessBoardPersistentAspect : IAspect
{
    public readonly RefRO<ChessBoardPersistentC> boardC;
    public ChessPiecesPrefabs GetWhitePrefabs() => boardC.ValueRO.whitePiecesMeshPrefabs;
    public ChessPiecesPrefabs GetBlackPrefabs() => boardC.ValueRO.blackPiecesMeshPrefabs;
}

readonly partial struct ChessBoardInstanceAspect : IAspect
{
    public readonly Entity Entity;
    public readonly DynamicBuffer<KilledPieces> killedPieces;

    public readonly DynamicBuffer<ChessBoardAllPiecesMeshes> allPiecesMeshesB;
    public readonly DynamicBuffer<ChessBoardAllPiecesData> allPiecesDataB;

    public readonly DynamicBuffer<ChessBoardInstanceSockets> boardSocketsB;
    private readonly DynamicBuffer<ChessBoardBlackPiecesBuffer> boardPiecesBlack;
    private readonly DynamicBuffer<ChessBoardWhitePiecesBuffer> boardPiecesWhite;

    public readonly RefRW<ChessBoardInstanceC> instanceC;
    public readonly RefRW<ChessBoardTurnC> turnC;

    public readonly DynamicBuffer<LinkedEntityGroup> liked;

    public const int GRID_X = 8;
    public const int GRID_Y = 8;

    public Entity GetPieceDataById(int id)
    {
        if (id == -1)
            return Entity.Null;

        return allPiecesDataB[id].dataPieceE;
    }

    public Entity GetPieceMeshById(int id)
    {
        if (id == -1)
            return Entity.Null;

        return allPiecesMeshesB[id].meshPieceE;
    }

    public bool IsWhiteStep()
    {
        return turnC.ValueRW.isWhite;
    }

    public Entity GetCurrentKing()
    {
        return IsWhiteStep() ? instanceC.ValueRO.whiteKingE : instanceC.ValueRO.blackKingE;
    }

    public Entity GetOponentKing()
    {
        return IsWhiteStep() ? instanceC.ValueRO.blackKingE : instanceC.ValueRO.whiteKingE;
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

    public NativeArray<int> GetBlackPiecesIds()
    {
        return boardPiecesBlack.Reinterpret<int>().AsNativeArray();
    }

    public NativeArray<int> GetWhitePiecesIds()
    {
        return boardPiecesWhite.Reinterpret<int>().AsNativeArray();
    }

    public NativeArray<int> GetOponentPiecesIds()
    {
       return IsWhiteStep() ?
           boardPiecesBlack.Reinterpret<int>().AsNativeArray() :
           boardPiecesWhite.Reinterpret<int>().AsNativeArray();
    }

    public NativeArray<int> GetCurrentPlayerPiecesIds()
    {
        return IsWhiteStep() ?
            boardPiecesWhite.Reinterpret<int>().AsNativeArray() :
            boardPiecesBlack.Reinterpret<int>().AsNativeArray();
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

    public void GetSocketPosition(Entity socket, out int x, out int y)
    {
        int index = -1;
        x = -1;
        y = -1;
        for (int i = 0; i < boardSocketsB.Length; i++)
        {
            if (boardSocketsB[i].socketE == socket)
                index = i;
        }

        x = index % GRID_X;
        y = index / GRID_X;    
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