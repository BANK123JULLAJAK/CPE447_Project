using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// ตัวจัดการระบบเควส Scene 2: สาส์นจากเงามืด (ตรอกในลอนดอน / London Alley)
/// - บรรยากาศ: มืดครึ้ม สกปรก มีหมอกลงจัดและศพผู้หญิงอยู่กลางตรอก
/// - ระบบสแกนหาหลักฐาน 5 ชิ้นรอบศพ (กระดุมเสื้อ, ตั๋ว, รอยเท้า, มีด, หนังสือพิมพ์)
/// - ระบบคัตซีนภาพยนตร์ 3 ช็อตมุมมองระดับ Movie Grade
/// - มินิเกมการถอดรหัสซีซาร์ (Caesar Cipher Minigame) เผยคำตอบ "THEATER" จาก Moriarty
/// - ระบบนำทาง Waypoint Navigation แสดงระยะทางเป็นเมตรแบบเรียลไทม์
/// </summary>
public class LondonAlleyQuestManager : MonoBehaviour
{
    private static LondonAlleyQuestManager _instance;
    public static LondonAlleyQuestManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<LondonAlleyQuestManager>();
            return _instance;
        }
        private set { _instance = value; }
    }

    [Header("Quest 2 Configuration")]
    public string questTitle = "<color=#F2C94C>◈ ภารกิจหลัก (เควสที่ 2)</color>  สาส์นจากเงามืด: ตรอกไวท์ชาเปล";
    public int totalEvidenceRequired = 5;

    [Header("Typography")]
    public Font thaiFont;
    public Font thaiFontBold;

    [Header("Alley Target & Cutscene Cameras")]
    public Transform alleyCenterTarget;
    public Transform theaterTarget;
    public CinemachineCamera camAlleyTracking;
    public CinemachineCamera camAlleyCorpseClose;
    public CinemachineCamera camAlleyNewspaperMacro;
    public CinemachineCamera playerFollowCamera;

    [Header("Crime Scene Elements")]
    public GameObject femaleVictimCorpse;
    public GameObject newspaperEvidenceGo;

    [Header("Developer Quick Test")]
    [Tooltip("เปิดใช้งานเพื่อทดสอบ Scene 2 ทันทีโดยไม่ต้องเล่น Scene 1 ก่อน")]
    public bool testScene2Directly = false;

    // State
    private int _scannedCount = 0;
    /// <summary>จำนวนวัตถุพยานที่สแกนแล้ว (ใช้แสดงในแฟ้มคดีเมนู E)</summary>
    public int ScannedCount => _scannedCount;
    private bool _hasEnteredAlley = false;
    private bool _isQuest2Active = false;
    public bool IsQuest2Active => _isQuest2Active;
    private bool _isDecryptionCompleted = false;
    private bool _isDeductionOpened = false;
    private float _scene2StartTime = -1f;
    private CaseDeductionBoardUI.Config _deductionConfig;
    private CaseDeductionBoardUI.Result _deductionResult;
    private HashSet<string> _scannedEvidenceIds = new HashSet<string>();

    // UI References
    private GameObject _objectivePanel;
    private Text _objectiveTitleText;
    private Text _objectiveProgressText;
    private Text _hintText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (thaiFont == null)
        {
            thaiFont = GameAssets.Load<Font>("Assets/Font/Prompt-Medium.ttf");
            if (thaiFont == null) thaiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (thaiFontBold == null)
        {
            thaiFontBold = GameAssets.Load<Font>("Assets/Font/Prompt-Bold.ttf");
            if (thaiFontBold == null) thaiFontBold = thaiFont;
        }

        BuildQuest2HUD();
    }

    private void Start()
    {
        ResetAlleyCutsceneCameras();

        // Subscribe to CiRA Core Vision Scanner
        if (CiraCoreVisionScanner.Instance != null)
        {
            CiraCoreVisionScanner.Instance.OnEvidenceScanned += HandleAlleyEvidenceScanned;
        }

        // Locate Alley Target
        if (alleyCenterTarget == null)
        {
            var alleyGo = GameObject.Find("ALLEY") ?? GameObject.Find("ALLEY_EVIDENCE_NEWSPAPER");
            if (alleyGo != null) alleyCenterTarget = alleyGo.transform;
        }

        // Locate Theater Target
        if (theaterTarget == null)
        {
            var thGo = GameObject.Find("THEATER");
            if (thGo != null) theaterTarget = thGo.transform;
        }

        if (testScene2Directly)
        {
            StartQuest2();
        }
    }

    public void ResetAlleyCutsceneCameras()
    {
        if (camAlleyTracking != null) camAlleyTracking.Priority.Value = 0;
        if (camAlleyCorpseClose != null) camAlleyCorpseClose.Priority.Value = 0;
        if (camAlleyNewspaperMacro != null) camAlleyNewspaperMacro.Priority.Value = 0;
        if (playerFollowCamera != null) playerFollowCamera.Priority.Value = 10;
        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.ResetAllCutsceneCameras();
        }
    }

    public void BuildQuest2HUD()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
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

        Transform existing = transform.Find("ObjectivePanel_Scene2");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }

        _objectivePanel = new GameObject("ObjectivePanel_Scene2", typeof(RectTransform), typeof(Image));
        _objectivePanel.transform.SetParent(transform, false);
        RectTransform oRect = _objectivePanel.GetComponent<RectTransform>();
        oRect.anchorMin = new Vector2(0f, 1f);
        oRect.anchorMax = new Vector2(0f, 1f);
        oRect.pivot = new Vector2(0f, 1f);
        oRect.anchoredPosition = new Vector2(40f, -40f);
        oRect.sizeDelta = new Vector2(660f, 120f);

        Image oImg = _objectivePanel.GetComponent<Image>();
        oImg.color = Color.clear; // ไร้กล่องพื้นหลัง แบบ Genshin Impact

        // Title
        GameObject tGo = new GameObject("ObjTitle", typeof(RectTransform), typeof(Text));
        tGo.transform.SetParent(_objectivePanel.transform, false);
        RectTransform tR = tGo.GetComponent<RectTransform>();
        tR.anchorMin = new Vector2(0f, 1f);
        tR.anchorMax = new Vector2(1f, 1f);
        tR.pivot = new Vector2(0f, 1f);
        tR.anchoredPosition = new Vector2(0f, 0f);
        tR.sizeDelta = new Vector2(0f, 32f);
        _objectiveTitleText = tGo.GetComponent<Text>();
        _objectiveTitleText.font = thaiFontBold != null ? thaiFontBold : VisionOSUITheme.BoldFont;
        _objectiveTitleText.fontSize = 20;
        _objectiveTitleText.color = VisionOSUITheme.TextPrimary;
        _objectiveTitleText.alignment = TextAnchor.MiddleLeft;
        _objectiveTitleText.text = "<color=#F2C94C>◆ ภารกิจหลัก (เควสที่ 2)</color>  สาส์นลวงจากเงามืด: ตรอกไวท์ชาเปล";
        var tShadow = tGo.AddComponent<Shadow>();
        tShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        tShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // Progress
        GameObject pGo = new GameObject("ObjProgress", typeof(RectTransform), typeof(Text));
        pGo.transform.SetParent(_objectivePanel.transform, false);
        RectTransform pR = pGo.GetComponent<RectTransform>();
        pR.anchorMin = new Vector2(0f, 1f);
        pR.anchorMax = new Vector2(1f, 1f);
        pR.pivot = new Vector2(0f, 1f);
        pR.anchoredPosition = new Vector2(0f, -34f);
        pR.sizeDelta = new Vector2(0f, 28f);
        _objectiveProgressText = pGo.GetComponent<Text>();
        _objectiveProgressText.font = thaiFont != null ? thaiFont : VisionOSUITheme.PrimaryFont;
        _objectiveProgressText.fontSize = 17;
        _objectiveProgressText.color = VisionOSUITheme.TextPrimary;
        _objectiveProgressText.alignment = TextAnchor.MiddleLeft;
        _objectiveProgressText.text = "• มุ่งหน้าสู่ตรอกไวท์ชาเปลเพื่อแกะรอยผู้ต้องสงสัย";
        var pShadow = pGo.AddComponent<Shadow>();
        pShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        pShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // Hint
        GameObject hGo = new GameObject("ObjHint", typeof(RectTransform), typeof(Text));
        hGo.transform.SetParent(_objectivePanel.transform, false);
        RectTransform hR = hGo.GetComponent<RectTransform>();
        hR.anchorMin = new Vector2(0f, 1f);
        hR.anchorMax = new Vector2(1f, 1f);
        hR.pivot = new Vector2(0f, 1f);
        hR.anchoredPosition = new Vector2(0f, -64f);
        hR.sizeDelta = new Vector2(0f, 26f);
        _hintText = hGo.GetComponent<Text>();
        _hintText.font = thaiFont != null ? thaiFont : VisionOSUITheme.PrimaryFont;
        _hintText.fontSize = 14;
        _hintText.color = new Color(0.95f, 0.82f, 0.42f, 1f);
        _hintText.alignment = TextAnchor.MiddleLeft;
        _hintText.text = "◆ เดินตามหมุดนำทางบนหน้าจอไปยังพิกัดตรอกลอนดอน";
        var hShadow = hGo.AddComponent<Shadow>();
        hShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        hShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // Hotkey Prompt
        GameObject hotkeyGo = new GameObject("ObjCaseArchivePrompt", typeof(RectTransform), typeof(Text));
        hotkeyGo.transform.SetParent(_objectivePanel.transform, false);
        RectTransform hkRect = hotkeyGo.GetComponent<RectTransform>();
        hkRect.anchorMin = new Vector2(0f, 1f);
        hkRect.anchorMax = new Vector2(1f, 1f);
        hkRect.pivot = new Vector2(0f, 1f);
        hkRect.anchoredPosition = new Vector2(0f, -94f);
        hkRect.sizeDelta = new Vector2(0f, 24f);
        Text hkt = hotkeyGo.GetComponent<Text>();
        hkt.font = thaiFont != null ? thaiFont : VisionOSUITheme.PrimaryFont;
        hkt.fontSize = 13;
        hkt.alignment = TextAnchor.MiddleLeft;
        hkt.color = VisionOSUITheme.TextSecondary;
        hkt.text = "<color=#F2C94C><b>[E]</b></color> เมนูเควส & แฟ้มบันทึกคดี";
        var hkShadow = hotkeyGo.AddComponent<Shadow>();
        hkShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        hkShadow.effectDistance = new Vector2(1.5f, -1.5f);

        _objectivePanel.SetActive(false);
    }

    public void ShowObjectiveHUD(bool show)
    {
        if (_objectivePanel != null) _objectivePanel.SetActive(show);

        if (show)
        {
            // ซ่อน HUD ของ Scene 1 และ Scene 3 ป้องกันการซ้อนทับกัน
            if (StMaryChurchQuestManager.Instance != null)
            {
                StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
            }
            var q1Hud = GameObject.Find("ObjectivePanel");
            if (q1Hud != null) q1Hud.SetActive(false);
            var q3Hud = GameObject.Find("Quest3_LyceumTheater_HUD");
            if (q3Hud != null) q3Hud.SetActive(false);
        }
    }

    /// <summary>
    /// เริ่มต้นเควสที่ 2 อย่างเป็นทางการ (เรียกเมื่อผู้เล่นรับเควสจาก Scene 1 หรือเริ่ม Scene 2)
    /// </summary>
    public void StartQuest2()
    {
        _isQuest2Active = true;
        if (CiraCoreVisionScanner.Instance != null) CiraCoreVisionScanner.Instance.SetActiveSceneModel(2);
        _hasEnteredAlley = false;
        _scannedCount = 0;
        _scannedEvidenceIds.Clear();

        // 1. ซ่อนและปิดการทำงานของ Scene 1 HUD และ Deduction Board ทั้งหมด 100% ป้องกันแท็บเควสซ้อนทับกัน
        if (StMaryChurchQuestManager.Instance != null)
        {
            StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
            var p1 = StMaryChurchQuestManager.Instance.transform.Find("ObjectivePanel");
            if (p1 != null) p1.gameObject.SetActive(false);
            var d1 = StMaryChurchQuestManager.Instance.transform.Find("DeductionPanel");
            if (d1 != null) d1.gameObject.SetActive(false);
            StMaryChurchQuestManager.Instance.enabled = false;
        }

        var allPanels = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in allPanels)
        {
            if (go != null && go.name == "ObjectivePanel" && go.transform.parent != null && go.transform.parent.name == "Quest1_StMaryChurch_System")
            {
                go.SetActive(false);
            }
        }

        // Ensure CiRA CORE scanner is unblocked and subscribed
        CiraCoreVisionScanner.IsScannerBlocked = false;
        if (CiraCoreVisionScanner.Instance != null)
        {
            CiraCoreVisionScanner.Instance.OnEvidenceScanned -= HandleAlleyEvidenceScanned;
            CiraCoreVisionScanner.Instance.OnEvidenceScanned += HandleAlleyEvidenceScanned;
        }

        if (_objectivePanel != null) _objectivePanel.SetActive(true);
        if (_objectiveTitleText != null) _objectiveTitleText.text = questTitle;
        if (_hintText != null) _hintText.text = "◈ เดินตามหมุดนำทางบนหน้าจอมุ่งหน้าสู่ตรอกไวท์ชาเปล";

        // เปิดระบบนำทาง Waypoint ชี้ไปที่ตรอกลอนดอน
        Transform target = null;
        var alleyTrigger = GameObject.Find("Alley_Entry_AreaTrigger");
        if (alleyTrigger != null) target = alleyTrigger.transform;
        else if (alleyCenterTarget != null) target = alleyCenterTarget;

        if (WaypointNavigationSystem.Instance != null && target != null)
        {
            WaypointNavigationSystem.Instance.SetTarget(target, "ตรอกไวท์ชาเปล (Whitechapel Alley)");
            WaypointNavigationSystem.Instance.SetVisible(true);
        }
    }

    private void Update()
    {
        if (!_isQuest2Active) return;

        // เฟส 1: เดินทางสู่ตรอกไวท์ชาเปล (แสดงระยะทางแบบ Genshin Impact)
        if (!_hasEnteredAlley)
        {
            int dist = WaypointNavigationSystem.Instance != null 
                ? WaypointNavigationSystem.Instance.CurrentDistanceMeters 
                : 0;

            if (_objectiveProgressText != null)
            {
                if (dist > 4)
                {
                    _objectiveProgressText.text = $"• เดินทางสู่ตรอกไวท์ชาเปล  <color=#FFD24C><b>{dist}m</b></color>";
                }
                else
                {
                    _objectiveProgressText.text = "• เดินทางสู่ตรอกไวท์ชาเปล  <color=#66FF99><b>[ถึงเป้าหมายแล้ว]</b></color>";
                }
            }
        }
        // เฟส 3: หลังถอดรหัส THEATER สำเร็จ มุ่งหน้าสู่โรงละคร (แสดงระยะทางแบบ Genshin Impact)
        else if (_isDecryptionCompleted)
        {
            int dist = WaypointNavigationSystem.Instance != null 
                ? WaypointNavigationSystem.Instance.CurrentDistanceMeters 
                : 0;

            if (_objectiveProgressText != null)
            {
                if (dist > 4)
                {
                    _objectiveProgressText.text = $"• มุ่งหน้าสู่โรงละครรอยัลไลเซียม  <color=#FFD24C><b>{dist}m</b></color>";
                }
                else
                {
                    _objectiveProgressText.text = "• มุ่งหน้าสู่โรงละครรอยัลไลเซียม  <color=#66FF99><b>[ถึงเป้าหมายแล้ว]</b></color>";
                }
            }
        }
    }

    /// <summary>
    /// เรียกเมื่อผู้เล่นเดินเข้าสู่พื้นที่ตรอก (Alley_Entry_AreaTrigger)
    /// </summary>
    public void OnEnterAlleyArea()
    {
        if (!_isQuest2Active) return; // ล็อคไม่ให้คัตซีน Scene 2 เผลอเล่นก่อนที่ผู้เล่นจะจบ Scene 1
        if (_hasEnteredAlley) return;
        _hasEnteredAlley = true;
        _scene2StartTime = Time.time;

        // เคลียร์และซ่อนหมุดนำทางของทางเข้าตรอกทันที เพื่อไม่ให้หมุดค้างและตัวเลขเมตรค้างบนหน้าจอขณะอยู่ในตรอก
        if (WaypointNavigationSystem.Instance != null)
        {
            WaypointNavigationSystem.Instance.ClearTarget();
            WaypointNavigationSystem.Instance.SetVisible(false);
        }

        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.PlayScene2IntroCinematic(
                femaleVictimCorpse != null ? femaleVictimCorpse.transform : null,
                newspaperEvidenceGo != null ? newspaperEvidenceGo.transform : null,
                () => {
                    // มั่นใจว่าหมุดนำทางยังคงถูกเคลียร์อยู่ (จะเปิดใหม่อีกทีเมื่อถอดรหัสซีซาร์สำเร็จเพื่อไปโรงละคร)
                    if (WaypointNavigationSystem.Instance != null)
                    {
                        WaypointNavigationSystem.Instance.ClearTarget();
                        WaypointNavigationSystem.Instance.SetVisible(false);
                    }

                    ShowObjectiveHUD(true);
                    UpdateAlleyObjectiveDisplay();
                    if (_hintText != null) _hintText.text = "◈ [คลิกขวา] หรือ [V] เพื่อเปิดโหมดสแกน CiRA CORE ค้นหาวัตถุพยาน 5 ชิ้น";
                }
            );
        }
        else
        {
            StartCoroutine(PlayAlleyIntroCutsceneRoutine());
        }
    }

    private IEnumerator PlayAlleyIntroCutsceneRoutine()
    {
        // ซ่อน HUD และหมุดนำทางชั่วคราวระหว่างเล่นคัตซีน
        ShowObjectiveHUD(false);
        if (WaypointNavigationSystem.Instance != null)
        {
            WaypointNavigationSystem.Instance.ClearTarget();
            WaypointNavigationSystem.Instance.SetVisible(false);
        }

        // ล็อคการควบคุมตัวละคร
        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.SetPlayerControlsLocked(true);
        }

        // แสดงแถบดำภาพยนตร์
        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.ShowLetterbox();
        }

        // =========================================================================
        // Shot 1: Alley Tracking Shot (มุมกล้องลื่นไหลส่องบรรยากาศตรอกมืดครึ้ม สกปรก และหมอกหนาทึบ)
        // =========================================================================
        SwitchCameraPriority(camAlleyTracking, 20);
        yield return new WaitForSeconds(0.4f);

        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.ShowDialogue(
                "สารวัตรนิวแมน (Inspector Newman)",
                "ตรอกไวท์ชาเปล (Whitechapel Alley)... ค่ำคืนนี้ชื้นแฉะและวังเวง กลิ่นคาวเลือดคละคลุ้งตัดกับไอหมอกหนาทึบ"
            );
        }
        yield return new WaitForSeconds(4.2f);

        // =========================================================================
        // Shot 2: Close-up on Newman & Corpse (มุมโคลสอัพนิวแมนพบศพหญิงสาวกลางตรอก)
        // =========================================================================
        SwitchCameraPriority(camAlleyCorpseClose, 20);
        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.ShowDialogue(
                "สารวัตรนิวแมน (Inspector Newman)",
                "พบร่างเหยื่อหญิงสาวถูกสังหารอย่างเหี้ยมโหดอยู่กลางตรอก... แต่ดูเหมือนคนร้ายจงใจทิ้งร่องรอยบางอย่างไว้รอบๆ ตัวเธอ!"
            );
        }
        yield return new WaitForSeconds(4.5f);

        // =========================================================================
        // Shot 3: Macro Push on Mysterious Newspaper (มุมกล้องดิ่งซูมเข้าสู่หนังสือพิมพ์ปริศนา)
        // =========================================================================
        SwitchCameraPriority(camAlleyNewspaperMacro, 20);
        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.ShowDialogue(
                "สารวัตรนิวแมน (Inspector Newman)",
                "หนังสือพิมพ์ฉบับนี้ถูกวางข้างศพอย่างประณีต... บนหน้าหนึ่งมีรอยขีดเขียนสีเลือดที่ดูคล้าย 'รหัสลับ'!"
            );
        }
        yield return new WaitForSeconds(4.2f);

        // =========================================================================
        // Return to Player Gameplay Camera
        // =========================================================================
        ResetAlleyCutsceneCameras();
        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.ResetAllCutsceneCameras();
            CinematicCutsceneManager.Instance.SetPlayerControlsLocked(false);
        }
        yield return new WaitForSeconds(0.2f);

        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.HideLetterbox();
        }

        // คืนค่า HUD และเปิดโหมดสืบสวน
        ShowObjectiveHUD(true);
        UpdateAlleyObjectiveDisplay();
        if (_hintText != null) _hintText.text = "[คลิกขวา] หรือ [V] เพื่อเปิดโหมดสแกน CiRA CORE หาหลักฐาน 5 ชิ้น";
        if (WaypointNavigationSystem.Instance != null)
        {
            WaypointNavigationSystem.Instance.ClearTarget();
            WaypointNavigationSystem.Instance.SetVisible(false);
        }
    }

    private void SwitchCameraPriority(CinemachineCamera targetCam, int priority)
    {
        if (camAlleyTracking != null) camAlleyTracking.Priority.Value = 0;
        if (camAlleyCorpseClose != null) camAlleyCorpseClose.Priority.Value = 0;
        if (camAlleyNewspaperMacro != null) camAlleyNewspaperMacro.Priority.Value = 0;
        if (playerFollowCamera != null) playerFollowCamera.Priority.Value = 10;

        if (targetCam != null)
        {
            targetCam.Priority.Value = priority;
        }
    }

    private void HandleAlleyEvidenceScanned(EvidenceItem item)
    {
        if (!_isQuest2Active) return;
        if (item == null) return;
        if (item.gameObject.name.ToUpper().StartsWith("CHURCH_")) return;

        string evidenceId = item.evidenceId;
        if (string.IsNullOrEmpty(evidenceId)) evidenceId = item.ciraClassName;
        if (string.IsNullOrEmpty(evidenceId)) return;

        // ตรวจสอบว่าหลักฐานเป็นของ Scene 2 หรือไม่
        string lower = evidenceId.ToLower();
        bool isScene2Evidence = lower.Contains("shirt_button") || lower.Contains("button")
                             || lower.Contains("ticket")
                             || (lower.Contains("footprint") && item.gameObject.name.ToUpper().StartsWith("ALLEY_"))
                             || lower.Contains("knife")
                             || lower.Contains("newspaper");

        if (!isScene2Evidence) return;

        if (!_scannedEvidenceIds.Contains(evidenceId))
        {
            _scannedEvidenceIds.Add(evidenceId);
            _scannedCount++;
            UpdateAlleyObjectiveDisplay();

            // นิวแมนพูดสั้น ๆ ว่าหลักฐานชิ้นนี้บอกอะไร (ไม่หยุดเกม)
            DetectiveThoughtToast.Show(GetAlleyEvidenceThought(lower));

            // เมื่อสแกนหลักฐานครบ 5 ชิ้น -> ตอบข้อสันนิษฐาน -> ถอดรหัสหนังสือพิมพ์ -> เฉลยสาส์นมอริอาร์ตี้
            if (_scannedCount >= totalEvidenceRequired && !_isDeductionOpened && !_isDecryptionCompleted)
            {
                _isDeductionOpened = true;
                StartCoroutine(OpenAlleyDeductionRoutine());
            }
        }
    }

    /// <summary>ความคิดของนิวแมนเมื่อสแกนพบหลักฐานแต่ละชิ้นของบทที่ 2 (ปูไปสู่ข้อสรุปว่ามีบุคคลที่สอง)</summary>
    private static string GetAlleyEvidenceThought(string lowerId)
    {
        if (lowerId.Contains("button"))
            return "กระดุมแก้วขาดพร้อมเศษผ้าขนสัตว์สีเทา ไม่ใช่เสื้อของเธอ เธอฉีกมันมาจากคนที่ยื้อยุดกับเธอ";
        if (lowerId.Contains("ticket"))
            return "ตั๋วรถไฟสายเหนือชั้นหนึ่ง คนแต่งตัวมอซอแบบเธอไม่ควรมีมัน แล้วใครให้เธอมา?";
        if (lowerId.Contains("footprint"))
            return "รอยรองเท้าหนังขัดมัน ปลายแหลม คนละคู่กับรอยบู๊ตในโบสถ์ ผู้ชายสองคนในคืนเดียวกัน";
        if (lowerId.Contains("knife"))
            return "มีดผ่าตัดเปื้อนเลือด ถูกวางขนานกับศพ ไม่ได้ถูกทิ้ง คนที่ฆ่าด้วยอารมณ์ไม่วางของแบบนี้";
        if (lowerId.Contains("newspaper"))
            return "The Daily Telegraph พับเรียบร้อย มีรอยหมึกแดงใต้ตัวอักษรบางตัว นี่ไม่ใช่ข่าว แต่เป็นข้อความ";
        return null;
    }

    private IEnumerator OpenAlleyDeductionRoutine()
    {
        // รอให้ผู้เล่นอ่านความคิดของนิวแมนชิ้นสุดท้ายจบก่อน
        yield return new WaitForSeconds(5.5f);

        if (CiraCoreVisionScanner.Instance != null && CiraCoreVisionScanner.Instance.IsScanningActive)
        {
            CiraCoreVisionScanner.Instance.ToggleScanMode(false);
        }

        ShowObjectiveHUD(false);
        DetectiveThoughtToast.Clear();
        if (CinematicLetterbox.Instance != null) CinematicLetterbox.Instance.ForceDisableLetterbox();
        if (CinematicCutsceneManager.Instance != null) CinematicCutsceneManager.Instance.SetPlayerControlsLocked(true);
        SetCursorLockedState(false);

        CiraCoreVisionScanner.IsScannerBlocked = true;
        if (CiraCoreVisionScanner.Instance != null) CiraCoreVisionScanner.Instance.ForceCloseScanMode();
        if (WaypointNavigationSystem.Instance != null) WaypointNavigationSystem.Instance.SetVisible(false);

        _deductionConfig = BuildAlleyDeductionConfig();
        CaseDeductionBoardUI.Open(_deductionConfig, OnAlleyDeductionSolved);
    }

    /// <summary>
    /// ข้อสันนิษฐานบทที่ 2 (3 ขั้น): หลักฐานที่ถูกจัดวาง -> มีบุคคลที่สอง -> สาส์นในหนังสือพิมพ์คือรหัส
    /// </summary>
    private CaseDeductionBoardUI.Config BuildAlleyDeductionConfig()
    {
        var config = new CaseDeductionBoardUI.Config
        {
            letterhead = "METROPOLITAN POLICE  ·  CASE FILE 1888/JR  ·  WHITECHAPEL ALLEY",
            openQuestion = "ใครคือคนที่สอง และเขาต้องการให้เราไปที่ใด?"
        };

        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("shirt_button", "กระดุมเสื้อขาด",
            "กระดุมแก้วพร้อมเศษผ้าขนสัตว์สีเทา ไม่ใช่เสื้อของเหยื่อ"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("ticket", "ตั๋วรถไฟลอนดอน",
            "สายเหนือ ชั้นหนึ่ง ราคาที่เหยื่อไม่น่าจะซื้อไหว"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("footprint", "รอยเท้าในตรอก",
            "รองเท้าหนังขัดมันปลายแหลม ต่างจากรอยบู๊ตในโบสถ์"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("knife", "มีดผ่าตัด",
            "เปื้อนเลือด ถูกวางขนานกับศพ ไม่ได้ถูกทิ้ง"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("newspaper", "หนังสือพิมพ์ Daily Telegraph",
            "พับเรียบร้อย มีรอยหมึกแดงใต้ตัวอักษรบางตัว"));

        // ข้อสันนิษฐานที่ 1: อะไรคือสิ่งที่ถูกจัดวางไว้เป็นข้อความ
        var s1 = new CaseDeductionBoardUI.Step
        {
            question = "ในห้าชิ้นนี้ มีชิ้นเดียวที่ไม่ใช่ผลของการฆาตกรรมโดยตรง แต่ถูกจัดวางไว้อย่างตั้งใจ คือชิ้นใด?",
            conclusion = "หนังสือพิมพ์คือสิ่งเดียวที่ถูกวางอย่างจงใจ ไม่ใช่ผลของการฆาตกรรม แต่เป็นข้อความที่ฝากไว้ให้เรา"
        };
        s1.options.Add(new CaseDeductionBoardUI.Option("กระดุมเสื้อที่ขาดกระเด็นจากเสื้อของใครสักคน", false,
            "กระดุมขาดเพราะการยื้อยุด เป็นผลของการต่อสู้ ไม่ได้มีใครตั้งใจวางมันไว้"));
        s1.options.Add(new CaseDeductionBoardUI.Option("ตั๋วรถไฟลอนดอนสายเหนือ", false,
            "ตั๋วอาจหล่นจากกระเป๋าของเหยื่อได้ตามธรรมชาติ ยังไม่พอจะสรุปว่ามีใครจัดวางมัน"));
        s1.options.Add(new CaseDeductionBoardUI.Option("มีดผ่าตัดเปื้อนเลือด", false,
            "มีดคืออาวุธที่ต้องอยู่ในที่เกิดเหตุอยู่แล้ว วิธีวางอาจประณีต แต่ตัวมันคือผลของการฆ่า"));
        s1.options.Add(new CaseDeductionBoardUI.Option("หนังสือพิมพ์ที่พับเรียบร้อยและมีรอยหมึกแดง", true,
            "ไม่มีเหตุผลทางการฆ่าที่ต้องพับหนังสือพิมพ์ให้เรียบร้อยและขีดตัวอักษร มันถูกเตรียมมาให้คนที่จะมาพบศพอ่านโดยเฉพาะ"));
        config.steps.Add(s1);

        // ข้อสันนิษฐานที่ 2: มีบุคคลที่สอง
        var s2 = new CaseDeductionBoardUI.Step
        {
            question = "รอยรองเท้าหนังขัดมันในตรอกไม่ตรงกับรอยบู๊ตในโบสถ์ และมีดผ่าตัดถูกวางเรียงขนานกับศพแทนที่จะถูกทิ้ง ข้อใดอธิบายได้ดีที่สุด?",
            relatedEvidenceIds = new[] { "footprint", "knife" },
            conclusion = "รองเท้าหนังขัดมันต่างจากรอยบู๊ตในโบสถ์ มีบุคคลที่สองอยู่ในที่เกิดเหตุ เขาไม่ได้ลงมือ แต่จัดฉากต่อจากแจ็ค"
        };
        s2.options.Add(new CaseDeductionBoardUI.Option("แจ็คเปลี่ยนรองเท้าเพื่ออำพรางตัว", false,
            "ที่โบสถ์เขาไม่เคยพยายามปกปิดอะไรเลย ไม่มีเหตุให้ต้องเปลี่ยนรองเท้าเฉพาะที่นี่"));
        s2.options.Add(new CaseDeductionBoardUI.Option("มีคนที่สองมาถึงหลังแจ็ค เขาไม่ได้ลงมือ แต่จัดฉากต่อ", true,
            "รองเท้าหนังขัดมันเป็นของผู้ชายฐานะดี ต่างจากบู๊ตของแจ็ค มีดที่ถูกวางเรียงกับหนังสือพิมพ์ที่พับเรียบร้อยคือฝีมือคนที่ชอบจัดวาง ไม่ใช่คนที่ฆ่าด้วยอารมณ์"));
        s2.options.Add(new CaseDeductionBoardUI.Option("ตำรวจในพื้นที่เข้ามาก่อนเราและเปลี่ยนที่เกิดเหตุ", false,
            "ไม่มีหลักฐานใดชี้ไปทางตำรวจ และตำรวจจะไม่วางมีดเรียงสวยให้เป็นหลักฐานชี้ตัว"));
        s2.options.Add(new CaseDeductionBoardUI.Option("เป็นรอยเท้าของคนเดินผ่านมาโดยบังเอิญ", false,
            "คนเดินผ่านจะไม่เดินวนรอบศพ ไม่จัดวางของ และไม่ทิ้งรอยเท้าไว้ลึกถึงปานนั้น"));
        config.steps.Add(s2);

        // ข้อสันนิษฐานที่ 3: ต้องถอดรหัส
        var s3 = new CaseDeductionBoardUI.Step
        {
            question = "ใต้หนังสือพิมพ์มีรอยหมึกแดงขีดทับตัวอักษรบางตัว เมื่อรวมกับตั๋วรถไฟชั้นหนึ่งที่เหยื่อไม่น่ามีเงินซื้อ เราควรทำอย่างไรต่อ?",
            relatedEvidenceIds = new[] { "newspaper", "ticket" },
            conclusion = "ตัวอักษรที่ถูกขีดใต้หนังสือพิมพ์คือรหัสที่ตั้งใจฝากถึงสก็อตแลนด์ยาร์ด ต้องถอดให้ได้"
        };
        s3.options.Add(new CaseDeductionBoardUI.Option("ปล่อยให้ตำรวจท้องที่จัดการหนังสือพิมพ์ แล้วเราไปตามรถไฟสายเหนือ", false,
            "สาส์นนี้เขียนถึงเราโดยตรง ถ้าไม่อ่านก็คือเสียเปรียบตั้งแต่ก้าวแรก ส่วนตั๋วใบเดียวบอกได้แค่ว่าเหยื่อเคยนั่งรถไฟ"));
        s3.options.Add(new CaseDeductionBoardUI.Option("ส่องหาตัวอักษรที่ถูกขีดทั้งหมด แล้วถอดรหัสให้ได้", true,
            "ตัวอักษรที่ถูกขีดไม่ได้เรียงเป็นคำ แปลว่าถูกเข้ารหัส และคนที่จัดฉากประณีตแบบนี้ต้องการให้เราถอดมันออก"));
        s3.options.Add(new CaseDeductionBoardUI.Option("เผาหนังสือพิมพ์ทิ้งเพื่อไม่ให้ประชาชนตื่นตระหนก", false,
            "นั่นคือการทำลายหลักฐานชิ้นเดียวที่ชี้ถึงคนที่สอง"));
        s3.options.Add(new CaseDeductionBoardUI.Option("รอให้มีศพที่สามแล้วค่อยหาแบบแผนที่ซ้ำกัน", false,
            "การรอให้มีเหยื่อเพิ่มคือการยอมเป็นฝ่ายตั้งรับตามกติกาของเขา"));
        config.steps.Add(s3);

        return config;
    }

    private void OnAlleyDeductionSolved(CaseDeductionBoardUI.Result result)
    {
        _deductionResult = result;
        StartCoroutine(TriggerNewspaperDecryptionRoutine());
    }

    private void SetCursorLockedState(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;

        var allInputs = FindObjectsByType<StarterAssets.StarterAssetsInputs>(FindObjectsInactive.Include);
        foreach (var inputs in allInputs)
        {
            inputs.cursorLocked = locked;
            inputs.cursorInputForLook = locked;
        }
    }

    private void UpdateAlleyObjectiveDisplay()
    {
        if (_objectiveProgressText != null)
        {
            if (_scannedCount < totalEvidenceRequired)
            {
                _objectiveProgressText.text = $"• สแกนหาหลักฐานในตรอกด้วย CiRA CORE: <color=#5CE1E6><b>{_scannedCount} / {totalEvidenceRequired}</b></color>";
            }
            else
            {
                _objectiveProgressText.text = "• หลักฐานครบ 5 ชิ้นแล้ว! <color=#66FF99><b>ตรวจสอบรหัสลับบนหน้าหนังสือพิมพ์</b></color>";
            }
        }
    }

    private IEnumerator TriggerNewspaperDecryptionRoutine()
    {
        // ซ่อนหน้าต่างเควสและปิดแถบดำภาพยนตร์ออกทันที ไม่ให้มีคร็อปดำบนล่างในหน้ามินิเกม
        ShowObjectiveHUD(false);
        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.ForceDisableLetterbox();
        }

        yield return new WaitForSeconds(0.2f);

        // เปิดมินิเกมการถอดรหัสซีซาร์ (Newspaper Decryption Minigame) เต็มจอปกติ
        if (NewspaperDecryptionMinigame.Instance != null)
        {
            NewspaperDecryptionMinigame.Instance.OpenMinigame(OnDecryptionPuzzleSolved);
        }
    }

    /// <summary>
    /// เรียกเมื่อผู้เล่นถอดรหัส Caesar Cipher สำเร็จได้คำว่า "THEATER"
    /// </summary>
    private void OnDecryptionPuzzleSolved()
    {
        _isDecryptionCompleted = true;

        // กันไม่ให้เปิดโหมดสแกนระหว่างคัตซีนเฉลยและหน้าสรุปบท
        CiraCoreVisionScanner.IsScannerBlocked = true;

        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.PlayScene2MoriartyRevealCinematic(
                femaleVictimCorpse != null ? femaleVictimCorpse.transform : null,
                ShowChapter2Summary
            );
        }
        else
        {
            StartCoroutine(MoriartyRevealDialogueRoutine());
        }
    }

    /// <summary>หน้าสรุปบทที่ 2 หลังเฉลยสาส์นของมอริอาร์ตี้</summary>
    private void ShowChapter2Summary()
    {
        // ล็อกผู้เล่นไว้ตลอดช่วงหน้าสรุป (คัตซีนปล่อยล็อกให้เราจัดการเอง)
        if (CinematicCutsceneManager.Instance != null) CinematicCutsceneManager.Instance.SetPlayerControlsLocked(true);
        SetCursorLockedState(false);

        var data = new ChapterSummaryUI.Data
        {
            chapterNumber = 2,
            chapterTitle = "สาส์นลวงจากเงามืด",
            location = "ตรอกไวท์ชาเปล  ·  ลอนดอน  ·  ค.ศ. 1888",
            openQuestion = "มอริอาร์ตี้ต้องการอะไรจากเราที่โรงละคร และเหยื่อรายต่อไปคือใคร?",
            nextLead = "เที่ยงคืนนี้ม่านที่โรงละครรอยัลไลเซียมจะเปิด และมีนักแสดงสาวคนหนึ่งยังไม่รู้ว่าเธอคือเหยื่อรายต่อไป",
            elapsedSeconds = _scene2StartTime > 0f ? Time.time - _scene2StartTime : -1f,
            accuracy = _deductionResult != null ? _deductionResult.accuracy : -1
        };

        if (_deductionConfig != null)
        {
            foreach (var step in _deductionConfig.steps) data.chain.Add(step.conclusion);
        }
        data.chain.Add("รหัส WKHDWHU เลื่อนถอยหลัง 3 ตำแหน่ง ได้คำว่า THEATER ซึ่งเป็นจุดนัดพบที่ฝ่ายตรงข้ามเลือกไว้");

        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("กระดุมเสื้อขาด", "เศษผ้าขนสัตว์สีเทา ไม่ใช่เสื้อของเหยื่อ"));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("ตั๋วรถไฟชั้นหนึ่ง", "ของที่เหยื่อไม่น่าจะซื้อเอง"));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("รอยเท้าหนังขัดมัน", "ต่างจากรอยบู๊ตในโบสถ์ มีบุคคลที่สอง"));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("มีดผ่าตัด", "ถูกวางเรียงขนานกับศพ ไม่ได้ถูกทิ้ง"));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("หนังสือพิมพ์ Daily Telegraph", "รหัสซีซาร์ WKHDWHU = THEATER สาส์นของมอริอาร์ตี้", true));

        ChapterSummaryUI.Show(data, OnChapter2SummaryContinue);
    }

    private void OnChapter2SummaryContinue()
    {
        CiraCoreVisionScanner.IsScannerBlocked = false;
        SetCursorLockedState(true);
        if (CinematicCutsceneManager.Instance != null) CinematicCutsceneManager.Instance.SetPlayerControlsLocked(false);
        if (CinematicLetterbox.Instance != null) CinematicLetterbox.Instance.HideLetterbox();

        ShowObjectiveHUD(true);

        // อัปเดตภารกิจสู่ Scene 3 (โรงละครรอยัลไลเซียม)
        if (_objectiveTitleText != null)
        {
            _objectiveTitleText.text = "<color=#F2C94C>◈ ภารกิจหลัก (เควสที่ 3)</color>  เฉียดฉิว: โรงละครรอยัลไลเซียม";
        }
        if (_objectiveProgressText != null)
        {
            _objectiveProgressText.text = "• มุ่งหน้าสู่โรงละครรอยัลไลเซียม ก่อนที่ม่านจะเปิดตอนเที่ยงคืน";
        }
        if (_hintText != null)
        {
            _hintText.text = "◈ ดูเข็มทิศบนหัวจอและเดินตามหมุดนำทางไปยังโรงละคร";
        }

        // อัปเดตระบบนำทาง Waypoint Navigation ให้ชี้ไปที่โรงละคร THEATER ทันที!
        if (WaypointNavigationSystem.Instance != null && theaterTarget != null)
        {
            WaypointNavigationSystem.Instance.SetTarget(theaterTarget, "โรงละครรอยัลไลเซียม (Royal Lyceum Theatre)");
            WaypointNavigationSystem.Instance.SetVisible(true);
        }

        if (RoyalLyceumTheaterQuestManager.Instance != null)
        {
            RoyalLyceumTheaterQuestManager.Instance.ActivateQuest3();
        }
    }

    private IEnumerator MoriartyRevealDialogueRoutine()
    {
        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.ShowLetterbox();
            CinematicLetterbox.Instance.ShowDialogue(
                "ศาสตราจารย์ มอริอาร์ตี้ (Professor James Moriarty)",
                "\"น่าประทับใจมาก สารวัตรนิวแมน... แจ็ค เดอะ ริปเปอร์ เป็นเพียงเบี้ยตัวเล็กในกระดานของฉัน แต่สถานที่จริงรอคุณอยู่ที่ 'THEATER' (โรงละครรอยัลไลเซียม)!\""
            );
        }

        yield return new WaitForSeconds(5.0f);

        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.ShowDialogue(
                "สารวัตรนิวแมน (Inspector Newman)",
                "มอริอาร์ตี้! มันกำลังท้าทายสก็อตแลนด์ยาร์ด... เราต้องรีบมุ่งหน้าไปยัง 'โรงละครรอยัลไลเซียม' ก่อนที่จะเกิดการฆาตกรรมครั้งต่อไป!"
            );
        }

        yield return new WaitForSeconds(4.5f);

        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.HideLetterbox();
        }

        // อัปเดตภารกิจสู่ Scene 3 (โรงละครรอยัลไลเซียม)
        if (_objectiveTitleText != null)
        {
            _objectiveTitleText.text = "<color=#F2C94C>◈ ภารกิจหลัก (เควสที่ 3)</color>  กับดักแห่งโรงละคร (The Royal Lyceum Theatre)";
        }
        if (_objectiveProgressText != null)
        {
            _objectiveProgressText.text = "• มุ่งหน้าสู่โรงละครรอยัลไลเซียมเพื่อตามล่าเบาะแสของมอริอาร์ตี้";
        }
        if (_hintText != null)
        {
            _hintText.text = "◈ ปริศนาตรอกมืดคลี่คลาย! เดินตามหมุดนำทางไปยังโรงละคร";
        }

        // อัปเดตระบบนำทาง Waypoint Navigation ให้ชี้ไปที่โรงละคร THEATER ทันที!
        if (WaypointNavigationSystem.Instance != null && theaterTarget != null)
        {
            WaypointNavigationSystem.Instance.SetTarget(theaterTarget, "โรงละครรอยัลไลเซียม (Royal Lyceum Theatre)");
            WaypointNavigationSystem.Instance.SetVisible(true);
        }

        if (RoyalLyceumTheaterQuestManager.Instance != null)
        {
            RoyalLyceumTheaterQuestManager.Instance.ActivateQuest3();
        }
    }
}
