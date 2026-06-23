using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 表示商店中的单个商品视图，负责展示名称、描述、价格和锁定状态。
/// </summary>
public sealed class GameShopOfferView : MonoBehaviour
{
    [Header("商品引用")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text lockStateText;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button lockButton;

    /// <summary>
    /// 购买按钮。
    /// </summary>
    public Button BuyButton => buyButton;

    /// <summary>
    /// 锁定按钮。
    /// </summary>
    public Button LockButton => lockButton;

    private void Awake()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
    }

    /// <summary>
    /// 刷新商品显示内容和按钮状态。
    /// </summary>
    /// <param name="offerName">商品名称。</param>
    /// <param name="description">商品描述。</param>
    /// <param name="cost">商品价格。</param>
    /// <param name="locked">是否已锁定。</param>
    /// <param name="purchased">是否已购买。</param>
    /// <param name="canBuy">是否可以买入。</param>
    /// <param name="backgroundColor">商品评级背景颜色。</param>
    public void Refresh(
        string offerName,
        string description,
        int cost,
        bool locked,
        bool purchased,
        bool canBuy,
        Color backgroundColor)
    {
        SetText(nameText, offerName);
        SetText(descriptionText, description);
        SetText(costText, $"价格：{cost}");
        SetText(lockStateText, locked ? "状态：已锁定" : "状态：未锁定");

        if (backgroundImage != null)
            backgroundImage.color = backgroundColor;

        if (buyButton != null)
            buyButton.interactable = !purchased && canBuy;

        if (lockButton != null)
            lockButton.interactable = !purchased;
    }

    /// <summary>
    /// 设置商品物体显示状态。
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
