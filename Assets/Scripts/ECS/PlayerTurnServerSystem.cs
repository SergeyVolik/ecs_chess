using System;
using System.Net.Sockets;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public enum WinReason
{
    Win,
    OponentSurrendreed
}

public struct EndGameRPC : IRpcCommand
{
    public bool isWhiteWin;
    public WinReason winReason;
}

public struct PieceSnapshotData
{
    public ChessPieceC pieceC;
    public ChessSocketC socketC;
    public Entity pieceMesh;
}
public struct PrevMove
{
    public Entity from;
    public Entity to;
}

public struct SocketSnapshotData
{
    public ChessSocketPieceIdC socketLinkC;
}

public struct BoardShapshot : IDisposable
{
    public NativeList<PieceSnapshotData> pieces;
    public NativeList<SocketSnapshotData> sockets;
    public bool isWhiteTurn;

    public void Dispose()
    {
        pieces.Dispose();
        sockets.Dispose();
    }

    public void Clear()
    {
        sockets.Clear();
        pieces.Clear();
    }
}
public struct MoveChess : IComponentData
{
    public int fromIndex;
    public int toIndex;
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class PlayerTurnServerSystem : SystemBase
{


    public struct SavedPieceTransformationData
    {
        public Entity socket;
        public int pieceId;
        public bool isWhite;
        public bool requireTransformation;
    }

    private Entity m_LastSelectedSocket;
    private Entity m_LastSelectedPieceE;
    private Entity m_LastSelectedPieceMeshE;

    public NativeList<PrevMove> m_PrevRealMoves;

    public SavedPieceTransformationData requireTransformData;

    BoardShapshot m_TempSnapshot;
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<ChessBoardInstanceT>();
        m_PrevRealMoves = new NativeList<PrevMove>(Allocator.Persistent);

        m_TempSnapshot = new BoardShapshot
        {
            pieces = new NativeList<PieceSnapshotData>(Allocator.Persistent),
            sockets = new NativeList<SocketSnapshotData>(Allocator.Persistent)
        };
    }
    protected override void OnDestroy()
    {
        base.OnDestroy();

        m_PrevRealMoves.Dispose();
        m_TempSnapshot.Dispose();
    }

    BoardShapshot CalculateSnapshot(BoardShapshot snapshot)
    {
        snapshot.Clear();
        var board = GetBoard();
        snapshot.isWhiteTurn = board.turnC.ValueRW.isWhite;
        foreach (var item in board.boardSocketsB)
        {
            snapshot.sockets.Add(new SocketSnapshotData
            {
                socketLinkC = SystemAPI.GetComponent<ChessSocketPieceIdC>(item.socketE)
            });
        }

        for (int i = 0; i < board.allPiecesDataB.Length; i++)
        {
            var pieceE = board.allPiecesDataB[i].dataPieceE;

            var data = new PieceSnapshotData
            {
                pieceC = SystemAPI.GetComponent<ChessPieceC>(pieceE),
                socketC = SystemAPI.GetComponent<ChessSocketC>(pieceE),
                pieceMesh = board.allPiecesMeshesB[i].meshPieceE
            };

            snapshot.pieces.Add(data);
        }

        return snapshot;
    }

    void ReturnBoardToSnapshot(BoardShapshot snapshot, bool ignoreMesh, EntityCommandBuffer ecb)
    {
        var board = GetBoard();
        board.turnC.ValueRW.isWhite = snapshot.isWhiteTurn;

        for (int i = 0; i < snapshot.sockets.Length; i++)
        {
            var socket = board.boardSocketsB[i].socketE;
            SystemAPI.SetComponent<ChessSocketPieceIdC>(socket, snapshot.sockets[i].socketLinkC);
        }

        var meshes = ecb.SetBuffer<ChessBoardAllPiecesMeshes>(board.Entity);
        meshes.CopyFrom(board.allPiecesMeshesB);

        for (int i = 0; i < snapshot.pieces.Length; i++)
        {
            var pieceDataE = board.allPiecesDataB[i].dataPieceE;

            var pieceC = snapshot.pieces[i].pieceC;
            SystemAPI.SetComponent<ChessPieceC>(pieceDataE, pieceC);
            SystemAPI.SetComponent<ChessSocketC>(pieceDataE, snapshot.pieces[i].socketC);

            if (!pieceC.isNotActive && !ignoreMesh)
            {
                var pieceMeshE = board.allPiecesMeshesB[i].meshPieceE;

                if (SystemAPI.HasComponent<ChessPieceC>(pieceMeshE))
                {
                    var meshPieceD = SystemAPI.GetComponent<ChessPieceC>(pieceMeshE);

                    if (meshPieceD.chessType != pieceC.chessType)
                    {
                        ecb.DestroyEntity(pieceMeshE);
                        var boardPrefabs = SystemAPI.GetSingleton<ChessBoardPersistentC>();
                        meshes[i] = new ChessBoardAllPiecesMeshes
                        {
                            meshPieceE = boardPrefabs.InstantiateChessMesh(pieceC.isWhite, pieceC.chessType, ecb)
                        };

                        pieceMeshE = meshes[i].meshPieceE;
                    }
                }
                else
                {
                    var boardPrefabs = SystemAPI.GetSingleton<ChessBoardPersistentC>();

                    meshes[i] = new ChessBoardAllPiecesMeshes
                    {
                        meshPieceE = boardPrefabs.InstantiateChessMesh(pieceC.isWhite, pieceC.chessType, ecb)
                    };
                    pieceMeshE = meshes[i].meshPieceE;
                }

                var position = SystemAPI.GetComponent<LocalTransform>(snapshot.pieces[i].socketC.socketE).Position;
                ecb.SetComponent<LocalTransform>(pieceMeshE, LocalTransform.FromPosition(position));
            }
        }
    }

    protected override void OnUpdate()
    {
        InitBoard();
        ExecuteTransfomation();
        MoveOrSelect(out bool needMove, out MoveChess moveData);

        if (needMove)
        {
            ExecuteMove(moveData);
        }
    }

    private void ExecuteTransfomation()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool transformed = false;
        foreach (var (rpc, e) in SystemAPI.Query<PieceTransformationRpc>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
        {
            if (requireTransformData.requireTransformation)
            {
                TransformPiece(requireTransformData.socket, ecb, requireTransformData.pieceId, requireTransformData.isWhite, rpc.type);
                transformed = true;
                requireTransformData = new SavedPieceTransformationData();
            }
            ecb.DestroyEntity(e);
        }
        ecb.Playback(EntityManager);

        if (transformed)
        {
            NextTurn();
        }
    }

