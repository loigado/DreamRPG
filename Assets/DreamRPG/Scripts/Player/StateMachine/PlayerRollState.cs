using UnityEngine;

public class PlayerRollState : PlayerBaseState
{
    private string currentAnimName;
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

        if (stateMachine.FootIK != null) stateMachine.FootIK.enabled = false;

        // 🟢 FIX: Bật I-Frame (Bất tử) khi lộn — chuẩn AAA (Elden Ring, GoW, Sekiro)
        stateMachine.EnableInvincibility();

        stateMachine.InputReader.Skill1Event += OnSkill1;
        stateMachine.InputReader.Skill2Event += OnSkill2;
        stateMachine.InputReader.Skill3Event += OnSkill3;
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);

        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName(currentAnimName) && stateInfo.normalizedTime >= 0.9f)
        {
            stateMachine.SwitchState(new PlayerMoveState(stateMachine));
        }
    }

    public override void Exit()
    {
        stateMachine.Animator.applyRootMotion = false; 
        stateMachine.Animator.SetBool("isPerformingAction", false); 

        // 🟢 FIX: Tắt I-Frame khi hết lộn
        stateMachine.DisableInvincibility();

        if (stateMachine.FootIK != null) stateMachine.FootIK.enabled = true;

        stateMachine.InputReader.Skill1Event -= OnSkill1;
        stateMachine.InputReader.Skill2Event -= OnSkill2;
        stateMachine.InputReader.Skill3Event -= OnSkill3;
    }

    private void OnSkill1() => stateMachine.TryExecuteSkill(1);
    private void OnSkill2() => stateMachine.TryExecuteSkill(2);
    private void OnSkill3() => stateMachine.TryExecuteSkill(3);
}