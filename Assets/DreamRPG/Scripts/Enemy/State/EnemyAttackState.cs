using UnityEngine;

public class EnemyAttackState : EnemyState
{
    private enum AttackPhase { Telegraph, Active, Recovery }
    private AttackPhase currentPhase;
    public bool IsInRecovery => currentPhase == AttackPhase.Recovery;
    private float phaseTimer;
    private int   attackVariant;   
    private bool  tokenReleased;
    private bool  hasDealtDamage;
    private int   forcedVariant = -1;

    private int currentComboHit;    
    private int maxComboHits;       

    private float telegraphDuration;   
    private float commitmentTime;      

    public AttackGlint currentGlint;
    public AttackGlint CurrentGlint => currentGlint;

    // 🟢 THÊM BIẾN LƯU TRỮ VFX ĐỂ BÁM THEO QUÁI
    private GameObject activeGlintVFX;

    public EnemyAttackState(EnemyStateMachine stateMachine) : base(stateMachine) { }

    public void Setup(int forcedVariant = -1)
    {
        this.forcedVariant = forcedVariant;
    }

    public override void Enter()
    {
        stateMachine.ManualVelocity = Vector3.zero;
        tokenReleased = false;
        hasDealtDamage = false;
        currentComboHit = 0; 
        activeGlintVFX = null; // Reset biến VFX

        if (forcedVariant != -1) maxComboHits = 1;
        else
        {
            if (stateMachine.Stats.isMiniBoss)
            {
                // 🟢 GOD OF WAR COMBO: Boss đánh theo nhịp cố định
                maxComboHits = stateMachine.IsPhase2 ? 4 : 3;
            }
            else
            {
                int minHits = stateMachine.Stats.comboHitsMin > 0 ? stateMachine.Stats.comboHitsMin : 1;
                int maxHits = stateMachine.Stats.comboHitsMax > 0 ? stateMachine.Stats.comboHitsMax : 3;
                maxComboHits = Random.Range(minHits, maxHits + 1);
            }
        }

        if (stateMachine.Agent.isActiveAndEnabled && stateMachine.Agent.isOnNavMesh)
        {
            stateMachine.Agent.ResetPath();
            stateMachine.Agent.isStopped = true; 
        }

        StartNewSwing();
    }

    private void StartNewSwing()
    {
        hasDealtDamage = false;
        currentPhase = AttackPhase.Telegraph;
        commitmentTime = 0.2f;

        if (forcedVariant != -1)
        {
            attackVariant = forcedVariant;
            currentGlint = AttackGlint.Normal;
        }
        else
        {
            int variantCount = stateMachine.Stats.attackVariants;

            // 🟢 Thay đổi 1: Bắt buộc đánh theo Sequence (Tuần tự) thay vì Random
            // Nếu có 2 đòn: 0 -> 1 -> 0 -> 1. Nếu có 3 đòn: 0 -> 1 -> 2.
            if (variantCount > 0)
            {
                attackVariant = currentComboHit % variantCount;
            }
            else
            {
                attackVariant = 0;
            }

            float guardDuration = stateMachine.PlayerGuardDuration;
            
            float redChance = (currentComboHit == maxComboHits - 1) 
                ? stateMachine.Stats.unblockableFinisherChance 
                : stateMachine.Stats.unblockableChance;
            
            float blueChance = stateMachine.Stats.heavyBlueChance;

            if (guardDuration >= stateMachine.Stats.guardPunishThreshold)
            {
                if (Random.Range(0f, 100f) < stateMachine.Stats.guardPunishChance)
                {
                    redChance = 80f; 
                }
            }

            float roll = Random.Range(0f, 100f);
            if (roll < redChance) currentGlint = AttackGlint.Red;
            else if (roll < redChance + blueChance) currentGlint = AttackGlint.Blue;
            else currentGlint = AttackGlint.Normal;
        }

        SpawnGlintVFX(currentGlint);
        
        telegraphDuration = (forcedVariant != -1) ? 0.15f : (0.4f + attackVariant * 0.1f);

        if (stateMachine.IsPlayerAttacking && forcedVariant == -1)
        {
            if (Random.Range(0f, 100f) < stateMachine.Stats.counterAttackChance)
            {
                telegraphDuration *= 0.7f;
            }
        }
        else if (forcedVariant == -1)
        {
            if (Random.Range(0f, 100f) < stateMachine.Stats.rhythmBreakChance)
            {
                float delay = Random.Range(stateMachine.Stats.delayedSwingMin, stateMachine.Stats.delayedSwingMax);
                telegraphDuration += delay;
            }
        }

        phaseTimer = telegraphDuration;
        stateMachine.Anim.SetInteger(EnemyConstants.HashAttackVariant, attackVariant);
        stateMachine.Anim.SetTrigger(EnemyConstants.HashAttack);
    }

