using UnityEngine;
using UnityEngine.UI;

public class PlayOnlineUI : BaseGameUI
{
    public Button startServerButton;
    public Button connectToServerButton;
    public Button pasteButton;

    public TMPro.TMP_InputField codeInput;
    public BaseGameUI gameUI;
    public CreateGameUI createGameUI;
    public MainMenuUI mainMenuUI;

    public Button returnButton;

    protected override void Awake()
    {
        base.Awake();

        returnButton.onClick.AddListener(() =>
        {
            mainMenuUI.Show();
            Hide();
        });

        pasteButton.onClick.AddListener(() =>
        {
            codeInput.text = GUIUtility.systemCopyBuffer;
        });

        startServerButton.onClick.AddListener(() =>
        {
            GameManager.Instance.CreateOnlineGame((result) =>
            {

                DisableInput();
                if (result == Result.Failed)
                {
                    Debug.Log("Fail to create Game");
                    EnableInput();
                    return;
                }

                EnableInput();
                Hide();
                createGameUI.Show(ConnectionManager.Instance.JoinCode);
            });
        });

        connectToServerButton.onClick.AddListener(() =>
        {
            DisableInput();

            GameManager.Instance.ConnectOnlineGame(codeInput.text, (result) =>
            {
                EnableInput();

                if (result == Result.Success)
                {
                    Hide();
                    gameUI.Show();
                    GameManager.Instance.EnableInput();
                }
            });
        });
    }

    private void EnableInput()
    {
        codeInput.interactable = true;
        connectToServerButton.interactable = true;
        pasteButton.interactable = true;
        startServerButton.interactable = true;
    }

    private void DisableInput()
    {
        connectToServerButton.interactable = false;
        pasteButton.interactable = false;
        startServerButton.interactable = false;
        codeInput.interactable = false;
    }
}
