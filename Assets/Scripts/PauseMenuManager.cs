using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// เมนูหยุดชั่วคราว (Pause Menu) สไตล์ "แฟ้มคดีสกอตแลนด์ยาร์ด 1888" แบบ 3 มิติ:
/// - ตอนกด ESC จะถ่ายภาพหน้าจอ แล้วเบลอ ลดสี และทำให้มืดลงโทนซีเปีย ใช้เป็นฉากหลังของเมนู (ไม่ขึ้นกับ Post-processing ของฉาก)
/// - เมนูเป็นแผ่นกระดาษบันทึกคดีเก่า ๆ ลอยเอียงอยู่ทางขวาของจอ หันเข้าหาตัวละครตามแนวเพอร์สเปกทีฟของฉาก
///   ตัวอักษรแบบ Serif / พิมพ์ดีด หมึกสีน้ำตาลเข้ม มีตราประทับ CONFIDENTIAL และเส้นใต้หมึกแดงที่รายการที่เลือก
/// - เมนูอยู่บน Canvas แบบ WorldSpace ที่วางไว้ไกลจากฉาก และเรนเดอร์ด้วยกล้อง Overlay แยก (URP Camera Stack)
///   ทำให้ตัวละครหรือฉากบังเมนูไม่ได้ 100%
/// - ใช้เมาส์ชี้/คลิก หรือคีย์บอร์ด (W/S หรือลูกศร + Enter) ได้ทุกหน้า
/// - ซ่อนแถบ Objective HUD / Waypoint ชั่วคราวขณะหยุดเกม
/// - เมนูหลัก: Resume Investigation (เล่นต่อ), Options (ตั้งค่า), Leave the Case (ออกจากคดี)
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    private static PauseMenuManager _instance;
    public static PauseMenuManager Instance => _instance;

    [Header("=== State ===")]
    public bool isPaused = false;

    [Header("=== Style ===")]
    [Tooltip("ฟอนต์หัวข้อ/รายการเมนู (ว่างไว้ = Georgia จากระบบ)")]
    public Font serifFont;
    [Tooltip("ฟอนต์พิมพ์ดีดสำหรับข้อความเล็ก (ว่างไว้ = Courier New จากระบบ)")]
    public Font typewriterFont;
    [Tooltip("หมายเลขคดีที่แสดงบนหัวกระดาษ")]
    public string caseNumber = "CASE No. 1888/JR";

    private Canvas _canvas;
    private CanvasGroup _menuCanvasGroup;
    private RectTransform _menu3DAnchor;
    private Text _titleText;
    private GameObject _mainMenuContainer;
    private GameObject _settingsContainer;
    private GameObject _confirmQuitContainer;

    // รายการที่เลือกได้ของหน้าปัจจุบัน (ใช้ร่วมกันระหว่างเมาส์และคีย์บอร์ด)
    private DossierMenuRow[] _mainRows;
    private DossierMenuRow[] _settingsRows;
    private DossierMenuRow[] _confirmRows;
    private DossierMenuRow[] _activeRows;
    private int _selectedIndex = 0;
    private int _mainSelectedIndex = 0;

    // 3D Menu Rig (Canvas WorldSpace + กล้อง Overlay แยกจากฉาก)
    private static readonly Vector3 MenuRigWorldPosition = new Vector3(0f, -5000f, 0f);
    private const int MenuLayer = 5; // Built-in "UI" layer
    private const float CanvasWorldScale = 0.001f; // 1920 x 1080 px => 1.92 x 1.08 m
    // แผ่นกระดาษยึดบานพับที่ขอบขวา หมุนแกน Y ให้ขอบซ้ายลึกเข้าไปในฉาก (หันหน้าเข้าหาตัวละคร)
    private static readonly Vector3 AnchorRestEuler = new Vector3(2f, 14f, -1.5f);
    private static readonly Vector3 AnchorHiddenEuler = new Vector3(2f, 80f, -5f);
    private const float CanvasDistance = 1.6f;
    private const float RightMargin = 50f; // ระยะห่างจากขอบขวาจอ (หน่วยพิกเซลของ Canvas)
    // กระดาษกินพื้นที่ครึ่งขวาของจอ สูงเกือบเต็มจอ
    private static readonly Vector2 PaperSize = new Vector2(1000f, 1020f);
    private const float ContentWidth = 840f;
    private const float RowHeight = 96f;
    private const float RowY0 = 120f;   // แถวแรกของรายการ
    private const float RowStep = 116f; // ระยะห่างระหว่างแถว
    private Vector2 _anchorRestPos = new Vector2(980f, 0f);
    private Vector2 _anchorHiddenPos = new Vector2(1140f, -40f);
    private Transform _menuRig;
    private Camera _menuCam;
    private bool _menuAnimating = false;
    private Vector3 _parallaxEuler = Vector3.zero;

    // ฉากหลังเบลอ (ภาพหน้าจอที่ถูกเบลอบน CPU แสดงบน Canvas ScreenSpaceCamera ด้านหลังเมนู)
    private const int BackdropWidth = 384;
    private Canvas _backdropCanvas;
    private CanvasGroup _backdropGroup;
    private RawImage _backdropImage;
    private Texture2D _backdropTex;

    // References to player controller
    private StarterAssets.ThirdPersonController _tpc;
    private StarterAssets.StarterAssetsInputs _inputs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        if (_instance == null)
        {
            var go = new GameObject("PauseMenu_System");
            go.AddComponent<PauseMenuManager>();
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
        DontDestroyOnLoad(gameObject);

        EnsureUIBuilt();
    }

    private void Start()
    {
        FindPlayerReferences();
        if (_canvas != null) _canvas.gameObject.SetActive(false);
        if (_backdropCanvas != null) _backdropCanvas.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_backdropTex != null) Destroy(_backdropTex);
    }

    private void Update()
    {
        bool escPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            escPressed = true;
        }
#else
        try
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                escPressed = true;
            }
        }
        catch { }
#endif

        if (escPressed)
        {
            HandleEscapePressed();
        }

        if (isPaused)
        {
            HandleMenuKeyboardNavigation();
            UpdateMenuParallax();
        }
    }

    // ==========================================
    // Keyboard Navigation (W/S, ลูกศร, Enter)
    // ==========================================
    private void HandleMenuKeyboardNavigation()
    {
        if (_menuAnimating || _activeRows == null || _activeRows.Length == 0) return;

        bool up = false, down = false, submit = false;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            submit = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
        }
