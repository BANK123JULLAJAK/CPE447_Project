using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// หน้าสรุปข้อสันนิษฐานแห่งคดี (Case Deduction Board) ใช้ร่วมกันทุกบท สไตล์แฟ้มคดีสกอตแลนด์ยาร์ด 1888
/// - ซ้าย: การ์ดหลักฐานติดบนบอร์ดไม้ก๊อก (การ์ดที่เกี่ยวกับข้อที่กำลังตอบจะยกขึ้นและมีกรอบแดง)
/// - ขวา: กระดาษบันทึกคดี ถามเป็นลำดับขั้น (หลายข้อสันนิษฐานต่อเนื่องกัน) ตอบผิดมีเหตุผลอธิบายว่าทำไมผิด
/// - ตอบครบทุกข้อ จะแสดง "ข้อสรุปแห่งคดี" รวมสายการอนุมานทั้งหมด ข้อสงสัยที่ยังค้าง และคะแนนความแม่นยำ
/// เลือกด้วยเมาส์ หรือคีย์ [1-4] / [W/S + Enter]
/// เรียกใช้: CaseDeductionBoardUI.Open(config, result => { ... });
/// </summary>
public class CaseDeductionBoardUI : MonoBehaviour
{
    // ==========================================
    // ข้อมูลที่ส่งเข้ามา
    // ==========================================
    [Serializable]
    public class EvidenceCard
    {
        public string id;
        public string title;
        public string note;

        public EvidenceCard(string id, string title, string note)
        {
            this.id = id;
            this.title = title;
            this.note = note;
        }
    }

    [Serializable]
    public class Option
    {
        public string text;
        public bool correct;
        public string feedback;

        public Option(string text, bool correct, string feedback)
        {
            this.text = text;
            this.correct = correct;
            this.feedback = feedback;
        }
    }

    [Serializable]
    public class Step
    {
        public string question;
        public string[] relatedEvidenceIds = new string[0];
        public List<Option> options = new List<Option>();
        /// <summary>บรรทัดที่เพิ่มเข้าสายการอนุมานเมื่อตอบถูก (แสดงในหน้าข้อสรุปแห่งคดี)</summary>
        public string conclusion;
    }

    [Serializable]
    public class Config
    {
        public string letterhead = "METROPOLITAN POLICE  ·  CASE FILE 1888/JR";
        public string finalTitle = "ข้อสรุปแห่งคดี";
        public string openQuestionLabel = "ข้อสงสัยที่ยังค้างอยู่";
        public string openQuestion;
        public string finishButtonLabel = "บันทึกข้อสรุปลงแฟ้มคดี  ›";
        public List<EvidenceCard> evidence = new List<EvidenceCard>();
        public List<Step> steps = new List<Step>();
    }

    public class Result
    {
        public int mistakes;
        /// <summary>ความแม่นยำ 1-3 (3 = ไม่ผิดเลย)</summary>
        public int accuracy;
    }

    // ==========================================
    // สถานะ
    // ==========================================
    public static CaseDeductionBoardUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance._root != null && Instance._root.activeSelf;

    /// <summary>ปิดหน้าต่างทันทีโดยไม่เรียก callback (ใช้ตอนกระโดดฉากเพื่อทดสอบ)</summary>
    public static void ForceClose()
    {
        if (Instance == null || Instance._root == null) return;
        Instance.StopAllCoroutines();
        Instance._root.SetActive(false);
    }

    private enum Phase { Opening, Answering, StepSolved, Final, Closing }

    [Tooltip("ฟอนต์หัวข้อภาษาไทย (ว่างไว้ = Prompt-Bold)")]
    public Font headingFont;
    [Tooltip("ฟอนต์เนื้อหาภาษาไทย (ว่างไว้ = Sarabun-Medium)")]
    public Font bodyFont;

    private static readonly Vector2 PaperSize = new Vector2(1280f, 960f);
    private static readonly Vector2 PaperPos = new Vector2(270f, 0f);
    private const float InnerLeft = -560f;
    private const float InnerWidth = 1120f;
    private const int MaxOptions = 4;
    private const int MaxPips = 6;
    private static readonly string[] Letters = { "A", "B", "C", "D" };

    private static readonly Color GreenInk = new Color(0.10f, 0.40f, 0.18f, 1f);
    private static readonly Color RedInk = new Color(0.62f, 0.08f, 0.07f, 1f);

    private Font _typewriterFont;
    private GameObject _root;
    private CanvasGroup _rootGroup;
    private RectTransform _paper;
    private RectTransform _cardsRoot;

    private Text _letterheadText;
    private Text _stepLabel;
    private Text _questionText;
    private Text _feedbackText;
    private Text _hintText;
    private Text _finalBodyText;
    private Text _openQuestionLabel;
    private Text _openQuestionText;
    private Text _accuracyLabel;
    private Image[] _pips = new Image[MaxPips];
    private Image[] _accuracyPips = new Image[3];
    private DeductionOptionRow[] _rows = new DeductionOptionRow[MaxOptions];
    private CanvasGroup[] _rowGroups = new CanvasGroup[MaxOptions];
    private CanvasGroup _buttonGroup;
    private Text _buttonLabel;
    private RectTransform _stampRect;
    private CanvasGroup _stampGroup;

