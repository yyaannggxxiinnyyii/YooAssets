using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 根据 Stage 房间门选项生成和清理场景门对象，当前作为独立骨架不接入旧战斗结算流程。
/// </summary>
public sealed class GameStageRoomDoorController : MonoBehaviour
{
    [Header("流程引用")]
    [Tooltip("Stage 房间流程控制器，用于获取门选项并提交玩家选择。")]
    [SerializeField] private GameStageProgressController stageProgressController;

    [Header("门生成")]
    [Tooltip("门对象预制体；为空时会创建临时门对象。")]
    [SerializeField] private StageRoomDoorView doorPrefab;
    [Tooltip("门对象生成父节点；为空时使用当前对象。")]
    [SerializeField] private Transform doorRoot;
    [Tooltip("单门选项生成时相对控制器的本地位置。")]
    [SerializeField] private Vector3 singleDoorLocalPosition = new Vector3(0f, 1.8f, 0f);
    [Tooltip("两个及以上门选项生成时，门之间的横向间距。")]
    [SerializeField] private float doorSpacing = 2.8f;

    [Header("玩家安全位置")]
    [Tooltip("需要在门生成后避让的玩家；为空时运行时自动查找当前玩家。")]
    [SerializeField] private GamePlayerController player;
    [Tooltip("门生成后玩家与门中心需要保持的最小距离，用于避免玩家正好站在门生成点被卡住。")]
    [SerializeField] private float playerDoorSafeRadius = 1.1f;
    [Tooltip("门存在非触发碰撞体时，在碰撞体包围范围外额外预留的安全距离。")]
    [SerializeField] private float doorColliderSafePadding = 0.25f;

    [Header("门外观")]
    [Tooltip("门外观配置列表，优先按 RoomOptionData.DoorVisualId 查找。")]
    [SerializeField] private StageRoomDoorVisualConfig[] visualConfigs = new StageRoomDoorVisualConfig[0];
    [Tooltip("普通战斗房默认门外观 ID。")]
    [SerializeField] private string normalDoorVisualId = "door_normal";
    [Tooltip("精英战斗房默认门外观 ID。")]
    [SerializeField] private string eliteDoorVisualId = "door_elite";
    [Tooltip("Boss 房默认门外观 ID。")]
    [SerializeField] private string bossDoorVisualId = "door_boss";
    [Tooltip("商店房默认门外观 ID。")]
    [SerializeField] private string shopDoorVisualId = "door_shop";
    [Tooltip("事件房默认门外观 ID。")]
    [SerializeField] private string eventDoorVisualId = "door_event";
    [Tooltip("宝藏房默认门外观 ID。")]
    [SerializeField] private string treasureDoorVisualId = "door_treasure";

    private readonly List<StageRoomDoorView> _activeDoors = new List<StageRoomDoorView>();

    /// <summary>
    /// 当前激活的门数量。
    /// </summary>
    public int ActiveDoorCount => _activeDoors.Count;

    private void Awake()
    {
        if (stageProgressController == null)
            stageProgressController = GetComponent<GameStageProgressController>();

        if (stageProgressController == null)
            stageProgressController = FindObjectOfType<GameStageProgressController>();

        if (doorRoot == null)
            doorRoot = transform;
    }

    /// <summary>
    /// 根据当前 Stage 节点规则生成门选项。
    /// </summary>
    public void SpawnDoorsForCurrentRoom()
    {
        if (stageProgressController == null)
        {
            Debug.LogWarning("[StageDoor] 缺少 GameStageProgressController，无法生成门。");
            return;
        }

        SpawnDoors(stageProgressController.RollCurrentRoomOptions());
    }

    /// <summary>
    /// 根据指定门选项列表生成门对象。
    /// </summary>
    /// <param name="roomOptions">门选项列表。</param>
    public void SpawnDoors(RoomOptionData[] roomOptions)
    {
        SpawnDoors(roomOptions, OnDoorSelected);
    }

