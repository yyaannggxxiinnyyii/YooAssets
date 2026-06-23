using System;

/// <summary>
/// 功能房对象运行时上下文，向宝箱、回血机等场景功能对象提供玩家和完成回调。
/// </summary>
public readonly struct StageRoomFunctionContext
{
    private readonly Action _completedCallback;
    private readonly Action<int> _goldCollectedCallback;
    private readonly Action _hudRefreshCallback;

    /// <summary>
    /// 创建功能房对象运行时上下文。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="completedCallback">功能对象完成回调。</param>
    /// <param name="goldCollectedCallback">金币获得统计回调。</param>
    /// <param name="hudRefreshCallback">HUD 刷新回调。</param>
    public StageRoomFunctionContext(
        GamePlayerController player,
        Action completedCallback,
        Action<int> goldCollectedCallback,
        Action hudRefreshCallback)
    {
        Player = player;
        _completedCallback = completedCallback;
        _goldCollectedCallback = goldCollectedCallback;
        _hudRefreshCallback = hudRefreshCallback;
    }

    /// <summary>
    /// 当前玩家。
    /// </summary>
    public GamePlayerController Player { get; }

    /// <summary>
    /// 记录功能房中直接获得的金币。
    /// </summary>
    /// <param name="gold">金币数量。</param>
    public void AddGoldCollected(int gold)
    {
        if (gold > 0)
            _goldCollectedCallback?.Invoke(gold);
    }

    /// <summary>
    /// 请求刷新战斗 HUD。
    /// </summary>
    public void RefreshHud()
    {
        _hudRefreshCallback?.Invoke();
    }

    /// <summary>
    /// 通知功能对象已完成，流程层可在此后生成离开门。
    /// </summary>
    public void CompleteFunctionObject()
    {
        _completedCallback?.Invoke();
    }
}
