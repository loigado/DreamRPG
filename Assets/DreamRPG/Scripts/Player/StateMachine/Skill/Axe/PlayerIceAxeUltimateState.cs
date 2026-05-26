using UnityEngine;

/// <summary>
/// PlayerIceAxeUltimateState — Chiêu cuối Rìu Băng:
///
/// Giai đoạn 1 (GỒM): 
///   • Player bất tử (I-Frame)
///   • Quái trong vùng ảnh hưởng bị LÀM CHẬM (Slow 30% tốc độ)
///   • Gồng càng lâu → sát thương càng mạnh (tối đa 3s)
///
/// Giai đoạn 2 (NỔ):
///   • Gây sát thương TOÀN BỘ quái trong bán kính 6m
///   • Đóng băng quái (Freeze)
///
/// Giai đoạn 3 (HẬU CHIẾN):
///   • Vùng Slow tiếp tục tồn tại thêm 8s sau khi nổ
///   • Quái đi vào vùng bị giảm 60% tốc độ
/// </summary>
public class PlayerIceAxeUltimateState : PlayerBaseState
{
    private SkillData skill;
    
    // Quản lý thời gian gồng
    private float chargeTimer = 0f;
    private const float MAX_CHARGE_TIME = 3.0f;
    
    // Quản lý trạng thái và độ trễ sau khi nổ
    private bool isReleased = false;
    private float postReleaseTimer = 0f;
    private const float POST_RELEASE_DELAY = 1.0f;

    // Slow zone
    private GameObject chargeVfxInstance;
    private const float POST_EXPLOSION_SLOW_DURATION = 8f; // Vùng slow kéo dài 8s sau nổ

    public PlayerIceAxeUltimateState(PlayerStateMachine stateMachine, SkillData skill) : base(stateMachine)
    {
        this.skill = skill;
    }

    public override void Enter()
    {
        chargeTimer = 0f;
        postReleaseTimer = 0f;
        isReleased = false;

        // 🟢 Bắt đầu gồng
        stateMachine.Animator.CrossFadeInFixedTime("Axe_Ultimate_Charge", 0.1f);
        stateMachine.Animator.SetBool("isPerformingAction", true);
        
        // 🎯 TỰ ĐIỀU HƯỚNG: Xoay mặt về phía quái khi bắt đầu gồng
        if (stateMachine.TargetSys.GetCurrentTarget() != null)
        {
            stateMachine.TargetSys.FaceTarget(stateMachine.TargetSys.GetCurrentTarget().position, deltaTime: 0, instant: true);
        }
        
        // 🟢 Đổi Material sang dạng Băng
        if (skill.iceFormMaterials != null && skill.iceFormMaterials.Length > 0)
        {
            stateMachine.ApplyOverlayMaterials(skill.iceFormMaterials);
        }

        // 🟢 Tạo VFX Gồng (Vùng băng làm chậm) bằng Pool
        if (skill.chargeVfxPrefab != null)
        {
            chargeVfxInstance = ObjectPoolManager.Instance.SpawnFromPool(skill.chargeVfxPrefab, stateMachine.transform.position, Quaternion.identity);
            
            if (!chargeVfxInstance.TryGetComponent<IceAxeZone>(out IceAxeZone zone))
            {
                zone = chargeVfxInstance.AddComponent<IceAxeZone>();
            }
            // Vùng slow tồn tại trong suốt thời gian gồng + buffer
            zone.Setup(MAX_CHARGE_TIME + 2f, 0.1f); 
        }

        // 🟢 BẤT TỬ: Player không nhận sát thương khi đang gồng
        stateMachine.EnableInvincibility();

        // ❄️ LÀM CHẬM QUÁI XUNG QUANH ngay khi bắt đầu gồng
        ApplySlowToNearbyEnemies(0.3f, MAX_CHARGE_TIME + 1f);
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);

