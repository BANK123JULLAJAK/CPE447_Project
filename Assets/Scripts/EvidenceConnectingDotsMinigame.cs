using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// มินิเกมบทที่ 4: Connecting the Dots - ลากเส้นด้ายแดงจากหมุดของหลักฐานแต่ละชิ้นไปยังข้อสรุปที่หลักฐานนั้นยืนยัน
/// - หลักฐาน 5 ชิ้นอยู่ซ้าย ข้อสรุป 4 ข้ออยู่ขวา (ชานชาลา 2 / ขบวนด่วน 11:45 PM / ตู้หมายเลข 4 / "ตู้หน้าสุด" จากจดหมาย M)
/// - เส้นที่ผิดจะหย่อนและหลุดออกเอง (นับเป็นความผิดพลาด) เส้นที่ถูกจะตรึงอยู่
/// - จดหมายตรา M ถูกมอริอาร์ตี้ผูกด้ายเหลืองไว้กับ "ตู้หน้าสุด" ล่วงหน้า ผู้เล่นต้องคลิกที่เส้นเพื่อ "ตัดคำลวง" ทิ้ง
/// - เมื่อต่อเส้นที่ถูกครบ 7 เส้นและตัดคำลวงแล้ว ระบบเผยเส้นทางหลบหนีที่แท้จริงและกลลวงของมอริอาร์ตี้
/// </summary>
public class EvidenceConnectingDotsMinigame : MonoBehaviour
{
    public static EvidenceConnectingDotsMinigame Instance { get; private set; }

    [Header("=== Fonts & Typography ===")]
    public Font thaiFont;
    public Font thaiFontBold;

    public bool IsMinigameActive => _root != null && _root.activeSelf;
    public Action OnDeductionSolved;
    /// <summary>จำนวนครั้งที่ต่อเส้นผิด</summary>
    public int Mistakes { get; private set; }

    // ----- ข้อมูล -----
    private class EvidenceDef
    {
        public string title;
        public string desc;
        public int[] truth;           // ข้อสรุปที่หลักฐานนี้ยืนยัน
        public RectTransform rect;
        public Vector2 socket;
        public Image pin;
        public Image pinRing;
        public Text linkCount;
        public float shake;
        public Vector2 basePos;
    }

    private class NodeDef
    {
        public string title;
        public string sub;
        public int expected;
        public bool isDecoy;
        public RectTransform rect;
        public Vector2 socket;
        public Image glow;
        public Image socketImg;
        public Text counter;
        public float flash;
        public Color flashColor;
    }

    private class Link
    {
        public int ev;
        public int node;
        public bool valid;
        public bool decoy;
        public YarnGraphic yarn;
        public float sag;
        public float targetSag;
        public float life;       // วินาทีนับจากเริ่มสร้าง
        public bool dying;
        public float dyingT;
        public bool cut;
        public Text tag;
    }

    private static readonly string[] NodeTitles =
    {
        "ชานชาลาที่ 2", "ขบวนด่วน 11:45 PM", "ตู้โดยสารหมายเลข 4", "ตู้หน้าสุดของขบวน"
    };
    private static readonly string[] NodeSubs =
    {
        "PLATFORM 2  ·  RIGHT BRANCH", "NORTHERN EXPRESS  ·  23:45", "CARRIAGE No. 4  ·  REAR OF TRAIN", "\"HE IS AT THE FRONT\"  ·  NOTE FROM M"
    };

    private readonly List<EvidenceDef> _evidence = new List<EvidenceDef>();
    private readonly List<NodeDef> _nodes = new List<NodeDef>();
    private readonly List<Link> _links = new List<Link>();

    private const int RequiredValid = 7;

    // ----- UI -----
    private GameObject _root;
    private CanvasGroup _rootGroup;
    private RectTransform _board;
    private RectTransform _yarnLayer;
    private Font _typewriterFont;
    private Texture2D _paperTex;
    private Text _statusText;
    private Text _feedbackText;
    private Text _hintText;
    private YarnGraphic _dragYarn;
    private GameObject _climax;
    private Text _stampText;

    private static readonly Rect SlipUv = new Rect(0.04f, 0.22f, 0.90f, 0.42f);
    private static readonly Color Ink = new Color(0.14f, 0.10f, 0.07f, 1f);
    private static readonly Color InkMuted = new Color(0.32f, 0.23f, 0.15f, 1f);
    private static readonly Color Brass = new Color(0.86f, 0.70f, 0.36f, 1f);
    private static readonly Color Crimson = new Color(0.72f, 0.09f, 0.10f, 1f);
    private static readonly Color Leather = new Color(0.10f, 0.07f, 0.05f, 1f);
    private static readonly Color GreenInk = new Color(0.20f, 0.62f, 0.34f, 1f);
    private static readonly Color DeceitYellow = new Color(0.88f, 0.72f, 0.20f, 1f);

    // ----- สถานะ -----
    private int _dragEv = -1;
    private bool _wasPressed;
    private bool _decoyCut;
    private bool _solved;
    private int _hoverEv = -1;
    private int _hoverNode = -1;
    private Link _hoverLink;
    private float _feedbackTimer;

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
        _paperTex = GameAssets.Load<Texture2D>("Assets/UI/Folder/Gemini_Generated_Image_oht62eoht62eoht6-Photoroom.png");
        if (thaiFont == null) thaiFont = VisionOSUITheme.PrimaryFont;
        if (thaiFontBold == null) thaiFontBold = VisionOSUITheme.BoldFont;
        _typewriterFont = Font.CreateDynamicFontFromOSFont(new[] { "Courier New", "Consolas" }, 24);
        if (_typewriterFont == null) _typewriterFont = thaiFontBold;

