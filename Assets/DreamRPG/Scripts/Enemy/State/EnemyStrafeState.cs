using UnityEngine;

/// <summary>
/// EnemyStrafeState — Vờn quanh Player (AAA chuẩn).
///
/// Tích hợp đầy đủ:
///   1. Slot System — di chuyển đến vị trí Slot + Pacing offset
///   2. Separation — V_final = V_goal * yield + V_separation
///   3. Yielding — quái ưu tiên thấp giảm tốc nhường đường
///   4. Off-screen Push — ép quái ngoài Camera vào trước mặt
/// </summary>
public class EnemyStrafeState : EnemyState
{
    // --- Timers ---
    private float maneuverTimer;
    private float feintTimer;

    // --- Slot ---
    private int  reservedSlot = -1;
    private bool isInnerRing;

    // --- Strafe Direction (CACHED — không random mỗi frame) ---
    private int strafeDirection;

    // --- Pacing ---
    private float pacingTimer;
    private float targetPacingOffset;
    private float currentPacingOffset;

    // --- Yielding ---
    private float yieldTimer;
    private bool  isYielding;

    // --- Separation buffer (tái sử dụng) ---
    private readonly Collider[] yieldCheckBuffer = new Collider[15];

    public EnemyStrafeState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        // Timers
        maneuverTimer = Random.Range(stateMachine.Stats.strafeTimeMin, stateMachine.Stats.strafeTimeMax);
        feintTimer    = Random.Range(stateMachine.Stats.feintIntervalMin, stateMachine.Stats.feintIntervalMax);

        // Vòng trong/ngoài
        isInnerRing = Random.value > 0.4f;

        // Đăng ký Slot
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

        // Strafe direction: CACHE 1 lần duy nhất, không random mỗi frame
        Vector3 dirToEnemy = (stateMachine.transform.position - stateMachine.PlayerTarget.position).normalized;
        float angleToRight = Vector3.Angle(stateMachine.PlayerTarget.right, dirToEnemy);
        strafeDirection = angleToRight < 90f ? 1 : -1;

        // Pacing
        pacingTimer         = 0f;
        targetPacingOffset  = 0f;
        currentPacingOffset = 0f;
        isYielding          = false;
        yieldTimer          = 0f;

