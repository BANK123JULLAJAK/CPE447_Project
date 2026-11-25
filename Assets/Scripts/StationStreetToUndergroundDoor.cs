using UnityEngine;

/// <summary>
/// ประตูทางลงสถานีรถไฟใต้ดิน (Street Entrance to Underground Station Platform)
/// เมื่อผู้เล่นเดินถึงซุ้มประตูสถานีบนถนนลอนดอน จะพาวาร์ปลงสู่ชานชาลารถไฟใต้ดินเพื่อเริ่มการสืบสวน Scene 4
/// </summary>
public class StationStreetToUndergroundDoor : MonoBehaviour
{
    private bool _hasTransitioned = false;

    private void OnTriggerEnter(Collider other)
    {
        if (_hasTransitioned) return;

        if (other.CompareTag("Player") ||
            other.name.Contains("Player") ||
            other.name.Contains("Armature") ||
            other.name.Contains("Newman"))
        {
            _hasTransitioned = true;
            TransitionToUndergroundPlatform(other.gameObject);
        }
    }

    public void TransitionToUndergroundPlatform(GameObject playerGo)
    {
        var player = playerGo.GetComponentInParent<CharacterController>()?.gameObject
                  ?? GameObject.Find("NestedParentArmature_Newman")
                  ?? GameObject.Find("PlayerArmature")
                  ?? GameObject.Find("Player_CH2_Newman");

        if (player != null)
        {
            var cc = player.GetComponentInChildren<CharacterController>();
            if (cc != null) cc.enabled = false;

            // วาร์ปลงมาที่หัวชานชาลาสถานีรถไฟใต้ดิน
            player.transform.position = new Vector3(-41.50f, -9.76f, 43.00f);
            player.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            if (cc != null) cc.enabled = true;
        }

        if (UndergroundStationQuestManager.Instance != null)
        {
            UndergroundStationQuestManager.Instance.OnPlayerEnteredStation();
        }
    }
}