    private void ExecuteMove(MoveChess moveData)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        var board = GetBoard();

        var moveFrom = board.GetSocket(moveData.fromIndex).socketE;
        var moveTo = board.GetSocket(moveData.toIndex).socketE;

        bool moved = TryMoveChess(moveFrom, moveTo, ecb, out bool killed);

        Debug.Log($"[Server] move chess: {moved}");
        ecb.Playback(EntityManager);

        if (moved && !requireTransformData.requireTransformation)
        {
            NextTurn();
        }
    }

    private void NextTurn()
    {
        GetBoard().NextTurn();
        RecalculateBoard();
    }


    private void RecalculateBoard()
    {
        ChessBoardInstanceAspect board = GetBoard();
        RecalculatePossibleStepsForBoard();
        bool isWhiteStep = board.IsWhiteStep();
        var king = board.GetCurrentKing();
        var currentPlayerPiecesIds = board.GetCurrentPlayerPiecesIds();

        var allPiecesSteps =
            new NativeList<NativeList<ChessPiecePossibleSteps>>(Allocator.Temp);
        int piecesLen = currentPlayerPiecesIds.Length;
        for (int i = 0; i < piecesLen; i++)
        {
            board = GetBoard();
            currentPlayerPiecesIds = board.GetCurrentPlayerPiecesIds();
            var pieceId = currentPlayerPiecesIds[i];
            var pieceE = board.GetPieceDataById(pieceId);
            var steps = new NativeList<ChessPiecePossibleSteps>(Allocator.Temp);
            var pieceC = SystemAPI.GetComponent<ChessPieceC>(pieceE);

            if (pieceC.isNotActive)
            {
                allPiecesSteps.Add(steps);
                continue;
            }

            var stepsBefore = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(pieceE).ToNativeArray(Allocator.Temp);
            var socketC = SystemAPI.GetComponent<ChessSocketC>(pieceE);

            foreach (var item1 in stepsBefore)
            {
                if (item1.isÑastling || item1.isTakeOfThePass)
                {
                    steps.Add(item1);
                    continue;
                }

                m_TempSnapshot = CalculateSnapshot(m_TempSnapshot);

                var ecb1 = new EntityCommandBuffer(Allocator.Temp);
                TryMoveChess(socketC.socketE, item1.defaultMoveTO.socketE, ecb1, out _, false);
                ecb1.Playback(EntityManager);

                RecalculatePossibleStepsForBoard();

                bool isKingUnderAttack = IsKingUnderAttack(king, out _, out _);

                if (!isKingUnderAttack && !IsGameFinished())
                {
                    steps.Add(item1);
                }

                var ecb2 = new EntityCommandBuffer(Allocator.Temp);
                ReturnBoardToSnapshot(m_TempSnapshot, false, ecb2);
                ecb2.Playback(EntityManager);

                RecalculatePossibleStepsForBoard();

               
            }

            allPiecesSteps.Add(steps);
        }

        RecalculatePossibleStepsForBoard();
        board = GetBoard();
        currentPlayerPiecesIds = board.GetCurrentPlayerPiecesIds();
        for (int i = 0; i < currentPlayerPiecesIds.Length; i++)
        {
            var pieceId = currentPlayerPiecesIds[i];
            var pieceE = board.GetPieceDataById(pieceId);

            if (SystemAPI.HasBuffer<ChessPiecePossibleSteps>(pieceE))
            {
                var buffer = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(pieceE);
                buffer.Clear();
                foreach (var item1 in allPiecesSteps[i])
                {
                    buffer.Add(item1);
                }
            }
        }

        if (IsGameFinished())
        {
            Debug.Log($"[Server] game ended");
            board = GetBoard();
            board.instanceC.ValueRW.blockInput = true;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var endGameE = ecb.CreateEntity();

            ecb.AddComponent<ExecuteEndGameC>(endGameE, new ExecuteEndGameC
            {
                isDraw = false,
                isWhiteWin = !isWhiteStep,
                winReason = WinReason.Win
            });

            ecb.Playback(EntityManager);
        }
        else
        {
            Debug.Log($"[Server] game continue");
        }
    }

    void ShowSelectedAndTurns(EntityCommandBuffer ecb)
    {
        Debug.Log("[Server] Clear selection");
        if (SystemAPI.HasComponent<ChessSocketC>(m_LastSelectedSocket))
        {
            var highlight = SystemAPI.GetAspect<ChessSocketHighlightAspect>(m_LastSelectedSocket);
            ecb.AddComponent<ChessSocketSelectedT>(m_LastSelectedSocket);
            highlight.ShowSelected(ecb);
        }

        if (HasSelectedPiece())
        {
            var steps = GetSelectedPossibleSteps();
            foreach (var turn in steps)
            {
                var highlight = SystemAPI.GetAspect<ChessSocketHighlightAspect>(turn.defaultMoveTO.socketE);

                if (turn.hasEnemy || turn.isÑastling)
                {
                    highlight.ShowEnemy(ecb);
                }
                else
                {
                    highlight.ShowMovePos(ecb);
                }
            }
        }
    }

    bool IsCorrectSocketToMove(Entity targetSocketE)
    {
        var turnForSelected = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(m_LastSelectedPieceE);

        foreach (var item in turnForSelected)
        {
            if (item.defaultMoveTO.socketE == targetSocketE)
            {
                return true;
            }
        }

        return false;
    }

    void ClearSelection(EntityCommandBuffer ecb)
    {
        Debug.Log("[Server] Clear selection");
        if (SystemAPI.HasComponent<ChessSocketSelectedT>(m_LastSelectedSocket))
        {
            ecb.RemoveComponent<ChessSocketSelectedT>(m_LastSelectedSocket);
            var asp = SystemAPI.GetAspect<ChessSocketHighlightAspect>(m_LastSelectedSocket);
            asp.DestoryHighlight(ecb);
        }

        var board = GetBoard();
        foreach (var item in board.boardSocketsB)
        {
            if (SystemAPI.HasComponent<ChessSocketHighlightInstanceC>(item.socketE))
            {
                var highlight = SystemAPI.GetAspect<ChessSocketHighlightAspect>(item.socketE);
                highlight.DestoryHighlight(ecb);
            }
        }

        m_LastSelectedSocket = Entity.Null;
        m_LastSelectedPieceE = Entity.Null;
        m_LastSelectedPieceMeshE = Entity.Null;
    }

    DynamicBuffer<ChessPiecePossibleSteps> GetSelectedPossibleSteps()
    {
        return GetPossibleSteps(m_LastSelectedPieceE);
    }

    public bool HasSelectedPiece()
    {
        return SystemAPI.HasBuffer<ChessPiecePossibleSteps>(m_LastSelectedPieceE);
    }

    public bool Raycast(float3 RayFrom, float3 RayTo, out Unity.Physics.RaycastHit hit)
    {
        EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp).WithAll<PhysicsWorldSingleton>();

        EntityQuery singletonQuery = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(builder);
        var collisionWorld = singletonQuery.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        singletonQuery.Dispose();

        RaycastInput input = new RaycastInput()
        {
            Start = RayFrom,
            End = RayTo,
            Filter = new CollisionFilter()
            {
                BelongsTo = ~0u,
                CollidesWith = ~0u, // all 1s, so all layers, collide with everything
                GroupIndex = 0
            }
        };

        bool haveHit = collisionWorld.CastRay(input, out hit);
        return haveHit;
    }

    bool RaycastSocket(float3 rayFrom, float3 rayTo, out Entity raycastedSocketE)
    {
        bool result = Raycast(rayFrom, rayTo, out var hit);
        raycastedSocketE = hit.Entity;
        if (result == false)
            return false;

        result = SystemAPI.HasComponent<ChessSocketC>(raycastedSocketE);

        Debug.Log($"[Server] do raycast has target {result} target {raycastedSocketE}");
        return result;
    }

    float3 lastMoveRaycastPos;
    void MoveOrSelect(out bool needMove, out MoveChess moveData)
    {
        moveData = new MoveChess { fromIndex = -1, toIndex = -1 };
        needMove = false;

        var grabQuery = SystemAPI.QueryBuilder().WithAll<GrabChessRpc>().Build();
        var moveQuery = SystemAPI.QueryBuilder().WithAll<MoveChessRpc>().Build();
        var dropQuery = SystemAPI.QueryBuilder().WithAll<DropChessRpc>().Build();

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        ecb.DestroyEntity(grabQuery, EntityQueryCaptureMode.AtPlayback);
        ecb.DestroyEntity(moveQuery, EntityQueryCaptureMode.AtPlayback);
        ecb.DestroyEntity(dropQuery, EntityQueryCaptureMode.AtPlayback);

        if (requireTransformData.requireTransformation)
            return;

        if (!grabQuery.IsEmpty)
        {
            var granRpcArray = grabQuery.ToComponentDataArray<GrabChessRpc>(Allocator.Temp);
            var grabRpc = granRpcArray[granRpcArray.Length - 1];

            if (RaycastSocket(grabRpc.rayFrom, grabRpc.rayTo, out Entity raycastedSocketE))
            {
                Debug.Log("[Server] raycasted select socket");
                var state = SystemAPI.GetSingleton<ChessBoardTurnC>();
                if (SystemAPI.HasComponent<ChessSocketC>(raycastedSocketE))
                {
                    if (HasPieceInSlot(raycastedSocketE))
                    {
                        var pieceId = SystemAPI.GetComponent<ChessSocketPieceIdC>(raycastedSocketE).pieceId;
                        var board = GetBoard();
                        var pieceE = board.GetPieceDataById(pieceId);
                        var pieceData = SystemAPI.GetComponent<ChessPieceC>(pieceE);
                        if (state.isWhite == pieceData.isWhite)
                        {
                            Debug.Log("[Server] select chess");

                            AudioManager.Instance.PlayRequest(SfxType.Select, ecb);
                            ClearSelection(ecb);

                            m_LastSelectedSocket = raycastedSocketE;
                            m_LastSelectedPieceE = pieceE;
                            m_LastSelectedPieceMeshE = board.GetPieceMeshById(pieceId);
                            ShowSelectedAndTurns(ecb);
                        }
                    }
                }
            }
        }

        bool hasSelectedSocket = SystemAPI.HasComponent<ChessSocketPieceIdC>(m_LastSelectedSocket);

        if (!moveQuery.IsEmpty)
        {
            var moveRpcArray = moveQuery.ToComponentDataArray<MoveChessRpc>(Allocator.Temp);
            var moveRpc = moveRpcArray[moveRpcArray.Length - 1];

            if (hasSelectedSocket)
            {
                if (Raycast(moveRpc.rayFrom, moveRpc.rayTo, out var hit))
                {
                    lastMoveRaycastPos = hit.Position;
                    var bouds = SystemAPI.GetComponent<ChessBoardBoundsC>(GetBoardEntity());

                    lastMoveRaycastPos = math.clamp(lastMoveRaycastPos, bouds.bounds.min, bouds.bounds.max);
                }

                var pieceLtw = SystemAPI.GetComponentRW<LocalTransform>(m_LastSelectedPieceMeshE);

                var hitPos = lastMoveRaycastPos;
                hitPos.y = 1f;

                pieceLtw.ValueRW.Position = hitPos;

            }
        }

        if (!dropQuery.IsEmpty)
        {
            if (hasSelectedSocket)
            {
                var dropRpcArray = dropQuery.ToComponentDataArray<DropChessRpc>(Allocator.Temp);
                var dropRpc = dropRpcArray[dropRpcArray.Length - 1];

                if (RaycastSocket(dropRpc.rayFrom, dropRpc.rayTo, out Entity targetSocket) &&
                    SystemAPI.HasComponent<ChessSocketC>(targetSocket) &&
                    IsCorrectSocketToMove(targetSocket))
                {
                    var board = GetBoard();
                    moveData = new MoveChess
                    {
                        fromIndex = board.IndexOf(m_LastSelectedSocket),
                        toIndex = board.IndexOf(targetSocket),
                    };
                    needMove = true;
                }
                else
                {
                    var pieceLtw = SystemAPI.GetComponentRW<LocalTransform>(m_LastSelectedPieceMeshE);
                    var socketLtw = SystemAPI.GetComponentRO<LocalTransform>(m_LastSelectedSocket);

                    pieceLtw.ValueRW.Position = socketLtw.ValueRO.Position;
                }

                ClearSelection(ecb);
            }
        }

        ecb.Playback(EntityManager);
    }

    bool IsGameFinished()
    {
        var board = GetBoard();

        bool isFinished = true;

        foreach (var piece in board.GetCurrentPlayerPiecesIds())
        {
            var pieceE = board.GetPieceDataById(piece);
            var pieceData = SystemAPI.GetComponent<ChessPieceC>(pieceE);

            var buffer = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(pieceE);

            if (buffer.Length > 0)
            {
                isFinished = false;
                break;
            }
        }

        return isFinished;
    }

    private void InitBoard()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (boardAsp, e) in SystemAPI.Query<ChessBoardInstanceAspect>().WithNone<ChessBoardStepsInitedT>().WithEntityAccess())
        {
            ecb.AddComponent<ChessBoardStepsInitedT>(e);
            RecalculatePossibleStepsForBoard(boardAsp);
        }
        ecb.Playback(EntityManager);
    }

    public bool IsKingUnderAttack(Entity kingE, out bool hasKnightAttacker, out int numberOfAttackes)
    {
        var board = GetBoard();

        var kingSocketE = SystemAPI.GetComponent<ChessSocketC>(kingE).socketE;

        NativeList<Entity> attackers = new NativeList<Entity>(Allocator.Temp);

        bool result = IsSocketUnderAttack(kingSocketE, board, attackers);
        hasKnightAttacker = false;
        foreach (var item in attackers)
        {
            if (SystemAPI.GetComponent<ChessPieceC>(item).chessType == ChessType.Knight)
            {
                hasKnightAttacker = true;
            }
        }

        numberOfAttackes = attackers.Length;

        return result;
    }

    bool IsSocketUnderAttack(Entity socket, ChessBoardInstanceAspect board)
    {
        NativeList<Entity> attackers = new NativeList<Entity>(Allocator.Temp);
        return IsSocketUnderAttack(socket, board, attackers);
    }

    bool IsSocketUnderAttack(Entity socket, ChessBoardInstanceAspect board, NativeList<Entity> numberOfAttackers)
    {
        NativeArray<int> attackes = board.GetOponentPiecesIds();

        foreach (var attackerId in attackes)
        {
            var attacker = board.GetPieceDataById(attackerId);

            if (!IsActive(attacker))
                continue;

            foreach (var turn1 in GetPossibleSteps(attacker))
            {
                if (turn1.defaultMoveTO.socketE == socket)
                {
                    numberOfAttackers.Add(attacker);
                }
            }
        }

        return numberOfAttackers.Length > 0;
    }

    private bool IsActive(Entity e)
    {
        if (!SystemAPI.HasComponent<ChessPieceC>(e))
            return false;

        return SystemAPI.GetComponent<ChessPieceC>(e).isNotActive == false;
    }

    Entity GetBoardEntity()
    {
        return SystemAPI.GetSingletonEntity<ChessBoardInstanceT>();
    }

    ChessBoardInstanceAspect GetBoard()
    {
        var boardE = SystemAPI.GetSingletonEntity<ChessBoardInstanceT>();
        return SystemAPI.GetAspect<ChessBoardInstanceAspect>(boardE);
    }

    ChessBoardPersistentAspect GetBoardPersistent()
    {
        var boardE = SystemAPI.GetSingletonEntity<ChessBoardPersistentC>();
        return SystemAPI.GetAspect<ChessBoardPersistentAspect>(boardE);
    }

    void RecalculatePossibleStepsForBoard()
    {
        var board = GetBoard();

        RecalculatePossibleStepsForBoard(board);
    }

    void RecalculatePossibleStepsForBoard(ChessBoardInstanceAspect board)
    {
        if (board.IsWhiteStep())
        {
            RecalculatePossibleStepsForBlack(board);
            RecalculatePossibleStepsForWhite(board);
        }
        else
        {
            RecalculatePossibleStepsForWhite(board);
            RecalculatePossibleStepsForBlack(board);
        }
    }

    void RecalculatePossibleSteps(ChessBoardInstanceAspect board, Entity king, NativeArray<int> piecesId)
    {
        bool isttackedByKnight = false;
        int attackers = 0;

        if (board.GetCurrentKing() == king)
        {
            IsKingUnderAttack(king, out isttackedByKnight, out attackers);
        }

        foreach (var pieceId in piecesId)
        {
            var piece = board.GetPieceDataById(pieceId);
            if (!IsActive(piece))
                continue;

            RecalculatePossibleTurnsForPiece(piece, board, attackers, isttackedByKnight, king == piece);
        }
    }

    void RecalculatePossibleStepsForBlack(ChessBoardInstanceAspect board)
    {
        RecalculatePossibleSteps(board, board.GetBlackKing(), board.GetBlackPiecesIds());
    }

    void RecalculatePossibleStepsForWhite(ChessBoardInstanceAspect board)
    {
        RecalculatePossibleSteps(board, board.GetWhiteKing(), board.GetWhitePiecesIds());
    }

    private void MovePieceFromToSocketWithChatMessage(Entity fromSocket, Entity toSocket, EntityCommandBuffer ecb)
    {
        string message = "[Sys] ";
        var board = GetBoard();
        var peice = SystemAPI.GetComponent<ChessSocketPieceIdC>(fromSocket);
        var peiceData = SystemAPI.GetComponentRW<ChessPieceC>(board.GetPieceDataById(peice.pieceId));

        string color = peiceData.ValueRO.isWhite ? "white" : "black";

        SystemAPI.GetAspect<ChessSocketHighlightAspect>(fromSocket).ShowPrevMove(ecb);
        SystemAPI.GetAspect<ChessSocketHighlightAspect>(toSocket).ShowPrevMove(ecb);

        board.GetSocketPosition(fromSocket, out int x, out int y);
        board.GetSocketPosition(toSocket, out int x1, out int y1);

        message += $"{color} {peiceData.ValueRO.chessType} {BoardPositions.horizontal[x]}{BoardPositions.vertical[y]}" +
            $" -> {BoardPositions.horizontal[x1]}{BoardPositions.vertical[y1]}";
        ChatWindow.Instance.RequestText(message, ecb);

        MovePieceFromToSocketReal(fromSocket, toSocket);
    }

    private bool MovePieceFromToSocketTemp(Entity fromSocket, Entity toSocket)
    {
        return MovePieceToSocketData(fromSocket, toSocket, out _);
    }

    private bool MovePieceFromToSocketReal(Entity fromSocket, Entity toSocket)
    {
        var result = MovePieceToSocketData(fromSocket, toSocket, out var prevMoveDataReal);
        m_PrevRealMoves.Add(prevMoveDataReal);
        return result;
    }

    private bool MovePieceToSocketData(Entity fromSocket, Entity toSocket, out PrevMove saveData)
    {
        var pieceLinkFrom = SystemAPI.GetComponent<ChessSocketPieceIdC>(fromSocket);
        var pieceLinkTo = SystemAPI.GetComponent<ChessSocketPieceIdC>(toSocket);

        var board = GetBoard();

        var pieceMeshFromE = board.GetPieceMeshById(pieceLinkFrom.pieceId);
        var pieceDataFromE = board.GetPieceDataById(pieceLinkFrom.pieceId);

        var pieceDataToE = board.GetPieceDataById(pieceLinkTo.pieceId);

        saveData = new PrevMove
        {
            from = fromSocket,
            to = toSocket,
        };

        if (SystemAPI.HasComponent<ChessPieceC>(pieceDataToE))
        {
            var pieceDataTo = SystemAPI.GetComponentRW<ChessPieceC>(pieceDataToE);
            pieceDataTo.ValueRW.isNotActive = true;
        }

        SystemAPI.SetComponent<ChessSocketPieceIdC>(fromSocket, new ChessSocketPieceIdC
        {
            pieceId = -1
        });

        SystemAPI.SetComponent<ChessSocketPieceIdC>(toSocket, pieceLinkFrom);

        var socketData = SystemAPI.GetComponent<ChessSocketC>(toSocket);
        SystemAPI.SetComponent<ChessSocketC>(pieceDataFromE, socketData);
        var data = SystemAPI.GetComponentRW<ChessPieceC>(pieceDataFromE);
        data.ValueRW.isMovedOnce = true;
        data.ValueRW.numberOfMoves += 1;

        var ltw = SystemAPI.GetComponentRW<LocalTransform>(pieceMeshFromE);
        ltw.ValueRW.Position = SystemAPI.GetComponent<LocalTransform>(toSocket).Position;
        //Debug.Log($"move from {prevMoveData.fromPos} to {prevMoveData.toPos}");

        return true;
    }

    bool IsCorrectSocketToMove(Entity moveFrom, Entity moveTo)
    {
        var pieceId = SystemAPI.GetComponent<ChessSocketPieceIdC>(moveFrom).pieceId;
        var piece = GetBoard().GetPieceDataById(pieceId);
        var turnForSelected = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(piece);

        foreach (var item in turnForSelected)
        {
            if (item.defaultMoveTO.socketE == moveTo)
            {
                return true;
            }
        }

        return false;
    }

    private bool GetCurrentPlayerEntity(out Entity currentPlayer)
    {
        currentPlayer = Entity.Null;

        var turn = SystemAPI.GetSingleton<ChessBoardTurnC>();
        foreach (var (player, e) in SystemAPI.Query<ChessPlayerC>().WithAll<NetworkId>().WithEntityAccess())
        {
            if (turn.isWhite == player.isWhite)
            {
                currentPlayer = e;
                return true;
            }
        }

        return false;
    }

    private bool GetOponentEntity(out Entity oponent)
    {
        oponent = Entity.Null;

        var turn = SystemAPI.GetSingleton<ChessBoardTurnC>();
        foreach (var (player, e) in SystemAPI.Query<ChessPlayerC>().WithAll<NetworkId>().WithEntityAccess())
        {
            if (turn.isWhite != player.isWhite)
            {
                oponent = e;
                return true;
            }
        }

        return false;
    }

    public bool DestoryPieceFromSocket(Entity moveToSocket, EntityCommandBuffer ecb, bool isRealMove)
    {
        bool killed = false;

        if (HasPieceInSlot(moveToSocket))
        {          
            killed = true;

            var socketPieceData = SystemAPI.GetComponentRW<ChessSocketPieceIdC>(moveToSocket);
            var board = GetBoard();
            var pieceMeshE = board.GetPieceMeshById(socketPieceData.ValueRO.pieceId);
            var pieceDataE = board.GetPieceDataById(socketPieceData.ValueRO.pieceId);

            var ltwRW = SystemAPI.GetComponentRW<LocalTransform>(pieceMeshE);
            ltwRW.ValueRW.Position = new float3(10000, 10000, 0);

            socketPieceData.ValueRW.Reset();

            var pieceToDestory = SystemAPI.GetComponentRW<ChessPieceC>(pieceDataE);
            pieceToDestory.ValueRW.isNotActive = true;

            if (isRealMove)
            {
                DestoryPieceFeedback(moveToSocket, pieceToDestory.ValueRO, ecb);
            } 
        }

        return killed;
    }

    void DestoryPieceFeedback(Entity moveToSocket, ChessPieceC pieceToDestory, EntityCommandBuffer ecb)
    {
        var board = GetBoard();

        AudioManager.Instance.PlayRequest(SfxType.Kill, ecb);
        PlayParticle.Instance.PlayRequest(SystemAPI.GetComponent<LocalTransform>(moveToSocket).Position, ParticleType.Kill, ecb);
        board.killedPieces.Add(new KilledPieces
        {
            chessType = pieceToDestory.chessType,
            isWhite = pieceToDestory.isWhite,
        });
        var updatePiecesViewE = ecb.CreateEntity();
        ecb.AddComponent<SendRpcCommandRequest>(updatePiecesViewE);
        ecb.AddComponent<AddKilledPiecesRPC>(updatePiecesViewE, new AddKilledPiecesRPC
        {
            data = pieceToDestory
        });

        if (GetOponentEntity(out Entity oponent))
        {
            var shakeEntity = ecb.CreateEntity();
            ecb.AddComponent<ShakeCameraRpc>(shakeEntity);
            ecb.AddComponent<SendRpcCommandRequest>(shakeEntity, new SendRpcCommandRequest
            {
                TargetConnection = oponent
            });
        }
    }

    bool TryMoveChess(Entity moveFromSocket, Entity moveToSocket, EntityCommandBuffer ecb, out bool killed, bool isRealMove = true)
    {
        killed = false;
        if (!IsCorrectSocketToMove(moveFromSocket, moveToSocket))
        {
            return false;
        }
        var board = GetBoard();

        var moveFromPieceId = SystemAPI.GetComponent<ChessSocketPieceIdC>(moveFromSocket).pieceId;
        var moveFromPieceE = board.GetPieceDataById(moveFromPieceId);
        var steps = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(moveFromPieceE);
        ChessPiecePossibleSteps step = new ChessPiecePossibleSteps();

        foreach (var item in steps)
        {
            if (item.defaultMoveTO.socketE == moveToSocket)
                step = item;
        }

        if (isRealMove)
        {
            foreach (var item in m_PrevRealMoves)
            {
                SystemAPI.GetAspect<ChessSocketHighlightAspect>(item.from).DestoryPrevMove(ecb);
                SystemAPI.GetAspect<ChessSocketHighlightAspect>(item.to).DestoryPrevMove(ecb);
            }
        }

        if (step.isÑastling)
        {
            if (isRealMove)
            {
                AudioManager.Instance.PlayRequest(SfxType.Move, ecb);

                MovePieceFromToSocketWithChatMessage(moveFromSocket, step.castlingMove.kingMoveTo.socketE, ecb);
                MovePieceFromToSocketWithChatMessage(moveToSocket, step.castlingMove.rookMoveTo.socketE, ecb);
            }
            else
            {
                MovePieceFromToSocketTemp(moveFromSocket, step.castlingMove.kingMoveTo.socketE);
                MovePieceFromToSocketTemp(moveToSocket, step.castlingMove.rookMoveTo.socketE);
            }
        }
        else if (step.isTakeOfThePass)
        {
            if (isRealMove)
            {
                MovePieceFromToSocketWithChatMessage(moveFromSocket, step.TakeOfThePassData.moveToSocket.socketE, ecb);

                killed = DestoryPieceFromSocket(step.TakeOfThePassData.destoryPieceSocket.socketE, ecb, isRealMove);

                if (isRealMove && !killed)
                {
                    AudioManager.Instance.PlayRequest(SfxType.Move, ecb);
                }
            }
            else 
            {
                MovePieceFromToSocketTemp(moveFromSocket, step.TakeOfThePassData.moveToSocket.socketE);
                DestoryPieceFromSocket(step.TakeOfThePassData.destoryPieceSocket.socketE, ecb, isRealMove);
            }
        }
        else
        {
            killed = DestoryPieceFromSocket(moveToSocket, ecb, isRealMove);

            if (isRealMove && !killed)
            {
                Debug.Log("Not killed");
                AudioManager.Instance.PlayRequest(SfxType.Move, ecb);
            }            

            var pieceId = SystemAPI.GetComponent<ChessSocketPieceIdC>(moveFromSocket);
            var pieceE = board.GetPieceDataById(pieceId.pieceId);
            var pieceData = SystemAPI.GetComponent<ChessPieceC>(pieceE);

            var boardAspect = GetBoard();

            //pawn promotion
            if (pieceData.chessType == ChessType.Pawn)
            {
                if (isRealMove)
                {
                    MovePieceFromToSocketWithChatMessage(moveFromSocket, moveToSocket, ecb);
                }
                else
                {
                    MovePieceFromToSocketTemp(moveFromSocket, moveToSocket);
                }
             
                var isWhite = pieceData.isWhite;

                if (boardAspect.IsBoardEnd(isWhite, boardAspect.IndexOf(moveToSocket)))
                {
                    requireTransformData = new SavedPieceTransformationData
                    {
                        isWhite = isWhite,
                        pieceId = pieceId.pieceId,
                        requireTransformation = true,
                        socket = moveToSocket
                    };

                    if (GetCurrentPlayerEntity(out Entity currentPlayer))
                    {
                        var ecb1 = new EntityCommandBuffer(Allocator.Temp);
                        var requestE = ecb1.CreateEntity();
                        ecb1.AddComponent<SendRpcCommandRequest>(requestE, new SendRpcCommandRequest
                        {
                            TargetConnection = currentPlayer
                        });
                        ecb1.AddComponent<ShowPieceTransformationUIRpc>(requestE, new ShowPieceTransformationUIRpc
                        {
                            isWhite = SystemAPI.GetComponent<ChessPlayerC>(currentPlayer).isWhite
                        });
                        ecb1.Playback(EntityManager);
                    }
                }
            }
            else
            {
                if (isRealMove)
                {
                    MovePieceFromToSocketWithChatMessage(moveFromSocket, moveToSocket, ecb);
                }
                else 
                {
                    MovePieceFromToSocketTemp(moveFromSocket, moveToSocket);
                }
            }
        }
        return true;
    }

    private void TransformPiece(Entity moveToSocket, EntityCommandBuffer ecb, int pieceId, bool isWhite, PieceTransformType transfType)
    {
        var prefabs = SystemAPI.GetSingleton<ChessBoardPersistentC>();

        var queenPrefabs = isWhite == true ?
            prefabs.whitePiecesMeshPrefabs :
           prefabs.blackPiecesMeshPrefabs;
        Entity newPieceMeshPrefab = queenPrefabs.queen;
        ChessType chessType = ChessType.Queen;
        switch (transfType)
        {
            case PieceTransformType.Queen:
                newPieceMeshPrefab = queenPrefabs.queen;
                chessType = ChessType.Queen;
                break;
            case PieceTransformType.Rook:
                newPieceMeshPrefab = queenPrefabs.rook;
                chessType = ChessType.Rook;

                break;
            case PieceTransformType.Bishop:
                newPieceMeshPrefab = queenPrefabs.bishop;
                chessType = ChessType.Bishop;

                break;
            case PieceTransformType.Knight:
                newPieceMeshPrefab = queenPrefabs.knight;
                chessType = ChessType.Knight;

                break;
            default:
                break;
        }

        var board = GetBoard();

        var meshE = board.GetPieceMeshById(pieceId);
        ecb.DestroyEntity(meshE);

        var pieceDataE = board.GetPieceDataById(pieceId);
        var pieceData = SystemAPI.GetComponentRW<ChessPieceC>(pieceDataE);
        pieceData.ValueRW.chessType = chessType;

        var newMeshInstance = ecb.Instantiate(newPieceMeshPrefab);

        var buffer = ecb.SetBuffer<ChessBoardAllPiecesMeshes>(board.Entity);
        var meshes = board.allPiecesMeshesB;
        buffer.CopyFrom(meshes);
        buffer[pieceId] = new ChessBoardAllPiecesMeshes
        {
            meshPieceE = newMeshInstance
        };

        var socketTrans = SystemAPI.GetComponent<LocalTransform>(moveToSocket);

        ecb.SetComponent<LocalTransform>(newMeshInstance, socketTrans);

        ecb.AddComponent<ChessSocketPieceIdC>(moveToSocket, new ChessSocketPieceIdC
        {
            pieceId = pieceId
        });

        PlayParticle.Instance.PlayRequest(socketTrans.Position, ParticleType.Kill, ecb);
    }

    void LoopMove(
        int x,
        int y,
        int offsetX,
        int offsetY,
        ChessBoardInstanceAspect boardAspect,
        DynamicBuffer<ChessPiecePossibleSteps> chessTurnPositions,
        bool isWhite
        )
    {
        while (true)
        {
            x += offsetX;
            y += offsetY;

            bool hasTurn = TryAddTurn(x, y, true, true, boardAspect, chessTurnPositions, isWhite, out bool hasEnemy);

            if (hasEnemy)
                break;

            if (!hasTurn)
                break;
        }
    }

    bool HasPossibleSteps(Entity pieceE)
    {
        return SystemAPI.HasBuffer<ChessPiecePossibleSteps>(pieceE);
    }

    DynamicBuffer<ChessPiecePossibleSteps> GetPossibleSteps(Entity pieceE)
    {
        return SystemAPI.GetBuffer<ChessPiecePossibleSteps>(pieceE);
    }

    private void FindTakeOfThePassPaw(int x, int y, ChessBoardInstanceAspect boardAspect, DynamicBuffer<ChessPiecePossibleSteps> turnPositions, int forwardOffset)
    {
        if (IsValidXY(x, y))
        {
            var socket = boardAspect.GetSocket(x, y);

            if (!HasPieceInSlot(socket.socketE))
                return;

            var pieceLink = SystemAPI.GetComponent<ChessSocketPieceIdC>(socket.socketE);


            var piece = GetPieceDataFromSlot(socket.socketE);
            if (piece.numberOfMoves == 1)
            {
                var pawDataE = boardAspect.GetPieceDataById(pieceLink.pieceId);

                var moveToSocket = boardAspect.GetSocket(x, y + forwardOffset);
                turnPositions.Add(new ChessPiecePossibleSteps
                {
                    hasEnemy = true,
                    isTakeOfThePass = true,
                    TakeOfThePassData = new TakeOfThePassData
                    {
                        moveToSocket = SystemAPI.GetComponent<ChessSocketC>(moveToSocket.socketE),
                        destoryPieceSocket = SystemAPI.GetComponent<ChessSocketC>(pawDataE)
                    },
                    defaultMoveTO = SystemAPI.GetComponent<ChessSocketC>(moveToSocket.socketE)
                });
            }
        }
    }

    void RecalculatePossibleTurnsForPiece(
        Entity pieceToMoveE,
        ChessBoardInstanceAspect boardAspect,
        int attackes,
        bool isAttackedByKnight,
        bool isKing)
    {
        if (!SystemAPI.HasComponent<ChessSocketC>(pieceToMoveE))
            return;

        var socketFromMoveC = SystemAPI.GetComponent<ChessSocketC>(pieceToMoveE);
        var pieceDataToMove = SystemAPI.GetComponentRW<ChessPieceC>(pieceToMoveE);

        bool isWhite = pieceDataToMove.ValueRO.isWhite;

        var turnPositions = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(pieceToMoveE);
        turnPositions.Clear();

        if (pieceDataToMove.ValueRO.isNotActive)
        {
            return;
        }

        if (!isKing && attackes >= 2 || !isKing && isAttackedByKnight)
            return;

        switch (pieceDataToMove.ValueRO.chessType)
        {
            case ChessType.Pawn:
                int offset = -1;
                if (isWhite)
                {
                    offset = 1;
                }

                int x = socketFromMoveC.x;
                int y = socketFromMoveC.y + offset;

                TryAddTurn(x + 1, y, true, false, boardAspect, turnPositions, isWhite, out bool hasEnemy1);
                TryAddTurn(x - 1, y, true, false, boardAspect, turnPositions, isWhite, out bool hasEnemy2);

                if (isWhite && socketFromMoveC.y == 4 || !isWhite && socketFromMoveC.y == 3)
                {
                    FindTakeOfThePassPaw(socketFromMoveC.x + 1, socketFromMoveC.y, boardAspect, turnPositions, offset);
                    FindTakeOfThePassPaw(socketFromMoveC.x - 1, socketFromMoveC.y, boardAspect, turnPositions, offset);
                }

                if (turnPositions.Length == 0)
                {
                    x = socketFromMoveC.x;
                    y = socketFromMoveC.y + offset;

                    if (TryAddTurn(x, y, false, true, boardAspect, turnPositions, isWhite, out bool hasEnemy))
                    {
                        if (!hasEnemy && !pieceDataToMove.ValueRO.isMovedOnce)
                        {
                            y += offset;
                            TryAddTurn(x, y, false, true, boardAspect, turnPositions, isWhite, out hasEnemy);
                        }
                    }
                }

                break;
            case ChessType.Bishop:
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, 1, 1, boardAspect, turnPositions, isWhite);
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, -1, -1, boardAspect, turnPositions, isWhite);
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, -1, 1, boardAspect, turnPositions, isWhite);
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, 1, -1, boardAspect, turnPositions, isWhite);
                break;
            case ChessType.Rook:
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, 1, 0, boardAspect, turnPositions, isWhite);
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, -1, 0, boardAspect, turnPositions, isWhite);
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, 0, 1, boardAspect, turnPositions, isWhite);
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, 0, -1, boardAspect, turnPositions, isWhite);
                break;
            case ChessType.Knight:

                TryAddTurn(socketFromMoveC.x + 2, socketFromMoveC.y + 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(socketFromMoveC.x + 2, socketFromMoveC.y - 1, true, true, boardAspect, turnPositions, isWhite, out bool _);

                TryAddTurn(socketFromMoveC.x - 2, socketFromMoveC.y + 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(socketFromMoveC.x - 2, socketFromMoveC.y - 1, true, true, boardAspect, turnPositions, isWhite, out bool _);

                TryAddTurn(socketFromMoveC.x - 1, socketFromMoveC.y + 2, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(socketFromMoveC.x + 1, socketFromMoveC.y + 2, true, true, boardAspect, turnPositions, isWhite, out bool _);

                TryAddTurn(socketFromMoveC.x + 1, socketFromMoveC.y - 2, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(socketFromMoveC.x - 1, socketFromMoveC.y - 2, true, true, boardAspect, turnPositions, isWhite, out bool _);

                break;
            case ChessType.Queen:
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, 1, 0, boardAspect, turnPositions, isWhite);
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, -1, 0, boardAspect, turnPositions, isWhite);
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, 0, 1, boardAspect, turnPositions, isWhite);
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, 0, -1, boardAspect, turnPositions, isWhite);
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, 1, 1, boardAspect, turnPositions, isWhite);
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, -1, -1, boardAspect, turnPositions, isWhite);
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, -1, 1, boardAspect, turnPositions, isWhite);
                LoopMove(socketFromMoveC.x, socketFromMoveC.y, 1, -1, boardAspect, turnPositions, isWhite);

                break;
            case ChessType.King:
                x = socketFromMoveC.x;
                y = socketFromMoveC.y;

                TryAddTurn(x + 1, y, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x - 1, y, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x + 1, y + 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x - 1, y - 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x, y + 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x, y - 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x - 1, y + 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x + 1, y - 1, true, true, boardAspect, turnPositions, isWhite, out bool _);

                TryAddCaslingSteps(pieceToMoveE, socketFromMoveC, boardAspect, turnPositions, isWhite);
                break;

            default:
                break;
        }
    }

    void TryAddCaslingSteps(
        Entity kingE,
        ChessSocketC socketC,
        ChessBoardInstanceAspect boardAspect,
        DynamicBuffer<ChessPiecePossibleSteps> turnPositions,
        bool isWhite)
    {
        var kingData = SystemAPI.GetComponent<ChessPieceC>(kingE);

        if (kingData.isMovedOnce)
            return;

        var piecesIds = boardAspect.GetCurrentPlayerPiecesIds();

        bool isKingUnderAttack = IsSocketUnderAttack(socketC.socketE, boardAspect);

        if (isKingUnderAttack)
            return;

        NativeList<Entity> rooks = new NativeList<Entity>(Allocator.Temp);

        foreach (var pieceId in piecesIds)
        {
            var pieceE = boardAspect.GetPieceDataById(pieceId);

            var pieceData = SystemAPI.GetComponent<ChessPieceC>(pieceE);

            if (pieceData.isNotActive)
                continue;

            var pieceSocket = SystemAPI.GetComponent<ChessSocketC>(pieceE);

            if (
                pieceData.chessType == ChessType.Rook &&
                !pieceData.isMovedOnce &&
                !IsSocketUnderAttack(pieceSocket.socketE, boardAspect))
            {
                rooks.Add(pieceE);
            }
        }

        foreach (var rook in rooks)
        {
            var rookSocketData = SystemAPI.GetComponent<ChessSocketC>(rook);
            var rookSocketSocketE = rookSocketData.socketE;

            var kingSocket = SystemAPI.GetComponent<ChessSocketC>(kingE);

            NativeList<Entity> checkSockets = new NativeList<Entity>(Allocator.Temp);

            if (kingSocket.y == 0 && rookSocketData.y == 0 && isWhite ||
                kingSocket.y == ChessBoardInstanceAspect.GRID_Y - 1 && rookSocketData.y == ChessBoardInstanceAspect.GRID_Y - 1 && !isWhite)
            {
                bool isRookLeft = kingSocket.x > rookSocketData.x;

                int offset = isRookLeft ? -1 : 1;

                int currentX = kingSocket.x + offset;

                bool hasAttackedSocket = false;
                // check all sockets bettwen not attacked
                while (currentX != rookSocketData.x)
                {
                    var socket = boardAspect.GetSocket(currentX, kingSocket.y).socketE;
                    if (HasPieceInSlot(socket) || IsSocketUnderAttack(socket, boardAspect))
                    {
                        hasAttackedSocket = true;
                        break;
                    }
                    currentX += offset;
                }

                if (hasAttackedSocket)
                    continue;

                var rookMoveToE = boardAspect.GetSocket(kingSocket.x + offset, kingSocket.y).socketE;
                var kingMoveToE = boardAspect.GetSocket(kingSocket.x + offset * 2, kingSocket.y).socketE;

                turnPositions.Add(new ChessPiecePossibleSteps
                {
                    isÑastling = true,
                    defaultMoveTO = rookSocketData,
                    hasEnemy = false,
                    castlingMove = new ÑastlingData
                    {
                        kingMoveTo = SystemAPI.GetComponent<ChessSocketC>(kingMoveToE),
                        rookMoveTo = SystemAPI.GetComponent<ChessSocketC>(rookMoveToE)
                    }
                });
            }
        }
    }

    bool IsValidXY(int x, int y)
    {
        if (x < 0 || x >= ChessBoardInstanceAspect.GRID_X)
        {
            return false;
        }

        if (y < 0 || y >= ChessBoardInstanceAspect.GRID_Y)
        {
            return false;
        }

        return true;
    }

    private bool TryAddTurn(
        int x,
        int y,
        bool canBeatEnemy,
        bool canMoveToEmpty,
        ChessBoardInstanceAspect boardAspect,
        DynamicBuffer<ChessPiecePossibleSteps> turns,
        bool isWhiteSource,
        out bool hasEnemy)
    {
        hasEnemy = false;

        if (!IsValidXY(x, y))
            return false;

        var targetSocket = boardAspect.GetSocket(x, y);

        bool isCanMove = false;

        bool hasPieceInSlot = HasPieceInSlot(targetSocket.socketE);
        if (hasPieceInSlot)
        {
            var data = GetPieceDataFromSlot(targetSocket.socketE);
            bool isWhiteTarget = data.isWhite;
            if (canBeatEnemy && isWhiteTarget != isWhiteSource)
            {
                isCanMove = true;
                hasEnemy = true;
            }
        }
        else if (canMoveToEmpty)
        {
            isCanMove = true;
        }

        if (isCanMove)
        {
            turns.Add(new ChessPiecePossibleSteps
            {
                hasEnemy = hasEnemy,
                defaultMoveTO = new ChessSocketC
                {
                    x = x,
                    y = y,
                    socketE = targetSocket.socketE
                }
            });
        }

        return isCanMove;
    }

    private ChessPieceC GetPieceDataFromSlot(Entity slot)
    {
        var board = GetBoard();
        var pieceId = SystemAPI.GetComponent<ChessSocketPieceIdC>(slot).pieceId;
        var pieceE = board.GetPieceDataById(pieceId);
        return SystemAPI.GetComponent<ChessPieceC>(pieceE);
    }

    private bool HasPieceInSlot(Entity e)
    {
        return SystemAPI.GetComponentRO<ChessSocketPieceIdC>(e).ValueRO.pieceId != -1;
    }
}