using UnityEngine;
using UnityEngine.UI;

public class OptionsUIController : MonoBehaviour
{
    public Slider volumeSlider;
    public Slider brightnessSlider;
    public Button closeButton;

    private void Start()
    {
        // Khi mở bảng lên, cập nhật thanh trượt khớp với dữ liệu đã lưu
        volumeSlider.value = PlayerPrefs.GetFloat("GameVolume", 1f);
        brightnessSlider.value = PlayerPrefs.GetFloat("GameBrightness", 1f);

        // Lắng nghe sự kiện kéo thanh trượt
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
        
        // Lắng nghe nút Đóng
        closeButton.onClick.AddListener(ClosePanel);
    }

    private void OnVolumeChanged(float value)
    {
        GlobalManager.instance.SetVolume(value);
    }

    private void OnBrightnessChanged(float value)
    {
        GlobalManager.instance.SetBrightness(value);
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false); // Ẩn cái bảng này đi
    }
}