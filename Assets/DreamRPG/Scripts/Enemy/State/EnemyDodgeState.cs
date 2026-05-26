using UnityEngine;

public class EnemyDodgeState : EnemyState
{
    private Vector3 dodgeDirection;
    private float   dodgeTimer;
    private Vector3 attackerPos;

    public EnemyDodgeState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public void Setup(Vector3 attackerPosition)
    {
        this.attackerPos = attackerPosition;
    }

    public override void Enter()
    {
        // 🟢 BẬT CỜ BÁO HIỆU: TA ĐANG NÉ ĐÒN ĐÂY!
        stateMachine.IsDodging = true;

        Vector3 dirFromAttacker = (stateMachine.transform.position - attackerPos).normalized;
        dirFromAttacker.y = 0f;
        float distToAttacker = Vector3.Distance(stateMachine.transform.position, attackerPos);

        if (distToAttacker > stateMachine.Stats.attackRange + 3f)
        {
            Vector3 sideDir = Vector3.Cross(dirFromAttacker, Vector3.up).normalized;
            float side = Random.value > 0.5f ? 1f : -1f;
            dodgeDirection = sideDir * side;
        }
        else
        {
            Vector3 sideOffset = Vector3.Cross(dirFromAttacker, Vector3.up) * Random.Range(-0.4f, 0.4f);
            dodgeDirection = (dirFromAttacker + sideOffset).normalized;
        }

        dodgeTimer = stateMachine.Stats.dodgeDuration;

        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
            stateMachine.Agent.isStopped = true;

        // 🟢 KIỂM TRA PHÍA TRƯỚC CÓ TƯỜNG/VẬT CẢN KHÔNG
        Vector3 rayStart = stateMachine.transform.position + Vector3.up;
        float checkDist = stateMachine.Stats.dodgeSpeed * stateMachine.Stats.dodgeDuration;
        if (Physics.Raycast(rayStart, dodgeDirection, checkDist, EnemyConstants.EnvLayerMask))
        {
            // Bị cản -> Lách sang hướng vuông góc
            dodgeDirection = Vector3.Cross(dodgeDirection, Vector3.up).normalized;
        }

        Vector3 localDodgeDir = stateMachine.transform.InverseTransformDirection(dodgeDirection);
        
        stateMachine.Anim.SetFloat(EnemyConstants.HashDodgeX, localDodgeDir.x);
        stateMachine.Anim.SetFloat(EnemyConstants.HashDodgeZ, localDodgeDir.z);

        stateMachine.Anim.applyRootMotion = true;
        stateMachine.ManualVelocity = Vector3.zero;
        stateMachine.Anim.SetTrigger(EnemyConstants.HashDodge);

        // 🟢 NÂNG CẤP BOSS: Sử dụng kỹ năng Tàn Ảnh của sát thủ hệ lôi để né đòn siêu tốc
        if (stateMachine.Stats.isMiniBoss)
        {
            var afterimage = stateMachine.GetComponent<AfterimageController>();
            if (afterimage != null) afterimage.StartTrail(stateMachine.Stats.enemyElement);
            stateMachine.Anim.speed = 1.3f; // Tăng tốc độ lộn né của Boss
            stateMachine.Anim.applyRootMotion = false; // Bỏ qua Root Motion, lướt bằng code!
        }
    }

    public override void Tick(float deltaTime)
    {
        dodgeTimer -= deltaTime;
        
        if (stateMachine.Stats.isMiniBoss)
        {
            // Boss lướt đi siêu nhanh bằng code thay vì phụ thuộc vào Animation
            stateMachine.ManualVelocity = dodgeDirection * (stateMachine.Stats.dodgeSpeed * 2.5f);
        }
        else
        {
            stateMachine.ManualVelocity = Vector3.zero;
        }

        if (dodgeTimer <= 0f)
        {
            stateMachine.SwitchState(stateMachine.StrafeState);
        }
    }

    public override void Exit()
    {
        // 🟢 TẮT CỜ: ĐÃ LỘN XONG
        stateMachine.IsDodging = false;

        stateMachine.Anim.applyRootMotion = false;
        stateMachine.ManualVelocity = Vector3.zero;

        // 🟢 Tắt tàn ảnh và trả lại tốc độ bình thường cho Boss
        if (stateMachine.Stats.isMiniBoss)
        {
            var afterimage = stateMachine.GetComponent<AfterimageController>();
            if (afterimage != null) afterimage.StopTrail();
            stateMachine.Anim.speed = 1f;
        }
    }
}