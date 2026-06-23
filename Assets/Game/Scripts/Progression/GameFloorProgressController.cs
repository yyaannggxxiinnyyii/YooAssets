using UnityEngine;

/// <summary>
/// 管理当前局内楼层序号、楼层计时、楼层数据选择和楼层结束后的流程推进。
/// </summary>
public sealed class GameFloorProgressController : MonoBehaviour
{
    private const int DefaultShopFloorInterval = 2;

    [Header("楼层配置")]
    [Tooltip("默认楼层持续时间，楼层数据未覆盖时使用。")]
    [SerializeField] private float floorDuration = 30f;
    [Tooltip("本局最大楼层。")]
    [SerializeField] private int maxFloor = 3;
    [Tooltip("每隔多少层进入一次商店。")]
    [SerializeField] private int shopFloorInterval = DefaultShopFloorInterval;

    [Header("楼层数据")]
    [Tooltip("按楼层顺序使用的楼层配置。超过数组长度时使用最后一个配置。")]
    [SerializeField] private FloorData[] floorDataList;

    private float _floorTimer;
    private int _currentFloor = 1;
    private int _bestFloorReached = 1;
    private FloorData _currentFloorData;
    private float _durationOverrideSeconds = -1f;

    /// <summary>
    /// 当前楼层序号。
    /// </summary>
    public int CurrentFloor => _currentFloor;

    /// <summary>
    /// 当前局内达到的最高楼层。
    /// </summary>
    public int BestFloorReached => _bestFloorReached;

    /// <summary>
    /// 当前楼层数据。
    /// </summary>
    public FloorData CurrentFloorData => _currentFloorData;

    /// <summary>
    /// 当前楼层或 Stage 战斗房已经经过的时间。
    /// </summary>
    public float CurrentFloorElapsedSeconds => Mathf.Max(0f, _floorTimer);

    /// <summary>
    /// 当前最大楼层数。
    /// </summary>
    public int MaxFloor => GetMaxFloor();

    /// <summary>
    /// 重置本局楼层进度。
    /// </summary>
    public void ResetRunProgress()
    {
        _currentFloor = 1;
        _bestFloorReached = 1;
        _floorTimer = 0f;
        _currentFloorData = null;
        _durationOverrideSeconds = -1f;
    }

    /// <summary>
    /// 开始第一层战斗。
    /// </summary>
    public void StartFirstFloor()
    {
        _currentFloor = 1;
        _bestFloorReached = 1;
        _floorTimer = 0f;
        _currentFloorData = GetFloorData(_currentFloor);
        _durationOverrideSeconds = -1f;
    }

    /// <summary>
    /// 进入下一层战斗。
    /// </summary>
    public void StartNextFloor()
    {
        _currentFloor++;
        _currentFloorData = GetFloorData(_currentFloor);
        _bestFloorReached = Mathf.Max(_bestFloorReached, _currentFloor);
        _floorTimer = 0f;
        _durationOverrideSeconds = -1f;
    }

    /// <summary>
    /// 重置当前楼层计时器，不改变旧楼层序号，用于 Stage 房间流程临时复用计时器。
    /// </summary>
    public void ResetCurrentFloorTimer()
    {
        _floorTimer = 0f;
    }

    /// <summary>
    /// 使用临时持续时间重置当前楼层计时器，主要用于 Stage 房间流程复用旧计时器。
    /// </summary>
    /// <param name="durationSeconds">本次计时持续时间。</param>
    public void ResetCurrentFloorTimer(float durationSeconds)
    {
        _durationOverrideSeconds = Mathf.Max(1f, durationSeconds);
        _floorTimer = 0f;
    }

    /// <summary>
    /// 清理临时持续时间覆盖，恢复 FloorData 或默认楼层持续时间。
    /// </summary>
    public void ClearDurationOverride()
    {
        _durationOverrideSeconds = -1f;
    }

    /// <summary>
    /// 推进当前楼层计时。
    /// </summary>
    /// <param name="deltaTime">本帧经过时间。</param>
    public void TickFloorTimer(float deltaTime)
    {
        _floorTimer += Mathf.Max(0f, deltaTime);
    }

