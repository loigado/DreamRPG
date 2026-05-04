using UnityEngine;

/// <summary>
/// EnemyHitReactionState — Quái bị choáng khi ăn đòn (GoW essential).
///
/// Logic:
///   • Light hit → choáng ngắn (0.4s), hơi dật lùi
///   • Heavy hit → choáng dài (1.0s), dật lùi mạnh
///   • Sau khi hết choáng → về StrafeState (đã biết Player ở đâu)
///   • Nếu chết trong lúc choáng → EnemyHealth.OnDeath sẽ tự chuyển sang DeathState
/// </summary>
public class EnemyHitReactionState : EnemyState
{
    private Vector3 attackerPos;
    private bool    isHeavy;
    private float   staggerTimer;
    private Vector3 knockbackVelocity;

    public EnemyHitReactionState(EnemyStateMachine stateMachine, Vector3 attackerPos, bool isHeavy)
        : base(stateMachine)
    {
        this.attackerPos = attackerPos;
        this.isHeavy     = isHeavy;
    }

    public override void Enter()
    {
        // Dừng mọi di chuyển
        stateMachine.ManualVelocity = Vector3.zero;
        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
            stateMachine.Agent.isStopped = true;

        // Tính thời gian choáng
        staggerTimer = isHeavy
            ? stateMachine.Stats.heavyStaggerDuration
            : stateMachine.Stats.lightStaggerDuration;

        // Tính knockback: lùi ra xa attacker
        Vector3 knockDir = (stateMachine.transform.position - attackerPos).normalized;
        knockDir.y = 0f;
        float knockStrength = isHeavy ? 4f : 1.5f;
        knockbackVelocity = knockDir * knockStrength;

        // Trigger animation
        stateMachine.Anim.SetTrigger(isHeavy ? "HeavyHit" : "LightHit");
    }

    public override void Tick(float deltaTime)
    {
        staggerTimer -= deltaTime;

        // Knockback giảm dần (damping)
        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, deltaTime * 5f);
        stateMachine.ManualVelocity = knockbackVelocity;

        // Hết choáng → về StrafeState (vẫn nhớ Player)
        if (staggerTimer <= 0f)
        {
            stateMachine.HasAggro = true;
            stateMachine.SwitchState(new EnemyStrafeState(stateMachine));
        }
    }

    public override void Exit()
    {
        stateMachine.ManualVelocity = Vector3.zero;
    }
}
