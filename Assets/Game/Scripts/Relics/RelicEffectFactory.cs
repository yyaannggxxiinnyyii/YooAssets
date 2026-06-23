using System;
using System.Collections.Generic;

/// <summary>
/// 通过显式效果键创建遗物效果实例。
/// </summary>
public sealed class RelicEffectFactory
{
    private readonly Dictionary<string, Func<IRelicEffect>> _creators = new Dictionary<string, Func<IRelicEffect>>();

    /// <summary>
    /// 注册一个遗物效果创建函数。
    /// </summary>
    /// <param name="effectKey">效果键。</param>
    /// <param name="creator">创建函数。</param>
    public void Register(string effectKey, Func<IRelicEffect> creator)
    {
        if (string.IsNullOrWhiteSpace(effectKey) || creator == null)
            return;

        _creators[effectKey] = creator;
    }

    /// <summary>
    /// 判断效果键是否已注册。
    /// </summary>
    /// <param name="effectKey">效果键。</param>
    /// <returns>已注册时返回 true。</returns>
    public bool Contains(string effectKey)
    {
        return !string.IsNullOrWhiteSpace(effectKey) && _creators.ContainsKey(effectKey);
    }

    /// <summary>
    /// 尝试创建遗物效果实例。
    /// </summary>
    /// <param name="effectKey">效果键。</param>
    /// <param name="effect">创建出的效果实例。</param>
    /// <returns>创建成功时返回 true。</returns>
    public bool TryCreate(string effectKey, out IRelicEffect effect)
    {
        effect = null;
        if (string.IsNullOrWhiteSpace(effectKey))
            return false;

        if (!_creators.TryGetValue(effectKey, out Func<IRelicEffect> creator))
            return false;

        effect = creator.Invoke();
        return effect != null;
    }
}
