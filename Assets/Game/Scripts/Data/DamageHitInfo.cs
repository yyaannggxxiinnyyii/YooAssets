using UnityEngine;

/// <summary>
/// 记录一次命中的最终伤害、来源类别、元素类型和触发控制信息。
/// </summary>
public readonly struct DamageHitInfo
{
    /// <summary>
    /// 创建一次兼容旧调用的伤害命中数据，默认按子弹本体伤害处理。
    /// </summary>
    /// <param name="amount">最终伤害数值。</param>
    /// <param name="sourceType">旧版跳字来源类型。</param>
    /// <param name="isCritical">是否暴击，仅普通伤害允许生效。</param>
    public DamageHitInfo(int amount, DamageSourceType sourceType, bool isCritical)
        : this(
            amount,
            DamageSourceCategory.ProjectilePrimary,
            ConvertSourceTypeToElement(sourceType),
            sourceType,
            isCritical,
            true,
            true,
            string.Empty,
            string.Empty,
            string.Empty,
            0)
    {
    }

    /// <summary>
    /// 创建一次伤害命中数据，属性伤害会自动禁止暴击标记。
    /// </summary>
    /// <param name="amount">最终伤害数值。</param>
    /// <param name="sourceCategory">伤害来源类别。</param>
    /// <param name="element">伤害元素类型。</param>
    /// <param name="isCritical">是否暴击，仅普通元素允许生效。</param>
    /// <param name="canTriggerDamageReaction">是否允许触发受伤反应。</param>
    /// <param name="canTriggerKillEffects">是否允许触发击杀效果。</param>
    /// <param name="sourceRelicId">来源遗物 ID。</param>
    /// <param name="sourceEffectKey">来源遗物效果键。</param>
    /// <param name="rootTriggerId">根触发 ID。</param>
    /// <param name="generation">连锁或派生代数。</param>
    public DamageHitInfo(
        int amount,
        DamageSourceCategory sourceCategory,
        DamageElementType element,
        bool isCritical,
        bool canTriggerDamageReaction = true,
        bool canTriggerKillEffects = true,
        string sourceRelicId = "",
        string sourceEffectKey = "",
        string rootTriggerId = "",
        int generation = 0)
        : this(
            amount,
            sourceCategory,
            element,
            ConvertElementToSourceType(element),
            isCritical,
            canTriggerDamageReaction,
            canTriggerKillEffects,
            sourceRelicId,
            sourceEffectKey,
            rootTriggerId,
            generation)
    {
    }

    /// <summary>
    /// 创建一次完整伤害命中数据，并允许指定旧版跳字来源类型。
    /// </summary>
    /// <param name="amount">最终伤害数值。</param>
    /// <param name="sourceCategory">伤害来源类别。</param>
    /// <param name="element">伤害元素类型。</param>
    /// <param name="sourceType">旧版跳字来源类型。</param>
    /// <param name="isCritical">是否暴击，仅普通元素允许生效。</param>
    /// <param name="canTriggerDamageReaction">是否允许触发受伤反应。</param>
    /// <param name="canTriggerKillEffects">是否允许触发击杀效果。</param>
    /// <param name="sourceRelicId">来源遗物 ID。</param>
    /// <param name="sourceEffectKey">来源遗物效果键。</param>
    /// <param name="rootTriggerId">根触发 ID。</param>
    /// <param name="generation">连锁或派生代数。</param>
    public DamageHitInfo(
        int amount,
        DamageSourceCategory sourceCategory,
        DamageElementType element,
        DamageSourceType sourceType,
        bool isCritical,
        bool canTriggerDamageReaction,
        bool canTriggerKillEffects,
        string sourceRelicId,
        string sourceEffectKey,
        string rootTriggerId,
        int generation)
    {
        Amount = Mathf.Max(0, amount);
        SourceCategory = sourceCategory;
        Element = element;
        SourceType = sourceType;
        IsCritical = element == DamageElementType.Normal && isCritical;
        CanTriggerDamageReaction = canTriggerDamageReaction;
        CanTriggerKillEffects = canTriggerKillEffects;
        SourceRelicId = sourceRelicId ?? string.Empty;
        SourceEffectKey = sourceEffectKey ?? string.Empty;
        RootTriggerId = rootTriggerId ?? string.Empty;
        Generation = Mathf.Max(0, generation);
    }

    /// <summary>
    /// 最终伤害数值。
    /// </summary>
    public int Amount { get; }

    /// <summary>
    /// 伤害来源类别。
    /// </summary>
    public DamageSourceCategory SourceCategory { get; }

    /// <summary>
    /// 伤害元素类型。
    /// </summary>
    public DamageElementType Element { get; }

    /// <summary>
    /// 旧版跳字来源类型。
    /// </summary>
    public DamageSourceType SourceType { get; }

    /// <summary>
    /// 是否为暴击伤害。
    /// </summary>
    public bool IsCritical { get; }

    /// <summary>
    /// 是否允许触发受伤反应遗物。
    /// </summary>
    public bool CanTriggerDamageReaction { get; }

    /// <summary>
    /// 是否允许触发击杀类效果。
    /// </summary>
    public bool CanTriggerKillEffects { get; }

    /// <summary>
    /// 造成伤害的遗物 ID。
    /// </summary>
    public string SourceRelicId { get; }

    /// <summary>
    /// 造成伤害的遗物效果键。
    /// </summary>
    public string SourceEffectKey { get; }

    /// <summary>
    /// 本次连锁伤害的根触发 ID。
    /// </summary>
    public string RootTriggerId { get; }

    /// <summary>
    /// 本次连锁或派生伤害代数。
    /// </summary>
    public int Generation { get; }

    /// <summary>
    /// 是否为属性伤害。
    /// </summary>
    public bool IsAttributeDamage => Element != DamageElementType.Normal;

    /// <summary>
    /// 保留现有上下文，仅替换伤害数值。
    /// </summary>
    /// <param name="amount">新的最终伤害数值。</param>
    /// <returns>替换伤害后的命中数据。</returns>
    public DamageHitInfo WithAmount(int amount)
    {
        return new DamageHitInfo(
            amount,
            SourceCategory,
            Element,
            SourceType,
            IsCritical,
            CanTriggerDamageReaction,
            CanTriggerKillEffects,
            SourceRelicId,
            SourceEffectKey,
            RootTriggerId,
            Generation);
    }

    /// <summary>
    /// 将旧版跳字来源类型转换为伤害元素。
    /// </summary>
    /// <param name="sourceType">旧版跳字来源类型。</param>
    /// <returns>对应伤害元素。</returns>
    private static DamageElementType ConvertSourceTypeToElement(DamageSourceType sourceType)
    {
        switch (sourceType)
        {
            case DamageSourceType.Fire:
                return DamageElementType.Fire;

            case DamageSourceType.Ice:
                return DamageElementType.Ice;

            case DamageSourceType.Poison:
                return DamageElementType.Poison;

            case DamageSourceType.Electric:
                return DamageElementType.Lightning;

            case DamageSourceType.Normal:
            default:
                return DamageElementType.Normal;
        }
    }

    /// <summary>
    /// 将伤害元素转换为旧版跳字来源类型。
    /// </summary>
    /// <param name="element">伤害元素。</param>
    /// <returns>对应旧版跳字来源类型。</returns>
    private static DamageSourceType ConvertElementToSourceType(DamageElementType element)
    {
        switch (element)
        {
            case DamageElementType.Fire:
                return DamageSourceType.Fire;

            case DamageElementType.Ice:
                return DamageSourceType.Ice;

            case DamageElementType.Poison:
                return DamageSourceType.Poison;

            case DamageElementType.Lightning:
                return DamageSourceType.Electric;

            case DamageElementType.Normal:
            default:
                return DamageSourceType.Normal;
        }
    }
}
