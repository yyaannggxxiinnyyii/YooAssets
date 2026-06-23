using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 根据 StageData 的预算、批次比例和敌人池生成普通/精英战斗房的有限刷怪计划。
/// </summary>
public sealed class GameStageEnemySpawnPlanGenerator
{
    /// <summary>
    /// 为指定战斗节点和房间类型生成刷怪计划。
    /// </summary>
    /// <param name="stageData">关卡配置。</param>
    /// <param name="combatNode">战斗节点。</param>
    /// <param name="roomType">房间类型。</param>
    /// <returns>刷怪计划；配置无效时返回空计划。</returns>
    public EnemySpawnPlan CreatePlan(StageData stageData, int combatNode, RoomType roomType)
    {
        if (stageData == null || stageData.EnemySpawnProfile == null)
        {
            Debug.LogWarning("[StageSpawn] StageData 或 EnemySpawnProfile 为空，生成空刷怪计划。");
            return new EnemySpawnPlan(combatNode, roomType, 0, new EnemySpawnWavePlan[0]);
        }

        EnemyPoolSegment poolSegment = stageData.FindEnemyPoolSegment(combatNode);
        if (poolSegment == null || poolSegment.EnemyEntries == null || poolSegment.EnemyEntries.Length <= 0)
        {
            Debug.LogWarning($"[StageSpawn] 战斗节点 {combatNode} 未配置敌人池，生成空刷怪计划。");
            return new EnemySpawnPlan(combatNode, roomType, 0, new EnemySpawnWavePlan[0]);
        }

        int totalBudget = stageData.CalculateRoomSpawnBudget(combatNode, roomType);
        EnemySpawnProfile spawnProfile = stageData.EnemySpawnProfile;
        int[] waveBudgets = AllocateWaveBudgets(totalBudget, spawnProfile.WaveBudgetRatios);
        float[] waveTimes = spawnProfile.WaveTimes;
        EnemySpawnWavePlan[] waves = new EnemySpawnWavePlan[waveBudgets.Length];

        for (int i = 0; i < waveBudgets.Length; i++)
        {
            float spawnTime = GetWaveTime(waveTimes, i);
            waves[i] = CreateWavePlan(spawnTime, waveBudgets[i], poolSegment.EnemyEntries);
        }

        return new EnemySpawnPlan(combatNode, roomType, totalBudget, waves);
    }

    /// <summary>
    /// 按批次比例分配总预算，最后一个批次接收四舍五入后的剩余预算。
    /// </summary>
    /// <param name="totalBudget">总预算。</param>
    /// <param name="ratios">批次预算比例。</param>
    /// <returns>每个批次的预算。</returns>
    private int[] AllocateWaveBudgets(int totalBudget, float[] ratios)
    {
        int safeTotalBudget = Mathf.Max(0, totalBudget);
        if (safeTotalBudget <= 0)
            return new int[0];

        if (ratios == null || ratios.Length <= 0)
            return new[] { safeTotalBudget };

        int[] budgets = new int[ratios.Length];
        int allocated = 0;
        for (int i = 0; i < ratios.Length; i++)
        {
            if (i == ratios.Length - 1)
            {
                budgets[i] = Mathf.Max(0, safeTotalBudget - allocated);
                continue;
            }

            int budget = Mathf.Max(0, Mathf.RoundToInt(safeTotalBudget * Mathf.Max(0f, ratios[i])));
            budgets[i] = budget;
            allocated += budget;
        }

        return budgets;
    }

    /// <summary>
    /// 创建单个批次内的敌人生成计划。
    /// </summary>
    /// <param name="spawnTime">批次触发时间。</param>
    /// <param name="budget">批次预算。</param>
    /// <param name="enemyEntries">敌人池条目。</param>
    /// <returns>批次计划。</returns>
    private EnemySpawnWavePlan CreateWavePlan(float spawnTime, int budget, EnemySpawnEntry[] enemyEntries)
    {
        int remainingBudget = Mathf.Max(0, budget);
        int usedBudget = 0;
        List<EnemySpawnPlanEntryBuilder> builders = new List<EnemySpawnPlanEntryBuilder>();

        while (remainingBudget > 0)
        {
            EnemySpawnEntry selectedEntry = SelectEnemyEntry(enemyEntries, remainingBudget);
            if (selectedEntry == null || selectedEntry.EnemyData == null)
                break;

            AddEnemy(builders, selectedEntry.EnemyData);
            remainingBudget -= selectedEntry.SpawnCost;
            usedBudget += selectedEntry.SpawnCost;
        }

        return new EnemySpawnWavePlan(spawnTime, budget, usedBudget, BuildEntries(builders));
    }

