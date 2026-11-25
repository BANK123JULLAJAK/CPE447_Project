using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// ระบบควบคุมท่าแจ็คบีบคอเหยื่อสาว (ปรับให้สมจริงระดับภาพยนตร์):
/// 1. ยกตัวเหยื่อลอยขึ้นจากพื้น (Lifting)
/// 2. ยกค้างไว้จนกว่าคัตซีนจะสั่งโยน (Holding) แจ็คไม่ได้ค้างแข็ง แต่มีกล้ามเนื้อสั่นเกร็งตามแรงดิ้นของเหยื่อ
///    เหยื่อห้อยจากลำคอ แกว่งเป็นลูกตุ้ม สะบัดเกร็งเป็นจังหวะ แล้วค่อย ๆ อ่อนแรงลงเรื่อย ๆ
/// 3. ง้างดึงเข้าหาตัวก่อนโยน (Wind-up) -> ปล่อยมือพร้อมสโลว์โมชันสั้น ๆ
/// 4. เหยื่อลอยกระเด็นหมุนตัวกลางอากาศ ตกกระแทกพื้นมีควันฝุ่น เสียงตุบ กระเด้ง แล้วไถลนอนราบ
/// 5. แจ็คเล่นอนิเมชันต่อจนจบแล้วกลับมายืนปกติ
/// </summary>
[ExecuteAlways]
public class JackTheaterChokeAndThrowSequence : MonoBehaviour
{
    [Header("=== Target References ===")]
    public Transform victimTransform;
    public Animator jackAnim;
    public Animator victimAnim;

    [Header("=== Sequence Settings ===")]
    [Tooltip("ระยะเยื้องไปข้างหน้าจากอุ้งมือขวาของ Jack")]
    public float handForwardOffset = 0.08f;

    [Tooltip("ระยะเวลาที่ยกค้างไว้กลางอากาศ (วินาที) ใช้เมื่อปิด holdUntilThrown")]
    public float holdDuration = 2.5f;

    [Tooltip("ยกค้างต่อไปเรื่อย ๆ จนกว่าคัตซีนจะเรียก ThrowVictim (ซิงก์กับบทพูด)")]
    public bool holdUntilThrown = true;

    [Tooltip("ความเร็วในการยก")]
    public float liftAnimSpeed = 1.2f;

    [Tooltip("ความเร็วในการเหวี่ยงโยน")]
    public float throwAnimSpeed = 1.3f;

    [Tooltip("ระยะเวลาที่เหยื่อลอยกระเด็นกลางอากาศ (วินาที)")]
    public float throwFlightDuration = 0.70f;

    [Tooltip("ความสูงวิถีโค้งพาราโบลาขณะลอยกระเด็น")]
    public float throwArcHeight = 0.85f;

    [Tooltip("จุดที่เหยื่อตกกระแทกพื้นแล้วนอนราบ")]
    public Vector3 landingPosition = new Vector3(29.8f, 0.24f, -87.8f);

    [Tooltip("มุมหมุนตัวเหยื่อเมื่อนอนราบกับพื้น")]
    public Vector3 landingRotationEuler = new Vector3(0f, 90f, 0f);

    [Tooltip("ความเร็วในการดิ้นของเหยื่อ")]
    public float strangleAnimSpeed = 1.45f;

    [Tooltip("เล่นอนิเมชั่นอัตโนมัติทันทีเมื่อเปิดเกม (ปิดไว้เพื่อรอคัตซีนเควส 3 และป้องกันสปอยล์)")]
    public bool autoPlaySequence = false;

    [Header("=== Realism ===")]
    [Tooltip("สโลว์โมชันตอนปล่อยมือ (Time.timeScale)")]
    public float releaseSlowMoScale = 0.28f;
    [Tooltip("ระยะเวลาสโลว์โมชันก่อนเร่งกลับ (วินาทีจริง)")]
    public float releaseSlowMoHold = 0.3f;

    public enum SequenceState
    {
        Idle,
        Lifting,
        HoldingSuspended,
        ThrowSwinging,
        InFlight,
        LandedFlat,
        Finished
    }

    [Header("=== Current State (Debug) ===")]
    [SerializeField] private SequenceState _currentState = SequenceState.Idle;
    public SequenceState CurrentState => _currentState;

