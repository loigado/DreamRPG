using UnityEngine;

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

        stateMachine.Anim.SetTrigger(EnemyConstants.HashGapCloser);
        stateMachine.Anim.SetBool(EnemyConstants.HashIsGapClosing, true);

        // 🟢 NÂNG CẤP BOSS: Sử dụng kỹ năng Tàn Ảnh của sát thủ hệ lôi để lướt sượt tới Player
        if (stateMachine.Stats.isMiniBoss)
        {
            var afterimage = stateMachine.GetComponent<AfterimageController>();
            if (afterimage != null) afterimage.StartTrail(stateMachine.Stats.enemyElement);
            stateMachine.Anim.speed = 1.3f; // Tăng tốc độ lướt
        }
    }

    public override void Tick(float deltaTime)
    {
        if (stateMachine.PlayerTarget == null) return;

        timeoutTimer -= deltaTime;
        Vector3 dirToPlayer = (stateMachine.PlayerTarget.position - stateMachine.transform.position);
        dirToPlayer.y = 0f;
        float distance = dirToPlayer.magnitude - stateMachine.Controller.radius;

        if (dirToPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToPlayer.normalized);
            stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRot, deltaTime * 12f);
        }

        Vector3 rayStart = stateMachine.transform.position + Vector3.up;
        bool wallAhead = Physics.Raycast(rayStart, dirToPlayer.normalized, 2f, EnemyConstants.EnvLayerMask);

        if (wallAhead)
        {
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
            if (stateMachine.Agent.isActiveAndEnabled) stateMachine.Agent.isStopped = true;
            float speed = stateMachine.Stats.gapCloserSpeed;
            stateMachine.ManualVelocity = dirToPlayer.normalized * speed;
        }

        if (distance <= stateMachine.Stats.attackRange + 0.8f) 
        {
            // Bất kể có token hay không, gap closer luôn chém
            stateMachine.AttackState.Setup(0);
            stateMachine.SwitchState(stateMachine.AttackState);
            return;
        }

        if (timeoutTimer <= 0f)
        {
                stateMachine.SwitchState(stateMachine.StrafeState);
        }
    }

    public override void Exit()
    {
        stateMachine.Anim.SetBool(EnemyConstants.HashIsGapClosing, false);
        stateMachine.ManualVelocity = Vector3.zero;

        // 🟢 Tắt tàn ảnh và trả lại tốc độ bình thường cho Boss
        if (stateMachine.Stats.isMiniBoss)
        {
            var afterimage = stateMachine.GetComponent<AfterimageController>();
            if (afterimage != null) afterimage.StopTrail();
            stateMachine.Anim.speed = 1f;
        }
    }
}