        BuildUI();
    }

    // ==========================================
    // Open / Close
    // ==========================================
    public void OpenMinigame(Action onSolvedCallback = null)
    {
        if (onSolvedCallback != null) OnDeductionSolved = onSolvedCallback;

        if (WaypointNavigationSystem.Instance != null) WaypointNavigationSystem.Instance.SetVisible(false);
        if (UndergroundStationQuestManager.Instance != null) UndergroundStationQuestManager.Instance.ShowObjectiveHUD(false);
        if (RoyalLyceumTheaterQuestManager.Instance != null) RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(false);
        if (LondonAlleyQuestManager.Instance != null) LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
        if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
        DetectiveThoughtToast.Clear();

        ResetState();
        _root.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(OpenAnimRoutine());

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (CinematicCutsceneManager.Instance != null) CinematicCutsceneManager.Instance.SetPlayerControlsLocked(true);
    }

    /// <summary>ปิดมินิเกม (ผู้จัดการเควสเป็นผู้คืน HUD/หมุดนำทางเอง)</summary>
    public void CloseMinigame()
    {
        if (_root != null) _root.SetActive(false);
    }

    private void ResetState()
    {
        _solved = false;
        _decoyCut = false;
        _dragEv = -1;
        Mistakes = 0;
        _feedbackTimer = 0f;

        foreach (var l in _links)
        {
            if (l.tag != null) Destroy(l.tag.gameObject);
            if (l.yarn != null) Destroy(l.yarn.gameObject);
        }
        _links.Clear();
        _dragYarn.gameObject.SetActive(false);
        _climax.SetActive(false);
        _stampText.gameObject.SetActive(false);

        // จดหมายตรา M ถูกผูกด้ายเหลืองไว้กับ "ตู้หน้าสุด" ล่วงหน้า (คำลวง)
        AddLink(4, 3, valid: false, decoy: true, animate: false);

        _feedbackText.text = "";
        UpdateStatus();
    }

    private IEnumerator OpenAnimRoutine()
    {
        _rootGroup.alpha = 0f;
        for (int i = 0; i < _evidence.Count; i++) _evidence[i].rect.anchoredPosition = _evidence[i].basePos + new Vector2(-60f, 0f);
        for (int i = 0; i < _nodes.Count; i++) _nodes[i].rect.anchoredPosition = NodeBasePos(i) + new Vector2(60f, 0f);

        float t = 0f;
        const float duration = 0.8f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _rootGroup.alpha = Mathf.Clamp01(t / 0.35f);
            for (int i = 0; i < _evidence.Count; i++)
            {
                float k = EaseOut(Mathf.Clamp01((t - i * 0.07f) / 0.4f));
                _evidence[i].rect.anchoredPosition = _evidence[i].basePos + new Vector2(Mathf.Lerp(-60f, 0f, k), 0f);
            }
            for (int i = 0; i < _nodes.Count; i++)
            {
                float k = EaseOut(Mathf.Clamp01((t - 0.15f - i * 0.07f) / 0.4f));
                _nodes[i].rect.anchoredPosition = NodeBasePos(i) + new Vector2(Mathf.Lerp(60f, 0f, k), 0f);
            }
            yield return null;
        }
        _rootGroup.alpha = 1f;
        for (int i = 0; i < _evidence.Count; i++) _evidence[i].rect.anchoredPosition = _evidence[i].basePos;
        for (int i = 0; i < _nodes.Count; i++) _nodes[i].rect.anchoredPosition = NodeBasePos(i);
    }

    private static float EaseOut(float t) { return 1f - (1f - t) * (1f - t) * (1f - t); }

    // ==========================================
    // Update: อินพุต + แอนิเมชันเส้นด้าย
    // ==========================================
    private void Update()
    {
        if (!IsMinigameActive) return;

        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        float dt = Time.unscaledDeltaTime;
        UpdateYarnAnimation(dt);
        UpdateNodeAnimation(dt);

        if (_solved) return;
        CheckSolved();
        if (_solved) return;

        Vector2 m = PointerLocal();
        bool pressed = IsPointerPressed();
        bool down = pressed && !_wasPressed;
        bool up = !pressed && _wasPressed;
        _wasPressed = pressed;

        // เป้าหมายใต้เมาส์
        _hoverEv = -1;
        _hoverNode = -1;
        _hoverLink = null;
        for (int i = 0; i < _evidence.Count; i++)
        {
            if (Vector2.Distance(m, _evidence[i].socket) < 30f) _hoverEv = i;
        }
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (NodeHit(i, m, 14f)) _hoverNode = i;
        }
        if (_dragEv < 0 && _hoverEv < 0)
        {
            float best = 16f;
            foreach (var l in _links)
            {
                if (l.dying || l.cut || l.yarn == null) continue;
                float d = l.yarn.DistanceTo(m);
                if (d < best) { best = d; _hoverLink = l; }
            }
        }

        if (down)
        {
            if (_hoverEv >= 0)
            {
                _dragEv = _hoverEv;
                _dragYarn.gameObject.SetActive(true);
                _dragYarn.a = _evidence[_dragEv].socket;
                _dragYarn.b = m;
                _dragYarn.sag = 24f;
                _dragYarn.SetVerticesDirty();
                PlayClick(0.35f);
            }
            else if (_hoverLink != null)
            {
                CutLink(_hoverLink);
            }
        }

        if (_dragEv >= 0)
        {
            _dragYarn.a = _evidence[_dragEv].socket;
            _dragYarn.b = m;
            _dragYarn.SetVerticesDirty();

            if (up)
            {
                int target = -1;
                for (int i = 0; i < _nodes.Count; i++) if (NodeHit(i, m, 24f)) target = i;
                _dragYarn.gameObject.SetActive(false);
                if (target >= 0) TryConnect(_dragEv, target);
                _dragEv = -1;
            }
        }

        // ข้อความคำใบ้/สถานะท้ายจอ
        if (_feedbackTimer > 0f)
        {
            _feedbackTimer -= dt;
            if (_feedbackTimer <= 0f) _feedbackText.text = "";
        }

        // ไฮไลต์หมุด
        for (int i = 0; i < _evidence.Count; i++)
        {
            var e = _evidence[i];
            bool active = i == _hoverEv || i == _dragEv;
            e.pinRing.color = new Color(1f, 0.9f, 0.6f, active ? 0.95f : 0f);
            float s = Mathf.Lerp(e.pin.rectTransform.localScale.x, active ? 1.25f : 1f, 1f - Mathf.Exp(-16f * dt));
            e.pin.rectTransform.localScale = Vector3.one * s;
        }
        for (int i = 0; i < _nodes.Count; i++)
        {
            var n = _nodes[i];
            bool willDrop = _dragEv >= 0 && i == _hoverNode;
            float s = Mathf.Lerp(n.socketImg.rectTransform.localScale.x, willDrop ? 1.35f : 1f, 1f - Mathf.Exp(-16f * dt));
            n.socketImg.rectTransform.localScale = Vector3.one * s;
        }

        _hintText.text = _dragEv >= 0
            ? "ปล่อยเมาส์บนข้อสรุปที่หลักฐานชิ้นนี้ยืนยัน"
            : (_hoverLink != null ? "คลิกเพื่อตัดเส้นด้ายนี้"
                : "ลากเส้นด้ายจากหมุดของหลักฐานไปยังข้อสรุป  ·  คลิกที่เส้นด้ายเพื่อตัดออก");
    }

    private bool NodeHit(int i, Vector2 m, float pad)
    {
        var rt = _nodes[i].rect;
        Vector2 c = rt.anchoredPosition;
        Vector2 half = rt.sizeDelta * 0.5f + new Vector2(pad, pad);
        return Mathf.Abs(m.x - c.x) <= half.x && Mathf.Abs(m.y - c.y) <= half.y;
    }

    private Vector2 PointerLocal()
    {
        Vector2 screen;
#if ENABLE_INPUT_SYSTEM
        screen = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        screen = Input.mousePosition;
#endif
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_board, screen, null, out var local);
        return local;
    }

    private static bool IsPointerPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    private void UpdateYarnAnimation(float dt)
    {
        for (int i = _links.Count - 1; i >= 0; i--)
        {
            var l = _links[i];
            if (l.yarn == null) { _links.RemoveAt(i); continue; }

            l.life += dt;
            var ev = _evidence[l.ev];
            var node = _nodes[l.node];
            l.yarn.a = ev.socket;
            l.yarn.b = node.socket;

            if (l.dying)
            {
                l.dyingT += dt;
                float k = Mathf.Clamp01(l.dyingT / 0.7f);
                l.yarn.sag = Mathf.Lerp(l.sag, l.sag + 180f, k * k);
                var c = l.yarn.color;
                l.yarn.color = new Color(c.r, c.g, c.b, 1f - k);
                l.yarn.thickness = Mathf.Lerp(6f, 3f, k);
                l.yarn.SetVerticesDirty();
                if (k >= 1f)
                {
                    Destroy(l.yarn.gameObject);
                    _links.RemoveAt(i);
                }
                continue;
            }

            if (l.cut)
            {
                l.dyingT += dt;
                float k = Mathf.Clamp01(l.dyingT / 0.35f);
                l.yarn.sag = Mathf.Lerp(l.sag, l.sag + 120f, k);
                var c = l.yarn.color;
                l.yarn.color = new Color(c.r, c.g, c.b, 1f - k);
                l.yarn.SetVerticesDirty();
                if (k >= 1f)
                {
                    if (l.tag != null) Destroy(l.tag.gameObject);
                    Destroy(l.yarn.gameObject);
                    _links.RemoveAt(i);
                }
                continue;
            }

            // เส้นที่เพิ่งต่อ: กระเด้งแล้วตรึง
            float settle = Mathf.Clamp01(l.life / 0.5f);
            float wobble = Mathf.Sin(l.life * 18f) * (1f - settle) * 14f;
            l.sag = Mathf.Lerp(l.sag, l.targetSag, 1f - Mathf.Exp(-10f * dt));
            l.yarn.sag = l.sag + wobble;
            l.yarn.thickness = (l == _hoverLink) ? 9f : 6f;
            l.yarn.SetVerticesDirty();

            if (l.tag != null)
            {
                l.tag.rectTransform.anchoredPosition = l.yarn.Point(0.5f) + new Vector2(0f, 20f);
            }
        }
    }

    private void UpdateNodeAnimation(float dt)
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            var n = _nodes[i];
            if (n.flash > 0f) n.flash = Mathf.Max(0f, n.flash - dt * 1.8f);
            n.glow.color = new Color(n.flashColor.r, n.flashColor.g, n.flashColor.b, n.flash * 0.55f);
        }
        for (int i = 0; i < _evidence.Count; i++)
        {
            var e = _evidence[i];
            if (e.shake > 0f)
            {
                e.shake = Mathf.Max(0f, e.shake - dt * 3f);
                e.rect.anchoredPosition = e.basePos + new Vector2(Mathf.Sin(Time.unscaledTime * 70f) * 8f * e.shake, 0f);
            }
        }
    }

    // ==========================================
    // Gameplay
    // ==========================================
    private Link AddLink(int ev, int node, bool valid, bool decoy, bool animate)
    {
        var go = new GameObject("Yarn_" + ev + "_" + node, typeof(RectTransform));
        go.transform.SetParent(_yarnLayer, false);
        Stretch((RectTransform)go.transform);
        var yarn = go.AddComponent<YarnGraphic>();
        yarn.raycastTarget = false;
        yarn.color = decoy ? DeceitYellow : Crimson;
        yarn.thickness = 6f;
        yarn.a = _evidence[ev].socket;
        yarn.b = _nodes[node].socket;

        var link = new Link { ev = ev, node = node, valid = valid, decoy = decoy, yarn = yarn };
        link.targetSag = 70f + 14f * ((ev + node) % 3);
        link.sag = animate ? 20f : link.targetSag;
        link.life = animate ? 0f : 1f;
        yarn.sag = link.sag;
        yarn.SetVerticesDirty();

        if (decoy)
        {
            link.tag = MakeText("DeceitTag", _yarnLayer, "M", _typewriterFont, 26, DeceitYellow, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(40f, 36f));
        }

        _links.Add(link);
        return link;
    }

    private void TryConnect(int ev, int node)
    {
        foreach (var l in _links)
        {
            if (l.dying || l.cut) continue;
            if (l.ev == ev && l.node == node)
            {
                SetFeedback("เชื่อมเส้นนี้ไว้แล้ว", Brass);
                return;
            }
        }

        bool valid = Array.IndexOf(_evidence[ev].truth, node) >= 0;
        var link = AddLink(ev, node, valid, false, true);

        if (valid)
        {
            PlayClick(0.6f);
            _nodes[node].flash = 1f;
            _nodes[node].flashColor = GreenInk;
            SetFeedback($"หลักฐานนี้ยืนยัน \"{NodeTitles[node]}\"", GreenInk);
            UpdateStatus();
            CheckSolved();
        }
        else
        {
            Mistakes++;
            link.dying = true;
            link.targetSag = link.sag;
            _evidence[ev].shake = 1f;
            _nodes[node].flash = 1f;
            _nodes[node].flashColor = Crimson;
            CutsceneFX.PlayThud(CamPos(), 0.25f);
            SetFeedback(node == 3 ? "ไม่ใช่ ตู้หน้าสุดคือคำกล่าวอ้างของจดหมาย M ไม่มีหลักฐานจริงรองรับ"
                                  : "หลักฐานชิ้นนี้ไม่ได้ยืนยันข้อสรุปนั้น เส้นด้ายหลุดออก", Crimson);
            UpdateStatus();
        }
    }

    private void CutLink(Link l)
    {
        if (l.cut || l.dying) return;
        l.cut = true;
        l.dyingT = 0f;
        PlayClick(0.5f);

        if (l.decoy)
        {
            _decoyCut = true;
            _nodes[3].flash = 1f;
            _nodes[3].flashColor = GreenInk;
            SetFeedback("ตัดคำลวงของมอริอาร์ตี้แล้ว จดหมายนี้ไม่ได้บอกความจริง", GreenInk);
        }
        else
        {
            SetFeedback("ตัดเส้นที่ถูกต้องออก ต้องเชื่อมใหม่อีกครั้ง", Brass);
        }
        UpdateStatus();
        CheckSolved();
    }

    private int CountValid()
    {
        int n = 0;
        foreach (var l in _links) if (l.valid && !l.dying && !l.cut) n++;
        return n;
    }

    private int CountValid(int node)
    {
        int n = 0;
        foreach (var l in _links) if (l.valid && !l.dying && !l.cut && l.node == node) n++;
        return n;
    }

    private void UpdateStatus()
    {
        int valid = CountValid();
        _statusText.text = $"เส้นด้ายที่ถูกต้อง  <b>{valid}/{RequiredValid}</b>      คำลวงที่ตัดทิ้ง  <b>{(_decoyCut ? 1 : 0)}/1</b>      ต่อผิด  <b>{Mistakes}</b> ครั้ง";

        for (int i = 0; i < _nodes.Count; i++)
        {
            var n = _nodes[i];
            if (n.isDecoy)
            {
                n.counter.text = _decoyCut ? "ตัดคำลวงแล้ว" : "ผูกด้ายโดย M";
                n.counter.color = _decoyCut ? GreenInk : DeceitYellow;
            }
            else
            {
                int c = CountValid(i);
                n.counter.text = $"หลักฐานยืนยัน  {c}/{n.expected}";
                n.counter.color = c >= n.expected ? GreenInk : new Color(Brass.r, Brass.g, Brass.b, 0.95f);
            }
        }
        for (int i = 0; i < _evidence.Count; i++)
        {
            int c = 0;
            foreach (var l in _links) if (l.ev == i && l.valid && !l.dying && !l.cut) c++;
            _evidence[i].linkCount.text = i == 4 ? (_decoyCut ? "ตัดทิ้งแล้ว" : "ถูกวางไว้ให้เชื่อ") : (c > 0 ? $"เชื่อมแล้ว {c}/{_evidence[i].truth.Length}" : "ยังไม่ได้เชื่อม");
            _evidence[i].linkCount.color = (i == 4 ? _decoyCut : c >= _evidence[i].truth.Length) ? GreenInk : InkMuted;
        }
    }

    private void SetFeedback(string text, Color color)
    {
        _feedbackText.text = text;
        _feedbackText.color = color;
        _feedbackTimer = 3.5f;
    }

    private void CheckSolved()
    {
        if (_solved) return;
        if (CountValid() >= RequiredValid && _decoyCut)
        {
            _solved = true;
            StartCoroutine(SolvedRoutine());
        }
    }

    private IEnumerator SolvedRoutine()
    {
        _dragYarn.gameObject.SetActive(false);
        _dragEv = -1;

        _stampText.gameObject.SetActive(true);
        var rt = _stampText.rectTransform;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.25f);
            rt.localScale = Vector3.one * Mathf.Lerp(2.8f, 1f, k);
            _stampText.color = new Color(Crimson.r + 0.15f, Crimson.g, Crimson.b, 0.92f * k);
            yield return null;
        }
        CutsceneFX.PlayThud(CamPos(), 0.6f);
        yield return new WaitForSecondsRealtime(1.4f);

        ShowClimax();
    }

    private static Vector3 CamPos()
    {
        return Camera.main != null ? Camera.main.transform.position : Vector3.zero;
    }

    private static void PlayClick(float volume)
    {
        CutsceneFX.PlayPaperFlip(CamPos(), volume);
    }

    private void OnConfirmClimax()
    {
        CloseMinigame();
        OnDeductionSolved?.Invoke();
    }

    private void ShowClimax()
    {
        _climax.SetActive(true);
        StartCoroutine(FadeClimax());
    }

    private IEnumerator FadeClimax()
    {
        var g = _climax.GetComponent<CanvasGroup>();
        g.alpha = 0f;
        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Clamp01(t / 0.5f);
            yield return null;
        }
        g.alpha = 1f;
    }

    // ==========================================
    // UI
    // ==========================================
    private Vector2 NodeBasePos(int i) { return new Vector2(570f, 285f - i * 190f); }

    private void BuildUI()
    {
        _root = new GameObject("ConnectDots_v2_Canvas", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 955;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        _root.AddComponent<GraphicRaycaster>();
        _rootGroup = _root.AddComponent<CanvasGroup>();

        var bg = NewImage("Backdrop", _root.transform, Vector2.zero, Vector2.zero, new Color(0.04f, 0.03f, 0.025f, 1f));
        Stretch(bg.rectTransform);
        bg.raycastTarget = true;
        Sprite cork = DetectiveUITheme.CorkboardBg;
        if (cork != null)
        {
            var corkImg = NewImage("Cork", _root.transform, Vector2.zero, Vector2.zero, new Color(0.55f, 0.46f, 0.38f, 1f));
            Stretch(corkImg.rectTransform);
            corkImg.sprite = cork;
            corkImg.raycastTarget = false;
        }
        var vignette = NewImage("Vignette", _root.transform, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.9f));
        Stretch(vignette.rectTransform);
        vignette.sprite = DetectiveUIShapes.Vignette;
        vignette.raycastTarget = false;

        _board = NewRect("Board", _root.transform, Vector2.zero, Vector2.zero);
        Stretch(_board);

        BuildHeader();
        BuildEvidenceCards();

        // ชั้นเส้นด้าย: อยู่เหนือการ์ด/ข้อสรุปเล็กน้อยเพื่อให้เห็นเส้นพาดผ่านชัด
        _yarnLayer = NewRect("YarnLayer", _board, Vector2.zero, Vector2.zero);
        Stretch(_yarnLayer);

        BuildNodes();

        var dragGo = new GameObject("DragYarn", typeof(RectTransform));
        dragGo.transform.SetParent(_yarnLayer, false);
        Stretch((RectTransform)dragGo.transform);
        _dragYarn = dragGo.AddComponent<YarnGraphic>();
        _dragYarn.raycastTarget = false;
        _dragYarn.color = new Color(Crimson.r, Crimson.g, Crimson.b, 0.85f);
        _dragYarn.thickness = 5f;
        dragGo.SetActive(false);

        BuildFooter();
        BuildClimax();

        _root.SetActive(false);
    }

    private void BuildHeader()
    {
        var plate = NewImage("HeaderPlate", _board, new Vector2(0f, 462f), new Vector2(1040f, 100f), new Color(Leather.r, Leather.g, Leather.b, 0.96f));
        plate.raycastTarget = false;
        AddFrame(plate.rectTransform, new Vector2(1028f, 88f), 3f, new Color(Brass.r, Brass.g, Brass.b, 0.9f));
        MakeText("Title", plate.transform, "CONNECTING THE DOTS", _typewriterFont, 40, Brass, TextAnchor.MiddleCenter,
            new Vector2(0f, 20f), new Vector2(1000f, 50f));
        MakeText("Subtitle", plate.transform, "ลากเส้นด้ายแดงจากหลักฐานไปยังข้อสรุป  แล้วตัดคำลวงของมอริอาร์ตี้ทิ้ง", thaiFont, 22,
            new Color(0.96f, 0.92f, 0.82f, 1f), TextAnchor.MiddleCenter, new Vector2(0f, -16f), new Vector2(1000f, 30f));
    }

    private void BuildEvidenceCards()
    {
        var defs = new[]
        {
            new EvidenceDef { title = "รอยเลือดบนชานชาลา", desc = "หยดเลือดสดจางลงเรื่อย ๆ ตามทางแยก มุ่งไปชานชาลาที่ 2", truth = new[] { 0 } },
            new EvidenceDef { title = "ปลอกกระสุน Webley .455", desc = "ยังอุ่น ตกใกล้รางฝั่งตู้ท้ายขบวน", truth = new[] { 0, 2 } },
            new EvidenceDef { title = "นาฬิกาพกหยุดเดิน", desc = "กระจกแตกค้างที่ 11:45 PM ตรงเวลาออกของรถด่วน", truth = new[] { 1 } },
            new EvidenceDef { title = "ตั๋วรถไฟครึ่งใบ", desc = "รถด่วนสายเหนือ ชานชาลา 2 ตู้หมายเลข 4 (Carriage 04)", truth = new[] { 0, 1, 2 } },
            new EvidenceDef { title = "จดหมายตราประทับ M", desc = "\"เขาซ่อนตัวอยู่ที่ตู้หน้าสุด\" ลายมือเรียบร้อยเกินไป", truth = new int[0] },
        };

        for (int i = 0; i < defs.Length; i++)
        {
            var d = defs[i];
            d.basePos = new Vector2(-610f, 290f - i * 152f);
            var rect = NewRect("Card_" + i, _board, d.basePos, new Vector2(470f, 142f));
            rect.localEulerAngles = new Vector3(0f, 0f, (i % 2 == 0 ? 1f : -1f) * (0.8f + 0.3f * i));
            d.rect = rect;

            // เงา + กระดาษเก่า (ตัดจากกระดาษของผู้ใช้)
            MakePaper("Shadow", rect, new Vector2(7f, -9f), rect.sizeDelta, new Color(0f, 0f, 0f, 0.5f));
            MakePaper("Paper", rect, Vector2.zero, rect.sizeDelta, i == 4 ? new Color(0.98f, 0.9f, 0.78f, 1f) : Color.white);

            MakeText("Code", rect, i == 4 ? "NOTE-M" : $"EVID-0{i + 1}", _typewriterFont, 15,
                i == 4 ? Crimson : InkMuted, TextAnchor.MiddleLeft, new Vector2(-198f, 56f), new Vector2(200f, 20f), new Vector2(0f, 0.5f));
            var title = MakeText("Title", rect, d.title, thaiFontBold, 25, Ink, TextAnchor.MiddleLeft,
                new Vector2(-198f, 30f), new Vector2(380f, 36f), new Vector2(0f, 0.5f));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 17;
            title.resizeTextMaxSize = 25;
            var desc = MakeText("Desc", rect, d.desc, thaiFont, 17, new Color(Ink.r, Ink.g, Ink.b, 0.9f), TextAnchor.UpperLeft,
                new Vector2(-198f, 10f), new Vector2(390f, 44f), new Vector2(0f, 1f));
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            d.linkCount = MakeText("LinkCount", rect, "", thaiFontBold, 16, InkMuted, TextAnchor.MiddleLeft,
                new Vector2(-198f, -57f), new Vector2(300f, 22f), new Vector2(0f, 0.5f));

            // หมุดแดงปลายการ์ด (จุดเริ่มลากเส้นด้าย)
            d.socket = d.basePos + new Vector2(235f, 0f);
            var pinShadow = NewImage("PinShadow", rect, new Vector2(237f - 0f, -3f), new Vector2(38f, 38f), new Color(0f, 0f, 0f, 0.4f));
            pinShadow.sprite = DetectiveUIShapes.Circle;
            pinShadow.raycastTarget = false;
            d.pinRing = NewImage("PinRing", rect, new Vector2(235f, 0f), new Vector2(54f, 54f), new Color(1f, 0.9f, 0.6f, 0f));
            d.pinRing.sprite = DetectiveUIShapes.Ring(0.1f);
            d.pinRing.raycastTarget = false;
            d.pin = NewImage("Pin", rect, new Vector2(235f, 0f), new Vector2(36f, 36f), i == 4 ? new Color(0.85f, 0.62f, 0.12f, 1f) : Crimson);
            d.pin.sprite = DetectiveUIShapes.Circle;
            d.pin.raycastTarget = false;
            var shine = NewImage("Shine", d.pin.transform, new Vector2(-6f, 6f), new Vector2(11f, 11f), new Color(1f, 1f, 1f, 0.65f));
            shine.sprite = DetectiveUIShapes.Circle;
            shine.raycastTarget = false;

            _evidence.Add(d);
        }
    }

    private void BuildNodes()
    {
        int[] expected = { 3, 2, 2, 0 };
        for (int i = 0; i < 4; i++)
        {
            bool decoy = i == 3;
            Vector2 pos = NodeBasePos(i);
            var rect = NewRect("Node_" + i, _board, pos, new Vector2(560f, 150f));

            var shadow = NewImage("Shadow", rect, new Vector2(8f, -10f), rect.sizeDelta, new Color(0f, 0f, 0f, 0.5f));
            shadow.raycastTarget = false;
            var back = NewImage("Back", rect, Vector2.zero, rect.sizeDelta, decoy ? new Color(0.16f, 0.10f, 0.06f, 0.97f) : new Color(Leather.r, Leather.g, Leather.b, 0.97f));
            back.raycastTarget = false;
            var glow = NewImage("Glow", rect, Vector2.zero, rect.sizeDelta + new Vector2(24f, 24f), new Color(1f, 1f, 1f, 0f));
            glow.sprite = DetectiveUIShapes.Vignette;
            glow.raycastTarget = false;
            Color frame = decoy ? new Color(DeceitYellow.r, DeceitYellow.g, DeceitYellow.b, 0.85f) : new Color(Brass.r, Brass.g, Brass.b, 0.9f);
            AddFrame(rect, new Vector2(548f, 138f), 3.5f, frame);
            AddFrame(rect, new Vector2(530f, 120f), 1.2f, new Color(frame.r, frame.g, frame.b, 0.35f));

            string[] romans = { "I", "II", "III", "?" };
            var tag = NewImage("Tag", rect, new Vector2(-218f, 0f), new Vector2(72f, 100f), new Color(0.05f, 0.035f, 0.025f, 1f));
            tag.raycastTarget = false;
            AddFrame(tag.rectTransform, new Vector2(72f, 100f), 2f, new Color(frame.r, frame.g, frame.b, 0.8f));
            MakeText("Roman", tag.transform, romans[i], thaiFontBold, 40, decoy ? DeceitYellow : Brass, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(72f, 100f));

            MakeText("Name", rect, NodeTitles[i], thaiFontBold, 30, new Color(0.97f, 0.93f, 0.83f, 1f), TextAnchor.MiddleLeft,
                new Vector2(-165f, 34f), new Vector2(420f, 40f), new Vector2(0f, 0.5f));
            MakeText("Sub", rect, NodeSubs[i], _typewriterFont, 15, new Color(frame.r, frame.g, frame.b, 0.95f), TextAnchor.MiddleLeft,
                new Vector2(-165f, 2f), new Vector2(420f, 22f), new Vector2(0f, 0.5f));
            var counter = MakeText("Counter", rect, "", thaiFontBold, 20, Brass, TextAnchor.MiddleLeft,
                new Vector2(-165f, -36f), new Vector2(420f, 28f), new Vector2(0f, 0.5f));

            // ช่องรับหมุดด้านซ้ายของแผ่น
            var socketImg = NewImage("Socket", rect, new Vector2(-292f, 0f), new Vector2(34f, 34f), decoy ? DeceitYellow : Brass);
            socketImg.sprite = DetectiveUIShapes.Circle;
            socketImg.raycastTarget = false;
            var hole = NewImage("Hole", socketImg.transform, Vector2.zero, new Vector2(16f, 16f), new Color(0.07f, 0.05f, 0.03f, 1f));
            hole.sprite = DetectiveUIShapes.Circle;
            hole.raycastTarget = false;

            _nodes.Add(new NodeDef
            {
                title = NodeTitles[i], sub = NodeSubs[i], expected = expected[i], isDecoy = decoy,
                rect = rect, socket = pos + new Vector2(-292f, 0f), glow = glow, socketImg = socketImg, counter = counter,
                flashColor = Brass
            });
        }
    }

    private void BuildFooter()
    {
        _statusText = MakeText("Status", _board, "", thaiFont, 24, new Color(0.97f, 0.93f, 0.83f, 1f), TextAnchor.MiddleCenter,
            new Vector2(0f, 400f - 6f), new Vector2(1000f, 36f));
        _statusText.supportRichText = true;
        var statusShadow = _statusText.gameObject.AddComponent<Shadow>();
        statusShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        statusShadow.effectDistance = new Vector2(1.5f, -1.5f);
        _statusText.rectTransform.anchoredPosition = new Vector2(0f, 390f);

        _feedbackText = MakeText("Feedback", _board, "", thaiFontBold, 26, Brass, TextAnchor.MiddleCenter,
            new Vector2(0f, -462f), new Vector2(1100f, 40f));
        var fShadow = _feedbackText.gameObject.AddComponent<Shadow>();
        fShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        fShadow.effectDistance = new Vector2(1.5f, -1.5f);

        _hintText = MakeText("Hint", _board, "", thaiFont, 20, new Color(0.9f, 0.85f, 0.72f, 0.85f), TextAnchor.MiddleCenter,
            new Vector2(0f, -502f), new Vector2(1300f, 28f));
        var hShadow = _hintText.gameObject.AddComponent<Shadow>();
        hShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        hShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // ตราประทับตอนต่อครบ
        _stampText = MakeText("Stamp", _board, "ROUTE CONFIRMED", _typewriterFont, 76, new Color(Crimson.r, Crimson.g, Crimson.b, 0f), TextAnchor.MiddleCenter,
            new Vector2(-20f, 20f), new Vector2(900f, 110f));
        _stampText.fontStyle = FontStyle.Bold;
        _stampText.rectTransform.localEulerAngles = new Vector3(0f, 0f, -8f);
        _stampText.gameObject.SetActive(false);
    }

    private void BuildClimax()
    {
        _climax = new GameObject("Climax", typeof(RectTransform), typeof(CanvasGroup));
        _climax.transform.SetParent(_root.transform, false);
        Stretch((RectTransform)_climax.transform);
        var dim = NewImage("Dim", _climax.transform, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.78f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        var panel = NewRect("Dossier", _climax.transform, new Vector2(0f, 10f), new Vector2(1240f, 760f));
        MakePaperFull("Shadow", panel, new Vector2(12f, -16f), panel.sizeDelta, new Color(0f, 0f, 0f, 0.55f));
        MakePaperFull("Paper", panel, Vector2.zero, panel.sizeDelta, new Color(1f, 0.97f, 0.9f, 1f));

        MakeText("Label", panel, "THE TRUE ESCAPE ROUTE", _typewriterFont, 22, Crimson, TextAnchor.MiddleCenter,
            new Vector2(0f, 290f), new Vector2(900f, 30f));
        MakeText("Title", panel, "เส้นทางหลบหนีที่แท้จริงของแจ็ค", thaiFontBold, 50, Ink, TextAnchor.MiddleCenter,
            new Vector2(0f, 232f), new Vector2(1000f, 70f));

        // แผนผังเส้นทาง 3 จุด
        string[] stops = { "ชานชาลาที่ 2", "ขบวนด่วน 11:45 PM", "ตู้หมายเลข 4" };
        string[] stopsSub = { "PLATFORM 2", "NORTHERN EXPRESS", "CARRIAGE 4" };
        for (int i = 0; i < 3; i++)
        {
            float x = -380f + i * 380f;
            var plate = NewImage("Stop_" + i, panel, new Vector2(x, 110f), new Vector2(300f, 120f), Leather);
            plate.raycastTarget = false;
            AddFrame(plate.rectTransform, new Vector2(290f, 110f), 3f, Brass);
            MakeText("Name", plate.transform, stops[i], thaiFontBold, 28, new Color(0.97f, 0.93f, 0.83f, 1f), TextAnchor.MiddleCenter,
                new Vector2(0f, 14f), new Vector2(290f, 40f));
            MakeText("Sub", plate.transform, stopsSub[i], _typewriterFont, 15, Brass, TextAnchor.MiddleCenter,
                new Vector2(0f, -22f), new Vector2(290f, 22f));
            if (i < 2)
            {
                var arrow = NewImage("Arrow_" + i, panel, new Vector2(x + 190f, 110f), new Vector2(54f, 30f), Crimson);
                arrow.sprite = WaypointSprites.Chevron;
                arrow.raycastTarget = false;
            }
        }

        var body = MakeText("Body", panel, "เส้นด้ายที่ถูกต้องทั้งเจ็ดเส้นชี้ไปทางเดียวกัน ส่วนจดหมายตราประทับ M ที่ชี้ไป \"ตู้หน้าสุด\" ถูกเขียนมาเพื่อให้เราวิ่งผิดทาง\n\n"
            + "ทุกอย่างบนชานชาลานี้ ทั้งรอยเลือด ปลอกกระสุน นาฬิกา และตั๋ว ถูกจัดวางไว้ล่วงหน้าโดยคนที่รู้ว่าเราจะมาถึงเมื่อไร\n"
            + "แจ็คไม่ได้หนี เขากำลังเดินตามบท และคนเขียนบทคือมอริอาร์ตี้",
            thaiFont, 26, Ink, TextAnchor.UpperCenter, new Vector2(0f, -10f), new Vector2(1060f, 230f), new Vector2(0.5f, 1f));
        body.horizontalOverflow = HorizontalWrapMode.Wrap;
        body.lineSpacing = 1.12f;

        var btnRect = NewRect("Btn_Continue", panel, new Vector2(0f, -310f), new Vector2(420f, 64f));
        var btnImg = btnRect.gameObject.AddComponent<Image>();
        btnImg.color = Color.white;
        var btn = btnRect.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Ink;
        colors.highlightedColor = Crimson;
        colors.selectedColor = Ink;
        colors.pressedColor = new Color(0.4f, 0.05f, 0.05f, 1f);
        colors.colorMultiplier = 1f;
        btn.colors = colors;
        btn.onClick.AddListener(OnConfirmClimax);
        AddFrame(btnRect, new Vector2(410f, 54f), 1.5f, new Color(Brass.r, Brass.g, Brass.b, 0.6f));
        MakeText("Label", btnRect, "ไล่ล่าต่อ  ›", thaiFontBold, 30, new Color(0.97f, 0.93f, 0.83f, 1f), TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(420f, 64f));

        _climax.SetActive(false);
    }

    // ----- UI helpers -----
    private void MakePaper(string name, RectTransform parent, Vector2 pos, Vector2 size, Color color)
    {
        var r = NewRect(name, parent, pos, size);
        var raw = r.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        raw.color = color;
        if (_paperTex != null)
        {
            raw.texture = _paperTex;
            raw.uvRect = SlipUv;
        }
    }

    private void MakePaperFull(string name, RectTransform parent, Vector2 pos, Vector2 size, Color color)
    {
        var r = NewRect(name, parent, pos, size);
        var raw = r.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        raw.color = color;
        if (_paperTex != null) raw.texture = _paperTex;
    }

    private void AddFrame(RectTransform parent, Vector2 size, float width, Color color)
    {
        var rect = NewRect("Frame", parent, Vector2.zero, size);
        var frame = rect.gameObject.AddComponent<UIParallelogramGraphic>();
        frame.skewAmount = 0f;
        frame.outlineWidth = width;
        frame.outlineColor = color;
        frame.fillColor = new Color(0f, 0f, 0f, 0f);
        frame.raycastTarget = false;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = new Vector2(0.5f, 0.5f);
        r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        return r;
    }

    private static Image NewImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var r = NewRect(name, parent, pos, size);
        var img = r.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Text MakeText(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor align,
        Vector2 pos, Vector2 box, Vector2? pivot = null)
    {
        var r = NewRect(name, parent, pos, box);
        if (pivot.HasValue) r.pivot = pivot.Value;
        r.anchoredPosition = pos;
        var t = r.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
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

/// <summary>
/// เส้นด้ายหนาโค้งหย่อนตามแรงโน้มถ่วง (Bezier กำลังสอง) พร้อมเงา ใช้ในมินิเกมเชื่อมโยงหลักฐาน
/// พิกัด a/b อยู่ในระบบพิกัดกึ่งกลาง RectTransform ของตัวมันเอง (ตั้ง Rect ให้เต็มกระดาน)
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class YarnGraphic : MaskableGraphic
{
    public Vector2 a;
    public Vector2 b;
    public float sag = 70f;
    public float thickness = 6f;
    public int segments = 36;

    public Vector2 Point(float t)
    {
        Vector2 c = (a + b) * 0.5f + new Vector2(0f, -sag);
        float u = 1f - t;
        return u * u * a + 2f * u * t * c + t * t * b;
    }

    /// <summary>ระยะที่ใกล้ที่สุดจากจุด p ถึงเส้นด้าย</summary>
    public float DistanceTo(Vector2 p)
    {
        float best = float.MaxValue;
        Vector2 prev = Point(0f);
        for (int i = 1; i <= 24; i++)
        {
            Vector2 cur = Point(i / 24f);
            best = Mathf.Min(best, DistToSegment(p, prev, cur));
            prev = cur;
        }
        return best;
    }

    private static float DistToSegment(Vector2 p, Vector2 s0, Vector2 s1)
    {
        Vector2 d = s1 - s0;
        float len2 = d.sqrMagnitude;
        if (len2 < 1e-4f) return Vector2.Distance(p, s0);
        float t = Mathf.Clamp01(Vector2.Dot(p - s0, d) / len2);
        return Vector2.Distance(p, s0 + d * t);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        // เงาด้านล่างก่อน แล้วค่อยวาดเส้นหลัก
        AddStrip(vh, new Vector2(4f, -6f), thickness + 1f, new Color(0f, 0f, 0f, 0.35f * color.a));
        AddStrip(vh, Vector2.zero, thickness, color);
        // ไฮไลต์เส้นบาง ๆ ให้ดูเป็นเกลียวด้าย
        Color hl = Color.Lerp(color, Color.white, 0.35f);
        hl.a = color.a * 0.55f;
        AddStrip(vh, new Vector2(0f, 1.5f), Mathf.Max(1.5f, thickness * 0.28f), hl);
    }

    private void AddStrip(VertexHelper vh, Vector2 offset, float width, Color col)
    {
        int start = vh.currentVertCount;
        Vector2 prev = Point(0f) + offset;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector2 p = Point(t) + offset;
            Vector2 next = Point(Mathf.Min(1f, t + 1f / segments)) + offset;
            Vector2 dir = (next - prev);
            if (dir.sqrMagnitude < 1e-6f) dir = (b - a);
            dir.Normalize();
            Vector2 n = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            vh.AddVert(p + n, col, Vector2.zero);
            vh.AddVert(p - n, col, Vector2.one);
            prev = p;
        }
        for (int i = 0; i < segments; i++)
        {
            int k = start + i * 2;
            vh.AddTriangle(k, k + 1, k + 3);
            vh.AddTriangle(k, k + 3, k + 2);
        }
    }
}
