using UnityEngine;

public class BowString : MonoBehaviour
{
    [Header("Trạng thái")]
    public bool isDrawing = false;

    [Header("Cấu hình Xương")]
    public Transform stringBone; 
    public Transform rightHandPullPoint; 
    public Transform restPosition; 

    [Header("Độ nảy của dây (Khi bắn)")]
    public float releaseSpeed = 40f; // Chỉ dùng để làm mượt lúc nhả dây

    private void LateUpdate()
    {
        if (stringBone == null) return;

        // 1. LÚC KÉO CUNG: KHÔNG DÙNG LERP! Ép vị trí dây dính chặt 100% vào tay
        if (isDrawing && rightHandPullPoint != null)
        {
            stringBone.position = rightHandPullPoint.position;
        }
        // 2. LÚC NHẢ CUNG: Mới dùng Lerp để dây bật nảy về chỗ cũ tưng tưng
        else if (restPosition != null)
        {
            stringBone.position = Vector3.Lerp(stringBone.position, restPosition.position, Time.deltaTime * releaseSpeed);
        }
    }
}