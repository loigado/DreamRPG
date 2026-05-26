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
    private const float PARRY_WINDOW = 0.2f;      // 400ms perfect parry (GoW Normal)
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
            Debug.Log($"<color=cyan>🛡️ BLOCK STATE: Đang phát animation [{blockAnimName}] trên Layer 1</color>");

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
            stateMachine.SwitchState(new PlayerMovementState(stateMachine));
        }
    }

    /// <summary>
    /// Trả về true nếu đỡ/parry/vỡ thủ thành công (Player không mất HP).
    /// Trả về false nếu là Đòn Đỏ hoặc đánh sau lưng (Player mất HP).
    /// </summary>
    public bool HandleBlockedHit(float damage, Vector3 attackerPos, AttackGlint glint = AttackGlint.Normal)
    {
        // 🟢 1. NẾU LÀ ĐÒN ĐỎ -> XUYÊN THỦNG PHÒNG NGỰ
        if (glint == AttackGlint.Red)
        {
            Debug.Log("<color=red>❌ ĐÒN ĐỎ! Xuyên thủng mọi hàng phòng ngự!</color>");
            return false; // Trả về false để bắt Kratos ăn sát thương máu
        }

        // Kiểm tra góc đỡ đòn (Chống đánh lén từ sau lưng)
        Vector3 dirToAttacker = (attackerPos - stateMachine.transform.position).normalized;
        float angle = Vector3.Angle(stateMachine.transform.forward, dirToAttacker);
        if (angle > BLOCK_ANGLE_LIMIT) return false; 

        if (parryWindowActive && !didParry)
        {
            // 🟢 2. PERFECT PARRY THÀNH CÔNG! (Parry được cả đòn Xanh)
            didParry = true;

            Transform parriedEnemyTransform = null;
            Collider[] hits = Physics.OverlapSphere(stateMachine.transform.position, 4f, LayerMask.GetMask("Enemy"));
            foreach (var hit in hits)
            {
                EnemyStateMachine enemy = hit.GetComponentInParent<EnemyStateMachine>();
                if (enemy != null && enemy.currentState is EnemyAttackState)
                {
                    enemy.TriggerParryStagger();
                    if (parriedEnemyTransform == null) parriedEnemyTransform = enemy.transform;
                }
            }

            stateMachine.SwitchState(new PlayerParryDecisionState(stateMachine, parriedEnemyTransform));
            if (stateMachine.Stamina != null) stateMachine.Stamina.HealStamina(40f); 

            Debug.Log("<color=yellow>✨ PARRY THÀNH CÔNG! Đang chờ quyết định: Phản công hay Hút năng lượng?</color>");
            return true; 
        }

        // 🟢 3. NẾU LÀ ĐÒN XANH MÀ CHỈ GIỮ NÚT ĐỠ (BỎ LỠ PARRY) -> VỠ THỦ NGAY LẬP TỨC
        if (glint == AttackGlint.Blue)
        {
            Debug.Log("<color=orange>🛡️ VỠ THỦ! Đòn Xanh quá nặng, giữ thủ sẽ bị phá vỡ!</color>");
            TriggerGuardBreak(attackerPos);
            return true; // Tính là đã đỡ (không mất HP), nhưng bị phạt vỡ thủ (choáng)
        }

        // 🟢 4. ĐỠ ĐÒN BÌNH THƯỜNG (BLOCK)
        float reducedDamage = damage * (1f - BLOCK_DAMAGE_REDUCTION);
        if (stateMachine.PlayerHP != null)
            stateMachine.PlayerHP.TakeDamage(reducedDamage);

        stateMachine.Stamina.UseStamina(damage * 1.5f); 

        // 🟢 5. KIỂM TRA VỠ THỦ VÌ HẾT STAMINA
        if (!stateMachine.Stamina.HasEnoughStamina(0.1f))
        {
            Debug.Log("<color=orange>🛡️ VỠ THỦ! Cạn kiệt thể lực!</color>");
            TriggerGuardBreak(attackerPos);
            return true; 
        }

        // 🟢 6. CÒN THỂ LỰC -> GIẬT NHẸ RỒI ĐỠ TIẾP
        if (!string.IsNullOrEmpty(blockHitAnimName))
        {
            if (stateMachine.Animator.layerCount > 1) 
                stateMachine.Animator.CrossFadeInFixedTime(blockHitAnimName, 0.1f, 1);
            else 
                stateMachine.Animator.CrossFadeInFixedTime(blockHitAnimName, 0.1f, 0);
        }

        return true;
    }

    // Hàm phụ trợ xử lý Vỡ Thủ
    private void TriggerGuardBreak(Vector3 attackerPos)
    {
        if (stateMachine.ShieldVFX != null) stateMachine.ShieldVFX.SetActive(false);
        if (stateMachine.Animator.layerCount > 1) 
            stateMachine.Animator.CrossFadeInFixedTime(guardBreakAnimName, 0.1f, 1);
        else 
            stateMachine.Animator.CrossFadeInFixedTime(guardBreakAnimName, 0.1f, 0);

        stateMachine.SwitchState(new PlayerImpactState(stateMachine, attackerPos));
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