    private Transform _rightHand;
    private bool _isHoldingVictim = true;
    private bool _isThrowing = false;
    private bool _isLandedFlat = false;
    private Coroutine _sequenceRoutine;

    // สถานะการดิ้นของเหยื่อ
    private float _holdStartTime;
    private float _nextSpasmTime;
    private Vector3 _spasm;
    private float _windUp;

    private void Awake()
    {
        CacheComponents();
    }

    private void Start()
    {
        CacheComponents();
        if (Application.isPlaying)
        {
            // หากยังเล่นฉากที่ 2 ไม่จบ ให้ซ่อน Jack และเหยื่อทันที เพื่อป้องกันคนเห็น Jack ก่อนเวลาอันควร (กันสปอยล์)
            bool isQuest3Ready = false;
            if (RoyalLyceumTheaterQuestManager.Instance != null)
            {
                isQuest3Ready = RoyalLyceumTheaterQuestManager.Instance.IsQuest3Active;
            }

            if (!isQuest3Ready)
            {
                gameObject.SetActive(false);
                if (victimTransform != null) victimTransform.gameObject.SetActive(false);
            }
            else if (autoPlaySequence)
            {
                StartChoking();
            }
        }
    }

    private void OnEnable()
    {
        CacheComponents();
        if (!Application.isPlaying)
        {
            _isHoldingVictim = true;
            _isThrowing = false;
            _isLandedFlat = false;
        }
    }

    public void CacheComponents()
    {
        if (jackAnim == null) jackAnim = GetComponentInChildren<Animator>();
        if (jackAnim != null)
        {
            _rightHand = jackAnim.GetBoneTransform(HumanBodyBones.RightHand);
        }

        if (victimTransform == null)
        {
            var vGo = GameObject.Find("Theater_Surviving_Victim");
            if (vGo != null) victimTransform = vGo.transform;
        }

        if (victimTransform != null && victimAnim == null)
        {
            victimAnim = victimTransform.GetComponentInChildren<Animator>();
        }
    }

    public void Init(Transform victim)
    {
        victimTransform = victim;
        CacheComponents();
        StartChoking();
    }

    /// <summary>
    /// เริ่มกระบวนการ: ยกขึ้น -> ค้างบีบคอ (จนกว่าจะเรียก ThrowVictim) -> โยนเหยื่อกระเด็นไปนอนราบ
    /// </summary>
    public void StartChoking(float customHoldDuration = -1f)
    {
        CacheComponents();

        if (customHoldDuration > 0f)
        {
            holdDuration = customHoldDuration;
            holdUntilThrown = false;
        }

        _isHoldingVictim = true;
        _isThrowing = false;
        _isLandedFlat = false;
        _spasm = Vector3.zero;
        _windUp = 0f;
        _holdStartTime = Time.time;
        _nextSpasmTime = Time.time + 0.4f;

        if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);

        if (!Application.isPlaying)
        {
            _currentState = SequenceState.HoldingSuspended;
            return;
        }

