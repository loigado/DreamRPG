using System;
using UnityEngine;

/// <summary>
/// PlayerHealth — Hệ thống HP cho Player (AAA standard).
///
/// Features:
///   • HP + MaxHP
///   • Event-driven (OnDamaged, OnDeath, OnHealthChanged)
///   • Rage Mode khi HP dưới 25% (GoW Spartan Rage threshold)
///   • Heal support
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float MaxHealth = 100f;
    
    // Cheat
    public static float GlobalCheatDamageBonus = 0f;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    /// <summary>Tỷ lệ HP hiện tại (0-1), dùng cho UI bar.</summary>
    public float HealthPercent => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;

    /// <summary>Player đang ở ngưỡng nguy hiểm (≤25% HP).</summary>
    public bool IsLowHealth => HealthPercent <= 0.25f;

    // === EVENTS ===
    /// <summary>Khi nhận sát thương: (damage, currentHP, maxHP)</summary>
    public event Action<float, float, float> OnDamaged;
    /// <summary>Khi chết</summary>
    public event Action OnDeath;
    /// <summary>Khi HP thay đổi (bất kỳ): (currentHP / maxHP)</summary>
    public event Action<float> OnHealthChanged;

    private void Start()
    {
        CurrentHealth = MaxHealth;
        IsDead = false;
    }

    /// <summary>
    /// Nhận sát thương. Gọi từ PlayerStateMachine.TakeDamage().
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (IsDead) return;
        if (damage <= 0) return;

        float previousHP = CurrentHealth;
        CurrentHealth -= damage;
        CurrentHealth = Mathf.Max(0f, CurrentHealth);

        Debug.Log($"<color=red>💥 Kratos bị ăn chém! Nhận {damage} sát thương. (HP: {previousHP} -> {CurrentHealth})</color>");

        OnDamaged?.Invoke(damage, CurrentHealth, MaxHealth);
        OnHealthChanged?.Invoke(HealthPercent);

        if (CurrentHealth <= 0f)
        {
            IsDead = true;
            OnDeath?.Invoke();
        }
    }

    /// <summary>Hồi máu (potion, rest point, skill).</summary>
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0) return;

        CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
        OnHealthChanged?.Invoke(HealthPercent);
    }

    /// <summary>Hồi full HP (checkpoint, cutscene).</summary>
    public void FullHeal()
    {
        if (IsDead) return;
        CurrentHealth = MaxHealth;
        OnHealthChanged?.Invoke(HealthPercent);
    }

    /// <summary>Hồi sinh (sau Death screen).</summary>
    public void Revive(float healthPercent = 0.5f)
    {
        IsDead = false;
        CurrentHealth = MaxHealth * Mathf.Clamp01(healthPercent);
        OnHealthChanged?.Invoke(HealthPercent);
    }
}
