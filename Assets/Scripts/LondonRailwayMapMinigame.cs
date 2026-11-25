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
/// มินิเกมบทที่ 3: ไล่รอยตั๋วรถไฟบนแผนที่รถไฟลอนดอน 1888 (London Railway Map Deduction)
/// ตั๋วที่ Jack ทำตกฉีกขาด เหลือเบาะแส 4 ข้อ: ออก 23:45 น. / ขบวนด่วน / มุ่งหน้าทิศเหนือ / ต่อ Circle Line
/// ผู้เล่นเลือกสถานีบนแผนที่ เปิดตารางเดินรถ แล้วกด "ตรวจสอบกับตั๋ว" ระบบจะบอกว่าเบาะแสข้อไหนตรงหรือไม่ตรง
/// ไม่มีสถานีไหนที่เบาะแสข้อเดียวชี้ตัวได้ ต้องไล่คัดออกทีละข้อ จนเหลือ "คิงส์ครอส" สถานีเดียวที่ตรงทั้ง 4 ข้อ
/// นาฬิกาพกบนแผนที่เดินต่อเนื่องจนถึง 23:45 ถ้าตรวจไม่ทันขบวนจะออกไปก่อน (ไม่แพ้ แต่คะแนนความแม่นยำจะลดลง)
/// </summary>
public class LondonRailwayMapMinigame : MonoBehaviour
{
    private static LondonRailwayMapMinigame _instance;
    public static LondonRailwayMapMinigame Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<LondonRailwayMapMinigame>();
            return _instance;
        }
        private set { _instance = value; }
    }

    [Header("=== Fonts & Sprites ===")]
    public Font thaiFont;
    public Font thaiFontBold;
    public Sprite parchmentPaperSprite;

    public bool IsMinigameActive => _root != null && _root.activeSelf;
    public Action OnPuzzleSolved;
    /// <summary>จำนวนครั้งที่กดตรวจสอบในรอบล่าสุด</summary>
    public int Attempts { get; private set; }
    /// <summary>ผู้เล่นตรวจไม่ทันก่อนขบวน 23:45 ออกหรือไม่</summary>
    public bool MissedTrain { get; private set; }

    // ----- ข้อมูลสถานี -----
    private class StationData
    {
        public string id;
        public string nameThai;
        public string nameEng;
        public string lineShort;
        public Color lineColor;
        public Vector2 mapPos;
        public Vector2 labelOffset;
        public string lastTrain;
        public string serviceType;
        public string info;
        // เบาะแส 4 ข้อจากตั๋ว
        public bool departs2345;
        public bool express;
        public bool north;
        public bool circle;
    }

    private static readonly string[] ClueTexts =
    {
        "ออกเดินทางเวลา 23:45 น.",
        "เป็นขบวนด่วน (EXPRESS)",
        "มุ่งหน้าทิศเหนือ (NORTHERN)",
        "ต่อ Circle Line ได้"
    };

    private static readonly string[] ClueFailTexts =
    {
        "เวลาไม่ใช่ 23:45",
        "ไม่ใช่ขบวนด่วน",
        "ไม่ได้มุ่งหน้าทิศเหนือ",
        "ไม่ต่อ Circle Line"
    };

    private readonly List<StationData> _stations = new List<StationData>
    {
        new StationData
        {
            id = "paddington", nameThai = "แพดดิงตัน", nameEng = "Paddington", lineShort = "Great Western",
            lineColor = new Color(0.20f, 0.62f, 0.32f), mapPos = new Vector2(-739f, 87f), labelOffset = new Vector2(0f, 62f),
            lastTrain = "22:30", serviceType = "ด่วน (Express 402)",
            info = "ชุมทางมุ่งหน้าแคว้นเวลส์ ทิศตะวันตก มีชุมทาง Circle Line",
            departs2345 = false, express = true, north = false, circle = true
        },
        new StationData
        {
            id = "waterloo", nameThai = "วอเตอร์ลู", nameEng = "Waterloo", lineShort = "London & South Western",
            lineColor = new Color(0.18f, 0.52f, 0.60f), mapPos = new Vector2(-427f, -24f), labelOffset = new Vector2(0f, -62f),
            lastTrain = "23:45", serviceType = "ด่วน (Express 612)",
            info = "ปลายทางสายตะวันตกเฉียงใต้ ข้ามแม่น้ำเทมส์ ทิศใต้ มีชุมทาง Circle Line",
            departs2345 = true, express = true, north = false, circle = true
        },
        new StationData
        {
            id = "kings_cross", nameThai = "คิงส์ครอส", nameEng = "King's Cross", lineShort = "Great Northern",
            lineColor = new Color(0.16f, 0.42f, 0.82f), mapPos = new Vector2(-384f, 211f), labelOffset = new Vector2(0f, -62f),
            lastTrain = "23:45", serviceType = "ด่วน (Express 901)",
            info = "ปลายทางสายเหนือ ชุมทางเชื่อม Circle Line",
            departs2345 = true, express = true, north = true, circle = true
        },
        new StationData
        {
            id = "whitechapel", nameThai = "ไวท์ชาเปล", nameEng = "Whitechapel", lineShort = "East London",
            lineColor = new Color(0.55f, 0.42f, 0.30f), mapPos = new Vector2(-233f, 64f), labelOffset = new Vector2(0f, -62f),
            lastTrain = "ปิด 22:00", serviceType = "ไม่มีขบวนด่วน",
            info = "ย่านที่เกิดเหตุก่อนหน้า ทิศตะวันออก มีชุมทาง Circle Line",
            departs2345 = false, express = false, north = false, circle = true
        },
        new StationData
        {
            id = "victoria", nameThai = "วิกตอเรีย", nameEng = "Victoria", lineShort = "LB & South Coast",
            lineColor = new Color(0.85f, 0.64f, 0.14f), mapPos = new Vector2(-581f, -79f), labelOffset = new Vector2(-135f, -6f),
            lastTrain = "23:15", serviceType = "ด่วน (Express 509)",
            info = "ปลายทางสายใต้ ข้ามแม่น้ำเทมส์ ทิศใต้ มีชุมทาง Circle Line",
            departs2345 = false, express = true, north = false, circle = true
        }
    };

    private static readonly Vector2 MapPos = Vector2.zero;
    private static readonly Vector2 MapSize = new Vector2(1920f, 1072f);
    private static readonly Vector2 PanelPos = new Vector2(505f, -8f);
    private static readonly Vector2 PanelSize = new Vector2(560f, 960f);

    private Texture2D _spreadTex;
    private Texture2D _paperTex;

    private static readonly Color Ink = new Color(0.14f, 0.10f, 0.07f, 1f);
    private static readonly Color InkMuted = new Color(0.30f, 0.21f, 0.14f, 1f);
    private static readonly Color StampRed = new Color(0.68f, 0.10f, 0.08f, 1f);
    private static readonly Color Brass = new Color(0.86f, 0.70f, 0.36f, 1f);
    private static readonly Color GreenInk = new Color(0.10f, 0.42f, 0.20f, 1f);
    private static readonly Color PaperPlate = new Color(0.96f, 0.93f, 0.85f, 0.92f);

    // ----- UI -----
    private GameObject _root;
    private CanvasGroup _rootGroup;
    private Font _typewriterFont;
    private RectTransform _map;
    private RectTransform _panel;
    private Text _clockText;
    private Text _clockCaption;
    private Image _clockRing;
    private Text _stationNameText;
    private Text _stationInfoText;
    private Text _feedbackText;
    private Text _attemptsText;
    private Button _checkButton;
    private Image _checkButtonImage;
    private GameObject _timetable;
    private Text _stampText;
    private readonly Text[] _clueResultTexts = new Text[4];
    private readonly Image[] _clueBullets = new Image[4];
    private readonly Dictionary<string, StationView> _views = new Dictionary<string, StationView>();

    private class StationView
    {
        public StationData data;
        public RectTransform root;
        public Image highlight;
        public Image plate;
        public Text nameThai;
        public bool hovered;
        public bool revealed;
    }

    // ----- สถานะ -----
    private string _selectedId = "";
    private bool _solved;
    private float _clockMinutes;
    private bool _wasWaypointVisible;

    private const float StartMinutes = 23f * 60f + 8f;
    private const float DepartMinutes = 23f * 60f + 45f;
    private const float RealSecondsPerGameMinute = 6f;

    // ==========================================
    // Lifecycle
    // ==========================================
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
        if (thaiFont == null) thaiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (thaiFontBold == null) thaiFontBold = thaiFont;
        _typewriterFont = Font.CreateDynamicFontFromOSFont(new[] { "Courier New", "Consolas" }, 24);
        if (_typewriterFont == null) _typewriterFont = thaiFontBold;

        BuildMinigameUI();
    }

    /// <summary>สร้าง UI ใหม่ (สคริปต์ Editor เรียกใช้ได้) และลบแผงเก่าที่ถูกเซฟค้างในฉากทิ้ง</summary>
    public void BuildMinigameUI()
    {
        if (Application.isPlaying)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t != null && t.name == "LondonRailwayMap_Panel" && t.gameObject.scene.IsValid()) Destroy(t.gameObject);
            }
        }

        if (_root != null)
        {
            if (Application.isPlaying) Destroy(_root);
            else DestroyImmediate(_root);
        }

        _root = new GameObject("RailwayMap_v2_Canvas", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 958;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        _root.AddComponent<GraphicRaycaster>();
        _rootGroup = _root.AddComponent<CanvasGroup>();

        _spreadTex = GameAssets.Load<Texture2D>("Assets/UI/Map_Train/MapTrain.jpg");
        _paperTex = GameAssets.Load<Texture2D>("Assets/UI/Folder/Gemini_Generated_Image_oht62eoht62eoht6-Photoroom2.png");
        var bg = StretchImage("Backdrop", _root.transform, new Color(0.03f, 0.025f, 0.02f, 1f));
        bg.raycastTarget = true;
        if (_spreadTex != null)
        {
            // แผนที่รถไฟลอนดอน + ตารางเดินรถ (ภาพของผู้ใช้) เต็มจอ
            var art = NewRect("Spread", _root.transform, Vector2.zero, new Vector2(1920f, 1072f), null).gameObject.AddComponent<RawImage>();
            art.texture = _spreadTex;
            art.raycastTarget = false;
        }
        else
        {
            Sprite cork = DetectiveUITheme.CorkboardBg;
            if (cork != null)
            {
                var corkImg = StretchImage("Corkboard", _root.transform, new Color(0.30f, 0.26f, 0.22f, 1f));
                corkImg.sprite = cork;
            }
        }
        var vignette = StretchImage("Vignette", _root.transform, new Color(1f, 1f, 1f, 0.55f));
        vignette.sprite = DetectiveUIShapes.Vignette;

        _views.Clear();
        BuildMap();
        BuildPanel();
        BuildTimetable();

        _root.SetActive(false);
    }

    private void Update()
    {
        if (!IsMinigameActive) return;

        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        float dt = Time.unscaledDeltaTime;

        // นาฬิกาพกเดินต่อเนื่องจนถึงเวลาขบวนออก
        if (!_solved)
        {
            _clockMinutes = Mathf.Min(23f * 60f + 59f, _clockMinutes + dt / RealSecondsPerGameMinute);
            RefreshClock();
        }

        // สถานี: เลือกแล้วเต้นเป็นจังหวะ / เมาส์ชี้แล้วขยายเล็กน้อย
        foreach (var kvp in _views)
        {
            var view = kvp.Value;
            bool selected = kvp.Key == _selectedId;
            float targetScale = !view.revealed ? 0f : (selected ? 1.18f : (view.hovered ? 1.1f : 1f));
            float s = Mathf.Lerp(view.root.localScale.x, targetScale, 1f - Mathf.Exp(-14f * dt));
            view.root.localScale = Vector3.one * s;

            if (selected)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
                view.highlight.color = new Color(Brass.r, Brass.g, Brass.b, pulse);
                view.highlight.rectTransform.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(Time.unscaledTime * 5f));
                view.plate.color = new Color(1f, 0.97f, 0.88f, 0.98f);
            }
            else
            {
                view.highlight.color = new Color(Brass.r, Brass.g, Brass.b, 0f);
                view.plate.color = PaperPlate;
            }
        }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame) ToggleTimetableModal();
