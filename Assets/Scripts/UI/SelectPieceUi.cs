using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UI;

public enum PieceTransformType
{ 
    Queen,
    Rook,
    Bishop,
    Knight
}

public struct PieceTransformationRpc : IRpcCommand
{
    public PieceTransformType type;
}

public struct ShowPieceTransformationUIRpc : IRpcCommand
{
    public bool isWhite;
}

public class SelectPieceUi : BaseGameUI
{
    public Button[] selectQueen;
    public Button[] selectRook;
    public Button[] selectKnight;
    public Button[] selectBishop;

    public GameObject blackGroup;
    public GameObject whiteGroup;

    public GameUI gameUi;

    public static SelectPieceUi Instance { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        Instance = this;
        SetupButtons(selectQueen, PieceTransformType.Queen);
        SetupButtons(selectRook, PieceTransformType.Rook);
        SetupButtons(selectKnight, PieceTransformType.Knight);
        SetupButtons(selectBishop, PieceTransformType.Bishop);
    }

    private void SetupButtons(Button[] buttons, PieceTransformType type)
    {
        foreach (var item in buttons)
        {
            item.onClick.AddListener(() =>
            {
                Hide();
                gameUi.Show();
                RequestTransformationToServer(type);
            });
        }
    }

    public void RequestTransformationToServer(PieceTransformType type)
    {
        var world = ConnectionManager.ClientWorld;

        var e = world.EntityManager.CreateEntity();
        world.EntityManager.AddComponent<SendRpcCommandRequest>(e);
        world.EntityManager.AddComponentData(e, new PieceTransformationRpc
        {
            type = type
        });
    }

    public void Show(bool isWhite)
    {
        Show();
        gameUi.Hide();
        blackGroup.SetActive(!isWhite);
        whiteGroup.SetActive(isWhite);
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class ClientShowSelectPieceUISystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach (var (data, e) in SystemAPI.Query<ShowPieceTransformationUIRpc>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
        {
            ecb.DestroyEntity(e);
            SelectPieceUi.Instance.Show(data.isWhite);
        }

        ecb.Playback(EntityManager);
    }
}