    private void SpawnGlintVFX(AttackGlint glint)
    {
        GameObject prefabToSpawn = null;
        if (glint == AttackGlint.Red) prefabToSpawn = stateMachine.Stats.UnblockableWarningVFX;
        else if (glint == AttackGlint.Blue) prefabToSpawn = stateMachine.Stats.BlueWarningVFX;

        if (prefabToSpawn != null)
        {
            float enemyHeight = stateMachine.Controller != null ? stateMachine.Controller.height : 2f;
            Vector3 spawnPos = stateMachine.transform.position + Vector3.up * (enemyHeight + 0.5f);

            // 🟢 Sinh ra và lưu vào biến để hàm Tick() cập nhật vị trí
            activeGlintVFX = GameObject.Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

            // Tự hủy sau 1.2 giây
            GameObject.Destroy(activeGlintVFX, 1.2f); 
        }
    }

    private float GetCurrentComboRange()
    {
        if (stateMachine.Stats.comboHitboxRanges != null && attackVariant < stateMachine.Stats.comboHitboxRanges.Length)
            return stateMachine.Stats.comboHitboxRanges[attackVariant];
        return stateMachine.Stats.attackRange;
    }

    public override void Tick(float deltaTime)
    {
        // =========================================================
        // 🟢 CẬP NHẬT VỊ TRÍ ĐÈN GIAO THÔNG LIÊN TỤC THEO QUÁI
        // =========================================================
        if (activeGlintVFX != null)
        {
            float enemyHeight = stateMachine.Controller != null ? stateMachine.Controller.height : 2f;
            
            // Ép VFX luôn chạy theo đỉnh đầu quái vật dù nó lướt đi đâu
            activeGlintVFX.transform.position = stateMachine.transform.position + Vector3.up * (enemyHeight + 0.5f);

            // Ép VFX xoay mặt vào Camera
            if (Camera.main != null)
            {
                activeGlintVFX.transform.rotation = Quaternion.LookRotation(activeGlintVFX.transform.position - Camera.main.transform.position);
            }
        }

        if (stateMachine.PlayerTarget == null) return;
        phaseTimer -= deltaTime;

        switch (currentPhase)
        {
            case AttackPhase.Telegraph: HandleTelegraphPhase(deltaTime); break;
            case AttackPhase.Active: HandleActivePhase(); break;
            case AttackPhase.Recovery: HandleRecoveryPhase(); break;
        }
    }

    private void HandleTelegraphPhase(float deltaTime)
    {
        float distToPlayer = Vector3.Distance(stateMachine.transform.position, stateMachine.PlayerTarget.position) - stateMachine.Controller.radius;

        if (phaseTimer > commitmentTime)
        {
            TrackPlayer(deltaTime); 

            if (distToPlayer > stateMachine.Stats.attackRange * 0.6f)
            {
                float lungeSpeed = stateMachine.Stats.moveSpeed * 2f * stateMachine.Stats.lungeForceMultiplier;
                if (currentGlint != AttackGlint.Normal) lungeSpeed *= 1.2f; 
                stateMachine.ManualVelocity = stateMachine.transform.forward * lungeSpeed;
                stateMachine.Anim.SetFloat("speed", 1f);
                
                // 🟢 BẬT TÀN ẢNH VÀ TRUYỀN HỆ CỦA QUÁI VÀO (Hệ Lôi)
                var afterimage = stateMachine.GetComponent<AfterimageController>();
                if (afterimage != null) afterimage.StartTrail(stateMachine.Stats.enemyElement);
            }
            else
            {
                stateMachine.ManualVelocity = Vector3.zero;
                stateMachine.Anim.SetFloat("speed", 0f);
            }
        }
        else
        {
            stateMachine.ManualVelocity = Vector3.zero;
            stateMachine.Anim.SetFloat("speed", 0f);
            
            // 🟢 TẮT TÀN ẢNH
            var afterimage = stateMachine.GetComponent<AfterimageController>();
            if (afterimage != null) afterimage.StopTrail();
        }

        if (phaseTimer <= 0)
        {
            currentPhase = AttackPhase.Active;
            // 🟢 NÂNG CẤP: Cho Active Phase tối đa 3 giây để chờ Animation Event kích hoạt damage
            // Tránh việc code tự ép trừ máu người chơi trước khi gươm của Boss chạm đất!
            phaseTimer = 3.0f; 
        }
    }

