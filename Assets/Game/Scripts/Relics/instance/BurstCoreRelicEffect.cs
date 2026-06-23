using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 实现爆裂弹芯在投射物命中或有效消失时造成范围爆炸的遗物效果。
/// </summary>
public sealed class BurstCoreRelicEffect : RelicEffectBase, IProjectileHitRelicEffect, IProjectileExpireRelicEffect
{
    /// <summary>
    /// 投射物命中后触发爆炸。
    /// </summary>
    /// <param name="context">命中特效上下文。</param>
    public void OnProjectileHit(ProjectileHitEffectContext context)
    {
        if (context == null)
            return;

        Explode(context.HitPosition, context.HitEnemy, context.GetAttackDamage(), context);
    }

    /// <summary>
    /// 投射物有效消失时触发爆炸。
    /// </summary>
    /// <param name="context">消失特效上下文。</param>
    public void OnProjectileExpire(ProjectileExpireEffectContext context)
    {
        if (context == null)
            return;

        Explode(context.Position, null, context.GetAttackDamage(), context);
    }

    /// <summary>
    /// 通过命中上下文造成范围爆炸伤害。
    /// </summary>
    /// <param name="position">爆炸中心。</param>
    /// <param name="ignoredEnemy">需要忽略的敌人。</param>
    /// <param name="attackDamage">当前基础攻击力。</param>
    /// <param name="context">命中特效上下文。</param>
    private void Explode(Vector3 position, GameEnemyController ignoredEnemy, int attackDamage, ProjectileHitEffectContext context)
    {
        DamageHitInfo hitInfo = CreateDamageInfo(attackDamage);
        List<GameEnemyController> enemies = context.FindEnemiesInRadius(position, GetRadius());
        for (int i = 0; i < enemies.Count; i++)
        {
            GameEnemyController enemy = enemies[i];
            if (enemy == null || enemy == ignoredEnemy)
                continue;

            context.ApplyDamage(enemy, hitInfo, enemy.transform.position);
        }
    }

    /// <summary>
    /// 通过消失上下文造成范围爆炸伤害。
    /// </summary>
    /// <param name="position">爆炸中心。</param>
    /// <param name="ignoredEnemy">需要忽略的敌人。</param>
    /// <param name="attackDamage">当前基础攻击力。</param>
    /// <param name="context">消失特效上下文。</param>
    private void Explode(Vector3 position, GameEnemyController ignoredEnemy, int attackDamage, ProjectileExpireEffectContext context)
    {
        DamageHitInfo hitInfo = CreateDamageInfo(attackDamage);
        List<GameEnemyController> enemies = context.FindEnemiesInRadius(position, GetRadius());
        for (int i = 0; i < enemies.Count; i++)
        {
            GameEnemyController enemy = enemies[i];
            if (enemy == null || enemy == ignoredEnemy)
                continue;

            context.ApplyDamage(enemy, hitInfo, enemy.transform.position);
        }
    }

    /// <summary>
    /// 创建爆炸伤害信息。
    /// </summary>
    /// <param name="attackDamage">当前基础攻击力。</param>
    /// <returns>爆炸伤害信息。</returns>
    private DamageHitInfo CreateDamageInfo(int attackDamage)
    {
        int damage = Mathf.Max(1, Mathf.RoundToInt(attackDamage * Mathf.Max(0.01f, Entry.RelicData.EffectValue)));
        return new DamageHitInfo(
            damage,
            DamageSourceCategory.ProjectileEffect,
            DamageElementType.Fire,
            false,
            true,
            true,
            Entry.RelicId,
            Entry.EffectKey,
            string.Empty,
            0);
    }

    /// <summary>
    /// 获取爆炸半径。
    /// </summary>
    /// <returns>爆炸半径。</returns>
    private float GetRadius()
    {
        return Entry?.RelicData != null ? Mathf.Max(0.1f, Entry.RelicData.SecondaryValue) : 0.1f;
    }
}
