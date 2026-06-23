using System.Globalization;
using UnityEngine;

/// <summary>
/// 提供玩家生命值到心形血量显示文本的转换。
/// </summary>
public static class HealthDisplayUtility
{
    private const int PointsPerHeart = 2;

    /// <summary>
    /// 将当前生命和最大生命格式化为心形血量文本。
    /// </summary>
    /// <param name="currentHealth">当前生命点数。</param>
    /// <param name="maxHealth">最大生命点数。</param>
    /// <returns>心形血量文本。</returns>
    public static string FormatHeartText(int currentHealth, int maxHealth)
    {
        string currentHearts = FormatHeartCount(currentHealth);
        string maxHearts = FormatHeartCount(maxHealth);
        return $"{currentHearts}/{maxHearts} 心";
    }

    /// <summary>
    /// 将生命点数格式化为心数，2 点生命等于 1 颗心。
    /// </summary>
    /// <param name="healthPoints">生命点数。</param>
    /// <returns>心数文本。</returns>
    public static string FormatHeartCount(int healthPoints)
    {
        float hearts = Mathf.Max(0, healthPoints) / (float)PointsPerHeart;
        return hearts.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
