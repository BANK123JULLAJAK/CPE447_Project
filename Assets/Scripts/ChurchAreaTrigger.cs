using UnityEngine;

/// <summary>
/// พื้นที่ตรวจจับการเดินเข้าสู่โบสถ์เซนต์แมรี่ (St. Mary Church Area Trigger)
/// เมื่อผู้เล่นเดินเข้าสู่พื้นที่ทางเข้าโบสถ์ จะเริ่มเปิดคัตซีนเนื้อเรื่อง Scene 1
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ChurchAreaTrigger : MonoBehaviour
{
    [Header("=== การตั้งค่า Trigger ===")]
    [Tooltip("เริ่มคัตซีนเฉพาะครั้งแรกที่เดินเข้ามา")]
    public bool triggerOnce = true;

    [Tooltip("แสดง Gizmos ใน Scene View")]
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

        // ตรวจสอบว่าเป็นผู้เล่น (Player, CharacterController, หรือ ThirdPersonController)
        bool isPlayer = other.CompareTag("Player") 
                     || other.GetComponentInParent<StarterAssets.ThirdPersonController>() != null
                     || other.GetComponent<CharacterController>() != null
                     || other.name.Contains("Player") 
                     || other.name.Contains("Newman");

        if (isPlayer)
        {
            _hasTriggered = true;
            Debug.Log("<color=cyan><b>[CHURCH AREA]</b> ผู้เล่นเดินเข้าสู่บริเวณโบสถ์เซนต์แมรี่แล้ว! กำลังเริ่มเล่นเนื้อเรื่อง Scene 1...</color>");

            if (StMaryChurchQuestManager.Instance != null)
            {
                StMaryChurchQuestManager.Instance.OnEnterChurchArea();
            }

            if (triggerOnce)
            {
                // ปิดการทำงาน collider เพื่อป้องกันการ trigger ซ้ำ
                Collider col = GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);
    }
#endif
}
