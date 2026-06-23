using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 表现场景中的房间门选项，负责靠近提示和交互选择事件。
/// </summary>
public sealed class StageRoomDoorView : MonoBehaviour
{
    private const int TemporaryDoorSpriteWidth = 32;
    private const int TemporaryDoorSpriteHeight = 48;

    private static Sprite _temporaryDoorSprite;

    [Header("交互")]
    [Tooltip("玩家靠近门后允许交互的距离。")]
    [SerializeField] private float interactRadius = 1.25f;
    [Tooltip("触发门交互的按键。")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("显示")]
    [Tooltip("门主体的 SpriteRenderer；为空时会自动查找或创建。")]
    [SerializeField] private SpriteRenderer doorRenderer;
    [Tooltip("门默认显示图标；为空时仅使用颜色方块。")]
    [SerializeField] private Sprite defaultDoorSprite;
    [Tooltip("门 Tooltip 文本；为空时会自动创建世界空间 TextMeshPro。")]
    [SerializeField] private TextMeshPro tooltipText;
    [Tooltip("Tooltip 相对门中心的本地偏移。")]
    [SerializeField] private Vector3 tooltipOffset = new Vector3(0f, 1.1f, 0f);
    [Tooltip("门 Animator；为空时会自动查找，用于门外观配置覆盖动画控制器。")]
    [SerializeField] private Animator doorAnimator;

    private RoomOptionData _roomOption;
    private StageRoomDoorVisualConfig _visualConfig;
    private int _optionIndex = -1;
    private Transform _player;
    private Action<int, RoomOptionData> _selectedCallback;
    private bool _playerInRange;

    /// <summary>
    /// 门选项索引。
    /// </summary>
    public int OptionIndex => _optionIndex;

    /// <summary>
    /// 门绑定的房间选项。
    /// </summary>
    public RoomOptionData RoomOption => _roomOption;

    private void Awake()
    {
        EnsureDoorRenderer();
        EnsureTooltipText();
        SetTooltipVisible(false);
    }

    private void Update()
    {
        if (_roomOption == null)
        {
            SetPlayerInRange(false);
            return;
        }

        RefreshPlayerRangeByDistance();
        if (!_playerInRange)
            return;

        if (Input.GetKeyDown(interactKey))
            _selectedCallback?.Invoke(_optionIndex, _roomOption);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        GamePlayerController player = other.GetComponentInParent<GamePlayerController>();
        _player = player != null ? player.transform : other.transform;
        SetPlayerInRange(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_player == null || other.GetComponentInParent<GamePlayerController>() == null)
            return;

        _player = null;
        SetPlayerInRange(false);
    }

    /// <summary>
    /// 在 Scene 视图中绘制门的距离交互范围。
    /// </summary>
    private void OnDrawGizmos()
    {
        GameRadiusGizmoUtility.DrawRadius(transform.position, Mathf.Max(0.1f, interactRadius), new Color(0.25f, 0.85f, 1f, 0.85f), "Door Interact");
    }

    /// <summary>
    /// 绑定门选项数据和选择回调。
    /// </summary>
    /// <param name="optionIndex">门选项索引。</param>
    /// <param name="roomOption">房间选项数据。</param>
    /// <param name="selectedCallback">选择回调。</param>
    public void Initialize(int optionIndex, RoomOptionData roomOption, Action<int, RoomOptionData> selectedCallback)
    {
        Initialize(optionIndex, roomOption, selectedCallback, null);
    }

    /// <summary>
    /// 绑定门选项数据、选择回调和门外观配置。
    /// </summary>
    /// <param name="optionIndex">门选项索引。</param>
    /// <param name="roomOption">房间选项数据。</param>
    /// <param name="selectedCallback">选择回调。</param>
    /// <param name="visualConfig">门外观配置；为空时使用房间类型默认表现。</param>
    public void Initialize(
        int optionIndex,
        RoomOptionData roomOption,
        Action<int, RoomOptionData> selectedCallback,
        StageRoomDoorVisualConfig visualConfig)
    {
        _optionIndex = optionIndex;
        _roomOption = roomOption;
        _selectedCallback = selectedCallback;
        _visualConfig = visualConfig;
        ApplyVisual();
        RefreshTooltipText();
    }

    /// <summary>
    /// 清理门运行时状态，供对象复用或销毁前调用。
    /// </summary>
    public void Clear()
    {
        _roomOption = null;
        _visualConfig = null;
        _optionIndex = -1;
        _player = null;
        _selectedCallback = null;
        SetPlayerInRange(false);
    }

