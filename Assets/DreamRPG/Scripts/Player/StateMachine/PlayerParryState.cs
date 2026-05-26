using UnityEngine;

public class PlayerParryState : PlayerBaseState
{
    private string parryAnimName;
    private bool isFinishing = false;

    private float timer = 0f;

    public PlayerParryState(PlayerStateMachine stateMachine, string parryAnim) : base(stateMachine)
    {
        this.parryAnimName = parryAnim;
    }

    public override void Enter()
    {
        // Phát Animation Parry
        if (stateMachine.Animator.layerCount > 1) 
            stateMachine.Animator.CrossFadeInFixedTime(parryAnimName, 0.05f, 1);
        else 
            stateMachine.Animator.CrossFadeInFixedTime(parryAnimName, 0.05f, 0);

        // Bật khiên VFX
        if (stateMachine.ShieldVFX != null)
        {
            stateMachine.ShieldVFX.SetActive(true);
            if (stateMachine.CurrentWeapon != null && stateMachine.CurrentWeapon.ShieldMaterial != null)
            {
                Renderer[] renderers = stateMachine.ShieldVFX.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers) r.material = stateMachine.CurrentWeapon.ShieldMaterial;
            }
        }
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);
        timer += deltaTime;

        // Dừng di chuyển
        stateMachine.RootMotionMultiplier = 0f;

        // Xoay mặt về hướng mục tiêu gần nhất nếu có soft-lock
        if (stateMachine.TargetSys != null)
        {
            Transform target = stateMachine.TargetSys.GetCurrentTarget();
            if (target != null)
            {
                stateMachine.TargetSys.FaceTarget(target.position, deltaTime, false);
            }
        }

        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(0);
        // Điều kiện thoát State: Hoặc diễn xong Animation, hoặc hết 0.8s (phòng trường hợp Animator bị kẹt)
        bool isAnimationDone = stateInfo.IsName(parryAnimName) && stateInfo.normalizedTime >= 0.8f;
        bool isTimeExpired = timer >= 0.8f;

        if ((isAnimationDone || isTimeExpired) && !isFinishing)
        {
            isFinishing = true;
            
            // Trở về Block nếu người chơi vẫn đang giữ nút Đỡ
            if (stateMachine.InputReader.IsHoldingBlock)
            {
                stateMachine.SwitchState(new PlayerBlockState(stateMachine));
            }
            else
            {
                stateMachine.SwitchState(new PlayerMovementState(stateMachine));
            }
        }
    }

    public override void Exit()
    {
        stateMachine.RootMotionMultiplier = 1f;
        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.SetBool("isPerformingAction", false);

        if (stateMachine.ShieldVFX != null)
        {
            stateMachine.ShieldVFX.SetActive(false);
        }
    }
}
