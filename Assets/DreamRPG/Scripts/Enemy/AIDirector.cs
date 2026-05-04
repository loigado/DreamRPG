using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// AIDirector — Nhạc trưởng AAA điều phối toàn bộ đàn quái.
///
/// Chức năng:
///   • Token System      — giới hạn số quái tấn công đồng thời
///   • Per-Enemy Cooldown — quái vừa đánh xong phải chờ cooldown mới được đánh tiếp
///   • Off-screen Guard   — cấm quái ngoài Camera lao tới
///   • Juggle Guard       — tạm dừng khi Player đang combo
///   • Priority System    — quái ưu tiên cao được cấp Token trước
///   • Wave Pacing        — sau N đợt tấn công, ép nghỉ để Player có breathing room
/// </summary>
public class AIDirector : MonoBehaviour
{
    public static AIDirector Instance { get; private set; }

    [Header("Token System")]
    public int maxConcurrentAttacks = 2;
    private int currentActiveTokens = 0;

    [Header("Wave Pacing")]
    [Tooltip("Sau bao nhiêu lượt tấn công thì ép toàn bộ quái nghỉ 1 nhịp")]
    public int attacksPerWave = 4;
    [Tooltip("Thời gian nghỉ giữa các sóng (giây)")]
    public float waveBreathingTime = 2f;
    private int attacksSinceLastBreak = 0;
    private float breathingTimer = 0f;
    private bool isBreathing = false;

    [Header("Juggle Guard")]
    public bool isPlayerJuggling = false;

    [Header("Debug")]
    [SerializeField] private int debugActiveTokens = 0;
    [SerializeField] private int debugRegisteredEnemies = 0;

    private Camera mainCam;

    // === ENEMY REGISTRY ===
    private readonly List<EnemyStateMachine> activeEnemies = new();

    // === PER-ENEMY COOLDOWN ===
    // Theo dõi thời điểm cuối cùng mỗi quái tấn công
    private readonly Dictionary<EnemyStateMachine, float> lastAttackTimes = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        mainCam = Camera.main;
    }

    private void Update()
    {
        // Wave breathing countdown
        if (isBreathing)
        {
            breathingTimer -= Time.deltaTime;
            if (breathingTimer <= 0f)
            {
                isBreathing = false;
                attacksSinceLastBreak = 0;
            }
        }

        debugActiveTokens = currentActiveTokens;
        debugRegisteredEnemies = activeEnemies.Count;
    }

    // =========================================================
    // REGISTRY
    // =========================================================

    public void RegisterEnemy(EnemyStateMachine enemy)
    {
        if (!activeEnemies.Contains(enemy))
            activeEnemies.Add(enemy);
    }

    public void UnregisterEnemy(EnemyStateMachine enemy)
    {
        activeEnemies.Remove(enemy);
        lastAttackTimes.Remove(enemy);
    }

    /// <summary>Danh sách tất cả quái đang hoạt động (read-only).</summary>
    public IReadOnlyList<EnemyStateMachine> ActiveEnemies => activeEnemies;

    // =========================================================
    // TOKEN SYSTEM
    // =========================================================

    /// <summary>
    /// Xin quyền tấn công. Trả về true nếu được phép.
    /// </summary>
    public bool RequestAttackToken(EnemyStateMachine enemy)
    {
        // 0. Nếu đã có Token rồi thì cứ xài
        if (enemy.IsHoldingAttackToken) return true;

        // 1. Wave breathing — toàn đàn nghỉ
        if (isBreathing) return false;

        // 2. Juggle Guard
        if (isPlayerJuggling) return false;

        // 3. Hết Token
        if (currentActiveTokens >= maxConcurrentAttacks) return false;

        // 4. Off-screen
        if (!IsEnemyOnScreen(enemy.transform.position)) return false;

        // 5. Per-enemy cooldown
        if (lastAttackTimes.TryGetValue(enemy, out float lastTime))
        {
            float cooldown = enemy.Stats != null ? enemy.Stats.attackCooldown : 3f;
            if (Time.time - lastTime < cooldown) return false;
        }

        // 6. Priority — nếu có quái ưu tiên cao hơn đang chờ
        if (IsHigherPriorityEnemyWaiting(enemy)) return false;

        // Cấp Token
        currentActiveTokens++;
        lastAttackTimes[enemy] = Time.time;
        enemy.IsHoldingAttackToken = true;
        return true;
    }

    /// <summary>Trả Token khi đánh xong hoặc bị gián đoạn. Chỉ release nếu thật sự đang giữ.</summary>
    public void ReleaseToken(EnemyStateMachine enemy)
    {
        if (enemy != null && !enemy.IsHoldingAttackToken) return; // Không giữ token → bỏ qua

        if (enemy != null) enemy.IsHoldingAttackToken = false;

        currentActiveTokens = Mathf.Max(0, currentActiveTokens - 1);

        // Wave tracking
        attacksSinceLastBreak++;
        if (attacksSinceLastBreak >= attacksPerWave)
        {
            isBreathing = true;
            breathingTimer = waveBreathingTime;
        }
    }

    // =========================================================
    // CAMERA VISIBILITY
    // =========================================================

    public bool IsEnemyOnScreen(Vector3 enemyPos)
    {
        if (mainCam == null) { mainCam = Camera.main; return true; }
        Vector3 vp = mainCam.WorldToViewportPoint(enemyPos);
        return vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
    }

    /// <summary>
    /// Tính hướng để quái off-screen di chuyển vào trước Camera.
    /// Trả về normalized direction trên mặt phẳng XZ.
    /// </summary>
    public Vector3 GetOnScreenDirection(Vector3 enemyPos)
    {
        if (mainCam == null) return Vector3.zero;

        // Chiếu vị trí quái lên viewport
        Vector3 vp = mainCam.WorldToViewportPoint(enemyPos);

        // Tính hướng đẩy: viewport center (0.5, 0.5) - viewport quái
        Vector2 pushDir = new Vector2(0.5f - vp.x, 0.5f - vp.y);

        // Chuyển thành world direction qua Camera axes
        Vector3 worldDir = mainCam.transform.right * pushDir.x + mainCam.transform.up * pushDir.y;
        worldDir.y = 0f;

        return worldDir.sqrMagnitude > 0.01f ? worldDir.normalized : mainCam.transform.forward;
    }

    // =========================================================
    // PRIORITY
    // =========================================================

    private bool IsHigherPriorityEnemyWaiting(EnemyStateMachine requestingEnemy)
    {
        int myPriority = requestingEnemy.GetPriority();

        foreach (var e in activeEnemies)
        {
            if (e == null || e == requestingEnemy) continue;
            if (!(e.currentState is EnemyStrafeState)) continue;

            if (e.GetPriority() < myPriority &&
                e.ReservedSlotIndex >= 0 &&
                EnemySlotManager.Instance != null &&
                EnemySlotManager.Instance.IsInnerSlot(e.ReservedSlotIndex))
            {
                return true;
            }
        }
        return false;
    }
}