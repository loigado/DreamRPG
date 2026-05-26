using UnityEngine;
using System.Collections;

public class SimpleGhostClone : MonoBehaviour
{
    [Tooltip("Thời gian tồn tại của mỗi tàn ảnh (giây)")]
    public float lifeTime = 0.5f;
    
    private float fadeSpeed;
    private Material[] ghostMaterials;

    // 🟢 HÀM NÀY SẼ CHỤP ẢNH LẠI DÁNG CỦA NHÂN VẬT TẠI FRAME HIỆN TẠI
    public void SetupAndFade(Animator sourceAnim, Material ghostMat)
    {
        if (sourceAnim == null) { Destroy(gameObject); return; }

        // 1. Tìm toàn bộ lớp da (SkinnedMeshRenderer) trên nhân vật gốc
        SkinnedMeshRenderer[] sourceSMRs = sourceAnim.GetComponentsInChildren<SkinnedMeshRenderer>();
        ghostMaterials = new Material[sourceSMRs.Length];

        for (int i = 0; i < sourceSMRs.Length; i++)
        {
            // 2. CHỤP ẢNH (Bake): Trích xuất dáng pose hiện tại thành một Mesh tĩnh
            Mesh bakedMesh = new Mesh();
            sourceSMRs[i].BakeMesh(bakedMesh);

            // 3. Tạo một Object con để bọc cái Mesh tĩnh đó lại
            GameObject ghostPart = new GameObject("GhostPart_" + i);
            
            // Đưa vào làm con trước
            ghostPart.transform.SetParent(this.transform, false);

            // Ép vị trí, góc xoay và dùng localScale để tránh double-scale!
            // BakeMesh thường đã chứa sẵn scale của bone, nên nếu áp dụng lossyScale sẽ bị nhân đôi.
            ghostPart.transform.position = sourceSMRs[i].transform.position;
            ghostPart.transform.rotation = sourceSMRs[i].transform.rotation;
            ghostPart.transform.localScale = sourceSMRs[i].transform.localScale;

            // 4. Gắn Mesh và Material hệ Lôi vào
            MeshFilter mf = ghostPart.AddComponent<MeshFilter>();
            mf.mesh = bakedMesh;

            MeshRenderer mr = ghostPart.AddComponent<MeshRenderer>();
            
            // Tạo Material Instance để mỗi tàn ảnh mờ đi độc lập, không làm hỏng Material gốc
            Material matInstance = new Material(ghostMat); 
            mr.material = matInstance;
            
            ghostMaterials[i] = matInstance;
        }

        // 5. Bắt đầu đếm ngược để mờ dần
        fadeSpeed = 1f / lifeTime;
        StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        float alpha = 1f;
        while (alpha > 0)
        {
            alpha -= Time.deltaTime * fadeSpeed;
            
            // Ép thông số Alpha (độ trong suốt) của URP Material giảm dần
            foreach (Material mat in ghostMaterials)
            {
                if (mat != null && mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor");
                    c.a = alpha;
                    mat.SetColor("_BaseColor", c);
                }
            }
            yield return null;
        }
        
        // Mờ hẳn thì xóa đi cho nhẹ máy
        Destroy(gameObject);
    }
}