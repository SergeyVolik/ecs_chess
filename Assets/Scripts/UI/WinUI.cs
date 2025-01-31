using TMPro;
using UnityEngine.UI;

public class WinUI : BaseGameUI
{
    public Button leaveButton;
    
    public TextMeshProUGUI winText;
    public TextMeshProUGUI reasonText;

    public void ShowWin(string text, string reason)
    {
        winText.text = text;
        reasonText.text = reason;
        Show();
    }

    protected override void Awake()
    {
        base.Awake();
        leaveButton.onClick.AddListener(() =>
        {
            ConnectionManager.Instance.Disconnect();
            UIPages.Instance.mainMenu.Show();
        });
    }
}
