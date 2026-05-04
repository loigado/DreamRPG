using UnityEngine;

public class PlayerAttackState : PlayerBaseState
{
    private int comboIndex; 
    private string currentAnimName;
    private bool canCombo = false; 
    private float timePassed = 0f;
    private bool isDashAttack; 

    public PlayerAttackState(PlayerStateMachine stateMachine, int comboIndex, bool isDashAttack = false) : base(stateMachine)
    {
        this.comboIndex = comboIndex;
        this.isDashAttack = isDashAttack;
    }

    public override void Enter()
    {
        WeaponData weapon = stateMachine.CurrentWeapon;
        if (weapon == null) { stateMachine.SwitchState(new PlayerMoveState(stateMachine)); return; }

        // Phát tín hiệu + truyền position để quái biết hướng
        PlayerStateMachine.OnPlayerAttack?.Invoke(stateMachine.transform.position);
        
        // Thông báo trực tiếp cho quái gần để kích hoạt Dodge
        NotifyNearbyEnemies();
        
        if (stateMachine.TargetSys != null) 
        {
            stateMachine.TargetSys.FindSoftTarget();
            
            Transform target = stateMachine.TargetSys.GetCurrentTarget();
            if (target != null)
            {
                stateMachine.TargetSys.FaceTarget(target.position, 0, true);
            }
        }

        stateMachine.Stamina.UseStamina(weapon.StaminaCost);
        stateMachine.Animator.SetBool("isPerformingAction", true);

        if (isDashAttack) currentAnimName = weapon.DashAttackAnimName;
        else currentAnimName = weapon.ComboAnimationNames[comboIndex];

        stateMachine.Animator.CrossFadeInFixedTime(currentAnimName, 0.1f);
        stateMachine.Animator.applyRootMotion = true;
        stateMachine.InputReader.AttackEvent += TryCombo;
        timePassed = 0f;
    }

    public override void Tick(float deltaTime)
    {
        timePassed += deltaTime;
        ApplyGravity(deltaTime); 

        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(0);

        if (stateMachine.TargetSys != null)
        {
            Transform target = stateMachine.TargetSys.GetCurrentTarget();
            if (target != null && stateInfo.normalizedTime < 0.3f)
            {
                stateMachine.TargetSys.FaceTarget(target.position, deltaTime, false);
            }
        }

        if (stateInfo.IsName(currentAnimName))
        {
            if (canCombo && stateInfo.normalizedTime > 0.6f)
            {
                int nextIndex = isDashAttack ? 0 : comboIndex + 1;
                if (nextIndex < stateMachine.CurrentWeapon.ComboAnimationNames.Count)
                {
                    stateMachine.SwitchState(new PlayerAttackState(stateMachine, nextIndex, false));
                    return;
                }
            }
            if (stateInfo.normalizedTime >= 0.95f) stateMachine.SwitchState(new PlayerMoveState(stateMachine));
        }
    }

    public override void Exit()
    {
        stateMachine.InputReader.AttackEvent -= TryCombo;
        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.SetBool("isPerformingAction", false); 
    }

    private void TryCombo() { if (timePassed > 0.1f) canCombo = true; }

    private void NotifyNearbyEnemies()
    {
        float notifyRange = stateMachine.CurrentWeapon != null 
            ? stateMachine.CurrentWeapon.Damage * 0.1f + 3f  // Scale theo vũ khí
            : 3f;
        
        Collider[] nearbyEnemies = Physics.OverlapSphere(
            stateMachine.transform.position, notifyRange, LayerMask.GetMask("Enemy"));
        
        foreach (var col in nearbyEnemies)
        {
            var enemy = col.GetComponent<EnemyStateMachine>();
            if (enemy != null)
                enemy.NotifyPlayerAttackNearby(stateMachine.transform.position);
        }
    }
}