        // Dừng NavMesh Agent
        if (stateMachine.Agent.isActiveAndEnabled)
            stateMachine.Agent.isStopped = true;
    }

    public override void Tick(float deltaTime)
    {
        if (stateMachine.PlayerTarget == null) return;

        // Cập nhật aggro
        stateMachine.LastKnownPlayerPos = stateMachine.PlayerTarget.position;

        // 1. NHÌN VỀ PLAYER
        FacePlayer(deltaTime);

        // 2. FEINT
        feintTimer -= deltaTime;
        if (feintTimer <= 0)
        {
            // stateMachine.Anim.SetTrigger("Feint");
            feintTimer = Random.Range(stateMachine.Stats.feintIntervalMin, stateMachine.Stats.feintIntervalMax);
        }

        // 3. PACING
        UpdatePacing(deltaTime);

        // 4. V_final = V_goal * yield + V_separation
        ComputeAndSetManualVelocity(deltaTime);

        // 5. EXIT CONDITIONS
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

    // =========================================================
    // HELPERS
    // =========================================================

    private void FacePlayer(float deltaTime)
    {
        stateMachine.LookAtTarget = stateMachine.PlayerTarget.position + Vector3.up * 1.2f;

        Vector3 dirToPlayer = (stateMachine.PlayerTarget.position - stateMachine.transform.position);
        dirToPlayer.y = 0f;
        if (dirToPlayer.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dirToPlayer.normalized);
        stateMachine.transform.rotation = Quaternion.Slerp(
            stateMachine.transform.rotation, targetRot, deltaTime * 8f);
    }

    private void UpdatePacing(float deltaTime)
    {
        pacingTimer -= deltaTime;
        if (pacingTimer <= 0)
        {
            targetPacingOffset = Random.Range(-1.5f, 2f);
            pacingTimer        = Random.Range(1.5f, 3f);

            // Lâu lâu đổi hướng strafe
            if (Random.value > 0.7f) strafeDirection *= -1;
        }
        currentPacingOffset = Mathf.Lerp(currentPacingOffset, targetPacingOffset, deltaTime * 1.5f);
    }

    private void ComputeAndSetManualVelocity(float deltaTime)
    {
        float moveSpeed = stateMachine.Stats.moveSpeed * (isInnerRing ? 0.7f : 0.5f);

        // A. V_goal
        Vector3 goalVelocity = ComputeGoalVelocity(moveSpeed);

        // B. V_separation
        Vector3 separationVelocity = stateMachine.ComputeSeparationForce();

        // C. Yielding
        float speedMultiplier = CheckYieldingMultiplier(deltaTime);

        // D. Tổng hợp
        Vector3 finalVelocity = goalVelocity * speedMultiplier + separationVelocity;
        stateMachine.ManualVelocity = Vector3.ClampMagnitude(finalVelocity, stateMachine.Stats.moveSpeed);
    }

    private Vector3 ComputeGoalVelocity(float moveSpeed)
    {
        Vector3 dirToPlayer = (stateMachine.PlayerTarget.position - stateMachine.transform.position).normalized;
        dirToPlayer.y = 0f;

        // ── CÓ SLOT → đi đến Slot + Pacing offset ──────────────────
        if (reservedSlot >= 0 && EnemySlotManager.Instance != null)
        {
            Vector3 slotPos = EnemySlotManager.Instance.WorldSlotPosition(reservedSlot);

            // Pacing offset: dịch slot tiến/lùi theo hướng Player↔Slot
            Vector3 slotToPlayer = (stateMachine.PlayerTarget.position - slotPos).normalized;
            slotPos += slotToPlayer * currentPacingOffset;

            slotPos.y = stateMachine.transform.position.y;
            Vector3 toSlot   = slotPos - stateMachine.transform.position;
            toSlot.y = 0f;
            float distToSlot = toSlot.magnitude;

            if (distToSlot < stateMachine.Stats.slotArrivalTolerance)
            {
                // Đã đến slot → chỉ strafe nhẹ theo cached direction
                Vector3 strafeDir = Vector3.Cross(dirToPlayer, Vector3.up) * strafeDirection;
                return strafeDir * moveSpeed * 0.3f;
            }

            float speed = Mathf.Min(moveSpeed, distToSlot * 2f);
            return toSlot.normalized * speed;
        }

        // ── FALLBACK (không có SlotManager) ──────────────────────────
        float baseRadius    = isInnerRing ? stateMachine.Stats.innerRingRadius : stateMachine.Stats.outerRingRadius;
        float dynamicRadius = Mathf.Max(baseRadius + currentPacingOffset, 1.5f);
        float currentDist   = Vector3.Distance(stateMachine.transform.position,
                                               stateMachine.PlayerTarget.position)
                              - stateMachine.Controller.radius;

        Vector3 velocity = Vector3.zero;

        if (currentDist < dynamicRadius - 0.1f)
            velocity += -dirToPlayer * moveSpeed;
        else if (currentDist > dynamicRadius + 0.1f)
        {
            velocity += dirToPlayer * moveSpeed;
            if (currentDist > stateMachine.Stats.outerRingRadius + 4f)
            {
                stateMachine.SwitchState(new EnemyChaseState(stateMachine));
                return Vector3.zero;
            }
        }

        // Strafe ngang (cached direction, không random mỗi frame)
        Vector3 strafeDirFb = Vector3.Cross(dirToPlayer, Vector3.up) * strafeDirection;
        Vector3 rayStart    = stateMachine.transform.position + Vector3.up;
        if (Physics.Raycast(rayStart, strafeDirFb, 1.5f))
        {
            strafeDirection *= -1;
            strafeDirFb = -strafeDirFb;
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
            if (other == null || !(other.currentState is EnemyStrafeState)) continue;

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
        float distToPlayer = Vector3.Distance(stateMachine.transform.position,
                                              stateMachine.PlayerTarget.position)
                             - stateMachine.Controller.radius;

        if (distToPlayer > stateMachine.Stats.outerRingRadius + 4f)
        {
            stateMachine.SwitchState(new EnemyChaseState(stateMachine));
            return;
        }

        maneuverTimer -= deltaTime;
        if (maneuverTimer > 0f) return;

        bool isInInner = EnemySlotManager.Instance != null && reservedSlot >= 0
            ? EnemySlotManager.Instance.IsInnerSlot(reservedSlot)
            : isInnerRing;

        // Chỉ xin Token khi quái đang ở vòng trong (tránh giam Token vĩnh viễn)
        if (isInInner)
        {
            bool hasToken = AIDirector.Instance != null &&
                            AIDirector.Instance.RequestAttackToken(stateMachine);

            if (hasToken)
            {
                // Đã có Token: nếu đứng gần thì chém, nếu đứng xa thì rượt lại gần rồi mới chém
                if (distToPlayer <= stateMachine.Stats.attackRange + 0.2f)
                {
                    stateMachine.SwitchState(new EnemyAttackState(stateMachine));
                }
                else
                {
                    stateMachine.SwitchState(new EnemyChaseState(stateMachine));
                }
                return;
            }
        }

        // Off-screen push: đẩy quái vào trước Camera thay vì chỉ log
        if (AIDirector.Instance != null &&
            !AIDirector.Instance.IsEnemyOnScreen(stateMachine.transform.position))
        {
            Vector3 pushDir = AIDirector.Instance.GetOnScreenDirection(stateMachine.transform.position);
            if (pushDir.sqrMagnitude > 0.01f)
            {
                // Đổi strafeDirection sao cho quái sẽ strafe về phía Camera
                float dot = Vector3.Dot(Vector3.Cross(
                    (stateMachine.PlayerTarget.position - stateMachine.transform.position).normalized,
                    Vector3.up), pushDir);
                strafeDirection = dot > 0 ? 1 : -1;
            }

            // Cho thêm thời gian vờn để strafe vào màn hình
            maneuverTimer = Random.Range(1.5f, 3f);
            return;
        }

        stateMachine.SwitchState(new EnemyIdleState(stateMachine));
    }
}