using UnityEngine;

public class EnemyHitReactionState : EnemyState
{
    private Vector3 attackerPos;
    private bool isHeavy;
    private float staggerTimer;
    private Vector3 knockbackVelocity;

    public EnemyHitReactionState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public void Setup(Vector3 attackerPos, bool isHeavy)
    {
        this.attackerPos = attackerPos;
        this.isHeavy = isHeavy;
    }

    public override void Enter()
    {
        var afterimage = stateMachine.GetComponent<AfterimageController>();
        if (afterimage != null) afterimage.StopTrail();
        stateMachine.Anim.speed = 1f;
        stateMachine.Anim.updateMode = AnimatorUpdateMode.Normal;
        stateMachine.ManualVelocity = Vector3.zero;

        // GIỮ NGUYÊN NavMeshAgent, chỉ bắt nó dừng lại. Tắt đi sẽ gây lỗi văng lên trời!
        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
        {
            stateMachine.Agent.ResetPath();
            stateMachine.Agent.isStopped = true;
        }

        staggerTimer = isHeavy ? stateMachine.Stats.heavyStaggerDuration : stateMachine.Stats.lightStaggerDuration;
        
        // 🟢 GIẢM MẠNH LỰC ĐẨY LÙI: Tránh trượt dốc bay lên trời
        float knockStrength = isHeavy ? 1.5f : 0.5f; 
        Vector3 knockDir = (stateMachine.transform.position - attackerPos).normalized;
        knockDir.y = 0;
        knockbackVelocity = knockDir * knockStrength;

        stateMachine.Anim.ResetTrigger(EnemyConstants.HashAttack);
        stateMachine.Anim.SetTrigger(isHeavy ? EnemyConstants.HashHeavyHit : EnemyConstants.HashLightHit);

        // 🟢 Vượt rào Slow-motion của Kratos
        if (Time.timeScale < 1f)
        {
            stateMachine.Anim.Update(0.1f);
        }
    }

    public override void Tick(float deltaTime)
    {
        staggerTimer -= deltaTime;
        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, deltaTime * 5f);
        stateMachine.ManualVelocity = knockbackVelocity;

        if (staggerTimer <= 0f)
        {
            stateMachine.HasAggro = true;
            stateMachine.SkillCooldownTimer = Random.Range(1.5f, 2.5f); 
            stateMachine.SwitchState(stateMachine.StrafeState);
        }
    }

    public override void Exit()
    {
        stateMachine.ManualVelocity = Vector3.zero;
        
        // Chỉ việc cho phép Agent tiếp tục di chuyển, KHÔNG DỊCH CHUYỂN
        if (stateMachine.Agent != null && stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
        {
            stateMachine.Agent.isStopped = false;
        }
    }
}