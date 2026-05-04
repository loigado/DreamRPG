using UnityEngine;

// Lớp trừu tượng định nghĩa cấu trúc bắt buộc của một Trạng Thái
public abstract class EnemyState
{
    protected EnemyStateMachine stateMachine;

    // Hàm khởi tạo yêu cầu phải truyền Bộ Não vào
    public EnemyState(EnemyStateMachine stateMachine)
    {
        this.stateMachine = stateMachine;
    }

    // Chạy 1 lần duy nhất khi BẮT ĐẦU bước vào trạng thái này
    public virtual void Enter() { }

    // Chạy liên tục mỗi frame khi ĐANG Ở TRONG trạng thái này
    public virtual void Tick(float deltaTime) { }

    // Chạy 1 lần duy nhất khi THOÁT KHỎI trạng thái này
    public virtual void Exit() { }
}