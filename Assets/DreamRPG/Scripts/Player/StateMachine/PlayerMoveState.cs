using UnityEngine;
using Unity.Cinemachine; 

public class PlayerMovementState : PlayerBaseState
{
    private readonly int FreeLookSpeedHash = Animator.StringToHash("FreeLookSpeed");
    private readonly int InputXHash = Animator.StringToHash("InputX");
    private readonly int InputYHash = Animator.StringToHash("InputY");
    private readonly int IsStrafeHash = Animator.StringToHash("IsStrafing");

    private const float AnimatorDampTime = 0.2f;
    private float fallTimer = 0f;
    private bool wasHardLocking = false; 

    public PlayerMovementState(PlayerStateMachine stateMachine) : base(stateMachine) {}

    public override void Enter()
    {
        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.SetBool("isPerformingAction", false);
        
        // Khởi tạo trạng thái Lock ban đầu
        wasHardLocking = stateMachine.TargetSys != null && stateMachine.TargetSys.IsHardLocking && stateMachine.TargetSys.GetCurrentTarget() != null;
        
        string locoName = stateMachine.CurrentWeapon != null ? stateMachine.CurrentWeapon.LocomotionStateName : "Unarmed_Locomotion";
        stateMachine.Animator.CrossFadeInFixedTime(locoName, 0.2f);
        

        // Reset vận tốc rơi khi vào MoveState
        stateMachine.VerticalVelocity = -2f;
        fallTimer = 0f;

        // Đồng bộ vũ khí visual mỗi khi về MoveState
        SyncWeaponVisual();

        stateMachine.InputReader.RollEvent += OnRoll;
        stateMachine.InputReader.AttackEvent += OnAttack;

        stateMachine.InputReader.Skill1Event += OnSkill1;
        stateMachine.InputReader.Skill2Event += OnSkill2;
        stateMachine.InputReader.Skill3Event += OnSkill3;
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime); 

        // Tính toán di chuyển thông thường dựa theo hướng Camera
        Vector3 movement = CalculateMovement(); 
        bool isHardLocking = stateMachine.TargetSys != null && stateMachine.TargetSys.IsHardLocking && stateMachine.TargetSys.GetCurrentTarget() != null;

        bool isTryingToSprint = stateMachine.InputReader.IsSprinting;
        bool isMoving = movement.sqrMagnitude > 0;

        float targetSpeed = stateMachine.FreeLookMovementSpeed;
        bool isSprinting = false;
        
        // Vẫn cho phép Sprint khi Lock (chạy nhanh vòng quanh quái)
        if (isMoving && isTryingToSprint && stateMachine.Stamina.HasEnoughStamina(1f))
        {
            targetSpeed = stateMachine.SprintSpeed;
            isSprinting = true;
        }

        Vector3 targetVelocity = movement * targetSpeed;

        if (isMoving)
        {
            if (isSprinting) stateMachine.Stamina.UseStamina(10f * deltaTime);

            // Luôn xoay mặt theo hướng di chuyển
            FaceMovementDirection(movement, deltaTime);
            
            float targetAnim = isSprinting ? 2f : 1f;
            stateMachine.Animator.SetFloat(FreeLookSpeedHash, targetAnim, AnimatorDampTime, deltaTime);
        }
        else
        {
            stateMachine.Animator.SetFloat(FreeLookSpeedHash, 0f, AnimatorDampTime, deltaTime);
        }

        // ÉP Animator hiểu là ĐANG KHÔNG STRAFE (Tắt Blend Tree 2D đi 4 hướng)
        stateMachine.Animator.SetBool(IsStrafeHash, false);
        stateMachine.Animator.SetFloat(InputXHash, 0, AnimatorDampTime, deltaTime);
        stateMachine.Animator.SetFloat(InputYHash, 0, AnimatorDampTime, deltaTime);

        stateMachine.CurrentVelocity = Vector3.Lerp(stateMachine.CurrentVelocity, targetVelocity, deltaTime * 10f);
        
        // 🟢 FIX 1: Ép vận tốc chạy dọc theo sườn dốc
        Vector3 finalMovement = AdjustVelocityToSlope(stateMachine.CurrentVelocity);

        // 🟢 FIX 2: Ép lực dính đất (Stick to ground) chống cà giật
        if (stateMachine.Controller.isGrounded && finalMovement.y <= 0)
        {
            stateMachine.VerticalVelocity = -5f; // Lực ghim chân mạnh hơn
            finalMovement.y += stateMachine.VerticalVelocity; 
        }
        else
        {
            finalMovement.y = stateMachine.VerticalVelocity;
        }

