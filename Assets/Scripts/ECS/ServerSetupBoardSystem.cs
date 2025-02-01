using System;
using System.Net.Sockets;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[System.Serializable]
public struct SpawnPieceData
{
    public ChessPositionHorizontal horizontal;
    public ChessPositionVertical vertical;
    public ChessType type;
}

public class ChessBoardInstanceSpawnConfigC : IComponentData
{
    public ChessBoardConfigurationSO config;
}


[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct ServerSetupBoardSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ChessBoardPersistentC>();
    }

    public void OnUpdate(ref SystemState state)
    {
        bool isBoardCreated = SystemAPI.HasSingleton<ChessBoardInstanceC>();
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

        SpawnBoardWithConfig(ref state, boardE, in persistentData);
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

                sockets.Add(new ChessBoardInstanceSockets
                {
                    socketE = socketInstance,
                });
            }
        }

        ecb.Playback(state.EntityManager);
    }

    public void SpawnBoardWithConfig(ref SystemState state, Entity boardE, in ChessBoardPersistentC bC)
    {
        var config = SystemAPI.ManagedAPI.GetComponent<ChessBoardInstanceSpawnConfigC>(boardE).config;

        int pieceIndex = 0;
        SpawnPieces(ref state, boardE, config.black, bC.blackPiecesMeshPrefabs, bC.blackPiecesDataPrefabs, false, ref pieceIndex);
        SpawnPieces(ref state, boardE, config.white, bC.whitePiecesMeshPrefabs, bC.whitePiecesDataPrefabs, true, ref pieceIndex);

        SetupPiecesBuffers(ref state, boardE);

        SystemAPI.SetComponent<ChessBoardTurnC>(boardE, new ChessBoardTurnC
        {
            isWhite = config.currentTurnWhite
        });
    }

    public void SpawnPieces(
        ref SystemState state,
          Entity boardE,
        SpawnPieceData[] spawn,
        ChessPiecesPrefabs meshPrefabs,
        ChessPiecesPrefabs dataPrefabs,

        bool isWhite,
        ref int pieceIndex)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
        var boardAspect = SystemAPI.GetAspect<ChessBoardInstanceAspect>(boardE);
        var sockets = new NativeList<ChessBoardInstanceSockets>(Allocator.Temp);

        foreach (var spawnData in spawn)
        {
            Entity prefabMeshPrefab = Entity.Null;
            Entity prefabDataPrefab = Entity.Null;

            switch (spawnData.type)
            {
                case ChessType.Pawn:
                    prefabMeshPrefab = meshPrefabs.pawn;
                    prefabDataPrefab = dataPrefabs.pawn;

                    break;
                case ChessType.Bishop:
                    prefabMeshPrefab = meshPrefabs.bishop;
                    prefabDataPrefab = dataPrefabs.bishop;
                    break;
                case ChessType.Rook:
                    prefabMeshPrefab = meshPrefabs.rook;
                    prefabDataPrefab = dataPrefabs.rook;

                    break;
                case ChessType.Knight:
                    prefabMeshPrefab = meshPrefabs.knight;
                    prefabDataPrefab = dataPrefabs.knight;

                    break;
                case ChessType.Queen:
                    prefabMeshPrefab = meshPrefabs.queen;
                    prefabDataPrefab = dataPrefabs.queen;

                    break;
                case ChessType.King:
                    prefabMeshPrefab = meshPrefabs.king;
                    prefabDataPrefab = dataPrefabs.king;

                    break;
                default:
                    break;
            }

            sockets.Clear();
            sockets.Add(boardAspect.GetSocket((int)spawnData.horizontal, (int)spawnData.vertical));
            SetupPieces(ref state, sockets, ecb, prefabMeshPrefab, prefabDataPrefab, boardE, spawnData.type, isWhite, ref pieceIndex);
        }

        ecb.Playback(state.EntityManager);
    }

    private void SetupPiecesBuffers(ref SystemState state, Entity boardE)
    {
        var black = SystemAPI.GetBuffer<ChessBoardBlackPiecesBuffer>(boardE);
        var white = SystemAPI.GetBuffer<ChessBoardWhitePiecesBuffer>(boardE);
        black.Clear();
        white.Clear();

        var boardAspect = SystemAPI.GetAspect<ChessBoardInstanceAspect>(boardE);
        var bInstance = SystemAPI.GetComponentRW<ChessBoardInstanceC>(boardE);

        for (int i = 0; i < boardAspect.allPiecesDataB.Length; i++)
        {
            var entity = boardAspect.allPiecesDataB[i].dataPieceE;
            var c = SystemAPI.GetComponent<ChessPieceC>(boardAspect.allPiecesDataB[i].dataPieceE);
            if (c.isWhite)
            {
                white.Add(new ChessBoardWhitePiecesBuffer {  pieceId = i });
                if (c.chessType == ChessType.King)
                {
                    bInstance.ValueRW.whiteKingE = entity;
                }
            }
            else
            {
                black.Add(new ChessBoardBlackPiecesBuffer { pieceId = i });
                if (c.chessType == ChessType.King)
                {
                    bInstance.ValueRW.blackKingE = entity;
                }
            }
        }
    }

    private void SetupPieces(
        ref SystemState state,
        NativeList<ChessBoardInstanceSockets> sockets,
        EntityCommandBuffer ecb,
        Entity meshPrefab,
        Entity dataPrefab,
        Entity boardE,
        ChessType type,
        bool isWhite,
        ref int pieceIndex)
    {
        foreach (var item in sockets)
        {
            var socketTrans = SystemAPI.GetComponent<LocalTransform>(item.socketE);
            var meshInstance = ecb.Instantiate(meshPrefab);

            ecb.AddComponent<ChessSocketPieceIdC>(item.socketE, new ChessSocketPieceIdC
            {
                pieceId = pieceIndex
            });

            ecb.SetComponent<LocalTransform>(meshInstance, LocalTransform.FromPositionRotation(socketTrans.Position, quaternion.identity));

            var dataE = ecb.Instantiate(dataPrefab);

            ecb.AddComponent<ChessPieceC>(dataE, new ChessPieceC
            {
                chessType = type,
                isWhite = isWhite,
            });

            ecb.AddComponent<ChessSocketC>(dataE, SystemAPI.GetComponent<ChessSocketC>(item.socketE));
            ecb.AddBuffer<ChessPiecePossibleSteps>(dataE);

            ecb.AppendToBuffer<ChessBoardAllPiecesMeshes>(boardE, new ChessBoardAllPiecesMeshes
            {
                meshPieceE = meshInstance,
            });

            ecb.AppendToBuffer<ChessBoardAllPiecesData>(boardE, new ChessBoardAllPiecesData
            {
                dataPieceE = dataE
            });
            pieceIndex++;
        }
    }
}
