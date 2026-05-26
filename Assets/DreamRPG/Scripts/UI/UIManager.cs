using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("Menu Panels")]
    [Tooltip("Kéo Panel Pause Menu vào đây")]
    public GameObject pauseMenuPanel;
    
    [Tooltip("Kéo Panel Game Over vào đây")]
    public GameObject gameOverMenuPanel;

    [Tooltip("Kéo Panel Victory / End Game vào đây")]
    public GameObject victoryMenuPanel;

    private bool isPaused = false;
    private PlayerHealth playerHealth;

    private void Start()
    {
        // Ẩn các menu lúc mới vào game
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (gameOverMenuPanel != null) gameOverMenuPanel.SetActive(false);
        if (victoryMenuPanel != null) victoryMenuPanel.SetActive(false);

        // Tự động tìm Player và kết nối với hệ thống Máu
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponentInChildren<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnDeath += HandleGameOver; // Lắng nghe sự kiện chết
            }
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandleGameOver;
        }
    }

    private void Update()
    {
        // Nếu Player chết rồi thì không cho phép nhấn Pause nữa
        if (playerHealth != null && playerHealth.IsDead) return;

        // Nhấn nút ESC để Bật/Tắt Pause Menu
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        
        // ĐÓNG BĂNG THỜI GIAN
        Time.timeScale = 0f; 
        
        // Hiện con trỏ chuột lên để click nút
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        
        // TRẢ LẠI THỜI GIAN
        Time.timeScale = 1f; 
        
        // Ẩn con trỏ chuột đi để tiếp tục quay camera
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void HandleGameOver()
    {
        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        // Đợi 2.5 giây cho Player diễn xong hoạt ảnh ngã xuống sàn rồi mới hiện Game Over
        yield return new WaitForSeconds(2.5f);
        
        if (gameOverMenuPanel != null) gameOverMenuPanel.SetActive(true);
        
        // Hiện con trỏ chuột lên
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ==========================================
    // CÁC HÀM NÀY DÙNG ĐỂ GẮN VÀO CÁC NÚT (BUTTON)
    // ==========================================

    // Gắn vào nút "Restart" / "Play Again"
    public void RestartGame()
    {
        Time.timeScale = 1f; // Nhớ trả lại thời gian trước khi load cảnh
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Gắn vào nút "Main Menu" / "Quit Game"
    public void QuitToMainMenu()
    {
        Time.timeScale = 1f; 
        // BẠN HÃY SỬA CHỮ "MainMenu" BÊN DƯỚI THÀNH TÊN SCENE MAIN MENU CỦA BẠN NHÉ
        SceneManager.LoadScene("MainMenu"); 
    }

    // ==========================================
    // GỌI HÀM NÀY KHI BOSS CHẾT
    // ==========================================
    public void ShowVictoryMenu()
    {
        StartCoroutine(VictoryRoutine());
    }

    private IEnumerator VictoryRoutine()
    {
        // Đợi 3 giây cho ngầu (nhìn Boss chết, hiệu ứng cháy nổ...)
        yield return new WaitForSeconds(3f);
        
        if (victoryMenuPanel != null) victoryMenuPanel.SetActive(true);
        
        // ĐÓNG BĂNG THỜI GIAN
        Time.timeScale = 0f;

        // Hiện con trỏ chuột lên
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
