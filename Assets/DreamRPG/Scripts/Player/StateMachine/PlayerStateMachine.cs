using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Cinemachine; 
using FischlWorks;

public class PlayerStateMachine : MonoBehaviour, IDamageable
{
    [Header("References")]
    [field: SerializeField] public InputReader InputReader { get; private set; }
    [field: SerializeField] public CharacterController Controller { get; private set; }
    [field: SerializeField] public Animator Animator { get; private set; }
    [field: SerializeField] public StaminaSystem Stamina { get; private set; }

    [Header("Combat & Inventory")]
    [SerializeField] private WeaponData[] equippedWeapons = new WeaponData[3];
    public WeaponData CurrentWeapon { get; private set; }
    public bool IsInvulnerable { get; private set; } 
    public PlayerHealth PlayerHP { get; private set; }

    public static Action<Vector3> OnPlayerAttack;    // Truyền vị trí Player khi tấn công
    
    [Header("Physics Settings")]
    public float Gravity = -15f; 
    public float JumpHeight = 2f;
    
    [Header("Movement Settings")]
    public float FreeLookMovementSpeed = 5f;
    public float SprintSpeed = 8f;
    public float RotationDamping = 5f;

    [Header("Bow & Arrow Settings")]
    public GameObject ArrowPrefab;
    public Transform ArrowSpawnPoint; 
    public float MinArrowForce = 10f; 
    public float MaxArrowForce = 60f; 
    public float MaxDrawTime = 1.0f;
    [HideInInspector] public float CurrentCalculatedForce;
    [SerializeField] public LayerMask AimLayerMask;

    [Header("Virtual Arrow & Reload")]
    public GameObject virtualArrow; 
    public float initialDrawSpeed = 1f;   
    public float reloadDrawSpeed = 1.5f;  
    [HideInInspector] public bool isReloading = false;

    [Header("Ultimate System (Skill 2)")]
    public bool HasSkill2Buff { get; private set; }
    private float skill2Timer;
    private SkillData activeSkill2;

    [Header("Wind Buff System")]
    public bool HasWindBuff { get; private set; }
    private float windBuffTimer;
    private SkillData activeWindSkill;

    public bool HasWindSpreadBuff { get; private set; }
    private float windSpreadTimer;
    private SkillData activeWindSpreadSkill;

    private Dictionary<Renderer, Material[]> originalBodyMaterials = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, Material[]> originalBowMaterials = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, Material[]> originalArrowMaterials = new Dictionary<Renderer, Material[]>();
    
    [Header("Shooting Stats")]
    public float CurrentArrowSpread = 0f;

    [Header("Rotation Fix")]
    public float CharacterRotationOffset = 0f;

    [Header("Bow String Rigging")]
    public BowString bowStringScript;

    [Header("Camera Settings")]
    public GameObject AimCamera;
    
    // 🟢 FIX 1: LƯU TRỮ SẴN COMPONENT CINEMACHINE CAMERA
    public CinemachineCamera CachedAimCam { get; private set; }

    [Header("Aiming IK (Bẻ xương)")]
    public Transform SpineBone;
    public Vector3 SpineOffset;
    public Vector3 SpineAimOffset; 
    public Vector3 SpineBlockOffset; // Dùng để chỉnh sửa độ nghiêng khi Đỡ đòn

    [Header("UI References")]
    public GameObject CrosshairUI;

    // 🟢 FIX 2: LƯU TRỮ SẴN RECT TRANSFORM CỦA UI
    public RectTransform CachedCrosshairRect { get; private set; }

    [Header("Systems")]
    public TargetSystem TargetSys;
    [field: SerializeField] public SkillManager SkillManager { get; private set; }

    [Header("Weapon System")]
    public WeaponHolder weaponHolder { get; private set; }
    public csHomebrewIK FootIK { get; private set; }

    public float VerticalVelocity { get; set; }
    public Vector3 CurrentVelocity { get; set; }
    public Transform MainCameraTransform { get; private set; }
    public State currentState { get; private set; }

    [Header("Shield VFX (Kratos Style)")]
    [Tooltip("Gắn trực tiếp Game Object Khiên trên tay nhân vật vào đây để bật/tắt")]
    public GameObject ShieldVFX; 

    private readonly int WeaponIDHash = Animator.StringToHash("WeaponID");
    private readonly int SwitchWeaponHash = Animator.StringToHash("SwitchWeapon");

