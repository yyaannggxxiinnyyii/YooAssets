using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 管理商店商品池、商品刷新、锁定和购买状态。
/// </summary>
public sealed class GameShopController : MonoBehaviour
{
    /// <summary>
    /// 商店运行时商品数据。
    /// </summary>
    public struct ShopOffer
    {
        public string Name;
        public string Description;
        public int Cost;
        public ShopItemData.ShopItemType ItemType;
        public GameRarity Rarity;
        public string RarityName;
        public Color RarityColor;
        public ItemData ItemData;
        public RelicData RelicData;
        public bool Purchased;
        public bool Locked;
    }

    /// <summary>
    /// 定义商店按评级抽取商品时使用的权重。
    /// </summary>
    [Serializable]
    public sealed class ShopRarityWeight
    {
        [Tooltip("本条权重配置对应的商品品质。")]
        [SerializeField] private GameRarity rarity = GameRarity.Common;
        [Tooltip("该品质的抽取权重，小于 0 时按 0 处理。")]
        [SerializeField] private float weight = 1f;

        /// <summary>
        /// 创建默认评级权重配置，供 Unity 序列化使用。
        /// </summary>
        public ShopRarityWeight()
        {
        }

        /// <summary>
        /// 创建指定评级和权重的配置。
        /// </summary>
        /// <param name="rarity">评级。</param>
        /// <param name="weight">权重。</param>
        public ShopRarityWeight(GameRarity rarity, float weight)
        {
            this.rarity = rarity;
            this.weight = weight;
        }

        /// <summary>
        /// 评级。
        /// </summary>
        public GameRarity Rarity => rarity;

        /// <summary>
        /// 抽取权重，小于 0 时按 0 处理。
        /// </summary>
        public float Weight => Mathf.Max(0f, weight);
    }

    /// <summary>
    /// 定义商店按品质使用的默认固定价格。
    /// </summary>
    [Serializable]
    public sealed class ShopRarityPrice
    {
        [Tooltip("本条价格配置对应的商品品质。")]
        [SerializeField] private GameRarity rarity = GameRarity.Common;
        [Tooltip("该品质的默认固定价格。")]
        [SerializeField] private int price = 1;

        /// <summary>
        /// 创建默认品质价格配置，供 Unity 序列化使用。
        /// </summary>
        public ShopRarityPrice()
        {
        }

        /// <summary>
        /// 创建指定品质和固定价格的配置。
        /// </summary>
        /// <param name="rarity">目标品质。</param>
        /// <param name="price">固定默认价格。</param>
        public ShopRarityPrice(GameRarity rarity, int price)
        {
            this.rarity = rarity;
            this.price = price;
        }

        /// <summary>
        /// 本条配置对应的品质。
        /// </summary>
        public GameRarity Rarity => rarity;

        /// <summary>
        /// 固定价格，最小为 1。
        /// </summary>
        public int Price => Mathf.Max(1, price);
    }

    [Header("商店数据")]
    [Tooltip("商店可随机出现的商品数据资产列表。")]
    [SerializeField] private ShopItemData[] shopItemDataList;

    [Header("刷新配置")]
    [Tooltip("每次商店界面展示的道具槽位数量。")]
    [FormerlySerializedAs("shopOfferCount")]
    [SerializeField] private int itemOfferCount = 3;
    [Tooltip("每次商店界面展示的遗物槽位数量。")]
    [SerializeField] private int relicOfferCount = 3;
    [Header("评级权重")]
    [Tooltip("道具槽位按评级抽取的权重。仅有对应评级商品时才会参与计算。")]
    [SerializeField] private ShopRarityWeight[] itemRarityWeights =
    {
        new ShopRarityWeight(GameRarity.Common, 70f),
        new ShopRarityWeight(GameRarity.Uncommon, 20f),
        new ShopRarityWeight(GameRarity.Rare, 8f),
        new ShopRarityWeight(GameRarity.Epic, 2f),
        new ShopRarityWeight(GameRarity.Legendary, 0.5f)
    };
    [Tooltip("遗物槽位按评级抽取的权重。仅有对应评级商品时才会参与计算。")]
    [SerializeField] private ShopRarityWeight[] relicRarityWeights =
    {
        new ShopRarityWeight(GameRarity.Common, 0f),
        new ShopRarityWeight(GameRarity.Uncommon, 30f),
        new ShopRarityWeight(GameRarity.Rare, 30f),
        new ShopRarityWeight(GameRarity.Epic, 25f),
        new ShopRarityWeight(GameRarity.Legendary, 15f)
    };

