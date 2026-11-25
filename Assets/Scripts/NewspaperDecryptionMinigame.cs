using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// มินิเกมบทที่ 2: ถอดรหัสหนังสือพิมพ์ของมอริอาร์ตี้ (2 ช่วง ตาม GDD)
/// ช่วงที่ 1 "ส่องหา": ใช้แว่นขยายส่องหน้าหนังสือพิมพ์ The Daily Telegraph หา 7 ตัวอักษรที่ถูกซ่อนด้วยหมึกสีเลือด คลิกเพื่อวงตัวอักษรเก็บลงสมุด
/// ช่วงที่ 2 "ถอดรหัส": หมุนจานซีซาร์ (Caesar Cipher Disk) ให้ตัวอักษร WKHDWHU กลายเป็นคำที่อ่านออก ได้คำตอบ "THEATER"
///   สาส์นของมอริอาร์ตี้ใบ้ว่า "สามก้าวที่ย่างกรายถอยร่น" และมีระบบคำใบ้ 3 ระดับ
/// ควบคุม: ช่วง 1 เมาส์ส่องและคลิก | ช่วง 2 ลากจานด้วยเมาส์ / ล้อเมาส์ / [A][D] หรือลูกศร
/// </summary>
public class NewspaperDecryptionMinigame : MonoBehaviour
{
    public static NewspaperDecryptionMinigame Instance { get; private set; }

    [Header("Fonts & Sprites")]
    public Font thaiFont;
    public Font thaiFontBold;
    public Sprite parchmentPaperSprite;

    public bool IsMinigameActive => _root != null && _root.activeSelf;
    public Action OnPuzzleSolved;

    private const string CIPHER_TEXT = "WKHDWHU";   // THEATER เลื่อน +3
    private const string TARGET_ANSWER = "THEATER";
    private const float AlphabetStep = 360f / 26f;

    private enum Phase { Find, Transition, Decode, Solved }

    // ตำแหน่งตัวอักษรที่ซ่อนบนหนังสือพิมพ์ (พิกัดกึ่งกลางกระดาษ) เรียงตามลำดับของรหัส
    private static readonly char[] MarkLetters = { 'W', 'K', 'H', 'D', 'W', 'H', 'U' };
    private static readonly Vector2[] MarkPositions =
    {
        new Vector2(-425f, 380f),
        new Vector2(335f, 262f),
        new Vector2(-350f, -20f),
        new Vector2(8f, 62f),
        new Vector2(-30f, -258f),
        new Vector2(352f, -100f),
        new Vector2(330f, -352f)
    };

    private const float LensRadius = 112f;
    private static readonly Vector2 PaperPos = new Vector2(-300f, 0f);
    private static readonly Vector2 PaperSize = new Vector2(1060f, 940f);
    private static readonly Vector2 NotebookPos = new Vector2(690f, 0f);
    private static readonly Vector2 NotebookSize = new Vector2(500f, 940f);

    private static readonly Color Ink = new Color(0.14f, 0.10f, 0.07f, 1f);
    private static readonly Color InkMuted = new Color(0.35f, 0.26f, 0.18f, 1f);
    private static readonly Color BloodRed = new Color(0.72f, 0.08f, 0.08f, 1f);
    private static readonly Color Brass = new Color(0.86f, 0.70f, 0.36f, 1f);
    private static readonly Color GreenInk = new Color(0.10f, 0.42f, 0.20f, 1f);

    // ----- UI root -----
    private GameObject _root;
    private CanvasGroup _rootGroup;
    private Font _typewriterFont;

    // ----- ช่วงส่องหา -----
    private RectTransform _paper;
    private CanvasGroup _paperGroup;
    private RectTransform _lens;
    private Image _lensRing;
    private Text _lensHint;
    private readonly List<Mark> _marks = new List<Mark>();
    private int _foundCount;

    // ----- สมุดบันทึก -----
    private RectTransform _notebook;
    private Text _messageText;
    private Text _foundProgressText;
    private readonly Text[] _foundSlots = new Text[7];
    private readonly Image[] _foundSlotBoxes = new Image[7];
    private Text _instructionText;
    private GameObject _decodeSection;
    private readonly Text[] _decodedSlots = new Text[7];
    private readonly Image[] _decodedSlotBoxes = new Image[7];
    private Text _shiftLabel;
    private Text _feedbackText;
    private Button _hintButton;
    private Text _hintButtonLabel;
    private Image _hintButtonImage;
    private Text _bottomHint;

    // ----- ช่วงถอดรหัส (จานซีซาร์) -----
    private RectTransform _disk;
    private CanvasGroup _diskGroup;
    private RectTransform _innerRing;
    private Text _hubShiftText;
    private Text _solvedStamp;

    // ----- สถานะ -----
    private Phase _phase = Phase.Find;
    private int _shift;
    private float _ringAngle;
    private int _hintLevel;
    private float _decodeStartTime;
    private bool _wasWaypointVisible;
    private float _dragStartAngle;
    private int _dragStartShift;

    private class Mark
    {
        public Vector2 pos;
        public char letter;
        public int slot;
        public RectTransform root;
        public CanvasGroup reveal;
        public float alpha;
        public bool collected;
    }

    // ==========================================
    // Lifecycle
    // ==========================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (thaiFont == null) thaiFont = GameAssets.Load<Font>("Assets/Font/Sarabun-Medium.ttf");
        if (thaiFontBold == null) thaiFontBold = GameAssets.Load<Font>("Assets/Font/Prompt-Bold.ttf");
        if (thaiFont == null) thaiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (thaiFontBold == null) thaiFontBold = thaiFont;
        _typewriterFont = Font.CreateDynamicFontFromOSFont(new[] { "Courier New", "Consolas" }, 24);
        if (_typewriterFont == null) _typewriterFont = thaiFontBold;

