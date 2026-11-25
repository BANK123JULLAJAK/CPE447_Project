using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// แฟ้มคดีสืบสวน (Scotland Yard Case Archive) เมนูเควสแบบ Genshin Impact ในธีมนักสืบยุควิกตอเรีย
/// - ซ้าย: การ์ดบทที่ 1-4 บนปกหนังสีเข้มขอบทองเหลือง (เลขโรมัน ชื่อบท สถานะ ระยะทาง) บทที่ยังไม่ปลดล็อกจะถูกปิดผนึก
/// - ขวา: กระดาษบันทึกคดี เป้าหมายปัจจุบัน สรุปคดี ผลการอนุมาน และบัญชีวัตถุพยาน
/// - พื้นหลังในโลกเกมถูกเบลอด้วย Depth of Field ตอนเปิดเมนู
/// - กด [E] เพื่อเปิด/ปิด, [ESC] ปิด, [W/S] หรือ [1-4] เลือกบท
/// </summary>
public class CaseArchiveJournalUI : MonoBehaviour
{
    private static CaseArchiveJournalUI _instance;
    public static CaseArchiveJournalUI Instance => _instance;

    [Header("=== ฟอนต์ภาษาไทย (Typography) ===")]
    public Font thaiFont;
    public Font thaiFontBold;

    // UI Root Elements
    private Canvas _canvas;
    private GameObject _modalPanel;
    private CanvasGroup _panelGroup;
    private RectTransform _boardRect;
    private Font _typewriterFont;

    // Background Blur Post-Processing
    private Volume _blurVolume;
    private DepthOfField _blurDof;
    private Coroutine _blurFadeCoroutine;

    // Right page
    private Text _chapterLabel;
    private Text _titleText;
    private Text _locationText;
    private Text _stampText;
    private UIParallelogramGraphic _stampFrame;
    private Text _objectiveLabel;
    private Text _objectiveText;
    private Text _summaryHeader;
    private Text _summaryText;
    private Text _evidenceHeader;
    private Text _evidenceProgressText;
    private readonly List<Text> _evidenceRows = new List<Text>();
    private readonly List<Image> _evidenceBullets = new List<Image>();
    private Button _navButton;
    private CanvasGroup _navGroup;

    // State
    private bool _isOpen = false;
    public bool IsOpen => _isOpen;
    private int _selectedQuestIndex = 0;
    private bool _wasWaypointVisibleBeforeOpen = false;
    private Coroutine _openAnim;

    private const int ChapterCount = 4;
    private const int MaxEvidenceRows = 6;
    private static readonly string[] Romans = { "I", "II", "III", "IV" };

    private static readonly Color Leather = new Color(0.105f, 0.075f, 0.05f, 1f);
    private static readonly Color CardBase = new Color(0.17f, 0.125f, 0.085f, 1f);
    private static readonly Color CardSelected = new Color(0.27f, 0.19f, 0.115f, 1f);
    private static readonly Color Brass = new Color(0.83f, 0.68f, 0.36f, 1f);
    private static readonly Color InkRed = new Color(0.62f, 0.08f, 0.07f, 1f);
    private static readonly Color InkGreen = new Color(0.10f, 0.40f, 0.18f, 1f);
    private static readonly Color LockedGray = new Color(0.52f, 0.46f, 0.38f, 1f);

    private class EvidenceLine
    {
        public string title;
        public string note;

        public EvidenceLine(string title, string note)
        {
            this.title = title;
            this.note = note;
        }
    }

    private class QuestEntryData
    {
        public int chapterIndex;
        public string title;
        public string category;
        public string location;
        public string currentObjective;
        public string fullDescription;
        public string conclusion;
        public string distanceText;
        public bool isCompleted;
        public bool isActive;
        public bool isLocked;
        public int scannedCount = -1;
        public int totalEvidence = 5;
        public List<EvidenceLine> evidence = new List<EvidenceLine>();
    }

    private readonly List<QuestEntryData> _questDataList = new List<QuestEntryData>();

    private class CardView
    {
        public RectTransform rect;
        public Image baseImg;
        public UIParallelogramGraphic frame;
        public Graphic accentBar;
        public Image numeralBox;
        public Text numeral;
        public Text category;
        public Text title;
        public Text status;
        public Text lockedTag;
        public Vector2 basePos;
    }

    private readonly List<CardView> _cards = new List<CardView>();

    // Animation refs
    private Texture2D _folderTex;
    private Texture2D _paperTex;
    private static readonly Rect SlipUv = new Rect(0.10f, 0.27f, 0.78f, 0.30f);
    private static readonly Rect LeatherUv = new Rect(0.14f, 0.1227f, 0.30f, 0.3230f);
    private readonly Vector2 _pageBasePos = new Vector2(345f, -5f);
    private RectTransform _pageRect;
    private RectTransform _stampRect;
    private CanvasGroup _pageGroup;
    private RectTransform _coverRect;
    private Image _coverShade;
    private readonly List<CanvasGroup> _cardGroups = new List<CanvasGroup>();
    private readonly List<CanvasGroup> _rowGroups = new List<CanvasGroup>();
    private readonly List<CanvasGroup> _bulletGroups = new List<CanvasGroup>();
    private Coroutine _swapAnim;
    private Coroutine _closeAnim;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (thaiFont == null) thaiFont = GameAssets.Load<Font>("Assets/Font/Sarabun-Medium.ttf");
        if (thaiFontBold == null) thaiFontBold = GameAssets.Load<Font>("Assets/Font/Prompt-Bold.ttf");
        if (thaiFont == null) thaiFont = VisionOSUITheme.PrimaryFont;
        if (thaiFontBold == null) thaiFontBold = VisionOSUITheme.BoldFont;
        _typewriterFont = Font.CreateDynamicFontFromOSFont(new[] { "Courier New", "Consolas" }, 24);
        if (_typewriterFont == null) _typewriterFont = thaiFontBold;