    private readonly List<CardView> _cards = new List<CardView>();
    private readonly Dictionary<Text, Coroutine> _typers = new Dictionary<Text, Coroutine>();

    private Config _config;
    private Action<Result> _onSolved;
    private Phase _phase;
    private int _stepIndex;
    private int _optionCount;
    private int _selected;
    private int _mistakes;
    private bool _canContinue;

    private class CardView
    {
        public RectTransform rect;
        public CanvasGroup group;
        public GameObject frame;
        public Image pin;
        public string id;
        public float scale = 1.12f;
        public float alpha = 0f;
        public float targetScale = 1f;
        public float targetAlpha = 1f;
        public float appearAt;
    }

    // ==========================================
    // Entry point
    // ==========================================
    public static void Open(Config config, Action<Result> onSolved)
    {
        if (Instance == null)
        {
            var go = new GameObject("CaseDeductionBoardUI");
            go.AddComponent<CaseDeductionBoardUI>();
        }
        Instance.BeginOpen(config, onSolved);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (headingFont == null) headingFont = GameAssets.Load<Font>("Assets/Font/Prompt-Bold.ttf");
        if (bodyFont == null) bodyFont = GameAssets.Load<Font>("Assets/Font/Sarabun-Medium.ttf");
        if (headingFont == null) headingFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (bodyFont == null) bodyFont = headingFont;
        _typewriterFont = Font.CreateDynamicFontFromOSFont(new[] { "Courier New", "Consolas" }, 24);
        if (_typewriterFont == null) _typewriterFont = headingFont;

        BuildFrame();
        _root.SetActive(false);
    }

    private void BeginOpen(Config config, Action<Result> onSolved)
    {
        _config = config;
        _onSolved = onSolved;
        _stepIndex = 0;
        _mistakes = 0;
        _canContinue = false;
        _phase = Phase.Opening;

        StopAllCoroutines();
        _typers.Clear();

        _letterheadText.text = config.letterhead;
        BuildCards();
        HideFinalElements();
        _buttonGroup.alpha = 0f;
        _buttonGroup.interactable = false;
        _buttonGroup.blocksRaycasts = false;
        _feedbackText.text = "";
        _questionText.text = "";
        for (int i = 0; i < MaxOptions; i++) _rows[i].gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        _root.SetActive(true);
        _rootGroup.alpha = 0f;
        _paper.anchoredPosition = PaperPos + new Vector2(140f, 0f);
        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        Vector2 from = PaperPos + new Vector2(140f, 0f);
        yield return Animate(0.35f, t =>
        {
            _rootGroup.alpha = t;
            _paper.anchoredPosition = Vector2.Lerp(from, PaperPos, t);
        });
        LoadStep();
    }

    // ==========================================
    // Input
    // ==========================================
    private enum Press { Up, Down, Submit, N1, N2, N3, N4 }

    private static bool Pressed(Press p)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        switch (p)
        {
            case Press.Up: return kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            case Press.Down: return kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            case Press.Submit: return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            case Press.N1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case Press.N2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case Press.N3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
            case Press.N4: return kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame;
        }
        return false;
#else
        switch (p)
        {
            case Press.Up: return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            case Press.Down: return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            case Press.Submit: return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
            case Press.N1: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
            case Press.N2: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
            case Press.N3: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
            case Press.N4: return Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4);
        }
        return false;
