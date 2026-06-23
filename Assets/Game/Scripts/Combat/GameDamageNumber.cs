using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 控制单个伤害跳字的文本刷新、类型图标、固定轨迹动画和对象池回收。
/// </summary>
public sealed class GameDamageNumber : MonoBehaviour
{
    private const string IconObjectName = "DamageTypeIcon";

    private TMP_Text _text;
    private SpriteRenderer _iconRenderer;
    private GamePooledObject _pooledObject;
    private Sequence _sequence;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnDisable()
    {
        KillSequence();
    }

    /// <summary>
    /// 播放一次伤害跳字表现。
    /// </summary>
    /// <param name="hitInfo">伤害命中数据。</param>
    /// <param name="config">跳字表现配置。</param>
    public void Play(DamageHitInfo hitInfo, DamageNumberConfigData config)
    {
        CacheComponents();
        KillSequence();

        if (_text == null || config == null)
        {
            ReleaseToPool();
            return;
        }

        Color startColor = config.GetColor(hitInfo);
        startColor.a = 1f;
        _text.text = hitInfo.Amount.ToString();
        _text.color = startColor;
        _text.outlineWidth = 0f;
        _text.fontSize = config.GetFontSize(hitInfo.Amount);
        transform.localScale = Vector3.one;
        RefreshIcon(config.GetIcon(hitInfo), config);

        Vector3 startPosition = transform.position + Vector3.up * config.StartHeightOffset;
        Vector3 jumpDirection = Quaternion.Euler(0f, 0f, config.JumpAngle) * Vector3.right;
        Vector3 peakPosition = startPosition + jumpDirection.normalized * config.JumpDistance;
        Vector3 endPosition = peakPosition + Vector3.down * config.FallDistance;
        float jumpDuration = config.Duration * Mathf.Clamp(config.JumpPhaseRatio, 0.05f, 0.95f);
        float fallDuration = Mathf.Max(0.01f, config.Duration - jumpDuration);

        transform.position = startPosition;
        _sequence = DOTween.Sequence();
        _sequence.Append(transform.DOMove(peakPosition, jumpDuration).SetEase(config.JumpEase));
        _sequence.Append(transform.DOMove(endPosition, fallDuration).SetEase(config.FallEase));
        _sequence.Join(DOTween.ToAlpha(() => _text.color, color => _text.color = color, 0f, fallDuration).SetEase(config.FadeEase));
        Tween iconFadeTween = CreateIconFadeTween(fallDuration, config.FadeEase);
        if (iconFadeTween != null)
            _sequence.Join(iconFadeTween);

        _sequence.OnComplete(ReleaseToPool);
    }

    /// <summary>
    /// 播放一次治疗跳字表现，保持纯文本显示，不显示伤害类型图标。
    /// </summary>
    /// <param name="healAmount">实际恢复的红血点数。</param>
    /// <param name="config">跳字表现配置。</param>
    public void PlayHeal(int healAmount, DamageNumberConfigData config)
    {
        CacheComponents();
        KillSequence();

        if (_text == null || config == null || healAmount <= 0)
        {
            ReleaseToPool();
            return;
        }

        Color startColor = config.HealColor;
        startColor.a = 1f;
        _text.text = $"+{healAmount}";
        _text.color = startColor;
        _text.outlineColor = config.HealOutlineColor;
        _text.outlineWidth = config.HealOutlineWidth;
        _text.fontSize = config.GetHealFontSize(healAmount);
        HideIcon();

        Vector3 startPosition = transform.position + Vector3.up * config.HealStartHeightOffset;
        Vector3 endPosition = startPosition + Vector3.up * config.HealFloatDistance;
        float fadeDuration = Mathf.Max(0.01f, config.HealDuration - config.HealFadeDelay);

        transform.position = startPosition;
        transform.localScale = Vector3.one * 0.72f;
        _sequence = DOTween.Sequence();
        _sequence.Insert(0f, transform.DOMove(endPosition, config.HealDuration).SetEase(config.HealFloatEase));
        _sequence.Insert(0f, transform.DOScale(Vector3.one * config.HealPopScale, config.HealPopDuration).SetEase(config.HealPopEase));
        _sequence.Insert(config.HealPopDuration, transform.DOScale(Vector3.one, Mathf.Max(0.01f, config.HealPopDuration * 0.6f)).SetEase(Ease.OutQuad));
        _sequence.Insert(config.HealFadeDelay, DOTween.ToAlpha(() => _text.color, color => _text.color = color, 0f, fadeDuration).SetEase(config.FadeEase));
        _sequence.OnComplete(ReleaseToPool);
    }

