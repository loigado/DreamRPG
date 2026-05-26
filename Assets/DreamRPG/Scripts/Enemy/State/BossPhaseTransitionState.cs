using UnityEngine;

public class BossPhaseTransitionState : EnemyState
{
    private float timer;

    public BossPhaseTransitionState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        stateMachine.ManualVelocity = Vector3.zero;
        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh) 
            stateMachine.Agent.isStopped = true;

        // Bật cờ Phase 2 để tăng tốc độ sau này
        stateMachine.IsPhase2 = true;
        
        // Cầu chì an toàn: Giả sử gầm mất 3 giây
        timer = 3.0f; 
        
        // 🟢 Lưu ý Animator: Bạn cần gán một clip Roar vào Animator và nối Any State với Trigger "BossRoar"
        stateMachine.Anim.SetTrigger(EnemyConstants.HashBossRoar);
        
        Debug.Log("<color=magenta>🔥 LÔI NGỰ TƯỚNG VÀO PHASE 2! SỨC MẠNH QUÁ TẢI!</color>");
        
        // 🟢 Reset lại toàn bộ máu vô hình (Guard) và Thanh Vỡ Thế (Posture)
        if (stateMachine.Health != null)
        {
            stateMachine.Health.RefillGuard();
            stateMachine.Health.ResetPosture();
        }
        
        // Vụ nổ sét không còn gọi lập tức ở đây nữa, mà sẽ đợi Animation Event (TriggerPhase2Explosion) gọi từ EnemyStateMachine.
    }

    public override void Tick(float deltaTime)
    {
        timer -= deltaTime;
        
        // Lấy thời gian thực tế của Animator để ra khỏi State
        AnimatorStateInfo stateInfo = stateMachine.Anim.GetCurrentAnimatorStateInfo(0);
        bool isRoaring = stateInfo.IsName("Boss_Roar"); // Thay "Boss_Roar" bằng tên State thật trong Animator

        if (timer <= 0 || (isRoaring && stateInfo.normalizedTime >= 0.95f))
        {
            stateMachine.SwitchState(stateMachine.StrafeState);
        }
    }

    public override void Exit() { }
}