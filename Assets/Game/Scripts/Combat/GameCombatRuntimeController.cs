using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 负责 Playing 状态下的战斗运行循环、武器开火、弹丸命中和击杀结算。
/// </summary>
public sealed class GameCombatRuntimeController : MonoBehaviour
{
    private const float MinAttackInterval = 0.1f;

    /// <summary>
    /// 战斗运行循环所需的运行时依赖和回调。
    /// </summary>
    public struct CombatRuntimeContext
    {
        public float DeltaTime;
        public float ElapsedTime;
        public bool Started;
        public GameStateManager StateManager;
        public GamePlayerController Player;
        public WeaponData WeaponData;
        public Transform ActiveRuntimeRoot;
        public GameRuntimeObjectPoolController RuntimeObjectPoolController;
        public GameProjectileCombatController ProjectileCombatController;
        public GameWeaponCombatController WeaponCombatController;
        public GameEnemySpawnController EnemySpawnController;
        public GameRewardDropController RewardDropController;
        public GameFloorProgressController FloorProgressController;
        public GameWeaponFireController WeaponFireController;
        public GameDamageNumberController DamageNumberController;
        public GameRelicRuntimeController RelicRuntimeController;
        public System.Action<float> AddElapsedTime;
        public System.Action<int> AddScore;
        public System.Action AddKill;
        public System.Action AddStageBossSummonKill;
        public System.Action<int> AddGoldCollected;
        public System.Action<EnemyDamagedContext> NotifyEnemyDamaged;
        public System.Action<GameObject> ReleaseObject;
        public System.Action RefreshCombatCanvasByState;
        public System.Action<float> PlayCursorFirePulse;
        public System.Action CompleteFloor;
        public System.Action<GameEnemyController, DamageHitInfo> HandleStageBossKilled;
        public bool UseStageSpawnPlan;
        public bool UseStageBossRuntime;
        public StageData StageData;
        public BossData StageBossData;
        public int StageCombatNode;
        public RoomType StageRoomType;
        public System.Func<Vector2> GetArenaMin;
        public System.Func<Vector2> GetArenaMax;
        public System.Func<Vector3, Vector3> ClampPositionInsideArena;
        public System.Func<IReadOnlyList<IProjectileHitTarget>> GetProjectileHitTargets;
    }

    /// <summary>
    /// 执行一帧战斗运行逻辑。
    /// </summary>
    /// <param name="context">战斗运行上下文。</param>
    public void TickCombat(CombatRuntimeContext context)
    {
        if (!context.Started || context.Player == null || context.StateManager == null)
            return;

        if (context.StateManager.CurrentState != GameStateManager.GameState.Playing)
            return;

        context.AddElapsedTime?.Invoke(context.DeltaTime);
        if (context.FloorProgressController != null)
            context.FloorProgressController.TickFloorTimer(context.DeltaTime);

        TickWeaponFire(context);
        TickSpawn(context);
        TickProjectiles(context);
        context.RefreshCombatCanvasByState?.Invoke();

        if (context.Player.IsDead)
            context.StateManager.EnterGameOver();
        else if (!context.UseStageBossRuntime && context.FloorProgressController != null && context.FloorProgressController.IsCurrentFloorComplete())
            context.CompleteFloor?.Invoke();
    }

    /// <summary>
    /// 初始化当前武器的弹夹和能量状态。
    /// </summary>
    /// <param name="context">战斗运行上下文。</param>
    public void InitializeWeaponRuntimeState(CombatRuntimeContext context)
    {
        if (context.WeaponFireController != null)
            context.WeaponFireController.InitializeWeaponRuntimeState(CreateWeaponFireRuntimeContext(context));
    }

