using System;
using UnityEngine;

/// <summary>
/// 定义一次性购买后立即应用的纯数值道具配置。
/// </summary>
[CreateAssetMenu(fileName = "ItemData", menuName = "Game/Data/Item Data")]
public sealed class ItemData : ScriptableObject
{
    /// <summary>
    /// 道具影响的通用属性类型。
    /// </summary>
    public enum ItemEffectType
    {
        MaxHealth,
        AddedAttackDamage,
        AttackDamageMultiplier,
        MoveSpeedMultiplier,
        ExperienceMultiplier,
        AttackSpeedMultiplier,
        ReloadSpeedMultiplier,
        MagazineCapacityMultiplier,
        AccuracyMultiplier,
        RecoilControlMultiplier,
        ImpactMultiplier,
        ProjectileSpeedMultiplier,
        ProjectileScaleMultiplier,
        ProjectileLifeTimeMultiplier,
        CriticalRate,
        CriticalDamageMultiplier
    }

    /// <summary>
    /// 定义道具提供的一条纯数值效果。
    /// </summary>
    [Serializable]
    public sealed class ItemEffectEntry
    {
        [Tooltip("道具效果类型。")]
        [SerializeField] private ItemEffectType effectType = ItemEffectType.MaxHealth;
        [Tooltip("道具效果数值。倍率类填小数，例如 0.2 表示 +20%。")]
        [SerializeField] private float value = 1f;

        /// <summary>
        /// 道具效果类型。
        /// </summary>
        public ItemEffectType EffectType => effectType;

        /// <summary>
        /// 道具效果数值。
        /// </summary>
        public float Value => value;
    }

    [Header("基础信息")]
    [Tooltip("道具唯一标识。")]
    [SerializeField] private string itemId = "default_item";
    [Tooltip("道具显示名称。")]
    [SerializeField] private string displayName = "默认道具";
    [Tooltip("道具说明文本。")]
    [SerializeField] private string description = "提升一项角色属性。";
    [Tooltip("道具图标。")]
    [SerializeField] private Sprite icon;

    [Header("评级")]
    [Tooltip("道具评级。")]
    [SerializeField] private GameRarity rarity = GameRarity.Common;

    [Header("效果")]
    [Tooltip("道具提供的纯数值效果列表。")]
    [SerializeField] private ItemEffectEntry[] effects = { new ItemEffectEntry() };

    /// <summary>
    /// 道具唯一标识。
    /// </summary>
    public string ItemId => itemId;

    /// <summary>
    /// 道具显示名称。
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// 道具说明文本。
    /// </summary>
    public string Description => description;

    /// <summary>
    /// 道具图标。
    /// </summary>
    public Sprite Icon => icon;

    /// <summary>
    /// 道具评级。
    /// </summary>
    public GameRarity Rarity => rarity;

    /// <summary>
    /// 道具评级显示名称。
    /// </summary>
    public string RarityName => GameRarityUtility.GetDisplayName(rarity);

    /// <summary>
    /// 道具评级显示颜色。
    /// </summary>
    public Color RarityColor => GameRarityUtility.GetColor(rarity);

    /// <summary>
    /// 道具效果列表。
    /// </summary>
    public ItemEffectEntry[] Effects => effects;
}