#endif
    }

    private void Update()
    {
        if (!IsOpen) return;

        float dt = Time.unscaledDeltaTime;
        foreach (var c in _cards)
        {
            if (Time.unscaledTime < c.appearAt) continue;
            float k = 1f - Mathf.Exp(-9f * dt);
            c.scale = Mathf.Lerp(c.scale, c.targetScale, k);
            c.alpha = Mathf.Lerp(c.alpha, c.targetAlpha, k);
            c.rect.localScale = Vector3.one * c.scale;
            c.group.alpha = c.alpha;
        }

        switch (_phase)
        {
            case Phase.Answering:
                if (Pressed(Press.Up)) SetSelected((_selected + _optionCount - 1) % Mathf.Max(1, _optionCount));
                if (Pressed(Press.Down)) SetSelected((_selected + 1) % Mathf.Max(1, _optionCount));
                if (Pressed(Press.N1)) Answer(0);
                else if (Pressed(Press.N2)) Answer(1);
                else if (Pressed(Press.N3)) Answer(2);
                else if (Pressed(Press.N4)) Answer(3);
                else if (Pressed(Press.Submit)) Answer(_selected);
                break;
            case Phase.StepSolved:
            case Phase.Final:
                if (_canContinue && Pressed(Press.Submit)) Continue();
                break;
        }
    }

    // ==========================================
    // Flow
    // ==========================================
    private void LoadStep()
    {
        var step = _config.steps[_stepIndex];
        int n = _config.steps.Count;

        _phase = Phase.Answering;
        _canContinue = false;
        _buttonGroup.alpha = 0f;
        _buttonGroup.interactable = false;
        _buttonGroup.blocksRaycasts = false;
        _hintText.gameObject.SetActive(true);
        _feedbackText.text = "";

        _stepLabel.text = $"ข้อสันนิษฐานที่ {_stepIndex + 1} จาก {n}";
        UpdatePips(n, _stepIndex);
        ApplyEvidenceHighlight(step.relatedEvidenceIds);

        Type(_questionText, "", step.question, 90f);

        _optionCount = Mathf.Min(step.options.Count, MaxOptions);
        for (int i = 0; i < MaxOptions; i++)
        {
            if (i < _optionCount)
            {
                _rows[i].gameObject.SetActive(true);
                _rows[i].SetContent(Letters[i], step.options[i].text);
                _rows[i].SetState(DeductionOptionRow.State.Normal);
            }
            else
            {
                _rows[i].gameObject.SetActive(false);
            }
        }

        SetSelected(0);
        StartCoroutine(RevealRows());
    }

    private IEnumerator RevealRows()
    {
        for (int i = 0; i < _optionCount; i++) _rowGroups[i].alpha = 0f;
        for (int i = 0; i < _optionCount; i++)
        {
            int idx = i;
            StartCoroutine(Animate(0.25f, t => _rowGroups[idx].alpha = t));
            yield return new WaitForSecondsRealtime(0.07f);
        }
    }

    private void SetSelected(int index)
    {
        if (_phase != Phase.Answering) return;
        _selected = Mathf.Clamp(index, 0, Mathf.Max(0, _optionCount - 1));
        for (int i = 0; i < _optionCount; i++) _rows[i].SetSelected(i == _selected);
    }

    private void OnOptionHover(int index)
    {
        if (_phase == Phase.Answering) SetSelected(index);
    }

    private void OnOptionClicked(int index)
    {
        if (_phase == Phase.Answering) Answer(index);
    }

    private void Answer(int index)
    {
        if (_phase != Phase.Answering || index < 0 || index >= _optionCount) return;
        var step = _config.steps[_stepIndex];
        var option = step.options[index];

        if (option.correct)
        {
            _phase = Phase.StepSolved;
            for (int i = 0; i < _optionCount; i++)
            {
                _rows[i].SetSelected(false);
                if (i == index) _rows[i].SetState(DeductionOptionRow.State.Correct);
                else if (!_rows[i].IsWrong) _rows[i].SetState(DeductionOptionRow.State.Dimmed);
            }

            _feedbackText.color = GreenInk;
            Type(_feedbackText, "<b>ถูกต้อง</b>  ", option.feedback, 75f);
            UpdatePips(_config.steps.Count, _stepIndex + 1);
            _hintText.gameObject.SetActive(false);
            bool last = _stepIndex >= _config.steps.Count - 1;
            StartCoroutine(ShowContinueButton(last ? "ดูข้อสรุปแห่งคดี  ›" : "ข้อสันนิษฐานถัดไป  ›", 0.9f));
        }
        else
        {
            _mistakes++;
            _rows[index].SetState(DeductionOptionRow.State.Wrong);
            _rows[index].SetSelected(false);

            // ย้ายไฮไลต์ไปรายการแรกที่ยังเลือกได้
            for (int i = 0; i < _optionCount; i++)
            {
                if (!_rows[i].IsWrong) { SetSelected(i); break; }
            }

            _feedbackText.color = RedInk;
            Type(_feedbackText, "<b>ยังไม่ใช่</b>  ", option.feedback, 80f);
            StartCoroutine(ShakePaper());
        }
    }

    private IEnumerator ShowContinueButton(string label, float delay)
    {
        _canContinue = false;
        _buttonLabel.text = label;
        yield return new WaitForSecondsRealtime(delay);
        yield return Animate(0.25f, t => _buttonGroup.alpha = t);
        _buttonGroup.interactable = true;
        _buttonGroup.blocksRaycasts = true;
        _canContinue = true;
    }

    private void Continue()
    {
        if (!_canContinue) return;
        _canContinue = false;
        _buttonGroup.interactable = false;
        _buttonGroup.blocksRaycasts = false;

        if (_phase == Phase.StepSolved)
        {
            if (_stepIndex < _config.steps.Count - 1)
            {
                _stepIndex++;
                StartCoroutine(SwitchStepRoutine());
            }
            else
            {
                StartCoroutine(ShowFinalRoutine());
            }
        }
        else if (_phase == Phase.Final)
        {
            StartCoroutine(CloseRoutine());
        }
    }

    private IEnumerator SwitchStepRoutine()
    {
        yield return Animate(0.18f, t =>
        {
            _buttonGroup.alpha = 1f - t;
            _feedbackText.color = new Color(_feedbackText.color.r, _feedbackText.color.g, _feedbackText.color.b, 1f - t);
            for (int i = 0; i < _optionCount; i++) _rowGroups[i].alpha = 1f - t;
        });
        var c = _feedbackText.color;
        _feedbackText.color = new Color(c.r, c.g, c.b, 1f);
        LoadStep();
    }

    private IEnumerator ShowFinalRoutine()
    {
        yield return Animate(0.2f, t =>
        {
            _buttonGroup.alpha = 1f - t;
            for (int i = 0; i < _optionCount; i++) _rowGroups[i].alpha = 1f - t;
            _feedbackText.color = new Color(_feedbackText.color.r, _feedbackText.color.g, _feedbackText.color.b, 1f - t);
        });

        _phase = Phase.Final;
        for (int i = 0; i < MaxOptions; i++) _rows[i].gameObject.SetActive(false);
        _feedbackText.text = "";
        _feedbackText.color = new Color(_feedbackText.color.r, _feedbackText.color.g, _feedbackText.color.b, 1f);
        _hintText.gameObject.SetActive(false);

        int n = _config.steps.Count;
        _stepLabel.text = "สรุปการสืบสวน";
        UpdatePips(n, n);
        ApplyEvidenceHighlight(null);
        Type(_questionText, "", _config.finalTitle, 60f);

        var sb = new StringBuilder();
        for (int i = 0; i < n; i++)
        {
            sb.Append("<b>").Append(i + 1).Append(".</b>  ").Append(_config.steps[i].conclusion);
            if (i < n - 1) sb.Append("\n\n");
        }
        _finalBodyText.text = sb.ToString();
        _openQuestionLabel.text = _config.openQuestionLabel;
        _openQuestionText.text = _config.openQuestion;

        int accuracy = CalculateAccuracy();
        for (int i = 0; i < _accuracyPips.Length; i++)
        {
            _accuracyPips[i].color = i < accuracy ? DetectiveUITheme.StampRed : new Color(DetectiveUITheme.InkDark.r, DetectiveUITheme.InkDark.g, DetectiveUITheme.InkDark.b, 0.25f);
        }

        // ค่อย ๆ เผยข้อสรุปทีละส่วน
        var groups = new List<CanvasGroup>();
        foreach (var finalText in new Text[] { _finalBodyText, _openQuestionLabel, _openQuestionText, _accuracyLabel })
        {
            finalText.gameObject.SetActive(true);
            var g = finalText.GetComponent<CanvasGroup>();
            if (g == null) g = finalText.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            groups.Add(g);
        }
        foreach (var img in _accuracyPips) { img.gameObject.SetActive(true); }

        yield return new WaitForSecondsRealtime(0.25f);
        yield return Animate(0.5f, t => groups[0].alpha = t);
        yield return Animate(0.35f, t => { groups[1].alpha = t; groups[2].alpha = t; });
        yield return Animate(0.3f, t => groups[3].alpha = t);

        // ตราประทับ SOLVED
        _stampGroup.gameObject.SetActive(true);
        _stampGroup.alpha = 0f;
        yield return Animate(0.16f, t =>
        {
            _stampGroup.alpha = t;
            _stampRect.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, t);
        });
        yield return ShakePaper();

        yield return ShowContinueButton(_config.finishButtonLabel, 0.5f);
    }

    private IEnumerator CloseRoutine()
    {
        _phase = Phase.Closing;
        yield return Animate(0.35f, t =>
        {
            _rootGroup.alpha = 1f - t;
            _paper.anchoredPosition = Vector2.Lerp(PaperPos, PaperPos + new Vector2(0f, 40f), t);
        });
        _root.SetActive(false);

        var cb = _onSolved;
        _onSolved = null;
        cb?.Invoke(new Result { mistakes = _mistakes, accuracy = CalculateAccuracy() });
    }

    private int CalculateAccuracy()
    {
        if (_mistakes == 0) return 3;
        if (_mistakes <= 2) return 2;
        return 1;
    }

    private IEnumerator ShakePaper()
    {
        float t = 0f;
        const float duration = 0.22f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float s = (1f - t / duration) * 7f;
            _paper.anchoredPosition = PaperPos + new Vector2(UnityEngine.Random.Range(-s, s), UnityEngine.Random.Range(-s, s));
            yield return null;
        }
        _paper.anchoredPosition = PaperPos;
    }

    // ==========================================
    // Visual helpers
    // ==========================================
    private void UpdatePips(int total, int solved)
    {
        for (int i = 0; i < MaxPips; i++)
        {
            bool used = i < total;
            _pips[i].gameObject.SetActive(used);
            if (!used) continue;

            _pips[i].rectTransform.anchoredPosition = new Vector2(540f - (total - 1 - i) * 38f, 372f);
            Color ink = DetectiveUITheme.InkDark;
            if (i < solved) _pips[i].color = DetectiveUITheme.StampRed;
            else if (i == solved) _pips[i].color = new Color(ink.r, ink.g, ink.b, 0.85f);
            else _pips[i].color = new Color(ink.r, ink.g, ink.b, 0.22f);
        }
    }

    private void ApplyEvidenceHighlight(string[] relatedIds)
    {
        bool any = relatedIds != null && relatedIds.Length > 0;
        foreach (var c in _cards)
        {
            bool related = any && Array.IndexOf(relatedIds, c.id) >= 0;
            c.targetScale = related ? 1.04f : 1f;
            c.targetAlpha = (!any || related) ? 1f : 0.45f;
            c.frame.SetActive(related);
            c.pin.color = related ? new Color(0.86f, 0.12f, 0.12f, 1f) : new Color(0.52f, 0.12f, 0.10f, 1f);
        }
    }

    private void HideFinalElements()
    {
        _finalBodyText.gameObject.SetActive(false);
        _openQuestionLabel.gameObject.SetActive(false);
        _openQuestionText.gameObject.SetActive(false);
        _accuracyLabel.gameObject.SetActive(false);
        foreach (var p in _accuracyPips) p.gameObject.SetActive(false);
        _stampGroup.gameObject.SetActive(false);
        _hintText.gameObject.SetActive(true);
    }

    private void Type(Text target, string prefix, string body, float cps)
    {
        if (_typers.TryGetValue(target, out var running) && running != null) StopCoroutine(running);
        _typers[target] = StartCoroutine(TypeRoutine(target, prefix, body ?? "", cps));
    }

    private IEnumerator TypeRoutine(Text target, string prefix, string body, float cps)
    {
        target.text = prefix;
        float interval = 1f / Mathf.Max(1f, cps);
        float acc = 0f;
        int i = 0;
        while (i < body.Length)
        {
            acc += Time.unscaledDeltaTime;
            while (acc >= interval && i < body.Length)
            {
                acc -= interval;
                i++;
                while (i < body.Length && IsThaiMark(body[i])) i++;
            }
            target.text = prefix + body.Substring(0, i);
            yield return null;
        }
        target.text = prefix + body;
    }

    private static bool IsThaiMark(char c)
    {
        return c == 'ั' || (c >= 'ิ' && c <= 'ฺ') || (c >= '็' && c <= '๎');
    }

    private static IEnumerator Animate(float duration, Action<float> step)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            step(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        step(1f);
    }

    // ==========================================
    // Evidence cards (ซ้าย)
    // ==========================================
    private void BuildCards()
    {
        foreach (var c in _cards) if (c.rect != null) Destroy(c.rect.gameObject);
        _cards.Clear();

        var list = _config.evidence;
        int count = list.Count;
        float step = count > 5 ? 150f : 178f;
        float startY = count > 5 ? 395f : 380f;
        float[] angles = { -1.2f, 0.8f, -0.6f, 1.1f, -0.9f, 0.7f };
        Sprite note = DetectiveUITheme.NoteCard;

        for (int i = 0; i < count; i++)
        {
            var data = list[i];
            var rect = NewRect("Card_" + data.id, _cardsRoot, new Vector2(-660f, startY - i * step), new Vector2(500f, 138f), null);
            rect.localEulerAngles = new Vector3(0f, 0f, angles[i % angles.Length]);
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var shadow = NewImage("Shadow", rect, new Vector2(7f, -8f), new Vector2(500f, 138f), new Color(0f, 0f, 0f, 0.5f), null);
            shadow.raycastTarget = false;

            var sheetRect = NewRect("Sheet", rect, Vector2.zero, new Vector2(500f, 138f), null);
            var sheet = sheetRect.gameObject.AddComponent<RawImage>();
            sheet.raycastTarget = false;
            if (note != null)
            {
                sheet.texture = note.texture;
                sheet.uvRect = new Rect(0.12f, 0.30f + (i % 5) * 0.05f, 0.76f, 0.2f);
                sheet.color = new Color(1f, 0.97f, 0.92f, 1f);
            }
            else sheet.color = DetectiveUITheme.AgedPaper;

            // กรอบแดงเมื่อเป็นหลักฐานที่เกี่ยวข้องกับข้อที่ตอบ
            var frameRect = NewRect("Frame", rect, Vector2.zero, new Vector2(500f, 138f), null);
            var frame = frameRect.gameObject.AddComponent<UIParallelogramGraphic>();
            frame.skewAmount = 0f;
            frame.outlineWidth = 4f;
            frame.outlineColor = DetectiveUITheme.StampRed;
            frame.fillColor = new Color(0f, 0f, 0f, 0f);
            frame.raycastTarget = false;
            frameRect.gameObject.SetActive(false);

            NewText("Title", rect, data.title, headingFont, 25, DetectiveUITheme.InkDark, TextAnchor.UpperLeft,
                new Vector2(-222f, 54f), new Vector2(370f, 34f), new Vector2(0f, 1f), false);
            var noteText = NewText("Note", rect, data.note, bodyFont, 19, DetectiveUITheme.InkMuted, TextAnchor.UpperLeft,
                new Vector2(-222f, 18f), new Vector2(450f, 80f), new Vector2(0f, 1f), true);
            noteText.lineSpacing = 1.05f;
            NewText("No", rect, "No." + (i + 1).ToString("00"), _typewriterFont, 17, DetectiveUITheme.InkMuted, TextAnchor.UpperRight,
                new Vector2(222f, 52f), new Vector2(90f, 24f), new Vector2(1f, 1f), false);

            // หมุดแดง
            var pinShadow = NewImage("PinShadow", rect, new Vector2(2f, 62f), new Vector2(26f, 26f), new Color(0f, 0f, 0f, 0.4f), null);
            pinShadow.sprite = DetectiveUIShapes.Circle;
            pinShadow.raycastTarget = false;
            var pin = NewImage("Pin", rect, new Vector2(0f, 65f), new Vector2(24f, 24f), new Color(0.52f, 0.12f, 0.10f, 1f), null);
            pin.sprite = DetectiveUIShapes.Circle;
            pin.raycastTarget = false;
            var pinHi = NewImage("PinHighlight", rect, new Vector2(-4f, 69f), new Vector2(8f, 8f), new Color(1f, 1f, 1f, 0.55f), null);
            pinHi.sprite = DetectiveUIShapes.Circle;
            pinHi.raycastTarget = false;

            rect.localScale = Vector3.one * 1.12f;
            _cards.Add(new CardView
            {
                rect = rect,
                group = group,
                frame = frameRect.gameObject,
                pin = pin,
                id = data.id,
                appearAt = Time.unscaledTime + 0.15f + i * 0.09f
            });
        }

        _cardsHeader.text = "แฟ้มหลักฐาน";
        _cardsFooter.text = $"หลักฐานที่รวบรวมได้ทั้งหมด {count} ชิ้น";
    }

    private Text _cardsHeader;
    private Text _cardsFooter;

    // ==========================================
    // Frame (สร้างครั้งเดียว)
    // ==========================================
    private void BuildFrame()
    {
        _root = new GameObject("CaseDeductionBoard_Canvas", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 955;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        _root.AddComponent<GraphicRaycaster>();
        _rootGroup = _root.AddComponent<CanvasGroup>();

        // พื้นหลัง: บอร์ดไม้ก๊อกมืด ๆ ทับด้วยขอบมืดรอบจอ
        var bg = StretchImage("Backdrop", _root.transform, new Color(0.03f, 0.025f, 0.02f, 1f));
        bg.raycastTarget = true; // กันคลิกทะลุไปโดนเกมด้านหลัง
        Sprite cork = DetectiveUITheme.CorkboardBg;
        if (cork != null)
        {
            var corkImg = StretchImage("Corkboard", _root.transform, new Color(0.34f, 0.29f, 0.24f, 1f));
            corkImg.sprite = cork;
            corkImg.raycastTarget = false;
        }
        var vignette = StretchImage("Vignette", _root.transform, Color.white);
        vignette.sprite = DetectiveUIShapes.Vignette;
        vignette.raycastTarget = false;

        // คอลัมน์การ์ดหลักฐาน
        _cardsRoot = NewRect("Cards", _root.transform, Vector2.zero, new Vector2(1920f, 1080f), null);
        _cardsHeader = NewText("CardsHeader", _cardsRoot, "", headingFont, 30, DetectiveUITheme.AgedPaper, TextAnchor.MiddleCenter,
            new Vector2(-660f, 495f), new Vector2(500f, 40f), null, false);
        _cardsFooter = NewText("CardsFooter", _cardsRoot, "", bodyFont, 20, new Color(0.82f, 0.76f, 0.62f, 1f), TextAnchor.MiddleCenter,
            new Vector2(-660f, -470f), new Vector2(500f, 30f), null, false);

        // กระดาษบันทึกคดี
        _paper = NewRect("Paper", _root.transform, PaperPos, PaperSize, null);
        var shadow = NewImage("Shadow", _paper, new Vector2(20f, -24f), PaperSize, new Color(0f, 0f, 0f, 0.6f), null);
        shadow.raycastTarget = false;
        var sheetRect = NewRect("Sheet", _paper, Vector2.zero, PaperSize, null);
        var sheet = sheetRect.gameObject.AddComponent<RawImage>();
        Sprite noteSprite = DetectiveUITheme.NoteCard;
        if (noteSprite != null)
        {
            sheet.texture = noteSprite.texture;
            sheet.uvRect = new Rect(0.105f, 0.232f, 0.79f, 0.54f);
            sheet.color = new Color(1f, 0.97f, 0.92f, 1f);
        }
        else sheet.color = DetectiveUITheme.AgedPaper;
        sheet.raycastTarget = true;

        Color ink = DetectiveUITheme.InkDark;
        _letterheadText = NewText("Letterhead", _paper, "", _typewriterFont, 20, DetectiveUITheme.InkMuted, TextAnchor.MiddleLeft,
            new Vector2(InnerLeft, 418f), new Vector2(800f, 30f), new Vector2(0f, 0.5f), false);
        _stepLabel = NewText("StepLabel", _paper, "", headingFont, 28, DetectiveUITheme.StampRed, TextAnchor.MiddleLeft,
            new Vector2(InnerLeft, 372f), new Vector2(700f, 40f), new Vector2(0f, 0.5f), false);

        for (int i = 0; i < MaxPips; i++)
        {
            var pip = NewImage("Pip_" + i, _paper, new Vector2(540f, 372f), new Vector2(18f, 18f), ink, null);
            pip.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            pip.raycastTarget = false;
            _pips[i] = pip;
        }

        var rule = NewImage("Rule", _paper, new Vector2(0f, 340f), new Vector2(InnerWidth, 3f), new Color(ink.r, ink.g, ink.b, 0.55f), null);
        rule.raycastTarget = false;

        _questionText = NewText("Question", _paper, "", headingFont, 34, ink, TextAnchor.UpperLeft,
            new Vector2(InnerLeft, 322f), new Vector2(InnerWidth, 170f), new Vector2(0f, 1f), true);
        _questionText.lineSpacing = 1.1f;

        for (int i = 0; i < MaxOptions; i++) BuildRow(i);

        _feedbackText = NewText("Feedback", _paper, "", bodyFont, 26, GreenInk, TextAnchor.UpperLeft,
            new Vector2(InnerLeft, -304f), new Vector2(InnerWidth, 130f), new Vector2(0f, 1f), true);
        _feedbackText.lineSpacing = 1.1f;
        _feedbackText.supportRichText = true;

        _hintText = NewText("Hint", _paper, "[1-4] เลือกคำตอบ     [W/S] เลื่อน     [Enter] ยืนยัน", bodyFont, 20, DetectiveUITheme.InkMuted,
            TextAnchor.MiddleLeft, new Vector2(InnerLeft, -448f), new Vector2(900f, 30f), new Vector2(0f, 0.5f), false);

        // ส่วนของหน้าข้อสรุป (ซ่อนไว้จนกว่าตอบครบ)
        _finalBodyText = NewText("FinalBody", _paper, "", bodyFont, 25, ink, TextAnchor.UpperLeft,
            new Vector2(InnerLeft, 215f), new Vector2(InnerWidth, 330f), new Vector2(0f, 1f), true);
        _finalBodyText.lineSpacing = 1.1f;
        _openQuestionLabel = NewText("OpenQuestionLabel", _paper, "", headingFont, 26, DetectiveUITheme.StampRed, TextAnchor.MiddleLeft,
            new Vector2(InnerLeft, -125f), new Vector2(InnerWidth, 36f), new Vector2(0f, 0.5f), false);
        _openQuestionText = NewText("OpenQuestion", _paper, "", bodyFont, 27, ink, TextAnchor.UpperLeft,
            new Vector2(InnerLeft, -152f), new Vector2(InnerWidth, 100f), new Vector2(0f, 1f), true);
        _openQuestionText.fontStyle = FontStyle.Italic;
        _accuracyLabel = NewText("AccuracyLabel", _paper, "ความแม่นยำในการสืบสวน", headingFont, 24, ink, TextAnchor.MiddleLeft,
            new Vector2(InnerLeft, -300f), new Vector2(380f, 36f), new Vector2(0f, 0.5f), false);
        for (int i = 0; i < _accuracyPips.Length; i++)
        {
            var pip = NewImage("AccuracyPip_" + i, _paper, new Vector2(InnerLeft + 420f + i * 36f, -300f), new Vector2(20f, 20f), ink, null);
            pip.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            pip.raycastTarget = false;
            _accuracyPips[i] = pip;
        }

        // ตราประทับ SOLVED
        _stampRect = NewRect("Stamp", _paper, new Vector2(380f, 262f), new Vector2(300f, 84f), null);
        _stampRect.localEulerAngles = new Vector3(0f, 0f, -8f);
        _stampGroup = _stampRect.gameObject.AddComponent<CanvasGroup>();
        _stampGroup.blocksRaycasts = false;
        Color stampInk = new Color(DetectiveUITheme.StampRed.r, DetectiveUITheme.StampRed.g, DetectiveUITheme.StampRed.b, 0.8f);
        var stampFrame = _stampRect.gameObject.AddComponent<UIParallelogramGraphic>();
        stampFrame.skewAmount = 0f;
        stampFrame.outlineWidth = 6f;
        stampFrame.outlineColor = stampInk;
        stampFrame.fillColor = new Color(0f, 0f, 0f, 0f);
        stampFrame.raycastTarget = false;
        var stampText = NewText("Label", _stampRect, "SOLVED", headingFont, 50, stampInk, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(300f, 84f), null, false);
        stampText.fontStyle = FontStyle.Bold;

        BuildContinueButton();
    }

    private void BuildRow(int i)
    {
        var rect = NewRect("Option_" + i, _paper, new Vector2(0f, 100f - i * 112f), new Vector2(InnerWidth, 100f), null);
        var hit = rect.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        var btn = rect.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = hit;
        int idx = i;
        btn.onClick.AddListener(() => OnOptionClicked(idx));

        // กรอบเลือก
        var frameRect = NewRect("Frame", rect, Vector2.zero, new Vector2(InnerWidth, 100f), null);
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        var frame = frameRect.gameObject.AddComponent<UIParallelogramGraphic>();
        frame.skewAmount = 0f;
        frame.outlineWidth = 3.5f;
        frame.outlineColor = DetectiveUITheme.StampRed;
        frame.fillColor = new Color(DetectiveUITheme.StampRed.r, DetectiveUITheme.StampRed.g, DetectiveUITheme.StampRed.b, 0.08f);
        frame.raycastTarget = false;
        var frameGroup = frameRect.gameObject.AddComponent<CanvasGroup>();
        frameGroup.alpha = 0f;
        frameGroup.blocksRaycasts = false;

        // ป้ายตัวอักษร A-D
        var badge = NewImage("Badge", rect, new Vector2(-510f, 0f), new Vector2(54f, 54f), DetectiveUITheme.InkDark, null);
        badge.raycastTarget = false;
        var letter = NewText("Letter", badge.rectTransform, "", headingFont, 30, DetectiveUITheme.AgedPaper, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(54f, 54f), null, false);

        var label = NewText("Label", rect, "", bodyFont, 26, DetectiveUITheme.InkDark, TextAnchor.MiddleLeft,
            new Vector2(-460f, 0f), new Vector2(990f, 96f), new Vector2(0f, 0.5f), true);
        label.lineSpacing = 1.05f;

        // เส้นขีดฆ่าเมื่อตอบผิด
        var strike = NewImage("Strike", rect, new Vector2(-460f + 495f, 0f), new Vector2(980f, 3f), new Color(0.35f, 0.08f, 0.06f, 0.85f), null);
        strike.raycastTarget = false;
        strike.gameObject.SetActive(false);

        var row = rect.gameObject.AddComponent<DeductionOptionRow>();
        row.Init(idx, btn, frameGroup, frame, badge, letter, label, strike.gameObject);
        row.onHover = OnOptionHover;

        _rows[i] = row;
        _rowGroups[i] = group;
        rect.gameObject.SetActive(false);
    }

    private void BuildContinueButton()
    {
        var rect = NewRect("ContinueButton", _paper, new Vector2(330f, -440f), new Vector2(460f, 66f), null);
        var img = rect.gameObject.AddComponent<Image>();
        img.color = Color.white;
        _buttonGroup = rect.gameObject.AddComponent<CanvasGroup>();
        var btn = rect.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = DetectiveUITheme.InkDark;
        colors.highlightedColor = DetectiveUITheme.StampRed;
        colors.selectedColor = DetectiveUITheme.InkDark;
        colors.pressedColor = new Color(0.45f, 0.06f, 0.05f, 1f);
        colors.colorMultiplier = 1f;
        btn.colors = colors;
        btn.onClick.AddListener(Continue);

        _buttonLabel = NewText("Label", rect, "", headingFont, 27, DetectiveUITheme.AgedPaper, TextAnchor.MiddleCenter,
            Vector2.zero, new Vector2(460f, 66f), null, false);
        NewText("Hint", rect, "[Enter]", _typewriterFont, 16, DetectiveUITheme.InkMuted, TextAnchor.MiddleCenter,
            new Vector2(0f, -50f), new Vector2(460f, 24f), null, false);
    }

    // ==========================================
    // UI factory helpers
    // ==========================================
    private static RectTransform NewRect(string name, Transform parent, Vector2 pos, Vector2 size, Vector2? pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = new Vector2(0.5f, 0.5f);
        r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        return r;
    }

    private static Image NewImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color, Vector2? pivot)
    {
        var r = NewRect(name, parent, pos, size, pivot);
        var img = r.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Image StretchImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Text NewText(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor align,
        Vector2 pos, Vector2 box, Vector2? pivot, bool wrap)
    {
        var r = NewRect(name, parent, pos, box, pivot);
        var t = r.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }
}

