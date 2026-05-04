using System;
using UnityEngine;

public class StaminaSystem : MonoBehaviour
{
    [Header("Stamina Settings")]
    public float MaxStamina = 100f;
    public float RegenRate = 20f; // Số Stamina hồi mỗi giây
    public float RegenDelay = 1.5f; // Thời gian chờ (giây) sau khi xài mới bắt đầu hồi

    public float CurrentStamina { get; private set; }
    private float timeSinceLastUse; // Bộ đếm thời gian chờ

    // Sự kiện này rất hữu ích để sau này update thanh UI (Thanh màu xanh lá)
    public event Action<float> OnStaminaChanged; 

    private void Start()
    {
        CurrentStamina = MaxStamina;
    }

    private void Update()
    {
        // Xử lý tự động hồi phục
        if (CurrentStamina < MaxStamina)
        {
            timeSinceLastUse += Time.deltaTime;

            // Nếu đã qua khoảng thời gian Delay thì mới bắt đầu hồi
            if (timeSinceLastUse >= RegenDelay)
            {
                CurrentStamina += RegenRate * Time.deltaTime;
                CurrentStamina = Mathf.Clamp(CurrentStamina, 0, MaxStamina);
                
                // Báo cho UI biết là Stamina vừa thay đổi (truyền vào % để làm thanh fill)
                OnStaminaChanged?.Invoke(CurrentStamina / MaxStamina);
            }
        }
    }

    // Hàm kiểm tra xem có đủ thể lực không
    public bool HasEnoughStamina(float amount)
    {
        return CurrentStamina >= amount;
    }

    // Hàm gọi khi thực hiện hành động (Roll, Jump, Attack)
    public void UseStamina(float amount)
    {
        CurrentStamina -= amount;
        // 🟢 FIX: Đảm bảo Stamina không bao giờ xuống dưới 0
        CurrentStamina = Mathf.Max(0f, CurrentStamina);
        timeSinceLastUse = 0f; // Reset lại bộ đếm Delay
        OnStaminaChanged?.Invoke(CurrentStamina / MaxStamina);
    }
}