using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// ตัวจัดการระบบเควส Scene 3: เฉียดฉิว (โรงละครรอยัลไลเซียม / The Royal Lyceum Theatre)
/// - บรรยากาศ: หรูหราแต่เละเทะ มีร่องรอยการต่อสู้อย่างดุเดือด
/// - คัตซีนภาพยนตร์ระดับเทพ: สารวัตรนิวแมนบุกมาถึงก่อนที่ Jack จะฆ่าเหยื่อสำเร็จ แต่ Jack ไหวตัวทันและหนีไปได้
/// - ระบบสแกนหาหลักฐาน 5 ชิ้นรอบโรงละครด้วย CiRA CORE:
///     1. หน้ากากละครเวทีแตกหัก (Broken Theater Mask)
///     2. ไม้เท้าทองเหลืองหัก (Victorian Walking Cane)
///     3. เศษผ้าชุดนักแสดงฉีกขาด (Torn Velvet Costume Scrap)
///     4. จดหมายขู่ลับหลังเวที (Backstage Secret Warning)
///     5. "ตั๋วรถไฟที่ฉีกขาด" (Torn Railway Ticket - วัตถุพยานสำคัญที่สุด!)
/// - มินิเกมแผนที่เส้นทางรถไฟลอนดอน: เทียบข้อมูลบนตั๋วกับตารางเดินรถ แล้วจิ้มเลือกสถานีคิงส์ครอส (King's Cross)
/// - ระบบนำทาง Waypoint Navigation System นำทางเข้าสู่ Scene 4 (สถานีรถไฟลอนดอน)
/// </summary>
public class RoyalLyceumTheaterQuestManager : MonoBehaviour
{
    private static RoyalLyceumTheaterQuestManager _instance;
    public static RoyalLyceumTheaterQuestManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<RoyalLyceumTheaterQuestManager>();
            return _instance;
        }
        private set { _instance = value; }
    }

    [Header("=== Quest 3 Configuration ===")]
    public string questTitle = "<color=#F2C94C>◈ ภารกิจหลัก (เควสที่ 3)</color>  เฉียดฉิว: โรงละครรอยัลไลเซียม";
    public int totalEvidenceRequired = 5;

    [Header("=== Typography ===")]
    public Font thaiFont;
    public Font thaiFontBold;

    [Header("=== Targets & Actors ===")]
    public Transform theaterCenterTarget;
    public GameObject survivingVictimGo;
    public GameObject jackTheRipperGo;
    public GameObject tornTicketEvidenceGo;
    public Transform trainStationTarget;

    [Header("=== Cutscene Cameras ===")]
    public CinemachineCamera camTheaterWideBreach;
    public CinemachineCamera camTheaterJackConfrontation;
    public CinemachineCamera camTheaterVictimRescue;
    public CinemachineCamera camTheaterTicketMacro;
    public CinemachineCamera playerFollowCamera;

    [Header("=== Developer Quick Test ===")]
    [Tooltip("เปิดเพื่อทดสอบ Scene 3 ทันทีโดยไม่ต้องเล่น Scene 1 และ 2")]
    public bool testScene3Directly = false;

    // Quest State
    private int _scannedCount = 0;
    /// <summary>จำนวนวัตถุพยานที่สแกนแล้ว (ใช้แสดงในแฟ้มคดีเมนู E)</summary>
    public int ScannedCount => _scannedCount;
    private bool _hasEnteredTheater = false;
    private bool _isQuest3Active = false;
    public bool IsQuest3Active => _isQuest3Active;
    private bool _isRailwayPuzzleSolved = false;
    private HashSet<string> _scannedEvidenceIds = new HashSet<string>();

    [Header("=== แรงกดดันด้านเวลา (Trail Pressure) ===")]
    [Tooltip("เวลาที่ร่องรอยของแจ็คจะเลือนหาย (วินาที) หลังคัตซีนจบ หมดเวลาแล้วเกมไม่จบ แต่คะแนนความแม่นยำจะลดลง")]
    public float trailDurationSeconds = 150f;

    private bool _isDeductionOpened = false;
    private bool _trailTimerRunning = false;
    private bool _trailLost = false;
    private float _trailTimeLeft = 0f;
    private float _progressRefreshTimer = 0f;
    private float _lastScanTime = 0f;
    private float _nextEvidenceHintCheck = 0f;
    private float _scene3StartTime = -1f;
    private CaseDeductionBoardUI.Config _deductionConfig;
    private CaseDeductionBoardUI.Result _deductionResult;

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
            thaiFont = GameAssets.Load<Font>("Assets/Font/Prompt-Medium.ttf")
                    ?? GameAssets.Load<Font>("Assets/Font/Sarabun-Medium.ttf");
            if (thaiFont == null) thaiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (thaiFontBold == null)
        {
            thaiFontBold = GameAssets.Load<Font>("Assets/Font/Prompt-Bold.ttf")
                        ?? GameAssets.Load<Font>("Assets/Font/Sarabun-Bold.ttf");
            if (thaiFontBold == null) thaiFontBold = thaiFont;
        }

        BuildQuest3HUD();
    }

    private void Start()
    {
        // Locate Targets if missing
        if (theaterCenterTarget == null)
        {
            var thGo = GameObject.Find("THEATER") ?? GameObject.Find("Theater_Center_Marker");
            if (thGo != null) theaterCenterTarget = thGo.transform;
        }

        if (trainStationTarget == null)
        {
            var tsGo = GameObject.Find("TRAINSTATION_BENCH") ?? GameObject.Find("TrainStation_Entry_Marker");
            if (tsGo != null) trainStationTarget = tsGo.transform;
        }

        if (tornTicketEvidenceGo == null)
        {
            tornTicketEvidenceGo = GameObject.Find("THEATER_EVIDENCE_TORN_TICKET");
        }

        // ค้นหาตัวละคร Jack และเหยื่อหน้าโรงละคร
        if (jackTheRipperGo == null)
        {
            jackTheRipperGo = GameObject.Find("Theater_Jack_The_Ripper");
        }
        if (survivingVictimGo == null)
        {
            survivingVictimGo = GameObject.Find("Theater_Surviving_Victim");
        }

        // ซ่อนตัวละคร Jack และเหยื่ออย่างเด็ดขาดหากยังเล่นฉากที่ 2 ไม่จบ เพื่อกันสปอยล์
        if (!_isQuest3Active)
        {
            if (jackTheRipperGo != null) jackTheRipperGo.SetActive(false);
            if (survivingVictimGo != null) survivingVictimGo.SetActive(false);
        }

        // Subscribe to CiRA Core Vision Scanner
        if (CiraCoreVisionScanner.Instance != null)
        {
            CiraCoreVisionScanner.Instance.OnEvidenceScanned += HandleTheaterEvidenceScanned;
        }

        // Ensure Quest 3 HUD is strictly hidden until Quest 3 is activated
        if (!_isQuest3Active)
        {
            ShowObjectiveHUD(false);
        }

        if (testScene3Directly)
        {
            StartCoroutine(StartQuest3QuickTestRoutine());
        }
    }

    private void OnDestroy()
    {
        if (CiraCoreVisionScanner.Instance != null)
        {
            CiraCoreVisionScanner.Instance.OnEvidenceScanned -= HandleTheaterEvidenceScanned;
        }
    }

    private IEnumerator StartQuest3QuickTestRoutine()
    {
        yield return new WaitForSeconds(0.6f);
        ActivateQuest3();

        // Teleport player near theater entrance for quick testing
        var player = GameObject.Find("NestedParentArmature_Newman")
                  ?? GameObject.Find("PlayerArmature")
                  ?? GameObject.Find("Player_CH2_Newman");

        if (player != null)
        {
            var cc = player.GetComponentInChildren<CharacterController>();
            if (cc != null) cc.enabled = false;
            (cc != null ? cc.transform : player.transform).position = new Vector3(20.0f, 0.34f, -87.76f);
            (cc != null ? cc.transform : player.transform).rotation = Quaternion.Euler(0f, 90f, 0f);
            if (cc != null) cc.enabled = true;
        }
    }

    /// <summary>รีเซ็ตสถานะของฉาก 3 ให้เล่นซ้ำได้ (ใช้กับทางลัดทดสอบ [P])</summary>
    public void DebugResetForReplay()
    {
        StopAllCoroutines();
        _scannedCount = 0;
        _hasEnteredTheater = false;
        _isRailwayPuzzleSolved = false;
        _isDeductionOpened = false;
        _trailTimerRunning = false;
        _trailLost = false;
        _trailTimeLeft = 0f;
        _scannedEvidenceIds.Clear();
        _deductionResult = null;
        foreach (var ev in FindObjectsByType<EvidenceItem>(FindObjectsInactive.Include))
        {
            if (ev != null && (ev.gameObject.name.ToUpper().StartsWith("THEATER_") || ev.evidenceId == "torn_railway_ticket")) ev.isScanned = false;
        }
    }

    /// <summary>ผูกหลักฐานในโรงละครเข้ากับคลาสของโมเดล CiRA CORE ฉาก 3 (model-scene3/obj.names)</summary>
    private void ApplyScene3CiraClasses()
    {
        foreach (var ev in FindObjectsByType<EvidenceItem>(FindObjectsInactive.Include))
        {
            if (ev == null) continue;
            switch (ev.evidenceId)
            {
                case "theater_mask": ev.ciraClassName = "mask"; break;
                case "walking_cane": ev.ciraClassName = "cane"; break;
                case "actor_costume": ev.ciraClassName = "fabric"; break;
                case "private_message": ev.ciraClassName = "note"; break;
                case "torn_railway_ticket": ev.ciraClassName = "theater_ticket"; break;
            }
        }
    }

    public void ActivateQuest3()
    {
        _isQuest3Active = true;
        ShowObjectiveHUD(true);
        ApplyScene3CiraClasses();
        if (CiraCoreVisionScanner.Instance != null) CiraCoreVisionScanner.Instance.SetActiveSceneModel(3);

        // เมื่อเริ่มเควส 3 (ผ่านฉาก 2 เรียบร้อยแล้ว) เปิดการแสดงตัวละคร Jack และเหยื่อหน้าโรงละคร
        if (jackTheRipperGo != null) jackTheRipperGo.SetActive(true);
        if (survivingVictimGo != null) survivingVictimGo.SetActive(true);

        if (_objectiveTitleText != null)
        {
            _objectiveTitleText.text = questTitle;
        }
        if (_objectiveProgressText != null)
        {
            _objectiveProgressText.text = "• มุ่งหน้าสู่หน้าโรงละครรอยัลไลเซียม (The Royal Lyceum Theatre)";
        }
        if (_hintText != null)
        {
            _hintText.text = "◈ เดินตามหมุดนำทางสีทองไปยังบันไดหน้าทางเข้าโรงละคร";
        }

        if (WaypointNavigationSystem.Instance != null && theaterCenterTarget != null)
        {
            WaypointNavigationSystem.Instance.SetTarget(theaterCenterTarget, "โรงละครรอยัลไลเซียม (Royal Lyceum Theatre)");
            WaypointNavigationSystem.Instance.SetVisible(true);
        }

        Debug.Log("<color=#F2C94C><b>[QUEST 3]</b> เริ่มต้นภารกิจเควสที่ 3: เฉียดฉิว (หน้าโรงละครรอยัลไลเซียม) เรียบร้อยแล้ว!</color>");
    }

    /// <summary>
    /// เรียกเมื่อผู้เล่นก้าวเข้าสู่บริเวณโรงละคร
    /// </summary>
    public void OnPlayerEnteredTheater()
    {
        if (!_isQuest3Active) return; // ล็อคไม่ให้คัตซีน Scene 3 เล่นก่อนที่ผู้เล่นจะจบ Scene 2
        if (_hasEnteredTheater) return;
        _hasEnteredTheater = true;

        StartCoroutine(TheaterCinematicSequenceRoutine());
    }

    private IEnumerator TheaterCinematicSequenceRoutine()
    {
        // 1. Play Cinematic Cutscene
        if (CinematicCutsceneManager.Instance != null)
        {
            Transform victimTr = survivingVictimGo != null ? survivingVictimGo.transform : null;
            Transform jackTr = jackTheRipperGo != null ? jackTheRipperGo.transform : null;
            Transform ticketTr = tornTicketEvidenceGo != null ? tornTicketEvidenceGo.transform : null;

            CinematicCutsceneManager.Instance.PlayScene3TheaterNarrowEscapeCinematic(
                victimTr,
                jackTr,
                ticketTr,
                OnCutsceneFinished
            );
        }
        else
        {
            // Fallback dialogue sequence
            yield return StartCoroutine(FallbackCutsceneRoutine());
            OnCutsceneFinished();
        }
    }

    private IEnumerator FallbackCutsceneRoutine()
    {
        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.ShowLetterbox();
            CinematicLetterbox.Instance.ShowDialogue(
                "สารวัตรนิวแมน (Inspector Newman)",
                "\"หยุดเดี๋ยวนี้ แจ็ค เดอะ ริปเปอร์!\" ที่บันไดหน้าโรงละครรอยัลไลเซียม... แจ็คกำลังง้างมีดเตรียมสังหารเหยื่อสาว!"
            );
            yield return new WaitForSeconds(4.0f);

            CinematicLetterbox.Instance.ShowDialogue(
                "แจ็ค เดอะ ริปเปอร์ (Jack the Ripper)",
                "\"ชิ! นิวแมน... แกตามมาเร็วกว่าที่คิด! แต่มันยังไม่จบหรอก!\" Jack สะบัดผ้าคลุมควันดำแล้วกระโดดหนีหายไปในเงามืดข้างโรงละคร!"
            );
            yield return new WaitForSeconds(4.5f);

            if (jackTheRipperGo != null) jackTheRipperGo.SetActive(false);

            CinematicLetterbox.Instance.ShowDialogue(
                "นักแสดงสาวผู้รอดชีวิต (Lady Rosamund)",
                "\"สารวัตร... ฆาตกรมันทำ 'ตั๋วรถไฟที่ฉีกขาด' ตกไว้ตรงบันไดทางเข้า... รีบตามไปเร็วเข้าค่ะ!\""
            );
            yield return new WaitForSeconds(4.0f);

            CinematicLetterbox.Instance.HideLetterbox();
        }
        yield return null;
    }

    private void OnCutsceneFinished()
    {
        // Make sure Jack is gone
        if (jackTheRipperGo != null) jackTheRipperGo.SetActive(false);

        // เริ่มนับถอยหลัง "ร่องรอยของแจ็คกำลังเลือนหาย"
        _scene3StartTime = Time.time;
        _lastScanTime = Time.time;
        _trailTimeLeft = trailDurationSeconds;
        _trailTimerRunning = true;
        _trailLost = false;

        // Update Quest Objectives
        UpdateTheaterObjectiveDisplay();

        if (_hintText != null)
        {
            _hintText.text = "◈ กด [V] หรือคลิกขวาเปิดโหมดสแกน ตรวจทุกอย่างที่แจ็คทิ้งไว้ก่อนที่ร่องรอยจะเลือนหาย";
        }

        // Point Waypoint to Torn Ticket or Investigation zone
        if (WaypointNavigationSystem.Instance != null && tornTicketEvidenceGo != null)
        {
            WaypointNavigationSystem.Instance.SetTarget(tornTicketEvidenceGo.transform, "ตั๋วรถไฟที่ฉีกขาด (Torn Ticket)");
            WaypointNavigationSystem.Instance.SetVisible(true);
        }
    }

    // ==========================================
    // แรงกดดันด้านเวลา + หมุดช่วยหาหลักฐาน
    // ==========================================
    private void Update()
    {
        if (!_isQuest3Active || !_trailTimerRunning) return;

        _trailTimeLeft -= Time.deltaTime;
        if (_trailTimeLeft <= 0f)
        {
            _trailTimeLeft = 0f;
            _trailTimerRunning = false;
            _trailLost = true;
            DetectiveThoughtToast.Show("ร่องรอยเลือนหายไปหมดแล้ว... ต้องอาศัยของที่แจ็คทิ้งไว้ให้ได้ เหลือเวลาไม่มากแล้ว");
            UpdateTheaterObjectiveDisplay();
        }

        _progressRefreshTimer -= Time.deltaTime;
        if (_progressRefreshTimer <= 0f)
        {
            _progressRefreshTimer = 0.25f;
            UpdateTheaterObjectiveDisplay();
        }

        // ผ่านไป 60 วินาทีแล้วยังหาไม่ครบ ให้หมุดนำทางชี้ไปที่หลักฐานชิ้นที่ใกล้ที่สุด
        bool needHint = _trailLost || (trailDurationSeconds - _trailTimeLeft) > 60f || Time.time - _lastScanTime > 50f;
        if (needHint && Time.time >= _nextEvidenceHintCheck)
        {
            _nextEvidenceHintCheck = Time.time + 2f;
            PointWaypointToNearestEvidence();
        }
    }

    private void PointWaypointToNearestEvidence()
    {
        if (WaypointNavigationSystem.Instance == null) return;

        Vector3 origin = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        EvidenceItem best = null;
        float bestDist = float.MaxValue;
        foreach (var ev in FindObjectsByType<EvidenceItem>(FindObjectsInactive.Exclude))
        {
            if (ev == null || ev.isScanned) continue;
            bool isTheater = ev.gameObject.name.ToUpper().StartsWith("THEATER_") || ev.evidenceId == "torn_railway_ticket";
            if (!isTheater || _scannedEvidenceIds.Contains(ev.evidenceId)) continue;

            float d = Vector3.Distance(origin, ev.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = ev;
            }
        }

        if (best != null && WaypointNavigationSystem.Instance.TargetTransform != best.transform)
        {
            WaypointNavigationSystem.Instance.SetTarget(best.transform, "เบาะแสในโรงละคร");
            WaypointNavigationSystem.Instance.SetVisible(true);
        }
    }

    /// <summary>ความคิดของนิวแมนเมื่อสแกนพบหลักฐานแต่ละชิ้นของบทที่ 3</summary>
    private static string GetTheaterEvidenceThought(string upperName, string evidenceId)
    {
        string id = (evidenceId ?? "").ToLower();
        if (upperName.Contains("MASK"))
            return "หน้ากากละครเวทีแตกครึ่ง รอยแตกคมเรียบ ถูกกระแทกกับขอบเหล็ก ไม่ได้แตกเพราะตกพื้นธรรมดา";
        if (upperName.Contains("CANE"))
            return "ไม้เท้าทองเหลืองหักที่ด้าม หัวด้ามสลักอักษร M คนที่ถือมันไม่ใช่แจ็ค และไม่ใช่นักแสดงที่นี่";
        if (upperName.Contains("COSTUME"))
            return "เศษกำมะหยี่แดงจากชุดของโรซามันด์ ฉีกตอนเธอดิ้นรน เส้นด้ายชี้ไปทางประตูหลังเวที";
        if (upperName.Contains("MESSAGE") || upperName.Contains("LETTER"))
            return "ลายมือเรียบร้อยแบบคนเขียนหนังสือพิมพ์ \"เมื่อสารวัตรมาถึง ให้ออกทางประตูหลัง\" เขาเขียนฉากหนีไว้ล่วงหน้า";
        if (upperName.Contains("TICKET") || upperName.Contains("INVITATION") || id.Contains("ticket"))
            return "ตั๋วฉีกครึ่ง เหลือเวลา 23:45 กับคำว่า Northern ปลายทางที่หายไปต้องไปหาในแผนที่";
        return null;
    }

    private void HandleTheaterEvidenceScanned(EvidenceItem item)
    {
        if (!_isQuest3Active) return; // ต้องอยู่ในเควส 3 เท่านั้น ป้องกันไม่ให้แอบทำงานตอนเล่นเควส 1 หรือ 2
        if (item == null) return;

        // ต้องเป็นหลักฐานของโรงละครใน Scene 3 เท่านั้น
        string goName = item.gameObject.name.ToUpper();
        bool isTheaterEvidence = goName.StartsWith("THEATER_") || item.evidenceId == "torn_railway_ticket";
        if (!isTheaterEvidence) return;

        if (!_scannedEvidenceIds.Add(item.evidenceId)) return; // Already scanned

        _scannedCount++;
        item.isScanned = true;

        Debug.Log($"<color=#5CE1E6><b>[CiRA CORE SCAN]</b> สแกนวัตถุพยานโรงละครสำเร็จ: {item.thaiTitle} ({_scannedCount}/{totalEvidenceRequired})</color>");

        _lastScanTime = Time.time;

        // ถ้าหมุดนำทางกำลังชี้ที่หลักฐานชิ้นนี้ เคลียร์ออก (หลักฐานชิ้นถัดไปจะถูกชี้เมื่อหาไม่เจอนานพอ)
        if (WaypointNavigationSystem.Instance != null && WaypointNavigationSystem.Instance.TargetTransform == item.transform)
        {
            WaypointNavigationSystem.Instance.ClearTarget();
        }

        // นิวแมนพูดสั้น ๆ ว่าหลักฐานชิ้นนี้บอกอะไร (ไม่หยุดเกม)
        DetectiveThoughtToast.Show(GetTheaterEvidenceThought(goName, item.evidenceId));

        UpdateTheaterObjectiveDisplay();

        // สแกนครบ 5 ชิ้น -> ตอบข้อสันนิษฐาน -> เปิดแผนที่รถไฟ -> สรุปบท
        if (_scannedCount >= totalEvidenceRequired && !_isDeductionOpened && !_isRailwayPuzzleSolved)
        {
            _isDeductionOpened = true;
            _trailTimerRunning = false;
            StartCoroutine(OpenTheaterDeductionRoutine());
        }
    }

    private IEnumerator OpenTheaterDeductionRoutine()
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

        _deductionConfig = BuildTheaterDeductionConfig();
        CaseDeductionBoardUI.Open(_deductionConfig, OnTheaterDeductionSolved);
    }

    private void OnTheaterDeductionSolved(CaseDeductionBoardUI.Result result)
    {
        _deductionResult = result;
        StartCoroutine(TriggerRailwayMapPuzzleRoutine());
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

    /// <summary>
    /// ข้อสันนิษฐานบทที่ 3 (3 ขั้น): ทางหนีถูกวางแผน -> ตั๋วบอกเวลาและทิศทาง -> ผู้วางแผนอยู่ในโรงละคร
    /// </summary>
    private CaseDeductionBoardUI.Config BuildTheaterDeductionConfig()
    {
        var config = new CaseDeductionBoardUI.Config
        {
            letterhead = "METROPOLITAN POLICE  ·  CASE FILE 1888/JR  ·  ROYAL LYCEUM THEATRE",
            openQuestion = "ทำไมเขายอมให้เราช่วยเหยื่อได้ทัน และเขาต้องการให้เราตามแจ็คไปที่ไหน?"
        };

        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("mask", "หน้ากากละครเวที",
            "แตกครึ่งเป็นรอยคม ถูกกระแทกกับขอบเหล็ก"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("cane", "ไม้เท้าทองเหลืองหัก",
            "หัวด้ามสลักอักษร M ไม่ใช่ของแจ็คหรือนักแสดง"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("costume", "เศษผ้ากำมะหยี่",
            "ฉีกจากชุดของโรซามันด์ ชี้ไปทางประตูหลังเวที"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("message", "จดหมายหลังเวที",
            "ลายมือเรียบร้อย สั่งให้ออกทางประตูหลังเมื่อสารวัตรมาถึง"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("ticket", "ตั๋วรถไฟฉีกขาด",
            "เวลา 23:45 ขบวนด่วนทิศเหนือ ปลายทางหายไปครึ่งหนึ่ง"));

        // ข้อสันนิษฐานที่ 1: ทางหนีถูกวางแผนไว้
        var s1 = new CaseDeductionBoardUI.Step
        {
            question = "แจ็คไหวตัวทันและหนีออกทางหลังเวทีภายในไม่กี่วินาที ของชิ้นใดบอกว่าการหนีครั้งนี้ถูกวางแผนไว้ล่วงหน้า ไม่ใช่การตกใจหนีเฉพาะหน้า?",
            conclusion = "จดหมายหลังเวทีกำหนดทางหนีไว้ล่วงหน้า แจ็คไม่ได้ตกใจหนี แต่ทำตามแผนที่มีคนวางไว้ให้"
        };
        s1.options.Add(new CaseDeductionBoardUI.Option("หน้ากากละครเวทีที่แตกครึ่ง", false,
            "หน้ากากแตกเพราะถูกกระแทกระหว่างวิ่ง เป็นผลของการหนี ไม่ได้บอกว่าใครวางแผน"));
        s1.options.Add(new CaseDeductionBoardUI.Option("เศษผ้ากำมะหยี่จากชุดนักแสดง", false,
            "เศษผ้าฉีกเพราะเหยื่อดิ้นรน บอกได้แค่ทิศที่ทั้งสองเคลื่อนไป ไม่ได้บอกว่ามีแผน"));
        s1.options.Add(new CaseDeductionBoardUI.Option("จดหมายหลังเวทีที่สั่งให้ออกทางประตูหลังเมื่อสารวัตรมาถึง", true,
            "คนเขียนรู้ล่วงหน้าว่าเราจะมา และกำหนดทางหนีไว้แล้ว แจ็คจึงหนีได้ในไม่กี่วินาทีโดยไม่ลังเล"));
        s1.options.Add(new CaseDeductionBoardUI.Option("ไม้เท้าทองเหลืองที่หัก", false,
            "ไม้เท้าหักเพราะการปะทะ อาจเป็นของคนอื่นที่มาที่นี่ แต่ยังไม่ใช่หลักฐานเรื่องเส้นทางหนี"));
        config.steps.Add(s1);

        // ข้อสันนิษฐานที่ 2: ตั๋วบอกอะไร
        var s2 = new CaseDeductionBoardUI.Step
        {
            question = "ตั๋วรถไฟฉีกขาดเหลือเวลา 23:45 ขณะนี้ใกล้เที่ยงคืนแล้ว แจ็คต้องไปให้ทันขบวนนี้ ข้อใดสรุปเกี่ยวกับปลายทางได้ถูกต้องที่สุด?",
            relatedEvidenceIds = new[] { "ticket" },
            conclusion = "ตั๋วฉีกครึ่งเหลือเบาะแสสามอย่าง ออก 23:45 ขบวนด่วน และมุ่งหน้าเหนือ แจ็คมีเวลาไม่ถึงชั่วโมง ปลายทางต้องตรงทุกข้อ"
        };
        s2.options.Add(new CaseDeductionBoardUI.Option("ปลายทางต้องเป็นสถานีทางใต้ข้ามแม่น้ำ เพราะแจ็คหนีไปทางหลังโรงละครฝั่งใต้", false,
            "ไม่มีหลักฐานว่าเขาหนีไปทางใต้ และคำว่า Northern บนตั๋วชี้ไปทางตรงข้ามกัน"));
        s2.options.Add(new CaseDeductionBoardUI.Option("ต้องเป็นสถานีที่มีขบวนด่วนสายเหนือออกเวลา 23:45 และไปถึงได้ภายในไม่ถึงหนึ่งชั่วโมง", true,
            "เบาะแสบนตั๋วครบสามอย่าง คือเวลา ทิศเหนือ และขบวนด่วน ต้องหาสถานีที่ตรงทุกข้อ ไม่ใช่แค่ข้อใดข้อหนึ่ง"));
        s2.options.Add(new CaseDeductionBoardUI.Option("ตั๋วถูกฉีกครึ่งจึงไม่บอกอะไร ต้องรอให้แจ็คก่อเหตุอีกครั้ง", false,
            "ตั๋วที่ถูกฉีกครึ่งยังเหลือข้อมูลพอจะคัดสถานีออกได้ และการรอคือการเสียเวลาทั้งหมดที่เรามี"));
        s2.options.Add(new CaseDeductionBoardUI.Option("เวลา 23:45 เป็นรอบดึกของการแสดงในโรงละคร ไม่เกี่ยวกับรถไฟ", false,
            "ตั๋วใบนี้พิมพ์ด้วยหมึกของบริษัทรถไฟและมีหมายเลขขบวน ไม่ใช่ตั๋วเข้าชมการแสดง"));
        config.steps.Add(s2);

        // ข้อสันนิษฐานที่ 3: ผู้วางแผนอยู่ในโรงละคร
        var s3 = new CaseDeductionBoardUI.Step
        {
            question = "ไม้เท้าทองเหลืองหักที่หัวด้ามสลักอักษร M ไม่ใช่ของแจ็คและไม่ใช่ของนักแสดงที่นี่ เมื่อรวมกับจดหมายหลังเวที ข้อใดน่าจะเป็นจริงที่สุด?",
            relatedEvidenceIds = new[] { "cane", "message" },
            conclusion = "ไม้เท้าสลักอักษร M ไม่ใช่ของแจ็คหรือนักแสดง ผู้ที่วางแผนอยู่ในโรงละครคืนนี้ เขามายืนดูแผนของเขาทำงานด้วยตัวเอง"
        };
        s3.options.Add(new CaseDeductionBoardUI.Option("นักแสดงชายคนหนึ่งในโรงละครมีส่วนรู้เห็น", false,
            "ถ้าเป็นนักแสดงในโรงละคร ไม้เท้าจะมีตราหรือชื่อโรงละคร ไม่ใช่อักษร M ตัวเดียวที่ถูกขัดมันอย่างดี"));
        s3.options.Add(new CaseDeductionBoardUI.Option("แจ็คขโมยไม้เท้าจากสุภาพบุรุษมาใช้เป็นอาวุธ", false,
            "ไม้เท้าแบบนี้เป็นของสุภาพบุรุษมีฐานะ ไม่ใช่ของที่แจ็คจะพกพา และด้ามไม่มีร่องรอยการแย่งชิงเลย"));
        s3.options.Add(new CaseDeductionBoardUI.Option("ผู้วางแผนทางหนีมาอยู่ที่โรงละครด้วยตัวเอง เขามายืนดูแผนของเขาทำงาน", true,
            "ไม้เท้าหักตรงที่คนยืนดูอยู่ใกล้ ๆ ทำตกเมื่อเหตุการณ์พลิก เมื่อรวมกับจดหมายที่เขียนอย่างประณีต แสดงว่าคนเบื้องหลังอยู่ในโรงละครคืนนี้"));
        s3.options.Add(new CaseDeductionBoardUI.Option("ไม้เท้าเป็นของตกแต่งฉากเก่าที่ไม่เกี่ยวกับคดี", false,
            "ในโรงละครที่ถูกรื้อค้นเมื่อไม่นานมานี้ ไม้เท้าชิ้นนี้ยังมีกลิ่นน้ำหอมและร่องรอยสดใหม่ ไม่ใช่ของเก่า"));
        config.steps.Add(s3);

        return config;
    }

    private void UpdateTheaterObjectiveDisplay()
    {
        if (_objectiveProgressText == null) return;

        if (_isRailwayPuzzleSolved)
        {
            _objectiveProgressText.text = "• ปลายทางคือคิงส์ครอส! <color=#66FF99><b>มุ่งหน้าสู่สถานีรถไฟลอนดอน</b></color>";
        }
        else if (_scannedCount >= totalEvidenceRequired)
        {
            _objectiveProgressText.text = "• พบเบาะแสครบ 5 ชิ้นแล้ว! <color=#66FF99><b>วิเคราะห์ข้อสันนิษฐานและแผนที่รถไฟ</b></color>";
        }
        else
        {
            string timer = "";
            if (_trailTimerRunning)
            {
                int sec = Mathf.CeilToInt(_trailTimeLeft);
                string color = _trailTimeLeft < 30f ? "#FF6B5B" : "#FFD24C";
                timer = $"   <color={color}><b>ร่องรอยเลือนหายใน {sec / 60:00}:{sec % 60:00}</b></color>";
            }
            else if (_trailLost)
            {
                timer = "   <color=#9A8F80>ร่องรอยเลือนหายไปแล้ว</color>";
            }
            _objectiveProgressText.text = $"• สแกนเบาะแสที่แจ็คทิ้งไว้: <color=#5CE1E6><b>{_scannedCount} / {totalEvidenceRequired}</b></color>{timer}";
        }
    }

    private IEnumerator TriggerRailwayMapPuzzleRoutine()
    {
        // มินิเกมท้ายเควสไม่ต้องทำคอปบนล่าง และซ่อน HUD เควสทั้งหมดไม่ให้ซ้อนทับ
        ShowObjectiveHUD(false);
        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.ForceDisableLetterbox();
        }

        // บล็อกและปิดระบบสแกน CiRA CORE
        CiraCoreVisionScanner.IsScannerBlocked = true;
        if (CiraCoreVisionScanner.Instance != null)
        {
            CiraCoreVisionScanner.Instance.ForceCloseScanMode();
        }

        // ซ่อนหมุดนำทาง 3D
        if (WaypointNavigationSystem.Instance != null)
        {
            WaypointNavigationSystem.Instance.SetVisible(false);
        }

        yield return new WaitForSeconds(0.2f);

        // เปิดมินิเกม London Railway Map
        if (LondonRailwayMapMinigame.Instance != null)
        {
            LondonRailwayMapMinigame.Instance.OpenMinigame(OnRailwayMapPuzzleSolved);
        }
    }

    /// <summary>
    /// เรียกเมื่อผู้เล่นเลือกสถานีคิงส์ครอส (King's Cross Station) บนแผนที่ถูกต้อง
    /// </summary>
    private void OnRailwayMapPuzzleSolved()
    {
        _isRailwayPuzzleSolved = true;
        UpdateTheaterObjectiveDisplay();

        StartCoroutine(RailwayPuzzleSolvedConclusionRoutine());
    }

    private IEnumerator RailwayPuzzleSolvedConclusionRoutine()
    {
        // ล็อกผู้เล่นไว้ตลอดช่วงบทสนทนาและหน้าสรุป (มินิเกมปลดล็อกให้ตอนปิด)
        CiraCoreVisionScanner.IsScannerBlocked = true;
        if (CinematicCutsceneManager.Instance != null) CinematicCutsceneManager.Instance.SetPlayerControlsLocked(true);

        if (CinematicLetterbox.Instance != null)
        {
            var lines = new List<CinematicLetterbox.DialogueEntry>
            {
                new CinematicLetterbox.DialogueEntry("สารวัตรนิวแมน",
                    "คิงส์ครอส ขบวนด่วนสายเหนือ 23:45 แจ็คกำลังจะออกจากลอนดอน"),
                new CinematicLetterbox.DialogueEntry("สารวัตรนิวแมน",
                    "แต่ทุกอย่างถูกเรียงไว้ให้เรามาถึงตรงนี้พอดี จดหมาย ตั๋ว ไม้เท้า... มอริอาร์ตี้อยากให้เราไปที่นั่น"),
                new CinematicLetterbox.DialogueEntry("สารวัตรนิวแมน",
                    "และเราก็ไม่มีทางเลือกอื่น ไปสถานี ก่อนที่ขบวนนั้นจะออก")
            };
            CinematicLetterbox.Instance.PlayDialogueSequence(lines, ShowChapter3Summary);
        }
        else
        {
            ShowChapter3Summary();
        }

        yield return null;
    }

    /// <summary>หน้าสรุปบทที่ 3 หลังไขปริศนาแผนที่รถไฟ</summary>
    private void ShowChapter3Summary()
    {
        if (CinematicLetterbox.Instance != null) CinematicLetterbox.Instance.HideLetterbox();
        if (CinematicCutsceneManager.Instance != null) CinematicCutsceneManager.Instance.SetPlayerControlsLocked(true);
        SetCursorLockedState(false);

        // ความแม่นยำ = ตามข้อสันนิษฐาน ลบ 1 ถ้าปล่อยให้ร่องรอยเลือนหาย และลบ 1 ถ้าตรวจแผนที่ไม่ทันขบวน 23:45 (ต่ำสุด 1)
        int accuracy = _deductionResult != null ? _deductionResult.accuracy : 3;
        if (_trailLost) accuracy--;
        if (LondonRailwayMapMinigame.Instance != null && LondonRailwayMapMinigame.Instance.MissedTrain) accuracy--;
        accuracy = Mathf.Clamp(accuracy, 1, 3);

        var data = new ChapterSummaryUI.Data
        {
            chapterNumber = 3,
            chapterTitle = "เฉียดฉิว",
            location = "โรงละครรอยัลไลเซียม  ·  ลอนดอน  ·  ค.ศ. 1888",
            openQuestion = _deductionConfig != null ? _deductionConfig.openQuestion : null,
            nextLead = "ขบวนด่วน 23:45 จากคิงส์ครอสกำลังจะออก และสถานีรถไฟใต้ดินคือจุดที่ทุกเส้นทางมาบรรจบกัน",
            elapsedSeconds = _scene3StartTime > 0f ? Time.time - _scene3StartTime : -1f,
            accuracy = accuracy
        };

        if (_deductionConfig != null)
        {
            foreach (var step in _deductionConfig.steps) data.chain.Add(step.conclusion);
        }
        data.chain.Add("ตั๋วฉีกครึ่งเทียบกับตารางเดินรถและแผนที่ ตรงทั้งสี่ข้อที่คิงส์ครอสเพียงสถานีเดียว");

        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("จดหมายหลังเวที", "ลายมือเรียบร้อย กำหนดทางหนีของแจ็คไว้ล่วงหน้า", true));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("ตั๋วรถไฟฉีกขาด", "ขบวนด่วน 23:45 ทิศเหนือ ชี้ไปคิงส์ครอส", true));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("ไม้เท้าทองเหลืองหัก", "สลักอักษร M ผู้วางแผนอยู่ในโรงละคร"));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("เศษผ้ากำมะหยี่", "ชุดของโรซามันด์ ชี้ทางประตูหลังเวที"));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("หน้ากากละครเวที", "แตกครึ่งจากการกระแทกระหว่างหนี"));

        ChapterSummaryUI.Show(data, OnChapter3SummaryContinue);
    }

    private void OnChapter3SummaryContinue()
    {
        CiraCoreVisionScanner.IsScannerBlocked = false;
        SetCursorLockedState(true);
        if (CinematicCutsceneManager.Instance != null) CinematicCutsceneManager.Instance.SetPlayerControlsLocked(false);

        // อัปเดตภารกิจสู่ Scene 4
        if (_objectiveTitleText != null)
        {
            _objectiveTitleText.text = "<color=#F2C94C>◈ ภารกิจหลัก (เควสที่ 4)</color>  การไล่ล่าแห่งความตาย: สถานีรถไฟลอนดอน (London Train Station)";
        }
        if (_objectiveProgressText != null)
        {
            _objectiveProgressText.text = "• มุ่งหน้าสู่สถานีรถไฟลอนดอน เพื่อปิดล้อมชานชาลารถไฟสายเหนือ";
        }
        if (_hintText != null)
        {
            _hintText.text = "◈ ปริศนาโรงละครคลี่คลาย! เดินตามหมุดนำทางไปยังสถานีรถไฟ";
        }

        // ซ่อน HUD เควส 3 และเปิดเควสที่ 4: การไล่ล่าใต้ดิน
        ShowObjectiveHUD(false);

        if (UndergroundStationQuestManager.Instance != null)
        {
            UndergroundStationQuestManager.Instance.ActivateQuest4();
        }

        // อัปเดต Waypoint Navigation ชี้เป้าไปยังทางลงสถานีรถไฟใต้ดินทันที!
        if (WaypointNavigationSystem.Instance != null)
        {
            var stairwell = GameObject.Find("Scene4_StationEntranceTrigger")
                         ?? GameObject.Find("TRAIN_STATION_STATION")
                         ?? GameObject.Find("TRAIN_STATION_STARIRWELL");
            if (stairwell != null)
            {
                trainStationTarget = stairwell.transform;
            }
            if (trainStationTarget != null)
            {
                WaypointNavigationSystem.Instance.SetTarget(trainStationTarget, "สถานีรถไฟเมโทรโพลิแทน (Metropolitan Railway)");
                WaypointNavigationSystem.Instance.SetVisible(true);
            }
        }

        Debug.Log("<color=green><b>[SUCCESS]</b> ปริศนาเควส Scene 3 เสร็จสมบูรณ์ 100%! นำทางเข้าสู่เควส Scene 4 (สถานีรถไฟลอนดอน)</color>");
    }

    public void ShowObjectiveHUD(bool show)
    {
        if (_objectivePanel != null)
        {
            _objectivePanel.SetActive(show);
        }

        if (show)
        {
            // ซ่อน HUD ของ Scene 1 และ Scene 2 ป้องกันการซ้อนทับกัน
            if (StMaryChurchQuestManager.Instance != null)
            {
                StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
            }
            if (LondonAlleyQuestManager.Instance != null)
            {
                LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
            }
            var q1Hud = GameObject.Find("ObjectivePanel");
            if (q1Hud != null) q1Hud.SetActive(false);
            var q2Hud = GameObject.Find("ObjectivePanel_Scene2");
            if (q2Hud != null) q2Hud.SetActive(false);
        }
    }

    public void BuildQuest3HUD()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;
            }
        }

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        // Objective HUD (แบบ Genshin Impact: ไร้กรอบ ไร้ขอบกล่อง ข้อความลอยอิสระพร้อม Drop Shadow)
        if (_objectivePanel == null)
        {
            _objectivePanel = new GameObject("Quest3_LyceumTheater_HUD", typeof(RectTransform), typeof(Image));
            _objectivePanel.transform.SetParent(transform, false);

            RectTransform rt = _objectivePanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(40f, -40f);
            rt.sizeDelta = new Vector2(660f, 120f);

            Image oImg = _objectivePanel.GetComponent<Image>();
            oImg.color = Color.clear; // ไร้กล่องพื้นหลัง แบบ Genshin Impact

            // Title
            GameObject titleObj = new GameObject("QuestTitle", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(_objectivePanel.transform, false);
            RectTransform trt = titleObj.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(0f, 0f);
            trt.sizeDelta = new Vector2(0f, 32f);

            _objectiveTitleText = titleObj.GetComponent<Text>();
            _objectiveTitleText.font = thaiFontBold != null ? thaiFontBold : VisionOSUITheme.BoldFont;
            _objectiveTitleText.fontSize = 20;
            _objectiveTitleText.color = VisionOSUITheme.TextPrimary;
            _objectiveTitleText.text = "<color=#F2C94C>◆ ภารกิจหลัก (เควสที่ 3)</color>  การประจันหน้า: โรงละครรอยัลไลเซียม";
            var tShadow = titleObj.AddComponent<Shadow>();
            tShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            tShadow.effectDistance = new Vector2(1.5f, -1.5f);

            // Progress Text
            GameObject progObj = new GameObject("QuestProgress", typeof(RectTransform), typeof(Text));
            progObj.transform.SetParent(_objectivePanel.transform, false);
            RectTransform prt = progObj.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 1f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(0f, -34f);
            prt.sizeDelta = new Vector2(0f, 28f);

            _objectiveProgressText = progObj.GetComponent<Text>();
            _objectiveProgressText.font = thaiFont != null ? thaiFont : VisionOSUITheme.PrimaryFont;
            _objectiveProgressText.fontSize = 17;
            _objectiveProgressText.color = VisionOSUITheme.TextPrimary;
            _objectiveProgressText.text = "• มุ่งหน้าสู่หน้าโรงละครรอยัลไลเซียม (The Royal Lyceum Theatre)";
            var pShadow = progObj.AddComponent<Shadow>();
            pShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            pShadow.effectDistance = new Vector2(1.5f, -1.5f);

            // Hint Text
            GameObject hintObj = new GameObject("QuestHint", typeof(RectTransform), typeof(Text));
            hintObj.transform.SetParent(_objectivePanel.transform, false);
            RectTransform hrt = hintObj.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -64f);
            hrt.sizeDelta = new Vector2(0f, 26f);

            _hintText = hintObj.GetComponent<Text>();
            _hintText.font = thaiFont != null ? thaiFont : VisionOSUITheme.PrimaryFont;
            _hintText.fontSize = 14;
            _hintText.color = new Color(0.95f, 0.82f, 0.42f, 1f);
            _hintText.text = "◆ เดินตามหมุดนำทางไปยังบันไดหน้าทางเข้าโรงละคร";
            var hShadow = hintObj.AddComponent<Shadow>();
            hShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            hShadow.effectDistance = new Vector2(1.5f, -1.5f);

            // Genshin Hotkey Prompt
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
    }
}
