using UnityEngine;

/// <summary>
/// 小蛇敌人行为：保持射击距离，进入攻击范围后边减速移动边蓄力发射弹幕，发射后进入短暂后摇。
/// </summary>
public sealed class GameSnakeEnemyBehavior : MonoBehaviour, IGameEnemyBehavior
{
    private enum SnakeState
    {
        Reposition,
        PrepareFire,
        Recovery
    }

    [Header("站位")]
    [Tooltip("小蛇希望与玩家保持的距离。")]
    [SerializeField] private float stopDistance = 3f;
    [Tooltip("站位容忍范围，处于该范围内时不会前后移动。")]
    [SerializeField] private float distanceTolerance = 0.5f;

    [Header("接触伤害")]
    [Tooltip("是否启用小蛇与玩家贴身时的接触伤害。")]
    [SerializeField] private bool enableContactDamage = true;
    [Tooltip("小蛇触发接触伤害的半径。")]
    [SerializeField] private float contactRadius = 0.65f;
    [Tooltip("小蛇连续造成接触伤害的间隔。")]
    [SerializeField] private float contactDamageInterval = 0.9f;

    [Header("弹幕")]
    [Tooltip("小蛇弹幕命中玩家时造成的伤害。")]
    [SerializeField] private int projectileDamage = 1;
    [Tooltip("小蛇弹幕飞行速度。")]
    [SerializeField] private float projectileSpeed = 3f;
    [Tooltip("小蛇弹幕存在时间。")]
    [SerializeField] private float projectileLifeTime = 4f;
    [Tooltip("小蛇弹幕命中玩家的判定半径。")]
    [SerializeField] private float projectileHitRadius = 0.35f;
    [Tooltip("小蛇两次开始攻击之间的冷却间隔。")]
    [SerializeField] private float fireInterval = 2f;
    [Tooltip("小蛇每次发射的弹幕数量。")]
    [SerializeField] private int barrageCount = 1;
    [Tooltip("多发弹幕时的总散射角度。")]
    [SerializeField] private float barrageSpreadAngle = 18f;
    [Tooltip("弹幕生成点相对小蛇中心的前向偏移。")]
    [SerializeField] private float muzzleOffset = 0.55f;

    [Header("攻击节奏")]
    [Tooltip("进入攻击范围并且冷却结束后，发射弹幕前的蓄力时间。")]
    [SerializeField] private float attackPrepareDuration = 0.75f;
    [Tooltip("发射弹幕后减速移动的后摇时间。")]
    [SerializeField] private float attackRecoveryDuration = 0.35f;
    [Tooltip("攻击前摇和后摇期间的移动速度倍率。")]
    [SerializeField] private float attackMoveSpeedMultiplier = 0.4f;

    [Header("攻击提示")]
    [Tooltip("敌人预制体内部的世界空间攻击指示条控制器。")]
    [SerializeField] private GameEnemyAttackIndicatorController attackIndicator;

    private GameEnemyController _enemy;
    private SnakeState _state;
    private Vector3 _currentFireDirection;
    private float _fireTimer;
    private float _prepareTimer;
    private float _recoveryTimer;

    /// <summary>
    /// 初始化小蛇敌人行为。
    /// </summary>
    /// <param name="enemy">所属敌人控制器。</param>
    public void Initialize(GameEnemyController enemy)
    {
        _enemy = enemy;
        _state = SnakeState.Reposition;
        _currentFireDirection = Vector3.right;
        _fireTimer = Mathf.Min(0.35f, fireInterval);
        _prepareTimer = 0f;
        _recoveryTimer = 0f;
        CacheAttackIndicator();
        HideAttackIndicator();
    }

    /// <summary>
    /// 推进站位、攻击前摇、后摇和接触伤害。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    public void TickBehavior(float deltaTime)
    {
        if (_enemy == null || !_enemy.HasLivingTarget)
            return;

        if (enableContactDamage)
            _enemy.TickContactDamage(deltaTime, contactRadius, contactDamageInterval);

        switch (_state)
        {
            case SnakeState.PrepareFire:
                TickPrepareFire(deltaTime);
                break;

            case SnakeState.Recovery:
                TickRecovery(deltaTime);
                break;

            case SnakeState.Reposition:
            default:
                TickReposition(deltaTime);
                break;
        }
    }

    /// <summary>
    /// 推进小蛇站位和攻击冷却，满足条件时进入蓄力。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    private void TickReposition(float deltaTime)
    {
        float distance = _enemy.DistanceToTarget;
        if (distance > stopDistance + distanceTolerance)
            _enemy.MoveTowardTarget(deltaTime);
        else if (distance < stopDistance - distanceTolerance)
            _enemy.MoveAwayFromTarget(deltaTime);

        _fireTimer = Mathf.Max(0f, _fireTimer - deltaTime);
        if (_fireTimer > 0f || distance > stopDistance + distanceTolerance * 2f)
            return;

        StartPrepareFire();
    }

