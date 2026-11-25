using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ตัวปรับแต่งกราฟิกและแสงเงาระดับภาพยนตร์ AAA (Old London Gaslight Night - มืด สลัว โทนส้ม สีสด)
/// ปรับบรรยากาศตามที่ผู้เล่นต้องการ:
/// 1. ลอนดอนยุคเก่าตอนกลางคืน (มืดๆ ได้ฟีลราตรีแท้จริง ไม่สว่างจ้า)
/// 2. แสงไฟตะเกียงแก๊สสลัวๆ (Dim & Cozy Gaslamp) สีส้มทองอบอุ่น
/// 3. สีสันสดใสเข้มข้น (Rich Saturation + ACES Filmic Contrast)
/// 4. โทนรวมออกส้มอบอุ่นสไตล์วิกตอเรียโบราณ (Warm Amber Gaslight Era)
/// </summary>
public static class ConfigureCinematicLighting
{
    [MenuItem("Tools/🎬 ปรับแต่งกราฟิก Global Volume & แสงเงาภาพยนตร์ (สวยเริ่ด + เบาเครื่อง)")]
    public static void ApplyCinematicLighting()
    {
        // =========================================================================
        // 1. ปรับแต่ง Global Volume Profile (LondonDetectiveNightProfile)
        // =========================================================================
        GameObject gv = GameObject.Find("Global Volume");
        if (gv == null)
        {
            gv = new GameObject("Global Volume", typeof(Volume));
            var v = gv.GetComponent<Volume>();
            v.isGlobal = true;
        }

        var volume = gv.GetComponent<Volume>();
        if (volume.sharedProfile == null)
        {
            var existingProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/LondonDetectiveNightProfile.asset");
            if (existingProfile == null)
            {
                existingProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(existingProfile, "Assets/Settings/LondonDetectiveNightProfile.asset");
            }
            volume.sharedProfile = existingProfile;
        }

        VolumeProfile profile = volume.sharedProfile;

        // 1.1 Tonemapping: Neutral
        Tonemapping tonemapping;
        if (!profile.TryGet(out tonemapping)) tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.active = true;
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.Neutral;

        // 1.2 Bloom: Warm Golden Amber (#FFBF70)
        Bloom bloom;
        if (!profile.TryGet(out bloom)) bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.90f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.88f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.70f;
        bloom.tint.overrideState = true;
        bloom.tint.value = new Color(1.0f, 0.75f, 0.44f, 1.0f); // Warm Golden Amber (#FFBF70)
        bloom.clamp.overrideState = false;
        bloom.highQualityFiltering.overrideState = true;
        bloom.highQualityFiltering.value = true;
        bloom.dirtIntensity.overrideState = false;

        // 1.3 ColorAdjustments: Contrast 18, PostExposure 0.08, Saturation 14
        ColorAdjustments colorAdjust;
        if (!profile.TryGet(out colorAdjust)) colorAdjust = profile.Add<ColorAdjustments>(true);
        colorAdjust.active = true;
        colorAdjust.postExposure.overrideState = true;
        colorAdjust.postExposure.value = 0.08f;
        colorAdjust.contrast.overrideState = true;
        colorAdjust.contrast.value = 18f; // คอนทราสต์ 18 ตามที่ผู้ใช้ระบุ
        colorAdjust.colorFilter.overrideState = true;
        colorAdjust.colorFilter.value = new Color(1.0f, 0.98f, 0.94f, 1.0f); // HDR Warm Off-White
        colorAdjust.hueShift.overrideState = false;
        colorAdjust.saturation.overrideState = true;
        colorAdjust.saturation.value = 14f;

        // 1.4 Vignette: ดำลึก 0.28 / 0.55
        Vignette vignette;
        if (!profile.TryGet(out vignette)) vignette = profile.Add<Vignette>(true);
        vignette.active = true;
        vignette.color.overrideState = true;
        vignette.color.value = Color.black;
        vignette.center.overrideState = false;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.28f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.55f;
        vignette.rounded.overrideState = false;

        // 1.5 Film Grain: Disabled ปิดถาวร ไม่มี Noise รบกวน
        FilmGrain grain;
        if (!profile.TryGet(out grain)) grain = profile.Add<FilmGrain>(true);
        grain.active = false;
        grain.type.overrideState = false;
        grain.type.value = FilmGrainLookup.Thin1;
        grain.intensity.overrideState = false;
        grain.intensity.value = 0f;
        grain.response.overrideState = false;
        grain.response.value = 0.85f;

        // 1.6 Motion Blur: Camera Only, Medium, Intensity 0.18
        MotionBlur mb;
        if (!profile.TryGet(out mb)) mb = profile.Add<MotionBlur>(true);
        mb.active = true;
        mb.mode.overrideState = true;
        mb.mode.value = MotionBlurMode.CameraOnly;
        mb.quality.overrideState = true;
        mb.quality.value = MotionBlurQuality.Medium;
        mb.intensity.overrideState = true;
        mb.intensity.value = 0.18f;
        mb.clamp.overrideState = false;

        // ลบ Overrides ที่ไม่ได้อยู่ในพรีเซ็ทที่ต้องการออกให้หมด
        if (profile.TryGet<ShadowsMidtonesHighlights>(out var smh)) profile.Remove<ShadowsMidtonesHighlights>();
        if (profile.TryGet<WhiteBalance>(out var wb)) profile.Remove<WhiteBalance>();
        if (profile.TryGet<ChromaticAberration>(out var ca)) profile.Remove<ChromaticAberration>();

        EditorUtility.SetDirty(profile);

        // =========================================================================
        // 2. ปรับแต่ง Screen Space Ambient Occlusion (SSAO) แบบเนียนกริบ ไร้เม็ด Dither Noise
        // =========================================================================
        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset != null)
        {
            var so = new SerializedObject(urpAsset);
            var rendererDataProp = so.FindProperty("m_RendererDataList");
            if (rendererDataProp != null && rendererDataProp.arraySize > 0)
            {
                var rendererData = rendererDataProp.GetArrayElementAtIndex(0).objectReferenceValue as UniversalRendererData;
                if (rendererData != null)
                {
                    foreach (var feature in rendererData.rendererFeatures)
                    {
                        if (feature != null && feature.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"))
                        {
                            var fSo = new SerializedObject(feature);
                            var sDownsample = fSo.FindProperty("m_Settings.Downsample");
                            var sIntensity = fSo.FindProperty("m_Settings.Intensity");
                            var sDirectLighting = fSo.FindProperty("m_Settings.DirectLightingStrength");
                            var sRadius = fSo.FindProperty("m_Settings.Radius");
                            var sSamples = fSo.FindProperty("m_Settings.Samples");
                            var sBlur = fSo.FindProperty("m_Settings.BlurQuality");

                            // ปิด Downsample: รัน AO ที่ความละเอียดเต็ม ป้องกันรอยกระด่าง / Noise เม็ดทรายตามซอกกำแพง
                            if (sDownsample != null) sDownsample.boolValue = false;
                            if (sSamples != null) sSamples.enumValueIndex = 2; // High Sample Count (เนียนละเอียด ไม่เป็นจุดรบกวน)
                            if (sBlur != null) sBlur.enumValueIndex = 2; // High Blur Quality (เบลอเกลี่ยเงานุ่มนวล 0% Noise)
                            if (sDirectLighting != null) sDirectLighting.floatValue = 0.05f; // ลดการกวนของเงาใต้แสงตรง
                            if (sIntensity != null) sIntensity.floatValue = 0.80f;
                            if (sRadius != null) sRadius.floatValue = 0.40f;

                            fSo.ApplyModifiedProperties();
                            EditorUtility.SetDirty(feature);
                            break;
                        }
                    }
                }
            }

            var shadowDistProp = so.FindProperty("m_ShadowDistance");
            if (shadowDistProp != null) shadowDistProp.floatValue = 42f;

            var addShadowResProp = so.FindProperty("m_AdditionalLightsShadowmapResolution");
            if (addShadowResProp != null) addShadowResProp.intValue = 4096;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(urpAsset);
        }

        // 2.1 เปิด Anti-Aliasing คุณภาพสูงระดับ SMAA (Subpixel Morphological Anti-Aliasing)
        // ช่วยกำจัด Noise และ Shimmering ตามขอบหินและตัวละครได้อย่างหมดจด คมกริบ
        var allCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        foreach (var cam in allCameras)
        {
            var camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null)
            {
                camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                camData.antialiasingQuality = AntialiasingQuality.High;
                EditorUtility.SetDirty(camData);
            }
        }