#else
        up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
        down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#endif

        int n = _activeRows.Length;
        if (up) SelectRow((_selectedIndex + n - 1) % n);
        if (down) SelectRow((_selectedIndex + 1) % n);
        if (submit)
        {
            var btn = _activeRows[_selectedIndex].GetComponent<Button>();
            if (btn != null) btn.onClick.Invoke();
        }
    }

    private void SetActiveRows(DossierMenuRow[] rows, int selected)
    {
        if (_activeRows != null)
        {
            foreach (var r in _activeRows) if (r != null) r.SetSelected(false, true);
        }
        _activeRows = rows;
        SelectRow(selected, true);
    }

    private void SelectRow(int index, bool instant = false)
    {
        if (_activeRows == null || _activeRows.Length == 0) return;
        _selectedIndex = Mathf.Clamp(index, 0, _activeRows.Length - 1);
        if (_activeRows == _mainRows) _mainSelectedIndex = _selectedIndex;
        for (int i = 0; i < _activeRows.Length; i++)
        {
            if (_activeRows[i] != null) _activeRows[i].SetSelected(i == _selectedIndex, instant);
        }
    }

    private void OnRowHovered(DossierMenuRow hovered)
    {
        if (_activeRows == null) return;
        int idx = System.Array.IndexOf(_activeRows, hovered);
        if (idx >= 0 && idx != _selectedIndex) SelectRow(idx);
    }

    // ==========================================
    // Mouse Parallax: แผ่นกระดาษเอียงตามเมาส์เล็กน้อยให้รู้สึกเป็นวัตถุ 3 มิติ
    // ==========================================
    private void UpdateMenuParallax()
    {
        if (_menuAnimating || _menu3DAnchor == null) return;

        Vector2 mouse = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null) mouse = Mouse.current.position.ReadValue();
#else
        mouse = Input.mousePosition;
