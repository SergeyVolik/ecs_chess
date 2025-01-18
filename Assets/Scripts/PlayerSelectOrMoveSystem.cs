using System.Data;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct MoveChess : IComponentData
{
    public int fromIndex;
    public int toIndex;
}

public struct RaycastChessRpc : IRpcCommand
{
    public float3 rayFrom;
    public float3 rayTo;
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class PlayerInputClientSystem : SystemBase
{
    private Camera m_Camera;
    protected override void OnCreate()
    {
        base.OnCreate();
        m_Camera = Camera.main;

    }

    protected override void OnUpdate()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var localPlayerEntity = SystemAPI.GetSingletonEntity<NetworkId>();

            if (!SystemAPI.HasComponent<ChessPlayerC>(localPlayerEntity))
            {
                return;
            }

            var turn = SystemAPI.GetSingleton<ChessBoardTurnC>();

            var playerData = SystemAPI.GetComponent<ChessPlayerC>(localPlayerEntity);

            bool turnIsWhite = turn.turnColor == PieceColor.White;

            if (turnIsWhite != playerData.white)
                return;

            var ray = m_Camera.ScreenPointToRay(Input.mousePosition);
            var rpc = new RaycastChessRpc
            {
                rayFrom = ray.origin,
                rayTo = ray.origin + ray.direction * 200f,
            };
            var request = EntityManager.CreateEntity();
            EntityManager.AddComponent<SendRpcCommandRequest>(request);
            EntityManager.AddComponentData<RaycastChessRpc>(request, rpc);
        }
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class PlayerSelectOrMoveSystem : SystemBase
{
    private Camera m_Camera;
    private Entity m_LastSelectedSocket;
    private Entity m_LastSelectedPieceE;

    protected override void OnCreate()
    {
        m_Camera = Camera.main;
        RequireForUpdate<ChessBoardInstanceT>();
        RequireForUpdate<ChessBoardTurnC>();
        RequireForUpdate<NetworkId>();
    }

    protected override void OnUpdate()
    {

        var quety = SystemAPI.QueryBuilder().WithAll<RaycastChessRpc>().Build();

        if (quety.IsEmpty)
            return;

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

            var requestE = ecb.CreateEntity();
            ecb.AddComponent<MoveChess>(requestE, new MoveChess
            {
                fromIndex = board.IndexOf(m_LastSelectedSocket),
                toIndex = board.IndexOf(raycastedSocketE),
            });
            ClearSelection(ecb);
        }
        else if (isSelected)
        {
            Debug.Log("[Client] select chess");
            ShowSelectedAndTurns(ecb);
        }

        ecb.Playback(EntityManager);
    }
    ChessBoardInstanceAspect GetBoard()
    {
        var boardE = SystemAPI.GetSingletonEntity<ChessBoardInstanceT>();
        return SystemAPI.GetAspect<ChessBoardInstanceAspect>(boardE);
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

    private bool HasPieceInSlot(Entity e)
    {
        return SystemAPI.HasComponent<ChessPieceC>(SystemAPI.GetComponentRO<ChessSocketPieceLinkC>(e).ValueRO.pieceE);
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
    DynamicBuffer<ChessPiecePossibleSteps> GetPossibleSteps(Entity pieceE)
    {
        return SystemAPI.GetBuffer<ChessPiecePossibleSteps>(pieceE);
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
}