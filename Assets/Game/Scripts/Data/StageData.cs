using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间类型，用于描述关卡流程中玩家即将进入的房间内容。
/// </summary>
public enum RoomType
{
    /// <summary>
    /// 普通战斗房，会占用一个关卡房间节点。
    /// </summary>
    CombatNormal,

    /// <summary>
    /// 精英战斗房，会占用一个关卡房间节点并使用更高难度倍率。
    /// </summary>
    CombatElite,

    /// <summary>
    /// Boss 房，会占用一个关卡房间节点并使用 Boss 配置。
    /// </summary>
    Boss,

    /// <summary>
    /// 商店房，会占用一个关卡房间节点，离开后进入绑定的目标战斗房。
    /// </summary>
    Shop,

    /// <summary>
    /// 事件房，会占用一个关卡房间节点，第一版作为通用功能房使用。
    /// </summary>
    Event,

    /// <summary>
    /// 宝箱房，会占用一个关卡房间节点，第一版作为通用功能房使用。
    /// </summary>
    Treasure
}

/// <summary>
/// Boss 技能触发类型，用于后续 Boss 行为运行时判断释放时机。
/// </summary>
public enum BossSkillTriggerType
{
    /// <summary>
    /// Boss 战开始时触发。
    /// </summary>
    OnStart,

    /// <summary>
    /// Boss 血量百分比达到阈值时触发。
    /// </summary>
    OnHealthPercent,

    /// <summary>
    /// Boss 技能冷却结束时触发。
    /// </summary>
    OnCooldown,

    /// <summary>
    /// Boss 死亡时触发，第一版仅预留。
    /// </summary>
    OnDeath
}

/// <summary>
/// Boss 技能效果类型，第一版优先支持召唤敌人。
/// </summary>
public enum BossSkillEffectType
{
    /// <summary>
    /// 召唤敌人。
    /// </summary>
    SummonEnemies
}

/// <summary>
/// Boss 召唤敌人时使用的位置规则。
/// </summary>
public enum BossSummonPositionRule
{
    /// <summary>
    /// 在 Boss 周围生成。
    /// </summary>
    AroundBoss,

    /// <summary>
    /// 在玩家周围生成。
    /// </summary>
    AroundPlayer,

    /// <summary>
    /// 使用房间预设生成点。
    /// </summary>
    RoomSpawnPoints
}

