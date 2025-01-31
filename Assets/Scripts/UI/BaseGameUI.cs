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

    private static BaseGameUI s_PrevShowed;
    public virtual void Show()
    {
        if (s_PrevShowed)
        {
            s_PrevShowed.Hide();
        }

        s_PrevShowed = this;
        enabled = true;
        m_Canvas.enabled = true;
        m_GraphicRaycaster.enabled = true;
    }

    public virtual void Hide()
    {
        enabled = false;
        m_Canvas.enabled = false;
        m_GraphicRaycaster.enabled = false;
    }
}
