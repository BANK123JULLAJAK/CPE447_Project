using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// ตัวติดตั้งระบบเควส Scene 2: สาส์นจากเงามืด (ตรอกในลอนดอน / London Alley) แบบ 1-Click
/// - ติดตั้งหลักฐาน 5 ชิ้น (กระดุม, ตั๋ว, รอยเท้า, มีด, หนังสือพิมพ์) รองรับ CiRA CORE
/// - ติดตั้งศพเหยื่อหญิงสาวกลางตรอก (Female Victim Corpse) พร้อมกองเลือด
/// - ติดตั้งกล้องคัตซีนภาพยนตร์ 3 ช็อตระดับ Movie Grade
/// - ติดตั้งมินิเกมการถอดรหัสซีซาร์ (Newspaper Decryption Minigame)
/// - ติดตั้งระบบนำทาง Waypoint Navigation System แสดงระยะทางเรียลไทม์
/// </summary>
public static class SetupQuest2LondonAlley
{

    [MenuItem("Tools/🕵️ ติดตั้งเควส Scene 2 (ตรอกลอนดอน + CiRA CORE + คัตซีน + มินิเกมถอดรหัส + ระบบนำทาง)")]
    public static void SetupQuest2()
    {
        Font thaiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Prompt-Medium.ttf")
                     ?? AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Sarabun-Medium.ttf");
        Font thaiFontBold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Prompt-Bold.ttf")
                         ?? AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Sarabun-Bold.ttf");

        // 1. Configure 5 Evidence Items in the Alley
        int configuredCount = 0;

        // Evidence 1: Shirt Button (กระดุมเสื้อโค้ต)
        configuredCount += ConfigureEvidence(
            new string[] { "ALLEY_EVIDENCE_COAT_BUTTON" },
            "shirt_button",
            "shirt_button",
            "กระดุมเสื้อโค้ต (Coat Button)",
            "กระดุมเสื้อโค้ตสั่งตัดพิเศษหลุดตกอยู่บนพื้นหิน สลักตราประจำตระกูลขุนนางอังกฤษชั้นสูง บ่งบอกว่าคนร้ายไม่ใช่คนจรจัดทั่วไป",
            new Vector3(0.5f, 0.4f, 0.5f),
            new Vector3(-17.80f, 0.23f, -102.50f)
        );

        // Evidence 2: Theater Ticket (ตั๋วชมละครเวที)
        configuredCount += ConfigureEvidence(
            new string[] { "ALLEY_EVIDENCE_THEATER_TICKET" },
            "ticket",
            "ticket",
            "ตั๋วชมละครเวที (Theater Ticket)",
            "ตั๋วที่นั่งแถวหน้าสำหรับโรงละครรอยัลไลเซียม (Royal Lyceum Theatre) สำหรับการแสดงคืนนี้! เปื้อนคราบโคลนบางส่วน",
            new Vector3(0.6f, 0.3f, 0.6f),
            new Vector3(-19.05f, 0.23f, -101.90f)
        );

        // Evidence 3: Footprint (รอยเท้าโคลน)
        configuredCount += ConfigureEvidence(
            new string[] { "ALLEY_EVIDENCE_FOOTPRINT", "ALLEY_EVIDENCE_FOOTPRINT.001", "ALLEY_EVIDENCE_FOOTPRINT.002", "ALLEY_EVIDENCE_FOOTPRINT.003", "ALLEY_EVIDENCE_FOOTPRINT.004", "ALLEY_EVIDENCE_FOOTPRINT.005", "ALLEY_EVIDENCE_FOOTPRINT.006" },
            "footprint",
            "footprint",
            "รอยเท้าโคลนของฆาตกร (Muddy Footprint)",
            "รอยฝีเท้าโคลนสดย่ำลึกบนพื้นถนน มุ่งหน้าหนีออกจากร่างเหยื่อเข้าสู่ความมืดของตรอก",
            new Vector3(0.8f, 0.3f, 0.8f),
            new Vector3(-16.00f, 0.23f, -103.00f)
        );

        // Evidence 4: Pocket Knife (มีดพกเปื้อนเลือด)
        configuredCount += ConfigureEvidence(
            new string[] { "ALLEY_EVIDENCE_POCKETKNIFE" },
            "knife",
            "knife",
            "มีดพกเปื้อนคราบเลือด (Bloodstained Pocket Knife)",
            "อาวุธสังหารที่คนร้ายโยนทิ้งไว้ในเงามืด คมมีดมีรอยหยักและมีร่องรอยการเชือดอย่างชำนาญการ",
            new Vector3(0.6f, 0.4f, 0.6f),
            new Vector3(-18.10f, 0.24f, -101.50f)
        );

        // Evidence 5: Newspaper (หนังสือพิมพ์ The Daily Telegraph)
        configuredCount += ConfigureEvidence(
            new string[] { "ALLEY_EVIDENCE_NEWSPAPER" },
            "newspaper",
            "newspaper",
            "หนังสือพิมพ์เปื้อนเลือด (The Daily Telegraph)",
            "หนังสือพิมพ์ฉบับพิเศษที่มีรอยขีดเขียนรหัสลับซีซาร์สีเลือด 'WKHDWHU' วางข้างศพอย่างจงใจ!",
            new Vector3(0.8f, 0.3f, 0.8f),
            new Vector3(-16.80f, 0.24f, -104.16f)
        );

        // 2. Setup or Find Quest 2 System GameObject
        GameObject quest2SystemGo = GameObject.Find("Quest2_LondonAlley_System");
        if (quest2SystemGo == null)
        {
            quest2SystemGo = new GameObject("Quest2_LondonAlley_System");
        }

        // Add / Configure Waypoint Navigation System
        var waypointSystem = quest2SystemGo.GetComponent<WaypointNavigationSystem>();
        if (waypointSystem == null) waypointSystem = quest2SystemGo.AddComponent<WaypointNavigationSystem>();
        if (thaiFont != null) waypointSystem.thaiFont = thaiFont;
        if (thaiFontBold != null) waypointSystem.thaiFontBold = thaiFontBold;

        // Add / Configure Newspaper Decryption Minigame
        var minigame = quest2SystemGo.GetComponent<NewspaperDecryptionMinigame>();
        if (minigame == null) minigame = quest2SystemGo.AddComponent<NewspaperDecryptionMinigame>();
        if (thaiFont != null) minigame.thaiFont = thaiFont;
        if (thaiFontBold != null) minigame.thaiFontBold = thaiFontBold;

        Sprite paperSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/lilydust-old-2012064.jpg");
        if (paperSprite != null) minigame.parchmentPaperSprite = paperSprite;

        // Add / Configure London Alley Quest Manager
        var alleyMgr = quest2SystemGo.GetComponent<LondonAlleyQuestManager>();
        if (alleyMgr == null) alleyMgr = quest2SystemGo.AddComponent<LondonAlleyQuestManager>();
        if (thaiFont != null) alleyMgr.thaiFont = thaiFont;
        if (thaiFontBold != null) alleyMgr.thaiFontBold = thaiFontBold;
        alleyMgr.BuildQuest2HUD();
        EditorUtility.SetDirty(quest2SystemGo);

        // 3. Setup Crime Scene Female Victim Corpse in the Center of the Alley
        GameObject corpseGo = GameObject.Find("Alley_Female_Victim_Corpse");
        if (corpseGo == null)
        {
            // Use MCUnity rig or model for the female victim
            GameObject victimPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MoCapCentral/MC_Sample/Demo/Characters/MCUnity/Prefabs/rig_MCUnity.prefab")
                                   ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D_Model/VAM/CH1/CH2_Newman.prefab");

            if (victimPrefab != null)
            {
                corpseGo = (GameObject)PrefabUtility.InstantiatePrefab(victimPrefab);
                corpseGo.name = "Alley_Female_Victim_Corpse";
            }
            else
            {
                corpseGo = new GameObject("Alley_Female_Victim_Corpse");
            }
        }

        // Position victim in the alley near the newspaper and footprint trail
        corpseGo.transform.position = new Vector3(-18.5f, 0.28f, -102.2f);
        corpseGo.transform.rotation = Quaternion.Euler(0f, 25f, 0f);

        var corpseAnim = corpseGo.GetComponentInChildren<Animator>();
        if (corpseAnim != null)
        {
            var cCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/3D_Model/Animetion_Jack/ctrl_Victim_DeadCorpse_CPR.controller");
            if (cCtrl != null) corpseAnim.runtimeAnimatorController = cCtrl;
        }
        EditorUtility.SetDirty(corpseGo);

        // Add Crime Scene Bloodstain pool underneath victim
        GameObject bloodGo = GameObject.Find("Alley_Victim_Bloodstain");
        if (bloodGo == null)
        {
            bloodGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bloodGo.name = "Alley_Victim_Bloodstain";
            Object.DestroyImmediate(bloodGo.GetComponent<Collider>());
        }
        bloodGo.transform.position = new Vector3(-18.5f, 0.26f, -102.2f);
        bloodGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        bloodGo.transform.localScale = new Vector3(2.5f, 2.5f, 1f);
        var bloodRend = bloodGo.GetComponent<MeshRenderer>();
        if (bloodRend != null)
        {
            var bloodMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bloodMat.name = "Bloodstain_Victim_Mat";
            bloodMat.SetColor("_BaseColor", new Color(0.18f, 0.02f, 0.02f, 0.95f)); // Dark dried blood
            bloodMat.SetFloat("_Smoothness", 0.85f); // Wet specular gloss
            bloodRend.sharedMaterial = bloodMat;
        }
        EditorUtility.SetDirty(bloodGo);

        // 4. Setup Alley Entry Area Trigger
        GameObject triggerGo = GameObject.Find("Alley_Entry_AreaTrigger");
        if (triggerGo == null)
        {
            triggerGo = new GameObject("Alley_Entry_AreaTrigger");
        }
        triggerGo.transform.position = new Vector3(-14.5f, 1.2f, -101.5f);
        BoxCollider box = triggerGo.GetComponent<BoxCollider>();
        if (box == null) box = triggerGo.AddComponent<BoxCollider>();
        box.size = new Vector3(7.0f, 4.0f, 7.0f);
        box.isTrigger = true;
        if (triggerGo.GetComponent<AlleyAreaTrigger>() == null) triggerGo.AddComponent<AlleyAreaTrigger>();
        EditorUtility.SetDirty(triggerGo);

        // 5. Setup Movie Cutscene Cameras for the Alley (3 Shots)
        // Shot 1: Tracking Shot along the foggy alley
        GameObject camTrackGo = GameObject.Find("Cutscene_Cam_AlleyTracking");
        if (camTrackGo == null) camTrackGo = new GameObject("Cutscene_Cam_AlleyTracking");
        camTrackGo.transform.position = new Vector3(-11.5f, 2.5f, -101.5f);
        camTrackGo.transform.rotation = Quaternion.Euler(5f, -90f, 0f);
        var cmTrack = camTrackGo.GetComponent<CinemachineCamera>();
        if (cmTrack == null) cmTrack = camTrackGo.AddComponent<CinemachineCamera>();
        cmTrack.Priority.Value = 0;
        cmTrack.Lens.FieldOfView = 38f;
        cmTrack.Lens.NearClipPlane = 0.15f;
        EditorUtility.SetDirty(camTrackGo);

        // Shot 2: Close-up on Newman & Victim Corpse
        GameObject camCorpseGo = GameObject.Find("Cutscene_Cam_AlleyCorpseClose");
        if (camCorpseGo == null) camCorpseGo = new GameObject("Cutscene_Cam_AlleyCorpseClose");
        camCorpseGo.transform.position = new Vector3(-16.8f, 1.4f, -100.8f);
        camCorpseGo.transform.rotation = Quaternion.Euler(22f, -125f, 0f);
        var cmCorpse = camCorpseGo.GetComponent<CinemachineCamera>();
        if (cmCorpse == null) cmCorpse = camCorpseGo.AddComponent<CinemachineCamera>();
        cmCorpse.Priority.Value = 0;
        cmCorpse.Lens.FieldOfView = 32f;
        cmCorpse.Lens.NearClipPlane = 0.15f;
        EditorUtility.SetDirty(camCorpseGo);

        // Shot 3: Macro Push on Mysterious Newspaper
        GameObject camNewsGo = GameObject.Find("Cutscene_Cam_AlleyNewspaperMacro");
        if (camNewsGo == null) camNewsGo = new GameObject("Cutscene_Cam_AlleyNewspaperMacro");
        camNewsGo.transform.position = new Vector3(-16.80f, 0.95f, -103.4f);
        camNewsGo.transform.rotation = Quaternion.Euler(52f, -165f, 0f);
        var cmNews = camNewsGo.GetComponent<CinemachineCamera>();
        if (cmNews == null) cmNews = camNewsGo.AddComponent<CinemachineCamera>();
        cmNews.Priority.Value = 0;
        cmNews.Lens.FieldOfView = 28f;
        cmNews.Lens.NearClipPlane = 0.10f;
        EditorUtility.SetDirty(camNewsGo);

        // Player Follow Cam
        GameObject followCamGo = GameObject.Find("PlayerFollowCamera");
        CinemachineCamera playerCmCam = followCamGo != null ? followCamGo.GetComponent<CinemachineCamera>() : null;

        // Assign to Alley Quest Manager
        alleyMgr.camAlleyTracking = cmTrack;
        alleyMgr.camAlleyCorpseClose = cmCorpse;
        alleyMgr.camAlleyNewspaperMacro = cmNews;
        alleyMgr.playerFollowCamera = playerCmCam;
        alleyMgr.femaleVictimCorpse = corpseGo;

        // Assign to Cinematic Cutscene Manager
        var cutsceneMgr = Object.FindAnyObjectByType<CinematicCutsceneManager>(FindObjectsInactive.Include);
        if (cutsceneMgr != null)
        {
            cutsceneMgr.camAlleyTracking = cmTrack;
            cutsceneMgr.camAlleyCorpseClose = cmCorpse;
            cutsceneMgr.camAlleyNewspaperMacro = cmNews;
            EditorUtility.SetDirty(cutsceneMgr);
        }

        var newspaperObj = GameObject.Find("ALLEY_EVIDENCE_NEWSPAPER");
        if (newspaperObj != null) alleyMgr.newspaperEvidenceGo = newspaperObj;

        var alleyObj = GameObject.Find("ALLEY") ?? corpseGo;
        if (alleyObj != null) alleyMgr.alleyCenterTarget = alleyObj.transform;

        var theaterObj = GameObject.Find("THEATER");
        if (theaterObj != null) alleyMgr.theaterTarget = theaterObj.transform;

        // Waypoint initial target points to Church (Scene 1 starts first)
        var churchTarget = GameObject.Find("Church_Entry_AreaTrigger") ?? GameObject.Find("CHURCH");
        if (churchTarget != null)
        {
            waypointSystem.SetTarget(churchTarget.transform, "โบสถ์เซนต์แมรี่ (St. Mary Church)");
        }
        else
        {
            waypointSystem.ClearTarget();
        }

        EditorUtility.SetDirty(quest2SystemGo);
        EditorUtility.SetDirty(alleyMgr);
        EditorUtility.SetDirty(waypointSystem);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"<color=green><b>[SUCCESS]</b> ติดตั้งระบบเควส Scene 2 (ตรอกไวท์ชาเปล) สำเร็จ 100%! พบหลักฐาน {configuredCount} ชิ้น, ศพเหยื่อ, คัตซีน 3 ช็อต, มินิเกมถอดรหัสซีซาร์, และระบบนำทาง Waypoint เรียบร้อย</color>");
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

