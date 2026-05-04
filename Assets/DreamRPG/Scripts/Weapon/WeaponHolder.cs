using UnityEngine;
using System;

[Serializable]
public class WeaponDictionary
{
    public string WeaponName; 
    public int WeaponAnimID;  
    public GameObject WeaponModel; 
    
    [Header("IK Settings")]
    public bool IsTwoHanded; 
    public Transform LeftHandIKTarget; 
}

public class WeaponHolder : MonoBehaviour
{
    public WeaponDictionary[] Weapons;

    public bool IsCurrentWeaponTwoHanded { get; private set; }
    public Transform CurrentLeftHandTarget { get; private set; }
    
    // 🟢 Lưu giữ Model Vũ khí đang được bật
    public GameObject CurrentWeaponModel { get; private set; } 
    
    private WeaponDamage currentHitbox; 

    public void ActivateWeapon(int weaponID)
    {
        // Tắt hết vũ khí cũ (Hàm này đã bao gồm việc Reset CurrentWeaponModel về null rồi)
        DeactivateAllWeapons();
        
        IsCurrentWeaponTwoHanded = false;
        CurrentLeftHandTarget = null;
        currentHitbox = null;

        foreach (var w in Weapons)
        {
            if (w.WeaponAnimID == weaponID && w.WeaponModel != null)
            {
                w.WeaponModel.SetActive(true);
                
                // 🟢 LƯU LẠI MODEL VŨ KHÍ ĐANG CẦM
                CurrentWeaponModel = w.WeaponModel; 
                
                // 🛡️ Dùng TryGetComponent thay vì GetComponent để an toàn hơn, lỡ vũ khí nào quên gắn Hitbox game cũng không báo lỗi đỏ
                if (w.WeaponModel.TryGetComponent<WeaponDamage>(out WeaponDamage damageComponent))
                {
                    currentHitbox = damageComponent;
                }
                
                if (w.IsTwoHanded && w.LeftHandIKTarget != null)
                {
                    IsCurrentWeaponTwoHanded = true;
                    CurrentLeftHandTarget = w.LeftHandIKTarget;
                }
                
                return; // Tìm thấy và bật xong rồi thì thoát vòng lặp luôn cho nhẹ máy
            }
        }
    }

    public void DeactivateAllWeapons()
    {
        foreach (var w in Weapons)
        {
            if (w.WeaponModel != null) w.WeaponModel.SetActive(false);
        }
        CurrentWeaponModel = null; // 🟢 Quét dọn sạch sẽ khi cất vũ khí
    }

    public void EnableWeaponHitbox(float damage)
    {
        if (currentHitbox != null) currentHitbox.OpenHitbox(damage);
    }

    public void DisableWeaponHitbox()
    {
        if (currentHitbox != null) currentHitbox.CloseHitbox();
    }
}