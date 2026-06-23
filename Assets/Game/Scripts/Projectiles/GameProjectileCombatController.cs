using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理投射物生成、命中检测、生命周期、分裂和弹丸特性执行。
/// </summary>
public sealed class GameProjectileCombatController : MonoBehaviour
{
    private const string RecoverableNailTag = "RecoverableNailSource";
    private const float DefaultRecoverableNailDamageMultiplier = 0.45f;
    private const int DefaultRecoverableNailMaxCount = 16;
    private const float DefaultRecoverableNailProjectileLifeTimeOverride = -1f;
    private const int DefaultRecoverableNailMaxChildGeneration = 1;
    private const float DefaultRecoverableNailAnchorLifeTime = 6f;
    private const float RecoverableNailRecallSpeed = 18f;
    private const float RecoverableNailHitRadius = 0.42f;
    private const float RecoverableNailArriveDistance = 0.22f;

    /// <summary>
    /// 记录忠诚 III 留在场上的可回收钉子运行时状态。
    /// </summary>
    private sealed class RecoverableNail
    {
        public readonly HashSet<int> HitEnemyIds = new HashSet<int>();
        public GameObject VisualObject;
        public RecoverableNailAnchorView AnchorView;
        public Vector3 Position;
        public float Timer;
        public bool IsRecalling;
        public bool RecallVisualApplied;
    }

    /// <summary>
    /// 投射物发射参数上下文。
    /// </summary>
    public struct ProjectileFireContext
    {
        public float DamageMultiplier;
        public float SpeedMultiplier;
        public float LifeTimeOverride;
        public int Pierce;
        public int Bounce;
        public float RecoilMultiplier;
        public float ImpactMultiplier;
        public bool CanSplitOnHit;
        public bool UseScaledDamage;
        public bool IsRecoverableNailCandidate;
        public ProjectileFireSourceType FireSourceType;
    }

    /// <summary>
    /// 投射物运行时依赖上下文。
    /// </summary>
    public struct ProjectileRuntimeContext
    {
        public GameRuntimeObjectPoolController RuntimeObjectPoolController;
        public Transform ActiveRuntimeRoot;
        public GameProjectile ProjectilePrefab;
        public Sprite ProjectileSprite;
        public GameObject ProjectileHitEffectPrefab;
        public GameObject ProjectileExpireEffectPrefab;
        public Transform PlayerTransform;
        public List<GameEnemyController> Enemies;
        public Func<IReadOnlyList<IProjectileHitTarget>> GetProjectileHitTargets;
        public Func<ProjectileFireContext, DamageHitInfo> CreateProjectileDamageHitInfo;
        public Func<float> GetProjectileSpeed;
        public Func<float> GetProjectileScaleMultiplier;
        public Func<float> GetProjectileLifeTime;
        public Func<float> GetImpactDistance;
        public Func<int> GetAttackDamage;
        public Func<Vector2> GetArenaMin;
        public Func<Vector2> GetArenaMax;
        public Func<IReadOnlyList<IProjectileHitRelicEffect>> CreateProjectileHitEffectSnapshot;
        public Func<IReadOnlyList<IProjectileExpireRelicEffect>> CreateProjectileExpireEffectSnapshot;
        public Func<GameProjectile, ProjectileFireContext> CreateChildProjectileContext;
        public Func<Vector3, Vector3, ProjectileFireContext, GameProjectile, GameProjectile> SpawnChildProjectile;
        public Action<GameObject> ReleaseObject;
        public Action<int, GameEnemyController, DamageHitInfo, Vector3, GameProjectile> ApplyDamageToEnemy;
        public Action<GameEnemyController, Vector3, float> ApplyImpact;
    }

    private readonly List<GameProjectile> _projectiles = new List<GameProjectile>();
    private readonly HashSet<GameProjectile> _hitSplitEnabledProjectiles = new HashSet<GameProjectile>();
    private readonly ProjectileEffectDispatcher _effectDispatcher = new ProjectileEffectDispatcher();
    private readonly List<Action<ProjectileHitEffectContext>> _hitEffectBuffer = new List<Action<ProjectileHitEffectContext>>();
    private readonly List<Action<ProjectileExpireEffectContext>> _expireEffectBuffer = new List<Action<ProjectileExpireEffectContext>>();
    private readonly List<RecoverableNail> _recoverableNails = new List<RecoverableNail>();

    private float _projectileScaleBonus;
    private float _projectileSplitOnHitDamageMultiplier;
    private float _recoverableNailDamageMultiplier = DefaultRecoverableNailDamageMultiplier;
    private float _recoverableNailProjectileLifeTimeOverride = DefaultRecoverableNailProjectileLifeTimeOverride;
    private float _recoverableNailAnchorScale = 1f;
    private float _recoverableNailAnchorLifeTime = DefaultRecoverableNailAnchorLifeTime;
    private int _recoverableNailMaxCount = DefaultRecoverableNailMaxCount;
    private int _recoverableNailMaxChildGeneration = DefaultRecoverableNailMaxChildGeneration;
    private GameObject _recoverableNailAnchorPrefab;
    private Sprite _recoverableNailRecallSprite;
    private RecoverableNailSpawnScope _recoverableNailSpawnScope = RecoverableNailSpawnScope.NormalProjectilesAndChildren;
    private bool _projectileSplitOnHit;
    private bool _recoverableNailRecallEnabled;
    private bool _recoverableNailSpawnOnHitEnemy = true;
    private bool _recoverableNailSpawnOnLifeTimeEnded = true;
    private bool _recoverableNailSpawnOnHitWall = true;
    private bool _recoverableNailMissingPrefabLogged;

