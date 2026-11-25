using System;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ระบบจัดการธีม UI สไตล์ Apple Vision Pro OS (visionOS UI Kit)
/// - จัดการโหลดและแคช Sprites, Textures, Shaders, และ Materials จากโฟลเดอร์ JetXR UI Kit For Vision Pro OS
/// - มอบ Glassmorphism Component (Frosted Glass, 9-Slice Window, Soft Drop Shadow, Translucent Pill Badges)
/// - ปรับสไตล์คำและโทนสีให้เป็นแบบ Mind Palace / High-Tech Forensic Detective OS
/// </summary>
public static class VisionOSUITheme
{
    // ==========================================
    // 1. โทนสีหลัก (Genshin Impact Detective Palette: Midnight Slate & Antique Gold)
    // ==========================================
    public static readonly Color GlassWindowBg = new Color(0.10f, 0.12f, 0.16f, 0.96f); // Deep Midnight Slate
    public static readonly Color GlassCardBg = new Color(0.14f, 0.17f, 0.22f, 0.90f);   // Dark Slate Card
    public static readonly Color GlassCardSelected = new Color(0.18f, 0.23f, 0.32f, 0.98f); // Selected Card Warm Navy
    public static readonly Color GlassHeaderBg = new Color(0.08f, 0.10f, 0.14f, 0.98f); // Header Obsidian

    public static readonly Color StrokeSubtle = new Color(0.72f, 0.60f, 0.40f, 0.40f);       // เส้นขอบทองหม่น 1px
    public static readonly Color StrokeHighlight = new Color(0.95f, 0.80f, 0.40f, 0.95f);    // ขอบสีทองอร่าม Genshin Gold
    public static readonly Color StrokeGold = new Color(0.95f, 0.80f, 0.40f, 0.95f);         // ขอบสีทองหลักฐานสำคัญ

    public static readonly Color AccentCyan = new Color(0.95f, 0.82f, 0.42f, 1f);        // สีทองอำพัน Genshin Gold
    public static readonly Color AccentGold = new Color(1.0f, 0.84f, 0.36f, 1f);         // สีทองสืบสวน
    public static readonly Color AccentGreen = new Color(0.42f, 0.86f, 0.58f, 1f);       // สีเขียวยืนยันผล
    public static readonly Color AccentRed = new Color(0.90f, 0.32f, 0.32f, 1f);          // สีแดงเบาะแสลวง

    public static readonly Color TextPrimary = new Color(0.96f, 0.95f, 0.91f, 1f);       // ข้อความสีขาวงาช้างอบอุ่น
    public static readonly Color TextSecondary = new Color(0.82f, 0.77f, 0.70f, 1f);    // ข้อความรองสีเบจทองหม่น
    public static readonly Color TextMuted = new Color(0.56f, 0.53f, 0.48f, 1f);        // ข้อความกำกับ

    public static readonly Color PillButtonNormal = new Color(0.20f, 0.24f, 0.32f, 0.90f);
    public static readonly Color PillButtonHover = new Color(0.28f, 0.34f, 0.44f, 0.98f);
    public static readonly Color PillButtonPressed = new Color(0.12f, 0.15f, 0.20f, 0.98f);
    public static readonly Color PillButtonCyan = new Color(0.88f, 0.72f, 0.30f, 1f); // Genshin Gold Action Button

    // ==========================================
    // 2. แคชทรัพยากร Sprites & Fonts
    // ==========================================
    private static Sprite _sprWindowGlass;
    private static Sprite _sprWindowStroke;
    private static Sprite _sprWindowShadow;
    private static Sprite _sprSidebar;
    private static Sprite _sprPillButton;
    private static Sprite _sprPillHighlight;
    private static Sprite _sprTabbarBg;
    private static Sprite _sprTabbarStroke;
    private static Sprite _sprTabbarShadow;