        _folderTex = GameAssets.Load<Texture2D>("Assets/UI/Folder/Gemini_Generated_Image_jds4szjds4szjds4-Photoroom.png");
        _paperTex = GameAssets.Load<Texture2D>("Assets/UI/Folder/Gemini_Generated_Image_oht62eoht62eoht6-Photoroom.png");
        BuildJournalUI();
        EnsureBlurVolume();
    }

    private void OnDisable()
    {
        if (_blurFadeCoroutine != null)
        {
            StopCoroutine(_blurFadeCoroutine);
            _blurFadeCoroutine = null;
        }
        if (_blurVolume != null)
        {
            _blurVolume.weight = 0f;
        }
    }

    private void OnDestroy()
    {
        if (_blurFadeCoroutine != null)
        {
            StopCoroutine(_blurFadeCoroutine);
            _blurFadeCoroutine = null;
        }
        if (_blurVolume != null)
        {
            _blurVolume.weight = 0f;
            if (_blurVolume.profile != null)
            {
                Destroy(_blurVolume.profile);
            }
            _blurVolume = null;
        }
    }

    // ==========================================
    // Background blur (URP Depth of Field)
    // ==========================================
    private void EnsureBlurVolume()
    {
        if (_blurVolume != null) return;

        _blurVolume = GetComponent<Volume>();
        if (_blurVolume == null) _blurVolume = gameObject.AddComponent<Volume>();
        _blurVolume.isGlobal = true;
        _blurVolume.priority = 250f;
        _blurVolume.weight = 0f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.DontSave;
        _blurVolume.profile = profile;

        _blurDof = profile.Add<DepthOfField>(true);
        _blurDof.active = true;

        // กำหนดทั้ง Gaussian และ Bokeh ให้ครอบคลุมทุกการตั้งค่า URP
        _blurDof.mode.overrideState = true;
        _blurDof.mode.value = DepthOfFieldMode.Gaussian;
        _blurDof.gaussianStart.overrideState = true;
        _blurDof.gaussianStart.value = 0.05f;
        _blurDof.gaussianEnd.overrideState = true;
        _blurDof.gaussianEnd.value = 1.0f;
        _blurDof.gaussianMaxRadius.overrideState = true;
        _blurDof.gaussianMaxRadius.value = 1.5f;
        _blurDof.highQualitySampling.overrideState = true;
        _blurDof.highQualitySampling.value = true;

        _blurDof.focusDistance.overrideState = true;
        _blurDof.focusDistance.value = 0.1f;
        _blurDof.focalLength.overrideState = true;
        _blurDof.focalLength.value = 200f;
        _blurDof.aperture.overrideState = true;
        _blurDof.aperture.value = 1.0f;

        var colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.active = true;
        colorAdj.postExposure.overrideState = true;
        colorAdj.postExposure.value = -0.4f;

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            var camData = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null) camData.renderPostProcessing = true;
        }
    }

    private void SetBlurActive(bool active)
    {
        if (_blurVolume == null) EnsureBlurVolume();
        if (_blurVolume == null) return;

        if (!gameObject.activeInHierarchy)
        {
            _blurVolume.weight = active ? 1f : 0f;
            return;
        }

        if (_blurFadeCoroutine != null) StopCoroutine(_blurFadeCoroutine);
        _blurFadeCoroutine = StartCoroutine(FadeBlurRoutine(active ? 1f : 0f, active ? 0.2f : 0.15f));
    }

    private IEnumerator FadeBlurRoutine(float targetWeight, float duration)
    {
        float startWeight = _blurVolume.weight;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            _blurVolume.weight = Mathf.Lerp(startWeight, targetWeight, elapsed / duration);
            yield return null;
        }
        _blurVolume.weight = targetWeight;
    }

    // ==========================================
    // Input / open / close
    // ==========================================
    private void Update()
    {
        bool ePressed = false;
        bool escPressed = false;
        int moveDir = 0;
        int numberKey = -1;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.eKey.wasPressedThisFrame) ePressed = true;
            if (kb.escapeKey.wasPressedThisFrame) escPressed = true;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) moveDir = -1;
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) moveDir = 1;
            if (kb.digit1Key.wasPressedThisFrame) numberKey = 0;
            else if (kb.digit2Key.wasPressedThisFrame) numberKey = 1;
            else if (kb.digit3Key.wasPressedThisFrame) numberKey = 2;
            else if (kb.digit4Key.wasPressedThisFrame) numberKey = 3;
        }
#else
        if (Input.GetKeyDown(KeyCode.E)) ePressed = true;
        if (Input.GetKeyDown(KeyCode.Escape)) escPressed = true;
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) moveDir = -1;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) moveDir = 1;
        if (Input.GetKeyDown(KeyCode.Alpha1)) numberKey = 0;
        else if (Input.GetKeyDown(KeyCode.Alpha2)) numberKey = 1;
        else if (Input.GetKeyDown(KeyCode.Alpha3)) numberKey = 2;
        else if (Input.GetKeyDown(KeyCode.Alpha4)) numberKey = 3;
