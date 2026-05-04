using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// EnemyHealth — Quản lý HP, nhận sát thương, trạng thái (đông/liệt), và sự kiện chết.
///
/// Hệ thống trạng thái băng:
///   • ApplyFreeze() → đóng băng quái (dừng Animator + dừng di chuyển + phủ Material băng)
///   • Khi bị đánh trong lúc đóng băng → SHATTER (vỡ băng) → x2 sát thương
///   • ApplySlow() → giảm tốc độ di chuyển + animation theo multiplier
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public bool IsDead { get; private set; }
    public bool isFrozen = false;
    public bool isParalyzed = false;
    public bool isBlocking = false;

    // === SHATTER SYSTEM ===
    private const float SHATTER_DAMAGE_MULTIPLIER = 2f; // x2 damage khi vỡ băng

    // === SLOW SYSTEM ===
    private float slowMultiplier = 1f;   // 1 = tốc độ bình thường, 0.3 = chậm 70%
    private float slowTimer = 0f;
    private Animator cachedAnimator;
    private EnemyStateMachine cachedStateMachine;

    // === FREEZE VISUAL ===
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

    // === SỰ KIỆN ===
    /// <summary>Khi nhận sát thương: (damage, attackerPos, isHeavyHit)</summary>
    public event Action<float, Vector3, bool> OnDamaged;
    /// <summary>Khi đỡ đòn thành công</summary>
    public event Action<Vector3> OnBlocked;
    /// <summary>Khi HP về 0</summary>
    public event Action OnDeath;
    /// <summary>Khi bị vỡ băng (Shatter)</summary>
    public event Action OnShattered;

    private EnemyStatsSO stats;

    private void Start()
    {
        cachedStateMachine = GetComponent<EnemyStateMachine>();
        stats = cachedStateMachine?.Stats;
        cachedAnimator = GetComponent<Animator>();
        MaxHealth = stats != null ? stats.maxHealth : 100f;
        Health = MaxHealth;
        IsDead = false;
    }

    private void Update()
    {
        // Cập nhật Slow timer
        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f)
            {
                RemoveSlow();
            }
        }
    }

    /// <summary>
    /// Nhận sát thương chuẩn. Gọi từ Player combat system.
    /// </summary>
    public void TakeDamage(float damage, Vector3 attackerPos, float launchForce = 0f)
    {
        if (IsDead) return;

        // 🧊 SHATTER: Nếu đang bị đóng băng mà bị đánh → VỠ BĂNG → x2 damage!
        if (isFrozen)
        {
            damage *= SHATTER_DAMAGE_MULTIPLIER;
            Debug.Log($"<color=cyan>💎 VỠ BĂNG! Sát thương x{SHATTER_DAMAGE_MULTIPLIER}! ({damage})</color>");
            UnFreeze(); // Giải băng + khôi phục visual
            OnShattered?.Invoke();
        }

        // Nếu đang đỡ đòn (Block)
        if (isBlocking)
        {
            Debug.Log($"<color=cyan>🛡️ Quái vật đã chặn thành công đòn chém của Kratos!</color>");
            OnBlocked?.Invoke(attackerPos);
            return; // Kháng 100% damage
        }

        float previousHP = Health;
        Health -= damage;
        Health = Mathf.Max(0f, Health);

        Debug.Log($"<color=orange>🗡️ Quái vật ăn đòn! Nhận {damage} sát thương. (HP: {previousHP} -> {Health})</color>");

        // Xác định heavy hay light hit
        bool isHeavy = (damage / MaxHealth) >= (stats != null ? stats.heavyStaggerThreshold : 0.15f);

        // Phát sự kiện → EnemyStateMachine sẽ lắng nghe để chuyển sang HitReactionState
        OnDamaged?.Invoke(damage, attackerPos, isHeavy);

        if (Health <= 0f)
        {
            IsDead = true;
            RevertFreezeMaterials(); // Xóa visual băng nếu chết
            OnDeath?.Invoke();
        }
    }

    /// <summary>Overload tương thích code cũ</summary>
    public void TakeDamage(float damage, Vector3 attackerPos)
    {
        TakeDamage(damage, attackerPos, 0f);
    }

    // =========================================================
    // FREEZE SYSTEM (Đóng băng + Visual)
    // =========================================================

    // Cache cho delayed freeze
    private float pendingFreezeDuration = 0f;

    /// <summary>
    /// Đóng băng quái. delay = thời gian chờ trước khi đóng băng (để VFX kịp chạy tới).
    /// </summary>
    public void ApplyFreeze(float duration, float delay = 0.5f)
    {
        if (IsDead || isFrozen) return;

        pendingFreezeDuration = duration;

        if (delay <= 0f)
        {
            ExecuteFreeze();
        }
        else
        {
            // Chờ VFX chạy tới rồi mới đóng băng
            CancelInvoke(nameof(ExecuteFreeze));
            Invoke(nameof(ExecuteFreeze), delay);
        }
    }

    /// <summary>Thực thi đóng băng (gọi từ Invoke hoặc trực tiếp)</summary>
    private void ExecuteFreeze()
    {
        if (IsDead || isFrozen) return;

        isFrozen = true;
        
        // 🧊 VISUAL: Phủ material băng lên toàn bộ quái
        ApplyFreezeMaterials();
        
        // Dừng Animator (quái đứng khựng như tượng băng)
        if (cachedAnimator != null) cachedAnimator.speed = 0f;
        
        // Dừng NavMeshAgent
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
        
        // 🧊 VISUAL: Khôi phục material gốc
        RevertFreezeMaterials();
        
        // Khôi phục Animator (tính cả slow nếu đang bị)
        if (cachedAnimator != null) cachedAnimator.speed = slowMultiplier;
        
        // Khôi phục NavMeshAgent
        if (cachedStateMachine != null && cachedStateMachine.Agent != null 
            && cachedStateMachine.Agent.isActiveAndEnabled && cachedStateMachine.Agent.isOnNavMesh)
        {
            cachedStateMachine.Agent.isStopped = false;
        }
    }

    // =========================================================
    // FREEZE VISUAL (Hiệu ứng băng trên thân quái)
    // =========================================================

    /// <summary>
    /// Phủ material băng lên tất cả Renderer của quái.
    /// Lưu material gốc để khôi phục khi hết freeze.
    /// </summary>
    private void ApplyFreezeMaterials()
    {
        Material freezeMat = stats != null ? stats.freezeMaterial : null;
        if (freezeMat == null) return;

        originalMaterials.Clear();
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        foreach (Renderer ren in renderers)
        {
            // Bỏ qua vũ khí (nếu có tag)
            if (ren.CompareTag("Weapon")) continue;

            // Lưu material gốc
            originalMaterials[ren] = ren.sharedMaterials;

            // Tạo mảng material mới = gốc + overlay băng
            Material[] originalMats = ren.sharedMaterials;
            Material[] frozenMats = new Material[originalMats.Length + 1];
            for (int i = 0; i < originalMats.Length; i++)
                frozenMats[i] = originalMats[i];
            frozenMats[originalMats.Length] = freezeMat;
            
            ren.sharedMaterials = frozenMats;
        }
    }

    /// <summary>
    /// Khôi phục material gốc sau khi hết freeze hoặc bị shatter.
    /// </summary>
    private void RevertFreezeMaterials()
    {
        foreach (var pair in originalMaterials)
        {
            if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        }
        originalMaterials.Clear();
    }

    // =========================================================
    // SLOW SYSTEM (Làm chậm)
    // =========================================================

    /// <summary>
    /// Làm chậm quái vật. multiplier = 0.3 nghĩa là chạy 30% tốc độ bình thường.
    /// </summary>
    public void ApplySlow(float multiplier, float duration)
    {
        if (IsDead || isFrozen) return;

        slowMultiplier = Mathf.Min(slowMultiplier, multiplier); // Lấy slow mạnh nhất
        slowTimer = Mathf.Max(slowTimer, duration); // Lấy thời gian dài nhất

        // Giảm tốc Animator
        if (cachedAnimator != null && !isFrozen) cachedAnimator.speed = slowMultiplier;

        // Giảm tốc NavMeshAgent
        if (cachedStateMachine != null && cachedStateMachine.Agent != null)
        {
            cachedStateMachine.Agent.speed = (stats != null ? stats.moveSpeed : 3f) * slowMultiplier;
        }
    }

    private void RemoveSlow()
    {
        slowMultiplier = 1f;
        slowTimer = 0f;

        // Khôi phục Animator
        if (cachedAnimator != null && !isFrozen) cachedAnimator.speed = 1f;

        // Khôi phục NavMeshAgent
        if (cachedStateMachine != null && cachedStateMachine.Agent != null && stats != null)
        {
            cachedStateMachine.Agent.speed = stats.moveSpeed;
        }
    }

    /// <summary>Hồi phục HP (potion, buff...)</summary>
    public void Heal(float amount)
    {
        if (IsDead) return;
        Health = Mathf.Min(Health + amount, MaxHealth);
    }
}