using UnityEngine;

public abstract class PlayerBaseState : State
{
    protected PlayerStateMachine stateMachine;

    public PlayerBaseState(PlayerStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
    }

    protected void ApplyGravity(float deltaTime)
    {
        if (stateMachine.Controller.isGrounded && stateMachine.VerticalVelocity < 0)
        {
            // Lực bám đất chuẩn để không bị kẹt sàn gây rung
            stateMachine.VerticalVelocity = -2f; 
        }
        else
        {
            stateMachine.VerticalVelocity += stateMachine.Gravity * deltaTime;
        }
    }

    protected Vector3 CalculateMovement()
    {
        Vector3 cameraForward = stateMachine.MainCameraTransform.forward;
        cameraForward.y = 0; 
        cameraForward.Normalize();

        Vector3 cameraRight = stateMachine.MainCameraTransform.right;
        cameraRight.y = 0;
        cameraRight.Normalize();

        Vector2 input = stateMachine.InputReader.MovementValue;
        return (cameraForward * input.y + cameraRight * input.x).normalized;
    }

    /// <summary>
    /// 🟢 FIX: Đồng bộ vũ khí visual với dữ liệu CurrentWeapon.
    /// Gọi khi về MoveState để đảm bảo không bao giờ bị "tay không + animation sai".
    /// </summary>
    protected void SyncWeaponVisual()
    {
        if (stateMachine.weaponHolder == null) return;

        if (stateMachine.CurrentWeapon != null)
        {
            // Nếu đang có vũ khí data nhưng model chưa hiện → ép bật lên
            if (stateMachine.weaponHolder.CurrentWeaponModel == null 
                || !stateMachine.weaponHolder.CurrentWeaponModel.activeSelf)
            {
                stateMachine.weaponHolder.ActivateWeapon(stateMachine.CurrentWeapon.WeaponAnimID);
            }

            // Đồng bộ Animator WeaponID để animation đúng loại vũ khí
            stateMachine.Animator.SetInteger("WeaponID", stateMachine.CurrentWeapon.WeaponAnimID);
        }
        else
        {
            // Không có vũ khí → ẩn tất cả model
            stateMachine.weaponHolder.DeactivateAllWeapons();
            stateMachine.Animator.SetInteger("WeaponID", 0);
        }
    }
}