/// <summary>
/// 定义一整关的房间、刷怪、Boss 和难度曲线静态配置。
/// </summary>
[CreateAssetMenu(fileName = "StageData", menuName = "Game/Data/Stage Data")]
public sealed class StageData : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("关卡唯一标识，用于存档、日志或关卡解锁查找。")]
    [SerializeField] private string _stageId = "stage_01";
    [Tooltip("关卡在 UI 或调试信息中显示的名称。")]
    [SerializeField] private string _displayName = "第一关";
    [Tooltip("本关包含的房间节点总数，所有房间类型都会占用节点。")]
    [SerializeField] private int _totalCombatNodeCount = 30;
    [Tooltip("通关后解锁的下一关关卡 ID；为空表示没有后续关卡。")]
    [SerializeField] private string _unlockStageIdAfterClear;

    [Header("战斗房配置")]
    [Tooltip("普通战斗房倍率和奖励配置。")]
    [SerializeField] private CombatRoomProfile _normalCombatRoomProfile = new CombatRoomProfile(RoomType.CombatNormal);
    [Tooltip("精英战斗房倍率和奖励配置。")]
    [SerializeField] private CombatRoomProfile _eliteCombatRoomProfile = new CombatRoomProfile(RoomType.CombatElite);
    [Tooltip("普通战斗房和精英战斗房共用的刷怪预算、批次和敌人池配置。")]
    [SerializeField] private EnemySpawnProfile _enemySpawnProfile = new EnemySpawnProfile();

    [Header("Boss 配置")]
    [Tooltip("本关可用的 Boss 配置列表，按适用战斗节点查找。")]
    [SerializeField] private BossData[] _bosses = new BossData[0];

    [Header("房间门规则")]
    [Tooltip("战斗结束后生成门选项时使用的规则列表，特殊节点可通过强制覆盖规则处理。")]
    [SerializeField] private RoomOptionRule[] _roomOptionRules = new RoomOptionRule[0];

    [Header("难度曲线")]
    [Tooltip("敌人属性和奖励随战斗节点增长的倍率曲线配置。")]
    [SerializeField] private StageDifficultyCurve _difficultyCurve = new StageDifficultyCurve();

    /// <summary>
    /// 关卡唯一标识。
    /// </summary>
    public string StageId => _stageId;

    /// <summary>
    /// 关卡显示名称。
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// 房间节点总数。
    /// </summary>
    public int TotalCombatNodeCount => Mathf.Max(1, _totalCombatNodeCount);

    /// <summary>
    /// 通关后解锁的下一关关卡 ID。
    /// </summary>
    public string UnlockStageIdAfterClear => _unlockStageIdAfterClear;

    /// <summary>
    /// 普通战斗房配置。
    /// </summary>
    public CombatRoomProfile NormalCombatRoomProfile => _normalCombatRoomProfile;

    /// <summary>
    /// 精英战斗房配置。
    /// </summary>
    public CombatRoomProfile EliteCombatRoomProfile => _eliteCombatRoomProfile;

    /// <summary>
    /// 敌人刷怪配置。
    /// </summary>
    public EnemySpawnProfile EnemySpawnProfile => _enemySpawnProfile;

    /// <summary>
    /// Boss 配置列表。
    /// </summary>
    public BossData[] Bosses => _bosses;

    /// <summary>
    /// 门选项规则列表。
    /// </summary>
    public RoomOptionRule[] RoomOptionRules => _roomOptionRules;

    /// <summary>
    /// 难度成长曲线。
    /// </summary>
    public StageDifficultyCurve DifficultyCurve => _difficultyCurve;

    /// <summary>
    /// 判断战斗节点是否在本关合法范围内。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <returns>节点合法时返回 true。</returns>
    public bool IsValidCombatNode(int combatNode)
    {
        return combatNode >= 1 && combatNode <= TotalCombatNodeCount;
    }

    /// <summary>
    /// 判断指定战斗节点是否为 Boss 节点。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <returns>存在 BossData 的节点返回 true。</returns>
    public bool IsBossCombatNode(int combatNode)
    {
        return FindBossData(combatNode) != null;
    }

    /// <summary>
    /// 判断指定战斗节点是否配置了最终 Boss 标记。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <returns>BossData 标记为最终 Boss 时返回 true。</returns>
    public bool IsFinalBossCombatNode(int combatNode)
    {
        BossData bossData = FindBossData(combatNode);
        return bossData != null && bossData.IsFinalBoss;
    }

    /// <summary>
    /// 按房间类型获取普通或精英战斗房配置。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>战斗房配置；非普通或精英战斗房返回 null。</returns>
    public CombatRoomProfile GetCombatRoomProfile(RoomType roomType)
    {
        if (roomType == RoomType.CombatElite)
            return _eliteCombatRoomProfile;

        if (roomType == RoomType.CombatNormal)
            return _normalCombatRoomProfile;

        return null;
    }

    /// <summary>
    /// 查找指定战斗节点对应的 Boss 配置。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <returns>匹配的 Boss 配置；没有匹配时返回 null。</returns>
    public BossData FindBossData(int combatNode)
    {
        if (_bosses == null)
            return null;

        for (int i = 0; i < _bosses.Length; i++)
        {
            BossData bossData = _bosses[i];
            if (bossData != null && bossData.CombatNode == combatNode)
                return bossData;
        }

        return null;
    }

    /// <summary>
    /// 创建指定 Boss 节点使用的唯一 Boss 门选项。
    /// </summary>
    /// <param name="combatNode">Boss 所在房间节点。</param>
    /// <returns>Boss 门选项；节点没有 Boss 配置时返回 null。</returns>
    public RoomOptionData CreateBossRoomOption(int combatNode)
    {
        BossData bossData = FindBossData(combatNode);
        if (bossData == null)
            return null;

        string displayName = string.IsNullOrWhiteSpace(bossData.DisplayName) ? "Boss 房" : bossData.DisplayName;
        string tooltip = $"挑战 {displayName}";
        return new RoomOptionData(RoomType.Boss, displayName, tooltip, "door_boss", RoomType.Boss);
    }

    /// <summary>
    /// 查找指定战斗节点对应的敌人池分段。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <returns>匹配的敌人池分段；没有匹配时返回 null。</returns>
    public EnemyPoolSegment FindEnemyPoolSegment(int combatNode)
    {
        if (_enemySpawnProfile == null || _enemySpawnProfile.EnemyPoolSegments == null)
            return null;

        EnemyPoolSegment[] segments = _enemySpawnProfile.EnemyPoolSegments;
        for (int i = 0; i < segments.Length; i++)
        {
            EnemyPoolSegment segment = segments[i];
            if (segment != null && segment.ContainsCombatNode(combatNode))
                return segment;
        }

        return null;
    }

    /// <summary>
    /// 获取指定战斗节点适用的所有门选项规则。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <returns>适用规则数组；没有匹配时返回空数组。</returns>
    public RoomOptionRule[] GetRoomOptionRules(int combatNode)
    {
        if (_roomOptionRules == null || _roomOptionRules.Length <= 0)
            return Array.Empty<RoomOptionRule>();

        List<RoomOptionRule> rules = new List<RoomOptionRule>();
        for (int i = 0; i < _roomOptionRules.Length; i++)
        {
            RoomOptionRule rule = _roomOptionRules[i];
            if (rule != null && rule.ContainsCombatNode(combatNode))
                rules.Add(rule);
        }

        return rules.ToArray();
    }

    /// <summary>
    /// 按强制覆盖优先、权重随机的规则选择一个门选项规则。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <returns>选中的门选项规则；没有匹配时返回 null。</returns>
    public RoomOptionRule SelectRoomOptionRule(int combatNode)
    {
        RoomOptionRule[] rules = GetRoomOptionRules(combatNode);
        if (rules == null || rules.Length <= 0)
            return null;

        List<RoomOptionRule> overrideRules = new List<RoomOptionRule>();
        List<RoomOptionRule> normalRules = new List<RoomOptionRule>();
        for (int i = 0; i < rules.Length; i++)
        {
            RoomOptionRule rule = rules[i];
            if (rule == null)
                continue;

            if (rule.ForceOverride)
                overrideRules.Add(rule);
            else
                normalRules.Add(rule);
        }

        if (overrideRules.Count > 0)
            return SelectWeightedRoomOptionRule(overrideRules);

        return SelectWeightedRoomOptionRule(normalRules);
    }

    /// <summary>
    /// 为指定房间节点生成门选项，Boss 节点会强制返回唯一 Boss 门。
    /// </summary>
    /// <param name="combatNode">即将进入的目标房间节点。</param>
    /// <returns>生成的门选项数组。</returns>
    public RoomOptionData[] CreateRoomOptionsForNode(int combatNode)
    {
        if (IsBossCombatNode(combatNode))
        {
            RoomOptionData bossOption = CreateBossRoomOption(combatNode);
            if (bossOption == null)
                return Array.Empty<RoomOptionData>();

            return new[] { bossOption };
        }

        RoomOptionRule rule = SelectRoomOptionRule(combatNode);
        return rule != null ? rule.CreateRoomOptions() : Array.Empty<RoomOptionData>();
    }

    /// <summary>
    /// 使用已经选定的门规则生成指定房间节点的门选项，Boss 节点仍会强制返回唯一 Boss 门。
    /// </summary>
    /// <param name="combatNode">即将进入的目标房间节点。</param>
    /// <param name="rule">已选定的门规则。</param>
    /// <returns>生成的门选项数组。</returns>
    public RoomOptionData[] CreateRoomOptionsForRule(int combatNode, RoomOptionRule rule)
    {
        if (IsBossCombatNode(combatNode))
        {
            RoomOptionData bossOption = CreateBossRoomOption(combatNode);
            if (bossOption == null)
                return Array.Empty<RoomOptionData>();

            return new[] { bossOption };
        }

        return rule != null ? rule.CreateRoomOptions() : Array.Empty<RoomOptionData>();
    }

    /// <summary>
    /// 计算指定战斗节点的基础刷怪预算。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <returns>未叠加房间倍率的刷怪预算。</returns>
    public int CalculateBaseSpawnBudget(int combatNode)
    {
        if (_enemySpawnProfile == null)
            return 0;

        int safeNode = Mathf.Max(1, combatNode);
        return _enemySpawnProfile.BaseBudget + (safeNode - 1) * _enemySpawnProfile.BudgetGrowthPerCombatNode;
    }

    /// <summary>
    /// 计算指定战斗节点和房间类型的最终刷怪预算。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <param name="roomType">房间类型。</param>
    /// <returns>叠加房间倍率后的刷怪预算。</returns>
    public int CalculateRoomSpawnBudget(int combatNode, RoomType roomType)
    {
        int baseBudget = CalculateBaseSpawnBudget(combatNode);
        CombatRoomProfile roomProfile = GetCombatRoomProfile(roomType);
        float multiplier = roomProfile != null ? roomProfile.BudgetMultiplier : 1f;
        return Mathf.Max(0, Mathf.RoundToInt(baseBudget * multiplier));
    }

    /// <summary>
    /// 计算指定战斗节点和房间类型的生命倍率。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <param name="roomType">房间类型。</param>
    /// <returns>最终生命倍率。</returns>
    public float CalculateHealthMultiplier(int combatNode, RoomType roomType)
    {
        CombatRoomProfile roomProfile = GetCombatRoomProfile(roomType);
        float roomMultiplier = roomProfile != null ? roomProfile.HealthMultiplier : 1f;
        return CalculateDifficultyMultiplier(
            combatNode,
            _difficultyCurve != null ? _difficultyCurve.BaseHealthMultiplier : 1f,
            _difficultyCurve != null ? _difficultyCurve.HealthMultiplierGrowthPerCombatNode : 0f) * roomMultiplier;
    }

    /// <summary>
    /// 计算指定战斗节点和房间类型的速度倍率。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <param name="roomType">房间类型。</param>
    /// <returns>最终速度倍率。</returns>
    public float CalculateSpeedMultiplier(int combatNode, RoomType roomType)
    {
        CombatRoomProfile roomProfile = GetCombatRoomProfile(roomType);
        float roomMultiplier = roomProfile != null ? roomProfile.SpeedMultiplier : 1f;
        return CalculateDifficultyMultiplier(
            combatNode,
            _difficultyCurve != null ? _difficultyCurve.BaseSpeedMultiplier : 1f,
            _difficultyCurve != null ? _difficultyCurve.SpeedMultiplierGrowthPerCombatNode : 0f) * roomMultiplier;
    }

    /// <summary>
    /// 计算指定战斗节点和房间类型的伤害倍率。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <param name="roomType">房间类型。</param>
    /// <returns>最终伤害倍率。</returns>
    public float CalculateDamageMultiplier(int combatNode, RoomType roomType)
    {
        CombatRoomProfile roomProfile = GetCombatRoomProfile(roomType);
        float roomMultiplier = roomProfile != null ? roomProfile.DamageMultiplier : 1f;
        return CalculateDifficultyMultiplier(
            combatNode,
            _difficultyCurve != null ? _difficultyCurve.BaseDamageMultiplier : 1f,
            _difficultyCurve != null ? _difficultyCurve.DamageMultiplierGrowthPerCombatNode : 0f) * roomMultiplier;
    }

    /// <summary>
    /// 计算指定战斗节点和房间类型的金币倍率。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <param name="roomType">房间类型。</param>
    /// <returns>最终金币倍率。</returns>
    public float CalculateGoldMultiplier(int combatNode, RoomType roomType)
    {
        CombatRoomProfile roomProfile = GetCombatRoomProfile(roomType);
        float roomMultiplier = roomProfile != null ? roomProfile.RewardMultiplier : 1f;
        return CalculateDifficultyMultiplier(
            combatNode,
            _difficultyCurve != null ? _difficultyCurve.BaseGoldMultiplier : 1f,
            _difficultyCurve != null ? _difficultyCurve.GoldMultiplierGrowthPerCombatNode : 0f) * roomMultiplier;
    }

    /// <summary>
    /// 计算指定战斗节点和房间类型的经验倍率。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <param name="roomType">房间类型。</param>
    /// <returns>最终经验倍率。</returns>
    public float CalculateExperienceMultiplier(int combatNode, RoomType roomType)
    {
        CombatRoomProfile roomProfile = GetCombatRoomProfile(roomType);
        float roomMultiplier = roomProfile != null ? roomProfile.RewardMultiplier : 1f;
        return CalculateDifficultyMultiplier(
            combatNode,
            _difficultyCurve != null ? _difficultyCurve.BaseExperienceMultiplier : 1f,
            _difficultyCurve != null ? _difficultyCurve.ExperienceMultiplierGrowthPerCombatNode : 0f) * roomMultiplier;
    }

    /// <summary>
    /// 从候选规则中按权重选择一个规则。
    /// </summary>
    /// <param name="rules">候选规则列表。</param>
    /// <returns>选中的规则；候选为空时返回 null。</returns>
    private RoomOptionRule SelectWeightedRoomOptionRule(List<RoomOptionRule> rules)
    {
        if (rules == null || rules.Count <= 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < rules.Count; i++)
        {
            RoomOptionRule rule = rules[i];
            if (rule != null)
                totalWeight += rule.Weight;
        }

        if (totalWeight <= 0f)
            return rules[0];

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < rules.Count; i++)
        {
            RoomOptionRule rule = rules[i];
            if (rule == null)
                continue;

            roll -= rule.Weight;
            if (roll <= 0f)
                return rule;
        }

        return rules[rules.Count - 1];
    }

    /// <summary>
    /// 按战斗节点计算基础倍率成长。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <param name="baseMultiplier">基础倍率。</param>
    /// <param name="growthPerCombatNode">每节点成长。</param>
    /// <returns>成长后的倍率。</returns>
    private float CalculateDifficultyMultiplier(int combatNode, float baseMultiplier, float growthPerCombatNode)
    {
        int safeNode = Mathf.Max(1, combatNode);
        return Mathf.Max(0f, baseMultiplier + (safeNode - 1) * growthPerCombatNode);
    }
}

