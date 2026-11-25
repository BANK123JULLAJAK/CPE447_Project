using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CinemachineCamera))]
public class EndfieldCameraController : MonoBehaviour
{
    [Header("🎯 Target Tracking (เป้าหมายติดตาม)")]
    [Tooltip("Target root Transform that the camera tracks. If null, automatically locates PlayerCameraRoot on Player.")]
    public Transform target;
    public string targetPlayerTag = "Player";
    public string cameraRootName = "PlayerCameraRoot";

    [Header("🔭 Resident Evil: Requiem Framing (OTS มุมมองบุคคลที่ 3 ข้ามไหล่)")]
    [Tooltip("องศาภาพ (FOV): 48°")]
    [Range(20f, 85f)]
    public float fieldOfView = 48f;

    [Tooltip("ระยะกล้อง (Camera Distance): 0.99 m")]
    public float cameraDistance = 0.99f;

    [Tooltip("เยื้องขวา (Shoulder Side): 0.60 m")]
    public float shoulderSide = 0.60f;

    [Tooltip("ระดับความสูง (Camera Height): -0.37 m (Shoulder Y: -0.374)")]
    public float shoulderHeight = -0.374f;

    [Tooltip("มุมก้ม/เงย (Camera Tilt): -16.4°")]
    public float cameraTilt = -16.4f;

    [Range(0f, 1f)]
    public float defaultCameraSide = 1.0f; // Over-the-right-shoulder (1.0 = เต็มไหล่ขวา)
    public Vector3 shoulderOffset = new Vector3(0.60f, -0.374f, 0f);
    public float verticalArmLength = 0.0f; // ล็อคระดับความสูงตายตัวตาม Shoulder Y
    public Vector3 damping = new Vector3(0.05f, 0.08f, 0.08f);

    [Header("🔍 Mouse Scroll Zoom (ล็อคค่าตายตัว)")]
    public bool enableZoom = false; // ปิดการซูมเพื่อล็อคระยะกล้องตายตัว 0.99m
    public float minDistance = 0.99f;
    public float maxDistance = 0.99f;
    public float defaultDistance = 0.99f;
    public float zoomSensitivity = 0.50f;
    public float zoomSmoothTime = 0.12f;

    private CinemachineCamera _cmCamera;
    private CinemachineThirdPersonFollow _thirdPersonFollow;
    private float _currentDistance;
    private float _targetDistance;
    private float _zoomVelocity;

    private void Awake()
    {
        _cmCamera = GetComponent<CinemachineCamera>();
        _thirdPersonFollow = GetComponent<CinemachineThirdPersonFollow>();
        if (_thirdPersonFollow == null)
        {
            _thirdPersonFollow = gameObject.AddComponent<CinemachineThirdPersonFollow>();
        }

        // Ensure this camera is not nested inside any player hierarchy
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        _targetDistance = cameraDistance;
        _currentDistance = cameraDistance;
    }

    private void Start()
    {
        ApplyEndfieldSettings();
        LocateAndBindTarget();
    }

    private void LateUpdate()
    {
        if (target == null || _cmCamera.Target.TrackingTarget == null)
        {
            LocateAndBindTarget();
        }

        if (enableZoom)
        {
            HandleScrollZoom();
        }
        else
        {
            // ล็อคค่าตายตัวทุกเฟรม ไม่ให้มีสคริปต์อื่นเปลี่ยนระยะหรือการเยื้องไหล่
            if (_thirdPersonFollow != null)
            {
                _thirdPersonFollow.CameraDistance = cameraDistance;
                _thirdPersonFollow.CameraSide = defaultCameraSide;
                _thirdPersonFollow.ShoulderOffset = new Vector3(shoulderSide, shoulderHeight, 0f);
                _thirdPersonFollow.VerticalArmLength = verticalArmLength;
            }
        }

        if (_thirdPersonFollow != null && _thirdPersonFollow.AvoidObstacles.Enabled)
        {
            _thirdPersonFollow.AvoidObstacles.Enabled = false;
        }
    }

    public void ApplyEndfieldSettings()
    {
        if (_cmCamera != null)
        {
            _cmCamera.Lens.FieldOfView = fieldOfView;
            _cmCamera.Lens.NearClipPlane = 0.15f;
        }

        if (_thirdPersonFollow != null)
        {
            _thirdPersonFollow.CameraDistance = cameraDistance;
            _thirdPersonFollow.CameraSide = defaultCameraSide;
            _thirdPersonFollow.ShoulderOffset = new Vector3(shoulderSide, shoulderHeight, 0f);
            _thirdPersonFollow.VerticalArmLength = verticalArmLength;
            _thirdPersonFollow.Damping = damping;

            // ปิด AvoidObstacles ถาวร เพื่อป้องกันกล้องกระตุกหรือสแนปลงพื้น
            _thirdPersonFollow.AvoidObstacles.Enabled = false;
        }

        ApplyCameraTilt();
    }

    public void ApplyCameraTilt()
    {
        var tpc = Object.FindAnyObjectByType<StarterAssets.ThirdPersonController>();
        if (tpc != null)
        {
            tpc.CameraAngleOverride = cameraTilt;
        }
        if (target != null)
        {
            target.localEulerAngles = new Vector3(cameraTilt, target.localEulerAngles.y, target.localEulerAngles.z);
        }
    }

    public void LocateAndBindTarget()
    {
        // ค้นหาตัวละครผู้เล่นในฉาก
        GameObject player = GameObject.FindWithTag(targetPlayerTag);
        if (player == null)
        {
            player = GameObject.Find("Player_CH2_Newman") 
                  ?? GameObject.Find("PlayerArmature") 
                  ?? GameObject.Find("NestedParentArmature_Newman");
        }

        if (player != null)
        {
            Transform root = player.transform.Find(cameraRootName);
            if (root == null)
            {
                root = FindChildRecursive(player.transform, cameraRootName);
            }

            if (root != null)
            {
                target = root;
            }
            else
            {
                target = player.transform;
            }

            if (_cmCamera != null && _cmCamera.Target.TrackingTarget != target)
            {
                _cmCamera.Target.TrackingTarget = target;
            }

            ApplyCameraTilt();
        }
    }

    private void HandleScrollZoom()
    {
        if (!enableZoom || _thirdPersonFollow == null) return;

        float scrollDelta = GetScrollInput();

        if (Mathf.Abs(scrollDelta) > 0.001f)
        {
            _targetDistance -= scrollDelta * zoomSensitivity;
            _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
        }

        _currentDistance = Mathf.SmoothDamp(_thirdPersonFollow.CameraDistance, _targetDistance, ref _zoomVelocity, zoomSmoothTime);
        _thirdPersonFollow.CameraDistance = _currentDistance;
    }

    private float GetScrollInput()
    {
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                return Mathf.Sign(scroll) * 1.0f;
            }
        }
        return 0f;
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var res = FindChildRecursive(parent.GetChild(i), name);
            if (res != null) return res;
        }
        return null;
    }
}
