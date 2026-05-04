using UnityEngine;

/// <summary>
/// EnemyGapCloserState — Lao tới Player khi ở xa rồi chém mạnh (GoW Lunge Attack).
///
/// Kích hoạt: ChaseState khi distanceToPlayer >= gapCloserRange.
///
/// Logic:
///   • Lao thẳng đến Player bằng ManualVelocity (nhanh hơn moveSpeed)
///   • Khi đủ gần → LUÔN chuyển sang AttackState chém ngay
///   • Timeout safety: nếu lao quá lâu mà chưa tới → về Strafe
/// </summary>
public class EnemyGapCloserState : EnemyState
{
    private float timeoutTimer;
    private const float MAX_GAP_CLOSE_TIME = 2.5f;

    public EnemyGapCloserState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        timeoutTimer = MAX_GAP_CLOSE_TIME;

        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
            stateMachine.Agent.isStopped = true;

        // Kích hoạt animation chạy/lao tới ngay lập tức
        stateMachine.Anim.SetTrigger("GapCloser");
        stateMachine.Anim.SetBool("IsGapClosing", true);
    }

    public override void Tick(float deltaTime)
    {
        if (stateMachine.PlayerTarget == null) return;

        timeoutTimer -= deltaTime;

        // Hướng tới Player và luôn quay mặt theo dõi
        Vector3 dirToPlayer = (stateMachine.PlayerTarget.position - stateMachine.transform.position);
        dirToPlayer.y = 0f;
        float distance = dirToPlayer.magnitude - stateMachine.Controller.radius;

        if (dirToPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToPlayer.normalized);
            stateMachine.transform.rotation = Quaternion.Slerp(
                stateMachine.transform.rotation, targetRot, deltaTime * 12f);
        }

        // Wall-check: Raycast phía trước để tránh đâm tường (Chỉ quét Environment)
        Vector3 rayStart = stateMachine.transform.position + Vector3.up;
        int envMask = LayerMask.GetMask("Environment");
        bool wallAhead = Physics.Raycast(rayStart, dirToPlayer.normalized, 2f, envMask);

        if (wallAhead)
        {
            // Có tường → chuyển sang Agent pathfinding thay vì lao thẳng
            stateMachine.ManualVelocity = Vector3.zero;
            stateMachine.EnableAgentMode();
            if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
            {
                stateMachine.Agent.isStopped = false;
                stateMachine.Agent.speed = stateMachine.Stats.gapCloserSpeed;
                stateMachine.Agent.SetDestination(stateMachine.PlayerTarget.position);
            }
        }
        else
        {
            // Đường trống → lao thẳng (nhanh hơn)
            if (stateMachine.Agent.isActiveAndEnabled)
                stateMachine.Agent.isStopped = true;
            stateMachine.ManualVelocity = dirToPlayer.normalized * stateMachine.Stats.gapCloserSpeed;
        }

        // --- ĐẾN GẦN → CHÉM NGAY ---
        if (distance <= stateMachine.Stats.attackRange + 0.8f) 
        {
            // Cố xin Token trước
            bool hasToken = AIDirector.Instance != null
                && AIDirector.Instance.RequestAttackToken(stateMachine);

            if (hasToken)
            {
                // Có Token → chém đòn mạnh nhất ngay lập tức
                stateMachine.SwitchState(new EnemyAttackState(stateMachine, 0)); 
            }
            else
            {
                // Không có Token → vẫn chém luôn (GapCloser là đòn đặc biệt, không cần xếp hàng)
                stateMachine.SwitchState(new EnemyAttackState(stateMachine, 0));
            }
            return;
        }

        // Timeout safety
        if (timeoutTimer <= 0f)
        {
            stateMachine.SwitchState(new EnemyStrafeState(stateMachine));
        }
    }

    public override void Exit()
    {
        stateMachine.Anim.SetBool("IsGapClosing", false);
        stateMachine.ManualVelocity = Vector3.zero;
    }
}
