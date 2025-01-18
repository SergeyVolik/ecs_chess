using UnityEngine.UI;

public class MenuUI : BaseGameUI
{
    public Button startServerButton;
    public Button connectToServerButton;

    public GameUI gameUI;
    protected override void Awake()
    {
        base.Awake();
        startServerButton.onClick.AddListener(() =>
        {
            ConnectionManager.Instance.CreateClientServer();
            Hide();
            gameUI.Show();
        });

        connectToServerButton.onClick.AddListener(() =>
        {
            ConnectionManager.Instance.ConnectToServer();
            Hide();
            gameUI.Show();
        });
    }
}
