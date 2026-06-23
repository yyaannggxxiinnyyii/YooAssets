using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 管理角色升级强化面板的选项展示、刷新按钮和选择事件。
/// </summary>
public sealed class GameLevelUpPanelController : Singleton<GameLevelUpPanelController>
{
    [Header("升级界面")]
    [Tooltip("升级强化界面标题文本。")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("剩余选择次数和刷新次数文本。")]
    [SerializeField] private TMP_Text remainingText;
    [Tooltip("刷新升级选项按钮。")]
    [SerializeField] private Button refreshButton;
    [Tooltip("升级强化选项视图数组。")]
    [SerializeField] private GameOfferView[] offerViews;
    [Tooltip("可选的当前属性摘要文本，未配置时会运行时创建。")]
    [SerializeField] private TMP_Text currentStatsText;

    private UnityAction[] _offerButtonActions;

    /// <summary>
    /// 请求选择升级强化选项。
    /// </summary>
    public event Action<int> OfferSelected;

    /// <summary>
    /// 请求刷新升级强化选项。
    /// </summary>
    public event Action RefreshRequested;

    protected override void Awake()
    {
        base.Awake();
        EnsureCurrentStatsText();
        SetText(titleText, "升级强化");
    }

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    /// <summary>
    /// 刷新升级强化选项，并显示剩余刷新次数和稀有度颜色。
    /// </summary>
    /// <param name="remainingChoices">剩余选择次数。</param>
    /// <param name="remainingRefreshes">剩余刷新次数。</param>
    /// <param name="names">选项名称列表。</param>
    /// <param name="descriptions">选项描述列表。</param>
    /// <param name="colors">选项背景颜色列表。</param>
    public void Refresh(int remainingChoices, int remainingRefreshes, string[] names, string[] descriptions, Color[] colors)
    {
        Refresh(remainingChoices, remainingRefreshes, names, descriptions, colors, null);
    }

    /// <summary>
    /// 刷新升级强化选项，并显示当前属性摘要。
    /// </summary>
    /// <param name="remainingChoices">剩余选择次数。</param>
    /// <param name="remainingRefreshes">剩余刷新次数。</param>
    /// <param name="names">选项名称列表。</param>
    /// <param name="descriptions">选项描述列表。</param>
    /// <param name="colors">选项背景颜色列表。</param>
    /// <param name="currentStats">当前属性摘要文本。</param>
    public void Refresh(int remainingChoices, int remainingRefreshes, string[] names, string[] descriptions, Color[] colors, string currentStats)
    {
        EnsureCurrentStatsText();
        SetText(remainingText, $"剩余选择次数：{remainingChoices}  刷新：{remainingRefreshes}");
        SetText(currentStatsText, currentStats);
        if (refreshButton != null)
        {
            refreshButton.interactable = remainingRefreshes > 0;
            SetButtonText(refreshButton, $"刷新（{remainingRefreshes}）");
        }

        RefreshOfferViews(offerViews, names, descriptions, colors);
    }

    /// <summary>
    /// 绑定升级强化按钮事件。
    /// </summary>
    private void BindButtons()
    {
        if (refreshButton != null)
            refreshButton.onClick.AddListener(NotifyRefreshRequested);

        BindOfferButtons();
    }

    /// <summary>
    /// 解绑升级强化按钮事件。
    /// </summary>
    private void UnbindButtons()
    {
        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(NotifyRefreshRequested);

        UnbindOfferButtons();
    }

    /// <summary>
    /// 绑定选项按钮。
    /// </summary>
    private void BindOfferButtons()
    {
        if (offerViews == null)
            return;

        _offerButtonActions = new UnityAction[offerViews.Length];
        for (int i = 0; i < offerViews.Length; i++)
        {
            int index = i;
            _offerButtonActions[i] = () => OfferSelected?.Invoke(index);
            Button button = offerViews[i] != null ? offerViews[i].ChooseButton : null;
            if (button != null)
                button.onClick.AddListener(_offerButtonActions[i]);
        }
    }

