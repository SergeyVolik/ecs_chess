using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial struct StartChessGameSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ChessStartGameT>();
    }

    public void OnUpdate(ref SystemState state)
    {
        RemovePrevBoard(ref state);
        SpawnNewBoard(ref state);

        state.EntityManager.DestroyEntity(SystemAPI.QueryBuilder().WithAll<ChessStartGameT>().Build());
    }

    public void SpawnNewBoard(ref SystemState state)
    {
        var persistentData = SystemAPI.GetSingleton<ChessBoardPersistentC>();
        var boardE = state.EntityManager.Instantiate(persistentData.chessBoardPrefab);
        state.EntityManager.AddComponent<ChessBoardInstanceT>(boardE);
        state.EntityManager.AddComponent<ChessBoardTurnC>(boardE);

        SetupSokets(ref state, boardE, in persistentData);
        SpawnPieces(ref state, boardE, in persistentData);
    }
    public void RemovePrevBoard(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (c,e) in SystemAPI.Query<ChessBoardInstanceT>().WithEntityAccess())
        {
            ecb.DestroyEntity(e);
        }

        ecb.Playback(state.EntityManager);
    }

    public void SetupSokets(ref SystemState state, Entity boardE, in ChessBoardPersistentC bC)
    {
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

        var sockets = ecb.AddBuffer<ChessBoardSockets>(boardE);

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
                    y = z
                });

                ecb.AddComponent<Parent>(socketInstance, new Parent
                {
                    Value = boardE
                });

                sockets.Add(new ChessBoardSockets
                {
                    socketE = socketInstance,
                });

                ecb.AppendToBuffer<LinkedEntityGroup>(boardE, new LinkedEntityGroup { 
                     Value = socketInstance,
                });
            }
        }

        ecb.Playback(state.EntityManager);
    }

    public void SpawnPieces(ref SystemState state, Entity boardE, in ChessBoardPersistentC bC)
    {

        EntityCommandBuffer buffer = new EntityCommandBuffer(Allocator.Temp);
        NativeList<ChessBoardSockets> sockets = new NativeList<ChessBoardSockets>(Allocator.Temp);

        var boardAspect = SystemAPI.GetAspect<ChessBoardInstanceAspect>(boardE);

        var whitePrefabs = bC.whitePiecesPrefabs;
        var blackPrefabs = bC.blackPiecesPrefabs;

        buffer.AddComponent<ChessBoardInstanceCreatedT>(boardE);

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

        buffer.Playback(state.EntityManager);
    }

    private void SetupPieces(ref SystemState state, NativeList<ChessBoardSockets> sockets, EntityCommandBuffer ecb, Entity prefab, Entity boardE)
    {
        foreach (var item in sockets)
        {
            var socketTrans = SystemAPI.GetComponent<LocalTransform>(item.socketE);
            var instance = ecb.Instantiate(prefab);
            ecb.AddComponent<ChessSocketPieceC>(item.socketE, new ChessSocketPieceC
            {
                pieceE = instance
            });
            ecb.AddComponent<Parent>(instance, new Parent
            {
                Value = boardE
            });
            ecb.AppendToBuffer<LinkedEntityGroup>(boardE, new LinkedEntityGroup
            {
                Value = instance,
            });
            ecb.SetComponent<LocalTransform>(instance, LocalTransform.FromPositionRotation(socketTrans.Position, quaternion.identity));
        }
    }
}
