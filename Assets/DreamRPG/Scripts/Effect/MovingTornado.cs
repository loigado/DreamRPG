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
    private float lifeTimer;

    private List<Collider> trappedEnemies = new List<Collider>();
    private List<IDamageable> damageableTargets = new List<IDamageable>();

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
    }

    void Update()
    {
        // 1. Quản lý việc biến mất bằng tuổi thọ
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0)
        {
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.ReturnToPool(gameObject);
            else
                Destroy(gameObject);
            return;
        }

        // 2. Di chuyển Lốc Xoáy
        Vector3 moveDirection = transform.forward;
        moveDirection.y = 0f; 
        moveDirection.Normalize(); 
        transform.position += moveDirection * moveSpeed * Time.deltaTime;

        // 3. CUỐN QUÁI ĐI THEO
        for (int i = trappedEnemies.Count - 1; i >= 0; i--)
        {
            // Kiểm tra null an toàn với Collider (Vì Collider kế thừa từ UnityEngine.Object)
            if (trappedEnemies[i] != null)
            {
                // Lưu ý: Nếu quái có CharacterController, đôi khi lệnh Lerp transform sẽ bị Controller cản lại.
                // Nếu thấy quái không bị hút mượt, bạn có thể phải báo cho quái biết nó đang bị hút để tạm tắt Controller đi.
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

        // 4. GIẬT MÁU THEO THỜI GIAN
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
            // 🟢 FIX QUAN TRỌNG: Ép kiểu về UnityEngine.Object để check null chuẩn xác!
            UnityEngine.Object unityObj = damageableTargets[i] as UnityEngine.Object;
            
            if (unityObj != null)
            {
                damageableTargets[i].TakeDamage(damagePerTick, transform.position);
            }
            else
            {
                // Nếu quái đã bị Destroy, loại bỏ khỏi danh sách
                damageableTargets.RemoveAt(i);
            }
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