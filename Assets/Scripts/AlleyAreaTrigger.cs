using UnityEngine;

/// <summary>
/// พื้นที่ตรวจจับการเดินเข้าสู่ตรอกไวท์ชาเปล (Whitechapel Alley Area Trigger)
/// เมื่อผู้เล่นเดินเข้าสู่บริเวณตรอก จะเริ่มเปิดคัตซีนภาพยนตร์ 3 ช็อต และเริ่มการสืบสวน Scene 2
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class AlleyAreaTrigger : MonoBehaviour
{
    [Header("=== การตั้งค่า Trigger ===")]
    public bool triggerOnce = true;
    public bool showGizmos = true;

    private bool _hasTriggered = false;

    private void Awake()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered && triggerOnce) return;

        bool isPlayer = other.CompareTag("Player")
                     || other.GetComponentInParent<StarterAssets.ThirdPersonController>() != null
                     || other.GetComponent<CharacterController>() != null
                     || other.name.Contains("Player")
                     || other.name.Contains("Newman");

        if (isPlayer)
        {
            // ตรวจสอบว่าผู้เล่นเล่นจบ Scene 1 และปลดล็อคเควสที่ 2 แล้วหรือยัง
            if (LondonAlleyQuestManager.Instance == null || !LondonAlleyQuestManager.Instance.IsQuest2Active)
            {
                return;
            }

            _hasTriggered = true;
            Debug.Log("<color=cyan><b>[ALLEY AREA]</b> ผู้เล่นเดินเข้าสู่บริเวณตรอกไวท์ชาเปลแล้ว! กำลังเริ่มเล่นคัตซีน Scene 2...</color>");

            LondonAlleyQuestManager.Instance.OnEnterAlleyArea();
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.35f);
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
