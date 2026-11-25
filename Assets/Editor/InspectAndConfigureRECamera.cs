using UnityEngine;
using UnityEditor;
using Unity.Cinemachine;
using StarterAssets;

public static class InspectAndConfigureRECamera
{
    [MenuItem("Tools/Inspect RE Camera")]
    public static void Inspect()
    {
        var sb = new System.Text.StringBuilder();
        var followCamGo = GameObject.Find("PlayerFollowCamera");
        if (followCamGo != null)
        {
            var cmCam = followCamGo.GetComponent<CinemachineCamera>();
            var tp = followCamGo.GetComponent<CinemachineThirdPersonFollow>();
            var endfield = followCamGo.GetComponent<EndfieldCameraController>();

            sb.AppendLine($"=== PlayerFollowCamera ===");
            sb.AppendLine($"Transform pos={followCamGo.transform.position}, rot={followCamGo.transform.eulerAngles}");

            if (tp != null)
            {
                sb.AppendLine("=== CinemachineThirdPersonFollow Fields & Properties ===");
                var type = tp.GetType();
                foreach (var f in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    sb.AppendLine($"  Field {f.Name} ({f.FieldType.Name}) = {f.GetValue(tp)}");
                }
                foreach (var p in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                {
                    if (p.CanRead)
                    {
                        try { sb.AppendLine($"  Prop {p.Name} ({p.PropertyType.Name}) = {p.GetValue(tp)}"); }
                        catch { }
                    }
                }
            }
        }
        else
        {
            sb.AppendLine("PlayerFollowCamera NOT FOUND!");
        }

        var tpc = Object.FindAnyObjectByType<ThirdPersonController>();
        if (tpc != null)
        {
            sb.AppendLine($"=== ThirdPersonController ===");
            sb.AppendLine($"GameObject: {tpc.gameObject.name}, scale={tpc.transform.lossyScale}");
            sb.AppendLine($"CinemachineCameraTarget: {tpc.CinemachineCameraTarget?.name}");
            if (tpc.CinemachineCameraTarget != null)
            {
                sb.AppendLine($"  Target localPos: {tpc.CinemachineCameraTarget.transform.localPosition}");
                sb.AppendLine($"  Target worldPos: {tpc.CinemachineCameraTarget.transform.position}");
                sb.AppendLine($"  Target localRot: {tpc.CinemachineCameraTarget.transform.localEulerAngles}");
            }
            var anim = tpc.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                var head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (head != null)
                {
                    sb.AppendLine($"  Head bone pos: {head.position}, localPos: {head.localPosition}");
                }
            }
            sb.AppendLine($"TopClamp: {tpc.TopClamp}");
            sb.AppendLine($"BottomClamp: {tpc.BottomClamp}");
            sb.AppendLine($"CameraAngleOverride: {tpc.CameraAngleOverride}");
            sb.AppendLine($"LockCameraPosition: {tpc.LockCameraPosition}");
        }

        System.IO.File.WriteAllText("Assets/Editor/camera_inspect.txt", sb.ToString());
        Debug.Log("[INSPECT COMPLETE] Wrote camera_inspect.txt:\n" + sb.ToString());
    }

    [MenuItem("Tools/Apply RE Camera Settings To Scene")]
    public static void ApplyRECameraSettingsToScene()
    {
        Debug.Log("[RE Camera] Applying locked Resident Evil OTS settings to scene...");

        // 1. Configure PlayerFollowCamera
        var followCamGo = GameObject.Find("PlayerFollowCamera");
        if (followCamGo != null)
        {
            var cmCam = followCamGo.GetComponent<CinemachineCamera>();
            if (cmCam != null)
            {
                cmCam.Lens.FieldOfView = 48f; // FOV 48°
                EditorUtility.SetDirty(cmCam);
                Debug.Log("[RE Camera] CinemachineCamera Lens.FieldOfView set to 48°");
            }

            var tp = followCamGo.GetComponent<CinemachineThirdPersonFollow>();
            if (tp != null)
            {
                tp.CameraDistance = 0.99f; // ระยะกล้อง 0.99 m
                tp.CameraSide = 1.0f; // เยื้องขวาเต็มรูปแบบ
                tp.ShoulderOffset = new Vector3(0.60f, -0.374f, 0.0f); // เยื้องขวา 0.60 m, ความสูง -0.374 m (-0.37 m)
                tp.VerticalArmLength = 0.0f;
                tp.Damping = new Vector3(0.05f, 0.08f, 0.08f);
                tp.AvoidObstacles.Enabled = false;
                EditorUtility.SetDirty(tp);
                Debug.Log("[RE Camera] CinemachineThirdPersonFollow: Distance=0.99, Side=1.0, ShoulderOffset=(0.60, -0.374, 0), ArmLength=0");
            }

            var endfield = followCamGo.GetComponent<EndfieldCameraController>();
            if (endfield != null)
            {
                endfield.fieldOfView = 48f;
                endfield.cameraDistance = 0.99f;
                endfield.shoulderSide = 0.60f;
                endfield.shoulderHeight = -0.374f;
                endfield.cameraTilt = -16.4f;
                endfield.shoulderOffset = new Vector3(0.60f, -0.374f, 0.0f);
                endfield.verticalArmLength = 0.0f;
                endfield.defaultCameraSide = 1.0f;
                endfield.enableZoom = false; // ปิดการซูมถาวรเพื่อล็อคระยะ
                endfield.defaultDistance = 0.99f;
                endfield.minDistance = 0.99f;
                endfield.maxDistance = 0.99f;
                EditorUtility.SetDirty(endfield);
                Debug.Log("[RE Camera] EndfieldCameraController: distance=0.99, side=0.60, height=-0.374, tilt=-16.4, FOV=48, enableZoom=false");
            }

            EditorUtility.SetDirty(followCamGo);
        }
        else
        {
            Debug.LogError("[RE Camera] PlayerFollowCamera GameObject not found in active scene!");
        }

        // 2. Configure ThirdPersonController on player
        var tpc = Object.FindAnyObjectByType<ThirdPersonController>();
        if (tpc != null)
        {
            tpc.CameraAngleOverride = -16.4f; // มุมก้ม/เงย -16.4°
            EditorUtility.SetDirty(tpc);
            Debug.Log("[RE Camera] ThirdPersonController CameraAngleOverride set to -16.4°");

            if (tpc.CinemachineCameraTarget != null)
            {
                tpc.CinemachineCameraTarget.transform.localEulerAngles = new Vector3(-16.4f, 0f, 0f);
                EditorUtility.SetDirty(tpc.CinemachineCameraTarget);
                Debug.Log($"[RE Camera] CinemachineCameraTarget ({tpc.CinemachineCameraTarget.name}) localEulerAngles set to (-16.4, 0, 0)");
            }
        }

        // 3. Configure PlayerCameraRoot directly if exists
        var playerCameraRoot = GameObject.Find("PlayerCameraRoot");
        if (playerCameraRoot != null)
        {
            playerCameraRoot.transform.localEulerAngles = new Vector3(-16.4f, 0f, 0f);
            EditorUtility.SetDirty(playerCameraRoot);
            Debug.Log("[RE Camera] PlayerCameraRoot localEulerAngles set to (-16.4, 0, 0)");
        }

        // 4. Save the active scene
        var currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(currentScene);
        bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[RE Camera] Scene '{currentScene.name}' saved: {saved}");

        // 5. Inspect and write output verification
        Inspect();
    }
}
