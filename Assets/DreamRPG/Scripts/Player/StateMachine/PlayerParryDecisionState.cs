using UnityEngine;

/// <summary>
/// PlayerParryDecisionState — Trạm Trung Chuyển sau khi Parry thành công.
///
/// Cơ chế 3 giai đoạn:
///   Phase 1: PARRY     — Animation parry chạy TỐC ĐỘ BÌNH THƯỜNG (không slow-mo)
///                         Khi animation đạt 70% → chuyển Phase 2
///   Phase 2: DECISION  — ĐÓNG BĂNG thời gian (slow-mo), chờ người chơi quyết định
///                         Chuột trái = Chém trả thù | Chuột phải = Hút nguyên tố
///   Phase 3: RAMP UP   — Nếu không chọn, thời gian mượt mà tăng về 1.0x rồi thoát
/// </summary>
public class PlayerParryDecisionState : PlayerBaseState
{
    private string parryAnimName;
    private float timer = 0f;

    // ===================== TUNING =====================
    // Phase 1: PARRY — Chạy animation bình thường
    private const float PARRY_TRIGGER_POINT = 0.7f;   // 70% animation → kích hoạt slow-mo

    // Phase 2: DECISION — Slow motion chờ quyết định
    private const float DECISION_DURATION = 1.5f;     // 1.5 giây thực để người chơi chọn
    private const float DECISION_TIME_SCALE = 0.05f;  // 5% tốc độ (cực chậm)

    // Phase 3: RAMP UP — Từ từ tăng tốc về bình thường
    private const float RAMP_DURATION = 0.4f;         // 0.4 giây thực để ramp
    // ==================================================

    private enum Phase { Parry, Decision, RampUp }
    private Phase currentPhase = Phase.Parry;
    private float decisionTimer = 0f;  // Timer riêng cho Phase 2
    private float rampTimer = 0f;      // Timer riêng cho Phase 3
    private int parryLayerIndex = 0;   // Layer đang phát animation
    private Transform parriedEnemy;    // 🟢 Lưu lại con quái vừa đánh mình
    public Transform ParriedEnemy => parriedEnemy;

    public PlayerParryDecisionState(PlayerStateMachine stateMachine, Transform parriedEnemy = null) : base(stateMachine) 
    {
        this.parriedEnemy = parriedEnemy;
    }

    public override void Enter()
    {
        stateMachine.Animator.applyRootMotion = true;
        stateMachine.Animator.SetBool("isPerformingAction", true);
        
        stateMachine.EnableInvincibility(); 

        // 🟢 Phase 1: KHÔNG slow-mo, chạy animation tốc độ bình thường
        Time.timeScale = 1f;
        stateMachine.Animator.updateMode = AnimatorUpdateMode.Normal;

        WeaponData weapon = stateMachine.CurrentWeapon;
        parryAnimName = (weapon != null && !string.IsNullOrEmpty(weapon.ParryAnimName)) ? weapon.ParryAnimName : "Parry";
        
        // Phát animation Parry trên Layer 1 (Upper Body)
        if (stateMachine.Animator.layerCount > 1) 
        {
            parryLayerIndex = 1;
            stateMachine.Animator.SetLayerWeight(1, 1f);
            stateMachine.Animator.CrossFadeInFixedTime(parryAnimName, 0.05f, 1);
        }
        else 
        {
            parryLayerIndex = 0;
            stateMachine.Animator.CrossFadeInFixedTime(parryAnimName, 0.05f, 0);
        }

        stateMachine.InputReader.ConsumeBuffer();

        // Giữ khiên hiển thị
        if (stateMachine.ShieldVFX != null)
        {
            stateMachine.ShieldVFX.SetActive(true);
        }

        // 🟢 Xoay người thẳng về phía đối thủ đã parry (hoặc target hiện tại) để tránh bị nhìn lệch hướng khi parry
        Transform parryTarget = parriedEnemy != null ? parriedEnemy : 
                              (stateMachine.TargetSys != null ? stateMachine.TargetSys.GetCurrentTarget() : null);
        if (parryTarget != null && stateMachine.TargetSys != null)
        {
            stateMachine.TargetSys.FaceTarget(parryTarget.position, 0f, true);
        }

        timer = 0f;
        decisionTimer = 0f;
        rampTimer = 0f;
        currentPhase = Phase.Parry;
    }

