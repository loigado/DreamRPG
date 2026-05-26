using UnityEngine;
using System.Collections.Generic;

// 🟢 STATE ĐỘC LẬP CHUYÊN XỬ LÝ KỸ NĂNG NÉM KIẾM DỊCH CHUYỂN
public class PlayerWarpStrikeState : PlayerBaseState
{
    private SkillData skill;
    private bool hasTeleported = false;
    private bool isStateActive = true;
    private bool hasLaunched = false;
    private Transform skillTarget;

    private Renderer[] playerMeshes; 
    private Quaternion targetRotation; 

    private GameObject flyingSwordObj;
    private float originalFOV;

    private GameObject struckEnemy = null;
    private bool isWarping = false;
    private Vector3 warpStartPos;
    private Vector3 warpTargetPos;
    private float warpTimer = 0f;
    public float warpDuration = 0.08f; 

    private bool isFinishing = false;
    private float finishTimer = 0f;

    public PlayerWarpStrikeState(PlayerStateMachine stateMachine, SkillData skill) : base(stateMachine) 
    { 
        this.skill = skill; 
    }

    public override void Enter()
    {
        isStateActive = true;
        hasTeleported = false;
        hasLaunched = false;
        isWarping = false;
        struckEnemy = null; 
        isFinishing = false; 
        finishTimer = 0f;

        playerMeshes = stateMachine.GetComponentsInChildren<Renderer>();
        originalFOV = stateMachine.MainCameraTransform.GetComponent<Camera>().fieldOfView;

        Behaviour cineBrain = stateMachine.MainCameraTransform.GetComponent("CinemachineBrain") as Behaviour;
        if (cineBrain != null) cineBrain.enabled = true;

        stateMachine.Animator.SetLayerWeight(1, 0f); 
        stateMachine.Stamina.UseStamina(skill.staminaCost);
        stateMachine.Animator.applyRootMotion = true;
        stateMachine.Animator.SetBool("isPerformingAction", true);

        if (stateMachine.TargetSys != null)
        {
            if (!stateMachine.TargetSys.IsHardLocking) stateMachine.TargetSys.FindSoftTarget();
            skillTarget = stateMachine.TargetSys.GetCurrentTarget();

            Vector3 lookDir = skillTarget != null ? 
                (skillTarget.position - stateMachine.transform.position).normalized : 
                stateMachine.MainCameraTransform.forward;

            lookDir.y = 0;
            targetRotation = lookDir != Vector3.zero ? Quaternion.LookRotation(lookDir) : stateMachine.transform.rotation;
        }

        stateMachine.Animator.CrossFadeInFixedTime(skill.animationName, 0.1f);
        stateMachine.SkillManager.StartCooldown(skill);
    }

    public void ExecuteSkillAction()
    {
        if (hasLaunched || !isStateActive) return;
        hasLaunched = true;

        // 🟢 BÁO ĐỘNG KHI PHI KIẾM RỜI TAY!
        PlayerStateMachine.OnPlayerAttack?.Invoke(stateMachine.transform.position);

        stateMachine.transform.rotation = targetRotation;

        Vector3 spawnPos = stateMachine.transform.position + Vector3.up * 1.2f;
        Vector3 fireDir = skillTarget != null ? 
            (skillTarget.position + Vector3.up * 1.0f - spawnPos).normalized : 
            stateMachine.transform.forward;

        if (skill.swordProjectilePrefab != null)
        {
            // 🟢 FIX: Dùng Object Pool thay vì Instantiate cho Phi Kiếm
            flyingSwordObj = ObjectPoolManager.Instance.SpawnFromPool(skill.swordProjectilePrefab, spawnPos, Quaternion.LookRotation(fireDir));
            
            if (flyingSwordObj != null && flyingSwordObj.TryGetComponent<SkillProjectile>(out SkillProjectile proj))
            {
                proj.Launch(skill.projectileSpeed, skill.maxProjectileDistance, HandleProjectileImpact);
            }
            
            stateMachine.ToggleWeaponVisual(false); 
            stateMachine.Animator.speed = 0f; 

            SpawnGhost(); 
            SetPlayerVisibility(false); 
        }
        else
        {
            hasTeleported = true; 
        }
    }

    private void HandleProjectileImpact(Vector3 impactPoint, GameObject hitObject)
    {
        if (!isStateActive || hasTeleported) return;
        hasTeleported = true;

        bool hit = hitObject != null && (hitObject.CompareTag("Enemy") || hitObject.layer == LayerMask.NameToLayer("Enemy"));
        if (hit) struckEnemy = hitObject;
        else struckEnemy = null;

        Vector3 finalPos = impactPoint - stateMachine.transform.forward * 0.5f;
        if (hitObject != null) finalPos.y = hitObject.transform.position.y;
        else finalPos.y = stateMachine.transform.position.y;

        warpStartPos = stateMachine.transform.position;
        warpTargetPos = finalPos;
        isWarping = true;
        warpTimer = 0f;
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);

