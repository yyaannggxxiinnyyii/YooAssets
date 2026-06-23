using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理武器强化选项的抽取、展示和选择状态。
/// </summary>
public sealed class GameWeaponUpgradeController : MonoBehaviour
{
    private struct WeaponUpgradeOffer
    {
        public string Name;
        public string Description;
        public WeaponUpgradeData Entry;
    }

    private WeaponUpgradeOffer[] _weaponUpgradeOffers;
    private readonly HashSet<WeaponUpgradeData> _selectedWeaponUpgrades = new HashSet<WeaponUpgradeData>();
    private bool _prepared;

    /// <summary>
    /// 准备当前武器的强化选项。
    /// </summary>
    /// <param name="weaponData">当前武器数据。</param>
    public void PrepareWeaponUpgradeChoices(WeaponData weaponData)
    {
        if (_prepared)
            return;

        _prepared = true;
        List<WeaponUpgradeOffer> offerPool = CreateWeaponUpgradeOfferPool(weaponData);
        if (offerPool.Count <= 0)
        {
            Debug.LogWarning("[WeaponUpgrade] 武器强化池为空，无法生成强化选项。");
            return;
        }

        int offerCount = Mathf.Min(GetWeaponUpgradeOfferCount(weaponData), offerPool.Count);
        _weaponUpgradeOffers = new WeaponUpgradeOffer[offerCount];
        for (int i = 0; i < _weaponUpgradeOffers.Length; i++)
        {
            int offerIndex = Random.Range(0, offerPool.Count);
            _weaponUpgradeOffers[i] = offerPool[offerIndex];
            offerPool.RemoveAt(offerIndex);
        }
    }

    /// <summary>
    /// 清理武器强化运行时状态。
    /// </summary>
    public void ResetWeaponUpgradeState()
    {
        _prepared = false;
        _weaponUpgradeOffers = null;
    }

    /// <summary>
    /// 尝试选择一个武器强化选项。
    /// </summary>
    /// <param name="index">选项索引。</param>
    /// <param name="player">当前玩家。</param>
    /// <param name="selectedEntry">选择成功的武器强化 SO 词条。</param>
    /// <returns>选择成功时返回 true。</returns>
    public bool TryChooseWeaponUpgradeOffer(
        int index,
        GamePlayerController player,
        out WeaponUpgradeData selectedEntry)
    {
        selectedEntry = null;
        if (player == null || _weaponUpgradeOffers == null || index < 0 || index >= _weaponUpgradeOffers.Length)
            return false;

        if (!player.TryConsumeWeaponUpgradeChoice())
            return false;

        selectedEntry = _weaponUpgradeOffers[index].Entry;
        if (selectedEntry != null)
            _selectedWeaponUpgrades.Add(selectedEntry);

        ResetWeaponUpgradeState();
        return selectedEntry != null;
    }

    /// <summary>
    /// 清理本局已经选择过的武器强化记录。
    /// </summary>
    public void ResetSelectedWeaponUpgrades()
    {
        _selectedWeaponUpgrades.Clear();
        ResetWeaponUpgradeState();
    }

    /// <summary>
    /// 获取武器强化选项名称列表。
    /// </summary>
    /// <returns>选项名称列表。</returns>
    public string[] GetOfferNames()
    {
        if (_weaponUpgradeOffers == null)
            return null;

        string[] names = new string[_weaponUpgradeOffers.Length];
        for (int i = 0; i < _weaponUpgradeOffers.Length; i++)
            names[i] = _weaponUpgradeOffers[i].Name;

        return names;
    }

    /// <summary>
    /// 获取武器强化选项描述列表。
    /// </summary>
    /// <returns>选项描述列表。</returns>
    public string[] GetOfferDescriptions()
    {
        if (_weaponUpgradeOffers == null)
            return null;

        string[] descriptions = new string[_weaponUpgradeOffers.Length];
        for (int i = 0; i < _weaponUpgradeOffers.Length; i++)
            descriptions[i] = _weaponUpgradeOffers[i].Description;

        return descriptions;
    }

    /// <summary>
    /// 获取当前武器强化每次展示的候选数量。
    /// </summary>
    /// <param name="weaponData">当前武器数据。</param>
    /// <returns>武器强化候选数量。</returns>
    private int GetWeaponUpgradeOfferCount(WeaponData weaponData)
    {
        return weaponData != null ? weaponData.WeaponUpgradeOfferCount : 2;
    }

    /// <summary>
    /// 创建武器强化候选池。
    /// </summary>
    /// <param name="weaponData">当前武器数据。</param>
    /// <returns>武器强化候选列表。</returns>
    private List<WeaponUpgradeOffer> CreateWeaponUpgradeOfferPool(WeaponData weaponData)
    {
        var offerPool = new List<WeaponUpgradeOffer>();
        if (weaponData == null || weaponData.WeaponUpgradeEntries == null)
            return offerPool;

        WeaponUpgradeData[] entries = weaponData.WeaponUpgradeEntries;
        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] == null)
                continue;

            if (_selectedWeaponUpgrades.Contains(entries[i]))
                continue;

            offerPool.Add(CreateWeaponUpgradeOffer(entries[i]));
        }

        return offerPool;
    }

    /// <summary>
    /// 创建一个武器强化选项。
    /// </summary>
    /// <param name="entry">武器强化 SO 配置。</param>
    /// <returns>武器强化选项。</returns>
    private WeaponUpgradeOffer CreateWeaponUpgradeOffer(WeaponUpgradeData entry)
    {
        return new WeaponUpgradeOffer
        {
            Name = entry.DisplayName,
            Description = entry.Description,
            Entry = entry
        };
    }
}
