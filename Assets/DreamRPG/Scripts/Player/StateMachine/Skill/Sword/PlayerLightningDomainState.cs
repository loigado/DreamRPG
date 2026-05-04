using UnityEngine;

public class PlayerLightningDomainState : PlayerBaseState
{
    private SkillData skill; 
    private float animDuration = 0.8f; 
    private float timer = 0f;
    private bool hasSpawnedDomain = false; 

    public PlayerLightningDomainState(PlayerStateMachine stateMachine, SkillData skill) : base(stateMachine) 
    {
        this.skill = skill;
    }

    public override void Enter()
    {
        timer = 0f;
        hasSpawnedDomain = false;

        // Trừ thể lực và Bắt đầu đếm Cooldown
        stateMachine.Stamina.UseStamina(skill.staminaCost);
        stateMachine.SkillManager.StartCooldown(skill);

        // Chạy Animation cắm kiếm (lấy từ Inspector)
        stateMachine.Animator.CrossFadeInFixedTime(skill.animationName, 0.1f);
        PlayerStateMachine.OnPlayerAttack?.Invoke(stateMachine.transform.position);
        stateMachine.Animator.applyRootMotion = true;
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);
        timer += deltaTime;

        // Canh thời gian kiếm vừa cắm xuống đất (0.4s) thì nổ vòng điện
        if (timer >= 0.4f && !hasSpawnedDomain)
        {
            SpawnDomain();
            hasSpawnedDomain = true;
        }

        // Cắm xong rút lên -> Trở về trạng thái di chuyển
        if (timer >= animDuration)
        {
            stateMachine.SwitchState(new PlayerMoveState(stateMachine));
        }
    }

    private void SpawnDomain()
    {
        if (skill.vfxPrefab != null)
        {
            // 🟢 FIX: Đã thay thế Instantiate bằng SpawnFromPool
            GameObject domainObj = ObjectPoolManager.Instance.SpawnFromPool(
                skill.vfxPrefab, 
                stateMachine.transform.position, 
                Quaternion.identity
            );

            // 🟢 FIX: Thêm điều kiện check domainObj != null phòng trường hợp Kho báo hết hàng
            if (domainObj != null && domainObj.TryGetComponent<LightningDomain>(out LightningDomain domainScript))
            {
                // Dùng damageMultiplier trong SkillData để khuếch đại sát thương vũ khí
                float baseDamage = stateMachine.CurrentWeapon != null ? stateMachine.CurrentWeapon.Damage : 10f;
                float dotDamage = baseDamage * skill.damageMultiplier;
                domainScript.Initialize(dotDamage);
            }
        }
        else
        {
            Debug.LogError($"⚠️ Sếp chưa kéo cục LightningDomain_VFX vào ô VFX Prefab của SkillData: {skill.skillName}!");
        }
    }
    public override void Exit()
    {
        stateMachine.Animator.applyRootMotion = false;
    }
}