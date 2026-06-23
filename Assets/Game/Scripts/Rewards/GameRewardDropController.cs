using System;
using UnityEngine;

/// <summary>
/// 管理经验、金币掉落物生成，以及金币收益倍率和小数余数。
/// </summary>
public sealed class GameRewardDropController : MonoBehaviour
{
    private float _goldGainMultiplier = 1f;
    private float _goldDropRemainder;

    /// <summary>
    /// 重置奖励掉落状态。
    /// </summary>
    public void ResetRewardState()
    {
        _goldGainMultiplier = 1f;
        _goldDropRemainder = 0f;
    }

    /// <summary>
    /// 增加金币收益倍率。
    /// </summary>
    /// <param name="value">倍率增量。</param>
    public void AddGoldGainMultiplier(float value)
    {
        _goldGainMultiplier += Mathf.Max(0f, value);
    }

    /// <summary>
    /// 根据敌人奖励数据生成经验和金币掉落物。
    /// </summary>
    /// <param name="target">拾取目标玩家。</param>
    /// <param name="activeRuntimeRoot">激活对象父级。</param>
    /// <param name="experiencePickupPool">经验掉落物对象池。</param>
    /// <param name="goldPickupPool">金币掉落物对象池。</param>
    /// <param name="position">掉落中心位置。</param>
    /// <param name="experience">经验值。</param>
    /// <param name="baseGold">基础金币值。</param>
    /// <returns>本次实际金币掉落数量。</returns>
    public int SpawnEnemyDrops(
        GamePlayerController target,
        Transform activeRuntimeRoot,
        GameRuntimeObjectPoolController runtimeObjectPoolController,
        Vector3 position,
        int experience,
        int baseGold)
    {
        int gold = CalculateGoldDrop(baseGold);
        SpawnPickup(target, activeRuntimeRoot, runtimeObjectPoolController, position + Vector3.left * 0.18f, GamePickup.PickupType.Experience, experience);
        SpawnPickup(target, activeRuntimeRoot, runtimeObjectPoolController, position + Vector3.right * 0.18f, GamePickup.PickupType.Gold, gold);
        return gold;
    }

    /// <summary>
    /// 清理当前场景内的掉落物。
    /// </summary>
    /// <param name="activeRuntimeRoot">激活对象父级。</param>
    /// <param name="releaseObject">对象释放回调。</param>
    public void ClearPickups(Transform activeRuntimeRoot, Action<GameObject> releaseObject)
    {
        if (activeRuntimeRoot == null)
            return;

        GamePickup[] pickups = activeRuntimeRoot.GetComponentsInChildren<GamePickup>();
        foreach (GamePickup pickup in pickups)
        {
            if (pickup != null && releaseObject != null)
                releaseObject.Invoke(pickup.gameObject);
        }
    }

    /// <summary>
    /// 创建掉落物。
    /// </summary>
    /// <param name="target">拾取目标玩家。</param>
    /// <param name="activeRuntimeRoot">激活对象父级。</param>
    /// <param name="pool">掉落物对象池。</param>
    /// <param name="position">生成位置。</param>
    /// <param name="type">掉落类型。</param>
    /// <param name="value">掉落数值。</param>
    private void SpawnPickup(
        GamePlayerController target,
        Transform activeRuntimeRoot,
        GameRuntimeObjectPoolController runtimeObjectPoolController,
        Vector3 position,
        GamePickup.PickupType type,
        int value)
    {
        if (value <= 0 || runtimeObjectPoolController == null)
            return;

        GameRuntimeObjectPoolController.PoolObjectType objectType = type == GamePickup.PickupType.Experience
            ? GameRuntimeObjectPoolController.PoolObjectType.ExperiencePickup
            : GameRuntimeObjectPoolController.PoolObjectType.GoldPickup;

        GamePooledObject pickupObject = runtimeObjectPoolController.Get(
            objectType,
            activeRuntimeRoot,
            position,
            Vector3.one * 0.24f,
            runtimeObjectPoolController.GetDefaultColor(objectType));
        GamePickup pickup = pickupObject.GetComponent<GamePickup>();
        if (pickup == null)
            pickup = pickupObject.gameObject.AddComponent<GamePickup>();

        pickup.Initialize(target, type, value);
    }

    /// <summary>
    /// 根据金币收益倍率计算本次金币掉落，并累积不足 1 的小数收益。
    /// </summary>
    /// <param name="baseGold">敌人基础金币掉落。</param>
    /// <returns>本次实际金币掉落。</returns>
    private int CalculateGoldDrop(int baseGold)
    {
        if (baseGold <= 0)
            return 0;

        float rawGold = baseGold * Mathf.Max(0f, _goldGainMultiplier) + _goldDropRemainder;
        int gold = Mathf.FloorToInt(rawGold);
        _goldDropRemainder = rawGold - gold;
        return Mathf.Max(0, gold);
    }
}
