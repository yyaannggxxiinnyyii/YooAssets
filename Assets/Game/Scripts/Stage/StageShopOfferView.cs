using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 表现商店房间里的悬浮商品，负责商品显示、靠近提示和购买交互。
/// </summary>
public sealed class StageShopOfferView : MonoBehaviour
{
    [Header("交互")]
    [Tooltip("玩家靠近商品后允许交互的距离。")]
    [SerializeField] private float interactRadius = 1.15f;
    [Tooltip("触发购买交互的按键。")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("显示")]
    [Tooltip("商品图标渲染器；为空时会自动查找或创建。")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [Tooltip("商品图标缺失时使用的默认 Sprite。")]
    [SerializeField] private Sprite fallbackIconSprite;
    [Tooltip("商品名称 TMP 文本；为空时会自动创建世界空间 TextMeshPro。")]
    [SerializeField] private TMP_Text nameText;
    [Tooltip("商品价格 TMP 文本；为空时会自动创建世界空间 TextMeshPro。")]
    [SerializeField] private TMP_Text priceText;
    [Tooltip("靠近商品时显示的提示 TMP 文本；为空时会自动创建世界空间 TextMeshPro。")]
    [SerializeField] private TMP_Text tooltipText;
    [Tooltip("Tooltip 整体根节点；用于同时显隐背景框和文字。为空时只显隐 Tooltip 文本。")]
    [SerializeField] private GameObject tooltipRoot;
    [Tooltip("商品可视内容根节点；购买后会隐藏该节点，刷新并绑定新商品后重新显示。为空时仅隐藏图标、名称和价格。")]
    [SerializeField] private GameObject offerVisualRoot;
    [Tooltip("商品购买后是否隐藏当前槽位的可视内容。")]
    [SerializeField] private bool hidePurchasedOffer = true;
    [Tooltip("名称文本相对商品点的本地偏移，仅在脚本自动创建文本时使用。")]
    [SerializeField] private Vector3 nameOffset = new Vector3(0f, 0.7f, 0f);
    [Tooltip("价格文本相对商品点的本地偏移，仅在脚本自动创建文本时使用。")]
    [SerializeField] private Vector3 priceOffset = new Vector3(0f, -0.55f, 0f);
    [Tooltip("提示文本相对商品点的本地偏移，仅在脚本自动创建文本时使用。")]
    [SerializeField] private Vector3 tooltipOffset = new Vector3(0f, 1.05f, 0f);
    [Tooltip("商品上下悬浮的振幅。")]
    [SerializeField] private float floatAmplitude = 0.08f;
    [Tooltip("商品上下悬浮的速度。")]
    [SerializeField] private float floatSpeed = 2.5f;

    private int _offerIndex = -1;
    private GameShopController.ShopOffer _offer;
    private Transform _player;
    private Action<int> _buyCallback;
    private Vector3 _iconBaseLocalPosition;
    private bool _hasOffer;
    private bool _canBuy;
    private bool _playerInRange;

    private void Awake()
    {
        EnsureVisualReferences();
        _iconBaseLocalPosition = iconRenderer != null ? iconRenderer.transform.localPosition : Vector3.zero;
        SetTooltipVisible(false);
    }

    private void Update()
    {
        TickFloatAnimation();
        if (!_hasOffer || _offer.Purchased)
        {
            SetPlayerInRange(false);
            return;
        }

        RefreshPlayerRangeByDistance();
        if (!_playerInRange)
            return;

        if (Input.GetKeyDown(interactKey))
            _buyCallback?.Invoke(_offerIndex);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        GamePlayerController player = other.GetComponentInParent<GamePlayerController>();
        _player = player != null ? player.transform : other.transform;
        SetPlayerInRange(CanInteractCurrentOffer());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_player == null || other.GetComponentInParent<GamePlayerController>() == null)
            return;

        _player = null;
        SetPlayerInRange(false);
    }

    /// <summary>
    /// 在 Scene 视图中绘制商品的距离交互范围。
    /// </summary>
    private void OnDrawGizmos()
    {
        GameRadiusGizmoUtility.DrawRadius(transform.position, Mathf.Max(0.1f, interactRadius), new Color(0.25f, 0.85f, 1f, 0.85f), "Offer Interact");
    }

