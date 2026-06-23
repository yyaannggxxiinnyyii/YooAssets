using System;
using UnityEngine;

/// <summary>
/// 管理 StageData 驱动的关卡房间进度，当前提供房间节点推进、门选项选择和中转房离开能力。
/// </summary>
public sealed class GameStageProgressController : MonoBehaviour
{
    [Header("关卡配置")]
    [Tooltip("当前关卡房间流程使用的主配置数据。")]
    [SerializeField] private StageData stageData;

    [Header("运行状态")]
    [Tooltip("当前关卡房间流程的运行时状态。")]
    [SerializeField] private StageRunState runState = new StageRunState();

    private RoomOptionRule _lastSelectedRoomOptionRule;
    private readonly GameStageEnemySpawnPlanGenerator _enemySpawnPlanGenerator = new GameStageEnemySpawnPlanGenerator();

    /// <summary>
    /// 玩家选择门选项后触发，参数为门选项索引和房间选项数据。
    /// </summary>
    public event Action<int, RoomOptionData> RoomOptionSelected;

    /// <summary>
    /// 当前关卡配置。
    /// </summary>
    public StageData StageData => stageData;

    /// <summary>
    /// 当前关卡运行状态。
    /// </summary>
    public StageRunState RunState => runState;

    /// <summary>
    /// 最近一次选中的门选项规则。
    /// </summary>
    public RoomOptionRule LastSelectedRoomOptionRule => _lastSelectedRoomOptionRule;

    /// <summary>
    /// 当前战斗节点。
    /// </summary>
    public int CurrentCombatNode => runState != null ? runState.CurrentCombatNode : 1;

    /// <summary>
    /// 当前房间类型。
    /// </summary>
    public RoomType CurrentRoomType => runState != null ? runState.CurrentRoomType : RoomType.CombatNormal;

    /// <summary>
    /// 当前关卡是否已配置有效 StageData。
    /// </summary>
    public bool HasStageData => stageData != null;

    /// <summary>
    /// 使用 Inspector 中配置的 StageData 开始关卡流程。
    /// </summary>
    public void StartStage()
    {
        StartStage(stageData);
    }

    /// <summary>
    /// 使用指定 StageData 开始关卡流程，并初始化运行状态。
    /// </summary>
    /// <param name="data">关卡配置。</param>
    public void StartStage(StageData data)
    {
        stageData = data;
        EnsureRunState();
        _lastSelectedRoomOptionRule = null;

        if (stageData == null)
        {
            Debug.LogWarning("[Stage] StageData 为空，无法开始关卡房间流程。");
            runState.Initialize(string.Empty);
            return;
        }

        runState.Initialize(stageData.StageId);
        runState.SetCurrentRoomType(GetDefaultCombatRoomTypeForNode(runState.CurrentCombatNode));
    }

    /// <summary>
    /// 重置当前关卡流程状态，不清空 StageData 引用。
    /// </summary>
    public void ResetStageProgress()
    {
        EnsureRunState();
        _lastSelectedRoomOptionRule = null;
        runState.Initialize(stageData != null ? stageData.StageId : string.Empty);
    }

    /// <summary>
    /// 为下一个房间节点生成当前房间结束后的门选项。
    /// </summary>
    /// <returns>生成的门选项列表；没有可用规则时返回空数组。</returns>
    public RoomOptionData[] RollCurrentRoomOptions()
    {
        EnsureRunState();
        if (stageData == null)
        {
            Debug.LogWarning("[Stage] StageData 为空，无法生成门选项。");
            runState.SetCurrentRoomOptions(new RoomOptionData[0]);
            return runState.CurrentRoomOptions;
        }

        if (IsStageComplete())
        {
            _lastSelectedRoomOptionRule = null;
            runState.SetCurrentRoomOptions(new RoomOptionData[0]);
            return runState.CurrentRoomOptions;
        }

        int targetCombatNode = GetNextCombatNode();
        _lastSelectedRoomOptionRule = stageData.IsBossCombatNode(targetCombatNode)
            ? null
            : stageData.SelectRoomOptionRule(targetCombatNode);
        RoomOptionData[] options = stageData.CreateRoomOptionsForRule(targetCombatNode, _lastSelectedRoomOptionRule);
        runState.SetCurrentRoomOptions(options);
        return runState.CurrentRoomOptions;
    }

