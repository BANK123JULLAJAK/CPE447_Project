using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Unity.Cinemachine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ระบบกล้องคัตซีนภาพยนตร์ (Cinematic Cutscene Camera Director)
/// สลับและเคลื่อนไหวมุมกล้องภาพยนตร์จริง:
/// 1. Shot 1: Establishing Shot - ดอลลี่เคลื่อนที่ช้าๆ พร้อมแพน/ทิลต์มุมกล้องขึ้นส่องยอดหอคอยโบสถ์เซนต์แมรี่
/// 2. Shot 2: Close-Up สารวัตรนิวแมน - ดอลลี่อิน (Push-in) พร้อมระบบ Depth of Field (Bokeh) หน้าชัดหลังเบลอ
/// 3. Shot 3: Crime Scene Tracking - ดอลลี่มุมต่ำกวาดผ่านร่องรอยวัตถุพยานบนพื้นมุ่งสู่แท่นบูชา
/// </summary>
public class CinematicCutsceneManager : MonoBehaviour
{
    private static CinematicCutsceneManager _instance;
    public static CinematicCutsceneManager Instance => _instance;

    [Header("🎬 กล้องคัตซีนภาพยนตร์ Scene 1 (St. Mary Church Cameras)")]
    [Tooltip("ช็อตที่ 1: มุมกว้างส่องความอลังการของโบสถ์เซนต์แมรี่และหมอกราตรี (Establishing Shot)")]
    public CinemachineCamera camChurchWide;

    [Tooltip("ช็อตที่ 2: มุมโคลสอัพครึ่งตัวสารวัตรนิวแมน (Character Portrait Medium Shot)")]
    public CinemachineCamera camNewmanClose;

    [Tooltip("ช็อตที่ 3: มุมเจาะลึกเข้าไปในที่เกิดเหตุและแท่นบูชา (Interior Crime Scene Shot)")]
    public CinemachineCamera camCrimeScene;

    [Header("🎬 กล้องคัตซีนภาพยนตร์ Scene 2 (London Alley Cameras)")]
    [Tooltip("ช็อตที่ 1: มุมกว้างติดตามตรอกไวท์ชาเปล (Alley Tracking Shot)")]
    public CinemachineCamera camAlleyTracking;

    [Tooltip("ช็อตที่ 2: มุมเจาะลึกโคลสอัพศพเหยื่อและสารวัตรนิวแมน (Corpse Forensic Close-Up)")]
    public CinemachineCamera camAlleyCorpseClose;

    [Tooltip("ช็อตที่ 3: มุมมาโครเจาะจงหนังสือพิมพ์ปริศนาและรหัสลับซีซาร์ (Newspaper Macro Push)")]
    public CinemachineCamera camAlleyNewspaperMacro;

    [Header("🎬 กล้องคัตซีนภาพยนตร์ Scene 3 (Royal Lyceum Theatre Cameras)")]
    [Tooltip("ช็อตที่ 1: มุมกว้างเปิดประตูบุกเข้าสู่โรงละครรอยัลไลเซียม (Theater Wide Breach Shot)")]
    public CinemachineCamera camTheaterWideBreach;

    [Tooltip("ช็อตที่ 2: มุมเผชิญหน้า Jack The Ripper กำลังจะลงมือกับเหยื่อ (Jack Confrontation Close-Up)")]
    public CinemachineCamera camTheaterJackConfrontation;

    [Tooltip("ช็อตที่ 3: มุมสารวัตรนิวแมนเข้าช่วยเหลือเหยื่อที่รอดชีวิต (Victim Rescue Medium Shot)")]
    public CinemachineCamera camTheaterVictimRescue;

    [Tooltip("ช็อตที่ 4: มุมมาโครเจาะจงตั๋วรถไฟที่ฉีกขาดตกอยู่บนพื้น (Torn Ticket Macro Shot)")]
    public CinemachineCamera camTheaterTicketMacro;

    [Tooltip("ช็อตสลับมาที่สารวัตรนิวแมนขณะ Jack หลบหนี (Inspector Newman Reaction Shot)")]
    public CinemachineCamera camTheaterNewmanReaction;

    [Header("🎬 กล้องคัตซีนภาพยนตร์ Scene 4 (Underground Station Cameras)")]
    [Tooltip("ช็อตที่ 1: มุมกว้างชานชาลารถไฟใต้ดินและขบวนรถไฟ (Underground Station Wide Shot)")]
    public CinemachineCamera camStationUndergroundWide;

    [Tooltip("ช็อตที่ 2: มุมเจาะลึกรอยเลือดและปลอกกระสุนข้างขบวนรถไฟ (Evidence Macro Push Shot)")]
    public CinemachineCamera camStationEvidencePush;

    [Header("🎮 กล้องผู้เล่น (Gameplay Follow Camera)")]
    public CinemachineCamera playerFollowCamera;

    [Header("🎭 แอนิเมชันคัตซีน (MoCap Central Animations)")]
    public AnimationClip animLookAround;
    public AnimationClip animTalking;

    [Header("🥊 แอนิเมชันต่อสู้ (MoCap Central Fight: A = ผู้โจมตี, B = ผู้ถูกโจมตี)")]
    public AnimationClip fightKickA;
    public AnimationClip fightKickB;
    public AnimationClip fightKnockA;
    public AnimationClip fightKnockB;

    private StarterAssets.ThirdPersonController _playerController;
    private CharacterController _characterController;
    private StarterAssets.StarterAssetsInputs _playerInputs;
    private Animator _playerAnimator;
    private Transform _playerCameraRoot;
    private Transform _newmanHeadTransform;

