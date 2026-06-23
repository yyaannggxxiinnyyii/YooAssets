using System;
using UnityEngine;

/// <summary>
/// 配置升级强化的品质池、品质显示、等级权重和具体词条列表。
/// </summary>
[CreateAssetMenu(fileName = "UpgradePoolConfigData", menuName = "Game/Data/Upgrade Pool Config Data")]
public sealed class UpgradePoolConfigData : ScriptableObject
{
    /// <summary>
    /// 升级强化影响的属性类型。
    /// </summary>
    public enum UpgradeEffectType
    {
        MaxHealth,
        AddedAttackDamage,
        MoveSpeedMultiplier,
        CriticalRate,
        CriticalDamageMultiplier,
        AttackSpeedMultiplier,
        ReloadSpeedMultiplier,
        MagazineCapacityMultiplier,
        AccuracyMultiplier,
        RecoilControlMultiplier,
        ImpactMultiplier,
        ProjectileSpeedMultiplier,
        ProjectileScaleMultiplier,
        ProjectileLifeTimeMultiplier,
        AttackDamageMultiplier,
        ExperienceMultiplier
    }

    /// <summary>
    /// 强化词条品质。
    /// </summary>
    public enum UpgradeRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>
    /// 表示一个可抽取的升级强化词条。
    /// </summary>
    [Serializable]
    public sealed class UpgradeEntry
    {
        [Header("基础信息")]
        [Tooltip("词条在升级强化界面中显示的名称。")]
        [SerializeField] private string displayName = "默认强化";
        [Tooltip("词条在升级强化界面中显示的效果描述。")]
        [SerializeField] private string description = "提升一项属性。";

        [Header("效果")]
        [Tooltip("选择该词条后应用到玩家身上的属性类型。")]
        [SerializeField] private UpgradeEffectType effectType = UpgradeEffectType.AddedAttackDamage;
        [Tooltip("选择该词条后应用到对应属性上的数值。")]
        [SerializeField] private float effectValue = 1f;

        [Header("抽取")]
        [Tooltip("同品质词条池内的抽取权重，数值越高越容易出现。")]
        [SerializeField] private float weight = 1f;

        /// <summary>
        /// 词条显示名称。
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 词条描述。
        /// </summary>
        public string Description => description;

        /// <summary>
        /// 词条效果类型。
        /// </summary>
        public UpgradeEffectType EffectType => effectType;

        /// <summary>
        /// 词条效果数值。
        /// </summary>
        public float EffectValue => effectValue;

        /// <summary>
        /// 同品质词条池内的抽取权重。
        /// </summary>
        public float Weight => Mathf.Max(0f, weight);
    }

    /// <summary>
    /// 表示一个品质下的抽取权重、显示信息和词条列表。
    /// </summary>
    [Serializable]
    public sealed class RarityPool
    {
        [Header("品质")]
        [Tooltip("该词条池对应的强化品质。")]
        [SerializeField] private UpgradeRarity rarity = UpgradeRarity.Common;
        [Tooltip("该品质在升级强化界面中显示的名称。")]
        [SerializeField] private string displayName = "普通";
        [Tooltip("该品质在升级强化界面中使用的背景颜色。")]
        [SerializeField] private Color backgroundColor = new Color(0.78f, 0.78f, 0.78f, 0.95f);

        [Header("等级权重")]
        [Tooltip("玩家 1 级时该品质池的抽取权重。")]
        [SerializeField] private float levelOneWeight = 70f;
        [Tooltip("玩家达到参考等级时该品质池的抽取权重。")]
        [SerializeField] private float referenceLevelWeight = 25f;
        [Tooltip("品质权重插值达到参考权重时使用的玩家等级。")]
        [SerializeField] private int referenceLevel = 21;

        [Header("词条池")]
        [Tooltip("当前品质下可抽取的升级强化词条列表。")]
        [SerializeField] private UpgradeEntry[] options;

