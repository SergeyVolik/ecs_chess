using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct ServerSetupBoardSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ChessBoardPersistentC>();
    }

    public void OnUpdate(ref SystemState state)
    {
        bool isBoardCreated = SystemAPI.HasSingleton<ChessBoardInstanceT>();
        if (!isBoardCreated)
        {
            Debug.Log("[Server] create board");
            SpawnNewBoard(ref state);
        }
    }

    public void SpawnNewBoard(ref SystemState state)
    {
        var persistentData = SystemAPI.GetSingleton<ChessBoardPersistentC>();
        var boardE = state.EntityManager.Instantiate(persistentData.chessBoardPrefab);

        SetupSokets(ref state, boardE, in persistentData);
        SpawnPieces(ref state, boardE, in persistentData);
    }

    public void SetupSokets(ref SystemState state, Entity boardE, in ChessBoardPersistentC bC)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        var sockets = ecb.AddBuffer<ChessBoardInstanceSockets>(boardE);

        for (int z = 0; z < 8; z++)
        {
            for (int x = 0; x < 8; x++)
            {
                var socketInstance = ecb.Instantiate(bC.socketPrefab);
                float3 spawnPos = bC.spawnGridOffset + new float3(x * bC.offsetBetweenSockets.x, 0, z * bC.offsetBetweenSockets.z);
                ecb.SetComponent<LocalTransform>(socketInstance, LocalTransform.FromPosition(spawnPos));

                ecb.SetComponent<ChessSocketC>(socketInstance, new ChessSocketC
                {
                    x = x,
                    y = z,
                    socketE = socketInstance
                });

                ecb.AddComponent<Parent>(socketInstance, new Parent
                {
                    Value = boardE
                });

                sockets.Add(new ChessBoardInstanceSockets
                {
                    socketE = socketInstance,
                });
            }
        }

        ecb.Playback(state.EntityManager);
    }

    public void SpawnPieces(ref SystemState state, Entity boardE, in ChessBoardPersistentC bC)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        NativeList<ChessBoardInstanceSockets> sockets = new NativeList<ChessBoardInstanceSockets>(Allocator.Temp);

        var boardAspect = SystemAPI.GetAspect<ChessBoardInstanceAspect>(boardE);

        var whitePrefabs = bC.whitePiecesPrefabs;
        var blackPrefabs = bC.blackPiecesPrefabs;

        SetupPieces(ref state, boardAspect.GetPawnSocketsBlack(sockets), ecb, blackPrefabs.pawn, boardE);
        SetupPieces(ref state, boardAspect.GetRookSocketsBlack(sockets), ecb, blackPrefabs.rook, boardE);
        SetupPieces(ref state, boardAspect.GetKnightSocketsBlack(sockets), ecb, blackPrefabs.knight, boardE);
        SetupPieces(ref state, boardAspect.GetBishopSocketsBlack(sockets), ecb, blackPrefabs.bishop, boardE);
        SetupPieces(ref state, boardAspect.GetQueenSocketsBlack(sockets), ecb, blackPrefabs.queen, boardE);
        SetupPieces(ref state, boardAspect.GetKingSocketsBlack(sockets), ecb, blackPrefabs.king, boardE);

        SetupPieces(ref state, boardAspect.GetPawnSocketsWhite(sockets), ecb, whitePrefabs.pawn, boardE);
        SetupPieces(ref state, boardAspect.GetRookSocketsWhite(sockets), ecb, whitePrefabs.rook, boardE);
        SetupPieces(ref state, boardAspect.GetKnightSocketsWhite(sockets), ecb, whitePrefabs.knight, boardE);
        SetupPieces(ref state, boardAspect.GetBishopSocketsWhite(sockets), ecb, whitePrefabs.bishop, boardE);
        SetupPieces(ref state, boardAspect.GetQueenSocketsWhite(sockets), ecb, whitePrefabs.queen, boardE);
        SetupPieces(ref state, boardAspect.GetKingSocketsWhite(sockets), ecb, whitePrefabs.king, boardE);

        ecb.Playback(state.EntityManager);

        var black = SystemAPI.GetBuffer<ChessBoardBlackPiecesBuffer>(boardE);
        var white = SystemAPI.GetBuffer<ChessBoardWhitePiecesBuffer>(boardE);

        var bInstance = SystemAPI.GetComponentRW<ChessBoardInstanceT>(boardE);
        foreach (var (c, e) in SystemAPI.Query<ChessPieceC>().WithEntityAccess())
        {
            if (c.color == PieceColor.White)
            {
                white.Add(new ChessBoardWhitePiecesBuffer { pieceE = e });
                if (c.chessType == ChessType.King)
                {
                    bInstance.ValueRW.whiteKingE = e;
                }
            }
            else
            {
                black.Add(new ChessBoardBlackPiecesBuffer { pieceE = e });
                if (c.chessType == ChessType.King)
                {
                    bInstance.ValueRW.blackKingE = e;
                }
            }
        }
    }

    private void SetupPieces(ref SystemState state, NativeList<ChessBoardInstanceSockets> sockets, EntityCommandBuffer ecb, Entity prefab, Entity boardE)
    {
        foreach (var item in sockets)
        {
            var socketTrans = SystemAPI.GetComponent<LocalTransform>(item.socketE);
            var instance = ecb.Instantiate(prefab);

            ecb.AddComponent<ChessSocketPieceLinkC>(item.socketE, new ChessSocketPieceLinkC
            {
                pieceE = instance
            });

            ecb.AddComponent<Parent>(instance, new Parent
            {
                Value = boardE
            });

            ecb.AddComponent<ChessSocketC>(instance, SystemAPI.GetComponent<ChessSocketC>(item.socketE));
            ecb.SetComponent<LocalTransform>(instance, LocalTransform.FromPositionRotation(socketTrans.Position, quaternion.identity));
        }
    }
}
