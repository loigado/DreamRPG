using UnityEngine;

public class DevCheats : MonoBehaviour
{
    private CharacterController cc;
    private PlayerHealth playerHealth;
    private PlayerStateMachine stateMachine;
    
    private bool isGodMode = false;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        playerHealth = GetComponent<PlayerHealth>();
        stateMachine = GetComponent<PlayerStateMachine>();
    }

    private void Update()
    {
        // Chạy qua Hệ thống Input mới để đảm bảo luôn nhận nút
        if (UnityEngine.InputSystem.Keyboard.current == null) return;

        // Nhấn F8 để dịch chuyển đến Boss
        if (UnityEngine.InputSystem.Keyboard.current.f8Key.wasPressedThisFrame)
        {
            TeleportToBoss();
        }

        // Nhấn F9 để bật/tắt God Mode
        if (UnityEngine.InputSystem.Keyboard.current.f9Key.wasPressedThisFrame)
        {
            isGodMode = !isGodMode;
            Debug.Log($"<color=magenta>🚀 CHEAT: God Mode {(isGodMode ? "BẬT" : "TẮT")}</color>");
        }

        // Nhấn F10 để bật/tắt +100 Damage
        if (UnityEngine.InputSystem.Keyboard.current.f10Key.wasPressedThisFrame)
        {
            PlayerHealth.GlobalCheatDamageBonus = (PlayerHealth.GlobalCheatDamageBonus == 0f) ? 100f : 0f;
            Debug.Log($"<color=magenta>🚀 CHEAT: +100 Damage {(PlayerHealth.GlobalCheatDamageBonus > 0 ? "BẬT" : "TẮT")}</color>");
        }

        // Giữ trạng thái bất tử và full năng lượng liên tục
        if (isGodMode)
        {
            ApplyGodMode();
        }
    }

    private void ApplyGodMode()
    {
        // 1. Full Máu
        if (playerHealth != null)
        {
            playerHealth.FullHeal();
        }

        // 2. Full Thể Lực và Nguyên Tố
        if (stateMachine != null)
        {
            if (stateMachine.Stamina != null)
            {
                stateMachine.Stamina.HealStamina(stateMachine.Stamina.MaxStamina);
            }
            
            // Hồi đầy tất cả các hệ nguyên tố
            foreach (SkillElement element in System.Enum.GetValues(typeof(SkillElement)))
            {
                if (element == SkillElement.KhongHe) continue;
                stateMachine.ElementalEnergies[element] = stateMachine.MaxElementalEnergy;
                // Cập nhật lên UI (Vì OnElementAbsorbed là static event nên phải gọi qua tên class)
                PlayerStateMachine.OnElementAbsorbed?.Invoke(element, 1f); 
            }
        }
    }

    private void TeleportToBoss()
    {
        // Tìm con Boss thông qua script BossDeathTrigger
        BossDeathTrigger boss = FindObjectOfType<BossDeathTrigger>();
        
        if (boss != null)
        {
            Vector3 direction = (transform.position - boss.transform.position).normalized;
            if (direction == Vector3.zero) direction = Vector3.forward;
            
            Vector3 targetPos = boss.transform.position + direction * 5f;
            targetPos.y = boss.transform.position.y + 2f; 
            
            // 🟢 TẮT CharacterController trước khi dịch chuyển để không bị lỗi vật lý
            if (cc != null) cc.enabled = false;
            
            transform.position = targetPos;
            transform.LookAt(boss.transform.position);

            // BẬT lại CharacterController
            if (cc != null) cc.enabled = true;
            
            Debug.Log("<color=magenta>🚀 CHEAT: Đã dịch chuyển đến Boss!</color>");
        }
        else
        {
            Debug.LogWarning("❌ KHÔNG TÌM THẤY BOSS (Không có script BossDeathTrigger trong Scene)!");
        }
    }
}
