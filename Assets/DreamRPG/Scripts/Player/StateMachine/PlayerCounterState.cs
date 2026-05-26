using UnityEngine;

public class PlayerCounterState : PlayerBaseState
{
    private string counterAnimName; 
    private float magneticSpeed = 6f; 
    private float safeDistance = 2f; 

    public PlayerCounterState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        stateMachine.Animator.applyRootMotion = true;
        stateMachine.Animator.SetBool("isPerformingAction", true);

        stateMachine.CancelSlowMotion();

        Time.timeScale = 0.1f; 
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        stateMachine.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        // 🟢 ĐỌC ĐÒN PHẢN CÔNG (COUNTER) TỪ SO VŨ KHÍ
        WeaponData weapon = stateMachine.CurrentWeapon;
        counterAnimName = (weapon != null && !string.IsNullOrEmpty(weapon.CounterAnimName)) ? weapon.CounterAnimName : "Unarmed_Counter";
        
        stateMachine.Animator.CrossFadeInFixedTime(counterAnimName, 0.1f);

        Vector3 startPos = stateMachine.transform.position; 

        if (stateMachine.LastDodgedEnemy != null)
        {
            Transform enemy = stateMachine.LastDodgedEnemy;
            
            Vector3 targetPos = enemy.position - enemy.forward * safeDistance;

            Vector3 rayOrigin = enemy.position + Vector3.up * 1f; 
            Vector3 rayDirection = -enemy.forward;

            // Kiểm tra vật cản phía sau (tường)
            if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, safeDistance + 0.5f))
            {
                targetPos = hit.point + enemy.forward * 0.5f;
            }

            // 🟢 FIX ĐỊA HÌNH DỐC: Bắn tia từ trên xuống để tìm mặt đất chuẩn
            Vector3 groundCheckOrigin = new Vector3(targetPos.x, enemy.position.y + 2f, targetPos.z);
            if (Physics.Raycast(groundCheckOrigin, Vector3.down, out RaycastHit groundHit, 5f))
            {
                // Dùng vị trí mặt đất tìm được
                targetPos.y = groundHit.point.y;
            }
            else
            {
                // Fallback nếu không tìm thấy (gần như không xảy ra)
                targetPos.y = enemy.position.y;
            }

            stateMachine.Controller.enabled = false;
            stateMachine.transform.position = targetPos;
            stateMachine.Controller.enabled = true;

            Vector3 faceDir = (enemy.position - stateMachine.transform.position).normalized;
            faceDir.y = 0;
            if (faceDir.sqrMagnitude > 0)
            {
                stateMachine.transform.rotation = Quaternion.LookRotation(faceDir);
            }

            CreateBlinkFX(startPos, targetPos);
            AfterimageController afterimage = stateMachine.GetComponent<AfterimageController>();
        if (afterimage != null) 
        {
            // Bạn có thể đổi SkillElement.Loi thành hệ nguyên tố vũ khí của Kratos 
            // hoặc hệ của con quái vừa bị Parry nhé.
            afterimage.StartTrail(SkillElement.Loi); 
        }
        }
    }

    public override void Tick(float deltaTime)
    {
        ApplyGravity(Time.unscaledDeltaTime);

        if (stateMachine.LastDodgedEnemy != null)
        {
            Transform enemy = stateMachine.LastDodgedEnemy;
            
            Vector3 faceDir = (enemy.position - stateMachine.transform.position).normalized;
            faceDir.y = 0;
            if (faceDir.sqrMagnitude > 0.01f)
            {
                stateMachine.transform.rotation = Quaternion.Slerp(stateMachine.transform.rotation, Quaternion.LookRotation(faceDir), 20f * Time.unscaledDeltaTime);
            }

            float currentDist = Vector3.Distance(stateMachine.transform.position, enemy.position);
            if (currentDist > safeDistance + 0.2f)
            {
                Vector3 slideDir = (enemy.position - stateMachine.transform.position).normalized;
                slideDir.y = 0;
                stateMachine.Controller.Move(slideDir * magneticSpeed * Time.unscaledDeltaTime); 
            }
        }

        AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.IsName(counterAnimName) && stateInfo.normalizedTime >= 0.9f)
        {
            stateMachine.SwitchState(new PlayerMovementState(stateMachine));
        }
    }

    public override void Exit()
    {
        Time.timeScale = 1f; 
        Time.fixedDeltaTime = 0.02f;
        stateMachine.Animator.updateMode = AnimatorUpdateMode.Normal;

        AfterimageController afterimage = stateMachine.GetComponent<AfterimageController>();
        if (afterimage != null) afterimage.StopTrail(); 

        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.SetBool("isPerformingAction", false);
    }

    private void CreateBlinkFX(Vector3 startPoint, Vector3 endPoint)
    {
        WeaponData weapon = stateMachine.CurrentWeapon;
        if (weapon == null) return;

        Vector3 offset = Vector3.up * 1.2f; 

        if (weapon.BlinkDisappearFX != null)
        {
            ObjectPoolManager.Instance.SpawnFromPool(weapon.BlinkDisappearFX, startPoint + offset, Quaternion.identity);
        }

        if (weapon.BlinkAppearFX != null)
        {
            Vector3 direction = (endPoint - startPoint).normalized;
            direction.y = 0;
            Quaternion rotation = direction.sqrMagnitude > 0 ? Quaternion.LookRotation(direction) : Quaternion.identity;
            
            ObjectPoolManager.Instance.SpawnFromPool(weapon.BlinkAppearFX, endPoint + offset, rotation);
        }
    }
}