    /// <summary>
    /// 选择当前门选项中的一个房间，并按房间类型推进状态。
    /// </summary>
    /// <param name="optionIndex">门选项索引。</param>
    /// <returns>选择成功时返回 true。</returns>
    public bool TryChooseCurrentRoomOption(int optionIndex)
    {
        EnsureRunState();
        RoomOptionData[] options = runState.CurrentRoomOptions;
        if (options == null || optionIndex < 0 || optionIndex >= options.Length)
            return false;

        return TryChooseRoomOption(optionIndex, options[optionIndex]);
    }

    /// <summary>
    /// 选择指定门选项，并推进到下一个关卡房间节点。
    /// </summary>
    /// <param name="roomOption">门选项。</param>
    /// <returns>选择成功时返回 true。</returns>
    public bool TryChooseRoomOption(RoomOptionData roomOption)
    {
        return TryChooseRoomOption(-1, roomOption);
    }

    /// <summary>
    /// 选择指定门选项，并保留门索引用于外部监听。
    /// </summary>
    /// <param name="optionIndex">门选项索引；未知时为 -1。</param>
    /// <param name="roomOption">门选项。</param>
    /// <returns>选择成功时返回 true。</returns>
    public bool TryChooseRoomOption(int optionIndex, RoomOptionData roomOption)
    {
        EnsureRunState();
        if (roomOption == null)
            return false;

        runState.RecordRouteChoice(roomOption);
        AdvanceToRoomOption(roomOption);

        runState.SetCurrentRoomOptions(new RoomOptionData[0]);
        RoomOptionSelected?.Invoke(optionIndex, roomOption);
        return true;
    }

    /// <summary>
    /// 中转房离开时进入绑定的目标战斗房。
    /// </summary>
    public void ExitTransitRoomToTargetCombatRoom()
    {
        EnsureRunState();
        EnterCombatRoom(runState.TransitExitTargetCombatRoomType);
    }

    /// <summary>
    /// 标记当前战斗房完成，并生成下一组门选项。
    /// </summary>
    /// <returns>生成的门选项列表。</returns>
    public RoomOptionData[] CompleteCurrentCombatRoom()
    {
        EnsureRunState();
        return RollCurrentRoomOptions();
    }

    /// <summary>
    /// 判断当前关卡是否已经完成全部房间节点。
    /// </summary>
    /// <returns>当前节点达到总房间数时返回 true。</returns>
    public bool IsStageComplete()
    {
        return stageData != null && CurrentCombatNode >= stageData.TotalCombatNodeCount;
    }

    /// <summary>
    /// 判断当前中转房离开后是否应直接通关。
    /// </summary>
    /// <returns>当前节点已是最终房间节点时返回 true。</returns>
    public bool ShouldCompleteStageAfterTransitExit()
    {
        return IsStageComplete();
    }

    /// <summary>
    /// 判断当前战斗节点是否为 Boss 节点。
    /// </summary>
    /// <returns>Boss 节点返回 true。</returns>
    public bool IsCurrentBossNode()
    {
        return stageData != null && stageData.IsBossCombatNode(CurrentCombatNode);
    }

    /// <summary>
    /// 获取当前战斗节点对应的 Boss 配置。
    /// </summary>
    /// <returns>Boss 配置；没有匹配时返回 null。</returns>
    public BossData GetCurrentBossData()
    {
        return stageData != null ? stageData.FindBossData(CurrentCombatNode) : null;
    }

    /// <summary>
    /// 获取当前战斗节点对应的敌人池分段。
    /// </summary>
    /// <returns>敌人池分段；没有匹配时返回 null。</returns>
    public EnemyPoolSegment GetCurrentEnemyPoolSegment()
    {
        return stageData != null ? stageData.FindEnemyPoolSegment(CurrentCombatNode) : null;
    }

    /// <summary>
    /// 获取当前战斗节点和房间类型对应的刷怪预算。
    /// </summary>
    /// <returns>当前房间刷怪预算。</returns>
    public int GetCurrentRoomSpawnBudget()
    {
        return stageData != null
            ? stageData.CalculateRoomSpawnBudget(CurrentCombatNode, CurrentRoomType)
            : 0;
    }

