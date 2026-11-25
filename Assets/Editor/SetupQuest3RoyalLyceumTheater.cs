using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// ตัวติดตั้งระบบเควส Scene 3: เฉียดฉิว (หน้าโรงละครรอยัลไลเซียม / In Front of The Royal Lyceum Theatre) แบบ 1-Click
/// - บรรยากาศ: บริเวณถนนและบันไดหน้าทางเข้าโรงละครรอยัลไลเซียม มีร่องรอยการต่อสู้อย่างดุเดือด
/// - บล็อกทริกเกอร์หน้าโรงละคร (Cutscene Trigger Block) บนถนน ให้ผู้เล่นเดินเข้ามาเพื่อเริ่มคัตซีนอัตโนมัติ
/// - ติดตั้งหลักฐาน 5 ชิ้น (หน้ากาก, ไม้เท้า, เศษผ้าชุดนักแสดง, จดหมายขู่, และ "ตั๋วรถไฟที่ฉีกขาด") รองรับ CiRA CORE
/// - ติดตั้งเหยื่อสาวผู้รอดชีวิต (Surviving Victim) และฆาตกร Jack The Ripper พร้อมมีดเปื้อนเลือดที่บันไดหน้าทางเข้า
/// - ติดตั้งกล้องคัตซีนภาพยนตร์ 4 ช็อตระดับ Movie Grade
/// - ติดตั้งมินิเกมแผนที่เส้นทางรถไฟลอนดอน (London Railway Map Puzzle Minigame)
/// </summary>
[InitializeOnLoad]
public static class SetupQuest3RoyalLyceumTheater
{
    static SetupQuest3RoyalLyceumTheater()
    {
        EditorApplication.delayCall += AutoSetupOnce;
    }

    private static void AutoSetupOnce()
    {
        if (!SessionState.GetBool("Quest3SetupAutoExecuted", false))
        {
            SessionState.SetBool("Quest3SetupAutoExecuted", true);
            SetupQuest3();
        }
    }