    [Header("价格配置")]
    [Tooltip("道具按品质使用的默认固定价格。单个商品未启用覆盖价格时使用这里的配置。")]
    [SerializeField] private ShopRarityPrice[] itemDefaultPrices =
    {
        new ShopRarityPrice(GameRarity.Common, 3),
        new ShopRarityPrice(GameRarity.Uncommon, 5),
        new ShopRarityPrice(GameRarity.Rare, 8),
        new ShopRarityPrice(GameRarity.Epic, 12),
        new ShopRarityPrice(GameRarity.Legendary, 18)
    };
    [Tooltip("遗物按品质使用的默认固定价格。单个商品未启用覆盖价格时使用这里的配置。")]
    [SerializeField] private ShopRarityPrice[] relicDefaultPrices =
    {
        new ShopRarityPrice(GameRarity.Common, 10),
        new ShopRarityPrice(GameRarity.Uncommon, 16),
        new ShopRarityPrice(GameRarity.Rare, 24),
        new ShopRarityPrice(GameRarity.Epic, 36),
        new ShopRarityPrice(GameRarity.Legendary, 54)
    };

    private ShopOffer[] _shopOffers;
    private bool _prepared;
    private int _runtimeItemOfferCountOffset;
    private int _runtimeRelicOfferCountOffset;

    /// <summary>
    /// 当前道具槽位数量，包含运行时加减值。
    /// </summary>
    public int CurrentItemOfferCount => Mathf.Max(0, itemOfferCount + _runtimeItemOfferCountOffset);

    /// <summary>
    /// 当前遗物槽位数量，包含运行时加减值。
    /// </summary>
    public int CurrentRelicOfferCount => Mathf.Max(0, relicOfferCount + _runtimeRelicOfferCountOffset);

    /// <summary>
    /// 当前商店总槽位数量。
    /// </summary>
    public int CurrentTotalOfferCount => CurrentItemOfferCount + CurrentRelicOfferCount;

    /// <summary>
    /// 当前商店商品是否已经准备完成。
    /// </summary>
    public bool IsPrepared => _prepared;

    /// <summary>
    /// 准备当前楼层的商店商品。
    /// </summary>
    /// <param name="floor">当前楼层。</param>
    /// <param name="canAcquireRelic">遗物可获得过滤器。</param>
    public void PrepareShop(int floor, Func<RelicData, bool> canAcquireRelic = null)
    {
        if (_prepared)
            return;

        _prepared = true;
        ShopOfferPools offerPools = CreateShopOfferPools(floor, canAcquireRelic);
        if (!offerPools.HasAnyOffer)
        {
            Debug.LogWarning("[Shop] 商店商品池为空，请在 GameShopController 配置 Shop Item Data List。");
            return;
        }

        _shopOffers = RollShopOffers(offerPools, null, canAcquireRelic);
    }

    /// <summary>
    /// 清理商店运行时状态。
    /// </summary>
    public void ResetShopState()
    {
        _prepared = false;
        _shopOffers = null;
    }

    /// <summary>
    /// 设置运行时道具槽位增减值，供遗物或其他系统临时影响商店结构。
    /// </summary>
    /// <param name="offset">相对于基础道具槽位数量的增减值。</param>
    public void SetRuntimeItemOfferCountOffset(int offset)
    {
        _runtimeItemOfferCountOffset = offset;
        ResetShopState();
    }

    /// <summary>
    /// 设置运行时遗物槽位增减值，供遗物或其他系统临时影响商店结构。
    /// </summary>
    /// <param name="offset">相对于基础遗物槽位数量的增减值。</param>
    public void SetRuntimeRelicOfferCountOffset(int offset)
    {
        _runtimeRelicOfferCountOffset = offset;
        ResetShopState();
    }

