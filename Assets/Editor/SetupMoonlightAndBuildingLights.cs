using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SetupMoonlightAndBuildingLights : EditorWindow
{

    [MenuItem("Tools/🌕 ตั้งค่าแสงจันทร์สปอตไลต์สีน้ำเงิน & หน้าต่างตึก-หอนาฬิกา (Moonlight & Windows)")]
    public static void ShowWindow()
    {
        var window = GetWindow<SetupMoonlightAndBuildingLights>("Moonlight & Windows");
        window.minSize = new Vector2(460, 380);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("🌕 ปรับหน้าต่างอาคาร หอนาฬิกา และแสงจันทร์สปอตไลต์สีน้ำเงิน", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. หน้าต่างตึก: ปรับแสงเรืองรองสีส้มอุ่น (Warm Amber Glow) ให้สว่างขึ้น เพิ่มบรรยากาศเมืองยามค่ำคืนทั้งสองฝั่งถนน\n" +
            "2. หอนาฬิกา (Big Ben): เพิ่มความสว่างเรืองแสงสีนวลทอง ให้เห็นหน้าปัดนาฬิกาโดดเด่นกลางท้องฟ้าราตรี\n" +
            "3. แสงจันทร์สปอตไลต์ (Blue Moonlight Spotlight): สร้างไฟสปอตไลต์สีน้ำเงินเข้มสว่างสูง สาดส่องบรรยากาศแสงจันทร์ลงมาบนถนนและอาคารอย่างงดงาม",
            MessageType.Info);

        GUILayout.Space(10);
        if (GUILayout.Button("🚀 1-Click ปรับปรุงทั้งหมดทันที (Apply All)", GUILayout.Height(45)))
        {
            ApplyAll();
        }

        GUILayout.Space(8);
        if (GUILayout.Button("1. ปรับหน้าต่างอาคาร & หอนาฬิกาให้สว่างขึ้น", GUILayout.Height(30)))
        {
            BrightenWindowsAndClock();
        }

        if (GUILayout.Button("2. ติดตั้งแสงจันทร์สปอตไลต์สีน้ำเงิน (Blue Spotlight Moonlight)", GUILayout.Height(30)))
        {
            SetupBlueMoonlightSpotlights();
        }
    }

    [MenuItem("Tools/🌕 กดปรับหน้าต่างและแสงจันทร์ทันที")]
    public static void ApplyAll()
    {
        BrightenWindowsAndClock();
        SetupBlueMoonlightSpotlights();

        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("<color=cyan><b>[SUCCESS]</b> ปรับหน้าต่างอาคาร หอนาฬิกา และติดตั้งสปอตไลต์แสงจันทร์สีน้ำเงินเรียบร้อยแล้ว!</color>");
    }

    public static void BrightenWindowsAndClock()
    {
        int windowCount = 0;
        int clockCount = 0;

        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" });
        foreach (string guid in matGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            string name = mat.name.ToLower();

            // 1. Clock Face
            if (name.Contains("clockface") || name.Contains("clock_face") || name == "clock")
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                // Bright warm-white / pale golden glow for Big Ben clock face
                Color clockGlow = new Color(1.2f, 1.15f, 0.95f) * 3.2f;
                mat.SetColor("_EmissionColor", clockGlow);
                EditorUtility.SetDirty(mat);
                clockCount++;
            }
            // 2. Amber / Lit Windows (Main warm glowing windows)
            else if (name.Contains("windowamber") || name.Contains("windowlit") || name.Contains("warmglass"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                // Rich warm amber glow
                Color amberGlow = new Color(1.0f, 0.72f, 0.32f) * 2.8f;
                mat.SetColor("_EmissionColor", amberGlow);
                EditorUtility.SetDirty(mat);
                windowCount++;
            }
            // 3. Dim Windows (Soft warm light so previously dark apartments have cozy life)
            else if (name.Contains("windowdim"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                // Soft cozy warm glow
                Color dimGlow = new Color(1.0f, 0.68f, 0.28f) * 1.5f;
                mat.SetColor("_EmissionColor", dimGlow);
                EditorUtility.SetDirty(mat);
                windowCount++;
            }
        }

        Debug.Log($"[Lighting] ปรับความสว่างหน้าต่างตึก {windowCount} ชิ้น และหอนาฬิกา {clockCount} ชิ้น สำเร็จ");
    }

    public static void SetupBlueMoonlightSpotlights()
    {
        // Clean up any old duplicate root
        GameObject oldRoot = GameObject.Find("[Moonlight_Spotlights]");
        if (oldRoot != null)
        {
            DestroyImmediate(oldRoot);
        }

        GameObject root = new GameObject("[Moonlight_Spotlights]");

        // 1. Main High-Altitude Blue Moonlight Spotlight (Casting down on street, carriage & facades)
        // Street center is around X: 30, Z: -50 to -90, Y: 0.
        GameObject streetMoonlightGo = new GameObject("Spotlight_Moonlight_Street");
        streetMoonlightGo.transform.SetParent(root.transform);
        // Positioned high up in the sky as if from the moon
        streetMoonlightGo.transform.position = new Vector3(15f, 38f, -125f);
        // Aiming down into the street avenue towards the clock tower
        Vector3 streetTarget = new Vector3(32f, 1.5f, -65f);
        streetMoonlightGo.transform.LookAt(streetTarget);

        Light streetSpot = streetMoonlightGo.AddComponent<Light>();
        streetSpot.type = LightType.Spot;
        // Vibrant rich moonlight blue
        streetSpot.color = new Color(0.20f, 0.55f, 1.0f); // Cinematic Electric Blue Moonlight
        streetSpot.intensity = 42f; // Very bright as requested
        streetSpot.range = 95f;
        streetSpot.spotAngle = 75f;
        streetSpot.innerSpotAngle = 35f;
        streetSpot.shadows = LightShadows.Soft;
        streetSpot.shadowStrength = 0.85f;
        streetSpot.shadowBias = 0.05f;
        streetSpot.shadowNormalBias = 0.4f;

        // 2. Secondary Moonlight Spotlight focusing on the Church / Gothic architecture
        // Church stained glass position is around (31f, 6.5f, 31f)
        var renderers = Object.FindObjectsByType<Renderer>();
        Vector3 churchPos = new Vector3(31f, 6.5f, 31f);
        foreach (var r in renderers)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m != null && m.name.ToLower().Contains("stained"))
                {
                    churchPos = r.bounds.center;
                    break;
                }
            }
        }

        GameObject churchMoonlightGo = new GameObject("Spotlight_Moonlight_Church");
        churchMoonlightGo.transform.SetParent(root.transform);
        churchMoonlightGo.transform.position = churchPos + new Vector3(4f, 12f, 16f);
        churchMoonlightGo.transform.LookAt(churchPos + new Vector3(-2f, -1f, -4f));

        Light churchSpot = churchMoonlightGo.AddComponent<Light>();
        churchSpot.type = LightType.Spot;
        churchSpot.color = new Color(0.22f, 0.58f, 1.0f); // Bright blue moonlight
        churchSpot.intensity = 38f;
        churchSpot.range = 45f;
        churchSpot.spotAngle = 65f;
        churchSpot.innerSpotAngle = 30f;
        churchSpot.shadows = LightShadows.Soft;
        churchSpot.shadowStrength = 0.80f;

        // 3. Clean up old temporary church beams/spotlights from previous attempts
        string[] oldObjects = new[] { "[Bright_Church_Spotlight]", "[Cinematic_Church_Moonlight]", "[Church_Moonlight_Beams]" };
        foreach (var objName in oldObjects)
        {
            GameObject o = GameObject.Find(objName);
            if (o != null) DestroyImmediate(o);
        }

        Debug.Log("<color=cyan>[Lighting] ติดตั้งสปอตไลต์แสงจันทร์สีน้ำเงินสว่างสูง (Street & Church) สำเร็จเรียบร้อย</color>");
    }

    [MenuItem("Tools/👤 ติดตั้งตัวละคร CH2_Newman (เรียกจากเมนูแสงจันทร์)")]
    public static void TriggerNewmanSetup()
    {
        SetupNewmanCharacterController.ApplyAll();
    }
}

