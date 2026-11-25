using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// เมนูเริ่มเกม (Title Screen) สไตล์ AAA: ชื่อเกมตัวแดงเว้นระยะตัวอักษร เมนูชิดซ้าย คำอธิบายด้านล่าง
/// ฉากหลังเป็นโลกในเกมจริงแบบเบลอ พร้อมสารวัตรนิวแมนยืนเท่ ๆ ทางขวาของจอ และสะเก็ดไฟลอยขึ้น
/// START เริ่มเกม / OPTIONS ปรับเสียง+เต็มจอ / CREDITS รายชื่อผู้พัฒนา / EXIT ออกจากเกม
/// (เมนูนี้แยกจากเมนู "Play Round 2?" ในฉากจบ)
/// </summary>
public class TitleMenuUI : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private const string GameTitle = "CPE447";

    private static bool _hooked;

    // RuntimeInitializeOnLoadMethod ทำงานครั้งเดียวต่อการกด Play จึงต้องผูกกับ sceneLoaded ด้วย
    // เพื่อให้เมนูกลับมาทุกครั้งที่โหลดฉากใหม่ (จบเกม / ESC -> Leave the Case)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (!_hooked)
        {
            _hooked = true;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => CreateIfMissing();
        }
        CreateIfMissing();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _hooked = false;
        IsOpen = false;
    }

    private static void CreateIfMissing()
    {
        if (FindAnyObjectByType<TitleMenuUI>() != null) return;
        new GameObject("TitleMenuUI").AddComponent<TitleMenuUI>();
    }

    // ----- ข้อมูลเมนู -----
    private static readonly string[] Items = { "Play", "Options", "Credits", "Exit" };
    private static readonly string[] Numerals = { "I.", "II.", "III.", "IV." };
    private static readonly string[] Descriptions =
    {
        "เริ่มสืบคดีฆาตกรรมต่อเนื่องแห่งไวท์ชาเปล  ลอนดอน ค.ศ. 1888",
        "ปรับระดับเสียงและการแสดงผล",
        "รายชื่อผู้พัฒนาเกม",
        "ออกจากเกม",
    };

    private struct CreditRow
    {
        public string role, id, name;
        public CreditRow(string role, string id, string name) { this.role = role; this.id = id; this.name = name; }
    }

    private static readonly CreditRow[] Credits =
    {
        new CreditRow("AI  ·  CiRA CORE", "66002666", "ธัญวรัตน์ จันทร์โหนง"),
        new CreditRow("3D MODEL", "66003065", "ประไพพิศ สวัสดิ์รักษา"),
        new CreditRow("3D MODEL", "66015312", "ชนัญญา เถระ"),
        new CreditRow("GAME DEVELOPER", "66019754", "จุลจักร จิรยะเทพถาวร"),
    };

    // ----- UI -----
    private CanvasGroup _rootGroup;
    private RectTransform _root;
    private Image _black;
    private readonly List<Text> _labels = new List<Text>();
    private readonly List<Image> _markers = new List<Image>();
    private Text _desc;
    private GameObject _optionsPanel;
    private GameObject _creditsPanel;
    private Text _volumeValue;
    private Text _fullscreenValue;
    private int _optionRow;
    private readonly List<Text> _optionLabels = new List<Text>();

    private Font _thai;
    private Font _thaiBold;
    private Font _mono;

    private int _index;
    private bool _busy;
    private enum Page { Main, Options, Credits }
    private Page _page = Page.Main;

    // ----- ฉากหลัง -----
    private class Ember
    {
        public RectTransform rect;
        public Image img;
        public float speed;
        public float sway;
        public float phase;
        public float life;
    }
    private readonly List<Ember> _embers = new List<Ember>();
    private Quaternion _playerRotBefore;
    private Transform _playerBody;

    private static readonly Color Brass = new Color(0.86f, 0.70f, 0.36f, 1f);
    private static readonly Color TitleRed = new Color(0.66f, 0.11f, 0.10f, 1f);   // หมึกแดงแบบตราประทับในเกม
    private static readonly Color Cream = new Color(0.96f, 0.92f, 0.82f, 1f);      // สีกระดาษเก่า
    private static readonly Color Muted = new Color(0.70f, 0.64f, 0.54f, 1f);
    private Font _serif;
    private Font _titleSerif;

    private void Start()
    {
        _thai = GameAssets.Load<Font>("Assets/Font/Sarabun-Medium.ttf");
        _thaiBold = GameAssets.Load<Font>("Assets/Font/Prompt-Bold.ttf");
        if (_thai == null) _thai = VisionOSUITheme.PrimaryFont;
        if (_thaiBold == null) _thaiBold = VisionOSUITheme.BoldFont;
        _mono = Font.CreateDynamicFontFromOSFont(new[] { "Courier New", "Consolas" }, 24);
        if (_mono == null) _mono = _thaiBold;
        _serif = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman", "Cambria" }, 32);
        if (_serif == null) _serif = _thaiBold;
        // Georgia ใช้ตัวเลขแบบ old-style (4, 7 ห้อยลงใต้บรรทัด) ชื่อเกมที่มีตัวเลขจึงใช้ฟอนต์ที่ตัวเลขสูงเท่าตัวพิมพ์ใหญ่
        _titleSerif = Font.CreateDynamicFontFromOSFont(new[] { "Times New Roman", "Cambria", "Georgia" }, 32);
        if (_titleSerif == null) _titleSerif = _serif;

        IsOpen = true;
        BuildUI();
        StartCoroutine(OpenRoutine());
    }

    private void OnDestroy()
    {
        IsOpen = false;
    }

    // ==========================================
    // เปิด / เข้าเกม
    // ==========================================
    private IEnumerator OpenRoutine()
    {
        // รอให้ระบบอื่นตั้งค่าเสร็จก่อน 1 เฟรม แล้วค่อยล็อกเกมเพลย์
        yield return null;
        LockGameplay(true);
        SetupHeroShot();

        float t = 0f;
        while (t < 1.4f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 1.4f);
            _black.color = new Color(0f, 0f, 0f, 1f - k);
            _rootGroup.alpha = Mathf.Clamp01((t - 0.4f) / 0.8f);
            yield return null;
        }
        _black.color = new Color(0f, 0f, 0f, 0f);
        _rootGroup.alpha = 1f;
    }

    private void SetupHeroShot()
    {
        var mgr = CinematicCutsceneManager.Instance;
        var cc = FindAnyObjectByType<CharacterController>();
        if (mgr == null || cc == null) return;

        _playerBody = cc.transform;
        _playerRotBefore = _playerBody.rotation;

        Vector3 head = mgr.GetNewmanHeadPosition();
        Vector3 fwd = Shot.Flat(_playerBody.forward);
        Vector3 right = Shot.Right(fwd);

        // กล้องอยู่ด้านหน้าค่อนข้างต่ำ (Low-Angle Hero) นิวแมนอยู่ทางขวาของเฟรม ฝั่งซ้ายว่างไว้ให้ชื่อเกมและเมนู
        Vector3 camPos = head + fwd * 3.7f + right * 0.35f - Vector3.up * 0.55f;
        Vector3 aim = head + right * 1.05f - Vector3.up * 0.35f;

        // นิวแมนหันเฉียงมองไปทางซ้ายของจอ (ทางเดียวกับเมนู) แบบภาพปกเกม
        Vector3 toCam = Shot.Flat(camPos - _playerBody.position);
        Vector3 face = (toCam * 0.72f + right * 0.69f).normalized;
        bool wasEnabled = cc.enabled;
        cc.enabled = false;
        _playerBody.rotation = Quaternion.LookRotation(face, Vector3.up);
        cc.enabled = wasEnabled;

        var shot = Shot.Move(camPos, camPos + fwd * -0.25f + Vector3.up * 0.05f, aim, aim + Vector3.up * 0.04f, 40f)
            .Lens(34f, 32f).Shake(0.18f).Dof(55f, 1.8f, 0f).Eased(ShotEase.Linear);
        mgr.PlayShots(mgr.camNewmanClose != null ? mgr.camNewmanClose : mgr.camChurchWide, shot);

        // ใช้ท่ายืน idle ปกติของตัวละคร (ดูเท่และเป็นธรรมชาติที่สุด)
    }

    private static void LockGameplay(bool locked)
    {
        var mgr = CinematicCutsceneManager.Instance;
        if (mgr != null) mgr.SetPlayerControlsLocked(locked);
        CiraCoreVisionScanner.IsScannerBlocked = locked;
        if (locked)
        {
            if (CiraCoreVisionScanner.Instance != null) CiraCoreVisionScanner.Instance.ForceCloseScanMode();
            if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
            if (WaypointNavigationSystem.Instance != null) WaypointNavigationSystem.Instance.SetVisible(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private IEnumerator StartGameRoutine()
    {
        _busy = true;
        GameEndingSequence.ReturnedFromEnding = false;

        yield return FadeBlack(0f, 1f, 0.7f);

        // คืนกล้อง/ทิศของตัวละคร แล้วซ่อนเมนู (ยังคง IsOpen = true จนจบบทนำ เพื่อกัน ESC/E)
        var mgr = CinematicCutsceneManager.Instance;
        if (mgr != null) mgr.ResetAllCutsceneCameras();
        if (_playerBody != null)
        {
            var cc = _playerBody.GetComponent<CharacterController>();
            bool was = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            _playerBody.rotation = _playerRotBefore;
            if (cc != null) cc.enabled = was;
        }
        _root.gameObject.SetActive(false);

        // การ์ดบทนำบนจอดำ
        yield return ChapterCardRoutine();

        // บทนำภาพยนตร์: เรามาเจอคดีแรกได้อย่างไร
        bool done = false;
        if (mgr != null)
        {
            mgr.PlayPrologueCinematic(OnPrologueLine, () => done = true);
            yield return FadeBlack(1f, 0f, 1.2f);
            while (!done) yield return null;
        }
        else
        {
            yield return FadeBlack(1f, 0f, 0.8f);
        }

        IsOpen = false;
        LockGameplay(false);
        if (StMaryChurchQuestManager.Instance != null && !StMaryChurchQuestManager.Instance.IsQuest1Finished)
        {
            StMaryChurchQuestManager.Instance.ShowObjectiveHUD(true);
            if (WaypointNavigationSystem.Instance != null) WaypointNavigationSystem.Instance.SetVisible(true);
        }
        Destroy(gameObject);
    }

    private IEnumerator FadeBlack(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        _black.color = new Color(0f, 0f, 0f, to);
    }

    private IEnumerator ChapterCardRoutine()
    {
        var card = NewRect("ChapterCard", transform, Vector2.zero, Vector2.zero);
        Stretch(card);
        var group = card.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        MakeText("Kicker", card, "P R O L O G U E", _mono, 24, Brass, TextAnchor.MiddleCenter, new Vector2(0f, 70f), new Vector2(1200f, 34f), null);
        var t1 = MakeText("Title", card, "คืนแห่งหมอก", _thaiBold, 72, Cream, TextAnchor.MiddleCenter, new Vector2(0f, -4f), new Vector2(1400f, 100f), null);
        t1.lineSpacing = 0.9f;
        var rule = NewImage("Rule", card, new Vector2(0f, -66f), new Vector2(260f, 2f), Brass);
        rule.raycastTarget = false;
        var t2 = MakeText("Place", card, "ไวท์ชาเปล  ·  ลอนดอน  ·  ค.ศ. 1888", _thai, 26, Muted, TextAnchor.MiddleCenter, new Vector2(0f, -102f), new Vector2(1200f, 34f), null);
        t2.fontStyle = FontStyle.Italic;

        // การ์ดต้องอยู่เหนือม่านดำ
        card.SetAsLastSibling();

        float t = 0f;
        while (t < 0.8f) { t += Time.unscaledDeltaTime; group.alpha = Mathf.Clamp01(t / 0.8f); yield return null; }
        yield return new WaitForSecondsRealtime(2.2f);
        t = 0f;
        while (t < 0.7f) { t += Time.unscaledDeltaTime; group.alpha = 1f - Mathf.Clamp01(t / 0.7f); yield return null; }
        Destroy(card.gameObject);
    }

    /// <summary>บทนำแจ้งเลขบรรทัด: บรรทัดที่ 1 แสดงป้ายชื่อแนะนำตัวละครแบบเกม AAA</summary>
    private void OnPrologueLine(int line)
    {
        if (line == 1) StartCoroutine(NameTagRoutine());
    }

    private IEnumerator NameTagRoutine()
    {
        var tag = NewRect("NameTag", transform, new Vector2(-640f, -150f), new Vector2(700f, 120f));
        var group = tag.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        var bar = NewImage("Bar", tag, new Vector2(-340f, 0f), new Vector2(4f, 96f), Brass);
        bar.raycastTarget = false;
        var name = MakeText("Name", tag, "สารวัตรนิวแมน", _thaiBold, 50, Cream, TextAnchor.MiddleLeft, new Vector2(-322f, 18f), new Vector2(660f, 64f), new Vector2(0f, 0.5f));
        var nShadow = name.gameObject.AddComponent<Shadow>();
        nShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        nShadow.effectDistance = new Vector2(2f, -2f);
        var role = MakeText("Role", tag, "INSPECTOR NEWMAN  ·  SCOTLAND YARD", _mono, 20, Brass, TextAnchor.MiddleLeft, new Vector2(-320f, -30f), new Vector2(660f, 28f), new Vector2(0f, 0.5f));
        var rShadow = role.gameObject.AddComponent<Shadow>();
        rShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        rShadow.effectDistance = new Vector2(1.5f, -1.5f);

        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.6f);
            group.alpha = k;
            tag.anchoredPosition = new Vector2(Mathf.Lerp(-700f, -640f, 1f - (1f - k) * (1f - k)), -150f);
            yield return null;
        }
        yield return new WaitForSecondsRealtime(3.6f);
        t = 0f;
        while (t < 0.6f) { t += Time.unscaledDeltaTime; group.alpha = 1f - Mathf.Clamp01(t / 0.6f); yield return null; }
        Destroy(tag.gameObject);
    }

    // ==========================================
    // Update: อินพุต + สะเก็ดไฟ
    // ==========================================
    private void Update()
    {
        UpdateEmbers(Time.unscaledDeltaTime);
        if (!IsOpen || _busy) return;

        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        int move = KeyMove();
        bool confirm = KeyConfirm();
        bool back = KeyBack();
        int lr = KeyLeftRight();

        if (_page == Page.Main)
        {
            if (move != 0) Select((_index + move + Items.Length) % Items.Length);
            if (confirm) Activate(_index);
        }
        else if (_page == Page.Options)
        {
            if (move != 0) { _optionRow = (_optionRow + move + 3) % 3; RefreshOptions(); }
            if (lr != 0) ChangeOption(_optionRow, lr);
            if (confirm) { if (_optionRow == 2) ShowPage(Page.Main); else ChangeOption(_optionRow, 1); }
            if (back) ShowPage(Page.Main);
        }
        else if (_page == Page.Credits)
        {
            if (back || confirm) ShowPage(Page.Main);
        }

        // ระหว่างเมนูเปิด ซ่อน HUD เควสตลอด (กันระบบอื่นเปิดกลับ)
        if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
    }

    private void Select(int i)
    {
        _index = i;
        for (int k = 0; k < _labels.Count; k++)
        {
            bool sel = k == _index;
            bool redPlay = k == 0 && GameEndingSequence.ReturnedFromEnding;
            _labels[k].color = sel ? (redPlay ? new Color(0.92f, 0.3f, 0.26f, 1f) : Cream) : new Color(Muted.r, Muted.g, Muted.b, 0.75f);
            _labels[k].fontSize = sel ? 36 : 28;
            _labels[k].fontStyle = sel ? FontStyle.Bold : FontStyle.Italic;
            _labels[k].rectTransform.anchoredPosition = new Vector2(sel ? -560f : -540f, _labels[k].rectTransform.anchoredPosition.y);
            _markers[k].enabled = sel;
        }
        _desc.text = Descriptions[_index];
    }

    private void Activate(int i)
    {
        switch (i)
        {
            case 0: StartCoroutine(StartGameRoutine()); break;
            case 1: ShowPage(Page.Options); break;
            case 2: ShowPage(Page.Credits); break;
            case 3:
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }

    private void ShowPage(Page p)
    {
        _page = p;
        _optionsPanel.SetActive(p == Page.Options);
        _creditsPanel.SetActive(p == Page.Credits);
        foreach (var l in _labels) l.gameObject.SetActive(p == Page.Main);
        foreach (var m in _markers) m.gameObject.SetActive(p == Page.Main);
        if (p == Page.Main) { Select(_index); }
        else if (p == Page.Options) { _optionRow = 0; RefreshOptions(); _desc.text = "[A/D] ปรับค่า   [ESC] กลับ"; }
        else _desc.text = "[ESC] หรือ [Enter] กลับ";
    }

    private void ChangeOption(int row, int dir)
    {
        if (row == 0)
        {
            AudioListener.volume = Mathf.Clamp01(Mathf.Round((AudioListener.volume + dir * 0.1f) * 10f) / 10f);
        }
        else if (row == 1)
        {
            Screen.fullScreen = !Screen.fullScreen;
        }
        RefreshOptions();
    }

    private void RefreshOptions()
    {
        _volumeValue.text = $"‹  {Mathf.RoundToInt(AudioListener.volume * 100f)}%  ›";
        _fullscreenValue.text = Screen.fullScreen ? "‹  เปิด  ›" : "‹  ปิด  ›";
        for (int i = 0; i < _optionLabels.Count; i++)
        {
            _optionLabels[i].color = i == _optionRow ? Color.white : Muted;
        }
    }

    private void UpdateEmbers(float dt)
    {
        foreach (var e in _embers)
        {
            e.life += dt;
            var p = e.rect.anchoredPosition;
            p.y += e.speed * dt;
            p.x += Mathf.Sin(e.life * e.sway + e.phase) * 18f * dt;
            if (p.y > 600f)
            {
                p.y = -600f;
                p.x = Random.Range(-980f, 980f);
            }
            e.rect.anchoredPosition = p;
            float flicker = 0.55f + 0.45f * Mathf.Sin(e.life * 6f + e.phase);
            var c = e.img.color;
            e.img.color = new Color(c.r, c.g, c.b, flicker * 0.9f);
        }
    }

    // ==========================================
    // Input
    // ==========================================
    private static int KeyMove()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return 0;
        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) return -1;
        if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) return 1;
        return 0;