    /// <summary>
    /// 累加运行时道具槽位增减值。
    /// </summary>
    /// <param name="delta">槽位变化量。</param>
    public void AddRuntimeItemOfferCountOffset(int delta)
    {
        SetRuntimeItemOfferCountOffset(_runtimeItemOfferCountOffset + delta);
    }

    /// <summary>
    /// 累加运行时遗物槽位增减值。
    /// </summary>
    /// <param name="delta">槽位变化量。</param>
    public void AddRuntimeRelicOfferCountOffset(int delta)
    {
        SetRuntimeRelicOfferCountOffset(_runtimeRelicOfferCountOffset + delta);
    }

    /// <summary>
    /// 清理运行时槽位增减值，恢复配置中的基础槽位数量。
    /// </summary>
    public void ClearRuntimeOfferCountOffsets()
    {
        _runtimeItemOfferCountOffset = 0;
        _runtimeRelicOfferCountOffset = 0;
        ResetShopState();
    }

    /// <summary>
    /// 尝试购买指定商品。
    /// </summary>
    /// <param name="index">商品索引。</param>
    /// <param name="buyer">购买商品的玩家。</param>
    /// <param name="purchasedOffer">购买成功的商品。</param>
    /// <returns>购买成功时返回 true。</returns>
    public bool TryBuyOffer(int index, GamePlayerController buyer, out ShopOffer purchasedOffer, Func<RelicData, bool> canAcquireRelic = null)
    {
        purchasedOffer = default;
        if (buyer == null || _shopOffers == null || index < 0 || index >= _shopOffers.Length)
            return false;

        ShopOffer offer = _shopOffers[index];
        if (offer.Purchased)
            return false;

        if (offer.ItemType == ShopItemData.ShopItemType.Relic &&
            canAcquireRelic != null &&
            !canAcquireRelic.Invoke(offer.RelicData))
            return false;

        if (!buyer.TrySpendGold(offer.Cost))
            return false;

        offer.Purchased = true;
        _shopOffers[index] = offer;
        purchasedOffer = offer;
        return true;
    }

    /// <summary>
    /// 尝试获取指定索引的商店商品。
    /// </summary>
    /// <param name="index">商品索引。</param>
    /// <param name="offer">查询到的商品数据。</param>
    /// <returns>存在商品时返回 true。</returns>
    public bool TryGetOffer(int index, out ShopOffer offer)
    {
        offer = default;
        if (_shopOffers == null || index < 0 || index >= _shopOffers.Length)
            return false;

        offer = _shopOffers[index];
        return true;
    }

    /// <summary>
    /// 获取当前商店商品数量。
    /// </summary>
    /// <returns>商品数量。</returns>
    public int GetOfferCount()
    {
        return _shopOffers != null ? _shopOffers.Length : 0;
    }

    /// <summary>
    /// 尝试消耗商店刷新次数并刷新当前商店商品。
    /// </summary>
    /// <param name="floor">当前楼层。</param>
    /// <param name="buyer">消耗刷新次数的玩家。</param>
    /// <returns>刷新成功时返回 true。</returns>
    public bool TryRefreshShop(int floor, GamePlayerController buyer, Func<RelicData, bool> canAcquireRelic = null)
    {
        if (buyer == null)
            return false;

        ShopOfferPools offerPools = CreateShopOfferPools(floor, canAcquireRelic);
        if (!offerPools.HasAnyOffer)
        {
            Debug.LogWarning("[Shop] 商店商品池为空，无法刷新商店。");
            return false;
        }

        if (!buyer.TryConsumeShopRefresh())
            return false;

        _prepared = true;
        _shopOffers = RollShopOffers(offerPools, _shopOffers, canAcquireRelic);
        return true;
    }

