using UnityEngine;
using System.Collections;

public class AfterimageController : MonoBehaviour
{
    [Header("Character Reference")] // 🟢 Đổi tên cho hợp lý với cả Player và Enemy
    public Animator characterAnimator; 

    [Header("Ghost Dummy Prefab")]
    public GameObject dummyGhostPrefab;

    [Header("Elemental Materials")]
    public Material matKhongHe;
    public Material matLoi;
    public Material matBang;
    public Material matGio;
    public Material matLua;

    private Coroutine trailRoutine;
    private bool isTrailing = false;
    [Header("Trail Settings")]
    public float spawnRate = 0.1f; // Mặc định là 0.1 giây đẻ 1 bóng

    // =========================================================
    // 🟢 HÀM ĐA NĂNG: DÙNG CHO CẢ PLAYER VÀ ENEMY
    // =========================================================
    
    public void StartTrail(SkillElement element)
    {
        // 🟢 DÒNG CHẶN QUAN TRỌNG NHẤT: 
        // Nếu đang xả bóng rồi thì bỏ qua, không reset lại thời gian đếm ngược nữa!
        if (isTrailing) return; 

        if (trailRoutine != null) StopCoroutine(trailRoutine);
        isTrailing = true; 
        trailRoutine = StartCoroutine(SpawnTrailRoutine(element));
    }

    public void StopTrail()
    {
        isTrailing = false;
        if (trailRoutine != null) StopCoroutine(trailRoutine);
    }

    private IEnumerator SpawnTrailRoutine(SkillElement element)
    {
        while (isTrailing)
        {
            SpawnSingleGhost(element);
            
            // Lấy biến spawnRate ra dùng thay vì fix cứng 1 con số
            yield return new WaitForSecondsRealtime(spawnRate); 
        }
    }

    private void SpawnSingleGhost(SkillElement element)
    {
        Material targetMat = GetMaterialForElement(element);
        if (targetMat == null || dummyGhostPrefab == null || characterAnimator == null) return;

        GameObject clone = Instantiate(dummyGhostPrefab, characterAnimator.transform.position, characterAnimator.transform.rotation);
        
        SimpleGhostClone ghostScript = clone.GetComponent<SimpleGhostClone>();
        if (ghostScript != null) ghostScript.SetupAndFade(characterAnimator, targetMat);
    }

    private Material GetMaterialForElement(SkillElement element)
    {
        switch (element)
        {
            case SkillElement.Loi: return matLoi;
            case SkillElement.Bang: return matBang;
            case SkillElement.Gio: return matGio;
            case SkillElement.Lua: return matLua;
            default: return matKhongHe;
        }
    }
}