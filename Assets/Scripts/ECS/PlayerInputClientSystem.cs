using System.Data;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class PlayerInputClientSystem : SystemBase
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<ChessBoardInstanceT>();
        RequireForUpdate<EnablePlayerInputT>();
    }

    protected override void OnUpdate()
    {
        bool hasInput = Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0);
        if (hasInput)
        {
            var board = SystemAPI.GetSingleton<ChessBoardInstanceT>();

            if (board.blockInput)
                return;

            var localPlayerEntity = SystemAPI.GetSingletonEntity<NetworkId>();

            if (!SystemAPI.HasComponent<ChessPlayerC>(localPlayerEntity))
            {
                return;
            }

            var turn = SystemAPI.GetSingleton<ChessBoardTurnC>();

            var playerData = SystemAPI.GetComponent<ChessPlayerC>(localPlayerEntity);

            bool turnIsWhite = turn.isWhite;

            if (turnIsWhite != playerData.isWhite)
                return;

            var ray = CameraController.Instance.GetCamera().ScreenPointToRay(Input.mousePosition);

            if (Input.GetMouseButtonDown(0))
            {              
                var rpc = new GrabChessRpc
                {
                    rayFrom = ray.origin,
                    rayTo = ray.origin + ray.direction * 200f,
                };
                var request = EntityManager.CreateEntity();
                EntityManager.AddComponent<SendRpcCommandRequest>(request);
                EntityManager.AddComponentData<GrabChessRpc>(request, rpc);
            }

            if (Input.GetMouseButton(0))
            {
                var rpc1 = new MoveChessRpc
                {
                    rayFrom = ray.origin,
                    rayTo = ray.origin + ray.direction * 200f,
                };
                var request1 = EntityManager.CreateEntity();
                EntityManager.AddComponent<SendRpcCommandRequest>(request1);
                EntityManager.AddComponentData<MoveChessRpc>(request1, rpc1);
            }

            if (Input.GetMouseButtonUp(0))
            {
                var rpc1 = new DropChessRpc
                {
                    rayFrom = ray.origin,
                    rayTo = ray.origin + ray.direction * 200f,
                };
                var request1 = EntityManager.CreateEntity();
                EntityManager.AddComponent<SendRpcCommandRequest>(request1);
                EntityManager.AddComponentData<DropChessRpc>(request1, rpc1);
            }
        }
    }
}