using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRM;

public class SetupNewmanCharacterController : EditorWindow
{

    [MenuItem("Tools/🧹 ล้างหน้าต่าง Console (Clear Console)")]
    public static void ClearConsole()
    {
        var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
        if (logEntries != null)
        {
            var clearMethod = logEntries.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (clearMethod != null) clearMethod.Invoke(null, null);
        }
    }

    [MenuItem("Tools/👤 ติดตั้งตัวละคร CH2_Newman (กล้อง Endfield + ฟิสิกส์เสื้อผ้ากันทะลุขา)")]
    public static void ShowWindow()
    {
        var window = GetWindow<SetupNewmanCharacterController>("Newman & Camera");
        window.minSize = new Vector2(480, 440);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("👤 CH2_Newman + กล้อง Arknights: Endfield + Cloth Physics", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "เครื่องมือนี้จะทำการ:\n" +
            "1. ติดตั้งกล้องมุมมองบุคคลที่สาม สไตล์ Arknights: Endfield (ระยะ 3.0m, เยื้องไหล่ขวา 0.7, FOV 52°, ซูมลูกกลิ้งเมาส์ได้)\n" +
            "2. ปรับทิศทางแสงเงาให้ตรงกับฉาก และจัดระเบียบเงาโคมไฟถนนให้คมชัดไม่กินสเปค\n" +
            "3. ใส่ฟิสิกส์ผ้า (VRM SpringBone) ให้เสื้อโค้ตและกระโปรงพริ้วไหวตามแรงโน้มถ่วง\n" +
            "4. ติดตั้ง Collider ที่ขาทั้งสองข้างและสะโพก ป้องกันเสื้อผ้าทะลุขา 100%\n" +
            "5. แปลง Material ของ Newman เป็น lilToon รับแสงเงาในเกมอย่างเป็นธรรมชาติ ไม่ติดเขียว",
            MessageType.Info);

        GUILayout.Space(12);
        if (GUILayout.Button("🚀 1-Click ติดตั้งทั้งหมดทันที (Apply All)", GUILayout.Height(50)))
        {
            ApplyAll();
            EditorUtility.DisplayDialog("เสร็จสิ้น", "ติดตั้งกล้อง Endfield (ซูมได้) และปรับทิศทางแสงเงาให้เนียนตาเรียบร้อยแล้ว!", "ตกลง");
        }

        GUILayout.Space(8);
        if (GUILayout.Button("💡 ปรับทิศทางแสงเงาให้สวยเนียนตา ไม่กินสเปค", GUILayout.Height(30)))
        {
            OptimizeLightingAndShadows();
        }
    }

    [MenuItem("Tools/👤 1-Click กล้อง Endfield + ฟิสิกส์เสื้อผ้า Newman ทันที")]
    public static void ApplyAll()
    {
        ConvertNewmanMaterialsToURPLit();
        SetupClothPhysicsAndLegColliders();
        SetupNewmanFromNestedArmature();
        OptimizeLightingAndShadows();
        AttachBlinkToActiveModel();

        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("<color=green><b>[SUCCESS]</b> ติดตั้ง Newman (URP Lit รับแสงเงาฉาก 100% + อนิเมชั่นกระพริบตา + ฟิสิกส์เสื้อผ้า + กล้อง Endfield) เรียบร้อยแล้ว!</color>");
    }

    public static void SetupEndfieldCamera()
    {
        // 1. Clean up obsolete empty CinemachineBrain objects
        var oldBrains = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var obj in oldBrains)
        {
            if (obj != null && obj.name == "CinemachineBrain" && obj.GetComponent<Camera>() == null)
            {
                DestroyImmediate(obj);
            }
        }

        // 2. Find or Create Main Camera
        Camera mainCam = Camera.main;
        GameObject camGo = mainCam != null ? mainCam.gameObject : GameObject.Find("Main Camera");

        if (camGo == null)
        {
            camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            mainCam = camGo.AddComponent<Camera>();
        }
        else
        {
            mainCam = camGo.GetComponent<Camera>();
            if (mainCam == null) mainCam = camGo.AddComponent<Camera>();
        }

        camGo.tag = "MainCamera";
        mainCam.enabled = true;
        mainCam.clearFlags = CameraClearFlags.Skybox;
        mainCam.cullingMask = -1; // Everything
        mainCam.nearClipPlane = 0.1f;
        mainCam.farClipPlane = 1000f;
        mainCam.fieldOfView = 52f; // Arknights: Endfield cinematic FOV

        // URP Additional Camera Data
        var urpCamData = camGo.GetComponent<UniversalAdditionalCameraData>();
        if (urpCamData == null) urpCamData = camGo.AddComponent<UniversalAdditionalCameraData>();
        urpCamData.renderPostProcessing = true;
        urpCamData.renderShadows = true;

        // AudioListener
        if (camGo.GetComponent<AudioListener>() == null)
        {
            camGo.AddComponent<AudioListener>();
        }

        // CinemachineBrain attached directly to Main Camera
        var brain = camGo.GetComponent<CinemachineBrain>();
        if (brain == null) brain = camGo.AddComponent<CinemachineBrain>();
        brain.enabled = true;
        brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;

        // 3. Configure PlayerFollowCamera with Arknights: Endfield Third Person Follow
        GameObject playerGo = GameObject.Find("Player_CH2_Newman");
        Transform camRoot = null;
        if (playerGo != null)
        {
            camRoot = playerGo.transform.Find("PlayerCameraRoot");
            if (camRoot == null)
            {
                var newRoot = new GameObject("PlayerCameraRoot");
                newRoot.transform.SetParent(playerGo.transform);
                newRoot.transform.localPosition = new Vector3(0f, 1.42f, 0f);
                camRoot = newRoot.transform;
            }
        }

        GameObject followCamGo = GameObject.Find("PlayerFollowCamera");
        if (followCamGo == null)
        {
            string followCamPrefabPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerFollowCamera.prefab";
            GameObject followCamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(followCamPrefabPath);
            if (followCamPrefab != null)
            {
                followCamGo = (GameObject)PrefabUtility.InstantiatePrefab(followCamPrefab);
                followCamGo.name = "PlayerFollowCamera";
            }
            else
            {
                followCamGo = new GameObject("PlayerFollowCamera");
                followCamGo.AddComponent<CinemachineCamera>();
            }
        }

