using System;
using UnityEngine;

/// <summary>
/// 保存一个战斗房进入时预先生成的有限敌人刷怪计划。
/// </summary>
[Serializable]
public sealed class EnemySpawnPlan
{
    [Header("房间")]
    [Tooltip("本计划对应的战斗节点。")]
    [SerializeField] private int _combatNode = 1;
    [Tooltip("本计划对应的房间类型。")]
    [SerializeField] private RoomType _roomType = RoomType.CombatNormal;
    [Tooltip("本房间可使用的总刷怪预算。")]
    [SerializeField] private int _totalBudget;

    [Header("计划")]
    [Tooltip("按时间排序的刷怪批次计划。")]
    [SerializeField] private EnemySpawnWavePlan[] _waves = new EnemySpawnWavePlan[0];

    /// <summary>
    /// 创建默认刷怪计划，供 Unity 序列化使用。
    /// </summary>
    public EnemySpawnPlan()
    {
    }

    /// <summary>
    /// 创建指定战斗节点、房间类型、预算和批次的刷怪计划。
    /// </summary>
    /// <param name="combatNode">战斗节点。</param>
    /// <param name="roomType">房间类型。</param>
    /// <param name="totalBudget">总预算。</param>
    /// <param name="waves">刷怪批次。</param>
    public EnemySpawnPlan(int combatNode, RoomType roomType, int totalBudget, EnemySpawnWavePlan[] waves)
    {
        _combatNode = Mathf.Max(1, combatNode);
        _roomType = roomType;
        _totalBudget = Mathf.Max(0, totalBudget);
        _waves = waves ?? new EnemySpawnWavePlan[0];
    }

    /// <summary>
    /// 本计划对应的战斗节点。
    /// </summary>
    public int CombatNode => Mathf.Max(1, _combatNode);

    /// <summary>
    /// 本计划对应的房间类型。
    /// </summary>
    public RoomType RoomType => _roomType;

    /// <summary>
    /// 总刷怪预算。
    /// </summary>
    public int TotalBudget => Mathf.Max(0, _totalBudget);

    /// <summary>
    /// 刷怪批次计划。
    /// </summary>
    public EnemySpawnWavePlan[] Waves => _waves;

    /// <summary>
    /// 统计计划内所有敌人数量。
    /// </summary>
    /// <returns>计划敌人总数。</returns>
    public int CountTotalEnemies()
    {
        if (_waves == null)
            return 0;

        int total = 0;
        for (int i = 0; i < _waves.Length; i++)
        {
            EnemySpawnWavePlan wave = _waves[i];
            if (wave != null)
                total += wave.CountTotalEnemies();
        }

        return total;
    }
}

/// <summary>
/// 保存一个刷怪批次的时间点、预算和敌人条目。
/// </summary>
[Serializable]
public sealed class EnemySpawnWavePlan
{
    [Header("批次")]
    [Tooltip("本批次在房间开始后的计划触发时间。")]
    [SerializeField] private float _spawnTimeSeconds;
    [Tooltip("本批次分配到的预算。")]
    [SerializeField] private int _budget;
    [Tooltip("本批次实际使用的预算。")]
    [SerializeField] private int _usedBudget;
    [Tooltip("本批次计划生成的敌人条目。")]
    [SerializeField] private EnemySpawnPlanEntry[] _entries = new EnemySpawnPlanEntry[0];

    /// <summary>
    /// 创建默认批次计划，供 Unity 序列化使用。
    /// </summary>
    public EnemySpawnWavePlan()
    {
    }

    /// <summary>
    /// 创建指定时间、预算和敌人列表的批次计划。
    /// </summary>
    /// <param name="spawnTimeSeconds">触发时间。</param>
    /// <param name="budget">批次预算。</param>
    /// <param name="usedBudget">实际使用预算。</param>
    /// <param name="entries">敌人条目。</param>
    public EnemySpawnWavePlan(float spawnTimeSeconds, int budget, int usedBudget, EnemySpawnPlanEntry[] entries)
    {
        _spawnTimeSeconds = Mathf.Max(0f, spawnTimeSeconds);
        _budget = Mathf.Max(0, budget);
        _usedBudget = Mathf.Max(0, usedBudget);
        _entries = entries ?? new EnemySpawnPlanEntry[0];
    }

    /// <summary>
    /// 批次计划触发时间。
    /// </summary>
    public float SpawnTimeSeconds => Mathf.Max(0f, _spawnTimeSeconds);

    /// <summary>
    /// 批次分配预算。
    /// </summary>
    public int Budget => Mathf.Max(0, _budget);

    /// <summary>
    /// 批次实际使用预算。
    /// </summary>
    public int UsedBudget => Mathf.Max(0, _usedBudget);

    /// <summary>
    /// 批次敌人条目。
    /// </summary>
    public EnemySpawnPlanEntry[] Entries => _entries;

    /// <summary>
    /// 统计本批次计划敌人数量。
    /// </summary>
    /// <returns>敌人总数。</returns>
    public int CountTotalEnemies()
    {
        if (_entries == null)
            return 0;

        int total = 0;
        for (int i = 0; i < _entries.Length; i++)
        {
            EnemySpawnPlanEntry entry = _entries[i];
            if (entry != null)
                total += entry.Count;
        }

        return total;
    }
}

/// <summary>
/// 保存刷怪计划中的一种敌人和对应数量。
/// </summary>
[Serializable]
public sealed class EnemySpawnPlanEntry
{
    [Header("敌人")]
    [Tooltip("计划生成的敌人数据。")]
    [SerializeField] private EnemyData _enemyData;
    [Tooltip("计划生成数量。")]
    [SerializeField] private int _count;

    /// <summary>
    /// 创建默认计划条目，供 Unity 序列化使用。
    /// </summary>
    public EnemySpawnPlanEntry()
    {
    }

    /// <summary>
    /// 创建指定敌人和数量的计划条目。
    /// </summary>
    /// <param name="enemyData">敌人数据。</param>
    /// <param name="count">敌人数量。</param>
    public EnemySpawnPlanEntry(EnemyData enemyData, int count)
    {
        _enemyData = enemyData;
        _count = Mathf.Max(0, count);
    }

    /// <summary>
    /// 计划生成的敌人数据。
    /// </summary>
    public EnemyData EnemyData => _enemyData;

    /// <summary>
    /// 计划生成数量。
    /// </summary>
    public int Count => Mathf.Max(0, _count);
}
