using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// EnemyStateMachine — Bộ não trung tâm của quái vật (AAA Standard).
///
/// Trách nhiệm:
///   • Quản lý State Machine
///   • Xử lý di chuyển tập trung (HandleMovement)
///   • Tính Separation Force (Boids)
///   • Quản lý NavMeshObstacle Carving (state-driven)
///   • Lắng nghe sự kiện Damage/Death từ EnemyHealth
///   • Tự đăng ký vào AIDirector
///   • Quản lý Aggro Memory
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Animator), typeof(CharacterController))]
[RequireComponent(typeof(NavMeshObstacle))]
public class EnemyStateMachine : MonoBehaviour, IDamageable
{
    [Header("Core Data")]
    public EnemyStatsSO Stats;
    public Transform PlayerTarget;

    // === COMPONENTS ===
    public NavMeshAgent        Agent      { get; private set; }
    public Animator            Anim       { get; private set; }
    public CharacterController Controller { get; private set; }
    public EnemyHealth         Health     { get; private set; }
    public NavMeshObstacle     Obstacle   { get; private set; }

    // === STATE ===
    public EnemyState currentState { get; private set; }
    public Vector3 SpawnPosition   { get; private set; }

    // === MOVEMENT ===
    public float   VerticalVelocity { get; private set; }
    public Vector3 ManualVelocity   { get; set; }
    public Vector3 LookAtTarget     { get; set; }

    // === PLAYER AWARENESS ===
    public bool IsPlayerMoving { get; private set; }
    private Vector3 lastPlayerPos;

    // === AGGRO MEMORY ===
    /// <summary>Quái đang aggro Player (nhớ vị trí cuối cùng dù mất tầm nhìn)</summary>
    public bool HasAggro          { get; set; }
    /// <summary>Vị trí cuối cùng nhìn thấy Player</summary>
    public Vector3 LastKnownPlayerPos { get; set; }
    private float aggroMemoryTimer;

    // === SLOT SYSTEM ===
    public int ReservedSlotIndex { get; set; } = -1;

    /// <summary>Quái đang giữ Attack Token? Dùng để DeathState chỉ release khi thật sự có token.</summary>
    public bool IsHoldingAttackToken { get; set; } = false;

    // === SEPARATION (Boids — NonAlloc cache) ===
    private readonly Collider[] separationBuffer = new Collider[20];

    // === CARVING ===
    private bool _isInObstacleMode = false;

    // =========================================================
    // PRIORITY
    // =========================================================
    /// <summary>
    /// Số nhỏ = ưu tiên cao (đánh trước, đi trước).
    /// Gốc = Stats.priorityId × 1000 + khoảng cách × 10.
    /// </summary>
    public int GetPriority()
    {
        float dist = PlayerTarget != null
            ? Vector3.Distance(transform.position, PlayerTarget.position)
            : 999f;
        return Stats.priorityId * 1000 + Mathf.RoundToInt(dist * 10f);
    }

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        Agent      = GetComponent<NavMeshAgent>();
        Anim       = GetComponentInChildren<Animator>();
        Controller = GetComponent<CharacterController>();
        Health     = GetComponent<EnemyHealth>();
        Obstacle   = GetComponent<NavMeshObstacle>();

        Agent.updateRotation = false;
        Agent.updatePosition = false;

