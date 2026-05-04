using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerFootIK : MonoBehaviour
{
    private Animator animator;

    [Header("Feet Grounder Settings")]
    public bool enableFeetIk = true;
    [Range(0, 2f)] public float heightFromGroundRaycast = 1.14f;
    [Range(0, 3f)] public float raycastDownDistance = 1.5f;
    public LayerMask environmentLayer;
    public float footOffset = 0.15f;

    [Header("Pelvis Settings")]
    public float pelvisOffset = 0f;
    [Range(0, 1f)] public float pelvisUpAndDownSpeed = 0.28f;

    [Header("Smoothing")]
    [Range(0, 1f)] public float feetToIkPositionSpeed = 0.5f;
    public float rotationSpeed = 15f;

    [Header("Animation Variable Names")]
    public string leftFootAnimVariableName = "LeftFootCurve";
    public string rightFootAnimVariableName = "RightFootCurve";

    [Header("Debug")]
    public bool showSolverDebug = true;

    // Internal
    private float currentPelvisOffset = 0f;
    private Vector3 leftFootIkPos, rightFootIkPos;
    private Quaternion leftFootIkRot, rightFootIkRot;
    private float lastLeftFootAnimY, lastRightFootAnimY;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!enableFeetIk || animator == null) return;

        // 1. Đọc trọng số từ Animator Curves
        float leftWeight = animator.GetFloat(leftFootAnimVariableName);
        float rightWeight = animator.GetFloat(rightFootAnimVariableName);

        // Nếu không có curve, mặc định là 1 khi đứng yên
        if (leftWeight == 0 && rightWeight == 0)
        {
            leftWeight = rightWeight = animator.GetBool("isMoving") ? 0.3f : 1.0f;
        }

        // 2. Tính toán vị trí chân
        SolveFoot(AvatarIKGoal.LeftFoot, ref leftFootIkPos, ref leftFootIkRot, ref lastLeftFootAnimY);
        SolveFoot(AvatarIKGoal.RightFoot, ref rightFootIkPos, ref rightFootIkRot, ref lastRightFootAnimY);

        // 3. Áp dụng IK
        ApplyFootIK(AvatarIKGoal.LeftFoot, leftFootIkPos, leftFootIkRot, leftWeight);
        ApplyFootIK(AvatarIKGoal.RightFoot, rightFootIkPos, rightFootIkRot, rightWeight);

        // 4. Di chuyển hông (Pelvis)
        AdjustPelvisHeight();
    }

    private void SolveFoot(AvatarIKGoal foot, ref Vector3 targetPos, ref Quaternion targetRot, ref float lastAnimY)
    {
        Vector3 origin = animator.GetIKPosition(foot);
        lastAnimY = origin.y;

        Ray ray = new Ray(origin + Vector3.up * heightFromGroundRaycast, Vector3.down);
        
        if (showSolverDebug)
            Debug.DrawRay(origin + Vector3.up * heightFromGroundRaycast, Vector3.down * (raycastDownDistance + heightFromGroundRaycast), Color.cyan);

        if (Physics.Raycast(ray, out RaycastHit hit, raycastDownDistance + heightFromGroundRaycast, environmentLayer))
        {
            targetPos = origin;
            targetPos.y = hit.point.y + footOffset;

            // Xoay khớp mặt sàn
            targetRot = Quaternion.FromToRotation(Vector3.up, hit.normal) * transform.rotation;
        }
    }

    private void ApplyFootIK(AvatarIKGoal foot, Vector3 targetPos, Quaternion targetRot, float weight)
    {
        animator.SetIKPositionWeight(foot, weight);
        animator.SetIKRotationWeight(foot, weight);
        
        // Làm mượt vị trí chân bằng Lerp
        Vector3 currentPos = animator.GetIKPosition(foot);
        animator.SetIKPosition(foot, Vector3.Lerp(currentPos, targetPos, feetToIkPositionSpeed));
        
        // Làm mượt góc xoay cổ chân
        animator.SetIKRotation(foot, Quaternion.Slerp(animator.GetIKRotation(foot), targetRot, Time.deltaTime * rotationSpeed));
    }

    private void AdjustPelvisHeight()
    {
        float lOffset = leftFootIkPos.y - lastLeftFootAnimY;
        float rOffset = rightFootIkPos.y - lastRightFootAnimY;
        float targetOffset = Mathf.Min(lOffset, rOffset);

        if (targetOffset < 0)
        {
            currentPelvisOffset = Mathf.Lerp(currentPelvisOffset, targetOffset + pelvisOffset, pelvisUpAndDownSpeed);
        }
        else
        {
            currentPelvisOffset = Mathf.Lerp(currentPelvisOffset, 0, pelvisUpAndDownSpeed);
        }

        Vector3 bodyPos = animator.bodyPosition;
        bodyPos.y += currentPelvisOffset;
        animator.bodyPosition = bodyPos;
    }
}