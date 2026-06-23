using UnityEngine;

/// <summary>
/// 保存单局游戏的统计数据。
/// </summary>
[System.Serializable]
public sealed class RunStatistics
{
    [Header("战斗统计")]
    [SerializeField] private int totalKills;
    [SerializeField] private int score;
    [SerializeField] private int totalGoldCollected;
    [SerializeField] private int bestFloorReached = 1;
    [SerializeField] private float elapsedSeconds;

    /// <summary>
    /// 总击杀数。
    /// </summary>
    public int TotalKills => totalKills;

    /// <summary>
    /// 总分数。
    /// </summary>
    public int Score => score;

    /// <summary>
    /// 本局获得金币总数。
    /// </summary>
    public int TotalGoldCollected => totalGoldCollected;

    /// <summary>
    /// 到达的最高楼层。
    /// </summary>
    public int BestFloorReached => bestFloorReached;

    /// <summary>
    /// 游玩秒数。
    /// </summary>
    public float ElapsedSeconds => elapsedSeconds;

    /// <summary>
    /// 重置统计数据。
    /// </summary>
    public void Reset()
    {
        totalKills = 0;
        score = 0;
        totalGoldCollected = 0;
        bestFloorReached = 1;
        elapsedSeconds = 0f;
    }

    /// <summary>
    /// 记录一次击杀奖励。
    /// </summary>
    /// <param name="enemyScore">击杀得分。</param>
    public void AddKill(int enemyScore)
    {
        totalKills++;
        score += Mathf.Max(0, enemyScore);
    }

    /// <summary>
    /// 记录获得金币。
    /// </summary>
    /// <param name="value">金币数量。</param>
    public void AddGold(int value)
    {
        totalGoldCollected += Mathf.Max(0, value);
    }

    /// <summary>
    /// 记录到达楼层。
    /// </summary>
    /// <param name="floor">当前楼层。</param>
    public void SetBestFloorReached(int floor)
    {
        bestFloorReached = Mathf.Max(bestFloorReached, floor);
    }

    /// <summary>
    /// 累加游玩时间。
    /// </summary>
    /// <param name="deltaSeconds">增量秒数。</param>
    public void AddElapsedTime(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
            return;

        elapsedSeconds += deltaSeconds;
    }
}
