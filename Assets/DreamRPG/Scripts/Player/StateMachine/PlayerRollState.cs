using UnityEngine;

public class PlayerRollState : PlayerBaseState
{
    private string currentAnimName;
    public bool IsPerfectDodgeWindow { get; set; } 

    public PlayerRollState(PlayerStateMachine stateMachine) : base(stateMachine) {}

    public override void Enter()
    {
        float rollCost = 25f;
        stateMachine.Stamina.UseStamina(rollCost);

        Vector3 movement = CalculateMovement();
        if (movement.sqrMagnitude > 0)
            stateMachine.transform.rotation = Quaternion.LookRotation(movement);

        stateMachine.Animator.applyRootMotion = true;
        stateMachine.Animator.SetBool("isPerformingAction", true); 

        currentAnimName = stateMachine.CurrentWeapon != null ? stateMachine.CurrentWeapon.RollAnimName : "Unarmed_Roll";
        stateMachine.Animator.CrossFadeInFixedTime(currentAnimName, 0.2f);

        IsPerfectDodgeWindow = false;

        stateMachine.InputReader.Skill1Event += OnSkill1;
        stateMachine.InputReader.Skill2Event += OnSkill2;
        stateMachine.InputReader.Skill3Event += OnSkill3;
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);

        // ====================================================================
        // 🟢 NGÃ RẼ 1: PERFECT DODGE COUNTER (CHỈ DÀNH CHO KIẾM - HỆ LÔI)
        // ====================================================================
        if (stateMachine.HasPerformedPerfectDodge && stateMachine.InputReader.BufferedInput == BufferedCommand.Attack)
        {
            WeaponData weapon = stateMachine.CurrentWeapon;
            
            // Kiểm tra xem vũ khí hiện tại có chữ "Kiếm" hoặc "Sword" trong tên không
            if (weapon != null && (weapon.WeaponName.Contains("Sword") || weapon.WeaponName.Contains("Kiếm") || weapon.WeaponName.Contains("Lôi")))
            {
                if (stateMachine.Stamina.HasEnoughStamina(15f))
                {
                    stateMachine.InputReader.ConsumeBuffer();
                    stateMachine.SwitchState(new PlayerCounterState(stateMachine)); 
                    return;
                }
            }
        }

        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName(currentAnimName))
        {
            // ====================================================================
            // 🟢 NGÃ RẼ 2: ĐÂM TRƯỢT BÌNH THƯỜNG (DÀNH CHO MỌI VŨ KHÍ)
            // ====================================================================
            if (stateInfo.normalizedTime > 0.8f)
            {
                if (stateMachine.InputReader.BufferedInput == BufferedCommand.Attack)
                {
                    if (stateMachine.CurrentWeapon != null && stateMachine.Stamina.HasEnoughStamina(stateMachine.CurrentWeapon.StaminaCost))
                    {
                        stateMachine.InputReader.ConsumeBuffer();
                        stateMachine.SwitchState(new PlayerAttackState(stateMachine, 0, false, true)); 
                        return;
                    }
                }

                if (stateMachine.InputReader.BufferedInput == BufferedCommand.Roll)
                {
                    if (stateMachine.Stamina.HasEnoughStamina(25f))
                    {
                        stateMachine.InputReader.ConsumeBuffer();
                        stateMachine.SwitchState(new PlayerRollState(stateMachine));
                        return;
                    }
                }
            }

            if (stateInfo.normalizedTime >= 0.9f)
            {
                stateMachine.SwitchState(new PlayerMovementState(stateMachine));
            }
        }
    }

    public override void Exit()
    {
        IsPerfectDodgeWindow = false; 
        stateMachine.HasPerformedPerfectDodge = false; 

        stateMachine.Animator.applyRootMotion = false; 
        stateMachine.Animator.SetBool("isPerformingAction", false); 
        
        stateMachine.DisableInvincibility();

        stateMachine.InputReader.Skill1Event -= OnSkill1;
        stateMachine.InputReader.Skill2Event -= OnSkill2;
        stateMachine.InputReader.Skill3Event -= OnSkill3;
    }

    private void OnSkill1() => stateMachine.TryExecuteSkill(1);
    private void OnSkill2() => stateMachine.TryExecuteSkill(2);
    private void OnSkill3() => stateMachine.TryExecuteSkill(3);
}