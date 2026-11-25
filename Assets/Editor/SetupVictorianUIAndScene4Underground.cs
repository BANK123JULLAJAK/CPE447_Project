using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;

public static class SetupVictorianUIAndScene4Underground
{
    [MenuItem("Tools/Setup Victorian UI And Scene 4 Underground")]
    public static void Run()
    {
        // 1. Configure VictorianSepiaBlueprint texture importer
        string blueprintPath = "Assets/Sprites/DetectiveUI/VictorianSepiaBlueprint.jpg";
        var importer = AssetImporter.GetAtPath(blueprintPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            Debug.Log("[CONFIGURED] VictorianSepiaBlueprint as Sprite");
        }

        // 2. Place Scene 4 Evidence on the Underground Station Platform
        // 1. Blood Splatter: On the platform brick floor near the train tracks
        var blood = GameObject.Find("TRAIN_EVIDENCE_BLOODSPLATTER");
        if (blood != null)
        {
            blood.transform.position = new Vector3(-44.00f, -9.75f, 47.26f);
            blood.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            SetupEvidenceCollider(blood, "station_blood_trail", "footprint", "รอยเลือดบนชานชาลารถไฟ", "หยดเลือดสดส่องตามชานชาลา มุ่งตรงไปยังตู้ท้ายของขบวนรถไฟด่วน");
            Debug.Log($"[MOVED UNDERGROUND] Blood to {blood.transform.position}");
        }

        // 2. Spent Bullet Casing: Beside train wheels / railway edge
        var bullet = GameObject.Find("TRAIN_EVIDENCE_SPENTCARTRIDGECASE");
        if (bullet != null)
        {
            bullet.transform.position = new Vector3(-46.50f, -9.75f, 44.50f);
            bullet.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            SetupEvidenceCollider(bullet, "station_bullet_casing", "ring", "ปลอกกระสุนปืนลูกโม่ Webley .455", "ปลอกกระสุนทองเหลืองเพิ่งยิงสดๆ ยังมีควันดินปืน ตกอยู่ใกล้รางรถไฟตู้ท้ายขบวน");
            Debug.Log($"[MOVED UNDERGROUND] Bullet to {bullet.transform.position}");
        }

        // 3. Frozen Pocket Watch: On the platform near the bench
        var watch = GameObject.Find("TRAIN_EVIDENCE_POCKETWATCH");
        if (watch != null)
        {
            watch.transform.position = new Vector3(-42.80f, -9.75f, 45.20f);
            watch.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            SetupEvidenceCollider(watch, "station_frozen_watch", "prayer_sheet", "นาฬิกาพกเรือนเงินหยุดเดิน 11:45 PM", "หน้าปัดแตกค้างอยู่ที่เวลา 11:45 PM พอดี ตรงกับเวลาออกของขบวนรถด่วนพิเศษเที่ยวเที่ยงคืน");
            Debug.Log($"[MOVED UNDERGROUND] Watch to {watch.transform.position}");
        }

        // 4. Railway Ticket: In front of the "TICKETS" booth window
        var ticket = GameObject.Find("TRAIN_EVIDENCE_RAILWAYTICKET");
        if (ticket != null)
        {
            ticket.transform.position = new Vector3(-43.50f, -9.75f, 42.50f);
            ticket.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            SetupEvidenceCollider(ticket, "station_railway_ticket", "handkerchief", "ตั๋วรถไฟระบุหมายเลขตู้ขบวน", "ตั๋วรถไฟฉีกขาดระบุ: รถด่วนสายเหนือ ชานชาลา 2 ตู้โดยสารหมายเลข 4 (Carriage 04)");
            Debug.Log($"[MOVED UNDERGROUND] Ticket to {ticket.transform.position}");
        }

        // 5. Moriarty Note: Pinned on the tunnel brick column
        var note = GameObject.Find("TRAIN_EVIDENCE_STATIONNOTE");
        if (note != null)
        {
            note.transform.position = new Vector3(-42.20f, -8.60f, 48.00f);
            note.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            SetupEvidenceCollider(note, "station_moriarty_note", "prayer_sheet", "จดหมายตราประทับตัว M จากมอริอาตี้", "\"สารวัตรนิวแมน... เขาซ่อนตัวอยู่ที่ตู้หน้าสุด! - M\" (คำใบ้ลวงที่จงใจชี้นำผิดทาง!)");
            Debug.Log($"[MOVED UNDERGROUND] Note to {note.transform.position}");
        }

        // 3. Create Scene 4 Underground Cutscene Cameras
        // Cam 1: Underground Wide Shot (Matching user's exact SceneView camera in screenshot!)
        GameObject cam1Go = GameObject.Find("Cutscene_Cam_StationUndergroundWide");
        if (cam1Go == null) cam1Go = new GameObject("Cutscene_Cam_StationUndergroundWide");
        cam1Go.transform.position = new Vector3(-41.89f, -7.14f, 42.96f);
        cam1Go.transform.rotation = Quaternion.Euler(12.38f, 291.34f, 0f);
        var cm1 = cam1Go.GetComponent<CinemachineCamera>();
        if (cm1 == null) cm1 = cam1Go.AddComponent<CinemachineCamera>();
        cm1.Priority = 0;

        // Cam 2: Train & Evidence Macro Push Shot
        GameObject cam2Go = GameObject.Find("Cutscene_Cam_StationEvidencePush");
        if (cam2Go == null) cam2Go = new GameObject("Cutscene_Cam_StationEvidencePush");
        cam2Go.transform.position = new Vector3(-44.20f, -8.60f, 45.20f);
        cam2Go.transform.rotation = Quaternion.Euler(18.0f, 245.0f, 0f);
        var cm2 = cam2Go.GetComponent<CinemachineCamera>();
        if (cm2 == null) cm2 = cam2Go.AddComponent<CinemachineCamera>();
        cm2.Priority = 0;

        // 4. Setup Scene 4 Underground Trigger
        GameObject triggerGo = GameObject.Find("Scene4_UndergroundPlatform_Trigger");
        if (triggerGo == null) triggerGo = new GameObject("Scene4_UndergroundPlatform_Trigger");
        triggerGo.transform.position = new Vector3(-42.50f, -9.50f, 44.50f);
        var box = triggerGo.GetComponent<BoxCollider>();
        if (box == null) box = triggerGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(14f, 6f, 16f);
        box.center = Vector3.zero;

        var sat = triggerGo.GetComponent<StationAreaTrigger>();
        if (sat == null) sat = triggerGo.AddComponent<StationAreaTrigger>();

        // 5. Update UndergroundStationQuestManager references
        var uqm = Object.FindAnyObjectByType<UndergroundStationQuestManager>();
        if (uqm != null)
        {
            uqm.stationStairwellTarget = triggerGo.transform;
            uqm.bloodTrailGo = blood;
            uqm.bulletCasingGo = bullet;
            uqm.frozenWatchGo = watch;
            uqm.railwayTicketGo = ticket;
            uqm.moriartyNoteGo = note;
            EditorUtility.SetDirty(uqm);
        }

        // Also add Transition Door from Street to Underground if player steps into Street entrance
        var streetEntrance = GameObject.Find("Scene4_StationEntranceTrigger");
        if (streetEntrance != null)
        {
            var dt = streetEntrance.GetComponent<StationStreetToUndergroundDoor>();
            if (dt == null) streetEntrance.AddComponent<StationStreetToUndergroundDoor>();
        }

        // 6. Rebuild London Railway Map UI with new Victorian Noir theme
        var rmm = Object.FindAnyObjectByType<LondonRailwayMapMinigame>();
        if (rmm != null)
        {
            rmm.BuildMinigameUI();
            EditorUtility.SetDirty(rmm);
            Debug.Log("[REBUILT] LondonRailwayMapMinigame UI with Victorian Noir styling");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("<color=green><b>[SUCCESS]</b> Scene 4 Underground Station Platform, Cutscene Cameras, and 5 Evidence Items successfully configured!</color>");
    }

    private static void SetupEvidenceCollider(GameObject go, string id, string ciraClass, string title, string desc)
    {
        if (go == null) return;
        var ev = go.GetComponent<EvidenceItem>();
        if (ev == null) ev = go.AddComponent<EvidenceItem>();
        ev.evidenceId = id;
        ev.ciraClassName = ciraClass;
        ev.thaiTitle = title;
        ev.thaiDescription = desc;
        ev.detectionRange = 12f;

        var box = go.GetComponent<BoxCollider>();
        if (box == null) box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        if (go.transform.lossyScale.x > 10f)
        {
            box.size = new Vector3(0.015f, 0.015f, 0.015f);
        }
        else
        {
            box.size = new Vector3(1.5f, 1.5f, 1.5f);
        }
        box.center = Vector3.zero;
        EditorUtility.SetDirty(go);
    }
}
