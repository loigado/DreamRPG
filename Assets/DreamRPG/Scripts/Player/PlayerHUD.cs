using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("Health UI")]
    public Image healthFill;  
    public Image healthTrail; 

    [Header("Stamina UI")]
    public Image staminaFill; 

    [Header("Low Health Effect")]
    public Image bloodScreenOverlay; // 🟢 Kéo lớp phủ máu màn hình vào đây
    public float lowHealthThreshold = 0.25f; // Mức máu báo động (Dưới 25%)
    public float flashSpeed = 5f; // Tốc độ chớp nháy (Nhịp tim)

    [Header("Player References")]
    public PlayerHealth playerHealth; 
    public StaminaSystem staminaSystem; 

    private float targetHealthFill;

    private void Start()
    {
        if (playerHealth != null)
        {
            targetHealthFill = playerHealth.CurrentHealth / playerHealth.MaxHealth; 
            if (healthFill != null) healthFill.fillAmount = targetHealthFill;
            if (healthTrail != null) healthTrail.fillAmount = targetHealthFill;
        }

        if (staminaSystem != null && staminaFill != null)
        {
            staminaFill.fillAmount = staminaSystem.CurrentStamina / staminaSystem.MaxStamina; 
        }

        // Tắt màn hình máu lúc mới vào game
        if (bloodScreenOverlay != null)
        {
            Color c = bloodScreenOverlay.color;
            c.a = 0f;
            bloodScreenOverlay.color = c;
        }
    }

    private void Update()
    {
        if (playerHealth == null) return;

        // ==========================================
        // 1. XỬ LÝ THANH MÁU & VỆT TRỄ 
        // ==========================================
        float hpPercent = playerHealth.CurrentHealth / playerHealth.MaxHealth;
        targetHealthFill = hpPercent;
        
        if (healthFill != null) healthFill.fillAmount = targetHealthFill;
        if (healthTrail != null)
        {
            healthTrail.fillAmount = Mathf.Lerp(healthTrail.fillAmount, targetHealthFill, Time.deltaTime * 3f);
        }

        // ==========================================
        // 2. XỬ LÝ THANH THỂ LỰC (STAMINA)
        // ==========================================
        if (staminaSystem != null && staminaFill != null)
        {
            staminaFill.fillAmount = staminaSystem.CurrentStamina / staminaSystem.MaxStamina; 
        }

        // ==========================================
        // 3. 🟢 XỬ LÝ HIỆU ỨNG CHỚP ĐỎ CẬN TỬ
        // ==========================================
        if (bloodScreenOverlay != null)
        {
            Color c = bloodScreenOverlay.color;

            // Nếu máu tụt xuống dưới mức báo động (ví dụ < 25%)
            if (hpPercent <= lowHealthThreshold && hpPercent > 0)
            {
                // Tính toán nhịp đập tim: Càng ít máu đập càng mạnh
                float pulseIntensity = 1f - (hpPercent / lowHealthThreshold); // Giới hạn từ 0 đến 1
                
                // Hàm PingPong tạo ra con số chạy qua chạy lại mượt mà như nhịp thở
                float alphaPulse = Mathf.PingPong(Time.time * flashSpeed, 0.6f) + 0.2f;                
                // Cập nhật độ trong suốt (Alpha)
                c.a = Mathf.Lerp(c.a, alphaPulse, Time.deltaTime * 10f);
            }
            else
            {
                // Máu an toàn -> Mờ dần về 0
                c.a = Mathf.Lerp(c.a, 0f, Time.deltaTime * 5f);
            }

            bloodScreenOverlay.color = c;
        }
    }
}