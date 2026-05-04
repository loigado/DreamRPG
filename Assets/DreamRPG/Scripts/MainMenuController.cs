using UnityEngine;
using UnityEngine.SceneManagement; // BẮT BUỘC phải có thư viện này để chuyển Scene

public class MainMenuController : MonoBehaviour
{
    [Header("--- Scene Settings ---")]
    [Tooltip("Gõ chính xác tên Scene game của bạn vào đây (ví dụ: GameScene)")]
    public string gameSceneName = "Level1"; 
    public GameObject optionsPanel;

    // Hàm gọi khi bấm nút PLAY
    public void PlayGame()
    {
        Debug.Log("Đang tải vào game...");
        // Tải Scene Game
        SceneManager.LoadScene(gameSceneName); 
    }

    // Hàm gọi khi bấm nút OPTIONS
    public void OpenOptions()
    {
        // Bật bảng Options lên
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    // Hàm gọi khi bấm nút QUIT
    public void QuitGame()
    {
        Debug.Log("Đã thoát game! (Lưu ý: Nút Quit chỉ hoạt động khi Build ra file .exe, trong Editor sẽ không thấy đóng cửa sổ)");
        Application.Quit();
    }
}