        /// <summary>
        /// 品质枚举值。
        /// </summary>
        public UpgradeRarity Rarity => rarity;

        /// <summary>
        /// 品质显示名称。
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 品质背景色。
        /// </summary>
        public Color BackgroundColor => backgroundColor;

        /// <summary>
        /// 当前品质下的词条列表。
        /// </summary>
        public UpgradeEntry[] Options => options;

        /// <summary>
        /// 计算指定玩家等级下的品质抽取权重。
        /// </summary>
        /// <param name="level">玩家等级。</param>
        /// <returns>品质权重。</returns>
        public float GetWeight(int level)
        {
            int safeReferenceLevel = Mathf.Max(2, referenceLevel);
            float progress = Mathf.Clamp01((Mathf.Max(1, level) - 1f) / (safeReferenceLevel - 1f));
            return Mathf.Max(0f, Mathf.Lerp(levelOneWeight, referenceLevelWeight, progress));
        }
    }

    [Header("抽取规则")]
    [Tooltip("每次升级强化界面展示的选项数量。")]
    [SerializeField] private int offerCount = 3;

    [Header("品质池")]
    [Tooltip("升级强化会先按这里配置的品质权重抽取品质，再从对应品质池中抽取具体词条。")]
    [SerializeField] private RarityPool[] rarityPools;

    /// <summary>
    /// 每次升级展示的选项数量。
    /// </summary>
    public int OfferCount => Mathf.Max(1, offerCount);

    /// <summary>
    /// 品质池列表。
    /// </summary>
    public RarityPool[] RarityPools => rarityPools;

    /// <summary>
    /// 按配置权重抽取一个品质。
    /// </summary>
    /// <param name="level">玩家等级。</param>
    /// <returns>抽取到的品质。</returns>
    public UpgradeRarity RollRarity(int level)
    {
        if (rarityPools == null || rarityPools.Length <= 0)
            return UpgradeRarity.Common;

        float totalWeight = 0f;
        for (int i = 0; i < rarityPools.Length; i++)
        {
            if (rarityPools[i] == null)
                continue;

            totalWeight += rarityPools[i].GetWeight(level);
        }

        if (totalWeight <= 0f)
            return UpgradeRarity.Common;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < rarityPools.Length; i++)
        {
            RarityPool pool = rarityPools[i];
            if (pool == null)
                continue;

            roll -= pool.GetWeight(level);
            if (roll <= 0f)
                return pool.Rarity;
        }

        return rarityPools[rarityPools.Length - 1].Rarity;
    }

    /// <summary>
    /// 获取指定品质的显示名称。
    /// </summary>
    /// <param name="rarity">品质。</param>
    /// <param name="fallback">未找到配置时的兜底文本。</param>
    /// <returns>品质显示名称。</returns>
    public string GetRarityName(UpgradeRarity rarity, string fallback)
    {
        RarityPool pool = FindRarityPool(rarity);
        if (pool == null || string.IsNullOrWhiteSpace(pool.DisplayName))
            return fallback;

        return pool.DisplayName;
    }

    /// <summary>
    /// 获取指定品质的背景色。
    /// </summary>
    /// <param name="rarity">品质。</param>
    /// <param name="fallback">未找到配置时的兜底颜色。</param>
    /// <returns>品质背景色。</returns>
    public Color GetRarityColor(UpgradeRarity rarity, Color fallback)
    {
        RarityPool pool = FindRarityPool(rarity);
        return pool != null ? pool.BackgroundColor : fallback;
    }

    /// <summary>
    /// 查找指定品质池。
    /// </summary>
    /// <param name="rarity">品质。</param>
    /// <returns>品质池配置。</returns>
    private RarityPool FindRarityPool(UpgradeRarity rarity)
    {
        if (rarityPools == null)
            return null;

        for (int i = 0; i < rarityPools.Length; i++)
        {
            if (rarityPools[i] != null && rarityPools[i].Rarity == rarity)
                return rarityPools[i];
        }

        return null;
    }
}
