using UnityEngine;

public class PlayerEquipState : PlayerBaseState
{
    private string currentEquipAnim;
    private float timePassed;

    public PlayerEquipState(PlayerStateMachine stateMachine) : base(stateMachine) {}

    public override void Enter()
    {
        stateMachine.Animator.SetBool("isPerformingAction", true);

        currentEquipAnim = stateMachine.CurrentWeapon != null ? 
                           stateMachine.CurrentWeapon.EquipAnimName : "Unarmed_Equip";

        // 👉 ĐÃ SỬA: Đổi số 1 thành 0 để chạy trên Base Layer
        stateMachine.Animator.CrossFadeInFixedTime(currentEquipAnim, 0.15f, 0);
        timePassed = 0f;
    }

    public override void Tick(float deltaTime)
    {
        timePassed += deltaTime;
        ApplyGravity(deltaTime);

        Vector3 movement = CalculateMovement();
        float equipMoveSpeed = stateMachine.FreeLookMovementSpeed * 0.5f; 
        
        if (movement.sqrMagnitude > 0)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movement);
            stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRotation, deltaTime * stateMachine.RotationDamping);
        }
        
        stateMachine.Controller.Move((movement * equipMoveSpeed + new Vector3(0, stateMachine.VerticalVelocity, 0)) * deltaTime);

        // 👉 ĐÃ SỬA: Đổi số 1 thành 0 để check thời gian ở Base Layer
        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(0);

        if (stateInfo.IsName(currentEquipAnim))
        {
            if (stateInfo.normalizedTime >= 0.85f) 
            {
                stateMachine.SwitchState(new PlayerMoveState(stateMachine));
            }
        }
        else if (timePassed > 0.5f)
        {
            stateMachine.SwitchState(new PlayerMoveState(stateMachine));
        }
    }

    public override void Exit()
    {
        stateMachine.Animator.SetBool("isPerformingAction", false);

        // 🟢 FIX: Nếu bị gián đoạn giữa chừng (bị đánh), ép hoàn tất hiển thị vũ khí
        // Tránh bug "tay không + animation vũ khí cũ"
        if (stateMachine.CurrentWeapon != null && stateMachine.weaponHolder != null)
        {
            stateMachine.weaponHolder.ActivateWeapon(stateMachine.CurrentWeapon.WeaponAnimID);
        }
    }
}