    /// <summary>
    /// 解绑选项按钮。
    /// </summary>
    private void UnbindOfferButtons()
    {
        if (offerViews == null || _offerButtonActions == null)
            return;

        for (int i = 0; i < offerViews.Length; i++)
        {
            Button button = offerViews[i] != null ? offerViews[i].ChooseButton : null;
            if (button != null && i < _offerButtonActions.Length)
                button.onClick.RemoveListener(_offerButtonActions[i]);
        }
    }

    /// <summary>
    /// 刷新通用选项视图数组。
    /// </summary>
    /// <param name="views">选项视图数组。</param>
    /// <param name="names">选项名称数组。</param>
    /// <param name="descriptions">选项描述数组。</param>
    /// <param name="colors">选项背景色数组。</param>
    private void RefreshOfferViews(GameOfferView[] views, string[] names, string[] descriptions, Color[] colors)
    {
        if (views == null)
            return;

        bool hasOffers = names != null && names.Length > 0;
        for (int i = 0; i < views.Length; i++)
        {
            GameOfferView offerView = views[i];
            if (offerView == null)
                continue;

            bool visible = hasOffers && i < names.Length;
            offerView.SetVisible(visible);
            if (!visible)
                continue;

            Color backgroundColor = colors != null && i < colors.Length
                ? colors[i]
                : new Color(0.12f, 0.17f, 0.24f, 0.92f);
            offerView.Refresh(GetArrayValue(names, i), GetArrayValue(descriptions, i), true, backgroundColor);
        }
    }

    /// <summary>
    /// 通知外部请求刷新升级强化选项。
    /// </summary>
    private void NotifyRefreshRequested()
    {
        RefreshRequested?.Invoke();
    }

    /// <summary>
    /// 确保升级界面存在当前属性摘要文本。
    /// </summary>
    private void EnsureCurrentStatsText()
    {
        if (currentStatsText != null)
            return;

        Transform existedStats = transform.Find("CurrentStatsText");
        if (existedStats != null)
            currentStatsText = existedStats.GetComponent<TMP_Text>();

        if (currentStatsText != null)
            return;

        GameObject statsObject = new GameObject("CurrentStatsText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        statsObject.transform.SetParent(transform, false);
        ApplyGeneratedStatsTextLayout(statsObject.GetComponent<RectTransform>());
        currentStatsText = statsObject.GetComponent<TMP_Text>();
        currentStatsText.fontSize = 20f;
        currentStatsText.color = new Color(0.92f, 0.96f, 1f, 0.95f);
        currentStatsText.alignment = TextAlignmentOptions.TopLeft;
        currentStatsText.raycastTarget = false;
    }

    /// <summary>
    /// 设置运行时创建的属性摘要文本布局，避免默认位置遮挡强化选项。
    /// </summary>
    /// <param name="rectTransform">属性摘要文本的 RectTransform。</param>
    private void ApplyGeneratedStatsTextLayout(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(32f, -96f);
        rectTransform.sizeDelta = new Vector2(320f, 360f);
    }

    /// <summary>
    /// 安全设置 TMP 文本。
    /// </summary>
    /// <param name="targetText">目标文本组件。</param>
    /// <param name="content">显示内容。</param>
    private void SetText(TMP_Text targetText, string content)
    {
        if (targetText == null)
            return;

        targetText.text = content;
    }

    /// <summary>
    /// 设置按钮子文本。
    /// </summary>
    /// <param name="button">目标按钮。</param>
    /// <param name="content">显示内容。</param>
    private void SetButtonText(Button button, string content)
    {
        if (button == null)
            return;

        TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>();
        SetText(buttonText, content);
    }

    /// <summary>
    /// 安全读取字符串数组值。
    /// </summary>
    /// <param name="values">数组。</param>
    /// <param name="index">索引。</param>
    /// <returns>数组值。</returns>
    private string GetArrayValue(string[] values, int index)
    {
        if (values == null || index < 0 || index >= values.Length)
            return string.Empty;

        return values[index];
    }
}
