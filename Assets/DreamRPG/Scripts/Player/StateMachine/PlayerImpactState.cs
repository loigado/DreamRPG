using UnityEngine;

/// <summary>
/// PlayerImpactState — AAA Sekiro/Elden Ring Style
/// Chỉ Auto-tracking khi Lock-on. Có phân biệt hướng đánh (Trái/Phải/Trước/Sau).
/// Kèm vật lý trượt đất và ngã (GetUp).
/// </summary>
public class PlayerImpactState : PlayerBaseState
{
    private float duration = 0.5f; 
    private Vector3 appliedVelocity; 
    private bool heavyKnockback;     
    
    // 🟢 BIẾN MỚI: Lưu hướng mục tiêu để xoay mượt mà
    private Quaternion targetRotation;

    public PlayerImpactState(PlayerStateMachine stateMachine, Vector3 attackerPos) : base(stateMachine) 
    {
        this.heavyKnockback = false; 
        Vector3 pushDir = (stateMachine.transform.position - attackerPos);
        pushDir.y = 0; 
        this.appliedVelocity = pushDir.normalized * 5f; 
    }

    public PlayerImpactState(PlayerStateMachine stateMachine, Vector3 knockbackForce, bool isHeavy) : base(stateMachine)
    {
        this.heavyKnockback = isHeavy; 
        this.appliedVelocity = knockbackForce; 
        if (isHeavy) this.duration = 1.0f; 
    }

    public override void Enter()
    {
        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.SetBool("isPerformingAction", true);
        
        // ── 1. TÍNH TOÁN HƯỚNG BỊ ĐÁNH & SOULS-LIKE LOCK-ON ──────────────
        Vector3 dirToAttacker = -appliedVelocity; 
        dirToAttacker.y = 0; 

        // Mặc định: Không xoay mặt (Giữ nguyên hướng hiện tại)
        targetRotation = stateMachine.transform.rotation;

        if (dirToAttacker.sqrMagnitude > 0.1f)
        {
            float angle = Vector3.Angle(stateMachine.transform.forward, dirToAttacker.normalized);
            
            // Dùng Tích có hướng (Cross Product) để xác định kẻ địch bên trái hay phải
            Vector3 cross = Vector3.Cross(stateMachine.transform.forward, dirToAttacker.normalized);
            bool isAttackerOnRight = cross.y > 0;

            // 🟢 Kiểm tra xem Player có đang Lock-on không?
            bool isLockedOn = stateMachine.TargetSys != null && stateMachine.TargetSys.IsHardLocking;

            // SOULS-LIKE LOGIC: Chỉ Slerp mặt về phía địch nếu ĐANG LOCK-ON và KHÔNG bị đánh từ sau lưng
            if (isLockedOn && angle <= 120f)
            {
                targetRotation = Quaternion.LookRotation(dirToAttacker.normalized);
                Debug.Log("<color=cyan>Đang Lock-on: Tự động xoay mặt về phía kẻ địch!</color>");
            }
            else if (!isLockedOn)
            {
                Debug.Log("<color=grey>Free Camera: Bị hất văng tự do, không tự xoay mặt!</color>");
            }

            // GỢI Ý HOẠT ẢNH TƯƠNG LAI (Dành cho lúc bạn lên Mixamo tải Animation)
            if (angle > 120f) {
                // Sẽ chạy Anim: Hit_Back (Ngã chúi về trước)
            } else if (angle > 45f) {
                if (isAttackerOnRight) {
                    // Sẽ chạy Anim: Hit_Right (Bị tát từ bên phải, lảo đảo sang trái)
                } else {
                    // Sẽ chạy Anim: Hit_Left (Bị tát từ bên trái, lảo đảo sang phải)
                }
            } else {
                // Sẽ chạy Anim: Hit_Front (Ngã ngửa ra sau)
            }
        }

        // ── 2. CHỌN ANIMATION TỪ VŨ KHÍ ──────────────────────────────
        string hitAnim = "Impact";
        string heavyHitAnim = "KnockbackFar";

        WeaponData weapon = stateMachine.CurrentWeapon;
        if (weapon != null)
        {
            if (!string.IsNullOrEmpty(weapon.HitAnimName)) hitAnim = weapon.HitAnimName;
            if (!string.IsNullOrEmpty(weapon.HeavyHitAnimName)) heavyHitAnim = weapon.HeavyHitAnimName;
        }

        if (heavyKnockback)
        {
            stateMachine.Animator.CrossFadeInFixedTime(heavyHitAnim, 0.1f);
        }
        else
        {
            stateMachine.Animator.CrossFadeInFixedTime(hitAnim, 0.1f);
        }

        // ── 3. VẬT LÝ TRƯỢT ĐẤT ─────────────────────────────────────
        stateMachine.VerticalVelocity = -2f; 
        Vector3 horizontalForce = appliedVelocity;
        horizontalForce.y = 0;
        stateMachine.CurrentVelocity = horizontalForce;
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);
        
        // Slerp xoay mặt. (Nếu không Lock-on, targetRotation = rotation hiện tại -> Không xoay)
        stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRotation, deltaTime * 12f);
        
        // Ma sát trượt lùi trên mặt đất
        stateMachine.CurrentVelocity = Vector3.Lerp(stateMachine.CurrentVelocity, Vector3.zero, deltaTime * 3f);
        
        Vector3 movement = stateMachine.CurrentVelocity;
        movement.y = stateMachine.VerticalVelocity;
        stateMachine.Controller.Move(movement * deltaTime);

        duration -= deltaTime;
        if (duration <= 0)
        {
            // 🟢 LƯU Ý: Phục hồi lại chuyển State GetUp cho các đòn hất văng mạnh!
            if (heavyKnockback)
            {
                stateMachine.SwitchState(new PlayerGetUpState(stateMachine));
            }
            else
            {
                stateMachine.SwitchState(new PlayerMovementState(stateMachine));
            }
        }
    }

    public override void Exit()
    {
        // GetUpState sẽ tự lo việc tắt cờ và mở I-Frame. Ở đây chỉ xử lý cho đòn nhẹ.
        if (!heavyKnockback) 
        {
            stateMachine.Animator.SetBool("isPerformingAction", false);
            stateMachine.StartHitRecoveryIFrame();
        }
    }
}