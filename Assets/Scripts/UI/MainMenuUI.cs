using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : BaseGameUI
{
    public Button quitButton;
    public Button playOnlineButton;
    public Button playSoloButton;
    public Button playVsBotButton;

    public PlayOnlineUI playOnlineUI;

    protected override void Awake()
    {
        base.Awake();

        quitButton.onClick.AddListener(() =>
        {
            Application.Quit();
        });

        playOnlineButton.onClick.AddListener(() =>
        {
            playOnlineUI.Show();
        });

        playSoloButton.onClick.AddListener(() =>
        {
           
        });

        playVsBotButton.onClick.AddListener(() =>
        {
           
        });
    }
}
