using System.Collections;
using UnityEngine;

public class TeleportPortal : MonoBehaviour
{
    [Header("Cài đặt Cổng")]
    [Tooltip("Kéo điểm đến (Destination) vào ô này")]
    public Transform destinationPoint; 

    [Header("Hiệu ứng Điện ảnh (Fade)")]
    [Tooltip("Kéo cái FadeScreen (Có Canvas Group) ngoài Hierarchy vào đây")]
    public CanvasGroup fadeGroup;
    
    [Tooltip("Thời gian mờ dần (giây)")]
    public float fadeDuration = 1f;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StartCoroutine(TeleportPlayer(other.gameObject));
        }
    }

    private IEnumerator TeleportPlayer(GameObject player)
    {
        // 1. KÉO MÀN XUỐNG: Tối màn hình từ từ (Fade Out)
        if (fadeGroup != null)
        {
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                // Dùng Lerp để tăng dần độ đục (Alpha) từ 0 lên 1
                fadeGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
                yield return null; // Chờ tới khung hình tiếp theo
            }
            fadeGroup.alpha = 1f; // Chốt hạ đen thui 100%
        }

        // 2. PHÉP THUẬT DỊCH CHUYỂN (Lúc này màn hình đang đen xì, người chơi không thấy gì)
        CharacterController cc = player.GetComponent<CharacterController>();
        
        if (cc != null) cc.enabled = false; 

        player.transform.position = destinationPoint.position;
        player.transform.rotation = destinationPoint.rotation;

        if (cc != null) cc.enabled = true; 

        // Đợi 1 chút xíu cho khung hình ổn định, tránh giật lag khi tới vùng đất mới
        yield return new WaitForSeconds(0.2f);

        // 3. KÉO MÀN LÊN: Sáng màn hình từ từ (Fade In)
        if (fadeGroup != null)
        {
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                // Tự động giảm dần độ đục từ 1 về 0
                fadeGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
                yield return null;
            }
            fadeGroup.alpha = 0f; // Trả lại màn hình trong suốt
        }
    }
}