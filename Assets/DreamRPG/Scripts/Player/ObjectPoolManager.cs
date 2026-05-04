using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    // 🟢 THÊM BIẾN BÁO HIỆU ĐANG DỌN KHO
    public bool IsPrewarming { get; private set; } 

    [System.Serializable]
    public class Pool
    {
        public string poolName; 
        public GameObject prefab; 
        public int size;          

        [Header("Giới hạn Pool")]
        [Tooltip("Bật: Hết hàng tự đẻ thêm. Tắt: Hết hàng thì tịt luôn không đẻ nữa")]
        public bool canExpand = true; 
    }

    public List<Pool> pools;
    private Dictionary<string, Queue<GameObject>> poolDictionary;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 🟢 BẬT BIỂN BÁO: "ĐANG DỌN KHO, CẤM XÀI SKILL!"
        IsPrewarming = true; 

        poolDictionary = new Dictionary<string, Queue<GameObject>>();

        GameObject quarantineZone = new GameObject("QuarantineZone");
        quarantineZone.SetActive(false); 
        quarantineZone.transform.SetParent(this.transform);

        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();

            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab, quarantineZone.transform);
                obj.name = pool.prefab.name; 
                obj.SetActive(false); // Lệnh này gọi OnDisable() của PhantomVFX, nhưng nhờ có biển báo nên sẽ bị chặn lại!
                obj.transform.SetParent(this.transform); 
                objectPool.Enqueue(obj);
            }

            if (poolDictionary.ContainsKey(pool.prefab.name))
            {
                foreach (var obj in objectPool) poolDictionary[pool.prefab.name].Enqueue(obj);
            }
            else
            {
                poolDictionary.Add(pool.prefab.name, objectPool);
            }
        }
        
        Destroy(quarantineZone); 

        // 🟢 TẮT BIỂN BÁO: DỌN XONG RỒI, ANH EM XÀI THOẢI MÁI!
        IsPrewarming = false; 
    }

    public GameObject SpawnFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        string poolKey = prefab.name;

        if (!poolDictionary.ContainsKey(poolKey))
        {
            poolDictionary[poolKey] = new Queue<GameObject>();
        }

        GameObject objectToSpawn = null;

        if (poolDictionary[poolKey].Count > 0)
        {
            objectToSpawn = poolDictionary[poolKey].Dequeue();
            pooledObjects.Remove(objectToSpawn); // 🟢 FIX: Đồng bộ HashSet khi lấy ra
        }
        else
        {
            Pool poolConfig = pools.Find(p => p.prefab.name == poolKey);
            
            if (poolConfig != null && !poolConfig.canExpand)
            {
                return null; 
            }

            objectToSpawn = Instantiate(prefab);
            objectToSpawn.name = prefab.name;
        }

        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        objectToSpawn.transform.SetParent(null); 
        objectToSpawn.SetActive(true);

        return objectToSpawn;
    }
    // 🟢 FIX: HashSet để kiểm tra trùng lặp O(1) thay vì Queue.Contains() O(n)
    private readonly HashSet<GameObject> pooledObjects = new HashSet<GameObject>();

    public void ReturnToPool(GameObject obj)
    {
        if (obj == null) return;

        // 🟢 FIX: Kiểm tra O(1) thay vì O(n)
        if (pooledObjects.Contains(obj)) return; // Đã nằm trong pool rồi, không trả lại nữa

        obj.SetActive(false);
        obj.transform.SetParent(this.transform); 

        string poolKey = obj.name;
        if (!poolDictionary.ContainsKey(poolKey))
        {
            poolDictionary[poolKey] = new Queue<GameObject>();
        }
        poolDictionary[poolKey].Enqueue(obj);
        pooledObjects.Add(obj);
    }
}