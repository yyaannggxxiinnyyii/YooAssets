using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制单个心槽的红血或临时特殊血显示状态。
/// </summary>
public sealed class GameHeartSlotView : MonoBehaviour
{
    private const int PointsPerHeart = 2;

    [Header("心槽图片")]
    [Tooltip("显示心槽状态的主图片。")]
    [SerializeField] private Image heartImage;
    [Tooltip("满心状态使用的图片。")]
    [SerializeField] private Sprite fullHeartSprite;
    [Tooltip("半心状态使用的图片。")]
    [SerializeField] private Sprite halfHeartSprite;
    [Tooltip("空心槽状态使用的图片。")]
    [SerializeField] private Sprite emptyHeartSprite;

    [Header("心槽颜色")]
    [Tooltip("红心颜色。")]
    [SerializeField] private Color redColor = new Color(1f, 0.2f, 0.24f, 1f);
    [Tooltip("蓝心颜色。")]
    [SerializeField] private Color blueColor = new Color(0.25f, 0.62f, 1f, 1f);
    [Tooltip("粉心颜色。")]
    [SerializeField] private Color pinkColor = new Color(1f, 0.45f, 0.75f, 1f);
    [Tooltip("玻璃心颜色。")]
    [SerializeField] private Color glassColor = new Color(0.72f, 0.95f, 1f, 0.92f);
    [Tooltip("爆炸心颜色。")]
    [SerializeField] private Color explosiveColor = new Color(1f, 0.55f, 0.16f, 1f);

    private int _maxPoints = PointsPerHeart;
    private int _fillPoints = PointsPerHeart;
    private SpecialHealthType _heartType = SpecialHealthType.Blue;
    private bool _useSpecialColor;

    private void Awake()
    {
        EnsureImage();
        RefreshVisual();
    }

    /// <summary>
    /// 设置心槽当前填充点数，0 为空、1 为半、2 为满。
    /// </summary>
    /// <param name="points">填充点数。</param>
    public void SetFillPoints(int points)
    {
        _fillPoints = Mathf.Clamp(points, 0, _maxPoints);
        RefreshVisual();
    }

    /// <summary>
    /// 设置心槽可容纳点数，支持最后一个半红心槽。
    /// </summary>
    /// <param name="points">最大点数。</param>
    public void SetMaxPoints(int points)
    {
        _maxPoints = Mathf.Clamp(points, 1, PointsPerHeart);
        _fillPoints = Mathf.Clamp(_fillPoints, 0, _maxPoints);
        RefreshVisual();
    }

    /// <summary>
    /// 设置当前心槽代表的临时特殊血类型。
    /// </summary>
    /// <param name="type">特殊血类型。</param>
    public void SetHeartType(SpecialHealthType type)
    {
        _heartType = type;
        _useSpecialColor = true;
        RefreshVisual();
    }

    /// <summary>
    /// 设置当前心槽为红血心槽。
    /// </summary>
    public void SetRedHeart()
    {
        _useSpecialColor = false;
        RefreshVisual();
    }

    /// <summary>
    /// 配置心槽图片资源，供运行时创建 prefab 占位时注入。
    /// </summary>
    /// <param name="fullSprite">满心图片。</param>
    /// <param name="halfSprite">半心图片。</param>
    /// <param name="emptySprite">空心图片。</param>
    public void SetSprites(Sprite fullSprite, Sprite halfSprite, Sprite emptySprite)
    {
        if (fullSprite != null)
            fullHeartSprite = fullSprite;

        if (halfSprite != null)
            halfHeartSprite = halfSprite;

        if (emptySprite != null)
            emptyHeartSprite = emptySprite;

        RefreshVisual();
    }

    /// <summary>
    /// 设置特殊心槽颜色，供不同类型 prefab 使用独立颜色。
    /// </summary>
    /// <param name="type">特殊血类型。</param>
    /// <param name="color">目标颜色。</param>
    public void SetSpecialColor(SpecialHealthType type, Color color)
    {
        switch (type)
        {
            case SpecialHealthType.Blue:
                blueColor = color;
                break;
            case SpecialHealthType.Pink:
                pinkColor = color;
                break;
            case SpecialHealthType.Glass:
                glassColor = color;
                break;
            case SpecialHealthType.Explosive:
                explosiveColor = color;
                break;
        }

        RefreshVisual();
    }

    /// <summary>
    /// 确保心槽存在可刷新图片组件。
    /// </summary>
    private void EnsureImage()
    {
        if (heartImage == null)
            heartImage = GetComponent<Image>();

        if (heartImage == null)
            heartImage = GetComponentInChildren<Image>(true);
    }

    /// <summary>
    /// 按当前填充点数、最大点数和类型刷新图片。
    /// </summary>
    private void RefreshVisual()
    {
        EnsureImage();
        if (heartImage == null)
            return;

        heartImage.sprite = GetSpriteForPoints();
        heartImage.color = _useSpecialColor ? GetSpecialColor(_heartType) : redColor;
        heartImage.preserveAspect = true;
        heartImage.raycastTarget = false;
    }

    /// <summary>
    /// 获取当前填充点数对应图片。
    /// </summary>
    /// <returns>心槽图片。</returns>
    private Sprite GetSpriteForPoints()
    {
        if (_fillPoints >= PointsPerHeart && _maxPoints >= PointsPerHeart)
            return fullHeartSprite;

        if (_fillPoints > 0)
            return halfHeartSprite != null ? halfHeartSprite : fullHeartSprite;

        return emptyHeartSprite;
    }

    /// <summary>
    /// 获取特殊血类型对应颜色。
    /// </summary>
    /// <param name="type">特殊血类型。</param>
    /// <returns>显示颜色。</returns>
    private Color GetSpecialColor(SpecialHealthType type)
    {
        switch (type)
        {
            case SpecialHealthType.Blue:
                return blueColor;
            case SpecialHealthType.Pink:
                return pinkColor;
            case SpecialHealthType.Glass:
                return glassColor;
            case SpecialHealthType.Explosive:
                return explosiveColor;
            default:
                return blueColor;
        }
    }
}
