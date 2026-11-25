using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ระบบนำทางเควสระดับเกม AAA ในธีมนักสืบ:
/// - เข็มทิศบนหัวจอ (Compass Bar): แถบทิศ N/E/S/W พร้อมองศา และไอคอนเป้าหมายเลื่อนไปตามทิศที่ต้องเดินไป
/// - หมุดในโลก 3 มิติ: เพชรทองเหลือง + วงแหวนพัลส์ + แสงลำพุ่งลงสู่พื้น (Quest Beam)
///   ชื่อสถานที่จะค่อยโผล่เมื่อเข้าใกล้ ระยะทางปรับขนาดตามความไกล และจางลงเมื่อมีสิ่งกีดขวางบัง
/// - เมื่อเป้าหมายอยู่นอกจอ: ตัวชี้ทิศที่ขอบจอ (Chevron + ไอคอน + ระยะ) เลื่อนนุ่มนวล ไม่กระโดด
/// - เมื่อถึงเป้าหมาย: วงแหวนระเบิดออก (Arrival Pulse) แล้วหมุดจางหายไป
/// เรียกใช้ผ่าน SetTarget / SetVisible / ClearTarget เหมือนเดิม
/// </summary>
public class WaypointNavigationSystem : MonoBehaviour
{
    private static WaypointNavigationSystem _instance;
    public static WaypointNavigationSystem Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<WaypointNavigationSystem>();
            return _instance;
        }
        private set { _instance = value; }
    }

    [Header("เป้าหมายการนำทาง")]
    [SerializeField] private Transform _targetTransform;
    [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private string _destinationName = "โบสถ์เซนต์แมรี่ (St. Mary Church)";

    [Header("การแสดงผล")]
    public Font thaiFont;
    public Font thaiFontBold;
    [SerializeField] private bool _showWaypoint = true;
    [SerializeField] private float _arrivalDistance = 3.5f;
    [Tooltip("เข้าใกล้กว่าระยะนี้ (เมตร) ชื่อสถานที่จะค่อยโผล่เหนือหมุด")]
    [SerializeField] private float _nameRevealDistance = 45f;

    // สถานะระยะทางแบบเรียลไทม์สำหรับหน้าต่างเควส
    public int CurrentDistanceMeters { get; private set; } = 0;
    public float CurrentDistance { get; private set; } = 0f;
    public Transform TargetTransform => _targetTransform;
    public string DestinationName => _destinationName;
    public bool IsVisible => _showWaypoint && _targetTransform != null;

    // ----- ค่าคงที่ของเข็มทิศ -----
    private const float CompassWidth = 560f;
    private const float CompassHalfWidth = 262f;
    private const float CompassHalfFov = 72f;
    private const float CompassTop = -26f;
    private const int TickCount = 24;
    private static readonly string[] CardinalNames = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    private static readonly Color Brass = new Color(0.86f, 0.70f, 0.36f, 1f);
    private static readonly Color Paper = new Color(0.96f, 0.92f, 0.80f, 1f);
    private static readonly Color AlertRed = new Color(0.95f, 0.36f, 0.30f, 1f);

    // ----- UI -----
    private Canvas _canvas;
    private RectTransform _hudRoot;
    private float _uiScale = 1f;
    private Font _typewriterFont;

    private RectTransform _worldRoot;
    private CanvasGroup _worldGroup;
    private Image _beam;
    private Image _pingRing;
    private Image _icon;
    private Text _nameText;
    private CanvasGroup _nameGroup;
    private Text _distText;
    private RectTransform _pulseRing;
    private Image _pulseImage;

    private RectTransform _edgeRoot;
    private CanvasGroup _edgeGroup;
    private RectTransform _chevron;
    private RectTransform _edgeIconRoot;
    private Text _edgeDist;

    private RectTransform _compassRoot;
    private RectTransform[] _tickRects = new RectTransform[TickCount];
    private Image[] _tickImages = new Image[TickCount];
    private Text[] _tickLabels = new Text[TickCount];
    private RectTransform _compassMarker;
    private Image _compassMarkerImg;
    private Text _compassDist;
    private Text _headingText;

    // ----- สถานะ -----
    private Camera _mainCamera;
    private Transform _playerTransform;
    private Transform _trackedTarget;
    private float _edgeBlend;
    private float _occlusionAlpha = 1f;
    private bool _occluded;
    private float _nextOcclusionCheck;
    private bool _arrivedFired;
    private float _pulseTimer = 99f;
    private float _camYaw;

    // ==========================================
    // Lifecycle
    // ==========================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (thaiFont == null)
        {
            thaiFont = GameAssets.Load<Font>("Assets/Font/Prompt-Medium.ttf");
            if (thaiFont == null) thaiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (thaiFontBold == null)
        {
            thaiFontBold = GameAssets.Load<Font>("Assets/Font/Prompt-Bold.ttf");
            if (thaiFontBold == null) thaiFontBold = thaiFont;
        }

        _typewriterFont = Font.CreateDynamicFontFromOSFont(new[] { "Courier New", "Consolas" }, 24);
        if (_typewriterFont == null) _typewriterFont = thaiFontBold;

        BuildWaypointUI();
    }

    private void Start()
    {
        _mainCamera = Camera.main;
        LocatePlayer();
    }

    private void LocatePlayer()
    {
        // 1. CharacterController ของผู้เล่นเป็นอันดับแรก (ตัวที่เคลื่อนที่และเปลี่ยนพิกัดจริง)
        var cc = FindAnyObjectByType<CharacterController>();
        if (cc != null)
        {
            _playerTransform = cc.transform;
            return;
        }

        var tpc = FindAnyObjectByType<StarterAssets.ThirdPersonController>();
        if (tpc != null)
        {
            _playerTransform = tpc.transform;
            return;
        }

        var armature = GameObject.Find("PlayerArmature");
        if (armature != null)
        {
            _playerTransform = armature.transform;
            return;
        }

        var player = GameObject.Find("NestedParentArmature_Newman") ?? GameObject.Find("Player_CH2_Newman");
        if (player != null)
        {
            var childCc = player.GetComponentInChildren<CharacterController>();
            _playerTransform = childCc != null ? childCc.transform : player.transform;
        }
    }

    // ==========================================
    // Public API (เหมือนเดิม)
    // ==========================================
    /// <summary>ตั้งเป้าหมายการนำทางใหม่พร้อมชื่อสถานที่</summary>
    public void SetTarget(Transform target, string destinationName, Vector3 offset = default)
    {
        _targetTransform = target;
        _destinationName = destinationName;
        if (offset != default) _targetOffset = offset;
        _showWaypoint = true;
        _arrivedFired = false;
        _pulseTimer = 99f;
        _edgeBlend = 0f;
    }

    /// <summary>สั่งปิดหรือเปิดแสดงผลระบบนำทาง (เข็มทิศ + หมุด)</summary>
    public void SetVisible(bool visible)
    {
        _showWaypoint = visible;
        if (_hudRoot != null && !visible) _hudRoot.gameObject.SetActive(false);
    }

    public void ClearTarget()
    {
        _targetTransform = null;
        CurrentDistance = 0f;
        CurrentDistanceMeters = 0;
        _showWaypoint = false;
        if (_hudRoot != null) _hudRoot.gameObject.SetActive(false);
    }

    /// <summary>คงชื่อเดิมไว้ให้สคริปต์ Editor เรียกได้</summary>
    public void BuildGenshinWaypointUI()
    {
        BuildWaypointUI();
    }

    // ==========================================
    // Per-frame update
    // ==========================================
    private void LateUpdate()
    {
        // เข็มทิศและหมุดแสดงเฉพาะตอนที่มีเป้าหมายนำทางอยู่ (เหมือนระบบเควสในเกม AAA)
        if (!_showWaypoint || _targetTransform == null)
        {
            if (_hudRoot != null && _hudRoot.gameObject.activeSelf) _hudRoot.gameObject.SetActive(false);
            return;
        }
        if (_hudRoot == null) return;
        if (!_hudRoot.gameObject.activeSelf) _hudRoot.gameObject.SetActive(true);

        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;
        if (_playerTransform == null || !_playerTransform.gameObject.activeInHierarchy) LocatePlayer();

        float dt = Time.unscaledDeltaTime;
        _uiScale = (_canvas != null && _canvas.scaleFactor > 0.01f) ? _canvas.scaleFactor : 1f;
        _camYaw = _mainCamera.transform.eulerAngles.y;

        UpdateCompassTicks();

        if (_targetTransform == null)
        {
            HideTargetVisuals();
            return;
        }

        if (_targetTransform != _trackedTarget)
        {
            _trackedTarget = _targetTransform;
            _arrivedFired = false;
            _pulseTimer = 99f;
            _edgeBlend = 0f;
        }

        // ----- ระยะทาง -----
        Vector3 targetWorldPos = _targetTransform.position + _targetOffset;
        Vector3 origin = _playerTransform != null ? _playerTransform.position : _mainCamera.transform.position;
        CurrentDistance = Vector3.Distance(origin, targetWorldPos);
        CurrentDistanceMeters = Mathf.RoundToInt(CurrentDistance);

        _distText.text = FormatDistanceRich(CurrentDistance);
        _edgeDist.text = FormatDistanceRich(CurrentDistance);
        _compassDist.text = FormatDistancePlain(CurrentDistance);
        _nameText.text = _destinationName;

        // ----- ถึงเป้าหมาย: หมุดจางหาย + วงแหวนระเบิด -----
        float arrivalFade;
        if (CurrentDistance <= 1.8f) arrivalFade = 0f;
        else if (CurrentDistance <= _arrivalDistance) arrivalFade = Mathf.InverseLerp(1.8f, _arrivalDistance, CurrentDistance);
        else arrivalFade = 1f;

        if (CurrentDistance <= _arrivalDistance)
        {
            if (!_arrivedFired)
            {
                _arrivedFired = true;
                _pulseTimer = 0f;
            }
        }
        else if (CurrentDistance > _arrivalDistance * 2f)
        {
            _arrivedFired = false;
        }

        // ----- ตำแหน่งบนหน้าจอ -----
        Vector3 sp = _mainCamera.WorldToScreenPoint(targetWorldPos);
        bool behind = sp.z <= 0.01f;
        float marginX = 90f * _uiScale;
        float marginBottom = 150f * _uiScale;
        float marginTop = 150f * _uiScale;
        bool inside = !behind
                      && sp.x >= marginX && sp.x <= Screen.width - marginX
                      && sp.y >= marginBottom && sp.y <= Screen.height - marginTop;

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector3 local = _mainCamera.transform.InverseTransformPoint(targetWorldPos);
        Vector2 edgeDir = behind
            ? new Vector2(local.x >= 0f ? 1f : -1f, -0.25f)   // เป้าหมายอยู่ด้านหลัง: ชี้ไปด้านที่หมุนตัวสั้นกว่า
            : new Vector2(sp.x, sp.y) - center;
        if (edgeDir.sqrMagnitude < 0.01f) edgeDir = Vector2.down;

        float halfW = Screen.width * 0.5f - marginX;
        float halfH = Mathf.Min(Screen.height * 0.5f - marginBottom, Screen.height * 0.5f - marginTop);
        Vector2 edgePos = ClampToRect(center, edgeDir, halfW, halfH);
        Vector2 iconPos = inside ? new Vector2(sp.x, sp.y) : edgePos;
        AvoidQuestHud(ref iconPos);
        AvoidQuestHud(ref edgePos);

        _edgeBlend = Mathf.MoveTowards(_edgeBlend, inside ? 0f : 1f, dt * 7f);

        // ----- สิ่งกีดขวางบังหมุด -----
        if (Time.unscaledTime >= _nextOcclusionCheck)
        {
            _nextOcclusionCheck = Time.unscaledTime + 0.15f;
            _occluded = IsOccluded(targetWorldPos);
        }
        _occlusionAlpha = Mathf.MoveTowards(_occlusionAlpha, _occluded ? 0.5f : 1f, dt * 3f);

        UpdateWorldMarker(iconPos, arrivalFade);
        UpdateEdgeIndicator(edgePos, edgeDir, arrivalFade);
        UpdateCompassMarker(targetWorldPos, arrivalFade);
        UpdateArrivalPulse(iconPos, dt);
    }

    private void HideTargetVisuals()
    {
        _worldGroup.alpha = 0f;
        _edgeGroup.alpha = 0f;
        _compassMarker.gameObject.SetActive(false);
        _compassDist.gameObject.SetActive(false);
        _pulseImage.color = new Color(1f, 1f, 1f, 0f);
    }

    private void UpdateWorldMarker(Vector2 iconPos, float arrivalFade)
    {
        _worldGroup.alpha = (1f - _edgeBlend) * arrivalFade * _occlusionAlpha;

        float distScale = Mathf.Lerp(1.12f, 0.74f, Mathf.InverseLerp(8f, 160f, CurrentDistance));
        float bob = Mathf.Sin(Time.unscaledTime * 2.4f) * 3f * _uiScale;
        _worldRoot.position = new Vector3(iconPos.x, iconPos.y + bob, 0f);
        _worldRoot.localScale = Vector3.one * distScale;

        float breathe = 1f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.045f;
        _icon.rectTransform.localScale = Vector3.one * breathe;

        // วงแหวนพัลส์ (ping) ขยายและจางทุก 1.9 วินาที
        float ping = Mathf.Repeat(Time.unscaledTime / 1.9f, 1f);
        _pingRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.85f, 2.0f, ping);
        _pingRing.color = new Color(Brass.r, Brass.g, Brass.b, Mathf.Lerp(0.75f, 0f, ping));

        // ชื่อสถานที่โผล่เมื่อเข้าใกล้
        _nameGroup.alpha = Mathf.InverseLerp(_nameRevealDistance, _nameRevealDistance * 0.6f, CurrentDistance);

        // แสงลำพุ่งลงสู่พื้นจากหมุด (ใช้ตำแหน่งฐานของเป้าหมาย)
        float beamPx = 0f;
        Vector3 ground = _mainCamera.WorldToScreenPoint(_targetTransform.position);
        if (ground.z > 0.01f)
        {
            beamPx = Mathf.Clamp(iconPos.y - ground.y, 0f, 480f * _uiScale);
        }
        float nearFade = Mathf.InverseLerp(4f, 14f, CurrentDistance);
        float len = beamPx / (_uiScale * Mathf.Max(0.01f, distScale));
        _beam.rectTransform.sizeDelta = new Vector2(3.5f, len * nearFade);
    }

    private void UpdateEdgeIndicator(Vector2 edgePos, Vector2 edgeDir, float arrivalFade)
    {
        _edgeGroup.alpha = _edgeBlend * arrivalFade * 0.95f;
        _edgeRoot.position = new Vector3(edgePos.x, edgePos.y, 0f);

        Vector2 dirN = edgeDir.normalized;
        float angle = Mathf.Atan2(dirN.y, dirN.x) * Mathf.Rad2Deg;
        _chevron.localEulerAngles = new Vector3(0f, 0f, angle);
        _chevron.anchoredPosition = dirN * 6f;
        _edgeIconRoot.anchoredPosition = -dirN * 46f;
    }

    private void UpdateCompassMarker(Vector3 targetWorldPos, float arrivalFade)
    {
        Vector3 flat = targetWorldPos - _mainCamera.transform.position;
        flat.y = 0f;
        float bearing = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(_camYaw, bearing);

        float x = Mathf.Clamp(delta / CompassHalfFov, -1.06f, 1.06f) * CompassHalfWidth;
        bool pinned = Mathf.Abs(delta) > CompassHalfFov;
        float a = (pinned ? 0.7f : 1f) * Mathf.Lerp(0.35f, 1f, arrivalFade);

        _compassMarker.gameObject.SetActive(true);
        _compassDist.gameObject.SetActive(true);
        _compassMarker.anchoredPosition = new Vector2(x, -19f);
        _compassMarkerImg.color = new Color(1f, 1f, 1f, a);
        _compassDist.rectTransform.anchoredPosition = new Vector2(x, 15f);
        _compassDist.color = new Color(Paper.r, Paper.g, Paper.b, a);
    }

    private void UpdateArrivalPulse(Vector2 iconPos, float dt)
    {
        _pulseTimer += dt;
        const float duration = 1.0f;
        if (_pulseTimer >= duration)
        {
            _pulseImage.color = new Color(Brass.r, Brass.g, Brass.b, 0f);
            return;
        }

        float k = _pulseTimer / duration;
        _pulseRing.position = new Vector3(iconPos.x, iconPos.y, 0f);
        _pulseRing.localScale = Vector3.one * Mathf.Lerp(0.8f, 4.2f, 1f - Mathf.Pow(1f - k, 3f));
        _pulseImage.color = new Color(1f, 0.9f, 0.55f, Mathf.Lerp(0.9f, 0f, k));
    }

    // ==========================================
    // Compass
    // ==========================================
    private void UpdateCompassTicks()
    {
        for (int i = 0; i < TickCount; i++)
        {
            float angle = i * 15f;
            float delta = Mathf.DeltaAngle(_camYaw, angle);
            float abs = Mathf.Abs(delta);

            if (abs > CompassHalfFov)
            {
                _tickImages[i].color = new Color(1f, 1f, 1f, 0f);
                if (_tickLabels[i] != null) _tickLabels[i].color = new Color(1f, 1f, 1f, 0f);
                continue;
            }

            float x = delta / CompassHalfFov * CompassHalfWidth;
            float fade = Mathf.Pow(1f - abs / CompassHalfFov, 0.6f);
            bool major = i % 3 == 0;

            _tickRects[i].anchoredPosition = new Vector2(x, -31f);
            _tickImages[i].color = new Color(Brass.r, Brass.g, Brass.b, fade * (major ? 1f : 0.65f));

            if (_tickLabels[i] != null)
            {
                _tickLabels[i].rectTransform.anchoredPosition = new Vector2(x, -13f);
                Color c = i == 0 ? AlertRed : (i % 6 == 0 ? Paper : new Color(Brass.r, Brass.g, Brass.b, 1f));
                _tickLabels[i].color = new Color(c.r, c.g, c.b, fade);
            }
        }

        int heading = Mathf.RoundToInt(Mathf.Repeat(_camYaw, 360f)) % 360;
        int nameIndex = Mathf.RoundToInt(heading / 45f) % 8;
        _headingText.text = $"{CardinalNames[nameIndex]}  {heading:000}°";
    }

    // ==========================================
    // Helpers
    // ==========================================
    private static string FormatDistanceRich(float meters)
    {
        if (meters >= 1000f) return $"{meters / 1000f:0.0}<size=15>km</size>";
        return $"{Mathf.RoundToInt(meters)}<size=15>m</size>";
    }

    private static string FormatDistancePlain(float meters)
    {
        if (meters >= 1000f) return $"{meters / 1000f:0.0}km";
        return $"{Mathf.RoundToInt(meters)}m";
    }

    private static Vector2 ClampToRect(Vector2 center, Vector2 dir, float halfW, float halfH)
    {
        float tx = Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
        float ty = Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
        float t = Mathf.Min(tx, ty);
        return center + dir * t;
    }

    /// <summary>กันหมุดและตัวชี้ทิศไม่ให้ลอยทับแผงเควสที่มุมซ้ายบน</summary>
    private void AvoidQuestHud(ref Vector2 pos)
    {
        float hudRight = 720f * _uiScale;
        float hudBottom = Screen.height - 190f * _uiScale;
        if (pos.x < hudRight && pos.y > hudBottom) pos.y = hudBottom;
    }

    private bool IsOccluded(Vector3 targetWorldPos)
    {
        Vector3 camPos = _mainCamera.transform.position;
        if (!Physics.Linecast(camPos, targetWorldPos, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)) return false;

        Transform t = hit.collider.transform;
        if (_targetTransform != null && t.IsChildOf(_targetTransform)) return false;
        string rootName = t.root.name;
        if (rootName.Contains("Player") || rootName.Contains("Newman")) return false;
        if (hit.collider.GetComponentInParent<EvidenceItem>() != null) return false;

        // ถ้าชนใกล้เป้าหมายมาก ถือว่าเป็นพื้นผิวของเป้าหมายเอง
        return Vector3.Distance(hit.point, targetWorldPos) > 1.2f;
    }

    // ==========================================
    // UI construction
    // ==========================================
    private void BuildWaypointUI()
    {
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
        {
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 450;
            }
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null && GetComponentInParent<CanvasScaler>() == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // ล้าง UI เก่า (ทั้งรุ่นก่อนหน้าและรุ่นนี้)
        foreach (var oldName in new[] { "Waypoint_HUD", "Genshin_Waypoint_HUD", "Detective_Waypoint_HUD" })
        {
            Transform old = transform.Find(oldName);
            if (old == null) continue;
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }

        var rootGo = new GameObject("Detective_Waypoint_HUD", typeof(RectTransform));
        rootGo.transform.SetParent(transform, false);
        _hudRoot = (RectTransform)rootGo.transform;
        _hudRoot.anchorMin = Vector2.zero;
        _hudRoot.anchorMax = Vector2.one;
        _hudRoot.offsetMin = Vector2.zero;
        _hudRoot.offsetMax = Vector2.zero;

        BuildCompass();
        BuildWorldMarker();
        BuildEdgeIndicator();

        _hudRoot.gameObject.SetActive(_showWaypoint);
    }

    private void BuildCompass()
    {
        _tickRects = new RectTransform[TickCount];
        _tickImages = new Image[TickCount];
        _tickLabels = new Text[TickCount];

        _compassRoot = MakeRect("Compass", _hudRoot, new Vector2(0.5f, 1f), new Vector2(0f, CompassTop), new Vector2(CompassWidth, 40f), new Vector2(0.5f, 1f));

        // พื้นแถบโปร่งแสงซีดจางที่ปลายทั้งสองข้าง + เส้นทองเหลืองบน/ล่าง
        var back = MakeImage("Back", _compassRoot, new Vector2(0.5f, 1f), new Vector2(0f, -19f), new Vector2(CompassWidth, 38f), new Color(0.04f, 0.03f, 0.02f, 0.62f), WaypointSprites.Strip);
        MakeImage("LineTop", _compassRoot, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(CompassWidth, 1.5f), new Color(Brass.r, Brass.g, Brass.b, 0.75f), WaypointSprites.Strip);
        MakeImage("LineBottom", _compassRoot, new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(CompassWidth, 1.5f), new Color(Brass.r, Brass.g, Brass.b, 0.75f), WaypointSprites.Strip);

        for (int i = 0; i < TickCount; i++)
        {
            bool major = i % 3 == 0;
            bool cardinal = i % 6 == 0;
            Vector2 size = cardinal ? new Vector2(2.5f, 12f) : (major ? new Vector2(2f, 9f) : new Vector2(1.5f, 6f));
            var tick = MakeImage("Tick_" + i * 15, _compassRoot, new Vector2(0.5f, 1f), new Vector2(0f, -31f), size, Color.white);
            _tickRects[i] = tick.rectTransform;
            _tickImages[i] = tick;

            if (major)
            {
                string label = CardinalNames[i / 3];
                var text = MakeText("Label_" + label, _compassRoot, new Vector2(0.5f, 1f), new Vector2(0f, -13f), new Vector2(60f, 24f),
                    label, thaiFontBold, cardinal ? 22 : 15, Brass, TextAnchor.MiddleCenter);
                _tickLabels[i] = text;
            }
        }

        // ลูกศรชี้กึ่งกลาง (ทิศที่กำลังหัน)
        var notch = MakeImage("Notch", _compassRoot, new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(13f, 13f), Brass, WaypointSprites.Chevron);
        notch.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
        _headingText = MakeText("Heading", _compassRoot, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(200f, 22f),
            "N  000°", _typewriterFont, 15, new Color(Brass.r, Brass.g, Brass.b, 0.85f), TextAnchor.MiddleCenter);

        // ไอคอนเป้าหมายบนเข็มทิศ
        var marker = MakeImage("TargetMarker", _compassRoot, new Vector2(0.5f, 1f), new Vector2(0f, -19f), new Vector2(24f, 24f), Color.white, WaypointSprites.Diamond);
        _compassMarker = marker.rectTransform;
        _compassMarkerImg = marker;
        AddShadow(marker.gameObject, 1.5f);
        _compassDist = MakeText("TargetDist", _compassRoot, new Vector2(0.5f, 1f), new Vector2(0f, 15f), new Vector2(100f, 22f),
            "", thaiFontBold, 15, Paper, TextAnchor.MiddleCenter);
        AddShadow(_compassDist.gameObject, 1.2f);
    }

    private void BuildWorldMarker()
    {
        _worldRoot = MakeRect("WorldMarker", _hudRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 200f), new Vector2(0.5f, 0.5f));
        _worldGroup = _worldRoot.gameObject.AddComponent<CanvasGroup>();
        _worldGroup.blocksRaycasts = false;
        _worldGroup.interactable = false;

        _beam = MakeImage("Beam", _worldRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(3.5f, 0f), new Color(Brass.r, Brass.g, Brass.b, 0.9f), WaypointSprites.Beam, new Vector2(0.5f, 1f));

        MakeImage("Glow", _worldRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f), new Color(1f, 0.78f, 0.3f, 0.3f), WaypointSprites.Glow);
        _pingRing = MakeImage("Ping", _worldRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 54f), new Color(Brass.r, Brass.g, Brass.b, 0.7f), DetectiveUIShapes.Ring(0.04f));
        MakeImage("Ring", _worldRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 54f), new Color(Brass.r, Brass.g, Brass.b, 0.55f), DetectiveUIShapes.Ring(0.02f));

        _icon = MakeImage("Diamond", _worldRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f), Color.white, WaypointSprites.Diamond);
        AddShadow(_icon.gameObject, 2f);

        // ชื่อสถานที่ + เส้นขีดใต้ชื่อ (โผล่เมื่อเข้าใกล้)
        _nameText = MakeText("Name", _worldRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, 52f), new Vector2(360f, 28f),
            "", thaiFontBold, 20, new Color(1f, 0.95f, 0.8f, 1f), TextAnchor.MiddleCenter);
        AddShadow(_nameText.gameObject, 1.5f);
        _nameGroup = _nameText.gameObject.AddComponent<CanvasGroup>();
        _nameGroup.alpha = 0f;
        var nameLine = MakeImage("NameLine", _nameText.transform, new Vector2(0.5f, 0f), new Vector2(0f, -3f), new Vector2(150f, 2f), new Color(Brass.r, Brass.g, Brass.b, 0.9f), WaypointSprites.Strip);
        nameLine.raycastTarget = false;

        _distText = MakeText("Dist", _worldRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(160f, 30f),
            "", thaiFontBold, 22, new Color(1f, 0.96f, 0.86f, 1f), TextAnchor.MiddleCenter);
        _distText.supportRichText = true;
        AddShadow(_distText.gameObject, 1.5f);

        // วงแหวนระเบิดตอนถึงเป้าหมาย (แยกจากกลุ่มหมุดเพื่อไม่ถูกจางตาม)
        _pulseRing = MakeRect("ArrivalPulse", _hudRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f), new Vector2(0.5f, 0.5f));
        _pulseImage = _pulseRing.gameObject.AddComponent<Image>();
        _pulseImage.sprite = DetectiveUIShapes.Ring(0.07f);
        _pulseImage.color = new Color(Brass.r, Brass.g, Brass.b, 0f);
        _pulseImage.raycastTarget = false;
    }

    private void BuildEdgeIndicator()
    {
        _edgeRoot = MakeRect("EdgeIndicator", _hudRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f), new Vector2(0.5f, 0.5f));
        _edgeGroup = _edgeRoot.gameObject.AddComponent<CanvasGroup>();
        _edgeGroup.blocksRaycasts = false;
        _edgeGroup.interactable = false;

        var chevron = MakeImage("Chevron", _edgeRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f), Brass, WaypointSprites.Chevron);
        _chevron = chevron.rectTransform;
        AddShadow(chevron.gameObject, 2f);

        _edgeIconRoot = MakeRect("IconRoot", _edgeRoot, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 60f), new Vector2(0.5f, 0.5f));
        var icon = MakeImage("Diamond", _edgeIconRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(28f, 28f), Color.white, WaypointSprites.Diamond);
        AddShadow(icon.gameObject, 1.5f);
        _edgeDist = MakeText("Dist", _edgeIconRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, -17f), new Vector2(100f, 24f),
            "", thaiFontBold, 18, new Color(1f, 0.96f, 0.86f, 1f), TextAnchor.MiddleCenter);
        AddShadow(_edgeDist.gameObject, 1.3f);
    }

    // ---- UI factory ----
    private static RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = anchor;
        r.anchorMax = anchor;
        r.pivot = pivot;
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        return r;
    }

    private static Image MakeImage(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, Sprite sprite = null, Vector2? pivot = null)
    {
        var r = MakeRect(name, parent, anchor, pos, size, pivot ?? new Vector2(0.5f, 0.5f));
        var img = r.gameObject.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    private static Text MakeText(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string text, Font font, int fontSize, Color color, TextAnchor align)
    {
        var r = MakeRect(name, parent, anchor, pos, size, new Vector2(0.5f, 0.5f));
        var t = r.gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void AddShadow(GameObject go, float distance)
    {
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(distance, -distance);
    }
}

/// <summary>สไปรต์ที่สร้างด้วยโค้ดสำหรับระบบนำทาง (เพชรทองเหลือง, ลูกศรชี้ทิศ, แสงลำ, แถบเข็มทิศ, แสงเรือง)</summary>
public static class WaypointSprites
{
    private static Sprite _diamond;
    private static Sprite _chevron;
    private static Sprite _beam;
    private static Sprite _strip;
    private static Sprite _glow;

    /// <summary>เพชรทองเหลือง ขอบทอง ตรงกลางหมึกเข้ม แกนกลางสว่าง</summary>
    public static Sprite Diamond
    {
        get
        {
            if (_diamond != null) return _diamond;
            const int size = 64;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            Color gold = new Color(0.95f, 0.76f, 0.30f, 1f);
            Color ink = new Color(0.12f, 0.085f, 0.055f, 1f);
            Color core = new Color(1f, 0.95f, 0.78f, 1f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - half);
                    float dy = Mathf.Abs(y - half);
                    float edge = half - (dx + dy); // ระยะถึงขอบ (เมตริก Manhattan)
                    float alpha = Mathf.Clamp01(edge * 0.7f + 0.5f);
                    Color c;
                    if (edge < half * 0.20f) c = gold;
                    else if (edge < half * 0.50f) c = ink;
                    else c = Color.Lerp(core, gold, Mathf.InverseLerp(half, half * 0.5f, edge));
                    px[y * size + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(alpha * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _diamond = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _diamond;
        }
    }

    /// <summary>หัวลูกศรเชฟรอนชี้ไปทางขวา (+X) หมุนด้วย localEulerAngles.z ให้ชี้ไปทางเป้าหมาย</summary>
    public static Sprite Chevron
    {
        get
        {
            if (_chevron != null) return _chevron;
            const int size = 64;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            Vector2 tip = new Vector2(60f, 32f);
            Vector2 topBack = new Vector2(8f, 4f);
            Vector2 botBack = new Vector2(8f, 60f);
            Vector2 notchTip = new Vector2(30f, 32f);
            Vector2 notchTop = new Vector2(6f, 14f);
            Vector2 notchBot = new Vector2(6f, 50f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++)
                    {
                        for (int sx = 0; sx < 4; sx++)
                        {
                            Vector2 p = new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f);
                            if (InTriangle(p, tip, topBack, botBack) && !InTriangle(p, notchTip, notchTop, notchBot)) hits++;
                        }
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _chevron = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _chevron;
        }
    }

    /// <summary>แสงลำแนวตั้ง (บนสว่าง ล่างจาง)</summary>
    public static Sprite Beam
    {
        get
        {
            if (_beam != null) return _beam;
            const int w = 4, h = 128;
            var tex = NewTexture(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float a = Mathf.Pow(y / (float)(h - 1), 1.3f);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _beam = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            return _beam;
        }
    }

    /// <summary>แถบแนวนอนที่จางหายตรงปลายทั้งสองข้าง (ใช้เป็นพื้นและเส้นของเข็มทิศ)</summary>
    public static Sprite Strip
    {
        get
        {
            if (_strip != null) return _strip;
            const int w = 256, h = 4;
            var tex = NewTexture(w, h);
            var px = new Color32[w * h];
            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs((x / (float)(w - 1)) * 2f - 1f);
                float a = 1f - Mathf.Pow(u, 3f);
                for (int y = 0; y < h; y++) px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _strip = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            return _strip;
        }
    }

    /// <summary>แสงเรืองวงกลมไล่ระดับ</summary>
    public static Sprite Glow
    {
        get
        {
            if (_glow != null) return _glow;
            const int size = 64;
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _glow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _glow;
        }
    }

    private static Texture2D NewTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);
        bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNeg && hasPos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }
}
