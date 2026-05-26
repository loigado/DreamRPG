using UnityEngine;

public class BossDeathTrigger : MonoBehaviour
{
    private EnemyHealth enemyHealth;

    private void Start()
    {
        enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += HandleBossDeath;
        }
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleBossDeath;
        }
    }

    private void HandleBossDeath()
    {
        // Tìm UIManager trong game và gọi hàm ShowVictoryMenu()
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.ShowVictoryMenu();
        }
        else
        {
            Debug.LogWarning("Không tìm thấy UIManager trong Scene để hiện Menu Victory!");
        }
    }
}
