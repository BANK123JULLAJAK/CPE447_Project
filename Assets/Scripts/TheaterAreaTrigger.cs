using UnityEngine;

/// <summary>
/// ทริกเกอร์ตรวจจับเมื่อผู้เล่นก้าวเข้าสู่บริเวณโรงละครรอยัลไลเซียม (The Royal Lyceum Theatre)
/// เพื่อเริ่มคัตซีนภาพยนตร์ Scene 3: เฉียดฉิว (Jack The Ripper Narrow Escape Cutscene)
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TheaterAreaTrigger : MonoBehaviour
{
    private bool _hasTriggered = false;

    private void Awake()
    {
        var col = GetComponent<BoxCollider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered) return;

        // ตรวจสอบว่าเป็นผู้เล่น (Player / Newman)
        bool isPlayer = other.CompareTag("Player") 
                     || other.name.Contains("Newman") 
                     || other.name.Contains("Player")
                     || other.GetComponentInParent<StarterAssets.ThirdPersonController>() != null;

        if (isPlayer)
        {
            if (RoyalLyceumTheaterQuestManager.Instance != null && !RoyalLyceumTheaterQuestManager.Instance.IsQuest3Active)
            {
                return; // เควส 3 ยังไม่เริ่ม (ผู้เล่นยังเล่นเควส 1 หรือ 2 อยู่)
            }

            _hasTriggered = true;
            Debug.Log("<color=#F2C94C><b>[THEATER TRIGGER]</b> ผู้เล่นก้าวเข้าสู่โรงละครรอยัลไลเซียม! เริ่มต้นคัตซีนภาพยนตร์ Scene 3</color>");

            if (RoyalLyceumTheaterQuestManager.Instance != null)
            {
                RoyalLyceumTheaterQuestManager.Instance.OnPlayerEnteredTheater();
            }
        }
    }

    public void ResetTrigger()
    {
        _hasTriggered = false;
    }
}
