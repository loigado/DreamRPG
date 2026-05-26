using UnityEngine;
using UnityEngine.AI;

// 🟢 ĐỊNH NGHĨA CÁC LOẠI ÁM HIỆU
public enum EnemyCalloutType
{
    RangedWarning, // "Dosho!" - Dạt ra cho tao bắn!
    ShieldWall,    // Gọi đồng minh nấp sau khiên
    FlankAttack    // Bao vây đánh úp
}

[RequireComponent(typeof(NavMeshAgent), typeof(Animator), typeof(CharacterController))]
[RequireComponent(typeof(NavMeshObstacle))]
public class EnemyStateMachine : MonoBehaviour, IDamageable
{
    // === 🟢 SYNERGY / CALLOUT SYSTEM ===
    public static event System.Action<EnemyStateMachine, EnemyCalloutType> OnEnemyCallout;

    [Header("Core Data")]
    public EnemyStatsSO Stats;
    public Transform PlayerTarget;

    [Header("Skills & VFX")]
    public Transform skillOriginPoint; 
    [Tooltip("Vị trí xương miệng/đầu của quái vật")]
    public Transform mouthPoint;       

    // === COMPONENTS ===
    public NavMeshAgent        Agent      { get; private set; }
    public Animator            Anim       { get; private set; }
    public CharacterController Controller { get; private set; }
    public EnemyHealth         Health     { get; private set; }
    public NavMeshObstacle     Obstacle   { get; private set; }

    // === STATE CACHING ===
    public EnemyState currentState { get; private set; }
    public EnemyIdleState       IdleState       { get; private set; }
    public EnemyPatrolState     PatrolState     { get; private set; }
    public EnemyChaseState      ChaseState      { get; private set; }
    public EnemyStrafeState     StrafeState     { get; private set; }
    public EnemyAttackState     AttackState     { get; private set; }
    public EnemyDodgeState      DodgeState      { get; private set; }
    public EnemyBlockState      BlockState      { get; private set; }
    public EnemyGapCloserState  GapCloserState  { get; private set; }
    public EnemyDeathState      DeathState      { get; private set; }
    public EnemyRangeAttackState RangeAttackState { get; private set; }
    public EnemyHitReactionState HitReactionState { get; private set; }
    public EnemyClearPathState  ClearPathState  { get; private set; }
    public EnemyParriedState    ParriedState    { get; private set; }
    public BossPhaseTransitionState PhaseTransitionState { get; private set; }

    // === DATA ===
    public Vector3 SpawnPosition   { get; private set; }
    public float SkillCooldownTimer { get; set; } = 0f;
    public float DodgeCooldownTimer { get; set; } = 0f;
    public float MeleeBurstCooldownTimer { get; set; } = 0f;
    public bool isTurningInPlace { get; set; } = false;
    public float   VerticalVelocity { get; private set; }
    public Vector3 ManualVelocity   { get; set; }
    public Vector3 LookAtTarget     { get; set; }
    public bool IsPlayerMoving { get; private set; }
    public bool IsDodging { get; set; } = false;
    public bool IsPhase2 { get; set; } = false;
    private Vector3 lastPlayerPos;
    private float lastRotationY;

    // === SEKIRO REACTIVE AI ===
    public bool IsPlayerBlocking { get; private set; }
    public bool IsPlayerAttacking { get; private set; }
    public float PlayerGuardDuration { get; private set; }
    
    // === GOT TURN-STEALING ===
    public int ConsecutiveHitsTaken { get; private set; } = 0;
    private float lastHitTime = 0f;
    
    // =========================================================
    // === AGGRO & SLOT PROPERTY ===
    // =========================================================
    private bool _hasAggro;
    public bool HasAggro          
    { 
        get => _hasAggro;
        set 
        {
            // Nếu LẦN ĐẦU TIÊN vào combat (chuyển từ false sang true) VÀ đây là Boss
            if (value == true && _hasAggro == false && Stats != null && Stats.isMiniBoss)
            {
                if (BossHUD.Instance != null && Health != null)
                {
                    string nameToShow = string.IsNullOrEmpty(Stats.bossName) ? gameObject.name : Stats.bossName;
                    BossHUD.Instance.SetupBossHUD(Health, nameToShow); 
                }
            }
            _hasAggro = value;
        }
    }

