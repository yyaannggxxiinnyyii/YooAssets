using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 保存玩家单局内拥有的武器、道具和遗物。
/// </summary>
[System.Serializable]
public sealed class Inventory
{
    [Header("内容")]
    [SerializeField] private List<WeaponRuntimeState> weapons = new List<WeaponRuntimeState>();
    [SerializeField] private List<ItemData> items = new List<ItemData>();
    [SerializeField] private List<RelicData> relics = new List<RelicData>();

    /// <summary>
    /// 当前武器列表。
    /// </summary>
    public IReadOnlyList<WeaponRuntimeState> Weapons => weapons;

    /// <summary>
    /// 当前道具列表。
    /// </summary>
    public IReadOnlyList<ItemData> Items => items;

    /// <summary>
    /// 当前遗物列表。
    /// </summary>
    public IReadOnlyList<RelicData> Relics => relics;

    /// <summary>
    /// 清空背包内容。
    /// </summary>
    public void Clear()
    {
        weapons.Clear();
        items.Clear();
        relics.Clear();
    }

    /// <summary>
    /// 添加武器运行时状态。
    /// </summary>
    /// <param name="weaponState">武器运行时状态。</param>
    public void AddWeapon(WeaponRuntimeState weaponState)
    {
        if (weaponState == null)
        {
            Debug.LogWarning("[Inventory] 武器状态为空，已跳过添加。");
            return;
        }

        weapons.Add(weaponState);
    }

    /// <summary>
    /// 添加道具。
    /// </summary>
    /// <param name="itemData">道具数据。</param>
    public void AddItem(ItemData itemData)
    {
        if (itemData == null)
        {
            Debug.LogWarning("[Inventory] 道具数据为空，已跳过添加。");
            return;
        }

        items.Add(itemData);
    }

    /// <summary>
    /// 添加遗物。
    /// </summary>
    /// <param name="relicData">遗物数据。</param>
    public void AddRelic(RelicData relicData)
    {
        if (relicData == null)
        {
            Debug.LogWarning("[Inventory] 遗物数据为空，已跳过添加。");
            return;
        }

        relics.Add(relicData);
    }
}
