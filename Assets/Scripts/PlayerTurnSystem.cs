using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct ChessTurnPositions
{
    public Entity socketE;
    public bool hasEnemy;
}

public partial class PlayerTurnSystem : SystemBase
{
    private Camera m_Camera;
    private Entity m_LastSelected;
    NativeList<ChessTurnPositions> m_TurnPositions;
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<ChessGameStateT>();
        RequireForUpdate<ChessBoardC>();

        m_TurnPositions = new NativeList<ChessTurnPositions>(Allocator.Persistent);
        m_Camera = Camera.main;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        m_TurnPositions.Dispose();
    }
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        RaycastInput(ecb, out bool hasCorrectInput);

        if (hasCorrectInput)
        {
            RecalculatePossibleTurns(ecb, m_LastSelected);
            ShowSelectedAndTurns(ecb);
        }

        ecb.Playback(EntityManager);
    }

    bool TryMoveChess(Entity raycastedSocketE, EntityCommandBuffer ecb)
    {
        bool result = false;

        foreach (var item in m_TurnPositions)
        {
            if (item.socketE == raycastedSocketE)
            {
                if (SystemAPI.HasComponent<ChessSocketPieceC>(raycastedSocketE))
                {
                    var toDestory = SystemAPI.GetComponent<ChessSocketPieceC>(raycastedSocketE);
                    ecb.DestroyEntity(toDestory.pieceE);
                }

                result = true;
                var pieces = SystemAPI.GetComponent<ChessSocketPieceC>(m_LastSelected);
                ecb.AddComponent<ChessSocketPieceC>(raycastedSocketE, pieces);
                var ltw = SystemAPI.GetComponentRW<LocalTransform>(pieces.pieceE);
                var pieceData = SystemAPI.GetComponentRW<ChessPieceC>(pieces.pieceE);
                pieceData.ValueRW.movedOnce = true;
                ltw.ValueRW.Position = SystemAPI.GetComponent<LocalTransform>(raycastedSocketE).Position;
                ecb.RemoveComponent<ChessSocketPieceC>(m_LastSelected);


                if (pieceData.ValueRO.chessType == ChessType.Pawn)
                {
                    var boardE = SystemAPI.GetSingletonEntity<ChessBoardC>();
                    var boardAspect = SystemAPI.GetAspect<ChessBoardAspect>(boardE);

                    var color = pieceData.ValueRO.color;

                    if (boardAspect.IsBoardEnd(color, boardAspect.IndexOf(raycastedSocketE)))
                    {
                        var queenPrefab = color == PieceColor.White ?
                            boardAspect.GetWhitePrefabs().queen :
                            boardAspect.GetBlackPrefabs().queen;

                        ecb.DestroyEntity(pieces.pieceE);
                        var instace = ecb.Instantiate(queenPrefab);
                        ecb.SetComponent<ChessPieceC>(instace, new ChessPieceC
                        {
                            chessType = ChessType.Queen,
                            color = color,
                            movedOnce = true
                        });
                        ecb.SetComponent<LocalTransform>(instace, SystemAPI.GetComponent<LocalTransform>(raycastedSocketE));
                        ecb.AddComponent<ChessSocketPieceC>(raycastedSocketE, new ChessSocketPieceC
                        {
                            pieceE = instace
                        });
                    }
                }

                var chessGameState = SystemAPI.GetSingletonRW<ChessGameStateT>();
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
            var state = SystemAPI.GetSingleton<ChessGameStateT>();

            var ray = m_Camera.ScreenPointToRay(Input.mousePosition);
            var raycastedSocketE = Raycast(ray.origin, ray.origin + ray.direction * 200f);

            if (SystemAPI.HasComponent<ChessSocketC>(raycastedSocketE))
            {
                hasCorrectInput = TryMoveChess(raycastedSocketE, ecb);

                if (!hasCorrectInput && SystemAPI.HasComponent<ChessSocketPieceC>(raycastedSocketE))
                {
                    var pieceData = SystemAPI.GetComponent<ChessPieceC>(SystemAPI.GetComponent<ChessSocketPieceC>(raycastedSocketE).pieceE);
                    if (state.turnColor == pieceData.color)
                    {
                        ClearSelection(ecb);
                        hasCorrectInput = true;
                        m_LastSelected = raycastedSocketE;
                    }
                }
            }
        }
    }

    void ClearSelection(EntityCommandBuffer ecb)
    {
        if (SystemAPI.HasComponent<ChessSocketSelectedT>(m_LastSelected))
        {
            ecb.RemoveComponent<ChessSocketSelectedT>(m_LastSelected);
            var asp = SystemAPI.GetAspect<ChessSocketHighlightAspect>(m_LastSelected);
            asp.Destory(ecb);
        }

        foreach (var turn in m_TurnPositions)
        {
            var highlight = SystemAPI.GetAspect<ChessSocketHighlightAspect>(turn.socketE);
            highlight.Destory(ecb);
        }

        m_LastSelected = Entity.Null;
        m_TurnPositions.Clear();
    }

    void LoopMove(int x, int y, int offsetX, int offsetY, ChessBoardAspect boardAspect, in ChessPieceC pieceData)
    {
        while (true)
        {
            x += offsetX;
            y += offsetY;

            bool hasTurn = TryAddTurn(x, y, true, true, boardAspect, m_TurnPositions, pieceData, out bool hasEnemy);

            if (hasEnemy)
                break;

            if (!hasTurn)
                break;
        }
    }

    void RecalculatePossibleTurns(EntityCommandBuffer ecb, Entity selectedE)
    {
        var boardE = SystemAPI.GetSingletonEntity<ChessBoardC>();
        var boardAspect = SystemAPI.GetAspect<ChessBoardAspect>(boardE);

        if (!SystemAPI.HasComponent<ChessSocketC>(selectedE))
            return;

        var socketC = SystemAPI.GetComponent<ChessSocketC>(selectedE);

        if (!SystemAPI.HasComponent<ChessSocketPieceC>(selectedE))
            return;

        var piece = SystemAPI.GetComponent<ChessSocketPieceC>(selectedE);
        var pieceData = SystemAPI.GetComponentRW<ChessPieceC>(piece.pieceE);

        Debug.Log(pieceData.ValueRO.ToString());
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

                TryAddTurn(x + 1, y, true, false, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool hasEnemy1);
                TryAddTurn(x - 1, y, true, false, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool hasEnemy2);

                if (!hasEnemy1 && !hasEnemy2)
                {
                    x = socketC.x;
                    y = socketC.y + offset;

                    if (TryAddTurn(x, y, false, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool hasEnemy))
                    {
                        if (!hasEnemy && !pieceData.ValueRO.movedOnce)
                        {
                            y += offset;
                            TryAddTurn(x, y, false, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out hasEnemy);
                        }
                    }
                }

                break;
            case ChessType.Bishop:

                LoopMove(socketC.x, socketC.y, 1, 1, boardAspect, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, -1, -1, boardAspect, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, -1, 1, boardAspect, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 1, -1, boardAspect, pieceData.ValueRO);

                break;
            case ChessType.Rook:
                LoopMove(socketC.x, socketC.y, 1, 0, boardAspect, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, -1, 0, boardAspect, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 0, 1, boardAspect, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 0, -1, boardAspect, pieceData.ValueRO);
                break;
            case ChessType.Knight:


                TryAddTurn(socketC.x + 2, socketC.y + 1, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(socketC.x + 2, socketC.y - 1, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);

                TryAddTurn(socketC.x - 2, socketC.y + 1, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(socketC.x - 2, socketC.y - 1, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);

                TryAddTurn(socketC.x - 1, socketC.y + 2, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(socketC.x + 1, socketC.y + 2, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);

                TryAddTurn(socketC.x + 1, socketC.y - 2, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(socketC.x - 1, socketC.y - 2, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);

                break;
            case ChessType.Queen:
                LoopMove(socketC.x, socketC.y, 1, 0, boardAspect, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, -1, 0, boardAspect, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 0, 1, boardAspect, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 0, -1, boardAspect, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 1, 1, boardAspect, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, -1, -1, boardAspect, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, -1, 1, boardAspect, pieceData.ValueRO);
                LoopMove(socketC.x, socketC.y, 1, -1, boardAspect, pieceData.ValueRO);

                break;
            case ChessType.King:
                Debug.Log("King Selected");
                x = socketC.x;
                y = socketC.y;

                TryAddTurn(x + 1, y, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x - 1, y, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x + 1, y + 1, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x - 1, y - 1, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x, y + 1, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x, y - 1, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x - 1, y + 1, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);
                TryAddTurn(x + 1, y - 1, true, true, boardAspect, m_TurnPositions, pieceData.ValueRO, out bool _);
                break;
            default:
                break;
        }
    }

    void ShowSelectedAndTurns(EntityCommandBuffer ecb)
    {
        if (SystemAPI.HasComponent<ChessSocketC>(m_LastSelected))
        {
            var highlight = SystemAPI.GetAspect<ChessSocketHighlightAspect>(m_LastSelected);
            ecb.AddComponent<ChessSocketSelectedT>(m_LastSelected);
            highlight.ShowSelected(ecb);
        }

        foreach (var turn in m_TurnPositions)
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
        if (x < 0 || x >= ChessBoardAspect.GRID_X)
        {
            return false;
        }

        if (y < 0 || y >= ChessBoardAspect.GRID_Y)
        {
            return false;
        }

        return true;
    }
    private bool TryAddTurn(int x, int y, bool canBeatEnemy, bool canMoveToEmpty, ChessBoardAspect boardAspect, NativeList<ChessTurnPositions> turns, in ChessPieceC pieceData, out bool hasEnemy)
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
                    socketE = socket.socketE
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
                socketE = socket.socketE
            });
            return true;
        }

        return false;
    }

    private ChessPieceC GetPieceDataFromSlot(Entity slot)
    {
        var pieceE = SystemAPI.GetComponent<ChessSocketPieceC>(slot).pieceE;
        return SystemAPI.GetComponent<ChessPieceC>(pieceE);
    }

    private bool IsEnemy(PieceColor color, PieceColor color1)
    {
        return color1 != color;
    }

    private bool HasPieceInSlot(Entity e)
    {
        return SystemAPI.HasComponent<ChessSocketPieceC>(e);
    }
}