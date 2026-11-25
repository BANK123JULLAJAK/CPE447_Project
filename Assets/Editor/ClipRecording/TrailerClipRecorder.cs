using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;

/// <summary>
/// อัดคลิปสำหรับตัดวิดีโอ (Editor เท่านั้น ไม่ติดไปกับ Build)
/// เมนู Tools/Trailer Clips/... หรือเรียก TrailerClipRecorder.Record(1|2|6|7)
/// ไฟล์ออกที่ <Project>/Recordings/clipN_*.mp4  (1920x1080, 30fps)
/// </summary>
[InitializeOnLoad]
public static class TrailerClipRecorder
{
    private const string PendingKey = "TrailerClipRecorder.Pending";
    public const int Fps = 30;

    static TrailerClipRecorder()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Trailer Clips/1 - London Evidence (15s)")] private static void M1() => Record(1);
    [MenuItem("Tools/Trailer Clips/2 - Explore Walk (20s)")] private static void M2() => Record(2);
    [MenuItem("Tools/Trailer Clips/6 - AI Scan Test (45s)")] private static void M6() => Record(6);
    [MenuItem("Tools/Trailer Clips/7 - Hacked Ending (15s)")] private static void M7() => Record(7);

    public static void Record(int clip)
    {
        SessionState.SetInt(PendingKey, clip);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false; // จะเข้าใหม่หลังออกจาก Play
        else EditorApplication.isPlaying = true;
    }

    public static string LastResult
    {
        get => SessionState.GetString("TrailerClipRecorder.Last", "");
        set => SessionState.SetString("TrailerClipRecorder.Last", value);
    }

    private static void OnPlayModeChanged(PlayModeStateChange s)
    {
        int clip = SessionState.GetInt(PendingKey, 0);
        if (clip == 0) return;
        if (s == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.isPlaying = true;
        }
        else if (s == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetInt(PendingKey, 0);
            var go = new GameObject("TrailerClipDirector");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<TrailerClipDirector>().clip = clip;
        }
    }

    public static string OutputPath(string fileName)
    {
        string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Recordings"));
        System.IO.Directory.CreateDirectory(dir);
        for (int take = 1; ; take++)
        {
            string p = System.IO.Path.Combine(dir, fileName + (take == 1 ? "" : "_take" + take) + ".mp4");
            if (!System.IO.File.Exists(p)) return p;
            try { using (System.IO.File.Open(p, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None)) { } return p; }
            catch (System.IO.IOException) { } // ไฟล์ถูกล็อกจากการอัดที่ค้าง -> ใช้ชื่อใหม่
        }
    }
}

/// <summary>
/// อัดภาพจากกล้องหลักลง RenderTexture แล้วอ่านกลับแบบ synchronous ทุกเฟรม เข้ารหัส MP4 ด้วย MediaEncoder
/// (ไม่พึ่ง Game View จึงอัดได้แม้หน้าต่าง Unity ไม่ได้โฟกัส)
/// UI แบบ Screen Space Overlay จะถูกสลับเป็น Screen Space Camera ชั่วคราวเพื่อให้ติดในภาพ
/// </summary>
public class ClipCapture : IDisposable
{
    private readonly RenderTexture _rt;
    private readonly Texture2D _tex;
    private readonly MediaEncoder _enc;
    private readonly bool _withUI;
    private Camera _bound;
    private readonly System.Collections.Generic.Dictionary<Canvas, RenderMode> _changed = new System.Collections.Generic.Dictionary<Canvas, RenderMode>();
    public int Frames { get; private set; }
    private readonly float _oldFixed;

    public ClipCapture(string path, int w, int h, int fps, bool withUI)
    {
        _withUI = withUI;
        _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        _rt.Create();
        _tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var attr = new VideoTrackAttributes
        {
            frameRate = new MediaRational(fps),
            width = (uint)w,
            height = (uint)h,
            includeAlpha = false,
            bitRateMode = VideoBitrateMode.High,
        };
        _enc = new MediaEncoder(path, attr);
        UnityEngine.Rendering.RenderPipelineManager.endContextRendering += OnEndContext;
        Time.captureFramerate = fps;
        _oldFixed = Time.fixedDeltaTime;
        Time.fixedDeltaTime = 1f / fps; // EditorApplication.Step เดินเวลาเท่ากับ fixedDeltaTime
    }

