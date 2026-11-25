using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// ฉากจบของเกม (ต่อจากหน้าสรุปบทที่ 4):
/// ช่วงที่ 1 แฟ้มคดีถูกประทับตรา "CASE CLOSED" พร้อมเสียงของโฮมส์สรุปคดี (แหวน 2 วงจากโบสถ์ = กุญแจของคดี, แจ็คเป็นแค่มือที่ลงมือ)
/// ช่วงที่ 2 ผู้เล่นกด Continue แล้วเมาส์ถูกล็อก เป้าสแกน CiRA CORE เปิดเอง สแกนคำว่า CASE CLOSED แล้วเปลี่ยนเป็น "ERROR: TARGET ESCAPED"
/// ช่วงที่ 3 UI พังกลายเป็นเทอร์มินัลดำ มอริอาร์ตี้พิมพ์ข้อความ ผู้เล่นเลือกคำตอบ แล้วระบบพิมพ์ให้เอง (มีแค่เสียงพิมพ์)
/// ช่วงที่ 4 ค้างประโยค "You solved the crime... but not the game." แล้วตัดกลับเมนูที่ปุ่ม Play กลายเป็น "Play Round 2?"
/// </summary>
public class GameEndingSequence : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }
    /// <summary>บันทึกว่าผู้เล่นจบรอบแรกแล้ว</summary>
    public const string Round2PrefKey = "Moriarty_Round2";
    /// <summary>เพิ่งเล่นจบแล้วกลับมาหน้าเมนูเริ่มเกม -> ปุ่ม Play เปลี่ยนเป็น "Play Round 2?" (เฉพาะรอบนั้น)</summary>
    public static bool ReturnedFromEnding;

    public static void Play(Action onComplete = null)
    {
        if (IsPlaying) return;
        var go = new GameObject("GameEndingSequence");
        DontDestroyOnLoad(go);
        go.AddComponent<GameEndingSequence>().Begin(onComplete);
    }

    // ==========================================
    // บท
    // ==========================================
    private static readonly string[] HolmesLines =
    {
        "คดีนี้ปิดลงแล้ว สารวัตร คุณจับมือที่ลงมือได้สำเร็จ",
        "แต่กุญแจที่แท้จริงอยู่ตั้งแต่ต้น แหวนสองวงในโบสถ์เซนต์แมรี่",
        "วงใหญ่กับวงเล็ก ตราเดียวกัน มันเผยต้นกำเนิดของแจ็ค และแรงจูงใจที่ฝังลึกอยู่ในอดีตของเขา",
        "แจ็คถูกต้อนจนมุม แต่เขาเป็นเพียงมือที่ลงมือ",
        "ผู้กำหนดเวทีทั้งหมด ตั้งแต่โบสถ์ ตรอก โรงละคร จนถึงสถานีปลายทาง คือศาสตราจารย์มอริอาร์ตี้",
    };

    private class Choice
    {
        public string text;
        public string[] reply;
        public Choice(string text, params string[] reply) { this.text = text; this.reply = reply; }
    }

    private const string FinalLine = "You solved the crime... but not the game.";

    // ==========================================
    // UI refs
    // ==========================================
    private Action _onComplete;
    private RectTransform _stage;
    private RectTransform _folderRect;
    private RectTransform _stampRect;
    private Text _stampMain;
    private Text _stampSub;
    private UIParallelogramGraphic _stampOuter;
    private UIParallelogramGraphic _stampInner;
    private GameObject _subBar;
    private Text _subSpeaker;
    private Text _subText;
    private GameObject _continueRow;
    private RectTransform _reticle;
    private Image _reticleFill;
    private Text _reticleLabel;
    private Image _flash;
    private Image _black;
    private RectTransform _terminal;
    private RectTransform _termLog;
    private RectTransform _termChoices;
    private readonly List<Image> _glitchBars = new List<Image>();

    private Font _thai;
    private Font _thaiBold;
    private Font _mono;
    private Texture2D _paperTex;
    private Texture2D _folderTex;

    private bool _continuePressed;
    private int _choicePicked = -1;
    private float _termY;

    private static readonly Color Brass = new Color(0.86f, 0.70f, 0.36f, 1f);
    private static readonly Color Cream = new Color(0.97f, 0.93f, 0.83f, 1f);
    private static readonly Color Ink = new Color(0.14f, 0.10f, 0.07f, 1f);
    private static readonly Color StampInk = new Color(0.72f, 0.09f, 0.09f, 0.9f);
    private static readonly Color ScanGreen = new Color(0.46f, 0.86f, 0.56f, 1f);
    private static readonly Color TermGreen = new Color(0.36f, 1f, 0.52f, 1f);
    private static readonly Color TermDim = new Color(0.22f, 0.62f, 0.32f, 1f);
    private static readonly Color TermAmber = new Color(1f, 0.78f, 0.35f, 1f);

    private void Begin(Action onComplete)
    {
        IsPlaying = true;
        _onComplete = onComplete;

        _thai = GameAssets.Load<Font>("Assets/Font/Sarabun-Medium.ttf");
        _thaiBold = GameAssets.Load<Font>("Assets/Font/Prompt-Bold.ttf");
        _paperTex = GameAssets.Load<Texture2D>("Assets/UI/Folder/Gemini_Generated_Image_oht62eoht62eoht6-Photoroom.png");
        _folderTex = GameAssets.Load<Texture2D>("Assets/UI/Folder/Gemini_Generated_Image_jds4szjds4szjds4-Photoroom.png");
        if (_thai == null) _thai = VisionOSUITheme.PrimaryFont;
        if (_thaiBold == null) _thaiBold = VisionOSUITheme.BoldFont;
        _mono = Font.CreateDynamicFontFromOSFont(new[] { "Courier New", "Consolas" }, 24);
        if (_mono == null) _mono = _thaiBold;

        BuildUI();
        StartCoroutine(MainRoutine());
    }

    private void OnDestroy()
    {
        IsPlaying = false;
    }

    // ==========================================
    // Flow
    // ==========================================
    private IEnumerator MainRoutine()
    {
        LockGameplay();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return Fade(_black, 1f, 0f, 1.2f);
        yield return new WaitForSecondsRealtime(0.5f);

        // ----- ช่วงที่ 1: CASE CLOSED + เสียงโฮมส์ -----
        yield return StampRoutine();
        yield return new WaitForSecondsRealtime(0.6f);

        _subBar.SetActive(true);
        foreach (var line in HolmesLines) yield return Subtitle("เชอร์ล็อก โฮมส์", line);

        _continueRow.SetActive(true);
        _continuePressed = false;
        while (!_continuePressed)
        {
            if (PressedAdvance()) _continuePressed = true;
            yield return null;
        }
        _continueRow.SetActive(false);
        _subBar.SetActive(false);

        // ----- ช่วงที่ 2: ระบบถูกแฮ็ก -----
        yield return HackRoutine();

        // ----- ช่วงที่ 3: เทอร์มินัลของมอริอาร์ตี้ (เลือกคำตอบ แล้วพิมพ์เอง) -----
        yield return TerminalRoutine();

        // ----- End Credits -----
        yield return CreditsRoutine();

        if (_onComplete != null)
        {
            Finish();
            yield break;
        }

        // ----- ช่วงที่ 4: กลับหน้าเมนูเริ่มเกมโดยตรง (ปุ่ม Play กลายเป็น "Play Round 2?") -----
        PlayerPrefs.SetInt(Round2PrefKey, 1);
        PlayerPrefs.Save();
        ReturnedFromEnding = true;
        Time.timeScale = 1f;
        CiraCoreVisionScanner.IsScannerBlocked = false;
        IsPlaying = false;
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;
        Destroy(gameObject);
        SceneManager.LoadScene(sceneIndex < 0 ? 0 : sceneIndex);
    }

    private IEnumerator StampRoutine()
    {
        _stampRect.gameObject.SetActive(true);
        var g = _stampRect.GetComponent<CanvasGroup>();
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / 0.2f);
            _stampRect.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, k * k);
            g.alpha = k;
            yield return null;
        }
        _stampRect.localScale = Vector3.one;
        g.alpha = 1f;
        CutsceneFX.PlayThud(CamPos(), 0.9f);

        float s = 0f;
        Vector2 basePos = _folderRect.anchoredPosition;
        while (s < 0.3f)
        {
            s += Time.unscaledDeltaTime;
            float k = 1f - s / 0.3f;
            _folderRect.anchoredPosition = basePos + new Vector2(UnityEngine.Random.Range(-7f, 7f), UnityEngine.Random.Range(-5f, 5f)) * k;
            _flash.color = new Color(1f, 0.95f, 0.85f, 0.18f * k);
            yield return null;
        }
        _folderRect.anchoredPosition = basePos;
        _flash.color = new Color(1f, 1f, 1f, 0f);
    }

    private IEnumerator Subtitle(string speaker, string line)
    {
        _subSpeaker.text = speaker;
        _subText.text = "";
        const float cps = 42f;
        float e = 0f;
        int shown = 0;
        bool skip = false;
        while (shown < line.Length)
        {
            e += Time.unscaledDeltaTime;
            int n = skip ? line.Length : Mathf.Min(line.Length, Mathf.FloorToInt(e * cps));
            if (n != shown) { shown = n; _subText.text = line.Substring(0, shown); }
            if (PressedAdvance()) skip = true;
            yield return null;
        }
        yield return null;
        float hold = Mathf.Clamp(line.Length * 0.045f, 1.6f, 3.6f);
        float w = 0f;
        while (w < hold)
        {
            w += Time.unscaledDeltaTime;
            if (PressedAdvance()) break;
            yield return null;
        }
    }

    // ==========================================
    // ช่วงที่ 2: แฮ็ก
    // ==========================================
    private IEnumerator HackRoutine()
    {
        // เมาส์ถูกล็อก ผู้เล่นขยับไม่ได้
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        yield return Glitch(0.35f, 0.5f);

        _reticle.gameObject.SetActive(true);
        _reticleLabel.text = "CiRA CORE  ·  REMOTE ACCESS";
        _reticleLabel.color = ScanGreen;
        _reticleFill.fillAmount = 0f;
        _reticle.anchoredPosition = new Vector2(-620f, -260f);

        Vector2 stampPos = _folderRect.anchoredPosition + _stampRect.anchoredPosition;
        Vector2[] path = { new Vector2(-300f, 220f), new Vector2(560f, 300f), new Vector2(-420f, -40f), new Vector2(160f, -260f), stampPos };
        for (int i = 0; i < path.Length; i++)
        {
            bool last = i == path.Length - 1;
            float dur = last ? 0.9f : UnityEngine.Random.Range(0.4f, 0.65f);
            Vector2 start = _reticle.anchoredPosition;
            float e = 0f;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / dur));
                Vector2 jitter = last ? Vector2.zero : new Vector2(Mathf.Sin(Time.unscaledTime * 31f), Mathf.Cos(Time.unscaledTime * 27f)) * 8f;
                _reticle.anchoredPosition = Vector2.Lerp(start, path[i], k) + jitter;
                yield return null;
            }
        }

        _reticleLabel.text = "ANALYZING  \"CASE CLOSED\"";
        float p = 0f;
        while (p < 1.5f)
        {
            p += Time.unscaledDeltaTime;
            _reticleFill.fillAmount = Mathf.Clamp01(p / 1.5f);
            _reticle.anchoredPosition = stampPos + new Vector2(Mathf.Sin(Time.unscaledTime * 40f), Mathf.Cos(Time.unscaledTime * 36f)) * 2f;
            yield return null;
        }
        _reticleLabel.text = "RESULT  ·  MISMATCH";
        yield return new WaitForSecondsRealtime(0.5f);

        // CASE CLOSED -> ERROR: TARGET ESCAPED (แดงกะพริบ)
        _stampSub.text = "ERROR";
        _stampMain.text = "TARGET ESCAPED";
        _stampMain.fontSize = 58;
        _reticleLabel.text = "TARGET ESCAPED";
        _reticleLabel.color = new Color(1f, 0.3f, 0.3f, 1f);
        float b = 0f;
        while (b < 2.4f)
        {
            b += Time.unscaledDeltaTime;
            bool on = Mathf.FloorToInt(b * 6f) % 2 == 0;
            Color c = on ? new Color(1f, 0.15f, 0.12f, 1f) : new Color(0.5f, 0.04f, 0.04f, 0.85f);
            _stampMain.color = c;
            _stampSub.color = c;
            _stampOuter.outlineColor = c;
            _stampInner.outlineColor = c;
            _stampOuter.SetVerticesDirty();
            _stampInner.SetVerticesDirty();
            if (b > 1.4f && UnityEngine.Random.value < 0.35f) GlitchFrame(0.6f);
            yield return null;
        }

        // UI พัง
        yield return Glitch(1.2f, 1f);
        _reticle.gameObject.SetActive(false);
        _stage.gameObject.SetActive(false);
        _black.color = Color.black;
        yield return new WaitForSecondsRealtime(0.8f);
    }

    private void GlitchFrame(float intensity)
    {
        foreach (var bar in _glitchBars)
        {
            bool show = UnityEngine.Random.value < 0.5f * intensity;
            bar.gameObject.SetActive(show);
            if (!show) continue;
            bar.rectTransform.sizeDelta = new Vector2(UnityEngine.Random.Range(200f, 1920f), UnityEngine.Random.Range(4f, 70f));
            bar.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-960f, 960f), UnityEngine.Random.Range(-540f, 540f));
            float r = UnityEngine.Random.value;
            bar.color = r < 0.33f ? new Color(1f, 0.15f, 0.15f, 0.55f) : (r < 0.66f ? new Color(0.3f, 1f, 0.55f, 0.45f) : new Color(1f, 1f, 1f, 0.6f));
        }
        _stage.anchoredPosition = new Vector2(UnityEngine.Random.Range(-24f, 24f), UnityEngine.Random.Range(-10f, 10f)) * intensity;
    }

    private IEnumerator Glitch(float duration, float intensity)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            if (Time.frameCount % 2 == 0) GlitchFrame(intensity * Mathf.Lerp(0.5f, 1f, t / duration));
            yield return null;
        }
        foreach (var bar in _glitchBars) bar.gameObject.SetActive(false);
        _stage.anchoredPosition = Vector2.zero;
    }

    // ==========================================
    // ช่วงที่ 3: เทอร์มินัล
    // ==========================================
    private IEnumerator TerminalRoutine()
    {
        _terminal.gameObject.SetActive(true);
        _termY = 0f;
        yield return new WaitForSecondsRealtime(0.5f);

        yield return TermLine("SYS", "remote session established  ::  cira_core.vision", TermDim, true, 70f);
        yield return TermLine("SYS", "operator override  ::  user \"M\" has taken control", TermDim, true, 70f);
        yield return new WaitForSecondsRealtime(0.5f);
        yield return TermLine("M", "ซอฟต์แวร์วิเคราะห์ภาพของคุณทำงานได้ดีมาก... ผมคงต้องชมคนเขียนโค้ดสักหน่อย", TermGreen, false, 26f);

        // รอบที่ 1: ผู้เล่นเลือกคำตอบ ระบบพิมพ์ให้เอง
        yield return AskAndAnswer(new[]
        {
            new Choice("คุณคือใคร?", "ชื่อไม่สำคัญหรอก สารวัตร... สิ่งที่สำคัญคือคุณเดินตามทางที่ผมปูไว้ได้ไม่พลาดสักก้าว"),
            new Choice("มอริอาร์ตี้... ใช่ไหม", "ฉลาดเสมอ... แต่การรู้ชื่อผม ไม่ได้ทำให้คุณจับผมได้"),
            new Choice("ตัดการเชื่อมต่อ", "#access denied", "ยังไม่ถึงเวลาวางสายครับ สารวัตร"),
        });

        yield return TermLine("M", "คุณจับเบี้ยของผมได้... แต่นั่นก็เป็นไปตามที่ผมคำนวณไว้ตั้งแต่แรก", TermGreen, false, 26f);

        // รอบที่ 2
        yield return AskAndAnswer(new[]
        {
            new Choice("เกมนี้ยังไม่จบ", "ถูกต้อง... มันเพิ่งเริ่มต้นเท่านั้น"),
            new Choice("แกหนีไปได้ไม่ตลอดหรอก", "ผมไม่เคยหนี สารวัตร ผมแค่ยืนอยู่หนึ่งก้าวข้างหน้าคุณเสมอ"),
        });

        yield return new WaitForSecondsRealtime(1.2f);

        // ล้างจอ แล้วค้างประโยคสุดท้าย
        for (int i = _termLog.childCount - 1; i >= 0; i--) Destroy(_termLog.GetChild(i).gameObject);
        yield return new WaitForSecondsRealtime(0.6f);
        var final = MakeText("Final", _terminal, "", _mono, 54, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1800f, 90f), null);
        yield return Type(final, FinalLine, 20f, true);
        float hold = 0f;
        while (hold < 3f)
        {
            hold += Time.unscaledDeltaTime;
            final.text = FinalLine + (Mathf.FloorToInt(hold * 2f) % 2 == 0 ? "_" : " ");
            yield return null;
        }
    }

    private IEnumerator AskAndAnswer(Choice[] choices)
    {
        yield return new WaitForSecondsRealtime(0.4f);
        _choicePicked = -1;
        ShowChoices(choices);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        while (_choicePicked < 0)
        {
            int key = PressedNumber();
            if (key >= 0 && key < choices.Length) _choicePicked = key;
            yield return null;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        ClearChoices();

        var c = choices[_choicePicked];
        // ผู้เล่นเลือกแล้ว ระบบพิมพ์คำตอบให้เอง
        yield return TermLine("NEWMAN", c.text, TermAmber, false, 30f);
        yield return new WaitForSecondsRealtime(0.5f);
        foreach (var r in c.reply)
        {
            if (r.StartsWith("#")) yield return TermLine("SYS", r.Substring(1), new Color(1f, 0.35f, 0.3f, 1f), true, 60f);
            else yield return TermLine("M", r, TermGreen, false, 26f);
            yield return new WaitForSecondsRealtime(0.35f);
        }
    }

    private void ShowChoices(Choice[] choices)
    {
        ClearChoices();
        for (int i = 0; i < choices.Length; i++)
        {
            int idx = i;
            float y = -i * 58f;
            var row = NewRect("Choice_" + i, _termChoices, new Vector2(0f, y), new Vector2(1500f, 50f));
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = new Color(TermGreen.r, TermGreen.g, TermGreen.b, 0f);
            var btn = row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { if (_choicePicked < 0) _choicePicked = idx; });
            var label = MakeText("Label", row, $"[{i + 1}]  {choices[i].text}", _thai, 30, TermGreen, TextAnchor.MiddleLeft,
                new Vector2(-730f, 0f), new Vector2(1460f, 50f), new Vector2(0f, 0.5f));
            var hover = row.gameObject.AddComponent<StationHoverHandler>();
            hover.onEnter = () => { hit.color = new Color(TermGreen.r, TermGreen.g, TermGreen.b, 0.14f); label.color = Color.white; };
            hover.onExit = () => { hit.color = new Color(TermGreen.r, TermGreen.g, TermGreen.b, 0f); label.color = TermGreen; };
        }
        _termChoices.gameObject.SetActive(true);
    }

    private void ClearChoices()
    {
        for (int i = _termChoices.childCount - 1; i >= 0; i--)
        {
            var child = _termChoices.GetChild(i);
            if (child.name.StartsWith("Choice_")) Destroy(child.gameObject);
        }
        _termChoices.gameObject.SetActive(false);
    }

    private IEnumerator TermLine(string who, string text, Color color, bool mono, float cps)
    {
        string prefix = who == "SYS" ? ">" : (who == "M" ? "M  >" : "NEWMAN  >");
        MakeText("Tag", _termLog, prefix, _mono, 24, who == "NEWMAN" ? TermAmber : TermDim, TextAnchor.UpperLeft,
            new Vector2(-860f, _termY - 4f), new Vector2(200f, 40f), new Vector2(0f, 1f));
        float textX = who == "SYS" ? -830f : (who == "M" ? -770f : -690f);
        float width = 860f - textX;
        var t = MakeText("Line", _termLog, "", mono ? _mono : _thai, mono ? 24 : 30, color, TextAnchor.UpperLeft,
            new Vector2(textX, _termY), new Vector2(width, 80f), new Vector2(0f, 1f));
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        yield return Type(t, text, cps, false);

        // บรรทัดถัดไปเว้นตามความสูงจริงของข้อความ
        float h = Mathf.Max(34f, t.preferredHeight);
        _termY -= h + (who == "SYS" ? 8f : 18f);
        if (_termY < -640f) ScrollLog(-640f - _termY);
    }

    private void ScrollLog(float amount)
    {
        for (int i = 0; i < _termLog.childCount; i++)
        {
            var r = (RectTransform)_termLog.GetChild(i);
            r.anchoredPosition += new Vector2(0f, amount);
            if (r.anchoredPosition.y > 30f) r.gameObject.SetActive(false);
        }
        _termY += amount;
    }

    private IEnumerator Type(Text target, string line, float cps, bool cursor)
    {
        float e = 0f;
        int shown = 0;
        while (shown < line.Length)
        {
            e += Time.unscaledDeltaTime;
            int n = Mathf.Min(line.Length, Mathf.FloorToInt(e * cps));
            if (n != shown)
            {
                if (n / 2 != shown / 2) CutsceneFX.PlayTick(CamPos(), 0.16f);
                shown = n;
                target.text = line.Substring(0, shown) + (cursor ? "_" : "");
            }
            yield return null;
        }
        target.text = line;
    }

    // ==========================================
    // End Credits
    // ==========================================
    private class CreditRole
    {
        public string role;
        public string[] ids;
        public string[] names;
        public CreditRole(string role, string[] ids, string[] names) { this.role = role; this.ids = ids; this.names = names; }
    }

    private static readonly CreditRole[] Roles =
    {
        new CreditRole("AI  ·  CiRA CORE", new[] { "66002666" }, new[] { "ธัญวรัตน์ จันทร์โหนง" }),
        new CreditRole("3D MODEL", new[] { "66003065", "66015312" }, new[] { "ประไพพิศ สวัสดิ์รักษา", "ชนัญญา เถระ" }),
        new CreditRole("GAME DEVELOPER", new[] { "66019754" }, new[] { "จุลจักร จิรยะเทพถาวร" }),
    };

    private IEnumerator CreditsRoutine()
    {
        // ปิดเทอร์มินัล แล้วเลื่อนเครดิตขึ้นช้า ๆ บนพื้นดำ (กด Space ค้างเพื่อเร่ง)
        yield return new WaitForSecondsRealtime(0.4f);
        _terminal.gameObject.SetActive(false);

        var root = NewRect("Credits", transform, Vector2.zero, Vector2.zero);
        Stretch(root);
        var bg = NewImage("Bg", root, Vector2.zero, Vector2.zero, new Color(0.02f, 0.015f, 0.012f, 1f));
        Stretch(bg.rectTransform);
        var vig = NewImage("Vignette", root, Vector2.zero, Vector2.zero, Color.white);
        Stretch(vig.rectTransform);
        vig.sprite = DetectiveUIShapes.Vignette;
        vig.raycastTarget = false;

        var scroll = NewRect("Scroll", root, new Vector2(0f, -620f), new Vector2(1200f, 10f));
        float y = 0f;

        Text Add(string text, Font font, int size, Color color, float gapAfter)
        {
            var t = MakeText("C", scroll, text, font, size, color, TextAnchor.UpperCenter, new Vector2(0f, y), new Vector2(1200f, size * 1.6f), new Vector2(0.5f, 1f));
            y -= size * 1.35f + gapAfter;
            return t;
        }

        void Rule(float width, float gapAfter)
        {
            var r = NewImage("Rule", scroll, new Vector2(0f, y), new Vector2(width, 2f), new Color(Brass.r, Brass.g, Brass.b, 0.6f));
            r.raycastTarget = false;
            y -= gapAfter;
        }

        Add("SCOTLAND YARD  ·  CASE FILE 1888/JR", _mono, 22, new Color(Brass.r, Brass.g, Brass.b, 0.9f), 18f);
        Add("CPE477", _thaiBold, 96, Cream, 6f);
        Add("คดีปริศนาแห่งไวท์ชาเปล", _thaiBold, 34, new Color(0.82f, 0.74f, 0.6f, 1f), 40f);
        Rule(360f, 120f);

        foreach (var role in Roles)
        {
            Add(role.role, _mono, 26, Brass, 26f);
            for (int i = 0; i < role.names.Length; i++)
            {
                Add(role.names[i], _thaiBold, 44, Cream, 2f);
                Add(role.ids[i], _mono, 22, new Color(0.62f, 0.56f, 0.46f, 1f), 30f);
            }
            y -= 90f;
        }

        Rule(360f, 90f);
        Add("แรงบันดาลใจจาก", _thai, 24, new Color(0.62f, 0.56f, 0.46f, 1f), 6f);
        Add("Detective Conan: The Phantom of Baker Street (2002)", _mono, 24, Cream, 120f);
        Add("ขอบคุณที่ร่วมสืบสวน", _thaiBold, 40, Cream, 30f);
        var last = Add("> M is still watching_", _mono, 24, TermDim, 0f);

        // เลื่อนจนบรรทัดสุดท้ายมาค้างกลางจอ
        float travel = -y + 620f;   // ระยะเลื่อนรวม
        float speed = 70f;
        float moved = 0f;
        while (moved < travel)
        {
            float dt = Time.unscaledDeltaTime;
            bool fast = HoldingSkip();
            moved += dt * speed * (fast ? 5f : 1f);
            scroll.anchoredPosition = new Vector2(0f, -620f + Mathf.Min(moved, travel));
            yield return null;
        }

        float hold = 0f;
        while (hold < 2.5f)
        {
            hold += Time.unscaledDeltaTime;
            last.color = new Color(TermDim.r, TermDim.g, TermDim.b, 0.4f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 1.6f)));
            yield return null;
        }
        Destroy(root.gameObject);
    }

    private static bool HoldingSkip()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed);
