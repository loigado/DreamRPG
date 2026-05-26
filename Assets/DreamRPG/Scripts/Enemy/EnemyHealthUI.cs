using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthUI : MonoBehaviour
{
    [Header("Health UI")]
    public Image healthFill;

    [Header("Super Armor / Guard UI")]
    public GameObject guardBarContainer; 
    public Image guardFill;

    // 🟢 THÊM MỚI: GIAO DIỆN POSTURE (PHÁ GIÁP)
    [Header("Posture UI")]
    public CanvasGroup postureCanvasGroup; // Để ẩn hiện thanh vàng mượt mà
    public Image postureFill;

    private EnemyHealth enemyHealth;
    private Transform mainCamera;
    
    private float maxGuardCached;
    private float maxPostureCached;

    private void Awake()
    {
        enemyHealth = GetComponentInParent<EnemyHealth>();
    }

    private void Start()
    {
        if (Camera.main != null)
        {
            mainCamera = Camera.main.transform;
        }

        if (enemyHealth != null)
        {
            var stateMachine = enemyHealth.GetComponent<EnemyStateMachine>();
            if (stateMachine != null && stateMachine.Stats != null)
            {
                maxGuardCached = stateMachine.Stats.maxGuard; 
                maxPostureCached = stateMachine.Stats.maxPosture; // 🟢 Lấy thông số Posture tối đa
            }

            if (maxGuardCached <= 0f && guardBarContainer != null)
            {
                guardBarContainer.SetActive(false);
            }

            // Ẩn thanh Posture lúc mới vào game
            if (postureCanvasGroup != null) postureCanvasGroup.alpha = 0f;
        }
    }

    private void Update()
    {
        if (enemyHealth == null) return;

        // 1. Cập nhật thanh Máu (HP)
        healthFill.fillAmount = enemyHealth.Health / enemyHealth.MaxHealth; 

        // 2. Cập nhật thanh Super Armor (Guard) 
        if (maxGuardCached > 0f && guardFill != null)
        {
            guardFill.fillAmount = enemyHealth.CurrentGuard / maxGuardCached; 
        }

        // 3. 🟢 Cập nhật thanh Posture (Thanh Vàng)
        if (maxPostureCached > 0f && postureFill != null)
        {
            postureFill.fillAmount = enemyHealth.CurrentPosture / maxPostureCached;

            // Hiệu ứng ẩn/hiện mượt mà (Chỉ hiện khi quái bị tích điểm Posture)
            if (postureCanvasGroup != null)
            {
                float targetAlpha = enemyHealth.CurrentPosture > 0.01f ? 1f : 0f;
                postureCanvasGroup.alpha = Mathf.MoveTowards(postureCanvasGroup.alpha, targetAlpha, Time.deltaTime * 3f);
            }
        }
    }

    private void LateUpdate()
    {
        if (mainCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.position);
        }
    }
}