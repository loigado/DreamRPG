using UnityEngine;

public class PlayerIceAxeSlamState : PlayerBaseState
{
    private SkillData skill;
    private bool hasLaunched = false;

    public PlayerIceAxeSlamState(PlayerStateMachine stateMachine, SkillData skill) : base(stateMachine)
    {
        this.skill = skill;
    }

    public override void Enter()
    {
        hasLaunched = false;

        stateMachine.Stamina.UseStamina(skill.staminaCost);
        stateMachine.SkillManager.StartCooldown(skill);

        // 🟢 SUPER ARMOR: Kích hoạt I-frames chống ngắt chiêu
        stateMachine.EnableInvincibility();

        // 🟢 BẬT LẠI ROOT MOTION: Ủy quyền di chuyển (XZ) cho Animation
        stateMachine.Animator.applyRootMotion = true;
        
        // 🟢 XÓA QUÁN TÍNH NGANG TỪ STATE TRƯỚC:
        // Đảm bảo nhân vật không bị trượt do trớn lúc đang chạy, nhưng vẫn giữ trục Y (trọng lực rơi)
        stateMachine.CurrentVelocity = new Vector3(0, stateMachine.VerticalVelocity, 0);
        
        stateMachine.Animator.CrossFadeInFixedTime(skill.animationName, 0.1f);
        PlayerStateMachine.OnPlayerAttack?.Invoke(stateMachine.transform.position);
        stateMachine.Animator.SetBool("isPerformingAction", true);

        // 🎯 TỰ ĐIỀU HƯỚNG: Xoay mặt về phía quái khi bắt đầu đập
        if (stateMachine.TargetSys.GetCurrentTarget() != null)
        {
            stateMachine.TargetSys.FaceTarget(stateMachine.TargetSys.GetCurrentTarget().position, deltaTime: 0, instant: true);
        }
    }

    public override void Tick(float deltaTime)
    {
        // 🟢 GIỮ LẠI TRỌNG LỰC CỦA SCRIPT: 
        // Phải giữ dòng này để cập nhật biến stateMachine.VerticalVelocity
        // Hàm OnAnimatorMove sẽ lấy biến này để kéo nhân vật xuống đất nếu nhảy bổ rìu ở mép vực
        ApplyGravity(deltaTime);

        // KHÔNG GỌI lệnh Controller.Move() ở đây nữa.
        // Mọi di chuyển vật lý frame-by-frame đã được đẩy sang OnAnimatorMove() trong PlayerStateMachine.

        // 🟢 CANCEL WINDOW: Cho phép Roll để hủy động tác thu rìu thừa thãi
        if (stateMachine.InputReader.IsRolling)
        {
            stateMachine.SwitchState(new PlayerRollState(stateMachine));
            return; 
        }

        // 🟢 ĐIỀU KIỆN THOÁT STATE
        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName(skill.animationName) && stateInfo.normalizedTime >= 0.75f)
        {
            stateMachine.SwitchState(new PlayerMoveState(stateMachine));
        }
    }

    public void ExecuteSkillAction()
    {
        if (hasLaunched) return;
        hasLaunched = true;

        if (skill.vfxPrefab != null)
        {
            Vector3 spawnPos = stateMachine.transform.position + stateMachine.transform.forward * 1.5f;
            ObjectPoolManager.Instance.SpawnFromPool(skill.vfxPrefab, spawnPos, stateMachine.transform.rotation);

            // 🟢 TẠO VÙNG SÁT THƯƠNG HÌNH NÓN (CONE) CHUẨN XÁC
            float coneRadius = 5f; // Chiều dài của tia băng (tùy chỉnh cho vừa VFX)
            float coneAngle = 45f; // Góc mở sang 2 bên (45 độ = tổng góc quét là 90 độ)

            // Bước 1: Quét tất cả quái trong bán kính xung quanh
            Collider[] hits = Physics.OverlapSphere(stateMachine.transform.position, coneRadius, LayerMask.GetMask("Enemy"));
            
            foreach (var hit in hits)
            {
                // Bước 2: Tính hướng từ Kratos tới con quái
                Vector3 dirToEnemy = (hit.transform.position - stateMachine.transform.position).normalized;
                
                // Bước 3: Đo góc giữa hướng mặt của mình và hướng tới con quái
                float angleToEnemy = Vector3.Angle(stateMachine.transform.forward, dirToEnemy);

                // Bước 4: Chỉ những con nằm trong góc hình nón mới bị hất tung!
                if (angleToEnemy <= coneAngle)
                {
                    if (hit.TryGetComponent<EnemyHealth>(out EnemyHealth health))
                    {
                        float damage = (stateMachine.CurrentWeapon != null ? stateMachine.CurrentWeapon.Damage : 30f) * skill.damageMultiplier;
                        health.TakeDamage(damage, stateMachine.transform.position, 13f);
                        
                        // 🧊 ĐÓNG BĂNG: Quái dính chiêu sẽ bị đóng băng
                        // Nếu bị đánh tiếp khi đang đóng băng → VỠ BĂNG (Shatter) x2 sát thương!
                        health.ApplyFreeze(skill.freezeDuration);
                    }
                }
            }
        }
    }

    public override void Exit()
    {
        // 🟢 DỌN DẸP STATE: Cực kỳ quan trọng để chống lỗi trượt băng ở state tiếp theo
        stateMachine.DisableInvincibility();
        stateMachine.Animator.applyRootMotion = false; 
        stateMachine.Animator.SetBool("isPerformingAction", false);
    }
}