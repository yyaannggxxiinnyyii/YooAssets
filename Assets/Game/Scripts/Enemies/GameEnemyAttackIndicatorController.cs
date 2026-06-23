using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制敌人攻击前摇期间的世界空间攻击指示条显示、朝向、长度和填充进度。
/// </summary>
public sealed class GameEnemyAttackIndicatorController : MonoBehaviour
{
    [Header("显示引用")]
    [Tooltip("控制整条攻击指示条显隐的 CanvasGroup。")]
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("整条攻击指示条的长度根节点，脚本会按攻击距离修改它的宽度。")]
    [SerializeField] private RectTransform lengthRoot;
    [Tooltip("蓄力进度填充图片，需要设置为 Filled / Horizontal。")]
    [SerializeField] private Image fillImage;
    [Tooltip("需要朝攻击方向旋转的根节点；为空时使用当前 Transform。")]
    [SerializeField] private RectTransform directionRoot;

    [Header("长度设置")]
    [Tooltip("是否根据攻击距离自动设置指示条世界长度。")]
    [SerializeField] private bool syncLengthWithAttackDistance = true;
    [Tooltip("1 个世界单位对应的 UI 宽度。World Space Canvas 推荐设置为 50-200，避免 Sliced 边框被过小宽度挤压。")]
    [SerializeField] private float uiUnitsPerWorldUnit = 100f;
    [Tooltip("指示条在攻击距离基础上额外增加的世界单位长度。")]
    [SerializeField] private float worldLengthPadding;
    [Tooltip("指示条最短世界长度。")]
    [SerializeField] private float minWorldLength = 0.3f;
    [Tooltip("指示条最长世界长度，0 表示不限制。")]
    [SerializeField] private float maxWorldLength;

    [Header("显示设置")]
    [Tooltip("Awake 时是否自动隐藏攻击指示条。")]
    [SerializeField] private bool hideOnAwake = true;
    [Tooltip("显示攻击指示条时是否根据攻击方向旋转。")]
    [SerializeField] private bool rotateToDirection = true;
    [Tooltip("显示时的透明度。")]
    [SerializeField] private float visibleAlpha = 1f;
    [Tooltip("隐藏时的透明度。")]
    [SerializeField] private float hiddenAlpha;

    private void Awake()
    {
        CacheReferences();
        if (hideOnAwake)
            HideImmediate();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    /// <summary>
    /// 按攻击方向和蓄力进度显示指示条。
    /// </summary>
    /// <param name="direction">攻击方向。</param>
    /// <param name="progress">蓄力进度，0 表示刚开始，1 表示即将出手。</param>
    public void Show(Vector3 direction, float progress)
    {
        Show(direction, progress, -1f);
    }

    /// <summary>
    /// 按攻击方向、蓄力进度和攻击距离显示指示条。
    /// </summary>
    /// <param name="direction">攻击方向。</param>
    /// <param name="progress">蓄力进度。</param>
    /// <param name="attackWorldLength">攻击距离对应的世界长度，小于等于 0 时不更新长度。</param>
    public void Show(Vector3 direction, float progress, float attackWorldLength)
    {
        CacheReferences();
        SetVisible(true);
        SetDirection(direction);
        SetLength(attackWorldLength);
        SetProgress(progress);
    }

    /// <summary>
    /// 隐藏攻击指示条。
    /// </summary>
    public void Hide()
    {
        CacheReferences();
        SetVisible(false);
        SetProgress(0f);
    }

    /// <summary>
    /// 立即隐藏攻击指示条，不依赖外部初始化调用。
    /// </summary>
    public void HideImmediate()
    {
        SetVisible(false);
        SetProgress(0f);
    }

    /// <summary>
    /// 缓存未手动绑定的显示引用。
    /// </summary>
    private void CacheReferences()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (lengthRoot == null)
            lengthRoot = transform as RectTransform;

        if (fillImage == null)
            fillImage = GetComponentInChildren<Image>(true);

        if (directionRoot == null)
            directionRoot = transform as RectTransform;
    }

    /// <summary>
    /// 设置指示条显隐状态。
    /// </summary>
    /// <param name="visible">是否显示。</param>
    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? Mathf.Max(0f, visibleAlpha) : Mathf.Max(0f, hiddenAlpha);
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        gameObject.SetActive(visible);
    }

    /// <summary>
    /// 根据攻击世界距离设置指示条宽度。
    /// </summary>
    /// <param name="attackWorldLength">攻击距离对应的世界长度。</param>
    private void SetLength(float attackWorldLength)
    {
        if (!syncLengthWithAttackDistance || lengthRoot == null || attackWorldLength <= 0f)
            return;

        float worldLength = Mathf.Max(0.01f, attackWorldLength + worldLengthPadding);
        worldLength = Mathf.Max(minWorldLength, worldLength);
        if (maxWorldLength > 0f)
            worldLength = Mathf.Min(maxWorldLength, worldLength);

        Vector2 size = lengthRoot.sizeDelta;
        size.x = worldLength * Mathf.Max(0.01f, uiUnitsPerWorldUnit);
        lengthRoot.sizeDelta = size;
    }

    /// <summary>
    /// 设置蓄力进度填充。
    /// </summary>
    /// <param name="progress">蓄力进度。</param>
    private void SetProgress(float progress)
    {
        if (fillImage != null)
            fillImage.fillAmount = Mathf.Clamp01(progress);
    }

    /// <summary>
    /// 让指示条朝向指定攻击方向。
    /// </summary>
    /// <param name="direction">攻击方向。</param>
    private void SetDirection(Vector3 direction)
    {
        if (!rotateToDirection || directionRoot == null || direction.sqrMagnitude <= 0.01f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        directionRoot.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
