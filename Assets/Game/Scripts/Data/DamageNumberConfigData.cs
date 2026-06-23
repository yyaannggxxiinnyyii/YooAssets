using DG.Tweening;
using UnityEngine;

/// <summary>
/// 配置伤害跳字的颜色、字号缩放、移动轨迹和持续时间。
/// </summary>
[CreateAssetMenu(fileName = "DamageNumberConfigData", menuName = "Game/Data/Damage Number Config Data")]
public sealed class DamageNumberConfigData : ScriptableObject
{
    [Header("时间")]
    [Tooltip("伤害跳字从出现到回收的总持续时间。")]
    [SerializeField] private float duration = 0.85f;
    [Tooltip("伤害跳字上跳阶段占总时长的比例。")]
    [SerializeField] private float jumpPhaseRatio = 0.42f;

    [Header("位移")]
    [Tooltip("伤害跳字生成时相对受击点向上的初始高度。")]
    [SerializeField] private float startHeightOffset = 0.42f;
    [Tooltip("伤害跳字上跳阶段沿跳动方向移动的距离。")]
    [SerializeField] private float jumpDistance = 0.78f;
    [Tooltip("伤害跳字下落阶段向下移动的距离。")]
    [SerializeField] private float fallDistance = 0.36f;
    [Tooltip("伤害跳字上跳方向角度。")]
    [SerializeField] private float jumpAngle = 45f;
    [Tooltip("短时间连续跳字之间沿跳动方向拉开的距离。")]
    [SerializeField] private float stackSpacing = 0.16f;
    [Tooltip("判定连续跳字的时间窗口。")]
    [SerializeField] private float stackTimeWindow = 0.18f;
    [Tooltip("判定连续跳字是否属于同一显示区域的半径。")]
    [SerializeField] private float stackPositionRadius = 0.55f;
    [Tooltip("连续跳字最多累计错开的次数。")]
    [SerializeField] private int maxStackOffsetCount = 5;

    [Header("字号")]
    [Tooltip("伤害跳字基础字号。")]
    [SerializeField] private float baseFontSize = 3.2f;
    [Tooltip("伤害跳字最小字号。")]
    [SerializeField] private float minFontSize = 2.4f;
    [Tooltip("伤害跳字最大字号。")]
    [SerializeField] private float maxFontSize = 6.4f;
    [Tooltip("按伤害数值调整伤害跳字大小的曲线。")]
    [SerializeField] private AnimationCurve sizeByDamage = AnimationCurve.Linear(0f, 1f, 100f, 1.8f);

    [Header("伤害颜色")]
    [Tooltip("普通伤害跳字颜色。")]
    [SerializeField] private Color normalColor = Color.white;
    [Tooltip("暴击伤害跳字颜色。")]
    [SerializeField] private Color criticalColor = new Color(1f, 0.52f, 0.12f);
    [Tooltip("火焰伤害跳字颜色。")]
    [SerializeField] private Color fireColor = new Color(1f, 0.16f, 0.12f);
    [Tooltip("冰霜伤害跳字颜色。")]
    [SerializeField] private Color iceColor = new Color(0.28f, 0.62f, 1f);
    [Tooltip("毒素伤害跳字颜色。")]
    [SerializeField] private Color poisonColor = new Color(0.25f, 0.95f, 0.32f);
    [Tooltip("电击伤害跳字颜色。")]
    [SerializeField] private Color electricColor = new Color(1f, 0.9f, 0.18f);

    [Header("跳字图标")]
    [Tooltip("暴击伤害跳字前显示的图标；为空时不显示图标。")]
    [SerializeField] private Sprite criticalIcon;
    [Tooltip("火焰伤害跳字前显示的图标；为空时不显示图标。")]
    [SerializeField] private Sprite fireIcon;
    [Tooltip("冰霜伤害跳字前显示的图标；为空时不显示图标。")]
    [SerializeField] private Sprite iceIcon;
    [Tooltip("毒素伤害跳字前显示的图标；为空时不显示图标。")]
    [SerializeField] private Sprite poisonIcon;
    [Tooltip("电击伤害跳字前显示的图标；为空时不显示图标。")]
    [SerializeField] private Sprite electricIcon;
    [Tooltip("图标高度相对当前数字文本高度的倍率。")]
    [SerializeField] private float iconHeightMultiplier = 0.85f;
    [Tooltip("图标与数字左边界之间的间距，相对当前数字文本高度。")]
    [SerializeField] private float iconSpacingMultiplier = 0.12f;
    [Tooltip("图标渲染层级相对跳字文本的偏移，正数会显示在文本上方。")]
    [SerializeField] private int iconSortingOrderOffset = 1;

