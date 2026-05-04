using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class ImpactFeedback : MonoBehaviour
{
    public static ImpactFeedback Instance { get; private set; }

    [Header("Hitstop Settings")]
    public float defaultHitstopDuration = 0.07f;
    private bool isHitstopping = false;

    private CinemachineImpulseSource impulseSource;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    // 🟢 HÀM KHỰNG HÌNH (HITSTOP)
    public void PlayHitstop(float duration = -1f)
    {
        if (isHitstopping) return;
        float d = duration < 0 ? defaultHitstopDuration : duration;
        StartCoroutine(HitstopCoroutine(d));
    }

    private IEnumerator HitstopCoroutine(float duration)
    {
        isHitstopping = true;
        float originalTimeScale = Time.timeScale;

        Time.timeScale = 0.05f; // Khựng gần như đứng yên
        yield return new WaitForSecondsRealtime(duration); // Dùng Realtime vì TimeScale đang bị giảm

        Time.timeScale = originalTimeScale;
        isHitstopping = false;
    }

    // 🟢 HÀM RUNG MÀN HÌNH (SCREEN SHAKE)
    public void PlayShake(float intensity = 1f)
    {
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(Vector3.one * intensity);
        }
    }
}
