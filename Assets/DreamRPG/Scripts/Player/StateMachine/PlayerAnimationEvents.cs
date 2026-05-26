using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    private PlayerStateMachine stateMachine;
    private AfterimageController afterimageFX;

    private void Awake()
    {
        stateMachine = GetComponentInParent<PlayerStateMachine>();
        afterimageFX = GetComponentInParent<AfterimageController>();
    }

    public void EnableWeaponHitbox()
    {
        if (stateMachine != null) stateMachine.EnableHitbox(); 
    }

    public void DisableWeaponHitbox()
    {
        if (stateMachine != null) stateMachine.DisableHitbox();
    }

    // 🟢 Dành cho Skill 1 Phi Kiếm và Skill 1 Rìu Băng (Slam)
    public void ExecuteSkillAction()
    {
        if (stateMachine == null) return;

        if (stateMachine.currentState is PlayerIceAxeSlamState axeSlamState)
        {
            axeSlamState.ExecuteSkillAction();
        }
        else if (stateMachine.currentState is PlayerWarpStrikeState warpState)
        {
            warpState.ExecuteSkillAction();
        }
    }

    // 🟢 Dành cho Skill 2 Rìu Băng (Phát nổ Ultimate)
    public void ExecuteImpactAction()
    {
        if (stateMachine != null && stateMachine.currentState is PlayerIceAxeUltimateState ultState)
        {
            ultState.ExecuteImpactAction();
        }
    }

    // 🟢 Dành cho Skill Nhảy bổ Rìu (Leap Slam)
    public void ExecuteLeapSlam()
    {
        if (stateMachine != null && stateMachine.currentState is PlayerIceAxeLeapSlamState leapSlamState)
        {
            leapSlamState.ExecuteLeapSlam();
        }
    }

    public void AE_FireSkillProjectile()
    {
        if (stateMachine != null && stateMachine.currentState is PlayerPhantomArrayState phantomState)
        {
            phantomState.FirePlayerProjectile();
        }
    }

    public void TriggerSkillAction()
    {
        if (stateMachine == null) return;
        if (stateMachine.currentState is PlayerWarpStrikeState swordState)
        {
            swordState.ExecuteSkillAction(); 
        }
    }

    public void SetIKWeight(float weight)
    {
        // Chừa sẵn cho hệ thống Foot IK sau này
        //if (stateMachine != null && stateMachine.FootIK != null)
        //{
        //    stateMachine.FootIK.enabled = weight > 0;
        //}
    }

    // ==========================================
    // 1. GỌI Ở FRAME 1 (HOẶC 2): BẬT TẤT CẢ (IFRAME)
    // ==========================================
    public void EnableIFrame()
    {
        if (stateMachine != null) 
        {
            // LỚP BẢO VỆ: Chặn đứng lỗi "Bóng ma Transition"
            if (stateMachine.currentState is PlayerRollState rollState)
            {
                stateMachine.EnableInvincibility(); 
                rollState.IsPerfectDodgeWindow = true; 
            }
        }
    }

    // ==========================================
    // 2. GỌI Ở FRAME 5 (HOẶC 6): ĐÓNG PERFECT DODGE WINDOW
    // ==========================================
    public void ClosePerfectDodgeWindow()
    {
        if (stateMachine != null && stateMachine.currentState is PlayerRollState rollState) 
        {
            rollState.IsPerfectDodgeWindow = false; 
        }
    }

    // ==========================================
    // 3. GỌI Ở KHOẢNG 30%-40% ANIMATION: TẮT BẤT TỬ
    // ==========================================
    public void DisableIFrame()
    {
        if (stateMachine != null) 
        {
            stateMachine.DisableInvincibility(); 
            
            if (stateMachine.currentState is PlayerRollState rollState)
            {
                rollState.IsPerfectDodgeWindow = false; 
            }
        }

        if (afterimageFX != null)
        {
            afterimageFX.StopTrail();
        }
    }

    // ==========================================================
    // 4. ANIMATION EVENTS CHO ATTACK MAGNETISM (ÁP SÁT KẺ ĐỊCH)
    // ==========================================================
    public void AE_StartAttackSlide()
    {
        if (stateMachine != null) 
        {
            stateMachine.IsAttackSliding = true;
        }
    }

    public void AE_StopAttackSlide()
    {
        if (stateMachine != null) 
        {
            stateMachine.IsAttackSliding = false;
        }
    }
}