    /// <summary>
    /// 为当前普通或精英战斗房创建固定预算刷怪计划。
    /// </summary>
    /// <returns>当前房间刷怪计划；非普通/精英战斗房返回空计划。</returns>
    public EnemySpawnPlan CreateCurrentEnemySpawnPlan()
    {
        if (CurrentRoomType != RoomType.CombatNormal && CurrentRoomType != RoomType.CombatElite)
            return new EnemySpawnPlan(CurrentCombatNode, CurrentRoomType, 0, new EnemySpawnWavePlan[0]);

        return _enemySpawnPlanGenerator.CreatePlan(stageData, CurrentCombatNode, CurrentRoomType);
    }

    /// <summary>
    /// 推进到门选项代表的下一个房间节点，并处理 Boss 节点强制覆盖。
    /// </summary>
    /// <param name="roomOption">玩家选择的门选项。</param>
    private void AdvanceToRoomOption(RoomOptionData roomOption)
    {
        if (stageData == null)
        {
            runState.SetCurrentRoomType(roomOption.RoomType);
            runState.SetTransitExitTargetCombatRoomType(roomOption.TransitExitTargetCombatRoomType);
            return;
        }

        AdvanceCombatNode();
        RoomType resolvedRoomType = stageData.IsBossCombatNode(runState.CurrentCombatNode)
            ? RoomType.Boss
            : roomOption.RoomType;

        runState.SetCurrentRoomType(resolvedRoomType);
        runState.SetTransitExitTargetCombatRoomType(roomOption.TransitExitTargetCombatRoomType);
        runState.ResetCurrentRoomStats();
    }

    /// <summary>
    /// 进入当前节点上的指定战斗房，Boss 节点会强制使用 Boss 房。
    /// </summary>
    /// <param name="roomType">战斗房类型。</param>
    private void EnterCombatRoom(RoomType roomType)
    {
        if (stageData == null)
        {
            runState.SetCurrentRoomType(roomType);
            return;
        }

        if (!IsCombatRoom(roomType))
            roomType = GetDefaultCombatRoomTypeForNode(runState.CurrentCombatNode);

        RoomType resolvedRoomType = stageData.IsBossCombatNode(runState.CurrentCombatNode)
            ? RoomType.Boss
            : roomType;
        runState.SetCurrentRoomType(resolvedRoomType);
        runState.ResetCurrentRoomStats();
    }

    /// <summary>
    /// 推进一个关卡房间节点，所有房间类型都会占用节点。
    /// </summary>
    private void AdvanceCombatNode()
    {
        if (stageData != null)
            runState.AdvanceCombatNode(stageData.TotalCombatNodeCount);
    }

    /// <summary>
    /// 获取下一次选门会进入的房间节点。
    /// </summary>
    /// <returns>下一房间节点序号。</returns>
    private int GetNextCombatNode()
    {
        if (stageData == null)
            return CurrentCombatNode;

        return Mathf.Min(stageData.TotalCombatNodeCount, CurrentCombatNode + 1);
    }

    /// <summary>
    /// 获取指定战斗节点默认应该进入的战斗房类型。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    /// <returns>默认战斗房类型。</returns>
    private RoomType GetDefaultCombatRoomTypeForNode(int combatNode)
    {
        if (stageData != null && stageData.IsBossCombatNode(combatNode))
            return RoomType.Boss;

        return RoomType.CombatNormal;
    }

    /// <summary>
    /// 判断房间类型是否为战斗房。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>战斗房返回 true。</returns>
    private bool IsCombatRoom(RoomType roomType)
    {
        return roomType == RoomType.CombatNormal || roomType == RoomType.CombatElite || roomType == RoomType.Boss;
    }

    /// <summary>
    /// 判断房间类型是否为中转房。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>中转房返回 true。</returns>
    private bool IsTransitRoom(RoomType roomType)
    {
        return roomType == RoomType.Shop || roomType == RoomType.Event || roomType == RoomType.Treasure;
    }

    /// <summary>
    /// 确保运行状态对象存在。
    /// </summary>
    private void EnsureRunState()
    {
        if (runState == null)
            runState = new StageRunState();
    }
}