    /// <summary>
    /// 切换指定商店商品的锁定状态。
    /// </summary>
    /// <param name="index">商品索引。</param>
    public void ToggleOfferLock(int index)
    {
        if (_shopOffers == null || index < 0 || index >= _shopOffers.Length)
            return;

        ShopOffer offer = _shopOffers[index];
        if (offer.Purchased)
            return;

        offer.Locked = !offer.Locked;
        _shopOffers[index] = offer;
    }

    /// <summary>
    /// 获取商店商品名称列表。
    /// </summary>
    /// <returns>商品名称列表。</returns>
    public string[] GetOfferNames()
    {
        if (_shopOffers == null)
            return null;

        string[] names = new string[_shopOffers.Length];
        for (int i = 0; i < _shopOffers.Length; i++)
            names[i] = $"[{_shopOffers[i].RarityName}] {_shopOffers[i].Name}";

        return names;
    }

    /// <summary>
    /// 获取商店商品描述列表。
    /// </summary>
    /// <returns>商品描述列表。</returns>
    public string[] GetOfferDescriptions()
    {
        if (_shopOffers == null)
            return null;

        string[] descriptions = new string[_shopOffers.Length];
        for (int i = 0; i < _shopOffers.Length; i++)
            descriptions[i] = _shopOffers[i].Description;

        return descriptions;
    }

    /// <summary>
    /// 获取商店商品价格列表。
    /// </summary>
    /// <returns>商品价格列表。</returns>
    public int[] GetOfferCosts()
    {
        if (_shopOffers == null)
            return null;

        int[] costs = new int[_shopOffers.Length];
        for (int i = 0; i < _shopOffers.Length; i++)
            costs[i] = _shopOffers[i].Cost;

        return costs;
    }

    /// <summary>
    /// 获取商店商品评级颜色列表。
    /// </summary>
    /// <returns>商品评级颜色列表。</returns>
    public Color[] GetOfferColors()
    {
        if (_shopOffers == null)
            return null;

        Color[] colors = new Color[_shopOffers.Length];
        for (int i = 0; i < _shopOffers.Length; i++)
            colors[i] = _shopOffers[i].RarityColor;

        return colors;
    }

    /// <summary>
    /// 获取商店商品锁定状态列表。
    /// </summary>
    /// <returns>锁定状态列表。</returns>
    public bool[] GetOfferLockedStates()
    {
        if (_shopOffers == null)
            return null;

        bool[] lockedStates = new bool[_shopOffers.Length];
        for (int i = 0; i < _shopOffers.Length; i++)
            lockedStates[i] = _shopOffers[i].Locked;

        return lockedStates;
    }

    /// <summary>
    /// 获取商店商品购买状态列表。
    /// </summary>
    /// <returns>购买状态列表。</returns>
    public bool[] GetOfferPurchasedStates()
    {
        if (_shopOffers == null)
            return null;

        bool[] purchasedStates = new bool[_shopOffers.Length];
        for (int i = 0; i < _shopOffers.Length; i++)
            purchasedStates[i] = _shopOffers[i].Purchased;

        return purchasedStates;
    }

    /// <summary>
    /// 从分类型商品池中随机抽取本次商店商品。
    /// </summary>
    /// <param name="offerPools">分类型商品候选池。</param>
    /// <param name="lockedOffers">需要保留的锁定商品。</param>
    /// <returns>本次商店展示的商品列表。</returns>
    private ShopOffer[] RollShopOffers(ShopOfferPools offerPools, ShopOffer[] lockedOffers, Func<RelicData, bool> canAcquireRelic = null)
    {
        List<ShopOffer> offers = new List<ShopOffer>();
        RollShopOffersByType(
            offers,
            offerPools.ItemOffers,
            lockedOffers,
            ShopItemData.ShopItemType.Item,
            CurrentItemOfferCount,
            itemRarityWeights,
            canAcquireRelic);
        RollShopOffersByType(
            offers,
            offerPools.RelicOffers,
            lockedOffers,
            ShopItemData.ShopItemType.Relic,
            CurrentRelicOfferCount,
            relicRarityWeights,
            canAcquireRelic);

        return offers.ToArray();
    }

