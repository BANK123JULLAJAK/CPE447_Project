using UnityEngine;

/// <summary>
/// ตัวตรวจจับการเข้าสู่สถานีรถไฟใต้ดิน (Underground Station Area Trigger)
/// เมื่อผู้เล่นเดินลงบันไดมาถึงบริเวณสถานี จะเปิดใช้งานเควสและการแจ้งเตือนของ Scene 4
/// </summary>
public class StationAreaTrigger : MonoBehaviour
{
    private bool _triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;

        if (other.CompareTag("Player") ||
            other.name.Contains("Player") ||
            other.name.Contains("Armature") ||
            other.name.Contains("Newman"))
        {
            _triggered = true;
            if (UndergroundStationQuestManager.Instance != null)
            {
                UndergroundStationQuestManager.Instance.OnPlayerEnteredStation();
            }
        }
    }
}
