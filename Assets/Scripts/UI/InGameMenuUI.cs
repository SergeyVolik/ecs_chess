using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InGameMenuUI : BaseGameUI
{
    public Button continueButton;
    public Button leaveButton;
    public Button copyButton;

    public TextMeshProUGUI enterCode;

    public GameUI gameUI;
    public MainMenuUI menuUI;

    protected override void Awake()
    {
        base.Awake();

        copyButton.onClick.AddListener(() =>
        {
            GUIUtility.systemCopyBuffer = ConnectionManager.Instance.JoinCode;
        });

        leaveButton.onClick.AddListener(() =>
        {
            ConnectionManager.Instance.Disconnect();
            Hide();
            menuUI.Show();
        });

        continueButton.onClick.AddListener(() =>
        {
            Continue();
        });
    }

    private void Continue()
    {
        Hide();
        GameManager.Instance.EnableInput();
        gameUI.Show();
    }

    public override void Show()
    {
        base.Show();
        GameManager.Instance.DisableInput();
        enterCode.text = ConnectionManager.Instance.JoinCode;
    }

    private void Update()
    {
        if (IsShowed && Input.GetKeyDown(KeyCode.Escape))
        {
            Continue();
        }
    }
}