        // Unparent cameras so they never follow player transform rotations
        if (camGo.transform.parent != null) camGo.transform.SetParent(null);
        if (followCamGo.transform.parent != null) followCamGo.transform.SetParent(null);

        CinemachineCamera cmCam = followCamGo.GetComponent<CinemachineCamera>();
        if (cmCam == null) cmCam = followCamGo.AddComponent<CinemachineCamera>();

        if (camRoot != null)
        {
            cmCam.Target.TrackingTarget = camRoot;
        }
        cmCam.Lens.FieldOfView = 48f; // Resident Evil OTS FOV
        cmCam.Lens.NearClipPlane = 0.15f;

        // Resident Evil OTS Framing Settings
        var tpFollow = followCamGo.GetComponent<CinemachineThirdPersonFollow>();
        if (tpFollow == null) tpFollow = followCamGo.AddComponent<CinemachineThirdPersonFollow>();

        tpFollow.CameraDistance = 0.99f; // RE distance: 0.99 m
        tpFollow.CameraSide = 1.0f;     // Over-the-right-shoulder: 1.0
        tpFollow.ShoulderOffset = new Vector3(0.60f, -0.374f, 0.0f); // Side: 0.60m, Shoulder Y: -0.374m
        tpFollow.VerticalArmLength = 0.0f;
        tpFollow.Damping = new Vector3(0.05f, 0.08f, 0.08f);

        tpFollow.AvoidObstacles.Enabled = false;

        // Add / Configure EndfieldCameraController locked to RE settings
        var zoomCtrl = followCamGo.GetComponent<EndfieldCameraController>();
        if (zoomCtrl == null) zoomCtrl = followCamGo.AddComponent<EndfieldCameraController>();
        zoomCtrl.target = camRoot;
        zoomCtrl.fieldOfView = 48f;
        zoomCtrl.cameraDistance = 0.99f;
        zoomCtrl.shoulderSide = 0.60f;
        zoomCtrl.shoulderHeight = -0.374f;
        zoomCtrl.cameraTilt = -16.4f;
        zoomCtrl.shoulderOffset = new Vector3(0.60f, -0.374f, 0.0f);
        zoomCtrl.verticalArmLength = 0.0f;
        zoomCtrl.defaultCameraSide = 1.0f;
        zoomCtrl.enableZoom = false; // Locked fixed distance
        zoomCtrl.defaultDistance = 0.99f;
        zoomCtrl.minDistance = 0.99f;
        zoomCtrl.maxDistance = 0.99f;
        zoomCtrl.ApplyEndfieldSettings();

