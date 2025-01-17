using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class MenuUI : BaseGameUI
{
    public Button startServerButton;
    public Button connectToServerButton;

    public GameUI gameUI;
    protected override void Awake()
    {
        startServerButton.onClick.AddListener(() =>
        {
            ConnectionManager.Instance.CreateClientServer();
            gameObject.SetActive(false);
            gameUI.Show();
        });

        connectToServerButton.onClick.AddListener(() =>
        {
            ConnectionManager.Instance.ConnectToServer();
            gameObject.SetActive(false);
            gameUI.Show();
        });
    }
}
