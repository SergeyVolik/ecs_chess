using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct StartGameT : IComponentData
{

}

readonly partial struct ChestBoardAspect : IAspect
{
    public readonly Entity Self;
    readonly DynamicBuffer<ChessBoardSockets> boardSocketsB;
    public readonly RefRO<ChessBoardC> boardC;

    const int GRID_X = 8;
    const int GRID_Y = 8;

    public ChessPiecesPrefabs GetWhitePrefabs() => boardC.ValueRO.white;
    public ChessPiecesPrefabs GetBlackPrefabs() => boardC.ValueRO.black;


    public ChessBoardSockets GetSocket(int x, int y)
    {
        return boardSocketsB[y * GRID_Y + x];
    }

    private void GetRow(NativeList<ChessBoardSockets> sockets, int rowIndex)
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

    public NativeList<ChessBoardSockets> GetPawnSocketsWhite(NativeList<ChessBoardSockets> sockets)
    {
        GetRow(sockets, 1);
        return sockets;
    }

    public NativeList<ChessBoardSockets> GetPawnSocketsBlack(NativeList<ChessBoardSockets> sockets)
    {
        GetRow(sockets, GRID_Y - 2);
        return sockets;
    }

    public NativeList<ChessBoardSockets> GetRookSocketsWhite(NativeList<ChessBoardSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetSocket(0, 0));
        sockets.Add(GetSocket(GRID_X - 1, 0));
        return sockets;
    }

    public NativeList<ChessBoardSockets> GetRookSocketsBlack(NativeList<ChessBoardSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetSocket(0, GRID_Y-1));
        sockets.Add(GetSocket(GRID_X - 1, GRID_Y - 1));
        return sockets;
    }

    public NativeList<ChessBoardSockets> GetKnightSocketsBlack(NativeList<ChessBoardSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetSocket(1, GRID_Y - 1));
        sockets.Add(GetSocket(GRID_X - 2, GRID_Y - 1));
        return sockets;
    }

    public NativeList<ChessBoardSockets> GetKnightSocketsWhite(NativeList<ChessBoardSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetSocket(1, 0));
        sockets.Add(GetSocket(GRID_X - 2, 0));
        return sockets;
    }

    public NativeList<ChessBoardSockets> GetBishopSocketsBlack(NativeList<ChessBoardSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetSocket(2, GRID_Y - 1));
        sockets.Add(GetSocket(GRID_X - 3, GRID_Y - 1));
        return sockets;
    }

    public NativeList<ChessBoardSockets> GetBishopSocketsWhite(NativeList<ChessBoardSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetSocket(2, 0));
        sockets.Add(GetSocket(GRID_X - 3, 0));
        return sockets;
    }

    public NativeList<ChessBoardSockets> GetKingSocketsWhite(NativeList<ChessBoardSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetKingSocketWhite());
        return sockets;
    }

    public NativeList<ChessBoardSockets> GetKingSocketsBlack(NativeList<ChessBoardSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetKingSocketBlack());
        return sockets;
    }

    public NativeList<ChessBoardSockets> GetQueenSocketsWhite(NativeList<ChessBoardSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetQueenSocketWhite());
        return sockets;
    }

    public NativeList<ChessBoardSockets> GetQueenSocketsBlack(NativeList<ChessBoardSockets> sockets)
    {
        sockets.Clear();
        sockets.Add(GetQueenSocketBlack());
        return sockets;
    }
    public ChessBoardSockets GetQueenSocketWhite() => GetSocket(4, 0);
    public ChessBoardSockets GetQueenSocketBlack() => GetSocket(3, GRID_Y - 1);

    public ChessBoardSockets GetKingSocketWhite() => GetSocket(3, 0);
    public ChessBoardSockets GetKingSocketBlack() => GetSocket(4, GRID_Y - 1);
}

public partial struct SpawnChessPiecesSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {

    }

    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer buffer = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        NativeList<ChessBoardSockets> sockets = new NativeList<ChessBoardSockets>(Allocator.Temp);

        foreach (var (boardAspect, e) in SystemAPI.Query<ChestBoardAspect>().WithEntityAccess().WithAll<StartGameT>())
        {
            var whitePrefabs = boardAspect.GetWhitePrefabs();
            var blackPrefabs = boardAspect.GetBlackPrefabs();

            buffer.RemoveComponent<StartGameT>(e);

            SetupPieces(ref state, boardAspect.GetPawnSocketsWhite(sockets), buffer, whitePrefabs.pawn);
            SetupPieces(ref state, boardAspect.GetPawnSocketsBlack(sockets), buffer, blackPrefabs.pawn);
            SetupPieces(ref state, boardAspect.GetRookSocketsWhite(sockets), buffer, whitePrefabs.rook);
            SetupPieces(ref state, boardAspect.GetRookSocketsBlack(sockets), buffer, blackPrefabs.rook);
            SetupPieces(ref state, boardAspect.GetRookSocketsWhite(sockets), buffer, whitePrefabs.rook);
            SetupPieces(ref state, boardAspect.GetRookSocketsBlack(sockets), buffer, blackPrefabs.rook);
            SetupPieces(ref state, boardAspect.GetKnightSocketsWhite(sockets), buffer, whitePrefabs.knight);
            SetupPieces(ref state, boardAspect.GetKnightSocketsBlack(sockets), buffer, blackPrefabs.knight);
            SetupPieces(ref state, boardAspect.GetBishopSocketsWhite(sockets), buffer, whitePrefabs.bishop);
            SetupPieces(ref state, boardAspect.GetBishopSocketsBlack(sockets), buffer, blackPrefabs.bishop);
            SetupPieces(ref state, boardAspect.GetKingSocketsWhite(sockets), buffer, whitePrefabs.king);
            SetupPieces(ref state, boardAspect.GetKingSocketsBlack(sockets), buffer, blackPrefabs.king);
            SetupPieces(ref state, boardAspect.GetQueenSocketsWhite(sockets), buffer, whitePrefabs.queen);
            SetupPieces(ref state, boardAspect.GetQueenSocketsBlack(sockets), buffer, blackPrefabs.queen);
        }

        buffer.Playback(state.EntityManager);
    }

    private void SetupPieces(ref SystemState state, NativeList<ChessBoardSockets> sockets, EntityCommandBuffer buffer, Entity prefab)
    {
        foreach (var item in sockets)
        {
            var socketTrans = SystemAPI.GetComponent<LocalTransform>(item.socketEntity);
            var instance = buffer.Instantiate(prefab);
            buffer.SetComponent<LocalTransform>(instance, LocalTransform.FromPositionRotation(socketTrans.Position, quaternion.identity));
        }
    }
}
