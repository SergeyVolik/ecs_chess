using UnityEngine;

public class UIPages : MonoBehaviour
{
    public static UIPages Instance { get; private set; }

    public MainMenuUI mainMenu;
    public PlayOnlineUI playOnline;
    public CreateGameUI createGameUi;
    public GameUI gameUi;
    public WinUI winUi;
    public SelectPieceUi selectPiecesUi;
    public InGameMenuUI inGameMenu;

    private void Awake()
    {
        Instance = this;
    }
}
