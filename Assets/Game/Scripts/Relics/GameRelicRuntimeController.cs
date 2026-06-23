using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理遗物拥有状态、堆叠、效果实例注册和触发调度。
/// </summary>
public sealed class GameRelicRuntimeController : MonoBehaviour
{
    private readonly Dictionary<string, RelicRuntimeEntry> _entriesByRelicId = new Dictionary<string, RelicRuntimeEntry>();
    private readonly Dictionary<string, IRelicEffect> _effectsByRelicId = new Dictionary<string, IRelicEffect>();
    private readonly List<RelicRuntimeEntry> _ownedEntries = new List<RelicRuntimeEntry>();
    private readonly List<IOnAcquireRelicEffect> _onAcquireEffects = new List<IOnAcquireRelicEffect>();
    private readonly List<IPassiveStatRelicEffect> _passiveStatEffects = new List<IPassiveStatRelicEffect>();
    private readonly List<IProjectileHitRelicEffect> _projectileHitEffects = new List<IProjectileHitRelicEffect>();
    private readonly List<IProjectileExpireRelicEffect> _projectileExpireEffects = new List<IProjectileExpireRelicEffect>();
    private readonly List<IDamageReactionRelicEffect> _damageReactionEffects = new List<IDamageReactionRelicEffect>();
    private readonly List<IEnemyKilledRelicEffect> _enemyKilledEffects = new List<IEnemyKilledRelicEffect>();
    private readonly List<IPickupRelicEffect> _pickupEffects = new List<IPickupRelicEffect>();
    private readonly List<IFloorStartRelicEffect> _floorStartEffects = new List<IFloorStartRelicEffect>();
    private readonly RelicEffectFactory _effectFactory = new RelicEffectFactory();

    private int _nextAcquireOrder;

    /// <summary>
    /// 已拥有遗物条目。
    /// </summary>
    public IReadOnlyList<RelicRuntimeEntry> OwnedEntries => _ownedEntries;

    /// <summary>
    /// 当前玩家控制器。
    /// </summary>
    public GamePlayerController Player { get; private set; }

    /// <summary>
    /// 奖励掉落控制器。
    /// </summary>
    public GameRewardDropController RewardDropController { get; private set; }

    private void Awake()
    {
        RegisterBuiltInEffects();
    }

    /// <summary>
    /// 初始化遗物运行时依赖。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="rewardDropController">奖励掉落控制器。</param>
    public void InitializeRuntime(GamePlayerController player, GameRewardDropController rewardDropController)
    {
        Player = player;
        RewardDropController = rewardDropController;
    }

    /// <summary>
    /// 重置遗物运行时状态。
    /// </summary>
    public void ResetRuntime()
    {
        _entriesByRelicId.Clear();
        _effectsByRelicId.Clear();
        _ownedEntries.Clear();
        _onAcquireEffects.Clear();
        _passiveStatEffects.Clear();
        _projectileHitEffects.Clear();
        _projectileExpireEffects.Clear();
        _damageReactionEffects.Clear();
        _enemyKilledEffects.Clear();
        _pickupEffects.Clear();
        _floorStartEffects.Clear();
        _nextAcquireOrder = 0;
    }

    /// <summary>
    /// 判断遗物当前是否可以获得。
    /// </summary>
    /// <param name="relicData">遗物配置。</param>
    /// <returns>可以获得时返回 true。</returns>
    public bool CanAcquireRelic(RelicData relicData)
    {
        if (!TryValidateRelicData(relicData, false, out string relicId, out string effectKey, out int maxStack))
            return false;

        if (!_entriesByRelicId.TryGetValue(relicId, out RelicRuntimeEntry entry))
            return true;

        return !relicData.GlobalUnique && entry.StackCount < maxStack;
    }

