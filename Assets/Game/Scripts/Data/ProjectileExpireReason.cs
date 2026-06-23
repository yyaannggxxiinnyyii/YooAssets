/// <summary>
/// 定义投射物进入消失阶段的原因，用于控制遗物消失效果是否触发。
/// </summary>
public enum ProjectileExpireReason
{
    HitConsumed,
    HitWall,
    LifeTimeEnded,
    ManualRelease
}