    private void HandleActivePhase()
    {
        stateMachine.ManualVelocity = Vector3.Lerp(stateMachine.ManualVelocity, Vector3.zero, Time.deltaTime * 5f);
        
        AnimatorStateInfo stateInfo = stateMachine.Anim.GetCurrentAnimatorStateInfo(0);
        bool isAnimFinishing = IsCurrentAnimationPlaying(stateInfo) && stateInfo.normalizedTime >= 0.85f;

        // 🟢 NÂNG CẤP: Chuyển sang Recovery nếu:
        // 1. Đã gọi Event trừ máu
        // 2. Hoặc Animation đã chạy gần xong (85%) (Failsafe cho trường hợp quên gắn Event)
        // 3. Hoặc hết 3 giây (Failsafe tuyệt đối)
        if (hasDealtDamage || isAnimFinishing || phaseTimer <= 0)
        {
            if (!hasDealtDamage) PerformHitboxCheck(); // Failsafe cho quái quên gắn Event
            currentPhase = AttackPhase.Recovery;
            CalculateRecoveryTime();
        }
    }

    private bool IsCurrentAnimationPlaying(AnimatorStateInfo stateInfo)
    {
        int hashAttack0 = Animator.StringToHash("Attack_0");
        int hashAttack1 = Animator.StringToHash("Attack_1");
        int hashAttack2 = Animator.StringToHash("Attack_2");
        int hashAttack3 = Animator.StringToHash("Attack_3");
        int hashAttack = Animator.StringToHash("Attack");
        int hashMeleeAttack = Animator.StringToHash("MeleeAttack");

        return stateInfo.shortNameHash == hashAttack0 ||
               stateInfo.shortNameHash == hashAttack1 ||
               stateInfo.shortNameHash == hashAttack2 ||
               stateInfo.shortNameHash == hashAttack3 ||
               stateInfo.shortNameHash == hashAttack ||
               stateInfo.shortNameHash == hashMeleeAttack ||
               stateInfo.IsName("Attack") || stateInfo.IsName("Attack_0") ||
               stateInfo.IsName("Attack_1") || stateInfo.IsName("Attack_2") ||
               stateInfo.IsName("Attack_3") ||
               stateInfo.IsName("MeleeAttack") || stateInfo.IsName("Combo1") ||
               stateInfo.IsName("Combo2") || stateInfo.IsName("Combo3");
    }

    private void HandleRecoveryPhase()
    {
        stateMachine.ManualVelocity = Vector3.zero;

        if (currentComboHit < maxComboHits - 1)
        {
            stateMachine.FaceTarget(stateMachine.PlayerTarget.position, 3f);
        }

        if (phaseTimer <= 0)
        {
            AnimatorStateInfo stateInfo = stateMachine.Anim.GetCurrentAnimatorStateInfo(0);
            if (IsCurrentAnimationPlaying(stateInfo) && stateInfo.normalizedTime < 0.75f && !stateMachine.Anim.IsInTransition(0))
            {
                return; 
            }

            currentComboHit++; 
            if (currentComboHit < maxComboHits)
            {
                float distToPlayer = Vector3.Distance(stateMachine.transform.position, stateMachine.PlayerTarget.position);
                float maxAllowedRange = stateMachine.Stats.attackRange * 3f;

                if (distToPlayer > maxAllowedRange)
                {
                    SafeReleaseToken();
                    stateMachine.SwitchState(stateMachine.ChaseState);
                }
                else
                {
                    StartNewSwing();
                }
            }
            else
            {
                SafeReleaseToken();
                stateMachine.SwitchState(stateMachine.StrafeState);
            }
        }
    }

