using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplayUI : BaseGameUI
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
            Debug.Log("Pause");
            UIPages.Instance.inGameMenu.Show();
            Hide();
        }
    }
}