    [Header("治疗跳字")]
    [Tooltip("治疗跳字颜色。")]
    [SerializeField] private Color healColor = new Color(0.4f, 0.95f, 0.54f);
    [Tooltip("治疗跳字描边颜色。")]
    [SerializeField] private Color healOutlineColor = new Color(0.07f, 0.22f, 0.13f);
    [Tooltip("治疗跳字描边宽度。")]
    [SerializeField] private float healOutlineWidth = 0.18f;
    [Tooltip("治疗跳字字号相对同数值伤害跳字的倍率。")]
    [SerializeField] private float healFontSizeMultiplier = 1.25f;
    [Tooltip("治疗跳字生成时相对玩家位置向上的初始高度。")]
    [SerializeField] private float healStartHeightOffset = 0.8f;
    [Tooltip("治疗跳字向上漂浮距离。")]
    [SerializeField] private float healFloatDistance = 0.72f;
    [Tooltip("治疗跳字总持续时间。")]
    [SerializeField] private float healDuration = 0.8f;
    [Tooltip("治疗跳字弹出放大阶段持续时间。")]
    [SerializeField] private float healPopDuration = 0.15f;
    [Tooltip("治疗跳字弹出时的最大缩放倍率。")]
    [SerializeField] private float healPopScale = 1.35f;
    [Tooltip("治疗跳字开始淡出的延迟时间。")]
    [SerializeField] private float healFadeDelay = 0.18f;

    [Header("缓动")]
    [Tooltip("伤害跳字上跳阶段缓动。")]
    [SerializeField] private Ease jumpEase = Ease.OutQuad;
    [Tooltip("伤害跳字下落阶段缓动。")]
    [SerializeField] private Ease fallEase = Ease.InQuad;
    [Tooltip("跳字淡出缓动。")]
    [SerializeField] private Ease fadeEase = Ease.InQuad;
    [Tooltip("治疗跳字向上漂浮缓动。")]
    [SerializeField] private Ease healFloatEase = Ease.OutCubic;
    [Tooltip("治疗跳字弹出放大缓动。")]
    [SerializeField] private Ease healPopEase = Ease.OutBack;

    /// <summary>
    /// 跳字总持续时间。
    /// </summary>
    public float Duration => Mathf.Max(0.05f, duration);

    /// <summary>
    /// 上跳阶段所占比例。
    /// </summary>
    public float JumpPhaseRatio => Mathf.Clamp01(jumpPhaseRatio);

    /// <summary>
    /// 生成时相对受击点的高度偏移。
    /// </summary>
    public float StartHeightOffset => startHeightOffset;

    /// <summary>
    /// 上跳移动距离。
    /// </summary>
    public float JumpDistance => Mathf.Max(0f, jumpDistance);

    /// <summary>
    /// 下落移动距离。
    /// </summary>
    public float FallDistance => Mathf.Max(0f, fallDistance);

    /// <summary>
    /// 上跳方向角度。
    /// </summary>
    public float JumpAngle => jumpAngle;

    /// <summary>
    /// 连续跳字之间沿跳动方向拉开的距离。
    /// </summary>
    public float StackSpacing => Mathf.Max(0f, stackSpacing);

    /// <summary>
    /// 判定连续跳字的时间窗口。
    /// </summary>
    public float StackTimeWindow => Mathf.Max(0.01f, stackTimeWindow);

    /// <summary>
    /// 判定连续跳字是否属于同一受击区域的半径。
    /// </summary>
    public float StackPositionRadius => Mathf.Max(0.01f, stackPositionRadius);

    /// <summary>
    /// 连续跳字最多累计的错开次数。
    /// </summary>
    public int MaxStackOffsetCount => Mathf.Max(0, maxStackOffsetCount);

    /// <summary>
    /// 上跳阶段缓动。
    /// </summary>
    public Ease JumpEase => jumpEase;

    /// <summary>
    /// 下落阶段缓动。
    /// </summary>
    public Ease FallEase => fallEase;

    /// <summary>
    /// 淡出缓动。
    /// </summary>
    public Ease FadeEase => fadeEase;

    /// <summary>
    /// 治疗跳字颜色。
    /// </summary>
    public Color HealColor => healColor;

    /// <summary>
    /// 治疗跳字描边颜色。
    /// </summary>
    public Color HealOutlineColor => healOutlineColor;

