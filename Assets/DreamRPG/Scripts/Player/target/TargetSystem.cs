using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// TargetSystem — Hệ thống Lock-On chuẩn AAA (Ghost of Tsushima style).
///
/// 🟢 ĐÃ FIX LỖI: Nhấn Tab 1 lần để Lock, nhấn lần 2 để Tắt (Không tự động cycle mục tiêu).
/// 🟢 GHOST OF TSUSHIMA MECHANIC: Soft Lock giờ đây ưu tiên Hướng di chuyển (WASD) thay vì Hướng Camera.
/// </summary>
public class TargetSystem : MonoBehaviour
{
    [Header("Scanning")]
    [Tooltip("Bán kính quét tìm quái")]
    public float scanRadius = 15f;
    [Tooltip("Góc tối đa để chấp nhận target")]
    public float maxViewAngle = 90f; 
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
    private readonly Collider[] scanBuffer = new Collider[30]; 

    private void Awake()
    {
        mainCam = Camera.main;
        stateMachine = GetComponent<PlayerStateMachine>();
    }

    private void Update()
    {
        // 🟢 FIX BUG: Nút Tab giờ đây hoạt động như một công tắc (Toggle) thuần túy
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (IsHardLocking)
                CancelLock(); // Đang lock thì HỦY
            else
                TryLock();    // Chưa lock thì TÌM VÀ LOCK
        }

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
        if (IsHardLocking && currentTarget != null && gowPivot != null)
        {
            Vector3 dir = (currentTarget.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                gowPivot.rotation = Quaternion.LookRotation(dir.normalized);
        }

        UpdateLockIcon();
    }

    // =========================================================
    // 🟢 HỆ THỐNG GHOST OF TSUSHIMA (INTENT-BASED TARGETING)
    // =========================================================

    public Transform GetCurrentTarget() => IsHardLocking ? currentTarget : softTarget;

    /// <summary>
    /// Tìm mục tiêu dựa trên ý định di chuyển của người chơi (Ghost of Tsushima).
    /// </summary>
    public void FindSoftTarget()
    {
        if (IsHardLocking) return;

        // Ưu tiên hướng mà người chơi đang bấm (WASD/Analog Stick)
        Vector3 playerIntentDir = GetPlayerIntentDirection();
        
        // Truyền hướng đó vào hàm tìm kiếm
        softTarget = FindBestTargetByDirection(playerIntentDir, scanRadius, maxViewAngle, true);
    }

    /// <summary>
    /// Tính toán hướng người chơi thực sự muốn đánh tới.
    /// </summary>
    private Vector3 GetPlayerIntentDirection()
    {
        if (stateMachine == null || stateMachine.InputReader == null || mainCam == null) 
            return transform.forward;

        Vector2 input = stateMachine.InputReader.MovementValue;

        // Nếu người chơi ĐANG BẤM PHÍM DI CHUYỂN
        if (input.sqrMagnitude > 0.01f)
        {
            Vector3 camForward = mainCam.transform.forward;
            camForward.y = 0;
            camForward.Normalize();

            Vector3 camRight = mainCam.transform.right;
            camRight.y = 0;
            camRight.Normalize();

            // Trả về hướng di chuyển tương đối so với Camera
            return (camForward * input.y + camRight * input.x).normalized;
        }

        // Nếu người chơi ĐỨNG YÊN, lấy hướng nhân vật đang nhìn
        Vector3 fallbackDir = transform.forward;
        fallbackDir.y = 0;
        return fallbackDir.normalized;
    }

