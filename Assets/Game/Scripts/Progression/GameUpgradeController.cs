using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理角色升级强化选项的抽取、刷新和应用。
/// </summary>
public sealed class GameUpgradeController : MonoBehaviour
{
    private struct UpgradeOffer
    {
        public string Name;
        public string Description;
        public UpgradePoolConfigData.UpgradeRarity Rarity;
        public UpgradePoolConfigData.UpgradeEffectType EffectType;
        public float Value;
        public float Weight;
    }

    [Header("升级强化数据")]
    [Tooltip("角色升级时使用的强化词条池。")]
    [SerializeField] private UpgradePoolConfigData upgradePoolConfigData;

    private UpgradeOffer[] _upgradeOffers;
    private bool _prepared;

    /// <summary>
    /// 准备指定玩家当前等级下的升级强化选项。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    public void PrepareUpgradeChoices(GamePlayerController player)
    {
        if (_prepared)
            return;

        _prepared = true;
        _upgradeOffers = RollUpgradeOffers(player);
    }

    /// <summary>
    /// 清理升级强化运行时状态。
    /// </summary>
    public void ResetUpgradeState()
    {
        _prepared = false;
        _upgradeOffers = null;
    }

    /// <summary>
    /// 尝试刷新当前升级强化选项。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>刷新成功时返回 true。</returns>
    public bool TryRefreshUpgradeOffers(GamePlayerController player)
    {
        if (player == null || !player.TryConsumeUpgradeRefresh())
            return false;

        ResetUpgradeState();
        PrepareUpgradeChoices(player);
        return true;
    }

    /// <summary>
    /// 尝试选择并应用一个升级强化选项。
    /// </summary>
    /// <param name="index">选项索引。</param>
    /// <param name="player">当前玩家。</param>
    /// <returns>选择成功时返回 true。</returns>
    public bool TryChooseUpgradeOffer(int index, GamePlayerController player)
    {
        if (player == null || _upgradeOffers == null || index < 0 || index >= _upgradeOffers.Length)
            return false;

        if (!player.TryConsumeUpgradeChoice())
            return false;

        ApplyUpgradeOffer(_upgradeOffers[index], player);
        ResetUpgradeState();
        return true;
    }

    /// <summary>
    /// 获取升级强化选项名称列表。
    /// </summary>
    /// <returns>选项名称列表。</returns>
    public string[] GetOfferNames()
    {
        if (_upgradeOffers == null)
            return null;

        string[] names = new string[_upgradeOffers.Length];
        for (int i = 0; i < _upgradeOffers.Length; i++)
            names[i] = $"[{GetRarityName(_upgradeOffers[i].Rarity)}] {_upgradeOffers[i].Name}";

        return names;
    }

    /// <summary>
    /// 获取升级强化选项描述列表。
    /// </summary>
    /// <returns>选项描述列表。</returns>
    public string[] GetOfferDescriptions()
    {
        if (_upgradeOffers == null)
            return null;

        string[] descriptions = new string[_upgradeOffers.Length];
        for (int i = 0; i < _upgradeOffers.Length; i++)
            descriptions[i] = _upgradeOffers[i].Description;

        return descriptions;
    }

    /// <summary>
    /// 获取升级强化选项背景色列表。
    /// </summary>
    /// <returns>背景色列表。</returns>
    public Color[] GetOfferColors()
    {
        if (_upgradeOffers == null)
            return null;

        Color[] colors = new Color[_upgradeOffers.Length];
        for (int i = 0; i < _upgradeOffers.Length; i++)
            colors[i] = GetRarityColor(_upgradeOffers[i].Rarity);

        return colors;
    }

    /// <summary>
    /// 根据当前等级先抽稀有度，再抽取具体升级强化词条。
    /// </summary>
    /// <param name="player">当前玩家。</param>
    /// <returns>升级强化选项数组。</returns>
    private UpgradeOffer[] RollUpgradeOffers(GamePlayerController player)
    {
        List<UpgradeOffer> offerPool = CreateUpgradeOfferPool();
        if (offerPool.Count <= 0)
        {
            Debug.LogWarning("[Upgrade] 升级强化池为空，无法生成强化选项。");
            return null;
        }

        int playerLevel = player != null ? player.Level : 1;
        int offerCount = Mathf.Min(GetUpgradeOfferCount(), offerPool.Count);
        UpgradeOffer[] offers = new UpgradeOffer[offerCount];
        for (int i = 0; i < offers.Length; i++)
        {
            UpgradePoolConfigData.UpgradeRarity rarity = RollUpgradeRarity(playerLevel, offerPool);
            int offerIndex = FindRandomOfferIndex(offerPool, rarity);
            if (offerIndex < 0)
                return null;

            offers[i] = offerPool[offerIndex];
            offerPool.RemoveAt(offerIndex);
        }

        return offers;
    }

