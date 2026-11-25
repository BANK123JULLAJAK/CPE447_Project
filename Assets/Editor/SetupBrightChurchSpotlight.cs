using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SetupBrightChurchSpotlight : EditorWindow
{
    [MenuItem("Tools/🔦 ติดตั้งสปอตไลต์สว่างๆ ส่องโบสถ์ (Bright Church Spotlight)")]
    public static void ShowWindow()
    {
        var window = GetWindow<SetupBrightChurchSpotlight>("Church Spotlight");
        window.minSize = new Vector2(380, 260);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("🔦 ติดตั้งสปอตไลต์สว่างๆ ส่องในโบสถ์", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "เครื่องมือนี้จะลบโมเดลลำแสงเก่าออกทั้งหมด และสร้างไฟ Spot Light กำลังสว่างสูง ส่องผ่านหน้าต่างกระจกเข้ามาที่แท่นบูชาและม้านั่งในโบสถ์อย่างชัดเจนและสะอาดตา",
            MessageType.Info);

        GUILayout.Space(10);
        if (GUILayout.Button("💡 ติดตั้งสปอตไลต์สว่างๆ ทันที", GUILayout.Height(40)))
        {
            ApplyBrightSpotlight();
        }

        GUILayout.Space(8);
        if (GUILayout.Button("🗑️ ลบสปอตไลต์โบสถ์ทั้งหมดออก", GUILayout.Height(30)))
        {
            RemoveAllChurchSpotlights();
        }
    }

    [MenuItem("Tools/🔦 ติดตั้งสปอตไลต์สว่างๆ ส่องโบสถ์ (กดติดตั้งทันที)")]
    public static void ApplyBrightSpotlight()
    {
        // 1. Clean up old beams & previous objects
        RemoveAllChurchSpotlights();

        // 2. Find Church Renderer & exact Stained Glass window position
        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>();
        Renderer churchRenderer = null;
        int stainedSubmeshIndex = -1;

        foreach (var r in renderers)
        {
            var sharedMats = r.sharedMaterials;
            for (int i = 0; i < sharedMats.Length; i++)
            {
                if (sharedMats[i] != null && sharedMats[i].name.ToLower().Contains("stained"))
                {
                    churchRenderer = r;
                    stainedSubmeshIndex = i;
                    break;
                }
            }
            if (churchRenderer != null) break;
        }

        Vector3 windowPos = new Vector3(31f, 6.5f, 31f); // default approximate inside church
        if (churchRenderer != null)
        {
            MeshFilter mf = churchRenderer.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && stainedSubmeshIndex >= 0 && stainedSubmeshIndex < mf.sharedMesh.subMeshCount)
            {
                var subBounds = mf.sharedMesh.GetSubMesh(stainedSubmeshIndex).bounds;
                windowPos = churchRenderer.transform.TransformPoint(subBounds.center);
            }
        }

        // 3. Create Root & Bright Spotlight
        GameObject root = new GameObject("[Bright_Church_Spotlight]");
        root.transform.position = windowPos;

        // Position spotlight right at the top of the altar window, shining down onto altar & pews
        GameObject spotGo = new GameObject("Spotlight_Altar_Window");
        spotGo.transform.SetParent(root.transform);
        spotGo.transform.position = windowPos + new Vector3(0f, 1.5f, 1.0f);

        // Aim towards altar and aisle
        Vector3 targetPos = windowPos + new Vector3(0f, -4.5f, -8.0f);
        spotGo.transform.LookAt(targetPos);

        Light spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(0.85f, 0.92f, 1.0f); // Clean, bright silvery-white moonlight
        spot.intensity = 30f; // Very bright as requested
        spot.range = 30f;
        spot.spotAngle = 65f;
        spot.innerSpotAngle = 35f;
        spot.shadows = LightShadows.Soft;
        spot.shadowStrength = 0.85f;

        EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();

        Debug.Log("<color=green><b>[SUCCESS]</b> ติดตั้งสปอตไลต์สว่างๆ ส่องโบสถ์เรียบร้อยแล้ว!</color>");
        EditorUtility.DisplayDialog("เสร็จสิ้น", "ลบลำแสงเก่าออก และติดตั้งสปอตไลต์สว่างๆ เรียบร้อยแล้วครับ!", "ตกลง");
    }

    public static void RemoveAllChurchSpotlights()
    {
        string[] toDelete = new[] { "[Cinematic_Church_Moonlight]", "[Church_Moonlight_Beams]", "[Bright_Church_Spotlight]" };
        foreach (var name in toDelete)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        EditorSceneManager.MarkAllScenesDirty();
    }
}
