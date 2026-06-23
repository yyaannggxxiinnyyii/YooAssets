using UnityEngine;

/// <summary>
/// 定义商店中可出现的商品配置数据。
/// </summary>
[CreateAssetMenu(fileName = "ShopItemData", menuName = "Game/Data/Shop Item Data")]
public sealed class ShopItemData : ScriptableObject
{
    /// <summary>
    /// 商店商品类型。
    /// </summary>
    public enum ShopItemType
    {
        Item,
        Relic
    }

    [Header("基础信息")]
    [Tooltip("商店商品唯一标识。")]
    [SerializeField] private string shopItemId = "default_shop_item";
    [Tooltip("商店商品显示名称。")]
    [SerializeField] private string displayName = "默认商品";
    [Tooltip("商店商品说明文本。")]
    [SerializeField] private string description = "商店商品。";
    [Tooltip("商店商品图标。")]
    [SerializeField] private Sprite icon;

    [Header("商品内容")]
    [Tooltip("商店商品类型，决定使用道具数据还是遗物数据。")]
    [SerializeField] private ShopItemType itemType = ShopItemType.Item;
    [Tooltip("属性道具数据。")]
    [SerializeField] private ItemData itemData;
    [Tooltip("遗物数据。")]
    [SerializeField] private RelicData relicData;

    [Header("价格")]
    [Tooltip("是否使用本商品单独配置的价格。关闭时使用商店按类型和品质配置的默认价格。")]
    [SerializeField] private bool overridePrice;
    [Tooltip("本商品单独配置的固定价格，仅在启用覆盖价格时生效。")]
    [SerializeField] private int overrideFixedPrice = 3;

    /// <summary>
    /// 商店商品唯一标识。
    /// </summary>
    public string ShopItemId => shopItemId;

    /// <summary>
    /// 商店商品显示名称。
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// 商店商品说明文本。
    /// </summary>
    public string Description => description;

    /// <summary>
    /// 商店商品图标。
    /// </summary>
    public Sprite Icon => icon;

    /// <summary>
    /// 商店商品类型。
    /// </summary>
    public ShopItemType ItemType => itemType;

    /// <summary>
    /// 属性道具数据。
    /// </summary>
    public ItemData ItemData => itemData;

    /// <summary>
    /// 遗物数据。
    /// </summary>
    public RelicData RelicData => relicData;

    /// <summary>
    /// 商品内容的评级。
    /// </summary>
    public GameRarity Rarity
    {
        get
        {
            if (itemType == ShopItemType.Relic && relicData != null)
                return relicData.Rarity;

            if (itemType == ShopItemType.Item && itemData != null)
                return itemData.Rarity;

            return GameRarity.Common;
        }
    }

    /// <summary>
    /// 商品内容的评级显示名称。
    /// </summary>
    public string RarityName => GameRarityUtility.GetDisplayName(Rarity);

    /// <summary>
    /// 商品内容的评级显示颜色。
    /// </summary>
    public Color RarityColor => GameRarityUtility.GetColor(Rarity);

    /// <summary>
    /// 是否使用本商品覆盖价格。
    /// </summary>
    public bool OverridePrice => overridePrice;

    /// <summary>
    /// 本商品覆盖固定价格。
    /// </summary>
    public int OverrideFixedPrice => Mathf.Max(1, overrideFixedPrice);
}