    private Volume _postProcessingVolume;
    private DepthOfField _depthOfField;
    private Coroutine _cameraMotionCoroutine;
    private PlayableGraph _playableGraph;
    private CinemachineBrain _cinemachineBrain;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        FindPlayerAndCameras();
        InitializePostProcessing();
        LoadMoCapAnimations();
        ResetAllCutsceneCameras();
        SetPlayerControlsLocked(false);
    }

    private void Start()
    {
        // ในตอนเริ่มเกม ให้ลด Priority กล้องคัตซีนทั้งหมด เพื่อให้กล้องผู้เล่นทำงานปกติ
        FindPlayerAndCameras();
        ResetAllCutsceneCameras();
        SetPlayerControlsLocked(false);
    }

    private void LoadMoCapAnimations()
    {
        if (animLookAround == null)
        {
            animLookAround = GameAssets.LoadClip("Assets/MoCapCentral/MC_Sample/Animations/Idle/MCU_am_Stand_Idle_03_LookAround.FBX");
        }
        if (animTalking == null)
        {
            animTalking = GameAssets.LoadClip("Assets/MoCapCentral/MC_Sample/Animations/Idle/MCU_am_Stand_Conv_Talk_05_Generic.FBX");
        }
        const string fightDir = "Assets/MoCapCentral/MC_Sample/Animations/Fight/";
        if (fightKickA == null) fightKickA = LoadFirstClip(fightDir + "MCU_am_Ready_Fight_02_Kickdown_A.FBX");
        if (fightKickB == null) fightKickB = LoadFirstClip(fightDir + "MCU_af_Ready_Fight_02_Kickdown_B.FBX");
        if (fightKnockA == null) fightKnockA = LoadFirstClip(fightDir + "MCU_am_Ready_Fight_01_Knockdown_A.FBX");
        if (fightKnockB == null) fightKnockB = LoadFirstClip(fightDir + "MCU_af_Ready_Fight_01_Knockdown_B.FBX");
    }

    private static AnimationClip LoadFirstClip(string path)
    {
        return GameAssets.LoadClip(path);
    }

    private AnimationClipPlayable _newmanClipPlayable;
    // Animator ของ PlayerArmature คุมกระดูกชุดเดียวกับ Newman_Model -> เล่นคลิปเดียวกันทั้งสองตัว ไม่ให้ท่าเดินทับท่าคัตซีน
    private PlayableGraph _mirrorGraph;
    private AnimationClipPlayable _mirrorClipPlayable;
    private Animator _visibleNewmanAnimator;
    /// <summary>Animator ที่ขับโมเดลนิวแมนที่มองเห็นจริง (Newman_Model) ถ้าไม่มีใช้ของ PlayerArmature</summary>
    // Animator สองตัว (Newman_Model + PlayerArmature) คุมกระดูกชุดเดียวกัน -> เล่นคลิปเดียวกันทั้งสองตัวพร้อมกัน
    // ห้ามปิด/เปิด Animator ตัวใดตัวหนึ่ง (การเปิดใหม่ทำให้ลำดับประมวลผลเปลี่ยน แล้วท่าเดิน/วิ่งในเกมพังหลังคัตซีน)
    private Animator CutsceneAnimator => _visibleNewmanAnimator != null ? _visibleNewmanAnimator : _playerAnimator;

    public void PlayCutsceneAnimation(AnimationClip clip)
    {
        PlayCutsceneAnimation(clip, 0f);
    }

    /// <summary>เล่นคลิปบนนิวแมนโดยเริ่มที่เวลา startTime (ใช้ข้ามช่วงยืนรอของท่าต่อสู้ที่ยาว)</summary>
    public void PlayCutsceneAnimation(AnimationClip clip, float startTime)
    {
        var target = CutsceneAnimator;
        if (target == null || clip == null) return;
        StopCutsceneAnimation();
        // ให้แน่ใจว่าโมเดลที่มองเห็นอัปเดตท่าเสมอ (ถ้าปิดอยู่หรือถูก cull ท่าจะค้างแข็ง)
        Debug.Log($"[Cutscene Anim] clip={clip.name} t={startTime:0.0} target={target.name} enabled={target.enabled} human={target.isHuman} avatar={(target.avatar != null ? target.avatar.name : "none")} mirror={(_playerAnimator != null && _playerAnimator != target ? _playerAnimator.name : "-")}");
        _newmanClipPlayable = AnimationPlayableUtilities.PlayClip(target, clip, out _playableGraph);
        if (startTime > 0f) _newmanClipPlayable.SetTime(startTime);
        if (_playerAnimator != null && _playerAnimator != target)
        {
            _mirrorClipPlayable = AnimationPlayableUtilities.PlayClip(_playerAnimator, clip, out _mirrorGraph);
            if (startTime > 0f) _mirrorClipPlayable.SetTime(startTime);
        }
    }

    public void StopCutsceneAnimation()
    {
        if (_playableGraph.IsValid())
        {
            _playableGraph.Destroy();
        }
        if (_mirrorGraph.IsValid())
        {
            _mirrorGraph.Destroy();
        }

    }

    private void OnDestroy()
    {
        StopCutsceneAnimation();
    }

    public void InitializePostProcessing()
    {
        var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include);
        foreach (var v in volumes)
        {
            if (v.profile != null)
            {
                if (v.profile.TryGet<DepthOfField>(out var dof))
                {
                    _postProcessingVolume = v;
                    _depthOfField = dof;
                    _depthOfField.active = false;
                    break;
                }
                else if (v.isGlobal)
                {
                    _postProcessingVolume = v;
                    _depthOfField = v.profile.Add<DepthOfField>(true);
                    _depthOfField.mode.overrideState = true;
                    _depthOfField.mode.value = DepthOfFieldMode.Bokeh;
                    _depthOfField.active = false;
                    break;
                }
            }
        }
    }

    public void FindPlayerAndCameras()
    {
        // ค้นหากล้อง PlayerFollowCamera
        if (playerFollowCamera == null)
        {
            var allCams = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);
            foreach (var c in allCams)
            {
                if (c.name.Contains("PlayerFollow") || c.name.Contains("FollowCamera"))
                {
                    playerFollowCamera = c;
                    break;
                }
            }
        }

        // ค้นหา CinemachineBrain บน MainCamera และตั้งค่าให้ตัดสลับภาพทันที (Hard Cut ห้ามบินทะลุกำแพง)
        if (Camera.main != null)
        {
            _cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
            if (_cinemachineBrain != null)
            {
                _cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            }
        }

        // ค้นหากล้องคัตซีน Scene 1
        if (camChurchWide == null)
        {
            GameObject obj = GameObject.Find("Cutscene_Cam_ChurchWide");
            if (obj != null) camChurchWide = obj.GetComponent<CinemachineCamera>();
            if (camChurchWide == null)
            {
                GameObject newObj = new GameObject("Cutscene_Cam_ChurchWide");
                newObj.transform.position = new Vector3(11.2f, 3.2f, 16.0f);
                newObj.transform.rotation = Quaternion.Euler(-6f, 34f, 0f);
                camChurchWide = newObj.AddComponent<CinemachineCamera>();
                camChurchWide.Lens.FieldOfView = 48f;
                camChurchWide.Priority.Value = 0;
            }
        }

        if (camNewmanClose == null)
        {
            GameObject obj = GameObject.Find("Cutscene_Cam_NewmanClose");
            if (obj != null) camNewmanClose = obj.GetComponent<CinemachineCamera>();
            if (camNewmanClose == null)
            {
                GameObject newObj = new GameObject("Cutscene_Cam_NewmanClose");
                newObj.transform.position = new Vector3(19.2f, 2.3f, 27.2f);
                newObj.transform.rotation = Quaternion.Euler(-2f, 20f, 0f);
                camNewmanClose = newObj.AddComponent<CinemachineCamera>();
                camNewmanClose.Lens.FieldOfView = 30f;
                camNewmanClose.Priority.Value = 0;
            }
        }

        if (camCrimeScene == null)
        {
            GameObject obj = GameObject.Find("Cutscene_Cam_CrimeScene");
            if (obj != null) camCrimeScene = obj.GetComponent<CinemachineCamera>();
            if (camCrimeScene == null)
            {
                GameObject newObj = new GameObject("Cutscene_Cam_CrimeScene");
                newObj.transform.position = new Vector3(21.5f, 1.30f, 29.30f);
                newObj.transform.rotation = Quaternion.Euler(5f, 90f, 0f);
                camCrimeScene = newObj.AddComponent<CinemachineCamera>();
                camCrimeScene.Lens.FieldOfView = 44f;
                camCrimeScene.Priority.Value = 0;
            }
        }

        // ค้นหากล้องคัตซีน Scene 2 (London Alley Cameras)
        if (camAlleyTracking == null)
        {
            GameObject obj = GameObject.Find("Cutscene_Cam_AlleyTracking");
            if (obj != null) camAlleyTracking = obj.GetComponent<CinemachineCamera>();
            if (camAlleyTracking == null)
            {
                GameObject newObj = new GameObject("Cutscene_Cam_AlleyTracking");
                newObj.transform.position = new Vector3(-11.5f, 2.5f, -101.5f);
                newObj.transform.rotation = Quaternion.Euler(5f, -90f, 0f);
                camAlleyTracking = newObj.AddComponent<CinemachineCamera>();
                camAlleyTracking.Lens.FieldOfView = 36f;
                camAlleyTracking.Priority.Value = 0;
            }
        }

        if (camAlleyCorpseClose == null)
        {
            GameObject obj = GameObject.Find("Cutscene_Cam_AlleyCorpseClose");
            if (obj != null) camAlleyCorpseClose = obj.GetComponent<CinemachineCamera>();
            if (camAlleyCorpseClose == null)
            {
                GameObject newObj = new GameObject("Cutscene_Cam_AlleyCorpseClose");
                newObj.transform.position = new Vector3(-16.8f, 1.4f, -100.8f);
                newObj.transform.rotation = Quaternion.Euler(22f, -125f, 0f);
                camAlleyCorpseClose = newObj.AddComponent<CinemachineCamera>();
                camAlleyCorpseClose.Lens.FieldOfView = 30f;
                camAlleyCorpseClose.Priority.Value = 0;
            }
        }

        if (camAlleyNewspaperMacro == null)
        {
            GameObject obj = GameObject.Find("Cutscene_Cam_AlleyNewspaperMacro");
            if (obj != null) camAlleyNewspaperMacro = obj.GetComponent<CinemachineCamera>();
            if (camAlleyNewspaperMacro == null)
            {
                GameObject newObj = new GameObject("Cutscene_Cam_AlleyNewspaperMacro");
                newObj.transform.position = new Vector3(-16.80f, 0.95f, -103.4f);
                newObj.transform.rotation = Quaternion.Euler(52f, -165f, 0f);
                camAlleyNewspaperMacro = newObj.AddComponent<CinemachineCamera>();
                camAlleyNewspaperMacro.Lens.FieldOfView = 26f;
                camAlleyNewspaperMacro.Priority.Value = 0;
            }
        }

        // ค้นหากล้องคัตซีน Scene 3 (หน้าโรงละครรอยัลไลเซียม / Royal Lyceum Theatre Front Cameras)
        if (camTheaterWideBreach == null)
        {
            GameObject obj = GameObject.Find("Cutscene_Cam_TheaterWideBreach");
            if (obj != null) camTheaterWideBreach = obj.GetComponent<CinemachineCamera>();
            if (camTheaterWideBreach == null)
            {
                GameObject newObj = new GameObject("Cutscene_Cam_TheaterWideBreach");
                newObj.transform.position = new Vector3(20.5f, 2.2f, -86.5f);
                newObj.transform.rotation = Quaternion.Euler(5f, 85f, 0f);
                camTheaterWideBreach = newObj.AddComponent<CinemachineCamera>();
                camTheaterWideBreach.Lens.FieldOfView = 45f;
                camTheaterWideBreach.Priority.Value = 0;
            }
        }

        if (camTheaterJackConfrontation == null)
        {
            GameObject obj = GameObject.Find("Cutscene_Cam_TheaterJackConfrontation");
            if (obj != null) camTheaterJackConfrontation = obj.GetComponent<CinemachineCamera>();
            if (camTheaterJackConfrontation == null)
            {
                GameObject newObj = new GameObject("Cutscene_Cam_TheaterJackConfrontation");
                newObj.transform.position = new Vector3(28.5f, 1.35f, -86.8f);
                newObj.transform.rotation = Quaternion.Euler(6f, 80f, 0f);
                camTheaterJackConfrontation = newObj.AddComponent<CinemachineCamera>();
                camTheaterJackConfrontation.Lens.FieldOfView = 32f;
                camTheaterJackConfrontation.Priority.Value = 0;
            }
        }

        if (camTheaterVictimRescue == null)
        {
            GameObject obj = GameObject.Find("Cutscene_Cam_TheaterVictimRescue");
            if (obj != null) camTheaterVictimRescue = obj.GetComponent<CinemachineCamera>();
            if (camTheaterVictimRescue == null)
            {
                GameObject newObj = new GameObject("Cutscene_Cam_TheaterVictimRescue");
                newObj.transform.position = new Vector3(29.2f, 1.2f, -88.5f);
                newObj.transform.rotation = Quaternion.Euler(12f, 65f, 0f);
                camTheaterVictimRescue = newObj.AddComponent<CinemachineCamera>();
                camTheaterVictimRescue.Lens.FieldOfView = 35f;
                camTheaterVictimRescue.Priority.Value = 0;
            }
        }

        if (camTheaterTicketMacro == null)
        {
            GameObject obj = GameObject.Find("Cutscene_Cam_TheaterTicketMacro");
            if (obj != null) camTheaterTicketMacro = obj.GetComponent<CinemachineCamera>();
            if (camTheaterTicketMacro == null)
            {
                GameObject newObj = new GameObject("Cutscene_Cam_TheaterTicketMacro");
                newObj.transform.position = new Vector3(30.4f, 0.85f, -87.2f);
                newObj.transform.rotation = Quaternion.Euler(48f, 70f, 0f);
                camTheaterTicketMacro = newObj.AddComponent<CinemachineCamera>();
                camTheaterTicketMacro.Lens.FieldOfView = 26f;
                camTheaterTicketMacro.Priority.Value = 0;
            }
        }

        if (camTheaterNewmanReaction == null)
        {
            GameObject obj = GameObject.Find("Cutscene_Cam_TheaterNewmanReaction");
            if (obj != null) camTheaterNewmanReaction = obj.GetComponent<CinemachineCamera>();
            if (camTheaterNewmanReaction == null)
            {
                GameObject newObj = new GameObject("Cutscene_Cam_TheaterNewmanReaction");
                newObj.transform.position = new Vector3(26.8f, 1.45f, -87.76f);
                newObj.transform.rotation = Quaternion.Euler(6f, -90f, 0f);
                camTheaterNewmanReaction = newObj.AddComponent<CinemachineCamera>();
                camTheaterNewmanReaction.Lens.FieldOfView = 30f;
                camTheaterNewmanReaction.Priority.Value = 0;
            }
        }

        // ค้นหาตัวละครผู้เล่นเพื่อล็อคการเดินขณะดูคัตซีน และค้นหาตำแหน่งศีรษะ/ระดับสายตาที่แท้จริง
        GameObject player = GameObject.Find("NestedParentArmature_Newman") 
                         ?? GameObject.Find("PlayerArmature") 
                         ?? GameObject.Find("Player_CH2_Newman");

        if (player != null)
        {
            _playerController = player.GetComponentInChildren<StarterAssets.ThirdPersonController>();
            _characterController = player.GetComponentInChildren<CharacterController>();
            _playerInputs = player.GetComponentInChildren<StarterAssets.StarterAssetsInputs>();
            _playerAnimator = player.GetComponentInChildren<Animator>();

            var allChildren = player.GetComponentsInChildren<Transform>(true);
            foreach (var t in allChildren)
            {
                if (t.name == "PlayerCameraRoot") { _playerCameraRoot = t; break; }
            }

            GameObject newmanModel = GameObject.Find("Newman_Model");
            if (newmanModel != null)
            {
                var anims = newmanModel.GetComponentsInChildren<Animator>(true);
                foreach (var a in anims)
                {
                    if (a.isHuman)
                    {
                        _visibleNewmanAnimator = a; // โมเดล VRoid ที่มองเห็นจริง (มี Animator ของตัวเอง)
                        Transform h = a.GetBoneTransform(HumanBodyBones.Head);
                        if (h != null) { _newmanHeadTransform = h; break; }
                    }
                }
            }

            if (_newmanHeadTransform == null && _playerCameraRoot != null)
            {
                _newmanHeadTransform = _playerCameraRoot;
            }
        }
    }

    /// <summary>
    /// ดึงตำแหน่งศีรษะ/ระดับสายตาจริงของสารวัตรนิวแมน เพื่อการโฟกัสและจัดเฟรมกล้องที่แม่นยำ 100%
    /// </summary>
    public Vector3 GetNewmanHeadPosition()
    {
        if (_newmanHeadTransform != null) return _newmanHeadTransform.position;
        if (_playerCameraRoot != null) return _playerCameraRoot.position;
        if (_playerController != null) return _playerController.transform.position + Vector3.up * 1.65f;
        return new Vector3(9.04f, 1.70f, 24.6f);
    }

    public Transform GetNewmanHeadTransform()
    {
        if (_newmanHeadTransform != null) return _newmanHeadTransform;
        if (_playerCameraRoot != null) return _playerCameraRoot;
        if (_playerController != null) return _playerController.transform;
        return null;
    }

    /// <summary>
    /// ทิศทางที่สารวัตรนิวแมนหันหน้าไป
    /// </summary>
    public Vector3 GetNewmanForward()
    {
        if (_playerController != null) return _playerController.transform.forward;
        GameObject pArm = GameObject.Find("PlayerArmature");
        if (pArm != null) return pArm.transform.forward;
        return Vector3.forward;
    }

    /// <summary>
    /// สั่งเปิดใช้งานมุมกล้องคัตซีนที่ระบุ โดยตัดภาพทันที (Hard Cut) ไม่บินกล้องชนกำแพง
    /// </summary>
    public void SwitchToCutsceneShot(CinemachineCamera targetShot)
    {
        if (targetShot == null) return;

        // บังคับให้ CinemachineBrain ตัดสลับภาพทันที (0 วินาที) ป้องกันการบินทะลุกำแพง
        if (_cinemachineBrain != null)
        {
            _cinemachineBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
        }

        ResetAllCutsceneCameras();

        // ตั้ง Priority กล้องเป้าหมายให้สูงสุด (25) เพื่อแซงกล้องผู้เล่น (10)
        targetShot.Priority.Value = 25;
        targetShot.gameObject.SetActive(true);
    }

    /// <summary>
    /// ปิดกล้องคัตซีนทั้งหมด และสลับกลับสู่กล้องควบคุมผู้เล่นตามเดิม
    /// </summary>
    public void ResetAllCutsceneCameras()
    {
        if (_cameraMotionCoroutine != null)
        {
            StopCoroutine(_cameraMotionCoroutine);
            _cameraMotionCoroutine = null;
        }

        StopCutsceneAnimation();

        foreach (var used in _usedShotCams)
        {
            if (used == null) continue;
            CinematicCameraFeel.Clear(used);
            used.Lens.Dutch = 0f;
        }

        // ตรวจสอบให้ MainCamera และ PlayerFollowCamera อยู่ที่ Root ของ Hierarchy เสมอ (ห้ามถูกจับยัดเข้าไปในตัวละคร)
        if (Camera.main != null && Camera.main.transform.parent != null)
        {
            Camera.main.transform.SetParent(null);
        }
        if (playerFollowCamera != null && playerFollowCamera.transform.parent != null)
        {
            playerFollowCamera.transform.SetParent(null);
        }

        // รีเซ็ต Priority กล้องคัตซีนทั้งหมดลงเป็น 0 เพื่อไม่ให้แย่งภาพจากกล้องผู้เล่น
        if (camChurchWide != null) camChurchWide.Priority.Value = 0;
        if (camNewmanClose != null) camNewmanClose.Priority.Value = 0;
        if (camCrimeScene != null) camCrimeScene.Priority.Value = 0;
        if (camAlleyTracking != null) camAlleyTracking.Priority.Value = 0;
        if (camAlleyCorpseClose != null) camAlleyCorpseClose.Priority.Value = 0;
        if (camAlleyNewspaperMacro != null) camAlleyNewspaperMacro.Priority.Value = 0;
        if (camTheaterWideBreach != null) camTheaterWideBreach.Priority.Value = 0;
        if (camTheaterJackConfrontation != null) camTheaterJackConfrontation.Priority.Value = 0;
        if (camTheaterVictimRescue != null) camTheaterVictimRescue.Priority.Value = 0;
        if (camTheaterTicketMacro != null) camTheaterTicketMacro.Priority.Value = 0;
        if (camTheaterNewmanReaction != null) camTheaterNewmanReaction.Priority.Value = 0;
        if (camStationUndergroundWide != null) camStationUndergroundWide.Priority.Value = 0;
        if (camStationEvidencePush != null) camStationEvidencePush.Priority.Value = 0;

        // บังคับให้กล้องผู้เล่น (PlayerFollowCamera) มี Priority ชนะเสมอในเกมเพลย์
        if (playerFollowCamera != null)
        {
            playerFollowCamera.Priority.Value = 10;
        }

        // คืนค่า Depth of Field เป็นปิด ให้ผู้เล่นมองเห็นโลกชัดเจนในเกมเพลย์
        if (_depthOfField != null)
        {
            _depthOfField.active = false;
        }
    }

    /// <summary>
    /// ล็อคหรือปลดล็อคการเคลื่อนไหวและการควบคุมมุมกล้องของผู้เล่น (พร้อมแก้บัคมุมกล้องหมุนควงหลังจบคัตซีน)
    /// </summary>
    public void SetPlayerControlsLocked(bool locked)
    {
        if (locked)
        {
            if (_playerInputs != null)
            {
                _playerInputs.MoveInput(Vector2.zero);
                _playerInputs.LookInput(Vector2.zero);
                _playerInputs.SprintInput(false);
                _playerInputs.JumpInput(false);
                _playerInputs.cursorLocked = false;
                _playerInputs.cursorInputForLook = false;
            }

            if (_playerAnimator != null)
            {
                _playerAnimator.SetFloat("Speed", 0f);
                _playerAnimator.SetFloat("MotionSpeed", 0f);
                _playerAnimator.SetBool("Jump", false);
                _playerAnimator.SetBool("FreeFall", false);
            }

            if (_playerController != null)
            {
                _playerController.LockCameraPosition = true;
                _playerController.enabled = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 1. ซิงก์มุมกล้องผู้เล่นให้ตรงกับทิศทางตัวละคร ป้องกันการหมุนควง
            if (_playerController != null)
            {
                _playerController.enabled = true;
                _playerController.LockCameraPosition = false;
                _playerController.SynchronizeCameraYawWithCharacter();
            }

            // 2. สั่งรีเซ็ตสถานะภายในของ Cinemachine ทันทีเพื่อไม่ให้ Lerp/Damp ข้ามมุมกล้อง
            if (playerFollowCamera != null)
            {
                playerFollowCamera.PreviousStateIsValid = false;
            }

            // 3. ล็อคเคอร์เซอร์กลับเข้ากลางจอ
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // 4. หน่วงเวลา 1 เฟรมก่อนเปิดรับ LookInput เพื่อดูดซับ Mouse Delta Jump จากการล็อคเคอร์เซอร์
            StartCoroutine(EnableLookInputNextFrame());
        }
    }

    private IEnumerator EnableLookInputNextFrame()
    {
        if (_playerInputs != null)
        {
            _playerInputs.LookInput(Vector2.zero);
            _playerInputs.cursorLocked = true;
            _playerInputs.cursorInputForLook = false;
        }
        yield return null;
        yield return new WaitForEndOfFrame();
        if (_playerInputs != null)
        {
            _playerInputs.LookInput(Vector2.zero);
            _playerInputs.cursorInputForLook = true;
        }
    }

    /// <summary>
    /// เริ่มเล่นคัตซีนเปิดตัว Scene 1 แบบภาพยนตร์เต็มรูปแบบ พร้อมสลับกล้องและขยับมุมกล้องตามบทสนทนา
    /// </summary>
    public void PlayScene1IntroCinematic(Action onFinished = null)
    {
        StartCoroutine(Scene1CinematicRoutine(onFinished));
    }

    private IEnumerator Scene1CinematicRoutine(Action onFinished)
    {
        SetPlayerControlsLocked(true);

        // ซ่อนหน้าต่างเควสทันทีขณะเข้าสู่คัตซีน
        if (StMaryChurchQuestManager.Instance != null)
        {
            StMaryChurchQuestManager.Instance.ShowObjectiveHUD(false);
        }

        if (CinematicLetterbox.Instance != null)
        {
            // บทคัตซีนเปิดบทที่ 1: ให้นิวแมนเล่าผ่านมุมมองตัวเอง (ไม่มีตัวละครอื่น)
            // ปูความเงียบ -> ข้อมูลผู้ตายแบบมีอารมณ์ -> คำถามที่ค้างในใจ -> ส่งต่อเข้าเกมเพลย์การสแกน
            // กล้องเปลี่ยนช็อตเฉพาะประโยคแรกของแต่ละช็อต ประโยคต่อ ๆ ไปใช้มุมกล้องเดิมที่ยังเคลื่อนอยู่
            const string newman = "สารวัตรนิวแมน";
            var cutsceneDialogues = new List<CinematicLetterbox.DialogueEntry>()
            {
                // Shot 1: Establishing Shot (โบสถ์เซนต์แมรี่) - เครนเสยขึ้นสู่ซุ้มประตูโบสถ์
                new CinematicLetterbox.DialogueEntry(newman,
                    "ระฆังเซนต์แมรี่ตีบอกเวลาเที่ยงคืน... แต่คืนนี้ไม่มีใครมาสวดภาวนา",
                    () => PlayShot1Establishing()),
                new CinematicLetterbox.DialogueEntry(newman,
                    "โทรเลขจากโรงพักมีแค่ประโยคเดียว \"พบศพหญิงในโบสถ์ ประตูไม่ได้ลงกลอน\"",
                    () => PlayShot1bTelegramProfile()),

                // Shot 2: Close-Up สารวัตรนิวแมน - ดอลลี่อินช้า ๆ ระหว่างทบทวนข้อมูลผู้ตาย
                new CinematicLetterbox.DialogueEntry(newman,
                    "ผู้ตายชื่อแมรี่ แอน อายุราวสี่สิบสองถึงสี่สิบห้า เป็นช่างตัดเย็บที่รับจ้างซ่อมเสื้อผ้าอยู่แถวนี้",
                    () => PlayShot2NewmanCloseUp()),
                new CinematicLetterbox.DialogueEntry(newman,
                    "หญิงยากจนคนหนึ่งในลอนดอน... คนแถวนี้เล่าว่าเมื่อหลายปีก่อน เธอต้องทิ้งลูกชายไว้เพราะเลี้ยงดูไม่ไหว",
                    () => PlayShot2bPovStreet()),
                new CinematicLetterbox.DialogueEntry(newman,
                    "ไม่มีเงิน ไม่มีศัตรู ไม่มีอะไรให้ปล้น... แล้วใครกันที่อยากให้เธอตาย?",
                    () => PlayShot2cExtremeCloseUp()),

                // Shot 3: Interior Crime Scene - กล้องต่ำเคลื่อนตามทางเดินกลางโบสถ์ ผ่านร่องรอยบนพื้นสู่แท่นบูชา
                new CinematicLetterbox.DialogueEntry(newman,
                    "ฆาตกรรีบร้อน แต่ไม่ได้ประมาท... ของที่ตกอยู่ในโบสถ์นี้ มันอาจตั้งใจทิ้งไว้ให้เราเห็น",
                    () => PlayShot3CrimeSceneTracking()),
                new CinematicLetterbox.DialogueEntry(newman,
                    "ห้าสิ่งที่ไม่ควรอยู่ตรงนี้... ต้องใช้ CiRA CORE ช่วยมองสิ่งที่ตาเปล่ามองข้าม",
                    () => PlayShot3bNavePullBack())
            };

            CinematicLetterbox.Instance.PlayDialogueSequence(cutsceneDialogues, () =>
            {
                // จบคัตซีน: คืนค่ามุมกล้องผู้เล่น, ปิดแถบดำ, แสดงหน้าต่างเควสกลับมา
                ResetAllCutsceneCameras();
                CinematicLetterbox.Instance.HideLetterbox();
                SetPlayerControlsLocked(false);

                if (StMaryChurchQuestManager.Instance != null)
                {
                    StMaryChurchQuestManager.Instance.ShowObjectiveHUD(true);
                }

                onFinished?.Invoke();
            });
        }
        else
        {
            SetPlayerControlsLocked(false);
            if (StMaryChurchQuestManager.Instance != null)
            {
                StMaryChurchQuestManager.Instance.ShowObjectiveHUD(true);
            }
            onFinished?.Invoke();
        }

        yield return null;
    }

    /// <summary>
    /// ตรวจสอบและค้นหา DepthOfField บน Volume ให้พร้อมใช้งานเสมอ
    /// </summary>
    private void EnsureDepthOfFieldReady()
    {
        if (_depthOfField != null) return;
        var volumes = FindObjectsByType<Volume>(FindObjectsInactive.Include);
        foreach (var v in volumes)
        {
            if (v.profile != null && v.profile.TryGet<DepthOfField>(out var dof))
            {
                _postProcessingVolume = v;
                _depthOfField = dof;
                return;
            }
            if (v.sharedProfile != null && v.sharedProfile.TryGet<DepthOfField>(out var sdof))
            {
                _postProcessingVolume = v;
                _depthOfField = sdof;
                return;
            }
        }
    }

    private static readonly Vector3 ChurchDoor = new Vector3(19.8f, 2.2f, 28.5f);

    private Vector3 NewmanToChurchDir()
    {
        return Shot.Flat(ChurchDoor - GetNewmanHeadPosition());
    }

    /// <summary>
    /// ช็อต 1: Establishing - เครนต่ำเสยขึ้นสู่ซุ้มโบสถ์ผ่านหมอก แล้วตัดเป็นมุมหลังไหล่นิวแมนมองประตูโบสถ์ไกล ๆ
    /// </summary>
    private void PlayShot1Establishing()
    {
        PlayCutsceneAnimation(animLookAround);
        Vector3 head = GetNewmanHeadPosition();

        var crane = Shot.Move(
            new Vector3(10.5f, 1.25f, 20.0f), new Vector3(14.5f, 2.80f, 23.5f),
            new Vector3(23.5f, 6.50f, 28.5f), new Vector3(20.5f, 2.50f, 28.5f), 5.5f)
            .Lens(46f, 38f).Shake(0.2f).Dof(30f, 6f, 6f);
        crane.avoidWalls = false;

        // มุมต่ำหลังไหล่: นิวแมนเป็นเงาสลัวในเฟรมล่างซ้าย ประตูโบสถ์สว่างอยู่ไกลออกไป (Silhouette + Depth)
        var ots = Shot.OverShoulder(head, ChurchDoor + Vector3.up * 0.6f, 2.6f, 0.65f, -0.35f, 0.9f, 9f)
            .Lens(36f).Shake(0.25f).Dof(40f, 4f, 3f);

        PlayShots(camChurchWide, crane, ots);
    }

    /// <summary>ช็อต 1b: โปรไฟล์ด้านข้างนิวแมนอ่านโทรเลข - ภาพเงาแสงตะเกียงข้างแก้ม ดอลลี่เข้าช้า ๆ</summary>
    private void PlayShot1bTelegramProfile()
    {
        PlayCutsceneAnimation(animTalking);
        Vector3 head = GetNewmanHeadPosition();
        Vector3 f = NewmanToChurchDir();
        Vector3 r = Shot.Right(f);

        var profile = Shot.Move(
            head + r * 1.35f + f * 0.45f, head + r * 1.05f + f * 0.40f,
            head + Vector3.up * 0.02f, head + Vector3.up * 0.02f, 9f)
            .Lens(30f, 27f).Dutch(-2f, 0f).Shake(0.2f).Dof(75f, 2.2f, 1f);
        PlayShots(camNewmanClose, profile);
    }

    /// <summary>
    /// ช็อต 2: Hero Portrait 3/4 MCU - ดอลลี่อินช้า ๆ พร้อมโค้งอ้อมเล็กน้อย โฟกัสใบหน้าคมกริบ
    /// </summary>
    private void PlayShot2NewmanCloseUp()
    {
        PlayCutsceneAnimation(animTalking);

        Vector3 headPos = GetNewmanHeadPosition();
        Vector3 charForward = GetNewmanForward();
        if (charForward.sqrMagnitude < 0.01f) charForward = Vector3.forward;
        charForward = Shot.Flat(charForward);
        Vector3 charRight = Shot.Right(charForward);
        Vector3 eye = headPos + Vector3.up * 0.04f;

        var hero = Shot.Move(
            eye + charForward * 1.65f + charRight * 0.55f - Vector3.up * 0.10f,
            eye + charForward * 1.20f + charRight * 0.28f - Vector3.up * 0.02f,
            eye, eye + Vector3.up * 0.02f, 12f)
            .Lens(32f, 28f).Dutch(1.5f, 0f).Shake(0.15f).Dof(65f, 2.6f, 1.3f);
        PlayShots(camNewmanClose, hero);
    }

    /// <summary>ช็อต 2b: POV สายตานิวแมน มองถนนหมอกยามค่ำ แล้วก้มลงช้า ๆ ตามความรู้สึกถึงลูกชายที่ถูกทิ้ง</summary>
    private void PlayShot2bPovStreet()
    {
        Vector3 head = GetNewmanHeadPosition();
        Vector3 fwd = Shot.Flat(GetNewmanForward());
        Vector3 pos = head + fwd * 0.15f;

        var pov = Shot.Move(pos, pos + fwd * 0.25f,
            head + fwd * 7f + Vector3.up * 0.4f, head + fwd * 5f - Vector3.up * 1.1f, 9f)
            .Lens(52f, 46f).Shake(0.9f).Dof(35f, 2.8f, 2f);
        pov.avoidWalls = false;
        PlayShots(camAlleyTracking != null ? camAlleyTracking : camNewmanClose, pov);
    }

    /// <summary>ช็อต 2c: Extreme Close-Up ใบหน้า มุมเอียง (Dutch) เล็กน้อยให้อึดอัด ตอนตั้งคำถามหาฆาตกร</summary>
    private void PlayShot2cExtremeCloseUp()
    {
        PlayCutsceneAnimation(animTalking);
        Vector3 head = GetNewmanHeadPosition() + Vector3.up * 0.03f;
        Vector3 fwd = Shot.Flat(GetNewmanForward());

        var ecu = Shot.Face(head, fwd, 0.95f, 0.14f, 0.0f, 0.18f, 10f)
            .Lens(22f, 20f).Dutch(3.5f, 3.5f).Shake(0.15f).Dof(85f, 1.8f, 0.6f).Macro();
        PlayShots(camNewmanClose, ecu);
    }

    /// <summary>
    /// ช็อต 3: ทางเดินกลางโบสถ์ - Steadicam มุมต่ำตามทางเดินสู่แท่นบูชา
    /// </summary>
    private void PlayShot3CrimeSceneTracking()
    {
        PlayCutsceneAnimation(animLookAround);

        Vector3 altar = new Vector3(34.5f, 2.20f, 29.30f);
        var nave = Shot.Move(
            new Vector3(20.5f, 1.15f, 29.30f), new Vector3(28.0f, 1.45f, 29.30f),
            new Vector3(25.5f, 0.40f, 29.30f), altar, 8f)
            .Lens(38f, 34f).Dutch(-2.5f, 0f).Shake(0.35f).Dof(42f, 3.2f, -4.5f);
        nave.avoidWalls = false;
        PlayShots(camCrimeScene, nave);
    }

    /// <summary>ช็อต 3b: ถอยกล้องเครนขึ้นสูงเผยทั้งโบสถ์ - ห้าสิ่งที่ซ่อนอยู่ในที่เกิดเหตุ</summary>
    private void PlayShot3bNavePullBack()
    {
        Vector3 altar = new Vector3(34.5f, 2.20f, 29.30f);
        var pull = Shot.Move(
            new Vector3(28.0f, 1.45f, 29.30f), new Vector3(21.5f, 3.4f, 29.30f),
            altar, altar + Vector3.down * 0.6f, 10f)
            .Lens(34f, 42f).Dutch(0f, 2f).Shake(0.25f).Dof(35f, 5.6f, 0f);
        pull.avoidWalls = false;
        PlayShots(camCrimeScene, pull);
    }

    // ==========================================
    // ระบบช็อตกล้องภาพยนตร์ (ShotSpec) : ตัดช็อตต่อเนื่องในบทพูดเดียว มุมกล้องอิงตัวละครจริง
    // ==========================================
    private CinemachineCamera _activeShotCam;
    private readonly HashSet<CinemachineCamera> _usedShotCams = new HashSet<CinemachineCamera>();

    /// <summary>เล่นช็อตต่อเนื่องบนกล้องที่ระบุ (Hard Cut ระหว่างช็อต) ช็อตสุดท้ายค้างที่ปลายทางจนกว่าจะเปลี่ยนช็อต</summary>
    public void PlayShots(CinemachineCamera cam, params ShotSpec[] shots)
    {
        if (cam == null || shots == null || shots.Length == 0) return;
        SwitchToCutsceneShot(cam);
        _activeShotCam = cam;
        _usedShotCams.Add(cam);
        _cameraMotionCoroutine = StartCoroutine(ShotSequenceRoutine(cam, shots));
    }

    private static float EaseT(float t, ShotEase e)
    {
        switch (e)
        {
            case ShotEase.Linear: return t;
            case ShotEase.EaseOut: return 1f - (1f - t) * (1f - t);
            default: return t * t * t * (t * (t * 6f - 15f) + 10f);
        }
    }

    private static Vector3 KeepClearOfWalls(Vector3 aim, Vector3 pos, float minDist)
    {
        Vector3 d = pos - aim;
        float dist = d.magnitude;
        if (dist < 0.2f) return pos;
        Vector3 dir = d / dist;
        var hits = Physics.RaycastAll(aim, dir, dist, ~0, QueryTriggerInteraction.Ignore);
        float best = dist;
        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<Animator>() != null) continue;
            if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (h.collider.bounds.Contains(aim)) continue;
            if (h.distance < best) best = h.distance;
        }
        if (best < dist) return aim + dir * Mathf.Max(minDist, best - 0.15f);
        return pos;
    }

    private IEnumerator ShotSequenceRoutine(CinemachineCamera cam, ShotSpec[] shots)
    {
        EnsureDepthOfFieldReady();
        for (int i = 0; i < shots.Length; i++)
        {
            var s = shots[i];
            bool last = i == shots.Length - 1;

            CinematicCameraFeel.SetHandheld(cam, s.shake, 1.1f);
            if (_depthOfField != null)
            {
                _depthOfField.active = true;
                _depthOfField.mode.overrideState = true;
                _depthOfField.mode.value = DepthOfFieldMode.Bokeh;
                _depthOfField.focalLength.overrideState = true;
                _depthOfField.focalLength.value = s.focal;
                _depthOfField.aperture.overrideState = true;
                _depthOfField.aperture.value = s.aperture;
                _depthOfField.focusDistance.overrideState = true;
            }

            float timer = 0f;
            float dur = Mathf.Max(0.1f, s.duration);
            while (timer < dur || last)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / dur);
                float k = EaseT(t, s.ease);

                Vector3 aim = s.aim(k);
                Vector3 pos = s.pos(k);
                if (s.avoidWalls) pos = KeepClearOfWalls(aim, pos, s.minDistance);

                cam.transform.position = pos;
                Vector3 look = aim - pos;
                if (look.sqrMagnitude > 1e-6f) cam.transform.rotation = Quaternion.LookRotation(look, Vector3.up);

                float fovEnd = s.fovEnd > 0f ? s.fovEnd : s.fov;
                cam.Lens.FieldOfView = Mathf.Lerp(s.fov, fovEnd, k);
                float rollEnd = float.IsNaN(s.rollEnd) ? s.roll : s.rollEnd;
                cam.Lens.Dutch = Mathf.Lerp(s.roll, rollEnd, k);

                if (_depthOfField != null)
                {
                    float rackK = EaseT(Mathf.Clamp01(timer / Mathf.Max(0.1f, s.rackTime)), ShotEase.Smooth);
                    _depthOfField.focusDistance.value = Mathf.Max(0.2f, Vector3.Distance(pos, aim) + Mathf.Lerp(s.rackOffset, 0f, rackK));
                }

                if (last && t >= 1f) yield break; // ช็อตสุดท้าย: ค้างเฟรมสุดท้าย (รอบทพูดเปลี่ยนช็อต/จบคัตซีน)
                yield return null;
            }
        }
    }

    /// <summary>
    /// ขับเคลื่อนมุมกล้องพร้อมระบบติดตามจุดโฟกัสแบบไดนามิกเรียลไทม์ (Continuous Autofocus & Rack Focus Engine)
    /// รับประกันว่ากล้องจะเริ่มจากเบลอแล้วดึงโฟกัสมาชัดกริบ และคมชัดตลอดการเคลื่อนที่ ไม่มีเบลอค้างเด็ดขาด
    /// </summary>
    public void StartCameraMotionWithFocus(
        Transform camTransform,
        Vector3 startPos,
        Vector3 endPos,
        Quaternion startRot,
        Quaternion endRot,
        float duration,
        Vector3 focusTargetPos,
        float focalLength = 50f,
        float aperture = 2.8f,
        float rackFocusDuration = 1.0f,
        float startDefocusOffset = 2.0f,
        Transform dynamicFollowTarget = null)
    {
        if (_cameraMotionCoroutine != null)
        {
            StopCoroutine(_cameraMotionCoroutine);
        }
        _cameraMotionCoroutine = StartCoroutine(AnimateCameraMotionWithDynamicFocusRoutine(
            camTransform, startPos, endPos, startRot, endRot, duration,
            focusTargetPos, focalLength, aperture, rackFocusDuration, startDefocusOffset, dynamicFollowTarget));
    }

    private IEnumerator AnimateCameraMotionWithDynamicFocusRoutine(
        Transform camTransform,
        Vector3 startPos,
        Vector3 endPos,
        Quaternion startRot,
        Quaternion endRot,
        float duration,
        Vector3 focusTargetPos,
        float focalLength,
        float aperture,
        float rackFocusDuration,
        float startDefocusOffset,
        Transform dynamicFollowTarget)
    {
        if (camTransform == null) yield break;

        EnsureDepthOfFieldReady();
        if (_depthOfField != null)
        {
            _depthOfField.active = true;
            _depthOfField.mode.overrideState = true;
            _depthOfField.mode.value = DepthOfFieldMode.Bokeh;
            _depthOfField.focalLength.overrideState = true;
            _depthOfField.focalLength.value = focalLength;
            _depthOfField.aperture.overrideState = true;
            _depthOfField.aperture.value = aperture;
            _depthOfField.focusDistance.overrideState = true;
        }

        float timer = 0f;
        camTransform.position = startPos;
        camTransform.rotation = startRot;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            camTransform.position = Vector3.Lerp(startPos, endPos, smoothT);
            camTransform.rotation = Quaternion.Slerp(startRot, endRot, smoothT);

            // คำนวณระยะโฟกัสจริงสู่เป้าหมายแบบเรียลไทม์ทุกเฟรม (Continuous Realtime Autofocus Tracking)
            Vector3 target = dynamicFollowTarget != null ? dynamicFollowTarget.position : focusTargetPos;
            float trueDist = Vector3.Distance(camTransform.position, target);

            // เอฟเฟกต์ Rack Focus / Focus Pull: เริ่มจากเบลอแล้วดึงโฟกัสเข้ามาคมชัดกริบ
            if (_depthOfField != null)
            {
                float rackT = Mathf.Clamp01(timer / Mathf.Max(0.1f, rackFocusDuration));
                float smoothRack = Mathf.SmoothStep(0f, 1f, rackT);
                float defocus = Mathf.Lerp(startDefocusOffset, 0f, smoothRack);
                _depthOfField.focusDistance.value = Mathf.Max(0.2f, trueDist + defocus);
            }

            yield return null;
        }

        camTransform.position = endPos;
        camTransform.rotation = endRot;

        // สิ้นสุดการเคลื่อนที่: ล็อคโฟกัสคมชัดที่เป้าหมาย 100% ไม่เบลอค้าง
        if (_depthOfField != null)
        {
            Vector3 finalTarget = dynamicFollowTarget != null ? dynamicFollowTarget.position : focusTargetPos;
            _depthOfField.focusDistance.value = Mathf.Max(0.2f, Vector3.Distance(camTransform.position, finalTarget));
        }
    }

    /// <summary>
    /// ปรับแต่งจุดโฟกัสเลนส์กล้องภาพยนตร์ (Depth of Field Bokeh Optical Focus Point)
    /// </summary>
    public void SetDepthOfFieldLensFocus(float focusDist, float focalLen = 50f, float aperture = 2.8f)
    {
        EnsureDepthOfFieldReady();
        if (_depthOfField != null)
        {
            _depthOfField.active = true;
            _depthOfField.mode.overrideState = true;
            _depthOfField.mode.value = DepthOfFieldMode.Bokeh;
            _depthOfField.focusDistance.overrideState = true;
            _depthOfField.focusDistance.value = Mathf.Max(0.1f, focusDist);
            _depthOfField.focalLength.overrideState = true;
            _depthOfField.focalLength.value = focalLen;
            _depthOfField.aperture.overrideState = true;
            _depthOfField.aperture.value = aperture;
        }
    }

    /// <summary>
    /// คัตซีนเปิดตัว Scene 2 (ตรอกไวท์ชาเปล) ระดับภาพยนตร์
    /// พร้อมจุดโฟกัสเลนส์กล้อง (Bokeh Depth of Field Lens Focus) และการเคลื่อนไหวมุมกล้องลื่นไหล 3 ช็อต
    /// </summary>
    public void PlayScene2IntroCinematic(Transform corpseTarget, Transform newspaperTarget, Action onFinished = null)
    {
        StartCoroutine(Scene2IntroCinematicRoutine(corpseTarget, newspaperTarget, onFinished));
    }

    private IEnumerator Scene2IntroCinematicRoutine(Transform corpseTarget, Transform newspaperTarget, Action onFinished)
    {
        SetPlayerControlsLocked(true);

        if (LondonAlleyQuestManager.Instance != null)
        {
            LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
        }

        if (WaypointNavigationSystem.Instance != null)
        {
            WaypointNavigationSystem.Instance.ClearTarget();
            WaypointNavigationSystem.Instance.SetVisible(false);
        }

        Vector3 corpsePos = corpseTarget != null ? corpseTarget.position : new Vector3(-18.5f, 0.28f, -102.2f);
        Vector3 newsPos = newspaperTarget != null ? newspaperTarget.position : new Vector3(-16.80f, 0.24f, -104.16f);

        if (CinematicLetterbox.Instance != null)
        {
            // บทคัตซีนเปิดบทที่ 2: ปมใหม่ "ศพที่สองถูกจัดวางอย่างประณีต" บอกใบ้ว่ามีบุคคลที่สามอยู่เบื้องหลัง
            const string newman = "สารวัตรนิวแมน";
            var cutsceneDialogues = new List<CinematicLetterbox.DialogueEntry>()
            {
                // Shot 1: ติดตามหมอกในตรอกไวท์ชาเปล
                new CinematicLetterbox.DialogueEntry(newman,
                    "ไวท์ชาเปลหลังเที่ยงคืน หมอกหนาจนโคมแก๊สเหลือเป็นแค่จุดเหลืองจาง ๆ และมีกลิ่นเหล็กลอยมากับสายลม",
                    () => PlayScene2Shot1Tracking(corpsePos)),

                // Shot 2: โคลสอัพร่างเหยื่อ
                new CinematicLetterbox.DialogueEntry(newman,
                    "ศพที่สอง ในคืนเดียวกัน ห่างจากโบสถ์ไม่ถึงหนึ่งไมล์ แจ็คไม่เคยลงมือเร็วขนาดนี้มาก่อน",
                    () => PlayScene2Shot2CorpseClose(corpsePos)),
                new CinematicLetterbox.DialogueEntry(newman,
                    "แต่ของทุกชิ้นรอบตัวเธอถูกวางเรียงอย่างกับโต๊ะอาหาร นี่ไม่ใช่ฝีมือคนที่ฆ่าด้วยอารมณ์"),

                // Shot 3: มาโครหนังสือพิมพ์
                new CinematicLetterbox.DialogueEntry(newman,
                    "และนี่... หนังสือพิมพ์ฉบับวันนี้ พับเรียบร้อยวางไว้ข้างมือเธอ ใต้ตัวอักษรบางตัวมีรอยหมึกสีแดง",
                    () => PlayScene2Shot3NewspaperMacro(newsPos)),
                new CinematicLetterbox.DialogueEntry(newman,
                    "ใช้ CiRA CORE ตรวจทุกชิ้นให้ครบก่อน แล้วค่อยดูว่ามันอยากบอกอะไรเรา",
                    () => PlayScene2Shot4NewmanResolve())
            };

            CinematicLetterbox.Instance.PlayDialogueSequence(cutsceneDialogues, () =>
            {
                ResetAllCutsceneCameras();
                CinematicLetterbox.Instance.HideLetterbox();
                SetPlayerControlsLocked(false);

                if (LondonAlleyQuestManager.Instance != null)
                {
                    LondonAlleyQuestManager.Instance.ShowObjectiveHUD(true);
                }

                onFinished?.Invoke();
            });
        }
        else
        {
            SetPlayerControlsLocked(false);
            if (LondonAlleyQuestManager.Instance != null)
            {
                LondonAlleyQuestManager.Instance.ShowObjectiveHUD(true);
            }
            onFinished?.Invoke();
        }

        yield return null;
    }

    /// <summary>
    /// ช็อต 1 (Scene 2): ตรอกไวท์ชาเปล - เครนต่ำไหลผ่านหมอกเข้าหาที่เกิดเหตุ แล้วตัดเป็นมุมหลังไหล่นิวแมนมองศพ
    /// </summary>
    private void PlayScene2Shot1Tracking(Vector3 corpsePos)
    {
        PlayCutsceneAnimation(animLookAround);
        Vector3 aimTarget = corpsePos + Vector3.up * 0.45f;
        Vector3 head = GetNewmanHeadPosition();

        var track = Shot.Move(
            new Vector3(-11.5f, 2.5f, -101.5f), new Vector3(-13.8f, 2.15f, -101.7f),
            aimTarget, 5.5f)
            .Lens(36f, 31f).Shake(0.3f).Dof(38f, 2.8f, 3.5f);
        track.avoidWalls = false;

        var ots = Shot.OverShoulder(head, aimTarget, 1.5f, 0.55f, -0.15f, 0.6f, 9f)
            .Lens(32f).Shake(0.25f).Dof(50f, 2.8f, 1.5f);

        PlayShots(camAlleyTracking, track, ots);
    }

    /// <summary>
    /// ช็อต 2 (Scene 2): เครนลงจากมุมสูงเห็นภาพรวมการจัดวางร่าง แล้วโคจรรอบศพช้า ๆ แบบนิติเวช
    /// </summary>
    private void PlayScene2Shot2CorpseClose(Vector3 corpsePos)
    {
        PlayCutsceneAnimation(animTalking);

        Vector3 aim = corpsePos + Vector3.up * 0.2f;
        var craneDown = Shot.Move(
            corpsePos + new Vector3(0.8f, 3.4f, 1.2f), corpsePos + new Vector3(1.0f, 1.9f, 1.2f),
            corpsePos + Vector3.up * 0.1f, 4.5f)
            .Lens(42f, 36f).Dutch(5f, 0f).Shake(0.2f).Dof(45f, 4f, 2f);

        float a0 = Shot.AngleTo(corpsePos, corpsePos + new Vector3(1.0f, 0f, 1.2f));
        var orbit = Shot.Orbit(corpsePos, 1.7f, 1.05f, 0.75f, a0, a0 + 55f, aim, 12f)
            .Lens(32f, 28f).Shake(0.2f).Dof(70f, 2.2f, 1.2f);

        PlayShots(camAlleyCorpseClose, craneDown, orbit);
    }

    /// <summary>
    /// ช็อต 3 (Scene 2): มาโครหนังสือพิมพ์เปื้อนหมึกแดง ดอลลี่ลงมาแล้วเลื่อนข้ามหน้ากระดาษ
    /// </summary>
    private void PlayScene2Shot3NewspaperMacro(Vector3 newsPos)
    {
        Vector3 paperTarget = newsPos + Vector3.up * 0.05f;
        var macro = Shot.Move(
            newsPos + new Vector3(0.05f, 0.72f, 0.42f), newsPos + new Vector3(-0.05f, 0.40f, 0.20f),
            paperTarget + new Vector3(0.05f, 0f, 0f), paperTarget + new Vector3(-0.06f, 0f, 0f), 12f)
            .Lens(26f, 22f).Dutch(-3f, 0f).Shake(0.1f).Dof(90f, 2.0f, 0.35f).Macro();
        macro.avoidWalls = false;
        PlayShots(camAlleyNewspaperMacro, macro);
    }

    /// <summary>ช็อต 4 (Scene 2): นิวแมนตัดสินใจ - โคลสอัพ 3/4 หน้านิวแมน ดอลลี่เข้า</summary>
    private void PlayScene2Shot4NewmanResolve()
    {
        PlayCutsceneAnimation(animTalking);
        Vector3 head = GetNewmanHeadPosition();
        Vector3 fwd = Shot.Flat(GetNewmanForward());

        var face = Shot.Face(head, fwd, 1.5f, 0.45f, -0.05f, 0.3f, 9f)
            .Lens(30f, 27f).Dutch(0f, 0f).Shake(0.15f).Dof(65f, 2.4f, 1f);
        PlayShots(camAlleyTracking, face);
    }

    // ===== คัตซีนเฉลยรหัส (Scene 2 Reveal) =====
    private void PlayScene2RevealShotCorpse(Vector3 corpsePos)
    {
        PlayCutsceneAnimation(animTalking);
        var push = Shot.Move(
            corpsePos + new Vector3(1.3f, 1.1f, 1.0f), corpsePos + new Vector3(0.8f, 0.6f, 0.6f),
            corpsePos + Vector3.up * 0.15f, 8f)
            .Lens(28f, 24f).Shake(0.15f).Dof(70f, 2.2f, 0.8f);
        PlayShots(camAlleyCorpseClose, push);
    }

    private void PlayScene2RevealShotMessage()
    {
        PlayCutsceneAnimation(animLookAround);
        Vector3 head = GetNewmanHeadPosition() + Vector3.up * 0.03f;
        Vector3 fwd = Shot.Flat(GetNewmanForward());
        var ecu = Shot.Face(head, fwd, 1.0f, 0.2f, -0.02f, 0.25f, 10f)
            .Lens(24f, 21f).Dutch(6f, 6f).Shake(0.35f).Dof(85f, 1.8f, 0.6f).Macro();
        PlayShots(camAlleyTracking, ecu);
    }

    private void PlayScene2RevealShotTrapPullback(Vector3 corpsePos)
    {
        var pull = Shot.Move(
            corpsePos + new Vector3(3.5f, 1.6f, 3.0f), corpsePos + new Vector3(5.5f, 4.8f, 4.4f),
            corpsePos + Vector3.up * 0.5f, 10f)
            .Lens(34f, 44f).Dutch(0f, -3f).Shake(0.2f).Dof(35f, 5.6f, 0f);
        PlayShots(camAlleyCorpseClose, pull);
    }

    private void PlayScene2RevealShotHero()
    {
        PlayCutsceneAnimation(animTalking);
        Vector3 head = GetNewmanHeadPosition();
        Vector3 fwd = Shot.Flat(GetNewmanForward());
        // มุมต่ำเสยขึ้น (Low-Angle Hero) ค่อย ๆ เครนขึ้น ให้ดูหนักแน่นแต่โดดเดี่ยว
        var hero = Shot.Move(
            head + fwd * 2.1f - Vector3.up * 1.05f, head + fwd * 1.8f - Vector3.up * 0.55f,
            head + Vector3.up * 0.15f, head + Vector3.up * 0.1f, 9f)
            .Lens(30f, 27f).Dutch(-2f, 0f).Shake(0.2f).Dof(60f, 2.6f, 1f);
        PlayShots(camAlleyNewspaperMacro, hero);
    }

    /// <summary>
    /// คัตซีนเปิดโปงสาส์นลับจาก มอริอาร์ตี้ (Professor Moriarty Reveal Cinematic)
    /// เมื่อผู้เล่นถอดรหัสซีซาร์บนหนังสือพิมพ์สำเร็จได้คำว่า "THEATER"
    /// </summary>
    public void PlayScene2MoriartyRevealCinematic(Transform corpseTarget, Action onFinished = null)
    {
        StartCoroutine(Scene2MoriartyRevealRoutine(corpseTarget, onFinished));
    }

    private IEnumerator Scene2MoriartyRevealRoutine(Transform corpseTarget, Action onFinished)
    {
        SetPlayerControlsLocked(true);

        if (LondonAlleyQuestManager.Instance != null)
        {
            LondonAlleyQuestManager.Instance.ShowObjectiveHUD(false);
        }

        Vector3 corpsePos = corpseTarget != null ? corpseTarget.position : new Vector3(-18.5f, 0.28f, -102.2f);

        if (CinematicLetterbox.Instance != null)
        {
            // บทคัตซีนเฉลยรหัส: นิวแมนอ่านคำที่ถอดได้ -> ข้อความลับของมอริอาร์ตี้ -> นิวแมนวิเคราะห์ทางเลือกที่ไม่มี
            var cutsceneDialogues = new List<CinematicLetterbox.DialogueEntry>()
            {
                new CinematicLetterbox.DialogueEntry(
                    "สารวัตรนิวแมน",
                    "T... H... E... A... T... E... R โรงละคร และมีข้อความสั้น ๆ เขียนไว้ใต้ตัวอักษรชุดสุดท้าย",
                    () => PlayScene2RevealShotCorpse(corpsePos)
                ),

                new CinematicLetterbox.DialogueEntry(
                    "ศาสตราจารย์ มอริอาร์ตี้ (สาส์นลับ)",
                    "\"ขอบคุณที่ตามรอยมาถึงที่นี่ สารวัตร แจ็คเป็นเพียงมือของฉัน ส่วนเวทีต่อไปฉันเตรียมไว้ให้คุณแล้ว ม่านจะเปิดตอนเที่ยงคืน\"",
                    () => PlayScene2RevealShotMessage()
                ),

                new CinematicLetterbox.DialogueEntry(
                    "สารวัตรนิวแมน",
                    "มอริอาร์ตี้ เขาไม่ได้ซ่อนตัว เขากำลังเชิญเรา ถ้าไม่ไป เหยื่อรายต่อไปต้องตาย แต่ถ้าไป ก็คือเดินเข้ากับดักของเขา",
                    () => PlayScene2RevealShotTrapPullback(corpsePos)
                ),

                new CinematicLetterbox.DialogueEntry(
                    "สารวัตรนิวแมน",
                    "ไม่มีทางเลือกอื่น... ไปโรงละครรอยัลไลเซียม",
                    () => PlayScene2RevealShotHero()
                )
            };

            CinematicLetterbox.Instance.PlayDialogueSequence(cutsceneDialogues, () =>
            {
                ResetAllCutsceneCameras();
                CinematicLetterbox.Instance.HideLetterbox();

                // ไม่ปลดล็อกผู้เล่นและไม่คืน HUD ที่นี่ เพราะต่อจากนี้เป็นหน้าสรุปบทที่ 2 (ผู้จัดการเควสเป็นผู้ปลดล็อกเอง)
                onFinished?.Invoke();
            });
        }
        else
        {
            onFinished?.Invoke();
        }

        yield return null;
    }

    /// <summary>
    /// คัตซีนภาพยนตร์ระดับเทพ Scene 3: เฉียดฉิว (The Royal Lyceum Theatre - The Narrow Escape)
    /// ผู้เล่นมาถึงก่อนที่ Jack จะฆ่าเหยื่อสำเร็จ แต่ Jack ไหวตัวทัน ทิ้งเหยื่อและตั๋วรถไฟฉีกขาดแล้วหลบหนีไปได้
    /// </summary>
    public void PlayScene3TheaterNarrowEscapeCinematic(Transform victimTarget, Transform jackTarget, Transform ticketTarget, Action onFinished = null)
    {
        StartCoroutine(Scene3TheaterNarrowEscapeRoutine(victimTarget, jackTarget, ticketTarget, onFinished));
    }

    private IEnumerator Scene3TheaterNarrowEscapeRoutine(Transform victimTarget, Transform jackTarget, Transform ticketTarget, Action onFinished)
    {
        SetPlayerControlsLocked(true);

        if (RoyalLyceumTheaterQuestManager.Instance != null)
        {
            RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(false);
        }

        // ให้แน่ใจว่าตัวละคร Jack และเหยื่อแสดงตัวอย่างชัดเจน
        if (jackTarget != null) jackTarget.gameObject.SetActive(true);
        if (victimTarget != null) victimTarget.gameObject.SetActive(true);
        if (ticketTarget != null) ticketTarget.gameObject.SetActive(true);

        // เปิดระบบควบคุมท่าบีบคอลอยขึ้นในมือแจ็ค
        JackTheaterChokeAndThrowSequence chokeSeq = null;
        if (jackTarget != null)
        {
            chokeSeq = jackTarget.GetComponent<JackTheaterChokeAndThrowSequence>();
            if (chokeSeq == null) chokeSeq = jackTarget.gameObject.AddComponent<JackTheaterChokeAndThrowSequence>();
            chokeSeq.holdUntilThrown = true; // ค้างบีบคอจนกว่าจะถึงจังหวะโยนในบทพูด
            chokeSeq.Init(victimTarget);
        }

        // กล้องสั่นแบบถือด้วยมือ (Handheld) ทุกช็อตของฉากนี้ ให้รู้สึกเหมือนกล้องวิ่งตามเหตุการณ์จริง
        ApplyScene3CameraFeel();

        if (CinematicLetterbox.Instance != null)
        {
            var cutsceneDialogues = new List<CinematicLetterbox.DialogueEntry>()
            {
                // Shot 1: Newman dashes down the street toward the Royal Lyceum Theatre
                new CinematicLetterbox.DialogueEntry(
                    "<color=#3498DB>สารวัตรนิวแมน</color>",
                    "\"แจ็ค! ปล่อยเธอเดี๋ยวนี้!\"",
                    () => {
                        PlayScene3ShotBreach(victimTarget, jackTarget);
                    }
                ),

                // Shot 2: Jack in the act of attacking the victim on the steps!
                new CinematicLetterbox.DialogueEntry(
                    "<color=#FF6B6B>เลดี้โรซามันด์</color>",
                    "\"สารวัตร... ช่วยด้วย... เขาบีบแน่นเหลือเกิน... หายใจไม่ออก...\"",
                    () => {
                        PlayScene3ShotVictimCU(victimTarget);
                        ShakeCamera(camTheaterJackConfrontation, 0.45f, 0.16f);
                    }
                ),

                // Shot 3: Jack is startled / realizes Newman is here (ไหวตัวทัน & กัดฟันขู่)
                new CinematicLetterbox.DialogueEntry(
                    "<color=#E74C3C>แจ็ค เดอะ ริปเปอร์</color>",
                    "\"มาเร็วกว่าที่เขาบอกไว้เสียอีก... ก็ได้ ฉันแค่ต้องถ่วงเวลาให้ครบเท่านี้ ลาก่อนสารวัตร\"",
                    () => {
                        PlayScene3ShotJackLow(jackTarget);
                        ShakeCamera(camTheaterJackConfrontation, 0.25f, 0.05f);
                    }
                ),

                // Shot 4: CUT TO INSPECTOR NEWMAN REACTION SHOT as Jack throws smoke & flees! (สลับกล้องมาที่ตัวผู้เล่นทันที)
                new CinematicLetterbox.DialogueEntry(
                    "<color=#3498DB>สารวัตรนิวแมน</color>",
                    "\"เขาบอกไว้? ใครกัน... หยุดนะ! ...ควันดำ! มันหายไปแล้ว!\"",
                    () => {
                        // สลับมุมกล้องมาที่สารวัตรนิวแมน (เรา) ทันทีตามที่ผู้เล่นต้องการ!
                        PlayScene3ShotNewmanReaction(jackTarget);

                        // Jack โยนเหยื่อลงพื้น แล้วอันตรธานหายไปในม่านหมอกควันทันทีขณะที่กล้องจับภาพนิวแมน (ไม่วิ่งทะลุกำแพงบนจอ!)
                        if (chokeSeq != null)
                        {
                            Vector3 throwTarget = new Vector3(29.8f, 0.24f, -87.8f);
                            chokeSeq.ThrowVictim(
                                throwTarget,
                                null,
                                () =>
                                {
                                    // ปล่อยมือ: กล้องสะเทือนแรงทันที และแจ็คหายตัวไปในควันดำก่อนที่ร่างเหยื่อจะตกถึงพื้น
                                    ShakeCamera(camTheaterNewmanReaction != null ? camTheaterNewmanReaction : camTheaterWideBreach, 0.6f, 0.22f);
                                    StartCoroutine(JackSmokeExitRoutine(jackTarget));
                                });
                        }
                        else if (jackTarget != null)
                        {
                            jackTarget.gameObject.SetActive(false);
                        }
                    }
                ),

                // Shot 5: Dropped Torn Ticket Macro Shot (เจาะจงตั๋วรถไฟที่ฉีกขาดตกอยู่บนขั้นบันได)
                new CinematicLetterbox.DialogueEntry(
                    "<color=#F39C12>บันทึกเหตุการณ์</color>",
                    "ควันดำจางลง แจ็คหายไปในเงามืด บนขั้นบันไดข้างเหยื่อ เหลือเพียงตั๋วรถไฟฉีกครึ่งหนึ่งใบเดียวที่ร่วงอยู่",
                    () => {
                        PlayScene3ShotTicketMacro(ticketTarget, jackTarget);
                    }
                ),

                // Shot 6: Rescuing the Surviving Victim in Front of the Theater
                new CinematicLetterbox.DialogueEntry(
                    "เลดี้โรซามันด์",
                    "\"ขอบคุณพระเจ้าที่คุณมาทัน... เขาไม่พูดอะไรเลยจนกระทั่งเห็นคุณ แล้วก็พึมพำคำเดียวว่า 'เที่ยวสุดท้าย'\"",
                    () => {
                        PlayScene3ShotRescueTwoShot(victimTarget);
                    }
                ),

                // Shot 7: Newman's Determination to Investigate
                new CinematicLetterbox.DialogueEntry(
                    "สารวัตรนิวแมน",
                    "\"เที่ยวสุดท้าย... ตั๋วใบนั้นเป็นเวลานัดของเขา แต่ก่อนจะตามไป ต้องตรวจทุกอย่างที่เขาทิ้งไว้ในโรงละครก่อนที่ร่องรอยจะเลือนหาย\"",
                    () => {
                        PlayScene3ShotNewmanResolve(jackTarget);
                    }
                )
            };

            CinematicLetterbox.Instance.PlayDialogueSequence(cutsceneDialogues, () =>
            {
                ClearScene3CameraFeel();
                ResetAllCutsceneCameras();
                CinematicLetterbox.Instance.HideLetterbox();
                SetPlayerControlsLocked(false);
                if (Time.timeScale < 1f) Time.timeScale = 1f;

                if (RoyalLyceumTheaterQuestManager.Instance != null)
                {
                    RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(true);
                }

                onFinished?.Invoke();
            });
        }
        else
        {
            if (jackTarget != null) jackTarget.gameObject.SetActive(false);
            SetPlayerControlsLocked(false);
            if (RoyalLyceumTheaterQuestManager.Instance != null)
            {
                RoyalLyceumTheaterQuestManager.Instance.ShowObjectiveHUD(true);
            }
            onFinished?.Invoke();
        }

        yield return null;
    }

    // ==========================================
    // ช็อต Scene 3 (อิงตำแหน่งตัวละครจริง: นิวแมน แจ็ค เหยื่อ ตั๋ว)
    // ==========================================
    private CinemachineCamera Scene3ReactionCam => camTheaterNewmanReaction != null ? camTheaterNewmanReaction : camTheaterWideBreach;

    /// <summary>L1: ตามหลังนิวแมนวิ่งพุ่งเข้าหา (กล้องแรงสั่น) แล้วตัดเป็นมุมต่ำโคจรรอบแจ็คกับเหยื่อ</summary>
    private void PlayScene3ShotBreach(Transform victim, Transform jack)
    {
        Vector3 nh = GetNewmanHeadPosition();
        Vector3 jh = Shot.Head(jack);
        Vector3 vh = Shot.Head(victim);
        Vector3 a = Shot.Flat(jh - nh);
        Vector3 r = Shot.Right(a);

        var run = Shot.Move(
            nh - a * 2.6f + r * 0.55f - Vector3.up * 0.55f, nh + a * 0.2f + r * 0.45f - Vector3.up * 0.45f,
            jh + Vector3.up * 0.1f, 2.6f)
            .Lens(54f, 46f).Shake(1.4f).Dof(35f, 4f, 3f).Eased(ShotEase.EaseOut);

        Vector3 mid = (jh + vh) * 0.5f;
        float dist = Vector3.Distance(nh, mid);
        float a0 = Shot.AngleTo(mid, nh) + 65f;
        var orbit = Shot.Orbit(mid, Mathf.Max(5.5f, dist * 0.9f), 0.6f - mid.y + nh.y - 1.6f, 0.3f - mid.y + nh.y - 1.6f, a0, a0 + 25f, mid + Vector3.up * 0.2f, 9f)
            .Lens(34f).Shake(0.7f).Dof(40f, 3.2f, 3f);

        PlayShots(camTheaterWideBreach, run, orbit);
    }

    /// <summary>L2: โคลสอัพใบหน้าเหยื่อที่ถูกบีบ - ดอลลี่เข้าช้า ๆ อึดอัด มุมเอียงเล็กน้อย</summary>
    private void PlayScene3ShotVictimCU(Transform victim)
    {
        Vector3 nh = GetNewmanHeadPosition();
        Vector3 vh = Shot.Head(victim);
        var cu = Shot.Face(vh, nh - vh, 1.55f, 0.35f, -0.05f, 0.4f, 9f)
            .Lens(28f, 25f).Dutch(5f, 2f).Shake(1.0f).Dof(80f, 1.4f, 0.8f);
        PlayShots(camTheaterJackConfrontation, cu);
    }

    /// <summary>L3: มุมต่ำเสยหน้าแจ็ค (Villain Low-Angle) เลนส์แคบ เอียง Dutch ตรงข้าม</summary>
    private void PlayScene3ShotJackLow(Transform jack)
    {
        Vector3 nh = GetNewmanHeadPosition();
        Vector3 jh = Shot.Head(jack);
        var low = Shot.Face(jh, nh - jh, 1.8f, -0.35f, -0.45f, 0.4f, 8f)
            .Lens(26f, 24f).Dutch(-6f, -3f).Shake(0.5f).Dof(80f, 1.4f, 0.8f);
        PlayShots(camTheaterJackConfrontation, low);
    }

    /// <summary>L4: รีแอคชันนิวแมนใกล้ ๆ แล้วถอยเป็นมุมกว้างหลังนิวแมน เห็นควันดำที่แจ็คอันตรธาน</summary>
    private void PlayScene3ShotNewmanReaction(Transform jack)
    {
        PlayCutsceneAnimation(animLookAround);
        Vector3 nh = GetNewmanHeadPosition();
        Vector3 jh = Shot.Head(jack);
        Vector3 a = Shot.Flat(jh - nh);
        Vector3 r = Shot.Right(a);

        var cu = Shot.Face(nh, a, 1.15f, 0.2f, 0f, 0.1f, 1.6f)
            .Lens(28f).Shake(1.6f).Dof(65f, 1.8f, 0.5f);
        var wide = Shot.Move(
            nh - a * 2.4f + r * 0.9f - Vector3.up * 0.4f, nh - a * 1.9f + r * 0.7f - Vector3.up * 0.3f,
            jh + Vector3.up * 0.2f, 8f)
            .Lens(40f).Shake(1.2f).Dof(40f, 2.8f, 2f);
        PlayShots(Scene3ReactionCam, cu, wide);
    }

    /// <summary>L5: มาโครตั๋วที่ฉีกขาด ดอลลี่ลงช้า ๆ ใกล้ตั๋ว</summary>
    private void PlayScene3ShotTicketMacro(Transform ticket, Transform jack)
    {
        Vector3 t = (ticket != null ? ticket.position : GetNewmanHeadPosition() - Vector3.up * 1.5f) + Vector3.up * 0.03f;
        Vector3 a = jack != null ? Shot.Flat(Shot.Head(jack) - GetNewmanHeadPosition()) : Vector3.forward;
        Vector3 r = Shot.Right(a);

        var macro = Shot.Move(
            t + Vector3.up * 0.6f + r * 0.2f - a * 0.25f, t + Vector3.up * 0.3f + r * 0.08f - a * 0.12f,
            t, 8f)
            .Lens(24f, 20f).Dutch(-4f, 0f).Shake(0.3f).Dof(90f, 1.6f, 0.3f).Macro();
        macro.avoidWalls = false;
        PlayShots(camTheaterTicketMacro, macro);
    }

    /// <summary>L6: Two-Shot นิวแมนกับเหยื่อที่รอด โคจรช้า ๆ</summary>
    private void PlayScene3ShotRescueTwoShot(Transform victim)
    {
        PlayCutsceneAnimation(animTalking);
        Vector3 nh = GetNewmanHeadPosition();
        Vector3 vp = victim != null ? victim.position + Vector3.up * 0.6f : nh - Vector3.up * 0.8f;
        Vector3 mid = (nh + vp) * 0.5f;
        float a0 = Shot.AngleTo(mid, nh) + 90f;
        var two = Shot.Orbit(mid, 2.7f, 0.1f, 0.2f, a0, a0 + 16f, mid, 10f)
            .Lens(34f, 31f).Shake(0.35f).Dof(50f, 2.4f, 1f);
        PlayShots(camTheaterVictimRescue, two);
    }

    /// <summary>L7: Hero Shot นิวแมน มุมต่ำ เครนขึ้นช้า ๆ ด้วยแววตามุ่งมั่น</summary>
    private void PlayScene3ShotNewmanResolve(Transform jack)
    {
        PlayCutsceneAnimation(animLookAround);
        Vector3 nh = GetNewmanHeadPosition();
        Vector3 a = jack != null ? Shot.Flat(Shot.Head(jack) - nh) : Shot.Flat(GetNewmanForward());
        var hero = Shot.Move(
            nh + a * 2.2f - Vector3.up * 0.9f, nh + a * 1.7f - Vector3.up * 0.4f,
            nh + Vector3.up * 0.12f, nh + Vector3.up * 0.08f, 9f)
            .Lens(32f, 28f).Dutch(-2f, 0f).Shake(0.25f).Dof(60f, 2.4f, 1f);
        PlayShots(camTheaterWideBreach, hero);
    }

    // ==========================================
    // ฟีลกล้องและเอฟเฟกต์ของคัตซีน Scene 3
    // ==========================================
    private void ApplyScene3CameraFeel()
    {
        CinematicCameraFeel.SetHandheld(camTheaterWideBreach, 0.6f, 1.1f);
        CinematicCameraFeel.SetHandheld(camTheaterJackConfrontation, 0.9f, 1.15f);
        CinematicCameraFeel.SetHandheld(camTheaterNewmanReaction, 1.1f, 1.25f);
        CinematicCameraFeel.SetHandheld(camTheaterTicketMacro, 0.35f, 1f);
        CinematicCameraFeel.SetHandheld(camTheaterVictimRescue, 0.5f, 1f);
    }

    private void ClearScene3CameraFeel()
    {
        CinematicCameraFeel.Clear(camTheaterWideBreach);
        CinematicCameraFeel.Clear(camTheaterJackConfrontation);
        CinematicCameraFeel.Clear(camTheaterNewmanReaction);
        CinematicCameraFeel.Clear(camTheaterTicketMacro);
        CinematicCameraFeel.Clear(camTheaterVictimRescue);
    }

    /// <summary>ดอลลี่กล้องไปข้างหน้าตามแนวมองของกล้อง (ใช้เวลาจริง ไม่ถูกสโลว์โมชันหน่วง)</summary>
    public void PushIn(Transform camTransform, float meters, float duration)
    {
        if (camTransform != null) StartCoroutine(PushInRoutine(camTransform, meters, duration));
    }

    private IEnumerator PushInRoutine(Transform camTransform, float meters, float duration)
    {
        Vector3 start = camTransform.position;
        Vector3 dir = camTransform.forward;
        float elapsed = 0f;
        while (elapsed < duration && camTransform != null)
        {
            elapsed += Time.unscaledDeltaTime;
            camTransform.position = start + dir * meters * Mathf.SmoothStep(0f, 1f, elapsed / duration);
            yield return null;
        }
    }

    /// <summary>แจ็คหายตัวด้วยควันดำระเบิด: ควันพุ่งออกรอบตัว แล้วโมเดลหายไปตอนควันบังจนมิด</summary>
    private IEnumerator JackSmokeExitRoutine(Transform jackTarget)
    {
        if (jackTarget == null) yield break;

        float t = 0f;
        while (t < 0.22f) { t += Time.unscaledDeltaTime; yield return null; }

        CutsceneFX.SmokeBurst(jackTarget.position + Vector3.up * 1.1f, 1.25f);

        t = 0f;
        while (t < 0.16f) { t += Time.unscaledDeltaTime; yield return null; }

        CutsceneFX.SmokeBurst(jackTarget.position + Vector3.up * 0.6f, 1.0f);
        if (jackTarget != null) jackTarget.gameObject.SetActive(false);
    }

    private IEnumerator JackEscapeMovementRoutine(Transform jackTarget)
    {
        if (jackTarget == null) yield break;

        Vector3 startPos = jackTarget.position;
        Vector3 escapeTarget = startPos + new Vector3(8.0f, 0f, 3.5f); // วิ่งหลบหนีเลี้ยวเข้าตรอกมืด
        Quaternion escapeRot = Quaternion.LookRotation(new Vector3(1f, 0f, 0.4f));

        float duration = 1.8f;
        float elapsed = 0f;

        Animator anim = jackTarget.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.speed = 1.4f;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (jackTarget != null)
            {
                jackTarget.position = Vector3.Lerp(startPos, escapeTarget, t);
                jackTarget.rotation = Quaternion.Slerp(jackTarget.rotation, escapeRot, Time.deltaTime * 10f);
            }
            yield return null;
        }

        if (jackTarget != null)
        {
            jackTarget.gameObject.SetActive(false); // เลี้ยวเข้าเงามืดแล้วจึงหายไป
        }
    }

    /// <summary>
    /// สั่นกล้องแบบภาพยนตร์ (Cinematic Camera Shake)
    /// </summary>
    public void ShakeCamera(CinemachineCamera cam, float duration = 0.35f, float intensity = 0.12f)
    {
        // ใช้ Cinemachine Noise (ไม่ไปแย่งตำแหน่งกล้องกับระบบดอลลี่) แปลงความแรงจากหน่วยเมตรเดิมเป็นค่าเกน
        if (cam != null) StartCoroutine(CinematicCameraFeel.ShakeRoutine(cam, intensity * 22f, duration));
    }

    private IEnumerator ShakeCameraRoutine(CinemachineCamera cam, float duration, float intensity)
    {
        Vector3 originalPos = cam.transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float strength = (1f - (elapsed / duration)) * intensity;
            Vector3 offset = UnityEngine.Random.insideUnitSphere * strength;
            cam.transform.position = originalPos + offset;
            yield return null;
        }
        cam.transform.position = originalPos;
    }

    // ==========================================
    // บทนำ (Prologue): เรามาเจอคดีแรกได้อย่างไร  เล่นหลังกด Play ที่เมนูเริ่มเกม
    // ==========================================
    public void PlayPrologueCinematic(Action<int> onLine, Action onFinished)
    {
        StartCoroutine(PrologueRoutine(onLine, onFinished));
    }

    private IEnumerator PrologueRoutine(Action<int> onLine, Action onFinished)
    {
        FindPlayerAndCameras();
        SetPlayerControlsLocked(true);

        Vector3 head = GetNewmanHeadPosition();
        Vector3 fwd = Shot.Flat(GetNewmanForward());
        Vector3 right = Shot.Right(fwd);
        Vector3 toChurch = NewmanToChurchDir();

        if (CinematicLetterbox.Instance == null || camChurchWide == null)
        {
            SetPlayerControlsLocked(false);
            onFinished?.Invoke();
            yield break;
        }

        CinematicLetterbox.Instance.ShowLetterbox();
        const string newman = "สารวัตรนิวแมน";
        var cams = camNewmanClose != null ? camNewmanClose : camChurchWide;

        var dialogues = new List<CinematicLetterbox.DialogueEntry>
        {
            // 1) เครนสูงเหนือถนนลอนดอนยามค่ำ ค่อย ๆ ลดลงหานิวแมนที่ยืนอยู่ไกล ๆ
            new CinematicLetterbox.DialogueEntry(newman,
                "ลอนดอน ค.ศ. 1888... หมอกหนาไม่เคยจางจากไวท์ชาเปล และผู้คนเริ่มกลัวความมืด",
                () =>
                {
                    onLine?.Invoke(0);
                    PlayCutsceneAnimation(animLookAround);
                    var crane = Shot.Move(
                            head + fwd * 10f + right * 3.5f + Vector3.up * 6.5f,
                            head + fwd * 6.5f + right * 2.2f + Vector3.up * 2.2f,
                            head + fwd * 2f - Vector3.up * 0.6f, head - Vector3.up * 0.2f, 9f)
                        .Lens(46f, 38f).Shake(0.25f).Dof(35f, 5.6f, 3f);
                    PlayShots(camChurchWide, crane);
                }, 7.5f),

            // 2) Pedestal ขึ้นจากพื้นหลังเท้านิวแมน เผยไหล่และถนนข้างหน้า + ป้ายชื่อแนะนำตัวละคร
            new CinematicLetterbox.DialogueEntry(newman,
                "สามเดือน ศพสี่ราย และสก็อตแลนด์ยาร์ดยังไม่มีชื่อผู้ต้องสงสัยแม้แต่คนเดียว",
                () =>
                {
                    onLine?.Invoke(1);
                    var rise = Shot.Move(
                            head - toChurch * 1.7f + Shot.Right(toChurch) * 0.45f - Vector3.up * 1.35f,
                            head - toChurch * 1.35f + Shot.Right(toChurch) * 0.5f + Vector3.up * 0.05f,
                            head + toChurch * 4f - Vector3.up * 0.6f, head + toChurch * 8f, 8f)
                        .Lens(40f, 36f).Shake(0.3f).Dof(40f, 2.8f, 2f);
                    PlayShots(cams, rise);
                }, 6.5f),

            // 3) Hero 3/4 Close-Up โค้งอ้อมช้า ๆ
            new CinematicLetterbox.DialogueEntry(newman,
                "คืนนี้โทรเลขด่วนเพิ่งมาถึงโต๊ะผม... เรียกผมไปที่โบสถ์เซนต์แมรี่ ก่อนระฆังเที่ยงคืน",
                () =>
                {
                    onLine?.Invoke(2);
                    PlayCutsceneAnimation(animTalking);
                    Vector3 eye = head + Vector3.up * 0.03f;
                    Vector3 r2 = Shot.Right(toChurch);
                    var hero = Shot.Move(
                            eye + toChurch * 1.6f + r2 * 0.7f - Vector3.up * 0.15f,
                            eye + toChurch * 1.25f + r2 * 0.25f - Vector3.up * 0.05f,
                            eye, eye + Vector3.up * 0.02f, 9f)
                        .Lens(30f, 27f).Dutch(2f, 0f).Shake(0.15f).Dof(70f, 2.0f, 1f);
                    PlayShots(cams, hero);
                }, 6f),

            // 4) หลังไหล่มองไปยังประตูโบสถ์ แล้วดอลลี่ตามไป
            new CinematicLetterbox.DialogueEntry(newman,
                "ถ้าฆาตกรเริ่มทิ้งร่องรอย... ผมจะเป็นคนแรกที่อ่านมันออก",
                () =>
                {
                    onLine?.Invoke(3);
                    PlayCutsceneAnimation(animLookAround);
                    var ots = Shot.OverShoulder(head, ChurchDoor + Vector3.up * 0.6f, 2.0f, 0.6f, -0.2f, 1.2f, 8f)
                        .Lens(36f, 32f).Shake(0.25f).Dof(40f, 4f, 3f);
                    PlayShots(camChurchWide, ots);
                }, 5.5f)
        };

        CinematicLetterbox.Instance.PlayDialogueSequence(dialogues, () =>
        {
            ResetAllCutsceneCameras();
            CinematicLetterbox.Instance.HideLetterbox();
            SetPlayerControlsLocked(false);
            onFinished?.Invoke();
        });

        yield return null;
    }

    /// <summary>
    /// คัตซีนเปิดฉาก Scene 4: นิวแมนก้าวลงสู่ชานชาลาสถานีรถไฟใต้ดิน ไอน้ำ ไฟกะพริบ และรอยเลือดที่ยังเปียก
    /// positions = ตำแหน่งวัตถุพยาน (เลือด, ปลอกกระสุน, นาฬิกา, ตั๋ว, จดหมาย) ใช้จัดช็อตให้ตรงฉากจริง
    /// </summary>
    public void PlayScene4UndergroundStationCutscene(List<Vector3> positions, Action onFinished = null)
    {
        StartCoroutine(Scene4UndergroundCutsceneRoutine(positions, onFinished));
    }

    public void PlayScene4UndergroundStationCutscene(Action onFinished = null)
    {
        StartCoroutine(Scene4UndergroundCutsceneRoutine(null, onFinished));
    }

    private void EnsureScene4Cameras()
    {
        if (camStationUndergroundWide == null)
        {
            var c1 = GameObject.Find("Cutscene_Cam_StationUndergroundWide");
            if (c1 != null) camStationUndergroundWide = c1.GetComponent<CinemachineCamera>();
        }
        if (camStationEvidencePush == null)
        {
            var c2 = GameObject.Find("Cutscene_Cam_StationEvidencePush");
            if (c2 != null) camStationEvidencePush = c2.GetComponent<CinemachineCamera>();
        }
        if (camStationUndergroundWide == null) camStationUndergroundWide = camStationEvidencePush;
        if (camStationEvidencePush == null) camStationEvidencePush = camStationUndergroundWide;
    }

    private static Vector3 CenterOf(List<Vector3> pts, Vector3 fallback)
    {
        if (pts == null || pts.Count == 0) return fallback;
        Vector3 sum = Vector3.zero;
        foreach (var p in pts) sum += p;
        return sum / pts.Count;
    }

    private static Vector3 PointAt(List<Vector3> pts, int i, Vector3 fallback)
    {
        return pts != null && i >= 0 && i < pts.Count ? pts[i] : fallback;
    }

    private IEnumerator Scene4UndergroundCutsceneRoutine(List<Vector3> positions, Action onFinished)
    {
        SetPlayerControlsLocked(true);
        FindPlayerAndCameras();
        EnsureScene4Cameras();

        Vector3 head = GetNewmanHeadPosition();
        Vector3 center = CenterOf(positions, head + Shot.Flat(GetNewmanForward()) * 6f);
        Vector3 blood = PointAt(positions, 0, center);
        Vector3 casing = PointAt(positions, 1, center);

        if (CinematicLetterbox.Instance != null && camStationUndergroundWide != null)
        {
            CinematicLetterbox.Instance.ShowLetterbox();
            const string newman = "สารวัตรนิวแมน";

            var dialogues = new List<CinematicLetterbox.DialogueEntry>
            {
                // ช็อต 1: เครนต่ำไหลลงมาตามบันได เห็นชานชาลาทั้งผืนผ่านไอน้ำ
                new CinematicLetterbox.DialogueEntry(newman,
                    "ไอน้ำพุ่งออกมาจากใต้ขบวนรถ ไฟเพดานกะพริบเหมือนมีใครเล่นกับสวิตช์... ที่นี่ไม่ใช่สถานี มันคือเวที",
                    () =>
                    {
                        PlayCutsceneAnimation(animLookAround);
                        var crane = Shot.OverShoulder(head, center + Vector3.up * 0.4f, 2.2f, 0.7f, 1.0f, 1.4f, 7f)
                            .Lens(40f, 34f).Shake(0.35f).Dof(35f, 4.5f, 5f);
                        var orbit = Shot.Orbit(center, 7.5f, 1.6f, 1.1f,
                                Shot.AngleTo(center, head) + 40f, Shot.AngleTo(center, head) + 75f, center + Vector3.up * 0.8f, 9f)
                            .Lens(36f).Shake(0.3f).Dof(30f, 5.6f, 0f);
                        PlayShots(camStationUndergroundWide, crane, orbit);
                    }),

                // ช็อต 2: มาโครรอยเลือดสด -> ปลอกกระสุนที่ยังอุ่น
                new CinematicLetterbox.DialogueEntry(newman,
                    "หยดเลือดยังเปียก ปลอกกระสุนยังอุ่น ทุกอย่างเพิ่งเกิดขึ้นไม่กี่นาที... แจ็คยังอยู่ในนี้",
                    () =>
                    {
                        var bloodShot = Shot.Move(
                            blood + new Vector3(0.7f, 0.9f, 0.9f), blood + new Vector3(0.35f, 0.45f, 0.45f),
                            blood, 5f).Lens(28f, 22f).Dutch(-4f, 0f).Shake(0.25f).Dof(80f, 2.0f, 0.5f).Macro();
                        var casingShot = Shot.Move(
                            casing + new Vector3(-0.6f, 0.7f, 0.7f), casing + new Vector3(-0.3f, 0.4f, 0.35f),
                            casing, 7f).Lens(24f, 20f).Dutch(3f, 0f).Shake(0.2f).Dof(90f, 1.8f, 0.4f).Macro();
                        PlayShots(camStationEvidencePush, bloodShot, casingShot);
                    }),

                // ช็อต 3: ECU นิวแมน แล้วถอยกว้างเห็นขบวนรถและเงามืดที่จ้องมอง
                new CinematicLetterbox.DialogueEntry(newman,
                    "ขบวนรถด่วนใกล้จะออกแล้ว และมีใครบางคนกำลังมองเราอยู่ ใช้ CiRA CORE ตามรอยให้ทัน ก่อนที่มันจะจางหายไป",
                    () =>
                    {
                        PlayCutsceneAnimation(animTalking);
                        Vector3 fwd = Shot.Flat(GetNewmanForward());
                        var ecu = Shot.Face(head + Vector3.up * 0.03f, fwd, 1.0f, 0.18f, 0f, 0.2f, 5f)
                            .Lens(24f, 21f).Dutch(3f, 3f).Shake(0.3f).Dof(85f, 1.8f, 0.6f).Macro();
                        var pull = Shot.Move(
                            head + fwd * 1.5f + Vector3.up * -0.2f, head + fwd * 5.5f + Vector3.up * 1.4f,
                            head + Vector3.up * 0.1f, center + Vector3.up * 0.6f, 9f)
                            .Lens(32f, 40f).Dutch(0f, -2f).Shake(0.25f).Dof(35f, 5.6f, 0f);
                        PlayShots(camStationUndergroundWide, ecu, pull);
                    })
            };

            CinematicLetterbox.Instance.PlayDialogueSequence(dialogues, () =>
            {
                ResetAllCutsceneCameras();
                CinematicLetterbox.Instance.HideLetterbox();
                SetPlayerControlsLocked(false);
                onFinished?.Invoke();
            });
        }
        else
        {
            SetPlayerControlsLocked(false);
            onFinished?.Invoke();
        }

        yield return null;
    }

    // ==========================================
    // คัตซีนจับกุม Scene 4 (มีตัวแจ็คจริงอยู่ในฉาก)
    // ==========================================
    private GameObject _scene4Jack;
    private PlayableGraph _jackGraph;

    private static float GroundY(Vector3 p, float fallback)
    {
        if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)) return hit.point.y;
        return fallback;
    }

    /// <summary>สร้างแจ็คตัวสำเนาจากตัวละครแจ็คในฉากโรงละคร มาไว้บนชานชาลา (ไม่ใช้สคริปต์ท่าบีบคอ)</summary>
    private GameObject SpawnScene4Jack(Vector3 pos, Vector3 faceToward)
    {
        GameObject src = RoyalLyceumTheaterQuestManager.Instance != null ? RoyalLyceumTheaterQuestManager.Instance.jackTheRipperGo : null;
        if (src == null) src = GameObject.Find("Theater_Jack_The_Ripper");
        if (src == null) return null;

        var clone = Instantiate(src);
        clone.name = "Scene4_Jack_Clone";
        foreach (var seq in clone.GetComponentsInChildren<JackTheaterChokeAndThrowSequence>(true)) DestroyImmediate(seq);
        clone.SetActive(true);
        clone.transform.SetParent(null);
        clone.transform.position = pos;
        clone.transform.rotation = Quaternion.LookRotation(Shot.Flat(faceToward - pos), Vector3.up);

        var anim = clone.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.runtimeAnimatorController = null;
            anim.applyRootMotion = false;
            if (_jackGraph.IsValid()) _jackGraph.Destroy();
            if (animLookAround != null) AnimationPlayableUtilities.PlayClip(anim, animLookAround, out _jackGraph);
        }
        return clone;
    }

    private AnimationClipPlayable _jackClipPlayable;

    private void PlayJackClip(AnimationClip clip, float startTime = 0f)
    {
        if (_scene4Jack == null || clip == null) return;
        var anim = _scene4Jack.GetComponentInChildren<Animator>();
        if (anim == null) return;
        if (_jackGraph.IsValid()) _jackGraph.Destroy();
        _jackClipPlayable = AnimationPlayableUtilities.PlayClip(anim, clip, out _jackGraph);
        if (startTime > 0f) _jackClipPlayable.SetTime(startTime);
    }

    private readonly Dictionary<AnimationClip, float> _fallTimes = new Dictionary<AnimationClip, float>();

    /// <summary>
    /// หาเวลาที่ผู้ถูกโจมตีในคลิป B ล้มลง (สะโพกตกต่ำกว่าครึ่งความสูงตอนยืน) โดยลอง sample ท่าบนตัวแจ็ค
    /// คลิปต่อสู้ยาว 13-25 วินาที มีช่วงยืนเตรียมท่ายาว จึงต้องหาจังหวะจริงเพื่อเริ่มคลิปให้ใกล้จังหวะปะทะ
    /// </summary>
    private float FindFallTime(AnimationClip bClip)
    {
        if (bClip == null) return 2f;
        if (_fallTimes.TryGetValue(bClip, out float cached)) return cached;
        float result = Mathf.Min(2f, bClip.length * 0.5f);
        if (_scene4Jack != null)
        {
            var anim = _scene4Jack.GetComponentInChildren<Animator>();
            var hips = anim != null ? anim.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (hips != null)
            {
                var go = anim.gameObject;
                bClip.SampleAnimation(go, 0f);
                float h0 = hips.position.y - go.transform.position.y;
                for (float t = 0.1f; t < bClip.length; t += 0.1f)
                {
                    bClip.SampleAnimation(go, t);
                    float h = hips.position.y - go.transform.position.y;
                    if (h0 > 0.2f && h < h0 * 0.55f) { result = t; break; }
                }
            }
        }
        _fallTimes[bClip] = result;
        return result;
    }

    private bool _fightRootMotionSaved;
    private bool _newmanRootMotionWas;
    private Vector3 _newmanAnimLocalPos;
    private Quaternion _newmanAnimLocalRot;

    /// <summary>
    /// คลิปต่อสู้ "อบ" การหมุนของสะโพกเข้าไปในท่า (lockRootRotation + keepOriginalOrientation) ตัวละครจึงหันตามมุม yaw ของคลิปเสมอ
    /// ต้องหมุน transform ชดเชยมุมของคลิป ณ เวลาเริ่ม เพื่อให้ตัวจริงหันหน้าเข้าหาคู่ต่อสู้
    /// </summary>
    /// <summary>เลือกฝั่งซ้าย/ขวาที่โล่งกว่าสำหรับวางกล้องด้านข้าง</summary>
    private static Vector3 OpenSide(Vector3 mid, Vector3 right)
    {
        Vector3 o = mid + Vector3.up * 1.0f;
        float dR = Physics.Raycast(o, right, out var h1, 8f, ~0, QueryTriggerInteraction.Ignore) ? h1.distance : 8f;
        float dL = Physics.Raycast(o, -right, out var h2, 8f, ~0, QueryTriggerInteraction.Ignore) ? h2.distance : 8f;
        return dR >= dL ? right : -right;
    }

    /// <summary>ระยะที่วางกล้องได้ไกลสุดในทิศนั้นโดยไม่ชนกำแพง (อย่างน้อย 1.4 ม.)</summary>
    private static float FreeDistance(Vector3 mid, Vector3 dir, float want)
    {
        if (Physics.Raycast(mid + Vector3.up * 1.0f, dir, out var hit, want + 0.5f, ~0, QueryTriggerInteraction.Ignore))
            return Mathf.Clamp(hit.distance - 0.45f, 1.4f, want);
        return want;
    }

    private void PlaceFighters(Vector3 newmanPos, float newmanClipYaw, Vector3 jackPos, float jackClipYaw)
    {
        var cc = FindAnyObjectByType<CharacterController>();
        if (cc != null)
        {
            bool was = cc.enabled;
            cc.enabled = false;
            cc.transform.position = newmanPos;
            cc.transform.rotation = Quaternion.LookRotation(Shot.Flat(jackPos - newmanPos), Vector3.up) * Quaternion.Euler(0f, -newmanClipYaw, 0f);
            cc.enabled = was;
        }
        if (_scene4Jack != null)
        {
            _scene4Jack.transform.position = jackPos;
            _scene4Jack.transform.rotation = Quaternion.LookRotation(Shot.Flat(newmanPos - jackPos), Vector3.up) * Quaternion.Euler(0f, -jackClipYaw, 0f);
        }
        Physics.SyncTransforms();
    }

    /// <summary>เปิด root motion ให้ตัวละครเคลื่อนที่ตามท่าจริง (ก้าวเข้า/ถอย/กระเด็น) และคืนค่าเดิมเมื่อจบ</summary>
    private void SetFightRootMotion(bool on)
    {
        if (_playerAnimator != null)
        {
            if (on && !_fightRootMotionSaved)
            {
                _fightRootMotionSaved = true;
                _newmanRootMotionWas = _playerAnimator.applyRootMotion;
                _newmanAnimLocalPos = _playerAnimator.transform.localPosition;
                _newmanAnimLocalRot = _playerAnimator.transform.localRotation;
            }
            if (on) _playerAnimator.applyRootMotion = true;
            else if (_fightRootMotionSaved)
            {
                _playerAnimator.applyRootMotion = _newmanRootMotionWas;
                // ถ้า Animator อยู่ที่ลูกของตัวควบคุม คืนตำแหน่งโมเดลให้ตรงกับ CharacterController
                if (_playerAnimator.GetComponent<CharacterController>() == null)
                {
                    _playerAnimator.transform.localPosition = _newmanAnimLocalPos;
                    _playerAnimator.transform.localRotation = _newmanAnimLocalRot;
                }
                _fightRootMotionSaved = false;
            }
        }
        if (_scene4Jack != null)
        {
            var anim = _scene4Jack.GetComponentInChildren<Animator>();
            if (anim != null) anim.applyRootMotion = on;
        }
    }

    /// <summary>หยุดท่าไว้ที่เฟรมปัจจุบันหลังผ่านไป delay วินาที (กันลุกขึ้นเองระหว่างบทพูดถัดไป)</summary>
    private IEnumerator FreezeFightAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (_jackClipPlayable.IsValid()) _jackClipPlayable.SetSpeed(0);
        if (_newmanClipPlayable.IsValid()) _newmanClipPlayable.SetSpeed(0);
        if (_mirrorClipPlayable.IsValid()) _mirrorClipPlayable.SetSpeed(0);
        SetFightRootMotion(false);
    }

    /// <summary>จังหวะปะทะ: สโลว์โมชันสั้น ๆ + กล้องสะเทือน + เสียงกระแทก (+ ฝุ่นถ้าล้มลงพื้น)</summary>
    private IEnumerator ImpactRoutine(float delay, CinemachineCamera cam, Vector3 at, bool dust)
    {
        yield return new WaitForSecondsRealtime(delay);
        Time.timeScale = 0.25f;
        CutsceneFX.PlayThud(at, 0.9f);
        ShakeCamera(cam, 0.45f, 0.28f);
        // ไม่ใช้ฝุ่น/ควันตอนล้ม (ดูเหมือนควันมากกว่าการต่อสู้จริง)
        yield return new WaitForSecondsRealtime(0.55f);
        Time.timeScale = 1f;
    }

    /// <summary>จังหวะโดนเบา ๆ (หมัดปะทะ/กันได้): กล้องสะเทือน + เสียงกระแทก ไม่สโลว์โมชัน</summary>
    private IEnumerator HitRoutine(float delay, CinemachineCamera cam, float intensity, bool unused, Vector3 at)
    {
        yield return new WaitForSecondsRealtime(delay);
        CutsceneFX.PlayThud(at, 0.55f);
        ShakeCamera(cam, 0.25f, intensity);
    }

    private void RemoveScene4Jack()
    {
        if (_jackGraph.IsValid()) _jackGraph.Destroy();
        if (_scene4Jack != null) Destroy(_scene4Jack);
        _scene4Jack = null;
    }

    private static void MovePlayerBody(Vector3 pos, Vector3 faceToward)
    {
        var cc = FindAnyObjectByType<CharacterController>();
        if (cc == null) return;
        bool wasEnabled = cc.enabled;
        cc.enabled = false;
        cc.transform.position = pos;
        cc.transform.rotation = Quaternion.LookRotation(Shot.Flat(faceToward - pos), Vector3.up);
        cc.enabled = wasEnabled;
        Physics.SyncTransforms();
    }

    /// <summary>
    /// คัตซีนจับกุม Scene 4 หลังเชื่อมโยงหลักฐานสำเร็จ: นิวแมนอ่านกลลวงของมอริอาร์ตี้ บุกตู้หมายเลข 4 เผชิญหน้าและจับกุมแจ็ค
    /// </summary>
    public void PlayScene4CaptureCutscene(List<Vector3> positions, Action onFinished = null)
    {
        StartCoroutine(Scene4CaptureCutsceneRoutine(positions, onFinished));
    }

    private IEnumerator Scene4CaptureCutsceneRoutine(List<Vector3> positions, Action onFinished)
    {
        SetPlayerControlsLocked(true);
        FindPlayerAndCameras();
        EnsureScene4Cameras();

        Vector3 head0 = GetNewmanHeadPosition();
        Vector3 center = CenterOf(positions, head0 + Shot.Flat(GetNewmanForward()) * 6f);
        Vector3 casing = PointAt(positions, 1, center);
        Vector3 ticket = PointAt(positions, 3, center);
        Vector3 note = PointAt(positions, 4, center);

        // ตำแหน่งยืน: แจ็คถูกต้อนจนมุมใกล้ตู้ท้ายขบวน นิวแมนเข้ามาหยุดห่างราว 5 เมตร
        Vector3 jackPos = casing + new Vector3(-3.5f, 0f, 0.8f);
        jackPos.y = GroundY(jackPos, casing.y);
        Vector3 dirToNewman = Shot.Flat(head0 - jackPos);
        Vector3 newmanPos = jackPos + dirToNewman * 5.0f;
        newmanPos.y = GroundY(newmanPos, jackPos.y) + 0.05f;

        if (CinematicLetterbox.Instance != null && camStationUndergroundWide != null)
        {
            CinematicLetterbox.Instance.ShowLetterbox();
            const string newman = "สารวัตรนิวแมน";
            const string jack = "<color=#E74C3C>แจ็ค เดอะ ริปเปอร์</color>";

            // คัตซีนจับกุมต้องกระชับ: พิมพ์เร็วขึ้นและค้างข้อความสั้นลง (คืนค่าเดิมตอนจบ)
            var lb = CinematicLetterbox.Instance;
            float oldType = lb.typewriterCharsPerSecond;
            float oldRead = lb.readingCharsPerSecond;
            float oldAuto = lb.autoPlayDurationPerShot;
            lb.typewriterCharsPerSecond = 60f;
            lb.readingCharsPerSecond = 26f;
            lb.autoPlayDurationPerShot = 3.2f;

            Vector3 NewmanHead() { return newmanPos + Vector3.up * 1.62f; }
            Vector3 JackHead() { return jackPos + Vector3.up * 1.75f; }

            var dialogues = new List<CinematicLetterbox.DialogueEntry>
            {
                // 1) มาโครตั๋ว -> จดหมาย M
                new CinematicLetterbox.DialogueEntry(newman,
                    "ตั๋วบอกตู้ที่สี่ แต่โน้ตของ M ชี้ไปหัวขบวน... เขาอยากให้ฉันวิ่งผิดทาง",
                    () =>
                    {
                        PlayCutsceneAnimation(animTalking);
                        var tk = Shot.Move(ticket + new Vector3(0.1f, 0.55f, 0.3f), ticket + new Vector3(0.04f, 0.28f, 0.12f),
                            ticket, 5f).Lens(24f, 20f).Dutch(-3f, 0f).Shake(0.2f).Dof(90f, 1.6f, 0.4f).Macro();
                        var nt = Shot.Move(note + new Vector3(-0.1f, 0.6f, 0.35f), note + new Vector3(-0.04f, 0.3f, 0.14f),
                            note, 6f).Lens(24f, 20f).Dutch(3f, 0f).Shake(0.2f).Dof(90f, 1.6f, 0.4f).Macro();
                        PlayShots(camStationEvidencePush, tk, nt);
                    }),

                // 2) บุกเข้าไปประจันหน้าแจ็ค (ย้ายนิวแมนมาประจำที่ และแจ็คปรากฏตัว)
                new CinematicLetterbox.DialogueEntry(newman,
                    "หลักฐานจริงไม่เคยโกหก ท้ายขบวน ตู้หมายเลขสี่... บุก!",
                    () =>
                    {
                        RemoveScene4Jack();
                        _scene4Jack = SpawnScene4Jack(jackPos, newmanPos);
                        MovePlayerBody(newmanPos, jackPos);
                        PlayCutsceneAnimation(animLookAround);

                        Vector3 toJack = Shot.Flat(jackPos - newmanPos);
                        Vector3 right = Shot.Right(toJack);
                        var run = Shot.Move(NewmanHead() - toJack * 3.0f + right * 0.6f - Vector3.up * 0.5f,
                                NewmanHead() + toJack * 0.5f + right * 0.55f - Vector3.up * 0.45f,
                                JackHead() - Vector3.up * 0.3f, 2.8f)
                            .Lens(54f, 46f).Shake(1.5f).Dof(35f, 4f, 3f).Eased(ShotEase.EaseOut);
                        Vector3 midPoint = (newmanPos + jackPos) * 0.5f;
                        float a0 = Shot.AngleTo(midPoint, newmanPos) + 70f;
                        var orbit = Shot.Orbit(midPoint, 5.0f, 0.9f, 0.6f, a0, a0 + 28f, midPoint + Vector3.up * 1.3f, 8f)
                            .Lens(34f).Shake(0.8f).Dof(35f, 4f, 2f);
                        PlayShots(camStationUndergroundWide, run, orbit);
                    }),

                // 3) แจ็ค: ใบหน้าเสยจากมุมต่ำ
                new CinematicLetterbox.DialogueEntry(jack,
                    "\"มาเร็วกว่าที่เขาบอกอีกแล้วนะ สารวัตร\"",
                    () =>
                    {
                        var low = Shot.Face(JackHead(), newmanPos - jackPos, 1.8f, -0.35f, -0.45f, 0.35f, 8f)
                            .Lens(26f, 24f).Dutch(-6f, -3f).Shake(0.5f).Dof(80f, 1.4f, 0.8f);
                        PlayShots(camStationEvidencePush, low);
                    }),

                // 4) นิวแมน: จับกุม
                new CinematicLetterbox.DialogueEntry(newman,
                    "จบแล้ว แจ็ค ในนามของสก็อตแลนด์ยาร์ด แกถูกจับกุม",
                    () =>
                    {
                        PlayCutsceneAnimation(animTalking);
                        var face = Shot.Face(NewmanHead() + Vector3.up * 0.02f, jackPos - newmanPos, 1.4f, 0.35f, -0.05f, 0.3f, 7f)
                            .Lens(30f, 27f).Dutch(2f, 0f).Shake(0.25f).Dof(65f, 2.2f, 1f);
                        PlayShots(camStationUndergroundWide, face);
                    }),

                // F1) แจ็คไม่ยอมให้จับ: แลกหมัดกันก่อน แล้วแจ็คเตะนิวแมนกระเด็นล้ม (Kickdown: A = นิวแมน, B = แจ็ค)
                new CinematicLetterbox.DialogueEntry(jack,
                    "\"จับฉันงั้นหรือ? ฝันไปเถอะ!\"",
                    () =>
                    {
                        // ข้อมูลจาก root curve: 7.4s เริ่มแลกหมัด (นิวแมนต่อย 7.9s/8.9s แจ็คย่อหลบ), แจ็คเตะ ~10.4s, นิวแมนล้ม 10.8-12.0s
                        const float t0 = 7.4f;
                        newmanPos = jackPos + dirToNewman * 1.15f;
                        newmanPos.y = GroundY(newmanPos, jackPos.y) + 0.05f;
                        PlaceFighters(newmanPos, 16f, jackPos, 45f);
                        PlayCutsceneAnimation(fightKickA, t0);
                        PlayJackClip(fightKickB, t0);
                        SetFightRootMotion(true);

                        Vector3 toJack = Shot.Flat(jackPos - newmanPos);
                        Vector3 mid = (newmanPos + jackPos) * 0.5f;
                        // ชานชาลาแคบ: เลือกฝั่งที่โล่งกว่าและไม่ให้กล้องชิดกำแพงจนเห็นแต่ขา
                        Vector3 side = OpenSide(mid, Shot.Right(toJack));
                        float room = FreeDistance(mid, side, 3.4f);

                        // 1) หลังไหล่แจ็ค เห็นนิวแมนพุ่งเข้าต่อย
                        var overJack = Shot.OverShoulder(JackHead(), NewmanHead(), 1.0f, 0.45f, 0.0f, 0.2f, 1.3f)
                            .Lens(38f).Shake(1.1f).Dof(45f, 2.8f, 0.5f);
                        // 2) มุมข้างกว้าง เห็นทั้งสองแลกหมัด/หลบ
                        var wide = Shot.Move(mid + side * room + Vector3.up * 0.6f, mid + side * (room - 0.3f) + toJack * 0.3f + Vector3.up * 0.55f,
                                mid + Vector3.up * 1.0f, 1.7f)
                            .Lens(36f, 34f).Shake(1.0f).Dof(35f, 4f, 1f);
                        // 3) หลังไหล่นิวแมน แจ็คหมุนตัวเตะเข้ามาเต็มเฟรม (จังหวะโดนเตะ ~3.0s)
                        var kick = Shot.OverShoulder(NewmanHead(), JackHead(), 1.1f, -0.5f, -0.1f, 0.15f, 1.5f)
                            .Lens(42f).Dutch(-6f, -4f).Shake(1.3f).Dof(40f, 2.8f, 0.5f);
                        // 4) มุมสูงตามนิวแมนกระเด็นล้มหงาย
                        Vector3 down = newmanPos - toJack * 1.3f + Vector3.up * 0.25f;
                        var high = Shot.Move(down + Vector3.up * 2.8f + side * 1.0f, down + Vector3.up * 2.0f + side * 0.8f, down, 3.2f)
                            .Lens(38f, 33f).Dutch(6f, 3f).Shake(0.5f).Dof(40f, 3.2f, 1f);
                        PlayShots(camStationUndergroundWide, overJack, wide, kick, high);

                        StartCoroutine(HitRoutine(0.55f, camStationUndergroundWide, 0.25f, false, jackPos + Vector3.up * 1.4f));
                        StartCoroutine(HitRoutine(1.55f, camStationUndergroundWide, 0.25f, false, jackPos + Vector3.up * 1.4f));
                        StartCoroutine(ImpactRoutine(3.0f, camStationUndergroundWide, newmanPos + Vector3.up * 1.2f, false));
                        StartCoroutine(HitRoutine(4.9f, camStationUndergroundWide, 0.35f, false, newmanPos - toJack * 1.2f));
                        StartCoroutine(FreezeFightAfter(5.4f));
                    }, 7.6f),

                // F2) นิวแมนลุกขึ้นสู้กลับ: แจ็คต่อย/เตะ นิวแมนหลบแล้วซัดกลับจนแจ็คล้ม (Knockdown: A = แจ็ค, B = นิวแมน)
                new CinematicLetterbox.DialogueEntry(newman,
                    "ยังไม่จบหรอก แจ็ค!",
                    () =>
                    {
                        // ข้อมูลจาก root curve: 17.6s เริ่ม, แจ็คต่อย ~18.4s, แจ็คเตะ ~20.2s, หมัดสุดท้ายของนิวแมน ~21.9s, แจ็คล้ม 22.0-23.0s
                        const float t0 = 17.6f;
                        newmanPos = jackPos + dirToNewman * 1.0f;
                        newmanPos.y = GroundY(newmanPos, jackPos.y) + 0.05f;
                        PlaceFighters(newmanPos, 175f, jackPos, -138f);
                        PlayCutsceneAnimation(fightKnockB, t0);
                        PlayJackClip(fightKnockA, t0);
                        SetFightRootMotion(true);

                        Vector3 toJack = Shot.Flat(jackPos - newmanPos);
                        Vector3 mid = (newmanPos + jackPos) * 0.5f;
                        Vector3 side = OpenSide(mid, Shot.Right(toJack));
                        float room = FreeDistance(mid, side, 3.4f);

                        // 1) มุมต่ำฮีโร่ นิวแมนตั้งการ์ด
                        var hero = Shot.Face(NewmanHead(), toJack, 1.5f, -0.4f, -0.55f, 0.35f, 1.0f)
                            .Lens(30f, 27f).Dutch(-4f, -2f).Shake(1.0f).Dof(60f, 2.2f, 0.5f);
                        // 2) มุมข้างกว้าง แลกหมัด-เตะ
                        var wide = Shot.Move(mid + side * room + toJack * 0.4f + Vector3.up * 0.65f, mid + side * (room - 0.4f) + Vector3.up * 0.6f,
                                mid + Vector3.up * 1.0f, 2.4f)
                            .Lens(36f, 33f).Shake(1.0f).Dof(35f, 4f, 1f);
                        // 3) หลังไหล่แจ็ค เห็นหมัดสุดท้ายของนิวแมนพุ่งเข้ากล้อง
                        var punch = Shot.OverShoulder(JackHead(), NewmanHead(), 1.0f, -0.45f, -0.05f, 0.2f, 1.2f)
                            .Lens(40f).Dutch(5f, 3f).Shake(1.3f).Dof(45f, 2.6f, 0.5f);
                        // 4) เครนลงตามแจ็คล้มลงพื้น
                        Vector3 fall = jackPos + Vector3.up * 0.3f;
                        var crane = Shot.Move(fall + Vector3.up * 2.6f + side * 1.2f, fall + Vector3.up * 1.6f + side * 0.9f, fall, 3.4f)
                            .Lens(36f, 32f).Dutch(-5f, -2f).Shake(0.5f).Dof(40f, 3.2f, 1f);
                        PlayShots(camStationEvidencePush, hero, wide, punch, crane);

                        StartCoroutine(HitRoutine(0.8f, camStationEvidencePush, 0.25f, false, newmanPos + Vector3.up * 1.4f));
                        StartCoroutine(HitRoutine(2.6f, camStationEvidencePush, 0.3f, false, newmanPos + Vector3.up * 1.0f));
                        StartCoroutine(ImpactRoutine(4.3f, camStationEvidencePush, jackPos + Vector3.up * 1.4f, false));
                        StartCoroutine(HitRoutine(5.8f, camStationEvidencePush, 0.4f, false, jackPos));
                        StartCoroutine(FreezeFightAfter(6.6f));
                    }, 7.8f),

                // F3) นิวแมนยืนค้ำแจ็คที่ล้มอยู่ (มุมต่ำจากพื้น)
                new CinematicLetterbox.DialogueEntry(newman,
                    "หยุดเล่นเกมของมอริอาร์ตี้ได้แล้ว",
                    () =>
                    {
                        SetFightRootMotion(false);
                        var ccNow = FindAnyObjectByType<CharacterController>();
                        if (ccNow != null) newmanPos = ccNow.transform.position;
                        PlayCutsceneAnimation(animLookAround);
                        Vector3 toJack = Shot.Flat(jackPos - newmanPos);
                        Vector3 side = Shot.Right(toJack);
                        Vector3 jh = _scene4Jack != null ? Shot.Head(_scene4Jack.transform) : jackPos + Vector3.up * 0.3f;
                        var worm = Shot.Move(jh + Vector3.up * 0.25f + side * 0.5f, jh + Vector3.up * 0.35f + side * 0.35f,
                                NewmanHead() - Vector3.up * 0.1f, NewmanHead(), 6f)
                            .Lens(26f, 24f).Dutch(-3f, 0f).Shake(0.35f).Dof(60f, 2.2f, 1f);
                        worm.avoidWalls = false;
                        PlayShots(camStationUndergroundWide, worm);
                    }),

                // 5) แจ็ค: ไม่ใช่ผู้เริ่มเกม
                new CinematicLetterbox.DialogueEntry(jack,
                    "\"ฮะ... ฉันเป็นแค่มือที่ลงมือ คนเขียนบทยังลอยนวลอยู่\"",
                    () =>
                    {
                        Vector3 jh = _scene4Jack != null ? Shot.Head(_scene4Jack.transform) : JackHead();
                        Vector3 toward = Shot.Flat(newmanPos - jackPos);
                        var cu = Shot.Move(jh + Vector3.up * 0.95f + toward * 0.55f, jh + Vector3.up * 0.75f + toward * 0.42f, jh, 8f)
                            .Lens(28f, 25f).Dutch(5f, 3f).Shake(0.3f).Dof(80f, 1.8f, 0.5f).Macro();
                        cu.avoidWalls = false;
                        PlayShots(camStationEvidencePush, cu);
                    }),

                // 8) นิวแมน: ปิดท้าย
                new CinematicLetterbox.DialogueEntry(newman,
                    "งั้นฉันจะตามหาคนเขียนบทนั้นให้เจอ",
                    () =>
                    {
                        PlayCutsceneAnimation(animTalking);
                        Vector3 toJack = Shot.Flat(jackPos - newmanPos);
                        var hero = Shot.Move(
                            NewmanHead() + toJack * 2.0f - Vector3.up * 1.05f, NewmanHead() + toJack * 1.6f - Vector3.up * 0.5f,
                            NewmanHead() + Vector3.up * 0.1f, NewmanHead() + Vector3.up * 0.06f, 9f)
                            .Lens(32f, 28f).Dutch(-2f, 0f).Shake(0.3f).Dof(60f, 2.4f, 1f);
                        PlayShots(camStationUndergroundWide, hero);
                    })
            };

            CinematicLetterbox.Instance.PlayDialogueSequence(dialogues, () =>
            {
                lb.typewriterCharsPerSecond = oldType;
                lb.readingCharsPerSecond = oldRead;
                lb.autoPlayDurationPerShot = oldAuto;
                Time.timeScale = 1f;
                SetFightRootMotion(false);
                ResetAllCutsceneCameras();
                CinematicLetterbox.Instance.HideLetterbox();
                RemoveScene4Jack();
                onFinished?.Invoke(); // ผู้จัดการเควสล็อกผู้เล่นต่อเองระหว่างหน้าสรุปบท
            });
        }
        else
        {
            onFinished?.Invoke();
        }

        yield return null;
    }
}