    /// <summary>
    /// 当前是否存在尚未进入召回状态的可回收钉子。
    /// </summary>
    public bool HasRecoverableNails
    {
        get
        {
            for (int i = 0; i < _recoverableNails.Count; i++)
            {
                RecoverableNail nail = _recoverableNails[i];
                if (nail != null && !nail.IsRecalling)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 重置投射物强化和特性状态。
    /// </summary>
    public void ResetProjectileState()
    {
        _projectiles.Clear();
        _hitSplitEnabledProjectiles.Clear();
        ClearRecoverableNails();
        _projectileScaleBonus = 0f;
        _projectileSplitOnHit = false;
        _projectileSplitOnHitDamageMultiplier = 0f;
        _recoverableNailDamageMultiplier = DefaultRecoverableNailDamageMultiplier;
        _recoverableNailProjectileLifeTimeOverride = DefaultRecoverableNailProjectileLifeTimeOverride;
        _recoverableNailAnchorScale = 1f;
        _recoverableNailAnchorLifeTime = DefaultRecoverableNailAnchorLifeTime;
        _recoverableNailMaxCount = DefaultRecoverableNailMaxCount;
        _recoverableNailMaxChildGeneration = DefaultRecoverableNailMaxChildGeneration;
        _recoverableNailAnchorPrefab = null;
        _recoverableNailRecallSprite = null;
        _recoverableNailSpawnScope = RecoverableNailSpawnScope.NormalProjectilesAndChildren;
        _recoverableNailRecallEnabled = false;
        _recoverableNailSpawnOnHitEnemy = true;
        _recoverableNailSpawnOnLifeTimeEnded = true;
        _recoverableNailSpawnOnHitWall = true;
        _recoverableNailMissingPrefabLogged = false;
    }

    /// <summary>
    /// 清理当前场景中仍存在的投射物对象。
    /// </summary>
    /// <param name="releaseObject">对象释放回调。</param>
    public void ClearProjectiles(Action<GameObject> releaseObject)
    {
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            if (_projectiles[i] != null && releaseObject != null)
                releaseObject.Invoke(_projectiles[i].gameObject);
        }

        _projectiles.Clear();
        _hitSplitEnabledProjectiles.Clear();
        ClearRecoverableNails();
    }

    /// <summary>
    /// 增加投射物缩放加成。
    /// </summary>
    /// <param name="value">缩放加成。</param>
    public void AddProjectileScaleBonus(float value)
    {
        _projectileScaleBonus += Mathf.Max(0f, value);
    }

    /// <summary>
    /// 开启投射物命中分裂。
    /// </summary>
    /// <param name="damageMultiplier">分裂弹丸伤害倍率。</param>
    public void EnableProjectileSplitOnHit(float damageMultiplier)
    {
        _projectileSplitOnHit = true;
        _projectileSplitOnHitDamageMultiplier = Mathf.Max(0.01f, damageMultiplier);
    }

    /// <summary>
    /// 启用忠诚 III 的可回收钉子和召回伤害规则。
    /// </summary>
    /// <param name="damageMultiplier">召回伤害相对当前攻击力的倍率。</param>
    /// <param name="maxCount">场上最多保留的可回收钉子数量。</param>
    /// <param name="projectileLifeTimeOverride">普通射钉最大飞行时间覆盖值，小于等于 0 时不覆盖。</param>
    /// <param name="anchorPrefab">锚点钉子表现预制体。</param>
    /// <param name="anchorScale">锚点钉子表现缩放。</param>
    /// <param name="anchorLifeTime">锚点钉子自然存在时间，0 表示不会自然消失。</param>
    /// <param name="recallSprite">召回飞行时替换使用的钉子贴图。</param>
    /// <param name="spawnScope">允许生成锚点钉子的投射物来源范围。</param>
    /// <param name="maxChildGeneration">允许生成锚点钉子的最大派生代数。</param>
    /// <param name="spawnOnHitEnemy">命中敌人消失时是否生成锚点。</param>
    /// <param name="spawnOnLifeTimeEnded">飞行时间结束时是否生成锚点。</param>
    /// <param name="spawnOnHitWall">撞到边界时是否生成锚点。</param>
    public void EnableRecoverableNailRecall(
        float damageMultiplier,
        int maxCount,
        float projectileLifeTimeOverride,
        GameObject anchorPrefab,
        float anchorScale,
        float anchorLifeTime,
        Sprite recallSprite,
        RecoverableNailSpawnScope spawnScope,
        int maxChildGeneration,
        bool spawnOnHitEnemy,
        bool spawnOnLifeTimeEnded,
        bool spawnOnHitWall)
    {
        _recoverableNailRecallEnabled = true;
        _recoverableNailDamageMultiplier = damageMultiplier > 0f
            ? damageMultiplier
            : DefaultRecoverableNailDamageMultiplier;
        _recoverableNailMaxCount = maxCount > 0
            ? maxCount
            : DefaultRecoverableNailMaxCount;
        _recoverableNailProjectileLifeTimeOverride = projectileLifeTimeOverride > 0f
            ? projectileLifeTimeOverride
            : DefaultRecoverableNailProjectileLifeTimeOverride;
        _recoverableNailAnchorPrefab = anchorPrefab;
        _recoverableNailAnchorScale = Mathf.Max(0.01f, anchorScale);
        _recoverableNailAnchorLifeTime = Mathf.Max(0f, anchorLifeTime);
        _recoverableNailRecallSprite = recallSprite;
        _recoverableNailSpawnScope = spawnScope;
        _recoverableNailMaxChildGeneration = Mathf.Max(0, maxChildGeneration);
        _recoverableNailSpawnOnHitEnemy = spawnOnHitEnemy;
        _recoverableNailSpawnOnLifeTimeEnded = spawnOnLifeTimeEnded;
        _recoverableNailSpawnOnHitWall = spawnOnHitWall;
        _recoverableNailMissingPrefabLogged = false;
    }

    /// <summary>
    /// 将当前场上尚未召回的钉子切换为飞回玩家的状态。
    /// </summary>
    public void RecallRecoverableNails()
    {
        for (int i = 0; i < _recoverableNails.Count; i++)
        {
            RecoverableNail nail = _recoverableNails[i];
            if (nail == null || nail.IsRecalling)
                continue;

            nail.IsRecalling = true;
            nail.Timer = 0f;
            nail.HitEnemyIds.Clear();
        }
    }

    /// <summary>
    /// 创建普通攻击投射物参数上下文。
    /// </summary>
    /// <param name="pierce">穿透层数。</param>
    /// <param name="bounce">反弹次数。</param>
    /// <returns>普通攻击投射物参数。</returns>
    public ProjectileFireContext CreateNormalProjectileContext(int pierce, int bounce)
    {
        ProjectileFireContext context = CreateSkillProjectileContext(1f, 1f, -1f, pierce, bounce, 1f, 1f);
        context.FireSourceType = ProjectileFireSourceType.NormalAttack;
        context.IsRecoverableNailCandidate = true;
        if (_recoverableNailRecallEnabled && _recoverableNailProjectileLifeTimeOverride > 0f)
            context.LifeTimeOverride = _recoverableNailProjectileLifeTimeOverride;

        return context;
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
    /// <returns>投射物参数上下文。</returns>
    public ProjectileFireContext CreateSkillProjectileContext(
        float damageMultiplier,
        float speedMultiplier,
        float lifeTimeOverride,
        int pierce,
        int bounce,
        float recoilMultiplier,
        float impactMultiplier)
    {
        return new ProjectileFireContext
        {
            DamageMultiplier = Mathf.Max(0.01f, damageMultiplier),
            SpeedMultiplier = Mathf.Max(0.01f, speedMultiplier),
            LifeTimeOverride = lifeTimeOverride,
            Pierce = Mathf.Max(0, pierce),
            Bounce = Mathf.Max(0, bounce),
            RecoilMultiplier = Mathf.Max(0f, recoilMultiplier),
            ImpactMultiplier = Mathf.Max(0f, impactMultiplier),
            CanSplitOnHit = _projectileSplitOnHit,
            UseScaledDamage = false,
            IsRecoverableNailCandidate = true,
            FireSourceType = ProjectileFireSourceType.WeaponSkill
        };
    }

    /// <summary>
    /// 发射一个投射物。
    /// </summary>
    /// <param name="direction">发射方向。</param>
    /// <param name="position">发射位置。</param>
    /// <param name="context">投射物发射参数。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    /// <returns>新投射物。</returns>
    public GameProjectile FireProjectile(
        Vector3 direction,
        Vector3 position,
        ProjectileFireContext context,
        ProjectileRuntimeContext runtimeContext)
    {
        return FireProjectileInternal(direction, position, context, runtimeContext, null);
    }

    /// <summary>
    /// 发射一个继承源投射物上下文的派生投射物。
    /// </summary>
    /// <param name="direction">发射方向。</param>
    /// <param name="position">发射位置。</param>
    /// <param name="context">投射物发射参数。</param>
    /// <param name="sourceProjectile">源投射物。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    /// <returns>新投射物。</returns>
    public GameProjectile FireChildProjectile(
        Vector3 direction,
        Vector3 position,
        ProjectileFireContext context,
        GameProjectile sourceProjectile,
        ProjectileRuntimeContext runtimeContext)
    {
        return FireProjectileInternal(direction, position, context, runtimeContext, sourceProjectile);
    }

    /// <summary>
    /// 创建派生投射物参数上下文。
    /// </summary>
    /// <param name="sourceProjectile">源投射物。</param>
    /// <returns>派生投射物参数。</returns>
    public ProjectileFireContext CreateChildProjectileContext(GameProjectile sourceProjectile)
    {
        ProjectileFireContext context;
        if (sourceProjectile == null)
            context = CreateNormalProjectileContext(0, 0);
        else
            context = CreateNormalProjectileContext(sourceProjectile.RemainingPierce, sourceProjectile.RemainingBounce);

        context.IsRecoverableNailCandidate = true;
        context.FireSourceType = sourceProjectile != null ? sourceProjectile.FireSourceType : ProjectileFireSourceType.NormalAttack;
        return context;
    }

    /// <summary>
    /// 发射投射物的内部实现，可选继承源投射物上下文。
    /// </summary>
    /// <param name="direction">发射方向。</param>
    /// <param name="position">发射位置。</param>
    /// <param name="context">投射物发射参数。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    /// <param name="sourceProjectile">源投射物。</param>
    /// <returns>新投射物。</returns>
    private GameProjectile FireProjectileInternal(
        Vector3 direction,
        Vector3 position,
        ProjectileFireContext context,
        ProjectileRuntimeContext runtimeContext,
        GameProjectile sourceProjectile)
    {
        if (runtimeContext.RuntimeObjectPoolController == null)
            return null;

        float playerScaleMultiplier = runtimeContext.GetProjectileScaleMultiplier != null
            ? runtimeContext.GetProjectileScaleMultiplier.Invoke()
            : 1f;
        float projectileScale = 0.24f * Mathf.Max(0.01f, playerScaleMultiplier) * (1f + Mathf.Max(0f, _projectileScaleBonus));
        GamePooledObject projectileObject = runtimeContext.RuntimeObjectPoolController.Get(
            GameRuntimeObjectPoolController.PoolObjectType.Projectile,
            runtimeContext.ProjectilePrefab != null ? runtimeContext.ProjectilePrefab.gameObject : null,
            runtimeContext.ActiveRuntimeRoot,
            position,
            Vector3.one * projectileScale,
            runtimeContext.RuntimeObjectPoolController.GetDefaultColor(GameRuntimeObjectPoolController.PoolObjectType.Projectile));
        GameProjectile projectile = projectileObject.GetComponent<GameProjectile>();
        if (projectile == null)
            projectile = projectileObject.gameObject.AddComponent<GameProjectile>();

        ApplyProjectileSprite(projectileObject, runtimeContext.ProjectileSprite);
        projectile.Initialize(
            direction,
            runtimeContext.CreateProjectileDamageHitInfo.Invoke(context),
            runtimeContext.GetProjectileSpeed.Invoke() * Mathf.Max(0.01f, context.SpeedMultiplier),
            context.LifeTimeOverride > 0f ? context.LifeTimeOverride : runtimeContext.GetProjectileLifeTime.Invoke(),
            context.Pierce,
            context.Bounce,
            runtimeContext.GetArenaMin.Invoke(),
            runtimeContext.GetArenaMax.Invoke(),
            runtimeContext.GetImpactDistance.Invoke() * Mathf.Max(0f, context.ImpactMultiplier),
            sourceProjectile != null ? sourceProjectile.Generation + 1 : 0,
            sourceProjectile != null ? sourceProjectile.RootTriggerId : string.Empty,
            sourceProjectile != null ? sourceProjectile.FireSourceType : context.FireSourceType);
        if (context.IsRecoverableNailCandidate)
            projectile.AddTag(RecoverableNailTag);

        if (context.CanSplitOnHit)
            _hitSplitEnabledProjectiles.Add(projectile);

        _projectiles.Add(projectile);
        AttachProjectileSnapshots(projectile, sourceProjectile, runtimeContext);
        return projectile;
    }

    /// <summary>
    /// 将武器配置的投射物贴图应用到当前子弹表现。
    /// </summary>
    /// <param name="projectileObject">池化投射物对象。</param>
    /// <param name="projectileSprite">投射物贴图。</param>
    private void ApplyProjectileSprite(GamePooledObject projectileObject, Sprite projectileSprite)
    {
        if (projectileObject == null || projectileSprite == null)
            return;

        SpriteRenderer spriteRenderer = projectileObject.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
            spriteRenderer.sprite = projectileSprite;
    }

    /// <summary>
    /// 更新投射物命中和生命周期。
    /// </summary>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    public void TickProjectiles(ProjectileRuntimeContext runtimeContext)
    {
        TickRecoverableNails(runtimeContext);

        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            if (i >= _projectiles.Count)
                continue;

            GameProjectile projectile = _projectiles[i];
            if (projectile == null || projectile.IsExpired || projectile.HasHitBoundary)
            {
                ProjectileExpireReason expireReason = GetProjectileExpireReason(projectile);
                RemoveProjectile(i, expireReason, runtimeContext);
                continue;
            }

            TryHitEnemy(i, projectile, runtimeContext);
            if (_projectiles.Count == 0)
                return;

            if (!TryResolveProjectileIndex(projectile, i, out int currentProjectileIndex))
                continue;

            TryHitProjectileTarget(currentProjectileIndex, projectile, runtimeContext);
            if (_projectiles.Count == 0)
                return;
        }
    }

    /// <summary>
    /// 根据投射物当前状态获取回收原因，用于区分生命周期结束、撞墙和手动释放。
    /// </summary>
    /// <param name="projectile">待回收的投射物。</param>
    /// <returns>投射物回收原因。</returns>
    private ProjectileExpireReason GetProjectileExpireReason(GameProjectile projectile)
    {
        if (projectile == null)
            return ProjectileExpireReason.ManualRelease;

        if (projectile.HasHitBoundary)
            return ProjectileExpireReason.HitWall;

        return projectile.IsExpired ? ProjectileExpireReason.LifeTimeEnded : ProjectileExpireReason.ManualRelease;
    }

    /// <summary>
    /// 给新发射的投射物绑定当前弹丸特性和遗物效果快照。
    /// </summary>
    /// <param name="projectile">新发射的投射物。</param>
    /// <param name="sourceProjectile">源投射物。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    private void AttachProjectileSnapshots(GameProjectile projectile, GameProjectile sourceProjectile, ProjectileRuntimeContext runtimeContext)
    {
        if (projectile == null)
            return;

        if (sourceProjectile != null)
        {
            projectile.SetRelicEffectSnapshots(sourceProjectile.HitRelicEffects, sourceProjectile.ExpireRelicEffects);
            return;
        }

        projectile.SetRelicEffectSnapshots(
            runtimeContext.CreateProjectileHitEffectSnapshot != null ? runtimeContext.CreateProjectileHitEffectSnapshot.Invoke() : null,
            runtimeContext.CreateProjectileExpireEffectSnapshot != null ? runtimeContext.CreateProjectileExpireEffectSnapshot.Invoke() : null);
    }

    /// <summary>
    /// 检测指定投射物是否命中敌人。
    /// </summary>
    /// <param name="projectileIndex">投射物索引。</param>
    /// <param name="projectile">投射物组件。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    private void TryHitEnemy(int projectileIndex, GameProjectile projectile, ProjectileRuntimeContext runtimeContext)
    {
        if (runtimeContext.Enemies == null)
            return;

        for (int enemyIndex = runtimeContext.Enemies.Count - 1; enemyIndex >= 0; enemyIndex--)
        {
            GameEnemyController enemy = runtimeContext.Enemies[enemyIndex];
            if (enemy == null)
            {
                runtimeContext.Enemies.RemoveAt(enemyIndex);
                continue;
            }

            float distance = Vector3.Distance(projectile.transform.position, enemy.transform.position);
            if (distance > projectile.HitRadius)
                continue;

            if (!projectile.TryRegisterHit(enemy))
                continue;

            Vector3 hitPosition = enemy.transform.position;
            runtimeContext.ApplyDamageToEnemy.Invoke(enemyIndex, enemy, projectile.DamageInfo, hitPosition, projectile);
            if (!TryResolveProjectileIndex(projectile, projectileIndex, out int currentProjectileIndex))
                return;

            if (enemy != null && !enemy.IsDead)
                runtimeContext.ApplyImpact.Invoke(enemy, projectile.Direction, projectile.ImpactDistance);

            SpawnProjectileEffect(runtimeContext.ProjectileHitEffectPrefab, hitPosition, runtimeContext);
            SplitProjectileOnHit(projectile, enemy, hitPosition, runtimeContext);
            DispatchProjectileHitEffects(projectile, enemy, hitPosition, runtimeContext);
            bool keepProjectile = projectile.TryConsumePierce();
            if (keepProjectile)
                projectile.ApplyPierceDamageDecay(0.85f);

            if (!keepProjectile)
                RemoveProjectile(currentProjectileIndex, ProjectileExpireReason.HitConsumed, runtimeContext);

            return;
        }
    }

    /// <summary>
    /// 检测指定投射物是否命中非敌方投射物目标，例如浆果丛或可破坏场景物。
    /// </summary>
    /// <param name="projectileIndex">投射物索引。</param>
    /// <param name="projectile">投射物组件。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    private void TryHitProjectileTarget(int projectileIndex, GameProjectile projectile, ProjectileRuntimeContext runtimeContext)
    {
        IReadOnlyList<IProjectileHitTarget> targets = runtimeContext.GetProjectileHitTargets != null
            ? runtimeContext.GetProjectileHitTargets.Invoke()
            : null;
        if (targets == null)
            return;

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            IProjectileHitTarget target = targets[i];
            if (target == null || !target.CanBeHitByProjectile)
                continue;

            float hitDistance = projectile.HitRadius + target.ProjectileHitRadius;
            if (Vector3.Distance(projectile.transform.position, target.ProjectileHitPosition) > hitDistance)
                continue;

            if (!projectile.TryRegisterHit(target))
                continue;

            Vector3 hitPosition = target.ProjectileHitPosition;
            target.HandleProjectileHit(projectile, projectile.DamageInfo);
            SpawnProjectileEffect(runtimeContext.ProjectileHitEffectPrefab, hitPosition, runtimeContext);
            bool keepProjectile = projectile.TryConsumePierce();
            if (keepProjectile)
                projectile.ApplyPierceDamageDecay(0.85f);

            if (!keepProjectile)
                RemoveProjectile(projectileIndex, ProjectileExpireReason.HitConsumed, runtimeContext);

            return;
        }
    }

    /// <summary>
    /// 移除投射物。
    /// </summary>
    /// <param name="index">投射物索引。</param>
    /// <param name="expireReason">投射物消失原因。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    private void RemoveProjectile(int index, ProjectileExpireReason expireReason, ProjectileRuntimeContext runtimeContext)
    {
        if (index < 0 || index >= _projectiles.Count)
            return;

        GameProjectile projectile = _projectiles[index];
        Vector3 removePosition = projectile != null ? projectile.transform.position : Vector3.zero;
        RegisterRecoverableNail(projectile, removePosition, expireReason, runtimeContext);
        if (ShouldTriggerExpireEffects(expireReason))
        {
            SpawnProjectileEffect(
                runtimeContext.ProjectileExpireEffectPrefab,
                removePosition,
                runtimeContext);
            DispatchProjectileExpireEffects(
                projectile,
                removePosition,
                expireReason,
                runtimeContext);
        }

        if (projectile != null)
        {
            _hitSplitEnabledProjectiles.Remove(projectile);
            runtimeContext.ReleaseObject?.Invoke(projectile.gameObject);
        }

        _projectiles.RemoveAt(index);
    }

    /// <summary>
    /// 更新忠诚 III 留在场上的钉子，处理自然消失、召回移动和路径伤害。
    /// </summary>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    private void TickRecoverableNails(ProjectileRuntimeContext runtimeContext)
    {
        if (_recoverableNails.Count <= 0)
            return;

        for (int i = _recoverableNails.Count - 1; i >= 0; i--)
        {
            RecoverableNail nail = _recoverableNails[i];
            if (nail == null)
            {
                _recoverableNails.RemoveAt(i);
                continue;
            }

            nail.Timer += Time.deltaTime;
            if (nail.IsRecalling)
            {
                TickRecoverableNailRecall(i, nail, runtimeContext);
                continue;
            }

            if (_recoverableNailAnchorLifeTime > 0f && nail.Timer >= _recoverableNailAnchorLifeTime)
                RemoveRecoverableNail(i);
        }
    }

    /// <summary>
    /// 推进单枚召回钉向玩家飞回，并检测本帧路径上的敌人。
    /// </summary>
    /// <param name="index">钉子列表索引。</param>
    /// <param name="nail">钉子运行时状态。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    private void TickRecoverableNailRecall(int index, RecoverableNail nail, ProjectileRuntimeContext runtimeContext)
    {
        if (runtimeContext.PlayerTransform == null)
        {
            RemoveRecoverableNail(index);
            return;
        }

        Vector3 previousPosition = nail.Position;
        Vector3 targetPosition = runtimeContext.PlayerTransform.position;
        nail.Position = Vector3.MoveTowards(
            previousPosition,
            targetPosition,
            RecoverableNailRecallSpeed * Time.deltaTime);
        SyncRecoverableNailVisual(nail, previousPosition);
        TryHitEnemiesWithRecoverableNail(nail, previousPosition, nail.Position, runtimeContext);
        if (Vector3.Distance(nail.Position, targetPosition) <= RecoverableNailArriveDistance)
            RemoveRecoverableNail(index);
    }

    /// <summary>
    /// 在普通射钉消失时登记一枚可回收钉子。
    /// </summary>
    /// <param name="projectile">即将被移除的投射物。</param>
    /// <param name="position">钉子留下的位置。</param>
    /// <param name="expireReason">投射物移除原因。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    private void RegisterRecoverableNail(
        GameProjectile projectile,
        Vector3 position,
        ProjectileExpireReason expireReason,
        ProjectileRuntimeContext runtimeContext)
    {
        if (!ShouldRegisterRecoverableNail(projectile, expireReason))
            return;

        while (_recoverableNails.Count >= Mathf.Max(1, _recoverableNailMaxCount))
            RemoveRecoverableNail(0);

        RecoverableNail nail = new RecoverableNail
        {
            Position = position,
            Timer = 0f,
            IsRecalling = false,
            RecallVisualApplied = false
        };
        nail.VisualObject = CreateRecoverableNailVisual(
            projectile,
            position,
            runtimeContext,
            out RecoverableNailAnchorView anchorView);
        if (nail.VisualObject == null)
            return;

        nail.AnchorView = anchorView;
        _recoverableNails.Add(nail);
    }

