using UnityEngine;

/// <summary>
/// 控制玩家移动范围限制和场景阻挡检测。
/// </summary>
public sealed class GamePlayerSceneCollisionController : MonoBehaviour
{
    [Header("场景碰撞")]
    [Tooltip("是否启用玩家移动前的场景阻挡检测；启用后仍然由脚本直接设置位置，不使用物理速度。")]
    [SerializeField] private bool sceneCollisionEnabled = true;
    [Tooltip("玩家用于检测门、宝箱、浆果丛等阻挡物的身体半径。")]
    [SerializeField] private float sceneCollisionRadius = 0.4f;
    [Tooltip("场景阻挡检测圆心相对玩家根节点的偏移，用于让碰撞主体贴合角色脚底或身体下半部分。")]
    [SerializeField] private Vector2 sceneCollisionOffset = Vector2.zero;
    [Tooltip("会阻挡玩家移动的场景层，例如 SolidObstacle。")]
    [SerializeField] private LayerMask sceneCollisionMask;
    [Tooltip("碰撞检测时额外预留的距离，避免玩家贴得过深或高速移动时穿过薄阻挡。")]
    [SerializeField] private float sceneCollisionSkinWidth = 0.02f;
    [Tooltip("撞到阻挡物时是否尝试沿单轴滑动，关闭后碰撞会直接停止本次移动。")]
    [SerializeField] private bool sceneCollisionSlideEnabled = true;

    private readonly RaycastHit2D[] _sceneCollisionHits = new RaycastHit2D[8];
    private Vector2 _movementMin;
    private Vector2 _movementMax;
    private bool _hasMovementBounds;

    private void OnDrawGizmos()
    {
        if (sceneCollisionEnabled)
            GameRadiusGizmoUtility.DrawRadius(GetSceneCollisionCenter(transform.position), Mathf.Max(0.01f, sceneCollisionRadius), new Color(0.05f, 1f, 0.45f, 0.9f), "Scene Collision");
    }

    /// <summary>
    /// 设置玩家移动边界，后续移动会被限制在该矩形区域内。
    /// </summary>
    /// <param name="minPosition">可移动区域最小坐标。</param>
    /// <param name="maxPosition">可移动区域最大坐标。</param>
    public void SetMovementBounds(Vector2 minPosition, Vector2 maxPosition)
    {
        _movementMin = minPosition;
        _movementMax = maxPosition;
        _hasMovementBounds = true;

        transform.position = ClampToBounds(transform.position);
    }

    /// <summary>
    /// 解析玩家本帧目标位置，依次应用移动边界和场景阻挡。
    /// </summary>
    /// <param name="currentPosition">当前位置。</param>
    /// <param name="targetPosition">期望目标位置。</param>
    /// <returns>修正后的目标位置。</returns>
    public Vector3 ResolvePosition(Vector3 currentPosition, Vector3 targetPosition)
    {
        Vector3 nextPosition = ClampToBounds(targetPosition);
        return ResolveSceneCollision(currentPosition, nextPosition);
    }

    /// <summary>
    /// 将指定位置限制到当前移动边界内。
    /// </summary>
    /// <param name="position">待限制的位置。</param>
    /// <returns>限制后的坐标。</returns>
    private Vector3 ClampToBounds(Vector3 position)
    {
        if (!_hasMovementBounds)
            return position;

        position.x = Mathf.Clamp(position.x, _movementMin.x, _movementMax.x);
        position.y = Mathf.Clamp(position.y, _movementMin.y, _movementMax.y);
        return position;
    }

