using UnityEngine;

/// <summary>
/// Unity 6 Animation Warping System for Newman (VRoid)
/// ประกอบด้วย 3 ระบบหลัก:
/// 1. Foot Grounder IK (OnAnimatorIK): เท้าแนบพื้นถนนหิน/ทางลาด/บันได ไม่ลอย ไม่จมดิน
/// 2. Pelvis / Hip Adjustment: ลดระดับสะโพกตามความสูงพื้นเพื่อป้องกันขาตึงผิดรูป
/// 3. Body Leaning / Orientation Tilt (LateUpdate): เอียงลำตัวตามแรงเหวี่ยงขณะเลี้ยว (AAA Feel)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class NewmanAnimationWarping : MonoBehaviour
{
    [Header("=== 1. Foot IK / Grounder Placement ===")]
    [Tooltip("เปิด/ปิดระบบจัดตำแหน่งเท้าแนบพื้น")]
    public bool enableFootIK = true;

    [Tooltip("Layer สำหรับตรวจสอบพื้นผิว (เช่น Default, Ground)")]
    public LayerMask groundLayers = ~0;

    [Tooltip("ระยะยิง Raycast จากระดับข้อเท้าขึ้นไปด้านบน")]
    [Range(0.2f, 1.0f)]
    public float raycastUpDistance = 0.5f;

    [Tooltip("ระยะยิง Raycast ลงไปหาพื้นดิน")]
    [Range(0.4f, 1.5f)]
    public float raycastDownDistance = 0.8f;

    [Tooltip("ความหนาของพื้นรองเท้า (Foot Sole Offset) ป้องกันรองเท้าจมพื้นหิน")]
    [Range(0.01f, 0.1f)]
    public float footSoleOffset = 0.02f;

    [Tooltip("ความเร็วในการปรับความสูงของเท้าให้เข้ากับพื้น (ลดการกระตุก)")]
    [Range(5f, 35f)]
    public float footPositionSpeed = 18f;

    [Tooltip("ความเร็วในการปรับองศาฝ่าเท้าให้ขนานกับพื้นผิว")]
    [Range(5f, 35f)]
    public float footRotationSpeed = 16f;

    [Header("=== 2. Pelvis / Hip Height Adjustment ===")]
    [Tooltip("เปิด/ปิดการปรับระดับสะโพกเพื่อลดความตึงของขา")]
    public bool enablePelvisAdjustment = true;

    [Tooltip("ความเร็วในการปรับระดับสะโพก")]
    [Range(2f, 25f)]
    public float pelvisSpeed = 12f;

    [Tooltip("ขีดจำกัดการลดระดับสะโพกสูงสุด")]
    [Range(0.05f, 0.4f)]
    public float maxPelvisDrop = 0.25f;

    [Header("=== 3. Body Leaning / Orientation Tilt ===")]
    [Tooltip("เปิด/ปิดระบบเอียงตัวตามแรงเหวี่ยงขณะเลี้ยวและเร่งความเร็ว")]
    public bool enableBodyLean = true;

    [Tooltip("ความไวต่อการเอียงตัวตามการหมุนเลี้ยว (Roll)")]
    [Range(0.01f, 0.15f)]
    public float turnLeanSensitivity = 0.045f;

    [Tooltip("มุมเอียงตัวสูงสุดขณะเลี้ยว (องศา)")]
    [Range(2f, 15f)]
    public float maxRollAngle = 8.5f;

    [Tooltip("ความเร็วในการคืนตัวของการเอียง")]
    [Range(3f, 20f)]
    public float leanSmoothSpeed = 10f;

    [Tooltip("มุมเอียงไปข้างหน้าขณะวิ่งเร็ว (Pitch)")]
    [Range(0f, 8f)]
    public float forwardSprintLean = 4.0f;

    // References
    private Animator _animator;
    private CharacterController _characterController;
    private StarterAssets.ThirdPersonController _tpc;

    // Foot IK Internal State
    private bool _isIKInitialized = false;
    private Vector3 _leftFootIKPos;
    private Vector3 _rightFootIKPos;
    private Quaternion _leftFootIKRot = Quaternion.identity;
    private Quaternion _rightFootIKRot = Quaternion.identity;

    private float _leftFootWeight = 1f;
    private float _rightFootWeight = 1f;
    private float _currentPelvisOffset = 0f;

    // Body Lean Internal State
    private float _lastYaw;
    private float _currentRoll;
    private float _currentPitch;
    private float _rollVelocity;
    private float _pitchVelocity;

    private Transform _spineBone;
    private Transform _chestBone;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _characterController = GetComponentInParent<CharacterController>();
        _tpc = GetComponentInParent<StarterAssets.ThirdPersonController>();

        _lastYaw = transform.parent != null ? transform.parent.eulerAngles.y : transform.eulerAngles.y;

        // Cache Humanoid bones
        if (_animator != null && _animator.isHuman)
        {
            _spineBone = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _chestBone = _animator.GetBoneTransform(HumanBodyBones.Chest);
        }

        // Initialize ground layers from ThirdPersonController if available
        if (_tpc != null && _tpc.GroundLayers.value != 0)
        {
            groundLayers = _tpc.GroundLayers;
        }
    }

    private void Start()
    {
        // IK positions are initialized safely on first frame inside OnAnimatorIK
    }

    private void Update()
    {
        UpdateBodyLeanCalculations();
    }

    private void LateUpdate()
    {
        ApplyBodyLeanToBones();
    }

    #region 1 & 2. Foot IK and Pelvis Adjustment

    private void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null || !enableFootIK) return;

        if (!_isIKInitialized)
        {
            _isIKInitialized = true;
            _leftFootIKPos = _animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            _rightFootIKPos = _animator.GetIKPosition(AvatarIKGoal.RightFoot);
            _leftFootIKRot = _animator.GetIKRotation(AvatarIKGoal.LeftFoot);
            _rightFootIKRot = _animator.GetIKRotation(AvatarIKGoal.RightFoot);
        }

        bool isGrounded = _tpc == null || _tpc.Grounded;
        float targetWeight = isGrounded ? 1.0f : 0.0f;

        // Smoothly fade weights when leaving or touching the ground
        _leftFootWeight = Mathf.MoveTowards(_leftFootWeight, targetWeight, Time.deltaTime * 6f);
        _rightFootWeight = Mathf.MoveTowards(_rightFootWeight, targetWeight, Time.deltaTime * 6f);

        // Process Feet
        float leftHeightDiff = ProcessFootIK(AvatarIKGoal.LeftFoot, ref _leftFootIKPos, ref _leftFootIKRot, _leftFootWeight);
        float rightHeightDiff = ProcessFootIK(AvatarIKGoal.RightFoot, ref _rightFootIKPos, ref _rightFootIKRot, _rightFootWeight);

        // Adjust Pelvis (Hips) Height
        if (enablePelvisAdjustment && isGrounded)
        {
            float targetPelvisDrop = Mathf.Min(leftHeightDiff, rightHeightDiff);
            if (targetPelvisDrop > 0f) targetPelvisDrop = 0f; // Only drop pelvis when stepping down
            targetPelvisDrop = Mathf.Clamp(targetPelvisDrop, -maxPelvisDrop, 0f);

            _currentPelvisOffset = Mathf.Lerp(_currentPelvisOffset, targetPelvisDrop, Time.deltaTime * pelvisSpeed);

            Vector3 bodyPos = _animator.bodyPosition;
            bodyPos.y += _currentPelvisOffset;
            _animator.bodyPosition = bodyPos;
        }
        else
        {
            _currentPelvisOffset = Mathf.Lerp(_currentPelvisOffset, 0f, Time.deltaTime * pelvisSpeed);
        }
    }

    private float ProcessFootIK(AvatarIKGoal foot, ref Vector3 currentPos, ref Quaternion currentRot, float weight)
    {
        Vector3 animFootPos = _animator.GetIKPosition(foot);
        Quaternion animFootRot = _animator.GetIKRotation(foot);

        Vector3 rayOrigin = animFootPos + Vector3.up * raycastUpDistance;
        float rayLength = raycastUpDistance + raycastDownDistance;

        float heightDifference = 0f;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayLength, groundLayers, QueryTriggerInteraction.Ignore))
        {
            Vector3 targetPos = hit.point + Vector3.up * footSoleOffset;
            heightDifference = hit.point.y - animFootPos.y;

            // Smooth position interpolation to prevent stutter
            currentPos = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * footPositionSpeed);

            // Align rotation with ground surface normal
            Quaternion surfaceRot = Quaternion.FromToRotation(transform.up, hit.normal) * animFootRot;
            currentRot = Quaternion.Slerp(currentRot, surfaceRot, Time.deltaTime * footRotationSpeed);

            _animator.SetIKPositionWeight(foot, weight);
            _animator.SetIKRotationWeight(foot, weight);
            _animator.SetIKPosition(foot, currentPos);
            _animator.SetIKRotation(foot, currentRot);
        }
        else
        {
            _animator.SetIKPositionWeight(foot, 0f);
            _animator.SetIKRotationWeight(foot, 0f);
        }

        return heightDifference;
    }

    #endregion

    #region 3. Body Leaning / Orientation Tilt

    private void UpdateBodyLeanCalculations()
    {
        if (!enableBodyLean) return;

        float currentYaw = transform.parent != null ? transform.parent.eulerAngles.y : transform.eulerAngles.y;
        float deltaYaw = Mathf.DeltaAngle(_lastYaw, currentYaw);
        _lastYaw = currentYaw;

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        float yawRate = deltaYaw / deltaTime;

        // Current speed
        float currentSpeed = 0f;
        if (_characterController != null)
        {
            Vector3 horizVel = new Vector3(_characterController.velocity.x, 0f, _characterController.velocity.z);
            currentSpeed = horizVel.magnitude;
        }

        bool isMoving = currentSpeed > 0.3f;
        bool isGrounded = _tpc == null || _tpc.Grounded;

        // Target Roll: banking into turns (lean into direction of curve)
        float targetRoll = 0f;
        if (isMoving && isGrounded)
        {
            targetRoll = Mathf.Clamp(-yawRate * turnLeanSensitivity, -maxRollAngle, maxRollAngle);
        }

        // Target Pitch: slight forward lean when sprinting
        float targetPitch = 0f;
        if (isMoving && isGrounded && _tpc != null)
        {
            float sprintRatio = Mathf.Clamp01(currentSpeed / Mathf.Max(1f, _tpc.SprintSpeed));
            targetPitch = sprintRatio * forwardSprintLean;
        }

        // Smooth damp angles
        _currentRoll = Mathf.SmoothDamp(_currentRoll, targetRoll, ref _rollVelocity, 1f / leanSmoothSpeed);
        _currentPitch = Mathf.SmoothDamp(_currentPitch, targetPitch, ref _pitchVelocity, 1f / leanSmoothSpeed);
    }

    private void ApplyBodyLeanToBones()
    {
        if (!enableBodyLean) return;
        if (Mathf.Abs(_currentRoll) < 0.01f && Mathf.Abs(_currentPitch) < 0.01f) return;

        // Apply lean to Spine and Chest for natural organic bending
        if (_spineBone != null)
        {
            Quaternion spineLean = Quaternion.Euler(_currentPitch * 0.6f, 0f, _currentRoll * 0.6f);
            _spineBone.localRotation = _spineBone.localRotation * spineLean;
        }

        if (_chestBone != null)
        {
            Quaternion chestLean = Quaternion.Euler(_currentPitch * 0.4f, 0f, _currentRoll * 0.4f);
            _chestBone.localRotation = _chestBone.localRotation * chestLean;
        }
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!enableFootIK) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_leftFootIKPos, 0.05f);
        Gizmos.DrawWireSphere(_rightFootIKPos, 0.05f);
    }
#endif
}
