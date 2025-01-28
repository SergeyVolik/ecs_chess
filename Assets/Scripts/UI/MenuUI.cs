using UnityEngine;
using UnityEngine.UI;

public class MenuUI : BaseGameUI
{
    public Button startServerButton;
    public Button connectToServerButton;
    public Button pasteButton;
    public Button quitButton;

    public TMPro.TMP_InputField codeInput;
    public BaseGameUI gameUI;
    public CreateGameUI createGameUI;

    public static MenuUI Instance { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        Instance = this;

        quitButton.onClick.AddListener(() =>
        {
            Application.Quit();
        });

        pasteButton.onClick.AddListener(() =>
        {
            codeInput.text = GUIUtility.systemCopyBuffer;
        });

        startServerButton.onClick.AddListener(() =>
        {       
            ConnectionManager.Instance.CreateClientServer((result) => {

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

        connectToServerButton.onClick.AddListener(async () =>
        {
            DisableInput();

            await ConnectionManager.Instance.ConnectToServer(codeInput.text, (result) =>
            {
                EnableInput();

                if (result == Result.Success)
                {
                    Hide();
                    gameUI.Show();
                    ConnectionManager.Instance.EnableInput();
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
