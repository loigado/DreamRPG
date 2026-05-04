using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// TargetSystem — Hệ thống Lock-On chuẩn AAA (God of War / FF7R).
///
/// Features:
///   • Hard Lock (Tab) — khóa cứng 1 mục tiêu, camera xoay theo
///   • Soft Lock (tự động) — attack/skill tự bám quái gần nhất trong tầm nhìn
///   • Switch Target — nhấn Tab khi đang lock → chuyển sang quái kế tiếp
///   • Line of Sight check — không lock qua tường
///   • Distance break — mất lock khi quá xa
///   • Dead target auto-release — quái chết → tự hủy lock
///   • Lock icon theo sát target (World-to-Screen)
///   • GOW-style Pivot rotation cho camera
/// </summary>
public class TargetSystem : MonoBehaviour
{
    [Header("Scanning")]
    [Tooltip("Bán kính quét tìm quái")]
    public float scanRadius = 15f;
    [Tooltip("Góc tối đa từ tâm Camera để chấp nhận target")]
    public float maxViewAngle = 70f;
    [Tooltip("Khoảng cách tối đa duy trì lock (xa hơn → tự hủy)")]
    public float maxLockDistance = 20f;
    public LayerMask enemyLayer;
    [Tooltip("Layer chặn Line-of-Sight (tường, đá, ...)")]
    public LayerMask obstructionLayer;

    [Header("GOW Cameras")]
    public CinemachineCamera freeLookCamera;
    public CinemachineCamera lockOnCamera;
    public Transform gowPivot;

    [Header("Lock Icon")]
    public GameObject lockIconPrefab;
    [Tooltip("Offset icon lên trên đầu target (m)")]
    public float iconHeightOffset = 2f;

    // === STATE ===
    public bool IsHardLocking { get; private set; } = false;
    
    private Transform currentTarget;
    private Transform softTarget;
    private Camera mainCam;
    private GameObject currentIcon;
    private PlayerStateMachine stateMachine;