    private void Awake() 
    {
        Animator = GetComponent<Animator>();
        weaponHolder = GetComponentInChildren<WeaponHolder>();
        FootIK = GetComponent<csHomebrewIK>();
        PlayerHP = GetComponent<PlayerHealth>();
        if (Camera.main != null) MainCameraTransform = Camera.main.transform;

        // 🟢 FIX: Thực hiện Cache (lưu trữ) Component ngay từ đầu để không gọi GetComponent trong Update
        if (AimCamera != null) CachedAimCam = AimCamera.GetComponent<CinemachineCamera>();
        if (CrosshairUI != null) CachedCrosshairRect = CrosshairUI.GetComponent<RectTransform>();
    }

    private void OnEnable() => InputReader.SwitchWeaponEvent += OnWeaponSwitched;
    private void OnDisable() => InputReader.SwitchWeaponEvent -= OnWeaponSwitched;

    private void Start()
    {
        ToggleEquipmentVFX("WindSwirl", false);
        ToggleEquipmentVFX("MagicCircle1", false);
        ToggleEquipmentVFX("MagicCircle", false);
        ToggleEquipmentVFX("MagicCircle2", false);
        SwitchState(new PlayerMoveState(this));
    }

    private void Update()
    {
        currentState?.Tick(Time.deltaTime);
        UpdateHitRecoveryIFrame();

        if (HasSkill2Buff)
        {
            skill2Timer -= Time.deltaTime;
            if (skill2Timer <= 0) DeactivateSkill2Buff();
        }

        if (HasWindBuff)
        {
            windBuffTimer -= Time.deltaTime;
            if (windBuffTimer <= 0) DeactivateWindBowBuff();
        }

        if (HasWindSpreadBuff)
        {
            windSpreadTimer -= Time.deltaTime;
            if (windSpreadTimer <= 0) DeactivateWindSpreadBuff();
        }
    }

    private void LateUpdate()
    {
        if (currentState is PlayerAimState || currentState is PlayerShootState)
        {
            if (SpineBone != null)
            {
                float cameraPitch = MainCameraTransform.localEulerAngles.x;
                if (cameraPitch > 180) cameraPitch -= 360f;
                float clampedPitch = Mathf.Clamp(cameraPitch, -40f, 40f);
                SpineBone.rotation = transform.rotation * Quaternion.Euler(clampedPitch, 0, 0) * Quaternion.Euler(SpineAimOffset);
            }
        }
        else if (currentState is PlayerBlockState)
        {
            if (SpineBone != null)
            {
                // 🟢 Procedural IK (Bẻ xương ngực) khi Block
                // Giúp tay và ngực luôn hướng về phía địch/camera, trong khi chân vẫn đi theo hướng di chuyển
                Vector3 targetLookDir = MainCameraTransform.forward;
                if (TargetSys != null && TargetSys.IsHardLocking && TargetSys.GetCurrentTarget() != null)
                {
                    targetLookDir = (TargetSys.GetCurrentTarget().position - transform.position).normalized;
                }
                targetLookDir.y = 0;

                // Tính góc lệch giữa thân dưới (root) và hướng mục tiêu
                float angle = Vector3.SignedAngle(transform.forward, targetLookDir, Vector3.up);
                
                // Giới hạn góc bẻ xương tối đa 70 độ để không bị "gãy lưng"
                float clampedAngle = Mathf.Clamp(angle, -70f, 70f);

                // Áp dụng xoay Spine (Yaw theo mục tiêu)
                SpineBone.rotation = Quaternion.AngleAxis(clampedAngle, transform.up) * SpineBone.rotation;
                
                // 🟢 FIX: Áp dụng góc lệch thủ công (Để sửa lỗi bị nghiêng/lệch)
                SpineBone.rotation = SpineBone.rotation * Quaternion.Euler(SpineBlockOffset);
            }
        }
    }

    private void OnAnimatorMove()
    {
        if (Animator.applyRootMotion && Controller != null)
        {
            Vector3 motion = Animator.deltaPosition;
            motion.y += VerticalVelocity * Time.deltaTime;
            Controller.Move(motion);
        }
    }

    public void SwitchState(State newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter();
    }

    private void OnWeaponSwitched(int weaponIndex)
    {
        if (weaponIndex >= 0 && weaponIndex < equippedWeapons.Length && equippedWeapons[weaponIndex] != null)
            EquipWeapon(equippedWeapons[weaponIndex]);
    }

