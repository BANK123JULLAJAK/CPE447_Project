using UnityEngine;
using UnityEditor;
using System.IO;

public static class ConfigureVRoidToonShaders
{
    [MenuItem("Tools/Apply VRoid MToon UTS3 Shaders")]
    public static void Apply()
    {
        Debug.Log("[VRoid Toon] Starting configuration of MToon / UTS3 toon shaders for all VRoid characters...");

        // ใช้รุ่นที่มี pass Outline จริง ๆ (lilToon/Hidden/lilToonCutout ไม่มี outline pass)
        var lilToonOpaque = Shader.Find("Hidden/lilToonOutline");
        if (lilToonOpaque == null) lilToonOpaque = Shader.Find("lilToon");
        var lilToonCutout = Shader.Find("Hidden/lilToonCutoutOutline");
        if (lilToonCutout == null) lilToonCutout = Shader.Find("Hidden/lilToonCutout");
        var lilToonTransparent = Shader.Find("Hidden/lilToonTransparent");

        if (lilToonOpaque == null)
        {
            Debug.LogError("[VRoid Toon] lilToon shader not found!");
            return;
        }

        // Configure Newman (CH1) materials
        ConfigureNewmanMaterials(lilToonOpaque, lilToonCutout, lilToonTransparent);

        // Configure Jack (CH2) materials
        ConfigureJackMaterials(lilToonOpaque, lilToonCutout, lilToonTransparent);

        // Update Scene instances
        ConfigureSceneRenderers(lilToonOpaque, lilToonCutout, lilToonTransparent);

        AssetDatabase.SaveAssets();
        var currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(currentScene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        Debug.Log("[VRoid Toon] All VRoid character materials successfully updated to MToon/UTS3 and saved!");
    }

    private static void ConfigureNewmanMaterials(Shader opaque, Shader cutout, Shader transparent)
    {
        string dir = "Assets/3D_Model/VAM/CH1/CH2_Newman.Materials";
        var assets = Directory.GetFiles(dir, "*.asset");
        foreach (var path in assets)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;
            ApplyToonSettings(mat, mat.name, opaque, cutout, transparent);
            EditorUtility.SetDirty(mat);
        }
        Debug.Log($"[VRoid Toon] Configured Newman materials in {dir}");
    }

