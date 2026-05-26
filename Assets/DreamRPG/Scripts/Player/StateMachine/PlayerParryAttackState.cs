using UnityEngine;

/// <summary>
/// State xử lý đòn chém phản công sau khi Parry thành công.
/// Kratos sẽ lướt nhẹ tới trước và vung đòn cực mạnh.
/// Tích hợp cơ chế Attack Magnetism chuẩn AAA điều khiển bằng Animation Event.
/// </summary>
public class PlayerParryAttackState : PlayerBaseState
{
    private string attackAnimName;
    private Transform parriedEnemy; 
    public Transform ParriedEnemy => parriedEnemy;

    public PlayerParryAttackState(PlayerStateMachine stateMachine, Transform parriedEnemy = null) : base(stateMachine) 
    { 
        this.parriedEnemy = parriedEnemy;
    }

    public override void Enter()
    {
        stateMachine.Animator.applyRootMotion = true;
        stateMachine.Animator.SetBool("isPerformingAction", true);
        
        // Bất tử toàn diện trong lúc tung đòn phản công
        stateMachine.EnableInvincibility();

        // 🟢 Đảm bảo cờ trượt luôn tắt khi mới vào state để tránh lỗi kẹt trượt
        stateMachine.IsAttackSliding = false;

        // Đảm bảo thời gian đã trở lại bình thường
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        stateMachine.Animator.updateMode = AnimatorUpdateMode.Normal;

        // Lấy Animation chém phản công
        WeaponData weapon = stateMachine.CurrentWeapon;
        if (weapon != null)
        {
            if (!string.IsNullOrEmpty(weapon.ParryAttackAnimName))
                attackAnimName = weapon.ParryAttackAnimName;
            else if (!string.IsNullOrEmpty(weapon.CounterAnimName))
                attackAnimName = weapon.CounterAnimName;
            else
                attackAnimName = "Attack_Heavy_1";
        }
        else
        {
            attackAnimName = "Unarmed_Counter";
        }

        stateMachine.Animator.CrossFadeInFixedTime(attackAnimName, 0.1f, 0);

        // Tắt Layer Upper Body để diễn trọn vẹn Root Motion toàn thân
        if (stateMachine.Animator.layerCount > 1)
        {
            stateMachine.Animator.SetLayerWeight(1, 0f);
            stateMachine.Animator.CrossFadeInFixedTime("Empty", 0.05f, 1);
        }
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);

        Transform target = parriedEnemy != null ? parriedEnemy : 
                           (stateMachine.TargetSys != null ? stateMachine.TargetSys.GetCurrentTarget() : null);

        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(0);

        if (target != null)
        {
            Vector3 targetLookDir = target.position - stateMachine.transform.position;
            targetLookDir.y = 0;
            
            // 1. LUÔN XOAY MẶT VỀ PHÍA KẺ ĐỊCH
            if (targetLookDir.sqrMagnitude > 0.01f)
            {
                stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, Quaternion.LookRotation(targetLookDir), deltaTime * 15f);
            }

            // =========================================================
            // 🟢 LƯỚT MỤC TIÊU BẰNG ANIMATION EVENT
            // =========================================================
            if (stateInfo.IsName(attackAnimName) && stateMachine.IsAttackSliding)
            {
                float distanceToTarget = Vector3.Distance(stateMachine.transform.position, target.position);
                float optimalHitDistance = 1.8f; // Tầm chém lý tưởng

                if (distanceToTarget > optimalHitDistance)
                {
                    float gap = distanceToTarget - optimalHitDistance;
                    
                    // Tính vận tốc trượt mượt mà (Xa trượt nhanh, gần tự hãm phanh)
                    float dynamicSlideSpeed = gap * 15f; 
                    dynamicSlideSpeed = Mathf.Clamp(dynamicSlideSpeed, 2f, 20f); 

                    Vector3 slideDir = targetLookDir.normalized;
                    stateMachine.Controller.Move(slideDir * (dynamicSlideSpeed * deltaTime));
                }
            }
        }

        // CHUYỂN TRẠNG THÁI (STATE TRANSITIONS)
        if (stateInfo.IsName(attackAnimName))
        {
            // Cho phép nối combo chém thường hoặc lộn nhào sớm từ 60% animation
            if (stateInfo.normalizedTime >= 0.6f)
            {
                if (stateMachine.InputReader.BufferedInput == BufferedCommand.Attack)
                {
                    stateMachine.InputReader.ConsumeBuffer();
                    stateMachine.SwitchState(new PlayerAttackState(stateMachine, 0, false, false));
                    return;
                }

                if (stateMachine.InputReader.BufferedInput == BufferedCommand.Roll)
                {
                    if (stateMachine.Stamina != null && stateMachine.Stamina.HasEnoughStamina(25f)) 
                    {
                        stateMachine.InputReader.ConsumeBuffer(); 
                        stateMachine.SwitchState(new PlayerRollState(stateMachine));
                        return;
                    }
                }
            }

            // Kết thúc hoạt ảnh -> Trở về đi bộ bình thường
            if (stateInfo.normalizedTime >= 0.9f)
            {
                stateMachine.SwitchState(new PlayerMovementState(stateMachine));
            }
        }
    }

    public override void Exit()
    {
        // 🟢 Cực kỳ quan trọng: Tắt cờ khi thoát State để không lướt bậy ở State khác
        stateMachine.IsAttackSliding = false; 

        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.SetBool("isPerformingAction", false);
        
        stateMachine.DisableHitbox(); 
        stateMachine.DisableInvincibility();
    }
}