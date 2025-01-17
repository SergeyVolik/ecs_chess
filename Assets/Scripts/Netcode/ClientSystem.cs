using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public struct MessageRPC : IRpcCommand
{
    public FixedString128Bytes message;
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class ClientSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<NetworkId>();
    }

    protected override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SendMessageRPC("RPC", ConnectionManager.ClientWorld);
        }
    }

    public void SendMessageRPC(string message, World world)
    {
        if (world == null || world.IsCreated == false)
            return;

        var entity = world.EntityManager.CreateEntity(typeof(SendRpcCommandRequest), typeof(MessageRPC));
        world.EntityManager.SetComponentData(entity, new MessageRPC
        {
              message = message,
        });
    }
}
