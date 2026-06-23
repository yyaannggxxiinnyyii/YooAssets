using UnityEngine;

/// <summary>
/// 作为武器强化 ScriptableObject 的显示数据与效果应用基类。
/// </summary>
public abstract class WeaponUpgradeData : ScriptableObject, IWeaponUpgradeEffect
{
    [Header("显示")]
    [Tooltip("强化词条在选择界面显示的名称。")]
    [SerializeField] private string displayName = "默认武器强化";
    [Tooltip("强化词条在选择界面显示的说明。")]
    [SerializeField] private string description = "提升一项武器属性。";
    [Tooltip("强化词条图标，当前 UI 可为空。")]
    [SerializeField] private Sprite icon;

    /// <summary>
    /// 强化显示名称。
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// 强化说明。
    /// </summary>
    public string Description => description;

    /// <summary>
    /// 强化图标。
    /// </summary>
    public Sprite Icon => icon;

    /// <summary>
    /// 将武器强化效果应用到当前战斗运行时上下文。
    /// </summary>
    /// <param name="context">武器强化应用上下文。</param>
    public abstract void Apply(WeaponUpgradeApplyContext context);
}
