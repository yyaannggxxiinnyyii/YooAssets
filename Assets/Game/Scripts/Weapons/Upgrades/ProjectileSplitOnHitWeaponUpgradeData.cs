using UnityEngine;

/// <summary>
/// 武器强化效果：让普通投射物命中后触发分裂子弹。
/// </summary>
[CreateAssetMenu(fileName = "WeaponUpgrade_ProjectileSplitOnHit", menuName = "Game/Data/Weapon Upgrade/Projectile Split On Hit")]
public sealed class ProjectileSplitOnHitWeaponUpgradeData : WeaponUpgradeData
{
    [Header("分裂")]
    [Tooltip("分裂子弹相对当前攻击力的伤害倍率。")]
    [SerializeField] private float splitDamageMultiplier = 0.5f;

    /// <summary>
    /// 开启投射物命中分裂，并设置派生子弹伤害倍率。
    /// </summary>
    /// <param name="context">武器强化应用上下文。</param>
    public override void Apply(WeaponUpgradeApplyContext context)
    {
        if (context.ProjectileCombatController == null)
        {
            Debug.LogError("[WeaponUpgrade] 水平分裂缺少 GameProjectileCombatController，无法应用。", this);
            return;
        }

        context.ProjectileCombatController.EnableProjectileSplitOnHit(splitDamageMultiplier);
    }
}