    /// <summary>
    /// 尝试获得并注册遗物。
    /// </summary>
    /// <param name="relicData">遗物配置。</param>
    /// <returns>获得成功时返回 true。</returns>
    public bool TryAcquireRelic(RelicData relicData)
    {
        if (!TryValidateRelicData(relicData, true, out string relicId, out string effectKey, out int maxStack))
            return false;

        if (_entriesByRelicId.TryGetValue(relicId, out RelicRuntimeEntry existingEntry))
        {
            if (relicData.GlobalUnique)
                return false;

            if (!existingEntry.TryAddStack())
                return false;

            SortEffectLists();
            RefreshPassiveStats();
            return true;
        }

        if (!_effectFactory.TryCreate(effectKey, out IRelicEffect effect))
        {
            Debug.LogError($"[Relic] 未知遗物效果键：{effectKey}，RelicId：{relicId}");
            return false;
        }

        RelicRuntimeEntry entry = new RelicRuntimeEntry(relicData, effectKey, maxStack, _nextAcquireOrder++);
        effect.Initialize(entry);
        _entriesByRelicId[relicId] = entry;
        _effectsByRelicId[relicId] = effect;
        _ownedEntries.Add(entry);
        RegisterEffectInterfaces(effect);
        SortEffectLists();

        if (effect is IOnAcquireRelicEffect onAcquireEffect)
            onAcquireEffect.OnAcquire(this);

        RefreshPassiveStats();
        return true;
    }

    /// <summary>
    /// 获取当前投射物命中特效快照。
    /// </summary>
    /// <returns>命中特效快照。</returns>
    public List<IProjectileHitRelicEffect> CreateProjectileHitEffectSnapshot()
    {
        return new List<IProjectileHitRelicEffect>(_projectileHitEffects);
    }

    /// <summary>
    /// 获取当前投射物消失特效快照。
    /// </summary>
    /// <returns>消失特效快照。</returns>
    public List<IProjectileExpireRelicEffect> CreateProjectileExpireEffectSnapshot()
    {
        return new List<IProjectileExpireRelicEffect>(_projectileExpireEffects);
    }

    /// <summary>
    /// 获取遗物提供的暴击率加成。
    /// </summary>
    /// <returns>暴击率加成。</returns>
    public float GetCriticalRateBonus()
    {
        float bonus = 0f;
        for (int i = 0; i < _passiveStatEffects.Count; i++)
        {
            if (_passiveStatEffects[i] is CollectorsLedgerRelicEffect collectorsLedger)
                bonus += collectorsLedger.GetCriticalRateBonus(_ownedEntries.Count);
        }

        return bonus;
    }

    /// <summary>
    /// 调度敌人受伤反应遗物。
    /// </summary>
    /// <param name="context">敌人受伤事件上下文。</param>
    public void DispatchEnemyDamaged(EnemyDamagedContext context)
    {
        if (!context.DamageInfo.CanTriggerDamageReaction)
            return;

        for (int i = 0; i < _damageReactionEffects.Count; i++)
            _damageReactionEffects[i].OnEnemyDamaged(context);
    }

    /// <summary>
    /// 注册内置遗物效果。
    /// </summary>
    private void RegisterBuiltInEffects()
    {
        _effectFactory.Register("old_charm", () => new OldCharmRelicEffect());
        _effectFactory.Register("gold_compass", () => new GoldCompassRelicEffect());
        _effectFactory.Register("collectors_ledger", () => new CollectorsLedgerRelicEffect());
        _effectFactory.Register("burst_core", () => new BurstCoreRelicEffect());
        _effectFactory.Register("split_bullet", () => new SplitBulletRelicEffect());
        _effectFactory.Register("sandevistan", () => new SandevistanRelicEffect());
    }

