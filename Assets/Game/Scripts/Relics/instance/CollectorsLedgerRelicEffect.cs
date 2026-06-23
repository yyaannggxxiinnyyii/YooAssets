/// <summary>
/// 实现收藏者账本按遗物数量提供暴击率加成的遗物效果。
/// </summary>
public sealed class CollectorsLedgerRelicEffect : RelicEffectBase, IPassiveStatRelicEffect
{
    /// <summary>
    /// 被动属性由运行时查询聚合，此处保留接口触发点。
    /// </summary>
    /// <param name="context">遗物运行时控制器。</param>
    public void RefreshPassiveStats(GameRelicRuntimeController context)
    {
    }

    /// <summary>
    /// 计算收藏者账本提供的暴击率加成。
    /// </summary>
    /// <param name="ownedRelicCount">当前拥有遗物数量。</param>
    /// <returns>暴击率加成。</returns>
    public float GetCriticalRateBonus(int ownedRelicCount)
    {
        if (Entry?.RelicData == null)
            return 0f;

        return Entry.RelicData.EffectValue * Entry.StackCount * ownedRelicCount;
    }
}
