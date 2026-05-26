using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WeaponDamage : MonoBehaviour
{
    private Collider myCollider;
    private float currentDamage; 
    private List<Collider> alreadyHit = new List<Collider>();
    private bool isHitboxOpen = false; // Cờ kiểm soát trạng thái quét liên tục

    [Header("Weapon Type Settings")]
    [Tooltip("Nếu tích chọn, vũ khí này sẽ LUÔN gây đòn nặng (Heavy Hit) lên quái, ví dụ Rìu")]
    public bool isHeavyWeapon = false;

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

    public void OpenHitbox(float damage)
    {
        currentDamage = damage; 
        alreadyHit.Clear();
        myCollider.enabled = true;
        isHitboxOpen = true; // Bật cờ quét chủ động
        if (weaponTrail != null) weaponTrail.emitting = true;
    }

    public void CloseHitbox()
    {
        myCollider.enabled = false;
        isHitboxOpen = false; // Tắt cờ quét
        if (weaponTrail != null) weaponTrail.emitting = false;
    }

    private void Update()
    {
        // Nếu hitbox chưa mở, không tốn hiệu năng quét
        if (!isHitboxOpen) return;

        // 🟢 AAA Polish: Quét liên tục một khối hộp theo kích thước chuẩn của Collider vũ khí
        Collider[] hits = Physics.OverlapBox(
            myCollider.bounds.center, 
            myCollider.bounds.extents, 
            transform.rotation, 
            LayerMask.GetMask("Enemy")
        );

        foreach (Collider hit in hits)
        {
            if (!alreadyHit.Contains(hit))
            {
                alreadyHit.Add(hit); // Tránh đa sát thương trong 1 đòn vung
                
                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    // 🟢 Truyền cờ isHeavyWeapon vào
                    damageable.TakeDamage(currentDamage, transform.position, isHeavyWeapon);
                    
                    // Tạo hiệu ứng chém trúng (VFX) từ Pool nếu có cấu hình
                    if (hitVFXPrefab != null && ObjectPoolManager.Instance != null)
                    {
                        Vector3 hitPoint = hit.ClosestPointOnBounds(myCollider.bounds.center);
                        ObjectPoolManager.Instance.SpawnFromPool(hitVFXPrefab, hitPoint, Quaternion.identity);
                    }

                    StartCoroutine(HitStopRoutine(hit)); 
                }
            }
        }
    }

    private IEnumerator HitStopRoutine(Collider hitTarget)
    {
        Animator targetAnim = hitTarget != null ? hitTarget.GetComponentInParent<Animator>() : null;
        if (targetAnim != null) targetAnim.speed = 0f;

        Animator playerAnim = null;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerAnim = player.GetComponentInChildren<Animator>();
        if (playerAnim != null) playerAnim.speed = 0f;

        yield return new WaitForSecondsRealtime(hitStopDuration);

        if (targetAnim != null) targetAnim.speed = 1f;
        if (playerAnim != null) playerAnim.speed = 1f;
    }
}