/// <summary>
/// 控制战斗结束后可生成的门组选项、适用节点范围和房间池抽取规则。
/// </summary>
[Serializable]
public sealed class RoomOptionRule
{
    [Header("适用范围")]
    [Tooltip("本规则适用的最小战斗节点，包含该节点。")]
    [SerializeField] private int _minCombatNode = 1;
    [Tooltip("本规则适用的最大战斗节点，包含该节点。")]
    [SerializeField] private int _maxCombatNode = 30;

    [Header("抽取规则")]
    [Tooltip("本规则在同一范围内参与随机抽取时使用的权重，小于 0 时按 0 处理。")]
    [SerializeField] private float _weight = 1f;
    [Tooltip("是否强制覆盖同范围内的普通门池。")]
    [SerializeField] private bool _forceOverride;
    [Tooltip("本规则最终生成的门数量；超过可用房间池时按可用数量生成。")]
    [SerializeField] private int _doorCount = 2;

    [Header("房间池")]
    [Tooltip("本规则可抽取的房间门选项池，需要引用 StageRoomOptionData 资产。")]
    [SerializeField] private RoomOptionPoolEntry[] _roomOptionPool = new RoomOptionPoolEntry[0];

    /// <summary>
    /// 最小适用战斗节点。
    /// </summary>
    public int MinCombatNode => Mathf.Max(1, _minCombatNode);

