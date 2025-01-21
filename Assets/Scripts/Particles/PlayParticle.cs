using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

public enum ParticleType
{
    Kill
}

public struct ShowParticleRpc : IRpcCommand
{
    public float3 pos;
    public ParticleType type;
}

public class PlayParticle : MonoBehaviour
{
    public ParticleSystem killParticle;

    public static PlayParticle Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public void PlayRequest(float3 pos, ParticleType type, EntityCommandBuffer ecb)
    {
        var e = ecb.CreateEntity();
        ecb.AddComponent<SendRpcCommandRequest>(e);
        ecb.AddComponent<ShowParticleRpc>(e, new ShowParticleRpc
        {
            pos = pos,
            type = type,
        });
    }

    public void PlayAtPos(float3 pos, ParticleType type)
    {
        killParticle.Play();
        killParticle.transform.position = pos;
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class PlayParticleCliendSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (p, e) in SystemAPI.Query<ShowParticleRpc>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
        {
            ecb.DestroyEntity(e);
            PlayParticle.Instance.PlayAtPos(p.pos, p.type);
        }

        ecb.Playback(EntityManager);
    }
}