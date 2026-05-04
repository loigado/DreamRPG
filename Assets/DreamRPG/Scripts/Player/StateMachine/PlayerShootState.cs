using UnityEngine;

public class PlayerShootState : PlayerBaseState
{
    private readonly int ShootHash = Animator.StringToHash("Bow_Shoot"); 
    private readonly int EmptyHash = Animator.StringToHash("Empty");
    private float stateTimer = 0f;

    public PlayerShootState(PlayerStateMachine stateMachine) : base(stateMachine) {}

    public override void Enter()
    {
        stateTimer = 0f;
        if (stateMachine.AimCamera != null) stateMachine.AimCamera.SetActive(true);

        if (stateMachine.virtualArrow != null) stateMachine.virtualArrow.SetActive(false);
        
        // Chạy animation bắn tức thì
        stateMachine.Animator.Play(ShootHash, 1, 0f);
        stateMachine.FireArrow();
        //if (stateMachine.bowStringScript != null) stateMachine.bowStringScript.isDrawing = false;
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);

        float targetYaw = stateMachine.MainCameraTransform.eulerAngles.y + stateMachine.CharacterRotationOffset;
        stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, Quaternion.Euler(0, targetYaw, 0), deltaTime * 15f);

        stateTimer += deltaTime;
        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(1);

        // Chờ 85% animation bắn hoặc failsafe 0.5s
        if ((stateInfo.shortNameHash == ShootHash && stateInfo.normalizedTime >= 0.85f) || stateTimer > 0.5f)
        {
            if (stateMachine.InputReader.IsHoldingAim)
            {
                stateMachine.isReloading = true; 
                stateMachine.SwitchState(new PlayerAimState(stateMachine));
            }
            else
            {
                stateMachine.isReloading = false;
                if (stateMachine.AimCamera != null) stateMachine.AimCamera.SetActive(false);
                
                stateMachine.Animator.Play(EmptyHash, 1, 0f);
                stateMachine.SwitchState(new PlayerMoveState(stateMachine));
            }
        }
    }

    public override void Exit()
    {
        if (stateMachine.bowStringScript != null) stateMachine.bowStringScript.isDrawing = false;
    }
}