using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// LightningDomain — Vùng sấm sét gây sát thương liên tục (DoT) + tê liệt.
///
/// 🟢 FIX: Dùng OverlapSphere thay vì OnTriggerEnter/Stay
/// Lý do: Collider của VFX thường là hình ring (vòng tròn rỗng giữa)
/// → quái đứng ở trung tâm không bị chạm Collider → không dính damage
/// OverlapSphere quét toàn bộ hình cầu → quái ở bất kỳ vị trí nào trong vùng đều bị dính
/// </summary>
public class LightningDomain : MonoBehaviour
{
    [Header("Domain Settings")]
    public float lifetime = 4f; 
    public float tickRate = 0.5f;
    public float domainRadius = 6f; // Bán kính vùng sấm sét
    
    private float damagePerTick = 5f; 
    private float tickTimer = 0f;
    private float lifeTimer = 0f;
    
    // Cache quái đang bị tê liệt để giải phóng khi domain hết
    private List<IParalyzable> paralyzedEnemies = new List<IParalyzable>();
    private int enemyLayerMask;

    public void Initialize(float damage)
    {
        damagePerTick = damage;
        tickTimer = 0f;
        lifeTimer = 0f;
        paralyzedEnemies.Clear();
        enemyLayerMask = LayerMask.GetMask("Enemy");
    }

    void Update()
    {
        lifeTimer += Time.deltaTime;

        // Hết thời gian → giải phóng tê liệt và cất vào Pool
        if (lifeTimer >= lifetime)
        {
            ReleaseAllParalyzed();

            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.ReturnToPool(gameObject);
            else
                Destroy(gameObject);
            return;
        }

        tickTimer += Time.deltaTime;
        if (tickTimer >= tickRate)
        {
            DealDamageToAllInside();
            tickTimer = 0f;
        }
    }

    /// <summary>
    /// 🟢 FIX: Dùng OverlapSphere quét TOÀN BỘ vùng tròn
    /// Quái ở trung tâm cũng sẽ bị dính damage
    /// </summary>
    private void DealDamageToAllInside()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, domainRadius, enemyLayerMask);

        foreach (var hit in hits)
        {
            // Gây sát thương
            if (hit.TryGetComponent<IDamageable>(out IDamageable damageable))
            {
                damageable.TakeDamage(damagePerTick, transform.position);
            }

            // Tê liệt
            if (hit.TryGetComponent<IParalyzable>(out IParalyzable paralyzable))
            {
                paralyzable.SetParalyzed(true);
                if (!paralyzedEnemies.Contains(paralyzable))
                {
                    paralyzedEnemies.Add(paralyzable);
                }
            }
        }
    }

    /// <summary>
    /// Giải phóng tê liệt cho tất cả quái khi domain biến mất
    /// </summary>
    private void ReleaseAllParalyzed()
    {
        paralyzedEnemies.RemoveAll(p => (p as UnityEngine.Object) == null);

        foreach (var paralyzable in paralyzedEnemies)
        {
            if ((paralyzable as UnityEngine.Object) != null)
            {
                paralyzable.SetParalyzed(false);
            }
        }
        paralyzedEnemies.Clear();
    }

    private void OnDisable()
    {
        // Safety: giải phóng tê liệt khi bị Pool cất đi
        ReleaseAllParalyzed();
    }
}