    public void EquipWeapon(WeaponData newWeapon)
    {
        if (currentState is PlayerAimState || 
            currentState is PlayerShootState ||
            currentState is PlayerAttackState || 
            currentState is PlayerRollState || 
            currentState is PlayerEquipState || 
            currentState is PlayerUnequipState)
        {
            Debug.Log("Đang bận thực hiện hành động, cấm rút vũ khí!");
            return;
        }

        if (HasSkill2Buff) DeactivateSkill2Buff();
        if (HasWindBuff) DeactivateWindBowBuff();
        if (HasWindSpreadBuff) DeactivateWindSpreadBuff();

        if (newWeapon == null)
        {
            CurrentWeapon = null;
            if (weaponHolder != null) weaponHolder.DeactivateAllWeapons();
            if (currentState is PlayerMoveState == false) SwitchState(new PlayerMoveState(this));
            return; 
        }

        if (CurrentWeapon == newWeapon) return;
        if (CurrentWeapon != null) SwitchState(new PlayerUnequipState(this, newWeapon));
        else
        {
            EquipWeaponDataOnly(newWeapon);
            SwitchState(new PlayerEquipState(this));
        }
    }

    public void EquipWeaponDataOnly(WeaponData newWeapon)
    {
        CurrentWeapon = newWeapon;
    }

    // 🟢 HÀM XỬ LÝ CHUNG CHO MỌI KỸ NĂNG
    public void TryExecuteSkill(int skillSlot)
    {
        if (CurrentWeapon == null) return;

        SkillData targetSkill = null;
        if (skillSlot == 1) targetSkill = CurrentWeapon.skill1;
        else if (skillSlot == 2) targetSkill = CurrentWeapon.skill2;
        else if (skillSlot == 3) targetSkill = CurrentWeapon.skill3;

        if (targetSkill == null || !SkillManager.IsSkillReady(targetSkill)) return;
        if (!Stamina.HasEnoughStamina(targetSkill.staminaCost)) return;

        string wName = CurrentWeapon.WeaponName.ToLower();

        // 🟢 GIAI ĐOẠN 4: PHÁT TÍN HIỆU TẤN CÔNG (Dành cho Skill Cận chiến)
        if (CurrentWeapon.Type == WeaponType.Melee)
        {
             // Phát tín hiệu để quái phản xạ gồng giáp hoặc lùi lại
             PlayerStateMachine.OnPlayerAttack?.Invoke(transform.position);

            if (wName.Contains("axe") || wName.Contains("rìu"))
            {
                if (skillSlot == 1) SwitchState(new PlayerIceAxeSlamState(this, targetSkill));
                else if (skillSlot == 2) SwitchState(new PlayerIceAxeUltimateState(this, targetSkill));
                else if (skillSlot == 3) SwitchState(new PlayerIceAxeLeapSlamState(this, targetSkill));
            }
            else 
            {
                if (skillSlot == 1) SwitchState(new PlayerWarpStrikeState(this, targetSkill));
                else if (skillSlot == 2) SwitchState(new PlayerPhantomArrayState(this, targetSkill));
                else if (skillSlot == 3) SwitchState(new PlayerLightningDomainState(this, targetSkill));
            }
        }
        // Xử lý Cung (Ngắm xong mới bắn nên chỉ lưu cờ Buff, CHƯA PHÁT TÍN HIỆU TẤN CÔNG Ở ĐÂY)
        else if (CurrentWeapon.Type == WeaponType.Ranged && (wName.Contains("bow") || wName.Contains("cung")))
        {
            if (skillSlot == 1) ActivateWindBowBuff(targetSkill, 10f);
            else if (skillSlot == 2) ActivateSkill2Buff(targetSkill, 10f);
            else if (skillSlot == 3) ActivateWindSpreadBuff(targetSkill, 15f);
        }
    }

    public void FireArrow()
    {
        if (ArrowPrefab == null || ArrowSpawnPoint == null) return;

        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, 100f, AimLayerMask) ? hit.point : ray.GetPoint(75f);
        Vector3 direction = targetPoint - ArrowSpawnPoint.position;

        bool isSkill1Ready = HasWindBuff && activeWindSkill != null && activeWindSkill.vfxPrefab != null;
        bool isSkill2Ready = HasSkill2Buff && activeSkill2 != null && activeSkill2.vfxPrefab != null;
        bool isSkill3Ready = HasWindSpreadBuff && activeWindSpreadSkill != null && activeWindSpreadSkill.vfxPrefab != null;

        // 🟢 FIX: DÙNG HỆ THỐNG POOL THAY CHO INSTANTIATE