    /// <summary>
    /// 根据配置的阻挡层修正玩家目标位置，命中障碍时优先尝试沿单轴滑动。
    /// </summary>
    /// <param name="currentPosition">当前玩家位置。</param>
    /// <param name="targetPosition">本帧期望移动到的位置。</param>
    /// <returns>经过场景阻挡修正后的最终位置。</returns>
    private Vector3 ResolveSceneCollision(Vector3 currentPosition, Vector3 targetPosition)
    {
        if (!ShouldCheckSceneCollision())
            return targetPosition;

        if (!IsSceneMovementBlocked(currentPosition, targetPosition))
            return targetPosition;

        if (!sceneCollisionSlideEnabled)
            return currentPosition;

        Vector3 delta = targetPosition - currentPosition;
        Vector3 firstSlideTarget;
        Vector3 secondSlideTarget;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
        {
            firstSlideTarget = new Vector3(targetPosition.x, currentPosition.y, targetPosition.z);
            secondSlideTarget = new Vector3(currentPosition.x, targetPosition.y, targetPosition.z);
        }
        else
        {
            firstSlideTarget = new Vector3(currentPosition.x, targetPosition.y, targetPosition.z);
            secondSlideTarget = new Vector3(targetPosition.x, currentPosition.y, targetPosition.z);
        }

        if (!IsSceneMovementBlocked(currentPosition, firstSlideTarget))
            return firstSlideTarget;

        if (!IsSceneMovementBlocked(currentPosition, secondSlideTarget))
            return secondSlideTarget;

        return currentPosition;
    }

    /// <summary>
    /// 判断当前玩家是否需要执行场景阻挡检测。
    /// </summary>
    /// <returns>配置有效且存在阻挡层时返回 true。</returns>
    private bool ShouldCheckSceneCollision()
    {
        return sceneCollisionEnabled && sceneCollisionMask.value != 0 && sceneCollisionRadius > 0f;
    }

    /// <summary>
    /// 使用 CircleCast 检查从当前位置到目标位置的移动路径是否被场景阻挡。
    /// </summary>
    /// <param name="currentPosition">移动起点。</param>
    /// <param name="targetPosition">移动终点。</param>
    /// <returns>路径被阻挡时返回 true。</returns>
    private bool IsSceneMovementBlocked(Vector3 currentPosition, Vector3 targetPosition)
    {
        Vector2 start = GetSceneCollisionCenter(currentPosition);
        Vector2 end = GetSceneCollisionCenter(targetPosition);
        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return false;

        ContactFilter2D filter = CreateSceneCollisionFilter();
        int hitCount = Physics2D.CircleCast(
            start,
            Mathf.Max(0.01f, sceneCollisionRadius),
            delta / distance,
            filter,
            _sceneCollisionHits,
            distance + Mathf.Max(0f, sceneCollisionSkinWidth));

        for (int i = 0; i < hitCount; i++)
        {
            if (IsBlockingSceneHit(_sceneCollisionHits[i]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 根据玩家根节点位置和配置偏移计算实际用于场景阻挡检测的圆心。
    /// </summary>
    /// <param name="playerPosition">玩家根节点位置。</param>
    /// <returns>应用偏移后的场景阻挡检测圆心。</returns>
    private Vector3 GetSceneCollisionCenter(Vector3 playerPosition)
    {
        return playerPosition + new Vector3(sceneCollisionOffset.x, sceneCollisionOffset.y, 0f);
    }

    /// <summary>
    /// 创建只检测阻挡层且忽略 Trigger 的 2D 物理过滤器。
    /// </summary>
    /// <returns>场景阻挡检测过滤器。</returns>
    private ContactFilter2D CreateSceneCollisionFilter()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(sceneCollisionMask);
        filter.useLayerMask = true;
        filter.useTriggers = false;
        return filter;
    }

    /// <summary>
    /// 判断一次 CircleCast 命中是否属于有效场景阻挡，并忽略玩家自身碰撞体。
    /// </summary>
    /// <param name="hit">CircleCast 命中结果。</param>
    /// <returns>有效阻挡返回 true。</returns>
    private bool IsBlockingSceneHit(RaycastHit2D hit)
    {
        if (hit.collider == null)
            return false;

        Transform hitTransform = hit.collider.transform;
        return hitTransform != transform && !hitTransform.IsChildOf(transform);
    }
}
