using TMPro;
using UnityEngine.UI;

public class WinUI : BaseGameUI
{
    public Button leaveButton;
    
    public TextMeshProUGUI winText;
    public GameUI gameUi;
    public MenuUI menuUi;

    public static WinUI Instance { get; private set; }

    public void ShowWin(string text)
    {
        gameUi.Hide();
        winText.text = text;
        Show();
    }

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        leaveButton.onClick.AddListener(() =>
        {
            ConnectionManager.Instance.Disconnect();
            Hide();
            menuUi.Show();
        });
    }
}
