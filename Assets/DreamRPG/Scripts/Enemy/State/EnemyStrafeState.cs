using UnityEngine;

public class EnemyStrafeState : EnemyState
{
    private float maneuverTimer;
    private float feintTimer;

    private int  reservedSlot = -1;
    private bool isInnerRing;
    private int strafeDirection;

    private float pacingTimer;
    private float targetPacingOffset;
    private float currentPacingOffset;

    private float yieldTimer;
    private bool  isYielding;

    private float bounceCooldown; 
    private readonly Collider[] yieldCheckBuffer = new Collider[15];

    public EnemyStrafeState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        maneuverTimer = Random.Range(stateMachine.Stats.strafeTimeMin, stateMachine.Stats.strafeTimeMax);
        feintTimer    = Random.Range(stateMachine.Stats.feintIntervalMin, stateMachine.Stats.feintIntervalMax);

        isInnerRing = stateMachine.Stats.prefersOuterRing ? false : (Random.value > 0.4f);

        if (EnemySlotManager.Instance != null)
        {
            reservedSlot = EnemySlotManager.Instance.ReserveSlot(stateMachine, isInnerRing);
            stateMachine.ReservedSlotIndex = reservedSlot;

            if (reservedSlot < 0)
            {
                reservedSlot = EnemySlotManager.Instance.ReserveSlot(stateMachine, !isInnerRing);
                if (reservedSlot >= 0) isInnerRing = !isInnerRing;
                stateMachine.ReservedSlotIndex = reservedSlot;
            }
        }

        Vector3 dirToEnemy = (stateMachine.transform.position - stateMachine.PlayerTarget.position).normalized;
        float angleToRight = Vector3.Angle(stateMachine.PlayerTarget.right, dirToEnemy);
        strafeDirection = angleToRight < 90f ? 1 : -1;

        pacingTimer         = 0f;
        targetPacingOffset  = 0f;
        currentPacingOffset = 0f;
        isYielding          = false;
        yieldTimer          = 0f;
        bounceCooldown      = 0f; 

