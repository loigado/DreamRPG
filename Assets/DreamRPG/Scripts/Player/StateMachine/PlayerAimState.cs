using UnityEngine;
using Unity.Cinemachine; // 🟢 THÊM THƯ VIỆN ĐỂ SỬ DỤNG CACHED CAMERA

public class PlayerAimState : PlayerBaseState
{
    private readonly int NockHash = Animator.StringToHash("Bow_Nock");
    private readonly int ReloadHash = Animator.StringToHash("Bow_Reload");
    private readonly int AimIdleHash = Animator.StringToHash("Bow_AimIdle");
    private readonly int PullHash = Animator.StringToHash("Bow_Pull");
    private readonly int UnequipArrowHash = Animator.StringToHash("Bow_Unequip_Arrow");
    private readonly int EmptyHash = Animator.StringToHash("Empty");

    private readonly int AimMovementHash = Animator.StringToHash("AimMovement");
    private readonly int AimXHash = Animator.StringToHash("AimX");
    private readonly int AimYHash = Animator.StringToHash("AimY");

    private enum AimPhase { RutTen, NgamCho, KeoDay, CatTen }
    private AimPhase currentPhase;

    private float stateTimer = 0f; 
    private float drawTimer = 0f;
    private bool isTransitioningToShoot = false;

    public PlayerAimState(PlayerStateMachine stateMachine) : base(stateMachine) {}

    public override void Enter()
    {
        stateMachine.Animator.applyRootMotion = false;

        Vector3 camForward = stateMachine.MainCameraTransform.forward;
        camForward.y = 0;
        if (camForward != Vector3.zero)
        {
            stateMachine.transform.rotation = Quaternion.LookRotation(camForward);
        }

        // 🟢 DÙNG CACHED COMPONENT VÀ ĐẢM BẢO GAMEOBJECT ĐƯỢC BẬT
        if (stateMachine.AimCamera != null)
        {
            stateMachine.AimCamera.SetActive(true);
        }
        
        if (stateMachine.CachedAimCam != null) 
        {
            stateMachine.CachedAimCam.Priority = 100; 
        }
        
        if (stateMachine.CrosshairUI != null) stateMachine.CrosshairUI.SetActive(true);
        if (stateMachine.virtualArrow != null) stateMachine.virtualArrow.SetActive(true);

        stateMachine.UpdateWindVisuals();

        stateMachine.Animator.CrossFadeInFixedTime(AimMovementHash, 0.1f, 0);
        stateMachine.Animator.SetLayerWeight(1, 1f);
        
        stateTimer = 0f;
        drawTimer = 0f;
        currentPhase = AimPhase.RutTen;

        if (stateMachine.isReloading)
            stateMachine.Animator.Play(ReloadHash, 1, 0f);
        else
            stateMachine.Animator.Play(NockHash, 1, 0f);

        stateMachine.InputReader.Skill1Event += OnSkill1;
        stateMachine.InputReader.Skill2Event += OnSkill2;
        stateMachine.InputReader.Skill3Event += OnSkill3;
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);
        UpdateMovementAndRotation(deltaTime);
        UpdateCameraFOV(deltaTime); 

        // 🟢 DÙNG CACHED COMPONENT
        if (stateMachine.CachedAimCam != null && currentPhase != AimPhase.CatTen) 
        {
            if (stateMachine.CachedAimCam.Priority != 100) stateMachine.CachedAimCam.Priority = 100;
        }

        stateTimer += deltaTime;
        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(1);

