using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 提供投射物消失特效执行时需要的位置、原因和运行时能力。
/// </summary>
public sealed class ProjectileExpireEffectContext
{
    private readonly GameProjectileCombatController.ProjectileRuntimeContext _runtimeContext;

    /// <summary>
    /// 创建投射物消失特效上下文。
    /// </summary>
    /// <param name="projectile">触发消失特效的投射物。</param>
    /// <param name="position">消失位置。</param>
    /// <param name="expireReason">消失原因。</param>
    /// <param name="runtimeContext">投射物运行时依赖上下文。</param>
    public ProjectileExpireEffectContext(
        GameProjectile projectile,
        Vector3 position,
        ProjectileExpireReason expireReason,
        GameProjectileCombatController.ProjectileRuntimeContext runtimeContext)
    {
        Projectile = projectile;
        Position = position;
        ExpireReason = expireReason;
        _runtimeContext = runtimeContext;
    }

    /// <summary>
    /// 触发消失特效的投射物。
    /// </summary>
    public GameProjectile Projectile { get; }

    /// <summary>
    /// 消失位置。
    /// </summary>
    public Vector3 Position { get; }

    /// <summary>
    /// 消失原因。
    /// </summary>
    public ProjectileExpireReason ExpireReason { get; }

    /// <summary>
    /// 投射物派生代数。
    /// </summary>
    public int ProjectileGeneration => Projectile != null ? Projectile.Generation : 0;

    /// <summary>
    /// 根触发 ID。
    /// </summary>
    public string RootTriggerId => Projectile != null ? Projectile.RootTriggerId : string.Empty;

    /// <summary>
    /// 获取当前基础攻击力。
    /// </summary>
    /// <returns>当前基础攻击力。</returns>
    public int GetAttackDamage()
    {
        return _runtimeContext.GetAttackDamage != null ? Mathf.Max(1, _runtimeContext.GetAttackDamage.Invoke()) : 1;
    }

    /// <summary>
    /// 查找指定范围内的敌人。
    /// </summary>
    /// <param name="position">范围中心。</param>
    /// <param name="radius">范围半径。</param>
    /// <returns>范围内敌人列表。</returns>
    public List<GameEnemyController> FindEnemiesInRadius(Vector3 position, float radius)
    {
        List<GameEnemyController> result = new List<GameEnemyController>();
        if (_runtimeContext.Enemies == null)
            return result;

        float sqrRadius = Mathf.Max(0.1f, radius);
        sqrRadius *= sqrRadius;
        for (int i = 0; i < _runtimeContext.Enemies.Count; i++)
        {
            GameEnemyController enemy = _runtimeContext.Enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            if (Vector3.SqrMagnitude(enemy.transform.position - position) <= sqrRadius)
                result.Add(enemy);
        }

        return result;
    }

    /// <summary>
    /// 通过统一伤害入口对敌人造成伤害。
    /// </summary>
    /// <param name="enemy">目标敌人。</param>
    /// <param name="damageInfo">伤害信息。</param>
    /// <param name="hitPosition">受击位置。</param>
    public void ApplyDamage(GameEnemyController enemy, DamageHitInfo damageInfo, Vector3 hitPosition)
    {
        if (enemy == null || _runtimeContext.Enemies == null || _runtimeContext.ApplyDamageToEnemy == null)
            return;

        int enemyIndex = _runtimeContext.Enemies.IndexOf(enemy);
        if (enemyIndex < 0)
            return;

        _runtimeContext.ApplyDamageToEnemy.Invoke(enemyIndex, enemy, damageInfo, hitPosition, Projectile);
    }

    /// <summary>
    /// 生成派生投射物。
    /// </summary>
    /// <param name="direction">派生投射物方向。</param>
    /// <param name="position">派生投射物位置。</param>
    /// <param name="configureContext">派生发射上下文配置回调。</param>
    /// <returns>新生成的投射物。</returns>
    public GameProjectile SpawnChildProjectile(
        Vector3 direction,
        Vector3 position,
        Func<GameProjectileCombatController.ProjectileFireContext, GameProjectileCombatController.ProjectileFireContext> configureContext = null)
    {
        if (Projectile == null || _runtimeContext.SpawnChildProjectile == null)
            return null;

        GameProjectileCombatController.ProjectileFireContext fireContext = _runtimeContext.CreateChildProjectileContext != null
            ? _runtimeContext.CreateChildProjectileContext.Invoke(Projectile)
            : default;
        if (configureContext != null)
            fireContext = configureContext.Invoke(fireContext);

        return _runtimeContext.SpawnChildProjectile.Invoke(direction, position, fireContext, Projectile);
    }

    /// <summary>
    /// 判断投射物是否拥有指定标签。
    /// </summary>
    /// <param name="tag">标签。</param>
    /// <returns>拥有标签时返回 true。</returns>
    public bool HasProjectileTag(string tag)
    {
        return Projectile != null && Projectile.HasTag(tag);
    }

    /// <summary>
    /// 给投射物添加标签。
    /// </summary>
    /// <param name="tag">标签。</param>
    public void AddProjectileTag(string tag)
    {
        if (Projectile != null)
            Projectile.AddTag(tag);
    }
}
