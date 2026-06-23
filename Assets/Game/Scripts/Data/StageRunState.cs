using System;
using UnityEngine;

/// <summary>
/// 保存单局关卡房间流程的运行时状态，供后续 StageData 驱动流程接入。
/// </summary>
[Serializable]
public sealed class StageRunState
{
    [Header("关卡进度")]
    [Tooltip("当前运行中的关卡 ID。")]
    [SerializeField] private string _currentStageId;
    [Tooltip("当前已进入或即将进入的战斗节点序号。")]
    [SerializeField] private int _currentCombatNode = 1;
    [Tooltip("当前所在房间类型。")]
    [SerializeField] private RoomType _currentRoomType = RoomType.CombatNormal;
    [Tooltip("中转房离开后要进入的目标战斗房类型。")]
    [SerializeField] private RoomType _transitExitTargetCombatRoomType = RoomType.CombatNormal;

    [Header("分数统计")]
    [Tooltip("当前总分。")]
    [SerializeField] private int _currentScore;
    [Tooltip("普通、精英和 Boss 召唤物击杀累计分。")]
    [SerializeField] private int _killScore;
    [Tooltip("普通和精英房清场奖励累计分。")]
    [SerializeField] private int _clearBonusScore;
    [Tooltip("普通和精英房速度奖励累计分。")]
    [SerializeField] private int _speedBonusScore;
    [Tooltip("普通、精英和 Boss 房无伤奖励累计分。")]
    [SerializeField] private int _noDamageBonusScore;
    [Tooltip("Boss 本体累计分。")]
    [SerializeField] private int _bossScore;
    [Tooltip("当前总击杀数。")]
    [SerializeField] private int _totalKillCount;
    [Tooltip("当前层击杀数。")]
    [SerializeField] private int _currentRoomKillCount;
    [Tooltip("当前层击杀得分。")]
    [SerializeField] private int _currentRoomKillScore;
    [Tooltip("已提前清场的普通或精英房数量。")]
    [SerializeField] private int _clearedRoomCount;
    [Tooltip("当前层受伤次数。")]
    [SerializeField] private int _currentRoomDamageTakenCount;
    [Tooltip("当前关卡总受伤次数。")]
    [SerializeField] private int _totalDamageTakenCount;
    [Tooltip("当前层是否提前清场。")]
    [SerializeField] private bool _currentRoomCleared;

    [Header("门选项")]
    [Tooltip("当前房间结束后已经生成或即将展示的门选项。")]
    [SerializeField] private RoomOptionData[] _currentRoomOptions = new RoomOptionData[0];
    [Tooltip("玩家已选择过的路线记录，用于后续统计或复盘。")]
    [SerializeField] private RouteChoiceRecord[] _routeChoiceRecords = new RouteChoiceRecord[0];

    [Header("Boss 统计")]
    [Tooltip("Boss 战开始时间，通常记录 Time.time。")]
    [SerializeField] private float _bossStartTime;
    [Tooltip("最近一次 Boss 击杀用时。")]
    [SerializeField] private float _bossKillDuration;
    [Tooltip("Boss 战期间受伤次数。")]
    [SerializeField] private int _bossDamageTakenCount;
    [Tooltip("Boss 战期间击杀召唤物数量。")]
    [SerializeField] private int _bossSummonKillCount;
    [Tooltip("Boss 战是否无伤完成。")]
    [SerializeField] private bool _bossNoDamage;
    [Tooltip("Boss 特殊挑战是否完成。")]
    [SerializeField] private bool _bossChallengeCompleted;

    /// <summary>
    /// 当前关卡 ID。
    /// </summary>
    public string CurrentStageId => _currentStageId;

    /// <summary>
    /// 当前战斗节点。
    /// </summary>
    public int CurrentCombatNode => Mathf.Max(1, _currentCombatNode);

    /// <summary>
    /// 当前房间类型。
    /// </summary>
    public RoomType CurrentRoomType => _currentRoomType;

    /// <summary>
    /// 中转房离开后的目标战斗房类型。
    /// </summary>
    public RoomType TransitExitTargetCombatRoomType => _transitExitTargetCombatRoomType;

    /// <summary>
    /// 当前总分。
    /// </summary>
    public int CurrentScore => Mathf.Max(0, _currentScore);

