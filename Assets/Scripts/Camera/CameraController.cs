using Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    private Camera m_Camera;

    public static CameraController Instance { get; private set; }

    private CinemachineImpulseSource m_CameraShake;
    [SerializeField]
    private Transform m_CameraTarget;

    public Camera GetCamera() => m_Camera;
    public Transform GetCameraTarget() => m_CameraTarget;
    private void Awake()
    {
        m_Camera = Camera.main;
        Instance = this;

        m_CameraShake = GetComponent<CinemachineImpulseSource>();
    }

    public void ShakeCamera()
    {
        m_CameraShake.GenerateImpulse();
    }
}
