using UnityEngine;

/// <summary>
/// 定义可被玩家投射物命中的非敌方目标，例如浆果丛、可破坏场景物或中立奖励物。
/// </summary>
public interface IProjectileHitTarget
{
    /// <summary>
    /// 目标是否仍可被投射物命中。
    /// </summary>
    bool CanBeHitByProjectile { get; }

    /// <summary>
    /// 目标用于距离命中的世界坐标。
    /// </summary>
    Vector3 ProjectileHitPosition { get; }

    /// <summary>
    /// 目标自身命中半径。
    /// </summary>
    float ProjectileHitRadius { get; }

    /// <summary>
    /// 处理投射物命中。
    /// </summary>
    /// <param name="projectile">命中的投射物。</param>
    /// <param name="damageInfo">投射物携带的伤害信息。</param>
    void HandleProjectileHit(GameProjectile projectile, DamageHitInfo damageInfo);
}
