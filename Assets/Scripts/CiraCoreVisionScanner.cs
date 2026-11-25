using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// ระบบสแกนภาพวัตถุพยานด้วย Computer Vision (CiRA CORE)
/// จำลองและเชื่อมโยงกับโมเดลของแต่ละฉาก (ดู SceneModels):
/// ฉาก 1 prayer_sheet, handkerchief, ring, footprint
/// ฉาก 2 shirt_button, ticket, footprint, knife, newspaper
/// ฉาก 3 theater_ticket, mask, cane, fabric, note
/// ฉาก 4 train_ticket, bullet, clock, blood, letter
/// </summary>
public class CiraCoreVisionScanner : MonoBehaviour
{
    private static CiraCoreVisionScanner _instance;
    public static CiraCoreVisionScanner Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<CiraCoreVisionScanner>();
            return _instance;
        }
    }

    [Header("=== การตั้งค่าโมเดล CiRA CORE ===")]
    public string modelVersion = "20261005_11_23_53_0.1729";
    public string trainedScene = "Scene 1: St. Mary Church";
    public string[] loadedClasses = new string[] { "prayer_sheet", "handkerchief", "ring", "footprint" };

    /// <summary>ข้อมูลโมเดล CiRA CORE ของแต่ละฉาก (ตรงกับไฟล์ใน Assets/CiraCore)</summary>
    [Serializable]
    public class CiraModelProfile
    {
        public int sceneNumber;
        public string trainedScene;
        public string modelVersion;
        public string modelFolder;
        public string[] classes;

        public bool HasClass(string cls)
        {
            if (string.IsNullOrEmpty(cls) || classes == null) return false;
            for (int i = 0; i < classes.Length; i++)
                if (string.Equals(classes[i], cls, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    /// <summary>โมเดลทั้ง 4 ฉาก: obj.names ของแต่ละโฟลเดอร์</summary>
    public static readonly CiraModelProfile[] SceneModels = new CiraModelProfile[]
    {
        new CiraModelProfile { sceneNumber = 1, trainedScene = "Scene 1: St. Mary Church", modelVersion = "20261005_11_23_53_0.1729",
            modelFolder = "Assets/CiraCore/Scene1-St.Mary Church-20261007T140353Z-1-001/Scene1-St.Mary Church/20261005_11_23_53_0.1729",
            classes = new[] { "prayer_sheet", "handkerchief", "ring", "footprint" } },
        new CiraModelProfile { sceneNumber = 2, trainedScene = "Scene 2: London Alley", modelVersion = "model-scene2",
            modelFolder = "Assets/CiraCore/Scene2-London Alley-20261007T140356Z-1-001/Scene2-London Alley/model-scene2",
            classes = new[] { "shirt_button", "ticket", "footprint", "knife", "newspaper" } },
        new CiraModelProfile { sceneNumber = 3, trainedScene = "Scene 3: Royal Lyceum Theater", modelVersion = "model-scene3",
            modelFolder = "Assets/CiraCore/Scene3-Royal Lyceum Theater/model-scene3",
            classes = new[] { "theater_ticket", "mask", "cane", "fabric", "note" } },
        new CiraModelProfile { sceneNumber = 4, trainedScene = "Scene 4: Underground Station", modelVersion = "model-scene4",
            modelFolder = "Assets/CiraCore/Scene4-Underground Station/model-scene4",
            classes = new[] { "train_ticket", "bullet", "clock", "blood", "letter" } },
    };

    public int ActiveSceneNumber { get; private set; } = 1;
    public CiraModelProfile ActiveModel => GetModel(ActiveSceneNumber);

    public static CiraModelProfile GetModel(int sceneNumber)
    {
        foreach (var m in SceneModels) if (m.sceneNumber == sceneNumber) return m;
        return SceneModels[0];
    }

    /// <summary>สลับโมเดลที่ใช้สแกนตามฉาก (เรียกจาก Quest Manager ตอนเริ่มเควสของฉากนั้น)</summary>
    public void SetActiveSceneModel(int sceneNumber)
    {
        var m = GetModel(sceneNumber);
        ActiveSceneNumber = m.sceneNumber;
        modelVersion = m.modelVersion;
        trainedScene = m.trainedScene;
        loadedClasses = m.classes;
        if (_topLabel != null) _topLabel.text = $"CiRA CORE  ·  FORENSIC LENS  ·  {m.modelVersion.ToUpper()}";
        Debug.Log($"<color=#5CE1E6><b>[CiRA CORE]</b> โหลดโมเดล {m.trainedScene} ({m.modelVersion}) คลาส: {string.Join(", ", m.classes)}</color>");
    }

    /// <summary>หาโมเดลที่รู้จักคลาสของหลักฐานชิ้นนี้ (โมเดลฉากปัจจุบันก่อน)</summary>
    private CiraModelProfile ResolveModelFor(EvidenceItem ev)
    {
        if (ev == null) return ActiveModel;
        if (ActiveModel.HasClass(ev.ciraClassName)) return ActiveModel;
        foreach (var m in SceneModels) if (m.HasClass(ev.ciraClassName)) return m;
        return ActiveModel;
    }

    /// <summary>ค่าความมั่นใจของโมเดล (คงที่ต่อหลักฐานแต่ละชิ้น และเพิ่มขึ้นตามเวลาที่เล็ง)</summary>
    private static float GetConfidence(EvidenceItem ev, float progress)
    {
        int h = (ev.evidenceId ?? ev.name).GetHashCode() & 0x7fffffff;
        float peak = 0.86f + (h % 1200) / 10000f; // 0.86 - 0.98
        return Mathf.Lerp(0.42f, peak, Mathf.Clamp01(progress));
    }

    private Text _topLabel;

    [Header("=== การควบคุมโหมดสแกน ===")]
    [Tooltip("ระยะการสแกนสูงสุด (เมตร) - ต้องเดินเข้าไปใกล้เพื่อสแกน ห้ามสแกนทะลุกำแพง")]
    public float maxScanDistance = 8.0f;

    [Tooltip("เวลาที่ต้องเล็งสแกนเพื่อยืนยันผล (วินาที)")]
    public float requiredScanTime = 1.0f;

    [Header("=== ฟอนต์ภาษาไทย ===")]
    public Font thaiFont;

    // Scan State
    public bool IsScanningActive { get; private set; } = false;
    private EvidenceItem _targetedEvidence = null;
    private float _currentScanTimer = 0f;

    // UI Elements (สไตล์แว่นขยายนักสืบ + ป้ายหลักฐานกระดาษมะนิลา)
    private Font _thaiFontBold;
    private Font _typewriterFont;
    private Canvas _canvas;
    private GameObject _hudContainer;
    private CanvasGroup _hudGroup;
    private RectTransform _boundingBoxRect;
    private RectTransform _tagRect;
    private Text _tagTitleText;
    private Text _tagStatusText;
    private Text _tagStampText;
    private List<Image> _scannerBoxGraphics = new List<Image>();
    private Image _scanProgressBar;
    private Image _reticleProgress;
    private Image _reticleRingImage;
    private RectTransform _reticleRoot;
    private AudioSource _audioSource;

    private static readonly Color LensBrass = new Color(0.83f, 0.68f, 0.36f, 1f);
    private static readonly Color LensDone = new Color(0.50f, 0.80f, 0.52f, 1f);
    private static readonly Color InkRed = new Color(0.62f, 0.08f, 0.07f, 1f);
    private static readonly Color InkGreen = new Color(0.10f, 0.40f, 0.18f, 1f);

    public event Action<EvidenceItem> OnEvidenceScanned;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (thaiFont == null)
        {
            thaiFont = GameAssets.Load<Font>("Assets/Font/Prompt-Medium.ttf")
                    ?? GameAssets.Load<Font>("Assets/Font/Sarabun-Medium.ttf");
            if (thaiFont == null) thaiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        _thaiFontBold = null;
        _thaiFontBold = GameAssets.Load<Font>("Assets/Font/Prompt-Bold.ttf");
        if (_thaiFontBold == null) _thaiFontBold = thaiFont;
        _typewriterFont = Font.CreateDynamicFontFromOSFont(new[] { "Courier New", "Consolas" }, 24);
        if (_typewriterFont == null) _typewriterFont = _thaiFontBold;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;

        BuildScannerHUD();
    }

    [Header("=== การตั้งค่าเสียง ===")]
    [Tooltip("เปิด/ปิดเสียงบี๊บขณะสแกนวัตถุพยาน")]
    public bool enableScanSound = false; // ปิดเสียงตามคำขอของผู้ใช้ (ไม่ชอบเสียงสแกน)

    private static bool _manualScannerBlocked = false;
    public static bool IsScannerBlocked
    {
        get
        {
            if (_manualScannerBlocked) return true;
            if (NewspaperDecryptionMinigame.Instance != null && NewspaperDecryptionMinigame.Instance.IsMinigameActive) return true;
            if (LondonRailwayMapMinigame.Instance != null && LondonRailwayMapMinigame.Instance.IsMinigameActive) return true;
            if (CaseArchiveJournalUI.Instance != null && CaseArchiveJournalUI.Instance.IsOpen) return true;
            if (StMaryChurchQuestManager.Instance != null && StMaryChurchQuestManager.Instance.IsDeductionPanelActive) return true;
            if (CaseDeductionBoardUI.IsOpen || ChapterSummaryUI.IsOpen) return true;
            return false;
        }
        set
        {
            _manualScannerBlocked = value;
        }
    }

    private void Update()
    {
        // เมื่ออยู่ในมินิเกมหรือหน้าต่างเควส ให้บล็อกระบบสแกน CiRA CORE ทันที ไม่ให้ขึ้นกรอบหรือตรวจจับรบกวน
        if (IsScannerBlocked)
        {
            if (IsScanningActive) ToggleScanMode(false);
            if (_boundingBoxRect != null && _boundingBoxRect.gameObject.activeSelf) _boundingBoxRect.gameObject.SetActive(false);
            return;
        }

        // Toggle Scanner Mode: Right Click or V or Tab or F
        bool toggleTriggered = false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) toggleTriggered = true;
        if (Keyboard.current != null && (Keyboard.current.vKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame)) toggleTriggered = true;
#endif

        if (!toggleTriggered)
        {
            try
            {
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.F))
                {
                    toggleTriggered = true;
                }
            }
            catch {}
        }

        if (toggleTriggered)
        {
            ToggleScanMode(!IsScanningActive);
        }

        if (IsScanningActive)
        {
            // จางเข้าตอนเปิดโหมดสแกน + แว่นขยายหายใจเข้าออกเบา ๆ
            if (_hudGroup != null) _hudGroup.alpha = Mathf.MoveTowards(_hudGroup.alpha, 1f, Time.unscaledDeltaTime * 6f);
            if (_reticleRoot != null) _reticleRoot.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(Time.unscaledTime * 3f));
            ProcessComputerVisionScan();
        }
    }

    public void ToggleScanMode(bool active)
    {
        if (active && IsScannerBlocked) active = false;

        bool wasActive = IsScanningActive;
        IsScanningActive = active;
        if (_hudContainer != null) _hudContainer.SetActive(active);
        if (active && !wasActive && _hudGroup != null) _hudGroup.alpha = 0f;

        if (!active)
        {
            _targetedEvidence = null;
            _currentScanTimer = 0f;
            if (_boundingBoxRect != null) _boundingBoxRect.gameObject.SetActive(false);
        }
        else
        {
            if (enableScanSound) PlayBeepSound(600f, 0.08f);
        }
    }

    /// <summary>
    /// สั่งปิดโหมดสแกนและล้างกรอบ Bounding Box ออกจากหน้าจออย่างเด็ดขาด (ใช้ตอนเปิดมินิเกม)
    /// </summary>
    public void ForceCloseScanMode()
    {
        IsScanningActive = false;
        if (_boundingBoxRect != null) _boundingBoxRect.gameObject.SetActive(false);
        if (_hudContainer != null) _hudContainer.SetActive(false);
        _targetedEvidence = null;
        _currentScanTimer = 0f;
    }

    private void ProcessComputerVisionScan()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        EvidenceItem foundEvidence = null;
        float bestScore = float.MaxValue;

        var allEvidences = FindObjectsByType<EvidenceItem>(FindObjectsInactive.Exclude);
        Vector3 camPos = cam.transform.position;
        Vector3 camForward = cam.transform.forward;

        // 1. ตรวจสอบจากการเล็งเป้ากึ่งกลางจอตรงๆ (Direct Reticle Raycast)
        Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(centerRay, out RaycastHit centerHit, maxScanDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            var evDirect = centerHit.collider.GetComponentInParent<EvidenceItem>();
            if (evDirect != null)
            {
                foundEvidence = evDirect;
            }
        }

        // 2. ถ้าไม่ได้เล็งตรงๆ ให้ใช้โหมดค้นหาตามมุมสายตา (Vision Cone Search)
        if (foundEvidence == null)
        {
            foreach (var ev in allEvidences)
            {
                Vector3 center = ev.GetWorldCenter();
                Vector3 toEv = center - camPos;
                float dist = toEv.magnitude;

                // ตรวจสอบระยะทาง: ต้องเดินเข้าไปใกล้ๆ (<= maxScanDistance) ถึงจะสแกนติด
                if (dist > maxScanDistance) continue;

                // ตรวจสอบว่าอยู่ด้านหน้ากล้องและมุมสายตา
                float angle = Vector3.Angle(camForward, toEv);
                if (angle > 32f) continue;

                // ตรวจสอบว่าอยู่บนหน้าจอ (Screen Viewport)
                Vector3 screenPos = cam.WorldToScreenPoint(center);
                if (screenPos.z <= 0.1f || screenPos.x < 0 || screenPos.x > cam.pixelWidth || screenPos.y < 0 || screenPos.y > cam.pixelHeight)
                    continue;

                // ตรวจสอบสิ่งกีดขวาง (Line of Sight Raycast Check): ห้ามสแกนทะลุกำแพง/เสา
                Vector3 rayDir = toEv.normalized;
                bool isOccluded = false;

                RaycastHit[] hits = Physics.RaycastAll(camPos, rayDir, dist - 0.1f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var hit in hits)
                {
                    if (hit.collider == null) continue;
                    if (hit.collider.isTrigger) continue;
                    // ละเว้นตัวละครผู้เล่น (ป้องกันกล้องบุคคลที่ 3 บังตัวเอง)
                    if (hit.collider.transform.root.name.Contains("Player") || hit.collider.transform.root.name.Contains("Newman")) continue;
                    // ละเว้นวัตถุพยานชิ้นอื่น (เช่น แหวนวงเล็กที่อยู่ข้างแหวนวงใหญ่)
                    if (hit.collider.GetComponentInParent<EvidenceItem>() != null) continue;
                    // ละเว้นพื้น/โต๊ะที่หลักฐานวางอยู่ (จุดชนอยู่ภายในกรอบของหลักฐานเอง เช่น นาฬิกาพกที่จมพื้นสถานีเล็กน้อย)
                    if (ev.GetWorldBounds().Contains(hit.point)) continue;

                    // ชนกำแพงหรือสิ่งก่อสร้างจริง
                    isOccluded = true;
                    break;
                }

                if (isOccluded) continue;

                // คำนวณคะแนนความใกล้เคียงกึ่งกลางจอและระยะทาง
                float distWeight = dist;
                float angleWeight = angle * 0.15f;
                float score = distWeight + angleWeight;

                if (score < bestScore)
                {
                    bestScore = score;
                    foundEvidence = ev;
                }
            }
        }

        _targetedEvidence = foundEvidence;

        // Process Targeted Evidence
        if (_targetedEvidence != null)
        {
            Bounds b = _targetedEvidence.GetWorldBounds();
            Vector3 min = b.min;
            Vector3 max = b.max;
            Vector3[] corners = new Vector3[8]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
            };

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            bool allInFront = true;

            for (int i = 0; i < 8; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint(corners[i]);
                if (sp.z <= 0) { allInFront = false; break; }
                if (sp.x < minX) minX = sp.x;
                if (sp.x > maxX) maxX = sp.x;
                if (sp.y < minY) minY = sp.y;
                if (sp.y > maxY) maxY = sp.y;
            }

            if (allInFront)
            {
                _boundingBoxRect.gameObject.SetActive(true);

                float padding = 20f;
                float boxW = Mathf.Clamp(maxX - minX + padding, 90f, 320f);
                float boxH = Mathf.Clamp(maxY - minY + padding, 70f, 260f);
                float uiScale = (_canvas != null && _canvas.scaleFactor > 0.01f) ? _canvas.scaleFactor : 1f;

                float centerX = (minX + maxX) * 0.5f;
                float centerY = (minY + maxY) * 0.5f;
                if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    // แคนวาสแบบ Screen Space Camera (เช่นตอนอัดคลิป): แปลงพิกัดจอเป็นตำแหน่งบนระนาบแคนวาส
                    RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)_canvas.transform, new Vector2(centerX, centerY), _canvas.worldCamera, out Vector3 wp);
                    _boundingBoxRect.position = wp;
                }
                else _boundingBoxRect.position = new Vector3(centerX, centerY, 0f);
                _boundingBoxRect.sizeDelta = new Vector2(boxW, boxH) / uiScale;

                // ป้ายหลักฐานปกติอยู่เหนือกรอบ ถ้าใกล้ขอบบนจอให้ย้ายไปไว้ใต้กรอบ
                if (_tagRect != null)
                {
                    bool flipBelow = centerY + boxH * 0.5f + (TagHeight + 16f) * uiScale > cam.pixelHeight;
                    if (flipBelow)
                    {
                        _tagRect.anchorMin = _tagRect.anchorMax = new Vector2(0f, 0f);
                        _tagRect.pivot = new Vector2(0f, 1f);
                        _tagRect.anchoredPosition = new Vector2(0f, -14f);
                    }
                    else
                    {
                        _tagRect.anchorMin = _tagRect.anchorMax = new Vector2(0f, 1f);
                        _tagRect.pivot = new Vector2(0f, 0f);
                        _tagRect.anchoredPosition = new Vector2(0f, 14f);
                    }
                }

                bool isComplete = _targetedEvidence.isScanned;
                float progress = Mathf.Clamp01(_currentScanTimer / requiredScanTime);

                // โทนสีทองเหลืองของแว่นขยาย (กำลังพินิจ) -> เขียวหม่น (บันทึกแล้ว)
                Color themeColor = isComplete ? LensDone : LensBrass;

                for (int i = 0; i < _scannerBoxGraphics.Count; i++)
                {
                    if (_scannerBoxGraphics[i] != null) _scannerBoxGraphics[i].color = themeColor;
                }

                if (_reticleRingImage != null) _reticleRingImage.color = new Color(themeColor.r, themeColor.g, themeColor.b, 0.9f);
                if (_reticleProgress != null)
                {
                    _reticleProgress.color = isComplete ? LensDone : new Color(0.92f, 0.30f, 0.22f, 1f);
                    _reticleProgress.fillAmount = isComplete ? 1f : progress;
                }
                if (_scanProgressBar != null)
                {
                    _scanProgressBar.color = isComplete ? InkGreen : InkRed;
                    _scanProgressBar.fillAmount = isComplete ? 1f : progress;
                }

                if (_tagTitleText != null) _tagTitleText.text = _targetedEvidence.thaiTitle;
                var detectModel = ResolveModelFor(_targetedEvidence);
                string detectedClass = detectModel.HasClass(_targetedEvidence.ciraClassName) ? _targetedEvidence.ciraClassName : "unknown";

                if (_tagStatusText != null)
                {
                    if (isComplete)
                    {
                        _tagStatusText.text = $"[{detectedClass} {GetConfidence(_targetedEvidence, 1f):0.00}]  บันทึกลงสำนวนคดีเรียบร้อย";
                        _tagStatusText.color = InkGreen;
                    }
                    else
                    {
                        _tagStatusText.text = $"[{detectedClass} {GetConfidence(_targetedEvidence, progress):0.00}]  กำลังพินิจร่องรอย...  {(int)(progress * 100)}%";
                        _tagStatusText.color = new Color(0.30f, 0.22f, 0.14f, 1f);
                    }
                }
                if (_tagStampText != null) _tagStampText.gameObject.SetActive(isComplete);
                // Advance scanning
                if (!_targetedEvidence.isScanned)
                {
                    _currentScanTimer += Time.deltaTime;

                    if (_currentScanTimer >= requiredScanTime)
                    {
                        // Successfully Scanned!
                        _targetedEvidence.isScanned = true;
                        _currentScanTimer = 0f;
                        if (_scanProgressBar != null) _scanProgressBar.fillAmount = 1f;

                        PlaySuccessChime();
                        OnEvidenceScanned?.Invoke(_targetedEvidence);

                        if (StMaryChurchQuestManager.Instance != null && !StMaryChurchQuestManager.Instance.IsQuest1Finished)
                        {
                            StMaryChurchQuestManager.Instance.HandleEvidenceScanned(_targetedEvidence);
                        }
                    }
                }
                else
                {
                    if (_scanProgressBar != null) _scanProgressBar.fillAmount = 1f;

                    // Fail-safe: หากสแกนวัตถุผ่านแล้ว แต่ตัวนับเควสยังไม่ได้บันทึก ให้ส่งค่าซ้ำทันที
                    if (StMaryChurchQuestManager.Instance != null && !StMaryChurchQuestManager.Instance.IsQuest1Finished)
                    {
                        StMaryChurchQuestManager.Instance.HandleEvidenceScanned(_targetedEvidence);
                    }
                }
            }
            else
            {
                _boundingBoxRect.gameObject.SetActive(false);
            }
        }
        else
        {
            _currentScanTimer = 0f;
            if (_scanProgressBar != null) _scanProgressBar.fillAmount = 0f;
            if (_reticleProgress != null) _reticleProgress.fillAmount = 0f;
            if (_reticleRingImage != null) _reticleRingImage.color = new Color(LensBrass.r, LensBrass.g, LensBrass.b, 0.85f);
            if (_boundingBoxRect != null) _boundingBoxRect.gameObject.SetActive(false);
        }
    }

    private const float TagHeight = 98f;
    private const float TagWidth = 500f;
    private Sprite _whiteSprite;

    private Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null)
            {
                var t = Texture2D.whiteTexture;
                _whiteSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
            }
            return _whiteSprite;
        }
    }

    /// <summary>
    /// UI โหมดสแกนสไตล์นักสืบยุควิกตอเรีย:
    /// แว่นขยายทองเหลืองกลางจอ (วงแหวนเติมตามความคืบหน้าการพินิจ) + ป้ายหลักฐานกระดาษมะนิลาเหนือวัตถุ
    /// + ขอบมืดซีเปียรอบจอ + ป้ายคำแนะนำล่างจอ
    /// </summary>
    private void BuildScannerHUD()
    {
        _canvas = GetComponent<Canvas>();
        if (_canvas == null)
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 900;
        }
        if (GetComponent<CanvasScaler>() == null)
        {
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        _hudContainer = new GameObject("CiRA_Core_Scanner_HUD", typeof(RectTransform), typeof(CanvasGroup));
        _hudContainer.transform.SetParent(transform, false);
        _hudGroup = _hudContainer.GetComponent<CanvasGroup>();
        _hudGroup.blocksRaycasts = false;
        _hudGroup.interactable = false;

        RectTransform hudRect = _hudContainer.GetComponent<RectTransform>();
        hudRect.anchorMin = Vector2.zero;
        hudRect.anchorMax = Vector2.one;
        hudRect.offsetMin = Vector2.zero;
        hudRect.offsetMax = Vector2.zero;

        Vector2 center = new Vector2(0.5f, 0.5f);

        // 1. ขอบมืดซีเปียรอบจอ ให้ความรู้สึกมองผ่านเลนส์
        var vignette = MakeImage("Vignette", _hudContainer.transform, center, Vector2.zero, Vector2.zero, Color.white, DetectiveUIShapes.Vignette);
        var vRect = vignette.rectTransform;
        vRect.anchorMin = Vector2.zero;
        vRect.anchorMax = Vector2.one;
        vRect.offsetMin = Vector2.zero;
        vRect.offsetMax = Vector2.zero;

        // 2. ป้ายหัวโหมด (บนกลางจอ)
        var topLabel = _topLabel = MakeText("TopLabel", _hudContainer.transform, new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(640f, 36f),
            "CiRA CORE  ·  FORENSIC LENS", _typewriterFont, 20, new Color(LensBrass.r, LensBrass.g, LensBrass.b, 0.9f), TextAnchor.MiddleCenter);
        MakeImage("TopLineL", _hudContainer.transform, new Vector2(0.5f, 1f), new Vector2(-380f, -112f), new Vector2(150f, 2f), new Color(LensBrass.r, LensBrass.g, LensBrass.b, 0.6f));
        MakeImage("TopLineR", _hudContainer.transform, new Vector2(0.5f, 1f), new Vector2(380f, -112f), new Vector2(150f, 2f), new Color(LensBrass.r, LensBrass.g, LensBrass.b, 0.6f));

        // 3. ป้ายคำแนะนำ (ล่างกลางจอ)
        var plate = MakeImage("HintPlate", _hudContainer.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(960f, 52f), new Color(0.07f, 0.05f, 0.035f, 0.82f));
        MakeImage("HintLine", plate.transform, new Vector2(0.5f, 1f), new Vector2(0f, -1f), new Vector2(960f, 2f), new Color(LensBrass.r, LensBrass.g, LensBrass.b, 0.85f));
        var hint = MakeText("HintText", plate.transform, center, Vector2.zero, new Vector2(940f, 52f),
            "เล็งวัตถุค้างไว้เพื่อพินิจหลักฐาน        [คลิกขวา] หรือ [V]  ปิดโหมดตรวจพิสูจน์", thaiFont, 21, DetectiveUITheme.AgedPaper, TextAnchor.MiddleCenter);

        // 4. แว่นขยายกลางจอ (Reticle)
        _reticleRoot = MakeRect("LensReticle", _hudContainer.transform, center, Vector2.zero, new Vector2(160f, 160f));
        _reticleRingImage = MakeImage("Ring", _reticleRoot, center, Vector2.zero, new Vector2(160f, 160f),
            new Color(LensBrass.r, LensBrass.g, LensBrass.b, 0.85f), DetectiveUIShapes.Ring(0.035f));
        MakeImage("InnerRing", _reticleRoot, center, Vector2.zero, new Vector2(116f, 116f),
            new Color(LensBrass.r, LensBrass.g, LensBrass.b, 0.3f), DetectiveUIShapes.Ring(0.012f));

        _reticleProgress = MakeImage("Progress", _reticleRoot, center, Vector2.zero, new Vector2(160f, 160f),
            new Color(0.92f, 0.30f, 0.22f, 1f), DetectiveUIShapes.Ring(0.075f));
        _reticleProgress.type = Image.Type.Filled;
        _reticleProgress.fillMethod = Image.FillMethod.Radial360;
        _reticleProgress.fillOrigin = (int)Image.Origin360.Top;
        _reticleProgress.fillClockwise = true;
        _reticleProgress.fillAmount = 0f;

        Color tickColor = new Color(LensBrass.r, LensBrass.g, LensBrass.b, 0.8f);
        MakeImage("TickN", _reticleRoot, center, new Vector2(0f, 56f), new Vector2(3f, 16f), tickColor);
        MakeImage("TickS", _reticleRoot, center, new Vector2(0f, -56f), new Vector2(3f, 16f), tickColor);
        MakeImage("TickE", _reticleRoot, center, new Vector2(56f, 0f), new Vector2(16f, 3f), tickColor);
        MakeImage("TickW", _reticleRoot, center, new Vector2(-56f, 0f), new Vector2(16f, 3f), tickColor);
        MakeImage("Dot", _reticleRoot, center, Vector2.zero, new Vector2(6f, 6f), tickColor, DetectiveUIShapes.Circle);

        // ด้ามแว่นขยาย: คอทองเหลือง + ด้ามไม้เข้ม ทแยงลงขวา
        var shaft = MakeImage("HandleShaft", _reticleRoot, center, new Vector2(57f, -57f), new Vector2(10f, 30f),
            new Color(0.62f, 0.48f, 0.24f, 1f), null, new Vector2(0.5f, 1f));
        shaft.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        var grip = MakeImage("HandleGrip", shaft.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(16f, 52f),
            new Color(0.20f, 0.12f, 0.07f, 1f), null, new Vector2(0.5f, 1f));

        // 5. กรอบล้อมวัตถุ + ป้ายหลักฐาน
        _scannerBoxGraphics.Clear();
        GameObject boxGo = new GameObject("BoundingBox", typeof(RectTransform));
        boxGo.transform.SetParent(_hudContainer.transform, false);
        _boundingBoxRect = boxGo.GetComponent<RectTransform>();
        _boundingBoxRect.sizeDelta = new Vector2(260f, 160f);

        float cornerLen = 26f;
        float cornerThick = 3.5f;
        Color initColor = LensBrass;
        CreateCornerBracket(boxGo, "C_TL_H", new Vector2(0f, 1f), new Vector2(cornerLen, cornerThick), new Vector2(cornerLen * 0.5f, -cornerThick * 0.5f), initColor);
        CreateCornerBracket(boxGo, "C_TL_V", new Vector2(0f, 1f), new Vector2(cornerThick, cornerLen), new Vector2(cornerThick * 0.5f, -cornerLen * 0.5f), initColor);
        CreateCornerBracket(boxGo, "C_TR_H", new Vector2(1f, 1f), new Vector2(cornerLen, cornerThick), new Vector2(-cornerLen * 0.5f, -cornerThick * 0.5f), initColor);
        CreateCornerBracket(boxGo, "C_TR_V", new Vector2(1f, 1f), new Vector2(cornerThick, cornerLen), new Vector2(-cornerThick * 0.5f, -cornerLen * 0.5f), initColor);
        CreateCornerBracket(boxGo, "C_BL_H", new Vector2(0f, 0f), new Vector2(cornerLen, cornerThick), new Vector2(cornerLen * 0.5f, cornerThick * 0.5f), initColor);
        CreateCornerBracket(boxGo, "C_BL_V", new Vector2(0f, 0f), new Vector2(cornerThick, cornerLen), new Vector2(cornerThick * 0.5f, cornerLen * 0.5f), initColor);
        CreateCornerBracket(boxGo, "C_BR_H", new Vector2(1f, 0f), new Vector2(cornerLen, cornerThick), new Vector2(-cornerLen * 0.5f, cornerThick * 0.5f), initColor);
        CreateCornerBracket(boxGo, "C_BR_V", new Vector2(1f, 0f), new Vector2(cornerThick, cornerLen), new Vector2(-cornerThick * 0.5f, cornerLen * 0.5f), initColor);

        // ป้ายหลักฐานกระดาษมะนิลา มีรูเจาะและตราประทับ
        _tagRect = MakeRect("EvidenceTag", boxGo.transform, new Vector2(0f, 1f), new Vector2(0f, 14f), new Vector2(TagWidth, TagHeight));
        _tagRect.pivot = new Vector2(0f, 0f);
        _tagRect.anchoredPosition = new Vector2(0f, 14f);

        MakeImage("Shadow", _tagRect, center, new Vector2(6f, -7f), new Vector2(TagWidth, TagHeight), new Color(0f, 0f, 0f, 0.45f));

        var paperGo = new GameObject("Paper", typeof(RectTransform), typeof(RawImage));
        paperGo.transform.SetParent(_tagRect, false);
        var paperRect = paperGo.GetComponent<RectTransform>();
        paperRect.anchorMin = Vector2.zero;
        paperRect.anchorMax = Vector2.one;
        paperRect.offsetMin = Vector2.zero;
        paperRect.offsetMax = Vector2.zero;
        var paper = paperGo.GetComponent<RawImage>();
        paper.raycastTarget = false;
        Sprite note = DetectiveUITheme.NoteCard;
        if (note != null)
        {
            paper.texture = note.texture;
            paper.uvRect = new Rect(0.2f, 0.42f, 0.6f, 0.12f);
            paper.color = new Color(1f, 0.93f, 0.8f, 1f);
        }
        else paper.color = DetectiveUITheme.ManilaFolder;

        // รูเจาะสำหรับร้อยเชือก
        MakeImage("HoleRim", _tagRect, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(20f, 20f), new Color(0.35f, 0.26f, 0.15f, 0.9f), DetectiveUIShapes.Circle);
        MakeImage("Hole", _tagRect, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(13f, 13f), new Color(0.06f, 0.05f, 0.04f, 1f), DetectiveUIShapes.Circle);

        _tagTitleText = MakeText("TagTitle", _tagRect, center, Vector2.zero, Vector2.zero, "", _thaiFontBold != null ? _thaiFontBold : thaiFont, 26,
            DetectiveUITheme.InkDark, TextAnchor.MiddleLeft);
        var ttRect = _tagTitleText.rectTransform;
        ttRect.anchorMin = new Vector2(0f, 0.5f);
        ttRect.anchorMax = new Vector2(1f, 1f);
        ttRect.offsetMin = new Vector2(50f, 0f);
        ttRect.offsetMax = new Vector2(-150f, -8f);
        _tagTitleText.resizeTextForBestFit = true;
        _tagTitleText.resizeTextMinSize = 16;
        _tagTitleText.resizeTextMaxSize = 26;
        _tagTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _tagTitleText.verticalOverflow = VerticalWrapMode.Truncate;

        _tagStatusText = MakeText("TagStatus", _tagRect, center, Vector2.zero, Vector2.zero, "", thaiFont, 19,
            new Color(0.30f, 0.22f, 0.14f, 1f), TextAnchor.MiddleLeft);
        var stRect = _tagStatusText.rectTransform;
        stRect.anchorMin = new Vector2(0f, 0f);
        stRect.anchorMax = new Vector2(1f, 0.5f);
        stRect.offsetMin = new Vector2(50f, 14f);
        stRect.offsetMax = new Vector2(-16f, 0f);

        // ตราประทับ "บันทึกแล้ว"
        _tagStampText = MakeText("TagStamp", _tagRect, new Vector2(1f, 1f), new Vector2(-18f, -22f), new Vector2(150f, 36f),
            "บันทึกแล้ว", _thaiFontBold != null ? _thaiFontBold : thaiFont, 24, new Color(InkRed.r, InkRed.g, InkRed.b, 0.85f), TextAnchor.MiddleCenter);
        _tagStampText.rectTransform.pivot = new Vector2(1f, 0.5f);
        _tagStampText.rectTransform.localEulerAngles = new Vector3(0f, 0f, -9f);
        _tagStampText.fontStyle = FontStyle.Italic;
        _tagStampText.gameObject.SetActive(false);

        // แถบความคืบหน้าหมึกแดงชิดขอบล่างป้าย
        var track = MakeImage("ProgressTrack", _tagRect, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 6f), new Color(0.14f, 0.1f, 0.07f, 0.28f));
        var trackRect = track.rectTransform;
        trackRect.anchorMin = new Vector2(0f, 0f);
        trackRect.anchorMax = new Vector2(1f, 0f);
        trackRect.pivot = new Vector2(0.5f, 0f);
        trackRect.sizeDelta = new Vector2(0f, 6f);
        _scanProgressBar = MakeImage("ProgressFill", trackRect, center, Vector2.zero, Vector2.zero, InkRed, WhiteSprite);
        var fillRect = _scanProgressBar.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        _scanProgressBar.type = Image.Type.Filled;
        _scanProgressBar.fillMethod = Image.FillMethod.Horizontal;
        _scanProgressBar.fillAmount = 0f;

        boxGo.SetActive(false);
        _hudContainer.SetActive(false);
    }

    // ---- UI factory ----
    private RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = anchor;
        r.anchorMax = anchor;
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        return r;
    }

    private Image MakeImage(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Sprite sprite = null, Vector2? pivot = null)
    {
        var r = MakeRect(name, parent, anchor, pos, size);
        if (pivot.HasValue) r.pivot = pivot.Value;
        var img = r.gameObject.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private Text MakeText(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string text, Font font, int fontSize, Color color, TextAnchor align)
    {
        var r = MakeRect(name, parent, anchor, pos, size);
        var t = r.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private Image CreateBorderLine(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPos, Color color)
    {
        GameObject line = new GameObject(name, typeof(RectTransform), typeof(Image));
        line.transform.SetParent(parent.transform, false);
        RectTransform rt = line.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPos;
        Image img = line.GetComponent<Image>();
        img.color = color;
        _scannerBoxGraphics.Add(img);
        return img;
    }

    private Image CreateCornerBracket(GameObject parent, string name, Vector2 anchor, Vector2 size, Vector2 anchoredPos, Color color)
    {
        GameObject corner = new GameObject(name, typeof(RectTransform), typeof(Image));
        corner.transform.SetParent(parent.transform, false);
        RectTransform rt = corner.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        Image img = corner.GetComponent<Image>();
        img.color = color;
        _scannerBoxGraphics.Add(img);
        return img;
    }

    private Sprite CreateCircleSprite(int diameter)
    {
        Texture2D tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float radius = (diameter - 1) / 2f;
        Vector2 center = new Vector2(radius, radius);
        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - dist) + 0.6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f));
    }

    private void PlayBeepSound(float freq, float duration)
    {
        if (!enableScanSound || _audioSource == null) return;
        AudioClip clip = CreateToneClip(freq, duration);
        _audioSource.PlayOneShot(clip, 0.35f);
    }

    private void PlaySuccessChime()
    {
        if (!enableScanSound || _audioSource == null) return;
        AudioClip chime = CreateChimeClip();
        _audioSource.PlayOneShot(chime, 0.55f);
    }

    private AudioClip CreateToneClip(float frequency, float duration)
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Clamp01(1f - t / duration);
        }
        AudioClip clip = AudioClip.Create("Beep", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateChimeClip()
    {
        int sampleRate = 44100;
        float duration = 0.5f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float s1 = Mathf.Sin(2f * Mathf.PI * 880f * t);
            float s2 = Mathf.Sin(2f * Mathf.PI * 1320f * t);
            samples[i] = (s1 * 0.6f + s2 * 0.4f) * Mathf.Clamp01(1f - t / duration);
        }
        AudioClip clip = AudioClip.Create("SuccessChime", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