#endif
        float nx = Mathf.Clamp(mouse.x / Mathf.Max(1, Screen.width) * 2f - 1f, -1f, 1f);
        float ny = Mathf.Clamp(mouse.y / Mathf.Max(1, Screen.height) * 2f - 1f, -1f, 1f);

        Vector3 target = new Vector3(ny * 2f, nx * 3f, 0f);
        _parallaxEuler = Vector3.Lerp(_parallaxEuler, target, 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime));
        _menu3DAnchor.localEulerAngles = AnchorRestEuler + _parallaxEuler;
    }

    private void HandleEscapePressed()
    {
        // 1. ถ้าคัตซีนภาพยนตร์กำลังเล่นบทสนทนาอยู่ ห้ามเปิด Pause Menu ขัดจังหวะ
        if (CinematicLetterbox.Instance != null && CinematicLetterbox.Instance.IsLetterboxActive)
        {
            return;
        }

        // 2. ถ้าหน้าต่างแฟ้มคดีเปิดอยู่ ให้แฟ้มคดีเป็นผู้รับ ESC ก่อน
        if (CaseArchiveJournalUI.Instance != null && CaseArchiveJournalUI.Instance.IsOpen)
        {
            return;
        }

        // 3. ระหว่างหน้าสรุปข้อสันนิษฐานหรือหน้าสรุปบท ไม่เปิดเมนูหยุดเกมซ้อน
        if (ChapterSummaryUI.IsOpen || CaseDeductionBoardUI.IsOpen || TitleMenuUI.IsOpen || GameEndingSequence.IsPlaying)
        {
            return;
        }

        // 4. ระหว่างมินิเกมถอดรหัสหนังสือพิมพ์ ไม่เปิดเมนูหยุดเกมซ้อน
        if (NewspaperDecryptionMinigame.Instance != null && NewspaperDecryptionMinigame.Instance.IsMinigameActive)
        {
            return;
        }
        if (LondonRailwayMapMinigame.Instance != null && LondonRailwayMapMinigame.Instance.IsMinigameActive)
        {
            return;
        }
        if (EvidenceConnectingDotsMinigame.Instance != null && EvidenceConnectingDotsMinigame.Instance.IsMinigameActive)
        {
            return;
        }

        // 3. ถ้าอยู่ในหน้า Settings หรือ Confirm Quit ของ Pause Menu ให้ย้อนกลับมาหน้าหลักของ Pause Menu
        if (isPaused)
        {
            if (_confirmQuitContainer != null && _confirmQuitContainer.activeSelf)
            {
                OnCancelQuit();
                return;
            }

            if (_settingsContainer != null && _settingsContainer.activeSelf)
            {
                OnClickCloseSettings();
                return;
            }

            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (isPaused) return;
        isPaused = true;

        FindPlayerReferences();

        Time.timeScale = 0f;

        if (_inputs != null)
        {
            _inputs.MoveInput(Vector2.zero);
            _inputs.LookInput(Vector2.zero);
            _inputs.cursorLocked = false;
            _inputs.cursorInputForLook = false;
        }

        if (_tpc != null)
        {
            _tpc.LockCameraPosition = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // ซ่อน Objective HUD และ Waypoint ขณะหยุดเกม (ก่อนถ่ายภาพฉากหลัง)
        SetHUDVisibility(false);

        StopAllCoroutines();
        StartCoroutine(OpenMenuRoutine());
    }

    private IEnumerator OpenMenuRoutine()
    {
        // รอให้เฟรมปัจจุบัน (ที่ซ่อน HUD แล้ว) เรนเดอร์เสร็จ แล้วจับภาพมาทำฉากหลังเบลอ
        yield return new WaitForEndOfFrame();
        if (!isPaused) yield break;

        CaptureBlurredBackdrop();

        if (_canvas == null) yield break;

        // เปิดกล้อง Overlay ที่เรนเดอร์เฉพาะเมนู 3 มิติ ซ้อนบนกล้องหลัก
        EnsureMenuCamera();

        if (_backdropCanvas != null)
        {
            _backdropCanvas.worldCamera = _menuCam;
            _backdropCanvas.gameObject.SetActive(_backdropImage != null && _backdropImage.texture != null);
        }

        _canvas.gameObject.SetActive(true);
        ShowView(_mainMenuContainer, "Investigation Paused", _mainRows, 0);
        _parallaxEuler = Vector3.zero;

        yield return AnimateMenuSlide(true);
    }

    private void ShowView(GameObject view, string title, DossierMenuRow[] rows, int selected)
    {
        if (_mainMenuContainer != null) _mainMenuContainer.SetActive(view == _mainMenuContainer);
        if (_settingsContainer != null) _settingsContainer.SetActive(view == _settingsContainer);
        if (_confirmQuitContainer != null) _confirmQuitContainer.SetActive(view == _confirmQuitContainer);
        if (_titleText != null) _titleText.text = title;
        SetActiveRows(rows, selected);
    }

    // ==========================================
    // ฉากหลังเบลอ: จับภาพหน้าจอ -> ย่อ -> Box Blur 3 รอบ (ใกล้เคียง Gaussian) -> โทนซีเปียมืด + Vignette
    // ==========================================
    private void CaptureBlurredBackdrop()
    {
        if (_backdropImage == null) return;

        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        if (shot == null) return;

        int w = BackdropWidth;
        int h = Mathf.Max(1, Mathf.RoundToInt(w * (float)shot.height / Mathf.Max(1, shot.width)));

        // ย่อทีละครึ่งเพื่อลด aliasing ก่อนลงไปถึงขนาดเล็กสุด
        RenderTexture prevActive = RenderTexture.active;
        Texture src = shot;
        RenderTexture step = null;
        int cw = shot.width, ch = shot.height;
        while (cw / 2 >= w)
        {
            cw /= 2;
            ch = Mathf.Max(1, ch / 2);
            var rt = RenderTexture.GetTemporary(cw, ch, 0);
            rt.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, rt);
            if (step != null) RenderTexture.ReleaseTemporary(step);
            step = rt;
            src = rt;
        }

        var small = RenderTexture.GetTemporary(w, h, 0);
        Graphics.Blit(src, small);

        if (_backdropTex == null || _backdropTex.width != w || _backdropTex.height != h)
        {
            if (_backdropTex != null) Destroy(_backdropTex);
            _backdropTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _backdropTex.wrapMode = TextureWrapMode.Clamp;
            _backdropTex.filterMode = FilterMode.Bilinear;
        }

        RenderTexture.active = small;
        _backdropTex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
        RenderTexture.active = prevActive;

        RenderTexture.ReleaseTemporary(small);
        if (step != null) RenderTexture.ReleaseTemporary(step);
        Destroy(shot);

        Color32[] a = _backdropTex.GetPixels32();
        Color32[] b = new Color32[a.Length];
        for (int pass = 0; pass < 3; pass++)
        {
            BoxBlur(a, b, w, h, 3, true);
            BoxBlur(b, a, w, h, 3, false);
        }

        // Color grade: มืดลง ลดสีแล้วย้อมโทนซีเปียอ่อน ๆ ขอบจอมืดกว่า (Vignette) ให้แผ่นกระดาษเด่น
        float cx = w * 0.5f, cy = h * 0.5f;
        float maxD = Mathf.Sqrt(cx * cx + cy * cy);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                Color32 c = a[i];
                float r = c.r / 255f, g = c.g / 255f, bl = c.b / 255f;
                float lum = r * 0.299f + g * 0.587f + bl * 0.114f;
                r = Mathf.Lerp(lum * 1.08f, r, 0.35f);
                g = Mathf.Lerp(lum * 0.98f, g, 0.35f);
                bl = Mathf.Lerp(lum * 0.82f, bl, 0.35f);

                float dx = x - cx, dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / maxD;
                float vignette = Mathf.Lerp(1f, 0.3f, Mathf.SmoothStep(0.3f, 1f, d));
                float k = 0.5f * vignette;

                a[i] = new Color32(
                    (byte)Mathf.Clamp(r * k * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(g * k * 255f, 0f, 255f),
                    (byte)Mathf.Clamp(bl * k * 255f, 0f, 255f),
                    255);
            }
        }

        _backdropTex.SetPixels32(a);
        _backdropTex.Apply(false);
        _backdropImage.texture = _backdropTex;
    }

    private static void BoxBlur(Color32[] src, Color32[] dst, int w, int h, int radius, bool horizontal)
    {
        int len = horizontal ? w : h;
        int lines = horizontal ? h : w;
        float inv = 1f / (radius * 2 + 1);

        for (int line = 0; line < lines; line++)
        {
            float sr = 0f, sg = 0f, sb = 0f;
            for (int k = -radius; k <= radius; k++)
            {
                Color32 c = src[Index(Mathf.Clamp(k, 0, len - 1), line, w, horizontal)];
                sr += c.r; sg += c.g; sb += c.b;
            }

            for (int p = 0; p < len; p++)
            {
                dst[Index(p, line, w, horizontal)] = new Color32((byte)(sr * inv), (byte)(sg * inv), (byte)(sb * inv), 255);

                Color32 outC = src[Index(Mathf.Clamp(p - radius, 0, len - 1), line, w, horizontal)];
                Color32 inC = src[Index(Mathf.Clamp(p + radius + 1, 0, len - 1), line, w, horizontal)];
                sr += inC.r - outC.r;
                sg += inC.g - outC.g;
                sb += inC.b - outC.b;
            }
        }
    }

    private static int Index(int p, int line, int w, bool horizontal)
    {
        return horizontal ? line * w + p : p * w + line;
    }

    // ==========================================
    // Overlay Camera สำหรับเมนู 3 มิติ (URP Camera Stacking)
    // ==========================================
    private void EnsureMenuCamera()
    {
        if (_menuCam == null)
        {
            var camGo = new GameObject("PauseMenu_OverlayCamera");
            camGo.transform.SetParent(_menuRig != null ? _menuRig : transform, false);
            camGo.transform.localPosition = Vector3.zero;
            camGo.transform.localRotation = Quaternion.identity;

            _menuCam = camGo.AddComponent<Camera>();
            _menuCam.clearFlags = CameraClearFlags.Depth;
            _menuCam.cullingMask = 1 << MenuLayer;
            _menuCam.fieldOfView = 40f;
            _menuCam.nearClipPlane = 0.05f;
            _menuCam.farClipPlane = 20f;

            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderType = CameraRenderType.Overlay;
            camData.renderPostProcessing = false;
            camData.renderShadows = false;
        }

        _menuCam.enabled = true;
        if (_canvas != null) _canvas.worldCamera = _menuCam;

        // ชิดขอบขวาของจอเสมอไม่ว่าจอจะเป็นอัตราส่วนเท่าไหร่ (16:9, 21:9, 16:10 ...)
        float halfH = Mathf.Tan(_menuCam.fieldOfView * 0.5f * Mathf.Deg2Rad) * CanvasDistance / CanvasWorldScale;
        float halfW = halfH * Mathf.Max(0.5f, _menuCam.aspect);
        _anchorRestPos = new Vector2(halfW - RightMargin, 0f);
        _anchorHiddenPos = _anchorRestPos + new Vector2(160f, -40f);

        // ผูกกล้อง Overlay เข้ากับ Stack ของกล้องหลักของฉากปัจจุบัน (กล้องหลักอาจเปลี่ยนเมื่อโหลดฉากใหม่)
        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam != _menuCam)
        {
            var mainData = mainCam.GetComponent<UniversalAdditionalCameraData>();
            if (mainData == null) mainData = mainCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            if (mainData.renderType == CameraRenderType.Base && !mainData.cameraStack.Contains(_menuCam))
            {
                mainData.cameraStack.Add(_menuCam);
            }
        }
    }

    private void DisableMenuCamera()
    {
        if (_menuCam != null) _menuCam.enabled = false;
    }

    public void ResumeGame()
    {
        if (!isPaused) return;
        isPaused = false;

        // หยุดแอนิเมชันเปิดเมนูที่อาจยังเล่นค้างอยู่ (กด ESC รัว ๆ) ก่อนเริ่มแอนิเมชันปิด
        StopAllCoroutines();
        StartCoroutine(ResumeRoutine());
    }

    private IEnumerator ResumeRoutine()
    {
        if (_canvas != null && _canvas.gameObject.activeSelf)
        {
            yield return StartCoroutine(AnimateMenuSlide(false));
        }

        HideMenuImmediate();

        // คืนค่า Objective HUD และ Waypoint กลับมา
        SetHUDVisibility(true);

        Time.timeScale = 1f;

        if (_tpc != null)
        {
            _tpc.LockCameraPosition = false;
            _tpc.ResetCameraRotation();
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yield return null;
        yield return new WaitForEndOfFrame();

        if (_inputs != null)
        {
            _inputs.LookInput(Vector2.zero);
            _inputs.cursorLocked = true;
            _inputs.cursorInputForLook = true;
        }
    }

    private void HideMenuImmediate()
    {
        _menuAnimating = false;
        if (_canvas != null) _canvas.gameObject.SetActive(false);
        if (_backdropCanvas != null) _backdropCanvas.gameObject.SetActive(false);
        DisableMenuCamera();
    }

    private IEnumerator AnimateMenuSlide(bool opening)
    {
        _menuAnimating = true;

        // เปิด: แผ่นกระดาษหมุนกวาดเข้ามาเหมือนถูกยื่นให้ดู แล้วหยุดที่มุมเอียง 3 มิติ พร้อมฉากหลังค่อย ๆ เบลอ
        // ปิด: หมุนกลับออกไปพร้อมจางหาย
        float duration = opening ? 0.45f : 0.2f;
        float elapsed = 0f;

        Vector3 restRot = AnchorRestEuler + _parallaxEuler;
        Vector3 startRot = opening ? AnchorHiddenEuler : restRot;
        Vector3 endRot = opening ? AnchorRestEuler : AnchorHiddenEuler;

        Vector2 startPos = opening ? _anchorHiddenPos : _anchorRestPos;
        Vector2 endPos = opening ? _anchorRestPos : _anchorHiddenPos;

        float startAlpha = opening ? 0f : 1f;
        float endAlpha = opening ? 1f : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // เปิดใช้ ease-out-back ให้มีแรงเหวี่ยงเล็กน้อยตอนหยุด, ปิดใช้ ease-in
            float eased = opening ? EaseOutBack(t) : t * t;
            float fade = Mathf.Lerp(startAlpha, endAlpha, Mathf.Clamp01(t * 1.6f));

            if (_menu3DAnchor != null)
            {
                _menu3DAnchor.localEulerAngles = Vector3.LerpUnclamped(startRot, endRot, eased);
                _menu3DAnchor.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, eased);
            }
            if (_menuCanvasGroup != null) _menuCanvasGroup.alpha = fade;
            if (_backdropGroup != null) _backdropGroup.alpha = fade;
            yield return null;
        }

        if (_menu3DAnchor != null)
        {
            _menu3DAnchor.localEulerAngles = endRot;
            _menu3DAnchor.anchoredPosition = endPos;
        }
        if (_menuCanvasGroup != null) _menuCanvasGroup.alpha = endAlpha;
        if (_backdropGroup != null) _backdropGroup.alpha = endAlpha;

        _parallaxEuler = Vector3.zero;
        _menuAnimating = false;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.2f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    public void OnClickPlay()
    {
        ResumeGame();
    }

    public void OnClickSettings()
    {
        ShowView(_settingsContainer, "Options", _settingsRows, 0);
    }

    public void OnClickCloseSettings()
    {
        ShowView(_mainMenuContainer, "Investigation Paused", _mainRows, _mainSelectedIndex);
    }

    public void OnClickQuit()
    {
        ShowView(_confirmQuitContainer, "Leave the Case?", _confirmRows, 1);
    }

    public void OnConfirmQuit()
    {
        isPaused = false;
        StopAllCoroutines();
        HideMenuImmediate();
        SetHUDVisibility(true);
        Time.timeScale = 1f;
        // ออกจากคดี -> กลับไปหน้าเมนูเริ่มเกม (TitleMenuUI สร้างตัวเองเมื่อโหลดฉากใหม่)
        CiraCoreVisionScanner.IsScannerBlocked = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnCancelQuit()
    {
        ShowView(_mainMenuContainer, "Investigation Paused", _mainRows, _mainSelectedIndex);
    }

    private void FindPlayerReferences()
    {
        if (_tpc == null) _tpc = FindAnyObjectByType<StarterAssets.ThirdPersonController>();
        if (_inputs == null && _tpc != null) _inputs = _tpc.GetComponent<StarterAssets.StarterAssetsInputs>();
    }

    // ==========================================
    // Objective HUD / Waypoint Visibility
    // ==========================================
    private void SetHUDVisibility(bool visible)
    {
        if (visible)
        {
            // คืนเฉพาะ HUD ของเควสที่กำลังเล่นอยู่จริง (เดิมคืน HUD ของเควส 4 ทุกครั้ง ทำให้เควส 4 โผล่ในฉาก 1)
            if (UndergroundStationQuestManager.Instance != null && UndergroundStationQuestManager.Instance.IsQuest4Active)
                UndergroundStationQuestManager.Instance.ShowObjectiveHUD(true);
            else if (RoyalLyceumTheaterQuestManager.Instance != null && RoyalLyceumTheaterQuestManager.Instance.IsQuest3Active)
                RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(true);
            else if (LondonAlleyQuestManager.Instance != null && LondonAlleyQuestManager.Instance.IsQuest2Active)
                LondonAlleyQuestManager.Instance.ShowObjectiveHUD(true);
            else if (StMaryChurchQuestManager.Instance != null && !StMaryChurchQuestManager.Instance.IsQuest1Finished)
                StMaryChurchQuestManager.Instance.ShowObjectiveHUD(true);

            if (WaypointNavigationSystem.Instance != null)
                WaypointNavigationSystem.Instance.SetVisible(true);
        }
        else
        {
            if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
            if (LondonAlleyQuestManager.Instance != null) LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
            if (RoyalLyceumTheaterQuestManager.Instance != null) RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(false);
            if (UndergroundStationQuestManager.Instance != null) UndergroundStationQuestManager.Instance.ShowObjectiveHUD(false);

            if (WaypointNavigationSystem.Instance != null)
                WaypointNavigationSystem.Instance.SetVisible(false);
        }
    }

    // ==========================================
    // UI Construction (แผ่นกระดาษบันทึกคดี 1888)
    // ==========================================
    private Font ResolveSerifFont()
    {
        if (serifFont == null) serifFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman", "Cambria" }, 32);
        if (serifFont == null) serifFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return serifFont;
    }

    private Font ResolveTypewriterFont()
    {
        if (typewriterFont == null) typewriterFont = Font.CreateDynamicFontFromOSFont(new[] { "Courier New", "Consolas" }, 24);
        if (typewriterFont == null) typewriterFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return typewriterFont;
    }

    public void EnsureUIBuilt()
    {
        // ทำลาย Canvas / Rig เดิมทิ้งเพื่อสร้างใหม่ให้ตรงตามสเปก 100%
        var oldCanvas = transform.Find("PauseMenu_Canvas");
        if (oldCanvas != null)
        {
            DestroyImmediate(oldCanvas.gameObject);
        }
        var oldRig = transform.Find("PauseMenu_3DRig");
        if (oldRig != null)
        {
            DestroyImmediate(oldRig.gameObject);
        }
        _menuCam = null;

        // 1. 3D Rig: วางไว้ไกลจากฉากมาก ๆ ให้มีแค่กล้อง Overlay ของเมนูที่มองเห็น
        GameObject rigGo = new GameObject("PauseMenu_3DRig");
        rigGo.transform.SetParent(transform, false);
        rigGo.transform.position = MenuRigWorldPosition;
        rigGo.transform.rotation = Quaternion.identity;
        _menuRig = rigGo.transform;

        // 2. Backdrop Canvas (ScreenSpaceCamera ระยะ 15 ม. อยู่หลังแผงเมนู เต็มจอเสมอ)
        GameObject backdropGo = new GameObject("PauseMenu_Backdrop");
        backdropGo.transform.SetParent(_menuRig, false);
        _backdropCanvas = backdropGo.AddComponent<Canvas>();
        _backdropCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        _backdropCanvas.planeDistance = 15f;
        _backdropCanvas.sortingOrder = -100;
        _backdropGroup = backdropGo.AddComponent<CanvasGroup>();
        _backdropGroup.blocksRaycasts = false;

        GameObject backdropImgGo = new GameObject("BlurredScene");
        backdropImgGo.transform.SetParent(backdropGo.transform, false);
        var backdropRect = backdropImgGo.AddComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.sizeDelta = Vector2.zero;
        _backdropImage = backdropImgGo.AddComponent<RawImage>();
        _backdropImage.raycastTarget = false;
        _backdropImage.color = Color.white;
        backdropGo.SetActive(false);

        // 3. Menu Canvas (WorldSpace) ห่างจากกล้องเมนู 1.6 เมตร
        GameObject canvasGo = new GameObject("PauseMenu_Canvas");
        canvasGo.transform.SetParent(_menuRig, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 10;
        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);
        canvasRect.localPosition = new Vector3(0f, 0f, CanvasDistance);
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = Vector3.one * CanvasWorldScale;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 4f; // ตัวอักษรคมชัดเมื่ออยู่ในพื้นที่ 3 มิติ

        canvasGo.AddComponent<GraphicRaycaster>();

        // 4. แผ่นกระดาษ 3 มิติ: ยึดบานพับที่ขอบขวา ขอบซ้ายลึกเข้าไปในฉาก หันหน้าเข้าหาตัวละคร
        GameObject anchorGo = new GameObject("Menu3D_Anchor");
        anchorGo.transform.SetParent(canvasGo.transform, false);
        _menu3DAnchor = anchorGo.AddComponent<RectTransform>();
        _menu3DAnchor.anchorMin = new Vector2(0.5f, 0.5f);
        _menu3DAnchor.anchorMax = new Vector2(0.5f, 0.5f);
        _menu3DAnchor.pivot = new Vector2(1f, 0.5f);
        _menu3DAnchor.sizeDelta = PaperSize;
        _menu3DAnchor.anchoredPosition = _anchorRestPos;
        _menu3DAnchor.localEulerAngles = AnchorRestEuler;

        _menuCanvasGroup = anchorGo.AddComponent<CanvasGroup>();

        BuildPaper(anchorGo.transform);

        // หัวกระดาษ: หน่วยงาน + เลขคดี (พิมพ์ดีด), หัวข้อ (Serif), เส้นหมึกคั่น
        // เลย์เอาต์สำหรับกระดาษ 1000 x 1020 (ขอบใน 80 หน่วย => เนื้อหากว้าง ContentWidth)
        CreateText(anchorGo.transform, "Letterhead", "METROPOLITAN POLICE  ·  " + caseNumber,
            new Vector2(0f, 400f), new Vector2(ContentWidth, 34f), 24, DetectiveUITheme.InkMuted, ResolveTypewriterFont(), FontStyle.Bold, TextAnchor.MiddleLeft);
        _titleText = CreateText(anchorGo.transform, "Title", "Investigation Paused",
            new Vector2(0f, 330f), new Vector2(ContentWidth, 90f), 66, DetectiveUITheme.InkDark, ResolveSerifFont(), FontStyle.Bold, TextAnchor.MiddleLeft);
        CreateRule(anchorGo.transform, new Vector2(0f, 272f), new Vector2(ContentWidth, 3f), new Color(DetectiveUITheme.InkDark.r, DetectiveUITheme.InkDark.g, DetectiveUITheme.InkDark.b, 0.55f));
        CreateText(anchorGo.transform, "Subtitle", "Whitechapel, London  —  Autumn 1888",
            new Vector2(0f, 232f), new Vector2(ContentWidth, 40f), 28, DetectiveUITheme.InkMuted, ResolveSerifFont(), FontStyle.Italic, TextAnchor.MiddleLeft);

        // ตราประทับ CONFIDENTIAL หมึกแดงเอียง ๆ มุมขวาล่าง
        BuildStamp(anchorGo.transform, "CONFIDENTIAL", new Vector2(250f, -270f), -8f);

        // 5. Main Menu
        _mainMenuContainer = CreateSubPanel(anchorGo.transform, "MainMenu_Container");
        _mainRows = new[]
        {
            CreateRow(_mainMenuContainer.transform, "Row_Resume", "I.", "Resume Investigation", new Vector2(0f, RowY0), OnClickPlay),
            CreateRow(_mainMenuContainer.transform, "Row_Options", "II.", "Options", new Vector2(0f, RowY0 - RowStep), OnClickSettings),
            CreateRow(_mainMenuContainer.transform, "Row_Quit", "III.", "Leave the Case", new Vector2(0f, RowY0 - RowStep * 2f), OnClickQuit),
        };

        // คำแนะนำปุ่มด้านล่าง (พิมพ์ดีด หมึกจาง)
        CreateRule(anchorGo.transform, new Vector2(0f, -370f), new Vector2(ContentWidth, 2f), new Color(DetectiveUITheme.InkMuted.r, DetectiveUITheme.InkMuted.g, DetectiveUITheme.InkMuted.b, 0.45f));
        CreateText(anchorGo.transform, "Hint", "[W/S] Select    [Enter] Confirm    [Esc] Back",
            new Vector2(0f, -408f), new Vector2(ContentWidth, 34f), 22, DetectiveUITheme.InkMuted, ResolveTypewriterFont(), FontStyle.Normal, TextAnchor.MiddleLeft);

        // 6. Settings View
        BuildSettingsView(anchorGo.transform);

        // 7. Confirm Quit View
        BuildConfirmQuitView(anchorGo.transform);

        // ให้ทุกชิ้นของเมนูอยู่บนเลเยอร์ UI ซึ่งกล้อง Overlay ของเมนูมองเห็นเพียงเลเยอร์เดียว
        SetLayerRecursively(_menuRig, MenuLayer);
    }

    private void BuildPaper(Transform parent)
    {
        // เงาตกกระทบใต้แผ่นกระดาษ
        GameObject shadowGo = new GameObject("PaperShadow");
        shadowGo.transform.SetParent(parent, false);
        var shadowRect = shadowGo.AddComponent<RectTransform>();
        shadowRect.anchorMin = Vector2.zero;
        shadowRect.anchorMax = Vector2.one;
        shadowRect.offsetMin = new Vector2(18f, -22f);
        shadowRect.offsetMax = new Vector2(18f, -22f);
        var shadowImg = shadowGo.AddComponent<Image>();
        shadowImg.color = new Color(0f, 0f, 0f, 0.55f);
        shadowImg.raycastTarget = false;

        // แผ่นกระดาษบันทึกคดีเก่า (ครอปจากภาพ DetectiveNoteCard ในโปรเจกต์ ถ้าไม่มีใช้สีกระดาษเก่าแทน)
        GameObject paperGo = new GameObject("Paper");
        paperGo.transform.SetParent(parent, false);
        var paperRect = paperGo.AddComponent<RectTransform>();
        paperRect.anchorMin = Vector2.zero;
        paperRect.anchorMax = Vector2.one;
        paperRect.sizeDelta = Vector2.zero;
        var paperImg = paperGo.AddComponent<RawImage>();
        paperImg.raycastTarget = true; // กันคลิกทะลุ
        Sprite noteCard = DetectiveUITheme.NoteCard;
        if (noteCard != null)
        {
            paperImg.texture = noteCard.texture;
            // ภาพต้นฉบับมีพื้นโต๊ะสีเทารอบ ๆ: ครอปเฉพาะส่วนกระดาษ (พิกัด UV จากภาพ 1024x1024)
            paperImg.uvRect = new Rect(0.105f, 0.232f, 0.79f, 0.54f);
            paperImg.color = new Color(1f, 0.97f, 0.92f, 1f);
        }
        else
        {
            paperImg.color = DetectiveUITheme.AgedPaper;
        }
    }

    private void BuildStamp(Transform parent, string label, Vector2 pos, float angle)
    {
        GameObject go = new GameObject("Stamp");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(310f, 68f);
        rect.anchoredPosition = pos;
        rect.localEulerAngles = new Vector3(0f, 0f, angle);

        Color ink = new Color(DetectiveUITheme.StampRed.r, DetectiveUITheme.StampRed.g, DetectiveUITheme.StampRed.b, 0.7f);
        var frame = go.AddComponent<UIParallelogramGraphic>();
        frame.skewAmount = 0f;
        frame.outlineWidth = 4f;
        frame.outlineColor = ink;
        frame.fillColor = new Color(0f, 0f, 0f, 0f);
        frame.raycastTarget = false;

        var txt = CreateText(go.transform, "Label", label, Vector2.zero, new Vector2(310f, 68f), 36, ink, ResolveTypewriterFont(), FontStyle.Bold, TextAnchor.MiddleCenter);
        var txtRect = txt.rectTransform;
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;
    }

    private DossierMenuRow CreateRow(Transform parent, string name, string numeral, string label, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        GameObject rowGo = new GameObject(name);
        rowGo.transform.SetParent(parent, false);
        var rect = rowGo.AddComponent<RectTransform>();
        float rowWidth = ContentWidth + 40f;
        rect.sizeDelta = new Vector2(rowWidth, RowHeight);
        rect.anchoredPosition = pos;

        // พื้นที่รับเมาส์ทั้งแถว (โปร่งใส)
        var hit = rowGo.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        var btn = rowGo.AddComponent<Button>();
        btn.targetGraphic = hit;
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(action);

        // กรอบเลือก: กรอบหมึกแดงรอบทั้งแถว + พื้นแดงจาง ๆ ปรากฏเมื่อชี้/เลือกรายการ
        GameObject frameGo = new GameObject("SelectionFrame");
        frameGo.transform.SetParent(rowGo.transform, false);
        var frameRect = frameGo.AddComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.sizeDelta = Vector2.zero;
        var frame = frameGo.AddComponent<UIParallelogramGraphic>();
        frame.skewAmount = 0f;
        frame.outlineWidth = 4f;
        frame.outlineColor = DetectiveUITheme.StampRed;
        frame.fillColor = new Color(DetectiveUITheme.StampRed.r, DetectiveUITheme.StampRed.g, DetectiveUITheme.StampRed.b, 0.09f);
        frame.shadowOffset = new Vector2(6f, -6f);
        frame.shadowColor = new Color(0.25f, 0.12f, 0.05f, 0.25f);
        frame.raycastTarget = false;
        var frameGroup = frameGo.AddComponent<CanvasGroup>();
        frameGroup.blocksRaycasts = false;
        frameGroup.interactable = false;

        var num = CreateText(rowGo.transform, "Numeral", numeral, new Vector2(-rowWidth * 0.5f + 80f, 0f), new Vector2(100f, RowHeight), 36,
            DetectiveUITheme.InkMuted, ResolveSerifFont(), FontStyle.Italic, TextAnchor.MiddleLeft);

        var txt = CreateText(rowGo.transform, "Label", label, new Vector2(70f, 0f), new Vector2(rowWidth - 220f, RowHeight), 50,
            DetectiveUITheme.InkDark, ResolveSerifFont(), FontStyle.Normal, TextAnchor.MiddleLeft);

        var row = rowGo.AddComponent<DossierMenuRow>();
        row.Init(frameGroup, frameRect, txt, num);
        row.onHovered = OnRowHovered;
        return row;
    }

    private Text CreateText(Transform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, Color color, Font font, FontStyle style, TextAnchor align)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;

        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.font = font;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = align;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false;
        return txt;
    }

    private void CreateRule(Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        GameObject go = new GameObject("Rule");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null) return;
        root.gameObject.layer = layer;
        foreach (Transform child in root) SetLayerRecursively(child, layer);
    }

    private GameObject CreateSubPanel(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        go.SetActive(false);
        return go;
    }

    private void BuildSettingsView(Transform parent)
    {
        _settingsContainer = CreateSubPanel(parent, "Settings_Container");

        CreateInkSlider(_settingsContainer.transform, "Volume", new Vector2(0f, RowY0), AudioListener.volume, (v) => AudioListener.volume = v);

        CreateInkSlider(_settingsContainer.transform, "Mouse Sensitivity", new Vector2(0f, RowY0 - RowStep), 1f, (v) => {
            if (_tpc != null) _tpc.RotationSpeed = v;
        }, 0.5f, 2.5f);

        _settingsRows = new[]
        {
            CreateRow(_settingsContainer.transform, "Row_Back", "«", "Back", new Vector2(0f, RowY0 - RowStep * 2f), OnClickCloseSettings),
        };
    }

    private void BuildConfirmQuitView(Transform parent)
    {
        _confirmQuitContainer = CreateSubPanel(parent, "ConfirmQuit_Container");

        CreateText(_confirmQuitContainer.transform, "Question", "Any unrecorded deductions will be lost.",
            new Vector2(0f, RowY0), new Vector2(ContentWidth, 60f), 34, DetectiveUITheme.InkMuted, ResolveSerifFont(), FontStyle.Italic, TextAnchor.MiddleLeft);

        _confirmRows = new[]
        {
            CreateRow(_confirmQuitContainer.transform, "Row_ConfirmQuit", "I.", "Yes, close the file", new Vector2(0f, RowY0 - RowStep), OnConfirmQuit),
            CreateRow(_confirmQuitContainer.transform, "Row_CancelQuit", "II.", "No, keep investigating", new Vector2(0f, RowY0 - RowStep * 2f), OnCancelQuit),
        };
    }

    private void CreateInkSlider(Transform parent, string label, Vector2 pos, float defaultVal, UnityEngine.Events.UnityAction<float> onValueChanged, float min = 0f, float max = 1f)
    {
        GameObject go = new GameObject("SliderRow_" + label);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(ContentWidth, RowHeight);
        rect.anchoredPosition = pos;

        CreateText(go.transform, "Label", label, new Vector2(-ContentWidth * 0.5f + 230f, 0f), new Vector2(460f, RowHeight), 42,
            DetectiveUITheme.InkDark, ResolveSerifFont(), FontStyle.Normal, TextAnchor.MiddleLeft);

        // Slider: เส้นหมึกบาง ๆ มีหมึกแดงเติมตามค่า ลากหรือคลิกบนเส้นได้
        GameObject sliderGo = new GameObject("Slider");
        sliderGo.transform.SetParent(go.transform, false);
        var sliderRect = sliderGo.AddComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(330f, 44f);
        sliderRect.anchoredPosition = new Vector2(ContentWidth * 0.5f - 165f, 0f);

        var hitArea = sliderGo.AddComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0f);

        GameObject trackGo = new GameObject("Track");
        trackGo.transform.SetParent(sliderGo.transform, false);
        var trackRect = trackGo.AddComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.sizeDelta = new Vector2(0f, 2f);
        var trackImg = trackGo.AddComponent<Image>();
        trackImg.color = new Color(DetectiveUITheme.InkDark.r, DetectiveUITheme.InkDark.g, DetectiveUITheme.InkDark.b, 0.5f);
        trackImg.raycastTarget = false;

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderGo.transform, false);
        var fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.sizeDelta = new Vector2(0f, 5f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = DetectiveUITheme.StampRed;
        fillImg.raycastTarget = false;

        var slider = sliderGo.AddComponent<Slider>();
        slider.fillRect = fillRect;
        slider.targetGraphic = hitArea;
        slider.transition = Selectable.Transition.None;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(defaultVal, min, max);
        slider.onValueChanged.AddListener(onValueChanged);
    }
}

