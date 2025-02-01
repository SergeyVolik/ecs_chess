using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public class ChatWindow : MonoBehaviour
{
    public ChatTextItem chatTextItemPrefab;
    public Transform spawnPoint;
    public GameObject chatView;

    public List<ChatTextItem> chatItems = new List<ChatTextItem>();

    public static ChatWindow Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public bool IsShowed() => chatView.activeSelf;
    public void Show(bool show)
    {
        chatView.SetActive(show);
    }

    public void RequestText(string text, EntityCommandBuffer ecb)
    {
        var e = ecb.CreateEntity();
        ecb.AddComponent<SendRpcCommandRequest>(e);
        ecb.AddComponent<AddTextRpc>(e, new AddTextRpc
        {
             chatText = text
        });
    }

    public void ClearChat()
    {
        Debug.Log("Clear chat");
        foreach (var item in chatItems)
        {
            GameObject.Destroy(item.gameObject);
        }
        chatItems.Clear(); 
    }

    public void AddText(string text)
    {
        var item = GameObject.Instantiate(chatTextItemPrefab, spawnPoint);
        item.text.text = text;
        chatItems.Add(item);
    } 
}

public struct AddTextRpc : IRpcCommand
{
    public FixedString128Bytes chatText;
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class ShowChatMessageClientSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (text, e) in SystemAPI.Query<AddTextRpc>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
        {
            ChatWindow.Instance.AddText(text.chatText.ToString());
            ecb.DestroyEntity(e);
        }
        ecb.Playback(EntityManager);       
    }
}