using UnityEngine;
using UnityEngine.UI;

public class ActionHUD : MonoBehaviour
{
    [Header("Weapon Elements")]
    public Image weaponIcon;

    [Header("Skill Elements (0 = Skill 1, 1 = Skill 2, 2 = Skill 3)")]
    // Dùng Mảng (Array) để chứa nhiều icon cùng lúc
    public Image[] skillIcons;
    public Image[] skillCooldownOverlays; 
    
    /// <summary>
    /// Gọi hàm này khi Kratos đổi vũ khí.
    /// Nó sẽ thay đổi ảnh Vũ khí và thay luôn 3 ảnh Kỹ năng đi kèm.
    /// </summary>
    public void SwitchWeaponProfile(Sprite newWeapon, Sprite skill1, Sprite skill2, Sprite skill3)
    {
        if (weaponIcon != null)
        {
            weaponIcon.sprite = newWeapon;
            weaponIcon.color = newWeapon != null ? Color.white : new Color(1f, 1f, 1f, 0.1f);
        }

        // Cập nhật 3 icon skill (Kiểm tra mảng có đủ 3 ô không để tránh lỗi)
        if (skillIcons != null && skillIcons.Length >= 3)
        {
            skillIcons[0].sprite = skill1;
            skillIcons[0].color = skill1 != null ? Color.white : new Color(1f, 1f, 1f, 0.1f);
            
            skillIcons[1].sprite = skill2;
            skillIcons[1].color = skill2 != null ? Color.white : new Color(1f, 1f, 1f, 0.1f);
            
            skillIcons[2].sprite = skill3;
            skillIcons[2].color = skill3 != null ? Color.white : new Color(1f, 1f, 1f, 0.1f);
        }
    }

    /// <summary>
    /// Gọi hàm này liên tục trong Update khi có một kỹ năng đang hồi chiêu
    /// </summary>
    /// <param name="skillIndex">Vị trí của skill (0, 1, hoặc 2)</param>
    /// <param name="currentCooldown">Thời gian hồi còn lại</param>
    /// <param name="maxCooldown">Thời gian hồi tối đa</param>
    public void UpdateSkillCooldown(int skillIndex, float currentCooldown, float maxCooldown)
    {
        // Tránh lỗi nếu truyền sai index
        if (skillCooldownOverlays == null || skillIndex < 0 || skillIndex >= skillCooldownOverlays.Length) return;

        Image overlay = skillCooldownOverlays[skillIndex];
        if (overlay == null) return;

        if (currentCooldown > 0f)
        {
            overlay.gameObject.SetActive(true);
            overlay.fillAmount = currentCooldown / maxCooldown;
        }
        else
        {
            overlay.fillAmount = 0f;
            overlay.gameObject.SetActive(false);
        }
    }
    /// <summary>
    /// Hàm này làm tối Icon kỹ năng nếu không đủ Thể lực / Năng lượng nguyên tố
    /// </summary>
    public void UpdateSkillUsability(int skillIndex, bool canAfford)
    {
        // Tránh lỗi nếu truyền sai index
        if (skillIcons == null || skillIndex < 0 || skillIndex >= skillIcons.Length) return;

        Image icon = skillIcons[skillIndex];
        if (icon == null) return;

        // Nếu đủ tài nguyên: Trả về màu Trắng (sáng bình thường)
        // Nếu thiếu tài nguyên: Phủ màu Xám tối (như Liên Minh Huyền Thoại)
        icon.color = canAfford ? Color.white : new Color(0.25f, 0.25f, 0.25f, 1f); 
    }
}