/// <summary>
/// ตัวเลือกคำตอบ A-D บนกระดาษบันทึกคดี: ชี้/เลือกแล้วมีกรอบหมึกแดง ตอบผิดถูกขีดฆ่า ตอบถูกกรอบเขียว
/// </summary>
public class DeductionOptionRow : MonoBehaviour, IPointerEnterHandler
{
    public enum State { Normal, Wrong, Correct, Dimmed }

    public Action<int> onHover;
    public bool IsWrong => _state == State.Wrong;

    private int _index;
    private Button _button;
    private CanvasGroup _frameGroup;
    private UIParallelogramGraphic _frame;
    private Image _badge;
    private Text _letter;
    private Text _label;
    private GameObject _strike;
    private Vector2 _labelBasePos;
    private State _state = State.Normal;
    private bool _selected;
    private float _frameTarget;

    private static readonly Color InkRed = new Color(0.62f, 0.08f, 0.07f, 1f);
    private static readonly Color Green = new Color(0.10f, 0.40f, 0.18f, 1f);
    private static readonly Color Gray = new Color(0.48f, 0.42f, 0.36f, 0.85f);

    public void Init(int index, Button button, CanvasGroup frameGroup, UIParallelogramGraphic frame, Image badge, Text letter, Text label, GameObject strike)
    {
        _index = index;
        _button = button;
        _frameGroup = frameGroup;
        _frame = frame;
        _badge = badge;
        _letter = letter;
        _label = label;
        _strike = strike;
        _labelBasePos = label.rectTransform.anchoredPosition;
    }

