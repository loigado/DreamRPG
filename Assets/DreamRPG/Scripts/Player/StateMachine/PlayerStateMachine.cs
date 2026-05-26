using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Cinemachine; 
using System.Collections;

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

    public static Action<Vector3> OnPlayerAttack;    
    
    [Header("Physics Settings")]
    public float Gravity = -15f; 
    public float JumpHeight = 2f;
    public bool CanDoubleJump { get; set; } = true;
    
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
    public CinemachineCamera CachedAimCam { get; private set; }

    public Transform SpineBone;
    public Vector3 SpineOffset;
    public Vector3 SpineAimOffset; 
    public Vector3 SpineBlockOffset; 
    public float ParryYawOffset = -30f; // Góc bẻ ngược lại bên trái để bù trừ đòn Parry nghiêng qua phải 

    [Header("UI References")]
    public GameObject CrosshairUI;
    public RectTransform CachedCrosshairRect { get; private set; }
    public ActionHUD actionHUD;

    [Header("Empty Vessel Settings")]
    public System.Collections.Generic.Dictionary<SkillElement, float> ElementalEnergies { get; private set; } = new System.Collections.Generic.Dictionary<SkillElement, float>();
    public float MaxElementalEnergy { get; private set; } = 100f;
    public static Action<SkillElement, float> OnElementAbsorbed; 

    [Header("Systems")]
    public TargetSystem TargetSys;
    [field: SerializeField] public SkillManager SkillManager { get; private set; }

    [Header("Weapon System")]
    public WeaponHolder weaponHolder { get; private set; }

    public float VerticalVelocity { get; set; }
    public Vector3 CurrentVelocity { get; set; }
    public Transform MainCameraTransform { get; private set; }
    public State currentState { get; private set; }

    [Header("Shield VFX (Kratos Style)")]
    public GameObject ShieldVFX; 
    
    public AfterimageController afterimageFX;

    public bool HasPerformedPerfectDodge { get; set; } = false;
    public Transform LastDodgedEnemy { get; set; }

    [HideInInspector] public float RootMotionMultiplier = 1f;
    [HideInInspector] public bool IsAttackSliding = false;
    private Coroutine slowMoCoroutine;

    private readonly int WeaponIDHash = Animator.StringToHash("WeaponID");
    private readonly int SwitchWeaponHash = Animator.StringToHash("SwitchWeapon");

    private void Awake() 
    {
        Animator = GetComponent<Animator>();
        weaponHolder = GetComponentInChildren<WeaponHolder>();
        PlayerHP = GetComponent<PlayerHealth>();
        if (Camera.main != null) MainCameraTransform = Camera.main.transform;

        if (AimCamera != null) CachedAimCam = AimCamera.GetComponent<CinemachineCamera>();
        if (CrosshairUI != null) CachedCrosshairRect = CrosshairUI.GetComponent<RectTransform>();
        afterimageFX = GetComponent<AfterimageController>();
        
        // Tự động tìm SkillManager nếu chưa kéo vào Inspector
        if (SkillManager == null) 
        {
            SkillManager = FindObjectOfType<SkillManager>();
            if (SkillManager == null) Debug.LogError("❌ KHÔNG TÌM THẤY SKILLMANAGER TRONG SCENE!");
        }
    }

    private void OnEnable() => InputReader.SwitchWeaponEvent += OnWeaponSwitched;
    private void OnDisable() => InputReader.SwitchWeaponEvent -= OnWeaponSwitched;

    private void Start()
    {
        ToggleEquipmentVFX("WindSwirl", false);
        ToggleEquipmentVFX("MagicCircle1", false);
        ToggleEquipmentVFX("MagicCircle", false);
        ToggleEquipmentVFX("MagicCircle2", false);
        SwitchState(new PlayerMovementState(this));
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

        // 🟢 GỌI CẬP NHẬT UI HỒI CHIÊU LIÊN TỤC
        UpdateSkillUI(); 
    }
    // 🟢 HÀM KIỂM TRA TÀI NGUYÊN ĐỘC LẬP (Không gộp chung Nguyên Tố và Thể Lực)
    private bool CanAffordSkill(SkillData skill)
    {
        if (skill == null) return false;

        // 1. Kiểm tra Nguyên Tố (Chỉ check nếu chiêu này có hệ)
        if (skill.element != SkillElement.KhongHe)
        {
            // Trượt nếu sai hệ, hoặc dùng hệ nhưng không đủ năng lượng
            if (!ElementalEnergies.ContainsKey(skill.element) || ElementalEnergies[skill.element] < skill.elementCost) return false;
        }

        // 2. Kiểm tra Thể Lực (Chỉ check nếu chiêu này có yêu cầu tốn thể lực)
        if (skill.staminaCost > 0 && Stamina != null)
        {
            // Trượt nếu cạn thể lực
            if (!Stamina.HasEnoughStamina(skill.staminaCost)) return false;
        }

        return true; // Phải qua được cả 2 bài test thì Icon mới sáng lên!
    }
    // 🟢 CẬP NHẬT LẠI HÀM NÀY ĐỂ BÁO TÌNH TRẠNG TÀI NGUYÊN CHO UI
    private void UpdateSkillUI()
    {
        if (actionHUD != null && CurrentWeapon != null && SkillManager != null)
        {
            // --- SKILL 1 ---
            if (CurrentWeapon.skill1 != null)
            {
                actionHUD.UpdateSkillCooldown(0, SkillManager.GetRemainingCooldown(CurrentWeapon.skill1), CurrentWeapon.skill1.cooldownTime);
                actionHUD.UpdateSkillUsability(0, CanAffordSkill(CurrentWeapon.skill1)); // 🟢 Cập nhật sáng/tối
            }
            
            // --- SKILL 2 ---
            if (CurrentWeapon.skill2 != null)
            {
                actionHUD.UpdateSkillCooldown(1, SkillManager.GetRemainingCooldown(CurrentWeapon.skill2), CurrentWeapon.skill2.cooldownTime);
                actionHUD.UpdateSkillUsability(1, CanAffordSkill(CurrentWeapon.skill2)); // 🟢 Cập nhật sáng/tối
            }
            
            // --- SKILL 3 ---
            if (CurrentWeapon.skill3 != null)
            {
                actionHUD.UpdateSkillCooldown(2, SkillManager.GetRemainingCooldown(CurrentWeapon.skill3), CurrentWeapon.skill3.cooldownTime);
                actionHUD.UpdateSkillUsability(2, CanAffordSkill(CurrentWeapon.skill3)); // 🟢 Cập nhật sáng/tối
            }
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
                Vector3 targetLookDir = MainCameraTransform.forward;
                if (TargetSys != null && TargetSys.IsHardLocking && TargetSys.GetCurrentTarget() != null)
                {
                    targetLookDir = (TargetSys.GetCurrentTarget().position - transform.position).normalized;
                }
                targetLookDir.y = 0;

                float angle = Vector3.SignedAngle(transform.forward, targetLookDir, Vector3.up);
                float clampedAngle = Mathf.Clamp(angle, -70f, 70f);

                SpineBone.rotation = Quaternion.AngleAxis(clampedAngle, transform.up) * SpineBone.rotation;
                SpineBone.rotation = SpineBone.rotation * Quaternion.Euler(SpineBlockOffset);
            }
        }
        else if (currentState is PlayerParryDecisionState)
        {
            if (SpineBone != null)
            {
                Transform parryTarget = null;
                if (currentState is PlayerParryDecisionState decision)
                {
                    parryTarget = decision.ParriedEnemy;
                }

                if (parryTarget == null && TargetSys != null)
                {
                    parryTarget = TargetSys.GetCurrentTarget();
                }

                if (parryTarget != null)
                {
                    Vector3 targetLookDir = (parryTarget.position - transform.position).normalized;
                    targetLookDir.y = 0;

                    if (targetLookDir.sqrMagnitude > 0.01f)
                    {
                        float angle = Vector3.SignedAngle(transform.forward, targetLookDir, Vector3.up);
                        float finalAngle = angle + ParryYawOffset;
                        SpineBone.rotation = Quaternion.AngleAxis(finalAngle, transform.up) * SpineBone.rotation;
                    }
                }
                else
                {
                    SpineBone.rotation = Quaternion.AngleAxis(ParryYawOffset, transform.up) * SpineBone.rotation;
                }
            }
        }
    }

    private void OnAnimatorMove()
    {
        if (Animator.applyRootMotion && Controller != null)
        {
            Vector3 rootMotionDelta = Animator.deltaPosition;
            rootMotionDelta.x *= RootMotionMultiplier;
            rootMotionDelta.z *= RootMotionMultiplier;
            
            Vector3 motion = rootMotionDelta;
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
            if (currentState is PlayerMovementState == false) SwitchState(new PlayerMovementState(this));
            return; 
        }

        if (CurrentWeapon == newWeapon) return;

        if (CurrentWeapon == null) 
        {
            EquipWeaponDataOnly(newWeapon);
            SwitchState(new PlayerEquipState(this));
        }
        else 
        {
            SwitchState(new PlayerUnequipState(this, newWeapon));
        }
    }

    public void EquipWeaponDataOnly(WeaponData newWeapon)
    {
        CurrentWeapon = newWeapon;

        if (actionHUD != null && CurrentWeapon != null)
        {
            Sprite s1 = CurrentWeapon.Skill1Icon != null ? CurrentWeapon.Skill1Icon : (CurrentWeapon.skill1 != null ? CurrentWeapon.skill1.icon : null);
            Sprite s2 = CurrentWeapon.Skill2Icon != null ? CurrentWeapon.Skill2Icon : (CurrentWeapon.skill2 != null ? CurrentWeapon.skill2.icon : null);
            Sprite s3 = CurrentWeapon.Skill3Icon != null ? CurrentWeapon.Skill3Icon : (CurrentWeapon.skill3 != null ? CurrentWeapon.skill3.icon : null);

            actionHUD.SwitchWeaponProfile(CurrentWeapon.WeaponIcon, s1, s2, s3);
        }
    }

    public void TryExecuteSkill(int skillSlot)
    {
        if (CurrentWeapon == null) return;

        SkillData targetSkill = null;
        if (skillSlot == 1) targetSkill = CurrentWeapon.skill1;
        else if (skillSlot == 2) targetSkill = CurrentWeapon.skill2;
        else if (skillSlot == 3) targetSkill = CurrentWeapon.skill3;

        if (targetSkill == null || !SkillManager.IsSkillReady(targetSkill)) return;

        // ==========================================
        // 🟢 1. XỬ LÝ NĂNG LƯỢNG NGUYÊN TỐ (Độc Lập)
        // ==========================================
        if (targetSkill.element != SkillElement.KhongHe)
        {
            if (!ElementalEnergies.ContainsKey(targetSkill.element) || ElementalEnergies[targetSkill.element] < targetSkill.elementCost)
            {
                Debug.Log($"<color=red>Thiếu Năng Lượng Nguyên Tố {targetSkill.element}!</color>");
                return;
            }
        }

        // ==========================================
        // 🟢 2. XỬ LÝ THỂ LỰC (Độc Lập)
        // ==========================================
        if (targetSkill.staminaCost > 0 && Stamina != null)
        {
            // LƯU Ý: Nếu là Cung, các hàm Activate...Buff bên dưới của bạn đang tự gọi Stamina.UseStamina() rồi. 
            // Nên ở đây ta CHỈ KIỂM TRA ĐIỀU KIỆN thôi, không trừ vội để tránh bị trừ 2 lần.
            if (!Stamina.HasEnoughStamina(targetSkill.staminaCost)) 
            {
                Debug.Log("<color=red>Thiếu Thể Lực!</color>");
                return;
            }
        }

        // ==========================================
        // 🟢 3. TRỪ TÀI NGUYÊN VÀ THỰC THI
        // ==========================================
        // Trừ Năng Lượng Nguyên Tố
        if (targetSkill.element != SkillElement.KhongHe)
        {
            ElementalEnergies[targetSkill.element] -= targetSkill.elementCost;
            if (ElementalEnergies[targetSkill.element] <= 0f) ElementalEnergies[targetSkill.element] = 0f;
            
            OnElementAbsorbed?.Invoke(targetSkill.element, ElementalEnergies[targetSkill.element] / MaxElementalEnergy);
            Debug.Log($"<color=cyan>⚡ TIÊU HAO {targetSkill.elementCost} NĂNG LƯỢNG {targetSkill.element}!</color>");
        }

        string wName = CurrentWeapon.WeaponName.ToLower();

        if (CurrentWeapon.Type == WeaponType.Melee)
        {
            PlayerStateMachine.OnPlayerAttack?.Invoke(transform.position);
            
            // Trừ Thể Lực cho Cận Chiến
            if (targetSkill.staminaCost > 0 && Stamina != null) Stamina.UseStamina(targetSkill.staminaCost);

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

        if (isSkill2Ready) 
        {
            GameObject tornado = ObjectPoolManager.Instance.SpawnFromPool(activeSkill2.vfxPrefab, ArrowSpawnPoint.position, Quaternion.LookRotation(direction));
            if (tornado.TryGetComponent<MovingTornado>(out MovingTornado tornadoScript))
            {
                tornadoScript.Setup(CurrentCalculatedForce * activeSkill2.damageMultiplier);
            }
            DeactivateSkill2Buff(); 
        }
        else if (isSkill1Ready && isSkill3Ready) 
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
        else if (isSkill1Ready) 
        {
            GameObject arrow = ObjectPoolManager.Instance.SpawnFromPool(activeWindSkill.vfxPrefab, ArrowSpawnPoint.position, Quaternion.LookRotation(direction));
            SetWindSwirlActive(arrow, true); 
            if (arrow.TryGetComponent<WindArrow>(out WindArrow windArrow))
                windArrow.Setup(CurrentCalculatedForce * 0.5f * activeWindSkill.damageMultiplier, CurrentCalculatedForce * 1.5f, 15f, false);
            
            DeactivateWindBowBuff(); 
        }
        else if (isSkill3Ready) 
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
        else 
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

    private float hitRecoveryTimer = 0f;
    private const float HIT_RECOVERY_IFRAME = 0.3f; 

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

    public void TakeDamage(float damage, Vector3 attackerPos, bool isHeavy = false)
    {
        TakeDamage(damage, attackerPos, AttackGlint.Normal);
    }

    public void TakeDamage(float damage, Vector3 attackerPos, AttackGlint glint = AttackGlint.Normal)
    {
        if (currentState is PlayerRollState rollState && rollState.IsPerfectDodgeWindow)
        {
            Collider[] colliders = Physics.OverlapSphere(attackerPos, 3f);
            Transform trueAttacker = null;
            float closestDistance = Mathf.Infinity;

            foreach (var col in colliders)
            {
                EnemyStateMachine enemy = col.GetComponent<EnemyStateMachine>();
                if (enemy != null && enemy.Stats != null)
                {
                    float dist = Vector3.Distance(attackerPos, enemy.transform.position);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        trueAttacker = enemy.transform;
                    }
                }
            }

            if (trueAttacker != null)
            {
                EnemyStateMachine enemy = trueAttacker.GetComponent<EnemyStateMachine>();
                HasPerformedPerfectDodge = true;
                LastDodgedEnemy = trueAttacker; 
                
                SkillElement enemyElem = enemy.Stats.enemyElement;
                if (afterimageFX != null) afterimageFX.StartTrail(enemyElem);
                
                if (slowMoCoroutine != null) StopCoroutine(slowMoCoroutine);
                slowMoCoroutine = StartCoroutine(PerfectDodgeSlowMotionRoutine());
                return; 
            }
        }

        if (IsInvulnerable) return;

        if (currentState is PlayerBlockState blockState)
        {
            bool successfullyBlocked = blockState.HandleBlockedHit(damage, attackerPos, glint);
            if (successfullyBlocked) return; 
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
    public void ApplyKnockback(Vector3 knockbackForce)
    {
        if (IsInvulnerable) return;
        SwitchState(new PlayerImpactState(this, knockbackForce, true));
    }

    public void TakeHeavyDamage(float damage, Vector3 attackerPos, Vector3 knockbackForce)
    {
        if (currentState is PlayerRollState rollState && rollState.IsPerfectDodgeWindow)
        {
            Collider[] colliders = Physics.OverlapSphere(attackerPos, 3f);
            foreach (var col in colliders)
            {
                EnemyStateMachine enemy = col.GetComponent<EnemyStateMachine>();
                if (enemy != null && enemy.Stats != null)
                {
                    SkillElement enemyElem = enemy.Stats.enemyElement;
                    Debug.Log($"<color=cyan>💨 PERFECT DODGE ĐÒN NẶNG! Liều mạng cướp 40 năng lượng hệ {enemyElem}!</color>");
                    return; 
                }
            }
            return;
        }

        if (IsInvulnerable) return;

        if (currentState is PlayerBlockState)
        {
            Debug.Log("<color=red>💥 VỠ PHÒNG THỦ! Đòn đánh này không thể bị chặn!</color>");
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
        
        SwitchState(new PlayerImpactState(this, knockbackForce, true));
    }

    public void AbsorbElement(SkillElement incomingElement, float amount)
    {
        if (incomingElement == SkillElement.KhongHe) 
        {
            float healAmount = amount * 0.25f; // Tỉ lệ 4:1
            if (PlayerHP != null && !PlayerHP.IsDead && PlayerHP.CurrentHealth < PlayerHP.MaxHealth)
            {
                PlayerHP.Heal(healAmount);
                Debug.Log($"<color=green>💚 [Empty Vessel] Hút quái Không Hệ. Chuyển {amount} năng lượng thành {healAmount} HP.</color>");
            }
            return;
        }

        if (!ElementalEnergies.ContainsKey(incomingElement))
        {
            ElementalEnergies[incomingElement] = 0f;
        }

        float newEnergy = ElementalEnergies[incomingElement] + amount;

        if (newEnergy > MaxElementalEnergy)
        {
            float overflow = newEnergy - MaxElementalEnergy;
            ElementalEnergies[incomingElement] = MaxElementalEnergy;
            
            float healAmount = overflow * 0.25f; // Tỉ lệ 4:1
            
            if (PlayerHP != null && !PlayerHP.IsDead && PlayerHP.CurrentHealth < PlayerHP.MaxHealth)
            {
                PlayerHP.Heal(healAmount);
                Debug.Log($"<color=green>💚 BÌNH CHỨA {incomingElement} ĐÃ ĐẦY! Năng lượng dư ({overflow}) chuyển hóa thành {healAmount} HP.</color>");
            }
            else
            {
                Debug.Log($"<color=orange>⚡ BÌNH CHỨA {incomingElement} ĐÃ ĐẦY! (HP cũng đã đầy, không thể hồi thêm)</color>");
            }
        }
        else
        {
            ElementalEnergies[incomingElement] = newEnergy;
            Debug.Log($"<color=yellow>⚡ HÚT SỨC MẠNH: Nhận {amount} năng lượng {incomingElement}! (Tiến độ: {ElementalEnergies[incomingElement]}/{MaxElementalEnergy})</color>");
        }
        
        OnElementAbsorbed?.Invoke(incomingElement, ElementalEnergies[incomingElement] / MaxElementalEnergy);
    }

    private IEnumerator PerfectDodgeSlowMotionRoutine()
    {
        Time.timeScale = 0.2f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        yield return new WaitForSecondsRealtime(0.4f);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

    public void CancelSlowMotion()
    {
        if (slowMoCoroutine != null)
        {
            StopCoroutine(slowMoCoroutine);
            slowMoCoroutine = null;
        }
    }
}