    [MenuItem("Tools/🎭 ติดตั้งเควส Scene 3 (โรงละครรอยัลไลเซียม + คัตซีนเฉียดฉิว Jack + สแกนตั๋วฉีกขาด + มินิเกมแผนที่รถไฟลอนดอน)")]
    public static void SetupQuest3()
    {
        Font thaiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Prompt-Medium.ttf")
                     ?? AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Sarabun-Medium.ttf");
        Font thaiFontBold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Prompt-Bold.ttf")
                         ?? AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Sarabun-Bold.ttf");

        // 1. Configure 5 Evidence Items in front of the Royal Lyceum Theatre
        int configuredCount = 0;

        // Evidence 1: Theater Mask (หน้ากากละครเวทีแตกหัก) - บนทางเท้าถนน
        configuredCount += ConfigureEvidence(
            new string[] { "THEATER_EVIDENCE_THEATERMASK" },
            "theater_mask",
            "theater_mask",
            "หน้ากากละครเวทีแตกหัก (Broken Theater Mask)",
            "หน้ากากละครเวทีวิกตอเรียสีงาช้างแตกหักเป็นสองเสี่ยง ตกอยู่บนทางเท้าถนนหน้าโรงละคร มีรอยเหยียบกระแทกจากการต่อสู้",
            new Vector3(0.6f, 0.4f, 0.6f),
            new Vector3(21.49f, 0.24f, -93.98f)
        );

        // Evidence 2: Walking Cane (ไม้เท้าทองเหลืองหรูหราหัก) - ริมทางเท้า
        configuredCount += ConfigureEvidence(
            new string[] { "THEATER_EVIDENCE_WALKINGCANE" },
            "walking_cane",
            "walking_cane",
            "ไม้เท้าทองเหลืองหรูหรา (Victorian Walking Cane)",
            "ไม้เท้าไม้เนื้อแข็งหัวสิงโตทองเหลืองสั่งทำพิเศษ ตกอยู่ริมทางเท้าก่อนถึงประตูโรงละคร ถูกกระแทกจนหักครึ่ง",
            new Vector3(0.6f, 0.4f, 0.8f),
            new Vector3(21.64f, 0.24f, -92.90f)
        );

        // Evidence 3: Actor Costume Scrap (เศษผ้าชุดนักแสดงฉีกขาด) - บนถนนหินหน้าโรงละคร
        configuredCount += ConfigureEvidence(
            new string[] { "THEATER_EVIDENCE_ACTORCOSTUMESCRAP" },
            "actor_costume",
            "actor_costume",
            "เศษผ้าชุดนักแสดงฉีกขาด (Torn Velvet Costume Scrap)",
            "เศษผ้ากำมะหยี่สีน้ำเงินขลิบทองของชุดนักแสดงสาว ตกอยู่กลางถนนหน้าโรงละคร มีรอยฉีกขาดจากคมมีดอย่างชัดเจน",
            new Vector3(0.6f, 0.3f, 0.6f),
            new Vector3(28.65f, 0.24f, -85.84f)
        );

        // Evidence 4: Backstage Warning Note (จดหมายขู่ลับหลังเวที) - ทางเดินหน้าโรงละคร
        configuredCount += ConfigureEvidence(
            new string[] { "THEATER_EVIDENCE_PRIVATEMESSAGE" },
            "private_message",
            "private_message",
            "จดหมายขู่ลับ (Threatening Note)",
            "กระดาษโน้ตเปื้อนคราบหมึกตกอยู่บนพื้น ข้อความเตือนถึง 'ความตายที่จะมาเยือนเมื่อม่านการแสดงปิดลง'",
            new Vector3(0.6f, 0.3f, 0.6f),
            new Vector3(24.27f, 0.24f, -84.55f)
        );

        // Evidence 5: Torn Railway Ticket (ตั๋วรถไฟที่ฉีกขาด - วัตถุพยานสำคัญที่สุด!) - หน้าบันไดทางเข้าโรงละคร
        configuredCount += ConfigureEvidence(
            new string[] { "THEATER_EVIDENCE_TORN_TICKET", "THEATER_EVIDENCE_PERFORMENCEINVITATION" },
            "torn_railway_ticket",
            "ticket",
            "ตั๋วรถไฟที่ฉีกขาด (Torn Railway Ticket - วัตถุพยานสำคัญ)",
            "ตั๋วรถไฟลอนดอนรอบดึก 23:45 น. ที่ Jack The Ripper ทำตกไว้หน้าบันไดโรงละครขณะสะบัดตัวหนี ปลายทางเหลือคำว่า '...ING'S CR...' และตราประทับ Circle Line!",
            new Vector3(0.6f, 0.3f, 0.6f),
            new Vector3(31.50f, 0.25f, -87.76f)
        );

        // 2. Setup or Find Quest 3 System GameObject
        GameObject quest3SystemGo = GameObject.Find("Quest3_RoyalLyceumTheater_System");
        if (quest3SystemGo == null)
        {
            quest3SystemGo = new GameObject("Quest3_RoyalLyceumTheater_System");
        }

        // Canvas & Raycaster Setup
        var canvas = quest3SystemGo.GetComponent<Canvas>();
        if (canvas == null) canvas = quest3SystemGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = quest3SystemGo.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = quest3SystemGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var raycaster = quest3SystemGo.GetComponent<GraphicRaycaster>();
        if (raycaster == null) raycaster = quest3SystemGo.AddComponent<GraphicRaycaster>();

        // Add / Configure London Railway Map Minigame
        var railwayMinigame = quest3SystemGo.GetComponent<LondonRailwayMapMinigame>();
        if (railwayMinigame == null) railwayMinigame = quest3SystemGo.AddComponent<LondonRailwayMapMinigame>();
        if (thaiFont != null) railwayMinigame.thaiFont = thaiFont;
        if (thaiFontBold != null) railwayMinigame.thaiFontBold = thaiFontBold;

        Sprite paperSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/lilydust-old-2012064.jpg");
        if (paperSprite != null) railwayMinigame.parchmentPaperSprite = paperSprite;

        // Add / Configure Royal Lyceum Theater Quest Manager
        var theaterMgr = quest3SystemGo.GetComponent<RoyalLyceumTheaterQuestManager>();
        if (theaterMgr == null) theaterMgr = quest3SystemGo.AddComponent<RoyalLyceumTheaterQuestManager>();
        if (thaiFont != null) theaterMgr.thaiFont = thaiFont;
        if (thaiFontBold != null) theaterMgr.thaiFontBold = thaiFontBold;
        theaterMgr.BuildQuest3HUD();

        // 3. Setup Trashed Theater Atmosphere & Props in Front of Theater
        SetupTheaterStruggleProps();

        // 4. Setup Surviving Victim (นักแสดงสาวผู้รอดชีวิต) - ยืนประจันหน้าโดนบีบคอที่บันไดหน้าโรงละคร
        GameObject victimGo = GameObject.Find("Theater_Surviving_Victim");
        if (victimGo == null)
        {
            GameObject victimPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MoCapCentral/MC_Sample/Demo/Characters/MCUnity/Prefabs/rig_MCUnity.prefab")
                                   ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D_Model/VAM/CH1/CH2_Newman.prefab");

            if (victimPrefab != null)
            {
                victimGo = (GameObject)PrefabUtility.InstantiatePrefab(victimPrefab);
                victimGo.name = "Theater_Surviving_Victim";
            }
            else
            {
                victimGo = new GameObject("Theater_Surviving_Victim");
            }
        }

        // ตำแหน่งเหยื่อสาว: ยืนตรงข้ามกับแจ็ค โดนบีบคอลอยขึ้นกลางอากาศ
        victimGo.transform.position = new Vector3(31.35f, 0.26f, -87.40f);
        victimGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // หันหน้าเข้าหาแจ็ค

        // ติดตั้ง Animator ท่าโดนบีบคอ (Being Strangled) และนอนหมดสติ (Receiving CPR)
        var victimAnim = victimGo.GetComponentInChildren<Animator>();
        if (victimAnim != null)
        {
            var vCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/3D_Model/Animetion_Jack/ctrl_Victim_StrangledAndCPR.controller");
            if (vCtrl != null) victimAnim.runtimeAnimatorController = vCtrl;
        }
        EditorUtility.SetDirty(victimGo);

        // 5. Setup Jack The Ripper Actor (โมเดล Jack จาก Assets/3D_Model/VAM/CH2/Jack.prefab)
        GameObject jackGo = GameObject.Find("Theater_Jack_The_Ripper");
        GameObject jackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D_Model/VAM/CH2/Jack.prefab");

        // ตรวจสอบว่าเป็นโมเดล Jack ตัวจริงจาก CH2 หรือไม่ ถ้าไม่ใช่ให้เปลี่ยนเป็นตัวจริง
        bool needRecreateJack = (jackGo == null);
        if (jackGo != null && jackGo.GetComponentInChildren<Animator>()?.avatar?.name != "VrmAvatar")
        {
            needRecreateJack = true;
            Object.DestroyImmediate(jackGo);
            jackGo = null;
        }

        if (needRecreateJack && jackPrefab != null)
        {
            jackGo = (GameObject)PrefabUtility.InstantiatePrefab(jackPrefab);
            jackGo.name = "Theater_Jack_The_Ripper";
        }
        else if (jackGo == null)
        {
            jackGo = new GameObject("Theater_Jack_The_Ripper");
        }

        jackGo.transform.position = new Vector3(32.10f, 0.26f, -87.40f);
        jackGo.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // หันหน้าออกไปยังถนนและบันได
        jackGo.transform.localScale = Vector3.one;

        // ติดตั้ง Animator ท่าบีบคอยกตัวเหยื่อ (Superhuman Choke Lift)
        var jackAnim = jackGo.GetComponentInChildren<Animator>();
        if (jackAnim != null)
        {
            var jCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/3D_Model/Animetion_Jack/ctrl_Jack_SuperhumanChoke.controller");
            if (jCtrl != null) jackAnim.runtimeAnimatorController = jCtrl;
        }

        jackGo.SetActive(true);
        EditorUtility.SetDirty(jackGo);

        // 6. Setup Theater Area Cutscene Trigger Block (บล็อกวางหน้าโรงละครบนถนน)
        GameObject triggerGo = GameObject.Find("Theater_Entry_AreaTrigger");
        if (triggerGo == null)
        {
            triggerGo = new GameObject("Theater_Entry_AreaTrigger");
        }
        // วางบล็อกบนถนนหน้าโรงละคร (เชื่อมต่อจากถนนเข้าสู่บันได)
        triggerGo.transform.position = new Vector3(26.0f, 1.2f, -87.76f);
        BoxCollider box = triggerGo.GetComponent<BoxCollider>();
        if (box == null) box = triggerGo.AddComponent<BoxCollider>();
        box.size = new Vector3(7.5f, 3.5f, 9.0f);
        box.isTrigger = true;
        if (triggerGo.GetComponent<TheaterAreaTrigger>() == null) triggerGo.AddComponent<TheaterAreaTrigger>();

        // สร้างบล็อกแสดงอาณาเขตพื้นที่หน้าโรงละคร (Visible Investigation Zone Marker)
        GameObject visualBlock = GameObject.Find("Theater_Cutscene_Trigger_Block_Visual");
        if (visualBlock == null)
        {
            visualBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visualBlock.name = "Theater_Cutscene_Trigger_Block_Visual";
            visualBlock.transform.SetParent(triggerGo.transform, false);
            visualBlock.transform.localPosition = new Vector3(0f, -1.05f, 0f);
            visualBlock.transform.localScale = new Vector3(1.0f, 0.05f, 1.0f);
            Object.DestroyImmediate(visualBlock.GetComponent<Collider>());

            var vRend = visualBlock.GetComponent<MeshRenderer>();
            if (vRend != null)
            {
                var vMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                vMat.name = "Theater_TriggerZone_Mat";
                vMat.SetColor("_BaseColor", new Color(0.95f, 0.75f, 0.20f, 0.35f)); // Golden crime scene zone
                vMat.SetFloat("_Smoothness", 0.5f);
                vRend.sharedMaterial = vMat;
            }
        }
        EditorUtility.SetDirty(triggerGo);

        // 7. Setup Movie Cutscene Cameras (4 Shots in Front of Theater)
        // Shot 1: Approaching the Theater Front from Street
        GameObject camBreachGo = GameObject.Find("Cutscene_Cam_TheaterWideBreach");
        if (camBreachGo == null) camBreachGo = new GameObject("Cutscene_Cam_TheaterWideBreach");
        camBreachGo.transform.position = new Vector3(20.5f, 2.2f, -86.5f);
        camBreachGo.transform.rotation = Quaternion.Euler(5f, 85f, 0f);
        var cmBreach = camBreachGo.GetComponent<CinemachineCamera>();
        if (cmBreach == null) cmBreach = camBreachGo.AddComponent<CinemachineCamera>();
        cmBreach.Priority.Value = 0;
        cmBreach.Lens.FieldOfView = 45f;
        cmBreach.Lens.NearClipPlane = 0.15f;
        EditorUtility.SetDirty(camBreachGo);

        // Shot 2: Jack Confrontation at Entrance Steps
        GameObject camJackGo = GameObject.Find("Cutscene_Cam_TheaterJackConfrontation");
        if (camJackGo == null) camJackGo = new GameObject("Cutscene_Cam_TheaterJackConfrontation");
        camJackGo.transform.position = new Vector3(28.5f, 1.35f, -86.8f);
        camJackGo.transform.rotation = Quaternion.Euler(6f, 80f, 0f);
        var cmJack = camJackGo.GetComponent<CinemachineCamera>();
        if (cmJack == null) cmJack = camJackGo.AddComponent<CinemachineCamera>();
        cmJack.Priority.Value = 0;
        cmJack.Lens.FieldOfView = 32f;
        cmJack.Lens.NearClipPlane = 0.15f;
        EditorUtility.SetDirty(camJackGo);

        // Shot 3: Victim Rescue at Theater Front Steps
        GameObject camVictimGo = GameObject.Find("Cutscene_Cam_TheaterVictimRescue");
        if (camVictimGo == null) camVictimGo = new GameObject("Cutscene_Cam_TheaterVictimRescue");
        camVictimGo.transform.position = new Vector3(29.2f, 1.2f, -88.5f);
        camVictimGo.transform.rotation = Quaternion.Euler(12f, 65f, 0f);
        var cmVictim = camVictimGo.GetComponent<CinemachineCamera>();
        if (cmVictim == null) cmVictim = camVictimGo.AddComponent<CinemachineCamera>();
        cmVictim.Priority.Value = 0;
        cmVictim.Lens.FieldOfView = 35f;
        cmVictim.Lens.NearClipPlane = 0.15f;
        EditorUtility.SetDirty(camVictimGo);

        // Shot 4: Torn Ticket Macro on Steps
        GameObject camTicketGo = GameObject.Find("Cutscene_Cam_TheaterTicketMacro");
        if (camTicketGo == null) camTicketGo = new GameObject("Cutscene_Cam_TheaterTicketMacro");
        camTicketGo.transform.position = new Vector3(30.4f, 0.85f, -87.2f);
        camTicketGo.transform.rotation = Quaternion.Euler(48f, 70f, 0f);
        var cmTicket = camTicketGo.GetComponent<CinemachineCamera>();
        if (cmTicket == null) cmTicket = camTicketGo.AddComponent<CinemachineCamera>();
        cmTicket.Priority.Value = 0;
        cmTicket.Lens.FieldOfView = 26f;
        cmTicket.Lens.NearClipPlane = 0.10f;
        EditorUtility.SetDirty(camTicketGo);

        // Player Follow Cam
        GameObject followCamGo = GameObject.Find("PlayerFollowCamera");
        CinemachineCamera playerCmCam = followCamGo != null ? followCamGo.GetComponent<CinemachineCamera>() : null;

        // Assign to RoyalLyceumTheaterQuestManager
        theaterMgr.camTheaterWideBreach = cmBreach;
        theaterMgr.camTheaterJackConfrontation = cmJack;
        theaterMgr.camTheaterVictimRescue = cmVictim;
        theaterMgr.camTheaterTicketMacro = cmTicket;
        theaterMgr.playerFollowCamera = playerCmCam;
        theaterMgr.survivingVictimGo = victimGo;
        theaterMgr.jackTheRipperGo = jackGo;

        var tornTicketObj = GameObject.Find("THEATER_EVIDENCE_PERFORMENCEINVITATION") ?? GameObject.Find("THEATER_EVIDENCE_TORN_TICKET");
        if (tornTicketObj != null) theaterMgr.tornTicketEvidenceGo = tornTicketObj;

        var theaterCenter = GameObject.Find("THEATER") ?? victimGo;
        if (theaterCenter != null) theaterMgr.theaterCenterTarget = theaterCenter.transform;

        // Assign to Cinematic Cutscene Manager
        var cutsceneMgr = Object.FindAnyObjectByType<CinematicCutsceneManager>(FindObjectsInactive.Include);
        if (cutsceneMgr != null)
        {
            cutsceneMgr.camTheaterWideBreach = cmBreach;
            cutsceneMgr.camTheaterJackConfrontation = cmJack;
            cutsceneMgr.camTheaterVictimRescue = cmVictim;
            cutsceneMgr.camTheaterTicketMacro = cmTicket;
            EditorUtility.SetDirty(cutsceneMgr);
        }

        // Setup Scene 4 Target Marker (Train Station)
        GameObject trainTargetGo = GameObject.Find("TRAINSTATION_BENCH") ?? GameObject.Find("TrainStation_Entry_Marker");
        if (trainTargetGo == null)
        {
            trainTargetGo = new GameObject("TrainStation_Entry_Marker");
            trainTargetGo.transform.position = new Vector3(43.56f, 0.5f, 41.17f);
        }
        theaterMgr.trainStationTarget = trainTargetGo.transform;

        EditorUtility.SetDirty(quest3SystemGo);
        EditorUtility.SetDirty(theaterMgr);
        EditorUtility.SetDirty(railwayMinigame);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"<color=green><b>[SUCCESS]</b> ติดตั้งระบบเควส Scene 3 (หน้าโรงละครรอยัลไลเซียม) สำเร็จ 100%! บล็อกทริกเกอร์วางไว้บนถนนหน้าบันไดโรงละคร, พบหลักฐาน {configuredCount} ชิ้น, คัตซีน 4 ช็อต, เหยื่อผู้รอดชีวิต, ฆาตกร Jack, และมินิเกมแผนที่รถไฟลอนดอน เรียบร้อย</color>");
    }

    private static void SetupTheaterStruggleProps()
    {
        // Struggle Marks on Steps/Pavement
        GameObject struggleGo = GameObject.Find("Theater_Struggle_Marks");
        if (struggleGo == null)
        {
            struggleGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            struggleGo.name = "Theater_Struggle_Marks";
            Object.DestroyImmediate(struggleGo.GetComponent<Collider>());
        }
        struggleGo.transform.position = new Vector3(30.5f, 0.25f, -87.5f);
        struggleGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        struggleGo.transform.localScale = new Vector3(3.0f, 3.0f, 1f);
        var sRend = struggleGo.GetComponent<MeshRenderer>();
        if (sRend != null)
        {
            var sMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            sMat.name = "Theater_Struggle_Mat";
            sMat.SetColor("_BaseColor", new Color(0.22f, 0.03f, 0.03f, 0.85f));
            sMat.SetFloat("_Smoothness", 0.7f);
            sRend.sharedMaterial = sMat;
        }

        // Overturned theater prop / debris on street
        GameObject chairGo = GameObject.Find("Theater_Overturned_Chair_Prop");
        if (chairGo == null)
        {
            chairGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chairGo.name = "Theater_Overturned_Chair_Prop";
            Object.DestroyImmediate(chairGo.GetComponent<Collider>());
        }
        chairGo.transform.position = new Vector3(29.8f, 0.35f, -86.8f);
        chairGo.transform.rotation = Quaternion.Euler(35f, 65f, -40f);
        chairGo.transform.localScale = new Vector3(0.55f, 0.65f, 0.55f);
        var cRend = chairGo.GetComponent<MeshRenderer>();
        if (cRend != null)
        {
            var cMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            cMat.name = "Theater_Chair_Velvet_Mat";
            cMat.SetColor("_BaseColor", new Color(0.48f, 0.08f, 0.12f));
            cMat.SetFloat("_Smoothness", 0.25f);
            cRend.sharedMaterial = cMat;
        }

        // Dramatic Spotlight on Theater Front
        GameObject lightGo = GameObject.Find("Theater_Dramatic_Spotlight");
        if (lightGo == null)
        {
            lightGo = new GameObject("Theater_Dramatic_Spotlight");
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1.0f, 0.88f, 0.65f); // Warm theatre spotlight
            l.intensity = 20.0f;
            l.range = 16.0f;
            l.spotAngle = 45.0f;
        }
        lightGo.transform.position = new Vector3(28.0f, 5.5f, -86.0f);
        lightGo.transform.rotation = Quaternion.Euler(55f, 85f, 0f);
    }

    private static int ConfigureEvidence(string[] candidateNames, string id, string ciraClass, string title, string desc, Vector3 colliderSize, Vector3 defaultSpawnPos)
    {
        int count = 0;
        GameObject targetGo = null;

        foreach (var name in candidateNames)
        {
            targetGo = GameObject.Find(name);
            if (targetGo == null)
            {
                var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
                foreach (var t in allTransforms)
                {
                    if (t.name == name) { targetGo = t.gameObject; break; }
                }
            }

            if (targetGo != null) break;
        }

        if (targetGo == null)
        {
            targetGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            targetGo.name = candidateNames[0];
            targetGo.transform.position = defaultSpawnPos;
            targetGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var rend = targetGo.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = $"{id}_Mat";

                if (id == "theater_mask")
                {
                    targetGo.transform.localScale = new Vector3(0.35f, 0.45f, 1f);
                    mat.SetColor("_BaseColor", new Color(0.92f, 0.90f, 0.82f));
                    mat.SetFloat("_Smoothness", 0.4f);
                }
                else if (id == "walking_cane")
                {
                    targetGo.transform.localScale = new Vector3(0.55f, 0.12f, 1f);
                    mat.SetColor("_BaseColor", new Color(0.75f, 0.60f, 0.20f));
                    mat.SetFloat("_Metallic", 0.9f);
                    mat.SetFloat("_Smoothness", 0.85f);
                }
                else if (id == "actor_costume")
                {
                    targetGo.transform.localScale = new Vector3(0.40f, 0.35f, 1f);
                    mat.SetColor("_BaseColor", new Color(0.12f, 0.25f, 0.55f));
                    mat.SetFloat("_Smoothness", 0.2f);
                }
                else if (id == "private_message")
                {
                    targetGo.transform.localScale = new Vector3(0.32f, 0.24f, 1f);
                    mat.SetColor("_BaseColor", new Color(0.95f, 0.90f, 0.78f));
                    mat.SetFloat("_Smoothness", 0.15f);
                }
                else if (id == "torn_railway_ticket")
                {
                    targetGo.transform.localScale = new Vector3(0.28f, 0.18f, 1f);
                    mat.SetColor("_BaseColor", new Color(0.95f, 0.86f, 0.70f));
                    mat.SetFloat("_Smoothness", 0.25f);
                }

                rend.sharedMaterial = mat;
            }
        }

        if (targetGo != null)
        {
            var box = targetGo.GetComponent<BoxCollider>();
            if (box == null) box = targetGo.AddComponent<BoxCollider>();

            Vector3 lossy = targetGo.transform.lossyScale;
            box.size = new Vector3(
                colliderSize.x / Mathf.Max(0.0001f, lossy.x),
                colliderSize.y / Mathf.Max(0.0001f, lossy.y),
                colliderSize.z / Mathf.Max(0.0001f, lossy.z)
            );
            box.center = Vector3.zero;
            box.isTrigger = false;

            var evidence = targetGo.GetComponent<EvidenceItem>();
            if (evidence == null) evidence = targetGo.AddComponent<EvidenceItem>();
            evidence.evidenceId = id;
            evidence.ciraClassName = ciraClass;
            evidence.thaiTitle = title;
            evidence.thaiDescription = desc;
            evidence.detectionRange = 12f;
            evidence.isScanned = false;
            EditorUtility.SetDirty(targetGo);
            count++;
        }

        return count;
    }
}
