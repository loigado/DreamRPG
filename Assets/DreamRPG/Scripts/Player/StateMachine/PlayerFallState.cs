using UnityEngine;

/// <summary>
/// PlayerFallState — Chuẩn AAA
/// Điểm nhấn: Tiếp đất tức thời (Instant Landing), triệt tiêu đà rơi ngay trong frame đầu tiên chạm đất,
/// "bóp cò" chuyển anim nhạy bén để chân chạm đất ngay lập tức.
/// </summary>
public class PlayerFallState : PlayerBaseState
{
    // =========================================================
    // THÔNG SỐ VẬT LÝ ĐÃ TINH CHỈNH (AAA ACTION FEEL)
    // =========================================================
    // Dù rơi nhanh hay chậm, khi chạm map, Kratos phải "đáp nhẹ"
    private const float LandingVerticalForce = -2f; 
    
    // Rơi nặng rưỡi (dứt khoát nhưng không lao đầu)
    private const float FallGravityMultiplier = 1.5f; 
    private const float MaxFallSpeed = -18f;          
    private const float AirControlSpeed = 5.0f;       
    private const float AirControlSmoothing = 6.0f;  
    
    // =========================================================
    private Vector3 currentAirVelocity;
    
    // Biến theo dõi Double Jump (Giữ nguyên từ bản trước)
    private bool hasReleasedJump = false;

    public PlayerFallState(PlayerStateMachine stateMachine) : base(stateMachine) {}

    public override void Enter()
    {
        // 🟢 AAA Polish: Tắt Root Motion để Code kiểm soát hoàn toàn vị trí XZ
        stateMachine.Animator.applyRootMotion = false;
        
        // 🟢 Chuyển sang Anim Rơi nhanh (0.1f) để tạo độ nhạy
        stateMachine.Animator.CrossFadeInFixedTime("Fall", 0.1f);

        //if (stateMachine.FootIK != null) stateMachine.FootIK.enabled = false;

        hasReleasedJump = !stateMachine.InputReader.IsJumping;

        // Bảo toàn đà ngang khi bắt đầu rơi
        currentAirVelocity = stateMachine.CurrentVelocity;
        currentAirVelocity.y = 0f;
    }

    public override void Tick(float deltaTime)
    {
        // 1. Kiểm tra nhả phím cho Double Jump
        if (!stateMachine.InputReader.IsJumping)
        {
            hasReleasedJump = true;
        }

        // 2. CỨU THUA BẰNG DOUBLE JUMP KHI ĐANG RƠI (Giữ nguyên)
        if (hasReleasedJump && stateMachine.InputReader.IsJumping && stateMachine.CanDoubleJump)
        {
            if (stateMachine.Stamina.HasEnoughStamina(15f))
            {
                stateMachine.CanDoubleJump = false; 
                stateMachine.SwitchState(new PlayerJumpState(stateMachine));
                return;
            }
        }

        // 3. 🔴 LOGIC CHẠM ĐẤT AAA: TỨC THỜI, KHÔNG TRỄ
        if (stateMachine.Controller.isGrounded)
        {
            // ⚡ AAA TRICK 1: TRIỆT TIÊU ĐÀ RƠI NGAY LẬP TỨC
            // Chúng ta ép VerticalVelocity về -2f ngay trong frame này.
            // Điều này ngăn hệ thống vật lý đẩy dội nhân vật lên (Bounce), tạo ra cú chạm "đét" chân xuống sàn.
            stateMachine.VerticalVelocity = LandingVerticalForce; 
            
            // Nạp lại Double Jump
            stateMachine.CanDoubleJump = true; 
            
            // ⚡ AAA TRICK 2: DỪNG HẲN TRƯỢT NGANG (Chống lỗi trôi chân)
            // Ép đà di chuyển ngang về 0 để khi Idle, chân cắm chặt xuống đất.
            stateMachine.CurrentVelocity = Vector3.zero;

            // ⚡ AAA TRICK 3: BẺ LÁI HƯỚNG MẶT NHỊP CUỐI (Turn speed 20f cực nhanh)
            // Xoay Kratos thẳng hướng camera ngay tích tắc chạm gót, trông rất uy lực.
            FaceAirDirection(deltaTime * 20f); 

            // ⚡ AAA TRICK 4: "BÓP CÒ" CHUYỂN STATE NGAY LẬP TỨC
            // Không đợi counter, không đợi counters, chuyển sang MoveState ngay trong frame này.
            stateMachine.SwitchState(new PlayerMovementState(stateMachine));
            return;
        }
        else
        {
            // Logic khi đang rơi (Giữ nguyên)
            stateMachine.VerticalVelocity += stateMachine.Gravity * FallGravityMultiplier * deltaTime;

            if (stateMachine.VerticalVelocity < MaxFallSpeed)
            {
                stateMachine.VerticalVelocity = MaxFallSpeed;
            }
        }

        // 4. Bẻ lái khi rơi (Air Control)
        Vector2 input = stateMachine.InputReader.MovementValue;
        Vector3 targetInputDir = CalculateMovementDirection(input);
        Vector3 targetAirVelocity = targetInputDir * AirControlSpeed;

        currentAirVelocity = Vector3.Lerp(currentAirVelocity, targetAirVelocity, deltaTime * AirControlSmoothing);

        Vector3 finalMotion = currentAirVelocity;
        finalMotion.y = stateMachine.VerticalVelocity;

        // Di chuyển
        stateMachine.Controller.Move(finalMotion * deltaTime);

        // Xoay mặt mượt mà khi đang rơi tự do
        if (!stateMachine.Controller.isGrounded)
        {
            FaceAirDirection(deltaTime * 10f);
        }
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
        Vector3 moveDir = currentAirVelocity;
        moveDir.y = 0;
        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir.normalized);
            stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRot, turnSpeed);
        }
    }

    public override void Exit()
    {
        // Gác chân lại (IK)
        //if (stateMachine.FootIK != null) stateMachine.FootIK.enabled = true;
    }
}