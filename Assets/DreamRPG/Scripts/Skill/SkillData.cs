using UnityEngine;

public enum SkillElement { KhongHe, Lua, Loi, Gio, Bang }

[CreateAssetMenu(fileName = "New Skill", menuName = "DreamRPG/Skill Data")]
public class SkillData : ScriptableObject
{
    public string skillName;
    public SkillElement element; 
    public Sprite icon;
    
    public float cooldownTime = 5f;
    public float staminaCost = 20f;
    public string animationName; // Hoạt ảnh lúc VUNG TAY PHÓNG KIẾM
    
    [Header("Blink Settings (Cho kỹ năng phóng kiếm)")]
    public bool isWarpSkill; 
    public GameObject swordProjectilePrefab;  // Viên đạn (Mũi kiếm bay)
    public float projectileSpeed = 40f;       // Tốc độ bay của kiếm
    public float maxProjectileDistance = 20f; // Tầm bay tối đa (nếu trượt)
    
    [Header("Effects & Damage")]
    public GameObject chargeVfxPrefab;
    public GameObject vfxPrefab; 
    public float damageMultiplier = 3f;
    public float freezeDuration = 5f; // ❄️ THỜI GIAN ĐÓNG BĂNG (giây)
    
    [Header("Ghost Effect Settings")]
    public GameObject ghostPrefab;
    public Material ghostMaterial;
    public Material[] iceFormMaterials;
}