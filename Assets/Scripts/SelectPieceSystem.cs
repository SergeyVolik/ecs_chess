using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

//[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Default)]
public partial class SelectPieceSystem : SystemBase
{
    private Camera m_Camera;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<ChessGameStartedT>();
        m_Camera = Camera.main;
    }

    protected override void OnUpdate()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var ray = m_Camera.ScreenPointToRay(Input.mousePosition);
            var targetE = Raycast(ray.origin, ray.origin + ray.direction * 200f);
            Debug.Log("Raycast");
           
            if (SystemAPI.HasComponent<ChessSocketPieceC>(targetE))
            {
                Debug.Log("Has Target");
                var ecb = new EntityCommandBuffer(Allocator.Temp);

                if (SystemAPI.HasSingleton<ChessPieceSelectedT>())
                {
                    var prevSelected = SystemAPI.GetSingletonEntity<ChessPieceSelectedT>();

                    if (targetE == prevSelected)
                        return;

                    var asp = SystemAPI.GetAspect<ChessSocketHighlightAspect>(prevSelected);
                    asp.Destory(ecb);
                    ecb.RemoveComponent<ChessPieceSelectedT>(prevSelected);
                }

                ecb.AddComponent<ChessPieceSelectedT>(targetE);
                var asp1 = SystemAPI.GetAspect<ChessSocketHighlightAspect>(targetE);
                asp1.ShowSelected(ecb);

                ecb.Playback(EntityManager);
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
}