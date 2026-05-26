using UnityEngine;

public class EnemyIdleState : EnemyState
{
    private float idleTimer;
    private float tickRateTimer;
    private float fidgetTimer;

    private bool isSuspicious = false;
    private float alertTimer;

    public EnemyIdleState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
        {
            stateMachine.Agent.isStopped = true;
            stateMachine.Agent.velocity  = Vector3.zero;
        }

        isSuspicious = false;
        idleTimer    = Random.Range(stateMachine.Stats.idleTimeMin, stateMachine.Stats.idleTimeMax);
        fidgetTimer  = Random.Range(2f, 4f);
        tickRateTimer = 0f;
        stateMachine.ManualVelocity = Vector3.zero;

        if (stateMachine.HasAggro)
        {
            isSuspicious = true;
            alertTimer   = stateMachine.Stats.alertDuration * 0.5f;
        }
    }

    public override void Tick(float deltaTime)
    {
        if (stateMachine.PlayerTarget == null) return;

        if (isSuspicious)
        {
            alertTimer -= deltaTime;
            float timeInAlert = stateMachine.Stats.alertDuration - alertTimer;
            if (timeInAlert > 0.3f)
            {
                Vector3 lookTarget = stateMachine.HasAggro ? stateMachine.LastKnownPlayerPos : stateMachine.PlayerTarget.position;
                stateMachine.FaceTarget(lookTarget, 2f);

                Vector3 dirToPlayer = (lookTarget - stateMachine.transform.position).normalized;
                dirToPlayer.y = 0;
                if (dirToPlayer.sqrMagnitude > 0.1f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dirToPlayer);
                    stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRot, deltaTime * 2f);
                }
            }

            if (alertTimer <= 0)
            {
                stateMachine.HasAggro = true;
                stateMachine.SwitchState(stateMachine.ChaseState);
            }
            return;
        }

        idleTimer     -= deltaTime;
        fidgetTimer   -= deltaTime;
        tickRateTimer -= deltaTime;

        if (fidgetTimer <= 0)
        {
            fidgetTimer = Random.Range(3f, 6f);
        }

        if (idleTimer <= 0)
        {
            stateMachine.SwitchState(stateMachine.PatrolState);
            return;
        }

        if (tickRateTimer <= 0)
        {
            CheckPerception();
            tickRateTimer = 0.2f;
        }
    }

    private void CheckPerception()
    {
        Vector3 playerPos = stateMachine.PlayerTarget.position;
        Vector3 enemyPos  = stateMachine.transform.position;
        float dist = Vector3.Distance(enemyPos, playerPos);

        if (dist <= stateMachine.Stats.hearingRange && stateMachine.IsPlayerMoving)
        {
            TriggerSuspicion();
            return;
        }

        if (dist <= stateMachine.Stats.visionRange)
        {
            Vector3 dir   = (playerPos - enemyPos).normalized;
            float angle   = Vector3.Angle(stateMachine.transform.forward, dir);

            if (angle <= stateMachine.Stats.visionAngle / 2f)
            {
                Vector3 rayStart = enemyPos + Vector3.up * 1.5f;
                Vector3 rayDir   = ((playerPos + Vector3.up * 1.5f) - rayStart).normalized;

                if (Physics.Raycast(rayStart, rayDir, out RaycastHit hit, stateMachine.Stats.visionRange))
                {
                    if (hit.transform.CompareTag("Player")) TriggerSuspicion();
                }
            }
        }
    }

    private void TriggerSuspicion()
    {
        isSuspicious = true;
        alertTimer   = stateMachine.Stats.alertDuration;
    }

    public override void Exit() { }
}