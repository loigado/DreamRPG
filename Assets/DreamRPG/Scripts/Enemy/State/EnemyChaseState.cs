using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// EnemyChaseState — Rượt đuổi Player.
/// Fix: Token system thực sự hoạt động + Gap Closer transition.
/// </summary>
public class EnemyChaseState : EnemyState
{
    private float pathUpdateTimer;
    private float gapCloserCooldownTimer;

    public EnemyChaseState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        stateMachine.EnableAgentMode();
        pathUpdateTimer = 0f;
        
        // Cooldown phải được lấy từ thời gian thực (giả lập) hoặc ít nhất random để không trigger lập tức
        gapCloserCooldownTimer = Random.Range(1f, 3f);

        // Bật aggro
        stateMachine.HasAggro = true;

        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
        {
            stateMachine.Agent.isStopped = false;
            stateMachine.Agent.speed = stateMachine.Stats.moveSpeed;
        }
    }

    public override void Tick(float deltaTime)
    {
        if (stateMachine.PlayerTarget == null) return;

        float distanceToPlayer = Vector3.Distance(stateMachine.transform.position,
                                                   stateMachine.PlayerTarget.position)
                                 - stateMachine.Controller.radius;

        // Cập nhật vị trí đã biết
        stateMachine.LastKnownPlayerPos = stateMachine.PlayerTarget.position;

        // ── 1. MẤT AGGRO ────────────────────────────────────────────
        // Nếu quái bị tấn công, aggroMemoryTimer sẽ được bơm đầy (VD: 5s).
        // Trong 5s đó dù bạn có đứng xa 30m nó vẫn sẽ rượt tới cùng!
        // Nó chỉ bỏ cuộc khi stateMachine tự động đặt HasAggro = false (hết thời gian nhớ)
        if (!stateMachine.HasAggro || distanceToPlayer >= stateMachine.Stats.dropAggroRange * 1.5f)
        {
            stateMachine.HasAggro = false;
            stateMachine.SwitchState(new EnemyIdleState(stateMachine));
            return;
        }

        // ── 2. ĐỦ GẦN → STRAFE HOẶC ATTACK ─────────────────────────
        // Sửa lỗi đánh hụt: đợi vào thật sát (bằng đúng attackRange) mới tung đòn
        if (distanceToPlayer <= stateMachine.Stats.attackRange)
        {
            // Xin Token ngay khi chạy tới — "Running Attack" giống GoW
            bool hasToken = AIDirector.Instance != null
                && AIDirector.Instance.RequestAttackToken(stateMachine);

            if (hasToken)
            {
                stateMachine.SwitchState(new EnemyAttackState(stateMachine));
            }
            else
            {
                stateMachine.SwitchState(new EnemyStrafeState(stateMachine));
            }
            return;
        }

        // ── 3. GAP CLOSER ────────────────────────────────────────────
        gapCloserCooldownTimer -= deltaTime;
        if (distanceToPlayer >= stateMachine.Stats.gapCloserRange && gapCloserCooldownTimer <= 0f)
        {
            // Chỉ gap close khi on-screen (GoW rule)
            if (AIDirector.Instance != null &&
                AIDirector.Instance.IsEnemyOnScreen(stateMachine.transform.position))
            {
                stateMachine.SwitchState(new EnemyGapCloserState(stateMachine));
                return;
            }
        }

        // ── 4. NAVIGATION ────────────────────────────────────────────
        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
        {
            pathUpdateTimer -= deltaTime;
            if (pathUpdateTimer <= 0)
            {
                stateMachine.Agent.SetDestination(stateMachine.PlayerTarget.position);
                pathUpdateTimer = 0.2f;
            }

            // Angular Momentum
            Vector3 desiredDir = stateMachine.Agent.desiredVelocity;
            desiredDir.y = 0;

            if (desiredDir.sqrMagnitude > 0.1f)
            {
                float currentSpeed = stateMachine.Agent.velocity.magnitude;
                float momentumFactor = Mathf.Clamp(
                    1f - (currentSpeed / stateMachine.Stats.moveSpeed), 0.3f, 1f);

                Quaternion targetRot = Quaternion.LookRotation(desiredDir);
                stateMachine.transform.rotation = Quaternion.Slerp(
                    stateMachine.transform.rotation, targetRot,
                    stateMachine.Stats.turnSpeed * momentumFactor * deltaTime * 0.01f);
            }
        }
    }

    public override void Exit()
    {
        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
        {
            stateMachine.Agent.ResetPath();
            stateMachine.Agent.isStopped = true;
        }
    }
}