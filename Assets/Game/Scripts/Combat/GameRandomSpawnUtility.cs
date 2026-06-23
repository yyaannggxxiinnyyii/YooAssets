using UnityEngine;

/// <summary>
/// 提供房间内随机生成点的通用筛选逻辑，统一处理边界留白、玩家安全距离和阻挡重叠检测。
/// </summary>
public static class GameRandomSpawnUtility
{
    private static readonly Collider2D[] OverlapResults = new Collider2D[8];

    /// <summary>
    /// 在指定矩形范围内尝试随机一个安全生成点。
    /// </summary>
    /// <param name="boundsMin">随机范围最小坐标。</param>
    /// <param name="boundsMax">随机范围最大坐标。</param>
    /// <param name="avoidPosition">需要避开的中心点，通常是玩家位置。</param>
    /// <param name="minDistanceFromAvoidPosition">距离避开中心点的最小距离。</param>
    /// <param name="boundaryPadding">距离范围边界的最小留白。</param>
    /// <param name="overlapRadius">用于检测阻挡重叠的半径。</param>
    /// <param name="overlapMask">阻挡检测层；为 0 时不检测重叠。</param>
    /// <param name="maxAttempts">最大尝试次数。</param>
    /// <param name="position">输出的安全位置。</param>
    /// <returns>找到安全位置时返回 true。</returns>
    public static bool TryGetRandomPoint(
        Vector2 boundsMin,
        Vector2 boundsMax,
        Vector3 avoidPosition,
        float minDistanceFromAvoidPosition,
        float boundaryPadding,
        float overlapRadius,
        LayerMask overlapMask,
        int maxAttempts,
        out Vector3 position)
    {
        position = Vector3.zero;
        Rect spawnRect = CreateSpawnRect(boundsMin, boundsMax, boundaryPadding);
        if (spawnRect.width <= 0f || spawnRect.height <= 0f)
            return false;

        float minDistanceSqr = Mathf.Max(0f, minDistanceFromAvoidPosition);
        minDistanceSqr *= minDistanceSqr;
        int safeAttempts = Mathf.Max(1, maxAttempts);
        for (int i = 0; i < safeAttempts; i++)
        {
            Vector3 candidate = new Vector3(
                Random.Range(spawnRect.xMin, spawnRect.xMax),
                Random.Range(spawnRect.yMin, spawnRect.yMax),
                avoidPosition.z);

            if (!IsFarEnough(candidate, avoidPosition, minDistanceSqr))
                continue;

            if (IsBlocked(candidate, overlapRadius, overlapMask))
                continue;

            position = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 按边界留白创建实际可随机的矩形范围。
    /// </summary>
    /// <param name="boundsMin">原始最小坐标。</param>
    /// <param name="boundsMax">原始最大坐标。</param>
    /// <param name="boundaryPadding">边界留白。</param>
    /// <returns>内缩后的随机矩形。</returns>
    private static Rect CreateSpawnRect(Vector2 boundsMin, Vector2 boundsMax, float boundaryPadding)
    {
        float padding = Mathf.Max(0f, boundaryPadding);
        float minX = Mathf.Min(boundsMin.x, boundsMax.x) + padding;
        float maxX = Mathf.Max(boundsMin.x, boundsMax.x) - padding;
        float minY = Mathf.Min(boundsMin.y, boundsMax.y) + padding;
        float maxY = Mathf.Max(boundsMin.y, boundsMax.y) - padding;
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// 判断候选点是否满足与避开点的最小距离。
    /// </summary>
    /// <param name="candidate">候选点。</param>
    /// <param name="avoidPosition">避开中心点。</param>
    /// <param name="minDistanceSqr">最小距离平方。</param>
    /// <returns>距离足够时返回 true。</returns>
    private static bool IsFarEnough(Vector3 candidate, Vector3 avoidPosition, float minDistanceSqr)
    {
        if (minDistanceSqr <= 0f)
            return true;

        Vector2 delta = candidate - avoidPosition;
        return delta.sqrMagnitude >= minDistanceSqr;
    }

    /// <summary>
    /// 判断候选点是否与配置的阻挡层发生重叠。
    /// </summary>
    /// <param name="candidate">候选点。</param>
    /// <param name="overlapRadius">重叠检测半径。</param>
    /// <param name="overlapMask">阻挡检测层。</param>
    /// <returns>存在阻挡时返回 true。</returns>
    private static bool IsBlocked(Vector3 candidate, float overlapRadius, LayerMask overlapMask)
    {
        if (overlapMask.value == 0 || overlapRadius <= 0f)
            return false;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(overlapMask);
        filter.useLayerMask = true;
        filter.useTriggers = false;
        return Physics2D.OverlapCircle(candidate, overlapRadius, filter, OverlapResults) > 0;
    }
}
