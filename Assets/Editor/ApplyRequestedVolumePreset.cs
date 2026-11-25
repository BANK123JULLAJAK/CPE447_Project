using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;

/// <summary>
/// เครื่องมือตั้งค่าพรีเซ็ทภาพ Global Volume ให้ตรงตามรูปภาพของผู้ใช้อย่างแม่นยำ 100%
/// - Tonemapping: Neutral
/// - Bloom: Threshold 0.9, Intensity 0.88, Scatter 0.7, Warm Tint, High Quality Filtering
/// - Color Adjustments: Post Exposure 0.08, Contrast 18, Color Filter HDR (Off-white), Saturation 14
/// - Vignette: Black, Intensity 0.28, Smoothness 0.55
/// - Film Grain: Inactive / Intensity 0
/// - Motion Blur: Camera Only, Medium, Intensity 0.18
/// </summary>
public static class ApplyRequestedVolumePreset
{
    [MenuItem("Tools/🎨 ตั้งค่าพรีเซ็ทภาพตามรูป (Apply Exact Volume Preset)")]
    public static void ApplyPreset()
    {
        string profilePath = "Assets/Settings/LondonDetectiveNightProfile.asset";
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }

        // 1. Tonemapping (Neutral)
        if (!profile.TryGet<Tonemapping>(out var tone))
        {
            tone = profile.Add<Tonemapping>(true);
        }
        tone.active = true;
        tone.mode.overrideState = true;
        tone.mode.value = TonemappingMode.Neutral;

        // 2. Bloom (Warm amber gold diffuse)
        if (!profile.TryGet<Bloom>(out var bloom))
        {
            bloom = profile.Add<Bloom>(true);
        }
        bloom.active = true;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.9f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.88f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.7f;
        bloom.tint.overrideState = true;
        bloom.tint.value = new Color(1.0f, 0.75f, 0.44f, 1.0f); // Warm golden amber
        bloom.clamp.overrideState = false;
        bloom.highQualityFiltering.overrideState = true;
        bloom.highQualityFiltering.value = true;
        bloom.dirtIntensity.overrideState = false;

        // 3. Color Adjustments
        if (!profile.TryGet<ColorAdjustments>(out var colorAdj))
        {
            colorAdj = profile.Add<ColorAdjustments>(true);
        }
        colorAdj.active = true;
        colorAdj.postExposure.overrideState = true;
        colorAdj.postExposure.value = 0.08f;
        colorAdj.contrast.overrideState = true;
        colorAdj.contrast.value = 18f;
        colorAdj.colorFilter.overrideState = true;
        colorAdj.colorFilter.value = new Color(1.0f, 0.98f, 0.94f, 1.0f); // Light warm off-white HDR
        colorAdj.hueShift.overrideState = false;
        colorAdj.saturation.overrideState = true;
        colorAdj.saturation.value = 14f;

        // 4. Vignette
        if (!profile.TryGet<Vignette>(out var vignette))
        {
            vignette = profile.Add<Vignette>(true);
        }
        vignette.active = true;
        vignette.color.overrideState = true;
        vignette.color.value = Color.black;
        vignette.center.overrideState = false;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.28f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.55f;
        vignette.rounded.overrideState = false;

        // 5. Film Grain (Inactive)
        if (!profile.TryGet<FilmGrain>(out var grain))
        {
            grain = profile.Add<FilmGrain>(true);
        }
        grain.active = false;
        grain.type.overrideState = false;
        grain.type.value = FilmGrainLookup.Thin1;
        grain.intensity.overrideState = false;
        grain.intensity.value = 0f;
        grain.response.overrideState = false;
        grain.response.value = 0.85f;

        // 6. Motion Blur (Camera Only, Medium, Intensity 0.18)
        if (!profile.TryGet<MotionBlur>(out var mb))
        {
            mb = profile.Add<MotionBlur>(true);
        }
        mb.active = true;
        mb.mode.overrideState = true;
        mb.mode.value = MotionBlurMode.CameraOnly;
        mb.quality.overrideState = true;
        mb.quality.value = MotionBlurQuality.Medium;
        mb.intensity.overrideState = true;
        mb.intensity.value = 0.18f;
        mb.clamp.overrideState = false;

        // 7. Depth of Field (Bokeh Mode - Managed dynamically by Cinematic Cutscenes, inactive by default in gameplay)
        if (!profile.TryGet<DepthOfField>(out var dof))
        {
            dof = profile.Add<DepthOfField>(true);
            if (!AssetDatabase.Contains(dof))
            {
                AssetDatabase.AddObjectToAsset(dof, profile);
            }
        }
        dof.active = false;
        dof.mode.overrideState = true;
        dof.mode.value = DepthOfFieldMode.Bokeh;
        dof.focusDistance.overrideState = true;
        dof.focusDistance.value = 10f;
        dof.focalLength.overrideState = true;
        dof.focalLength.value = 50f;
        dof.aperture.overrideState = true;
        dof.aperture.value = 2.8f;

        // Remove overrides not present in the user's configuration
        if (profile.TryGet<ShadowsMidtonesHighlights>(out var smh)) profile.Remove<ShadowsMidtonesHighlights>();
        if (profile.TryGet<ChromaticAberration>(out var ca)) profile.Remove<ChromaticAberration>();
        if (profile.TryGet<WhiteBalance>(out var wb)) profile.Remove<WhiteBalance>();

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        // Assign to Global Volume in scene
        var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include);
        foreach (var v in volumes)
        {
            if (v.isGlobal)
            {
                v.profile = profile;
                v.weight = 1f;
                v.priority = 0;
                EditorUtility.SetDirty(v);
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("<color=green><b>[SUCCESS]</b> ตั้งค่าพรีเซ็ทภาพ Global Volume ตามรูปเรียบร้อยแล้ว!</color>");
    }
}