        _sequenceRoutine = StartCoroutine(FullChokeAndThrowRoutine());
    }

    private IEnumerator FullChokeAndThrowRoutine()
    {
        // -------------------------------------------------------------
        // เฟส 1: เริ่มท่ายก (Lifting) แจ็คยกเหยื่อลอยขึ้นจากพื้น
        // -------------------------------------------------------------
        _currentState = SequenceState.Lifting;
        _isHoldingVictim = true;
        _isThrowing = false;
        _isLandedFlat = false;
        _holdStartTime = Time.time;

        if (jackAnim != null)
        {
            jackAnim.speed = liftAnimSpeed;
            jackAnim.Play("Superhuman_Choke_Lift", 0, 0.15f);
        }

        if (victimAnim != null)
        {
            victimAnim.SetBool("IsUnconscious", false);
            victimAnim.Play("Being_Strangled", 0, 0f);
            victimAnim.speed = strangleAnimSpeed;
        }

        float liftTimer = 0f;
        float liftTargetTime = 1.35f / liftAnimSpeed;
        while (liftTimer < liftTargetTime)
        {
            liftTimer += Time.deltaTime;
            yield return null;
        }

        // -------------------------------------------------------------
        // เฟส 2: ยกค้าง (Holding) แจ็คคงท่ายก (ความเร็วอนิเมชันเป็น 0) แต่ LateUpdate เติมการสั่นเกร็งตลอดเวลา
        // เหยื่อดิ้นและอ่อนแรงลง ค้างจนกว่าคัตซีนจะเรียก ThrowVictim
        // -------------------------------------------------------------
        _currentState = SequenceState.HoldingSuspended;
        if (jackAnim != null) jackAnim.speed = 0f;

        if (victimAnim != null)
        {
            victimAnim.speed = strangleAnimSpeed;
            victimAnim.Play("Being_Strangled");
        }

        if (holdUntilThrown)
        {
            while (true) yield return null; // รอ ThrowVictim ซึ่งจะหยุด Coroutine นี้เอง
        }

        float elapsedHold = 0f;
        while (elapsedHold < holdDuration)
        {
            elapsedHold += Time.deltaTime;
            yield return null;
        }

        yield return StartCoroutine(ExecuteThrowRoutine(landingPosition, null, null));
    }

    /// <summary>
    /// สั่งเหวี่ยงโยนทันที (เรียกจากคัตซีน) onRelease = ตอนปล่อยมือ, onLanded = ตอนร่างกระแทกพื้น
    /// </summary>
    public void ThrowVictim(Vector3 targetLandingPos, Action onLanded = null, Action onRelease = null)
    {
        CacheComponents();

        if (!Application.isPlaying)
        {
            _currentState = SequenceState.LandedFlat;
            _isHoldingVictim = false;
            _isThrowing = false;
            _isLandedFlat = true;

            if (victimTransform != null)
            {
                victimTransform.position = targetLandingPos;
                victimTransform.rotation = Quaternion.Euler(landingRotationEuler);
            }
            if (victimAnim != null)
            {
                victimAnim.Play("Receiving_CPR_Unconscious", 0, 0f);
            }
            onLanded?.Invoke();
            return;
        }

        if (_sequenceRoutine != null) StopCoroutine(_sequenceRoutine);

        // เดินลำดับโยนบนตัวจัดการคัตซีน เพื่อไม่ให้หยุดกลางคันเมื่อคัตซีนซ่อนตัวแจ็คด้วยควัน
        var runner = CinematicCutsceneManager.Instance;
        _sequenceRoutine = runner != null
            ? runner.StartCoroutine(ExecuteThrowRoutine(targetLandingPos, onLanded, onRelease))
            : StartCoroutine(ExecuteThrowRoutine(targetLandingPos, onLanded, onRelease));
    }

    private IEnumerator ExecuteThrowRoutine(Vector3 targetLandingPos, Action onLanded, Action onRelease)
    {
        _currentState = SequenceState.ThrowSwinging;
        landingPosition = targetLandingPos;

        // 1. ง้างดึงร่างเหยื่อเข้าหาตัวก่อนโยน (Wind-up) ศีรษะเหยื่อสะบัดหงายหลัง
        float windTimer = 0f;
        const float windDuration = 0.24f;
        while (windTimer < windDuration)
        {
            windTimer += Time.deltaTime;
            _windUp = Mathf.SmoothStep(0f, 1f, windTimer / windDuration);
            yield return null;
        }
        _windUp = 1f;

        // 2. แจ็คกลับมาขยับแขนเหวี่ยงโยนไปข้างหน้าอย่างรุนแรง
        if (jackAnim != null)
        {
            jackAnim.speed = throwAnimSpeed;
        }
        yield return new WaitForSeconds(0.14f);

        // 3. ปล่อยมือ! สโลว์โมชันสั้น ๆ ให้คนดูเห็นแรงเหวี่ยง
        _currentState = SequenceState.InFlight;
        _isHoldingVictim = false;
        _isThrowing = true;
        _windUp = 0f;
        onRelease?.Invoke();

        var runner = CinematicCutsceneManager.Instance;
        if (runner != null) runner.StartCoroutine(SlowMotionPulse(releaseSlowMoScale, releaseSlowMoHold, 0.4f));

        if (victimTransform == null) yield break;

        // 4. ทันทีที่โดนโยน เหยื่อเปลี่ยนเป็นท่าสลบ (Receiving CPR)
        if (victimAnim != null)
        {
            victimAnim.SetBool("IsUnconscious", true);
            victimAnim.SetTrigger("Knockdown");
            victimAnim.Play("Receiving_CPR_Unconscious", 0, 0f);
            victimAnim.speed = 1.0f;
        }

        Vector3 startPos = victimTransform.position;
        Quaternion startRot = victimTransform.rotation;
        Quaternion targetRot = Quaternion.Euler(landingRotationEuler);
        Vector3 flatDir = targetLandingPos - startPos;
        flatDir.y = 0f;
        flatDir = flatDir.sqrMagnitude > 0.0001f ? flatDir.normalized : transform.forward;

        float duration = throwFlightDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // วิถีโค้งพาราโบลา
            Vector3 pos = Vector3.Lerp(startPos, targetLandingPos, smoothT);
            pos.y += 4f * throwArcHeight * t * (1f - t);
            victimTransform.position = pos;

            // หมุนตัวกลางอากาศ: ตัวตีลังกาเอนหลังแล้วค่อยแผ่ลงมานอนราบ
            Quaternion tumble = Quaternion.AngleAxis(Mathf.Sin(t * Mathf.PI) * -38f, Vector3.Cross(Vector3.up, flatDir))
                              * Quaternion.AngleAxis(Mathf.Sin(t * Mathf.PI * 2f) * 14f, flatDir);
            victimTransform.rotation = tumble * Quaternion.Slerp(startRot, targetRot, smoothT);

            yield return null;
        }

        // 5. กระแทกพื้น: ฝุ่น เสียงตุบ กล้องสะเทือน
        _currentState = SequenceState.LandedFlat;
        victimTransform.position = targetLandingPos;
        victimTransform.rotation = targetRot;
        CutsceneFX.DustPuff(targetLandingPos, 1.1f);
        CutsceneFX.PlayThud(targetLandingPos, 1f);
        if (runner != null)
        {
            var cam = runner.camTheaterNewmanReaction != null ? runner.camTheaterNewmanReaction : runner.camTheaterWideBreach;
            runner.ShakeCamera(cam, 0.5f, 0.2f);
        }

        // 6. กระเด้งเล็กน้อยแล้วไถลไปตามแรงเหวี่ยง ก่อนนอนนิ่ง
        Vector3 bounceStart = targetLandingPos;
        Vector3 finalPos = targetLandingPos + flatDir * 0.28f;
        float bounceTimer = 0f;
        const float bounceDuration = 0.3f;
        while (bounceTimer < bounceDuration)
        {
            bounceTimer += Time.deltaTime;
            float k = Mathf.Clamp01(bounceTimer / bounceDuration);
            Vector3 p = Vector3.Lerp(bounceStart, finalPos, 1f - (1f - k) * (1f - k));
            p.y += 4f * 0.12f * k * (1f - k);
            victimTransform.position = p;
            yield return null;
        }

        landingPosition = finalPos;
        victimTransform.position = finalPos;
        _isThrowing = false;
        _isLandedFlat = true;

        if (victimAnim != null)
        {
            victimAnim.Play("Receiving_CPR_Unconscious", 0, 0f);
        }

        // 7. แจ็คเล่นอนิเมชันทิ้งน้ำหนักแขนจนจบอย่างเป็นธรรมชาติ (ไม่ค้างแข็ง)
        if (jackAnim != null)
        {
            jackAnim.speed = 1.0f;
        }

        onLanded?.Invoke();
    }

    /// <summary>สโลว์โมชันสั้น ๆ แล้วไล่ความเร็วกลับเป็นปกติ (ทำงานด้วยเวลาจริง เพื่อไม่ให้ตัวเองถูกหน่วงด้วย)</summary>
    private static IEnumerator SlowMotionPulse(float scale, float holdSeconds, float rampSeconds)
    {
        Time.timeScale = scale;
        float t = 0f;
        while (t < holdSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        t = 0f;
        while (t < rampSeconds)
        {
            t += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(scale, 1f, Mathf.SmoothStep(0f, 1f, t / rampSeconds));
            yield return null;
        }
        Time.timeScale = 1f;
    }

    // ==========================================
    // Procedural realism (LateUpdate)
    // ==========================================
    private void LateUpdate()
    {
        // บังคับสเกลตัวละครทั้งสองเป็น 1.3 เท่าอย่างเคร่งครัด
        if (transform.localScale != new Vector3(1.3f, 1.3f, 1.3f))
        {
            transform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
        }
        if (victimTransform != null && victimTransform.localScale != new Vector3(1.3f, 1.3f, 1.3f))
        {
            victimTransform.localScale = new Vector3(1.3f, 1.3f, 1.3f);
        }

        bool holding = _isHoldingVictim && !_isThrowing && !_isLandedFlat;

        // 0. แจ็คเกร็งแขน/ลำตัวตามแรงดิ้นของเหยื่อ (ซ้อนทับบนท่าที่หยุดค้างไว้ ไม่ให้ดูเป็นหุ่นนิ่ง)
        if (holding && Application.isPlaying && jackAnim != null)
        {
            ApplyJackExertion();
        }

        // 1. ตรึงลำคอของเหยื่อให้อยู่ในอุ้งมือขวาของ Jack ขณะที่ถูกยก
        if (holding && _rightHand != null && victimTransform != null)
        {
            Transform vNeck = victimAnim != null ? (victimAnim.GetBoneTransform(HumanBodyBones.Neck) ?? victimAnim.GetBoneTransform(HumanBodyBones.Head)) : null;

            Vector3 neckDelta = Vector3.up * 1.50f;
            if (vNeck != null)
            {
                neckDelta = vNeck.position - victimTransform.position;
            }

            Vector3 handGripPos = _rightHand.position + transform.forward * handForwardOffset;
            Vector3 targetPos = handGripPos - neckDelta;
            Quaternion targetRot = Quaternion.LookRotation(-transform.forward, Vector3.up);

            if (Application.isPlaying)
            {
                ApplyStruggle(handGripPos, ref targetPos, ref targetRot);
                float posLerp = Mathf.Clamp01(Time.deltaTime * 32f);
                float rotLerp = Mathf.Clamp01(Time.deltaTime * 24f);
                victimTransform.position = Vector3.Lerp(victimTransform.position, targetPos, posLerp);
                victimTransform.rotation = Quaternion.Slerp(victimTransform.rotation, targetRot, rotLerp);
            }
            else
            {
                victimTransform.position = targetPos;
                victimTransform.rotation = targetRot;
            }
        }
        // 2. เมื่อนอนราบกับพื้นแล้ว ตรึงให้อยู่ในตำแหน่งนอนราบสนิท ไม่ดีดกลับหรือลอยเคว้ง
        else if (_isLandedFlat && victimTransform != null && !_isThrowing)
        {
            victimTransform.position = landingPosition;
            victimTransform.rotation = Quaternion.Euler(landingRotationEuler);
        }

        // 3. จัดระเบียบกระดูกข้อเท้าและขาของ Jack ให้ยืนเต็มฝ่าเท้า ไม่เขย่ง เบี้ยว หรือห้อยลอย
        if (jackAnim != null)
        {
            Transform rFoot = jackAnim.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform lFoot = jackAnim.GetBoneTransform(HumanBodyBones.LeftFoot);

            if (rFoot != null && lFoot != null)
            {
                Vector3 rRot = rFoot.eulerAngles;
                Vector3 lRot = lFoot.eulerAngles;
                rFoot.eulerAngles = new Vector3(0f, rRot.y, 0f);
                lFoot.eulerAngles = new Vector3(0f, lRot.y, 0f);
            }
        }
    }

    /// <summary>
    /// เหยื่อห้อยจากลำคอ: แกว่งเป็นลูกตุ้มรอบมือแจ็ค สะบัดเกร็งเป็นจังหวะ หงายหลังมากขึ้น และอ่อนแรงลงเรื่อย ๆ
    /// </summary>
    private void ApplyStruggle(Vector3 pivot, ref Vector3 pos, ref Quaternion rot)
    {
        float t = Time.time - _holdStartTime;
        float fatigue = Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(0.8f, 9f, t));

        // แรงสะบัดเกร็งแบบกระตุก (Spasm) ความถี่ลดลงเมื่อเหยื่ออ่อนแรง
        if (Time.time >= _nextSpasmTime)
        {
            float s = UnityEngine.Random.Range(6f, 13f) * fatigue;
            _spasm = new Vector3(
                UnityEngine.Random.Range(-0.5f, 0.5f) * s,
                UnityEngine.Random.Range(-1f, 1f) * s * 0.7f,
                UnityEngine.Random.Range(-1f, 1f) * s);
            _nextSpasmTime = Time.time + UnityEngine.Random.Range(0.55f, 1.2f) / Mathf.Max(0.35f, fatigue);
        }
        _spasm = Vector3.Lerp(_spasm, Vector3.zero, 1f - Mathf.Exp(-7f * Time.deltaTime));

        float lean = Mathf.Lerp(-8f, -16f, Mathf.InverseLerp(0f, 5f, t)) - 24f * _windUp;
        float pitch = lean + Mathf.Sin(t * 2.4f) * 4f * fatigue + _spasm.x;
        float yaw = Mathf.Sin(t * 1.6f + 1.2f) * 6f * fatigue + _spasm.y;
        float roll = Mathf.Sin(t * 3.8f) * 4.5f * fatigue + _spasm.z;

        Quaternion frame = Quaternion.LookRotation(transform.forward, Vector3.up);
        Quaternion delta = frame * Quaternion.Euler(pitch, yaw, roll) * Quaternion.Inverse(frame);
        pos = pivot + delta * (pos - pivot);
        rot = delta * rot;

        // ตอนง้างโยน ดึงตัวเหยื่อเข้าหาอกแจ็คเล็กน้อย
        pos -= transform.forward * 0.12f * _windUp;
    }

    /// <summary>แจ็คเกร็งแขนขวา ไหล่ และลำตัวด้วยสัญญาณสั่นแบบสุ่มนุ่ม (Perlin) ตามแรงดิ้นของเหยื่อ</summary>
    private void ApplyJackExertion()
    {
        float t = Time.time;
        float strain = 0.65f + 0.35f * Mathf.Clamp01(Mathf.Abs(_spasm.z) / 9f);

        AddBoneRotation(HumanBodyBones.RightUpperArm, new Vector3(PerlinSigned(t * 10f, 1f) * 1.8f, PerlinSigned(t * 9f, 2f) * 1.2f, PerlinSigned(t * 11f, 3f) * 1.8f) * strain);
        AddBoneRotation(HumanBodyBones.RightLowerArm, new Vector3(PerlinSigned(t * 12f, 4f) * 2.4f, 0f, PerlinSigned(t * 10f, 5f) * 2.0f) * strain);
        AddBoneRotation(HumanBodyBones.RightShoulder, new Vector3(PerlinSigned(t * 8f, 6f) * 1.2f, 0f, 0f) * strain);
        AddBoneRotation(HumanBodyBones.Spine, new Vector3(-2.5f + Mathf.Sin(t * 2.2f) * 0.8f, PerlinSigned(t * 1.3f, 7f) * 2f, 0f));
        AddBoneRotation(HumanBodyBones.Chest, new Vector3(Mathf.Sin(t * 2.2f + 0.6f) * 1.1f, PerlinSigned(t * 1.1f, 8f) * 1.5f, 0f));
        AddBoneRotation(HumanBodyBones.Head, new Vector3(PerlinSigned(t * 2f, 9f) * 2f + 2f, PerlinSigned(t * 1.6f, 10f) * 3f, 0f));
    }

    private void AddBoneRotation(HumanBodyBones bone, Vector3 euler)
    {
        var tr = jackAnim.GetBoneTransform(bone);
        if (tr != null) tr.localRotation *= Quaternion.Euler(euler);
    }

    private static float PerlinSigned(float x, float seed)
    {
        return (Mathf.PerlinNoise(x, seed * 7.13f) - 0.5f) * 2f;
    }

    private void OnDisable()
    {
        // กันสโลว์โมชันค้างถ้าตัวละครถูกซ่อนระหว่างเล่น
        if (Application.isPlaying && Time.timeScale < 1f && Time.timeScale > 0f && CinematicCutsceneManager.Instance == null)
        {
            Time.timeScale = 1f;
        }
    }
}
