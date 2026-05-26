using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnData
    {
        [Tooltip("Prefab của loại quái muốn đẻ ra (vd: Skeleton, Goblin...)")]
        public GameObject enemyPrefab;
        [Tooltip("Vị trí sẽ đẻ con quái này")]
        public Transform spawnPoint;
    }

    [Header("Cài đặt Đội hình Quái")]
    [Tooltip("Danh sách các con quái và vị trí đẻ của chúng")]
    public SpawnData[] enemiesToSpawn;

    [Header("Cách thức sinh quái")]
    [Tooltip("Bật cái này nếu muốn quái tự đẻ ra ngay khi vừa Play game")]
    public bool spawnOnStart = false;

    [Tooltip("Nếu không bật Spawn On Start, quái sẽ chỉ đẻ ra khi Player dẫm vào vùng này")]
    public bool spawnOnTriggerEnter = true;

    private bool hasSpawned = false; // Đảm bảo chỉ đẻ 1 lần khi dẫm vào

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnEnemies();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Kiểm tra xem có bật chế độ chạm vào để đẻ quái không, và người chạm có phải Player không
        if (spawnOnTriggerEnter && !hasSpawned && other.CompareTag("Player"))
        {
            SpawnEnemies();
            hasSpawned = true; // Đánh dấu là đã đẻ rồi, không đẻ thêm nữa
        }
    }

    public void SpawnEnemies()
    {
        if (enemiesToSpawn == null || enemiesToSpawn.Length == 0)
        {
            Debug.LogWarning("⚠️ Spawner chưa được cài đặt Đội hình Quái!");
            return;
        }

        // Duyệt qua từng con quái trong danh sách và đẻ ra đúng vị trí của nó
        foreach (SpawnData data in enemiesToSpawn)
        {
            if (data.enemyPrefab == null || data.spawnPoint == null) continue;

            // TRIỆU HỒI TỪ OBJECT POOL
            GameObject enemy = ObjectPoolManager.Instance.SpawnFromPool(data.enemyPrefab, data.spawnPoint.position, data.spawnPoint.rotation);
            
            if (enemy != null)
            {
                // Có thể thêm hiệu ứng khói bụi, tiếng gầm gừ lúc đẻ quái ở đây
            }
        }
    }
}
