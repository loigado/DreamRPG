using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WeaponDamage : MonoBehaviour
{
    private Collider myCollider;
    private float currentDamage; // 🟢 ĐÃ THÊM LẠI: Biến lưu sát thương
    private List<Collider> alreadyHit = new List<Collider>();

    [Header("Game Feel Settings")]
    public float hitStopDuration = 0.05f; 
    public GameObject hitVFXPrefab; 
    public TrailRenderer weaponTrail; 

    private void Awake()
    {
        myCollider = GetComponent<Collider>();
        myCollider.isTrigger = true;
        myCollider.enabled = false;
        if (weaponTrail != null) weaponTrail.emitting = false;
    }

    // 🟢 ĐÃ SỬA LẠI: Thêm (float damage) để nhận sát thương từ WeaponHolder
    public void OpenHitbox(float damage)
    {
        Debug.Log("<color=green>🟩 LƯỠI VŨ KHÍ ĐÃ BẬT!</color>");
        currentDamage = damage; // Nhận sát thương và lưu vào biến
        alreadyHit.Clear();
        myCollider.enabled = true;
        if (weaponTrail != null) weaponTrail.emitting = true;
    }

    public void CloseHitbox()
    {
        Debug.Log("<color=gray>⬛ ĐÃ TẮT LƯỠI VŨ KHÍ!</color>"); 
        myCollider.enabled = false;
        if (weaponTrail != null) weaponTrail.emitting = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"<color=white>Triggers hit: {other.name} (Root: {other.transform.root.name})</color>");
        
        if (other.transform.root == transform.root) 
        {
            Debug.Log($"<color=gray>Bỏ qua: chạm vào chính mình hoặc vũ khí của mình.</color>");
            return;
        }
        
        if (alreadyHit.Contains(other)) return;
        alreadyHit.Add(other);

        IDamageable target = other.GetComponentInParent<IDamageable>();
        
        if (target != null)
        {
            Debug.Log($"<color=green>Tìm thấy IDamageable trên {other.name}!</color>");
            target.TakeDamage(currentDamage, transform.root.position);
            StartCoroutine(HitStopRoutine(other));

            if (hitVFXPrefab != null)
            {
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                
                // 🟢 FIX: Gọi hiệu ứng từ Pool
                ObjectPoolManager.Instance.SpawnFromPool(hitVFXPrefab, hitPoint, Quaternion.identity);
                
                // ❌ ĐÃ XÓA: Destroy(vfx, 2f); 
                // Hiệu ứng này sẽ tự cất vào kho thông qua script VFXAutoDestroy gắn trên nó.
            }
        }
        else
        {
            Debug.Log($"<color=red>KHÔNG tìm thấy IDamageable ở cha của {other.name}! Root là: {other.transform.root.name}</color>");
            // Liệt kê thử xem trên Root có những script gì để bắt bệnh
            Component[] comps = other.transform.root.GetComponents<Component>();
            string compNames = "";
            foreach (var c in comps)
            {
                if (c != null) compNames += c.GetType().Name + ", ";
            }
            Debug.Log($"<color=yellow>Các component có trên {other.transform.root.name}: {compNames}</color>");
        }
    }

    private void OnDisable()
    {
        // Cleanup: không cần reset timeScale nữa
    }

    /// <summary>
    /// Per-entity hit freeze — chỉ freeze Animator của QUÁI BỊ ĐÁNH + Player.
    /// KHÔNG ảnh hưởng Time.timeScale toàn cục (AAA standard).
    /// </summary>
    private IEnumerator HitStopRoutine(Collider hitTarget)
    {
        // 🟢 FIX: Freeze Animator của CON QUÁI bị chém (lấy từ collider hit, KHÔNG phải từ vũ khí)
        Animator targetAnim = hitTarget != null ? hitTarget.GetComponentInParent<Animator>() : null;
        if (targetAnim != null) targetAnim.speed = 0f;

        // Freeze Player (game feel)
        Animator playerAnim = null;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerAnim = player.GetComponentInChildren<Animator>();
        if (playerAnim != null) playerAnim.speed = 0f;

        yield return new WaitForSecondsRealtime(hitStopDuration);

        // Unfreeze
        if (targetAnim != null) targetAnim.speed = 1f;
        if (playerAnim != null) playerAnim.speed = 1f;
    }
}