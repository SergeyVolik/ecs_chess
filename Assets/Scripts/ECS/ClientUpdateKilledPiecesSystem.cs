using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public struct KilledViewInitedT : IComponentData
{
    
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class ClientUpdateKilledPiecesSystem : SystemBase
{
    protected override void OnUpdate()
    {     
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        var ui = UIPages.Instance.gameUi;


        if (SystemAPI.HasSingleton<ChessBoardInstanceC>())
        {
            foreach (var (d, e) in SystemAPI.Query<NetworkStreamInGame>().WithNone<KilledViewInitedT>().WithEntityAccess())
            {
                var boardE = SystemAPI.GetSingletonEntity<ChessBoardInstanceC>();
                var killedPieces = SystemAPI.GetBuffer<KilledPieces>(boardE);

                ui.blackView.Clear();
                ui.whiteView.Clear();
                ecb.AddComponent<KilledViewInitedT>(e);
                foreach (var item1 in killedPieces)
                {
                    if (item1.isWhite)
                    {
                        ui.whiteView.AddPiece(item1.chessType);
                    }
                    else
                    {
                        ui.blackView.AddPiece(item1.chessType);
                    }
                }
            }
        }        
      
        foreach (var (dataC, e) in SystemAPI.Query<AddKilledPiecesRPC>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
        {
            if (dataC.data.isWhite)
            {
                ui.whiteView.AddPiece(dataC.data.chessType);
            }
            else
            {
                ui.blackView.AddPiece(dataC.data.chessType);
            }
            ecb.DestroyEntity(e);
        }

        ecb.Playback(EntityManager);
    }
}
