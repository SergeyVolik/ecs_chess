using UnityEngine.UI;

public class GameUI : BaseGameUI
{
    public Button leaveButton;
    public MenuUI menuUI;

    protected override void Awake()
    {
        leaveButton.onClick.AddListener(() =>
        {
            ConnectionManager.Instance.Disconnect();
            Hide();
            menuUI.Show();
        });
    }
}