    /// <summary>
    /// 按商品类型抽取指定槽位数量的商品，确保道具槽和遗物槽互不混用。
    /// </summary>
    /// <param name="offers">本次商店商品列表。</param>
    /// <param name="offerPool">指定类型的候选池。</param>
    /// <param name="lockedOffers">需要保留的锁定商品。</param>
    /// <param name="itemType">本次抽取的商品类型。</param>
    /// <param name="offerCount">本类型槽位数量。</param>
    /// <param name="rarityWeights">本类型商品使用的评级权重。</param>
    /// <param name="canAcquireRelic">遗物可获得过滤器。</param>
    private void RollShopOffersByType(
        List<ShopOffer> offers,
        List<ShopOffer> offerPool,
        ShopOffer[] lockedOffers,
        ShopItemData.ShopItemType itemType,
        int offerCount,
        ShopRarityWeight[] rarityWeights,
        Func<RelicData, bool> canAcquireRelic)
    {
        if (offerCount <= 0)
            return;

        int startCount = offers.Count;
        AddLockedShopOffers(offers, offerPool, lockedOffers, itemType, offerCount, canAcquireRelic);

        int targetCount = startCount + Mathf.Min(offerCount, offers.Count - startCount + offerPool.Count);
        while (offers.Count < targetCount && offerPool.Count > 0)
        {
            int offerIndex = FindWeightedOfferIndex(offerPool, rarityWeights);
            if (offerIndex < 0)
                break;

            ShopOffer offer = offerPool[offerIndex];
            offers.Add(offer);
            offerPool.RemoveAt(offerIndex);
            RemoveDuplicateUniqueRelicOffers(offerPool, offer);
        }
    }

    /// <summary>
    /// 将指定类型的锁定商品加入本次商店列表，并从候选池中移除同名商品。
    /// </summary>
    /// <param name="offers">本次商店商品列表。</param>
    /// <param name="offerPool">商品候选池。</param>
    /// <param name="lockedOffers">需要保留的锁定商品。</param>
    /// <param name="itemType">本次处理的商品类型。</param>
    /// <param name="offerCount">本类型槽位数量。</param>
    /// <param name="canAcquireRelic">遗物可获得过滤器。</param>
    private void AddLockedShopOffers(
        List<ShopOffer> offers,
        List<ShopOffer> offerPool,
        ShopOffer[] lockedOffers,
        ShopItemData.ShopItemType itemType,
        int offerCount,
        Func<RelicData, bool> canAcquireRelic)
    {
        if (lockedOffers == null)
            return;

        int startCount = offers.Count;
        for (int i = 0; i < lockedOffers.Length && offers.Count - startCount < offerCount; i++)
        {
            ShopOffer lockedOffer = lockedOffers[i];
            if (!lockedOffer.Locked || lockedOffer.Purchased || lockedOffer.ItemType != itemType)
                continue;

            if (lockedOffer.ItemType == ShopItemData.ShopItemType.Relic &&
                canAcquireRelic != null &&
                !canAcquireRelic.Invoke(lockedOffer.RelicData))
                continue;

            offers.Add(lockedOffer);
            RemoveShopOfferByName(offerPool, lockedOffer.Name);
            RemoveDuplicateUniqueRelicOffers(offerPool, lockedOffer);
        }
    }

    /// <summary>
    /// 从商品候选池中移除指定名称的商品，避免刷新后出现重复商品。
    /// </summary>
    /// <param name="offerPool">商品候选池。</param>
    /// <param name="offerName">商品名称。</param>
    private void RemoveShopOfferByName(List<ShopOffer> offerPool, string offerName)
    {
        for (int i = offerPool.Count - 1; i >= 0; i--)
        {
            if (offerPool[i].Name == offerName)
                offerPool.RemoveAt(i);
        }
    }

