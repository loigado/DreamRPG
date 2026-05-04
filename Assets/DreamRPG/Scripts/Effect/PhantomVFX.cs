using UnityEngine;

public class PhantomVFX : MonoBehaviour
{
    [Header("Kéo Prefab Hiệu ứng vào đây")]
    public GameObject spawnEffect;   
    public GameObject despawnEffect; 

    void OnEnable()
    {
        // 🟢 CHỐT CHẶN: Đang dọn kho thì nghỉ sinh hiệu ứng
        if (ObjectPoolManager.Instance != null && ObjectPoolManager.Instance.IsPrewarming) return;

        if (spawnEffect != null && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.SpawnFromPool(spawnEffect, transform.position, transform.rotation);
        }
    }

    void OnDisable()
    {
        if (!gameObject.scene.isLoaded) return;
        if (ObjectPoolManager.Instance == null) return;

        // 🟢 CHỐT CHẶN TƯƠNG TỰ BÊN DƯỚI
        if (ObjectPoolManager.Instance.IsPrewarming) return;

        if (despawnEffect != null)
        {
            ObjectPoolManager.Instance.SpawnFromPool(despawnEffect, transform.position, transform.rotation);
        }
    }
}