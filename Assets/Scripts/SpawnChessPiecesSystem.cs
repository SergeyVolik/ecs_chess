using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

//[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Default)]
public partial struct SpawnChessPiecesSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ChessGameStartedT>();
    }

    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer buffer = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        NativeList<ChessBoardSockets> sockets = new NativeList<ChessBoardSockets>(Allocator.Temp);

        foreach (var (boardAspect, boardE) in SystemAPI.Query<ChessBoardAspect>().WithEntityAccess().WithNone<ChessBoardCreatedT>())
        {
            var whitePrefabs = boardAspect.GetWhitePrefabs();
            var blackPrefabs = boardAspect.GetBlackPrefabs();

            buffer.AddComponent<ChessBoardCreatedT>(boardE);

            SetupPieces(ref state, boardAspect.GetPawnSocketsWhite(sockets), buffer, whitePrefabs.pawn, boardE);
            SetupPieces(ref state, boardAspect.GetPawnSocketsBlack(sockets), buffer, blackPrefabs.pawn, boardE);

            SetupPieces(ref state, boardAspect.GetRookSocketsWhite(sockets), buffer, whitePrefabs.rook, boardE);
            SetupPieces(ref state, boardAspect.GetRookSocketsBlack(sockets), buffer, blackPrefabs.rook, boardE);

            SetupPieces(ref state, boardAspect.GetKnightSocketsWhite(sockets), buffer, whitePrefabs.knight, boardE);
            SetupPieces(ref state, boardAspect.GetKnightSocketsBlack(sockets), buffer, blackPrefabs.knight, boardE);

            SetupPieces(ref state, boardAspect.GetBishopSocketsWhite(sockets), buffer, whitePrefabs.bishop, boardE);
            SetupPieces(ref state, boardAspect.GetBishopSocketsBlack(sockets), buffer, blackPrefabs.bishop, boardE);

            SetupPieces(ref state, boardAspect.GetKingSocketsWhite(sockets), buffer, whitePrefabs.king, boardE);
            SetupPieces(ref state, boardAspect.GetKingSocketsBlack(sockets), buffer, blackPrefabs.king, boardE);

            SetupPieces(ref state, boardAspect.GetQueenSocketsWhite(sockets), buffer, whitePrefabs.queen, boardE);
            SetupPieces(ref state, boardAspect.GetQueenSocketsBlack(sockets), buffer, blackPrefabs.queen, boardE);
        }

        buffer.Playback(state.EntityManager);
    }

    private void SetupPieces(ref SystemState state, NativeList<ChessBoardSockets> sockets, EntityCommandBuffer buffer, Entity prefab, Entity boardE)
    {
        foreach (var item in sockets)
        {
            var socketTrans = SystemAPI.GetComponent<LocalTransform>(item.socketE);
            var instance = buffer.Instantiate(prefab);
            buffer.AddComponent<ChessSocketPieceC>(item.socketE, new ChessSocketPieceC
            {
                pieceE = instance
            });
            buffer.AddComponent<Parent>(instance, new Parent { 
                 Value = boardE
            });
            buffer.SetComponent<LocalTransform>(instance, LocalTransform.FromPositionRotation(socketTrans.Position, quaternion.identity));
        }
    }
}
