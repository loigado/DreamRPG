using UnityEngine;

/// <summary>
/// Lưu trữ toàn bộ Hash của Animator và LayerMask để tái sử dụng.
/// Tiết kiệm chi phí tính toán ngầm của Unity mỗi frame.
/// </summary>
public static class EnemyConstants
{
    // === ANIMATOR HASHES ===
    public static readonly int HashVelocityX   = Animator.StringToHash("VelocityX");
    public static readonly int HashVelocityZ   = Animator.StringToHash("VelocityZ");
    public static readonly int HashAttack      = Animator.StringToHash("Attack");
    public static readonly int HashAttackVariant = Animator.StringToHash("AttackVariant");
    public static readonly int HashBlockStart  = Animator.StringToHash("BlockStart");
    public static readonly int HashIsBlocking  = Animator.StringToHash("IsBlocking");
    public static readonly int HashBlockImpact = Animator.StringToHash("BlockImpact");
    public static readonly int HashDie         = Animator.StringToHash("Die");
    public static readonly int HashDodge       = Animator.StringToHash("Dodge");
    public static readonly int HashDodgeX      = Animator.StringToHash("DodgeX");
    public static readonly int HashDodgeZ      = Animator.StringToHash("DodgeZ");
    public static readonly int HashGapCloser   = Animator.StringToHash("GapCloser");
    public static readonly int HashIsGapClosing = Animator.StringToHash("IsGapClosing");
    public static readonly int HashSkill       = Animator.StringToHash("Skill");
    public static readonly int HashBossRoar    = Animator.StringToHash("BossRoar");

    // Lệnh reset triggers
    public static readonly int HashLightHit    = Animator.StringToHash("LightHit");
    public static readonly int HashHeavyHit    = Animator.StringToHash("HeavyHit");

    // === LAYER MASKS ===
    public static readonly int PlayerLayerMask = LayerMask.GetMask("Player");
    public static readonly int EnvLayerMask    = LayerMask.GetMask("Environment");
}