#else
        return Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return);
#endif
    }

    // ==========================================
    // ช่วงที่ 4: เมนู
    // ==========================================
    private bool _menuChoiceMade;
    private int _menuIndex;

    private IEnumerator MenuRoutine()
    {
        PlayerPrefs.SetInt(Round2PrefKey, 1);
        PlayerPrefs.Save();
        yield return new WaitForSecondsRealtime(0.4f);
        _terminal.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var menu = NewRect("Menu", transform, Vector2.zero, Vector2.zero);
        Stretch(menu);
        var group = menu.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        var bg = NewImage("MenuBg", menu, Vector2.zero, Vector2.zero, new Color(0.035f, 0.026f, 0.02f, 1f));
        Stretch(bg.rectTransform);
        Sprite cork = DetectiveUITheme.CorkboardBg;
        if (cork != null)
        {
            var corkImg = NewImage("Cork", menu, Vector2.zero, Vector2.zero, new Color(0.22f, 0.17f, 0.13f, 1f));
            Stretch(corkImg.rectTransform);
            corkImg.sprite = cork;
            corkImg.raycastTarget = false;
        }

        // แฟ้มคดีที่ถูกประทับ TARGET ESCAPED (ฝั่งขวา) เป็นภาพตกค้างจากฉากจบ
        var file = NewRect("File", menu, new Vector2(470f, -20f), new Vector2(980f, 548f));
        file.localEulerAngles = new Vector3(0f, 0f, 5f);
        NewRaw("Shadow", file, new Vector2(14f, -20f), file.sizeDelta, _folderTex, new Color(0f, 0f, 0f, 0.6f));
        NewRaw("Art", file, Vector2.zero, file.sizeDelta, _folderTex, new Color(0.55f, 0.48f, 0.42f, 1f));
        var err = NewRect("Err", file, new Vector2(170f, 10f), new Vector2(360f, 120f));
        err.localEulerAngles = new Vector3(0f, 0f, -10f);
        AddFrame(err, new Vector2(350f, 110f), 5f, new Color(0.75f, 0.1f, 0.1f, 0.85f));
        AddFrame(err, new Vector2(330f, 92f), 1.5f, new Color(0.75f, 0.1f, 0.1f, 0.7f));
        MakeText("ErrText", err, "TARGET ESCAPED", _thaiBold, 36, new Color(0.75f, 0.1f, 0.1f, 0.9f), TextAnchor.MiddleCenter, Vector2.zero, new Vector2(340f, 60f), null);

        var vig = NewImage("Vignette", menu, Vector2.zero, Vector2.zero, Color.white);
        Stretch(vig.rectTransform);
        vig.sprite = DetectiveUIShapes.Vignette;
        vig.raycastTarget = false;

        // หัวเรื่องชิดซ้าย
        const float left = -820f;
        MakeText("Kicker", menu, "SCOTLAND YARD  ·  CASE FILE 1888/JR", _mono, 22, new Color(Brass.r, Brass.g, Brass.b, 0.9f), TextAnchor.MiddleLeft,
            new Vector2(left, 300f), new Vector2(800f, 30f), new Vector2(0f, 0.5f));
        MakeText("Title", menu, "CPE477", _thaiBold, 112, Cream, TextAnchor.MiddleLeft,
            new Vector2(left - 6f, 214f), new Vector2(900f, 140f), new Vector2(0f, 0.5f));
        MakeText("Sub", menu, "คดีปริศนาแห่งไวท์ชาเปล", _thaiBold, 34, new Color(0.82f, 0.74f, 0.6f, 1f), TextAnchor.MiddleLeft,
            new Vector2(left, 128f), new Vector2(800f, 46f), new Vector2(0f, 0.5f));
        var rule = NewImage("Rule", menu, new Vector2(left + 170f, 92f), new Vector2(340f, 3f), new Color(0.72f, 0.1f, 0.1f, 1f));
        rule.raycastTarget = false;

        var rows = new List<UIParallelogramGraphic>();
        var labels = new List<Text>();
        string[] items = { "I.    Play Round 2?", "II.   Leave the Case" };
        for (int i = 0; i < items.Length; i++)
        {
            int idx = i;
            var row = NewRect("Row_" + i, menu, new Vector2(left + 250f, -10f - i * 84f), new Vector2(500f, 66f));
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            var btn = row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => ActivateMenu(idx));
            var frame = NewRect("Frame", row, Vector2.zero, new Vector2(500f, 66f)).gameObject.AddComponent<UIParallelogramGraphic>();
            frame.skewAmount = 14f;
            frame.outlineWidth = 2f;
            frame.outlineColor = new Color(Brass.r, Brass.g, Brass.b, 0f);
            frame.fillColor = new Color(0f, 0f, 0f, 0f);
            frame.raycastTarget = false;
            rows.Add(frame);
            labels.Add(MakeText("Label", row, items[i], _thaiBold, 34, Cream, TextAnchor.MiddleLeft,
                new Vector2(-224f, 0f), new Vector2(470f, 66f), new Vector2(0f, 0.5f)));
            var hover = row.gameObject.AddComponent<StationHoverHandler>();
            hover.onEnter = () => _menuIndex = idx;
        }

        var watching = MakeText("Watching", menu, "> M is still watching_", _mono, 22, TermDim, TextAnchor.MiddleLeft,
            new Vector2(left, -440f), new Vector2(800f, 30f), new Vector2(0f, 0.5f));
        MakeText("Keys", menu, "[W/S] เลือก   [Enter] ยืนยัน", _thai, 20, new Color(0.7f, 0.62f, 0.5f, 0.8f), TextAnchor.MiddleLeft,
            new Vector2(left, -200f), new Vector2(600f, 28f), new Vector2(0f, 0.5f));

        float t = 0f;
        while (t < 1.2f)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / 1.2f);
            yield return null;
        }
        group.alpha = 1f;

        _menuIndex = 0;
        while (!_menuChoiceMade)
        {
            int move = MenuMove();
            if (move != 0) _menuIndex = (_menuIndex + move + items.Length) % items.Length;
            if (PressedConfirm()) ActivateMenu(_menuIndex);

            for (int i = 0; i < rows.Count; i++)
            {
                bool sel = i == _menuIndex;
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
                rows[i].outlineColor = new Color(Brass.r, Brass.g, Brass.b, sel ? pulse : 0f);
                rows[i].fillColor = new Color(i == 0 ? 0.45f : 0.2f, 0.06f, 0.05f, sel ? 0.55f : 0f);
                rows[i].SetVerticesDirty();
                labels[i].color = sel ? Color.white : new Color(Cream.r, Cream.g, Cream.b, 0.6f);
            }
            watching.color = new Color(TermDim.r, TermDim.g, TermDim.b, 0.4f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 1.5f)));
            yield return null;
        }
    }

    private void ActivateMenu(int index)
    {
        if (_menuChoiceMade) return;
        _menuChoiceMade = true;
        if (index == 0)
        {
            Time.timeScale = 1f;
            CiraCoreVisionScanner.IsScannerBlocked = false;
            IsPlaying = false;
            int idx = SceneManager.GetActiveScene().buildIndex;
            Destroy(gameObject);
            SceneManager.LoadScene(idx < 0 ? 0 : idx);
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    private void Finish()
    {
        PlayerPrefs.SetInt(Round2PrefKey, 1);
        PlayerPrefs.Save();
        IsPlaying = false;
        _onComplete?.Invoke();
    }

    // ==========================================
    // Input helpers
    // ==========================================
    private static bool PressedAdvance()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
    }

    private static bool PressedConfirm()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return);
