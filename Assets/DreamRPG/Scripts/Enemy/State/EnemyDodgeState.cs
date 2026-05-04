using UnityEngine;

/// <summary>
/// EnemyDodgeState — Quái lộn/bật lùi né đòn Player (GoW Evasion).
///
/// Kích hoạt: EnemyStateMachine.NotifyPlayerAttackNearby() → kiểm tra dodgeChance.
///
/// Logic:
///   • Tính hướng né: ngược hướng attacker (lộn lùi)
///   • Di chuyển bằng ManualVelocity trong khoảng dodgeDuration
///   • Sau khi xong → về StrafeState
/// </summary>
public class EnemyDodgeState : EnemyState
{
    private Vector3 dodgeDirection;
    private float   dodgeTimer;

    public EnemyDodgeState(EnemyStateMachine stateMachine, Vector3 attackerPos)
        : base(stateMachine)
    {
        Vector3 dirFromAttacker = (stateMachine.transform.position - attackerPos).normalized;
        dirFromAttacker.y = 0f;

        float distToAttacker = Vector3.Distance(stateMachine.transform.position, attackerPos);

        if (distToAttacker > stateMachine.Stats.attackRange + 3f)
        {
            // 🟢 BỊ BẮN TỪ XA → Lộn NGANG (sang trái hoặc phải) để né mũi tên
            Vector3 sideDir = Vector3.Cross(dirFromAttacker, Vector3.up).normalized;
            float side = Random.value > 0.5f ? 1f : -1f; // Random trái hoặc phải
            dodgeDirection = sideDir * side;
        }
        else
        {
            // BỊ CHÉM CẬN CHIẾN → Lộn LÙI ra xa + offset ngang nhẹ
            Vector3 sideOffset = Vector3.Cross(dirFromAttacker, Vector3.up) * Random.Range(-0.4f, 0.4f);
            dodgeDirection = (dirFromAttacker + sideOffset).normalized;
        }
    }

    public override void Enter()
    {
        dodgeTimer = stateMachine.Stats.dodgeDuration;

        // Dừng Agent
        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
            stateMachine.Agent.isStopped = true;

        // Tính toán hướng né tương đối so với mặt con quái (Local Direction)
        Vector3 localDodgeDir = stateMachine.transform.InverseTransformDirection(dodgeDirection);
        
        // Gửi tham số vào Animator để bạn có thể dùng Blend Tree (DodgeX, DodgeZ)
        stateMachine.Anim.SetFloat("DodgeX", localDodgeDir.x);
        stateMachine.Anim.SetFloat("DodgeZ", localDodgeDir.z);

        // Bật Root Motion để Animation tự động đẩy CharacterController
        stateMachine.Anim.applyRootMotion = true;
        stateMachine.ManualVelocity = Vector3.zero;

        // Trigger animation
        stateMachine.Anim.SetTrigger("Dodge");

        // Kiểm tra tường: nếu dodge sẽ đâm vào tường → đổi hướng
        Vector3 rayStart = stateMachine.transform.position + Vector3.up;
        // 🟢 FIX: Chỉ quét Layer Environment để tránh đụng trúng Player/quái khác
        int wallMask = LayerMask.GetMask("Environment");
        if (Physics.Raycast(rayStart, dodgeDirection, stateMachine.Stats.dodgeSpeed * stateMachine.Stats.dodgeDuration, wallMask))
        {
            // Đổi sang lộn sang bên thay vì lùi
            dodgeDirection = Vector3.Cross(dodgeDirection, Vector3.up).normalized;
        }
    }

    public override void Tick(float deltaTime)
    {
        dodgeTimer -= deltaTime;

        // KHÔNG dùng ManualVelocity nữa, để Root Motion của Animation tự di chuyển con quái!
        stateMachine.ManualVelocity = Vector3.zero;

        if (dodgeTimer <= 0f)
        {
            stateMachine.SwitchState(new EnemyStrafeState(stateMachine));
        }
    }

    public override void Exit()
    {
        // Trả lại trạng thái tắt Root Motion để các code di chuyển khác hoạt động bình thường
        stateMachine.Anim.applyRootMotion = false;
        stateMachine.ManualVelocity = Vector3.zero;
    }
}
