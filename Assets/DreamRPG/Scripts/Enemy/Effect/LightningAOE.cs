using UnityEngine;

public class LightningAOE : MonoBehaviour
{
    [Tooltip("Thời gian từ lúc hiện vòng đỏ tới lúc sét giật xuống")]
    public float delayTime = 1.0f; 
    [Tooltip("Bán kính sát thương")]
    public float radius = 2.5f;
    [Tooltip("Prefab tia sét cắm từ trời xuống (VFX thực sự)")]
    public GameObject lightningStrikeVFX; 

    private float damage;
    private SkillElement element;
    private float timer;
    private bool hasStruck;
    // 🟢 BIẾN MỚI: Đánh dấu xem đây là đòn nổ ngay hay có thời gian chờ
    private bool isInstantMode; 

    // 🟢 CẬP NHẬT: Thêm tham số isInstant vào cuối hàm Setup
    public void Setup(float enemyDamage, SkillElement enemyElement, bool isInstant = false)
    {
        damage = enemyDamage;
        element = enemyElement;
        isInstantMode = isInstant;

        if (isInstant)
        {
            // Nổ ngay lập tức
            ExecuteStrike();
        }
        else
        {
            timer = delayTime;
            hasStruck = false;
        }
    }

    private void OnEnable()
    {
        timer = delayTime;
        hasStruck = false;
        isInstantMode = false;
    }

    private void Update()
    {
        if (hasStruck) return;

        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            ExecuteStrike();
        }
    }

    private void ExecuteStrike()
    {
        hasStruck = true;

        // 1. Spawn tia sét chớp nhoáng (VFX)
        if (lightningStrikeVFX != null)
        {
            ObjectPoolManager.Instance.SpawnFromPool(lightningStrikeVFX, transform.position, Quaternion.identity);
        }

        // 2. Gây sát thương nổ AOE
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, EnemyConstants.PlayerLayerMask);
        foreach (var hit in hits)
        {
            PlayerStateMachine player = hit.GetComponentInParent<PlayerStateMachine>();
            if (player != null)
            {
                // Gọi TakeHeavyDamage -> Sét xuyên khiên, Kratos phải lộn nhào Perfect Dodge mới né được!
                player.TakeHeavyDamage(damage, transform.position, Vector3.zero);
            }
        }

        // 3. 🟢 CHỈ trả vòng cảnh báo (Decal) về Pool nếu nó là đòn đánh xa.
        // Còn nếu là đòn đánh gần (Instant), ta để hệ thống Particle System của bạn tự động tắt/thu hồi nó.
        if (!isInstantMode)
        {
            ObjectPoolManager.Instance.ReturnToPool(gameObject);
        }
    }
}