using UnityEngine;
using System.Collections.Generic;

public class PlayerPhantomArrayState : PlayerBaseState
{
    private SkillData skill;
    private int phase = 0; // 0: Gồng (Khóa hướng), 1: Snap (Xoay nhanh), 2: Chém
    private float timer = 0f;
    private float spawnInterval = 0.5f; 
    private int maxPhantoms = 4; 
    private Transform skillTarget;
    private float maxHoldTimer = 0f; 
    
    private List<GameObject> activePhantoms = new List<GameObject>();
    private Vector3[] phantomPositions;

    public PlayerPhantomArrayState(PlayerStateMachine stateMachine, SkillData skill) : base(stateMachine) 
    { 
        this.skill = skill; 
        
        phantomPositions = new Vector3[] {
            new Vector3(1.5f, 0, -1.5f), 
            new Vector3(-1.5f, 0, -1.5f), 
            new Vector3(2.5f, 0, 0.5f), 
            new Vector3(-2.5f, 0, 0.5f)   
        };
    }

    public override void Enter()
    {
        phase = 0;
        timer = 0f;
        maxHoldTimer = 0f;
        activePhantoms.Clear();
        
        if (skill != null)
        {
            stateMachine.Stamina.UseStamina(skill.staminaCost);
            stateMachine.SkillManager.StartCooldown(skill);
        }

        stateMachine.Animator.CrossFadeInFixedTime("LightningSwordCharge", 0.1f);
        stateMachine.Animator.applyRootMotion = true; 
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);

        // --- PHASE 0: GỒNG TỤ LỰC (KHÓA HƯỚNG ĐỨNG IM) ---
        if (phase == 0)
        {
            timer += deltaTime;
            
            // Đẻ bóng theo chu kỳ
            if (timer >= spawnInterval && activePhantoms.Count < maxPhantoms)
            {
                SpawnPhantom();
                timer = 0f;
            }

            // Xử lý nhả phím hoặc gồng quá lâu (Overcharge)
            if (!stateMachine.InputReader.IsHoldingSkill2) 
            {
                StartSnapPhase();
            }
            else if (activePhantoms.Count >= maxPhantoms)
            {
                maxHoldTimer += deltaTime;
                if (maxHoldTimer >= 1.5f) 
                {
                    StartSnapPhase();
                }
            }
        }
        // --- PHASE 1: SNAP TARGET (XOAY CỰC NHANH TRONG 0.1S) ---
        else if (phase == 1)
        {
            timer += deltaTime;
            
            // Ép xoay với tốc độ siêu cao (40f) để chốt mục tiêu
            UpdateRotation(deltaTime * 40f); 

            // Chờ đúng 0.1s cho mặt xoay xong thì tung chiêu
            if (timer >= 0.1f) 
            {
                ExecuteGroupSlash();
            }
        }
        // --- PHASE 2: THU THẾ KẾT THÚC ---
        else if (phase == 2)
        {
            timer += deltaTime;
            if (timer >= 0.8f) 
            {
                stateMachine.SwitchState(new PlayerMovementState(stateMachine));
            }
        }
    }

    private void StartSnapPhase()
    {
        phase = 1;
        timer = 0f;
        
        // Chốt mục tiêu một lần duy nhất trước khi xoay để bám dính
        if (stateMachine.TargetSys != null)
        {
            skillTarget = stateMachine.TargetSys.FindSkillTarget();
        }
    }

    private void UpdateRotation(float speedMult)
    {
        Vector3 targetDir = Vector3.zero;
        
        if (skillTarget != null) 
        {
            targetDir = (skillTarget.position - stateMachine.transform.position).normalized;
        }
        else if (stateMachine.MainCameraTransform != null) 
        {
            targetDir = stateMachine.MainCameraTransform.forward;
        }

        targetDir.y = 0; 
        
        if (targetDir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDir);
            stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRot, speedMult);

            bool isHardLock = stateMachine.TargetSys != null && stateMachine.TargetSys.IsHardLocking;
            
            foreach (var phantom in activePhantoms)
            {
                if (phantom == null) continue;
                
                Vector3 pDir = (isHardLock && skillTarget != null) 
                    ? (skillTarget.position - phantom.transform.position).normalized 
                    : targetDir;
                    
                pDir.y = 0;
                
                if (pDir != Vector3.zero)
                {
                    phantom.transform.rotation = Quaternion.Slerp(phantom.transform.rotation, Quaternion.LookRotation(pDir), speedMult);
                }
            }
        }
    }

    private void ExecuteGroupSlash()
    {
        phase = 2;
        timer = 0f;

        // 🟢 BÁO ĐỘNG KHI TẤT CẢ PHANTOM CÙNG LAO VÀO CHÉM!
        PlayerStateMachine.OnPlayerAttack?.Invoke(stateMachine.transform.position);
        
        stateMachine.Animator.speed = 2f; 
        stateMachine.Animator.CrossFadeInFixedTime("LightningSword", 0.05f); 

        foreach (var phantom in activePhantoms)
        {
            if (phantom != null && phantom.TryGetComponent<PhantomController>(out PhantomController controller))
            {
                controller.ExecuteSlash();
            }
        }
    }

    private void SpawnPhantom()
    {
        if (skill.vfxPrefab != null)
        {
            Vector3 offset = stateMachine.transform.TransformDirection(phantomPositions[activePhantoms.Count]);
            
            // 🟢 FIX: Dùng Object Pool sinh Đệ tử ra
            GameObject phantom = ObjectPoolManager.Instance.SpawnFromPool(skill.vfxPrefab, stateMachine.transform.position + offset, stateMachine.transform.rotation);
            
            if (phantom != null && phantom.TryGetComponent<PhantomController>(out PhantomController controller))
            {
                float dmg = (stateMachine.CurrentWeapon != null ? stateMachine.CurrentWeapon.Damage : 10f) * skill.damageMultiplier;
                controller.Setup(skill.swordProjectilePrefab, skill.projectileSpeed, skill.maxProjectileDistance, dmg);
            }
            activePhantoms.Add(phantom);
        }
    }

    // Hàm này được Animation Event gọi khi đến frame chém (Của nhân vật chính)
    public void FirePlayerProjectile()
    {
        if (skill.swordProjectilePrefab != null)
        {
            // 🟢 FIX: Dùng Object Pool bắn phi kiếm của bản thân
            GameObject wave = ObjectPoolManager.Instance.SpawnFromPool(skill.swordProjectilePrefab, stateMachine.transform.position + Vector3.up * 1f, stateMachine.transform.rotation);
            
            if (wave != null && wave.TryGetComponent<SkillProjectile>(out SkillProjectile proj))
            {
                proj.Launch(skill.projectileSpeed, skill.maxProjectileDistance, HandlePlayerImpact, true); 
            }
        }
    }

    private void HandlePlayerImpact(Vector3 hitPoint, GameObject hitObj)
    {
        if (hitObj != null && hitObj.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            float dmg = (stateMachine.CurrentWeapon != null ? stateMachine.CurrentWeapon.Damage : 10f) * skill.damageMultiplier;
            damageable.TakeDamage(dmg, stateMachine.transform.position);
        }
    }

    public override void Exit()
    {
        // 🟢 FIX: Thu dọn đệ tử đưa về rổ thay vì Destroy
        foreach (var phantom in activePhantoms) 
        {
            if (phantom != null) 
            {
                ObjectPoolManager.Instance.ReturnToPool(phantom);
            }
        }
        activePhantoms.Clear();
        
        stateMachine.Animator.speed = 1f;
        stateMachine.Animator.applyRootMotion = false;
    }
}