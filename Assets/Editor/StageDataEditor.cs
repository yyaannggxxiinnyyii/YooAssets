using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 为 StageData 提供 Inspector 内配置校验入口。
/// </summary>
[CustomEditor(typeof(StageData))]
public sealed class StageDataEditor : Editor
{
    /// <summary>
    /// 绘制 StageData 默认 Inspector，并追加配置校验按钮。
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("配置校验", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("校验 Stage 基础信息、Boss 节点、门规则覆盖和敌人池覆盖。工具只报告问题，不自动修改资产。", MessageType.Info);

        if (GUILayout.Button("校验当前 StageData"))
            ValidateCurrentStageData();

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 校验当前 Inspector 选中的 StageData，并弹出结果摘要。
    /// </summary>
    private void ValidateCurrentStageData()
    {
        StageData stageData = target as StageData;
        StageValidationResult result = StageDataValidator.Validate(stageData, true);
        StageDataValidationDialog.Show(result);
    }
}

/// <summary>
/// 提供 StageData 资源批量校验菜单入口。
/// </summary>
internal static class StageDataValidationMenu
{
    private const string ValidateAllMenuPath = "Tools/Stage/校验所有 StageData";
    private const string ValidateSelectedMenuPath = "Tools/Stage/校验选中的 StageData";

    /// <summary>
    /// 校验 Project 中选中的 StageData 资源。
    /// </summary>
    [MenuItem(ValidateSelectedMenuPath)]
    public static void ValidateSelectedStageData()
    {
        StageData[] selectedStageData = Selection.GetFiltered<StageData>(SelectionMode.Assets);
        if (selectedStageData == null || selectedStageData.Length <= 0)
        {
            EditorUtility.DisplayDialog("Stage 校验", "当前没有选中 StageData 资源。", "确定");
            return;
        }

        StageValidationBatchResult batchResult = new StageValidationBatchResult();
        for (int i = 0; i < selectedStageData.Length; i++)
            batchResult.Add(StageDataValidator.Validate(selectedStageData[i], true));

        StageDataValidationDialog.Show(batchResult);
    }

    /// <summary>
    /// 判断当前是否存在可校验的选中 StageData 资源。
    /// </summary>
    /// <returns>存在选中 StageData 时返回 true。</returns>
    [MenuItem(ValidateSelectedMenuPath, true)]
    public static bool CanValidateSelectedStageData()
    {
        StageData[] selectedStageData = Selection.GetFiltered<StageData>(SelectionMode.Assets);
        return selectedStageData != null && selectedStageData.Length > 0;
    }

    /// <summary>
    /// 扫描项目内全部 StageData 资源并逐个校验。
    /// </summary>
    [MenuItem(ValidateAllMenuPath)]
    public static void ValidateAllStageData()
    {
        string[] guids = AssetDatabase.FindAssets("t:StageData");
        if (guids == null || guids.Length <= 0)
        {
            EditorUtility.DisplayDialog("Stage 校验", "项目中没有找到 StageData 资源。", "确定");
            return;
        }

        StageValidationBatchResult batchResult = new StageValidationBatchResult();
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            StageData stageData = AssetDatabase.LoadAssetAtPath<StageData>(assetPath);
            batchResult.Add(StageDataValidator.Validate(stageData, true));
        }

        StageDataValidationDialog.Show(batchResult);
    }
}

/// <summary>
/// 执行 StageData 配置一致性校验。
/// </summary>
internal static class StageDataValidator
{
    /// <summary>
    /// 校验指定 StageData，并按需要把详细问题输出到 Console。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="logDetails">是否输出详细日志。</param>
    /// <returns>本次校验结果。</returns>
    public static StageValidationResult Validate(StageData stageData, bool logDetails)
    {
        StageValidationResult result = new StageValidationResult(stageData, logDetails);
        if (stageData == null)
        {
            result.AddError("StageData 为空，无法校验。", null);
            return result;
        }

        HashSet<int> bossNodes = ValidateBosses(stageData, result);
        HashSet<int> normalCombatNodes = new HashSet<int>();
        HashSet<int> eliteCombatNodes = new HashSet<int>();

        ValidateBasicInfo(stageData, result);
        ValidateRoomRules(stageData, bossNodes, normalCombatNodes, eliteCombatNodes, result);
        AddInitialCombatNode(stageData, bossNodes, normalCombatNodes);
        ValidateEnemySpawnProfile(stageData, normalCombatNodes, eliteCombatNodes, result);
        ValidateDifficultyCurve(stageData, result);

        result.LogSummary();
        return result;
    }

    /// <summary>
    /// 校验 Stage 基础字段。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="result">校验结果收集器。</param>
    private static void ValidateBasicInfo(StageData stageData, StageValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(stageData.StageId))
            result.AddError("StageId 为空。", stageData);

        if (string.IsNullOrWhiteSpace(stageData.DisplayName))
            result.AddWarning("DisplayName 为空，运行时调试和 UI 显示会缺少关卡名。", stageData);

