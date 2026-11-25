using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

public class SetupLondonNightAtmosphere : EditorWindow
{
    [MenuItem("Tools/🌙 ปรับบรรยากาศลอนดอนกลางคืน (Arknights Endfield & Detective Night)")]
    public static void ShowWindow()
    {
        var window = GetWindow<SetupLondonNightAtmosphere>("London Night");
        window.minSize = new Vector2(440, 480);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("🌙 ตั้งค่าบรรยากาศลอนดอนกลางคืน สไตล์ Arknights: Endfield", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "เครื่องมือนี้จะทำการ:\n" +
            "1. ปรับ Directional Light เป็นแสงจันทร์สลัวสีน้ำเงินคราม (Moonlight)\n" +
            "2. ตั้งค่า Global Volume สไตล์ Arknights: Endfield + สืบสวนย้อนยุค (ACES, คอนทราสต์จัดจ้าน, เงาน้ำเงินคราม, ฟิล์มเกรน, Vignette)\n" +
            "3. ใส่หมอกควันลอนดอนยามค่ำคืน (London Victorian Fog)\n" +
            "4. เพิ่มแสงไฟส้มอุ่น (Warm Gaslight) ตามโคมไฟถนนให้ถนนและรถม้ามีมิติแสงเงา\n" +
            "5. ติดตั้งระบบ Dynamic Night Sky (ท้องฟ้า เมฆ และดวงดาวหมุนเคลื่อนที่ตลอดเวลา)",
            MessageType.Info);

        GUILayout.Space(10);
        if (GUILayout.Button("🚀 ปรับทุกอย่างเป็นบรรยากาศกลางคืนทันที (1-Click Apply All)", GUILayout.Height(50)))
        {
            ApplyAllNightSettings();
        }

        GUILayout.Space(10);
        if (GUILayout.Button("1. ปรับแสงจันทร์และหมอกควัน (Moonlight & Fog)", GUILayout.Height(30)))
        {
            SetupMoonlightAndFog();
        }

        if (GUILayout.Button("2. ตั้งค่า Global Volume (Arknights & Detective Noir)", GUILayout.Height(30)))
        {
            SetupGlobalVolumeProfile();
        }

        if (GUILayout.Button("3. เพิ่มดวงไฟโคมไฟถนนสีส้มอุ่น (Street Gaslights)", GUILayout.Height(30)))
        {
            SetupStreetLanternLights();
        }

        if (GUILayout.Button("4. ติดตั้งไดนามิกท้องฟ้า (Dynamic Night Sky Component)", GUILayout.Height(30)))
        {
            SetupDynamicSkyComponent();
        }
    }

    public static void ApplyAllNightSettings()
    {
        SetupMoonlightAndFog();
        SetupGlobalVolumeProfile();
        SetupStreetLanternLights();
        SetupDynamicSkyComponent();

        EditorSceneManager.MarkAllScenesDirty();
        AssetDatabase.SaveAssets();

        Debug.Log("<color=cyan><b>[SUCCESS]</b> ตั้งค่าบรรยากาศลอนดอนกลางคืน Arknights: Endfield & Dynamic Sky เรียบร้อยแล้ว!</color>");
        EditorUtility.DisplayDialog("เสร็จสิ้น", "ปรับบรรยากาศลอนดอนยามค่ำคืน สไตล์ Arknights: Endfield และไดนามิกท้องฟ้าเรียบร้อยแล้วครับ!", "ตกลง");
    }

