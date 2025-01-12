using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class GameUI : BaseGameUI
{
    public Button startButton;


    protected override void Awake()
    {
        startButton.onClick.AddListener(() =>
        {
            var e = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntity();
            World.DefaultGameObjectInjectionWorld.EntityManager.AddComponent<ChessStartGameT>(e);
        });
    }
}
