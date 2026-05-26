using UnityEngine;

public class PlayerAttackState : PlayerBaseState
{
    private int comboIndex; 
    private string currentAnimName;
    private bool isDashAttack; 
    private bool isRollingAttack; // 🟢 THÊM MỚI: Đánh dấu đây là đòn lộn chém

    // Cập nhật Constructor để nhận biết đòn Rolling Attack
    public PlayerAttackState(PlayerStateMachine stateMachine, int comboIndex, bool isDashAttack = false, bool isRollingAttack = false) : base(stateMachine)
    {
        this.comboIndex = comboIndex;
        this.isDashAttack = isDashAttack;
        this.isRollingAttack = isRollingAttack;
    }

    public override void Enter()
    {
        WeaponData weapon = stateMachine.CurrentWeapon;
        if (weapon == null) { stateMachine.SwitchState(new PlayerMovementState(stateMachine)); return; }

        PlayerStateMachine.OnPlayerAttack?.Invoke(stateMachine.transform.position);
        NotifyNearbyEnemies();
        
        if (stateMachine.TargetSys != null && !stateMachine.TargetSys.IsHardLocking) 
        {
            stateMachine.TargetSys.FindSoftTarget();
        }

        if (stateMachine.TargetSys != null)
        {
            Transform target = stateMachine.TargetSys.GetCurrentTarget();
            if (target != null)
            {
                // LUÔN LUÔN xoay mặt về phía mục tiêu khi bắt đầu chém nếu có mục tiêu
                stateMachine.TargetSys.FaceTarget(target.position, 0, true);
            }
            else
            {
                Vector3 movementInput = CalculateMovement();
                if (movementInput.sqrMagnitude > 0.01f)
                {
                    stateMachine.transform.rotation = Quaternion.LookRotation(movementInput);
                }
            }
        }

        stateMachine.Stamina.UseStamina(weapon.StaminaCost);
        stateMachine.Animator.SetBool("isPerformingAction", true);

        // 🟢 LỰA CHỌN ANIMATION CHUẨN TỪ SO
        if (isRollingAttack && !string.IsNullOrEmpty(weapon.RollingAttackAnimName)) 
            currentAnimName = weapon.RollingAttackAnimName;
        else if (isDashAttack && !string.IsNullOrEmpty(weapon.DashAttackAnimName)) 
            currentAnimName = weapon.DashAttackAnimName;
        else 
            currentAnimName = weapon.ComboAnimationNames[comboIndex];

        stateMachine.Animator.CrossFadeInFixedTime(currentAnimName, 0.1f);
        stateMachine.Animator.applyRootMotion = true;
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(deltaTime); 

        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(0);
        stateMachine.RootMotionMultiplier = 1f;

        if (stateInfo.normalizedTime < 0.3f)
        {
            Transform target = stateMachine.TargetSys != null ? stateMachine.TargetSys.GetCurrentTarget() : null;
            if (target != null)
            {
                // Ưu tiên bám dính mục tiêu (Tracking) khi đang chém
                stateMachine.TargetSys.FaceTarget(target.position, deltaTime, false);
            }
            else
            {
                Vector3 movementInput = CalculateMovement();
                if (movementInput.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(movementInput);
                    stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, targetRotation, deltaTime * 15f);
                }
            }
        }

        if (stateMachine.TargetSys != null)
        {
            Transform target = stateMachine.TargetSys.GetCurrentTarget();
            if (target != null)
            {
                EnemyStateMachine enemyAI = target.GetComponent<EnemyStateMachine>();
                
                if (enemyAI != null && !enemyAI.IsDodging)
                {
                    float enemyRadius = 0.5f; 
                    CapsuleCollider col = target.GetComponent<CapsuleCollider>();
                    if (col != null) enemyRadius = col.radius * target.lossyScale.x; 

                    float distToSkin = Vector3.Distance(stateMachine.transform.position, target.position) - enemyRadius;
                    
                    if (distToSkin > 1.0f && distToSkin < 3.5f)
                        stateMachine.RootMotionMultiplier = 1.5f; 
                    else if (distToSkin < 0.7f)
                        stateMachine.RootMotionMultiplier = 0f; 
                    else
                        stateMachine.RootMotionMultiplier = 1f;
                }
            }
        }

        if (stateInfo.IsName(currentAnimName))
        {
            if (stateInfo.normalizedTime > 0.6f)
            {
                if (stateMachine.InputReader.BufferedInput == BufferedCommand.Roll)
                {
                    if (stateMachine.Stamina.HasEnoughStamina(25f)) 
                    {
                        stateMachine.InputReader.ConsumeBuffer(); 
                        stateMachine.SwitchState(new PlayerRollState(stateMachine));
                        return;
                    }
                }

                if (stateMachine.InputReader.BufferedInput == BufferedCommand.Attack)
                {
                    if (stateMachine.Stamina.HasEnoughStamina(stateMachine.CurrentWeapon.StaminaCost))
                    {
                        stateMachine.InputReader.ConsumeBuffer(); 
                        
                        // Nếu đang đánh lướt hoặc đánh lộn nhào -> Nối về Combo 1 (Index 0)
                        int nextIndex = (isDashAttack || isRollingAttack) ? 0 : comboIndex + 1;
                        if (nextIndex < stateMachine.CurrentWeapon.ComboAnimationNames.Count)
                        {
                            stateMachine.SwitchState(new PlayerAttackState(stateMachine, nextIndex, false, false));
                            return;
                        }
                    }
                }
            }

            if (stateInfo.normalizedTime >= 0.95f) 
            {
                stateMachine.SwitchState(new PlayerMovementState(stateMachine));
            }
        }
    }

    public override void Exit()
    {
        stateMachine.RootMotionMultiplier = 1f; 
        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.SetBool("isPerformingAction", false); 
        
        // BẢO VỆ HITBOX: Tắt hitbox của vũ khí nếu thoát khỏi đòn đánh sớm (do spam chuột hoặc bị đánh)
        // Điều này đảm bảo đòn tiếp theo sẽ quét sát thương lại từ đầu, không bị "tịt ngòi".
        stateMachine.DisableHitbox();
    }

    private void NotifyNearbyEnemies()
    {
        float notifyRange = stateMachine.CurrentWeapon != null ? stateMachine.CurrentWeapon.Damage * 0.1f + 3f : 3f;
        int foundCount = 0;

        if (AIDirector.Instance != null)
        {
            foreach (var enemy in AIDirector.Instance.ActiveEnemies)
            {
                if (enemy == null || enemy.Health.IsDead) continue;
                float dist = Vector3.Distance(stateMachine.transform.position, enemy.transform.position);
                if (dist <= notifyRange)
                {
                    foundCount++;
                    enemy.NotifyPlayerAttackNearby(stateMachine.transform.position);
                }
            }
        }
    }
}