    public Vector3 LastKnownPlayerPos { get; set; }
    private float aggroMemoryTimer;
    public int ReservedSlotIndex { get; set; } = -1;
    public bool IsHoldingAttackToken { get; set; } = false;

    private readonly Collider[] separationBuffer = new Collider[20];
    private bool _isInObstacleMode = false;

    public int GetPriority()
    {
        float dist = PlayerTarget != null ? Vector3.Distance(transform.position, PlayerTarget.position) : 999f;
        return Stats.priorityId * 1000 + Mathf.RoundToInt(dist * 10f);
    }

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

        IdleState       = new EnemyIdleState(this);
        PatrolState     = new EnemyPatrolState(this);
        ChaseState      = new EnemyChaseState(this);
        StrafeState     = new EnemyStrafeState(this);
        AttackState     = new EnemyAttackState(this);
        DodgeState      = new EnemyDodgeState(this);
        BlockState      = new EnemyBlockState(this);
        GapCloserState  = new EnemyGapCloserState(this);
        DeathState      = new EnemyDeathState(this);
        RangeAttackState = new EnemyRangeAttackState(this);
        HitReactionState = new EnemyHitReactionState(this);
        ClearPathState  = new EnemyClearPathState(this);
        ParriedState    = new EnemyParriedState(this);
        PhaseTransitionState = new BossPhaseTransitionState(this);
    }

    private void OnEnable()
    {
        if (Health != null)
        {
            Health.OnDamaged += OnDamaged;
            Health.OnDeath   += OnDeath;
            Health.OnBlocked += OnBlockedAttack;
            Health.OnPostureBroken += TriggerParryStagger;
        }

        if (currentState != null)
        {
            SwitchState(IdleState);
        }

        if (AIDirector.Instance != null)
        {
            AIDirector.Instance.RegisterEnemy(this);
        }
    }

    private void OnDisable()
    {
        if (Health != null)
        {
            Health.OnDamaged -= OnDamaged;
            Health.OnDeath   -= OnDeath;
            Health.OnBlocked -= OnBlockedAttack;
            Health.OnPostureBroken -= TriggerParryStagger;
        }

        if (AIDirector.Instance != null)
        {
            AIDirector.Instance.ReleaseToken(this);
            AIDirector.Instance.UnregisterEnemy(this);
        }
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

        OnEnemyCallout += HandleAllyCallout;

        if (currentState == null) SwitchState(IdleState);
        lastRotationY = transform.eulerAngles.y;
    }
    
    private void OnDestroy()
    {
        if (EnemySlotManager.Instance != null && ReservedSlotIndex >= 0)
            EnemySlotManager.Instance.ReleaseSlot(ReservedSlotIndex);

        OnEnemyCallout -= HandleAllyCallout;
    }

    private void Update()
    {
        if (Health != null && Health.isFrozen)
        {
            ManualVelocity = Vector3.zero;
            return;
        }
        if (SkillCooldownTimer > 0f) SkillCooldownTimer -= Time.deltaTime;
        if (DodgeCooldownTimer > 0f) DodgeCooldownTimer -= Time.deltaTime;
        if (MeleeBurstCooldownTimer > 0f) MeleeBurstCooldownTimer -= Time.deltaTime;

        UpdateReactiveAI();
        currentState?.Tick(Time.deltaTime);
        HandleMovement();
        UpdatePlayerAwareness();
        UpdateAggroMemory();
    }

    public void BroadcastCallout(EnemyCalloutType type)
    {
        Debug.Log($"<color=cyan>📢 [SYNERGY] {gameObject.name} hô to: {type}!</color>");
        OnEnemyCallout?.Invoke(this, type);
    }

    private void HandleAllyCallout(EnemyStateMachine caller, EnemyCalloutType type)
    {
        if (caller == this || Health == null || Health.IsDead || Health.isFrozen) return;
        if (currentState == HitReactionState || currentState == DeathState || currentState == ClearPathState) return;
        if (PlayerTarget == null) return;

        float distToCaller = Vector3.Distance(transform.position, caller.transform.position);
        if (distToCaller > 15f) return;

        if (type == EnemyCalloutType.RangedWarning)
        {
            Vector3 dirToPlayer = (PlayerTarget.position - caller.transform.position).normalized;
            Vector3 dirToMe = (transform.position - caller.transform.position).normalized;
            float angle = Vector3.Angle(dirToPlayer, dirToMe);
            
            float myDistToCaller = Vector3.Distance(transform.position, caller.transform.position);
            float playerDistToCaller = Vector3.Distance(PlayerTarget.position, caller.transform.position);

            if (angle < 35f && myDistToCaller < playerDistToCaller)
            {
                Debug.Log($"<color=orange>⚠️ {gameObject.name} dạt sang bên để nhường đường cho đồng đội!</color>");
                ClearPathState.Setup(caller.transform.position, PlayerTarget.position);
                SwitchState(ClearPathState);
            }
        }
    }

    private void UpdateReactiveAI()
    {
        if (PlayerTarget != null)
        {
            var playerState = PlayerTarget.GetComponentInParent<PlayerStateMachine>();
            if (playerState != null && playerState.currentState != null)
            {
                IsPlayerBlocking = playerState.currentState is PlayerBlockState || playerState.currentState is PlayerParryDecisionState;
                IsPlayerAttacking = playerState.currentState is PlayerAttackState || playerState.currentState is PlayerParryAttackState;

                if (IsPlayerBlocking) PlayerGuardDuration += Time.deltaTime;
                else PlayerGuardDuration = 0f;
            }
        }
    }

    // =========================================================
    // 🟢 CẬP NHẬT: LỚP BẢO VỆ KHI CHUYỂN TRẠNG THÁI
    // =========================================================
    public void SwitchState(EnemyState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();

        // Nếu bất kỳ logic AI nào chủ động ép quái vào trạng thái tấn công/truy đuổi người chơi -> Bật Aggro UI lập tức
        if (newState == ChaseState || newState == AttackState || newState == RangeAttackState || newState == GapCloserState)
        {
            HasAggro = true;
        }
    }

    public void TakeDamage(float damage, Vector3 attackerPosition, bool isHeavy = false) { if (Health != null) Health.TakeDamage(damage, attackerPosition, isHeavy); }

    private void OnDamaged(float damage, Vector3 attackerPos, bool isHeavy)
    {
        if (currentState == DeathState) return;
        
        if (Stats.isMiniBoss && !IsPhase2)
        {
            float healthPercent = Health.Health / Health.MaxHealth; 
            if (healthPercent <= 0.5f)
            {
                SwitchState(PhaseTransitionState);
                return; 
            }
        }
        
        HasAggro = true;
        aggroMemoryTimer = Stats.aggroMemoryDuration;
        LastKnownPlayerPos = attackerPos;

        // =========================================================
        // TURN-STEALING (CHỐNG STUNLOCK) & HIT TRACKING
        // =========================================================
        if (Time.time - lastHitTime > 1.5f) ConsecutiveHitsTaken = 0; 
        ConsecutiveHitsTaken++;
        lastHitTime = Time.time;

        if (Stats.isMiniBoss)
        {
            // 1. Kiểm tra Super Armor lúc ra chiêu
            bool hasSuperArmor = (currentState == AttackState && !AttackState.IsInRecovery) || 
                                 (currentState == BlockState) ||
                                 (currentState == GapCloserState) ||
                                 (currentState == RangeAttackState) ||
                                 (currentState == PhaseTransitionState);

            if (hasSuperArmor) return; 

            if (currentState == IdleState)
            {
                SwitchState(ChaseState);
                return;
            }

            // 🟢 MỚI: Xử lý né đòn tầm xa cho Boss (trước khi lỳ đòn với Light Attack)
            float dist = Vector3.Distance(transform.position, attackerPos);
            if (dist > Stats.attackRange + 3f && !isHeavy)
            {
                bool canReact = (currentState == StrafeState) || (currentState == ChaseState);
                if (canReact)
                {
                    float rand = Random.Range(0f, 100f);
                    // Boss rất nhạy bén với đòn tầm xa, tăng tỷ lệ né
                    if (rand <= Stats.dodgeChance + 40f && DodgeCooldownTimer <= 0f) 
                    {
                        DodgeCooldownTimer = 2.5f; // Né nhanh hơn lính thường
                        DodgeState.Setup(attackerPos);
                        SwitchState(DodgeState);
                        return;
                    }
                }
            }

            // 2. Nếu là đòn nhẹ (Light Attack), Boss hoàn toàn lỳ đòn, KHÔNG bị flinch!
            if (!isHeavy)
            {
                // Mặc dù lỳ đòn, nhưng nếu bị chém trúng quá nhiều (5 hit), Boss sẽ dùng chiêu giải vây
                if (ConsecutiveHitsTaken >= 5)
                {
                    ConsecutiveHitsTaken = 0; 
                    if (MeleeBurstCooldownTimer <= 0f)
                    {
                        SwitchState(RangeAttackState);
                        return;
                    }
                    float rand = Random.Range(0f, 100f);
                    if (rand <= Stats.dodgeChance + 20f && DodgeCooldownTimer <= 0f)
                    {
                        DodgeCooldownTimer = 4f;
                        DodgeState.Setup(attackerPos);
                        SwitchState(DodgeState);
                        return;
                    }
                    else if (Stats.maxGuard > 0f)
                    {
                        SwitchState(BlockState);
                        return;
                    }
                }
                
                // Trả về luôn, KHÔNG nhảy xuống dòng SwitchState(HitReactionState) bên dưới
                return; 
            }
        }
        else // Quái thường
        {
            if (currentState == AttackState && !isHeavy)
            {
                if (AttackState.CurrentGlint == AttackGlint.Blue || AttackState.CurrentGlint == AttackGlint.Red) return; 
            }

            if (ConsecutiveHitsTaken >= 3)
            {
                ConsecutiveHitsTaken = 0; 
                float rand = Random.Range(0f, 100f);
                if (rand <= Stats.dodgeChance + 20f && DodgeCooldownTimer <= 0f)
                {
                    DodgeCooldownTimer = 4f;
                    DodgeState.Setup(attackerPos);
                    SwitchState(DodgeState);
                    return;
                }
                else if (Stats.maxGuard > 0f)
                {
                    SwitchState(BlockState);
                    return;
                }
            }

            float dist = Vector3.Distance(transform.position, attackerPos);
            if (dist > Stats.attackRange + 3f && !isHeavy)
            {
                bool canReact = (currentState == IdleState) || (currentState == StrafeState) || (currentState == ChaseState);
                if (canReact)
                {
                    float rand = Random.Range(0f, 100f);
                    if (rand <= Stats.dodgeChance && DodgeCooldownTimer <= 0f) 
                    {
                        DodgeCooldownTimer = 4f; 
                        DodgeState.Setup(attackerPos);
                        SwitchState(DodgeState);
                        return;
                    }
                }
            }
        }

        HitReactionState.Setup(attackerPos, isHeavy);
        SwitchState(HitReactionState);
    }
    
    public void TriggerParryStagger()
    {
        if (currentState != DeathState)
        {
            SwitchState(ParriedState);
        }
    }

    private void OnDeath() { SwitchState(DeathState); }
    private void OnBlockedAttack(Vector3 attackerPos) { if (currentState == BlockState) BlockState.OnImpact(); }

    private float lastReactToPlayerAttackTime = 0f;

    public void NotifyPlayerAttackNearby(Vector3 attackerPos)
    {
        if (Health == null || Health.IsDead || Health.isFrozen || Health.isParalyzed) return;
        if (currentState == DeathState) return; 

        if (Time.time - lastReactToPlayerAttackTime < 2.0f) return;
        lastReactToPlayerAttackTime = Time.time;

        float dist = Vector3.Distance(transform.position, attackerPos);
        float threshold = Stats.attackRange + 1.5f; 
        if (dist > threshold) return;

        bool canReact = (currentState == IdleState) || (currentState == StrafeState) || 
                        (currentState == ChaseState) || (currentState == GapCloserState);

        if (canReact)
        {
            float rand = Random.Range(0f, 100f); 

            if (rand <= (Stats.dodgeChance / 2f) && DodgeCooldownTimer <= 0f) 
            {
                DodgeCooldownTimer = 4f; 
                DodgeState.Setup(attackerPos);
                SwitchState(DodgeState);
                return;
            }
            else if (Stats != null && Stats.maxGuard > 0f && rand <= (Stats.dodgeChance / 2f) + 15f)
            {
                SwitchState(BlockState);
            }
        }
    }

    private void UpdatePlayerAwareness()
    {
        if (PlayerTarget == null) return;
        float playerSpeed = Vector3.Distance(PlayerTarget.position, lastPlayerPos) / Time.deltaTime;
        IsPlayerMoving = playerSpeed > 1.5f;
        lastPlayerPos = PlayerTarget.position;
    }

    // =========================================================
    // 🟢 CẬP NHẬT: KÍCH HOẠT AGGRO BẰNG TẦM NHÌN (SIGHT DETECTION)
    // =========================================================
    private void UpdateAggroMemory()
    {
        // Nếu chưa có Aggro mà Kratos lọt vào tầm nhìn -> Kích hoạt Aggro UI ngay lập tức!
        if (!HasAggro)
        {
            if (PlayerTarget != null && CanSeePlayer())
            {
                HasAggro = true;
                aggroMemoryTimer = Stats.aggroMemoryDuration;
                LastKnownPlayerPos = PlayerTarget.position;
            }
            return;
        }

        // Logic đếm ngược bộ nhớ Aggro cũ giữ nguyên
        if (PlayerTarget != null && CanSeePlayer())
        {
            aggroMemoryTimer = Stats.aggroMemoryDuration;
            LastKnownPlayerPos = PlayerTarget.position;
        }
        else
        {
            aggroMemoryTimer -= Time.deltaTime;
            if (aggroMemoryTimer <= 0f) HasAggro = false;
        }
    }

    private bool CanSeePlayer()
    {
        if (PlayerTarget == null) return false;
        float dist = Vector3.Distance(transform.position, PlayerTarget.position);
        if (dist > Stats.visionRange) return false;

        Vector3 dir = (PlayerTarget.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dir);
        return angle <= 180f && dist <= Stats.dropAggroRange;
    }

    private void HandleMovement()
    {
        Vector3 currentVelocity = Vector3.zero;

        if (currentState == StrafeState || currentState == DodgeState || 
            currentState == GapCloserState || currentState == AttackState ||
            currentState == HitReactionState || currentState == ClearPathState) 
        {
            currentVelocity = ManualVelocity;
        }
        else if (Agent.enabled && !Agent.isStopped)
        {
            currentVelocity = Agent.desiredVelocity;
        }

        if (currentState != StrafeState && currentState != DodgeState && 
            currentState != DeathState && currentState != AttackState &&
            currentState != HitReactionState && currentState != ClearPathState)
        {
            currentVelocity += ComputeSeparationForce();
        }

        Vector3 motion = currentVelocity * Time.deltaTime;
        if (!Controller.isGrounded) VerticalVelocity += Physics.gravity.y * Time.deltaTime;
        else if (VerticalVelocity < 0) VerticalVelocity = -2f;
        motion.y = VerticalVelocity * Time.deltaTime;

        if (Controller.enabled) Controller.Move(motion);
        if (Agent.isActiveAndEnabled && Agent.isOnNavMesh) Agent.nextPosition = transform.position;

        HandleCarvingSwitch();

        if (currentState != StrafeState && currentState != DeathState && 
            currentState != AttackState && currentState != DodgeState &&
            currentState != HitReactionState && currentState != ClearPathState)
        {
            Vector3 lookDir = currentVelocity;
            lookDir.y = 0;
            if (lookDir.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, Stats.turnSpeed * Time.deltaTime);
            }
        }

        UpdateAnimator(currentVelocity);
    }

    public Vector3 ComputeSeparationForce()
    {
        Vector3 separation = Vector3.zero;
        float baseRadius = Stats.separationRadius;

        float searchRadius = baseRadius * 3f;
        int count = Physics.OverlapSphereNonAlloc(transform.position, searchRadius, separationBuffer);
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

            float effectiveRadius = baseRadius;
            if (dist > effectiveRadius) continue;

            float strength = (effectiveRadius - dist) / effectiveRadius;
            separation += diff.normalized * (strength * Stats.separationStrength);
        }
        return separation;
    }

    private void HandleCarvingSwitch()
    {
        bool shouldCarve = currentState == IdleState;

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

            if (Agent.isOnNavMesh) Agent.nextPosition = transform.position;
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

    private void UpdateAnimator(Vector3 velocity)
    {
        float speedMag = velocity.magnitude;

        float currentRotY = transform.eulerAngles.y;
        float deltaRotY = Mathf.DeltaAngle(lastRotationY, currentRotY);
        float turnSpeed = (Time.deltaTime > 0) ? (deltaRotY / Time.deltaTime) : 0f;
        lastRotationY = currentRotY;
        float normalizedTurn = turnSpeed / Stats.turnSpeed;

        if (speedMag < 0.05f)
        {
            float velX = 0f;
            if (Mathf.Abs(normalizedTurn) > 0.05f) velX = normalizedTurn;

            Anim.SetFloat(EnemyConstants.HashVelocityX, velX, 0.1f, Time.deltaTime);
            Anim.SetFloat(EnemyConstants.HashVelocityZ, 0f, 0.1f, Time.deltaTime);
            Anim.SetFloat("MoveMultiplier", 1f); 
            return;
        }

        Vector3 localDir = transform.InverseTransformDirection(velocity.normalized);

        float walkSpeed = 2.0f;
        float runSpeed = 5.0f; 

        float blendMagnitude = 1f; 
        float animMultiplier = 1f; 

        if (speedMag <= walkSpeed + 0.5f) 
        {
            blendMagnitude = 1f;
            animMultiplier = speedMag / walkSpeed;
            animMultiplier = Mathf.Clamp(animMultiplier, 0.5f, 1.2f);
        }
        else
        {
            blendMagnitude = Mathf.Lerp(1f, 2f, (speedMag - walkSpeed) / (runSpeed - walkSpeed));
            blendMagnitude = Mathf.Clamp(blendMagnitude, 1f, 2f);
            
            animMultiplier = speedMag / runSpeed;
            animMultiplier = Mathf.Clamp(animMultiplier, 0.8f, 1.5f);
        }

        Anim.SetFloat(EnemyConstants.HashVelocityX, localDir.x * blendMagnitude, 0.1f, Time.deltaTime);
        Anim.SetFloat(EnemyConstants.HashVelocityZ, localDir.z * blendMagnitude, 0.1f, Time.deltaTime);
        Anim.SetFloat("MoveMultiplier", animMultiplier);
    }

    public void OnAnimatorMoveProxy()
    {
        if (Anim == null || !Anim.applyRootMotion) return;
        if (currentState == DodgeState || currentState == GapCloserState || currentState == AttackState)
        {
            Vector3 rootMotion = Anim.deltaPosition;
            rootMotion.y = 0; 
            if (rootMotion.sqrMagnitude > 0.0001f && Controller.enabled) Controller.Move(rootMotion);
        }
    }

    public void AnimationEvent_TriggerHitbox()
    {
        if (currentState == AttackState) 
            AttackState.PerformHitboxCheck();
            
        else if (currentState == RangeAttackState) 
            RangeAttackState.FireProjectile(); 
    }

    public void AnimationEvent_TriggerPhase2Explosion()
    {
        // 🟢 Được gọi từ Animation Event của đòn gầm thét Phase 2
        if (currentState == PhaseTransitionState && Stats.meleeBurstPrefab != null)
        {
            GameObject explosion = ObjectPoolManager.Instance.SpawnFromPool(
                Stats.meleeBurstPrefab, transform.position, Quaternion.identity);

            LightningAOE aoeScript = explosion.GetComponent<LightningAOE>();
            if (aoeScript != null)
            {
                aoeScript.Setup(Stats.attackDamage, Stats.enemyElement, true);
            }
        }
    }

    public void FaceTarget(Vector3 targetPos, float speedMultiplier = 1f)
    {
        Vector3 dirToTarget = (targetPos - transform.position);
        dirToTarget.y = 0f;
        if (dirToTarget.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dirToTarget.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, Stats.turnSpeed * speedMultiplier * Time.deltaTime);
    }
}