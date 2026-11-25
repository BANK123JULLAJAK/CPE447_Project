using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// หน้าสรุปบท (Chapter Summary) ใช้ร่วมกันทุกบท ในสไตล์แฟ้มคดีสกอตแลนด์ยาร์ด 1888
/// - หัวกระดาษ: บทที่ / ชื่อบท / สถานที่
/// - ซ้าย: หลักฐานที่รวบรวมได้ (ชิ้นสำคัญมีกรอบหมึกแดง)
/// - ขวา: ข้อสรุปของสารวัตร + เบาะแสที่จะนำไปบทถัดไป + เวลาสืบสวน
/// - ตราประทับ "CASE FILE UPDATED" กระแทกลงมา แล้วกดดำเนินการต่อ
/// เรียกใช้: ChapterSummaryUI.Show(data, () => { ...ไปบทถัดไป... });
/// </summary>
public class ChapterSummaryUI : MonoBehaviour
{
    [Serializable]
    public class EvidenceEntry
    {
        public string title;
        public string note;
        public bool isKey;

        public EvidenceEntry(string title, string note, bool isKey = false)
        {
            this.title = title;
            this.note = note;
            this.isKey = isKey;
        }
    }

    [Serializable]
    public class Data
    {
        public int chapterNumber;
        public string chapterTitle;
        public string location;
        public List<EvidenceEntry> evidence = new List<EvidenceEntry>();
        /// <summary>สายการอนุมานของบท (เรียงตามลำดับข้อสันนิษฐาน)</summary>
        public List<string> chain = new List<string>();
        public string openQuestion;
        public string nextLead;
        public float elapsedSeconds = -1f;
        /// <summary>ความแม่นยำ 1-3 (ติดลบ = ไม่แสดง)</summary>
        public int accuracy = -1;
    }

    public static ChapterSummaryUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance._root != null && Instance._root.activeSelf;

    /// <summary>ปิดหน้าต่างทันทีโดยไม่เรียก callback (ใช้ตอนกระโดดฉากเพื่อทดสอบ)</summary>
    public static void ForceClose()
    {
        if (Instance == null || Instance._root == null) return;
        Instance.StopAllCoroutines();
        Instance._root.SetActive(false);
    }

    [Tooltip("ฟอนต์หัวข้อภาษาไทย (ว่างไว้ = Prompt-Bold)")]
    public Font headingFont;
    [Tooltip("ฟอนต์เนื้อหาภาษาไทย (ว่างไว้ = Sarabun-Medium)")]
    public Font bodyFont;

    private GameObject _root;
    private CanvasGroup _rootGroup;
    private RectTransform _paper;
    private Action _onContinue;
    private bool _canContinue;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private const float PaperW = 1560f;
    private const float PaperH = 940f;

