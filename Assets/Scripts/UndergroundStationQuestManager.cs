using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ตัวจัดการระบบเควส Scene 4: การไล่ล่าใต้ดิน (สถานีรถไฟใต้ดิน / Underground Railway Station)
/// - บรรยากาศ: อึดอัด ควันไอน้ำรถไฟพวยพุ่ง เสียงฝีเท้าก้องกังวาน ไฟกะพริบติดๆ ดับๆ
/// - ระบบ Dynamic Scanning & Evidence Mapping ภายใต้ความกดดันที่ Moriarty กำลังจ้องมองอยู่
/// - หลักฐาน 5 ชิ้น:
///     1. รอยเลือดบนพื้น (สแกนตามรอยเลือดที่ค่อยๆ จางหายไปตามทางแยก มุ่งสู่ชานชาลาที่ 2)
///     2. ปลอกกระสุน (ปลอกกระสุน Webley .455 ตกใกล้ขบวนรถไฟตู้ท้าย)
///     3. นาฬิกาที่หยุดเดิน (เวลา 11:45 PM ตรงกับเวลาออกของขบวนรถด่วนเที่ยงคืน)
///     4. ตั๋วรถไฟจาก Scene 3 (ระบุตู้โดยสารหมายเลข 4)
///     5. กระดาษโน้ตจากมอริอาตี้ (คำใบ้ลวงที่ต้องตัดทิ้ง!)
/// - Evidence Mapping Climax: โหมด Connecting the Dots เชื่อมโยงหลักฐาน ตัดคำลวง และเผยความจริง!
/// </summary>
public class UndergroundStationQuestManager : MonoBehaviour
{
    private static UndergroundStationQuestManager _instance;
    public static UndergroundStationQuestManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<UndergroundStationQuestManager>();
            return _instance;
        }
        private set { _instance = value; }
    }

    [Header("=== Quest 4 Configuration ===")]
    public string questTitle = "<color=#F2C94C>◆ ภารกิจหลัก (เควสที่ 4)</color>  การไล่ล่าใต้ดิน: สถานีรถไฟเมโทรโพลิแทน (Metropolitan Railway Station)";
    public int totalEvidenceRequired = 5;

    [Header("=== Typography ===")]
    public Font thaiFont;
    public Font thaiFontBold;

    [Header("=== Targets & Evidence Objects ===")]
    public Transform stationStairwellTarget;
    public GameObject bloodTrailGo;
    public GameObject bulletCasingGo;
    public GameObject frozenWatchGo;
    public GameObject railwayTicketGo;
    public GameObject moriartyNoteGo;
    [Tooltip("ป้ายตารางเดินรถบนชานชาลา (ถ้าไม่มีในฉาก ระบบสร้างให้ตอนเริ่มเล่น)")]
    public GameObject timetableBoardGo;

    [Header("=== Developer Quick Test ===")]
    [Tooltip("เปิดเพื่อทดสอบ Scene 4 ทันทีโดยไม่ต้องเล่น Scene 1-3")]
    public bool testScene4Directly = false;

    // State
    private int _scannedCount = 0;
    /// <summary>จำนวนวัตถุพยานที่สแกนแล้ว (ใช้แสดงในแฟ้มคดีเมนู E)</summary>
    public int ScannedCount => _scannedCount;
    /// <summary>จำนวนจุดสแกนทั้งหมดของฉาก 4 (หลักฐาน 5 ชิ้น + ป้ายตารางเดินรถ)</summary>
    public int TotalEvidence => totalEvidenceRequired;
    private bool _hasEnteredStation = false;
    private bool _isQuest4Active = false;
    public bool IsQuest4Active => _isQuest4Active;
    private bool _isDeductionCompleted = false;
    private HashSet<string> _scannedEvidenceIds = new HashSet<string>();
    private float _scene4StartTime = -1f;
    private bool _isDeductionOpened;
    private bool _isMinigameSolved;
    private CaseDeductionBoardUI.Config _deductionConfig;
    private CaseDeductionBoardUI.Result _deductionResult;
    private Coroutine _whisperCoroutine;
    /// <summary>จบเควสที่ 4 แล้ว (ผ่านหน้าสรุปบท)</summary>
    public bool IsQuest4Finished { get; private set; }

    // UI References
    private GameObject _objectivePanel;
    private Text _objectiveTitleText;
    private Text _objectiveProgressText;
    private Text _hintText;

    private List<Light> _stationLamps = new List<Light>();

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

        BuildQuest4HUD();
    }

    private void Start()
    {
        LocateStationEvidenceObjects();
        ConfigureEvidenceItemComponents();
        FindAndSetupFlickeringLamps();

        // Subscribe to CiRA Core Scanner
        if (CiraCoreVisionScanner.Instance != null)
        {
            CiraCoreVisionScanner.Instance.OnEvidenceScanned += HandleStationEvidenceScanned;
        }

        if (!_isQuest4Active)
        {
            ShowObjectiveHUD(false);
        }

        if (testScene4Directly)
        {
            StartCoroutine(StartQuest4QuickTestRoutine());
        }
    }

    private void OnDestroy()
    {
        if (CiraCoreVisionScanner.Instance != null)
        {
            CiraCoreVisionScanner.Instance.OnEvidenceScanned -= HandleStationEvidenceScanned;
        }
    }

    private IEnumerator StartQuest4QuickTestRoutine()
    {
        yield return new WaitForSeconds(0.6f);
        ActivateQuest4();

        // วาร์ปผู้เล่นลงมาที่ชานชาลาสถานีรถไฟใต้ดิน (Underground Station Platform)
        var player = GameObject.Find("NestedParentArmature_Newman")
                  ?? GameObject.Find("PlayerArmature")
                  ?? GameObject.Find("Player_CH2_Newman");

        if (player != null)
        {
            var cc = player.GetComponentInChildren<CharacterController>();
            if (cc != null) cc.enabled = false;
            (cc != null ? cc.transform : player.transform).position = new Vector3(-41.50f, -9.66f, 43.00f);
            (cc != null ? cc.transform : player.transform).rotation = Quaternion.Euler(0f, 45f, 0f);
            if (cc != null) cc.enabled = true;
        }

        OnPlayerEnteredStation();
    }

    public void LocateStationEvidenceObjects()
    {
        if (stationStairwellTarget == null)
        {
            var entranceGo = GameObject.Find("Scene4_StationEntranceTrigger")
                          ?? GameObject.Find("TRAIN_STATION_STATION");
            if (entranceGo != null) stationStairwellTarget = entranceGo.transform;
        }

        if (bloodTrailGo == null) bloodTrailGo = GameObject.Find("TRAIN_EVIDENCE_BLOODSPLATTER");
        if (bulletCasingGo == null) bulletCasingGo = GameObject.Find("TRAIN_EVIDENCE_SPENTCARTRIDGECASE");
        if (frozenWatchGo == null) frozenWatchGo = GameObject.Find("TRAIN_EVIDENCE_POCKETWATCH");
        if (railwayTicketGo == null) railwayTicketGo = GameObject.Find("TRAIN_EVIDENCE_RAILWAYTICKET");
        if (moriartyNoteGo == null) moriartyNoteGo = GameObject.Find("TRAIN_EVIDENCE_STATIONNOTE");

        // ป้องกันกำแพงอากาศ (Invisible Wall Bug): ปรับแก้ Collider ของบันไดทางลงให้เป็น Trigger ขนาดพอเหมาะ และเพิ่ม MeshCollider สำหรับเดิน
        if (stationStairwellTarget != null)
        {
            var swBox = stationStairwellTarget.GetComponent<BoxCollider>();
            if (swBox != null)
            {
                swBox.isTrigger = true;
                swBox.size = new Vector3(0.04f, 0.04f, 0.04f); // 4x4x4m ใน World Space
                swBox.center = Vector3.zero;
            }
            if (stationStairwellTarget.GetComponent<MeshCollider>() == null)
            {
                stationStairwellTarget.gameObject.AddComponent<MeshCollider>();
            }
        }

        // เพิ่ม MeshCollider ให้ชานชาลาและรางรถไฟใต้ดินเดินเหยียบได้ ไม่ร่วงหรือชนกำแพงอากาศ
        string[] stationMeshes = { "TRAIN_STATION_STARIRWELL.001", "TRAIN_STATION_STARIRWELL.002", "TRAIN_STATION_RAILWAY", "TRAIN_STATION", "TRAIN_STATION_STATION" };
        foreach (var smName in stationMeshes)
        {
            var smGo = GameObject.Find(smName);
            if (smGo != null && smGo.GetComponent<MeshCollider>() == null)
            {
                smGo.AddComponent<MeshCollider>();
            }
        }
    }

    public void ConfigureEvidenceItemComponents()
    {
        // ฟังก์ชันช่วยตั้งค่า Trigger ให้มีขนาดกะทัดรัดและเป็น isTrigger=true เสมอ ป้องกันการกลายเป็นกำแพงอากาศขวางผู้เล่น
        void SetupEvidenceTrigger(GameObject go, string id, string ciraClass, string title, string desc, float range)
        {
            if (go == null) return;
            var ev = go.GetComponent<EvidenceItem>() ?? go.AddComponent<EvidenceItem>();
            ev.evidenceId = id;
            ev.ciraClassName = ciraClass;
            ev.thaiTitle = title;
            ev.thaiDescription = desc;
            ev.detectionRange = range;

            var box = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.015f, 0.015f, 0.015f); // ~1.5m ใน World Space เพราะโมเดลสเกล 100
            box.center = Vector3.zero;
        }

        // 1. Blood Trail
        SetupEvidenceTrigger(bloodTrailGo, "station_blood_trail", "blood", "รอยเลือดบนทางแยกชานชาลา", "หยดเลือดสดส่องตามทางแยก เลี้ยวขวาเข้าสู่ชานชาลาที่ 2 มุ่งตรงไปยังขบวนรถไฟ", 14f);

        // 2. Spent Bullet Casing
        SetupEvidenceTrigger(bulletCasingGo, "station_bullet_casing", "bullet", "ปลอกกระสุนปืนลูกโม่ Webley .455", "ปลอกกระสุนทองเหลืองเพิ่งยิงสดๆ ยังมีควันดินปืน ตกอยู่ใกล้รางรถไฟตู้ท้ายขบวน", 12f);

        // 3. Frozen Pocket Watch
        SetupEvidenceTrigger(frozenWatchGo, "station_frozen_watch", "clock", "นาฬิกาพกเรือนเงินหยุดเดิน 11:45 PM", "หน้าปัดแตกค้างอยู่ที่เวลา 11:45 PM พอดี ตรงกับเวลาออกของขบวนรถด่วนพิเศษเที่ยวเที่ยงคืน", 12f);

        // 4. Railway Ticket
        SetupEvidenceTrigger(railwayTicketGo, "station_railway_ticket", "train_ticket", "ตั๋วรถไฟระบุหมายเลขตู้ขบวน", "ตั๋วรถไฟฉีกขาดระบุ: รถด่วนสายเหนือ ชานชาลา 2 ตู้โดยสารหมายเลข 4 (Carriage 04)", 12f);

        // 5. Moriarty Note
        SetupEvidenceTrigger(moriartyNoteGo, "station_moriarty_note", "letter", "จดหมายตราประทับตัว M จากมอริอาตี้", "\"สารวัตรนิวแมน... เขาซ่อนตัวอยู่ที่ตู้หน้าสุด! - M\" (คำใบ้ลวงที่จงใจชี้นำผิดทาง!)", 12f);
    

        // จดหมาย M: โมเดลเดิมแบนติดพื้นจนมองไม่เห็นในที่มืด -> ซ่อนของเดิม แล้ววางกระดาษจำลอง + ตราประทับ M ให้เห็นชัด
        if (moriartyNoteGo != null && !_noteLifted)
        {
            _noteLifted = true;
            BuildVisibleNoteProp(moriartyNoteGo);
        }

    }

    private bool _noteLifted;

    private void BuildVisibleNoteProp(GameObject note)
    {
        foreach (var r in note.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.gameObject.name.StartsWith("Scene4_NoteProp")) continue;
            r.enabled = false; // collider/EvidenceItem ยังอยู่ จึงยังสแกนได้ตามเดิม
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material Mat(Color c)
        {
            var m = new Material(shader) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            return m;
        }

        Vector3 basePos = note.transform.position;
        if (Physics.Raycast(basePos + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)) basePos.y = hit.point.y;
        basePos.y += 0.02f;

        var root = new GameObject("Scene4_NoteProp");
        root.transform.SetParent(note.transform, true);
        root.transform.position = basePos;
        root.transform.rotation = Quaternion.Euler(0f, 18f, 0f);

        var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paper.name = "Scene4_NotePropPaper";
        paper.transform.SetParent(root.transform, false);
        paper.transform.localScale = new Vector3(0.34f, 0.012f, 0.24f);
        Destroy(paper.GetComponent<Collider>());
        paper.GetComponent<MeshRenderer>().sharedMaterial = Mat(new Color(0.93f, 0.88f, 0.74f));

        var seal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        seal.name = "Scene4_NotePropSeal";
        seal.transform.SetParent(root.transform, false);
        seal.transform.localPosition = new Vector3(0.06f, 0.012f, -0.02f);
        seal.transform.localScale = new Vector3(0.07f, 0.004f, 0.07f);
        Destroy(seal.GetComponent<Collider>());
        seal.GetComponent<MeshRenderer>().sharedMaterial = Mat(new Color(0.62f, 0.07f, 0.07f));

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var textGo = new GameObject("Scene4_NotePropText");
        textGo.transform.SetParent(root.transform, false);
        textGo.transform.localPosition = new Vector3(-0.05f, 0.011f, 0.02f);
        textGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var tm = textGo.AddComponent<TextMesh>();
        tm.font = font;
        tm.text = "M";
        tm.fontSize = 64;
        tm.characterSize = 0.012f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(0.12f, 0.08f, 0.05f);
        textGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    private void FindAndSetupFlickeringLamps()
    {
        _stationLamps.Clear();
        var allLamps = FindObjectsByType<Light>();
        foreach (var l in allLamps)
        {
            if (l.name.Contains("TRAINSTATION_LAMP") || l.transform.position.y < -5f)
            {
                _stationLamps.Add(l);
            }
        }
        StartCoroutine(FlickeringLampsRoutine());
    }

    private IEnumerator FlickeringLampsRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(2.0f, 5.0f));
            if (_stationLamps.Count > 0 && _isQuest4Active)
            {
                Light randomLamp = _stationLamps[UnityEngine.Random.Range(0, _stationLamps.Count)];
                if (randomLamp != null)
                {
                    float origIntensity = randomLamp.intensity;
                    for (int i = 0; i < 3; i++)
                    {
                        randomLamp.intensity = origIntensity * 0.15f;
                        yield return new WaitForSeconds(0.06f);
                        randomLamp.intensity = origIntensity;
                        yield return new WaitForSeconds(0.08f);
                    }
                }
            }
        }
    }

    /// <summary>รีเซ็ตสถานะของฉาก 4 ให้เล่นซ้ำได้ (ใช้กับทางลัดทดสอบ [P])</summary>
    public void DebugResetForReplay()
    {
        StopAllCoroutines();
        StartCoroutine(FlickeringLampsRoutine());
        _scannedCount = 0;
        _scannedEvidenceIds.Clear();
        _hasEnteredStation = false;
        _isDeductionOpened = false;
        _isMinigameSolved = false;
        _isDeductionCompleted = false;
        _scene4StartTime = -1f;
        _deductionResult = null;
        IsQuest4Finished = false;
        foreach (var go in new[] { bloodTrailGo, bulletCasingGo, frozenWatchGo, railwayTicketGo, moriartyNoteGo, timetableBoardGo })
        {
            if (go == null) continue;
            var ev = go.GetComponent<EvidenceItem>();
            if (ev != null) ev.isScanned = false;
        }
    }

    public void ActivateQuest4()
    {
        _isQuest4Active = true;
        if (CiraCoreVisionScanner.Instance != null) CiraCoreVisionScanner.Instance.SetActiveSceneModel(4);
        ShowObjectiveHUD(true);

        if (_objectiveTitleText != null)
        {
            _objectiveTitleText.text = questTitle;
        }
        if (_objectiveProgressText != null)
        {
            _objectiveProgressText.text = "• มุ่งหน้าสู่หน้าสถานีรถไฟเมโทรโพลิแทน (Metropolitan Railway Station Entrance)";
        }
        if (_hintText != null)
        {
            _hintText.text = "◈ เดินตามหมุดนำทางไปยังหน้าสถานีรถไฟเพื่อตามรอยแจ็ค";
        }

        if (WaypointNavigationSystem.Instance != null && stationStairwellTarget != null)
        {
            WaypointNavigationSystem.Instance.SetTarget(stationStairwellTarget, "สถานีรถไฟเมโทรโพลิแทน (Metropolitan Railway)");
            WaypointNavigationSystem.Instance.SetVisible(true);
        }

        Debug.Log("<color=#F2C94C><b>[QUEST 4]</b> เริ่มต้นภารกิจเควสที่ 4: การไล่ล่าใต้ดิน เรียบร้อยแล้ว!</color>");
    }

    public void OnPlayerEnteredStation()
    {
        if (!_isQuest4Active) return;
        if (_hasEnteredStation) return;
        _hasEnteredStation = true;
        _scene4StartTime = Time.time;

        UpdateStationObjectiveDisplay();
        if (_hintText != null)
        {
            _hintText.text = "◈ กด [V] เปิดระบบสแกน CiRA CORE แล้วตามรอยบนชานชาลาให้ทันก่อนที่ร่องรอยจะจางหาย";
        }

        // ซ่อน HUD และล็อกการสแกนระหว่างคัตซีนเปิดฉาก
        ShowObjectiveHUD(false);
        CiraCoreVisionScanner.IsScannerBlocked = true;
        if (WaypointNavigationSystem.Instance != null)
        {
            WaypointNavigationSystem.Instance.ClearTarget();
            WaypointNavigationSystem.Instance.SetVisible(false);
        }

        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.PlayScene4UndergroundStationCutscene(
                StationEvidencePositions(), OnStationCutsceneFinished);
        }
        else
        {
            OnStationCutsceneFinished();
        }
    }

    private void OnStationCutsceneFinished()
    {
        CiraCoreVisionScanner.IsScannerBlocked = false;
        ShowObjectiveHUD(true);
        if (WaypointNavigationSystem.Instance != null) WaypointNavigationSystem.Instance.SetVisible(false); // หมุดใช้นำทางเควสเท่านั้น ไม่ชี้ตำแหน่งหลักฐาน
        if (_whisperCoroutine != null) StopCoroutine(_whisperCoroutine);
        _whisperCoroutine = StartCoroutine(MoriartyWhispersRoutine());
    }

    /// <summary>ตำแหน่งวัตถุพยาน 5 ชิ้น ใช้จัดช็อตกล้องคัตซีน</summary>
    public List<Vector3> StationEvidencePositions()
    {
        var list = new List<Vector3>();
        foreach (var go in new[] { bloodTrailGo, bulletCasingGo, frozenWatchGo, railwayTicketGo, moriartyNoteGo })
        {
            if (go != null) list.Add(go.transform.position);
        }
        return list;
    }

    private Vector3 EvidencePos(GameObject go, Vector3 fallback)
    {
        return go != null ? go.transform.position : fallback;
    }

    private void UpdateStationObjectiveDisplay()
    {
        if (_objectiveProgressText == null) return;
        if (_scannedCount < totalEvidenceRequired)
        {
            _objectiveProgressText.text = $"• สแกนหาร่องรอยบนชานชาลาด้วย CiRA CORE: <color=#5CE1E6><b>{_scannedCount} / {totalEvidenceRequired}</b></color>";
        }
        else
        {
            _objectiveProgressText.text = "• ครบ 5 ชิ้นแล้ว! <color=#66FF99><b>เชื่อมโยงหลักฐานทั้งหมดเข้าด้วยกัน</b></color>";
        }
    }

    /// <summary>เสียงกระซิบของมอริอาร์ตี้ขณะผู้เล่นสแกน สร้างความกดดัน "มีคนกำลังจ้องมองอยู่"</summary>
    private IEnumerator MoriartyWhispersRoutine()
    {
        string[] lines =
        {
            "เสียงจากเงามืด: \"นาฬิกากำลังเดินนะ สารวัตร... ขบวนรถไม่รอใคร\"",
            "เสียงจากเงามืด: \"คุณเดินตามรอยที่ผมเตรียมไว้ได้เก่งมาก\"",
            "เสียงจากเงามืด: \"ลองเชื่อโน้ตของผมสักครั้งสิ... ผมซื่อสัตย์เสมอ\"",
        };
        int i = 0;
        yield return new WaitForSeconds(28f);
        while (_isQuest4Active && !_isDeductionOpened && i < lines.Length)
        {
            bool busy = (CinematicLetterbox.Instance != null && CinematicLetterbox.Instance.IsLetterboxActive)
                     || CaseDeductionBoardUI.IsOpen || ChapterSummaryUI.IsOpen;
            if (!busy)
            {
                DetectiveThoughtToast.Show(lines[i]);
                i++;
                yield return new WaitForSeconds(UnityEngine.Random.Range(38f, 55f));
            }
            else
            {
                yield return new WaitForSeconds(3f);
            }
        }
    }

    private void HandleStationEvidenceScanned(EvidenceItem item)
    {
        if (!_isQuest4Active) return;
        if (item == null) return;
        if (!item.evidenceId.StartsWith("station_")) return;

        if (_scannedEvidenceIds.Add(item.evidenceId))
        {
            _scannedCount = _scannedEvidenceIds.Count;
            UpdateStationObjectiveDisplay();

            DetectiveThoughtToast.Show(GetStationEvidenceThought(item.evidenceId));
            if (_hintText != null) _hintText.text = GetStationEvidenceHint(item.evidenceId);

            if (_scannedCount >= totalEvidenceRequired && !_isDeductionOpened)
            {
                _isDeductionOpened = true;
                StartCoroutine(OpenStationDeductionRoutine());
            }
        }
    }

    private static string GetStationEvidenceThought(string id)
    {
        switch (id)
        {
            case "station_blood_trail":
                return "หยดเลือดเล็กลงและห่างขึ้นเรื่อย ๆ เขากำลังวิ่งและเลือดไหลน้อยลง รอยนี้ไปทางชานชาลาที่สอง";
            case "station_bullet_casing":
                return "ปลอกกระสุน Webley .455 ยังอุ่นอยู่ ตกใกล้ตู้ท้ายขบวน คนยิงหยุดรีโหลดตรงนี้ก่อนวิ่งต่อ";
            case "station_frozen_watch":
                return "นาฬิกาพกหยุดที่ 11:45 กระจกแตกจากแรงกระแทก ตรงกับเวลาออกของรถด่วนที่กำลังจะออก";
            case "station_railway_ticket":
                return "ตั๋วครึ่งใบที่ขาดจากโรงละคร ตรงกับครึ่งนี้พอดี ตู้โดยสารหมายเลข 4 ชานชาลา 2";
            case "station_timetable_board":
                return "ป้ายตารางเดินรถของจริง ขบวนที่ออก 11:45 PM เป๊ะ คือรถด่วนสายเหนือ ชานชาลา 2 ตรงกับเวลาบนนาฬิกาพก";
            case "station_moriarty_note":
                return "ตราประทับ M จดหมายบอกว่าเขาอยู่ตู้หน้าสุด แต่ลายมือเรียบร้อยเกินไป นี่คือของที่เตรียมไว้ให้เราอ่าน";
        }
        return null;
    }

    private static string GetStationEvidenceHint(string id)
    {
        switch (id)
        {
            case "station_blood_trail": return "◈ รอยเลือดจางหายไปทางชานชาลาที่ 2 (ฝั่งขวา)";
            case "station_bullet_casing": return "◈ ปลอกกระสุนตกใกล้รางรถไฟตู้ท้ายขบวน";
            case "station_frozen_watch": return "◈ 11:45 PM ตรงกับเวลาออกของขบวนรถด่วน";
            case "station_railway_ticket": return "◈ ตั๋วระบุตู้โดยสารหมายเลข 4 (Carriage 04)";
            case "station_timetable_board": return "◈ ป้ายตารางเดินรถยืนยัน: รถด่วนสายเหนือ 11:45 PM ที่ชานชาลา 2";
            case "station_moriarty_note": return "◈ จดหมายบอกตู้หน้าสุด ขัดแย้งกับหลักฐานอื่น จำไว้ว่ามอริอาร์ตี้ชอบเล่นกับจิตใจ";
        }
        return "";
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

    // ==========================================
    // ข้อสันนิษฐาน (กระดานอนุมาน) -> Connecting the Dots -> คัตซีนจับกุม -> หน้าสรุปบท
    // ==========================================
    private IEnumerator OpenStationDeductionRoutine()
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

        _deductionConfig = BuildStationDeductionConfig();
        CaseDeductionBoardUI.Open(_deductionConfig, OnStationDeductionSolved);
    }

    private CaseDeductionBoardUI.Config BuildStationDeductionConfig()
    {
        var config = new CaseDeductionBoardUI.Config
        {
            letterhead = "METROPOLITAN POLICE  ·  CASE FILE 1888/JR  ·  METROPOLITAN RAILWAY",
            openQuestion = "แจ็คเป็นเพียงมือที่ลงมือ แล้วใครคือคนที่กำหนดว่าเขาต้องไปอยู่ที่ไหน?"
        };

        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("blood", "รอยเลือดบนชานชาลา",
            "หยดเลือดสดจางลงเรื่อย ๆ ตามทางแยก มุ่งไปทางชานชาลาที่ 2"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("casing", "ปลอกกระสุน Webley .455",
            "ยังอุ่น ตกใกล้ตู้ท้ายขบวน คนยิงหยุดรีโหลดตรงนี้"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("watch", "นาฬิกาพกหยุดเดิน",
            "ค้างที่ 11:45 PM ตรงกับเวลาออกของรถด่วน"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("ticket", "ตั๋วรถไฟครึ่งใบ",
            "ตรงกับตั๋วที่โรงละคร ระบุชานชาลา 2 ตู้หมายเลข 4"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("note", "จดหมายตราประทับ M",
            "\"เขาอยู่ตู้หน้าสุด\" ลายมือเรียบร้อยเกินไป"));

        // ข้อสันนิษฐานที่ 1: ทางแยก
        var s1 = new CaseDeductionBoardUI.Step
        {
            question = "รอยเลือดจางหายไปที่ทางแยกของสถานี หยดเลือดที่ยังเปียกเรียงต่อกันไปทางไหน แจ็คหนีไปชานชาลาใด?",
            relatedEvidenceIds = new[] { "blood", "casing" },
            conclusion = "รอยเลือดและปลอกกระสุนชี้ไปชานชาลาที่ 2 ฝั่งขวา"
        };
        s1.options.Add(new CaseDeductionBoardUI.Option("ชานชาลาที่ 1 ฝั่งซ้าย เพราะใกล้ทางออกถนน", false,
            "ไม่มีหยดเลือดหรือรอยเท้าฝั่งซ้ายเลย ทางออกถนนไม่ใช่เหตุผลเมื่อทิ้งรอยไว้ชัดเจนฝั่งตรงข้าม"));
        s1.options.Add(new CaseDeductionBoardUI.Option("ชานชาลาที่ 2 ฝั่งขวา ตามรอยเลือดและปลอกกระสุน", true,
            "หยดเลือดยังเปียกและห่างขึ้นเรื่อย ๆ ชี้ไปทางขวา ส่วนปลอกกระสุนที่ยังอุ่นตกใกล้รางฝั่งเดียวกัน"));
        s1.options.Add(new CaseDeductionBoardUI.Option("เขาแยกตัวไปทั้งสองทางเพื่อลวงตำรวจ", false,
            "รอยเลือดมีเส้นเดียว ไม่มีรอยแยกออกอีกทาง การแยกตัวไม่มีหลักฐานรองรับ"));
        s1.options.Add(new CaseDeductionBoardUI.Option("เขาขึ้นบันไดกลับไปบนถนนแล้ว", false,
            "รอยเลือดไม่มีบนบันได และขบวนรถด่วนยังจอดอยู่ เขาไม่มีเหตุผลต้องกลับขึ้นไป"));
        config.steps.Add(s1);

        // ข้อสันนิษฐานที่ 2: ขบวนไหน
        var s2 = new CaseDeductionBoardUI.Step
        {
            question = "นาฬิกาพกหยุดที่ 11:45 PM บนชานชาลาที่ 2 ขบวนใดคือขบวนที่แจ็คต้องขึ้น?",
            relatedEvidenceIds = new[] { "watch", "ticket" },
            conclusion = "นาฬิกาและตั๋วตรงกับรถด่วนสายเหนือ 11:45 PM"
        };
        s2.options.Add(new CaseDeductionBoardUI.Option("ขบวนรถด่วนสายเหนือที่ออกเวลา 11:45 PM", true,
            "เวลาบนนาฬิกาตรงกับตารางเดินรถ และตั๋วที่ขาดครึ่งระบุสายเหนือชานชาลา 2 ทั้งสองอย่างชี้ไปที่ขบวนเดียวกัน"));
        s2.options.Add(new CaseDeductionBoardUI.Option("ขบวนรถเที่ยวเที่ยงคืนตรงที่ออกหลังจากนั้น", false,
            "ถ้าเป็นขบวนเที่ยงคืน นาฬิกาจะไม่ถูกทำให้หยุดที่ 11:45 เวลาที่หยุดคือเวลาที่เขาต้องขึ้นรถ"));
        s2.options.Add(new CaseDeductionBoardUI.Option("ขบวนรถสายตะวันออกที่จอดอยู่ฝั่งตรงข้าม", false,
            "ตั๋วระบุสายเหนือ ไม่มีหลักฐานใดชี้ไปสายตะวันออก"));
        s2.options.Add(new CaseDeductionBoardUI.Option("ไม่มีขบวนไหนเลย นาฬิกาแตกเพราะการต่อสู้เท่านั้น", false,
            "นาฬิกาที่หยุดพอดีเวลาออกของรถไม่ใช่เรื่องบังเอิญ มันถูกวางไว้ให้เราเห็นเวลา"));
        config.steps.Add(s2);

        // ข้อสันนิษฐานที่ 3: ตู้ไหน + จดหมายลวง
        var s3 = new CaseDeductionBoardUI.Step
        {
            question = "ตั๋วระบุตู้หมายเลข 4 ปลอกกระสุนอยู่ท้ายขบวน แต่จดหมายตรา M บอกว่าเขาอยู่ตู้หน้าสุด เราควรเชื่อสิ่งใด?",
            relatedEvidenceIds = new[] { "ticket", "casing", "note" },
            conclusion = "ตู้หมายเลข 4 ท้ายขบวน จดหมาย M คือคำลวง"
        };
        s3.options.Add(new CaseDeductionBoardUI.Option("เชื่อจดหมาย เพราะมีตราประทับ M ยืนยัน", false,
            "ตราประทับแสดงว่าใครเขียน ไม่ได้แสดงว่าเขียนความจริง มอริอาร์ตี้ไม่เคยบอกความจริงโดยไม่มีเหตุผล"));
        s3.options.Add(new CaseDeductionBoardUI.Option("เชื่อตั๋วและปลอกกระสุน ไปตู้หมายเลข 4 ท้ายขบวน", true,
            "ตั๋วที่ขาดครึ่งและปลอกกระสุนที่ยังอุ่นเป็นร่องรอยที่เกิดขึ้นเอง ไม่มีใครเตรียมไว้ ส่วนจดหมายเขียนเรียบร้อยเกินไป คือสิ่งที่ถูกจัดไว้"));
        s3.options.Add(new CaseDeductionBoardUI.Option("แบ่งกำลังค้นทุกตู้พร้อมกัน", false,
            "ไม่มีเวลาเหลือ ขบวนจะออกในไม่กี่นาที และเรามีหลักฐานชัดพอจะเลือกตู้เดียว"));
        s3.options.Add(new CaseDeductionBoardUI.Option("รอให้ขบวนออกแล้วค่อยตามที่สถานีปลายทาง", false,
            "นั่นคือเดินตามกติกาของเขา ปล่อยให้แจ็คหนีและเสียโอกาสที่ดีที่สุดไป"));
        config.steps.Add(s3);

        return config;
    }

    private void OnStationDeductionSolved(CaseDeductionBoardUI.Result result)
    {
        _deductionResult = result;
        StartCoroutine(TriggerEvidenceMappingClimaxRoutine());
    }

    private IEnumerator TriggerEvidenceMappingClimaxRoutine()
    {
        yield return new WaitForSeconds(0.4f);

        if (_objectiveProgressText != null)
        {
            _objectiveProgressText.text = "<color=#2ECC71>★ รวบรวมหลักฐานครบ 5 ชิ้นแล้ว! เข้าสู่โหมด Connecting the Dots</color>";
        }

        // เปิดมินิเกมเชื่อมโยงหลักฐาน (ผู้เล่นกด ESC ปิดเองไม่ได้ ถ้าถูกปิดจะเปิดซ้ำจนกว่าจะเสร็จ)
        if (EvidenceConnectingDotsMinigame.Instance != null)
        {
            _isMinigameSolved = false;
            EvidenceConnectingDotsMinigame.Instance.OpenMinigame(OnDeductionSolved);
            StartCoroutine(MinigameGuardRoutine());
        }
        else
        {
            OnDeductionSolved();
        }
    }

    private IEnumerator MinigameGuardRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        while (!_isMinigameSolved)
        {
            var mg = EvidenceConnectingDotsMinigame.Instance;
            if (mg != null && !mg.IsMinigameActive && !_isMinigameSolved)
            {
                yield return new WaitForSeconds(0.2f);
                if (!_isMinigameSolved && !mg.IsMinigameActive) mg.OpenMinigame(OnDeductionSolved);
            }
            yield return new WaitForSeconds(0.3f);
        }
    }

    private void OnDeductionSolved()
    {
        if (_isMinigameSolved) return;
        _isMinigameSolved = true;
        _isDeductionCompleted = true;

        CiraCoreVisionScanner.IsScannerBlocked = true;
        ShowObjectiveHUD(false);
        if (WaypointNavigationSystem.Instance != null) WaypointNavigationSystem.Instance.SetVisible(false);

        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.PlayScene4CaptureCutscene(StationEvidencePositions(), ShowChapter4Summary);
        }
        else
        {
            ShowChapter4Summary();
        }

        Debug.Log("<color=#2ECC71><b>[QUEST 4]</b> เชื่อมโยงหลักฐานสำเร็จ เข้าสู่คัตซีนจับกุม</color>");
    }

    /// <summary>หน้าสรุปบทที่ 4: เส้นทางหลบหนีที่แท้จริงและกลลวงของมอริอาร์ตี้</summary>
    private void ShowChapter4Summary()
    {
        if (CinematicCutsceneManager.Instance != null) CinematicCutsceneManager.Instance.SetPlayerControlsLocked(true);
        SetCursorLockedState(false);

        var data = new ChapterSummaryUI.Data
        {
            chapterNumber = 4,
            chapterTitle = "การไล่ล่าใต้ดิน",
            location = "สถานีรถไฟเมโทรโพลิแทน  ·  ลอนดอน  ·  ค.ศ. 1888",
            openQuestion = "แจ็คเป็นแค่มือที่ลงมือ แล้วมอริอาร์ตี้อยู่ที่ไหน?",
            nextLead = "แหวนสองวงจากโบสถ์เซนต์แมรี่คือกุญแจของต้นกำเนิดทั้งหมด",
            elapsedSeconds = _scene4StartTime > 0f ? Time.time - _scene4StartTime : -1f,
            accuracy = _deductionResult != null ? _deductionResult.accuracy : -1
        };

        if (_deductionConfig != null)
        {
            foreach (var step in _deductionConfig.steps) data.chain.Add(step.conclusion);
        }
        data.chain.Add("ทุกร่องรอยถูกมอริอาร์ตี้จัดวางไว้ล่วงหน้า");

        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("รอยเลือดบนชานชาลา", "จางไปทางชานชาลาที่ 2 ฝั่งขวา"));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("ปลอกกระสุน Webley .455", "ยังอุ่น ตกใกล้ตู้ท้ายขบวน"));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("นาฬิกาพกหยุดเดิน", "11:45 PM ตรงกับเวลาออกของรถด่วน"));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("ตั๋วรถไฟครึ่งใบ", "ตู้โดยสารหมายเลข 4 ชานชาลา 2", true));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("จดหมายตราประทับ M", "คำลวงที่ชี้ไปตู้หน้าสุด"));

        ChapterSummaryUI.Show(data, OnChapter4SummaryContinue);
    }

    private void OnChapter4SummaryContinue()
    {
        IsQuest4Finished = true;
        if (CinematicLetterbox.Instance != null) CinematicLetterbox.Instance.HideLetterbox();

        Debug.Log("<color=#2ECC71><b>[QUEST 4 COMPLETED]</b> เควสที่ 4 จบสมบูรณ์ เข้าสู่ฉากจบ</color>");

        // ฉากจบ: CASE CLOSED -> โฮมส์สรุปคดี -> ระบบถูกแฮ็ก -> เทอร์มินัลของมอริอาร์ตี้
        GameEndingSequence.Play();
    }

    public void ShowObjectiveHUD(bool show)
    {
        if (_objectivePanel != null)
        {
            _objectivePanel.SetActive(show);
        }
    }

    private void BuildQuest4HUD()
    {
        Canvas parentCanvas = FindAnyObjectByType<Canvas>();
        if (parentCanvas == null) return;

        _objectivePanel = new GameObject("Quest4_Objective_HUD");
        _objectivePanel.transform.SetParent(parentCanvas.transform, false);

        RectTransform panelRt = _objectivePanel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 1f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.anchoredPosition = new Vector2(40f, -40f);
        panelRt.sizeDelta = new Vector2(660f, 140f);

        Image bg = _objectivePanel.AddComponent<Image>();
        bg.color = Color.clear; // ไร้กล่องพื้นหลัง แบบ Genshin Impact

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_objectivePanel.transform, false);
        RectTransform tRt = titleObj.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0f, 1f);
        tRt.anchoredPosition = new Vector2(0f, 0f);
        tRt.sizeDelta = new Vector2(0f, 32f);
        _objectiveTitleText = titleObj.AddComponent<Text>();
        _objectiveTitleText.font = thaiFontBold != null ? thaiFontBold : VisionOSUITheme.BoldFont;
        _objectiveTitleText.fontSize = 20;
        _objectiveTitleText.color = VisionOSUITheme.TextPrimary;
        _objectiveTitleText.text = questTitle;
        var tShadow = titleObj.AddComponent<Shadow>();
        tShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        tShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // Progress
        GameObject progObj = new GameObject("Progress");
        progObj.transform.SetParent(_objectivePanel.transform, false);
        RectTransform pRt = progObj.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0f, 1f);
        pRt.anchorMax = new Vector2(1f, 1f);
        pRt.pivot = new Vector2(0f, 1f);
        pRt.anchoredPosition = new Vector2(0f, -34f);
        pRt.sizeDelta = new Vector2(0f, 28f);
        _objectiveProgressText = progObj.AddComponent<Text>();
        _objectiveProgressText.font = thaiFont != null ? thaiFont : VisionOSUITheme.PrimaryFont;
        _objectiveProgressText.fontSize = 17;
        _objectiveProgressText.color = VisionOSUITheme.TextPrimary;
        _objectiveProgressText.text = "• มุ่งหน้าสู่สถานีรถไฟใต้ดิน (Underground Railway Station)";
        var pShadow = progObj.AddComponent<Shadow>();
        pShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        pShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // Hint
        GameObject hintObj = new GameObject("Hint");
        hintObj.transform.SetParent(_objectivePanel.transform, false);
        RectTransform hRt = hintObj.AddComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 1f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot = new Vector2(0f, 1f);
        hRt.anchoredPosition = new Vector2(0f, -64f);
        hRt.sizeDelta = new Vector2(0f, 26f);
        _hintText = hintObj.AddComponent<Text>();
        _hintText.font = thaiFont != null ? thaiFont : VisionOSUITheme.PrimaryFont;
        _hintText.fontSize = 14;
        _hintText.color = new Color(0.95f, 0.82f, 0.42f, 1f);
        _hintText.text = "◆ เดินตามหมุดนำทางสีทองลงสู่ชานชาลาสถานีรถไฟใต้ดิน";
        var hShadow = hintObj.AddComponent<Shadow>();
        hShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        hShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // Hotkey Prompt
        GameObject hotkeyGo = new GameObject("ObjCaseArchivePrompt");
        hotkeyGo.transform.SetParent(_objectivePanel.transform, false);
        RectTransform hkRect = hotkeyGo.AddComponent<RectTransform>();
        hkRect.anchorMin = new Vector2(0f, 1f);
        hkRect.anchorMax = new Vector2(1f, 1f);
        hkRect.pivot = new Vector2(0f, 1f);
        hkRect.anchoredPosition = new Vector2(0f, -94f);
        hkRect.sizeDelta = new Vector2(0f, 24f);
        Text hkt = hotkeyGo.AddComponent<Text>();
        hkt.font = thaiFont != null ? thaiFont : VisionOSUITheme.PrimaryFont;
        hkt.fontSize = 13;
        hkt.alignment = TextAnchor.MiddleLeft;
        hkt.color = VisionOSUITheme.TextSecondary;
        hkt.text = "<color=#F2C94C><b>[E]</b></color> เมนูเควส & แฟ้มบันทึกคดี";
        var hkShadow = hotkeyGo.AddComponent<Shadow>();
        hkShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        hkShadow.effectDistance = new Vector2(1.5f, -1.5f);
    }
}