        BuildUI();
        _root.SetActive(false);
    }

    private void Update()
    {
        if (!IsMinigameActive) return;

        // ปลดล็อคเมาส์ให้เคลื่อนที่และคลิกได้อย่างอิสระตลอดเวลา
        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        float dt = Time.unscaledDeltaTime;
        if (_phase == Phase.Find) UpdateFindPhase(dt);
        else if (_phase == Phase.Decode) UpdateDecodePhase(dt);
    }

    // ==========================================
    // Open / Close
    // ==========================================
    public void OpenMinigame(Action onSolvedCallback = null)
    {
        // 1. ปิดแถบดำภาพยนตร์ออกทันที (ไม่มีคร็อปบนล่างในหน้ามินิเกม)
        if (CinematicLetterbox.Instance != null) CinematicLetterbox.Instance.ForceDisableLetterbox();

        // 2. ซ่อนหน้าต่างเควสและ HUD ทั้งหมดไม่ให้ซ้อนทับมินิเกม
        if (LondonAlleyQuestManager.Instance != null) LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
        if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
        if (RoyalLyceumTheaterQuestManager.Instance != null) RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(false);
        DetectiveThoughtToast.Clear();

        // 3. บล็อกและปิดระบบสแกน CiRA CORE
        CiraCoreVisionScanner.IsScannerBlocked = true;
        if (CiraCoreVisionScanner.Instance != null) CiraCoreVisionScanner.Instance.ForceCloseScanMode();

        // 4. ซ่อนระบบนำทาง (จำสถานะเดิมไว้คืนตอนปิด)
        if (WaypointNavigationSystem.Instance != null)
        {
            _wasWaypointVisible = WaypointNavigationSystem.Instance.IsVisible;
            WaypointNavigationSystem.Instance.SetVisible(false);
        }

        OnPuzzleSolved = onSolvedCallback;
        ResetState();

        _root.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeGroup(_rootGroup, 0f, 1f, 0.35f));

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (CinematicCutsceneManager.Instance != null) CinematicCutsceneManager.Instance.SetPlayerControlsLocked(true);
    }

    public void CloseMinigame()
    {
        if (_root != null) _root.SetActive(false);

        CiraCoreVisionScanner.IsScannerBlocked = false;

        if (WaypointNavigationSystem.Instance != null && _wasWaypointVisible)
        {
            WaypointNavigationSystem.Instance.SetVisible(true);
        }

        if (LondonAlleyQuestManager.Instance != null && LondonAlleyQuestManager.Instance.IsQuest2Active)
        {
            LondonAlleyQuestManager.Instance.ShowObjectiveHUD(true);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (CinematicCutsceneManager.Instance != null) CinematicCutsceneManager.Instance.SetPlayerControlsLocked(false);
    }

    private void ResetState()
    {
        _phase = Phase.Find;
        _foundCount = 0;
        _hintLevel = 0;
        _shift = UnityEngine.Random.Range(5, 11);
        _ringAngle = _shift * AlphabetStep;
        _innerRing.localEulerAngles = new Vector3(0f, 0f, _ringAngle);

        foreach (var m in _marks)
        {
            m.collected = false;
            m.alpha = 0f;
            m.reveal.alpha = 0f;
        }
        for (int i = 0; i < 7; i++)
        {
            _foundSlots[i].text = "";
            _foundSlotBoxes[i].color = new Color(0.14f, 0.10f, 0.07f, 0.12f);
            _decodedSlots[i].text = "";
            _decodedSlotBoxes[i].color = new Color(0.14f, 0.10f, 0.07f, 0.12f);
        }

        _paper.gameObject.SetActive(true);
        _paperGroup.alpha = 1f;
        _paper.anchoredPosition = PaperPos;
        _disk.gameObject.SetActive(false);
        _diskGroup.alpha = 0f;
        _solvedStamp.gameObject.SetActive(false);
        _lens.gameObject.SetActive(true);
        _decodeSection.SetActive(false);
        _instructionText.gameObject.SetActive(true);
        _feedbackText.text = "";
        _bottomHint.text = "เลื่อนเมาส์ส่องหน้าหนังสือพิมพ์   [คลิกซ้าย] วงตัวอักษรที่เจอ";
        _foundProgressText.text = "พบแล้ว  0 / 7";
        _hintButtonLabel.text = "ขอคำใบ้  (0/3)";
        _messageText.text = MessageText(false);
    }

    private static string MessageText(bool highlightClue)
    {
        string clue = "สามก้าวที่ย่างกรายถอยร่นสู่เงามืด";
        if (highlightClue) clue = "<color=#9E1B1B><b>" + clue + "</b></color>";
        return "\"" + clue + " จะเปิดเผยเวทีแห่งโศกนาฏกรรมถัดไป...\"";
    }

    // ==========================================
    // ช่วงที่ 1: ส่องหาตัวอักษร
    // ==========================================
    private void UpdateFindPhase(float dt)
    {
        Vector2 mouse = MousePosition();
        bool overPaper = RectTransformUtility.RectangleContainsScreenPoint(_paper, mouse, null);

        _lens.gameObject.SetActive(overPaper);
        Vector2 paperLocal = Vector2.zero;
        if (overPaper)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_paper, mouse, null, out paperLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_root.transform, mouse, null, out Vector2 rootLocal);
            _lens.anchoredPosition = rootLocal;
        }

        // เผยหมึกสีเลือดเมื่ออยู่ใต้แว่นขยาย
        Mark hovered = null;
        float bestDist = float.MaxValue;
        foreach (var m in _marks)
        {
            float target = 0f;
            if (m.collected) target = 1f;
            else if (overPaper)
            {
                float d = Vector2.Distance(paperLocal, m.pos);
                target = Mathf.Clamp01((LensRadius - d) / 38f);
                if (target > 0.55f && d < bestDist && d < 42f)
                {
                    bestDist = d;
                    hovered = m;
                }
            }
            m.alpha = Mathf.MoveTowards(m.alpha, target, dt * 7f);
            m.reveal.alpha = m.alpha;
        }

        // แว่นขยายกะพริบสีแดงเมื่อชี้โดนตัวอักษร
        _lensRing.color = hovered != null ? new Color(1f, 0.35f, 0.28f, 1f) : Brass;
        _lensHint.gameObject.SetActive(hovered != null);

        if (hovered != null && LeftClickPressed())
        {
            Collect(hovered);
        }
    }

    private void Collect(Mark mark)
    {
        if (mark.collected) return;
        mark.collected = true;
        _foundCount++;
        _foundProgressText.text = $"พบแล้ว  {_foundCount} / 7";
        StartCoroutine(FlyLetterToSlot(mark));

        if (_foundCount >= MarkLetters.Length)
        {
            StartCoroutine(TransitionToDecodeRoutine());
        }
    }

    private IEnumerator FlyLetterToSlot(Mark mark)
    {
        // ตัวอักษรลอยจากกระดาษไปลงช่องในสมุดบันทึก
        var flyGo = new GameObject("FlyingLetter", typeof(RectTransform), typeof(Text));
        flyGo.transform.SetParent(_root.transform, false);
        var fly = flyGo.GetComponent<Text>();
        fly.font = thaiFontBold;
        fly.text = mark.letter.ToString();
        fly.fontSize = 46;
        fly.color = BloodRed;
        fly.alignment = TextAnchor.MiddleCenter;
        fly.raycastTarget = false;
        var rt = (RectTransform)flyGo.transform;
        rt.sizeDelta = new Vector2(60f, 60f);

        Vector3 from = mark.root.position;
        Vector3 to = _foundSlots[mark.slot].transform.position;
        float t = 0f;
        const float duration = 0.55f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            Vector3 p = Vector3.Lerp(from, to, k);
            p.y += Mathf.Sin(k * Mathf.PI) * 60f; // ส่งเป็นวงโค้ง
            rt.position = p;
            rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.85f, k);
            yield return null;
        }

        Destroy(flyGo);
        _foundSlots[mark.slot].text = mark.letter.ToString();
        _foundSlotBoxes[mark.slot].color = new Color(0.72f, 0.08f, 0.08f, 0.18f);
    }

    private IEnumerator TransitionToDecodeRoutine()
    {
        _phase = Phase.Transition;
        _lens.gameObject.SetActive(false);
        yield return new WaitForSecondsRealtime(1.1f);

        _bottomHint.text = "หมุนจานซีซาร์   ลากด้วยเมาส์ / ล้อเมาส์ / [A][D] หรือลูกศร";
        _disk.gameObject.SetActive(true);
        Vector2 diskStart = PaperPos + new Vector2(80f, 0f);
        yield return Animate(0.5f, t =>
        {
            _paperGroup.alpha = 1f - t;
            _paper.anchoredPosition = Vector2.Lerp(PaperPos, PaperPos + new Vector2(-80f, 0f), t);
            _diskGroup.alpha = t;
            _disk.anchoredPosition = Vector2.Lerp(diskStart, PaperPos, t);
            _disk.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, t);
        });
        _paper.gameObject.SetActive(false);

        _instructionText.gameObject.SetActive(false);
        _decodeSection.SetActive(true);
        _feedbackText.text = "หมุนวงในจนตัวอักษรที่ได้อ่านเป็นคำภาษาอังกฤษ";
        _decodeStartTime = Time.unscaledTime;
        _phase = Phase.Decode;
        RefreshDecodeDisplay();
    }

    // ==========================================
    // ช่วงที่ 2: จานซีซาร์
    // ==========================================
    private void UpdateDecodePhase(float dt)
    {
        int delta = 0;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame) delta -= 1;
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) delta += 1;
        }
        var mouse = Mouse.current;
        float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
