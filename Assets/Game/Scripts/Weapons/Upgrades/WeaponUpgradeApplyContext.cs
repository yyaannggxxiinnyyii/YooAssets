/// <summary>
/// 封装武器强化应用时需要访问的运行时对象。
/// </summary>
public readonly struct WeaponUpgradeApplyContext
{
    /// <summary>
    /// 创建武器强化应用上下文。
    /// </summary>
    /// <param name="player">当前玩家控制器。</param>
    /// <param name="projectileCombatController">投射物战斗控制器。</param>
    /// <param name="weaponCombatController">武器实例战斗控制器。</param>
    /// <param name="weaponFireController">武器开火控制器。</param>
    /// <param name="weaponData">当前武器配置。</param>
    public WeaponUpgradeApplyContext(
        GamePlayerController player,
        GameProjectileCombatController projectileCombatController,
        GameWeaponCombatController weaponCombatController,
        GameWeaponFireController weaponFireController,
        WeaponData weaponData)
    {
        Player = player;
        ProjectileCombatController = projectileCombatController;
        WeaponCombatController = weaponCombatController;
        WeaponFireController = weaponFireController;
        WeaponData = weaponData;
    }

    /// <summary>
    /// 当前玩家控制器。
    /// </summary>
    public GamePlayerController Player { get; }

    /// <summary>
    /// 投射物战斗控制器。
    /// </summary>
    public GameProjectileCombatController ProjectileCombatController { get; }

    /// <summary>
    /// 武器实例战斗控制器。
    /// </summary>
    public GameWeaponCombatController WeaponCombatController { get; }

    /// <summary>
    /// 武器开火控制器。
    /// </summary>
    public GameWeaponFireController WeaponFireController { get; }

    /// <summary>
    /// 当前武器配置。
    /// </summary>
    public WeaponData WeaponData { get; }
}
