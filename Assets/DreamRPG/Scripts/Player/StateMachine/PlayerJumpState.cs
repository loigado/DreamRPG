using UnityEngine;

public class PlayerJumpState : PlayerBaseState
{
    private string currentAnimName;
    public PlayerJumpState(PlayerStateMachine stateMachine) : base(stateMachine) {}

    public override void Enter()
    {
        float jumpCost = 15f;
        stateMachine.Stamina.UseStamina(jumpCost);

        stateMachine.VerticalVelocity = Mathf.Sqrt(stateMachine.JumpHeight * -2f * stateMachine.Gravity);
        
        currentAnimName = stateMachine.CurrentWeapon != null ? stateMachine.CurrentWeapon.JumpAnimName : "Unarmed_Jump";

        stateMachine.Animator.SetBool("isPerformingAction", true); 
        stateMachine.Animator.CrossFadeInFixedTime(currentAnimName, 0.2f);

        if (stateMachine.FootIK != null) stateMachine.FootIK.enabled = false;
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);

        Vector3 movement = stateMachine.CurrentVelocity;
        movement.y = stateMachine.VerticalVelocity;
        stateMachine.Controller.Move(movement * deltaTime);

        if (stateMachine.VerticalVelocity <= 0)
            stateMachine.SwitchState(new PlayerFallState(stateMachine));
    }

    public override void Exit() {}
}