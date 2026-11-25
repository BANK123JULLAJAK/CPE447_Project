using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// อัปเกรดวัสดุ VRoid (lilToon) ให้ได้ฟีลอนิเมะแบบ Genshin / Wuthering Waves / Endfield:
/// เงา 2 ชั้นโทนอมม่วง-ชมพู, ขอบแสง (Rim) อุ่น, Backlight, ไฮไลต์ผม (Anisotropy), เส้นขอบสีเข้มจากสีพื้น,
/// จำกัดแสงขั้นต่ำ/สูงสุดให้ตัวละครไม่แบนและไม่ขาวจ้า เพื่อให้ตัวละครกลืนไปกับฉากและไม่ดู "ลอย"
/// รันต่อจาก ConfigureVRoidToonShaders.Apply() (เมนู Tools/Anime Look)
/// </summary>
public static class ConfigureAnimeCharacterLook
{
    private static readonly string[] Dirs =
    {
        "Assets/3D_Model/VAM/CH1/CH2_Newman.Materials",
        "Assets/3D_Model/VAM/CH2/Materials_URP",
        "Assets/3D_Model/VAM/CH2/Jack.Materials",
    };

    [MenuItem("Tools/Anime Look - Genshin/WuWa/Endfield Character Shading")]
    public static void Apply()
    {
        ConfigureVRoidToonShaders.Apply();
        int count = 0;
        foreach (var dir in Dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var path in Directory.GetFiles(dir))
            {
                if (!path.EndsWith(".mat") && !path.EndsWith(".asset")) continue;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null || !mat.shader.name.ToLower().Contains("liltoon")) continue;
                Upgrade(mat);
                EditorUtility.SetDirty(mat);
                count++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Anime Look] upgraded {count} materials");
    }

    private static void F(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
    private static void C(Material m, string p, Color v) { if (m.HasProperty(p)) m.SetColor(p, v); }

    private static void Upgrade(Material m)
    {
        string n = m.name.ToLower();
        bool face = n.Contains("eye") || n.Contains("brow") || n.Contains("mouth") || n.Contains("eyeline");
        bool hair = n.Contains("hair");
        bool skin = n.Contains("skin") || n.Contains("body") || n.Contains("face");

        // แสงรอบตัว: ไม่ให้มืดสนิทหรือขาวจ้า -> ตัวละครอ่านออกในฉากมืดแต่ยังรับโทนแสงฉาก
        F(m, "_LightMinLimit", 0.36f);
        F(m, "_LightMaxLimit", 1.0f);
        F(m, "_MonochromeLighting", 0.7f); // ตัดสีส้มของไฟตะเกียงออก ผิว/เสื้อไม่แดงเป็นแผ่น
        F(m, "_AsUnlit", 0f);
        F(m, "_ShadowMainStrength", 0.35f); // เงาอมสีพื้นผิว (hue-shift shadow)
        F(m, "_ShadowStrength", 1f);
        F(m, "_ShadowReceive", face || skin ? 0f : 0.35f); // ลดเงาหย่อม/ขรุขระจากไฟหลายดวง
        F(m, "_ShadowFlatBorder", 0.5f);
        F(m, "_ShadowFlatBlur", 0.4f);
        F(m, "_ShadowNormalStrength", 0.4f);

        if (face)
        {
            // ตา: สว่างเอง (ไม่โดนฉากกลืน) ให้ดวงตาดูมีชีวิตแบบตัวละครเกมอนิเมะ
            bool eye = n.Contains("eye") && !n.Contains("eyeline") && !n.Contains("brow");
            if (eye)
            {
                F(m, "_MonochromeLighting", 0f);
                F(m, "_LightMinLimit", n.Contains("white") ? 0.55f : 0.85f);
                F(m, "_AsUnlit", n.Contains("highlight") ? 1f : 0.35f);
            }
            else
            {
                F(m, "_MonochromeLighting", 0.7f);
                F(m, "_LightMinLimit", 0.4f);
            }
            return;
        }

        // เส้นขอบสีเข้มเหมือนกันทุกตัวละคร (Jack เดิมเป็นสีม่วงอ่อนจนเส้นขอบหายไป)
        C(m, "_OutlineColor", new Color(0.08f, 0.06f, 0.07f, 1f));

        // Rim light อุ่น (แสงตะเกียงแก๊ส) ให้ขอบตัวละครแยกจากฉากหลัง
        F(m, "_UseRim", 1f);
        C(m, "_RimColor", hair ? new Color(1f, 0.95f, 0.88f, 1f) : new Color(1f, 0.90f, 0.80f, 1f));
        F(m, "_RimBorder", 0.65f);
        F(m, "_RimBlur", 0.35f);
        F(m, "_RimFresnelPower", 3.5f);
        F(m, "_RimEnableLighting", 1f);
        F(m, "_RimMainStrength", 0.15f);
        F(m, "_RimShadowMask", 0.5f);
        F(m, "_RimDirStrength", 0.4f);

        // Backlight นุ่ม ๆ ช่วยให้ตัวละครมีมิติจากด้านหลัง
        F(m, "_UseBacklight", 0f); // ทำให้เสื้อด่างเป็นหย่อมขาว
        C(m, "_BacklightColor", new Color(0.62f, 0.55f, 0.75f, 1f));
        F(m, "_BacklightMainStrength", 0.2f);
        F(m, "_BacklightBorder", 0.3f);
        F(m, "_BacklightBlur", 0.25f);

        if (hair)
        {
            // ปิดสเปกคูลาร์/รีเฟล็กชัน: ไฟตะเกียงหลายดวงทำให้สีผมซีดเป็นเทาขาว (สีผมจริงต้องคงเดิม)
            F(m, "_UseReflection", 0f);
            F(m, "_ApplySpecular", 0f);
            F(m, "_UseAnisotropy", 0f);
            F(m, "_LightMinLimit", 0.25f);
            F(m, "_RimMainStrength", 0.05f);
            C(m, "_ShadowColor", new Color(0.55f, 0.50f, 0.68f, 1f));
            C(m, "_Shadow2ndColor", new Color(0.38f, 0.34f, 0.52f, 1f));
        }
        else if (skin)
        {
            // ผิว: เงาอมชมพู-ส้ม นุ่ม ไม่มีสเปกคูลาร์
            F(m, "_UseReflection", 0f);
            C(m, "_ShadowColor", new Color(0.90f, 0.74f, 0.74f, 1f));
            C(m, "_Shadow2ndColor", new Color(0.80f, 0.55f, 0.60f, 1f));
            F(m, "_ShadowBorder", 0.50f);
            F(m, "_ShadowBlur", 0.22f);
            F(m, "_Shadow2ndBorder", 0.30f);
            F(m, "_Shadow2ndBlur", 0.15f);
            C(m, "_OutlineLitColor", new Color(0.55f, 0.32f, 0.30f, 1f));
            F(m, "_OutlineWidth", 0.06f);
        }
        else
        {
            // เสื้อผ้า: เงา 2 ขั้นชัด ไม่ใช้สเปกคูลาร์ (กันสีซีด/ด่าง)
            F(m, "_UseReflection", 0f);
            F(m, "_ApplySpecular", 0f);
            C(m, "_ShadowColor", new Color(0.62f, 0.60f, 0.76f, 1f));
            C(m, "_Shadow2ndColor", new Color(0.44f, 0.42f, 0.60f, 1f));
            F(m, "_ShadowBorder", 0.48f);
            F(m, "_ShadowBlur", 0.18f);
            F(m, "_Shadow2ndBorder", 0.28f);
            F(m, "_Shadow2ndBlur", 0.10f);
            C(m, "_OutlineLitColor", new Color(0.12f, 0.10f, 0.14f, 1f));
            F(m, "_OutlineWidth", 0.09f);
        }
    }
}
