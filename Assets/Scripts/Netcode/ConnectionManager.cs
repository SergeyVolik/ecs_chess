using System.Collections;
using System.Collections.Generic;
using System.Net;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Scenes;
using UnityEngine;
using static ConnectionManager;

public class ConnectionManager : MonoBehaviour
{
    [SerializeField] private string m_ListenIp = "127.0.0.1";
    [SerializeField] private string m_ConnectIp = "127.0.0.1";

    [SerializeField] private ushort m_Port = 7979;
    private Role m_Role;

    public static World ServerWorld;
    public static World ClientWorld;

    public static ConnectionManager Instance { get; private set; }

    public enum Role
    {
        ServerClient, Server, Client
    }


    private void Start()
    {
        Instance = this;
    }

    public void ConnectToServer()
    {
        m_Role = Role.Client;
        StartCoroutine(Connect());
    }

    public void CreateClientServer()
    {
        m_Role = Role.ServerClient;
        StartCoroutine(Connect());
    }

    public void Disconnect()
    {
        foreach (var world in World.All)
        {
            if (world.Flags == WorldFlags.GameClient || world.Flags == WorldFlags.GameThinClient || world.Flags == WorldFlags.GameServer)
            {
                world.Dispose();
                break;
            }
        }
    }

    public IEnumerator Connect()
    {
        foreach (var world in World.All)
        {
            if (world.Flags == WorldFlags.Game)
            {
                world.Dispose();
                break;
            }
        }

        if (m_Role == Role.ServerClient || m_Role == Role.Client)
        {
            ClientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            World.DefaultGameObjectInjectionWorld = ClientWorld;
        }

        if (m_Role == Role.ServerClient || m_Role == Role.Server)
        {
            ServerWorld = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            World.DefaultGameObjectInjectionWorld = ServerWorld;
        }

        SubScene[] subeScenes = FindObjectsByType<SubScene>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (ServerWorld != null)
        {
            while (!ServerWorld.IsCreated)
            {
                yield return null;
            }

            for (int i = 0; i < subeScenes.Length; i++)
            {
                SceneSystem.LoadParameters loadParams = new SceneSystem.LoadParameters() { Flags = SceneLoadFlags.BlockOnStreamIn };
                var sceneEntity = SceneSystem.LoadSceneAsync(ServerWorld.Unmanaged, new Unity.Entities.Hash128(subeScenes[i].SceneGUID.Value), loadParams);

                while (!SceneSystem.IsSceneLoaded(ServerWorld.Unmanaged, sceneEntity))
                {
                    ServerWorld.Update();
                }
            }

            using var query = ServerWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            query.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(NetworkEndpoint.Parse(m_ListenIp, m_Port));
        }

        if (ClientWorld != null)
        {
            while (!ClientWorld.IsCreated)
            {
                yield return null;
            }

            for (int i = 0; i < subeScenes.Length; i++)
            {
                SceneSystem.LoadParameters loadParams = new SceneSystem.LoadParameters() { Flags = SceneLoadFlags.BlockOnStreamIn };
                var sceneEntity = SceneSystem.LoadSceneAsync(ClientWorld.Unmanaged, new Unity.Entities.Hash128(subeScenes[i].SceneGUID.Value), loadParams);

                while (!SceneSystem.IsSceneLoaded(ClientWorld.Unmanaged, sceneEntity))
                {
                    ClientWorld.Update();
                }
            }

            IPAddress serverAddress = IPAddress.Parse(m_ConnectIp);
            var serverAddressBytes = serverAddress.GetAddressBytes();
            NativeArray<byte> naAddress = new NativeArray<byte>(serverAddressBytes.Length, Allocator.Temp);
            naAddress.CopyFrom(serverAddressBytes);
            NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4;
            endpoint.SetRawAddressBytes(naAddress);
            endpoint.Port = m_Port;
            using var query = ClientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            query.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(ClientWorld.EntityManager, endpoint);
        }
    }
}
