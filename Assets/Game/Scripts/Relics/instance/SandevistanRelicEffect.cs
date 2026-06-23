/// <summary>
/// 实现斯安威斯坦遗物，强化翻滚、移动速度和残影表现。
/// </summary>
public sealed class SandevistanRelicEffect : RelicEffectBase, IOnAcquireRelicEffect
{
    private const float RollDistanceBonusRate = 0.3f;
    private const float RollCooldownReductionRate = 0.2f;
    private const float RollInvincibleDurationBonus = 0.1f;
    private const float MoveSpeedBonusRate = 0.5f;

    /// <summary>
    /// 获得遗物时立即应用移动与翻滚强化，并切换残影表现。
    /// </summary>
    /// <param name="context">遗物运行时控制器。</param>
    public void OnAcquire(GameRelicRuntimeController context)
    {
        if (context == null || context.Player == null)
            return;

        context.Player.AddRollDistanceMultiplier(RollDistanceBonusRate);
        context.Player.ReduceRollCooldownRate(RollCooldownReductionRate);
        context.Player.AddRollInvincibleDuration(RollInvincibleDurationBonus);
        context.Player.AddMoveSpeedMultiplier(MoveSpeedBonusRate);
        context.Player.ApplySandevistanAfterimage();
    }
}
