using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// WindArrow — Mũi tên gió (skill bow).
/// 
/// 🟢 FIX: Dùng Layer "Enemy" thay vì Tag "Enemy" để nhất quán với ArrowProjectile.
/// 🟢 FIX: Truyền shooterPosition thay vì arrow position cho TakeDamage.
/// </summary>
public class WindArrow : MonoBehaviour
{
    private float damage;
    private float knockbackForce;
    private Rigidbody rb;
    private bool hasGravity = false;
    
    private List<Collider> hitEnemies = new List<Collider>();
    private float lifeTimer;
    private Vector3 shooterPosition; // 🟢 FIX: Lưu vị trí Player lúc bắn

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // 🟢 FIX: Reset lại mọi thứ khi móc từ kho ra
    private void OnEnable()
    {
        hitEnemies.Clear();
        lifeTimer = 3f;
    }

    public void Setup(float dmg, float force, float speed, bool applyGravity = false)
    {
        damage = dmg;
        knockbackForce = force;
        hasGravity = applyGravity;

        // 🟢 FIX: Lưu vị trí Player lúc bắn
        var player = GameObject.FindGameObjectWithTag("Player");
        shooterPosition = player != null ? player.transform.position : transform.position;

        if (rb != null)
        {
            rb.useGravity = applyGravity;
            if (applyGravity) rb.constraints = RigidbodyConstraints.None;
            else rb.constraints = RigidbodyConstraints.FreezeRotation;

            rb.linearVelocity = transform.forward * speed;
        }
    }

    void Update()
    {
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0)
        {
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.ReturnToPool(gameObject);
            else
                Destroy(gameObject);
            return;
        }

        if (hasGravity && rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            transform.forward = rb.linearVelocity.normalized;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 🟢 FIX: Dùng LAYER thay vì TAG để nhất quán với ArrowProjectile
        if (other.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            if (!hitEnemies.Contains(other))
            {
                if (other.TryGetComponent<IDamageable>(out IDamageable target))
                {
                    // 🟢 FIX: Truyền vị trí PLAYER (shooter) chứ không phải vị trí mũi tên
                    target.TakeDamage(damage, shooterPosition);
                }
                hitEnemies.Add(other);
            }
        }
        else if (other.gameObject.layer == LayerMask.NameToLayer("Ground") || other.gameObject.layer == LayerMask.NameToLayer("Environment"))
        {
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.ReturnToPool(gameObject);
            else
                Destroy(gameObject);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // 🟢 FIX: Dùng LAYER thay vì TAG
        if (other.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            if (other.TryGetComponent<Rigidbody>(out Rigidbody enemyRb))
            {
                Vector3 pushDir = transform.forward;
                pushDir.y = 0; 
                
                float pushSpeed = (rb != null && rb.linearVelocity.magnitude > 0) ? rb.linearVelocity.magnitude : knockbackForce;

                enemyRb.linearVelocity = new Vector3(pushDir.x * (pushSpeed * 1.1f), enemyRb.linearVelocity.y, pushDir.z * (pushSpeed * 1.1f));
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Enemy") && hitEnemies.Contains(other))
        {
            hitEnemies.Remove(other); 
        }
    }
}