        // =========================================================================
        // 3. ปรับแต่งพื้นถนนหินเปียกชุ่ม (Wet Cobblestone & Puddles) สะท้อนแสงไฟส้ม
        // =========================================================================
        string[] streetNames = new string[] { "LONDON_STREET", "LONDON_STREET.001" };
        foreach (var sName in streetNames)
        {
            var sGo = GameObject.Find(sName);
            if (sGo == null) continue;
            var mr = sGo.GetComponent<MeshRenderer>();
            if (mr == null) continue;

            foreach (var m in mr.sharedMaterials)
            {
                if (m == null) continue;
                string mName = m.name;
                if (mName.StartsWith("Setts_"))
                {
                    m.SetFloat("_Smoothness", 0.65f); // ก้อนหินเปียก สะท้อนเงาไฟส้มทองระยิบระยับ
                }
                else if (mName.Contains("Puddle"))
                {
                    m.SetFloat("_Smoothness", 0.90f); // แอ่งน้ำสะท้อนแสงไฟตะเกียง
                }
                else if (mName.Contains("WetKerb"))
                {
                    m.SetFloat("_Smoothness", 0.72f);
                }
                else if (mName.Contains("Mortar"))
                {
                    m.SetFloat("_Smoothness", 0.42f);
                }
                else if (mName.Contains("DarkStone") || mName.Contains("Stone"))
                {
                    m.SetFloat("_Smoothness", 0.50f);
                }
                EditorUtility.SetDirty(m);
            }
        }

