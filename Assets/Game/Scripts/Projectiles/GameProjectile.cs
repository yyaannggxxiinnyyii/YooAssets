using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 控制投射物飞行、命中记录、反弹和生命周期。
/// </summary>
public sealed class GameProjectile : MonoBehaviour
{
    [Header("投射物属性")]
    [Tooltip("投射物命中敌人的中心距离判定半径。")]
    [SerializeField] private float hitRadius = 0.46f;
    [Tooltip("投射物运行时移动速度，会在发射时被武器数据覆盖。")]
    [SerializeField] private float moveSpeed = 9f;
    [Tooltip("投射物运行时存在时间，会在发射时被武器数据覆盖。")]
    [SerializeField] private float lifeTime = 2.2f;

    private readonly HashSet<int> _hitTargetIds = new HashSet<int>();
    private readonly HashSet<string> _tags = new HashSet<string>();
    private readonly List<IProjectileHitRelicEffect> _hitRelicEffects = new List<IProjectileHitRelicEffect>();
    private readonly List<IProjectileExpireRelicEffect> _expireRelicEffects = new List<IProjectileExpireRelicEffect>();
    private Camera _boundaryCamera;
    private Vector3 _direction;
    private Vector2 _arenaMin;
    private Vector2 _arenaMax;
    private DamageHitInfo _damageInfo;
    private string _rootTriggerId;
    private int _remainingPierce;
    private int _remainingBounce;
    private int _generation;
    private ProjectileFireSourceType _fireSourceType;
    private float _impactDistance;
    private float _timer;
    private bool _hasArenaBounds;
    private bool _hitBoundary;

    /// <summary>
    /// 伤害值。
    /// </summary>
    public int Damage => _damageInfo.Amount;

    /// <summary>
    /// 本投射物携带的完整伤害信息。
    /// </summary>
    public DamageHitInfo DamageInfo => _damageInfo;

    /// <summary>
    /// 当前飞行方向。
    /// </summary>
    public Vector3 Direction => _direction;

    /// <summary>
    /// 剩余穿透层数。
    /// </summary>
    public int RemainingPierce => _remainingPierce;

    /// <summary>
    /// 剩余反弹次数。
    /// </summary>
    public int RemainingBounce => _remainingBounce;

    /// <summary>
    /// 派生代数。
    /// </summary>
    public int Generation => _generation;

    /// <summary>
    /// 根触发 ID。
    /// </summary>
    public string RootTriggerId => _rootTriggerId;

    /// <summary>
    /// 投射物发射来源。
    /// </summary>
    public ProjectileFireSourceType FireSourceType => _fireSourceType;

    /// <summary>
    /// 投射物命中遗物效果快照。
    /// </summary>
    public IReadOnlyList<IProjectileHitRelicEffect> HitRelicEffects => _hitRelicEffects;

    /// <summary>
    /// 投射物消失遗物效果快照。
    /// </summary>
    public IReadOnlyList<IProjectileExpireRelicEffect> ExpireRelicEffects => _expireRelicEffects;

    /// <summary>
    /// 命中敌人时击退距离。
    /// </summary>
    public float ImpactDistance => _impactDistance;

    /// <summary>
    /// 投射物命中敌人的中心距离判定半径。
    /// </summary>
    public float HitRadius => Mathf.Max(0.01f, hitRadius);

    /// <summary>
    /// 是否已经过期。
    /// </summary>
    public bool IsExpired => _timer >= lifeTime;

    /// <summary>
    /// 是否已经命中战斗边界。
    /// </summary>
    public bool HasHitBoundary => _hitBoundary;

    private void Update()
    {
        _timer += Time.deltaTime;
        transform.position += _direction * moveSpeed * Time.deltaTime;
        TickBoundaryCollision();
    }

    /// <summary>
    /// 在 Scene 视图中绘制投射物命中半径。
    /// </summary>
    private void OnDrawGizmos()
    {
        GameRadiusGizmoUtility.DrawRadius(transform.position, HitRadius, new Color(1f, 0.85f, 0.15f, 0.9f), "Projectile Hit");
    }

    /// <summary>
    /// 初始化投射物飞行方向、伤害和飞行参数。
    /// </summary>
    /// <param name="direction">归一化飞行方向。</param>
    /// <param name="damage">伤害值。</param>
    /// <param name="speed">飞行速度。</param>
    /// <param name="duration">存在时间。</param>
    public void Initialize(Vector3 direction, int damage, float speed, float duration)
    {
        Initialize(direction, new DamageHitInfo(damage, DamageSourceType.Normal, false), speed, duration, 0, 0, Vector2.zero, Vector2.zero);
    }

