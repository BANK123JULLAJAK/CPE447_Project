using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ตัวจัดการเควส Scene 1: จุดเริ่มต้นแห่งปริศนา (โบสถ์เซนต์แมรี่ / St. Mary Church)
/// ทำงานร่วมกับ CiRA CORE, Cinematic Letterbox, และฟอนต์ภาษาไทย (Google Sans)
/// </summary>
public class StMaryChurchQuestManager : MonoBehaviour
{
    private static StMaryChurchQuestManager _instance;
    public static StMaryChurchQuestManager Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<StMaryChurchQuestManager>();
            return _instance;
        }
    }

    [Header("=== การตั้งค่าเนื้อเรื่องเควส 1 ===")]
    public string questTitle = "Scene 1: จุดเริ่มต้นแห่งปริศนา (โบสถ์เซนต์แมรี่)";
    public int totalEvidenceRequired = 5;

    [Header("=== ฟอนต์ภาษาไทย (Prompt Typography) ===")]
    public Font thaiFont;
    public Font thaiFontBold;

    [Header("=== พื้นผิวสมุดบันทึกโบราณ (Parchment Paper Sprite) ===")]
    public Sprite parchmentPaperSprite;

    // State
    private int _scannedCount = 0;
    /// <summary>จำนวนวัตถุพยานที่สแกนแล้ว (ใช้แสดงในแฟ้มคดีเมนู E)</summary>
    public int ScannedCount => _scannedCount;
    private List<EvidenceItem> _scannedItems = new List<EvidenceItem>();
    private bool _isDeductionCompleted = false;

    // UI Elements
    private GameObject _objectivePanel;
    private Text _objectiveTitleText;
    private Text _objectiveProgressText;
    private Text _hintText;

    /// <summary>ใช้โดยระบบสแกนเพื่อบล็อกการสแกนระหว่างตอบข้อสันนิษฐาน/ดูหน้าสรุปบท</summary>
    public bool IsDeductionPanelActive => CaseDeductionBoardUI.IsOpen || ChapterSummaryUI.IsOpen;

    private void Awake()
    {
        this.enabled = true;
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // โหลดฟอนต์ Static TTF ที่คมชัดสำหรับภาษาไทย (Prompt / Sarabun) ป้องกันปัญหา Variable Font ตัวอักษรทับซ้อน
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

        if (parchmentPaperSprite == null)
        {
            parchmentPaperSprite = GameAssets.Load<Sprite>("Assets/UI/lilydust-old-2012064.jpg");
        }

        EnsureUIReferences();
        SubscribeToScanner();
    }

    private bool _hasEnteredChurch = false;
    private float _investigationStartTime = -1f;
    private bool _isQuest1Finished = false;
    public bool IsQuest1Finished => _isQuest1Finished;
    private bool _isSubscribed = false;

    private void OnEnable()
    {
        SubscribeToScanner();
        EnsureUIReferences();
    }

    private void OnDisable()
    {
        UnsubscribeFromScanner();
    }

    private void SubscribeToScanner()
    {
        if (_isSubscribed) return;
        var scanner = CiraCoreVisionScanner.Instance;
        if (scanner != null)
        {
            scanner.OnEvidenceScanned -= HandleEvidenceScanned;
            scanner.OnEvidenceScanned += HandleEvidenceScanned;
            _isSubscribed = true;
            Debug.Log("<color=cyan><b>[QUEST 1]</b> เชื่อมต่อ StMaryChurchQuestManager กับ CiraCoreVisionScanner สำเร็จ!</color>");
        }
    }

    private void UnsubscribeFromScanner()
    {
        if (!_isSubscribed) return;
        var scanner = CiraCoreVisionScanner.Instance;
        if (scanner != null)
        {
            scanner.OnEvidenceScanned -= HandleEvidenceScanned;
        }
        _isSubscribed = false;
    }

    private void Start()
    {
        EnsureUIReferences();
        SubscribeToScanner();

        // หากอยู่ในโหมดเควสที่ 2 อยู่แล้ว ให้ซ่อน HUD ของ Scene 1 ทันที ไม่ให้เกิดการซ้อนทับ
        if (LondonAlleyQuestManager.Instance != null && LondonAlleyQuestManager.Instance.IsQuest2Active)
        {
            ShowObjectiveHUD(false);
            return;
        }

        // Initial Exploration Phase on street
        ShowObjectiveHUD(true);
        if (_objectiveTitleText != null) _objectiveTitleText.text = "<color=#F2C94C>◈ ภารกิจหลัก (เควสที่ 1)</color>  จุดเริ่มต้นแห่งปริศนา: โบสถ์เซนต์แมรี่";
        if (_hintText != null) _hintText.text = "◈ เดินตามหมุดนำทางบนหน้าจอมุ่งหน้าสู่โบสถ์เพื่อเริ่มการสืบสวน";

        // ตั้งเป้าหมายนำทางไปยังโบสถ์เซนต์แมรี่
        Transform churchTarget = null;
        var triggerObj = GameObject.Find("Church_Entry_AreaTrigger");
        if (triggerObj != null) churchTarget = triggerObj.transform;
        else
        {
            var churchBuilding = GameObject.Find("CHURCH") ?? GameObject.Find("Church");
            if (churchBuilding != null) churchTarget = churchBuilding.transform;
        }

        if (WaypointNavigationSystem.Instance != null && churchTarget != null)
        {
            WaypointNavigationSystem.Instance.SetTarget(churchTarget, "โบสถ์เซนต์แมรี่ (St. Mary Church)");
            WaypointNavigationSystem.Instance.SetVisible(true);
        }
    }

    private void Update()
    {
        if (!_isSubscribed)
        {
            SubscribeToScanner();
        }

        // หากหน้าสรุปข้อสันนิษฐานหรือหน้าสรุปบทกำลังเปิดอยู่ ปลดล็อคเมาส์ให้สามารถคลิกเลือกได้ตลอดเวลา
        if (IsDeductionPanelActive)
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        // หากจบ Quest 1 แล้ว หรือ Quest 2 เริ่มทำงาน ให้ซ่อนและปิดการอัปเดต HUD ของ Scene 1 ทันที
        if (_isQuest1Finished || (LondonAlleyQuestManager.Instance != null && LondonAlleyQuestManager.Instance.IsQuest2Active))
        {
            if (_objectivePanel != null && _objectivePanel.activeSelf)
            {
                _objectivePanel.SetActive(false);
            }
            return;
        }

        // ขณะยังไม่เข้าโบสถ์ อัปเดตระยะทางแบบ Genshin Impact บน Objective HUD
        if (!_hasEnteredChurch)
        {
            int dist = WaypointNavigationSystem.Instance != null 
                ? WaypointNavigationSystem.Instance.CurrentDistanceMeters 
                : 0;

            if (_objectiveProgressText != null)
            {
                if (dist > 3)
                {
                    _objectiveProgressText.text = $"• เดินเข้าสู่โบสถ์เซนต์แมรี่  <color=#FFD24C><b>{dist}m</b></color>";
                }
                else
                {
                    _objectiveProgressText.text = "• เดินเข้าสู่โบสถ์เซนต์แมรี่  <color=#66FF99><b>[ถึงเป้าหมายแล้ว]</b></color>";
                }
            }
        }
    }

    /// <summary>
    /// เรียกเมื่อผู้เล่นเดินเข้าสู่พื้นที่บริเวณโบสถ์เซนต์แมรี่ (ผ่าน ChurchAreaTrigger)
    /// </summary>
    public void OnEnterChurchArea()
    {
        if (_hasEnteredChurch) return;
        _hasEnteredChurch = true;
        _investigationStartTime = Time.time;

        if (WaypointNavigationSystem.Instance != null)
        {
            WaypointNavigationSystem.Instance.SetVisible(false);
        }

        if (_objectiveTitleText != null) _objectiveTitleText.text = "<color=#F2C94C>◈ ภารกิจหลัก (เควสที่ 1)</color>  สืบสวนคดีฆาตกรรมในโบสถ์เซนต์แมรี่";
        StartCoroutine(PlayIntroCutsceneRoutine());
    }

    private IEnumerator PlayIntroCutsceneRoutine()
    {
        // ซ่อนหน้าต่างเควสทันทีขณะเล่นคัตซีน
        ShowObjectiveHUD(false);

        yield return new WaitForSeconds(0.2f);

        // สั่งเล่นคัตซีนภาพยนตร์เต็มรูปแบบผ่าน CinematicCutsceneManager สลับมุมกล้องภาพยนตร์ 3 ช็อต
        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.PlayScene1IntroCinematic(() =>
            {
                ShowObjectiveHUD(true);
                UpdateObjectiveDisplay();
                if (_hintText != null) _hintText.text = "[คลิกขวา] หรือ [V] เพื่อเปิดโหมดสแกนภาพ";
            });
        }
        else if (CinematicLetterbox.Instance != null)
        {
            var introDialogue = new List<CinematicLetterbox.DialogueEntry>()
            {
                new CinematicLetterbox.DialogueEntry(
                    "สารวัตรนิวแมน (Inspector Newman)",
                    "โบสถ์เซนต์แมรี่ (St. Mary Church)... ค่ำคืนนี้เงียบสงัดผิดปกติ ทว่าความวังเวงกลับซ่อนร่องรอยการฆาตกรรมอันโหดเหี้ยมไว้"
                ),
                new CinematicLetterbox.DialogueEntry(
                    "แฟ้มประวัติเหยื่อ (Victim Profile)",
                    "ผู้ตายชื่อ 'แมรี่ แอน' (Mary Ann) อายุ 42-45 ปี อาชีพช่างตัดเย็บ หญิงชนชั้นแรงงานในลอนดอนยุควิกตอเรีย ผู้เคยมีอดีตขมขื่นต้องทิ้งลูกชายไปเพราะความยากจน"
                ),
                new CinematicLetterbox.DialogueEntry(
                    "ระบบวิเคราะห์ภาพ CiRA CORE",
                    "ระบบ Computer Vision ได้โหลดโมเดล 20261005_11_23_53_0.1729 พร้อมแล้ว! กด [คลิกขวา] หรือ [V] เพื่อเข้าโหมดสแกนหาวัตถุพยาน 5 ชิ้นในโบสถ์"
                )
            };

            CinematicLetterbox.Instance.PlayDialogueSequence(introDialogue, () =>
            {
                CinematicLetterbox.Instance.HideLetterbox();
                ShowObjectiveHUD(true);
                UpdateObjectiveDisplay();
                if (_hintText != null) _hintText.text = "กด [คลิกขวา] หรือ [V] เพื่อเปิดโหมดสแกนภาพ";
            });
        }
        else
        {
            ShowObjectiveHUD(true);
        }
    }

    public void HandleEvidenceScanned(EvidenceItem item)
    {
        if (item == null) return;

        // หากจบเควส 1 แล้ว หรือเควส 2 กำลังทำงาน ให้ข้าม
        if (_isQuest1Finished || (LondonAlleyQuestManager.Instance != null && LondonAlleyQuestManager.Instance.IsQuest2Active))
            return;

        // ตรวจสอบว่าเป็นหลักฐานของโบสถ์ (Scene 1) หรือไม่
        string itemName = item.gameObject.name.ToUpper();
        string evId = (item.evidenceId ?? "").ToLower();
        string clsName = (item.ciraClassName ?? "").ToLower();

        bool isChurchEvidence = itemName.StartsWith("CHURCH_")
                             || evId == "prayer_sheet"
                             || evId == "handkerchief"
                             || evId == "ring_large"
                             || evId == "ring_small"
                             || (evId == "footprint" && itemName.Contains("CHURCH"))
                             || clsName == "prayer_sheet"
                             || clsName == "handkerchief"
                             || clsName == "ring";

        if (!isChurchEvidence) return;

        // เมื่อเริ่มสแกนหลักฐานในโบสถ์ ถือว่าผู้เล่นเข้าสู่บริเวณโบสถ์แล้วโดยอัตโนมัติ
        _hasEnteredChurch = true;
        if (_investigationStartTime < 0f) _investigationStartTime = Time.time;
        if (WaypointNavigationSystem.Instance != null)
        {
            WaypointNavigationSystem.Instance.SetVisible(false);
        }

        // ตรวจสอบว่าเคยบันทึกหลักฐานชิ้นนี้หรือยัง
        bool alreadyScanned = false;
        foreach (var ev in _scannedItems)
        {
            if (ev == item || (ev != null && ev.gameObject == item.gameObject) || (ev != null && ev.evidenceId == item.evidenceId && ev.name == item.name))
            {
                alreadyScanned = true;
                break;
            }
        }

        if (!alreadyScanned)
        {
            _scannedItems.Add(item);
            _scannedCount = _scannedItems.Count;
            Debug.Log($"<color=green><b>[QUEST 1]</b> สแกนพบหลักฐานชิ้นใหม่: {item.thaiTitle} ({item.name}) | ความคืบหน้า: {_scannedCount} / {totalEvidenceRequired}</color>");

            UpdateObjectiveDisplay();

            // นิวแมนพูดสั้น ๆ ว่าหลักฐานชิ้นนี้บอกอะไร (ไม่หยุดเกม)
            DetectiveThoughtToast.Show(GetEvidenceThought(item));

            if (_hintText != null)
            {
                if (_scannedCount < totalEvidenceRequired)
                {
                    _hintText.text = $"◈ วิเคราะห์แล้ว: <color=#5CE1E6>{item.thaiTitle}</color> (เหลืออีก {totalEvidenceRequired - _scannedCount} ชิ้น)";
                }
                else
                {
                    _hintText.text = "★ วิเคราะห์วัตถุพยานครบ 5 ชิ้นแล้ว! กำลังเปิดสมุดบันทึกสรุปคดี...";
                }
            }

            // Check if all 5 pieces found
            if (_scannedCount >= totalEvidenceRequired && !_isDeductionCompleted)
            {
                Debug.Log("<color=yellow><b>[QUEST 1]</b> วัตถุพยานครบ 5 ชิ้นแล้ว! เริ่มเปิดสมุดบันทึกนักสืบสรุปรูปคดี...</color>");
                StartCoroutine(TriggerEvidenceDeductionRoutine());
            }
        }
    }

    /// <summary>
    /// ความคิดของนิวแมนเมื่อสแกนพบหลักฐานแต่ละชิ้น (ปูทางไปสู่ข้อสรุปเรื่องแหวน 2 วง)
    /// </summary>
    private string GetEvidenceThought(EvidenceItem item)
    {
        string evId = (item.evidenceId ?? "").ToLower();
        string cls = (item.ciraClassName ?? "").ToLower();
        string objName = item.gameObject.name.ToLower();

        if (evId == "footprint" || cls == "footprint")
            return "รอยรองเท้าบู๊ตผู้ชาย ก้าวยาวสม่ำเสมอ... มันเดินออกไปเฉย ๆ ไม่ได้วิ่ง ฆาตกรไม่ได้ตื่นตระหนกเลย";
        if (evId == "handkerchief" || cls == "handkerchief")
            return "ผ้าเช็ดหน้าปักอักษร M.A. ของผู้ตายเอง มีคราบน้ำตาแห้งติดอยู่... เธอร้องไห้ก่อนตาย ไม่ใช่เพราะกลัว";
        if (evId == "prayer_sheet" || cls == "prayer_sheet")
            return "บทสวดขอการอภัยให้แม่ที่ละทิ้งลูก... ใครกันเลือกบทนี้มาวางไว้ข้างศพ?";
        if (evId == "ring_large" || objName.Contains("large") || objName.Contains("big"))
            return "แหวนเงินวงใหญ่ในมือผู้ตาย สลักตราประจำตระกูลไว้รอบวง ของเก่าแต่ถูกขัดจนเงา เธอหวงมันมาก";
        if (evId == "ring_small" || objName.Contains("small"))
            return "แหวนวงเล็ก... ขนาดนิ้วเด็ก ทำไมแหวนของเด็กถึงมาตกอยู่ในที่เกิดเหตุ?";
        if (cls == "ring")
            return "แหวนเงินสลักตราประจำตระกูล... ต้องดูให้ละเอียดกว่านี้";

        return string.IsNullOrEmpty(item.thaiTitle) ? null : item.thaiTitle + "... ต้องจดไว้ก่อน";
    }

    private IEnumerator TriggerEvidenceDeductionRoutine()
    {
        // รอให้ผู้เล่นอ่านความคิดของนิวแมนชิ้นสุดท้ายจบก่อน ค่อยเปิดหน้าสรุปข้อสันนิษฐาน
        yield return new WaitForSeconds(5.5f);

        // Turn off scanner if open
        if (CiraCoreVisionScanner.Instance != null && CiraCoreVisionScanner.Instance.IsScanningActive)
        {
            CiraCoreVisionScanner.Instance.ToggleScanMode(false);
        }

        // ปิดแถบดำภาพยนตร์ออกทันที ไม่ให้มีคร็อปดำบนล่างในหน้าตอบคำถาม เพราะเป็นหน้าสมุดบันทึก ไม่ใช่คัตซีน
        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.HideLetterbox();
        }

        ShowObjectiveHUD(false);
        OpenDeductionBoard();
    }

    public void EnsureUIReferences()
    {
        if (_objectivePanel == null)
        {
            Transform t = transform.Find("ObjectivePanel");
            if (t != null) _objectivePanel = t.gameObject;
        }

        if (_objectivePanel != null)
        {
            var pt = _objectivePanel.transform.Find("ObjProgress");
            if (pt != null) _objectiveProgressText = pt.GetComponent<Text>();
            
            var tt = _objectivePanel.transform.Find("ObjTitle");
            if (tt != null) _objectiveTitleText = tt.GetComponent<Text>();
            
            var ht = _objectivePanel.transform.Find("ObjHint");
            if (ht != null) _hintText = ht.GetComponent<Text>();
        }
    }

    public void BuildQuestUI(bool forceRebuild = false)
    {
        // Canvas setup
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

        if (!forceRebuild)
        {
            EnsureUIReferences();
            if (_objectivePanel != null)
            {
                return;
            }
        }

        // ล้าง UI เก่าถ้ามีอยู่ เพื่อป้องกัน UI ทับซ้อนหรือใช้ฟอนต์เก่าที่ค้างอยู่ในซีน
        Transform oldObj = transform.Find("ObjectivePanel");
        if (oldObj != null)
        {
            if (Application.isPlaying) Destroy(oldObj.gameObject);
            else DestroyImmediate(oldObj.gameObject);
        }

        // สมุดสรุปคดีเล่มเก่า (DeductionPanel) ถูกแทนที่ด้วย CaseDeductionBoardUI แล้ว ลบทิ้งถ้ายังค้างอยู่ในฉาก
        Transform oldDeduct = transform.Find("DeductionPanel");
        if (oldDeduct != null)
        {
            if (Application.isPlaying) Destroy(oldDeduct.gameObject);
            else DestroyImmediate(oldDeduct.gameObject);
        }

        // 1. Objective HUD Panel (Top-Left) - สไตล์ Genshin Impact: ข้อความลอยอิสระ ไร้กล่องทึบบดบัง พร้อม Drop Shadow คมชัด
        _objectivePanel = new GameObject("ObjectivePanel", typeof(RectTransform), typeof(Image));
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
        _objectiveTitleText.fontStyle = FontStyle.Normal;
        _objectiveTitleText.color = VisionOSUITheme.TextPrimary;
        _objectiveTitleText.alignment = TextAnchor.MiddleLeft;
        _objectiveTitleText.text = "<color=#F2C94C>◆ ภารกิจหลัก (เควสที่ 1)</color>  จุดเริ่มต้นแห่งปริศนา: โบสถ์เซนต์แมรี่";
        var tShadow = tGo.AddComponent<Shadow>();
        tShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        tShadow.effectDistance = new Vector2(2f, -2f);

        // Progress / Target Instruction
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
        _objectiveProgressText.text = "• เดินเข้าสู่บริเวณโบสถ์เซนต์แมรี่ (St. Mary Church)";
        var pShadow = pGo.AddComponent<Shadow>();
        pShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        pShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // Controls Hint
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
        _hintText.text = "◆ เมื่อเข้าสู่ที่เกิดเหตุ ให้เปิดโหมดตรวจสอบวัตถุพยาน [คลิกขวา] หรือ [V]";
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
    }

    /// <summary>
    /// ข้อมูลหน้าสรุปข้อสันนิษฐานของบทที่ 1: หลักฐาน 5 ชิ้น + ข้อสันนิษฐาน 3 ขั้นที่ต่อเนื่องกัน
    /// ขั้นที่ 1 เลือกหลักฐานสำคัญ -> ขั้นที่ 2 ตีความแหวนคู่ -> ขั้นที่ 3 เชื่อมโยงไปถึงตัวฆาตกร
    /// </summary>
    private CaseDeductionBoardUI.Config BuildDeductionConfig()
    {
        var config = new CaseDeductionBoardUI.Config
        {
            letterhead = "METROPOLITAN POLICE  ·  CASE FILE 1888/JR  ·  ST. MARY CHURCH",
            openQuestion = "ใครคือผู้ถือแหวนวงเล็กในคืนนี้ และเหตุใดเขาจึงอยากให้เราพบมัน?"
        };

        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("footprint", "รอยเท้า",
            "รอยบู๊ตชาย ก้าวยาวสม่ำเสมอ เดินออกจากแท่นบูชาอย่างไม่รีบร้อน"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("handkerchief", "ผ้าเช็ดหน้า",
            "ปักอักษร M.A. ของผู้ตาย มีคราบน้ำตาแห้ง และเส้นด้ายจากงานตัดเย็บ"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("prayer_sheet", "กระดาษบทสวด",
            "บทขอการอภัยสำหรับแม่ที่ละทิ้งบุตร ถูกพับไว้อย่างระมัดระวัง"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("ring_large", "แหวนวงใหญ่",
            "แหวนเงินสลักตราตระกูล ถูกขัดเงาเสมอ อยู่ในมือผู้ตาย"));
        config.evidence.Add(new CaseDeductionBoardUI.EvidenceCard("ring_small", "แหวนวงเล็ก",
            "แหวนเงินตราเดียวกัน ขนาดเท่านิ้วเด็ก ตกอยู่ห่างจากศพสามก้าว"));

        // ข้อสันนิษฐานที่ 1: เลือกหลักฐานสำคัญ
        var s1 = new CaseDeductionBoardUI.Step
        {
            question = "ในหลักฐานทั้ง 5 ชิ้น สิ่งใดคือเบาะแสสำคัญที่เชื่อมโยงถึงตัวฆาตกร?",
            conclusion = "แหวนเงินสองวงที่สลักตราตระกูลเดียวกัน คือหลักฐานชิ้นเดียวที่ผูกติดกับตัวบุคคล และตามรอยไปถึงต้นตอได้"
        };
        s1.options.Add(new CaseDeductionBoardUI.Option("รอยเท้าบู๊ตชายที่ก้าวออกจากแท่นบูชา", false,
            "รอยเท้าบอกได้แค่ว่าฆาตกรเป็นผู้ชายและเดินอย่างไม่รีบร้อน แต่ยังไม่บอกว่าเขาคือใคร หรือทำไมถึงลงมือ"));
        s1.options.Add(new CaseDeductionBoardUI.Option("ผ้าเช็ดหน้าปักอักษร M.A. ที่มีคราบน้ำตา", false,
            "ผ้าเช็ดหน้าเป็นของแมรี่ แอนเอง เล่าเรื่องของผู้ตาย แต่ไม่ได้ชี้ไปยังตัวคนร้ายเลย"));
        s1.options.Add(new CaseDeductionBoardUI.Option("กระดาษบทสวดขออภัยที่ถูกพับไว้อย่างดี", false,
            "บทสวดบอกจิตใจของผู้ตายก่อนเสียชีวิต แต่ใครก็หยิบมาวางได้ ไม่ผูกกับตัวบุคคลใดโดยเฉพาะ"));
        s1.options.Add(new CaseDeductionBoardUI.Option("แหวนเงิน 2 วง ที่สลักตราตระกูลเดียวกัน", true,
            "นี่คือสิ่งเดียวในที่เกิดเหตุที่มีตราเฉพาะตัวและตามย้อนไปถึงที่มาได้ ไม่ใช่ของที่ใครจะหยิบมาวางเล่น ๆ"));
        config.steps.Add(s1);

        // ข้อสันนิษฐานที่ 2: ตีความแหวนคู่
        var s2 = new CaseDeductionBoardUI.Step
        {
            question = "แหวนทั้งสองวงมีตราและลายสลักเดียวกันทุกเส้น แต่วงหนึ่งใหญ่ อีกวงเล็กเท่านิ้วเด็ก ข้อใดอธิบายได้ดีที่สุด?",
            relatedEvidenceIds = new[] { "ring_large", "ring_small" },
            conclusion = "แหวนทั้งสองเป็นของคู่กันระหว่างแม่กับลูก วงใหญ่คือแมรี่ แอน วงเล็กคือบุตรชายที่เธอจำใจทิ้งไว้เบื้องหลัง"
        };
        s2.options.Add(new CaseDeductionBoardUI.Option("ช่างทำแหวนคนเดียวกันขายแหวนชุดนี้ให้ลูกค้าหลายคน", false,
            "ถ้าเป็นแหวนที่ขายทั่วไป คงไม่สลักตราตระกูลเฉพาะตัว และหญิงช่างตัดเย็บอย่างแมรี่ แอนไม่มีเงินซื้อของแบบนี้"));
        s2.options.Add(new CaseDeductionBoardUI.Option("เป็นแหวนคู่ที่ทำพร้อมกัน วงใหญ่ของผู้ใหญ่ วงเล็กของเด็ก น่าจะเป็นแม่และลูก", true,
            "ตราเดียวกันแต่ขนาดต่างกัน คือแหวนที่ทำไว้ให้คนในสายเลือดเดียวกัน ตรงกับประวัติว่าแมรี่ แอนเคยมีลูกชายและจำใจทิ้งเขาไป"));
        s2.options.Add(new CaseDeductionBoardUI.Option("แมรี่ แอนซื้อแหวนวงเล็กไว้เป็นของขวัญให้คนแปลกหน้า", false,
            "ตราตระกูลไม่ใช่ของที่ซื้อฝากใครได้ และเธอหวงวงใหญ่ของตัวเองมากจนต้องขัดเงาอยู่เสมอ"));
        s2.options.Add(new CaseDeductionBoardUI.Option("แหวนวงเล็กเป็นของปลอมที่ฆาตกรทำเลียนแบบมาวางไว้", false,
            "ลายสลักและความหมองของโลหะเก่าเท่ากับวงใหญ่ เลียนแบบกันข้ามคืนไม่ได้"));
        config.steps.Add(s2);

        // ข้อสันนิษฐานที่ 3: เชื่อมโยงไปถึงตัวฆาตกร
        var s3 = new CaseDeductionBoardUI.Step
        {
            question = "ลูกชายถูกทิ้งไว้เมื่อหลายปีก่อน แต่คืนนี้แหวนวงเล็กอยู่ห่างจากศพเพียงสามก้าว ผู้ตายร้องไห้ ขอการอภัย และไม่มีร่องรอยการต่อสู้ ขณะที่ฆาตกรเดินออกไปอย่างใจเย็น เราสรุปเรื่องฆาตกรได้อย่างไร?",
            relatedEvidenceIds = new[] { "ring_small", "handkerchief", "prayer_sheet", "footprint" },
            conclusion = "ผู้ตายรู้จักฆาตกร ร้องไห้ขออภัยและไม่ต่อสู้ ฆาตกรจงใจทิ้งแหวนวงเล็กไว้ เขาเป็นคนจากอดีตที่เธอทิ้งไว้เบื้องหลัง"
        };
        s3.options.Add(new CaseDeductionBoardUI.Option("เป็นคนแปลกหน้าที่ผ่านมาโดยบังเอิญ", false,
            "คนแปลกหน้าไม่มีทางมีแหวนวงเล็กของลูกชายผู้ตาย และผู้ตายคงไม่ร้องไห้ขออภัยต่อคนที่ไม่รู้จัก"));
        s3.options.Add(new CaseDeductionBoardUI.Option("แมรี่ แอนพกแหวนวงเล็กมาเองแล้วทำตก ไม่เกี่ยวกับฆาตกร", false,
            "ถ้าเธอพกมันเอง เธอต้องกำคู่กับวงใหญ่ ไม่ใช่ให้มันกระเด็นห่างตัว และบทสวดขออภัยบอกว่าเธอกำลังรอใครบางคนอยู่"));
        s3.options.Add(new CaseDeductionBoardUI.Option("เป็นคนจากอดีตของเธอ และจงใจทิ้งแหวนวงเล็กไว้ให้เราเห็น", true,
            "เธอไม่ต่อสู้เพราะรู้ว่าใครมา น้ำตากับบทสวดคือคำขอโทษต่อเขา ส่วนก้าวเดินที่ใจเย็นและแหวนที่วางไว้ แสดงว่านี่คือการจัดฉากโดยคนที่รู้จักเธอดี"));
        s3.options.Add(new CaseDeductionBoardUI.Option("แหวนวงเล็กตกอยู่เฉย ๆ ไม่มีความหมายอะไร", false,
            "ในที่เกิดเหตุที่ถูกจัดวางอย่างตั้งใจ ไม่มีวัตถุชิ้นไหนตกอยู่เฉย ๆ"));
        config.steps.Add(s3);

        return config;
    }

    private CaseDeductionBoardUI.Config _deductionConfig;

    /// <summary>
    /// ตอบข้อสันนิษฐานครบทุกข้อ: -> หน้าสรุปบทที่ 1 -> เริ่มบทที่ 2
    /// </summary>
    private void OnDeductionSolved(CaseDeductionBoardUI.Result result)
    {
        _isDeductionCompleted = true;
        ShowChapterSummary(result);
    }

    private void ShowChapterSummary(CaseDeductionBoardUI.Result result)
    {
        var data = new ChapterSummaryUI.Data
        {
            chapterNumber = 1,
            chapterTitle = "จุดเริ่มต้นแห่งปริศนา",
            location = "โบสถ์เซนต์แมรี่  ·  ไวท์ชาเปล ลอนดอน  ·  ค.ศ. 1888",
            openQuestion = _deductionConfig != null ? _deductionConfig.openQuestion : null,
            nextLead = "มีรายงานพบศพหญิงอีกรายในตรอกมืดไวท์ชาเปล... ฆาตกรยังไม่หยุด และคราวนี้เขาทิ้งข้อความไว้ให้เรา",
            elapsedSeconds = _investigationStartTime > 0f ? Time.time - _investigationStartTime : -1f,
            accuracy = result != null ? result.accuracy : -1
        };

        if (_deductionConfig != null)
        {
            foreach (var step in _deductionConfig.steps) data.chain.Add(step.conclusion);
        }

        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("รอยเท้า", "บู๊ตชาย ก้าวสม่ำเสมอ ไม่รีบร้อน ฆาตกรควบคุมสถานการณ์ได้"));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("ผ้าเช็ดหน้า", "อักษร M.A. คราบน้ำตา ผู้ตายร้องไห้ก่อนเสียชีวิต"));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("กระดาษบทสวด", "บทขอการอภัยสำหรับแม่ผู้ละทิ้งบุตร"));
        data.evidence.Add(new ChapterSummaryUI.EvidenceEntry("แหวนวงใหญ่  +  แหวนวงเล็ก", "ตราตระกูลเดียวกัน แหวนคู่ของแม่และลูก ผูกฆาตกรเข้ากับอดีตของเธอ", true));

        ChapterSummaryUI.Show(data, OnAcceptQuest2);
    }

    /// <summary>
    /// เมื่อผู้เล่นกดปุ่มรับภารกิจเควสที่ 2 (Accept Quest 2)
    /// </summary>
    private void OnAcceptQuest2()
    {
        Debug.Log("<color=green><b>[QUEST 1 COMPLETED]</b> Scene 1 สำเร็จ! ปลดล็อกและรับเควสที่ 2 (ตรอกมืดไวท์ชาเปล) เรียบร้อยแล้ว</color>");
        
        CiraCoreVisionScanner.IsScannerBlocked = false;

        // Lock cursor back to gameplay
        SetCursorLockedState(true);

        // Re-enable player controls
        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.SetPlayerControlsLocked(false);
        }

        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.HideLetterbox();
        }

        // ส่งต่อให้ LondonAlleyQuestManager เริ่มต้น Scene 2 ทันทีพร้อมระบบนำทาง Waypoint
        _isQuest1Finished = true;
        ShowObjectiveHUD(false);

        if (LondonAlleyQuestManager.Instance != null)
        {
            LondonAlleyQuestManager.Instance.StartQuest2();
        }
        else
        {
            // แสดง HUD สำรองสำหรับ Scene 2
            ShowObjectiveHUD(true);
            _objectiveTitleText.text = "<color=#F2C94C>◈ ภารกิจหลัก (เควสที่ 2)</color>  ไล่ล่าฆาตกรสู่ตรอกไวท์ชาเปล";
            _objectiveProgressText.text = "• เดินออกจากประตูโบสถ์เซนต์แมรี่ เพื่อตามรอยหยดเลือดในตรอกลอนดอน";
            _hintText.text = "◈ คดีที่ 1 คลี่คลาย! มุ่งหน้าสู่ตรอกมืดเพื่อเปิดฉาก Scene 2";
        }
    }

    private void UpdateObjectiveDisplay()
    {
        EnsureUIReferences();
        if (_objectiveProgressText != null)
        {
            if (_scannedCount < totalEvidenceRequired)
            {
                _objectiveProgressText.text = $"• ตรวจสอบวัตถุพยานในโบสถ์: <color=#FFD24C><b>{_scannedCount} / {totalEvidenceRequired}</b></color>";
            }
            else
            {
                _objectiveProgressText.text = "• วัตถุพยานครบ 5 ชิ้นแล้ว! <color=#6EE7B7><b>เปิดสมุดบันทึกเพื่อสรุปรูปคดี</b></color>";
            }
        }
    }

    public void ShowObjectiveHUD(bool show)
    {
        EnsureUIReferences();
        if (show)
        {
            if (_isQuest1Finished || (LondonAlleyQuestManager.Instance != null && LondonAlleyQuestManager.Instance.IsQuest2Active) || (RoyalLyceumTheaterQuestManager.Instance != null && RoyalLyceumTheaterQuestManager.Instance.IsQuest3Active))
            {
                if (_objectivePanel != null) _objectivePanel.SetActive(false);
                return;
            }
            // ปิด HUD ของฉากอื่นทันที ป้องกัน UI ซ้อนกัน
            var q2Hud = GameObject.Find("ObjectivePanel_Scene2");
            if (q2Hud != null) q2Hud.SetActive(false);
            var q3Hud = GameObject.Find("Quest3_LyceumTheater_HUD");
            if (q3Hud != null) q3Hud.SetActive(false);
        }
        if (_objectivePanel != null) _objectivePanel.SetActive(show);
    }

    /// <summary>
    /// เปิดหน้าสรุปข้อสันนิษฐานแห่งคดี พร้อมล็อคตัวละครและปลดล็อคเมาส์ให้คลิกตอบคำถามได้ทันที
    /// </summary>
    public void OpenDeductionBoard()
    {
        if (CaseDeductionBoardUI.IsOpen) return;

        // 1. ล็อคการเคลื่อนไหวของผู้เล่น
        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.SetPlayerControlsLocked(true);
        }

        // 2. ปลดล็อคเมาส์ให้เคลื่อนที่อิสระเพื่อตอบคำถาม
        SetCursorLockedState(false);

        // ซ่อน Objective HUD และแถบดำภาพยนตร์ ไม่ให้มีคอปบนล่างหรือเมนูเควสซ้อนกับหน้าสรุป
        ShowObjectiveHUD(false);
        DetectiveThoughtToast.Clear();
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

        if (WaypointNavigationSystem.Instance != null)
        {
            WaypointNavigationSystem.Instance.SetVisible(false);
        }

        // 3. เปิดกระดานสรุปข้อสันนิษฐาน (ซ่อนสมุดเล่มเก่าถ้ายังค้างอยู่ในฉาก)
        var legacy = transform.Find("DeductionPanel");
        if (legacy != null) legacy.gameObject.SetActive(false);

        _deductionConfig = BuildDeductionConfig();
        CaseDeductionBoardUI.Open(_deductionConfig, OnDeductionSolved);
    }

    /// <summary>
    /// ควบคุมสถานะการล็อคเมาส์ ทั้งใน Unity Cursor และ StarterAssetsInputs
    /// </summary>
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
}
