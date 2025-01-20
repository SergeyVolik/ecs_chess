using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateGameUI : BaseGameUI
{
    public Button goToGameButton;
    public TextMeshProUGUI codeText;
    public Button copyCode;
    public Button returnButton;

    public GameUI gameUI;
    public BaseGameUI menuUI;

    protected override void Awake()
    {
        base.Awake();

        returnButton.onClick.AddListener(() =>
        {
            menuUI.Show();
            Hide();
        });

        copyCode.onClick.AddListener(() =>
        {
            GUIUtility.systemCopyBuffer = ConnectionManager.Instance.JoinCode;
        });

        goToGameButton.onClick.AddListener(() =>
        {
            gameUI.Show();
            Hide();
        });
    }

    public void CreateGame()
    {
        Show();
        codeText.text = "Creating Game...";
        copyCode.interactable = false;
        goToGameButton.interactable = false;
        returnButton.interactable = false;
        ConnectionManager.Instance.CreateClientServer((result) => {

            returnButton.interactable = true;

            if (result == Result.Failed)
            {
                Debug.Log("Fail to create Game");
                return;
            }
          
            codeText.text = ConnectionManager.Instance.JoinCode;
            copyCode.interactable = true;
            goToGameButton.interactable = true;
        });       
    }
}