        // =========================================================================
        // 4. แสงจันทร์ราตรีมืดสลัว (Subtle Dark Midnight Moonlight)
        // =========================================================================
        var dirLightGo = GameObject.Find("Directional Light (Moonlight)") 
                      ?? GameObject.Find("Directional Light");
        if (dirLightGo != null)
        {
            // แสงจันทร์สลัวๆ ไม่สว่างจ้าเกินไป เพื่อให้บรรยากาศมืดและขับให้แสงไฟตะเกียงส้มเด่นชัด
            dirLightGo.transform.rotation = Quaternion.Euler(42f, 195f, 0f);

            var l = dirLightGo.GetComponent<Light>();
            if (l != null)
            {
                l.color = new Color(0.24f, 0.32f, 0.48f); // สีครามเข้มราตรีมืดสลัว
                l.intensity = 0.45f; // ความสว่างต่ำ ให้ฉากมืดสลัวสมจริง (Dark Night)
                l.shadows = LightShadows.Soft;
                l.shadowNormalBias = 0.40f;
                l.shadowBias = 0.05f;
                EditorUtility.SetDirty(l);
            }
            EditorUtility.SetDirty(dirLightGo);
        }

        // =========================================================================
        // 5. โคมไฟถนน "ไฟสลัวๆ โทนส้ม สีสด" (Dim Warm Amber Gaslamps)
        // =========================================================================
        Color richAmberGaslamp = new Color(1.0f, 0.68f, 0.28f, 1.0f); // สีส้มทองอำพันสดใสสไตล์ลอนดอนยุคเก่า
        var allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        foreach (var light in allLights)
        {
            if (light.type == LightType.Point && light.name.StartsWith("Lantern_Light_"))
            {
                light.color = richAmberGaslamp;

                // "ไฟสลัวๆ" ปรับความสว่างให้ออกสลัวกำลังดี อบอุ่น ไม่สาดจ้าแสบตา
                if (light.intensity > 8f || light.intensity < 6f) light.intensity = 7.5f;
                if (light.range > 12f || light.range < 10f) light.range = 11.0f;

                if (light.name == "Lantern_Light_8" || light.name == "Lantern_Light_20")
                {
                    light.shadows = LightShadows.Soft;
                    light.shadowBias = 0.06f;
                    light.shadowNormalBias = 0.4f;
                }
                else
                {
                    light.shadows = LightShadows.None;
                }
                EditorUtility.SetDirty(light);
            }
        }

        // ปรับแสงเรืองของโมเดลดวงโคมไฟเป็นสีส้มอำพันสดใส
        var allMats = Resources.FindObjectsOfTypeAll<Material>();
        Color warmAmberEmission = new Color(5.2f, 3.2f, 1.0f, 1.0f);
        foreach (var mat in allMats)
        {
            if (mat != null && mat.HasProperty("_EmissionColor"))
            {
                string lowerName = mat.name.ToLower();
                if (lowerName.Contains("lanternglow") || lowerName.Contains("lampext"))
                {
                    mat.SetColor("_EmissionColor", warmAmberEmission);
                    EditorUtility.SetDirty(mat);
                }
            }
        }

        // =========================================================================
        // 6. แสง Ambient และหมอกราตรีมืดสลัว (Dark Moody Ambient & Warm Haze)
        // =========================================================================
        RenderSettings.ambientMode = AmbientMode.Trilight;
        // คุมแสง Ambient ให้มืดสลัว ได้บรรยากาศราตรีมืดๆ แต่ยังมองเห็นรายละเอียดสถาปัตยกรรม
        RenderSettings.ambientSkyColor = new Color(0.045f, 0.038f, 0.040f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.038f, 0.030f, 0.028f, 1f);
        RenderSettings.ambientGroundColor = new Color(0.018f, 0.014f, 0.012f, 1f);

        // หมอกราตรีกรุงลอนดอนยุคเก่าปนควันไฟถ่านหินโทนอุ่น
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.055f, 0.042f, 0.035f, 1f); // หมอกราตรีโทนอุ่นมืดสลัว
        RenderSettings.fogDensity = 0.0090f;

        if (RenderSettings.skybox != null)
        {
            if (RenderSettings.skybox.HasProperty("_Tint"))
            {
                RenderSettings.skybox.SetColor("_Tint", new Color(0.22f, 0.20f, 0.22f, 0.5f));
            }
            if (RenderSettings.skybox.HasProperty("_Exposure"))
            {
                RenderSettings.skybox.SetFloat("_Exposure", 0.70f); // ท้องฟ้ามืดสลัว ได้บรรยากาศกลางคืนแท้จริง
            }
            EditorUtility.SetDirty(RenderSettings.skybox);
        }

        // บันทึก Scene และ Asset
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("<color=green><b>[SUCCESS]</b> ปรับแต่งแสงเงาสไตล์ Old London Gaslight Night สำเร็จ! (มืดสลัว + ไฟส้มอำพันสดใส + ราตรีลอนดอนยุคเก่า)</color>");
    }
}