#else
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Q)) delta -= 1;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.E)) delta += 1;
        float wheel = Input.mouseScrollDelta.y * 120f;
#endif
        if (Mathf.Abs(wheel) > 0.01f) delta += wheel > 0f ? 1 : -1;
        if (delta != 0) SetShift(_shift + delta);

        // หมุนจานแบบนุ่มนวล
        float targetAngle = _shift * AlphabetStep;
        _ringAngle = Mathf.Lerp(_ringAngle, targetAngle, 1f - Mathf.Exp(-14f * dt));
        _innerRing.localEulerAngles = new Vector3(0f, 0f, _ringAngle);

        // ปุ่มคำใบ้กะพริบเตือนถ้ายังไม่ขอคำใบ้เกิน 35 วินาที
        if (_hintLevel == 0 && Time.unscaledTime - _decodeStartTime > 35f)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            _hintButtonImage.color = new Color(Ink.r * pulse + 0.3f * (1f - pulse), Ink.g, Ink.b, 1f);
        }

        if (Mathf.Abs(_ringAngle - targetAngle) < 1.5f && Decode(CIPHER_TEXT, _shift) == TARGET_ANSWER)
        {
            StartCoroutine(SolvedRoutine());
        }
    }

    private void SetShift(int newShift)
    {
        if (_phase != Phase.Decode) return;
        _shift = newShift;
        RefreshDecodeDisplay();
    }

    private void RefreshDecodeDisplay()
    {
        string decoded = Decode(CIPHER_TEXT, _shift);
        for (int i = 0; i < 7; i++)
        {
            _decodedSlots[i].text = decoded[i].ToString();
            _decodedSlots[i].color = Ink;
            _decodedSlotBoxes[i].color = new Color(0.14f, 0.10f, 0.07f, 0.12f);
        }

        int shown = ((_shift % 26) + 26) % 26;
        if (shown > 13) shown -= 26;
        string text = shown > 0 ? "+" + shown : shown.ToString();
        _shiftLabel.text = "คีย์เลื่อน  (SHIFT)  " + text;
        _hubShiftText.text = text;
    }

    private static string Decode(string input, int shift)
    {
        char[] buffer = input.ToCharArray();
        for (int i = 0; i < buffer.Length; i++)
        {
            char c = buffer[i];
            if (c < 'A' || c > 'Z') continue;
            int pos = ((c - 'A' + shift) % 26 + 26) % 26;
            buffer[i] = (char)('A' + pos);
        }
        return new string(buffer);
    }

    private void OnHintPressed()
    {
        if (_phase != Phase.Decode || _hintLevel >= 3) return;
        _hintLevel++;
        _hintButtonImage.color = Ink;
        _hintButtonLabel.text = $"ขอคำใบ้  ({_hintLevel}/3)";

        switch (_hintLevel)
        {
            case 1:
                _messageText.text = MessageText(true);
                _feedbackText.text = "<b>คำใบ้ 1</b>  \"สามก้าว\" คือ 3 ตำแหน่ง ส่วน \"ถอยร่น\" คือถอยไปหาตัวอักษรก่อนหน้า";
                break;
            case 2:
                _feedbackText.text = "<b>คำใบ้ 2</b>  หมุนวงในให้คีย์เลื่อนเป็น -3 แล้วดูว่า W กลายเป็นตัวอะไร";
                break;
            default:
                _feedbackText.text = "<b>คำใบ้ 3</b>  ตั้งคีย์ให้แล้ว ลองอ่านคำที่ได้";
                SetShift(-3 + 26 * Mathf.RoundToInt(_shift / 26f));
                break;
        }
    }

    private IEnumerator SolvedRoutine()
    {
        _phase = Phase.Solved;
        for (int i = 0; i < 7; i++)
        {
            _decodedSlots[i].color = GreenInk;
            _decodedSlotBoxes[i].color = new Color(0.10f, 0.42f, 0.20f, 0.22f);
        }
        _feedbackText.text = "<color=#0A5C22><b>ไขปริศนาสำเร็จ</b></color>  สาส์นของมอริอาร์ตี้ชี้ไปที่ THEATER (โรงละคร)";

        _solvedStamp.gameObject.SetActive(true);
        var stampRect = _solvedStamp.rectTransform;
        yield return Animate(0.2f, t =>
        {
            stampRect.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, t);
            _solvedStamp.color = new Color(BloodRed.r, BloodRed.g, BloodRed.b, 0.85f * t);
        });

        yield return new WaitForSecondsRealtime(2.2f);
        yield return FadeGroup(_rootGroup, 1f, 0f, 0.3f);

        CloseMinigame();
        OnPuzzleSolved?.Invoke();
    }

    // ==========================================
    // Input helpers
    // ==========================================
    private static Vector2 MousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    private static bool LeftClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private static IEnumerator Animate(float duration, Action<float> step)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            step(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        step(1f);
    }

    private static IEnumerator FadeGroup(CanvasGroup g, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        g.alpha = to;
    }

    // ==========================================
    // UI construction
    // ==========================================
    private void BuildUI()
    {
        _root = new GameObject("NewspaperCipher_Canvas", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        var rootCanvas = _root.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 960;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        _root.AddComponent<GraphicRaycaster>();
        _rootGroup = _root.AddComponent<CanvasGroup>();

        // พื้นหลัง: โต๊ะไม้ก๊อกมืดและขอบมืดรอบจอ
        var bg = StretchImage("Backdrop", _root.transform, new Color(0.03f, 0.025f, 0.02f, 1f));
        bg.raycastTarget = true;
        Sprite cork = DetectiveUITheme.CorkboardBg;
        if (cork != null)
        {
            var corkImg = StretchImage("Corkboard", _root.transform, new Color(0.30f, 0.26f, 0.22f, 1f));
            corkImg.sprite = cork;
        }
        var vignette = StretchImage("Vignette", _root.transform, Color.white);
        vignette.sprite = DetectiveUIShapes.Vignette;

        BuildNewspaper();
        BuildDisk();
        BuildNotebook();
        BuildLens();
    }

    private void BuildNewspaper()
    {
        _paper = NewRect("Newspaper", _root.transform, PaperPos, PaperSize, null);
        _paper.localEulerAngles = new Vector3(0f, 0f, 0f);
        _paperGroup = _paper.gameObject.AddComponent<CanvasGroup>();

        var shadow = NewImage("Shadow", _paper, new Vector2(16f, -20f), PaperSize, new Color(0f, 0f, 0f, 0.6f));
        shadow.raycastTarget = false;

        // กระดาษหนังสือพิมพ์: ใช้พื้นผิวกระดาษเก่า โทนเทาอมเหลือง
        var sheetRect = NewRect("Sheet", _paper, Vector2.zero, PaperSize, null);
        var sheet = sheetRect.gameObject.AddComponent<RawImage>();
        sheet.raycastTarget = false;
        Sprite note = parchmentPaperSprite != null ? null : DetectiveUITheme.NoteCard;
        if (parchmentPaperSprite != null)
        {
            sheet.texture = parchmentPaperSprite.texture;
            sheet.color = new Color(0.93f, 0.90f, 0.82f, 1f);
        }
        else if (note != null)
        {
            sheet.texture = note.texture;
            sheet.uvRect = new Rect(0.105f, 0.232f, 0.79f, 0.54f);
            sheet.color = new Color(0.94f, 0.92f, 0.85f, 1f);
        }
        else sheet.color = new Color(0.92f, 0.88f, 0.78f, 1f);

        // หัวหนังสือพิมพ์
        NewText("Masthead", _paper, "THE DAILY TELEGRAPH", thaiFontBold, 66, Ink, TextAnchor.MiddleCenter,
            new Vector2(0f, 385f), new Vector2(1000f, 90f), null, false);
        NewText("Dateline", _paper, "LONDON  ·  SATURDAY, 8 SEPTEMBER 1888  ·  ONE PENNY", _typewriterFont, 19, InkMuted, TextAnchor.MiddleCenter,
            new Vector2(0f, 336f), new Vector2(1000f, 26f), null, false);
        MakeRule(_paper, new Vector2(0f, 316f), new Vector2(980f, 3f), 0.7f);
        MakeRule(_paper, new Vector2(0f, 309f), new Vector2(980f, 1.5f), 0.7f);

        NewText("Headline", _paper, "ฆาตกรรมสะเทือนขวัญแห่งตรอกไวท์ชาเปล", thaiFontBold, 40, Ink, TextAnchor.MiddleCenter,
            new Vector2(0f, 262f), new Vector2(1000f, 56f), null, false);
        var sub = NewText("SubHeadline", _paper, "ตำรวจพบสตรีรายที่สองในคืนเดียว สารวัตรนิวแมนเร่งสืบสวนท่ามกลางหมอกหนา", thaiFont, 23, InkMuted, TextAnchor.MiddleCenter,
            new Vector2(0f, 219f), new Vector2(1000f, 32f), null, false);
        sub.fontStyle = FontStyle.Italic;
        MakeRule(_paper, new Vector2(0f, 194f), new Vector2(980f, 2f), 0.6f);

        // ภาพถ่ายแกะไม้ (กรอบเข้ม) ในคอลัมน์ซ้าย
        var photo = NewImage("Photo", _paper, new Vector2(-345f, 100f), new Vector2(300f, 150f), new Color(0.20f, 0.16f, 0.12f, 0.85f));
        photo.raycastTarget = false;
        for (int i = 0; i < 9; i++)
        {
            var hatch = NewImage("Hatch_" + i, photo.transform, new Vector2(0f, 62f - i * 15f), new Vector2(280f - (i % 3) * 40f, 3f), new Color(0.85f, 0.80f, 0.68f, 0.22f));
            hatch.raycastTarget = false;
        }
        MakeRule(_paper, new Vector2(-345f, 14f), new Vector2(300f, 2f), 0.5f);

        // คอลัมน์ข่าว (เส้นข้อความจำลอง) + เส้นแบ่งคอลัมน์
        var rng = new System.Random(1888);
        for (int col = 0; col < 3; col++)
        {
            float cx = -345f + col * 345f;
            float y = col == 0 ? -4f : 168f;
            int lineInParagraph = 0;
            while (y > -440f)
            {
                if (lineInParagraph > 4 && rng.NextDouble() < 0.22)
                {
                    // ท้ายย่อหน้า: บรรทัดสั้น + เว้นช่อง
                    float shortW = 300f * (0.3f + (float)rng.NextDouble() * 0.3f);
                    MakeTextLine(cx, y, shortW);
                    y -= 30f;
                    lineInParagraph = 0;
                    if (rng.NextDouble() < 0.5 && y > -400f)
                    {
                        // หัวข้อย่อย (แถบหนา)
                        var head = NewImage("SubHead", _paper, new Vector2(cx - (300f - 170f) * 0.5f, y), new Vector2(170f, 9f), new Color(Ink.r, Ink.g, Ink.b, 0.6f));
                        head.raycastTarget = false;
                        y -= 22f;
                    }
                    continue;
                }
                MakeTextLine(cx, y, 300f);
                y -= 17f;
                lineInParagraph++;
            }
            if (col < 2) MakeRule(_paper, new Vector2(cx + 172f, -125f), new Vector2(1.5f, 620f), 0.35f);
        }

        // รอยเปื้อนจาง ๆ + ตัวอักษรหมึกเลือดที่ซ่อนอยู่ (เห็นได้เมื่อส่องด้วยแว่นขยาย)
        _marks.Clear();
        for (int i = 0; i < MarkLetters.Length; i++)
        {
            var root = NewRect("Mark_" + i, _paper, MarkPositions[i], new Vector2(90f, 90f), null);
            var stain = NewImage("Stain", root, Vector2.zero, new Vector2(78f, 78f), new Color(0.32f, 0.12f, 0.08f, 0.24f));
            stain.sprite = WaypointSprites.Glow;
            stain.raycastTarget = false;

            var revealRect = NewRect("Reveal", root, Vector2.zero, new Vector2(90f, 90f), null);
            var group = revealRect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var ring = NewImage("Circle", revealRect, Vector2.zero, new Vector2(70f, 70f), BloodRed);
            ring.sprite = DetectiveUIShapes.Ring(0.07f);
            ring.raycastTarget = false;
            var letter = NewText("Letter", revealRect, MarkLetters[i].ToString(), thaiFontBold, 44, BloodRed, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(70f, 70f), null, false);
            NewText("Order", revealRect, (i + 1).ToString(), _typewriterFont, 15, BloodRed, TextAnchor.MiddleCenter,
                new Vector2(30f, 30f), new Vector2(24f, 20f), null, false);

            _marks.Add(new Mark { pos = MarkPositions[i], letter = MarkLetters[i], slot = i, root = root, reveal = group });
        }
    }

    private void MakeTextLine(float cx, float y, float width)
    {
        var line = NewImage("Line", _paper, new Vector2(cx - (300f - width) * 0.5f, y), new Vector2(width, 5.5f), new Color(Ink.r, Ink.g, Ink.b, 0.38f));
        line.raycastTarget = false;
    }

    private void MakeRule(RectTransform parent, Vector2 pos, Vector2 size, float alpha)
    {
        var rule = NewImage("Rule", parent, pos, size, new Color(Ink.r, Ink.g, Ink.b, alpha));
        rule.raycastTarget = false;
    }

    private void BuildLens()
    {
        _lens = NewRect("MagnifyingGlass", _root.transform, Vector2.zero, new Vector2(240f, 240f), null);
        var shadow = NewImage("Shadow", _lens, new Vector2(10f, -12f), new Vector2(232f, 232f), new Color(0f, 0f, 0f, 0.22f));
        shadow.sprite = DetectiveUIShapes.Circle;
        shadow.raycastTarget = false;
        var glass = NewImage("Glass", _lens, Vector2.zero, new Vector2(228f, 228f), new Color(1f, 0.78f, 0.62f, 0.10f));
        glass.sprite = DetectiveUIShapes.Circle;
        glass.raycastTarget = false;
        _lensRing = NewImage("Ring", _lens, Vector2.zero, new Vector2(236f, 236f), Brass);
        _lensRing.sprite = DetectiveUIShapes.Ring(0.05f);
        _lensRing.raycastTarget = false;
        var inner = NewImage("InnerRing", _lens, Vector2.zero, new Vector2(214f, 214f), new Color(Brass.r, Brass.g, Brass.b, 0.35f));
        inner.sprite = DetectiveUIShapes.Ring(0.012f);
        inner.raycastTarget = false;

        // ด้ามแว่นขยาย
        var shaft = NewImage("HandleShaft", _lens, new Vector2(84f, -84f), new Vector2(14f, 44f), new Color(0.62f, 0.48f, 0.24f, 1f));
        shaft.rectTransform.pivot = new Vector2(0.5f, 1f);
        shaft.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        shaft.raycastTarget = false;
        var grip = NewImage("HandleGrip", shaft.transform, new Vector2(0f, 0f), new Vector2(22f, 96f), new Color(0.20f, 0.12f, 0.07f, 1f));
        grip.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        grip.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        grip.rectTransform.pivot = new Vector2(0.5f, 1f);
        grip.rectTransform.anchoredPosition = Vector2.zero;
        grip.raycastTarget = false;

        _lensHint = NewText("Hint", _lens, "คลิกเพื่อวงตัวอักษร", thaiFontBold, 22, new Color(1f, 0.85f, 0.7f, 1f), TextAnchor.MiddleCenter,
            new Vector2(0f, 150f), new Vector2(300f, 30f), null, false);
        var hintShadow = _lensHint.gameObject.AddComponent<Shadow>();
        hintShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        hintShadow.effectDistance = new Vector2(1.5f, -1.5f);
        _lensHint.gameObject.SetActive(false);
    }

    private void BuildNotebook()
    {
        _notebook = NewRect("Notebook", _root.transform, NotebookPos, NotebookSize, null);
        var shadow = NewImage("Shadow", _notebook, new Vector2(12f, -16f), NotebookSize, new Color(0f, 0f, 0f, 0.55f));
        shadow.raycastTarget = false;

        var sheetRect = NewRect("Sheet", _notebook, Vector2.zero, NotebookSize, null);
        var sheet = sheetRect.gameObject.AddComponent<RawImage>();
        sheet.raycastTarget = true;
        Sprite note = DetectiveUITheme.NoteCard;
        if (note != null)
        {
            sheet.texture = note.texture;
            sheet.uvRect = new Rect(0.2f, 0.232f, 0.55f, 0.54f);
            sheet.color = new Color(1f, 0.97f, 0.92f, 1f);
        }
        else sheet.color = DetectiveUITheme.AgedPaper;

        const float left = -205f;
        const float width = 410f;

        NewText("Title", _notebook, "สมุดบันทึกนักสืบ", thaiFontBold, 34, Ink, TextAnchor.MiddleLeft,
            new Vector2(left, 415f), new Vector2(width, 50f), new Vector2(0f, 0.5f), false);
        NewText("Subtitle", _notebook, "CASE FILE 1888/JR  ·  CIPHER NOTES", _typewriterFont, 16, InkMuted, TextAnchor.MiddleLeft,
            new Vector2(left, 380f), new Vector2(width, 24f), new Vector2(0f, 0.5f), false);
        MakeRule(_notebook, new Vector2(0f, 358f), new Vector2(width, 2.5f), 0.55f);

        // สาส์นของมอริอาร์ตี้
        NewText("MessageHeader", _notebook, "สาส์นของมอริอาร์ตี้", thaiFontBold, 23, BloodRed, TextAnchor.MiddleLeft,
            new Vector2(left, 325f), new Vector2(width, 32f), new Vector2(0f, 0.5f), false);
        _messageText = NewText("Message", _notebook, "", thaiFont, 21, Ink, TextAnchor.UpperLeft,
            new Vector2(left, 302f), new Vector2(width, 110f), new Vector2(0f, 1f), true);
        _messageText.fontStyle = FontStyle.Italic;
        _messageText.lineSpacing = 1.12f;

        // ช่องตัวอักษรที่พบ
        NewText("FoundHeader", _notebook, "ตัวอักษรที่พบ", thaiFontBold, 23, Ink, TextAnchor.MiddleLeft,
            new Vector2(left, 170f), new Vector2(220f, 32f), new Vector2(0f, 0.5f), false);
        _foundProgressText = NewText("FoundProgress", _notebook, "", thaiFont, 20, InkMuted, TextAnchor.MiddleRight,
            new Vector2(205f, 170f), new Vector2(190f, 30f), new Vector2(1f, 0.5f), false);
        BuildSlotRow("Found", new Vector2(0f, 110f), _foundSlots, _foundSlotBoxes, true);

        // ข้อความแนะนำช่วงส่องหา
        _instructionText = NewText("Instruction", _notebook, "ส่องหน้าหนังสือพิมพ์ด้วยแว่นขยาย\n\nรอยหมึกสีเลือดจะปรากฏใต้ตัวอักษรบางตัวที่ถูกซ่อนไว้ เลื่อนแว่นไปบนรอยเปื้อนจาง ๆ แล้วคลิกเพื่อวงตัวอักษรเก็บลงสมุด\n\nตัวเลขเล็ก ๆ ข้างตัวอักษรคือลำดับในรหัส",
            thaiFont, 21, Ink, TextAnchor.UpperLeft, new Vector2(left, 50f), new Vector2(width, 330f), new Vector2(0f, 1f), true);
        _instructionText.lineSpacing = 1.12f;

        // ส่วนของช่วงถอดรหัส
        _decodeSection = new GameObject("DecodeSection", typeof(RectTransform));
        _decodeSection.transform.SetParent(_notebook, false);
        var dsRect = (RectTransform)_decodeSection.transform;
        dsRect.anchorMin = Vector2.zero;
        dsRect.anchorMax = Vector2.one;
        dsRect.offsetMin = Vector2.zero;
        dsRect.offsetMax = Vector2.zero;

        NewText("DecodedHeader", dsRect, "ผลถอดรหัส", thaiFontBold, 23, Ink, TextAnchor.MiddleLeft,
            new Vector2(left, 30f), new Vector2(220f, 32f), new Vector2(0f, 0.5f), false);
        BuildSlotRow("Decoded", new Vector2(0f, -30f), _decodedSlots, _decodedSlotBoxes, false, dsRect);
        _shiftLabel = NewText("ShiftLabel", dsRect, "", _typewriterFont, 19, InkMuted, TextAnchor.MiddleCenter,
            new Vector2(0f, -92f), new Vector2(width, 28f), null, false);

        _feedbackText = NewText("Feedback", dsRect, "", thaiFont, 21, Ink, TextAnchor.UpperLeft,
            new Vector2(left, -125f), new Vector2(width, 110f), new Vector2(0f, 1f), true);
        _feedbackText.lineSpacing = 1.1f;

        // ปุ่มขอคำใบ้
        var hintRect = NewRect("Btn_Hint", dsRect, new Vector2(0f, -330f), new Vector2(width - 20f, 54f), null);
        _hintButtonImage = hintRect.gameObject.AddComponent<Image>();
        _hintButtonImage.color = Ink;
        _hintButton = hintRect.gameObject.AddComponent<Button>();
        var colors = _hintButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.5f, 0.8f, 0.8f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.colorMultiplier = 1f;
        _hintButton.colors = colors;
        _hintButton.onClick.AddListener(OnHintPressed);
        _hintButtonLabel = NewText("Label", hintRect, "ขอคำใบ้  (0/3)", thaiFontBold, 24, DetectiveUITheme.AgedPaper, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(width, 54f), null, false);
        _decodeSection.SetActive(false);

        _bottomHint = NewText("BottomHint", _notebook, "", _typewriterFont, 15, InkMuted, TextAnchor.MiddleCenter,
            new Vector2(0f, -432f), new Vector2(width + 40f, 40f), null, true);
    }

    private void BuildSlotRow(string name, Vector2 center, Text[] letters, Image[] boxes, bool showNumbers, RectTransform parent = null)
    {
        Transform p = parent != null ? parent : _notebook;
        const float slotW = 52f;
        const float gap = 8f;
        float total = 7 * slotW + 6 * gap;
        for (int i = 0; i < 7; i++)
        {
            float x = center.x - total * 0.5f + slotW * 0.5f + i * (slotW + gap);
            var box = NewImage(name + "Box_" + i, p, new Vector2(x, center.y), new Vector2(slotW, 62f), new Color(0.14f, 0.10f, 0.07f, 0.12f));
            box.raycastTarget = false;
            var frameRect = NewRect("Frame", box.transform, Vector2.zero, new Vector2(slotW, 62f), null);
            var frame = frameRect.gameObject.AddComponent<UIParallelogramGraphic>();
            frame.skewAmount = 0f;
            frame.outlineWidth = 2f;
            frame.outlineColor = new Color(Ink.r, Ink.g, Ink.b, 0.55f);
            frame.fillColor = new Color(0f, 0f, 0f, 0f);
            frame.raycastTarget = false;

            var letter = NewText("Letter", box.transform, "", thaiFontBold, 40, showNumbers ? BloodRed : Ink, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(slotW, 62f), null, false);
            letters[i] = letter;
            boxes[i] = box;

            if (showNumbers)
            {
                NewText("No", box.transform, (i + 1).ToString(), _typewriterFont, 13, InkMuted, TextAnchor.MiddleCenter,
                    new Vector2(0f, -43f), new Vector2(slotW, 18f), null, false);
            }
        }
    }

    private void BuildDisk()
    {
        _disk = NewRect("CipherDisk", _root.transform, PaperPos, new Vector2(900f, 900f), null);
        _diskGroup = _disk.gameObject.AddComponent<CanvasGroup>();

        var shadow = NewImage("Shadow", _disk, new Vector2(14f, -18f), new Vector2(800f, 800f), new Color(0f, 0f, 0f, 0.55f));
        shadow.sprite = DetectiveUIShapes.Circle;
        shadow.raycastTarget = false;
        var plate = NewImage("Plate", _disk, Vector2.zero, new Vector2(800f, 800f), new Color(0.16f, 0.11f, 0.065f, 1f));
        plate.sprite = DetectiveUIShapes.Circle;
        plate.raycastTarget = false;
        var plateRing = NewImage("PlateRing", _disk, Vector2.zero, new Vector2(800f, 800f), Brass);
        plateRing.sprite = DetectiveUIShapes.Ring(0.03f);
        plateRing.raycastTarget = false;
        var plateRing2 = NewImage("PlateRing2", _disk, Vector2.zero, new Vector2(690f, 690f), new Color(Brass.r, Brass.g, Brass.b, 0.5f));
        plateRing2.sprite = DetectiveUIShapes.Ring(0.008f);
        plateRing2.raycastTarget = false;

        // ตัวอักษรวงนอก (ตายตัว) = ตัวอักษรในรหัสที่พบ
        for (int i = 0; i < 26; i++)
        {
            float a = i * AlphabetStep;
            Vector2 pos = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * 335f;
            char c = (char)('A' + i);
            bool inCipher = CIPHER_TEXT.IndexOf(c) >= 0;
            var t = NewText("Outer_" + c, _disk, c.ToString(), thaiFontBold, 38, inCipher ? new Color(1f, 0.82f, 0.55f, 1f) : DetectiveUITheme.AgedPaper,
                TextAnchor.MiddleCenter, pos, new Vector2(52f, 52f), null, false);
            t.rectTransform.localEulerAngles = new Vector3(0f, 0f, -a);

            if (inCipher)
            {
                var dot = NewImage("Dot_" + c, _disk, new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * 372f, new Vector2(11f, 11f), BloodRed);
                dot.sprite = DetectiveUIShapes.Circle;
                dot.raycastTarget = false;
            }
        }

        // วงใน (หมุนได้) = ตัวอักษรที่ถอดรหัสแล้ว
        _innerRing = NewRect("InnerRing", _disk, Vector2.zero, new Vector2(600f, 600f), null);
        var disc = NewImage("Disc", _innerRing, Vector2.zero, new Vector2(600f, 600f), new Color(0.085f, 0.06f, 0.04f, 1f));
        disc.sprite = DetectiveUIShapes.Circle;
        disc.raycastTarget = true; // รับการลากหมุน
        var discRing = NewImage("DiscRing", _innerRing, Vector2.zero, new Vector2(600f, 600f), new Color(Brass.r, Brass.g, Brass.b, 0.8f));
        discRing.sprite = DetectiveUIShapes.Ring(0.02f);
        discRing.raycastTarget = false;

        for (int i = 0; i < 26; i++)
        {
            float a = i * AlphabetStep;
            Vector2 pos = new Vector2(Mathf.Sin(a * Mathf.Deg2Rad), Mathf.Cos(a * Mathf.Deg2Rad)) * 250f;
            var t = NewText("Inner_" + (char)('A' + i), _innerRing, ((char)('A' + i)).ToString(), thaiFontBold, 34, Brass,
                TextAnchor.MiddleCenter, pos, new Vector2(48f, 48f), null, false);
            t.rectTransform.localEulerAngles = new Vector3(0f, 0f, -a);
        }

        var drag = disc.gameObject.AddComponent<CipherWheelDragHandler>();
        drag.onBegin = OnWheelDragBegin;
        drag.onDrag = OnWheelDrag;

        // แกนกลาง (ไม่หมุน) แสดงค่าคีย์เลื่อน
        var hub = NewImage("Hub", _disk, Vector2.zero, new Vector2(250f, 250f), new Color(0.12f, 0.085f, 0.055f, 1f));
        hub.sprite = DetectiveUIShapes.Circle;
        hub.raycastTarget = false;
        var hubRing = NewImage("HubRing", _disk, Vector2.zero, new Vector2(250f, 250f), Brass);
        hubRing.sprite = DetectiveUIShapes.Ring(0.04f);
        hubRing.raycastTarget = false;
        NewText("HubTitle", _disk, "CAESAR", _typewriterFont, 24, new Color(Brass.r, Brass.g, Brass.b, 0.85f), TextAnchor.MiddleCenter,
            new Vector2(0f, 62f), new Vector2(240f, 30f), null, false);
        _hubShiftText = NewText("HubShift", _disk, "0", thaiFontBold, 64, DetectiveUITheme.AgedPaper, TextAnchor.MiddleCenter,
            new Vector2(0f, 4f), new Vector2(240f, 80f), null, false);
        NewText("HubSub", _disk, "SHIFT", _typewriterFont, 18, new Color(Brass.r, Brass.g, Brass.b, 0.7f), TextAnchor.MiddleCenter,
            new Vector2(0f, -52f), new Vector2(240f, 24f), null, false);

        // ตราประทับเฉลย
        _solvedStamp = NewText("SolvedStamp", _disk, "THEATER", thaiFontBold, 96, new Color(BloodRed.r, BloodRed.g, BloodRed.b, 0f), TextAnchor.MiddleCenter,
            new Vector2(0f, -470f), new Vector2(700f, 120f), null, false);
        _solvedStamp.rectTransform.localEulerAngles = new Vector3(0f, 0f, -6f);
        _solvedStamp.fontStyle = FontStyle.Bold;
        _solvedStamp.gameObject.SetActive(false);

        _disk.gameObject.SetActive(false);
    }

    private void OnWheelDragBegin(Vector2 screenPos)
    {
        if (_phase != Phase.Decode) return;
        _dragStartAngle = AngleOnWheel(screenPos);
        _dragStartShift = _shift;
    }

    private void OnWheelDrag(Vector2 screenPos)
    {
        if (_phase != Phase.Decode) return;
        float delta = Mathf.DeltaAngle(_dragStartAngle, AngleOnWheel(screenPos));
        SetShift(_dragStartShift + Mathf.RoundToInt(delta / AlphabetStep));
    }

    private float AngleOnWheel(Vector2 screenPos)
    {
        Vector2 center = RectTransformUtility.WorldToScreenPoint(null, _innerRing.position);
        Vector2 dir = screenPos - center;
        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
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

    private static Image NewImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var r = NewRect(name, parent, pos, size, null);
        var img = r.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Image StretchImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
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
}

/// <summary>ส่งตำแหน่งเมาส์ตอนลากบนจานซีซาร์ให้มินิเกมคำนวณมุมหมุน</summary>
public class CipherWheelDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public Action<Vector2> onBegin;
    public Action<Vector2> onDrag;

    public void OnBeginDrag(PointerEventData eventData)
    {
        onBegin?.Invoke(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        onDrag?.Invoke(eventData.position);
    }
}
