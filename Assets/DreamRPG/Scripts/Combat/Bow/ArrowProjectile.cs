using UnityEngine;

public class ArrowProjectile : MonoBehaviour
{
    private Rigidbody rb;
    private Collider myCollider;
    private bool hasHit = false;
    private float damage;
    private Vector3 shooterPosition; // Vị trí Player lúc bắn

    [SerializeField] private float lifeTime = 10f;
    private float lifeTimer; // 🟢 FIX: Thay Invoke/Destroy bằng bộ đếm thời gian tự thân

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myCollider = GetComponent<Collider>();
    }

    // 🟢 FIX: Hàm này chạy mỗi khi mũi tên được kéo ra từ Pool
    private void OnEnable()
    {
        hasHit = false;
        if (myCollider != null) myCollider.enabled = true;
        if (rb != null) 
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero; // Reset lực cũ
        }
        lifeTimer = lifeTime; // Cài đặt lại đồng hồ đếm ngược
        transform.SetParent(null); // Gỡ khỏi xác quái cũ (nếu có)
    }

    public void Launch(float force, float damageValue)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        
        damage = damageValue;
        // 🟢 FIX: Lưu vị trí Player lúc bắn để truyền cho quái biết kẻ tấn công ở đâu
        var player = GameObject.FindGameObjectWithTag("Player");
        shooterPosition = player != null ? player.transform.position : transform.position;
        rb.isKinematic = false;
        rb.linearVelocity = transform.forward * force; 
    }

    void FixedUpdate()
    {
        // 🟢 FIX: Đếm thời gian sống, nếu hết thì cất vào Pool
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
            // Nếu đã ghim vào tường/quái, cũng cho nó một bộ đếm để tự biến mất sau 5-10s
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

            // 🟢 FIX: Truyền vị trí NGƯỜI BẮN (Player) chứ không phải vị trí mũi tên va chạm
            // Để quái biết kẻ tấn công ở xa → kích hoạt phản ứng né/đỡ mũi tên
            target.TakeDamage(damage, shooterPosition);
            
            lifeTimer = 5f; // Ghim vào quái 5s rồi tự biến mất về Pool
        }
        else if (hitCollider.gameObject.layer == LayerMask.NameToLayer("Environment"))
        {
            hasHit = true;
            
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            
            if (myCollider != null) myCollider.enabled = false;
            
            transform.SetParent(hitCollider.transform, true);

            lifeTimer = 10f; // Ghim vào tường 10s rồi về Pool
        }
    }
}