    public static void Show(Data data, Action onContinue)
    {
        if (Instance == null)
        {
            var go = new GameObject("ChapterSummaryUI");
            go.AddComponent<ChapterSummaryUI>();
        }
        Instance.Open(data, onContinue);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (headingFont == null) headingFont = GameAssets.Load<Font>("Assets/Font/Prompt-Bold.ttf");
        if (bodyFont == null) bodyFont = GameAssets.Load<Font>("Assets/Font/Sarabun-Medium.ttf");
        if (headingFont == null) headingFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (bodyFont == null) bodyFont = headingFont;

        BuildFrame();
        _root.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen || !_canContinue) return;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
            Continue();
#else
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) Continue();
#endif
    }

    private void Open(Data data, Action onContinue)
    {
        _onContinue = onContinue;
        _canContinue = false;

        foreach (var go in _spawned) if (go != null) Destroy(go);
        _spawned.Clear();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _root.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(OpenRoutine(data));
    }

    private IEnumerator OpenRoutine(Data data)
    {
        // หัวกระดาษ
        Spawn(CreateText(_paper, "Letterhead", "METROPOLITAN POLICE  ·  CASE FILE 1888/JR", bodyFont, 20,
            DetectiveUITheme.InkMuted, TextAnchor.MiddleLeft, new Vector2(-PaperW * 0.5f + 110f, 400f), new Vector2(700f, 30f), pivotLeft: true));
        Spawn(CreateText(_paper, "Chapter", $"บทที่ {data.chapterNumber}  ·  สรุปการสืบสวน", headingFont, 26,
            DetectiveUITheme.StampRed, TextAnchor.MiddleLeft, new Vector2(-PaperW * 0.5f + 110f, 355f), new Vector2(900f, 40f), pivotLeft: true));
        Spawn(CreateText(_paper, "Title", data.chapterTitle, headingFont, 56,
            DetectiveUITheme.InkDark, TextAnchor.MiddleLeft, new Vector2(-PaperW * 0.5f + 110f, 297f), new Vector2(1100f, 76f), pivotLeft: true));
        Spawn(CreateText(_paper, "Location", data.location, bodyFont, 26,
            DetectiveUITheme.InkMuted, TextAnchor.MiddleLeft, new Vector2(-PaperW * 0.5f + 112f, 248f), new Vector2(1100f, 36f), pivotLeft: true, italic: true));
        Spawn(CreateRule(_paper, new Vector2(0f, 218f), new Vector2(PaperW - 220f, 3f), 0.55f));

        // คอลัมน์ซ้าย: หลักฐาน
        float leftX = -PaperW * 0.5f + 110f;
        Spawn(CreateText(_paper, "EvidenceHeader", "หลักฐานที่รวบรวมได้", headingFont, 30,
            DetectiveUITheme.InkDark, TextAnchor.MiddleLeft, new Vector2(leftX, 175f), new Vector2(640f, 44f), pivotLeft: true));

        float y = 120f;
        float rowH = data.evidence.Count > 5 ? 74f : 84f;
        var rows = new List<CanvasGroup>();
        foreach (var ev in data.evidence)
        {
            rows.Add(BuildEvidenceRow(ev, new Vector2(leftX, y), rowH));
            y -= rowH;
        }

        // คอลัมน์ขวา: สายการอนุมาน -> ข้อสงสัยที่ยังค้าง -> เบาะแสถัดไป
        float rightX = 70f;
        float rightW = PaperW * 0.5f - 180f;

        var chainBlock = CreateBlock("ChainBlock");
        CreateText(chainBlock, "ChainHeader", "ลำดับการอนุมาน", headingFont, 30,
            DetectiveUITheme.InkDark, TextAnchor.MiddleLeft, new Vector2(rightX, 175f), new Vector2(rightW, 44f), pivotLeft: true);
        var chainText = CreateText(chainBlock, "Chain", BuildChainText(data.chain), bodyFont, 21,
            DetectiveUITheme.InkDark, TextAnchor.UpperLeft, new Vector2(rightX, 140f), new Vector2(rightW, 230f), pivotLeft: true, pivotTop: true);
        chainText.horizontalOverflow = HorizontalWrapMode.Wrap;
        chainText.lineSpacing = 1.05f;
        chainText.resizeTextForBestFit = true;
        chainText.resizeTextMinSize = 16;
        chainText.resizeTextMaxSize = 21;
        chainText.verticalOverflow = VerticalWrapMode.Truncate;

        var openBlock = CreateBlock("OpenQuestionBlock");
        if (!string.IsNullOrEmpty(data.openQuestion))
        {
            CreateText(openBlock, "OpenHeader", "ข้อสงสัยที่ยังค้างอยู่", headingFont, 26,
                DetectiveUITheme.StampRed, TextAnchor.MiddleLeft, new Vector2(rightX, -105f), new Vector2(rightW, 40f), pivotLeft: true);
            var openText = CreateText(openBlock, "OpenQuestion", data.openQuestion, bodyFont, 22,
                DetectiveUITheme.InkDark, TextAnchor.UpperLeft, new Vector2(rightX, -132f), new Vector2(rightW, 70f), pivotLeft: true, pivotTop: true, italic: true);
            openText.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        var leadBlock = CreateBlock("LeadBlock");
        CreateText(leadBlock, "LeadHeader", "เบาะแสถัดไป", headingFont, 26,
            DetectiveUITheme.InkDark, TextAnchor.MiddleLeft, new Vector2(rightX, -218f), new Vector2(rightW, 40f), pivotLeft: true);
        var lead = CreateText(leadBlock, "Lead", data.nextLead, bodyFont, 22,
            DetectiveUITheme.InkDark, TextAnchor.UpperLeft, new Vector2(rightX, -245f), new Vector2(rightW, 70f), pivotLeft: true, pivotTop: true, italic: true);
        lead.horizontalOverflow = HorizontalWrapMode.Wrap;

        // สถิติการสืบสวน (ล่างซ้าย): เวลา + ความแม่นยำ
        var statsBlock = CreateBlock("StatsBlock");
        if (data.elapsedSeconds >= 0f)
        {
            int total = Mathf.RoundToInt(data.elapsedSeconds);
            CreateText(statsBlock, "Elapsed", $"เวลาที่ใช้สืบสวน  {total / 60:00}:{total % 60:00} นาที", bodyFont, 22,
                DetectiveUITheme.InkMuted, TextAnchor.MiddleLeft, new Vector2(leftX, -395f), new Vector2(380f, 34f), pivotLeft: true);
        }
        if (data.accuracy >= 0)
        {
            CreateText(statsBlock, "AccuracyLabel", "ความแม่นยำ", bodyFont, 22,
                DetectiveUITheme.InkMuted, TextAnchor.MiddleLeft, new Vector2(leftX + 400f, -395f), new Vector2(130f, 34f), pivotLeft: true);
            for (int i = 0; i < 3; i++)
            {
                var pip = NewImage(statsBlock, "AccuracyPip_" + i, i < data.accuracy ? DetectiveUITheme.StampRed : new Color(0.14f, 0.1f, 0.07f, 0.25f));
                pip.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                pip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                pip.rectTransform.sizeDelta = new Vector2(18f, 18f);
                pip.rectTransform.anchoredPosition = new Vector2(leftX + 548f + i * 30f, -395f);
                pip.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            }
        }

        var chainGroup = chainBlock.GetComponent<CanvasGroup>();
        var openGroup = openBlock.GetComponent<CanvasGroup>();
        var leadGroup = leadBlock.GetComponent<CanvasGroup>();
        var statsGroup = statsBlock.GetComponent<CanvasGroup>();
        // ตราประทับ
        var stamp = BuildStamp(new Vector2(PaperW * 0.5f - 300f, 330f));

        // ปุ่มไปต่อ
        var button = BuildContinueButton(new Vector2(PaperW * 0.5f - 290f, -385f));

        foreach (var r in rows) r.alpha = 0f;
        chainGroup.alpha = 0f;
        openGroup.alpha = 0f;
        leadGroup.alpha = 0f;
        statsGroup.alpha = 0f;
        stamp.alpha = 0f;
        button.alpha = 0f;

        // แอนิเมชันเปิด: กระดาษเลื่อนขึ้นพร้อมจางเข้า -> หลักฐานทีละแถว -> ข้อสรุป -> ตราประทับ -> ปุ่ม
        _paper.anchoredPosition = new Vector2(0f, -60f);
        _paper.localEulerAngles = new Vector3(0f, 0f, 1.5f);
        yield return Animate(0.45f, t =>
        {
            _rootGroup.alpha = t;
            _paper.anchoredPosition = Vector2.Lerp(new Vector2(0f, -60f), Vector2.zero, t);
            _paper.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(1.5f, -0.6f, t));
        });

        foreach (var r in rows)
        {
            yield return Animate(0.18f, t => r.alpha = t);
        }
        yield return Animate(0.4f, t => { chainGroup.alpha = t; statsGroup.alpha = t; });
        yield return Animate(0.3f, t => openGroup.alpha = t);
        yield return Animate(0.3f, t => leadGroup.alpha = t);

        yield return new WaitForSecondsRealtime(0.25f);
        var stampRect = (RectTransform)stamp.transform;
        yield return Animate(0.16f, t =>
        {
            stamp.alpha = t;
            stampRect.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, t);
        });
        // สั่นกระดาษเล็กน้อยตอนตราประทับกระแทก
        yield return Animate(0.18f, t =>
        {
            float shake = (1f - t) * 6f;
            _paper.anchoredPosition = new Vector2(UnityEngine.Random.Range(-shake, shake), UnityEngine.Random.Range(-shake, shake));
        });
        _paper.anchoredPosition = Vector2.zero;

        yield return Animate(0.3f, t => button.alpha = t);
        button.interactable = true;
        button.blocksRaycasts = true;
        _canContinue = true;
    }

    private void Continue()
    {
        if (!_canContinue) return;
        _canContinue = false;
        StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        yield return Animate(0.35f, t =>
        {
            _rootGroup.alpha = 1f - t;
            _paper.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(0f, 40f), t);
        });
        _root.SetActive(false);
        var cb = _onContinue;
        _onContinue = null;
        cb?.Invoke();
    }

    // ==========================================
    // Building blocks
    // ==========================================
    private void BuildFrame()
    {
        _root = new GameObject("ChapterSummary_Canvas", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 960;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        _root.AddComponent<GraphicRaycaster>();
        _rootGroup = _root.AddComponent<CanvasGroup>();

        var bg = NewImage(_root.transform, "Backdrop", new Color(0.03f, 0.025f, 0.02f, 1f));
        Stretch(bg.rectTransform);
        Sprite cork = DetectiveUITheme.CorkboardBg;
        if (cork != null)
        {
            var corkImg = NewImage(_root.transform, "Corkboard", new Color(0.3f, 0.26f, 0.22f, 1f));
            corkImg.sprite = cork;
            Stretch(corkImg.rectTransform);
        }

        var paperGo = new GameObject("Paper", typeof(RectTransform));
        paperGo.transform.SetParent(_root.transform, false);
        _paper = paperGo.GetComponent<RectTransform>();
        _paper.sizeDelta = new Vector2(PaperW, PaperH);

        var shadow = NewImage(_paper, "Shadow", new Color(0f, 0f, 0f, 0.6f));
        Stretch(shadow.rectTransform);
        shadow.rectTransform.offsetMin = new Vector2(22f, -26f);
        shadow.rectTransform.offsetMax = new Vector2(22f, -26f);

        var sheetGo = new GameObject("Sheet", typeof(RectTransform), typeof(RawImage));
        sheetGo.transform.SetParent(_paper, false);
        Stretch(sheetGo.GetComponent<RectTransform>());
        var sheet = sheetGo.GetComponent<RawImage>();
        Sprite note = DetectiveUITheme.NoteCard;
        if (note != null)
        {
            sheet.texture = note.texture;
            sheet.uvRect = new Rect(0.105f, 0.232f, 0.79f, 0.54f);
            sheet.color = new Color(1f, 0.97f, 0.92f, 1f);
        }
        else sheet.color = DetectiveUITheme.AgedPaper;

        // เส้นแบ่งคอลัมน์
        var divider = NewImage(_paper, "ColumnDivider", new Color(0.14f, 0.1f, 0.07f, 0.25f));
        divider.rectTransform.sizeDelta = new Vector2(2f, 560f);
        divider.rectTransform.anchoredPosition = new Vector2(20f, -90f);
    }

    private CanvasGroup BuildEvidenceRow(EvidenceEntry ev, Vector2 topLeft, float rowH)
    {
        var go = new GameObject("Evidence_" + ev.title, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(_paper, false);
        var rect = go.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(PaperW * 0.5f - 160f, rowH - 10f);
        rect.anchoredPosition = topLeft;
        Spawn(go);

        if (ev.isKey)
        {
            var frame = go.AddComponent<UIParallelogramGraphic>();
            frame.skewAmount = 0f;
            frame.outlineWidth = 3f;
            frame.outlineColor = DetectiveUITheme.StampRed;
            frame.fillColor = new Color(DetectiveUITheme.StampRed.r, DetectiveUITheme.StampRed.g, DetectiveUITheme.StampRed.b, 0.08f);
            frame.raycastTarget = false;
        }

        Color titleColor = ev.isKey ? DetectiveUITheme.StampRed : DetectiveUITheme.InkDark;
        string bullet = ev.isKey ? "◆" : "•";
        CreateText(rect, "Title", $"{bullet}  {ev.title}", headingFont, 24, titleColor, TextAnchor.UpperLeft,
            new Vector2(16f, -8f), new Vector2(rect.sizeDelta.x - 32f, 34f), pivotLeft: true, pivotTop: true, rowAnchored: true);
        var note = CreateText(rect, "Note", ev.note, bodyFont, 20, DetectiveUITheme.InkMuted, TextAnchor.UpperLeft,
            new Vector2(42f, -40f), new Vector2(rect.sizeDelta.x - 58f, rowH - 46f), pivotLeft: true, pivotTop: true, rowAnchored: true);
        note.horizontalOverflow = HorizontalWrapMode.Wrap;

        if (ev.isKey)
        {
            CreateText(rect, "KeyTag", "หลักฐานสำคัญ", headingFont, 18, DetectiveUITheme.StampRed, TextAnchor.UpperRight,
                new Vector2(-16f, -10f), new Vector2(220f, 28f), pivotLeft: false, pivotTop: true, anchorRight: true, rowAnchored: true);
        }

        return go.GetComponent<CanvasGroup>();
    }

    private CanvasGroup BuildStamp(Vector2 pos)
    {
        var go = new GameObject("Stamp", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(_paper, false);
        Spawn(go);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(420f, 120f);
        rect.anchoredPosition = pos;
        rect.localEulerAngles = new Vector3(0f, 0f, -9f);

        Color ink = new Color(DetectiveUITheme.StampRed.r, DetectiveUITheme.StampRed.g, DetectiveUITheme.StampRed.b, 0.8f);
        var frame = go.AddComponent<UIParallelogramGraphic>();
        frame.skewAmount = 0f;
        frame.outlineWidth = 6f;
        frame.outlineColor = ink;
        frame.fillColor = new Color(0f, 0f, 0f, 0f);
        frame.raycastTarget = false;

        var t1 = CreateText(rect, "Top", "CASE FILE", headingFont, 46, ink, TextAnchor.MiddleCenter, new Vector2(0f, 20f), new Vector2(400f, 56f));
        t1.fontStyle = FontStyle.Bold;
        CreateText(rect, "Bottom", "UPDATED", headingFont, 34, ink, TextAnchor.MiddleCenter, new Vector2(0f, -30f), new Vector2(400f, 44f));
        return go.GetComponent<CanvasGroup>();
    }

    private CanvasGroup BuildContinueButton(Vector2 pos)
    {
        var go = new GameObject("ContinueButton", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
        go.transform.SetParent(_paper, false);
        Spawn(go);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(440f, 72f);
        rect.anchoredPosition = pos;

        var img = go.GetComponent<Image>();
        img.color = Color.white;
        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = DetectiveUITheme.InkDark;
        colors.highlightedColor = DetectiveUITheme.StampRed; // ชี้แล้วเป็นแดงหมึก
        colors.selectedColor = DetectiveUITheme.InkDark;
        colors.pressedColor = new Color(0.45f, 0.06f, 0.05f, 1f);
        colors.colorMultiplier = 1f;
        btn.colors = colors;
        btn.onClick.AddListener(Continue);

        CreateText(rect, "Label", "ดำเนินการสืบสวนต่อ  ›", headingFont, 28, DetectiveUITheme.AgedPaper, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(440f, 72f));
        CreateText(rect, "Hint", "[Enter]", bodyFont, 18, DetectiveUITheme.InkMuted, TextAnchor.MiddleCenter,
            new Vector2(0f, -54f), new Vector2(440f, 28f));

        var group = go.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    /// <summary>กลุ่มองค์ประกอบที่ทับกระดาษเต็มแผ่น (ใช้จางเข้าทีละส่วน) พิกัดลูกอ้างอิงกึ่งกลางกระดาษ</summary>
    private RectTransform CreateBlock(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(_paper, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        go.GetComponent<CanvasGroup>().blocksRaycasts = false;
        Spawn(go);
        return r;
    }

    private static string BuildChainText(List<string> chain)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < chain.Count; i++)
        {
            sb.Append("<b>").Append(i + 1).Append(".</b>  ").Append(chain[i]);
            if (i < chain.Count - 1) sb.Append("\n");
        }
        return sb.ToString();
    }

    private GameObject CreateRule(RectTransform parent, Vector2 pos, Vector2 size, float alpha)
    {
        var img = NewImage(parent, "Rule", new Color(0.14f, 0.1f, 0.07f, alpha));
        img.rectTransform.sizeDelta = size;
        img.rectTransform.anchoredPosition = pos;
        img.raycastTarget = false;
        return img.gameObject;
    }

    private Text CreateText(RectTransform parent, string name, string text, Font font, int size, Color color, TextAnchor align,
        Vector2 pos, Vector2 box, bool pivotLeft = false, bool pivotTop = false, bool italic = false, bool anchorRight = false, bool rowAnchored = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();

        // ข้อความในแถวหลักฐานอ้างอิงมุมซ้ายบน/ขวาบนของแถว ส่วนข้อความอื่นอ้างอิงกึ่งกลางของ parent
        Vector2 anchor = rowAnchored ? new Vector2(anchorRight ? 1f : 0f, 1f) : new Vector2(0.5f, 0.5f);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(pivotLeft ? 0f : (anchorRight ? 1f : 0.5f), pivotTop ? 1f : 0.5f);
        rect.sizeDelta = box;
        rect.anchoredPosition = pos;

        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = italic ? FontStyle.Italic : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private void Spawn(Component c) { if (c != null) _spawned.Add(c.gameObject); }
    private void Spawn(GameObject go) { if (go != null) _spawned.Add(go); }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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
}