    /// <summary>
    /// 击杀累计分。
    /// </summary>
    public int KillScore => Mathf.Max(0, _killScore);

    /// <summary>
    /// 清场奖励累计分。
    /// </summary>
    public int ClearBonusScore => Mathf.Max(0, _clearBonusScore);

    /// <summary>
    /// 速度奖励累计分。
    /// </summary>
    public int SpeedBonusScore => Mathf.Max(0, _speedBonusScore);

    /// <summary>
    /// 无伤奖励累计分。
    /// </summary>
    public int NoDamageBonusScore => Mathf.Max(0, _noDamageBonusScore);

    /// <summary>
    /// Boss 本体累计分。
    /// </summary>
    public int BossScore => Mathf.Max(0, _bossScore);

    /// <summary>
    /// 总击杀数。
    /// </summary>
    public int TotalKillCount => Mathf.Max(0, _totalKillCount);

    /// <summary>
    /// 当前层击杀数。
    /// </summary>
    public int CurrentRoomKillCount => Mathf.Max(0, _currentRoomKillCount);

    /// <summary>
    /// 当前层击杀得分。
    /// </summary>
    public int CurrentRoomKillScore => Mathf.Max(0, _currentRoomKillScore);

    /// <summary>
    /// 已提前清场房间数。
    /// </summary>
    public int ClearedRoomCount => Mathf.Max(0, _clearedRoomCount);

    /// <summary>
    /// 当前层受伤次数。
    /// </summary>
    public int CurrentRoomDamageTakenCount => Mathf.Max(0, _currentRoomDamageTakenCount);

    /// <summary>
    /// 当前关卡总受伤次数。
    /// </summary>
    public int TotalDamageTakenCount => Mathf.Max(0, _totalDamageTakenCount);

    /// <summary>
    /// 当前层是否提前清场。
    /// </summary>
    public bool CurrentRoomCleared => _currentRoomCleared;

    /// <summary>
    /// 当前门选项。
    /// </summary>
    public RoomOptionData[] CurrentRoomOptions => _currentRoomOptions;

    /// <summary>
    /// 已选择路线记录。
    /// </summary>
    public RouteChoiceRecord[] RouteChoiceRecords => _routeChoiceRecords;

    /// <summary>
    /// Boss 战开始时间。
    /// </summary>
    public float BossStartTime => Mathf.Max(0f, _bossStartTime);

    /// <summary>
    /// Boss 击杀用时。
    /// </summary>
    public float BossKillDuration => Mathf.Max(0f, _bossKillDuration);

    /// <summary>
    /// Boss 战受伤次数。
    /// </summary>
    public int BossDamageTakenCount => Mathf.Max(0, _bossDamageTakenCount);

    /// <summary>
    /// Boss 召唤物击杀数。
    /// </summary>
    public int BossSummonKillCount => Mathf.Max(0, _bossSummonKillCount);

    /// <summary>
    /// Boss 战是否无伤。
    /// </summary>
    public bool BossNoDamage => _bossNoDamage;

    /// <summary>
    /// Boss 特殊挑战是否完成。
    /// </summary>
    public bool BossChallengeCompleted => _bossChallengeCompleted;

    /// <summary>
    /// 初始化关卡运行状态，后续接入 StageData 流程时由流程控制器调用。
    /// </summary>
    /// <param name="stageId">关卡 ID。</param>
    public void Initialize(string stageId)
    {
        _currentStageId = stageId;
        _currentCombatNode = 1;
        _currentRoomType = RoomType.CombatNormal;
        _transitExitTargetCombatRoomType = RoomType.CombatNormal;
        _currentScore = 0;
        _killScore = 0;
        _clearBonusScore = 0;
        _speedBonusScore = 0;
        _noDamageBonusScore = 0;
        _bossScore = 0;
        _totalKillCount = 0;
        _clearedRoomCount = 0;
        _totalDamageTakenCount = 0;
        ResetCurrentRoomStats();
        ResetBossStats();
        _currentRoomOptions = new RoomOptionData[0];
        _routeChoiceRecords = new RouteChoiceRecord[0];
    }

