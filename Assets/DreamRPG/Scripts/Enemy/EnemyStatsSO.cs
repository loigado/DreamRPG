using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyStats", menuName = "DreamRPG/Enemy Stats")]
public class EnemyStatsSO : ScriptableObject
{
    // =========================================================
    // 1. SINH TỒN
    // =========================================================
    [Header("Chỉ số Sinh tồn")]
    public float maxHealth = 100f;

    // =========================================================
    // 2. DI CHUYỂN
    // =========================================================
    [Header("Chỉ số Di chuyển")]
    public float moveSpeed = 3f;
    public float turnSpeed = 120f;

    // =========================================================
    // 3. CHIẾN ĐẤU
    // =========================================================
    [Header("Chỉ số Chiến đấu")]
    public float attackDamage = 10f;
    public float attackRange = 2f;

    [Tooltip("Cooldown giữa 2 lần tấn công (giây). Sau khi đánh xong, quái không được đánh lại ngay.")]
    public float attackCooldown = 3f;

    [Tooltip("Số lượng combo attacks khác nhau (1-3). Random mỗi lần vào AttackState.")]
    [Range(1, 3)] public int attackVariants = 2;

    // =========================================================
    // 4. TUẦN TRA & TẦM NHÌN
    // =========================================================
    [Header("Chỉ số Tuần tra & Tầm nhìn")]
    public float detectionRange = 10f;
    public float patrolRadius = 8f;
    public float patrolWaitTime = 2f;

    // =========================================================
    // 5. VỜN (STRAFE)
    // =========================================================
    [Header("Chỉ số Vờn (Strafe)")]
    public float strafeTimeMin = 2f;
    public float strafeTimeMax = 4f;

    // =========================================================
    // 6. RƯỢT ĐUỔI (CHASE)
    // =========================================================
    [Header("Chỉ số Rượt đuổi (Chase)")]
    public float gapCloserRange = 10f;
    public float dropAggroRange = 15f;

    [Tooltip("Thời gian nhớ Player sau khi mất tầm nhìn (giây). GoW quái nhớ ~5-8s.")]
    public float aggroMemoryDuration = 6f;

    // =========================================================
    // 7. CẢM BIẾN (PERCEPTION)
    // =========================================================
    [Header("Cảm biến (Perception) & Cảnh giác")]
    public float visionRange = 12f;
    public float visionAngle = 90f;
    public float hearingRange = 4f;
    public float alertDuration = 1.5f;

    // =========================================================
    // 8. AMBIENT
    // =========================================================
    [Header("Hoạt ảnh Tĩnh (Ambient)")]
    public float idleTimeMin = 3f;
    public float idleTimeMax = 6f;

    // =========================================================
    // 9. CƠ ĐỘNG (COMBAT MANEUVER)
    // =========================================================
    [Header("Chỉ số Cơ động (Combat Maneuver)")]
    public float innerRingRadius = 2.5f;
    public float outerRingRadius = 5.5f;
    [Range(0f, 100f)] public float dodgeChance = 30f;
    public float feintIntervalMin = 2f;
    public float feintIntervalMax = 4f;

    // =========================================================
    // 10. STEERING SEPARATION (BOIDS)
    // =========================================================
    [Header("Steering Separation (Boids)")]
    [Tooltip("Bán kính vùng cá nhân — quái khác lọt vào đây sẽ bị đẩy ra")]
    public float separationRadius = 1.4f;
    [Tooltip("Cường độ lực đẩy Separation")]
    public float separationStrength = 8f;
    [Tooltip("Bán kính 'đã đến nơi' khi bám Slot")]
    public float slotArrivalTolerance = 0.4f;

    // =========================================================
    // 11. YIELDING (NHƯỜNG ĐƯỜNG)
    // =========================================================
    [Header("Yielding (Nhường đường)")]
    [Range(0f, 1f)] public float yieldSpeedMultiplier = 0.25f;
    public float yieldMaxDuration = 0.8f;

    // =========================================================
    // 12. ĐỘ ƯU TIÊN
    // =========================================================
    [Header("Độ ưu tiên (Priority)")]
    [Tooltip("Số nhỏ = ưu tiên cao (Boss=0, Thường=1, Yếu=2)")]
    public int priorityId = 1;

    // =========================================================
    // 13. HIT REACTION & DODGE
    // =========================================================
    [Header("Hit Reaction")]
    [Tooltip("Thời gian choáng khi bị đánh nhẹ (giây)")]
    public float lightStaggerDuration = 0.4f;
    [Tooltip("Thời gian choáng khi bị đánh nặng (giây)")]
    public float heavyStaggerDuration = 1.0f;
    [Tooltip("Ngưỡng sát thương để kích hoạt heavy stagger (% maxHealth)")]
    [Range(0f, 1f)] public float heavyStaggerThreshold = 0.15f;

    [Header("Dodge")]
    [Tooltip("Tốc độ lộn khi né đòn")]
    public float dodgeSpeed = 6f;
    [Tooltip("Thời gian thực hiện dodge (giây)")]
    public float dodgeDuration = 0.5f;

    // =========================================================
    // 14. GAP CLOSER
    // =========================================================
    [Header("Gap Closer")]
    [Tooltip("Tốc độ lao tới khi dùng Gap Closer")]
    public float gapCloserSpeed = 8f;
    [Tooltip("Cooldown giữa 2 lần Gap Closer (giây)")]
    public float gapCloserCooldown = 8f;

    // =========================================================
    // 15. HIỆU ỨNG TRẠNG THÁI
    // =========================================================
    [Header("Hiệu ứng Trạng thái")]
    [Tooltip("Material phủ lên quái khi bị đóng băng (ice overlay). Kéo thả Material băng vào đây.")]
    public Material freezeMaterial;
}