#endif

        if (ePressed)
        {
            if (NewspaperDecryptionMinigame.Instance != null && NewspaperDecryptionMinigame.Instance.IsMinigameActive) return;
            if (LondonRailwayMapMinigame.Instance != null && LondonRailwayMapMinigame.Instance.IsMinigameActive) return;
            if (EvidenceConnectingDotsMinigame.Instance != null && EvidenceConnectingDotsMinigame.Instance.IsMinigameActive) return;
            if (CinematicLetterbox.Instance != null && CinematicLetterbox.Instance.IsLetterboxActive) return;
            if (CaseDeductionBoardUI.IsOpen || ChapterSummaryUI.IsOpen || TitleMenuUI.IsOpen) return;

            ToggleQuestMenu();
        }
        else if (escPressed && _isOpen)
        {
            CloseQuestMenu();
        }
        else if (_isOpen)
        {
            if (moveDir != 0) SelectQuest((_selectedQuestIndex + moveDir + _questDataList.Count) % Mathf.Max(1, _questDataList.Count));
            if (numberKey >= 0) SelectQuest(numberKey);
        }
    }

    public void ToggleQuestMenu()
    {
        if (_isOpen) CloseQuestMenu();
        else OpenQuestMenu();
    }

    public void OpenQuestMenu()
    {
        _isOpen = true;
        RefreshQuestData();
        SelectQuest(_selectedQuestIndex);

        if (_modalPanel != null)
        {
            if (_closeAnim != null) { StopCoroutine(_closeAnim); _closeAnim = null; }
            if (_swapAnim != null) { StopCoroutine(_swapAnim); _swapAnim = null; }
            _modalPanel.SetActive(true);
            if (_openAnim != null) StopCoroutine(_openAnim);
            _openAnim = StartCoroutine(OpenAnimRoutine());
        }

        // ซ่อนหมุดนำทางบนหน้าจอชั่วคราว ไม่ให้ซ้อนทับกับหน้าต่างแฟ้มคดี
        if (WaypointNavigationSystem.Instance != null)
        {
            _wasWaypointVisibleBeforeOpen = WaypointNavigationSystem.Instance.IsVisible;
            WaypointNavigationSystem.Instance.SetVisible(false);
        }

        // ซ่อนแถบแสดงภารกิจ (Objective HUD) ของทุกฉากชั่วคราว
        if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
        if (LondonAlleyQuestManager.Instance != null) LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
        if (RoyalLyceumTheaterQuestManager.Instance != null) RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(false);
        if (UndergroundStationQuestManager.Instance != null) UndergroundStationQuestManager.Instance.ShowObjectiveHUD(false);

        SetBlurActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.SetPlayerControlsLocked(true);
        }
    }

    public void CloseQuestMenu()
    {
        _isOpen = false;

        if (_openAnim != null)
        {
            StopCoroutine(_openAnim);
            _openAnim = null;
        }
        if (_swapAnim != null) { StopCoroutine(_swapAnim); _swapAnim = null; }
        if (_modalPanel != null && _modalPanel.activeSelf)
        {
            if (_closeAnim != null) StopCoroutine(_closeAnim);
            _closeAnim = StartCoroutine(CloseAnimRoutine());
        }

        // คืนค่าการแสดงผลหมุดนำทาง
        if (WaypointNavigationSystem.Instance != null && _wasWaypointVisibleBeforeOpen)
        {
            WaypointNavigationSystem.Instance.SetVisible(true);
        }

        // คืนค่าการแสดงผลแถบภารกิจตามเควสที่กำลังดำเนินอยู่
        RestoreActiveQuestHUD();

        SetBlurActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.SetPlayerControlsLocked(false);
        }
    }

    /// <summary>ปกแฟ้มคดีหนัง (ตัดจากหนังในภาพแฟ้มของผู้ใช้) บานพับด้านซ้าย: เปิดออกตอนกด E เหมือนเปิดแฟ้มจริง</summary>
    private void BuildCover()
    {
        // ตัวแฟ้มจริงกว้าง ~1462 สูง ~900 อยู่กลาง (5, -6) ปกพับเปิดไปทางซ้ายรอบบานพับด้านซ้ายของแฟ้ม
        const float w = 1462f;
        const float h = 900f;
        _coverRect = NewRect("Cover", _boardRect, new Vector2(5f - w * 0.5f, -6f), new Vector2(w, h), new Vector2(0f, 0.5f));
        var leather = _coverRect.gameObject.AddComponent<RawImage>();
        leather.texture = _folderTex;
        leather.uvRect = LeatherUv;
        leather.color = _folderTex != null ? new Color(0.92f, 0.82f, 0.78f, 1f) : new Color(0.28f, 0.17f, 0.10f, 1f);
        leather.raycastTarget = true;

        var shade = NewImage("Shade", _coverRect, Vector2.zero, new Vector2(w, h), new Color(0f, 0f, 0f, 0f));
        shade.raycastTarget = false;
        _coverShade = shade;

        Color gold = new Color(Brass.r, Brass.g, Brass.b, 1f);
        var outer = NewRect("CoverFrame", _coverRect, Vector2.zero, new Vector2(w - 70f, h - 70f), null).gameObject.AddComponent<UIParallelogramGraphic>();
        outer.skewAmount = 0f;
        outer.outlineWidth = 4f;
        outer.outlineColor = new Color(gold.r, gold.g, gold.b, 0.8f);
        outer.fillColor = new Color(0f, 0f, 0f, 0f);
        outer.raycastTarget = false;
        var inner = NewRect("CoverFrameInner", _coverRect, Vector2.zero, new Vector2(w - 100f, h - 100f), null).gameObject.AddComponent<UIParallelogramGraphic>();
        inner.skewAmount = 0f;
        inner.outlineWidth = 1.5f;
        inner.outlineColor = new Color(gold.r, gold.g, gold.b, 0.35f);
        inner.fillColor = new Color(0f, 0f, 0f, 0f);
        inner.raycastTarget = false;

        // ตราบนปก: วงแหวนทอง + ข้อความตัวพิมพ์ดีด
        var seal = NewImage("Seal", _coverRect, new Vector2(0f, 90f), new Vector2(210f, 210f), gold);
        seal.sprite = DetectiveUIShapes.Ring(0.04f);
        seal.raycastTarget = false;
        var sealInner = NewImage("SealInner", _coverRect, new Vector2(0f, 90f), new Vector2(160f, 160f), new Color(gold.r, gold.g, gold.b, 0.55f));
        sealInner.sprite = DetectiveUIShapes.Ring(0.03f);
        sealInner.raycastTarget = false;
        NewText("SealText", _coverRect, "SY", thaiFontBold, 74, gold, TextAnchor.MiddleCenter,
            new Vector2(0f, 90f), new Vector2(210f, 120f), null, false);
        NewText("CoverTitle", _coverRect, "แฟ้มคดี", thaiFontBold, 66, DetectiveUITheme.AgedPaper, TextAnchor.MiddleCenter,
            new Vector2(0f, -78f), new Vector2(900f, 90f), null, false);
        NewText("CoverSub", _coverRect, "SCOTLAND YARD  ·  CASE FILE No. 1888/JR", _typewriterFont, 24,
            new Color(gold.r, gold.g, gold.b, 0.95f), TextAnchor.MiddleCenter,
            new Vector2(0f, -150f), new Vector2(1000f, 36f), null, false);

        var stamp = NewRect("ConfidentialStamp", _coverRect, new Vector2(340f, -280f), new Vector2(300f, 64f), null);
        stamp.localEulerAngles = new Vector3(0f, 0f, 9f);
        var stampFrame = stamp.gameObject.AddComponent<UIParallelogramGraphic>();
        stampFrame.skewAmount = 0f;
        stampFrame.outlineWidth = 4f;
        stampFrame.outlineColor = new Color(0.85f, 0.22f, 0.18f, 0.9f);
        stampFrame.fillColor = new Color(0f, 0f, 0f, 0f);
        stampFrame.raycastTarget = false;
        NewText("Label", stamp, "CONFIDENTIAL", _typewriterFont, 30, new Color(0.85f, 0.22f, 0.18f, 0.95f),
            TextAnchor.MiddleCenter, Vector2.zero, new Vector2(300f, 64f), null, false);
    }

    // ==========================================
    // Animations
    // ==========================================
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.35f;
        const float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static float EaseInOutCubic(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    private static float Seg(float t, float start, float end)
    {
        return Mathf.Clamp01((t - start) / Mathf.Max(0.0001f, end - start));
    }

    private Vector2 CardTarget(int i)
    {
        bool selected = i == _selectedQuestIndex;
        return _cards[i].basePos + new Vector2(selected ? 18f : 0f, 0f);
    }

    private void PlayJournalSound(bool stamp)
    {
        var cam = Camera.main;
        Vector3 at = cam != null ? cam.transform.position : Vector3.zero;
        if (stamp) CutsceneFX.PlayThud(at, 0.55f);
        else CutsceneFX.PlayPaperFlip(at, 0.7f);
    }

    private IEnumerator OpenAnimRoutine()
    {
        const float total = 1.55f;
        float t = 0f;
        bool stamped = false;
        bool flipped = false;

        _panelGroup.blocksRaycasts = true;
        _panelGroup.alpha = 0f;
        _coverRect.gameObject.SetActive(true);
        _coverRect.localScale = Vector3.one;
        _coverShade.color = new Color(0f, 0f, 0f, 0f);
        _pageGroup.alpha = 0f;
        for (int i = 0; i < _cardGroups.Count; i++) _cardGroups[i].alpha = 0f;
        for (int i = 0; i < _rowGroups.Count; i++) { _rowGroups[i].alpha = 0f; _bulletGroups[i].alpha = 0f; }
        _stampRect.localScale = Vector3.one * 2.4f;

        PlayJournalSound(false);

        while (t < total)
        {
            t += Time.unscaledDeltaTime;

            // 1) แฟ้มลอยขึ้นมาจากด้านล่าง เอียงเล็กน้อยแล้วตั้งตรง (0 - 0.45s)
            float rise = Seg(t, 0f, 0.45f);
            float riseK = EaseOutBack(rise);
            _panelGroup.alpha = Mathf.Clamp01(rise * 2.2f);
            _boardRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-170f, 0f, riseK));
            _boardRect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(4.5f, 0f, riseK));
            _boardRect.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, riseK);

            // 2) ปกพับเปิดไปทางซ้าย (0.32 - 0.88s)
            float open = Seg(t, 0.32f, 0.88f);
            float openK = EaseInOutCubic(open);
            if (open > 0.02f && !flipped) { flipped = true; PlayJournalSound(false); }
            _coverRect.localScale = new Vector3(Mathf.Lerp(1f, 0f, openK), 1f, 1f);
            _coverShade.color = new Color(0f, 0f, 0f, Mathf.Sin(open * Mathf.PI) * 0.55f);
            if (open >= 1f) _coverRect.gameObject.SetActive(false);

            // 3) การ์ดบทด้านซ้ายไหลเข้ามาทีละใบ (0.62s เป็นต้นไป)
            for (int i = 0; i < _cards.Count; i++)
            {
                float k = Seg(t, 0.62f + i * 0.09f, 0.62f + i * 0.09f + 0.32f);
                float e = EaseOutBack(k);
                _cardGroups[i].alpha = Mathf.Clamp01(k * 2f);
                _cards[i].rect.anchoredPosition = CardTarget(i) + new Vector2(Mathf.Lerp(-90f, 0f, e), 0f);
            }

            // 4) หน้ากระดาษขวาเลื่อนขึ้นและจางเข้ามา (0.7 - 1.05s)
            float page = Seg(t, 0.70f, 1.05f);
            _pageGroup.alpha = page;
            _pageRect.anchoredPosition = new Vector2(_pageBasePos.x, _pageBasePos.y + Mathf.Lerp(-30f, 0f, EaseOutBack(page)));

            // 5) ตราประทับกระแทกลงบนกระดาษ + กระดานสะเทือน (1.0 - 1.18s)
            float st = Seg(t, 1.0f, 1.18f);
            _stampRect.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, st * st);
            if (st >= 1f && !stamped)
            {
                stamped = true;
                PlayJournalSound(true);
            }
            if (stamped)
            {
                float sh = 1f - Seg(t, 1.18f, 1.36f);
                _boardRect.anchoredPosition = new Vector2(Mathf.Sin(t * 90f) * 5f * sh, Mathf.Cos(t * 80f) * 4f * sh);
            }

            // 6) รายการวัตถุพยานเรียงขึ้นทีละบรรทัด (1.0s เป็นต้นไป)
            for (int i = 0; i < _rowGroups.Count; i++)
            {
                float k = Seg(t, 1.0f + i * 0.07f, 1.0f + i * 0.07f + 0.25f);
                _rowGroups[i].alpha = k;
                _bulletGroups[i].alpha = k;
            }

            yield return null;
        }

        _panelGroup.alpha = 1f;
        _boardRect.anchoredPosition = Vector2.zero;
        _boardRect.localEulerAngles = Vector3.zero;
        _boardRect.localScale = Vector3.one;
        _coverRect.gameObject.SetActive(false);
        _pageGroup.alpha = 1f;
        _pageRect.anchoredPosition = _pageBasePos;
        _stampRect.localScale = Vector3.one;
        for (int i = 0; i < _cards.Count; i++) { _cardGroups[i].alpha = 1f; _cards[i].rect.anchoredPosition = CardTarget(i); }
        for (int i = 0; i < _rowGroups.Count; i++) { _rowGroups[i].alpha = 1f; _bulletGroups[i].alpha = 1f; }
        _openAnim = null;
    }

    /// <summary>เปลี่ยนบท: กระดาษพลิกเข้ามาใหม่เบา ๆ และตราประทับกระแทกซ้ำ</summary>
    private IEnumerator PageSwapRoutine()
    {
        const float duration = 0.34f;
        float t = 0f;
        _stampRect.localScale = Vector3.one * 1.9f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = EaseOutBack(Seg(k, 0f, 0.8f));
            _pageGroup.alpha = Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(k * 2.5f));
            _pageRect.anchoredPosition = new Vector2(_pageBasePos.x + Mathf.Lerp(34f, 0f, e), _pageBasePos.y);
            _stampRect.localScale = Vector3.one * Mathf.Lerp(1.9f, 1f, Mathf.Clamp01((k - 0.25f) / 0.35f));
            for (int i = 0; i < _rowGroups.Count; i++)
            {
                float rk = Seg(k, 0.12f + i * 0.06f, 0.12f + i * 0.06f + 0.3f);
                _rowGroups[i].alpha = rk;
                _bulletGroups[i].alpha = rk;
            }
            yield return null;
        }
        _pageGroup.alpha = 1f;
        _pageRect.anchoredPosition = _pageBasePos;
        _stampRect.localScale = Vector3.one;
        for (int i = 0; i < _rowGroups.Count; i++) { _rowGroups[i].alpha = 1f; _bulletGroups[i].alpha = 1f; }
        _swapAnim = null;
    }

    private IEnumerator CloseAnimRoutine()
    {
        const float duration = 0.2f;
        float t = 0f;
        _panelGroup.blocksRaycasts = false;
        float startAlpha = _panelGroup.alpha;
        PlayJournalSound(false);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = k * k;
            _panelGroup.alpha = Mathf.Lerp(startAlpha, 0f, e);
            _boardRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, -90f, e));
            _boardRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.94f, e);
            _boardRect.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(0f, -2.5f, e));
            yield return null;
        }
        if (_modalPanel != null) _modalPanel.SetActive(false);
        _boardRect.anchoredPosition = Vector2.zero;
        _boardRect.localScale = Vector3.one;
        _boardRect.localEulerAngles = Vector3.zero;
        _closeAnim = null;
    }

    private void RestoreActiveQuestHUD()
    {
        if (UndergroundStationQuestManager.Instance != null && UndergroundStationQuestManager.Instance.IsQuest4Active)
        {
            UndergroundStationQuestManager.Instance.ShowObjectiveHUD(true);
        }
        else if (RoyalLyceumTheaterQuestManager.Instance != null && RoyalLyceumTheaterQuestManager.Instance.IsQuest3Active)
        {
            RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(true);
        }
        else if (LondonAlleyQuestManager.Instance != null && LondonAlleyQuestManager.Instance.IsQuest2Active)
        {
            LondonAlleyQuestManager.Instance.ShowObjectiveHUD(true);
        }
        else if (StMaryChurchQuestManager.Instance != null && !StMaryChurchQuestManager.Instance.IsQuest1Finished)
        {
            StMaryChurchQuestManager.Instance.ShowObjectiveHUD(true);
        }
    }

    // ==========================================
    // UI construction
    // ==========================================
    private void BuildJournalUI()
    {
        _canvas = GetComponent<Canvas>();
        if (_canvas == null)
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 820;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        // 1. พื้นหลังเต็มจอ: ม่านดำโปร่งแสง + ขอบมืดซีเปีย (ให้ฉาก 3D ที่เบลอส่องผ่านได้)
        _modalPanel = new GameObject("Detective_Journal_Modal", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        _modalPanel.transform.SetParent(transform, false);
        var mpRect = (RectTransform)_modalPanel.transform;
        mpRect.anchorMin = Vector2.zero;
        mpRect.anchorMax = Vector2.one;
        mpRect.offsetMin = Vector2.zero;
        mpRect.offsetMax = Vector2.zero;
        _modalPanel.GetComponent<Image>().color = new Color(0.03f, 0.025f, 0.02f, 0.5f);
        _panelGroup = _modalPanel.GetComponent<CanvasGroup>();

        var vignette = NewImage("Vignette", _modalPanel.transform, Vector2.zero, Vector2.zero, Color.white);
        vignette.sprite = DetectiveUIShapes.Vignette;
        vignette.raycastTarget = false;
        Stretch(vignette.rectTransform);

        // 2. แฟ้มหนังเปิดออก (ภาพของผู้ใช้) ขนาด 1936 x 1081 ตัวแฟ้มจริงกว้างราว 1460 พิกเซล
        _boardRect = NewRect("Detective_Dossier", _modalPanel.transform, Vector2.zero, new Vector2(1640f, 920f), null);
        NewRaw("FolderShadow", _boardRect, new Vector2(12f, -22f), new Vector2(1936f, 1081f), _folderTex, new Color(0f, 0f, 0f, 0.55f), null);
        var folderArt = NewRaw("Folder", _boardRect, new Vector2(0f, -6f), new Vector2(1936f, 1081f), _folderTex, Color.white, null);
        if (_folderTex == null)
        {
            // ถ้าไม่มีภาพ ใช้กล่องหนังสีเข้มแทน
            folderArt.rectTransform.sizeDelta = new Vector2(1460f, 900f);
            folderArt.color = Leather;
        }

        BuildTopBar();
        BuildCardList();
        BuildRightPage();
        BuildCover();

        _modalPanel.SetActive(false);
    }

    private void BuildTopBar()
    {
        // หัวเรื่องลอยเหนือแฟ้มบนพื้นหลังเบลอ
        NewText("Title", _boardRect, "แฟ้มคดี", thaiFontBold, 46, DetectiveUITheme.AgedPaper, TextAnchor.MiddleLeft,
            new Vector2(-722f, 494f), new Vector2(500f, 64f), new Vector2(0f, 0.5f), false);
        NewText("Subtitle", _boardRect, "SCOTLAND YARD  ·  CASE ARCHIVE  ·  LONDON 1888", _typewriterFont, 18,
            new Color(Brass.r, Brass.g, Brass.b, 0.95f), TextAnchor.MiddleLeft,
            new Vector2(-470f, 490f), new Vector2(700f, 28f), new Vector2(0f, 0.5f), false);

        // ปุ่มปิด: วงกลมทองเหลือง + กากบาทที่วาดด้วยเส้น (ไม่พึ่งฟอนต์)
        var closeBase = NewImage("Btn_Close", _boardRect, new Vector2(736f, 494f), new Vector2(54f, 54f), new Color(0.16f, 0.11f, 0.07f, 1f));
        closeBase.sprite = DetectiveUIShapes.Circle;
        var closeBtn = closeBase.gameObject.AddComponent<Button>();
        var colors = closeBtn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.6f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.colorMultiplier = 1f;
        closeBtn.colors = colors;
        closeBtn.onClick.AddListener(CloseQuestMenu);
        var ring = NewImage("Ring", closeBase.transform, Vector2.zero, new Vector2(54f, 54f), Brass);
        ring.sprite = DetectiveUIShapes.Ring(0.07f);
        ring.raycastTarget = false;
        var bar1 = NewImage("X1", closeBase.transform, Vector2.zero, new Vector2(24f, 3.5f), Brass);
        bar1.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        bar1.raycastTarget = false;
        var bar2 = NewImage("X2", closeBase.transform, Vector2.zero, new Vector2(24f, 3.5f), Brass);
        bar2.rectTransform.localEulerAngles = new Vector3(0f, 0f, -45f);
        bar2.raycastTarget = false;
        NewText("EscHint", _boardRect, "ESC", _typewriterFont, 16, new Color(Brass.r, Brass.g, Brass.b, 0.8f), TextAnchor.MiddleRight,
            new Vector2(686f, 494f), new Vector2(60f, 24f), new Vector2(1f, 0.5f), false);
    }

    private void BuildCardList()
    {
        _cards.Clear();
        for (int i = 0; i < ChapterCount; i++)
        {
            int index = i;
            float y = 252f - i * 168f;
            var rect = NewRect("Card_" + (i + 1), _boardRect, new Vector2(-390f, y), new Vector2(560f, 150f), null);
            var view = new CardView { rect = rect, basePos = rect.anchoredPosition };

            view.baseImg = rect.gameObject.AddComponent<Image>();
            view.baseImg.color = new Color(1f, 1f, 1f, 0.001f); // ภาพรับคลิกโปร่งใส
            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = view.baseImg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.colorMultiplier = 1f;
            btn.colors = colors;
            btn.onClick.AddListener(() => SelectQuest(index));

            // ใบแทรกกระดาษเก่า (ตัดจากกระดาษของผู้ใช้) + เงาตามรูปทรง
            NewRaw("PaperShadow", rect, new Vector2(6f, -8f), new Vector2(560f, 150f), _paperTex, new Color(0f, 0f, 0f, 0.45f), SlipUv);
            view.accentBar = NewRaw("Back", rect, Vector2.zero, new Vector2(560f, 150f), _paperTex, Color.white, SlipUv);

            var frameRect = NewRect("Frame", rect, Vector2.zero, new Vector2(548f, 138f), null);
            view.frame = frameRect.gameObject.AddComponent<UIParallelogramGraphic>();
            view.frame.skewAmount = 0f;
            view.frame.outlineWidth = 2f;
            view.frame.outlineColor = new Color(0.17f, 0.11f, 0.07f, 0.4f);
            view.frame.fillColor = new Color(0f, 0f, 0f, 0f);
            view.frame.raycastTarget = false;

            // กล่องเลขโรมันด้านซ้าย (ป้ายหนังเข้ม)
            view.numeralBox = NewImage("NumeralBox", rect, new Vector2(-212f, 0f), new Vector2(100f, 112f), new Color(0.08f, 0.055f, 0.035f, 1f));
            view.numeralBox.raycastTarget = false;
            var boxFrameRect = NewRect("NumeralFrame", view.numeralBox.transform, Vector2.zero, new Vector2(100f, 112f), null);
            var boxFrame = boxFrameRect.gameObject.AddComponent<UIParallelogramGraphic>();
            boxFrame.skewAmount = 0f;
            boxFrame.outlineWidth = 2f;
            boxFrame.outlineColor = new Color(Brass.r, Brass.g, Brass.b, 0.85f);
            boxFrame.fillColor = new Color(0f, 0f, 0f, 0f);
            boxFrame.raycastTarget = false;
            view.numeral = NewText("Numeral", view.numeralBox.transform, Romans[i], thaiFontBold, 50, Brass, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(100f, 112f), null, false);

            view.category = NewText("Category", rect, "", thaiFont, 18, DetectiveUITheme.InkMuted, TextAnchor.MiddleLeft,
                new Vector2(-148f, 44f), new Vector2(400f, 28f), new Vector2(0f, 0.5f), false);
            view.title = NewText("Title", rect, "", thaiFontBold, 25, DetectiveUITheme.InkDark, TextAnchor.MiddleLeft,
                new Vector2(-148f, 3f), new Vector2(395f, 66f), new Vector2(0f, 0.5f), true);
            view.title.resizeTextForBestFit = true;
            view.title.resizeTextMinSize = 17;
            view.title.resizeTextMaxSize = 25;
            view.title.verticalOverflow = VerticalWrapMode.Truncate;
            view.status = NewText("Status", rect, "", thaiFont, 19, DetectiveUITheme.InkDark, TextAnchor.MiddleLeft,
                new Vector2(-148f, -42f), new Vector2(400f, 28f), new Vector2(0f, 0.5f), false);
            view.lockedTag = NewText("LockedTag", rect, "SEALED", _typewriterFont, 17, DetectiveUITheme.InkMuted, TextAnchor.MiddleRight,
                new Vector2(262f, 44f), new Vector2(120f, 26f), new Vector2(1f, 0.5f), false);

            _cards.Add(view);
            _cardGroups.Add(rect.gameObject.AddComponent<CanvasGroup>());
        }
    }

    private void BuildRightPage()
    {
        // กระดาษบันทึกคดี 950 x 740 วางทับปึกเอกสารด้านขวาของแฟ้ม (ใช้ภาพกระดาษของผู้ใช้ ขอบฉีกขาดจริง)
        var paper = NewRect("RightPage", _boardRect, _pageBasePos, new Vector2(950f, 740f), null);
        _pageRect = paper;
        _pageGroup = paper.gameObject.AddComponent<CanvasGroup>();
        NewRaw("Shadow", paper, new Vector2(10f, -14f), new Vector2(950f, 740f), _paperTex, new Color(0f, 0f, 0f, 0.5f), null);
        NewRaw("Sheet", paper, Vector2.zero, new Vector2(950f, 740f), _paperTex, new Color(1f, 0.97f, 0.92f, 1f), null);
        // ถ้าไม่มีภาพกระดาษ ให้ใช้สีกระดาษเรียบแทน
        if (_paperTex == null)
        {
            var flat = NewImage("SheetFlat", paper, Vector2.zero, new Vector2(950f, 740f), DetectiveUITheme.AgedPaper);
            flat.raycastTarget = false;
        }

        Color ink = DetectiveUITheme.InkDark;
        Color muted = DetectiveUITheme.InkMuted;
        const float left = -405f;
        const float width = 810f;

        _chapterLabel = NewText("ChapterLabel", paper, "", _typewriterFont, 20, InkRed, TextAnchor.MiddleLeft,
            new Vector2(left, 318f), new Vector2(500f, 28f), new Vector2(0f, 0.5f), false);
        _titleText = NewText("Title", paper, "", thaiFontBold, 44, ink, TextAnchor.MiddleLeft,
            new Vector2(left, 270f), new Vector2(560f, 64f), new Vector2(0f, 0.5f), true);
        _titleText.resizeTextForBestFit = true;
        _titleText.resizeTextMinSize = 26;
        _titleText.resizeTextMaxSize = 44;
        _titleText.verticalOverflow = VerticalWrapMode.Truncate;
        _locationText = NewText("Location", paper, "", thaiFont, 22, muted, TextAnchor.MiddleLeft,
            new Vector2(left, 228f), new Vector2(width, 32f), new Vector2(0f, 0.5f), false);
        _locationText.fontStyle = FontStyle.Italic;

        // ตราประทับสถานะบทที่มุมขวาบน
        var stampRect = NewRect("Stamp", paper, new Vector2(290f, 280f), new Vector2(260f, 70f), null);
        stampRect.localEulerAngles = new Vector3(0f, 0f, -7f);
        _stampRect = stampRect;
        _stampFrame = stampRect.gameObject.AddComponent<UIParallelogramGraphic>();
        _stampFrame.skewAmount = 0f;
        _stampFrame.outlineWidth = 4f;
        _stampFrame.fillColor = new Color(0f, 0f, 0f, 0f);
        _stampFrame.raycastTarget = false;
        _stampText = NewText("Label", stampRect, "", thaiFontBold, 30, InkRed, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(260f, 70f), null, false);

        var rule = NewImage("Rule", paper, new Vector2(0f, 204f), new Vector2(width, 3f), new Color(ink.r, ink.g, ink.b, 0.5f));
        rule.raycastTarget = false;

        // แผ่นเป้าหมายปัจจุบัน (หนังเข้ม แถบทองเหลืองด้านซ้าย)
        var plate = NewImage("ObjectivePlate", paper, new Vector2(0f, 142f), new Vector2(width, 100f), new Color(0.12f, 0.085f, 0.055f, 1f));
        plate.raycastTarget = false;
        var bar = NewImage("Bar", plate.transform, new Vector2(-width * 0.5f + 5f, 0f), new Vector2(10f, 100f), Brass);
        bar.raycastTarget = false;
        _objectiveLabel = NewText("ObjectiveLabel", plate.transform, "เป้าหมายปัจจุบัน", thaiFontBold, 18, Brass, TextAnchor.MiddleLeft,
            new Vector2(-width * 0.5f + 32f, 30f), new Vector2(600f, 26f), new Vector2(0f, 0.5f), false);
        _objectiveText = NewText("ObjectiveText", plate.transform, "", thaiFont, 24, DetectiveUITheme.AgedPaper, TextAnchor.UpperLeft,
            new Vector2(-width * 0.5f + 32f, 10f), new Vector2(width - 60f, 60f), new Vector2(0f, 1f), true);

        _summaryHeader = NewText("SummaryHeader", paper, "สรุปคดี", thaiFontBold, 26, ink, TextAnchor.MiddleLeft,
            new Vector2(left, 64f), new Vector2(500f, 36f), new Vector2(0f, 0.5f), false);
        _summaryText = NewText("Summary", paper, "", thaiFont, 21, ink, TextAnchor.UpperLeft,
            new Vector2(left, 40f), new Vector2(width, 170f), new Vector2(0f, 1f), true);
        _summaryText.lineSpacing = 1.08f;

        _evidenceHeader = NewText("EvidenceHeader", paper, "บัญชีวัตถุพยาน", thaiFontBold, 26, ink, TextAnchor.MiddleLeft,
            new Vector2(left, -146f), new Vector2(500f, 36f), new Vector2(0f, 0.5f), false);
        _evidenceProgressText = NewText("EvidenceProgress", paper, "", thaiFont, 21, muted, TextAnchor.MiddleRight,
            new Vector2(405f, -146f), new Vector2(380f, 32f), new Vector2(1f, 0.5f), false);

        for (int i = 0; i < MaxEvidenceRows; i++)
        {
            float rowY = -172f - i * 30f;
            var bullet = NewImage("Bullet_" + i, paper, new Vector2(left + 8f, rowY - 14f), new Vector2(10f, 10f), ink);
            bullet.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            bullet.raycastTarget = false;
            var row = NewText("Evidence_" + i, paper, "", thaiFont, 20, muted, TextAnchor.MiddleLeft,
                new Vector2(left + 30f, rowY), new Vector2(width - 30f, 28f), new Vector2(0f, 1f), false);
            _evidenceBullets.Add(bullet);
            _evidenceRows.Add(row);
            _bulletGroups.Add(bullet.gameObject.AddComponent<CanvasGroup>());
            _rowGroups.Add(row.gameObject.AddComponent<CanvasGroup>());
        }

        // ปุ่มนำทาง (ป้ายทองเหลือง) แสดงเมื่อบทนั้นกำลังดำเนินอยู่
        var navRect = NewRect("Btn_Navigate", paper, new Vector2(285f, -322f), new Vector2(330f, 54f), null);
        _navGroup = navRect.gameObject.AddComponent<CanvasGroup>();
        var navImg = navRect.gameObject.AddComponent<Image>();
        navImg.color = Color.white;
        _navButton = navRect.gameObject.AddComponent<Button>();
        var colors = _navButton.colors;
        colors.normalColor = ink;
        colors.highlightedColor = InkRed;
        colors.selectedColor = ink;
        colors.pressedColor = new Color(0.45f, 0.06f, 0.05f, 1f);
        colors.colorMultiplier = 1f;
        _navButton.colors = colors;
        _navButton.onClick.AddListener(CloseQuestMenu);
        NewText("Label", navRect, "นำทางสู่จุดเกิดเหตุ  ›", thaiFontBold, 24, DetectiveUITheme.AgedPaper, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(330f, 54f), null, false);
    }

    // ==========================================
    // Data
    // ==========================================
    private void RefreshQuestData()
    {
        _questDataList.Clear();

        bool q1Finished = StMaryChurchQuestManager.Instance != null && StMaryChurchQuestManager.Instance.IsQuest1Finished;
        bool q2Active = LondonAlleyQuestManager.Instance != null && LondonAlleyQuestManager.Instance.IsQuest2Active;
        bool q3Active = RoyalLyceumTheaterQuestManager.Instance != null && RoyalLyceumTheaterQuestManager.Instance.IsQuest3Active;
        bool q4Active = UndergroundStationQuestManager.Instance != null && UndergroundStationQuestManager.Instance.IsQuest4Active;

        int dist = WaypointNavigationSystem.Instance != null ? WaypointNavigationSystem.Instance.CurrentDistanceMeters : 0;

        // บทที่ 1: โบสถ์เซนต์แมรี่
        var c1 = new QuestEntryData
        {
            chapterIndex = 1,
            title = "จุดเริ่มต้นแห่งปริศนา",
            category = "คดีหลักที่ 1  ·  โบสถ์เซนต์แมรี่",
            location = "โบสถ์เซนต์แมรี่ (St. Mary's Church)  ·  ไวท์ชาเปล ลอนดอน 1888",
            isCompleted = q1Finished,
            isActive = !q1Finished,
            isLocked = false,
            distanceText = $"{dist}m",
            scannedCount = StMaryChurchQuestManager.Instance != null ? StMaryChurchQuestManager.Instance.ScannedCount : 0,
            currentObjective = q1Finished
                ? "คลี่คลายสำนวนคดีในโบสถ์เซนต์แมรี่สำเร็จ"
                : "สแกนวัตถุพยาน 5 ชิ้นในที่เกิดเหตุ แล้วอนุมานว่าสิ่งใดเชื่อมโยงถึงตัวฆาตกร",
            fullDescription =
                "เที่ยงคืนที่โบสถ์เซนต์แมรี่ พบร่างของแมรี่ แอน ช่างตัดเย็บผู้ยากจน หญิงที่เคยจำใจทิ้งลูกชายไว้เบื้องหลัง " +
                "เธอไม่มีศัตรู ไม่มีทรัพย์สินให้ปล้น และไม่มีร่องรอยการต่อสู้\n" +
                "ใช้ CiRA CORE สแกนวัตถุพยานที่ถูกทิ้งไว้ในโบสถ์ แล้วอนุมานทีละขั้นว่าใครอยู่เบื้องหลัง",
            conclusion = "แหวนเงินสองวงตราตระกูลเดียวกันคือของแม่และลูก ฆาตกรเป็นคนจากอดีตของเธอ และจงใจทิ้งแหวนวงเล็กไว้ให้เราเห็น"
        };
        c1.evidence.Add(new EvidenceLine("รอยเท้าบู๊ตชาย", "ก้าวสม่ำเสมอ ไม่รีบร้อน"));
        c1.evidence.Add(new EvidenceLine("ผ้าเช็ดหน้าปักอักษร M.A.", "คราบน้ำตาของผู้ตาย"));
        c1.evidence.Add(new EvidenceLine("กระดาษบทสวด", "บทขอการอภัยสำหรับแม่ผู้ละทิ้งบุตร"));
        c1.evidence.Add(new EvidenceLine("แหวนเงินวงใหญ่", "สลักตราตระกูล ถูกขัดเงาเสมอ"));
        c1.evidence.Add(new EvidenceLine("แหวนเงินวงเล็ก", "ตราเดียวกัน ขนาดเท่านิ้วเด็ก"));
        _questDataList.Add(c1);

        // บทที่ 2: ตรอกไวท์ชาเปล
        bool q2Done = q3Active || q4Active;
        var c2 = new QuestEntryData
        {
            chapterIndex = 2,
            title = "สาส์นลวงจากเงามืด",
            category = "คดีหลักที่ 2  ·  ตรอกไวท์ชาเปล",
            location = "ตรอกมืดไวท์ชาเปล (Whitechapel Alley)  ·  ลอนดอน 1888",
            isCompleted = q2Done,
            isActive = q2Active || (q1Finished && !q2Done),
            isLocked = !q1Finished,
            distanceText = $"{dist}m",
            scannedCount = LondonAlleyQuestManager.Instance != null ? LondonAlleyQuestManager.Instance.ScannedCount : 0,
            currentObjective = q2Done
                ? "ถอดรหัสลับสำเร็จ รู้แล้วว่าเป้าหมายต่อไปคือโรงละคร"
                : (q1Finished ? "สแกนหลักฐาน 5 ชิ้นรอบศพ แล้วถอดรหัสบนหนังสือพิมพ์ The Daily Telegraph" : "คลี่คลายคดีบทที่ 1 ให้สำเร็จก่อน"),
            fullDescription =
                "ศพหญิงรายที่สองถูกทิ้งไว้กลางตรอกมืดภายใต้หมอกหนา ข้างศพมีหนังสือพิมพ์ที่มีรอยขีดเขียนแปลก ๆ " +
                "ซึ่งไม่ใช่ฝีมือของแจ็ค แต่เป็นสาส์นท้าทายจากบุคคลที่สาม\n" +
                "ใช้แว่นขยายส่องหาตัวอักษรที่ซ่อนอยู่ แล้วถอดรหัสซีซาร์ให้ได้คำตอบ",
            conclusion = "เราไม่ได้สู้กับแจ็คคนเดียว มอริอาร์ตี้ส่งรหัส WKHDWHU ซึ่งถอดได้ว่า THEATER เรียกให้เราไปโรงละคร"
        };
        c2.evidence.Add(new EvidenceLine("กระดุมเสื้อขาดกระเด็น", "บ่งบอกการยื้อยุด"));
        c2.evidence.Add(new EvidenceLine("รอยเท้าปริศนาเปื้อนโคลน", "ขนาดรองเท้าผู้ต้องสงสัย"));
        c2.evidence.Add(new EvidenceLine("มีดพกเปื้อนเลือด", "ใบมีดผ่าตัดเฉพาะทาง"));
        c2.evidence.Add(new EvidenceLine("ตั๋วรถไฟลอนดอน", "เส้นทางสัญจร"));
        c2.evidence.Add(new EvidenceLine("หนังสือพิมพ์ The Daily Telegraph", "รหัสลับ WKHDWHU"));
        _questDataList.Add(c2);

        // บทที่ 3: โรงละคร
        bool q3Done = q4Active;
        var c3 = new QuestEntryData
        {
            chapterIndex = 3,
            title = "เฉียดฉิว",
            category = "คดีหลักที่ 3  ·  โรงละครรอยัลไลเซียม",
            location = "โรงละครรอยัลไลเซียม (The Royal Lyceum Theatre)  ·  ลอนดอน",
            isCompleted = q3Done,
            isActive = q3Active && !q3Done,
            isLocked = !q3Active && !q3Done,
            distanceText = $"{dist}m",
            scannedCount = RoyalLyceumTheaterQuestManager.Instance != null ? RoyalLyceumTheaterQuestManager.Instance.ScannedCount : 0,
            currentObjective = q3Done
                ? "รู้ปลายทางการหลบหนีของแจ็คแล้ว"
                : (q3Active ? "สแกนเบาะแสที่แจ็คทิ้งไว้ให้ครบ 5 ชิ้นก่อนร่องรอยเลือนหาย แล้วเทียบตั๋วกับแผนที่รถไฟ" : "ถอดรหัสหนังสือพิมพ์ในบทที่ 2 ให้สำเร็จก่อน"),
            fullDescription =
                "เรามาถึงโรงละครก่อนที่แจ็คจะฆ่าเหยื่อสำเร็จ แต่เขาไหวตัวทันและหนีรอดไปได้ ทิ้งร่องรอยการต่อสู้ไว้ทั่วเวที\n" +
                "รีบตรวจเบาะแสก่อนที่ร่องรอยจะเลือนหาย แล้วนำตั๋วรถไฟที่ฉีกขาดไปเทียบกับตารางเดินรถและแผนที่รถไฟลอนดอน",
            conclusion = "ทางหนีของแจ็คถูกวางไว้ล่วงหน้า ตั๋วฉีกครึ่งชี้ไปคิงส์ครอส ขบวนด่วน 23:45 และมีผู้วางแผนที่มายืนดูในโรงละครคืนนี้"
        };
        c3.evidence.Add(new EvidenceLine("หน้ากากละครเวที", "แตกครึ่งจากการกระแทกระหว่างหนี"));
        c3.evidence.Add(new EvidenceLine("ไม้เท้าทองเหลืองหัก", "หัวด้ามสลักอักษร M"));
        c3.evidence.Add(new EvidenceLine("เศษผ้ากำมะหยี่ชุดนักแสดง", "ติดอยู่กับขอบเวที"));
        c3.evidence.Add(new EvidenceLine("จดหมายหลังเวที", "กำหนดทางหนีไว้ล่วงหน้า"));
        c3.evidence.Add(new EvidenceLine("ตั๋วรถไฟที่ฉีกขาด", "ปลายทางบนตั๋วเหลืออยู่ครึ่งเดียว"));
        _questDataList.Add(c3);

        // บทที่ 4: สถานีรถไฟใต้ดิน
        bool q4Done = UndergroundStationQuestManager.Instance != null && UndergroundStationQuestManager.Instance.IsQuest4Finished;
        var c4 = new QuestEntryData
        {
            chapterIndex = 4,
            title = "การไล่ล่าใต้ดิน",
            category = "คดีไคลแม็กซ์  ·  สถานีรถไฟใต้ดิน",
            location = "สถานีรถไฟใต้ดินลอนดอน (Metropolitan Railway)  ·  ลอนดอน 1888",
            isCompleted = q4Done,
            isActive = q4Active && !q4Done,
            isLocked = !q4Active,
            distanceText = $"{dist}m",
            scannedCount = UndergroundStationQuestManager.Instance != null ? UndergroundStationQuestManager.Instance.ScannedCount : 0,
            currentObjective = q4Done ? "จับกุมแจ็คได้แล้ว แต่ทุกอย่างเป็นไปตามแผนของมอริอาร์ตี้" : q4Active ? "สแกนร่องรอยการหลบหนีและเชื่อมโยงหลักฐานให้ครบ" : "คลี่คลายปริศนาแผนที่รถไฟในบทที่ 3 ให้สำเร็จก่อน",
            fullDescription =
                "ควันไอน้ำพวยพุ่ง ไฟกะพริบติด ๆ ดับ ๆ ในสถานีรถไฟใต้ดิน การไล่ล่าภายใต้สายตาของมอริอาร์ตี้\n" +
                "สแกนรอยเลือด ปลอกกระสุน นาฬิกา ตั๋วรถไฟ และโน้ตลวง แล้วเชื่อมโยงทุกอย่างเข้าด้วยกัน",
            conclusion = "แจ็คหนีไปชานชาลา 2 ขบวน 11:45 PM ตู้หมายเลข 4 จดหมายตรา M คือคำลวง และทุกร่องรอยถูกมอริอาร์ตี้จัดวางไว้ล่วงหน้า"
        };
        c4.evidence.Add(new EvidenceLine("รอยเลือดสดบนทางแยก", "ทิศทางหลบหนี"));
        c4.evidence.Add(new EvidenceLine("ปลอกกระสุน", "ตกอยู่ใกล้ขบวนรถไฟ"));
        c4.evidence.Add(new EvidenceLine("นาฬิกาพกหยุดเดิน", "ชี้ที่ 11:45 PM"));
        c4.evidence.Add(new EvidenceLine("ตั๋วรถไฟจากโรงละคร", "ระบุหมายเลขตู้ขบวน"));
        c4.evidence.Add(new EvidenceLine("โน้ตจากมอริอาร์ตี้", "ข้อมูลลวง ต้องตัดทิ้ง"));
        _questDataList.Add(c4);

        // เลือกบทที่กำลังดำเนินอยู่เป็นค่าเริ่มต้น (บทสุดท้ายที่ active)
        int lastActive = -1;
        for (int i = 0; i < _questDataList.Count; i++)
        {
            if (_questDataList[i].isActive) lastActive = i;
        }
        if (lastActive >= 0) _selectedQuestIndex = lastActive;
        _selectedQuestIndex = Mathf.Clamp(_selectedQuestIndex, 0, _questDataList.Count - 1);
    }

    private void SelectQuest(int index)
    {
        if (index < 0 || index >= _questDataList.Count) return;
        bool changed = index != _selectedQuestIndex;
        _selectedQuestIndex = index;
        QuestEntryData data = _questDataList[index];

        // ----- การ์ดด้านซ้าย -----
        for (int i = 0; i < _cards.Count && i < _questDataList.Count; i++)
        {
            var view = _cards[i];
            var q = _questDataList[i];
            bool selected = i == _selectedQuestIndex;

            view.accentBar.color = q.isLocked ? new Color(0.62f, 0.57f, 0.50f, 1f)
                : (selected ? new Color(1f, 0.95f, 0.80f, 1f) : new Color(0.90f, 0.85f, 0.75f, 1f));
            view.rect.anchoredPosition = view.basePos + new Vector2(selected ? 18f : 0f, 0f);
            view.frame.outlineWidth = selected ? 4f : 2f;
            view.frame.outlineColor = selected ? new Color(0.45f, 0.08f, 0.06f, 0.95f) : new Color(0.17f, 0.11f, 0.07f, q.isLocked ? 0.25f : 0.4f);
            view.frame.SetVerticesDirty();

            view.numeral.color = q.isLocked ? LockedGray : Brass;
            view.category.text = q.category;
            view.category.color = DetectiveUITheme.InkMuted;
            view.title.text = q.title;
            view.title.color = q.isLocked ? DetectiveUITheme.InkMuted : DetectiveUITheme.InkDark;
            view.lockedTag.gameObject.SetActive(q.isLocked);

            if (q.isCompleted)
            {
                view.status.text = "คลี่คลายแล้ว";
                view.status.color = InkGreen;
            }
            else if (q.isLocked)
            {
                view.status.text = "ยังไม่ปลดล็อก";
                view.status.color = DetectiveUITheme.InkMuted;
            }
            else
            {
                view.status.text = $"กำลังสืบสวน  ·  ระยะห่าง {q.distanceText}";
                view.status.color = InkRed;
            }
        }

        // ----- หน้ากระดาษด้านขวา -----
        _chapterLabel.text = $"CHAPTER {Romans[Mathf.Clamp(data.chapterIndex - 1, 0, 3)]}";
        _titleText.text = data.title;
        _locationText.text = data.location;

        string stamp;
        Color stampColor;
        if (data.isCompleted) { stamp = "คลี่คลายแล้ว"; stampColor = InkGreen; }
        else if (data.isLocked) { stamp = "ปิดผนึก"; stampColor = LockedGray; }
        else { stamp = "กำลังสืบสวน"; stampColor = InkRed; }
        _stampText.text = stamp;
        _stampText.color = stampColor;
        _stampFrame.outlineColor = stampColor;
        _stampFrame.SetVerticesDirty();

        if (data.isLocked)
        {
            _objectiveText.text = data.currentObjective;
            _summaryText.text = "แฟ้มคดีนี้ถูกปิดผนึก\nคลี่คลายคดีก่อนหน้าให้สำเร็จเพื่อเปิดอ่านรายละเอียด";
            _summaryText.color = LockedGray;
        }
        else
        {
            _objectiveText.text = data.currentObjective;
            string summary = data.fullDescription;
            if (data.isCompleted && !string.IsNullOrEmpty(data.conclusion))
            {
                summary += "\n<b>ผลการอนุมาน:</b>  " + data.conclusion;
            }
            _summaryText.text = summary;
            _summaryText.color = DetectiveUITheme.InkDark;
        }

        _evidenceHeader.gameObject.SetActive(!data.isLocked);
        _evidenceProgressText.gameObject.SetActive(!data.isLocked);
        if (!data.isLocked)
        {
            if (data.isCompleted) _evidenceProgressText.text = $"รวบรวมครบแล้ว  {data.totalEvidence}/{data.totalEvidence}";
            else if (data.scannedCount >= 0) _evidenceProgressText.text = $"รวบรวมแล้ว  {data.scannedCount}/{data.totalEvidence}";
            else _evidenceProgressText.text = "";
        }

        for (int i = 0; i < MaxEvidenceRows; i++)
        {
            bool has = !data.isLocked && i < data.evidence.Count;
            _evidenceRows[i].gameObject.SetActive(has);
            _evidenceBullets[i].gameObject.SetActive(has);
            if (!has) continue;

            var line = data.evidence[i];
            _evidenceRows[i].text = $"<b><color=#2B2118>{line.title}</color></b>   {line.note}";
            _evidenceBullets[i].color = data.isCompleted ? InkGreen : new Color(0.14f, 0.10f, 0.07f, 0.45f);
        }

        bool showNav = data.isActive && !data.isLocked && !data.isCompleted;
        _navGroup.alpha = showNav ? 1f : 0f;
        _navGroup.interactable = showNav;
        _navGroup.blocksRaycasts = showNav;

        if (changed && _isOpen && _openAnim == null && _modalPanel != null && _modalPanel.activeInHierarchy)
        {
            if (_swapAnim != null) StopCoroutine(_swapAnim);
            _swapAnim = StartCoroutine(PageSwapRoutine());
            PlayJournalSound(false);
        }
    }

    // ==========================================
    // UI factory helpers (พิกัดอ้างอิงกึ่งกลางของ parent)
    // ==========================================
    private static RectTransform NewRect(string name, Transform parent, Vector2 pos, Vector2 size, Vector2? pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = new Vector2(0.5f, 0.5f);
        r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        return r;
    }

    private static RawImage NewRaw(string name, Transform parent, Vector2 pos, Vector2 size, Texture tex, Color color, Rect? uv)
    {
        var r = NewRect(name, parent, pos, size, null);
        var raw = r.gameObject.AddComponent<RawImage>();
        raw.texture = tex;
        raw.color = color;
        if (uv.HasValue) raw.uvRect = uv.Value;
        raw.raycastTarget = false;
        return raw;
    }

    private static Image NewImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var r = NewRect(name, parent, pos, size, null);
        var img = r.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Text NewText(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor align,
        Vector2 pos, Vector2 box, Vector2? pivot, bool wrap)
    {
        var r = NewRect(name, parent, pos, box, pivot);
        var t = r.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
