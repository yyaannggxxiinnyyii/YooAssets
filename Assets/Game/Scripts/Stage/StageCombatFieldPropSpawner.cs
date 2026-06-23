using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗房场地道具生成器，按当前房间范围随机生成浆果丛等中立场景物。
/// </summary>
public sealed class StageCombatFieldPropSpawner : MonoBehaviour
{
    [Header("生成开关")]
    [Tooltip("战斗房内是否启用浆果丛定时生成。")]
    [SerializeField] private bool spawnBerryBushesEnabled = true;
    [Tooltip("进入战斗房时是否立即进行一次浆果丛生成判定。")]
    [SerializeField] private bool spawnBerryBushesImmediately = true;
    [Tooltip("每次生成判定成功生成浆果丛的概率。")]
    [SerializeField] private float berryBushSpawnChance = 0.6f;
    [Tooltip("浆果丛生成判定的时间间隔，单位秒。")]
    [SerializeField] private float berryBushSpawnInterval = 8f;
    [Tooltip("每次生成判定成功时最少生成的浆果丛数量。")]
    [SerializeField] private int minBerryBushCountPerCheck = 1;
    [Tooltip("每次生成判定成功时最多生成的浆果丛数量。")]
    [SerializeField] private int maxBerryBushCountPerCheck = 1;
    [Tooltip("当前房间内允许同时存在的最大浆果丛数量。")]
    [SerializeField] private int maxActiveBerryBushCount = 3;
    [Tooltip("当前房间内累计允许生成的最大浆果丛数量，用于限制拖时间反复回血；小于等于 0 时不限制累计生成数量。")]
    [SerializeField] private int maxBerryBushSpawnCountPerRoom = 3;
    [Tooltip("战斗清场进入结算或选门阶段时是否立即清理场地道具；关闭后进入新房间或重置整局时仍会强制清理。")]
    [SerializeField] private bool clearFieldPropsOnCombatClear;

    [Header("生成引用")]
    [Tooltip("浆果丛预制体，根节点或子节点需要挂 StageBerryBushFieldProp。")]
    [SerializeField] private StageBerryBushFieldProp berryBushPrefab;
    [Tooltip("场地道具实例父节点。为空时使用当前对象。")]
    [SerializeField] private Transform fieldPropRoot;

    [Header("安全位置")]
    [Tooltip("生成点距离玩家的最小距离。")]
    [SerializeField] private float minDistanceFromPlayer = 4f;
    [Tooltip("生成点距离房间边界的最小留白。")]
    [SerializeField] private float boundaryPadding = 1.2f;
    [Tooltip("生成点检测阻挡重叠时使用的半径。")]
    [SerializeField] private float overlapRadius = 0.6f;
    [Tooltip("场地道具生成点不能重叠的阻挡层，例如 SolidObstacle。为空时不检测阻挡重叠。")]
    [SerializeField] private LayerMask obstacleMask;
    [Tooltip("每个场地道具最多尝试随机安全点的次数。")]
    [SerializeField] private int maxSpawnAttempts = 32;

    private readonly List<StageBerryBushFieldProp> _activeBerryBushes = new List<StageBerryBushFieldProp>();
    private GamePlayerController _player;
    private Vector2 _arenaMin;
    private Vector2 _arenaMax;
    private float _spawnTimer;
    private int _spawnedBerryBushCount;
    private bool _spawnActive;

    /// <summary>
    /// 当前可被投射物命中的场地道具列表。
    /// </summary>
    public IReadOnlyList<StageBerryBushFieldProp> ActiveBerryBushes => _activeBerryBushes;

    /// <summary>
    /// 战斗清场进入结算或选门阶段时是否需要立即清理场地道具。
    /// </summary>
    public bool ClearFieldPropsOnCombatClear => clearFieldPropsOnCombatClear;

    private void Update()
    {
        if (!_spawnActive)
            return;

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer < Mathf.Max(0.1f, berryBushSpawnInterval))
            return;

