using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 管理武器强化面板的选项展示和选择事件。
/// </summary>
public sealed class GameWeaponUpgradePanelController : Singleton<GameWeaponUpgradePanelController>
{
    [Header("武器强化界面")]
    [Tooltip("武器强化界面标题文本。")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("当前武器和剩余选择次数文本。")]
    [SerializeField] private TMP_Text currentWeaponText;
    [Tooltip("武器强化选项视图数组。")]
    [SerializeField] private GameOfferView[] offerViews;
    [Tooltip("可选的当前属性摘要文本，未配置时会运行时创建。")]
    [SerializeField] private TMP_Text currentStatsText;

    private UnityAction[] _offerButtonActions;

    /// <summary>
    /// 请求选择武器强化选项。
    /// </summary>
    public event Action<int> OfferSelected;

    protected override void Awake()
    {
        base.Awake();
        EnsureCurrentStatsText();
        SetText(titleText, "武器强化");
    }

    private void OnEnable()
    {
        BindOfferButtons();
    }

    private void OnDisable()
    {
        UnbindOfferButtons();
    }

    /// <summary>
    /// 刷新武器强化选项。
    /// </summary>
    /// <param name="currentWeaponName">当前武器名称。</param>
    /// <param name="remainingChoices">剩余选择次数。</param>
    /// <param name="names">选项名称列表。</param>
    /// <param name="descriptions">选项描述列表。</param>
    public void Refresh(string currentWeaponName, int remainingChoices, string[] names, string[] descriptions)
    {
        Refresh(currentWeaponName, remainingChoices, names, descriptions, null);
    }

    /// <summary>
    /// 刷新武器强化选项，并显示当前属性摘要。
    /// </summary>
    /// <param name="currentWeaponName">当前武器名称。</param>
    /// <param name="remainingChoices">剩余选择次数。</param>
    /// <param name="names">选项名称列表。</param>
    /// <param name="descriptions">选项描述列表。</param>
    /// <param name="currentStats">当前属性摘要文本。</param>
    public void Refresh(string currentWeaponName, int remainingChoices, string[] names, string[] descriptions, string currentStats)
    {
        EnsureCurrentStatsText();
        SetText(currentWeaponText, $"当前武器：{currentWeaponName}  剩余选择次数：{remainingChoices}");
        SetText(currentStatsText, currentStats);
        RefreshOfferViews(offerViews, names, descriptions);
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
    private void RefreshOfferViews(GameOfferView[] views, string[] names, string[] descriptions)
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

            offerView.Refresh(GetArrayValue(names, i), GetArrayValue(descriptions, i), true, new Color(0.12f, 0.17f, 0.24f, 0.92f));
        }
    }

    /// <summary>
    /// 确保武器强化界面存在当前属性摘要文本。
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
