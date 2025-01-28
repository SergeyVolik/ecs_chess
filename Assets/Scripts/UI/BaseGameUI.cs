using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(GraphicRaycaster))]
public class BaseGameUI : MonoBehaviour
{
    Canvas m_Canvas;
    GraphicRaycaster m_GraphicRaycaster;

    public bool IsShowed => m_Canvas.enabled;
    protected virtual void Awake()
    {
        m_Canvas = GetComponent<Canvas>();
        m_GraphicRaycaster = GetComponent<GraphicRaycaster>();
    }

    public virtual void Show()
    {
        m_Canvas.enabled = true;
        m_GraphicRaycaster.enabled = true;
    }

    public virtual void Hide()
    {
        m_Canvas.enabled = false;
        m_GraphicRaycaster.enabled = false;
    }
}