/// <summary>
/// ตัววาดกราฟิกสี่เหลี่ยมด้านขนาน (Parallelogram) พร้อมเส้นขอบคมกริบและเงาตกกระทบสำหรับ UI
/// เส้นขอบวาดเป็นกรอบ 4 ด้าน (ไม่ทับใต้พื้น) ทำให้ใช้พื้นโปร่งใสเป็นกรอบตราประทับได้
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIParallelogramGraphic : MaskableGraphic
{
    public float skewAmount = 38f;
    public float outlineWidth = 3f;
    public Color outlineColor = Color.white;
    public Color fillColor = new Color(0.04f, 0.04f, 0.06f, 1f);
    public Vector2 shadowOffset = Vector2.zero;
    public Color shadowColor = new Color(0f, 0f, 0f, 0.55f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float halfW = r.width * 0.5f;
        float halfH = r.height * 0.5f;
        Vector2 c = r.center;

        // สี่เหลี่ยมด้านขนานเฉียง: มุมล่างซ้ายยื่นออกทางซ้าย, มุมบนซ้ายเฉียงเข้า
        Vector2 bl = c + new Vector2(-halfW - skewAmount * 0.5f, -halfH);
        Vector2 br = c + new Vector2(halfW - skewAmount * 0.5f, -halfH);
        Vector2 tr = c + new Vector2(halfW + skewAmount * 0.5f, halfH);
        Vector2 tl = c + new Vector2(-halfW + skewAmount * 0.5f, halfH);

        if (shadowOffset != Vector2.zero && shadowColor.a > 0f)
        {
            AddQuad(vh, bl + shadowOffset, br + shadowOffset, tr + shadowOffset, tl + shadowOffset, shadowColor);
        }

        if (outlineWidth > 0f)
        {
            // Quad ด้านใน (ยุบเข้ามาตามความหนาเส้นขอบ)
            float inset = outlineWidth;
            float insetX = skewAmount > 0f ? inset * 1.15f : inset;
            Vector2 in_bl = bl + new Vector2(insetX, inset);
            Vector2 in_br = br + new Vector2(-insetX, inset);
            Vector2 in_tr = tr + new Vector2(-insetX, -inset);
            Vector2 in_tl = tl + new Vector2(insetX, -inset);

            if (fillColor.a > 0f) AddQuad(vh, in_bl, in_br, in_tr, in_tl, fillColor);

            // กรอบ 4 ด้าน
            AddQuad(vh, bl, br, in_br, in_bl, outlineColor);
            AddQuad(vh, br, tr, in_tr, in_br, outlineColor);
            AddQuad(vh, tr, tl, in_tl, in_tr, outlineColor);
            AddQuad(vh, tl, bl, in_bl, in_tl, outlineColor);
        }
        else
        {
            AddQuad(vh, bl, br, tr, tl, fillColor);
        }
    }

    private void AddQuad(VertexHelper vh, Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3, Color col)
    {
        int idx = vh.currentVertCount;
        vh.AddVert(v0, col, new Vector2(0, 0));
        vh.AddVert(v1, col, new Vector2(1, 0));
        vh.AddVert(v2, col, new Vector2(1, 1));
        vh.AddVert(v3, col, new Vector2(0, 1));

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx);
    }
}

