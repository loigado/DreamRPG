using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerIK : MonoBehaviour
{
    private Animator anim;
    
    [Header("Kéo bàn tay phải (WeaponSlot) vào đây")]
    public WeaponHolder weaponHolder; 

    [Range(0f, 1f)]
    public float LeftHandWeight = 1f;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        // Nếu không có Animator, hoặc không có túi đồ thì bỏ qua
        if (anim == null || weaponHolder == null) return;

        // BÍ QUYẾT Ở ĐÂY: Nếu đang cầm vũ khí 2 tay VÀ có điểm bám
        if (weaponHolder.IsCurrentWeaponTwoHanded && weaponHolder.CurrentLeftHandTarget != null)
        {
            // Ép tay trái bám chặt vào cán rìu/kiếm
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, LeftHandWeight);
            anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, LeftHandWeight);

            anim.SetIKPosition(AvatarIKGoal.LeftHand, weaponHolder.CurrentLeftHandTarget.position);
            anim.SetIKRotation(AvatarIKGoal.LeftHand, weaponHolder.CurrentLeftHandTarget.rotation);
        }
        else
        {
            // Trả tay trái tự do nếu cất vũ khí hoặc cầm 1 tay
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
        }
    }
}