    /// <summary>
    /// 更新武器资源、技能输入、普通开火和持续技能。
    /// </summary>
    /// <param name="context">战斗运行上下文。</param>
    private void TickWeaponFire(CombatRuntimeContext context)
    {
        if (context.WeaponFireController == null)
            return;

        GameWeaponFireController.WeaponFireRuntimeContext weaponFireContext = CreateWeaponFireRuntimeContext(context);
        context.WeaponFireController.TickWeaponResources(weaponFireContext);
        context.WeaponFireController.TickSkillInput(weaponFireContext);
        context.WeaponFireController.TickNormalFire(weaponFireContext);
        context.WeaponFireController.TickMajorSkill(weaponFireContext);
    }

    /// <summary>
    /// 处理怪物刷新计时。
    /// </summary>
    /// <param name="context">战斗运行上下文。</param>
    private void TickSpawn(CombatRuntimeContext context)
    {
        if (context.EnemySpawnController == null)
            return;

        context.EnemySpawnController.TickSpawn(CreateEnemySpawnRuntimeContext(context));
    }

    /// <summary>
    /// 更新投射物命中和生命周期。
    /// </summary>
    /// <param name="context">战斗运行上下文。</param>
    private void TickProjectiles(CombatRuntimeContext context)
    {
        if (context.ProjectileCombatController == null)
            return;

        context.ProjectileCombatController.TickProjectiles(CreateProjectileRuntimeContext(context));
    }

