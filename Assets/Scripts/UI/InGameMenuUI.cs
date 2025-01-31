using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InGameMenuUI : BaseGameUI
{
    public Button continueButton;
    public Button leaveButton;
    public Button copyButton;
    public Button surrenderButton;
    public Button drawButton;

    public TextMeshProUGUI enterCode;

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
            UIPages.Instance.mainMenu.Show();
        });

        continueButton.onClick.AddListener(() =>
        {
            Continue();
        });

        surrenderButton.onClick.AddListener(() =>
        {
            Debug.Log("[Client] Surrender");
            GameManager.Instance.RequestSurrender();
        });

        drawButton.onClick.AddListener(() =>
        {
            GameManager.Instance.RequestDraw();
        });
    }

    private void Continue()
    {
        Hide();
        GameManager.Instance.EnableInput();
        UIPages.Instance.gameUi.Show();
    }

    public override void Show()
    {
        base.Show();

        copyButton.gameObject.SetActive(GameManager.Instance.GameMode == GameMode.Online);
        //surrenderButton.gameObject.SetActive(GameManager.Instance.GameMode == GameMode.Online);
        drawButton.gameObject.SetActive(GameManager.Instance.GameMode == GameMode.Online);

        GameManager.Instance.DisableInput();
        enterCode.text = ConnectionManager.Instance.JoinCode;
    }

    private void Update()
    {
        if (IsShowed && Input.GetKeyDown(KeyCode.Escape))
        {
            Debug.Log("Continue");
            Continue();
        }
    }
}
