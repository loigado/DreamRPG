using UnityEngine;
using UnityEngine.AI;

public class EnemyChaseState : EnemyState
{
    private float pathUpdateTimer;
    private float gapCloserCooldownTimer;

    public EnemyChaseState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        stateMachine.EnableAgentMode();
        pathUpdateTimer = 0f;
        gapCloserCooldownTimer = Random.Range(1f, 3f);
        stateMachine.HasAggro = true;

        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
        {
            stateMachine.Agent.isStopped = false;
            // 🟢 PHASE 2: Tăng tốc độ chạy/truy đuổi lên 50%
            float speedMult = (stateMachine.Stats.isMiniBoss && stateMachine.IsPhase2) ? 1.5f : 1f;
            stateMachine.Agent.speed = stateMachine.Stats.moveSpeed * speedMult;
        }
    }

    public override void Tick(float deltaTime)
    {
        if (stateMachine.PlayerTarget == null) return;

        float distanceToPlayer = Vector3.Distance(stateMachine.transform.position, stateMachine.PlayerTarget.position) - stateMachine.Controller.radius;
        stateMachine.LastKnownPlayerPos = stateMachine.PlayerTarget.position;

        // ── 1. KIỂM TRA AGGRO ─────────────────────────
        if (!stateMachine.HasAggro || distanceToPlayer >= stateMachine.Stats.dropAggroRange * 1.5f)
        {
            stateMachine.HasAggro = false;
            stateMachine.SwitchState(stateMachine.IdleState);
            return;
        }

        Vector3 dirToPlayer = (stateMachine.PlayerTarget.position - stateMachine.transform.position).normalized;
        dirToPlayer.y = 0f;
        float angleToPlayer = Vector3.Angle(stateMachine.transform.forward, dirToPlayer);

        // Blindspot Punishment đã chuyển sang Radar ngầm (EnemyStateMachine.Update)
        // Tanker sẽ không vào ChaseState — đã redirect sang IntimidationState

        // ── 2B. MINI-BOSS PHÓNG SÓNG SÉT TẦM XA (CHASE) ─────────
        // Cập nhật: Dùng rangedProjectilePrefab (vì đã đổi sang gọi sấm sét)
        if (stateMachine.Stats.isMiniBoss && stateMachine.Stats.rangedProjectilePrefab != null 
            && stateMachine.SkillCooldownTimer <= 0f && !stateMachine.Stats.isRangedEnemy)
        {
            float minRangedDist = stateMachine.Stats.attackRange + 2f; // Vùng an toàn cận chiến
            // Cho phép bắn xa tới 20m để chống lại cung thủ
            if (distanceToPlayer > minRangedDist && distanceToPlayer <= 20f && angleToPlayer <= 45f)
            {
                stateMachine.SkillCooldownTimer = 6f; // Cooldown 6 giây để Boss không spam
                stateMachine.SwitchState(stateMachine.RangeAttackState);
                return;
            }
        }

        // ── 3. ĐÁNH CẬN CHIẾN BÌNH THƯỜNG ─────────────────────────
        if (distanceToPlayer <= stateMachine.Stats.attackRange)
        {
            // 🟢 KHÓA GÓC ĐÁNH TOÀN DIỆN CHO TẤT CẢ QUÁI
            if (angleToPlayer > 60f)
            {
                // Chưa quay mặt tới nơi -> Cấm đánh, ép về Strafe để vặn sườn tiếp!
                stateMachine.SwitchState(stateMachine.StrafeState);
                return;
            }

            // 🟢 MINI-BOSS: Phải chờ cooldown giữa các combo, không chém liên tục
            if (stateMachine.Stats.isMiniBoss && stateMachine.SkillCooldownTimer > 0f)
            {
                stateMachine.SwitchState(stateMachine.StrafeState);
                return;
            }

            bool hasToken = AIDirector.Instance != null && AIDirector.Instance.RequestAttackToken(stateMachine);
            // Mini-Boss bỏ qua Token system
            if (stateMachine.Stats.isMiniBoss) hasToken = true;

            if (hasToken)
            {
                if (stateMachine.Stats.isRangedEnemy)
                    stateMachine.SwitchState(stateMachine.RangeAttackState);
                else
                {
                    // 🟢 Boss cận chiến: Set cooldown sau khi chém để tạo nhịp God of War
                    if (stateMachine.Stats.isMiniBoss)
                    {
                        // Phase 2: Nhịp tấn công dồn dập hơn (chỉ nghỉ 1-1.5s thay vì 1.5-3s)
                        if (stateMachine.IsPhase2) stateMachine.SkillCooldownTimer = Random.Range(1.0f, 1.5f);
                        else stateMachine.SkillCooldownTimer = Random.Range(1.5f, 3f);
                    }
                    stateMachine.AttackState.Setup(-1);
                    stateMachine.SwitchState(stateMachine.AttackState);
                }
            }
            else
            {
                stateMachine.SwitchState(stateMachine.StrafeState);
            }
            return;
        }

        // ── 4. GAP CLOSER (CHỈ DÀNH CHO LÍNH THƯỜNG) ─────
        gapCloserCooldownTimer -= deltaTime;
        if (distanceToPlayer >= stateMachine.Stats.gapCloserRange && gapCloserCooldownTimer <= 0f)
        {
            if (AIDirector.Instance != null && AIDirector.Instance.IsEnemyOnScreen(stateMachine.transform.position))
            {
                stateMachine.SwitchState(stateMachine.GapCloserState);
                return;
            }
        }

        // ── 5. NAVIGATION (CHỈ CẬP NHẬT ĐIỂM ĐẾN, CẤM XOAY CHIÊU) ───
        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
        {
            pathUpdateTimer -= deltaTime;
            if (pathUpdateTimer <= 0)
            {
                stateMachine.Agent.SetDestination(stateMachine.PlayerTarget.position);
                pathUpdateTimer = 0.2f;
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