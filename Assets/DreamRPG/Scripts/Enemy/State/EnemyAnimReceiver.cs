using UnityEngine;

/// <summary>
/// Cầu nối (Bridge) để nhận Animation Event từ Animator (nằm ở Object con)
/// và truyền ngược lên cho EnemyStateMachine (nằm ở Object cha).
/// </summary>
public class EnemyAnimReceiver : MonoBehaviour
{
    private EnemyStateMachine stateMachine;

    private void Awake()
    {
        // Khi game bắt đầu, tự động dò tìm bộ não ở Object Cha
        stateMachine = GetComponentInParent<EnemyStateMachine>();
        
        if (stateMachine == null)
        {
            Debug.LogError($"[AnimReceiver] Không tìm thấy EnemyStateMachine ở Object cha của {gameObject.name}!");
        }
    }

    // ==========================================
    // NHẬN EVENT TỪ ANIMATOR VÀ CHUYỀN LÊN TRÊN
    // ==========================================

    // 1. Dùng cho đòn chém thường và dậm đất
    public void AnimationEvent_TriggerHitbox()
    {
        if (stateMachine != null) 
            stateMachine.AnimationEvent_TriggerHitbox();
    }

    // 2. 🟢 MỚI: Dùng cho vụ nổ chuyển Phase 2 của Boss
    public void AnimationEvent_TriggerPhase2Explosion()
    {
        if (stateMachine != null)
            stateMachine.AnimationEvent_TriggerPhase2Explosion();
    }
}