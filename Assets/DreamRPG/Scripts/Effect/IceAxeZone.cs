using UnityEngine;

/// <summary>
/// IceAxeZone — Vùng băng giá tồn tại trên mặt đất.
/// 
/// Chức năng:
///   • Gây sát thương nhẹ liên tục cho quái đứng trong vùng
///   • Làm chậm quái 60% tốc độ (slow 0.4)
///   • Tự động cất vào Pool khi hết thời gian
/// </summary>
public class IceAxeZone : MonoBehaviour
{
    private float lifeTime;
    private float damageMult;

    private void Awake()
    {
        // 🛡️ TỰ ĐỘNG THIẾT LẬP TẤT CẢ COLLIDER THÀNH TRIGGER
        Collider[] colliders = GetComponents<Collider>();
        foreach (var c in colliders)
        {
            c.isTrigger = true;
        }

        // Nếu hoàn toàn không có Collider thì mới thêm mới
        if (colliders.Length == 0)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 6f;
        }
    }

    private void OnEnable()
    {
        lifeTime = 0f;
        damageMult = 0f;
    }

    public void Setup(float duration, float damageMultiplier)
    {
        this.lifeTime = duration;
        this.damageMult = damageMultiplier;
    }

    private void Update()
    {
        if (lifeTime > 0)
        {
            lifeTime -= Time.deltaTime;
            if (lifeTime <= 0)
            {
                // 🟢 FIX: Dùng Pool thay vì Destroy
                if (ObjectPoolManager.Instance != null)
                    ObjectPoolManager.Instance.ReturnToPool(gameObject);
                else
                    Destroy(gameObject);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.TryGetComponent<EnemyHealth>(out EnemyHealth target))
        {
            // ❄️ CHỈ LÀM CHẬM — Không gây sát thương (sát thương chỉ khi nổ)
            target.ApplySlow(0.4f, 0.5f);
        }
    }
}