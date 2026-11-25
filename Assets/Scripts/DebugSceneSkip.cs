using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// ทางลัดสำหรับทดสอบเกม (เฉพาะใน Editor / Development Build): กด [O] ไปฉาก 3, [P] ไปฉาก 4, [L] ไปฉากจบ
/// ปิดหน้าต่าง/มินิเกม/คัตซีนที่ค้างอยู่ ปลดล็อกผู้เล่น วาร์ปตัวละคร แล้วเริ่มเควสของฉากนั้นทันที
/// </summary>
public class DebugSceneSkip : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static DebugSceneSkip _instance;
    private bool _busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (_instance != null) return;
        var go = new GameObject("DebugSceneSkip");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<DebugSceneSkip>();
    }

    private void Update()
    {
        if (_busy || GameEndingSequence.IsPlaying) return;

        int target = 0;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.pKey.wasPressedThisFrame) target = 4;        // P = ไปฉาก 4 ทันที
            else if (kb.oKey.wasPressedThisFrame) target = 3;   // O = ไปฉาก 3
            else if (kb.lKey.wasPressedThisFrame) target = 5;   // L = ไปฉากจบ
        }
#else
        if (Input.GetKeyDown(KeyCode.P)) target = 4;
        else if (Input.GetKeyDown(KeyCode.O)) target = 3;
        else if (Input.GetKeyDown(KeyCode.L)) target = 5;
#endif
        if (target == 0) return;

        StartCoroutine(JumpRoutine(target));
    }

    /// <summary>ตัวผู้เล่นที่มี CharacterController จริง (PlayerArmature) ไม่ใช่ root ที่อยู่สูงกว่า</summary>
    private static Transform FindPlayerBody()
    {
        var cc = FindAnyObjectByType<CharacterController>();
        if (cc != null) return cc.transform;
        var go = GameObject.Find("PlayerArmature");
        return go != null ? go.transform : null;
    }

    private static void ClearEverything()
    {
        Time.timeScale = 1f;
        var title = FindAnyObjectByType<TitleMenuUI>();
        if (title != null) Destroy(title.gameObject); // ข้ามหน้าเมนูเริ่มเกม
        StopAllCinematics();

        CaseDeductionBoardUI.ForceClose();
        ChapterSummaryUI.ForceClose();

        if (CaseArchiveJournalUI.Instance != null && CaseArchiveJournalUI.Instance.IsOpen) CaseArchiveJournalUI.Instance.CloseQuestMenu();
        if (NewspaperDecryptionMinigame.Instance != null && NewspaperDecryptionMinigame.Instance.IsMinigameActive) NewspaperDecryptionMinigame.Instance.CloseMinigame();
        if (LondonRailwayMapMinigame.Instance != null && LondonRailwayMapMinigame.Instance.IsMinigameActive) LondonRailwayMapMinigame.Instance.CloseMinigame();
        if (EvidenceConnectingDotsMinigame.Instance != null && EvidenceConnectingDotsMinigame.Instance.IsMinigameActive) EvidenceConnectingDotsMinigame.Instance.CloseMinigame();

        DetectiveThoughtToast.Clear();
        if (CinematicLetterbox.Instance != null)
        {
            CinematicLetterbox.Instance.StopAllCoroutines(); // กัน callback ของคัตซีนเก่ามาเปิด HUD ซ้อนทีหลัง
            CinematicLetterbox.Instance.ForceDisableLetterbox();
        }
        if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.StopAllCoroutines();
        if (LondonAlleyQuestManager.Instance != null) LondonAlleyQuestManager.Instance.StopAllCoroutines();

        CiraCoreVisionScanner.IsScannerBlocked = false;
        if (CiraCoreVisionScanner.Instance != null) CiraCoreVisionScanner.Instance.ForceCloseScanMode();

        if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
        if (LondonAlleyQuestManager.Instance != null) LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
        if (RoyalLyceumTheaterQuestManager.Instance != null) RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(false);
        if (UndergroundStationQuestManager.Instance != null) UndergroundStationQuestManager.Instance.ShowObjectiveHUD(false);
    }

    private static void StopAllCinematics()
    {
        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.StopAllCoroutines();
            CinematicCutsceneManager.Instance.ResetAllCutsceneCameras();
            CinematicCutsceneManager.Instance.SetPlayerControlsLocked(false);
        }
    }

    private static void Teleport(Vector3 groundPosition, float yaw)
    {
        var body = FindPlayerBody();
        if (body == null) return;

        var cc = body.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        body.position = groundPosition + Vector3.up * 0.1f; // เผื่อเหนือพื้นเล็กน้อย ไม่ให้จมแล้วร่วง
        body.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cc != null) cc.enabled = true;
        Physics.SyncTransforms();

        if (CinematicCutsceneManager.Instance != null && CinematicCutsceneManager.Instance.playerFollowCamera != null)
        {
            CinematicCutsceneManager.Instance.playerFollowCamera.PreviousStateIsValid = false;
        }
    }

    private IEnumerator JumpRoutine(int scene)
    {
        _busy = true;
        ClearEverything();
        yield return null;

        if (scene == 3)
        {
            var q = RoyalLyceumTheaterQuestManager.Instance;
            if (q != null)
            {
                q.DebugResetForReplay();
                q.ActivateQuest3();
                Teleport(new Vector3(20.0f, 0.24f, -87.76f), 90f); // พื้นในโรงละคร (THEATER) สูง 0.24 ไม่ใช่ถนน -0.01
                DetectiveThoughtToast.Show("[DEBUG] กระโดดไปฉาก 3: โรงละครรอยัลไลเซียม  (กด P ไปฉาก 4, L ไปฉากจบ)");
                yield return new WaitForSeconds(0.8f);
                q.OnPlayerEnteredTheater();
            }
        }
        else if (scene == 5)
        {
            DetectiveThoughtToast.Show("[DEBUG] ฉากจบของเกม");
            GameEndingSequence.Play();
        }
        else
        {
            var q = UndergroundStationQuestManager.Instance;
            if (q != null)
            {
                q.DebugResetForReplay();
                q.ActivateQuest4();
                Teleport(new Vector3(-41.50f, -9.76f, 43.00f), 45f);
                DetectiveThoughtToast.Show("[DEBUG] กระโดดไปฉาก 4: สถานีรถไฟใต้ดิน  (กด O ไปฉาก 3, L ไปฉากจบ)");
                yield return new WaitForSeconds(0.8f);
                q.OnPlayerEnteredStation();
            }
        }

        // กัน HUD ของเควสเก่าโผล่ซ้อน: ปิดซ้ำอยู่ช่วงหนึ่ง
        float until = Time.unscaledTime + 4f;
        while (Time.unscaledTime < until)
        {
            if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
            if (LondonAlleyQuestManager.Instance != null) LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
            if (scene == 4 && RoyalLyceumTheaterQuestManager.Instance != null) RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(false);
            if (scene == 3 && UndergroundStationQuestManager.Instance != null) UndergroundStationQuestManager.Instance.ShowObjectiveHUD(false);
            yield return null;
        }

        _busy = false;
    }
#endif
}
