using System;
using System.Collections.Generic;

/// <summary>
/// 统一调度投射物命中和消失阶段的机制效果。
/// </summary>
public sealed class ProjectileEffectDispatcher
{
    /// <summary>
    /// 按顺序执行投射物命中特效。
    /// </summary>
    /// <param name="context">投射物命中特效上下文。</param>
    /// <param name="effects">待执行效果列表。</param>
    public void DispatchHitEffects(ProjectileHitEffectContext context, IReadOnlyList<Action<ProjectileHitEffectContext>> effects)
    {
        if (context == null || effects == null)
            return;

        for (int i = 0; i < effects.Count; i++)
            effects[i]?.Invoke(context);
    }

    /// <summary>
    /// 按顺序执行投射物消失特效。
    /// </summary>
    /// <param name="context">投射物消失特效上下文。</param>
    /// <param name="effects">待执行效果列表。</param>
    public void DispatchExpireEffects(ProjectileExpireEffectContext context, IReadOnlyList<Action<ProjectileExpireEffectContext>> effects)
    {
        if (context == null || effects == null)
            return;

        for (int i = 0; i < effects.Count; i++)
            effects[i]?.Invoke(context);
    }
}
