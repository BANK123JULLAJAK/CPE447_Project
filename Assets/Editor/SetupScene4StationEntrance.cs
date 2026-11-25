using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SetupScene4StationEntrance
{
    [MenuItem("Tools/Setup Scene 4 Station Entrance")]
    public static void Run()
    {
        // 1. Create or Find Station Entrance Trigger GameObject right in front of the station archway
        Vector3 entrancePos = new Vector3(-16.8f, 0.5f, 44.16f);
        GameObject triggerGo = GameObject.Find("Scene4_StationEntranceTrigger");
        if (triggerGo == null)
        {
            triggerGo = new GameObject("Scene4_StationEntranceTrigger");
        }
        triggerGo.transform.position = entrancePos;
        triggerGo.transform.rotation = Quaternion.identity;

        var box = triggerGo.GetComponent<BoxCollider>();
        if (box == null) box = triggerGo.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(8f, 4f, 8f);
        box.center = Vector3.zero;

        var sat = triggerGo.GetComponent<StationAreaTrigger>();
        if (sat == null) sat = triggerGo.AddComponent<StationAreaTrigger>();

        // Remove old StationAreaTrigger from TRAIN_STATION_STARIRWELL if present
        var oldStairwell = GameObject.Find("TRAIN_STATION_STARIRWELL");
        if (oldStairwell != null)
        {
            var oldTrigger = oldStairwell.GetComponent<StationAreaTrigger>();
            if (oldTrigger != null) Object.DestroyImmediate(oldTrigger);
        }

        // 2. Relocate all 5 evidence items to the station entrance area
        // 1. Blood Splatter: On the stone threshold ramp leading into the archway
        var blood = GameObject.Find("TRAIN_EVIDENCE_BLOODSPLATTER");
        if (blood != null)
        {
            blood.transform.position = new Vector3(-17.6f, 0.12f, 44.16f);
            blood.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            SetupEvidenceCollider(blood, "station_blood_trail", "footprint", "รอยเลือดบนธรณีประตูสถานี", "หยดเลือดสดส่องตามทางเข้า เลี้ยวขวาเข้าสู่ชานชาลาที่ 2 มุ่งตรงไปยังขบวนรถไฟ");
            Debug.Log($"[MOVED] Blood splatter to {blood.transform.position}");
        }

        // 2. Spent Bullet Casing: Under the street lamp near the station
        var bullet = GameObject.Find("TRAIN_EVIDENCE_SPENTCARTRIDGECASE");
        if (bullet != null)
        {
            bullet.transform.position = new Vector3(-15.2f, 0.12f, 41.8f);
            bullet.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            SetupEvidenceCollider(bullet, "station_bullet_casing", "ring", "ปลอกกระสุนปืนลูกโม่ Webley .455", "ปลอกกระสุนทองเหลืองเพิ่งยิงสดๆ ยังมีควันดินปืน ตกอยู่ใกล้เสาไฟหน้าสถานี");
            Debug.Log($"[MOVED] Bullet casing to {bullet.transform.position}");
        }

        // 3. Frozen Pocket Watch: On the cobblestone pavement next to the entrance gate
        var watch = GameObject.Find("TRAIN_EVIDENCE_POCKETWATCH");
        if (watch != null)
        {
            watch.transform.position = new Vector3(-16.2f, 0.12f, 46.2f);
            watch.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            SetupEvidenceCollider(watch, "station_frozen_watch", "prayer_sheet", "นาฬิกาพกเรือนเงินหยุดเดิน 11:45 PM", "หน้าปัดแตกค้างอยู่ที่เวลา 11:45 PM พอดี ตกอยู่ข้างซุ้มประตูสถานี");
            Debug.Log($"[MOVED] Pocket watch to {watch.transform.position}");
        }

        // 4. Railway Ticket: At the entrance wooden door
        var ticket = GameObject.Find("TRAIN_EVIDENCE_RAILWAYTICKET");
        if (ticket != null)
        {
            ticket.transform.position = new Vector3(-18.5f, 0.15f, 44.16f);
            ticket.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            SetupEvidenceCollider(ticket, "station_railway_ticket", "handkerchief", "ตั๋วรถไฟระบุหมายเลขตู้ขบวน", "ตั๋วรถไฟฉีกขาดระบุ: รถด่วนสายเหนือ ชานชาลา 2 ตู้โดยสารหมายเลข 4 (Carriage 04)");
            Debug.Log($"[MOVED] Railway ticket to {ticket.transform.position}");
        }

        // 5. Moriarty Note: Pinned on the brick wall column beside the entrance archway
        var note = GameObject.Find("TRAIN_EVIDENCE_STATIONNOTE");
        if (note != null)
        {
            note.transform.position = new Vector3(-17.2f, 1.35f, 42.6f);
            note.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            SetupEvidenceCollider(note, "station_moriarty_note", "prayer_sheet", "จดหมายตราประทับตัว M จากมอริอาตี้", "\"สารวัตรนิวแมน... เขาซ่อนตัวอยู่ที่ตู้หน้าสุด! - M\" (คำใบ้ลวงที่จงใจชี้นำผิดทาง!)");
            Debug.Log($"[MOVED] Moriarty note to {note.transform.position}");
        }

        // 3. Update UndergroundStationQuestManager references
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

        // Mark scene dirty and save
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("<color=green><b>[SUCCESS]</b> Scene 4 Station Entrance & 5 Evidence Objects successfully placed at Metropolitan Railway Entrance!</color>");
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
        // Check scale
        if (go.transform.lossyScale.x > 10f)
        {
            box.size = new Vector3(0.015f, 0.015f, 0.015f); // ~1.5m in world
        }
        else
        {
            box.size = new Vector3(1.5f, 1.5f, 1.5f);
        }
        box.center = Vector3.zero;
        EditorUtility.SetDirty(go);
    }
}
