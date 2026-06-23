using System;
using UnityEngine;

/// <summary>
/// 临时特殊血量的类型定义。
/// </summary>
public enum SpecialHealthType
{
    Blue,
    Pink,
    Glass,
    Explosive
}

/// <summary>
/// 记录一段连续的临时特殊血量。
/// </summary>
[Serializable]
public struct SpecialHealthSegment
{
    [SerializeField] private SpecialHealthType type;
    [SerializeField] private int points;

    /// <summary>
    /// 创建一段临时特殊血量，并保证点数不小于 0。
    /// </summary>
    /// <param name="type">特殊血类型。</param>
    /// <param name="points">特殊血点数。</param>
    public SpecialHealthSegment(SpecialHealthType type, int points)
    {
        this.type = type;
        this.points = Mathf.Max(0, points);
    }

    /// <summary>
    /// 特殊血类型。
    /// </summary>
    public SpecialHealthType Type => type;

    /// <summary>
    /// 特殊血点数。
    /// </summary>
    public int Points => points;

    /// <summary>
    /// 使用新的点数创建同类型血量段。
    /// </summary>
    /// <param name="newPoints">新的特殊血点数。</param>
    /// <returns>更新后的特殊血量段。</returns>
    public SpecialHealthSegment WithPoints(int newPoints)
    {
        return new SpecialHealthSegment(type, newPoints);
    }
}
