using UnityEngine;
using UnityEngine.UI; // Thư viện bắt buộc để dùng UI của Unity

public class StaminaBarUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image staminaFillImage; // Kéo thả StaminaBar_Fill vào đây
    [SerializeField] private StaminaSystem playerStamina; // Kéo thả Nhân vật vào đây

    private void OnEnable()
    {
        // Khi UI được bật lên, nó "đăng ký" lắng nghe sự kiện từ StaminaSystem
        if (playerStamina != null)
        {
            playerStamina.OnStaminaChanged += UpdateStaminaBar;
            
            // Cập nhật ngay giá trị lần đầu tiên khi vừa vào game
            UpdateStaminaBar(playerStamina.CurrentStamina / playerStamina.MaxStamina);
        }
    }

    private void OnDisable()
    {
        // BẮT BUỘC: Hủy đăng ký khi tắt UI để tránh lỗi Memory Leak (rò rỉ bộ nhớ)
        if (playerStamina != null)
        {
            playerStamina.OnStaminaChanged -= UpdateStaminaBar;
        }
    }

    // Hàm này sẽ tự động chạy mỗi khi StaminaSystem gọi OnStaminaChanged?.Invoke()
    private void UpdateStaminaBar(float normalizedStamina)
    {
        // normalizedStamina là tỷ lệ phần trăm (từ 0.0 đến 1.0)
        staminaFillImage.fillAmount = normalizedStamina;
    }
}