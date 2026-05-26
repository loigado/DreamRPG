using UnityEngine;

public class EnemyClearPathState : EnemyState
{
    private Vector3 evadeDirection;
    private float evadeTimer;

    public EnemyClearPathState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public void Setup(Vector3 archerPos, Vector3 playerPos)
    {
        // 1. Tính đường thẳng từ cung thủ tới Player (Đường đạn bay)
        Vector3 lineOfFire = (playerPos - archerPos).normalized;
        lineOfFire.y = 0;

        // 2. Tính hướng từ cung thủ tới vị trí của mình
        Vector3 dirToMe = (stateMachine.transform.position - archerPos).normalized;
        dirToMe.y = 0;

        // 3. Dùng Cross Product để xác định mình đang đứng bên trái hay phải đường đạn
        float cross = Vector3.Cross(lineOfFire, dirToMe).y;

        // 4. Lấy hướng vuông góc để dạt ra. (Nếu cross < 0 -> dạt trái, ngược lại -> dạt phải)
        Vector3 perpDir = Vector3.Cross(lineOfFire, Vector3.up).normalized;
        if (cross < 0) perpDir = -perpDir;

        evadeDirection = perpDir;
    }

    public override void Enter()
    {
        evadeTimer = 1.0f; // Di chuyển dạt ra trong 1 giây (đủ để nhường đường bay)
        
        // Dừng hệ thống tìm đường tự động
        if (stateMachine.Agent != null && stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
        {
            stateMachine.Agent.isStopped = true;
        }
    }

    public override void Tick(float deltaTime)
    {
        evadeTimer -= deltaTime;
        
        // Di chuyển bằng code. Hệ thống FSM của bạn sẽ TỰ ĐỘNG đọc ManualVelocity này 
        // và truyền vào Animator BlendTree -> Quái sẽ phát hoạt ảnh đi ngang (Strafe) rất mượt!
        stateMachine.ManualVelocity = evadeDirection * (stateMachine.Stats.moveSpeed * 1.5f);
        
        // Mặt vẫn luôn hướng về phía Player
        stateMachine.FaceTarget(stateMachine.PlayerTarget.position, 2f);

        if (evadeTimer <= 0f)
        {
            stateMachine.SwitchState(stateMachine.StrafeState);
        }
    }

    public override void Exit()
    {
        stateMachine.ManualVelocity = Vector3.zero;
    }
}