using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// ตัวติดตั้งระบบเควส Scene 1: จุดเริ่มต้นแห่งปริศนา (โบสถ์เซนต์แมรี่ / St. Mary Church)
/// 1-Click ติดตั้ง CiRA CORE Scanner, Cinematic Letterbox, Thai Font, วัตถุพยาน 5 ชิ้น,
/// และกล้องคัตซีนภาพยนตร์ 3 มุมมองระดับ Movie Grade พร้อมแก้ปัญหากล้องผู้เล่นจมพื้น
/// </summary>
public static class SetupQuest1StMaryChurch
{

    [MenuItem("Tools/⛪ ติดตั้งเควส Scene 1 (โบสถ์เซนต์แมรี่ + CiRA CORE + คัตซีนภาพยนตร์ + ฟอนต์ไทย)")]
    public static void SetupQuest1()
    {
        // โหลดฟอนต์ Static TTF ที่คมชัดสำหรับภาษาไทย (Prompt / Sarabun) ป้องกันปัญหา Variable Font ตัวอักษรทับซ้อน
        Font thaiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Prompt-Medium.ttf")
                     ?? AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Sarabun-Medium.ttf");
        Font thaiFontBold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Prompt-Bold.ttf")
                         ?? AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Sarabun-Bold.ttf");
        if (thaiFont == null)
        {
            Debug.LogWarning("กำลังใช้ฟอนต์มาตรฐานแทน");
        }

        // 1. Locate and configure 5 Evidence Items in the church
        int configuredCount = 0;

        // Evidence 1: Footprint
        configuredCount += ConfigureEvidence(
            new string[] { "CHURCH_EVIDENC_DRYSHOEPRINT.001", "CHURCH_EVIDENC_DRYSHOEPRINT.003", "CHURCH_EVIDENC_DRYSHOEPRINT.004", "CHURCH_EVIDENC_DRYSHOEPRINT.002" },
            "footprint",
            "footprint",
            "รอยเท้าแห้ง (Dry Footprint)",
            "รอยพื้นรองเท้าบูทแห้งติดอยู่บนพื้นหินในโบสถ์เซนต์แมรี่ มีคราบโคลนที่บ่งบอกถึงการเคลื่อนไหวอย่างเร่งรีบของคนร้าย"
        );

        // Evidence 2: Handkerchief
        configuredCount += ConfigureEvidence(
            new string[] { "CHURCH_EVIDENCE_EMBROIDEREDHANDKERCHIEF" },
            "handkerchief",
            "handkerchief",
            "ผ้าเช็ดหน้าปักลาย (Embroidered Handkerchief)",
            "ผ้าเช็ดหน้าผ้าลินินปักลายประณีต มีคราบเขม่าจางๆ และกลิ่นสารเคมีแปลกปลอมตกอยู่ใกล้ที่นั่งโบสถ์"
        );

        // Evidence 3: Prayer Sheet
        configuredCount += ConfigureEvidence(
            new string[] { "CHURCH_EVIDENCE_EVENINGPRAYERSHEET" },
            "prayer_sheet",
            "prayer_sheet",
            "กระดาษบทสวดภาวนา (Evening Prayer Sheet)",
            "แผ่นกระดาษบทสวดช่วงค่ำที่ถูกฉีกขอบ ตกอยู่ใกล้แท่นบูชาของโบสถ์ มีรอยเปื้อนคราบน้ำตา"
        );

        // Evidence 4: Large Ring
        configuredCount += ConfigureEvidence(
            new string[] { "CHURCH_EVIDENCE_RINGLARGE" },
            "ring_large",
            "ring",
            "แหวนทองโบราณวงใหญ่ (Victorian Large Ring)",
            "แหวนทองคำสลักลวดลายวิกตอเรีย สลักอักษรย่อ เป็นขนาดของผู้หญิงวัยผู้ใหญ่ (ของผู้ตาย แมรี่ แอน)"
        );

        // Evidence 5: Small Ring
        configuredCount += ConfigureEvidence(
            new string[] { "CHURCH_EVIDENCE_RINGSMALL" },
            "ring_small",
            "ring",
            "แหวนทองโบราณวงเล็ก (Victorian Small Ring)",
            "แหวนทองคำที่มีลวดลายและตราสัญลักษณ์เหมือนกับวงใหญ่อย่างน่าประหลาดใจ แต่เป็นขนาดสำหรับเด็กเล็ก!"
        );

        // 2. Setup or Find Quest System GameObject
        GameObject questSystemGo = GameObject.Find("Quest1_StMaryChurch_System");
        if (questSystemGo == null)
        {
            questSystemGo = new GameObject("Quest1_StMaryChurch_System");
        }

        // Add / Configure Cinematic Letterbox
        var letterbox = questSystemGo.GetComponent<CinematicLetterbox>();
        if (letterbox == null) letterbox = questSystemGo.AddComponent<CinematicLetterbox>();
        if (thaiFont != null) letterbox.thaiFont = thaiFont;

        // Add / Configure CiRA Core Vision Scanner
        var scanner = questSystemGo.GetComponent<CiraCoreVisionScanner>();
        if (scanner == null) scanner = questSystemGo.AddComponent<CiraCoreVisionScanner>();
        if (thaiFont != null) scanner.thaiFont = thaiFont;

        // Add / Configure Quest Manager
        var questMgr = questSystemGo.GetComponent<StMaryChurchQuestManager>();
        if (questMgr == null) questMgr = questSystemGo.AddComponent<StMaryChurchQuestManager>();
        if (thaiFont != null) questMgr.thaiFont = thaiFont;
        if (thaiFontBold != null) questMgr.thaiFontBold = thaiFontBold;

        // โหลดและตั้งค่าพื้นผิวกระดาษโบราณ lilydust-old-2012064 เป็น Sprite
        string paperTexPath = "Assets/UI/lilydust-old-2012064.jpg";
        TextureImporter paperImporter = AssetImporter.GetAtPath(paperTexPath) as TextureImporter;
        if (paperImporter != null)
        {
            if (paperImporter.textureType != TextureImporterType.Sprite || paperImporter.spriteImportMode != SpriteImportMode.Single)
            {
                paperImporter.textureType = TextureImporterType.Sprite;
                paperImporter.spriteImportMode = SpriteImportMode.Single;
                paperImporter.mipmapEnabled = false;
                paperImporter.SaveAndReimport();
            }
        }
        Sprite paperSprite = AssetDatabase.LoadAssetAtPath<Sprite>(paperTexPath);
        if (paperSprite != null)
        {
            questMgr.parchmentPaperSprite = paperSprite;
            EditorUtility.SetDirty(questMgr);
        }

        // สร้าง UI ทั้งหมดใหม่ด้วยฟอนต์ไทยแท้ (Prompt-Medium / Prompt-Bold) พร้อมแผ่นรองอ่านคมชัด
        questMgr.BuildQuestUI();
        EditorUtility.SetDirty(questMgr);

        // 3. Create or Configure Church Area Trigger (บริเวณซุ้มประตูทางเข้าโบสถ์เซนต์แมรี่)
        GameObject triggerGo = GameObject.Find("Church_Entry_AreaTrigger");
        if (triggerGo == null)
        {
            triggerGo = new GameObject("Church_Entry_AreaTrigger");
        }
        triggerGo.transform.position = new Vector3(19.5f, 1.2f, 28.5f);
        BoxCollider box = triggerGo.GetComponent<BoxCollider>();
        if (box == null) box = triggerGo.AddComponent<BoxCollider>();
        box.size = new Vector3(6.0f, 3.5f, 5.0f);
        box.isTrigger = true;
        if (triggerGo.GetComponent<ChurchAreaTrigger>() == null) triggerGo.AddComponent<ChurchAreaTrigger>();
        EditorUtility.SetDirty(triggerGo);

        // 4. Calibrate Player Position & PlayerCameraRoot on Newman
        GameObject player = GameObject.Find("NestedParentArmature_Newman") 
                         ?? GameObject.Find("PlayerArmature") 
                         ?? GameObject.Find("Player_CH2_Newman");
        Transform playerCamRoot = null;
        if (player != null)
        {
            // วางตัวละคร Newman ลงบนพื้นถนนหินพอดี (ไม่ลอยกลางอากาศ)
            player.transform.position = new Vector3(9.04f, 1.826f, 24.6f);
            EditorUtility.SetDirty(player);

            var allT = player.GetComponentsInChildren<Transform>(true);
            foreach (var t in allT) { if (t.name == "PlayerCameraRoot") { playerCamRoot = t; break; } }
            if (playerCamRoot != null)
            {
                // ตำแหน่ง local 0.85m ให้ความสูงระดับอก 1.35m พอดีในระดับสายตา
                playerCamRoot.localPosition = new Vector3(0f, 0.85f, 0f);
                EditorUtility.SetDirty(playerCamRoot.gameObject);
            }
        }

        // ปลด Main Camera ออกจากตัวละคร Newman มาอยู่ที่ Root เพื่อป้องกันการหมุน/สเกลเพี้ยน
        GameObject mainCam = GameObject.FindGameObjectWithTag("MainCamera");
        if (mainCam != null)
        {
            if (mainCam.transform.parent != null)
            {
                mainCam.transform.SetParent(null, true);
            }
            mainCam.transform.position = new Vector3(9.49f, 1.85f, 22.4f);
            mainCam.transform.rotation = Quaternion.identity;
            EditorUtility.SetDirty(mainCam);
        }

        // 5. Configure & Fix PlayerFollowCamera (มุมมองบุคคลที่ 3 สไตล์ Resident Evil Requiem OTS)
        GameObject followCamGo = GameObject.Find("PlayerFollowCamera");
        CinemachineCamera playerCmCam = null;
        if (followCamGo != null)
        {
            // ปลดกล้องออกจากตัวละครให้อยู่ที่ Root
            if (followCamGo.transform.parent != null)
            {
                followCamGo.transform.SetParent(null, true);
            }

            playerCmCam = followCamGo.GetComponent<CinemachineCamera>();
            if (playerCmCam != null)
            {
                playerCmCam.Priority.Value = 10;
                playerCmCam.Lens.FieldOfView = 48f; // Resident Evil OTS Cinematic FOV
                playerCmCam.Lens.NearClipPlane = 0.10f;
                if (playerCamRoot != null)
                {
                    playerCmCam.Target.TrackingTarget = playerCamRoot;
                }
                EditorUtility.SetDirty(playerCmCam);
            }

            var thirdPerson = followCamGo.GetComponent<CinemachineThirdPersonFollow>();
            if (thirdPerson != null)
            {
                // Resident Evil Requiem Over-the-Shoulder (OTS) Locked Framing
                thirdPerson.CameraDistance = 0.99f; // ล็อคระยะกล้อง 0.99 m
                thirdPerson.CameraSide = 1.0f; // ข้ามไหล่ขวาเต็มรูปแบบ
                thirdPerson.ShoulderOffset = new Vector3(0.60f, -0.374f, 0.0f); // เยื้องขวา 0.60m, ความสูงไหล่ -0.374m (-0.37m)
                thirdPerson.VerticalArmLength = 0.0f;
                thirdPerson.Damping = new Vector3(0.05f, 0.08f, 0.08f);
                thirdPerson.AvoidObstacles.Enabled = false;
                EditorUtility.SetDirty(thirdPerson);
            }

            var endfieldCtrl = followCamGo.GetComponent<EndfieldCameraController>();
            if (endfieldCtrl != null)
            {
                endfieldCtrl.fieldOfView = 48f; // FOV 48°
                endfieldCtrl.cameraDistance = 0.99f;
                endfieldCtrl.shoulderSide = 0.60f;
                endfieldCtrl.shoulderHeight = -0.374f;
                endfieldCtrl.cameraTilt = -16.4f; // มุมก้ม/เงย -16.4°
                endfieldCtrl.shoulderOffset = new Vector3(0.60f, -0.374f, 0.0f);
                endfieldCtrl.verticalArmLength = 0.0f;
                endfieldCtrl.defaultCameraSide = 1.0f;
                endfieldCtrl.enableZoom = false; // ล็อคตายตัว ห้ามซูม
                endfieldCtrl.defaultDistance = 0.99f;
                endfieldCtrl.minDistance = 0.99f;
                endfieldCtrl.maxDistance = 0.99f;
                if (playerCamRoot != null) endfieldCtrl.target = playerCamRoot;
                EditorUtility.SetDirty(endfieldCtrl);
            }

            // ตั้งค่ามุมก้ม/เงยให้ ThirdPersonController ถ้ามี
            var tpc = player != null ? player.GetComponent<StarterAssets.ThirdPersonController>() : null;
            if (tpc == null) tpc = Object.FindAnyObjectByType<StarterAssets.ThirdPersonController>();
            if (tpc != null)
            {
                tpc.CameraAngleOverride = -16.4f;
                EditorUtility.SetDirty(tpc);
            }
            if (playerCamRoot != null)
            {
                playerCamRoot.localEulerAngles = new Vector3(-16.4f, 0f, 0f);
                EditorUtility.SetDirty(playerCamRoot);
            }

            followCamGo.transform.rotation = Quaternion.identity;
            EditorUtility.SetDirty(followCamGo);
        }

        // 6. Setup Movie Cutscene Cameras (กล้องภาพยนตร์ 3 มุมมองระดับ Movie Grade เทพ)
        // Shot 1: Establishing Shot - มุมกว้างส่องความอลังการของโบสถ์เซนต์แมรี่และหมอกราตรี
        GameObject camWideGo = GameObject.Find("Cutscene_Cam_ChurchWide");
        if (camWideGo == null) camWideGo = new GameObject("Cutscene_Cam_ChurchWide");
        camWideGo.transform.position = new Vector3(10.5f, 1.25f, 20.0f);
        camWideGo.transform.rotation = Quaternion.LookRotation(new Vector3(23.5f, 6.50f, 28.5f) - new Vector3(10.5f, 1.25f, 20.0f));
        var cmWide = camWideGo.GetComponent<CinemachineCamera>();
        if (cmWide == null) cmWide = camWideGo.AddComponent<CinemachineCamera>();
        cmWide.Priority.Value = 0;
        cmWide.Lens.FieldOfView = 44f;
        cmWide.Lens.NearClipPlane = 0.15f;
        EditorUtility.SetDirty(camWideGo);

        // Shot 2: Close-up / Portrait - มุมโคลสอัพ 3/4 Hero Profile สารวัตรนิวแมน
        GameObject camNewmanGo = GameObject.Find("Cutscene_Cam_NewmanClose");
        if (camNewmanGo == null) camNewmanGo = new GameObject("Cutscene_Cam_NewmanClose");
        camNewmanGo.transform.position = new Vector3(19.2f, 2.95f, 27.2f);
        camNewmanGo.transform.rotation = Quaternion.Euler(-2f, 25f, 0f);
        var cmNewman = camNewmanGo.GetComponent<CinemachineCamera>();
        if (cmNewman == null) cmNewman = camNewmanGo.AddComponent<CinemachineCamera>();
        cmNewman.Priority.Value = 0;
        cmNewman.Lens.FieldOfView = 30f;
        cmNewman.Lens.NearClipPlane = 0.15f;
        EditorUtility.SetDirty(camNewmanGo);

        // Shot 3: Crime Scene / Altar Interior - ทางเดินกลางโบสถ์มุ่งสู่แท่นบูชา
        GameObject camCrimeGo = GameObject.Find("Cutscene_Cam_CrimeScene");
        if (camCrimeGo == null) camCrimeGo = new GameObject("Cutscene_Cam_CrimeScene");
        camCrimeGo.transform.position = new Vector3(20.5f, 1.15f, 29.30f);
        camCrimeGo.transform.rotation = Quaternion.LookRotation(new Vector3(25.5f, 0.40f, 29.30f) - new Vector3(20.5f, 1.15f, 29.30f));
        var cmCrime = camCrimeGo.GetComponent<CinemachineCamera>();
        if (cmCrime == null) cmCrime = camCrimeGo.AddComponent<CinemachineCamera>();
        cmCrime.Priority.Value = 0;
        cmCrime.Lens.FieldOfView = 38f;
        cmCrime.Lens.NearClipPlane = 0.15f;
        EditorUtility.SetDirty(camCrimeGo);


        // 7. Setup Cinematic Cutscene Director
        var cutsceneDirector = questSystemGo.GetComponent<CinematicCutsceneManager>();
        if (cutsceneDirector == null) cutsceneDirector = questSystemGo.AddComponent<CinematicCutsceneManager>();
        cutsceneDirector.camChurchWide = cmWide;
        cutsceneDirector.camNewmanClose = cmNewman;
        cutsceneDirector.camCrimeScene = cmCrime;
        cutsceneDirector.playerFollowCamera = playerCmCam;
        EditorUtility.SetDirty(cutsceneDirector);

        // 8. Apply Cinematic Global Volume & Lighting
        ConfigureCinematicLighting.ApplyCinematicLighting();

        EditorUtility.SetDirty(questSystemGo);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"<color=green><b>[SUCCESS]</b> ติดตั้งระบบเควส Scene 1 (โบสถ์เซนต์แมรี่) และระบบกล้องคัตซีนภาพยนตร์ 3 ช็อต (Wide / Newman Portrait / Crime Scene) สำเร็จ 100%! ปลดล็อคปัญหากล้องจมพื้นเรียบร้อย</color>");
    }

    private static int ConfigureEvidence(string[] candidateNames, string id, string ciraClass, string title, string desc)
    {
        GameObject targetGo = null;
        foreach (var name in candidateNames)
        {
            targetGo = GameObject.Find(name);
            if (targetGo != null) break;
        }

        if (targetGo == null)
        {
            // Search in scene transforms
            var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (var t in allTransforms)
            {
                foreach (var name in candidateNames)
                {
                    if (t.name == name || t.name.Contains(name))
                    {
                        targetGo = t.gameObject;
                        break;
                    }
                }
                if (targetGo != null) break;
            }
        }

        if (targetGo != null)
        {
            var ev = targetGo.GetComponent<EvidenceItem>();
            if (ev == null) ev = targetGo.AddComponent<EvidenceItem>();

            ev.evidenceId = id;
            ev.ciraClassName = ciraClass;
            ev.thaiTitle = title;
            ev.thaiDescription = desc;
            ev.detectionRange = 14f;

            // Ensure Collider with proper world size regardless of mesh scale
            BoxCollider box = targetGo.GetComponent<BoxCollider>();
            if (box == null) box = targetGo.AddComponent<BoxCollider>();
            Vector3 lossy = targetGo.transform.lossyScale;
            box.size = new Vector3(
                0.65f / Mathf.Max(0.0001f, lossy.x),
                0.35f / Mathf.Max(0.0001f, lossy.y),
                0.65f / Mathf.Max(0.0001f, lossy.z)
            );
            box.center = Vector3.zero;
            box.isTrigger = false;

            EditorUtility.SetDirty(targetGo);
            return 1;
        }

        return 0;
    }
}