    /// <summary>
    /// 让所有武器实例各自从枪口发射一组投射物。
    /// </summary>
    /// <param name="projectileCount">每把武器的弹道数量。</param>
    /// <param name="spreadAngle">左右总散射角。</param>
    /// <param name="useAccuracy">是否叠加普通射击准度偏差。</param>
    /// <param name="projectileContext">投射物参数上下文。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    private void FireProjectileVolleyFromWeaponInstances(
        int projectileCount,
        float spreadAngle,
        bool useAccuracy,
        GameProjectileCombatController.ProjectileFireContext projectileContext,
        CombatRuntimeContext runtimeContext)
    {
        if (runtimeContext.WeaponCombatController == null)
        {
            FireProjectileVolley(GetMouseFireDirection(runtimeContext), projectileCount, spreadAngle, useAccuracy, projectileContext, runtimeContext);
            return;
        }

        bool fired = false;
        runtimeContext.WeaponCombatController.ForEachWeaponInstance(weaponInstance =>
        {
            fired = true;
            FireProjectileVolley(
                weaponInstance.FireDirection,
                weaponInstance.MuzzlePosition,
                projectileCount,
                spreadAngle,
                useAccuracy,
                projectileContext,
                runtimeContext);
        });

        if (!fired)
            FireProjectileVolley(GetMouseFireDirection(runtimeContext), projectileCount, spreadAngle, useAccuracy, projectileContext, runtimeContext);
    }

    /// <summary>
    /// 按指定弹道数和散射角发射一组投射物。
    /// </summary>
    /// <param name="direction">中心发射方向。</param>
    /// <param name="projectileCount">弹道数量。</param>
    /// <param name="spreadAngle">左右总散射角。</param>
    /// <param name="useAccuracy">是否叠加普通射击准度偏差。</param>
    /// <param name="projectileContext">投射物参数上下文。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    private void FireProjectileVolley(
        Vector3 direction,
        int projectileCount,
        float spreadAngle,
        bool useAccuracy,
        GameProjectileCombatController.ProjectileFireContext projectileContext,
        CombatRuntimeContext runtimeContext)
    {
        FireProjectileVolley(
            direction,
            GetDefaultMuzzlePosition(direction, runtimeContext),
            projectileCount,
            spreadAngle,
            useAccuracy,
            projectileContext,
            runtimeContext);
    }

    /// <summary>
    /// 从指定枪口位置按弹道数和散射角发射一组投射物。
    /// </summary>
    /// <param name="direction">中心发射方向。</param>
    /// <param name="origin">枪口世界坐标。</param>
    /// <param name="projectileCount">弹道数量。</param>
    /// <param name="spreadAngle">左右总散射角。</param>
    /// <param name="useAccuracy">是否叠加普通射击准度偏差。</param>
    /// <param name="projectileContext">投射物参数上下文。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    private void FireProjectileVolley(
        Vector3 direction,
        Vector3 origin,
        int projectileCount,
        float spreadAngle,
        bool useAccuracy,
        GameProjectileCombatController.ProjectileFireContext projectileContext,
        CombatRuntimeContext runtimeContext)
    {
        int safeProjectileCount = Mathf.Max(1, projectileCount);
        float spreadStep = safeProjectileCount > 1 ? spreadAngle / (safeProjectileCount - 1) : 0f;
        float startAngle = -spreadAngle * 0.5f;
        for (int i = 0; i < safeProjectileCount; i++)
        {
            float accuracyOffset = useAccuracy
                ? Random.Range(-GetCurrentAccuracyAngle(runtimeContext), GetCurrentAccuracyAngle(runtimeContext))
                : 0f;
            float angle = safeProjectileCount > 1 ? startAngle + spreadStep * i : 0f;
            Vector3 projectileDirection = Quaternion.Euler(0f, 0f, angle + accuracyOffset) * direction;
            FireProjectile(projectileDirection.normalized, origin, projectileContext, runtimeContext);
        }
    }

    /// <summary>
    /// 在指定位置创建投射物。
    /// </summary>
    /// <param name="direction">发射方向。</param>
    /// <param name="position">发射位置。</param>
    /// <param name="projectileContext">投射物参数上下文。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    private GameProjectile FireProjectile(
        Vector3 direction,
        Vector3 position,
        GameProjectileCombatController.ProjectileFireContext projectileContext,
        CombatRuntimeContext runtimeContext)
    {
        if (runtimeContext.ProjectileCombatController == null)
            return null;

        return runtimeContext.ProjectileCombatController.FireProjectile(
            direction,
            position,
            projectileContext,
            CreateProjectileRuntimeContext(runtimeContext));
    }

    /// <summary>
    /// 对指定怪物应用伤害并触发跳字和死亡处理。
    /// </summary>
    /// <param name="enemyIndex">怪物列表索引。</param>
    /// <param name="enemy">怪物组件。</param>
    /// <param name="hitInfo">伤害命中数据。</param>
    /// <param name="hitPosition">受击世界坐标。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    private void ApplyDamageToEnemy(
        int enemyIndex,
        GameEnemyController enemy,
        DamageHitInfo hitInfo,
        Vector3 hitPosition,
        GameProjectile sourceProjectile,
        CombatRuntimeContext runtimeContext)
    {
        if (enemy == null || enemy.IsDead || hitInfo.Amount <= 0)
            return;

        int previousHealth = enemy.CurrentHealth;
        enemy.TakeDamage(hitInfo.Amount);
        int actualDamage = Mathf.Max(0, previousHealth - enemy.CurrentHealth);
        bool killed = enemy.IsDead;
        ShowDamageNumber(hitPosition, hitInfo, runtimeContext);
        runtimeContext.NotifyEnemyDamaged?.Invoke(new EnemyDamagedContext(
            enemy,
            hitPosition,
            hitInfo,
            actualDamage,
            killed,
            sourceProjectile));

        if (enemy.IsDead)
            KillEnemy(enemyIndex, enemy, hitInfo, runtimeContext);
    }

    /// <summary>
    /// 显示一次伤害跳字。
    /// </summary>
    /// <param name="position">受击世界坐标。</param>
    /// <param name="hitInfo">伤害命中数据。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    private void ShowDamageNumber(Vector3 position, DamageHitInfo hitInfo, CombatRuntimeContext runtimeContext)
    {
        if (runtimeContext.DamageNumberController == null)
            return;

        runtimeContext.DamageNumberController.SpawnDamageNumber(runtimeContext.ActiveRuntimeRoot, position, hitInfo);
    }

    /// <summary>
    /// 处理怪物死亡、掉落和统计。
    /// </summary>
    /// <param name="enemyIndex">怪物索引。</param>
    /// <param name="enemy">怪物组件。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    private void KillEnemy(int enemyIndex, GameEnemyController enemy, DamageHitInfo hitInfo, CombatRuntimeContext runtimeContext)
    {
        if (enemy != null && enemy.IsStageBoss)
        {
            runtimeContext.HandleStageBossKilled?.Invoke(enemy, hitInfo);
            return;
        }

        Vector3 position = enemy.transform.position;
        int experience = enemy.DropExperience;
        int baseGold = enemy.DropGold;
        runtimeContext.AddScore?.Invoke(enemy.Score);
        runtimeContext.AddKill?.Invoke();
        if (enemy.IsStageBossSummon)
            runtimeContext.AddStageBossSummonKill?.Invoke();
        if (hitInfo.CanTriggerKillEffects && runtimeContext.WeaponFireController != null)
            runtimeContext.WeaponFireController.AddEnergyPerKill(CreateWeaponFireRuntimeContext(runtimeContext));

        if (runtimeContext.EnemySpawnController != null)
            runtimeContext.EnemySpawnController.ReleaseEnemyAt(enemyIndex, enemy, runtimeContext.ReleaseObject);

        if (runtimeContext.RewardDropController != null)
        {
            int gold = runtimeContext.RewardDropController.SpawnEnemyDrops(
                runtimeContext.Player,
                runtimeContext.ActiveRuntimeRoot,
                runtimeContext.RuntimeObjectPoolController,
                position,
                experience,
                baseGold);
            runtimeContext.AddGoldCollected?.Invoke(gold);
        }
    }

    /// <summary>
    /// 创建投射物系统运行时依赖上下文。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>投射物运行时依赖上下文。</returns>
    private GameProjectileCombatController.ProjectileRuntimeContext CreateProjectileRuntimeContext(CombatRuntimeContext runtimeContext)
    {
        return new GameProjectileCombatController.ProjectileRuntimeContext
        {
            RuntimeObjectPoolController = runtimeContext.RuntimeObjectPoolController,
            ActiveRuntimeRoot = runtimeContext.ActiveRuntimeRoot,
            ProjectilePrefab = runtimeContext.WeaponData != null ? runtimeContext.WeaponData.ProjectilePrefab : null,
            ProjectileSprite = runtimeContext.WeaponData != null ? runtimeContext.WeaponData.ProjectileSprite : null,
            ProjectileHitEffectPrefab = runtimeContext.WeaponData != null ? runtimeContext.WeaponData.ProjectileHitEffectPrefab : null,
            ProjectileExpireEffectPrefab = runtimeContext.WeaponData != null ? runtimeContext.WeaponData.ProjectileExpireEffectPrefab : null,
            PlayerTransform = runtimeContext.Player != null ? runtimeContext.Player.transform : null,
            Enemies = runtimeContext.EnemySpawnController != null ? runtimeContext.EnemySpawnController.Enemies : null,
            GetProjectileHitTargets = runtimeContext.GetProjectileHitTargets,
            CreateProjectileDamageHitInfo = context => CreateProjectileDamageHitInfo(context, runtimeContext),
            GetProjectileSpeed = () => GetCurrentProjectileSpeed(runtimeContext),
            GetProjectileScaleMultiplier = () => runtimeContext.Player != null ? runtimeContext.Player.ProjectileScaleMultiplier : 1f,
            GetProjectileLifeTime = () => GetCurrentProjectileLifeTime(runtimeContext),
            GetImpactDistance = () => GetCurrentImpactDistance(runtimeContext),
            GetAttackDamage = () => GetCurrentAttackDamage(runtimeContext),
            GetArenaMin = runtimeContext.GetArenaMin,
            GetArenaMax = runtimeContext.GetArenaMax,
            CreateProjectileHitEffectSnapshot = () => runtimeContext.RelicRuntimeController != null
                ? runtimeContext.RelicRuntimeController.CreateProjectileHitEffectSnapshot()
                : null,
            CreateProjectileExpireEffectSnapshot = () => runtimeContext.RelicRuntimeController != null
                ? runtimeContext.RelicRuntimeController.CreateProjectileExpireEffectSnapshot()
                : null,
            CreateChildProjectileContext = projectile => runtimeContext.ProjectileCombatController != null
                ? runtimeContext.ProjectileCombatController.CreateChildProjectileContext(projectile)
                : default,
            SpawnChildProjectile = (direction, position, context, sourceProjectile) => runtimeContext.ProjectileCombatController != null
                ? runtimeContext.ProjectileCombatController.FireChildProjectile(direction, position, context, sourceProjectile, CreateProjectileRuntimeContext(runtimeContext))
                : null,
            ReleaseObject = runtimeContext.ReleaseObject,
            ApplyDamageToEnemy = (index, enemy, hitInfo, position, sourceProjectile) => ApplyDamageToEnemy(index, enemy, hitInfo, position, sourceProjectile, runtimeContext),
            ApplyImpact = (enemy, direction, distance) => ApplyImpact(enemy, direction, distance, runtimeContext)
        };
    }

    /// <summary>
    /// 创建敌人生成系统运行时依赖上下文。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>敌人生成运行时依赖上下文。</returns>
    private GameEnemySpawnController.EnemySpawnRuntimeContext CreateEnemySpawnRuntimeContext(CombatRuntimeContext runtimeContext)
    {
        return new GameEnemySpawnController.EnemySpawnRuntimeContext
        {
            RuntimeObjectPoolController = runtimeContext.RuntimeObjectPoolController,
            ActiveRuntimeRoot = runtimeContext.ActiveRuntimeRoot,
            Player = runtimeContext.Player,
            CurrentFloorData = runtimeContext.FloorProgressController != null ? runtimeContext.FloorProgressController.CurrentFloorData : null,
            ElapsedTime = runtimeContext.ElapsedTime,
            GetArenaMin = runtimeContext.GetArenaMin,
            GetArenaMax = runtimeContext.GetArenaMax,
            ClampPositionInsideArena = runtimeContext.ClampPositionInsideArena,
            UseStageSpawnPlan = runtimeContext.UseStageSpawnPlan,
            UseStageBossRuntime = runtimeContext.UseStageBossRuntime,
            StageData = runtimeContext.StageData,
            StageBossData = runtimeContext.StageBossData,
            StageCombatNode = runtimeContext.StageCombatNode,
            StageRoomType = runtimeContext.StageRoomType,
            CompleteStageCombatRoom = runtimeContext.CompleteFloor
        };
    }

    /// <summary>
    /// 创建武器开火系统运行时依赖上下文。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>武器开火运行时依赖上下文。</returns>
    private GameWeaponFireController.WeaponFireRuntimeContext CreateWeaponFireRuntimeContext(CombatRuntimeContext runtimeContext)
    {
        return new GameWeaponFireController.WeaponFireRuntimeContext
        {
            Player = runtimeContext.Player,
            WeaponData = runtimeContext.WeaponData,
            CreateNormalProjectileContext = () => CreateNormalProjectileContext(runtimeContext),
            CreateSkillProjectileContext = (damageMultiplier, speedMultiplier, lifeTimeOverride, pierce, bounce, recoilMultiplier, impactMultiplier) =>
                CreateSkillProjectileContext(damageMultiplier, speedMultiplier, lifeTimeOverride, pierce, bounce, recoilMultiplier, impactMultiplier, runtimeContext),
            FireProjectileVolleyFromWeaponInstances = (projectileCount, spreadAngle, useAccuracy, projectileContext) =>
                FireProjectileVolleyFromWeaponInstances(projectileCount, spreadAngle, useAccuracy, projectileContext, runtimeContext),
            HasRecoverableNails = () => runtimeContext.ProjectileCombatController != null && runtimeContext.ProjectileCombatController.HasRecoverableNails,
            RecallRecoverableNails = () =>
            {
                if (runtimeContext.ProjectileCombatController != null)
                    runtimeContext.ProjectileCombatController.RecallRecoverableNails();
            },
            GetMouseFireDirection = () => GetMouseFireDirection(runtimeContext),
            ApplyRecoil = (direction, distance) => ApplyRecoil(direction, distance, runtimeContext),
            PlayCursorFirePulse = runtimeContext.PlayCursorFirePulse
        };
    }

    /// <summary>
    /// 根据投射物上下文创建伤害数据，命中分裂子弹不重复进行暴击判定。
    /// </summary>
    /// <param name="context">投射物参数上下文。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>投射物携带的伤害数据。</returns>
    private DamageHitInfo CreateProjectileDamageHitInfo(
        GameProjectileCombatController.ProjectileFireContext context,
        CombatRuntimeContext runtimeContext)
    {
        if (context.UseScaledDamage)
            return CreateScaledDamageHitInfo(context.DamageMultiplier, runtimeContext);

        return CreateNormalDamageHitInfo(context.DamageMultiplier, runtimeContext);
    }

    /// <summary>
    /// 创建一次普通伤害命中数据，包含暴击判定和伤害倍率。
    /// </summary>
    /// <param name="damageMultiplier">本次发射的额外伤害倍率。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>普通伤害命中数据。</returns>
    private DamageHitInfo CreateNormalDamageHitInfo(float damageMultiplier, CombatRuntimeContext runtimeContext)
    {
        bool isCritical = Random.value < GetCurrentCriticalRate(runtimeContext);
        int damage = GetCurrentAttackDamage(runtimeContext);
        int scaledDamage = Mathf.Max(1, Mathf.RoundToInt(damage * Mathf.Max(0.01f, damageMultiplier)));
        int finalDamage = isCritical ? Mathf.RoundToInt(scaledDamage * GetCurrentCriticalDamage(runtimeContext)) : scaledDamage;
        return new DamageHitInfo(
            finalDamage,
            DamageSourceCategory.ProjectilePrimary,
            DamageElementType.Normal,
            isCritical);
    }

    /// <summary>
    /// 创建不参与暴击的倍率伤害命中数据。
    /// </summary>
    /// <param name="damageMultiplier">最终攻击力倍率。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>普通伤害命中数据。</returns>
    private DamageHitInfo CreateScaledDamageHitInfo(float damageMultiplier, CombatRuntimeContext runtimeContext)
    {
        int damage = Mathf.Max(1, Mathf.RoundToInt(GetCurrentAttackDamage(runtimeContext) * Mathf.Max(0.01f, damageMultiplier)));
        return new DamageHitInfo(
            damage,
            DamageSourceCategory.ProjectilePrimary,
            DamageElementType.Normal,
            false);
    }

    /// <summary>
    /// 创建普通攻击投射物参数上下文。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>普通攻击投射物参数。</returns>
    private GameProjectileCombatController.ProjectileFireContext CreateNormalProjectileContext(CombatRuntimeContext runtimeContext)
    {
        if (runtimeContext.ProjectileCombatController != null)
        {
            return runtimeContext.ProjectileCombatController.CreateNormalProjectileContext(
                GetCurrentProjectilePierce(runtimeContext),
                GetCurrentProjectileBounce(runtimeContext));
        }

        return CreateSkillProjectileContext(1f, 1f, -1f, GetCurrentProjectilePierce(runtimeContext), GetCurrentProjectileBounce(runtimeContext), 1f, 1f, runtimeContext);
    }

    /// <summary>
    /// 创建技能投射物参数上下文。
    /// </summary>
    /// <param name="damageMultiplier">伤害倍率。</param>
    /// <param name="speedMultiplier">弹速倍率。</param>
    /// <param name="lifeTimeOverride">持续时间覆盖。</param>
    /// <param name="pierce">穿透层数。</param>
    /// <param name="bounce">反弹次数。</param>
    /// <param name="recoilMultiplier">后坐力倍率。</param>
    /// <param name="impactMultiplier">冲击力倍率。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>投射物参数上下文。</returns>
    private GameProjectileCombatController.ProjectileFireContext CreateSkillProjectileContext(
        float damageMultiplier,
        float speedMultiplier,
        float lifeTimeOverride,
        int pierce,
        int bounce,
        float recoilMultiplier,
        float impactMultiplier,
        CombatRuntimeContext runtimeContext)
    {
        if (runtimeContext.ProjectileCombatController != null)
        {
            return runtimeContext.ProjectileCombatController.CreateSkillProjectileContext(
                damageMultiplier,
                speedMultiplier,
                lifeTimeOverride,
                pierce,
                bounce,
                recoilMultiplier,
                impactMultiplier);
        }

        return new GameProjectileCombatController.ProjectileFireContext();
    }

    /// <summary>
    /// 获取当前鼠标方向作为发射方向。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>鼠标方向。</returns>
    private Vector3 GetMouseFireDirection(CombatRuntimeContext runtimeContext)
    {
        Camera camera = Camera.main;
        if (camera == null || runtimeContext.Player == null)
            return Vector3.right;

        Vector3 mouseWorld = camera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = runtimeContext.Player.transform.position.z;
        Vector3 direction = mouseWorld - runtimeContext.Player.transform.position;
        return direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.right;
    }

    /// <summary>
    /// 获取旧发射入口使用的默认枪口位置。
    /// </summary>
    /// <param name="direction">发射方向。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>枪口位置。</returns>
    private Vector3 GetDefaultMuzzlePosition(Vector3 direction, CombatRuntimeContext runtimeContext)
    {
        if (runtimeContext.WeaponCombatController != null)
            return runtimeContext.WeaponCombatController.GetDefaultMuzzlePosition(direction);

        return runtimeContext.Player.transform.position + direction.normalized;
    }

    /// <summary>
    /// 根据射击方向应用后坐力。
    /// </summary>
    /// <param name="direction">发射方向。</param>
    /// <param name="distance">反推距离。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    private void ApplyRecoil(Vector3 direction, float distance, CombatRuntimeContext runtimeContext)
    {
        if (distance <= 0f || runtimeContext.Player == null || runtimeContext.GetArenaMin == null || runtimeContext.GetArenaMax == null)
            return;

        Vector3 nextPosition = runtimeContext.Player.transform.position - direction.normalized * distance;
        nextPosition.x = Mathf.Clamp(nextPosition.x, runtimeContext.GetArenaMin.Invoke().x, runtimeContext.GetArenaMax.Invoke().x);
        nextPosition.y = Mathf.Clamp(nextPosition.y, runtimeContext.GetArenaMin.Invoke().y, runtimeContext.GetArenaMax.Invoke().y);
        runtimeContext.Player.transform.position = nextPosition;
    }

    /// <summary>
    /// 根据命中方向击退敌人。
    /// </summary>
    /// <param name="enemy">被击退敌人。</param>
    /// <param name="direction">击退方向。</param>
    /// <param name="distance">击退距离。</param>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    private void ApplyImpact(GameEnemyController enemy, Vector3 direction, float distance, CombatRuntimeContext runtimeContext)
    {
        if (enemy == null || distance <= 0f || runtimeContext.GetArenaMin == null || runtimeContext.GetArenaMax == null)
            return;

        Vector3 nextPosition = enemy.transform.position + direction.normalized * distance;
        nextPosition.x = Mathf.Clamp(nextPosition.x, runtimeContext.GetArenaMin.Invoke().x, runtimeContext.GetArenaMax.Invoke().x);
        nextPosition.y = Mathf.Clamp(nextPosition.y, runtimeContext.GetArenaMin.Invoke().y, runtimeContext.GetArenaMax.Invoke().y);
        enemy.transform.position = nextPosition;
    }

    /// <summary>
    /// 获取当前最终基础伤害。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>最终基础伤害。</returns>
    private int GetCurrentAttackDamage(CombatRuntimeContext runtimeContext)
    {
        if (runtimeContext.Player == null)
            return 1;

        if (runtimeContext.WeaponData != null)
        {
            float multiplier = Mathf.Max(0.01f, runtimeContext.Player.AttackDamageMultiplier);
            int baseDamage = runtimeContext.Player.AttackDamage + runtimeContext.WeaponData.BaseDamage + runtimeContext.Player.AttackDamageBonus;
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
        }

        return runtimeContext.Player.AttackDamage;
    }

    /// <summary>
    /// 获取当前武器或玩家提供的攻击间隔。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>攻击间隔。</returns>
    private float GetCurrentAttackInterval(CombatRuntimeContext runtimeContext)
    {
        if (runtimeContext.Player == null)
            return MinAttackInterval;

        if (runtimeContext.WeaponData != null)
            return Mathf.Max(MinAttackInterval, runtimeContext.WeaponData.AttackInterval / runtimeContext.Player.AttackSpeedMultiplier);

        return MinAttackInterval;
    }

    /// <summary>
    /// 获取当前武器的投射物速度。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>投射物速度。</returns>
    private float GetCurrentProjectileSpeed(CombatRuntimeContext runtimeContext)
    {
        if (runtimeContext.Player == null)
            return 9f;

        if (runtimeContext.WeaponData != null)
            return Mathf.Max(0.1f, runtimeContext.WeaponData.ProjectileSpeed * runtimeContext.Player.ProjectileSpeedMultiplier);

        return 9f;
    }

    /// <summary>
    /// 获取当前武器的投射物存在时间。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>投射物存在时间。</returns>
    private float GetCurrentProjectileLifeTime(CombatRuntimeContext runtimeContext)
    {
        if (runtimeContext.Player == null)
            return 2.2f;

        if (runtimeContext.WeaponData != null)
            return Mathf.Max(0.1f, runtimeContext.WeaponData.ProjectileLifeTime * runtimeContext.Player.ProjectileLifeTimeMultiplier);

        return 2.2f;
    }

    /// <summary>
    /// 获取当前暴击率。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>暴击率。</returns>
    private float GetCurrentCriticalRate(CombatRuntimeContext runtimeContext)
    {
        if (runtimeContext.Player == null)
            return 0f;

        float relicBonus = runtimeContext.RelicRuntimeController != null
            ? runtimeContext.RelicRuntimeController.GetCriticalRateBonus()
            : 0f;
        return Mathf.Clamp01(runtimeContext.Player.CriticalRate + relicBonus);
    }

    /// <summary>
    /// 获取当前暴击伤害倍率。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>暴击伤害倍率。</returns>
    private float GetCurrentCriticalDamage(CombatRuntimeContext runtimeContext)
    {
        return runtimeContext.Player != null ? Mathf.Max(1f, runtimeContext.Player.CriticalDamage) : 1f;
    }

    /// <summary>
    /// 获取当前子弹穿透层数。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>子弹穿透层数。</returns>
    private int GetCurrentProjectilePierce(CombatRuntimeContext runtimeContext)
    {
        return runtimeContext.Player != null ? Mathf.Max(0, runtimeContext.Player.ProjectilePierce) : 0;
    }

    /// <summary>
    /// 获取当前子弹反弹层数。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>子弹反弹层数。</returns>
    private int GetCurrentProjectileBounce(CombatRuntimeContext runtimeContext)
    {
        return runtimeContext.Player != null ? Mathf.Max(0, runtimeContext.Player.ProjectileBounce) : 0;
    }

    /// <summary>
    /// 获取当前射击准度偏差角。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>偏差角。</returns>
    private float GetCurrentAccuracyAngle(CombatRuntimeContext runtimeContext)
    {
        return runtimeContext.WeaponFireController != null
            ? runtimeContext.WeaponFireController.GetCurrentAccuracyAngle(CreateWeaponFireRuntimeContext(runtimeContext))
            : 0f;
    }

    /// <summary>
    /// 获取当前冲击距离。
    /// </summary>
    /// <param name="runtimeContext">战斗运行上下文。</param>
    /// <returns>冲击距离。</returns>
    private float GetCurrentImpactDistance(CombatRuntimeContext runtimeContext)
    {
        return runtimeContext.WeaponFireController != null
            ? runtimeContext.WeaponFireController.GetCurrentImpactDistance(CreateWeaponFireRuntimeContext(runtimeContext))
            : 0f;
    }
}
