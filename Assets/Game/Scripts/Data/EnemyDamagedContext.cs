using UnityEngine;

/// <summary>
/// 描述敌人完成一次伤害结算后的目标、位置、伤害数据和结算结果。
/// </summary>
public readonly struct EnemyDamagedContext
{
    /// <summary>
    /// 创建敌人受伤事件上下文。
    /// </summary>
    /// <param name="targetEnemy">实际受伤敌人。</param>
    /// <param name="hitPosition">受伤发生位置。</param>
    /// <param name="damageInfo">原始伤害信息。</param>
    /// <param name="actualDamage">实际扣除伤害。</param>
    /// <param name="killed">本次伤害是否击杀目标。</param>
    /// <param name="sourceProjectile">来源投射物。</param>
    public EnemyDamagedContext(
        GameEnemyController targetEnemy,
        Vector3 hitPosition,
        DamageHitInfo damageInfo,
        int actualDamage,
        bool killed,
        GameProjectile sourceProjectile = null)
    {
        TargetEnemy = targetEnemy;
        HitPosition = hitPosition;
        DamageInfo = damageInfo;
        ActualDamage = Mathf.Max(0, actualDamage);
        Killed = killed;
        SourceProjectile = sourceProjectile;
    }

    /// <summary>
    /// 实际受伤敌人。
    /// </summary>
    public GameEnemyController TargetEnemy { get; }

    /// <summary>
    /// 受伤发生位置。
    /// </summary>
    public Vector3 HitPosition { get; }

    /// <summary>
    /// 原始伤害信息。
    /// </summary>
    public DamageHitInfo DamageInfo { get; }

    /// <summary>
    /// 实际扣除伤害。
    /// </summary>
    public int ActualDamage { get; }

    /// <summary>
    /// 本次伤害是否击杀目标。
    /// </summary>
    public bool Killed { get; }

    /// <summary>
    /// 来源投射物。
    /// </summary>
    public GameProjectile SourceProjectile { get; }
}
