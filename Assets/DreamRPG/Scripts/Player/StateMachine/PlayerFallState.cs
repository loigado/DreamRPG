using UnityEngine;

public class PlayerFallState : PlayerBaseState
{
    private float groundedGracePeriod = 0f;
    private const float GROUND_CONFIRM_TIME = 0.1f; // Phải chạm đất ổn định 0.1s mới chuyển state

    public PlayerFallState(PlayerStateMachine stateMachine) : base(stateMachine) {}

    public override void Enter()
    {
        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.CrossFadeInFixedTime("Fall", 0.15f);

        if (stateMachine.FootIK != null) stateMachine.FootIK.enabled = false;

        // Reset grace period
        groundedGracePeriod = 0f;
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);

        // Cho phép điều hướng nhẹ trong lúc rơi (air control)
        Vector3 movement = CalculateMovement() * stateMachine.FreeLookMovementSpeed * 0.5f;
        movement.y = stateMachine.VerticalVelocity;
        stateMachine.Controller.Move(movement * deltaTime);

        // 🟢 FIX: Không chuyển state ngay khi isGrounded = true 1 frame
        // Phải chạm đất ỔN ĐỊNH (liên tục trong 0.1s) mới chuyển sang MoveState
        // Tránh loop Fall → Move → Fall trên địa hình gồ ghề
        if (stateMachine.Controller.isGrounded)
        {
            groundedGracePeriod += deltaTime;

            if (groundedGracePeriod >= GROUND_CONFIRM_TIME)
            {
                // Đã chạm đất ổn định → chuyển sang MoveState
                stateMachine.VerticalVelocity = -2f;
                stateMachine.CurrentVelocity = Vector3.zero;
                stateMachine.SwitchState(new PlayerMoveState(stateMachine));
            }
        }
        else
        {
            // Chưa chạm đất hoặc bị nảy lên → reset counter
            groundedGracePeriod = 0f;
        }
    }

    public override void Exit()
    {
        if (stateMachine.FootIK != null) stateMachine.FootIK.enabled = true;
    }
}