    /// <summary>
    /// 判断当前投射物消失是否应该留下忠诚 III 可回收钉子。
    /// </summary>
    /// <param name="projectile">即将被移除的投射物。</param>
    /// <param name="expireReason">投射物移除原因。</param>
    /// <returns>需要留下钉子时返回 true。</returns>
    private bool ShouldRegisterRecoverableNail(GameProjectile projectile, ProjectileExpireReason expireReason)
    {
        if (!_recoverableNailRecallEnabled || projectile == null || !projectile.HasTag(RecoverableNailTag))
            return false;

        if (!IsRecoverableNailExpireReasonAllowed(expireReason))
            return false;

        return IsRecoverableNailSourceAllowed(projectile);
    }

    /// <summary>
    /// 判断指定投射物消失原因是否允许生成锚点钉子。
    /// </summary>
    /// <param name="expireReason">投射物移除原因。</param>
    /// <returns>允许生成时返回 true。</returns>
    private bool IsRecoverableNailExpireReasonAllowed(ProjectileExpireReason expireReason)
    {
        switch (expireReason)
        {
            case ProjectileExpireReason.HitConsumed:
                return _recoverableNailSpawnOnHitEnemy;

            case ProjectileExpireReason.LifeTimeEnded:
                return _recoverableNailSpawnOnLifeTimeEnded;

            case ProjectileExpireReason.HitWall:
                return _recoverableNailSpawnOnHitWall;

            default:
                return false;
        }
    }

