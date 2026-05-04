using UnityEngine;

public class PlayerImpactState : PlayerBaseState
{
    private readonly int ImpactHash = Animator.StringToHash("Impact"); // Animation bị trúng đòn
    private float duration = 0.5f; // Thời gian bị khựng
    private Vector3 attackerPosition;

    public PlayerImpactState(PlayerStateMachine stateMachine, Vector3 attackerPos) : base(stateMachine) 
    {
        this.attackerPosition = attackerPos;
    }

    public override void Enter()
    {
        // 1. Tắt Root Motion để không bị hoạt ảnh nhấc bổng lên
        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.CrossFadeInFixedTime(ImpactHash, 0.1f);
        stateMachine.Animator.SetBool("isPerformingAction", true);
        
        // 2. Triệt tiêu vận tốc rơi tự do cũ (nếu có) để tránh cộng dồn lực
        stateMachine.VerticalVelocity = 0f;

        // 3. Tính hướng văng (Chỉ văng ngang, không văng lên trời)
        Vector3 pushDir = (stateMachine.transform.position - attackerPosition);
        pushDir.y = 0; // 🟢 QUAN TRỌNG: Loại bỏ lực theo phương đứng
        
        stateMachine.CurrentVelocity = pushDir.normalized * 5f; // Lực văng lùi 5m/s
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);
        
        // Giảm dần lực văng
        stateMachine.CurrentVelocity = Vector3.Lerp(stateMachine.CurrentVelocity, Vector3.zero, deltaTime * 5f);
        Vector3 movement = stateMachine.CurrentVelocity;
        movement.y = stateMachine.VerticalVelocity;
        stateMachine.Controller.Move(movement * deltaTime);

        duration -= deltaTime;
        if (duration <= 0)
        {
            stateMachine.SwitchState(new PlayerMoveState(stateMachine));
        }
    }

    public override void Exit()
    {
        stateMachine.Animator.SetBool("isPerformingAction", false);
        // Bật i-frame 0.3s sau khi hồi phục (chống stun-lock)
        stateMachine.StartHitRecoveryIFrame();
    }
}