using UnityEngine;

/// <summary>
/// ระบบ Contact Shadow / Grounded Shadow สไตล์ Genshin Impact & Arknights: Endfield
/// ฉายเงาสัมผัสใต้ฝ่าเท้าของตัวละครลงบนพื้นผิวถนนหินแบบไดนามิก
/// ป้องกันตัวละครดู "ลอย" จากฉาก 100% ทำให้ตัวละครแนบเนียนไปกับพื้นถนนอย่างสมบูรณ์แบบ
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class CharacterGroundedShadow : MonoBehaviour
{
    [Header("=== ตั้งค่าเงาสัมผัส (Genshin / Arknights Contact Shadow) ===")]
    [Tooltip("ขนาดของเงาใต้ฝ่าเท้า (กว้าง x ยาว)")]
    public Vector2 shadowSize = new Vector2(0.95f, 0.95f);

    [Tooltip("ความเข้มสูงสุดของเงา")]
    [Range(0.2f, 1.0f)]
    public float maxAlpha = 0.75f;

    [Tooltip("ระยะยกตัวเหนือพื้นเพื่อกัน Z-fighting")]
    public float groundOffset = 0.008f;

    [Tooltip("Layer ของพื้นผิวถนน/สิ่งแวดล้อม")]
    public LayerMask groundLayers = 1; // Default layer

    [Tooltip("Material เงาโปร่งแสง")]
    public Material shadowMaterial;

    private GameObject _shadowQuad;
    private MeshRenderer _shadowRenderer;
    private MaterialPropertyBlock _mpb;

    private void OnEnable()
    {
        EnsureShadowQuad();
    }

    private void Start()
    {
        EnsureShadowQuad();
    }

    private void Update()
    {
        UpdateShadowPosition();
    }

    private void EnsureShadowQuad()
    {
        if (_shadowQuad == null)
        {
            Transform existing = transform.Find("Grounded_Contact_Shadow");
            if (existing != null)
            {
                _shadowQuad = existing.gameObject;
            }
            else
            {
                _shadowQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _shadowQuad.name = "Grounded_Contact_Shadow";
                _shadowQuad.transform.SetParent(transform, false);

                // Remove collider so it never interferes with character movement or raycasts
                Collider col = _shadowQuad.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);
            }
        }

        if (_shadowRenderer == null && _shadowQuad != null)
        {
            _shadowRenderer = _shadowQuad.GetComponent<MeshRenderer>();
            _shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _shadowRenderer.receiveShadows = false;
            _shadowRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _shadowRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        if (shadowMaterial == null)
        {
            shadowMaterial = GameAssets.Load<Material>("Assets/3D_Model/VAM/CH1/M_Newman_ContactShadow.mat");
        }

        if (shadowMaterial != null && shadowMaterial.shader != null && shadowMaterial.shader.name.ToLower().Contains("liltoon"))
            shadowMaterial = null; // lilToon ทึบ -> ได้แผ่นขาว ใช้วัสดุโปร่งใสสำรองแทน

        if (shadowMaterial == null)
        {
            // Fallback runtime: วงเงานุ่มจากสไปรต์ Glow สีดำ (ใช้ได้ทั้งใน Build และ URP)
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                shadowMaterial = new Material(shader) { name = "RuntimeContactShadow", mainTexture = WaypointSprites.Glow.texture, color = new Color(0f, 0f, 0f, 1f) };
                shadowMaterial.renderQueue = 2100;
            }
        }

        if (_shadowRenderer != null && shadowMaterial != null)
        {
            _shadowRenderer.sharedMaterial = shadowMaterial;
        }

        if (_mpb == null)
        {
            _mpb = new MaterialPropertyBlock();
        }
    }

    private void UpdateShadowPosition()
    {
        if (_shadowQuad == null)
        {
            EnsureShadowQuad();
            if (_shadowQuad == null) return;
        }

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 3.5f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            _shadowQuad.SetActive(true);

            // วางตำแหน่งเงาลงบนพื้นผิวพอดี พร้อมยกขึ้นเล็กน้อยกัน z-fighting
            _shadowQuad.transform.position = hit.point + hit.normal * groundOffset;

            // ปรับองศาเงาให้ขนานกับระนาบพื้นผิวถนน
            _shadowQuad.transform.rotation = Quaternion.FromToRotation(Vector3.back, hit.normal) * Quaternion.Euler(0f, 0f, transform.eulerAngles.y);

            // ปรับขนาดและความจางตามความสูง (หากกระโดดลอยสูง เงาจะขยายและจางลงแบบ Genshin)
            float heightAboveGround = hit.distance - 0.5f;
            float heightRatio = Mathf.Clamp01(heightAboveGround / 1.5f);
            float scaleMult = Mathf.Lerp(1.0f, 1.4f, heightRatio);
            float alpha = Mathf.Lerp(maxAlpha, 0.1f, heightRatio);

            _shadowQuad.transform.localScale = new Vector3(shadowSize.x * scaleMult, shadowSize.y * scaleMult, 1f);

            if (_shadowRenderer != null && _mpb != null)
            {
                _mpb.SetColor("_BaseColor", new Color(1f, 1f, 1f, alpha));
                _mpb.SetColor("_Color", new Color(0f, 0f, 0f, alpha));
                _mpb.SetColor("_RendererColor", new Color(0f, 0f, 0f, alpha));
                _shadowRenderer.SetPropertyBlock(_mpb);
            }
        }
        else
        {
            _shadowQuad.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (_shadowQuad != null)
        {
            _shadowQuad.SetActive(false);
        }
    }
}
