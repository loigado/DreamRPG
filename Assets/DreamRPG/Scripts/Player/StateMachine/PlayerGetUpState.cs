using UnityEngine;

/// <summary>
/// Xử lý lúc Player nằm dưới đất và lồm cồm bò dậy sau khi bị Knockback.
/// Cung cấp Wake-up I-Frames (Bất tử lúc đứng dậy) chuẩn AAA.
/// </summary>
public class PlayerGetUpState : PlayerBaseState
{
    // Khi nào có Animation Đứng dậy, bạn điền tên nó vào đây (ví dụ: "GetUp")
    // Tạm thời để trống hoặc dùng tên một Animation nhàn rỗi nào đó.
    private readonly int GetUpHash = Animator.StringToHash("Idle"); 
    
    private float getUpTimer;

    public PlayerGetUpState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        // Khóa input và di chuyển
        stateMachine.CurrentVelocity = Vector3.zero;
        stateMachine.VerticalVelocity = -2f; // Ép dính chặt xuống đất
        
        // 🟢 WAKE-UP I-FRAMES: Bật bất tử ngay khi bắt đầu đứng dậy
        stateMachine.EnableInvincibility();

        // Chạy Animation Đứng dậy (Sau này thay Hash vào là xong)
        stateMachine.Animator.CrossFadeInFixedTime(GetUpHash, 0.2f);
        
        // Thời gian giả lập cho việc lồm cồm bò dậy (khoảng 1.2 giây)
        getUpTimer = 0.2f;
    }

    public override void Tick(float deltaTime)
    {
        // Ép rớt xuống đất nếu có lỡ lơ lửng
        ApplyGravity(deltaTime);
        Vector3 movement = Vector3.zero;
        movement.y = stateMachine.VerticalVelocity;
        stateMachine.Controller.Move(movement * deltaTime);

        getUpTimer -= deltaTime;

        // Bò dậy xong -> Trả lại quyền điều khiển
        if (getUpTimer <= 0f)
        {
            stateMachine.SwitchState(new PlayerMovementState(stateMachine));
        }
    }

    public override void Exit()
    {
        // Trả lại Input
        stateMachine.Animator.SetBool("isPerformingAction", false);
        
        // 🟢 TẮT BẤT TỬ khi đã đứng thẳng hoàn toàn
        stateMachine.DisableInvincibility();
        
        // Kích hoạt thêm I-Frame phụ để không bị đánh lén ngay khoảnh khắc chuyển State
        stateMachine.StartHitRecoveryIFrame();
    }
}