        // Giai đoạn 1: ĐANG GỒNG
        if (!isReleased)
        {
            // Kiểm tra người chơi còn giữ phím Skill 2 không
            if (stateMachine.InputReader.IsHoldingSkill2 && chargeTimer < MAX_CHARGE_TIME)
            {
                chargeTimer += deltaTime;
                
                // Cập nhật vị trí VFX gồng theo Player
                if (chargeVfxInstance != null) chargeVfxInstance.transform.position = stateMachine.transform.position;
            }
            else
            {
                // Buông phím hoặc gồng max -> Phát nổ!
                ReleaseUltimate();
            }
        }
        // Giai đoạn 2: SAU KHI NỔ (Chờ Animation kết thúc)
        else 
        {
            postReleaseTimer += deltaTime;
            if (postReleaseTimer >= POST_RELEASE_DELAY)
            {
                stateMachine.SwitchState(new PlayerMovementState(stateMachine));
            }
        }
    }

    private void ReleaseUltimate()
    {
        isReleased = true;
        // 🟢 BÁO ĐỘNG NGAY LÚC NHẢ RÌU XUỐNG!
        PlayerStateMachine.OnPlayerAttack?.Invoke(stateMachine.transform.position);

        // Chỉ ra lệnh chạy animation phát nổ đập rìu xuống
        stateMachine.Animator.CrossFadeInFixedTime("Axe_Ultimate_Release", 0.05f);

        // Trừ stamina và bắt đầu hồi chiêu ngay thời điểm tung chiêu
        stateMachine.Stamina.UseStamina(skill.staminaCost);
        stateMachine.SkillManager.StartCooldown(skill);
    }

    // 🟢 HÀM MỚI ĐƯỢC THÊM VÀO: Sẽ được gọi bởi Animation Event từ PlayerAnimationEvents
    public void ExecuteImpactAction()
    {
        // 🟢 Giải phóng năng lượng băng: Trả lại màu gốc ngay khi nổ
        stateMachine.RevertIceForm();

        // Tính toán sức mạnh dựa trên thời gian gồng
        float chargeRatio = chargeTimer / MAX_CHARGE_TIME;
        float damageMult = Mathf.Lerp(skill.damageMultiplier, skill.damageMultiplier * 2.5f, chargeRatio);

        // 💥 GÂY SÁT THƯƠNG VỤ NỔ — TOÀN BỘ QUÁI TRONG BÁN KÍNH
        Collider[] hits = Physics.OverlapSphere(stateMachine.transform.position, 6f, LayerMask.GetMask("Enemy"));
        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<EnemyHealth>(out EnemyHealth health))
            {
                // Sát thương nổ cực mạnh, luôn là đòn nặng
                health.TakeDamage(30f * damageMult, stateMachine.transform.position, true);
                // 🧊 Đóng băng quái sau vụ nổ
                health.ApplyFreeze(skill.freezeDuration * (1f + chargeRatio));
            }
        }

        // 🟢 Tạo VFX Nổ (Vùng băng nổ ra) - Dùng Pool
        if (skill.vfxPrefab != null)
        {
            ObjectPoolManager.Instance.SpawnFromPool(skill.vfxPrefab, stateMachine.transform.position, Quaternion.identity);
        }

        // ❄️ DUY TRÌ VÙNG LÀM CHẬM THÊM 8 GIÂY SAU KHI NỔ
        if (chargeVfxInstance != null)
        {
            if (chargeVfxInstance.TryGetComponent<IceAxeZone>(out IceAxeZone zone))
            {
                // Vùng slow tiếp tục 8s với sát thương nhẹ
                zone.Setup(POST_EXPLOSION_SLOW_DURATION, damageMult * 0.3f); 
            }
        }

        // Áp dụng lại slow cho quái mới đi vào vùng (zone sẽ tự xử lý qua OnTriggerStay)
        ApplySlowToNearbyEnemies(0.3f, POST_EXPLOSION_SLOW_DURATION);
    }

    /// <summary>
    /// Làm chậm tất cả quái trong bán kính 8m.
    /// </summary>
    private void ApplySlowToNearbyEnemies(float slowAmount, float duration)
    {
        Collider[] enemies = Physics.OverlapSphere(stateMachine.transform.position, 8f, LayerMask.GetMask("Enemy"));
        foreach (var e in enemies)
        {
            if (e.TryGetComponent<EnemyHealth>(out EnemyHealth health))
            {
                health.ApplySlow(slowAmount, duration);
            }
        }
    }

    public override void Exit()
    {
        // Safety Revert: Đảm bảo nhân vật luôn về màu gốc nếu chiêu thức bị ngắt quãng
        stateMachine.RevertIceForm(); 
        
        stateMachine.DisableInvincibility();
        stateMachine.Animator.SetBool("isPerformingAction", false);

        // 🟢 FIX: Cất VFX gồng vào Pool thay vì Destroy nếu bị ngắt chiêu
        if (!isReleased && chargeVfxInstance != null)
        {
            ObjectPoolManager.Instance.ReturnToPool(chargeVfxInstance);
        }
    }
}