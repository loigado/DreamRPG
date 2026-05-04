using UnityEngine;
using UnityEngine.UI;

public class GlobalManager : MonoBehaviour
{
    public static GlobalManager instance; // Biến Singleton gọi từ mọi nơi

    [Header("--- UI References ---")]
    public Image brightnessOverlay; // Kéo thả cái ảnh màu đen vào đây

    private void Awake()
    {
        // Đảm bảo chỉ có 1 GlobalManager duy nhất tồn tại
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // BẤT TỬ QUA MỌI SCENE
            LoadSettings(); // Tải cài đặt đã lưu
        }
        else
        {
            Destroy(gameObject); // Xóa bản sao nếu lỡ quay lại Main Menu
        }
    }

    // Hàm chỉnh Âm lượng
    public void SetVolume(float volume)
    {
        AudioListener.volume = volume; // Hàm có sẵn của Unity để chỉnh âm lượng tổng
        PlayerPrefs.SetFloat("GameVolume", volume); // Lưu lại
    }

    // Hàm chỉnh Độ sáng
    public void SetBrightness(float brightnessValue)
    {
        // brightnessValue: 1 là sáng nhất (alpha 0), 0 là tối nhất (alpha 1)
        if (brightnessOverlay != null)
        {
            Color c = brightnessOverlay.color;
            c.a = 1f - brightnessValue; // Lật ngược giá trị cho Slider
            brightnessOverlay.color = c;
        }
        PlayerPrefs.SetFloat("GameBrightness", brightnessValue); // Lưu lại
    }

    // Tải lại cài đặt mỗi khi mở game
    private void LoadSettings()
    {
        float savedVolume = PlayerPrefs.GetFloat("GameVolume", 1f);
        SetVolume(savedVolume);

        float savedBrightness = PlayerPrefs.GetFloat("GameBrightness", 1f);
        SetBrightness(savedBrightness);
    }
}