        _spawnTimer = 0f;
        TryRollBerryBushSpawn();
    }

    /// <summary>
    /// 进入战斗房时初始化场地道具生成器，并启动浆果丛定时生成。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <param name="arenaMin">当前房间范围最小坐标。</param>
    /// <param name="arenaMax">当前房间范围最大坐标。</param>
    public void SpawnForCombatRoom(GamePlayerController player, Vector2 arenaMin, Vector2 arenaMax)
    {
        ClearFieldProps();
        _player = player;
        _arenaMin = arenaMin;
        _arenaMax = arenaMax;
        _spawnTimer = 0f;
        _spawnedBerryBushCount = 0;
        _spawnActive = spawnBerryBushesEnabled && berryBushPrefab != null && player != null;
        if (!_spawnActive)
            return;

        if (spawnBerryBushesImmediately)
            TryRollBerryBushSpawn();
    }

    /// <summary>
    /// 清理当前房间生成的所有场地道具。
    /// </summary>
    public void ClearFieldProps()
    {
        _spawnActive = false;
        _spawnTimer = 0f;
        _spawnedBerryBushCount = 0;
        for (int i = _activeBerryBushes.Count - 1; i >= 0; i--)
        {
            StageBerryBushFieldProp berryBush = _activeBerryBushes[i];
            if (berryBush != null)
                Destroy(berryBush.gameObject);
        }

        _activeBerryBushes.Clear();
    }

    /// <summary>
    /// 停止当前房间继续生成新的场地道具，但保留已经生成的道具实例。
    /// </summary>
    public void StopSpawningAndKeepFieldProps()
    {
        _spawnActive = false;
        _spawnTimer = 0f;
    }

    /// <summary>
    /// 获取当前所有可被投射物命中的场地目标。
    /// </summary>
    /// <param name="results">目标写入列表。</param>
    public void CollectProjectileHitTargets(List<IProjectileHitTarget> results)
    {
        if (results == null)
            return;

        RemoveNullBerryBushes();
        for (int i = _activeBerryBushes.Count - 1; i >= 0; i--)
        {
            StageBerryBushFieldProp berryBush = _activeBerryBushes[i];
            if (berryBush.CanBeHitByProjectile)
                results.Add(berryBush);
        }
    }

    /// <summary>
    /// 按生成概率和场上数量上限尝试生成一批浆果丛。
    /// </summary>
    private void TryRollBerryBushSpawn()
    {
        RemoveNullBerryBushes();
        int maxActiveCount = Mathf.Max(0, maxActiveBerryBushCount);
        if (maxActiveCount <= 0 || _activeBerryBushes.Count >= maxActiveCount)
            return;

        int remainingRoomSpawnCount = GetRemainingRoomSpawnCount();
        if (remainingRoomSpawnCount <= 0)
            return;

        if (UnityEngine.Random.value > Mathf.Clamp01(berryBushSpawnChance))
            return;

        int minCount = Mathf.Max(0, minBerryBushCountPerCheck);
        int maxCount = Mathf.Max(minCount, maxBerryBushCountPerCheck);
        int count = UnityEngine.Random.Range(minCount, maxCount + 1);
        count = Mathf.Min(count, maxActiveCount - _activeBerryBushes.Count);
        count = Mathf.Min(count, remainingRoomSpawnCount);
        for (int i = 0; i < count; i++)
            TrySpawnBerryBush(_arenaMin, _arenaMax);
    }

    /// <summary>
    /// 获取当前房间剩余可生成的浆果丛数量。
    /// </summary>
    /// <returns>剩余可生成数量；无累计上限时返回 int.MaxValue。</returns>
    private int GetRemainingRoomSpawnCount()
    {
        if (maxBerryBushSpawnCountPerRoom <= 0)
            return int.MaxValue;

        return Mathf.Max(0, maxBerryBushSpawnCountPerRoom - _spawnedBerryBushCount);
    }

    /// <summary>
    /// 尝试生成一个浆果丛。
    /// </summary>
    /// <param name="arenaMin">当前房间范围最小坐标。</param>
    /// <param name="arenaMax">当前房间范围最大坐标。</param>
    private void TrySpawnBerryBush(Vector2 arenaMin, Vector2 arenaMax)
    {
        if (!GameRandomSpawnUtility.TryGetRandomPoint(
            arenaMin,
            arenaMax,
            _player != null ? _player.transform.position : transform.position,
            minDistanceFromPlayer,
            boundaryPadding,
            overlapRadius,
            obstacleMask,
            maxSpawnAttempts,
            out Vector3 position))
        {
            Debug.LogWarning($"[FieldProp] 房间 {name} 未找到浆果丛安全生成点，请检查房间范围、玩家安全距离或阻挡层配置。");
            return;
        }

        Transform root = fieldPropRoot != null ? fieldPropRoot : transform;
        StageBerryBushFieldProp berryBush = Instantiate(berryBushPrefab, root);
        berryBush.transform.position = position;
        berryBush.transform.rotation = Quaternion.identity;
        berryBush.Initialize(_player, OnBerryBushCompleted);
        _activeBerryBushes.Add(berryBush);
        _spawnedBerryBushCount++;
    }

    /// <summary>
    /// 在浆果丛被采集或消失时移出运行列表并销毁对象。
    /// </summary>
    /// <param name="berryBush">结束的浆果丛。</param>
    private void OnBerryBushCompleted(StageBerryBushFieldProp berryBush)
    {
        if (berryBush == null)
            return;

        _activeBerryBushes.Remove(berryBush);
        Destroy(berryBush.gameObject);
    }

    /// <summary>
    /// 移除运行列表中已经被销毁的浆果丛引用。
    /// </summary>
    private void RemoveNullBerryBushes()
    {
        for (int i = _activeBerryBushes.Count - 1; i >= 0; i--)
        {
            if (_activeBerryBushes[i] == null)
                _activeBerryBushes.RemoveAt(i);
        }
    }
}
