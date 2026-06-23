/// <summary>
/// 记录单个遗物在运行时的配置、堆叠层数和获得顺序。
/// </summary>
public sealed class RelicRuntimeEntry
{
    /// <summary>
    /// 创建遗物运行时条目。
    /// </summary>
    /// <param name="relicData">遗物配置。</param>
    /// <param name="effectKey">效果键。</param>
    /// <param name="maxStack">最大堆叠层数。</param>
    /// <param name="acquireOrder">获得顺序。</param>
    public RelicRuntimeEntry(RelicData relicData, string effectKey, int maxStack, int acquireOrder)
    {
        RelicData = relicData;
        EffectKey = effectKey ?? string.Empty;
        MaxStack = maxStack;
        AcquireOrder = acquireOrder;
        StackCount = 1;
    }

    /// <summary>
    /// 遗物配置。
    /// </summary>
    public RelicData RelicData { get; }

    /// <summary>
    /// 遗物 ID。
    /// </summary>
    public string RelicId => RelicData != null ? RelicData.RelicId : string.Empty;

    /// <summary>
    /// 效果键。
    /// </summary>
    public string EffectKey { get; }

    /// <summary>
    /// 最大堆叠层数。
    /// </summary>
    public int MaxStack { get; }

    /// <summary>
    /// 当前堆叠层数。
    /// </summary>
    public int StackCount { get; private set; }

    /// <summary>
    /// 获得顺序。
    /// </summary>
    public int AcquireOrder { get; }

    /// <summary>
    /// 同阶段执行顺序。
    /// </summary>
    public int ExecutionOrder => RelicData != null ? RelicData.ExecutionOrder : 0;

    /// <summary>
    /// 增加一层堆叠。
    /// </summary>
    /// <returns>增加成功时返回 true。</returns>
    public bool TryAddStack()
    {
        if (StackCount >= MaxStack)
            return false;

        StackCount++;
        return true;
    }
}