    /// <summary>
    /// 设置当前战斗节点，用于从存档或调试入口恢复进度。
    /// </summary>
    /// <param name="combatNode">战斗节点序号。</param>
    public void SetCurrentCombatNode(int combatNode)
    {
        _currentCombatNode = Mathf.Max(1, combatNode);
    }

    /// <summary>
    /// 推进到下一个战斗节点，并按最大节点数限制范围。
    /// </summary>
    /// <param name="maxCombatNode">当前关卡最大战斗节点。</param>
    public void AdvanceCombatNode(int maxCombatNode)
    {
        _currentCombatNode = Mathf.Min(Mathf.Max(1, maxCombatNode), _currentCombatNode + 1);
    }

    /// <summary>
    /// 设置当前房间类型。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    public void SetCurrentRoomType(RoomType roomType)
    {
        _currentRoomType = roomType;
    }

    /// <summary>
    /// 设置中转房离开后要进入的目标战斗房类型。
    /// </summary>
    /// <param name="roomType">目标战斗房类型。</param>
    public void SetTransitExitTargetCombatRoomType(RoomType roomType)
    {
        _transitExitTargetCombatRoomType = roomType;
    }

    /// <summary>
    /// 设置当前可选门列表。
    /// </summary>
    /// <param name="roomOptions">门选项列表。</param>
    public void SetCurrentRoomOptions(RoomOptionData[] roomOptions)
    {
        _currentRoomOptions = roomOptions ?? new RoomOptionData[0];
    }

    /// <summary>
    /// 记录一次路线选择，用于后续统计或复盘。
    /// </summary>
    /// <param name="roomOption">玩家选择的门选项。</param>
    public void RecordRouteChoice(RoomOptionData roomOption)
    {
        if (roomOption == null)
            return;

        RouteChoiceRecord record = new RouteChoiceRecord(_currentCombatNode, roomOption.RoomType, roomOption.DisplayName);
        if (_routeChoiceRecords == null)
            _routeChoiceRecords = new RouteChoiceRecord[0];

        int nextIndex = _routeChoiceRecords.Length;
        Array.Resize(ref _routeChoiceRecords, nextIndex + 1);
        _routeChoiceRecords[nextIndex] = record;
    }

    /// <summary>
    /// 累加分数。
    /// </summary>
    /// <param name="score">增加的分数。</param>
    public void AddScore(int score)
    {
        _currentScore += Mathf.Max(0, score);
    }

    /// <summary>
    /// 累加普通击杀分，并同步当前房间击杀得分。
    /// </summary>
    /// <param name="score">击杀得分。</param>
    public void AddKillScore(int score)
    {
        int safeScore = Mathf.Max(0, score);
        _killScore += safeScore;
        _currentRoomKillScore += safeScore;
        AddScore(safeScore);
    }

    /// <summary>
    /// 累加清场奖励分。
    /// </summary>
    /// <param name="score">清场奖励分。</param>
    public void AddClearBonusScore(int score)
    {
        int safeScore = Mathf.Max(0, score);
        _clearBonusScore += safeScore;
        AddScore(safeScore);
    }

    /// <summary>
    /// 累加速度奖励分。
    /// </summary>
    /// <param name="score">速度奖励分。</param>
    public void AddSpeedBonusScore(int score)
    {
        int safeScore = Mathf.Max(0, score);
        _speedBonusScore += safeScore;
        AddScore(safeScore);
    }

    /// <summary>
    /// 累加无伤奖励分。
    /// </summary>
    /// <param name="score">无伤奖励分。</param>
    public void AddNoDamageBonusScore(int score)
    {
        int safeScore = Mathf.Max(0, score);
        _noDamageBonusScore += safeScore;
        AddScore(safeScore);
    }

    /// <summary>
    /// 累加 Boss 本体分。
    /// </summary>
    /// <param name="score">Boss 本体得分。</param>
    public void AddBossScore(int score)
    {
        int safeScore = Mathf.Max(0, score);
        _bossScore += safeScore;
        AddScore(safeScore);
    }

    /// <summary>
    /// 记录一次当前房间击杀，并同步总击杀数。
    /// </summary>
    public void AddKill()
    {
        _currentRoomKillCount++;
        _totalKillCount++;
    }

    /// <summary>
    /// 记录当前房间一次受伤。
    /// </summary>
    public void AddCurrentRoomDamageTaken()
    {
        _currentRoomDamageTakenCount++;
        _totalDamageTakenCount++;
    }

