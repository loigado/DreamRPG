using UnityEngine;

/// <summary>
/// EnemyDeathState — Quái chết: dừng mọi thứ, giải phóng tài nguyên, fade out.
///
/// Logic:
///   • Tắt Agent/Controller
///   • Giải phóng Token + Slot
///   • Hủy đăng ký khỏi AIDirector
///   • Chờ animation chết xong → Destroy (hoặc pool)
/// </summary>
public class EnemyDeathState : EnemyState
{
    private float destroyTimer = 3f;

    public EnemyDeathState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        // === 1. DỪNG MỌI DI CHUYỂN ===
        stateMachine.ManualVelocity = Vector3.zero;

        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
        {
            stateMachine.Agent.ResetPath();
            stateMachine.Agent.isStopped = true;
        }
        stateMachine.Agent.enabled     = false;
        stateMachine.Controller.enabled = false;

        // === 2. GIẢI PHÓNG TÀI NGUYÊN ===
        // Release Token (chỉ release nếu đang thật sự giữ — AIDirector kiểm tra)
        if (AIDirector.Instance != null)
            AIDirector.Instance.ReleaseToken(stateMachine);

        // Release Slot
        if (EnemySlotManager.Instance != null && stateMachine.ReservedSlotIndex >= 0)
        {
            EnemySlotManager.Instance.ReleaseSlot(stateMachine.ReservedSlotIndex);
            stateMachine.ReservedSlotIndex = -1;
        }

        // Hủy đăng ký khỏi AIDirector
        if (AIDirector.Instance != null)
            AIDirector.Instance.UnregisterEnemy(stateMachine);

        // === 3. ANIMATION CHẾT ===
        stateMachine.Anim.SetTrigger("Die");

        // Tắt Collider để Player không bị chặn bởi xác chết
        Collider col = stateMachine.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Disable tất cả collider con (nếu có)
        foreach (var c in stateMachine.GetComponentsInChildren<Collider>())
            c.enabled = false;
    }

    public override void Tick(float deltaTime)
    {
        // Chờ animation chết xong rồi tự hủy
        destroyTimer -= deltaTime;
        if (destroyTimer <= 0f)
        {
            // Có thể thay bằng Object Pooling: gameObject.SetActive(false)
            Object.Destroy(stateMachine.gameObject);
        }
    }

    public override void Exit()
    {
        // Không bao giờ exit bình thường — Destroy sẽ xóa trước
    }
}