    public static void SetupMoonlightAndFog()
    {
        // 1. Directional Light -> Moonlight
        Light dirLight = null;
        var lights = UnityEngine.Object.FindObjectsByType<Light>();
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional)
            {
                dirLight = l;
                break;
            }
        }

        if (dirLight != null)
        {
            dirLight.gameObject.name = "Directional Light (Moonlight)";
            dirLight.transform.rotation = Quaternion.Euler(46f, -38f, 0f); // Cinematic angle
            dirLight.color = new Color(0.85f, 0.88f, 0.92f); // Soft neutral moonlight
            dirLight.intensity = 0.12f; // Dim natural night moonlight
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.92f;
            dirLight.shadowNormalBias = 0.4f;
            dirLight.shadowBias = 0.05f;
            EditorUtility.SetDirty(dirLight);
        }

        // 2. RenderSettings: Skybox & Ambient & Fog
        Material nightSky = AssetDatabase.LoadAssetAtPath<Material>("Assets/SkySeries Freebie/CoriolisNight4k.mat");
        if (nightSky != null)
        {
            if (nightSky.HasProperty("_Exposure"))
            {
                nightSky.SetFloat("_Exposure", 0.18f); // Truly dark night sky
            }
            RenderSettings.skybox = nightSky;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.015f, 0.015f, 0.018f); // Dark neutral night
        RenderSettings.ambientEquatorColor = new Color(0.01f, 0.01f, 0.012f);
        RenderSettings.ambientGroundColor = new Color(0.005f, 0.005f, 0.005f);
        RenderSettings.subtractiveShadowColor = new Color(0.02f, 0.02f, 0.02f);

        // Turn off heavy fog to keep night sky and city crisp & truly dark
        RenderSettings.fog = false;

        Debug.Log("[London Night] ตั้งค่าแสงจันทร์และท้องฟ้ามืดสนิทสำเร็จ");
    }

    public static void SetupGlobalVolumeProfile()
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
        bloom.tint.value = new Color(1.0f, 0.75f, 0.44f, 1.0f);
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
        colorAdj.colorFilter.value = new Color(1.0f, 0.98f, 0.94f, 1.0f);
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

        // Clean up overrides not present in image
        if (profile.TryGet<ShadowsMidtonesHighlights>(out var smh)) profile.Remove<ShadowsMidtonesHighlights>();
        if (profile.TryGet<ChromaticAberration>(out var chroma)) profile.Remove<ChromaticAberration>();
        if (profile.TryGet<WhiteBalance>(out var wb)) profile.Remove<WhiteBalance>();

        EditorUtility.SetDirty(profile);

        // Assign to Global Volume in scene
        Volume globalVol = null;
        var volumes = UnityEngine.Object.FindObjectsByType<Volume>();
        foreach (var v in volumes)
        {
            if (v.isGlobal)
            {
                globalVol = v;
                break;
            }
        }

        if (globalVol == null)
        {
            GameObject volGo = new GameObject("Global Volume");
            globalVol = volGo.AddComponent<Volume>();
            globalVol.isGlobal = true;
        }

        globalVol.sharedProfile = profile;
        EditorUtility.SetDirty(globalVol);

        Debug.Log("[London Night] ตั้งค่า Global Volume สำเร็จ");
    }

    public static void SetupStreetLanternLights()
    {
        // Group under root object
        GameObject lightsRoot = GameObject.Find("[Street_Lantern_Lights]");
        if (lightsRoot == null)
        {
            lightsRoot = new GameObject("[Street_Lantern_Lights]");
        }

        // Find renderers using lantern glow materials or lamp meshes
        var renderers = UnityEngine.Object.FindObjectsByType<Renderer>();
        List<Vector3> lightPositions = new List<Vector3>();

        foreach (var r in renderers)
        {
            bool isLamp = false;
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                string mName = m.name.ToLower();
                if (mName.Contains("lanternglow") || mName.Contains("lampext"))
                {
                    isLamp = true;
                    break;
                }
            }

            if (isLamp)
            {
                Vector3 center = r.bounds.center;
                // Avoid placing lights too close to each other
                bool tooClose = false;
                foreach (var pos in lightPositions)
                {
                    if (Vector3.Distance(pos, center) < 3.0f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (!tooClose)
                {
                    lightPositions.Add(center);
                }
            }
        }

        // Clear previous child lights if any
        while (lightsRoot.transform.childCount > 0)
        {
            DestroyImmediate(lightsRoot.transform.GetChild(0).gameObject);
        }

        int count = 0;
        foreach (var pos in lightPositions)
        {
            GameObject lightObj = new GameObject($"Lantern_Light_{count + 1}");
            lightObj.transform.SetParent(lightsRoot.transform);
            lightObj.transform.position = pos;

            Light pLight = lightObj.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = new Color(1.0f, 0.64f, 0.22f); // Warm gaslight amber
            pLight.intensity = 3.6f;
            pLight.range = 10f;
            pLight.shadows = (count % 3 == 0) ? LightShadows.Soft : LightShadows.None; // Enable soft shadows on select key lights for performance
            count++;
        }

        Debug.Log($"[London Night] ติดตั้งดวงไฟโคมไฟถนนสีส้มอุ่นทั้งหมด {count} จุด");
    }

    public static void SetupDynamicSkyComponent()
    {
        GameObject ctrl = GameObject.Find("[Night_Atmosphere_Controller]");
        if (ctrl == null)
        {
            ctrl = new GameObject("[Night_Atmosphere_Controller]");
        }

        DynamicNightSky dynamicSky = ctrl.GetComponent<DynamicNightSky>();
        if (dynamicSky == null)
        {
            dynamicSky = ctrl.AddComponent<DynamicNightSky>();
        }

        dynamicSky.autoRotateSky = true;
        dynamicSky.rotationSpeed = 0.5f;

        EditorUtility.SetDirty(ctrl);
        Debug.Log("[London Night] ติดตั้ง Dynamic Night Sky สำเร็จ");
    }
}