    /// <summary>
    /// 最大适用战斗节点。
    /// </summary>
    public int MaxCombatNode => Mathf.Max(MinCombatNode, _maxCombatNode);

    /// <summary>
    /// 随机抽取权重。
    /// </summary>
    public float Weight => Mathf.Max(0f, _weight);

    /// <summary>
    /// 是否强制覆盖普通门池。
    /// </summary>
    public bool ForceOverride => _forceOverride;

    /// <summary>
    /// 本规则最终生成的门数量。
    /// </summary>
    public int DoorCount => Mathf.Max(1, _doorCount);

    /// <summary>
    /// 本规则可抽取的房间门选项池。
    /// </summary>
    public RoomOptionPoolEntry[] RoomOptionPool => _roomOptionPool;

    /// <summary>
    /// 判断指定战斗节点是否落在本规则适用范围内。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <returns>适用时返回 true。</returns>
    public bool ContainsCombatNode(int combatNode)
    {
        return combatNode >= MinCombatNode && combatNode <= MaxCombatNode;
    }

    /// <summary>
    /// 从房间池中按权重生成最终门选项。
    /// </summary>
    /// <returns>生成的门选项数组；配置错误时返回空数组。</returns>
    public RoomOptionData[] CreateRoomOptions()
    {
        List<RoomOptionPoolEntry> candidates = BuildValidCandidatePool();
        if (candidates.Count <= 0)
        {
            Debug.LogError($"[Stage] 门规则 {MinCombatNode}-{MaxCombatNode} 没有配置有效的 StageRoomOptionData。");
            return Array.Empty<RoomOptionData>();
        }

        int count = Mathf.Min(DoorCount, candidates.Count);
        RoomOptionData[] options = new RoomOptionData[count];
        for (int i = 0; i < count; i++)
        {
            RoomOptionPoolEntry selected = SelectWeightedRoomOption(candidates);
            if (selected == null || selected.RoomOptionData == null)
                return Array.Empty<RoomOptionData>();

            options[i] = selected.RoomOptionData.CreateRuntimeOption();
            candidates.Remove(selected);
        }

        return options;
    }

    /// <summary>
    /// 收集当前规则中引用有效的房间门候选项。
    /// </summary>
    /// <returns>有效候选项列表。</returns>
    private List<RoomOptionPoolEntry> BuildValidCandidatePool()
    {
        List<RoomOptionPoolEntry> candidates = new List<RoomOptionPoolEntry>();
        if (_roomOptionPool == null)
            return candidates;

        for (int i = 0; i < _roomOptionPool.Length; i++)
        {
            RoomOptionPoolEntry entry = _roomOptionPool[i];
            if (entry != null && entry.RoomOptionData != null)
                candidates.Add(entry);
        }

        return candidates;
    }

    /// <summary>
    /// 从候选房间池中按权重选择一个房间门选项。
    /// </summary>
    /// <param name="candidates">候选房间池。</param>
    /// <returns>选中的房间池条目。</returns>
    private RoomOptionPoolEntry SelectWeightedRoomOption(List<RoomOptionPoolEntry> candidates)
    {
        if (candidates == null || candidates.Count <= 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += candidates[i] != null ? candidates[i].Weight : 0f;

        if (totalWeight <= 0f)
            return candidates[0];

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            RoomOptionPoolEntry entry = candidates[i];
            if (entry == null)
                continue;

            roll -= entry.Weight;
            if (roll <= 0f)
                return entry;
        }

        return candidates[candidates.Count - 1];
    }
}

/// <summary>
/// 定义门规则房间池中的一个可抽取房间选项及其权重。
/// </summary>
[Serializable]
public sealed class RoomOptionPoolEntry
{
    [Header("房间选项")]
    [Tooltip("可复用的房间门选项资产。")]
    [SerializeField] private StageRoomOptionData _roomOptionData;
    [Tooltip("该房间选项在所属门规则中的抽取权重，小于 0 时按 0 处理。")]
    [SerializeField] private float _weight = 1f;