    /// <summary>เรียกทุกเฟรมก่อนเรนเดอร์ ให้กล้องหลักเรนเดอร์ลง RT และ UI ติดไปด้วย</summary>
    public void Bind()
    {
        var cam = Camera.main;
        if (cam != _bound)
        {
            if (_bound != null) _bound.targetTexture = null;
            _bound = cam;
        }
        if (cam == null) return;
        cam.targetTexture = _rt;
        if (!_withUI) return;
        foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
        {
            if (!c.isRootCanvas) continue;
            if (c.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _changed[c] = RenderMode.ScreenSpaceOverlay;
                c.renderMode = RenderMode.ScreenSpaceCamera;
            }
            if (_changed.ContainsKey(c))
            {
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.02f + (1000 - Mathf.Clamp(c.sortingOrder, -1000, 1000)) * 0.0001f;
            }
        }
    }

    private int _armedFrame = -1, _grabbedFrame = -1;

    /// <summary>ให้เก็บภาพของเฟรมนี้หลังเรนเดอร์เสร็จ</summary>
    public void Arm() { _armedFrame = Time.frameCount; }

    private void OnEndContext(UnityEngine.Rendering.ScriptableRenderContext ctx, System.Collections.Generic.List<Camera> cams)
    {
        if (_armedFrame != Time.frameCount || _grabbedFrame == Time.frameCount) return;
        if (_bound == null || !cams.Contains(_bound)) return;
        _grabbedFrame = Time.frameCount;
        Grab();
    }

    private void Grab()
    {
        var prev = RenderTexture.active;
        RenderTexture.active = _rt;
        _tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0, false);
        RenderTexture.active = prev;
        _enc.AddFrame(_tex);
        Frames++;
    }

    public void Dispose()
    {
        UnityEngine.Rendering.RenderPipelineManager.endContextRendering -= OnEndContext;
        Time.captureFramerate = 0;
        Time.fixedDeltaTime = _oldFixed;
        if (_bound != null) _bound.targetTexture = null;
        foreach (var kv in _changed) if (kv.Key != null) kv.Key.renderMode = kv.Value;
        _enc.Dispose();
        _rt.Release();
        UnityEngine.Object.Destroy(_tex);
    }
}

/// <summary>คุมกล้อง/ตัวละคร/ฉากระหว่างอัดคลิป</summary>
public class TrailerClipDirector : MonoBehaviour
{
    public int clip;
    private Camera _cam;
    private bool _hideUI;
    private ClipCapture _cap;

    // ถ้าออกจาก Play กลางคัน ต้องปิดไฟล์วิดีโอ ไม่งั้นไฟล์ถูกล็อกค้าง
    private void OnDestroy()
    {
        if (_cap != null) { try { _cap.Dispose(); } catch { } _cap = null; }
    }
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private IEnumerator Start()
    {
        // ให้ฉากเริ่มทำงานก่อน (Start ของ Quest Manager, เมนูหน้าแรก ฯลฯ)
        for (int i = 0; i < 20; i++) yield return null;
        ClearToGameplay();
        for (int i = 0; i < 10; i++) yield return null;
        _cam = Camera.main;

        string name; float seconds; IEnumerator shot;
        switch (clip)
        {
            case 1: name = "clip1_london_evidence"; seconds = 15f; shot = Clip1(); _hideUI = true; break;
            case 2: name = "clip2_explore_walk"; seconds = 20f; shot = Clip2(); _hideUI = true; break;
            case 6: name = "clip6_ai_scan_test"; seconds = 45f; shot = Clip6(); break;
            default: name = "clip7_hacked_ending"; seconds = 15f; shot = Clip7(); break;
        }

        if (_hideUI) SetCanvases(false);
        var setup = clip == 6 ? Clip6Setup() : clip == 2 ? Clip2Setup() : null;
        if (setup != null) yield return setup;

        _cap = new ClipCapture(TrailerClipRecorder.OutputPath(name), 1920, 1080, TrailerClipRecorder.Fps, !_hideUI);
        var cap = _cap;
        StartCoroutine(shot);
        int frames = Mathf.RoundToInt(seconds * TrailerClipRecorder.Fps);
        int startFrame = Time.frameCount;
        while (cap.Frames < frames && Time.frameCount - startFrame < frames * 2)
        {
            if (_hideUI) SetCanvases(false);
            cap.Bind();
            cap.Arm();
            yield return null;
        }
        cap.Dispose();
        _cap = null;
        TrailerClipRecorder.LastResult = name + ".mp4 done";
        Debug.Log("[TrailerClip] recorded " + name);
        yield return null;
        EditorApplication.isPlaying = false;
    }