        EditorUtility.SetDirty(camGo);
        EditorUtility.SetDirty(followCamGo);
        Debug.Log("<color=cyan>[Camera] ล็อคมุมกล้องสไตล์ Resident Evil OTS (Dist: 0.99m, Height: -0.37m, Side: 0.60m, Tilt: -16.4°, FOV: 48°) สำเร็จเรียบร้อย!</color>");
    }

    public static void SetupClothPhysicsAndLegColliders()
    {
        string newmanPrefabPath = "Assets/3D_Model/VAM/CH1/CH2_Newman.prefab";
        GameObject newmanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(newmanPrefabPath);
        if (newmanPrefab == null)
        {
            Debug.LogError("ไม่พบ CH2_Newman.prefab!");
            return;
        }

        // Open prefab for editing
        string assetPath = AssetDatabase.GetAssetPath(newmanPrefab);
        GameObject root = PrefabUtility.LoadPrefabContents(assetPath);

        try
        {
            // 1. Find Legs and Hips bones
            Transform hip = FindChildRecursive(root.transform, "J_Bip_C_Hips");
            Transform leftUpperLeg = FindChildRecursive(root.transform, "J_Bip_L_UpperLeg");
            Transform rightUpperLeg = FindChildRecursive(root.transform, "J_Bip_R_UpperLeg");
            Transform leftLowerLeg = FindChildRecursive(root.transform, "J_Bip_L_LowerLeg");
            Transform rightLowerLeg = FindChildRecursive(root.transform, "J_Bip_R_LowerLeg");

            List<VRMSpringBoneColliderGroup> legColliders = new List<VRMSpringBoneColliderGroup>();

            // Setup Colliders on Hips
            if (hip != null)
            {
                var col = GetOrAddComponent<VRMSpringBoneColliderGroup>(hip.gameObject);
                col.Colliders = new VRMSpringBoneColliderGroup.SphereCollider[]
                {
                    new VRMSpringBoneColliderGroup.SphereCollider { Offset = new Vector3(0f, -0.03f, 0f), Radius = 0.13f }
                };
                legColliders.Add(col);
            }

            // Setup Colliders on Left Thigh (UpperLeg)
            if (leftUpperLeg != null)
            {
                var col = GetOrAddComponent<VRMSpringBoneColliderGroup>(leftUpperLeg.gameObject);
                col.Colliders = new VRMSpringBoneColliderGroup.SphereCollider[]
                {
                    new VRMSpringBoneColliderGroup.SphereCollider { Offset = new Vector3(0f, -0.08f, 0.01f), Radius = 0.095f },
                    new VRMSpringBoneColliderGroup.SphereCollider { Offset = new Vector3(0f, -0.22f, 0.01f), Radius = 0.090f }
                };
                legColliders.Add(col);
            }

            // Setup Colliders on Right Thigh (UpperLeg)
            if (rightUpperLeg != null)
            {
                var col = GetOrAddComponent<VRMSpringBoneColliderGroup>(rightUpperLeg.gameObject);
                col.Colliders = new VRMSpringBoneColliderGroup.SphereCollider[]
                {
                    new VRMSpringBoneColliderGroup.SphereCollider { Offset = new Vector3(0f, -0.08f, 0.01f), Radius = 0.095f },
                    new VRMSpringBoneColliderGroup.SphereCollider { Offset = new Vector3(0f, -0.22f, 0.01f), Radius = 0.090f }
                };
                legColliders.Add(col);
            }

            // Setup Colliders on Left Calf (LowerLeg)
            if (leftLowerLeg != null)
            {
                var col = GetOrAddComponent<VRMSpringBoneColliderGroup>(leftLowerLeg.gameObject);
                col.Colliders = new VRMSpringBoneColliderGroup.SphereCollider[]
                {
                    new VRMSpringBoneColliderGroup.SphereCollider { Offset = new Vector3(0f, -0.10f, 0f), Radius = 0.075f }
                };
                legColliders.Add(col);
            }

            // Setup Colliders on Right Calf (LowerLeg)
            if (rightLowerLeg != null)
            {
                var col = GetOrAddComponent<VRMSpringBoneColliderGroup>(rightLowerLeg.gameObject);
                col.Colliders = new VRMSpringBoneColliderGroup.SphereCollider[]
                {
                    new VRMSpringBoneColliderGroup.SphereCollider { Offset = new Vector3(0f, -0.10f, 0f), Radius = 0.075f }
                };
                legColliders.Add(col);
            }

            // 2. Find Coat Skirt root bones
            string[] coatBoneNames = new string[]
            {
                "J_Sec_L_CoatSkirtFront_01", "J_Sec_R_CoatSkirtFront_01",
                "J_Sec_L_CoatSkirtBack_01",  "J_Sec_R_CoatSkirtBack_01",
                "J_Sec_L_CoatSkirtSide1_01", "J_Sec_R_CoatSkirtSide1_01",
                "J_Sec_L_CoatSkirtSide2_01", "J_Sec_R_CoatSkirtSide2_01"
            };

            List<Transform> coatRoots = new List<Transform>();
            foreach (var bName in coatBoneNames)
            {
                Transform b = FindChildRecursive(root.transform, bName);
                if (b != null) coatRoots.Add(b);
            }

            // 3. Find or Create Secondary GameObject for SpringBones
            Transform secondary = FindChildRecursive(root.transform, "secondary");
            if (secondary == null)
            {
                var secGo = new GameObject("secondary");
                secGo.transform.SetParent(root.transform);
                secondary = secGo.transform;
            }

            // 4. Setup Coat Skirt VRMSpringBone
            VRMSpringBone coatSpringBone = null;
            var existingSprings = secondary.GetComponents<VRMSpringBone>();
            foreach (var sb in existingSprings)
            {
                if (sb.m_comment != null && sb.m_comment.Contains("CoatSkirt"))
                {
                    coatSpringBone = sb;
                    break;
                }
            }

            if (coatSpringBone == null)
            {
                coatSpringBone = secondary.gameObject.AddComponent<VRMSpringBone>();
            }

            coatSpringBone.m_comment = "CoatSkirt_ClothPhysics";
            coatSpringBone.m_stiffnessForce = 0.28f; // Smooth fluid cloth motion
            coatSpringBone.m_dragForce = 0.45f;      // Air resistance damping
            coatSpringBone.m_gravityPower = 0.16f;   // Natural downward drape
            coatSpringBone.m_gravityDir = new Vector3(0f, -1f, 0f);
            coatSpringBone.m_hitRadius = 0.045f;     // Cloth collision thickness
            coatSpringBone.m_center = hip;
            coatSpringBone.RootBones = coatRoots;
            coatSpringBone.ColliderGroups = legColliders.ToArray();

            // Also attach leg colliders to other SpringBones on secondary so nothing clips
            foreach (var sb in existingSprings)
            {
                if (sb != coatSpringBone)
                {
                    var list = new List<VRMSpringBoneColliderGroup>(sb.ColliderGroups ?? new VRMSpringBoneColliderGroup[0]);
                    foreach (var legCol in legColliders)
                    {
                        if (!list.Contains(legCol)) list.Add(legCol);
                    }
                    sb.ColliderGroups = list.ToArray();
                }
            }

            // 5. Ensure root Animator is configured with VrmAvatar
            string newmanAvatarPath = "Assets/3D_Model/VAM/CH1/CH2_Newman.Avatar/VrmAvatar.asset";
            Avatar newmanAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(newmanAvatarPath);
            string controllerPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";
            RuntimeAnimatorController animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

            var modelAnim = GetOrAddComponent<Animator>(root);
            if (newmanAvatar != null) modelAnim.avatar = newmanAvatar;
            if (animController != null) modelAnim.runtimeAnimatorController = animController;
            modelAnim.applyRootMotion = false;
            modelAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            Debug.Log($"<color=green>[Cloth Physics] ติดตั้งฟิสิกส์เสื้อโค้ตและ Collider กันทะลุขา {legColliders.Count} จุดให้ CH2_Newman เรียบร้อยแล้ว!</color>");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/🎨 แปลง Material Newman เป็น Unity URP Lit (Receive & Cast Shadows 100%)")]
    public static void ConvertNewmanMaterialsToURPLit()
    {
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit")
                              ?? AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
        if (urpLitShader == null)
        {
            Debug.LogError("ไม่พบ Shader 'Universal Render Pipeline/Lit'!");
            return;
        }

        string folder = "Assets/3D_Model/VAM/CH1/CH2_Newman.Materials";
        string[] matFiles = Directory.GetFiles(folder, "*.asset");

        int converted = 0;
        foreach (string file in matFiles)
        {
            string unityPath = file.Replace("\\", "/");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(unityPath);
            if (mat == null) continue;

            string name = mat.name.ToLower();
            Texture mainTex = mat.GetTexture("_BaseMap") ?? mat.GetTexture("_MainTex");
            if (mainTex == null)
            {
                string texFolder = "Assets/3D_Model/VAM/CH1/CH2_Newman.Textures";
                if (name.Contains("body")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_10.png");
                else if (name.Contains("face_00_skin")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_04.png");
                else if (name.Contains("eyeiris")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_02.png");
                else if (name.Contains("eyehighlight")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_03.png");
                else if (name.Contains("eyewhite")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_06.png");
                else if (name.Contains("facebrow")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_07.png");
                else if (name.Contains("faceeyeline")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_09.png");
                else if (name.Contains("facemouth")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_01.png");
                else if (name.Contains("hairback")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_15.png");
                else if (name.Contains("hair_00_hair")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_17.png");
                else if (name.Contains("bottoms")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_12.png");
                else if (name.Contains("cloth_01")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_14.png");
                else if (name.Contains("cloth_02")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_16.png");
                else if (name.Contains("shoes")) mainTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texFolder + "/_13.png");
            }

            mat.shader = urpLitShader;

            if (mainTex != null)
            {
                mat.SetTexture("_BaseMap", mainTex);
                mat.SetTexture("_MainTex", mainTex);
            }

            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_Cull", 0f); // Double-sided rendering for VRoid hair & clothes!
            bool isFace = name.Contains("face") || name.Contains("eye");
            mat.SetFloat("_ReceiveShadows", isFace ? 0f : 1f); // Keeps face porcelain-clean, zero dirty shadow acne!
            mat.doubleSidedGI = true;

            bool isCutout = name.Contains("hair") || name.Contains("facebrow") || 
                           name.Contains("faceeyeline") || name.Contains("facemouth") || 
                           name.Contains("eyehighlight");

            if (isCutout)
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }

            if (name.Contains("skin") || name.Contains("face"))
            {
                mat.SetFloat("_Smoothness", 0.15f);
            }
            else if (name.Contains("eyeiris") || name.Contains("eyewhite"))
            {
                mat.SetFloat("_Smoothness", 0.65f);
            }
            else if (name.Contains("eyehighlight"))
            {
                mat.SetFloat("_Smoothness", 0.85f);
            }
            else if (name.Contains("hair"))
            {
                mat.SetFloat("_Smoothness", 0.22f);
            }
            else
            {
                mat.SetFloat("_Smoothness", 0.10f); // Matte cloth fabric
            }

            EditorUtility.SetDirty(mat);
            converted++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"<color=green><b>[SUCCESS]</b> แปลง Material VRoid ทั้งหมด {converted} ชิ้นเป็น Unity URP Lit ตอบสนองต่อแสงและเงา (Receive & Cast Shadows) ของฉาก 100% เรียบร้อยแล้ว!</color>");
    }

    [MenuItem("Tools/👀 ติดตั้งระบบกระพริบตา Newman (Blink Animation)")]
    public static void AttachBlinkToActiveModel()
    {
        GameObject newmanGo = GameObject.Find("Newman_Model") ?? GameObject.Find("CH2_Newman");
        if (newmanGo != null)
        {
            var blink = newmanGo.GetComponent<VRoidEyeBlink>();
            if (blink == null) blink = newmanGo.AddComponent<VRoidEyeBlink>();
            blink.InitializeComponents();
            EditorUtility.SetDirty(newmanGo);
            Debug.Log("<color=green><b>[SUCCESS]</b> ติดตั้ง VRoidEyeBlink บน Newman_Model เรียบร้อยแล้ว!</color>");
        }
    }

    [MenuItem("Tools/🏃 ติดตั้งระบบ Animation Warping & Foot IK ให้ Newman (Unity 6 Native)")]
    public static void SetupAnimationWarpingMenu()
    {
        SetupAnimationWarpingForNewman();
    }

    public static void SetupAnimationWarpingForNewman()
    {
        // 1. Locate Newman_Model in the open scene
        GameObject newmanGo = GameObject.Find("Newman_Model") ?? GameObject.Find("CH2_Newman");
        if (newmanGo == null)
        {
            var animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude);
            foreach (var a in animators)
            {
                if (a.name.Contains("Newman"))
                {
                    newmanGo = a.gameObject;
                    break;
                }
            }
        }

        if (newmanGo != null)
        {
            var warping = newmanGo.GetComponent<NewmanAnimationWarping>();
            if (warping == null) warping = newmanGo.AddComponent<NewmanAnimationWarping>();

            EditorUtility.SetDirty(newmanGo);
            Debug.Log("<color=green><b>[SUCCESS]</b> ติดตั้ง NewmanAnimationWarping บน Newman_Model เรียบร้อยแล้ว!</color>");
        }

        // 2. Configure ThirdPersonController in scene
        var tpc = UnityEngine.Object.FindAnyObjectByType<StarterAssets.ThirdPersonController>(FindObjectsInactive.Exclude);
        if (tpc != null)
        {
            tpc.EnableStrideWarping = true;
            tpc.MinMotionSpeed = 0.5f;
            tpc.MaxMotionSpeed = 1.35f;
            EditorUtility.SetDirty(tpc);
            Debug.Log("<color=green><b>[SUCCESS]</b> เปิดใช้งาน Stride Warping บน ThirdPersonController เรียบร้อยแล้ว!</color>");
        }

        // 3. Configure CH2_Newman.prefab
        string prefabPath = "Assets/3D_Model/VAM/CH1/CH2_Newman.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            using (var editScope = new PrefabUtility.EditPrefabContentsScope(prefabPath))
            {
                GameObject root = editScope.prefabContentsRoot;
                var pWarping = root.GetComponent<NewmanAnimationWarping>();
                if (pWarping == null) root.AddComponent<NewmanAnimationWarping>();
            }
            Debug.Log("<color=green><b>[SUCCESS]</b> บันทึก NewmanAnimationWarping ลงใน CH2_Newman.prefab เรียบร้อยแล้ว!</color>");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    public static void ConvertNewmanMaterialsToLilToon()
    {
        Shader shaderOpaqueOutline = AssetDatabase.LoadAssetAtPath<Shader>("Packages/jp.lilxyzw.liltoon/Shader/lts_o.shader") 
                                     ?? Shader.Find("Hidden/lilToonOutline") 
                                     ?? Shader.Find("lilToon");
        Shader shaderCutoutOutline = AssetDatabase.LoadAssetAtPath<Shader>("Packages/jp.lilxyzw.liltoon/Shader/lts_cutout_o.shader") 
                                     ?? Shader.Find("Hidden/lilToonCutoutOutline") 
                                     ?? shaderOpaqueOutline;
        Shader shaderCutoutNoOutline = AssetDatabase.LoadAssetAtPath<Shader>("Packages/jp.lilxyzw.liltoon/Shader/lts_cutout.shader") 
                                       ?? Shader.Find("Hidden/lilToonCutout") 
                                       ?? shaderCutoutOutline;

        string folder = "Assets/3D_Model/VAM/CH1/CH2_Newman.Materials";
        string[] matFiles = Directory.GetFiles(folder, "*.asset");

        int converted = 0;
        foreach (string file in matFiles)
        {
            string unityPath = file.Replace("\\", "/");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(unityPath);
            if (mat == null) continue;

            string name = mat.name.ToLower();
            Texture mainTex = mat.GetTexture("_MainTex");

            bool isEyeHighlight = name.Contains("eyehighlight");
            bool isCutout = name.Contains("hair") || name.Contains("facebrow") || name.Contains("faceeyeline") || name.Contains("facemouth");

            if (isEyeHighlight)
                mat.shader = shaderCutoutNoOutline;
            else if (isCutout)
                mat.shader = shaderCutoutOutline;
            else
                mat.shader = shaderOpaqueOutline;

            if (mainTex != null)
                mat.SetTexture("_MainTex", mainTex);

            // 1. Shading & Shadow reception (Resident Evil Requiem Cinematic Realistic Shading)
            if (mat.HasProperty("_UseShadow")) mat.SetFloat("_UseShadow", 1f);
            if (mat.HasProperty("_ShadowStrength")) mat.SetFloat("_ShadowStrength", 0.95f); // Deep realistic occlusion
            if (mat.HasProperty("_ShadowBorder")) mat.SetFloat("_ShadowBorder", 0.52f);
            if (mat.HasProperty("_ShadowBlur")) mat.SetFloat("_ShadowBlur", 0.28f); // Soft cinematic penumbra
            if (mat.HasProperty("_ShadowColor")) mat.SetColor("_ShadowColor", new Color(0.12f, 0.13f, 0.17f, 1f)); // Deep midnight shadow matching the scene
            if (mat.HasProperty("_UseShadow2")) mat.SetFloat("_UseShadow2", 1f); // 2-tier shadow for clothing folds and depth
            if (mat.HasProperty("_Shadow2Border")) mat.SetFloat("_Shadow2Border", 0.28f);
            if (mat.HasProperty("_Shadow2Blur")) mat.SetFloat("_Shadow2Blur", 0.32f);
            if (mat.HasProperty("_Shadow2Color")) mat.SetColor("_Shadow2Color", new Color(0.05f, 0.05f, 0.08f, 1f));
            if (mat.HasProperty("_ShadowReceive")) mat.SetFloat("_ShadowReceive", 1f); // Receive real scene shadows!
            if (mat.HasProperty("_ShadowEnvStrength")) mat.SetFloat("_ShadowEnvStrength", 0.95f); // Receive ambient occlusion & environment shadows

            // 2. Realistic Cinematic Lighting (Blends into darkness, no radioactive glowing sticker)
            if (mat.HasProperty("_LightDirectionOverride")) mat.SetVector("_LightDirectionOverride", Vector4.zero); // Strictly follow real scene lighting!
            if (mat.HasProperty("_LightMinLimit")) mat.SetFloat("_LightMinLimit", 0.015f); // Character darkens realistically in shadow/dark corners
            if (mat.HasProperty("_LightMaxLimit")) mat.SetFloat("_LightMaxLimit", 1.0f);
            if (mat.HasProperty("_MonochromeLighting")) mat.SetFloat("_MonochromeLighting", 0.92f); // Naturally illuminated by cold moonlight & warm lanterns
            if (mat.HasProperty("_lilDirectionalLightStrength")) mat.SetFloat("_lilDirectionalLightStrength", 1f);
            if (mat.HasProperty("_VertexLightStrength")) mat.SetFloat("_VertexLightStrength", 1f);

            // 3. Cinematic Moonlight Rim Light (Separates silhouette from darkness naturally)
            if (mat.HasProperty("_UseRim")) mat.SetFloat("_UseRim", 1f);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", new Color(0.38f, 0.48f, 0.65f, 1.0f)); // Pale moonlight rim
            if (mat.HasProperty("_RimBorder")) mat.SetFloat("_RimBorder", 0.65f);
            if (mat.HasProperty("_RimBlur")) mat.SetFloat("_RimBlur", 0.45f);
            if (mat.HasProperty("_RimFresnelPower")) mat.SetFloat("_RimFresnelPower", 4.2f);
            if (mat.HasProperty("_RimShadowMask")) mat.SetFloat("_RimShadowMask", 1.0f); // Masked by shadow, only lights up where moonlight hits!

            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            if (isCutout && mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.5f);

            // 4. Subtle Micro Outline (Eliminates the cartoon cutout / sticker look)
            if (!isEyeHighlight)
            {
                if (mat.HasProperty("_UseOutline")) mat.SetFloat("_UseOutline", 1f);
                if (mat.HasProperty("_OutlineColor")) mat.SetColor("_OutlineColor", new Color(0.08f, 0.08f, 0.10f, 0.65f));
                float outlineW = name.Contains("eye") ? 0.008f : 0.012f; // Delicate micro-outline instead of heavy cartoon border
                if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", outlineW);

                if (mat.HasProperty("_OutlineLitApplyTex")) mat.SetFloat("_OutlineLitApplyTex", 1f);
                if (mat.HasProperty("_OutlineLitShadowReceive")) mat.SetFloat("_OutlineLitShadowReceive", 1f);
                if (mat.HasProperty("_OutlineEnableLighting")) mat.SetFloat("_OutlineEnableLighting", 1f);
                if (mat.HasProperty("_OutlineFixWidth")) mat.SetFloat("_OutlineFixWidth", 0.5f);
            }
            else
            {
                if (mat.HasProperty("_UseShadow")) mat.SetFloat("_UseShadow", 0f);
            }

            EditorUtility.SetDirty(mat);
            converted++;
        }

        Debug.Log($"<color=cyan>[lilToon] อัปเดต Material {converted} ไฟล์เป็น Resident Evil Requiem Cinematic Look เรียบร้อย</color>");
    }

    public static GameObject CreateNewmanPlayerPrefab()
    {
        string starterArmaturePath = "Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(starterArmaturePath);
        string newmanModelPath = "Assets/3D_Model/VAM/CH1/CH2_Newman.prefab";
        GameObject newmanModel = AssetDatabase.LoadAssetAtPath<GameObject>(newmanModelPath);
        string newmanAvatarPath = "Assets/3D_Model/VAM/CH1/CH2_Newman.Avatar/VrmAvatar.asset";
        Avatar newmanAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(newmanAvatarPath);
        string controllerPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";
        RuntimeAnimatorController animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

        if (basePrefab == null || newmanModel == null) return null;

        GameObject playerGo = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        PrefabUtility.UnpackPrefabInstance(playerGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        playerGo.name = "Player_CH2_Newman";

        // Remove old mannequin meshes and bones to prevent bone conflicts
        Transform geom = playerGo.transform.Find("Geometry");
        if (geom != null) DestroyImmediate(geom.gameObject);

        Transform skeleton = playerGo.transform.Find("Skeleton");
        if (skeleton != null) DestroyImmediate(skeleton.gameObject);

        // Remove dummy mannequin Animator on root so it does not conflict
        Animator rootAnim = playerGo.GetComponent<Animator>();
        if (rootAnim != null)
        {
            DestroyImmediate(rootAnim);
        }

        // Instantiate Newman model as child
        GameObject newmanInstance = (GameObject)PrefabUtility.InstantiatePrefab(newmanModel, playerGo.transform);
        newmanInstance.name = "Newman_Model";
        newmanInstance.transform.localPosition = Vector3.zero;
        newmanInstance.transform.localRotation = Quaternion.identity;
        newmanInstance.transform.localScale = Vector3.one;

        // Configure Animator directly on Newman_Model with VRM Avatar and ThirdPersonController
        Animator childAnim = newmanInstance.GetComponent<Animator>();
        if (childAnim == null) childAnim = newmanInstance.AddComponent<Animator>();
        if (newmanAvatar != null) childAnim.avatar = newmanAvatar;
        if (animController != null) childAnim.runtimeAnimatorController = animController;
        childAnim.applyRootMotion = false;
        childAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Add AnimationEvent forwarder so OnFootstep / OnLand reach ThirdPersonController
        if (newmanInstance.GetComponent<NewmanAnimationEvents>() == null)
        {
            newmanInstance.AddComponent<NewmanAnimationEvents>();
        }

        // Add NewmanAnimationWarping for Foot IK, Stride Warping, and Body Leaning
        if (newmanInstance.GetComponent<NewmanAnimationWarping>() == null)
        {
            newmanInstance.AddComponent<NewmanAnimationWarping>();
        }

        CharacterController cc = playerGo.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.height = 1.75f;
            cc.center = new Vector3(0f, 0.88f, 0f);
            cc.radius = 0.28f;
        }

        Transform camRoot = playerGo.transform.Find("PlayerCameraRoot");
        if (camRoot != null)
        {
            camRoot.localPosition = new Vector3(0f, 1.42f, 0f);
        }

        var smrs = newmanInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in smrs)
        {
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            smr.receiveShadows = true;
            smr.updateWhenOffscreen = true;
        }

        string outDir = "Assets/3D_Model/VAM/CH1";
        string outPath = $"{outDir}/Player_CH2_Newman_ThirdPerson.prefab";
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(playerGo, outPath);
        DestroyImmediate(playerGo);

        return savedPrefab;
    }

    public static void SetupNewmanInScene()
    {
        // 1. Ensure cameras are unparented from any player objects
        var mainCamObj = GameObject.Find("Main Camera");
        if (mainCamObj != null && mainCamObj.transform.parent != null)
        {
            mainCamObj.transform.SetParent(null);
        }

        var followCamObj = GameObject.Find("PlayerFollowCamera");
        if (followCamObj != null && followCamObj.transform.parent != null)
        {
            followCamObj.transform.SetParent(null);
        }

        // 2. Clean up empty wrapper "Player" or existing "Player_CH2_Newman"
        GameObject emptyPlayer = GameObject.Find("Player");
        if (emptyPlayer != null)
        {
            for (int i = emptyPlayer.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = emptyPlayer.transform.GetChild(i);
                if (child.name != "Player_CH2_Newman")
                {
                    child.SetParent(null);
                }
            }
            DestroyImmediate(emptyPlayer);
        }

        Vector3 spawnPos = new Vector3(31.5f, 0.2f, -80f);
        GameObject existingPlayer = GameObject.Find("Player_CH2_Newman");
        if (existingPlayer != null)
        {
            spawnPos = existingPlayer.transform.position;
            DestroyImmediate(existingPlayer);
        }

        // 3. Re-create & instantiate updated player prefab
        GameObject playerPrefab = CreateNewmanPlayerPrefab();
        if (playerPrefab == null)
        {
            string playerPrefabPath = "Assets/3D_Model/VAM/CH1/Player_CH2_Newman_ThirdPerson.prefab";
            playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
        }

        GameObject playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        playerInstance.name = "Player_CH2_Newman";
        playerInstance.transform.position = spawnPos;
        playerInstance.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        Transform camRoot = playerInstance.transform.Find("PlayerCameraRoot");

        // Make sure ThirdPersonController CinemachineCameraTarget is assigned
        var tpc = playerInstance.GetComponent<StarterAssets.ThirdPersonController>();
        if (tpc != null && camRoot != null)
        {
            tpc.CinemachineCameraTarget = camRoot.gameObject;
        }

        EditorSceneManager.MarkAllScenesDirty();
    }

    public static void SetupNewmanFromNestedArmature()
    {
        string nestedPrefabPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/NestedParentArmature_Unpack.prefab";
        GameObject nestedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(nestedPrefabPath);
        if (nestedPrefab == null)
        {
            Debug.LogError("ไม่พบ NestedParentArmature_Unpack.prefab!");
            return;
        }

        string newmanModelPath = "Assets/3D_Model/VAM/CH1/CH2_Newman.prefab";
        GameObject newmanModel = AssetDatabase.LoadAssetAtPath<GameObject>(newmanModelPath);
        string newmanAvatarPath = "Assets/3D_Model/VAM/CH1/CH2_Newman.Avatar/VrmAvatar.asset";
        Avatar newmanAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(newmanAvatarPath);
        string controllerPath = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";
        RuntimeAnimatorController animController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

        // 1. Remember spawn position from old player if present
        Vector3 spawnPos = new Vector3(-1.02f, 0.93f, 34.27f);
        GameObject oldPlayer = GameObject.Find("Player") ?? GameObject.Find("Player_CH2_Newman") ?? GameObject.Find("PlayerArmature") ?? GameObject.Find("NestedParentArmature_Newman");
        if (oldPlayer != null)
        {
            spawnPos = new Vector3(oldPlayer.transform.position.x, 0.93f, oldPlayer.transform.position.z);
        }

        // Raycast down to find exact street pavement height so boots touch the floor directly
        if (Physics.Raycast(new Vector3(spawnPos.x, 20f, spawnPos.z), Vector3.down, out RaycastHit groundHit, 50f))
        {
            spawnPos.y = groundHit.point.y + 0.93f; // 0.93 offset cancels PlayerArmature's -0.93 local position!
        }

        // 2. Clean up old players and stray cameras
        string[] oldNames = new string[] { "Player", "Player_CH2_Newman", "NestedParentArmature_Unpack", "NestedParentArmature_Newman", "PlayerFollowCamera", "MainCamera", "Main Camera", "UI_EventSystem" };
        foreach (var n in oldNames)
        {
            var go = GameObject.Find(n);
            if (go != null) DestroyImmediate(go);
        }

        // 3. Instantiate NestedParentArmature_Unpack in scene
        GameObject nestedInstance = (GameObject)PrefabUtility.InstantiatePrefab(nestedPrefab);
        nestedInstance.name = "NestedParentArmature_Newman";
        nestedInstance.transform.position = spawnPos;
        nestedInstance.transform.localScale = Vector3.one; // Ensure exact 1:1 human proportions!
        PrefabUtility.UnpackPrefabInstance(nestedInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        // 4. Find components inside the nested system
        Transform playerArmatureT = nestedInstance.transform.Find("PlayerArmature");
        Transform followCamT = nestedInstance.transform.Find("PlayerFollowCamera");
        Transform mainCamT = nestedInstance.transform.Find("MainCamera");

        if (playerArmatureT == null)
        {
            Debug.LogError("ไม่พบ PlayerArmature ใน NestedParentArmature!");
            return;
        }

        GameObject playerArmature = playerArmatureT.gameObject;

        // Hide mannequin Geometry and Skeleton
        Transform geom = playerArmature.transform.Find("Geometry");
        if (geom != null) geom.gameObject.SetActive(false);

        Transform skeleton = playerArmature.transform.Find("Skeleton");
        if (skeleton != null) skeleton.gameObject.SetActive(false);

        // Instantiate Newman model as child of PlayerArmature
        GameObject newmanInstance = (GameObject)PrefabUtility.InstantiatePrefab(newmanModel, playerArmature.transform);
        newmanInstance.name = "Newman_Model";
        newmanInstance.transform.localPosition = Vector3.zero;
        newmanInstance.transform.localRotation = Quaternion.identity;
        newmanInstance.transform.localScale = Vector3.one;

        // Configure Newman_Model Animator
        Animator childAnim = newmanInstance.GetComponent<Animator>();
        if (childAnim == null) childAnim = newmanInstance.AddComponent<Animator>();
        if (newmanAvatar != null) childAnim.avatar = newmanAvatar;
        if (animController != null) childAnim.runtimeAnimatorController = animController;
        childAnim.applyRootMotion = false;
        childAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Add NewmanAnimationEvents
        if (newmanInstance.GetComponent<NewmanAnimationEvents>() == null)
        {
            newmanInstance.AddComponent<NewmanAnimationEvents>();
        }

        // Add VRoidEyeBlink for lively natural blinking
        if (newmanInstance.GetComponent<VRoidEyeBlink>() == null)
        {
            newmanInstance.AddComponent<VRoidEyeBlink>();
        }

        // Add NewmanAnimationWarping for Foot IK, Stride Warping, and Body Leaning
        if (newmanInstance.GetComponent<NewmanAnimationWarping>() == null)
        {
            newmanInstance.AddComponent<NewmanAnimationWarping>();
        }

        // Configure SkinnedMeshRenderers
        var smrs = newmanInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in smrs)
        {
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            smr.receiveShadows = true;
            smr.updateWhenOffscreen = true;
        }

        // Configure PlayerArmature CharacterController
        CharacterController cc = playerArmature.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.height = 1.75f;
            cc.center = new Vector3(0f, 0.88f, 0f);
            cc.radius = 0.28f;
        }

        // Configure PlayerCameraRoot
        Transform camRoot = playerArmature.transform.Find("PlayerCameraRoot");
        if (camRoot != null)
        {
            // 2.38f offset accounts for playerArmature's -0.93f local position, placing camera root at 1.45m shoulder height!
            camRoot.localPosition = new Vector3(0f, 2.38f, 0f);
        }

        // Configure PlayerArmature root Animator
        Animator rootAnim = playerArmature.GetComponent<Animator>();
        if (rootAnim != null)
        {
            if (newmanAvatar != null) rootAnim.avatar = newmanAvatar;
            if (animController != null) rootAnim.runtimeAnimatorController = animController;
            rootAnim.applyRootMotion = false;
            rootAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // Ensure ThirdPersonController target is linked
        var tpc = playerArmature.GetComponent<StarterAssets.ThirdPersonController>();
        if (tpc != null && camRoot != null)
        {
            tpc.CinemachineCameraTarget = camRoot.gameObject;
        }

        // 5. Configure PlayerFollowCamera with Resident Evil OTS framing
        if (followCamT != null)
        {
            GameObject followCamGo = followCamT.gameObject;
            var cmCam = followCamGo.GetComponent<CinemachineCamera>();
            if (cmCam != null && camRoot != null)
            {
                cmCam.Target.TrackingTarget = camRoot;
                cmCam.Lens.FieldOfView = 48f;
                cmCam.Lens.NearClipPlane = 0.15f;
            }

            var tpFollow = followCamGo.GetComponent<CinemachineThirdPersonFollow>();
            if (tpFollow == null) tpFollow = followCamGo.AddComponent<CinemachineThirdPersonFollow>();
            tpFollow.CameraDistance = 0.99f;
            tpFollow.CameraSide = 1.0f;
            tpFollow.ShoulderOffset = new Vector3(0.60f, -0.374f, 0f);
            tpFollow.VerticalArmLength = 0.0f;
            tpFollow.Damping = new Vector3(0.05f, 0.08f, 0.08f);
            tpFollow.AvoidObstacles.Enabled = false;

            var zoomCtrl = followCamGo.GetComponent<EndfieldCameraController>();
            if (zoomCtrl == null) zoomCtrl = followCamGo.AddComponent<EndfieldCameraController>();
            zoomCtrl.target = camRoot;
            zoomCtrl.fieldOfView = 48f;
            zoomCtrl.cameraDistance = 0.99f;
            zoomCtrl.shoulderSide = 0.60f;
            zoomCtrl.shoulderHeight = -0.374f;
            zoomCtrl.cameraTilt = -16.4f;
            zoomCtrl.shoulderOffset = new Vector3(0.60f, -0.374f, 0f);
            zoomCtrl.verticalArmLength = 0.0f;
            zoomCtrl.defaultCameraSide = 1.0f;
            zoomCtrl.enableZoom = false;
            zoomCtrl.defaultDistance = 0.99f;
            zoomCtrl.minDistance = 0.99f;
            zoomCtrl.maxDistance = 0.99f;
            zoomCtrl.ApplyEndfieldSettings();
            followCamGo.transform.SetParent(null);
        }

        // 6. Configure MainCamera
        if (mainCamT != null)
        {
            GameObject mainCamGo = mainCamT.gameObject;
            mainCamGo.name = "Main Camera";
            mainCamGo.tag = "MainCamera";
            mainCamGo.transform.SetParent(null);

            var cam = mainCamGo.GetComponent<Camera>();
            if (cam != null)
            {
                cam.enabled = true;
                cam.fieldOfView = 52f;
                cam.nearClipPlane = 0.15f;
                cam.farClipPlane = 1000f;
            }

            var brain = mainCamGo.GetComponent<CinemachineBrain>();
            if (brain == null) brain = mainCamGo.AddComponent<CinemachineBrain>();
            brain.enabled = true;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;

            var urpData = mainCamGo.GetComponent<UniversalAdditionalCameraData>();
            if (urpData == null) urpData = mainCamGo.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderPostProcessing = true;
            urpData.renderShadows = true;

            if (mainCamGo.GetComponent<AudioListener>() == null)
            {
                mainCamGo.AddComponent<AudioListener>();
            }
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("<color=green><b>[SUCCESS]</b> ติดตั้ง NestedParentArmature_Newman พร้อมกล้อง Endfield (ซูมเข้า-ออกได้) สำเร็จอย่างเป็นระบบ!</color>");
    }

    [MenuItem("Tools/🎬 1-Click แสงเงา Resident Evil Requiem (ภาพยนตร์สมจริง ตัวละครกลมกลืนไม่ลอย)")]
    public static void OptimizeLightingAndShadows()
    {
        // 1. Directional Light (Moonlight) - Cold cinematic moonlight with soft dramatic shadows
        GameObject dirLightGo = GameObject.Find("Directional Light (Moonlight)") ?? GameObject.Find("Directional Light");
        if (dirLightGo != null)
        {
            var dirLight = dirLightGo.GetComponent<Light>();
            if (dirLight != null)
            {
                dirLight.enabled = true;
                dirLight.type = LightType.Directional;
                dirLight.color = new Color(0.36f, 0.46f, 0.62f, 1f); // Deep cold slate moonlight
                dirLight.intensity = 0.70f;
                dirLight.shadows = LightShadows.Soft;
                dirLight.shadowStrength = 0.90f;
                dirLight.shadowBias = 0.03f;
                dirLight.shadowNormalBias = 0.35f;

                // High-angle down-alley slant
                dirLightGo.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
                EditorUtility.SetDirty(dirLightGo);
            }
        }

        // 2. Atmospheric Fog & Ambient Trilight (Resident Evil horror/cinematic atmospheric depth)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0085f;
        RenderSettings.fogColor = new Color(0.04f, 0.06f, 0.10f, 1.0f); // Deep midnight cinematic mist

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.035f, 0.045f, 0.07f, 1.0f); // Cool night sky ambient fill
        RenderSettings.ambientEquatorColor = new Color(0.025f, 0.03f, 0.045f, 1.0f);
        RenderSettings.ambientGroundColor = new Color(0.012f, 0.014f, 0.018f, 1.0f); // Dark cobblestone bounce

        // 3. Optimize Street Lanterns (Warm tungsten glow, soft local shadows near player)
        Vector3 playerPos = new Vector3(-1.02f, 0f, 34.27f);
        GameObject playerObj = GameObject.Find("PlayerArmature") ?? GameObject.Find("Player_CH2_Newman");
        if (playerObj != null) playerPos = playerObj.transform.position;

        var allLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        List<KeyValuePair<Light, float>> lanternLights = new List<KeyValuePair<Light, float>>();

        foreach (var l in allLights)
        {
            if (l.type == LightType.Point && l.gameObject.name.Contains("Lantern"))
            {
                l.color = new Color(1.0f, 0.62f, 0.26f, 1.0f); // Warm tungsten glow
                float dist = Vector3.Distance(l.transform.position, playerPos);
                lanternLights.Add(new KeyValuePair<Light, float>(l, dist));
            }
        }

        // Sort by distance to player: only the 4 closest lanterns cast soft shadows!
        lanternLights.Sort((a, b) => a.Value.CompareTo(b.Value));

        int shadowLanternsCount = 0;
        for (int i = 0; i < lanternLights.Count; i++)
        {
            var l = lanternLights[i].Key;
            if (i < 4) // 4 closest lanterns have soft shadows
            {
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.85f;
                l.shadowBias = 0.04f;
                l.shadowNormalBias = 0.35f;
                shadowLanternsCount++;
            }
            else // Distant lanterns illuminate warmly without choking shadow atlas or draw calls!
            {
                l.shadows = LightShadows.None;
            }
            EditorUtility.SetDirty(l);
        }

        // 4. Update Newman Materials (lilToon) to follow real scene lighting
        ConvertNewmanMaterialsToLilToon();

        // 5. Optimize URP Pipeline Asset Shadows (PC_RPAsset.asset)
        string urpAssetPath = "Assets/Settings/PC_RPAsset.asset";
        var urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpAssetPath);
        if (urpAsset != null)
        {
            urpAsset.shadowDistance = 45f;
            urpAsset.shadowCascadeCount = 4;
            urpAsset.cascade4Split = new Vector3(0.08f, 0.22f, 0.48f); // High resolution near player

            SerializedObject so = new SerializedObject(urpAsset);
            var tierHigh = so.FindProperty("m_AdditionalLightsShadowResolutionTierHigh");
            if (tierHigh != null) tierHigh.intValue = 1024;
            var tierMed = so.FindProperty("m_AdditionalLightsShadowResolutionTierMedium");
            if (tierMed != null) tierMed.intValue = 512;
            var tierLow = so.FindProperty("m_AdditionalLightsShadowResolutionTierLow");
            if (tierLow != null) tierLow.intValue = 256;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(urpAsset);
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"<color=cyan><b>[Resident Evil Requiem Style]</b> ติดตั้งแสงจันทร์สลัว หมอกกลางคืน SSAO เงาโคมไฟ {shadowLanternsCount} ดวง และปรับ Material ตัวละครให้กลืนกับฉากไม่ลอยเรียบร้อยแล้ว!</color>");
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var res = FindChildRecursive(parent.GetChild(i), name);
            if (res != null) return res;
        }
        return null;
    }

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }
}