    /// <summary>
    /// 根据指定门选项列表生成门对象，并使用外部传入的选择回调。
    /// </summary>
    /// <param name="roomOptions">门选项列表。</param>
    /// <param name="selectedCallback">门被选择时调用的回调；为空时使用默认 Stage 选门提交逻辑。</param>
    public void SpawnDoors(RoomOptionData[] roomOptions, Action<int, RoomOptionData> selectedCallback)
    {
        ClearDoors();
        if (roomOptions == null || roomOptions.Length <= 0)
            return;

        Action<int, RoomOptionData> resolvedCallback = selectedCallback ?? OnDoorSelected;
        for (int i = 0; i < roomOptions.Length; i++)
        {
            RoomOptionData roomOption = roomOptions[i];
            if (roomOption == null)
                continue;

            StageRoomDoorView doorView = CreateDoorView(i, roomOptions.Length);
            doorView.Initialize(i, roomOption, resolvedCallback, ResolveDoorVisualConfig(roomOption));
            _activeDoors.Add(doorView);
        }

        PushPlayerAwayFromSpawnedDoors();
    }

    /// <summary>
    /// 清理当前所有场景门。
    /// </summary>
    public void ClearDoors()
    {
        for (int i = _activeDoors.Count - 1; i >= 0; i--)
        {
            StageRoomDoorView doorView = _activeDoors[i];
            if (doorView == null)
                continue;

            doorView.Clear();
            Destroy(doorView.gameObject);
        }

        _activeDoors.Clear();
    }

    /// <summary>
    /// 设置后续门对象生成的父节点，房间 Prefab 切换后用于绑定当前房间的 DoorRoot。
    /// </summary>
    /// <param name="root">新的门生成父节点；为空时回退到当前控制器 Transform。</param>
    public void SetDoorRoot(Transform root)
    {
        doorRoot = root != null ? root : transform;
    }

    /// <summary>
    /// 创建一个门对象并放置到对应位置。
    /// </summary>
    /// <param name="optionIndex">门选项索引。</param>
    /// <param name="optionCount">门选项总数。</param>
    /// <returns>门视图组件。</returns>
    private StageRoomDoorView CreateDoorView(int optionIndex, int optionCount)
    {
        StageRoomDoorView doorView = doorPrefab != null
            ? Instantiate(doorPrefab, doorRoot)
            : CreateTemporaryDoorView();

        doorView.transform.SetParent(doorRoot, false);
        doorView.transform.localPosition = CalculateDoorLocalPosition(optionIndex, optionCount);
        doorView.transform.localRotation = Quaternion.identity;
        doorView.transform.localScale = Vector3.one;
        return doorView;
    }

    /// <summary>
    /// 创建一个无预制体时使用的临时门对象。
    /// </summary>
    /// <returns>门视图组件。</returns>
    private StageRoomDoorView CreateTemporaryDoorView()
    {
        GameObject doorObject = new GameObject("StageDoor");
        doorObject.transform.SetParent(doorRoot, false);
        SpriteRenderer spriteRenderer = doorObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 2;
        doorObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        return doorObject.AddComponent<StageRoomDoorView>();
    }

    /// <summary>
    /// 计算门在父节点下的本地位置。
    /// </summary>
    /// <param name="optionIndex">门选项索引。</param>
    /// <param name="optionCount">门选项总数。</param>
    /// <returns>门本地位置。</returns>
    private Vector3 CalculateDoorLocalPosition(int optionIndex, int optionCount)
    {
        if (optionCount <= 1)
            return singleDoorLocalPosition;

        float startX = -doorSpacing * (optionCount - 1) * 0.5f;
        return singleDoorLocalPosition + new Vector3(startX + doorSpacing * optionIndex, 0f, 0f);
    }

