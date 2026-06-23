using UnityEngine;

/// <summary>
/// 实现裂变弹芯在投射物有效消失时向左右两侧生成派生弹的遗物效果。
/// </summary>
public sealed class SplitBulletRelicEffect : RelicEffectBase, IProjectileExpireRelicEffect
{
    private const string SplitTag = "split_bullet_expired";

    /// <summary>
    /// 投射物有效消失时触发一次左右分裂。
    /// </summary>
    /// <param name="context">消失特效上下文。</param>
    public void OnProjectileExpire(ProjectileExpireEffectContext context)
    {
        if (context == null || context.Projectile == null)
            return;

        if (context.ProjectileGeneration > 0 || context.HasProjectileTag(SplitTag))
            return;

        context.AddProjectileTag(SplitTag);
        Vector3 sourceDirection = context.Projectile.Direction.sqrMagnitude > 0.01f
            ? context.Projectile.Direction.normalized
            : Vector3.right;
        float splitAngle = GetSplitAngle();
        context.SpawnChildProjectile(Quaternion.Euler(0f, 0f, splitAngle) * sourceDirection, context.Position);
        context.SpawnChildProjectile(Quaternion.Euler(0f, 0f, -splitAngle) * sourceDirection, context.Position);
    }

    /// <summary>
    /// 获取左右分裂角度。
    /// </summary>
    /// <returns>分裂角度。</returns>
    private float GetSplitAngle()
    {
        float configuredAngle = Entry?.RelicData != null ? Entry.RelicData.SecondaryValue : 0f;
        return configuredAngle > 0f ? configuredAngle : 70f;
    }
}