#else
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) return -1;
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) return 1;
        return 0;
#endif
    }

    private static int KeyLeftRight()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return 0;
        if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) return -1;
        if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) return 1;
        return 0;
#else
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) return -1;
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) return 1;
        return 0;
#endif
    }

    private static bool KeyConfirm()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#endif
    }

    private static bool KeyBack()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace);
#endif
    }

    // ==========================================
    // UI build
    // ==========================================
    private void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 990;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        _root = NewRect("Root", transform, Vector2.zero, Vector2.zero);
        Stretch(_root);
        _rootGroup = _root.gameObject.AddComponent<CanvasGroup>();
        _rootGroup.alpha = 0f;

        // ไล่เฉดมืดจากซ้าย ให้ตัวหนังสืออ่านง่ายและนิวแมนเด่นทางขวา
        var grad = NewRect("LeftShade", _root, Vector2.zero, Vector2.zero);
        Stretch(grad);
        var gradRaw = grad.gameObject.AddComponent<RawImage>();
        gradRaw.texture = BuildHorizontalGradient();
        gradRaw.raycastTarget = true; // กันคลิกทะลุไปเกม
        var bottom = NewRect("BottomShade", _root, new Vector2(0f, -470f), new Vector2(1920f, 220f));
        var bottomRaw = bottom.gameObject.AddComponent<RawImage>();
        bottomRaw.texture = BuildVerticalGradient();
        bottomRaw.raycastTarget = false;
        var vig = NewImage("Vignette", _root, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.85f));
        Stretch(vig.rectTransform);
        vig.sprite = DetectiveUIShapes.Vignette;
        vig.raycastTarget = false;

        // สะเก็ดไฟลอยขึ้น
        for (int i = 0; i < 46; i++)
        {
            float size = Random.Range(2f, 6.5f);
            var img = NewImage("Ember", _root, new Vector2(Random.Range(-980f, 980f), Random.Range(-600f, 600f)), new Vector2(size, size),
                Random.value < 0.6f ? new Color(1f, 0.55f, 0.18f, 0.8f) : new Color(1f, 0.82f, 0.4f, 0.8f));
            img.sprite = DetectiveUIShapes.Circle;
            img.raycastTarget = false;
            _embers.Add(new Ember { rect = img.rectTransform, img = img, speed = Random.Range(18f, 55f), sway = Random.Range(0.6f, 1.8f), phase = Random.Range(0f, 6.28f) });
        }

        // ชื่อเกม: ตัวแดงเว้นระยะ + เส้นใต้
        const float left = -560f;
        MakeText("Kicker", _root, "SCOTLAND YARD   ·   CASE FILE 1888/JR", _mono, 20, new Color(Brass.r, Brass.g, Brass.b, 0.85f), TextAnchor.MiddleLeft,
            new Vector2(left, 268f), new Vector2(900f, 28f), new Vector2(0f, 0.5f));
        var title = MakeText("Title", _root, SpacedTitle(GameTitle), _titleSerif, 120, Cream, TextAnchor.MiddleLeft,
            new Vector2(left - 6f, 184f), new Vector2(1000f, 150f), new Vector2(0f, 0.5f));
        title.fontStyle = FontStyle.Bold;
        var titleShadow = title.gameObject.AddComponent<Shadow>();
        titleShadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        titleShadow.effectDistance = new Vector2(3f, -3f);
        float underline = Mathf.Max(420f, title.preferredWidth);
        var line = NewImage("Underline", _root, new Vector2(left + underline * 0.5f, 104f), new Vector2(underline, 3f), Brass);
        line.raycastTarget = false;
        var seal = NewImage("Seal", _root, new Vector2(left + underline + 14f, 104f), new Vector2(16f, 16f), TitleRed);
        seal.sprite = DetectiveUIShapes.Circle;
        seal.raycastTarget = false;
        var sub = MakeText("Subtitle", _root, "คดีปริศนาแห่งไวท์ชาเปล", _thai, 28, new Color(Muted.r, Muted.g, Muted.b, 0.95f), TextAnchor.MiddleLeft,
            new Vector2(left, 72f), new Vector2(700f, 36f), new Vector2(0f, 0.5f));
        sub.fontStyle = FontStyle.Italic;

        // เมนูหลัก
        bool round2 = GameEndingSequence.ReturnedFromEnding;
        for (int i = 0; i < Items.Length; i++)
        {
            int idx = i;
            float y = -20f - i * 64f;
            string label = Numerals[i] + "   " + ((i == 0 && round2) ? "Play Round 2?" : Items[i]);
            var text = MakeText("Item_" + i, _root, label, _serif, 30, Muted, TextAnchor.MiddleLeft,
                new Vector2(left, y), new Vector2(560f, 54f), new Vector2(0f, 0.5f));
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            text.raycastTarget = true;
            var btn = text.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { if (!_busy && _page == Page.Main) { Select(idx); Activate(idx); } });
            var hover = text.gameObject.AddComponent<StationHoverHandler>();
            hover.onEnter = () => { if (!_busy && _page == Page.Main) Select(idx); };
            _labels.Add(text);

            var marker = NewImage("Marker_" + i, _root, new Vector2(left - 26f, y), new Vector2(12f, 12f), Brass);
            marker.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            marker.raycastTarget = false;
            _markers.Add(marker);
        }

        _desc = MakeText("Description", _root, "", _thai, 26, new Color(Cream.r, Cream.g, Cream.b, 0.88f), TextAnchor.MiddleCenter,
            new Vector2(0f, -470f), new Vector2(1600f, 40f), null);
        var descShadow = _desc.gameObject.AddComponent<Shadow>();
        descShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        descShadow.effectDistance = new Vector2(1.5f, -1.5f);
        MakeText("Keys", _root, "[W/S] เลือก    [Enter] ยืนยัน", _thai, 20, new Color(Muted.r, Muted.g, Muted.b, 0.75f), TextAnchor.MiddleRight,
            new Vector2(900f, -510f), new Vector2(600f, 28f), new Vector2(1f, 0.5f));

        BuildOptionsPanel(left);
        BuildCreditsPanel(left);

        Select(0);

        // ม่านดำสำหรับเฟดเข้า/ออก
        _black = NewImage("Black", transform, Vector2.zero, Vector2.zero, Color.black);
        Stretch(_black.rectTransform);
        _black.raycastTarget = false;
    }

    private void BuildOptionsPanel(float left)
    {
        _optionsPanel = NewRect("Options", _root, Vector2.zero, Vector2.zero).gameObject;
        Stretch((RectTransform)_optionsPanel.transform);
        MakeText("Header", _optionsPanel.transform, "Options", _serif, 36, Cream, TextAnchor.MiddleLeft,
            new Vector2(left, 20f), new Vector2(500f, 50f), new Vector2(0f, 0.5f));
        string[] rows = { "ระดับเสียงรวม", "เต็มจอ", "กลับ" };
        for (int i = 0; i < rows.Length; i++)
        {
            int row = i;
            float y = -50f - i * 58f;
            var label = MakeText("Row_" + i, _optionsPanel.transform, rows[i], _thaiBold, 28, Muted, TextAnchor.MiddleLeft,
                new Vector2(left, y), new Vector2(300f, 46f), new Vector2(0f, 0.5f));
            label.raycastTarget = true;
            var btn = label.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                _optionRow = row;
                if (row == 2) ShowPage(Page.Main); else ChangeOption(row, 1);
            });
            _optionLabels.Add(label);
        }
        _volumeValue = MakeText("Volume", _optionsPanel.transform, "", _thaiBold, 28, Cream, TextAnchor.MiddleLeft,
            new Vector2(left + 300f, -50f), new Vector2(260f, 46f), new Vector2(0f, 0.5f));
        _fullscreenValue = MakeText("Fullscreen", _optionsPanel.transform, "", _thaiBold, 28, Cream, TextAnchor.MiddleLeft,
            new Vector2(left + 300f, -108f), new Vector2(260f, 46f), new Vector2(0f, 0.5f));
        _optionsPanel.SetActive(false);
    }

    private void BuildCreditsPanel(float left)
    {
        _creditsPanel = NewRect("Credits", _root, Vector2.zero, Vector2.zero).gameObject;
        Stretch((RectTransform)_creditsPanel.transform);
        MakeText("Header", _creditsPanel.transform, "Credits", _serif, 36, Cream, TextAnchor.MiddleLeft,
            new Vector2(left, 20f), new Vector2(500f, 50f), new Vector2(0f, 0.5f));
        float y = -40f;
        string lastRole = null;
        foreach (var c in Credits)
        {
            if (c.role != lastRole)
            {
                MakeText("Role", _creditsPanel.transform, c.role, _mono, 20, Brass, TextAnchor.MiddleLeft,
                    new Vector2(left, y), new Vector2(600f, 28f), new Vector2(0f, 0.5f));
                y -= 34f;
                lastRole = c.role;
            }
            MakeText("Name", _creditsPanel.transform, c.name, _thaiBold, 28, Cream, TextAnchor.MiddleLeft,
                new Vector2(left + 20f, y), new Vector2(500f, 38f), new Vector2(0f, 0.5f));
            MakeText("Id", _creditsPanel.transform, c.id, _mono, 20, Muted, TextAnchor.MiddleLeft,
                new Vector2(left + 380f, y), new Vector2(200f, 30f), new Vector2(0f, 0.5f));
            y -= 46f;
        }
        _creditsPanel.SetActive(false);
    }

    private static string SpacedTitle(string s)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            sb.Append(s[i]);
            if (i < s.Length - 1) sb.Append(' ');
        }
        return sb.ToString();
    }

    private static Texture2D BuildHorizontalGradient()
    {
        const int w = 256;
        var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < w; x++)
        {
            float u = x / (float)(w - 1);
            float a = Mathf.Lerp(0.82f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.62f)));
            tex.SetPixel(x, 0, new Color(0.03f, 0.02f, 0.02f, a));
        }
        tex.Apply();
        return tex;
    }

    private static Texture2D BuildVerticalGradient()
    {
        const int h = 64;
        var tex = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            tex.SetPixel(0, y, new Color(0f, 0f, 0f, Mathf.Lerp(0.75f, 0f, v)));
        }
        tex.Apply();
        return tex;
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
        Vector2 pos, Vector2 box, Vector2? pivot)
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