    /// <summary>
    /// 从候选池移除同 RelicId 的全局唯一遗物，避免同一轮商店出现重复唯一遗物。
    /// </summary>
    /// <param name="offerPool">商品候选池。</param>
    /// <param name="selectedOffer">已选中的商品。</param>
    private void RemoveDuplicateUniqueRelicOffers(List<ShopOffer> offerPool, ShopOffer selectedOffer)
    {
        if (selectedOffer.ItemType != ShopItemData.ShopItemType.Relic ||
            selectedOffer.RelicData == null ||
            !selectedOffer.RelicData.GlobalUnique)
            return;

        string relicId = selectedOffer.RelicData.RelicId;
        for (int i = offerPool.Count - 1; i >= 0; i--)
        {
            RelicData relicData = offerPool[i].RelicData;
            if (relicData != null && relicData.RelicId == relicId)
                offerPool.RemoveAt(i);
        }
    }

    /// <summary>
    /// 按评级权重从候选池中选择一个商品索引。
    /// </summary>
    /// <param name="offerPool">商品候选池。</param>
    /// <param name="rarityWeights">评级权重配置。</param>
    /// <returns>商品索引。</returns>
    private int FindWeightedOfferIndex(List<ShopOffer> offerPool, ShopRarityWeight[] rarityWeights)
    {
        if (offerPool == null || offerPool.Count <= 0)
            return -1;

        GameRarity rarity = RollOfferRarity(offerPool, rarityWeights);
        int rarityOfferCount = CountOffersByRarity(offerPool, rarity);
        if (rarityOfferCount <= 0)
            return UnityEngine.Random.Range(0, offerPool.Count);

        int targetIndex = UnityEngine.Random.Range(0, rarityOfferCount);
        for (int i = 0; i < offerPool.Count; i++)
        {
            if (offerPool[i].Rarity != rarity)
                continue;

            if (targetIndex == 0)
                return i;

            targetIndex--;
        }

        return UnityEngine.Random.Range(0, offerPool.Count);
    }

    /// <summary>
    /// 根据当前候选池和权重配置抽取一个评级。
    /// </summary>
    /// <param name="offerPool">商品候选池。</param>
    /// <param name="rarityWeights">评级权重配置。</param>
    /// <returns>抽取到的评级。</returns>
    private GameRarity RollOfferRarity(List<ShopOffer> offerPool, ShopRarityWeight[] rarityWeights)
    {
        if (rarityWeights == null || rarityWeights.Length <= 0)
            return offerPool[UnityEngine.Random.Range(0, offerPool.Count)].Rarity;

        float totalWeight = 0f;
        for (int i = 0; i < rarityWeights.Length; i++)
        {
            ShopRarityWeight rarityWeight = rarityWeights[i];
            if (rarityWeight == null ||
                rarityWeight.Weight <= 0f ||
                !HasOfferByRarity(offerPool, rarityWeight.Rarity))
                continue;

            totalWeight += rarityWeight.Weight;
        }

        if (totalWeight <= 0f)
            return offerPool[UnityEngine.Random.Range(0, offerPool.Count)].Rarity;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        for (int i = 0; i < rarityWeights.Length; i++)
        {
            ShopRarityWeight rarityWeight = rarityWeights[i];
            if (rarityWeight == null ||
                rarityWeight.Weight <= 0f ||
                !HasOfferByRarity(offerPool, rarityWeight.Rarity))
                continue;

            roll -= rarityWeight.Weight;
            if (roll <= 0f)
                return rarityWeight.Rarity;
        }

        return offerPool[UnityEngine.Random.Range(0, offerPool.Count)].Rarity;
    }

    /// <summary>
    /// 判断候选池中是否存在指定评级商品。
    /// </summary>
    /// <param name="offerPool">商品候选池。</param>
    /// <param name="rarity">目标评级。</param>
    /// <returns>存在时返回 true。</returns>
    private bool HasOfferByRarity(List<ShopOffer> offerPool, GameRarity rarity)
    {
        return CountOffersByRarity(offerPool, rarity) > 0;
    }

    /// <summary>
    /// 统计候选池中指定评级商品数量。
    /// </summary>
    /// <param name="offerPool">商品候选池。</param>
    /// <param name="rarity">目标评级。</param>
    /// <returns>商品数量。</returns>
    private int CountOffersByRarity(List<ShopOffer> offerPool, GameRarity rarity)
    {
        if (offerPool == null)
            return 0;

        int count = 0;
        for (int i = 0; i < offerPool.Count; i++)
        {
            if (offerPool[i].Rarity == rarity)
                count++;
        }

        return count;
    }