    /// <summary>
    /// 创建升级强化候选池。
    /// </summary>
    /// <returns>升级强化候选列表。</returns>
    private List<UpgradeOffer> CreateUpgradeOfferPool()
    {
        var offerPool = new List<UpgradeOffer>();
        if (upgradePoolConfigData == null || upgradePoolConfigData.RarityPools == null)
            return offerPool;

        foreach (UpgradePoolConfigData.RarityPool rarityPool in upgradePoolConfigData.RarityPools)
        {
            if (rarityPool == null || rarityPool.Options == null)
                continue;

            foreach (UpgradePoolConfigData.UpgradeEntry option in rarityPool.Options)
            {
                if (option == null)
                    continue;

                offerPool.Add(CreateUpgradeOffer(
                    option.DisplayName,
                    option.Description,
                    rarityPool.Rarity,
                    option.EffectType,
                    option.EffectValue,
                    option.Weight));
            }
        }

        return offerPool;
    }

    /// <summary>
    /// 创建一个升级强化选项。
    /// </summary>
    /// <param name="offerName">选项名。</param>
    /// <param name="description">选项描述。</param>
    /// <param name="rarity">词条稀有度。</param>
    /// <param name="effectType">强化类型。</param>
    /// <param name="value">强化数值。</param>
    /// <param name="weight">同品质词条池内的抽取权重。</param>
    /// <returns>升级强化选项。</returns>
    private UpgradeOffer CreateUpgradeOffer(
        string offerName,
        string description,
        UpgradePoolConfigData.UpgradeRarity rarity,
        UpgradePoolConfigData.UpgradeEffectType effectType,
        float value,
        float weight)
    {
        return new UpgradeOffer
        {
            Name = offerName,
            Description = description,
            Rarity = rarity,
            EffectType = effectType,
            Value = value,
            Weight = Mathf.Max(0f, weight)
        };
    }

    /// <summary>
    /// 获取每次升级展示的选项数量。
    /// </summary>
    /// <returns>选项数量。</returns>
    private int GetUpgradeOfferCount()
    {
        return upgradePoolConfigData != null ? upgradePoolConfigData.OfferCount : 3;
    }

    /// <summary>
    /// 根据强化池配置抽取词条稀有度。
    /// </summary>
    /// <param name="level">玩家等级。</param>
    /// <param name="offerPool">当前候选池。</param>
    /// <returns>抽取到的稀有度。</returns>
    private UpgradePoolConfigData.UpgradeRarity RollUpgradeRarity(int level, List<UpgradeOffer> offerPool)
    {
        if (upgradePoolConfigData != null)
            return upgradePoolConfigData.RollRarity(level);

        if (offerPool == null || offerPool.Count <= 0)
            return UpgradePoolConfigData.UpgradeRarity.Common;

        return offerPool[Random.Range(0, offerPool.Count)].Rarity;
    }

    /// <summary>
    /// 从词条池中按权重查找指定稀有度的随机词条。
    /// </summary>
    /// <param name="offerPool">词条池。</param>
    /// <param name="rarity">目标稀有度。</param>
    /// <returns>词条索引。</returns>
    private int FindRandomOfferIndex(List<UpgradeOffer> offerPool, UpgradePoolConfigData.UpgradeRarity rarity)
    {
        var indexes = new List<int>();
        for (int i = 0; i < offerPool.Count; i++)
        {
            if (offerPool[i].Rarity == rarity)
                indexes.Add(i);
        }

        if (indexes.Count <= 0)
        {
            Debug.LogError($"[Upgrade] 品质 {rarity} 没有可抽取的升级强化词条，请检查升级词条池配置。");
            return -1;
        }

        int offerIndex = FindWeightedOfferIndex(offerPool, indexes);
        if (offerIndex < 0)
            Debug.LogError($"[Upgrade] 品质 {rarity} 的升级强化词条权重总和为 0，请至少配置一个正权重词条。");

        return offerIndex;
    }

    /// <summary>
    /// 从指定候选索引中按词条权重抽取一个词条。
    /// </summary>
    /// <param name="offerPool">词条池。</param>
    /// <param name="indexes">可参与抽取的词条索引。</param>
    /// <returns>抽取到的词条索引。</returns>
    private int FindWeightedOfferIndex(List<UpgradeOffer> offerPool, List<int> indexes)
    {
        float totalWeight = 0f;
        for (int i = 0; i < indexes.Count; i++)
            totalWeight += offerPool[indexes[i]].Weight;

        if (totalWeight <= 0f)
            return -1;

        float roll = Random.Range(0f, totalWeight);
        for (int i = 0; i < indexes.Count; i++)
        {
            int offerIndex = indexes[i];
            float weight = offerPool[offerIndex].Weight;
            if (weight <= 0f)
                continue;

            roll -= weight;
            if (roll <= 0f)
                return offerIndex;
        }

        for (int i = indexes.Count - 1; i >= 0; i--)
        {
            int offerIndex = indexes[i];
            if (offerPool[offerIndex].Weight > 0f)
                return offerIndex;
        }

        return -1;
    }

