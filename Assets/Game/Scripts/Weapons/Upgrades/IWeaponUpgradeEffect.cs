/// <summary>
/// 定义武器强化效果的统一应用入口。
/// </summary>
public interface IWeaponUpgradeEffect
{
    /// <summary>
    /// 将武器强化效果应用到当前战斗运行时上下文。
    /// </summary>
    /// <param name="context">武器强化应用上下文。</param>
    void Apply(WeaponUpgradeApplyContext context);
}
