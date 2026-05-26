using UnityEngine;

public class EnemyRangeAttackState : EnemyState
{
    private enum AttackPhase { Telegraph, Recovery } 
    private AttackPhase currentPhase;
    private float phaseTimer;
    private bool  tokenReleased;
    private bool  hasFired;

    private bool isMeleeBurst;
    private Vector3 lockedTargetPos;

    public EnemyRangeAttackState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        stateMachine.ManualVelocity = Vector3.zero;
        tokenReleased = false;
        hasFired = false;
        phaseTimer = 0f;

        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
            stateMachine.Agent.isStopped = true;

        float distToPlayer = Vector3.Distance(stateMachine.transform.position, stateMachine.PlayerTarget.position) - stateMachine.Controller.radius;
        
        // Sốc Điện cận chiến dành cho quái đánh xa, HOẶC dành cho Mini Boss đang tung đòn giải vây
        isMeleeBurst = (stateMachine.Stats.isRangedEnemy && distToPlayer <= 3.5f && stateMachine.MeleeBurstCooldownTimer <= 0f) || 
                       (stateMachine.Stats.isMiniBoss && stateMachine.MeleeBurstCooldownTimer > 0f && distToPlayer <= 5f);

        currentPhase = AttackPhase.Telegraph;

        if (isMeleeBurst)
        {
            stateMachine.MeleeBurstCooldownTimer = 8f; 
            
            if (stateMachine.Stats.isMiniBoss)
            {
                // Boss dùng đòn đánh thường nhưng giật sét
                stateMachine.Anim.SetInteger(EnemyConstants.HashAttackVariant, 3);
                stateMachine.Anim.SetTrigger(EnemyConstants.HashAttack); 
            }
            else
            {
                // Mage dùng animation dậm trượng
                stateMachine.Anim.Play("Mage_MeleeBurst", 0, 0f); 
            }
        }
        else
        {
            // 🟢 Boss cận chiến hoặc quái đánh xa đều dùng Attack Variant 3 (chém dọc bổ đất)
            stateMachine.Anim.SetInteger(EnemyConstants.HashAttackVariant, 3);
            stateMachine.Anim.SetTrigger(EnemyConstants.HashAttack); 

            lockedTargetPos = stateMachine.PlayerTarget.position;
            if (Physics.Raycast(lockedTargetPos + Vector3.up, Vector3.down, out RaycastHit hit, 5f, EnemyConstants.EnvLayerMask))
            {
                lockedTargetPos.y = hit.point.y + 0.1f;
            }
        }
    }

    public override void Tick(float deltaTime)
    {
        if (stateMachine.PlayerTarget == null) return;
        
        phaseTimer += deltaTime;

        switch (currentPhase)
        {
            case AttackPhase.Telegraph:
                TrackPlayer(deltaTime * 12f);
                stateMachine.ManualVelocity = Vector3.zero; 

                if (hasFired)
                {
                    currentPhase = AttackPhase.Recovery;
                    phaseTimer = 0f; 
                }
                
                // 🟢 THỰC HIỆN Ý TƯỞNG CỦA BẠN: KHÔNG CHO TỰ ĐỘNG BẮN NỮA!
                if (phaseTimer > 2.5f && !hasFired) 
                {
                    // In ra cảnh báo đỏ để Dev biết Animation Event đang bị lỗi/mất tích
                    Debug.LogError($"<color=red>⚠️ LỖI ANIMATION: {stateMachine.gameObject.name} không gọi được Event 'AnimationEvent_TriggerHitbox' sau 2.5 giây. HỦY ĐÒN!</color>");
                    
                    // Thoát khẩn cấp khỏi State này để AI không bị hóa đá
                    SafeReleaseToken();
                    stateMachine.SwitchState(stateMachine.StrafeState);
                    return; 
                }
                break;      

            case AttackPhase.Recovery:
                if (!isMeleeBurst) TrackPlayer(deltaTime * 3f);
                
                AnimatorStateInfo stateInfo = stateMachine.Anim.GetCurrentAnimatorStateInfo(0);
                
                if (stateInfo.normalizedTime >= 0.9f || phaseTimer > 2.5f)
                {
                    SafeReleaseToken();
                    
                    if (isMeleeBurst && Vector3.Distance(stateMachine.transform.position, stateMachine.PlayerTarget.position) < 4f)
                    {
                        stateMachine.DodgeState.Setup(stateMachine.PlayerTarget.position);
                        stateMachine.SwitchState(stateMachine.DodgeState);
                    }
                    else
                    {
                        stateMachine.SwitchState(stateMachine.StrafeState);
                    }
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
            stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRot, turnSpeed);
        }
    }

    public void FireProjectile()
    {
        if (hasFired) return;
        hasFired = true;

        if (isMeleeBurst)
        {
            Vector3 spawnPos = stateMachine.skillOriginPoint != null 
                ? stateMachine.skillOriginPoint.position 
                : stateMachine.transform.position + Vector3.up;

            GameObject burstVFX = ObjectPoolManager.Instance.SpawnFromPool(
                stateMachine.Stats.meleeBurstPrefab, spawnPos, Quaternion.identity);

            LightningAOE aoeScript = burstVFX.GetComponent<LightningAOE>();
            if (aoeScript != null)
            {
                aoeScript.Setup(stateMachine.Stats.attackDamage * 1.5f, stateMachine.Stats.enemyElement, true);
            }
        }
        else
        {
            // QUÁI THƯỜNG VÀ BOSS: Đều triệu hồi sét đánh thẳng vào vị trí Player (Giống Mage Lôi)
            if (stateMachine.Stats.rangedProjectilePrefab == null) return;
            GameObject aoeWarning = ObjectPoolManager.Instance.SpawnFromPool(
                stateMachine.Stats.rangedProjectilePrefab, lockedTargetPos, Quaternion.identity);
            LightningAOE aoeScript = aoeWarning.GetComponent<LightningAOE>();
            if (aoeScript != null)
            {
                aoeScript.Setup(stateMachine.Stats.attackDamage, stateMachine.Stats.enemyElement, false);
            }
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
        SafeReleaseToken();
    }
}