/// <summary>
/// 定义遗物效果基础接口，提供排序和来源信息。
/// </summary>
public interface IRelicEffect
{
    /// <summary>
    /// 遗物运行时条目。
    /// </summary>
    RelicRuntimeEntry Entry { get; }

    /// <summary>
    /// 初始化遗物效果实例。
    /// </summary>
    /// <param name="entry">遗物运行时条目。</param>
    void Initialize(RelicRuntimeEntry entry);
}

/// <summary>
/// 定义获得遗物时立即触发的效果接口。
/// </summary>
public interface IOnAcquireRelicEffect : IRelicEffect
{
    /// <summary>
    /// 处理获得遗物后的立即效果。
    /// </summary>
    /// <param name="context">遗物运行时控制器。</param>
    void OnAcquire(GameRelicRuntimeController context);
}

/// <summary>
/// 定义被动属性聚合遗物接口。
/// </summary>
public interface IPassiveStatRelicEffect : IRelicEffect
{
    /// <summary>
    /// 刷新被动属性聚合。
    /// </summary>
    /// <param name="context">遗物运行时控制器。</param>
    void RefreshPassiveStats(GameRelicRuntimeController context);
}

/// <summary>
/// 定义投射物命中阶段遗物效果接口。
/// </summary>
public interface IProjectileHitRelicEffect : IRelicEffect
{
    /// <summary>
    /// 处理投射物命中特效。
    /// </summary>
    /// <param name="context">命中特效上下文。</param>
    void OnProjectileHit(ProjectileHitEffectContext context);
}

/// <summary>
/// 定义投射物消失阶段遗物效果接口。
/// </summary>
public interface IProjectileExpireRelicEffect : IRelicEffect
{
    /// <summary>
    /// 处理投射物消失特效。
    /// </summary>
    /// <param name="context">消失特效上下文。</param>
    void OnProjectileExpire(ProjectileExpireEffectContext context);
}

/// <summary>
/// 定义敌人受伤反应遗物效果接口。
/// </summary>
public interface IDamageReactionRelicEffect : IRelicEffect
{
    /// <summary>
    /// 处理敌人受伤后的反应效果。
    /// </summary>
    /// <param name="context">敌人受伤事件上下文。</param>
    void OnEnemyDamaged(EnemyDamagedContext context);
}

/// <summary>
/// 定义击杀敌人时触发的遗物效果接口。
/// </summary>
public interface IEnemyKilledRelicEffect : IRelicEffect
{
    /// <summary>
    /// 处理击杀触发效果。
    /// </summary>
    /// <param name="context">敌人受伤事件上下文。</param>
    void OnEnemyKilled(EnemyDamagedContext context);
}

/// <summary>
/// 定义拾取奖励时触发的遗物效果接口。
/// </summary>
public interface IPickupRelicEffect : IRelicEffect
{
    /// <summary>
    /// 处理拾取触发效果。
    /// </summary>
    void OnPickup();
}

/// <summary>
/// 定义楼层开始时触发的遗物效果接口。
/// </summary>
public interface IFloorStartRelicEffect : IRelicEffect
{
    /// <summary>
    /// 处理楼层开始触发效果。
    /// </summary>
    void OnFloorStart();
}