    // === CACHE ===
    private readonly Collider[] scanBuffer = new Collider[30]; // NonAlloc

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        mainCam = Camera.main;
        stateMachine = GetComponent<PlayerStateMachine>();
    }

    private void Update()
    {
        // Lock/Unlock input qua InputReader (nếu có) hoặc fallback Tab
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (IsHardLocking)
                SwitchOrUnlock(); // Đang lock → chuyển/hủy
            else
                TryLock();         // Chưa lock → tìm & lock
        }

        // Đang Lock → validate mỗi frame
        if (IsHardLocking)
        {
            ValidateLock();
        }

        // Auto-cancel lock khi chuyển sang cung
        if (IsHardLocking && stateMachine != null 
            && stateMachine.CurrentWeapon != null 
            && stateMachine.CurrentWeapon.Type == WeaponType.Ranged)
        {
            ClearTargetSilently();
        }
    }

    private void LateUpdate()
    {
        // GOW Pivot: Xoay pivot hướng về target (camera sẽ follow pivot)
        if (IsHardLocking && currentTarget != null && gowPivot != null)
        {
            Vector3 dir = (currentTarget.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                gowPivot.rotation = Quaternion.LookRotation(dir.normalized);
        }

        // Update icon position (World → Screen → World above target)
        UpdateLockIcon();
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    /// <summary>Trả về target hiện tại (hard lock ưu tiên > soft lock).</summary>
    public Transform GetCurrentTarget() => IsHardLocking ? currentTarget : softTarget;

    /// <summary>Tìm soft target cho attack/skill (không lock camera).</summary>
    public void FindSoftTarget()
    {
        if (IsHardLocking) return;
        softTarget = FindBestTarget(scanRadius, maxViewAngle, true);
    }

    /// <summary>Xoay Player mặt về phía target.</summary>
    public void FaceTarget(Vector3 targetPos, float deltaTime, bool instant = false)
    {
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        if (instant)
            transform.rotation = targetRot;
        else
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, deltaTime * 20f);
    }

    /// <summary>Đồng bộ góc camera khi chuyển giữa FreeLook ↔ LockOn.</summary>
    public void SyncCamera(CinemachineCamera targetCam)
    {
        if (targetCam == null || mainCam == null) return;
        var orbital = targetCam.GetComponent<CinemachineOrbitalFollow>();
        if (orbital != null)
        {
            Vector3 camRot = mainCam.transform.eulerAngles;
            orbital.HorizontalAxis.Value = camRot.y;
            float pitch = camRot.x;
            if (pitch > 180f) pitch -= 360f;
            orbital.VerticalAxis.Value = pitch;
        }
    }

    /// <summary>Hủy lock cứng + sync camera + cleanup icon.</summary>
    public void CancelLock()
    {
        if (!IsHardLocking) return;
        SyncCamera(freeLookCamera);
        ReleaseLock();
    }

    /// <summary>Hủy lock im lặng (không sync camera — dùng khi chuyển weapon).</summary>
    public void ClearTargetSilently()
    {
        if (!IsHardLocking) return;
        ReleaseLock();
    }

    /// <summary>
    /// Tìm target cho Skill (có LOS check + tầm xa hơn).
    /// Ưu tiên: Hard Lock > Soft scan.
    /// </summary>
    public Transform FindSkillTarget(float maxDistance = 20f, float maxAngle = 45f)
    {
        if (IsHardLocking && currentTarget != null)
        {
            // Verify target còn sống + trong tầm
            if (IsTargetValid(currentTarget, maxDistance))
                return currentTarget;
        }
        return FindBestTarget(maxDistance, maxAngle, true);
    }

    private bool IsTargetValid(Transform target, float maxDistance)
    {
        if (target == null) return false;
        var hp = target.GetComponent<EnemyHealth>();
        if (hp != null && hp.IsDead) return false;
        
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > maxDistance) return false;
        
        return true;
    }

    // =========================================================
    // LOCK LOGIC
    // =========================================================

    /// <summary>Thử lock target mới.</summary>
    private void TryLock()
    {
        // Cấm lock khi cầm cung
        if (stateMachine != null && stateMachine.CurrentWeapon != null 
            && stateMachine.CurrentWeapon.Type == WeaponType.Ranged)
            return;

        Transform target = FindBestTarget(scanRadius, maxViewAngle, true);
        if (target == null) return;

        // Lock thành công
        currentTarget = target;
        IsHardLocking = true;

        // Sync pivot trước khi camera chuyển
        if (gowPivot != null)
            gowPivot.rotation = Quaternion.LookRotation(mainCam.transform.forward);

        // Camera priority
        if (lockOnCamera != null) lockOnCamera.Priority = 20;
        if (freeLookCamera != null) freeLookCamera.Priority = 10;

        // Spawn icon
        SpawnLockIcon();
    }

    /// <summary>Đang lock → nhấn Tab lần nữa → chuyển target hoặc hủy lock.</summary>
    private void SwitchOrUnlock()
    {
        Transform nextTarget = FindNextTarget();
        
        if (nextTarget != null && nextTarget != currentTarget)
        {
            // Chuyển sang target mới
            currentTarget = nextTarget;
            
            // Di chuyển icon sang target mới
            DespawnLockIcon();
            SpawnLockIcon();
        }
        else
        {
            // Không có target khác → hủy lock
            SyncCamera(freeLookCamera);
            ReleaseLock();
        }
    }

    /// <summary>Validate lock mỗi frame — tự hủy nếu target không hợp lệ.</summary>
    private void ValidateLock()
    {
        if (currentTarget == null)
        {
            CancelLock();
            return;
        }

        // Check 1: Target đã chết?
        var health = currentTarget.GetComponent<EnemyHealth>();
        if (health != null && health.IsDead)
        {
            // Target chết → tìm target kế tiếp hoặc hủy
            Transform next = FindBestTarget(scanRadius, maxViewAngle, true);
            if (next != null && next != currentTarget)
            {
                currentTarget = next;
                DespawnLockIcon();
                SpawnLockIcon();
            }
            else
            {
                CancelLock();
            }
            return;
        }

        // Check 2: Quá xa?
        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist > maxLockDistance)
        {
            CancelLock();
            return;
        }

        // Check 3: Bị che khuất quá lâu? (Optional — GoW vẫn giữ lock qua tường ngắn)
        // Có thể thêm timer nếu muốn: nếu LOS bị chặn > 2s → hủy lock
    }

    // =========================================================
    // TARGET FINDING
    // =========================================================

    /// <summary>
    /// Tìm target tốt nhất — NonAlloc, LOS check, dead check.
    /// Scoring: góc nhỏ + khoảng cách gần = điểm cao.
    /// </summary>
    private Transform FindBestTarget(float radius, float maxAngle, bool requireLOS)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, scanBuffer, enemyLayer);
        
        Transform best = null;
        float bestScore = Mathf.Infinity;

        for (int i = 0; i < count; i++)
        {
            Transform candidate = scanBuffer[i].transform;

            // Skip dead
            var hp = candidate.GetComponent<EnemyHealth>();
            if (hp != null && hp.IsDead) continue;

            Vector3 dirToTarget = candidate.position - mainCam.transform.position;
            float dist = dirToTarget.magnitude;

            // Angle check
            float angle = Vector3.Angle(mainCam.transform.forward, dirToTarget.normalized);
            if (angle > maxAngle) continue;

            // LOS check (raycast từ Player → target, không phải camera → target)
            if (requireLOS)
            {
                Vector3 origin = transform.position + Vector3.up;
                Vector3 targetPoint = candidate.position + Vector3.up;
                if (Physics.Linecast(origin, targetPoint, obstructionLayer))
                    continue;
            }

            // Scoring: 70% góc + 30% khoảng cách (normalize cả hai)
            float angleScore = angle / maxAngle;        // [0, 1]
            float distScore = dist / radius;             // [0, 1]
            float totalScore = angleScore * 0.7f + distScore * 0.3f;

            if (totalScore < bestScore)
            {
                bestScore = totalScore;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Tìm target KẾ TIẾP (dùng khi Switch Target).
    /// Logic: tìm quái gần tâm camera nhất MÀ KHÔNG PHẢI quái đang lock.
    /// </summary>
    private Transform FindNextTarget()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, scanBuffer, enemyLayer);
        
        Transform best = null;
        float bestScore = Mathf.Infinity;

        for (int i = 0; i < count; i++)
        {
            Transform candidate = scanBuffer[i].transform;

            // Skip current target
            if (candidate == currentTarget) continue;

            // Skip dead
            var hp = candidate.GetComponent<EnemyHealth>();
            if (hp != null && hp.IsDead) continue;

            float dist = Vector3.Distance(transform.position, candidate.position);
            if (dist > maxLockDistance) continue;

            // LOS check
            Vector3 origin = transform.position + Vector3.up;
            Vector3 targetPoint = candidate.position + Vector3.up;
            if (Physics.Linecast(origin, targetPoint, obstructionLayer))
                continue;

            // Scoring: ưu tiên gần Player nhất
            float angle = Vector3.Angle(mainCam.transform.forward, 
                (candidate.position - mainCam.transform.position).normalized);
            float score = angle * 0.5f + dist * 0.5f;

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    // =========================================================
    // LOCK ICON
    // =========================================================

    private void SpawnLockIcon()
    {
        if (lockIconPrefab == null || currentTarget == null) return;

        Vector3 iconPos = currentTarget.position + Vector3.up * iconHeightOffset;
        currentIcon = ObjectPoolManager.Instance.SpawnFromPool(
            lockIconPrefab, iconPos, Quaternion.identity);

        if (currentIcon != null)
            currentIcon.transform.SetParent(currentTarget);
    }

    private void DespawnLockIcon()
    {
        if (currentIcon != null)
        {
            currentIcon.transform.SetParent(null);
            ObjectPoolManager.Instance.ReturnToPool(currentIcon);
            currentIcon = null;
        }
    }

    private void UpdateLockIcon()
    {
        if (currentIcon == null || currentTarget == null) return;

        // Icon theo sát target (đã SetParent nên tự di chuyển)
        // Nhưng nếu muốn icon luôn facing camera:
        currentIcon.transform.position = currentTarget.position + Vector3.up * iconHeightOffset;
        
        // Billboard: icon luôn quay mặt về camera
        if (mainCam != null)
        {
            currentIcon.transform.rotation = Quaternion.LookRotation(
                currentIcon.transform.position - mainCam.transform.position);
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    /// <summary>Cleanup chung khi hủy lock.</summary>
    private void ReleaseLock()
    {
        IsHardLocking = false;
        currentTarget = null;

        DespawnLockIcon();

        if (lockOnCamera != null) lockOnCamera.Priority = 0;
        if (freeLookCamera != null) freeLookCamera.Priority = 10;
    }
}