    /// <summary>
    /// 初始化投射物飞行方向、伤害、飞行参数和特殊属性。
    /// </summary>
    /// <param name="direction">归一化飞行方向。</param>
    /// <param name="damage">伤害值。</param>
    /// <param name="speed">飞行速度。</param>
    /// <param name="duration">存在时间。</param>
    /// <param name="pierce">穿透层数。</param>
    /// <param name="bounce">反弹层数。</param>
    /// <param name="arenaMin">战斗区域最小坐标。</param>
    /// <param name="arenaMax">战斗区域最大坐标。</param>
    public void Initialize(Vector3 direction, int damage, float speed, float duration, int pierce, int bounce, Vector2 arenaMin, Vector2 arenaMax)
    {
        Initialize(direction, new DamageHitInfo(damage, DamageSourceType.Normal, false), speed, duration, pierce, bounce, arenaMin, arenaMax);
    }

    /// <summary>
    /// 初始化投射物飞行方向、完整伤害信息、飞行参数和特殊属性。
    /// </summary>
    /// <param name="direction">归一化飞行方向。</param>
    /// <param name="damageInfo">完整伤害信息。</param>
    /// <param name="speed">飞行速度。</param>
    /// <param name="duration">存在时间。</param>
    /// <param name="pierce">穿透层数。</param>
    /// <param name="bounce">反弹层数。</param>
    /// <param name="arenaMin">战斗区域最小坐标。</param>
    /// <param name="arenaMax">战斗区域最大坐标。</param>
    /// <param name="impactDistance">命中击退距离。</param>
    public void Initialize(
        Vector3 direction,
        DamageHitInfo damageInfo,
        float speed,
        float duration,
        int pierce,
        int bounce,
        Vector2 arenaMin,
        Vector2 arenaMax,
        float impactDistance = 0f,
        int generation = 0,
        string rootTriggerId = "",
        ProjectileFireSourceType fireSourceType = ProjectileFireSourceType.NormalAttack)
    {
        _direction = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.right;
        _damageInfo = damageInfo.WithAmount(Mathf.Max(1, damageInfo.Amount));
        _remainingPierce = Mathf.Max(0, pierce);
        _remainingBounce = Mathf.Max(0, bounce);
        _generation = Mathf.Max(0, generation);
        _rootTriggerId = string.IsNullOrWhiteSpace(rootTriggerId) ? GetInstanceID().ToString() : rootTriggerId;
        _fireSourceType = fireSourceType;
        _impactDistance = Mathf.Max(0f, impactDistance);
        _arenaMin = arenaMin;
        _arenaMax = arenaMax;
        _hasArenaBounds = arenaMin != arenaMax;
        _boundaryCamera = Camera.main;
        _hitTargetIds.Clear();
        _tags.Clear();
        _hitRelicEffects.Clear();
        _expireRelicEffects.Clear();
        _timer = 0f;
        _hitBoundary = false;
        moveSpeed = Mathf.Max(0.1f, speed);
        lifeTime = Mathf.Max(0.1f, duration);
        SyncRotationToDirection();
    }

    /// <summary>
    /// 设置投射物携带的遗物效果快照。
    /// </summary>
    /// <param name="hitEffects">命中效果快照。</param>
    /// <param name="expireEffects">消失效果快照。</param>
    public void SetRelicEffectSnapshots(
        IReadOnlyList<IProjectileHitRelicEffect> hitEffects,
        IReadOnlyList<IProjectileExpireRelicEffect> expireEffects)
    {
        _hitRelicEffects.Clear();
        _expireRelicEffects.Clear();
        if (hitEffects != null)
            _hitRelicEffects.AddRange(hitEffects);

        if (expireEffects != null)
            _expireRelicEffects.AddRange(expireEffects);
    }

    /// <summary>
    /// 判断投射物是否带有指定标签。
    /// </summary>
    /// <param name="tag">标签。</param>
    /// <returns>存在标签时返回 true。</returns>
    public bool HasTag(string tag)
    {
        return !string.IsNullOrWhiteSpace(tag) && _tags.Contains(tag);
    }

