using UnityEngine;

/// <summary>
/// 管理伤害跳字显示，并通过运行时对象池复用跳字对象。
/// </summary>
public sealed class GameDamageNumberController : MonoBehaviour
{
    [Header("伤害跳字")]
    [Tooltip("伤害数字显示配置。")]
    [SerializeField] private DamageNumberConfigData damageNumberConfigData;

    private GameRuntimeObjectPoolController _runtimeObjectPoolController;
    private Vector3 _lastSpawnPosition;
    private float _lastSpawnTime = -999f;
    private int _stackOffsetCount;

    /// <summary>
    /// 注入运行时对象池依赖。
    /// </summary>
    /// <param name="runtimeObjectPoolController">运行时对象池控制器。</param>
    public void InitializeRuntimeObjectPool(GameRuntimeObjectPoolController runtimeObjectPoolController)
    {
        _runtimeObjectPoolController = runtimeObjectPoolController;
    }

    /// <summary>
    /// 显示一次伤害跳字。
    /// </summary>
    /// <param name="activeRuntimeRoot">激活对象父级。</param>
    /// <param name="position">受击世界坐标。</param>
    /// <param name="hitInfo">伤害命中数据。</param>
    public void SpawnDamageNumber(Transform activeRuntimeRoot, Vector3 position, DamageHitInfo hitInfo)
    {
        if (_runtimeObjectPoolController == null || damageNumberConfigData == null || hitInfo.Amount <= 0)
            return;

        Vector3 displayPosition = GetStackedDisplayPosition(position);
        GamePooledObject pooledObject = _runtimeObjectPoolController.Get(
            GameRuntimeObjectPoolController.PoolObjectType.DamageNumber,
            activeRuntimeRoot,
            displayPosition,
            Vector3.one,
            Color.white);
        GameDamageNumber damageNumber = pooledObject.GetComponent<GameDamageNumber>();
        if (damageNumber != null)
            damageNumber.Play(hitInfo, damageNumberConfigData);
    }

    /// <summary>
    /// 在指定世界位置显示一次治疗跳字。
    /// </summary>
    /// <param name="activeRuntimeRoot">激活对象父级。</param>
    /// <param name="position">治疗目标世界坐标。</param>
    /// <param name="healAmount">实际恢复的红血点数。</param>
    public void SpawnHealNumber(Transform activeRuntimeRoot, Vector3 position, int healAmount)
    {
        if (_runtimeObjectPoolController == null || damageNumberConfigData == null || healAmount <= 0)
            return;

        GamePooledObject pooledObject = _runtimeObjectPoolController.Get(
            GameRuntimeObjectPoolController.PoolObjectType.DamageNumber,
            activeRuntimeRoot,
            position,
            Vector3.one,
            Color.white);
        GameDamageNumber damageNumber = pooledObject.GetComponent<GameDamageNumber>();
        if (damageNumber != null)
            damageNumber.PlayHeal(healAmount, damageNumberConfigData);
    }

    /// <summary>
    /// 根据短时间内的连续跳字顺序计算固定方向的显示错位。
    /// </summary>
    /// <param name="position">原始受击世界坐标。</param>
    /// <returns>带有连续跳字间距的显示坐标。</returns>
    private Vector3 GetStackedDisplayPosition(Vector3 position)
    {
        float timeSinceLastSpawn = Time.time - _lastSpawnTime;
        float positionDistance = Vector2.Distance(position, _lastSpawnPosition);
        bool isStackedSpawn =
            timeSinceLastSpawn <= damageNumberConfigData.StackTimeWindow &&
            positionDistance <= damageNumberConfigData.StackPositionRadius;

        _stackOffsetCount = isStackedSpawn
            ? Mathf.Min(_stackOffsetCount + 1, damageNumberConfigData.MaxStackOffsetCount)
            : 0;
        _lastSpawnTime = Time.time;
        _lastSpawnPosition = position;

        if (_stackOffsetCount <= 0 || damageNumberConfigData.StackSpacing <= 0f)
            return position;

        Vector3 stackDirection = Quaternion.Euler(0f, 0f, damageNumberConfigData.JumpAngle) * Vector3.right;
        return position + stackDirection.normalized * damageNumberConfigData.StackSpacing * _stackOffsetCount;
    }
}
