using UnityEngine;

/// <summary>
/// EnemyBlockState — Quái chủ động giơ vũ khí/khiên lên đỡ đòn (GoW Guard).
///
/// Kích hoạt: Tương tự Dodge, khi phát hiện Player tấn công, có tỷ lệ chuyển sang Block.
/// Logic:
///   • Bật cờ `isBlocking` trong EnemyHealth để kháng 100% damage.
///   • Phát animation Block_Loop.
///   • Nếu bị chém trúng trong lúc đỡ -> phát animation Block_Impact và dội lại Player (nếu muốn).
///   • Tự động thoát sau `blockDuration` hoặc chuyển sang phản công (Parry).
/// </summary>
public class EnemyBlockState : EnemyState
{
    private float blockTimer;

    public EnemyBlockState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        // Dừng mọi di chuyển
        stateMachine.ManualVelocity = Vector3.zero;
        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
            stateMachine.Agent.isStopped = true;

        // Thời gian đỡ đòn (có thể lấy từ Stats, tạm hardcode 1.5s - 2.5s)
        blockTimer = Random.Range(1.5f, 2.5f);

        // Bật cờ miễn nhiễm sát thương
        if (stateMachine.Health != null)
            stateMachine.Health.isBlocking = true;

        // Phát animation giơ khiên/vũ khí
        stateMachine.Anim.SetBool("IsBlocking", true);
        stateMachine.Anim.SetTrigger("BlockStart");
    }

    public override void Tick(float deltaTime)
    {
        if (stateMachine.PlayerTarget == null) return;

        blockTimer -= deltaTime;

        // Xoay mặt bám theo Player để đỡ cho chuẩn
        Vector3 dirToPlayer = (stateMachine.PlayerTarget.position - stateMachine.transform.position);
        dirToPlayer.y = 0f;
        if (dirToPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dirToPlayer.normalized);
            stateMachine.transform.rotation = Quaternion.Slerp(
                stateMachine.transform.rotation, targetRot, deltaTime * 8f);
        }

        // Hết thời gian đỡ -> bỏ khiên xuống, quay về Strafe
        if (blockTimer <= 0f)
        {
            stateMachine.SwitchState(new EnemyStrafeState(stateMachine));
        }
    }

    public void OnImpact()
    {
        // Hàm này được gọi từ EnemyStateMachine khi Health báo OnBlocked
        stateMachine.Anim.SetTrigger("BlockImpact");
        
        // Trừ bớt thời gian block để không giơ khiên mãi
        blockTimer -= 0.5f; 
        
        // (Tùy chọn) Thêm hiệu ứng xẹt lửa, âm thanh keng keng ở đây!
    }

    public override void Exit()
    {
        stateMachine.ManualVelocity = Vector3.zero;

        // Tắt cờ miễn nhiễm
        if (stateMachine.Health != null)
            stateMachine.Health.isBlocking = false;

        // Hạ khiên
        stateMachine.Anim.SetBool("IsBlocking", false);
    }
}