/// <summary>
/// รายการเมนูบนแผ่นกระดาษบันทึกคดี: เมื่อชี้หรือเลือก จะมีกรอบหมึกแดงล้อมรอบทั้งแถว (หดเข้ามาจากขนาดใหญ่กว่าเล็กน้อย)
/// ตัวหนังสือเปลี่ยนเป็นหมึกแดงและเลื่อนไปทางขวาเล็กน้อย
/// </summary>
public class DossierMenuRow : MonoBehaviour, IPointerEnterHandler
{
    private CanvasGroup _frameGroup;
    private RectTransform _frameRect;
    private Text _label;
    private Text _numeral;
    private Vector2 _labelBasePos;
    private float _t = 0f;
    private Coroutine _anim;

    /// <summary>แจ้งเมนูเมื่อเมาส์ชี้ แล้วให้เมนูเป็นผู้จัดการการเลือก (ใช้ร่วมกับคีย์บอร์ด)</summary>
    public System.Action<DossierMenuRow> onHovered;

    private static readonly Color InkDark = new Color(0.14f, 0.10f, 0.07f, 1f);
    private static readonly Color InkMuted = new Color(0.35f, 0.26f, 0.18f, 1f);
    private static readonly Color InkRed = new Color(0.62f, 0.08f, 0.07f, 1f);

