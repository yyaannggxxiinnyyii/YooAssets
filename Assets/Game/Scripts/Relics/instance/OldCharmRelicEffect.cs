using UnityEngine;

/// <summary>
/// 实现旧护符获得时提升最大生命的遗物效果。
/// </summary>
public sealed class OldCharmRelicEffect : RelicEffectBase, IOnAcquireRelicEffect
{
    /// <summary>
    /// 获得旧护符时提升最大生命。
    /// </summary>
    /// <param name="context">遗物运行时控制器。</param>
    public void OnAcquire(GameRelicRuntimeController context)
    {
        if (context == null || context.Player == null || Entry?.RelicData == null)
            return;

        context.Player.AddMaxHealth(Mathf.RoundToInt(Entry.RelicData.EffectValue));
    }
}
