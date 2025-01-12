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
        RequireForUpdate<ChessGameStateT>();
        m_Camera = Camera.main;
    }

    protected override void OnUpdate()
    {

    }

  
}