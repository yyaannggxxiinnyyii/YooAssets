using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理标题面板的标题文本、提示文本和继续输入事件。
/// </summary>
public sealed class GameTitlePanelController : Singleton<GameTitlePanelController>
{
    [Header("标题界面")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Button clickButton;
    [SerializeField] private string gameTitle = "戳泡泡";
    [SerializeField] private string continueHint = "按任意键继续";

    /// <summary>
    /// 请求进入角色选择。
    /// </summary>
    public event Action ContinueRequested;

    protected override void Awake()
    {
        base.Awake();
        Refresh();
    }

    private void OnEnable()
    {
        if (clickButton != null)
            clickButton.onClick.AddListener(NotifyContinueRequested);
    }

    private void OnDisable()
    {
        if (clickButton != null)
            clickButton.onClick.RemoveListener(NotifyContinueRequested);
    }

    /// <summary>
    /// 刷新标题面板静态文本。
    /// </summary>
    public void Refresh()
    {
        SetText(titleText, gameTitle);
        SetText(hintText, continueHint);
    }

    /// <summary>
    /// 通知外部进入角色选择。
    /// </summary>
    private void NotifyContinueRequested()
    {
        ContinueRequested?.Invoke();
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
