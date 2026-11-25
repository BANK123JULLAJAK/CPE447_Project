using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// สร้างสไปรต์รูปทรงพื้นฐานด้วยโค้ด (วงกลม, วงแหวน, ขอบมืดรอบจอ) สำหรับ UI สไตล์นักสืบ ไม่ต้องมีไฟล์ภาพ
/// ใช้ร่วมกันระหว่างหน้าสรุปข้อสันนิษฐาน และ UI สแกนหลักฐาน
/// </summary>
public static class DetectiveUIShapes
{
    private static Sprite _circle;
    private static Sprite _vignette;
    private static readonly Dictionary<int, Sprite> _rings = new Dictionary<int, Sprite>();

    /// <summary>วงกลมทึบขอบเนียน</summary>
    public static Sprite Circle
    {
        get
        {
            if (_circle == null)
            {
                const int size = 128;
                var tex = NewTexture(size);
                var px = new Color32[size * size];
                float c = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                        byte a = (byte)(Mathf.Clamp01(c - d + 0.5f) * 255f);
                        px[y * size + x] = new Color32(255, 255, 255, a);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            }
            return _circle;
        }
    }

    /// <summary>
    /// วงแหวนกลวง ความหนาเป็นสัดส่วนของรัศมี (0.01 - 0.5) ใช้กับ Image ชนิด Filled แบบ Radial360 ได้
    /// </summary>
    public static Sprite Ring(float thickness)
    {
        int key = Mathf.RoundToInt(Mathf.Clamp(thickness, 0.01f, 0.5f) * 1000f);
        if (_rings.TryGetValue(key, out var cached) && cached != null) return cached;

        const int size = 256;
        var tex = NewTexture(size);
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        float outer = c;
        float inner = c * (1f - key / 1000f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                byte a = (byte)(Mathf.Clamp01(Mathf.Min(outer - d, d - inner) + 0.5f) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        _rings[key] = sprite;
        return sprite;
    }

    /// <summary>ขอบมืดโทนซีเปียรอบจอ (กลางใส ขอบมืด) ยืดเต็มจอได้</summary>
    public static Sprite Vignette
    {
        get
        {
            if (_vignette == null)
            {
                const int size = 128;
                var tex = NewTexture(size);
                var px = new Color32[size * size];
                float c = (size - 1) * 0.5f;
                float maxD = Mathf.Sqrt(2f) * c;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / maxD;
                        float a = Mathf.SmoothStep(0.42f, 1.02f, d) * 0.7f;
                        px[y * size + x] = new Color32(14, 9, 4, (byte)(a * 255f));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _vignette = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            }
            return _vignette;
        }
    }

    private static Texture2D NewTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }
}
