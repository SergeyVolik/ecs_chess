using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : BaseGameUI
{
    public Button quitButton;
    public Button playOnlineButton;
    public Button playSoloButton;
    public Button playVsBotButton;

    protected override void Awake()
    {
        base.Awake();

        quitButton.onClick.AddListener(() =>
        {
            Application.Quit();
        });

        playOnlineButton.onClick.AddListener(() =>
        {
            Hide();
            UIPages.Instance.playOnline.Show();
        });

        playSoloButton.onClick.AddListener(() =>
        {
            GameManager.Instance.PlaySolo((_) => {
                Hide();
                UIPages.Instance.gameUi.Show();
            });      
        });

        playVsBotButton.onClick.AddListener(() =>
        {
           
        });
    }
}
