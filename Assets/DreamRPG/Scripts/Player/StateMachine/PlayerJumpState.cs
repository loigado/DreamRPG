using UnityEngine;

public class PlayerJumpState : PlayerBaseState
{
    private string currentAnimName;

    private const float JumpGravityMultiplier = 1.0f; 
    private const float ApexGravityMultiplier = 1.0f; 
    private const float ApexThreshold = 1.0f;        
    private const float AirControlSpeed = 5.0f;       
    private const float AirControlSmoothing = 8.0f;  

    private Vector3 currentAirVelocity;
    
    // 🟢 Biến theo dõi thao tác nhả phím của người chơi
    private bool hasReleasedJump = false;

    public PlayerJumpState(PlayerStateMachine stateMachine) : base(stateMachine) {}

    public override void Enter()
    {
        float jumpCost = 15f;
        stateMachine.Stamina.UseStamina(jumpCost);

        // Chuẩn AAA: Ghi đè tuyệt đối vận tốc Y. Dù đang rơi nhanh cỡ nào cũng bị triệt tiêu!
        stateMachine.VerticalVelocity = Mathf.Sqrt(stateMachine.JumpHeight * -2f * stateMachine.Gravity);
        
        currentAnimName = stateMachine.CurrentWeapon != null ? stateMachine.CurrentWeapon.JumpAnimName : "Unarmed_Jump";

        stateMachine.Animator.SetBool("isPerformingAction", true); 
        stateMachine.Animator.CrossFadeInFixedTime(currentAnimName, 0.1f); 
        
        // 🟢 AAA Trick: Ép Animation phát lại từ frame 0 để tạo độ "giật" (Snap) nếu đang ở giữa đòn nhảy 1
        stateMachine.Animator.Play(currentAnimName, -1, 0f);

        //if (stateMachine.FootIK != null) stateMachine.FootIK.enabled = false;

        currentAirVelocity = stateMachine.CurrentVelocity;
        currentAirVelocity.y = 0f;

        // Nếu lúc vừa vào state mà không giữ phím nhảy -> Đánh dấu là đã nhả phím
        hasReleasedJump = !stateMachine.InputReader.IsJumping;
    }

    public override void Tick(float deltaTime)
    {
        // 1. Kiểm tra nhả phím
        if (!stateMachine.InputReader.IsJumping)
        {
            hasReleasedJump = true;
        }

        // 2. 🟢 KÍCH HOẠT DOUBLE JUMP
        if (hasReleasedJump && stateMachine.InputReader.IsJumping && stateMachine.CanDoubleJump)
        {
            if (stateMachine.Stamina.HasEnoughStamina(15f))
            {
                stateMachine.CanDoubleJump = false; // Tước quyền nhảy tiếp
                stateMachine.SwitchState(new PlayerJumpState(stateMachine)); // Gọi lại chính nó để bật lên
                return;
            }
        }

        // 3. Logic Động Lực Học (Giữ nguyên)
        float targetGravity = stateMachine.Gravity;

        if (stateMachine.VerticalVelocity > ApexThreshold)
        {
            targetGravity *= JumpGravityMultiplier;
        }
        else if (stateMachine.VerticalVelocity > -ApexThreshold)
        {
            targetGravity *= ApexGravityMultiplier;
        }
        else
        {
            stateMachine.SwitchState(new PlayerFallState(stateMachine));
            return;
        }

        stateMachine.VerticalVelocity += targetGravity * deltaTime;

        // 4. Điều khiển bẻ lái
        Vector2 input = stateMachine.InputReader.MovementValue;
        Vector3 targetInputDir = CalculateMovementDirection(input);

        Vector3 targetAirVelocity = targetInputDir * AirControlSpeed;
        currentAirVelocity = Vector3.Lerp(currentAirVelocity, targetAirVelocity, deltaTime * AirControlSmoothing);

        Vector3 finalMotion = currentAirVelocity;
        finalMotion.y = stateMachine.VerticalVelocity;

        stateMachine.Controller.Move(finalMotion * deltaTime);

        FaceAirDirection(deltaTime * 10f); 
    }

    private Vector3 CalculateMovementDirection(Vector2 input)
    {
        if (input == Vector2.zero) return Vector3.zero;

        Vector3 forward = stateMachine.MainCameraTransform.forward;
        Vector3 right = stateMachine.MainCameraTransform.right;
        forward.y = 0f;
        right.y = 0f;

        return (forward * input.y + right * input.x).normalized;
    }

    private void FaceAirDirection(float turnSpeed)
    {
        if (currentAirVelocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(currentAirVelocity.normalized);
            stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRot, turnSpeed);
        }
    }

    public override void Exit() {}
}