    /// <summary>
    /// Core logic: Chấm điểm mục tiêu ưu tiên HƯỚNG CHỈ ĐỊNH thay vì chỉ dùng Camera.
    /// </summary>
    private Transform FindBestTargetByDirection(Vector3 referenceDir, float radius, float maxAngle, bool requireLOS)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, scanBuffer, enemyLayer);
        
        Transform best = null;
        float bestScore = Mathf.Infinity;

        for (int i = 0; i < count; i++)
        {
            Transform candidate = scanBuffer[i].transform;

            var hp = candidate.GetComponentInParent<EnemyHealth>();
            // 🟢 ĐÃ FIX LỖI: Bắt buộc phải có EnemyHealth (để không trúng cỏ cây hoặc trúng Player)
            // Đồng thời bỏ qua chính bản thân Player
            if (hp == null || hp.IsDead || candidate.root == transform.root) continue;

            Vector3 dirToTarget = candidate.position - transform.position;
            float dist = dirToTarget.magnitude;
            dirToTarget.y = 0;
            dirToTarget.Normalize();

            // Kiểm tra góc lệch so với HƯỚNG Ý ĐỊNH
            float angle = Vector3.Angle(referenceDir, dirToTarget);
            if (angle > maxAngle) continue;

            if (requireLOS)
            {
                Vector3 origin = transform.position + Vector3.up;
                Vector3 targetPoint = candidate.position + Vector3.up;
                if (Physics.Linecast(origin, targetPoint, obstructionLayer))
                    continue;
            }

            // 🟢 GoT Scoring: Đặt trọng số Góc quay lên cực cao (85%) so với Khoảng cách (15%)
            // Nghĩa là: Con quái dù ở xa nhưng nằm đúng hướng phím bấm sẽ bị chém trúng, 
            // thay vì chém nhầm con quái ở gần nhưng nằm chệch hướng!
            float angleScore = angle / maxAngle;       
            float distScore = dist / radius;             
            float totalScore = angleScore * 0.85f + distScore * 0.15f;

            if (totalScore < bestScore)
            {
                bestScore = totalScore;
                best = candidate;
            }
        }

        return best;
    }

    // =========================================================
    // PUBLIC API & UTILS
    // =========================================================

    public void FaceTarget(Vector3 targetPos, float deltaTime, bool instant = false)
    {
        Vector3 dirToTarget = (targetPos - transform.position);
        dirToTarget.y = 0;

        if (dirToTarget.sqrMagnitude < 0.36f) return; 

        Quaternion targetRot = Quaternion.LookRotation(dirToTarget.normalized);
        
        if (instant) 
            transform.rotation = targetRot;
        else 
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, deltaTime * 15f);
    }

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

    public void CancelLock()
    {
        if (!IsHardLocking) return;
        SyncCamera(freeLookCamera);
        ReleaseLock();
    }

    public void ClearTargetSilently()
    {
        if (!IsHardLocking) return;
        ReleaseLock();
    }

    public Transform FindSkillTarget(float maxDistance = 20f, float maxAngle = 45f)
    {
        if (IsHardLocking && currentTarget != null)
        {
            if (IsTargetValid(currentTarget, maxDistance))
                return currentTarget;
        }
        Vector3 intentDir = GetPlayerIntentDirection();
        return FindBestTargetByDirection(intentDir, maxDistance, maxAngle, true);
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
    // HARD LOCK LOGIC
    // =========================================================

    private void TryLock()
    {
        if (stateMachine != null && stateMachine.CurrentWeapon != null 
            && stateMachine.CurrentWeapon.Type == WeaponType.Ranged)
            return;

        // Hard lock thì dựa vào hướng Camera để thân thiện với game thủ PC
        Vector3 camDir = mainCam.transform.forward;
        camDir.y = 0;
        
        Transform target = FindBestTargetByDirection(camDir.normalized, scanRadius, maxViewAngle, true);
        if (target == null) return;

        currentTarget = target;
        IsHardLocking = true;

        if (gowPivot != null)
            gowPivot.rotation = Quaternion.LookRotation(mainCam.transform.forward);

        if (lockOnCamera != null) 
        {
            // 🟢 ÉP CAMERA PHẢI NHÌN VÀO QUÁI (Chống trôi / quay mòng mòng nếu bạn lỡ set LookAt vào Player)
            lockOnCamera.LookAt = currentTarget;
            lockOnCamera.Priority = 20;
        }
        
        if (freeLookCamera != null) freeLookCamera.Priority = 10;

        SpawnLockIcon();
    }

    private void ValidateLock()
    {
        if (currentTarget == null)
        {
            CancelLock();
            return;
        }

        var health = currentTarget.GetComponent<EnemyHealth>();
        if (health != null && health.IsDead)
        {
            // Tự động tìm con khác sát với góc nhìn camera nếu con cũ chết
            Vector3 camDir = mainCam.transform.forward;
            camDir.y = 0;
            Transform next = FindBestTargetByDirection(camDir.normalized, scanRadius, maxViewAngle, true);
            
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

        float dist = Vector3.Distance(transform.position, currentTarget.position);
        if (dist > maxLockDistance)
        {
            CancelLock();
            return;
        }
    }

    // =========================================================
    // LOCK ICON & CLEANUP
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

        currentIcon.transform.position = currentTarget.position + Vector3.up * iconHeightOffset;
        
        if (mainCam != null)
        {
            currentIcon.transform.rotation = Quaternion.LookRotation(
                currentIcon.transform.position - mainCam.transform.position);
        }
    }

    private void ReleaseLock()
    {
        IsHardLocking = false;
        currentTarget = null;

        DespawnLockIcon();

        if (lockOnCamera != null) 
        {
            lockOnCamera.Priority = 0;
            lockOnCamera.LookAt = null; // 🟢 Trả lại trạng thái tự do
        }
        if (freeLookCamera != null) freeLookCamera.Priority = 10;
    }
}