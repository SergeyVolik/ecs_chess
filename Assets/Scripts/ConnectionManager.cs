using System;
using System.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Scenes;
using Unity.Services.Core;
using Unity.Services.Relay.Models;
using Unity.Services.Relay;
using UnityEngine;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using System.Collections.Generic;
using System.Linq;
using Unity.Networking.Transport.Relay;

public enum Result
{
    Success,
    Failed,
}

/// <summary>
/// Necessary wrappers around unsafe functions to convert raw data to various relay structs.
/// </summary>
public static class RelayUtilities
{
    public static RelayServerEndpoint GetEndpointForConnectionType(List<RelayServerEndpoint> endpoints, string connectionType)
    {
        return endpoints.FirstOrDefault(endpoint => endpoint.ConnectionType == connectionType);
    }
}

public class ConnectionManager : MonoBehaviour
{
    private Role m_Role;

    public static World ServerWorld;
    public static World ClientWorld;
    private Allocation m_HostAllocation;
    private JoinAllocation m_JoinAllocation;

    public string JoinCode { get; private set; }

    public Allocation RelayAllocation => m_HostAllocation;
    public static ConnectionManager Instance { get; private set; }
    public RelayServerData ServerData { get; private set; }
    public RelayServerData ClientData { get; private set; }
    public enum Role
    {
        ServerClient, Server, Client
    }

    private async void Awake()
    {
        await UnityServices.InitializeAsync();
        await SignIn();
    }
    public async Task SignIn()
    {
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log($"Signed in. Player ID: {AuthenticationService.Instance.PlayerId}");
    }

    private void Start()
    {
        Instance = this;
    }

    public const string prodocol = "dtls";

    public async Task ConnectToServer(string code, Action<Result> result)
    {
        try
        {
            await JointGameWithCode(code);
            m_Role = Role.Client;
            ClientData = PlayerRelayData(m_JoinAllocation, prodocol);
            StartCoroutine(ConnectECS(result));
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            result?.Invoke(Result.Failed);
        }
    }

    static RelayServerData PlayerRelayData(JoinAllocation allocation, string connectionType = "dtls")
    {
        // Select endpoint based on desired connectionType
        var endpoint = RelayUtilities.GetEndpointForConnectionType(allocation.ServerEndpoints, connectionType);
        if (endpoint == null)
        {
            throw new Exception($"endpoint for connectionType {connectionType} not found");
        }

        // Prepare the server endpoint using the Relay server IP and port
        var serverEndpoint = NetworkEndpoint.Parse(endpoint.Host, (ushort)endpoint.Port);

        // UTP uses pointers instead of managed arrays for performance reasons, so we use these helper functions to convert them
        var allocationIdBytes = RelayAllocationId.FromByteArray(allocation.AllocationIdBytes);
        var connectionData = RelayConnectionData.FromByteArray(allocation.ConnectionData);
        var hostConnectionData = RelayConnectionData.FromByteArray(allocation.HostConnectionData);
        var key = RelayHMACKey.FromByteArray(allocation.Key);

        // Prepare the Relay server data and compute the nonce values
        // A player joining the host passes its own connectionData as well as the host's
        var relayServerData = new RelayServerData(ref serverEndpoint, 0, ref allocationIdBytes, ref connectionData,
            ref hostConnectionData, ref key, connectionType == "dtls");

        return relayServerData;
    }

    // connectionType also supports udp, but this is not recommended
    static RelayServerData HostRelayData(Allocation allocation, string connectionType = "dtls")
    {
        // Select endpoint based on desired connectionType
        var endpoint = RelayUtilities.GetEndpointForConnectionType(allocation.ServerEndpoints, connectionType);
        if (endpoint == null)
        {
            throw new InvalidOperationException($"endpoint for connectionType {connectionType} not found");
        }

        // Prepare the server endpoint using the Relay server IP and port
        var serverEndpoint = NetworkEndpoint.Parse(endpoint.Host, (ushort)endpoint.Port);

        // UTP uses pointers instead of managed arrays for performance reasons, so we use these helper functions to convert them
        var allocationIdBytes = RelayAllocationId.FromByteArray(allocation.AllocationIdBytes);
        var connectionData = RelayConnectionData.FromByteArray(allocation.ConnectionData);
        var key = RelayHMACKey.FromByteArray(allocation.Key);

        // Prepare the Relay server data and compute the nonce value
        // The host passes its connectionData twice into this function
        var relayServerData = new RelayServerData(ref serverEndpoint, 0, ref allocationIdBytes, ref connectionData,
            ref connectionData, ref key, connectionType == "dtls");

        return relayServerData;
    }