        // หากยังไม่มีวัตถุพยานในฉาก ให้สร้างขึ้นมาอัตโนมัติรอบๆ ศพเหยื่อในตรอก
        if (targetGo == null)
        {
            targetGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            targetGo.name = candidateNames[0];
            targetGo.transform.position = defaultSpawnPos;
            targetGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // ปรับขนาดและ Material ของวัตถุพยานให้สมจริง
            var rend = targetGo.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = $"{id}_Mat";

                if (id == "shirt_button")
                {
                    targetGo.transform.localScale = new Vector3(0.18f, 0.18f, 1f);
                    mat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.90f, 1f));
                    mat.SetFloat("_Metallic", 0.95f);
                    mat.SetFloat("_Smoothness", 0.90f);
                }
                else if (id == "ticket")
                {
                    targetGo.transform.localScale = new Vector3(0.28f, 0.16f, 1f);
                    mat.SetColor("_BaseColor", new Color(0.95f, 0.88f, 0.72f, 1f)); // Antique amber paper
                    mat.SetFloat("_Smoothness", 0.2f);
                }
                else if (id == "footprint")
                {
                    targetGo.transform.localScale = new Vector3(0.38f, 0.22f, 1f);
                    mat.SetColor("_BaseColor", new Color(0.12f, 0.08f, 0.05f, 0.95f)); // Dark mud
                    mat.SetFloat("_Smoothness", 0.6f);
                }
                else if (id == "knife")
                {
                    targetGo.transform.localScale = new Vector3(0.32f, 0.10f, 1f);
                    mat.SetColor("_BaseColor", new Color(0.55f, 0.15f, 0.15f, 1f)); // Bloodstained blade
                    mat.SetFloat("_Metallic", 0.85f);
                    mat.SetFloat("_Smoothness", 0.80f);
                }
                else if (id == "newspaper")
                {
                    targetGo.transform.localScale = new Vector3(0.55f, 0.40f, 1f);
                    mat.SetColor("_BaseColor", new Color(0.92f, 0.86f, 0.74f, 1f)); // Vintage newsprint
                    mat.SetFloat("_Smoothness", 0.15f);
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
