using UnityEngine;

/// <summary>
/// Cầu nối (Bridge) để truyền sự kiện từ Animator (thường ở con) lên StateMachine (thường ở cha).
/// Unity yêu cầu OnAnimatorMove phải nằm cùng GameObject với Animator.
/// </summary>
public class EnemyAnimationProxy : MonoBehaviour
{
    private EnemyStateMachine stateMachine;

    private void Awake()
    {
        // Tìm StateMachine ở cha
        stateMachine = GetComponentInParent<EnemyStateMachine>();
    }

    private void OnAnimatorMove()
    {
        if (stateMachine != null)
        {
            stateMachine.OnAnimatorMoveProxy();
        }
    }
}
