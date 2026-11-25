using System.Collections;
using UnityEngine;
using VRM;

/// <summary>
/// ระบบอนิเมชั่นกระพริบตาอัตโนมัติสำหรับตัวละคร VRoid (VRM / SkinnedMeshRenderer)
/// รองรับทั้ง VRMBlendShapeProxy และ BlendShape Fcl_EYE_Close โดยตรง
/// </summary>
[DisallowMultipleComponent]
public class VRoidEyeBlink : MonoBehaviour
{
    [Header("Blink Timing")]
    [Tooltip("เวลาระหว่างการกระพริบตาต่ำสุด (วินาที)")]
    public float minInterval = 2.5f;
    [Tooltip("เวลาระหว่างการกระพริบตาสูงสุด (วินาที)")]
    public float maxInterval = 5.0f;

    [Header("Blink Speed")]
    [Tooltip("ความเร็วในการปิดตา (วินาที)")]
    public float closingDuration = 0.06f;
    [Tooltip("ระยะเวลาค้างตาปิด (วินาที)")]
    public float holdClosedDuration = 0.04f;
    [Tooltip("ความเร็วในการเปิดตา (วินาที)")]
    public float openingDuration = 0.07f;

    [Header("Natural Variation")]
    [Tooltip("โอกาสที่จะกระพริบตาสองครั้งติด (0 - 1)")]
    [Range(0f, 1f)]
    public float doubleBlinkChance = 0.15f;

    private VRMBlendShapeProxy blendShapeProxy;
    private SkinnedMeshRenderer faceRenderer;
    private int blinkBlendShapeIndex = -1;

    private BlendShapeKey vrmBlinkKey;
    private Coroutine blinkCoroutine;

    private void Awake()
    {
        InitializeComponents();
    }

    private void OnEnable()
    {
        InitializeComponents();
        if (blinkCoroutine != null) StopCoroutine(blinkCoroutine);
        blinkCoroutine = StartCoroutine(BlinkLoop());
    }

    private void OnDisable()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        SetBlinkWeight(0f);
    }

    public void InitializeComponents()
    {
        // 1. Try finding VRMBlendShapeProxy
        blendShapeProxy = GetComponent<VRMBlendShapeProxy>();
        if (blendShapeProxy == null) blendShapeProxy = GetComponentInParent<VRMBlendShapeProxy>();
        if (blendShapeProxy == null) blendShapeProxy = GetComponentInChildren<VRMBlendShapeProxy>();

        vrmBlinkKey = BlendShapeKey.CreateFromPreset(BlendShapePreset.Blink);

        // 2. Try finding SkinnedMeshRenderer with Face blendshapes
        if (faceRenderer == null)
        {
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr.sharedMesh == null) continue;
                int count = smr.sharedMesh.blendShapeCount;
                for (int i = 0; i < count; i++)
                {
                    string shapeName = smr.sharedMesh.GetBlendShapeName(i);
                    if (shapeName.Equals("Fcl_EYE_Close", System.StringComparison.OrdinalIgnoreCase) ||
                        shapeName.Equals("Blink", System.StringComparison.OrdinalIgnoreCase))
                    {
                        faceRenderer = smr;
                        blinkBlendShapeIndex = i;
                        break;
                    }
                }
                if (faceRenderer != null) break;
            }
        }
    }

    private IEnumerator BlinkLoop()
    {
        while (true)
        {
            float waitTime = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(waitTime);

            // Execute Blink
            yield return StartCoroutine(DoSingleBlink());

            // Random double blink
            if (Random.value < doubleBlinkChance)
            {
                yield return new WaitForSeconds(0.12f);
                yield return StartCoroutine(DoSingleBlink());
            }
        }
    }

    private IEnumerator DoSingleBlink()
    {
        // 1. Closing eyes
        float elapsed = 0f;
        while (elapsed < closingDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / closingDuration);
            SetBlinkWeight(Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        SetBlinkWeight(1f);

        // 2. Hold eyes closed briefly
        if (holdClosedDuration > 0f)
        {
            yield return new WaitForSeconds(holdClosedDuration);
        }

        // 3. Opening eyes
        elapsed = 0f;
        while (elapsed < openingDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / openingDuration);
            SetBlinkWeight(Mathf.SmoothStep(1f, 0f, t));
            yield return null;
        }
        SetBlinkWeight(0f);
    }

    private void SetBlinkWeight(float weight01)
    {
        // Apply to VRMBlendShapeProxy (0 - 1)
        if (blendShapeProxy != null)
        {
            blendShapeProxy.ImmediatelySetValue(vrmBlinkKey, weight01);
        }

        // Apply directly to Face SkinnedMeshRenderer (0 - 100)
        if (faceRenderer != null && blinkBlendShapeIndex >= 0)
        {
            faceRenderer.SetBlendShapeWeight(blinkBlendShapeIndex, weight01 * 100f);
        }
    }

    [ContextMenu("Test Blink")]
    public void TestBlink()
    {
        if (isActiveAndEnabled)
        {
            StartCoroutine(DoSingleBlink());
        }
    }
}
