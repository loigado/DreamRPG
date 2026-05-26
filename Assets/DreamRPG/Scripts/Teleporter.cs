using UnityEngine;

public class Teleporter : MonoBehaviour
{
    [Header("Cài đặt Dịch chuyển")]
    [Tooltip("Kéo một cục GameObject rỗng đặt ở sàn đấu Boss vào đây làm Điểm Đến")]
    public Transform destination;

    private void OnTriggerEnter(Collider other)
    {
        // Kiểm tra xem có đúng là Player dẫm vào cổng không
        if (other.CompareTag("Player"))
        {
            if (destination != null)
            {
                // LƯU Ý QUAN TRỌNG: Phải tạm tắt CharacterController trước khi dịch chuyển
                // Nếu không, hệ thống Vật lý của Unity sẽ tự kéo Player giật ngược về chỗ cũ
                CharacterController cc = other.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                // Dịch chuyển Player tới vị trí mới
                other.transform.position = destination.position;
                
                // Xoay mặt Player về hướng của điểm đến (để vô cửa là nhìn thẳng mặt Boss)
                other.transform.rotation = destination.rotation; 

                // Bật lại CharacterController cho Player đi lại bình thường
                if (cc != null) cc.enabled = true;
                
                Debug.Log("<color=green>Đã dịch chuyển Player tới phòng Boss!</color>");
            }
            else
            {
                Debug.LogWarning("⚠️ Cổng dịch chuyển này chưa được gắn Điểm Đến (Destination)!");
            }
        }
    }
}