        if (stateMachine.Agent.isActiveAndEnabled) stateMachine.Agent.isStopped = true;
    }

    public override void Tick(float deltaTime)
    {
        if (stateMachine.PlayerTarget == null) return;

        if (bounceCooldown > 0f) bounceCooldown -= deltaTime;

        stateMachine.LastKnownPlayerPos = stateMachine.PlayerTarget.position;
        FacePlayer(deltaTime);

        feintTimer -= deltaTime;
        if (feintTimer <= 0)
        {
            feintTimer = Random.Range(stateMachine.Stats.feintIntervalMin, stateMachine.Stats.feintIntervalMax);
        }

        UpdatePacing(deltaTime);
        ComputeAndSetManualVelocity(deltaTime);
        CheckExitConditions(deltaTime);
    }

    public override void Exit()
    {
        if (EnemySlotManager.Instance != null && reservedSlot >= 0)
            EnemySlotManager.Instance.ReleaseSlot(reservedSlot);

        reservedSlot = -1;
        stateMachine.ReservedSlotIndex = -1;
        stateMachine.ManualVelocity    = Vector3.zero;
    }

    private void FacePlayer(float deltaTime)
    {
        stateMachine.LookAtTarget = stateMachine.PlayerTarget.position + Vector3.up * 1.2f;
        stateMachine.FaceTarget(stateMachine.PlayerTarget.position);
    }

    private void UpdatePacing(float deltaTime)
    {
        pacingTimer -= deltaTime;
        if (pacingTimer <= 0)
        {
            targetPacingOffset = Random.Range(-1.5f, 2f);
            pacingTimer        = Random.Range(1.5f, 3f);
            if (Random.value > 0.7f) strafeDirection *= -1;
        }
        currentPacingOffset = Mathf.Lerp(currentPacingOffset, targetPacingOffset, deltaTime * 1.5f);
    }

    private void ComputeAndSetManualVelocity(float deltaTime)
    {
        float moveSpeed = stateMachine.Stats.moveSpeed * (isInnerRing ? 0.7f : 0.5f);
        Vector3 goalVelocity = ComputeGoalVelocity(moveSpeed);
        Vector3 separationVelocity = stateMachine.ComputeSeparationForce();
        float speedMultiplier = CheckYieldingMultiplier(deltaTime);
        
        Vector3 targetVelocity = goalVelocity * speedMultiplier + separationVelocity;
        targetVelocity = Vector3.ClampMagnitude(targetVelocity, stateMachine.Stats.moveSpeed);

        stateMachine.ManualVelocity = Vector3.Lerp(stateMachine.ManualVelocity, targetVelocity, deltaTime * 8f);
    }

    private Vector3 ComputeGoalVelocity(float moveSpeed)
    {
        Vector3 dirToPlayer = (stateMachine.PlayerTarget.position - stateMachine.transform.position).normalized;
        dirToPlayer.y = 0f;

        float currentDist = Vector3.Distance(stateMachine.transform.position, stateMachine.PlayerTarget.position) - stateMachine.Controller.radius;

        if (reservedSlot >= 0 && EnemySlotManager.Instance != null)
        {
            Vector3 slotPos = EnemySlotManager.Instance.WorldSlotPosition(reservedSlot);
            Vector3 slotToPlayer = (stateMachine.PlayerTarget.position - slotPos).normalized;
            
            slotPos += slotToPlayer * currentPacingOffset;
            slotPos.y = stateMachine.transform.position.y;
            
            Vector3 toSlot   = slotPos - stateMachine.transform.position;
            toSlot.y = 0f;
            float distToSlot = toSlot.magnitude;

            if (distToSlot < stateMachine.Stats.slotArrivalTolerance)
            {
                float slowDownFactor = distToSlot / stateMachine.Stats.slotArrivalTolerance;
                return toSlot.normalized * (moveSpeed * slowDownFactor);
            }

            return toSlot.normalized * moveSpeed;
        }

        float baseRadius    = isInnerRing ? stateMachine.Stats.innerRingRadius : stateMachine.Stats.outerRingRadius;
        float dynamicRadius = Mathf.Max(baseRadius + currentPacingOffset, 1.5f);
        Vector3 velocity = Vector3.zero;

        if (currentDist < dynamicRadius - 0.5f)
            velocity += -dirToPlayer * (moveSpeed * 0.5f); 
        else if (currentDist > dynamicRadius + 0.5f)
        {
            velocity += dirToPlayer * moveSpeed;
            if (currentDist > stateMachine.Stats.outerRingRadius + 4f)
            {
                stateMachine.SwitchState(stateMachine.ChaseState);
                return Vector3.zero;
            }
        }

        Vector3 strafeDirFb = Vector3.Cross(dirToPlayer, Vector3.up) * strafeDirection;
        Vector3 rayStart    = stateMachine.transform.position + Vector3.up;
        
        if (bounceCooldown <= 0f)
        {
            if (Physics.Raycast(rayStart, strafeDirFb, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                strafeDirection *= -1;
                strafeDirFb = -strafeDirFb;
                bounceCooldown = 0.6f; 
            }
        }
        
        velocity += strafeDirFb * moveSpeed;
        return velocity;
    }

    private float CheckYieldingMultiplier(float deltaTime)
    {
        if (isYielding)
        {
            yieldTimer -= deltaTime;
            if (yieldTimer <= 0f) { isYielding = false; return 1f; }
            return stateMachine.Stats.yieldSpeedMultiplier;
        }

        float checkRadius = stateMachine.Stats.separationRadius * 2.5f;
        int count = Physics.OverlapSphereNonAlloc(stateMachine.transform.position, checkRadius, yieldCheckBuffer);

        for (int i = 0; i < count; i++)
        {
            if (yieldCheckBuffer[i] == stateMachine.Controller) continue;
            var other = yieldCheckBuffer[i].GetComponent<EnemyStateMachine>();
            if (other == null || !(other.currentState == this)) continue;

            Vector3 diff = other.transform.position - stateMachine.transform.position;
            diff.y = 0f;

            float dot = Vector3.Dot(stateMachine.ManualVelocity.normalized, diff.normalized);
            if (dot > 0.5f)
            {
                if (stateMachine.GetPriority() > other.GetPriority())
                {
                    isYielding = true;
                    yieldTimer = stateMachine.Stats.yieldMaxDuration;
                    return stateMachine.Stats.yieldSpeedMultiplier;
                }
            }
        }
        return 1f;
    }

    private void CheckExitConditions(float deltaTime)
    {
        float distToPlayer = Vector3.Distance(stateMachine.transform.position, stateMachine.PlayerTarget.position) - stateMachine.Controller.radius;

        if (stateMachine.Stats.isRangedEnemy && distToPlayer < 4f)
        {
            if (stateMachine.DodgeCooldownTimer <= 0f)
            {
                stateMachine.DodgeCooldownTimer = 4f; 
                stateMachine.DodgeState.Setup(stateMachine.PlayerTarget.position);
                stateMachine.SwitchState(stateMachine.DodgeState);
                return;
            }
            else if (distToPlayer <= 3.5f && stateMachine.MeleeBurstCooldownTimer <= 0f) 
            {
                stateMachine.SwitchState(stateMachine.RangeAttackState);
                return;
            }
        }

        // ── MINI-BOSS PHÓNG SÓNG SÉT KHI ĐANG VỜN (STRAFE) ───────
        // Cập nhật: Dùng rangedProjectilePrefab (vì đã đổi sang gọi sấm sét)
        if (stateMachine.Stats.isMiniBoss && stateMachine.Stats.rangedProjectilePrefab != null 
            && stateMachine.SkillCooldownTimer <= 0f && !stateMachine.Stats.isRangedEnemy)
        {
            Vector3 dirToTarget = (stateMachine.PlayerTarget.position - stateMachine.transform.position).normalized;
            dirToTarget.y = 0f;
            float angleToTarget = Vector3.Angle(stateMachine.transform.forward, dirToTarget);

            float minRangedDist = stateMachine.Stats.attackRange + 2f;
            // Cho phép bắn xa tới 20m
            if (distToPlayer > minRangedDist && distToPlayer <= 20f && angleToTarget <= 45f)
            {
                stateMachine.SkillCooldownTimer = 6f;
                stateMachine.SwitchState(stateMachine.RangeAttackState);
                return;
            }
        }

        if (distToPlayer > stateMachine.Stats.outerRingRadius + 4f)
        {
            stateMachine.SwitchState(stateMachine.ChaseState);
            return;
        }

        maneuverTimer -= deltaTime;
        if (maneuverTimer > 0f) return;

        bool isInInner = EnemySlotManager.Instance != null && reservedSlot >= 0 ? EnemySlotManager.Instance.IsInnerSlot(reservedSlot) : isInnerRing;

        if (isInInner)
        {
            Vector3 dirToPlayer = (stateMachine.PlayerTarget.position - stateMachine.transform.position).normalized;
            dirToPlayer.y = 0f;
            float angleToPlayer = Vector3.Angle(stateMachine.transform.forward, dirToPlayer);

            bool hasToken = stateMachine.Stats.isMiniBoss || 
                           (AIDirector.Instance != null && AIDirector.Instance.RequestAttackToken(stateMachine));

            // 🟢 MINI-BOSS: Dù bỏ qua Token, vẫn phải chờ Cooldown giữa các combo
            // Tạo nhịp chiến đấu God of War: Chém -> Vờn -> Chém, không phải máy bay trực thăng
            if (stateMachine.Stats.isMiniBoss && stateMachine.SkillCooldownTimer > 0f)
            {
                hasToken = false;
            }

            if (hasToken)
            {
                if (distToPlayer <= stateMachine.Stats.attackRange + 0.2f)
                {
                    if (angleToPlayer > 60f)
                    {
                        if (AIDirector.Instance != null) AIDirector.Instance.ReleaseToken(stateMachine);
                        return; 
                    }

                    if (stateMachine.Stats.isRangedEnemy) 
                    {
                        // 🟢 FIX CHÍNH: Nếu có Token nhưng chiêu nổ đang hồi và Kratos ở quá sát -> Trả Token lại, đứng im!
                        if (distToPlayer <= 3.5f && stateMachine.MeleeBurstCooldownTimer > 0f)
                        {
                            if (AIDirector.Instance != null) AIDirector.Instance.ReleaseToken(stateMachine);
                            return; 
                        }
                        
                        stateMachine.SwitchState(stateMachine.RangeAttackState);
                    }
                    else 
                    {
                        // 🟢 Boss cận chiến: Set cooldown sau khi chém để tạo nhịp chiến đấu
                        if (stateMachine.Stats.isMiniBoss)
                        {
                            // Phase 2: Nhịp tấn công dồn dập hơn (chỉ nghỉ 1-1.5s thay vì 1.5-3s)
                            if (stateMachine.IsPhase2) stateMachine.SkillCooldownTimer = Random.Range(1.0f, 1.5f);
                            else stateMachine.SkillCooldownTimer = Random.Range(1.5f, 3f);
                        }
                        stateMachine.AttackState.Setup(-1);
                        stateMachine.SwitchState(stateMachine.AttackState);
                    }
                }
                else
                {
                    stateMachine.SwitchState(stateMachine.ChaseState);
                }
                return;
            }
        }

        if (AIDirector.Instance != null && !AIDirector.Instance.IsEnemyOnScreen(stateMachine.transform.position))
        {
            Vector3 pushDir = AIDirector.Instance.GetOnScreenDirection(stateMachine.transform.position);
            if (pushDir.sqrMagnitude > 0.01f)
            {
                float dot = Vector3.Dot(Vector3.Cross((stateMachine.PlayerTarget.position - stateMachine.transform.position).normalized, Vector3.up), pushDir);
                strafeDirection = dot > 0 ? 1 : -1;
            }
            maneuverTimer = Random.Range(1.5f, 3f);
            return;
        }

        stateMachine.SwitchState(stateMachine.IdleState);
    }
}