    private void CalculateRecoveryTime()
    {
        float penalty = (currentGlint != AttackGlint.Normal) ? 0.5f : 0f;
        if (currentComboHit < maxComboHits - 1) 
        {
            phaseTimer = 0.15f + penalty;
        }
        else 
        {
            // 🟢 Thay đổi 2: Thời gian "Thở dốc" sau khi đánh xong Combo
            float baseRecovery = 1.0f + attackVariant * 0.3f;
            
            // Nếu là MiniBoss đánh đòn cuối, bắt buộc đứng yên thở dốc thêm 1.5 giây
            if (stateMachine.Stats.isMiniBoss)
            {
                baseRecovery += 1.5f; 
            }
            
            phaseTimer = baseRecovery + penalty;
        }
    }

    private void TrackPlayer(float deltaTime)
    {
        Vector3 dirToPlayer = (stateMachine.PlayerTarget.position - stateMachine.transform.position);
        dirToPlayer.y = 0f;
        if (dirToPlayer.sqrMagnitude < 0.01f) return;
        stateMachine.FaceTarget(stateMachine.PlayerTarget.position, 3f);
    }

    private void SafeReleaseToken()
    {
        if (tokenReleased) return;
        tokenReleased = true;
        if (AIDirector.Instance != null) AIDirector.Instance.ReleaseToken(stateMachine);
    }

    public override void Exit()
    {
        var afterimage = stateMachine.GetComponent<AfterimageController>();
        if (afterimage != null) afterimage.StopTrail();
        SafeReleaseToken();
        stateMachine.ManualVelocity = Vector3.zero;

        // 🟢 CỰC KỲ QUAN TRỌNG: Dọn dẹp sạch sẽ chữ cảnh báo
        // Nếu quái bị chém khựng (ngắt đòn) hoặc bị chết, chữ cảnh báo phải biến mất lập tức!
        if (activeGlintVFX != null)
        {
            GameObject.Destroy(activeGlintVFX);
        }
    }

    public void PerformHitboxCheck()
    {
        if (hasDealtDamage) return; 
        hasDealtDamage = true;
        
        float range = GetCurrentComboRange();
        Vector3 boxCenter = stateMachine.transform.position + Vector3.up * 1f + stateMachine.transform.forward * (range * 0.5f);
        Vector3 boxHalfExtents = new Vector3(1.2f, 1f, (range * 0.5f) + 0.5f); 
        
        Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, stateMachine.transform.rotation, EnemyConstants.PlayerLayerMask);

        bool hasHitPlayer = false; // Đảm bảo chỉ trừ máu 1 lần dù trúng nhiều Collider
        foreach (var hit in hits)
        {
            PlayerStateMachine player = hit.GetComponentInParent<PlayerStateMachine>();
            if (player != null && !hasHitPlayer)
            {
                hasHitPlayer = true;
                player.TakeDamage(stateMachine.Stats.attackDamage, stateMachine.transform.position, currentGlint);
            }
        }

        // 🟢 NÂNG CẤP: CHỈ BOSS PHASE 2 MỚI KÍCH HOẠT NỔ SÉT KHI CHÉM
        if (stateMachine.Stats.isMiniBoss && stateMachine.IsPhase2)
        {
            if (stateMachine.Stats.meleeBurstPrefab != null)
            {
                Vector3 spawnPos = stateMachine.skillOriginPoint != null 
                    ? stateMachine.skillOriginPoint.position 
                    : stateMachine.transform.position + Vector3.up;

                GameObject burst = ObjectPoolManager.Instance.SpawnFromPool(
                    stateMachine.Stats.meleeBurstPrefab, spawnPos, Quaternion.identity);

                LightningAOE aoeScript = burst.GetComponent<LightningAOE>();
                if (aoeScript != null)
                {
                    aoeScript.Setup(stateMachine.Stats.attackDamage * 0.8f, stateMachine.Stats.enemyElement, true);
                }
            }
        }
    }
}