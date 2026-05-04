using UnityEngine;

/// <summary>
/// PlayerBlockState — Giơ khiên/vũ khí đỡ đòn (GoW Shield Block).
///
/// Nhấn giữ Block → đỡ đòn, giảm sát thương.
/// Nhấn Block đúng timing → Parry (counter-attack window).
///
/// Tích hợp:
///   • Perfect Parry window (0.2s đầu)
///   • Stamina drain khi đỡ
///   • Guard Break khi hết Stamina
///   • Di chuyển chậm khi block
/// </summary>
public class PlayerBlockState : PlayerBaseState
{
    private string blockAnimName;
    private string parryAnimName;
    private string guardBreakAnimName;
    private string blockHitAnimName;

    private readonly int FreeLookSpeedHash = Animator.StringToHash("FreeLookSpeed");
    private readonly int InputXHash = Animator.StringToHash("InputX");
    private readonly int InputYHash = Animator.StringToHash("InputY");
    private readonly int IsStrafeHash = Animator.StringToHash("IsStrafing");

    private float stateTimer = 0f;
    private bool parryWindowActive = true;
    private bool didParry = false;
    private bool wasHardLocking = false; // 🟢 FIX: Giữ lại để biết lúc nào đổi locomotion

    // Tuning
    private const float PARRY_WINDOW = 0.2f;      // 200ms perfect parry
    private const float BLOCK_STAMINA_DRAIN = 5f;  // Stamina/giây khi giữ block
    private const float BLOCK_DAMAGE_REDUCTION = 0.8f; // Giảm 80% damage
    private const float BLOCK_MOVE_SPEED = 0.3f;   // 30% tốc độ
    private const float BLOCK_ANGLE_LIMIT = 100f;  // Quái đánh ngoài góc 100 độ (sau lưng) sẽ bị dính đòn

    public PlayerBlockState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        // Lấy tên Animation riêng biệt cho từng loại vũ khí
        if (stateMachine.CurrentWeapon != null)
        {
            blockAnimName = stateMachine.CurrentWeapon.BlockAnimName;
            parryAnimName = stateMachine.CurrentWeapon.ParryAnimName;
            guardBreakAnimName = stateMachine.CurrentWeapon.GuardBreakAnimName;
            blockHitAnimName = stateMachine.CurrentWeapon.BlockHitAnimName;
        }
        else
        {
            // Dự phòng nếu không có vũ khí
            blockAnimName = "Block_Idle";
            parryAnimName = "Parry";
            guardBreakAnimName = "GuardBreak";
            blockHitAnimName = "Block_Hit";
        }

        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.SetBool("isPerformingAction", true);

        // 🟢 FIX: Kiểm tra trạng thái Lock ngay khi bấm Đỡ đòn
        wasHardLocking = stateMachine.TargetSys != null && stateMachine.TargetSys.IsHardLocking && stateMachine.TargetSys.GetCurrentTarget() != null;

