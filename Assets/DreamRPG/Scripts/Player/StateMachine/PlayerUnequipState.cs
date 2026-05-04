using UnityEngine;

public class PlayerUnequipState : PlayerBaseState
{
    private WeaponData nextWeapon;
    private string currentUnequipAnim;
    private float timePassed;

    public PlayerUnequipState(PlayerStateMachine stateMachine, WeaponData nextWeapon) : base(stateMachine)
    {
        this.nextWeapon = nextWeapon;
    }

    public override void Enter()
    {
        stateMachine.Animator.SetBool("isPerformingAction", true);

        currentUnequipAnim = stateMachine.CurrentWeapon != null ? 
                             stateMachine.CurrentWeapon.UnequipAnimName : "Unarmed_Unequip";

        // 👉 ĐÃ SỬA: Đổi số 1 thành 0 để chạy trên Base Layer
        stateMachine.Animator.CrossFadeInFixedTime(currentUnequipAnim, 0.15f, 0);
        timePassed = 0f;
    }

    public override void Tick(float deltaTime)
    {
        timePassed += deltaTime;
        ApplyGravity(deltaTime);

        Vector3 movement = CalculateMovement();
        float unequipMoveSpeed = stateMachine.FreeLookMovementSpeed * 0.5f; 
        
        if (movement.sqrMagnitude > 0)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movement);
            stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRotation, deltaTime * stateMachine.RotationDamping);
        }
        
        stateMachine.Controller.Move((movement * unequipMoveSpeed + new Vector3(0, stateMachine.VerticalVelocity, 0)) * deltaTime);

        // 👉 ĐÃ SỬA: Đổi số 1 thành 0
        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(0);

        if (stateInfo.IsName(currentUnequipAnim))
        {
            if (stateInfo.normalizedTime >= 0.85f) 
            {
                stateMachine.EquipWeaponDataOnly(nextWeapon);
                stateMachine.SwitchState(new PlayerEquipState(stateMachine));
            }
        }
        else if (timePassed > 0.5f)
        {
            stateMachine.EquipWeaponDataOnly(nextWeapon);
            stateMachine.SwitchState(new PlayerEquipState(stateMachine));
        }
    }

    public override void Exit()
    {
        stateMachine.Animator.SetBool("isPerformingAction", false);

        // 🟢 FIX: Nếu bị gián đoạn giữa chừng (bị đánh), ép hoàn tất đổi vũ khí
        // Tránh bug "tay không + animation vũ khí cũ"
        if (nextWeapon != null)
        {
            stateMachine.EquipWeaponDataOnly(nextWeapon);
            if (stateMachine.weaponHolder != null)
            {
                stateMachine.weaponHolder.ActivateWeapon(nextWeapon.WeaponAnimID);
            }
        }
    }
}