#endif
    }

    private static int MenuMove()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return 0;
        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) return -1;
        if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) return 1;
        return 0;
#else
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) return -1;
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) return 1;
        return 0;
#endif
    }

    private static int PressedNumber()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return -1;
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 0;
        if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 1;
        if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 2;
        return -1;
#else
        if (Input.GetKeyDown(KeyCode.Alpha1)) return 0;
        if (Input.GetKeyDown(KeyCode.Alpha2)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha3)) return 2;
        return -1;
#endif
    }

    private static Vector3 CamPos()
    {
        return Camera.main != null ? Camera.main.transform.position : Vector3.zero;
    }

    private static void LockGameplay()
    {
        if (CinematicCutsceneManager.Instance != null) CinematicCutsceneManager.Instance.SetPlayerControlsLocked(true);
        CiraCoreVisionScanner.IsScannerBlocked = true;
        if (CiraCoreVisionScanner.Instance != null) CiraCoreVisionScanner.Instance.ForceCloseScanMode();
        DetectiveThoughtToast.Clear();
        if (StMaryChurchQuestManager.Instance != null) StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
        if (LondonAlleyQuestManager.Instance != null) LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
        if (RoyalLyceumTheaterQuestManager.Instance != null) RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(false);
        if (UndergroundStationQuestManager.Instance != null) UndergroundStationQuestManager.Instance.ShowObjectiveHUD(false);
        if (WaypointNavigationSystem.Instance != null) WaypointNavigationSystem.Instance.SetVisible(false);
    }

    private static IEnumerator Fade(Image img, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            Color c = img.color;
            img.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        Color e = img.color;
        img.color = new Color(e.r, e.g, e.b, to);
    }

    // ==========================================
    // UI build
    // ==========================================
    private void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        var bg = NewImage("Backdrop", transform, Vector2.zero, Vector2.zero, new Color(0.03f, 0.022f, 0.017f, 1f));
        Stretch(bg.rectTransform);

        _stage = NewRect("Stage", transform, Vector2.zero, Vector2.zero);
        Stretch(_stage);

        // วงแหวนตราสก็อตแลนด์ยาร์ดจาง ๆ เป็นลายน้ำด้านหลัง
        var water = NewImage("Watermark", _stage, new Vector2(0f, 30f), new Vector2(900f, 900f), new Color(Brass.r, Brass.g, Brass.b, 0.05f));
        water.sprite = DetectiveUIShapes.Ring(0.02f);
        water.raycastTarget = false;

        // แฟ้มหนังเปิด (ภาพของผู้ใช้) อยู่กลางจอ เว้นที่ด้านล่างให้ซับไตเติล
        _folderRect = NewRect("Folder", _stage, new Vector2(0f, 60f), new Vector2(1560f, 872f));
        NewRaw("Shadow", _folderRect, new Vector2(14f, -22f), _folderRect.sizeDelta, _folderTex, new Color(0f, 0f, 0f, 0.6f));
        var art = NewRaw("Art", _folderRect, Vector2.zero, _folderRect.sizeDelta, _folderTex, Color.white);
        if (_folderTex == null) { art.color = new Color(0.25f, 0.16f, 0.1f, 1f); art.rectTransform.sizeDelta = new Vector2(1200f, 760f); }

        // หน้าซ้าย: กระดาษรายงานปิดคดี วางอยู่ในแฟ้มพอดี
        var sheet = NewRect("Report", _folderRect, new Vector2(-292f, 4f), new Vector2(500f, 600f));
        sheet.localEulerAngles = new Vector3(0f, 0f, -1.2f);
        NewRaw("Shadow", sheet, new Vector2(8f, -10f), sheet.sizeDelta, _paperTex, new Color(0f, 0f, 0f, 0.45f));
        NewRaw("Paper", sheet, Vector2.zero, sheet.sizeDelta, _paperTex, new Color(1f, 0.97f, 0.9f, 1f));
        MakeText("Letterhead", sheet, "METROPOLITAN POLICE", _mono, 18, new Color(0.55f, 0.1f, 0.08f, 1f), TextAnchor.MiddleCenter,
            new Vector2(0f, 236f), new Vector2(440f, 24f), null);
        MakeText("FileNo", sheet, "FINAL REPORT  ·  FILE 1888/JR", _mono, 15, new Color(0.35f, 0.25f, 0.18f, 1f), TextAnchor.MiddleCenter,
            new Vector2(0f, 212f), new Vector2(440f, 22f), null);
        var line = NewImage("Rule", sheet, new Vector2(0f, 194f), new Vector2(400f, 2f), new Color(Ink.r, Ink.g, Ink.b, 0.5f));
        line.raycastTarget = false;
        var title = MakeText("Title", sheet, "คดีฆาตกรรมต่อเนื่อง\nแห่งไวท์ชาเปล", _thaiBold, 38, Ink, TextAnchor.MiddleCenter,
            new Vector2(0f, 128f), new Vector2(440f, 110f), null);
        title.lineSpacing = 0.92f;

        string[,] rows =
        {
            { "ผู้ต้องหา", "แจ็ค เดอะ ริปเปอร์" },
            { "สถานะ", "จับกุมแล้ว" },
            { "สถานที่", "สถานีเมโทรโพลิแทน ตู้ 4" },
            { "ผู้สืบสวน", "สารวัตรนิวแมน" },
            { "หลักฐานชี้ขาด", "แหวนสองวง โบสถ์เซนต์แมรี่" },
        };
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            float y = 42f - i * 46f;
            MakeText("K" + i, sheet, rows[i, 0], _thaiBold, 20, new Color(0.4f, 0.28f, 0.2f, 1f), TextAnchor.MiddleLeft,
                new Vector2(-200f, y), new Vector2(150f, 30f), new Vector2(0f, 0.5f));
            MakeText("V" + i, sheet, rows[i, 1], _thai, 21, Ink, TextAnchor.MiddleLeft,
                new Vector2(-48f, y), new Vector2(250f, 30f), new Vector2(0f, 0.5f));
            var dots = NewImage("D" + i, sheet, new Vector2(0f, y - 18f), new Vector2(400f, 1f), new Color(Ink.r, Ink.g, Ink.b, 0.18f));
            dots.raycastTarget = false;
        }

        // หน้าขวา: ตราประทับยางสีแดงบนปึกเอกสาร (อยู่ในขอบแฟ้มทั้งหมด)
        _stampRect = NewRect("Stamp", _folderRect, new Vector2(290f, 40f), new Vector2(460f, 170f));
        _stampRect.localEulerAngles = new Vector3(0f, 0f, -9f);
        _stampRect.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        _stampOuter = AddFrame(_stampRect, new Vector2(450f, 160f), 6f, StampInk);
        _stampInner = AddFrame(_stampRect, new Vector2(424f, 134f), 2f, StampInk);
        _stampSub = MakeText("Sub", _stampRect, "SCOTLAND YARD  ·  9 NOV 1888", _mono, 17, StampInk, TextAnchor.MiddleCenter,
            new Vector2(0f, 40f), new Vector2(420f, 24f), null);
        _stampMain = MakeText("Main", _stampRect, "CASE CLOSED", _thaiBold, 70, StampInk, TextAnchor.MiddleCenter,
            new Vector2(0f, -14f), new Vector2(430f, 90f), null);
        _stampRect.gameObject.SetActive(false);

        // แถบซับไตเติลล่างจอ (เหมือนแถบบทพูดในคัตซีนของเกม)
        _subBar = NewRect("SubtitleBar", _stage, new Vector2(0f, -455f), new Vector2(1920f, 170f)).gameObject;
        var barBg = NewImage("Bg", _subBar.transform, Vector2.zero, new Vector2(1920f, 170f), new Color(0f, 0f, 0f, 0.82f));
        barBg.raycastTarget = false;
        var gold = NewImage("Line", _subBar.transform, new Vector2(0f, 84f), new Vector2(1400f, 2f), new Color(Brass.r, Brass.g, Brass.b, 0.7f));
        gold.raycastTarget = false;
        _subSpeaker = MakeText("Speaker", _subBar.transform, "", _thaiBold, 26, Brass, TextAnchor.MiddleLeft,
            new Vector2(-700f, 52f), new Vector2(800f, 34f), new Vector2(0f, 0.5f));
        _subText = MakeText("Text", _subBar.transform, "", _thai, 30, Cream, TextAnchor.UpperLeft,
            new Vector2(-700f, 30f), new Vector2(1400f, 90f), new Vector2(0f, 1f));
        _subText.horizontalOverflow = HorizontalWrapMode.Wrap;
        MakeText("Skip", _subBar.transform, "[Space] ข้าม", _thai, 18, new Color(0.7f, 0.62f, 0.5f, 0.7f), TextAnchor.MiddleRight,
            new Vector2(700f, 52f), new Vector2(300f, 26f), new Vector2(1f, 0.5f));
        _subBar.SetActive(false);

        // แถว Continue (สไตล์เมนูของเกม)
        var cont = NewRect("Continue", _stage, new Vector2(640f, -455f), new Vector2(360f, 64f));
        var contHit = cont.gameObject.AddComponent<Image>();
        contHit.color = new Color(0.30f, 0.05f, 0.05f, 0.9f);
        var contBtn = cont.gameObject.AddComponent<Button>();
        var cc = contBtn.colors;
        cc.normalColor = Color.white;
        cc.highlightedColor = new Color(1.5f, 1.2f, 1.2f, 1f);
        cc.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        cc.colorMultiplier = 1f;
        contBtn.colors = cc;
        contBtn.onClick.AddListener(() => _continuePressed = true);
        AddFrame(cont, new Vector2(350f, 54f), 2f, new Color(Brass.r, Brass.g, Brass.b, 0.9f));
        MakeText("Label", cont, "Continue  ›", _thaiBold, 32, Cream, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(360f, 64f), null);
        _continueRow = cont.gameObject;
        _continueRow.SetActive(false);

        // เป้าสแกน CiRA CORE (หน้าตาเดียวกับระบบสแกนในเกม)
        _reticle = NewRect("Reticle", transform, Vector2.zero, new Vector2(220f, 220f));
        var ring = NewImage("Ring", _reticle, Vector2.zero, new Vector2(140f, 140f), ScanGreen);
        ring.sprite = DetectiveUIShapes.Ring(0.05f);
        ring.raycastTarget = false;
        _reticleFill = NewImage("Fill", _reticle, Vector2.zero, new Vector2(118f, 118f), new Color(ScanGreen.r, ScanGreen.g, ScanGreen.b, 0.45f));
        _reticleFill.sprite = DetectiveUIShapes.Ring(0.08f);
        _reticleFill.type = Image.Type.Filled;
        _reticleFill.fillMethod = Image.FillMethod.Radial360;
        _reticleFill.raycastTarget = false;
        foreach (var d in new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) })
        {
            var h = NewImage("BracketH", _reticle, new Vector2(d.x * 96f, d.y * 108f), new Vector2(24f, 3f), ScanGreen);
            h.raycastTarget = false;
            var v = NewImage("BracketV", _reticle, new Vector2(d.x * 107f, d.y * 97f), new Vector2(3f, 24f), ScanGreen);
            v.raycastTarget = false;
        }
        foreach (var d in new[] { new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(1f, 0f), new Vector2(-1f, 0f) })
        {
            var tick = NewImage("Tick", _reticle, d * 56f, d.x != 0f ? new Vector2(14f, 2f) : new Vector2(2f, 14f), new Color(Brass.r, Brass.g, Brass.b, 0.9f));
            tick.raycastTarget = false;
        }
        var labelBg = NewImage("LabelBg", _reticle, new Vector2(0f, 140f), new Vector2(460f, 36f), new Color(0.04f, 0.035f, 0.03f, 0.85f));
        labelBg.raycastTarget = false;
        _reticleLabel = MakeText("Label", _reticle, "", _mono, 20, ScanGreen, TextAnchor.MiddleCenter, new Vector2(0f, 140f), new Vector2(460f, 36f), null);
        _reticle.gameObject.SetActive(false);

        for (int i = 0; i < 14; i++)
        {
            var bar = NewImage("Glitch_" + i, transform, Vector2.zero, new Vector2(400f, 20f), Color.white);
            bar.raycastTarget = false;
            bar.gameObject.SetActive(false);
            _glitchBars.Add(bar);
        }

        // ม่านดำ (ใต้เทอร์มินัล)
        _black = NewImage("Black", transform, Vector2.zero, Vector2.zero, Color.black);
        Stretch(_black.rectTransform);
        _black.raycastTarget = false;

        // เทอร์มินัล
        _terminal = NewRect("Terminal", transform, Vector2.zero, Vector2.zero);
        Stretch(_terminal);
        var tbg = NewImage("TerminalBg", _terminal, Vector2.zero, Vector2.zero, new Color(0.005f, 0.018f, 0.01f, 1f));
        Stretch(tbg.rectTransform);
        tbg.raycastTarget = true;
        var scan = NewRect("Scanlines", _terminal, Vector2.zero, Vector2.zero);
        Stretch(scan);
        var scanRaw = scan.gameObject.AddComponent<RawImage>();
        scanRaw.texture = BuildScanlineTexture();
        scanRaw.uvRect = new Rect(0f, 0f, 1f, 270f);
        scanRaw.color = new Color(1f, 1f, 1f, 0.55f);
        scanRaw.raycastTarget = false;

        MakeText("Header", _terminal, "CIRA_CORE.VISION   //   REMOTE SESSION   //   OPERATOR: M", _mono, 20, TermDim, TextAnchor.MiddleLeft,
            new Vector2(-860f, 480f), new Vector2(1700f, 30f), new Vector2(0f, 0.5f));
        var hr = NewImage("HeaderRule", _terminal, new Vector2(0f, 458f), new Vector2(1720f, 1.5f), new Color(TermDim.r, TermDim.g, TermDim.b, 0.6f));
        hr.raycastTarget = false;

        _termLog = NewRect("Log", _terminal, new Vector2(0f, 420f), new Vector2(1720f, 10f));
        _termChoices = NewRect("Choices", _terminal, new Vector2(0f, -300f), new Vector2(1720f, 10f));
        var choicesRule = NewImage("ChoicesRule", _termChoices, new Vector2(0f, 46f), new Vector2(1500f, 1.5f), new Color(TermDim.r, TermDim.g, TermDim.b, 0.5f));
        choicesRule.raycastTarget = false;
        MakeText("ChoicesHint", _termChoices, "เลือกคำตอบ  [1] [2] [3]  หรือคลิก", _thai, 20, TermDim, TextAnchor.MiddleRight,
            new Vector2(750f, 66f), new Vector2(600f, 26f), new Vector2(1f, 0.5f));
        _termChoices.gameObject.SetActive(false);
        _terminal.gameObject.SetActive(false);

        _flash = NewImage("Flash", transform, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0f));
        Stretch(_flash.rectTransform);
        _flash.raycastTarget = false;
    }

    private static Texture2D BuildScanlineTexture()
    {
        var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(new[]
        {
            new Color(0f, 0f, 0f, 0.35f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f)
        });
        tex.Apply();
        return tex;
    }

    // ==========================================
    // UI helpers
    // ==========================================
    private static UIParallelogramGraphic AddFrame(RectTransform parent, Vector2 size, float width, Color color)
    {
        var frame = NewRect("Frame", parent, Vector2.zero, size).gameObject.AddComponent<UIParallelogramGraphic>();
        frame.skewAmount = 0f;
        frame.outlineWidth = width;
        frame.outlineColor = color;
        frame.fillColor = new Color(0f, 0f, 0f, 0f);
        frame.raycastTarget = false;
        return frame;
    }

    private static RawImage NewRaw(string name, Transform parent, Vector2 pos, Vector2 size, Texture tex, Color color)
    {
        var r = NewRect(name, parent, pos, size);
        var raw = r.gameObject.AddComponent<RawImage>();
        raw.texture = tex;
        raw.color = color;
        raw.raycastTarget = false;
        return raw;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = new Vector2(0.5f, 0.5f);
        r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        return r;
    }

    private static Image NewImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var r = NewRect(name, parent, pos, size);
        var img = r.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Text MakeText(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor align,
        Vector2 pos, Vector2 box, Vector2? pivot)
    {
        var r = NewRect(name, parent, pos, box);
        if (pivot.HasValue) r.pivot = pivot.Value;
        r.anchoredPosition = pos;
        var t = r.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }
}
