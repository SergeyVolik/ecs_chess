using Cinemachine;
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
    public float rotateSensitivity = 10;
    public float moveSensitivity = 10;

    private float cameraDistance;

    public Camera GetCamera() => m_Camera;
    public Transform GetCameraTarget() => m_CameraTarget;

    public Vector3 bodyOffset;
    public Vector3 aimOffset;
    public float aimRotationX;

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
        var currentAimOffset = aimOffset;
        if (isWhite)
            currentAimOffset.z *= -1;
        m_CameraTarget.position = currentAimOffset;

        m_CameraTarget.rotation = Quaternion.Euler(aimRotationX, isWhite ? 0 : 180, 0);
        UpdateCameraDistance();
        UpdateBodyOffset();
    }

    void UpdateCameraDistance()
    {
        var body = m_VCamera.GetCinemachineComponent(CinemachineCore.Stage.Body);
        if (body is CinemachineFramingTransposer trans)
        {
            trans.m_CameraDistance = 10;
        }
    }

    private void UpdateBodyOffset()
    {
        var body = m_VCamera.GetCinemachineComponent(CinemachineCore.Stage.Body);
        if (body is CinemachineFramingTransposer trans)
        {
            var offset = bodyOffset;
            trans.m_TrackedObjectOffset = offset;
        }
    }

    private void Update()
    {
        ExecuteZoom();
        ExecuteMove();
        ExecuteRotate();

        UpdateBodyOffset();
    }

    private void ExecuteRotate()
    {
        if (Input.GetMouseButton(1))
        {
            var x = Input.GetAxis("Mouse X");
            var y = Input.GetAxis("Mouse Y");

            m_CameraTarget.Rotate(Vector3.right, y * rotateSensitivity);
            m_CameraTarget.Rotate(Vector3.up, x * rotateSensitivity, Space.World);
        }
    }

    private void ExecuteMove()
    {
        if (Input.GetKey(KeyCode.Mouse2))
        {
            var x = Input.GetAxis("Mouse X");
            var y = Input.GetAxis("Mouse Y");

            m_CameraTarget.position += new Vector3(x, 0, y) * moveSensitivity;
        }
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
