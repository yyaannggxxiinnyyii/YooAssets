using UnityEngine;

/// <summary>
/// 灰狼敌人行为：持续追踪玩家，接触玩家时造成伤害。
/// </summary>
public sealed class GameWolfEnemyBehavior : MonoBehaviour, IGameEnemyBehavior
{
    [Header("接触伤害")]
    [Tooltip("与玩家接触时造成伤害的半径")]
    [SerializeField] private float contactRadius = 0.75f;
    [Tooltip("与玩家接触时造成伤害的间隔")]
    [SerializeField] private float contactDamageInterval = 0.7f;

    private GameEnemyController _enemy;

    /// <summary>
    /// 初始化灰狼敌人行为。
    /// </summary>
    /// <param name="enemy">所属敌人控制器。</param>
    public void Initialize(GameEnemyController enemy)
    {
        _enemy = enemy;
    }

    /// <summary>
    /// 持续向玩家移动并尝试接触伤害。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    public void TickBehavior(float deltaTime)
    {
        if (_enemy == null || !_enemy.HasLivingTarget)
            return;

        _enemy.MoveTowardTarget(deltaTime);
        _enemy.TickContactDamage(deltaTime, contactRadius, contactDamageInterval);
    }

    /// <summary>
    /// 在 Scene 视图中绘制灰狼接触伤害半径。
    /// </summary>
    private void OnDrawGizmos()
    {
        GameRadiusGizmoUtility.DrawRadius(transform.position, contactRadius, new Color(1f, 0.2f, 0.15f, 0.9f), "Contact Damage");
    }
}