    public void SetContent(string letter, string text)
    {
        _letter.text = letter;
        _label.text = text;
        _label.rectTransform.anchoredPosition = _labelBasePos;
        _frameGroup.alpha = 0f;
        _selected = false;
        _state = State.Normal;
        Refresh();
    }

    public void SetState(State state)
    {
        _state = state;
        Refresh();
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_state == State.Normal) onHover?.Invoke(_index);
    }

    private void Update()
    {
        if (_frameGroup == null) return;
        _frameGroup.alpha = Mathf.MoveTowards(_frameGroup.alpha, _frameTarget, Time.unscaledDeltaTime * 9f);
        _label.rectTransform.anchoredPosition = _labelBasePos + new Vector2(10f * _frameGroup.alpha, 0f);
    }

    private void Refresh()
    {
        Color label, badge, frameColor = InkRed;
        bool strike = false;
        _frameTarget = 0f;

        switch (_state)
        {
            case State.Wrong:
                label = Gray;
                badge = Gray;
                strike = true;
                break;
            case State.Correct:
                label = Green;
                badge = Green;
                frameColor = Green;
                _frameTarget = 1f;
                break;
            case State.Dimmed:
                label = new Color(Gray.r, Gray.g, Gray.b, 0.6f);
                badge = new Color(Gray.r, Gray.g, Gray.b, 0.6f);
                break;
            default:
                label = _selected ? InkRed : DetectiveUITheme.InkDark;
                badge = _selected ? InkRed : DetectiveUITheme.InkDark;
                _frameTarget = _selected ? 1f : 0f;
                break;
        }

        _label.color = label;
        _badge.color = badge;
        _strike.SetActive(strike);
        _frame.outlineColor = frameColor;
        _frame.fillColor = new Color(frameColor.r, frameColor.g, frameColor.b, 0.09f);
        _frame.SetVerticesDirty();
        if (_button != null) _button.interactable = _state == State.Normal;
    }
}