    private static Sprite _iconSearch;
    private static Sprite _iconDoc;
    private static Sprite _iconWatch;
    private static Sprite _iconList;
    private static Sprite _iconTable;
    private static Sprite _iconDots;
    private static Sprite _iconClose;
    private static Sprite _iconBubble;

    private static Font _thaiFont;
    private static Font _thaiFontBold;

    // Fallback programmatic sprites
    private static Sprite _fallbackCircle;
    private static Sprite _fallbackRoundedRect;

    public static Sprite WindowGlass => _sprWindowGlass ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Runtime/Sprites/Windows/WindowGlass.png");
    public static Sprite WindowStroke => _sprWindowStroke ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Runtime/Sprites/Windows/Stroke.png");
    public static Sprite WindowShadow => _sprWindowShadow ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Runtime/Sprites/Windows/WindowShadow.png");
    public static Sprite Sidebar => _sprSidebar ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Runtime/Sprites/Windows/Sidebar.png");

    public static Sprite PillButton => IPhoneCard;
    public static Sprite PillHighlight => IPhoneCard;
    public static Sprite IPhoneCard => FallbackRoundedRect;
    public static Sprite TabbarBg => _sprTabbarBg ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Runtime/Sprites/Tabbar/TabbarBackground.png");
    public static Sprite TabbarStroke => _sprTabbarStroke ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Runtime/Sprites/Tabbar/TabbarStroke.png");
    public static Sprite TabbarShadow => _sprTabbarShadow ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Runtime/Sprites/Tabbar/TabbarShadow.png");

    public static Sprite IconSearch => _iconSearch ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Icons/Magnifyingglass.png");
    public static Sprite IconDoc => _iconDoc ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Icons/Doc Plaintext.png");
    public static Sprite IconWatch => _iconWatch ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Icons/Watch.png");
    public static Sprite IconList => _iconList ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Icons/List.png");
    public static Sprite IconTable => _iconTable ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Icons/Table.png");
    public static Sprite IconDots => _iconDots ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Icons/Dots.png");
    public static Sprite IconClose => _iconClose ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Icons/Chevron Left.png");
    public static Sprite IconBubble => _iconBubble ??= LoadSprite("Assets/JetXR/UI Kit For Vision Pro OS/Icons/Captions Bubble.png");

    public static Font PrimaryFont => _thaiFont ??= LoadFont("Assets/Font/Prompt-Medium.ttf", "Assets/Font/Sarabun-Medium.ttf");
    public static Font BoldFont => _thaiFontBold ??= LoadFont("Assets/Font/Prompt-Bold.ttf", "Assets/Font/Sarabun-Bold.ttf");

    private static Sprite LoadSprite(string path)
    {
        var s = GameAssets.Load<Sprite>(path);
        if (s != null) return s;
        return FallbackRoundedRect;
    }

