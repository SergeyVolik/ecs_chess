using TMPro;
using UnityEngine.UI;

public class WinUI : BaseGameUI
{
    public Button leaveButton;
    
    public TextMeshProUGUI winText;

    public void ShowWin(string text)
    {
        winText.text = text;
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
