using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理结算面板的统计文本、角色武器信息和重开、返回标题、退出按钮事件。
/// </summary>
public sealed class GameResultPanelController : Singleton<GameResultPanelController>
{
    [Header("结算界面")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text characterText;
    [SerializeField] private TMP_Text weaponText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text characterStatsText;
    [SerializeField] private TMP_Text weaponStatsText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button returnTitleButton;
    [SerializeField] private Button quitButton;

    private bool _statsLayoutPrepared;

    /// <summary>
    /// 请求重新开始本局。
    /// </summary>
    public event Action RestartRequested;

    /// <summary>
    /// 请求返回标题界面。
    /// </summary>
    public event Action ReturnTitleRequested;

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    /// <summary>
    /// 刷新结算页面。
    /// </summary>
    /// <param name="isVictory">是否胜利。</param>
    /// <param name="characterName">角色名称。</param>
    /// <param name="weaponName">武器名称。</param>
    /// <param name="summaryText">本局统计文本。</param>
    /// <param name="characterStats">角色属性文本。</param>
    /// <param name="weaponStats">武器属性文本。</param>
    public void Refresh(
        bool isVictory,
        string characterName,
        string weaponName,
        string summaryText,
        string characterStats,
        string weaponStats)
    {
        SetText(titleText, isVictory ? "通关结算" : "战败结算");
        SetText(characterText, characterName);
        SetText(weaponText, $"武器：{weaponName}");
        PrepareStatsLayout();
        SetText(statsText, summaryText);
        SetText(characterStatsText, characterStats);
        SetText(weaponStatsText, weaponStats);

        if (avatarImage != null)
            avatarImage.color = isVictory ? new Color(0.95f, 0.72f, 0.28f) : new Color(0.55f, 0.58f, 0.62f);
    }

    /// <summary>
    /// 刷新旧版单列结算页面。
    /// </summary>
    /// <param name="isVictory">是否胜利。</param>
    /// <param name="characterName">角色名称。</param>
    /// <param name="weaponName">武器名称。</param>
    /// <param name="stats">统计文本。</param>
    public void Refresh(bool isVictory, string characterName, string weaponName, string stats)
    {
        SetText(titleText, isVictory ? "通关结算" : "战败结算");
        SetText(characterText, characterName);
        SetText(weaponText, $"武器：{weaponName}");
        SetText(statsText, stats);

        if (avatarImage != null)
            avatarImage.color = isVictory ? new Color(0.95f, 0.72f, 0.28f) : new Color(0.55f, 0.58f, 0.62f);
    }

    /// <summary>
    /// 绑定结算按钮。
    /// </summary>
    private void BindButtons()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(NotifyRestartRequested);

        if (returnTitleButton != null)
            returnTitleButton.onClick.AddListener(NotifyReturnTitleRequested);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    /// <summary>
    /// 解绑结算按钮。
    /// </summary>
    private void UnbindButtons()
    {
        if (restartButton != null)
            restartButton.onClick.RemoveListener(NotifyRestartRequested);

        if (returnTitleButton != null)
            returnTitleButton.onClick.RemoveListener(NotifyReturnTitleRequested);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);
    }

    /// <summary>
    /// 准备结算页面三列统计文本，未手动绑定时复用原统计文本克隆生成。
    /// </summary>
    private void PrepareStatsLayout()
    {
        if (_statsLayoutPrepared || statsText == null)
            return;

        _statsLayoutPrepared = true;
        bool shouldCreateCharacterStatsText = characterStatsText == null;
        bool shouldCreateWeaponStatsText = weaponStatsText == null;
        characterStatsText = EnsureStatsText(characterStatsText, "CharacterStatsText");
        weaponStatsText = EnsureStatsText(weaponStatsText, "WeaponStatsText");
        if (shouldCreateCharacterStatsText || shouldCreateWeaponStatsText)
        {
            ApplyStatsTextLayout(statsText, 0);
            ApplyStatsTextLayout(characterStatsText, 1);
            ApplyStatsTextLayout(weaponStatsText, 2);
        }
    }

    /// <summary>
    /// 获取或创建结算页统计文本节点。
    /// </summary>
    /// <param name="targetText">已配置的文本节点。</param>
    /// <param name="name">缺失时创建的节点名称。</param>
    /// <returns>可用于显示的文本节点。</returns>
    private TMP_Text EnsureStatsText(TMP_Text targetText, string name)
    {
        if (targetText != null)
            return targetText;

        TMP_Text clonedText = Instantiate(statsText, statsText.transform.parent);
        clonedText.name = name;
        return clonedText;
    }

    /// <summary>
    /// 应用结算页统计文本列布局。
    /// </summary>
    /// <param name="targetText">目标文本节点。</param>
    /// <param name="columnIndex">列索引。</param>
    private void ApplyStatsTextLayout(TMP_Text targetText, int columnIndex)
    {
        if (targetText == null)
            return;

        targetText.alignment = TextAlignmentOptions.TopLeft;
        targetText.enableWordWrapping = true;
        targetText.overflowMode = TextOverflowModes.Ellipsis;
        targetText.fontSize = Mathf.Min(targetText.fontSize, 34f);

        RectTransform rectTransform = targetText.rectTransform;
        if (rectTransform == null)
            return;

        float columnWidth = 280f;
        float columnGap = 48f;
        float totalWidth = columnWidth * 3f + columnGap * 2f;
        float leftX = -totalWidth * 0.5f + columnWidth * 0.5f;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.sizeDelta = new Vector2(columnWidth, 360f);
        rectTransform.anchoredPosition = new Vector2(leftX + (columnWidth + columnGap) * columnIndex, 120f);
    }

    /// <summary>
    /// 通知外部重新开始本局。
    /// </summary>
    private void NotifyRestartRequested()
    {
        RestartRequested?.Invoke();
    }

    /// <summary>
    /// 通知外部返回标题界面。
    /// </summary>
    private void NotifyReturnTitleRequested()
    {
        ReturnTitleRequested?.Invoke();
    }

    /// <summary>
    /// 输出退出游戏日志，后续接入项目级退出流程。
    /// </summary>
    private void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("[GameUI] 退出游戏。");
#else
        Application.Quit();
#endif
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
}
