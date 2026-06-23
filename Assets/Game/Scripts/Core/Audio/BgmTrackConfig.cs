using System;
using UnityEngine;

/// <summary>
/// 单条 BGM 轨道配置，在 AudioManager Inspector 列表中逐行填写。
/// </summary>
[Serializable]
public class BgmTrackConfig
{
    [Tooltip("轨道名称，仅用于 Inspector 识别")]
    public string trackName = "BGM Track";

    [Tooltip("要循环播放的 AudioClip")]
    public AudioClip clip;

    [Tooltip("基础音量（0–1），受全局 BGM 音量系数影响）")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("是否为主轨道：主轨道全程播放，可通过 PlayBgm() 替换；非主轨道在点击开始游戏时启动，进入结算时暂停")]
    public bool isMainTrack = false;

    [Tooltip("是否在游戏启动时自动播放（主轨道通常勾选，非主轨道通常不勾选）")]
    public bool autoPlayOnStart = false;
}
