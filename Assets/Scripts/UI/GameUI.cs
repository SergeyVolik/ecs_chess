using TMPro;
using UnityEngine.UI;

public class GameUI : BaseGameUI
{
    public Button leaveButton;
    public MenuUI menuUI;

    public static GameUI Instance { get; private set; }

    public TextMeshProUGUI timeText;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        leaveButton.onClick.AddListener(() =>
        {
            ConnectionManager.Instance.Disconnect();
            Hide();
            menuUI.Show();
        });
    }
}
