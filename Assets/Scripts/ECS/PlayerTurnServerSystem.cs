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

    private Entity m_LastSelectedSocket;
    private Entity m_LastSelectedPieceE;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<ChessBoardInstanceT>();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    protected override void OnUpdate()
    {
        InitBoard();
        MoveOrSelect(out bool needMove, out MoveChess moveData);

        if (needMove)
        {
            ExecuteMove(moveData);
        }
    }

    private void ExecuteMove(MoveChess moveData)
    {
        var ecb1 = new EntityCommandBuffer(Allocator.Temp);

        var board = GetBoard();
        bool isWhiteStep = board.IsWhiteStep();

        var moveFrom = board.GetSocket(moveData.fromIndex).socketE;
        var moveTo = board.GetSocket(moveData.toIndex).socketE;

        bool moved = TryMoveChess(moveFrom, moveTo, ecb1);

        Debug.Log($"[Server] move chess: {moved}");
        ecb1.Playback(EntityManager);

        board = GetBoard();

        RecalculatePossibleStepsForBoard();

        var king = board.GetCurrentKing();
        var pieces = board.GetCurrentPlayerPieces();

        NativeList<ChessPiecePossibleSteps> steps = new NativeList<ChessPiecePossibleSteps>(Allocator.Temp);

        foreach (var item in pieces)
        {
            if (SystemAPI.HasBuffer<ChessPiecePossibleSteps>(item))
            {
                steps.Clear();
                var stepsBefore = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(item).ToNativeArray(Allocator.Temp);
                var socketC = SystemAPI.GetComponent<ChessSocketC>(item);

                foreach (var item1 in stepsBefore)
                {
                    MoveFromToSocket(socketC.socketE, item1.socketC.socketE);
                    RecalculatePossibleStepsForOponent(board);
                    if (!IsKingUnderAttack(king, out _, out _))
                    {
                        steps.Add(item1);
                    }
                    ResetPrevMoveData();
                }

                var possibleSteps = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(item);
                possibleSteps.Clear();
                possibleSteps.AddRange(steps.AsArray());
            }
        }

        if (IsGameFinished())
        {          
            board = GetBoard();
            board.instanceC.ValueRW.blockInput = true;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var request = EntityManager.CreateEntity();
            ecb.AddComponent<SendRpcCommandRequest>(request);
            ecb.AddComponent<EndGameRPC>(request,new EndGameRPC { 
                  isWhiteWin = isWhiteStep,
            });
            ecb.Playback(EntityManager);
            Debug.Log($"[Server] winner white:{isWhiteStep}");
        }
        else 
        {
            Debug.Log($"[Server] game continue");
        }
    }

    void ShowSelectedAndTurns(EntityCommandBuffer ecb)
    {
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

                if (turn.hasEnemy)
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

    bool CanMoveChess(Entity raycastedSocketE, EntityCommandBuffer ecb)
    {
        if (!SystemAPI.HasBuffer<ChessPiecePossibleSteps>(m_LastSelectedPieceE))
            return false;

        return IsCorrectSocketToMove(raycastedSocketE);
    }

    bool IsCorrectSocketToMove(Entity raycastedSocketE)
    {
        var turnForSelected = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(m_LastSelectedPieceE);

        foreach (var item in turnForSelected)
        {
            if (item.socketC.socketE == raycastedSocketE)
            {
                return true;
            }
        }

        return false;
    }

    void ClearSelection(EntityCommandBuffer ecb)
    {
        if (SystemAPI.HasComponent<ChessSocketSelectedT>(m_LastSelectedSocket))
        {
            ecb.RemoveComponent<ChessSocketSelectedT>(m_LastSelectedSocket);
            var asp = SystemAPI.GetAspect<ChessSocketHighlightAspect>(m_LastSelectedSocket);
            asp.Destory(ecb);
        }

        if (HasSelectedPiece())
        {
            var steps = GetSelectedPossibleSteps();
            foreach (var turn in steps)
            {
                var highlight = SystemAPI.GetAspect<ChessSocketHighlightAspect>(turn.socketC.socketE);
                highlight.Destory(ecb);
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

    public Entity Raycast(float3 RayFrom, float3 RayTo)
    {
        // Set up Entity Query to get PhysicsWorldSingleton
        // If doing this in SystemBase or ISystem, call GetSingleton<PhysicsWorldSingleton>()/SystemAPI.GetSingleton<PhysicsWorldSingleton>() directly.
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

        Unity.Physics.RaycastHit hit = new Unity.Physics.RaycastHit();
        bool haveHit = collisionWorld.CastRay(input, out hit);
        if (haveHit)
        {
            return hit.Entity;
        }

        return Entity.Null;
    }

    bool RaycastSocket(float3 rayFrom, float3 rayTo, out Entity raycastedSocketE)
    {
        raycastedSocketE = Raycast(rayFrom, rayTo);

        bool result = SystemAPI.HasComponent<ChessSocketC>(raycastedSocketE);

        Debug.Log($"[Server] do raycast has target {result} target {raycastedSocketE}");
        return result;
    }

    void MoveOrSelect(out bool needMove, out MoveChess moveData)
    {
        moveData = new MoveChess { fromIndex = -1, toIndex = -1 };
        needMove = false;
        var quety = SystemAPI.QueryBuilder().WithAll<RaycastChessRpc>().Build();

        if (quety.IsEmpty)
        {
            return;
        }

        var data = quety.GetSingleton<RaycastChessRpc>();
        EntityManager.DestroyEntity(quety);

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool canMove = false;
        bool isSelected = false;

        if (RaycastSocket(data.rayFrom, data.rayTo, out Entity raycastedSocketE))
        {
            Debug.Log("[Client] raycasted socket");
            var state = SystemAPI.GetSingleton<ChessBoardTurnC>();
            if (SystemAPI.HasComponent<ChessSocketC>(raycastedSocketE))
            {
                canMove = CanMoveChess(raycastedSocketE, ecb);

                if (!canMove && HasPieceInSlot(raycastedSocketE))
                {
                    var pieceE = SystemAPI.GetComponent<ChessSocketPieceLinkC>(raycastedSocketE).pieceE;
                    var pieceData = SystemAPI.GetComponent<ChessPieceC>(pieceE);
                    if (state.turnColor == pieceData.color)
                    {
                        isSelected = true;

                        ClearSelection(ecb);
                        m_LastSelectedSocket = raycastedSocketE;
                        m_LastSelectedPieceE = pieceE;
                    }
                }
            }
        }

        if (canMove)
        {
            var board = GetBoard();
            moveData = new MoveChess
            {
                fromIndex = board.IndexOf(m_LastSelectedSocket),
                toIndex = board.IndexOf(raycastedSocketE),
            };

            ClearSelection(ecb);
            needMove = true;
        }
        else if (isSelected)
        {
            Debug.Log("[Client] select chess");
            ShowSelectedAndTurns(ecb);
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

    //check piece between slots horizonta, vertical, dialonal
    bool HasPieceBetween(int startX, int endX, int startY, int endY, ChessBoardInstanceAspect board)
    {
        var end = new int2(endX, endY);
        var start = new int2(startX, startY);

        int2 dir = end - start;
        dir.x = math.clamp(dir.x, -1, 1);
        dir.y = math.clamp(dir.y, -1, 1);
        int2 current = new int2(startX, startY) + dir;

        while (true)
        {
            if (current.x == end.x && current.y == end.y)
            {
                break;
            }

            bool hasPiece = HasPieceInSlot(board.GetSocket(current.x, current.y).socketE);

            if (hasPiece)
                return true;

            current += dir;
        }

        return false;
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
        //CalculateKingSteps(king);

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

            RecalculatePossibleTurnsForPiece(piece, board, attackers, isttackedByKnight);
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

    private void MoveFromToSocket(Entity fromSocket, Entity toSocket)
    {
        var toPiece = MovePieceToSocketData(fromSocket, toSocket);
        MovePieceToSocketPosition(toPiece, toSocket);
    }

    public struct PrevMoveData
    {
        public Entity from;
        public Entity to;

        public ChessSocketPieceLinkC pieceLinkFrom;
        public ChessPieceC pieceDataFrom;
        public ChessSocketC socketFrom;

        public ChessSocketPieceLinkC pieceLinkTo;
        public ChessPieceC pieceDataTo;
        public ChessSocketC socketTo;

        public float3 fromPos;
        public float3 toPos;
    }

    public PrevMoveData prevMoveData;

    private Entity MovePieceToSocketData(Entity fromSocket, Entity toSocket)
    {
        var pieceLinkFrom = SystemAPI.GetComponent<ChessSocketPieceLinkC>(fromSocket);
        var pieceLinkTo = SystemAPI.GetComponent<ChessSocketPieceLinkC>(toSocket);

        var pieceDataFrom = SystemAPI.GetComponent<ChessPieceC>(pieceLinkFrom.pieceE);

        prevMoveData = new PrevMoveData
        {
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
            prevMoveData.pieceDataTo = pieceDataTo;
            pieceDataTo.notActive = true;
            prevMoveData.socketTo = SystemAPI.GetComponent<ChessSocketC>(pieceLinkTo.pieceE);
            SystemAPI.SetComponent<ChessSocketC>(pieceLinkTo.pieceE, SystemAPI.GetComponent<ChessSocketC>(fromSocket));
            SystemAPI.SetComponent<ChessPieceC>(pieceLinkTo.pieceE, pieceDataTo);
        }

        SystemAPI.SetComponent<ChessSocketPieceLinkC>(fromSocket, pieceLinkTo);
        SystemAPI.SetComponent<ChessSocketPieceLinkC>(toSocket, pieceLinkFrom);

        SystemAPI.SetComponent<ChessSocketC>(pieceLinkFrom.pieceE, SystemAPI.GetComponent<ChessSocketC>(toSocket));
        SystemAPI.SetComponent<ChessPieceC>(pieceLinkFrom.pieceE, new ChessPieceC
        {
            chessType = pieceDataFrom.chessType,
            color = pieceDataFrom.color,
            isMovedOnce = true
        });

        return pieceLinkFrom.pieceE;
    }

    private void ResetPrevMoveData()
    {
        Entity fromSocket = prevMoveData.from;
        Entity toSocket = prevMoveData.to;

        SystemAPI.SetComponent<ChessSocketPieceLinkC>(fromSocket, prevMoveData.pieceLinkFrom);
        SystemAPI.SetComponent<ChessSocketPieceLinkC>(toSocket, prevMoveData.pieceLinkTo);

        SystemAPI.SetComponent<ChessPieceC>(prevMoveData.pieceLinkFrom.pieceE, prevMoveData.pieceDataFrom);
        SystemAPI.SetComponent<LocalTransform>(prevMoveData.pieceLinkFrom.pieceE, LocalTransform.FromPosition(prevMoveData.fromPos));
        SystemAPI.SetComponent<ChessSocketC>(prevMoveData.pieceLinkFrom.pieceE, prevMoveData.socketFrom);

        if (SystemAPI.HasComponent<ChessPieceC>(prevMoveData.pieceLinkTo.pieceE))
        {
            SystemAPI.SetComponent<ChessPieceC>(prevMoveData.pieceLinkTo.pieceE, prevMoveData.pieceDataTo);
            SystemAPI.SetComponent<LocalTransform>(prevMoveData.pieceLinkTo.pieceE, LocalTransform.FromPosition(prevMoveData.toPos));
            SystemAPI.SetComponent<ChessSocketC>(prevMoveData.pieceLinkTo.pieceE, prevMoveData.socketTo);

        }

        //Debug.Log($"reset from {prevMoveData.fromPos} to {prevMoveData.toPos}");
    }

    private void MovePieceToSocketPosition(Entity pieceE, Entity destionationSocket)
    {
        var ltw = SystemAPI.GetComponentRW<LocalTransform>(pieceE);

        prevMoveData.fromPos = ltw.ValueRW.Position;
        prevMoveData.toPos = SystemAPI.GetComponent<LocalTransform>(destionationSocket).Position;

        ltw.ValueRW.Position = prevMoveData.toPos;
        //Debug.Log($"move from {prevMoveData.fromPos} to {prevMoveData.toPos}");
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

    bool TryMoveChess(Entity moveFromSocket, Entity moveToSocket, EntityCommandBuffer ecb)
    {
        if (!IsCorrectSocketToMove(moveFromSocket, moveToSocket))
        {
            return false;
        }

        if (HasPieceInSlot(moveToSocket))
        {
            var toDestory = SystemAPI.GetComponent<ChessSocketPieceLinkC>(moveToSocket);
            ecb.DestroyEntity(toDestory.pieceE);
        }

        var pieces = SystemAPI.GetComponent<ChessSocketPieceLinkC>(moveFromSocket);
        var pieceData = SystemAPI.GetComponent<ChessPieceC>(pieces.pieceE);

        var boardAspect = GetBoard();

        //pawn promotion
        if (pieceData.chessType == ChessType.Pawn)
        {
            MoveFromToSocket(moveFromSocket, moveToSocket);
            var color = pieceData.color;

            if (boardAspect.IsBoardEnd(color, boardAspect.IndexOf(moveToSocket)))
            {
                var prefabs = SystemAPI.GetSingleton<ChessBoardPersistentC>();

                var queenPrefab = color == PieceColor.White ?
                    prefabs.whitePiecesPrefabs.queen :
                   prefabs.blackPiecesPrefabs.queen;

                ecb.DestroyEntity(pieces.pieceE);
                var instace = ecb.Instantiate(queenPrefab);
                ecb.SetComponent<ChessPieceC>(instace, new ChessPieceC
                {
                    chessType = ChessType.Queen,
                    color = color,
                    isMovedOnce = true
                });
                ecb.AddComponent<ChessSocketC>(instace, SystemAPI.GetComponent<ChessSocketC>(moveToSocket));
                ecb.SetComponent<LocalTransform>(instace, SystemAPI.GetComponent<LocalTransform>(moveToSocket));
                ecb.AddComponent<ChessSocketPieceLinkC>(moveToSocket, new ChessSocketPieceLinkC
                {
                    pieceE = instace
                });
            }
        }
        //castling
        else if (pieceData.chessType == ChessType.Rook && !pieceData.isMovedOnce)
        {
            bool isWhite = pieceData.color == PieceColor.White;
            var kingE = isWhite ?
                boardAspect.instanceC.ValueRO.whiteKingE : boardAspect.instanceC.ValueRO.blackKingE;

            var kingPiece = SystemAPI.GetComponent<ChessPieceC>(kingE);

            if (!kingPiece.isMovedOnce && !HasPieceInSlot(moveToSocket))
            {
                var kingSocket = SystemAPI.GetComponent<ChessSocketC>(kingE);
                var pieceSocket = SystemAPI.GetComponent<ChessSocketC>(pieces.pieceE);

                var kingSocketLeftE = boardAspect.GetSocket(kingSocket.x - 1, kingSocket.y).socketE;
                var kingSocketRightE = boardAspect.GetSocket(kingSocket.x + 1, kingSocket.y).socketE;

                if (moveToSocket == kingSocketLeftE || moveToSocket == kingSocketRightE)
                {
                    NativeList<Entity> checkSockets = new NativeList<Entity>(Allocator.Temp);

                    if (kingSocket.y == 0 && pieceSocket.y == 0 && isWhite ||
                        kingSocket.y == ChessBoardInstanceAspect.GRID_Y - 1 && pieceSocket.y == ChessBoardInstanceAspect.GRID_Y - 1 && !isWhite)
                    {
                        bool isRookLeft = kingSocket.x > pieceSocket.x;

                        int offset = isRookLeft ? -1 : 1;
                        var socket2 = boardAspect.GetSocket(kingSocket.x + offset, kingSocket.y).socketE;

                        if (!HasPieceInSlot(socket2))
                        {
                            var socket1 = boardAspect.GetSocket(kingSocket.x, kingSocket.y).socketE;
                            var socket3 = boardAspect.GetSocket(kingSocket.x + offset * 2, kingSocket.y).socketE;

                            checkSockets.Add(socket1);
                            checkSockets.Add(socket2);
                            checkSockets.Add(socket3);
                        }
                    }

                    bool isUnderAttack = false;
                    foreach (var item1 in checkSockets)
                    {
                        if (IsSocketUnderAttack(item1, boardAspect))
                        {
                            isUnderAttack = true;
                            break;
                        }
                    }

                    if (!isUnderAttack && checkSockets.Length != 0)
                    {
                        MoveFromToSocket(checkSockets[0], checkSockets[2]);
                    }
                }
            }
            MoveFromToSocket(moveFromSocket, moveToSocket);
        }
        else
        {
            MoveFromToSocket(moveFromSocket, moveToSocket);
        }

        boardAspect.NextTurn();

        return true;
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
        bool isAttackedByKnight)
    {
        if (!SystemAPI.HasComponent<ChessSocketC>(pieceToMoveE))
            return;

        var socketC = SystemAPI.GetComponent<ChessSocketC>(pieceToMoveE);
        var pieceData = SystemAPI.GetComponentRW<ChessPieceC>(pieceToMoveE);

        bool isWhite = pieceData.ValueRO.color == PieceColor.White;
        bool isCurrentPlayer = boardAspect.turnC.ValueRO.turnColor == pieceData.ValueRO.color;
        var turnPositions = SystemAPI.GetBuffer<ChessPiecePossibleSteps>(pieceToMoveE);
        turnPositions.Clear();

        if (attackes >= 2 || isAttackedByKnight)
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
                break;

            default:
                break;
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
            bool isWhiteTarget = data.color == PieceColor.White;
            if (canBeatEnemy && IsEnemy(isWhiteTarget, isWhiteSource))
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

    private bool IsEnemy(bool isWhite, bool isWhite1)
    {
        return isWhite != isWhite1;
    }

    private bool HasPieceInSlot(Entity e)
    {
        return SystemAPI.HasComponent<ChessPieceC>(SystemAPI.GetComponentRO<ChessSocketPieceLinkC>(e).ValueRO.pieceE);
    }
}