        switch (currentPhase)
        {
            case AimPhase.RutTen:
                if (!stateMachine.InputReader.IsHoldingAim) { StartUnequipping(); break; }
                if (stateInfo.normalizedTime >= 0.9f || stateTimer > 1.2f)
                {
                    currentPhase = AimPhase.NgamCho;
                    stateMachine.Animator.Play(AimIdleHash, 1, 0f);
                    stateMachine.isReloading = false;
                    if (stateMachine.bowStringScript != null) stateMachine.bowStringScript.isDrawing = true;
                }
                break;
                
            case AimPhase.NgamCho:
                if (!stateMachine.InputReader.IsHoldingAim) StartUnequipping();
                else if (stateMachine.InputReader.IsHoldingAttack)
                {
                    currentPhase = AimPhase.KeoDay;
                    stateMachine.Animator.Play(PullHash, 1, 0f);
                    drawTimer = 0f;
                }
                break;
                
            case AimPhase.KeoDay:
                drawTimer += deltaTime * stateMachine.initialDrawSpeed;
                drawTimer = Mathf.Clamp(drawTimer, 0f, stateMachine.MaxDrawTime);
                if (!stateMachine.InputReader.IsHoldingAttack) ExecuteShoot();
                else if (!stateMachine.InputReader.IsHoldingAim) StartUnequipping();
                break;
                
            case AimPhase.CatTen:
                if (stateMachine.InputReader.IsHoldingAim)
                {
                    currentPhase = AimPhase.RutTen;
                    stateTimer = 0f;
                    
                    if (stateMachine.CachedAimCam != null) 
                    {
                        stateMachine.CachedAimCam.Priority = 100;
                    }
                    
                    stateMachine.Animator.Play(NockHash, 1, 0.1f);
                    break;
                }

                if ((stateInfo.shortNameHash == UnequipArrowHash && stateInfo.normalizedTime >= 0.9f) || stateTimer > 1.2f)
                {
                    CleanUpAimState();
                    stateMachine.SwitchState(new PlayerMovementState(stateMachine));
                }
                break;
        }
    }

    private void StartUnequipping()
    {
        currentPhase = AimPhase.CatTen;
        stateTimer = 0f; 
        stateMachine.Animator.Play(UnequipArrowHash, 1, 0f);
        if (stateMachine.bowStringScript != null) stateMachine.bowStringScript.isDrawing = false;
    }

    private void ExecuteShoot()
    {
        float drawPercent = drawTimer / stateMachine.MaxDrawTime;
        stateMachine.CurrentCalculatedForce = Mathf.Lerp(stateMachine.MinArrowForce, stateMachine.MaxArrowForce, drawPercent);
        isTransitioningToShoot = true;
        stateMachine.SwitchState(new PlayerShootState(stateMachine));
    }

    private void UpdateMovementAndRotation(float deltaTime)
    {
        float targetYaw = stateMachine.MainCameraTransform.eulerAngles.y + stateMachine.CharacterRotationOffset;
        Quaternion targetRotation = Quaternion.Euler(0, targetYaw, 0);

        stateMachine.transform.rotation = Quaternion.RotateTowards(
            stateMachine.transform.rotation, 
            targetRotation, 
            500f * deltaTime
        );

        Vector2 moveInput = stateMachine.InputReader.MovementValue; 
        Vector3 cameraForward = Vector3.ProjectOnPlane(stateMachine.MainCameraTransform.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(stateMachine.MainCameraTransform.right, Vector3.up).normalized;
        Vector3 targetDirection = (cameraForward * moveInput.y + cameraRight * moveInput.x).normalized;
        
        float aimSpeed = stateMachine.FreeLookMovementSpeed * 0.5f; 
        stateMachine.Controller.Move((targetDirection * aimSpeed + new Vector3(0, stateMachine.VerticalVelocity, 0)) * deltaTime);

        stateMachine.Animator.SetFloat(AimXHash, moveInput.x, 0.1f, deltaTime);
        stateMachine.Animator.SetFloat(AimYHash, moveInput.y, 0.1f, deltaTime);
        
        // 🟢 DÙNG CACHED RECT TRANSFORM
        if (stateMachine.CachedCrosshairRect != null && stateMachine.MaxDrawTime > 0f)
        {
            float drawPercent = Mathf.Clamp01(drawTimer / stateMachine.MaxDrawTime);
            float targetSize = Mathf.Lerp(50f, 20f, drawPercent);
            stateMachine.CachedCrosshairRect.sizeDelta = Vector2.Lerp(stateMachine.CachedCrosshairRect.sizeDelta, new Vector2(targetSize, targetSize), deltaTime * 10f);
        }
    }

    private void UpdateCameraFOV(float deltaTime)
    {
        // 🟢 DÙNG CACHED CAMERA
        if (stateMachine.CachedAimCam == null) return;

        float standardFOV = 40f; 
        float focusFOV = 25f;    
        
        float drawPercent = Mathf.Clamp01(drawTimer / stateMachine.MaxDrawTime);
        float targetFOV = Mathf.Lerp(standardFOV, focusFOV, drawPercent);
        stateMachine.CachedAimCam.Lens.FieldOfView = Mathf.Lerp(stateMachine.CachedAimCam.Lens.FieldOfView, targetFOV, deltaTime * 5f);
    }

    private void CleanUpAimState()
    {
        if (stateMachine.TargetSys != null && stateMachine.TargetSys.freeLookCamera != null)
        {
            stateMachine.TargetSys.SyncCamera(stateMachine.TargetSys.freeLookCamera);
        }

        if (stateMachine.CachedAimCam != null)
        {
            stateMachine.CachedAimCam.Lens.FieldOfView = 40f;
            stateMachine.CachedAimCam.Priority = 0; 
        }

        if (stateMachine.AimCamera != null)
        {
            stateMachine.AimCamera.SetActive(false);
        }

        if (stateMachine.CrosshairUI != null) stateMachine.CrosshairUI.SetActive(false);
        if (stateMachine.virtualArrow != null) stateMachine.virtualArrow.SetActive(false);
        
        stateMachine.Animator.SetLayerWeight(1, 0f);
        stateMachine.Animator.Play(EmptyHash, 1, 0f);

        stateMachine.isReloading = false;
        if (stateMachine.bowStringScript != null) stateMachine.bowStringScript.isDrawing = false;
    }

    // 🟢 DỌN DẸP SPAGHETTI CODE (Chỉ còn 3 dòng gọi hàm cực sạch sẽ)
    private void OnSkill1() => stateMachine.TryExecuteSkill(1);
    private void OnSkill2() => stateMachine.TryExecuteSkill(2);
    private void OnSkill3() => stateMachine.TryExecuteSkill(3);

    public override void Exit()
    {
        if (currentPhase != AimPhase.CatTen && !isTransitioningToShoot) CleanUpAimState();
        
        stateMachine.InputReader.Skill1Event -= OnSkill1;
        stateMachine.InputReader.Skill2Event -= OnSkill2;
        stateMachine.InputReader.Skill3Event -= OnSkill3;
    }
}