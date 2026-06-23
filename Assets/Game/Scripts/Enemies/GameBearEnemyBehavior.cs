using UnityEngine;

/// <summary>
/// 大熊敌人行为：靠近玩家后锁定方向蓄力，并沿锁定方向快速冲刺。
/// </summary>
public sealed class GameBearEnemyBehavior : MonoBehaviour, IGameEnemyBehavior
{
    private enum BearState
    {
        Chase,
        PrepareCharge,
        Charge
    }

    [Header("冲刺")]
    [Tooltip("进入此距离后开始蓄力冲刺。")]
    [SerializeField] private float chargeStartDistance = 3.2f;
    [Tooltip("蓄力持续时间。")]
    [SerializeField] private float chargePrepareDuration = 0.75f;
    [Tooltip("冲刺移动距离。")]
    [SerializeField] private float chargeDistance = 4.2f;
    [Tooltip("冲刺移动速度。")]
    [SerializeField] private float chargeSpeed = 12f;
    [Tooltip("冲刺结束后的冷却时间。")]
    [SerializeField] private float chargeCooldown = 3f;

    [Header("接触伤害")]
    [Tooltip("与玩家接触时造成伤害的半径。")]
    [SerializeField] private float contactRadius = 0.85f;
    [Tooltip("与玩家接触时造成伤害的间隔。")]
    [SerializeField] private float contactDamageInterval = 0.7f;

    [Header("攻击提示")]
    [Tooltip("敌人预制体内部的世界空间攻击指示条控制器。")]
    [SerializeField] private GameEnemyAttackIndicatorController attackIndicator;

    private GameEnemyController _enemy;
    private BearState _state;
    private Vector3 _chargeDirection;
    private float _prepareTimer;
    private float _chargeRemainingDistance;
    private float _cooldownTimer;

    /// <summary>
    /// 初始化大熊敌人行为。
    /// </summary>
    /// <param name="enemy">所属敌人控制器。</param>
    public void Initialize(GameEnemyController enemy)
    {
        _enemy = enemy;
        _state = BearState.Chase;
        _chargeDirection = Vector3.right;
        _prepareTimer = 0f;
        _chargeRemainingDistance = 0f;
        _cooldownTimer = 0f;
        CacheAttackIndicator();
        HideAttackIndicator();
    }

    /// <summary>
    /// 推进追踪、蓄力、冲刺和接触伤害状态。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    public void TickBehavior(float deltaTime)
    {
        if (_enemy == null || !_enemy.HasLivingTarget)
            return;

        switch (_state)
        {
            case BearState.PrepareCharge:
                TickPrepare(deltaTime);
                break;

            case BearState.Charge:
                TickCharge(deltaTime);
                break;

            case BearState.Chase:
            default:
                TickChase(deltaTime);
                break;
        }

        _enemy.TickContactDamage(deltaTime, contactRadius, contactDamageInterval);
    }

    /// <summary>
    /// 普通追踪玩家，冲刺冷却结束且进入距离后开始蓄力。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    private void TickChase(float deltaTime)
    {
        _cooldownTimer = Mathf.Max(0f, _cooldownTimer - deltaTime);
        if (_cooldownTimer > 0f || _enemy.DistanceToTarget > chargeStartDistance)
        {
            _enemy.MoveTowardTarget(deltaTime);
            return;
        }

        StartPrepareCharge();
    }

    /// <summary>
    /// 原地蓄力，结束时沿进入蓄力那一刻锁定的方向开始冲刺。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    private void TickPrepare(float deltaTime)
    {
        _prepareTimer -= deltaTime;
        float duration = Mathf.Max(0.01f, chargePrepareDuration);
        ShowAttackIndicator(1f - Mathf.Clamp01(_prepareTimer / duration));
        if (_prepareTimer > 0f)
            return;

        HideAttackIndicator();
        _chargeRemainingDistance = Mathf.Max(0f, chargeDistance);
        _state = BearState.Charge;
    }

    /// <summary>
    /// 锁定当前玩家方向并进入冲刺前摇。
    /// </summary>
    private void StartPrepareCharge()
    {
        _chargeDirection = _enemy.DirectionToTarget;
        if (_chargeDirection.sqrMagnitude <= 0.01f)
            _chargeDirection = Vector3.right;

        _prepareTimer = Mathf.Max(0.01f, chargePrepareDuration);
        _state = BearState.PrepareCharge;
        ShowAttackIndicator(0f);
    }

    /// <summary>
    /// 沿蓄力开始时锁定的方向快速冲刺。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    private void TickCharge(float deltaTime)
    {
        float moveDistance = Mathf.Min(_chargeRemainingDistance, Mathf.Max(0f, chargeSpeed) * deltaTime);
        _enemy.MoveInDirection(_chargeDirection, moveDistance);
        _chargeRemainingDistance -= moveDistance;

        if (_chargeRemainingDistance > 0f)
            return;

        _cooldownTimer = Mathf.Max(0f, chargeCooldown);
        _state = BearState.Chase;
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
            attackIndicator.Show(_chargeDirection, progress, chargeDistance);
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
    /// 在 Scene 视图中绘制大熊接触伤害和冲刺触发半径。
    /// </summary>
    private void OnDrawGizmos()
    {
        GameRadiusGizmoUtility.DrawRadius(transform.position, chargeStartDistance, new Color(1f, 0.55f, 0.05f, 0.9f), "Charge Start");
        GameRadiusGizmoUtility.DrawRadius(transform.position, contactRadius, new Color(1f, 0.2f, 0.15f, 0.9f), "Contact Damage");
    }
}
