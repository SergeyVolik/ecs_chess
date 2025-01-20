using UnityEngine;
using UnityEngine.UI;

public class MenuUI : BaseGameUI
{
    public Button startServerButton;
    public Button connectToServerButton;
    public Button pasteButton;
    
    public TMPro.TMP_InputField codeInput;
    public BaseGameUI gameUI;
    public CreateGameUI createGameUI;

    protected override void Awake()
    {
        base.Awake();

        pasteButton.onClick.AddListener(() =>
        {
            codeInput.text = GUIUtility.systemCopyBuffer;
        });

        startServerButton.onClick.AddListener(() =>
        {
            Hide();
            createGameUI.CreateGame();
        });

        connectToServerButton.onClick.AddListener(async () =>
        {
            connectToServerButton.interactable = false;
            pasteButton.interactable = false;
            startServerButton.interactable = false;
            codeInput.interactable = false;

            await ConnectionManager.Instance.ConnectToServer(codeInput.text, (result) => {
                if (result == Result.Success)
                {
                    codeInput.interactable = true;
                    connectToServerButton.interactable = true;
                    pasteButton.interactable = true;
                    startServerButton.interactable = true;
                    Hide();
                    gameUI.Show();
                    ConnectionManager.Instance.EnableInput();
                }
            });
        });
    }
}
