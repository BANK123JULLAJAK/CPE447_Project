using UnityEditor;
using UnityEngine;

/// <summary>
/// ตัวติดตั้งระบบ Genshin Impact Quest Menu & Frameless HUD (ไร้กรอบ 100%)
/// - เมนูเควสสไตล์ Genshin Impact 2 คอลัมน์ (กด E หรือ ESC)
/// - HUD บอกเควสบนหน้าจอแบบ Genshin ไร้กรอบสี่เหลี่ยม ข้อความลอยอิสระพร้อมเงาคมชัด
/// - หมุดนำทาง Waypoint Navigation แบบ Genshin เพชรสีทอง ◆ ไร้กล่องรองใต้
/// - ป้องกัน UI เควสซ้อนกัน 100% (เควส 2 และ 3 ถูกปิดเป็นค่าเริ่มต้น)
/// </summary>
public static class SetupCaseArchiveJournal
{
    [MenuItem("Tools/✨ ติดตั้ง UI เมนูเควส & HUD แบบเก็นชิน (Genshin Impact Frameless Style)")]
    public static void SetupJournal()
    {
        Font thaiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Prompt-Medium.ttf")
                     ?? AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Sarabun-Medium.ttf");
        Font thaiFontBold = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Prompt-Bold.ttf")
                         ?? AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Sarabun-Bold.ttf");

        // 1. Setup CaseArchiveJournalUI (Genshin Quest Menu)
        GameObject journalGo = GameObject.Find("CaseArchive_Journal_System");
        if (journalGo != null)
        {
            var oldModal = journalGo.transform.Find("CaseArchive_ModalPanel") ?? journalGo.transform.Find("GenshinQuestMenu_Modal");
            if (oldModal != null) UnityEngine.Object.DestroyImmediate(oldModal.gameObject);
        }
        else
        {
            journalGo = new GameObject("CaseArchive_Journal_System");
        }

        var journalComp = journalGo.GetComponent<CaseArchiveJournalUI>();
        if (journalComp == null) journalComp = journalGo.AddComponent<CaseArchiveJournalUI>();
        if (thaiFont != null) journalComp.thaiFont = thaiFont;
        if (thaiFontBold != null) journalComp.thaiFontBold = thaiFontBold;

        EditorUtility.SetDirty(journalGo);

        // 2. Refresh StMaryChurchQuestManager HUD (Genshin Frameless)
        var q1Go = GameObject.Find("Quest1_StMaryChurch_System") ?? GameObject.Find("Quest_StMaryChurch_System");
        if (q1Go != null)
        {
            var q1 = q1Go.GetComponent<StMaryChurchQuestManager>();
            if (q1 != null)
            {
                var oldHud = q1Go.transform.Find("ObjectivePanel");
                if (oldHud != null) UnityEngine.Object.DestroyImmediate(oldHud.gameObject);
                q1.BuildQuestUI(true);
                q1.ShowObjectiveHUD(true);
                EditorUtility.SetDirty(q1Go);
            }
        }

        // 3. Refresh LondonAlleyQuestManager HUD (Genshin Frameless - Inactive on start)
        var q2Go = GameObject.Find("Quest2_LondonAlley_System");
        if (q2Go != null)
        {
            var q2 = q2Go.GetComponent<LondonAlleyQuestManager>();
            if (q2 != null)
            {
                var oldHud = q2Go.transform.Find("ObjectivePanel_Scene2");
                if (oldHud != null) UnityEngine.Object.DestroyImmediate(oldHud.gameObject);
                q2.BuildQuest2HUD();
                q2.ShowObjectiveHUD(false); // Inactive until Scene 2 starts
                EditorUtility.SetDirty(q2Go);
            }
        }

        // 4. Refresh RoyalLyceumTheaterQuestManager HUD (Genshin Frameless - Inactive on start)
        var q3Go = GameObject.Find("Quest3_RoyalLyceumTheater_System");
        if (q3Go != null)
        {
            var q3 = q3Go.GetComponent<RoyalLyceumTheaterQuestManager>();
            if (q3 != null)
            {
                var oldHud = q3Go.transform.Find("Quest3_LyceumTheater_HUD");
                if (oldHud != null) UnityEngine.Object.DestroyImmediate(oldHud.gameObject);
                q3.BuildQuest3HUD();
                q3.ShowObjectiveHUD(false); // Inactive until Scene 3 starts
                EditorUtility.SetDirty(q3Go);
            }
        }

        // 5. Refresh WaypointNavigationSystem UI (Genshin Frameless Marker)
        var wpGo = GameObject.Find("Waypoint_Navigation_System");
        if (wpGo != null)
        {
            var wp = wpGo.GetComponent<WaypointNavigationSystem>();
            if (wp != null)
            {
                var oldWpHud = wpGo.transform.Find("Genshin_Waypoint_HUD");
                if (oldWpHud != null) UnityEngine.Object.DestroyImmediate(oldWpHud.gameObject);
                wp.BuildGenshinWaypointUI();
                EditorUtility.SetDirty(wpGo);
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=#FFE600><b>[GENSHIN UI SETUP]</b> ติดตั้งระบบเมนูเควส & HUD แบบเก็นชิน (ไร้กรอบ 100%) สำเร็จเรียบร้อย!</color>");
    }
}
