using Unity.Entities;
using Unity.NetCode;
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

            if (GameManager.Instance.GameMode != GameMode.Solo && turnIsWhite != playerData.isWhite)
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
                var rpc = new MoveChessRpc
                {
                    rayFrom = ray.origin,
                    rayTo = ray.origin + ray.direction * 200f,
                };
                var request = EntityManager.CreateEntity();
                EntityManager.AddComponent<SendRpcCommandRequest>(request);
                EntityManager.AddComponentData<MoveChessRpc>(request, rpc);
            }

            if (Input.GetMouseButtonUp(0))
            {
                var request = EntityManager.CreateEntity();
                var rpc = new DropChessRpc
                {
                    rayFrom = ray.origin,
                    rayTo = ray.origin + ray.direction * 200f,
                };
                EntityManager.AddComponent<SendRpcCommandRequest>(request);
                EntityManager.AddComponentData<DropChessRpc>(request, rpc);
            }
        }
    }
}