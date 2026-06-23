using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 管理 HUD 中红血槽位和临时特殊血槽位的心形显示。
/// </summary>
public sealed class GameHeartHealthView : MonoBehaviour
{
    private const int PointsPerHeart = 2;

    /// <summary>
    /// 记录特殊血类型与对应心槽 prefab 的绑定关系。
    /// </summary>
    [Serializable]
    public struct GameHeartSlotPrefabEntry
    {
        [SerializeField] private SpecialHealthType type;
        [SerializeField] private GameHeartSlotView prefab;

        /// <summary>
        /// 特殊血类型。
        /// </summary>
        public SpecialHealthType Type => type;

        /// <summary>
        /// 特殊血心槽 prefab。
        /// </summary>
        public GameHeartSlotView Prefab => prefab;
    }

    [Header("心槽图片")]
    [Tooltip("满心状态使用的图片。")]
    [SerializeField] private Sprite fullHeartSprite;
    [Tooltip("半心状态使用的图片。")]
    [SerializeField] private Sprite halfHeartSprite;
    [Tooltip("空心槽状态使用的图片。")]
    [SerializeField] private Sprite emptyHeartSprite;

    [Header("心槽引用")]
    [Tooltip("所有心槽实例的父节点，为空时使用当前对象。")]
    [SerializeField] private Transform heartSlotRoot;
    [Tooltip("红血心槽 prefab。")]
    [SerializeField] private GameHeartSlotView redHeartSlotPrefab;
    [Tooltip("特殊血类型对应的心槽 prefab。")]
    [SerializeField] private GameHeartSlotPrefabEntry[] specialHeartSlotPrefabs;

    [Header("兼容旧场景")]
    [Tooltip("旧版场景中预先搭建的心槽图片，仅用于自动迁移为心槽控制脚本。")]
    [SerializeField] private Image[] heartImages;

    private readonly List<GameHeartSlotView> _redHeartSlots = new List<GameHeartSlotView>();
    private readonly List<GameHeartSlotView> _specialHeartSlots = new List<GameHeartSlotView>();
    private int _currentHealth = -1;
    private int _maxHealth = -1;
    private bool _initialized;

    /// <summary>
    /// 同时刷新红血槽位和临时特殊血槽位。
    /// </summary>
    /// <param name="currentHealth">当前红血点数。</param>
    /// <param name="maxHealth">红血最大点数。</param>
    public void Refresh(int currentHealth, int maxHealth)
    {
        Refresh(currentHealth, maxHealth, null);
    }

    /// <summary>
    /// 同时刷新红血槽位和临时特殊血槽位。
    /// </summary>
    /// <param name="currentHealth">当前红血点数。</param>
    /// <param name="maxHealth">红血最大点数。</param>
    /// <param name="segments">临时特殊血量段。</param>
    public void Refresh(int currentHealth, int maxHealth, IReadOnlyList<SpecialHealthSegment> segments)
    {
        _maxHealth = Mathf.Max(0, maxHealth);
        _currentHealth = Mathf.Clamp(currentHealth, 0, _maxHealth);
        SetMaxHealth(_maxHealth);
        RefreshRedHealth(_currentHealth, _maxHealth);
        RefreshSpecialHealth(segments);
    }

    /// <summary>
    /// 设置红血上限，并按上限创建或隐藏红心槽。
    /// </summary>
    /// <param name="maxHealth">红血最大点数。</param>
    public void SetMaxHealth(int maxHealth)
    {
        _maxHealth = Mathf.Max(0, maxHealth);
        if (_currentHealth < 0)
            _currentHealth = _maxHealth;

        _currentHealth = Mathf.Clamp(_currentHealth, 0, _maxHealth);
        EnsureRedHeartSlots(GetHeartCount(_maxHealth));
    }

    /// <summary>
    /// 刷新红血槽位的空、半、满状态。
    /// </summary>
    /// <param name="currentHealth">当前红血点数。</param>
    /// <param name="maxHealth">红血最大点数。</param>
    public void RefreshRedHealth(int currentHealth, int maxHealth)
    {
        _maxHealth = Mathf.Max(0, maxHealth);
        _currentHealth = Mathf.Clamp(currentHealth, 0, _maxHealth);
        EnsureRedHeartSlots(GetHeartCount(_maxHealth));

        int visibleHeartCount = GetHeartCount(_maxHealth);
        for (int i = 0; i < _redHeartSlots.Count; i++)
        {
            GameHeartSlotView slot = _redHeartSlots[i];
            if (slot == null)
                continue;

            bool visible = i < visibleHeartCount;
            slot.gameObject.SetActive(visible);
            if (!visible)
                continue;

            int maxPoints = Mathf.Clamp(_maxHealth - i * PointsPerHeart, 1, PointsPerHeart);
            int fillPoints = Mathf.Clamp(_currentHealth - i * PointsPerHeart, 0, maxPoints);
            slot.SetSprites(fullHeartSprite, halfHeartSprite, emptyHeartSprite);
            slot.SetRedHeart();
            slot.SetMaxPoints(maxPoints);
            slot.SetFillPoints(fillPoints);
        }
    }

