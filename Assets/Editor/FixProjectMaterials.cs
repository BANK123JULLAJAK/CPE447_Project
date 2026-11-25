using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class FixLondonMaterialsWindow : EditorWindow
{

    [MenuItem("Tools/🛠️ แก้บัคหลังคาและพื้นผิว (Fix Roof & Ground Materials)")]
    public static void ShowWindow()
    {
        var window = GetWindow<FixLondonMaterialsWindow>("Fix Materials");
        window.minSize = new Vector2(420, 360);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("🛠️ ตัวช่วยแก้ไขโมเดลและ Material อัตโนมัติ", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. แก้หลังคาหาย: ปรับ Material ของหลังคาเป็น Double-Sided (Render Face: Both)\n" +
            "2. แก้พื้นไม่นูน: นำเข้า Normal Map ทุกไฟล์ให้ถูกต้อง พร้อมใส่ Normal Map และลดความเงา (Smoothness) ของพื้นผิว",
            MessageType.Info);

        GUILayout.Space(10);
        if (GUILayout.Button("🚀 กดแก้ไขทั้งหมดทันที (1-Click Fix All)", GUILayout.Height(45)))
        {
            FixAll();
        }

        GUILayout.Space(10);
        if (GUILayout.Button("1. ตั้งค่า Texture Type ของ Normal Map ทั้งหมด", GUILayout.Height(30)))
        {
            FixNormalMapTextureTypes();
        }

        if (GUILayout.Button("2. แก้หลังคาหาย (Render Face: Both)", GUILayout.Height(30)))
        {
            FixRoofDoubleSided();
        }

        if (GUILayout.Button("3. ใส่ Normal Map และปรับมิติพื้นผิวถนน/หิน (Setts & Ground)", GUILayout.Height(30)))
        {
            FixGroundMaterials();
        }
    }

    public static void RunAutoFix()
    {
        // Run once on compilation
        if (!SessionState.GetBool("LondonMaterialsAutoFixed", false))
        {
            SessionState.SetBool("LondonMaterialsAutoFixed", true);
            FixAll();
        }
    }

    public static void FixAll()
    {
        FixNormalMapTextureTypes();
        FixSceneMaterials();
        EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();
        Debug.Log("<color=green><b>[SUCCESS]</b> แก้ไขโมเดลหลังคาและพื้นผิวเรียบร้อยแล้ว!</color>");
        EditorUtility.DisplayDialog("เสร็จสิ้น", "แก้ไขโมเดลหลังคา (Double-Sided) และตั้งค่า Normal Map เรียบร้อยแล้วครับ!", "ตกลง");
    }

    public static void FixNormalMapTextureTypes()
    {
        int count = 0;
        string[] allGuids = AssetDatabase.FindAssets("t:Texture", new[] { "Assets/3D_Model/London" });
        foreach (string guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileName(path).ToLower();

            if (fileName.Contains("normal") || fileName.Contains("nor_gl") || fileName.Contains("_nor_"))
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                    count++;
                }
            }
        }
        Debug.Log($"[Fix Materials] อัปเดต Texture Type เป็น Normal Map สำเร็จ {count} ไฟล์");
    }

    public static void FixRoofDoubleSided()
    {
        int count = 0;
        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>();
        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                string matName = m.name.ToLower();
                // Check if it's roof or church geometry
                if (matName.Contains("roof") || matName.Contains("slate") || r.gameObject.name.ToLower().Contains("roof") || matName.Contains("darkstone") || matName.Contains("stone"))
                {
                    Material editableMat = GetOrCreateEditableMaterial(m);
                    if (editableMat.HasProperty("_Cull"))
                    {
                        editableMat.SetFloat("_Cull", 0f); // 0 = Off (Both sides rendered)
                    }
                    editableMat.doubleSidedGI = true;
                    mats[i] = editableMat;
                    changed = true;
                    count++;
                }
            }
            if (changed)
            {
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }
        }
        Debug.Log($"[Fix Materials] ปรับ Material เป็น Double-Sided ทั้งหมด {count} ชิ้น");
    }

    public static void FixGroundMaterials()
    {
        FixSceneMaterials();
    }

    public static void FixSceneMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        // Cache existing normal map textures
        Texture2D stoneNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/3D_Model/London/London.fbm/0_stone_normal.png")
                             ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/3D_Model/London/textures/stone_normal.png");
        Texture2D roofNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/3D_Model/London/Untitled.fbm/grey_roof_01_nor_gl_2k.exr");
        Texture2D brickNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/3D_Model/London/London.fbm/3_brick3_normal.png")
                             ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/3D_Model/London/textures/brick0_normal.png");
        Texture2D slateNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/3D_Model/London/textures/slate_normal.png");

        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>();
        int fixedCount = 0;

        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            bool modified = false;

            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null) continue;

                Material editable = GetOrCreateEditableMaterial(m);
                string nameLower = editable.name.ToLower();

                // 1. Fix Roof - make double sided and set roof normal map
                if (nameLower.Contains("roof") || nameLower.Contains("slate"))
                {
                    if (editable.HasProperty("_Cull"))
                    {
                        editable.SetFloat("_Cull", 0f); // Render both sides
                    }
                    editable.doubleSidedGI = true;
                    if (roofNormal != null && editable.GetTexture("_BumpMap") == null)
                    {
                        editable.SetTexture("_BumpMap", roofNormal);
                        editable.EnableKeyword("_NORMALMAP");
                        editable.SetFloat("_BumpScale", 1.2f);
                    }
                    if (editable.HasProperty("_Smoothness"))
                    {
                        editable.SetFloat("_Smoothness", 0.25f);
                    }
                    modified = true;
                }

                // 2. Fix Setts (Ground Cobblestones) and Street materials
                if (nameLower.Contains("setts") || nameLower.Contains("stone") || nameLower.Contains("mortar") || nameLower.Contains("kerb"))
                {
                    // Enable Normal Map
                    if (editable.GetTexture("_BumpMap") == null)
                    {
                        Texture2D targetNormal = stoneNormal ?? slateNormal;
                        if (targetNormal != null)
                        {
                            editable.SetTexture("_BumpMap", targetNormal);
                            editable.EnableKeyword("_NORMALMAP");
                            editable.SetFloat("_BumpScale", 1.5f);
                        }
                    }
                    else
                    {
                        editable.EnableKeyword("_NORMALMAP");
                        editable.SetFloat("_BumpScale", 1.5f);
                    }

                    // Keep ground matte, clean and natural (no glitter sparkles)
                    if (editable.HasProperty("_Smoothness"))
                    {
                        editable.SetFloat("_Smoothness", 0.05f);
                    }

                    modified = true;
                }

                // 3. Make all walls / building surfaces double-sided as well to prevent any see-through artifacts
                if (editable.HasProperty("_Cull"))
                {
                    editable.SetFloat("_Cull", 0f);
                    editable.doubleSidedGI = true;
                }

                mats[i] = editable;
                fixedCount++;
            }

            if (modified)
            {
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }
        }

        Debug.Log($"[Fix Materials] อัปเดตและปรับแต่ง Material ทั้งหมด {fixedCount} จุด สำเร็จ!");
    }

    private static Material GetOrCreateEditableMaterial(Material src)
    {
        string targetPath = $"Assets/Materials/{src.name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
        if (existing != null)
        {
            return existing;
        }

        Material newMat = new Material(src);
        AssetDatabase.CreateAsset(newMat, targetPath);
        return newMat;
    }
}
