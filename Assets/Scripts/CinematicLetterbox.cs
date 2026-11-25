using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// ระบบ Cinematic Letterbox (ขอบดำบน-ล่าง สไตล์ภาพยนตร์)
/// รองรับฟอนต์ภาษาไทย (Google Sans) และแสดงบทสนทนา/ซับไตเติล
/// </summary>
public class CinematicLetterbox : MonoBehaviour
{
    private static CinematicLetterbox _instance;
    public static CinematicLetterbox Instance => _instance;

    [Header("=== การตั้งค่าขอบดำภาพยนตร์ (Cinematic Bars) ===")]
    [Tooltip("ความสูงของแถบดำบน-ล่าง (% ของความสูงหน้าจอ 0.0 - 0.25)")]
    [Range(0.05f, 0.25f)]
    public float barHeightPercent = 0.12f;

    [Tooltip("ความเร็วในการเลื่อนแถบดำเข้า-ออก")]
    public float transitionSpeed = 3.5f;

    [Header("=== ฟอนต์ภาษาไทย ===")]
    public Font thaiFont;

    // UI Elements
    private RectTransform _topBar;
    private RectTransform _bottomBar;
    private CanvasGroup _subtitleGroup;
    private Text _speakerText;
    private Text _dialogueText;
    private Text _continuePromptText;

    private bool _isLetterboxActive = false;
    public bool IsLetterboxActive => _isLetterboxActive;
    private float _currentBarHeight = 0f;
    private float _targetBarHeight = 0f;

    private Action _onDialogueFinished;
    private Queue<DialogueEntry> _dialogueQueue = new Queue<DialogueEntry>();
    private bool _isWaitingForInput = false;

    [System.Serializable]
    public struct DialogueEntry
    {
        public string speaker;
        public string text;
        public Action onComplete;
        /// <summary>เวลาขั้นต่ำที่บรรทัดนี้ต้องค้างไว้ (วินาที) เช่น ช็อตต่อสู้ที่ยาวกว่าบทพูด</summary>
        public float minHold;

        public DialogueEntry(string speaker, string text, Action onComplete = null, float minHold = 0f)
        {
            this.speaker = speaker;
            this.text = text;
            this.onComplete = onComplete;
            this.minHold = minHold;
        }
    }

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

        BuildLetterboxUI();
    }

    [Header("🎬 ระบบเล่นคัตซีนภาพยนตร์อัตโนมัติ (Auto-Play Cinematic)")]
    [Tooltip("เล่นคัตซีนอัตโนมัติเหมือนเกม AAA โดยไม่ต้องกดคลิกเอง")]
    public bool enableAutoPlay = true;
    [Tooltip("ระยะเวลาแสดงแต่ละช็อต (วินาที) ใช้เป็นค่าต่ำสุด เวลาจริงจะยืดตามความยาวประโยค")]
    public float autoPlayDurationPerShot = 5.0f;
    private float _autoPlayTimer = 0f;

    [Header("⌨ ตัวอักษรพิมพ์ทีละตัว (Typewriter)")]
    [Tooltip("ความเร็วพิมพ์ตัวอักษร (ตัวต่อวินาที)")]
    public float typewriterCharsPerSecond = 40f;
    [Tooltip("ความเร็วอ่านโดยประมาณ (ตัวอักษรต่อวินาที) ใช้คำนวณเวลาค้างบรรทัดก่อนเล่นต่ออัตโนมัติ")]
    public float readingCharsPerSecond = 16f;
    private Coroutine _typeRoutine;
    private bool _isTyping = false;
    private string _fullLineText = "";
    private float _currentLineHold = 5f;

    private void Update()
    {
        // Animate Letterbox Bars
        if (Mathf.Abs(_currentBarHeight - _targetBarHeight) > 0.001f)
        {
            _currentBarHeight = Mathf.Lerp(_currentBarHeight, _targetBarHeight, Time.deltaTime * transitionSpeed);
            UpdateBarPositions();
        }

        // Advance Dialogue on Click or Space or Auto Timer
        if (_isWaitingForInput)
        {
            bool advancePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) advancePressed = true;
            if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) advancePressed = true;
#else
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) advancePressed = true;
            if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)) advancePressed = true;