    /// <summary>
    /// 使用距离检测刷新玩家是否处于交互范围，避免只依赖物理触发事件。
    /// </summary>
    private void RefreshPlayerRangeByDistance()
    {
        if (_player == null)
            CachePlayerTransform();

        bool isInRange = _player != null &&
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
    /// 设置玩家交互范围状态，并同步 Tooltip 显隐。
    /// </summary>
    /// <param name="isInRange">玩家是否在交互范围内。</param>
    private void SetPlayerInRange(bool isInRange)
    {
        if (_playerInRange == isInRange)
            return;

        _playerInRange = isInRange;
        SetTooltipVisible(_playerInRange);
    }

    /// <summary>
    /// 应用门选项对应的基础颜色和名称。
    /// </summary>
    private void ApplyVisual()
    {
        EnsureDoorRenderer();
        EnsureDoorAnimator();
        if (doorRenderer == null)
            return;

        Sprite visualSprite = _visualConfig != null ? _visualConfig.Sprite : null;
        if (visualSprite != null)
            doorRenderer.sprite = visualSprite;
        else if (defaultDoorSprite != null)
            doorRenderer.sprite = defaultDoorSprite;
        else if (doorRenderer.sprite == null)
            doorRenderer.sprite = GetTemporaryDoorSprite();

        Color defaultColor = GetRoomTypeColor(_roomOption != null ? _roomOption.RoomType : RoomType.CombatNormal);
        doorRenderer.color = _visualConfig != null ? _visualConfig.TintColor : defaultColor;
        if (doorAnimator != null && _visualConfig != null && _visualConfig.AnimatorController != null)
            doorAnimator.runtimeAnimatorController = _visualConfig.AnimatorController;

        gameObject.name = _roomOption != null ? $"StageDoor_{_roomOption.RoomType}_{_optionIndex}" : "StageDoor";
    }

    /// <summary>
    /// 刷新门 Tooltip 文本内容。
    /// </summary>
    private void RefreshTooltipText()
    {
        EnsureTooltipText();
        if (tooltipText == null || _roomOption == null)
            return;

        tooltipText.text = $"E 进入{_roomOption.DisplayName}\n{_roomOption.TooltipDescription}";
        tooltipText.transform.localPosition = _visualConfig != null && _visualConfig.OverrideTooltipOffset
            ? _visualConfig.TooltipOffset
            : tooltipOffset;
    }

    /// <summary>
    /// 设置 Tooltip 显隐。
    /// </summary>
    /// <param name="visible">是否显示。</param>
    private void SetTooltipVisible(bool visible)
    {
        if (tooltipText != null)
            tooltipText.gameObject.SetActive(visible);
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
    /// 确保门主体渲染器存在。
    /// </summary>
    private void EnsureDoorRenderer()
    {
        if (doorRenderer == null)
            doorRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (doorRenderer != null)
            return;

        GameObject rendererObject = new GameObject("DoorVisual");
        rendererObject.transform.SetParent(transform, false);
        doorRenderer = rendererObject.AddComponent<SpriteRenderer>();
        doorRenderer.sortingOrder = 2;
    }

    /// <summary>
    /// 确保门动画组件引用存在。
    /// </summary>
    private void EnsureDoorAnimator()
    {
        if (doorAnimator == null)
            doorAnimator = GetComponentInChildren<Animator>(true);
    }

    /// <summary>
    /// 获取临时门形 Sprite，便于未配置门预制体时仍能看到门对象。
    /// </summary>
    /// <returns>临时门 Sprite。</returns>
    private Sprite GetTemporaryDoorSprite()
    {
        if (_temporaryDoorSprite != null)
            return _temporaryDoorSprite;

        Texture2D texture = new Texture2D(TemporaryDoorSpriteWidth, TemporaryDoorSpriteHeight);
        texture.filterMode = FilterMode.Point;

        for (int y = 0; y < TemporaryDoorSpriteHeight; y++)
        {
            for (int x = 0; x < TemporaryDoorSpriteWidth; x++)
            {
                bool isFrame = x <= 2 || x >= TemporaryDoorSpriteWidth - 3 || y <= 2 || y >= TemporaryDoorSpriteHeight - 3;
                bool isHandle = x >= TemporaryDoorSpriteWidth - 9 && x <= TemporaryDoorSpriteWidth - 6 && y >= TemporaryDoorSpriteHeight / 2 - 2 && y <= TemporaryDoorSpriteHeight / 2 + 2;
                texture.SetPixel(x, y, isFrame || isHandle ? Color.white : new Color(1f, 1f, 1f, 0.78f));
            }
        }

        texture.Apply();
        _temporaryDoorSprite = Sprite.Create(
            texture,
            new Rect(0, 0, TemporaryDoorSpriteWidth, TemporaryDoorSpriteHeight),
            new Vector2(0.5f, 0f),
            32f);
        return _temporaryDoorSprite;
    }

    /// <summary>
    /// 确保 Tooltip 文本存在。
    /// </summary>
    private void EnsureTooltipText()
    {
        if (tooltipText == null)
            tooltipText = GetComponentInChildren<TextMeshPro>(true);

        if (tooltipText == null)
        {
            GameObject tooltipObject = new GameObject("DoorTooltip");
            tooltipObject.transform.SetParent(transform, false);
            tooltipText = tooltipObject.AddComponent<TextMeshPro>();
            tooltipText.alignment = TextAlignmentOptions.Center;
            tooltipText.fontSize = 0.32f;
            tooltipText.enableWordWrapping = false;
            tooltipText.sortingOrder = 100;
        }

        tooltipText.transform.localPosition = tooltipOffset;
    }

    /// <summary>
    /// 获取房间类型对应的临时门颜色。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>门显示颜色。</returns>
    private Color GetRoomTypeColor(RoomType roomType)
    {
        switch (roomType)
        {
            case RoomType.CombatElite:
                return new Color(0.95f, 0.22f, 0.18f);

            case RoomType.Boss:
                return new Color(0.42f, 0.03f, 0.05f);

            case RoomType.Shop:
                return new Color(1f, 0.78f, 0.18f);

            case RoomType.Event:
                return new Color(0.45f, 0.35f, 1f);

            case RoomType.Treasure:
                return new Color(1f, 0.9f, 0.28f);

            case RoomType.CombatNormal:
            default:
                return new Color(0.62f, 0.42f, 0.26f);
        }
    }
}
