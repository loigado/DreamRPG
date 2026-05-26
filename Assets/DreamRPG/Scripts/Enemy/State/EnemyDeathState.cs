using UnityEngine;
using System.Collections.Generic;

public class EnemyDeathState : EnemyState
{
    private float despawnTimer = 3f; 
    private bool hasCalculatedLength = false;
    private bool isDead = false;
    
    // 🟢 Lưu trữ danh sách các Collider đã bị tắt để phục hồi khi Object Pool hồi sinh quái
    private List<Collider> disabledColliders = new List<Collider>();

    public EnemyDeathState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        isDead = false;
        hasCalculatedLength = false;
        disabledColliders.Clear();
        
        // 1. Dừng mọi di chuyển & Tắt NavMesh
        stateMachine.ManualVelocity = Vector3.zero;
        if (stateMachine.Agent != null && stateMachine.Agent.isOnNavMesh)
        {
            stateMachine.Agent.isStopped = true;
            stateMachine.Agent.enabled = false; 
        }

        // Tắt chướng ngại vật NavMesh (nếu có) để không cản đường AI khác
        if (stateMachine.Obstacle != null) stateMachine.Obstacle.enabled = false;

        // =========================================================
        // 🟢 AAA FIX: DỌN SẠCH MỌI VẬT CẢN VẬT LÝ (GHOST OF TSUSHIMA)
        // =========================================================
        
        // Tắt CharacterController chính
        if (stateMachine.Controller != null) stateMachine.Controller.enabled = false;

        // Quét toàn bộ cơ thể quái (bao gồm cả khiên, vũ khí, hitbox)
        Collider[] allColliders = stateMachine.GetComponentsInChildren<Collider>();
        foreach (Collider col in allColliders)
        {
            // Chỉ tắt và lưu lại những Collider đang thực sự hoạt động
            if (col.enabled) 
            {
                col.enabled = false;
                disabledColliders.Add(col);
            }
        }

        stateMachine.PlayerTarget = null;

        // 3. Kích hoạt hoạt ảnh chết
        stateMachine.Anim.SetTrigger(EnemyConstants.HashDie); 
    }

    public override void Tick(float deltaTime)
    {
        if (isDead) return;

        // Đọc độ dài Clip Animation thực tế
        AnimatorStateInfo stateInfo = stateMachine.Anim.GetCurrentAnimatorStateInfo(0);

        // Chờ Animator chuyển thành công vào state "Die"
        if (!hasCalculatedLength && (stateInfo.IsName("Die") || stateInfo.shortNameHash == EnemyConstants.HashDie))
        {
            hasCalculatedLength = true;
            // Cộng thêm 2 giây nằm trên mặt đất trước khi bốc hơi
            despawnTimer = stateInfo.length + 2.0f; 
        }

        // Đếm ngược
        despawnTimer -= deltaTime;
        if (despawnTimer <= 0f)
        {
            isDead = true;
            DespawnEnemy();
        }
    }

    private void DespawnEnemy()
    {
        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.ReturnToPool(stateMachine.gameObject);
        }
        else
        {
            stateMachine.gameObject.SetActive(false);
        }
    }

    public override void Exit()
    {
        // Phục hồi lại Component phòng khi dùng Object Pool hồi sinh
        if (stateMachine.Controller != null) stateMachine.Controller.enabled = true;
        if (stateMachine.Agent != null) stateMachine.Agent.enabled = true;
        
        // 🟢 Bật lại chính xác các Collider đã bị tắt lúc chết
        foreach (Collider col in disabledColliders)
        {
            if (col != null) col.enabled = true;
        }
        disabledColliders.Clear();
    }
}