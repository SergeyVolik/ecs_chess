using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : BaseGameUI
{
    public MenuUI menuUI;

    public static GameUI Instance { get; private set; }

    public TextMeshProUGUI timeText;
    public InGameMenuUI inGameMenuUI;
    protected override void Awake()
    {
        base.Awake();
        Instance = this;
    }

    private void Update()
    {
        if (IsShowed && Input.GetKeyDown(KeyCode.Escape))
        {
            inGameMenuUI.Show();
            Hide();
        }
    }
}
