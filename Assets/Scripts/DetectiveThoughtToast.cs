using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ความคิดของนักสืบ (Detective Thought Toast)
/// แสดงประโยคสั้น ๆ ของสารวัตรนิวแมนตรงกลางล่างจอระหว่างเล่น โดยไม่หยุดเกม ไม่ขึ้นแถบดำ
/// ใช้ตอนสแกนพบหลักฐานแต่ละชิ้น เพื่อให้ผู้เล่นรู้ว่าหลักฐานชิ้นนั้น "บอกอะไร" แทนการนับจำนวนเฉย ๆ
/// เรียกใช้: DetectiveThoughtToast.Show("ข้อความ");
/// </summary>
public class DetectiveThoughtToast : MonoBehaviour
{
    private static DetectiveThoughtToast _instance;

    [Tooltip("ฟอนต์ภาษาไทย (ว่างไว้ = Sarabun-Medium ในโปรเจกต์)")]
    public Font thaiFont;

    private CanvasGroup _group;
    private Text _speakerText;
    private Text _bodyText;
    private RectTransform _panelRect;
    private readonly Queue<string> _queue = new Queue<string>();
    private Coroutine _routine;

    private const string SpeakerName = "สารวัตรนิวแมน";

    /// <summary>แสดงความคิดของนิวแมน (ต่อคิวถ้ามีข้อความกำลังแสดงอยู่)</summary>
    public static void Show(string thought)
    {
        if (string.IsNullOrEmpty(thought)) return;
        if (_instance == null)
        {
            var go = new GameObject("DetectiveThoughtToast");
            _instance = go.AddComponent<DetectiveThoughtToast>();
        }
        _instance.Enqueue(thought);
    }

    /// <summary>ล้างความคิดที่ค้างอยู่ทั้งหมดทันที (ใช้ตอนเปิดหน้าสรุปข้อสันนิษฐาน ไม่ให้ข้อความซ้อนกัน)</summary>
    public static void Clear()
    {
        if (_instance != null) _instance.ClearAll();
    }

    private void ClearAll()
    {
        _queue.Clear();
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        if (_group != null) _group.alpha = 0f;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (thaiFont == null)
        {
            thaiFont = GameAssets.Load<Font>("Assets/Font/Sarabun-Medium.ttf")
                    ?? GameAssets.Load<Font>("Assets/Font/Prompt-Medium.ttf");
            if (thaiFont == null) thaiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        BuildUI();
    }

    private void Enqueue(string thought)
    {
        _queue.Enqueue(thought);
        if (_routine == null) _routine = StartCoroutine(PlayQueue());
    }

    private IEnumerator PlayQueue()
    {
        while (_queue.Count > 0)
        {
            string line = _queue.Dequeue();
            _bodyText.text = line;

            // ค้างตามความยาวประโยค (ประมาณ 15 ตัวอักษรต่อวินาที)
            float hold = Mathf.Clamp(line.Length / 15f, 2.8f, 7f);

            yield return Fade(0f, 1f, 0.25f, new Vector2(0f, -12f), Vector2.zero);
            yield return new WaitForSecondsRealtime(hold);
            yield return Fade(1f, 0f, 0.35f, Vector2.zero, new Vector2(0f, 6f));
        }
        _routine = null;
    }

    private IEnumerator Fade(float from, float to, float duration, Vector2 fromOffset, Vector2 toOffset)
    {
        float t = 0f;
        Vector2 basePos = new Vector2(0f, 150f);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            _group.alpha = Mathf.Lerp(from, to, k);
            _panelRect.anchoredPosition = basePos + Vector2.Lerp(fromOffset, toOffset, k);
            yield return null;
        }
        _group.alpha = to;
        _panelRect.anchoredPosition = basePos + toOffset;
    }

    private void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // ใต้แถบดำคัตซีน (999) และเมนูหยุดเกม

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // แผงพื้นหลังโปร่งแสงเข้มแบบซีเปีย ให้อ่านออกในทุกฉาก
        var panelGo = new GameObject("ThoughtPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panelGo.transform.SetParent(transform, false);
        _panelRect = panelGo.GetComponent<RectTransform>();
        _panelRect.anchorMin = new Vector2(0.5f, 0f);
        _panelRect.anchorMax = new Vector2(0.5f, 0f);
        _panelRect.pivot = new Vector2(0.5f, 0f);
        _panelRect.sizeDelta = new Vector2(1100f, 110f);
        _panelRect.anchoredPosition = new Vector2(0f, 150f);
        var bg = panelGo.GetComponent<Image>();
        bg.color = new Color(0.06f, 0.045f, 0.03f, 0.78f);
        bg.raycastTarget = false;
        _group = panelGo.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        // แถบสีทองเหลืองด้านซ้าย
        var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        accentGo.transform.SetParent(panelGo.transform, false);
        var accentRect = accentGo.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.sizeDelta = new Vector2(5f, 0f);
        var accentImg = accentGo.GetComponent<Image>();
        accentImg.color = DetectiveUITheme.BrassGold;
        accentImg.raycastTarget = false;

        _speakerText = CreateText(panelGo.transform, "Speaker", SpeakerName, 20, DetectiveUITheme.BrassGold, FontStyle.Bold,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -40f), new Vector2(-28f, -10f));

        _bodyText = CreateText(panelGo.transform, "Body", "", 24, new Color(0.96f, 0.93f, 0.86f, 1f), FontStyle.Italic,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 10f), new Vector2(-28f, -42f));
        _bodyText.alignment = TextAnchor.UpperLeft;
        _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private Text CreateText(Transform parent, string name, string text, int size, Color color, FontStyle style,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        var t = go.GetComponent<Text>();
        t.font = thaiFont;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleLeft;
        t.raycastTarget = false;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(1.2f, -1.2f);
        return t;
    }
}
