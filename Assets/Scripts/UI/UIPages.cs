using UnityEngine;

public class UIPages : MonoBehaviour
{
    public static UIPages Instance { get; private set; }

    public MainMenuUI mainMenu;
    public PlayOnlineUI playOnline;
    public CreateGameUI createGameUi;
    public GameplayUI gameUi;
    public WinUI winUi;
    public SelectPieceUi selectPiecesUi;
    public InGameMenuUI inGameMenu;

    private void Awake()
    {
        Instance = this;

        mainMenu.Hide();
        playOnline.Hide();
        createGameUi.Hide();
        gameUi.Hide();
        winUi.Hide();
        selectPiecesUi.Hide();
        inGameMenu.Hide();

        mainMenu.Show();
    }
}
