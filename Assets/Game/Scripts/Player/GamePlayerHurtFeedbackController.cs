using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// 控制玩家受击无敌、闪烁、粒子和 Feel 反馈。
/// </summary>
public sealed class GamePlayerHurtFeedbackController : MonoBehaviour
{
    [Header("受伤反馈")]
    [Tooltip("玩家成功受到伤害后进入无敌状态的持续时间。")]
    [SerializeField] private float hurtInvincibleDuration = 0.8f;
    [Tooltip("玩家成功受到伤害时播放的 Feel 反馈，可在其中配置顿帧、镜头震动、音效等。")]
    [SerializeField] private MMF_Player hurtFeedbacks;
    [Tooltip("玩家成功受到伤害时在角色位置生成的粒子特效预制体。")]
    [SerializeField] private GameObject hurtEffectPrefab;
    [Tooltip("受伤粒子特效生成位置相对玩家根节点的偏移。")]
    [SerializeField] private Vector3 hurtEffectOffset = Vector3.zero;
    [Tooltip("受伤粒子特效实例自动销毁时间，小于等于 0 时不自动销毁。")]
    [SerializeField] private float hurtEffectDestroyDelay = 2f;
    [Tooltip("受伤无敌期间 SpriteRenderer 显隐闪烁的间隔。")]
    [SerializeField] private float hurtBlinkInterval = 0.08f;
    [Tooltip("受伤无敌闪烁隐藏帧的透明度倍率，0 表示完全透明。")]
    [SerializeField] private float hurtBlinkInvisibleAlphaMultiplier = 0f;
    [Tooltip("参与受伤无敌闪烁的玩家 SpriteRenderer。为空时会自动收集子节点中的 SpriteRenderer。")]
    [SerializeField] private SpriteRenderer[] hurtBlinkRenderers;

    private SpriteRenderer[] _runtimeHurtBlinkRenderers;
    private Color[] _runtimeHurtBlinkOriginalColors;
    private GameCompositeSpriteOutlineController[] _runtimeCompositeOutlineControllers;
    private float _hurtInvincibleTimer;
    private float _hurtBlinkTimer;
    private bool _hurtBlinkVisible = true;
    private bool _hurtFeedbacksMissingLogged;

    /// <summary>
    /// 受击无敌是否生效。
    /// </summary>
    public bool IsInvincible => _hurtInvincibleTimer > 0f;

    private void Awake()
    {
        CacheHurtBlinkRenderers();
    }

    private void OnDisable()
    {
        StopBlink();
    }

    /// <summary>
    /// 推进受伤无敌计时和闪烁显示。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    public void Tick(float deltaTime)
    {
        if (_hurtInvincibleTimer <= 0f)
            return;

        _hurtInvincibleTimer = Mathf.Max(0f, _hurtInvincibleTimer - Mathf.Max(0f, deltaTime));
        if (_hurtInvincibleTimer <= 0f)
        {
            StopBlink();
            return;
        }

        TickBlink(deltaTime);
    }

    /// <summary>
    /// 播放玩家受伤粒子特效并启动受伤无敌帧。
    /// </summary>
    public void Play()
    {
        PlayFeelFeedbacks();
        SpawnHurtEffect();
        _hurtInvincibleTimer = Mathf.Max(0f, hurtInvincibleDuration);
        _hurtBlinkTimer = 0f;
        CacheHurtBlinkRenderers();
        SetBlinkVisible(true);
    }

    /// <summary>
    /// 重置受击无敌和闪烁运行时状态。
    /// </summary>
    public void ResetRuntime()
    {
        _hurtInvincibleTimer = 0f;
        StopBlink();
    }

    /// <summary>
    /// 停止受伤闪烁并恢复渲染器显示。
    /// </summary>
    public void StopBlink()
    {
        _hurtBlinkTimer = 0f;
        SetBlinkVisible(true);
    }

    /// <summary>
    /// 设置玩家成功受伤时播放的 Feel 反馈播放器。
    /// </summary>
    /// <param name="feedbacks">受击反馈播放器。</param>
    public void SetHurtFeedbacks(MMF_Player feedbacks)
    {
        hurtFeedbacks = feedbacks;
        _hurtFeedbacksMissingLogged = false;
    }

    /// <summary>
    /// 推进受伤无敌期间的可见性闪烁。
    /// </summary>
    /// <param name="deltaTime">本帧时间。</param>
    private void TickBlink(float deltaTime)
    {
        float interval = Mathf.Max(0.01f, hurtBlinkInterval);
        _hurtBlinkTimer -= deltaTime;
        if (_hurtBlinkTimer > 0f)
            return;

        _hurtBlinkTimer = interval;
        SetBlinkVisible(!_hurtBlinkVisible);
    }

    /// <summary>
    /// 播放玩家受击时绑定的 Feel 反馈链。
    /// </summary>
    private void PlayFeelFeedbacks()
    {
        if (hurtFeedbacks == null)
        {
            if (!_hurtFeedbacksMissingLogged)
            {
                Debug.LogWarning($"[Player] 玩家 {name} 未配置受击 Feel 反馈。", this);
                _hurtFeedbacksMissingLogged = true;
            }

            return;
        }

        hurtFeedbacks.PlayFeedbacks(transform.position, hurtFeedbacks.FeedbacksIntensity);
    }