    /// <summary>
    /// 绑定商店商品数据和购买回调。
    /// </summary>
    /// <param name="offerIndex">商品索引。</param>
    /// <param name="offer">商品数据。</param>
    /// <param name="canBuy">当前是否有足够金币购买。</param>
    /// <param name="buyCallback">购买请求回调。</param>
    public void BindOffer(int offerIndex, GameShopController.ShopOffer offer, bool canBuy, Action<int> buyCallback)
    {
        _offerIndex = offerIndex;
        _offer = offer;
        _canBuy = canBuy;
        _buyCallback = buyCallback;
        _hasOffer = true;
        ApplyOfferVisual();
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 清空当前商品显示。
    /// </summary>
    public void ClearOffer()
    {
        _offerIndex = -1;
        _offer = default;
        _buyCallback = null;
        _hasOffer = false;
        _canBuy = false;
        _player = null;
        SetOfferVisualVisible(false);
        SetPlayerInRange(false);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 根据商品数据刷新图标、文本和购买状态。
    /// </summary>
    private void ApplyOfferVisual()
    {
        EnsureVisualReferences();
        Sprite icon = ResolveOfferIcon(_offer);
        if (iconRenderer != null)
        {
            iconRenderer.sprite = icon != null ? icon : fallbackIconSprite;
            iconRenderer.color = Color.white;
        }

        SetText(nameText, _offer.Name);
        SetText(priceText, $"{_offer.Cost}G");
        if (nameText != null)
            nameText.color = _offer.RarityColor;
        if (priceText != null)
            priceText.color = _canBuy ? Color.white : new Color(1f, 0.35f, 0.25f);

        RefreshTooltipText();
        SetOfferVisualVisible(!_offer.Purchased || !hidePurchasedOffer);
        SetPlayerInRange(false);
        SetTooltipVisible(false);
    }

    /// <summary>
    /// 获取商品应显示的图标。
    /// </summary>
    /// <param name="offer">商品数据。</param>
    /// <returns>商品图标。</returns>
    private Sprite ResolveOfferIcon(GameShopController.ShopOffer offer)
    {
        if (offer.ItemType == ShopItemData.ShopItemType.Relic && offer.RelicData != null)
            return offer.RelicData.Icon;

        if (offer.ItemData != null)
            return offer.ItemData.Icon;

        return fallbackIconSprite;
    }

    /// <summary>
    /// 刷新玩家距离状态。
    /// </summary>
    private void RefreshPlayerRangeByDistance()
    {
        if (_player == null)
            CachePlayerTransform();

        bool isInRange = _player != null &&
            CanInteractCurrentOffer() &&
            Vector2.Distance(transform.position, _player.position) <= Mathf.Max(0.1f, interactRadius);
        SetPlayerInRange(isInRange);
    }

    /// <summary>
    /// 查找并缓存当前玩家 Transform。
    /// </summary>
    private void CachePlayerTransform()
    {
        GamePlayerController player = FindObjectOfType<GamePlayerController>();
        if (player != null)
            _player = player.transform;
    }

    /// <summary>
    /// 设置玩家交互范围状态。
    /// </summary>
    /// <param name="isInRange">玩家是否在交互范围内。</param>
    private void SetPlayerInRange(bool isInRange)
    {
        if (isInRange && !CanInteractCurrentOffer())
            isInRange = false;

        if (_playerInRange == isInRange)
            return;

        _playerInRange = isInRange;
        SetTooltipVisible(_playerInRange);
    }

    /// <summary>
    /// 刷新商品提示文本。
    /// </summary>
    private void RefreshTooltipText()
    {
        if (tooltipText == null)
            return;

        if (_offer.Purchased)
        {
            tooltipText.text = "已购买";
            return;
        }

        string buyText = _canBuy ? $"E 购买 {_offer.Name}" : $"金币不足：需要 {_offer.Cost}G";
        tooltipText.text = $"{buyText}\n{_offer.Description}";
    }

    /// <summary>
    /// 设置 Tooltip 显隐。
    /// </summary>
    /// <param name="visible">是否显示。</param>
    private void SetTooltipVisible(bool visible)
    {
        if (visible && !CanInteractCurrentOffer())
            visible = false;

        if (tooltipRoot != null)
        {
            tooltipRoot.SetActive(visible);
            return;
        }

        if (tooltipText != null)
            tooltipText.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 播放商品悬浮动画。
    /// </summary>
    private void TickFloatAnimation()
    {
        if (iconRenderer == null || !_hasOffer || (_offer.Purchased && hidePurchasedOffer) || floatAmplitude <= 0f || floatSpeed <= 0f)
            return;

        Vector3 offset = new Vector3(0f, Mathf.Sin(Time.time * floatSpeed) * floatAmplitude, 0f);
        iconRenderer.transform.localPosition = _iconBaseLocalPosition + offset;
    }

    /// <summary>
    /// 判断碰撞体是否属于玩家。
    /// </summary>
    /// <param name="other">进入触发区的碰撞体。</param>
    /// <returns>属于玩家时返回 true。</returns>
    private bool IsPlayerCollider(Collider2D other)
    {
        return other != null && other.GetComponentInParent<GamePlayerController>() != null;
    }

    /// <summary>
    /// 确保显示引用存在。
    /// </summary>
    private void EnsureVisualReferences()
    {
        if (iconRenderer == null)
            iconRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (iconRenderer == null)
        {
            GameObject iconObject = new GameObject("OfferIcon");
            iconObject.transform.SetParent(transform, false);
            iconRenderer = iconObject.AddComponent<SpriteRenderer>();
            iconRenderer.sortingOrder = 5;
        }

        if (nameText == null)
            nameText = CreateOrFindText("OfferName", nameOffset, 0.28f, 20);

        if (priceText == null)
            priceText = CreateOrFindText("OfferPrice", priceOffset, 0.24f, 20);

        if (tooltipText == null)
            tooltipText = CreateOrFindText("OfferTooltip", tooltipOffset, 0.24f, 100);

        if (tooltipRoot == null && tooltipText != null)
            tooltipRoot = tooltipText.gameObject;
    }

    /// <summary>
    /// 判断当前商品是否允许玩家交互。
    /// </summary>
    /// <returns>当前商品可购买时返回 true。</returns>
    private bool CanInteractCurrentOffer()
    {
        return _hasOffer && !_offer.Purchased;
    }

    /// <summary>
    /// 设置商品图标、名称和价格等可视内容的显隐。
    /// </summary>
    /// <param name="visible">是否显示商品可视内容。</param>
    private void SetOfferVisualVisible(bool visible)
    {
        if (offerVisualRoot != null && offerVisualRoot != gameObject)
        {
            offerVisualRoot.SetActive(visible);
            return;
        }

        if (iconRenderer != null)
            iconRenderer.gameObject.SetActive(visible);
        if (nameText != null)
            nameText.gameObject.SetActive(visible);
        if (priceText != null)
            priceText.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 查找或创建指定名称的世界空间文本。
    /// </summary>
    /// <param name="objectName">文本对象名称。</param>
    /// <param name="localPosition">本地位置。</param>
    /// <param name="fontSize">字体大小。</param>
    /// <param name="sortingOrder">排序层级。</param>
    /// <returns>文本组件。</returns>
    private TMP_Text CreateOrFindText(string objectName, Vector3 localPosition, float fontSize, int sortingOrder)
    {
        Transform existing = transform.Find(objectName);
        TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;
        if (text == null)
        {
            GameObject textObject = new GameObject(objectName);
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = localPosition;
            text = textObject.AddComponent<TextMeshPro>();
        }

        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.enableWordWrapping = false;
        if (text is TextMeshPro worldText)
            worldText.sortingOrder = sortingOrder;

        return text;
    }

    /// <summary>
    /// 安全设置文本内容。
    /// </summary>
    /// <param name="targetText">目标文本。</param>
    /// <param name="content">文本内容。</param>
    private void SetText(TMP_Text targetText, string content)
    {
        if (targetText != null)
            targetText.text = content;
    }
}
