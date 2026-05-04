using UnityEngine;

public class PlayerIceAxeLeapSlamState : PlayerBaseState
{
    private SkillData skill;
    private bool hasLaunched = false;

    public PlayerIceAxeLeapSlamState(PlayerStateMachine stateMachine, SkillData skill) : base(stateMachine)
    {
        this.skill = skill;
    }

    public override void Enter()
    {
        hasLaunched = false;

        stateMachine.Stamina.UseStamina(skill.staminaCost);
        stateMachine.SkillManager.StartCooldown(skill);

        // 🟢 SUPER ARMOR: Chống bị đánh ngắt khi đang bay người
        stateMachine.EnableInvincibility();

        // 🟢 BẬT ROOT MOTION: Để Animation điều khiển việc nhảy lên và lao về phía trước
        stateMachine.Animator.applyRootMotion = true;
        
        // Reset vận tốc cũ để bước nhảy chuẩn xác theo Animation
        stateMachine.CurrentVelocity = Vector3.zero;
        
        stateMachine.Animator.CrossFadeInFixedTime(skill.animationName, 0.1f);
        PlayerStateMachine.OnPlayerAttack?.Invoke(stateMachine.transform.position);
        stateMachine.Animator.SetBool("isPerformingAction", true);

        // 🎯 TỰ ĐIỀU HƯỚNG: Xoay mặt về phía quái khi bắt đầu nhảy
        if (stateMachine.TargetSys.GetCurrentTarget() != null)
        {
            stateMachine.TargetSys.FaceTarget(stateMachine.TargetSys.GetCurrentTarget().position, deltaTime: 0, instant: true);
        }
    }

    public override void Tick(float deltaTime)
    {
        // Vẫn áp dụng trọng lực nhẹ để nếu nhảy qua vực sẽ không bị bay lơ lửng
        ApplyGravity(deltaTime);

        // Cho phép Roll để hủy động tác (Cancel window)
        if (stateMachine.InputReader.IsRolling)
        {
            stateMachine.SwitchState(new PlayerRollState(stateMachine));
            return; 
        }

        // Thoát State khi Animation gần kết thúc
        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName(skill.animationName) && stateInfo.normalizedTime >= 0.75f)
        {
            stateMachine.SwitchState(new PlayerMoveState(stateMachine));
        }
    }

    public void ExecuteLeapSlam()
    {
        if (hasLaunched) return;
        hasLaunched = true;

        if (skill.vfxPrefab != null)
        {
            // Spawn VFX tại vị trí chân nhân vật lúc đáp xuống
            Vector3 spawnPos = stateMachine.transform.position + stateMachine.transform.forward * 1f;
            
            // 🟢 FIX: Dùng Object Pool
            ObjectPoolManager.Instance.SpawnFromPool(skill.vfxPrefab, spawnPos, stateMachine.transform.rotation);
            // ❌ ĐÃ XÓA LỆNH DESTROY

            // Quét sát thương diện rộng
            Collider[] hits = Physics.OverlapSphere(spawnPos, 7f, LayerMask.GetMask("Enemy"));
            
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<EnemyHealth>(out EnemyHealth health))
                {
                    float damage = (stateMachine.CurrentWeapon != null ? stateMachine.CurrentWeapon.Damage : 30f) * skill.damageMultiplier;
                    
                    health.TakeDamage(damage, stateMachine.transform.position, 18f);
                    
                    // 🧊 ĐÓNG BĂNG: Quái dính chiêu sẽ bị đóng băng
                    // Nếu bị đánh tiếp khi đang đóng băng → VỠ BĂNG (Shatter) x2 sát thương!
                    health.ApplyFreeze(skill.freezeDuration);
                }
            }
        }
    }

    public override void Exit()
    {
        stateMachine.DisableInvincibility();
        stateMachine.Animator.applyRootMotion = false; 
        stateMachine.Animator.SetBool("isPerformingAction", false);
    }
}