    /// <summary>
    /// 推进发射前摇，并在前摇结束时按当前瞄准方向发射弹幕。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    private void TickPrepareFire(float deltaTime)
    {
        TickAttackMovement(deltaTime);
        RefreshFireDirection();
        _prepareTimer -= deltaTime;
        float duration = Mathf.Max(0.01f, attackPrepareDuration);
        ShowAttackIndicator(1f - Mathf.Clamp01(_prepareTimer / duration));
        if (_prepareTimer > 0f)
            return;

        FireBarrage(_currentFireDirection);
        HideAttackIndicator();
        _recoveryTimer = Mathf.Max(0f, attackRecoveryDuration);
        _fireTimer = Mathf.Max(0.1f, fireInterval);
        _state = _recoveryTimer > 0f ? SnakeState.Recovery : SnakeState.Reposition;
    }

    /// <summary>
    /// 推进发射后摇，后摇期间小蛇减速维持站位。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    private void TickRecovery(float deltaTime)
    {
        TickAttackMovement(deltaTime);
        _recoveryTimer -= deltaTime;
        if (_recoveryTimer > 0f)
            return;

        _state = SnakeState.Reposition;
    }

    /// <summary>
    /// 初始化当前射击方向并开始攻击前摇。
    /// </summary>
    private void StartPrepareFire()
    {
        RefreshFireDirection();
        _prepareTimer = Mathf.Max(0.01f, attackPrepareDuration);
        _state = SnakeState.PrepareFire;
        ShowAttackIndicator(0f);
    }

    /// <summary>
    /// 攻击前摇和后摇期间按减速倍率维持站位。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    private void TickAttackMovement(float deltaTime)
    {
        float distance = _enemy.DistanceToTarget;
        float moveDistance = _enemy.MoveSpeed * Mathf.Clamp01(attackMoveSpeedMultiplier) * Mathf.Max(0f, deltaTime);
        if (distance > stopDistance + distanceTolerance)
            _enemy.MoveInDirection(_enemy.DirectionToTarget, moveDistance);
        else if (distance < stopDistance - distanceTolerance)
            _enemy.MoveInDirection(-_enemy.DirectionToTarget, moveDistance);
    }

    /// <summary>
    /// 按当前玩家位置刷新弹幕瞄准方向。
    /// </summary>
    private void RefreshFireDirection()
    {
        Vector3 direction = _enemy.DirectionToTarget;
        if (direction.sqrMagnitude > 0.01f)
            _currentFireDirection = direction;
    }

    /// <summary>
    /// 朝当前瞄准方向发射一组弹幕。
    /// </summary>
    /// <param name="baseDirection">弹幕基础方向。</param>
    private void FireBarrage(Vector3 baseDirection)
    {
        if (baseDirection.sqrMagnitude <= 0.01f)
            return;

        int safeCount = Mathf.Max(1, barrageCount);
        float spreadStep = safeCount > 1 ? barrageSpreadAngle / (safeCount - 1) : 0f;
        float startAngle = -barrageSpreadAngle * 0.5f;
        for (int i = 0; i < safeCount; i++)
        {
            float angle = safeCount > 1 ? startAngle + spreadStep * i : 0f;
            Vector3 direction = Quaternion.Euler(0f, 0f, angle) * baseDirection.normalized;
            SpawnProjectile(direction.normalized);
        }
    }

    /// <summary>
    /// 创建一枚敌人弹幕。
    /// </summary>
    /// <param name="direction">弹幕飞行方向。</param>
    private void SpawnProjectile(Vector3 direction)
    {
        GameObject projectileObject = new GameObject("EnemyProjectile");
        projectileObject.transform.SetParent(transform.parent, false);
        projectileObject.transform.position = transform.position + direction * Mathf.Max(0f, muzzleOffset);
        projectileObject.transform.localScale = Vector3.one * 0.32f;

        GameEnemyProjectile projectile = projectileObject.AddComponent<GameEnemyProjectile>();
        projectile.Initialize(
            _enemy.Target,
            direction,
            projectileDamage,
            projectileSpeed,
            projectileLifeTime,
            projectileHitRadius);
    }

    /// <summary>
    /// 缓存预制体内部的攻击指示条控制器。
    /// </summary>
    private void CacheAttackIndicator()
    {
        if (attackIndicator == null)
            attackIndicator = GetComponentInChildren<GameEnemyAttackIndicatorController>(true);
    }

    /// <summary>
    /// 显示攻击指示条并刷新蓄力进度。
    /// </summary>
    /// <param name="progress">蓄力进度。</param>
    private void ShowAttackIndicator(float progress)
    {
        CacheAttackIndicator();
        if (attackIndicator != null)
            attackIndicator.Show(_currentFireDirection, progress);
    }

    /// <summary>
    /// 隐藏攻击指示条。
    /// </summary>
    private void HideAttackIndicator()
    {
        if (attackIndicator != null)
            attackIndicator.Hide();
    }

    /// <summary>
    /// 在 Scene 视图中绘制小蛇站位、接触伤害和弹幕命中半径。
    /// </summary>
    private void OnDrawGizmos()
    {
        GameRadiusGizmoUtility.DrawRadius(transform.position, stopDistance, new Color(0.2f, 0.8f, 1f, 0.9f), "Stop Distance");
        if (enableContactDamage)
            GameRadiusGizmoUtility.DrawRadius(transform.position, contactRadius, new Color(1f, 0.2f, 0.15f, 0.9f), "Contact Damage");

        GameRadiusGizmoUtility.DrawRadius(transform.position, projectileHitRadius, new Color(1f, 0.85f, 0.15f, 0.9f), "Projectile Hit");
    }
}