    /// <summary>
    /// 设置当前房间是否提前清场。
    /// </summary>
    /// <param name="cleared">是否提前清场。</param>
    public void SetCurrentRoomCleared(bool cleared)
    {
        if (!_currentRoomCleared && cleared)
            _clearedRoomCount++;

        _currentRoomCleared = cleared;
    }

    /// <summary>
    /// 标记 Boss 战开始并重置 Boss 战统计。
    /// </summary>
    /// <param name="startTime">Boss 战开始时间。</param>
    public void BeginBoss(float startTime)
    {
        ResetBossStats();
        _bossStartTime = Mathf.Max(0f, startTime);
    }

    /// <summary>
    /// 标记 Boss 战完成并记录击杀用时。
    /// </summary>
    /// <param name="endTime">Boss 战结束时间。</param>
    public void CompleteBoss(float endTime)
    {
        _bossKillDuration = Mathf.Max(0f, endTime - _bossStartTime);
        _bossNoDamage = _bossDamageTakenCount <= 0;
    }

    /// <summary>
    /// 记录 Boss 战期间一次受伤。
    /// </summary>
    public void AddBossDamageTaken()
    {
        _bossDamageTakenCount++;
        _bossNoDamage = false;
        AddCurrentRoomDamageTaken();
    }

    /// <summary>
    /// 记录 Boss 战期间击杀一个召唤物。
    /// </summary>
    public void AddBossSummonKill()
    {
        _bossSummonKillCount++;
    }

    /// <summary>
    /// 设置 Boss 特殊挑战完成状态。
    /// </summary>
    /// <param name="completed">是否完成特殊挑战。</param>
    public void SetBossChallengeCompleted(bool completed)
    {
        _bossChallengeCompleted = completed;
    }

    /// <summary>
    /// 重置当前房间的击杀、受伤和清场统计。
    /// </summary>
    public void ResetCurrentRoomStats()
    {
        _currentRoomKillCount = 0;
        _currentRoomKillScore = 0;
        _currentRoomDamageTakenCount = 0;
        _currentRoomCleared = false;
    }

    /// <summary>
    /// 重置 Boss 战统计字段。
    /// </summary>
    public void ResetBossStats()
    {
        _bossStartTime = 0f;
        _bossKillDuration = 0f;
        _bossDamageTakenCount = 0;
        _bossSummonKillCount = 0;
        _bossNoDamage = false;
        _bossChallengeCompleted = false;
    }
}

/// <summary>
/// 记录玩家在某个战斗节点结束后选择的路线。
/// </summary>
[Serializable]
public sealed class RouteChoiceRecord
{
    [Header("路线记录")]
    [Tooltip("选择发生时对应的战斗节点。")]
    [SerializeField] private int _combatNode = 1;
    [Tooltip("玩家选择的房间类型。")]
    [SerializeField] private RoomType _selectedRoomType = RoomType.CombatNormal;
    [Tooltip("玩家选择的门显示名称。")]
    [SerializeField] private string _selectedOptionName;

    /// <summary>
    /// 创建默认路线记录，供 Unity 序列化使用。
    /// </summary>
    public RouteChoiceRecord()
    {
    }

    /// <summary>
    /// 创建指定战斗节点和房间类型的路线记录。
    /// </summary>
    /// <param name="combatNode">战斗节点。</param>
    /// <param name="selectedRoomType">选择的房间类型。</param>
    /// <param name="selectedOptionName">选择的门名称。</param>
    public RouteChoiceRecord(int combatNode, RoomType selectedRoomType, string selectedOptionName)
    {
        _combatNode = combatNode;
        _selectedRoomType = selectedRoomType;
        _selectedOptionName = selectedOptionName;
    }

    /// <summary>
    /// 选择发生时对应的战斗节点。
    /// </summary>
    public int CombatNode => Mathf.Max(1, _combatNode);

    /// <summary>
    /// 玩家选择的房间类型。
    /// </summary>
    public RoomType SelectedRoomType => _selectedRoomType;

    /// <summary>
    /// 玩家选择的门显示名称。
    /// </summary>
    public string SelectedOptionName => _selectedOptionName;
}
