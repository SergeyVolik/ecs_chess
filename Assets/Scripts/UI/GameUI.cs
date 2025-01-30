using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : BaseGameUI
{
    public TextMeshProUGUI timeText;

    protected override void Awake()
    {
        base.Awake();

    }

    private void Update()
    {
        if (IsShowed && Input.GetKeyDown(KeyCode.Escape))
        {
            UIPages.Instance.inGameMenu.Show();
            Hide();
        }
    }
}
