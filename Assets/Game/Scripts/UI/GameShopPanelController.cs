using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 管理商店面板的商品展示、刷新、锁定、购买和下一层按钮事件。
/// </summary>
public sealed class GameShopPanelController : Singleton<GameShopPanelController>
{
    [Header("商店界面")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text emptyText;
    [FormerlySerializedAs("offerViews")]
    [SerializeField] private GameShopOfferView[] itemOfferViews;
    [SerializeField] private GameShopOfferView[] relicOfferViews;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button nextFloorButton;

    private UnityAction[] _buyButtonActions;
    private UnityAction[] _lockButtonActions;

    /// <summary>
    /// 请求购买商店商品。
    /// </summary>
    public event Action<int> OfferBuyRequested;

    /// <summary>
    /// 请求切换商店商品锁定状态。
    /// </summary>
    public event Action<int> OfferLockRequested;

    /// <summary>
    /// 请求刷新商店。
    /// </summary>
    public event Action RefreshRequested;

    /// <summary>
    /// 请求进入下一层。
    /// </summary>
    public event Action NextFloorRequested;

    protected override void Awake()
    {
        base.Awake();
        SetText(descriptionText, "商店出售道具；角色升级会从不同稀有度词条池中抽取强化。");
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
    /// 刷新商店页面。
    /// </summary>
    /// <param name="title">商店标题。</param>
    /// <param name="gold">当前金币。</param>
    /// <param name="refreshCount">剩余刷新次数。</param>
    /// <param name="nextText">下一步按钮文本。</param>
    /// <param name="names">商品名称列表。</param>
    /// <param name="descriptions">商品描述列表。</param>
    /// <param name="costs">商品价格列表。</param>
    /// <param name="lockedStates">商品锁定状态列表。</param>
    /// <param name="purchasedStates">商品购买状态列表。</param>
    /// <param name="colors">商品评级颜色列表。</param>
    public void Refresh(
        string title,
        int gold,
        int refreshCount,
        string nextText,
        string[] names,
        string[] descriptions,
        int[] costs,
        bool[] lockedStates,
        bool[] purchasedStates,
        Color[] colors)
    {
        SetText(titleText, title);
        SetText(goldText, $"金币：{gold}");

        if (refreshButton != null)
        {
            refreshButton.interactable = names != null && refreshCount > 0;
            SetButtonText(refreshButton, $"刷新商品（{refreshCount}）");
        }

        if (nextFloorButton != null)
            SetButtonText(nextFloorButton, nextText);

        bool hasOffers = names != null && names.Length > 0;
        SetActive(emptyText, !hasOffers);

        int itemCount = itemOfferViews != null ? itemOfferViews.Length : 0;
        RefreshOfferViews(itemOfferViews, 0, hasOffers, gold, names, descriptions, costs, lockedStates, purchasedStates, colors);
        RefreshOfferViews(relicOfferViews, itemCount, hasOffers, gold, names, descriptions, costs, lockedStates, purchasedStates, colors);
    }

    /// <summary>
    /// 绑定商店按钮。
    /// </summary>
    private void BindButtons()
    {
        if (refreshButton != null)
            refreshButton.onClick.AddListener(NotifyRefreshRequested);

        if (nextFloorButton != null)
            nextFloorButton.onClick.AddListener(NotifyNextFloorRequested);

        BindOfferButtons();
    }

    /// <summary>
    /// 解绑商店按钮。
    /// </summary>
    private void UnbindButtons()
    {
        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(NotifyRefreshRequested);

        if (nextFloorButton != null)
            nextFloorButton.onClick.RemoveListener(NotifyNextFloorRequested);

        UnbindOfferButtons();
    }

    /// <summary>
    /// 绑定商店商品按钮。
    /// </summary>
    private void BindOfferButtons()
    {
        int totalCount = GetOfferViewCount();
        _buyButtonActions = new UnityAction[totalCount];
        _lockButtonActions = new UnityAction[totalCount];
        BindOfferButtons(itemOfferViews, 0);
        BindOfferButtons(relicOfferViews, itemOfferViews != null ? itemOfferViews.Length : 0);
    }

    /// <summary>
    /// 解绑商店商品按钮。
    /// </summary>
    private void UnbindOfferButtons()
    {
        UnbindOfferButtons(itemOfferViews, 0);
        UnbindOfferButtons(relicOfferViews, itemOfferViews != null ? itemOfferViews.Length : 0);
    }

    /// <summary>
    /// 刷新指定类型的商店槽位。
    /// </summary>
    /// <param name="views">槽位视图数组。</param>
    /// <param name="offerStartIndex">对应商店商品起始索引。</param>
    /// <param name="hasOffers">是否有商品。</param>
    /// <param name="gold">当前金币。</param>
    /// <param name="names">商品名称数组。</param>
    /// <param name="descriptions">商品描述数组。</param>
    /// <param name="costs">商品价格数组。</param>
    /// <param name="lockedStates">锁定状态数组。</param>
    /// <param name="purchasedStates">购买状态数组。</param>
    /// <param name="colors">商品评级颜色数组。</param>
    private void RefreshOfferViews(
        GameShopOfferView[] views,
        int offerStartIndex,
        bool hasOffers,
        int gold,
        string[] names,
        string[] descriptions,
        int[] costs,
        bool[] lockedStates,
        bool[] purchasedStates,
        Color[] colors)
    {
        if (views == null)
            return;

        for (int i = 0; i < views.Length; i++)
        {
            GameShopOfferView offerView = views[i];
            if (offerView == null)
                continue;

            int offerIndex = offerStartIndex + i;
            bool visible = hasOffers && names != null && offerIndex < names.Length;
            offerView.SetVisible(visible);
            if (!visible)
                continue;

            int cost = GetArrayValue(costs, offerIndex);
            bool purchased = GetArrayValue(purchasedStates, offerIndex);
            offerView.Refresh(
                GetArrayValue(names, offerIndex),
                GetArrayValue(descriptions, offerIndex),
                cost,
                GetArrayValue(lockedStates, offerIndex),
                purchased,
                gold >= cost,
                GetArrayValue(colors, offerIndex, new Color(0.12f, 0.17f, 0.24f, 0.92f)));
        }
    }

    /// <summary>
    /// 绑定指定槽位组的购买和锁定按钮。
    /// </summary>
    /// <param name="views">槽位视图数组。</param>
    /// <param name="offerStartIndex">对应商店商品起始索引。</param>
    private void BindOfferButtons(GameShopOfferView[] views, int offerStartIndex)
    {
        if (views == null)
            return;

        for (int i = 0; i < views.Length; i++)
        {
            int offerIndex = offerStartIndex + i;
            GameShopOfferView offerView = views[i];
            if (offerView == null || offerIndex < 0 || offerIndex >= _buyButtonActions.Length)
                continue;

            _buyButtonActions[offerIndex] = () => OfferBuyRequested?.Invoke(offerIndex);
            _lockButtonActions[offerIndex] = () => OfferLockRequested?.Invoke(offerIndex);

            if (offerView.BuyButton != null)
                offerView.BuyButton.onClick.AddListener(_buyButtonActions[offerIndex]);

            if (offerView.LockButton != null)
                offerView.LockButton.onClick.AddListener(_lockButtonActions[offerIndex]);
        }
    }

    /// <summary>
    /// 解绑指定槽位组的购买和锁定按钮。
    /// </summary>
    /// <param name="views">槽位视图数组。</param>
    /// <param name="offerStartIndex">对应商店商品起始索引。</param>
    private void UnbindOfferButtons(GameShopOfferView[] views, int offerStartIndex)
    {
        if (views == null)
            return;

        for (int i = 0; i < views.Length; i++)
        {
            int offerIndex = offerStartIndex + i;
            GameShopOfferView offerView = views[i];
            if (offerView == null)
                continue;

            if (offerView.BuyButton != null && _buyButtonActions != null && offerIndex < _buyButtonActions.Length)
                offerView.BuyButton.onClick.RemoveListener(_buyButtonActions[offerIndex]);

            if (offerView.LockButton != null && _lockButtonActions != null && offerIndex < _lockButtonActions.Length)
                offerView.LockButton.onClick.RemoveListener(_lockButtonActions[offerIndex]);
        }
    }

    /// <summary>
    /// 获取当前配置的商店槽位总数。
    /// </summary>
    /// <returns>槽位总数。</returns>
    private int GetOfferViewCount()
    {
        int itemCount = itemOfferViews != null ? itemOfferViews.Length : 0;
        int relicCount = relicOfferViews != null ? relicOfferViews.Length : 0;
        return itemCount + relicCount;
    }

    /// <summary>
    /// 通知外部请求刷新商店。
    /// </summary>
    private void NotifyRefreshRequested()
    {
        RefreshRequested?.Invoke();
    }

    /// <summary>
    /// 通知外部请求进入下一层。
    /// </summary>
    private void NotifyNextFloorRequested()
    {
        NextFloorRequested?.Invoke();
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
    /// 设置文本物体显示状态。
    /// </summary>
    /// <param name="targetText">目标文本。</param>
    /// <param name="active">是否显示。</param>
    private void SetActive(TMP_Text targetText, bool active)
    {
        if (targetText == null)
            return;

        targetText.gameObject.SetActive(active);
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

    /// <summary>
    /// 安全读取整数数组值。
    /// </summary>
    /// <param name="values">数组。</param>
    /// <param name="index">索引。</param>
    /// <returns>数组值。</returns>
    private int GetArrayValue(int[] values, int index)
    {
        if (values == null || index < 0 || index >= values.Length)
            return 0;

        return values[index];
    }

    /// <summary>
    /// 安全读取布尔数组值。
    /// </summary>
    /// <param name="values">数组。</param>
    /// <param name="index">索引。</param>
    /// <returns>数组值。</returns>
    private bool GetArrayValue(bool[] values, int index)
    {
        if (values == null || index < 0 || index >= values.Length)
            return false;

        return values[index];
    }

    /// <summary>
    /// 安全读取颜色数组值。
    /// </summary>
    /// <param name="values">数组。</param>
    /// <param name="index">索引。</param>
    /// <param name="fallback">兜底颜色。</param>
    /// <returns>数组值。</returns>
    private Color GetArrayValue(Color[] values, int index, Color fallback)
    {
        if (values == null || index < 0 || index >= values.Length)
            return fallback;

        return values[index];
    }
}