        if (isSkill2Ready) // 🌪️ Chiêu Lốc Xoáy
        {
            GameObject tornado = ObjectPoolManager.Instance.SpawnFromPool(activeSkill2.vfxPrefab, ArrowSpawnPoint.position, Quaternion.LookRotation(direction));
            if (tornado.TryGetComponent<MovingTornado>(out MovingTornado tornadoScript))
            {
                tornadoScript.Setup(CurrentCalculatedForce * activeSkill2.damageMultiplier);
            }
            DeactivateSkill2Buff(); 
        }
        else if (isSkill1Ready && isSkill3Ready) // 🌬️ Combo 2 Buff Gió
        {
            for (int i = 0; i < 5; i++)
            {
                float angleOffset = (i - 2) * 12f; 
                Quaternion rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, angleOffset, 0);

                if (i == 2) 
                {
                    GameObject arrow = ObjectPoolManager.Instance.SpawnFromPool(activeWindSkill.vfxPrefab, ArrowSpawnPoint.position, rotation);
                    SetWindSwirlActive(arrow, true); 
                    if (arrow.TryGetComponent<WindArrow>(out WindArrow windArrow))
                        windArrow.Setup(CurrentCalculatedForce * 0.5f * activeWindSkill.damageMultiplier, CurrentCalculatedForce * 1.5f, 15f, false);
                }
                else 
                {
                    GameObject energy = ObjectPoolManager.Instance.SpawnFromPool(activeWindSpreadSkill.vfxPrefab, ArrowSpawnPoint.position, rotation);
                    SetWindSwirlActive(energy, true); 
                    if (energy.TryGetComponent<WindArrow>(out WindArrow wArrow))
                        wArrow.Setup(CurrentCalculatedForce * 0.35f * activeWindSpreadSkill.damageMultiplier, CurrentCalculatedForce * 1.5f, 15f, false);
                }
            }
            DeactivateWindBowBuff(); 
        }
        else if (isSkill1Ready) // 🏹 Bắn 1 tia gió xuyên thấu
        {
            GameObject arrow = ObjectPoolManager.Instance.SpawnFromPool(activeWindSkill.vfxPrefab, ArrowSpawnPoint.position, Quaternion.LookRotation(direction));
            SetWindSwirlActive(arrow, true); 
            if (arrow.TryGetComponent<WindArrow>(out WindArrow windArrow))
                windArrow.Setup(CurrentCalculatedForce * 0.5f * activeWindSkill.damageMultiplier, CurrentCalculatedForce * 1.5f, 15f, false);
            
            DeactivateWindBowBuff(); 
        }
        else if (isSkill3Ready) // 🏹 Bắn 5 tia hình quạt
        {
            for (int i = 0; i < 5; i++)
            {
                float angleOffset = (i - 2) * 12f; 
                Quaternion rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, angleOffset, 0);

                if (i == 2) 
                {
                    GameObject cArrow = ObjectPoolManager.Instance.SpawnFromPool(ArrowPrefab, ArrowSpawnPoint.position, rotation);
                    if (cArrow.TryGetComponent<ArrowProjectile>(out ArrowProjectile script))
                        script.Launch(CurrentCalculatedForce, CurrentCalculatedForce * 0.5f);
                }
                else 
                {
                    GameObject energy = ObjectPoolManager.Instance.SpawnFromPool(activeWindSpreadSkill.vfxPrefab, ArrowSpawnPoint.position, rotation);
                    SetWindSwirlActive(energy, false); 

                    if (energy.TryGetComponent<WindArrow>(out WindArrow wArrow))
                        wArrow.Setup(CurrentCalculatedForce * 0.35f * activeWindSpreadSkill.damageMultiplier, CurrentCalculatedForce * 0.2f, CurrentCalculatedForce, true); 
                }
            }
        }
        else // 🏹 BẮN THƯỜNG
        {
            GameObject nArrow = ObjectPoolManager.Instance.SpawnFromPool(ArrowPrefab, ArrowSpawnPoint.position, Quaternion.LookRotation(direction));
            if (nArrow.TryGetComponent<ArrowProjectile>(out ArrowProjectile normalArrowScript))
                normalArrowScript.Launch(CurrentCalculatedForce, CurrentCalculatedForce * 0.5f);
        }
    }

    private void SetWindSwirlActive(GameObject parent, bool isActive)
    {
        ParticleSystem[] allPs = parent.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allPs)
        {
            if (ps.gameObject.name == "WindSwirl") 
            {
                ps.gameObject.SetActive(isActive);
                var emission = ps.emission;
                emission.enabled = isActive;
                if (isActive) ps.Play();
                else { ps.Stop(); ps.Clear(); }
            }
        }
    }

    public void ActivateSkill2Buff(SkillData skill, float duration) { HasSkill2Buff = true; skill2Timer = duration; activeSkill2 = skill; UpdateWindVisuals(); Stamina.UseStamina(skill.staminaCost); SkillManager.StartCooldown(skill); }
    public void DeactivateSkill2Buff() { HasSkill2Buff = false; UpdateWindVisuals(); }
    public void ActivateWindBowBuff(SkillData skill, float duration) { HasWindBuff = true; windBuffTimer = duration; activeWindSkill = skill; UpdateWindVisuals(); Stamina.UseStamina(skill.staminaCost); SkillManager.StartCooldown(skill); }
    public void DeactivateWindBowBuff() { HasWindBuff = false; UpdateWindVisuals(); }
    public void ActivateWindSpreadBuff(SkillData skill, float duration) { HasWindSpreadBuff = true; windSpreadTimer = duration; activeWindSpreadSkill = skill; UpdateWindVisuals(); Stamina.UseStamina(skill.staminaCost); SkillManager.StartCooldown(skill); }
    public void DeactivateWindSpreadBuff() { HasWindSpreadBuff = false; UpdateWindVisuals(); }

    public void UpdateWindVisuals() 
    {
        Material bowMat = null;
        Material arrowMat = null;

        if (HasSkill2Buff && activeSkill2 != null) { bowMat = activeSkill2.ghostMaterial; arrowMat = activeSkill2.ghostMaterial; }
        else if (HasWindSpreadBuff && activeWindSpreadSkill != null) { bowMat = activeWindSpreadSkill.ghostMaterial; arrowMat = null; }
        else if (HasWindBuff && activeWindSkill != null) { bowMat = null; arrowMat = null; }

        SetBowMaterial(bowMat != null, bowMat);
        SetArrowMaterial(arrowMat != null, arrowMat);

        bool isHoldingArrow = virtualArrow != null && virtualArrow.activeSelf;
        ToggleEquipmentVFX("MagicCircle1", HasWindBuff);       
        ToggleEquipmentVFX("MagicCircle", HasWindSpreadBuff);  
        ToggleEquipmentVFX("MagicCircle2", HasSkill2Buff);     
        ToggleEquipmentVFX("WindSwirl", HasWindBuff && isHoldingArrow);
    }

    private void SetBowMaterial(bool isWind, Material windMat)
    {
        if (weaponHolder == null || weaponHolder.CurrentWeaponModel == null) return;
        Renderer[] renderers = weaponHolder.CurrentWeaponModel.GetComponentsInChildren<Renderer>();
        if (isWind && windMat != null)
        {
            foreach (Renderer ren in renderers)
            {
                if (ren.gameObject.name.ToLower().Contains("bow") || ren.CompareTag("Weapon"))
                {
                    if (!originalBowMaterials.ContainsKey(ren)) originalBowMaterials[ren] = ren.sharedMaterials;
                    Material[] newMats = new Material[ren.sharedMaterials.Length];
                    for (int i = 0; i < newMats.Length; i++) newMats[i] = windMat;
                    ren.sharedMaterials = newMats;
                }
            }
        }
        else
        {
            foreach (var pair in originalBowMaterials) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            originalBowMaterials.Clear();
        }
    }

    private void SetArrowMaterial(bool isActive, Material arrowMat)
    {
        if (virtualArrow == null) return;
        Renderer[] renderers = virtualArrow.GetComponentsInChildren<Renderer>(true);
        if (isActive && arrowMat != null)
        {
            foreach (Renderer ren in renderers)
            {
                if (ren is ParticleSystemRenderer) continue;
                if (!originalArrowMaterials.ContainsKey(ren)) originalArrowMaterials[ren] = ren.sharedMaterials;
                Material[] newMats = new Material[ren.sharedMaterials.Length];
                for (int i = 0; i < newMats.Length; i++) newMats[i] = arrowMat;
                ren.sharedMaterials = newMats;
            }
        }
        else
        {
            foreach (var pair in originalArrowMaterials) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            originalArrowMaterials.Clear();
        }
    }

    private void ToggleEquipmentVFX(string vfxName, bool isActive)
    {
        if (virtualArrow != null) ToggleVFXOnTarget(virtualArrow.transform, vfxName, isActive);
        if (weaponHolder != null && weaponHolder.CurrentWeaponModel != null) ToggleVFXOnTarget(weaponHolder.CurrentWeaponModel.transform, vfxName, isActive);
    }

    private void ToggleVFXOnTarget(Transform parentTarget, string vfxName, bool isActive)
    {
        Transform[] allChildren = parentTarget.GetComponentsInChildren<Transform>(true);
        foreach (var child in allChildren)
        {
            if (child.name == vfxName) 
            {
                child.gameObject.SetActive(isActive);
                ParticleSystem ps = child.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var em = ps.emission;
                    em.enabled = isActive;
                    if (isActive) ps.Play();
                    else { ps.Stop(); ps.Clear(); }
                }
            }
        }
    }

    public void ApplyOverlayMaterials(Material[] overlayMats)
    {
        if (overlayMats == null || overlayMats.Length == 0) return;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer ren in renderers)
        {
            if (ren.CompareTag("Weapon")) continue;
            if (!originalBodyMaterials.ContainsKey(ren)) originalBodyMaterials[ren] = ren.sharedMaterials;
            Material[] originalMats = originalBodyMaterials[ren];
            Material[] newMats = new Material[originalMats.Length + overlayMats.Length];
            for (int i = 0; i < originalMats.Length; i++) newMats[i] = originalMats[i];
            for (int i = 0; i < overlayMats.Length; i++) newMats[originalMats.Length + i] = overlayMats[i];
            ren.sharedMaterials = newMats;
        }
    }

    public void RevertIceForm()
    {
        foreach (var pair in originalBodyMaterials) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        originalBodyMaterials.Clear();
    }

    // === HIT RECOVERY I-FRAME ===
    private float hitRecoveryTimer = 0f;
    private const float HIT_RECOVERY_IFRAME = 0.3f; // 0.3s bất tử sau khi hết choáng

    /// <summary>Bật i-frame sau khi hồi phục từ ImpactState (chống stun-lock).</summary>
    public void StartHitRecoveryIFrame()
    {
        hitRecoveryTimer = HIT_RECOVERY_IFRAME;
        IsInvulnerable = true;
    }

    private void UpdateHitRecoveryIFrame()
    {
        if (hitRecoveryTimer > 0f)
        {
            hitRecoveryTimer -= Time.deltaTime;
            if (hitRecoveryTimer <= 0f) IsInvulnerable = false;
        }
    }

    public void TakeDamage(float damage, Vector3 attackerPos)
    {
        if (IsInvulnerable) return;

        // Block/Parry check — nếu đang giữ Block thì xử lý riêng
        if (currentState is PlayerBlockState blockState)
        {
            blockState.HandleBlockedHit(damage, attackerPos);
            return;
        }

        if (PlayerHP != null)
        {
            PlayerHP.TakeDamage(damage);
            if (PlayerHP.IsDead)
            {
                SwitchState(new PlayerDeathState(this));
                return;
            }
        }
        SwitchState(new PlayerImpactState(this, attackerPos));
    }
    public void EnableInvincibility() => IsInvulnerable = true;
    public void DisableInvincibility() => IsInvulnerable = false;
    public void EnableHitbox() { if (CurrentWeapon != null && weaponHolder != null) weaponHolder.EnableWeaponHitbox(CurrentWeapon.Damage); }
    public void DisableHitbox() { if (weaponHolder != null) weaponHolder.DisableWeaponHitbox(); }
    public void AE_ShowWeapon() { if (weaponHolder != null && CurrentWeapon != null) weaponHolder.ActivateWeapon(CurrentWeapon.WeaponAnimID); }
    public void AE_HideWeapon() { if (weaponHolder != null) weaponHolder.DeactivateAllWeapons(); }
    public void AE_EnableBowIK() { if (bowStringScript != null) bowStringScript.isDrawing = true; }
    public void AE_DisableBowIK() { if (bowStringScript != null) bowStringScript.isDrawing = false; }
    
    public void AE_OnTouchQuiver(int isUnequipping) 
    { 
        if (virtualArrow == null) return; 
        if (isUnequipping == 0) { virtualArrow.SetActive(true); UpdateWindVisuals(); }
        else if (isUnequipping == 1) { virtualArrow.SetActive(false); UpdateWindVisuals(); }
    }
    
    public void ToggleWeaponVisual(bool isVisible) 
    { 
        if (weaponHolder != null && weaponHolder.CurrentWeaponModel != null) weaponHolder.CurrentWeaponModel.SetActive(isVisible); 
    }
}