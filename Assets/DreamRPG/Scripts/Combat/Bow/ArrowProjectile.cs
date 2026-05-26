using UnityEngine;

public class ArrowProjectile : MonoBehaviour
{
    private Rigidbody rb;
    private Collider myCollider;
    private bool hasHit = false;
    private float damage;
    private Vector3 shooterPosition; 

    [SerializeField] private float lifeTime = 10f;
    private float lifeTimer; 

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        hasHit = false;
        if (myCollider != null) myCollider.enabled = true;
        if (rb != null) 
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero; 
        }
        lifeTimer = lifeTime; 
        transform.SetParent(null); 
    }

    public void Launch(float force, float damageValue)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        
        damage = damageValue;
        var player = GameObject.FindGameObjectWithTag("Player");
        shooterPosition = player != null ? player.transform.position : transform.position;
        rb.isKinematic = false;
        rb.linearVelocity = transform.forward * force; 
    }

    void FixedUpdate()
    {
        if (!hasHit)
        {
            lifeTimer -= Time.fixedDeltaTime;
            if (lifeTimer <= 0)
            {
                ObjectPoolManager.Instance.ReturnToPool(gameObject);
                return;
            }
        }
        else
        {
            lifeTimer -= Time.fixedDeltaTime;
            if (lifeTimer <= 0) ObjectPoolManager.Instance.ReturnToPool(gameObject);
            return;
        }

        if (rb.isKinematic) return;

        float distanceThisFrame = rb.linearVelocity.magnitude * Time.fixedDeltaTime;

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, distanceThisFrame + 0.5f))
        {
            ExecuteHit(hit.collider);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit || rb.isKinematic) return;
        ExecuteHit(other);
    }

    private void ExecuteHit(Collider hitCollider)
    {
        // Bỏ qua Player và các Trigger (tránh lỗi đâm vào hitbox của quái chưa kích hoạt)
        if (hitCollider.CompareTag("Player")) return; 
        if (hitCollider.isTrigger) return; 

        IDamageable target = hitCollider.GetComponentInParent<IDamageable>();

        if (target != null)
        {
            hasHit = true;
            
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero; 
            rb.isKinematic = true;

            if (myCollider != null) myCollider.enabled = false;

            transform.SetParent(hitCollider.transform, true); 

            // Gọi TakeDamage bình thường và truyền vị trí Kratos
            target.TakeDamage(damage, shooterPosition);
            
            lifeTimer = 5f; 
        }
        else if (hitCollider.gameObject.layer == LayerMask.NameToLayer("Environment"))
        {
            hasHit = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            if (myCollider != null) myCollider.enabled = false;
            transform.SetParent(hitCollider.transform, true);
            lifeTimer = 10f; 
        }
    }
}