    /// <summary>
    /// 判断指定投射物来源和派生代数是否允许生成锚点钉子。
    /// </summary>
    /// <param name="projectile">即将被移除的投射物。</param>
    /// <returns>允许生成时返回 true。</returns>
    private bool IsRecoverableNailSourceAllowed(GameProjectile projectile)
    {
        if (projectile == null)
            return false;

        bool isNormalAttack = projectile.FireSourceType == ProjectileFireSourceType.NormalAttack;
        bool isOriginal = projectile.Generation <= 0;
        switch (_recoverableNailSpawnScope)
        {
            case RecoverableNailSpawnScope.OriginalNormalProjectilesOnly:
                return isNormalAttack && isOriginal;

            case RecoverableNailSpawnScope.NormalProjectilesAndChildren:
                return isNormalAttack && projectile.Generation <= _recoverableNailMaxChildGeneration;

            case RecoverableNailSpawnScope.AllOriginalProjectiles:
                return isOriginal;

            case RecoverableNailSpawnScope.AllProjectilesAndChildren:
                return projectile.Generation <= _recoverableNailMaxChildGeneration;

            default:
                return false;
        }
    }

    /// <summary>
    /// 创建忠诚 III 留在场上的钉子表现对象。
    /// </summary>
    /// <param name="projectile">来源投射物。</param>
    /// <param name="position">生成位置。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    /// <param name="anchorView">创建出的锚点钉子表现视图。</param>
    /// <returns>钉子表现对象。</returns>
    private GameObject CreateRecoverableNailVisual(
        GameProjectile projectile,
        Vector3 position,
        ProjectileRuntimeContext runtimeContext,
        out RecoverableNailAnchorView anchorView)
    {
        anchorView = null;
        if (_recoverableNailAnchorPrefab == null)
        {
            if (!_recoverableNailMissingPrefabLogged)
            {
                Debug.LogError("[Projectile] 忠诚 III 缺少锚点钉子 Prefab，无法生成可回收钉子。", this);
                _recoverableNailMissingPrefabLogged = true;
            }

            return null;
        }

        GameObject visualObject = Instantiate(_recoverableNailAnchorPrefab);
        Transform visualTransform = visualObject.transform;
        visualTransform.SetParent(runtimeContext.ActiveRuntimeRoot, false);
        visualTransform.position = position;
        visualTransform.rotation = Quaternion.identity;
        visualTransform.localScale = _recoverableNailAnchorPrefab.transform.localScale * _recoverableNailAnchorScale;
        DisableRecoverableNailRuntimeComponents(visualObject);
        anchorView = visualObject.GetComponent<RecoverableNailAnchorView>();
        if (anchorView == null)
        {
            Debug.LogError("[Projectile] 忠诚 III 锚点钉子 Prefab 缺少 RecoverableNailAnchorView，无法生成可回收钉子。", visualObject);
            Destroy(visualObject);
            return null;
        }

        return visualObject;
    }

