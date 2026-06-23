using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 音效管理器（单例）。
/// 职责：
///   - 维护 SFX 对象池（复用 AudioSource，避免高频场景时的 GC）。
///   - 通过 <see cref="SoundId"/> 枚举查找并播放对应 <see cref="AudioClip"/>。
///   - 管理多条 BGM 轨道：
///       主轨道  — 全程播放，可通过 PlayBgm() 替换当前曲目
///       非主轨道 — 点击开始游戏时启动（StartSecondaryBgms），进入结算时暂停（PauseSecondaryBgms），可恢复（ResumeSecondaryBgms）
///   - 全局音量控制（SFX / BGM），持久化到 PlayerPrefs。
///
/// 使用方法：
///   AudioManager.Instance.PlaySfx(SoundId.ButtonClick);
///   AudioManager.Instance.PlayBgm(clip);          // 替换主轨道曲目
///   AudioManager.Instance.StartSecondaryBgms();   // 点击开始游戏时调用
///   AudioManager.Instance.PauseSecondaryBgms();   // 进入结算时调用
///   AudioManager.Instance.ResumeSecondaryBgms();  // 结算结束回到游戏时调用
/// </summary>
public class AudioManager : Singleton<AudioManager>
{
    // ── PlayerPrefs 键名 ──────────────────────────────────────

    private const string PrefKeySfxVolume = "SfxVolume";
    private const string PrefKeyBgmVolume = "BgmVolume";

    // ── Inspector 配置 ────────────────────────────────────────

    [Header("音效条目列表")]
    [Tooltip("将每个 SoundId 与对应 AudioClip / 音量 / 音调随机范围绑定。")]
    [SerializeField] private AudioEntry[] _entries;

    [Header("音效池")]
    [Tooltip("预生成的 SFX AudioSource 数量，高频音效场景建议 8–16。")]
    [SerializeField] private int _poolSize = 10;

    [Header("BGM 轨道列表")]
    [Tooltip("配置所有 BGM 轨道。isMainTrack=true 为主轨道（全程播放），false 为非主轨道（开始游戏→结算期间播放）。")]
    [SerializeField] private BgmTrackConfig[] _bgmTracks;

    [Tooltip("BGM 淡出 / 淡入默认时长（秒）。")]
    [SerializeField] private float _bgmFadeDuration = 0.5f;

    // ── 运行时字段 ────────────────────────────────────────────

    /// <summary>SoundId → AudioEntry 快速查找字典，Awake 时构建。</summary>
    private Dictionary<SoundId, AudioEntry> _entryMap;

    /// <summary>SFX 对象池队列。</summary>
    private Queue<AudioSource> _pool;

    /// <summary>主轨道 AudioSource（只有一条）。</summary>
    private AudioSource _mainTrackSource;

    /// <summary>主轨道当前目标音量（受全局 BGM 音量影响）。</summary>
    private float _mainTrackBaseVolume = 1f;

    /// <summary>非主轨道 AudioSource 列表（与 _bgmTracks 中非主轨道一一对应）。</summary>
    private readonly List<AudioSource> _secondaryTrackSources = new();

    /// <summary>非主轨道对应的基础音量列表。</summary>
    private readonly List<float> _secondaryTrackBaseVolumes = new();

    /// <summary>全局 SFX 音量系数（0–1）。</summary>
    private float _sfxVolume = 1f;

    /// <summary>全局 BGM 音量上限（0–1）。</summary>
    private float _bgmVolume = 1f;

    /// <summary>全局旁白音量系数（0–1）。</summary>
    private float _narrationVolume = 1f;

    // ── 公开属性 ──────────────────────────────────────────────

    public float SfxVolume      => _sfxVolume;
    public float BgmVolume      => _bgmVolume;
    public float NarrationVolume => _narrationVolume;

    // ── 生命周期 ──────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        BuildEntryMap();
        BuildPool();
        BuildBgmTracks();
        LoadVolumeSettings();
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // autoPlayOnStart 的轨道在 Start 里启动（Awake 时 AudioSource 刚创建，Start 更安全）
        if (_bgmTracks == null) return;