    /// <summary>
    /// 缓存跳字运行所需组件。
    /// </summary>
    private void CacheComponents()
    {
        if (_text == null)
            _text = GetComponent<TMP_Text>();

        if (_iconRenderer == null)
        {
            Transform iconTransform = transform.Find(IconObjectName);
            if (iconTransform != null)
                _iconRenderer = iconTransform.GetComponent<SpriteRenderer>();
        }

        if (_pooledObject == null)
            _pooledObject = GetComponent<GamePooledObject>();
    }

    /// <summary>
    /// 根据当前文本边界刷新伤害类型图标的位置和尺寸。
    /// </summary>
    /// <param name="iconSprite">需要显示的图标；为空时隐藏图标。</param>
    /// <param name="config">跳字表现配置。</param>
    private void RefreshIcon(Sprite iconSprite, DamageNumberConfigData config)
    {
        if (iconSprite == null || config == null || _text == null)
        {
            HideIcon();
            return;
        }

        EnsureIconRenderer();
        if (_iconRenderer == null)
            return;

        _text.ForceMeshUpdate();
        Bounds textBounds = _text.textBounds;
        float textHeight = Mathf.Max(0.01f, textBounds.size.y);
        float iconHeight = textHeight * config.IconHeightMultiplier;
        float spriteHeight = Mathf.Max(0.01f, iconSprite.bounds.size.y);
        float iconScale = iconHeight / spriteHeight;
        float iconWidth = iconSprite.bounds.size.x * iconScale;
        float spacing = textHeight * config.IconSpacingMultiplier;

        _iconRenderer.sprite = iconSprite;
        _iconRenderer.color = Color.white;
        _iconRenderer.sortingLayerID = GetTextSortingLayerId();
        _iconRenderer.sortingOrder = GetTextSortingOrder() + config.IconSortingOrderOffset;
        _iconRenderer.transform.localRotation = Quaternion.identity;
        _iconRenderer.transform.localScale = Vector3.one * iconScale;
        _iconRenderer.transform.localPosition = new Vector3(
            textBounds.min.x - spacing - iconWidth * 0.5f,
            textBounds.center.y,
            0f);
        _iconRenderer.gameObject.SetActive(true);
    }

    /// <summary>
    /// 创建或恢复图标渲染器，用于显示数字前缀图标。
    /// </summary>
    private void EnsureIconRenderer()
    {
        if (_iconRenderer != null)
            return;

        Transform iconTransform = transform.Find(IconObjectName);
        if (iconTransform != null)
            _iconRenderer = iconTransform.GetComponent<SpriteRenderer>();

        if (_iconRenderer != null)
            return;

        GameObject iconObject = new GameObject(IconObjectName);
        iconObject.transform.SetParent(transform, false);
        _iconRenderer = iconObject.AddComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 获取文本渲染器当前使用的排序层 ID。
    /// </summary>
    /// <returns>文本排序层 ID；缺少渲染器时返回默认层。</returns>
    private int GetTextSortingLayerId()
    {
        MeshRenderer textRenderer = _text != null ? _text.GetComponent<MeshRenderer>() : null;
        return textRenderer != null ? textRenderer.sortingLayerID : 0;
    }

    /// <summary>
    /// 获取文本渲染器当前使用的排序顺序。
    /// </summary>
    /// <returns>文本排序顺序；缺少渲染器时返回 0。</returns>
    private int GetTextSortingOrder()
    {
        MeshRenderer textRenderer = _text != null ? _text.GetComponent<MeshRenderer>() : null;
        return textRenderer != null ? textRenderer.sortingOrder : 0;
    }

    /// <summary>
    /// 隐藏跳字图标，避免对象池复用时残留上一种伤害类型。
    /// </summary>
    private void HideIcon()
    {
        if (_iconRenderer != null)
            _iconRenderer.gameObject.SetActive(false);
    }

    /// <summary>
    /// 创建图标淡出动画；当前没有图标时返回空。
    /// </summary>
    /// <param name="duration">淡出持续时间。</param>
    /// <param name="ease">淡出缓动。</param>
    /// <returns>图标淡出动画。</returns>
    private Tween CreateIconFadeTween(float duration, Ease ease)
    {
        if (_iconRenderer == null || !_iconRenderer.gameObject.activeSelf)
            return null;

        return DOTween.ToAlpha(
            () => _iconRenderer.color,
            color => _iconRenderer.color = color,
            0f,
            duration).SetEase(ease);
    }

    /// <summary>
    /// 停止当前动画序列，避免对象池复用时残留旧动画。
    /// </summary>
    private void KillSequence()
    {
        if (_sequence == null)
            return;

        _sequence.Kill(false);
        _sequence = null;
    }

    /// <summary>
    /// 将跳字对象归还对象池。
    /// </summary>
    private void ReleaseToPool()
    {
        KillSequence();
        if (_pooledObject != null)
            _pooledObject.Release();
        else
            gameObject.SetActive(false);
    }
}