    /// <summary>
    /// 判断当前楼层计时是否结束。
    /// </summary>
    /// <returns>当前楼层结束时返回 true。</returns>
    public bool IsCurrentFloorComplete()
    {
        return _floorTimer >= GetCurrentFloorDuration();
    }

    /// <summary>
    /// 获取当前楼层 HUD 显示文本。
    /// </summary>
    /// <returns>楼层和倒计时文本。</returns>
    public string GetHudFloorText()
    {
        float remainingSeconds = Mathf.Max(0f, GetCurrentFloorDuration() - _floorTimer);
        return $"{_currentFloor}/{GetMaxFloor()}  倒计时 {Mathf.CeilToInt(remainingSeconds)}s";
    }

    /// <summary>
    /// 获取商店标题文本。
    /// </summary>
    /// <returns>商店标题文本。</returns>
    public string GetShopTitleText()
    {
        return $"商店  楼层 {_currentFloor}/{GetMaxFloor()}";
    }

    /// <summary>
    /// 获取商店下一层按钮文本。
    /// </summary>
    /// <returns>下一步按钮文本。</returns>
    public string GetShopNextButtonText()
    {
        return IsFinalFloor() ? "完成挑战" : "下一层";
    }

    /// <summary>
    /// 获取结算页面的楼层进度文本。
    /// </summary>
    /// <returns>楼层进度文本。</returns>
    public string GetResultFloorProgressText()
    {
        return $"{_bestFloorReached}/{GetMaxFloor()}";
    }

    /// <summary>
    /// 根据待处理奖励和当前楼层进度决定下一个流程状态。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>下一个流程状态。</returns>
    public GameStateManager.GameState GetNextPostFloorState(GamePlayerController player)
    {
        if (player != null && player.PendingWeaponUpgradeChoices > 0)
            return GameStateManager.GameState.WeaponUpgrade;

        if (player != null && player.PendingUpgradeChoices > 0)
            return GameStateManager.GameState.LevelUp;

        if (IsFinalFloor())
            return GameStateManager.GameState.Victory;

        if (ShouldEnterShopAfterFloor())
            return GameStateManager.GameState.Shop;

        return GameStateManager.GameState.Playing;
    }

    /// <summary>
    /// 判断当前楼层是否已经是最终楼层。
    /// </summary>
    /// <returns>最终楼层时返回 true。</returns>
    public bool IsFinalFloor()
    {
        return _currentFloor >= GetMaxFloor();
    }

    /// <summary>
    /// 获取当前楼层持续时间。
    /// </summary>
    /// <returns>楼层持续秒数。</returns>
    private float GetCurrentFloorDuration()
    {
        if (_durationOverrideSeconds > 0f)
            return _durationOverrideSeconds;

        if (_currentFloorData != null)
            return Mathf.Max(1f, _currentFloorData.DurationSeconds);

        return Mathf.Max(1f, floorDuration);
    }

    /// <summary>
    /// 判断当前楼层结算后是否进入商店。
    /// </summary>
    /// <returns>需要进入商店时返回 true。</returns>
    private bool ShouldEnterShopAfterFloor()
    {
        int interval = Mathf.Max(1, shopFloorInterval);
        return _currentFloor % interval == 0;
    }

    /// <summary>
    /// 获取指定楼层的数据配置。
    /// </summary>
    /// <param name="floor">楼层序号，从 1 开始。</param>
    /// <returns>楼层配置；未配置时返回 null。</returns>
    private FloorData GetFloorData(int floor)
    {
        if (floorDataList == null || floorDataList.Length <= 0)
            return null;

        int index = Mathf.Clamp(floor - 1, 0, floorDataList.Length - 1);
        return floorDataList[index];
    }

    /// <summary>
    /// 获取当前最大楼层数。
    /// </summary>
    /// <returns>最大楼层数。</returns>
    private int GetMaxFloor()
    {
        if (floorDataList != null && floorDataList.Length > 0)
            return floorDataList.Length;

        return Mathf.Max(1, maxFloor);
    }
}