#endif

            // กดระหว่างกำลังพิมพ์ = แสดงทั้งประโยคทันที (ยังไม่ข้ามไปบรรทัดถัดไป)
            if (_isTyping)
            {
                if (advancePressed) FinishTyping();
                return;
            }

            if (enableAutoPlay)
            {
                _autoPlayTimer += Time.deltaTime;
                if (_autoPlayTimer >= _currentLineHold)
                {
                    advancePressed = true;
                }
            }

            if (advancePressed)
            {
                _autoPlayTimer = 0f;
                AdvanceDialogue();
            }
        }
    }

    private void BuildLetterboxUI()
    {
        // Canvas setup
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // Above regular UI
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

        // 1. Top Bar
        GameObject topBarGo = new GameObject("TopBlackBar", typeof(RectTransform), typeof(Image));
        topBarGo.transform.SetParent(transform, false);
        _topBar = topBarGo.GetComponent<RectTransform>();
        _topBar.anchorMin = new Vector2(0f, 1f);
        _topBar.anchorMax = new Vector2(1f, 1f);
        _topBar.pivot = new Vector2(0.5f, 1f);
        _topBar.sizeDelta = new Vector2(0f, 0f);
        Image topImg = topBarGo.GetComponent<Image>();
        topImg.color = Color.black;
        topImg.raycastTarget = false;

        // 2. Bottom Bar
        GameObject bottomBarGo = new GameObject("BottomBlackBar", typeof(RectTransform), typeof(Image));
        bottomBarGo.transform.SetParent(transform, false);
        _bottomBar = bottomBarGo.GetComponent<RectTransform>();
        _bottomBar.anchorMin = new Vector2(0f, 0f);
        _bottomBar.anchorMax = new Vector2(1f, 0f);
        _bottomBar.pivot = new Vector2(0.5f, 0f);
        _bottomBar.sizeDelta = new Vector2(0f, 0f);
        Image btmImg = bottomBarGo.GetComponent<Image>();
        btmImg.color = Color.black;
        btmImg.raycastTarget = false;

        // 3. Subtitle Text Container (Genshin Impact Style: ไร้กรอบพื้นหลัง ไร้กล่อง มีเฉพาะตัวหนังสือลอยพร้อมเงาคมชัด)
        GameObject subContainer = new GameObject("SubtitleContainer", typeof(RectTransform), typeof(CanvasGroup));
        subContainer.transform.SetParent(_bottomBar, false);
        RectTransform subRect = subContainer.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.12f, 0f);
        subRect.anchorMax = new Vector2(0.88f, 1f);
        subRect.offsetMin = Vector2.zero;
        subRect.offsetMax = Vector2.zero;

        _subtitleGroup = subContainer.GetComponent<CanvasGroup>();
        _subtitleGroup.alpha = 0f;

        // Speaker Name (Genshin Style: ชื่อตัวละครสีทองอร่าม ลอยอิสระ ไร้กล่องพื้นหลัง)
        GameObject speakerGo = new GameObject("SpeakerText", typeof(RectTransform), typeof(Text));
        speakerGo.transform.SetParent(subContainer.transform, false);
        RectTransform spkRect = speakerGo.GetComponent<RectTransform>();
        spkRect.anchorMin = new Vector2(0f, 1f);
        spkRect.anchorMax = new Vector2(1f, 1f);
        spkRect.pivot = new Vector2(0f, 1f);
        spkRect.anchoredPosition = new Vector2(30f, -14f);
        spkRect.sizeDelta = new Vector2(-60f, 32f);

        _speakerText = speakerGo.GetComponent<Text>();
        _speakerText.font = thaiFont != null ? thaiFont : VisionOSUITheme.BoldFont;
        _speakerText.fontSize = 21;
        _speakerText.fontStyle = FontStyle.Bold;
        _speakerText.color = new Color(1f, 0.82f, 0.35f, 1f); // Genshin Impact Signature Gold
        _speakerText.alignment = TextAnchor.MiddleLeft;
        var spkShadow = speakerGo.AddComponent<Shadow>();
        spkShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        spkShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // Dialogue Body Text (Genshin Style: ตัวหนังสือสีขาวคมชัด ลอยอิสระบนแถบดำภาพยนตร์)
        GameObject dialogueGo = new GameObject("DialogueText", typeof(RectTransform), typeof(Text));
        dialogueGo.transform.SetParent(subContainer.transform, false);
        RectTransform diaRect = dialogueGo.GetComponent<RectTransform>();
        diaRect.anchorMin = new Vector2(0f, 0f);
        diaRect.anchorMax = new Vector2(1f, 1f);
        diaRect.pivot = new Vector2(0f, 1f);
        diaRect.offsetMin = new Vector2(30f, 14f);
        diaRect.offsetMax = new Vector2(-30f, -50f);

        _dialogueText = dialogueGo.GetComponent<Text>();
        _dialogueText.font = thaiFont != null ? thaiFont : VisionOSUITheme.PrimaryFont;
        _dialogueText.fontSize = 19;
        _dialogueText.lineSpacing = 1.35f;
        _dialogueText.color = Color.white;
        _dialogueText.alignment = TextAnchor.UpperLeft;
        var diaShadow = dialogueGo.AddComponent<Shadow>();
        diaShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        diaShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // Continue / Skip Prompt (Genshin Style: ข้อความระบุปุ่มกดตัวเล็กมุมล่างขวา ไร้กล่องปุ่ม)
        GameObject promptGo = new GameObject("ContinuePrompt", typeof(RectTransform), typeof(Text));
        promptGo.transform.SetParent(subContainer.transform, false);
        RectTransform pRect = promptGo.GetComponent<RectTransform>();
        pRect.anchorMin = new Vector2(1f, 0f);
        pRect.anchorMax = new Vector2(1f, 0f);
        pRect.pivot = new Vector2(1f, 0f);
        pRect.anchoredPosition = new Vector2(-20f, 14f);
        pRect.sizeDelta = new Vector2(300f, 26f);

        _continuePromptText = promptGo.GetComponent<Text>();
        _continuePromptText.font = thaiFont != null ? thaiFont : VisionOSUITheme.PrimaryFont;
        _continuePromptText.fontSize = 13;
        _continuePromptText.color = new Color(0.85f, 0.85f, 0.85f, 0.75f);
        _continuePromptText.alignment = TextAnchor.MiddleRight;
        _continuePromptText.text = "[ Space / คลิก เพื่อไปต่อ ▶ ]";
        var pShadow = promptGo.AddComponent<Shadow>();
        pShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        pShadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private void UpdateBarPositions()
    {
        float targetPixelHeight = 1080f * _currentBarHeight;
        if (_topBar != null) _topBar.sizeDelta = new Vector2(0f, targetPixelHeight);
        if (_bottomBar != null) _bottomBar.sizeDelta = new Vector2(0f, targetPixelHeight);
    }

    /// <summary>
    /// สั่งเปิดแถบดำขอบภาพยนตร์ พร้อมสั่งซ่อนหน้าต่างเควสทันที
    /// </summary>
    public void ShowLetterbox()
    {
        _isLetterboxActive = true;
        _targetBarHeight = barHeightPercent;
        if (_topBar != null) _topBar.gameObject.SetActive(true);
        if (_bottomBar != null) _bottomBar.gameObject.SetActive(true);

        // ซ่อนหน้าต่างเควสขณะมีแถบดำภาพยนตร์
        if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
        if (LondonAlleyQuestManager.Instance != null) LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
        if (RoyalLyceumTheaterQuestManager.Instance != null) RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(false);
    }

    /// <summary>
    /// ปิดแถบดำขอบภาพยนตร์แบบเด็ดขาดทันที (สำหรับใช้ตอนเปิดมินิเกมท้ายเควส ไม่ให้มีคอปบนล่างเด็ดขาด)
    /// </summary>
    public void ForceDisableLetterbox()
    {
        _isLetterboxActive = false;
        _targetBarHeight = 0f;
        _currentBarHeight = 0f;
        UpdateBarPositions();
        if (_topBar != null) _topBar.gameObject.SetActive(false);
        if (_bottomBar != null) _bottomBar.gameObject.SetActive(false);
        if (_subtitleGroup != null) _subtitleGroup.alpha = 0f;
        StopTyping();
        _isWaitingForInput = false;
        _dialogueQueue.Clear();
    }

    /// <summary>
    /// สั่งปิดแถบดำกลับสู่มุมมองเกมปกติ พร้อมนำหน้าต่างเควสกลับมา
    /// </summary>
    public void HideLetterbox(bool instant = false)
    {
        _isLetterboxActive = false;
        _targetBarHeight = 0f;
        if (instant)
        {
            _currentBarHeight = 0f;
            UpdateBarPositions();
            if (_topBar != null) _topBar.gameObject.SetActive(false);
            if (_bottomBar != null) _bottomBar.gameObject.SetActive(false);
        }
        if (_subtitleGroup != null) _subtitleGroup.alpha = 0f;
        StopTyping();
        _isWaitingForInput = false;
        _dialogueQueue.Clear();

        // ตรวจสอบว่ามีมินิเกมเปิดอยู่หรือไม่ ถ้ามีมินิเกมเปิดอยู่ ห้ามเปิดหน้าต่างเควสเด็ดขาด
        if (NewspaperDecryptionMinigame.Instance != null && NewspaperDecryptionMinigame.Instance.IsMinigameActive) return;
        if (LondonRailwayMapMinigame.Instance != null && LondonRailwayMapMinigame.Instance.IsMinigameActive) return;
        if (CaseArchiveJournalUI.Instance != null && CaseArchiveJournalUI.Instance.IsOpen) return;

        // นำหน้าต่างเควสกลับมาแสดงตามเควสที่กำลังดำเนินอยู่ (ป้องกัน UI Scene 1, 2, 3 ซ้อนทับกัน)
        if (RoyalLyceumTheaterQuestManager.Instance != null && RoyalLyceumTheaterQuestManager.Instance.IsQuest3Active)
        {
            RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(true);
            if (LondonAlleyQuestManager.Instance != null) LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
            if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
        }
        else if (LondonAlleyQuestManager.Instance != null && LondonAlleyQuestManager.Instance.IsQuest2Active)
        {
            LondonAlleyQuestManager.Instance.ShowObjectiveHUD(true);
            if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
            if (RoyalLyceumTheaterQuestManager.Instance != null) RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(false);
        }
        else if (StMaryChurchQuestManager.Instance != null && !StMaryChurchQuestManager.Instance.IsQuest1Finished)
        {
            StMaryChurchQuestManager.Instance.ShowObjectiveHUD(true);
            if (LondonAlleyQuestManager.Instance != null) LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
            if (RoyalLyceumTheaterQuestManager.Instance != null) RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(false);
        }
    }

    /// <summary>
    /// แสดงบทสนทนาเดี่ยว (Single Dialogue)
    /// </summary>
    public void ShowDialogue(string speaker, string text, Action onComplete = null)
    {
        PlayDialogueSequence(new List<DialogueEntry> { new DialogueEntry(speaker, text, onComplete) });
    }

    /// <summary>
    /// แสดงบทสนทนาชุดหนึ่ง (คัตซีนบทพูด)
    /// </summary>
    public void PlayDialogueSequence(List<DialogueEntry> entries, Action onFinished = null)
    {
        ShowLetterbox();
        _onDialogueFinished = onFinished;
        _dialogueQueue.Clear();

        foreach (var entry in entries)
        {
            _dialogueQueue.Enqueue(entry);
        }

        AdvanceDialogue();
    }

    private void AdvanceDialogue()
    {
        if (_dialogueQueue.Count > 0)
        {
            DialogueEntry current = _dialogueQueue.Dequeue();
            _speakerText.text = current.speaker;
            _subtitleGroup.alpha = 1f;
            _isWaitingForInput = true;
            _autoPlayTimer = 0f;
            StartTyping(current.text ?? "");
            if (current.minHold > _currentLineHold) _currentLineHold = current.minHold;
            current.onComplete?.Invoke();
        }
        else
        {
            StopTyping();
            _subtitleGroup.alpha = 0f;
            _isWaitingForInput = false;
            _onDialogueFinished?.Invoke();
        }
    }

    // ==========================================
    // Typewriter: พิมพ์ทีละตัว (ข้ามสระบน/ล่างและวรรณยุกต์ไทยให้ขึ้นพร้อมพยัญชนะ)
    // ==========================================
    private void StartTyping(string text)
    {
        StopTyping();
        _fullLineText = text;

        int visibleLength = CountVisibleChars(text);
        float readHold = Mathf.Clamp(visibleLength / Mathf.Max(1f, readingCharsPerSecond), 2.5f, 9f);
        _currentLineHold = Mathf.Max(autoPlayDurationPerShot * 0.6f, readHold);

        // ข้อความที่มี Rich Text tag แสดงทันที (การตัดกลาง tag จะทำให้แสดงผลผิด)
        if (text.IndexOf('<') >= 0 || typewriterCharsPerSecond <= 0f || !isActiveAndEnabled)
        {
            _dialogueText.text = text;
            _isTyping = false;
            return;
        }

        _typeRoutine = StartCoroutine(TypeRoutine(text));
    }

    private IEnumerator TypeRoutine(string text)
    {
        _isTyping = true;
        _dialogueText.text = "";
        float interval = 1f / typewriterCharsPerSecond;
        int i = 0;
        float acc = 0f;
        while (i < text.Length)
        {
            acc += Time.unscaledDeltaTime;
            while (acc >= interval && i < text.Length)
            {
                acc -= interval;
                i++;
                while (i < text.Length && IsThaiCombiningMark(text[i])) i++;
            }
            _dialogueText.text = text.Substring(0, i);
            yield return null;
        }
        _isTyping = false;
        _typeRoutine = null;
    }

    private void FinishTyping()
    {
        StopTyping();
        _dialogueText.text = _fullLineText;
        _autoPlayTimer = 0f;
    }

    private void StopTyping()
    {
        if (_typeRoutine != null) StopCoroutine(_typeRoutine);
        _typeRoutine = null;
        _isTyping = false;
    }

    private static bool IsThaiCombiningMark(char c)
    {
        return c == 'ั' || (c >= 'ิ' && c <= 'ฺ') || (c >= '็' && c <= '๎');
    }

    private static int CountVisibleChars(string text)
    {
        int count = 0;
        bool inTag = false;
        foreach (char c in text)
        {
            if (c == '<') { inTag = true; continue; }
            if (c == '>') { inTag = false; continue; }
            if (!inTag && !IsThaiCombiningMark(c)) count++;
        }
        return count;
    }
}
