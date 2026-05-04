using UnityEngine;

public class CameraAnchorAAA : MonoBehaviour
{
    public Transform playerTransform;      // Kéo Player (gốc) vào đây
    public Vector3 offset = new Vector3(0.6f, 1.6f, 0); // Vị trí ngang vai giống Joel
    
    [Tooltip("Số càng cao càng bám sát, số nhỏ thì mượt nhưng trễ. Chỉnh tầm 20-30.")]
    public float smoothSpeed = 30f; 

    void LateUpdate() // Bắt buộc dùng LateUpdate để chạy sau khi nhân vật di chuyển
    {
        if (playerTransform == null) return;

        // 1. Tính toán vị trí mục tiêu (bám theo Player)
        Vector3 targetPosition = playerTransform.TransformPoint(offset);

        // 2. Dùng Lerp với tốc độ cực cao để lọc sạch micro-stutters của CharacterController
        // Nó vẫn cho cảm giác cứng cáp (Damping=0) nhưng mượt mà không bị giật li ti
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
        
        // 3. Xoay theo Player nhen sếp
        transform.rotation = playerTransform.rotation;
    }
}