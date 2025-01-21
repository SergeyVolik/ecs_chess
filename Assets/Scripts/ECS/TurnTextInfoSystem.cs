using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class TurnTextInfoSystem : SystemBase
{
    bool whiteTurn;

    protected override void OnCreate()
    {
        base.OnCreate();
        RequireForUpdate<ChessBoardTurnC>();
        RequireForUpdate<ChessPlayerC>();
        RequireForUpdate<NetworkId>();

    }

    protected override void OnUpdate()
    {
        if (!GameUI.Instance)
            return;

        var localPlayerE = SystemAPI.GetSingletonEntity<NetworkId>();

    
        var localPlayer = SystemAPI.GetComponent<ChessPlayerC>(localPlayerE);

        var turn = SystemAPI.GetSingleton<ChessBoardTurnC>();

        bool isYourStep = turn.isWhite == localPlayer.isWhite;

        var canvasTransform = GameUI.Instance.yourTurnText.transform.parent.transform;
        canvasTransform.gameObject.SetActive(isYourStep);
        if (whiteTurn != isYourStep)
        {
            whiteTurn = !whiteTurn;
            var uiPoints = SystemAPI.GetSingleton<ChessBoardUIPoints>();

            var posE = isYourStep ? uiPoints.whiteTurnUiPoint : uiPoints.blackTurnUiPoint;
            var ltw = SystemAPI.GetComponent<LocalTransform>(posE);
        
            canvasTransform.position = ltw.Position;
            canvasTransform.rotation = ltw.Rotation;
        }
    }
}
