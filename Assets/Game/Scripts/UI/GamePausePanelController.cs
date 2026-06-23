using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理暂停面板的属性文本和恢复、重开、退出按钮事件。
/// </summary>
public sealed class GamePausePanelController : Singleton<GamePausePanelController>
{
    [Header("暂停界面")]
    [Tooltip("暂停界面标题文本。")]
    [SerializeField] private TMP_Text titleText;
    [Tooltip("暂停界面当前属性统计文本。")]
    [SerializeField] private TMP_Text statsText;
    [Tooltip("暂停界面当前武器文本。")]
    [SerializeField] private TMP_Text weaponText;
    [Tooltip("暂停界面已获得道具文本。")]
    [SerializeField] private TMP_Text itemsText;
    [Tooltip("恢复游戏按钮。")]
    [SerializeField] private Button resumeButton;
    [Tooltip("重新开始本局按钮。")]
    [SerializeField] private Button restartButton;
    [Tooltip("设置按钮，当前暂未启用。")]
    [SerializeField] private Button settingsButton;
    [Tooltip("退出游戏按钮。")]
    [SerializeField] private Button quitButton;

    /// <summary>
    /// 请求恢复游戏。
    /// </summary>
    public event Action ResumeRequested;

    /// <summary>
    /// 请求重新开始本局。
    /// </summary>
    public event Action RestartRequested;

    protected override void Awake()
    {
        base.Awake();
        SetText(titleText, "暂停");

        if (settingsButton != null)
            settingsButton.interactable = false;
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
    /// 刷新暂停页面。
    /// </summary>
    /// <param name="stats">属性统计文本。</param>
    /// <param name="weaponName">当前武器名称。</param>
    /// <param name="items">道具文本。</param>
    public void Refresh(string stats, string weaponName, string items)
    {
        SetText(statsText, stats);
        SetText(weaponText, $"当前武器：{weaponName}");
        SetText(itemsText, $"道具：{items}");
    }

    /// <summary>
    /// 绑定暂停按钮。
    /// </summary>
    private void BindButtons()
    {
        if (resumeButton != null)
            resumeButton.onClick.AddListener(NotifyResumeRequested);

        if (restartButton != null)
            restartButton.onClick.AddListener(NotifyRestartRequested);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    /// <summary>
    /// 解绑暂停按钮。
    /// </summary>
    private void UnbindButtons()
    {
        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(NotifyResumeRequested);

        if (restartButton != null)
            restartButton.onClick.RemoveListener(NotifyRestartRequested);

        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);
    }

    /// <summary>
    /// 通知外部恢复游戏。
    /// </summary>
    private void NotifyResumeRequested()
    {
        ResumeRequested?.Invoke();
    }

    /// <summary>
    /// 通知外部重新开始本局。
    /// </summary>
    private void NotifyRestartRequested()
    {
        RestartRequested?.Invoke();
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