    // ---------------- helpers ----------------
    private static void ClearToGameplay()
    {
        var m = typeof(DebugSceneSkip).GetMethod("ClearEverything", Any);
        if (m != null) m.Invoke(null, null);
        var title = FindAnyObjectByType<TitleMenuUI>();
        if (title != null) Destroy(title.gameObject);
        Time.timeScale = 1f;
    }

    private static void SetCanvases(bool on)
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            if (c.isRootCanvas) c.enabled = on;
    }

    private void FreeCamera()
    {
        _cam = Camera.main;
        foreach (var b in _cam.GetComponents<Behaviour>())
            if (b != _cam && !(b is AudioListener) && b.GetType().Name != "UniversalAdditionalCameraData") b.enabled = false;
        var endfield = FindAnyObjectByType<EndfieldCameraController>();
        if (endfield != null) endfield.enabled = false;
    }

    private IEnumerator Dolly(Vector3 fromPos, Vector3 toPos, Vector3 fromLook, Vector3 toLook, float dur)
    {
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / dur);
            _cam.transform.position = Vector3.Lerp(fromPos, toPos, k);
            _cam.transform.LookAt(Vector3.Lerp(fromLook, toLook, k));
            yield return null;
        }
    }

    private static Transform PlayerBody()
    {
        var cc = FindAnyObjectByType<CharacterController>();
        return cc != null ? cc.transform : null;
    }

    private static void TeleportPlayer(Vector3 pos, float yaw)
    {
        var body = PlayerBody();
        if (body == null) return;
        var cc = body.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        body.position = pos;
        body.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cc != null) cc.enabled = true;
        Physics.SyncTransforms();
    }

    private static Vector3 EvidencePos(string goName)
    {
        var go = GameObject.Find(goName);
        if (go == null) return Vector3.zero;
        var ev = go.GetComponent<EvidenceItem>();
        return ev != null ? ev.GetWorldCenter() : go.transform.position;
    }

    // ---------------- Clip 1: London + evidence (15s) ----------------
    private IEnumerator Clip1()
    {
        FreeCamera();
        // 1) มุมสูงเหนือถนนลอนดอนยามค่ำ เลื่อนลงช้า ๆ
        yield return Dolly(new Vector3(1.5f, 22f, 62f), new Vector3(1.5f, 9f, 40f), new Vector3(1.5f, 0f, 10f), new Vector3(1.5f, 2f, -20f), 5f);
        // 2) เลื่อนต่ำเลียบที่เกิดเหตุในตรอก (ศพ รอยเท้าเปื้อนเลือด ตั๋ว กระดุม)
        yield return Dolly(new Vector3(-12.6f, 1.15f, -98.7f), new Vector3(-19.2f, 1.0f, -98.9f), new Vector3(-14.2f, 0.3f, -101.2f), new Vector3(-20.6f, 0.3f, -101.4f), 5f);
        // 3) ภาพใกล้แหวนสองวงหน้าโบสถ์เซนต์แมรี่ ค่อย ๆ วนเข้า
        Vector3 ring = new Vector3(15.65f, 0.25f, 37.85f);
        for (float t = 0f; t < 5f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 5f);
            float a = Mathf.Lerp(-70f, 10f, k) * Mathf.Deg2Rad;
            float r = Mathf.Lerp(0.75f, 0.42f, k);
            _cam.transform.position = ring + new Vector3(Mathf.Sin(a) * r, Mathf.Lerp(0.5f, 0.3f, k), -Mathf.Cos(a) * r);
            _cam.transform.LookAt(ring);
            yield return null;
        }
    }

    // ---------------- Clip 2: walk & explore (20s) ----------------
    private IEnumerator Clip2()
    {
        var body = PlayerBody();
        var inputs = body != null ? body.GetComponentInParent<StarterAssets.StarterAssetsInputs>() ?? body.GetComponentInChildren<StarterAssets.StarterAssetsInputs>() : null;
        var tpc = body != null ? body.GetComponent<StarterAssets.ThirdPersonController>() : null;
        var yawField = typeof(StarterAssets.ThirdPersonController).GetField("_cinemachineTargetYaw", Any);
        if (inputs == null) yield break;

        // เดินตามถนนสายหลัก มองซ้ายขวาสำรวจ แล้วเลี้ยวเข้าหาโบสถ์
        Vector3[] path = { new Vector3(11f, 0f, 14f), new Vector3(11f, 0f, -6f), new Vector3(11.3f, 0f, -20f), new Vector3(11f, 0f, -34f) }; // ทางเท้า ไม่ให้รถม้าบังกล้อง
        int idx = 0;
        float camYaw = 180f;
        for (float t = 0f; t < 20.5f; t += Time.deltaTime)
        {
            Vector3 p = body.position;
            Vector3 to = path[idx] - new Vector3(p.x, 0f, p.z); to.y = 0f;
            if (to.magnitude < 1.2f && idx < path.Length - 1) { idx++; continue; }
            float worldYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            // กล้องหมุนตามทิศเดินพร้อมเหลียวมองสองข้างทาง
            float lookAround = Mathf.Sin(t * 0.45f) * 24f;
            camYaw = Mathf.LerpAngle(camYaw, worldYaw + lookAround, Time.deltaTime * 1.5f);
            if (tpc != null && yawField != null) yawField.SetValue(tpc, camYaw);
            float rel = Mathf.DeltaAngle(camYaw, worldYaw) * Mathf.Deg2Rad;
            bool pause = t > 9f && t < 11f; // หยุดยืนมองชั่วครู่
            inputs.move = pause ? Vector2.zero : new Vector2(Mathf.Sin(rel), Mathf.Cos(rel));
            inputs.look = Vector2.zero;
            inputs.sprint = false;
            yield return null;
        }
        inputs.move = Vector2.zero;
    }

    private IEnumerator Clip2Setup()
    {
        TeleportPlayer(new Vector3(11f, 0.1f, 30f), 180f);
        yield return null;
    }

    // ---------------- Clip 6: AI scan test (45s) ----------------
    private IEnumerator Clip6Setup()
    {
        yield return Jump(3);
    }

    private IEnumerator Jump(int scene)
    {
        var skip = FindAnyObjectByType<DebugSceneSkip>();
        var m = typeof(DebugSceneSkip).GetMethod("JumpRoutine", Any);
        if (skip != null && m != null) skip.StartCoroutine((IEnumerator)m.Invoke(skip, new object[] { scene }));
        for (float t = 0f; t < 1.6f; t += Time.deltaTime) { DetectiveThoughtToast.Clear(); yield return null; }
        DetectiveThoughtToast.Clear();
        if (CinematicCutsceneManager.Instance != null)
        {
            CinematicCutsceneManager.Instance.StopAllCoroutines();
            CinematicCutsceneManager.Instance.ResetAllCutsceneCameras();
            CinematicCutsceneManager.Instance.SetPlayerControlsLocked(false);
        }
        if (CinematicLetterbox.Instance != null) CinematicLetterbox.Instance.ForceDisableLetterbox();
        CiraCoreVisionScanner.IsScannerBlocked = false;
    }

    // มุมกล้องที่จูนเองสำหรับหลักฐานที่อยู่ชิดม่าน/ซอกเก้าอี้
    private static readonly System.Collections.Generic.Dictionary<string, Vector3> CameraSideOverride = new System.Collections.Generic.Dictionary<string, Vector3>
    {
        { "THEATER_EVIDENCE_THEATERMASK", new Vector3(0.375f, 0f, 0.93f) },
        { "THEATER_EVIDENCE_WALKINGCANE", new Vector3(0.3f, 0f, 1f) },
        { "THEATER_EVIDENCE_ACTORCOSTUMESCRAP", new Vector3(0.12f, 0f, -0.98f) },
    };
    private static readonly System.Collections.Generic.Dictionary<string, float> CameraHeightOverride = new System.Collections.Generic.Dictionary<string, float>
    {
        { "THEATER_EVIDENCE_ACTORCOSTUMESCRAP", 2.1f },
    };

    /// <summary>หาทิศวางกล้องที่ไม่จมในผนัง/ม่าน และมองเห็นหลักฐานได้ตรง ๆ (ลองรอบตัว 12 ทิศ เริ่มจากทิศที่ต้องการ)</summary>
    private static Vector3 PickClearSide(Vector3 ev, Vector3 preferred)
    {
        Vector3 pref = new Vector3(preferred.x, 0f, preferred.z).normalized;
        Vector3 best = pref; float bestScore = float.MinValue;
        for (int i = 0; i < 12; i++)
        {
            float ang = (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 30f;
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * pref;
            Vector3 cam = ev + dir * 2.0f + Vector3.up * 1.45f;
            Vector3 stand = ev + dir * 1.1f + Vector3.Cross(Vector3.up, -dir).normalized * 0.95f + Vector3.up * 0.9f;
            float score = -Mathf.Abs(ang) * 0.01f;
            if (Physics.CheckSphere(cam, 0.35f, ~0, QueryTriggerInteraction.Ignore)) score -= 100f;
            if (Physics.CheckCapsule(stand, stand + Vector3.up * 0.8f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) score -= 10f;
            Vector3 to = ev - cam;
            foreach (var h in Physics.RaycastAll(cam, to.normalized, to.magnitude - 0.15f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.GetComponentInParent<EvidenceItem>() != null) continue;
                if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
                score -= 50f; break;
            }
            // เผื่อพื้นที่ระหว่างทาง (ม่าน/ผนังบาง ๆ ที่ไม่มีคอลไลเดอร์ ตรวจไม่ได้)
            if (score > bestScore) { bestScore = score; best = dir; }
        }
        return best;
    }

    private IEnumerator ScanSequence(string[] items, Vector3 playerSide)
    {
        var scanner = CiraCoreVisionScanner.Instance;
        Vector3 lastPos = _cam.transform.position, lastLook = _cam.transform.position + _cam.transform.forward * 5f;
        foreach (var n in items)
        {
            Vector3 ev = EvidencePos(n);
            Vector3 side = CameraSideOverride.TryGetValue(n, out var ov) ? ov.normalized : PickClearSide(ev, playerSide);
            float camUp = CameraHeightOverride.TryGetValue(n, out var hv) ? hv : 1.45f;
            Vector3 face = -side;
            Vector3 right = Vector3.Cross(Vector3.up, face).normalized;
            float floorY = PlayerBody().position.y;
            // ตัวละครยืนเยื้องไปด้านขวา (เห็นไหล่ขอบจอ) ไม่บังหลักฐาน
            Vector3 standAt = new Vector3(ev.x, floorY, ev.z) + side * 1.1f + right * 0.95f;
            Vector3 camPos = new Vector3(ev.x, ev.y, ev.z) + side * 2.0f + Vector3.up * camUp;
            float yaw = Quaternion.LookRotation(new Vector3(ev.x - standAt.x, 0f, ev.z - standAt.z)).eulerAngles.y;
            scanner.ToggleScanMode(false);
            TeleportPlayer(standAt, yaw);
            yield return Dolly(lastPos, camPos, lastLook, ev, 0.7f);
            scanner.ToggleScanMode(true);
            for (float t = 0f; t < 3.4f; t += Time.deltaTime)
            {
                _cam.transform.position = camPos + new Vector3(Mathf.Sin(t * 0.8f), Mathf.Sin(t * 1.1f) * 0.4f, 0f) * 0.05f;
                _cam.transform.LookAt(ev);
                yield return null;
            }
            lastPos = camPos; lastLook = ev;
        }
        scanner.ToggleScanMode(false);
    }

    private IEnumerator Clip6()
    {
        FreeCamera();
        // ตัวละครในโรงละครยืนทับหลักฐาน -> ซ่อนไว้เฉพาะตอนอัดคลิปนี้
        foreach (var n in new[] { "Theater_Jack_The_Ripper", "Theater_Surviving_Victim" })
        {
            var npc = GameObject.Find(n);
            if (npc != null) npc.SetActive(false);
        }
        yield return ScanSequence(new[]
        {
            "THEATER_EVIDENCE_THEATERMASK", "THEATER_EVIDENCE_WALKINGCANE", "THEATER_EVIDENCE_PRIVATEMESSAGE",
            "THEATER_EVIDENCE_ACTORCOSTUMESCRAP", "THEATER_EVIDENCE_PERFORMENCEINVITATION",
        }, new Vector3(-1f, 0f, 0.4f));

        // ปิดกระดานอนุมานของฉาก 3 ที่เด้งขึ้นหลังสแกนครบ ก่อนข้ามไปฉาก 4
        if (RoyalLyceumTheaterQuestManager.Instance != null) RoyalLyceumTheaterQuestManager.Instance.StopAllCoroutines();
        CaseDeductionBoardUI.ForceClose();
        ChapterSummaryUI.ForceClose();
        yield return Jump(4);
        if (RoyalLyceumTheaterQuestManager.Instance != null) RoyalLyceumTheaterQuestManager.Instance.StopAllCoroutines();
        CaseDeductionBoardUI.ForceClose();
        ChapterSummaryUI.ForceClose();
        CiraCoreVisionScanner.IsScannerBlocked = false;
        FreeCamera();
        yield return ScanSequence(new[]
        {
            "TRAIN_EVIDENCE_BLOODSPLATTER", "TRAIN_EVIDENCE_POCKETWATCH", "TRAIN_EVIDENCE_RAILWAYTICKET",
            "TRAIN_EVIDENCE_SPENTCARTRIDGECASE", "TRAIN_EVIDENCE_STATIONNOTE",
        }, new Vector3(0.5f, 0f, -1f));
    }

    // ---------------- Clip 7: hacked ending (15s) ----------------
    private IEnumerator Clip7()
    {
        GameEndingSequence.Play();
        yield return null;
        var seq = FindAnyObjectByType<GameEndingSequence>();
        if (seq == null) yield break;
        var t = typeof(GameEndingSequence);
        seq.StopAllCoroutines();

        // แฟ้ม CASE CLOSED ชั่วครู่ -> ระบบถูกแฮ็ก -> เทอร์มินัลของ M
        var black = t.GetField("_black", Any)?.GetValue(seq) as UnityEngine.UI.Image;
        if (black != null) black.color = new Color(0f, 0f, 0f, 0f);
        IEnumerator Run(string method) => (IEnumerator)t.GetMethod(method, Any).Invoke(seq, null);
        seq.StartCoroutine(Chain(seq, Run("StampRoutine"), Run("HackRoutine"), Run("TerminalRoutine")));

        // ถ้าเทอร์มินัลถึงตอนเลือกคำตอบ ให้เลือก "มอริอาร์ตี้... ใช่ไหม" อัตโนมัติ
        var picked = t.GetField("_choicePicked", Any);
        float waiting = 0f;
        while (seq != null && picked != null)
        {
            if ((int)picked.GetValue(seq) < 0) { waiting += Time.deltaTime; if (waiting > 1.4f) { picked.SetValue(seq, 1); waiting = 0f; } }
            else waiting = 0f;
            yield return null;
        }
    }

    private static IEnumerator Chain(MonoBehaviour host, params IEnumerator[] steps)
    {
        foreach (var s in steps) yield return host.StartCoroutine(s);
    }
}
