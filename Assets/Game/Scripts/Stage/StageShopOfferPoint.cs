using UnityEngine;

/// <summary>
/// 标记商店房间中的一个场景商品摆放点，运行时会绑定对应索引的商店商品。
/// </summary>
public sealed class StageShopOfferPoint : MonoBehaviour
{
    [Header("商品点")]
    [Tooltip("该摆放点绑定的商店商品索引，按 GameShopController 当前商品列表读取。")]
    [SerializeField] private int offerIndex;
    [Tooltip("该摆放点使用的场景商品视图；为空时会从自身或子对象查找。")]
    [SerializeField] private StageShopOfferView offerView;

    /// <summary>
    /// 商品索引。
    /// </summary>
    public int OfferIndex => Mathf.Max(0, offerIndex);

    /// <summary>
    /// 场景商品视图。
    /// </summary>
    public StageShopOfferView OfferView => offerView;

    private void Awake()
    {
        ResolveMissingReferences();
    }

    private void OnValidate()
    {
        offerIndex = Mathf.Max(0, offerIndex);
        ResolveMissingReferences();
    }

    /// <summary>
    /// 绑定指定商品索引和购买回调。
    /// </summary>
    /// <param name="index">商店商品索引。</param>
    /// <param name="offer">商品数据。</param>
    /// <param name="canBuy">当前金币是否足够购买。</param>
    /// <param name="buyCallback">购买请求回调。</param>
    public void BindOffer(int index, GameShopController.ShopOffer offer, bool canBuy, System.Action<int> buyCallback)
    {
        SetOfferIndex(index);
        ResolveMissingReferences();
        if (offerView != null)
            offerView.BindOffer(offerIndex, offer, canBuy, buyCallback);
    }

    /// <summary>
    /// 设置该商品点绑定的商店商品索引。
    /// </summary>
    /// <param name="index">商品索引。</param>
    public void SetOfferIndex(int index)
    {
        offerIndex = Mathf.Max(0, index);
    }

    /// <summary>
    /// 清空当前摆放点显示。
    /// </summary>
    public void ClearOffer()
    {
        ResolveMissingReferences();
        if (offerView != null)
            offerView.ClearOffer();
    }

    /// <summary>
    /// 补齐场景商品视图引用。
    /// </summary>
    private void ResolveMissingReferences()
    {
        if (offerView == null)
            offerView = GetComponentInChildren<StageShopOfferView>(true);
    }
}