    /// <summary>
    /// 治疗跳字描边宽度。
    /// </summary>
    public float HealOutlineWidth => Mathf.Max(0f, healOutlineWidth);

    /// <summary>
    /// 治疗跳字生成时相对玩家位置向上的初始高度。
    /// </summary>
    public float HealStartHeightOffset => healStartHeightOffset;

    /// <summary>
    /// 治疗跳字向上漂浮距离。
    /// </summary>
    public float HealFloatDistance => Mathf.Max(0f, healFloatDistance);

    /// <summary>
    /// 治疗跳字总持续时间。
    /// </summary>
    public float HealDuration => Mathf.Max(0.05f, healDuration);

    /// <summary>
    /// 治疗跳字弹出放大阶段持续时间。
    /// </summary>
    public float HealPopDuration => Mathf.Clamp(healPopDuration, 0.01f, HealDuration);

    /// <summary>
    /// 治疗跳字弹出时的最大缩放倍率。
    /// </summary>
    public float HealPopScale => Mathf.Max(1f, healPopScale);

    /// <summary>
    /// 治疗跳字开始淡出的延迟时间。
    /// </summary>
    public float HealFadeDelay => Mathf.Clamp(healFadeDelay, 0f, HealDuration);

    /// <summary>
    /// 治疗跳字向上漂浮缓动。
    /// </summary>
    public Ease HealFloatEase => healFloatEase;

    /// <summary>
    /// 治疗跳字弹出放大缓动。
    /// </summary>
    public Ease HealPopEase => healPopEase;

    /// <summary>
    /// 图标高度相对数字文本高度的倍率。
    /// </summary>
    public float IconHeightMultiplier => Mathf.Max(0.01f, iconHeightMultiplier);

    /// <summary>
    /// 图标与数字左边界之间的间距倍率。
    /// </summary>
    public float IconSpacingMultiplier => Mathf.Max(0f, iconSpacingMultiplier);

    /// <summary>
    /// 图标渲染层级相对跳字文本的偏移。
    /// </summary>
    public int IconSortingOrderOffset => iconSortingOrderOffset;

    /// <summary>
    /// 根据伤害数据获取跳字前缀图标；未配置时返回空。
    /// </summary>
    /// <param name="hitInfo">伤害命中数据。</param>
    /// <returns>需要显示的图标 Sprite。</returns>
    public Sprite GetIcon(DamageHitInfo hitInfo)
    {
        if (hitInfo.IsCritical)
            return criticalIcon;

        switch (hitInfo.SourceType)
        {
            case DamageSourceType.Fire:
                return fireIcon;

            case DamageSourceType.Ice:
                return iceIcon;

            case DamageSourceType.Poison:
                return poisonIcon;

            case DamageSourceType.Electric:
                return electricIcon;

            case DamageSourceType.Normal:
            default:
                return null;
        }
    }

    /// <summary>
    /// 根据伤害数据获取显示颜色。
    /// </summary>
    /// <param name="hitInfo">伤害命中数据。</param>
    /// <returns>跳字颜色。</returns>
    public Color GetColor(DamageHitInfo hitInfo)
    {
        if (hitInfo.IsCritical)
            return criticalColor;

        switch (hitInfo.SourceType)
        {
            case DamageSourceType.Fire:
                return fireColor;

            case DamageSourceType.Ice:
                return iceColor;

            case DamageSourceType.Poison:
                return poisonColor;

            case DamageSourceType.Electric:
                return electricColor;

            case DamageSourceType.Normal:
            default:
                return normalColor;
        }
    }

    /// <summary>
    /// 根据伤害量计算跳字字号。
    /// </summary>
    /// <param name="damage">伤害数值。</param>
    /// <returns>最终字号。</returns>
    public float GetFontSize(int damage)
    {
        float scale = sizeByDamage != null ? sizeByDamage.Evaluate(Mathf.Max(0, damage)) : 1f;
        return Mathf.Clamp(baseFontSize * Mathf.Max(0.1f, scale), minFontSize, maxFontSize);
    }

    /// <summary>
    /// 根据实际治疗量计算治疗跳字字号。
    /// </summary>
    /// <param name="healAmount">实际恢复的红血点数。</param>
    /// <returns>治疗跳字字号。</returns>
    public float GetHealFontSize(int healAmount)
    {
        return GetFontSize(healAmount) * Mathf.Max(0.1f, healFontSizeMultiplier);
    }
}