    /// <summary>
    /// 禁用锚点预制体上从子弹预制体继承来的运行时移动组件，避免锚点被视口边界逻辑移动。
    /// </summary>
    /// <param name="visualObject">锚点表现对象。</param>
    private void DisableRecoverableNailRuntimeComponents(GameObject visualObject)
    {
        if (visualObject == null)
            return;

        GameProjectile[] projectileComponents = visualObject.GetComponentsInChildren<GameProjectile>(true);
        for (int i = 0; i < projectileComponents.Length; i++)
            projectileComponents[i].enabled = false;
    }

    /// <summary>
    /// 同步可回收钉子的表现对象位置和朝向。
    /// </summary>
    /// <param name="nail">钉子运行时状态。</param>
    /// <param name="previousPosition">上一帧位置。</param>
    private void SyncRecoverableNailVisual(RecoverableNail nail, Vector3 previousPosition)
    {
        if (nail == null || nail.VisualObject == null)
            return;

        ApplyRecoverableNailRecallVisual(nail);
        Transform visualTransform = nail.VisualObject.transform;
        visualTransform.position = nail.Position;
        Vector3 direction = nail.Position - previousPosition;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        visualTransform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>
    /// 召回开始后切换锚点钉子的贴图，让正常钉子尖头沿移动方向飞向玩家。
    /// </summary>
    /// <param name="nail">钉子运行时状态。</param>
    private void ApplyRecoverableNailRecallVisual(RecoverableNail nail)
    {
        if (nail == null || nail.RecallVisualApplied)
            return;

        if (nail.AnchorView == null)
        {
            Debug.LogError("[Projectile] 可回收钉子缺少锚点表现视图，无法应用召回表现。", this);
            nail.RecallVisualApplied = true;
            return;
        }

        nail.AnchorView.ApplyRecallVisual(_recoverableNailRecallSprite);
        nail.RecallVisualApplied = true;
    }

    /// <summary>
    /// 检测召回钉本帧路径上的敌人，并对每个敌人最多造成一次伤害。
    /// </summary>
    /// <param name="nail">钉子运行时状态。</param>
    /// <param name="from">本帧路径起点。</param>
    /// <param name="to">本帧路径终点。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    private void TryHitEnemiesWithRecoverableNail(
        RecoverableNail nail,
        Vector3 from,
        Vector3 to,
        ProjectileRuntimeContext runtimeContext)
    {
        if (nail == null || runtimeContext.Enemies == null || runtimeContext.ApplyDamageToEnemy == null)
            return;

        DamageHitInfo hitInfo = CreateRecoverableNailRecallHitInfo(runtimeContext);
        for (int enemyIndex = runtimeContext.Enemies.Count - 1; enemyIndex >= 0; enemyIndex--)
        {
            GameEnemyController enemy = runtimeContext.Enemies[enemyIndex];
            if (enemy == null || enemy.IsDead)
                continue;

            int enemyId = enemy.GetInstanceID();
            if (nail.HitEnemyIds.Contains(enemyId))
                continue;

            float distance = GetDistanceToSegment(enemy.transform.position, from, to);
            if (distance > RecoverableNailHitRadius)
                continue;

            nail.HitEnemyIds.Add(enemyId);
            runtimeContext.ApplyDamageToEnemy.Invoke(enemyIndex, enemy, hitInfo, enemy.transform.position, null);
        }
    }