        // 🟢 BẬT KHIÊN VFX (KRATOS STYLE)
        if (stateMachine.ShieldVFX != null)
        {
            stateMachine.ShieldVFX.SetActive(true);

            // 🟢 Thay đổi Material (Nguyên tố) của Khiên tùy theo vũ khí đang cầm
            if (stateMachine.CurrentWeapon != null && stateMachine.CurrentWeapon.ShieldMaterial != null)
            {
                Renderer[] renderers = stateMachine.ShieldVFX.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    r.material = stateMachine.CurrentWeapon.ShieldMaterial;
                }
            }
        }

        if (stateMachine.Animator.layerCount > 1)
        {
            stateMachine.Animator.SetLayerWeight(1, 1f);
            stateMachine.Animator.CrossFadeInFixedTime(blockAnimName, 0.1f, 1); 

            // Chạy locomotion đúng loại
            string locoName = wasHardLocking ? "Combat_Locomotion" : (stateMachine.CurrentWeapon != null ? stateMachine.CurrentWeapon.LocomotionStateName : "Unarmed_Locomotion");
            stateMachine.Animator.CrossFadeInFixedTime(locoName, 0.1f, 0);
        }
        else
        {
            stateMachine.Animator.CrossFadeInFixedTime(blockAnimName, 0.1f, 0);
        }

        stateTimer = 0f;
        parryWindowActive = true;
        didParry = false;

        // Subscribe damage override
        stateMachine.InputReader.RollEvent += OnRollWhileBlocking;
    }

    public override void Tick(float deltaTime)
    {
        stateTimer += deltaTime;
        ApplyGravity(deltaTime);

        // 1. PARRY WINDOW (0.2s đầu)
        if (stateTimer > PARRY_WINDOW)
            parryWindowActive = false;

        // 2. STAMINA DRAIN
        stateMachine.Stamina.UseStamina(BLOCK_STAMINA_DRAIN * deltaTime);

        // 3. GUARD BREAK — hết stamina → choáng
        if (!stateMachine.Stamina.HasEnoughStamina(0.1f))
        {
            // Tắt khiên nếu bị vỡ phòng thủ
            if (stateMachine.ShieldVFX != null)
            {
                stateMachine.ShieldVFX.SetActive(false);
            }

            stateMachine.Animator.CrossFadeInFixedTime(guardBreakAnimName, 0.1f);
            stateMachine.SwitchState(new PlayerImpactState(stateMachine, stateMachine.transform.position));
            return;
        }

        // 4. DI CHUYỂN CHẬM
        Vector3 movement = CalculateMovement();
        bool isHardLocking = stateMachine.TargetSys != null && stateMachine.TargetSys.IsHardLocking && stateMachine.TargetSys.GetCurrentTarget() != null;
        
        // Không dùng C# ép CrossFade nữa, để Animator tự xử lý bằng biến IsStrafing
        /*
        if (isHardLocking != wasHardLocking)
        {
            wasHardLocking = isHardLocking;
        }
        */

        if (isHardLocking)
        {
            Vector3 dirToEnemy = (stateMachine.TargetSys.GetCurrentTarget().position - stateMachine.transform.position).normalized;
            dirToEnemy.y = 0;
            Vector2 input = stateMachine.InputReader.MovementValue;
            movement = (dirToEnemy * input.y + Vector3.Cross(Vector3.up, dirToEnemy) * input.x).normalized;
        }

        float speed = stateMachine.FreeLookMovementSpeed * BLOCK_MOVE_SPEED;
        Vector3 moveVelocity = movement * speed;
        moveVelocity.y = stateMachine.VerticalVelocity;
        stateMachine.Controller.Move(moveVelocity * deltaTime);

        float turnInput = 0f;

        if (isHardLocking)
        {
            Vector3 targetLookDir = stateMachine.TargetSys.GetCurrentTarget().position - stateMachine.transform.position;
            targetLookDir.y = 0;
            if (targetLookDir.sqrMagnitude > 0.01f)
            {
                float angleDelta = Vector3.SignedAngle(stateMachine.transform.forward, targetLookDir.normalized, Vector3.up);
                
                // Nếu đứng im và quái chạy vòng quanh -> Kích hoạt xoay người
                if (movement.sqrMagnitude < 0.01f && Mathf.Abs(angleDelta) > 5f)
                {
                    turnInput = Mathf.Sign(angleDelta) * 0.5f; // 0.5 là xoay phải, -0.5 là xoay trái
                }

                Quaternion targetRot = Quaternion.LookRotation(targetLookDir.normalized);
                stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRot, deltaTime * 8f);
            }
        }
        else
        {
            Vector3 cameraForward = stateMachine.MainCameraTransform.forward;
            cameraForward.y = 0;
            if (cameraForward.sqrMagnitude > 0.01f)
            {
                float angleDelta = Vector3.SignedAngle(stateMachine.transform.forward, cameraForward.normalized, Vector3.up);
                
                // Nếu đứng im và xoay camera -> Kích hoạt xoay người
                if (movement.sqrMagnitude < 0.01f && Mathf.Abs(angleDelta) > 5f)
                {
                    turnInput = Mathf.Sign(angleDelta) * 0.5f;
                }

                // CHỈ xoay Transform khi có di chuyển HOẶC khi camera bị lệch quá 5 độ
                if (movement.sqrMagnitude > 0.01f || Mathf.Abs(angleDelta) > 5f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(cameraForward.normalized);
                    stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRot, deltaTime * 8f);
                }
            }
        }

        // 5. XOAY HƯỚNG VÀ TRUYỀN ANIMATION PARAMETERS (LUÔN BẬT STRAFE)
        stateMachine.Animator.SetBool(IsStrafeHash, true);
        stateMachine.Animator.SetFloat(FreeLookSpeedHash, 0f, 0.1f, deltaTime);

        Vector2 rawInput = stateMachine.InputReader.MovementValue;
        
        // 🟢 Bơm giá trị Turn vào InputX nếu đang đứng im
        if (movement.sqrMagnitude < 0.01f)
        {
            rawInput.x = turnInput;
        }

        stateMachine.Animator.SetFloat(InputXHash, rawInput.x, 0.1f, deltaTime);
        stateMachine.Animator.SetFloat(InputYHash, rawInput.y, 0.1f, deltaTime);

        // 6. THẢ NÚT BLOCK → về MoveState
        if (!stateMachine.InputReader.IsHoldingBlock)
        {
            stateMachine.SwitchState(new PlayerMoveState(stateMachine));
        }
    }

    /// <summary>
    /// Gọi từ bên ngoài khi Player bị đánh trong lúc Block.
    /// Trả về true nếu đỡ được (hoặc parry), false nếu guard break/đánh sau lưng.
    /// </summary>
    public bool HandleBlockedHit(float damage, Vector3 attackerPos)
    {
        // 🟢 FIX: Kiểm tra hướng tấn công. Đánh sau lưng -> Vỡ block!
        Vector3 attackDir = (attackerPos - stateMachine.transform.position).normalized;
        attackDir.y = 0; // Bỏ qua trục Y
        float angle = Vector3.Angle(stateMachine.transform.forward, attackDir);

        if (angle > BLOCK_ANGLE_LIMIT)
        {
            Debug.Log($"<color=red>💥 Bị đánh lén từ phía sau! Góc: {angle}</color>");
            // Mất máu toàn phần + dính hiệu ứng bị đánh văng
            if (stateMachine.PlayerHP != null) stateMachine.PlayerHP.TakeDamage(damage);
            stateMachine.SwitchState(new PlayerImpactState(stateMachine, attackerPos));
            return false;
        }

        if (parryWindowActive && !didParry)
        {
            // PERFECT PARRY!
            didParry = true;
            if (stateMachine.Animator.layerCount > 1) stateMachine.Animator.CrossFadeInFixedTime(parryAnimName, 0.05f, 1);
            else stateMachine.Animator.CrossFadeInFixedTime(parryAnimName, 0.05f, 0);
            return true; 
        }

        // Block bình thường — giảm damage
        float reducedDamage = damage * (1f - BLOCK_DAMAGE_REDUCTION);
        if (stateMachine.PlayerHP != null)
            stateMachine.PlayerHP.TakeDamage(reducedDamage);

        // Drain thêm stamina
        stateMachine.Stamina.UseStamina(damage * 0.3f);

        // 🟢 FIX: Chạy animation Block Hit (giật nhẹ)
        if (!string.IsNullOrEmpty(blockHitAnimName))
        {
            if (stateMachine.Animator.layerCount > 1) stateMachine.Animator.CrossFadeInFixedTime(blockHitAnimName, 0.1f, 1);
            else stateMachine.Animator.CrossFadeInFixedTime(blockHitAnimName, 0.1f, 0);
        }

        return true;
    }

    private void OnRollWhileBlocking()
    {
        if (stateMachine.Stamina.HasEnoughStamina(25f))
            stateMachine.SwitchState(new PlayerRollState(stateMachine));
    }

    public override void Exit()
    {
        // 🟢 TẮT KHIÊN VFX
        if (stateMachine.ShieldVFX != null)
        {
            stateMachine.ShieldVFX.SetActive(false);
        }

        stateMachine.Animator.SetBool("isPerformingAction", false);
        stateMachine.InputReader.RollEvent -= OnRollWhileBlocking;

        // 🟢 Tắt UpperBodyLayer khi thoát khỏi Block
        if (stateMachine.Animator.layerCount > 1)
        {
            stateMachine.Animator.SetLayerWeight(1, 0f);
            stateMachine.Animator.CrossFadeInFixedTime("Empty", 0.1f, 1);
        }
    }
}
