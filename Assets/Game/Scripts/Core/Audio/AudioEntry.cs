using System;
using UnityEngine;

/// <summary>
/// 单条音效配置数据，在 AudioManager 的 Inspector 列表中逐行填写。
/// </summary>
[Serializable]
public class AudioEntry
{
    /// <summary>此条目对应的音效枚举 ID。</summary>
    public SoundId id;

    /// <summary>要播放的 AudioClip 资源。</summary>
    public AudioClip clip;

    /// <summary>基础音量，范围 0–1。</summary>
    [Range(0f, 1f)]
    public float volume = 1f;

    /// <summary>音调随机范围的最小值（1 = 原始音调）。</summary>
    [Range(0.5f, 2f)]
    public float pitchMin = 1f;

    /// <summary>音调随机范围的最大值（1 = 原始音调）。</summary>
    [Range(0.5f, 2f)]
    public float pitchMax = 1f;
}

