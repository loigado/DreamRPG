using UnityEngine;
using System.Collections.Generic;

// Định nghĩa các loại vũ khí để State Machine biết đường xử lý
public enum WeaponType
{
    Melee,  // Cận chiến (Kiếm, Rìu...)
    Ranged, // Đánh xa (Cung, Nỏ, Súng...)
    Unarmed // Tay không
}

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Combat/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("--- Thông tin Cơ bản ---")]
    public string WeaponName;
    [Tooltip("ID của vũ khí để đồng bộ với Animator và WeaponHolder")]
    public int WeaponAnimID;
    public WeaponType Type;

    [Header("--- Chỉ số Chiến đấu ---")]
    public float Damage = 20f;
    [Tooltip("Lượng thể lực tiêu hao mỗi lần ra đòn")]
    public float StaminaCost = 15f;

    [Header("--- Cận Chiến (Melee Settings) ---")]
    [Tooltip("Danh sách tên chính xác của các cục Animation chém (Combo) trong Animator")]
    public List<string> ComboAnimationNames;
    public string DashAttackAnimName;

    [Header("--- Đánh Xa (Ranged Settings) ---")]
    [Tooltip("Kéo Prefab Mũi Tên/Đạn vào đây (Chỉ dành cho Cung/Súng)")]
    public GameObject ArrowPrefab;
    [Tooltip("Lực đẩy mũi tên bay về phía trước")]
    public float ShootForce = 25f;

    [Header("--- Tên State Animation (Phẳng) ---")]
    [Tooltip("Tên Blend Tree hoặc Animation đi bộ/chạy của vũ khí này")]
    public string EquipAnimName;
    public string UnequipAnimName;
    public string LocomotionStateName = "Unarmed_Locomotion";
    public string JumpAnimName = "Unarmed_Jump";
    [Tooltip("Tên Animation lộn nhào")]
    public string RollAnimName = "Unarmed_Roll";
    
    [Header("--- Đỡ Đòn (Block Settings) ---")]
    [Tooltip("Tên Animation giơ vũ khí đỡ đòn (Idle)")]
    public string BlockAnimName = "Block_Idle";
    [Tooltip("Tên Animation gạt vũ khí (Perfect Parry)")]
    public string ParryAnimName = "Parry";
    [Tooltip("Tên Animation bị phá thủng lớp phòng ngự (Choáng)")]
    public string GuardBreakAnimName = "GuardBreak";
    [Tooltip("Tên Animation bị giật nhẹ khi đỡ đòn thành công")]
    public string BlockHitAnimName = "Block_Hit";
    [Tooltip("Vật liệu (Màu/Nguyên tố) của Khiên VFX khi dùng vũ khí này")]
    public Material ShieldMaterial;

    [Header("Kỹ năng của Vũ khí")]
    public SkillData skill1; // Phím F
    public SkillData skill2; // Phím V
    public SkillData skill3; // Phím G
}