    /// <summary>
    /// 校验遗物配置并提取运行时字段。
    /// </summary>
    /// <param name="relicData">遗物配置。</param>
    /// <param name="logError">是否输出错误日志。</param>
    /// <param name="relicId">遗物 ID。</param>
    /// <param name="effectKey">效果键。</param>
    /// <param name="maxStack">最大堆叠。</param>
    /// <returns>配置有效时返回 true。</returns>
    private bool TryValidateRelicData(RelicData relicData, bool logError, out string relicId, out string effectKey, out int maxStack)
    {
        relicId = relicData != null ? relicData.RelicId : string.Empty;
        effectKey = relicData != null ? relicData.EffectKey : string.Empty;
        maxStack = relicData != null ? relicData.MaxStack : 1;

        if (relicData == null)
        {
            if (logError)
                Debug.LogWarning("[Relic] 获取遗物失败：RelicData 为空。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(relicId))
        {
            if (logError)
                Debug.LogError("[Relic] 获取遗物失败：RelicId 为空。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(effectKey))
        {
            if (logError)
                Debug.LogError($"[Relic] 获取遗物失败：EffectKey 为空，RelicId：{relicId}");
            return false;
        }

        if (!_effectFactory.Contains(effectKey))
        {
            if (logError)
                Debug.LogError($"[Relic] 获取遗物失败：未知 EffectKey：{effectKey}，RelicId：{relicId}");
            return false;
        }

        if (maxStack <= 0)
        {
            if (logError)
                Debug.LogWarning($"[Relic] MaxStack 配置无效，按 1 处理，RelicId：{relicId}");
            maxStack = 1;
        }

        return true;
    }

    /// <summary>
    /// 按实现接口注册遗物效果。
    /// </summary>
    /// <param name="effect">遗物效果实例。</param>
    private void RegisterEffectInterfaces(IRelicEffect effect)
    {
        if (effect is IOnAcquireRelicEffect onAcquireEffect)
            _onAcquireEffects.Add(onAcquireEffect);

        if (effect is IPassiveStatRelicEffect passiveStatEffect)
            _passiveStatEffects.Add(passiveStatEffect);

        if (effect is IProjectileHitRelicEffect projectileHitEffect)
            _projectileHitEffects.Add(projectileHitEffect);

        if (effect is IProjectileExpireRelicEffect projectileExpireEffect)
            _projectileExpireEffects.Add(projectileExpireEffect);

        if (effect is IDamageReactionRelicEffect damageReactionEffect)
            _damageReactionEffects.Add(damageReactionEffect);

        if (effect is IEnemyKilledRelicEffect enemyKilledEffect)
            _enemyKilledEffects.Add(enemyKilledEffect);

        if (effect is IPickupRelicEffect pickupEffect)
            _pickupEffects.Add(pickupEffect);

        if (effect is IFloorStartRelicEffect floorStartEffect)
            _floorStartEffects.Add(floorStartEffect);
    }

    /// <summary>
    /// 刷新被动属性聚合。
    /// </summary>
    private void RefreshPassiveStats()
    {
        for (int i = 0; i < _passiveStatEffects.Count; i++)
            _passiveStatEffects[i].RefreshPassiveStats(this);
    }

    /// <summary>
    /// 按执行顺序、获得顺序和效果键排序所有效果列表。
    /// </summary>
    private void SortEffectLists()
    {
        _onAcquireEffects.Sort(CompareEffects);
        _passiveStatEffects.Sort(CompareEffects);
        _projectileHitEffects.Sort(CompareEffects);
        _projectileExpireEffects.Sort(CompareEffects);
        _damageReactionEffects.Sort(CompareEffects);
        _enemyKilledEffects.Sort(CompareEffects);
        _pickupEffects.Sort(CompareEffects);
        _floorStartEffects.Sort(CompareEffects);
    }

    /// <summary>
    /// 比较两个遗物效果的稳定执行顺序。
    /// </summary>
    /// <param name="left">左侧效果。</param>
    /// <param name="right">右侧效果。</param>
    /// <returns>排序结果。</returns>
    private int CompareEffects(IRelicEffect left, IRelicEffect right)
    {
        if (left == null || right == null)
            return left == null ? 1 : -1;

        int orderCompare = left.Entry.ExecutionOrder.CompareTo(right.Entry.ExecutionOrder);
        if (orderCompare != 0)
            return orderCompare;

        int acquireCompare = left.Entry.AcquireOrder.CompareTo(right.Entry.AcquireOrder);
        if (acquireCompare != 0)
            return acquireCompare;

        return string.CompareOrdinal(left.Entry.EffectKey, right.Entry.EffectKey);
    }
}
