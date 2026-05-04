using UnityEngine;
using System;

public class SkillProjectile : MonoBehaviour
{
    private float speed;
    private float maxDistance;
    private Vector3 startPos;
    private Action<Vector3, GameObject> onImpact; 
    private bool hasHit = false;
    private bool isPiercing = false;

    // 🟢 FIX: Reset state khi lôi từ Pool ra
    private void OnEnable()
    {
        hasHit = false;
        isPiercing = false;
        onImpact = null;
    }

    public void Launch(float moveSpeed, float distance, Action<Vector3, GameObject> callback, bool piercing = false)
    {
        speed = moveSpeed;
        maxDistance = distance;
        startPos = transform.position;
        onImpact = callback;
        hasHit = false;
        isPiercing = piercing; 
    }

    void Update()
    {
        if (hasHit) return;

        transform.Translate(Vector3.forward * speed * Time.deltaTime);

        if (Vector3.Distance(startPos, transform.position) >= maxDistance)
        {
            hasHit = true;
            if (!isPiercing) 
            {
                onImpact?.Invoke(transform.position, null); 
            }
            
            // 🟢 FIX: Hết tầm xa thì cất vô kho
            ObjectPoolManager.Instance.ReturnToPool(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        if (((1 << other.gameObject.layer) & LayerMask.GetMask("Enemy")) != 0)
        {
            onImpact?.Invoke(transform.position, other.gameObject); 

            if (!isPiercing)
            {
                hasHit = true;
                ObjectPoolManager.Instance.ReturnToPool(gameObject); // 🟢 FIX
            }
        }
        else if (((1 << other.gameObject.layer) & LayerMask.GetMask("Obstacle", "Ground", "Default")) != 0)
        {
            if (!isPiercing)
            {
                onImpact?.Invoke(transform.position, null);
            }
            
            hasHit = true;
            ObjectPoolManager.Instance.ReturnToPool(gameObject); // 🟢 FIX
        }
    }
}