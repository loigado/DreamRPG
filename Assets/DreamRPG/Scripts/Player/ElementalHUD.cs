using UnityEngine;
using UnityEngine.UI;

public class ElementalHUD : MonoBehaviour
{
    // Singleton giúp Player gọi UI từ bất cứ đâu mà không cần kéo thả reference thủ công
    public static ElementalHUD Instance { get; private set; }

    [Header("Elemental Progress Bars (Images with 'Filled' type)")]
    public Image iceFill;
    public Image lightningFill;
    public Image windFill;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // Khởi tạo tất cả các bình chứa bằng 0 khi mới vào game
        if (iceFill != null) iceFill.fillAmount = 0f;
        if (lightningFill != null) lightningFill.fillAmount = 0f;
        if (windFill != null) windFill.fillAmount = 0f;
    }

    private void OnEnable()
    {
        PlayerStateMachine.OnElementAbsorbed += HandleElementAbsorbed;
    }

    private void OnDisable()
    {
        PlayerStateMachine.OnElementAbsorbed -= HandleElementAbsorbed;
    }

    /// <summary>
    /// Lắng nghe event khi năng lượng thay đổi và cập nhật thanh UI
    /// </summary>
    private void HandleElementAbsorbed(SkillElement element, float fillAmount)
    {
        switch (element)
        {
            case SkillElement.Bang:
                if (iceFill != null) iceFill.fillAmount = fillAmount;
                break;
            case SkillElement.Loi:
                if (lightningFill != null) lightningFill.fillAmount = fillAmount;
                break;
            case SkillElement.Gio:
                if (windFill != null) windFill.fillAmount = fillAmount;
                break;
        }
    }
}