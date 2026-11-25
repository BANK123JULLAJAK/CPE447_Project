using System;
using UnityEngine;

public enum ShotEase { Smooth, Linear, EaseOut }

/// <summary>
/// คำอธิบายช็อตกล้องหนึ่งช็อต (ตำแหน่ง/จุดเล็ง เป็นฟังก์ชันของเวลา 0..1) ใช้ร่วมกับ CinematicCutsceneManager.PlayShots
/// สร้างผ่าน static class Shot ที่จัดเฟรมแบบอิงตัวละคร (Over-the-Shoulder, Close-Up, Orbit, Crane ฯลฯ)
/// </summary>
public class ShotSpec
{
    public Func<float, Vector3> pos;
    public Func<float, Vector3> aim;
    public float duration = 6f;
    public ShotEase ease = ShotEase.Smooth;

    public float fov = 35f;
    public float fovEnd = -1f;
    public float roll;            // Dutch angle (องศา)
    public float rollEnd = float.NaN;

    public float shake = 0.3f;    // แอมพลิจูดกล้องถือมือ
    public float focal = 50f;     // DOF: ความยาวโฟกัส (mm)
    public float aperture = 2.8f; // DOF: f-stop
    public float rackOffset = 1.2f; // เริ่มโฟกัสเลยเป้าหมายแล้วดึงกลับ (เมตร)
    public float rackTime = 0.8f;
    public bool avoidWalls = true;
    public float minDistance = 0.45f;

    public ShotSpec Lens(float from, float to = -1f) { fov = from; fovEnd = to; return this; }
    public ShotSpec Dutch(float from, float to = float.NaN) { roll = from; rollEnd = to; return this; }
    public ShotSpec Shake(float amplitude) { shake = amplitude; return this; }
    public ShotSpec Dof(float focalLength, float fStop, float rack = 1.2f) { focal = focalLength; aperture = fStop; rackOffset = rack; return this; }
    public ShotSpec Eased(ShotEase e) { ease = e; return this; }
    public ShotSpec Macro() { minDistance = 0.2f; return this; }
}

/// <summary>ตัวสร้างช็อตกล้องสไตล์ภาพยนตร์ AAA จัดเฟรมสัมพัทธ์กับตัวละครจริงในฉาก (ไม่ผูกพิกัดโลกตายตัว)</summary>
public static class Shot
{
    public static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized;
    }

    public static Vector3 Right(Vector3 flatDir) { return Vector3.Cross(Vector3.up, flatDir).normalized; }

    /// <summary>ตำแหน่งศีรษะของตัวละคร (ใช้กระดูก Head ถ้ามี ไม่งั้นประมาณจากราก)</summary>
    public static Vector3 Head(Transform t, float fallbackUp = 1.6f)
    {
        if (t == null) return Vector3.zero;
        var anim = t.GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            var h = anim.GetBoneTransform(HumanBodyBones.Head);
            if (h != null) return h.position;
        }
        return t.position + Vector3.up * fallbackUp;
    }

    public static float AngleTo(Vector3 center, Vector3 point)
    {
        Vector3 d = point - center;
        return Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
    }

    /// <summary>ช็อตทั่วไป: กล้องเลื่อนจาก p0 ไป p1 พร้อมเล็งจาก a0 ไป a1</summary>
    public static ShotSpec Move(Vector3 p0, Vector3 p1, Vector3 a0, Vector3 a1, float duration)
    {
        return new ShotSpec
        {
            pos = t => Vector3.Lerp(p0, p1, t),
            aim = t => Vector3.Lerp(a0, a1, t),
            duration = duration,
        };
    }

    /// <summary>กล้องเลื่อนจาก p0 ไป p1 เล็งจุดเดียวตลอด</summary>
    public static ShotSpec Move(Vector3 p0, Vector3 p1, Vector3 aim, float duration)
    {
        return Move(p0, p1, aim, aim, duration);
    }

    /// <summary>กล้องโคจรรอบจุดศูนย์กลาง (Arc/Orbit) ความสูงคงที่หรือเปลี่ยนได้</summary>
    public static ShotSpec Orbit(Vector3 center, float radius, float height0, float height1, float deg0, float deg1, Vector3 aim, float duration)
    {
        return new ShotSpec
        {
            pos = t =>
            {
                float a = Mathf.Lerp(deg0, deg1, t) * Mathf.Deg2Rad;
                float h = Mathf.Lerp(height0, height1, t);
                return new Vector3(center.x + Mathf.Cos(a) * radius, center.y + h, center.z + Mathf.Sin(a) * radius);
            },
            aim = t => aim,
            duration = duration,
        };
    }

    /// <summary>
    /// Over-the-Shoulder: กล้องอยู่หลังไหล่ตัวละคร (headPos) มองผ่านไหล่ไปยังเป้าหมาย
    /// back = ระยะถอยหลัง, side = เยื้องขวา(+)/ซ้าย(-), up = สูง/ต่ำกว่าศีรษะ, push = ดอลลี่เข้าไปกี่เมตร
    /// </summary>
    public static ShotSpec OverShoulder(Vector3 headPos, Vector3 target, float back, float side, float up, float push, float duration)
    {
        Vector3 f = Flat(target - headPos);
        Vector3 r = Right(f);
        Vector3 p0 = headPos - f * back + r * side + Vector3.up * up;
        Vector3 p1 = p0 + f * push;
        Vector3 a = target;
        return Move(p0, p1, a, a, duration);
    }

    /// <summary>
    /// โคลสอัพใบหน้า: กล้องอยู่ด้านหน้าตัวละครตามทิศ toward (ทิศจากหน้าตัวละครไปหากล้อง)
    /// dist = ระยะห่าง, side = เยื้องข้าง, up = ต่ำ/สูง, push = ดอลลี่เข้า
    /// </summary>
    public static ShotSpec Face(Vector3 headPos, Vector3 toward, float dist, float side, float up, float push, float duration)
    {
        Vector3 f = Flat(toward);
        Vector3 r = Right(f);
        Vector3 p0 = headPos + f * dist + r * side + Vector3.up * up;
        Vector3 p1 = headPos + f * (dist - push) + r * side * 0.8f + Vector3.up * up * 0.8f;
        Vector3 a = headPos + Vector3.up * 0.02f;
        return Move(p0, p1, a, a, duration);
    }
}
