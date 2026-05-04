using UnityEngine;

/// <summary>
/// EnemyIdleState — Đứng im, cảm biến xung quanh, chuyển Patrol hoặc Chase.
/// Nếu đã có aggro memory → skip cảm biến, chuyển thẳng Chase.
/// </summary>
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

        // AGGRO MEMORY: Nếu đã biết Player ở đâu → skip chờ, lập tức cảnh giác
        if (stateMachine.HasAggro)
        {
            isSuspicious = true;
            alertTimer   = stateMachine.Stats.alertDuration * 0.5f; // alert nhanh hơn bình thường
        }
    }

    public override void Tick(float deltaTime)
    {
        if (stateMachine.PlayerTarget == null) return;

        // ── NHÁNH 1: ĐÃ SINH NGHI ──────────────────────────────────
        if (isSuspicious)
        {
            alertTimer -= deltaTime;

            float timeInAlert = stateMachine.Stats.alertDuration - alertTimer;
            if (timeInAlert > 0.3f)
            {
                Vector3 lookTarget = stateMachine.HasAggro
                    ? stateMachine.LastKnownPlayerPos
                    : stateMachine.PlayerTarget.position;

                Vector3 dirToPlayer = (lookTarget - stateMachine.transform.position).normalized;
                dirToPlayer.y = 0;
                if (dirToPlayer.sqrMagnitude > 0.1f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dirToPlayer);
                    stateMachine.transform.rotation = Quaternion.Slerp(
                        stateMachine.transform.rotation, targetRot, deltaTime * 2f);
                }
            }

            if (alertTimer <= 0)
            {
                stateMachine.HasAggro = true;
                stateMachine.SwitchState(new EnemyChaseState(stateMachine));
            }
            return;
        }

        // ── NHÁNH 2: BÌNH THƯỜNG ──────────────────────────────────
        idleTimer     -= deltaTime;
        fidgetTimer   -= deltaTime;
        tickRateTimer -= deltaTime;

        // Fidget animation
        if (fidgetTimer <= 0)
        {
            // stateMachine.Anim.SetTrigger("Fidget");
            fidgetTimer = Random.Range(3f, 6f);
        }

        // Chuyển Patrol
        if (idleTimer <= 0)
        {
            stateMachine.SwitchState(new EnemyPatrolState(stateMachine));
            return;
        }

        // Cảm biến (throttled)
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

        // A. Thính giác
        if (dist <= stateMachine.Stats.hearingRange && stateMachine.IsPlayerMoving)
        {
            TriggerSuspicion();
            return;
        }

        // B. Thị giác (cone + raycast)
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
                    if (hit.transform.CompareTag("Player"))
                        TriggerSuspicion();
                }
            }
        }
    }

    private void TriggerSuspicion()
    {
        isSuspicious = true;
        alertTimer   = stateMachine.Stats.alertDuration;
        // stateMachine.Anim.SetBool("IsAlert", true);
    }

    public override void Exit()
    {
        // stateMachine.Anim.SetBool("IsAlert", false);
    }
}