    /// <summary>
    /// 解析门选项应使用的门外观配置，优先使用门外观 ID，失败后按房间类型回退。
    /// </summary>
    /// <param name="roomOption">门选项数据。</param>
    /// <returns>匹配的门外观配置；没有配置时返回 null。</returns>
    private StageRoomDoorVisualConfig ResolveDoorVisualConfig(RoomOptionData roomOption)
    {
        if (roomOption == null)
            return null;

        StageRoomDoorVisualConfig config = FindVisualConfig(roomOption.DoorVisualId);
        if (config != null)
            return config;

        return FindVisualConfig(GetDefaultVisualId(roomOption.RoomType));
    }

    /// <summary>
    /// 按外观 ID 查找门外观配置。
    /// </summary>
    /// <param name="visualId">门外观 ID。</param>
    /// <returns>匹配的门外观配置；没有匹配时返回 null。</returns>
    private StageRoomDoorVisualConfig FindVisualConfig(string visualId)
    {
        if (string.IsNullOrWhiteSpace(visualId) || visualConfigs == null)
            return null;

        for (int i = 0; i < visualConfigs.Length; i++)
        {
            StageRoomDoorVisualConfig config = visualConfigs[i];
            if (config != null && string.Equals(config.VisualId, visualId, StringComparison.OrdinalIgnoreCase))
                return config;
        }

        return null;
    }

    /// <summary>
    /// 获取指定房间类型的默认门外观 ID。
    /// </summary>
    /// <param name="roomType">房间类型。</param>
    /// <returns>默认门外观 ID。</returns>
    private string GetDefaultVisualId(RoomType roomType)
    {
        switch (roomType)
        {
            case RoomType.CombatElite:
                return eliteDoorVisualId;

            case RoomType.Boss:
                return bossDoorVisualId;

            case RoomType.Shop:
                return shopDoorVisualId;

            case RoomType.Event:
                return eventDoorVisualId;

            case RoomType.Treasure:
                return treasureDoorVisualId;

            case RoomType.CombatNormal:
            default:
                return normalDoorVisualId;
        }
    }

    /// <summary>
    /// 门生成完成后把玩家推出门的安全范围，避免门碰撞体覆盖玩家导致卡死。
    /// </summary>
    private void PushPlayerAwayFromSpawnedDoors()
    {
        GamePlayerController targetPlayer = ResolvePlayer();
        if (targetPlayer == null || _activeDoors.Count <= 0)
            return;

        Vector3 playerPosition = targetPlayer.transform.position;
        bool moved = false;
        for (int i = 0; i < _activeDoors.Count; i++)
        {
            StageRoomDoorView doorView = _activeDoors[i];
            if (doorView == null)
                continue;

            moved |= PushPlayerAwayFromPoint(doorView.transform.position, Mathf.Max(0.05f, playerDoorSafeRadius), ref playerPosition);
            moved |= PushPlayerAwayFromDoorColliders(doorView, ref playerPosition);
        }

        if (moved)
            targetPlayer.transform.position = playerPosition;
    }

    /// <summary>
    /// 获取需要避让门生成的玩家引用。
    /// </summary>
    /// <returns>当前玩家控制器；不存在时返回 null。</returns>
    private GamePlayerController ResolvePlayer()
    {
        if (player != null)
            return player;

        player = FindObjectOfType<GamePlayerController>();
        return player;
    }

    /// <summary>
    /// 根据门上的非触发碰撞体包围范围修正玩家位置。
    /// </summary>
    /// <param name="doorView">需要检查的门视图。</param>
    /// <param name="playerPosition">玩家位置，会在需要时被修正。</param>
    /// <returns>玩家位置被修正时返回 true。</returns>
    private bool PushPlayerAwayFromDoorColliders(StageRoomDoorView doorView, ref Vector3 playerPosition)
    {
        Collider2D[] colliders = doorView.GetComponentsInChildren<Collider2D>(true);
        bool moved = false;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D doorCollider = colliders[i];
            if (doorCollider == null || !doorCollider.enabled || doorCollider.isTrigger)
                continue;

            Bounds bounds = doorCollider.bounds;
            float safeRadius = Mathf.Max(bounds.extents.x, bounds.extents.y) + Mathf.Max(0.05f, doorColliderSafePadding);
            moved |= PushPlayerAwayFromPoint(bounds.center, safeRadius, ref playerPosition);
        }

