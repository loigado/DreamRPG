using UnityEngine;

/// <summary>
/// EnemyAttackState — Pha tấn công 3 giai đoạn: Telegraph → Active → Recovery.
///
/// Cải tiến AAA:
///   • Attack Variety — random 1 trong N combo (dùng animIndex)
///   • Proper Token release (cả khi bị gián đoạn)
///   • Track Player trong pha Telegraph (GoW standard)
/// </summary>
public class EnemyAttackState : EnemyState
{
    private enum AttackPhase { Telegraph, Active, Recovery }
    private AttackPhase currentPhase;
    private float phaseTimer;
    private int   attackVariant;   // 0, 1, 2 — random combo
    private bool  tokenReleased;
    private bool  hasDealtDamage;
    private int   forcedVariant;

    public EnemyAttackState(EnemyStateMachine stateMachine, int forcedVariant = -1) : base(stateMachine) 
    {
        this.forcedVariant = forcedVariant;
    }

    public override void Enter()
    {
        stateMachine.ManualVelocity = Vector3.zero;
        tokenReleased = false;
        hasDealtDamage = false;

        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
            stateMachine.Agent.isStopped = true;

        // Chọn đòn chém: nếu có đòn chỉ định thì xài, không thì random
        if (forcedVariant != -1)
            attackVariant = forcedVariant;
        else
            attackVariant = Random.Range(0, stateMachine.Stats.attackVariants);

        // Pha 1: TELEGRAPH (wind-up)
        currentPhase = AttackPhase.Telegraph;
        // 🟢 FIX: Đòn ép buộc (sau GapCloser) chém gần như ngay lập tức, không cần lấy đà lâu
        phaseTimer = (forcedVariant != -1) ? 0.1f : (0.5f + attackVariant * 0.15f);

        stateMachine.Anim.SetInteger("AttackVariant", attackVariant);
        stateMachine.Anim.SetTrigger("Attack");
    }

    public override void Tick(float deltaTime)
    {
        if (stateMachine.PlayerTarget == null) return;
        phaseTimer -= deltaTime;

        switch (currentPhase)
        {
            // ── TELEGRAPH: vung tay lấy đà, track Player ──────────────
            case AttackPhase.Telegraph:
                TrackPlayer(deltaTime * 10f);
                Debug.DrawRay(stateMachine.transform.position + Vector3.up,
                    stateMachine.transform.forward * 2f, Color.yellow);

                // Lunge (Trượt tới): Nếu đang đứng ở slot xa (3m) mà tầm đánh ngắn (1.5m)
                // Quái sẽ tự động lướt lên phía trước trong lúc giơ vũ khí lên (Attack Magnetism)
                float distToPlayer = Vector3.Distance(stateMachine.transform.position, stateMachine.PlayerTarget.position) 
                                     - stateMachine.Controller.radius;
                if (distToPlayer > stateMachine.Stats.attackRange)
                {
                    stateMachine.ManualVelocity = stateMachine.transform.forward * (stateMachine.Stats.moveSpeed * 1.2f);
                }
                else
                {
                    stateMachine.ManualVelocity = Vector3.zero;
                }

                if (phaseTimer <= 0)
                {
                    currentPhase = AttackPhase.Active;
                    phaseTimer   = 0.2f + attackVariant * 0.1f;
                }
                break;

            // ── ACTIVE: hitbox active, hướng khóa ──────────────────────
            case AttackPhase.Active:
                stateMachine.ManualVelocity = Vector3.zero; // Dừng lướt, đứng chém cứng lại

                if (!hasDealtDamage)
                {
                    hasDealtDamage = true;
                    
                    // Gây sát thương bằng OverlapBox phía trước mặt quái
                    Vector3 boxCenter = stateMachine.transform.position + Vector3.up * 1f + stateMachine.transform.forward * 1f;
                    Vector3 boxHalfExtents = new Vector3(1f, 1f, 1f); // Kích thước vùng chém (ngang, cao, sâu)
                    
                    Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, stateMachine.transform.rotation, LayerMask.GetMask("Player"));
                    foreach (var hit in hits)
                    {
                        // Kiểm tra IDamageable của Player
                        IDamageable playerHealth = hit.GetComponentInParent<IDamageable>();
                        if (playerHealth != null)
                        {
                            playerHealth.TakeDamage(stateMachine.Stats.attackDamage, stateMachine.transform.position);
                            
                            // Tạo hiệu ứng máu hoặc tia lửa ở đây nếu có
                        }
                    }
                }

                if (phaseTimer <= 0)
                {
                    currentPhase = AttackPhase.Recovery;
                    phaseTimer   = 1.0f + attackVariant * 0.3f; // variant nặng = recovery lâu hơn
                }
                break;

            // ── RECOVERY: sơ hở, Player có thể trừng phạt ─────────────
            case AttackPhase.Recovery:
                if (phaseTimer <= 0)
                {
                    SafeReleaseToken();
                    stateMachine.SwitchState(new EnemyStrafeState(stateMachine));
                }
                break;
        }
    }

    private void TrackPlayer(float turnSpeed)
    {
        Vector3 dir = (stateMachine.PlayerTarget.position - stateMachine.transform.position).normalized;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            stateMachine.transform.rotation = Quaternion.Slerp(
                stateMachine.transform.rotation, targetRot, turnSpeed);
        }
    }

    private void SafeReleaseToken()
    {
        if (tokenReleased) return;
        tokenReleased = true;
        if (AIDirector.Instance != null) AIDirector.Instance.ReleaseToken(stateMachine);
    }

    public override void Exit()
    {
        // Luôn release token khi bị ép thoát (bị đánh, bị choáng...)
        SafeReleaseToken();
    }
}