        stateMachine.Controller.Move(finalMovement * deltaTime);

        if (stateMachine.InputReader.IsJumping) 
        { 
            if (stateMachine.Stamina.HasEnoughStamina(15f)) 
            {
                stateMachine.SwitchState(new PlayerJumpState(stateMachine)); 
            }
            return; 
        }

        // Fall detection cải tiến — chống loop
        if (!stateMachine.Controller.isGrounded)
        {
            fallTimer += deltaTime;
            if (fallTimer > 0.3f && stateMachine.VerticalVelocity < -3f) 
            { 
                stateMachine.SwitchState(new PlayerFallState(stateMachine)); 
                return; 
            }
        }
        else 
        {
            fallTimer = 0f;
        }

        if (stateMachine.InputReader.IsHoldingAim && stateMachine.CurrentWeapon != null && stateMachine.CurrentWeapon.Type == WeaponType.Ranged)
        {
            if (stateMachine.TargetSys != null) 
            {
                stateMachine.TargetSys.SyncCamera(stateMachine.CachedAimCam);
                stateMachine.TargetSys.ClearTargetSilently(); 
            }

            stateMachine.SwitchState(new PlayerAimState(stateMachine));
            return; 
        }

        // Block (chỉ cận chiến)
        if (stateMachine.InputReader.IsHoldingBlock && stateMachine.CurrentWeapon != null 
            && stateMachine.CurrentWeapon.Type == WeaponType.Melee)
        {
            stateMachine.SwitchState(new PlayerBlockState(stateMachine));
            return;
        }
    }

    protected void FaceMovementDirection(Vector3 movement, float deltaTime)
    {
        if (movement == Vector3.zero) return;
        Quaternion targetRotation = Quaternion.LookRotation(movement);
        stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRotation, deltaTime * stateMachine.RotationDamping);
    }

    // 🟢 HÀM MỚI: Bẻ cong vận tốc ép sát theo độ nghiêng của dốc
    private Vector3 AdjustVelocityToSlope(Vector3 velocity)
    {
        // Bắn tia từ vị trí nhích lên một chút để không bị kẹt dưới sàn
        Ray ray = new Ray(stateMachine.transform.position + Vector3.up * 0.2f, Vector3.down);
        
        // Quét khoảng cách 0.5m xuống dưới chân
        if (Physics.Raycast(ray, out RaycastHit hit, 0.5f))
        {
            float slopeAngle = Vector3.Angle(Vector3.up, hit.normal);

            // Nếu đang đứng trên mặt phẳng có độ dốc (lớn hơn 0)
            if (slopeAngle > 0f)
            {
                // Bẻ cong vector di chuyển chạy dọc theo mặt dốc
                Vector3 slopeVelocity = Vector3.ProjectOnPlane(velocity, hit.normal);
                
                // Chỉ ép dính khi đi xuống dốc (y < 0)
                if (slopeVelocity.y < 0)
                {
                    return slopeVelocity;
                }
            }
        }
        return velocity;
    }

    private void OnRoll() 
    {
        if (stateMachine.Stamina.HasEnoughStamina(25f))
        {
            stateMachine.SwitchState(new PlayerRollState(stateMachine));
        }
    }
    
    private void OnAttack() 
    {
        if (stateMachine.CurrentWeapon != null && stateMachine.CurrentWeapon.Type != WeaponType.Ranged)
        {
            if (stateMachine.Stamina.HasEnoughStamina(stateMachine.CurrentWeapon.StaminaCost))
            {
                bool isRunning = stateMachine.InputReader.MovementValue.sqrMagnitude > 0 && stateMachine.InputReader.IsSprinting;
                stateMachine.SwitchState(new PlayerAttackState(stateMachine, 0, isRunning)); 
            }
        }
    }

    private void OnSkill1() => stateMachine.TryExecuteSkill(1);
    private void OnSkill2() => stateMachine.TryExecuteSkill(2);
    private void OnSkill3() => stateMachine.TryExecuteSkill(3);

    public override void Exit()
    {
        stateMachine.InputReader.RollEvent -= OnRoll;
        stateMachine.InputReader.AttackEvent -= OnAttack;

        stateMachine.InputReader.Skill1Event -= OnSkill1;
        stateMachine.InputReader.Skill2Event -= OnSkill2;
        stateMachine.InputReader.Skill3Event -= OnSkill3;
    }
}