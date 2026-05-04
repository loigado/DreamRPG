using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    private PlayerStateMachine stateMachine;

    private void Awake()
    {
        stateMachine = GetComponentInParent<PlayerStateMachine>();
    }

    public void EnableWeaponHitbox()
    {
        if (stateMachine != null) stateMachine.EnableHitbox(); 
    }

    public void DisableWeaponHitbox()
    {
        if (stateMachine != null) stateMachine.DisableHitbox();
    }

    // 🟢 HÀM CŨ: Dành cho Skill 1 Phi Kiếm và Skill 1 Rìu Băng (Slam)
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

    // 🟢 HÀM MỚI: Dành cho Skill 2 Rìu Băng (Phát nổ Ultimate)
    public void ExecuteImpactAction()
    {
        if (stateMachine != null && stateMachine.currentState is PlayerIceAxeUltimateState ultState)
        {
            ultState.ExecuteImpactAction();
        }
    }

    // 🟢 HÀM MỚI: Dành cho Skill Nhảy bổ Rìu (Leap Slam)
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
            // Gọi hàm thực thi bên trong State đó
            swordState.ExecuteSkillAction(); 
        }
    }
    public void SetIKWeight(float weight)
    {
        if (stateMachine != null && stateMachine.FootIK != null)
        {
            stateMachine.FootIK.enabled = weight > 0;
        }
    }
}