    /// <summary>
    /// 可复用房间门选项资产。
    /// </summary>
    public StageRoomOptionData RoomOptionData => _roomOptionData;

    /// <summary>
    /// 抽取权重。
    /// </summary>
    public float Weight => Mathf.Max(0f, _weight);
}

/// <summary>
/// 定义一个战斗结束后可展示给玩家选择的房间入口。
/// </summary>
[Serializable]
public sealed class RoomOptionData
{
    [Header("房间")]
    [Tooltip("玩家选择该门后进入的房间类型。")]
    [SerializeField] private RoomType _roomType = RoomType.CombatNormal;
    [Tooltip("门选项在 Tooltip 或调试信息中显示的名称。")]
    [SerializeField] private string _displayName = "普通房";
    [Tooltip("玩家靠近门选项时显示的说明文本。")]
    [SerializeField] private string _tooltipDescription = "进入普通战斗房。";
    [Tooltip("门外观配置 ID，后续由场景门表现层映射到具体 Prefab 或样式。")]
    [SerializeField] private string _doorVisualId = "door_normal";
    [Tooltip("当前房间为中转房时，离开后要进入的目标战斗房类型。")]
    [SerializeField] private RoomType _transitExitTargetCombatRoomType = RoomType.CombatNormal;

    /// <summary>
    /// 创建默认普通战斗房门选项，供 Unity 序列化使用。
    /// </summary>
    public RoomOptionData()
    {
    }

    /// <summary>
    /// 创建运行时门选项数据，用于中转房离开门等不落资产的临时入口。
    /// </summary>
    /// <param name="roomType">目标房间类型。</param>
    /// <param name="displayName">门显示名称。</param>
    /// <param name="tooltipDescription">门提示说明。</param>
    /// <param name="doorVisualId">门外观配置 ID。</param>
    /// <param name="transitExitTargetCombatRoomType">中转房离开后的目标战斗房类型。</param>
    public RoomOptionData(
        RoomType roomType,
        string displayName,
        string tooltipDescription,
        string doorVisualId,
        RoomType transitExitTargetCombatRoomType)
    {
        _roomType = roomType;
        _displayName = string.IsNullOrWhiteSpace(displayName) ? roomType.ToString() : displayName;
        _tooltipDescription = string.IsNullOrWhiteSpace(tooltipDescription) ? _displayName : tooltipDescription;
        _doorVisualId = string.IsNullOrWhiteSpace(doorVisualId) ? "door_normal" : doorVisualId;
        _transitExitTargetCombatRoomType = transitExitTargetCombatRoomType;
    }

    /// <summary>
    /// 目标房间类型。
    /// </summary>
    public RoomType RoomType => _roomType;

    /// <summary>
    /// 门选项显示名称。
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// 门选项 Tooltip 描述。
    /// </summary>
    public string TooltipDescription => _tooltipDescription;

    /// <summary>
    /// 门外观 ID。
    /// </summary>
    public string DoorVisualId => _doorVisualId;

    /// <summary>
    /// 中转房离开后的目标战斗房类型。
    /// </summary>
    public RoomType TransitExitTargetCombatRoomType => _transitExitTargetCombatRoomType;
}

/// <summary>
/// 定义普通或精英战斗房的预算、属性和奖励倍率。
/// </summary>
[Serializable]
public sealed class CombatRoomProfile
{
    [Header("房间类型")]
    [Tooltip("该配置对应的战斗房类型。")]
    [SerializeField] private RoomType _roomType = RoomType.CombatNormal;

    [Header("倍率")]
    [Tooltip("该房间在基础生成预算上的额外倍率。")]
    [SerializeField] private float _budgetMultiplier = 1f;
    [Tooltip("该房间在难度曲线生命倍率上的额外倍率。")]
    [SerializeField] private float _healthMultiplier = 1f;
    [Tooltip("该房间在难度曲线伤害倍率上的额外倍率。")]
    [SerializeField] private float _damageMultiplier = 1f;
    [Tooltip("该房间在难度曲线速度倍率上的额外倍率。")]
    [SerializeField] private float _speedMultiplier = 1f;
    [Tooltip("该房间在金币、经验和分数奖励上的额外倍率。")]
    [SerializeField] private float _rewardMultiplier = 1f;

    [Header("完成奖励")]
    [Tooltip("完成该房间后是否生成额外奖励宝箱，精英房第一版建议启用。")]
    [SerializeField] private bool _spawnRewardChestOnComplete;

    /// <summary>
    /// 创建默认普通战斗房配置，供 Unity 序列化使用。
    /// </summary>
    public CombatRoomProfile()
    {
    }

    /// <summary>
    /// 创建指定战斗房类型的默认配置，便于 StageData 初始化普通房和精英房。
    /// </summary>
    /// <param name="roomType">战斗房类型。</param>
    public CombatRoomProfile(RoomType roomType)
    {
        _roomType = roomType;

        if (roomType != RoomType.CombatElite)
            return;

        _budgetMultiplier = 1.25f;
        _healthMultiplier = 1.15f;
        _damageMultiplier = 1.1f;
        _rewardMultiplier = 1.5f;
        _spawnRewardChestOnComplete = true;
    }

    /// <summary>
    /// 战斗房类型。
    /// </summary>
    public RoomType RoomType => _roomType;

    /// <summary>
    /// 生成预算倍率。
    /// </summary>
    public float BudgetMultiplier => Mathf.Max(0f, _budgetMultiplier);

    /// <summary>
    /// 生命倍率。
    /// </summary>
    public float HealthMultiplier => Mathf.Max(0f, _healthMultiplier);

    /// <summary>
    /// 伤害倍率。
    /// </summary>
    public float DamageMultiplier => Mathf.Max(0f, _damageMultiplier);

    /// <summary>
    /// 速度倍率。
    /// </summary>
    public float SpeedMultiplier => Mathf.Max(0f, _speedMultiplier);