        if (stageData.TotalCombatNodeCount <= 0)
            result.AddError("TotalCombatNodeCount 必须大于 0。", stageData);
    }

    /// <summary>
    /// 校验 Boss 节点、Boss 数据和 Boss 技能配置。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="result">校验结果收集器。</param>
    /// <returns>已配置 Boss 的节点集合。</returns>
    private static HashSet<int> ValidateBosses(StageData stageData, StageValidationResult result)
    {
        HashSet<int> bossNodes = new HashSet<int>();
        Dictionary<string, int> bossIdCounts = new Dictionary<string, int>();
        int finalBossCount = 0;
        BossData[] bosses = stageData.Bosses;

        if (bosses == null || bosses.Length <= 0)
        {
            result.AddWarning("Bosses 为空。本关不会出现 Boss 门，如果这是临时测试关可以忽略。", stageData);
            return bossNodes;
        }

        for (int i = 0; i < bosses.Length; i++)
        {
            BossData bossData = bosses[i];
            if (bossData == null)
            {
                result.AddError($"Bosses[{i}] 为空。", stageData);
                continue;
            }

            string bossLabel = GetBossLabel(bossData, i);
            if (bossData.CombatNode > stageData.TotalCombatNodeCount)
                result.AddError($"{bossLabel} 的 CombatNode={bossData.CombatNode} 超出 Stage 总节点数 {stageData.TotalCombatNodeCount}。", stageData);

            if (!bossNodes.Add(bossData.CombatNode))
                result.AddError($"{bossLabel} 的 CombatNode={bossData.CombatNode} 与其他 Boss 重复，同一节点只能配置一个 Boss。", stageData);

            if (bossData.CombatNode == 1)
                result.AddWarning($"{bossLabel} 配在 B1，关卡开局会直接进入 Boss 房。", stageData);

            if (string.IsNullOrWhiteSpace(bossData.BossId))
            {
                result.AddWarning($"{bossLabel} 的 BossId 为空。", stageData);
            }
            else
            {
                if (!bossIdCounts.ContainsKey(bossData.BossId))
                    bossIdCounts.Add(bossData.BossId, 0);

                bossIdCounts[bossData.BossId]++;
            }

            if (string.IsNullOrWhiteSpace(bossData.DisplayName))
                result.AddWarning($"{bossLabel} 的 DisplayName 为空。", stageData);

            if (bossData.BossEnemyData == null)
                result.AddError($"{bossLabel} 没有配置 BossEnemyData。", stageData);

            if (bossData.IsFinalBoss)
                finalBossCount++;

            ValidateBossSkills(bossData, bossLabel, result, stageData);
        }

        foreach (KeyValuePair<string, int> pair in bossIdCounts)
        {
            if (pair.Value > 1)
                result.AddWarning($"BossId={pair.Key} 重复配置了 {pair.Value} 次。", stageData);
        }

        if (finalBossCount <= 0)
            result.AddWarning("没有 BossData 标记 IsFinalBoss。当前通关不依赖最终 Boss，但后续 Boss 血条或展示区分可能缺少依据。", stageData);
        else if (finalBossCount > 1)
            result.AddWarning($"共有 {finalBossCount} 个 BossData 标记 IsFinalBoss，后续最终 Boss 表现可能出现歧义。", stageData);

        return bossNodes;
    }

    /// <summary>
    /// 校验单个 Boss 的技能和召唤物配置。
    /// </summary>
    /// <param name="bossData">Boss 配置。</param>
    /// <param name="bossLabel">日志中使用的 Boss 名称。</param>
    /// <param name="result">校验结果收集器。</param>
    /// <param name="context">日志上下文资源。</param>
    private static void ValidateBossSkills(BossData bossData, string bossLabel, StageValidationResult result, Object context)
    {
        BossSkillData[] skills = bossData.Skills;
        if (skills == null || skills.Length <= 0)
            return;

        Dictionary<string, int> skillIdCounts = new Dictionary<string, int>();
        for (int i = 0; i < skills.Length; i++)
        {
            BossSkillData skillData = skills[i];
            if (skillData == null)
            {
                result.AddError($"{bossLabel} Skills[{i}] 为空。", context);
                continue;
            }

            string skillLabel = string.IsNullOrWhiteSpace(skillData.DisplayName)
                ? $"{bossLabel}.Skills[{i}]"
                : $"{bossLabel}.{skillData.DisplayName}";

            if (string.IsNullOrWhiteSpace(skillData.SkillId))
            {
                result.AddWarning($"{skillLabel} 的 SkillId 为空。", context);
            }
            else
            {
                if (!skillIdCounts.ContainsKey(skillData.SkillId))
                    skillIdCounts.Add(skillData.SkillId, 0);

                skillIdCounts[skillData.SkillId]++;
            }

            if (skillData.TriggerType == BossSkillTriggerType.OnCooldown && skillData.CooldownSeconds <= 0f)
                result.AddWarning($"{skillLabel} 是冷却触发技能，但 CooldownSeconds <= 0，可能会过于频繁触发。", context);

            if (skillData.TriggerType == BossSkillTriggerType.OnHealthPercent &&
                (skillData.HealthPercentThreshold <= 0f || skillData.HealthPercentThreshold >= 1f))
            {
                result.AddWarning($"{skillLabel} 是血量阈值技能，HealthPercentThreshold 建议配置在 0 到 1 之间的中间值。", context);
            }

            if (skillData.EffectType == BossSkillEffectType.SummonEnemies)
                ValidateBossSummons(skillData, skillLabel, result, context);
        }

        foreach (KeyValuePair<string, int> pair in skillIdCounts)
        {
            if (pair.Value > 1)
                result.AddWarning($"{bossLabel} 的 SkillId={pair.Key} 重复配置了 {pair.Value} 次。", context);
        }
    }

    /// <summary>
    /// 校验 Boss 召唤技能中的召唤物条目。
    /// </summary>
    /// <param name="skillData">Boss 技能配置。</param>
    /// <param name="skillLabel">日志中使用的技能名称。</param>
    /// <param name="result">校验结果收集器。</param>
    /// <param name="context">日志上下文资源。</param>
    private static void ValidateBossSummons(BossSkillData skillData, string skillLabel, StageValidationResult result, Object context)
    {
        BossSummonEntry[] summonEntries = skillData.SummonEnemies;
        if (summonEntries == null || summonEntries.Length <= 0)
        {
            result.AddError($"{skillLabel} 是 SummonEnemies 技能，但没有配置 SummonEnemies。", context);
            return;
        }

        for (int i = 0; i < summonEntries.Length; i++)
        {
            BossSummonEntry summonEntry = summonEntries[i];
            if (summonEntry == null)
            {
                result.AddError($"{skillLabel}.SummonEnemies[{i}] 为空。", context);
                continue;
            }

            if (summonEntry.EnemyData == null)
                result.AddError($"{skillLabel}.SummonEnemies[{i}] 没有配置 EnemyData。", context);

            if (summonEntry.Count <= 0)
                result.AddWarning($"{skillLabel}.SummonEnemies[{i}] 的 Count <= 0，该条目不会产生召唤物。", context);
        }
    }

    /// <summary>
    /// 校验门规则覆盖，并收集可能进入的普通和精英战斗节点。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="bossNodes">Boss 节点集合。</param>
    /// <param name="normalCombatNodes">普通战斗节点收集结果。</param>
    /// <param name="eliteCombatNodes">精英战斗节点收集结果。</param>
    /// <param name="result">校验结果收集器。</param>
    private static void ValidateRoomRules(
        StageData stageData,
        HashSet<int> bossNodes,
        HashSet<int> normalCombatNodes,
        HashSet<int> eliteCombatNodes,
        StageValidationResult result)
    {
        RoomOptionRule[] rules = stageData.RoomOptionRules;
        HashSet<StageRoomOptionData> validatedRoomOptions = new HashSet<StageRoomOptionData>();

        if (stageData.TotalCombatNodeCount > 1 && (rules == null || rules.Length <= 0))
        {
            if (HasNonBossNodeAfterStart(stageData, bossNodes))
                result.AddError("RoomOptionRules 为空，非 Boss 的后续节点无法生成门。", stageData);
            return;
        }

        if (rules == null)
            return;

        for (int i = 0; i < rules.Length; i++)
            ValidateRoomRule(stageData, rules[i], i, bossNodes, validatedRoomOptions, result);

        for (int node = 2; node <= stageData.TotalCombatNodeCount; node++)
        {
            if (bossNodes.Contains(node))
                continue;

            ValidateRoomRuleCoverageAtNode(stageData, node, normalCombatNodes, eliteCombatNodes, result);
        }
    }

    /// <summary>
    /// 校验单条门规则本身的范围、权重和房间池。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="rule">门规则。</param>
    /// <param name="ruleIndex">规则索引。</param>
    /// <param name="bossNodes">Boss 节点集合。</param>
    /// <param name="validatedRoomOptions">已校验过的房间选项资产集合。</param>
    /// <param name="result">校验结果收集器。</param>
    private static void ValidateRoomRule(
        StageData stageData,
        RoomOptionRule rule,
        int ruleIndex,
        HashSet<int> bossNodes,
        HashSet<StageRoomOptionData> validatedRoomOptions,
        StageValidationResult result)
    {
        if (rule == null)
        {
            result.AddError($"RoomOptionRules[{ruleIndex}] 为空。", stageData);
            return;
        }

        string ruleLabel = GetRoomRuleLabel(ruleIndex, rule);
        if (rule.MinCombatNode > stageData.TotalCombatNodeCount)
            result.AddWarning($"{ruleLabel} 的最小节点超出 Stage 总节点数，该规则不会生效。", stageData);

        if (rule.MaxCombatNode > stageData.TotalCombatNodeCount)
            result.AddWarning($"{ruleLabel} 的最大节点超出 Stage 总节点数，超出部分不会被使用。", stageData);

        if (!RuleCanApplyToSelectableNonBossNode(stageData, rule, bossNodes))
            result.AddWarning($"{ruleLabel} 没有覆盖任何可选的非 Boss 目标节点。B1 不通过门规则进入，Boss 节点会被 BossData 强制覆盖。", stageData);

        if (rule.Weight <= 0f)
            result.AddWarning($"{ruleLabel} 的规则权重为 0。若同范围存在其他正权重规则，它通常不会被抽中。", stageData);

        RoomOptionPoolEntry[] pool = rule.RoomOptionPool;
        if (pool == null || pool.Length <= 0)
        {
            result.AddError($"{ruleLabel} 的 RoomOptionPool 为空。", stageData);
            return;
        }

        int validOptionCount = 0;
        int positiveWeightCount = 0;
        HashSet<StageRoomOptionData> seenOptions = new HashSet<StageRoomOptionData>();
        for (int i = 0; i < pool.Length; i++)
        {
            RoomOptionPoolEntry entry = pool[i];
            if (entry == null)
            {
                result.AddError($"{ruleLabel}.RoomOptionPool[{i}] 为空。", stageData);
                continue;
            }

            StageRoomOptionData optionData = entry.RoomOptionData;
            if (optionData == null)
            {
                result.AddError($"{ruleLabel}.RoomOptionPool[{i}] 没有引用 StageRoomOptionData。", stageData);
                continue;
            }

            validOptionCount++;
            if (entry.Weight > 0f)
                positiveWeightCount++;
            else
                result.AddWarning($"{ruleLabel}.RoomOptionPool[{i}] 的权重为 0，通常不会参与随机。", optionData);

            if (!seenOptions.Add(optionData))
                result.AddWarning($"{ruleLabel} 重复引用了 {GetRoomOptionAssetName(optionData)}。", optionData);

            if (validatedRoomOptions.Add(optionData))
                ValidateRoomOptionAsset(optionData, result);

            ValidateRoomOptionUsage(ruleLabel, i, optionData, result);
        }

        if (validOptionCount <= 0)
            result.AddError($"{ruleLabel} 没有任何有效 StageRoomOptionData。", stageData);

        if (rule.DoorCount > validOptionCount)
            result.AddWarning($"{ruleLabel} 的 DoorCount={rule.DoorCount} 大于有效房间池数量 {validOptionCount}，实际生成门数会减少。", stageData);

        if (validOptionCount > rule.DoorCount && positiveWeightCount <= 0)
            result.AddWarning($"{ruleLabel} 的有效房间池都为 0 权重，运行时会按列表顺序取前面的房间。", stageData);
    }

    /// <summary>
    /// 校验单个 StageRoomOptionData 资产的展示字段。
    /// </summary>
    /// <param name="optionData">房间选项资产。</param>
    /// <param name="result">校验结果收集器。</param>
    private static void ValidateRoomOptionAsset(StageRoomOptionData optionData, StageValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(optionData.DisplayName))
            result.AddWarning($"{GetRoomOptionAssetName(optionData)} 的 DisplayName 为空。", optionData);

        if (string.IsNullOrWhiteSpace(optionData.TooltipDescription))
            result.AddWarning($"{GetRoomOptionAssetName(optionData)} 的 TooltipDescription 为空，靠近门时说明会缺失。", optionData);

        if (string.IsNullOrWhiteSpace(optionData.DoorVisualId))
            result.AddWarning($"{GetRoomOptionAssetName(optionData)} 的 DoorVisualId 为空，门外观可能只能使用默认表现。", optionData);
    }

    /// <summary>
    /// 校验房间选项在门规则中的使用是否会导致运行时无效房间。
    /// </summary>
    /// <param name="ruleLabel">规则日志标签。</param>
    /// <param name="entryIndex">房间池条目索引。</param>
    /// <param name="optionData">房间选项资产。</param>
    /// <param name="result">校验结果收集器。</param>
    private static void ValidateRoomOptionUsage(string ruleLabel, int entryIndex, StageRoomOptionData optionData, StageValidationResult result)
    {
        string entryLabel = $"{ruleLabel}.RoomOptionPool[{entryIndex}]({GetRoomOptionAssetName(optionData)})";
        if (optionData.RoomType == RoomType.Boss)
        {
            result.AddError($"{entryLabel} 配置为 Boss。Boss 门应只由 BossData.CombatNode 自动生成，不要放进普通门池。", optionData);
            return;
        }

        if (!IsTransitRoom(optionData.RoomType))
            return;

        if (!IsCombatRoom(optionData.TransitExitTargetCombatRoomType))
        {
            result.AddError($"{entryLabel} 是中转房，但 TransitExitTargetCombatRoomType={optionData.TransitExitTargetCombatRoomType} 不是战斗房。", optionData);
            return;
        }

        if (optionData.TransitExitTargetCombatRoomType == RoomType.Boss)
            result.AddError($"{entryLabel} 的中转出口指向 Boss。Boss 房应由目标节点上的 BossData 强制决定。", optionData);
    }

    /// <summary>
    /// 校验指定目标节点是否存在可用门规则，并收集该节点可能进入的战斗类型。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="node">目标房间节点。</param>
    /// <param name="normalCombatNodes">普通战斗节点收集结果。</param>
    /// <param name="eliteCombatNodes">精英战斗节点收集结果。</param>
    /// <param name="result">校验结果收集器。</param>
    private static void ValidateRoomRuleCoverageAtNode(
        StageData stageData,
        int node,
        HashSet<int> normalCombatNodes,
        HashSet<int> eliteCombatNodes,
        StageValidationResult result)
    {
        RoomOptionRule[] matchingRules = stageData.GetRoomOptionRules(node);
        if (matchingRules == null || matchingRules.Length <= 0)
        {
            result.AddError($"B{node} 不是 Boss 节点，但没有任何 RoomOptionRule 覆盖。上一房间结束后无法生成门。", stageData);
            return;
        }

        List<RoomOptionRule> effectiveRules = GetEffectiveRules(matchingRules);
        if (effectiveRules.Count <= 0)
        {
            result.AddError($"B{node} 没有有效 RoomOptionRule。", stageData);
            return;
        }

        int forcedRuleCount = CountForcedRules(matchingRules);
        if (forcedRuleCount > 1)
            result.AddWarning($"B{node} 同时命中 {forcedRuleCount} 条 ForceOverride 门规则，运行时会在这些强制规则中继续按权重抽取。", stageData);

        if (effectiveRules.Count > 1 && AreAllRuleWeightsZero(effectiveRules))
            result.AddWarning($"B{node} 命中的有效门规则权重全为 0，运行时会按列表顺序选择第一条。", stageData);

        bool hasUsableOption = false;
        for (int i = 0; i < effectiveRules.Count; i++)
        {
            RoomOptionRule rule = effectiveRules[i];
            if (RuleHasUsableRoomOption(rule))
                hasUsableOption = true;

            CollectCombatNodesFromRule(node, rule, normalCombatNodes, eliteCombatNodes);
        }

        if (!hasUsableOption)
            result.AddError($"B{node} 命中的有效门规则没有任何可用房间选项。", stageData);
    }

    /// <summary>
    /// 将 B1 默认进入的战斗房加入敌人池覆盖检查。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="bossNodes">Boss 节点集合。</param>
    /// <param name="normalCombatNodes">普通战斗节点收集结果。</param>
    private static void AddInitialCombatNode(StageData stageData, HashSet<int> bossNodes, HashSet<int> normalCombatNodes)
    {
        if (stageData.TotalCombatNodeCount >= 1 && !bossNodes.Contains(1))
            normalCombatNodes.Add(1);
    }

    /// <summary>
    /// 校验敌人生成配置是否覆盖所有可能进入的普通和精英战斗节点。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="normalCombatNodes">普通战斗节点集合。</param>
    /// <param name="eliteCombatNodes">精英战斗节点集合。</param>
    /// <param name="result">校验结果收集器。</param>
    private static void ValidateEnemySpawnProfile(
        StageData stageData,
        HashSet<int> normalCombatNodes,
        HashSet<int> eliteCombatNodes,
        StageValidationResult result)
    {
        if ((normalCombatNodes == null || normalCombatNodes.Count <= 0) &&
            (eliteCombatNodes == null || eliteCombatNodes.Count <= 0))
        {
            result.AddWarning("没有检测到任何普通或精英战斗节点。若本关不是纯中转或纯 Boss 测试关，请检查门规则。", stageData);
            return;
        }

        EnemySpawnProfile spawnProfile = stageData.EnemySpawnProfile;
        if (spawnProfile == null)
        {
            result.AddError("存在普通或精英战斗节点，但 EnemySpawnProfile 为空。", stageData);
            return;
        }

        ValidateEnemySpawnTiming(spawnProfile, result, stageData);
        ValidateEnemyPoolSegments(stageData, spawnProfile, result);
        ValidateEnemyPoolCoverage(stageData, normalCombatNodes, RoomType.CombatNormal, result);
        ValidateEnemyPoolCoverage(stageData, eliteCombatNodes, RoomType.CombatElite, result);
    }

    /// <summary>
    /// 校验敌人生成的时间和批次配置。
    /// </summary>
    /// <param name="spawnProfile">敌人生成配置。</param>
    /// <param name="result">校验结果收集器。</param>
    /// <param name="context">日志上下文资源。</param>
    private static void ValidateEnemySpawnTiming(EnemySpawnProfile spawnProfile, StageValidationResult result, Object context)
    {
        if (spawnProfile.DurationSeconds <= 0f)
            result.AddError("EnemySpawnProfile.DurationSeconds 必须大于 0。", context);

        if (spawnProfile.SpawnCutoffSeconds >= spawnProfile.DurationSeconds)
            result.AddWarning("EnemySpawnProfile.SpawnCutoffSeconds 大于或等于 DurationSeconds，最后阶段不会提前停止刷怪。", context);

        if (spawnProfile.BaseBudget <= 0)
            result.AddWarning("EnemySpawnProfile.BaseBudget <= 0，前期战斗房可能没有敌人。", context);

        if (spawnProfile.WaveTimes == null || spawnProfile.WaveTimes.Length <= 0)
            result.AddWarning("EnemySpawnProfile.WaveTimes 为空，刷怪计划会退化为 0 秒批次。", context);

        if (spawnProfile.WaveBudgetRatios == null || spawnProfile.WaveBudgetRatios.Length <= 0)
            result.AddWarning("EnemySpawnProfile.WaveBudgetRatios 为空，全部预算会集中在一个批次。", context);

        if (spawnProfile.WaveTimes != null &&
            spawnProfile.WaveBudgetRatios != null &&
            spawnProfile.WaveTimes.Length != spawnProfile.WaveBudgetRatios.Length)
        {
            result.AddWarning($"EnemySpawnProfile 批次时间数量={spawnProfile.WaveTimes.Length}，预算比例数量={spawnProfile.WaveBudgetRatios.Length}，运行时会按预算比例数量生成批次。", context);
        }

        if (spawnProfile.SoftAliveLimit <= 0)
            result.AddWarning("EnemySpawnProfile.SoftAliveLimit <= 0，会导致提前批次逻辑缺少有效软上限。", context);
    }

    /// <summary>
    /// 校验敌人池分段本身的条目有效性。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="spawnProfile">敌人生成配置。</param>
    /// <param name="result">校验结果收集器。</param>
    private static void ValidateEnemyPoolSegments(StageData stageData, EnemySpawnProfile spawnProfile, StageValidationResult result)
    {
        EnemyPoolSegment[] segments = spawnProfile.EnemyPoolSegments;
        if (segments == null || segments.Length <= 0)
        {
            result.AddError("EnemySpawnProfile.EnemyPoolSegments 为空。", stageData);
            return;
        }

        for (int i = 0; i < segments.Length; i++)
            ValidateEnemyPoolSegment(stageData, segments[i], i, result);

        for (int node = 1; node <= stageData.TotalCombatNodeCount; node++)
        {
            int coveringCount = CountEnemyPoolSegmentsAtNode(segments, node);
            if (coveringCount > 1)
                result.AddWarning($"B{node} 被 {coveringCount} 个 EnemyPoolSegment 覆盖，运行时只会使用列表中第一个匹配分段。", stageData);
        }
    }

    /// <summary>
    /// 校验单个敌人池分段。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="segment">敌人池分段。</param>
    /// <param name="segmentIndex">分段索引。</param>
    /// <param name="result">校验结果收集器。</param>
    private static void ValidateEnemyPoolSegment(StageData stageData, EnemyPoolSegment segment, int segmentIndex, StageValidationResult result)
    {
        if (segment == null)
        {
            result.AddError($"EnemyPoolSegments[{segmentIndex}] 为空。", stageData);
            return;
        }

        string segmentLabel = $"EnemyPoolSegments[{segmentIndex}]({segment.MinCombatNode}-{segment.MaxCombatNode})";
        if (segment.MinCombatNode > stageData.TotalCombatNodeCount)
            result.AddWarning($"{segmentLabel} 的最小节点超出 Stage 总节点数，该分段不会生效。", stageData);

        if (segment.MaxCombatNode > stageData.TotalCombatNodeCount)
            result.AddWarning($"{segmentLabel} 的最大节点超出 Stage 总节点数，超出部分不会被使用。", stageData);

        EnemySpawnEntry[] entries = segment.EnemyEntries;
        if (entries == null || entries.Length <= 0)
        {
            result.AddError($"{segmentLabel} 没有配置 EnemyEntries。", stageData);
            return;
        }

        int selectableCount = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            EnemySpawnEntry entry = entries[i];
            if (entry == null)
            {
                result.AddError($"{segmentLabel}.EnemyEntries[{i}] 为空。", stageData);
                continue;
            }

            if (entry.EnemyData == null)
                result.AddError($"{segmentLabel}.EnemyEntries[{i}] 没有配置 EnemyData。", stageData);

            if (entry.SpawnWeight <= 0f)
                result.AddWarning($"{segmentLabel}.EnemyEntries[{i}] 的 SpawnWeight <= 0，通常不会参与随机。", stageData);

            if (entry.EnemyData != null && entry.SpawnWeight > 0f)
                selectableCount++;
        }

        if (selectableCount <= 0)
            result.AddError($"{segmentLabel} 没有任何带 EnemyData 且正权重的敌人条目。", stageData);
    }

    /// <summary>
    /// 校验指定房间类型的敌人池覆盖。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="combatNodes">需要敌人池的节点集合。</param>
    /// <param name="roomType">战斗房类型。</param>
    /// <param name="result">校验结果收集器。</param>
    private static void ValidateEnemyPoolCoverage(
        StageData stageData,
        HashSet<int> combatNodes,
        RoomType roomType,
        StageValidationResult result)
    {
        if (combatNodes == null)
            return;

        foreach (int node in combatNodes)
        {
            EnemyPoolSegment segment = stageData.FindEnemyPoolSegment(node);
            if (segment == null)
            {
                result.AddError($"B{node} 可能进入 {roomType}，但没有 EnemyPoolSegment 覆盖。", stageData);
                continue;
            }

            int roomBudget = stageData.CalculateRoomSpawnBudget(node, roomType);
            if (roomBudget <= 0)
            {
                result.AddWarning($"B{node} 的 {roomType} 生成预算为 0，战斗房可能没有敌人。", stageData);
                continue;
            }

            if (!SegmentHasSelectableEntryWithinBudget(segment, roomBudget))
                result.AddError($"B{node} 的 {roomType} 预算为 {roomBudget}，但首个匹配敌人池没有可在预算内抽取的正权重敌人。", stageData);
        }
    }

    /// <summary>
    /// 校验难度曲线中的明显异常值。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="result">校验结果收集器。</param>
    private static void ValidateDifficultyCurve(StageData stageData, StageValidationResult result)
    {
        StageDifficultyCurve curve = stageData.DifficultyCurve;
        if (curve == null)
        {
            result.AddError("DifficultyCurve 为空。", stageData);
            return;
        }

        if (curve.BaseHealthMultiplier <= 0f)
            result.AddWarning("DifficultyCurve.BaseHealthMultiplier <= 0，敌人生命可能异常。", stageData);

        if (curve.BaseDamageMultiplier <= 0f)
            result.AddWarning("DifficultyCurve.BaseDamageMultiplier <= 0，敌人伤害可能异常。", stageData);
    }

    /// <summary>
    /// 判断 Stage 在 B1 之后是否还有非 Boss 节点。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="bossNodes">Boss 节点集合。</param>
    /// <returns>存在非 Boss 后续节点时返回 true。</returns>
    private static bool HasNonBossNodeAfterStart(StageData stageData, HashSet<int> bossNodes)
    {
        for (int node = 2; node <= stageData.TotalCombatNodeCount; node++)
        {
            if (!bossNodes.Contains(node))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 判断门规则是否覆盖任何可由门进入的非 Boss 目标节点。
    /// </summary>
    /// <param name="stageData">待校验的 StageData。</param>
    /// <param name="rule">门规则。</param>
    /// <param name="bossNodes">Boss 节点集合。</param>
    /// <returns>覆盖有效节点时返回 true。</returns>
    private static bool RuleCanApplyToSelectableNonBossNode(StageData stageData, RoomOptionRule rule, HashSet<int> bossNodes)
    {
        int minNode = Mathf.Max(2, rule.MinCombatNode);
        int maxNode = Mathf.Min(stageData.TotalCombatNodeCount, rule.MaxCombatNode);
        for (int node = minNode; node <= maxNode; node++)
        {
            if (!bossNodes.Contains(node))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 获取某个节点实际会参与抽取的门规则集合。
    /// </summary>
    /// <param name="matchingRules">覆盖该节点的全部规则。</param>
    /// <returns>强制规则或普通规则集合。</returns>
    private static List<RoomOptionRule> GetEffectiveRules(RoomOptionRule[] matchingRules)
    {
        List<RoomOptionRule> forcedRules = new List<RoomOptionRule>();
        List<RoomOptionRule> normalRules = new List<RoomOptionRule>();
        for (int i = 0; i < matchingRules.Length; i++)
        {
            RoomOptionRule rule = matchingRules[i];
            if (rule == null)
                continue;

            if (rule.ForceOverride)
                forcedRules.Add(rule);
            else
                normalRules.Add(rule);
        }

        return forcedRules.Count > 0 ? forcedRules : normalRules;
    }

    /// <summary>
    /// 统计规则数组中的强制覆盖规则数量。
    /// </summary>
    /// <param name="rules">规则数组。</param>
    /// <returns>强制覆盖规则数量。</returns>
    private static int CountForcedRules(RoomOptionRule[] rules)
    {
        int count = 0;
        for (int i = 0; i < rules.Length; i++)
        {
            if (rules[i] != null && rules[i].ForceOverride)
                count++;
        }

        return count;
    }

    /// <summary>
    /// 判断规则列表的权重是否全部为 0。
    /// </summary>
    /// <param name="rules">规则列表。</param>
    /// <returns>全部为 0 时返回 true。</returns>
    private static bool AreAllRuleWeightsZero(List<RoomOptionRule> rules)
    {
        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i] != null && rules[i].Weight > 0f)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 判断门规则是否至少包含一个可进入的有效房间选项。
    /// </summary>
    /// <param name="rule">门规则。</param>
    /// <returns>存在有效房间选项时返回 true。</returns>
    private static bool RuleHasUsableRoomOption(RoomOptionRule rule)
    {
        if (rule == null || rule.RoomOptionPool == null)
            return false;

        RoomOptionPoolEntry[] pool = rule.RoomOptionPool;
        for (int i = 0; i < pool.Length; i++)
        {
            RoomOptionPoolEntry entry = pool[i];
            if (entry == null || entry.RoomOptionData == null)
                continue;

            StageRoomOptionData optionData = entry.RoomOptionData;
            if (optionData.RoomType == RoomType.Boss)
                continue;

            if (IsTransitRoom(optionData.RoomType) && !IsValidTransitExit(optionData.TransitExitTargetCombatRoomType))
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// 从门规则中收集某个节点可能进入的战斗房类型。
    /// </summary>
    /// <param name="node">目标房间节点。</param>
    /// <param name="rule">门规则。</param>
    /// <param name="normalCombatNodes">普通战斗节点集合。</param>
    /// <param name="eliteCombatNodes">精英战斗节点集合。</param>
    private static void CollectCombatNodesFromRule(
        int node,
        RoomOptionRule rule,
        HashSet<int> normalCombatNodes,
        HashSet<int> eliteCombatNodes)
    {
        if (rule == null || rule.RoomOptionPool == null)
            return;

        RoomOptionPoolEntry[] pool = rule.RoomOptionPool;
        for (int i = 0; i < pool.Length; i++)
        {
            RoomOptionPoolEntry entry = pool[i];
            if (entry == null || entry.RoomOptionData == null)
                continue;

            StageRoomOptionData optionData = entry.RoomOptionData;
            RoomType combatRoomType = GetRoomOptionCombatRoomType(optionData);
            if (combatRoomType == RoomType.CombatNormal)
                normalCombatNodes.Add(node);
            else if (combatRoomType == RoomType.CombatElite)
                eliteCombatNodes.Add(node);
        }
    }

    /// <summary>
    /// 获取房间选项最终需要检查敌人池的战斗房类型。
    /// </summary>
    /// <param name="optionData">房间选项资产。</param>
    /// <returns>普通或精英战斗房；无效时返回原房间类型。</returns>
    private static RoomType GetRoomOptionCombatRoomType(StageRoomOptionData optionData)
    {
        if (optionData.RoomType == RoomType.CombatNormal || optionData.RoomType == RoomType.CombatElite)
            return optionData.RoomType;

        if (IsTransitRoom(optionData.RoomType))
            return optionData.TransitExitTargetCombatRoomType;

        return optionData.RoomType;
    }

    /// <summary>
    /// 判断中转房出口是否为有效的非 Boss 战斗房。
    /// </summary>
    /// <param name="roomType">中转房出口房间类型。</param>
    /// <returns>有效时返回 true。</returns>
    private static bool IsValidTransitExit(RoomType roomType)
    {
        return roomType == RoomType.CombatNormal || roomType == RoomType.CombatElite;
    }

    /// <summary>
    /// 统计覆盖指定节点的敌人池分段数量。
    /// </summary>
    /// <param name="segments">敌人池分段数组。</param>
    /// <param name="node">节点编号。</param>
    /// <returns>覆盖数量。</returns>
    private static int CountEnemyPoolSegmentsAtNode(EnemyPoolSegment[] segments, int node)
    {
        int count = 0;
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] != null && segments[i].ContainsCombatNode(node))
                count++;
        }

        return count;
    }

    /// <summary>
    /// 判断敌人池中是否存在能在指定预算内被抽取的敌人。
    /// </summary>
    /// <param name="segment">敌人池分段。</param>
    /// <param name="budget">当前房间预算。</param>
    /// <returns>存在可抽取敌人时返回 true。</returns>
    private static bool SegmentHasSelectableEntryWithinBudget(EnemyPoolSegment segment, int budget)
    {
        if (segment == null || segment.EnemyEntries == null)
            return false;

        EnemySpawnEntry[] entries = segment.EnemyEntries;
        for (int i = 0; i < entries.Length; i++)
        {
            EnemySpawnEntry entry = entries[i];
            if (entry != null &&
                entry.EnemyData != null &&
                entry.SpawnWeight > 0f &&
                entry.SpawnCost <= budget)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断房间类型是否为战斗房。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>战斗房返回 true。</returns>
    private static bool IsCombatRoom(RoomType roomType)
    {
        return roomType == RoomType.CombatNormal || roomType == RoomType.CombatElite || roomType == RoomType.Boss;
    }

    /// <summary>
    /// 判断房间类型是否为中转房。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>中转房返回 true。</returns>
    private static bool IsTransitRoom(RoomType roomType)
    {
        return roomType == RoomType.Shop || roomType == RoomType.Event || roomType == RoomType.Treasure;
    }

    /// <summary>
    /// 获取 Boss 日志标签。
    /// </summary>
    /// <param name="bossData">Boss 配置。</param>
    /// <param name="index">Boss 数组索引。</param>
    /// <returns>日志标签。</returns>
    private static string GetBossLabel(BossData bossData, int index)
    {
        if (bossData == null)
            return $"Bosses[{index}]";

        if (!string.IsNullOrWhiteSpace(bossData.DisplayName))
            return $"Bosses[{index}]({bossData.DisplayName})";

        if (!string.IsNullOrWhiteSpace(bossData.BossId))
            return $"Bosses[{index}]({bossData.BossId})";

        return $"Bosses[{index}]";
    }

    /// <summary>
    /// 获取门规则日志标签。
    /// </summary>
    /// <param name="index">规则索引。</param>
    /// <param name="rule">门规则。</param>
    /// <returns>日志标签。</returns>
    private static string GetRoomRuleLabel(int index, RoomOptionRule rule)
    {
        if (rule == null)
            return $"RoomOptionRules[{index}]";

        return $"RoomOptionRules[{index}]({rule.MinCombatNode}-{rule.MaxCombatNode})";
    }

    /// <summary>
    /// 获取房间选项资产名。
    /// </summary>
    /// <param name="optionData">房间选项资产。</param>
    /// <returns>资产名。</returns>
    private static string GetRoomOptionAssetName(StageRoomOptionData optionData)
    {
        return optionData != null ? optionData.name : "Missing StageRoomOptionData";
    }
}

/// <summary>
/// 保存单个 StageData 校验结果。
/// </summary>
internal sealed class StageValidationResult
{
    private readonly StageData _stageData;
    private readonly bool _logDetails;

    /// <summary>
    /// 创建 StageData 校验结果收集器。
    /// </summary>
    /// <param name="stageData">被校验的 StageData。</param>
    /// <param name="logDetails">是否输出详细日志。</param>
    public StageValidationResult(StageData stageData, bool logDetails)
    {
        _stageData = stageData;
        _logDetails = logDetails;
    }

    /// <summary>
    /// 错误数量。
    /// </summary>
    public int ErrorCount { get; private set; }

    /// <summary>
    /// 警告数量。
    /// </summary>
    public int WarningCount { get; private set; }

    /// <summary>
    /// 被校验资源的显示名称。
    /// </summary>
    public string StageName => _stageData != null ? _stageData.name : "Missing StageData";

    /// <summary>
    /// 记录一个错误。
    /// </summary>
    /// <param name="message">错误内容。</param>
    /// <param name="context">日志上下文对象。</param>
    public void AddError(string message, Object context)
    {
        ErrorCount++;
        if (_logDetails)
            Debug.LogError($"[StageValidator] {StageName}: {message}", context);
    }

    /// <summary>
    /// 记录一个警告。
    /// </summary>
    /// <param name="message">警告内容。</param>
    /// <param name="context">日志上下文对象。</param>
    public void AddWarning(string message, Object context)
    {
        WarningCount++;
        if (_logDetails)
            Debug.LogWarning($"[StageValidator] {StageName}: {message}", context);
    }

    /// <summary>
    /// 输出本次校验汇总。
    /// </summary>
    public void LogSummary()
    {
        if (!_logDetails)
            return;

        string summary = $"[StageValidator] {StageName} 校验完成：{ErrorCount} 个错误，{WarningCount} 个警告。";
        if (ErrorCount > 0)
            Debug.LogError(summary, _stageData);
        else if (WarningCount > 0)
            Debug.LogWarning(summary, _stageData);
        else
            Debug.Log(summary, _stageData);
    }
}

/// <summary>
/// 保存批量 StageData 校验的汇总结果。
/// </summary>
internal sealed class StageValidationBatchResult
{
    /// <summary>
    /// 已校验 StageData 数量。
    /// </summary>
    public int StageCount { get; private set; }

    /// <summary>
    /// 错误总数。
    /// </summary>
    public int ErrorCount { get; private set; }

    /// <summary>
    /// 警告总数。
    /// </summary>
    public int WarningCount { get; private set; }

    /// <summary>
    /// 累加单个 StageData 校验结果。
    /// </summary>
    /// <param name="result">单个 StageData 校验结果。</param>
    public void Add(StageValidationResult result)
    {
        if (result == null)
            return;

        StageCount++;
        ErrorCount += result.ErrorCount;
        WarningCount += result.WarningCount;
    }
}

/// <summary>
/// 负责显示 StageData 校验结果对话框。
/// </summary>
internal static class StageDataValidationDialog
{
    /// <summary>
    /// 显示单个 StageData 校验结果。
    /// </summary>
    /// <param name="result">校验结果。</param>
    public static void Show(StageValidationResult result)
    {
        if (result == null)
            return;

        EditorUtility.DisplayDialog(
            "Stage 校验",
            $"{result.StageName} 校验完成。\n错误：{result.ErrorCount}\n警告：{result.WarningCount}\n详细信息已输出到 Console。",
            "确定");
    }

    /// <summary>
    /// 显示批量 StageData 校验结果。
    /// </summary>
    /// <param name="result">批量校验结果。</param>
    public static void Show(StageValidationBatchResult result)
    {
        if (result == null)
            return;

        EditorUtility.DisplayDialog(
            "Stage 校验",
            $"已校验 StageData：{result.StageCount}\n错误：{result.ErrorCount}\n警告：{result.WarningCount}\n详细信息已输出到 Console。",
            "确定");
    }
}
