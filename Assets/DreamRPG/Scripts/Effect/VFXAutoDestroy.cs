using UnityEngine;

public class VFXAutoDestroy : MonoBehaviour
{
    [Tooltip("Thời gian sống của VFX trước khi cất về kho")]
    public float lifetime = 3f;
    private float timer;

    // 🟢 Chạy mỗi khi VFX được lôi từ kho ra
    private void OnEnable()
    {
        timer = lifetime;

        // Ép tất cả các hạt Particle bên trong phải chạy lại từ đầu
        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in particles)
        {
            ps.Play();
        }
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            // 🛡️ BẢO HIỂM: Cất vào kho, nếu lỡ quên tạo kho thì mới Destroy
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.ReturnToPool(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}