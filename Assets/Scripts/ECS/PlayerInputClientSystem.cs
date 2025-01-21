using System.Data;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct RaycastChessRpc : IRpcCommand
{
    public float3 rayFrom;
    public float3 rayTo;
}
public struct EnablePlayerInputT : IRpcCommand
{

}
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
        if (Input.GetMouseButtonDown(0))
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

            var ray = CameraData.Instance.GetCamera().ScreenPointToRay(Input.mousePosition);
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