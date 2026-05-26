using UnityEngine;
using System.Collections.Generic;

public enum WeaponType
{
    Melee,  
    Ranged, 
    Unarmed 
}

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Combat/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("--- Thông tin Cơ bản ---")]
    public string WeaponName;
    public int WeaponAnimID;
    public WeaponType Type;

    [Header("--- UI Icons ---")]
    public Sprite WeaponIcon;
    public Sprite Skill1Icon;
    public Sprite Skill2Icon;
    public Sprite Skill3Icon;

    [Header("--- Chỉ số Chiến đấu ---")]
    public float Damage = 20f;
    public float StaminaCost = 15f;

    [Header("--- Cận Chiến (Melee Settings) ---")]
    public List<string> ComboAnimationNames;
    public string DashAttackAnimName;
    public string RollingAttackAnimName;

    // 🟢 ĐÃ KHÔI PHỤC: Hệ thống Kỹ năng (Skills)
    [Header("--- Kỹ Năng (Skills) ---")]
    public SkillData skill1;
    public SkillData skill2;
    public SkillData skill3;

    [Header("--- Đỡ Đòn (Block & Parry Settings) ---")]
    [Tooltip("Tên Animation Parry (Phát khi Gạt đòn thành công trong 0.2s)")]
    public string ParryAnimName = "Parry"; 

    [Tooltip("Tên Animation giơ vũ khí đỡ đòn chủ động (Loop)")]
    public string BlockAnimName = "Block_Idle";
    
    [Tooltip("Tên Animation bị phá thủng lớp phòng ngự khi hết Stamina")]
    public string GuardBreakAnimName = "GuardBreak";
    
    [Tooltip("Tên Animation bị giật nhẹ khi đỡ đòn thành công")]
    public string BlockHitAnimName = "Block_Hit";

    [Tooltip("Tên Animation đòn chém phản công sau khi Parry thành công")]
    public string ParryAttackAnimName = "Parry_Attack";

    [Tooltip("Tên Animation hút năng lượng nguyên tố sau khi Parry thành công")]
    public string ParryAbsorbAnimName = "Parry_Absorb";
    
    [Tooltip("Vật liệu (Màu/Nguyên tố) của Khiên VFX khi dùng vũ khí này")]
    public Material ShieldMaterial;

    [Header("--- Phản Công (Perfect Dodge Counter) ---")]
    [Tooltip("Tên Animation tung đòn chém trả thủ sau khi né hoàn hảo")]
    public string CounterAnimName = "Unarmed_Counter";

    [Header("--- Hiệu ứng Biến Ảo (Blink Counter VFX) ---")]
    [Tooltip("Hiệu ứng bung ra tại chỗ đứng lúc biến mất")]
    public GameObject BlinkDisappearFX;
    [Tooltip("Hiệu ứng tụ lại tại vị trí mới xuất hiện sau lưng quái")]
    public GameObject BlinkAppearFX;

    [Header("--- Phản Ứng (Hit & Death) ---")]
    [Tooltip("Tên Animation bị đánh trúng (nhẹ)")]
    public string HitAnimName = "Impact";
    [Tooltip("Tên Animation bị hất văng (mạnh)")]
    public string HeavyHitAnimName = "KnockbackFar";
    [Tooltip("Tên Animation gục ngã (chết)")]
    public string DeathAnimName = "Death";

    [Header("--- Cấu Hình Di Chuyển ---")]
    public string EquipAnimName;
    public string UnequipAnimName;
    public string LocomotionStateName = "Unarmed_Locomotion";
    public string JumpAnimName = "Unarmed_Jump";
    public string RollAnimName = "Unarmed_Roll";
}