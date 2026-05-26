using UnityEngine;

public enum EnemyClassType
{
    Melee,      // Lính cận chiến thường
    Ranged,     // Cung thủ/Pháp sư
    Tanker,     // Quái trâu bò, lù đù
    Assassin,   // Sát thủ lướt nhanh (dự kiến)
    Boss        // Boss chính
}

// 🟢 GOT CẬP NHẬT: Enum phân loại đèn giao thông cho đòn đánh
public enum AttackGlint 
{ 
    Normal, // Không sáng: Đỡ/Parry bình thường
    Blue,   // Sáng Xanh: Đòn nặng, giữ Block sẽ vỡ thủ, bắt buộc Parry/Né
    Red     // Sáng Đỏ: Đòn hiểm, bỏ qua mọi Block/Parry, bắt buộc Né
}

[CreateAssetMenu(fileName = "NewEnemyStats", menuName = "DreamRPG/Enemy Stats")]
public class EnemyStatsSO : ScriptableObject
{
    // =========================================================
    // 0. PHÂN LOẠI
    // =========================================================
    [Header("Phân loại Quái")]
    public EnemyClassType enemyClass = EnemyClassType.Melee;

    [Tooltip("Hệ nguyên tố của quái (Dùng cho cơ chế Perfect Parry hút năng lượng)")]
    public SkillElement enemyElement = SkillElement.KhongHe; 

    [Tooltip("Đánh dấu nếu đây là quái đánh xa (Cung thủ, Pháp sư)")]
    public bool isRangedEnemy = false;
    
    // =========================================================
    // 1. SINH TỒN & PHÁ THẾ (GoT SYSTEM)
    // =========================================================
    [Header("Chỉ số Sinh tồn")]
    public float maxHealth = 100f;

    [Header("GoT - Stagger System (Thanh Phá Thế)")]
    [Tooltip("Lượng máu của Thanh Phá Thế. Lính thường = 0. Tanker = 100.")]
    public float maxGuard = 0f;
    [Tooltip("Thời gian quái bị choáng (Groggy) khi vỡ thế để Player xả combo")]
    public float guardBrokenStunDuration = 3f;

    [Header("Sekiro - Posture System (Phá Giáp)")]
    [Tooltip("Lượng điểm cần tích lũy để phá vỡ thế đứng của quái (Ví dụ: 100)")]
    public float maxPosture = 100f;
    [Tooltip("Tốc độ phục hồi thanh Posture mỗi giây khi quái được nghỉ")]
    public float postureRecoveryRate = 15f; 
    [Tooltip("Thời gian chờ (giây) không bị đánh để quái bắt đầu hồi Posture")]
    public float postureRecoveryDelay = 3f;
    [Tooltip("Hệ số nhân Posture khi quái bị chém trúng (1.0 = bằng sát thương vật lý)")]
    public float postureDamageMultiplier = 1.2f;

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

    [Tooltip("Tầm đánh thực tế của từng đòn (Combo 0, 1, 2...). Nếu để trống sẽ dùng Attack Range gốc.")]
    public float[] comboHitboxRanges;

    [Tooltip("Cooldown giữa 2 lần tấn công (giây).")]
    public float attackCooldown = 3f;

    [Tooltip("Số lượng combo attacks khác nhau (1-3).")]
    [Range(1, 3)] public int attackVariants = 2;
    [Tooltip("Lực trượt tới khi tấn công.")]
    public float lungeForceMultiplier = 1f;
    [Tooltip("Trọng số token khi tấn công (mặc định 1).")]
    [Range(1, 5)] public int tokenWeight = 1;

    // =========================================================
    // 3B. SEKIRO/GoT - COMBO & RHYTHM
    // =========================================================
    [Header("Sekiro/GoT — Combo & Rhythm")]
    [Range(1, 5)] public int comboHitsMin = 1;
    [Range(1, 5)] public int comboHitsMax = 3;
    
    [Range(0f, 100f)] public float rhythmBreakChance = 30f;
    public float delayedSwingMin = 0.3f;
    public float delayedSwingMax = 0.7f;

    [Header("GoT — Glint Warning (Đèn Giao Thông)")]
    [Tooltip("Tỷ lệ ra đòn Xanh (Nặng) trong combo")]
    [Range(0f, 100f)] public float heavyBlueChance = 20f;
    [Tooltip("Tỷ lệ ra đòn Đỏ (Xuyên thủ) trong combo")]
    [Range(0f, 100f)] public float unblockableChance = 20f;
    [Tooltip("Tỷ lệ đòn cuối combo là đòn Đỏ")]
    [Range(0f, 100f)] public float unblockableFinisherChance = 35f;

