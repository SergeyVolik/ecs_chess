using System.Collections.Generic;
using System.Net.Sockets;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct EndGameRPC : IRpcCommand
{
    public bool isWhiteWin;
}


[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class PlayerTurnServerSystem : SystemBase
{
    public struct MoveChess : IComponentData
    {
        public int fromIndex;
        public int toIndex;
    }

    public struct SavedPieceTransformationData
    {
        public Entity socket;
        public Entity pieceE;
        public bool isWhite;
        public bool requireTransformation;
    }

    private Entity m_LastSelectedSocket;
    private Entity m_LastSelectedPieceE;


    public PrevMoveData prevMoveDataTemp;

    public NativeList<PrevMoveData> m_PrevRealMoves;

    public SavedPieceTransformationData requireTransformData;
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<ChessBoardInstanceT>();
        m_PrevRealMoves = new NativeList<PrevMoveData>(Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
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
                TransformPiece(requireTransformData.socket, ecb, requireTransformData.pieceE, requireTransformData.isWhite, rpc.type);
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
        var pieces = board.GetCurrentPlayerPieces();

        var allPiecesSteps =
            new NativeList<NativeList<ChessPiecePossibleSteps>>(Allocator.Temp);

        for (int i = 0; i < pieces.Length; i++)
        {
            var pieceE = pieces[i];
            var steps = new NativeList<ChessPiecePossibleSteps>(Allocator.Temp);
            if (SystemAPI.HasBuffer<ChessPiecePossibleSteps>(pieceE))
            {
                var stepsBefore = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(pieceE).ToNativeArray(Allocator.Temp);
                var socketC = SystemAPI.GetComponent<ChessSocketC>(pieceE);

                foreach (var item1 in stepsBefore)
                {
                    if (item1.isÑastling)
                    {
                        steps.Add(item1);
                        continue;
                    }

                    MovePieceFromToSocketTemp(socketC.socketE, item1.socketC.socketE);
                    RecalculatePossibleStepsForBoard(board);

                    bool isKingUnderAttack = IsKingUnderAttack(king, out _, out _);

                    if (!isKingUnderAttack && !IsGameFinished())
                    {
                        steps.Add(item1);
                    }

                    ResetPrevMoveData();
                    RecalculatePossibleStepsForBoard(board);

                }
            }

            allPiecesSteps.Add(steps);
        }

        RecalculatePossibleStepsForBoard(board);

        for (int i = 0; i < pieces.Length; i++)
        {
            var item = pieces[i];
            if (SystemAPI.HasBuffer<ChessPiecePossibleSteps>(item))
            {
                var buffer = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(item);
                buffer.Clear();
                foreach (var item1 in allPiecesSteps[i])
                {
                    buffer.Add(item1);
                }
            }
        }

        if (IsGameFinished())
        {
            board = GetBoard();
            board.instanceC.ValueRW.blockInput = true;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var request = EntityManager.CreateEntity();
            ecb.AddComponent<SendRpcCommandRequest>(request);
            ecb.AddComponent<EndGameRPC>(request, new EndGameRPC
            {
                isWhiteWin = !isWhiteStep,
            });
            ecb.Playback(EntityManager);
            Debug.Log($"[Server] winner white:{!isWhiteStep}");
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
                var highlight = SystemAPI.GetAspect<ChessSocketHighlightAspect>(turn.socketC.socketE);

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
            if (item.socketC.socketE == targetSocketE)
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
                        var pieceE = SystemAPI.GetComponent<ChessSocketPieceLinkC>(raycastedSocketE).pieceE;
                        var pieceData = SystemAPI.GetComponent<ChessPieceC>(pieceE);
                        if (state.isWhite == pieceData.isWhite)
                        {
                            Debug.Log("[Server] select chess");

                            AudioManager.Instance.PlayRequest(SfxType.Select, ecb);
                            ClearSelection(ecb);

                            m_LastSelectedSocket = raycastedSocketE;
                            m_LastSelectedPieceE = pieceE;

                            ShowSelectedAndTurns(ecb);
                        }
                    }
                }
            }
        }

        bool hasSelectedSocket = SystemAPI.HasComponent<ChessSocketPieceLinkC>(m_LastSelectedSocket);

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

                var pieceLtw = SystemAPI.GetComponentRW<LocalTransform>(m_LastSelectedPieceE);

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
                    var pieceLtw = SystemAPI.GetComponentRW<LocalTransform>(m_LastSelectedPieceE);
                    pieceLtw = SystemAPI.GetComponentRW<LocalTransform>(m_LastSelectedPieceE);
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

        foreach (var piece in board.GetCurrentPlayerPieces())
        {
            if (!SystemAPI.HasBuffer<ChessPiecePossibleSteps>(piece))
                continue;

            var buffer = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(piece);

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
        NativeArray<Entity> attackes = board.GetOponentPieces();

        foreach (var attacker in attackes)
        {
            if (!HasPossibleSteps(attacker))
                continue;

            if (!IsActive(attacker))
                continue;

            foreach (var turn1 in GetPossibleSteps(attacker))
            {
                if (turn1.socketC.socketE == socket)
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

        return SystemAPI.GetComponent<ChessPieceC>(e).notActive == false;
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

    void RecalculatePossibleStepsForBoard()
    {
        var board = GetBoard();

        RecalculatePossibleStepsForBoard(board);
    }

    void RecalculatePossibleStepsForOponent(ChessBoardInstanceAspect board)
    {
        if (board.IsWhiteStep())
        {
            RecalculatePossibleStepsForBlack(board);
        }
        else
        {
            RecalculatePossibleStepsForWhite(board);
        }
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

    void RecalculatePossibleSteps(ChessBoardInstanceAspect board, Entity king, NativeArray<Entity> pieces)
    {
        bool isttackedByKnight = false;
        int attackers = 0;

        if (board.GetCurrentKing() == king)
        {
            IsKingUnderAttack(king, out isttackedByKnight, out attackers);
        }

        foreach (var piece in pieces)
        {
            if (!IsActive(piece))
                continue;

            RecalculatePossibleTurnsForPiece(piece, board, attackers, isttackedByKnight, king == piece);
        }
    }

    void RecalculatePossibleStepsForBlack(ChessBoardInstanceAspect board)
    {
        RecalculatePossibleSteps(board, board.GetBlackKing(), board.GetBlackPieces());
    }

    void RecalculatePossibleStepsForWhite(ChessBoardInstanceAspect board)
    {
        RecalculatePossibleSteps(board, board.GetWhiteKing(), board.GetWhitePieces());
    }

    private void MovePieceFromToSocketWithChatMessage(Entity fromSocket, Entity toSocket, EntityCommandBuffer ecb)
    {
        string message = "[Sys] ";
        var board = GetBoard();
        var peice = SystemAPI.GetComponent<ChessSocketPieceLinkC>(fromSocket);
        var peiceData = SystemAPI.GetComponent<ChessPieceC>(peice.pieceE);

        string color = peiceData.isWhite ? "white" : "black";

        SystemAPI.GetAspect<ChessSocketHighlightAspect>(fromSocket).ShowPrevMove(ecb);
        SystemAPI.GetAspect<ChessSocketHighlightAspect>(toSocket).ShowPrevMove(ecb);

        board.GetSocketPosition(fromSocket, out int x, out int y);
        board.GetSocketPosition(toSocket, out int x1, out int y1);

        message += $"{color} {peiceData.chessType} {BoardPositions.horizontal[x]}{BoardPositions.vertical[y]}" +
            $" -> {BoardPositions.horizontal[x1]}{BoardPositions.vertical[y1]}";
        ChatWindow.Instance.RequestText(message, ecb);

        MovePieceFromToSocketReal(fromSocket, toSocket);
    }

    private bool MovePieceFromToSocketTemp(Entity fromSocket, Entity toSocket)
    {
        return MovePieceToSocketData(fromSocket, toSocket, out prevMoveDataTemp);
    }

    private bool MovePieceFromToSocketReal(Entity fromSocket, Entity toSocket)
    {
        var result = MovePieceToSocketData(fromSocket, toSocket, out var prevMoveDataReal);
        m_PrevRealMoves.Add(prevMoveDataReal);
        return result;
    }

    public struct PrevMoveData
    {
        public Entity from;
        public Entity to;

        public bool isValid;

        public ChessSocketPieceLinkC pieceLinkFrom;
        public ChessPieceC pieceDataFrom;
        public ChessSocketC socketFrom;

        public ChessSocketPieceLinkC pieceLinkTo;
        public ChessPieceC pieceDataTo;
        public ChessSocketC socketTo;

        public float3 fromPos;
        public float3 toPos;
    }

    private bool MovePieceToSocketData(Entity fromSocket, Entity toSocket, out PrevMoveData saveData)
    {
        saveData = new PrevMoveData();
        var pieceLinkFrom = SystemAPI.GetComponent<ChessSocketPieceLinkC>(fromSocket);
        var pieceLinkTo = SystemAPI.GetComponent<ChessSocketPieceLinkC>(toSocket);

        var pieceDataFrom = SystemAPI.GetComponent<ChessPieceC>(pieceLinkFrom.pieceE);

        //if (SystemAPI.HasComponent<ChessPieceC>(pieceLinkTo.pieceE))
        //{
        //    var pieceDataTo = SystemAPI.GetComponent<ChessPieceC>(pieceLinkTo.pieceE);
        //    if (pieceDataFrom.isWhite == pieceDataTo.isWhite)
        //        return false;
        //}

        saveData = new PrevMoveData
        {
            isValid = true,
            from = fromSocket,
            to = toSocket,
            pieceDataFrom = pieceDataFrom,
            pieceLinkTo = pieceLinkTo,
            pieceLinkFrom = pieceLinkFrom,
            socketFrom = SystemAPI.GetComponent<ChessSocketC>(pieceLinkFrom.pieceE)
        };

        if (SystemAPI.HasComponent<ChessPieceC>(pieceLinkTo.pieceE))
        {
            var pieceDataTo = SystemAPI.GetComponent<ChessPieceC>(pieceLinkTo.pieceE);

            saveData.pieceDataTo = pieceDataTo;
            pieceDataTo.notActive = true;
            saveData.socketTo = SystemAPI.GetComponent<ChessSocketC>(pieceLinkTo.pieceE);
            SystemAPI.SetComponent<ChessSocketC>(pieceLinkTo.pieceE, SystemAPI.GetComponent<ChessSocketC>(fromSocket));
            SystemAPI.SetComponent<ChessPieceC>(pieceLinkTo.pieceE, pieceDataTo);
        }

        SystemAPI.SetComponent<ChessSocketPieceLinkC>(fromSocket, pieceLinkTo);
        SystemAPI.SetComponent<ChessSocketPieceLinkC>(toSocket, pieceLinkFrom);

        SystemAPI.SetComponent<ChessSocketC>(pieceLinkFrom.pieceE, SystemAPI.GetComponent<ChessSocketC>(toSocket));
        SystemAPI.SetComponent<ChessPieceC>(pieceLinkFrom.pieceE, new ChessPieceC
        {
            chessType = pieceDataFrom.chessType,
            isWhite = pieceDataFrom.isWhite,
            isMovedOnce = true
        });

        var ltw = SystemAPI.GetComponentRW<LocalTransform>(pieceLinkFrom.pieceE);

        saveData.fromPos = ltw.ValueRW.Position;
        saveData.toPos = SystemAPI.GetComponent<LocalTransform>(toSocket).Position;

        ltw.ValueRW.Position = saveData.toPos;
        //Debug.Log($"move from {prevMoveData.fromPos} to {prevMoveData.toPos}");

        return true;
    }

    private void ResetPrevMoveData()
    {
        Entity fromSocket = prevMoveDataTemp.from;
        Entity toSocket = prevMoveDataTemp.to;

        SystemAPI.SetComponent<ChessSocketPieceLinkC>(fromSocket, prevMoveDataTemp.pieceLinkFrom);
        SystemAPI.SetComponent<ChessSocketPieceLinkC>(toSocket, prevMoveDataTemp.pieceLinkTo);

        SystemAPI.SetComponent<ChessPieceC>(prevMoveDataTemp.pieceLinkFrom.pieceE, prevMoveDataTemp.pieceDataFrom);
        SystemAPI.SetComponent<LocalTransform>(prevMoveDataTemp.pieceLinkFrom.pieceE, LocalTransform.FromPosition(prevMoveDataTemp.fromPos));
        SystemAPI.SetComponent<ChessSocketC>(prevMoveDataTemp.pieceLinkFrom.pieceE, prevMoveDataTemp.socketFrom);

        if (SystemAPI.HasComponent<ChessPieceC>(prevMoveDataTemp.pieceLinkTo.pieceE))
        {
            SystemAPI.SetComponent<ChessPieceC>(prevMoveDataTemp.pieceLinkTo.pieceE, prevMoveDataTemp.pieceDataTo);
            SystemAPI.SetComponent<LocalTransform>(prevMoveDataTemp.pieceLinkTo.pieceE, LocalTransform.FromPosition(prevMoveDataTemp.toPos));
            SystemAPI.SetComponent<ChessSocketC>(prevMoveDataTemp.pieceLinkTo.pieceE, prevMoveDataTemp.socketTo);
        }

        //Debug.Log($"reset from {prevMoveData.fromPos} to {prevMoveData.toPos}");
    }

    bool IsCorrectSocketToMove(Entity moveFrom, Entity moveTo)
    {
        var piece = SystemAPI.GetComponent<ChessSocketPieceLinkC>(moveFrom).pieceE;
        var turnForSelected = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(piece);

        foreach (var item in turnForSelected)
        {
            if (item.socketC.socketE == moveTo)
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

    bool TryMoveChess(Entity moveFromSocket, Entity moveToSocket, EntityCommandBuffer ecb, out bool killed)
    {
        killed = false;
        if (!IsCorrectSocketToMove(moveFromSocket, moveToSocket))
        {
            return false;
        }

        var moveFromPieceE = SystemAPI.GetComponent<ChessSocketPieceLinkC>(moveFromSocket).pieceE;
        var steps = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(moveFromPieceE);
        ChessPiecePossibleSteps step = new ChessPiecePossibleSteps();

        foreach (var item in steps)
        {
            if (item.socketC.socketE == moveToSocket)
                step = item;
        }

        foreach (var item in m_PrevRealMoves)
        {
            SystemAPI.GetAspect<ChessSocketHighlightAspect>(item.from).DestoryPrevMove(ecb);
            SystemAPI.GetAspect<ChessSocketHighlightAspect>(item.to).DestoryPrevMove(ecb);
        }

        if (step.isÑastling)
        {
            AudioManager.Instance.PlayRequest(SfxType.Move, ecb);

            MovePieceFromToSocketWithChatMessage(moveFromSocket, step.castlingMove.kingMoveTo.socketE, ecb);
            MovePieceFromToSocketWithChatMessage(moveToSocket, step.castlingMove.rookMoveTo.socketE, ecb);
        }
        else
        {
            if (HasPieceInSlot(moveToSocket))
            {
                var toDestory = SystemAPI.GetComponent<ChessSocketPieceLinkC>(moveToSocket);
                ecb.DestroyEntity(toDestory.pieceE);
                killed = true;
                AudioManager.Instance.PlayRequest(SfxType.Kill, ecb);
                PlayParticle.Instance.PlayRequest(SystemAPI.GetComponent<LocalTransform>(moveToSocket).Position, ParticleType.Kill, ecb);

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

            if (!killed)
            {
                AudioManager.Instance.PlayRequest(SfxType.Move, ecb);
            }

            var pieces = SystemAPI.GetComponent<ChessSocketPieceLinkC>(moveFromSocket);
            var pieceData = SystemAPI.GetComponent<ChessPieceC>(pieces.pieceE);

            var boardAspect = GetBoard();

            //pawn promotion
            if (pieceData.chessType == ChessType.Pawn)
            {
                MovePieceFromToSocketWithChatMessage(moveFromSocket, moveToSocket, ecb);
                var isWhite = pieceData.isWhite;

                if (boardAspect.IsBoardEnd(isWhite, boardAspect.IndexOf(moveToSocket)))
                {
                    Entity pieceToDestory = pieces.pieceE;

                    requireTransformData = new SavedPieceTransformationData
                    {
                        isWhite = isWhite,
                        pieceE = pieceToDestory,
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
                MovePieceFromToSocketWithChatMessage(moveFromSocket, moveToSocket, ecb);
            }
        }
        return true;
    }

    private void TransformPiece(Entity moveToSocket, EntityCommandBuffer ecb, Entity pieceE, bool isWhite, PieceTransformType transfType)
    {
        var prefabs = SystemAPI.GetSingleton<ChessBoardPersistentC>();

        var queenPrefabs = isWhite == true ?
            prefabs.whitePiecesPrefabs :
           prefabs.blackPiecesPrefabs;
        Entity newPiecePrefab = queenPrefabs.queen;

        switch (transfType)
        {

            case PieceTransformType.Queen:
                newPiecePrefab = queenPrefabs.queen;

                break;
            case PieceTransformType.Rook:
                newPiecePrefab = queenPrefabs.rook;

                break;
            case PieceTransformType.Bishop:
                newPiecePrefab = queenPrefabs.bishop;


                break;
            case PieceTransformType.Knight:
                newPiecePrefab = queenPrefabs.knight;

                break;
            default:
                break;
        }

        ecb.DestroyEntity(pieceE);
        var instace = ecb.Instantiate(newPiecePrefab);
        var socketTrans = SystemAPI.GetComponent<LocalTransform>(moveToSocket);
        PlayParticle.Instance.PlayRequest(socketTrans.Position, ParticleType.Kill, ecb);
        ecb.AddComponent<ChessSocketC>(instace, SystemAPI.GetComponent<ChessSocketC>(moveToSocket));
        ecb.SetComponent<LocalTransform>(instace, socketTrans);

        ecb.AddComponent<ChessSocketPieceLinkC>(moveToSocket, new ChessSocketPieceLinkC
        {
            pieceE = instace
        });

        var board = SystemAPI.GetSingletonEntity<ChessBoardInstanceT>();

        if (isWhite)
        {
            ecb.AppendToBuffer<ChessBoardWhitePiecesBuffer>(board, new ChessBoardWhitePiecesBuffer
            {
                pieceE = instace
            });
        }
        else
        {
            ecb.AppendToBuffer<ChessBoardBlackPiecesBuffer>(board, new ChessBoardBlackPiecesBuffer
            {
                pieceE = instace
            });
        }
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

    void RecalculatePossibleTurnsForPiece(
        Entity pieceToMoveE,
        ChessBoardInstanceAspect boardAspect,
        int attackes,
        bool isAttackedByKnight,
        bool isKing)
    {
        if (!SystemAPI.HasComponent<ChessSocketC>(pieceToMoveE))
            return;

        var socketC = SystemAPI.GetComponent<ChessSocketC>(pieceToMoveE);
        var pieceData = SystemAPI.GetComponentRW<ChessPieceC>(pieceToMoveE);

        bool isWhite = pieceData.ValueRO.isWhite;

        var turnPositions = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(pieceToMoveE);
        turnPositions.Clear();

        if (!isKing && attackes >= 2 || !isKing && isAttackedByKnight)
            return;

        switch (pieceData.ValueRO.chessType)
        {
            case ChessType.Pawn:
                int offset = -1;
                if (isWhite)
                {
                    offset = 1;
                }

                int x = socketC.x;
                int y = socketC.y + offset;

                TryAddTurn(x + 1, y, true, false, boardAspect, turnPositions, isWhite, out bool hasEnemy1);
                TryAddTurn(x - 1, y, true, false, boardAspect, turnPositions, isWhite, out bool hasEnemy2);

                if (!hasEnemy1 && !hasEnemy2)
                {
                    x = socketC.x;
                    y = socketC.y + offset;

                    if (TryAddTurn(x, y, false, true, boardAspect, turnPositions, isWhite, out bool hasEnemy))
                    {
                        if (!hasEnemy && !pieceData.ValueRO.isMovedOnce)
                        {
                            y += offset;
                            TryAddTurn(x, y, false, true, boardAspect, turnPositions, isWhite, out hasEnemy);
                        }
                    }
                }

                break;
            case ChessType.Bishop:

                LoopMove(socketC.x, socketC.y, 1, 1, boardAspect, turnPositions, isWhite);
                LoopMove(socketC.x, socketC.y, -1, -1, boardAspect, turnPositions, isWhite);
                LoopMove(socketC.x, socketC.y, -1, 1, boardAspect, turnPositions, isWhite);
                LoopMove(socketC.x, socketC.y, 1, -1, boardAspect, turnPositions, isWhite);

                break;
            case ChessType.Rook:
                LoopMove(socketC.x, socketC.y, 1, 0, boardAspect, turnPositions, isWhite);
                LoopMove(socketC.x, socketC.y, -1, 0, boardAspect, turnPositions, isWhite);
                LoopMove(socketC.x, socketC.y, 0, 1, boardAspect, turnPositions, isWhite);
                LoopMove(socketC.x, socketC.y, 0, -1, boardAspect, turnPositions, isWhite);
                break;
            case ChessType.Knight:

                TryAddTurn(socketC.x + 2, socketC.y + 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(socketC.x + 2, socketC.y - 1, true, true, boardAspect, turnPositions, isWhite, out bool _);

                TryAddTurn(socketC.x - 2, socketC.y + 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(socketC.x - 2, socketC.y - 1, true, true, boardAspect, turnPositions, isWhite, out bool _);

                TryAddTurn(socketC.x - 1, socketC.y + 2, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(socketC.x + 1, socketC.y + 2, true, true, boardAspect, turnPositions, isWhite, out bool _);

                TryAddTurn(socketC.x + 1, socketC.y - 2, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(socketC.x - 1, socketC.y - 2, true, true, boardAspect, turnPositions, isWhite, out bool _);

                break;
            case ChessType.Queen:
                LoopMove(socketC.x, socketC.y, 1, 0, boardAspect, turnPositions, isWhite);
                LoopMove(socketC.x, socketC.y, -1, 0, boardAspect, turnPositions, isWhite);
                LoopMove(socketC.x, socketC.y, 0, 1, boardAspect, turnPositions, isWhite);
                LoopMove(socketC.x, socketC.y, 0, -1, boardAspect, turnPositions, isWhite);
                LoopMove(socketC.x, socketC.y, 1, 1, boardAspect, turnPositions, isWhite);
                LoopMove(socketC.x, socketC.y, -1, -1, boardAspect, turnPositions, isWhite);
                LoopMove(socketC.x, socketC.y, -1, 1, boardAspect, turnPositions, isWhite);
                LoopMove(socketC.x, socketC.y, 1, -1, boardAspect, turnPositions, isWhite);

                break;
            case ChessType.King:
                x = socketC.x;
                y = socketC.y;

                TryAddTurn(x + 1, y, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x - 1, y, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x + 1, y + 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x - 1, y - 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x, y + 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x, y - 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x - 1, y + 1, true, true, boardAspect, turnPositions, isWhite, out bool _);
                TryAddTurn(x + 1, y - 1, true, true, boardAspect, turnPositions, isWhite, out bool _);

                TryAddCaslingSteps(pieceToMoveE, socketC, boardAspect, turnPositions, isWhite);
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

        var pieces = boardAspect.GetCurrentPlayerPieces();

        bool isKingUnderAttack = IsSocketUnderAttack(socketC.socketE, boardAspect);

        if (isKingUnderAttack)
            return;

        NativeList<Entity> rooks = new NativeList<Entity>(Allocator.Temp);

        foreach (var pieceE in pieces)
        {
            var pieceData = SystemAPI.GetComponent<ChessPieceC>(pieceE);
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
                var kingMoveToE = boardAspect.GetSocket(kingSocket.x + offset*2, kingSocket.y).socketE;

                turnPositions.Add(new ChessPiecePossibleSteps
                {
                    isÑastling = true,
                    socketC = rookSocketData,
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
                socketC = new ChessSocketC
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
        var pieceE = SystemAPI.GetComponent<ChessSocketPieceLinkC>(slot).pieceE;
        return SystemAPI.GetComponent<ChessPieceC>(pieceE);
    }

    private bool HasPieceInSlot(Entity e)
    {
        return SystemAPI.HasComponent<ChessPieceC>(SystemAPI.GetComponentRO<ChessSocketPieceLinkC>(e).ValueRO.pieceE);
    }
}