    /// <summary>
    /// 刷新临时特殊血槽位显示。
    /// </summary>
    /// <param name="segments">临时特殊血量段。</param>
    public void RefreshSpecialHealth(IReadOnlyList<SpecialHealthSegment> segments)
    {
        HideSpecialHeartSlots();
        if (segments == null)
            return;

        int slotIndex = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            SpecialHealthSegment segment = segments[i];
            int points = Mathf.Max(0, segment.Points);
            int heartCount = GetHeartCount(points);
            for (int heartIndex = 0; heartIndex < heartCount; heartIndex++)
            {
                GameHeartSlotView slot = GetOrCreateSpecialHeartSlot(slotIndex, segment.Type);
                if (slot == null)
                    continue;

                int maxPoints = Mathf.Clamp(points - heartIndex * PointsPerHeart, 1, PointsPerHeart);
                int fillPoints = maxPoints;
                slot.gameObject.SetActive(true);
                slot.SetSprites(fullHeartSprite, halfHeartSprite, emptyHeartSprite);
                slot.SetHeartType(segment.Type);
                slot.SetMaxPoints(maxPoints);
                slot.SetFillPoints(fillPoints);
                slot.transform.SetAsLastSibling();
                slotIndex++;
            }
        }
    }

    /// <summary>
    /// 设置当前红血点数并刷新红心槽。
    /// </summary>
    /// <param name="currentHealth">当前红血点数。</param>
    public void SetCurrentHealth(int currentHealth)
    {
        if (_maxHealth < 0)
            _maxHealth = Mathf.Max(0, currentHealth);

        RefreshRedHealth(currentHealth, _maxHealth);
    }

    /// <summary>
    /// 初始化已有或默认心槽配置。
    /// </summary>
    private void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        if (heartSlotRoot == null)
            heartSlotRoot = transform;

        AddLayoutGroupIfMissing();
        RegisterExistingRedSlots();
        if (redHeartSlotPrefab == null && _redHeartSlots.Count > 0)
            redHeartSlotPrefab = _redHeartSlots[0];
    }

    /// <summary>
    /// 将旧场景中的 Image 心槽迁移为 GameHeartSlotView。
    /// </summary>
    private void RegisterExistingRedSlots()
    {
        if (heartImages != null)
        {
            for (int i = 0; i < heartImages.Length; i++)
                RegisterExistingRedSlot(heartImages[i]);
        }

        if (_redHeartSlots.Count > 0)
            return;

        Image[] childImages = GetHeartSlotRoot().GetComponentsInChildren<Image>(true);
        for (int i = 0; i < childImages.Length; i++)
            RegisterExistingRedSlot(childImages[i]);
    }

    /// <summary>
    /// 注册一个已有图片为红心槽。
    /// </summary>
    /// <param name="heartImage">心槽图片。</param>
    private void RegisterExistingRedSlot(Image heartImage)
    {
        if (heartImage == null)
            return;

        GameHeartSlotView slot = heartImage.GetComponent<GameHeartSlotView>();
        if (slot == null)
            slot = heartImage.gameObject.AddComponent<GameHeartSlotView>();

        if (_redHeartSlots.Contains(slot))
            return;

        ConfigureSlotRect(slot);
        slot.SetSprites(fullHeartSprite, halfHeartSprite, emptyHeartSprite);
        slot.SetRedHeart();
        _redHeartSlots.Add(slot);
    }

    /// <summary>
    /// 确保红心槽数量满足当前红血上限。
    /// </summary>
    /// <param name="visibleHeartCount">需要显示的红心槽数量。</param>
    private void EnsureRedHeartSlots(int visibleHeartCount)
    {
        Initialize();
        while (_redHeartSlots.Count < visibleHeartCount)
        {
            GameHeartSlotView slot = CreateHeartSlot(redHeartSlotPrefab, "HeartSlot_Red");
            if (slot == null)
                return;

            _redHeartSlots.Add(slot);
        }
    }

    /// <summary>
    /// 获取或创建指定索引的特殊血心槽。
    /// </summary>
    /// <param name="slotIndex">特殊血心槽索引。</param>
    /// <param name="type">特殊血类型。</param>
    /// <returns>特殊血心槽。</returns>
    private GameHeartSlotView GetOrCreateSpecialHeartSlot(int slotIndex, SpecialHealthType type)
    {
        while (_specialHeartSlots.Count <= slotIndex)
        {
            GameHeartSlotView slot = CreateHeartSlot(GetSpecialPrefab(type), $"HeartSlot_{type}");
            if (slot == null)
                return null;

            _specialHeartSlots.Add(slot);
        }

        return _specialHeartSlots[slotIndex];
    }

    /// <summary>
    /// 创建一个心槽实例。
    /// </summary>
    /// <param name="prefab">心槽 prefab。</param>
    /// <param name="namePrefix">实例名称前缀。</param>
    /// <returns>心槽实例。</returns>
    private GameHeartSlotView CreateHeartSlot(GameHeartSlotView prefab, string namePrefix)
    {
        Transform root = GetHeartSlotRoot();
        GameHeartSlotView slot = null;
        if (prefab != null)
            slot = Instantiate(prefab, root);
        else
            slot = CreateFallbackHeartSlot(root);

        if (slot == null)
            return null;

        slot.name = $"{namePrefix}_{GetHeartSlotRoot().childCount}";
        ConfigureSlotRect(slot);
        slot.SetSprites(fullHeartSprite, halfHeartSprite, emptyHeartSprite);
        return slot;
    }

    /// <summary>
    /// 创建缺省心槽，用于未配置 prefab 的旧场景。
    /// </summary>
    /// <param name="root">父节点。</param>
    /// <returns>心槽实例。</returns>
    private GameHeartSlotView CreateFallbackHeartSlot(Transform root)
    {
        GameObject slotObject = new GameObject("HeartSlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(GameHeartSlotView));
        slotObject.transform.SetParent(root, false);

        Image image = slotObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.sprite = fullHeartSprite != null ? fullHeartSprite : emptyHeartSprite;

        return slotObject.GetComponent<GameHeartSlotView>();
    }

    /// <summary>
    /// 隐藏所有临时特殊血槽位。
    /// </summary>
    private void HideSpecialHeartSlots()
    {
        for (int i = 0; i < _specialHeartSlots.Count; i++)
        {
            if (_specialHeartSlots[i] != null)
                _specialHeartSlots[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 获取特殊血类型对应 prefab。
    /// </summary>
    /// <param name="type">特殊血类型。</param>
    /// <returns>特殊血心槽 prefab。</returns>
    private GameHeartSlotView GetSpecialPrefab(SpecialHealthType type)
    {
        if (specialHeartSlotPrefabs != null)
        {
            for (int i = 0; i < specialHeartSlotPrefabs.Length; i++)
            {
                if (specialHeartSlotPrefabs[i].Type == type && specialHeartSlotPrefabs[i].Prefab != null)
                    return specialHeartSlotPrefabs[i].Prefab;
            }
        }

        return redHeartSlotPrefab;
    }

    /// <summary>
    /// 获取红血上限对应心槽数量。
    /// </summary>
    /// <param name="healthPoints">血量点数。</param>
    /// <returns>心槽数量。</returns>
    private int GetHeartCount(int healthPoints)
    {
        return Mathf.CeilToInt(Mathf.Max(0, healthPoints) / (float)PointsPerHeart);
    }

    /// <summary>
    /// 获取心槽父节点。
    /// </summary>
    /// <returns>心槽父节点。</returns>
    private Transform GetHeartSlotRoot()
    {
        return heartSlotRoot != null ? heartSlotRoot : transform;
    }

    /// <summary>
    /// 给心槽补齐稳定布局尺寸。
    /// </summary>
    /// <param name="slot">心槽实例。</param>
    private void ConfigureSlotRect(GameHeartSlotView slot)
    {
        RectTransform rectTransform = slot.GetComponent<RectTransform>();
        if (rectTransform != null)
            rectTransform.sizeDelta = new Vector2(60f, 60f);

        LayoutElement layoutElement = slot.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = slot.gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredWidth = 60f;
        layoutElement.preferredHeight = 60f;
        layoutElement.minWidth = 60f;
        layoutElement.minHeight = 60f;
    }

    /// <summary>
    /// 确保心槽父节点拥有横向自动布局。
    /// </summary>
    private void AddLayoutGroupIfMissing()
    {
        Transform root = GetHeartSlotRoot();
        if (root == null || root.GetComponent<HorizontalLayoutGroup>() != null)
            return;

        HorizontalLayoutGroup layoutGroup = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.MiddleLeft;
        layoutGroup.childControlWidth = false;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = 6f;
    }
}