    [Header("Reactive AI")]
    [Range(0f, 100f)] public float counterAttackChance = 25f;
    [Range(0f, 100f)] public float guardPunishChance = 50f;
    public float guardPunishThreshold = 1.5f;

    // =========================================================
    // 4 -> 15. CÁC THÔNG SỐ CŨ (Giữ nguyên)
    // =========================================================
    [Header("Chỉ số Tuần tra & Tầm nhìn")]
    public float detectionRange = 10f;
    public float patrolRadius = 8f;
    public float patrolWaitTime = 2f;

    [Header("Chỉ số Vờn (Strafe)")]
    public float strafeTimeMin = 2f;
    public float strafeTimeMax = 4f;
    public bool prefersOuterRing = false;

    [Header("Chỉ số Rượt đuổi (Chase)")]
    public float gapCloserRange = 10f;
    public float dropAggroRange = 15f;
    public float aggroMemoryDuration = 6f;

    [Header("Cảm biến (Perception) & Cảnh giác")]
    public float visionRange = 12f;
    public float visionAngle = 90f;
    public float hearingRange = 4f;
    public float alertDuration = 1.5f;

    [Header("Hoạt ảnh Tĩnh (Ambient)")]
    public float idleTimeMin = 3f;
    public float idleTimeMax = 6f;

    [Header("Chỉ số Cơ động (Combat Maneuver)")]
    public float innerRingRadius = 2.5f;
    public float outerRingRadius = 5.5f;
    [Range(0f, 100f)] public float dodgeChance = 30f;
    public float feintIntervalMin = 2f;
    public float feintIntervalMax = 4f;

    [Header("Steering Separation (Boids)")]
    public float separationRadius = 1.4f;
    public float separationStrength = 8f;
    public float slotArrivalTolerance = 0.4f;

    [Header("Yielding (Nhường đường)")]
    [Range(0f, 1f)] public float yieldSpeedMultiplier = 0.25f;
    public float yieldMaxDuration = 0.8f;

    [Header("Độ ưu tiên (Priority)")]
    public int priorityId = 1;

    [Header("Tanker — Animator Triggers (Optional)")]
    public string heavySmashTrigger = "";
    public string sweepTrigger = "";
    public string chargeTrigger = "";
    public string recoveryTrigger = "";

    [Header("Hit Reaction")]
    public float lightStaggerDuration = 0.4f;
    public float heavyStaggerDuration = 1.0f;
    [Range(0f, 1f)] public float heavyStaggerThreshold = 0.15f;

    [Header("Dodge")]
    public float dodgeSpeed = 6f;
    public float dodgeDuration = 0.5f;

    [Header("Gap Closer")]
    public float gapCloserSpeed = 8f;
    public float gapCloserCooldown = 8f;

    [Header("Hiệu ứng Trạng thái & Kỹ năng")]
    public Material freezeMaterial;

    // =========================================================
    // 16. KỸ NĂNG ĐẶC BIỆT & VFX
    // =========================================================
    [Header("Kỹ năng Tanker: Dậm đất (Stomp)")]
    public float stompRadius = 3.5f;
    public float stompDamageMultiplier = 0.8f;
    public float stompPushForce = 15f;
    
    [Header("Enrage Roar (Gầm chuyển pha)")]
    public float enragePushForce = 25f;      
    public float enrageKnockupForce = 0.5f;  

    [Header("Lực Đòn Đỏ/Unblockable")]
    public float unblockablePushForce = 30f;     
    public float unblockableKnockupForce = 0.8f; 
    
    [Header("VFX Kỹ năng Pháp Sư Lôi")]
    [Tooltip("Prefab của Đạn hoặc Vòng tròn gọi sét (Ranged)")]
    public GameObject rangedProjectilePrefab;
    public GameObject meleeBurstPrefab;
    [Header("VFX Kỹ năng & Đèn Giao Thông")]
    [Tooltip("Hiệu ứng chớp Xanh (Đòn nặng)")]
    public GameObject BlueWarningVFX;
    [Tooltip("Hiệu ứng chớp Đỏ (Đòn hiểm)")]
    public GameObject UnblockableWarningVFX;
    [Tooltip("Prefab hiệu ứng sóng âm khi gầm")]
    public GameObject enrageRoarVFX;
    
    [Header("Boss Settings")]
    [Tooltip("Đánh dấu ô này nếu đây là Boss (Sẽ bỏ qua luật chờ Token)")]
    public bool isMiniBoss = false; 
    [Tooltip("Prefab Sóng kích / Tường sét quét trên mặt đất")]
    public GameObject shockwavePrefab;
    public ParticleSystem enrageParticles;

    [Header("Boss Settings")]
    [Tooltip("Tên hiển thị của Boss dưới thanh máu khổng lồ")]
    public string bossName = "Tên Boss Mặc Định";
}