    private static void ConfigureJackMaterials(Shader opaque, Shader cutout, Shader transparent)
    {
        string[] dirs = { "Assets/3D_Model/VAM/CH2/Materials_URP", "Assets/3D_Model/VAM/CH2/Jack.Materials" };
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            var files = Directory.GetFiles(dir, "*.*");
            foreach (var path in files)
            {
                if (!path.EndsWith(".mat") && !path.EndsWith(".asset")) continue;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                ApplyToonSettings(mat, mat.name, opaque, cutout, transparent);
                EditorUtility.SetDirty(mat);
            }
            Debug.Log($"[VRoid Toon] Configured Jack materials in {dir}");
        }
    }

    public static void ApplyToonSettings(Material mat, string name, Shader opaque, Shader cutout, Shader transparent)
    {
        string lower = name.ToLower();

        // 1. Determine Shader and Render Mode
        if (lower.Contains("brow") || lower.Contains("eyeline") || lower.Contains("eyeiris") || lower.Contains("eyehighlight"))
        {
            // Transparent Facial Features
            mat.shader = transparent != null ? transparent : opaque;
            if (mat.HasProperty("_TransparentMode")) mat.SetFloat("_TransparentMode", 2f); // Transparent
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // Double-sided
            if (mat.HasProperty("_UseOutline")) mat.SetFloat("_UseOutline", 0f);
            mat.renderQueue = lower.Contains("eyehighlight") ? 3001 : 3000;
        }
        else if (lower.Contains("mouth") || lower.Contains("hair"))
        {
            // Cutout (Hair and Mouth)
            mat.shader = cutout != null ? cutout : opaque;
            if (mat.HasProperty("_TransparentMode")) mat.SetFloat("_TransparentMode", 1f); // Cutout
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // Double-sided for hair strands!
            mat.renderQueue = 2450;

            if (lower.Contains("hair"))
            {
                SetupUTS3Outline(mat, 0.12f, new Color(0.20f, 0.18f, 0.22f, 1f));
                SetupUTS3Shadow(mat, new Color(0.80f, 0.78f, 0.85f, 1f), new Color(0.65f, 0.62f, 0.72f, 1f), 0.50f, 0.05f);
            }
            else
            {
                if (mat.HasProperty("_UseOutline")) mat.SetFloat("_UseOutline", 0f);
            }
        }
        else if (lower.Contains("skin") || lower.Contains("body") || lower.Contains("face"))
        {
            // Opaque Skin
            mat.shader = opaque;
            if (mat.HasProperty("_TransparentMode")) mat.SetFloat("_TransparentMode", 0f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 2f); // Back cull
            mat.renderQueue = 2000;

            // Warm Anime UTS3 Skin Shadow
            SetupUTS3Shadow(mat, new Color(0.96f, 0.82f, 0.82f, 1f), new Color(0.88f, 0.70f, 0.72f, 1f), 0.52f, 0.08f);
            // Soft warm skin outline
            SetupUTS3Outline(mat, 0.08f, new Color(0.65f, 0.45f, 0.40f, 1f));
        }
        else if (lower.Contains("eyewhite"))
        {
            mat.shader = opaque;
            if (mat.HasProperty("_TransparentMode")) mat.SetFloat("_TransparentMode", 0f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 2f);
            mat.renderQueue = 2000;
            SetupUTS3Shadow(mat, new Color(0.92f, 0.92f, 0.95f, 1f), new Color(0.82f, 0.82f, 0.88f, 1f), 0.50f, 0.05f);
            if (mat.HasProperty("_UseOutline")) mat.SetFloat("_UseOutline", 0f);
        }
        else
        {
            // Clothes: Tops, Bottoms, Shoes, SilkHat, Accessories
            mat.shader = opaque; // CRUCIAL: Must be main lilToon, NEVER Hidden/lilToonOutline!
            if (mat.HasProperty("_TransparentMode")) mat.SetFloat("_TransparentMode", 0f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // Double-sided so clothing linings don't vanish!
            mat.renderQueue = 2000;

            // UTS3 Cool tinted cloth shadow
            SetupUTS3Shadow(mat, new Color(0.82f, 0.82f, 0.88f, 1f), new Color(0.68f, 0.68f, 0.75f, 1f), 0.50f, 0.05f);
            // Clean fine anime outline
            SetupUTS3Outline(mat, 0.12f, new Color(0.15f, 0.15f, 0.18f, 1f));
        }

        // Synchronize Textures
        SyncTextures(mat);
    }

    private static void SetupUTS3Shadow(Material mat, Color shadow1, Color shadow2, float border, float blur)
    {
        if (mat.HasProperty("_UseShadow")) mat.SetFloat("_UseShadow", 1f);
        if (mat.HasProperty("_ShadowStrength")) mat.SetFloat("_ShadowStrength", 1.0f);
        if (mat.HasProperty("_ShadowColor")) mat.SetColor("_ShadowColor", shadow1);
        if (mat.HasProperty("_ShadowBorder")) mat.SetFloat("_ShadowBorder", border);
        if (mat.HasProperty("_ShadowBlur")) mat.SetFloat("_ShadowBlur", blur);
        if (mat.HasProperty("_Shadow2ndColor")) mat.SetColor("_Shadow2ndColor", shadow2);
        if (mat.HasProperty("_Shadow2ndBorder")) mat.SetFloat("_Shadow2ndBorder", border + 0.02f);
        if (mat.HasProperty("_Shadow2ndBlur")) mat.SetFloat("_Shadow2ndBlur", blur);
        if (mat.HasProperty("_ShadowReceive")) mat.SetFloat("_ShadowReceive", 1f);
    }

    private static void SetupUTS3Outline(Material mat, float width, Color color)
    {
        if (mat.HasProperty("_UseOutline")) mat.SetFloat("_UseOutline", 1f);
        if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", width);
        if (mat.HasProperty("_OutlineLitColor")) mat.SetColor("_OutlineLitColor", color);
        if (mat.HasProperty("_OutlineLitApplyTex")) mat.SetFloat("_OutlineLitApplyTex", 0.5f); // Blend with base texture for natural outline
    }

    private static void SyncTextures(Material mat)
    {
        var mainTex = mat.GetTexture("_MainTex");
        var baseMap = mat.GetTexture("_BaseMap");
        var tex = mainTex != null ? mainTex : baseMap;

        if (tex != null)
        {
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_ShadowColorTex")) mat.SetTexture("_ShadowColorTex", tex);
        }
    }

    private static void ConfigureSceneRenderers(Shader opaque, Shader cutout, Shader transparent)
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        foreach (var r in renderers)
        {
            var root = r.transform.root;
            if (root.name.Contains("Newman") || root.name.Contains("Jack") || root.name.Contains("Player"))
            {
                var sharedMats = r.sharedMaterials;
                bool modified = false;
                for (int i = 0; i < sharedMats.Length; i++)
                {
                    var m = sharedMats[i];
                    if (m != null && (m.name.Contains("N00_") || m.name.Contains("Accessory") || m.name.Contains("Newman") || m.name.Contains("Jack")))
                    {
                        ApplyToonSettings(m, m.name, opaque, cutout, transparent);
                        EditorUtility.SetDirty(m);
                        modified = true;
                    }
                }
                if (modified)
                {
                    EditorUtility.SetDirty(r);
                }
            }
        }
        Debug.Log("[VRoid Toon] Configured all scene character renderers.");
    }
}