    /// <summary>
    /// 给投射物添加运行时标签。
    /// </summary>
    /// <param name="tag">标签。</param>
    public void AddTag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
            _tags.Add(tag);
    }

    /// <summary>
    /// 消耗一次穿透机会。
    /// </summary>
    /// <returns>仍可继续飞行时返回 true。</returns>
    public bool TryConsumePierce()
    {
        if (_remainingPierce <= 0)
            return false;

        _remainingPierce--;
        return true;
    }

    /// <summary>
    /// 判断并记录本次命中的目标，同一轮飞行不会重复命中同一目标。
    /// </summary>
    /// <param name="target">命中目标。</param>
    /// <returns>首次命中该目标时返回 true。</returns>
    public bool TryRegisterHit(GameEnemyController target)
    {
        if (target == null)
            return false;

        return TryRegisterHit((object)target);
    }

    /// <summary>
    /// 判断并记录本次命中的通用目标，同一轮飞行不会重复命中同一目标。
    /// </summary>
    /// <param name="target">命中目标。</param>
    /// <returns>首次命中该目标时返回 true。</returns>
    public bool TryRegisterHit(object target)
    {
        if (target == null)
            return false;

        int targetId = target is Object unityObject ? unityObject.GetInstanceID() : target.GetHashCode();
        if (_hitTargetIds.Contains(targetId))
            return false;

        _hitTargetIds.Add(targetId);
        return true;
    }

    /// <summary>
    /// 按比例递减后续穿透命中的伤害。
    /// </summary>
    /// <param name="multiplier">伤害保留倍率。</param>
    public void ApplyPierceDamageDecay(float multiplier)
    {
        int nextDamage = Mathf.Max(1, Mathf.RoundToInt(_damageInfo.Amount * Mathf.Clamp01(multiplier)));
        _damageInfo = _damageInfo.WithAmount(nextDamage);
    }

    /// <summary>
    /// 检查边界并在还有次数时反弹。
    /// </summary>
    private void TickBoundaryCollision()
    {
        if (!TryGetActiveBounceBounds(out Vector2 boundsMin, out Vector2 boundsMax))
            return;

        Vector3 position = transform.position;
        bool hitHorizontalBoundary = position.x <= boundsMin.x || position.x >= boundsMax.x;
        bool hitVerticalBoundary = position.y <= boundsMin.y || position.y >= boundsMax.y;
        if (!hitHorizontalBoundary && !hitVerticalBoundary)
            return;

        if (_remainingBounce <= 0)
        {
            position.x = Mathf.Clamp(position.x, boundsMin.x, boundsMax.x);
            position.y = Mathf.Clamp(position.y, boundsMin.y, boundsMax.y);
            transform.position = position;
            _hitBoundary = true;
            return;
        }

        if (hitHorizontalBoundary)
        {
            _direction.x *= -1f;
            position.x = Mathf.Clamp(position.x, boundsMin.x, boundsMax.x);
        }

        if (hitVerticalBoundary)
        {
            _direction.y *= -1f;
            position.y = Mathf.Clamp(position.y, boundsMin.y, boundsMax.y);
        }

        _remainingBounce--;
        _hitTargetIds.Clear();
        transform.position = position;
        SyncRotationToDirection();
    }

    /// <summary>
    /// 让投射物表现朝向当前飞行方向。
    /// </summary>
    private void SyncRotationToDirection()
    {
        if (_direction.sqrMagnitude <= 0.01f)
            return;

        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>
    /// 获取当前可用于反弹的边界，地图边界和相机显示边界会共同限制投射物。
    /// </summary>
    /// <param name="boundsMin">有效边界最小坐标。</param>
    /// <param name="boundsMax">有效边界最大坐标。</param>
    /// <returns>存在可用边界时返回 true。</returns>
    private bool TryGetActiveBounceBounds(out Vector2 boundsMin, out Vector2 boundsMax)
    {
        boundsMin = Vector2.zero;
        boundsMax = Vector2.zero;
        bool hasBounds = false;

        if (_hasArenaBounds)
        {
            boundsMin = _arenaMin;
            boundsMax = _arenaMax;
            hasBounds = true;
        }

        if (TryGetDisplayBounds(out Vector2 displayMin, out Vector2 displayMax))
        {
            if (hasBounds)
            {
                boundsMin = new Vector2(Mathf.Max(boundsMin.x, displayMin.x), Mathf.Max(boundsMin.y, displayMin.y));
                boundsMax = new Vector2(Mathf.Min(boundsMax.x, displayMax.x), Mathf.Min(boundsMax.y, displayMax.y));
            }
            else
            {
                boundsMin = displayMin;
                boundsMax = displayMax;
                hasBounds = true;
            }
        }

        return hasBounds && boundsMin.x < boundsMax.x && boundsMin.y < boundsMax.y;
    }

    /// <summary>
    /// 将当前主相机视口转换为投射物所在 Z 平面的世界坐标边界。
    /// </summary>
    /// <param name="boundsMin">显示边界最小坐标。</param>
    /// <param name="boundsMax">显示边界最大坐标。</param>
    /// <returns>成功获取显示边界时返回 true。</returns>
    private bool TryGetDisplayBounds(out Vector2 boundsMin, out Vector2 boundsMax)
    {
        boundsMin = Vector2.zero;
        boundsMax = Vector2.zero;

        if (_boundaryCamera == null || !_boundaryCamera.isActiveAndEnabled)
            _boundaryCamera = Camera.main;

        if (_boundaryCamera == null)
            return false;

        float depth = transform.position.z - _boundaryCamera.transform.position.z;
        Vector3 bottomLeft = _boundaryCamera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 topRight = _boundaryCamera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        boundsMin = new Vector2(Mathf.Min(bottomLeft.x, topRight.x), Mathf.Min(bottomLeft.y, topRight.y));
        boundsMax = new Vector2(Mathf.Max(bottomLeft.x, topRight.x), Mathf.Max(bottomLeft.y, topRight.y));
        return true;
    }
}
