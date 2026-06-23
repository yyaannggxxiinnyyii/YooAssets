using UnityEngine;

/// <summary>
/// 定义单个楼层的目标和敌人强度配置。
/// </summary>
[CreateAssetMenu(fileName = "FloorData", menuName = "Game/Data/Floor Data")]
public sealed class FloorData : ScriptableObject
{
    [Header("基础信息")]
    [SerializeField] private string floorId = "floor_01";
    [SerializeField] private int floorIndex = 1;

    [Header("目标")]
    [SerializeField] private float durationSeconds = 30f;

    [Header("刷怪")]
    [SerializeField] private EnemyData[] enemyPool;
    [SerializeField] private float spawnInterval = 1.1f;
    [SerializeField] private int maxAliveEnemies = 18;
    [SerializeField] private float enemyHealthMultiplier = 1f;
    [SerializeField] private float enemySpeedMultiplier = 1f;

    /// <summary>
    /// 楼层唯一标识。
    /// </summary>
    public string FloorId => floorId;

    /// <summary>
    /// 楼层序号。
    /// </summary>
    public int FloorIndex => floorIndex;

    /// <summary>
    /// 楼层持续秒数。
    /// </summary>
    public float DurationSeconds => durationSeconds;

    /// <summary>
    /// 可刷新的敌人池。
    /// </summary>
    public EnemyData[] EnemyPool => enemyPool;

    /// <summary>
    /// 刷怪间隔。
    /// </summary>
    public float SpawnInterval => spawnInterval;

    /// <summary>
    /// 最大存活敌人数。
    /// </summary>
    public int MaxAliveEnemies => maxAliveEnemies;

    /// <summary>
    /// 敌人生命倍率。
    /// </summary>
    public float EnemyHealthMultiplier => enemyHealthMultiplier;

    /// <summary>
    /// 敌人速度倍率。
    /// </summary>
    public float EnemySpeedMultiplier => enemySpeedMultiplier;
}
