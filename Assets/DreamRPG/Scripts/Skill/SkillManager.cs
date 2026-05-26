using UnityEngine;
using System.Collections.Generic;

public class SkillManager : MonoBehaviour
{
    // Lưu thời gian hồi chiêu của TỪNG KỸ NĂNG
    private Dictionary<SkillData, float> cooldownTimers = new Dictionary<SkillData, float>();
    private List<SkillData> cachedKeys = new List<SkillData>();

    void Update()
    {
        cachedKeys.Clear();
        cachedKeys.AddRange(cooldownTimers.Keys);
        foreach (var skill in cachedKeys)
        {
            if (cooldownTimers[skill] > 0) cooldownTimers[skill] -= Time.deltaTime;
        }
    }

    public bool IsSkillReady(SkillData skill)
    {
        if (skill == null) return false;
        if (!cooldownTimers.ContainsKey(skill)) return true; // Chưa dùng bao giờ -> Đã sẵn sàng
        return cooldownTimers[skill] <= 0;
    }

    public void StartCooldown(SkillData skill)
    {
        if (skill == null) return;
        cooldownTimers[skill] = skill.cooldownTime;
    }

    // ========================================================
    // 🟢 THÊM HÀM NÀY ĐỂ TRÍCH XUẤT THỜI GIAN GỬI LÊN UI
    // ========================================================
    public float GetRemainingCooldown(SkillData skill)
    {
        if (skill == null || !cooldownTimers.ContainsKey(skill)) return 0f;
        return Mathf.Max(0f, cooldownTimers[skill]);
    }
}