        if (!hasLaunched)
        {
            stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRotation, deltaTime * 15f);
            return;
        }

        if (!hasTeleported)
        {
            if (flyingSwordObj != null && flyingSwordObj.activeInHierarchy) // Đảm bảo thanh kiếm chưa bị cất đi
            {
                Camera cam = stateMachine.MainCameraTransform.GetComponent<Camera>();
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, originalFOV + 12f, deltaTime * 10f);
                Vector3 lookDir = flyingSwordObj.transform.position - cam.transform.position;
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(lookDir), deltaTime * 10f);
            }
            return;
        }

        if (isWarping)
        {
            warpTimer += deltaTime;
            float t = Mathf.Clamp01(warpTimer / warpDuration);
            stateMachine.transform.position = Vector3.Lerp(warpStartPos, warpTargetPos, t);

            if (t >= 1f)
            {
                isWarping = false;
                isFinishing = true; 
                finishTimer = 0f;

                stateMachine.Animator.speed = 1f; 
                SetPlayerVisibility(true); 
                stateMachine.ToggleWeaponVisual(true);
                SpawnGhost(); 

                if (struckEnemy != null)
                {
                    stateMachine.Animator.CrossFadeInFixedTime("ThrowSwordHit", 0.05f); 
                    stateMachine.EnableHitbox(); 

                    // 🟢 GUARANTEED DAMAGE: Gây sát thương trực tiếp lên mục tiêu thay vì đợi Hitbox va chạm hên xui
                    EnemyStateMachine enemy = struckEnemy.GetComponentInParent<EnemyStateMachine>();
                    if (enemy != null)
                    {
                        float baseDamage = stateMachine.CurrentWeapon != null ? stateMachine.CurrentWeapon.Damage : 10f;
                        enemy.TakeDamage(baseDamage * skill.damageMultiplier, stateMachine.transform.position);
                    }

                    if (skill.vfxPrefab != null) 
                    {
                        Vector3 vfxSpawnPos = stateMachine.transform.position + Vector3.up * 1.0f; 
                        
                        // 🟢 FIX: Dùng Object Pool cho VFX trúng đích
                        GameObject vfxInstance = ObjectPoolManager.Instance.SpawnFromPool(skill.vfxPrefab, vfxSpawnPos, stateMachine.transform.rotation);
                        Component impulse = vfxInstance.GetComponent("CinemachineImpulseSource");
                        if (impulse != null) impulse.SendMessage("GenerateImpulse", SendMessageOptions.DontRequireReceiver);
                        
                        // Chú ý: VFX này tự cất vào pool bằng script VFXAutoDestroy, không xài Destroy nữa.
                    }
                }
                else
                {
                    stateMachine.Animator.CrossFadeInFixedTime("ThrowSwortRoll", 0.1f); 
                }
            }
            return; 
        }

        Camera mainCam = stateMachine.MainCameraTransform.GetComponent<Camera>();
        mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, originalFOV, deltaTime * 10f);

        if (isFinishing)
        {
            finishTimer += deltaTime;
            if (finishTimer > 0.1f)
            {
                AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.normalizedTime >= 0.9f && !stateMachine.Animator.IsInTransition(0)) 
                {
                    mainCam.fieldOfView = originalFOV; 
                    stateMachine.SwitchState(new PlayerMovementState(stateMachine));
                }
            }
        }
    }

    private void SpawnGhost()
    {
        if (playerMeshes == null || playerMeshes.Length == 0) return;
        
        // 🟢 FIX 1: Chốt chặn an toàn
        if (skill.ghostPrefab == null) 
        {
            Debug.LogWarning("Sếp quên kéo Prefab GhostFrame vào SkillData rồi!");
            return;
        }

        // 🟢 FIX 2: Gọi GhostFrame từ kho Object Pool ra thay vì new GameObject()
        GameObject ghostObj = ObjectPoolManager.Instance.SpawnFromPool(skill.ghostPrefab, stateMachine.transform.position, stateMachine.transform.rotation);
        
        // 🟢 FIX 3: Lấy script và bơm data vào
        if (ghostObj.TryGetComponent<GhostEffect>(out GhostEffect ghostScript))
        {
            if (skill.ghostMaterial != null)
            {
                List<Renderer> activeMeshes = new List<Renderer>();
                foreach (var mesh in playerMeshes)
                {
                    if (mesh != null && mesh.gameObject.activeInHierarchy && mesh.enabled) 
                        activeMeshes.Add(mesh);
                }
                ghostScript.Setup(activeMeshes.ToArray(), skill.ghostMaterial);
            }
        }
    }

    private void SetPlayerVisibility(bool isVisible)
    {
        if (playerMeshes == null) return;
        foreach (Renderer mesh in playerMeshes) { if (mesh != null) mesh.enabled = isVisible; }
    }

    public override void Exit()
    {
        isStateActive = false;
        SetPlayerVisibility(true); 
        stateMachine.ToggleWeaponVisual(true); 
        stateMachine.DisableHitbox();
        stateMachine.Animator.speed = 1f; 
        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.SetBool("isPerformingAction", false);
    }
}