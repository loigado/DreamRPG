using UnityEngine;

/// <summary>
/// EnemySlotManager — Hệ thống Slot quỹ đạo quanh Player.
/// 
/// CẢI TIẾN AAA: Slot xoay theo hướng Camera thay vì cố định world axis.
/// → Quái luôn phân bố đẹp trên màn hình bất kể Player quay Camera.
/// </summary>
public class EnemySlotManager : MonoBehaviour
{
    public static EnemySlotManager Instance { get; private set; }

    [Header("Cấu hình Slot")]
    [Tooltip("Số lượng điểm neo (8-12 lý tưởng)")]
    public int slotCount = 10;

    [Tooltip("Bán kính vòng trong (Melee)")]
    public float innerRingRadius = 3f;

    [Tooltip("Bán kính vòng ngoài (Wait)")]
    public float outerRingRadius = 6f;

    [Tooltip("Transform Player (tự tìm nếu để trống)")]
    public Transform playerTarget;

    // === DỮ LIỆU NỘI BỘ ===
    private EnemyStateMachine[] slotOwners;
    private bool[] slotIsInner;
    private Camera mainCam;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        slotOwners  = new EnemyStateMachine[slotCount];
        slotIsInner = new bool[slotCount];

        // Xen kẽ Inner/Outer: chẵn = inner, lẻ = outer
        for (int i = 0; i < slotCount; i++)
            slotIsInner[i] = (i % 2 == 0);
    }

    private void Start()
    {
        mainCam = Camera.main;
        if (playerTarget == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) playerTarget = go.transform;
        }
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    /// <summary>
    /// Đăng ký chiếm Slot. Trả về index (≥0) hoặc -1 nếu hết chỗ.
    /// Ưu tiên slot gần quái nhất + đúng vòng.
    /// </summary>
    public int ReserveSlot(EnemyStateMachine enemy, bool preferInner)
    {
        int bestSlot  = -1;
        float bestDist = float.MaxValue;

        // Lần 1: tìm đúng vòng
        for (int i = 0; i < slotCount; i++)
        {
            if (slotOwners[i] != null) continue;
            if (slotIsInner[i] != preferInner) continue;

            float dist = Vector3.Distance(enemy.transform.position, WorldSlotPosition(i));
            if (dist < bestDist) { bestDist = dist; bestSlot = i; }
        }

        // Lần 2: bất kỳ slot trống nào
        if (bestSlot == -1)
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (slotOwners[i] != null) continue;
                float dist = Vector3.Distance(enemy.transform.position, WorldSlotPosition(i));
                if (dist < bestDist) { bestDist = dist; bestSlot = i; }
            }
        }

        if (bestSlot >= 0)
            slotOwners[bestSlot] = enemy;

        return bestSlot;
    }

    /// <summary>Giải phóng slot khi quái rời StrafeState / chết.</summary>
    public void ReleaseSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotCount) return;
        slotOwners[slotIndex] = null;
    }

    /// <summary>
    /// Vị trí thế giới của slot — XOAY THEO CAMERA.
    /// Slot 0 = phía trước Camera, phân bố đều vòng tròn.
    /// </summary>
    public Vector3 WorldSlotPosition(int slotIndex)
    {
        if (playerTarget == null) return Vector3.zero;

        // Hướng "phía trước" = Camera forward chiếu lên mặt phẳng XZ
        Vector3 camForward = Vector3.forward; // fallback
        if (mainCam != null)
        {
            camForward = mainCam.transform.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude < 0.01f)
                camForward = Vector3.forward;
            else
                camForward.Normalize();
        }

        // Góc cơ sở = hướng camera, cộng thêm offset theo slot index
        float baseAngle = Mathf.Atan2(camForward.x, camForward.z) * Mathf.Rad2Deg;
        float slotAngle = baseAngle + (360f / slotCount) * slotIndex;
        float rad = slotIsInner[slotIndex] ? innerRingRadius : outerRingRadius;

        float x = Mathf.Sin(slotAngle * Mathf.Deg2Rad) * rad;
        float z = Mathf.Cos(slotAngle * Mathf.Deg2Rad) * rad;

        return playerTarget.position + new Vector3(x, 0f, z);
    }

    public float SlotRadius(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotCount) return innerRingRadius;
        return slotIsInner[slotIndex] ? innerRingRadius : outerRingRadius;
    }

    public bool IsInnerSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotCount) return false;
        return slotIsInner[slotIndex];
    }

    /// <summary>Số slot trống hiện tại.</summary>
    public int FreeSlotCount()
    {
        int count = 0;
        for (int i = 0; i < slotCount; i++)
            if (slotOwners[i] == null) count++;
        return count;
    }

    // =========================================================
    // GIZMOS
    // =========================================================
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || playerTarget == null) return;

        for (int i = 0; i < slotCount; i++)
        {
            Vector3 pos = WorldSlotPosition(i);
            bool occupied = slotOwners[i] != null;

            Gizmos.color = occupied
                ? (slotIsInner[i] ? Color.red : Color.yellow)
                : (slotIsInner[i] ? Color.green : Color.cyan);

            Gizmos.DrawWireSphere(pos, 0.35f);

            if (occupied && slotOwners[i] != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(pos, slotOwners[i].transform.position);
            }
        }
    }
}
