using UnityEngine;

/// <summary>
/// วัตถุพยานในที่เกิดเหตุ (Scene 1: โบสถ์เซนต์แมรี่)
/// เชื่อมโยงกับโมเดล CiRA CORE: prayer_sheet, handkerchief, ring, footprint
/// </summary>
[DisallowMultipleComponent]
public class EvidenceItem : MonoBehaviour
{
    [Header("=== ข้อมูลวัตถุพยาน (Evidence Data) ===")]
    [Tooltip("รหัสประจำหลักฐาน เช่น footprint, handkerchief, prayer_sheet, ring_large, ring_small")]
    public string evidenceId = "evidence_01";

    [Tooltip("ชื่อคลาสที่ตรงกับไฟล์ obj.names ของ CiRA CORE")]
    public string ciraClassName = "ring";

    [Tooltip("ชื่อภาษาไทยของหลักฐาน")]
    public string thaiTitle = "แหวนทองคำโบราณ";

    [Tooltip("คำอธิบายทางนิติวิทยาศาสตร์ภาษาไทย")]
    [TextArea(2, 5)]
    public string thaiDescription = "แหวนทองคำสลักลวดลายวิกตอเรีย";

    [Tooltip("ระยะที่ระบบ CiRA CORE สามารถตรวจจับได้ (เมตร)")]
    public float detectionRange = 12f;

    [Tooltip("สถานะการสแกนแล้ว")]
    public bool isScanned = false;

    [Header("=== การแสดงผล Marker ===")]
    [Tooltip("แสดงแสง Marker บนวัตถุเมื่อเข้าใกล้")]
    public bool showWorldMarker = true;

    private Collider _col;

    private void Awake()
    {
        _col = GetComponent<Collider>();
        if (_col == null)
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            Vector3 lossy = transform.lossyScale;
            box.size = new Vector3(
                0.65f / Mathf.Max(0.0001f, lossy.x),
                0.35f / Mathf.Max(0.0001f, lossy.y),
                0.65f / Mathf.Max(0.0001f, lossy.z)
            );
            box.center = Vector3.zero;
            _col = box;
        }
    }

    public Vector3 GetWorldCenter()
    {
        if (_col != null) return _col.bounds.center;
        return transform.position;
    }

    public Bounds GetWorldBounds()
    {
        if (_col != null) return _col.bounds;
        return new Bounds(transform.position, new Vector3(0.65f, 0.35f, 0.65f));
    }
}
