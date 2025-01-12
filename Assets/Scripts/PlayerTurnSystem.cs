using System.Net.Sockets;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct ChessTurnPositions
{
    public Entity socketE;
    public ChessSocketC socketPos;
    public bool hasEnemy;
}

public partial class PlayerTurnSystem : SystemBase
{
    private Camera m_Camera;
    private Entity m_LastSelectedSocket;
    private Entity m_LastSelectedPieceE;

    NativeList<ChessTurnPositions> m_TurnForSelected;
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<ChessBoardInstanceT>();

        m_TurnForSelected = new NativeList<ChessTurnPositions>(Allocator.Persistent);
        m_Camera = Camera.main;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        m_TurnForSelected.Dispose();
    }

    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        RaycastInput(ecb, out bool hasCorrectInput);

        if (hasCorrectInput)
        {
            CalculatePossibleTurnsForPiece(m_LastSelectedPieceE, m_TurnForSelected);
            ShowSelectedAndTurns(ecb);
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

            bool hasPiece = SystemAPI.HasComponent<ChessSocketPieceLinkC>(board.GetSocket(current.x, current.y).socketE);

            if (hasPiece)
                return true;

            current += dir;
        }

        return false;
    }

    bool IsSocketUnderAttack(Entity socket, PieceColor targetColor, ChessBoardInstanceAspect board, out int numberOfAttackers)
    {
        numberOfAttackers = 0;
        NativeArray<Entity> attackes = targetColor == PieceColor.White ?
            board.boardPiecesBlack.Reinterpret<Entity>().AsNativeArray() :
            board.boardPiecesWhite.Reinterpret<Entity>().AsNativeArray();

        NativeList<ChessTurnPositions> turns = new NativeList<ChessTurnPositions>(Allocator.Temp);
        foreach (var attacker in attackes)
        {
            turns.Clear();
            CalculatePossibleTurnsForPiece(attacker, turns);

            foreach (var turn1 in turns)
            {
                if (turn1.socketE == socket)
                {
                    numberOfAttackers++;
                }
            }
        }

        return numberOfAttackers > 0;
    }

    private void MovePieceToSocket(Entity sourceSocket, Entity destionationSocket, EntityCommandBuffer ecb)
    {
        var pieces = SystemAPI.GetComponent<ChessSocketPieceLinkC>(sourceSocket);
        ecb.AddComponent<ChessSocketPieceLinkC>(destionationSocket, pieces);

        var ltw = SystemAPI.GetComponentRW<LocalTransform>(pieces.pieceE);
        var pieceData = SystemAPI.GetComponentRW<ChessPieceC>(pieces.pieceE);
        pieceData.ValueRW.isMovedOnce = true;
        ecb.AddComponent<ChessSocketC>(pieces.pieceE, SystemAPI.GetComponent<ChessSocketC>(destionationSocket));
        ltw.ValueRW.Position = SystemAPI.GetComponent<LocalTransform>(destionationSocket).Position;
        ecb.RemoveComponent<ChessSocketPieceLinkC>(sourceSocket);
    }

    bool TryMoveChess(Entity raycastedSocketE, EntityCommandBuffer ecb)
    {
        bool result = false;

        foreach (var item in m_TurnForSelected)
        {
            if (item.socketE == raycastedSocketE)
            {
                if (SystemAPI.HasComponent<ChessSocketPieceLinkC>(raycastedSocketE))
                {
                    var toDestory = SystemAPI.GetComponent<ChessSocketPieceLinkC>(raycastedSocketE);
                    ecb.DestroyEntity(toDestory.pieceE);
                }

                result = true;
              
                var pieces = SystemAPI.GetComponent<ChessSocketPieceLinkC>(m_LastSelectedSocket);
                ecb.AddComponent<ChessSocketPieceLinkC>(raycastedSocketE, pieces);

                var ltw = SystemAPI.GetComponentRW<LocalTransform>(pieces.pieceE);
                var pieceData = SystemAPI.GetComponentRW<ChessPieceC>(pieces.pieceE);
                ecb.AddComponent<ChessSocketC>(pieces.pieceE, SystemAPI.GetComponent<ChessSocketC>(raycastedSocketE));
                ltw.ValueRW.Position = SystemAPI.GetComponent<LocalTransform>(raycastedSocketE).Position;
                ecb.RemoveComponent<ChessSocketPieceLinkC>(m_LastSelectedSocket);

                //upgrade for pawn
                if (pieceData.ValueRO.chessType == ChessType.Pawn)
                {
                    var boardE = SystemAPI.GetSingletonEntity<ChessBoardInstanceT>();
                    var boardAspect = SystemAPI.GetAspect<ChessBoardInstanceAspect>(boardE);

                    var color = pieceData.ValueRO.color;

                    if (boardAspect.IsBoardEnd(color, boardAspect.IndexOf(raycastedSocketE)))
                    {
                        var prefabs = SystemAPI.GetSingleton<ChessBoardPersistentC>();

                        var queenPrefab = color == PieceColor.White ?
                            prefabs.whitePiecesPrefabs.queen :
                           prefabs.blackPiecesPrefabs.queen;

                        ecb.DestroyEntity(pieces.pieceE);
                        var instace = ecb.Instantiate(queenPrefab);
                        ecb.AppendToBuffer<LinkedEntityGroup>(boardE, new LinkedEntityGroup { Value = instace });
                        ecb.SetComponent<ChessPieceC>(instace, new ChessPieceC
                        {
                            chessType = ChessType.Queen,
                            color = color,
                            isMovedOnce = true
                        });
                        ecb.AddComponent<ChessSocketC>(instace, SystemAPI.GetComponent<ChessSocketC>(raycastedSocketE));
                        ecb.SetComponent<LocalTransform>(instace, SystemAPI.GetComponent<LocalTransform>(raycastedSocketE));
                        ecb.AddComponent<ChessSocketPieceLinkC>(raycastedSocketE, new ChessSocketPieceLinkC
                        {
                            pieceE = instace
                        });
                    }
                }
                else if (pieceData.ValueRO.chessType == ChessType.Rook && !pieceData.ValueRO.isMovedOnce)
                {
                    var boardE = SystemAPI.GetSingletonEntity<ChessBoardInstanceT>();
                    var boardAspect = SystemAPI.GetAspect<ChessBoardInstanceAspect>(boardE);
                    bool isWhite = pieceData.ValueRO.color == PieceColor.White;
                    var kingE = isWhite ?
                        boardAspect.instanceC.ValueRO.whiteKingE : boardAspect.instanceC.ValueRO.blackKingE;

                    var kingPiece = SystemAPI.GetComponent<ChessPieceC>(kingE);

                    if (!kingPiece.isMovedOnce && !SystemAPI.HasComponent<ChessSocketPieceLinkC>(raycastedSocketE))
                    {
                        var kingSocket = SystemAPI.GetComponent<ChessSocketC>(kingE);
                        var pieceSocket = SystemAPI.GetComponent<ChessSocketC>(pieces.pieceE);

                        var kingSocketLeftE = boardAspect.GetSocket(kingSocket.x - 1, kingSocket.y).socketE;
                        var kingSocketRightE = boardAspect.GetSocket(kingSocket.x + 1, kingSocket.y).socketE;

                        if (raycastedSocketE == kingSocketLeftE || raycastedSocketE == kingSocketRightE)
                        {
                            var kingSocketE = boardAspect.GetSocket(kingSocket.x, kingSocket.y);
                            var pieceSocketE = boardAspect.GetSocket(pieceSocket.x, pieceSocket.y);

                            NativeList<Entity> checkSockets = new NativeList<Entity>(Allocator.Temp);

                            if (kingSocket.y == 0 && pieceSocket.y == 0 && isWhite ||
                                kingSocket.y == ChessBoardInstanceAspect.GRID_Y - 1 && pieceSocket.y == ChessBoardInstanceAspect.GRID_Y - 1 && !isWhite)
                            {
                                bool isRookLeft = kingSocket.x > pieceSocket.x;

                                int offset = isRookLeft ? -1 : 1;       
                                var socket2 = boardAspect.GetSocket(kingSocket.x + offset, kingSocket.y).socketE;
                                                    
                                if (!SystemAPI.HasComponent<ChessSocketPieceLinkC>(socket2))
                                {
                                    var socket1 = boardAspect.GetSocket(kingSocket.x, kingSocket.y).socketE;
                                    var socket3 = boardAspect.GetSocket(kingSocket.x + offset * 2, kingSocket.y).socketE;
                                    Debug.Log($"x:{kingSocket.x}y:{kingSocket.y}");
                                    Debug.Log($"x:{kingSocket.x}y:{kingSocket.y}");
                                    Debug.Log($"x:{kingSocket.x}y:{kingSocket.y}");

                                    checkSockets.Add(socket1);
                                    checkSockets.Add(socket2);
                                    checkSockets.Add(socket3);
                                }      
                            }

                            bool isUnderAttack = false;
                            foreach (var item1 in checkSockets)
                            {
                                if (IsSocketUnderAttack(item1, pieceData.ValueRO.color, boardAspect, out _))
                                {
                                    isUnderAttack = true;
                                    break;
                                }
                            }

                            if (!isUnderAttack && checkSockets.Length != 0)
                            {
                                MovePieceToSocket(checkSockets[0], checkSockets[2], ecb);
                            }
                        }
                    }
                }

                pieceData.ValueRW.isMovedOnce = true;

                var chessGameState = SystemAPI.GetSingletonRW<ChessBoardTurnC>();
                chessGameState.ValueRW.turnColor = PieceColor.White == chessGameState.ValueRW.turnColor ? PieceColor.Black : PieceColor.White;
                break;
            }
        }

        if (result)
            ClearSelection(ecb);

        return result;
    }

    void RaycastInput(EntityCommandBuffer ecb, out bool hasCorrectInput)
    {
        hasCorrectInput = false;

        if (Input.GetMouseButtonDown(0))
        {
            var state = SystemAPI.GetSingleton<ChessBoardTurnC>();

            var ray = m_Camera.ScreenPointToRay(Input.mousePosition);
            var raycastedSocketE = Raycast(ray.origin, ray.origin + ray.direction * 200f);

            if (SystemAPI.HasComponent<ChessSocketC>(raycastedSocketE))
            {
                hasCorrectInput = TryMoveChess(raycastedSocketE, ecb);

                if (!hasCorrectInput && SystemAPI.HasComponent<ChessSocketPieceLinkC>(raycastedSocketE))
                {
                    var pieceData = SystemAPI.GetComponent<ChessPieceC>(SystemAPI.GetComponent<ChessSocketPieceLinkC>(raycastedSocketE).pieceE);
                    if (state.turnColor == pieceData.color)
                    {
                        ClearSelection(ecb);
                        hasCorrectInput = true;
                        m_LastSelectedSocket = raycastedSocketE;
                        m_LastSelectedPieceE = Entity.Null;
                        if (SystemAPI.HasComponent<ChessSocketPieceLinkC>(m_LastSelectedSocket))
                        {
                            m_LastSelectedPieceE = SystemAPI.GetComponent<ChessSocketPieceLinkC>(m_LastSelectedSocket).pieceE;
                        }
                    }
                }
            }
        }
    }

    void ClearSelection(EntityCommandBuffer ecb)
    {
        if (SystemAPI.HasComponent<ChessSocketSelectedT>(m_LastSelectedSocket))
        {
            ecb.RemoveComponent<ChessSocketSelectedT>(m_LastSelectedSocket);
            var asp = SystemAPI.GetAspect<ChessSocketHighlightAspect>(m_LastSelectedSocket);
            asp.Destory(ecb);
        }

        foreach (var turn in m_TurnForSelected)
        {
            var highlight = SystemAPI.GetAspect<ChessSocketHighlightAspect>(turn.socketE);
            highlight.Destory(ecb);
        }

        m_LastSelectedSocket = Entity.Null;
        m_LastSelectedPieceE = Entity.Null;
        m_TurnForSelected.Clear();
    }

    void LoopMove(int x, int y, int offsetX, int offsetY, ChessBoardInstanceAspect boardAspect, NativeList<ChessTurnPositions> chessTurnPositions, in ChessPieceC pieceData)
    {
        while (true)
        {
            x += offsetX;
            y += offsetY;

            bool hasTurn = TryAddTurn(x, y, true, true, boardAspect, chessTurnPositions, pieceData, out bool hasEnemy);

            if (hasEnemy)
                break;

            if (!hasTurn)
                break;
        }
    }

    void CalculatePossibleTurnsForPiece(Entity pieceAttacker, NativeList<ChessTurnPositions> turnPositions)
    {
        if (!SystemAPI.HasComponent<ChessSocketC>(pieceAttacker))
            return;

        var boardE = SystemAPI.GetSingletonEntity<ChessBoardInstanceT>();
        var boardAspect = SystemAPI.GetAspect<ChessBoardInstanceAspect>(boardE);

        var socketC = SystemAPI.GetComponent<ChessSocketC>(pieceAttacker);
        var pieceData = SystemAPI.GetComponentRW<ChessPieceC>(pieceAttacker);

        switch (pieceData.ValueRO.chessType)
        {
            case ChessType.Pawn:
                int offset = -1;
                if (pieceData.ValueRO.color == PieceColor.White)
                {
                    offset = 1;
                }

                int x = socketC.x;
                int y = socketC.y + offset;

                TryAddTurn(x + 1, y, true, false, boardAspect, turnPositions, pieceData.ValueRO, out bool hasEnemy1);
                TryAddTurn(x - 1, y, true, false, boardAspect, turnPositions, pieceData.ValueRO, out bool hasEnemy2);

                if (!hasEnemy1 && !hasEnemy2)
                {
                    x = socketC.x;
                    y = socketC.y + offset;

                    if (TryAddTurn(x, y, false, true, boardAspect, turnPositions, pieceData.ValueRO, out bool hasEnemy))
                    {
                        if (!hasEnemy && !pieceData.ValueRO.isMovedOnce)
                        {
                            y += offset;
                            TryAddTurn(x, y, false, true, boardAspect, turnPositions, pieceData.ValueRO, out hasEnemy);
                        }
                    }
                }

                break;
            case ChessType.Bishop:

                LoopMove(socketC.x, socketC.y, 1, 1, boardAspect, turnPositions, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, -1, -1, boardAspect, turnPositions, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, -1, 1, boardAspect, turnPositions, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 1, -1, boardAspect, turnPositions, pieceData.ValueRO);

                break;
            case ChessType.Rook:
                LoopMove(socketC.x, socketC.y, 1, 0, boardAspect, turnPositions, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, -1, 0, boardAspect, turnPositions, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 0, 1, boardAspect, turnPositions, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 0, -1, boardAspect, turnPositions, pieceData.ValueRO);
                break;
            case ChessType.Knight:

                TryAddTurn(socketC.x + 2, socketC.y + 1, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(socketC.x + 2, socketC.y - 1, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);

                TryAddTurn(socketC.x - 2, socketC.y + 1, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(socketC.x - 2, socketC.y - 1, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);

                TryAddTurn(socketC.x - 1, socketC.y + 2, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(socketC.x + 1, socketC.y + 2, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);

                TryAddTurn(socketC.x + 1, socketC.y - 2, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(socketC.x - 1, socketC.y - 2, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);

                break;
            case ChessType.Queen:
                LoopMove(socketC.x, socketC.y, 1, 0, boardAspect, turnPositions, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, -1, 0, boardAspect, turnPositions, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 0, 1, boardAspect, turnPositions, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 0, -1, boardAspect, turnPositions, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 1, 1, boardAspect, turnPositions, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, -1, -1, boardAspect, turnPositions, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, -1, 1, boardAspect, turnPositions, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 1, -1, boardAspect, turnPositions, pieceData.ValueRO);

                break;
            case ChessType.King:
                Debug.Log("King Selected");
                x = socketC.x;
                y = socketC.y;

                TryAddTurn(x + 1, y, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x - 1, y, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x + 1, y + 1, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x - 1, y - 1, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x, y + 1, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x, y - 1, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x - 1, y + 1, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x + 1, y - 1, true, true, boardAspect, turnPositions, pieceData.ValueRO, out bool _);
                break;
            default:
                break;
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

        foreach (var turn in m_TurnForSelected)
        {
            var highlight = SystemAPI.GetAspect<ChessSocketHighlightAspect>(turn.socketE);

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
        NativeList<ChessTurnPositions> turns,
        in ChessPieceC pieceData,
        out bool hasEnemy)
    {
        hasEnemy = false;
        if (!IsValidXY(x, y))
            return false;

        var socket = boardAspect.GetSocket(x, y);

        if (canBeatEnemy && HasPieceInSlot(socket.socketE))
        {
            var data = GetPieceDataFromSlot(socket.socketE);
            if (IsEnemy(data.color, pieceData.color))
            {
                turns.Add(new ChessTurnPositions
                {
                    hasEnemy = true,
                    socketE = socket.socketE,
                    socketPos = new ChessSocketC
                    {
                        x = x,
                        y = y,
                    }
                });
                hasEnemy = true;
                return true;
            }
        }
        else if (canMoveToEmpty)
        {
            turns.Add(new ChessTurnPositions
            {
                hasEnemy = false,
                socketE = socket.socketE,
                socketPos = new ChessSocketC
                {
                    x = x,
                    y = y,
                }
            });
            return true;
        }

        return false;
    }

    private ChessPieceC GetPieceDataFromSlot(Entity slot)
    {
        var pieceE = SystemAPI.GetComponent<ChessSocketPieceLinkC>(slot).pieceE;
        return SystemAPI.GetComponent<ChessPieceC>(pieceE);
    }

    private bool IsEnemy(PieceColor color, PieceColor color1)
    {
        return color1 != color;
    }

    private bool HasPieceInSlot(Entity e)
    {
        return SystemAPI.HasComponent<ChessSocketPieceLinkC>(e);
    }
}