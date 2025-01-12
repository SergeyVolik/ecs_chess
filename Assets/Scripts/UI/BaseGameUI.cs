using UnityEngine;

public class BaseGameUI : MonoBehaviour
{
    protected virtual void Awake() { }
    internal void Show()
    {
        gameObject.SetActive(true);
    }

    internal void Hide()
    {
        gameObject.SetActive(false);
    }
}