    /// <summary>
    /// 在剩余预算范围内按权重选择一个敌人条目。
    /// </summary>
    /// <param name="enemyEntries">敌人池条目。</param>
    /// <param name="remainingBudget">剩余预算。</param>
    /// <returns>选中的敌人条目。</returns>
    private EnemySpawnEntry SelectEnemyEntry(EnemySpawnEntry[] enemyEntries, int remainingBudget)
    {
        if (enemyEntries == null || enemyEntries.Length <= 0 || remainingBudget <= 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < enemyEntries.Length; i++)
        {
            EnemySpawnEntry entry = enemyEntries[i];
            if (CanSelectEnemyEntry(entry, remainingBudget))
                totalWeight += entry.SpawnWeight;
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Random.Range(0f, totalWeight);
        for (int i = 0; i < enemyEntries.Length; i++)
        {
            EnemySpawnEntry entry = enemyEntries[i];
            if (!CanSelectEnemyEntry(entry, remainingBudget))
                continue;

            roll -= entry.SpawnWeight;
            if (roll <= 0f)
                return entry;
        }

        return null;
    }

    /// <summary>
    /// 判断敌人条目是否可在当前剩余预算下被选择。
    /// </summary>
    /// <param name="entry">敌人池条目。</param>
    /// <param name="remainingBudget">剩余预算。</param>
    /// <returns>可选择时返回 true。</returns>
    private bool CanSelectEnemyEntry(EnemySpawnEntry entry, int remainingBudget)
    {
        return entry != null &&
               entry.EnemyData != null &&
               entry.SpawnWeight > 0f &&
               entry.SpawnCost <= remainingBudget;
    }

    /// <summary>
    /// 累加指定敌人的计划数量。
    /// </summary>
    /// <param name="builders">计划条目构建列表。</param>
    /// <param name="enemyData">敌人数据。</param>
    private void AddEnemy(List<EnemySpawnPlanEntryBuilder> builders, EnemyData enemyData)
    {
        for (int i = 0; i < builders.Count; i++)
        {
            if (builders[i].EnemyData == enemyData)
            {
                builders[i].Count++;
                return;
            }
        }

        builders.Add(new EnemySpawnPlanEntryBuilder(enemyData));
    }

    /// <summary>
    /// 将临时构建列表转换为可序列化计划条目。
    /// </summary>
    /// <param name="builders">计划条目构建列表。</param>
    /// <returns>计划条目数组。</returns>
    private EnemySpawnPlanEntry[] BuildEntries(List<EnemySpawnPlanEntryBuilder> builders)
    {
        if (builders == null || builders.Count <= 0)
            return new EnemySpawnPlanEntry[0];

        EnemySpawnPlanEntry[] entries = new EnemySpawnPlanEntry[builders.Count];
        for (int i = 0; i < builders.Count; i++)
        {
            EnemySpawnPlanEntryBuilder builder = builders[i];
            entries[i] = new EnemySpawnPlanEntry(builder.EnemyData, builder.Count);
        }

        return entries;
    }

    /// <summary>
    /// 获取指定批次索引对应的计划触发时间。
    /// </summary>
    /// <param name="waveTimes">配置的批次时间。</param>
    /// <param name="waveIndex">批次索引。</param>
    /// <returns>批次触发时间。</returns>
    private float GetWaveTime(float[] waveTimes, int waveIndex)
    {
        if (waveTimes == null || waveTimes.Length <= 0)
            return 0f;

        if (waveIndex < waveTimes.Length)
            return Mathf.Max(0f, waveTimes[waveIndex]);

        return Mathf.Max(0f, waveTimes[waveTimes.Length - 1]);
    }

    /// <summary>
    /// 临时累加同一种敌人的计划数量。
    /// </summary>
    private sealed class EnemySpawnPlanEntryBuilder
    {
        public readonly EnemyData EnemyData;
        public int Count;

        /// <summary>
        /// 创建指定敌人的计划条目构建数据。
        /// </summary>
        /// <param name="enemyData">敌人数据。</param>
        public EnemySpawnPlanEntryBuilder(EnemyData enemyData)
        {
            EnemyData = enemyData;
            Count = 1;
        }
    }
}