        int secondaryIdx = 0;
        foreach (var track in _bgmTracks)
        {
            if (track.isMainTrack)
            {
                if (track.autoPlayOnStart)
                    FadeInSource(_mainTrackSource, _mainTrackBaseVolume, _bgmFadeDuration);
            }
            else
            {
                if (track.autoPlayOnStart && secondaryIdx < _secondaryTrackSources.Count)
                    FadeInSource(_secondaryTrackSources[secondaryIdx], _secondaryTrackBaseVolumes[secondaryIdx], _bgmFadeDuration);
                secondaryIdx++;
            }
        }
    }

    // ── 全局音量 API ──────────────────────────────────────────

    /// <summary>设置全局 SFX 音量并持久化。</summary>
    public void SetSfxVolume(float v)
    {
        _sfxVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(PrefKeySfxVolume, _sfxVolume);
        PlayerPrefs.Save();
    }

    /// <summary>设置全局 BGM 音量并持久化，实时更新所有正在播放的 BGM 轨道。</summary>
    public void SetBgmVolume(float v)
    {
        _bgmVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(PrefKeyBgmVolume, _bgmVolume);
        PlayerPrefs.Save();

        // 更新主轨道
        if (_mainTrackSource != null && _mainTrackSource.isPlaying)
            _mainTrackSource.volume = _mainTrackBaseVolume * _bgmVolume;

        // 更新非主轨道
        for (int i = 0; i < _secondaryTrackSources.Count; i++)
        {
            var src = _secondaryTrackSources[i];
            if (src != null && src.isPlaying)
                src.volume = _secondaryTrackBaseVolumes[i] * _bgmVolume;
        }
    }

    /// <summary>设置全局旁白音量（不持久化）。</summary>
    public void SetNarrationVolume(float v) => _narrationVolume = Mathf.Clamp01(v);

    // ── SFX API ──────────────────────────────────────────────

    /// <summary>播放一次性音效。</summary>
    public void PlaySfx(SoundId id)
    {
        if (!_entryMap.TryGetValue(id, out var entry))
        {
            Debug.LogWarning($"[AudioManager] 找不到 SoundId={id} 的配置，请检查 Inspector 列表。");
            return;
        }
        if (entry.clip == null)
        {
            Debug.LogWarning($"[AudioManager] SoundId={id} 的 AudioClip 为空。");
            return;
        }

        AudioSource src = RentSource();
        src.clip   = entry.clip;
        src.volume = entry.volume * _sfxVolume;
        src.pitch  = Random.Range(entry.pitchMin, entry.pitchMax);
        src.Play();

        float delay = entry.clip.length / Mathf.Abs(src.pitch) + 0.05f;
        StartCoroutine(ReturnSourceDelayed(src, delay));
    }

    // ── 主轨道 BGM API ────────────────────────────────────────

    /// <summary>
    /// 替换主轨道曲目（淡出当前 → 淡入新曲）。
    /// 传入 null 则只淡出不播放。
    /// </summary>
    public void PlayBgm(AudioClip clip, float fadeDuration = -1f)
    {
        if (_mainTrackSource == null)
        {
            Debug.LogWarning("[AudioManager] 未配置主轨道，请在 Inspector 中将某条 BgmTrackConfig.isMainTrack 设为 true。");
            return;
        }

        float duration = fadeDuration < 0f ? _bgmFadeDuration : fadeDuration;
        _mainTrackSource.DOKill();

        if (_mainTrackSource.isPlaying)
        {
            DOTween.To(() => _mainTrackSource.volume, x => _mainTrackSource.volume = x, 0f, duration)
                   .OnComplete(() => SwapMainBgm(clip, duration));
        }
        else
        {
            SwapMainBgm(clip, duration);
        }
    }

    /// <summary>
    /// 切回主轨道的默认曲目（即配置列表中主轨道的原始 clip）。
    /// 等同于 PlayBgm(originalClip)，供结算结束回主菜单时调用。
    /// </summary>
    public void PlayDefaultBgm(float fadeDuration = -1f)
    {
        if (_mainTrackSource == null) return;
        // _mainTrackSource.clip 可能已被替换，从配置里取原始 clip
        AudioClip defaultClip = null;
        if (_bgmTracks != null)
        {
            foreach (var t in _bgmTracks)
            {
                if (t.isMainTrack) { defaultClip = t.clip; break; }
            }
        }
        if (defaultClip == null) return;
        PlayBgm(defaultClip, fadeDuration);
    }

    /// <summary>立即停止主轨道（无淡出）。</summary>
    public void StopBgm()
    {
        if (_mainTrackSource == null) return;
        _mainTrackSource.DOKill();
        _mainTrackSource.Stop();
    }

    /// <summary>暂停 / 恢复主轨道。</summary>
    public void SetBgmPaused(bool paused)
    {
        if (_mainTrackSource == null) return;
        if (paused) _mainTrackSource.Pause();
        else        _mainTrackSource.UnPause();
    }

    // ── 非主轨道 BGM API ──────────────────────────────────────

    /// <summary>
    /// 启动所有非主轨道（点击开始游戏时调用）。
    /// 已在播放的轨道不会重新开始。
    /// </summary>
    public void StartSecondaryBgms(float fadeDuration = -1f)
    {
        float duration = fadeDuration < 0f ? _bgmFadeDuration : fadeDuration;
        for (int i = 0; i < _secondaryTrackSources.Count; i++)
        {
            var src = _secondaryTrackSources[i];
            if (src == null) continue;
            if (src.isPlaying) continue;  // 已在播放则跳过

            // 从头开始播放并淡入
            src.volume = 0f;
            src.Play();
            src.DOKill();
            DOTween.To(() => src.volume, x => src.volume = x,
                       _secondaryTrackBaseVolumes[i] * _bgmVolume, duration);

            Debug.Log($"[AudioManager] 启动次轨道: {src.gameObject.name}");
        }
    }

    /// <summary>
    /// 暂停所有非主轨道（进入结算界面时调用）。
    /// </summary>
    public void PauseSecondaryBgms(float fadeDuration = -1f)
    {
        float duration = fadeDuration < 0f ? _bgmFadeDuration : fadeDuration;
        foreach (var src in _secondaryTrackSources)
        {
            if (src == null || !src.isPlaying) continue;
            src.DOKill();
            DOTween.To(() => src.volume, x => src.volume = x, 0f, duration)
                   .OnComplete(() => src.Pause());
        }
    }

    /// <summary>
    /// 恢复所有非主轨道（结算结束回到游戏时调用）。
    /// </summary>
    public void ResumeSecondaryBgms(float fadeDuration = -1f)
    {
        float duration = fadeDuration < 0f ? _bgmFadeDuration : fadeDuration;
        for (int i = 0; i < _secondaryTrackSources.Count; i++)
        {
            var src = _secondaryTrackSources[i];
            if (src == null) continue;
            src.UnPause();
            FadeInSource(src, _secondaryTrackBaseVolumes[i], duration);
        }
    }

    /// <summary>
    /// 停止所有非主轨道并重置到开头（彻底结束游戏会话时调用，如回到主菜单）。
    /// </summary>
    public void StopSecondaryBgms(float fadeDuration = -1f)
    {
        float duration = fadeDuration < 0f ? _bgmFadeDuration : fadeDuration;
        foreach (var src in _secondaryTrackSources)
        {
            if (src == null) continue;
            src.DOKill();
            DOTween.To(() => src.volume, x => src.volume = x, 0f, duration)
                   .OnComplete(() => { src.Stop(); src.time = 0f; });
        }
    }

    // ── 内部方法 ──────────────────────────────────────────────

    private void LoadVolumeSettings()
    {
        _sfxVolume       = PlayerPrefs.GetFloat(PrefKeySfxVolume, 1f);
        _bgmVolume       = PlayerPrefs.GetFloat(PrefKeyBgmVolume, 1f);
        _narrationVolume = _sfxVolume;
    }

    private void BuildEntryMap()
    {
        _entryMap = new Dictionary<SoundId, AudioEntry>(_entries?.Length ?? 0);
        if (_entries == null) return;
        foreach (var entry in _entries)
        {
            if (_entryMap.ContainsKey(entry.id))
            {
                Debug.LogWarning($"[AudioManager] SoundId={entry.id} 重复配置，忽略后续。");
                continue;
            }
            _entryMap[entry.id] = entry;
        }
    }

    private void BuildPool()
    {
        _pool = new Queue<AudioSource>(_poolSize);
        for (int i = 0; i < _poolSize; i++)
            _pool.Enqueue(CreatePooledSource(i));
    }

    /// <summary>根据 _bgmTracks 配置创建对应的 AudioSource 子对象。</summary>
    private void BuildBgmTracks()
    {
        if (_bgmTracks == null) return;

        bool mainFound = false;
        int secondaryIdx = 0;

        foreach (var track in _bgmTracks)
        {
            if (track.clip == null)
            {
                Debug.LogWarning($"[AudioManager] BGM 轨道「{track.trackName}」的 AudioClip 为空，已跳过。");
                if (!track.isMainTrack) secondaryIdx++;
                continue;
            }

            var go  = new GameObject($"BGM_{track.trackName}");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.clip        = track.clip;
            src.loop        = true;
            src.playOnAwake = false;
            src.volume      = 0f; // 统一从 0 开始，由 Start() 或 API 调用淡入

            if (track.isMainTrack)
            {
                if (mainFound)
                    Debug.LogWarning($"[AudioManager] 检测到多条主轨道，只使用第一条，「{track.trackName}」将被忽略。");
                else
                {
                    _mainTrackSource      = src;
                    _mainTrackBaseVolume  = track.volume;
                    mainFound             = true;
                }
            }
            else
            {
                _secondaryTrackSources.Add(src);
                _secondaryTrackBaseVolumes.Add(track.volume);
                Debug.Log($"[AudioManager] 注册次轨道[{_secondaryTrackSources.Count - 1}]: {track.trackName}");
                secondaryIdx++;
            }
        }

        Debug.Log($"[AudioManager] BGM 轨道构建完成，主轨道: {(mainFound ? "已找到" : "未找到")}，次轨道数量: {_secondaryTrackSources.Count}");
    }

    private void SwapMainBgm(AudioClip clip, float fadeDuration)
    {
        if (clip == null) { _mainTrackSource.Stop(); return; }
        _mainTrackSource.clip   = clip;
        _mainTrackSource.volume = 0f;
        _mainTrackSource.loop   = true;
        _mainTrackSource.Play();
        DOTween.To(() => _mainTrackSource.volume, x => _mainTrackSource.volume = x,
                   _mainTrackBaseVolume * _bgmVolume, fadeDuration);
    }

    /// <summary>将 AudioSource 从当前音量淡入到目标音量（受全局 BGM 音量影响）。</summary>
    private void FadeInSource(AudioSource src, float baseVolume, float duration)
    {
        if (src == null) return;
        if (!src.isPlaying) src.Play();
        src.DOKill();
        DOTween.To(() => src.volume, x => src.volume = x,
                   baseVolume * _bgmVolume, duration);
    }

    private AudioSource CreatePooledSource(int index)
    {
        var go  = new GameObject($"SFX_Pooled_{index:D2}");
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        return src;
    }

    private AudioSource RentSource()
    {
        if (_pool.Count > 0) return _pool.Dequeue();
        Debug.LogWarning("[AudioManager] 音效池已耗尽，临时扩容。建议增大 Pool Size。");
        return CreatePooledSource(_poolSize++);
    }

    private IEnumerator ReturnSourceDelayed(AudioSource src, float delay)
    {
        yield return new WaitForSeconds(delay);
        src.Stop();
        src.clip = null;
        _pool.Enqueue(src);
    }
}