    private static Font LoadFont(string p1, string p2)
    {
        var f = GameAssets.Load<Font>(p1) ?? GameAssets.Load<Font>(p2);
        if (f != null) return f;
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    public static Sprite FallbackCircle
    {
        get
        {
            if (_fallbackCircle == null)
            {
                int r = 32;
                Texture2D tex = new Texture2D(r * 2, r * 2, TextureFormat.RGBA32, false);
                Color[] cols = new Color[r * 2 * r * 2];
                Vector2 center = new Vector2(r, r);
                for (int y = 0; y < r * 2; y++)
                {
                    for (int x = 0; x < r * 2; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), center);
                        float a = Mathf.Clamp01((r - d) / 1.5f);
                        cols[y * r * 2 + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels(cols);
                tex.Apply();
                _fallbackCircle = Sprite.Create(tex, new Rect(0, 0, r * 2, r * 2), new Vector2(0.5f, 0.5f));
            }
            return _fallbackCircle;
        }
    }

    public static Sprite FallbackRoundedRect
    {
        get
        {
            if (_fallbackRoundedRect == null)
            {
                int sz = 64;
                int rad = 12; // Sleek modern iPhone squircle
                Texture2D tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
                Color[] cols = new Color[sz * sz];
                for (int y = 0; y < sz; y++)
                {
                    for (int x = 0; x < sz; x++)
                    {
                        int cx = x < rad ? rad : (x >= sz - rad ? sz - 1 - rad : x);
                        int cy = y < rad ? rad : (y >= sz - rad ? sz - 1 - rad : y);
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                        float a = Mathf.Clamp01((rad - d) / 1.2f);
                        cols[y * sz + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels(cols);
                tex.Apply();
                _fallbackRoundedRect = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(rad, rad, rad, rad));
            }
            return _fallbackRoundedRect;
        }
    }

    // ==========================================
    // 3. Helper Factory Methods (VisionOS Builders)
    // ==========================================

    /// <summary>
    /// ตกแต่ง GameObject ให้กลายเป็นหน้าต่างกระจกฝ้า Vision Pro OS (มีเงาตกกระทบลึก และขอบสะท้อนแสงกระจก)
    /// </summary>
    public static void ApplyVisionWindowStyle(GameObject windowGo, bool withShadow = true, bool withStroke = true)
    {
        // 1. Soft Shadow ด้านหลัง
        if (withShadow && WindowShadow != null)
        {
            GameObject shadowGo = new GameObject("Vision_Shadow", typeof(RectTransform), typeof(Image));
            shadowGo.transform.SetParent(windowGo.transform, false);
            shadowGo.transform.SetAsFirstSibling();
            RectTransform sRect = shadowGo.GetComponent<RectTransform>();
            sRect.anchorMin = Vector2.zero;
            sRect.anchorMax = Vector2.one;
            sRect.offsetMin = new Vector2(-60f, -60f);
            sRect.offsetMax = new Vector2(60f, 60f);
            Image sImg = shadowGo.GetComponent<Image>();
            sImg.sprite = WindowShadow;
            sImg.type = Image.Type.Sliced;
            sImg.color = new Color(0f, 0f, 0f, 0.65f);
            sImg.raycastTarget = false;
        }

        // 2. แผ่นกระจกฝ้าหลัก (Frosted Glass Body)
        Image winImg = windowGo.GetComponent<Image>();
        if (winImg == null) winImg = windowGo.AddComponent<Image>();
        winImg.sprite = WindowGlass;
        winImg.type = Image.Type.Sliced;
        winImg.color = GlassWindowBg;

        // 3. เส้นขอบสะท้อนแสง (Specular Stroke)
        if (withStroke && WindowStroke != null)
        {
            GameObject strokeGo = new GameObject("Vision_Stroke", typeof(RectTransform), typeof(Image));
            strokeGo.transform.SetParent(windowGo.transform, false);
            RectTransform stRect = strokeGo.GetComponent<RectTransform>();
            stRect.anchorMin = Vector2.zero;
            stRect.anchorMax = Vector2.one;
            stRect.offsetMin = Vector2.zero;
            stRect.offsetMax = Vector2.zero;
            Image stImg = strokeGo.GetComponent<Image>();
            stImg.sprite = WindowStroke;
            stImg.type = Image.Type.Sliced;
            stImg.color = StrokeSubtle;
            stImg.raycastTarget = false;
        }
    }

    /// <summary>
    /// สร้างการ์ดกระจกฝ้า Vision Pro OS (สำหรับวางรายการ หรือเนื้อหาข้อความ)
    /// </summary>
    public static GameObject CreateVisionCard(Transform parent, string name, Vector2 size, Color? cardColor = null, bool hasStroke = true)
    {
        GameObject cardGo = new GameObject(name, typeof(RectTransform), typeof(Image));
        cardGo.transform.SetParent(parent, false);
        RectTransform rt = cardGo.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        Image img = cardGo.GetComponent<Image>();
        img.sprite = PillButton;
        img.type = Image.Type.Sliced;
        img.color = cardColor ?? GlassCardBg;

        if (hasStroke)
        {
            var outline = cardGo.AddComponent<Outline>();
            outline.effectColor = StrokeSubtle;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        return cardGo;
    }

    /// <summary>
    /// สร้างปุ่มรูปทรงแคปซูลมน (VisionOS Pill Button)
    /// </summary>
    public static Button CreateVisionPillButton(Transform parent, string name, string labelText, Vector2 size, Action onClick, Color? accentColor = null, Sprite icon = null)
    {
        GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(parent, false);
        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        Color baseCol = accentColor ?? PillButtonNormal;

        Image img = btnGo.GetComponent<Image>();
        img.sprite = PillButton;
        img.type = Image.Type.Sliced;
        img.color = baseCol;

        Button btn = btnGo.GetComponent<Button>();
        btn.targetGraphic = img;

        ColorBlock cb = btn.colors;
        cb.normalColor = baseCol;
        cb.highlightedColor = accentColor.HasValue ? Color.Lerp(accentColor.Value, Color.white, 0.25f) : PillButtonHover;
        cb.pressedColor = PillButtonPressed;
        cb.selectedColor = cb.highlightedColor;
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.12f;
        btn.colors = cb;

        // Icon if provided
        float textLeftOffset = 16f;
        if (icon != null)
        {
            GameObject iconGo = new GameObject("BtnIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(btnGo.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(14f, 0f);
            iconRt.sizeDelta = new Vector2(20f, 20f);
            Color iconCol = (baseCol.r > 0.7f && baseCol.g > 0.6f) ? new Color(0.12f, 0.09f, 0.04f, 1f) : TextPrimary;
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.sprite = icon;
            iconImg.color = iconCol;
            iconImg.raycastTarget = false;
            textLeftOffset = 42f;
        }

        // Text
        GameObject txtGo = new GameObject("BtnText", typeof(RectTransform), typeof(Text));
        txtGo.transform.SetParent(btnGo.transform, false);
        RectTransform txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(textLeftOffset, 0f);
        txtRt.offsetMax = new Vector2(-16f, 0f);

        Color textCol = (baseCol.r > 0.7f && baseCol.g > 0.6f) ? new Color(0.12f, 0.09f, 0.04f, 1f) : TextPrimary;
        Text txt = txtGo.GetComponent<Text>();
        txt.font = BoldFont;
        txt.fontSize = 15;
        txt.color = textCol;
        txt.alignment = icon != null ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
        txt.text = labelText;
        txt.raycastTarget = false;

        if (textCol != TextPrimary)
        {
            // Subtle glow for dark text on gold button
            var shadow = txtGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(1f, 0.95f, 0.8f, 0.5f);
            shadow.effectDistance = new Vector2(0.5f, -0.5f);
        }
        else
        {
            var shadow = txtGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        if (onClick != null) btn.onClick.AddListener(() => onClick.Invoke());

        return btn;
    }

    /// <summary>
    /// สร้างป้ายแคปซูลสถานะหรือหมวดหมู่ (VisionOS Pill Badge)
    /// </summary>
    public static GameObject CreateVisionBadge(Transform parent, string name, string text, Color badgeColor, Color textColor)
    {
        GameObject badgeGo = new GameObject(name, typeof(RectTransform), typeof(Image));
        badgeGo.transform.SetParent(parent, false);

        Image img = badgeGo.GetComponent<Image>();
        img.sprite = PillButton;
        img.type = Image.Type.Sliced;
        img.color = badgeColor;

        GameObject txtGo = new GameObject("BadgeText", typeof(RectTransform), typeof(Text));
        txtGo.transform.SetParent(badgeGo.transform, false);
        RectTransform tr = txtGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(8f, 2f);
        tr.offsetMax = new Vector2(-8f, -2f);

        Text t = txtGo.GetComponent<Text>();
        t.font = PrimaryFont;
        t.fontSize = 12;
        t.color = textColor;
        t.alignment = TextAnchor.MiddleCenter;
        t.text = text;

        return badgeGo;
    }
}
