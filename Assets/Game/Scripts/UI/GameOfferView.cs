using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 表示一个通用三选一选项视图，供升级和武器强化界面复用。
/// </summary>
public sealed class GameOfferView : MonoBehaviour
{
    [Header("选项引用")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button chooseButton;

    /// <summary>
    /// 选项按钮。
    /// </summary>
    public Button ChooseButton => chooseButton;

    private void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
    }

    /// <summary>
    /// 刷新选项显示内容和可交互状态。
    /// </summary>
    /// <param name="offerName">选项名称。</param>
    /// <param name="description">选项描述。</param>
    /// <param name="interactable">是否允许点击。</param>
    public void Refresh(string offerName, string description, bool interactable)
    {
        Refresh(offerName, description, interactable, new Color(0.12f, 0.17f, 0.24f, 0.92f));
    }

    /// <summary>
    /// 刷新选项显示内容、可交互状态和背景颜色。
    /// </summary>
    /// <param name="offerName">选项名称。</param>
    /// <param name="description">选项描述。</param>
    /// <param name="interactable">是否允许点击。</param>
    /// <param name="backgroundColor">选项背景颜色。</param>
    public void Refresh(string offerName, string description, bool interactable, Color backgroundColor)
    {
        SetText(nameText, offerName);
        SetText(descriptionText, description);

        if (backgroundImage != null)
            backgroundImage.color = backgroundColor;

        if (chooseButton != null)
            chooseButton.interactable = interactable;
    }

    /// <summary>
    /// 设置选项物体显示状态。
    /// </summary>
    /// <param name="visible">是否显示。</param>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
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
