using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class MenuUI : BaseGameUI
{
    public Button startButton;
    public GameUI gameUI;
    protected override void Awake()
    {
        startButton.onClick.AddListener(() =>
        {
            var e = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntity();
            World.DefaultGameObjectInjectionWorld.EntityManager.AddComponent<ChessGameStartT>(e);
            gameObject.SetActive(false);
            gameUI.Show();
        });

       
    }
}