    /// <summary>
    /// 在玩家当前位置生成受伤粒子特效。
    /// </summary>
    private void SpawnHurtEffect()
    {
        if (hurtEffectPrefab == null)
            return;

        GameObject effectObject = Instantiate(hurtEffectPrefab, transform.position + hurtEffectOffset, Quaternion.identity, transform.parent);
        float destroyDelay = Mathf.Max(0f, hurtEffectDestroyDelay);
        if (destroyDelay > 0f)
            Destroy(effectObject, destroyDelay);
    }

    /// <summary>
    /// 缓存受伤闪烁使用的 SpriteRenderer。
    /// </summary>
    private void CacheHurtBlinkRenderers()
    {
        if (hurtBlinkRenderers != null && hurtBlinkRenderers.Length > 0)
        {
            _runtimeHurtBlinkRenderers = hurtBlinkRenderers;
            CacheCompositeOutlineControllers();
            CacheHurtBlinkOriginalColors();
            return;
        }

        SpriteRenderer[] childRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        List<SpriteRenderer> blinkRenderers = new List<SpriteRenderer>();
        for (int i = 0; i < childRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = childRenderers[i];
            if (spriteRenderer == null || IsWeaponRenderer(spriteRenderer))
                continue;

            blinkRenderers.Add(spriteRenderer);
        }

        _runtimeHurtBlinkRenderers = blinkRenderers.ToArray();
        CacheCompositeOutlineControllers();
        CacheHurtBlinkOriginalColors();
    }

    /// <summary>
    /// 判断渲染器是否属于玩家武器显示层。
    /// </summary>
    /// <param name="spriteRenderer">待检查渲染器。</param>
    /// <returns>属于武器显示层时返回 true。</returns>
    private bool IsWeaponRenderer(SpriteRenderer spriteRenderer)
    {
        Transform current = spriteRenderer.transform;
        while (current != null && current != transform)
        {
            if (current.name == "WeaponRoot")
                return true;

            current = current.parent;
        }

        return false;
    }

    /// <summary>
    /// 缓存玩家身上的组合描边控制器，受伤闪烁时需要同步显隐。
    /// </summary>
    private void CacheCompositeOutlineControllers()
    {
        _runtimeCompositeOutlineControllers = GetComponentsInChildren<GameCompositeSpriteOutlineController>(true);
    }

    /// <summary>
    /// 缓存受伤闪烁前的 SpriteRenderer 原始颜色。
    /// </summary>
    private void CacheHurtBlinkOriginalColors()
    {
        if (_runtimeHurtBlinkRenderers == null)
        {
            _runtimeHurtBlinkOriginalColors = null;
            return;
        }

        _runtimeHurtBlinkOriginalColors = new Color[_runtimeHurtBlinkRenderers.Length];
        for (int i = 0; i < _runtimeHurtBlinkRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = _runtimeHurtBlinkRenderers[i];
            _runtimeHurtBlinkOriginalColors[i] = spriteRenderer != null ? spriteRenderer.color : Color.white;
        }
    }

    /// <summary>
    /// 设置受伤闪烁渲染器的透明度状态。
    /// </summary>
    /// <param name="visible">是否显示。</param>
    private void SetBlinkVisible(bool visible)
    {
        _hurtBlinkVisible = visible;
        if (_runtimeHurtBlinkRenderers == null || _runtimeHurtBlinkRenderers.Length <= 0)
            CacheHurtBlinkRenderers();

        if (_runtimeHurtBlinkRenderers == null)
            return;

        for (int i = 0; i < _runtimeHurtBlinkRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = _runtimeHurtBlinkRenderers[i];
            if (spriteRenderer == null)
                continue;

            Color color = spriteRenderer.color;
            float originalAlpha = GetHurtBlinkOriginalAlpha(i);
            color.a = visible ? originalAlpha : originalAlpha * Mathf.Clamp01(hurtBlinkInvisibleAlphaMultiplier);
            spriteRenderer.color = color;
        }

        SetCompositeOutlineVisible(visible);
    }

    /// <summary>
    /// 同步组合描边显示状态。
    /// </summary>
    /// <param name="visible">是否显示。</param>
    private void SetCompositeOutlineVisible(bool visible)
    {
        if (_runtimeCompositeOutlineControllers == null || _runtimeCompositeOutlineControllers.Length <= 0)
            CacheCompositeOutlineControllers();

        if (_runtimeCompositeOutlineControllers == null)
            return;

        for (int i = 0; i < _runtimeCompositeOutlineControllers.Length; i++)
        {
            GameCompositeSpriteOutlineController outlineController = _runtimeCompositeOutlineControllers[i];
            if (outlineController != null)
                outlineController.SetOutlineVisible(visible);
        }
    }

    /// <summary>
    /// 获取指定闪烁渲染器的原始透明度。
    /// </summary>
    /// <param name="index">渲染器索引。</param>
    /// <returns>原始透明度。</returns>
    private float GetHurtBlinkOriginalAlpha(int index)
    {
        if (_runtimeHurtBlinkOriginalColors == null ||
            index < 0 ||
            index >= _runtimeHurtBlinkOriginalColors.Length)
        {
            return 1f;
        }

        return _runtimeHurtBlinkOriginalColors[index].a;
    }
}
