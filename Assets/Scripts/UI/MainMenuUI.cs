using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : BaseGameUI
{
    public Button quitButton;
    public Button playOnlineButton;
    public Button playSoloButton;
    public Button playVsBotButton;

    public PlayOnlineUI playOnlineUI;
    public GameUI gameUI;

    public static MainMenuUI Instance { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        Instance = this;

        quitButton.onClick.AddListener(() =>
        {
            Application.Quit();
        });

        playOnlineButton.onClick.AddListener(() =>
        {
            Hide();
            playOnlineUI.Show();
        });

        playSoloButton.onClick.AddListener(() =>
        {
            GameManager.Instance.PlaySolo((_) => {
                Hide();
                gameUI.Show();
            });      
        });

        playVsBotButton.onClick.AddListener(() =>
        {
           
        });
    }
}