    /// <summary>
    /// 奖励倍率。
    /// </summary>
    public float RewardMultiplier => Mathf.Max(0f, _rewardMultiplier);

    /// <summary>
    /// 完成后是否生成奖励宝箱。
    /// </summary>
    public bool SpawnRewardChestOnComplete => _spawnRewardChestOnComplete;
}

/// <summary>
/// 定义普通和精英战斗房使用的预算刷怪规则。
/// </summary>
[Serializable]
public sealed class EnemySpawnProfile
{
    [Header("预算")]
    [Tooltip("第一个战斗节点的基础生成预算。")]
    [SerializeField] private int _baseBudget = 12;
    [Tooltip("每推进一个战斗节点增加的生成预算。")]
    [SerializeField] private int _budgetGrowthPerCombatNode = 4;

    [Header("时间")]
    [Tooltip("普通战斗房和精英战斗房的默认持续时间。")]
    [SerializeField] private float _durationSeconds = 40f;
    [Tooltip("本层停止计划刷怪的时间点，应早于层持续时间。")]
    [SerializeField] private float _spawnCutoffSeconds = 35f;

    [Header("批次")]
    [Tooltip("每个刷怪批次的默认触发时间点，单位为秒。")]
    [SerializeField] private float[] _waveTimes = { 0f, 8f, 16f, 24f, 32f };
    [Tooltip("每个刷怪批次占总预算的比例，应与批次时间数量保持一致。")]
    [SerializeField] private float[] _waveBudgetRatios = { 0.15f, 0.2f, 0.2f, 0.2f, 0.25f };

    [Header("提前刷怪")]
    [Tooltip("场上敌人数量小于等于该阈值时，允许提前触发下一批刷怪。")]
    [SerializeField] private int _earlySpawnEnemyThreshold = 3;
    [Tooltip("两次提前刷怪之间的最小间隔，避免瞬时刷出过多敌人。")]
    [SerializeField] private float _earlySpawnMinIntervalSeconds = 2f;

    [Header("数量上限")]
    [Tooltip("场上敌人软上限，达到后后续批次可延后或减缓。")]
    [SerializeField] private int _softAliveLimit = 35;
    [Tooltip("场上敌人硬上限，达到后不再继续生成新敌人。")]
    [SerializeField] private int _hardAliveLimit = 50;

    [Header("敌人池")]
    [Tooltip("按战斗节点分段配置的敌人生成池。")]
    [SerializeField] private EnemyPoolSegment[] _enemyPoolSegments = new EnemyPoolSegment[0];

    /// <summary>
    /// 基础生成预算。
    /// </summary>
    public int BaseBudget => Mathf.Max(0, _baseBudget);

    /// <summary>
    /// 每个战斗节点的预算成长。
    /// </summary>
    public int BudgetGrowthPerCombatNode => Mathf.Max(0, _budgetGrowthPerCombatNode);

    /// <summary>
    /// 战斗房持续时间。
    /// </summary>
    public float DurationSeconds => Mathf.Max(1f, _durationSeconds);

    /// <summary>
    /// 刷怪截止时间。
    /// </summary>
    public float SpawnCutoffSeconds => Mathf.Clamp(_spawnCutoffSeconds, 0f, DurationSeconds);

    /// <summary>
    /// 刷怪批次触发时间点。
    /// </summary>
    public float[] WaveTimes => _waveTimes;

    /// <summary>
    /// 刷怪批次预算比例。
    /// </summary>
    public float[] WaveBudgetRatios => _waveBudgetRatios;

    /// <summary>
    /// 提前刷怪敌人阈值。
    /// </summary>
    public int EarlySpawnEnemyThreshold => Mathf.Max(0, _earlySpawnEnemyThreshold);

    /// <summary>
    /// 提前刷怪最小间隔。
    /// </summary>
    public float EarlySpawnMinIntervalSeconds => Mathf.Max(0f, _earlySpawnMinIntervalSeconds);

    /// <summary>
    /// 场上敌人软上限。
    /// </summary>
    public int SoftAliveLimit => Mathf.Max(0, _softAliveLimit);

    /// <summary>
    /// 场上敌人硬上限。
    /// </summary>
    public int HardAliveLimit => Mathf.Max(SoftAliveLimit, _hardAliveLimit);

    /// <summary>
    /// 敌人池分段列表。
    /// </summary>
    public EnemyPoolSegment[] EnemyPoolSegments => _enemyPoolSegments;
}

/// <summary>
/// 定义一段战斗节点范围内可使用的敌人生成池。
/// </summary>
[Serializable]
public sealed class EnemyPoolSegment
{
    [Header("适用范围")]
    [Tooltip("本敌人池分段适用的最小战斗节点，包含该节点。")]
    [SerializeField] private int _minCombatNode = 1;
    [Tooltip("本敌人池分段适用的最大战斗节点，包含该节点。")]
    [SerializeField] private int _maxCombatNode = 4;

    [Header("敌人权重")]
    [Tooltip("本分段可生成的敌人、生成成本和权重配置。")]
    [SerializeField] private EnemySpawnEntry[] _enemyEntries = new EnemySpawnEntry[0];

    /// <summary>
    /// 最小适用战斗节点。
    /// </summary>
    public int MinCombatNode => Mathf.Max(1, _minCombatNode);

    /// <summary>
    /// 最大适用战斗节点。
    /// </summary>
    public int MaxCombatNode => Mathf.Max(MinCombatNode, _maxCombatNode);

    /// <summary>
    /// 敌人生成条目列表。
    /// </summary>
    public EnemySpawnEntry[] EnemyEntries => _enemyEntries;

    /// <summary>
    /// 判断指定战斗节点是否落在本敌人池分段适用范围内。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <returns>适用时返回 true。</returns>
    public bool ContainsCombatNode(int combatNode)
    {
        return combatNode >= MinCombatNode && combatNode <= MaxCombatNode;
    }
}

