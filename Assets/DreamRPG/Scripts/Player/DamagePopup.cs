using UnityEngine;
using TMPro; // Bắt buộc phải có thư viện này để dùng TextMeshPro

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private float disappearTimer;
    private Color textColor;
    private Vector3 moveVector;
    private Transform mainCamera;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
    }

    public void Setup(float damageAmount, bool isHeavy)
    {
        mainCamera = Camera.main.transform;

        // Làm tròn sát thương
        textMesh.text = Mathf.RoundToInt(damageAmount).ToString();

        // Đòn nặng thì chữ To và màu Đỏ, đòn thường chữ Nhỏ màu Trắng
        if (isHeavy)
        {
            textMesh.fontSize = 3;
            textColor = Color.red; // Hoặc Color.yellow
        }
        else
        {
            textMesh.fontSize = 2;
            textColor = Color.yellow;
        }

        textMesh.color = textColor;
        disappearTimer = 1f; // Chữ tồn tại 1 giây trước khi mờ đi

        // Tạo hướng nảy ngẫu nhiên (Hơi văng sang 2 bên và bay lên trên)
        moveVector = new Vector3(Random.Range(-1f, 1f), 2f, Random.Range(-1f, 1f)).normalized * 2f;
    }

    private void Update()
    {
        // 1. Chữ bay lên
        transform.position += moveVector * Time.deltaTime;
        
        // 2. Chữ luôn xoay mặt về phía Camera người chơi
        if (mainCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.position);
        }

        // 3. Đếm ngược thời gian
        disappearTimer -= Time.deltaTime;
        if (disappearTimer < 0)
        {
            // Bắt đầu làm mờ chữ (Tụt Alpha)
            float fadeSpeed = 3f;
            textColor.a -= fadeSpeed * Time.deltaTime;
            textMesh.color = textColor;

            // Xóa chữ khi đã mờ hẳn
            if (textColor.a < 0)
            {
                // Nếu dùng Object Pool, đổi thành: ObjectPoolManager.Instance.ReturnToPool(gameObject);
                Destroy(gameObject); 
            }
        }
    }
}