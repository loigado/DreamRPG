using UnityEngine;
using System.Collections.Generic;

public class MovingTornado : MonoBehaviour
{
    [Header("Cài đặt Di Chuyển")]
    public float moveSpeed = 5f;        
    public float duration = 8f;         
    public float heightOffset = 2f;     

    [Header("Cài đặt Hút & Sát thương")]
    public float pullForce = 15f;       
    public float tickRate = 0.3f;       
    private float damagePerTick;
    private float tickTimer;
    private float lifeTimer; // 🟢 FIX: Thêm bộ đếm tuổi thọ tự thân

    private List<Collider> trappedEnemies = new List<Collider>();
    private List<IDamageable> damageableTargets = new List<IDamageable>();

    // 🟢 FIX: Reset lại mọi danh sách khi lôi từ trong kho ra
    private void OnEnable()
    {
        lifeTimer = duration; 
        tickTimer = 0f;
        trappedEnemies.Clear();
        damageableTargets.Clear();
    }

    public void Setup(float damage)
    {
        damagePerTick = damage;

        // Nhấc cơn lốc lên cao ngay khi vừa sinh ra
        transform.position += new Vector3(0, heightOffset, 0);

        // ❌ XÓA LỆNH: Destroy(gameObject, duration);
    }

    void Update()
    {
        // 🟢 FIX: Quản lý việc biến mất bằng tuổi thọ
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0)
        {
            // Cất vào kho, nếu lỡ quên tạo kho thì mới Destroy để tránh lỗi
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.ReturnToPool(gameObject);
            else
                Destroy(gameObject);
            return;
        }

        // 1. LẤY HƯỚNG BAY NHƯNG KHÓA TRỤC Y (Chỉ lướt trên mặt đất)
        Vector3 moveDirection = transform.forward;
        moveDirection.y = 0f; 
        moveDirection.Normalize(); 

        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        // 2. CUỐN QUÁI ĐI THEO
        for (int i = trappedEnemies.Count - 1; i >= 0; i--)
        {
            if (trappedEnemies[i] != null)
            {
                trappedEnemies[i].transform.position = Vector3.Lerp(
                    trappedEnemies[i].transform.position, 
                    transform.position, 
                    pullForce * Time.deltaTime
                );
            }
            else
            {
                trappedEnemies.RemoveAt(i);
            }
        }

        // 3. GIẬT MÁU THEO THỜI GIAN
        tickTimer += Time.deltaTime;
        if (tickTimer >= tickRate)
        {
            ApplyDamage();
            tickTimer = 0;
        }
    }

    private void ApplyDamage()
    {
        for (int i = damageableTargets.Count - 1; i >= 0; i--)
        {
            if (damageableTargets[i] != null)
                damageableTargets[i].TakeDamage(damagePerTick, transform.position);
            else
                damageableTargets.RemoveAt(i);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.isTrigger) return;
        
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            if (!trappedEnemies.Contains(other)) trappedEnemies.Add(other);
            if (!damageableTargets.Contains(target)) damageableTargets.Add(target);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (trappedEnemies.Contains(other)) trappedEnemies.Remove(other);
        
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target != null && damageableTargets.Contains(target)) damageableTargets.Remove(target);
    }
}