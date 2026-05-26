using UnityEngine;

/// <summary>
/// PlayerDeathState — Player chết: dừng mọi input, chạy animation, chờ respawn.
///
/// Tương đương: GoW "YOU DIED" / FF7R "GAME OVER" moment.
/// </summary>
public class PlayerDeathState : PlayerBaseState
{
    private float deathTimer = 3f;

    public PlayerDeathState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        // 1. Dừng mọi di chuyển
        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.SetBool("isPerformingAction", true);
        stateMachine.CurrentVelocity = Vector3.zero;
        stateMachine.VerticalVelocity = 0f;

        // 2. Lấy animation chết từ vũ khí
        string deathAnim = "Death";
        WeaponData weapon = stateMachine.CurrentWeapon;
        if (weapon != null && !string.IsNullOrEmpty(weapon.DeathAnimName))
        {
            deathAnim = weapon.DeathAnimName;
        }

        stateMachine.Animator.CrossFadeInFixedTime(deathAnim, 0.2f);

        // 3. Tắt input (Player không thể hành động khi chết)
        // InputReader vẫn enable nhưng state không xử lý input

        // 4. Tắt hitbox nếu đang bật
        stateMachine.DisableHitbox();

        // 5. Bật invulnerable để không bị đánh thêm
        stateMachine.EnableInvincibility();
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);

        // Giữ Player đứng yên
        Vector3 movement = Vector3.zero;
        movement.y = stateMachine.VerticalVelocity;
        stateMachine.Controller.Move(movement * deltaTime);

        deathTimer -= deltaTime;
        if (deathTimer <= 0f)
        {
            // Có thể trigger UI "Game Over" hoặc Respawn
            // Ví dụ: GameManager.Instance.ShowGameOverScreen();
            // Hoặc respawn tại checkpoint:
            // stateMachine.PlayerHP.Revive(0.5f);
            // stateMachine.DisableInvincibility();
            // stateMachine.SwitchState(new PlayerMovementState(stateMachine));
        }
    }

    public override void Exit()
    {
        stateMachine.Animator.SetBool("isPerformingAction", false);
    }
}