    public async void CreateClientServer(Action<Result> result)
    {
        try
        {
            await AllocateHost();
            await RequestJoinCode();
            await JointGameWithCode(JoinCode);

            m_Role = Role.ServerClient;
            ServerData = HostRelayData(m_HostAllocation);
            ClientData = PlayerRelayData(m_JoinAllocation, prodocol);
          
            StartCoroutine(ConnectECS(result));
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            result?.Invoke(Result.Failed);
        }
    }

    /// <summary>
    /// Event handler for when the Join button is clicked.
    /// </summary>
    public async Task JointGameWithCode(string code)
    {
        Debug.Log("Player - Joining host allocation using join code.");

        try
        {
            m_JoinAllocation = await RelayService.Instance.JoinAllocationAsync(code);
            Debug.Log("Player Allocation ID: " + m_JoinAllocation.AllocationId);
        }
        catch (RelayServiceException ex)
        {
            Debug.LogError(ex.Message + "\n" + ex.StackTrace);
        }
    }

    public async Task RequestJoinCode()
    {
        Debug.Log("Host - Getting a join code for my allocation. I would share that join code with the other players so they can join my session.");

        try
        {
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(m_HostAllocation.AllocationId);
            Debug.Log("Host - Got join code: " + JoinCode);
        }
        catch (RelayServiceException ex)
        {
            Debug.LogError(ex.Message + "\n" + ex.StackTrace);
        }
    }

    public async Task AllocateHost()
    {
        Debug.Log("Host - Creating an allocation.");

        // Important: Once the allocation is created, you have ten seconds to BIND
        m_HostAllocation = await RelayService.Instance.CreateAllocationAsync(2, null);

        Debug.Log($"Host Allocation ID: {m_HostAllocation.AllocationId}, region: {m_HostAllocation.Region}");
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

    public IEnumerator ConnectECS(Action<Result> result)
    {
        foreach (var world in World.All)
        {
            if (world.Flags == WorldFlags.Game)
            {
                world.Dispose();
                break;
            }
        }
        var oldConstructor = NetworkStreamReceiveSystem.DriverConstructor;

        if (m_Role == Role.ServerClient || m_Role == Role.Client)
        {         
            NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(new RelayServerData(), ClientData);
            ClientWorld = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;
            World.DefaultGameObjectInjectionWorld = ClientWorld;
        }

        if (m_Role == Role.ServerClient || m_Role == Role.Server)
        {
            NetworkStreamReceiveSystem.DriverConstructor = new RelayDriverConstructor(ServerData, ClientData);
            ServerWorld = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            NetworkStreamReceiveSystem.DriverConstructor = oldConstructor;
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
            query.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(NetworkEndpoint.AnyIpv4);
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

            using var query = ClientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
            query.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(ClientWorld.EntityManager, ClientData.Endpoint);
        }

        result?.Invoke(Result.Success);
    }

    internal void EnableInput()
    {
        var e = ClientWorld.EntityManager.CreateEntity();
        ClientWorld.EntityManager.AddComponent<EnablePlayerInputT>(e);
    }
}
/// <summary>
/// Register client and server using relay server settings.
///
/// Settings are retrieved from bootstrap world. This driver constructor will run when pressing 'Start Game'
/// and should only be pressed after both server and client configuration has been properly initialized.
/// </summary>
public class RelayDriverConstructor : INetworkStreamDriverConstructor
{
    RelayServerData m_RelayClientData;
    RelayServerData m_RelayServerData;

    public RelayDriverConstructor(RelayServerData serverData, RelayServerData clientData)
    {
        m_RelayServerData = serverData;
        m_RelayClientData = clientData;
    }

    /// <summary>
    /// This method will ensure that we only register a UDP driver. This forces the client to always go through the
    /// relay service. In a setup with client-hosted servers it will make sense to allow for IPC connections and
    /// UDP both, which is what invoking
    /// <see cref="DefaultDriverBuilder.RegisterClientDriver(World, ref NetworkDriverStore, NetDebug, ref RelayServerData)"/> will do.
    /// </summary>
    public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
    {
        var settings = DefaultDriverBuilder.GetNetworkSettings();
        settings.WithRelayParameters(ref m_RelayClientData);
        DefaultDriverBuilder.RegisterClientDriver(world, ref driverStore, netDebug, settings);
    }

    public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        DefaultDriverBuilder.RegisterServerDriver(world, ref driverStore, netDebug, ref m_RelayServerData);
#else
            throw new System.NotSupportedException("It is not allowed to create a server NetworkDriver for WebGL build.");
#endif
    }
}