    /// <summary>
    /// 创建召回钉子的无暴击技能伤害，关闭击杀回能和击杀类触发。
    /// </summary>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    /// <returns>召回钉伤害信息。</returns>
    private DamageHitInfo CreateRecoverableNailRecallHitInfo(ProjectileRuntimeContext runtimeContext)
    {
        int attackDamage = runtimeContext.GetAttackDamage != null
            ? runtimeContext.GetAttackDamage.Invoke()
            : 1;
        int damage = Mathf.Max(1, Mathf.RoundToInt(attackDamage * Mathf.Max(0.01f, _recoverableNailDamageMultiplier)));
        return new DamageHitInfo(
            damage,
            DamageSourceCategory.WeaponSkill,
            DamageElementType.Normal,
            false,
            true,
            false,
            string.Empty,
            "loyalty_recall",
            "loyalty_recall",
            0);
    }

    /// <summary>
    /// 计算点到线段的最短距离，用于召回路径命中判定。
    /// </summary>
    /// <param name="point">待检测点。</param>
    /// <param name="segmentStart">线段起点。</param>
    /// <param name="segmentEnd">线段终点。</param>
    /// <returns>点到线段的最短距离。</returns>
    private float GetDistanceToSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= 0.0001f)
            return Vector3.Distance(point, segmentEnd);

        float t = Mathf.Clamp01(Vector3.Dot(point - segmentStart, segment) / lengthSquared);
        Vector3 closestPoint = segmentStart + segment * t;
        return Vector3.Distance(point, closestPoint);
    }

    /// <summary>
    /// 移除指定索引的可回收钉子和它的表现对象。
    /// </summary>
    /// <param name="index">钉子列表索引。</param>
    private void RemoveRecoverableNail(int index)
    {
        if (index < 0 || index >= _recoverableNails.Count)
            return;

        RecoverableNail nail = _recoverableNails[index];
        if (nail != null && nail.VisualObject != null)
            Destroy(nail.VisualObject);

        _recoverableNails.RemoveAt(index);
    }

    /// <summary>
    /// 清理所有可回收钉子和表现对象。
    /// </summary>
    private void ClearRecoverableNails()
    {
        for (int i = _recoverableNails.Count - 1; i >= 0; i--)
        {
            RecoverableNail nail = _recoverableNails[i];
            if (nail != null && nail.VisualObject != null)
                Destroy(nail.VisualObject);
        }

        _recoverableNails.Clear();
    }

    /// <summary>
    /// 重新确认投射物仍在运行列表中，并返回当前有效索引。
    /// </summary>
    /// <param name="projectile">需要确认的投射物。</param>
    /// <param name="preferredIndex">命中检测开始时的索引。</param>
    /// <param name="currentIndex">当前有效索引。</param>
    /// <returns>投射物仍可被安全访问时返回 true。</returns>
    private bool TryResolveProjectileIndex(GameProjectile projectile, int preferredIndex, out int currentIndex)
    {
        currentIndex = -1;
        if (projectile == null)
            return false;

        if (preferredIndex >= 0 &&
            preferredIndex < _projectiles.Count &&
            ReferenceEquals(_projectiles[preferredIndex], projectile))
        {
            currentIndex = preferredIndex;
            return true;
        }

        currentIndex = _projectiles.IndexOf(projectile);
        return currentIndex >= 0;
    }

    /// <summary>
    /// 调度投射物命中阶段效果。
    /// </summary>
    /// <param name="projectile">触发命中的投射物。</param>
    /// <param name="hitEnemy">命中敌人。</param>
    /// <param name="hitPosition">命中位置。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    private void DispatchProjectileHitEffects(
        GameProjectile projectile,
        GameEnemyController hitEnemy,
        Vector3 hitPosition,
        ProjectileRuntimeContext runtimeContext)
    {
        _hitEffectBuffer.Clear();
        AddRelicHitEffects(projectile, _hitEffectBuffer);
        ProjectileHitEffectContext context = new ProjectileHitEffectContext(
            projectile,
            hitEnemy,
            hitPosition,
            projectile != null ? projectile.DamageInfo : default,
            runtimeContext);
        _effectDispatcher.DispatchHitEffects(context, _hitEffectBuffer);
    }

    /// <summary>
    /// 调度投射物消失阶段效果。
    /// </summary>
    /// <param name="projectile">触发消失的投射物。</param>
    /// <param name="position">消失位置。</param>
    /// <param name="expireReason">消失原因。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    private void DispatchProjectileExpireEffects(
        GameProjectile projectile,
        Vector3 position,
        ProjectileExpireReason expireReason,
        ProjectileRuntimeContext runtimeContext)
    {
        _expireEffectBuffer.Clear();
        AddRelicExpireEffects(projectile, _expireEffectBuffer);
        ProjectileExpireEffectContext context = new ProjectileExpireEffectContext(projectile, position, expireReason, runtimeContext);
        _effectDispatcher.DispatchExpireEffects(context, _expireEffectBuffer);
    }

    /// <summary>
    /// 将投射物携带的遗物命中效果加入调度列表。
    /// </summary>
    /// <param name="projectile">投射物。</param>
    /// <param name="effects">效果调度列表。</param>
    private void AddRelicHitEffects(GameProjectile projectile, List<Action<ProjectileHitEffectContext>> effects)
    {
        if (projectile == null || effects == null)
            return;

        IReadOnlyList<IProjectileHitRelicEffect> relicEffects = projectile.HitRelicEffects;
        for (int i = 0; i < relicEffects.Count; i++)
        {
            IProjectileHitRelicEffect effect = relicEffects[i];
            if (effect != null)
                effects.Add(effect.OnProjectileHit);
        }
    }

    /// <summary>
    /// 将投射物携带的遗物消失效果加入调度列表。
    /// </summary>
    /// <param name="projectile">投射物。</param>
    /// <param name="effects">效果调度列表。</param>
    private void AddRelicExpireEffects(GameProjectile projectile, List<Action<ProjectileExpireEffectContext>> effects)
    {
        if (projectile == null || effects == null)
            return;

        IReadOnlyList<IProjectileExpireRelicEffect> relicEffects = projectile.ExpireRelicEffects;
        for (int i = 0; i < relicEffects.Count; i++)
        {
            IProjectileExpireRelicEffect effect = relicEffects[i];
            if (effect != null)
                effects.Add(effect.OnProjectileExpire);
        }
    }

    /// <summary>
    /// 判断指定消失原因是否允许触发投射物消失效果。
    /// </summary>
    /// <param name="expireReason">投射物消失原因。</param>
    /// <returns>需要触发消失效果时返回 true。</returns>
    private bool ShouldTriggerExpireEffects(ProjectileExpireReason expireReason)
    {
        return expireReason == ProjectileExpireReason.HitConsumed || expireReason == ProjectileExpireReason.LifeTimeEnded;
    }

    /// <summary>
    /// 生成投射物命中或消失特效。
    /// </summary>
    /// <param name="effectPrefab">特效预制体。</param>
    /// <param name="position">生成位置。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    private void SpawnProjectileEffect(
        GameObject effectPrefab,
        Vector3 position,
        ProjectileRuntimeContext runtimeContext)
    {
        if (effectPrefab == null || runtimeContext.RuntimeObjectPoolController == null)
            return;

        GamePooledObject effectObject = runtimeContext.RuntimeObjectPoolController.Get(
            GameRuntimeObjectPoolController.PoolObjectType.ProjectileEffect,
            effectPrefab,
            runtimeContext.ActiveRuntimeRoot,
            position,
            Vector3.one,
            Color.white);
        GamePooledEffect pooledEffect = effectObject.GetComponent<GamePooledEffect>();
        if (pooledEffect == null)
            pooledEffect = effectObject.gameObject.AddComponent<GamePooledEffect>();

        pooledEffect.Play(runtimeContext.ReleaseObject);
    }

    /// <summary>
    /// 子弹命中后随机方向分裂子弹。
    /// </summary>
    /// <param name="projectile">触发分裂的投射物。</param>
    /// <param name="hitEnemy">触发分裂的敌人。</param>
    /// <param name="runtimeContext">运行时依赖上下文。</param>
    private void SplitProjectileOnHit(
        GameProjectile projectile,
        GameEnemyController hitEnemy,
        Vector3 hitPosition,
        ProjectileRuntimeContext runtimeContext)
    {
        if (!_projectileSplitOnHit || projectile == null || !_hitSplitEnabledProjectiles.Contains(projectile))
            return;

        _hitSplitEnabledProjectiles.Remove(projectile);
        ProjectileFireContext context = CreateNormalProjectileContext(projectile.RemainingPierce, projectile.RemainingBounce);
        context.DamageMultiplier = _projectileSplitOnHitDamageMultiplier;
        context.CanSplitOnHit = false;
        context.UseScaledDamage = true;
        context.IsRecoverableNailCandidate = true;
        for (int i = 0; i < 2; i++)
        {
            Vector3 splitDirection = GetRandomProjectileSplitDirection();
            GameProjectile splitProjectile = FireChildProjectile(splitDirection, hitPosition + splitDirection * 0.18f, context, projectile, runtimeContext);
            IgnoreSplitSourceTarget(splitProjectile, hitEnemy);
        }
    }

    /// <summary>
    /// 获取命中分裂子弹的随机散射方向。
    /// </summary>
    /// <returns>随机单位方向。</returns>
    private Vector3 GetRandomProjectileSplitDirection()
    {
        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
    }

    /// <summary>
    /// 让命中分裂子弹忽略刚触发分裂的目标。
    /// </summary>
    /// <param name="projectile">分裂出的投射物。</param>
    /// <param name="sourceTarget">触发分裂的目标。</param>
    private void IgnoreSplitSourceTarget(GameProjectile projectile, GameEnemyController sourceTarget)
    {
        if (projectile == null || sourceTarget == null)
            return;

        projectile.TryRegisterHit(sourceTarget);
    }

}
