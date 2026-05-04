using UnityEngine;

public class PhantomController : MonoBehaviour
{
    private Animator myAnimator;
    private GameObject projectilePrefab;
    private float projSpeed;
    private float maxDist;
    private float phantomDamage;

    private void Awake()
    {
        myAnimator = GetComponent<Animator>();
    }

    // 🟢 THÊM: Đảm bảo Animator reset state khi gọi từ Pool
    private void OnEnable()
    {
        if (myAnimator != null)
        {
             myAnimator.speed = 1f;
        }
    }

    public void Setup(GameObject prefab, float speed, float distance, float damage)
    {
        projectilePrefab = prefab;
        projSpeed = speed;
        maxDist = distance;
        phantomDamage = damage;
        
        if (myAnimator != null)
        {
            myAnimator.CrossFadeInFixedTime("LightningSwordCharge", 0.1f);
        }
    }

    public void ExecuteSlash()
    {
        if (myAnimator != null)
        {
            myAnimator.speed = 2f; 
            myAnimator.CrossFadeInFixedTime("LightningSword", 0.05f); 
        }
    }

    public void AE_FireSkillProjectile()
    {
        if (projectilePrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 1f;
            
            // 🟢 THAY THẾ: Dùng Object Pool thay vì Instantiate
            GameObject wave = ObjectPoolManager.Instance.SpawnFromPool(projectilePrefab, spawnPos, transform.rotation);
            
            if (wave != null && wave.TryGetComponent<SkillProjectile>(out SkillProjectile proj))
            {
                proj.Launch(projSpeed, maxDist, HandlePhantomImpact, true); 
            }
        }
    }

    private void HandlePhantomImpact(Vector3 hitPoint, GameObject hitObj)
    {
        if (hitObj != null)
        {
            if (hitObj.TryGetComponent<IDamageable>(out IDamageable damageable))
            {
                damageable.TakeDamage(phantomDamage, transform.position);
            }
        }
    }
}