    /// <summary>
    /// 创建按商品类型分组的商店候选池。
    /// </summary>
    /// <param name="floor">当前楼层。</param>
    /// <param name="canAcquireRelic">遗物可获得过滤器。</param>
    /// <returns>分类型商店商品候选池。</returns>
    private ShopOfferPools CreateShopOfferPools(int floor, Func<RelicData, bool> canAcquireRelic = null)
    {
        ShopOfferPools offerPools = new ShopOfferPools();
        if (shopItemDataList == null || shopItemDataList.Length <= 0)
            return offerPools;

        foreach (ShopItemData itemData in shopItemDataList)
        {
            if (itemData == null)
                continue;

            if (itemData.ItemType == ShopItemData.ShopItemType.Relic &&
                canAcquireRelic != null &&
                !canAcquireRelic.Invoke(itemData.RelicData))
                continue;

            if (itemData.ItemType == ShopItemData.ShopItemType.Relic)
                offerPools.RelicOffers.Add(CreateOffer(itemData, floor));
            else
                offerPools.ItemOffers.Add(CreateOffer(itemData, floor));
        }

        return offerPools;
    }

    /// <summary>
    /// 根据数据资产创建商店商品。
    /// </summary>
    /// <param name="itemData">商店商品数据资产。</param>
    /// <param name="floor">当前楼层。</param>
    /// <returns>商店商品。</returns>
    private ShopOffer CreateOffer(ShopItemData itemData, int floor)
    {
        return new ShopOffer
        {
            Name = itemData.DisplayName,
            Description = itemData.Description,
            Cost = GetOfferPrice(itemData),
            ItemType = itemData.ItemType,
            Rarity = itemData.Rarity,
            RarityName = itemData.RarityName,
            RarityColor = itemData.RarityColor,
            ItemData = itemData.ItemData,
            RelicData = itemData.RelicData,
            Purchased = false,
            Locked = false
        };
    }

    /// <summary>
    /// 获取指定商店商品的固定价格。
    /// </summary>
    /// <param name="itemData">商店商品数据资产。</param>
    /// <returns>最终固定价格。</returns>
    private int GetOfferPrice(ShopItemData itemData)
    {
        if (itemData == null)
            return 1;

        if (itemData.OverridePrice)
            return itemData.OverrideFixedPrice;

        if (itemData.ItemType == ShopItemData.ShopItemType.Relic)
            return GetDefaultPrice(itemData.Rarity, relicDefaultPrices, 10);

        return GetDefaultPrice(itemData.Rarity, itemDefaultPrices, 3);
    }

    /// <summary>
    /// 查找目标品质对应的默认固定价格。
    /// </summary>
    /// <param name="rarity">目标品质。</param>
    /// <param name="prices">品质价格配置表。</param>
    /// <param name="fallback">没有匹配配置时使用的后备价格。</param>
    /// <returns>默认固定价格。</returns>
    private int GetDefaultPrice(GameRarity rarity, ShopRarityPrice[] prices, int fallback)
    {
        if (prices == null)
            return Mathf.Max(1, fallback);

        for (int i = 0; i < prices.Length; i++)
        {
            ShopRarityPrice rarityPrice = prices[i];
            if (rarityPrice != null && rarityPrice.Rarity == rarity)
                return rarityPrice.Price;
        }

        return Mathf.Max(1, fallback);
    }

    /// <summary>
    /// 按商品类型保存商店候选池，避免刷新时混用道具和遗物槽位。
    /// </summary>
    private sealed class ShopOfferPools
    {
        public readonly List<ShopOffer> ItemOffers = new List<ShopOffer>();
        public readonly List<ShopOffer> RelicOffers = new List<ShopOffer>();

        /// <summary>
        /// 是否存在任意可抽取商品。
        /// </summary>
        public bool HasAnyOffer => ItemOffers.Count > 0 || RelicOffers.Count > 0;
    }
}
