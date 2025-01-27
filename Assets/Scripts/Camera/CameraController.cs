using Cinemachine;
using System;
using Unity.Burst.Intrinsics;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    private Camera m_Camera;

    public static CameraController Instance { get; private set; }

    private CinemachineImpulseSource m_CameraShake;
    [SerializeField]
    private Transform m_CameraTarget;
    [SerializeField]
    private CinemachineVirtualCamera m_VCamera;
    private CinemachineComponentBase m_ComponentCamera;
    public float zoomSensitivity = 10;
    private float cameraDistance;

    public Camera GetCamera() => m_Camera;
    public Transform GetCameraTarget() => m_CameraTarget;

    public Vector3 bodyOffset;
    public Vector3 aimOffset;
    private bool m_IsWhite = true;

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

    public void SetupPlayerCamera(bool isWhite)
    {
        m_IsWhite = isWhite;

        UpdateCameraDistance();
        UpdateBodyOffset();
        UpdateAimOffset();
    }

    void UpdateCameraDistance()
    {
        var body = m_VCamera.GetCinemachineComponent(CinemachineCore.Stage.Body);
        if (body is CinemachineFramingTransposer trans)
        {
            trans.m_CameraDistance = 10;
        }
    }

    private void UpdateAimOffset()
    {
        var aim = m_VCamera.GetCinemachineComponent(CinemachineCore.Stage.Aim);
        if (aim is CinemachineComposer aimComp)
        {
            aimComp.m_TrackedObjectOffset = aimOffset;
        }
    }

    private void UpdateBodyOffset()
    {
        var body = m_VCamera.GetCinemachineComponent(CinemachineCore.Stage.Body);
        if (body is CinemachineFramingTransposer trans)
        {
            var offset = bodyOffset;

            if (m_IsWhite)
            {
                offset.z *= -1;
            }

            trans.m_TrackedObjectOffset = offset;
        }
    }

    private void Update()
    {
        if (Input.GetMouseButton(1))
        {

        }

        ExecuteZoom();

        UpdateBodyOffset();
        UpdateAimOffset();
    }

    private void ExecuteZoom()
    {
        if (m_ComponentCamera == null)
        {
            m_ComponentCamera = m_VCamera.GetCinemachineComponent(CinemachineCore.Stage.Body);
        }

        if (Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            cameraDistance = Input.GetAxis("Mouse ScrollWheel") * zoomSensitivity;

            if (m_ComponentCamera is CinemachineFramingTransposer trans)
            {
                trans.m_CameraDistance -= cameraDistance;
            }
        }
    }
}
