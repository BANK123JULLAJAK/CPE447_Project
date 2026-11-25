using UnityEngine;

/// <summary>
/// โหลดไฟล์ภาพ/ฟอนต์/คลิปแอนิเมชันที่ UI สร้างด้วยโค้ดต้องใช้ ให้ทำงานได้ทั้งใน Editor และในเกมที่ Build แล้ว
/// - ใน Editor: โหลดจาก path เดิม (Assets/...) ผ่าน AssetDatabase
/// - ในเกมที่ Build: AssetDatabase ไม่มี จึงโหลดสำเนาจาก Assets/Resources/GameAssets/... (path เดียวกัน ตัด "Assets/" และนามสกุลออก)
/// สำเนาใน Resources สร้างด้วยเมนู Tools/Build Prep - Copy Runtime UI Assets
/// </summary>
public static class GameAssets
{
    public const string ResourcesRoot = "GameAssets/";

    /// <summary>"Assets/UI/Folder/a.png" -> "GameAssets/UI/Folder/a"</summary>
    public static string ToResourcesPath(string assetPath)
    {
        string p = assetPath.Replace('\\', '/');
        if (p.StartsWith("Assets/")) p = p.Substring("Assets/".Length);
        int dot = p.LastIndexOf('.');
        int slash = p.LastIndexOf('/');
        if (dot > slash) p = p.Substring(0, dot);
        return ResourcesRoot + p;
    }

    public static T Load<T>(string assetPath) where T : Object
    {
#if UNITY_EDITOR
        var editorAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (editorAsset != null) return editorAsset;
#endif
        var res = Resources.Load<T>(ToResourcesPath(assetPath));
        return res != null ? res : null;
    }

    /// <summary>โหลด AnimationClip ตัวแรกในไฟล์ FBX (ใน Build ใช้ Resources.Load ซึ่งค้น sub-asset ตามชนิดให้)</summary>
    public static AnimationClip LoadClip(string assetPath)
    {
#if UNITY_EDITOR
        foreach (var o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        }
#endif
        var clips = Resources.LoadAll<AnimationClip>(ToResourcesPath(assetPath));
        foreach (var c in clips) if (c != null && !c.name.StartsWith("__preview__")) return c;
        return Resources.Load<AnimationClip>(ToResourcesPath(assetPath));
    }

    /// <summary>โหลดทุก sub-asset (เช่น AnimationClip ในไฟล์ FBX)</summary>
    public static Object[] LoadAll(string assetPath)
    {
#if UNITY_EDITOR
        var editorAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (editorAssets != null && editorAssets.Length > 0) return editorAssets;
#endif
        return Resources.LoadAll(ToResourcesPath(assetPath));
    }
}
