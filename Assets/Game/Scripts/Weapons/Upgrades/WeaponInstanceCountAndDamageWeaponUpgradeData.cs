using UnityEngine;

/// <summary>
/// 武器强化效果：增加武器实例数量，并同步调整攻击力倍率。
/// </summary>
[CreateAssetMenu(fileName = "WeaponUpgrade_WeaponInstanceCountAndDamage", menuName = "Game/Data/Weapon Upgrade/Weapon Instance Count And Damage")]
public sealed class WeaponInstanceCountAndDamageWeaponUpgradeData : WeaponUpgradeData
{
    [Header("武器实例")]
    [Tooltip("获得强化时增加的武器实例数量。")]
    [SerializeField] private int weaponInstanceBonus = 1;
    [Tooltip("获得强化时追加到攻击力倍率上的增量，-0.5 表示当前攻击力倍率减少 50%。")]
    [SerializeField] private float attackDamageMultiplierBonus = -0.5f;

    /// <summary>
    /// 增加当前武器实例数量，并调整玩家当前攻击力倍率。
    /// </summary>
    /// <param name="context">武器强化应用上下文。</param>
    public override void Apply(WeaponUpgradeApplyContext context)
    {
        if (context.WeaponCombatController == null || context.Player == null)
        {
            Debug.LogError("[WeaponUpgrade] 双持射钉枪缺少玩家或武器实例控制器，无法应用。", this);
            return;
        }

        context.WeaponCombatController.AddWeaponInstanceCount(weaponInstanceBonus);
        context.Player.AddAttackDamageMultiplier(attackDamageMultiplierBonus);
    }
}
