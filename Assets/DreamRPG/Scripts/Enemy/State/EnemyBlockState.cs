using UnityEngine;

public class EnemyBlockState : EnemyState
{
    private float blockTimer;

    public EnemyBlockState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        stateMachine.ManualVelocity = Vector3.zero;
        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
            stateMachine.Agent.isStopped = true;

        blockTimer = Random.Range(1.5f, 2.5f);
        if (stateMachine.Health != null)
        {
            stateMachine.Health.isBlocking = true;
            stateMachine.Health.RefillGuard(); // 🟢 Khôi phục 100% khiên trước khi đỡ!
        }

        stateMachine.Anim.SetBool(EnemyConstants.HashIsBlocking, true);
        stateMachine.Anim.SetTrigger(EnemyConstants.HashBlockStart);
    }

    public override void Tick(float deltaTime)
    {
        if (stateMachine.PlayerTarget == null) return;

        blockTimer -= deltaTime;
        Vector3 dirToPlayer = (stateMachine.PlayerTarget.position - stateMachine.transform.position);
        dirToPlayer.y = 0f;
        if (dirToPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToPlayer.normalized);
            stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRot, deltaTime * 8f);
        }

        if (blockTimer <= 0f)
        {
            // 🟢 Thay đổi 3: Phạt cooldown sau khi Đỡ đòn xong để Player có thời gian chuyển mục tiêu
            stateMachine.SkillCooldownTimer = Random.Range(1.5f, 2.5f);
            stateMachine.SwitchState(stateMachine.StrafeState);
        }
    }

    public void OnImpact()
    {
        stateMachine.Anim.SetTrigger(EnemyConstants.HashBlockImpact);
        blockTimer -= 0.5f; 
    }

    public override void Exit()
    {
        stateMachine.ManualVelocity = Vector3.zero;
        if (stateMachine.Health != null) stateMachine.Health.isBlocking = false;
        stateMachine.Anim.SetBool(EnemyConstants.HashIsBlocking, false);
    }
}