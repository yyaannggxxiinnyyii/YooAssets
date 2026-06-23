using UnityEngine;

/// <summary>
/// 定义可长期生效的遗物静态配置数据。
/// </summary>
[CreateAssetMenu(fileName = "RelicData", menuName = "Game/Data/Relic Data")]
public sealed class RelicData : ScriptableObject
{
    [Header("基础信息")]
    [SerializeField] private string relicId = "default_relic";
    [SerializeField] private string displayName = "默认遗物";
    [SerializeField] private string description = "提供一个被动效果。";
    [SerializeField] private Sprite icon;

    [Header("评级")]
    [SerializeField] private GameRarity rarity = GameRarity.Common;

    [Header("效果")]
    [SerializeField] private string effectKey = "";
    [Tooltip("是否全局唯一。开启后获得一次即不再进入商店候选，也不能重复购买。")]
    [SerializeField] private bool globalUnique = true;
    [Tooltip("最大堆叠层数")]
    [SerializeField] private int maxStack = 1;
    [Tooltip("同一触发阶段内的执行顺序，数值越小越先执行")]
    [SerializeField] private int executionOrder = 200;
    [Tooltip("遗物效果数值，含义由EffectKey对应实现解释")]
    [SerializeField] private float effectValue = 1f;
    [Tooltip("遗物附加效果数值，含义由EffectKey对应实现解释")]
    [SerializeField] private float secondaryValue;

    /// <summary>
    /// 遗物唯一标识。
    /// </summary>
    public string RelicId => relicId;

    /// <summary>
    /// 遗物显示名称。
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// 遗物说明文本。
    /// </summary>
    public string Description => description;

    /// <summary>
    /// 遗物图标。
    /// </summary>
    public Sprite Icon => icon;

    /// <summary>
    /// 遗物评级。
    /// </summary>
    public GameRarity Rarity => rarity;

    /// <summary>
    /// 遗物评级显示名称。
    /// </summary>
    public string RarityName => GameRarityUtility.GetDisplayName(rarity);

    /// <summary>
    /// 遗物评级显示颜色。
    /// </summary>
    public Color RarityColor => GameRarityUtility.GetColor(rarity);

    /// <summary>
    /// 遗物效果绑定键。
    /// </summary>
    public string EffectKey => effectKey;

    /// <summary>
    /// 是否全局唯一。
    /// </summary>
    public bool GlobalUnique => globalUnique;

    /// <summary>
    /// 最大堆叠层数。
    /// </summary>
    public int MaxStack => maxStack;

    /// <summary>
    /// 同阶段内的执行顺序，数值越小越先执行。
    /// </summary>
    public int ExecutionOrder => executionOrder;

    /// <summary>
    /// 遗物效果数值。
    /// </summary>
    public float EffectValue => effectValue;

    /// <summary>
    /// 遗物附加效果数值。
    /// </summary>
    public float SecondaryValue => secondaryValue;
}
