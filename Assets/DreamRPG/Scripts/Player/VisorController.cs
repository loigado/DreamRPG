using UnityEngine;

public class VisorController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Kéo object chứa model phần Đầu/Mũ bảo hiểm của Knight vào đây")]
    public Renderer visorRenderer;
    [Tooltip("Thứ tự của Material chứa Visor (Thường là 0, nếu giáp có nhiều vật liệu thì nhập 1, 2...)")]
    public int materialIndex = 0; 

    [Header("Emission Settings")]
    [Tooltip("Tên biến phát sáng trong Shader (Thường là _EmissionColor)")]
    public string colorPropertyName = "_EmissionColor";
    [Tooltip("Độ rực rỡ của ánh sáng (Cần có Post Processing Bloom để thấy rõ)")]
    public float maxGlowIntensity = 5f;

    [Header("Element Colors (Kiếm Lôi, Rìu Băng, Cung Gió)")]
    public Color colorNone = Color.black;                // Không có năng lượng -> Tắt đèn
    public Color colorLoi = new Color(1f, 0.8f, 0f);     // Lôi -> Vàng chói
    public Color colorBang = new Color(0f, 1f, 1f);      // Băng -> Xanh lam (Cyan)
    public Color colorGio = new Color(0f, 1f, 0.2f);     // Gió -> Xanh lá mạ
    public Color colorLua = new Color(1f, 0.1f, 0f);     // Lửa -> Đỏ rực (Dùng khi hút quái hệ Lửa)

    private Material visorMaterial;

    private void Awake()
    {
        if (visorRenderer != null)
        {
            // Tạo một bản sao (Instance) của Material để không làm đổi màu quái vật nếu dùng chung model
            visorMaterial = visorRenderer.materials[materialIndex];
            visorMaterial.EnableKeyword("_EMISSION");
        }
    }

    private void OnEnable()
    {
        // Lắng nghe sự kiện Hút nguyên tố từ StateMachine
        PlayerStateMachine.OnElementAbsorbed += HandleElementAbsorbed;
    }

    private void OnDisable()
    {
        // Hủy lắng nghe khi nhân vật chết hoặc bị tắt
        PlayerStateMachine.OnElementAbsorbed -= HandleElementAbsorbed;
    }

    private void HandleElementAbsorbed(SkillElement element, float energyPercentage)
    {
        if (visorMaterial == null) return;

        Color targetColor = colorNone;

        // 1. Chọn màu sắc dựa theo Nguyên tố
        switch (element)
        {
            case SkillElement.Bang: targetColor = colorBang; break;
            case SkillElement.Loi: targetColor = colorLoi; break;
            case SkillElement.Gio: targetColor = colorGio; break;
            case SkillElement.Lua: targetColor = colorLua; break;
            case SkillElement.KhongHe: targetColor = colorNone; break;
        }

        // 2. Tính toán độ sáng (Càng nhiều năng lượng -> Mắt sáng càng rực)
        // Nếu có năng lượng, độ sáng tối thiểu là 30%, tối đa là 100% của maxGlowIntensity
        float currentIntensity = 0f;
        if (element != SkillElement.KhongHe)
        {
            currentIntensity = maxGlowIntensity * (0.3f + 0.7f * energyPercentage);
        }

        // 3. Ép màu vào Shader
        visorMaterial.SetColor(colorPropertyName, targetColor * currentIntensity);
    }
}