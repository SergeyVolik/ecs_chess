using Cinemachine;
using Cinemachine.Utility;
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

    private float rotY;
    private float rotX;
    private Vector3 m_TouchStart;
    private bool m_IsMoving;
    private bool m_IsRotation;

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
        var currentAimOffset = aimOffset;
        if (isWhite)
            currentAimOffset.z *= -1;
        m_CameraTarget.position = currentAimOffset;

        rotX = aimRotationX;
        rotY = isWhite ? 0 : 180;
        UpdateRotation();
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
        if (Input.GetMouseButtonDown(1))
        {
            m_IsRotation = true;
            m_IsMoving = false;
        }

        if (Input.GetMouseButton(1) && m_IsRotation)
        {
            var x = Input.GetAxis("Mouse X");
            var y = Input.GetAxis("Mouse Y");

            rotY += x * rotateSensitivity;
            rotX -= y * rotateSensitivity;

            //rotY = Mathf.Clamp(rotY, -90f, 90f);
            rotX = Mathf.Clamp(rotX, 0f, 90f);

            UpdateRotation();
        }

        if (Input.GetMouseButtonUp(1))
        {
            m_IsRotation = false;
        }
    }

    private void UpdateRotation()
    {
        m_CameraTarget.eulerAngles = new Vector3(rotX, rotY, 0);
    }

    private void ExecuteMove()
    {
        if (Input.GetKeyDown(KeyCode.Mouse2))
        {
            m_TouchStart = GetWorldPosition(0);
            m_IsMoving = true;
            m_IsRotation = false;
        }

        if (Input.GetKey(KeyCode.Mouse2) && m_IsMoving)
        {

            var moveDelta = m_TouchStart - GetWorldPosition(0);

            var x = Input.GetAxis("Mouse X");
            var y = Input.GetAxis("Mouse Y");

            var vector = Vector3.ProjectOnPlane(-m_Camera.transform.forward, Vector3.up).normalized * y;
            var vector2 = Vector3.ProjectOnPlane(-m_Camera.transform.right, Vector3.up).normalized * x;
            var delta = GetWorldPosition(0);
            m_CameraTarget.position += moveDelta;
        }

        if (Input.GetKeyUp(KeyCode.Mouse2))
        {
            m_IsMoving = false;
        }
    }
    private Vector3 GetWorldPosition(float z)
    {
        Ray mousePos = m_Camera.ScreenPointToRay(Input.mousePosition);
        Plane ground = new Plane(Vector3.up, new Vector3(0, 0, z));
        float distance;
        ground.Raycast(mousePos, out distance);
        return mousePos.GetPoint(distance);
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
