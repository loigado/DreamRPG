using UnityEngine;

/// <summary>
/// State xử lý khi người chơi chọn Hút Nguyên Tố (Chuột Phải) sau khi Parry.
/// Kratos sẽ quét các quái vật xung quanh và cướp năng lượng của chúng.
/// Animation chạy toàn thân (Layer 0).
/// </summary>
public class PlayerParryAbsorbState : PlayerBaseState
{
    private string absorbAnimName;
    private float timer = 0f;
    private const float ABSORB_DURATION = 1.2f; // Thời gian tối đa nếu animation không khớp tên
    private Transform parriedEnemy; // 🟢 Quái vừa bị parry
    public Transform ParriedEnemy => parriedEnemy;

    public PlayerParryAbsorbState(PlayerStateMachine stateMachine, Transform parriedEnemy = null) : base(stateMachine) 
    {
        this.parriedEnemy = parriedEnemy;
    }

    public override void Enter()
    {
        timer = 0f;
        stateMachine.Animator.applyRootMotion = true;
        stateMachine.Animator.SetBool("isPerformingAction", true);
        
        // 🟢 Bất tử trong lúc hút để không bị quái khác chém ngắt quãng
        stateMachine.EnableInvincibility();

        // Trả thời gian về bình thường
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        stateMachine.Animator.updateMode = AnimatorUpdateMode.Normal;

        // 🟢 Lấy animation hút từ WeaponData, fallback sang Parry nếu chưa gắn
        WeaponData weapon = stateMachine.CurrentWeapon;
        if (weapon != null && !string.IsNullOrEmpty(weapon.ParryAbsorbAnimName))
            absorbAnimName = weapon.ParryAbsorbAnimName;
        else if (weapon != null && !string.IsNullOrEmpty(weapon.ParryAnimName))
            absorbAnimName = weapon.ParryAnimName;
        else
            absorbAnimName = "Parry";

        // 🟢 Phát animation trên Layer 0 (toàn thân)
        stateMachine.Animator.CrossFadeInFixedTime(absorbAnimName, 0.1f, 0);

        // 🟢 Tắt Layer 1 (Upper Body) vì animation chạy full body
        if (stateMachine.Animator.layerCount > 1)
        {
            stateMachine.Animator.SetLayerWeight(1, 0f);
            stateMachine.Animator.CrossFadeInFixedTime("Empty", 0.05f, 1);
        }

        // 🟢 TẮT khiên khi hút năng lượng (tay cần rảnh để hút)
        if (stateMachine.ShieldVFX != null)
        {
            stateMachine.ShieldVFX.SetActive(false);
        }

        // ==========================================
        // 🟢 THỰC THI LỆNH HÚT NĂNG LƯỢNG (CHỈ TỪ KẺ ĐỊCH BỊ PARRY)
        // ==========================================
        bool hasAbsorbed = false;
        
        // Xác định chính xác con quái đang bị nhắm tới hoặc vừa bị parry
        Transform targetToAbsorb = parriedEnemy != null ? parriedEnemy : 
            (stateMachine.TargetSys != null ? stateMachine.TargetSys.GetCurrentTarget() : null);

        if (targetToAbsorb != null)
        {
            EnemyStateMachine enemy = targetToAbsorb.GetComponentInParent<EnemyStateMachine>();
            if (enemy != null && enemy.Stats != null && enemy.Stats.enemyElement != SkillElement.KhongHe)
            {
                // Hút 40 năng lượng từ con quái. Nếu năng lượng đầy, sẽ tự động chuyển hóa thành HP.
                stateMachine.AbsorbElement(enemy.Stats.enemyElement, 40f);
                hasAbsorbed = true;
            }
        }

        if (!hasAbsorbed)
        {
            Debug.Log("<color=grey>⚪ Quái vật không mang nguyên tố hoặc không tìm thấy mục tiêu, không thể hút!</color>");
        }
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime);
        timer += deltaTime;

        // Khóa mục tiêu: Ép nhân vật quay mặt về phía quái vật vừa bị parry lúc hút
        Transform target = parriedEnemy != null ? parriedEnemy : 
                           (stateMachine.TargetSys != null ? stateMachine.TargetSys.GetCurrentTarget() : null);

        if (target != null)
        {
            Vector3 targetLookDir = target.position - stateMachine.transform.position;
            targetLookDir.y = 0;
            if (targetLookDir.sqrMagnitude > 0.01f)
            {
                stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, Quaternion.LookRotation(targetLookDir), deltaTime * 10f);
            }
        }

        // 🟢 Hút xong: kiểm tra animation trên Layer 0 hoặc timer dự phòng
        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(0);
        bool isAnimDone = stateInfo.IsName(absorbAnimName) && stateInfo.normalizedTime >= 0.85f;
        bool isTimeExpired = timer >= ABSORB_DURATION;

        if (isAnimDone || isTimeExpired)
        {
            stateMachine.SwitchState(new PlayerMovementState(stateMachine));
        }
    }

    public override void Exit()
    {
        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.SetBool("isPerformingAction", false);
        stateMachine.DisableInvincibility();

        // 🟢 Tắt khiên
        if (stateMachine.ShieldVFX != null)
        {
            stateMachine.ShieldVFX.SetActive(false);
        }

    }
}