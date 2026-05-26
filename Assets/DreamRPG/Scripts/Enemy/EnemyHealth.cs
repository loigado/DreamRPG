using UnityEngine;
using UnityEngine.UI; // 🟢 THÊM THƯ VIỆN UI
using System;
using System.Collections.Generic;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public bool IsDead { get; private set; }
    public bool isFrozen = false;
    public bool isParalyzed = false;
    public bool isBlocking = false;

    // === CŨ: THANH SUPER ARMOR ===
    public float CurrentGuard { get; private set; }
    public bool IsGuardBroken => CurrentGuard <= 0f && stats != null && stats.maxGuard > 0f;

    // === 🟢 MỚI: THANH POSTURE (PHÁ THẾ) ===
    public float CurrentPosture { get; private set; }
    private float lastPostureDamageTime;
    private float lastGuardDamageTime; // 🟢 Dùng để phục hồi Guard
    private float guardBreakCooldownTimer = 0f; // 🟢 Thời gian chờ phục hồi sau khi vỡ khiên/giáp
    public event Action OnPostureBroken; // Bắn tín hiệu khi quái bị Vỡ Thế

    [Header("Posture UI (Kéo từ EnemyUI_Canvas vào đây)")]
    public CanvasGroup postureCanvasGroup; // Dùng để ẩn/hiện thanh mượt mà
    public Image postureFill;              // Thanh màu vàng

    // === SHATTER SYSTEM ===
    private const float SHATTER_DAMAGE_MULTIPLIER = 2f; 

    // === SLOW SYSTEM ===
    private float slowMultiplier = 1f;   
    private float slowTimer = 0f;
    private Animator cachedAnimator;
    private EnemyStateMachine cachedStateMachine;

    // === FREEZE VISUAL ===
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

    // === SỰ KIỆN ===
    public event Action<float, Vector3, bool> OnDamaged;
    public event Action<Vector3> OnBlocked;
    public event Action OnDeath;
    public event Action OnShattered;

    [Header("UI Feedback")]
    public GameObject damagePopupPrefab;

    private EnemyStatsSO stats;

    private void Start()
    {
        cachedStateMachine = GetComponent<EnemyStateMachine>();
        stats = cachedStateMachine?.Stats;
        cachedAnimator = GetComponent<Animator>();
        MaxHealth = stats != null ? stats.maxHealth : 100f;
        Health = MaxHealth;
        IsDead = false;

        // Khởi tạo thanh Guard
        CurrentGuard = stats != null ? stats.maxGuard : 0f;

        // 🟢 Khởi tạo Posture và Ẩn thanh UI lúc mới vào game
        CurrentPosture = 0f;
        if (postureCanvasGroup != null) postureCanvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        ResetHealth();
    }

    public void ResetHealth()
    {
        if (stats == null && cachedStateMachine != null) stats = cachedStateMachine.Stats;
        
        MaxHealth = stats != null ? stats.maxHealth : 100f;
        Health = MaxHealth;
        IsDead = false;
        isFrozen = false;
        isParalyzed = false;
        isBlocking = false;
        CurrentGuard = stats != null ? stats.maxGuard : 0f;
        CurrentPosture = 0f;
        guardBreakCooldownTimer = 0f;
        if (postureCanvasGroup != null) postureCanvasGroup.alpha = 0f;
    }

    private void Update()
    {
        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f) RemoveSlow();
        }

        // ==========================================
        // 🟢 XỬ LÝ HỒI POSTURE & GUARD CẬP NHẬT UI
        // ==========================================
        if (stats != null && !IsDead)
        {
            // Tụt Posture
            if (CurrentPosture > 0f && Time.time - lastPostureDamageTime > stats.postureRecoveryDelay)
            {
                CurrentPosture -= stats.postureRecoveryRate * Time.deltaTime;
                CurrentPosture = Mathf.Max(0f, CurrentPosture); 
            }

            // 🟢 MỚI: Chờ Cooldown nếu Guard bị đánh vỡ hoàn toàn
            if (guardBreakCooldownTimer > 0f)
            {
                guardBreakCooldownTimer -= Time.deltaTime;
                
                // Khi cooldown kết thúc, hồi lại 100% lớp giáp mới
                if (guardBreakCooldownTimer <= 0f)
                {
                    CurrentGuard = stats.maxGuard;
                    Debug.Log("<color=cyan>🛡️ Boss đã khôi phục lại toàn bộ lớp Giáp (Super Armor)!</color>");
                }
            }

            // Gửi dữ liệu phần trăm lên thanh Vàng
            if (postureFill != null && stats.maxPosture > 0f)
            {
                postureFill.fillAmount = CurrentPosture / stats.maxPosture;
            }

            // Ẩn/Hiện thanh UI mượt mà: Chỉ hiện khi có điểm Posture
            if (postureCanvasGroup != null)
            {
                float targetAlpha = CurrentPosture > 0.01f ? 1f : 0f;
                postureCanvasGroup.alpha = Mathf.MoveTowards(postureCanvasGroup.alpha, targetAlpha, Time.deltaTime * 3f);
            }
        }
    }

    public void TakeDamage(float damage, Vector3 attackerPos, bool isHeavy = false)
    {
        if (IsDead) return;

        // 🟢 THÊM CHEAT DAMAGE (Lấy từ hệ thống Player)
        damage += PlayerHealth.GlobalCheatDamageBonus;

        // 💨 I-Frames khi lộn nhào
        if (cachedStateMachine != null && cachedStateMachine.currentState is EnemyDodgeState)
        {
            Debug.Log("<color=cyan>💨 Enemy DODGED the attack! (I-frames active)</color>");
            return; 
        }

        // 🧊 SHATTER: Vỡ băng
        if (isFrozen)
        {
            damage *= SHATTER_DAMAGE_MULTIPLIER;
            Debug.Log($"<color=cyan>💎 VỠ BĂNG! Sát thương x{SHATTER_DAMAGE_MULTIPLIER}! ({damage})</color>");
            UnFreeze(); 
            OnShattered?.Invoke();
        }

        float heavyThreshold = (stats != null) ? stats.heavyStaggerThreshold : 0.15f;
        bool isHeavyDamage = (damage / MaxHealth) >= heavyThreshold || isHeavy;

        // ==========================================
        // 🟢 CỘNG ĐIỂM POSTURE KHI BỊ CHÉM TRÚNG
        // ==========================================
        if (stats != null && stats.maxPosture > 0f)
        {
            float postureDamage = damage * stats.postureDamageMultiplier;
            CurrentPosture += postureDamage;
            lastPostureDamageTime = Time.time;

            // KIỂM TRA VỠ THẾ (POSTURE BROKEN)
            if (CurrentPosture >= stats.maxPosture)
            {
                CurrentPosture = 0f; // Reset lại thanh
                Debug.Log("<color=red>💥 VỠ THẾ (POSTURE BROKEN)! Kẻ địch lảo đảo!</color>");
                OnPostureBroken?.Invoke(); // Bắn tín hiệu để StateMachine ép quái quỳ xuống
            }
        }

        // Nếu đang đỡ đòn (Skill chủ động BlockState)
        if (isBlocking)
        {
            if (isHeavyDamage)
            {
                Debug.Log($"<color=yellow>💥 GUARD BREAK! Người chơi dùng đòn nặng đập vỡ khiên quái!</color>");
                isBlocking = false; 
                if (cachedStateMachine != null && cachedStateMachine.Health != null) cachedStateMachine.Health.isBlocking = false;
                // Vỡ khiên thì lọt xuống dưới để mất máu và bị giật
            }
            else
            {
                // Đỡ thành công thì trừ thanh Guard
                if (stats != null && stats.maxGuard > 0f)
                {
                    CurrentGuard -= damage;
                    lastGuardDamageTime = Time.time; // 🟢 Cập nhật thời gian nhận ST
                    if (CurrentGuard <= 0f)
                    {
                        Debug.Log($"<color=yellow>💥 VỠ KHIÊN! Thanh Guard đã hết!</color>");
                        isBlocking = false;
                        guardBreakCooldownTimer = 8f; // 🟢 Phạt 8 giây không hồi Guard
                        if (cachedStateMachine != null) cachedStateMachine.Health.isBlocking = false;
                        // Không return để lọt xuống dưới chịu damage
                    }
                    else
                    {
                        Debug.Log($"<color=cyan>🛡️ Quái vật đã chặn thành công đòn chém! Guard còn lại: {CurrentGuard}</color>");
                        OnBlocked?.Invoke(attackerPos);
                        return; // Vẫn còn Guard thì không mất máu
                    }
                }
                else
                {
                    // Nếu không có maxGuard thì đỡ vô hạn (tới khi bị đòn nặng)
                    Debug.Log($"<color=cyan>🛡️ Quái vật đã chặn thành công đòn chém!</color>");
                    OnBlocked?.Invoke(attackerPos);
                    return; 
                }
            }
        }

        // 🟢 CƠ CHẾ SUPER ARMOR SHIELD
        // Kiểm tra xem quái có đang ở trạng thái sơ hở không? (Bị Parry hoặc đang Thở dốc)
        bool isVulnerable = false;
        if (cachedStateMachine != null)
        {
            if (cachedStateMachine.currentState is EnemyParriedState) isVulnerable = true;
            else if (cachedStateMachine.currentState is EnemyAttackState attackState && attackState.IsInRecovery) isVulnerable = true;
        }

        // Nếu quái có thanh Super Armor (maxGuard > 0), không chủ động Block, và KHÔNG sơ hở
        if (!isBlocking && stats != null && stats.maxGuard > 0f && CurrentGuard > 0f && !isVulnerable)
        {
            CurrentGuard -= damage; 
            lastGuardDamageTime = Time.time; // 🟢 Cập nhật thời gian nhận ST
            if (CurrentGuard <= 0f)
            {
                Debug.Log("<color=yellow>💥 VỠ SUPER ARMOR!</color>");
                guardBreakCooldownTimer = 8f; // 🟢 Phạt 8 giây không hồi Guard
                // Cho phép lọt xuống dưới để trừ HP thật và kích hoạt HitReaction
            }
            else
            {
                Debug.Log($"<color=white>🛡️ Quái lỳ đòn! Super Armor còn: {CurrentGuard}</color>");
                // Return ngay, không trừ HP và không giật
                return; 
            }
        }

        // 🩸 Trừ HP thực tế
        float oldHealth = Health;
        Health -= damage;
        Health = Mathf.Max(0f, Health);
        
        Debug.Log($"<color=lime>🩸 Kẻ địch trúng đòn! Mất {damage} HP. (Máu: {oldHealth} -> {Health})</color>");

        // Bắt buộc Heavy Hit nếu bị Vỡ Khiên/Vỡ Thế hoặc vũ khí có cờ isHeavyWeapon
        bool finalIsHeavy = isHeavyDamage || IsGuardBroken || isHeavy;

        // =========================================================
        // 🟢 HIỆN CHỮ NẢY SÁT THƯƠNG (DAMAGE POP-UP)
        // =========================================================
        if (damagePopupPrefab != null)
        {
            // Tọa độ văng ra (đỉnh đầu con quái)
            Vector3 popupPos = transform.position + Vector3.up * 1.5f;
            
            // Sinh chữ nảy
            GameObject popup = Instantiate(damagePopupPrefab, popupPos, Quaternion.identity);
            
            // Setup số sát thương và loại đòn đánh
            DamagePopup popupScript = popup.GetComponent<DamagePopup>();
            if (popupScript != null)
            {
                popupScript.Setup(damage, finalIsHeavy);
            }
        }

        if (Health <= 0f)
        {
            IsDead = true;

            // 🟢 Tắt thanh Posture khi quái chết để dọn dẹp màn hình
            if (postureCanvasGroup != null) postureCanvasGroup.alpha = 0f;

            RevertFreezeMaterials(); 
            OnDeath?.Invoke();
        }
        else
        {
            // Chỉ gọi OnDamaged (HitReaction) nếu quái VẪN CÒN SỐNG
            OnDamaged?.Invoke(damage, attackerPos, finalIsHeavy);
        }
    }

    public void TakeDamage(float damage, Vector3 attackerPos)
    {
        TakeDamage(damage, attackerPos, false);
    }

    // 🟢 MỚI: Reset lại toàn bộ Guard khi Quái bắt đầu chủ động Giơ Khiên Đỡ Đòn
    public void RefillGuard()
    {
        if (stats != null)
        {
            CurrentGuard = stats.maxGuard;
        }
    }

    // 🟢 MỚI: Trả lại thanh vỡ thế về 0
    public void ResetPosture()
    {
        CurrentPosture = 0f;
    }

    // =========================================================
    // FREEZE & SLOW (Giữ nguyên hoàn toàn logic cũ)
    // =========================================================
    private float pendingFreezeDuration = 0f;

    public void ApplyFreeze(float duration, float delay = 0.5f)
    {
        if (IsDead || isFrozen) return;
        pendingFreezeDuration = duration;
        if (delay <= 0f) ExecuteFreeze();
        else
        {
            CancelInvoke(nameof(ExecuteFreeze));
            Invoke(nameof(ExecuteFreeze), delay);
        }
    }

    private void ExecuteFreeze()
    {
        if (IsDead || isFrozen) return;
        isFrozen = true;
        ApplyFreezeMaterials();
        
        if (cachedAnimator != null) cachedAnimator.speed = 0f;
        if (cachedStateMachine != null && cachedStateMachine.Agent != null 
            && cachedStateMachine.Agent.isActiveAndEnabled && cachedStateMachine.Agent.isOnNavMesh)
        {
            cachedStateMachine.Agent.isStopped = true;
        }

        CancelInvoke(nameof(UnFreeze));
        Invoke(nameof(UnFreeze), pendingFreezeDuration);
    }

    private void UnFreeze()
    {
        isFrozen = false;
        RevertFreezeMaterials();
        
        if (cachedAnimator != null) cachedAnimator.speed = slowMultiplier;
        if (cachedStateMachine != null && cachedStateMachine.Agent != null 
            && cachedStateMachine.Agent.isActiveAndEnabled && cachedStateMachine.Agent.isOnNavMesh)
        {
            cachedStateMachine.Agent.isStopped = false;
        }
    }

    private void ApplyFreezeMaterials()
    {
        Material freezeMat = stats != null ? stats.freezeMaterial : null;
        if (freezeMat == null) return;
        originalMaterials.Clear();
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        foreach (Renderer ren in renderers)
        {
            if (ren.CompareTag("Weapon")) continue;
            originalMaterials[ren] = ren.sharedMaterials;
            Material[] originalMats = ren.sharedMaterials;
            Material[] frozenMats = new Material[originalMats.Length + 1];
            for (int i = 0; i < originalMats.Length; i++) frozenMats[i] = originalMats[i];
            frozenMats[originalMats.Length] = freezeMat;
            ren.sharedMaterials = frozenMats;
        }
    }

    private void RevertFreezeMaterials()
    {
        foreach (var pair in originalMaterials)
        {
            if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        }
        originalMaterials.Clear();
    }

    public void ApplySlow(float multiplier, float duration)
    {
        if (IsDead || isFrozen) return;
        slowMultiplier = Mathf.Min(slowMultiplier, multiplier); 
        slowTimer = Mathf.Max(slowTimer, duration); 

        if (cachedAnimator != null && !isFrozen) cachedAnimator.speed = slowMultiplier;
        if (cachedStateMachine != null && cachedStateMachine.Agent != null)
        {
            cachedStateMachine.Agent.speed = (stats != null ? stats.moveSpeed : 3f) * slowMultiplier;
        }
    }

    private void RemoveSlow()
    {
        slowMultiplier = 1f;
        slowTimer = 0f;
        if (cachedAnimator != null && !isFrozen) cachedAnimator.speed = 1f;
        if (cachedStateMachine != null && cachedStateMachine.Agent != null && stats != null)
        {
            cachedStateMachine.Agent.speed = stats.moveSpeed;
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Health = Mathf.Min(Health + amount, MaxHealth);
    }
}