        return moved;
    }

    /// <summary>
    /// 当玩家处于指定中心点安全半径内时，把玩家移动到安全半径外侧。
    /// </summary>
    /// <param name="center">需要避让的中心点。</param>
    /// <param name="safeRadius">最小安全距离。</param>
    /// <param name="playerPosition">玩家位置，会在需要时被修正。</param>
    /// <returns>玩家位置被修正时返回 true。</returns>
    private bool PushPlayerAwayFromPoint(Vector3 center, float safeRadius, ref Vector3 playerPosition)
    {
        Vector2 delta = (Vector2)(playerPosition - center);
        float distance = delta.magnitude;
        if (distance >= safeRadius)
            return false;

        Vector2 pushDirection = distance > 0.001f ? delta / distance : Vector2.up;
        Vector2 pushedPosition = (Vector2)center + pushDirection * safeRadius;
        playerPosition.x = pushedPosition.x;
        playerPosition.y = pushedPosition.y;
        return true;
    }

    /// <summary>
    /// 处理门选择事件，提交给 Stage 进度控制器后清理其他门。
    /// </summary>
    /// <param name="optionIndex">门选项索引。</param>
    /// <param name="roomOption">门选项数据。</param>
    private void OnDoorSelected(int optionIndex, RoomOptionData roomOption)
    {
        if (stageProgressController != null)
            stageProgressController.TryChooseRoomOption(optionIndex, roomOption);

        ClearDoors();
    }
}

/// <summary>
/// 定义门外观 ID 对应的 Sprite、颜色、Tooltip 偏移和动画控制器。
/// </summary>
[Serializable]
public sealed class StageRoomDoorVisualConfig
{
    [Header("标识")]
    [Tooltip("门外观 ID，需要与 RoomOptionData.DoorVisualId 或默认门 ID 匹配。")]
    [SerializeField] private string visualId = "door_normal";

    [Header("显示")]
    [Tooltip("门主体 Sprite；为空时保留门预制体默认 Sprite。")]
    [SerializeField] private Sprite sprite;
    [Tooltip("门主体颜色。")]
    [SerializeField] private Color tintColor = Color.white;
    [Tooltip("门动画控制器；为空时不覆盖门预制体动画。")]
    [SerializeField] private RuntimeAnimatorController animatorController;

    [Header("Tooltip")]
    [Tooltip("是否覆盖门预制体上的 Tooltip 偏移。")]
    [SerializeField] private bool overrideTooltipOffset;
    [Tooltip("覆盖后的 Tooltip 本地偏移。")]
    [SerializeField] private Vector3 tooltipOffset = new Vector3(0f, 1.1f, 0f);

    /// <summary>
    /// 门外观 ID。
    /// </summary>
    public string VisualId => visualId;

    /// <summary>
    /// 门主体 Sprite。
    /// </summary>
    public Sprite Sprite => sprite;

    /// <summary>
    /// 门主体颜色。
    /// </summary>
    public Color TintColor => tintColor;

    /// <summary>
    /// 门动画控制器。
    /// </summary>
    public RuntimeAnimatorController AnimatorController => animatorController;

    /// <summary>
    /// 是否覆盖 Tooltip 偏移。
    /// </summary>
    public bool OverrideTooltipOffset => overrideTooltipOffset;

    /// <summary>
    /// Tooltip 本地偏移。
    /// </summary>
    public Vector3 TooltipOffset => tooltipOffset;
}
