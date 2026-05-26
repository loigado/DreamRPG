using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHUD : MonoBehaviour
{
    public static BossHUD Instance { get; private set; }

    [Header("UI Components")]
    public CanvasGroup canvasGroup;    
    public TextMeshProUGUI bossNameText;
    public Image hpFill;
    public Image hpTrail;
    public Image guardFill;

    // 🟢 THÊM MỚI: POSTURE BOSS
    [Header("Posture UI (Phá Giáp Boss)")]
    public CanvasGroup postureCanvasGroup; // Tùy chọn để ẩn/hiện
    public Image postureFill;

    [Header("Settings")]
    public float fadeSpeed = 2f;
    public float trailSpeed = 3f;

    private EnemyHealth targetBossHealth;
    private float maxGuardCached;
    private float maxPostureCached;
    private bool isVisualActive = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (postureCanvasGroup != null) postureCanvasGroup.alpha = 0f;
    }

    public void SetupBossHUD(EnemyHealth bossHealth, string bossName)
    {
        targetBossHealth = bossHealth;
        bossNameText.text = bossName;

        var stateMachine = bossHealth.GetComponent<EnemyStateMachine>();
        if (stateMachine != null && stateMachine.Stats != null)
        {
            maxGuardCached = stateMachine.Stats.maxGuard;
            maxPostureCached = stateMachine.Stats.maxPosture; // 🟢 Lấy thông số Posture Boss
        }

        hpFill.fillAmount = 1f;
        hpTrail.fillAmount = 1f;
        
        if (maxGuardCached > 0)
        {
            guardFill.gameObject.SetActive(true);
            guardFill.fillAmount = 1f;
        }
        else guardFill.gameObject.SetActive(false); 

        // Khởi tạo Posture rỗng
        if (postureFill != null) postureFill.fillAmount = 0f;

        isVisualActive = true;
    }

    private void Update()
    {
        float targetAlpha = isVisualActive ? 1f : 0f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);

        if (targetBossHealth == null || !isVisualActive) return;

        if (targetBossHealth.IsDead)
        {
            isVisualActive = false;
            return;
        }

        float hpPercent = targetBossHealth.Health / targetBossHealth.MaxHealth;
        hpFill.fillAmount = hpPercent;
        hpTrail.fillAmount = Mathf.Lerp(hpTrail.fillAmount, hpPercent, Time.deltaTime * trailSpeed);

        if (maxGuardCached > 0)
        {
            guardFill.fillAmount = targetBossHealth.CurrentGuard / maxGuardCached;
        }

        // 🟢 Cập nhật thanh vàng của Boss
        if (maxPostureCached > 0f && postureFill != null)
        {
            postureFill.fillAmount = targetBossHealth.CurrentPosture / maxPostureCached;
            
            // Chỉ làm mờ thanh Posture nếu nó là một CanvasGroup riêng biệt, không phải là CanvasGroup tổng của thanh máu
            if (postureCanvasGroup != null && postureCanvasGroup != canvasGroup)
            {
                float postureAlpha = targetBossHealth.CurrentPosture > 0.01f ? 1f : 0f;
                postureCanvasGroup.alpha = Mathf.MoveTowards(postureCanvasGroup.alpha, postureAlpha, Time.deltaTime * 3f);
            }
        }
    }
}