    public void Init(CanvasGroup frameGroup, RectTransform frameRect, Text label, Text numeral)
    {
        _frameGroup = frameGroup;
        _frameRect = frameRect;
        _label = label;
        _numeral = numeral;
        _labelBasePos = _label.rectTransform.anchoredPosition;
        Apply(0f);
    }

    public void SetSelected(bool selected, bool instant = false)
    {
        float target = selected ? 1f : 0f;
        if (instant || !isActiveAndEnabled)
        {
            if (_anim != null) StopCoroutine(_anim);
            Apply(target);
            return;
        }
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimateTo(target));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (onHovered != null) onHovered(this);
    }

    private IEnumerator AnimateTo(float target)
    {
        float start = _t;
        float elapsed = 0f;
        const float duration = 0.14f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            Apply(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
            yield return null;
        }
        Apply(target);
    }

    private void Apply(float t)
    {
        _t = t;
        if (_frameGroup != null) _frameGroup.alpha = t;
        if (_frameRect != null) _frameRect.localScale = Vector3.one * Mathf.Lerp(1.04f, 1f, t);
        if (_label != null)
        {
            _label.color = Color.Lerp(InkDark, InkRed, t);
            _label.rectTransform.anchoredPosition = _labelBasePos + new Vector2(12f * t, 0f);
        }
        if (_numeral != null) _numeral.color = Color.Lerp(InkMuted, InkRed, t);
    }
}