/// <summary>
/// 定义敌人生成池中的单个敌人、预算成本和抽取权重。
/// </summary>
[Serializable]
public sealed class EnemySpawnEntry
{
    [Header("敌人")]
    [Tooltip("本条目生成的敌人数据。")]
    [SerializeField] private EnemyData _enemyData;
    [Tooltip("生成一个该敌人消耗的预算成本。")]
    [SerializeField] private int _spawnCost = 1;
    [Tooltip("该敌人在所属敌人池中的抽取权重。")]
    [SerializeField] private float _spawnWeight = 1f;

    /// <summary>
    /// 敌人数据。
    /// </summary>
    public EnemyData EnemyData => _enemyData;

    /// <summary>
    /// 生成成本。
    /// </summary>
    public int SpawnCost => Mathf.Max(1, _spawnCost);

    /// <summary>
    /// 生成权重。
    /// </summary>
    public float SpawnWeight => Mathf.Max(0f, _spawnWeight);
}

/// <summary>
/// 定义一个 Boss 在指定战斗节点中的敌人数据、直接奖励和技能列表。
/// </summary>
[Serializable]
public sealed class BossData
{
    [Header("基础信息")]
    [Tooltip("Boss 唯一标识，用于关卡配置、日志或挑战条件查找。")]
    [SerializeField] private string _bossId = "boss_01";
    [Tooltip("Boss 在 UI 或调试信息中显示的名称。")]
    [SerializeField] private string _displayName = "默认 Boss";
    [Tooltip("Boss 本体使用的敌人数据。")]
    [SerializeField] private EnemyData _bossEnemyData;
    [Tooltip("该 Boss 适用的房间节点，例如 15 或 30。")]
    [SerializeField] private int _combatNode = 15;
    [Tooltip("是否是最终 Boss；用于后续 Boss 血条样式、镜头或展示区分，不作为通关条件。")]
    [SerializeField] private bool _isFinalBoss;

    [Header("直接奖励")]
    [Tooltip("击杀 Boss 本体获得的固定分数。")]
    [SerializeField] private int _killScore = 500;
    [Tooltip("击杀 Boss 本体后直接给予玩家的金币数量。")]
    [SerializeField] private int _directRewardGold = 10;
    [Tooltip("击杀 Boss 本体后直接给予玩家的经验数量。")]
    [SerializeField] private int _directRewardExperience = 20;

    [Header("技能")]
    [Tooltip("Boss 可释放的技能配置列表，第一版优先支持召唤敌人。")]
    [SerializeField] private BossSkillData[] _skills = new BossSkillData[0];

    /// <summary>
    /// Boss 唯一标识。
    /// </summary>
    public string BossId => _bossId;

    /// <summary>
    /// Boss 显示名称。
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// Boss 本体敌人数据。
    /// </summary>
    public EnemyData BossEnemyData => _bossEnemyData;

    /// <summary>
    /// Boss 适用的战斗节点。
    /// </summary>
    public int CombatNode => Mathf.Max(1, _combatNode);

    /// <summary>
    /// 是否是最终 Boss。
    /// </summary>
    public bool IsFinalBoss => _isFinalBoss;

    /// <summary>
    /// Boss 本体击杀分数。
    /// </summary>
    public int KillScore => Mathf.Max(0, _killScore);

    /// <summary>
    /// Boss 直接奖励金币。
    /// </summary>
    public int DirectRewardGold => Mathf.Max(0, _directRewardGold);

    /// <summary>
    /// Boss 直接奖励经验。
    /// </summary>
    public int DirectRewardExperience => Mathf.Max(0, _directRewardExperience);

    /// <summary>
    /// Boss 技能列表。
    /// </summary>
    public BossSkillData[] Skills => _skills;
}

/// <summary>
/// 定义 Boss 技能的触发条件、释放节奏和召唤敌人效果。
/// </summary>
[Serializable]
public sealed class BossSkillData
{
    [Header("基础信息")]
    [Tooltip("Boss 技能唯一标识，用于运行时状态记录或调试。")]
    [SerializeField] private string _skillId = "boss_skill_01";
    [Tooltip("Boss 技能显示名称。")]
    [SerializeField] private string _displayName = "召唤小怪";
    [Tooltip("Boss 技能效果类型，第一版优先使用召唤敌人。")]
    [SerializeField] private BossSkillEffectType _effectType = BossSkillEffectType.SummonEnemies;

    [Header("触发条件")]
    [Tooltip("技能触发类型，例如开局、血量阈值、冷却或死亡。")]
    [SerializeField] private BossSkillTriggerType _triggerType = BossSkillTriggerType.OnCooldown;
    [Tooltip("冷却触发时使用的冷却时间，单位为秒。")]
    [SerializeField] private float _cooldownSeconds = 12f;
    [Tooltip("血量百分比触发阈值，取值 0 到 1，例如 0.5 表示 50% 血量。")]
    [SerializeField] private float _healthPercentThreshold = 0.5f;
    [Tooltip("该技能是否只允许触发一次，血量阈值技能通常启用。")]
    [SerializeField] private bool _triggerOnce = true;

    [Header("释放节奏")]
    [Tooltip("技能效果生效前的前摇时间，单位为秒。")]
    [SerializeField] private float _castLeadTimeSeconds;
    [Tooltip("技能效果生效后的后摇时间，单位为秒。")]
    [SerializeField] private float _castRecoveryTimeSeconds;
    [Tooltip("释放技能期间是否打断 Boss 移动。")]
    [SerializeField] private bool _interruptMovement;

    [Header("召唤")]
    [Tooltip("技能召唤的敌人列表，第一版用于 Boss 召唤物。")]
    [SerializeField] private BossSummonEntry[] _summonEnemies = new BossSummonEntry[0];
    [Tooltip("召唤物生成位置规则。")]
    [SerializeField] private BossSummonPositionRule _summonPositionRule = BossSummonPositionRule.AroundBoss;

    /// <summary>
    /// Boss 技能唯一标识。
    /// </summary>
    public string SkillId => _skillId;

