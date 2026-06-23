using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 提供战斗判定半径在 Scene 视图中的统一绘制能力。
/// </summary>
public static class GameRadiusGizmoUtility
{
    /// <summary>
    /// 在对象所在的 XY 平面绘制一个半径圈，并在编辑器中显示标签。
    /// </summary>
    /// <param name="center">圆心世界坐标。</param>
    /// <param name="radius">绘制半径。</param>
    /// <param name="color">绘制颜色。</param>
    /// <param name="label">编辑器标签。</param>
    public static void DrawRadius(Vector3 center, float radius, Color color, string label)
    {
        float safeRadius = Mathf.Max(0f, radius);
        if (safeRadius <= 0f)
            return;

#if UNITY_EDITOR
        Color previousColor = Handles.color;
        Handles.color = color;
        Handles.DrawWireDisc(center, Vector3.forward, safeRadius);
        if (!string.IsNullOrWhiteSpace(label))
            Handles.Label(center + new Vector3(safeRadius, 0f, 0f), label);

        Handles.color = previousColor;
#else
        Color previousColor = Gizmos.color;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(center, safeRadius);
        Gizmos.color = previousColor;
#endif
    }
}
