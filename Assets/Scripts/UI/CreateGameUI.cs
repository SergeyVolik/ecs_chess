using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateGameUI : BaseGameUI
{
    public Button goToGameButton;
    public TextMeshProUGUI codeText;
    public Button copyCode;
    public Button returnButton;

    protected override void Awake()
    {
        base.Awake();

        returnButton.onClick.AddListener(() =>
        {
            UIPages.Instance.mainMenu.Show();
            Hide();
            ConnectionManager.Instance.Disconnect();
        });

        copyCode.onClick.AddListener(() =>
        {
            GUIUtility.systemCopyBuffer = ConnectionManager.Instance.JoinCode;
        });

        goToGameButton.onClick.AddListener(() =>
        {
            GameManager.Instance.EnableInput();
            UIPages.Instance.gameUi.Show();
            Hide();
        });
    }

    public void Show(string code)
    {
        Show();

        codeText.text = code;     
    }
}
