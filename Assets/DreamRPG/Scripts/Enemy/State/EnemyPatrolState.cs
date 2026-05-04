using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// EnemyPatrolState — Tuần tra quanh SpawnPosition.
/// Cải tiến: Aggro Memory check + tất cả Agent call đều có guard.
/// </summary>
public class EnemyPatrolState : EnemyState
{
    private bool  hasDestination;
    private float tickRateTimer;
    private float headLookTimer;
    private Vector3 currentLookTarget;

    public EnemyPatrolState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        stateMachine.EnableAgentMode();

        hasDestination = false;
        tickRateTimer  = 0f;
        headLookTimer  = Random.Range(2f, 5f);

        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
        {
            stateMachine.Agent.isStopped = false;
            stateMachine.Agent.speed = stateMachine.Stats.moveSpeed * 0.4f;
        }

        // Aggro Memory: nếu vẫn nhớ Player → đi thẳng về Chase
        if (stateMachine.HasAggro)
        {
            stateMachine.SwitchState(new EnemyChaseState(stateMachine));
            return;
        }
    }

    public override void Tick(float deltaTime)
    {
        if (stateMachine.PlayerTarget == null) return;

        // ── 1. CẢM BIẾN (throttled) ──────────────────────────────
        tickRateTimer -= deltaTime;
        if (tickRateTimer <= 0)
        {
            // Tethering
            float distFromAnchor = Vector3.Distance(stateMachine.transform.position, stateMachine.SpawnPosition);
            if (distFromAnchor > stateMachine.Stats.patrolRadius + 2f)
            {
                if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
                {
                    stateMachine.Agent.SetDestination(stateMachine.SpawnPosition);
                    hasDestination = true;
                }
            }
            else
            {
                if (CheckPerception())
                {
                    stateMachine.SwitchState(new EnemyIdleState(stateMachine));
                    return;
                }
            }
            tickRateTimer = 0.2f;
        }

        // ── 2. NAVIGATION ──────────────────────────────────────────
        if (!hasDestination)
        {
            FindRandomPatrolPoint();
        }
        else
        {
            if (stateMachine.Agent.isActiveAndEnabled
                && stateMachine.Agent.isOnNavMesh
                && !stateMachine.Agent.pathPending
                && stateMachine.Agent.remainingDistance <= 1.5f)
            {
                stateMachine.SwitchState(new EnemyIdleState(stateMachine));
                return;
            }
        }

        // ── 3. HEAD TRACKING ──────────────────────────────────────
        headLookTimer -= deltaTime;
        if (headLookTimer <= 0)
        {
            Vector3 randomOffset = new Vector3(Random.Range(-5f, 5f), Random.Range(0f, 3f), Random.Range(2f, 6f));
            currentLookTarget = stateMachine.transform.position + stateMachine.transform.TransformDirection(randomOffset);
            headLookTimer = Random.Range(3f, 6f);
        }
    }

    private void FindRandomPatrolPoint()
    {
        Vector3 randomDir = Random.insideUnitSphere * stateMachine.Stats.patrolRadius;
        randomDir += stateMachine.SpawnPosition;
        randomDir.y = stateMachine.SpawnPosition.y;

        if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
            {
                stateMachine.Agent.SetDestination(hit.position);
                hasDestination = true;
            }
        }
        else
        {
            // Nếu không tìm được điểm (do random trúng góc kẹt), đợi 0.5s rồi tìm lại
            hasDestination = false; 
        }
    }

    private bool CheckPerception()
    {
        float dist = Vector3.Distance(stateMachine.transform.position, stateMachine.PlayerTarget.position);

        // Thính giác
        if (dist <= stateMachine.Stats.hearingRange && stateMachine.IsPlayerMoving)
            return true;

        // Thị giác (cone + raycast)
        if (dist <= stateMachine.Stats.visionRange)
        {
            Vector3 dir = (stateMachine.PlayerTarget.position - stateMachine.transform.position).normalized;
            float angle = Vector3.Angle(stateMachine.transform.forward, dir);
            if (angle <= stateMachine.Stats.visionAngle / 2f)
            {
                // 🟢 FIX: Thêm Raycast kiểm tra tường chắn (Line Of Sight)
                // Tránh quái nhìn xuyên tường phát hiện Player ở phòng bên cạnh
                Vector3 rayStart = stateMachine.transform.position + Vector3.up * 1.5f;
                Vector3 rayDir = ((stateMachine.PlayerTarget.position + Vector3.up * 1.5f) - rayStart).normalized;
                if (Physics.Raycast(rayStart, rayDir, out RaycastHit hit, stateMachine.Stats.visionRange))
                {
                    if (hit.transform.CompareTag("Player"))
                        return true;
                }
            }
        }

        return false;
    }

    public override void Exit()
    {
        currentLookTarget = stateMachine.transform.position + stateMachine.transform.forward * 5f;
    }
}