#else
        if (Input.GetKeyDown(KeyCode.T)) ToggleTimetableModal();
#endif
    }

    // ==========================================
    // Open / Close
    // ==========================================
    public void OpenMinigame(Action onSolvedCallback = null)
    {
        if (CinematicLetterbox.Instance != null) CinematicLetterbox.Instance.ForceDisableLetterbox();

        if (RoyalLyceumTheaterQuestManager.Instance != null) RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(false);
        if (LondonAlleyQuestManager.Instance != null) LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
        if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
        DetectiveThoughtToast.Clear();

        CiraCoreVisionScanner.IsScannerBlocked = true;
        if (CiraCoreVisionScanner.Instance != null) CiraCoreVisionScanner.Instance.ForceCloseScanMode();

        if (WaypointNavigationSystem.Instance != null)
        {
            _wasWaypointVisible = WaypointNavigationSystem.Instance.IsVisible;
            WaypointNavigationSystem.Instance.SetVisible(false);
        }

        OnPuzzleSolved = onSolvedCallback;
        ResetState();

        _root.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(OpenAnimRoutine());

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        var starterInputs = FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
        if (starterInputs != null)
        {
            starterInputs.cursorLocked = false;
            starterInputs.cursorInputForLook = false;
        }

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

        if (RoyalLyceumTheaterQuestManager.Instance != null && RoyalLyceumTheaterQuestManager.Instance.IsQuest3Active)
        {
            RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(true);
        }

        var starterInputs = FindAnyObjectByType<StarterAssets.StarterAssetsInputs>();
        if (starterInputs != null)
        {
            starterInputs.cursorLocked = true;
            starterInputs.cursorInputForLook = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (CinematicCutsceneManager.Instance != null) CinematicCutsceneManager.Instance.SetPlayerControlsLocked(false);
    }

    private void ResetState()
    {
        _solved = false;
        _selectedId = "";
        Attempts = 0;
        MissedTrain = false;
        _clockMinutes = StartMinutes;

        foreach (var kvp in _views)
        {
            kvp.Value.highlight.color = new Color(Brass.r, Brass.g, Brass.b, 0f);
            kvp.Value.revealed = false;
            kvp.Value.root.localScale = Vector3.zero;
        }
        _timetable.SetActive(false);
        _stampText.gameObject.SetActive(false);

        _stationNameText.text = "ยังไม่ได้เลือกสถานี";
        _stationInfoText.text = "คลิกสถานีบนแผนที่ แล้วเทียบกับเบาะแสบนตั๋ว เปิดตารางเดินรถด้วย [T]";
        _feedbackText.text = "";
        _attemptsText.text = "ตรวจสอบแล้ว  0  ครั้ง";
        ClearClueResults();
        SetCheckEnabled(false);
        RefreshClock();
    }

    private IEnumerator OpenAnimRoutine()
    {
        _rootGroup.alpha = 0f;
        _map.anchoredPosition = MapPos;
        _panel.anchoredPosition = PanelPos + new Vector2(60f, 0f);

        float t = 0f;
        const float duration = 0.45f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            _rootGroup.alpha = k;
            _map.anchoredPosition = MapPos;
            _panel.anchoredPosition = Vector2.Lerp(PanelPos + new Vector2(60f, 0f), PanelPos, k);
            yield return null;
        }
        _rootGroup.alpha = 1f;

        // สถานีผุดขึ้นทีละแห่ง (ใช้ Update ไล่สเกลเข้าหา 1)
        foreach (var kvp in _views)
        {
            kvp.Value.revealed = true;
            yield return new WaitForSecondsRealtime(0.07f);
        }
    }

    // ==========================================
    // Interaction
    // ==========================================
    private void SelectStation(string id)
    {
        if (_solved) return;
        if (!_views.TryGetValue(id, out var view)) return;

        _selectedId = id;

        var d = view.data;
        _stationNameText.text = $"{d.nameThai}  <size=20><color=#5A4230>{d.nameEng}</color></size>";
        _stationInfoText.text = $"สาย {d.lineShort}\nเที่ยวสุดท้าย <b>{d.lastTrain}</b>  ·  {d.serviceType}\n{d.info}";
        _feedbackText.text = "";
        ClearClueResults();
        SetCheckEnabled(true);
    }

    private void OnCheckPressed()
    {
        if (_solved || string.IsNullOrEmpty(_selectedId)) return;
        if (!_views.TryGetValue(_selectedId, out var view)) return;

        Attempts++;
        _attemptsText.text = $"ตรวจสอบแล้ว  {Attempts}  ครั้ง";

        var d = view.data;
        bool[] results = { d.departs2345, d.express, d.north, d.circle };
        int passed = 0;
        var failed = new List<string>();
        for (int i = 0; i < 4; i++)
        {
            bool ok = results[i];
            _clueResultTexts[i].text = ok ? "ตรง" : "ไม่ตรง";
            _clueResultTexts[i].color = ok ? GreenInk : StampRed;
            _clueBullets[i].color = ok ? GreenInk : StampRed;
            if (ok) passed++;
            else failed.Add(ClueFailTexts[i]);
        }

        if (passed == 4)
        {
            StartCoroutine(SolvedRoutine());
            return;
        }

        _feedbackText.color = StampRed;
        _feedbackText.text = $"<b>ยังไม่ใช่ {d.nameThai}</b>  ตรง {passed} จาก 4 ข้อ\n{string.Join("  ·  ", failed)}";
    }

    private void ToggleTimetableModal()
    {
        if (_timetable != null) _timetable.SetActive(!_timetable.activeSelf);
    }

    private void ClearClueResults()
    {
        for (int i = 0; i < 4; i++)
        {
            _clueResultTexts[i].text = "";
            _clueBullets[i].color = new Color(Ink.r, Ink.g, Ink.b, 0.45f);
        }
    }

    private void SetCheckEnabled(bool enabled)
    {
        _checkButton.interactable = enabled;
        _checkButtonImage.color = enabled ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.6f);
    }

    private void RefreshClock()
    {
        int total = Mathf.FloorToInt(_clockMinutes);
        _clockText.text = $"{total / 60:00}:{total % 60:00}";

        bool late = _clockMinutes >= DepartMinutes;
        if (late && !MissedTrain && !_solved)
        {
            MissedTrain = true;
        }

        _clockText.color = late ? new Color(1f, 0.45f, 0.4f, 1f) : Brass;
        _clockRing.color = late ? StampRed : Brass;
        _clockCaption.text = late ? "ขบวน 23:45 ออกไปแล้ว" : $"ขบวนด่วนออก 23:45  (อีก {Mathf.CeilToInt(DepartMinutes - _clockMinutes)} นาที)";
        _clockCaption.color = late ? new Color(1f, 0.5f, 0.45f, 1f) : new Color(Brass.r, Brass.g, Brass.b, 0.9f);
    }

    private IEnumerator SolvedRoutine()
    {
        _solved = true;
        SetCheckEnabled(false);

        _feedbackText.color = GreenInk;
        _feedbackText.text = "<b>ตรงทั้ง 4 ข้อ</b>\nแจ็คกำลังจะขึ้นขบวนด่วน 23:45 จาก King's Cross";

        _stampText.gameObject.SetActive(true);
        var stampRect = _stampText.rectTransform;
        float t = 0f;
        while (t < 0.22f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.22f);
            stampRect.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, k);
            _stampText.color = new Color(StampRed.r, StampRed.g, StampRed.b, 0.9f * k);
            yield return null;
        }

        yield return new WaitForSecondsRealtime(2.6f);
        yield return FadeGroup(_rootGroup, 1f, 0f, 0.3f);

        CloseMinigame();
        OnPuzzleSolved?.Invoke();
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
    // UI: แผนที่
    // ==========================================
    private void BuildMap()
    {
        // แผนที่จริงมาจากภาพของผู้ใช้ ที่นี่วางเฉพาะจุดกดสถานี นาฬิกา และตราประทับเฉลยทับบนภาพ
        _map = NewRect("Map", _root.transform, MapPos, MapSize, null);

        foreach (var st in _stations) BuildStation(st);

        BuildClock();

        _stampText = NewText("MapStamp", _map, "KING'S CROSS  23:45", thaiFontBold, 66, new Color(StampRed.r, StampRed.g, StampRed.b, 0f), TextAnchor.MiddleCenter,
            new Vector2(-330f, -20f), new Vector2(860f, 100f), null, false);
        _stampText.rectTransform.localEulerAngles = new Vector3(0f, 0f, -8f);
        _stampText.fontStyle = FontStyle.Bold;
        _stampText.gameObject.SetActive(false);
    }

    private void AddFrame(RectTransform parent, Vector2 size, float width, float alpha)
    {
        var rect = NewRect("Frame", parent, Vector2.zero, size, null);
        var frame = rect.gameObject.AddComponent<UIParallelogramGraphic>();
        frame.skewAmount = 0f;
        frame.outlineWidth = width;
        frame.outlineColor = new Color(Ink.r, Ink.g, Ink.b, alpha);
        frame.fillColor = new Color(0f, 0f, 0f, 0f);
        frame.raycastTarget = false;
    }

    private void DrawRiver(Vector2[] points, float thickness, Color color)
    {
        for (int i = 0; i < points.Length - 1; i++) MakeSegment(_map, points[i], points[i + 1], thickness, color);
        foreach (var p in points)
        {
            var joint = NewImage("RiverJoint", _map, p, new Vector2(thickness, thickness), color);
            joint.sprite = DetectiveUIShapes.Circle;
            joint.raycastTarget = false;
        }
    }

    private void DrawLine(Vector2 a, Vector2 b, Color color)
    {
        MakeSegment(_map, a, b, 13f, new Color(Ink.r, Ink.g, Ink.b, 0.78f));
        MakeSegment(_map, a, b, 8f, color);
    }

    private static Image MakeSegment(RectTransform parent, Vector2 a, Vector2 b, float thickness, Color color)
    {
        var img = NewImage("Segment", parent, (a + b) * 0.5f, new Vector2((b - a).magnitude, thickness), color);
        img.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        img.raycastTarget = false;
        return img;
    }

    private void BuildStation(StationData d)
    {
        var rect = NewRect("Station_" + d.id, _map, d.mapPos, new Vector2(110f, 110f), null);
        var hit = rect.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        var btn = rect.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = hit;
        string id = d.id;
        btn.onClick.AddListener(() => SelectStation(id));

        var highlight = NewImage("Highlight", rect, Vector2.zero, new Vector2(96f, 96f), new Color(Brass.r, Brass.g, Brass.b, 0f));
        highlight.sprite = DetectiveUIShapes.Ring(0.09f);
        highlight.raycastTarget = false;

        var shadow = NewImage("Shadow", rect, new Vector2(3f, -4f), new Vector2(54f, 54f), new Color(0f, 0f, 0f, 0.35f));
        shadow.sprite = DetectiveUIShapes.Circle;
        shadow.raycastTarget = false;
        var outline = NewImage("Outline", rect, Vector2.zero, new Vector2(54f, 54f), Ink);
        outline.sprite = DetectiveUIShapes.Circle;
        outline.raycastTarget = false;
        var color = NewImage("Color", rect, Vector2.zero, new Vector2(46f, 46f), d.lineColor);
        color.sprite = DetectiveUIShapes.Circle;
        color.raycastTarget = false;
        var inner = NewImage("Inner", rect, Vector2.zero, new Vector2(25f, 25f), new Color(0.98f, 0.96f, 0.9f, 1f));
        inner.sprite = DetectiveUIShapes.Circle;
        inner.raycastTarget = false;

        // ป้ายชื่อสถานีบนแผ่นกระดาษ (อ่านง่ายแม้ทับเส้น)
        var plate = NewImage("LabelPlate", rect, d.labelOffset, new Vector2(196f, 54f), PaperPlate);
        plate.raycastTarget = false;
        var plateFrame = NewRect("PlateFrame", plate.transform, Vector2.zero, new Vector2(196f, 54f), null);
        var pf = plateFrame.gameObject.AddComponent<UIParallelogramGraphic>();
        pf.skewAmount = 0f;
        pf.outlineWidth = 1.5f;
        pf.outlineColor = new Color(Ink.r, Ink.g, Ink.b, 0.55f);
        pf.fillColor = new Color(0f, 0f, 0f, 0f);
        pf.raycastTarget = false;

        var nameThai = NewText("NameThai", plate.transform, d.nameThai, thaiFontBold, 26, Ink, TextAnchor.MiddleCenter,
            new Vector2(0f, 8f), new Vector2(190f, 32f), null, false);
        NewText("NameEng", plate.transform, d.nameEng.ToUpper(), _typewriterFont, 14, new Color(0.25f, 0.18f, 0.12f, 1f), TextAnchor.MiddleCenter,
            new Vector2(0f, -14f), new Vector2(190f, 18f), null, false);

        var hover = hit.gameObject.AddComponent<StationHoverHandler>();
        hover.onEnter = () => { if (_views.TryGetValue(id, out var v)) v.hovered = true; };
        hover.onExit = () => { if (_views.TryGetValue(id, out var v)) v.hovered = false; };

        _views[d.id] = new StationView { data = d, root = rect, highlight = highlight, plate = plate, nameThai = nameThai };
    }

    private void BuildLegend()
    {
        var entries = new[]
        {
            new KeyValuePair<Color, string>(new Color(0.16f, 0.42f, 0.82f), "Great Northern  (สายเหนือ)"),
            new KeyValuePair<Color, string>(new Color(0.20f, 0.62f, 0.32f), "Great Western  (สายตะวันตก)"),
            new KeyValuePair<Color, string>(new Color(0.58f, 0.25f, 0.70f), "Metropolitan / Circle Line"),
            new KeyValuePair<Color, string>(new Color(0.85f, 0.64f, 0.14f), "LB & South Coast  (สายใต้)"),
            new KeyValuePair<Color, string>(new Color(0.82f, 0.34f, 0.24f), "South Eastern  (ตะวันออกเฉียงใต้)")
        };

        var box = NewImage("LegendBox", _map, new Vector2(-20f, -350f), new Vector2(400f, 160f), PaperPlate);
        box.raycastTarget = false;
        var frameRect = NewRect("LegendFrame", box.transform, Vector2.zero, new Vector2(400f, 160f), null);
        var frame = frameRect.gameObject.AddComponent<UIParallelogramGraphic>();
        frame.skewAmount = 0f;
        frame.outlineWidth = 2.5f;
        frame.outlineColor = new Color(Ink.r, Ink.g, Ink.b, 0.7f);
        frame.fillColor = new Color(0f, 0f, 0f, 0f);
        frame.raycastTarget = false;

        NewText("LegendTitle", box.transform, "สัญลักษณ์เส้นทาง", thaiFontBold, 20, Ink, TextAnchor.MiddleLeft,
            new Vector2(-185f, 62f), new Vector2(300f, 26f), new Vector2(0f, 0.5f), false);
        for (int i = 0; i < entries.Length; i++)
        {
            float y = 32f - i * 24f;
            var bar = NewImage("LegendBar_" + i, box.transform, new Vector2(-170f, y), new Vector2(34f, 8f), entries[i].Key);
            bar.raycastTarget = false;
            NewText("LegendText_" + i, box.transform, entries[i].Value, thaiFont, 18, Ink, TextAnchor.MiddleLeft,
                new Vector2(-145f, y), new Vector2(320f, 22f), new Vector2(0f, 0.5f), false);
        }
    }

    private void BuildClock()
    {
        // ป้ายนาฬิกาพกบนมุมขวาบน (กรอบทองเหลือง พื้นหนังเข้ม) ไม่ทับเส้นทางรถไฟ
        var plate = NewImage("ClockPlate", _map, new Vector2(-560f, -408f), new Vector2(330f, 120f), new Color(0.09f, 0.065f, 0.045f, 1f));
        plate.raycastTarget = false;
        var frameRect = NewRect("ClockFrame", plate.transform, Vector2.zero, new Vector2(330f, 120f), null);
        var frame = frameRect.gameObject.AddComponent<UIParallelogramGraphic>();
        frame.skewAmount = 0f;
        frame.outlineWidth = 4f;
        frame.outlineColor = Brass;
        frame.fillColor = new Color(0f, 0f, 0f, 0f);
        frame.raycastTarget = false;

        var dial = NewImage("Dial", plate.transform, new Vector2(-110f, 4f), new Vector2(84f, 84f), new Color(0.15f, 0.11f, 0.075f, 1f));
        dial.sprite = DetectiveUIShapes.Circle;
        dial.raycastTarget = false;
        _clockRing = NewImage("DialRing", plate.transform, new Vector2(-110f, 4f), new Vector2(84f, 84f), Brass);
        _clockRing.sprite = DetectiveUIShapes.Ring(0.07f);
        _clockRing.raycastTarget = false;
        // เข็มนาฬิกา (ตกแต่ง)
        var hand1 = NewImage("Hand1", plate.transform, new Vector2(-110f, 4f), new Vector2(3f, 30f), Brass);
        hand1.rectTransform.pivot = new Vector2(0.5f, 0f);
        hand1.raycastTarget = false;
        var hand2 = NewImage("Hand2", plate.transform, new Vector2(-110f, 4f), new Vector2(3f, 22f), Brass);
        hand2.rectTransform.pivot = new Vector2(0.5f, 0f);
        hand2.rectTransform.localEulerAngles = new Vector3(0f, 0f, -75f);
        hand2.raycastTarget = false;

        NewText("ClockLabel", plate.transform, "POCKET WATCH", _typewriterFont, 13, new Color(Brass.r, Brass.g, Brass.b, 0.75f), TextAnchor.MiddleLeft,
            new Vector2(-50f, 42f), new Vector2(200f, 18f), new Vector2(0f, 0.5f), false);
        _clockText = NewText("Clock", plate.transform, "23:08", thaiFontBold, 52, Brass, TextAnchor.MiddleLeft,
            new Vector2(-52f, 6f), new Vector2(200f, 64f), new Vector2(0f, 0.5f), false);
        _clockCaption = NewText("ClockCaption", plate.transform, "", thaiFont, 17, Brass, TextAnchor.MiddleLeft,
            new Vector2(-52f, -38f), new Vector2(230f, 22f), new Vector2(0f, 0.5f), false);
    }

    private void BuildCompassRose(Vector2 pos)
    {
        var rose = NewImage("RoseRing", _map, pos, new Vector2(80f, 80f), new Color(Ink.r, Ink.g, Ink.b, 0.85f));
        rose.sprite = DetectiveUIShapes.Ring(0.04f);
        rose.raycastTarget = false;
        NewImage("RoseV", _map, pos, new Vector2(3f, 70f), new Color(Ink.r, Ink.g, Ink.b, 0.85f)).raycastTarget = false;
        NewImage("RoseH", _map, pos, new Vector2(70f, 3f), new Color(Ink.r, Ink.g, Ink.b, 0.5f)).raycastTarget = false;
        var north = NewImage("RoseNorth", _map, pos + new Vector2(0f, 24f), new Vector2(16f, 16f), StampRed);
        north.sprite = WaypointSprites.Chevron;
        north.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
        north.raycastTarget = false;
        NewText("RoseN", _map, "N", thaiFontBold, 22, StampRed, TextAnchor.MiddleCenter, pos + new Vector2(0f, 58f), new Vector2(30f, 28f), null, false);
    }

    // ==========================================
    // UI: แผงขวา (ตั๋ว + เบาะแส + ปุ่ม)
    // ==========================================
    private void BuildPanel()
    {
        _panel = NewRect("Panel", _root.transform, PanelPos, PanelSize, null);
        var shadowRect = NewRect("Shadow", _panel, new Vector2(12f, -16f), PanelSize, null);
        var shadow = shadowRect.gameObject.AddComponent<RawImage>();
        shadow.texture = _paperTex;
        shadow.color = new Color(0f, 0f, 0f, _paperTex != null ? 0.55f : 0.55f);
        shadow.raycastTarget = false;
        var sheetRect = NewRect("Sheet", _panel, Vector2.zero, PanelSize, null);
        var sheet = sheetRect.gameObject.AddComponent<RawImage>();
        sheet.raycastTarget = true;
        Sprite note = DetectiveUITheme.NoteCard;
        if (_paperTex != null)
        {
            sheet.texture = _paperTex;
            sheet.color = new Color(1f, 0.97f, 0.92f, 1f);
        }
        else if (note != null)
        {
            sheet.texture = note.texture;
            sheet.uvRect = new Rect(0.2f, 0.232f, 0.55f, 0.54f);
            sheet.color = new Color(1f, 0.97f, 0.92f, 1f);
        }
        else sheet.color = DetectiveUITheme.AgedPaper;

        const float left = -215f;
        const float width = 430f;

        NewText("Title", _panel, "ตั๋วที่ฉีกขาด", thaiFontBold, 34, Ink, TextAnchor.MiddleLeft,
            new Vector2(left, 392f), new Vector2(width, 48f), new Vector2(0f, 0.5f), false);
        NewText("Subtitle", _panel, "EVIDENCE  ·  THEATRE STEPS", _typewriterFont, 15, InkMuted, TextAnchor.MiddleLeft,
            new Vector2(left, 358f), new Vector2(width, 22f), new Vector2(0f, 0.5f), false);

        BuildTicket(new Vector2(-6f, 262f));

        NewText("CluesHeader", _panel, "เบาะแสบนตั๋ว  (ผลตรวจสถานีที่เลือก)", thaiFontBold, 22, Ink, TextAnchor.MiddleLeft,
            new Vector2(left, 150f), new Vector2(width, 30f), new Vector2(0f, 0.5f), false);
        for (int i = 0; i < 4; i++)
        {
            float y = 114f - i * 36f;
            var bullet = NewImage("ClueBullet_" + i, _panel, new Vector2(left + 8f, y), new Vector2(11f, 11f), new Color(Ink.r, Ink.g, Ink.b, 0.45f));
            bullet.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            bullet.raycastTarget = false;
            _clueBullets[i] = bullet;
            NewText("Clue_" + i, _panel, ClueTexts[i], thaiFont, 21, Ink, TextAnchor.MiddleLeft,
                new Vector2(left + 28f, y), new Vector2(300f, 30f), new Vector2(0f, 0.5f), false);
            _clueResultTexts[i] = NewText("ClueResult_" + i, _panel, "", thaiFontBold, 20, Ink, TextAnchor.MiddleRight,
                new Vector2(215f, y), new Vector2(110f, 30f), new Vector2(1f, 0.5f), false);
        }

        MakeRule(_panel, new Vector2(0f, -36f), new Vector2(width, 2f), 0.5f);

        NewText("StationHeader", _panel, "สถานีที่เลือก", thaiFontBold, 22, Ink, TextAnchor.MiddleLeft,
            new Vector2(left, -58f), new Vector2(width, 30f), new Vector2(0f, 0.5f), false);
        _stationNameText = NewText("StationName", _panel, "", thaiFontBold, 32, StampRed, TextAnchor.MiddleLeft,
            new Vector2(left, -96f), new Vector2(width, 44f), new Vector2(0f, 0.5f), false);
        _stationInfoText = NewText("StationInfo", _panel, "", thaiFont, 20, Ink, TextAnchor.UpperLeft,
            new Vector2(left, -120f), new Vector2(width, 110f), new Vector2(0f, 1f), true);
        _stationInfoText.lineSpacing = 1.1f;

        // ปุ่มตารางเดินรถ
        var ttRect = NewRect("Btn_Timetable", _panel, new Vector2(0f, -264f), new Vector2(width, 50f), null);
        var ttImg = ttRect.gameObject.AddComponent<Image>();
        ttImg.color = Color.white;
        var ttBtn = ttRect.gameObject.AddComponent<Button>();
        StyleButton(ttBtn, Ink, new Color(0.35f, 0.24f, 0.14f, 1f));
        ttBtn.onClick.AddListener(ToggleTimetableModal);
        AddButtonFrame(ttRect, new Vector2(width, 50f), Brass);
        NewText("Label", ttRect, "เปิดตารางเดินรถ  [T]", thaiFontBold, 22, DetectiveUITheme.AgedPaper, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(width, 50f), null, false);

        // ปุ่มตรวจสอบกับตั๋ว
        var chkRect = NewRect("Btn_Check", _panel, new Vector2(0f, -332f), new Vector2(width, 60f), null);
        _checkButtonImage = chkRect.gameObject.AddComponent<Image>();
        _checkButtonImage.color = Color.white;
        _checkButton = chkRect.gameObject.AddComponent<Button>();
        StyleButton(_checkButton, StampRed, new Color(0.85f, 0.16f, 0.12f, 1f));
        _checkButton.onClick.AddListener(OnCheckPressed);
        AddButtonFrame(chkRect, new Vector2(width, 60f), new Color(1f, 0.85f, 0.7f, 0.9f));
        NewText("Label", chkRect, "ตรวจสอบกับตั๋ว  ›", thaiFontBold, 26, Color.white, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(width, 60f), null, false);

        _feedbackText = NewText("Feedback", _panel, "", thaiFont, 20, StampRed, TextAnchor.UpperLeft,
            new Vector2(left, -372f), new Vector2(width, 70f), new Vector2(0f, 1f), true);
        _feedbackText.lineSpacing = 1.08f;
        _attemptsText = NewText("Attempts", _panel, "", _typewriterFont, 15, InkMuted, TextAnchor.MiddleCenter,
            new Vector2(0f, -452f), new Vector2(width, 22f), null, false);
    }

    private void AddButtonFrame(RectTransform parent, Vector2 size, Color color)
    {
        var rect = NewRect("Frame", parent, Vector2.zero, size - new Vector2(8f, 8f), null);
        var frame = rect.gameObject.AddComponent<UIParallelogramGraphic>();
        frame.skewAmount = 0f;
        frame.outlineWidth = 1.5f;
        frame.outlineColor = new Color(color.r, color.g, color.b, 0.55f);
        frame.fillColor = new Color(0f, 0f, 0f, 0f);
        frame.raycastTarget = false;
    }

    private void BuildTicket(Vector2 pos)
    {
        var ticket = NewRect("Ticket", _panel, pos, new Vector2(420f, 156f), null);
        ticket.localEulerAngles = new Vector3(0f, 0f, 1.5f);

        // กระดาษตั๋ว: ขอบขวาฉีกเป็นรอยหยัก
        var shadow = NewRect("Shadow", ticket, new Vector2(6f, -7f), new Vector2(420f, 156f), null);
        var shadowGraphic = shadow.gameObject.AddComponent<UIJaggedCardGraphic>();
        shadowGraphic.color = new Color(0f, 0f, 0f, 0.35f);
        shadowGraphic.raycastTarget = false;
        var card = NewRect("Card", ticket, Vector2.zero, new Vector2(420f, 156f), null);
        var cardGraphic = card.gameObject.AddComponent<UIJaggedCardGraphic>();
        cardGraphic.color = new Color(0.97f, 0.91f, 0.76f, 1f);
        cardGraphic.raycastTarget = false;

        // แถบสีของบริษัท (Great Northern)
        var band = NewImage("Band", ticket, new Vector2(-30f, 62f), new Vector2(340f, 28f), new Color(0.16f, 0.30f, 0.56f, 1f));
        band.raycastTarget = false;
        NewText("Company", ticket, "...NORTHERN RAILWAY", _typewriterFont, 15, new Color(0.95f, 0.92f, 0.8f, 1f), TextAnchor.MiddleLeft,
            new Vector2(-190f, 62f), new Vector2(330f, 22f), new Vector2(0f, 0.5f), false);

        NewText("Time", ticket, "23:45", thaiFontBold, 64, StampRed, TextAnchor.MiddleLeft,
            new Vector2(-190f, 6f), new Vector2(260f, 78f), new Vector2(0f, 0.5f), false);
        NewText("Class", ticket, "EXPRESS  ·  1ST CLASS", _typewriterFont, 18, Ink, TextAnchor.MiddleLeft,
            new Vector2(-190f, -42f), new Vector2(300f, 22f), new Vector2(0f, 0.5f), false);
        NewText("Circle", ticket, "CIRCLE LINE CONNECTION", _typewriterFont, 12, InkMuted, TextAnchor.MiddleLeft,
            new Vector2(-190f, -62f), new Vector2(300f, 16f), new Vector2(0f, 0.5f), false);

        // รูเจาะตั๋ว + ตราวันที่ประทับ
        for (int i = 0; i < 4; i++)
        {
            var hole = NewImage("Hole_" + i, ticket, new Vector2(-204f, 36f - i * 24f), new Vector2(8f, 8f), new Color(0.35f, 0.28f, 0.18f, 0.8f));
            hole.sprite = DetectiveUIShapes.Circle;
            hole.raycastTarget = false;
        }
        var stampRing = NewImage("DateStamp", ticket, new Vector2(88f, -24f), new Vector2(92f, 92f), new Color(0.40f, 0.18f, 0.52f, 0.7f));
        stampRing.sprite = DetectiveUIShapes.Ring(0.06f);
        stampRing.rectTransform.localEulerAngles = new Vector3(0f, 0f, 14f);
        stampRing.raycastTarget = false;
        var stampText = NewText("DateStampText", ticket, "8 SEP\n1888", _typewriterFont, 15, new Color(0.40f, 0.18f, 0.52f, 0.75f), TextAnchor.MiddleCenter,
            new Vector2(88f, -24f), new Vector2(90f, 40f), null, false);
        stampText.rectTransform.localEulerAngles = new Vector3(0f, 0f, 14f);
    }

    private static void StyleButton(Button btn, Color normal, Color highlight)
    {
        var colors = btn.colors;
        colors.normalColor = normal;
        colors.highlightedColor = highlight;
        colors.selectedColor = normal;
        colors.pressedColor = new Color(normal.r * 0.7f, normal.g * 0.7f, normal.b * 0.7f, 1f);
        colors.disabledColor = new Color(0.45f, 0.4f, 0.35f, 0.7f);
        colors.colorMultiplier = 1f;
        btn.colors = colors;
    }

    // ==========================================
    // UI: ตารางเดินรถ
    // ==========================================
    private void BuildTimetable()
    {
        var rect = NewRect("Timetable", _root.transform, new Vector2(-340f, 10f), new Vector2(1000f, 700f), null);
        _timetable = rect.gameObject;

        var dim = NewImage("Dim", rect, Vector2.zero, new Vector2(1160f, 900f), new Color(0f, 0f, 0f, 0.55f));
        dim.raycastTarget = true;

        var shadow = NewImage("Shadow", rect, new Vector2(16f, -20f), new Vector2(1000f, 700f), new Color(0f, 0f, 0f, 0.65f));
        shadow.raycastTarget = false;
        var sheetRect = NewRect("Sheet", rect, Vector2.zero, new Vector2(1000f, 700f), null);
        var sheet = sheetRect.gameObject.AddComponent<RawImage>();
        sheet.raycastTarget = true;
        Sprite note = DetectiveUITheme.NoteCard;
        if (note != null)
        {
            sheet.texture = note.texture;
            sheet.uvRect = new Rect(0.12f, 0.27f, 0.76f, 0.44f);
            sheet.color = new Color(1f, 0.96f, 0.88f, 1f);
        }
        else sheet.color = DetectiveUITheme.AgedPaper;
        AddFrame(rect, new Vector2(980f, 680f), 3f, 0.7f);
        AddFrame(rect, new Vector2(962f, 662f), 1.2f, 0.45f);

        NewText("Title", rect, "ตารางเดินรถไฟ  คืนวันเสาร์", thaiFontBold, 40, Ink, TextAnchor.MiddleCenter,
            new Vector2(0f, 290f), new Vector2(900f, 56f), null, false);
        NewText("Sub", rect, "BRADSHAW'S RAILWAY GUIDE  ·  LAST DEPARTURES OF THE NIGHT", _typewriterFont, 16, InkMuted, TextAnchor.MiddleCenter,
            new Vector2(0f, 252f), new Vector2(900f, 24f), null, false);
        MakeRule(rect, new Vector2(0f, 228f), new Vector2(900f, 3f), 0.6f);

        float[] colX = { -440f, -150f, 90f, 260f };
        string[] heads = { "สถานี", "สาย", "ออกเที่ยวสุดท้าย", "ประเภทขบวน" };
        for (int c = 0; c < 4; c++)
        {
            NewText("Head_" + c, rect, heads[c], thaiFontBold, 22, StampRed, TextAnchor.MiddleLeft,
                new Vector2(colX[c], 196f), new Vector2(300f, 30f), new Vector2(0f, 0.5f), false);
        }
        MakeRule(rect, new Vector2(0f, 176f), new Vector2(900f, 1.5f), 0.4f);

        for (int i = 0; i < _stations.Count; i++)
        {
            var d = _stations[i];
            float y = 140f - i * 62f;
            NewText("Row_Station_" + i, rect, $"{d.nameThai}  <size=16><color=#5A4230>{d.nameEng}</color></size>", thaiFontBold, 25, Ink, TextAnchor.MiddleLeft,
                new Vector2(colX[0], y), new Vector2(300f, 40f), new Vector2(0f, 0.5f), false);
            var bar = NewImage("Row_Color_" + i, rect, new Vector2(colX[1] - 18f, y), new Vector2(6f, 30f), d.lineColor);
            bar.raycastTarget = false;
            NewText("Row_Line_" + i, rect, d.lineShort, thaiFont, 21, Ink, TextAnchor.MiddleLeft,
                new Vector2(colX[1], y), new Vector2(230f, 40f), new Vector2(0f, 0.5f), false);
            NewText("Row_Time_" + i, rect, d.lastTrain, _typewriterFont, 26, Ink, TextAnchor.MiddleLeft,
                new Vector2(colX[2], y), new Vector2(170f, 40f), new Vector2(0f, 0.5f), false);
            NewText("Row_Type_" + i, rect, d.serviceType, thaiFont, 21, Ink, TextAnchor.MiddleLeft,
                new Vector2(colX[3], y), new Vector2(240f, 40f), new Vector2(0f, 0.5f), false);
            MakeRule(rect, new Vector2(0f, y - 31f), new Vector2(900f, 1f), 0.25f);
        }

        var closeRect = NewRect("Btn_Close", rect, new Vector2(0f, -310f), new Vector2(300f, 50f), null);
        var closeImg = closeRect.gameObject.AddComponent<Image>();
        closeImg.color = Color.white;
        var closeBtn = closeRect.gameObject.AddComponent<Button>();
        StyleButton(closeBtn, Ink, StampRed);
        closeBtn.onClick.AddListener(ToggleTimetableModal);
        AddButtonFrame(closeRect, new Vector2(300f, 50f), Brass);
        NewText("Label", closeRect, "ปิดตาราง  [T]", thaiFontBold, 22, DetectiveUITheme.AgedPaper, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(300f, 50f), null, false);

        _timetable.SetActive(false);
    }

    private void MakeRule(RectTransform parent, Vector2 pos, Vector2 size, float alpha)
    {
        var rule = NewImage("Rule", parent, pos, size, new Color(Ink.r, Ink.g, Ink.b, alpha));
        rule.raycastTarget = false;
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

/// <summary>ส่งเหตุการณ์เมาส์ชี้เข้า/ออกสถานีบนแผนที่ ให้ปุ่มสถานีขยายเล็กน้อยตอนชี้</summary>
public class StationHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Action onEnter;
    public Action onExit;

    public void OnPointerEnter(PointerEventData eventData) { onEnter?.Invoke(); }
    public void OnPointerExit(PointerEventData eventData) { onExit?.Invoke(); }
}

/// <summary>
/// กระดาษสี่เหลี่ยมที่ขอบขวาฉีกเป็นรอยหยักไม่เรียบ (ใช้วาดตั๋วรถไฟที่ฉีกขาด)
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIJaggedCardGraphic : MaskableGraphic
{
    public int segments = 16;
    public float jag = 22f;
    public int seed = 1888;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        var rng = new System.Random(seed);

        float x0 = r.xMin;
        float yTop = r.yMax;
        float h = r.height;
        for (int i = 0; i <= segments; i++)
        {
            float y = yTop - h * (i / (float)segments);
            // รอยฉีกหยักแบบสุ่มคงที่: สลับสั้นยาวตามลำดับ
            float off = (i % 2 == 0 ? 0.1f : 0.55f) * jag + (float)rng.NextDouble() * jag * 0.45f;
            vh.AddVert(new Vector3(x0, y), color, Vector2.zero);
            vh.AddVert(new Vector3(r.xMax - off, y), color, Vector2.one);
        }
        for (int i = 0; i < segments; i++)
        {
            int a = i * 2;
            vh.AddTriangle(a, a + 1, a + 3);
            vh.AddTriangle(a, a + 3, a + 2);
        }
    }
}