    /// <summary>
    /// Boss 技能显示名称。
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// 技能效果类型。
    /// </summary>
    public BossSkillEffectType EffectType => _effectType;

    /// <summary>
    /// 技能触发类型。
    /// </summary>
    public BossSkillTriggerType TriggerType => _triggerType;

    /// <summary>
    /// 技能冷却时间。
    /// </summary>
    public float CooldownSeconds => Mathf.Max(0f, _cooldownSeconds);

    /// <summary>
    /// 血量百分比触发阈值。
    /// </summary>
    public float HealthPercentThreshold => Mathf.Clamp01(_healthPercentThreshold);

    /// <summary>
    /// 是否只触发一次。
    /// </summary>
    public bool TriggerOnce => _triggerOnce;

    /// <summary>
    /// 技能释放前摇。
    /// </summary>
    public float CastLeadTimeSeconds => Mathf.Max(0f, _castLeadTimeSeconds);

    /// <summary>
    /// 技能释放后摇。
    /// </summary>
    public float CastRecoveryTimeSeconds => Mathf.Max(0f, _castRecoveryTimeSeconds);

    /// <summary>
    /// 是否打断 Boss 移动。
    /// </summary>
    public bool InterruptMovement => _interruptMovement;

    /// <summary>
    /// 召唤敌人列表。
    /// </summary>
    public BossSummonEntry[] SummonEnemies => _summonEnemies;

    /// <summary>
    /// 召唤位置规则。
    /// </summary>
    public BossSummonPositionRule SummonPositionRule => _summonPositionRule;
}

/// <summary>
/// 定义 Boss 技能一次召唤的敌人类型和数量。
/// </summary>
[Serializable]
public sealed class BossSummonEntry
{
    [Header("召唤敌人")]
    [Tooltip("Boss 技能召唤的敌人数据。")]
    [SerializeField] private EnemyData _enemyData;
    [Tooltip("本技能一次释放时召唤该敌人的数量。")]
    [SerializeField] private int _count = 1;

    /// <summary>
    /// 召唤敌人数据。
    /// </summary>
    public EnemyData EnemyData => _enemyData;

    /// <summary>
    /// 召唤数量。
    /// </summary>
    public int Count => Mathf.Max(0, _count);
}

/// <summary>
/// 定义敌人属性、金币和经验随战斗节点增长的基础曲线。
/// </summary>
[Serializable]
public sealed class StageDifficultyCurve
{
    [Header("基础倍率")]
    [Tooltip("敌人生命基础倍率。")]
    [SerializeField] private float _baseHealthMultiplier = 1f;
    [Tooltip("敌人速度基础倍率。")]
    [SerializeField] private float _baseSpeedMultiplier = 1f;
    [Tooltip("敌人伤害基础倍率。")]
    [SerializeField] private float _baseDamageMultiplier = 1f;
    [Tooltip("敌人金币基础倍率。")]
    [SerializeField] private float _baseGoldMultiplier = 1f;
    [Tooltip("敌人经验基础倍率。")]
    [SerializeField] private float _baseExperienceMultiplier = 1f;

    [Header("每节点成长")]
    [Tooltip("每推进一个战斗节点增加的生命倍率。")]
    [SerializeField] private float _healthMultiplierGrowthPerCombatNode = 0.08f;
    [Tooltip("每推进一个战斗节点增加的速度倍率。")]
    [SerializeField] private float _speedMultiplierGrowthPerCombatNode = 0.01f;
    [Tooltip("每推进一个战斗节点增加的伤害倍率。")]
    [SerializeField] private float _damageMultiplierGrowthPerCombatNode = 0.03f;
    [Tooltip("每推进一个战斗节点增加的金币倍率。")]
    [SerializeField] private float _goldMultiplierGrowthPerCombatNode = 0.02f;
    [Tooltip("每推进一个战斗节点增加的经验倍率。")]
    [SerializeField] private float _experienceMultiplierGrowthPerCombatNode = 0.03f;

    /// <summary>
    /// 生命基础倍率。
    /// </summary>
    public float BaseHealthMultiplier => Mathf.Max(0f, _baseHealthMultiplier);

    /// <summary>
    /// 速度基础倍率。
    /// </summary>
    public float BaseSpeedMultiplier => Mathf.Max(0f, _baseSpeedMultiplier);

    /// <summary>
    /// 伤害基础倍率。
    /// </summary>
    public float BaseDamageMultiplier => Mathf.Max(0f, _baseDamageMultiplier);

    /// <summary>
    /// 金币基础倍率。
    /// </summary>
    public float BaseGoldMultiplier => Mathf.Max(0f, _baseGoldMultiplier);

    /// <summary>
    /// 经验基础倍率。
    /// </summary>
    public float BaseExperienceMultiplier => Mathf.Max(0f, _baseExperienceMultiplier);

    /// <summary>
    /// 每节点生命倍率成长。
    /// </summary>
    public float HealthMultiplierGrowthPerCombatNode => Mathf.Max(0f, _healthMultiplierGrowthPerCombatNode);

    /// <summary>
    /// 每节点速度倍率成长。
    /// </summary>
    public float SpeedMultiplierGrowthPerCombatNode => Mathf.Max(0f, _speedMultiplierGrowthPerCombatNode);

    /// <summary>
    /// 每节点伤害倍率成长。
    /// </summary>
    public float DamageMultiplierGrowthPerCombatNode => Mathf.Max(0f, _damageMultiplierGrowthPerCombatNode);

    /// <summary>
    /// 每节点金币倍率成长。
    /// </summary>
    public float GoldMultiplierGrowthPerCombatNode => Mathf.Max(0f, _goldMultiplierGrowthPerCombatNode);

    /// <summary>
    /// 每节点经验倍率成长。
    /// </summary>
    public float ExperienceMultiplierGrowthPerCombatNode => Mathf.Max(0f, _experienceMultiplierGrowthPerCombatNode);
}
