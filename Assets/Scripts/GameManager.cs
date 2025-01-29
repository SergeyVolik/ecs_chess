using System;
using UnityEngine;

public enum GameMode
{
    None,
    Solo,
    Online,
    VsBot
}


public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }
    public GameMode GameMode { get; private set; }
    public void PlaySolo(Action<Result> result)
    {
        GameMode = GameMode.Solo;
        ConnectionManager.Instance.CreateClientServerLocalHost(result);
        GameManager.Instance.EnableInput();
    }

    public void PlayVsBot(Action<Result> result)
    {
        GameMode = GameMode.VsBot;
        GameManager.Instance.EnableInput();
    }

    public void CreateOnlineGame(Action<Result> result)
    {
        GameMode = GameMode.Online;
        ConnectionManager.Instance.CreateClientServerRelay(result);
    }

    public void ConnectOnlineGame(string code, Action<Result> resultCallback)
    {
        GameMode = GameMode.Online;
        ConnectionManager.Instance.ConnectToServer(code, resultCallback);
    }

    internal void EnableInput()
    {
        var e = ConnectionManager.ClientWorld.EntityManager.CreateEntity();
        ConnectionManager.ClientWorld.EntityManager.AddComponent<EnablePlayerInputT>(e);
    }
}
