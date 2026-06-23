/// <summary>
/// 提供遗物效果实例的通用初始化和运行时条目访问。
/// </summary>
public abstract class RelicEffectBase : IRelicEffect
{
    /// <summary>
    /// 遗物运行时条目。
    /// </summary>
    public RelicRuntimeEntry Entry { get; private set; }

    /// <summary>
    /// 初始化遗物效果实例。
    /// </summary>
    /// <param name="entry">遗物运行时条目。</param>
    public virtual void Initialize(RelicRuntimeEntry entry)
    {
        Entry = entry;
    }
}