    /// <summary>
    /// 获取稀有度显示名称。
    /// </summary>
    /// <param name="rarity">稀有度。</param>
    /// <returns>显示名称。</returns>
    private string GetRarityName(UpgradePoolConfigData.UpgradeRarity rarity)
    {
        string fallback;
        switch (rarity)
        {
            case UpgradePoolConfigData.UpgradeRarity.Uncommon:
                fallback = "稀有";
                break;

            case UpgradePoolConfigData.UpgradeRarity.Rare:
                fallback = "罕见";
                break;

            case UpgradePoolConfigData.UpgradeRarity.Epic:
                fallback = "史诗";
                break;

            case UpgradePoolConfigData.UpgradeRarity.Legendary:
                fallback = "传奇";
                break;

            case UpgradePoolConfigData.UpgradeRarity.Common:
            default:
                fallback = "普通";
                break;
        }

        if (upgradePoolConfigData != null)
            return upgradePoolConfigData.GetRarityName(rarity, fallback);

        return fallback;
    }

    /// <summary>
    /// 获取稀有度对应背景色。
    /// </summary>
    /// <param name="rarity">稀有度。</param>
    /// <returns>背景色。</returns>
    private Color GetRarityColor(UpgradePoolConfigData.UpgradeRarity rarity)
    {
        Color fallback;
        switch (rarity)
        {
            case UpgradePoolConfigData.UpgradeRarity.Uncommon:
                fallback = new Color(0.18f, 0.45f, 0.22f, 0.95f);
                break;

            case UpgradePoolConfigData.UpgradeRarity.Rare:
                fallback = new Color(0.16f, 0.32f, 0.62f, 0.95f);
                break;

            case UpgradePoolConfigData.UpgradeRarity.Epic:
                fallback = new Color(0.43f, 0.22f, 0.62f, 0.95f);
                break;

            case UpgradePoolConfigData.UpgradeRarity.Legendary:
                fallback = new Color(0.88f, 0.58f, 0.12f, 0.95f);
                break;

            case UpgradePoolConfigData.UpgradeRarity.Common:
            default:
                fallback = new Color(0.78f, 0.78f, 0.78f, 0.95f);
                break;
        }

        if (upgradePoolConfigData != null)
            return upgradePoolConfigData.GetRarityColor(rarity, fallback);

        return fallback;
    }

    /// <summary>
    /// 应用升级强化效果到玩家。
    /// </summary>
    /// <param name="offer">升级强化选项。</param>
    /// <param name="player">当前玩家。</param>
    private void ApplyUpgradeOffer(UpgradeOffer offer, GamePlayerController player)
    {
        switch (offer.EffectType)
        {
            case UpgradePoolConfigData.UpgradeEffectType.MaxHealth:
                player.AddMaxHealth(Mathf.RoundToInt(offer.Value));
                break;

            case UpgradePoolConfigData.UpgradeEffectType.AddedAttackDamage:
                player.AddWeaponAttackDamage(Mathf.RoundToInt(offer.Value));
                break;

            case UpgradePoolConfigData.UpgradeEffectType.AttackDamageMultiplier:
                player.AddAttackDamageMultiplier(offer.Value);
                break;

            case UpgradePoolConfigData.UpgradeEffectType.MoveSpeedMultiplier:
                player.AddMoveSpeedMultiplier(offer.Value);
                break;

            case UpgradePoolConfigData.UpgradeEffectType.CriticalRate:
                player.AddCriticalRate(offer.Value);
                break;

            case UpgradePoolConfigData.UpgradeEffectType.CriticalDamageMultiplier:
                player.AddCriticalDamage(offer.Value);
                break;

            case UpgradePoolConfigData.UpgradeEffectType.AttackSpeedMultiplier:
                player.AddAttackSpeedMultiplier(offer.Value);
                break;

            case UpgradePoolConfigData.UpgradeEffectType.ReloadSpeedMultiplier:
                player.AddReloadSpeedMultiplier(offer.Value);
                break;

            case UpgradePoolConfigData.UpgradeEffectType.MagazineCapacityMultiplier:
                player.AddMagazineCapacityMultiplier(offer.Value);
                break;

            case UpgradePoolConfigData.UpgradeEffectType.AccuracyMultiplier:
                player.AddAccuracyMultiplier(offer.Value);
                break;

            case UpgradePoolConfigData.UpgradeEffectType.RecoilControlMultiplier:
                player.AddRecoilControlMultiplier(offer.Value);
                break;

            case UpgradePoolConfigData.UpgradeEffectType.ImpactMultiplier:
                player.AddImpactMultiplier(offer.Value);
                break;

            case UpgradePoolConfigData.UpgradeEffectType.ProjectileSpeedMultiplier:
                player.AddProjectileSpeedMultiplier(offer.Value);
                break;

            case UpgradePoolConfigData.UpgradeEffectType.ProjectileScaleMultiplier:
                player.AddProjectileScaleMultiplier(offer.Value);
                break;

            case UpgradePoolConfigData.UpgradeEffectType.ProjectileLifeTimeMultiplier:
                player.AddProjectileLifeTimeMultiplier(offer.Value);
                break;

            case UpgradePoolConfigData.UpgradeEffectType.ExperienceMultiplier:
                player.AddExperienceMultiplier(offer.Value);
                break;
        }
    }
}
