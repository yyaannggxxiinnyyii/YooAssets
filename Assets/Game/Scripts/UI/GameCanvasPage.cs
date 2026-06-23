using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 封装单个 Canvas 界面的显隐与交互状态，统一使用 CanvasGroup 控制页面切换。
/// </summary>
public sealed class GameCanvasPage : MonoBehaviour
{
    [Header("页面配置")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private bool hideOnAwake = true;

    /// <summary>
    /// 页面当前是否处于显示状态。
    /// </summary>
    public bool IsVisible { get; private set; }

    private void Awake()
    {
        CacheCanvasGroup();

        if (hideOnAwake)
            Hide();
    }

    /// <summary>
    /// 显示页面，并恢复射线检测和交互。
    /// </summary>
    public void Show()
    {
        SetVisible(true);
    }

    /// <summary>
    /// 隐藏页面，并关闭射线检测和交互。
    /// </summary>
    public void Hide()
    {
        SetVisible(false);
    }

    /// <summary>
    /// 根据传入状态切换页面显隐。
    /// </summary>
    /// <param name="visible">是否显示页面。</param>
    public void SetVisible(bool visible)
    {
        SetVisible(visible, visible);
    }

    /// <summary>
    /// 根据传入状态切换页面显示和交互状态。
    /// </summary>
    /// <param name="visible">是否显示页面。</param>
    /// <param name="interactive">是否允许页面交互和射线检测。</param>
    public void SetVisible(bool visible, bool interactive)
    {
        CacheCanvasGroup();

        if (canvasGroup == null)
        {
            Debug.LogWarning($"[GameUI] {name} 缺少 CanvasGroup，无法切换页面。");
            return;
        }

        IsVisible = visible;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible && interactive;
        canvasGroup.blocksRaycasts = visible && interactive;
        gameObject.SetActive(true);
        if (visible)
            RebuildVisibleLayout();
    }

    /// <summary>
    /// 缓存页面上的 CanvasGroup，未显式配置时从当前物体获取。
    /// </summary>
    private void CacheCanvasGroup()
    {
        if (canvasGroup != null)
            return;

        canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// 页面显示后立即刷新一次布局，避免 CanvasGroup 隐藏期间文本尺寸未参与布局。
    /// </summary>
    private void RebuildVisibleLayout()
    {
        Canvas.ForceUpdateCanvases();
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

        LayoutGroup[] layoutGroups = GetComponentsInChildren<LayoutGroup>(true);
        for (int i = 0; i < layoutGroups.Length; i++)
        {
            RectTransform layoutTransform = layoutGroups[i] != null ? layoutGroups[i].transform as RectTransform : null;
            if (layoutTransform != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(layoutTransform);
        }
    }
}
