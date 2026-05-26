using UnityEngine;

public class EnemyParriedState : EnemyState
{
    private float stunTimer;
    private const float STUN_DURATION = 2.0f; // 🟢 THỜI GIAN BỊ CHOÁNG (2 GIÂY)

    public EnemyParriedState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        var afterimage = stateMachine.GetComponent<AfterimageController>();
        if (afterimage != null) afterimage.StopTrail(); 
        // 1. Dừng mọi di chuyển của quái
        stateMachine.ManualVelocity = Vector3.zero;
        if (stateMachine.Agent != null && stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
        {
            stateMachine.Agent.isStopped = true;
        }

        // 2. Tắt cờ tấn công (nếu có) để tránh lỗi quái chém tiếp sau khi tỉnh
        stateMachine.IsHoldingAttackToken = false;

        // 3. 🟢 KÍCH HOẠT ANIMATION BỊ PARRY
        // Yêu cầu: Trong Animator của quái, bạn phải tạo một Parameter kiểu Trigger tên là "Parried"
        stateMachine.Anim.SetTrigger("Parried");

        // Đặt thời gian choáng
        stunTimer = STUN_DURATION;
        
        Debug.Log($"<color=cyan>💫 {stateMachine.gameObject.name} bị Parry! Choáng váng trong {STUN_DURATION}s!</color>");
    }

    public override void Tick(float deltaTime)
    {
        stunTimer -= deltaTime;
        
        // Ép quái đứng im tại chỗ chịu trận
        stateMachine.ManualVelocity = Vector3.Lerp(stateMachine.ManualVelocity, Vector3.zero, deltaTime * 5f);

        // Hết thời gian choáng -> Trở lại trạng thái bình thường
        if (stunTimer <= 0f)
        {
            stateMachine.SwitchState(stateMachine.IdleState);
        }
    }

    public override void Exit()
    {
        stateMachine.ManualVelocity = Vector3.zero;
    }
}