        Obstacle.carving = false;
        Obstacle.enabled = false;
    }

    private void Start()
    {
        if (PlayerTarget == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) PlayerTarget = go.transform;
        }
        if (PlayerTarget != null) lastPlayerPos = PlayerTarget.position;

        SpawnPosition = transform.position;

        // ĐĂng ký vào AIDirector
        if (AIDirector.Instance != null)
            AIDirector.Instance.RegisterEnemy(this);

        // Lắng nghe sự kiện từ EnemyHealth
        if (Health != null)
        {
            Health.OnDamaged += OnDamaged;
            Health.OnDeath   += OnDeath;
            Health.OnBlocked += OnBlockedAttack;
        }

        SwitchState(new EnemyIdleState(this));
    }

    private void OnDestroy()
    {
        // Giải phóng Token nếu đang cầm mà bị Destroy đột ngột
        if (AIDirector.Instance != null)
        {
            AIDirector.Instance.ReleaseToken(this);
            AIDirector.Instance.UnregisterEnemy(this);
        }

        if (EnemySlotManager.Instance != null && ReservedSlotIndex >= 0)
            EnemySlotManager.Instance.ReleaseSlot(ReservedSlotIndex);

        if (Health != null)
        {
            Health.OnDamaged -= OnDamaged;
            Health.OnDeath   -= OnDeath;
            Health.OnBlocked -= OnBlockedAttack;
        }
    }

    private void Update()
    {
        // 🧊 FROZEN: Khi bị đóng băng, dừng hoàn toàn mọi logic AI
        // Quái đứng khựng như tượng băng, không tick state, không di chuyển
        if (Health != null && Health.isFrozen)
        {
            ManualVelocity = Vector3.zero;
            return;
        }

        currentState?.Tick(Time.deltaTime);
        HandleMovement();
        UpdatePlayerAwareness();
        UpdateAggroMemory();
    }

    // =========================================================
    // STATE MACHINE
    // =========================================================

    public void SwitchState(EnemyState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }

    // =========================================================
    // DAMAGE / DEATH EVENT HANDLERS
    // =========================================================

    public void TakeDamage(float damage, Vector3 attackerPosition)
    {
        // Forward sát thương thẳng vào Health Component (giống hệt PlayerStateMachine)
        if (Health != null)
        {
            Health.TakeDamage(damage, attackerPosition);
        }
        else
        {
            Debug.LogError($"<color=red>LỖI: Chưa kéo file EnemyHealth vào ô 'Health' trong Inspector của con quái {gameObject.name}!</color>");
        }
    }

    private void OnDamaged(float damage, Vector3 attackerPos, bool isHeavy)
    {
        // Đang chết thì bỏ qua
        if (currentState is EnemyDeathState) return;

        // Bật aggro ngay lập tức
        HasAggro = true;
        aggroMemoryTimer = Stats.aggroMemoryDuration;
        LastKnownPlayerPos = attackerPos;

        // --- SUPER ARMOR (Poise) ---
        if (currentState is EnemyAttackState && !isHeavy)
        {
            return;
        }

        // --- PHẢN ỨNG MŨI TÊN (Ranged Reaction) ---
        // Nếu bị bắn từ xa (khoảng cách > attackRange + 3) VÀ đòn nhẹ
        // → quái có cơ hội Dodge/Block thay vì cứ đứng ăn đòn
        float distToAttacker = Vector3.Distance(transform.position, attackerPos);
        bool isRangedHit = distToAttacker > Stats.attackRange + 3f;
        
        if (isRangedHit && !isHeavy)
        {
            bool canReact = (currentState is EnemyIdleState)
                         || (currentState is EnemyStrafeState)
                         || (currentState is EnemyChaseState)
                         || (currentState is EnemyHitReactionState);

            if (canReact)
            {
                float rand = Random.Range(0f, 100f);
                
                // Dodge né mũi tên (tỷ lệ = dodgeChance)
                if (rand <= Stats.dodgeChance)
                {
                    SwitchState(new EnemyDodgeState(this, attackerPos));
                    return;
                }
                // Block giơ khiên đỡ mũi tên tiếp theo (25%)
                else if (rand <= Stats.dodgeChance + 25f)
                {
                    SwitchState(new EnemyBlockState(this));
                    return;
                }
                // Còn lại: ăn đòn bình thường rồi lao tới (GapCloser)
            }
        }

        // Chuyển sang HitReaction — cho phép re-enter để reset timer (GoW standard)
        SwitchState(new EnemyHitReactionState(this, attackerPos, isHeavy));
    }

    private void OnDeath()
    {
        SwitchState(new EnemyDeathState(this));
    }

    private void OnBlockedAttack(Vector3 attackerPos)
    {
        // Nếu Player chém trúng khi quái đang ở BlockState
        if (currentState is EnemyBlockState blockState)
        {
            blockState.OnImpact();
        }
    }

    /// <summary>
    /// Player gọi khi Kratos vung rìu gần quái → quái có thể Dodge.
    /// </summary>
    public void NotifyPlayerAttackNearby(Vector3 attackerPos)
    {
        if (Health == null || Health.IsDead || Health.isFrozen || Health.isParalyzed) return;
        if (currentState is EnemyDeathState || currentState is EnemyHitReactionState) return;

        float dist = Vector3.Distance(transform.position, attackerPos);
        if (dist > Stats.attackRange + 2.5f) return;

        bool canReact = (currentState is EnemyIdleState)
                     || (currentState is EnemyStrafeState)
                     || (currentState is EnemyChaseState);

        if (canReact)
        {
            float rand = Random.Range(0f, 100f);
            
            // Nếu có dodgeChance trong Stats thì dùng, tạm thời mình lấy Random
            if (rand <= Stats.dodgeChance)
            {
                SwitchState(new EnemyDodgeState(this, attackerPos));
            }
            // Giả sử có thêm 25% tỷ lệ giơ khiên đỡ đòn (Block)
            else if (rand <= Stats.dodgeChance + 25f)
            {
                SwitchState(new EnemyBlockState(this));
            }
        }
    }

    // =========================================================
    // PLAYER AWARENESS
    // =========================================================

    private void UpdatePlayerAwareness()
    {
        if (PlayerTarget == null) return;

        float playerSpeed = Vector3.Distance(PlayerTarget.position, lastPlayerPos) / Time.deltaTime;
        IsPlayerMoving = playerSpeed > 1.5f;
        lastPlayerPos = PlayerTarget.position;
    }

    // =========================================================
    // AGGRO MEMORY
    // =========================================================

    private void UpdateAggroMemory()
    {
        if (!HasAggro) return;

        // Nếu đang thấy Player → reset timer
        if (PlayerTarget != null && CanSeePlayer())
        {
            aggroMemoryTimer = Stats.aggroMemoryDuration;
            LastKnownPlayerPos = PlayerTarget.position;
        }
        else
        {
            aggroMemoryTimer -= Time.deltaTime;
            if (aggroMemoryTimer <= 0f)
            {
                HasAggro = false;
            }
        }
    }

    /// <summary>Check nhanh xem có đang thấy Player không (dùng cho aggro memory).</summary>
    private bool CanSeePlayer()
    {
        if (PlayerTarget == null) return false;

        float dist = Vector3.Distance(transform.position, PlayerTarget.position);
        if (dist > Stats.visionRange) return false;

        Vector3 dir = (PlayerTarget.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dir);
        // Trong combat thì dùng 360 độ awareness (đã biết Player ở đâu)
        return angle <= 180f && dist <= Stats.dropAggroRange;
    }

    // =========================================================
    // HANDLE MOVEMENT
    // =========================================================

    private void HandleMovement()
    {
        Vector3 currentVelocity = Vector3.zero;

        // --- 1. NGUỒN VẬN TỐC ---
        // 🟢 FIX: Thêm AttackState (Attack Magnetism) và HitReactionState (Knockback) vào nguồn vận tốc
        if (currentState is EnemyStrafeState || currentState is EnemyDodgeState 
            || currentState is EnemyGapCloserState || currentState is EnemyAttackState
            || currentState is EnemyHitReactionState)
        {
            currentVelocity = ManualVelocity;
        }
        else if (Agent.enabled && !Agent.isStopped)
        {
            currentVelocity = Agent.desiredVelocity;
        }

        // --- 2. SEPARATION (chỉ cho state không tự tính) ---
        if (!(currentState is EnemyStrafeState) && !(currentState is EnemyDodgeState)
            && !(currentState is EnemyDeathState) && !(currentState is EnemyHitReactionState)
            && !(currentState is EnemyAttackState)) // Cực kỳ quan trọng: đang chém thì không bị dạt ra
        {
            currentVelocity += ComputeSeparationForce();
        }

        // --- 3. GRAVITY ---
        Vector3 motion = currentVelocity * Time.deltaTime;

        if (!Controller.isGrounded)
            VerticalVelocity += Physics.gravity.y * Time.deltaTime;
        else if (VerticalVelocity < 0)
            VerticalVelocity = -2f;

        motion.y = VerticalVelocity * Time.deltaTime;

        // --- 4. EXECUTE ---
        if (Controller.enabled) Controller.Move(motion);
        if (Agent.isActiveAndEnabled && Agent.isOnNavMesh) Agent.nextPosition = transform.position;

        // --- 5. CARVING ---
        HandleCarvingSwitch();

        // --- 6. ROTATION (không Strafe/HitReaction/Death/Attack) ---
        // AttackState đã tự xử lý xoay mặt (TrackPlayer), nếu để tự động xoay theo currentVelocity
        // thì khi bị đẩy nó sẽ quay mặt đi chỗ khác chém hụt!
        if (!(currentState is EnemyStrafeState) && !(currentState is EnemyHitReactionState)
            && !(currentState is EnemyDeathState) && !(currentState is EnemyAttackState)
            && !(currentState is EnemyDodgeState))
        {
            Vector3 lookDir = currentVelocity;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                    Stats.turnSpeed * Time.deltaTime * 0.01f);
            }
        }

        // --- 7. ANIMATOR ---
        UpdateAnimator(currentVelocity);
    }

    // =========================================================
    // BOIDS SEPARATION — NonAlloc optimized
    // =========================================================

    public Vector3 ComputeSeparationForce()
    {
        Vector3 separation = Vector3.zero;
        float radius = Stats.separationRadius;

        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, separationBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider col = separationBuffer[i];
            if (col == Controller) continue;

            var other = col.GetComponent<EnemyStateMachine>();
            if (other == null) continue;

            Vector3 diff = transform.position - other.transform.position;
            diff.y = 0f;

            float dist = diff.magnitude;
            if (dist < 0.01f)
            {
                diff = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
                dist = 0.01f;
            }

            float strength = (radius - dist) / radius;
            separation += diff.normalized * (strength * Stats.separationStrength);
        }

        return separation;
    }

    // =========================================================
    // CARVING — State-driven
    // =========================================================

    private void HandleCarvingSwitch()
    {
        bool shouldCarve = currentState is EnemyIdleState;

        if (shouldCarve && !_isInObstacleMode)
        {
            Agent.isStopped   = true;
            Agent.enabled     = false;
            Obstacle.enabled  = true;
            Obstacle.carving  = true;
            _isInObstacleMode = true;
        }
        else if (!shouldCarve && _isInObstacleMode)
        {
            Obstacle.carving  = false;
            Obstacle.enabled  = false;
            Agent.enabled     = true;
            _isInObstacleMode = false;

            if (Agent.isOnNavMesh)
                Agent.nextPosition = transform.position;
        }
    }

    public void EnableAgentMode()
    {
        if (_isInObstacleMode)
        {
            Obstacle.carving  = false;
            Obstacle.enabled  = false;
            Agent.enabled     = true;
            _isInObstacleMode = false;
            if (Agent.isOnNavMesh) Agent.nextPosition = transform.position;
        }
    }

    // =========================================================
    // ANIMATOR
    // =========================================================

    private void UpdateAnimator(Vector3 velocity)
    {
        // Chống nhiễu số (khử các giá trị quá nhỏ như 0.00005 khiến Anim nhảy loạn xạ)
        if (velocity.magnitude < 0.05f) velocity = Vector3.zero;

        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        float maxSpeed = Stats.moveSpeed > 0 ? Stats.moveSpeed : 1f;
        
        // Khi đi tuần (Patrol), vận tốc vật lý bị giảm còn 40%. 
        // Nhưng ta muốn Anim vẫn phát đủ biên độ (đạt Z=1) để không bị nhòe với Idle.
        if (currentState is EnemyPatrolState && velocity.magnitude > 0.1f)
        {
            maxSpeed *= 0.4f; // Normalize lại để Z chạm tới 1.0
        }

        float velocityX = localVelocity.x / maxSpeed;
        float velocityZ = localVelocity.z / maxSpeed;

        Anim.SetFloat("VelocityX", velocityX, 0.1f, Time.deltaTime);
        Anim.SetFloat("VelocityZ", velocityZ, 0.1f, Time.deltaTime);
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmos()
    {
        if (currentState == null) return;

        Vector3 head = transform.position + Vector3.up * 2.5f;

        if (currentState is EnemyAttackState)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(head, 0.3f);
        }
        else if (currentState is EnemyStrafeState)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(head, Vector3.one * 0.3f);
        }
        else if (currentState is EnemyHitReactionState)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(head, 0.4f);
        }
        else if (currentState is EnemyDeathState)
        {
            Gizmos.color = Color.black;
            Gizmos.DrawWireSphere(head, 0.5f);
        }
        else if (currentState is EnemyDodgeState)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(head, 0.3f);
        }

        // Vùng Separation
        if (currentState is EnemyStrafeState && Stats != null)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.12f);
            Gizmos.DrawSphere(transform.position, Stats.separationRadius);
        }

        // Slot link
        if (Application.isPlaying && ReservedSlotIndex >= 0 && EnemySlotManager.Instance != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position + Vector3.up,
                EnemySlotManager.Instance.WorldSlotPosition(ReservedSlotIndex) + Vector3.up);
        }

        // Aggro indicator
        if (Application.isPlaying && HasAggro)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up * 2f, LastKnownPlayerPos + Vector3.up);
        }
    }

    private void OnAnimatorMove()
    {
        if (Anim == null || !Anim.applyRootMotion) return;

        // Bắt sự kiện Root Motion từ Animator và truyền thẳng vào CharacterController
        // Áp dụng cho các State cần xài Root Motion thật (ví dụ: Dodge, GapCloser)
        if (currentState is EnemyDodgeState || currentState is EnemyGapCloserState || currentState is EnemyAttackState)
        {
            Vector3 rootMotion = Anim.deltaPosition;
            rootMotion.y = 0; // Khóa Y để quái không bay lên trời
            if (rootMotion.sqrMagnitude > 0.0001f)
            {
                Controller.Move(rootMotion);
            }
        }
    }
}