    public override void Tick(float deltaTime)
    {
        timer += Time.unscaledDeltaTime;

        // 🟢 Xoay người mượt mà hướng về đối thủ trong suốt thời gian parry (dùng unscaledDeltaTime vì slow-mo)
        if (stateMachine.TargetSys != null)
        {
            Transform parryTarget = parriedEnemy != null ? parriedEnemy : stateMachine.TargetSys.GetCurrentTarget();
            if (parryTarget != null)
            {
                stateMachine.TargetSys.FaceTarget(parryTarget.position, Time.unscaledDeltaTime, false);
            }
        }

        // ===================== KIỂM TRA INPUT MỌI LÚC =====================
        // Người chơi có thể chọn bất cứ lúc nào (kể cả Phase 1)

        if (stateMachine.InputReader.BufferedInput == BufferedCommand.Attack)
        {
            stateMachine.InputReader.ConsumeBuffer();
            stateMachine.SwitchState(new PlayerParryAttackState(stateMachine, parriedEnemy)); 
            Debug.Log("🔥 ĐÃ CHỌN: CHUỘT TRÁI - CHÉM TRẢ THÙ!");
            return;
        }

        if (stateMachine.InputReader.BufferedInput == BufferedCommand.Block)
        {
            stateMachine.InputReader.ConsumeBuffer();
            stateMachine.SwitchState(new PlayerParryAbsorbState(stateMachine, parriedEnemy));
            Debug.Log("🌀 ĐÃ CHỌN: CHUỘT PHẢI - HÚT NGUYÊN TỐ!");
            return;
        }

        // ===================== XỬ LÝ 3 GIAI ĐOẠN =====================

        if (currentPhase == Phase.Parry)
        {
            // Phase 1: Animation parry chạy TỐC ĐỘ BÌNH THƯỜNG
            // Khi animation đạt 70% → kích hoạt slow motion
            AnimatorStateInfo stateInfo = stateMachine.Animator.GetCurrentAnimatorStateInfo(parryLayerIndex);
            
            if (stateInfo.IsName(parryAnimName) && stateInfo.normalizedTime >= PARRY_TRIGGER_POINT)
            {
                // 🟢 CHUYỂN SANG PHASE 2: ĐÓNG BĂNG THỜI GIAN
                currentPhase = Phase.Decision;
                decisionTimer = 0f;
                
                Time.timeScale = DECISION_TIME_SCALE;
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                stateMachine.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                
                Debug.Log("<color=yellow>⏳ SLOW MOTION! Chờ quyết định: Chuột trái (Chém) / Chuột phải (Hút)</color>");
            }
        }
        else if (currentPhase == Phase.Decision)
        {
            // Phase 2: SLOW MOTION — Chờ người chơi quyết định
            decisionTimer += Time.unscaledDeltaTime;

            if (decisionTimer >= DECISION_DURATION)
            {
                // Hết thời gian chờ → bắt đầu ramp về tốc độ bình thường
                currentPhase = Phase.RampUp;
                rampTimer = 0f;
            }
        }
        else if (currentPhase == Phase.RampUp)
        {
            // Phase 3: RAMP UP — Từ từ tăng timeScale về 1.0
            rampTimer += Time.unscaledDeltaTime;
            float rampProgress = Mathf.Clamp01(rampTimer / RAMP_DURATION);

            // SmoothStep tạo cảm giác tăng tốc tự nhiên (không tuyến tính)
            float smoothProgress = Mathf.SmoothStep(0f, 1f, rampProgress);
            Time.timeScale = Mathf.Lerp(DECISION_TIME_SCALE, 1f, smoothProgress);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            // Hết ramp → quay về combat bình thường
            if (rampProgress >= 1f)
            {
                stateMachine.SwitchState(new PlayerMovementState(stateMachine)); 
            }
        }
    }

    public override void Exit()
    {
        // Đảm bảo trả về tốc độ bình thường
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        
        stateMachine.Animator.updateMode = AnimatorUpdateMode.Normal;
        stateMachine.Animator.applyRootMotion = false;
        stateMachine.Animator.SetBool("isPerformingAction", false);
        stateMachine.DisableInvincibility();

        // Tắt khiên khi thoát Parry
        if (stateMachine.ShieldVFX != null)
        {
            stateMachine.ShieldVFX.SetActive(false);
        }

        // 🟢 Tắt Layer 1 khi thoát vì các state tấn công/hút năng lượng tiếp theo (Layer 0) sẽ chạy toàn thân
        if (stateMachine.Animator.layerCount > 1)
        {
            stateMachine.Animator.SetLayerWeight(1, 0f);
            stateMachine.Animator.CrossFadeInFixedTime("Empty", 0.1f, 1);
        }
    }
}