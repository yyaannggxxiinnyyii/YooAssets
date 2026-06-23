/// <summary>
/// 实现金币罗盘提高金币收益倍率的遗物效果。
/// </summary>
public sealed class GoldCompassRelicEffect : RelicEffectBase, IOnAcquireRelicEffect
{
    /// <summary>
    /// 获得金币罗盘时提高后续金币收益。
    /// </summary>
    /// <param name="context">遗物运行时控制器。</param>
    public void OnAcquire(GameRelicRuntimeController context)
    {
        if (context == null || context.RewardDropController == null || Entry?.RelicData == null)
            return;

        context.RewardDropController.AddGoldGainMultiplier(Entry.RelicData.EffectValue);
    }
}
